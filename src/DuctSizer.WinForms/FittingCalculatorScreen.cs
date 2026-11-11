using System.Globalization;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed partial class FittingCalculatorScreen
{
    private readonly MainForm _host;

    internal FittingCalculatorScreen(MainForm host)
    {
        _host = host;
    }

    private readonly ComboBox _fittingGroupCombo = MainForm.CreateDropDown();
    private readonly ComboBox _fittingTypeCombo = MainForm.CreateDropDown();
    private readonly ComboBox _fittingShapeCombo = MainForm.CreateDropDown();
    private readonly TextBox _fittingAirflowText = MainForm.CreateInput("1000");
    private readonly TextBox _fittingDiameterText = MainForm.CreateInput("12");
    private readonly TextBox _fittingWidthText = MainForm.CreateInput("24");
    private readonly TextBox _fittingHeightText = MainForm.CreateInput("12");
    private readonly TextBox _fittingTemperatureText = MainForm.CreateInput("70");
    private readonly PictureBox _fittingPreviewPictureBox = new();
    private readonly PictureBox _fittingCalculationImage = new();
    private readonly Label _fittingPreviewCaption = new();
    private readonly Button _browseFittingImagesButton = new();
    private readonly TableLayoutPanel _fittingBaseInputTable = new();
    private readonly Panel _fittingInputScroll = new();
    private readonly TableLayoutPanel _fittingRectangularGeometryPanel = new();
    private readonly TableLayoutPanel _fittingParameterTable = new();
    private readonly TableLayoutPanel _junctionInputTable = new();
    private readonly Dictionary<string, Control> _fittingParameterInputs = new();
    private readonly Label _fittingSourceLabel = new();
    private readonly Label _fittingAutomationLabel = new();
    private readonly Label _fittingAirflowLabel = new();
    private readonly Label _fittingShapeLabel = new();
    private readonly Label _fittingDiameterLabel = new();
    private readonly Label _fittingWidthLabel = new();
    private readonly Label _fittingHeightLabel = new();
    private readonly Label _fittingTemperatureLabel = new();
    private readonly Label _fittingCoefficientValue = MainForm.CreateSummaryValue();
    private readonly Label _fittingVelocityValue = MainForm.CreateSummaryValue();
    private readonly Label _fittingVelocityPressureValue = MainForm.CreateSummaryValue();
    private readonly Label _fittingPressureLossValue = MainForm.CreateSummaryValue();
    private Label? _fittingCoefficientCaption;
    private Label? _fittingVelocityCaption;
    private Label? _fittingVelocityPressureCaption;
    private Label? _fittingPressureLossCaption;
    private GrdPerformanceSelection? _lastGrdPerformanceSelection;
    private VavPerformanceSelection? _lastVavPerformanceSelection;
    private readonly RichTextBox _fittingCalculationDetails = new();
    internal readonly Label _fittingStatusLabel = new();
    internal readonly Button _addFittingButton = new();
    internal readonly Label _fittingEditingLabel = new();
    private readonly ComboBox _junctionPathCombo = MainForm.CreateDropDown();
    private readonly TextBox _junctionStraightAirflowText = MainForm.CreateInput("600");
    private readonly ComboBox _junctionStraightShapeCombo = MainForm.CreateDropDown();
    private readonly TextBox _junctionStraightDiameterText = MainForm.CreateInput("12");
    private readonly TextBox _junctionStraightWidthText = MainForm.CreateInput("24");
    private readonly TextBox _junctionStraightHeightText = MainForm.CreateInput("12");
    private readonly TextBox _junctionBranchAirflowText = MainForm.CreateInput("400");
    private readonly ComboBox _junctionBranchShapeCombo = MainForm.CreateDropDown();
    private readonly TextBox _junctionBranchDiameterText = MainForm.CreateInput("10");
    private readonly TextBox _junctionBranchWidthText = MainForm.CreateInput("12");
    private readonly TextBox _junctionBranchHeightText = MainForm.CreateInput("10");
    private readonly Label _junctionStraightAirflowLabel = new();
    private readonly Label _junctionStraightDiameterLabel = new();
    private readonly Label _junctionStraightWidthLabel = new();
    private readonly Label _junctionStraightHeightLabel = new();
    private readonly Label _junctionBranchAirflowLabel = new();
    private readonly Label _junctionBranchDiameterLabel = new();
    private readonly Label _junctionBranchWidthLabel = new();
    private readonly Label _junctionBranchHeightLabel = new();
    internal string _fittingDescription = string.Empty;
    private bool _resizingFittingParameterRows;
    private FittingCalculationResult? _lastFittingCalculation;
    private TableLayoutPanel? _fittingContentLayout;
    private readonly SplitContainer _fittingCalculatorSplitter = new();
    private RoundedPanel? _fittingInputCard;
    private RoundedPanel? _fittingOutputCard;
    private bool _fittingCalculationsCollapsed;
    private bool _fittingCalculationsExpanded;
    private bool _updatingFittingCalculatorSplitter;
    private double _fittingCalculatorSplitRatio = 0.50;
    private double _fittingCalculatorRestoreRatio = 0.50;
    private readonly Button _fittingCalculationsShowButton = MainForm.CreatePaneButton("Show Calculations", 172);
    private readonly Button _fittingCalculationsHideButton = MainForm.CreatePaneButton("Hide", 58);
    private readonly Button _fittingCalculationsExpandButton = MainForm.CreatePaneButton("Expand", 88);

    internal Control BuildFittingLossPage()
    {
        MainForm.ConfigureCalculatorSplitter(_fittingCalculatorSplitter, "FittingCalculatorSplitter");
        _fittingCalculatorSplitter.SplitterMoved += (_, _) =>
        {
            if (_updatingFittingCalculatorSplitter
                || _fittingCalculatorSplitter.ClientSize.Width <= _fittingCalculatorSplitter.SplitterWidth)
                return;
            _fittingCalculatorSplitRatio = Math.Clamp(
                _fittingCalculatorSplitter.SplitterDistance
                / (double)(_fittingCalculatorSplitter.ClientSize.Width - _fittingCalculatorSplitter.SplitterWidth), .25, .75);
            if (!_fittingCalculationsExpanded)
                _fittingCalculatorRestoreRatio = _fittingCalculatorSplitRatio;
            UpdateFittingCalculatorColumnStyles();
        };
        _fittingCalculatorSplitter.Resize += (_, _) => ApplyFittingCalculatorSplitterRatio();
        _fittingGroupCombo.Name = "FittingGroupCombo";
        _fittingTypeCombo.Name = "FittingTypeCombo";
        _fittingShapeCombo.Name = "FittingShapeCombo";
        _fittingAirflowText.Name = "FittingAirflowText";
        _fittingDiameterText.Name = "FittingDiameterText";
        _fittingWidthText.Name = "FittingWidthText";
        _fittingHeightText.Name = "FittingHeightText";
        _fittingTemperatureText.Name = "FittingTemperatureText";
        _fittingBaseInputTable.Name = "FittingBaseInputTable";
        _fittingShapeLabel.Name = "FittingShapeLabel";
        _fittingTemperatureLabel.Name = "FittingTemperatureLabel";
        _fittingAutomationLabel.Name = "FittingAutomationLabel";
        _fittingPreviewPictureBox.Name = "FittingPreviewPictureBox";
        _fittingPreviewCaption.Name = "FittingPreviewCaption";
        _browseFittingImagesButton.Name = "BrowseFittingImagesButton";
        _fittingVelocityValue.Name = "FittingVelocityValue";
        _fittingCalculationDetails.Name = "FittingCalculationDetails";
        _junctionPathCombo.Name = "JunctionPathCombo";
        _junctionInputTable.Name = "JunctionInputTable";
        _junctionStraightAirflowText.Name = "JunctionStraightAirflowText";
        _junctionBranchAirflowText.Name = "JunctionBranchAirflowText";
        _addFittingButton.Name = "AddFittingButton";
        _fittingGroupCombo.Items.AddRange(FittingCatalog.SelectionGroups.Cast<object>().ToArray());
        _fittingGroupCombo.SelectedIndex = 0;
        _fittingTypeCombo.DisplayMember = nameof(FittingDefinition.Code);
        RefreshFittingTypeChoices();
        _fittingShapeCombo.Items.AddRange(new object[]
        {
            new MainForm.Choice<DuctShape>("Round", DuctShape.Round),
            new MainForm.Choice<DuctShape>("Rectangular", DuctShape.Rectangular)
        });
        _fittingShapeCombo.SelectedIndex = 0;
        _junctionPathCombo.Items.AddRange(new object[]
        {
            new MainForm.Choice<JunctionPath>("Common To Branch", JunctionPath.Branch),
            new MainForm.Choice<JunctionPath>("Common To Straight/Main", JunctionPath.Straight)
        });
        _junctionPathCombo.SelectedIndex = 0;
        foreach (var combo in new[] { _junctionStraightShapeCombo, _junctionBranchShapeCombo })
        {
            combo.Items.AddRange(new object[]
            {
                new MainForm.Choice<DuctShape>("Round", DuctShape.Round),
                new MainForm.Choice<DuctShape>("Rectangular", DuctShape.Rectangular)
            });
            combo.SelectedIndex = 0;
        }
        foreach (var combo in new[]
                 {
                     _fittingGroupCombo, _fittingTypeCombo, _fittingShapeCombo, _junctionPathCombo,
                     _junctionStraightShapeCombo, _junctionBranchShapeCombo
                 })
            MainForm.ConfigureComboDropDown(combo);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = _host.BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // A slim title band: the main tabs and ribbon already say where you are (v0.96).
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var header = new SectionTitlePanel
        {
            Name = "FittingSectionHeader",
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Blue
        };
        header.Controls.Add(new Label
        {
            Text = "Fitting Calculator",
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(20, 2, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13F),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        });
        header.Controls.Add(new Label
        {
            Text = "ASHRAE fittings • manufacturer GRD performance • airflow • pressure loss",
            Dock = DockStyle.Bottom,
            Height = 22,
            Padding = new Padding(22, 0, 0, 6),
            ForeColor = Color.FromArgb(215, 229, 242),
            Font = new Font("Segoe UI", 8.5F),
            BackColor = Color.Transparent
        });

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(14),
            BackColor = _host.BackColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _fittingContentLayout = content;

        var inputCard = new RoundedPanel
        {
            Name = "FittingInputCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 12,
            BorderColor = UiPalette.CardBorder,
            Margin = new Padding(0, 0, 7, 0)
        };
        _fittingInputCard = inputCard;
        var inputHeader = MainForm.CreateCardHeader("FITTING INPUTS", string.Empty);
        _fittingInputScroll.Name = "FittingInputScroll";
        _fittingInputScroll.Dock = DockStyle.Fill;
        _fittingInputScroll.AutoScroll = true;
        _fittingInputScroll.BackColor = Color.White;
        var inputTable = _fittingBaseInputTable;
        inputTable.Dock = DockStyle.Top;
        inputTable.AutoSize = true;
        inputTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        inputTable.ColumnCount = 4;
        inputTable.RowCount = 0;
        inputTable.ColumnStyles.Clear();
        inputTable.RowStyles.Clear();
        inputTable.Padding = new Padding(16, 12, 16, 14);
        inputTable.BackColor = Color.White;
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        inputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));

        _fittingEditingLabel.Name = "FittingEditingLabel";
        _fittingEditingLabel.AutoSize = true;
        _fittingEditingLabel.Dock = DockStyle.Top;
        _fittingEditingLabel.BackColor = Color.FromArgb(232, 244, 253);
        _fittingEditingLabel.ForeColor = Color.FromArgb(22, 65, 102);
        _fittingEditingLabel.Font = new Font("Segoe UI Semibold", 9F);
        _fittingEditingLabel.Padding = new Padding(10, 7, 10, 7);
        _fittingEditingLabel.Margin = new Padding(0, 0, 0, 4);
        _fittingEditingLabel.Visible = false;
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_fittingEditingLabel, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(_fittingEditingLabel, 4);
        inputTable.RowCount++;

        var fittingSelectorPanel = BuildFittingSelectorPanel();
        inputTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
        inputTable.Controls.Add(fittingSelectorPanel, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(fittingSelectorPanel, 4);
        inputTable.RowCount++;

        BuildJunctionInputRows();
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_junctionInputTable, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(_junctionInputTable, 4);
        inputTable.RowCount++;

        _fittingAutomationLabel.Dock = DockStyle.Top;
        _fittingAutomationLabel.AutoSize = true;
        _fittingAutomationLabel.MaximumSize = new Size(700, 0);
        _fittingAutomationLabel.Padding = new Padding(6, 4, 6, 4);
        _fittingAutomationLabel.Margin = new Padding(0, 4, 0, 2);
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_fittingAutomationLabel, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(_fittingAutomationLabel, 4);
        inputTable.RowCount++;

        _fittingParameterTable.Dock = DockStyle.Top;
        _fittingParameterTable.AutoSize = true;
        _fittingParameterTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _fittingParameterTable.ColumnCount = 2;
        _fittingParameterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _fittingParameterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        _fittingParameterTable.SizeChanged += (_, _) => ResizeFittingParameterRows();
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_fittingParameterTable, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(_fittingParameterTable, 4);
        inputTable.RowCount++;

        _fittingSourceLabel.Dock = DockStyle.Top;
        _fittingSourceLabel.AutoSize = true;
        _fittingSourceLabel.MaximumSize = new Size(700, 0);
        _fittingSourceLabel.ForeColor = Color.FromArgb(76, 92, 108);
        _fittingSourceLabel.Padding = new Padding(0, 4, 0, 4);
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(_fittingSourceLabel, 0, inputTable.RowCount);
        inputTable.SetColumnSpan(_fittingSourceLabel, 4);
        inputTable.RowCount++;
        inputTable.RowStyles[1].SizeType = SizeType.Absolute;
        inputTable.RowStyles[1].Height = 236F;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 310,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(4, 3, 0, 3),
            BackColor = Color.White
        };
        var calculate = new Button
        {
            Name = "CalculateFittingButton",
            Text = "Calculate",
            Size = new Size(112, 30),
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F),
            FlatStyle = FlatStyle.Flat
        };
        calculate.FlatAppearance.BorderSize = 0;
        calculate.Margin = Padding.Empty;
        UiPolish.RoundButton(calculate);
        calculate.Click += (_, _) => CalculateFittingAndDisplay();
        _addFittingButton.Text = DuctSystemScreen.DefaultFittingActionText;
        _addFittingButton.Size = new Size(176, 30);
        _addFittingButton.Margin = Padding.Empty;
        _addFittingButton.BackColor = Color.FromArgb(42, 135, 91);
        _addFittingButton.ForeColor = Color.White;
        _addFittingButton.Font = new Font("Segoe UI Semibold", 8.5F);
        _addFittingButton.FlatStyle = FlatStyle.Flat;
        _addFittingButton.FlatAppearance.BorderSize = 0;
        UiPolish.RoundButton(_addFittingButton);
        _addFittingButton.Click += (_, _) => AddCurrentFittingToSystem();
        _addFittingButton.Visible = false;
        buttons.Controls.Add(_addFittingButton);
        calculate.Margin = new Padding(0, 0, 8, 0);
        buttons.Controls.Add(calculate);
        var outputCard = new RoundedPanel
        {
            Name = "FittingOutputCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 12,
            BorderColor = UiPalette.CardBorder,
            Margin = new Padding(7, 0, 0, 0)
        };
        _fittingOutputCard = outputCard;
        var outputHeader = MainForm.CreateCardHeader("CALCULATIONS", "Geometry, flow, air, loss method, and assumptions.");
        MainForm.AddCalculationPaneControls(inputHeader, outputHeader,
            _fittingCalculationsShowButton, _fittingCalculationsHideButton, _fittingCalculationsExpandButton,
            () =>
            {
                _fittingCalculationsCollapsed = false;
                ApplyFittingCalculationsLayout();
            },
            () =>
            {
                _fittingCalculationsCollapsed = true;
                ApplyFittingCalculationsLayout();
            },
            () =>
            {
                if (_fittingCalculationsExpanded)
                {
                    _fittingCalculationsExpanded = false;
                    _fittingCalculatorSplitRatio = _fittingCalculatorRestoreRatio;
                }
                else
                {
                    _fittingCalculatorRestoreRatio = _fittingCalculatorSplitRatio;
                    _fittingCalculationsExpanded = true;
                    _fittingCalculatorSplitRatio = .34;
                }
                _fittingCalculationsExpandButton.Text = _fittingCalculationsExpanded ? "Restore" : "Expand";
                ApplyFittingCalculationsLayout();
            });
        var summary = new TableLayoutPanel
        {
            Name = "FittingSummaryTable",
            Dock = DockStyle.Fill,
            Height = 146,
            Padding = new Padding(8, 7, 8, 7),
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.White
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        summary.Controls.Add(CreateFittingSummaryCard("COEFFICIENT C", _fittingCoefficientValue, out _fittingCoefficientCaption), 0, 0);
        summary.Controls.Add(CreateFittingSummaryCard("DUCT VELOCITY", _fittingVelocityValue, out _fittingVelocityCaption), 1, 0);
        summary.Controls.Add(CreateFittingSummaryCard("VELOCITY PRESSURE", _fittingVelocityPressureValue, out _fittingVelocityPressureCaption), 0, 1);
        summary.Controls.Add(CreateFittingSummaryCard("PRESSURE LOSS", _fittingPressureLossValue, out _fittingPressureLossCaption), 1, 1);
        foreach (var valueLabel in new[]
                 {
                     _fittingCoefficientValue, _fittingVelocityValue,
                     _fittingVelocityPressureValue, _fittingPressureLossValue
                 })
        {
            valueLabel.Padding = new Padding(0, 2, 0, 0);
            valueLabel.Font = new Font("Segoe UI Semibold", 9.3F);
        }

        var outputHeading = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        outputHeading.Controls.Add(MainForm.CreateOutputSectionLabel("FittingOutputsHeader"));
        var actionRow = new Panel
        {
            Name = "FittingActionRow",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = Padding.Empty
        };
        actionRow.Controls.Add(buttons);

        _fittingInputScroll.Controls.Add(inputTable);

        var inputBody = new TableLayoutPanel
        {
            Name = "FittingInputAndSummaryLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        MainForm.ConfigureCalculatorInputBody(inputBody, _fittingInputScroll, inputTable,
            outputHeading, summary, actionRow, 108, 40);
        inputCard.Controls.Add(inputBody);
        inputCard.Controls.Add(inputHeader);

        _fittingCalculationDetails.Dock = DockStyle.Fill;
        _fittingCalculationDetails.Multiline = true;
        _fittingCalculationDetails.ReadOnly = true;
        _fittingCalculationDetails.BorderStyle = BorderStyle.None;
        _fittingCalculationDetails.BackColor = Color.White;
        _fittingCalculationDetails.ForeColor = Color.FromArgb(54, 70, 86);
        _fittingCalculationDetails.Font = new Font("Segoe UI", 9.5F);
        _fittingCalculationDetails.ScrollBars = RichTextBoxScrollBars.Vertical;
        _fittingCalculationDetails.DetectUrls = false;
        _fittingCalculationDetails.Text = DefaultFittingCalculationDetails();
        _fittingCalculationImage.Name = "FittingCalculationSourceImage";
        _fittingCalculationImage.Dock = DockStyle.Fill;
        _fittingCalculationImage.BackColor = Color.White;
        _fittingCalculationImage.BorderStyle = BorderStyle.FixedSingle;
        _fittingCalculationImage.SizeMode = PictureBoxSizeMode.Zoom;
        _fittingCalculationImage.Cursor = Cursors.Hand;
        _fittingCalculationImage.AccessibleName = "Selected ASHRAE source table; click to enlarge";
        _fittingCalculationImage.Click += (_, _) => ShowSelectedFittingImage();

        var sourceImageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 14, 20, 8),
            BackColor = Color.White
        };
        sourceImageHost.Controls.Add(_fittingCalculationImage);
        var explanationHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 8, 20, 18),
            BackColor = Color.White
        };
        explanationHost.Controls.Add(_fittingCalculationDetails);
        var calculationBody = new TableLayoutPanel
        {
            Name = "FittingCalculationBody",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        calculationBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        calculationBody.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        calculationBody.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        calculationBody.Controls.Add(sourceImageHost, 0, 0);
        calculationBody.Controls.Add(explanationHost, 0, 1);
        outputCard.Controls.Add(calculationBody);
        outputCard.Controls.Add(outputHeader);

        ApplyFittingCalculationsLayout();

        _fittingStatusLabel.Dock = DockStyle.Fill;
        _fittingStatusLabel.Padding = new Padding(18, 0, 10, 0);
        _fittingStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _fittingStatusLabel.ForeColor = Color.FromArgb(80, 96, 112);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(content, 0, 1);
        root.Controls.Add(_fittingStatusLabel, 0, 2);

        _fittingGroupCombo.SelectedIndexChanged += (_, _) => RefreshFittingTypeChoices();
        _fittingTypeCombo.SelectedIndexChanged += (_, _) =>
        {
            FillSelectedFittingDescription();
            LoadSelectedFittingPreview();
            RebuildFittingParameterInputs();
            CalculateFittingAndDisplay();
            ResetFittingInputScroll();
        };
        _fittingShapeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateFittingGeometryVisibility();
            CalculateFittingAndDisplay();
        };
        _junctionPathCombo.SelectedIndexChanged += (_, _) => CalculateFittingAndDisplay();
        _junctionStraightShapeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateJunctionGeometryVisibility();
            CalculateFittingAndDisplay();
        };
        _junctionBranchShapeCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdateJunctionGeometryVisibility();
            CalculateFittingAndDisplay();
        };
        foreach (var input in new[]
                 {
                     _junctionStraightAirflowText, _junctionStraightDiameterText, _junctionStraightWidthText,
                     _junctionStraightHeightText, _junctionBranchAirflowText, _junctionBranchDiameterText,
                     _junctionBranchWidthText, _junctionBranchHeightText
                 })
            input.Leave += (_, _) => CalculateFittingAndDisplay();
        FillSelectedFittingDescription();
        LoadSelectedFittingPreview();
        RebuildFittingParameterInputs();
        UpdateFittingGeometryVisibility();
        UpdateJunctionGeometryVisibility();
        UpdateFittingUnitLabels();
        CalculateFittingAndDisplay();
        return root;
    }

    private void ApplyFittingCalculationsLayout()
    {
        if (_fittingContentLayout is null || _fittingInputCard is null || _fittingOutputCard is null)
            return;
        var content = _fittingContentLayout;
        content.SuspendLayout();
        try
        {
            content.Controls.Clear();
            _fittingCalculationsShowButton.Visible = _fittingCalculationsCollapsed;
            _fittingOutputCard.Visible = !_fittingCalculationsCollapsed;
            content.ColumnStyles.Clear();
            if (_fittingCalculationsCollapsed)
            {
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
                content.SetColumnSpan(_fittingInputCard, 2);
                _fittingInputCard.Margin = Padding.Empty;
                content.Controls.Add(_fittingInputCard, 0, 0);
            }
            else
            {
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)(_fittingCalculatorSplitRatio * 100.0)));
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (float)((1.0 - _fittingCalculatorSplitRatio) * 100.0)));
                _fittingInputCard.Margin = Padding.Empty;
                _fittingOutputCard.Margin = Padding.Empty;
                _fittingCalculatorSplitter.Panel1.Controls.Add(_fittingInputCard);
                _fittingCalculatorSplitter.Panel2.Controls.Add(_fittingOutputCard);
                content.Controls.Add(_fittingCalculatorSplitter, 0, 0);
                content.SetColumnSpan(_fittingCalculatorSplitter, 2);
                ApplyFittingCalculatorSplitterRatio();
            }
        }
        finally
        {
            content.ResumeLayout(true);
        }
    }

    private static Panel CreateFittingSummaryCard(string caption, Label value, out Label captionLabel)
    {
        var card = MainForm.CreateSummaryCard(caption, value);
        captionLabel = card.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<Label>()
            .First(label => !ReferenceEquals(label, value));
        return card;
    }

    private void SetFittingSummaryCaptions(bool performanceGrd, bool grille = false)
    {
        if (_fittingCoefficientCaption is null || _fittingVelocityCaption is null
            || _fittingVelocityPressureCaption is null || _fittingPressureLossCaption is null)
            return;
        _fittingCoefficientCaption.Text = performanceGrd ? "SELECTED AIRFLOW:" : "COEFFICIENT C:";
        _fittingVelocityCaption.Text = performanceGrd ? (grille ? "STATIC PRESSURE:" : "TOTAL PRESSURE:") : "DUCT VELOCITY:";
        _fittingVelocityPressureCaption.Text = performanceGrd ? "SOUND:" : "VELOCITY PRESSURE:";
        _fittingPressureLossCaption.Text = performanceGrd ? (grille ? "CORE VELOCITY:" : "THROW:") : "PRESSURE LOSS:";
    }

    private void SetVavSummaryCaptions()
    {
        if (_fittingCoefficientCaption is null || _fittingVelocityCaption is null
            || _fittingVelocityPressureCaption is null || _fittingPressureLossCaption is null)
            return;
        _fittingCoefficientCaption.Text = "VAV SIZE:";
        _fittingVelocityCaption.Text = "AIRFLOW:";
        _fittingVelocityPressureCaption.Text = "CONFIGURATION:";
        _fittingPressureLossCaption.Text = "AIRSIDE ΔP:";
    }

    private void ApplyFittingCalculatorSplitterRatio()
    {
        if (_fittingCalculatorSplitter.ClientSize.Width <= _fittingCalculatorSplitter.SplitterWidth
            || _fittingCalculatorSplitter.Panel1Collapsed || _fittingCalculatorSplitter.Panel2Collapsed)
            return;
        var available = _fittingCalculatorSplitter.ClientSize.Width - _fittingCalculatorSplitter.SplitterWidth;
        var minimum = _fittingCalculatorSplitter.Panel1MinSize;
        var maximum = available - _fittingCalculatorSplitter.Panel2MinSize;
        if (maximum < minimum)
            return;
        var distance = Math.Clamp((int)Math.Round(available * _fittingCalculatorSplitRatio), minimum, maximum);
        _updatingFittingCalculatorSplitter = true;
        try
        {
            if (_fittingCalculatorSplitter.SplitterDistance != distance)
                _fittingCalculatorSplitter.SplitterDistance = distance;
        }
        finally
        {
            _updatingFittingCalculatorSplitter = false;
        }
        UpdateFittingCalculatorColumnStyles();
    }

    private void UpdateFittingCalculatorColumnStyles()
    {
        if (_fittingContentLayout is null || _fittingContentLayout.ColumnStyles.Count < 2)
            return;
        _fittingContentLayout.ColumnStyles[0].SizeType = SizeType.Percent;
        _fittingContentLayout.ColumnStyles[0].Width = (float)(_fittingCalculatorSplitRatio * 100.0);
        _fittingContentLayout.ColumnStyles[1].SizeType = SizeType.Percent;
        _fittingContentLayout.ColumnStyles[1].Width = (float)((1.0 - _fittingCalculatorSplitRatio) * 100.0);
    }

    internal void ResetFittingInputScroll()
    {
        if (_fittingInputScroll.IsDisposed || !_fittingInputScroll.IsHandleCreated)
            return;

        void ResetPosition()
        {
            if (_fittingInputScroll.IsDisposed || !_fittingInputScroll.IsHandleCreated)
                return;
            _fittingInputScroll.SuspendLayout();
            _fittingInputScroll.AutoScrollPosition = Point.Empty;
            _fittingInputScroll.ResumeLayout(true);
            _fittingInputScroll.PerformLayout();
            _fittingInputScroll.AutoScrollPosition = Point.Empty;
            if (_fittingInputScroll.VerticalScroll.Visible)
                _fittingInputScroll.VerticalScroll.Value = _fittingInputScroll.VerticalScroll.Minimum;
            _fittingInputScroll.Invalidate();
        }

        ResetPosition();
        _host.BeginInvoke(ResetPosition);
    }

    private Control BuildFittingSelectorPanel()
    {
        var selector = new TableLayoutPanel
        {
            Name = "FittingSelectorPanel",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(0, 4, 0, 8),
            Padding = new Padding(8),
            MinimumSize = new Size(0, 236)
        };
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        selector.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var selection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            BackColor = selector.BackColor,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(2, 4, 4, 4)
        };
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        for (var row = 0; row < 6; row++)
            selection.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        var groupLabel = new Label
        {
            Name = "FittingGroupLabel",
            Text = "Fitting Group",
            Font = new Font("Segoe UI Semibold", 8.2F)
        };
        var typeLabel = new Label
        {
            Name = "FittingTypeLabel",
            Text = "Fitting Number",
            Font = new Font("Segoe UI Semibold", 8.2F)
        };
        _fittingAirflowLabel.Text = "Airflow (cfm)";
        _fittingTemperatureLabel.Text = "Air Temperature (°F)";
        _fittingShapeLabel.Text = "Reference Duct Shape";
        _fittingDiameterLabel.Text = "Diameter (in)";
        _fittingWidthLabel.Text = "Width (in)";
        _fittingHeightLabel.Text = "Height (in)";

        AddSelectorRow(groupLabel, _fittingGroupCombo, 0, 118, 118);
        AddSelectorRow(typeLabel, _fittingTypeCombo, 1, 96, 96);
        AddSelectorRow(_fittingAirflowLabel, _fittingAirflowText, 2, 96, 84);
        AddSelectorRow(_fittingTemperatureLabel, _fittingTemperatureText, 3, 96, 84);
        AddSelectorRow(_fittingShapeLabel, _fittingShapeCombo, 4, 112, 96);
        AddSelectorRow(_fittingDiameterLabel, _fittingDiameterText, 5, 96, 84);
        ConfigureRectangularGeometryRow();
        selection.Controls.Add(_fittingRectangularGeometryPanel, 0, 5);
        selection.SetColumnSpan(_fittingRectangularGeometryPanel, 2);
        _fittingTypeCombo.MaxDropDownItems = 16;

        selector.Controls.Add(selection, 0, 0);
        selector.Controls.Add(BuildFittingPreviewPanel(), 1, 0);
        return selector;

        void AddSelectorRow(Label label, Control control, int row, int comboWidth, int textWidth)
        {
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.ForeColor = Color.FromArgb(34, 62, 88);
            label.Font = new Font("Segoe UI", 8.2F);
            label.AutoEllipsis = false;
            label.UseMnemonic = false;
            label.Margin = new Padding(0, 3, 4, 3);
            ConfigureCompactFittingControl(control, comboWidth, textWidth);
            control.Margin = new Padding(0, 1, 0, 1);
            selection.Controls.Add(label, 0, row);
            selection.Controls.Add(control, 1, row);
        }

        void ConfigureRectangularGeometryRow()
        {
            _fittingRectangularGeometryPanel.Dock = DockStyle.Fill;
            _fittingRectangularGeometryPanel.Margin = Padding.Empty;
            _fittingRectangularGeometryPanel.Padding = Padding.Empty;
            _fittingRectangularGeometryPanel.BackColor = selection.BackColor;
            _fittingRectangularGeometryPanel.ColumnCount = 4;
            _fittingRectangularGeometryPanel.RowCount = 1;
            _fittingRectangularGeometryPanel.ColumnStyles.Clear();
            _fittingRectangularGeometryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            _fittingRectangularGeometryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            _fittingRectangularGeometryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            _fittingRectangularGeometryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            AddGeometryPair(_fittingWidthLabel, _fittingWidthText, 0);
            AddGeometryPair(_fittingHeightLabel, _fittingHeightText, 2);
        }

        void AddGeometryPair(Label label, Control control, int column)
        {
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.ForeColor = Color.FromArgb(34, 62, 88);
            label.Font = new Font("Segoe UI", 7.8F);
            label.AutoEllipsis = false;
            label.UseMnemonic = false;
            label.Margin = new Padding(column == 0 ? 0 : 3, 1, 3, 1);
            ConfigureCompactFittingControl(control, 66, 60);
            control.Margin = new Padding(0, 2, 0, 2);
            _fittingRectangularGeometryPanel.Controls.Add(label, column, 0);
            _fittingRectangularGeometryPanel.Controls.Add(control, column + 1, 0);
        }
    }

    private Control BuildFittingPreviewPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(4),
            Margin = Padding.Empty
        };
        var captionHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = panel.BackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _fittingPreviewCaption.Dock = DockStyle.Top;
        _fittingPreviewCaption.Height = 52;
        _fittingPreviewCaption.ForeColor = Color.FromArgb(51, 78, 104);
        _fittingPreviewCaption.Font = new Font("Segoe UI Semibold", 8.5F);
        _fittingPreviewCaption.TextAlign = ContentAlignment.MiddleCenter;
        _fittingPreviewCaption.AutoEllipsis = false;
        _fittingPreviewCaption.UseMnemonic = false;
        _fittingPreviewCaption.Margin = Padding.Empty;
        _fittingPreviewCaption.Padding = new Padding(4, 2, 4, 2);
        _browseFittingImagesButton.Text = "Browse Fitting Gallery";
        _browseFittingImagesButton.Dock = DockStyle.None;
        _browseFittingImagesButton.Height = 32;
        _browseFittingImagesButton.FlatStyle = FlatStyle.Flat;
        _browseFittingImagesButton.BackColor = Color.White;
        _browseFittingImagesButton.ForeColor = Color.FromArgb(28, 69, 108);
        _browseFittingImagesButton.FlatAppearance.BorderColor = Color.FromArgb(164, 184, 201);
        UiPolish.RoundButton(_browseFittingImagesButton);
        _browseFittingImagesButton.Click += (_, _) => ShowFittingGallery();
        void ResizeCaption()
        {
            var captionWidth = Math.Max(100, captionHost.ClientSize.Width - 8);
            var measured = TextRenderer.MeasureText(
                _fittingPreviewCaption.Text,
                _fittingPreviewCaption.Font,
                new Size(captionWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            _fittingPreviewCaption.Height = Math.Max(34, measured.Height + 8);
            captionHost.Height = _fittingPreviewCaption.Height;
        }
        captionHost.Resize += (_, _) => ResizeCaption();
        _fittingPreviewCaption.TextChanged += (_, _) => ResizeCaption();
        captionHost.Controls.Add(_fittingPreviewCaption);

        var buttonHost = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            BackColor = panel.BackColor,
            Margin = Padding.Empty
        };
        var previewArea = new Panel { Dock = DockStyle.Fill, BackColor = panel.BackColor };
        _fittingPreviewPictureBox.BackColor = Color.White;
        _fittingPreviewPictureBox.BorderStyle = BorderStyle.FixedSingle;
        _fittingPreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _fittingPreviewPictureBox.Cursor = Cursors.Hand;
        _fittingPreviewPictureBox.AccessibleName = "Selected ASHRAE fitting image; click to enlarge";
        _fittingPreviewPictureBox.Click += (_, _) => ShowSelectedFittingImage();
        void ResizePreview()
        {
            var previewWidth = Math.Max(125, Math.Min(230, previewArea.ClientSize.Width - 10));
            var previewHeight = Math.Max(72, previewArea.ClientSize.Height - 10);
            _fittingPreviewPictureBox.Size = new Size(previewWidth, previewHeight);
            _fittingPreviewPictureBox.Location = new Point(
                Math.Max(0, (previewArea.ClientSize.Width - previewWidth) / 2),
                Math.Max(0, (previewArea.ClientSize.Height - previewHeight) / 2));
            _browseFittingImagesButton.Width = previewWidth;
            _browseFittingImagesButton.Left = Math.Max(0, (buttonHost.ClientSize.Width - previewWidth) / 2);
            _browseFittingImagesButton.Top = Math.Max(0, (buttonHost.ClientSize.Height - _browseFittingImagesButton.Height) / 2);
        }
        previewArea.Resize += (_, _) => ResizePreview();
        buttonHost.Resize += (_, _) => ResizePreview();
        previewArea.Controls.Add(_fittingPreviewPictureBox);
        buttonHost.Controls.Add(_browseFittingImagesButton);

        panel.Controls.Add(previewArea);
        panel.Controls.Add(buttonHost);
        panel.Controls.Add(captionHost);
        return panel;
    }

    private void ShowSelectedFittingImage()
    {
        if (_fittingTypeCombo.SelectedItem is not FittingDefinition definition)
            return;
        using var viewer = new FittingImageViewer(definition);
        viewer.ShowDialog(_host);
    }

    private void ShowFittingGallery()
    {
        var selectedCode = (_fittingTypeCombo.SelectedItem as FittingDefinition)?.Code;
        using var gallery = new FittingGalleryDialog(FittingCatalog.All, selectedCode);
        if (gallery.ShowDialog(_host) != DialogResult.OK || gallery.SelectedDefinition is null)
            return;
        SelectFittingGroup(FittingCatalog.SelectionGroup(gallery.SelectedDefinition), gallery.SelectedDefinition.Code);
    }

    private void RefreshFittingTypeChoices(string? preferredCode = null)
    {
        preferredCode ??= (_fittingTypeCombo.SelectedItem as FittingDefinition)?.Code;
        var group = Convert.ToString(_fittingGroupCombo.SelectedItem, CultureInfo.CurrentCulture)
                    ?? FittingCatalog.SelectionGroups[0];
        _fittingTypeCombo.BeginUpdate();
        _fittingTypeCombo.Items.Clear();
        foreach (var fitting in FittingCatalog.All.Where(x => FittingCatalog.SelectionGroup(x) == group))
            _fittingTypeCombo.Items.Add(fitting);
        _fittingTypeCombo.EndUpdate();
        var preferredIndex = _fittingTypeCombo.Items.Cast<FittingDefinition>()
            .Select((definition, index) => (definition, index))
            .FirstOrDefault(x => string.Equals(x.definition.Code, preferredCode, StringComparison.OrdinalIgnoreCase)).index;
        _fittingTypeCombo.SelectedIndex = _fittingTypeCombo.Items.Count == 0 ? -1 : preferredIndex;
        MainForm.ResizeComboDropDown(_fittingTypeCombo);
    }

    private void SelectFittingGroup(string group, string? preferredCode = null)
    {
        var groupIndex = _fittingGroupCombo.Items.Cast<object>()
            .Select((item, itemIndex) => (Text: Convert.ToString(item, CultureInfo.CurrentCulture), itemIndex))
            .FirstOrDefault(x => string.Equals(x.Text, group, StringComparison.OrdinalIgnoreCase)).itemIndex;
        _fittingGroupCombo.SelectedIndex = groupIndex;
        RefreshFittingTypeChoices(preferredCode);
        if (preferredCode is null)
            return;
        for (var index = 0; index < _fittingTypeCombo.Items.Count; index++)
        {
            if (_fittingTypeCombo.Items[index] is FittingDefinition definition
                && string.Equals(definition.Code, preferredCode, StringComparison.OrdinalIgnoreCase))
            {
                _fittingTypeCombo.SelectedIndex = index;
                break;
            }
        }
    }

    private void FillSelectedFittingDescription()
    {
        if (_fittingTypeCombo.SelectedItem is FittingDefinition definition)
        {
            _fittingDescription = definition.Description;
            _fittingPreviewCaption.Text = $"{definition.Code} — {definition.Description}";
        }
        else
        {
            _fittingDescription = string.Empty;
            _fittingPreviewCaption.Text = string.Empty;
        }
    }

    private void LoadSelectedFittingPreview()
    {
        var previous = _fittingPreviewPictureBox.Image;
        _fittingPreviewPictureBox.Image = null;
        previous?.Dispose();
        var previousCalculationImage = _fittingCalculationImage.Image;
        _fittingCalculationImage.Image = null;
        previousCalculationImage?.Dispose();

        if (_fittingTypeCombo.SelectedItem is not FittingDefinition definition)
            return;

        _fittingPreviewPictureBox.Image = FittingImageResources.Load(definition.Code);
        _fittingCalculationImage.Image = FittingImageResources.Load(definition.Code);
    }

    private static void AddFittingRow(TableLayoutPanel table, string labelText, Control control)
    {
        AddFittingRow(table, new Label
        {
            Text = ToInputTitleCase(labelText),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            UseMnemonic = false,
            Margin = new Padding(0, 4, 8, 4)
        }, control);
    }

    private static string ToInputTitleCase(string text)
    {
        var characters = text.ToCharArray();
        var capitalize = true;
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (char.IsLetter(character))
            {
                if (capitalize)
                    characters[index] = char.ToUpperInvariant(character);
                capitalize = false;
            }
            else if (character is '(' or '[')
                capitalize = false;
            else if (char.IsWhiteSpace(character) || character is '-' or '/')
                capitalize = true;
        }
        return new string(characters);
    }

    private static void AddFittingRow(TableLayoutPanel table, Label label, Control control, int rowHeight = 40)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleRight;
        label.AutoEllipsis = false;
        label.UseMnemonic = false;
        ConfigureCompactFittingControl(control, 220, 150);
        var verticalMargin = Math.Max(3, (rowHeight - 32) / 2);
        control.Margin = new Padding(0, verticalMargin, 0, verticalMargin);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
        if (table.ColumnCount > 2)
            table.SetColumnSpan(control, table.ColumnCount - 1);
    }

    private static void AddFittingPairRow(TableLayoutPanel table,
        Label firstLabel, Control firstControl, Label secondLabel, Control secondControl, int rowHeight = 42)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        AddPair(firstLabel, firstControl, 0);
        AddPair(secondLabel, secondControl, 2);
        return;

        void AddPair(Label label, Control control, int column)
        {
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.AutoEllipsis = false;
            label.UseMnemonic = false;
            label.Font = new Font("Segoe UI", 8.2F);
            label.Margin = new Padding(column == 0 ? 0 : 8, 4, 3, 4);
            ConfigureCompactFittingControl(control, 165, 120);
            control.Margin = new Padding(0, 5, 0, 5);
            table.Controls.Add(label, column, row);
            table.Controls.Add(control, column + 1, row);
        }
    }

    private static void ConfigureCompactFittingControl(Control control, int comboWidth, int textWidth)
    {
        var preferredWidth = control is ComboBox ? comboWidth : textWidth;
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left;
        control.Width = preferredWidth;
        control.MaximumSize = new Size(preferredWidth, 0);
        control.MinimumSize = new Size(Math.Min(86, preferredWidth), 0);
    }

    private void BuildJunctionInputRows()
    {
        _junctionInputTable.Dock = DockStyle.Top;
        _junctionInputTable.AutoSize = true;
        _junctionInputTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _junctionInputTable.ColumnCount = 2;
        _junctionInputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        _junctionInputTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        _junctionInputTable.Padding = new Padding(0, 8, 0, 4);

        var heading = new Label
        {
            Text = "JUNCTION FLOW PATHS",
            Dock = DockStyle.Fill,
            Height = 30,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _junctionInputTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        _junctionInputTable.Controls.Add(heading, 0, _junctionInputTable.RowCount);
        _junctionInputTable.SetColumnSpan(heading, 2);
        _junctionInputTable.RowCount++;

        AddFittingRow(_junctionInputTable, "Calculated Path", _junctionPathCombo);
        AddFittingRow(_junctionInputTable, _junctionStraightAirflowLabel, _junctionStraightAirflowText);
        AddFittingRow(_junctionInputTable, "Straight Duct Shape", _junctionStraightShapeCombo);
        AddFittingRow(_junctionInputTable, _junctionStraightDiameterLabel, _junctionStraightDiameterText);
        AddFittingRow(_junctionInputTable, _junctionStraightWidthLabel, _junctionStraightWidthText);
        AddFittingRow(_junctionInputTable, _junctionStraightHeightLabel, _junctionStraightHeightText);
        AddFittingRow(_junctionInputTable, _junctionBranchAirflowLabel, _junctionBranchAirflowText);
        AddFittingRow(_junctionInputTable, "Branch Duct Shape", _junctionBranchShapeCombo);
        AddFittingRow(_junctionInputTable, _junctionBranchDiameterLabel, _junctionBranchDiameterText);
        AddFittingRow(_junctionInputTable, _junctionBranchWidthLabel, _junctionBranchWidthText);
        AddFittingRow(_junctionInputTable, _junctionBranchHeightLabel, _junctionBranchHeightText);
    }

    private void UpdateJunctionGeometryVisibility()
    {
        if (_junctionInputTable.RowStyles.Count < 12)
            return;
        var straightRound = MainForm.Selected(_junctionStraightShapeCombo, DuctShape.Round) == DuctShape.Round;
        _junctionStraightDiameterLabel.Visible = _junctionStraightDiameterText.Visible = straightRound;
        _junctionStraightWidthLabel.Visible = _junctionStraightWidthText.Visible = !straightRound;
        _junctionStraightHeightLabel.Visible = _junctionStraightHeightText.Visible = !straightRound;
        _junctionInputTable.RowStyles[4].Height = straightRound ? 40 : 0;
        _junctionInputTable.RowStyles[5].Height = straightRound ? 0 : 40;
        _junctionInputTable.RowStyles[6].Height = straightRound ? 0 : 40;

        var branchRound = MainForm.Selected(_junctionBranchShapeCombo, DuctShape.Round) == DuctShape.Round;
        _junctionBranchDiameterLabel.Visible = _junctionBranchDiameterText.Visible = branchRound;
        _junctionBranchWidthLabel.Visible = _junctionBranchWidthText.Visible = !branchRound;
        _junctionBranchHeightLabel.Visible = _junctionBranchHeightText.Visible = !branchRound;
        _junctionInputTable.RowStyles[9].Height = branchRound ? 40 : 0;
        _junctionInputTable.RowStyles[10].Height = branchRound ? 0 : 40;
        _junctionInputTable.RowStyles[11].Height = branchRound ? 0 : 40;
    }

    private void ConvertJunctionInputs(bool toMetric)
    {
        if (toMetric)
        {
            MainForm.ConvertText(_junctionStraightAirflowText, UnitConversions.CfmToM3PerS, UnitConversions.M3PerSToLps);
            MainForm.ConvertText(_junctionBranchAirflowText, UnitConversions.CfmToM3PerS, UnitConversions.M3PerSToLps);
            foreach (var input in new[] { _junctionStraightDiameterText, _junctionStraightWidthText, _junctionStraightHeightText,
                         _junctionBranchDiameterText, _junctionBranchWidthText, _junctionBranchHeightText })
                MainForm.ConvertText(input, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
        }
        else
        {
            MainForm.ConvertText(_junctionStraightAirflowText, UnitConversions.LpsToM3PerS, UnitConversions.M3PerSToCfm);
            MainForm.ConvertText(_junctionBranchAirflowText, UnitConversions.LpsToM3PerS, UnitConversions.M3PerSToCfm);
            foreach (var input in new[] { _junctionStraightDiameterText, _junctionStraightWidthText, _junctionStraightHeightText,
                         _junctionBranchDiameterText, _junctionBranchWidthText, _junctionBranchHeightText })
                MainForm.ConvertText(input, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
        }
    }

    private void RebuildFittingParameterInputs()
    {
        if (_fittingTypeCombo.SelectedItem is not FittingDefinition definition)
            return;
        _fittingParameterTable.SuspendLayout();
        _fittingParameterTable.Controls.Clear();
        _fittingParameterTable.RowStyles.Clear();
        _fittingParameterTable.RowCount = 0;
        _fittingParameterInputs.Clear();

        foreach (var spec in definition.Inputs)
        {
            var titledLabel = ToInputTitleCase(spec.Label);
            var labelText = string.IsNullOrWhiteSpace(spec.Unit) ? titledLabel : $"{titledLabel} ({spec.Unit})";
            var label = new Label
            {
                Name = $"FittingInputLabel_{spec.Key}",
                Text = labelText,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                UseMnemonic = false,
                Padding = new Padding(0, 2, 6, 2)
            };
            Control input;
            if (spec.Choices is { Count: > 0 })
            {
                var combo = MainForm.CreateDropDown();
                foreach (var choice in spec.Choices)
                    combo.Items.Add(new MainForm.Choice<double>(choice.Label, choice.Value));
                var selectedIndex = spec.Choices
                    .Select((choice, index) => (choice, index))
                    .OrderBy(item => Math.Abs(item.choice.Value - spec.DefaultValue))
                    .First().index;
                combo.SelectedIndex = selectedIndex;
                MainForm.ConfigureComboDropDown(combo);
                combo.SelectedIndexChanged += (_, _) =>
                {
                    if (string.Equals(spec.Key, "face", StringComparison.OrdinalIgnoreCase))
                        UpdatePriceScdNeckChoices();
                    if (string.Equals(spec.Key, "sizeIndex", StringComparison.OrdinalIgnoreCase))
                        UpdateVavRowChoices();
                    CalculateFittingAndDisplay();
                };
                input = combo;
            }
            else
            {
                var text = MainForm.CreateInput(spec.DefaultValue.ToString("0.####", CultureInfo.CurrentCulture));
                if (string.Equals(spec.Key, "re", StringComparison.OrdinalIgnoreCase))
                {
                    text.ReadOnly = true;
                    text.BackColor = Color.FromArgb(235, 241, 246);
                    label.Text += " (Calculated)";
                }
                text.Leave += (_, _) => CalculateFittingAndDisplay();
                input = text;
            }
            input.Name = $"FittingInput_{spec.Key}";
            input.Tag = spec;
            _fittingParameterInputs[spec.Key] = input;
            AddFittingRow(_fittingParameterTable, label, input, rowHeight: 44);
        }
        _fittingParameterTable.ResumeLayout(true);
        UpdatePriceScdNeckChoices();
        UpdateVavRowChoices();
        ResizeFittingParameterRows();

        var junction = definition.IsJunction;
        _junctionInputTable.Visible = junction;
        _fittingShapeLabel.Text = junction ? "Common Duct Shape" : "Reference Duct Shape";
        ApplyInferredFittingShape(definition);
        UpdateFittingGeometryVisibility();
        UpdateFittingUnitLabels();

        _fittingSourceLabel.Text = $"Reference section: {definition.ReferenceVelocity}. {definition.SourceNote}";
        if (IsPerformanceGrd(definition))
        {
            _fittingAutomationLabel.Text = "Published manufacturer performance table — requested airflow is rounded upward to the next listed cfm value.";
            _fittingAutomationLabel.BackColor = Color.FromArgb(232, 246, 238);
            _fittingAutomationLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        else if (IsPerformanceVav(definition))
        {
            _fittingAutomationLabel.Text = "Published Titus DESV performance table — airside pressure drop is linearly interpolated between adjacent airflow points.";
            _fittingAutomationLabel.BackColor = Color.FromArgb(232, 246, 238);
            _fittingAutomationLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        else if (definition.IsAutomated)
        {
            _fittingAutomationLabel.Text = "Built-in table/equation — C is calculated and interpolated automatically.";
            _fittingAutomationLabel.BackColor = Color.FromArgb(232, 246, 238);
            _fittingAutomationLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        else
        {
            _fittingAutomationLabel.Text = "Reference coefficient required — enter C from the cited ASHRAE table. No coefficient is guessed.";
            _fittingAutomationLabel.BackColor = Color.FromArgb(255, 245, 220);
            _fittingAutomationLabel.ForeColor = Color.FromArgb(130, 82, 12);
        }
    }

    internal void CalculateFittingAndDisplay()
    {
        try
        {
            var definition = _fittingTypeCombo.SelectedItem as FittingDefinition
                             ?? throw new InvalidOperationException("Select a fitting.");
            var airflow = ReadFittingAirflowM3PerS();
            var temperature = ReadFittingTemperatureC();
            var values = new Dictionary<string, double>();
            foreach (var (key, input) in _fittingParameterInputs)
            {
                double value;
                if (input is ComboBox combo && combo.SelectedItem is MainForm.Choice<double> choice)
                    value = choice.Value;
                else if (input is TextBox text && double.TryParse(text.Text,
                             NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var parsed))
                    value = parsed;
                else
                    throw new FormatException($"Enter a valid value for {((FittingInputSpec)input.Tag!).Label}.");
                values[key] = value;
            }

            if (IsPerformanceGrd(definition))
            {
                CalculateGrdPerformanceAndDisplay(definition, values, airflow, temperature);
                return;
            }

            if (IsPerformanceVav(definition))
            {
                CalculateVavPerformanceAndDisplay(definition, values, airflow, temperature);
                return;
            }

            _lastGrdPerformanceSelection = null;
            _lastVavPerformanceSelection = null;
            SetFittingSummaryCaptions(false);
            var geometry = ReadFittingGeometry();

            if (values.ContainsKey("re"))
            {
                var air = AirProperties.Calculate(temperature, MainForm.FixedRelativeHumidity, MainForm.FixedAtmosphericPressurePa);
                var referenceVelocity = airflow / geometry.ActualAreaM2;
                var calculatedReynolds = air.DensityKgPerM3 * referenceVelocity * geometry.HydraulicDiameterM
                                         / air.DynamicViscosityPaS;
                values["re"] = calculatedReynolds;
                if (_fittingParameterInputs.TryGetValue("re", out var reynoldsInput)
                    && reynoldsInput is TextBox reynoldsText)
                    reynoldsText.Text = calculatedReynolds.ToString("0", CultureInfo.CurrentCulture);
            }

            var result = definition.IsJunction
                ? FittingCalculator.Calculate(definition, values, ReadJunctionFlowData(airflow, geometry), temperature,
                    MainForm.FixedRelativeHumidity, MainForm.FixedAtmosphericPressurePa)
                : FittingCalculator.Calculate(definition, values, airflow, geometry, temperature,
                    MainForm.FixedRelativeHumidity, MainForm.FixedAtmosphericPressurePa);
            _lastFittingCalculation = result;
            _fittingCoefficientValue.Text = result.Coefficient.ToString("0.####", CultureInfo.CurrentCulture);
            _fittingVelocityValue.Text = FormatFittingVelocity(result.ReferenceVelocityMPerS);
            _fittingVelocityPressureValue.Text = FormatFittingPressure(result.VelocityPressurePa);
            _fittingPressureLossValue.Text = FormatFittingPressure(result.PressureLossPa);
            UpdateFittingCalculationDetails(result);
            _fittingStatusLabel.Text = definition.IsAutomated
                ? definition.IsJunction
                    ? $"Calculated ASHRAE {definition.Code} for {MainForm.Selected(_junctionPathCombo, JunctionPath.Branch).ToString().ToLowerInvariant()} path using common-duct VP."
                    : $"Calculated ASHRAE {definition.Code} from its built-in table. Area: {FormatFittingArea(geometry.ActualAreaM2)}."
                : $"Calculated pressure loss for {definition.Code} using the entered reference coefficient C.";
            _fittingStatusLabel.ForeColor = definition.IsAutomated ? Color.FromArgb(30, 100, 55) : Color.FromArgb(130, 82, 12);
            if (result.TableRangeWarnings.Count > 0)
            {
                // An input beyond the published table: the coefficient is the table-edge value, not interpolated.
                _fittingStatusLabel.Text = "Outside published table: " + string.Join(" ", result.TableRangeWarnings);
                _fittingStatusLabel.ForeColor = Color.FromArgb(160, 82, 0);
            }
            _addFittingButton.Enabled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            _lastFittingCalculation = null;
            _lastGrdPerformanceSelection = null;
            _lastVavPerformanceSelection = null;
            SetFittingSummaryCaptions(false);
            _fittingCoefficientValue.Text = "—";
            _fittingVelocityValue.Text = "—";
            _fittingVelocityPressureValue.Text = "—";
            _fittingPressureLossValue.Text = "—";
            _fittingCalculationDetails.Text = $"Calculation unavailable\r\n\r\n{ex.Message}\r\n\r\n{DefaultFittingCalculationDetails()}";
            _fittingStatusLabel.Text = ex.Message;
            _fittingStatusLabel.ForeColor = Color.Firebrick;
            _addFittingButton.Enabled = false;
        }
    }

    private static bool IsPerformanceGrd(FittingDefinition definition) =>
        string.Equals(definition.Code, GrdPerformanceCatalog.Titus350RCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(definition.Code, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(definition.Code, GrdPerformanceCatalog.TitusTbd80Code, StringComparison.OrdinalIgnoreCase);

    private static bool IsPerformanceVav(FittingDefinition definition) =>
        string.Equals(definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(definition.Code, VavPerformanceCatalog.WithoutReheatCode, StringComparison.OrdinalIgnoreCase);

    private void CalculateVavPerformanceAndDisplay(FittingDefinition definition,
        IReadOnlyDictionary<string, double> values, double airflowM3PerS, double temperatureC)
    {
        var requestedCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        var withReheat = string.Equals(definition.Code, VavPerformanceCatalog.WithReheatCode,
            StringComparison.OrdinalIgnoreCase);
        var sizes = withReheat ? VavPerformanceCatalog.WithReheatSizes : VavPerformanceCatalog.WithoutReheatSizes;
        var sizeIndex = Convert.ToInt32(values["sizeIndex"]);
        if (sizeIndex < 0 || sizeIndex >= sizes.Count)
            throw new ArgumentOutOfRangeException(nameof(values), "Select a published Titus DESV VAV size.");
        var size = sizes[sizeIndex];
        _lastVavPerformanceSelection = withReheat
            ? VavPerformanceCatalog.SelectWithReheat(requestedCfm, size, Convert.ToInt32(values["rows"]))
            : VavPerformanceCatalog.SelectWithoutReheat(requestedCfm, size);
        _lastGrdPerformanceSelection = null;
        _lastFittingCalculation = VavPerformanceCatalog.CalculateFittingResult(definition, values, airflowM3PerS,
            temperatureC, MainForm.FixedRelativeHumidity, MainForm.FixedAtmosphericPressurePa);

        SetVavSummaryCaptions();
        _fittingCoefficientValue.Text = size == "24x16" ? "24 × 16 in" : $"Size {size}";
        _fittingVelocityValue.Text = $"{requestedCfm:N0} cfm";
        _fittingVelocityPressureValue.Text = withReheat
            ? $"{Convert.ToInt32(values["rows"])}-row coil"
            : "Damper full open";
        _fittingPressureLossValue.Text = $"{_lastVavPerformanceSelection.PressureDropInWg:0.###} in. w.c.";
        UpdateVavPerformanceCalculationDetails(_lastVavPerformanceSelection);
        _fittingStatusLabel.Text = _lastVavPerformanceSelection.WasInterpolated
            ? $"Interpolated between {_lastVavPerformanceSelection.LowerPoint.AirflowCfm:0} and {_lastVavPerformanceSelection.UpperPoint.AirflowCfm:0} cfm published points."
            : $"Matched the published {requestedCfm:0} cfm performance point.";
        _fittingStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        _addFittingButton.Enabled = true;
    }

    private void UpdateVavPerformanceCalculationDetails(VavPerformanceSelection selection)
    {
        var box = _fittingCalculationDetails;
        box.Clear();
        void Heading(string text)
        {
            if (box.TextLength > 0) EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            EngineeringMathRichText.AppendAtEnd(box, text + Environment.NewLine);
            box.SelectionFont = box.Font;
            box.SelectionColor = box.ForeColor;
        }
        void Line(string text, Color? color = null, FontStyle style = FontStyle.Regular)
        {
            box.SelectionColor = color ?? box.ForeColor;
            box.SelectionFont = new Font(box.Font, style);
            EngineeringMathRichText.AppendAtEnd(box, text + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
        }

        Heading("MANUFACTURER PERFORMANCE SELECTION");
        Line(selection.Product, Color.FromArgb(28, 69, 108), FontStyle.Bold);
        Line(selection.Configuration);
        Line(selection.SourceReference, Color.FromArgb(28, 69, 108), FontStyle.Italic);
        Line($"Requested airflow: {selection.RequestedAirflowCfm:0.##} cfm", Color.FromArgb(190, 44, 44), FontStyle.Bold);

        Heading("RECREATED PUBLISHED AIRSIDE PRESSURE TABLE");
        box.SelectionFont = new Font("Consolas", 9F, FontStyle.Bold);
        EngineeringMathRichText.AppendAtEnd(box, "Airflow (cfm)   ΔPs / Min ΔPs (in. w.c.)\r\n");
        box.SelectionFont = new Font("Consolas", 9F);
        foreach (var point in selection.PublishedTable)
        {
            var bound = point == selection.LowerPoint || point == selection.UpperPoint;
            box.SelectionColor = bound ? Color.FromArgb(0, 103, 145) : box.ForeColor;
            box.SelectionFont = new Font("Consolas", 9F, bound ? FontStyle.Bold : FontStyle.Regular);
            EngineeringMathRichText.AppendAtEnd(box, $"{(bound ? ">" : " ")} {point.AirflowCfm,8:0}       {point.PressureDropInWg,8:0.###}\r\n");
        }
        box.SelectionFont = box.Font;
        box.SelectionColor = box.ForeColor;

        Heading("INTERPOLATION AND RESULT");
        if (selection.WasInterpolated)
        {
            Line($"Lower point: {selection.LowerPoint.AirflowCfm:0} cfm at {selection.LowerPoint.PressureDropInWg:0.###} in. w.c.");
            Line($"Upper point: {selection.UpperPoint.AirflowCfm:0} cfm at {selection.UpperPoint.PressureDropInWg:0.###} in. w.c.");
            Line("ΔP = ΔP₁ + [(Q − Q₁)/(Q₂ − Q₁)](ΔP₂ − ΔP₁)");
        }
        else
            Line("The requested airflow exactly matches a published table point.");
        Line($"Selected airside pressure drop: {selection.PressureDropInWg:0.###} in. w.c.",
            Color.FromArgb(0, 103, 145), FontStyle.Bold);
        Heading("BASIS AND ASSUMPTIONS");
        Line(selection.Product.Contains("with Reheat", StringComparison.OrdinalIgnoreCase)
            ? "Airside ΔPs is the hot-water coil air pressure drop for the selected Titus DESV size and coil-row count. No extrapolation is permitted outside the published airflow range."
            : "Min ΔPs is the terminal inlet-to-outlet static pressure drop with the primary damper full open. No extrapolation is permitted outside the published airflow range.");
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    private void UpdatePriceScdNeckChoices()
    {
        if (_fittingTypeCombo.SelectedItem is not FittingDefinition definition
            || !string.Equals(definition.Code, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
            || !_fittingParameterInputs.TryGetValue("face", out var faceInput)
            || faceInput is not ComboBox faceCombo
            || faceCombo.SelectedItem is not MainForm.Choice<double> faceChoice
            || !_fittingParameterInputs.TryGetValue("neck", out var neckInput)
            || neckInput is not ComboBox neckCombo)
            return;

        var previous = neckCombo.SelectedItem is MainForm.Choice<double> selected ? selected.Value : 8;
        var valid = GrdPerformanceCatalog.PriceNeckDiameters(Convert.ToInt32(faceChoice.Value));
        neckCombo.BeginUpdate();
        neckCombo.Items.Clear();
        foreach (var neck in valid)
            neckCombo.Items.Add(new MainForm.Choice<double>($"{neck} in", neck));
        neckCombo.EndUpdate();
        var nearest = valid.Select((value, index) => (value, index))
            .OrderBy(item => Math.Abs(item.value - previous)).First().index;
        neckCombo.SelectedIndex = nearest;
    }

    private void UpdateVavRowChoices()
    {
        if (_fittingTypeCombo.SelectedItem is not FittingDefinition definition
            || !string.Equals(definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
            || !_fittingParameterInputs.TryGetValue("sizeIndex", out var sizeInput)
            || sizeInput is not ComboBox sizeCombo
            || sizeCombo.SelectedItem is not MainForm.Choice<double> sizeChoice
            || !_fittingParameterInputs.TryGetValue("rows", out var rowInput)
            || rowInput is not ComboBox rowCombo)
            return;
        var sizeIndex = Convert.ToInt32(sizeChoice.Value);
        if (sizeIndex < 0 || sizeIndex >= VavPerformanceCatalog.WithReheatSizes.Count)
            return;
        var previous = rowCombo.SelectedItem is MainForm.Choice<double> selected ? Convert.ToInt32(selected.Value) : 3;
        var available = VavPerformanceCatalog.AvailableRows(VavPerformanceCatalog.WithReheatSizes[sizeIndex]);
        if (available.Count == 0)
            return;
        rowCombo.BeginUpdate();
        rowCombo.Items.Clear();
        foreach (var rows in available)
            rowCombo.Items.Add(new MainForm.Choice<double>($"{rows} row{(rows == 1 ? "" : "s")}", rows));
        rowCombo.EndUpdate();
        rowCombo.SelectedIndex = available.Select((value, index) => (value, index))
            .OrderBy(item => Math.Abs(item.value - previous)).First().index;
    }

    private void CalculateGrdPerformanceAndDisplay(FittingDefinition definition,
        IReadOnlyDictionary<string, double> values, double airflowM3PerS, double temperatureC)
    {
        var requestedCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        _lastGrdPerformanceSelection = string.Equals(definition.Code, GrdPerformanceCatalog.Titus350RCode,
            StringComparison.OrdinalIgnoreCase)
            ? GrdPerformanceCatalog.SelectTitus350R(requestedCfm,
                GrdPerformanceCatalog.Titus350RSizes[Convert.ToInt32(values["sizeIndex"])])
            : string.Equals(definition.Code, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
                ? GrdPerformanceCatalog.SelectPriceScd(requestedCfm, Convert.ToInt32(values["face"]), Convert.ToInt32(values["neck"]))
                : GrdPerformanceCatalog.SelectTitusTbd80(requestedCfm, Convert.ToInt32(values["length"]),
                    values["slotWidth"], Convert.ToInt32(values["slotCount"]));
        _lastFittingCalculation = GrdPerformanceCatalog.CalculateFittingResult(definition, values, airflowM3PerS,
            temperatureC, MainForm.FixedRelativeHumidity, MainForm.FixedAtmosphericPressurePa);

        var selected = _lastGrdPerformanceSelection.Selected;
        var grille = string.Equals(definition.Code, GrdPerformanceCatalog.Titus350RCode,
            StringComparison.OrdinalIgnoreCase);
        SetFittingSummaryCaptions(true, grille);
        _fittingCoefficientValue.Text = $"{selected.AirflowCfm:0} cfm";
        _fittingVelocityValue.Text = $"{selected.TotalPressureInWg:0.###} in. w.c.";
        _fittingVelocityPressureValue.Text = $"NC {selected.SoundNc}";
        _fittingPressureLossValue.Text = grille
            ? $"{selected.CoreVelocityFpm:0} fpm"
            : $"{selected.ThrowFt} ft";
        UpdateGrdPerformanceCalculationDetails(_lastGrdPerformanceSelection);
        _fittingStatusLabel.Text = _lastGrdPerformanceSelection.AirflowWasRoundedUp
            ? $"Selected the next published airflow: {requestedCfm:0.##} cfm rounded up to {selected.AirflowCfm:0} cfm."
            : $"Selected the published {selected.AirflowCfm:0} cfm performance point.";
        _fittingStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        _addFittingButton.Enabled = true;
    }

    private void UpdateGrdPerformanceCalculationDetails(GrdPerformanceSelection selection)
    {
        var box = _fittingCalculationDetails;
        box.Clear();
        void Heading(string text)
        {
            if (box.TextLength > 0) EngineeringMathRichText.AppendAtEnd(box, Environment.NewLine);
            box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            box.SelectionColor = Color.FromArgb(28, 69, 108);
            EngineeringMathRichText.AppendAtEnd(box, text + Environment.NewLine);
            box.SelectionFont = box.Font;
            box.SelectionColor = box.ForeColor;
        }
        void Line(string text, Color? color = null, FontStyle style = FontStyle.Regular)
        {
            box.SelectionColor = color ?? box.ForeColor;
            box.SelectionFont = new Font(box.Font, style);
            EngineeringMathRichText.AppendAtEnd(box, text + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.SelectionFont = box.Font;
        }

        Heading("MANUFACTURER PERFORMANCE SELECTION");
        Line(selection.Product, Color.FromArgb(28, 69, 108), FontStyle.Bold);
        Line(selection.Configuration);
        if (!string.IsNullOrWhiteSpace(selection.SourceReference))
            Line(selection.SourceReference, Color.FromArgb(28, 69, 108), FontStyle.Italic);
        Line($"Requested airflow: {selection.RequestedAirflowCfm:0.##} cfm", Color.FromArgb(190, 44, 44), FontStyle.Bold);
        Line(selection.InletDiameterIn is null
            ? "Selection rule: round upward to the first published airflow at or above the request."
            : "Selection rule: select the smallest published inlet whose table covers the requested airflow, then round upward to the first listed cfm value at or above the request.");

        Heading(selection.Product.StartsWith("Titus", StringComparison.OrdinalIgnoreCase)
            ? "TITUS PERFORMANCE CHART / LOOKUP TABLE"
            : "RECREATED PUBLISHED TABLE");
        var grille = !string.IsNullOrWhiteSpace(selection.NominalDuctSizeIn);
        box.SelectionFont = new Font("Consolas", 8.3F, FontStyle.Bold);
        EngineeringMathRichText.AppendAtEnd(box, grille
            ? "Core V   CFM      VP       Neg SP   NC\r\n"
            : "CFM     TP       SP       NC      Throw (ft)\r\n");
        box.SelectionFont = new Font("Consolas", 8.3F);
        foreach (var point in selection.PublishedTable)
        {
            var marker = ReferenceEquals(point, selection.Selected) || point == selection.Selected ? ">" : " ";
            box.SelectionColor = point == selection.Selected ? Color.FromArgb(0, 103, 145) : box.ForeColor;
            box.SelectionFont = new Font("Consolas", 8.3F, point == selection.Selected ? FontStyle.Bold : FontStyle.Regular);
            EngineeringMathRichText.AppendAtEnd(box, grille
                ? $"{marker}{point.CoreVelocityFpm,5:0}   {point.AirflowCfm,6:0}   {point.VelocityPressureInWg,6:0.###}   {point.TotalPressureInWg,6:0.###}   {point.SoundNc,4}\r\n"
                : $"{marker}{point.AirflowCfm,5:0}   {point.TotalPressureInWg,6:0.###}   {(point.StaticPressureInWg ?? 0),6:0.###}   {point.SoundNc,4}    {point.ThrowFt}\r\n");
        }
        box.SelectionFont = box.Font;
        box.SelectionColor = box.ForeColor;

        Heading("SELECTED OUTPUTS");
        Line($"Published airflow: {selection.Selected.AirflowCfm:0} cfm", Color.FromArgb(0, 103, 145), FontStyle.Bold);
        if (selection.InletDiameterIn is int inletDiameter)
            Line($"Automatically selected inlet diameter: {inletDiameter} in", Color.FromArgb(0, 103, 145), FontStyle.Bold);
        if (grille)
        {
            Line($"Nominal duct size: {selection.NominalDuctSizeIn} in", Color.FromArgb(0, 103, 145), FontStyle.Bold);
            Line($"Core velocity: {selection.Selected.CoreVelocityFpm:0} fpm");
            Line($"Velocity pressure: {selection.Selected.VelocityPressureInWg:0.###} in. w.c.");
            Line($"Negative static pressure / pressure drop: {selection.Selected.TotalPressureInWg:0.###} in. w.c.", Color.FromArgb(0, 103, 145), FontStyle.Bold);
        }
        else
            Line($"Total pressure drop: {selection.Selected.TotalPressureInWg:0.###} in. w.c.", Color.FromArgb(0, 103, 145), FontStyle.Bold);
        if (selection.Selected.StaticPressureInWg is double staticPressure)
            Line($"Published static pressure: {staticPressure:0.###} in. w.c.");
        Line($"Sound: NC {selection.Selected.SoundNc}", Color.FromArgb(0, 103, 145), FontStyle.Bold);
        if (!grille)
            Line($"Throw to 150/100/50 fpm terminal velocities: {selection.Selected.ThrowFt} ft", Color.FromArgb(0, 103, 145), FontStyle.Bold);

        Heading("BASIS AND ASSUMPTIONS");
        if (grille)
            Line("Titus 350R / 350F / 350R-SS grille performance data; ASHRAE Standard 70-2006 test basis. Published static pressures are negative; FanWorks reports their positive magnitude as system pressure loss. A dash in the source NC row is reported as NC <10. Performance data courtesy of Titus.");
        else if (selection.Product.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            Line("Price SCD performance data; ASHRAE Standard 70-2006 test basis. A dash in the source table is reported as NC <15. Throw is listed for 150/100/50 fpm terminal velocities.");
        else
            Line("Titus TBD-80 D plenum slot diffuser; ASHRAE Standard 70-2006 test basis. A dash in the source table is reported as NC <10. Throw is the one-way value for 150/100/50 fpm terminal velocities. Performance data courtesy of Titus.");
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    private double ReadFittingAirflowM3PerS()
    {
        var value = MainForm.ReadPositive(_fittingAirflowText, "airflow");
        return _host._currentUnits == UnitSystem.Imperial ? UnitConversions.CfmToM3PerS(value) : UnitConversions.LpsToM3PerS(value);
    }

    private DuctGeometry ReadFittingGeometry()
    {
        var shape = MainForm.Selected(_fittingShapeCombo, DuctShape.Round);
        if (shape == DuctShape.Round)
        {
            var diameter = MainForm.ReadPositive(_fittingDiameterText, "reference duct diameter");
            return GeometryCalculator.Round(_host._currentUnits == UnitSystem.Imperial
                ? UnitConversions.InchesToM(diameter)
                : UnitConversions.MillimetersToM(diameter));
        }
        var width = MainForm.ReadPositive(_fittingWidthText, "reference duct width");
        var height = MainForm.ReadPositive(_fittingHeightText, "reference duct height");
        return GeometryCalculator.Rectangular(
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(width) : UnitConversions.MillimetersToM(width),
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(height) : UnitConversions.MillimetersToM(height));
    }

    private JunctionFlowData ReadJunctionFlowData(double commonAirflow, DuctGeometry commonGeometry) => new(
        MainForm.Selected(_junctionPathCombo, JunctionPath.Branch),
        commonAirflow,
        commonGeometry,
        ReadJunctionAirflow(_junctionStraightAirflowText, "straight/main airflow"),
        ReadJunctionGeometry(_junctionStraightShapeCombo, _junctionStraightDiameterText,
            _junctionStraightWidthText, _junctionStraightHeightText, "straight/main"),
        ReadJunctionAirflow(_junctionBranchAirflowText, "branch airflow"),
        ReadJunctionGeometry(_junctionBranchShapeCombo, _junctionBranchDiameterText,
            _junctionBranchWidthText, _junctionBranchHeightText, "branch"));

    private double ReadJunctionAirflow(TextBox input, string name)
    {
        var value = MainForm.ReadPositive(input, name);
        return _host._currentUnits == UnitSystem.Imperial ? UnitConversions.CfmToM3PerS(value) : UnitConversions.LpsToM3PerS(value);
    }

    private DuctGeometry ReadJunctionGeometry(ComboBox shapeInput, TextBox diameterInput, TextBox widthInput,
        TextBox heightInput, string name)
    {
        if (MainForm.Selected(shapeInput, DuctShape.Round) == DuctShape.Round)
        {
            var diameter = MainForm.ReadPositive(diameterInput, $"{name} duct diameter");
            return GeometryCalculator.Round(_host._currentUnits == UnitSystem.Imperial
                ? UnitConversions.InchesToM(diameter)
                : UnitConversions.MillimetersToM(diameter));
        }
        var width = MainForm.ReadPositive(widthInput, $"{name} duct width");
        var height = MainForm.ReadPositive(heightInput, $"{name} duct height");
        return GeometryCalculator.Rectangular(
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(width) : UnitConversions.MillimetersToM(width),
            _host._currentUnits == UnitSystem.Imperial ? UnitConversions.InchesToM(height) : UnitConversions.MillimetersToM(height));
    }

    private double ReadFittingTemperatureC()
    {
        if (!double.TryParse(_fittingTemperatureText.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out var value))
            throw new FormatException("Enter a valid air temperature.");
        return _host._currentUnits == UnitSystem.Imperial ? UnitConversions.FahrenheitToCelsius(value) : value;
    }

    private string FormatFittingPressure(double pascals) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.PaToInchesWater(pascals):0.####} in. w.c."
        : $"{pascals:0.##} Pa";

    private string FormatFittingVelocity(double metersPerSecond) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MpsToFpm(metersPerSecond):N0} fpm"
        : $"{metersPerSecond:0.##} m/s";

    private string FormatFittingArea(double squareMeters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.SquareMetersToSquareFeet(squareMeters):0.###} ft²"
        : $"{squareMeters:0.####} m²";

    private static string DefaultFittingCalculationDetails() =>
        "CALCULATION METHOD\r\n\r\n" +
        "1. Reference-duct area is calculated from the selected round diameter or rectangular width and height.\r\n\r\n" +
        "2. Velocity is airflow divided by actual duct area. Values and equations are shown in Imperial HVAC units. Velocity pressure uses moist-air density.\r\n\r\n" +
        "3. C is interpolated from the selected ASHRAE fitting table when safely digitized. Pressure loss is ΔPt = C × VP.\r\n\r\n" +
        "Entries marked Reference Coefficient Required accept C from the cited ASHRAE fitting and do not invent a value.";

    private void UpdateFittingCalculationDetails(FittingCalculationResult result)
    {
        var box = _fittingCalculationDetails;
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

        Heading("REQUIRED EQUATIONS");
        MathLine(("A_{ref} = A(geometry)", false));
        MathLine(("D_{h} = \\frac{4A_{ref}}{P_{wetted}}", false));
        MathLine(("V_{ref} = \\frac{Q}{A_{ref}}", false));
        MathLine(("Re = \\frac{\\rho V_{ref}D_{h}}{\\mu}", false));
        MathLine(("C = C_{ASHRAE}(table inputs)", false));
        MathLine(("VP = (\\frac{V_{ref}}{4005})^{2}(\\frac{\\rho}{0.075})", false));
        MathLine(("\\Delta P_{t} = C \\times VP", false));

        Heading("PARAMETERS AND SUB-EQUATIONS");
        DefinitionRow("Q  Reference airflow", "Aref  Reference area");
        DefinitionRow("Vref  Reference velocity", "Dh  Hydraulic diameter");
        DefinitionRow("ρ  Moist-air density", "μ  Dynamic viscosity");
        DefinitionRow("Re  Reynolds number", "C  ASHRAE loss coefficient");
        DefinitionRow("VP  Velocity pressure", "ΔPt  Total-pressure loss");
        MathLine(("\\rho = \\rho_{moist}(T, RH, p_{atm})", false));
        MathLine(("\\mu = \\mu_{Sutherland}(T)", false));

        Heading("ASHRAE SOURCE AND SELECTION");
        Line(($"ASHRAE {result.Definition.Code} — {result.Definition.Description}", false));
        Line((result.Definition.SourceNote, false));
        Line(("Reference velocity basis: ", false), (result.Definition.ReferenceVelocity, true));
        Line(("Selection method: ", false), (result.Definition.IsAutomated
            ? "continuous inputs are linearly interpolated between adjacent published table points; dropdown choices select an exact table family. Two-axis lookups use bilinear interpolation."
            : "the user enters the signed or unsigned coefficient from the cited ASHRAE table; no missing coefficient is estimated.", false));

        Heading("TABLE / EQUATION INPUTS");
        if (result.Definition.Inputs.Count == 0)
        {
            Line(("This fitting has a fixed published coefficient and requires no table-axis input.", false));
        }
        else
        {
            foreach (var spec in result.Definition.Inputs)
            {
                result.Inputs.TryGetValue(spec.Key, out var value);
                var selectedChoice = spec.Choices?.OrderBy(choice => Math.Abs(choice.Value - value)).FirstOrDefault();
                var formattedValue = selectedChoice is not null && Math.Abs(selectedChoice.Value - value) < 1e-9
                    ? $"{selectedChoice.Label} ({value:0.####})"
                    : $"{value:0.####}{(string.IsNullOrWhiteSpace(spec.Unit) ? string.Empty : " " + spec.Unit)}";
                Line(($"{spec.Label} [{spec.Key}] = ", false), (formattedValue, true),
                    ($"   published range: {spec.Minimum:0.####} to {spec.Maximum:0.####}", false));
                if (!string.IsNullOrWhiteSpace(spec.Help))
                    Line(($"  {spec.Help}", false));
            }
        }

        Heading("FLOW AND GEOMETRY");
        if (result.AirflowM3PerS is double airflow)
        {
            var formattedFlow = $"{UnitConversions.M3PerSToCfm(airflow):0.##} cfm";
            MathLine(("Q = ", false), (formattedFlow, true));
        }
        if (result.ReferenceGeometry is { } geometry)
        {
            var dimensions = geometry.Shape == DuctShape.Round
                ? $"Diameter {UnitConversions.MToInches(geometry.DiameterM):0.###} in"
                : $"{UnitConversions.MToInches(geometry.WidthM):0.###} × {UnitConversions.MToInches(geometry.HeightM):0.###} in";
            Line(("Reference duct = ", false), ($"{geometry.Shape}, {dimensions}", true));
            StyledMathLine(("A_{ref} = A(", EngineeringMathRichText.MathTone.Formula),
                (dimensions, EngineeringMathRichText.MathTone.Input),
                (") = ", EngineeringMathRichText.MathTone.Formula),
                ($"{UnitConversions.SquareMetersToSquareFeet(geometry.ActualAreaM2):0.####} ft²", EngineeringMathRichText.MathTone.Solved));
            StyledMathLine(("D_{h} = D_{h}(", EngineeringMathRichText.MathTone.Formula),
                (dimensions, EngineeringMathRichText.MathTone.Input),
                (") = ", EngineeringMathRichText.MathTone.Formula),
                ($"{UnitConversions.MToInches(geometry.HydraulicDiameterM):0.###} in", EngineeringMathRichText.MathTone.Solved));
        }
        var referenceAreaFt2 = result.ReferenceGeometry is null
            ? 0
            : UnitConversions.SquareMetersToSquareFeet(result.ReferenceGeometry.ActualAreaM2);
        var referenceFlowCfm = result.AirflowM3PerS is double referenceFlow
            ? UnitConversions.M3PerSToCfm(referenceFlow)
            : 0;
        StyledMathLine(("V_{ref} = \\frac{Q}{A_{ref}} = ", EngineeringMathRichText.MathTone.Formula),
            ($"\\frac{{{referenceFlowCfm:0.##} cfm}}{{{referenceAreaFt2:0.####} ft²}}", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS):0.#} fpm", EngineeringMathRichText.MathTone.Solved));

        Heading("INTERMEDIATE CALCULATIONS");
        var densityLbmFt3 = result.Air.DensityKgPerM3 * 0.0624279606;
        var viscosityLbmFtS = result.Air.DynamicViscosityPaS * 0.671968975;
        if (result.ReferenceGeometry is { } referenceGeometry)
        {
            var velocityFtS = UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS) / 60.0;
            var hydraulicDiameterFt = UnitConversions.MToFeet(referenceGeometry.HydraulicDiameterM);
            var reynolds = result.Air.DensityKgPerM3 * result.ReferenceVelocityMPerS
                           * referenceGeometry.HydraulicDiameterM / result.Air.DynamicViscosityPaS;
            if (referenceGeometry.Shape == DuctShape.Rectangular)
            {
                StyledMathLine(("\\frac{W}{H} = ", EngineeringMathRichText.MathTone.Formula),
                    ($"\\frac{{{UnitConversions.MToInches(referenceGeometry.WidthM):0.###}}}{{{UnitConversions.MToInches(referenceGeometry.HeightM):0.###}}}", EngineeringMathRichText.MathTone.Input),
                    (" = ", EngineeringMathRichText.MathTone.Formula),
                    ($"{referenceGeometry.WidthM / referenceGeometry.HeightM:0.####}", EngineeringMathRichText.MathTone.Solved));
                StyledMathLine(("\\frac{H}{W} = ", EngineeringMathRichText.MathTone.Formula),
                    ($"{referenceGeometry.HeightM / referenceGeometry.WidthM:0.####}", EngineeringMathRichText.MathTone.Solved));
            }
            StyledMathLine(("Re = \\frac{\\rho V D_{h}}{\\mu} = ", EngineeringMathRichText.MathTone.Formula),
                ($"\\frac{{{densityLbmFt3:0.#####} \\times {velocityFtS:0.####} \\times {hydraulicDiameterFt:0.####}}}{{{viscosityLbmFtS:0.########}}}", EngineeringMathRichText.MathTone.Input),
                (" = ", EngineeringMathRichText.MathTone.Formula),
                ($"{reynolds:0}", EngineeringMathRichText.MathTone.Solved));
        }
        var publishedInputKeys = result.Definition.Inputs.Select(input => input.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var derived in result.Inputs.Where(pair => !publishedInputKeys.Contains(pair.Key)).OrderBy(pair => pair.Key))
            StyledMathLine(($"{FittingIntermediateLabel(derived.Key)} = ", EngineeringMathRichText.MathTone.Formula),
                ($"{derived.Value:0.####}", EngineeringMathRichText.MathTone.Solved));

        Heading("COEFFICIENT AND PRESSURE LOSS");
        StyledMathLine(("C = C_{ASHRAE}(", EngineeringMathRichText.MathTone.Formula),
            ("table inputs", EngineeringMathRichText.MathTone.Input),
            (") = ", EngineeringMathRichText.MathTone.Formula),
            ($"{result.Coefficient:0.####}", EngineeringMathRichText.MathTone.Solved));
        var velocityFpm = UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS);
        StyledMathLine(("VP = (\\frac{V}{4005})^{2}(\\frac{\\rho}{0.075}) = ", EngineeringMathRichText.MathTone.Formula),
            ($"(\\frac{{{velocityFpm:0.#}}}{{4005}})^{{2}}(\\frac{{{densityLbmFt3:0.#####}}}{{0.075}})", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{UnitConversions.PaToInchesWater(result.VelocityPressurePa):0.####} in. w.c.", EngineeringMathRichText.MathTone.Solved));
        StyledMathLine(("\\Delta P_{t} = C \\times VP = ", EngineeringMathRichText.MathTone.Formula),
            ($"{result.Coefficient:0.####} \\times {UnitConversions.PaToInchesWater(result.VelocityPressurePa):0.####} in. w.c.", EngineeringMathRichText.MathTone.Input),
            (" = ", EngineeringMathRichText.MathTone.Formula),
            ($"{UnitConversions.PaToInchesWater(result.PressureLossPa):0.####} in. w.c.", EngineeringMathRichText.MathTone.Solved));

        Heading("AIR AND MATERIAL");
        var temperature = $"{UnitConversions.CelsiusToFahrenheit(result.Air.TemperatureC):0.##} °F";
        Line(("Air temperature = ", false), (temperature, true), ("; RH = ", false), ($"{result.Air.RelativeHumidity:P0}", true));
        Line(("Air density = ", false), ($"{densityLbmFt3:0.#####} lbm/ft³", false));
        Line(("Dynamic viscosity = ", false), ($"{viscosityLbmFtS:0.########} lbm/(ft·s)", false));
        Line(("Duct material/roughness is not used in the ASHRAE fitting-loss coefficient calculation.", false));

        Heading("ASSUMPTIONS");
        Line(("Standard sea-level pressure (14.696 psia); steady flow; reference velocity and C basis follow the selected ASHRAE fitting entry. Interpolation is restricted to the digitized published limits.", false));
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    internal static string FittingIntermediateLabel(string key) => key.ToLowerInvariant() switch
    {
        "qbqc" => "Branch/common airflow ratio Qb/Qc",
        "qsqc" => "Straight/common airflow ratio Qs/Qc",
        "qbqs" => "Branch/straight airflow ratio Qb/Qs",
        "abac" => "Branch/common area ratio Ab/Ac",
        "asac" => "Straight/common area ratio As/Ac",
        "abas" => "Branch/straight area ratio Ab/As",
        "vbvc" => "Branch/common velocity ratio Vb/Vc",
        "vsvc" => "Straight/common velocity ratio Vs/Vc",
        "vbvs" => "Branch/straight velocity ratio Vb/Vs",
        "vcfpm" => "Common-duct velocity (fpm)",
        "re" => "Reynolds number",
        _ => key
    };

    private string FormatFittingDimension(double meters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToInches(meters):0.###} in"
        : $"{UnitConversions.MToMillimeters(meters):0.##} mm";

    private void UpdateFittingGeometryVisibility()
    {
        var performanceLookup = _fittingTypeCombo.SelectedItem is FittingDefinition definition
                                && (IsPerformanceGrd(definition) || IsPerformanceVav(definition));
        _fittingTemperatureLabel.Visible = _fittingTemperatureText.Visible = !performanceLookup;
        _fittingShapeLabel.Visible = _fittingShapeCombo.Visible = !performanceLookup;
        var round = MainForm.Selected(_fittingShapeCombo, DuctShape.Round) == DuctShape.Round;
        _fittingDiameterLabel.Visible = _fittingDiameterText.Visible = !performanceLookup && round;
        _fittingWidthLabel.Visible = _fittingWidthText.Visible = !performanceLookup && !round;
        _fittingHeightLabel.Visible = _fittingHeightText.Visible = !performanceLookup && !round;
        _fittingRectangularGeometryPanel.Visible = !performanceLookup && !round;
    }

    private void ApplyInferredFittingShape(FittingDefinition definition)
    {
        if (definition.ReferenceDuctShape is DuctShape inferredShape)
        {
            DuctSystemScreen.SelectChoice(_fittingShapeCombo, inferredShape);
            EnsureValidInferredGeometry(inferredShape);
        }
        _fittingShapeCombo.Enabled = definition.ReferenceDuctShape is null;
    }

    private void EnsureValidInferredGeometry(DuctShape shape)
    {
        static bool IsPositive(TextBox input) => double.TryParse(input.Text,
            NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value) && value > 0;

        if (shape == DuctShape.Round)
        {
            if (!IsPositive(_fittingDiameterText))
                _fittingDiameterText.Text = _host._currentUnits == UnitSystem.Imperial ? "12" : "300";
            return;
        }

        if (!IsPositive(_fittingWidthText))
            _fittingWidthText.Text = _host._currentUnits == UnitSystem.Imperial ? "24" : "600";
        if (!IsPositive(_fittingHeightText))
            _fittingHeightText.Text = _host._currentUnits == UnitSystem.Imperial ? "12" : "300";
    }

    private void ResizeFittingParameterRows()
    {
        if (_resizingFittingParameterRows || _fittingParameterTable.IsDisposed)
            return;
        _resizingFittingParameterRows = true;
        try
        {
            _fittingParameterTable.SuspendLayout();
            for (var row = 0; row < _fittingParameterTable.RowCount; row++)
            {
                if (_fittingParameterTable.GetControlFromPosition(0, row) is not Label label
                    || row >= _fittingParameterTable.RowStyles.Count)
                    continue;
                var textWidth = Math.Max(80, label.ClientSize.Width - label.Padding.Horizontal - 4);
                var measured = TextRenderer.MeasureText(
                    label.Text,
                    label.Font,
                    new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                var rowHeight = Math.Max(40, measured.Height + label.Padding.Vertical + 8);
                var style = _fittingParameterTable.RowStyles[row];
                style.SizeType = SizeType.Absolute;
                style.Height = rowHeight;
                if (_fittingParameterTable.GetControlFromPosition(1, row) is { } input)
                {
                    var verticalMargin = Math.Max(3, (rowHeight - 32) / 2);
                    input.Margin = new Padding(0, verticalMargin, 0, verticalMargin);
                }
            }
        }
        finally
        {
            _fittingParameterTable.ResumeLayout(true);
            _resizingFittingParameterRows = false;
        }
    }

    internal void UpdateFittingUnitLabels()
    {
        if (_fittingAirflowLabel.IsDisposed)
            return;
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        _fittingAirflowLabel.Text = imperial ? "Airflow (cfm)" : "Airflow (L/s)";
        if (_fittingTypeCombo.SelectedItem is FittingDefinition definition && definition.IsJunction)
            _fittingAirflowLabel.Text = imperial ? "Common Airflow (cfm)" : "Common Airflow (L/s)";
        _fittingDiameterLabel.Text = imperial ? "Diameter (in)" : "Diameter (mm)";
        _fittingWidthLabel.Text = imperial ? "Width (in)" : "Width (mm)";
        _fittingHeightLabel.Text = imperial ? "Height (in)" : "Height (mm)";
        _fittingTemperatureLabel.Text = imperial ? "Air Temperature (°F)" : "Air Temperature (°C)";
        _junctionStraightAirflowLabel.Text = imperial ? "Straight Airflow (cfm)" : "Straight Airflow (L/s)";
        _junctionBranchAirflowLabel.Text = imperial ? "Branch Airflow (cfm)" : "Branch Airflow (L/s)";
        _junctionStraightDiameterLabel.Text = imperial ? "Straight Diameter (in)" : "Straight Diameter (mm)";
        _junctionStraightWidthLabel.Text = imperial ? "Straight Width (in)" : "Straight Width (mm)";
        _junctionStraightHeightLabel.Text = imperial ? "Straight Height (in)" : "Straight Height (mm)";
        _junctionBranchDiameterLabel.Text = imperial ? "Branch Diameter (in)" : "Branch Diameter (mm)";
        _junctionBranchWidthLabel.Text = imperial ? "Branch Width (in)" : "Branch Width (mm)";
        _junctionBranchHeightLabel.Text = imperial ? "Branch Height (in)" : "Branch Height (mm)";
        if (_lastGrdPerformanceSelection is not null)
        {
            var selected = _lastGrdPerformanceSelection.Selected;
            var grille = !string.IsNullOrWhiteSpace(_lastGrdPerformanceSelection.NominalDuctSizeIn);
            SetFittingSummaryCaptions(true, grille);
            _fittingCoefficientValue.Text = $"{selected.AirflowCfm:0} cfm";
            _fittingVelocityValue.Text = $"{selected.TotalPressureInWg:0.###} in. w.c.";
            _fittingVelocityPressureValue.Text = $"NC {selected.SoundNc}";
            _fittingPressureLossValue.Text = grille ? $"{selected.CoreVelocityFpm:0} fpm" : $"{selected.ThrowFt} ft";
            UpdateGrdPerformanceCalculationDetails(_lastGrdPerformanceSelection);
            return;
        }
        if (_lastVavPerformanceSelection is not null)
        {
            SetVavSummaryCaptions();
            var withReheat = _lastVavPerformanceSelection.Product.Contains("with Reheat", StringComparison.OrdinalIgnoreCase);
            var sizeText = _lastVavPerformanceSelection.Configuration.Split(';')[0].Replace("Size ", "", StringComparison.OrdinalIgnoreCase);
            _fittingCoefficientValue.Text = sizeText.Contains('x') ? sizeText.Replace("x", " × ") + " in" : "Size " + sizeText;
            _fittingVelocityValue.Text = $"{_lastVavPerformanceSelection.RequestedAirflowCfm:N0} cfm";
            _fittingVelocityPressureValue.Text = withReheat
                ? _lastVavPerformanceSelection.Configuration.Split(';').ElementAtOrDefault(1)?.Trim() ?? "Reheat coil"
                : "Damper full open";
            _fittingPressureLossValue.Text = $"{_lastVavPerformanceSelection.PressureDropInWg:0.###} in. w.c.";
            UpdateVavPerformanceCalculationDetails(_lastVavPerformanceSelection);
            return;
        }
        if (_lastFittingCalculation is not null)
        {
            _fittingVelocityValue.Text = FormatFittingVelocity(_lastFittingCalculation.ReferenceVelocityMPerS);
            _fittingVelocityPressureValue.Text = FormatFittingPressure(_lastFittingCalculation.VelocityPressurePa);
            _fittingPressureLossValue.Text = FormatFittingPressure(_lastFittingCalculation.PressureLossPa);
            UpdateFittingCalculationDetails(_lastFittingCalculation);
        }
    }

    internal void ConvertFittingUnitInputs(UnitSystem from, UnitSystem to)
    {
        if (from == to || _fittingAirflowText.IsDisposed)
            return;
        if (from == UnitSystem.Imperial)
        {
            MainForm.ConvertText(_fittingAirflowText, UnitConversions.CfmToM3PerS, UnitConversions.M3PerSToLps);
            MainForm.ConvertText(_fittingDiameterText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
            MainForm.ConvertText(_fittingWidthText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
            MainForm.ConvertText(_fittingHeightText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
            MainForm.ConvertText(_fittingTemperatureText, UnitConversions.FahrenheitToCelsius);
            ConvertJunctionInputs(true);
        }
        else
        {
            MainForm.ConvertText(_fittingAirflowText, UnitConversions.LpsToM3PerS, UnitConversions.M3PerSToCfm);
            MainForm.ConvertText(_fittingDiameterText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
            MainForm.ConvertText(_fittingWidthText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
            MainForm.ConvertText(_fittingHeightText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
            MainForm.ConvertText(_fittingTemperatureText, UnitConversions.CelsiusToFahrenheit);
            ConvertJunctionInputs(false);
        }
    }

    private void AddCurrentFittingToSystem()
    {
        var returnToDesigner = _host.LayoutScreen.PendingLayoutNode(LayoutNodeType.Fitting) is not null;
        var historyState = returnToDesigner ? _host.LayoutScreen.CaptureLayoutHistoryState() : null;
        CalculateFittingAndDisplay();
        if (_lastFittingCalculation is null)
            return;
        var existing = _host.DuctSystemScreen._editingFittingSequence is int editingSequence
            ? _host.DuctSystemScreen._ductSystem.Fittings.FirstOrDefault(x => x.Sequence == editingSequence)
            : null;
        var existingNode = existing is null ? null : _host.DuctSystemScreen._ductSystem.LayoutNodes
            .FirstOrDefault(x => x.LinkedFittingSequence == existing.Sequence);
        if (existing is not null && !_host.DuctSystemScreen.ConfirmAirflowOverride(existingNode,
                _lastFittingCalculation.AirflowM3PerS ?? existing.AirflowM3PerS, existing.Id))
            return;
        var sequence = existing?.Sequence ?? _host.DuctSystemScreen._ductSystem.ReserveFittingSequence();
        var description = existing?.Description ?? _fittingDescription.Trim();
        if (string.IsNullOrWhiteSpace(description))
            description = $"{_lastFittingCalculation.Definition.Code} — {_lastFittingCalculation.Definition.Description}";
        var fitting = SavedFitting.FromResult(sequence, description, _lastFittingCalculation,
            existing?.AirSystemType ?? _host.DuctSystemScreen._ductSystem.DefaultAirSystemType,
            existing?.BranchId ?? _host.DuctSystemScreen._ductSystem.DefaultBranchId);
        fitting.CustomId = existing?.CustomId ?? string.Empty;
        fitting.DeltaXM = existing?.DeltaXM ?? 0;
        fitting.DeltaYM = existing?.DeltaYM ?? 0;
        fitting.DeltaZM = existing?.DeltaZM ?? 0;
        _host.LayoutScreen.ApplyLayoutMetadata(fitting, _lastFittingCalculation, allowBranchReservation: existing is null);
        if (existing is null)
            _host.DuctSystemScreen._ductSystem.Fittings.Add(fitting);
        else
            _host.DuctSystemScreen._ductSystem.Fittings[_host.DuctSystemScreen._ductSystem.Fittings.IndexOf(existing)] = fitting;
        _host.DuctSystemScreen._editingFittingSequence = null;
        _addFittingButton.Text = DuctSystemScreen.DefaultFittingActionText;
        _addFittingButton.Visible = false;
        _fittingEditingLabel.Visible = false;
        if (historyState is not null)
            _host.LayoutScreen.CommitLayoutHistoryState(historyState);
        _host.DuctSystemScreen.SaveDuctSystem();
        _host.DuctSystemScreen.RefreshDuctSystemGrid();
        _host.DuctSystemScreen._mainTabs.SelectedTab = returnToDesigner ? _host.DuctSystemScreen._layoutTab : _host.DuctSystemScreen._systemTab;
        if (returnToDesigner)
            _host.LayoutScreen._layoutStatusLabel.Text = existing is null
                ? "Fitting configured and linked. Continue placing components on the grid."
                : $"Updated {fitting.Id} and refreshed its designer component.";
        _host.DuctSystemScreen._systemStorageStatusLabel.Text = existing is null
            ? $"Added fitting F-{sequence:000} and saved the duct system locally."
            : $"Updated fitting {fitting.Id} and saved the duct system locally.";
        _host.DuctSystemScreen._systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    internal void LoadFittingIntoCalculator(SavedFitting fitting)
    {
        var definition = FittingCatalog.Find(fitting.FittingCode);
        SelectFittingGroup(FittingCatalog.SelectionGroup(definition), definition.Code);
        DuctSystemScreen.SelectChoice(_fittingShapeCombo, definition.ReferenceDuctShape ?? fitting.Shape);
        _fittingAirflowText.Text = (_host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.M3PerSToCfm(fitting.AirflowM3PerS)
            : UnitConversions.M3PerSToLps(fitting.AirflowM3PerS)).ToString("0.####", CultureInfo.CurrentCulture);
        _fittingTemperatureText.Text = (_host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.CelsiusToFahrenheit(fitting.TemperatureC)
            : fitting.TemperatureC).ToString("0.####", CultureInfo.CurrentCulture);
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            _fittingDiameterText.Text = UnitConversions.MToInches(fitting.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
            _fittingWidthText.Text = UnitConversions.MToInches(fitting.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
            _fittingHeightText.Text = UnitConversions.MToInches(fitting.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
        }
        else
        {
            _fittingDiameterText.Text = UnitConversions.MToMillimeters(fitting.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
            _fittingWidthText.Text = UnitConversions.MToMillimeters(fitting.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
            _fittingHeightText.Text = UnitConversions.MToMillimeters(fitting.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
        }

        foreach (var (key, value) in fitting.Inputs)
        {
            if (!_fittingParameterInputs.TryGetValue(key, out var input))
                continue;
            if (input is TextBox text)
                text.Text = value.ToString("0.####", CultureInfo.CurrentCulture);
            else if (input is ComboBox combo)
            {
                var match = combo.Items.Cast<object>()
                    .Select((item, index) => (item, index))
                    .Where(x => x.item is MainForm.Choice<double>)
                    .OrderBy(x => Math.Abs(((MainForm.Choice<double>)x.item).Value - value))
                    .FirstOrDefault();
                if (match.item is not null)
                    combo.SelectedIndex = match.index;
            }
        }

        if (fitting.Junction is { } junction)
        {
            DuctSystemScreen.SelectChoice(_junctionPathCombo, junction.Path);
            LoadJunctionPath(_junctionStraightShapeCombo, _junctionStraightAirflowText,
                _junctionStraightDiameterText, _junctionStraightWidthText, _junctionStraightHeightText,
                junction.StraightAirflowM3PerS, junction.StraightGeometry);
            LoadJunctionPath(_junctionBranchShapeCombo, _junctionBranchAirflowText,
                _junctionBranchDiameterText, _junctionBranchWidthText, _junctionBranchHeightText,
                junction.BranchAirflowM3PerS, junction.BranchGeometry);
        }
        UpdateFittingGeometryVisibility();
        UpdateJunctionGeometryVisibility();
        CalculateFittingAndDisplay();
    }

    private void LoadJunctionPath(ComboBox shape, TextBox airflow, TextBox diameter, TextBox width, TextBox height,
        double airflowM3PerS, DuctGeometry geometry)
    {
        DuctSystemScreen.SelectChoice(shape, geometry.Shape);
        airflow.Text = (_host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.M3PerSToCfm(airflowM3PerS)
            : UnitConversions.M3PerSToLps(airflowM3PerS)).ToString("0.####", CultureInfo.CurrentCulture);
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            diameter.Text = UnitConversions.MToInches(geometry.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
            width.Text = UnitConversions.MToInches(geometry.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
            height.Text = UnitConversions.MToInches(geometry.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
        }
        else
        {
            diameter.Text = UnitConversions.MToMillimeters(geometry.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
            width.Text = UnitConversions.MToMillimeters(geometry.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
            height.Text = UnitConversions.MToMillimeters(geometry.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
        }
    }
}
