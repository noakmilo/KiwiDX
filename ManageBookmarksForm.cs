namespace KiwiDX;

internal sealed class ManageBookmarksForm : Form
{
    private readonly List<FrequencyBookmark> bookmarks;
    private readonly IReadOnlyList<FavoriteServer> servers;
    private readonly string currentServerUrl;
    private readonly ListBox list = new() { Dock = DockStyle.Fill };
    public bool Changed { get; private set; }

    public ManageBookmarksForm(List<FrequencyBookmark> bookmarks, IReadOnlyList<FavoriteServer> servers, string currentServerUrl)
    {
        this.bookmarks = bookmarks;
        this.servers = servers;
        this.currentServerUrl = currentServerUrl;
        Text = "Manage Bookmarks";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 330);
        Size = new Size(620, 410);
        BackColor = Color.FromArgb(30, 36, 42);
        ForeColor = Color.Gainsboro;

        list.BackColor = Color.FromArgb(20, 25, 29);
        list.ForeColor = Color.White;
        list.Font = new Font(Font.FontFamily, 10);
        list.DoubleClick += (_, _) => EditSelected();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(7) };
        var close = new Button { Text = "Close", AutoSize = true };
        var delete = new Button { Text = "Delete", AutoSize = true, BackColor = Color.FromArgb(145, 45, 45), ForeColor = Color.White, UseVisualStyleBackColor = false };
        var visibility = new Button { Text = "Hide / Show", AutoSize = true };
        var edit = new Button { Text = "Edit", AutoSize = true };
        close.Click += (_, _) => Close();
        delete.Click += (_, _) => DeleteSelected();
        visibility.Click += (_, _) => ToggleSelectedVisibility();
        edit.Click += (_, _) => EditSelected();
        buttons.Controls.AddRange(new Control[] { close, delete, visibility, edit });
        Controls.Add(list);
        Controls.Add(buttons);
        RefreshList();
    }

    private FrequencyBookmark? Selected => list.SelectedItem as FrequencyBookmark;

    private void RefreshList(Guid? selectedId = null)
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var bookmark in bookmarks.OrderBy(item => item.FrequencyHz))
            list.Items.Add(bookmark);
        list.EndUpdate();
        if (selectedId is Guid id)
            list.SelectedItem = bookmarks.FirstOrDefault(item => item.Id == id);
    }

    private void EditSelected()
    {
        var selected = Selected;
        if (selected is null) return;
        using var editor = new BookmarkEditorForm(selected.FrequencyHz, servers, currentServerUrl, selected);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        Changed = true;
        RefreshList(selected.Id);
    }

    private void ToggleSelectedVisibility()
    {
        var selected = Selected;
        if (selected is null) return;
        selected.Visible = !selected.Visible;
        Changed = true;
        RefreshList(selected.Id);
    }

    private void DeleteSelected()
    {
        var selected = Selected;
        if (selected is null) return;
        if (MessageBox.Show(this, $"Delete bookmark '{selected.Name}'?", "Delete Bookmark", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        bookmarks.Remove(selected);
        Changed = true;
        RefreshList();
    }
}
