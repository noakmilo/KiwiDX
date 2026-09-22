using KiwiDX;
using System.Buffers.Binary;
using System.Diagnostics;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
foreach (var url in new[] { "sdr://207.49.195.32:5555", "sdr://example.com:5555", "sdr://example.com", "sdr://[::1]:5555" })
    Check(SpyServerAddress.TryParse(url, out var a) && a.Port == 5555 && SpyServerAddress.TryParse(a.Url, out _), "URL " + url);
foreach (var url in new[] { "http://example.com:5555", "sdr://", "sdr://:5555", "sdr://a:0", "sdr://a:65536", "sdr://a:-1", "sdr://u:p@host", "sdr://host/path", "sdr://host?x=y" })
    Check(!SpyServerAddress.TryParse(url, out _), "Reject " + url);
var header = new byte[20]; BinaryPrimitives.WriteUInt32LittleEndian(header, SpyServerProtocol.Version);
BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 1200);
Check(SpyServerProtocol.Header(header).Size == 1200, "Header length");
BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), uint.MaxValue);
try { SpyServerProtocol.Header(header); throw new Exception("Accepted oversized packet"); } catch (InvalidDataException) { checks++; }
var iq = new float[4]; SpyServerProtocol.DecodeIq(100, new byte[] { 0, 128, 255, 64 }, iq);
Check(iq[0] == -1 && iq[1] == 0 && iq[2] > .99 && iq[3] == -.5, "Unsigned IQ centering");
SpyServerProtocol.DecodeIq(101, new byte[] { 0, 128, 255, 127 }, iq);
Check(iq[0] == -1 && iq[1] > .999, "Signed IQ16");
SpyServerProtocol.DecodeIq(102, new byte[] { 0, 0, 128, 255, 255, 127 }, iq);
Check(iq[0] == -1 && iq[1] > .999, "Signed IQ24");
try { SpyServerProtocol.DecodeIq(101, new byte[3], iq); throw new Exception("Accepted partial IQ"); } catch (InvalidDataException) { checks++; }
var floats = new byte[8]; BinaryPrimitives.WriteSingleLittleEndian(floats,.5f); BinaryPrimitives.WriteSingleLittleEndian(floats.AsSpan(4),float.NaN);
SpyServerProtocol.DecodeIq(103,floats,iq); Check(iq[0]==.5f && iq[1]==0,"Float IQ and non-finite sanitization");
var incompatible = new byte[20]; BinaryPrimitives.WriteUInt32LittleEndian(incompatible,0x03000000);
try { SpyServerProtocol.Header(incompatible); throw new Exception("Accepted unsupported version"); } catch(InvalidDataException) { checks++; }
var entries = AirspyDirectoryService.Parse("""{"servers":[{"streamingHost":"example.com","streamingPort":5555,"online":true,"registered":true,"ownerName":"Test","antennaLocation":{"lat":40,"long":-70},"maxClients":2}]}""");
Check(entries.Length == 1 && entries[0].Available && entries[0].Url == "sdr://example.com:5555", "Directory metadata");
var readyEntry=entries[0];
Check(!(readyEntry with {Registered=false}).Available && (readyEntry with {Registered=false}).StatusColor=="#ef5350","Online but unregistered is red/unreachable");
Check((readyEntry with {Online=false}).StatusLabel=="Offline" && (readyEntry with {Online=false}).StatusColor=="#ef5350","Offline is red");
Check((readyEntry with {Clients=2}).StatusLabel=="Busy" && !(readyEntry with {Clients=2}).Available,"Busy is not ready");
Check((readyEntry with {Registered=null}).StatusLabel=="Unverified" && !(readyEntry with {Registered=null}).Available,"Missing reachability flag is not green");


