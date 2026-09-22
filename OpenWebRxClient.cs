using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
namespace KiwiDX;

internal sealed record OpenWebRxProfile(string Id, string Name)
{
    public override string ToString() => Name;
}

// Independent OpenWebRX receiver transport. No browser or hosted scripts are executed.
internal sealed class OpenWebRxClient : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim sendLock = new(1,1);
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OpenWebRxAdpcm audioCodec = new();
    private Task? receiving;
    private string audioCompression="none", fftCompression="none";
    private bool started, audioReceived, spectrumReceived;
    private double center, rate, frequency, viewCenter, viewSpan;
    private string mode="usb", sdrId="", profileId="";
    public string ProfileId => sdrId + "|" + profileId;
    private int width=2400;
    private readonly object state = new();
    public bool IsConnected => socket.State == WebSocketState.Open && !stop.IsCancellationRequested;
    public double Center { get { lock(state)return center; } }
    public double SampleRate { get { lock(state)return rate; } }
    public double Frequency { get { lock(state)return frequency; } }
    public event Action<byte[]>? Audio;
    public event Action<byte[]>? Spectrum;
    public event Action<string>? Status;
    public event Action<double,double,double>? Configuration;
    public event Action<(string Id,string Name)[]>? Profiles;
    public event Action<string,string>? Details;

    public async Task ConnectAsync(string address,double hz,string modulation,int bandwidth)
    {
        if(!double.IsFinite(hz)||hz<=0)throw new ArgumentException("Invalid frequency.");
        frequency=hz;mode=modulation;width=bandwidth;
        var uri=new Uri(address.TrimEnd('/')+"/");
        var endpoint=new UriBuilder(new Uri(uri,"ws/")){Scheme=uri.Scheme=="https"?"wss":"ws"};
        socket.Options.SetRequestHeader("Origin",uri.GetLeftPart(UriPartial.Authority));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(endpoint.Uri,timeout.Token).ConfigureAwait(false);
        await SendText("SERVER DE CLIENT client=KiwiDX type=receiver").ConfigureAwait(false);
        await Send(new {type="connectionproperties",@params=new {output_rate=12000,hd_output_rate=12000}}).ConfigureAwait(false);
        receiving=Task.Run(Receive);
        try { await ready.Task.WaitAsync(timeout.Token).ConfigureAwait(false); }
        catch { stop.Cancel();socket.Abort();throw; }
    }
    private Task Send(object value)=>SendText(JsonSerializer.Serialize(value));
    private async Task SendText(string value)
    {
        await sendLock.WaitAsync(stop.Token).ConfigureAwait(false);
        try { await socket.SendAsync(Encoding.UTF8.GetBytes(value),WebSocketMessageType.Text,true,stop.Token).ConfigureAwait(false); }
        finally {sendLock.Release();}
    }
    private async Task Receive()
    {
        try {
            var buffer=new byte[65536];
            while(!stop.IsCancellationRequested){
                using var message=new MemoryStream();WebSocketReceiveResult part;
                do {part=await socket.ReceiveAsync(buffer,stop.Token).ConfigureAwait(false);if(part.MessageType==WebSocketMessageType.Close)throw new IOException("Receiver closed the connection.");message.Write(buffer,0,part.Count);if(message.Length>4_194_304)throw new IOException("Receiver packet too large.");}while(!part.EndOfMessage);
                var data=message.ToArray();
                if(part.MessageType==WebSocketMessageType.Text)await Text(Encoding.UTF8.GetString(data)).ConfigureAwait(false);else Binary(data);
            }
        }catch(Exception ex){if(!stop.IsCancellationRequested){ready.TrySetException(ex);stop.Cancel();socket.Abort();Status?.Invoke("Connection lost: "+ex.Message);}}
    }
    private async Task Text(string text)
    {
        if(text.StartsWith("CLIENT DE SERVER",StringComparison.Ordinal))return;
        using var doc=JsonDocument.Parse(text);var root=doc.RootElement;
        if(!root.TryGetProperty("type",out var type)||!root.TryGetProperty("value",out var value))return;
        switch(type.GetString()){
        case "config":
            if(value.TryGetProperty("sdr_id",out var sid))sdrId=sid.ToString();
            if(value.TryGetProperty("profile_id",out var pid))profileId=pid.ToString();
            if(value.TryGetProperty("profile_id",out _))audioCodec=new OpenWebRxAdpcm();
            lock(state){
                if(value.TryGetProperty("audio_compression",out var a))audioCompression=a.GetString()??"none";
                if(value.TryGetProperty("fft_compression",out var f))fftCompression=f.GetString()??"none";
                if(value.TryGetProperty("center_freq",out var c))center=c.GetDouble();
                if(value.TryGetProperty("samp_rate",out var r))rate=r.GetDouble();
                if(rate>0 && (frequency<center-rate/2 || frequency>center+rate/2))frequency=center;
            }
            if(audioCompression is not ("none" or "adpcm")||fftCompression is not ("none" or "adpcm"))throw new NotSupportedException("Unsupported OpenWebRX compression.");
            if(!double.IsFinite(center)||!double.IsFinite(rate)||rate<0||rate>1e10)throw new IOException("Invalid receiver frequency range.");
            if(rate>0){
                if(!started){started=true;await Send(new {type="dspcontrol",action="start"}).ConfigureAwait(false);}
                await SendTuning().ConfigureAwait(false);
                Configuration?.Invoke(Center,SampleRate,Frequency);
            }
            break;
        case "profiles":
            Profiles?.Invoke(value.EnumerateArray().Select(v=>(v.GetProperty("id").GetString()!,v.GetProperty("name").GetString()!)).ToArray());break;
        case "receiver_details":
            string Get(string n)=>value.TryGetProperty(n,out var v)?v.ToString():"Not reported";
            Details?.Invoke(Get("receiver_name"),"Location: "+Get("receiver_location")+Environment.NewLine+"Antenna: "+Get("receiver_antenna"));break;
        case "sdr_error":case "demodulator_error":case "backoff":throw new IOException(value.ToString());
        }
    }
    private void Binary(byte[] data)
    {
        if(data.Length<2)return;
        if(data[0]==2){
            var pcm=audioCompression=="adpcm"?audioCodec.DecodeAudio(data.AsSpan(1)):data[1..];
            if(pcm.Length%2!=0)throw new IOException("Invalid PCM packet.");
            if(pcm.Length>0){Audio?.Invoke(pcm);audioReceived=true;}
        }else if(data[0]==1){
            var levels=DecodeSpectrum(data.AsSpan(1),fftCompression);
            double c,r,vc,vs;lock(state){c=center;r=rate;vc=viewCenter;vs=viewSpan;}
            if(r<=0)return;if(vs<=0){vc=c;vs=r;}
            var line=new byte[1200];
            for(int x=0;x<line.Length;x++){
                var bin=(int)Math.Floor(((vc-vs/2+x*vs/line.Length)-(c-r/2))/r*levels.Length);
                line[x]=bin>=0&&bin<levels.Length?(byte)Math.Clamp(Math.Round(levels[bin]+255),0,255):(byte)0;
            }
            Spectrum?.Invoke(line);spectrumReceived=true;
        }
        if(audioReceived&&spectrumReceived)ready.TrySetResult();
    }
    internal static float[] DecodeSpectrum(ReadOnlySpan<byte> data,string compression)
    {
        if(compression=="adpcm"){
            var decoder=new OpenWebRxAdpcm();var samples=decoder.Decode(data);
            if(samples.Length<10)throw new IOException("Invalid compressed spectrum.");
            return samples.Skip(10).Select(v=>v/100f).ToArray();
        }
        if(data.Length%4!=0)throw new IOException("Invalid spectrum packet.");
        var result=new float[data.Length/4];for(int i=0;i<result.Length;i++){result[i]=BinaryPrimitives.ReadSingleLittleEndian(data.Slice(i*4,4));if(!float.IsFinite(result[i]))result[i]=-255;}
        return result;
    }
    private Task SendTuning(){lock(state)return Send(new {type="dspcontrol",@params=new {mod=mode,offset_freq=frequency-center,low_cut=mode=="lsb"?-width:mode is "usb" or "cw"?0:-width/2,high_cut=mode=="lsb"?0:mode is "usb" or "cw"?width:width/2}});}
    private async void Command(Func<Task> command){try{await command().ConfigureAwait(false);}catch(Exception ex){if(!stop.IsCancellationRequested)Status?.Invoke("OpenWebRX: "+ex.Message);}}
    public void Tune(double? hz=null,string? modulation=null,int? bandwidth=null){lock(state){if(hz.HasValue){if(rate>0&&Math.Abs(hz.Value-center)>rate/2){Status?.Invoke("Frequency outside the active profile. Select an OpenWebRX profile from Receiver.");Configuration?.Invoke(center,rate,frequency);return;}frequency=hz.Value;}if(modulation is not null)mode=modulation;if(bandwidth.HasValue)width=bandwidth.Value;}if(started)Command(SendTuning);}
    public void SetView(double c,double span){lock(state){viewCenter=c;viewSpan=span;}}
    public void SelectProfile(string id)=>Command(async()=>{started=false;await Send(new {type="selectprofile",@params=new {profile=id}}).ConfigureAwait(false);});
    public async ValueTask DisposeAsync(){stop.Cancel();socket.Abort();if(receiving is not null)await receiving.ConfigureAwait(false);socket.Dispose();sendLock.Dispose();stop.Dispose();}
}

