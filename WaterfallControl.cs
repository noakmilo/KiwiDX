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
    private double MaximumFrequency = 30_000_000;
    private double zoomFrequencyRange = 30_000_000;
    private int zoomLevel = 11;
    private double span = 30_000_000d / (1 << 11);
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
    public bool ShowBandLabels { get; set; }
    private byte[] latestSpectrum = Array.Empty<byte>();
    private int SpectrumHeight => Math.Clamp(ClientSize.Height / 4, 90, 180);
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
    public event EventHandler<double>? AddHamLogRequested;
    public event EventHandler<double>? AddShortwaveLogRequested;
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
    public void ClearHistory()
    {
        lock(sync){Array.Fill(pixels,Color.Black.ToArgb());using var g=Graphics.FromImage(bitmap);g.Clear(Color.Black);latestSpectrum=Array.Empty<byte>();receivedLines=0;}
        Invalidate();
    }
    public void SetFrequencyLimit(double maximum, double? zoomRange = null) { MaximumFrequency = Math.Max(30_000_000,maximum); zoomFrequencyRange = zoomRange ?? MaximumFrequency; }
    public double VisibleSpan => span;
    public double CenterFrequency => centerFrequency;
    public void SetRadioState(double center, double span, double tuned, double passband)
    {
        centerFrequency = center;
        zoomLevel = Math.Clamp((int)Math.Round(Math.Log(zoomFrequencyRange / span, 2)), 0, 14);
        this.span = zoomFrequencyRange / (1 << zoomLevel);
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
        dialContextMenu.Items.Add("Log Ham Radio...", null, (_, _) => AddHamLogRequested?.Invoke(this, tunedFrequency));
        dialContextMenu.Items.Add("Log Shortwave Listening...", null, (_, _) => AddShortwaveLogRequested?.Invoke(this, tunedFrequency));
        BuildPalette();
    }

    public void AddLine(byte[] line)
    {
        if (line.Length == 0) return;
        receivedLines++;
        lock (sync)
        {
            latestSpectrum = (byte[])line.Clone();
            Array.Copy(pixels, 0, pixels, BitmapWidth, pixels.Length - BitmapWidth);
            const int row = 0;
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
        int top = SpectrumHeight;
        e.Graphics.Clear(Color.FromArgb(8,17,25));
        lock (sync) e.Graphics.DrawImage(bitmap,new Rectangle(0,top+24,ClientSize.Width,Math.Max(1,ClientSize.Height-top-24)));
        DrawBandMap(e.Graphics);
        DrawSpectrum(e.Graphics);
        var state=e.Graphics.Save();e.Graphics.TranslateTransform(0,top);DrawFrequencyScale(e.Graphics);e.Graphics.Restore(state);
        DrawBookmarks(e.Graphics);
        using var font = new Font(Font.FontFamily, 9);
        if (receivedLines == 0)
        {
            const string message = "Waiting for waterfall data...";
            var size = e.Graphics.MeasureString(message, font);
            e.Graphics.DrawString(message, font, Brushes.LightGray, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
        }
    }

    private void DrawSpectrum(Graphics g)
    {
        int height=SpectrumHeight;
        using(var background=new SolidBrush(Color.FromArgb(10,21,30)))g.FillRectangle(background,0,0,Width,height);
        using var grid=new Pen(Color.FromArgb(29,44,56));using var labels=new SolidBrush(Color.FromArgb(178,195,208));
        for(int i=1;i<5;i++){float y=i*height/5f;g.DrawLine(grid,0,y,Width,y);g.DrawString($"{maximumDb-(maximumDb-minimumDb)*i/5}",Font,labels,4,y-14);}
        for(int x=0;x<Width;x+=50)g.DrawLine(grid,x,0,x,height);
        byte[] samples;lock(sync)samples=latestSpectrum;
        if(samples.Length>1&&Width>1){var points=new PointF[Width];for(int x=0;x<Width;x++){double db=samples[(int)((long)x*samples.Length/Width)]-255;points[x]=new PointF(x,(float)(height-6-Math.Clamp((db-minimumDb)/(maximumDb-minimumDb),0,1)*(height-12)));}using var trace=new Pen(Color.FromArgb(142,236,143),1);g.DrawLines(trace,points);}
        float carrier=(float)((tunedFrequency-(centerFrequency-span/2))/span*Width);float band=(float)(passbandWidth/span*Width);
        using var fill=new SolidBrush(Color.FromArgb(55,95,177,235));g.FillRectangle(fill,carrier-band/2,0,band,height);
        using var edge=new Pen(Color.FromArgb(110,187,240));g.DrawLine(edge,carrier-band/2,0,carrier-band/2,height);g.DrawLine(edge,carrier+band/2,0,carrier+band/2,height);
        using var center=new Pen(Color.FromArgb(202,235,255));g.DrawLine(center,carrier,0,carrier,height);
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
        var stripTop = ClientSize.Height - (ShowBandLabels ? stripHeight : 0);
        if (ShowBandLabels)
        {
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
        }
        var carrierX = (float)((tunedFrequency - left) / span * ClientSize.Width);
        if (carrierX >= 0 && carrierX <= ClientSize.Width)
        {
            using var carrierPen = new Pen(Color.FromArgb(175,215,245), 1);
            graphics.DrawLine(carrierPen, carrierX, 24, carrierX, stripTop);
            var dialText = $"{tunedFrequency / 1_000_000:0.000000}";
            var labelX = Math.Clamp(carrierX + 4, 2, Math.Max(2, ClientSize.Width - 82));
            // Frequency is displayed in the tuning control above the spectrum.
            var passband = Math.Min(span * 0.45, passbandWidth);
            var passbandLeft = (float)((tunedFrequency - passband / 2 - left) / span * ClientSize.Width);
            var passbandRight = (float)((tunedFrequency + passband / 2 - left) / span * ClientSize.Width);
            using var passbandPen = new Pen(Color.FromArgb(90,140,185), 1f);
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
            var label = (value / 1000).ToString(stepKhz < 1 ? "0.000000" : stepKhz < 10 ? "0.0000" : "0.000");
            var size = graphics.MeasureString(label, font);
            graphics.DrawString(label, font, Brushes.White, Math.Clamp(x - size.Width / 2, 1, Math.Max(1, ClientSize.Width - size.Width - 1)), 2);
            var minorX = (float)((value + stepKhz / 2 - leftKhz) / (rightKhz - leftKhz) * ClientSize.Width);
            if (minorX >= 0 && minorX <= ClientSize.Width) graphics.DrawLine(minorPen, minorX, scaleHeight - 7, minorX, scaleHeight);
        }
        graphics.DrawString("MHz", font, Brushes.LightGray, Math.Max(0,Width-32), scaleHeight-12);
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
            var top = SpectrumHeight + 32 + row * 22;
            var size = graphics.MeasureString(bookmark.Name, font);
            var width = Math.Max(28, size.Width + 8);
            var labelX = Math.Clamp(x - width / 2, 1, Math.Max(1, ClientSize.Width - width - 1));
            var bounds = new RectangleF(labelX, top, width, 18);
            using var fill = new SolidBrush(Color.FromArgb(225, bookmark.Color));
            using var border = new Pen(Color.White, 1);
            using var marker = new Pen(bookmark.Color, 2);
            graphics.DrawLine(marker, x, SpectrumHeight + 24, x, top);
            graphics.FillPolygon(fill, new[] { new PointF(x - 5, SpectrumHeight + 24), new PointF(x + 5, SpectrumHeight + 24), new PointF(x, SpectrumHeight + 31) });
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
        span = zoomFrequencyRange / (1 << zoomLevel);
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
        var stops = new (int At, Color Color)[] { (0,Color.FromArgb(0,3,17)), (50,Color.FromArgb(0,15,86)), (115,Color.FromArgb(0,57,190)), (175,Color.FromArgb(20,188,235)), (218,Color.FromArgb(249,228,45)), (255,Color.FromArgb(255,255,221)) };
        for (int sample=0;sample<256;sample++)
        {
            double value=Math.Clamp((sample-255-minimumDb)*255d/(maximumDb-minimumDb),0,255);
            int i=0;while(i<stops.Length-2&&value>stops[i+1].At)i++;
            var a=stops[i];var b=stops[i+1];double t=(value-a.At)/(b.At-a.At);
            palette[sample]=Color.FromArgb((int)(a.Color.R+(b.Color.R-a.Color.R)*t),(int)(a.Color.G+(b.Color.G-a.Color.G)*t),(int)(a.Color.B+(b.Color.B-a.Color.B)*t)).ToArgb();
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
