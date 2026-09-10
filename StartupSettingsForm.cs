namespace KiwiDX;

internal sealed class StartupSettingsForm : Form
{
    private const string DefaultServer = "http://sdr.hfunderground.com:8076";
    private readonly ComboBox serverBox = new();
    private readonly NumericUpDown frequencyBox = new();
    private readonly CheckBox connectBox = new();
    private readonly CheckBox playBox = new();
    private readonly NumericUpDown waterfallMinimumBox = new();
    private readonly NumericUpDown waterfallMaximumBox = new();

    private sealed record ServerChoice(string Label, string Url)
    {
        public override string ToString() => Label;
    }

    public StartupConfiguration Configuration { get; private set; }

    public StartupSettingsForm(IReadOnlyList<FavoriteServer> favorites, StartupConfiguration current)
    {
        Configuration = current;
        Text = "Startup Settings";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(475, 325);
        BackColor = Color.FromArgb(30, 36, 42);
        ForeColor = Color.Gainsboro;

        var choices = favorites.Count > 0
            ? favorites.Select(item => new ServerChoice($"{item.Title} — {item.Location}", item.Url)).ToArray()
            : new[] { new ServerChoice("KiwiDX default server", DefaultServer) };

        serverBox.DropDownStyle = ComboBoxStyle.DropDownList;
        serverBox.Width = 330;
        serverBox.Items.AddRange(choices);
        serverBox.SelectedItem = choices.FirstOrDefault(item => item.Url.Equals(current.ServerUrl, StringComparison.OrdinalIgnoreCase)) ?? choices[0];

        frequencyBox.DecimalPlaces = 6;
        frequencyBox.Minimum = 0.010000m;
        frequencyBox.Maximum = 30.000000m;
        frequencyBox.Increment = 0.001000m;
        frequencyBox.Width = 130;
        frequencyBox.Value = Math.Clamp((decimal)current.FrequencyMHz, frequencyBox.Minimum, frequencyBox.Maximum);
        ConfigureDbBox(waterfallMinimumBox, current.WaterfallMinimumDb);
        ConfigureDbBox(waterfallMaximumBox, current.WaterfallMaximumDb);

        connectBox.Text = "Connect automatically when KiwiDX starts";
        connectBox.AutoSize = true;
        connectBox.Checked = current.ConnectOnStartup;

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 250, ColumnCount = 2, RowCount = 6, Padding = new Padding(16, 18, 16, 8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        grid.Controls.Add(new Label { Text = "Startup server:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(serverBox, 1, 0);
        grid.Controls.Add(new Label { Text = "Frequency (MHz):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(frequencyBox, 1, 1);
        grid.Controls.Add(new Label { Text = "WF minimum (dB):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        grid.Controls.Add(waterfallMinimumBox, 1, 2);
        grid.Controls.Add(new Label { Text = "WF maximum (dB):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        grid.Controls.Add(waterfallMaximumBox, 1, 3);
        grid.Controls.Add(connectBox, 1, 4);
        grid.Controls.Add(new Label { Text = "Audio starts on connection. Use Mute beside Volume.", AutoSize = true }, 1, 5);

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Size = new Size(82, 29), Location = new Point(281, 273) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(82, 29), Location = new Point(371, 273) };
        save.Click += (_, _) =>
        {
            if (waterfallMinimumBox.Value >= waterfallMaximumBox.Value)
            {
                MessageBox.Show(this, "WF minimum must be lower than WF maximum.", "Startup Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }
            Configuration = new StartupConfiguration
            {
                ServerUrl = ((ServerChoice)serverBox.SelectedItem!).Url,
                FrequencyMHz = (double)frequencyBox.Value,
                ConnectOnStartup = connectBox.Checked,
                PlayOnStartup = true,
                WaterfallMinimumDb = (int)waterfallMinimumBox.Value,
                WaterfallMaximumDb = (int)waterfallMaximumBox.Value
            };
        };
        AcceptButton = save;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { grid, save, cancel });
    }

    private static void ConfigureDbBox(NumericUpDown box, int value)
    {
        box.Minimum = -200;
        box.Maximum = 0;
        box.Width = 90;
        box.Value = Math.Clamp(value, -200, 0);
    }
}
