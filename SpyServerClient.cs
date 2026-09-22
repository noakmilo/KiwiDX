using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace KiwiDX;

internal sealed class SpyServerClient : IAsyncDisposable
{
    private readonly TcpClient tcp = new() { NoDelay = true };
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly object state = new();
    private readonly TaskCompletionSource<bool> metadata = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> streaming = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<(uint Type, byte[] Data, int Length)> iqQueue = Channel.CreateBounded<(uint, byte[], int)>(new BoundedChannelOptions(4) { SingleReader = true, SingleWriter = true });
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(1);
    private readonly Dictionary<uint, uint> settings = new();
    private NetworkStream? network;
    private Task? receiver, processor, sender, monitor;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private string connectionStage = "TCP connection";
    private int disposed;
    private SpyServerDevice? device;
    private SpyServerDemodulator? demodulator;
    private string mode = "am", url = "", version = "";
    private int bandwidth = 6000, fftDecimation;
    private double frequency, iqCenter, fftCenter, viewCenter, viewSpan, sampleRate, fftSpan;
    private double minIq, maxIq;
    private long tuneRequestedAt;
    private uint requestedIqCenter;
    private bool canControl, configured, gotIq, gotFft;
    private volatile bool connected;
    private int failed;
    private IOException? failure;
    private long packets, samples, frames, dropped;
    public bool IsConnected => connected && !stop.IsCancellationRequested;
    public double MaximumFrequency => device?.MaximumFrequency ?? 30_000_000;
    public event Action<byte[]>? Audio;
    public event Action<byte[]>? Spectrum;
    public event Action<string>? Status;
    public event Action<string>? Log;
    private void Trace(string message) => Log?.Invoke($"[SpyServer {url} +{elapsed.Elapsed.TotalSeconds:F1}s] {message}");
    private void ReportStatus(string message) { Trace(message); Status?.Invoke(message); }
    private string StreamSummary => $"IQ received={gotIq}, FFT received={gotFft}, queued IQ drops={Interlocked.Read(ref dropped)}";
    public event Action<string>? Error;
    public event Action<string, string>? Details;
    public event Action<double, double, double>? Configuration;

