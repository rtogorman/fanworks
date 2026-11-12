using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutDuctEditDialog : DpiScaledForm, IInlineEditable
{
    private readonly List<InlineEditRow> _inlineRows = new();
    private bool _inline;
    private readonly UnitSystem _units;
    private readonly SavedDuct _original;
    private readonly double _sizingIncrementM;
    private readonly SizeRoundingDirection _roundingDirection;
    private readonly ComboBox _mode = new();
    private readonly TextBox _airflow = new();
    private readonly TextBox _temperature = new();
    private readonly TextBox _length = new();
    private readonly ComboBox _shape = new();
    private readonly TextBox _diameter = new();
    private readonly TextBox _width = new();
    private readonly TextBox _height = new();
    private readonly TextBox _aspectRatio = new();
    private readonly TextBox _targetFriction = new();
    private readonly TextBox _maximumVelocity = new();
    private readonly ComboBox _material = new();
    private readonly Label _result = new();
    private readonly Button _updateButton = new();
    internal SavedDuct? Result { get; private set; }

    internal LayoutDuctEditDialog(SavedDuct duct, UnitSystem units, double sizingIncrementM,
        SizeRoundingDirection roundingDirection)
    {
        _original = duct;
        _units = units;
        _sizingIncrementM = sizingIncrementM > 0 ? sizingIncrementM
            : units == UnitSystem.Imperial ? UnitConversions.InchesToM(0.5) : UnitConversions.MillimetersToM(10);
        _roundingDirection = roundingDirection;
        Text = duct.Sequence > 0 ? $"Edit {duct.Id}" : "Size Duct Connection";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(650, 650);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _mode.Name = "LayoutDuctSizingMethodCombo";
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[] { "Check Existing", "Autosize (SP Loss)", "Autosize (Velocity)" });
        _mode.SelectedIndex = 1;
        _mode.SelectedIndexChanged += (_, _) => { UpdateModeInputs(); InvalidateCalculation(); };
        _shape.DropDownStyle = ComboBoxStyle.DropDownList;
        _shape.Items.AddRange(new object[] { DuctShape.Round, DuctShape.Rectangular });
        _shape.SelectedItem = duct.Shape;
        _shape.SelectedIndexChanged += (_, _) => { UpdateModeInputs(); InvalidateCalculation(); };
        _material.DropDownStyle = ComboBoxStyle.DropDownList;
        _material.Items.AddRange(DuctMaterialCatalog.All.Cast<object>().ToArray());
        _material.SelectedItem = DuctMaterialCatalog.All.FirstOrDefault(x =>
            string.Equals(x.Name, duct.MaterialName, StringComparison.OrdinalIgnoreCase))
            ?? DuctMaterialCatalog.All.First(x => x.Name == "Galvanized Spiral Duct");

        if (units == UnitSystem.Imperial)
        {
            _airflow.Text = UnitConversions.M3PerSToCfm(duct.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = UnitConversions.CelsiusToFahrenheit(duct.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture);
            _length.Text = UnitConversions.MToFeet(duct.LengthM).ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToInches(duct.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToInches(duct.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToInches(duct.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }

        else
        {
            _airflow.Text = UnitConversions.M3PerSToLps(duct.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = duct.TemperatureC.ToString("0.##", CultureInfo.CurrentCulture);
            _length.Text = duct.LengthM.ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToMillimeters(duct.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToMillimeters(duct.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToMillimeters(duct.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }
        _aspectRatio.Text = duct.Shape == DuctShape.Rectangular && duct.HeightM > 0
            ? (duct.WidthM / duct.HeightM).ToString("0.###", CultureInfo.CurrentCulture)
            : "1.5";
        _targetFriction.Text = units == UnitSystem.Imperial ? "0.08" : "0.65";
        _maximumVelocity.Text = units == UnitSystem.Imperial ? "1000" : "5";

        var flowUnit = units == UnitSystem.Imperial ? "cfm" : "L/s";
        var temperatureUnit = units == UnitSystem.Imperial ? "°F" : "°C";
        var lengthUnit = units == UnitSystem.Imperial ? "ft" : "m";
        var sizeUnit = units == UnitSystem.Imperial ? "in" : "mm";
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 14,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Calculation Method", _mode);
        AddRow(table, 1, $"Airflow ({flowUnit})", _airflow);
        AddRow(table, 2, $"Air Temperature ({temperatureUnit})", _temperature);
        AddRow(table, 3, $"Duct Length ({lengthUnit})", _length);
        AddRow(table, 4, "Duct Shape", _shape);
        AddRow(table, 5, $"Diameter ({sizeUnit})", _diameter);
        AddRow(table, 6, $"Width ({sizeUnit})", _width);
        AddRow(table, 7, $"Height ({sizeUnit})", _height);
        AddRow(table, 8, "Width / Height Ratio", _aspectRatio);
        AddRow(table, 9, units == UnitSystem.Imperial ? "Target SP (in. w.g./100 ft)" : "Target SP (Pa/m)", _targetFriction);
        AddRow(table, 10, units == UnitSystem.Imperial ? "Maximum Velocity (fpm)" : "Maximum Velocity (m/s)", _maximumVelocity);
        AddRow(table, 11, "Duct Material", _material);
        var checking = () => SelectedMode == CalculationMode.CheckEnteredSize;
        var round = () => _shape.SelectedItem is DuctShape.Round;
        _inlineRows.AddRange(new[]
        {
            new InlineEditRow("Calculation method", _mode),
            new InlineEditRow($"Airflow ({flowUnit})", _airflow),
            new InlineEditRow($"Air temperature ({temperatureUnit})", _temperature),
            new InlineEditRow($"Duct length ({lengthUnit})", _length),
            new InlineEditRow("Duct shape", _shape),
            new InlineEditRow($"Diameter ({sizeUnit})", _diameter, () => checking() && round()),
            new InlineEditRow($"Width ({sizeUnit})", _width, () => checking() && !round()),
            new InlineEditRow($"Height ({sizeUnit})", _height, () => checking() && !round()),
            new InlineEditRow("Width / height ratio", _aspectRatio, () => !checking() && !round()),
            new InlineEditRow(units == UnitSystem.Imperial ? "Target SP (in. w.c./100 ft)" : "Target SP (Pa/m)", _targetFriction,
                () => SelectedMode == CalculationMode.SizeByTargetFriction),
            new InlineEditRow(units == UnitSystem.Imperial ? "Maximum velocity (fpm)" : "Maximum velocity (m/s)", _maximumVelocity,
                () => SelectedMode == CalculationMode.SizeByMaximumVelocity),
            new InlineEditRow("Duct material", _material)
        });
        _result.Dock = DockStyle.Fill;
        _result.ForeColor = Color.FromArgb(16, 45, 84);
        _result.Font = new Font("Segoe UI Semibold", 9F);
        _result.TextAlign = ContentAlignment.MiddleLeft;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        table.Controls.Add(_result, 0, 12);
        table.SetColumnSpan(_result, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _updateButton.Name = "LayoutDuctDirectUpdateButton";
        _updateButton.Text = "Update Duct";
        _updateButton.Width = 120;
        _updateButton.Height = 32;
        _updateButton.BackColor = Color.FromArgb(14, 111, 230);
        _updateButton.ForeColor = Color.White;
        _updateButton.FlatStyle = FlatStyle.Flat;
        _updateButton.Enabled = false;
        var calculate = new Button
        {
            Name = "LayoutDuctCalculateButton",
            Text = "Calculate",
            Width = 105,
            Height = 32,
            BackColor = Color.FromArgb(14, 111, 230),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        calculate.Click += (_, _) => CalculatePreview();
        _updateButton.Click += (_, _) => SaveAndClose();
        UiPolish.RoundButton(calculate, 7);
        UiPolish.RoundButton(_updateButton, 7);
        UiPolish.RoundButton(cancel, 7);
        buttons.Controls.Add(_updateButton);
        buttons.Controls.Add(calculate);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, 13);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = calculate;
        CancelButton = cancel;
        UpdateModeInputs();
        InvalidateCalculation();
        foreach (var input in new[] { _airflow, _temperature, _length, _diameter, _width, _height,
                     _aspectRatio, _targetFriction, _maximumVelocity })
            input.TextChanged += (_, _) => InvalidateCalculation();
        _material.SelectedIndexChanged += (_, _) => InvalidateCalculation();
    }

    private CalculationMode SelectedMode => _mode.SelectedIndex switch
    {
        1 => CalculationMode.SizeByTargetFriction,
        2 => CalculationMode.SizeByMaximumVelocity,
        _ => CalculationMode.CheckEnteredSize
    };

    private void UpdateModeInputs()
    {
        var round = _shape.SelectedItem is DuctShape.Round;
        var checking = SelectedMode == CalculationMode.CheckEnteredSize;
        _diameter.Enabled = checking && round;
        _width.Enabled = checking && !round;
        _height.Enabled = checking && !round;
        _aspectRatio.Enabled = !checking && !round;
        _targetFriction.Enabled = SelectedMode == CalculationMode.SizeByTargetFriction;
        _maximumVelocity.Enabled = SelectedMode == CalculationMode.SizeByMaximumVelocity;
        InlineRowsChanged?.Invoke();
    }

    private void InvalidateCalculation()
    {
        Result = null;
        _updateButton.Enabled = false;
        _result.Text = _inline
            ? string.Empty
            : "Inputs changed — click Calculate to size the duct before updating.";
        _result.ForeColor = Color.FromArgb(92, 76, 42);
    }

    public IReadOnlyList<InlineEditRow> InlineRows => _inlineRows;

    public event Action? InlineRowsChanged;

    public Control? InlineStatus => _result;

    public void UseInline(IWin32Window messageOwner)
    {
        _inline = true;
        InvalidateCalculation();
    }

    /// <summary>Calculates the duct exactly as the Calculate button does; accepted when the calculation succeeds.</summary>
    public bool TryAcceptInline()
    {
        CalculatePreview();
        return Result is not null;
    }

    private void CalculatePreview()
    {
        try
        {
            var draft = ReadDuct();
            var calculation = draft.Calculate();
            var size = draft.Shape == DuctShape.Round
                ? (_units == UnitSystem.Imperial
                    ? $"{UnitConversions.MToInches(draft.DiameterM):0.##} in round"
                    : $"{UnitConversions.MToMillimeters(draft.DiameterM):0.#} mm round")
                : (_units == UnitSystem.Imperial
                    ? $"{UnitConversions.MToInches(draft.WidthM):0.##} × {UnitConversions.MToInches(draft.HeightM):0.##} in"
                    : $"{UnitConversions.MToMillimeters(draft.WidthM):0.#} × {UnitConversions.MToMillimeters(draft.HeightM):0.#} mm");
            _result.Text = _units == UnitSystem.Imperial
                ? $"{size}   •   {UnitConversions.MpsToFpm(calculation.ActualVelocityMPerS):0} fpm   •   {UnitConversions.PaToInchesWater(calculation.TotalPressureLossPa):0.###} in. w.g."
                : $"{size}   •   {calculation.ActualVelocityMPerS:0.##} m/s   •   {calculation.TotalPressureLossPa:0.##} Pa";
            _result.ForeColor = Color.FromArgb(16, 45, 84);
            Result = draft;
            _updateButton.Enabled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            Result = null;
            _updateButton.Enabled = false;
            _result.Text = ex.Message;
            _result.ForeColor = Color.Firebrick;
        }
    }

    private SavedDuct ReadDuct()
    {
        var airflowDisplay = Positive(_airflow, "airflow");
        var temperatureDisplay = Number(_temperature, "air temperature");
        var lengthDisplay = Positive(_length, "duct length");
        var shape = _shape.SelectedItem is DuctShape selectedShape ? selectedShape : DuctShape.Round;
        var material = _material.SelectedItem as DuctMaterial
                       ?? throw new InvalidOperationException("Select a duct material.");
        var airflow = _units == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(airflowDisplay)
            : UnitConversions.LpsToM3PerS(airflowDisplay);
        var temperature = _units == UnitSystem.Imperial
            ? UnitConversions.FahrenheitToCelsius(temperatureDisplay)
            : temperatureDisplay;
        var length = _units == UnitSystem.Imperial ? UnitConversions.FeetToM(lengthDisplay) : lengthDisplay;
        SavedDuct result;
        if (SelectedMode == CalculationMode.CheckEnteredSize)
        {
            var diameterDisplay = shape == DuctShape.Round ? Positive(_diameter, "diameter") : 0;
            var widthDisplay = shape == DuctShape.Rectangular ? Positive(_width, "width") : 0;
            var heightDisplay = shape == DuctShape.Rectangular ? Positive(_height, "height") : 0;
            result = new SavedDuct
            {
                AirflowM3PerS = airflow,
                TemperatureC = temperature,
                RelativeHumidity = _original.RelativeHumidity,
                AtmosphericPressurePa = _original.AtmosphericPressurePa,
                Shape = shape,
                DiameterM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(diameterDisplay) : UnitConversions.MillimetersToM(diameterDisplay),
                WidthM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(widthDisplay) : UnitConversions.MillimetersToM(widthDisplay),
                HeightM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(heightDisplay) : UnitConversions.MillimetersToM(heightDisplay),
                MaterialName = material.Name,
                RoughnessFeet = material.RoughnessFeet,
                EquivalentDiameterMethod = _original.EquivalentDiameterMethod,
                PressureLossBasis = _original.PressureLossBasis,
                LengthM = length
            };
        }
        else
        {
            var ratio = shape == DuctShape.Rectangular ? Positive(_aspectRatio, "width-to-height ratio") : 1.0;
            var targetFrictionDisplay = SelectedMode == CalculationMode.SizeByTargetFriction
                ? Positive(_targetFriction, "target static pressure loss") : 1.0;
            var maximumVelocityDisplay = SelectedMode == CalculationMode.SizeByMaximumVelocity
                ? Positive(_maximumVelocity, "maximum velocity") : 1.0;
            var targetFriction = _units == UnitSystem.Imperial
                ? UnitConversions.InchesWaterPer100FeetToPaPerM(targetFrictionDisplay)
                : targetFrictionDisplay;
            var maximumVelocity = _units == UnitSystem.Imperial
                ? UnitConversions.FpmToMps(maximumVelocityDisplay)
                : maximumVelocityDisplay;
            var sizing = DuctSizingSolver.Size(new DuctSizingInput(
                airflow, temperature, _original.RelativeHumidity, _original.AtmosphericPressurePa,
                shape, ratio, material, _original.EquivalentDiameterMethod, _original.PressureLossBasis,
                length, SelectedMode, targetFriction, maximumVelocity, _sizingIncrementM, _roundingDirection));
            result = SavedDuct.FromResult(_original.Sequence, _original.Description, sizing.Calculation,
                _original.AirSystemType, _original.BranchId);
        }
        result.Sequence = _original.Sequence;
        result.Description = _original.Description;
        result.AirSystemType = _original.AirSystemType;
        result.BranchId = _original.BranchId;
        result.DeltaXM = _original.DeltaXM;
        result.DeltaYM = _original.DeltaYM;
        result.DeltaZM = _original.DeltaZM;
        return result;
    }

    private void SaveAndClose()
    {
        if (Result is null)
        {
            MessageBox.Show(this, "Click Calculate and resolve any input errors before updating the duct.",
                "Calculate Duct First", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static double Positive(TextBox input, string name)
    {
        var value = Number(input, name);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, $"Enter a positive {name}.");
        return value;
    }

    private static double Number(TextBox input, string name)
    {
        if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || !double.IsFinite(value))
            throw new ArgumentException($"Enter a valid {name}.");
        return value;
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(48, 65, 80)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }
}
