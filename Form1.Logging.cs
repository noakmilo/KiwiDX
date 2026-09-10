using System.Globalization;

namespace KiwiDX;

public partial class Form1
{
    private async void AddListeningLog(bool ham, double frequencyHz)
    {
        if (!client.IsConnected && webReceiver is null)
        { MessageBox.Show(this, "Connect to a receiver before adding a listening log.", "Logging"); return; }
        if (webReceiver is not null)
        {
            await webReceiver.ReadStateAsync();
            if (webReceiver is null || !webReceiver.Ready) { MessageBox.Show(this, "Wait for the receiver to initialize before logging.", "Logging"); return; }
            frequencyHz = webReceiver.FrequencyHz;
        }
        var now = DateTimeOffset.UtcNow;
        var band = BandRanges.Where(b => frequencyHz >= b.LowHz && frequencyHz <= b.HighHz)
            .OrderByDescending(b => b.Band.Name.Contains(ham ? "Amateur" : "Broadcast"))
            .ThenBy(b => Math.Abs(b.Band.FrequencyMHz * 1_000_000 - frequencyHz))
            .Select(b => b.Band.Name).FirstOrDefault() ?? "Outside preset bands";
        var entry = new ListeningLog {
            IsHamRadio = ham, Server = currentServerUrl, Antenna = currentServerAntenna, LocalTime = now.ToLocalTime(), UtcTime = now,
            RxTime = clocks.GetReceiverTime(now), FrequencyHz = frequencyHz, Band = band, Mode = webReceiver?.Mode ?? modeBox.Text.ToUpperInvariant()
        };
        using var editor = new ListeningLogEditorForm(entry, log => ListeningLogStore.Append(UserData.PathFor("listening-logs.json"), log));
        if (editor.ShowDialog(this) == DialogResult.OK) statusLabel.Text = "Listening log saved.";
    }

    private void ShowListeningLogs()
    {
        try
        {
            using var logs = new ListeningLogsForm(ListeningLogStore.Read(UserData.PathFor("listening-logs.json")));
            logs.ShowDialog(this);
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not read listening logs.\n\n" + ex.Message, "Logging", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
