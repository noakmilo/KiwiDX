using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KiwiDX;

public sealed class ReceiverMapForm : Form
{
    private const string MapUrl = "http://rx.linkfanel.net/";
    private const string ReceiverbookUrl = "https://www.receiverbook.de/map";
    private bool receiverbook;
    private bool airspy;
    private bool loadingAirspyDocument;
    private readonly CancellationTokenSource mapCancellation = new();
    private readonly Button airspyMap = new() { Text = "Airspy SpyServer", Width = 155, Height = 38, BackColor = Color.FromArgb(30,36,42), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button switchMap = new() { Text = "Receiverbook map", Width = 170, Height = 38, BackColor = Color.FromArgb(30, 36, 42), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly WebView2 webView = new() { Dock = DockStyle.Fill };
    private bool selectionCommitted;
    private readonly Label statusLabel = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        Text = "Click a receiver marker, then click its station name to connect KiwiDX.",
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0),
        BackColor = Color.FromArgb(30, 36, 42),
        ForeColor = Color.White
    };

    public event EventHandler<string>? ServerSelected;

    public ReceiverMapForm()
    {
        Text = "KiwiDX Receiver Map";
        Width = 1100;
        Height = 720;
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterParent;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(webView);
        Controls.Add(statusLabel);
        Controls.Add(switchMap); switchMap.BringToFront();
        Controls.Add(airspyMap); airspyMap.BringToFront();
        airspyMap.Click += async (_, _) => await LoadAirspyAsync();
        FormClosed += (_, _) => mapCancellation.Cancel();
        void PositionSwitch() { switchMap.Location = new Point(Math.Max(8, ClientSize.Width - switchMap.Width - 16), 12); airspyMap.Location = new Point(Math.Max(8, switchMap.Left - 165), 12); }
        Resize += (_, _) => PositionSwitch(); PositionSwitch();
        switchMap.Click += (_, _) =>
        {
            if (webView.CoreWebView2 is null) return;
            airspy = false;
            receiverbook = !receiverbook;
            switchMap.Text = receiverbook ? "KiwiSDR map" : "Receiverbook map";
            statusLabel.Text = "Loading map...";
            webView.CoreWebView2.Navigate(receiverbook ? ReceiverbookUrl : MapUrl);
        };
        Shown += async (_, _) => await InitializeMapAsync();
    }

    private async Task LoadAirspyAsync()
    {
        if (webView.CoreWebView2 is null) return;
        airspy = true;
        airspyMap.Enabled = false;
        statusLabel.Text = "Loading Airspy SpyServer directory...";
        try
        {
            var entries = await new AirspyDirectoryService().LoadAsync(mapCancellation.Token);
            if (IsDisposed || !airspy) return;
            ShowAirspyEntries(entries);
            statusLabel.Text = $"Airspy SpyServer: {entries.Length} receivers. Select a marker or a receiver in the list.";
        }
        catch (OperationCanceledException) { if (!IsDisposed) statusLabel.Text = "Airspy directory request timed out. Click Airspy SpyServer to retry."; }
        catch (Exception ex) { if (!IsDisposed) statusLabel.Text = "Unable to load Airspy directory: " + ex.Message; }
        finally { if (!IsDisposed) airspyMap.Enabled = true; }
    }

    private void ShowAirspyEntries(SpyServerDirectoryEntry[] entries)
    {
        airspy = true;
        loadingAirspyDocument = true;
        webView.NavigateToString(AirspyMapPage.Create(entries));
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, UserData.PathFor("Map.WebView2"));
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            webView.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (airspy && loadingAirspyDocument && e.Uri.StartsWith("data:text/html;charset=utf-8;base64,", StringComparison.Ordinal))
                { loadingAirspyDocument = false; return; }
                if (!IsMapUrl(e.Uri)) e.Cancel = true;
            };
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess) { statusLabel.Text = $"Map failed to load: {args.WebErrorStatus}"; return; }
                if (!airspy) await InstallReceiverLinkHandlerAsync();
                switchMap.BringToFront();
                statusLabel.Text = "Map ready — select a marker and click the receiver name.";
            };
            webView.Source = new Uri(MapUrl);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Unable to load the receiver map.";
            MessageBox.Show(this, $"The receiver map could not be loaded.\n\n{ex.Message}\n\nInstall the Microsoft Edge WebView2 Runtime if it is missing.", "KiwiDX Receiver Map", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task InstallReceiverLinkHandlerAsync() => await webView.CoreWebView2.ExecuteScriptAsync("""
        if (location.hostname === 'www.receiverbook.de' || location.hostname === 'receiverbook.de') {
            if (!document.getElementById('kiwidx-map-style')) {
                const style = document.createElement('style'); style.id = 'kiwidx-map-style';
                style.textContent = 'body>nav.navbar,body>header,body>footer.footer{display:none!important}html,body{margin:0!important;padding:0!important;height:100%!important;overflow:hidden!important}.content-container.container{margin:0!important;padding:0!important;max-width:none!important;width:100%!important;height:100vh!important}.map-container{width:100%!important;height:100vh!important}';
                document.head.appendChild(style);
                window.dispatchEvent(new Event('resize'));
                const map = window.jQuery && window.jQuery('.map-container').data('map');
                if (map && window.google?.maps) google.maps.event.trigger(map, 'resize');
            }
        }
        if (!window.__kiwiDxReceiverHandler) {
            window.__kiwiDxReceiverHandler = true;
            document.addEventListener('click', function(event) {
                const anchor = event.target.closest('a[href]');
                if (!anchor || !anchor.closest('.leaflet-popup-content, .gm-style-iw .infobox')) return;
                if (!/^https?:$/.test(new URL(anchor.href).protocol)) return;
                event.preventDefault();
                event.stopPropagation();
                window.chrome.webview.postMessage(anchor.href);
            }, true);
        }
        """);

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!(airspy && e.Source == "about:blank") && !IsMapUrl(e.Source)) return;
        var url = e.TryGetWebMessageAsString();
        SelectServer(url);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        // Receiver selection is handled only by marker links, never attribution or other popups.
    }

    private void SelectServer(string url)
    {
        if (selectionCommitted) return;
        if (!(airspy && SpyServerAddress.TryParse(url, out _)) && !IsReceiverUrl(url)) { statusLabel.Text = "That map link is not a compatible receiver address."; return; }
        selectionCommitted = true;
        webView.Enabled = false;
        statusLabel.Text = $"Selected {url} — connecting KiwiDX...";
        ServerSelected?.Invoke(this, url);
        BeginInvoke(Close);
    }

    private static bool IsMapUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 &&
        (uri.Host.Equals("rx.linkfanel.net", StringComparison.OrdinalIgnoreCase) ||
         ((uri.Host.Equals("www.receiverbook.de", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("receiverbook.de", StringComparison.OrdinalIgnoreCase)) && uri.AbsolutePath.TrimEnd('/') == "/map"));

    private static bool IsReceiverUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        uri.UserInfo.Length == 0 &&
        !uri.Host.Equals("rx.linkfanel.net", StringComparison.OrdinalIgnoreCase) &&
        !uri.Host.Equals("www.receiverbook.de", StringComparison.OrdinalIgnoreCase) &&
        !uri.Host.Equals("receiverbook.de", StringComparison.OrdinalIgnoreCase);
}
