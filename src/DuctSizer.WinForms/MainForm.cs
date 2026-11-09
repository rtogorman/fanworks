using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

public sealed partial class MainForm : Form
{
    private DuctSystemScreen? _ductSystemScreen;
    internal DuctSystemScreen DuctSystemScreen => _ductSystemScreen ??= new DuctSystemScreen(this);

    private FittingCalculatorScreen? _fittingScreen;
    internal FittingCalculatorScreen FittingScreen => _fittingScreen ??= new FittingCalculatorScreen(this);

    internal const double FixedRelativeHumidity = 0.50;
    internal const double FixedAtmosphericPressurePa = 101_325.0;

    internal readonly ComboBox _unitsCombo = CreateDropDown();
    internal readonly ComboBox _modeCombo = CreateDropDown();
    internal readonly ComboBox _shapeCombo = CreateDropDown();
    internal readonly ComboBox _materialCombo = CreateDropDown();
    internal readonly ComboBox _equivalentMethodCombo = CreateDropDown();
    internal readonly ComboBox _pressureBasisCombo = CreateDropDown();
    internal static readonly string[] AirSystemTypes =
        { "Supply Air", "Return Air", "Exhaust Air", "Outdoor Air", "Relief Air", "Other" };

    internal readonly TextBox _airflowText = CreateInput("1000");
    internal readonly TextBox _temperatureText = CreateInput("70");
    internal readonly TextBox _lengthText = CreateInput("100");
    internal readonly TextBox _diameterText = CreateInput("12");
    internal readonly TextBox _widthText = CreateInput("24");
    internal readonly TextBox _heightText = CreateInput("12");
    internal readonly ComboBox _rectangularFixedDimensionCombo = CreateDropDown();
    internal readonly TextBox _rectangularFixedDimensionText = CreateInput("16");
    private readonly TextBox _targetFrictionText = CreateInput("0.08");
    private readonly TextBox _maximumVelocityText = CreateInput("1500");
    internal readonly TextBox _sizeIncrementText = CreateInput("0");
    private readonly Label _diameterUnitLabel = CreateInputUnitLabel();
    private readonly Label _widthUnitLabel = CreateInputUnitLabel();
    private readonly Label _heightUnitLabel = CreateInputUnitLabel();
    private readonly Label _rectangularFixedDimensionUnitLabel = CreateInputUnitLabel();

    private readonly Dictionary<string, Label> _inputLabels = new();
    private readonly Dictionary<string, Control> _inputControls = new();
    private readonly Dictionary<string, Label> _sectionHeaders = new();
    private readonly TableLayoutPanel _contentLayout = new();
    private readonly SplitContainer _ductCalculatorSplitter = new();
    private readonly RoundedPanel _inputCard = new();
    private readonly RoundedPanel _resultsCard = new();
    internal readonly Label _ductEditingLabel = new();
    private readonly TableLayoutPanel _summaryTable = new();
    private readonly ListView _results = new();
    internal readonly Label _statusLabel = new();
    private readonly Label _methodHintLabel = new();
    private readonly Label _sizeSummaryValue = CreateSummaryValue();
    private readonly Label _velocitySummaryValue = CreateSummaryValue();
    private readonly Label _pressureSummaryValue = CreateSummaryValue();
    private readonly RichTextBox _ductCalculationDetails = new();
    private readonly TextBox _assumptionsText = new();
    private readonly ListViewGroup _geometryGroup = new("Geometry");
    private readonly ListViewGroup _flowGroup = new("Flow");
    private readonly ListViewGroup _pressureGroup = new("Pressure loss");
    private readonly ListViewGroup _propertiesGroup = new("Air and material");
    internal UnitSystem _currentUnits = UnitSystem.Imperial;
    internal bool _updatingControls;
    private bool _stackedLayout;
    private bool _ductCalculationsCollapsed;
    private bool _ductCalculationsExpanded;
    private bool _updatingDuctCalculatorSplitter;
    private double _ductCalculatorSplitRatio = 0.46;
    private double _ductCalculatorRestoreRatio = 0.46;
    private readonly Button _ductCalculationsShowButton = CreatePaneButton("Show Calculations", 172);
    private readonly Button _ductCalculationsHideButton = CreatePaneButton("Hide", 58);
    private readonly Button _ductCalculationsExpandButton = CreatePaneButton("Expand", 88);

    internal sealed record Choice<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

    public MainForm()
    {
        Text = "FanWorks";
        MinimumSize = new Size(760, 650);
        Size = new Size(1200, 1200);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        BackColor = UiPalette.Canvas;
        AutoScaleMode = AutoScaleMode.Dpi;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream(
                   "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.ico"))
        {
            if (iconStream is not null)
            {
                using var applicationIcon = new Icon(iconStream);
                Icon = (Icon)applicationIcon.Clone();
            }
        }

        _contentLayout.Name = "ContentLayout";
        ConfigureCalculatorSplitter(_ductCalculatorSplitter, "DuctCalculatorSplitter");
        _ductCalculatorSplitter.SplitterMoved += (_, _) =>
        {
            if (_updatingDuctCalculatorSplitter || _ductCalculatorSplitter.ClientSize.Width <= _ductCalculatorSplitter.SplitterWidth)
                return;
            _ductCalculatorSplitRatio = Math.Clamp(
                _ductCalculatorSplitter.SplitterDistance
                / (double)(_ductCalculatorSplitter.ClientSize.Width - _ductCalculatorSplitter.SplitterWidth), .25, .75);
            if (!_ductCalculationsExpanded)
                _ductCalculatorRestoreRatio = _ductCalculatorSplitRatio;
            UpdateDuctCalculatorColumnStyles();
        };
        _ductCalculatorSplitter.Resize += (_, _) => ApplyDuctCalculatorSplitterRatio();
        _shapeCombo.Name = "ShapeCombo";
        _modeCombo.Name = "ModeCombo";
        _materialCombo.Name = "MaterialCombo";
        _equivalentMethodCombo.Name = "EquivalentMethodCombo";
        _pressureBasisCombo.Name = "PressureBasisCombo";
        _rectangularFixedDimensionCombo.Name = "RectangularSizingDimensionCombo";
        _rectangularFixedDimensionText.Name = "RectangularSizingDimensionText";
        _rectangularFixedDimensionUnitLabel.Name = "RectangularSizingDimensionUnitLabel";
        _sizeSummaryValue.Font = new Font("Segoe UI Semibold", 8.8F);
        _velocitySummaryValue.Font = new Font("Segoe UI Semibold", 8.8F);
        _pressureSummaryValue.Font = new Font("Segoe UI Semibold", 8.3F);

