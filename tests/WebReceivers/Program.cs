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
                var spyFavorite = new FavoriteServer("Remote Airspy", "Test location", "sdr://example.com:5555") { Protocol="SpyServer", Antenna="Dipole" };
                Check(JsonSerializer.Deserialize<FavoriteServer>(JsonSerializer.Serialize(spyFavorite)) == spyFavorite,"SpyServer favorite round trip");
                var spyShare = new ReceiverShare(spyFavorite.Url,145500000,"NFM","SpyServer");
                Check(ReceiverShare.Parse(spyShare.Link)==spyShare,"SpyServer RX link round trip");
                Check(await ReceiverProtocols.DetectAsync(spyFavorite.Url)=="SpyServer","sdr scheme routes directly without HTTP detection");
                List<FavoriteServer>? edited = null;
                using (var manager = new ManageFavoritesForm(new(){spyFavorite}, items=>{edited=items;return true;}))
                using (var editTimer = new System.Windows.Forms.Timer { Interval=100 }) {
                    Exception? editError=null;
                    IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Descendants(x)));
                    editTimer.Tick += (_,_) => {
                        var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Edit Favorite");if(dialog is null)return;
                        editTimer.Stop();
                        try {
                            var boxes=Descendants(dialog).OfType<TextBox>().ToArray();
                            Check(boxes.Count(b=>b.ReadOnly)==2,"Favorite URL and Antenna are read-only");
                            boxes.Single(b=>b.Text==spyFavorite.Title).Text="Edited receiver";
                            boxes.Single(b=>b.Text==spyFavorite.Location).Text="My description";
                            Descendants(dialog).OfType<Button>().Single(b=>b.Text=="Save").PerformClick();
                        } catch(Exception e){editError=e;dialog.Close();}
                    };
                    editTimer.Start();
                    typeof(ManageFavoritesForm).GetMethod("EditFavorite",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(manager,null);
                    if(editError is not null)throw editError;
                }
                Check(edited is { Count:1 } && edited[0].Title=="Edited receiver" && edited[0].Location=="My description" && edited[0].HasCustomDescription && edited[0].Url==spyFavorite.Url && edited[0].Antenna==spyFavorite.Antenna && edited[0].Protocol==spyFavorite.Protocol,"Favorite editing preserves receiver identity and antenna");
                Check(JsonSerializer.Deserialize<FavoriteServer>(JsonSerializer.Serialize(edited![0]))!.HasCustomDescription,"Custom description flag persists");
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
                    Check(layout == (protocol == "OpenWebRX" ? "true" : "false"), protocol + " page visibility");
                    var selector = protocol == "OpenWebRX" ? "#openwebrx-waterfall-color-min" : "#wf-brightness";
                    Check(await view.ExecuteScriptAsync($"document.querySelector('{selector}').getClientRects().length>0") == "true", protocol + " waterfall control visibility");
                    if (protocol == "WebSDR") {
                        await view.ExecuteScriptAsync("document.body.style.paddingTop='900px';document.body.style.paddingBottom='600px'");
                        await view.ExecuteScriptAsync("(()=>{"+WebReceiverControl.TwenteScrollScript+"})()");
                        Check(await view.ExecuteScriptAsync("Math.abs((document.getElementById('wfcontainer') || document.querySelector('.html5waterfall')).getBoundingClientRect().bottom - innerHeight)<2") == "true", "Twente aligns the waterfall bottom, not the page footer");
                        var target=await view.ExecuteScriptAsync("window.__kiwidxTwenteScroll.target");
                        await view.ExecuteScriptAsync("window.scrollTo(0,0)");await Task.Delay(100);
                        Check(await view.ExecuteScriptAsync("Math.abs(scrollY-window.__kiwidxTwenteScroll.target)<2 && getComputedStyle(document.documentElement).overflowY==='hidden'") == "true", "Twente locks scroll at waterfall bottom with hidden scrollbar");
                        Check(await view.ExecuteScriptAsync("!document.getElementById('kiwidx-waterfall-layout')") == "true", "WebSDR keeps its original page layout");
                        Check(await view.ExecuteScriptAsync("document.getElementById('wfwidecheckbox').checked && document.getElementById('wfstickycheckbox').checked && window.fullWidthApplied && window.stickyApplied") == "true", "WebSDR enables full width and sticky through their handlers");
                        await view.ExecuteScriptAsync("document.getElementById('wfstickycheckbox').click()");
                        await receiver.ReadStateAsync();
                        Check(await view.ExecuteScriptAsync("!document.getElementById('wfstickycheckbox').checked") == "true", "WebSDR respects subsequent user changes to sticky");
                    }
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
