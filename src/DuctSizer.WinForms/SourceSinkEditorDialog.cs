using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class SourceSinkEditorDialog : Form
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
        Text = "Source and Sink Editor";
        Name = "SourceSinkEditorDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(690, Math.Min(580, 190 + _boundaries.Count * 38));
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(new Label
        {
            Text = "SOURCE AND SINK AIRFLOWS\r\nEdit the boundary flows so total source airflow equals total sink airflow.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _balanceStatus.Name = "SourceSinkBalanceStatusLabel";
        _balanceStatus.Dock = DockStyle.Fill;
        _balanceStatus.TextAlign = ContentAlignment.MiddleLeft;
        _balanceStatus.Font = new Font("Segoe UI Semibold", 8.5F);
        _balanceStatus.Padding = new Padding(8, 0, 8, 0);
        root.Controls.Add(_balanceStatus, 0, 1);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = _boundaries.Count + 1,
            Padding = new Padding(8),
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        AddHeader("TYPE", 0);
        AddHeader("ITEM", 1);
        AddHeader("AIRFLOW (CFM)", 2);
        AddHeader("PRESSURE (IN. W.C.)", 3);
        var row = 1;
        foreach (var boundary in _boundaries)
        {
            var source = string.Equals(boundary.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(boundary.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase);
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.Controls.Add(CellLabel(source ? "Source" : "Sink", true), 0, row);
            table.Controls.Add(CellLabel(boundary.Id, false), 1, row);
            var input = new TextBox
            {
                Name = $"SourceSinkAirflow_{boundary.Sequence}",
                Text = UnitConversions.M3PerSToCfm(boundary.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture),
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(4, 4, 8, 4)
            };
            input.TextChanged += (_, _) => ValidateBalance();
            _airflowInputs[boundary.Sequence] = input;
            table.Controls.Add(input, 2, row);
            table.Controls.Add(new Label
            {
                Name = $"SourceSinkPressure_{boundary.Sequence}",
                Text = $"{NormalizePressure(UnitConversions.PaToInchesWater(boundary.PressurePa)):0.###} in. w.c.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(28, 69, 108),
                Font = new Font("Segoe UI Semibold", 8.7F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(4, 0, 8, 0)
            }, 3, row++);
        }
        scroll.Controls.Add(table);
        table.Dock = DockStyle.Top;
        root.Controls.Add(scroll, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _save.Name = "SourceSinkSaveButton";
        _save.Text = "Save Airflows";
        _save.Width = 118;
        _save.Height = 32;
        _save.BackColor = Color.FromArgb(42, 135, 91);
        _save.ForeColor = Color.White;
        _save.FlatStyle = FlatStyle.Flat;
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(_save, 7);
        UiPolish.RoundButton(cancel, 7);
        _save.Click += (_, _) => SaveAirflows();
        actions.Controls.Add(_save);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
        AcceptButton = _save;
        CancelButton = cancel;
        ValidateBalance();

        void AddHeader(string text, int column)
        {
            table.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(72, 92, 110),
                Font = new Font("Segoe UI Semibold", 8F),
                TextAlign = column >= 2 ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 8, 0)
            }, column, 0);
        }

        static Label CellLabel(string text, bool bold) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI" + (bold ? " Semibold" : string.Empty), 8.7F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
    }

    private static double NormalizePressure(double pressure) => Math.Abs(pressure) < 0.0005 ? 0.0 : pressure;

    private void ValidateBalance()
    {
        var valid = TryReadAirflows(out var values, out var sourceTotal, out var sinkTotal);
        var tolerance = Math.Max(0.5, Math.Max(sourceTotal, sinkTotal) * 0.001);
        var balanced = valid && Math.Abs(sourceTotal - sinkTotal) <= tolerance;
        _save.Enabled = balanced;
        _balanceStatus.ForeColor = balanced ? Color.FromArgb(30, 100, 55) : Color.Firebrick;
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
