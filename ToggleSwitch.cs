using System.Drawing.Drawing2D;

namespace KiwiDX;

internal sealed class ToggleSwitch : CheckBox
{
    internal bool ShowState { get; set; } = true;
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        AutoSize = true;
        Margin = new Padding(3, 1, 12, 1);
        UseVisualStyleBackColor = false;
    }

    public override Size GetPreferredSize(Size proposedSize)
        => new(TextRenderer.MeasureText(Text, Font).Width + (ShowState ? 66 : 42), 24);

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int y = (Height - 16) / 2;
        using var track = new SolidBrush(Checked ? Color.FromArgb(30, 145, 80) : Color.FromArgb(80, 88, 96));
        e.Graphics.FillEllipse(track, 0, y, 16, 16);
        e.Graphics.FillRectangle(track, 10, y, 16, 16);
        e.Graphics.FillEllipse(track, 20, y, 16, 16);
        e.Graphics.FillEllipse(Brushes.White, Checked ? 18 : 2, y + 2, 12, 12);
        TextRenderer.DrawText(e.Graphics, Text + (ShowState ? (Checked ? "  On" : "  Off") : ""), Font,
            new Rectangle(38, 0, Width - 38, Height), Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
