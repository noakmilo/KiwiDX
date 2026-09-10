using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace KiwiDX;

internal sealed class CommunityChatControl : UserControl
{
    private readonly TextBox address = new() { Width = 275, PlaceholderText = "https://chat.your-domain.com" };
    private readonly Button open = new() { Text = "Open chat", AutoSize = true };
    private readonly Label status = new() { Text = "Enter your Community Chat service URL.", AutoSize = true };
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };
    private Uri? origin;
    private bool ready;
    internal Func<Task<ReceiverShare?>>? GetReceiver { get; set; }
    internal Func<ReceiverShare, Task>? TuneReceiver { get; set; }
    public CommunityChatControl()
    {
        Dock = DockStyle.Fill;
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        header.Controls.AddRange(new Control[] { address, open, status });
        Controls.Add(view); Controls.Add(header);
        try { var path = UserData.PathFor("community-chat.json"); if (File.Exists(path)) address.Text = JsonSerializer.Deserialize<ChatSettings>(File.ReadAllText(path))?.Url ?? ""; }
        catch (Exception) { status.Text = "Re-enter the chat service URL."; }
        open.Click += async (_, _) => await OpenAsync();
    }
    private bool IsChatOrigin(string value) => origin is not null && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority);
    private async Task OpenAsync()
    {
        if (!Uri.TryCreate(address.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        { status.Text = "Use the HTTPS root URL of your chat service."; return; }
        open.Enabled = false; ready = false; origin = uri;
        try
        {
            if (view.CoreWebView2 is null)
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(UserData.DirectoryPath, "Chat.WebView2"));
                if (IsDisposed) return;
                await view.EnsureCoreWebView2Async(env);
                if (IsDisposed || view.CoreWebView2 is null) return;
                view.CoreWebView2.NavigationStarting += (_, e) => { ready = false; if (!IsChatOrigin(e.Uri)) e.Cancel = true; };
                view.CoreWebView2.NewWindowRequested += (_, e) =>
                {
                    e.Handled = true;
                    if (e.IsUserInitiated && e.Uri is "https://www.cloudflare.com/privacypolicy/" or "https://www.cloudflare.com/website-terms/")
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
                        catch (Exception) { status.Text = "Could not open the privacy/terms page in your browser."; }
                    }
                };
                view.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                view.CoreWebView2.NavigationCompleted += (_, e) => { ready = e.IsSuccess && IsChatOrigin(view.Source?.ToString() ?? ""); status.Text = ready ? "Choose a nick and Connect." : "Chat page could not load."; };
                view.CoreWebView2.WebMessageReceived += OnMessage;
                view.CoreWebView2.ProcessFailed += (_, _) => { ready = false; status.Text = "Chat browser stopped. Reopen chat to retry."; };
            }
            view.CoreWebView2.Navigate(uri.AbsoluteUri);
            UserData.Write(UserData.PathFor("community-chat.json"), new ChatSettings(uri.AbsoluteUri));
        }
        catch (Exception ex) { status.Text = "Could not open chat: " + ex.Message; }
        finally { if (!IsDisposed) open.Enabled = true; }
    }
    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!ready || !IsChatOrigin(e.Source)) return;
        try
        {
            if (e.WebMessageAsJson.Length > 8192) return;
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type)) return;
            if (type.GetString() == "pasteRx" && GetReceiver is not null)
            {
                var receiver = await GetReceiver();
                if (receiver is null) { status.Text = "Connect to a receiver before sharing."; return; }
                if (ready && !IsDisposed && IsChatOrigin(e.Source)) await view.ExecuteScriptAsync("window.setRxDraft?.(" + JsonSerializer.Serialize(receiver.Draft) + ");");
            }
            else if (type.GetString() == "tune" && root.TryGetProperty("link", out var link) && link.ValueKind == JsonValueKind.String && TuneReceiver is not null)
            {
                var receiver = ReceiverShare.Parse(link.GetString()!);
                if (receiver is not null) await TuneReceiver(receiver);
            }
        }
        catch (Exception ex) { if (!IsDisposed) status.Text = "Chat action failed: " + ex.Message; }
    }
    private sealed record ChatSettings(string Url);
}
