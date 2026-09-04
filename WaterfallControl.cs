using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KiwiDX;

public sealed class WaterfallControl : Control
{
    private const int BitmapWidth = 1200;
    private const int BitmapHeight = 500;
    private readonly Bitmap bitmap = new(BitmapWidth, BitmapHeight, PixelFormat.Format32bppArgb);
    private readonly int[] pixels = new int[BitmapWidth * BitmapHeight];
    private readonly int[] palette = new int[256];
    private readonly object sync = new();
    private double centerFrequency = 7_100_000;
    private const double MaximumFrequency = 30_000_000;
    private int zoomLevel = 11;
    private double span = MaximumFrequency / (1 << 11);
    private Point dragStart;
    private double dragFrequency;
    private bool moved;
    private double tunedFrequency = 7_100_000;
    private double passbandWidth = 2_400;
    private int dragEdge;
    private double dragPassbandWidth;
    private int minimumDb = -150;
    private int maximumDb = -50;
    private long receivedLines;
    private IReadOnlyList<FrequencyBookmark> bookmarks = Array.Empty<FrequencyBookmark>();
    private readonly List<(RectangleF Bounds, FrequencyBookmark Bookmark)> bookmarkHits = new();
    private readonly ToolTip bookmarkToolTip = new();
    private readonly ContextMenuStrip dialContextMenu = new();
    private string currentBookmarkToolTip = "";
    private static readonly (double Low, double High, string Label, bool Broadcast)[] FrequencyBands =
    {
        (135_700, 137_800, "2200m", false), (472_000, 479_000, "630m", false),
        (1_800_000, 2_000_000, "160m", false), (2_300_000, 2_495_000, "120m BC", true),
        (3_200_000, 3_400_000, "90m BC", true), (3_500_000, 4_000_000, "80m", false),
        (3_900_000, 4_000_000, "75m BC", true), (4_750_000, 5_060_000, "60m BC", true),
        (5_330_500, 5_406_400, "60m", false), (5_900_000, 6_200_000, "49m BC", true),
        (7_000_000, 7_300_000, "40m", false), (7_200_000, 7_600_000, "41m BC", true),
        (9_400_000, 9_900_000, "31m BC", true), (10_100_000, 10_150_000, "30m", false),
        (11_600_000, 12_100_000, "25m BC", true), (13_570_000, 13_870_000, "22m BC", true),
        (14_000_000, 14_350_000, "20m", false), (15_100_000, 15_800_000, "19m BC", true),
        (17_480_000, 17_900_000, "16m BC", true), (18_068_000, 18_168_000, "17m", false),
        (18_900_000, 19_020_000, "15m BC", true), (21_000_000, 21_450_000, "15m", false),
        (21_450_000, 21_850_000, "13m BC", true), (24_890_000, 24_990_000, "12m", false),
        (25_600_000, 26_100_000, "11m BC", true), (28_000_000, 29_700_000, "10m", false)
    };
    public event EventHandler<double>? CursorChangedByUser;
    public event EventHandler<double>? CursorMoved;
    public event EventHandler<int>? BandwidthChanged;
    public event EventHandler<(double center, double span)>? ViewChanged;
    public event EventHandler<double>? AddBookmarkRequested;
    public double TunedFrequency => tunedFrequency;

    public void ZoomIn() => SetZoom(zoomLevel + 1);
    public void ZoomOut() => SetZoom(zoomLevel - 1);
    public void SetZoomLevel(int zoom) => SetZoom(zoom);
    public void PanTo(double center)
    {
        centerFrequency = Math.Clamp(center, span / 2, MaximumFrequency - span / 2);
        RaiseViewChanged();
        Invalidate();
    }
    public void CenterOnTune() { centerFrequency = tunedFrequency; RaiseViewChanged(); Invalidate(); }
    public void SetDbRange(int minimum, int maximum) { minimumDb = minimum; maximumDb = Math.Max(minimum + 1, maximum); BuildPalette(); Invalidate(); }
    public void SetTunedFrequency(double frequency)
    {
        tunedFrequency = Math.Clamp(frequency, 10_000, MaximumFrequency);
        var left = centerFrequency - span / 2;
        var right = centerFrequency + span / 2;
        if (tunedFrequency < left || tunedFrequency > right)
        {
            centerFrequency = Math.Clamp(tunedFrequency, span / 2, MaximumFrequency - span / 2);
            RaiseViewChanged();
        }
        Invalidate();
    }
    public void SetPassbandWidth(int bandwidth) { passbandWidth = Math.Clamp(bandwidth, 50, 12_000); Invalidate(); }
    internal void SetBookmarks(IReadOnlyList<FrequencyBookmark> items) { bookmarks = items.ToArray(); Invalidate(); }
    public void SetRadioState(double center, double span, double tuned, double passband)
    {
        centerFrequency = center;
        zoomLevel = Math.Clamp((int)Math.Round(Math.Log(MaximumFrequency / span, 2)), 0, 14);
        this.span = MaximumFrequency / (1 << zoomLevel);
        tunedFrequency = tuned;
        passbandWidth = Math.Clamp(passband, 50, this.span * 0.9);
        Invalidate();
    }

