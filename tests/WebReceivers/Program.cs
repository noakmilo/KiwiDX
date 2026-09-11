using KiwiDX;
using System.Reflection;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
internal static class Program
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var host = new Form { ShowInTaskbar = false, Opacity = 0, Width = 900, Height = 650 };
        host.Shown += async (_, _) =>
        {
            try
            {
                Check(ReceiverProtocols.DetectHtml("<script src='websdr-base.js'>") == "WebSDR", "Detect WebSDR script");
                Check(ReceiverProtocols.DetectHtml("<div id='openwebrx-panel-receiver'>") == "OpenWebRX", "Detect OpenWebRX interface");
                Check(ReceiverProtocols.DetectHtml("KiwiSDR - also visit my OpenWebRX server") == "KiwiSDR", "Descriptions mentioning OpenWebRX cannot misclassify KiwiSDR");
                Check(ReceiverProtocols.DetectHtml("<script src='kiwi/kiwi.js'></script><div id='openwebrx-panel-receiver'>") == "KiwiSDR", "KiwiSDR assets take priority");
                Check(ReceiverProtocols.DetectHtml("KiwiSDR") == "KiwiSDR", "Keep KiwiSDR routing");
                Check(ReceiverProtocols.DetectHtml("<p>Also try websdr-base and OpenWebRX</p>") == "KiwiSDR", "Protocol names in descriptions are ignored");
                Check(ReceiverProtocols.DetectHtml("<!-- <div id='openwebrx-panel-receiver'> -->") == "KiwiSDR", "Commented interfaces are ignored");
                Check(ReceiverProtocols.DetectHtml("<DIV ID = \"openwebrx-sdr-profiles-listbox\">") == "OpenWebRX", "Case and whitespace in actual OpenWebRX containers");
                var legacy = JsonSerializer.Deserialize<FavoriteServer>("{\"Title\":\"Test\",\"Location\":\"\",\"Url\":\"http://test\"}")!;
                Check(legacy.Protocol == "Auto", "Old favorites default to Auto");
                var saved = legacy with { Protocol = "WebSDR" };
                Check(JsonSerializer.Deserialize<FavoriteServer>(JsonSerializer.Serialize(saved))!.Protocol == "WebSDR", "Favorite protocol survives reload");
                using var combo = new FavoriteServerBox(); combo.Items.Add(saved); combo.Text = "http://typed.example";
                Check(combo.Text == "http://typed.example" && combo.DropDownStyle == ComboBoxStyle.DropDown, "Favorites field permits typed URLs");
                var share = new ReceiverShare("https://receiver.example/radio?x=1&y=2", 7100123, "USB", "KiwiSDR");
                Check(ReceiverShare.Parse(share.Link) == share, "RX links preserve URL, frequency, mode and protocol");
                Check(ReceiverShare.Parse("kiwidx://tune?rx=javascript%3Aalert(1)&hz=7100000&mode=USB") is null, "RX links reject executable URLs");
                Check(ReceiverShare.Parse("kiwidx://tune?rx=https%3A%2F%2Fexample.org&hz=NaN&mode=USB") is null, "RX links reject non-finite frequency");
                Check(ReceiverShare.Parse(share.Link + "&mode=AM") is null, "RX links reject duplicate fields");
                if (args.Contains("--basic")) return;
                foreach (var protocol in new[] { "OpenWebRX", "WebSDR" })
                {
                    var path = protocol == "OpenWebRX" ? "owrx.html" : "websdr.html";
                    using var receiver = new WebReceiverControl("https://receiver.test/" + path, protocol, 7110000, "USB", 2400, 50);
                    receiver.StatusChanged += (_, status) => Console.WriteLine(status);
                    host.Controls.Add(receiver);
                    var view = (WebView2)typeof(WebReceiverControl).GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(receiver)!;
                    view.CoreWebView2InitializationCompleted += (_, e) =>
                    {
                        Console.WriteLine("WebView initialization: " + e.IsSuccess);
                        if (e.IsSuccess) view.CoreWebView2.ProcessFailed += (_, failed) => Console.WriteLine("Browser failure: " + failed.ProcessFailedKind);
                        if (e.IsSuccess) view.CoreWebView2.SetVirtualHostNameToFolderMapping("receiver.test", Path.Combine(AppContext.BaseDirectory, "fixtures"), CoreWebView2HostResourceAccessKind.DenyCors);
                    };
                    _ = view.Handle;
                    await receiver.InitializeAsync(Path.Combine(Environment.CurrentDirectory, "dist", "webview-test-profile"));
                    for (int i=0;i<40 && (!receiver.Ready || receiver.Mode != "USB" || receiver.FrequencyHz != 7110000);i++) { await Task.Delay(100); await receiver.ReadStateAsync(); }
                    if (view.CoreWebView2 is null) throw new InvalidOperationException("WebView2 browser process exited during integration testing.");
                    Console.WriteLine($"Ready={receiver.Ready} freq={receiver.FrequencyHz} mode={receiver.Mode}");
                    Check(receiver.Ready && receiver.Mode == "USB" && receiver.FrequencyHz == 7110000, protocol + " initial tuning and state");
                    var layout = await view.ExecuteScriptAsync("['duplicate','native-duplicate'].every(id=>getComputedStyle(document.getElementById(id)).display==='none') && document.getElementById('test-waterfall').getClientRects().length>0");
                    Check(layout == "true", protocol + " hides duplicate UI and keeps waterfall visible");
                    var selector = protocol == "OpenWebRX" ? "#openwebrx-waterfall-color-min" : "#wf-brightness";
                    Check(await view.ExecuteScriptAsync($"document.querySelector('{selector}').getClientRects().length>0") == "true", protocol + " waterfall controls remain visible");
                    receiver.SetBandwidth(2200);
                    await Task.Delay(100); await receiver.ReadStateAsync();
                    Check(receiver.Bandwidth == 2200, protocol + " bandwidth command");
                    await view.ExecuteScriptAsync(protocol == "OpenWebRX" ? "d.offset=20000;d.mod='lsb';" : "freq=7120;mode='LSB';");
                    await receiver.ReadStateAsync();
                    Check(receiver.FrequencyHz == 7120000 && receiver.Mode == "LSB", protocol + " web tuning readback for logging");
                    receiver.Play(); Check(!view.CoreWebView2.IsMuted, protocol + " Play unmutes");
                    receiver.Stop(); Check(view.CoreWebView2.IsMuted, protocol + " Stop mutes");
                    if (protocol == "OpenWebRX") { receiver.SetFrequency(14000000); await Task.Delay(100); await receiver.ReadStateAsync(); Check(receiver.FrequencyHz == 7120000, "Out-of-profile tuning is rejected"); }
                    host.Controls.Remove(receiver);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { host.Close(); }
        };
        Application.Run(host);
    }
}
