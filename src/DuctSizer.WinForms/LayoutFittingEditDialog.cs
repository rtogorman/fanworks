using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutFittingEditDialog : DpiScaledForm, IInlineEditable
{
    private readonly List<InlineEditRow> _baseInlineRows = new();
    private readonly List<InlineEditRow> _junctionInlineRows = new();
    private readonly List<InlineEditRow> _parameterInlineRows = new();
    private readonly TableLayoutPanel _definitionHost;
    private readonly Button _galleryButton;
    private bool _inline;
    private static readonly HashSet<string> JunctionDerivedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "path", "qbqc", "qsqc", "qbqs", "abac", "asac", "abas", "vbvc", "vsvc", "vbvs", "vcfpm"
    };
    private readonly SavedFitting _original;
    private readonly UnitSystem _units;
    private readonly ComboBox _definition = new();
    private readonly Label _description = new();
    private readonly TextBox _airflow = new();
    private readonly TextBox _temperature = new();
    private readonly ComboBox _shape = new();
    private readonly TextBox _diameter = new();
    private readonly TextBox _width = new();
    private readonly TextBox _height = new();
    private readonly TableLayoutPanel _parameters = new();
    private readonly GroupBox _junctionGroup = new();
    private readonly ComboBox _junctionPath = new();
    private readonly TextBox _commonFlow = new();
    private readonly TextBox _straightFlow = new();
    private readonly TextBox _branchFlow = new();
    private readonly Label _result = new();
    private readonly Button _updateButton = new();
    private readonly Dictionary<string, Control> _inputControls = new(StringComparer.OrdinalIgnoreCase);
    private bool _initializing = true;
    private bool _updatingJunctionFlows;
    internal SavedFitting? Result { get; private set; }

    internal LayoutFittingEditDialog(SavedFitting fitting, string fittingGroup, UnitSystem units)
    {
        _original = fitting;
        _units = units;
        Text = fitting.Sequence > 0 ? $"Edit {fitting.Id}" : "Define Fitting";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 620);
        MinimumSize = Size;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var definitions = FittingCatalog.All
            .OrderBy(x => string.Equals(FittingCatalog.SelectionGroup(x), fittingGroup, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Code).ToArray();
        _definition.DropDownStyle = ComboBoxStyle.DropDownList;
        _definition.FormattingEnabled = true;
        _definition.Items.AddRange(definitions.Cast<object>().ToArray());
        _definition.Format += (_, e) =>
        {
            if (e.ListItem is FittingDefinition item)
                e.Value = item.Code;
        };
        _definition.SelectedItem = definitions.FirstOrDefault(x =>
            string.Equals(x.Code, fitting.FittingCode, StringComparison.OrdinalIgnoreCase)) ?? definitions[0];
        _definition.SelectedIndexChanged += (_, _) => DefinitionChanged();
        var definitionHost = _definitionHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        definitionHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        definitionHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        var browseGallery = new Button
        {
            Name = "LayoutFittingGalleryButton",
            Text = "Fitting Gallery",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 69, 108)
        };
        _galleryButton = browseGallery;
        UiPolish.RoundButton(browseGallery, 6);
        browseGallery.Click += (_, _) =>
        {
            using var gallery = new FittingGalleryDialog(FittingCatalog.All, SelectedDefinition.Code);
            if (gallery.ShowDialog(browseGallery.FindForm() ?? this) == DialogResult.OK && gallery.SelectedDefinition is not null)
                _definition.SelectedItem = _definition.Items.Cast<FittingDefinition>().First(x =>
                    string.Equals(x.Code, gallery.SelectedDefinition.Code, StringComparison.OrdinalIgnoreCase));
        };
        definitionHost.Controls.Add(_definition, 0, 0);
        definitionHost.Controls.Add(browseGallery, 1, 0);

        _description.Dock = DockStyle.Fill;
        _description.AutoEllipsis = false;
        _description.ForeColor = Color.FromArgb(28, 69, 108);
        _description.Font = new Font("Segoe UI Semibold", 9F);
        _description.TextAlign = ContentAlignment.MiddleLeft;
        _shape.DropDownStyle = ComboBoxStyle.DropDownList;
        _shape.Items.AddRange(new object[] { DuctShape.Round, DuctShape.Rectangular });
        _shape.SelectedItem = fitting.Shape;
        _shape.SelectedIndexChanged += (_, _) => { UpdateShapeInputs(); InvalidateCalculation(); };

        if (units == UnitSystem.Imperial)
        {
            _airflow.Text = UnitConversions.M3PerSToCfm(fitting.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = UnitConversions.CelsiusToFahrenheit(fitting.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToInches(fitting.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToInches(fitting.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToInches(fitting.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }
        else
        {
            _airflow.Text = UnitConversions.M3PerSToLps(fitting.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = fitting.TemperatureC.ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToMillimeters(fitting.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToMillimeters(fitting.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToMillimeters(fitting.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }

        _junctionPath.DropDownStyle = ComboBoxStyle.DropDownList;
        _junctionPath.Items.AddRange(new object[] { JunctionPath.Straight, JunctionPath.Branch });
        _junctionPath.SelectedItem = fitting.Junction?.Path ?? JunctionPath.Straight;
        SetFlow(_commonFlow, fitting.Junction?.CommonAirflowM3PerS ?? fitting.AirflowM3PerS);
        SetFlow(_straightFlow, fitting.Junction?.StraightAirflowM3PerS ?? fitting.AirflowM3PerS * .6);
        SetFlow(_branchFlow, fitting.Junction?.BranchAirflowM3PerS ?? fitting.AirflowM3PerS * .4);

        var flowUnit = units == UnitSystem.Imperial ? "cfm" : "L/s";
        var temperatureUnit = units == UnitSystem.Imperial ? "°F" : "°C";
        var sizeUnit = units == UnitSystem.Imperial ? "in" : "mm";
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 268));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var basics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7 };
        basics.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        basics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(basics, 0, "Fitting Number", definitionHost, 38);
        AddRow(basics, 1, "Description", _description, 56);
        AddRow(basics, 2, $"Airflow ({flowUnit})", _airflow, 38);
        AddRow(basics, 3, $"Air Temperature ({temperatureUnit})", _temperature, 38);
        AddRow(basics, 4, "Reference Duct Shape", _shape, 38);
        var sizePanel = LayoutDialogControls.CreateLocationEditor(_diameter, _width, _height);
        foreach (Control control in sizePanel.Controls)
            if (control is Label label)
                label.Text = label.Text switch { "X" => "Dia", "Y" => "W", _ => "H" };
        AddRow(basics, 5, $"Duct Size ({sizeUnit})", sizePanel, 46);
        root.Controls.Add(basics, 0, 0);
        _baseInlineRows.AddRange(new[]
        {
            new InlineEditRow("Fitting number", definitionHost),
            new InlineEditRow("Fitting", _description),
            new InlineEditRow($"Airflow ({flowUnit})", _airflow),
            new InlineEditRow($"Air temperature ({temperatureUnit})", _temperature),
            new InlineEditRow("Reference duct shape", _shape),
            new InlineEditRow($"Duct size ({sizeUnit})", sizePanel)
        });

        var parameterGroup = new GroupBox { Text = "Fitting Inputs", Dock = DockStyle.Fill, Padding = new Padding(10) };
        _parameters.Dock = DockStyle.Fill;
        _parameters.AutoScroll = true;
        _parameters.ColumnCount = 2;
        _parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        _parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        parameterGroup.Controls.Add(_parameters);
        root.Controls.Add(parameterGroup, 0, 1);

        _junctionGroup.Text = "Junction Flow Inputs";
        _junctionGroup.Dock = DockStyle.Fill;
        var junction = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(6) };
        for (var index = 0; index < 4; index++)
            junction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddCompact(junction, 0, "Loss Path", _junctionPath);
        AddCompact(junction, 1, $"Common ({flowUnit})", _commonFlow);
        AddCompact(junction, 2, $"Straight ({flowUnit})", _straightFlow);
        AddCompact(junction, 3, $"Branch ({flowUnit})", _branchFlow);
        var isJunction = () => _definition.SelectedItem is FittingDefinition { IsJunction: true };
        _junctionInlineRows.AddRange(new[]
        {
            new InlineEditRow("Loss path", _junctionPath, isJunction),
            new InlineEditRow($"Common airflow ({flowUnit})", _commonFlow, isJunction),
            new InlineEditRow($"Straight airflow ({flowUnit})", _straightFlow, isJunction),
            new InlineEditRow($"Branch airflow ({flowUnit})", _branchFlow, isJunction)
        });
        _junctionGroup.Controls.Add(junction);
        root.Controls.Add(_junctionGroup, 0, 2);

        _result.Dock = DockStyle.Fill;
        _result.TextAlign = ContentAlignment.MiddleLeft;
        _result.Font = new Font("Segoe UI Semibold", 9F);
        _result.ForeColor = Color.FromArgb(28, 69, 108);
        root.Controls.Add(_result, 0, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _updateButton.Name = "LayoutFittingDirectUpdateButton";
        _updateButton.Text = "Update Fitting";
        _updateButton.Width = 130;
        _updateButton.Height = 32;
        _updateButton.BackColor = Color.FromArgb(42, 135, 91);
        _updateButton.ForeColor = Color.White;
        _updateButton.FlatStyle = FlatStyle.Flat;
        _updateButton.Enabled = false;
        var calculate = new Button
        {
            Name = "LayoutFittingCalculateButton",
            Text = "Calculate",
            Width = 105,
            Height = 32,
            BackColor = Color.FromArgb(31, 111, 168),
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
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);
        AcceptButton = calculate;
        CancelButton = cancel;

        foreach (var input in new[] { _temperature, _diameter, _width, _height })
            input.TextChanged += (_, _) => InvalidateCalculation();
        _airflow.TextChanged += (_, _) => HandleJunctionFlowChanged(_airflow);
        _commonFlow.TextChanged += (_, _) => HandleJunctionFlowChanged(_commonFlow);
        _straightFlow.TextChanged += (_, _) => HandleJunctionFlowChanged(_straightFlow);
        _branchFlow.TextChanged += (_, _) => HandleJunctionFlowChanged(_branchFlow);
        _junctionPath.SelectedIndexChanged += (_, _) => InvalidateCalculation();
        _initializing = false;
        DefinitionChanged();
        UpdateShapeInputs();
        InvalidateCalculation();
    }

    private FittingDefinition SelectedDefinition => (FittingDefinition)_definition.SelectedItem!;

    private void DefinitionChanged()
    {
        if (_definition.SelectedItem is not FittingDefinition definition)
            return;
        _description.Text = definition.Description;
        _junctionGroup.Visible = definition.IsJunction;
        var group = FittingCatalog.SelectionGroup(definition);
        var converging = string.Equals(group, "Converging Fittings", StringComparison.OrdinalIgnoreCase);
        _airflow.Enabled = !converging;
        _commonFlow.Enabled = !converging;
        _inputControls.Clear();
        _parameterInlineRows.Clear();
        _parameters.Controls.Clear();
        _parameters.RowStyles.Clear();
        var visibleInputs = definition.Inputs.Where(x => !definition.IsJunction || !JunctionDerivedKeys.Contains(x.Key)).ToArray();
        _parameters.RowCount = Math.Max(1, visibleInputs.Length);
        if (visibleInputs.Length == 0)
        {
            _parameters.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var automatic = new Label { Text = "Coefficient inputs are calculated from the junction flow data below.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(72, 92, 108) };
            _parameters.Controls.Add(automatic, 0, 0);
            _parameters.SetColumnSpan(automatic, 2);
        }
        for (var index = 0; index < visibleInputs.Length; index++)
        {
            var spec = visibleInputs[index];
            Control control;
            if (spec.Choices is { Count: > 0 })
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = nameof(FittingInputChoice.Label) };
                combo.Items.AddRange(spec.Choices.Cast<object>().ToArray());
                var saved = !_initializing && !string.Equals(_original.FittingCode, definition.Code, StringComparison.OrdinalIgnoreCase)
                    ? spec.DefaultValue
                    : _original.Inputs.GetValueOrDefault(spec.Key, spec.DefaultValue);
                combo.SelectedItem = spec.Choices.OrderBy(x => Math.Abs(x.Value - saved)).First();
                combo.SelectedIndexChanged += (_, _) =>
                {
                    if (string.Equals(spec.Key, "sizeIndex", StringComparison.OrdinalIgnoreCase))
                        UpdateVavRowChoices();
                    InvalidateCalculation();
                };
                control = combo;
            }
            else
            {
                var saved = !_initializing && !string.Equals(_original.FittingCode, definition.Code, StringComparison.OrdinalIgnoreCase)
                    ? spec.DefaultValue
                    : _original.Inputs.GetValueOrDefault(spec.Key, spec.DefaultValue);
                var input = new TextBox { Text = saved.ToString("0.####", CultureInfo.CurrentCulture), Dock = DockStyle.Fill };
                input.TextChanged += (_, _) => InvalidateCalculation();
                control = input;
            }
            _inputControls[spec.Key] = control;
            _parameterInlineRows.Add(new InlineEditRow(
                string.IsNullOrWhiteSpace(spec.Unit) ? spec.Label : $"{spec.Label} ({spec.Unit})", control));
            _parameters.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _parameters.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(spec.Unit) ? spec.Label : $"{spec.Label} ({spec.Unit})",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, index);
            control.Margin = new Padding(4, 4, 2, 4);
            _parameters.Controls.Add(control, 1, index);
        }
        UpdateVavRowChoices();
        if (!_initializing)
            HandleJunctionFlowChanged(converging ? _straightFlow : _commonFlow);
        InlineRowsChanged?.Invoke();
    }

    private void UpdateVavRowChoices()
    {
        if (_definition.SelectedItem is not FittingDefinition definition
            || !string.Equals(definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
            || !_inputControls.TryGetValue("sizeIndex", out var sizeInput)
            || sizeInput is not ComboBox sizeCombo
            || sizeCombo.SelectedItem is not FittingInputChoice sizeChoice
            || !_inputControls.TryGetValue("rows", out var rowInput)
            || rowInput is not ComboBox rowCombo)
            return;
        var sizeIndex = Convert.ToInt32(sizeChoice.Value);
        if (sizeIndex < 0 || sizeIndex >= VavPerformanceCatalog.WithReheatSizes.Count)
            return;
        var previous = rowCombo.SelectedItem is FittingInputChoice selected ? Convert.ToInt32(selected.Value) : 3;
        var available = VavPerformanceCatalog.AvailableRows(VavPerformanceCatalog.WithReheatSizes[sizeIndex]);
        if (available.Count == 0)
            return;
        rowCombo.BeginUpdate();
        rowCombo.Items.Clear();
        foreach (var rows in available)
            rowCombo.Items.Add(new FittingInputChoice($"{rows} row{(rows == 1 ? "" : "s")}", rows));
        rowCombo.EndUpdate();
        rowCombo.SelectedIndex = available.Select((value, index) => (value, index))
            .OrderBy(item => Math.Abs(item.value - previous)).First().index;
    }

    private void UpdateShapeInputs()
    {
        var round = _shape.SelectedItem is DuctShape.Round;
        _diameter.Enabled = round;
        _width.Enabled = !round;
        _height.Enabled = !round;
    }

    private void InvalidateCalculation()
    {
        if (_initializing)
            return;
        Result = null;
        _updateButton.Enabled = false;
        _result.Text = _inline ? string.Empty : "Inputs changed — click Calculate before updating the fitting.";
        _result.ForeColor = Color.FromArgb(92, 76, 42);
    }

    public IReadOnlyList<InlineEditRow> InlineRows =>
        _baseInlineRows.Concat(_parameterInlineRows).Concat(_junctionInlineRows).ToArray();

    public event Action? InlineRowsChanged;

    public Control? InlineStatus => _result;

    public void UseInline(IWin32Window messageOwner)
    {
        _inline = true;
        // The gallery button keeps a short caption beside the fitting list in the narrow inspector.
        _galleryButton.Text = "Gallery…";
        _definitionHost.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, DpiScaling.Scale(74));
        _galleryButton.Margin = new Padding(DpiScaling.Scale(4), 0, 0, 0);
        _result.Text = string.Empty;
    }

    /// <summary>Calculates the fitting exactly as the Calculate button does; accepted when it succeeds.</summary>
    public bool TryAcceptInline()
    {
        CalculatePreview();
        return Result is not null;
    }

    private void HandleJunctionFlowChanged(TextBox changed)
    {
        if (_initializing || _updatingJunctionFlows || _definition.SelectedItem is not FittingDefinition definition)
            return;
        if (!definition.IsJunction)
        {
            InvalidateCalculation();
            return;
        }

        _updatingJunctionFlows = true;
        try
        {
            var group = FittingCatalog.SelectionGroup(definition);
            var converging = string.Equals(group, "Converging Fittings", StringComparison.OrdinalIgnoreCase);
            if (converging)
            {
                if (TryReadDisplayFlow(_straightFlow, out var straight) && TryReadDisplayFlow(_branchFlow, out var branch))
                {
                    SetFlow(_commonFlow, straight + branch);
                    SetFlow(_airflow, straight + branch);
                }
            }
            else
            {
                if (changed == _airflow && TryReadDisplayFlow(_airflow, out var airflow))
                    SetFlow(_commonFlow, airflow);
                if (TryReadDisplayFlow(_commonFlow, out var common))
                {
                    SetFlow(_airflow, common);
                    if (changed == _branchFlow && TryReadDisplayFlow(_branchFlow, out var branch))
                        SetFlow(_straightFlow, Math.Max(UnitConversions.CfmToM3PerS(0.01), common - branch));
                    else if (TryReadDisplayFlow(_straightFlow, out var straight))
                        SetFlow(_branchFlow, Math.Max(UnitConversions.CfmToM3PerS(0.01), common - straight));
                }
            }
        }
        finally
        {
            _updatingJunctionFlows = false;
        }
        InvalidateCalculation();
    }

    private bool TryReadDisplayFlow(TextBox input, out double cubicMetersPerSecond)
    {
        cubicMetersPerSecond = 0;
        if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || !double.IsFinite(value) || value < 0)
            return false;
        cubicMetersPerSecond = _units == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(value)
            : UnitConversions.LpsToM3PerS(value);
        return true;
    }

    private void CalculatePreview()
    {
        if (_initializing || _definition.SelectedItem is null)
            return;
        try
        {
            var fitting = ReadFitting();
            var calculation = fitting.Calculate();
            _result.Text = _units == UnitSystem.Imperial
                ? $"C {calculation.Coefficient:0.###}   •   VP {UnitConversions.PaToInchesWater(calculation.VelocityPressurePa):0.###} in. w.g.   •   Loss {UnitConversions.PaToInchesWater(calculation.PressureLossPa):0.###} in. w.g."
                : $"C {calculation.Coefficient:0.###}   •   VP {calculation.VelocityPressurePa:0.##} Pa   •   Loss {calculation.PressureLossPa:0.##} Pa";
            _result.ForeColor = Color.FromArgb(28, 69, 108);
            Result = fitting;
            _updateButton.Enabled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        {
            Result = null;
            _updateButton.Enabled = false;
            _result.Text = ex.Message;
            _result.ForeColor = Color.Firebrick;
        }
    }

    private SavedFitting ReadFitting()
    {
        var definition = SelectedDefinition;
        var airflow = ReadFlow(_airflow, "airflow");
        var temperatureDisplay = Number(_temperature, "air temperature");
        var temperature = _units == UnitSystem.Imperial ? UnitConversions.FahrenheitToCelsius(temperatureDisplay) : temperatureDisplay;
        var shape = _shape.SelectedItem is DuctShape selectedShape ? selectedShape : DuctShape.Round;
        var geometry = ReadGeometry(shape);
        var inputs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in definition.Inputs)
        {
            if (!_inputControls.TryGetValue(spec.Key, out var control))
            {
                inputs[spec.Key] = _original.Inputs.GetValueOrDefault(spec.Key, spec.DefaultValue);
                continue;
            }
            inputs[spec.Key] = control switch
            {
                ComboBox combo when combo.SelectedItem is FittingInputChoice choice => choice.Value,
                TextBox text => Number(text, spec.Label),
                _ => spec.DefaultValue
            };
        }

        JunctionFlowData? junction = null;
        if (definition.IsJunction)
        {
            var common = ReadFlow(_commonFlow, "common airflow");
            var straight = ReadFlow(_straightFlow, "straight airflow");
            var branch = ReadFlow(_branchFlow, "branch airflow");
            junction = new JunctionFlowData(
                _junctionPath.SelectedItem is JunctionPath path ? path : JunctionPath.Straight,
                common, geometry, straight, geometry, branch, geometry);
            airflow = common;
        }
        var description = string.Equals(definition.Code, _original.FittingCode, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_original.Description)
                ? _original.Description
                : $"{definition.Code} — {definition.Description}";
        var candidate = new SavedFitting
        {
            Sequence = _original.Sequence,
            CustomId = _original.CustomId,
            Description = description,
            AirSystemType = _original.AirSystemType,
            BranchId = _original.BranchId,
            FittingCode = definition.Code,
            Inputs = inputs,
            AirflowM3PerS = airflow,
            Shape = shape,
            DiameterM = geometry.DiameterM,
            WidthM = geometry.WidthM,
            HeightM = geometry.HeightM,
            TemperatureC = temperature,
            RelativeHumidity = _original.RelativeHumidity,
            AtmosphericPressurePa = _original.AtmosphericPressurePa,
            Junction = junction,
            DeltaXM = _original.DeltaXM,
            DeltaYM = _original.DeltaYM,
            DeltaZM = _original.DeltaZM
        };
        var result = candidate.Calculate();
        candidate.ReferenceVelocityMPerS = result.ReferenceVelocityMPerS;
        return candidate;
    }

    private DuctGeometry ReadGeometry(DuctShape shape)
    {
        if (shape == DuctShape.Round)
        {
            var diameter = SizeToMeters(Positive(_diameter, "diameter"));
            return GeometryCalculator.Round(diameter);
        }
        return GeometryCalculator.Rectangular(SizeToMeters(Positive(_width, "width")), SizeToMeters(Positive(_height, "height")));
    }

    private double SizeToMeters(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.InchesToM(value)
        : UnitConversions.MillimetersToM(value);

    private double ReadFlow(TextBox input, string name)
    {
        var value = Positive(input, name);
        return _units == UnitSystem.Imperial ? UnitConversions.CfmToM3PerS(value) : UnitConversions.LpsToM3PerS(value);
    }

    private void SetFlow(TextBox input, double cubicMetersPerSecond) => input.Text = (_units == UnitSystem.Imperial
        ? UnitConversions.M3PerSToCfm(cubicMetersPerSecond)
        : UnitConversions.M3PerSToLps(cubicMetersPerSecond)).ToString("0.##", CultureInfo.CurrentCulture);

    private void SaveAndClose()
    {
        if (Result is null)
        {
            MessageBox.Show(this, "Click Calculate and resolve any input errors before updating the fitting.",
                "Calculate Fitting First", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input, int height)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
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

    private static void AddCompact(TableLayoutPanel table, int column, string label, Control input)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Font = new Font("Segoe UI Semibold", 8F) }, column, 0);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(3);
        table.Controls.Add(input, column, 1);
    }
}
