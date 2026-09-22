using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using KiwiDX;

internal sealed class MockSpyServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task run;
    public string Url { get; }
    public List<(uint Key, uint Value)> Settings { get; } = new();
    public bool Malformed { get; init; }
    public bool Truncate { get; init; }
    public bool SilentHandshake { get; init; }
    public MockSpyServer()
    {
        listener.Start(); Url = $"sdr://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        run = Task.Run(RunAsync);
    }
    private async Task RunAsync()
    {
        try
        {
            using var socket = await listener.AcceptTcpClientAsync(stop.Token); using var stream = socket.GetStream();
            var command = new byte[8]; await stream.ReadExactlyAsync(command, stop.Token);
            int size = (int)SpyServerProtocol.U32(command, 4); var body = new byte[size]; await stream.ReadExactlyAsync(body, stop.Token);
            if (SpyServerProtocol.U32(command) != 0 || SpyServerProtocol.U32(body) != SpyServerProtocol.Version) throw new Exception("Bad client HELLO");
            if (SilentHandshake) { await Task.Delay(Timeout.Infinite,stop.Token); return; }
            async Task Message(uint type, uint channel, byte[] payload)
            {
                var h = new byte[20]; BinaryPrimitives.WriteUInt32LittleEndian(h, SpyServerProtocol.Version);
                BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), type); BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(8), channel);
                BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), (uint)payload.Length);
                // Deliberately split the fixed header across TCP writes.
                await stream.WriteAsync(h.AsMemory(0,7), stop.Token); await stream.WriteAsync(h.AsMemory(7), stop.Token); await stream.WriteAsync(payload, stop.Token);
            }
            byte[] Words(params uint[] words) { var b = new byte[words.Length * 4]; for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4), words[i]); return b; }
            if (Truncate) { await stream.WriteAsync(new byte[7], stop.Token); return; }
            if (Malformed)
            {
                var h = new byte[20]; BinaryPrimitives.WriteUInt32LittleEndian(h, SpyServerProtocol.Version); BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), uint.MaxValue);
                await stream.WriteAsync(h, stop.Token); return;
            }
            await Message(0,0,Words(2,123,48000,38400,2,0,0,100000,30000000,16,0,0));
            await Message(1,0,Words(0,0,7100000,7100000,7100000,7000000,7200000,7000000,7200000,1));
            while (true)
            {
                await stream.ReadExactlyAsync(command, stop.Token); size = (int)SpyServerProtocol.U32(command,4);
                if (size != 8) throw new Exception("Bad setting length"); await stream.ReadExactlyAsync(body = new byte[size], stop.Token);
                uint key = SpyServerProtocol.U32(body), value = SpyServerProtocol.U32(body,4);
                lock(Settings) Settings.Add((key,value));
                if (key == 1 && value == 1) break;
            }
            var drain = Task.Run(async () => {
                try { while (!stop.IsCancellationRequested) { await stream.ReadExactlyAsync(command,stop.Token); var b=new byte[(int)SpyServerProtocol.U32(command,4)]; await stream.ReadExactlyAsync(b,stop.Token); lock(Settings)Settings.Add((SpyServerProtocol.U32(b),SpyServerProtocol.U32(b,4))); } }
                catch (Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException) { }
            });
            var iq = new byte[4800 * 4]; for(int i=0;i<4800;i++) BinaryPrimitives.WriteInt16LittleEndian(iq.AsSpan(i*4),(short)(15000+5000*Math.Sin(2*Math.PI*1000*i/48000)));
            var fft = Enumerable.Range(0,1200).Select(i=>(byte)(i%256)).ToArray();
            while(!stop.IsCancellationRequested){await Message(101,1,iq);await Message(301,4,fft);await Task.Delay(100,stop.Token);}
            await drain;
        }
        catch (Exception e) when(e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
    }
    public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await run; stop.Dispose(); }
}
