namespace KiwiDX;

internal sealed class BookmarkEditorForm : Form
{
    private readonly TextBox nameBox = new() { MaxLength = 10, Width = 220 };
    private readonly TextBox descriptionBox = new() { MaxLength = 30, Width = 300 };
    private readonly Panel selectedColorPanel = new() { Width = 48, Height = 25, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckedListBox serversBox = new() { Height = 76, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
    private Color selectedColor;

    private sealed record ServerChoice(string Label, string Url)
    {
        public override string ToString() => Label;
    }

    public FrequencyBookmark Bookmark { get; }

    public BookmarkEditorForm(double frequencyHz, IReadOnlyList<FavoriteServer> servers, string currentServerUrl, FrequencyBookmark? existing = null)
    {
        Bookmark = existing ?? new FrequencyBookmark { FrequencyHz = frequencyHz };
        selectedColor = Bookmark.Color;
        Text = existing is null ? "Add Bookmark" : "Edit Bookmark";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 535);
        BackColor = Color.FromArgb(30, 36, 42);
        ForeColor = Color.Gainsboro;

        nameBox.Text = Bookmark.Name;
        descriptionBox.Text = Bookmark.Description;
        selectedColorPanel.BackColor = selectedColor;
        var choices = servers.Select(item => new ServerChoice($"{item.Title} — {item.Location}", item.Url.TrimEnd('/'))).ToList();
        var normalizedCurrent = currentServerUrl.Trim().TrimEnd('/');
        if (normalizedCurrent.Length > 0 && !choices.Any(item => item.Url.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase)))
        {
            var currentLabel = Uri.TryCreate(normalizedCurrent, UriKind.Absolute, out var currentUri) ? currentUri.Host : normalizedCurrent;
            choices.Insert(0, new ServerChoice($"Current server — {currentLabel}", normalizedCurrent));
        }
        foreach (var assignedUrl in Bookmark.ServerUrls.Where(url => !choices.Any(item => item.Url.Equals(url, StringComparison.OrdinalIgnoreCase))))
        {
            var assignedLabel = Uri.TryCreate(assignedUrl, UriKind.Absolute, out var assignedUri) ? assignedUri.Host : assignedUrl;
            choices.Add(new ServerChoice($"Assigned server — {assignedLabel}", assignedUrl));
        }
        foreach (var choice in choices)
        {
            var index = serversBox.Items.Add(choice);
            if ((existing is null && choice.Url.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase)) ||
                Bookmark.ServerUrls.Any(url => url.Equals(choice.Url, StringComparison.OrdinalIgnoreCase)))
                serversBox.SetItemChecked(index, true);
        }

        var palette = new TableLayoutPanel { ColumnCount = 12, RowCount = 6, Width = 396, Height = 198, Margin = Padding.Empty };
        for (var column = 0; column < 12; column++) palette.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 33));
        for (var row = 0; row < 6; row++) palette.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        var colors = BuildPalette();
        for (var index = 0; index < colors.Count; index++)
        {
            var color = colors[index];
            var swatch = new Button { Dock = DockStyle.Fill, Margin = new Padding(2), BackColor = color, FlatStyle = FlatStyle.Flat, TabStop = false };
            swatch.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
            swatch.Click += (_, _) => SelectColor(color);
            palette.Controls.Add(swatch, index % 12, index / 12);
        }

        var customColor = new Button { Text = "Custom color...", AutoSize = true };
        customColor.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = selectedColor, FullOpen = true, AnyColor = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) SelectColor(dialog.Color);
        };

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Size = new Size(82, 29), Location = new Point(310, 490) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(82, 29), Location = new Point(400, 490) };
        save.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(this, "Enter a bookmark name.", "Bookmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }
            var selectedServers = serversBox.CheckedItems.Cast<ServerChoice>().Select(item => item.Url).ToList();
            if (selectedServers.Count == 0)
            {
                MessageBox.Show(this, "Select at least one server for this bookmark.", "Bookmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }
            Bookmark.Name = nameBox.Text.Trim();
            Bookmark.Description = descriptionBox.Text.Trim();
            Bookmark.ColorArgb = selectedColor.ToArgb();
            Bookmark.ServerUrls = selectedServers;
        };

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 475, ColumnCount = 2, RowCount = 6, Padding = new Padding(16, 16, 16, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        grid.Controls.Add(new Label { Text = "Frequency:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(new Label { Text = $"{Bookmark.FrequencyHz / 1_000:0.000} kHz", AutoSize = true, ForeColor = Color.Gold, Anchor = AnchorStyles.Left }, 1, 0);
        grid.Controls.Add(new Label { Text = "Name:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(nameBox, 1, 1);
        grid.Controls.Add(new Label { Text = "Description:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        grid.Controls.Add(descriptionBox, 1, 2);
        grid.Controls.Add(new Label { Text = "Servers:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        grid.Controls.Add(serversBox, 1, 3);
        grid.Controls.Add(new Label { Text = "Color:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        grid.Controls.Add(palette, 1, 4);
        var colorActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        colorActions.Controls.AddRange(new Control[] { selectedColorPanel, customColor });
        grid.Controls.Add(colorActions, 1, 5);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { grid, save, cancel });
    }

    private void SelectColor(Color color) { selectedColor = color; selectedColorPanel.BackColor = color; }

    private static List<Color> BuildPalette()
    {
        var colors = new List<Color>();
        int[] lightness = { 35, 50, 65, 80, 95, 110 };
        for (var row = 0; row < 6; row++)
        for (var column = 0; column < 12; column++)
        {
            var hue = column * 30d;
            colors.Add(FromHsv(hue, row < 5 ? 0.82 : 0.25, lightness[row] / 110d));
        }
        return colors;
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}
