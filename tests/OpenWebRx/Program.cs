using KiwiDX;
using System.Buffers.Binary;
var pcm=new OpenWebRxAdpcm();
if(pcm.Decode(new byte[]{0x71}) is not [1,12])throw new Exception("IMA nibble order");
var sync=new OpenWebRxAdpcm();
if(sync.DecodeAudio("SY"u8).Length!=0)throw new Exception("Partial sync");
var decoded=sync.DecodeAudio(new byte[]{(byte)'N',(byte)'C',0,0,0,0,0x71});
if(!decoded.SequenceEqual(new byte[]{1,0,12,0}))throw new Exception("Synchronized ADPCM");
var floats=new byte[8];BinaryPrimitives.WriteSingleLittleEndian(floats,-100);BinaryPrimitives.WriteSingleLittleEndian(floats.AsSpan(4),-75);
if(!OpenWebRxClient.DecodeSpectrum(floats,"none").SequenceEqual(new[]{-100f,-75f}))throw new Exception("FFT floats");
Console.WriteLine("PASS: PCM spectrum and ADPCM nibble order / fragmented sync");
if(args.Length==0)return;
await using var client=new OpenWebRxClient();int audio=0,lines=0;string? nextProfile=null;
client.Status+=Console.WriteLine;client.Audio+=b=>Interlocked.Add(ref audio,b.Length);client.Spectrum+=b=>Interlocked.Increment(ref lines);
client.Configuration+=(c,r,f)=>Console.WriteLine($"Configuration center={c}, span={r}, tuned={f}");
client.Profiles+=p=>{Console.WriteLine($"Profiles: {p.Length}");nextProfile=p.FirstOrDefault(v=>v.Name.Contains("40m",StringComparison.OrdinalIgnoreCase)).Id;};
await client.ConnectAsync(args[0],7100000,"usb",2400);
await Task.Delay(1500);
if(audio==0||lines==0)throw new Exception("Missing streams");
Console.WriteLine($"PASS: direct OpenWebRX reception: {audio} PCM bytes, {lines} waterfall frames");

if(nextProfile is not null){
 var changed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 var previous=client.Center;
 client.Configuration+=(c,r,f)=>{if(c!=previous)changed.TrySetResult();};
 client.SelectProfile(nextProfile);
 await changed.Task.WaitAsync(TimeSpan.FromSeconds(12));
 var beforeAudio=audio;var beforeLines=lines;await Task.Delay(1500);
 if(audio<=beforeAudio||lines<=beforeLines)throw new Exception("Profile change stopped streams");
 Console.WriteLine("PASS: live profile selection continues audio and waterfall");
}
