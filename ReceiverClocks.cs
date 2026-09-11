using System.Globalization;

namespace KiwiDX;

// Draw the digits directly so the digital face does not require an installed font.
internal sealed class ReceiverClocks : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private TimeSpan? receiverTime;
    private long receivedAt;
    public TimeZoneInfo? ReceiverZone { get; set; }

    public ReceiverClocks()
    {
        DoubleBuffered = true;
        Size = new Size(300, 30);
        timer.Tick += (_, _) => Invalidate();
        timer.Start();
    }

    public void SetReceiverTime(TimeSpan? value)
    {
        receiverTime = value;
        receivedAt = Environment.TickCount64;
        Invalidate();
    }

    internal string GetReceiverTime(DateTimeOffset now)
    {
        if (ReceiverZone is not null)
            return TimeZoneInfo.ConvertTime(now, ReceiverZone).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - receivedAt);
        return receiverTime.HasValue && elapsed.TotalSeconds < 30
            ? DateTime.Today.Add(receiverTime.Value + elapsed).ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : "Not available";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var now = DateTimeOffset.UtcNow;
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - receivedAt);
        var rx = ReceiverZone is not null ? TimeZoneInfo.ConvertTime(now, ReceiverZone).ToString("HH:mm:ss")
            : receiverTime.HasValue && elapsed.TotalSeconds < 30 ? DateTime.Today.Add(receiverTime.Value + elapsed).ToString("HH:mm:ss") : "--:--:--";
        var text = $"Local  {now.ToLocalTime():HH:mm:ss}     RX  {rx}     UTC  {now:HH:mm:ss}";
        TextRenderer.DrawText(e.Graphics, text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        AccessibleName = text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
