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
}
finally { Directory.Delete(root, recursive: true); }
