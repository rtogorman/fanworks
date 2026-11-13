using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Customize Duct Class (Analysis ribbon): for each side of the fan — the negative (suction) side upstream of it and
/// the positive (discharge) side downstream — shows the calculated SMACNA pressure class and the recommended seal
/// class, and lets the engineer override either. Saving recalculates each side's leakage factor F and duct leakage.
/// Each side's preview line shows the result before saving.
/// </summary>
internal sealed class DuctClassDialog : DpiScaledForm
{
    private readonly bool _imperial;
    private readonly SideEditor _negative;
    private readonly SideEditor _positive;

    /// <summary>Positive (discharge) side: the chosen override pressure class, in. w.c. (null = calculated).</summary>
    internal double? PressureClassOverride { get; private set; }

    /// <summary>Positive (discharge) side: the chosen override seal class (null = recommended).</summary>
    internal SmacnaSealClass? SealClassOverride { get; private set; }

    /// <summary>Negative (suction) side: the chosen override pressure class, in. w.c. (null = calculated).</summary>
    internal double? SuctionPressureClassOverride { get; private set; }

    /// <summary>Negative (suction) side: the chosen override seal class (null = recommended).</summary>
    internal SmacnaSealClass? SuctionSealClassOverride { get; private set; }

    /// <summary>Whole system with no suction side (kept for callers that classify the system as one).</summary>
    internal DuctClassDialog(SystemLeakageResult calculated, AnalysisExpectations current, UnitSystem units)
        : this(new SystemLeakageSides(null, calculated), current, units)
    {
    }

    internal DuctClassDialog(SystemLeakageSides calculated, AnalysisExpectations current, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(calculated);
        ArgumentNullException.ThrowIfNull(current);
        _imperial = units == UnitSystem.Imperial;
        Text = "Customize Duct Class";
        Name = "DuctClassDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 560);
        Font = new Font("Segoe UI", 9F);
        BackColor = FormKit.Background;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20, 16, 20, 14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label
        {
            Text = "Duct pressure class and seal class, each side of the fan",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(11, 31, 59),
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "FanWorks selects these per SMACNA HVAC Duct Construction Standards for each side of the fan. Tick "
                   + "Override to use your own class; the leakage factor F and duct leakage are recalculated when you save.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(74, 91, 106)
        }, 0, 1);

        _negative = new SideEditor(this, "Suction", "Negative side: suction, upstream of the fan", calculated.Negative,
            current.SuctionPressureClassOverrideInWc, current.SuctionSealClassOverride,
            "No ductwork is upstream of the fan, so nothing is under negative pressure. An override saved here applies "
            + "once suction ductwork is added.");
        _positive = new SideEditor(this, string.Empty, "Positive side: discharge, downstream of the fan", calculated.Positive,
            current.PressureClassOverrideInWc, current.SealClassOverride, null);
        root.Controls.Add(_negative.Card, 0, 2);
        root.Controls.Add(_positive.Card, 0, 3);

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

