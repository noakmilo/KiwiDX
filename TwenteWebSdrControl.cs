using System.Globalization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KiwiDX;

internal sealed class TwenteWebSdrControl : UserControl
{
    private readonly WebView2 webView = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly SemaphoreSlim scriptGate = new(1, 1);
    private double frequencyHz;
    private string mode;
    private int bandwidth;
    private int volume;
    private bool playing;
    private bool ready;

    public event EventHandler<string>? StatusChanged;

    public TwenteWebSdrControl(double frequencyHz, string mode, int bandwidth, int volume)
    {
        this.frequencyHz = frequencyHz;
        this.mode = NormalizeMode(mode);
        this.bandwidth = bandwidth;
        this.volume = volume;
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        Controls.Add(webView);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KiwiDX", "Twente.WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            var frequencyKhz = frequencyHz / 1_000d;
            webView.Source = new Uri($"http://websdr.ewi.utwente.nl:8901/?tune={frequencyKhz:0.###}{mode}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Twente WebSDR failed to initialize: {ex.Message}");
        }
    }

    public void SetFrequency(double value) { frequencyHz = value; QueueScript($"if(typeof setfreqif==='function')setfreqif({JsNumber(value / 1_000d)});"); }
    public void SetMode(string value) { mode = NormalizeMode(value); QueueScript($"if(typeof set_mode==='function')set_mode('{mode}');"); SetBandwidth(bandwidth); }
    public void SetBandwidth(int value) { bandwidth = value; QueueScript(BuildBandwidthScript()); }
    public void SetVolume(int value) { volume = Math.Clamp(value, 0, 100); QueueScript(BuildAudioScript()); }
    public void Play() { playing = true; QueueScript(BuildAudioScript()); }
    public void Stop() { playing = false; QueueScript(BuildAudioScript()); }
    public void ZoomIn() => QueueScript("if(typeof wfset==='function')wfset(0);");
    public void ZoomOut() => QueueScript("if(typeof wfset==='function')wfset(1);");
    public void CenterWaterfall() => QueueScript("if(typeof wfset==='function')wfset(3);");

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            StatusChanged?.Invoke(this, $"Twente WebSDR failed to load: {e.WebErrorStatus}");
            return;
        }

        ready = true;
        await RunScriptAsync("""
            (() => {
                const style = document.createElement('style');
                style.textContent = `
                    html, body { margin:0 !important; padding:0 !important; overflow:hidden !important; background:#000 !important; }
                    body > * { display:none !important; }
                    body > .mainspan { display:block !important; margin:0 !important; padding:0 !important; background:#000 !important; }
                    .mainspan > * { display:none !important; }
                    .mainspan > #maincontrols { display:block !important; position:relative !important; margin:0 !important; background:#000 !important; }
                    #wfccontainer, #wfcontainer, #waterfalls { display:block !important; margin:0 !important; }
                `;
                document.head.appendChild(style);
                const wide = document.getElementById('wfwidecheckbox');
                if (wide && !wide.checked) { wide.checked = true; if (typeof stretch_waterfalls === 'function') stretch_waterfalls(); }
            })();
            """);
        await ApplyStateAsync();
        StatusChanged?.Invoke(this, "Connected to the University of Twente WebSDR.");
    }

    private async Task ApplyStateAsync()
    {
        await RunScriptAsync($"if(typeof setfreqif==='function')setfreqif({JsNumber(frequencyHz / 1_000d)});");
        await RunScriptAsync($"if(typeof set_mode==='function')set_mode('{mode}');");
        await RunScriptAsync(BuildBandwidthScript());
        await RunScriptAsync(BuildAudioScript());
    }

    private string BuildBandwidthScript()
    {
        var widthKhz = bandwidth / 1_000d;
        var low = mode == "lsb" ? -widthKhz : mode is "am" or "fm" ? -widthKhz / 2 : 0;
        var high = mode == "lsb" ? 0 : mode is "am" or "fm" ? widthKhz / 2 : widthKhz;
        return $"if(typeof updbw==='function'){{lo={JsNumber(low)};hi={JsNumber(high)};updbw();}}";
    }

    private string BuildAudioScript()
    {
        var webVolume = -20d + volume / 100d * 26d;
        var muted = !playing || volume == 0;
        return $"if(typeof set_volume==='function')set_volume({JsNumber(webVolume)});" +
               $"if(typeof setmute==='function')setmute({muted.ToString().ToLowerInvariant()});" +
               (playing ? "if(window.soundapplet&&typeof soundapplet.audioresume==='function')soundapplet.audioresume();" : "");
    }

    private void QueueScript(string script)
    {
        if (ready && !IsDisposed) _ = RunScriptAsync(script);
    }

    private async Task RunScriptAsync(string script)
    {
        if (!ready || IsDisposed || webView.CoreWebView2 is null) return;
        await scriptGate.WaitAsync();
        try { await webView.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        finally { scriptGate.Release(); }
    }

    private static string JsNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string NormalizeMode(string value) => value.ToLowerInvariant() switch
    {
        "lsb" => "lsb", "usb" => "usb", "cw" => "cw", "nfm" => "fm", _ => "am"
    };

    protected override void Dispose(bool disposing)
    {
        ready = false;
        if (disposing)
        {
            webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
