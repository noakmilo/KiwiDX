namespace KiwiDX;

public sealed class ListeningLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsHamRadio { get; set; }
    public string Server { get; set; } = "";
    public string Antenna { get; set; } = "";
    public bool IsManual { get; set; }
    public DateTimeOffset LocalTime { get; set; }
    public string RxTime { get; set; } = "Not available";
    public DateTimeOffset UtcTime { get; set; }
    public double FrequencyHz { get; set; }
    public string Band { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Station { get; set; } = "";
    public string Qth { get; set; } = "";
    public string Rst { get; set; } = "";
    public string S { get; set; } = "";
    public string I { get; set; } = "";
    public string M { get; set; } = "";
    // Preserve the old JSON field so existing reports retain their third rating.
    [System.Text.Json.Serialization.JsonIgnore]
    public string N { get => M; set => M = value; }
    public string P { get; set; } = "";
    public string O { get; set; } = "";
    public string Program { get; set; } = "";
    public string Comments { get; set; } = "";

    internal string ToTextReport()
    {
        var text = new System.Text.StringBuilder();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        text.AppendLine(IsHamRadio ? "KiwiDX - Ham Radio Report" : "KiwiDX - Shortwave Listening Report");
        text.AppendLine();
        text.AppendLine($"Rx: {Server}");
        text.AppendLine($"Antenna: {(string.IsNullOrWhiteSpace(Antenna) ? "Not reported" : Antenna)}");
        text.AppendLine($"Local time: {LocalTime.ToString("yyyy-MM-dd HH:mm:ss zzz", culture)}");
        text.AppendLine($"RX time: {RxTime}");
        text.AppendLine($"UTC time: {UtcTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", culture)}");
        text.AppendLine($"Frequency: {(FrequencyHz / 1000).ToString("0.000", culture)} kHz");
        text.AppendLine($"Band: {Band}");
        if (IsHamRadio)
        {
            text.AppendLine($"Mode: {Mode}");
            text.AppendLine($"Callsign: {Station}");
            text.AppendLine($"QTH: {Qth}");
            text.AppendLine($"RST: {Rst}");
        }
        else
        {
            text.AppendLine($"Station callsign: {Station}");
            text.AppendLine("SIMPO CODE:");
            text.AppendLine($"S (Signal): {S}");
            text.AppendLine($"I (Interference): {I}");
            text.AppendLine($"N (Noise): {N}");
            text.AppendLine($"P (Propagation): {P}");
            text.AppendLine($"O (Overall): {O}");
            text.AppendLine($"Program heard: {Program}");
        }
        text.AppendLine();
        text.AppendLine("Comments:");
        text.AppendLine(Comments);
        return text.ToString();
    }
}

internal static class ListeningLogStore
{
    internal static List<ListeningLog> Read(string path) => File.Exists(path)
        ? System.Text.Json.JsonSerializer.Deserialize<List<ListeningLog>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The log file does not contain a log list.")
        : new();

    internal static void Append(string path, ListeningLog entry)
    {
        var entries = Read(path);
        entries.Add(entry);
        UserData.Write(path, entries);
    }

    internal static void Update(string path, ListeningLog entry)
    {
        var entries = Read(path);
        var index = entries.FindIndex(item => item.Id == entry.Id);
        if (index < 0) throw new InvalidOperationException("This report no longer exists. Reopen the log history.");
        entries[index] = entry;
        UserData.Write(path, entries);
    }

    internal static void Delete(string path, Guid id)
    {
        var entries = Read(path);
        if (entries.RemoveAll(item => item.Id == id) == 0)
            throw new InvalidOperationException("This report no longer exists. Reopen the log history.");
        UserData.Write(path, entries);
    }
}
