using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KiwiDX;

internal sealed class CommunityChatControl : UserControl
{
    private static readonly Uri Service = new("https://kiwidx.noakmilo.com/");
    private readonly TextBox nick = new() { Width = 115, MaxLength = 24 };
    private readonly TextBox composer = new() { Dock = DockStyle.Fill, MaxLength = 1500, PlaceholderText = "Message or /help" };
    private readonly Label status = new() { AutoSize = true, Text = "Disconnected", Padding = new Padding(0, 6, 0, 0) };
    private readonly RadioButton connection = new() { Width = 94, Height = 30, Text = "Connect" };
    private readonly RadioTabs tabs = new() { Dock = DockStyle.Fill };
    private readonly Panel content = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, Transcript> channels = new();
    private CommunityChatClient? client;
    private CancellationTokenSource? attempt;
    private bool firstIdentity;
    private string desiredNick = "";
    internal Func<Task<ReceiverShare?>>? GetReceiver { get; set; }
    internal Func<ReceiverShare, Task>? TuneReceiver { get; set; }
    private string ActiveChannel => tabs.SelectedTab?.Name ?? "#hamradio";

    public CommunityChatControl()
    {
        Dock = DockStyle.Fill;
        BackColor = WorkspaceTheme.Background;
        Font = new Font("Segoe UI", 10);
        ForeColor = WorkspaceTheme.Text;
        nick.Text = "ANON" + RandomNumberGenerator.GetInt32(1000, 10000);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        var title = new Label { Text = "Community Chat", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = WorkspaceTheme.Green, Font = new Font(Font.FontFamily, 14, FontStyle.Bold), Padding = new Padding(32,0,0,0) };
        title.Paint += (_, e) => WorkspaceTheme.Glyph(e.Graphics, RadioGlyph.Chat, new RectangleF(0,12,24,24), WorkspaceTheme.Green);
        status.AutoSize = false; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleRight; status.Font = new Font(Font.FontFamily,8); status.ForeColor = WorkspaceTheme.Muted;
        header.Controls.Add(title,0,0);header.Controls.Add(status,1,0);
        var setNick = new RadioButton { Text = "Set nick", Width = 78, Height = 30 };
        var account = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,28));account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        var userIcon = new Panel { Dock = DockStyle.Fill };
        userIcon.Paint += (_, e) => WorkspaceTheme.Glyph(e.Graphics,RadioGlyph.User,new RectangleF(2,6,20,20),WorkspaceTheme.Muted);
        nick.Dock = DockStyle.Fill; nick.Margin = new Padding(2,6,4,2); WorkspaceTheme.Style(nick);
        account.Controls.Add(userIcon,0,0);account.Controls.Add(nick,1,0);account.Controls.Add(setNick,2,0);account.Controls.Add(connection,3,0);
        setNick.Click += async (_, _) => await SendAsync("/nick " + nick.Text.Trim());
        connection.Click += async (_, _) => { if (attempt is not null || client?.Connected == true) Disconnect(); else await ActivateAsync(); };
        foreach (var name in new[] { "#hamradio", "#shortwave" })
        {
            var page = new RadioTabPage(name) { Name = name, BackColor = WorkspaceTheme.Background, Padding = new Padding(8,14,8,8) };
            var transcript = new Transcript();
            channels.Add(name, transcript);
            page.Controls.Add(transcript.View);
            tabs.TabPages.Add(page);
            transcript.View.MouseUp += async (_, e) =>
            {
                if (e.Button != MouseButtons.Left || TuneReceiver is null) return;
                var index = transcript.View.GetCharIndexFromPosition(e.Location);
                var link = transcript.Links.FirstOrDefault(l => index >= l.Start && index < l.End);
                if (link.Share is null) return;
                try { await TuneReceiver(link.Share); }
                catch (Exception) { Notice("Could not tune this receiver."); }
            };
        }
        tabs.SelectedIndexChanged += (_, _) => { if (tabs.SelectedTab is { } page) page.Text = page.Name; };
        content.Controls.Add(tabs);
        var send = new RadioButton { Text = "Send", Width = 82, Height = 36, Active = true };
        var paste = new RadioButton { Text = "Share RX", Glyph = RadioGlyph.Share, Width = 116, Height = 32 };
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0,4,0,0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        WorkspaceTheme.Style(composer); composer.Font = new Font(Font.FontFamily,11); composer.Margin = new Padding(2,7,2,3);
        footer.Controls.Add(composer,0,0);footer.Controls.Add(send,1,0);
        var shareRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };shareRow.Controls.Add(paste);
        composer.TextChanged += (_, _) => composer.UseSystemPasswordChar = Regex.IsMatch(composer.Text, @"^\s*/(login|register|nick\s+register)\b", RegexOptions.IgnoreCase);
        send.Click += async (_, _) => await SendDraftAsync();
        composer.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendDraftAsync(); } };
        paste.Click += async (_, _) =>
        {
            try
            {
                var receiver = GetReceiver is null ? null : await GetReceiver();
                if (receiver is null) { Notice("Connect to a receiver before sharing."); return; }
                composer.Text = receiver.Draft; composer.Focus(); composer.SelectionStart = composer.TextLength;
            }
            catch (Exception) { Notice("Could not read the current receiver."); }
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12,8,12,8), Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        layout.Controls.Add(header,0,0);layout.Controls.Add(content,0,1);layout.Controls.Add(account,0,2);layout.Controls.Add(footer,0,3);layout.Controls.Add(shareRow,0,4);
        Controls.Add(layout);
    }

    internal async Task ActivateAsync()
    {
        if (IsDisposed || attempt is not null || client?.Connected == true) return;
        var currentAttempt = new CancellationTokenSource();
        attempt = currentAttempt;
        connection.Text = "Cancel"; status.Text = "Verifying...";
        desiredNick = nick.Text.Trim(); firstIdentity = true;
        using var verification = new ChatVerificationControl();
        content.Controls.Add(verification); verification.BringToFront();
        CommunityChatClient? next = null;
        try
        {
            var token = await verification.VerifyAsync(currentAttempt.Token);
            currentAttempt.Token.ThrowIfCancellationRequested();
            status.Text = "Connecting...";
            next = new CommunityChatClient(Service);
            await next.ConnectAsync(token, currentAttempt.Token);
            currentAttempt.Token.ThrowIfCancellationRequested();
            client?.Dispose(); client = next;
            var connectedClient = next;
            next.Message += message => { if (!IsDisposed && client == connectedClient) Receive(message); };
            next.Disconnected += () =>
            {
                if (!IsDisposed && client == connectedClient) { status.Text = "Disconnected"; connection.Text = "Reconnect"; }
            };
            connection.Text = "Disconnect"; status.Text = "Connected";
            _ = next.ListenAsync();
        }
        catch (OperationCanceledException) { next?.Dispose(); }
        catch (Exception)
        {
            next?.Dispose();
            if (!IsDisposed && attempt == currentAttempt)
            {
                status.Text = "Connection failed";
                Notice("Could not connect or verify. Use Reconnect to retry. The server must include static/verify.html and verify.js.");
            }
        }
        finally
        {
            if (!IsDisposed) content.Controls.Remove(verification);
            if (attempt == currentAttempt)
            {
                attempt = null;
                if (!IsDisposed && client?.Connected != true) connection.Text = "Reconnect";
            }
            currentAttempt.Dispose();
        }
    }

    private void Disconnect()
    {
        attempt?.Cancel();
        var old = client; client = null; old?.Dispose();
        composer.Clear(); status.Text = "Disconnected"; connection.Text = "Reconnect";
    }
    private async Task SendDraftAsync()
    {
        var text = composer.Text.Trim();
        if (text.Length == 0) return;
        // Never locally echo commands or passwords, even on send failure.
        composer.Clear();
        await SendAsync(text);
    }
    private async Task SendAsync(string text)
    {
        if (client?.Connected != true) { Notice("Connect to chat first."); return; }
        try { await client.SendAsync(ActiveChannel, text); }
        catch (Exception) { Notice("Message could not be sent. Reconnect if needed."); }
    }
    private void Notice(string text) => channels[ActiveChannel].Add("[Private] " + text);
    private async void Receive(JsonElement message)
    {
        try
        {
            switch (message.GetProperty("type").GetString())
            {
                case "identity":
                    nick.Text = message.GetProperty("nick").GetString() ?? "";
                    status.Text = message.GetProperty("authenticated").GetBoolean() ? "Logged in: " + nick.Text : "Guest: " + nick.Text;
                    if (firstIdentity)
                    {
                        firstIdentity = false;
                        if (desiredNick.Length > 0 && desiredNick != nick.Text) await SendAsync("/nick " + desiredNick);
                    }
                    break;
                case "notice": Notice(message.GetProperty("text").GetString() ?? ""); break;
                case "history":
                    if (channels.TryGetValue(message.GetProperty("channel").GetString() ?? "", out var history))
                    {
                        history.Clear();
                        foreach (var entry in message.GetProperty("messages").EnumerateArray()) history.Add(FormatMessage(entry));
                    }
                    break;
                case "message":
                    var channel = message.GetProperty("channel").GetString() ?? "";
                    if (channels.TryGetValue(channel, out var transcript))
                    {
                        transcript.Add(FormatMessage(message));
                        if (channel != ActiveChannel) tabs.TabPages[channel]!.Text = channel + " *";
                    }
                    break;
            }
        }
        catch (Exception) { if (!IsDisposed) Notice("Invalid server response."); }
    }
    private static string FormatMessage(JsonElement entry) =>
        $"{DateTimeOffset.FromUnixTimeSeconds(entry.GetProperty("time").GetInt64()).LocalDateTime:HH:mm:ss} <{entry.GetProperty("nick").GetString()}> {entry.GetProperty("text").GetString()}";
    protected override void Dispose(bool disposing)
    {
        if (disposing) { attempt?.Cancel(); client?.Dispose(); client = null; }
        base.Dispose(disposing);
    }

    private sealed class Transcript
    {
        internal readonly RichTextBox View = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false, BackColor = WorkspaceTheme.Background, ForeColor = WorkspaceTheme.Text, Font = new Font("Segoe UI", 10.5f), BorderStyle = BorderStyle.None };
        private readonly Queue<string> lines = new();
        internal readonly List<(int Start, int End, ReceiverShare? Share)> Links = new();
        internal void Clear() { lines.Clear(); Links.Clear(); View.Clear(); }
        internal void Add(string text)
        {
            lines.Enqueue(text);
            if (lines.Count > 500)
            {
                lines.Dequeue(); View.Clear(); Links.Clear();
                foreach (var line in lines) Append(line);
            }
            else Append(text);
            View.SelectionStart = View.TextLength; View.SelectionLength = 0; View.ScrollToCaret();
        }
        private void Append(string text)
        {
            int offset = 0;
            int close = text.IndexOf('>');
            int open = text.IndexOf('<');
            if (open > 0 && close > open)
            {
                View.SelectionStart = View.TextLength; View.SelectionColor = WorkspaceTheme.Muted;
                View.AppendText(text[..open]);
                View.SelectionStart = View.TextLength; View.SelectionColor = WorkspaceTheme.Text;
                using var bold = new Font(View.Font, FontStyle.Bold); View.SelectionFont = bold;
                View.AppendText(text[(open+1)..close] + "  ");
                View.SelectionStart = View.TextLength; View.SelectionFont = View.Font;
                offset = close + 1;
            }
            foreach (Match match in Regex.Matches(text, @"kiwidx://tune\?[^\s]+"))
            {
                if (match.Index < offset) continue;
                View.AppendText(text[offset..match.Index]);
                var share = ReceiverShare.Parse(match.Value);
                if (share is null) View.AppendText(match.Value);
                else
                {
                    int start = View.TextLength;
                    View.SelectionStart = start;
                    View.SelectionColor = Color.LightSkyBlue;
                    using var font = new Font(View.Font, FontStyle.Underline);
                    View.SelectionFont = font;
                    View.AppendText($"{new Uri(share.Url).Host} \u00B7 {share.FrequencyHz / 1_000_000:0.000} MHz \u00B7 {share.Mode}");
                    Links.Add((start, View.TextLength, share));
                    View.SelectionStart = View.TextLength;
                    View.SelectionColor = View.ForeColor; View.SelectionFont = View.Font;
                }
                offset = match.Index + match.Length;
            }
            View.AppendText(text[offset..] + Environment.NewLine + Environment.NewLine);
        }
    }
}
