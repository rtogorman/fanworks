using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Analysis Settings (Analysis ribbon): the low and high values the engineer expects for duct friction rate, duct
/// velocity, fitting velocity and fitting loss. Values are shown in the project's units and stored in SI. Every
/// value is optional; FanWorks supplies no defaults. The Analysis tab's top-10 list then shows whether each duct
/// and fitting is within its expected ranges.
/// </summary>
internal sealed class AnalysisSettingsDialog : DpiScaledForm
{
    private sealed record RangeRow(ExpectedQuantity Quantity, string Caption, string Unit, string Format,
        Func<double, double> ToDisplay, Func<double, double> ToSi, TextBox Low, TextBox High,
        string OriginalLow, string OriginalHigh, ExpectedRange Original);

    private readonly List<RangeRow> _rows = new();

    /// <summary>The ranges as entered, in SI (valid only after the dialog returns OK).</summary>
    internal AnalysisExpectations Expectations { get; private set; }

    internal AnalysisSettingsDialog(AnalysisExpectations current, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(current);
        Expectations = current.Copy();
        Text = "Analysis Settings";
        Name = "AnalysisSettingsDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 362);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var imperial = units == UnitSystem.Imperial;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20, 16, 20, 14),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label
        {
            Text = "Expected value ranges",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(11, 31, 59),
            Font = new Font("Segoe UI Semibold", 11F)
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Enter the range you expect for each quantity. The Analysis tab flags ducts and fittings outside it. "
                   + "Leave a value empty to skip it.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Color.FromArgb(74, 91, 106)
        }, 0, 1);

        var table = new TableLayoutPanel
        {
            Name = "AnalysisSettingsTable",
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,
            BackColor = Color.White,
            Padding = new Padding(14, 6, 14, 6),
            Margin = new Padding(0, 4, 0, 8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        for (var index = 0; index < 4; index++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.Controls.Add(Header("Quantity", ContentAlignment.MiddleLeft), 0, 0);
        table.Controls.Add(Header("Low", ContentAlignment.MiddleLeft), 1, 0);
        table.Controls.Add(Header("High", ContentAlignment.MiddleLeft), 2, 0);
        table.Controls.Add(Header("Units", ContentAlignment.MiddleLeft), 3, 0);

        AddRange(ExpectedQuantity.DuctPressureRate, "Duct pressure drop rate",
            imperial ? "in. w.c./100 ft" : "Pa/m", "0.####",
            imperial ? UnitConversions.PaPerMToInchesWaterPer100Feet : value => value,
            imperial ? UnitConversions.InchesWaterPer100FeetToPaPerM : value => value);
        AddRange(ExpectedQuantity.DuctVelocity, "Duct velocity", imperial ? "fpm" : "m/s", imperial ? "0.#" : "0.###",
            imperial ? UnitConversions.MpsToFpm : value => value,
            imperial ? UnitConversions.FpmToMps : value => value);
        AddRange(ExpectedQuantity.FittingVelocity, "Fitting velocity", imperial ? "fpm" : "m/s", imperial ? "0.#" : "0.###",
            imperial ? UnitConversions.MpsToFpm : value => value,
            imperial ? UnitConversions.FpmToMps : value => value);
        AddRange(ExpectedQuantity.FittingLoss, "Fitting pressure loss", imperial ? "in. w.c." : "Pa", imperial ? "0.####" : "0.##",
            imperial ? UnitConversions.PaToInchesWater : value => value,
            imperial ? UnitConversions.InchesWaterToPa : value => value);
        root.Controls.Add(table, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button
        {
            Name = "AnalysisSettingsSaveButton", Text = "Save", Width = 100, Height = 32,
            BackColor = Color.FromArgb(14, 111, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        var clear = new Button { Name = "AnalysisSettingsClearButton", Text = "Clear All", Width = 96, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        UiPolish.RoundButton(clear, 7);
        save.Click += (_, _) => SaveRanges();
        clear.Click += (_, _) =>
        {
            foreach (var row in _rows)
            {
                row.Low.Clear();
                row.High.Clear();
            }
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(clear);
        clear.Margin = new Padding(3, 3, 120, 3);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        return;

        void AddRange(ExpectedQuantity quantity, string caption, string unit, string format,
            Func<double, double> toDisplay, Func<double, double> toSi)
        {
            var row = _rows.Count + 1;
            var range = Expectations.RangeFor(quantity);
            var low = Box($"AnalysisSettings{quantity}Low", range.Low, toDisplay, format);
            var high = Box($"AnalysisSettings{quantity}High", range.High, toDisplay, format);
            table.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(48, 65, 80),
                Font = new Font("Segoe UI Semibold", 9F)
            }, 0, row);
            table.Controls.Add(low, 1, row);
            table.Controls.Add(high, 2, row);
            table.Controls.Add(new Label
            {
                Text = unit,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(74, 91, 106)
            }, 3, row);
            _rows.Add(new RangeRow(quantity, caption, unit, format, toDisplay, toSi, low, high, low.Text, high.Text, range.Copy()));
        }

        static TextBox Box(string name, double? value, Func<double, double> toDisplay, string format) => new()
        {
            Name = name,
            Width = 92,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 10, 3),
            TextAlign = HorizontalAlignment.Right,
            Text = value is double si ? toDisplay(si).ToString(format, CultureInfo.CurrentCulture) : string.Empty
        };

        static Label Header(string text, ContentAlignment align) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = align,
            ForeColor = Color.FromArgb(74, 91, 106),
            Font = new Font("Segoe UI Semibold", 8.5F)
        };
    }

    private void SaveRanges()
    {
        var result = new AnalysisExpectations();
        foreach (var row in _rows)
        {
            if (!TryBound(row, row.Low, row.OriginalLow, row.Original.Low, "low", out var low)
                || !TryBound(row, row.High, row.OriginalHigh, row.Original.High, "high", out var high))
                return;
            if (low is double l && high is double h && l > h)
            {
                MessageBox.Show(this, $"{row.Caption}: the low value is above the high value.", "Check Expected Range",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                row.Low.Focus();
                return;
            }
            var range = result.RangeFor(row.Quantity);
            range.Low = low;
            range.High = high;
        }
        Expectations = result;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// Reads one bound. An empty box is no bound. A box left as it was shown keeps the stored SI value exactly, so
    /// opening and saving the dialog never shifts a range by display rounding.
    /// </summary>
    private bool TryBound(RangeRow row, TextBox box, string originalText, double? originalValue, string which, out double? value)
    {
        value = null;
        var text = box.Text.Trim();
        if (text.Length == 0)
            return true;
        if (string.Equals(text, originalText, StringComparison.Ordinal) && originalValue is not null)
        {
            value = originalValue;
            return true;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var display)
            || !double.IsFinite(display) || display < 0)
        {
            MessageBox.Show(this, $"{row.Caption}: enter the {which} value as a number of zero or more ({row.Unit}), or leave it empty.",
                "Check Expected Range", MessageBoxButtons.OK, MessageBoxIcon.Information);
            box.Focus();
            box.SelectAll();
            return false;
        }
        value = row.ToSi(display);
        return true;
    }
}