    public async Task ConnectAsync(string address, double initialFrequency, string initialMode, int initialBandwidth, CancellationToken cancellationToken = default)
    {
        if (!SpyServerAddress.TryParse(address, out var endpoint)) throw new ArgumentException("Enter a valid sdr://host:port SpyServer address.");
        url = endpoint.Url; elapsed.Restart();
        var listing = AirspyDirectoryService.Find(url);
        if (listing is not null) Trace($"Directory reports online={listing.Online}, registered={listing.Registered}, status={listing.StatusLabel}, clients={listing.Clients}/{listing.MaxClients}. Directory status does not guarantee TCP reachability.");
        monitor = Task.Run(MonitorAsync);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            ReportStatus($"Connecting to SpyServer at {endpoint.Host}:{endpoint.Port}...");
            await tcp.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token).ConfigureAwait(false);
            Trace($"TCP connected to {tcp.Client.RemoteEndPoint}.");
            connectionStage = "HELLO / device information and client sync";
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            network = tcp.GetStream();
            var name = Encoding.ASCII.GetBytes("KiwiDX"); var hello = new byte[12 + name.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(hello.AsSpan(4), (uint)(4 + name.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(hello.AsSpan(8), SpyServerProtocol.Version); name.CopyTo(hello, 12);
            await network.WriteAsync(hello, timeout.Token).ConfigureAwait(false);
            Trace("HELLO sent: protocol 2.0.1700, client KiwiDX.");
            ReportStatus("Negotiating SpyServer capabilities...");
            receiver = Task.Run(ReceiveAsync); processor = Task.Run(ProcessAsync); sender = Task.Run(SendChangesAsync);
            await metadata.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            connectionStage = "stream configuration / first IQ and FFT";
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            lock (state)
            {
                var d = device!;
                int decimation = d.MinimumDecimation;
                while (decimation + 1 < d.Stages && d.SampleRate / Math.Pow(2, decimation + 1) >= 24000) decimation++;
                sampleRate = d.SampleRate / Math.Pow(2, decimation);
                if (sampleRate is < 12000 or > 500000) throw new InvalidDataException("This server does not offer a supported reduced-IQ rate (12-500 kHz).");
                uint format = d.ForcedFormat == 0 ? 2u : d.ForcedFormat;
                if (format is < 1 or > 4) throw new InvalidDataException("The server requires an unsupported IQ format.");
                mode = initialMode.ToLowerInvariant(); bandwidth = initialBandwidth;
                double minimum = canControl ? d.MinimumFrequency : minIq, maximum = canControl ? d.MaximumFrequency : maxIq;
                frequency = double.IsFinite(initialFrequency) && initialFrequency >= minimum && initialFrequency <= maximum ? initialFrequency : Math.Clamp(iqCenter, minimum, maximum);
                iqCenter = frequency;
                Set(0, 5); Set(100, format); Set(102, (uint)decimation); Set(101, (uint)iqCenter);
                Set(200, 1); Set(203, 0); Set(204, 150); Set(205, 1200);
                configured = true;
                ConfigureView(frequency, Math.Min(200000, d.Bandwidth));
                ResetDsp();
                Set(1, 1); connected = true;
                ReportStatus($"SpyServer connected: {d.Name}, reduced IQ {sampleRate:0.##} samples/s");
                Trace($"Capabilities: {d.Name}, serial {d.Serial}, rate {sampleRate}, IQ format {format}, FFT+IQ, coverage {minimum}-{maximum}");
            }
            PublishDetails();
            PublishConfiguration();
            await streaming.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            connectionStage = "streaming";
            ReportStatus("SpyServer streaming (native FFT + reduced IQ)");
        }
        catch (OperationCanceledException) when (failure is not null)
        { await DisposeAsync(); throw failure; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !stop.IsCancellationRequested)
        { var message = $"SpyServer timeout during {connectionStage} at {endpoint.Host}:{endpoint.Port}. {StreamSummary}"; Trace(message); await DisposeAsync(); throw new TimeoutException(message); }
        catch (SocketException e) { Trace($"{connectionStage} failed: {e.SocketErrorCode}: {e.Message}"); await DisposeAsync(); throw new IOException($"Unable to connect to SpyServer at {endpoint.Host}:{endpoint.Port}: {e.Message}", e); }
        catch (Exception e) { Trace($"{connectionStage} failed: {e.GetType().Name}: {e.Message}"); await DisposeAsync(); throw; }
    }

    private static string SettingName(uint key) => key switch {
        0 => "stream mode", 1 => "stream enabled", 100 => "IQ format", 101 => "IQ frequency",
        102 => "IQ decimation", 200 => "FFT format", 201 => "FFT frequency", 202 => "FFT decimation",
        203 => "FFT offset", 204 => "FFT range", 205 => "FFT pixels", _ => key.ToString()
    };
    private async Task MonitorAsync()
    {
        try {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stop.Token))
                if (connectionStage != "streaming") Trace($"Waiting for {connectionStage}. {StreamSummary}");
        } catch (OperationCanceledException) { }
    }

    private void PublishDetails()
    {
        var d = device!; var entry = AirspyDirectoryService.Find(url);
        Details?.Invoke(string.IsNullOrWhiteSpace(entry?.Name) ? d.Name : entry.Name,
            $"Backend: Airspy SpyServer\nReceiver: {d.Name}\nServer version: {version}\nCoverage: {d.MinimumFrequency / 1e6:0.######}-{d.MaximumFrequency / 1e6:0.######} MHz\nMaximum sample rate: {d.SampleRate} Hz\nMaximum bandwidth: {d.Bandwidth} Hz\nCurrent IQ sample rate: {sampleRate:0.##} Hz\nStreaming mode: Reduced IQ + FFT" +
            (entry is null ? "" : $"\nLocation: {entry.Location}\nAntenna: {entry.Antenna}\nDescription: {entry.Description}\nPlatform: {entry.Platform}"));
    }

    // UI operations only update coalesced settings. The TCP writer and DSP never run on the UI thread.
    private void Set(uint key, uint value)
    {
        if (key == 101) { requestedIqCenter = value; tuneRequestedAt = Environment.TickCount64; }
        settings[key] = value; changes.Writer.TryWrite(true);
    }
    private async Task SendChangesAsync()
    {
        try
        {
            await foreach (var _ in changes.Reader.ReadAllAsync(stop.Token))
            {
                KeyValuePair<uint, uint>[] batch;
                lock (state) { batch = settings.ToArray(); settings.Clear(); }
                foreach (var setting in batch) await SendSettingAsync(setting.Key, setting.Value, stop.Token).ConfigureAwait(false);
                Trace("Settings sent: " + string.Join(", ", batch.Select(s => $"{SettingName(s.Key)}={s.Value}")));
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { if (!stop.IsCancellationRequested) Fail(e); }
    }
    private async Task SendSettingAsync(uint key, uint value, CancellationToken token)
    {
        await sendGate.WaitAsync(token).ConfigureAwait(false);
        try { await network!.WriteAsync(SpyServerProtocol.Command(2, key, value), token).ConfigureAwait(false); }
        finally { sendGate.Release(); }
    }
    public void Tune(double? hz = null, string? modulation = null, int? filter = null)
    {
        lock (state)
        {
            if (!configured || device is null) return;
            if (hz is double invalid && !double.IsFinite(invalid)) { ReportStatus("Enter a finite frequency."); PublishConfiguration(); return; }
            if (modulation is not null && modulation.ToLowerInvariant() is not ("am" or "usb" or "lsb" or "cw" or "nfm" or "fm")) { ReportStatus("Unsupported SpyServer mode."); return; }
            if (modulation is not null) mode = modulation.ToLowerInvariant();
            if (filter is int width) bandwidth = width;
            if (hz is double f)
            {
                double minimum = canControl ? device.MinimumFrequency : minIq, maximum = canControl ? device.MaximumFrequency : maxIq;
                frequency = Math.Clamp(f, minimum, maximum);
                if (frequency != f) ReportStatus($"SpyServer tuning is limited to {minimum / 1e6:0.######}-{maximum / 1e6:0.######} MHz.");
                if (Math.Abs(frequency - iqCenter) + bandwidth > sampleRate * .38)
                { iqCenter = frequency; Set(101, (uint)iqCenter); }
                if (Math.Abs(frequency - viewCenter) > viewSpan * .45) ConfigureView(frequency, viewSpan);
            }
            ResetDsp();
        }
        PublishConfiguration();
    }
    private void ResetDsp() => demodulator = new SpyServerDemodulator(sampleRate, mode, bandwidth, frequency - iqCenter);
    public void SetView(double center, double span)
    {
        if (!double.IsFinite(center) || !double.IsFinite(span) || span <= 0) return;
        lock (state) { if (configured) ConfigureView(center, span); }
    }
    private void ConfigureView(double center, double span)
    {
        var d = device!;
        int decimation = Math.Clamp((int)Math.Floor(Math.Log2(d.SampleRate * .8 / Math.Max(1, span))), 0, d.Stages - 1);
        double available = d.SampleRate * .8 / Math.Pow(2, decimation);
        double min = d.MinimumFrequency + available / 2, max = d.MaximumFrequency - available / 2;
        if (!canControl) { min = Math.Max(min, minIq); max = Math.Min(max, maxIq); }
        if (max < min) { min = max = iqCenter; }
        double target = Math.Clamp(center, min, max);
        if (fftDecimation != decimation || fftSpan == 0) Set(202, (uint)decimation);
        if (fftCenter != target || fftSpan == 0) Set(201, (uint)target);
        fftDecimation = decimation; fftSpan = available; fftCenter = target;
        viewCenter = center; viewSpan = span;
    }
    private void PublishConfiguration()
    {
        double c, s, f; lock (state) { c = viewCenter; s = viewSpan; f = frequency; }
        Configuration?.Invoke(c, s, f);
    }
    private async Task ReceiveAsync()
    {
        var header = new byte[20]; var timer = Stopwatch.StartNew(); bool firstHeader = true;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
                await network!.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(false);
                if (firstHeader) { Trace($"First server header: protocol=0x{SpyServerProtocol.U32(header):X8}, type={SpyServerProtocol.U32(header,4)}, body={SpyServerProtocol.U32(header,16)} bytes."); firstHeader = false; }
                var h = SpyServerProtocol.Header(header); var data = ArrayPool<byte>.Shared.Rent(Math.Max(1, h.Size)); bool queued = false;
                try
                {
                    await network.ReadExactlyAsync(data.AsMemory(0, h.Size), deadline.Token).ConfigureAwait(false); packets++;
                    uint type = h.Type & 65535;
                    if ((type is >= 100 and <= 103 && h.Stream != 1) || (type is 300 or 301 && h.Stream != 4))
                        throw new InvalidDataException("SpyServer packet has an invalid stream type.");
                    if (type == 0)
                    {
                        device = SpyServerDevice.Parse(data.AsSpan(0, h.Size));
                        uint v = SpyServerProtocol.U32(header);
                        version = $"{v >> 24}.{(v >> 16) & 255}.{v & 65535}";
                        Trace($"DEVICE_INFO: protocol {version}, {device.Name}, max rate {device.SampleRate}, bandwidth {device.Bandwidth}, stages {device.Stages}, minimum IQ decimation {device.MinimumDecimation}, forced IQ format {device.ForcedFormat}, coverage {device.MinimumFrequency}-{device.MaximumFrequency} Hz.");

                    }
                    else if (type == 1)
                    {
                        if (h.Size < 36) throw new InvalidDataException("Incomplete SpyServer synchronization packet.");
                        lock (state)
                        {
                            canControl = SpyServerProtocol.U32(data) != 0; minIq = SpyServerProtocol.U32(data, 20); maxIq = SpyServerProtocol.U32(data, 24);
                            if (minIq > maxIq) throw new InvalidDataException("Invalid SpyServer tuning limits.");
                            double actualIq = SpyServerProtocol.U32(data, 12), actualFft = SpyServerProtocol.U32(data, 16);
                            if (!configured) { iqCenter = actualIq; fftCenter = actualFft; }
                            else
                            {
                                // Sync also arrives for earlier commands. Do not let an older acknowledgment undo a pending tune.
                                if (actualIq == requestedIqCenter || Environment.TickCount64 - tuneRequestedAt > 1000)
                                {
                                    if (iqCenter != actualIq) { iqCenter = actualIq; ResetDsp(); }
                                    if (frequency < minIq || frequency > maxIq)
                                    {
                                        frequency = Math.Clamp(frequency, minIq, maxIq); iqCenter = frequency;
                                        Set(101, (uint)frequency); ResetDsp(); PublishConfiguration();
                                    }
                                }
                                if (Math.Abs(actualFft - fftCenter) > 1 && Environment.TickCount64 - tuneRequestedAt > 1000)
                                { fftCenter = actualFft; }
                            }
                        }
                        if (!metadata.Task.IsCompleted) Trace($"CLIENT_SYNC: control={canControl}, IQ center={iqCenter}, FFT center={fftCenter}, IQ tuning range={minIq}-{maxIq} Hz.");
                        if (device is not null) metadata.TrySetResult(true);
                    }
                    else if (type is >= 100 and <= 103)
                    {
                        if (!configured) continue;
                        queued = iqQueue.Writer.TryWrite((h.Type, data, h.Size)); if (!queued) dropped++;
                    }
                    else if (type == 301)
                    {
                        if (h.Size is < 100 or > 32768) throw new InvalidDataException("Invalid SpyServer FFT length.");
                        RenderSpectrum(data, h.Size); frames++; if (!gotFft) Trace($"First FFT received: {h.Size} bins, stream {h.Stream}."); gotFft = true; if (gotIq) streaming.TrySetResult(true);
                    }
                    else if (type == 300) throw new InvalidDataException("SpyServer returned compressed FFT despite UINT8 negotiation.");
                    if (timer.Elapsed.TotalSeconds >= 10)
                    {
                        Trace($"Rates: packets/s {packets / timer.Elapsed.TotalSeconds:F1}, IQ samples/s {samples / timer.Elapsed.TotalSeconds:F0}, FFT/s {frames / timer.Elapsed.TotalSeconds:F1}, dropped IQ packets {dropped}");
                        timer.Restart(); packets = frames = samples = 0;
                    }
                }
                finally { if (!queued) ArrayPool<byte>.Shared.Return(data); }
            }
        }
        catch (Exception e) { if (!stop.IsCancellationRequested) Fail(e); }
        finally { iqQueue.Writer.TryComplete(); }
    }
    private void RenderSpectrum(byte[] data, int length)
    {
        double center, span, displayCenter, displaySpan;
        lock (state) { center = fftCenter; span = fftSpan; displayCenter = viewCenter; displaySpan = viewSpan; }
        if (span <= 0) return;
        var line = new byte[1200];
        for (int i = 0; i < line.Length; i++)
        {
            double f = displayCenter - displaySpan / 2 + displaySpan * i / line.Length;
            int bin = (int)Math.Floor((f - center + span / 2) / span * length);
            // UINT8 FFT: 0 = bottom of negotiated dB range, 255 = top (offset 0 dB).
            if (bin >= 0 && bin < length) line[i] = (byte)Math.Clamp(255 - 150 + data[bin] * 150.0 / 255, 0, 255);
        }
        Spectrum?.Invoke(line);
    }
    private async Task ProcessAsync()
    {
        var iq = ArrayPool<float>.Shared.Rent(SpyServerProtocol.MaxBody);
        try
        {
            await foreach (var packet in iqQueue.Reader.ReadAllAsync(stop.Token))
            {
                try
                {
                    int count = SpyServerProtocol.DecodeIq(packet.Type, packet.Data.AsSpan(0, packet.Length), iq);
                    SpyServerDemodulator? dsp; lock (state) dsp = demodulator;
                    if (dsp is null) continue;
                    var pcm = dsp.Process(iq.AsSpan(0, count)); samples += count / 2;
                    Audio?.Invoke(pcm); if (!gotIq) Trace($"First IQ decoded: format {packet.Type & 65535}, {count / 2} complex samples, {pcm.Length} PCM bytes."); gotIq = true; if (gotFft) streaming.TrySetResult(true);
                }
                finally { ArrayPool<byte>.Shared.Return(packet.Data); }
            }
        }
        catch (Exception e) { if (!stop.IsCancellationRequested) Fail(e); }
        finally { ArrayPool<float>.Shared.Return(iq); while (iqQueue.Reader.TryRead(out var p)) ArrayPool<byte>.Shared.Return(p.Data); }
    }
    private void Fail(Exception e)
    {
        if (Interlocked.Exchange(ref failed, 1) != 0) return;
        connected = false;
        var error = new IOException(e is OperationCanceledException ? $"SpyServer receive timeout during {connectionStage} (15 seconds without a complete packet). {StreamSummary}" : "SpyServer: " + e.Message, e);
        Trace($"Failure during {connectionStage}: {e.GetType().Name}: {error.Message}");
        failure = error;
        metadata.TrySetException(error); streaming.TrySetException(error);
        if (streaming.Task.IsCompletedSuccessfully) Error?.Invoke(error.Message);
        ReportStatus("Connection lost: " + error.Message); stop.Cancel(); tcp.Dispose();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Trace("Disconnecting; stopping stream and closing TCP.");
        if (!stop.IsCancellationRequested && network is not null)
        {
            try { using var timeout = new CancellationTokenSource(500); await SendSettingAsync(1, 0, timeout.Token).ConfigureAwait(false); }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        }
        connected = false; stop.Cancel(); tcp.Dispose(); changes.Writer.TryComplete(); iqQueue.Writer.TryComplete();
        foreach (var task in new[] { receiver, processor, sender, monitor }) if (task is not null) await task.ConfigureAwait(false);
        while (iqQueue.Reader.TryRead(out var packet)) ArrayPool<byte>.Shared.Return(packet.Data);
        Trace("Disconnected; receiver workers stopped.");
    }
}
