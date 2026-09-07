using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace KiwiDX;

public partial class Form1
{
    private void ExportData(bool bookmarks)
    {
        using var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = bookmarks ? "bookmarks.json" : "favorites.json", DefaultExt = "json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (bookmarks) UserData.Write(dialog.FileName, frequencyBookmarks);
            else UserData.Write(dialog.FileName, ReadList<FavoriteServer>(favoritesPath));
            statusLabel.Text = "Export completed.";
        }
        catch (Exception ex) { ShowDataError(ex); }
    }

    private static List<T> ReadList<T>(string path) => JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Expected a JSON array.");

    private void ImportData(bool bookmarks)
    {
        using var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json", Title = bookmarks ? "Import Bookmarks (merge)" : "Import Favorites (merge)" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int added;
            if (bookmarks)
            {
                var incoming = ReadList<FrequencyBookmark>(dialog.FileName);
                foreach (var item in incoming)
                {
                    if (item is null || item.Id == Guid.Empty || !double.IsFinite(item.FrequencyHz) || item.FrequencyHz < 0 ||
                        string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 10 || item.Description is null || item.Description.Length > 30 ||
                        item.ServerUrls is null || item.ServerUrls.Count == 0 || item.ServerUrls.Any(url => NormalizeServerUrl(url).Length == 0))
                        throw new InvalidDataException("Invalid bookmark. Check its ID, frequency, name (1–10 characters), description (up to 30), and server URLs.");
                    item.ServerUrls = item.ServerUrls.Select(NormalizeServerUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
                // Read persisted data strictly: never overwrite a malformed existing file with an empty list.
                var merged = ReadList<FrequencyBookmark>(bookmarksPath);
                var ids = merged.Select(item => item.Id).ToHashSet();
                var additions = incoming.Where(item => ids.Add(item.Id)).ToList();
                added = additions.Count;
                merged.AddRange(additions);
                UserData.Write(bookmarksPath, merged);
                frequencyBookmarks = merged;
                RefreshFrequencyBookmarks();
            }
            else
            {
                var incoming = ReadList<FavoriteServer>(dialog.FileName);
                if (incoming.Any(item => item is null || string.IsNullOrWhiteSpace(item.Title) || item.Location is null || NormalizeServerUrl(item.Url).Length == 0))
                    throw new InvalidDataException("Each favorite must contain a title, location, and valid HTTP/HTTPS server URL.");
                var merged = ReadList<FavoriteServer>(favoritesPath);
                var urls = merged.Select(item => NormalizeServerUrl(item.Url)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var additions = incoming.Select(item => item with { Url = NormalizeServerUrl(item.Url) }).Where(item => urls.Add(item.Url)).ToList();
                added = additions.Count;
                merged.AddRange(additions);
                UserData.Write(favoritesPath, merged);
                RefreshFavoritesMenu();
            }
            MessageBox.Show(this, $"Imported {added} new item(s). Existing entries were kept; duplicates were skipped.", "Import completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowDataError(ex); }
    }

    private void ShowDataError(Exception ex) => MessageBox.Show(this, ex.Message, "KiwiDX data", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private async Task CheckForUpdates(ToolStripMenuItem menu)
    {
        menu.Enabled = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("KiwiDX/" + Application.ProductVersion.Split('+')[0]);
            using var response = await http.GetAsync("https://api.github.com/repos/noakmilo/KiwiDX/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                MessageBox.Show(this, "No published release is available yet.", "KiwiDX updates");
                return;
            }
            response.EnsureSuccessStatusCode();
            using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                throw new InvalidDataException("The latest release has an unsupported version tag.");
            var current = Version.Parse(Application.ProductVersion.Split('+')[0]);
            if (latest <= current) MessageBox.Show(this, $"KiwiDX {current} is up to date.", "KiwiDX updates");
            else if (MessageBox.Show(this, $"KiwiDX {latest} is available (installed: {current}). Open the download page? Install the new Setup to update; your settings will be preserved.", "KiwiDX updates", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://github.com/noakmilo/KiwiDX/releases/latest") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, $"Could not check for updates.\n\n{ex.Message}", "KiwiDX updates", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!menu.IsDisposed) menu.Enabled = true; }
    }
}
