using System.Globalization;
using System.Text.Json;

namespace KiwiDX;

public partial class Form1 : Form
{
    private readonly KiwiClient client = new();
    private readonly WaterfallControl waterfall = new();
    private readonly TextBox urlBox = new();
    private readonly TextBox frequencyBox = new();
    private readonly ComboBox modeBox = new();
    private readonly NumericUpDown bandwidthBox = new();
    private readonly Label statusLabel = new();
    private readonly Label cursorLabel = new();
    private readonly Button connectButton = new();
    private readonly Button playButton = new();
    private readonly Button recordButton = new();
    private readonly CheckBox recordWaterfallCheckBox = new();
    private readonly System.Windows.Forms.Timer waterfallRecordingTimer = new() { Interval = 100 };
    private readonly TrackBar volumeBar = new();
    private readonly TextBox consoleBox = new();
    private readonly NumericUpDown wfMinBox = new();
    private readonly NumericUpDown wfMaxBox = new();
    private readonly ComboBox bandBox = new();
    private readonly TrackBar zoomBar = new();
    private readonly Label zoomLabel = new();
    private readonly HScrollBar spectrumBar = new();
    private readonly Label spectrumRangeLabel = new();
    private readonly Label spectrumCenterLabel = new();
    private readonly CheckBox consoleLogCheckBox = new();
    private readonly CheckBox serverInfoCheckBox = new();
    private readonly ToolStripMenuItem favoritesMenu = new("Favorites");
    private readonly ToolStripMenuItem myBookmarksMenu = new("My Bookmarks");
    private readonly Button favoriteButton = new();
    private readonly ToolTip favoriteToolTip = new();
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly RichTextBox serverInfoBox = new();
    private readonly GroupBox consolePanel = new();
    private readonly GroupBox serverInfoPanel = new();
    private TableLayoutPanel? mainLayout;
    private bool updatingNavigation;
    private bool selectingDetectedBand;
    private double currentViewSpan = 30_000_000d / (1 << 11);
    private long wfUiLines;
    private bool audioPlaying;
    private double recordingFrequencyHz;
    private string recordingMode = "USB";
    private bool recordingWaterfall;
    private WaterfallVideoRecorder? waterfallVideoRecorder;
    private string currentServerTitle = "KiwiSDR";
    private string currentServerLocation = "Location not reported";
    private string currentServerUrl = "";
    private readonly string favoritesPath = Path.Combine(AppContext.BaseDirectory, "favorites.json");
    private readonly string startupPath = Path.Combine(AppContext.BaseDirectory, "startup.json");
    private readonly string bookmarksPath = Path.Combine(AppContext.BaseDirectory, "bookmarks.json");
    private StartupConfiguration startupConfiguration = new();
    private List<FrequencyBookmark> frequencyBookmarks = new();

    private sealed record BandPreset(string Name, double FrequencyMHz, string Mode, int Bandwidth, int Zoom)
    {
        public override string ToString() => Name;
    }

    private static readonly BandPreset[] Bands =
    {
        new("2200m (Amateur)", 0.136, "CW", 500, 14), new("630m (Amateur)", 0.475, "CW", 500, 14),
        new("160m (Amateur)", 1.900, "LSB", 2400, 7), new("120m (Broadcast)", 2.400, "AM", 6000, 8),
        new("90m (Broadcast)", 3.300, "AM", 6000, 8), new("80m (Amateur)", 3.750, "LSB", 2400, 6),
        new("75m (Broadcast)", 3.950, "AM", 6000, 8), new("60m (Broadcast)", 4.900, "AM", 6000, 7),
        new("60m (Amateur)", 5.357, "USB", 2400, 12), new("49m (Broadcast)", 6.100, "AM", 6000, 6),
        new("40m (Amateur)", 7.100, "LSB", 2400, 6), new("41m (Broadcast)", 7.300, "AM", 6000, 6),
        new("31m (Broadcast)", 9.650, "AM", 6000, 6), new("30m (Amateur)", 10.125, "CW", 500, 9),
        new("25m (Broadcast)", 11.850, "AM", 6000, 6), new("22m (Broadcast)", 13.700, "AM", 6000, 7),
        new("20m (Amateur)", 14.200, "USB", 2400, 6), new("19m (Broadcast)", 15.300, "AM", 6000, 6),
        new("17m (Amateur)", 18.118, "USB", 2400, 8), new("16m (Broadcast)", 17.700, "AM", 6000, 6),
        new("15m (Amateur)", 21.225, "USB", 2400, 6), new("15m (Broadcast)", 18.950, "AM", 6000, 7),
        new("13m (Broadcast)", 21.600, "AM", 6000, 7), new("12m (Amateur)", 24.940, "USB", 2400, 8),
        new("11m (Broadcast)", 25.800, "AM", 6000, 7), new("10m (Amateur)", 28.500, "USB", 2400, 5)
    };

    private static readonly (BandPreset Band, double LowHz, double HighHz)[] BandRanges =
    {
        (Bands[0], 135_700, 137_800), (Bands[1], 472_000, 479_000),
        (Bands[2], 1_800_000, 2_000_000), (Bands[3], 2_300_000, 2_495_000),
        (Bands[4], 3_200_000, 3_400_000), (Bands[5], 3_500_000, 4_000_000),
        (Bands[6], 3_900_000, 4_000_000), (Bands[7], 4_750_000, 5_060_000),
        (Bands[8], 5_330_500, 5_406_400), (Bands[9], 5_900_000, 6_200_000),
        (Bands[10], 7_000_000, 7_300_000), (Bands[11], 7_200_000, 7_600_000),
        (Bands[12], 9_400_000, 9_900_000), (Bands[13], 10_100_000, 10_150_000),
        (Bands[14], 11_600_000, 12_100_000), (Bands[15], 13_570_000, 13_870_000),
        (Bands[16], 14_000_000, 14_350_000), (Bands[17], 15_100_000, 15_800_000),
        (Bands[18], 18_068_000, 18_168_000), (Bands[19], 17_480_000, 17_900_000),
        (Bands[20], 21_000_000, 21_450_000), (Bands[21], 18_900_000, 19_020_000),
        (Bands[22], 21_450_000, 21_850_000), (Bands[23], 24_890_000, 24_990_000),
        (Bands[24], 25_600_000, 26_100_000), (Bands[25], 28_000_000, 29_700_000)
    };