internal sealed class OpenWebRxAdpcm
{
    private int predictor,index,phase,matched,headerLength,remaining;
    private readonly byte[] header=new byte[4];
    private static readonly int[] adjust={-1,-1,-1,-1,2,4,6,8};
    private static readonly int[] steps={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
    private short Nibble(int n){int step=steps[index],delta=step/8;if((n&1)!=0)delta+=step/4;if((n&2)!=0)delta+=step/2;if((n&4)!=0)delta+=step;predictor=Math.Clamp(predictor+((n&8)!=0?-delta:delta),short.MinValue,short.MaxValue);index=Math.Clamp(index+adjust[n&7],0,88);return (short)predictor;}
    public short[] Decode(ReadOnlySpan<byte> bytes){var output=new short[bytes.Length*2];for(int i=0;i<bytes.Length;i++){output[i*2]=Nibble(bytes[i]&15);output[i*2+1]=Nibble(bytes[i]>>4);}return output;}
    public byte[] DecodeAudio(ReadOnlySpan<byte> bytes){using var output=new MemoryStream();foreach(var b in bytes){if(phase==0){if(b=="SYNC"[matched])matched++;else matched=b=='S'?1:0;if(matched==4){phase=1;headerLength=0;matched=0;}}else if(phase==1){header[headerLength++]=b;if(headerLength==4){index=BinaryPrimitives.ReadInt16LittleEndian(header);predictor=BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(2));if(index is <0 or >88)throw new IOException("Invalid ADPCM state.");remaining=1001;phase=2;}}else{foreach(int n in new[]{b&15,b>>4}){short v=Nibble(n);output.WriteByte((byte)v);output.WriteByte((byte)(v>>8));}if(--remaining==0)phase=0;}}return output.ToArray();}
}
