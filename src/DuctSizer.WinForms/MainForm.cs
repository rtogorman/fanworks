using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

public sealed partial class MainForm : DpiScaledForm
{
    private DuctCalculatorScreen? _calculatorScreen;
    internal DuctCalculatorScreen CalculatorScreen => _calculatorScreen ??= new DuctCalculatorScreen(this);

    private LayoutDesignerScreen? _layoutScreen;
    internal LayoutDesignerScreen LayoutScreen => _layoutScreen ??= new LayoutDesignerScreen(this);

    protected override bool UsesDialogStyle => false;

    protected override bool ProcessCmdKey(ref Message message, Keys keyData) =>
        LayoutScreen.TryHandleCommandKey(keyData) || base.ProcessCmdKey(ref message, keyData);

    /// <summary>
    /// The window opens at its designed 1200 x 1200 size. On a screen whose working area (the
    /// desktop minus the taskbar) is smaller, it opens at the working-area size instead, centred,
    /// so no part of it starts off-screen or under the taskbar.
    /// </summary>
    internal static Size OpeningSize(Size designed, Rectangle workingArea) => new(
        Math.Min(designed.Width, workingArea.Width),
        Math.Min(designed.Height, workingArea.Height));

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // The opening calculation ran before the window had its final (display-scaled) layout, when the worksheet
        // box could still have no usable size and its text formatting was lost. Show it again now.
        CalculatorScreen.CalculateAndDisplay();
        var workingArea = Screen.FromControl(this).WorkingArea;
        var fitted = OpeningSize(Size, workingArea);
        // A design tool wants the whole screen: open maximized whenever the designed window does not fit
        // (interface checks keep the plain window so its size is predictable).
        var fillsScreen = fitted != Size || Height >= workingArea.Height || Width >= workingArea.Width;
        if (fillsScreen && !AutomationOptions.SuppressDialogs)
        {
            WindowState = FormWindowState.Maximized;
            return;
        }
        if (fitted != Size)
        {
            Size = fitted;
            Location = new Point(
                workingArea.Left + (workingArea.Width - fitted.Width) / 2,
                workingArea.Top + (workingArea.Height - fitted.Height) / 2);
        }
    }

    private MainRibbon? _ribbon;
    internal MainRibbon Ribbon => _ribbon ??= new MainRibbon(this);

    private DuctSystemScreen? _ductSystemScreen;
    internal DuctSystemScreen DuctSystemScreen => _ductSystemScreen ??= new DuctSystemScreen(this);

    private FittingCalculatorScreen? _fittingScreen;
    internal FittingCalculatorScreen FittingScreen => _fittingScreen ??= new FittingCalculatorScreen(this);

    internal const double FixedRelativeHumidity = 0.50;
    internal const double FixedAtmosphericPressurePa = 101_325.0;

    internal readonly ComboBox _unitsCombo = CreateDropDown();
    internal static readonly string[] AirSystemTypes =
        { "Supply Air", "Return Air", "Exhaust Air", "Outdoor Air", "Relief Air", "Other" };

    internal readonly TableLayoutPanel _contentLayout = new();
    internal readonly Label _statusLabel = new();
    internal UnitSystem _currentUnits = UnitSystem.Imperial;
    internal bool _updatingControls;
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
        AutoScaleMode = AutoScaleMode.None; // scaled by DpiScaledForm (see DpiScaling)
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
        ConfigureCalculatorSplitter(CalculatorScreen._ductCalculatorSplitter, "DuctCalculatorSplitter");
        CalculatorScreen._ductCalculatorSplitter.SplitterMoved += (_, _) =>
        {
            if (CalculatorScreen._updatingDuctCalculatorSplitter || CalculatorScreen._ductCalculatorSplitter.ClientSize.Width <= CalculatorScreen._ductCalculatorSplitter.SplitterWidth)
                return;
            CalculatorScreen._ductCalculatorSplitRatio = Math.Clamp(
                CalculatorScreen._ductCalculatorSplitter.SplitterDistance
                / (double)(CalculatorScreen._ductCalculatorSplitter.ClientSize.Width - CalculatorScreen._ductCalculatorSplitter.SplitterWidth), .25, .75);
            if (!CalculatorScreen._ductCalculationsExpanded)
                CalculatorScreen._ductCalculatorRestoreRatio = CalculatorScreen._ductCalculatorSplitRatio;
            CalculatorScreen.UpdateDuctCalculatorColumnStyles();
        };
        CalculatorScreen._ductCalculatorSplitter.Resize += (_, _) => CalculatorScreen.ApplyDuctCalculatorSplitterRatio();
        CalculatorScreen._shapeCombo.Name = "ShapeCombo";
        CalculatorScreen._modeCombo.Name = "ModeCombo";
        CalculatorScreen._materialCombo.Name = "MaterialCombo";
        CalculatorScreen._equivalentMethodCombo.Name = "EquivalentMethodCombo";
        CalculatorScreen._pressureBasisCombo.Name = "PressureBasisCombo";
        CalculatorScreen._rectangularFixedDimensionCombo.Name = "RectangularSizingDimensionCombo";
        CalculatorScreen._rectangularFixedDimensionText.Name = "RectangularSizingDimensionText";
        CalculatorScreen._rectangularFixedDimensionUnitLabel.Name = "RectangularSizingDimensionUnitLabel";
        CalculatorScreen._sizeSummaryValue.Font = new Font("Segoe UI Semibold", 8.8F);
        CalculatorScreen._velocitySummaryValue.Font = new Font("Segoe UI Semibold", 8.8F);
        CalculatorScreen._pressureSummaryValue.Font = new Font("Segoe UI Semibold", 8.3F);

        BuildChoices();
        BuildLayout();
        WireEvents();
        DuctSystemScreen.LoadDuctSystem();
        CalculatorScreen.UpdateUnitsLabels();
        CalculatorScreen.UpdateControlState();
        CalculatorScreen.ApplyResponsiveLayout();
        CalculatorScreen.CalculateAndDisplay();
    }

    private void BuildChoices()
    {
        _unitsCombo.Name = "UnitsCombo";
        _unitsCombo.Items.AddRange(new object[]
        {
            new Choice<UnitSystem>("Imperial", UnitSystem.Imperial),
            new Choice<UnitSystem>("Metric", UnitSystem.Metric)
        });
        CalculatorScreen._modeCombo.Items.AddRange(new object[]
        {
            new Choice<CalculationMode>("Check Existing", CalculationMode.CheckEnteredSize),
            new Choice<CalculationMode>("Autosize (SP Loss)", CalculationMode.SizeByTargetFriction),
            new Choice<CalculationMode>("Autosize (Velocity)", CalculationMode.SizeByMaximumVelocity)
        });
        CalculatorScreen._shapeCombo.Items.AddRange(new object[]
        {
            new Choice<DuctShape>("Round", DuctShape.Round),
            new Choice<DuctShape>("Rectangular", DuctShape.Rectangular)
        });
        foreach (var material in DuctMaterialCatalog.All
                     .OrderBy(item => string.Equals(item.Name, "Legacy Reference",
                         StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            CalculatorScreen._materialCombo.Items.Add(material);
        CalculatorScreen._equivalentMethodCombo.Items.AddRange(new object[]
        {
            new Choice<EquivalentDiameterMethod>("Huebscher", EquivalentDiameterMethod.Huebscher),
            new Choice<EquivalentDiameterMethod>("Hydraulic Diameter", EquivalentDiameterMethod.HydraulicDiameter)
        });
        CalculatorScreen._pressureBasisCombo.Items.AddRange(new object[]
        {
            new Choice<PressureLossBasis>("Equivalent Round Area", PressureLossBasis.EquivalentRoundArea),
            new Choice<PressureLossBasis>("Actual Duct Area", PressureLossBasis.ActualDuctArea)
        });
        CalculatorScreen._rectangularFixedDimensionCombo.Items.AddRange(new object[]
        {
            new Choice<RectangularFixedDimension>("Width", RectangularFixedDimension.Width),
            new Choice<RectangularFixedDimension>("Height", RectangularFixedDimension.Height)
        });
        _unitsCombo.SelectedIndex = 0;
        CalculatorScreen._modeCombo.SelectedIndex = 1;
        CalculatorScreen._shapeCombo.SelectedIndex = 0;
        CalculatorScreen._materialCombo.SelectedItem = CalculatorScreen._materialCombo.Items.Cast<DuctMaterial>()
            .Single(item => string.Equals(item.Name, "Legacy Reference", StringComparison.OrdinalIgnoreCase));
        CalculatorScreen._equivalentMethodCombo.SelectedIndex = 0;
        CalculatorScreen._pressureBasisCombo.SelectedIndex = 0;
        CalculatorScreen._rectangularFixedDimensionCombo.SelectedIndex = 0;
        foreach (var combo in new[]
                 {
                     _unitsCombo, CalculatorScreen._modeCombo, CalculatorScreen._shapeCombo, CalculatorScreen._materialCombo, CalculatorScreen._equivalentMethodCombo,
                     CalculatorScreen._pressureBasisCombo, CalculatorScreen._rectangularFixedDimensionCombo
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
        // A slim title band: the main tabs and ribbon already say where you are (v0.96).
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
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
            Size = new Size(360, 30),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 13F),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(20, 2),
            BackColor = Color.Transparent
        };
        var subtitle = new Label
        {
            Name = "CalculatorPageSubtitle",
            Text = "HVAC duct sizing • ASHRAE fitting calculations • system pressure loss",
            AutoSize = false,
            Size = new Size(780, 18),
            ForeColor = Color.FromArgb(215, 229, 242),
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(22, 32),
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
            Location = new Point(Width - 103, 13)
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

        CalculatorScreen._inputCard.Dock = DockStyle.Fill;
        CalculatorScreen._inputCard.Name = "InputCard";
        CalculatorScreen._inputCard.BackColor = Color.White;
        CalculatorScreen._inputCard.CornerRadius = 12;
        CalculatorScreen._inputCard.BorderColor = UiPalette.CardBorder;
        CalculatorScreen._inputCard.Margin = new Padding(0, 0, 7, 0);

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

        CalculatorScreen._ductEditingLabel.Name = "DuctEditingLabel";
        CalculatorScreen._ductEditingLabel.AutoSize = true;
        CalculatorScreen._ductEditingLabel.Dock = DockStyle.Top;
        CalculatorScreen._ductEditingLabel.BackColor = Color.FromArgb(232, 244, 253);
        CalculatorScreen._ductEditingLabel.ForeColor = Color.FromArgb(11, 31, 59);
        CalculatorScreen._ductEditingLabel.Font = new Font("Segoe UI Semibold", 9F);
        CalculatorScreen._ductEditingLabel.Padding = new Padding(10, 7, 10, 7);
        CalculatorScreen._ductEditingLabel.Margin = new Padding(0, 0, 0, 4);
        CalculatorScreen._ductEditingLabel.Visible = false;
        var editingRow = inputTable.RowCount++;
        inputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inputTable.Controls.Add(CalculatorScreen._ductEditingLabel, 0, editingRow);
        inputTable.SetColumnSpan(CalculatorScreen._ductEditingLabel, inputTable.ColumnCount);

        CalculatorScreen.AddInputSection(inputTable, "calculation", "CALCULATION");
        CalculatorScreen.AddInputRow(inputTable, "mode", "Calculation", CalculatorScreen._modeCombo);
        CalculatorScreen.AddInputRow(inputTable, "airflow", "Airflow (cfm)", CalculatorScreen._airflowText);
        CalculatorScreen.AddInputRow(inputTable, "temperature", "Air Temp (°F)", CalculatorScreen._temperatureText);
        CalculatorScreen.AddInputRow(inputTable, "length", "Length (ft)", CalculatorScreen._lengthText);
        CalculatorScreen.AddInputRow(inputTable, "friction", "Target Friction", CalculatorScreen._targetFrictionText);
        CalculatorScreen.AddInputRow(inputTable, "velocity", "Maximum Velocity", CalculatorScreen._maximumVelocityText);

        CalculatorScreen.AddInputSection(inputTable, "geometry", "DUCT GEOMETRY");
        CalculatorScreen.AddInputRow(inputTable, "shape", "Duct Shape", CalculatorScreen._shapeCombo);
        CalculatorScreen.AddInputPairRow(inputTable,
            ("diameter", "Diameter", CreateUnitInputHost(CalculatorScreen._diameterText, CalculatorScreen._diameterUnitLabel)),
            ("width", "Width", CreateUnitInputHost(CalculatorScreen._widthText, CalculatorScreen._widthUnitLabel)));
        CalculatorScreen.AddInputPairRow(inputTable,
            ("height", "Height", CreateUnitInputHost(CalculatorScreen._heightText, CalculatorScreen._heightUnitLabel)));
        CalculatorScreen.AddRectangularSizingDimensionRow(inputTable);

        CalculatorScreen.AddInputSection(inputTable, "methods", "METHODS AND MATERIAL");
        CalculatorScreen.AddInputRow(inputTable, "material", "Duct Material", CalculatorScreen._materialCombo);
        AddHintRow(inputTable, CalculatorScreen._methodHintLabel);

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
            BackColor = Color.FromArgb(14, 111, 230),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F),
            FlatStyle = FlatStyle.Flat
        };
        calculateButton.FlatAppearance.BorderSize = 0;
        calculateButton.Margin = Padding.Empty;
        UiPolish.RoundButton(calculateButton);
        calculateButton.Click += (_, _) => CalculatorScreen.CalculateAndDisplay();
        DuctSystemScreen._addDuctButton.Name = "AddDuctButton";
        DuctSystemScreen._addDuctButton.Text = DuctSystemScreen.DefaultDuctActionText;
        DuctSystemScreen._addDuctButton.Size = new Size(175, 34);
        DuctSystemScreen._addDuctButton.Margin = Padding.Empty;
        DuctSystemScreen._addDuctButton.BackColor = Color.FromArgb(14, 111, 230);
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

        CalculatorScreen._resultsCard.Dock = DockStyle.Fill;
        CalculatorScreen._resultsCard.Name = "ResultsCard";
        CalculatorScreen._resultsCard.BackColor = Color.White;
        CalculatorScreen._resultsCard.CornerRadius = 12;
        CalculatorScreen._resultsCard.BorderColor = UiPalette.CardBorder;
        CalculatorScreen._resultsCard.Margin = new Padding(7, 0, 0, 0);
        var resultsHeader = CreateCardHeader("CALCULATIONS", "Geometry, flow, pressure loss, air, material, and assumptions.");
        AddCalculationPaneControls(inputHeader, resultsHeader,
            CalculatorScreen._ductCalculationsShowButton, CalculatorScreen._ductCalculationsHideButton, CalculatorScreen._ductCalculationsExpandButton,
            () =>
            {
                CalculatorScreen._ductCalculationsCollapsed = false;
                CalculatorScreen.ApplyResponsiveLayout(force: true);
            },
            () =>
            {
                CalculatorScreen._ductCalculationsCollapsed = true;
                CalculatorScreen.ApplyResponsiveLayout(force: true);
            },
            () =>
            {
                if (CalculatorScreen._ductCalculationsExpanded)
                {
                    CalculatorScreen._ductCalculationsExpanded = false;
                    CalculatorScreen._ductCalculatorSplitRatio = CalculatorScreen._ductCalculatorRestoreRatio;
                }
                else
                {
                    CalculatorScreen._ductCalculatorRestoreRatio = CalculatorScreen._ductCalculatorSplitRatio;
                    CalculatorScreen._ductCalculationsExpanded = true;
                    CalculatorScreen._ductCalculatorSplitRatio = .34;
                }
                CalculatorScreen._ductCalculationsExpandButton.Text = CalculatorScreen._ductCalculationsExpanded ? "Restore" : "Expand";
                CalculatorScreen.ApplyResponsiveLayout(force: true);
            });

        CalculatorScreen._summaryTable.Name = "DuctSummaryTable";
        CalculatorScreen._summaryTable.Dock = DockStyle.Fill;
        CalculatorScreen._summaryTable.Height = 116;
        CalculatorScreen._summaryTable.Padding = new Padding(8, 5, 8, 5);
        CalculatorScreen._summaryTable.ColumnCount = 3;
        CalculatorScreen._summaryTable.RowCount = 1;
        CalculatorScreen._summaryTable.BackColor = Color.White;
        CalculatorScreen._summaryTable.ColumnCount = 1;
        CalculatorScreen._summaryTable.RowCount = 3;
        CalculatorScreen._summaryTable.ColumnStyles.Clear();
        CalculatorScreen._summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        CalculatorScreen._summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        CalculatorScreen._summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        CalculatorScreen._summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));
        CalculatorScreen._summaryTable.Controls.Add(CreateSummaryCard("DUCT SIZE", CalculatorScreen._sizeSummaryValue), 0, 0);
        CalculatorScreen._summaryTable.Controls.Add(CreateSummaryCard("ACTUAL VELOCITY", CalculatorScreen._velocitySummaryValue), 0, 1);
        CalculatorScreen._summaryTable.Controls.Add(CreateSummaryCard("PRESSURE RATE", CalculatorScreen._pressureSummaryValue), 0, 2);

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
            CreateOutputSectionLabel("DuctOutputsHeader"), CalculatorScreen._summaryTable, buttonPanel, 92, 64);
        CalculatorScreen._inputCard.Controls.Add(inputBody);
        CalculatorScreen._inputCard.Controls.Add(inputHeader);

        CalculatorScreen._results.Dock = DockStyle.Fill;
        CalculatorScreen._results.View = View.Details;
        CalculatorScreen._results.FullRowSelect = true;
        CalculatorScreen._results.GridLines = false;
        CalculatorScreen._results.BorderStyle = BorderStyle.None;
        CalculatorScreen._results.BackColor = Color.White;
        CalculatorScreen._results.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        CalculatorScreen._results.ShowGroups = true;
        CalculatorScreen._results.Columns.Add("Result", 260);
        CalculatorScreen._results.Columns.Add("Value", 390);
        CalculatorScreen._results.Groups.AddRange(new[] { CalculatorScreen._geometryGroup, CalculatorScreen._flowGroup, CalculatorScreen._pressureGroup, CalculatorScreen._propertiesGroup });
        CalculatorScreen._results.Resize += (_, _) =>
        {
            if (CalculatorScreen._results.ClientSize.Width > 200)
            {
                CalculatorScreen._results.Columns[0].Width = Math.Min(270, CalculatorScreen._results.ClientSize.Width / 2);
                CalculatorScreen._results.Columns[1].Width = Math.Max(180, CalculatorScreen._results.ClientSize.Width - CalculatorScreen._results.Columns[0].Width - 5);
            }
        };
        CalculatorScreen._results.Visible = false;

        CalculatorScreen._ductCalculationDetails.Name = "DuctCalculationDetails";
        CalculatorScreen._ductCalculationDetails.Dock = DockStyle.Fill;
        CalculatorScreen._ductCalculationDetails.ReadOnly = true;
        CalculatorScreen._ductCalculationDetails.BorderStyle = BorderStyle.None;
        CalculatorScreen._ductCalculationDetails.BackColor = Color.White;
        CalculatorScreen._ductCalculationDetails.ForeColor = Color.FromArgb(54, 70, 86);
        CalculatorScreen._ductCalculationDetails.Font = new Font("Segoe UI", 9.5F);
        CalculatorScreen._ductCalculationDetails.ScrollBars = RichTextBoxScrollBars.Vertical;
        CalculatorScreen._ductCalculationDetails.DetectUrls = false;
        CalculatorScreen._ductCalculationDetails.Text = "Select inputs and calculate to display the complete equation worksheet.";
        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 7, 12, 0),
            BackColor = Color.White
        };
        detailsHost.Controls.Add(CalculatorScreen._ductCalculationDetails);

        CalculatorScreen._assumptionsText.Dock = DockStyle.Fill;
        CalculatorScreen._assumptionsText.Multiline = true;
        CalculatorScreen._assumptionsText.ReadOnly = true;
        CalculatorScreen._assumptionsText.ScrollBars = ScrollBars.None;
        CalculatorScreen._assumptionsText.BackColor = Color.FromArgb(248, 250, 252);
        CalculatorScreen._assumptionsText.BorderStyle = BorderStyle.None;
        CalculatorScreen._assumptionsText.Margin = new Padding(8, 0, 8, 8);
        CalculatorScreen._assumptionsText.Text =
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
        assumptionsPanel.Controls.Add(CalculatorScreen._assumptionsText, 0, 1);

        CalculatorScreen._resultsCard.Controls.Add(detailsHost);
        CalculatorScreen._resultsCard.Controls.Add(assumptionsPanel);
        CalculatorScreen._resultsCard.Controls.Add(resultsHeader);

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
        Resize += (_, _) => CalculatorScreen.ApplyResponsiveLayout();
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
        CalculatorScreen._shapeCombo.SelectedIndexChanged += (_, _) =>
        {
            CalculatorScreen.UpdateControlState();
            CalculatorScreen.CalculateAndDisplay();
        };
        CalculatorScreen._modeCombo.SelectedIndexChanged += (_, _) =>
        {
            CalculatorScreen.UpdateControlState();
            CalculatorScreen.CalculateAndDisplay();
        };
        CalculatorScreen._materialCombo.SelectedIndexChanged += (_, _) =>
        {
            CalculatorScreen.ApplyMaterialShapeConstraint();
            CalculatorScreen.UpdateControlState();
            CalculatorScreen.UpdateMaterialStatus();
            CalculatorScreen.CalculateAndDisplay();
        };
        CalculatorScreen._equivalentMethodCombo.SelectedIndexChanged += (_, _) => CalculatorScreen.CalculateAndDisplay();
        CalculatorScreen._pressureBasisCombo.SelectedIndexChanged += (_, _) => CalculatorScreen.CalculateAndDisplay();
        CalculatorScreen._rectangularFixedDimensionCombo.SelectedIndexChanged += (_, _) => CalculatorScreen.CalculateAndDisplay();
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
                ConvertText(CalculatorScreen._airflowText, UnitConversions.CfmToM3PerS, value => UnitConversions.M3PerSToLps(value));
                ConvertText(CalculatorScreen._temperatureText, UnitConversions.FahrenheitToCelsius);
                ConvertText(CalculatorScreen._lengthText, UnitConversions.FeetToM);
                ConvertText(CalculatorScreen._diameterText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(CalculatorScreen._widthText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(CalculatorScreen._heightText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(CalculatorScreen._rectangularFixedDimensionText,
                    value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
                ConvertText(CalculatorScreen._targetFrictionText, UnitConversions.InchesWaterPer100FeetToPaPerM);
                ConvertText(CalculatorScreen._maximumVelocityText, UnitConversions.FpmToMps);
                ConvertText(CalculatorScreen._sizeIncrementText, value => UnitConversions.MToMillimeters(UnitConversions.InchesToM(value)));
            }
            else
            {
                ConvertText(CalculatorScreen._airflowText, UnitConversions.LpsToM3PerS, UnitConversions.M3PerSToCfm);
                ConvertText(CalculatorScreen._temperatureText, UnitConversions.CelsiusToFahrenheit);
                ConvertText(CalculatorScreen._lengthText, UnitConversions.MToFeet);
                ConvertText(CalculatorScreen._diameterText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(CalculatorScreen._widthText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(CalculatorScreen._heightText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(CalculatorScreen._rectangularFixedDimensionText,
                    value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
                ConvertText(CalculatorScreen._targetFrictionText, UnitConversions.PaPerMToInchesWaterPer100Feet);
                ConvertText(CalculatorScreen._maximumVelocityText, UnitConversions.MpsToFpm);
                ConvertText(CalculatorScreen._sizeIncrementText, value => UnitConversions.MToInches(UnitConversions.MillimetersToM(value)));
            }

            _currentUnits = selected;
            CalculatorScreen.UpdateUnitsLabels();
            FittingScreen.UpdateFittingUnitLabels();
        }
        finally
        {
            _updatingControls = false;
        }

        CalculatorScreen.CalculateAndDisplay();
        FittingScreen.CalculateFittingAndDisplay();
        DuctSystemScreen.RefreshDuctSystemGrid();
    }

    internal static DuctShape? RequiredShapeForMaterial(DuctMaterial material)
    {
        if (material.Name.Equals("Galvanized Spiral Duct", StringComparison.OrdinalIgnoreCase))
            return DuctShape.Round;
        if (material.Name.Equals("Galvanized Rectangular Duct", StringComparison.OrdinalIgnoreCase)
            || material.Name.Equals("Internally Fiber-Lined Rectangular Duct", StringComparison.OrdinalIgnoreCase))
            return DuctShape.Rectangular;
        return null;
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
        ForeColor = Color.FromArgb(11, 31, 59),
        Font = new Font("Segoe UI Semibold", 12F),
        Padding = Padding.Empty
    };

    internal static Label CreateInputUnitLabel() => new()
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

    internal static void ConfigureCompactInputControl(Control control, int comboWidth, int textWidth)
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

    internal static double ReadFinite(TextBox box, string name)
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

    internal static double ReadNonNegative(TextBox box, string name)
    {
        var value = ReadFinite(box, name);
        if (value < 0.0)
            throw new ArgumentOutOfRangeException(name, $"{name} cannot be negative.");
        return value;
    }

    internal static bool GeometryMatches(DuctGeometry first, DuctGeometry second)
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
