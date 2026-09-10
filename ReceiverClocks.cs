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
        base.OnPaint(e);
        var now = DateTimeOffset.UtcNow;
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - receivedAt);
        var rx = ReceiverZone is not null
            ? TimeZoneInfo.ConvertTime(now, ReceiverZone).ToString("HH:mm", CultureInfo.InvariantCulture)
            : receiverTime.HasValue && elapsed.TotalSeconds < 30
                ? DateTime.Today.Add(receiverTime.Value + elapsed).ToString("HH:mm", CultureInfo.InvariantCulture) : "--:--";
        e.Graphics.ScaleTransform(DeviceDpi / 96f, DeviceDpi / 96f);
        e.Graphics.TranslateTransform(0, 2.25f);
        e.Graphics.ScaleTransform(0.75f, 0.75f);
        DrawClock(e.Graphics, 0, "Local time:", now.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), Color.LimeGreen);
        DrawClock(e.Graphics, 145, "RX:", rx, Color.Yellow);
        DrawClock(e.Graphics, 243, "UTC:", now.ToString("HH:mm", CultureInfo.InvariantCulture), Color.Red);
        AccessibleName = $"Local time {now.ToLocalTime():HH:mm}, RX {rx}, UTC {now:HH:mm}";
    }

    private void DrawClock(Graphics graphics, int x, string label, string time, Color color)
    {
        using var font = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Point);
        using var brush = new SolidBrush(color);
        graphics.DrawString(label, font, brush, x, 7);
        x += label == "Local time:" ? 69 : label == "UTC:" ? 35 : 26;
        int[] masks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        foreach (var digit in time)
        {
            if (digit == ':')
            {
                graphics.FillRectangle(brush, x + 1, 10, 3, 3);
                graphics.FillRectangle(brush, x + 1, 18, 3, 3);
                x += 7;
                continue;
            }
            var mask = digit == '-' ? 0x40 : masks[digit - '0'];
            Rectangle[] segments = { new(x + 2, 5, 8, 3), new(x + 9, 7, 3, 8), new(x + 9, 16, 3, 8),
                new(x + 2, 23, 8, 3), new(x, 16, 3, 8), new(x, 7, 3, 8), new(x + 2, 14, 8, 3) };
            for (var i = 0; i < segments.Length; i++)
                if ((mask & (1 << i)) != 0) graphics.FillRectangle(brush, segments[i]);
            x += 15;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
