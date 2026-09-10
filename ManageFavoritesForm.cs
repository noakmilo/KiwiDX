namespace KiwiDX;

internal sealed class ManageFavoritesForm : Form
{
    private List<FavoriteServer> favorites;
    private readonly Func<List<FavoriteServer>, bool> save;
    private readonly ListBox list = new() { Dock = DockStyle.Fill, DisplayMember = nameof(FavoriteServer.Title) };
    private readonly TextBox title = new() { Dock = DockStyle.Fill, MaxLength = 120 };
    private readonly TextBox description = new() { Dock = DockStyle.Fill, MaxLength = 200 };
    private readonly ComboBox protocol = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly TextBox url = new() { Dock = DockStyle.Fill, MaxLength = 2048 };

    internal ManageFavoritesForm(List<FavoriteServer> favorites, Func<List<FavoriteServer>, bool> save)
    {
        this.favorites = favorites.ToList();
        this.save = save;
        Text = "Manage Favorites";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 460);
        ClientSize = new Size(680, 480);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var notice = new Label { Dock = DockStyle.Fill, Text = "Add KiwiSDR, OpenWebRX or WebSDR receivers using their HTTP/HTTPS address.\nChoose a protocol or use Auto detection." };
        layout.Controls.Add(notice, 0, 0); layout.SetColumnSpan(notice, 2);
        layout.Controls.Add(list, 0, 1); layout.SetColumnSpan(list, 2);
        var fields = new[] { ("Name", title), ("Description", description), ("Server URL", url) };
        for (var i = 0; i < fields.Length; i++)
        {
            layout.Controls.Add(new Label { Text = fields[i].Item1, AutoSize = true, Anchor = AnchorStyles.Left }, 0, i + 2);
            layout.Controls.Add(fields[i].Item2, 1, i + 2);
        }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var add = new Button { Text = "Add", AutoSize = true };
        var delete = new Button { Text = "Delete selected", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        add.Click += (_, _) => AddFavorite();
        delete.Click += (_, _) => DeleteFavorite();
        protocol.Items.AddRange(ReceiverProtocols.Names); protocol.SelectedItem = "Auto";
        buttons.Controls.AddRange(new Control[] { new Label { Text = "Protocol", AutoSize = true }, protocol, add, delete, close });
        layout.Controls.Add(buttons, 0, 5); layout.SetColumnSpan(buttons, 2);
        var selected = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        list.SelectedIndexChanged += (_, _) => selected.Text = list.SelectedItem is FavoriteServer item ? $"{item.Url} — {item.Location}" : "";
        layout.Controls.Add(selected, 0, 6); layout.SetColumnSpan(selected, 2);
        Controls.Add(layout);
        CancelButton = close;
        RefreshList();
    }

    private void RefreshList()
    {
        list.DataSource = null;
        list.DataSource = favorites;
    }

    private void AddFavorite()
    {
        var candidate = url.Text.Trim();
        if (!candidate.Contains("://")) candidate = "http://" + candidate;
        if (string.IsNullOrWhiteSpace(title.Text) || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0)
        {
            MessageBox.Show(this, "Enter a name and a valid HTTP/HTTPS receiver URL without embedded credentials.", "Add Favorite");
            return;
        }
        var normalized = uri.ToString().TrimEnd('/');
        if (favorites.Any(item => item.Url.TrimEnd('/').Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "This receiver is already in Favorites.", "Add Favorite");
            return;
        }
        var isTwente = uri.Host.Equals("websdr.ewi.utwente.nl", StringComparison.OrdinalIgnoreCase) && uri.Port == 8901;
        var next = favorites.Append(new FavoriteServer(title.Text.Trim(), isTwente ? "[OFF Kiwi Network]" : description.Text.Trim(), normalized) { Protocol = ReceiverProtocols.Normalize(protocol.Text) }).ToList();
        if (!save(next)) return;
        favorites = next;
        RefreshList();
        title.Clear(); description.Clear(); url.Clear();
    }

    private void DeleteFavorite()
    {
        if (list.SelectedItem is not FavoriteServer selected) return;
        if (MessageBox.Show(this, $"Delete '{selected.Title}' from Favorites?", "Delete Favorite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var next = favorites.Where(item => item != selected).ToList();
        if (!save(next)) return;
        favorites = next;
        RefreshList();
    }
}