    public Form1()
    {
        Text = "KiwiDX v0.1.46 - DIAL ZOOM";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(850, 560);
        BackColor = Color.FromArgb(22, 27, 32);
        ForeColor = Color.Gainsboro;

        var menuStrip = BuildMenuStrip();
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8), WrapContents = true, BackColor = Color.FromArgb(35, 42, 48) };
        top.Controls.Add(new Label { Text = "KiwiDX v0.1.46", AutoSize = true, ForeColor = Color.Gold, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 7, 8, 0) });
        startupConfiguration = LoadStartupConfiguration();
        urlBox.Width = 260;
        urlBox.Text = startupConfiguration.ServerUrl;
        urlBox.PlaceholderText = "KiwiSDR URL";
        urlBox.TextChanged += (_, _) => { UpdateFavoriteButton(); RefreshFrequencyBookmarks(); };
        connectButton.Text = "Connect";
        connectButton.AutoSize = true;
        connectButton.Click += async (_, _) => await ToggleConnection();
        var mapButton = new Button { Text = "🗺 Map", AutoSize = true, BackColor = Color.FromArgb(42, 91, 120), ForeColor = Color.White, UseVisualStyleBackColor = false };
        mapButton.Click += (_, _) => OpenReceiverMap();
        favoriteButton.Text = "☆"; favoriteButton.AutoSize = false; favoriteButton.Size = new Size(26, urlBox.PreferredHeight); favoriteButton.Margin = urlBox.Margin; favoriteButton.Padding = Padding.Empty; favoriteButton.BackColor = top.BackColor; favoriteButton.ForeColor = Color.Gold; favoriteButton.FlatStyle = FlatStyle.Flat; favoriteButton.Font = new Font("Segoe UI Symbol", 10, FontStyle.Regular); favoriteButton.TextAlign = ContentAlignment.MiddleCenter; favoriteButton.UseVisualStyleBackColor = false; favoriteButton.TabStop = false;
        favoriteButton.FlatAppearance.BorderSize = 0;
        favoriteButton.FlatAppearance.MouseOverBackColor = top.BackColor;
        favoriteButton.FlatAppearance.MouseDownBackColor = top.BackColor;
        favoriteButton.Click += (_, _) => ToggleCurrentServerFavorite();
        favoriteToolTip.SetToolTip(favoriteButton, "Add this server to Favorites");
        frequencyBox.Width = 100;
        frequencyBox.Text = startupConfiguration.FrequencyMHz.ToString("0.000000", CultureInfo.InvariantCulture);
        frequencyBox.PlaceholderText = "MHz";
        frequencyBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) SendFrequency(); };
        modeBox.Width = 72;
        modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        modeBox.Items.AddRange(new object[] { "AM", "USB", "LSB", "CW", "NFM", "IQ" });
        modeBox.SelectedIndex = 1;
        modeBox.SelectedIndexChanged += (_, _) => SendMode();
        bandwidthBox.Width = 82;
        bandwidthBox.Minimum = 50;
        bandwidthBox.Maximum = 12000;
        bandwidthBox.Increment = 50;
        bandwidthBox.Value = 2400;
        bandwidthBox.ValueChanged += (_, _) => SendBandwidth();
        playButton.Text = "▶ Play";
        playButton.AutoSize = true;
        playButton.BackColor = Color.FromArgb(35, 125, 62);
        playButton.ForeColor = Color.White;
        playButton.FlatStyle = FlatStyle.Flat;
        playButton.UseVisualStyleBackColor = false;
        playButton.Click += (_, _) => ToggleAudio();
        recordButton.Text = "● Record";
        recordButton.AutoSize = true;
        recordButton.BackColor = Color.FromArgb(65, 70, 75);
        recordButton.ForeColor = Color.White;
        recordButton.FlatStyle = FlatStyle.Flat;
        recordButton.UseVisualStyleBackColor = false;
        recordButton.Click += async (_, _) => await ToggleRecording();
        recordWaterfallCheckBox.Text = "Record Waterfall";
        recordWaterfallCheckBox.AutoSize = true;
        recordWaterfallCheckBox.Padding = new Padding(0, 5, 0, 0);
        waterfallRecordingTimer.Tick += (_, _) => CaptureWaterfallVideoFrame();
        volumeBar.Minimum = 0;
        volumeBar.Maximum = 100;
        volumeBar.Value = 75;
        volumeBar.Width = 110;
        volumeBar.TickFrequency = 25;
        volumeBar.ValueChanged += (_, _) => client.SetVolume(volumeBar.Value / 100f);
        var centerButton = new Button { Text = "Center", AutoSize = true };
        centerButton.Click += (_, _) => waterfall.CenterOnTune();
        bandBox.Width = 175;
        bandBox.DropDownStyle = ComboBoxStyle.DropDownList;
        bandBox.Items.AddRange(Bands.Cast<object>().ToArray());
        bandBox.SelectedIndexChanged += (_, _) => SelectBand();
        var startupWfMin = Math.Clamp(startupConfiguration.WaterfallMinimumDb, -200, -1);
        var startupWfMax = Math.Clamp(startupConfiguration.WaterfallMaximumDb, -199, 0);
        if (startupWfMin >= startupWfMax) { startupWfMin = -120; startupWfMax = -50; }
        ConfigureDbControl(wfMinBox, startupWfMin);
        ConfigureDbControl(wfMaxBox, startupWfMax);
        wfMinBox.ValueChanged += (_, _) => ApplyWaterfallRange();
        wfMaxBox.ValueChanged += (_, _) => ApplyWaterfallRange();
        consoleLogCheckBox.Text = "Console Log"; consoleLogCheckBox.AutoSize = true; consoleLogCheckBox.Checked = false; consoleLogCheckBox.Padding = new Padding(0, 5, 0, 0);
        serverInfoCheckBox.Text = "Server Info"; serverInfoCheckBox.AutoSize = true; serverInfoCheckBox.Checked = true; serverInfoCheckBox.Padding = new Padding(0, 5, 0, 0);
        consoleLogCheckBox.CheckedChanged += (_, _) => UpdateOptionalPanels();
        serverInfoCheckBox.CheckedChanged += (_, _) => UpdateOptionalPanels();
        var serverLabel = new Label { Text = "Server:", AutoSize = true, ForeColor = Color.LimeGreen, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 7, 0, 0) };
        var bandwidthGroup = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty };
        bandwidthGroup.Controls.Add(new Label { Text = "BW Hz", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        bandwidthGroup.Controls.Add(bandwidthBox);
        var recordingGroup = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty };
        recordingGroup.Controls.Add(new Label { Text = "Recording", AutoSize = true, ForeColor = Color.Gold, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 7, 2, 0) });
        recordingGroup.Controls.Add(recordButton);
        recordingGroup.Controls.Add(recordWaterfallCheckBox);
        top.Controls.AddRange(new Control[] { serverLabel, urlBox, connectButton, mapButton, new Label { Text = "Band", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, bandBox, new Label { Text = "MHz", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, frequencyBox, modeBox, bandwidthGroup, playButton, recordingGroup, new Label { Text = "Vol", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, volumeBar, centerButton, new Label { Text = "WF min", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, wfMinBox, new Label { Text = "WF max", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, wfMaxBox, consoleLogCheckBox, serverInfoCheckBox });
        top.Controls.Add(favoriteButton);
        top.Controls.SetChildIndex(favoriteButton, 3);

        var navigation = BuildNavigationBar();
        var waterfallHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black, Padding = Padding.Empty };
        var dialPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 38, WrapContents = false, AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6, 4, 0, 3),
            BackColor = Color.FromArgb(210, 18, 24, 30)
        };
        dialPanel.Controls.Add(new Label { Text = "DIAL", AutoSize = true, ForeColor = Color.Gold, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 6, 5, 0) });
        dialPanel.Controls.AddRange(new Control[]
        {
            CreateDialButton("---", -100_000), CreateDialButton("--", -10_000), CreateDialButton("-", -1_000),
            CreateDialButton("+", 1_000), CreateDialButton("++", 10_000), CreateDialButton("+++", 100_000)
        });
        waterfallHost.Controls.Add(waterfall);
        waterfallHost.Controls.Add(dialPanel);

        waterfall.Dock = DockStyle.Fill;
        waterfall.CursorChangedByUser += (_, frequency) => { frequencyBox.Text = (frequency / 1_000_000d).ToString("0.000000", CultureInfo.InvariantCulture); client.SetFrequency(frequency); };
        waterfall.CursorMoved += (_, frequency) => cursorLabel.Text = $"Cursor: {frequency / 1_000_000d:0.000000} MHz";
        waterfall.BandwidthChanged += (_, bandwidth) => { bandwidthBox.Value = Math.Clamp(bandwidth, (int)bandwidthBox.Minimum, (int)bandwidthBox.Maximum); };
        waterfall.ViewChanged += (_, view) =>
        {
            SyncNavigation(view.center, view.span);
            client.SetWaterfallView(SpanToZoom(view.span), view.center);
        };
        waterfall.AddBookmarkRequested += (_, frequency) => AddFrequencyBookmark(frequency);
        statusLabel.AutoSize = true;
        statusLabel.Padding = new Padding(8, 5, 0, 0);
        cursorLabel.AutoSize = true;
        cursorLabel.Padding = new Padding(8, 5, 0, 0);
        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(35, 42, 48) };
        bottom.Controls.Add(statusLabel);
        bottom.Controls.Add(cursorLabel);
        cursorLabel.Left = 230;
        consoleBox.Dock = DockStyle.Fill;
        consoleBox.Multiline = true;
        consoleBox.ReadOnly = true;
        consoleBox.ScrollBars = ScrollBars.Vertical;
        consoleBox.BackColor = Color.FromArgb(10, 13, 15);
        consoleBox.ForeColor = Color.LightGreen;
        consoleBox.Font = new Font(Font.FontFamily, 8.5f);
        consolePanel.Text = "CONSOLE LOG";
        consolePanel.Dock = DockStyle.Fill;
        consolePanel.ForeColor = Color.LightGreen;
        consolePanel.BackColor = Color.FromArgb(10, 13, 15);
        consolePanel.Padding = new Padding(6, 4, 6, 6);
        consolePanel.Controls.Add(consoleBox);
        serverInfoBox.Dock = DockStyle.Fill;
        serverInfoBox.Multiline = true;
        serverInfoBox.ReadOnly = true;
        serverInfoBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        serverInfoBox.BorderStyle = BorderStyle.None;
        serverInfoBox.BackColor = Color.FromArgb(24, 31, 37);
        serverInfoBox.ForeColor = Color.Gainsboro;
        serverInfoBox.Font = new Font(Font.FontFamily, 8.5f);
        serverInfoBox.Text = "Connect to a KiwiSDR to view server information.";
        serverInfoPanel.Text = "SERVER INFO";
        serverInfoPanel.Dock = DockStyle.Fill;
        serverInfoPanel.ForeColor = Color.Gold;
        serverInfoPanel.BackColor = Color.FromArgb(24, 31, 37);
        serverInfoPanel.Padding = new Padding(6, 4, 6, 6);
        serverInfoPanel.Controls.Add(serverInfoBox);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty, Padding = Padding.Empty };
        mainLayout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(waterfallHost, 0, 1);
        layout.Controls.Add(navigation, 0, 2);
        layout.Controls.Add(serverInfoPanel, 0, 3);
        layout.Controls.Add(consolePanel, 0, 4);
        layout.Controls.Add(bottom, 0, 5);
        Controls.Add(layout);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        client.WaterfallLine += (_, line) =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                waterfall.AddLine(line);
                wfUiLines++;
                if (wfUiLines == 1 || wfUiLines % 10 == 0) statusLabel.Text = $"Connected | WF: {wfUiLines} lines ({line.Length} bins)";
            });
        };
        client.StatusChanged += (_, status) =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                statusLabel.Text = status;
                if (status == "Disconnected" || status.StartsWith("Connection lost", StringComparison.OrdinalIgnoreCase))
                {
                    connectButton.Text = "Connect";
                    ResetAudioButton();
                    ResetRecordingButton();
                    _ = CancelWaterfallRecordingAsync();
                }
            });
        };
        client.Error += (_, error) => { if (!IsDisposed) BeginInvoke(() => MessageBox.Show(this, error, "KiwiSDR", MessageBoxButtons.OK, MessageBoxIcon.Warning)); };
        client.Log += (_, message) => { if (!IsDisposed) BeginInvoke(() => AddLog(message)); };
        client.ServerInfoChanged += (_, info) => { if (!IsDisposed) BeginInvoke(() => SetServerInfo(info)); };
        FormClosing += async (_, _) => { await CancelWaterfallRecordingAsync(); await client.DisposeAsync(); };
        Resize += (_, _) => urlBox.Width = Math.Clamp(ClientSize.Width / 4, 150, 300);
        SyncNavigation(7_100_000, currentViewSpan);
        UpdateOptionalPanels();
        ApplyWaterfallRange();
        RefreshFavoritesMenu();
        frequencyBookmarks = LoadFrequencyBookmarks();
        RefreshFrequencyBookmarks();
        Shown += async (_, _) => await ApplyStartupConfiguration();
    }

    private void UpdateOptionalPanels()
    {
        if (mainLayout is null) return;
        serverInfoPanel.Visible = serverInfoCheckBox.Checked;
        consolePanel.Visible = consoleLogCheckBox.Checked;
        mainLayout.RowStyles[3].Height = serverInfoCheckBox.Checked ? 112 : 0;
        mainLayout.RowStyles[4].Height = consoleLogCheckBox.Checked ? 150 : 0;
        if (consoleLogCheckBox.Checked)
        {
            mainLayout.SetCellPosition(consolePanel, new TableLayoutPanelCellPosition(0, 4));
            consolePanel.BringToFront();
        }
        mainLayout.PerformLayout();
    }

    private void SetServerInfo(string info)
    {
        currentServerTitle = ReadServerInfoField(info, "Station / operator message") ?? "KiwiSDR";
        currentServerLocation = ReadServerInfoField(info, "Location") ?? "Location not reported";
        currentServerUrl = (ReadServerInfoField(info, "URL") ?? "").TrimEnd('/');
        UpdateFavoriteButton();
        serverInfoBox.Clear();
        foreach (var line in info.Replace("\r", "").Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator >= 0)
            {
                serverInfoBox.SelectionColor = Color.LimeGreen;
                serverInfoBox.SelectionFont = new Font(serverInfoBox.Font, FontStyle.Bold);
                serverInfoBox.AppendText(line[..(separator + 1)]);
                serverInfoBox.SelectionColor = Color.Gainsboro;
                serverInfoBox.SelectionFont = new Font(serverInfoBox.Font, FontStyle.Regular);
                serverInfoBox.AppendText(line[(separator + 1)..]);
            }
            else serverInfoBox.AppendText(line);
            serverInfoBox.AppendText(Environment.NewLine);
        }
        serverInfoBox.SelectionStart = 0;
        serverInfoBox.SelectionLength = 0;
    }

    private static string? ReadServerInfoField(string info, string field)
    {
        var prefix = field + ":";
        var line = info.Replace("\r", "").Split('\n').FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        var value = line?[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(value) || value.Equals("Not reported", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private Control BuildNavigationBar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 2, BackColor = Color.FromArgb(30, 36, 42), Padding = new Padding(8, 3, 8, 3) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "Zoom", AutoSize = true, ForeColor = Color.White, Anchor = AnchorStyles.Left }, 0, 0);
        zoomBar.Minimum = 0; zoomBar.Maximum = 14; zoomBar.Value = 11; zoomBar.TickFrequency = 1; zoomBar.Dock = DockStyle.Fill;
        zoomBar.ValueChanged += (_, _) => { zoomLabel.Text = $"Z{zoomBar.Value}"; if (!updatingNavigation) waterfall.SetZoomLevel(zoomBar.Value); };
        panel.Controls.Add(zoomBar, 1, 0);
        zoomLabel.Text = "Z11"; zoomLabel.AutoSize = true; zoomLabel.ForeColor = Color.Gold; zoomLabel.Font = new Font(Font, FontStyle.Bold); zoomLabel.Anchor = AnchorStyles.Left;
        panel.Controls.Add(zoomLabel, 2, 0);
        var zoomButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty, Padding = Padding.Empty };
        var zoomOutButton = new Button { Text = "Zoom −", AutoSize = false, Size = new Size(72, 25), Margin = new Padding(0, 1, 3, 0) };
        var zoomInButton = new Button { Text = "Zoom +", AutoSize = false, Size = new Size(72, 25), Margin = new Padding(0, 1, 0, 0) };
        zoomOutButton.Click += (_, _) => waterfall.ZoomOut();
        zoomInButton.Click += (_, _) => waterfall.ZoomIn();
        zoomButtons.Controls.AddRange(new Control[] { zoomOutButton, zoomInButton });
        panel.Controls.Add(zoomButtons, 1, 1);
        panel.SetColumnSpan(zoomButtons, 2);
        var leftButton = CreateSpectrumButton("◀", -1);
        var rightButton = CreateSpectrumButton("▶", 1);
        panel.Controls.Add(leftButton, 3, 1);
        spectrumBar.Minimum = 0; spectrumBar.Maximum = 29_999_999; spectrumBar.LargeChange = (int)currentViewSpan; spectrumBar.SmallChange = Math.Max(1, (int)currentViewSpan / 20); spectrumBar.Dock = DockStyle.Fill;
        spectrumBar.Scroll += (_, _) => { if (!updatingNavigation) waterfall.PanTo(spectrumBar.Value + currentViewSpan / 2); };
        panel.Controls.Add(spectrumBar, 4, 1);
        panel.Controls.Add(rightButton, 5, 1);
        spectrumRangeLabel.Text = "0.000000 — 30.000000 MHz"; spectrumRangeLabel.AutoSize = true; spectrumRangeLabel.ForeColor = Color.Silver; spectrumRangeLabel.Anchor = AnchorStyles.Right;
        panel.Controls.Add(spectrumRangeLabel, 6, 0);
        spectrumCenterLabel.Text = "Center 7.100000 MHz"; spectrumCenterLabel.AutoSize = true; spectrumCenterLabel.ForeColor = Color.Cyan; spectrumCenterLabel.Font = new Font(Font, FontStyle.Bold); spectrumCenterLabel.Anchor = AnchorStyles.None;
        panel.Controls.Add(spectrumCenterLabel, 4, 0);
        panel.SetColumnSpan(spectrumCenterLabel, 2);
        var spectrumTitle = new Label { Text = "EXPLORE SPECTRUM", AutoSize = true, ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold), Anchor = AnchorStyles.Left };
        panel.Controls.Add(spectrumTitle, 3, 0);
        return panel;
    }

    private Button CreateSpectrumButton(string text, int direction)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(2, 0, 2, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(48, 65, 76), ForeColor = Color.White, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderColor = Color.SlateGray;
        button.Click += (_, _) => waterfall.PanTo(spectrumBar.Value + currentViewSpan / 2 + direction * currentViewSpan / 5);
        return button;
    }

    private void SyncNavigation(double center, double span)
    {
        updatingNavigation = true;
        try
        {
            currentViewSpan = span;
            var zoom = SpanToZoom(span);
            zoomBar.Value = zoom;
            zoomLabel.Text = $"Z{zoom}";
            spectrumBar.LargeChange = Math.Clamp((int)Math.Round(span), 1, 30_000_000);
            spectrumBar.SmallChange = Math.Max(1, spectrumBar.LargeChange / 20);
            var maximumValue = Math.Max(0, spectrumBar.Maximum - spectrumBar.LargeChange + 1);
            spectrumBar.Value = Math.Clamp((int)Math.Round(center - span / 2), 0, maximumValue);
            spectrumCenterLabel.Text = $"Center {center / 1_000_000:0.000000} MHz";
            spectrumRangeLabel.Text = $"{Math.Max(0, center - span / 2) / 1_000_000:0.000000} — {Math.Min(30_000_000, center + span / 2) / 1_000_000:0.000000} MHz";
        }
        finally { updatingNavigation = false; }
    }

    private static void ConfigureDbControl(NumericUpDown control, int value)
    {
        control.Width = 58;
        control.Minimum = -200;
        control.Maximum = 0;
        control.Value = value;
    }

    private void ApplyWaterfallRange()
    {
        var minimum = (int)wfMinBox.Value;
        var maximum = (int)wfMaxBox.Value;
        if (minimum >= maximum) return;
        waterfall.SetDbRange(minimum, maximum);
        client.SetWaterfallRange(minimum, maximum);
    }

    private static int SpanToZoom(double span) => Math.Clamp((int)Math.Round(Math.Log(30_000_000d / span, 2)), 0, 14);

    private Button CreateDialButton(string text, double deltaHz)
    {
        var button = new Button
        {
            Text = text, AutoSize = false, Size = new Size(42, 27), Margin = new Padding(2, 0, 2, 0), Tag = deltaHz,
            BackColor = deltaHz < 0 ? Color.FromArgb(170, 48, 62) : Color.FromArgb(28, 125, 145),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(Font, FontStyle.Bold), UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = deltaHz < 0 ? Color.OrangeRed : Color.Cyan;
        button.Click += (_, _) => MoveDial(deltaHz);
        new ToolTip().SetToolTip(button, $"Move dial {(deltaHz > 0 ? "+" : "")}{deltaHz / 1_000:0} kHz");
        return button;
    }

    private void MoveDial(double deltaHz)
    {
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) return;
        var frequency = Math.Clamp(mhz * 1_000_000 + deltaHz, 10_000, 30_000_000);
        frequencyBox.Text = (frequency / 1_000_000).ToString("0.000000", CultureInfo.InvariantCulture);
        waterfall.SetTunedFrequency(frequency);
        client.SetFrequency(frequency);
    }

    private void SelectBand()
    {
        if (selectingDetectedBand) return;
        if (bandBox.SelectedItem is not BandPreset band) return;
        frequencyBox.Text = band.FrequencyMHz.ToString("0.000000", CultureInfo.InvariantCulture);
        modeBox.SelectedItem = band.Mode;
        bandwidthBox.Value = band.Bandwidth;
        var frequency = band.FrequencyMHz * 1_000_000;
        var span = 30_000_000d / (1 << band.Zoom);
        waterfall.SetRadioState(frequency, span, frequency, band.Bandwidth);
        client.SetFrequency(frequency);
        client.SetMode(band.Mode);
        client.SetBandwidth(band.Bandwidth);
        client.SetWaterfallView(band.Zoom, frequency);
    }

    private void AddLog(string message)
    {
        consoleBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        if (consoleBox.TextLength > 20000) consoleBox.Text = consoleBox.Text[^16000..];
        consoleBox.SelectionStart = consoleBox.TextLength;
        consoleBox.ScrollToCaret();
    }

    private async Task ToggleConnection()
    {
        await connectionGate.WaitAsync();
        try { await ToggleConnectionCore(); }
        finally { connectionGate.Release(); }
    }

    private async Task ToggleConnectionCore()
    {
        if (client.IsConnected) { await CancelWaterfallRecordingAsync(); await client.DisconnectAsync(); connectButton.Text = "Connect"; ResetAudioButton(); return; }
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) { MessageBox.Show("Invalid frequency."); return; }
        ResetAudioButton();
        connectButton.Enabled = false;
        try
        {
            var frequency = mhz * 1_000_000;
            await client.ConnectAsync(urlBox.Text, frequency, modeBox.Text.ToLowerInvariant(), (int)bandwidthBox.Value);
            PreselectBandForFrequency(frequency);
            waterfall.SetRadioState(frequency, 30_000_000d / (1 << 11), frequency, (int)bandwidthBox.Value);
            ApplyWaterfallRange();
            connectButton.Text = "Disconnect";
        }
        catch (Exception ex)
        {
            await client.DisconnectAsync();
            connectButton.Text = "Connect";
            ResetAudioButton();
            MessageBox.Show(this, GetFriendlyConnectionError(ex), "Receiver unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { connectButton.Enabled = true; }
    }

    private void PreselectBandForFrequency(double frequencyHz)
    {
        var detected = BandRanges
            .Where(item => frequencyHz >= item.LowHz && frequencyHz <= item.HighHz)
            .OrderBy(item => Math.Abs(item.Band.FrequencyMHz * 1_000_000 - frequencyHz))
            .Select(item => item.Band)
            .FirstOrDefault();

        selectingDetectedBand = true;
        try { bandBox.SelectedItem = detected; }
        finally { selectingDetectedBand = false; }
    }

    private static string GetFriendlyConnectionError(Exception exception)
    {
        if (exception is TimeoutException) return exception.Message;
        if (exception is InvalidOperationException && exception.Message.StartsWith("Receiver", StringComparison.OrdinalIgnoreCase)) return exception.Message;
        if (exception is System.Net.WebSockets.WebSocketException or HttpRequestException or System.Net.Sockets.SocketException)
            return "Receiver temporarily unavailable. The server is offline, unreachable, or its port is closed.";
        return $"The receiver could not be connected.\n\n{exception.Message}";
    }

    private void ToggleAudio()
    {
        if (!client.IsConnected)
        {
            statusLabel.Text = "Connect to a KiwiSDR before playing audio.";
            return;
        }

        audioPlaying = !audioPlaying;
        if (audioPlaying)
        {
            client.PlayAudio();
            playButton.Text = "■ Stop";
            playButton.BackColor = Color.FromArgb(170, 45, 45);
        }
        else
        {
            client.StopAudio();
            ResetAudioButton();
        }
    }

    private void ResetAudioButton()
    {
        audioPlaying = false;
        playButton.Text = "▶ Play";
        playButton.BackColor = Color.FromArgb(35, 125, 62);
    }

    private async Task ToggleRecording()
    {
        if (!client.IsRecording && !recordingWaterfall)
        {
            if (!client.IsConnected)
            {
                statusLabel.Text = "Connect to a KiwiSDR before recording.";
                return;
            }
            if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) return;
            recordingFrequencyHz = mhz * 1_000_000d;
            recordingMode = modeBox.Text.ToUpperInvariant();
            if (recordWaterfallCheckBox.Checked)
            {
                var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                if (!File.Exists(ffmpegPath))
                {
                    MessageBox.Show(this, "ffmpeg.exe is missing from the KiwiDX folder. It is required for waterfall video recording.", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                try { waterfallVideoRecorder = WaterfallVideoRecorder.Start(ffmpegPath); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"The waterfall recorder could not start.\n\n{ex.Message}", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                client.StartRecording();
                recordingWaterfall = true;
                CaptureWaterfallVideoFrame();
                waterfallRecordingTimer.Start();
                statusLabel.Text = "Recording waterfall video at 480p...";
            }
            else client.StartRecording();
            recordWaterfallCheckBox.Enabled = false;
            recordButton.Text = "■ Stop Recording";
            recordButton.BackColor = Color.FromArgb(180, 38, 38);
            return;
        }

        string temporaryRecording;
        var video = recordingWaterfall;
        try
        {
            if (video)
            {
                waterfallRecordingTimer.Stop();
                var recorder = waterfallVideoRecorder ?? throw new InvalidOperationException("The waterfall video recorder is unavailable.");
                var videoOnlyPath = await recorder.StopAsync();
                await recorder.DisposeAsync();
                waterfallVideoRecorder = null;
                recordingWaterfall = false;
                var audioPath = client.StopRecording();
                var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                try { temporaryRecording = await WaterfallVideoRecorder.AddAudioAsync(ffmpegPath, videoOnlyPath, audioPath); }
                finally
                {
                    try { File.Delete(videoOnlyPath); } catch { }
                    try { File.Delete(audioPath); } catch { }
                }
            }
            else temporaryRecording = client.StopRecording();
        }
        catch (Exception ex)
        {
            await CancelWaterfallRecordingAsync();
            ResetRecordingButton();
            MessageBox.Show(this, $"The recording could not be finalized.\n\n{ex.Message}", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var frequencyName = (recordingFrequencyHz / 1_000d).ToString("0.000", CultureInfo.InvariantCulture);
        var extension = video ? "mp4" : "mp3";
        var suggestedName = $"Record_{frequencyName}_{recordingMode}_{DateTime.Now:MMddyy_HH-mm-ss}.{extension}";
        using var dialog = new SaveFileDialog
        {
            Title = "Save KiwiDX Recording",
            Filter = video ? "MP4 video (*.mp4)|*.mp4" : "MP3 audio (*.mp3)|*.mp3",
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            recordButton.Enabled = false;
            if (video)
            {
                statusLabel.Text = "Saving 480p MP4 video...";
                File.Move(temporaryRecording, dialog.FileName, true);
            }
            else
            {
                statusLabel.Text = "Encoding MP3 at 128 kbps...";
                await KiwiClient.EncodeRecordingToMp3Async(temporaryRecording, dialog.FileName);
            }
            statusLabel.Text = $"Recording saved: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The recording could not be saved.\n\n{ex.Message}", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            recordButton.Enabled = true;
            ResetRecordingButton();
            try { File.Delete(temporaryRecording); } catch { }
        }
    }

    private void CaptureWaterfallVideoFrame()
    {
        if (!recordingWaterfall || waterfallVideoRecorder is null) return;
        try { waterfallVideoRecorder.Capture(waterfall); }
        catch (Exception ex)
        {
            waterfallRecordingTimer.Stop();
            _ = CancelWaterfallRecordingAsync();
            MessageBox.Show(this, $"Waterfall video recording stopped.\n\n{ex.Message}", "Recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CancelWaterfallRecordingAsync()
    {
        if (!recordingWaterfall || waterfallVideoRecorder is null) return;
        waterfallRecordingTimer.Stop();
        var recorder = waterfallVideoRecorder;
        waterfallVideoRecorder = null;
        recordingWaterfall = false;
        try
        {
            string? audioPath = null;
            if (client.IsRecording)
            {
                try { audioPath = client.StopRecording(); } catch { }
            }
            var path = await recorder.StopAsync();
            await recorder.DisposeAsync();
            try { File.Delete(path); } catch { }
            if (audioPath is not null) try { File.Delete(audioPath); } catch { }
        }
        catch { await recorder.DisposeAsync(); }
        ResetRecordingButton();
    }

    private void ResetRecordingButton()
    {
        recordButton.Text = "● Record";
        recordButton.BackColor = Color.FromArgb(65, 70, 75);
        recordWaterfallCheckBox.Enabled = true;
    }

    private void SendFrequency()
    {
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) return;
        var frequency = mhz * 1_000_000;
        client.SetFrequency(frequency);
        waterfall.SetTunedFrequency(frequency);
    }

    private void OpenReceiverMap()
    {
        var map = new ReceiverMapForm();
        map.ServerSelected += async (_, serverUrl) =>
        {
            try { await SwitchReceiverAsync(serverUrl); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Map connection", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        map.Show(this);
    }

    private MenuStrip BuildMenuStrip()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(45, 52, 58), ForeColor = Color.White };
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var bookmarks = new ToolStripMenuItem("Bookmarks");
        bookmarks.DropDownItems.Add(favoritesMenu);
        bookmarks.DropDownItems.Add(myBookmarksMenu);
        var settings = new ToolStripMenuItem("Settings");
        settings.DropDownItems.Add("Startup", null, (_, _) => OpenStartupSettings());
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About", null, (_, _) => new AboutForm().ShowDialog(this));
        menu.Items.AddRange(new ToolStripItem[] { file, bookmarks, settings, help });
        return menu;
    }

    private StartupConfiguration LoadStartupConfiguration()
    {
        try
        {
            if (!File.Exists(startupPath))
            {
                var defaults = new StartupConfiguration();
                File.WriteAllText(startupPath, JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
                return defaults;
            }

            return JsonSerializer.Deserialize<StartupConfiguration>(File.ReadAllText(startupPath)) ?? new StartupConfiguration();
        }
        catch (Exception ex)
        {
            AddLog($"Could not read startup.json: {ex.Message}");
            return new StartupConfiguration();
        }
    }

    private void OpenStartupSettings()
    {
        using var dialog = new StartupSettingsForm(LoadFavorites(), startupConfiguration);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        startupConfiguration = dialog.Configuration;
        try
        {
            File.WriteAllText(startupPath, JsonSerializer.Serialize(startupConfiguration, new JsonSerializerOptions { WriteIndented = true }));
            statusLabel.Text = "Startup settings saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save startup.json.\n\n{ex.Message}", "Startup Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ApplyStartupConfiguration()
    {
        if (!startupConfiguration.ConnectOnStartup) return;
        await ToggleConnection();
        if (startupConfiguration.PlayOnStartup && client.IsConnected) ToggleAudio();
    }

    private List<FavoriteServer> LoadFavorites()
    {
        try
        {
            if (!File.Exists(favoritesPath)) File.WriteAllText(favoritesPath, "[]");
            return JsonSerializer.Deserialize<List<FavoriteServer>>(File.ReadAllText(favoritesPath)) ?? new();
        }
        catch (Exception ex)
        {
            AddLog($"Could not read favorites.json: {ex.Message}");
            return new();
        }
    }

    private List<FrequencyBookmark> LoadFrequencyBookmarks()
    {
        try
        {
            if (!File.Exists(bookmarksPath)) File.WriteAllText(bookmarksPath, "[]");
            var loaded = JsonSerializer.Deserialize<List<FrequencyBookmark>>(File.ReadAllText(bookmarksPath)) ?? new();
            var currentUrl = NormalizeServerUrl(urlBox.Text);
            var migrated = false;
            foreach (var bookmark in loaded.Where(item => item.ServerUrls is null || item.ServerUrls.Count == 0))
            {
                bookmark.ServerUrls = currentUrl.Length == 0 ? new List<string>() : new List<string> { currentUrl };
                migrated = true;
            }
            if (migrated) File.WriteAllText(bookmarksPath, JsonSerializer.Serialize(loaded, new JsonSerializerOptions { WriteIndented = true }));
            return loaded;
        }
        catch (Exception ex)
        {
            AddLog($"Could not read bookmarks.json: {ex.Message}");
            return new();
        }
    }

    private bool SaveFrequencyBookmarks()
    {
        try
        {
            File.WriteAllText(bookmarksPath, JsonSerializer.Serialize(frequencyBookmarks, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save bookmarks.json.\n\n{ex.Message}", "Bookmarks", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void AddFrequencyBookmark(double frequencyHz)
    {
        var currentUrl = NormalizeServerUrl(urlBox.Text);
        if (currentUrl.Length == 0)
        {
            MessageBox.Show(this, "Enter or connect to a valid server before adding a bookmark.", "Bookmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var editor = new BookmarkEditorForm(frequencyHz, LoadFavorites(), currentUrl);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        frequencyBookmarks.Add(editor.Bookmark);
        if (SaveFrequencyBookmarks())
        {
            RefreshFrequencyBookmarks();
            statusLabel.Text = $"Bookmark added: {editor.Bookmark.Name} at {frequencyHz / 1_000:0.000} kHz";
        }
    }

    private void RefreshFrequencyBookmarks()
    {
        var currentUrl = NormalizeServerUrl(urlBox.Text);
        var currentBookmarks = frequencyBookmarks
            .Where(bookmark => bookmark.ServerUrls is not null && bookmark.ServerUrls.Any(url => NormalizeServerUrl(url).Equals(currentUrl, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        waterfall.SetBookmarks(currentBookmarks);
        myBookmarksMenu.DropDownItems.Clear();
        if (currentBookmarks.Count == 0)
            myBookmarksMenu.DropDownItems.Add(new ToolStripMenuItem("No bookmarks for this server") { Enabled = false });
        else
        {
            foreach (var bookmark in currentBookmarks.OrderBy(item => item.FrequencyHz))
            {
                var label = $"{bookmark.Name} — {bookmark.FrequencyHz / 1_000:0.000} kHz{(bookmark.Visible ? "" : " (hidden)")}";
                var item = new ToolStripMenuItem(label) { Tag = bookmark, ToolTipText = bookmark.Description };
                item.Click += (_, _) => TuneToBookmark((FrequencyBookmark)item.Tag!);
                myBookmarksMenu.DropDownItems.Add(item);
            }
        }
        myBookmarksMenu.DropDownItems.Add(new ToolStripSeparator());
        myBookmarksMenu.DropDownItems.Add("Manage Bookmarks...", null, (_, _) => ManageFrequencyBookmarks());
    }

    private void TuneToBookmark(FrequencyBookmark bookmark)
    {
        frequencyBox.Text = (bookmark.FrequencyHz / 1_000_000d).ToString("0.000000", CultureInfo.InvariantCulture);
        waterfall.SetTunedFrequency(bookmark.FrequencyHz);
        client.SetFrequency(bookmark.FrequencyHz);
        statusLabel.Text = $"Tuned to {bookmark.Name}: {bookmark.FrequencyHz / 1_000:0.000} kHz";
    }

    private void ManageFrequencyBookmarks()
    {
        using var manager = new ManageBookmarksForm(frequencyBookmarks, LoadFavorites(), NormalizeServerUrl(urlBox.Text));
        manager.ShowDialog(this);
        if (!manager.Changed) return;
        if (SaveFrequencyBookmarks()) RefreshFrequencyBookmarks();
    }

    private static string NormalizeServerUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var candidate = value.Contains("://", StringComparison.Ordinal) ? value.Trim() : "http://" + value.Trim();
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.ToString().TrimEnd('/') : "";
    }

    private void ToggleCurrentServerFavorite()
    {
        if (!Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "Enter a valid KiwiSDR URL first.", "Favorites", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var url = uri.ToString().TrimEnd('/');
        var favorites = LoadFavorites();
        var existing = favorites.FirstOrDefault(item => item.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            favorites.Remove(existing);
            if (SaveFavorites(favorites))
            {
                RefreshFavoritesMenu();
                statusLabel.Text = $"Favorite removed: {existing.Title} — {existing.Location}";
            }
            return;
        }

        var serverDetailsMatch = currentServerUrl.Equals(url, StringComparison.OrdinalIgnoreCase);
        var title = serverDetailsMatch && currentServerTitle != "KiwiSDR" ? currentServerTitle : uri.Host;
        var location = serverDetailsMatch ? currentServerLocation : "Location not reported";
        favorites.Add(new FavoriteServer(title, location, url));
        if (SaveFavorites(favorites))
        {
            RefreshFavoritesMenu();
            statusLabel.Text = $"Favorite added: {title} — {location}";
        }
    }

    private bool SaveFavorites(List<FavoriteServer> favorites)
    {
        try
        {
            File.WriteAllText(favoritesPath, JsonSerializer.Serialize(favorites, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save favorites.json.\n\n{ex.Message}", "Favorites", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void RefreshFavoritesMenu()
    {
        favoritesMenu.DropDownItems.Clear();
        var favorites = LoadFavorites();
        if (favorites.Count == 0)
        {
            favoritesMenu.DropDownItems.Add(new ToolStripMenuItem("No favorites yet") { Enabled = false });
            UpdateFavoriteButton();
            return;
        }

        foreach (var favorite in favorites)
        {
            var item = new ToolStripMenuItem($"{favorite.Title} — {favorite.Location}") { ToolTipText = favorite.Url, Tag = favorite };
            item.Click += async (_, _) => await ConnectToFavorite((FavoriteServer)item.Tag!);
            favoritesMenu.DropDownItems.Add(item);
        }
        UpdateFavoriteButton();
    }

    private void UpdateFavoriteButton()
    {
        var normalizedUrl = Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri) ? uri.ToString().TrimEnd('/') : "";
        var isFavorite = normalizedUrl.Length > 0 && LoadFavorites().Any(item => item.Url.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase));
        favoriteButton.Text = isFavorite ? "★" : "☆";
        favoriteButton.BackColor = Color.FromArgb(35, 42, 48);
        favoriteToolTip.SetToolTip(favoriteButton, isFavorite ? "Remove this server from Favorites" : "Add this server to Favorites");
    }

    private async Task ConnectToFavorite(FavoriteServer favorite)
    {
        try { await SwitchReceiverAsync(favorite.Url); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Favorite connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SwitchReceiverAsync(string serverUrl)
    {
        await connectionGate.WaitAsync();
        try
        {
            connectButton.Enabled = false;
            client.StopAudio();
            ResetAudioButton();
            await CancelWaterfallRecordingAsync();
            await client.DisconnectAsync();
            urlBox.Text = serverUrl.Trim().TrimEnd('/');
            connectButton.Text = "Connect";
            await ToggleConnectionCore();
        }
        finally
        {
            connectButton.Enabled = true;
            connectionGate.Release();
        }
    }

    private void SendMode() => client.SetMode(modeBox.Text.ToLowerInvariant());
    private void SendBandwidth() { waterfall.SetPassbandWidth((int)bandwidthBox.Value); client.SetBandwidth((int)bandwidthBox.Value); }
}
