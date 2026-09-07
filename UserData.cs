using System.Text.Json;

namespace KiwiDX;

internal static class UserData
{
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiwiDX");
    internal static string PathFor(string name) => Path.Combine(DirectoryPath, name);

    internal static void Initialize()
    {
        Directory.CreateDirectory(DirectoryPath);
        MigrateFrom(AppContext.BaseDirectory);
    }

    // Copy only missing files; the originals remain available to older portable versions.
    internal static int MigrateFrom(string source, string? destination = null)
    {
        destination ??= DirectoryPath;
        Directory.CreateDirectory(destination);
        var count = 0;
        foreach (var name in new[] { "favorites.json", "bookmarks.json", "startup.json" })
        {
            var oldPath = Path.Combine(source, name);
            var newPath = Path.Combine(destination, name);
            if (!File.Exists(oldPath) || File.Exists(newPath)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(oldPath));
            File.Copy(oldPath, newPath, overwrite: false);
            count++;
        }
        return count;
    }

    internal static void Write<T>(string path, T value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
