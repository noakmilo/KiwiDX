using System.Runtime.InteropServices;
namespace KiwiDX;

internal sealed class FavoriteServerBox : RadioComboBox
{
    public FavoriteServerBox()
    {
        DropDownStyle = ComboBoxStyle.DropDown;
        AutoCompleteMode = AutoCompleteMode.None;
        MaxDropDownItems = 12;
        DropDownWidth = 480;
    }
    protected override void WndProc(ref Message m)
    {
        // The editable child receives mouse clicks instead of the ComboBox itself.
        bool clickedEdit = m.Msg == 0x210 && ((long)m.WParam & 0xffff) == 0x201;
        base.WndProc(ref m);
        if (clickedEdit && !DroppedDown && Items.Count > 0) BeginInvoke(() => { if (!IsDisposed) DroppedDown = true; });
    }
}

internal static class ReceiverProtocols
{
    internal static readonly string[] Names = { "Auto", "KiwiSDR", "OpenWebRX", "WebSDR", "SpyServer" };
    internal static string Normalize(string? value) => Names.FirstOrDefault(n => n.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "Auto";
    internal static string DetectHtml(string html)
    {
        html = System.Text.RegularExpressions.Regex.Replace(html, "<!--[\\s\\S]*?-->", "");
        // Match application assets/containers, never names mentioned in station descriptions.
        if (System.Text.RegularExpressions.Regex.IsMatch(html, "(?:<script\\b[^>]*\\bsrc\\s*=\\s*[\"'][^\"']*(?:/kiwi/|kiwi(?:\\.min)?\\.js)|<[^>]+\\bid\\s*=\\s*[\"']id-kiwi)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "KiwiSDR";
        if (System.Text.RegularExpressions.Regex.IsMatch(html, "<[^>]+\\bid\\s*=\\s*[\"']openwebrx-(?:panel-receiver|sdr-profiles-listbox)[\"']", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "OpenWebRX";
        if (System.Text.RegularExpressions.Regex.IsMatch(html, "<script\\b[^>]*\\bsrc\\s*=\\s*[\"'][^\"']*websdr-(?:base|controls|sound)(?:\\.min)?\\.js", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "WebSDR";
        return "KiwiSDR";
    }
    internal static async Task<string> DetectAsync(string url)
    {
        if (SpyServerAddress.TryParse(url, out _)) return "SpyServer";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 2_000_000 };
        try { return DetectHtml(await http.GetStringAsync(url)); }
        catch (HttpRequestException) { return "KiwiSDR"; }
        catch (TaskCanceledException) { return "KiwiSDR"; }
    }
}