        BuildChoices();
        BuildLayout();
        WireEvents();
        DuctSystemScreen.LoadDuctSystem();
        UpdateUnitsLabels();
        UpdateControlState();
        ApplyResponsiveLayout();
        CalculateAndDisplay();
    }

    private void BuildChoices()
    {
        _unitsCombo.Name = "UnitsCombo";
        _unitsCombo.Items.AddRange(new object[]
        {
            new Choice<UnitSystem>("Imperial", UnitSystem.Imperial),
            new Choice<UnitSystem>("Metric", UnitSystem.Metric)
        });
        _modeCombo.Items.AddRange(new object[]
        {
            new Choice<CalculationMode>("Check Existing", CalculationMode.CheckEnteredSize),
            new Choice<CalculationMode>("Autosize (SP Loss)", CalculationMode.SizeByTargetFriction),
            new Choice<CalculationMode>("Autosize (Velocity)", CalculationMode.SizeByMaximumVelocity)
        });
        _shapeCombo.Items.AddRange(new object[]
        {
            new Choice<DuctShape>("Round", DuctShape.Round),
            new Choice<DuctShape>("Rectangular", DuctShape.Rectangular)
        });
        foreach (var material in DuctMaterialCatalog.All
                     .OrderBy(item => string.Equals(item.Name, "Legacy Reference",
                         StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            _materialCombo.Items.Add(material);
        _equivalentMethodCombo.Items.AddRange(new object[]
        {
            new Choice<EquivalentDiameterMethod>("Huebscher", EquivalentDiameterMethod.Huebscher),
            new Choice<EquivalentDiameterMethod>("Hydraulic Diameter", EquivalentDiameterMethod.HydraulicDiameter)
        });
        _pressureBasisCombo.Items.AddRange(new object[]
        {
            new Choice<PressureLossBasis>("Equivalent Round Area", PressureLossBasis.EquivalentRoundArea),
            new Choice<PressureLossBasis>("Actual Duct Area", PressureLossBasis.ActualDuctArea)
        });
        _rectangularFixedDimensionCombo.Items.AddRange(new object[]
        {
            new Choice<RectangularFixedDimension>("Width", RectangularFixedDimension.Width),
            new Choice<RectangularFixedDimension>("Height", RectangularFixedDimension.Height)
        });
        _unitsCombo.SelectedIndex = 0;
        _modeCombo.SelectedIndex = 1;
        _shapeCombo.SelectedIndex = 0;
        _materialCombo.SelectedItem = _materialCombo.Items.Cast<DuctMaterial>()
            .Single(item => string.Equals(item.Name, "Legacy Reference", StringComparison.OrdinalIgnoreCase));
        _equivalentMethodCombo.SelectedIndex = 0;
        _pressureBasisCombo.SelectedIndex = 0;
        _rectangularFixedDimensionCombo.SelectedIndex = 0;
        foreach (var combo in new[]
                 {
                     _unitsCombo, _modeCombo, _shapeCombo, _materialCombo, _equivalentMethodCombo,
                     _pressureBasisCombo, _rectangularFixedDimensionCombo
                 })
            ConfigureComboDropDown(combo);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

        var titlePanel = new SectionTitlePanel
        {
            Name = "CalculatorSectionHeader",
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Blue
        };
        var title = new Label
        {
            Name = "CalculatorPageTitle",
            Text = "Duct Calculator",
            AutoSize = false,
            Size = new Size(360, 50),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 20F),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(28, 0),
            BackColor = Color.Transparent
        };
        var subtitle = new Label
        {
            Name = "CalculatorPageSubtitle",
            Text = "HVAC duct sizing • ASHRAE fitting calculations • system pressure loss",
            AutoSize = false,
            Size = new Size(780, 28),
            ForeColor = Color.FromArgb(215, 229, 242),
            Font = new Font("Segoe UI", 9.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(31, 51),
            BackColor = Color.Transparent
        };
        var version = new Label
        {
            Text = $"v{typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0.74.0"}",
            AutoSize = false,
            Size = new Size(66, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(44, 98, 146),
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 103, 29)
        };
        titlePanel.Resize += (_, _) =>
        {
            version.Left = Math.Max(20, titlePanel.ClientSize.Width - version.Width - 22);
            subtitle.Width = Math.Max(180, version.Left - subtitle.Left - 12);
        };
        titlePanel.Controls.Add(title);
        titlePanel.Controls.Add(subtitle);
        titlePanel.Controls.Add(version);

        _contentLayout.Dock = DockStyle.Fill;
        _contentLayout.Padding = new Padding(14);
        _contentLayout.BackColor = BackColor;

        _inputCard.Dock = DockStyle.Fill;
        _inputCard.Name = "InputCard";
        _inputCard.BackColor = Color.White;
        _inputCard.CornerRadius = 12;
        _inputCard.BorderColor = UiPalette.CardBorder;
        _inputCard.Margin = new Padding(0, 0, 7, 0);

        var inputHeader = CreateCardHeader("INPUTS", "Enter design conditions and calculation method.");
        var inputTable = new TableLayoutPanel
        {
            Name = "DuctInputTable",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 6,
            RowCount = 0,
            Padding = new Padding(16, 5, 16, 12),
            BackColor = Color.White
        };
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));

        _ductEditingLabel.Name = "DuctEditingLabel";
        _ductEditingLabel.AutoSize = true;
        _ductEditingLabel.Dock = DockStyle.Top;
        _ductEditingLabel.BackColor = Color.FromArgb(232, 244, 253);
        _ductEditingLabel.ForeColor = Color.FromArgb(22, 65, 102);
        _ductEditingLabel.Font = new Font("Segoe UI Semibold", 9F);
        _ductEditingLabel.Padding = new Padding(10, 7, 10, 7);
        _ductEditingLabel.Margin = new Padding(0, 0, 0, 4);
        _ductEditingLabel.Visible = false;
        var editingRow = inputTable.RowCount++;
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_ductEditingLabel, 0, editingRow);
        inputTable.SetColumnSpan(_ductEditingLabel, inputTable.ColumnCount);

        AddInputSection(inputTable, "calculation", "CALCULATION");
        AddInputRow(inputTable, "mode", "Calculation", _modeCombo);
        AddInputRow(inputTable, "airflow", "Airflow (cfm)", _airflowText);
        AddInputRow(inputTable, "temperature", "Air Temp (°F)", _temperatureText);
        AddInputRow(inputTable, "length", "Length (ft)", _lengthText);
        AddInputRow(inputTable, "friction", "Target Friction", _targetFrictionText);
        AddInputRow(inputTable, "velocity", "Maximum Velocity", _maximumVelocityText);

        AddInputSection(inputTable, "geometry", "DUCT GEOMETRY");
        AddInputRow(inputTable, "shape", "Duct Shape", _shapeCombo);
        AddInputPairRow(inputTable,
            ("diameter", "Diameter", CreateUnitInputHost(_diameterText, _diameterUnitLabel)),
            ("width", "Width", CreateUnitInputHost(_widthText, _widthUnitLabel)));
        AddInputPairRow(inputTable,
            ("height", "Height", CreateUnitInputHost(_heightText, _heightUnitLabel)));
        AddRectangularSizingDimensionRow(inputTable);

        AddInputSection(inputTable, "methods", "METHODS AND MATERIAL");
        AddInputRow(inputTable, "material", "Duct Material", _materialCombo);
        AddHintRow(inputTable, _methodHintLabel);

        var inputScroll = new Panel
        {
            Name = "DuctInputScroll",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 16, 10),
            BackColor = Color.FromArgb(248, 250, 252),
            WrapContents = false
        };
        var calculateButton = new Button
        {
            Name = "CalculateButton",
            Text = "Calculate",
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F),
            FlatStyle = FlatStyle.Flat
        };
        calculateButton.FlatAppearance.BorderSize = 0;
        calculateButton.Margin = Padding.Empty;
        UiPolish.RoundButton(calculateButton);
        calculateButton.Click += (_, _) => CalculateAndDisplay();
        DuctSystemScreen._addDuctButton.Name = "AddDuctButton";
        DuctSystemScreen._addDuctButton.Text = DuctSystemScreen.DefaultDuctActionText;
        DuctSystemScreen._addDuctButton.Size = new Size(175, 34);
        DuctSystemScreen._addDuctButton.Margin = Padding.Empty;
        DuctSystemScreen._addDuctButton.BackColor = Color.FromArgb(42, 135, 91);
        DuctSystemScreen._addDuctButton.ForeColor = Color.White;
        DuctSystemScreen._addDuctButton.Font = new Font("Segoe UI Semibold", 8.5F);
        DuctSystemScreen._addDuctButton.FlatStyle = FlatStyle.Flat;
        DuctSystemScreen._addDuctButton.FlatAppearance.BorderSize = 0;
        UiPolish.RoundButton(DuctSystemScreen._addDuctButton);
        DuctSystemScreen._addDuctButton.Click += (_, _) => DuctSystemScreen.AddOrUpdateCurrentDuct();
        DuctSystemScreen._addDuctButton.Visible = false;
        buttonPanel.Controls.Add(DuctSystemScreen._addDuctButton);
        calculateButton.Margin = new Padding(0, 0, 10, 0);
        buttonPanel.Controls.Add(calculateButton);

        _resultsCard.Dock = DockStyle.Fill;
        _resultsCard.Name = "ResultsCard";
        _resultsCard.BackColor = Color.White;
        _resultsCard.CornerRadius = 12;
        _resultsCard.BorderColor = UiPalette.CardBorder;
        _resultsCard.Margin = new Padding(7, 0, 0, 0);
        var resultsHeader = CreateCardHeader("CALCULATIONS", "Geometry, flow, pressure loss, air, material, and assumptions.");
        AddCalculationPaneControls(inputHeader, resultsHeader,
            _ductCalculationsShowButton, _ductCalculationsHideButton, _ductCalculationsExpandButton,
            () =>
            {
                _ductCalculationsCollapsed = false;
                ApplyResponsiveLayout(force: true);
            },
            () =>
            {
                _ductCalculationsCollapsed = true;
                ApplyResponsiveLayout(force: true);
            },
            () =>
            {
                if (_ductCalculationsExpanded)
                {
                    _ductCalculationsExpanded = false;
                    _ductCalculatorSplitRatio = _ductCalculatorRestoreRatio;
                }
                else
                {
                    _ductCalculatorRestoreRatio = _ductCalculatorSplitRatio;
                    _ductCalculationsExpanded = true;
                    _ductCalculatorSplitRatio = .34;
                }
                _ductCalculationsExpandButton.Text = _ductCalculationsExpanded ? "Restore" : "Expand";
                ApplyResponsiveLayout(force: true);
            });

        _summaryTable.Name = "DuctSummaryTable";
        _summaryTable.Dock = DockStyle.Fill;
        _summaryTable.Height = 116;
        _summaryTable.Padding = new Padding(8, 5, 8, 5);
        _summaryTable.ColumnCount = 3;
        _summaryTable.RowCount = 1;
        _summaryTable.BackColor = Color.White;
        _summaryTable.ColumnCount = 1;
        _summaryTable.RowCount = 3;
        _summaryTable.ColumnStyles.Clear();
        _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        _summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        _summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));
        _summaryTable.Controls.Add(CreateSummaryCard("DUCT SIZE", _sizeSummaryValue), 0, 0);
        _summaryTable.Controls.Add(CreateSummaryCard("ACTUAL VELOCITY", _velocitySummaryValue), 0, 1);
        _summaryTable.Controls.Add(CreateSummaryCard("PRESSURE RATE", _pressureSummaryValue), 0, 2);

        inputScroll.Controls.Add(inputTable);

        var inputBody = new TableLayoutPanel
        {
            Name = "DuctInputAndSummaryLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ConfigureCalculatorInputBody(inputBody, inputScroll, inputTable,
            CreateOutputSectionLabel("DuctOutputsHeader"), _summaryTable, buttonPanel, 92, 64);
        _inputCard.Controls.Add(inputBody);
        _inputCard.Controls.Add(inputHeader);

        _results.Dock = DockStyle.Fill;
        _results.View = View.Details;
        _results.FullRowSelect = true;
        _results.GridLines = false;
        _results.BorderStyle = BorderStyle.None;
        _results.BackColor = Color.White;
        _results.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _results.ShowGroups = true;
        _results.Columns.Add("Result", 260);
        _results.Columns.Add("Value", 390);
        _results.Groups.AddRange(new[] { _geometryGroup, _flowGroup, _pressureGroup, _propertiesGroup });
        _results.Resize += (_, _) =>
        {
            if (_results.ClientSize.Width > 200)
            {
                _results.Columns[0].Width = Math.Min(270, _results.ClientSize.Width / 2);
                _results.Columns[1].Width = Math.Max(180, _results.ClientSize.Width - _results.Columns[0].Width - 5);
            }
        };
        _results.Visible = false;

        _ductCalculationDetails.Name = "DuctCalculationDetails";
        _ductCalculationDetails.Dock = DockStyle.Fill;
        _ductCalculationDetails.ReadOnly = true;
        _ductCalculationDetails.BorderStyle = BorderStyle.None;
        _ductCalculationDetails.BackColor = Color.White;
        _ductCalculationDetails.ForeColor = Color.FromArgb(54, 70, 86);
        _ductCalculationDetails.Font = new Font("Segoe UI", 9.5F);
        _ductCalculationDetails.ScrollBars = RichTextBoxScrollBars.Vertical;
        _ductCalculationDetails.DetectUrls = false;
        _ductCalculationDetails.Text = "Select inputs and calculate to display the complete equation worksheet.";
        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 7, 12, 0),
            BackColor = Color.White
        };
        detailsHost.Controls.Add(_ductCalculationDetails);

        _assumptionsText.Dock = DockStyle.Fill;
        _assumptionsText.Multiline = true;
        _assumptionsText.ReadOnly = true;
        _assumptionsText.ScrollBars = ScrollBars.None;
        _assumptionsText.BackColor = Color.FromArgb(248, 250, 252);
        _assumptionsText.BorderStyle = BorderStyle.None;
        _assumptionsText.Margin = new Padding(8, 0, 8, 8);
        _assumptionsText.Text =
            "Assumptions: 50% relative humidity; standard sea-level pressure (14.696 psia); steady, fully developed flow. " +
            "Laminar flow uses Hagen-Poiseuille for round ducts and Shah-London for rectangular ducts. " +
            "Turbulent friction is solved from the implicit Colebrook equation by bisection. " +
            "Transitional results are shown as unblended bounds.";
        var assumptionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 145,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 0, 12, 8),
            BackColor = Color.White
        };
        assumptionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        assumptionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        assumptionsPanel.Controls.Add(new Label
        {
            Text = "CALCULATION ASSUMPTIONS",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(80, 96, 112),
            Font = new Font("Segoe UI Semibold", 8F)
        }, 0, 0);
        assumptionsPanel.Controls.Add(_assumptionsText, 0, 1);

        _resultsCard.Controls.Add(detailsHost);
        _resultsCard.Controls.Add(assumptionsPanel);
        _resultsCard.Controls.Add(resultsHeader);

        var statusPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18, 5, 18, 5)
        };
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.FromArgb(50, 70, 90);
        _statusLabel.AutoEllipsis = true;
        statusPanel.Controls.Add(_statusLabel);

        root.Controls.Add(titlePanel, 0, 0);
        root.Controls.Add(_contentLayout, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);
        DuctSystemScreen.InitializeTabsAndSystem(root);
        AcceptButton = calculateButton;
        Resize += (_, _) => ApplyResponsiveLayout();
    }

    internal static void ConfigureCalculatorInputBody(TableLayoutPanel body, Panel inputScroll,
        TableLayoutPanel inputTable, Control outputHeading, Control summary, Control actionRow,
        int summaryHeight, int actionHeight)
    {
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryHeight));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, actionHeight));

        inputScroll.Dock = DockStyle.Fill;
        actionRow.Dock = DockStyle.Fill;
        body.Controls.Add(inputScroll, 0, 0);
        body.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.White }, 0, 1);
        body.Controls.Add(outputHeading, 0, 2);
        body.Controls.Add(summary, 0, 3);
        body.Controls.Add(actionRow, 0, 4);

        var originalRows = inputTable.RowStyles.Cast<RowStyle>()
            .Select(style => (style.SizeType, style.Height)).ToArray();

        void UpdateVisibleRows()
        {
            inputTable.SuspendLayout();
            try
            {
                for (var row = 0; row < inputTable.RowCount && row < originalRows.Length; row++)
                {
                    var controls = inputTable.Controls.Cast<Control>()
                        .Where(control => inputTable.GetRow(control) == row).ToArray();
                    if (controls.Length == 0)
                        continue;
                    var style = inputTable.RowStyles[row];
                    if (controls.Any(control => control.Visible))
                    {
                        style.SizeType = originalRows[row].SizeType;
                        style.Height = originalRows[row].Height;
                    }
                    else
                    {
                        style.SizeType = SizeType.Absolute;
                        style.Height = 0;
                    }
                }
            }
            finally
            {
                inputTable.ResumeLayout(true);
            }
            UpdateScrollAvailability();
        }

        void UpdateScrollAvailability()
        {
            if (!inputScroll.IsHandleCreated || !inputTable.IsHandleCreated)
                return;
            var needsVerticalScroll = inputTable.Height > inputScroll.Height;
            if (inputScroll.AutoScroll != needsVerticalScroll)
                inputScroll.AutoScroll = needsVerticalScroll;
        }

        foreach (Control control in inputTable.Controls)
            control.VisibleChanged += (_, _) => UpdateVisibleRows();
        inputTable.SizeChanged += (_, _) => UpdateScrollAvailability();
        inputScroll.SizeChanged += (_, _) => UpdateScrollAvailability();
        inputTable.HandleCreated += (_, _) => inputTable.BeginInvoke(UpdateVisibleRows);
        UpdateVisibleRows();
    }

    private void WireEvents()
    {
        _unitsCombo.SelectedIndexChanged += (_, _) => ChangeUnits();
        _shapeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateControlState();
            CalculateAndDisplay();
        };
        _modeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateControlState();
            CalculateAndDisplay();
        };
        _materialCombo.SelectedIndexChanged += (_, _) =>
        {
            ApplyMaterialShapeConstraint();
            UpdateControlState();
            UpdateMaterialStatus();
            CalculateAndDisplay();
        };
        _equivalentMethodCombo.SelectedIndexChanged += (_, _) => CalculateAndDisplay();
        _pressureBasisCombo.SelectedIndexChanged += (_, _) => CalculateAndDisplay();
        _rectangularFixedDimensionCombo.SelectedIndexChanged += (_, _) => CalculateAndDisplay();
    }

    private void ChangeUnits()
    {
        if (_updatingControls)
            return;

        var selected = Selected(_unitsCombo, _currentUnits);
        if (selected == _currentUnits)
            return;

        _updatingControls = true;
        try
        {
            FittingScreen.ConvertFittingUnitInputs(_currentUnits, selected);
            if (_currentUnits == UnitSystem.Imperial && selected == UnitSystem.Metric)
            {
                ConvertText(_airflowText, UnitConversions.CfmToM3PerS, value => UnitConversions.M3PerSToLps(value));
                ConvertText(_temperatureText, UnitConversions.FahrenheitToCelsius);
                ConvertText(_lengthText, UnitConversions.FeetToM);
                ConvertText(_diameterText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(_widthText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(_heightText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(_rectangularFixedDimensionText,
                    value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(_targetFrictionText, UnitConversions.InchesWaterPer100FeetToPaPerM);
                ConvertText(_maximumVelocityText, UnitConversions.FpmToMps);
                ConvertText(_sizeIncrementText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
            }
            else
            {
                ConvertText(_airflowText, UnitConversions.LpsToM3PerS, UnitConversions.M3PerSToCfm);
                ConvertText(_temperatureText, UnitConversions.CelsiusToFahrenheit);
                ConvertText(_lengthText, UnitConversions.MToFeet);
                ConvertText(_diameterText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(_widthText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(_heightText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(_rectangularFixedDimensionText,
                    value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(_targetFrictionText, UnitConversions.PaPerMToInchesWaterPer100Feet);
                ConvertText(_maximumVelocityText, UnitConversions.MpsToFpm);
                ConvertText(_sizeIncrementText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
            }

            _currentUnits = selected;
            UpdateUnitsLabels();
            FittingScreen.UpdateFittingUnitLabels();
        }
        finally
        {
            _updatingControls = false;
        }

        CalculateAndDisplay();
        FittingScreen.CalculateFittingAndDisplay();
        DuctSystemScreen.RefreshDuctSystemGrid();
    }

    private void UpdateUnitsLabels()
    {
        var imperial = _currentUnits == UnitSystem.Imperial;
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
        var shape = Selected(_shapeCombo, DuctShape.Round);
        var mode = Selected(_modeCombo, CalculationMode.CheckEnteredSize);
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
            ? "Equivalent-diameter, pressure-loss basis, and autosize increment are controlled in Settings."
            : "Project calculation defaults are controlled in Settings and apply automatically to new sizing calculations.";

        UpdateMaterialStatus();
    }

    private void UpdateMaterialStatus()
    {
        if (_materialCombo.SelectedItem is not DuctMaterial material)
            return;

        var requiredShape = RequiredShapeForMaterial(material);
        _statusLabel.Text = requiredShape is DuctShape.Round
            ? "Galvanized spiral duct is available only as a round duct."
            : requiredShape is DuctShape.Rectangular
                ? $"{material.Name} is available only as a rectangular duct."
                : material.Name.StartsWith("Flexible", StringComparison.OrdinalIgnoreCase)
            ? "Warning: the flexible-duct preset is valid only when the duct is fully extended."
            : material.Note;
    }

    private void ApplyMaterialShapeConstraint()
    {
        if (_materialCombo.SelectedItem is not DuctMaterial material)
            return;
        var required = RequiredShapeForMaterial(material);
        _shapeCombo.Enabled = required is null;
        if (required is null || Selected(_shapeCombo, required.Value) == required.Value)
            return;
        var previousUpdating = _updatingControls;
        _updatingControls = true;
        try
        {
            _shapeCombo.SelectedItem = _shapeCombo.Items.Cast<object>()
                .OfType<Choice<DuctShape>>()
                .First(choice => choice.Value == required.Value);
        }
        finally
        {
            _updatingControls = previousUpdating;
        }
    }

    private static DuctShape? RequiredShapeForMaterial(DuctMaterial material)
    {
        if (material.Name.Equals("Galvanized Spiral Duct", StringComparison.OrdinalIgnoreCase))
            return DuctShape.Round;
        if (material.Name.Equals("Galvanized Rectangular Duct", StringComparison.OrdinalIgnoreCase)
            || material.Name.Equals("Internally Fiber-Lined Rectangular Duct", StringComparison.OrdinalIgnoreCase))
            return DuctShape.Rectangular;
        return null;
    }

    internal void CalculateAndDisplay()
    {
        if (_updatingControls)
            return;

        try
        {
            var airflow = ReadAirflow();
            var temperatureC = ReadTemperature();
            var lengthM = ReadLength();
            var shape = Selected(_shapeCombo, DuctShape.Round);
            var mode = Selected(_modeCombo, CalculationMode.CheckEnteredSize);
            var material = (DuctMaterial)(_materialCombo.SelectedItem
                           ?? throw new InvalidOperationException("Select a duct material."));
            var equivalentMethod = Selected(_equivalentMethodCombo, EquivalentDiameterMethod.Huebscher);
            var pressureBasis = Selected(_pressureBasisCombo, PressureLossBasis.EquivalentRoundArea);

            DuctCalculationResult result;
            DuctGeometry? continuousGeometry = null;

            if (mode == CalculationMode.CheckEnteredSize)
            {
                var geometry = ReadEnteredGeometry(shape);
                result = DuctCalculator.Calculate(new DuctCalculationInput(
                    airflow,
                    temperatureC,
                    FixedRelativeHumidity,
                    FixedAtmosphericPressurePa,
                    geometry,
                    material,
                    equivalentMethod,
                    pressureBasis,
                    lengthM));
            }
            else
            {
                var fixedDimension = shape == DuctShape.Rectangular
                    ? Selected(_rectangularFixedDimensionCombo, RectangularFixedDimension.Width)
                    : RectangularFixedDimension.None;
                var fixedDimensionDisplay = shape == DuctShape.Rectangular
                    ? ReadPositive(_rectangularFixedDimensionText,
                        $"fixed {fixedDimension.ToString().ToLowerInvariant()}")
                    : 0.0;
                var fixedDimensionM = _currentUnits == UnitSystem.Imperial
                    ? UnitConversions.InchesToM(fixedDimensionDisplay)
                    : UnitConversions.MillimetersToM(fixedDimensionDisplay);
                var targetFriction = ReadTargetFriction();
                var maximumVelocity = ReadMaximumVelocity();
                var increment = ReadSizeIncrement();
                var sizing = DuctSizingSolver.Size(new DuctSizingInput(
                    airflow,
                    temperatureC,
                    FixedRelativeHumidity,
                    FixedAtmosphericPressurePa,
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
                    DuctSystemScreen._ductSystem.DefaultSizeRoundingDirection,
                    fixedDimension,
                    fixedDimensionM));
                continuousGeometry = sizing.ContinuousGeometry;
                result = sizing.Calculation;
            }

            DisplayResults(result, continuousGeometry);
            DuctSystemScreen._lastCalculation = result;

            var warning = result.Friction.Regime == FlowRegime.Transitional
                ? "Transitional flow: pressure loss is shown as unblended laminar-to-turbulent bounds."
                : "Calculation completed.";
            if (material.Name.StartsWith("Flexible", StringComparison.OrdinalIgnoreCase))
                warning += " Flex-duct result assumes fully extended installation.";
            if (continuousGeometry is not null
                && result.Input.Geometry.ActualAreaM2 < continuousGeometry.ActualAreaM2 - 1e-12)
                warning += " Rounded to a smaller nominal size: the resulting velocity or pressure rate may exceed the selected design target.";
            _statusLabel.Text = warning;
            _statusLabel.ForeColor = result.Friction.Regime == FlowRegime.Transitional
                ? Color.DarkOrange
                : Color.FromArgb(30, 100, 55);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            DuctSystemScreen._lastCalculation = null;
            _results.Items.Clear();
            _sizeSummaryValue.Text = "—";
            _velocitySummaryValue.Text = "—";
            _pressureSummaryValue.Text = "—";
            _statusLabel.Text = ex.Message;
            _statusLabel.ForeColor = Color.Firebrick;
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
            if (continuousGeometry is not null && !GeometryMatches(geometry, continuousGeometry))
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
                box.AppendText(Environment.NewLine);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            box.AppendText(text + Environment.NewLine);
            box.SelectionFont = box.Font;
            box.SelectionColor = box.ForeColor;
        }

        void Line(params (string Text, bool Input)[] parts)
        {
            foreach (var part in parts)
            {
                box.SelectionColor = part.Input ? Color.FromArgb(190, 44, 44) : box.ForeColor;
                box.SelectionFont = part.Input ? new Font(box.Font, FontStyle.Bold) : box.Font;
                box.AppendText(part.Text);
            }
            box.AppendText(Environment.NewLine);
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
            box.AppendText($"{left}\t{right}{Environment.NewLine}");
        }

        void Step(string title, string explanation)
        {
            box.AppendText(Environment.NewLine);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            box.AppendText(title + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
            box.AppendText(explanation + Environment.NewLine);
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
                box.AppendText(part.Text);
            }
            box.AppendText(Environment.NewLine);
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
        box.AppendText("SOLVED" + Environment.NewLine);
        box.SelectionColor = box.ForeColor;
        box.SelectionFont = box.Font;
        Step("1. FINAL DUCT GEOMETRY",
            "The sizing method first establishes the duct dimensions. Project rounding is applied before the final area and all downstream velocity calculations are evaluated.");
        if (continuousGeometry is not null && !GeometryMatches(geometry, continuousGeometry))
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
        if (_currentUnits == UnitSystem.Imperial)
        {
            return geometry.Shape == DuctShape.Round
                ? $"{UnitConversions.MToInches(geometry.DiameterM):0.###} in round"
                : $"{UnitConversions.MToInches(geometry.WidthM):0.###} × {UnitConversions.MToInches(geometry.HeightM):0.###} in";
        }

        return geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToMillimeters(geometry.DiameterM):0.#} mm round"
            : $"{UnitConversions.MToMillimeters(geometry.WidthM):0.#} × {UnitConversions.MToMillimeters(geometry.HeightM):0.#} mm";
    }

    private string FormatLength(double meters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToInches(meters):0.###} in"
        : $"{UnitConversions.MToMillimeters(meters):0.#} mm";

    private string FormatDuctLength(double meters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.###} ft"
        : $"{meters:0.###} m";

    private string FormatArea(double squareMeters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.SquareMetersToSquareFeet(squareMeters):0.####} ft²"
        : $"{squareMeters:0.#####} m²";

    private string FormatVelocity(double metersPerSecond) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MpsToFpm(metersPerSecond):N0} fpm"
        : $"{metersPerSecond:0.###} m/s";

    private string FormatPressureRate(DuctCalculationResult result)
    {
        if (_currentUnits == UnitSystem.Imperial)
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
        if (_currentUnits == UnitSystem.Imperial)
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
        if (_currentUnits == UnitSystem.Imperial)
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
        var value = ReadPositive(_airflowText, "airflow");
        return _currentUnits == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(value)
            : UnitConversions.LpsToM3PerS(value);
    }

    private double ReadTemperature()
    {
        var value = ReadFinite(_temperatureText, "air temperature");
        return _currentUnits == UnitSystem.Imperial
            ? UnitConversions.FahrenheitToCelsius(value)
            : value;
    }

    private double ReadLength()
    {
        var value = ReadPositive(_lengthText, "duct length");
        return _currentUnits == UnitSystem.Imperial
            ? UnitConversions.FeetToM(value)
            : value;
    }

    private DuctGeometry ReadEnteredGeometry(DuctShape shape)
    {
        if (shape == DuctShape.Round)
        {
            var diameter = ReadPositive(_diameterText, "diameter");
            return GeometryCalculator.Round(_currentUnits == UnitSystem.Imperial
                ? UnitConversions.InchesToM(diameter)
                : UnitConversions.MillimetersToM(diameter));
        }

        var width = ReadPositive(_widthText, "width");
        var height = ReadPositive(_heightText, "height");
        return GeometryCalculator.Rectangular(
            _currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(width) : UnitConversions.MillimetersToM(width),
            _currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(height) : UnitConversions.MillimetersToM(height));
    }

    private double ReadTargetFriction()
    {
        if (Selected(_modeCombo, CalculationMode.CheckEnteredSize) != CalculationMode.SizeByTargetFriction)
            return 1.0;
        var value = ReadPositive(_targetFrictionText, "target friction");
        return _currentUnits == UnitSystem.Imperial
            ? UnitConversions.InchesWaterPer100FeetToPaPerM(value)
            : value;
    }

    private double ReadMaximumVelocity()
    {
        if (Selected(_modeCombo, CalculationMode.CheckEnteredSize) != CalculationMode.SizeByMaximumVelocity)
            return 1.0;
        var value = ReadPositive(_maximumVelocityText, "maximum velocity");
        return _currentUnits == UnitSystem.Imperial
            ? UnitConversions.FpmToMps(value)
            : value;
    }

    private double ReadSizeIncrement()
    {
        var value = ReadNonNegative(_sizeIncrementText, "rounding increment");
        return _currentUnits == UnitSystem.Imperial
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
            _statusLabel.Text = "Results copied to the clipboard.";
            _statusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (ExternalException ex)
        {
            _statusLabel.Text = $"The clipboard is unavailable: {ex.Message}";
            _statusLabel.ForeColor = Color.Firebrick;
        }
    }

    private void AddResult(string name, string value, ListViewGroup group)
    {
        var item = new ListViewItem(name, group);
        item.SubItems.Add(value);
        _results.Items.Add(item);
    }

    internal static Panel CreateCardHeader(string title, string subtitle)
    {
        var panel = new CardHeaderPanel
        {
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(16, 6, 16, 5),
            BackColor = Color.White
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Location = new Point(16, 6),
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI Semibold", 11F)
        };
        var subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = false,
            Location = new Point(16, 34),
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(100, 112, 124),
            AutoEllipsis = true
        };
        void ResizeLabels()
        {
            var reserved = panel.Controls.Cast<Control>()
                .Where(control => !ReferenceEquals(control, titleLabel)
                                  && !ReferenceEquals(control, subtitleLabel)
                                  && control.Dock == DockStyle.Right)
                .Sum(control => control.Width);
            var width = Math.Max(1, panel.ClientSize.Width - 32 - reserved);
            titleLabel.Width = width;
            subtitleLabel.Width = width;
        }
        panel.Resize += (_, _) => ResizeLabels();
        panel.Controls.Add(titleLabel);
        panel.Controls.Add(subtitleLabel);
        ResizeLabels();
        return panel;
    }

    internal static Label CreateOutputSectionLabel(string name) => new()
    {
        Name = name,
        Text = "OUTPUTS",
        Dock = DockStyle.Fill,
        Padding = new Padding(16, 5, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(34, 62, 88),
        Font = new Font("Segoe UI Semibold", 11F),
        BackColor = Color.White
    };

    internal static Panel CreateSummaryCard(string caption, Label valueLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2, 2, 2, 2),
            Padding = new Padding(0),
            BackColor = Color.White
        };
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 28,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var captionLabel = new Label
        {
            Text = caption + ":",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 5, 0),
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI Semibold", 7.3F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Padding = Padding.Empty;
        valueLabel.Margin = Padding.Empty;
        valueLabel.ForeColor = Color.FromArgb(0, 111, 145);
        row.Controls.Add(captionLabel, 0, 0);
        row.Controls.Add(valueLabel, 1, 0);
        panel.Controls.Add(row);
        return panel;
    }

    internal static Label CreateSummaryValue() => new()
    {
        Text = "—",
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(22, 65, 102),
        Font = new Font("Segoe UI Semibold", 12F),
        Padding = Padding.Empty
    };

    private static Label CreateInputUnitLabel() => new()
    {
        Text = "(in.)",
        AutoSize = true,
        Margin = new Padding(5, 4, 0, 0),
        ForeColor = Color.FromArgb(72, 92, 110),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Control CreateUnitInputHost(TextBox input, Label unitLabel)
    {
        input.Width = 94;
        input.Margin = Padding.Empty;
        var host = new FlowLayoutPanel
        {
            Tag = "UnitInput",
            AutoSize = false,
            Size = new Size(152, 27),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        host.Controls.Add(input);
        host.Controls.Add(unitLabel);
        return host;
    }

    internal static Button CreatePaneButton(string text, int width) => new()
    {
        Text = text,
        Size = new Size(width, 28),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(34, 62, 88),
        Font = new Font("Segoe UI Semibold", 8F),
        Anchor = AnchorStyles.Top | AnchorStyles.Right
    };

    internal static void AddCalculationPaneControls(Panel inputHeader, Panel calculationsHeader,
        Button showButton, Button hideButton, Button expandButton,
        Action show, Action hide, Action expand)
    {
        foreach (var button in new[] { showButton, hideButton, expandButton })
        {
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 199, 214);
            UiPolish.RoundButton(button, 6);
        }
        showButton.Click += (_, _) => show();
        hideButton.Click += (_, _) => hide();
        expandButton.Click += (_, _) => expand();
        var showHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = showButton.Width + 18,
            Height = 40,
            BackColor = Color.White
        };
        var calculationHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = hideButton.Width + expandButton.Width + 28,
            Height = 40,
            BackColor = Color.White
        };
        showButton.Margin = Padding.Empty;
        hideButton.Margin = Padding.Empty;
        expandButton.Margin = Padding.Empty;
        showHost.Controls.Add(showButton);
        calculationHost.Controls.Add(hideButton);
        calculationHost.Controls.Add(expandButton);
        showButton.Location = new Point(2, 5);
        void PositionCalculationButtons()
        {
            hideButton.Location = new Point(calculationHost.ClientSize.Width - hideButton.Width - 10, 5);
            expandButton.Location = new Point(hideButton.Left - expandButton.Width - 6, 5);
        }
        calculationHost.Resize += (_, _) => PositionCalculationButtons();
        PositionCalculationButtons();
        inputHeader.Controls.Add(showHost);
        calculationsHeader.Controls.Add(calculationHost);
        showButton.Visible = false;
        showHost.BringToFront();
        calculationHost.BringToFront();
        hideButton.BringToFront();
        expandButton.BringToFront();
    }

    private void AddInputSection(TableLayoutPanel table, string key, string text)
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

    private static void AddHintRow(TableLayoutPanel table, Label label)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.MaximumSize = new Size(520, 0);
        label.ForeColor = Color.FromArgb(92, 105, 118);
        label.Font = new Font("Segoe UI", 8.3F, FontStyle.Italic);
        label.Padding = new Padding(0, 5, 0, 8);
        table.Controls.Add(label, 0, row);
        table.SetColumnSpan(label, table.ColumnCount);
    }

    private void AddInputRow(TableLayoutPanel table, string key, string labelText, Control control)
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
        ConfigureCompactInputControl(control, 250, 135);
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

    private void AddInputPairRow(TableLayoutPanel table,
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
            ConfigureCompactInputControl(item.Control, 165, 120);
            item.Control.Margin = new Padding(0, 3, 0, 3);
            table.Controls.Add(label, column, row);
            table.Controls.Add(item.Control, column + 1, row);
            _inputLabels[item.Key] = label;
            _inputControls[item.Key] = item.Control;
        }
    }

    private void AddRectangularSizingDimensionRow(TableLayoutPanel table)
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
            ConfigureCompactInputControl(item.Control, 82, 76);
            item.Control.Margin = new Padding(0, 3, 0, 3);
            table.Controls.Add(label, column, row);
            table.Controls.Add(item.Control, column + 1, row);
            _inputLabels[item.Key] = label;
            _inputControls[item.Key] = item.Control;
        }
    }

    private static void ConfigureCompactInputControl(Control control, int comboWidth, int textWidth)
    {
        var preferredWidth = control.Tag as string == "UnitInput"
            ? 152
            : control is ComboBox ? comboWidth : textWidth;
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left;
        control.Width = preferredWidth;
        control.MaximumSize = new Size(preferredWidth, 0);
        control.MinimumSize = new Size(Math.Min(86, preferredWidth), 0);
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

    private void ApplyResponsiveLayout(bool force = false)
    {
        var shouldStack = ClientSize.Width < 980;
        if (!force && _contentLayout.Controls.Count > 0 && shouldStack == _stackedLayout)
            return;

        _stackedLayout = shouldStack;
        _contentLayout.SuspendLayout();
        try
        {
            _contentLayout.Controls.Clear();
            _contentLayout.ColumnStyles.Clear();
            _contentLayout.RowStyles.Clear();

            _ductCalculationsShowButton.Visible = _ductCalculationsCollapsed;
            _resultsCard.Visible = !_ductCalculationsCollapsed;
            if (_ductCalculationsCollapsed)
            {
                _contentLayout.ColumnCount = 1;
                _contentLayout.RowCount = 1;
                _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                _inputCard.Margin = Padding.Empty;
                _contentLayout.Controls.Add(_inputCard, 0, 0);
                return;
            }

            if (shouldStack)
            {
                _contentLayout.ColumnCount = 1;
                _contentLayout.RowCount = 2;
                _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
                _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
                _inputCard.Margin = new Padding(0, 0, 0, 6);
                _resultsCard.Margin = new Padding(0, 6, 0, 0);
                _contentLayout.Controls.Add(_inputCard, 0, 0);
                _contentLayout.Controls.Add(_resultsCard, 0, 1);
            }
            else
            {
                _contentLayout.ColumnCount = 2;
                _contentLayout.RowCount = 1;
                _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)(_ductCalculatorSplitRatio * 100.0)));
                _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)((1.0 - _ductCalculatorSplitRatio) * 100.0)));
                _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                _inputCard.Margin = Padding.Empty;
                _resultsCard.Margin = Padding.Empty;
                _ductCalculatorSplitter.Panel1.Controls.Add(_inputCard);
                _ductCalculatorSplitter.Panel2.Controls.Add(_resultsCard);
                _contentLayout.Controls.Add(_ductCalculatorSplitter, 0, 0);
                _contentLayout.SetColumnSpan(_ductCalculatorSplitter, 2);
                ApplyDuctCalculatorSplitterRatio();
            }
        }
        finally
        {
            _contentLayout.ResumeLayout(true);
        }
    }

    internal static void ConfigureCalculatorSplitter(SplitContainer splitter, string name)
    {
        splitter.Name = name;
        splitter.Dock = DockStyle.Fill;
        splitter.Orientation = Orientation.Vertical;
        splitter.IsSplitterFixed = false;
        splitter.FixedPanel = FixedPanel.None;
        splitter.SplitterWidth = 10;
        splitter.Size = new Size(900, 600);
        splitter.SplitterDistance = 440;
        splitter.Panel1MinSize = 280;
        splitter.Panel2MinSize = 280;
        splitter.BackColor = UiPalette.Canvas;
        splitter.Margin = Padding.Empty;
        splitter.TabStop = true;
        splitter.AccessibleDescription = "Drag the center divider left or right to resize the input and calculation columns.";
    }

    private void ApplyDuctCalculatorSplitterRatio()
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

    private void UpdateDuctCalculatorColumnStyles()
    {
        if (_contentLayout.ColumnStyles.Count < 2)
            return;
        _contentLayout.ColumnStyles[0].SizeType = SizeType.Percent;
        _contentLayout.ColumnStyles[0].Width = (float)(_ductCalculatorSplitRatio * 100.0);
        _contentLayout.ColumnStyles[1].SizeType = SizeType.Percent;
        _contentLayout.ColumnStyles[1].Width = (float)((1.0 - _ductCalculatorSplitRatio) * 100.0);
    }

    internal static ComboBox CreateDropDown() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        IntegralHeight = false,
        DropDownHeight = 240,
        FlatStyle = FlatStyle.Standard,
        BackColor = UiPalette.SurfaceShade,
        ForeColor = UiPalette.Ink
    };

    internal static void ConfigureComboDropDown(ComboBox combo)
    {
        combo.MaxDropDownItems = Math.Clamp(combo.Items.Count, 2, 12);
        combo.DropDown += (_, _) => ResizeComboDropDown(combo);
        combo.Resize += (_, _) => ResizeComboDropDown(combo);
        ResizeComboDropDown(combo);
    }

    internal static void ResizeComboDropDown(ComboBox combo)
    {
        if (combo.Items.Count == 0)
            return;
        try
        {
            var measured = combo.Items.Cast<object>()
                .Select(item => TextRenderer.MeasureText(combo.GetItemText(item), combo.Font).Width)
                .DefaultIfEmpty(combo.Width)
                .Max();
            var contentWidth = Math.Min(measured + SystemInformation.VerticalScrollBarWidth + 24, 520);
            // A ComboBox never permits its popup to be narrower than the control. Keep
            // the clamp bounds ordered when responsive layouts make the control very wide.
            combo.DropDownWidth = Math.Max(Math.Max(90, combo.Width), contentWidth);
            combo.MaxDropDownItems = Math.Clamp(combo.Items.Count, 2, 12);
        }
        catch (ArgumentException)
        {
            // Retain a usable, focusable control even if Windows rejects a platform-
            // specific popup measurement. The user can continue from the exact input.
            combo.DropDownWidth = Math.Max(1, combo.Width);
            combo.Focus();
        }
    }

    internal static TextBox CreateInput(string text) => new()
    {
        Text = text,
        TextAlign = HorizontalAlignment.Right,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.White,
        ForeColor = UiPalette.Ink
    };

    internal static T Selected<T>(ComboBox combo, T fallback) where T : struct
    {
        return combo.SelectedItem is Choice<T> choice ? choice.Value : fallback;
    }

    private static double ReadFinite(TextBox box, string name)
    {
        if (!double.TryParse(box.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value)
            && !double.TryParse(box.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
            throw new FormatException($"Enter a valid number for {name}.");
        if (!double.IsFinite(value))
            throw new FormatException($"Enter a finite number for {name}.");
        return value;
    }

    internal static double ReadPositive(TextBox box, string name)
    {
        var value = ReadFinite(box, name);
        if (value <= 0.0)
            throw new ArgumentOutOfRangeException(name, $"{name} must be greater than zero.");
        return value;
    }

    private static double ReadNonNegative(TextBox box, string name)
    {
        var value = ReadFinite(box, name);
        if (value < 0.0)
            throw new ArgumentOutOfRangeException(name, $"{name} cannot be negative.");
        return value;
    }

    private static bool GeometryMatches(DuctGeometry first, DuctGeometry second)
    {
        const double toleranceM = 1e-10;
        return first.Shape == second.Shape
               && Math.Abs(first.DiameterM - second.DiameterM) <= toleranceM
               && Math.Abs(first.WidthM - second.WidthM) <= toleranceM
               && Math.Abs(first.HeightM - second.HeightM) <= toleranceM;
    }

    internal static void ConvertText(TextBox box, Func<double, double> conversion)
    {
        if (double.TryParse(box.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value)
            || double.TryParse(box.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
            box.Text = conversion(value).ToString("0.####", CultureInfo.CurrentCulture);
    }

    internal static void ConvertText(TextBox box, Func<double, double> toSi, Func<double, double> fromSi)
    {
        ConvertText(box, value => fromSi(toSi(value)));
    }
}
