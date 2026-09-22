namespace KiwiDX;

internal sealed record SpyServerAddress(string Host, int Port)
{
    public string Url => new UriBuilder("sdr", Host, Port).Uri.AbsoluteUri.TrimEnd('/');
    public static bool TryParse(string? value, out SpyServerAddress address)
    {
        address = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "sdr" ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.HostNameType == UriHostNameType.Unknown ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.AbsolutePath is not ("" or "/") || uri.Port == 0) return false;
        address = new(uri.IdnHost.Trim('[', ']'), uri.Port < 0 ? 5555 : uri.Port);
        return true;
    }
}
