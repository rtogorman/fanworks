using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Customize Duct Class (Analysis ribbon): shows the calculated SMACNA pressure class and the recommended seal class,
/// and lets the engineer override either. Saving recalculates the leakage factor F and the duct leakage. The preview
/// line shows the result before saving.
/// </summary>
internal sealed class DuctClassDialog : DpiScaledForm
{
    private readonly SystemLeakageResult _calculated;
    private readonly bool _imperial;
    private readonly CheckBox _overrideClass = new() { Name = "DuctClassOverrideCheck", Text = "Override", AutoSize = true };
    private readonly ComboBox _classChoice = new() { Name = "DuctClassChoice", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _overrideSeal = new() { Name = "SealClassOverrideCheck", Text = "Override", AutoSize = true };
    private readonly ComboBox _sealChoice = new() { Name = "SealClassChoice", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _recommendedSeal = new() { Name = "DuctClassRecommendedSeal" };
    private readonly Label _preview = new() { Name = "DuctClassPreview" };

    /// <summary>The chosen override pressure class, in. w.c. (null = use the calculated class).</summary>
    internal double? PressureClassOverride { get; private set; }

    /// <summary>The chosen override seal class (null = use the recommended one).</summary>
    internal SmacnaSealClass? SealClassOverride { get; private set; }

    internal DuctClassDialog(SystemLeakageResult calculated, AnalysisExpectations current, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(calculated);
        ArgumentNullException.ThrowIfNull(current);
        _calculated = calculated;
        _imperial = units == UnitSystem.Imperial;
        Text = "Customize Duct Class";
        Name = "DuctClassDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 350);
        Font = new Font("Segoe UI", 9F);
        BackColor = FormKit.Background;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20, 16, 20, 14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label
        {
            Text = "Duct pressure class and seal class",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(22, 50, 78),
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "FanWorks selects these per SMACNA HVAC Duct Construction Standards. Tick Override to use your own "
                   + "class; the leakage factor F and the duct leakage are recalculated when you save.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(74, 91, 106)
        }, 0, 1);

        var table = new TableLayoutPanel
        {
            Name = "DuctClassTable",
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(Header(string.Empty), 0, 0);
        table.Controls.Add(Header("Calculated (SMACNA)"), 1, 0);
        table.Controls.Add(Header(string.Empty), 2, 0);
        table.Controls.Add(Header("Use instead"), 3, 0);

        foreach (var value in SmacnaLeakage.PressureClassesInWc)
            _classChoice.Items.Add(ClassText(value));
        foreach (var seal in new[] { SmacnaSealClass.A, SmacnaSealClass.B, SmacnaSealClass.C })
            _sealChoice.Items.Add($"Seal Class {seal}");
        _classChoice.Width = 140;
        _sealChoice.Width = 140;
        _classChoice.Anchor = AnchorStyles.Left;
        _sealChoice.Anchor = AnchorStyles.Left;
        _overrideClass.Anchor = AnchorStyles.Left;
        _overrideSeal.Anchor = AnchorStyles.Left;

        table.Controls.Add(RowLabel("Duct pressure class"), 0, 1);
        table.Controls.Add(Value(calculated.CalculatedPressureClassInWc is double calculatedClass
            ? ClassText(calculatedClass) + (calculated.DesignPressureInWc is double design
                ? $"  (design {design.ToString("0.###", CultureInfo.CurrentCulture)})" : string.Empty)
            : "Not available: " + (calculated.Problem ?? "the system pressure is not known")), 1, 1);
        table.Controls.Add(_overrideClass, 2, 1);
        table.Controls.Add(_classChoice, 3, 1);
        table.Controls.Add(RowLabel("Seal class"), 0, 2);
        _recommendedSeal.Dock = DockStyle.Fill;
        _recommendedSeal.TextAlign = ContentAlignment.MiddleLeft;
        _recommendedSeal.ForeColor = Color.FromArgb(22, 65, 102);
        _recommendedSeal.Font = new Font("Segoe UI Semibold", 9F);
        table.Controls.Add(_recommendedSeal, 1, 2);
        table.Controls.Add(_overrideSeal, 2, 2);
        table.Controls.Add(_sealChoice, 3, 2);
        var card = FormKit.Card(table, new Padding(14, 4, 14, 4));
        card.Margin = new Padding(0, 4, 0, 6);
        root.Controls.Add(card, 0, 2);

        _preview.Dock = DockStyle.Fill;
        _preview.ForeColor = Color.FromArgb(22, 65, 102);
        _preview.Padding = new Padding(2, 4, 2, 0);
        root.Controls.Add(_preview, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button
        {
            Name = "DuctClassSaveButton", Text = "Save", Width = 100, Height = 32,
            BackColor = DialogStyle.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        save.Click += (_, _) => SaveChoice();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;

        // Initial state: the saved overrides, else the calculated values shown greyed out.
        _overrideClass.Checked = current.PressureClassOverrideInWc is not null;
        _classChoice.SelectedIndex = IndexOfClass(current.PressureClassOverrideInWc ?? calculated.CalculatedPressureClassInWc ?? 1);
        _overrideSeal.Checked = current.SealClassOverride is not null;
        _sealChoice.SelectedIndex = (int)(current.SealClassOverride ?? calculated.RecommendedSealClass ?? SmacnaSealClass.C);
        _overrideClass.CheckedChanged += (_, _) => UpdateState();
        _overrideSeal.CheckedChanged += (_, _) => UpdateState();
        _classChoice.SelectedIndexChanged += (_, _) => UpdateState();
        _sealChoice.SelectedIndexChanged += (_, _) => UpdateState();
        UpdateState();
        return;

        static Label Header(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(74, 91, 106), Font = new Font("Segoe UI Semibold", 8.5F)
        };

        static Label RowLabel(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80), Font = new Font("Segoe UI Semibold", 9F)
        };

        static Label Value(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            ForeColor = Color.FromArgb(22, 65, 102), Font = new Font("Segoe UI Semibold", 9F)
        };
    }

    private string ClassText(double inWc) => _imperial
        ? $"{inWc.ToString("0.#", CultureInfo.CurrentCulture)} in. w.c."
        : $"{UnitConversions.InchesWaterToPa(inWc):0} Pa ({inWc:0.#} in. w.c.)";

    private static int IndexOfClass(double inWc)
    {
        for (var index = 0; index < SmacnaLeakage.PressureClassesInWc.Count; index++)
            if (Math.Abs(SmacnaLeakage.PressureClassesInWc[index] - inWc) < 1e-9)
                return index;
        return 1;
    }

    /// <summary>The pressure class in use with the current choices (override or calculated).</summary>
    private double? ChosenClass => _overrideClass.Checked
        ? SmacnaLeakage.PressureClassesInWc[Math.Max(0, _classChoice.SelectedIndex)]
        : _calculated.CalculatedPressureClassInWc;

    private void UpdateState()
    {
        _classChoice.Enabled = _overrideClass.Checked;
        _sealChoice.Enabled = _overrideSeal.Checked;
        var pressureClass = ChosenClass;
        var recommended = pressureClass is double known ? SmacnaLeakage.SealClassFor(known) : (SmacnaSealClass?)null;
        _recommendedSeal.Text = recommended is SmacnaSealClass seal
            ? $"Seal Class {seal} (for {ClassText(pressureClass!.Value)})"
            : "Not available until the pressure class is known";
        if (!_overrideSeal.Checked && recommended is SmacnaSealClass follow)
            _sealChoice.SelectedIndex = (int)follow;
        var sealInUse = _overrideSeal.Checked ? (SmacnaSealClass)_sealChoice.SelectedIndex : recommended;
        if (pressureClass is not double p || sealInUse is not SmacnaSealClass s)
        {
            _preview.Text = "Choose a pressure class to calculate the leakage factor F and the duct leakage.";
            return;
        }
        // Preview with the same calculation the Analysis tab uses: F = CL x P^0.65 per duct shape, leakage = F x A / 100.
        var parts = _calculated.Areas.Where(area => area.Problem is null).GroupBy(area => area.Shape).OrderBy(group => group.Key)
            .Select(group =>
            {
                var leakageClass = SmacnaLeakage.LeakageClass(s, group.Key);
                var factor = SmacnaLeakage.AllowableLeakageFactor(leakageClass, p);
                return (Shape: group.Key, Class: leakageClass, Factor: factor, Cfm: factor * group.Sum(area => area.AreaFt2) / 100);
            }).ToArray();
        var total = parts.Sum(part => part.Cfm);
        var leakage = _imperial
            ? $"{total.ToString("0.#", CultureInfo.CurrentCulture)} cfm"
            : $"{UnitConversions.M3PerSToLps(UnitConversions.CfmToM3PerS(total)).ToString("0.#", CultureInfo.CurrentCulture)} L/s";
        _preview.Text = parts.Length == 0
            ? "No duct or fitting surface area yet."
            : "With these classes: leakage factor F = "
              + string.Join(", ", parts.Select(part => $"{part.Factor.ToString("0.##", CultureInfo.CurrentCulture)} cfm/100 ft² "
                                                      + (part.Shape == DuctShape.Round ? "round" : "rectangular") + $" (CL {part.Class})"))
              + $"; duct leakage = {leakage}.";
    }

    private void SaveChoice()
    {
        PressureClassOverride = _overrideClass.Checked
            ? SmacnaLeakage.PressureClassesInWc[Math.Max(0, _classChoice.SelectedIndex)]
            : null;
        SealClassOverride = _overrideSeal.Checked ? (SmacnaSealClass)_sealChoice.SelectedIndex : null;
        DialogResult = DialogResult.OK;
        Close();
    }
}
