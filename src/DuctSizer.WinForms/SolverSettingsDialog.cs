using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class SolverSettingsDialog : Form
{
    private readonly UnitSystem _units;
    private readonly TextBox _sizingIncrement = new() { Name = "SolverSettingsSizingIncrementText", TextAlign = HorizontalAlignment.Right };
    private readonly ComboBox _roundingMethod = new() { Name = "SolverSettingsRoundingMethodCombo", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _equivalentMethod = new() { Name = "SolverSettingsEquivalentMethodCombo", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _pressureBasis = new() { Name = "SolverSettingsPressureBasisCombo", DropDownStyle = ComboBoxStyle.DropDownList };

    internal double SizingIncrementM { get; private set; }
    internal SizeRoundingDirection RoundingDirection => Choice(_roundingMethod, SizeRoundingDirection.Automatic);
    internal EquivalentDiameterMethod EquivalentDiameterMethod => Choice(_equivalentMethod, EquivalentDiameterMethod.Huebscher);
    internal PressureLossBasis PressureLossBasis => Choice(_pressureBasis, PressureLossBasis.EquivalentRoundArea);

    internal SolverSettingsDialog(DuctSystemDocument document, UnitSystem units)
    {
        _units = units;
        Text = "Calculation Method Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(590, 300);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);

        _sizingIncrement.Text = (units == UnitSystem.Imperial
            ? UnitConversions.MToInches(document.DefaultSizingIncrementM)
            : UnitConversions.MToMillimeters(document.DefaultSizingIncrementM)).ToString("0.####", CultureInfo.CurrentCulture);
        _roundingMethod.Items.AddRange(new object[]
        {
            new SettingChoice<SizeRoundingDirection>("Automatic (.5 rounds up)", SizeRoundingDirection.Automatic),
            new SettingChoice<SizeRoundingDirection>("Round Down", SizeRoundingDirection.Down)
        });
        _equivalentMethod.Items.AddRange(new object[]
        {
            new SettingChoice<EquivalentDiameterMethod>("Huebscher", EquivalentDiameterMethod.Huebscher),
            new SettingChoice<EquivalentDiameterMethod>("Hydraulic Diameter", EquivalentDiameterMethod.HydraulicDiameter)
        });
        _pressureBasis.Items.AddRange(new object[]
        {
            new SettingChoice<PressureLossBasis>("Equivalent Round Area", PressureLossBasis.EquivalentRoundArea),
            new SettingChoice<PressureLossBasis>("Actual Duct Area", PressureLossBasis.ActualDuctArea)
        });
        Select(_roundingMethod, document.DefaultSizeRoundingDirection);
        Select(_equivalentMethod, document.DefaultEquivalentDiameterMethod);
        Select(_pressureBasis, document.DefaultPressureLossBasis);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(20), BackColor = BackColor };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, units == UnitSystem.Imperial ? "Autosize Increment (in)" : "Autosize Increment (mm)", _sizingIncrement);
        AddRow(table, 1, "Autosize Rounding", _roundingMethod);
        AddRow(table, 2, "Equivalent Diameter Method", _equivalentMethod);
        AddRow(table, 3, "Duct Area Calculation Method", _pressureBasis);
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Save", Width = 94, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 94, Height = 34, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 8);
        UiPolish.RoundButton(cancel, 8);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, 5);
        table.SetColumnSpan(buttons, 2);
        save.Click += (_, _) => SaveSettings();
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
        UiPolish.ApplyDialogSurface(this);
    }

    private void SaveSettings()
    {
        if ((!double.TryParse(_sizingIncrement.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.CurrentCulture, out var displayed)
             && !double.TryParse(_sizingIncrement.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.InvariantCulture, out displayed))
            || !double.IsFinite(displayed) || displayed < 0)
        {
            MessageBox.Show(this, "Enter a nonnegative autosize increment.", "Invalid Solver Setting",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _sizingIncrement.Focus();
            _sizingIncrement.SelectAll();
            return;
        }
        SizingIncrementM = _units == UnitSystem.Imperial
            ? UnitConversions.InchesToM(displayed) : UnitConversions.MillimetersToM(displayed);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static T Choice<T>(ComboBox combo, T fallback) where T : struct =>
        (combo.SelectedItem as SettingChoice<T>)?.Value ?? fallback;

    private static void Select<T>(ComboBox combo, T value) where T : struct =>
        combo.SelectedItem = combo.Items.Cast<SettingChoice<T>>().First(x => EqualityComparer<T>.Default.Equals(x.Value, value));

    private sealed record SettingChoice<T>(string Label, T Value) { public override string ToString() => Label; }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80), Font = new Font("Segoe UI Semibold", 9F) }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 6, 0, 6);
        table.Controls.Add(input, 1, row);
    }
}
