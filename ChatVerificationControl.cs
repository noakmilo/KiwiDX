using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KiwiDX;

// Browser is restricted to the CAPTCHA page. It never receives chat credentials,
// messages or session cookies; the native client performs admission and WebSocket IO.
internal sealed class ChatVerificationControl : UserControl
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly Uri page = new("https://kiwidx.noakmilo.com/static/verify.html");
    private TaskCompletionSource<string>? pending;
    internal ChatVerificationControl() { Dock = DockStyle.Fill; Controls.Add(browser); }
    internal async Task<string> VerifyAsync(CancellationToken cancellation)
    {
        pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (browser.CoreWebView2 is null)
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(UserData.DirectoryPath, "ChatCaptcha.WebView2"));
            await browser.EnsureCoreWebView2Async(env);
            cancellation.ThrowIfCancellationRequested();
            if (IsDisposed || browser.CoreWebView2 is null) throw new OperationCanceledException();
            browser.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (e.Uri == page.AbsoluteUri) return;
                e.Cancel = true;
                if (e.IsUserInitiated && e.Uri is "https://www.cloudflare.com/privacypolicy/" or "https://www.cloudflare.com/website-terms/")
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true }); }
                    catch (Exception) { }
                }
            };
            browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            browser.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            browser.CoreWebView2.NavigationCompleted += (_, e) => { if (!e.IsSuccess) pending?.TrySetException(new InvalidOperationException("Verification page unavailable.")); };
            browser.CoreWebView2.ProcessFailed += (_, _) => pending?.TrySetException(new InvalidOperationException("Verification browser stopped."));
            browser.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.Source != page.AbsoluteUri || e.WebMessageAsJson.Length > 8192) return;
                try
                {
                    using var data = JsonDocument.Parse(e.WebMessageAsJson);
                    if (data.RootElement.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String && token.GetString() is { Length: > 0 and <= 2048 } value)
                        pending?.TrySetResult(value);
                    else pending?.TrySetException(new InvalidOperationException("Verification failed."));
                }
                catch (JsonException) { pending?.TrySetException(new InvalidOperationException("Invalid verification response.")); }
            };
        }
        cancellation.ThrowIfCancellationRequested();
        browser.CoreWebView2.Navigate(page.AbsoluteUri);
        return await pending.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellation);
    }
}
