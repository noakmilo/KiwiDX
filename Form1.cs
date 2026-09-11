using System.Globalization;
using System.Text.Json;

namespace KiwiDX;

public partial class Form1 : Form
{
    private readonly KiwiClient client = new();
    private readonly WaterfallControl waterfall = new();
    private readonly FavoriteServerBox urlBox = new();
    private readonly ComboBox protocolBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private bool syncingWebState;
    private readonly TextBox frequencyBox = new();
    private readonly RadioComboBox modeBox = new();
    private readonly NumericUpDown bandwidthBox = new();
    private readonly Label statusLabel = new();
    private readonly Label cursorLabel = new();
    private readonly ReceiverClocks clocks = new() { Dock = DockStyle.Right };
    private readonly RadioButton connectButton = new();
    private readonly CheckBox muteBox = new() { Text = "Mute", AutoSize = true };
    private readonly Panel chatPanel = new() { Dock = DockStyle.Fill };
    private readonly CommunityChatControl communityChat = new();
    private readonly RadioButton recordButton = new() { Glyph = RadioGlyph.Dot, GlyphColor = WorkspaceTheme.Green };
    private readonly CheckBox recordWaterfallCheckBox = new();
    private readonly System.Windows.Forms.Timer waterfallRecordingTimer = new() { Interval = 100 };
    private readonly RadioSlider volumeBar = new();
    private readonly TextBox consoleBox = new();
    private readonly NumericUpDown wfMinBox = new();
    private readonly NumericUpDown wfMaxBox = new();
    private readonly RadioComboBox bandBox = new();
    private readonly RadioSlider zoomBar = new();
    private readonly ToolStripMenuItem favoritesMenu = new("Favorites");
    private readonly ToolStripMenuItem myBookmarksMenu = new("My Bookmarks");
    private readonly RadioButton favoriteButton = new() { Glyph = RadioGlyph.Star, GlyphColor = Color.FromArgb(249, 199, 73) };
    private readonly ToolTip favoriteToolTip = new();
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly RichTextBox serverInfoBox = new();
    private readonly Panel waterfallHost = new() { Dock = DockStyle.Fill, BackColor = Color.Black, Padding = Padding.Empty };
    private readonly Panel receiverDisplayHost = new() { Dock = DockStyle.Fill, BackColor = Color.Black, Padding = Padding.Empty };
    private TableLayoutPanel? mainLayout;
    private Control? navigationBar;
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
    private string currentServerAntenna = "Not reported";
    private readonly string favoritesPath = UserData.PathFor("favorites.json");
    private readonly string startupPath = UserData.PathFor("startup.json");
    private readonly string bookmarksPath = UserData.PathFor("bookmarks.json");
    private StartupConfiguration startupConfiguration = new();
    private List<FrequencyBookmark> frequencyBookmarks = new();
    private WebReceiverControl? webReceiver;

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
        Text = "KiwiDX v0.2.0 - Community Driven SDR Listener";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Width = 1440;
        Height = 900;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 650);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(22, 27, 32);
        ForeColor = Color.Gainsboro;

        var menuStrip = BuildMenuStrip();
        startupConfiguration = LoadStartupConfiguration();
        urlBox.Width = 260;
        urlBox.Text = startupConfiguration.ServerUrl;
        protocolBox.Items.AddRange(ReceiverProtocols.Names);
        protocolBox.SelectedItem = "Auto";
        urlBox.AccessibleName = "Receiver URL or favorite";
        urlBox.Enter += (_, _) => { if (urlBox.Items.Count > 0) urlBox.DroppedDown = true; };
        urlBox.SelectionChangeCommitted += (_, _) =>
        {
            if (urlBox.SelectedItem is FavoriteServer selected) { urlBox.Text = selected.Url; protocolBox.SelectedItem = ReceiverProtocols.Normalize(selected.Protocol); }
        };
        urlBox.DisplayMember = nameof(FavoriteServer.Url);
        urlBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; urlBox.DroppedDown = false; await ToggleConnection(); } };
        urlBox.TextChanged += (_, _) =>
        {
            var favorite = LoadFavorites().FirstOrDefault(item => NormalizeServerUrl(item.Url).Equals(NormalizeServerUrl(urlBox.Text), StringComparison.OrdinalIgnoreCase));
            protocolBox.SelectedItem = ReceiverProtocols.Normalize(favorite?.Protocol);
            UpdateFavoriteButton(); RefreshFrequencyBookmarks();
        };
        connectButton.Text = "Connect";
        connectButton.AutoSize = true;
        connectButton.Click += async (_, _) => await ToggleConnection();
        var mapButton = new RadioButton { Text = "Map", Glyph = RadioGlyph.Globe };
        mapButton.Click += (_, _) => OpenReceiverMap();
        favoriteButton.Text = "☆"; favoriteButton.AutoSize = false; favoriteButton.Size = new Size(26, urlBox.PreferredHeight); favoriteButton.Margin = urlBox.Margin; favoriteButton.Padding = Padding.Empty; favoriteButton.BackColor = WorkspaceTheme.Surface; favoriteButton.ForeColor = Color.Gold; favoriteButton.FlatStyle = FlatStyle.Flat; favoriteButton.Font = new Font("Segoe UI Symbol", 10, FontStyle.Regular); favoriteButton.TextAlign = ContentAlignment.MiddleCenter; favoriteButton.UseVisualStyleBackColor = false; favoriteButton.TabStop = false;
        favoriteButton.FlatAppearance.BorderSize = 0;
        favoriteButton.FlatAppearance.MouseOverBackColor = WorkspaceTheme.Surface;
        favoriteButton.FlatAppearance.MouseDownBackColor = WorkspaceTheme.Surface;
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
        recordButton.Text = "Record";
        recordButton.AutoSize = true;
        recordButton.BackColor = Color.FromArgb(30, 125, 70);
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
        volumeBar.ValueChanged += (_, _) => ApplyAudioPreferences();
        muteBox.CheckedChanged += (_, _) => ApplyAudioPreferences();
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
        navigationBar = BuildNavigationBar();
        receiverDisplayHost.Controls.Add(waterfall);
        waterfallHost.Controls.Add(receiverDisplayHost);

        waterfall.Dock = DockStyle.Fill;
        waterfall.CursorChangedByUser += (_, frequency) => { frequencyBox.Text = (frequency / 1_000_000d).ToString("0.000000", CultureInfo.InvariantCulture); client.SetFrequency(frequency); };
        waterfall.CursorMoved += (_, frequency) => cursorLabel.Text = $"Cursor: {frequency / 1_000_000d:0.000000} MHz";
        waterfall.BandwidthChanged += (_, bandwidth) => { bandwidthBox.Value = Math.Clamp(bandwidth, (int)bandwidthBox.Minimum, (int)bandwidthBox.Maximum); };
        waterfall.ViewChanged += (_, view) =>
        {
            SyncNavigation(view.center, view.span);
            client.SetWaterfallView(SpanToZoom(view.span), view.center);
        };
        waterfall.AddHamLogRequested += (_, frequency) => AddListeningLog(true, frequency);
        waterfall.AddShortwaveLogRequested += (_, frequency) => AddListeningLog(false, frequency);
        waterfall.AddBookmarkRequested += (_, frequency) => AddFrequencyBookmark(frequency);
        statusLabel.AutoSize = true;
        statusLabel.Padding = new Padding(8, 5, 0, 0);
        cursorLabel.AutoSize = true;
        cursorLabel.Padding = new Padding(8, 5, 0, 0);
        consoleBox.Dock = DockStyle.Fill;
        consoleBox.Multiline = true;
        consoleBox.ReadOnly = true;
        consoleBox.ScrollBars = ScrollBars.Vertical;
        consoleBox.BackColor = Color.FromArgb(10, 13, 15);
        consoleBox.ForeColor = Color.LightGreen;
        consoleBox.Font = new Font(Font.FontFamily, 8.5f);
        serverInfoBox.Dock = DockStyle.Fill;
        serverInfoBox.Multiline = true;
        serverInfoBox.ReadOnly = true;
        serverInfoBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        serverInfoBox.BorderStyle = BorderStyle.None;
        serverInfoBox.BackColor = Color.FromArgb(24, 31, 37);
        serverInfoBox.ForeColor = Color.Gainsboro;
        serverInfoBox.Font = new Font(Font.FontFamily, 8.5f);
        serverInfoBox.Text = "Connect to a KiwiSDR to view server information.";
        communityChat.GetReceiver = GetChatReceiverAsync;
        communityChat.TuneReceiver = TuneChatReceiverAsync;
        BuildWorkspace(menuStrip, mapButton);

        client.WaterfallLine += (_, line) => Interlocked.Exchange(ref pendingWaterfallLine, line);
        receiverUiTimer.Tick += (_, _) =>
        {
            RefreshConsole();
            var line = Interlocked.Exchange(ref pendingWaterfallLine, null);
            if (line is not null) { waterfall.AddLine(line); wfUiLines++; }
        };
        receiverUiTimer.Start();
        Disposed += (_, _) => receiverUiTimer.Dispose();
        client.StatusChanged += (_, status) =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                statusLabel.Text = status;
                if (status == "Disconnected" || status.StartsWith("Connection lost", StringComparison.OrdinalIgnoreCase))
                {
                    if (webReceiver is null) clocks.SetReceiverTime(null);
                    connectButton.Text = "Connect";
                    ResetAudioButton();
                    ResetRecordingButton();
                    _ = CancelWaterfallRecordingAsync();
                }
            });
        };
        client.Error += (_, error) => { if (!IsDisposed) BeginInvoke(() => MessageBox.Show(this, error, "KiwiSDR", MessageBoxButtons.OK, MessageBoxIcon.Warning)); };
        client.Log += (_, message) => AddLog(message);
        client.ConnectionProgress += (_, progress) => { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { if (!connectButton.Enabled) ShowConnectionProgress(progress.Percent, progress.Stage); }); };
        client.ServerInfoChanged += (_, info) => { if (!IsDisposed) BeginInvoke(() => SetServerInfo(info)); };
        client.ReceiverTimeChanged += (_, time) => { if (!IsDisposed) BeginInvoke(() => { if (webReceiver is null) clocks.SetReceiverTime(time); }); };
        FormClosing += async (_, _) => { DeactivateWebReceiver(); await CancelWaterfallRecordingAsync(); await client.DisposeAsync(); };

        SyncNavigation(7_100_000, currentViewSpan);
        ApplyWaterfallRange();
        RefreshFavoritesMenu();
        frequencyBookmarks = LoadFrequencyBookmarks();
        RefreshFrequencyBookmarks();
        Shown += async (_, _) => { FitWorkspace(); _ = communityChat.ActivateAsync(); await ApplyStartupConfiguration(); };
    }

    private void SetServerInfo(string info)
    {
        var infoUrl = ReadServerInfoField(info, "URL");
        if (webReceiver is not null || (infoUrl is not null && !NormalizeServerUrl(infoUrl).Equals(NormalizeServerUrl(currentServerUrl), StringComparison.OrdinalIgnoreCase))) return;
        currentServerAntenna = ReadServerInfoField(info, "Antenna") ?? "Not reported";
        currentServerTitle = ReadServerInfoField(info, "Station / operator message") ?? "KiwiSDR";
        currentServerLocation = ReadServerInfoField(info, "Location") ?? "Location not reported";
        currentServerUrl = (ReadServerInfoField(info, "URL") ?? currentServerUrl).TrimEnd('/');
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
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = WorkspaceTheme.Surface, Padding = new Padding(8, 4, 8, 4), Margin = Padding.Empty };
        zoomBar.Minimum = 0; zoomBar.Maximum = 14; zoomBar.Value = 11; zoomBar.Width = 180; zoomBar.Height = 32;
        zoomBar.ValueChanged += (_, _) => { if (!updatingNavigation) waterfall.SetZoomLevel(zoomBar.Value); };
        var minus = new RadioButton { Glyph = RadioGlyph.Minus, Width = 30, Height = 30 };
        var plus = new RadioButton { Glyph = RadioGlyph.Plus, Width = 30, Height = 30 };
        minus.AccessibleName = "Zoom out"; plus.AccessibleName = "Zoom in";
        minus.Click += (_, _) => { if (webReceiver is not null) webReceiver.ZoomOut(); else waterfall.ZoomOut(); };
        plus.Click += (_, _) => { if (webReceiver is not null) webReceiver.ZoomIn(); else waterfall.ZoomIn(); };
        var center = new RadioButton { Text = "Center", Width = 82, Height = 32 };
        center.Click += (_, _) => { if (webReceiver is not null) webReceiver.CenterWaterfall(); else waterfall.CenterOnTune(); };
        var display = new RadioButton { Text = "Display settings", Glyph = RadioGlyph.Gear, Width = 174, Height = 32 };
        display.Click += (_, _) => OpenDisplaySettings();
        panel.Controls.AddRange(new Control[] { WorkspaceTheme.Label("Zoom"), minus, zoomBar, plus, center, display });
        return panel;
    }

    private void SyncNavigation(double center, double span)
    {
        updatingNavigation = true;
        try
        {
            currentViewSpan = span;
            var zoom = SpanToZoom(span);
            zoomBar.Value = zoom;
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

    private void MoveDial(double deltaHz)
    {
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) return;
        var frequency = webReceiver is null ? Math.Clamp(mhz * 1_000_000 + deltaHz, 10_000, 30_000_000) : Math.Max(1, mhz * 1_000_000 + deltaHz);
        frequencyBox.Text = (frequency / 1_000_000).ToString("0.000000", CultureInfo.InvariantCulture);
        waterfall.SetTunedFrequency(frequency);
        client.SetFrequency(frequency);
        webReceiver?.SetFrequency(frequency);
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
        webReceiver?.SetFrequency(frequency);
        webReceiver?.SetMode(band.Mode);
        webReceiver?.SetBandwidth(band.Bandwidth);
    }

    // Keep only the latest display frame when the UI cannot keep up with the receiver.
    private byte[]? pendingWaterfallLine;
    private readonly System.Windows.Forms.Timer receiverUiTimer = new() { Interval = 33 };

    private readonly Queue<string> consoleHistory = new();
    private int consoleHistoryLength;
    private bool consoleDirty;
    private void AddLog(string message)
    {
        // Never touch the native edit control while its tool window is hidden/uncreated.
        var entry = $"[{DateTime.Now:HH:mm:ss}] {(message.Length > 2048 ? message[..2048] + " [truncated]" : message)}{Environment.NewLine}";
        lock (consoleHistory)
        {
        consoleHistory.Enqueue(entry); consoleHistoryLength += entry.Length;
        while (consoleHistoryLength > 20000 && consoleHistory.Count > 1) consoleHistoryLength -= consoleHistory.Dequeue().Length;
        consoleDirty = true;
        }
    }
    private void RefreshConsole()
    {
        if (consoleWindow is not { Visible: true }) return;
        string snapshot;
        lock (consoleHistory) { if (!consoleDirty) return; snapshot = string.Concat(consoleHistory); consoleDirty = false; }
        consoleBox.Text = snapshot;
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
        if (webReceiver is not null)
        {
            if (NormalizeServerUrl(urlBox.Text).Equals(currentServerUrl, StringComparison.OrdinalIgnoreCase)) { DeactivateWebReceiver(); return; }
            DeactivateWebReceiver();
        }
        if (client.IsConnected)
        {
            var sameReceiver = NormalizeServerUrl(urlBox.Text).Equals(currentServerUrl, StringComparison.OrdinalIgnoreCase);
            await CancelWaterfallRecordingAsync(); await client.DisconnectAsync(); connectButton.Text = "Connect"; ResetAudioButton();
            if (sameReceiver) return;
        }
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) { MessageBox.Show("Invalid frequency."); return; }
        ResetAudioButton();
        connectButton.Enabled = false;
        ShowConnectionProgress(5, "Finding receiver...");
        try
        {
            var frequency = mhz * 1_000_000;
            var receiverUrl = NormalizeServerUrl(urlBox.Text);
            if (receiverUrl.Length == 0) throw new InvalidOperationException("Enter a valid HTTP/HTTPS receiver URL.");
            var protocol = ReceiverProtocols.Normalize(protocolBox.Text);
            if (protocol == "Auto") protocol = ReceiverProtocols.Normalize(LoadFavorites().FirstOrDefault(f => NormalizeServerUrl(f.Url) == receiverUrl)?.Protocol);
            if (protocol == "Auto") protocol = IsTwenteWebSdr(receiverUrl) ? "WebSDR" : await ReceiverProtocols.DetectAsync(receiverUrl);
            if (protocol is "OpenWebRX" or "WebSDR")
            {
                ShowConnectionProgress(35, "Loading receiver waterfall...");
                await ActivateWebReceiverAsync(receiverUrl, protocol, frequency);
                ShowConnectionProgress(65, "Waiting for receiver initialization...");
                var deadline = Environment.TickCount64 + 20000;
                while (webReceiver is { Ready: false } && Environment.TickCount64 < deadline) await Task.Delay(100);
                if (webReceiver is { Ready: true }) { ShowConnectionProgress(100, "Receiver ready"); await Task.Delay(250); }
                else statusLabel.Text = "Receiver still loading. Check the receiver panel for a profile or error.";
                return;
            }
            currentServerUrl = NormalizeServerUrl(urlBox.Text);
            currentServerAntenna = "Not reported";
            ShowConnectionProgress(25, "Opening receiver channels...");
            await client.ConnectAsync(urlBox.Text, frequency, modeBox.Text.ToLowerInvariant(), (int)bandwidthBox.Value);
            ShowConnectionProgress(85, "Starting audio...");
            PreselectBandForFrequency(frequency);
            waterfall.SetRadioState(frequency, 30_000_000d / (1 << 11), frequency, (int)bandwidthBox.Value);
            ApplyWaterfallRange();
            StartReceiverAudio();
            connectButton.Text = "Disconnect";
            ShowConnectionProgress(100, "Receiver ready");
            await Task.Delay(250);
        }
        catch (Exception ex)
        {
            await client.DisconnectAsync();
            connectButton.Text = "Connect";
            ResetAudioButton();
            MessageBox.Show(this, GetFriendlyConnectionError(ex), "Receiver unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { connectButton.Enabled = true; HideConnectionProgress(); }
    }

    private async Task ActivateWebReceiverAsync(string receiverUrl, string protocol, double frequencyHz)
    {
        currentServerAntenna = "Not reported";
        currentServerUrl = receiverUrl;
        currentServerTitle = protocol + " - " + new Uri(receiverUrl).Host;
        currentServerLocation = "Location not reported";
        clocks.ReceiverZone = IsTwenteWebSdr(receiverUrl) ? TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time") : null;
        clocks.SetReceiverTime(null);
        var receiver = new WebReceiverControl(receiverUrl, protocol, frequencyHz, modeBox.Text, (int)bandwidthBox.Value, volumeBar.Value);
        webReceiver = receiver;
        receiver.StatusChanged += (_, status) => { if (!IsDisposed && webReceiver == receiver) statusLabel.Text = status; };
        receiver.StateChanged += (_, _) =>
        {
            if (webReceiver != receiver || syncingWebState) return;
            syncingWebState = true;
            try
            {
                if (!frequencyBox.Focused) frequencyBox.Text = (receiver.FrequencyHz / 1_000_000).ToString("0.000000", CultureInfo.InvariantCulture);
                if (!modeBox.Items.Contains(receiver.Mode)) modeBox.Items.Add(receiver.Mode);
                modeBox.SelectedItem = receiver.Mode;
                if (receiver.Bandwidth >= bandwidthBox.Minimum && receiver.Bandwidth <= bandwidthBox.Maximum) bandwidthBox.Value = receiver.Bandwidth;
            }
            finally { syncingWebState = false; }
        };
        waterfall.Visible = false;
        receiverDisplayHost.Controls.Add(receiver);
        receiver.BringToFront();
        connectButton.Text = "Disconnect";
        recordButton.Enabled = false;
        recordWaterfallCheckBox.Enabled = false;
        bandBox.Enabled = false;
        wfMinBox.Enabled = wfMaxBox.Enabled = false;
        zoomBar.Enabled = false;
        serverInfoBox.Text = currentServerTitle + "\n" + receiverUrl + "\nSelect bands/profiles and advanced modes in the receiver panel.\nNative recording is unavailable for web receivers.";
        UpdateFavoriteButton();
        try { await receiver.InitializeAsync(); StartReceiverAudio(); }
        catch { DeactivateWebReceiver(); throw; }
    }
    private void DeactivateWebReceiver()
    {
        if (webReceiver is null) return;
        receiverDisplayHost.Controls.Remove(webReceiver);
        webReceiver.Dispose();
        webReceiver = null;
        clocks.ReceiverZone = null;
        clocks.SetReceiverTime(null);
        waterfall.Visible = true;
        waterfall.BringToFront();
        receiverDisplayHost.PerformLayout();
        waterfall.Invalidate(true);
        connectButton.Text = "Connect";
        bandBox.Enabled = true;
        wfMinBox.Enabled = wfMaxBox.Enabled = true;
        zoomBar.Enabled = true;
        recordButton.Enabled = true;
        recordWaterfallCheckBox.Enabled = true;
        ResetAudioButton();
        currentServerTitle = "KiwiSDR";
        currentServerLocation = "Location not reported";
        currentServerUrl = "";
        serverInfoBox.Text = "Connect to a KiwiSDR to view server information.";
        statusLabel.Text = "Disconnected";
    }

    private static bool IsTwenteWebSdr(string input)
    {
        if (!Uri.TryCreate(input.Contains("://", StringComparison.Ordinal) ? input : "http://" + input,
                UriKind.Absolute, out var uri)) return false;
        return uri.Host.Equals("websdr.ewi.utwente.nl", StringComparison.OrdinalIgnoreCase) && uri.Port == 8901;
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

    private void ApplyAudioPreferences()
    {
        client.SetVolume(muteBox.Checked ? 0 : volumeBar.Value / 100f);
        webReceiver?.SetVolume(volumeBar.Value);
        webReceiver?.SetMuted(muteBox.Checked);
    }
    private void StartReceiverAudio()
    {
        ApplyAudioPreferences();
        if (webReceiver is not null) webReceiver.Play(); else if (client.IsConnected) client.PlayAudio();
        audioPlaying = client.IsConnected || webReceiver is not null;
    }
    private void ResetAudioButton() { audioPlaying = false; }
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
            bandBox.Enabled = true;
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
        recordButton.Text = "Record";
        recordButton.BackColor = Color.FromArgb(30, 125, 70);
        recordWaterfallCheckBox.Enabled = true;
    }

    private void SendFrequency()
    {
        if (!double.TryParse(frequencyBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)) return;
        var frequency = mhz * 1_000_000;
        client.SetFrequency(frequency);
        webReceiver?.SetFrequency(frequency);
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
        var file = new ToolStripMenuItem("Receiver");
        file.DropDownItems.Add("Startup preferences...", null, (_, _) => OpenStartupSettings());
        var protocols = new ToolStripMenuItem("Protocol");
        foreach (var name in ReceiverProtocols.Names) { var item = new ToolStripMenuItem(name) { Checked = name == "Auto" }; item.Click += (_, _) => { protocolBox.SelectedItem = name; foreach (ToolStripMenuItem other in protocols.DropDownItems) other.Checked = other == item; }; protocols.DropDownItems.Add(item); }
        protocols.DropDownOpening += (_, _) => { foreach (ToolStripMenuItem item in protocols.DropDownItems) item.Checked = item.Text == protocolBox.Text; };
        file.DropDownItems.Add(protocols);
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var bookmarks = new ToolStripMenuItem("Bookmarks");
        bookmarks.DropDownItems.Add(favoritesMenu);
        bookmarks.DropDownItems.Add(myBookmarksMenu);
        bookmarks.DropDownItems.Add(new ToolStripSeparator());
        bookmarks.DropDownItems.Add("Import Favorites...", null, (_, _) => ImportData(false));
        bookmarks.DropDownItems.Add("Export Favorites...", null, (_, _) => ExportData(false));
        bookmarks.DropDownItems.Add("Import Bookmarks...", null, (_, _) => ImportData(true));
        bookmarks.DropDownItems.Add("Export Bookmarks...", null, (_, _) => ExportData(true));
        var settings = new ToolStripMenuItem("View");
        settings.DropDownItems.Add("Community Chat", null, async (_, _) => { ShowSidebar("chat"); await communityChat.ActivateAsync(); });
        settings.DropDownItems.Add("Detach chat", null, (_, _) => DetachChat());
        settings.DropDownItems.Add("Server details", null, (_, _) => ShowSidebar("details"));
        settings.DropDownItems.Add("Console Log", null, (_, _) => OpenConsoleWindow());
        settings.DropDownItems.Add("Display settings", null, (_, _) => OpenDisplaySettings());
        var help = new ToolStripMenuItem("Help");
        var updates = new ToolStripMenuItem("Check for Updates...");
        updates.Click += async (_, _) => await CheckForUpdates(updates);
        help.DropDownItems.Add(updates);
        help.DropDownItems.Add("About", null, (_, _) => new AboutForm().ShowDialog(this));
        var logging = new ToolStripMenuItem("Logging");
        logging.DropDownItems.Add("View Logs...", null, (_, _) => ShowListeningLogs());
        logging.DropDownItems.Add("Log Ham Radio...", null, (_, _) => AddListeningLog(true, waterfall.TunedFrequency));
        logging.DropDownItems.Add("Log Shortwave Listening...", null, (_, _) => AddListeningLog(false, waterfall.TunedFrequency));
        menu.Items.AddRange(new ToolStripItem[] { file, bookmarks, logging, settings, help });
        return menu;
    }

    private StartupConfiguration LoadStartupConfiguration()
    {
        try
        {
            if (!File.Exists(startupPath))
            {
                var defaults = new StartupConfiguration();
                UserData.Write(startupPath, defaults);
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
            UserData.Write(startupPath, startupConfiguration);
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

    }

    private List<FavoriteServer> LoadFavorites()
    {
        try
        {
            if (!File.Exists(favoritesPath)) UserData.Write(favoritesPath, new[] { new FavoriteServer("Twente WebSDR", "[OFF Kiwi Network]", "http://websdr.ewi.utwente.nl:8901") });
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
            if (migrated) UserData.Write(bookmarksPath, loaded);
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
            UserData.Write(bookmarksPath, frequencyBookmarks);
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
        webReceiver?.SetFrequency(bookmark.FrequencyHz);
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
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 ? uri.ToString().TrimEnd('/') : "";
    }

    private void ToggleCurrentServerFavorite()
    {
        if (!Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "Enter a valid receiver URL first.", "Favorites", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        favorites.Add(new FavoriteServer(title, location, url) { Protocol = ReceiverProtocols.Normalize(protocolBox.Text) });
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
            UserData.Write(favoritesPath, favorites);
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
        var typedUrl = urlBox.Text;
        urlBox.Items.Clear();
        urlBox.Items.AddRange(favorites.Cast<object>().ToArray());
        urlBox.Text = typedUrl;
        if (favorites.Count == 0)
        {
            favoritesMenu.DropDownItems.Add(new ToolStripMenuItem("No favorites yet") { Enabled = false });
        }

        foreach (var favorite in favorites)
        {
            var item = new ToolStripMenuItem($"{favorite.Title} — {favorite.Location}") { ToolTipText = favorite.Url, Tag = favorite };
            item.Click += async (_, _) => await ConnectToFavorite((FavoriteServer)item.Tag!);
            favoritesMenu.DropDownItems.Add(item);
        }
        favoritesMenu.DropDownItems.Add(new ToolStripSeparator());
        favoritesMenu.DropDownItems.Add("Manage Favorites...", null, (_, _) =>
        {
            using var manager = new ManageFavoritesForm(LoadFavorites(), SaveFavorites);
            manager.ShowDialog(this);
            RefreshFavoritesMenu();
        });
        UpdateFavoriteButton();
    }

    private void UpdateFavoriteButton()
    {
        var normalizedUrl = Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri) ? uri.ToString().TrimEnd('/') : "";
        var isFavorite = normalizedUrl.Length > 0 && LoadFavorites().Any(item => item.Url.Equals(normalizedUrl, StringComparison.OrdinalIgnoreCase));
        favoriteButton.Text = ""; favoriteButton.Active = isFavorite; favoriteButton.Invalidate();
        favoriteButton.BackColor = Color.FromArgb(35, 42, 48);
        favoriteToolTip.SetToolTip(favoriteButton, isFavorite ? "Remove this server from Favorites" : "Add this server to Favorites");
    }

    private async Task ConnectToFavorite(FavoriteServer favorite)
    {
        try { await SwitchReceiverAsync(favorite.Url, favorite.Protocol); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Favorite connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SwitchReceiverAsync(string serverUrl, string protocol = "Auto", double? requestedFrequency = null, string? requestedMode = null)
    {
        await connectionGate.WaitAsync();
        try
        {
            connectButton.Enabled = false;
        ShowConnectionProgress(5, "Finding receiver...");
            client.StopAudio();
            ResetAudioButton();
            await CancelWaterfallRecordingAsync();
            DeactivateWebReceiver();
            await client.DisconnectAsync();
            urlBox.Text = serverUrl.Trim().TrimEnd('/');
            protocolBox.SelectedItem = ReceiverProtocols.Normalize(protocol);
            if (requestedFrequency.HasValue) frequencyBox.Text = (requestedFrequency.Value / 1_000_000).ToString("0.000000", CultureInfo.InvariantCulture);
            if (requestedMode is not null)
            {
                if (!modeBox.Items.Contains(requestedMode)) modeBox.Items.Add(requestedMode);
                syncingWebState = true;
                try { modeBox.SelectedItem = requestedMode; }
                finally { syncingWebState = false; }
            }
            connectButton.Text = "Connect";
            await ToggleConnectionCore();
        }
        finally
        {
            connectButton.Enabled = true;
            connectionGate.Release();
        }
    }

    private void SendMode() { if (syncingWebState) return; client.SetMode(modeBox.Text.ToLowerInvariant()); webReceiver?.SetMode(modeBox.Text); }
    private void SendBandwidth() { if (syncingWebState) return; waterfall.SetPassbandWidth((int)bandwidthBox.Value); client.SetBandwidth((int)bandwidthBox.Value); webReceiver?.SetBandwidth((int)bandwidthBox.Value); }
}
