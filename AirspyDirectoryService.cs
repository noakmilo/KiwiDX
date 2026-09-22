using System.Collections.Concurrent;
using System.Text.Json;

namespace KiwiDX;

internal sealed record SpyServerDirectoryEntry(string Name, string Url, string Device, string Description,
    string Antenna, string ServerVersion, string Platform, double? Latitude, double? Longitude,
    double MinimumFrequency, double MaximumFrequency, double Bandwidth, bool Online, int Clients, int MaxClients)
{
    public string Location => Latitude is double lat && Longitude is double lon ? FormattableString.Invariant($"{lat:F4}, {lon:F4}") : "Location not reported";
    // The official directory uses online + registered for READY; online alone can mean UNREACHABLE.
    public bool? Registered { get; init; }
    public bool Busy => MaxClients > 0 && Clients >= MaxClients;
    public bool Available => Online && Registered == true && !Busy;
    public string StatusLabel => !Online ? "Offline" : Registered is null ? "Unverified" : !Registered.Value ? "Unreachable" : Busy ? "Busy" : "Ready";
    public string StatusColor => Available ? "#18b578" : StatusLabel == "Busy" ? "#efad37" : StatusLabel == "Unverified" ? "#9aa6b2" : "#ef5350";
}

internal sealed class AirspyDirectoryService
{
    public const string Endpoint = "https://airspy.com/directory/status.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
    private static readonly ConcurrentDictionary<string, SpyServerDirectoryEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static SpyServerDirectoryEntry? Find(string url) => Cache.GetValueOrDefault(url);
    public async Task<SpyServerDirectoryEntry[]> LoadAsync(CancellationToken cancellationToken)
    {
        var entries = Parse(await Http.GetStringAsync(Endpoint, cancellationToken).ConfigureAwait(false));
        Cache.Clear();
        foreach (var entry in entries) Cache[entry.Url] = entry;
        return entries;
    }
    internal static SpyServerDirectoryEntry[] Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
        if (!doc.RootElement.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The Airspy directory returned an unrecognized response.");
        var result = new List<SpyServerDirectoryEntry>();
        foreach (var s in servers.EnumerateArray().Take(10000))
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            string Text(string key) => s.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "")[..Math.Min(v.GetString()!.Length, 2048)] : "";
            double Number(string key) => s.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n) ? n : 0;
            var host = Text("streamingHost"); var port = Number("streamingPort");
            if (host.Contains(':') && !host.StartsWith('[')) host = "[" + host + "]";
            if (port is < 1 or > 65535 || !SpyServerAddress.TryParse($"sdr://{host}:{port:0}", out var address)) continue;
            double? lat = null, lon = null;
            if (s.TryGetProperty("antennaLocation", out var location) && location.ValueKind == JsonValueKind.Object &&
                location.TryGetProperty("lat", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetDouble(out var latitude) && latitude is >= -90 and <= 90 &&
                location.TryGetProperty("long", out var b) && b.ValueKind == JsonValueKind.Number && b.TryGetDouble(out var longitude) && longitude is >= -180 and <= 180)
            { lat = latitude; lon = longitude; }
            result.Add(new(Text("ownerName"), address.Url, Text("deviceType"), Text("generalDescription"), Text("antennaType"),
                Text("serverVersion"), Text("operatingSystem"), lat, lon, Number("minimumFrequency"), Number("maximumFrequency"),
                Number("maximumDisplayedBandwidth"), s.TryGetProperty("online", out var online) && online.ValueKind == JsonValueKind.True,
                (int)Math.Clamp(Number("currentClientCount"), 0, 100000), (int)Math.Clamp(Number("maxClients"), 0, 100000)) {
                Registered = s.TryGetProperty("registered", out var registered) && registered.ValueKind is JsonValueKind.True or JsonValueKind.False ? registered.GetBoolean() : null
            });
        }
        return result.ToArray();
    }
}
