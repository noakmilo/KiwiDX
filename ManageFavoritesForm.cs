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
        ClientSize = new Size(780, 480);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var notice = new Label { Dock = DockStyle.Fill, Text = "Add HTTP/HTTPS receivers or SpyServer sdr:// addresses.\nChoose a protocol or use Auto detection." };
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
        var edit = new Button { Text = "Edit selected", AutoSize = true };
        edit.Click += (_, _) => EditFavorite();
        list.DoubleClick += (_, _) => EditFavorite();
        var delete = new Button { Text = "Delete selected", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        add.Click += (_, _) => AddFavorite();
        delete.Click += (_, _) => DeleteFavorite();
        protocol.Items.AddRange(ReceiverProtocols.Names); protocol.SelectedItem = "Auto";
        buttons.Controls.AddRange(new Control[] { new Label { Text = "Protocol", AutoSize = true }, protocol, add, edit, delete, close });
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
            (uri.Scheme is not ("http" or "https") && !SpyServerAddress.TryParse(candidate, out _)) || string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0)
        {
            MessageBox.Show(this, "Enter a name and a valid HTTP/HTTPS or sdr://host:port receiver URL without embedded credentials.", "Add Favorite");
            return;
        }
        var normalized = SpyServerAddress.TryParse(candidate, out var spy) ? spy.Url : uri.ToString().TrimEnd('/');
        if (favorites.Any(item => item.Url.TrimEnd('/').Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "This receiver is already in Favorites.", "Add Favorite");
            return;
        }
        var isTwente = uri.Host.Equals("websdr.ewi.utwente.nl", StringComparison.OrdinalIgnoreCase) && uri.Port == 8901;
        var next = favorites.Append(new FavoriteServer(title.Text.Trim(), isTwente ? "[OFF Kiwi Network]" : description.Text.Trim(), normalized) { Protocol = uri.Scheme == "sdr" ? "SpyServer" : ReceiverProtocols.Normalize(protocol.Text) }).ToList();
        if (!save(next)) return;
        favorites = next;
        RefreshList();
        title.Clear(); description.Clear(); url.Clear();
    }

    private void EditFavorite()
    {
        if (list.SelectedItem is not FavoriteServer selected) return;
        using var editor = new Form { Text = "Edit Favorite", Icon = Icon, ClientSize = new Size(600, 240),
            MinimumSize = new Size(500, 280), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var name = new TextBox { Text = selected.Title, Dock = DockStyle.Fill, MaxLength = 120 };
        var details = new TextBox { Text = selected.Location, Dock = DockStyle.Fill, MaxLength = 200 };
        var address = new TextBox { Text = selected.Url, Dock = DockStyle.Fill, ReadOnly = true };
        var antenna = new TextBox { Text = selected.Antenna, Dock = DockStyle.Fill, ReadOnly = true };
        var rows = new[] { ("Name", name), ("Description", details), ("URL", address), ("Antenna", antenna) };
        for (int i = 0; i < rows.Length; i++) {
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            fields.Controls.Add(new Label { Text = rows[i].Item1, AutoSize = true, Anchor = AnchorStyles.Left }, 0, i);
            fields.Controls.Add(rows[i].Item2, 1, i);
        }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Save" };
        apply.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(editor, "Enter a name.", "Edit Favorite"); return; }
            int index = favorites.IndexOf(selected);
            var next = favorites.ToList();
            next[index] = selected with { Title = name.Text.Trim(), Location = details.Text.Trim(), HasCustomDescription = true };
            if (!save(next)) return;
            favorites = next; RefreshList(); list.SelectedIndex = index;
            editor.DialogResult = DialogResult.OK;
        };
        buttons.Controls.AddRange(new Control[] { cancel, apply });
        fields.Controls.Add(buttons, 0, 4); fields.SetColumnSpan(buttons, 2);
        editor.Controls.Add(fields); editor.AcceptButton = apply; editor.CancelButton = cancel;
        editor.ShowDialog(this);
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
