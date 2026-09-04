using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;

namespace KiwiDX;

public sealed class KiwiClient : IAsyncDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private ClientWebSocket? audioSocket;
    private ClientWebSocket? waterfallSocket;
    private CancellationTokenSource? cancellation;
    private WaveOutEvent? audioOutput;
    private BufferedWaveProvider? audioBuffer;
    private System.Threading.Timer? statsTimer;
    private System.Threading.Timer? keepAliveTimer;
    private System.Threading.Timer? watchdogTimer;
    private TaskCompletionSource<bool> audioReady = NewSignal();
    private TaskCompletionSource<bool> waterfallReady = NewSignal();
    private TaskCompletionSource<string> connectionRejected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long lastAudioDataTicks;
    private long lastWaterfallDataTicks;
    private int audioPlaybackRequested;
    private bool connectionEstablished;
    private int handlingConnectionLoss;
    private readonly object recordingSync = new();
    private WaveFileWriter? recordingWriter;
    private string? recordingTempPath;
    private readonly SemaphoreSlim audioSendLock = new(1, 1);
    private readonly SemaphoreSlim waterfallSendLock = new(1, 1);
    private double frequencyHz;
    private string currentMode = "usb";
    private int lowCut = 300;
    private int highCut = 2700;
    private int currentBandwidth = 2400;
    private int waterfallZoom = 11;
    private double waterfallCenterHz = 7_100_000;
    private int waterfallMinimumDb = -150;
    private int waterfallMaximumDb = -50;
    private int audioAdpcmIndex;
    private int audioAdpcmValue;
    private static readonly int[] AdpcmIndexAdjust = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
    private static readonly int[] AdpcmSteps = { 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767 };
    public bool IsConnected => audioSocket?.State == WebSocketState.Open;
    public bool IsRecording { get { lock (recordingSync) return recordingWriter is not null; } }
    public event EventHandler<byte[]>? WaterfallLine;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? Error;
    public event EventHandler<string>? Log;
    public event EventHandler<string>? ServerInfoChanged;
    private long audioPackets;
    private long waterfallPackets;
    private long audioBytes;
    private long waterfallBytes;
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ConnectAsync(string input, double frequency, string mode, int bandwidth)
    {
        var baseUri = new Uri(input.Contains("://", StringComparison.Ordinal) ? input : "http://" + input);
        var scheme = baseUri.Scheme is "https" or "wss" ? "wss" : "ws";
        var port = baseUri.IsDefaultPort ? (scheme == "wss" ? 443 : 80) : baseUri.Port;
        // KiwiSDR parses this path component as an unsigned 32-bit client id.
        var timestamp = unchecked((uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Environment.ProcessId)).ToString();
        var audioEndpoint = new UriBuilder(scheme, baseUri.Host, port, $"/ws/kiwi/{timestamp}/SND").Uri;
        var waterfallEndpoint = new UriBuilder(scheme, baseUri.Host, port, $"/ws/kiwi/{timestamp}/W/F").Uri;
        cancellation = new CancellationTokenSource();
        audioReady = NewSignal();
        waterfallReady = NewSignal();
        connectionRejected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionEstablished = false;
        handlingConnectionLoss = 0;
        audioPackets = waterfallPackets = audioBytes = waterfallBytes = 0;
        audioAdpcmIndex = audioAdpcmValue = 0;
        Interlocked.Exchange(ref audioPlaybackRequested, 0);
        frequencyHz = frequency;
        currentMode = NormalizeMode(mode);
        currentBandwidth = bandwidth;
        (lowCut, highCut) = GetPassband(currentMode, currentBandwidth);
        waterfallCenterHz = frequency;
        audioSocket = new ClientWebSocket();
        waterfallSocket = new ClientWebSocket();
        StatusChanged?.Invoke(this, "Connecting...");
        Log?.Invoke(this, $"Connecting audio to {audioEndpoint}");
        Log?.Invoke(this, $"Connecting waterfall to {waterfallEndpoint}");
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
        {
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await audioSocket.ConnectAsync(audioEndpoint, connectTimeout.Token);
                await waterfallSocket.ConnectAsync(waterfallEndpoint, connectTimeout.Token);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new TimeoutException("Receiver temporarily unavailable: the connection timed out.");
            }
        }
        _ = LoadServerInfoAsync(baseUri);
        _ = ReceiveLoop(waterfallSocket, true, cancellation.Token);
        _ = ReceiveLoop(audioSocket, false, cancellation.Token);
        await SendAsync(audioSocket, audioSendLock, "SET auth t=kiwi p=");
        await SendAsync(waterfallSocket, waterfallSendLock, "SET auth t=kiwi p=");
        await SendTuningAsync();
        await SendAsync(audioSocket, audioSendLock, "SET compression=0");
        await SendAsync(audioSocket, audioSendLock, "SET little-endian");
        audioBuffer = new BufferedWaveProvider(new WaveFormat(12000, 16, 1)) { DiscardOnBufferOverflow = true };
        audioOutput = new WaveOutEvent();
        audioOutput.Init(audioBuffer);
        audioOutput.Pause();
        await ConfigureWaterfallAsync(11, frequency);
        var ready = Task.WhenAll(audioReady.Task, waterfallReady.Task);
        var timeout = Task.Delay(TimeSpan.FromSeconds(12), cancellation.Token);
        var completed = await Task.WhenAny(ready, connectionRejected.Task, timeout);
        if (completed == connectionRejected.Task) throw new InvalidOperationException(await connectionRejected.Task);
        if (completed == timeout) throw new TimeoutException("Receiver temporarily unavailable: it did not confirm the audio and waterfall channels.");
        await ready;
        connectionEstablished = true;
        var now = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref lastAudioDataTicks, now);
        Interlocked.Exchange(ref lastWaterfallDataTicks, now);
        statsTimer = new System.Threading.Timer(_ => Log?.Invoke(this, $"Status: WF {waterfallPackets} packets/{waterfallBytes} bytes; audio {audioPackets} packets/{audioBytes} bytes; buffer {audioBuffer?.BufferedBytes ?? 0} bytes"), null, 1000, 1000);
        keepAliveTimer = new System.Threading.Timer(_ => { SendText(audioSocket, audioSendLock, "SET keepalive"); SendText(waterfallSocket, waterfallSendLock, "SET keepalive"); }, null, 5000, 5000);
        watchdogTimer = new System.Threading.Timer(_ => CheckConnectionHealth(), null, 5000, 5000);
        StatusChanged?.Invoke(this, "Connected");
        Log?.Invoke(this, "WebSockets open: audio and waterfall");
    }

    public void SetFrequency(double frequency) { frequencyHz = Math.Clamp(frequency, 10_000, 30_000_000); SendTuning(); }
    public void SetMode(string mode) { currentMode = NormalizeMode(mode); (lowCut, highCut) = GetPassband(currentMode, currentBandwidth); SendTuning(); }
    public void SetBandwidth(int bandwidth) { currentBandwidth = bandwidth; (lowCut, highCut) = GetPassband(currentMode, currentBandwidth); SendTuning(); }
    public void SetWaterfallView(int zoom, double centerFrequency)
    {
        waterfallZoom = Math.Clamp(zoom, 0, 14);
        waterfallCenterHz = Math.Clamp(centerFrequency, 0, 30_000_000);
        SendText(waterfallSocket, waterfallSendLock, $"SET zoom={waterfallZoom} cf={waterfallCenterHz / 1_000:0.000}");
    }
    public void SetWaterfallRange(int minimumDb, int maximumDb)
    {
        waterfallMinimumDb = minimumDb;
        waterfallMaximumDb = maximumDb;
        SendText(waterfallSocket, waterfallSendLock, $"SET maxdb={waterfallMaximumDb} mindb={waterfallMinimumDb}");
    }
    public void PlayAudio()
    {
        if (audioOutput is null) return;
        Interlocked.Exchange(ref lastAudioDataTicks, DateTime.UtcNow.Ticks);
        Interlocked.Exchange(ref audioPlaybackRequested, 1);
        audioOutput.Play();
        SendText(audioSocket, audioSendLock, "SET reinit");
        SendText(audioSocket, audioSendLock, "SET agc=1 hang=0 thresh=-100 slope=6 decay=1000 manGain=50");
        SendText(audioSocket, audioSendLock, "SET squelch=0 max=0");
        SendText(audioSocket, audioSendLock, "SET little-endian");
        Log?.Invoke(this, "Audio requested: reinit, AGC and open squelch");
        StatusChanged?.Invoke(this, "Audio playing");
    }
    public void StopAudio()
    {
        Interlocked.Exchange(ref audioPlaybackRequested, 0);
        audioOutput?.Pause();
        StatusChanged?.Invoke(this, "Audio stopped");
    }
    public void SetVolume(float volume) { if (audioOutput is not null) audioOutput.Volume = Math.Clamp(volume, 0, 1); }
    public void StartRecording()
    {
        lock (recordingSync)
        {
            if (recordingWriter is not null) return;
            recordingTempPath = Path.Combine(Path.GetTempPath(), $"KiwiDX_{Guid.NewGuid():N}.wav");
            recordingWriter = new WaveFileWriter(recordingTempPath, new WaveFormat(12000, 16, 1));
        }
        StatusChanged?.Invoke(this, "Recording audio...");
    }

    public string StopRecording()
    {
        lock (recordingSync)
        {
            if (recordingWriter is null || recordingTempPath is null) throw new InvalidOperationException("No recording is active.");
            recordingWriter.Dispose();
            recordingWriter = null;
            var path = recordingTempPath;
            recordingTempPath = null;
            return path;
        }
    }

    public static Task EncodeRecordingToMp3Async(string wavePath, string mp3Path) => Task.Run(() =>
    {
        using var reader = new WaveFileReader(wavePath);
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(44100, 16, 1)) { ResamplerQuality = 60 };
        MediaFoundationEncoder.EncodeToMp3(resampler, mp3Path, 128_000);
    });

    private void CaptureRecordingAudio(byte[] pcm)
    {
        lock (recordingSync) recordingWriter?.Write(pcm, 0, pcm.Length);
    }

    private void CancelRecording()
    {
        string? path;
        lock (recordingSync)
        {
            recordingWriter?.Dispose();
            recordingWriter = null;
            path = recordingTempPath;
            recordingTempPath = null;
        }
        if (path is not null)
        {
            try { File.Delete(path); } catch { }
        }
    }
    private void SendTuning() { if (audioSocket?.State == WebSocketState.Open) _ = SendTuningAsync(); }
    private Task SendTuningAsync() => SendAsync(audioSocket!, audioSendLock, $"SET mod={currentMode} low_cut={lowCut} high_cut={highCut} freq={frequencyHz / 1_000:0.000}");
    private async Task ConfigureWaterfallAsync(int zoom, double center)
    {
        waterfallZoom = zoom;
        waterfallCenterHz = center;
        await SendAsync(waterfallSocket!, waterfallSendLock, "SET send_dB=1");
        await SendAsync(waterfallSocket!, waterfallSendLock, $"SET zoom={waterfallZoom} cf={waterfallCenterHz / 1_000:0.000}");
        await SendAsync(waterfallSocket!, waterfallSendLock, $"SET maxdb={waterfallMaximumDb} mindb={waterfallMinimumDb}");
        await SendAsync(waterfallSocket!, waterfallSendLock, "SET wf_comp=0");
        await SendAsync(waterfallSocket!, waterfallSendLock, "SET interp=13");
        await SendAsync(waterfallSocket!, waterfallSendLock, "SET wf_speed=3");
    }
    private void SendText(ClientWebSocket? socket, SemaphoreSlim sendLock, string command)
    {
        if (socket?.State != WebSocketState.Open) return;
        _ = SendSafelyAsync(socket, sendLock, command);
    }
    private async Task SendSafelyAsync(ClientWebSocket socket, SemaphoreSlim sendLock, string command)
    {
        try { await SendAsync(socket, sendLock, command); }
        catch (Exception ex) { Log?.Invoke(this, $"TX error: {ex.Message}"); Error?.Invoke(this, ex.Message); }
    }

    private async Task ReceiveLoop(ClientWebSocket socket, bool waterfall, CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream(); WebSocketReceiveResult result;
                do { result = await socket.ReceiveAsync(buffer, token); stream.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
                var packet = stream.ToArray();
                if (waterfall) { waterfallPackets++; waterfallBytes += packet.Length; }
                else { audioPackets++; audioBytes += packet.Length; }
                if ((waterfall ? waterfallPackets : audioPackets) <= 5 || (waterfall ? waterfallPackets : audioPackets) % 50 == 0)
                    Log?.Invoke(this, $"{(waterfall ? "WF" : "AUD")} packet #{(waterfall ? waterfallPackets : audioPackets)}: {packet.Length} bytes [{Convert.ToHexString(packet.AsSpan(0, Math.Min(16, packet.Length)))}]");
                var firstChars = packet.Length >= 3 ? Encoding.ASCII.GetString(packet, 0, 3) : "";
                if (packet.Length >= 4 && firstChars == "MSG")
                {
                    var message = Encoding.UTF8.GetString(packet, 4, packet.Length - 4);
                    Log?.Invoke(this, $"{(waterfall ? "WF" : "AUD")} server: {message.Trim()}");
                    var rejection = ClassifyServerRejection(message);
                    if (rejection is not null) { connectionRejected.TrySetResult(rejection); return; }
                    if (!waterfall && message.Contains("audio_rate=", StringComparison.Ordinal))
                    {
                        await SendAsync(socket, audioSendLock, "SET AR OK in=12000 out=12000");
                        audioReady.TrySetResult(true);
                    }
                    if (waterfall && message.Contains("wf_setup", StringComparison.Ordinal))
                    {
                        await ConfigureWaterfallAsync(waterfallZoom, waterfallCenterHz);
                        waterfallReady.TrySetResult(true);
                    }
                    if (!waterfall && message.StartsWith("audio_adpcm_state=", StringComparison.Ordinal))
                    {
                        var values = message[18..].Split(',');
                        if (values.Length >= 2) { audioAdpcmIndex = Math.Clamp(int.Parse(values[0]), 0, 88); audioAdpcmValue = Math.Clamp(int.Parse(values[1]), short.MinValue, short.MaxValue); }
                    }
                }
                else if (waterfall && firstChars == "W/F" && packet.Length > 16)
                {
                    Interlocked.Exchange(ref lastWaterfallDataTicks, DateTime.UtcNow.Ticks);
                    WaterfallLine?.Invoke(this, packet[16..]);
                    Log?.Invoke(this, $"WF line received: {packet.Length - 16} bytes");
                }
                else if (!waterfall && firstChars == "SND" && packet.Length > 10)
                {
                    Interlocked.Exchange(ref lastAudioDataTicks, DateTime.UtcNow.Ticks);
                    var start = 0;
                    if (packet.Length > start + 10)
                    {
                        var flags = packet[start + 3];
                        var sampleBytes = packet.Length - start - 10;
                        if ((flags & 0x10) != 0)
                        {
                            var pcm = DecodeAdpcm(packet, start + 10, sampleBytes);
                            audioBuffer?.AddSamples(pcm, 0, pcm.Length);
                            CaptureRecordingAudio(pcm);
                            Log?.Invoke(this, $"ADPCM audio decoded: {pcm.Length} PCM bytes, buffer {audioBuffer?.BufferedBytes ?? 0} bytes");
                        }
                        else
                        {
                            var pcm = ToLittleEndianPcm(packet, start + 10, sampleBytes, (flags & 0x80) != 0);
                            audioBuffer?.AddSamples(pcm, 0, pcm.Length);
                            CaptureRecordingAudio(pcm);
                            Log?.Invoke(this, $"Audio PCM: {pcm.Length} bytes, buffer {audioBuffer?.BufferedBytes ?? 0} bytes");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log?.Invoke(this, $"Error {(waterfall ? "WF" : "AUD")}: {ex.Message}");
            if (!connectionEstablished) connectionRejected.TrySetResult("Receiver temporarily unavailable: the connection was interrupted during setup.");
            else _ = HandleConnectionLostAsync("Connection lost: the receiver stopped responding.");
        }
        if (connectionEstablished && !token.IsCancellationRequested) _ = HandleConnectionLostAsync("Connection lost: the receiver closed the connection.");
    }

    private static string? ClassifyServerRejection(string message)
    {
        if (message.Contains("too_busy=", StringComparison.OrdinalIgnoreCase)) return "Receiver busy: all available channels are currently in use.";
        if (message.Contains("down=", StringComparison.OrdinalIgnoreCase)) return "Receiver temporarily offline: the operator has placed this server out of service.";
        if (message.Contains("badp=1", StringComparison.OrdinalIgnoreCase)) return "Receiver busy or protected: no public channel is currently available.";
        if (message.Contains("badp=5", StringComparison.OrdinalIgnoreCase)) return "Connection restricted: this receiver does not allow another connection from your IP address.";
        if (message.Contains("badp=6", StringComparison.OrdinalIgnoreCase)) return "Receiver temporarily unavailable: a database update is in progress. Try again in one minute.";
        return null;
    }

    private void CheckConnectionHealth()
    {
        if (!connectionEstablished) return;
        var now = DateTime.UtcNow;
        var audioAge = now - new DateTime(Interlocked.Read(ref lastAudioDataTicks), DateTimeKind.Utc);
        var waterfallAge = now - new DateTime(Interlocked.Read(ref lastWaterfallDataTicks), DateTimeKind.Utc);
        if (waterfallAge > TimeSpan.FromSeconds(15))
        {
            _ = HandleConnectionLostAsync("Connection lost: no waterfall data was received for 15 seconds.");
            return;
        }
        if (Volatile.Read(ref audioPlaybackRequested) != 0 && audioAge > TimeSpan.FromSeconds(15))
            _ = HandleConnectionLostAsync("Connection lost: Play is active, but no audio data was received for 15 seconds.");
    }

    private async Task HandleConnectionLostAsync(string message)
    {
        if (Interlocked.Exchange(ref handlingConnectionLoss, 1) != 0) return;
        StatusChanged?.Invoke(this, message);
        Error?.Invoke(this, message);
        await DisconnectAsync();
    }

    private static int Find(byte[] data, byte[] marker) { for (var i = 0; i <= data.Length - marker.Length; i++) if (data.AsSpan(i, marker.Length).SequenceEqual(marker)) return i; return -1; }
    private async Task SendAsync(ClientWebSocket socket, SemaphoreSlim sendLock, string text)
    {
        await sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
            Log?.Invoke(this, $"TX {(ReferenceEquals(socket, waterfallSocket) ? "WF" : "AUD")}: {text}");
        }
        finally { sendLock.Release(); }
    }
    private static string NormalizeMode(string mode) => mode.ToLowerInvariant() switch { "nfm" => "nbfm", var value => value };
    private static (int low, int high) GetPassband(string mode, int bandwidth)
    {
        bandwidth = Math.Clamp(bandwidth, 50, 12_000);
        return mode switch
        {
            "usb" or "usn" => (Math.Max(0, 300), Math.Max(350, bandwidth)),
            "lsb" or "lsn" => (-Math.Max(350, bandwidth), -Math.Max(0, 300)),
            "cw" or "cwn" => (-bandwidth / 2, bandwidth / 2),
            _ => (-bandwidth / 2, bandwidth / 2)
        };
    }
    private byte[] DecodeAdpcm(byte[] source, int offset, int length)
    {
        var pcm = new byte[length * 4];
        for (var i = 0; i < length; i++)
        {
            WriteSample(pcm, i * 4, DecodeNibble(source[offset + i] & 15));
            WriteSample(pcm, i * 4 + 2, DecodeNibble(source[offset + i] >> 4));
        }
        return pcm;
    }

    private int DecodeNibble(int nibble)
    {
        var step = AdpcmSteps[audioAdpcmIndex];
        var difference = step >> 3;
        if ((nibble & 1) != 0) difference += step >> 2;
        if ((nibble & 2) != 0) difference += step >> 1;
        if ((nibble & 4) != 0) difference += step;
        audioAdpcmValue += (nibble & 8) != 0 ? -difference : difference;
        audioAdpcmValue = Math.Clamp(audioAdpcmValue, short.MinValue, short.MaxValue);
        audioAdpcmIndex = Math.Clamp(audioAdpcmIndex + AdpcmIndexAdjust[nibble], 0, 88);
        return audioAdpcmValue;
    }

    private static byte[] ToLittleEndianPcm(byte[] source, int offset, int length, bool littleEndian)
    {
        var pcm = source[offset..(offset + length)].ToArray();
        if (!littleEndian) for (var i = 0; i + 1 < pcm.Length; i += 2) (pcm[i], pcm[i + 1]) = (pcm[i + 1], pcm[i]);
        return pcm;
    }

    private static void WriteSample(byte[] target, int offset, int sample) { target[offset] = (byte)sample; target[offset + 1] = (byte)(sample >> 8); }
    private async Task LoadServerInfoAsync(Uri baseUri)
    {
        try
        {
            var statusUri = new UriBuilder(baseUri) { Path = baseUri.AbsolutePath.TrimEnd('/') + "/status", Query = "" }.Uri;
            var content = await Http.GetStringAsync(statusUri);
            var values = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
            string Get(string key, string fallback = "Not reported") => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
            var port = baseUri.IsDefaultPort ? (baseUri.Scheme is "https" or "wss" ? 443 : 80) : baseUri.Port;
            var info = $"Server: {baseUri.Host}:{port}{Environment.NewLine}" +
                       $"URL: {baseUri}{Environment.NewLine}" +
                       $"Station / operator message: {Get("name")}{Environment.NewLine}" +
                       $"Location: {Get("loc")}{Environment.NewLine}" +
                       $"Grid: {Get("grid")}{Environment.NewLine}" +
                       $"Altitude: {Get("asl")} m{Environment.NewLine}" +
                       $"Antenna: {Get("antenna")}{Environment.NewLine}" +
                       $"Antenna connected: {Get("ant_connected")}{Environment.NewLine}" +
                       $"Hardware: {Get("sdr_hw")}{Environment.NewLine}" +
                       $"Software: {Get("sw_version")}{Environment.NewLine}" +
                       $"Coverage: {Get("bands")} Hz{Environment.NewLine}" +
                       $"Users: {Get("users")}/{Get("users_max")}{Environment.NewLine}" +
                       $"SNR: {Get("snr")} dB{Environment.NewLine}" +
                       $"GPS fixes: {Get("gps_good")}";
            ServerInfoChanged?.Invoke(this, info);
        }
        catch (Exception ex) { ServerInfoChanged?.Invoke(this, $"Server: {baseUri}{Environment.NewLine}Public server details unavailable: {ex.Message}"); }
    }

    public async Task DisconnectAsync() { connectionEstablished = false; Interlocked.Exchange(ref audioPlaybackRequested, 0); CancelRecording(); cancellation?.Cancel(); statsTimer?.Dispose(); statsTimer = null; keepAliveTimer?.Dispose(); keepAliveTimer = null; watchdogTimer?.Dispose(); watchdogTimer = null; audioOutput?.Stop(); audioOutput?.Dispose(); audioOutput = null; audioBuffer = null; if (audioSocket is not null) await Close(audioSocket); if (waterfallSocket is not null) await Close(waterfallSocket); audioSocket = null; waterfallSocket = null; StatusChanged?.Invoke(this, "Disconnected"); }
    private static async Task Close(ClientWebSocket socket) { if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None); socket.Dispose(); }
    public async ValueTask DisposeAsync() { await DisconnectAsync(); cancellation?.Dispose(); audioSendLock.Dispose(); waterfallSendLock.Dispose(); }
}
