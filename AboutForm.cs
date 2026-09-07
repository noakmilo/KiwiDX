using System.Diagnostics;

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
        ClientSize = new Size(430, 280);
        BackColor = Color.FromArgb(30, 36, 42);
        ForeColor = Color.Gainsboro;

        var icon = new PictureBox { Image = Icon?.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(20, 20), Location = new Point(24, 25) };
        var details = new Label { AutoSize = true, Location = new Point(58, 23), Text = "KiwiDX, 2026 v0.1.51\r\n\r\nBuilt by: Kmilo Noa\r\nContact: noakmilo90@gmail.com" };
        var close = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(78, 28), Location = new Point(328, 232) };
        AcceptButton = close;
        CancelButton = close;
        var github = new LinkLabel { AutoSize = true, Location = new Point(58, 110), Text = "github.com/noakmilo/KiwiDX", LinkColor = Color.DeepSkyBlue };
        github.LinkClicked += (_, _) => OpenLink("https://github.com/noakmilo/KiwiDX");
        var support = new Label { AutoSize = true, Location = new Point(24, 150), Text = "Enjoying KiwiDX? Support its development!" };
        var donate = new Button { Text = "Donate", Location = new Point(24, 180), Size = new Size(100, 32) };
        donate.Click += (_, _) => OpenLink("https://www.paypal.com/paypalme/soscubamap");
        Controls.AddRange(new Control[] { icon, details, github, support, donate, close });
    }

    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open browser", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
