using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed partial class DuctCalculatorScreen
{
    private readonly MainForm _host;

    internal DuctCalculatorScreen(MainForm host)
    {
        _host = host;
    }

    internal readonly ComboBox _modeCombo = MainForm.CreateDropDown();

    internal readonly ComboBox _shapeCombo = MainForm.CreateDropDown();

    internal readonly ComboBox _materialCombo = MainForm.CreateDropDown();

    internal readonly ComboBox _equivalentMethodCombo = MainForm.CreateDropDown();

    internal readonly ComboBox _pressureBasisCombo = MainForm.CreateDropDown();

    internal readonly TextBox _airflowText = MainForm.CreateInput("1000");

    internal readonly TextBox _temperatureText = MainForm.CreateInput("70");

    internal readonly TextBox _lengthText = MainForm.CreateInput("100");

    internal readonly TextBox _diameterText = MainForm.CreateInput("12");

    internal readonly TextBox _widthText = MainForm.CreateInput("24");

    internal readonly TextBox _heightText = MainForm.CreateInput("12");

    internal readonly ComboBox _rectangularFixedDimensionCombo = MainForm.CreateDropDown();

    internal readonly TextBox _rectangularFixedDimensionText = MainForm.CreateInput("16");

    internal readonly TextBox _targetFrictionText =MainForm. CreateInput("0.08");

    internal readonly TextBox _maximumVelocityText =MainForm. CreateInput("1500");

    internal readonly TextBox _sizeIncrementText = MainForm.CreateInput("0");

    internal readonly Label _diameterUnitLabel =MainForm. CreateInputUnitLabel();

    internal readonly Label _widthUnitLabel =MainForm. CreateInputUnitLabel();

    internal readonly Label _heightUnitLabel =MainForm. CreateInputUnitLabel();

    internal readonly Label _rectangularFixedDimensionUnitLabel =MainForm. CreateInputUnitLabel();

    private readonly Dictionary<string, Label> _inputLabels = new();

    private readonly Dictionary<string, Control> _inputControls = new();

    private readonly Dictionary<string, Label> _sectionHeaders = new();

    internal readonly SplitContainer _ductCalculatorSplitter = new();

    internal readonly RoundedPanel _inputCard = new();

    internal readonly RoundedPanel _resultsCard = new();

    internal readonly Label _ductEditingLabel = new();

    internal readonly TableLayoutPanel _summaryTable = new();

    internal readonly ListView _results = new();

    internal readonly Label _methodHintLabel = new();

    internal readonly Label _sizeSummaryValue =MainForm. CreateSummaryValue();

    internal readonly Label _velocitySummaryValue =MainForm. CreateSummaryValue();

    internal readonly Label _pressureSummaryValue =MainForm. CreateSummaryValue();

    internal readonly RichTextBox _ductCalculationDetails = new();

    internal readonly TextBox _assumptionsText = new();

    internal readonly ListViewGroup _geometryGroup = new("Geometry");

    internal readonly ListViewGroup _flowGroup = new("Flow");

    internal readonly ListViewGroup _pressureGroup = new("Pressure loss");

    internal readonly ListViewGroup _propertiesGroup = new("Air and material");

    private bool _stackedLayout;

    internal bool _ductCalculationsCollapsed;

    internal bool _ductCalculationsExpanded;

    internal bool _updatingDuctCalculatorSplitter;

    internal double _ductCalculatorSplitRatio = 0.46;

    internal double _ductCalculatorRestoreRatio = 0.46;

    internal readonly Button _ductCalculationsShowButton =MainForm. CreatePaneButton("Show Calculations", 172);

    internal readonly Button _ductCalculationsHideButton =MainForm. CreatePaneButton("Hide", 58);

    internal readonly Button _ductCalculationsExpandButton =MainForm. CreatePaneButton("Expand", 88);

    internal void UpdateUnitsLabels()
    {
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        _inputLabels["airflow"].Text = imperial ? "Airflow (cfm)" : "Airflow (L/s)";
        _inputLabels["temperature"].Text = imperial ? "Air Temp (°F)" : "Air Temp (°C)";
        _inputLabels["length"].Text = imperial ? "Length (ft)" : "Length (m)";
        _inputLabels["diameter"].Text = "Diameter";
        _inputLabels["width"].Text = "Width";
        _inputLabels["height"].Text = "Height";
        _diameterUnitLabel.Text = imperial ? "(in.)" : "(mm)";
        _widthUnitLabel.Text = imperial ? "(in.)" : "(mm)";
        _heightUnitLabel.Text = imperial ? "(in.)" : "(mm)";
        _rectangularFixedDimensionUnitLabel.Text = imperial ? "(in)" : "(mm)";
        _inputLabels["friction"].Text = imperial ? "Target SP (in. w.c./100 ft)" : "Target SP (Pa/m)";
        _inputLabels["velocity"].Text = imperial ? "Max Velocity (fpm)" : "Max Velocity (m/s)";
    }

    internal void UpdateControlState()
    {
        if (_shapeCombo.SelectedItem is null || _modeCombo.SelectedItem is null)
            return;

        ApplyMaterialShapeConstraint();
        var shape = MainForm.Selected(_shapeCombo, DuctShape.Round);
        var mode = MainForm.Selected(_modeCombo, CalculationMode.CheckEnteredSize);
        var checking = mode == CalculationMode.CheckEnteredSize;
        var rectangular = shape == DuctShape.Rectangular;

        SetInputVisible("diameter", checking && !rectangular);
        SetInputVisible("width", checking && rectangular);
        SetInputVisible("height", checking && rectangular);
        SetInputVisible("aspect", !checking && rectangular);
        SetInputVisible("friction", mode == CalculationMode.SizeByTargetFriction);
        SetInputVisible("velocity", mode == CalculationMode.SizeByMaximumVelocity);
        // Keep both method controls available. For round ducts the selections are
        // mathematically equivalent, but leaving them active makes the behavior
        // visible and prevents settings from appearing inaccessible.
        _equivalentMethodCombo.Enabled = true;
        _pressureBasisCombo.Enabled = true;
        _methodHintLabel.Text = rectangular
            ? "Equivalent-diameter, pressure-loss basis, and autosize increment are set in Project Settings > Calculation Settings."
            : "Project calculation defaults are set in Project Settings and apply automatically to new sizing calculations.";

        UpdateMaterialStatus();
    }

    internal void UpdateMaterialStatus()
    {
        if (_materialCombo.SelectedItem is not DuctMaterial material)
            return;

        var requiredShape = MainForm.RequiredShapeForMaterial(material);
        _host._statusLabel.Text = requiredShape is DuctShape.Round
            ? "Galvanized spiral duct is available only as a round duct."
            : requiredShape is DuctShape.Rectangular
                ? $"{material.Name} is available only as a rectangular duct."
                : material.Name.StartsWith("Flexible", StringComparison.OrdinalIgnoreCase)
            ? "Warning: the flexible-duct preset is valid only when the duct is fully extended."
            : material.Note;
    }

    internal void ApplyMaterialShapeConstraint()
    {
        if (_materialCombo.SelectedItem is not DuctMaterial material)
            return;
        var required = MainForm.RequiredShapeForMaterial(material);
        _shapeCombo.Enabled = required is null;
        if (required is null || MainForm.Selected(_shapeCombo, required.Value) == required.Value)
            return;
        var previousUpdating = _host._updatingControls;
        _host._updatingControls = true;
        try
        {
            _shapeCombo.SelectedItem = _shapeCombo.Items.Cast<object>()
                .OfType<MainForm.Choice<DuctShape>>()
                .First(choice => choice.Value == required.Value);
        }
        finally
        {
            _host._updatingControls = previousUpdating;
        }
    }

    internal void CalculateAndDisplay()
    {
        if (_host._updatingControls)
            return;

        try
        {
            var airflow = ReadAirflow();
            var temperatureC = ReadTemperature();
            var lengthM = ReadLength();
            var shape = MainForm.Selected(_shapeCombo, DuctShape.Round);
            var mode = MainForm.Selected(_modeCombo, CalculationMode.CheckEnteredSize);
            var material = (DuctMaterial)(_materialCombo.SelectedItem
                           ?? throw new InvalidOperationException("Select a duct material."));
            var equivalentMethod = MainForm.Selected(_equivalentMethodCombo, EquivalentDiameterMethod.Huebscher);
            var pressureBasis = MainForm.Selected(_pressureBasisCombo, PressureLossBasis.EquivalentRoundArea);

            DuctCalculationResult result;
            DuctGeometry? continuousGeometry = null;

            if (mode == CalculationMode.CheckEnteredSize)
            {
                var geometry = ReadEnteredGeometry(shape);
                result = DuctCalculator.Calculate(new DuctCalculationInput(
                    airflow,
                    temperatureC,
                    MainForm.FixedRelativeHumidity,
                    MainForm.FixedAtmosphericPressurePa,
                    geometry,
                    material,
                    equivalentMethod,
                    pressureBasis,
                    lengthM));
            }
            else
            {
                var fixedDimension = shape == DuctShape.Rectangular
                    ? MainForm.Selected(_rectangularFixedDimensionCombo, RectangularFixedDimension.Width)
                    : RectangularFixedDimension.None;
                var fixedDimensionDisplay = shape == DuctShape.Rectangular
                    ? MainForm.ReadPositive(_rectangularFixedDimensionText,
                        $"fixed {fixedDimension.ToString().ToLowerInvariant()}")
                    : 0.0;
                var fixedDimensionM = _host._currentUnits == UnitSystem.Imperial
                    ? UnitConversions.InchesToM(fixedDimensionDisplay)
                    : UnitConversions.MillimetersToM(fixedDimensionDisplay);
                var targetFriction = ReadTargetFriction();
                var maximumVelocity = ReadMaximumVelocity();
                var increment = ReadSizeIncrement();
                var sizing = DuctSizingSolver.Size(new DuctSizingInput(
                    airflow,
                    temperatureC,
                    MainForm.FixedRelativeHumidity,
                    MainForm.FixedAtmosphericPressurePa,
                    shape,
                    1.0,
                    material,
                    equivalentMethod,
                    pressureBasis,
                    lengthM,
                    mode,
                    targetFriction,
                    maximumVelocity,
                    increment,
                    _host.DuctSystemScreen._ductSystem.DefaultSizeRoundingDirection,
                    fixedDimension,
                    fixedDimensionM));
                continuousGeometry = sizing.ContinuousGeometry;
                result = sizing.Calculation;
            }

            DisplayResults(result, continuousGeometry);
            _host.DuctSystemScreen._lastCalculation = result;

            var warning = result.Friction.Regime == FlowRegime.Transitional
                ? "Transitional flow: pressure loss is shown as unblended laminar-to-turbulent bounds."
                : "Calculation completed.";
            if (material.Name.StartsWith("Flexible", StringComparison.OrdinalIgnoreCase))
                warning += " Flex-duct result assumes fully extended installation.";
            if (continuousGeometry is not null
                && result.Input.Geometry.ActualAreaM2 < continuousGeometry.ActualAreaM2 - 1e-12)
                warning += " Rounded to a smaller nominal size: the resulting velocity or pressure rate may exceed the selected design target.";
            _host._statusLabel.Text = warning;
            _host._statusLabel.ForeColor = result.Friction.Regime == FlowRegime.Transitional
                ? Color.DarkOrange
                : Color.FromArgb(30, 100, 55);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            _host.DuctSystemScreen._lastCalculation = null;
            _results.Items.Clear();
            _sizeSummaryValue.Text = "—";
            _velocitySummaryValue.Text = "—";
            _pressureSummaryValue.Text = "—";
            _host._statusLabel.Text = ex.Message;
            _host._statusLabel.ForeColor = Color.Firebrick;
        }
    }

    private void DisplayResults(DuctCalculationResult result, DuctGeometry? continuousGeometry)
    {
        _results.BeginUpdate();
        try
        {
            _results.Items.Clear();
            var geometry = result.Input.Geometry;
            _sizeSummaryValue.Text = FormatGeometry(geometry);
            _velocitySummaryValue.Text = FormatVelocity(result.ActualVelocityMPerS);
            _pressureSummaryValue.Text = FormatPressureRateSummary(result);

            AddResult("Duct size", FormatGeometry(geometry), _geometryGroup);
            if (continuousGeometry is not null && !MainForm.GeometryMatches(geometry, continuousGeometry))
                AddResult("Exact calculated size", FormatGeometry(continuousGeometry), _geometryGroup);
            AddResult("Actual area", FormatArea(geometry.ActualAreaM2), _geometryGroup);
            AddResult("Equivalent-round area", FormatArea(result.EquivalentAreaM2), _geometryGroup);
            AddResult("Hydraulic diameter", FormatLength(result.Input.Geometry.HydraulicDiameterM), _geometryGroup);
            AddResult("Huebscher equivalent diameter", FormatLength(result.Input.Geometry.HuebscherDiameterM), _geometryGroup);
            AddResult("Selected equivalent diameter", FormatLength(result.EquivalentDiameterM), _geometryGroup);
            AddResult("Calculation diameter", FormatLength(result.CalculationDiameterM), _geometryGroup);

            AddResult("Actual velocity", FormatVelocity(result.ActualVelocityMPerS), _flowGroup);
            AddResult("Equivalent-round velocity", FormatVelocity(result.EquivalentVelocityMPerS), _flowGroup);
            AddResult("Reynolds number", $"{result.ReynoldsNumber:N0}", _flowGroup);
            AddResult("Flow regime", result.Friction.Regime.ToString(), _flowGroup);

            AddResult("Darcy friction factor", FormatFrictionFactor(result.Friction), _pressureGroup);
            AddResult("Friction method", result.Friction.Method, _pressureGroup);
            AddResult("Pressure-loss basis", result.CalculationBasisDescription, _pressureGroup);
            AddResult("Straight-duct pressure rate", FormatPressureRate(result), _pressureGroup);
            AddResult($"Pressure loss for {FormatDuctLength(result.Input.LengthM)}", FormatTotalPressure(result), _pressureGroup);

            AddResult("Air density", $"{result.Air.DensityKgPerM3:0.#####} kg/m³", _propertiesGroup);
            AddResult("Dynamic viscosity", $"{result.Air.DynamicViscosityPaS:0.00000000} Pa·s", _propertiesGroup);
            AddResult("Duct material", result.Input.Material.Name, _propertiesGroup);
            AddResult("Absolute roughness", $"{result.Input.Material.RoughnessFeet:0.000000} ft  ({result.Input.Material.RoughnessMeters * 1000.0:0.####} mm)", _propertiesGroup);
            UpdateDuctCalculationDetails(result, continuousGeometry);
        }
        finally
        {
            _results.EndUpdate();
        }
    }

    private void UpdateDuctCalculationDetails(DuctCalculationResult result, DuctGeometry? continuousGeometry)
    {
        var box = _ductCalculationDetails;
        box.Clear();

        void Heading(string text)
        {
            if (box.TextLength > 0)
                EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            EngineeringMathRichText.AppendAtEnd(box, text + Environment.NewLine);
            box.SelectionFont = box.Font;
            box.SelectionColor = box.ForeColor;
        }

        void Line(params (string Text, bool Input)[] parts)
        {
            foreach (var part in parts)
            {
                box.SelectionColor = part.Input ? Color.FromArgb(190, 44, 44) : box.ForeColor;
                box.SelectionFont = part.Input ? new Font(box.Font, FontStyle.Bold) : box.Font;
                EngineeringMathRichText.AppendAtEnd(box, part.Text);
            }
            EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
        }

        void MathLine(params (string Markup, bool Input)[] parts) =>
            EngineeringMathRichText.AppendEquation(box, parts);

        void StyledMathLine(params (string Markup, EngineeringMathRichText.MathTone Tone)[] parts) =>
            EngineeringMathRichText.AppendStyledEquation(box, parts);

        void DefinitionRow(string left, string right)
        {
            box.SelectionTabs = new[] { Math.Max(250, box.ClientSize.Width / 2) };
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
            EngineeringMathRichText.AppendAtEnd(box, $"{left}\t{right}{Environment.NewLine}");
        }

        void Step(string title, string explanation)
        {
            EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            EngineeringMathRichText.AppendAtEnd(box, title + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
            EngineeringMathRichText.AppendAtEnd(box, explanation + Environment.NewLine);
        }

        void NarrativeLine(params (string Text, EngineeringMathRichText.MathTone Tone)[] parts)
        {
            foreach (var part in parts)
            {
                box.SelectionColor = part.Tone switch
                {
                    EngineeringMathRichText.MathTone.Input => Color.FromArgb(190, 44, 44),
                    EngineeringMathRichText.MathTone.Solved => Color.FromArgb(0, 105, 154),
                    _ => box.ForeColor
                };
                box.SelectionFont = part.Tone == EngineeringMathRichText.MathTone.Formula
                    ? box.Font
                    : new Font(box.Font, FontStyle.Bold);
                EngineeringMathRichText.AppendAtEnd(box, part.Text);
            }
            EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
        }

        var geometry = result.Input.Geometry;
        var flowCfm = UnitConversions.M3PerSToCfm(result.Input.AirflowM3PerS);
        var temperatureF = UnitConversions.CelsiusToFahrenheit(result.Input.TemperatureC);
        var lengthFt = UnitConversions.MToFeet(result.Input.LengthM);
        var areaFt2 = UnitConversions.SquareMetersToSquareFeet(geometry.ActualAreaM2);
        var densityLbmFt3 = result.Air.DensityKgPerM3 * 0.0624279606;
        var viscosityLbmFtS = result.Air.DynamicViscosityPaS * 0.671968975;
        var actualVelocityFpm = UnitConversions.MpsToFpm(result.ActualVelocityMPerS);
        var calculationVelocityFpm = UnitConversions.MpsToFpm(result.CalculationVelocityMPerS);
        var calculationDiameterIn = UnitConversions.MToInches(result.CalculationDiameterM);
        var velocityPressureInWc = UnitConversions.PaToInchesWater(
            result.Air.DensityKgPerM3 * result.CalculationVelocityMPerS * result.CalculationVelocityMPerS / 2.0);
        var calculationDiameterFt = UnitConversions.MToFeet(result.CalculationDiameterM);
        var actualDiameterFt = geometry.Shape == DuctShape.Round
            ? UnitConversions.MToFeet(geometry.DiameterM)
            : calculationDiameterFt;
        var imperialPressureRate = result.Friction.Regime == FlowRegime.Transitional
            ? $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossLowerPaPerM):0.####}–{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM):0.####} in. w.c./100 ft"
            : $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM):0.####} in. w.c./100 ft";
        var imperialTotalPressure = result.Friction.Regime == FlowRegime.Transitional
            ? $"{UnitConversions.PaToInchesWater(result.TotalPressureLossLowerPa):0.####}–{UnitConversions.PaToInchesWater(result.TotalPressureLossUpperPa):0.####} in. w.c."
            : $"{UnitConversions.PaToInchesWater(result.TotalPressureLossPa):0.####} in. w.c.";
        var relativeRoughness = result.Input.Material.RoughnessMeters / result.CalculationDiameterM;

        Heading("REQUIRED EQUATIONS");
        MathLine(("\\rho = \\rho_{moist}(T, RH, p_{atm})", false));
        MathLine(("\\mu = \\mu_{Sutherland}(T)", false));
        if (geometry.Shape == DuctShape.Round)
        {
            MathLine(("A = \\frac{\\pi D^{2}}{4}", false));
        }
        else
        {
            MathLine(("A = W \\times H", false));
            var equivalentEquation = result.Input.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher
                ? "D_{e} = \\frac{1.30(WH)^{0.625}}{(W+H)^{0.25}}"
                : "D_{h} = \\frac{2WH}{W+H}";
            MathLine((equivalentEquation, false));
        }
        MathLine(("V_{actual} = \\frac{Q}{A_{actual}}", false));
        MathLine(("V_{calc} = \\frac{Q}{A_{calc}}", false));
        MathLine(("\\epsilon_{r} = \\frac{\\epsilon}{D_{calc}}", false));
        MathLine(("Re = \\frac{\\rho V_{calc}D_{calc}}{\\mu}", false));
        if (result.Friction.Regime == FlowRegime.Laminar)
        {
            MathLine((geometry.Shape == DuctShape.Round
                ? "f = \\frac{64}{Re}"
                : "f = \\frac{96 \\cdot F(aspect ratio)}{Re}", false));
        }
        else
        {
            MathLine(("\\frac{1}{\\sqrt{f}} = -2 log_{10}(X)", false));
            MathLine(("X = \\frac{\\epsilon}{3.7D} + \\frac{2.51}{Re\\sqrt{f}}", false));
        }
        MathLine(("VP = (\\frac{V_{calc}}{4005})^{2}(\\frac{\\rho}{0.075})", false));
        MathLine(("\\frac{\\Delta P}{100 ft} = f(\\frac{100 ft}{D_{calc}})VP", false));
        MathLine(("\\Delta P = (\\frac{\\Delta P}{100 ft})(\\frac{L}{100 ft})", false));

        Heading("PARAMETERS AND SUB-EQUATIONS");
        DefinitionRow("Q  Airflow", "A  Duct area");
        DefinitionRow("V  Air velocity", "Dcalc  Calculation diameter");
        DefinitionRow("ρ  Moist-air density", "μ  Dynamic viscosity");
        DefinitionRow("ε  Absolute roughness", "εr  Relative roughness");
        DefinitionRow("Re  Reynolds number", "f  Darcy friction factor");
        DefinitionRow("VP  Velocity pressure", "ΔP  Static-pressure loss");
        Line(("Calculation basis: ", false), (result.CalculationBasisDescription, false));
        MathLine(("A_{calc} = A(D_{calc})", false));
        MathLine(("V_{ft/s} = \\frac{V_{fpm}}{60}", false));
        Line((result.Friction.Regime == FlowRegime.Laminar
            ? "Laminar friction uses a direct correlation; iteration is not required."
            : "Turbulent friction uses the implicit Colebrook equation with safeguarded bisection.", false));

        Heading("INPUT VALUES");
        Line(("Airflow Q = ", false), ($"{flowCfm:0.##} cfm", true));
        Line(("Air temperature T = ", false), ($"{temperatureF:0.##} °F", true));
        Line(("Relative humidity RH = ", false), ($"{result.Input.RelativeHumidity:P0}", true));
        Line(("Atmospheric pressure p_atm = ", false),
            ($"{result.Input.AtmosphericPressurePa * 0.000145037738:0.###} psia", true));
        Line(("Duct length L = ", false), ($"{lengthFt:0.###} ft", true));
        Line(("Duct geometry = ", false), (ImperialGeometry(geometry), true));
        Line(("Material roughness ε = ", false), ($"{result.Input.Material.RoughnessFeet:0.######} ft", true));
        if (result.Friction.Regime != FlowRegime.Laminar)
            Line(("Initial friction-factor bracket = ", false),
                ($"[{result.Friction.InitialLowerBound:0.########}, {result.Friction.InitialUpperBound:0.########}]", true));

        Heading("SOLVED PARAMETERS — EQUATION SUBSTITUTIONS");
        Line(("Entered values are red; solved results are blue.", false), ("  INPUT", true), ("  ", false));
        box.SelectionColor = Color.FromArgb(0, 105, 154);
        box.SelectionFont = new Font(box.Font, FontStyle.Bold);
        EngineeringMathRichText.AppendAtEnd(box, "SOLVED" + Environment.NewLine);
        box.SelectionColor = box.ForeColor;
        box.SelectionFont = box.Font;
        Step("1. FINAL DUCT GEOMETRY",
            "The sizing method first establishes the duct dimensions. Project rounding is applied before the final area and all downstream velocity calculations are evaluated.");
        if (continuousGeometry is not null && !MainForm.GeometryMatches(geometry, continuousGeometry))
        {
            StyledMathLine(("Continuous solved duct size = ", EngineeringMathRichText.MathTone.Formula),
                (ImperialGeometry(continuousGeometry), EngineeringMathRichText.MathTone.Solved));
            NarrativeLine(("The continuous value is the unrounded design result. The project increment and rounding rule convert it to the constructible final size below.", EngineeringMathRichText.MathTone.Formula));
        }
        StyledMathLine(("Final duct size = ", EngineeringMathRichText.MathTone.Formula),
            (ImperialGeometry(geometry), EngineeringMathRichText.MathTone.Solved));
        if (geometry.Shape == DuctShape.Round)
        {
            StyledMathLine(("A = \\frac{\\pi D^{2}}{4} = ", EngineeringMathRichText.MathTone.Formula),
                ($"\\frac{{\\pi({actualDiameterFt:0.####} ft)^{{2}}}}{{4}}", EngineeringMathRichText.MathTone.Input),
                (" = ", EngineeringMathRichText.MathTone.Formula),
                ($"{areaFt2:0.####} ft²", EngineeringMathRichText.MathTone.Solved));
        }
        else
        {
            var widthFt = UnitConversions.MToFeet(geometry.WidthM);
            var heightFt = UnitConversions.MToFeet(geometry.HeightM);
            StyledMathLine(("A = W \\times H = ", EngineeringMathRichText.MathTone.Formula),
                ($"{widthFt:0.####} ft \\times {heightFt:0.####} ft", EngineeringMathRichText.MathTone.Input),
                (" = ", EngineeringMathRichText.MathTone.Formula),
                ($"{areaFt2:0.####} ft²", EngineeringMathRichText.MathTone.Solved));
        }
        StyledMathLine(("D_{calc} = D_{basis}(", EngineeringMathRichText.MathTone.Formula),
            (ImperialGeometry(geometry), EngineeringMathRichText.MathTone.Input),
            (") = ", EngineeringMathRichText.MathTone.Formula),
            ($"{calculationDiameterIn:0.###} in", EngineeringMathRichText.MathTone.Solved));

        Step("2. AIR PROPERTIES",
            "Temperature, relative humidity, and atmospheric pressure determine moist-air density. Dynamic viscosity is then evaluated from temperature with the Sutherland relation.");
        StyledMathLine(("\\rho = \\rho_{moist}(", EngineeringMathRichText.MathTone.Formula),
            ($"{temperatureF:0.##} °F, {result.Input.RelativeHumidity:P0}, {result.Input.AtmosphericPressurePa * 0.000145037738:0.###} psia", EngineeringMathRichText.MathTone.Input),
            (") = ", EngineeringMathRichText.MathTone.Formula),
            ($"{densityLbmFt3:0.#####} lbm/ft³", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("\\mu = \\mu_{Sutherland}(", EngineeringMathRichText.MathTone.Formula),
            ($"{temperatureF:0.##} °F", EngineeringMathRichText.MathTone.Input),
            (") = ", EngineeringMathRichText.MathTone.Formula),
            ($"{viscosityLbmFtS:0.########} lbm/(ft·s)", EngineeringMathRichText.MathTone.Solved));
        Step("3. VELOCITY AND FLOW REGIME",
            "Airflow divided by area gives velocity. The calculation-basis diameter, roughness, density, and viscosity then establish Reynolds number and the applicable flow regime.");
        StyledMathLine(("V_{actual} = \\frac{Q}{A_{actual}} = ", EngineeringMathRichText.MathTone.Formula),
            ($"\\frac{{{flowCfm:0.##} cfm}}{{{areaFt2:0.####} ft²}}", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{actualVelocityFpm:0.#} fpm", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("V_{calc} = \\frac{Q}{A_{calc}} = ", EngineeringMathRichText.MathTone.Formula),
            ($"\\frac{{{flowCfm:0.##} cfm}}{{A({calculationDiameterIn:0.###} in)}}", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{calculationVelocityFpm:0.#} fpm", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("\\epsilon_{r} = \\frac{\\epsilon}{D_{calc}} = ", EngineeringMathRichText.MathTone.Formula),
            ($"\\frac{{{result.Input.Material.RoughnessFeet:0.######} ft}}{{{calculationDiameterFt:0.####} ft}}", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{relativeRoughness:0.########}", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("Re = \\frac{\\rho V_{calc}D_{calc}}{\\mu} = ", EngineeringMathRichText.MathTone.Formula),
            ($"\\frac{{{densityLbmFt3:0.#####} \\times {calculationVelocityFpm / 60.0:0.####} \\times {calculationDiameterFt:0.####}}}{{{viscosityLbmFtS:0.########}}}", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{result.ReynoldsNumber:0}", EngineeringMathRichText.MathTone.Solved));
        var regimeExplanation = result.Friction.Regime switch
        {
            FlowRegime.Laminar => $"Re = {result.ReynoldsNumber:0} is below {FrictionModel.LaminarUpperReynolds:0}, so the flow is laminar and a direct laminar correlation is used.",
            FlowRegime.Transitional => $"Re = {result.ReynoldsNumber:0} lies between {FrictionModel.LaminarUpperReynolds:0} and {FrictionModel.TurbulentLowerReynolds:0}. The worksheet therefore reports unblended laminar and Colebrook bounds.",
            _ => $"Re = {result.ReynoldsNumber:0} exceeds {FrictionModel.TurbulentLowerReynolds:0}, so the flow is turbulent and the implicit Colebrook equation applies."
        };
        NarrativeLine((regimeExplanation, EngineeringMathRichText.MathTone.Formula));

        Step("4. DARCY FRICTION FACTOR",
            result.Friction.Regime == FlowRegime.Laminar
                ? "Laminar flow uses the direct Darcy-factor correlation, so no iterative solve is required."
                : "The implicit Colebrook relation is solved with safeguarded bisection after a valid friction-factor bracket is established.");
        StyledMathLine((result.Friction.Regime == FlowRegime.Laminar ? "f = \\frac{64}{Re}; Re = " : "Colebrook(\\epsilon_{r}, Re); ", EngineeringMathRichText.MathTone.Formula),
            (result.Friction.Regime == FlowRegime.Laminar ? $"{result.ReynoldsNumber:0}" : $"εr = {relativeRoughness:0.########}, Re = {result.ReynoldsNumber:0}", EngineeringMathRichText.MathTone.Input),
            (" → f = ", EngineeringMathRichText.MathTone.Formula),
            (FormatFrictionFactor(result.Friction), EngineeringMathRichText.MathTone.Solved));
        if (result.Friction.Regime != FlowRegime.Laminar)
        {
            NarrativeLine(("Starting bracket: ", EngineeringMathRichText.MathTone.Formula),
                ($"f = {result.Friction.InitialLowerBound:0.########} to {result.Friction.InitialUpperBound:0.########}", EngineeringMathRichText.MathTone.Input),
                (". Safeguarded bisection required ", EngineeringMathRichText.MathTone.Formula),
                ($"{result.Friction.Iterations} iterations", EngineeringMathRichText.MathTone.Solved),
                (" and finished with residual ", EngineeringMathRichText.MathTone.Formula),
                ($"{result.Friction.FinalResidual:0.000E+00}", EngineeringMathRichText.MathTone.Solved),
                (". Convergence is accepted when |R| ≤ 1.0E−13 or the friction-factor bracket reaches its relative-width tolerance.", EngineeringMathRichText.MathTone.Formula));
        }
        Step("5. VELOCITY PRESSURE AND DUCT LOSS",
            "The solved velocity and density produce velocity pressure. Darcy friction converts velocity pressure into loss per 100 ft, and duct length produces the final straight-duct pressure loss.");
        StyledMathLine(("VP = (\\frac{V_{calc}}{4005})^{2}(\\frac{\\rho}{0.075}) = ", EngineeringMathRichText.MathTone.Formula),
            ($"(\\frac{{{calculationVelocityFpm:0.#}}}{{4005}})^{{2}}(\\frac{{{densityLbmFt3:0.#####}}}{{0.075}})", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{velocityPressureInWc:0.####} in. w.c.", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("\\frac{\\Delta P}{100 ft} = f(\\frac{100 ft}{D_{calc}})VP = ", EngineeringMathRichText.MathTone.Formula),
            ($"{FormatFrictionFactor(result.Friction)}(\\frac{{100 ft}}{{{calculationDiameterFt:0.####} ft}})({velocityPressureInWc:0.####})", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            (imperialPressureRate, EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("\\Delta P = (\\frac{\\Delta P}{100 ft})(\\frac{L}{100 ft}) = ", EngineeringMathRichText.MathTone.Formula),
            ($"({imperialPressureRate.Replace(" in. w.c./100 ft", string.Empty)})(\\frac{{{lengthFt:0.###} ft}}{{100 ft}})", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            (imperialTotalPressure, EngineeringMathRichText.MathTone.Solved));
        Heading("ASSUMPTIONS");
        Line(("Steady, fully developed flow; fixed 50% RH; sea-level pressure. Transitional flow is reported as unblended laminar and Colebrook bounds.", false));
        box.SelectionStart = 0;
        box.ScrollToCaret();

        static string ImperialGeometry(DuctGeometry value) => value.Shape == DuctShape.Round
            ? $"{UnitConversions.MToInches(value.DiameterM):0.###} in round"
            : $"{UnitConversions.MToInches(value.WidthM):0.###} × {UnitConversions.MToInches(value.HeightM):0.###} in";
    }

    internal string FormatGeometry(DuctGeometry geometry)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            return geometry.Shape == DuctShape.Round
                ? $"{UnitConversions.MToInches(geometry.DiameterM):0.###} in round"
                : $"{UnitConversions.MToInches(geometry.WidthM):0.###} × {UnitConversions.MToInches(geometry.HeightM):0.###} in";
        }

        return geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToMillimeters(geometry.DiameterM):0.#} mm round"
            : $"{UnitConversions.MToMillimeters(geometry.WidthM):0.#} × {UnitConversions.MToMillimeters(geometry.HeightM):0.#} mm";
    }

    private string FormatLength(double meters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToInches(meters):0.###} in"
        : $"{UnitConversions.MToMillimeters(meters):0.#} mm";

    private string FormatDuctLength(double meters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.###} ft"
        : $"{meters:0.###} m";

    private string FormatArea(double squareMeters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.SquareMetersToSquareFeet(squareMeters):0.####} ft²"
        : $"{squareMeters:0.#####} m²";

    private string FormatVelocity(double metersPerSecond) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MpsToFpm(metersPerSecond):N0} fpm"
        : $"{metersPerSecond:0.###} m/s";

    private string FormatPressureRate(DuctCalculationResult result)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossLowerPaPerM):0.####}–{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM):0.####} in. w.c./100 ft"
                : $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM):0.####} in. w.c./100 ft";
        }

        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.PressureLossLowerPaPerM:0.####}–{result.PressureLossUpperPaPerM:0.####} Pa/m"
            : $"{result.PressureLossPaPerM:0.####} Pa/m";
    }

    private string FormatPressureRateSummary(DuctCalculationResult result)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossLowerPaPerM):0.###}–{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM):0.###} in. w.c./100 ft"
                : $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM):0.####} in. w.c./100 ft";
        }

        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.PressureLossLowerPaPerM:0.###}–{result.PressureLossUpperPaPerM:0.###} Pa/m"
            : $"{result.PressureLossPaPerM:0.####} Pa/m";
    }

    private string FormatTotalPressure(DuctCalculationResult result)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaToInchesWater(result.TotalPressureLossLowerPa):0.####}–{UnitConversions.PaToInchesWater(result.TotalPressureLossUpperPa):0.####} in. w.c."
                : $"{UnitConversions.PaToInchesWater(result.TotalPressureLossPa):0.####} in. w.c.";
        }

        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.TotalPressureLossLowerPa:0.###}–{result.TotalPressureLossUpperPa:0.###} Pa"
            : $"{result.TotalPressureLossPa:0.###} Pa";
    }

    private static string FormatFrictionFactor(FrictionFactorResult friction) =>
        friction.Factor.HasValue
            ? friction.Factor.Value.ToString("0.000000", CultureInfo.InvariantCulture)
            : $"{friction.LowerFactor:0.000000}–{friction.UpperFactor:0.000000}";

    private double ReadAirflow()
    {
        var value = MainForm.ReadPositive(_airflowText, "airflow");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(value)
            : UnitConversions.LpsToM3PerS(value);
    }

    private double ReadTemperature()
    {
        var value = MainForm.ReadFinite(_temperatureText, "air temperature");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.FahrenheitToCelsius(value)
            : value;
    }

    private double ReadLength()
    {
        var value = MainForm.ReadPositive(_lengthText, "duct length");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.FeetToM(value)
            : value;
    }

    private DuctGeometry ReadEnteredGeometry(DuctShape shape)
    {
        if (shape == DuctShape.Round)
        {
            var diameter = MainForm.ReadPositive(_diameterText, "diameter");
            return GeometryCalculator.Round(_host._currentUnits == UnitSystem.Imperial
                ? UnitConversions.InchesToM(diameter)
                : UnitConversions.MillimetersToM(diameter));
        }

        var width = MainForm.ReadPositive(_widthText, "width");
        var height = MainForm.ReadPositive(_heightText, "height");
        return GeometryCalculator.Rectangular(
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(width) : UnitConversions.MillimetersToM(width),
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(height) : UnitConversions.MillimetersToM(height));
    }

    private double ReadTargetFriction()
    {
        if (MainForm.Selected(_modeCombo, CalculationMode.CheckEnteredSize) != CalculationMode.SizeByTargetFriction)
            return 1.0;
        var value = MainForm.ReadPositive(_targetFrictionText, "target friction");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.InchesWaterPer100FeetToPaPerM(value)
            : value;
    }

    private double ReadMaximumVelocity()
    {
        if (MainForm.Selected(_modeCombo, CalculationMode.CheckEnteredSize) != CalculationMode.SizeByMaximumVelocity)
            return 1.0;
        var value = MainForm.ReadPositive(_maximumVelocityText, "maximum velocity");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.FpmToMps(value)
            : value;
    }

    private double ReadSizeIncrement()
    {
        var value = MainForm.ReadNonNegative(_sizeIncrementText, "rounding increment");
        return _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.InchesToM(value)
            : UnitConversions.MillimetersToM(value);
    }

    private void CopyResults()
    {
        if (_results.Items.Count == 0)
            return;

        var builder = new StringBuilder("FanWorks results");
        builder.AppendLine();
        foreach (ListViewItem item in _results.Items)
            builder.AppendLine($"{item.Text}: {item.SubItems[1].Text}");
        builder.AppendLine();
        builder.Append(_assumptionsText.Text);

        try
        {
            Clipboard.SetText(builder.ToString());
            _host._statusLabel.Text = "Results copied to the clipboard.";
            _host._statusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (ExternalException ex)
        {
            _host._statusLabel.Text = $"The clipboard is unavailable: {ex.Message}";
            _host._statusLabel.ForeColor = Color.Firebrick;
        }
    }

    private void AddResult(string name, string value, ListViewGroup group)
    {
        var item = new ListViewItem(name, group);
        item.SubItems.Add(value);
        _results.Items.Add(item);
    }

    internal void AddInputSection(TableLayoutPanel table, string key, string text)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Color.FromArgb(65, 91, 115),
            Font = new Font("Segoe UI Semibold", 8F),
            Padding = new Padding(0, 11, 0, 3),
            Margin = new Padding(0)
        };
        table.Controls.Add(label, 0, row);
        table.SetColumnSpan(label, table.ColumnCount);
        _sectionHeaders[key] = label;
    }

    internal void AddInputRow(TableLayoutPanel table, string key, string labelText, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Name = $"InputLabel_{key}",
            Text = labelText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(45, 58, 70),
            Margin = new Padding(0, 6, 4, 4)
        };
        MainForm.ConfigureCompactInputControl(control, 250, 135);
        control.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(label, 0, row);
        if (table.ColumnCount >= 6)
        {
            table.SetColumnSpan(label, 2);
            table.Controls.Add(control, 2, row);
            table.SetColumnSpan(control, table.ColumnCount - 2);
        }
        else
        {
            table.Controls.Add(control, 1, row);
            table.SetColumnSpan(control, Math.Max(1, table.ColumnCount - 1));
        }
        _inputLabels[key] = label;
        _inputControls[key] = control;
    }

    internal void AddInputPairRow(TableLayoutPanel table,
        (string Key, string Label, Control Control) first,
        (string Key, string Label, Control Control)? second = null)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AddPair(first, 0);
        if (second is { } secondValue)
            AddPair(secondValue, table.ColumnCount >= 6 ? 3 : 2);
        return;

        void AddPair((string Key, string Label, Control Control) item, int column)
        {
            var label = new Label
            {
                Name = $"InputLabel_{item.Key}",
                Text = item.Label,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(45, 58, 70),
                Font = new Font("Segoe UI", 8.3F),
                Margin = new Padding(column == 0 ? 0 : 8, 6, 3, 4)
            };
            MainForm.ConfigureCompactInputControl(item.Control, 165, 120);
            item.Control.Margin = new Padding(0, 3, 0, 3);
            table.Controls.Add(label, column, row);
            table.Controls.Add(item.Control, column + 1, row);
            _inputLabels[item.Key] = label;
            _inputControls[item.Key] = item.Control;
        }
    }

    internal void AddRectangularSizingDimensionRow(TableLayoutPanel table)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rectangularFixedDimensionCombo.Width = 112;
        _rectangularFixedDimensionCombo.Margin = new Padding(0, 0, 6, 0);
        _rectangularFixedDimensionText.Width = 94;
        _rectangularFixedDimensionText.Margin = Padding.Empty;
        _rectangularFixedDimensionUnitLabel.Margin = new Padding(0, 4, 6, 0);
        var host = new FlowLayoutPanel
        {
            Name = "RectangularSizingDimensionHost",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 3, 0, 3),
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        host.Controls.Add(_rectangularFixedDimensionCombo);
        host.Controls.Add(_rectangularFixedDimensionUnitLabel);
        host.Controls.Add(_rectangularFixedDimensionText);
        table.Controls.Add(host, 0, row);
        table.SetColumnSpan(host, table.ColumnCount);
        _inputControls["aspect"] = host;
    }

    private void AddInputTripleRow(TableLayoutPanel table,
        (string Key, string Label, Control Control) first,
        (string Key, string Label, Control Control) second,
        (string Key, string Label, Control Control) third)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AddPair(first, 0);
        AddPair(second, 2);
        AddPair(third, 4);
        return;

        void AddPair((string Key, string Label, Control Control) item, int column)
        {
            var label = new Label
            {
                Name = $"InputLabel_{item.Key}",
                Text = item.Label,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(45, 58, 70),
                Font = new Font("Segoe UI", 8F),
                Margin = new Padding(column == 0 ? 0 : 4, 6, 3, 4)
            };
            MainForm.ConfigureCompactInputControl(item.Control, 82, 76);
            item.Control.Margin = new Padding(0, 3, 0, 3);
            table.Controls.Add(label, column, row);
            table.Controls.Add(item.Control, column + 1, row);
            _inputLabels[item.Key] = label;
            _inputControls[item.Key] = item.Control;
        }
    }

    private void SetInputVisible(string key, bool visible)
    {
        if (_inputLabels.TryGetValue(key, out var label))
            label.Visible = visible;
        if (_inputControls.TryGetValue(key, out var control))
            control.Visible = visible;
    }

    private void SetSectionVisible(string key, bool visible)
    {
        if (_sectionHeaders.TryGetValue(key, out var label))
            label.Visible = visible;
    }

    internal void ApplyResponsiveLayout(bool force = false)
    {
        var shouldStack = _host.ClientSize.Width < DpiScaling.Scale(980);
        if (!force && _host._contentLayout.Controls.Count > 0 && shouldStack == _stackedLayout)
            return;

        _stackedLayout = shouldStack;
        _host._contentLayout.SuspendLayout();
        try
        {
            _host._contentLayout.Controls.Clear();
            _host._contentLayout.ColumnStyles.Clear();
            _host._contentLayout.RowStyles.Clear();

            _ductCalculationsShowButton.Visible = _ductCalculationsCollapsed;
            _resultsCard.Visible = !_ductCalculationsCollapsed;
            if (_ductCalculationsCollapsed)
            {
                _host._contentLayout.ColumnCount = 1;
                _host._contentLayout.RowCount = 1;
                _host._contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _host._contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                _inputCard.Margin = Padding.Empty;
                _host._contentLayout.Controls.Add(_inputCard, 0, 0);
                return;
            }

            if (shouldStack)
            {
                _host._contentLayout.ColumnCount = 1;
                _host._contentLayout.RowCount = 2;
                _host._contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _host._contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
                _host._contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
                _inputCard.Margin = new Padding(0, 0, 0, 6);
                _resultsCard.Margin = new Padding(0, 6, 0, 0);
                _host._contentLayout.Controls.Add(_inputCard, 0, 0);
                _host._contentLayout.Controls.Add(_resultsCard, 0, 1);
            }
            else
            {
                _host._contentLayout.ColumnCount = 2;
                _host._contentLayout.RowCount = 1;
                _host._contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)(_ductCalculatorSplitRatio * 100.0)));
                _host._contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)((1.0 - _ductCalculatorSplitRatio) * 100.0)));
                _host._contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                _inputCard.Margin = Padding.Empty;
                _resultsCard.Margin = Padding.Empty;
                _ductCalculatorSplitter.Panel1.Controls.Add(_inputCard);
                _ductCalculatorSplitter.Panel2.Controls.Add(_resultsCard);
                _host._contentLayout.Controls.Add(_ductCalculatorSplitter, 0, 0);
                _host._contentLayout.SetColumnSpan(_ductCalculatorSplitter, 2);
                ApplyDuctCalculatorSplitterRatio();
            }
        }
        finally
        {
            _host._contentLayout.ResumeLayout(true);
        }
    }

    internal void ApplyDuctCalculatorSplitterRatio()
    {
        if (_ductCalculatorSplitter.ClientSize.Width <= _ductCalculatorSplitter.SplitterWidth
            || _ductCalculatorSplitter.Panel1Collapsed || _ductCalculatorSplitter.Panel2Collapsed)
            return;
        var available = _ductCalculatorSplitter.ClientSize.Width - _ductCalculatorSplitter.SplitterWidth;
        var minimum = _ductCalculatorSplitter.Panel1MinSize;
        var maximum = available - _ductCalculatorSplitter.Panel2MinSize;
        if (maximum < minimum)
            return;
        var distance = Math.Clamp((int)Math.Round(available * _ductCalculatorSplitRatio), minimum, maximum);
        _updatingDuctCalculatorSplitter = true;
        try
        {
            if (_ductCalculatorSplitter.SplitterDistance != distance)
                _ductCalculatorSplitter.SplitterDistance = distance;
        }
        finally
        {
            _updatingDuctCalculatorSplitter = false;
        }
        UpdateDuctCalculatorColumnStyles();
    }

    internal void UpdateDuctCalculatorColumnStyles()
    {
        if (_host._contentLayout.ColumnStyles.Count < 2)
            return;
        _host._contentLayout.ColumnStyles[0].SizeType = SizeType.Percent;
        _host._contentLayout.ColumnStyles[0].Width = (float)(_ductCalculatorSplitRatio * 100.0);
        _host._contentLayout.ColumnStyles[1].SizeType = SizeType.Percent;
        _host._contentLayout.ColumnStyles[1].Width = (float)((1.0 - _ductCalculatorSplitRatio) * 100.0);
    }
}
