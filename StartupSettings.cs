namespace KiwiDX;

internal sealed record FavoriteServer(string Title, string Location, string Url)
{
    public string Protocol { get; init; } = "Auto";
}

internal sealed record StartupConfiguration
{
    public string ServerUrl { get; init; } = "http://sdr.hfunderground.com:8076";
    public double FrequencyMHz { get; init; } = 7.100;
    public bool ConnectOnStartup { get; init; }
    public bool PlayOnStartup { get; init; }
    public int WaterfallMinimumDb { get; init; } = -120;
    public int WaterfallMaximumDb { get; init; } = -50;
}
