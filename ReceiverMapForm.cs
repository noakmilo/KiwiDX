using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KiwiDX;

public sealed class ReceiverMapForm : Form
{
    private const string MapUrl = "http://rx.linkfanel.net/";
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
        Shown += async (_, _) => await InitializeMapAsync();
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
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess) { statusLabel.Text = $"Map failed to load: {args.WebErrorStatus}"; return; }
                await InstallReceiverLinkHandlerAsync();
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
        if (!window.__kiwiDxReceiverHandler) {
            window.__kiwiDxReceiverHandler = true;
            document.addEventListener('click', function(event) {
                const anchor = event.target.closest('a[href]');
                if (!anchor || !anchor.closest('.leaflet-popup-content')) return;
                event.preventDefault();
                event.stopPropagation();
                window.chrome.webview.postMessage(anchor.href);
            }, true);
        }
        """);

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var url = e.TryGetWebMessageAsString();
        SelectServer(url);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (IsReceiverUrl(e.Uri)) SelectServer(e.Uri);
    }

    private void SelectServer(string url)
    {
        if (selectionCommitted) return;
        if (!IsReceiverUrl(url)) { statusLabel.Text = "That map link is not a compatible receiver address."; return; }
        selectionCommitted = true;
        webView.Enabled = false;
        statusLabel.Text = $"Selected {url} — connecting KiwiDX...";
        ServerSelected?.Invoke(this, url);
        BeginInvoke(Close);
    }

    private static bool IsReceiverUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        !uri.Host.Equals("rx.linkfanel.net", StringComparison.OrdinalIgnoreCase);
}
