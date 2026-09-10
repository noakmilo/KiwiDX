using KiwiDX;

var root = Path.Combine(Path.GetTempPath(), "KiwiDX-tests-" + Guid.NewGuid().ToString("N"));
var old = Path.Combine(root, "portable");
var data = Path.Combine(root, "profile");
Directory.CreateDirectory(old);
Directory.CreateDirectory(data);
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}
try
{
    File.WriteAllText(Path.Combine(old, "favorites.json"), "[1]");
    File.WriteAllText(Path.Combine(old, "startup.json"), "{}");
    File.WriteAllText(Path.Combine(data, "favorites.json"), "[2]");
    Check(UserData.MigrateFrom(old, data) == 1, "Migrate missing files only");
    Check(File.ReadAllText(Path.Combine(data, "favorites.json")) == "[2]", "Existing preferences survive migration");
    Check(File.ReadAllText(Path.Combine(old, "favorites.json")) == "[1]", "Portable originals are preserved");
    Check(UserData.MigrateFrom(old, data) == 0, "Repeated migration is idempotent");
    var path = Path.Combine(data, "favorites.json");
    UserData.Write(path, new[] { 3 });
    Check(File.ReadAllText(path + ".bak") == "[2]", "Atomic save preserves previous content in backup");
    UserData.Write(path, new[] { 4 });
    Check(System.Text.Json.JsonSerializer.Deserialize<int[]>(File.ReadAllText(path + ".bak"))![0] == 3, "Repeated atomic saves rotate backup");
    File.WriteAllText(Path.Combine(old, "bookmarks.json"), "invalid");
    try { UserData.MigrateFrom(old, data); throw new Exception("Invalid JSON accepted"); }
    catch (System.Text.Json.JsonException) { }
    Check(!File.Exists(Path.Combine(data, "bookmarks.json")), "Malformed migration does not create destination");
    Check(Directory.GetFiles(data, "*.tmp").Length == 0, "No temporary files remain");
    var logsPath = Path.Combine(data, "listening-logs.json");
    var time = DateTimeOffset.UtcNow;
    ListeningLogStore.Append(logsPath, new ListeningLog { IsHamRadio = true, Station = "CO2TEST", Server = "http://receiver", LocalTime = time.ToOffset(TimeSpan.FromHours(-4)), UtcTime = time, RxTime = "12:30:00", FrequencyHz = 7100000, Band = "40m", Mode = "LSB", Qth = "Havana", Rst = "579", Comments = "Test" });
    ListeningLogStore.Append(logsPath, new ListeningLog { Station = "SW Station", S = "5", I = "4", M = "3", P = "2", O = "1", Program = "News", Comments = "English" });
    var logs = ListeningLogStore.Read(logsPath);
    Check(logs.Count == 2 && logs[0].IsHamRadio && !logs[1].IsHamRadio, "Both log categories survive reload");
    Check(logs[0].UtcTime == time && logs[0].LocalTime.Offset == TimeSpan.FromHours(-4) && logs[0].FrequencyHz == 7100000 && logs[0].Rst == "579", "Reception snapshot survives reload");
    Check(logs[1].S == "5" && logs[1].I == "4" && logs[1].M == "3" && logs[1].P == "2" && logs[1].O == "1" && logs[1].Program == "News", "Separate SIMPO values survive reload");
    Check(logs[1].N == "3", "Legacy third rating is preserved as Noise");
    logs[1].N = "5";
    logs[1].Comments = "Updated comments";
    ListeningLogStore.Update(logsPath, logs[1]);
    var updated = ListeningLogStore.Read(logsPath);
    Check(updated.Count == 2 && updated[1].N == "5" && updated[1].Comments == "Updated comments" && updated[0].Station == "CO2TEST", "Editing preserves other reports and persists changes");
    Check(updated[1].ToTextReport().Contains("SIMPO CODE:") && updated[1].ToTextReport().Contains("N (Noise): 5"), "Text export uses descriptive code labels");
    ListeningLogStore.Delete(logsPath, updated[0].Id);
    Check(ListeningLogStore.Read(logsPath).Single().Id == updated[1].Id, "Deleting removes only the selected report");
    Check(ListeningLogStore.Read(logsPath + ".bak").Count == 2, "Deletion retains a backup");
    Check(!updated[0].IsManual && updated[0].Antenna == "", "Older reports remain compatible without antenna or source fields");
    foreach (var ham in new[] { true, false })
    {
        var manual = new ListeningLog { IsManual = true, IsHamRadio = ham, Server = "XHDATA d-808", Antenna = "Telescopic whip", Station = "Test station" };
        ListeningLogStore.Append(logsPath, manual);
        var reloaded = ListeningLogStore.Read(logsPath).Single(item => item.Id == manual.Id);
        Check(reloaded.IsManual && reloaded.IsHamRadio == ham && reloaded.Server == manual.Server && reloaded.Antenna == manual.Antenna, "Manual receiver and antenna persist in " + (ham ? "Ham Radio" : "Shortwave"));
        Check(reloaded.ToTextReport().Contains("Rx: XHDATA d-808") && reloaded.ToTextReport().Contains("Antenna: Telescopic whip"), "Export includes manual Rx and antenna");
    }
    File.WriteAllText(logsPath, "broken");
    try { ListeningLogStore.Append(logsPath, new ListeningLog()); throw new Exception("Corrupt log accepted"); }
    catch (System.Text.Json.JsonException) { }
    Check(File.ReadAllText(logsPath) == "broken", "Corrupt history is never overwritten");
}
finally { Directory.Delete(root, recursive: true); }
