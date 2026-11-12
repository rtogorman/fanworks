using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class SourceSinkEditorDialog : DpiScaledForm
{
    private readonly IReadOnlyList<SavedLayoutNode> _boundaries;
    private readonly Dictionary<int, TextBox> _airflowInputs = new();
    private readonly Label _balanceStatus = new();
    private readonly Button _save = new();
    internal IReadOnlyDictionary<int, double> AirflowsCfm { get; private set; } =
        new Dictionary<int, double>();

    internal SourceSinkEditorDialog(IReadOnlyList<SavedLayoutNode> boundaries)
    {
        _boundaries = boundaries.OrderBy(node => node.Sequence).ToArray();
        // Room for every row up to about eight; more rows scroll with the slim scroll bar.
        FormKit.SetUp(this, "Source and Sink Editor", "SourceSinkEditorDialog",
            new Size(640, Math.Clamp(268 + _boundaries.Count * 38, 330, 600)));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22, 16, 22, 14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.Controls.Add(FormKit.Header("Source and sink airflows",
            "Edit the boundary flows so the total source airflow equals the total sink airflow."), 0, 0);

        _balanceStatus.Name = "SourceSinkBalanceStatusLabel";
        _balanceStatus.Dock = DockStyle.Fill;
        _balanceStatus.TextAlign = ContentAlignment.MiddleLeft;
        _balanceStatus.Font = new Font("Segoe UI Semibold", 9F);
        _balanceStatus.Padding = new Padding(12, 0, 12, 0);
        _balanceStatus.Margin = new Padding(0, 4, 0, 4);
        _balanceStatus.Resize += (_, _) =>
        {
            if (_balanceStatus.Width < 8 || _balanceStatus.Height < 8)
                return;
            using var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, _balanceStatus.Width, _balanceStatus.Height), 8);
            _balanceStatus.Region = new Region(path);
        };
        root.Controls.Add(_balanceStatus, 0, 1);

        // Column headings (fixed above the scrolling rows).
        var headings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = BackColor, Margin = Padding.Empty, Padding = new Padding(16, 0, 28, 0) };
        ConfigureColumns(headings);
        AddHeading(headings, "Type", 0, false);
        AddHeading(headings, "Item", 1, false);
        AddHeading(headings, "Airflow (cfm)", 2, true);
        AddHeading(headings, "Pressure (in. w.c.)", 3, true);
        root.Controls.Add(headings, 0, 2);

        var scroll = new SlimScrollArea { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var table = new TableLayoutPanel
        {
            Name = "SourceSinkRows",
            ColumnCount = 4,
            RowCount = _boundaries.Count,
            BackColor = Color.White,
            Padding = new Padding(16, 6, 16, 6),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
        ConfigureColumns(table);
        var row = 0;
        foreach (var boundary in _boundaries)
        {
            var source = string.Equals(boundary.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(boundary.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase);
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var pill = FormKit.Pill(source ? "Source" : "Sink",
                source ? Color.FromArgb(221, 236, 248) : Color.FromArgb(220, 243, 240),
                source ? Color.FromArgb(24, 92, 150) : Color.FromArgb(18, 112, 104));
            pill.Size = new Size(60, 22);
            pill.Anchor = AnchorStyles.Left;
            table.Controls.Add(pill, 0, row);
            table.Controls.Add(new Label
            {
                Text = boundary.Id, AutoSize = true, Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(34, 62, 88), Font = new Font("Segoe UI Semibold", 9F)
            }, 1, row);
            var input = new TextBox
            {
                Name = $"SourceSinkAirflow_{boundary.Sequence}",
                Text = UnitConversions.M3PerSToCfm(boundary.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture),
                Width = 104,
                Anchor = AnchorStyles.Right,
                TextAlign = HorizontalAlignment.Right
            };
            input.TextChanged += (_, _) => ValidateBalance();
            _airflowInputs[boundary.Sequence] = input;
            table.Controls.Add(input, 2, row);
            table.Controls.Add(new Label
            {
                Name = $"SourceSinkPressure_{boundary.Sequence}",
                Text = $"{NormalizePressure(UnitConversions.PaToInchesWater(boundary.PressurePa)):0.###} in. w.c.",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = Color.FromArgb(28, 69, 108),
                Font = new Font("Segoe UI", 9F)
            }, 3, row++);
        }
        scroll.Content.Controls.Add(table);
        scroll.Content.SizeChanged += (_, _) => table.Width = scroll.Content.Width;
        root.Controls.Add(FormKit.Card(scroll, new Padding(2, 6, 4, 6)), 0, 3);

        _save.Name = "SourceSinkSaveButton";
        var buttons = FormKit.Buttons(this, "Save Airflows", "SourceSinkSaveButton", SaveAirflows);
        // Keep the field the rest of the editor refers to.
        var created = (Button)buttons.Controls[0];
        buttons.Controls.Remove(created);
        created.Dispose();
        _save.Text = "Save Airflows";
        _save.Height = 34;
        _save.Width = 124;
        _save.BackColor = DialogStyle.Accent;
        _save.ForeColor = Color.White;
        _save.FlatStyle = FlatStyle.Flat;
        _save.Font = new Font("Segoe UI Semibold", 9F);
        _save.Margin = new Padding(8, 4, 0, 0);
        UiPolish.RoundButton(_save, 8);
        _save.Click += (_, _) => SaveAirflows();
        buttons.Controls.Add(_save);
        buttons.Controls.SetChildIndex(_save, 0);
        AcceptButton = _save;
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);
        ValidateBalance();

        static void ConfigureColumns(TableLayoutPanel table)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        }

        static void AddHeading(TableLayoutPanel table, string text, int column, bool right) =>
            table.Controls.Add(new Label
            {
                Text = text, AutoSize = true, Anchor = right ? AnchorStyles.Right : AnchorStyles.Left,
                ForeColor = Color.FromArgb(92, 107, 120), Font = new Font("Segoe UI Semibold", 8.5F)
            }, column, 0);
    }

    private static double NormalizePressure(double pressure) => Math.Abs(pressure) < 0.0005 ? 0.0 : pressure;

    private void ValidateBalance()
    {
        var valid = TryReadAirflows(out var values, out var sourceTotal, out var sinkTotal);
        var tolerance = Math.Max(0.5, Math.Max(sourceTotal, sinkTotal) * 0.001);
        var balanced = valid && Math.Abs(sourceTotal - sinkTotal) <= tolerance;
        _save.Enabled = balanced;
        _save.BackColor = balanced ? DialogStyle.Accent : Color.FromArgb(160, 178, 194);
        _balanceStatus.ForeColor = balanced ? Color.FromArgb(24, 100, 56) : Color.Firebrick;
        _balanceStatus.BackColor = balanced ? Color.FromArgb(222, 243, 229) : Color.FromArgb(252, 228, 228);
        _balanceStatus.Text = !valid
            ? "Enter a positive numeric airflow for every source and sink."
            : balanced
                ? $"Balanced: {sourceTotal:N0} cfm source = {sinkTotal:N0} cfm sink."
                : $"Airflows do not balance: sources {sourceTotal:N0} cfm; sinks {sinkTotal:N0} cfm; difference {sourceTotal - sinkTotal:+#,##0;-#,##0;0} cfm.";
    }

    private void SaveAirflows()
    {
        if (!TryReadAirflows(out var values, out var sourceTotal, out var sinkTotal)
            || Math.Abs(sourceTotal - sinkTotal) > Math.Max(0.5, Math.Max(sourceTotal, sinkTotal) * 0.001))
        {
            ValidateBalance();
            return;
        }
        AirflowsCfm = values;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadAirflows(out Dictionary<int, double> values, out double sourceTotal,
        out double sinkTotal)
    {
        values = new Dictionary<int, double>();
        sourceTotal = 0;
        sinkTotal = 0;
        foreach (var boundary in _boundaries)
        {
            if (!_airflowInputs.TryGetValue(boundary.Sequence, out var input)
                || !double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm)
                || !double.IsFinite(cfm) || cfm <= 0)
                return false;
            values[boundary.Sequence] = cfm;
            var source = string.Equals(boundary.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(boundary.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase);
            if (source) sourceTotal += cfm;
            else sinkTotal += cfm;
        }
        return sourceTotal > 0 && sinkTotal > 0;
    }
}