    private void SaveChoice()
    {
        (PressureClassOverride, SealClassOverride) = _positive.Choice();
        (SuctionPressureClassOverride, SuctionSealClassOverride) = _negative.Choice();
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// One side's card: calculated class and recommended seal class, the override choices and a preview of the
    /// leakage with the classes chosen. Control names carry <c>prefix</c> ("Suction" for the negative side).
    /// </summary>
    private sealed class SideEditor
    {
        private readonly DuctClassDialog _owner;
        private readonly SystemLeakageResult? _calculated;
        private readonly CheckBox _overrideClass;
        private readonly ComboBox _classChoice;
        private readonly CheckBox _overrideSeal;
        private readonly ComboBox _sealChoice;
        private readonly Label _recommendedSeal;
        private readonly Label _preview;

        internal Control Card { get; }

        internal SideEditor(DuctClassDialog owner, string prefix, string title, SystemLeakageResult? calculated,
            double? classOverride, SmacnaSealClass? sealOverride, string? absentNote)
        {
            _owner = owner;
            _calculated = calculated;
            _overrideClass = new CheckBox { Name = prefix + "DuctClassOverrideCheck", Text = "Override", AutoSize = true, Anchor = AnchorStyles.Left };
            _classChoice = new ComboBox { Name = prefix + "DuctClassChoice", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Anchor = AnchorStyles.Left };
            _overrideSeal = new CheckBox { Name = prefix + "SealClassOverrideCheck", Text = "Override", AutoSize = true, Anchor = AnchorStyles.Left };
            _sealChoice = new ComboBox { Name = prefix + "SealClassChoice", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Anchor = AnchorStyles.Left };
            _recommendedSeal = new Label
            {
                Name = prefix + "DuctClassRecommendedSeal", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(11, 31, 59), Font = new Font("Segoe UI Semibold", 9F), AutoEllipsis = true
            };
            _preview = new Label
            {
                Name = prefix + "DuctClassPreview", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(11, 31, 59),
                Padding = new Padding(2, 2, 2, 0)
            };

            var table = new TableLayoutPanel
            {
                Name = prefix + "DuctClassTable", Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5,
                BackColor = Color.White, Margin = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = new Label
            {
                Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(14, 111, 230), Font = new Font("Segoe UI Semibold", 9.75F)
            };
            table.Controls.Add(heading, 0, 0);
            table.SetColumnSpan(heading, 4);
            table.Controls.Add(Header(string.Empty), 0, 1);
            table.Controls.Add(Header("Calculated (SMACNA)"), 1, 1);
            table.Controls.Add(Header(string.Empty), 2, 1);
            table.Controls.Add(Header("Use instead"), 3, 1);
            foreach (var value in SmacnaLeakage.PressureClassesInWc)
                _classChoice.Items.Add(owner.ClassText(value));
            foreach (var seal in new[] { SmacnaSealClass.A, SmacnaSealClass.B, SmacnaSealClass.C })
                _sealChoice.Items.Add($"Seal Class {seal}");
            table.Controls.Add(RowLabel("Duct pressure class"), 0, 2);
            table.Controls.Add(Value(calculated is null
                ? "No ductwork on this side"
                : calculated.CalculatedPressureClassInWc is double calculatedClass
                    ? owner.ClassText(calculatedClass) + (calculated.DesignPressureInWc is double design
                        ? $"  (design {design.ToString("0.###", CultureInfo.CurrentCulture)})" : string.Empty)
                    : "Not available: " + (calculated.Problem ?? "the system pressure is not known")), 1, 2);
            table.Controls.Add(_overrideClass, 2, 2);
            table.Controls.Add(_classChoice, 3, 2);
            table.Controls.Add(RowLabel("Seal class"), 0, 3);
            table.Controls.Add(_recommendedSeal, 1, 3);
            table.Controls.Add(_overrideSeal, 2, 3);
            table.Controls.Add(_sealChoice, 3, 3);
            table.Controls.Add(_preview, 0, 4);
            table.SetColumnSpan(_preview, 4);
            Card = FormKit.Card(table, new Padding(14, 4, 14, 4));
            Card.Margin = new Padding(0, 4, 0, 6);

            // Initial state: the saved overrides, else the calculated values shown greyed out.
            _overrideClass.Checked = classOverride is not null;
            _classChoice.SelectedIndex = IndexOfClass(classOverride ?? calculated?.CalculatedPressureClassInWc ?? 1);
            _overrideSeal.Checked = sealOverride is not null;
            _sealChoice.SelectedIndex = (int)(sealOverride ?? calculated?.RecommendedSealClass ?? SmacnaSealClass.C);
            _overrideClass.CheckedChanged += (_, _) => UpdateState();
            _overrideSeal.CheckedChanged += (_, _) => UpdateState();
            _classChoice.SelectedIndexChanged += (_, _) => UpdateState();
            _sealChoice.SelectedIndexChanged += (_, _) => UpdateState();
            UpdateState();
            if (calculated is null && absentNote is not null)
                _preview.Text = absentNote;
        }

        /// <summary>The overrides chosen (null where Override is not ticked).</summary>
        internal (double? PressureClass, SmacnaSealClass? Seal) Choice() =>
            (_overrideClass.Checked ? SmacnaLeakage.PressureClassesInWc[Math.Max(0, _classChoice.SelectedIndex)] : null,
             _overrideSeal.Checked ? (SmacnaSealClass)_sealChoice.SelectedIndex : null);

        private double? ChosenClass => _overrideClass.Checked
            ? SmacnaLeakage.PressureClassesInWc[Math.Max(0, _classChoice.SelectedIndex)]
            : _calculated?.CalculatedPressureClassInWc;

        private void UpdateState()
        {
            _classChoice.Enabled = _overrideClass.Checked;
            _sealChoice.Enabled = _overrideSeal.Checked;
            var pressureClass = ChosenClass;
            var recommended = pressureClass is double known ? SmacnaLeakage.SealClassFor(known) : (SmacnaSealClass?)null;
            _recommendedSeal.Text = recommended is SmacnaSealClass seal
                ? $"Seal Class {seal} (for {_owner.ClassText(pressureClass!.Value)})"
                : "Not available until the pressure class is known";
            if (!_overrideSeal.Checked && recommended is SmacnaSealClass follow)
                _sealChoice.SelectedIndex = (int)follow;
            var sealInUse = _overrideSeal.Checked ? (SmacnaSealClass)_sealChoice.SelectedIndex : recommended;
            if (_calculated is null)
                return;
            if (pressureClass is not double p || sealInUse is not SmacnaSealClass s)
            {
                _preview.Text = "Choose a pressure class to calculate the leakage factor F and the duct leakage.";
                return;
            }
            // The same calculation the Analysis tab uses: F = CL x P^0.65 per duct shape, leakage = F x A / 100.
            var parts = _calculated.Areas.Where(area => area.Problem is null).GroupBy(area => area.Shape).OrderBy(group => group.Key)
                .Select(group =>
                {
                    var leakageClass = SmacnaLeakage.LeakageClass(s, group.Key);
                    var factor = SmacnaLeakage.AllowableLeakageFactor(leakageClass, p);
                    return (Shape: group.Key, Class: leakageClass, Factor: factor, Cfm: factor * group.Sum(area => area.AreaFt2) / 100);
                }).ToArray();
            var total = parts.Sum(part => part.Cfm);
            var leakage = _owner._imperial
                ? $"{total.ToString("0.#", CultureInfo.CurrentCulture)} cfm"
                : $"{UnitConversions.M3PerSToLps(UnitConversions.CfmToM3PerS(total)).ToString("0.#", CultureInfo.CurrentCulture)} L/s";
            _preview.Text = parts.Length == 0
                ? "No duct or fitting surface area on this side yet."
                : "With these classes: leakage factor F = "
                  + string.Join(", ", parts.Select(part => $"{part.Factor.ToString("0.##", CultureInfo.CurrentCulture)} cfm/100 ft² "
                                                          + (part.Shape == DuctShape.Round ? "round" : "rectangular") + $" (CL {part.Class})"))
                  + $"; duct leakage = {leakage}.";
        }

        private static Label Header(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(74, 91, 106), Font = new Font("Segoe UI Semibold", 8.5F)
        };

        private static Label RowLabel(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80), Font = new Font("Segoe UI Semibold", 9F)
        };

        private static Label Value(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            ForeColor = Color.FromArgb(11, 31, 59), Font = new Font("Segoe UI Semibold", 9F)
        };
    }
}
