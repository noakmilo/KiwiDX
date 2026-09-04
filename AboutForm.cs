namespace KiwiDX;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About KiwiDX";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(350, 175);
        BackColor = Color.FromArgb(30, 36, 42);
        ForeColor = Color.Gainsboro;

        var icon = new PictureBox { Image = Icon?.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(20, 20), Location = new Point(24, 25) };
        var details = new Label { AutoSize = true, Location = new Point(58, 23), Text = "KiwiDX, 2026 v0.1.47\r\n\r\nBuilt by: Kmilo Noa\r\nContact: noakmilo90@gmail.com" };
        var close = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(78, 28), Location = new Point(248, 125) };
        AcceptButton = close;
        CancelButton = close;
        Controls.AddRange(new Control[] { icon, details, close });
    }
}
