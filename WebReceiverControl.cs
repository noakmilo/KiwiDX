using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace KiwiDX;

internal sealed class WebReceiverControl : UserControl
{
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private readonly string url;
    private readonly string protocol;
    private bool disposed, initialized;
    private Task? reading;
    private int navigation;
    private int volume;
    private bool playing;
    private bool muted;
    public string Protocol => protocol;
    private double initialFrequency;
    private string initialMode;
    public bool Ready { get; private set; }
    public double FrequencyHz { get; private set; }
    public string Mode { get; private set; } = "";
    public int Bandwidth { get; private set; }
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? StateChanged;

    public WebReceiverControl(string url, string protocol, double frequency, string mode, int bandwidth, int volume)
    {
        this.url = url; this.protocol = protocol; initialFrequency = frequency; initialMode = mode; this.volume = volume;
        Dock = DockStyle.Fill; Controls.Add(view);
        timer.Tick += async (_, _) => await ReadStateAsync();
    }
    public async Task InitializeAsync(string? userDataFolder = null)
    {
        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder ?? Path.Combine(UserData.DirectoryPath, "Receivers.WebView2"),
            new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
        if (disposed) return;
        await view.EnsureCoreWebView2Async(env);
        if (disposed) return;
        view.CoreWebView2.IsMuted = true;
        view.CoreWebView2.ProcessFailed += (_, e) =>
        {
            if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited or CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {
                Ready = false; timer.Stop();
                StatusChanged?.Invoke(this, "Receiver browser stopped. Disconnect and reconnect to reload it.");
            }
        };
        view.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https")) { e.Cancel = true; return; }
            var original = new Uri(url);
            bool upgrade = original.Scheme == "http" && target.Scheme == "https" && original.Host == target.Host && original.IsDefaultPort && target.IsDefaultPort;
            if (!upgrade && !original.GetLeftPart(UriPartial.Authority).Equals(target.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            { e.Cancel = true; StatusChanged?.Invoke(this, "Enter the new receiver URL in KiwiDX to switch servers."); return; }
            navigation++; Ready = false; initialized = false;
        };
        view.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; StatusChanged?.Invoke(this, "Use the server URL field to open another receiver."); };
        view.CoreWebView2.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess) { Ready = false; StatusChanged?.Invoke(this, "Receiver page failed to load: " + e.WebErrorStatus); return; }
            StatusChanged?.Invoke(this, "Receiver page loaded. Select a profile or start audio in the receiver panel if needed.");
            timer.Start();
            Play();
        };
        view.Source = new Uri(url);
    }
    private async Task<string> Script(string script)
    {
        if (disposed || view.CoreWebView2 is null) return "null";
        try { return await view.CoreWebView2.ExecuteScriptAsync("(()=>{try{" + script + "}catch(e){return null;}})()"); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException) { return "null"; }
    }
    public Task ReadStateAsync() => reading is { IsCompleted: false } ? reading : reading = ReadStateCoreAsync();
    private async Task ReadStateCoreAsync()
    {
        if (disposed) return;
        var generation = navigation;
        try
        {
            await Script(WaterfallLayoutScript(protocol));
            var json = await Script(protocol == "OpenWebRX" ? """
                if(typeof getDemodulators!=='function')return null;
                const d=getDemodulators()[0]; if(!d)return null;
                return {frequency:center_freq+d.get_offset_frequency(),mode:d.get_secondary_demod()||d.get_modulation(),width:d.high_cut-d.low_cut};
                """ : """
                if(typeof nominalfreq!=='function'&&typeof getfreq!=='function')return null;
                const f=typeof nominalfreq==='function'?nominalfreq():getfreq(); const m=typeof getmode==='function'?getmode():(typeof mode==='string'?mode:'');
                return {frequency:f*1000,mode:m,width:typeof hi==='number'&&typeof lo==='number'?(hi-lo)*1000:0};
                """);
            if (disposed || generation != navigation) return;
            if (json == "null") { Ready = false; return; }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.GetProperty("frequency").TryGetDouble(out var freq) || !double.IsFinite(freq) || freq <= 0) { Ready = false; return; }
            FrequencyHz = freq; Mode = root.GetProperty("mode").GetString()?.ToUpperInvariant() ?? "";
            Bandwidth = root.GetProperty("width").TryGetDouble(out var width) && double.IsFinite(width) ? (int)Math.Clamp(width, 0, 2_000_000) : 0;
            Ready = Mode.Length > 0;
            if (!initialized)
            {
                initialized = true;
                SetFrequency(initialFrequency); SetMode(initialMode); Play();
                StatusChanged?.Invoke(this, "Connected to " + protocol + ". Tune using KiwiDX; waterfall controls remain in the receiver panel.");
                return;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (JsonException) { Ready = false; }
    }
    // Keep the original DOM and event handlers: the native controls still call the receiver APIs.
    internal static string WaterfallLayoutScript(string protocol) => "const openRx=" + (protocol == "OpenWebRX" ? "true;" : "false;") + """
        const surface=document.querySelector(openRx?'.openwebrx-waterfall-container':'#wfccontainer');
        if(!surface)return false;
        const roots=openRx
          ? [...document.querySelectorAll('#openwebrx-frequency-container,#webrx-canvas-background,#openwebrx-error-overlay,#openwebrx-autoplay-overlay,#openwebrx-sdr-profiles-listbox,[id^="openwebrx-waterfall-color"],.openwebrx-zoom-button')]
          : [surface,...document.querySelectorAll('#audiostartbutton,.warning'),
             ...[...document.querySelectorAll('[onchange*="waterfall"],[onclick*="wfset"],#wfwidecheckbox,#wf-brightness')].map(e=>e.closest('.ctl')).filter(Boolean)];
        if(openRx && !document.querySelector('#webrx-canvas-background'))return false;
        const paths=new Set();
        for(const root of roots)for(let node=root;node;node=node.parentElement)paths.add(node);
        function trim(parent){
          for(const child of parent.children){
            if(child.matches('script,style,link'))continue;
            const keep=paths.has(child);
            child.classList.toggle('kiwidx-hidden',!keep);
            if(keep && !roots.includes(child))trim(child);
          }
        }
        trim(document.body);
        if(!document.getElementById('kiwidx-waterfall-layout')){
          const style=document.createElement('style');style.id='kiwidx-waterfall-layout';
          style.textContent=`.kiwidx-hidden{display:none!important}
            html,body{margin:0!important;padding:0!important;background:#0e1821!important;color:#e4edf6!important}
            #webrx-page-container,.openwebrx-waterfall-container{top:0!important;margin:0!important;height:100vh!important}
            #openwebrx-panel-receiver{width:259px!important}
            .mainspan{margin:0!important;padding:0!important;width:100%!important}
            .tabs{margin-top:8px!important}.tabs>.tab:not(.kiwidx-hidden){display:block!important;position:relative!important}
            .ctl{background:#15222e!important;color:#e4edf6!important}
            #wfccontainer{margin:0!important}`;
          document.head.append(style);window.dispatchEvent(new Event('resize'));
        }
        return true;
        """;

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    public void SetFrequency(double hz)
    {
        if (!Ready) { initialFrequency = hz; return; }
        _ = TuneAsync(hz);
    }
    private async Task TuneAsync(double hz)
    {
        var result = await Script(protocol == "OpenWebRX"
            ? $"if(Math.abs({Num(hz)}-center_freq)>bandwidth/2)return false;getDemodulators()[0].set_offset_frequency({Num(hz)}-center_freq);return true;"
            : $"if(typeof setfreqif==='function'){{setfreqif({Num(hz / 1000)});return true;}}return false;");
        if (result == "false") StatusChanged?.Invoke(this, "Frequency unavailable in the current profile. Select a band/profile in the receiver panel.");
    }
    public void SetMode(string mode)
    {
        if (!Ready) { initialMode = mode; return; }
        var m = mode.ToLowerInvariant(); if (protocol == "WebSDR" && m == "nfm") m = "fm";
        var value = JsonSerializer.Serialize(m);
        _ = Script(protocol == "OpenWebRX" ? $"$('#openwebrx-panel-receiver').demodulatorPanel().setMode({value});" : $"if(typeof set_mode==='function')set_mode({value});");
    }
    public void SetBandwidth(int width)
    {
        if (!Ready) return;
        var low = Mode == "LSB" ? -width : Mode is "USB" or "CW" ? 0 : -width / 2;
        var high = low + width;
        _ = Script(protocol == "OpenWebRX" ? $"getDemodulators()[0].setBandpass({{low_cut:{low},high_cut:{high}}});"
            : $"if(typeof updbw==='function'){{lo={Num(low / 1000d)};hi={Num(high / 1000d)};updbw();}}");
    }
    public void SetVolume(int value)
    {
        volume = Math.Clamp(value, 0, 100);
        if (view.CoreWebView2 is null) return;
        view.CoreWebView2.IsMuted = !playing || muted || volume == 0;
        _ = Script(protocol == "OpenWebRX" ? $"if(typeof audioEngine!=='undefined')audioEngine.setVolume({Num(volume / 100d)});"
            : $"if(typeof set_volume==='function')set_volume({Num(-20 + volume / 100d * 26)});");
    }
    public void Play() { playing = true; SetVolume(volume); _ = Script("document.querySelector('#openwebrx-autoplay-overlay .overlay-content')?.click();if(typeof audioEngine!=='undefined'&&typeof audioEngine.resume==='function')audioEngine.resume();if(window.soundapplet&&typeof soundapplet.audioresume==='function')soundapplet.audioresume();if(typeof setmute==='function')setmute(false);"); }
    public void SetMuted(bool value) { muted = value; SetVolume(volume); }
    public void Stop() { playing = false; SetVolume(volume); }
    public void ZoomIn() => _ = Script(protocol == "OpenWebRX" ? "if(typeof zoomInOneStep==='function')zoomInOneStep();" : "if(typeof wfset==='function')wfset(0);");
    public void ZoomOut() => _ = Script(protocol == "OpenWebRX" ? "if(typeof zoomOutOneStep==='function')zoomOutOneStep();" : "if(typeof wfset==='function')wfset(1);");
    public void CenterWaterfall() => _ = Script(protocol == "OpenWebRX" ? "if(typeof zoom_set==='function'){zoom_center_rel=getDemodulators()[0].get_offset_frequency();zoom_center_where=0.5;resize_canvases();mkscale();bookmarks.position();}" : "if(typeof wfset==='function')wfset(3);");
    protected override void Dispose(bool disposing)
    {
        disposed = true; Ready = false;
        if (disposing) { timer.Dispose(); view.Dispose(); }
        base.Dispose(disposing);
    }
}
