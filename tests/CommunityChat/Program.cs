using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using KiwiDX;

var start = new ProcessStartInfo(args[0]) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "fixture.py"));
start.ArgumentList.Add(Path.GetFullPath("community-chat"));
using var server = Process.Start(start)!;
try
{
    var port = await server.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
    var origin = new Uri("http://127.0.0.1:" + port);
    using var first = new CommunityChatClient(origin);
    using var second = new CommunityChatClient(origin);
    using var rejected = new CommunityChatClient(origin);
    try { await rejected.ConnectAsync("invalid", CancellationToken.None); throw new Exception("CAPTCHA bypass"); }
    catch (HttpRequestException) { Console.WriteLine("PASS: native admission rejects invalid CAPTCHA."); }
    var a = Channel.CreateUnbounded<JsonElement>();
    var b = Channel.CreateUnbounded<JsonElement>();
    first.Message += e => a.Writer.TryWrite(e);
    second.Message += e => b.Writer.TryWrite(e);
    await first.ConnectAsync("fixture-token", CancellationToken.None);
    await second.ConnectAsync("fixture-token", CancellationToken.None);
    var loopA = first.ListenAsync(); var loopB = second.ListenAsync();
    async Task<JsonElement> Read(Channel<JsonElement> q) => await q.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    void Check(bool value, string error) { if (!value) throw new Exception(error); }
    foreach (var q in new[] { a, b })
    {
        Check((await Read(q)).GetProperty("type").GetString() == "identity", "No identity");
        Check((await Read(q)).GetProperty("channel").GetString() == "#hamradio", "No ham history");
        Check((await Read(q)).GetProperty("channel").GetString() == "#shortwave", "No SW history");
    }
    Console.WriteLine("PASS: native sockets join both channels and receive history.");
    await first.SendAsync("#hamradio", "/register NativeUser fixture-password-123");
    Check((await Read(a)).GetProperty("text").GetString()!.Contains("registered"), "Registration failed");
    await first.SendAsync("#hamradio", "/login NativeUser fixture-password-123");
    Check((await Read(a)).GetProperty("authenticated").GetBoolean(), "Login failed");
    await first.SendAsync("#shortwave", "/help");
    Check((await Read(a)).GetProperty("type").GetString() == "notice", "Help missing");
    const string link = "kiwidx://tune?rx=http%3A%2F%2Fexample.org&hz=7100000&mode=USB&protocol=KiwiSDR";
    foreach (var ch in new[] { "#hamradio", "#shortwave" })
    {
        await first.SendAsync(ch, link);
        foreach (var q in new[] { a, b })
        {
            var e = await Read(q);
            Check(e.GetProperty("type").GetString() == "message" && e.GetProperty("text").GetString() == link && e.GetProperty("channel").GetString() == ch, "Private command leaked or public message lost");
        }
    }
    Console.WriteLine("PASS: registration/login/help stay private; tuning links broadcast on both channels.");
    first.Dispose(); second.Dispose();
    await Task.WhenAll(loopA, loopB).WaitAsync(TimeSpan.FromSeconds(5));
    Console.WriteLine("PASS: native disconnect stops receive loops.");
}
finally { if (!server.HasExited) server.Kill(entireProcessTree: true); }
