using System.Globalization;
using System.Text.RegularExpressions;
namespace KiwiDX;

internal sealed record ReceiverShare(string Url, double FrequencyHz, string Mode, string Protocol)
{
    internal string Link => $"kiwidx://tune?rx={Uri.EscapeDataString(Url)}&hz={FrequencyHz.ToString("0.###", CultureInfo.InvariantCulture)}&mode={Uri.EscapeDataString(Mode)}&protocol={Uri.EscapeDataString(Protocol)}";
    internal string Draft => $"{Url} | {FrequencyHz / 1000:0.000} kHz | {Mode} {Link}";
    internal static ReceiverShare? Parse(string link)
    {
        if (link.Length > 4096 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "kiwidx" || uri.Host != "tune") return null;
        try
        {
            var values = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
            if (!values.TryGetValue("rx", out var rx) || !Uri.TryCreate(rx, UriKind.Absolute, out var target) || (target.Scheme is not ("http" or "https") && !SpyServerAddress.TryParse(rx, out _)) || target.UserInfo.Length > 0) return null;
            if (!values.TryGetValue("hz", out var hz) || !double.TryParse(hz, NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency) || !double.IsFinite(frequency) || frequency <= 0 || frequency > 1e12) return null;
            if (!values.TryGetValue("mode", out var mode) || !Regex.IsMatch(mode, "^[A-Za-z0-9+_-]{1,20}$")) return null;
            var protocol = ReceiverProtocols.Normalize(values.GetValueOrDefault("protocol"));
            return new(rx, frequency, mode.ToUpperInvariant(), target.Scheme == "sdr" ? "SpyServer" : protocol);
        }
        catch (ArgumentException) { return null; }
    }
}