double Energy(byte[] pcm, double hz)
{
    double re = 0, im = 0; int start = 3000;
    for (int i = start; i < pcm.Length / 2; i++) { double v = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2)); re += v * Math.Cos(2 * Math.PI * hz * i / 12000); im += v * Math.Sin(2 * Math.PI * hz * i / 12000); }
    return re * re + im * im;
}
foreach (string mode in new[] { "am", "usb", "lsb", "nfm", "cw" })
{
    const int rate = 48000;
    var data = new float[rate * 2]; double phase = 0;
    for (int i = 0; i < rate; i++)
    {
        double t = (double)i / rate, re, im;
        if (mode == "am") { re = .4 + .2 * Math.Sin(2 * Math.PI * 1000 * t); im = 0; }
        else if (mode == "nfm") { phase += 2 * Math.PI * 2000 * Math.Sin(2 * Math.PI * 1000 * t) / rate; re = .5 * Math.Cos(phase); im = .5 * Math.Sin(phase); }
        else if (mode == "cw") { re = .5; im = 0; }
        else { re = .25 * (Math.Cos(2 * Math.PI * 1000 * t) + Math.Cos(2 * Math.PI * 2000 * t)); im = .25 * (Math.Sin(2 * Math.PI * 1000 * t) - Math.Sin(2 * Math.PI * 2000 * t)); }
        data[2*i] = (float)re; data[2*i+1] = (float)im;
    }
    var dsp = new SpyServerDemodulator(rate, mode, mode == "nfm" ? 12000 : mode == "cw" ? 500 : 3000, 0);
    var pcm = dsp.Process(data);
    Check(Math.Abs(pcm.Length - 24000) <= 2, mode + " PCM sample rate");
    double wanted = mode == "cw" ? 700 : mode == "lsb" ? 2000 : 1000;
    Check(Energy(pcm, wanted) > Energy(pcm, 3500) * 100, mode + " detected tone");
    if (mode is "usb" or "lsb") Check(Energy(pcm, wanted) > Energy(pcm, mode == "usb" ? 2000 : 1000) * 1000, mode + " opposite sideband rejection >30 dB");
}
await using (var mock = new MockSpyServer())
{
    await using var client = new SpyServerClient();
    int lines=0, bytes=0; double tuned=0;
    var logs=new System.Collections.Concurrent.ConcurrentQueue<string>(); client.Log += logs.Enqueue;
    client.Spectrum += _ => Interlocked.Increment(ref lines);
    client.Audio += b => Interlocked.Add(ref bytes,b.Length);
    client.Configuration += (_,_,f) => tuned=f;
    await client.ConnectAsync(mock.Url,7100000,"am",6000);
    await Task.Delay(350);
    Check(lines>1 && bytes>4000,"Fragmented TCP handshake, metadata, FFT and PCM");
    lock(mock.Settings) Check(mock.Settings.Contains((0u,5u)) && mock.Settings.Contains((100u,2u)),"Reduced IQ + FFT negotiation");
    client.Tune(hz:7100100); await Task.Delay(100);
    lock(mock.Settings) Check(mock.Settings.Count(s=>s.Key==101)==1,"Small VFO change uses local translation");
    client.Tune(hz:9000000); Check(tuned==7200000,"Locked receiver coverage clamping");
    await client.DisposeAsync(); Check(!client.IsConnected,"Mock disconnect");
    foreach(var expected in new[]{"TCP connected","HELLO sent","DEVICE_INFO","CLIENT_SYNC","Settings sent","First FFT","First IQ","Disconnected; receiver workers stopped"})
        Check(logs.Any(l=>l.Contains(expected)),"Diagnostic event: " + expected);
    Check(logs.Count<40,"Diagnostics do not log every packet");
}
var unavailable = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); unavailable.Start();
int closedPort=((System.Net.IPEndPoint)unavailable.LocalEndpoint).Port; unavailable.Stop();
await using(var refused = new SpyServerClient()) {
 try { await refused.ConnectAsync($"sdr://127.0.0.1:{closedPort}",7100000,"am",6000); throw new Exception("Connected to closed port"); }
 catch(IOException e) { Check(e.Message.Contains("127.0.0.1"),"Connection refused identifies receiver"); }
}
foreach(bool truncate in new[]{false,true})
{
    await using var mock = new MockSpyServer { Malformed=!truncate, Truncate=truncate };
    await using var client = new SpyServerClient();
    try { await client.ConnectAsync(mock.Url,7100000,"am",6000); throw new Exception("Accepted invalid network packet"); }
    catch(IOException) { Check(!client.IsConnected,truncate?"EOF during header":"Oversized network packet rejected"); }
}
await using(var silent=new MockSpyServer { SilentHandshake=true })
{
    await using var client=new SpyServerClient(); var logs=new System.Collections.Concurrent.ConcurrentQueue<string>();client.Log+=logs.Enqueue;
    try { await client.ConnectAsync(silent.Url,7100000,"am",6000);throw new Exception("Accepted silent handshake"); }
    catch(IOException e) { Check(e.Message.Contains("HELLO") && e.Message.Contains("IQ received=False"),"Handshake timeout names stage and missing streams"); }
    Check(logs.Any(l=>l.Contains("Waiting for HELLO")) && logs.Any(l=>l.Contains("Failure during HELLO")),"Timeout progress and cause reach Console Log event");
}
Console.WriteLine($"{checks} offline checks passed.");
if (args.Length > 0)
{
    var liveEntries = await new AirspyDirectoryService().LoadAsync(CancellationToken.None); Check(liveEntries.Length > 0, $"Live directory: {liveEntries.Length} receivers");
    await using var client = new SpyServerClient(); long audio = 0, fft = 0; long peak = 0;
    client.Log += Console.WriteLine; client.Status += Console.WriteLine; client.Error += Console.WriteLine;
    client.Details += (name, details) => Console.WriteLine(name + "\n" + details);
    client.Audio += pcm => { Interlocked.Add(ref audio, pcm.Length); foreach (var b in pcm) if (b != 0) { Interlocked.Increment(ref peak); break; } };
    client.Spectrum += line => { Interlocked.Increment(ref fft); };
    client.Configuration += (c,s,f) => Console.WriteLine($"View {c} / {s}, tuned {f}");
    await client.ConnectAsync(args[0], 7100000, "am", 6000);
    foreach (var m in new[] { "am", "usb", "lsb", "nfm", "cw" }) { client.Tune(modulation:m); await Task.Delay(3000); }
    client.Tune(hz:7200000); await Task.Delay(3000);
    int extra = args.Length > 1 ? Math.Max(0,int.Parse(args[1])-18) : 0;
    while(extra > 0) { int seconds=Math.Min(15,extra); await Task.Delay(seconds*1000); extra-=seconds; Console.WriteLine($"Streaming: PCM {audio} bytes, FFT {fft} frames"); }
    Check(audio > 12000 * 2 * 12 && fft > 20 && peak > 10, $"Live stream: PCM {audio} bytes, FFT {fft} frames");
    await client.DisposeAsync(); Check(!client.IsConnected, "Live disconnect");
    await using var again = new SpyServerClient();
    await again.ConnectAsync(args[0],7100000,"usb",2400); await Task.Delay(2000);
    Check(again.IsConnected,"Live reconnect after clean disconnect");
}