    public WaterfallControl()
    {
        DoubleBuffered = true;
        BackColor = Color.Black;
        Dock = DockStyle.Fill;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseWheel += OnMouseWheel;
        dialContextMenu.Items.Add("Add Bookmark", null, (_, _) => AddBookmarkRequested?.Invoke(this, tunedFrequency));
        BuildPalette();
    }

    public void AddLine(byte[] line)
    {
        if (line.Length == 0) return;
        receivedLines++;
        lock (sync)
        {
            Array.Copy(pixels, BitmapWidth, pixels, 0, pixels.Length - BitmapWidth);
            var row = pixels.Length - BitmapWidth;
            for (var x = 0; x < BitmapWidth; x++)
                pixels[row + x] = palette[line[(int)((long)x * line.Length / BitmapWidth)]];
            var data = bitmap.LockBits(new Rectangle(0, 0, BitmapWidth, BitmapHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        lock (sync) e.Graphics.DrawImage(bitmap, ClientRectangle);
        DrawBandMap(e.Graphics);
        DrawFrequencyScale(e.Graphics);
        DrawBookmarks(e.Graphics);
        using var font = new Font(Font.FontFamily, 9);
        var zoomText = $"Z{zoomLevel}  •  span {span / 1_000:0.###} kHz";
        var zoomSize = e.Graphics.MeasureString(zoomText, font);
        e.Graphics.DrawString(zoomText, font, Brushes.Gold, Math.Max(8, (ClientSize.Width - zoomSize.Width) / 2), 27);
        if (receivedLines == 0)
        {
            const string message = "Waiting for waterfall data...";
            var size = e.Graphics.MeasureString(message, font);
            e.Graphics.DrawString(message, font, Brushes.LightGray, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var viewLeft = centerFrequency - span / 2;
            var carrierX = (tunedFrequency - viewLeft) / span * ClientSize.Width;
            if (Math.Abs(e.X - carrierX) <= 10)
                dialContextMenu.Show(this, e.Location);
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        var left = centerFrequency - span / 2;
        var passbandLeft = (float)((tunedFrequency - passbandWidth / 2 - left) / span * ClientSize.Width);
        var passbandRight = (float)((tunedFrequency + passbandWidth / 2 - left) / span * ClientSize.Width);
        dragEdge = Math.Abs(e.X - passbandLeft) < 10 ? -1 : Math.Abs(e.X - passbandRight) < 10 ? 1 : 0;
        dragStart = e.Location; dragFrequency = centerFrequency; dragPassbandWidth = passbandWidth; moved = false;
    }
    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        var frequency = centerFrequency + (e.X - ClientSize.Width / 2) * span / Math.Max(1, ClientSize.Width);
        CursorMoved?.Invoke(this, frequency);
        var hovered = bookmarkHits.LastOrDefault(item => item.Bounds.Contains(e.Location)).Bookmark;
        var tooltip = hovered is null ? "" : string.IsNullOrWhiteSpace(hovered.Description) ? hovered.Name : hovered.Description;
        if (tooltip != currentBookmarkToolTip)
        {
            currentBookmarkToolTip = tooltip;
            bookmarkToolTip.SetToolTip(this, tooltip);
        }
        if (dragStart != Point.Empty && e.Button == MouseButtons.Left)
        {
            moved = moved || Math.Abs(e.X - dragStart.X) > 3;
            var delta = (e.X - dragStart.X) * span / Math.Max(1, ClientSize.Width);
            if (dragEdge != 0)
            {
                passbandWidth = Math.Clamp(dragPassbandWidth + dragEdge * delta * 2, 50, 12_000);
                BandwidthChanged?.Invoke(this, (int)passbandWidth);
            }
            else
            {
                centerFrequency = dragFrequency - delta;
                centerFrequency = Math.Clamp(centerFrequency, span / 2, MaximumFrequency - span / 2);
                RaiseViewChanged();
            }
            Invalidate();
        }
    }
    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && dragStart != Point.Empty && !moved)
        {
            var frequency = centerFrequency + (e.X - ClientSize.Width / 2) * span / Math.Max(1, ClientSize.Width);
            tunedFrequency = frequency;
            CursorChangedByUser?.Invoke(this, frequency);
        }
        dragStart = Point.Empty;
        dragEdge = 0;
    }
    private void DrawBandMap(Graphics graphics)
    {
        var left = centerFrequency - span / 2;
        const int stripHeight = 30;
        var stripTop = ClientSize.Height - stripHeight;
        using (var stripBrush = new SolidBrush(Color.FromArgb(215, 12, 17, 21))) graphics.FillRectangle(stripBrush, 0, stripTop, ClientSize.Width, stripHeight);
        foreach (var band in FrequencyBands)
        {
            var x1 = (float)((band.Low - left) / span * ClientSize.Width);
            var x2 = (float)((band.High - left) / span * ClientSize.Width);
            if (x2 < 0 || x1 > ClientSize.Width) continue;
            x1 = Math.Max(0, x1); x2 = Math.Min(ClientSize.Width, x2);
            var active = tunedFrequency >= band.Low && tunedFrequency <= band.High;
            var color = active ? Color.Gold : band.Broadcast ? Color.FromArgb(75, 125, 190) : Color.FromArgb(55, 155, 95);
            using var brush = new SolidBrush(Color.FromArgb(active ? 230 : 185, color));
            using var outline = new Pen(active ? Color.Yellow : Color.FromArgb(210, color), active ? 2f : 1f);
            var width = Math.Max(2, x2 - x1);
            graphics.FillRectangle(brush, x1, stripTop + 2, width, stripHeight - 4);
            graphics.DrawRectangle(outline, x1, stripTop + 2, width, stripHeight - 5);
            var labelSize = graphics.MeasureString(band.Label, Font);
            if (width >= labelSize.Width + 4) graphics.DrawString(band.Label, Font, active ? Brushes.Black : Brushes.White, x1 + 2, stripTop + 7);
        }
        var carrierX = (float)((tunedFrequency - left) / span * ClientSize.Width);
        if (carrierX >= 0 && carrierX <= ClientSize.Width)
        {
            using var carrierPen = new Pen(Color.Yellow, 2);
            graphics.DrawLine(carrierPen, carrierX, 24, carrierX, stripTop);
            var dialText = $"{tunedFrequency / 1_000_000:0.000000}";
            var labelX = Math.Clamp(carrierX + 4, 2, Math.Max(2, ClientSize.Width - 82));
            graphics.DrawString(dialText, Font, Brushes.Yellow, labelX, 28);
            var passband = Math.Min(span * 0.45, passbandWidth);
            var passbandLeft = (float)((tunedFrequency - passband / 2 - left) / span * ClientSize.Width);
            var passbandRight = (float)((tunedFrequency + passband / 2 - left) / span * ClientSize.Width);
            using var passbandPen = new Pen(Color.Lime, 2);
            graphics.DrawLine(passbandPen, passbandLeft, 24, passbandLeft, stripTop);
            graphics.DrawLine(passbandPen, passbandRight, 24, passbandRight, stripTop);
        }
    }

    private void DrawFrequencyScale(Graphics graphics)
    {
        const int scaleHeight = 24;
        using var background = new SolidBrush(Color.FromArgb(220, 10, 15, 19));
        graphics.FillRectangle(background, 0, 0, ClientSize.Width, scaleHeight);
        using var linePen = new Pen(Color.FromArgb(210, 210, 220));
        using var minorPen = new Pen(Color.FromArgb(120, 180, 190));
        using var font = new Font(Font.FontFamily, 8);
        var leftKhz = (centerFrequency - span / 2) / 1_000d;
        var rightKhz = (centerFrequency + span / 2) / 1_000d;
        var targetStep = Math.Max(0.001, span / 1_000d * 100d / Math.Max(1, ClientSize.Width));
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(targetStep)));
        var normalized = targetStep / magnitude;
        var stepKhz = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude;
        var first = Math.Ceiling(leftKhz / stepKhz) * stepKhz;
        for (var value = first; value <= rightKhz + stepKhz * 0.001; value += stepKhz)
        {
            var x = (float)((value - leftKhz) / (rightKhz - leftKhz) * ClientSize.Width);
            graphics.DrawLine(linePen, x, 0, x, scaleHeight);
            var label = value.ToString(stepKhz < 1 ? "0.000" : stepKhz < 10 ? "0.0" : "0");
            var size = graphics.MeasureString(label, font);
            graphics.DrawString(label, font, Brushes.White, Math.Clamp(x - size.Width / 2, 1, Math.Max(1, ClientSize.Width - size.Width - 1)), 2);
            var minorX = (float)((value + stepKhz / 2 - leftKhz) / (rightKhz - leftKhz) * ClientSize.Width);
            if (minorX >= 0 && minorX <= ClientSize.Width) graphics.DrawLine(minorPen, minorX, scaleHeight - 7, minorX, scaleHeight);
        }
        graphics.DrawString("kHz", font, Brushes.Gold, 3, scaleHeight - 12);
    }

