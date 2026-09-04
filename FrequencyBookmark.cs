namespace KiwiDX;

internal sealed class FrequencyBookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double FrequencyHz { get; set; }
    public string Name { get; set; } = "Bookmark";
    public string Description { get; set; } = "";
    public int ColorArgb { get; set; } = Color.Gold.ToArgb();
    public bool Visible { get; set; } = true;
    public List<string> ServerUrls { get; set; } = new();

    public Color Color => Color.FromArgb(ColorArgb);
    public override string ToString() => $"{Name}  ({FrequencyHz / 1_000:0.000} kHz)  [{ServerUrls.Count} server{(ServerUrls.Count == 1 ? "" : "s")}]{(Visible ? "" : "  [Hidden]")}";
}
