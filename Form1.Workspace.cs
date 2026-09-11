namespace KiwiDX;

public partial class Form1
{
    private readonly SplitContainer workspace = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = WorkspaceTheme.Border, SplitterWidth = 6, FixedPanel = FixedPanel.Panel2 };
    private readonly RadioButton chatToggle = new() { Text = "Chat", Glyph = RadioGlyph.Chat, GlyphColor = WorkspaceTheme.Green, Width = 132 };
    private readonly Label receiverState = new() { Text = "Disconnected", AutoSize = false, ForeColor = WorkspaceTheme.Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label receiverSummary = new() { Text = "Disconnected", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = WorkspaceTheme.Green };
    private readonly Label volumePercent = new() { Text = "75%", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = WorkspaceTheme.Text };
    private readonly Label recordingElapsed = new() { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = WorkspaceTheme.Green };
    private readonly System.Windows.Forms.Timer workspaceTimer = new() { Interval = 1000 };
    private readonly RadioComboBox recordingFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private Form? consoleWindow, chatWindow, displayWindow;
    private Panel? detailsHost;
    private bool closingWorkspace;
    private string sidebar = "";
    private long recordingStarted;
    private bool sizedWorkspace;

    private void BuildWorkspace(MenuStrip menu, RadioButton map)
    {
        BackColor = WorkspaceTheme.Background;
        WorkspaceTheme.ApplyWindowStyle(this);
        menu.BackColor = WorkspaceTheme.Surface; menu.ForeColor = WorkspaceTheme.Text;
        menu.Renderer = new RadioMenuRenderer(); menu.Font = new Font("Segoe UI", 10);
        foreach (var control in new Control[] { urlBox, protocolBox, frequencyBox, modeBox, bandwidthBox, bandBox, wfMinBox, wfMaxBox, recordingFormat }) WorkspaceTheme.Style(control);
        connectButton.AutoSize = false; connectButton.Width = 130; connectButton.Height = 38;
        favoriteButton.AutoSize = false; favoriteButton.Size = new Size(54,38); favoriteButton.Text = ""; favoriteButton.TabStop = true;
        favoriteButton.BackColor = WorkspaceTheme.Surface; favoriteButton.AccessibleName = "Toggle receiver favorite";
        map.Size = new Size(102,38); chatToggle.Height = 38;
        urlBox.Font = new Font(Font.FontFamily, 11); urlBox.Dock = DockStyle.Fill; urlBox.Margin = new Padding(5,13,8,5);
        var receiverRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, BackColor = WorkspaceTheme.Surface, Padding = new Padding(12,4,12,4), Margin = Padding.Empty };
        foreach (int width in new[]{170,75,0,110,136,108,62,140}) receiverRow.ColumnStyles.Add(width==0?new ColumnStyle(SizeType.Percent,100):new ColumnStyle(SizeType.Absolute,width));
        receiverRow.Controls.Add(new RadioBrand(),0,0); receiverRow.Controls.Add(WorkspaceTheme.Label("Receiver"),1,0);receiverRow.Controls.Add(urlBox,2,0);
        receiverState.Dock = DockStyle.Fill; receiverRow.Controls.Add(receiverState,3,0);receiverRow.Controls.Add(connectButton,4,0);receiverRow.Controls.Add(map,5,0);receiverRow.Controls.Add(favoriteButton,6,0);receiverRow.Controls.Add(chatToggle,7,0);
        foreach(Control c in new Control[]{connectButton,map,favoriteButton,chatToggle}) c.Anchor=AnchorStyles.None;
        chatToggle.Click += async (_, _) => { if(chatWindow is { Visible:true }) {chatWindow.Activate();return;} ShowSidebar(sidebar=="chat"?"":"chat"); if(sidebar=="chat") await communityChat.ActivateAsync(); };
        var tuning = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 1, BackColor = WorkspaceTheme.Surface, Padding = new Padding(6,6,6,6), Margin = Padding.Empty };
        foreach(var width in new[]{48,140,84,270,52,85,74,90,0}) tuning.ColumnStyles.Add(width==0?new ColumnStyle(SizeType.Percent,100):new ColumnStyle(SizeType.Absolute,width));
        tuning.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        tuning.SizeChanged += (_,_)=> tuning.ColumnStyles[3].Width = Math.Clamp(tuning.Width-600,170,280);
        bandBox.FormattingEnabled = true;
        bandBox.Format += (_, e) => { if (e.ListItem is BandPreset band) e.Value = band.Name.Split(' ')[0].Replace("m", " m"); };
        bandBox.Font = new Font(Font.FontFamily,10);bandBox.Dock=DockStyle.Fill;
        frequencyBox.Font = new Font(Font.FontFamily,18,FontStyle.Bold); frequencyBox.TextAlign=HorizontalAlignment.Center; frequencyBox.BorderStyle=BorderStyle.None; frequencyBox.Dock=DockStyle.Fill;
        var frequencyPanel = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=WorkspaceTheme.Input,Padding=new Padding(4),Margin=new Padding(4,0,8,0) };
        frequencyPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        frequencyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frequencyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));
        frequencyPanel.Controls.Add(frequencyBox,0,0);frequencyPanel.Controls.Add(WorkspaceTheme.Label("MHz"),1,0);
        tuning.Controls.Add(WorkspaceTheme.Label("Band"),0,0);tuning.Controls.Add(bandBox,1,0);tuning.Controls.Add(WorkspaceTheme.Label("Frequency"),2,0);tuning.Controls.Add(frequencyPanel,3,0);
        tuning.Controls.Add(WorkspaceTheme.Label("Mode"),4,0);tuning.Controls.Add(modeBox,5,0);tuning.Controls.Add(WorkspaceTheme.Label("BW Hz"),6,0);tuning.Controls.Add(bandwidthBox,7,0);
        foreach(var label in tuning.Controls.OfType<Label>()){label.AutoSize=false;label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleLeft;label.Margin=new Padding(3,0,3,0);}
        foreach(var label in frequencyPanel.Controls.OfType<Label>()){label.AutoSize=false;label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleLeft;label.Margin=Padding.Empty;}
        foreach(Control c in new Control[]{bandBox,modeBox,bandwidthBox}) {c.Anchor=AnchorStyles.Left|AnchorStyles.Right;c.Margin=new Padding(4,8,4,8);}
        var receiverArea = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty };
        receiverArea.RowStyles.Add(new RowStyle(SizeType.Absolute,60));receiverArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));receiverArea.RowStyles.Add(new RowStyle(SizeType.Absolute,46));receiverArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        receiverArea.Controls.Add(tuning,0,0);receiverArea.Controls.Add(waterfallHost,0,1);receiverArea.Controls.Add(navigationBar!,0,2);
        workspace.Size=new Size(1400,600); workspace.Panel1MinSize=760;workspace.Panel2MinSize=300;workspace.SplitterDistance=1000;
        workspace.Panel1.Controls.Add(receiverArea);workspace.Panel2.BackColor=WorkspaceTheme.Background;
        chatPanel.Controls.Add(communityChat); chatPanel.Padding=Padding.Empty; chatPanel.Margin=Padding.Empty; communityChat.Font=Font;
        var audio = new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(18,6,8,4),Margin=Padding.Empty,BackColor=WorkspaceTheme.Surface };
        var speaker=new RadioButton { Glyph=RadioGlyph.Speaker,Width=34,Height=34,TabStop=false,Enabled=false };
        volumeBar.Size=new Size(230,34);volumeBar.BackColor=WorkspaceTheme.Surface;volumeBar.AccessibleName="Volume";
        var mute = new ToggleSwitch { Text="Mute", ShowState=false, BackColor=WorkspaceTheme.Surface, ForeColor=WorkspaceTheme.Text, Checked=muteBox.Checked, Margin=new Padding(18,6,22,0) };
        mute.CheckedChanged += (_,_)=>muteBox.Checked=mute.Checked;
        recordButton.AutoSize=false;recordButton.Size=new Size(116,36);recordButton.BackColor=WorkspaceTheme.Surface;
        recordingFormat.Items.AddRange(new object[]{"Audio MP3","Waterfall + Audio"});recordingFormat.SelectedIndex=0;recordingFormat.Margin=new Padding(12,7,12,0);
        recordingFormat.SelectedIndexChanged += (_,_)=>recordWaterfallCheckBox.Checked=recordingFormat.SelectedIndex==1;
        recordWaterfallCheckBox.EnabledChanged += (_,_)=>recordingFormat.Enabled=recordWaterfallCheckBox.Enabled;
        audio.Controls.AddRange(new Control[]{speaker,WorkspaceTheme.Label("Volume"),volumeBar,volumePercent,mute,recordButton,recordingFormat,recordingElapsed});
        volumeBar.ValueChanged += (_,_)=>volumePercent.Text=$"{volumeBar.Value}%";
        var status = new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(20,0,10,0),Margin=Padding.Empty,BackColor=WorkspaceTheme.Background};
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,430));status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));
        clocks.Dock=DockStyle.Fill;clocks.BackColor=WorkspaceTheme.Background;
        var details=new RadioButton {Text="Server details",Glyph=RadioGlyph.Info,Dock=DockStyle.Fill,Margin=new Padding(4,3,4,3)};
        details.Click += (_,_)=>ShowSidebar(sidebar=="details"?"":"details");
        status.Controls.Add(receiverSummary,0,0);status.Controls.Add(clocks,1,0);status.Controls.Add(details,2,0);
        favoriteToolTip.SetToolTip(receiverSummary,"Receiver status. Open Server details for full information.");
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=WorkspaceTheme.Background};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,64));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        root.Controls.Add(receiverRow,0,0);root.Controls.Add(workspace,0,1);root.Controls.Add(audio,0,2);root.Controls.Add(status,0,3);mainLayout=root;
        Controls.Add(root);Controls.Add(menu);MainMenuStrip=menu;
        statusLabel.TextChanged += (_,_)=>UpdateWorkspaceStatus();connectButton.TextChanged += (_,_)=>UpdateWorkspaceStatus();
        workspace.SizeChanged += (_,_)=>FitWorkspace();
        workspaceTimer.Tick += (_,_)=>{UpdateWorkspaceStatus();bool recording=client.IsRecording||recordingWaterfall;if(recording){if(recordingStarted==0)recordingStarted=Environment.TickCount64;recordingElapsed.Text=TimeSpan.FromMilliseconds(Environment.TickCount64-recordingStarted).ToString(@"hh\:mm\:ss");}else{recordingStarted=0;recordingElapsed.Text="";}recordButton.GlyphColor=recording?Color.OrangeRed:WorkspaceTheme.Green;recordButton.Invalidate();};workspaceTimer.Start();
        FormClosing += (_,_)=>{closingWorkspace=true;workspaceTimer.Stop();};
        Disposed += (_,_)=>{workspaceTimer.Dispose();detailsHost?.Dispose();consoleWindow?.Dispose();chatWindow?.Dispose();displayWindow?.Dispose();};
        ShowSidebar("chat");
    }

    private Panel? connectionProgress;
    private Label? connectionProgressLabel;
    private ProgressBar? connectionProgressBar;
    private void ShowConnectionProgress(int percent, string stage)
    {
        if (connectionProgress is null)
        {
            connectionProgress = new Panel { Size = new Size(380, 100), BackColor = WorkspaceTheme.Surface, BorderStyle = BorderStyle.FixedSingle };
            connectionProgressLabel = new Label { Dock = DockStyle.Top, Height = 48, TextAlign = ContentAlignment.MiddleCenter, ForeColor = WorkspaceTheme.Green };
            connectionProgressBar = new ProgressBar { Location = new Point(20, 55), Size = new Size(338, 20), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
            connectionProgress.Controls.Add(connectionProgressLabel); connectionProgress.Controls.Add(connectionProgressBar);
            Controls.Add(connectionProgress);
            Resize += (_, _) => CenterConnectionProgress();
        }
        connectionProgressLabel!.Text = $"{stage}  {percent}% (steps)";
        CenterConnectionProgress(); connectionProgress.Show(); connectionProgress.BringToFront();
    }
    private void CenterConnectionProgress()
    {
        if (connectionProgress is not null) connectionProgress.Location = new Point(Math.Max(0, (ClientSize.Width - connectionProgress.Width) / 2), Math.Max(0, (ClientSize.Height - connectionProgress.Height) / 2));
    }
    private void HideConnectionProgress() => connectionProgress?.Hide();

    private void FitWorkspace()
    {
        if(workspace.Width<1000)return;
        if(!sizedWorkspace){workspace.SplitterDistance=Math.Max(workspace.Panel1MinSize,workspace.Width-Math.Max(300,Math.Min(390,(int)(workspace.Width*.28))));sizedWorkspace=true;}
    }
    private void UpdateWorkspaceStatus()
    {
        bool connected=client.IsConnected||webReceiver is not null;
        receiverState.Text=connected?"Connected":"Disconnected";receiverState.ForeColor=connected?WorkspaceTheme.Green:WorkspaceTheme.Muted;
        receiverSummary.Text=connected?$"Connected \u00B7 {currentServerTitle}":"Disconnected";
        favoriteToolTip.SetToolTip(receiverSummary,statusLabel.Text);
    }
    private void ShowSidebar(string name)
    {
        if(name=="chat" && chatWindow is { Visible:true }) chatWindow.Hide();
        sidebar=name;workspace.SuspendLayout();
        foreach(Control c in workspace.Panel2.Controls.Cast<Control>().ToArray())workspace.Panel2.Controls.Remove(c);
        if(name=="chat")workspace.Panel2.Controls.Add(chatPanel);
        else if(name=="details")
        {
            if(detailsHost is null)
            {
            var host=new Panel{Dock=DockStyle.Fill,BackColor=WorkspaceTheme.Background,Padding=new Padding(14)};
            var close=new RadioButton{Text="Back to chat",Glyph=RadioGlyph.Chat,Dock=DockStyle.Bottom,Height=38};close.Click+=(_,_)=>ShowSidebar("chat");
            var title=new Label{Text="Server details",Dock=DockStyle.Top,Height=42,ForeColor=WorkspaceTheme.Green,Font=new Font(Font.FontFamily,15,FontStyle.Bold)};
            serverInfoBox.BackColor=WorkspaceTheme.Background;serverInfoBox.Font=Font;
            host.Controls.Add(serverInfoBox);host.Controls.Add(title);host.Controls.Add(close);detailsHost=host;
            }
            workspace.Panel2.Controls.Add(detailsHost);
        }
        workspace.Panel2Collapsed=name.Length==0;chatToggle.Active=name=="chat";chatToggle.Invalidate();workspace.ResumeLayout();
    }
    private void ShowToolWindow(Form window)
    {
        if (!window.Visible) window.Show(this);
        if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
        window.Activate();
    }
    private void OpenConsoleWindow()
    {
        if(consoleWindow is null||consoleWindow.IsDisposed)
        {
            consoleWindow=new Form{Text="KiwiDX \u2014 Console Log",Size=new Size(850,420),BackColor=WorkspaceTheme.Background,ForeColor=WorkspaceTheme.Text,Font=Font};
            consoleWindow.Controls.Add(consoleBox);
            consoleWindow.FormClosing+=(_,e)=>{if(!closingWorkspace){e.Cancel=true;consoleWindow.Hide();}};
        }
        ShowToolWindow(consoleWindow); RefreshConsole();
    }
    private void DetachChat()
    {
        if (chatWindow is { IsDisposed: false, Visible: true }) { ShowToolWindow(chatWindow); return; }
        if(chatWindow is null||chatWindow.IsDisposed)
        {
            chatWindow=new Form{Text="KiwiDX \u2014 Community Chat",Size=new Size(440,700),MinimumSize=new Size(350,400),BackColor=WorkspaceTheme.Background,Font=Font};
            chatWindow.FormClosing+=(_,e)=>{if(!closingWorkspace){e.Cancel=true;chatWindow.Hide();ShowSidebar("chat");}};
        }
        ShowSidebar("");chatWindow.Controls.Add(chatPanel);ShowToolWindow(chatWindow);_ = communityChat.ActivateAsync();
    }
    private void OpenDisplaySettings()
    {
        if(displayWindow is not null&&!displayWindow.IsDisposed){ShowToolWindow(displayWindow);return;}
        displayWindow=new Form{Text="Display settings",ClientSize=new Size(440,240),FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.CenterParent,BackColor=WorkspaceTheme.Surface,ForeColor=WorkspaceTheme.Text,Font=Font};
        var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var levels=new FlowLayoutPanel{AutoSize=true};levels.Controls.AddRange(new Control[]{WorkspaceTheme.Label("WF Min (dB)"),wfMinBox,WorkspaceTheme.Label("WF Max (dB)"),wfMaxBox});flow.Controls.Add(levels);
        flow.Controls.Add(WorkspaceTheme.Label("Drag the waterfall to pan. Scroll to zoom."));
        var bands=new CheckBox { Text="Show band markers", AutoSize=true, Checked=waterfall.ShowBandLabels }; bands.CheckedChanged+=(_,_)=>{waterfall.ShowBandLabels=bands.Checked;waterfall.Invalidate();};flow.Controls.Add(bands);
        flow.Controls.Add(WorkspaceTheme.Label("Tune step (kHz)"));
        var steps=new FlowLayoutPanel{AutoSize=true};foreach(var delta in new[]{-100000d,-10000d,-1000d,1000d,10000d,100000d}){var button=new RadioButton{Text=$"{delta/1000:+0;-0}",Width=56,Height=32};button.Click+=(_,_)=>MoveDial(delta);steps.Controls.Add(button);}flow.Controls.Add(steps);
        displayWindow.Controls.Add(flow);displayWindow.FormClosing+=(_,e)=>{if(!closingWorkspace){e.Cancel=true;displayWindow.Hide();}};displayWindow.Show(this);
    }
}