    private void DrawBookmarks(Graphics graphics)
    {
        bookmarkHits.Clear();
        var left = centerFrequency - span / 2;
        using var font = new Font(Font.FontFamily, 8, FontStyle.Bold);
        var visibleIndex = 0;
        foreach (var bookmark in bookmarks.Where(item => item.Visible && item.FrequencyHz >= left && item.FrequencyHz <= left + span).OrderBy(item => item.FrequencyHz))
        {
            var x = (float)((bookmark.FrequencyHz - left) / span * ClientSize.Width);
            var row = visibleIndex++ % 2;
            var top = 47 + row * 22;
            var size = graphics.MeasureString(bookmark.Name, font);
            var width = Math.Max(28, size.Width + 8);
            var labelX = Math.Clamp(x - width / 2, 1, Math.Max(1, ClientSize.Width - width - 1));
            var bounds = new RectangleF(labelX, top, width, 18);
            using var fill = new SolidBrush(Color.FromArgb(225, bookmark.Color));
            using var border = new Pen(Color.White, 1);
            using var marker = new Pen(bookmark.Color, 2);
            graphics.DrawLine(marker, x, 24, x, top);
            graphics.FillPolygon(fill, new[] { new PointF(x - 5, 24), new PointF(x + 5, 24), new PointF(x, 31) });
            graphics.FillRectangle(fill, bounds);
            graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var brightness = bookmark.Color.GetBrightness();
            graphics.DrawString(bookmark.Name, font, brightness > 0.55f ? Brushes.Black : Brushes.White, bounds.X + 4, bounds.Y + 2);
            bookmarkHits.Add((bounds, bookmark));
        }
    }
    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        SetZoom(zoomLevel + (e.Delta > 0 ? 1 : -1));
    }
    private void SetZoom(int zoom)
    {
        var nextZoom = Math.Clamp(zoom, 0, 14);
        if (nextZoom == zoomLevel) return;

        var previousCenter = centerFrequency;
        var previousSpan = span;
        zoomLevel = nextZoom;
        span = MaximumFrequency / (1 << zoomLevel);
        centerFrequency = Math.Clamp(tunedFrequency, span / 2, MaximumFrequency - span / 2);
        ReprojectWaterfallHistory(previousCenter, previousSpan, centerFrequency, span);
        RaiseViewChanged();
        Invalidate();
    }

    private void ReprojectWaterfallHistory(double oldCenter, double oldSpan, double newCenter, double newSpan)
    {
        lock (sync)
        {
            var source = (int[])pixels.Clone();
            var oldLeft = oldCenter - oldSpan / 2;
            var newLeft = newCenter - newSpan / 2;
            var black = Color.Black.ToArgb();
            for (var x = 0; x < BitmapWidth; x++)
            {
                var frequency = newLeft + (x + 0.5) * newSpan / BitmapWidth;
                var sourceX = (int)Math.Floor((frequency - oldLeft) / oldSpan * BitmapWidth);
                for (var y = 0; y < BitmapHeight; y++)
                    pixels[y * BitmapWidth + x] = sourceX >= 0 && sourceX < BitmapWidth ? source[y * BitmapWidth + sourceX] : black;
            }

            var data = bitmap.LockBits(new Rectangle(0, 0, BitmapWidth, BitmapHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
        }
    }
    private void RaiseViewChanged() => ViewChanged?.Invoke(this, (centerFrequency, span));
    private void BuildPalette()
    {
        for (var sample = 0; sample < palette.Length; sample++)
        {
            var db = sample - 255;
            var index = Math.Clamp((int)Math.Round((db - minimumDb) * 255d / (maximumDb - minimumDb)), 0, 255);
            Color color;
            if (index < 32) color = Color.FromArgb(0, 0, index * 255 / 31);
            else if (index < 72) color = Color.FromArgb(0, (index - 32) * 255 / 39, 255);
            else if (index < 96) color = Color.FromArgb(0, 255, 255 - (index - 72) * 255 / 23);
            else if (index < 116) color = Color.FromArgb((index - 96) * 255 / 19, 255, 0);
            else if (index < 184) color = Color.FromArgb(255, 255 - (index - 116) * 255 / 67, 0);
            else color = Color.FromArgb(255, 0, Math.Min(128, (index - 184) * 128 / 70));
            palette[sample] = color.ToArgb();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            dialContextMenu.Dispose();
            bookmarkToolTip.Dispose();
            bitmap.Dispose();
        }
        base.Dispose(disposing);
    }
}
