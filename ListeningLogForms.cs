using System.Globalization;

namespace KiwiDX;

internal sealed class ListeningLogEditorForm : Form
{
    internal ListeningLogEditorForm(ListeningLog entry, Action<ListeningLog> save, bool editing = false)
    {
        entry = System.Text.Json.JsonSerializer.Deserialize<ListeningLog>(System.Text.Json.JsonSerializer.Serialize(entry))!;
        Text = entry.IsHamRadio ? "Log Ham Radio" : "Log Shortwave Listening";
        if (editing) Text = "Edit - " + Text;
        else if (entry.IsManual) Text = "Manual - " + Text;
        bool editableReception = editing || entry.IsManual;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(650, 740);
        MinimumSize = new Size(540, 540);
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(fields);
        TextBox Field(string label, string value = "", bool readOnly = false, bool multiline = false, bool bold = false)
        {
            var box = new TextBox { Text = value, ReadOnly = readOnly, Dock = DockStyle.Fill, Multiline = multiline, Height = multiline ? 72 : 25, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Font = bold ? new Font(Font, FontStyle.Bold) : Font, Padding = new Padding(0, 6, 0, 6) }, 0, row);
            fields.Controls.Add(box, 1, row);
            return box;
        }
        var server = Field("Rx", entry.Server, !editableReception);
        var antenna = Field("Antenna", entry.Antenna, !editableReception);
        server.PlaceholderText = "e.g. XHDATA d-808";
        antenna.PlaceholderText = "e.g. Telescopic whip, long wire, or dipole";
        var local = Field("Local time", entry.LocalTime.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), !editableReception);
        var rx = Field("RX time", entry.RxTime, !editableReception);
        var utc = Field("UTC time", entry.UtcTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), !editableReception);
        var frequency = Field("Frequency (kHz)", entry.FrequencyHz > 0 ? (entry.FrequencyHz / 1000).ToString("0.000", CultureInfo.InvariantCulture) : "", !editableReception);
        var band = Field("Band", entry.Band, !editableReception);
        var mode = entry.IsHamRadio ? Field("Mode", entry.Mode, !editableReception) : null;
        var station = Field(entry.IsHamRadio ? "Callsign" : "Station callsign", entry.Station);
        frequency.PlaceholderText = "e.g. 7100.000";
        band.PlaceholderText = entry.IsHamRadio ? "e.g. 40m" : "e.g. 49m";
        station.PlaceholderText = entry.IsHamRadio ? "e.g. W1AW" : "e.g. Radio Romania International";
        if (mode is not null) mode.PlaceholderText = "e.g. AM, USB, LSB, CW";
        TextBox? qth = null, rst = null, program = null;
        var ratings = new List<TextBox>();
        if (entry.IsHamRadio) { qth = Field("QTH", entry.Qth); rst = Field("RST", entry.Rst); }
        else
        {
            int headingRow = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading = new Label { Text = "SIMPO CODE:", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 10, 0, 6) };
            fields.Controls.Add(heading, 0, headingRow);
            fields.SetColumnSpan(heading, 2);
            foreach (var ratingField in new[] { ("S (Signal)", entry.S), ("I (Interference)", entry.I), ("N (Noise)", entry.N), ("P (Propagation)", entry.P), ("O (Overall)", entry.O) })
            {
                var rating = Field(ratingField.Item1, ratingField.Item2, bold: true);
                rating.PlaceholderText = "1-5";
                rating.MaxLength = 1;
                ratings.Add(rating);
            }
            program = Field("Program heard", entry.Program);
            program.PlaceholderText = "e.g. World news in English";
        }
        var comments = Field("Comments", entry.Comments, multiline: true);
        comments.PlaceholderText = "e.g. Fading, interference, and listening conditions";
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var saveButton = new Button { Text = "Save log", AutoSize = true };
        saveButton.Click += (_, _) =>
        {
            if (editableReception)
            {
                if (!DateTimeOffset.TryParseExact(local.Text.Trim(), "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localTime)
                    || !DateTimeOffset.TryParseExact(utc.Text.Trim(), "yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utcTime))
                { MessageBox.Show(this, "Use yyyy-MM-dd HH:mm:ss +00:00 for local time and yyyy-MM-dd HH:mm:ss UTC for UTC time.", Text); return; }
                if (!double.TryParse(frequency.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var khz) || !double.IsFinite(khz * 1000) || khz <= 0)
                { MessageBox.Show(this, "Enter a valid frequency greater than 0 kHz.", Text); return; }
                if (string.IsNullOrWhiteSpace(server.Text)) { MessageBox.Show(this, "Enter the receiver in Rx.", Text); return; }
                entry.Antenna = antenna.Text.Trim();
                entry.Server = server.Text.Trim(); entry.LocalTime = localTime; entry.UtcTime = utcTime; entry.RxTime = rx.Text.Trim();
                entry.FrequencyHz = khz * 1000; entry.Band = band.Text.Trim(); entry.Mode = mode?.Text.Trim() ?? entry.Mode;
            }
            if (string.IsNullOrWhiteSpace(station.Text)) { MessageBox.Show(this, "Enter the station callsign.", Text); station.Focus(); return; }
            if (ratings.Any(box => box.Text.Length != 1 || box.Text[0] < '1' || box.Text[0] > '5'))
            { MessageBox.Show(this, "Enter a value from 1 to 5 in each SIMPO field.", Text); return; }
            entry.Station = station.Text.Trim(); entry.Qth = qth?.Text.Trim() ?? ""; entry.Rst = rst?.Text.Trim() ?? "";
            entry.Program = program?.Text.Trim() ?? ""; entry.Comments = comments.Text.Trim();
            if (ratings.Count == 5) { entry.S = ratings[0].Text; entry.I = ratings[1].Text; entry.N = ratings[2].Text; entry.P = ratings[3].Text; entry.O = ratings[4].Text; }
            try { save(entry); DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(this, "Could not save the log.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        buttons.Controls.AddRange(new Control[] { cancel, saveButton });
        Controls.Add(scroll); Controls.Add(buttons);
        AcceptButton = saveButton; CancelButton = cancel;
    }
}

internal sealed class ListeningLogsForm : Form
{
    internal ListeningLogsForm(List<ListeningLog> entries)
    {
        Text = "Logging"; StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1150, 650); MinimumSize = new Size(700, 400);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (bool ham in new[] { true, false })
        {
            var records = entries.Where(e => e.IsHamRadio == ham).OrderByDescending(e => e.UtcTime).ToList();
            var tab = new TabPage((ham ? "Ham Radio" : "Shortwave Listening") + $" ({records.Count})");
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            void Column(string name, string title) => grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = name, HeaderText = title, MinimumWidth = 65 });
            Column("Station", ham ? "Callsign" : "Station callsign"); Column("Server", "Rx"); Column("Antenna", "Antenna");
            Column("LocalTime", "Local time"); Column("RxTime", "RX time"); Column("UtcTime", "UTC time");
            Column("FrequencyHz", "Frequency (Hz)"); Column("Band", "Band");
            if (ham) { Column("Mode", "Mode"); Column("Qth", "QTH"); Column("Rst", "RST"); }
            else { Column("S", "S (Signal)"); Column("I", "I (Interference)"); Column("N", "N (Noise)"); Column("P", "P (Propagation)"); Column("O", "O (Overall)"); Column("Program", "Program heard"); }
            Column("Comments", "Comments");

            foreach (DataGridViewColumn col in grid.Columns)
                if (col.DataPropertyName is "LocalTime" or "UtcTime") col.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss zzz";
            grid.DataSource = records;
            grid.MultiSelect = false;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            var export = new Button { Text = "Export selected report (.txt)...", AutoSize = true, Enabled = records.Count > 0 };
            var edit = new Button { Text = "Edit selected report...", AutoSize = true, Enabled = records.Count > 0 };
            var delete = new Button { Text = "Delete selected report...", AutoSize = true, Enabled = records.Count > 0 };
            void RefreshRecords()
            {
                grid.DataSource = null;
                grid.DataSource = records;
                tab.Text = (ham ? "Ham Radio" : "Shortwave Listening") + $" ({records.Count})";
                export.Enabled = edit.Enabled = delete.Enabled = records.Count > 0;
            }
            var addManual = new Button { Text = "Add manual report...", AutoSize = true };
            addManual.Click += (_, _) =>
            {
                var now = DateTimeOffset.UtcNow;
                var manual = new ListeningLog { IsHamRadio = ham, IsManual = true, LocalTime = now.ToLocalTime(), UtcTime = now, RxTime = now.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) };
                using var editor = new ListeningLogEditorForm(manual, saved =>
                {
                    ListeningLogStore.Append(UserData.PathFor("listening-logs.json"), saved);
                    records.Insert(0, saved);
                });
                if (editor.ShowDialog(this) == DialogResult.OK) RefreshRecords();
            };
            edit.Click += (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not ListeningLog selected) return;
                using var editor = new ListeningLogEditorForm(selected, updated =>
                {
                    ListeningLogStore.Update(UserData.PathFor("listening-logs.json"), updated);
                    records[records.FindIndex(item => item.Id == updated.Id)] = updated;
                }, editing: true);
                if (editor.ShowDialog(this) == DialogResult.OK) RefreshRecords();
            };
            delete.Click += (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not ListeningLog selected) return;
                if (MessageBox.Show(this, $"Delete the report for {selected.Station} ({selected.UtcTime:yyyy-MM-dd HH:mm:ss} UTC)?", "Delete report",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                try
                {
                    ListeningLogStore.Delete(UserData.PathFor("listening-logs.json"), selected.Id);
                    records.Remove(selected);
                    RefreshRecords();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not delete the report.\n\n" + ex.Message, "Delete report", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            export.Click += (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not ListeningLog entry)
                {
                    MessageBox.Show(this, "Select a report to export.", "Export report");
                    return;
                }
                using var dialog = new SaveFileDialog
                {
                    Title = "Export report", Filter = "Text files (*.txt)|*.txt", DefaultExt = "txt", AddExtension = true,
                    FileName = $"KiwiDX_{(entry.IsHamRadio ? "HamRadio" : "Shortwave")}_{entry.UtcTime:yyyyMMdd_HHmmss}_{entry.Id:N}.txt"
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, entry.ToTextReport(), new System.Text.UTF8Encoding(true));
                    MessageBox.Show(this, "Report exported successfully.", "Export report");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not export the report.\n\n" + ex.Message, "Export report", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actions.Controls.AddRange(new Control[] { addManual, edit, delete, export });
            tab.Controls.Add(grid); tab.Controls.Add(actions); tabs.TabPages.Add(tab);
        }
        Controls.Add(tabs);
    }
}

