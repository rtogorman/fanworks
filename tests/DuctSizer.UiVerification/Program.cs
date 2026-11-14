using DuctSizer.Core;
using DuctSizer.WinForms;
using System.Reflection;
using System.Text;

namespace DuctSizer.UiVerification;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "DuctSizer-ui-tests", Guid.NewGuid().ToString("N"));
        var systemPath = Path.Combine(testDirectory, "duct-system.json");
        AutomationOptions.SystemStoragePath = systemPath;
        AutomationOptions.AcceptDefaultCoordinates = true;
        AutomationOptions.SuppressDialogs = true;
        ApplicationConfiguration.Initialize();
        try
        {
            using var form = new MainForm();
            form.CreateControl();
            form.Show();
            Application.DoEvents();
            if (Environment.GetEnvironmentVariable("FANWORKS_DUMP_LAYOUT") is { Length: > 0 })
            {
                foreach (var name in new[] { "LayoutCanvasArea", "LayoutCanvasHost", "LayoutDesignerCanvas", "LayoutWorkspaceSplitter" })
                {
                    var control = form.Controls.Find(name, true).FirstOrDefault();
                    Console.WriteLine($"DUMP {name}: bounds={control?.Bounds} client={control?.ClientSize} visible={control?.Visible} dock={control?.Dock}");
                }
                Console.WriteLine($"DUMP dpi={form.DeviceDpi}");
                return 0;
            }
            var openingArea = Screen.FromControl(form).WorkingArea;
            // The designed 1200 x 1200 window is scaled to the display (its frame is not), then fitted to the screen.
            var designedOpening = DpiScaling.Scale(new Size(1200, 1200));
            bool OpeningFits(int actual, int designed, int available) =>
                actual == available || (actual <= available && Math.Abs(actual - designed) <= Math.Max(2, designed / 40));
            Require(OpeningFits(form.Width, designedOpening.Width, openingArea.Width)
                    && OpeningFits(form.Height, designedOpening.Height, openingArea.Height)
                    && openingArea.Contains(form.Bounds),
                $"The application should open at 1200 x 1200, or fitted to a smaller working area ({openingArea.Width} x {openingArea.Height}); it opened at {form.Width} x {form.Height}.");
            Require(MainForm.OpeningSize(new Size(1200, 1200), new Rectangle(0, 0, 1366, 728)) == new Size(1200, 728)
                    && MainForm.OpeningSize(new Size(1200, 1200), new Rectangle(0, 0, 2560, 1400)) == new Size(1200, 1200),
                "The opening size should keep the designed size when it fits and shrink only the dimension that does not.");
            var openingCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_OPENING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(openingCapturePath))
            {
                CaptureForm(form, openingCapturePath);
            }

            // Report-only run (Linux review aid): write the report of the loaded project and stop.
            if (Environment.GetEnvironmentVariable("FANWORKS_GALLERY_SHOT") is { Length: > 0 } galleryShot)
            {
                // Quick capture of the fitting gallery as opened from a fitting's editor (fan/plenum fitting selected).
                var galleryDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.FittingGalleryDialog")!;
                var code = FittingCatalog.All.First(definition => FittingCatalog.SelectionGroup(definition) == FittingCatalog.FanExitsAndPlenumsSelectionGroup).Code;
                using var gallery = (Form)Activator.CreateInstance(galleryDialogType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object?[] { FittingCatalog.All, code }, null)!;
                gallery.StartPosition = FormStartPosition.Manual;
                gallery.Location = new Point(0, 0);
                gallery.Show(form);
                for (var pump = 0; pump < 40; pump++)
                {
                    Application.DoEvents();
                    Thread.Sleep(25);
                }
                using var shot = new Bitmap(gallery.Width, gallery.Height);
                using (var shotGraphics = Graphics.FromImage(shot))
                    shotGraphics.CopyFromScreen(gallery.Location, Point.Empty, gallery.Size);
                shot.Save(galleryShot, System.Drawing.Imaging.ImageFormat.Png);
                var firstPicture = gallery.Controls.Find("FittingGalleryPanel", true)[0].Controls[0].Controls[0].Controls
                    .OfType<PictureBox>().First();
                Console.WriteLine($"Gallery {gallery.Size}; first tile {gallery.Controls.Find("FittingGalleryPanel", true)[0].Controls[0].Size}; picture {firstPicture.Size} image {firstPicture.Image?.Size}");
                gallery.Close();
                return 0;
            }
            if (Environment.GetEnvironmentVariable("FANWORKS_PREVIEW_ONLY") == "1")
            {
                // Quick check of the Reports tab preview alone (timing and status).
                if (Environment.GetEnvironmentVariable("FANWORKS_REPORT_PROJECT") is { Length: > 0 } previewProject)
                    form.DuctSystemScreen.ReplaceCurrentSystem(DuctSystemStore.Load(previewProject));
                var previewClock = System.Diagnostics.Stopwatch.StartNew();
                ClickButton(Find<Button>(form, "TopTabReports"));
                var preview = Find<Control>(form, "ReportsPage");
                Console.WriteLine($"Tab opened (UI blocked) for {previewClock.ElapsedMilliseconds} ms.");
                var ready = (bool)preview.GetType().GetMethod("WaitUntilReady", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(preview, new object[] { 60_000 })!;
                Console.WriteLine($"Ready={ready} after {previewClock.ElapsedMilliseconds} ms. Status: {Find<Label>(form, "ReportPreviewStatus").Text}");
                return ready ? 0 : 1;
            }
            var reportOnlyPath = Environment.GetEnvironmentVariable("FANWORKS_REPORT_ONLY");
            if (!string.IsNullOrWhiteSpace(reportOnlyPath))
            {
                if (Environment.GetEnvironmentVariable("FANWORKS_REPORT_PROJECT") is { Length: > 0 } reportProject)
                    form.DuctSystemScreen.ReplaceCurrentSystem(DuctSystemStore.Load(reportProject));
                var reportClock = System.Diagnostics.Stopwatch.StartNew();
                form.DuctSystemScreen.WriteReportPdf(reportOnlyPath);
                Console.WriteLine($"Report written in {reportClock.ElapsedMilliseconds} ms.");
                if (Environment.GetEnvironmentVariable("FANWORKS_REPORT_TIMING") == "1")
                {
                    reportClock.Restart();
                    form.DuctSystemScreen.WriteReportPdf(reportOnlyPath);
                    Console.WriteLine($"Report written again in {reportClock.ElapsedMilliseconds} ms.");
                }
                return 0;
            }

            var content = Find<TableLayoutPanel>(form, "ContentLayout");
            var ductCalculatorSplitter = Find<SplitContainer>(form, "DuctCalculatorSplitter");
            var fittingCalculatorSplitter = Find<SplitContainer>(form, "FittingCalculatorSplitter");
            var inputCard = Find<Panel>(form, "InputCard");
            var calculateButton = Find<Button>(form, "CalculateButton");
            var shape = Find<ComboBox>(form, "ShapeCombo");
            var mode = Find<ComboBox>(form, "ModeCombo");
            var material = Find<ComboBox>(form, "MaterialCombo");
            var equivalent = PrivateField<ComboBox>(form, "_equivalentMethodCombo");
            var pressureBasis = PrivateField<ComboBox>(form, "_pressureBasisCombo");
            var airflowInput = PrivateField<TextBox>(form, "_airflowText");
            var temperatureInput = PrivateField<TextBox>(form, "_temperatureText");
            var lengthInput = PrivateField<TextBox>(form, "_lengthText");
            var targetFrictionInput = PrivateField<TextBox>(form, "_targetFrictionText");
            var maximumVelocityInput = PrivateField<TextBox>(form, "_maximumVelocityText");
            var rectangularSizingDimension = Find<ComboBox>(form, "RectangularSizingDimensionCombo");
            var rectangularSizingValue = Find<TextBox>(form, "RectangularSizingDimensionText");
            var rectangularSizingUnit = Find<Label>(form, "RectangularSizingDimensionUnitLabel");
            var tabs = Find<TabControl>(form, "MainTabs");
            // Navigation (v0.96): top tabs Project / Analysis / Reports / Quick Calc; the Quick Calc ribbon chooses the
            // duct or fitting calculator. ClickButton invokes a button even when its ribbon page is not showing.
            var calculatorNavigation = Find<Button>(form, "RibbonDuctCalculatorButton");
            var fittingNavigation = Find<Button>(form, "RibbonFittingCalculatorButton");
            var systemNavigation = Find<Button>(form, "TopTabAnalysis");
            var layoutNavigation = Find<Button>(form, "TopTabProject");
            var layoutCanvasHost = Find<Panel>(form, "LayoutCanvasHost");
            var saveSystem = Find<Button>(form, "RibbonSaveButton");
            var settingsButton = Find<Button>(form, "RibbonProjectSettingsButton");
            var ribbon = Find<Control>(form, "MainRibbon");
            var ribbonComponentTools = new[] { "RibbonSpacesGroup", "RibbonEquipmentGroup", "RibbonFittingsGroup",
                    "RibbonTerminalsGroup", "RibbonPointsGroup" }
                .SelectMany(name => Descendants<Control>(Find<Control>(form, name))
                    .Where(control => control.GetType().Name == "LayoutToolCard"))
                .ToArray();
            var layoutToolTips = PrivateField<ToolTip>(form, "_layoutToolTips");
            var layoutSummaryGrid = Find<DataGridView>(form, "LayoutSystemSummaryGrid");
            var layoutSummarySplitter = Find<SplitContainer>(form, "LayoutWorkspaceSplitter");
            var layoutSummaryCollapse = Find<Button>(form, "LayoutSummaryCollapseButton");
            var layoutSummaryReopen = Find<Button>(form, "LayoutSummaryReopenButton");
            var layoutCanvas = Find<Control>(form, "LayoutDesignerCanvas");
            var zoomValue = Find<Label>(form, "LayoutZoomValue");
            var layoutScale = Find<ComboBox>(form, "LayoutScaleSelector");
            var layoutPdfBackground = Find<Button>(form, "LayoutPdfBackgroundButton");
            var layoutSourceSinkEditor = Find<Button>(form, "LayoutSourceSinkEditorButton");
            var layoutAutoSize = Find<Button>(form, "RibbonAutoSizeButton");
            var layoutCriticalPath = Find<Button>(form, "RibbonHighlightCriticalPathButton");
            var layoutViewMode = Find<ComboBox>(form, "LayoutViewModeSelector");
            var layoutUndo = Find<Button>(form, "LayoutUndoButton");
            var layoutRedo = Find<Button>(form, "LayoutRedoButton");
            var layoutDelete = Find<Button>(form, "LayoutDeleteButton");
            var layoutRearrange = Find<Button>(form, "RibbonMoveButton");
            var layoutLabels = Find<CheckBox>(form, "LayoutShowLabelsCheck");
            var layoutDuctSizes = Find<CheckBox>(form, "LayoutShowSizesCheck");
            var layoutFlow = Find<CheckBox>(form, "LayoutShowFlowCheck");
            var layoutCenter = Find<Button>(form, "LayoutCenterButton");
            var sourceSpaceTool = Find<Control>(form, "LayoutToolSpaceSource");
            var sinkSpaceTool = Find<Control>(form, "LayoutToolSpaceSink");
            var calculationPointTools = new[]
            {
                Find<Control>(form, "LayoutToolPressure"),
                Find<Control>(form, "LayoutToolTemperature"),
                Find<Control>(form, "LayoutToolVelocity"),
                Find<Control>(form, "LayoutToolAirflow")
            };
            var fanTool = Find<Control>(form, "LayoutToolFan");
            var coilTool = Find<Control>(form, "LayoutToolCoil");
            var filterTool = Find<Control>(form, "LayoutToolFilter");
            var customComponentTool = Find<Control>(form, "LayoutToolCustomComponent");
            var fittingTools = new[]
            {
                Find<Control>(form, "LayoutToolElbows"),
                Find<Control>(form, "LayoutToolTransitions"),
                Find<Control>(form, "LayoutToolDivertingFittings"),
                Find<Control>(form, "LayoutToolConvergingFittings"),
                Find<Control>(form, "LayoutToolDampers"),
                Find<Control>(form, "LayoutToolVAVs"),
                Find<Control>(form, "LayoutToolTerminalsTakeoffs"),
                Find<Control>(form, "LayoutToolGRDs"),
                Find<Control>(form, "LayoutToolFanExitsAndPlenums")
            };
            var grid = Find<DataGridView>(form, "DuctSystemGrid");
            var gridContextMenu = grid.ContextMenuStrip
                                  ?? throw new InvalidOperationException("The Duct System grid needs a context menu.");
            var editSystemItem = gridContextMenu.Items.Find("DuctGridEditItemMenuItem", true)
                .OfType<ToolStripMenuItem>().Single();
            var removeSystemItem = gridContextMenu.Items.Find("DuctGridRemoveItemMenuItem", true)
                .OfType<ToolStripMenuItem>().Single();
            var total = Find<Label>(form, "SystemTotalLabel");
            var addDuct = Find<Button>(form, "AddDuctButton");
            var fittingType = Find<ComboBox>(form, "FittingTypeCombo");
            var fittingGroup = Find<ComboBox>(form, "FittingGroupCombo");
            var fittingShape = Find<ComboBox>(form, "FittingShapeCombo");
            var fittingAirflow = Find<TextBox>(form, "FittingAirflowText");
            var fittingDiameter = Find<TextBox>(form, "FittingDiameterText");
            var fittingWidth = Find<TextBox>(form, "FittingWidthText");
            var fittingHeight = Find<TextBox>(form, "FittingHeightText");
            var fittingTemperature = Find<TextBox>(form, "FittingTemperatureText");
            var fittingTemperatureLabel = Find<Label>(form, "FittingTemperatureLabel");
            var fittingAutomation = Find<Label>(form, "FittingAutomationLabel");
            var fittingSource = PrivateField<Label>(form, "_fittingSourceLabel");
            var fittingInputScroll = Find<Panel>(form, "FittingInputScroll");
            var ductInputScroll = Find<Panel>(form, "DuctInputScroll");
            var ductInputTable = Find<TableLayoutPanel>(form, "DuctInputTable");
            var ductSummary = Find<TableLayoutPanel>(form, "DuctSummaryTable");
            var fittingSummary = Find<TableLayoutPanel>(form, "FittingSummaryTable");
            var fittingCalculationDetails = Find<RichTextBox>(form, "FittingCalculationDetails");
            var ductCalculationDetails = Find<RichTextBox>(form, "DuctCalculationDetails");
            var fittingCalculationSourceImage = Find<PictureBox>(form, "FittingCalculationSourceImage");
            var ductOutputsHeader = Find<Label>(form, "DuctOutputsHeader");
            var fittingOutputsHeader = Find<Label>(form, "FittingOutputsHeader");
            var assumptionsText = PrivateField<TextBox>(form, "_assumptionsText");
            var projectHeader = Find<Label>(form, "ProjectHeaderLabel");
            var projectHeaderBrand = Find<Control>(form, "ProjectHeaderBrand");
            var projectHeaderAppIcon = Find<PictureBox>(form, "ProjectHeaderAppIcon");
            var projectIdentityHeader = Find<Control>(form, "ProjectIdentityHeader");
            var projectHeaderLogo = Find<PictureBox>(form, "ProjectHeaderLogo");
            var resultsCard = Find<Control>(form, "ResultsCard");
            var fittingOutputCard = Find<Control>(form, "FittingOutputCard");
            var fittingPreview = Find<PictureBox>(form, "FittingPreviewPictureBox");
            var fittingPreviewCaption = Find<Label>(form, "FittingPreviewCaption");
            var fittingSelectorPanel = Find<TableLayoutPanel>(form, "FittingSelectorPanel");
            var fittingBaseInputs = Find<TableLayoutPanel>(form, "FittingBaseInputTable");
            var browseFittingImages = Find<Button>(form, "BrowseFittingImagesButton");
            var airTemperatureLabel = Find<Label>(form, "InputLabel_temperature");
            var calculatorTitle = Find<Label>(form, "CalculatorPageTitle");
            var calculatorSubtitle = Find<Label>(form, "CalculatorPageSubtitle");
            var fittingShapeLabel = Find<Label>(form, "FittingShapeLabel");
            var fittingVelocity = Find<Label>(form, "FittingVelocityValue");
            var addFitting = Find<Button>(form, "AddFittingButton");
            var calculateFitting = Find<Button>(form, "CalculateFittingButton");
            var junctionInputs = Find<TableLayoutPanel>(form, "JunctionInputTable");
            var junctionPath = Find<ComboBox>(form, "JunctionPathCombo");
            var deleteAllSystem = Find<Button>(form, "DeleteAllSystemButton");
            var autoSizeFan = Find<Button>(form, "RibbonCalculateButton");
            var viewFanInformation = Find<Button>(form, "RibbonFanInformationButton");
            var criticalPathFilter = Find<Button>(form, "CriticalPathFilterButton");
            Require(mode.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(new[]
                { "Check Existing", "Autosize (SP Loss)", "Autosize (Velocity)" }),
                "The calculation dropdown should use the requested concise labels.");
            Require(mode.SelectedIndex == 1,
                "New duct calculations should default to Autosize (SP Loss).");
            Require(tabs.SelectedTab?.Name == "LayoutDesignerTab"
                    && ((dynamic)Find<Button>(form, "TopTabProject")).Selected
                    && Find<Control>(form, "RibbonProjectPage").Visible,
                "FanWorks should open on the Project tab (Layout Designer) with the Project ribbon showing.");
            // Round X: Project (file commands + Project Settings) | Auto Size (criteria + Auto Size) | … | Calculate.
            Require(settingsButton.Text.Replace("\n", " ", StringComparison.Ordinal) == "Project Settings"
                    && settingsButton.Parent?.Parent?.Name == "RibbonProjectGroup"
                    && Find<Button>(form, "RibbonSizingCriteriaButton").Parent?.Parent?.Name == "RibbonAutoSizeGroup"
                    && Find<Button>(form, "RibbonAutoSizeButton").Parent?.Parent?.Name == "RibbonAutoSizeGroup"
                    && Find<Button>(form, "RibbonCalculateButton").Parent?.Parent?.Name == "RibbonCalculateGroup"
                    && Find<Button>(form, "RibbonHighlightCriticalPathButton").Parent?.Parent?.Name == "RibbonCalculateGroup"
                    && form.Controls.Find("RibbonRoundSizesButton", true).Length == 0
                    && form.Controls.Find("RibbonSettingsGroup", true).Length == 0
                    && form.Controls.Find("SolverSettingsButton", true).Length == 0
                    && form.Controls.Find("SettingsButton", true).Length == 0,
                "The Project ribbon should group Project (with Project Settings), Auto Size (Auto Sizing Criteria, Auto Size) and Calculate (Calculate, Highlight Critical Path).");
            ClickButton(calculatorNavigation);
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "CalculatorTab" && Find<Control>(form, "RibbonQuickCalcPage").Visible
                    && ((dynamic)Find<Button>(form, "TopTabQuickCalc")).Selected,
                "Quick Calc's Duct Calculator command should show the duct calculator.");
            Require(material.Items[^1]?.ToString() == "Legacy Reference"
                    && material.SelectedItem?.ToString() == "Legacy Reference",
                "Legacy Reference should remain the selected default at the bottom of the material dropdown.");
            Require(fittingGroup.Items.Cast<object>().Any(item =>
                        item?.ToString() == FittingCatalog.TerminalSelectionGroup)
                    && !fittingGroup.Items.Cast<object>().Any(item => item?.ToString() == "Terminals"),
                "The fitting group should be labeled Terminals & Takeoffs.");
            Require(pressureBasis.Items[0]?.ToString() == "Equivalent Round Area",
                "The equivalent-area pressure basis should use the requested label.");
            Require(form.Controls.Find("RoundingDirectionCombo", true).Length == 0,
                "Autosize rounding should be controlled only from project Settings, not from each duct.");
            Require(saveSystem.Text == "Save" && addDuct.Text == "Save Duct Changes" && !addDuct.Visible
                    && calculateFitting.Text == "Calculate" && addFitting.Text == "Save Fitting Changes" && !addFitting.Visible
                    && form.Controls.Find("CopyResultsButton", true).Length == 0
                    && !Descendants<Button>(form).Any(button => button.Text == "Copy system"),
                "Save Changes actions should be hidden until an existing duct or fitting is opened for editing.");
            Require(autoSizeFan.Text == "Calculate\nCritical Path"
                    && form.Controls.Find("RibbonAnalysisCalculateButton", true).Length == 0
                    && form.Controls.Find("CalculateLongestRouteButton", true).Length == 0
                    && form.Controls.Find("AutoSizeFanButton", true).Length == 0
                    && form.Controls.Find("ViewFanInformationButton", true).Length == 0,
                "Fan sizing is the Project ribbon's Calculate; the Analysis ribbon has no duplicate Calculate, Auto-size Fan or longest-route button.");
            Require(TextRenderer.MeasureText(criticalPathFilter.Text, criticalPathFilter.Font).Width
                    <= criticalPathFilter.ClientSize.Width - 12,
                "The Isolate Critical Path label should fit without clipping.");
            Require(airTemperatureLabel.Text.StartsWith("Air Temp", StringComparison.Ordinal),
                "Duct-calculator input labels should use Title Case.");
            Require(form.Controls.Find("InputLabel_equivalent", true).Length == 0
                    && form.Controls.Find("InputLabel_pressureBasis", true).Length == 0
                    && form.Controls.Find("InputLabel_increment", true).Length == 0,
                "Project-wide calculation defaults should be removed from the per-duct input card.");
            Require(calculatorTitle.Bottom <= calculatorSubtitle.Top,
                "The DuctSizer page title should not overlap or clip into its subtitle.");
            var settingsType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.ProjectSettingsDialog")
                               ?? throw new InvalidOperationException("Could not find the project settings dialog.");
            var settingsDocument = new DuctSystemDocument
            {
                UserName = "Alex Designer",
                ProjectName = "North Clinic Renovation",
                ProjectNumber = "M-24017",
                CompanyLogoPngBase64 = CreateTestCompanyLogo(),
                DefaultSizingIncrementM = UnitConversions.InchesToM(0.5),
                DefaultSizeRoundingDirection = SizeRoundingDirection.Down,
                DefaultEquivalentDiameterMethod = EquivalentDiameterMethod.HydraulicDiameter,
                DefaultPressureLossBasis = PressureLossBasis.ActualDuctArea
            };
            var calculationPage = Enum.Parse(settingsType.GetNestedType("Page", BindingFlags.NonPublic)!, "ProjectInformation");
            using (var settingsDialog = (Form?)Activator.CreateInstance(
                       settingsType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object[] { settingsDocument, UnitSystem.Imperial, calculationPage },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the project settings dialog."))
            {
                var pages = Find<TabControl>(settingsDialog, "ProjectSettingsPages");
                var logoPreview = Find<PictureBox>(settingsDialog, "SettingsCompanyLogoPreview");
                Require(pages.TabPages.Cast<TabPage>().Select(page => page.Text)
                            .SequenceEqual(new[] { "Project Information", "Units" })
                        && pages.SelectedTab?.Name == "ProjectInformationPage"
                        && logoPreview.Image is not null
                        && Find<RadioButton>(settingsDialog, "SettingsImperialUnitsRadio").Checked
                        && !Find<RadioButton>(settingsDialog, "SettingsMetricUnitsRadio").Checked
                        && settingsDialog.Controls.Find("SettingsSizingIncrementText", true).Length == 0,
                    "Project Settings should hold project information and units; calculation settings moved to Auto Sizing Criteria.");
                var settingsCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SETTINGS_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(settingsCapturePath))
                {
                    settingsDialog.Show(form);
                    Application.DoEvents();
                    CaptureForm(settingsDialog, settingsCapturePath);
                    settingsDialog.Hide();
                }
            }
            Require(typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.SolverSettingsDialog") is null,
                "The separate Calculation Method Inputs dialog should be folded into Project Settings.");
            var automaticSizingDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.AutomaticSizingInputsDialog")
                                            ?? throw new InvalidOperationException("Could not find automatic sizing inputs.");
            using (var automaticSizingDialog = (Form?)Activator.CreateInstance(automaticSizingDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { settingsDocument.AutomaticSizing, UnitSystem.Imperial }, null)
                   ?? throw new InvalidOperationException("Could not create automatic sizing inputs."))
            {
                var componentList = Find<ListBox>(automaticSizingDialog, "AutomaticSizingComponentList");
                Require(componentList.Items.Cast<string>().SequenceEqual(new[]
                        { "Duct", "Coil", "Filter", "Elbow", "Transition", "Split", "Merge", "Damper", "Terminal", "GRD" })
                        && Find<ComboBox>(automaticSizingDialog, "DuctCalculationMethod").Items.Count == 3
                        && Find<ComboBox>(automaticSizingDialog, "DuctMaterial").Items.Count == DuctMaterialCatalog.All.Count,
                    "Automatic sizing inputs should expose only the requested components and reuse all duct methods and materials.");
                Require(string.IsNullOrWhiteSpace(Find<TextBox>(automaticSizingDialog, "DuctDiameter").Text)
                        && string.IsNullOrWhiteSpace(Find<TextBox>(automaticSizingDialog, "DuctWidth").Text),
                    "Automatic duct sizing should not prefill a round diameter or rectangular width that the solver must calculate.");
                var sizingCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SIZING_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(sizingCapturePath))
                {
                    automaticSizingDialog.Show(form);
                    Application.DoEvents();
                    Application.DoEvents();
                    CaptureForm(automaticSizingDialog, sizingCapturePath);
                    componentList.SelectedItem = "Elbow";
                    Application.DoEvents();
                    Application.DoEvents();
                    CaptureForm(automaticSizingDialog, Path.ChangeExtension(sizingCapturePath, ".elbow.png"));
                    componentList.SelectedItem = "Duct";
                    Application.DoEvents();
                    automaticSizingDialog.Hide();
                }
                componentList.SelectedItem = "Elbow";
                Application.DoEvents();
                Require(Find<ComboBox>(automaticSizingDialog, "AutomaticFittingDefinition").Items.Count > 0
                        && automaticSizingDialog.Controls.Find("AutomaticFittingParameterTable", true).Length == 1,
                    "Elbow defaults should allow selection of an ASHRAE elbow and its fitting-specific inputs.");
                // Round Z: fitting sizes come from the connected ducts and an elbow's angle from the drawn ducts, so they
                // are greyed out; a split or merge has no flow percentage (its flows come from the sources).
                foreach (var page in new[] { "Elbow", "Transition", "Split", "Merge", "Damper", "Terminal" })
                {
                    componentList.SelectedItem = page;
                    Application.DoEvents();
                    Require(!Find<TextBox>(automaticSizingDialog, "AutomaticFittingDiameter").Enabled
                            && !Find<TextBox>(automaticSizingDialog, "AutomaticFittingWidth").Enabled
                            && !Find<TextBox>(automaticSizingDialog, "AutomaticFittingHeight").Enabled
                            && automaticSizingDialog.Controls.Find("AutomaticFittingBranchPercent", true).Length == 0,
                        $"{page} defaults should grey out the diameter, width and height and have no flow percentage.");
                    if (page == "Elbow")
                    {
                        var elbowDefinitions = Find<ComboBox>(automaticSizingDialog, "AutomaticFittingDefinition");
                        elbowDefinitions.SelectedItem = elbowDefinitions.Items.Cast<FittingDefinition>()
                            .First(definition => definition.Inputs.Any(input => input.Key == "angle"));
                        Application.DoEvents();
                        Require(automaticSizingDialog.Controls.Find("AutomaticFittingInput_angle", true) is { Length: 1 } angleInputs
                                && !angleInputs[0].Enabled,
                            "The elbow angle should be greyed out: it comes from the drawn ducts.");
                    }
                }
                componentList.SelectedItem = "GRD";
                Application.DoEvents();
                Require(Find<ComboBox>(automaticSizingDialog, "AutomaticGrdTitus350RSize").Items.Count == 78
                        && !Find<ComboBox>(automaticSizingDialog, "AutomaticGrdNeckDiameter").Enabled,
                    "Automatic GRD sizing should expose every Titus 350R chart and take the diffuser neck from the connected duct.");
            }
            using (var wideDropDown = new ComboBox { Width = 612, DropDownStyle = ComboBoxStyle.DropDownList })
            {
                wideDropDown.Items.Add("A deliberately long dropdown item used to verify responsive sizing");
                UiMember.Method("ResizeComboDropDown", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { wideDropDown });
                Require(wideDropDown.DropDownWidth >= wideDropDown.Width,
                    "Wide dropdowns should resize safely without reversing their minimum and maximum limits.");
            }

            form.Size = Sz(form, 1280, 820);
            form.PerformLayout();
            Application.DoEvents();
            Require(content.ColumnCount == 2, "Wide layout should display inputs and outputs side by side.");
            // Round W: calculator inputs use the rounded frames of the redesigned windows.
            Require(mode.FlatStyle == FlatStyle.Flat && fittingGroup.FlatStyle == FlatStyle.Flat,
                "Calculator dropdowns should use the rounded input frame of the redesigned windows.");
            Require(airflowInput.Parent is { } airflowHost
                    && airflowInput.Width >= Px(60) && airflowInput.Height >= Px(12)
                    && airflowHost.ClientRectangle.Contains(airflowInput.Bounds)
                    && (!form.Visible || airflowInput.Visible),
                $"The duct airflow input should be fully visible inside its value-and-unit field. Input={airflowInput.Bounds}, host={airflowInput.Parent?.Bounds}, visible={airflowInput.Visible}.");
            Require(!ductCalculatorSplitter.IsSplitterFixed && ductCalculatorSplitter.SplitterWidth >= 8
                    && ductCalculatorSplitter.Panel1MinSize >= 250 && ductCalculatorSplitter.Panel2MinSize >= 250
                    && !fittingCalculatorSplitter.IsSplitterFixed && fittingCalculatorSplitter.SplitterWidth >= 8,
                "Both calculators should expose draggable vertical dividers with usable minimum pane widths.");
            var originalDuctSplit = ductCalculatorSplitter.SplitterDistance;
            ductCalculatorSplitter.SplitterDistance = originalDuctSplit + 36;
            Application.DoEvents();
            Require(ductCalculatorSplitter.SplitterDistance == originalDuctSplit + 36
                    && ductCalculatorSplitter.Panel1.Width > originalDuctSplit,
                "Dragging the Duct Calculator divider should resize both columns.");
            ductCalculatorSplitter.SplitterDistance = originalDuctSplit;
            Require(ductOutputsHeader.Text == "OUTPUTS" && fittingOutputsHeader.Text == "OUTPUTS"
                    && assumptionsText.ScrollBars == ScrollBars.None,
                "Both calculators should label the blue summary area as OUTPUTS and show duct assumptions without a scrollbar.");
            var ductInputsTitle = Descendants<Label>(inputCard).Single(label => label.Text == "INPUTS");
            var fittingInputsTitle = Descendants<Label>(Find<Control>(form, "FittingInputCard"))
                .Single(label => label.Text == "FITTING INPUTS");
            Require(Math.Abs(ductOutputsHeader.Font.Size - ductInputsTitle.Font.Size) < 0.1F
                    && Math.Abs(fittingOutputsHeader.Font.Size - fittingInputsTitle.Font.Size) < 0.1F,
                "OUTPUTS headings should use the same title size as their calculator INPUTS headings.");
            Require(projectHeader.Text.Contains("Untitled Project", StringComparison.Ordinal)
                    && form.Text.Contains("Untitled Project", StringComparison.Ordinal),
                "The top bar and window title should show the project identity.");
            Require(projectIdentityHeader.Height <= Px(50) && ribbon.Height <= Px(104)
                    && projectHeaderBrand.Text == "fan works"
                    && projectHeaderAppIcon.Image is not null
                    && projectIdentityHeader.GetType().Name == "ProjectIdentityHeaderPanel"
                    && Find<Control>(form, "MainTopTabs").Parent == projectIdentityHeader
                    && new[] { "TopTabProject", "TopTabAnalysis", "TopTabReports", "TopTabQuickCalc" }
                        .Select(name => Find<Button>(form, name).Text)
                        .SequenceEqual(new[] { "Project", "Analysis", "Reports", "Quick Calc" })
                    && form.Controls.Find("MainNavigationHost", true).Length == 0
                    && form.Controls.Find("DesignerSectionHeader", true).Length == 0
                    && Find<Control>(form, "CalculatorSectionHeader").GetType().Name == "SectionTitlePanel"
                    && Find<Control>(form, "CalculatorSectionHeader").Height <= Px(60)
                    && form.Controls.Find("SystemSectionHeader", true).Length == 0,
                "The window frame should be a slim top bar (brand, Project/Analysis/Reports/Quick Calc tabs, project identity) over the ribbon, with a fixed height on any screen.");
            Require(projectIdentityHeader is TableLayoutPanel identityLayout
                    && identityLayout.GetColumn(projectHeader) == 3
                    && identityLayout.GetColumn(projectHeaderLogo) == 4
                    && projectHeader.TextAlign == ContentAlignment.MiddleRight,
                "Project metadata should stay right-aligned beside the company logo in the top bar.");
            Require(IsDescendantOf(ductSummary, inputCard), "The duct summary should remain in the input card.");
            Require(Descendants<Label>(resultsCard).Any(label => label.Text == "CALCULATIONS"),
                "The detail pane should retain its CALCULATIONS heading.");
            foreach (var requiredText in new[] { "Colebrook", "iterations", "cfm", "fpm", "in. w.c.", "lbm/ft³" })
                Require(ductCalculationDetails.Text.Contains(requiredText, StringComparison.OrdinalIgnoreCase),
                    $"The Duct Calculator calculation worksheet should contain {requiredText}.");
            Require((ductCalculationDetails.Rtf ?? "").Contains("\\pict", StringComparison.Ordinal),
                "The Duct Calculator should render equations as stacked display math.");
            // Round W: the Quick Calc worksheet is the duct's report worksheet (same sections and order as the PDF).
            var ductWorksheetOrder = new[]
            {
                "DUCT SIZING CALCULATION", "INPUT VALUES", "1. FINAL DUCT GEOMETRY", "2. AIR PROPERTIES",
                "3. VELOCITY AND FLOW REGIME", "4. DARCY FRICTION FACTOR", "5. VELOCITY PRESSURE AND DUCT LOSS",
                "VARIABLES AND WHERE THEY COME FROM", "ASSUMPTIONS"
            }.Select(text => ductCalculationDetails.Text.IndexOf(text, StringComparison.Ordinal)).ToArray();
            Require(ductWorksheetOrder.All(index => index >= 0)
                    && ductWorksheetOrder.SequenceEqual(ductWorksheetOrder.OrderBy(index => index))
                    && ductCalculationDetails.Text.Contains("Sutherland relation", StringComparison.Ordinal)
                    && ductCalculationDetails.Text.Contains("Solved by bisection", StringComparison.Ordinal)
                    && ductCalculationDetails.Text.Contains("Final duct geometry", StringComparison.Ordinal)
                    && (ductCalculationDetails.Rtf ?? "").Contains("\\emfblip", StringComparison.Ordinal)
                    && !ductCalculationDetails.Text.Contains("Geometry_rounded", StringComparison.Ordinal),
                "The Duct Calculator should show the duct's report worksheet: inputs, steps 1 to 5 in dependency order, the variables and the assumptions. "
                + $"Order={string.Join(",", ductWorksheetOrder)}, length={ductCalculationDetails.TextLength}.");
            var reportDuctSections = (IReadOnlyList<DuctSystemPdfCalculationSection>)typeof(MainForm).Assembly
                .GetType("DuctSizer.WinForms.DuctSystemScreen")!
                .GetMethod("BuildDuctPdfCalculations", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { PrivateField<DuctCalculationResult>(form, "_lastCalculation")! })!;
            Require(reportDuctSections.Where(section => !section.IsTitle)
                    .All(section => ductCalculationDetails.Text.Contains(section.Heading, StringComparison.Ordinal)),
                "Every section of the duct's report worksheet should appear in the Duct Calculator.");
            Require(ductSummary.Controls.Cast<Control>().All(control => control.BackColor == Color.White)
                    && fittingSummary.Controls.Cast<Control>().All(control => control.BackColor == Color.White),
                "Calculator outputs should use clean label/value rows without blue surrounding boxes.");
            var ductShowCalculations = PrivateField<Button>(form, "_ductCalculationsShowButton");
            var ductHideCalculations = PrivateField<Button>(form, "_ductCalculationsHideButton");
            var ductExpandCalculations = PrivateField<Button>(form, "_ductCalculationsExpandButton");
            Require(ductShowCalculations.Text == "Show Calculations",
                "The collapsed Duct Calculator control should be labeled Show Calculations.");
            Require(TextRenderer.MeasureText(ductShowCalculations.Text, ductShowCalculations.Font).Width
                    <= ductShowCalculations.ClientSize.Width - 12,
                "The full Duct Calculator Show Calculations label should be visible.");
            Require(TextRenderer.MeasureText(ductExpandCalculations.Text, ductExpandCalculations.Font).Width
                    <= ductExpandCalculations.ClientSize.Width - 12,
                "The Duct Calculator Expand label should be fully visible.");
            ductExpandCalculations.PerformClick();
            Application.DoEvents();
            Require(content.ColumnStyles[1].Width > content.ColumnStyles[0].Width,
                "The Duct Calculator calculation pane should expand on request.");
            Require(ductExpandCalculations.Text == "Restore"
                    && TextRenderer.MeasureText(ductExpandCalculations.Text, ductExpandCalculations.Font).Width
                    <= ductExpandCalculations.ClientSize.Width - 12,
                "The Duct Calculator Restore label should be fully visible.");
            ductHideCalculations.PerformClick();
            Application.DoEvents();
            Require(!resultsCard.Visible && ductShowCalculations.Visible,
                "The Duct Calculator calculation pane should minimize and expose a restore control.");
            ductShowCalculations.PerformClick();
            ductExpandCalculations.PerformClick();
            Application.DoEvents();
            Require(ScreenBounds(ductSummary).Top >= ScreenBounds(ductInputScroll).Bottom
                    && ScreenBounds(ductSummary).Bottom <= ScreenBounds(calculateButton).Top,
                $"Duct outputs should remain visible between the scrollable inputs and bottom action row. " +
                $"Input={ScreenBounds(ductInputScroll)}, Outputs={ScreenBounds(ductSummary)}, Button={ScreenBounds(calculateButton)}.");
            var ductPageCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_DUCT_PAGE_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(ductPageCapturePath))
            {
                form.Size = Sz(form, 1800, 1200);
                form.PerformLayout();
                Application.DoEvents();
                CaptureForm(form, ductPageCapturePath);
                Require(!ductInputScroll.VerticalScroll.Visible,
                    $"Expanded Duct Calculator inputs should fit without a vertical scrollbar. " +
                    $"Panel={ductInputScroll.ClientSize}, table={ductInputTable.Size}, preferred={ductInputTable.PreferredSize}, " +
                    $"rows={string.Join(',', ductInputTable.GetRowHeights())}, styles={string.Join(',', ductInputTable.RowStyles.Cast<RowStyle>().Select(style => $"{style.SizeType}:{style.Height}"))}.");
                form.Size = Sz(form, 1280, 820);
                Application.DoEvents();
            }
            Require(ScreenBounds(airflowInput).Top < ScreenBounds(temperatureInput).Top
                    && ScreenBounds(temperatureInput).Top < ScreenBounds(lengthInput).Top,
                "Airflow, air temperature, and duct length should use a clear vertical Calculation column.");
            ClickButton(layoutNavigation);
            Application.DoEvents();
            Require(ribbonComponentTools.Length == 20
                    && ribbonComponentTools.All(card => IsDescendantOf(card, ribbon) && card.Height <= Px(40))
                    && ScreenBounds(ribbon).Bottom <= ScreenBounds(layoutCanvasHost).Top
                    && form.Controls.Find("LayoutToolPalette", true).Length == 0,
                "The component toolbox should live in the Project ribbon, grouped by kind, above the drawing.");
            sourceSpaceTool.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sourceSpaceTool, new object[] { EventArgs.Empty });
            Require((LayoutNodeType?)UiMember.Field("_selectedLayoutTool", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form) == LayoutNodeType.Reservoir,
                "Clicking a toolbox component should enter placement mode.");
            Require((bool)UiMember.Method("CancelSelectedLayoutTool", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(form, null)!
                    && UiMember.Field("_selectedLayoutTool", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(form) is null
                    && layoutCanvas.GetType().GetProperty("SelectedTool", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                        .GetValue(layoutCanvas) is null,
                "Escape cancellation should clear the selected toolbox component and canvas placement mode.");
            Require(layoutSummaryGrid.Columns.Cast<DataGridViewColumn>().Select(x => x.Name).SequenceEqual(
                        new[] { "Id", "Description", "Branch", "Size", "Airflow", "Velocity", "PressureRate", "StaticDrop", "PressureIn",
                            "PressureOut", "Length", "SurfaceArea", "Upstream", "Downstream", "AbsoluteX", "AbsoluteY", "AbsoluteZ" })
                    && layoutSummaryGrid.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.Fill
                    && layoutSummarySplitter.Orientation == Orientation.Horizontal
                    && !layoutSummarySplitter.Panel2Collapsed
                    && IsDescendantOf(layoutSummaryGrid, layoutSummarySplitter.Panel2)
                    && IsDescendantOf(layoutCanvasHost, layoutSummarySplitter.Panel1),
                "The System Overview (former quick view) should sit in the bottom panel under the drawing.");
            Require(new[] { "LayoutOverviewTab", "LayoutErrorsTab", "LayoutValidationTab" }
                        .Select(name => Find<Button>(form, name).Text.Split(' ')[0])
                        .SequenceEqual(new[] { "System", "Errors", "Net" })
                    && Find<Button>(form, "LayoutValidationTab").Text == "Net Path Static Pressure"
                    && form.Controls.Find("LayoutSchedulesTab", true).Length == 0,
                "The bottom panel should have System Overview, Errors and Net Path Static Pressure tabs (no Schedules).");
            ClickButton(layoutSummaryCollapse);
            Application.DoEvents();
            Require(layoutSummarySplitter.Panel2Collapsed && layoutSummaryReopen.Visible,
                "The bottom panel should hide and leave a reopen bar.");
            var quickViewCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_QUICKVIEW_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(quickViewCapturePath))
            {
                CaptureForm(form, quickViewCapturePath);
            }
            ClickButton(layoutSummaryReopen);
            Application.DoEvents();
            Require(!layoutSummarySplitter.Panel2Collapsed && !layoutSummaryReopen.Visible,
                "The hidden bottom panel should reopen in place.");
            ClickButton(calculatorNavigation);
            Application.DoEvents();
            Require(inputCard.Region is not null && calculateButton.Region is not null,
                "Primary cards and actions should use rounded edges.");
            Require(mode.Width <= Px(250) && airflowInput.Width <= Px(135)
                    && ScreenBounds(mode).Left - ScreenBounds(Find<Label>(form, "InputLabel_mode")).Right <= Px(12),
                $"Duct-calculator dropdowns and text inputs should use compact widths close to their labels. " +
                $"Mode={mode.Width}, airflow={airflowInput.Width}, gap=" +
                $"{ScreenBounds(mode).Left - ScreenBounds(Find<Label>(form, "InputLabel_mode")).Right}.");
            Require(typeof(MainForm).Assembly.GetManifestResourceNames().Contains(
                        "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.ico", StringComparer.Ordinal)
                    && typeof(MainForm).Assembly.GetManifestResourceNames().Contains(
                        "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.png", StringComparer.Ordinal)
                    && typeof(MainForm).Assembly.GetManifestResourceNames().Count(name =>
                        name.StartsWith("DuctSizer.WinForms.Assets.OpenSourceIcons.png.", StringComparison.Ordinal)) >= 20
                    && form.Icon is { Width: > 0, Height: > 0 },
                "The application should embed and display the HVAC application icon and open-source designer icon set.");

            form.Size = Sz(form, 800, 900);
            form.PerformLayout();
            Application.DoEvents();
            Require(content.ColumnCount == 1, "Narrow layout should stack inputs and outputs.");

            shape.SelectedIndex = 0;
            Application.DoEvents();
            Require(equivalent.Enabled, "Equivalent-diameter dropdown must remain enabled for round ducts.");
            Require(pressureBasis.Enabled, "Pressure-loss-basis dropdown must remain enabled for round ducts.");

            shape.SelectedIndex = 1;
            Application.DoEvents();
            Require(equivalent.Enabled, "Equivalent-diameter dropdown must remain enabled for rectangular ducts.");
            Require(pressureBasis.Enabled, "Pressure-loss-basis dropdown must remain enabled for rectangular ducts.");

            material.SelectedItem = material.Items.Cast<object>().Single(item =>
                string.Equals(item.ToString(), "Galvanized Spiral Duct", StringComparison.Ordinal));
            Application.DoEvents();
            Require(!shape.Enabled && shape.SelectedItem?.ToString() == "Round",
                "Galvanized spiral material should force and lock a round duct shape.");
            material.SelectedItem = material.Items.Cast<object>().Single(item =>
                string.Equals(item.ToString(), "Galvanized Rectangular Duct", StringComparison.Ordinal));
            Application.DoEvents();
            Require(!shape.Enabled && shape.SelectedItem?.ToString() == "Rectangular",
                "Galvanized rectangular material should force and lock a rectangular duct shape.");
            material.SelectedItem = material.Items.Cast<object>().Single(item =>
                string.Equals(item.ToString(), "Internally Fiber-Lined Rectangular Duct", StringComparison.Ordinal));
            Application.DoEvents();
            Require(!shape.Enabled && shape.SelectedItem?.ToString() == "Rectangular",
                "Internally lined rectangular material should force and lock a rectangular duct shape.");
            material.SelectedItem = material.Items.Cast<object>().Single(item =>
                string.Equals(item.ToString(), "Legacy Reference", StringComparison.Ordinal));
            Application.DoEvents();
            Require(shape.Enabled, "Unrestricted duct materials should restore the shape selector.");

            mode.SelectedIndex = 1;
            shape.SelectedIndex = 1;
            Application.DoEvents();
            Require(targetFrictionInput.Visible && !maximumVelocityInput.Visible
                    && ScreenBounds(targetFrictionInput).Top > ScreenBounds(lengthInput).Top
                    // Next row: borderless inputs in rounded frames (round W) are shorter, so compare row tops.
                    && ScreenBounds(targetFrictionInput).Top - ScreenBounds(lengthInput).Top <= Px(40),
                $"SP autosizing should show its target directly below duct length in the Calculation section. Length={ScreenBounds(lengthInput)}, target={ScreenBounds(targetFrictionInput)}, visible={targetFrictionInput.Visible}/{maximumVelocityInput.Visible}, form={form.Size}.");
            Require(rectangularSizingDimension.Visible && rectangularSizingValue.Visible
                    && rectangularSizingDimension.Items.Cast<object>().Select(item => item.ToString())
                        .SequenceEqual(new[] { "Width", "Height" })
                    && rectangularSizingUnit.Text == "in."
                    && ScreenBounds(rectangularSizingDimension).Right <= ScreenBounds(rectangularSizingUnit).Left
                    && ScreenBounds(rectangularSizingUnit).Right <= ScreenBounds(rectangularSizingValue).Left,
                "Rectangular autosizing should use a Width/Height selector, an inch value, and an explicit unit suffix.");
            rectangularSizingDimension.SelectedIndex = 0;
            rectangularSizingValue.Text = "16";
            calculateButton.PerformClick();
            Application.DoEvents();
            var lastCalculation = PrivateField<DuctCalculationResult>(form, "_lastCalculation");
            Require(lastCalculation is not null
                    && Math.Abs(UnitConversions.MToInches(lastCalculation.Input.Geometry.WidthM) - 16.0) < 1e-8,
                "Selecting Width should hold the entered inch dimension while sizing rectangular duct height.");
            var rectangularCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_RECTANGULAR_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(rectangularCapturePath))
            {
                var priorSize = form.Size;
                form.Size = Sz(form, 1200, 1200);
                form.PerformLayout();
                Application.DoEvents();
                CaptureForm(form, rectangularCapturePath);
                form.Size = priorSize;
                form.PerformLayout();
                Application.DoEvents();
            }
            rectangularSizingDimension.SelectedIndex = 1;
            rectangularSizingValue.Text = "12";
            calculateButton.PerformClick();
            Application.DoEvents();
            lastCalculation = PrivateField<DuctCalculationResult>(form, "_lastCalculation");
            Require(lastCalculation is not null
                    && Math.Abs(UnitConversions.MToInches(lastCalculation.Input.Geometry.HeightM) - 12.0) < 1e-8,
                "Selecting Height should hold the entered inch dimension while sizing rectangular duct width.");
            mode.SelectedIndex = 2;
            Application.DoEvents();
            Require(maximumVelocityInput.Visible && !targetFrictionInput.Visible
                    && ScreenBounds(maximumVelocityInput).Top > ScreenBounds(lengthInput).Top
                    && ScreenBounds(maximumVelocityInput).Top - ScreenBounds(lengthInput).Top <= Px(40),
                "Velocity autosizing should show its target directly below duct length in the Calculation section.");
            Require(!Descendants<Label>(inputCard).Any(label => label.Text == "DESIGN TARGET"),
                "Autosize target inputs should remain in Calculation without a separate Design Target section.");
            Require(form.Controls.Find("RoundingDirectionCombo", true).Length == 0,
                "Pressure and velocity autosizing should continue to use the project rounding setting without a per-duct selector.");

            shape.SelectedIndex = 0;
            mode.SelectedIndex = 0;
            UiMember.Method("AddOrUpdateCurrentDuct", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Application.DoEvents();
            Require(grid.Rows.Count == 1, "Add duct should create one duct-system row.");
            Require(Convert.ToString(grid.Rows[0].Cells["Id"].Value) == "D-001", "The first saved duct ID should be D-001.");
            Require(!total.Text.EndsWith("—", StringComparison.Ordinal)
                    && !total.Text.Contains("fan positive static", StringComparison.OrdinalIgnoreCase),
                "The system pressure-loss total should be calculated without the removed fan-static note.");
            Require(File.Exists(systemPath),
                $"The duct system should be saved to the local JSON file. Path={systemPath}; status={Find<Label>(form, "SystemStorageStatusLabel").Text}");
            Require(tabs.TabPages[1].Text == "Fitting Calculator", "The fitting calculator should have its own tab.");
            Require(tabs.TabPages[2].Text == "Duct System", "The system tab title should not contain a duct count.");
            Require(tabs.TabPages.Count == 5 && tabs.TabPages[3].Text == "Layout Designer"
                    && tabs.TabPages[4].Name == "ReportsTab",
                "The Layout Designer (Project tab) and the Reports page should each have their own page.");
            Require(grid.Columns[2].Name == "StaticDrop", "Static pressure drop should be near the front of the system table.");
            Require(grid.Columns.Contains("AirSystem") && grid.Columns.Contains("BranchId")
                    && Convert.ToString(grid.Rows[0].Cells["AirSystem"].Value) == "Supply Air"
                    && Convert.ToString(grid.Rows[0].Cells["BranchId"].Value) == "Main",
                "Manual system rows should share default air-system and branch metadata.");
            Require(grid.Columns.Contains("UpstreamId") && grid.Columns.Contains("DownstreamId"),
                "The system schedule should expose upstream and downstream item IDs.");
            Require(form.Controls.Find("ExportSystemButton", true).Length == 0
                    && form.Controls.Find("ImportSystemButton", true).Length == 0,
                "Project export and import should be removed from the Duct System action row.");
            Require(new[] { "RibbonNewProjectButton", "RibbonOpenButton", "RibbonSaveButton", "RibbonSaveAsButton" }
                        .All(name => Find<Button>(form, name).Parent?.Parent?.Name == "RibbonProjectGroup")
                    && Find<Button>(form, "RibbonSizingCriteriaButton").Text.Replace("\n", " ", StringComparison.Ordinal) == "Auto Sizing Criteria"
                    && UiMember.Field("_settingsMenu", BindingFlags.Instance | BindingFlags.NonPublic) is null,
                "The Project ribbon should expose New Project, Open, Save, Save As and Auto Sizing Criteria in place of the Settings menu.");
            Require(form.Controls.Find("EditDuctButton", true).Length == 0
                    && form.Controls.Find("RemoveDuctButton", true).Length == 0
                    && editSystemItem.Text == "Edit item"
                    && removeSystemItem.Text == "Remove item",
                "Duct System editing and removal should be available from the grid right-click menu, not action buttons.");
            Require(deleteAllSystem.Text == "Delete all",
                "The system tab should expose a clearly labeled delete-all action.");
            Require(criticalPathFilter.Text == "Isolate Critical Path"
                    && ScreenBounds(deleteAllSystem).Left > ScreenBounds(criticalPathFilter).Right
                    && viewFanInformation.Text.Replace("\n", " ", StringComparison.Ordinal) == "Fan Information",
                "Isolate Critical Path should stay with the table, with Delete all isolated at the far right; fan information is a ribbon command.");
            form.Size = Sz(form, 1280, 820);
            Application.DoEvents();
            tabs.SelectedTab = tabs.TabPages[1];
            Application.DoEvents();
            fittingInputScroll.AutoScrollPosition = new Point(0, 255);
            Application.DoEvents();
            tabs.SelectedTab = tabs.TabPages[0];
            Application.DoEvents();
            tabs.SelectedTab = tabs.TabPages[1];
            Application.DoEvents();
            Application.DoEvents();
            var fittingScrollBounds = ScreenBounds(fittingInputScroll);
            var fittingGroupBounds = ScreenBounds(fittingGroup);
            Require(fittingGroupBounds.Top >= fittingScrollBounds.Top
                    && fittingGroupBounds.Bottom <= fittingScrollBounds.Bottom,
                $"Opening Fitting Loss should always restore its fitting selector and common inputs. " +
                $"Scroll={fittingInputScroll.AutoScrollPosition}, panel={fittingScrollBounds}, group={fittingGroupBounds}.");
            Require(fittingGroup.Items.Cast<object>().Select(Convert.ToString).SequenceEqual(FittingCatalog.SelectionGroups),
                "The fitting selector should expose the requested seven fitting groups in order.");
            Require(IsDescendantOf(fittingSummary, Find<Control>(form, "FittingInputCard"))
                    && Descendants<Label>(fittingOutputCard).Any(label => label.Text == "CALCULATIONS")
                    && fittingCalculationSourceImage.Image is not null
                    // Round W: the fitting's report worksheet, as in the PDF and the Project tab.
                    && fittingCalculationDetails.Text.Contains("FITTING SIZING CALCULATION", StringComparison.Ordinal)
                    && fittingCalculationDetails.Text.Contains("PUBLISHED TABLE / EQUATION INPUTS", StringComparison.Ordinal)
                    && fittingCalculationDetails.Text.Contains("FLOW AND GEOMETRY", StringComparison.Ordinal)
                    && fittingCalculationDetails.Text.Contains("COEFFICIENT AND PRESSURE LOSS", StringComparison.Ordinal)
                    && fittingCalculationDetails.Text.Contains("VARIABLES AND WHERE THEY COME FROM", StringComparison.Ordinal)
                    && fittingCalculationDetails.Text.Contains("in. w.c.", StringComparison.Ordinal)
                    && (fittingCalculationDetails.Rtf ?? "").Contains("\\pict", StringComparison.Ordinal),
                "The Fitting Calculator should keep summary outputs in its left card and show the ASHRAE source table with properly typeset Imperial equations on the right.");
            var fittingShowCalculations = PrivateField<Button>(form, "_fittingCalculationsShowButton");
            var fittingHideCalculations = PrivateField<Button>(form, "_fittingCalculationsHideButton");
            var fittingExpandCalculations = PrivateField<Button>(form, "_fittingCalculationsExpandButton");
            Require(fittingShowCalculations.Text == "Show Calculations",
                "The collapsed Fitting Calculator control should be labeled Show Calculations.");
            Require(TextRenderer.MeasureText(fittingShowCalculations.Text, fittingShowCalculations.Font).Width
                    <= fittingShowCalculations.ClientSize.Width - 12,
                "The full Fitting Calculator Show Calculations label should be visible.");
            Require(TextRenderer.MeasureText(fittingExpandCalculations.Text, fittingExpandCalculations.Font).Width
                    <= fittingExpandCalculations.ClientSize.Width - 12,
                "The Fitting Calculator Expand label should be fully visible.");
            fittingExpandCalculations.PerformClick();
            Application.DoEvents();
            Require(fittingCalculatorSplitter.Panel2.Width > fittingCalculatorSplitter.Panel1.Width,
                "The Fitting Calculator calculation pane should expand on request.");
            Require(fittingExpandCalculations.Text == "Restore"
                    && TextRenderer.MeasureText(fittingExpandCalculations.Text, fittingExpandCalculations.Font).Width
                    <= fittingExpandCalculations.ClientSize.Width - 12,
                "The Fitting Calculator Restore label should be fully visible.");
            fittingHideCalculations.PerformClick();
            Application.DoEvents();
            Require(!fittingOutputCard.Visible && fittingShowCalculations.Visible,
                "The Fitting Calculator calculation pane should minimize and expose a restore control.");
            fittingShowCalculations.PerformClick();
            fittingExpandCalculations.PerformClick();
            Application.DoEvents();
            Require(IsDescendantOf(fittingOutputsHeader, Find<Control>(form, "FittingInputCard"))
                    && IsDescendantOf(fittingSummary, Find<Control>(form, "FittingInputCard"))
                    && ScreenBounds(fittingSummary).Top >= ScreenBounds(fittingInputScroll).Bottom
                    && ScreenBounds(fittingSummary).Bottom <= ScreenBounds(calculateFitting).Top,
                "Fitting outputs should remain visible between the scrollable inputs and bottom action row.");
            {
                // The Calculate button must stay inside its card on a short window (it was pushed out of view).
                var fittingCard = Find<Control>(form, "FittingInputCard");
                var sizeBeforeShort = form.Size;
                form.Size = new Size(form.Width, Math.Max(form.MinimumSize.Height, 650));
                Application.DoEvents();
                // Compare with the visible page area (the card itself can be clipped by its parent).
                var visibleArea = fittingCard.Parent;
                while (visibleArea is not null && visibleArea is not TableLayoutPanel)
                    visibleArea = visibleArea.Parent;
                var cardBounds = Rectangle.Intersect(ScreenBounds(fittingCard), ScreenBounds(visibleArea ?? fittingCard));
                var buttonBounds = ScreenBounds(calculateFitting);
                Require(calculateFitting.Visible && buttonBounds.Bottom <= cardBounds.Bottom && buttonBounds.Top >= cardBounds.Top,
                    $"The Fitting Calculator's Calculate button should stay visible inside its card on a short window. visible card={cardBounds}, button={buttonBounds}");
                form.Size = sizeBeforeShort;
                Application.DoEvents();
            }
            var fittingPageCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_FITTING_PAGE_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(fittingPageCapturePath))
            {
                form.Size = Sz(form, 1800, 1200);
                form.PerformLayout();
                Application.DoEvents();
                CaptureForm(form, fittingPageCapturePath);
                Require(!fittingInputScroll.VerticalScroll.Visible,
                    $"Expanded Fitting Calculator inputs should fit without a vertical scrollbar. " +
                    $"Panel={fittingInputScroll.ClientSize}, table={fittingBaseInputs.Size}, preferred={fittingBaseInputs.PreferredSize}, " +
                    $"rows={string.Join(',', fittingBaseInputs.GetRowHeights())}.");
                form.Size = Sz(form, 1280, 820);
                Application.DoEvents();
            }
            var fittingSelectorInputs = fittingSelectorPanel.Controls.Cast<Control>()
                .Single(control => IsDescendantOf(fittingGroup, control));
            Require(fittingBaseInputs.ColumnCount == 4
                    && IsDescendantOf(fittingType, fittingSelectorInputs)
                    && IsDescendantOf(fittingAirflow, fittingSelectorInputs)
                    && IsDescendantOf(fittingTemperature, fittingSelectorInputs)
                    && IsDescendantOf(fittingShape, fittingSelectorInputs)
                    && IsDescendantOf(fittingDiameter, fittingSelectorInputs)
                    && ScreenBounds(fittingAirflow).Top > ScreenBounds(fittingType).Top,
                $"Fitting selection, airflow, temperature, shape, and geometry should share the compact selector column beside the image. " +
                $"Airflow={ScreenBounds(fittingAirflow)}, Temperature={ScreenBounds(fittingTemperature)}, " +
                $"Shape={ScreenBounds(fittingShape)}, Diameter={ScreenBounds(fittingDiameter)}, " +
                $"Rows={fittingBaseInputs.GetRow(fittingAirflow)}/{fittingBaseInputs.GetRow(fittingShape)}/{fittingBaseInputs.GetRow(fittingDiameter)}, " +
                $"Heights={string.Join(',', fittingBaseInputs.GetRowHeights())}, " +
                $"Local={fittingAirflow.Bounds}/{fittingShape.Bounds}/{fittingDiameter.Bounds}.");
            Require(fittingGroup.Width <= Px(180) && fittingType.Width <= Px(125)
                    && fittingAirflow.Width <= Px(120) && fittingShape.Width <= Px(165)
                    && ScreenBounds(fittingAirflow).Left - ScreenBounds(PrivateField<Label>(form, "_fittingAirflowLabel")).Right <= Px(12),
                "Fitting dropdowns and text inputs should use compact widths close to their labels.");
            var groupedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupName in FittingCatalog.SelectionGroups)
            {
                fittingGroup.SelectedItem = groupName;
                Application.DoEvents();
                Require(fittingType.Items.Cast<FittingDefinition>().All(x => FittingCatalog.SelectionGroup(x) == groupName),
                    $"The {groupName} selector should contain only fittings from that group.");
                foreach (var definition in fittingType.Items.Cast<FittingDefinition>())
                    groupedCodes.Add(definition.Code);
            }
            Require(groupedCodes.Count == 108, "The grouped selectors should contain 103 ASHRAE fittings, three published GRD entries, and two VAV entries.");
            fittingGroup.SelectedItem = "GRDs";
            Application.DoEvents();
            var titus350RIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == GrdPerformanceCatalog.Titus350RCode).index;
            fittingType.SelectedIndex = titus350RIndex;
            var titus350RSize = Find<ComboBox>(form, "FittingInput_sizeIndex");
            titus350RSize.SelectedIndex = GrdPerformanceCatalog.Titus350RSizeIndex("12x12");
            fittingAirflow.Text = "500";
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            // Round W: the Quick Calc worksheet is the item's report worksheet, section for section.
            bool ShowsReportWorksheet(string details, FittingCalculationResult? calculation) =>
                calculation is not null
                && details.Contains("COMPONENT SIZING CALCULATION", StringComparison.Ordinal)
                && ((IReadOnlyList<DuctSystemPdfCalculationSection>)UiMember.Method("BuildFittingPdfCalculations",
                        BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { calculation })!)
                    .Where(section => !section.IsTitle)
                    .All(section => details.Contains(section.Heading, StringComparison.Ordinal));
            var titus350RDetails = Find<RichTextBox>(form, "FittingCalculationDetails").Text;
            Require(titus350RSize.Items.Count == 78
                    && PrivateField<Label>(form, "_fittingCoefficientValue").Text == "528 cfm"
                    && PrivateField<Label>(form, "_fittingVelocityValue").Text == "0.073 in. w.c."
                    && PrivateField<Label>(form, "_fittingVelocityPressureValue").Text == "NC 19"
                    && PrivateField<Label>(form, "_fittingPressureLossValue").Text == "600 fpm"
                    && ShowsReportWorksheet(titus350RDetails, PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")),
                "The Titus 350R grille calculator should expose all nominal sizes, round 500 cfm upward, and report chart pressure, NC, and core velocity.");
            var titus350RCalculation = PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")
                                       ?? throw new InvalidOperationException("The Titus 350R calculation result was not retained.");
            var titus350RCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_GRILLE_FITTING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(titus350RCapturePath))
            {
                CaptureForm(form, titus350RCapturePath);
            }
            var priceDiffuserIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == GrdPerformanceCatalog.PriceScdCode).index;
            fittingType.SelectedIndex = priceDiffuserIndex;
            fittingAirflow.Text = "450";
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            Require(Find<ComboBox>(form, "FittingInput_face").Items.Count == 3
                    && Find<ComboBox>(form, "FittingInput_neck").Items.Count == 6
                    && PrivateField<Label>(form, "_fittingCoefficientValue").Text == "489 cfm"
                    && PrivateField<Label>(form, "_fittingVelocityValue").Text == "0.199 in. w.c."
                    && PrivateField<Label>(form, "_fittingVelocityPressureValue").Text == "NC 36"
                    && PrivateField<Label>(form, "_fittingPressureLossValue").Text == "6-8-12 ft"
                    && ShowsReportWorksheet(Find<RichTextBox>(form, "FittingCalculationDetails").Text, PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")),
                $"The Price SCD calculator should expose face/neck choices, round 450 cfm upward, and show pressure, NC, throw, and the recreated table. " +
                $"Face={Find<ComboBox>(form, "FittingInput_face").Items.Count}; Neck={Find<ComboBox>(form, "FittingInput_neck").Items.Count}; " +
                $"Values={PrivateField<Label>(form, "_fittingCoefficientValue").Text}|{PrivateField<Label>(form, "_fittingVelocityValue").Text}|" +
                $"{PrivateField<Label>(form, "_fittingVelocityPressureValue").Text}|{PrivateField<Label>(form, "_fittingPressureLossValue").Text}");
            var grdCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_GRD_FITTING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(grdCapturePath))
            {
                CaptureForm(form, grdCapturePath);
            }
            var titusIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == GrdPerformanceCatalog.TitusTbd80Code).index;
            fittingType.SelectedIndex = titusIndex;
            var titusLength = Find<ComboBox>(form, "FittingInput_length");
            titusLength.SelectedIndex = 1;
            Find<ComboBox>(form, "FittingInput_slotWidth").SelectedIndex = 1;
            Find<ComboBox>(form, "FittingInput_slotCount").SelectedIndex = 2;
            fittingAirflow.Text = "500";
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            var titusDetails = Find<RichTextBox>(form, "FittingCalculationDetails").Text;
            Require(PrivateField<Label>(form, "_fittingCoefficientValue").Text == "535 cfm"
                    && PrivateField<Label>(form, "_fittingVelocityValue").Text == "0.178 in. w.c."
                    && PrivateField<Label>(form, "_fittingVelocityPressureValue").Text == "NC 36"
                    && PrivateField<Label>(form, "_fittingPressureLossValue").Text == "25-30-43 ft"
                    && ShowsReportWorksheet(titusDetails, PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")),
                "The Titus TBD-80 D calculator should use length, slot size, and slot count; select the published inlet; and show the referenced performance chart.");
            var titusCalculation = PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")
                                   ?? throw new InvalidOperationException("The Titus calculation result was not retained.");
            var titusCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_TITUS_FITTING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(titusCapturePath))
            {
                CaptureForm(form, titusCapturePath);
            }
            fittingGroup.SelectedItem = FittingCatalog.VavSelectionGroup;
            Application.DoEvents();
            var vavReheatIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == VavPerformanceCatalog.WithReheatCode).index;
            fittingType.SelectedIndex = vavReheatIndex;
            Find<ComboBox>(form, "FittingInput_sizeIndex").SelectedIndex = 8;
            Find<ComboBox>(form, "FittingInput_rows").SelectedIndex = 0;
            fittingAirflow.Text = "850";
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            var vavDetails = Find<RichTextBox>(form, "FittingCalculationDetails").Text;
            var vavCalculation = PrivateField<FittingCalculationResult>(form, "_lastFittingCalculation")
                                 ?? throw new InvalidOperationException("The VAV calculation result was not retained.");
            Require(!fittingShape.Visible && !fittingDiameter.Visible
                    && PrivateField<Label>(form, "_fittingCoefficientValue").Text == "Size 14"
                    && PrivateField<Label>(form, "_fittingVelocityValue").Text.Contains("850", StringComparison.Ordinal)
                    && PrivateField<Label>(form, "_fittingVelocityPressureValue").Text == "1-row coil"
                    && PrivateField<Label>(form, "_fittingPressureLossValue").Text == "0.045 in. w.c."
                    && ShowsReportWorksheet(vavDetails, vavCalculation)
                    && vavDetails.Contains("AHRI 410-2001", StringComparison.Ordinal),
                "The Titus DESV VAV-with-reheat calculator should expose size, rows, airflow, interpolation, and manufacturer pressure drop without unrelated duct geometry inputs.");
            var vavPdfSections = (IReadOnlyList<DuctSystemPdfCalculationSection>)(UiMember.Method("BuildFittingPdfCalculations", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { vavCalculation }) ?? Array.Empty<DuctSystemPdfCalculationSection>());
            Require(vavPdfSections.Any(section => section.Heading == "PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)"
                                                   && section.Lines.Count == 10)
                    && vavPdfSections.Any(section => section.Heading == "INPUT / INTERPOLATION / SOLVED RESULT"
                                                   && section.Lines.Any(line => line.Formula.Contains("frac", StringComparison.Ordinal)))
                    && vavPdfSections.SelectMany(section => section.Lines)
                        .Any(line => line.Formula.Contains("AHRI 410-2001", StringComparison.Ordinal)),
                "The VAV report calculation should include its manufacturer source, published table, interpolation equation, and solved pressure drop.");
            // Round X: the reheat VAV's own terminal pressure drop is entered and added to the coil drop.
            var terminalDrop = Find<TextBox>(form, "FittingInput_" + VavPerformanceCatalog.TerminalDropKey);
            terminalDrop.Text = "0.1";
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            Require(PrivateField<Label>(form, "_fittingPressureLossValue").Text == "0.145 in. w.c.",
                $"The VAV calculator should add the entered terminal pressure drop to the coil drop. Shown={PrivateField<Label>(form, "_fittingPressureLossValue").Text}.");
            terminalDrop.Text = "0";
            fittingAirflow.Text = "1000";
            fittingGroup.SelectedItem = "Elbows";
            Application.DoEvents();
            Find<Button>(form, "CalculateFittingButton").PerformClick();
            Application.DoEvents();
            Require(fittingType.Items.Cast<object>().All(item => fittingType.GetItemText(item) == ((FittingDefinition)item).Code),
                "Each filtered ASHRAE fitting dropdown should display fitting numbers only.");
            Require(typeof(MainForm).Assembly.GetManifestResourceNames()
                    .Count(name => name.StartsWith("DuctSizer.WinForms.Assets.Fittings.", StringComparison.Ordinal)
                                   && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) == 103,
                "All 103 ASHRAE fitting images should be embedded in the standalone application.");
            var initialFitting = (FittingDefinition)fittingType.SelectedItem!;
            Require(fittingPreviewCaption.Text == $"{initialFitting.Code} — {initialFitting.Description}",
                "The fitting number and description should be populated above the selected image.");
            var longestDefinition = FittingCatalog.All.OrderByDescending(x => x.Description.Length).First();
            fittingGroup.SelectedItem = FittingCatalog.SelectionGroup(longestDefinition);
            Application.DoEvents();
            var longestFittingIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == longestDefinition.Code).index;
            fittingType.SelectedIndex = longestFittingIndex;
            Application.DoEvents();
            var longestCaptionMeasurement = TextRenderer.MeasureText(
                fittingPreviewCaption.Text,
                fittingPreviewCaption.Font,
                new Size(Math.Max(1, fittingPreviewCaption.ClientSize.Width - 8), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            Require(longestCaptionMeasurement.Height <= fittingPreviewCaption.ClientSize.Height - 4,
                "Long fitting numbers and descriptions should expand above the image without clipping.");
            fittingGroup.SelectedItem = "Elbows";
            fittingType.SelectedIndex = 0;
            Application.DoEvents();
            Require(fittingPreview.Image is { Width: >= 800, Height: >= 150 },
                "The selected fitting should display its complete embedded ASHRAE reference section.");
            Require(IsDescendantOf(fittingPreview, fittingBaseInputs),
                "The fitting image should be located in the fitting-input section.");
            var numberSelectorColumn = fittingSelectorPanel.Controls.Cast<Control>()
                .Single(control => IsDescendantOf(fittingType, control));
            var imageSelectorColumn = fittingSelectorPanel.Controls.Cast<Control>()
                .Single(control => IsDescendantOf(fittingPreview, control));
            Require(fittingSelectorPanel.GetPositionFromControl(numberSelectorColumn).Column == 0
                    && fittingSelectorPanel.GetPositionFromControl(imageSelectorColumn).Column == 1,
                "The fitting image should be positioned to the right of the number selector.");
            Require(fittingPreview.Cursor == Cursors.Hand && browseFittingImages.Text == "Browse Fitting Gallery",
                "The fitting input should expose click-to-enlarge and the visual fitting gallery.");
            Require(Math.Abs(browseFittingImages.Width - fittingPreview.Width) <= 2,
                "The Browse Fitting Gallery button should match the selected fitting image width.");
            var previewCaptionBottom = fittingSelectorPanel.PointToClient(
                fittingPreviewCaption.PointToScreen(new Point(0, fittingPreviewCaption.Height))).Y;
            var previewImageTop = fittingSelectorPanel.PointToClient(fittingPreview.PointToScreen(Point.Empty)).Y;
            Require(previewCaptionBottom <= previewImageTop && fittingPreview.Width >= 220
                    && !Descendants<Label>(form).Any(label => label.Text.Contains("FITTING IMAGE - CLICK TO ENLARGE", StringComparison.Ordinal)),
                $"The selected fitting description should appear above a larger image without redundant instruction text. " +
                $"Caption bottom={previewCaptionBottom}, image top={previewImageTop}, image width={fittingPreview.Width}.");
            var galleryType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.FittingGalleryDialog")
                              ?? throw new InvalidOperationException("Could not find the fitting gallery dialog.");
            using (var galleryForm = (Form?)Activator.CreateInstance(
                       galleryType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object?[] { FittingCatalog.All, initialFitting.Code },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the fitting gallery dialog."))
            {
                galleryForm.Show(form);
                Application.DoEvents();
                var galleryImage = Find<PictureBox>(galleryForm, $"FittingGalleryImage_{initialFitting.Code}");
                var galleryDescription = Find<Label>(galleryForm, $"FittingGalleryDescription_{initialFitting.Code}");
                var galleryNumber = Find<Label>(galleryForm, $"FittingGalleryNumber_{initialFitting.Code}");
                var galleryDescriptions = Descendants<Label>(galleryForm)
                    .Where(label => label.Name.StartsWith("FittingGalleryDescription_", StringComparison.Ordinal))
                    .ToArray();
                // 348 x 290 at 100 %, scaled with the display (round V: built after the window is scaled).
                var galleryScale = galleryForm.DeviceDpi / 96.0;
                Require(galleryImage.Image is { } galleryPicture
                        && Math.Abs(galleryPicture.Width - 348 * galleryScale) <= 2 && Math.Abs(galleryPicture.Height - 290 * galleryScale) <= 2,
                    $"Gallery fitting images should be 348 x 290 at 100 % display scale (scaled with the display); got {galleryImage.Image?.Size}.");
                Require(galleryDescriptions.Length == FittingCatalog.All.Count
                        && galleryDescription.Text == initialFitting.Description,
                    "Every fitting gallery card should show its ASHRAE description.");
                Require(Math.Abs(galleryDescription.Font.Size - 10.2F) < 0.1F && galleryDescription.Font.Bold,
                    "Gallery descriptions should use the reduced 10.2-point bold font.");
                Require(galleryDescription.Top < galleryImage.Top && galleryNumber.Top >= galleryImage.Bottom,
                    "Each gallery description should appear above its image, with the fitting number below.");
                var clippedGalleryDescriptions = galleryDescriptions.Where(label =>
                {
                    var measured = TextRenderer.MeasureText(
                        label.Text,
                        label.Font,
                        new Size(Math.Max(1, label.ClientSize.Width - 4), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    return measured.Height > label.ClientSize.Height;
                }).ToArray();
                Require(clippedGalleryDescriptions.Length == 0,
                    $"Fitting descriptions should fit their gallery cards without clipping. First clipped item: " +
                    $"{clippedGalleryDescriptions.FirstOrDefault()?.Name ?? "none"}.");
                var galleryFilter = Find<ComboBox>(galleryForm, "FittingGalleryGroupCombo");
                galleryFilter.SelectedItem = "Dampers";
                Application.DoEvents();
                var expectedDampers = FittingCatalog.All.Count(x => FittingCatalog.SelectionGroup(x) == "Dampers");
                Require(Descendants<PictureBox>(galleryForm).Count(image => image.Name.StartsWith("FittingGalleryImage_", StringComparison.Ordinal)) == expectedDampers,
                    "The fitting gallery group filter should limit the visible fitting images.");
                galleryFilter.SelectedItem = "Transitions";
                Application.DoEvents();
                var transitionImages = Descendants<PictureBox>(galleryForm)
                    .Where(image => image.Name.StartsWith("FittingGalleryImage_", StringComparison.Ordinal))
                    .Select(image => image.Name)
                    .ToArray();
                Require(transitionImages.Any(name => name.EndsWith("6-10", StringComparison.Ordinal))
                        && transitionImages.Any(name => name.EndsWith("6-11", StringComparison.Ordinal)),
                    "The Transitions gallery should include ASHRAE 6-10 and 6-11.");
                Require(galleryFilter.Items.Cast<object>().Any(item => string.Equals(
                            item?.ToString(), FittingCatalog.FanExitsAndPlenumsSelectionGroup, StringComparison.Ordinal))
                        && !galleryFilter.Items.Cast<object>().Any(item => string.Equals(item?.ToString(), "Others", StringComparison.Ordinal)),
                    "The gallery should label the former Others group as Fan Exits and Plenums.");
                galleryFilter.SelectedIndex = 0;
                Application.DoEvents();

                var galleryCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_GALLERY_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(galleryCapturePath))
                {
                    CaptureForm(galleryForm, galleryCapturePath);
                }
                galleryForm.Close();
            }
            var viewerType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.FittingImageViewer")
                             ?? throw new InvalidOperationException("Could not find the fitting image viewer.");
            var viewerFitting = FittingCatalog.All.Single(definition => definition.Code == "3-14");
            using (var viewerForm = (Form?)Activator.CreateInstance(
                       viewerType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object?[] { viewerFitting },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the fitting image viewer."))
            {
                viewerForm.Show(form);
                Application.DoEvents();
                var viewerHeader = Find<Label>(viewerForm, "FittingViewerHeader");
                var viewerHeaderMeasurement = TextRenderer.MeasureText(
                    viewerHeader.Text,
                    viewerHeader.Font,
                    new Size(Math.Max(1, viewerHeader.ClientSize.Width - viewerHeader.Padding.Horizontal), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                Require(viewerHeaderMeasurement.Height <= viewerHeader.ClientSize.Height - viewerHeader.Padding.Vertical,
                    "The fitting preview heading should resize to show every line without clipping.");

                var viewerCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_VIEWER_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(viewerCapturePath))
                {
                    CaptureForm(viewerForm, viewerCapturePath);
                }
                viewerForm.Close();
            }
            fittingType.SelectedIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == "3-1").index;
            Application.DoEvents();
            var radiusLabel = Find<Label>(form, "FittingInputLabel_rd");
            var diameterBounds = ScreenBounds(fittingDiameter);
            var temperatureBounds = ScreenBounds(fittingTemperature);
            var airflowBounds = ScreenBounds(fittingAirflow);
            var automationBounds = ScreenBounds(fittingAutomation);
            var radiusLabelBounds = ScreenBounds(radiusLabel);
            Require(fittingTemperature.Visible && fittingTemperatureLabel.Visible,
                "The air-temperature label and input should remain visible for round fittings.");
            Require(!temperatureBounds.IntersectsWith(airflowBounds)
                    && !ScreenBounds(fittingShape).IntersectsWith(temperatureBounds)
                    && !diameterBounds.IntersectsWith(ScreenBounds(fittingShape)),
                $"Airflow, temperature, reference shape, and geometry should form a compact nonoverlapping stack beside the fitting image. " +
                $"Airflow={airflowBounds}, temperature={temperatureBounds}, shape={ScreenBounds(fittingShape)}, diameter={diameterBounds}.");
            Require(!temperatureBounds.IntersectsWith(automationBounds)
                    && automationBounds.Bottom <= radiusLabelBounds.Top,
                "Temperature, calculation notice, and fitting-specific inputs should not overlap.");
            var radiusLabelMeasurement = TextRenderer.MeasureText(
                radiusLabel.Text,
                radiusLabel.Font,
                new Size(Math.Max(1, radiusLabel.ClientSize.Width - 6), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            Require(radiusLabelMeasurement.Height <= radiusLabel.ClientSize.Height,
                "Long fitting-input labels should wrap without being cut off.");
            Require(fittingShapeLabel.Text == "Reference Duct Shape",
                "Fitting input labels should use Title Case.");
            Require(fittingAirflow.Visible && fittingDiameter.Visible
                    && fittingShape.SelectedItem?.ToString() == "Round" && !fittingShape.Enabled,
                "Round-only fittings should infer and lock the round reference duct shape.");
            fittingType.SelectedIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == "3-5").index;
            Application.DoEvents();
            var rectangularAirflowBounds = ScreenBounds(fittingAirflow);
            var widthBounds = ScreenBounds(fittingWidth);
            var heightBounds = ScreenBounds(fittingHeight);
            Require(fittingShape.SelectedItem?.ToString() == "Rectangular" && !fittingShape.Enabled
                    && !fittingDiameter.Visible && fittingWidth.Visible && fittingHeight.Visible
                    && widthBounds.Top > rectangularAirflowBounds.Bottom
                    && !widthBounds.IntersectsWith(rectangularAirflowBounds)
                    && !heightBounds.IntersectsWith(rectangularAirflowBounds),
                "Rectangular-only fittings should infer rectangular geometry and keep width/height clear of the airflow row.");
            Require(fittingVelocity.Text.Contains("fpm", StringComparison.Ordinal),
                "The fitting output should calculate duct velocity from airflow and actual area.");
            fittingType.SelectedIndex = fittingType.Items.Cast<FittingDefinition>()
                .Select((definition, index) => (definition, index))
                .First(item => item.definition.Code == "3-1").index;
            Application.DoEvents();
            UiMember.Method("AddCurrentFittingToSystem", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Application.DoEvents();
            Require(grid.Rows.Count == 2, "Adding a fitting should create a second system row.");
            Require(Convert.ToString(grid.Rows[1].Cells["Id"].Value) == "F-001", "The first saved fitting ID should be F-001.");
            Require(Convert.ToString(grid.Rows[1].Cells["AirSystem"].Value) == Convert.ToString(grid.Rows[0].Cells["AirSystem"].Value)
                    && Convert.ToString(grid.Rows[1].Cells["BranchId"].Value) == Convert.ToString(grid.Rows[0].Cells["BranchId"].Value),
                "Ducts and fittings added manually should use the same air-system type and branch ID.");

            grid.Rows[1].Selected = true;
            grid.CurrentCell = grid.Rows[1].Cells["Id"];
            RaiseCellDoubleClick(grid, grid.Columns["Id"].Index, 1);
            Application.DoEvents();
            Require(grid.IsCurrentCellInEditMode, "Double-clicking a fitting ID should enter edit mode.");
            grid.CurrentCell.Value = "FIT-EXH-1";
            grid.EndEdit();
            Application.DoEvents();
            Require(Convert.ToString(grid.Rows[1].Cells["Id"].Value) == "FIT-EXH-1",
                "A fitting ID should be editable and saved from the system table.");

            grid.Rows[0].Selected = true;
            grid.CurrentCell = grid.Rows[0].Cells["Description"];
            RaiseCellDoubleClick(grid, grid.Columns["Description"].Index, 0);
            Application.DoEvents();
            Require(grid.IsCurrentCellInEditMode, "Double-clicking a description should enter edit mode.");
            grid.CurrentCell.Value = "Main supply";
            grid.EndEdit();
            Application.DoEvents();
            Require(Convert.ToString(grid.Rows[0].Cells["Description"].Value) == "Main supply", "Duct descriptions should be editable.");

            grid.Rows[0].Selected = true;
            grid.CurrentCell = grid.Rows[0].Cells[0];
            editSystemItem.PerformClick();
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "CalculatorTab", "Edit selected should return to the calculator tab.");
            Require(addDuct.Visible && addDuct.Text == "Save Duct Changes"
                    && Find<Label>(form, "DuctEditingLabel").Visible
                    && Find<Label>(form, "DuctEditingLabel").Text.Contains("D-001", StringComparison.Ordinal),
                "Editing a saved duct should expose Save Duct Changes and identify the selected duct.");
            Require(ScreenBounds(calculateButton).Top == ScreenBounds(addDuct).Top
                    && inputCard.RectangleToScreen(inputCard.ClientRectangle).Right - ScreenBounds(addDuct).Right <= 28,
                "Duct Calculate and Save Duct Changes should share the bottom-right action row of the left card.");
            addDuct.PerformClick();
            Application.DoEvents();
            Require(grid.Rows.Count == 2, "Updating a saved duct must not create a duplicate row or remove the fitting.");
            Require(Convert.ToString(grid.Rows[0].Cells["Id"].Value) == "D-001", "Updating a duct should preserve its ID.");

            var fittingRowForEdit = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                row.Tag?.GetType().Name == "SystemRowTag"
                && Convert.ToString(row.Cells["Id"].Value)?.StartsWith("FIT-", StringComparison.Ordinal) == true);
            fittingRowForEdit.Selected = true;
            grid.CurrentCell = fittingRowForEdit.Cells["StaticDrop"];
            editSystemItem.PerformClick();
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "FittingLossTab"
                    && addFitting.Visible && addFitting.Text == "Save Fitting Changes"
                    && Find<Label>(form, "FittingEditingLabel").Visible
                    && Find<Label>(form, "FittingEditingLabel").Text.Contains("FIT-EXH-1", StringComparison.Ordinal),
                "Opening an existing fitting should expose Save Fitting Changes and identify the selected fitting.");
            Require(ScreenBounds(calculateFitting).Top == ScreenBounds(addFitting).Top
                    && Find<Control>(form, "FittingInputCard").RectangleToScreen(
                        Find<Control>(form, "FittingInputCard").ClientRectangle).Right - ScreenBounds(addFitting).Right <= 28,
                "Fitting Calculate and Save Fitting Changes should share the bottom-right action row of the left card.");
            ClickButton(fittingNavigation);
            Application.DoEvents();
            Require(!addFitting.Visible && !Find<Label>(form, "FittingEditingLabel").Visible,
                "Opening Fitting Loss directly should clear the selected fitting and hide Save Fitting Changes.");

            tabs.SelectedTab = tabs.TabPages[1];
            fittingGroup.SelectedItem = "Diverting Fittings";
            Application.DoEvents();
            fittingType.SelectedIndex = fittingType.Items.Cast<object>()
                .Select((item, index) => (item, index))
                .First(x => x.item.ToString()?.StartsWith("5-24 ", StringComparison.Ordinal) == true).index;
            Application.DoEvents();
            var selectedFitting = (FittingDefinition)fittingType.SelectedItem!;
            Require(fittingPreviewCaption.Text == $"{selectedFitting.Code} — {selectedFitting.Description}",
                "Changing the selected fitting should refresh the number and ASHRAE description beneath the image.");
            Require(fittingPreview.Image is { Width: >= 800, Height: >= 150 },
                "Changing the selected fitting should refresh its embedded reference image.");
            Require(junctionInputs.Visible, "Junction fittings should show common, straight/main, and branch inputs.");
            Require(junctionPath.Items.Count == 2, "Junctions should allow branch or straight/main result selection.");
            var angleChoice = Find<ComboBox>(form, "FittingInput_angle");
            Require(angleChoice.Items.Count == 5, "Fitting 5-24 should show its five published branch angles as a dropdown.");
            angleChoice.SelectedIndex = 2;
            junctionPath.SelectedIndex = 0;
            Application.DoEvents();
            Require(fittingVelocity.Text.Contains("fpm", StringComparison.Ordinal),
                "A junction selection should calculate from the common-duct velocity pressure.");

            var systemField = UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)
                              ?? throw new InvalidOperationException("Could not inspect the local system model.");
            var document = (DuctSystemDocument)systemField.GetValue(form)!;
            SavedDuct mainDuct1() => document.Ducts.Single(x => x.Sequence == 1);
            var firstDuctRow = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                Convert.ToString(row.Cells["Id"].Value) == "D-001");
            firstDuctRow.Cells["DeltaX"].Value = "12";
            UiMember.Method("SaveEditedSystemCell", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { firstDuctRow.Index, grid.Columns["DeltaX"].Index });
            Application.DoEvents();
            Require(Math.Abs(document.Ducts.Single().DeltaXM - UnitConversions.FeetToM(12)) < 1e-12,
                "Directly editing a system-table coordinate should update the saved item.");
            var fanNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                GridX = 6, GridY = 16, AirSystemType = "Supply Air", BranchId = "Main",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(1200), TemperatureC = UnitConversions.FahrenheitToCelsius(70)
            };
            document.LayoutNodes.Add(fanNode);
            var coilNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Coil,
                GridX = 14, GridY = 16, ParentNodeId = fanNode.Id, AirSystemType = "Supply Air", BranchId = "Main",
                AirflowM3PerS = fanNode.AirflowM3PerS, TemperatureC = UnitConversions.FahrenheitToCelsius(55),
                PressureDropPa = UnitConversions.InchesWaterToPa(.25), ConnectionDuctSequence = 1
            };
            document.LayoutNodes.Add(coilNode);
            var filterNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 22, GridY = 16, ParentNodeId = coilNode.Id, AirSystemType = "Supply Air", BranchId = "Main",
                AirflowM3PerS = fanNode.AirflowM3PerS, PressureDropPa = UnitConversions.InchesWaterToPa(.10)
            };
            document.LayoutNodes.Add(filterNode);
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            ClickButton(layoutNavigation);
            form.Size = Sz(form, 1280, 820);
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "LayoutDesignerTab" && layoutCanvas.Visible,
                "The Layout Designer navigation button should open the graphical canvas.");
            var selectCanvasNode = layoutCanvas.GetType().GetMethod("SelectNode",
                                       BindingFlags.Instance | BindingFlags.NonPublic)
                                   ?? throw new InvalidOperationException("Could not select a Designer component.");
            selectCanvasNode.Invoke(layoutCanvas, new object?[] { coilNode });
            Application.DoEvents();
            Require(layoutSummaryGrid.SelectedRows.Count == 1
                    && Convert.ToString(layoutSummaryGrid.SelectedRows[0].Cells["Id"].Value) == coilNode.Id,
                $"Selecting a Designer component should highlight its matching quick-view row. " +
                $"Expected={coilNode.Id}, selected={string.Join(",", layoutSummaryGrid.SelectedRows.Cast<DataGridViewRow>().Select(row => Convert.ToString(row.Cells["Id"].Value)))}, " +
                $"rows={string.Join(",", layoutSummaryGrid.Rows.Cast<DataGridViewRow>().Select(row => Convert.ToString(row.Cells["Id"].Value)))}");
            var fanSummaryRow = layoutSummaryGrid.Rows.Cast<DataGridViewRow>().Single(row =>
                Convert.ToString(row.Cells["Id"].Value) == fanNode.Id);
            layoutSummaryGrid.ClearSelection();
            fanSummaryRow.Selected = true;
            layoutSummaryGrid.CurrentCell = fanSummaryRow.Cells[0];
            Application.DoEvents();
            var selectedNodeId = (string?)layoutCanvas.GetType().GetField("_selectedNodeId",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(layoutCanvas);
            Require(selectedNodeId == fanNode.Id,
                "Selecting a quick-view row should highlight the matching Designer component.");

            // Selection Inspector (v0.96 stage 2).
            var inspector = Find<Control>(form, "SelectionInspector");
            Require(ScreenBounds(inspector).Left >= ScreenBounds(layoutCanvasHost).Right - 2
                    && new[] { "InspectorDescriptionTab", "InspectorCalculationsTab", "InspectorNotesTab" }
                        .Select(name => Find<Button>(form, name).Text)
                        .SequenceEqual(new[] { "Description", "Calculations", "Notes" })
                    && Find<Label>(form, "InspectorTitle").Text.StartsWith(fanNode.Id, StringComparison.Ordinal)
                    && (string?)inspector.GetType().GetProperty("CurrentItemKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(inspector) == $"L:{fanNode.Sequence}",
                "Selecting an item should show it in the Selection Inspector on the right, with Description, Calculations and Notes tabs.");
            var inspectorDescription = Find<Control>(form, "InspectorDescriptionPage");
            Require(Descendants<Control>(inspectorDescription).Any(control => control.Name == "InspectorInputsSection")
                    && Descendants<Control>(inspectorDescription).Any(control => control.Name == "InspectorOutputsSection")
                    && Descendants<Control>(inspectorDescription).Any(control => control.Name == "InspectorUpdateButton")
                    && Descendants<Label>(inspectorDescription).Any(label => label.Name == "InspectorValue" && label.Text == fanNode.Id)
                    && !Descendants<TextBox>(inspectorDescription).Any(box => box.Text == fanNode.Id),
                "The Description tab should show fixed values as text, editable inputs in place, Update, and the outputs.");
            ClickButton(Find<Button>(form, "InspectorCalculationsTab"));
            Application.DoEvents();
            Require(Find<RichTextBox>(form, "InspectorCalculations").Text.Length > 20,
                "The Calculations tab should show the item's calculation (or say it is a manual entry).");
            ClickButton(Find<Button>(form, "InspectorNotesTab"));
            Application.DoEvents();
            var inspectorNotes = Find<TextBox>(form, "InspectorNotes");
            inspectorNotes.Text = "Confirm fan selection with the supplier.";
            Application.DoEvents();
            var notesModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(notesModel.ItemNotes.TryGetValue($"L:{fanNode.Sequence}", out var savedNote)
                    && savedNote == "Confirm fan selection with the supplier.",
                "Notes typed in the inspector should be kept with the item in the project.");
            inspector.GetType().GetMethod("SaveNotesNow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(inspector, null);
            ClickButton(Find<Button>(form, "InspectorDescriptionTab"));
            Application.DoEvents();
            ClickButton(Find<Button>(form, "InspectorCollapseButton"));
            Application.DoEvents();
            Require(Find<Button>(form, "InspectorReopenButton").Visible && inspector.Width <= 40,
                "The Selection Inspector should collapse to a narrow tab.");
            ClickButton(Find<Button>(form, "InspectorReopenButton"));
            Application.DoEvents();
            Require(inspector.Width >= 300 && !Find<Button>(form, "InspectorReopenButton").Visible,
                "The collapsed Selection Inspector should reopen.");

            // Project Explorer and the Errors / Validation lists (v0.96 stage 3).
            var viewOffsetField = layoutCanvas.GetType().GetField("_viewOffset", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var savedViewOffset = viewOffsetField.GetValue(layoutCanvas);
            var explorerTree = Find<TreeView>(form, "ProjectExplorerTree");
            var explorerGroups = explorerTree.Nodes.Cast<TreeNode>().ToArray();
            Require(explorerGroups.Select(node => node.Text.Split(" (")[0]).SequenceEqual(new[]
                    { "Equipment", "Sources", "Sinks", "Terminals", "Dampers", "Fittings", "Duct Segments", "Calculation Points" })
                    && explorerGroups.All(node => !node.IsExpanded)
                    && ScreenBounds(Find<Control>(form, "ProjectExplorer")).Right <= ScreenBounds(layoutCanvasHost).Left + 2,
                "The Project Explorer should list the system's items by kind in collapsed groups, left of the drawing.");
            var equipmentGroup = explorerGroups.Single(node => node.Name == "ExplorerGroupEquipment");
            var fanEntry = equipmentGroup.Nodes.Cast<TreeNode>().Single(node => node.Text.StartsWith(fanNode.Id + " ", StringComparison.Ordinal));
            layoutCanvas.GetType().GetMethod("SelectNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object?[] { null });
            Application.DoEvents();
            explorerTree.SelectedNode = fanEntry;
            Application.DoEvents();
            Require((string?)layoutCanvas.GetType().GetField("_selectedNodeId", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(layoutCanvas) == fanNode.Id
                    && Find<Label>(form, "InspectorTitle").Text.StartsWith(fanNode.Id, StringComparison.Ordinal),
                "Choosing an explorer entry should select it on the drawing and show it in the inspector.");
            var explorerSearch = Find<TextBox>(form, "ProjectExplorerSearch");
            explorerSearch.Text = fanNode.Id;
            Application.DoEvents();
            Require(explorerTree.Nodes.Count >= 1 && explorerTree.Nodes.Cast<TreeNode>().All(node => node.IsExpanded)
                    && explorerTree.Nodes.Cast<TreeNode>().SelectMany(node => node.Nodes.Cast<TreeNode>())
                        .All(node => node.Text.Contains(fanNode.Id, StringComparison.OrdinalIgnoreCase)),
                "Searching should filter the explorer to matching items and open their groups.");
            explorerSearch.Text = string.Empty;
            viewOffsetField.SetValue(layoutCanvas, savedViewOffset);
            layoutCanvas.Invalidate();
            Application.DoEvents();
            var errorsGrid = Find<DataGridView>(form, "LayoutErrorsGrid");
            var validationModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            ClickButton(Find<Button>(form, "LayoutValidationTab"));
            Application.DoEvents();
            // Round X: the former Validation list is a graph of gauge static pressure along each path.
            var netStaticChart = Find<Control>(form, "NetStaticPressureChart");
            var netStaticCombo = Find<ComboBox>(form, "NetStaticPathCombo");
            var expectedPaths = PathPressureProfiles.Build(validationModel);
            var chartPaths = (System.Collections.IList)netStaticChart.GetType().GetProperty("Paths", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(netStaticChart)!;
            Require((!form.Visible || netStaticChart.Visible) && form.Controls.Find("LayoutValidationGrid", true).Length == 0
                    && chartPaths.Count == expectedPaths.Count
                    && netStaticCombo.Items.Count == expectedPaths.Count + 1
                    && Convert.ToString(netStaticCombo.Items[0])!.StartsWith("All paths", StringComparison.Ordinal),
                $"The Net Path Static Pressure tab should graph every path, with All paths and each path in its Show list. Paths={chartPaths.Count}/{expectedPaths.Count}, items={netStaticCombo.Items.Count}.");
            if (netStaticCombo.Items.Count > 1)
            {
                netStaticCombo.SelectedIndex = 1;
                Application.DoEvents();
                Require((int?)netStaticChart.GetType().GetProperty("SelectedPath", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(netStaticChart) == 0,
                    "Choosing a path in the Show list should highlight that path (in blue).");
                netStaticCombo.SelectedIndex = 0;
            }
            // Round Y: Static / Total / Velocity check boxes show or hide each line; a point clicked selects that item on
            // the drawing and in the System Overview, and the item selected elsewhere is ringed on the graph.
            var staticCheck = Find<CheckBox>(form, "NetStaticStaticCheck");
            var totalCheck = Find<CheckBox>(form, "NetStaticTotalCheck");
            var velocityCheck = Find<CheckBox>(form, "NetStaticVelocityCheck");
            bool ChartFlag(string name) => (bool)netStaticChart.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(netStaticChart)!;
            Require(staticCheck.Checked && totalCheck.Checked && velocityCheck.Checked
                    && ChartFlag("ShowStatic") && ChartFlag("ShowTotal") && ChartFlag("ShowVelocity"),
                "The graph should start with the static, total and velocity pressure lines all shown.");
            velocityCheck.Checked = false;
            totalCheck.Checked = false;
            Require(!ChartFlag("ShowVelocity") && !ChartFlag("ShowTotal") && ChartFlag("ShowStatic"),
                "Clearing the Velocity and Total boxes should hide those lines.");
            velocityCheck.Checked = true;
            totalCheck.Checked = true;
            var clickedStation = expectedPaths.SelectMany(path => path.Stations).FirstOrDefault(station => station.ItemId.StartsWith("D-", StringComparison.Ordinal));
            if (clickedStation is not null)
            {
                var clickHandler = (Action<string>?)netStaticChart.GetType().GetField("ItemClicked", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(netStaticChart);
                clickHandler!.Invoke(clickedStation.ItemId);
                Application.DoEvents();
                var overview = Find<DataGridView>(form, "LayoutSystemSummaryGrid");
                Require(overview.SelectedRows.Count == 1 && Convert.ToString(overview.SelectedRows[0].Cells["Id"].Value) == clickedStation.ItemId
                        && (string?)netStaticChart.GetType().GetProperty("HighlightItemId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(netStaticChart) == clickedStation.ItemId,
                    $"Clicking {clickedStation.ItemId} on the graph should select it in the System Overview and ring it on the graph.");
            }
            viewOffsetField.SetValue(layoutCanvas, savedViewOffset); // selecting scrolled the drawing: put the view back
            layoutCanvas.Invalidate();
            UiMember.Method("RefreshLayoutBottomPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var issues = (System.Collections.IList)UiMember.Method("BuildProjectIssues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null)!;
            Require(errorsGrid.Rows.Count == issues.Count
                    && Find<Button>(form, "LayoutErrorsTab").Text == $"Errors ({issues.Count})",
                "The Errors tab should list every current problem and show the count.");
            ClickButton(Find<Button>(form, "LayoutOverviewTab"));
            Application.DoEvents();
            Require(layoutRearrange.Text == "Move" && Find<Button>(form, "RibbonSelectButton").Text == "Select"
                    && layoutRearrange.Parent == Find<Button>(form, "RibbonSelectButton").Parent,
                "The Project ribbon should offer Select and a visual-only Move mode side by side.");
            Require(string.IsNullOrEmpty(layoutUndo.Text) && string.IsNullOrEmpty(layoutRedo.Text)
                    && layoutUndo.AccessibleName == "Undo" && layoutRedo.AccessibleName == "Redo"
                    && layoutUndo.Width < 56 && layoutRedo.Width < 56,
                "Designer Undo and Redo should use compact, accessible icon buttons.");
            var canvasToolbar = Find<Control>(form, "LayoutCanvasToolbar");
            var zoomIn = Find<Button>(form, "LayoutZoomInButton");
            var zoomOut = Find<Button>(form, "LayoutZoomOutButton");
            Require(new Control[] { layoutUndo, layoutRedo, layoutDelete, zoomOut, zoomIn, layoutCenter, layoutPdfBackground,
                            layoutScale, layoutViewMode, layoutLabels, layoutFlow, layoutDuctSizes,
                            layoutSourceSinkEditor }
                        .All(control => control.Parent == canvasToolbar)
                    && canvasToolbar.Controls.IndexOf(layoutPdfBackground) < canvasToolbar.Controls.IndexOf(Find<Control>(form, "LayoutGridIcon"))
                    && form.Controls.Find("LayoutCoordinateOriginButton", true).Length == 0
                    && ((string?)layoutPdfBackground.GetType().GetProperty("Caption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layoutPdfBackground))!.Contains("PDF", StringComparison.Ordinal)
                    && ((string?)layoutSourceSinkEditor.GetType().GetProperty("Caption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layoutSourceSinkEditor))!.Contains("Source", StringComparison.Ordinal)
                    && canvasToolbar.Controls.IndexOf(Find<Control>(form, "LayoutGridIcon")) < canvasToolbar.Controls.IndexOf(layoutScale)
                    && layoutLabels.Text == "Labels" && layoutFlow.Text == "Flow" && layoutDuctSizes.Text == "Sizes"
                    && layoutLabels.Checked && !layoutFlow.Checked && !layoutDuctSizes.Checked
                    && layoutScale.DropDownStyle == ComboBoxStyle.DropDownList
                    && form.Controls.Find("LayoutScaleInput", true).Length == 0
                    && form.Controls.Find("LayoutShowToScaleCheckBox", true).Length == 0
                    && form.Controls.Find("ClearLayoutButton", true).Length == 0,
                "The canvas toolbar should hold undo/redo/delete, zoom buttons, PDF import left of the grid-scale dropdown, the view direction, and Labels/Flow/Sizes check boxes.");
            var zoomBefore = (float)layoutCanvas.GetType().GetProperty("ZoomFactor",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(layoutCanvas)!;
            ClickButton(zoomIn);
            Application.DoEvents();
            var zoomAfter = (float)layoutCanvas.GetType().GetProperty("ZoomFactor",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(layoutCanvas)!;
            ClickButton(zoomOut);
            Application.DoEvents();
            Require(zoomAfter > zoomBefore + 0.05F
                    && Math.Abs((float)layoutCanvas.GetType().GetProperty("ZoomFactor",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(layoutCanvas)! - zoomBefore) < 0.001F,
                "The zoom buttons should zoom in and back out (mouse-wheel zoom is unchanged).");
            Require(layoutAutoSize.Text == "Auto Size\nDuctwork" && Find<Button>(form, "RibbonCalculateButton").Text == "Calculate\nCritical Path"
                    && form.Controls.Find("LayoutSizeUndefinedButton", true).Length == 0
                    && form.Controls.Find("LayoutRecalculateDuctworkButton", true).Length == 0,
                "Auto Size Ductwork should replace Size Undefined Components and Recalculate All Ductwork; Calculate is labelled Calculate Critical Path (round Y).");
            Require(layoutPdfBackground.AccessibleName == "PDF underlay" && layoutPdfBackground.Visible,
                "The Designer should expose a visible PDF-underlay import action in its canvas toolbar.");
            var gridLocationMethod = layoutCanvas.GetType().GetMethod("GridLocation",
                                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                     ?? throw new InvalidOperationException("Could not inspect Designer grid locations.");
            var fanCanvasPoint = (Point)gridLocationMethod.Invoke(layoutCanvas, new object[] { fanNode.GridX, fanNode.GridY })!;
            Require(Math.Abs(fanCanvasPoint.X - layoutCanvas.ClientSize.Width / 2) <= 2
                    && Math.Abs(fanCanvasPoint.Y - layoutCanvas.ClientSize.Height / 2) <= 2,
                $"Opening Layout Designer should immediately center the viewport on the first system component. Fan at {fanCanvasPoint}, canvas {layoutCanvas.ClientSize}.");
            layoutViewMode.SelectedIndex = 1;
            Application.DoEvents();
            var viewModeProperty = layoutCanvas.GetType().GetProperty("ViewMode",
                                       BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                   ?? throw new InvalidOperationException("Could not inspect Designer view mode.");
            var coordinatePointMethod = layoutCanvas.GetType().GetMethod("CoordinatePoint",
                                            BindingFlags.Instance | BindingFlags.NonPublic)
                                        ?? throw new InvalidOperationException("Could not inspect Designer coordinate projection.");
            var negativeOrigin = (Point)coordinatePointMethod.Invoke(layoutCanvas,
                new object[] { new LayoutCoordinate(0, 0, 0) })!;
            var negativePositiveX = (Point)coordinatePointMethod.Invoke(layoutCanvas,
                new object[] { new LayoutCoordinate(UnitConversions.FeetToM(1), 0, 0) })!;
            var negativePositiveY = (Point)coordinatePointMethod.Invoke(layoutCanvas,
                new object[] { new LayoutCoordinate(0, UnitConversions.FeetToM(1), 0) })!;
            Require(viewModeProperty.GetValue(layoutCanvas)?.ToString() == "IsometricNegative"
                    && negativePositiveX.X > negativeOrigin.X && negativePositiveX.Y < negativeOrigin.Y
                    && negativePositiveY.X > negativeOrigin.X && negativePositiveY.Y > negativeOrigin.Y,
                "Isometric Negative should rotate the positive grid counterclockwise 90 degrees, placing +Y on former +X and +X on former -Y.");
            var unresolvedProjectionNode = new SavedLayoutNode { Sequence = 99991, GridX = 4, GridY = 2 };
            var visualLocationMethod = layoutCanvas.GetType().GetMethod("VisualLocation",
                                           BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                       ?? throw new InvalidOperationException("Could not inspect Designer node projection.");
            var unresolvedNegativeLocation = (Point)visualLocationMethod.Invoke(layoutCanvas,
                new object[] { unresolvedProjectionNode })!;
            var expectedUnresolvedNegativeLocation = (Point)gridLocationMethod.Invoke(layoutCanvas,
                new object[] { 2, -4 })!;
            Require(unresolvedNegativeLocation == expectedUnresolvedNegativeLocation,
                "Isometric Negative should rotate even an unconnected component's canonical grid position, not leave it at its positive-view screen location.");
            var negativeViewCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_NEGATIVE_VIEW_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(negativeViewCapturePath))
            {
                CaptureForm(form, negativeViewCapturePath);
            }
            layoutViewMode.SelectedIndex = 2;
            Application.DoEvents();
            var topDownOrigin = (Point)gridLocationMethod.Invoke(layoutCanvas, new object[] { 0, 0 })!;
            var topDownPositiveX = (Point)gridLocationMethod.Invoke(layoutCanvas, new object[] { 1, 1 })!;
            var topDownPositiveY = (Point)gridLocationMethod.Invoke(layoutCanvas, new object[] { -1, 1 })!;
            Require(viewModeProperty.GetValue(layoutCanvas)?.ToString() == "TopDown"
                    && topDownPositiveX.X > topDownOrigin.X && topDownPositiveX.Y == topDownOrigin.Y
                    && topDownPositiveY.X == topDownOrigin.X && topDownPositiveY.Y > topDownOrigin.Y,
                "Top-down mode should map positive X to the right and positive Y downward on the same editable grid.");
            layoutViewMode.SelectedIndex = 0;
            Application.DoEvents();
            Require(viewModeProperty.GetValue(layoutCanvas)?.ToString() == "Isometric",
                "The view control should switch back to the isometric Designer projection.");
            var viewOffsetProperty = layoutCanvas.GetType().GetProperty("ViewOffset",
                                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                     ?? throw new InvalidOperationException("Could not inspect Designer viewport offset.");
            layoutCenter.PerformClick();
            Application.DoEvents();
            fanCanvasPoint = (Point)gridLocationMethod.Invoke(layoutCanvas, new object[] { fanNode.GridX, fanNode.GridY })!;
            Require(Math.Abs(fanCanvasPoint.X - layoutCanvas.ClientSize.Width / 2) <= 2
                    && Math.Abs(fanCanvasPoint.Y - layoutCanvas.ClientSize.Height / 2) <= 2,
                "Center on Start should return the viewport to the first system component.");
            var existingLayoutNodes = document.LayoutNodes.ToArray();
            document.LayoutNodes.Clear();
            UiMember.Field("_layoutViewportInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(form, false);
            UiMember.Method("EnsureLayoutViewportPositioned", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { true });
            Application.DoEvents();
            var emptyOffset = (PointF)viewOffsetProperty.GetValue(layoutCanvas)!;
            Require(Math.Abs(emptyOffset.X) <= 1 && Math.Abs(emptyOffset.Y) <= 1,
                "An empty Designer should begin near the center of its larger canvas so it can be extended upward and leftward.");
            document.LayoutNodes.AddRange(existingLayoutNodes);
            UiMember.Field("_layoutViewportInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(form, false);
            UiMember.Method("EnsureLayoutViewportPositioned", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { true });
            Application.DoEvents();
            coilNode.DeltaXM = UnitConversions.FeetToM(18);
            ClickButton(systemNavigation);
            Application.DoEvents();
            Require(Math.Abs(document.Ducts.Single(x => x.Sequence == coilNode.ConnectionDuctSequence).DeltaXM
                             - UnitConversions.FeetToM(18)) < 1e-12,
                "Opening Duct System should synchronize linked rows from the previously edited Designer.");
            var synchronizedDuct = document.Ducts.Single(x => x.Sequence == coilNode.ConnectionDuctSequence);
            synchronizedDuct.AirSystemType = "Return Air";
            ClickButton(layoutNavigation);
            Application.DoEvents();
            Require(coilNode.AirSystemType == "Return Air",
                "Opening Layout Designer should synchronize linked component metadata from Duct System.");
            synchronizedDuct.AirSystemType = "Supply Air";
            coilNode.AirSystemType = "Supply Air";
            var fanCaption = (string)UiMember.Method("LayoutNodeCaption",
                                 BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { fanNode })!;
            Require(fanCaption == fanNode.Id && !fanCaption.Contains('\n'),
                "Designer components should display only their component ID.");
            var nodeCountBeforeUnconnectedPlacement = document.LayoutNodes.Count;
            UiMember.Method("PlaceLayoutNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { LayoutNodeType.Filter, 10, 16, null });
            var unconnectedPlacement = document.LayoutNodes.OrderByDescending(x => x.Sequence).First();
            Require(document.LayoutNodes.Count == nodeCountBeforeUnconnectedPlacement + 1
                    && unconnectedPlacement.Type == LayoutNodeType.Filter
                    && string.IsNullOrWhiteSpace(unconnectedPlacement.ParentNodeId)
                    && string.IsNullOrWhiteSpace(unconnectedPlacement.SecondaryParentNodeId)
                    && unconnectedPlacement.ConnectionDuctSequence is null
                    && unconnectedPlacement.HasAbsoluteCoordinateAnchor,
                "Placing a component directly on or near existing items must leave it unconnected until the user drags between connector circles.");
            document.LayoutNodes.Remove(unconnectedPlacement);
            Require(new[] { sourceSpaceTool, sinkSpaceTool, fanTool, coilTool, filterTool, customComponentTool }
                        .Concat(fittingTools).Concat(calculationPointTools)
                        .All(tool => tool.Visible && tool.Cursor == Cursors.Hand)
                    && form.Controls.Find("LayoutToolDuct", true).Length == 0,
                "The layout toolbox should expose source/sink spaces, equipment, grouped fittings, and four calculation points without a duct icon.");
            Require(ribbonComponentTools.Length == 20
                    && ribbonComponentTools.All(tool => tool.Height <= Px(40)
                        && (layoutToolTips.GetToolTip(tool) ?? "").Contains("\r\n", StringComparison.Ordinal)),
                $"The ribbon components should be compact captioned tools with names and explanations as hover help. Count={ribbonComponentTools.Length}.");
            Require(settingsButton.Visible && grid.AllowUserToOrderColumns
                    && grid.RowTemplate.Height <= Px(26) && grid.ColumnHeadersHeight <= Px(52)
                    && grid.Columns["Airflow"].ReadOnly == false
                    && grid.Columns["AirSystem"] is DataGridViewComboBoxColumn
                    && ((DataGridViewComboBoxColumn)grid.Columns["AirSystem"]).DisplayStyle
                        == DataGridViewComboBoxDisplayStyle.Nothing
                    && grid.Columns.Contains("DeltaX") && grid.Columns.Contains("DeltaY") && grid.Columns.Contains("DeltaZ")
                    && grid.Columns.Contains("AbsoluteX") && grid.Columns["AbsoluteX"].ReadOnly
                    && grid.Columns.Contains("AbsoluteY") && grid.Columns["AbsoluteY"].ReadOnly
                    && grid.Columns.Contains("AbsoluteZ") && grid.Columns["AbsoluteZ"].ReadOnly
                    && grid.Columns.Contains("PressureIn") && grid.Columns["PressureIn"].ReadOnly
                    && grid.Columns.Contains("PressureOut") && grid.Columns["PressureOut"].ReadOnly
                    && !grid.Columns["DeltaX"].ReadOnly && !grid.Columns["DeltaY"].ReadOnly
                    && !grid.Columns["DeltaZ"].ReadOnly,
                "Global settings, editable airflow/air-system fields, and X/Y/Z delta columns should be available.");
            Require(grid.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible)
                        .OrderBy(column => column.DisplayIndex).Take(12).Select(column => column.Name)
                        .SequenceEqual(new[]
                        {
                            "Id", "Description", "Airflow", "PressureIn", "PressureOut", "StaticOut", "AirSystem",
                            "Temperature", "PressureRate", "Velocity", "Length", "Size"
                        })
                    && grid.Columns["PressureIn"].HeaderText.StartsWith("Total pressure in", StringComparison.Ordinal)
                    && grid.Columns["StaticOut"].HeaderText.StartsWith("Static pressure out", StringComparison.Ordinal)
                    && grid.Columns["AirSystem"].HeaderText == "Air type",
                "The Duct System schedule should lead with the requested operating-data column order.");
            var formattedNegativeZero = (string)UiMember.Method("FormatSystemPressureValue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { -0.00001 })!;
            Require(formattedNegativeZero == "0" && !formattedNegativeZero.StartsWith("-", StringComparison.Ordinal),
                "Static pressure in/out formatting should normalize rounded negative zero to zero.");
            Require(layoutCriticalPath.Text.Replace("\n", " ", StringComparison.Ordinal) == "Highlight Critical Path"
                    && layoutCriticalPath.Height >= 32,
                "The Project ribbon should expose a fully visible Highlight Critical Path toggle.");
            var showLabelsProperty = layoutCanvas.GetType().GetProperty("ShowLabels",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var showDuctSizesProperty = layoutCanvas.GetType().GetProperty("ShowDuctSizes",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            layoutLabels.Checked = false;
            layoutDuctSizes.Checked = true;
            Application.DoEvents();
            Require(!(bool)showLabelsProperty.GetValue(layoutCanvas)!
                    && (bool)showDuctSizesProperty.GetValue(layoutCanvas)!,
                "The Designer should independently hide item labels and show duct-size annotations.");
            layoutDuctSizes.Checked = false;
            layoutFlow.Checked = true;
            Application.DoEvents();
            Require((bool)showDuctSizesProperty.GetValue(layoutCanvas)!,
                "The Flow check box should show duct captions (airflow) even with sizes off.");
            layoutFlow.Checked = false;
            layoutLabels.Checked = true;
            Application.DoEvents();
            Require((bool)showLabelsProperty.GetValue(layoutCanvas)! && !(bool)showDuctSizesProperty.GetValue(layoutCanvas)!,
                "Clearing Flow and Sizes should hide duct captions again.");
            ClickButton(layoutCriticalPath);
            Application.DoEvents();
            Require((bool)(layoutCanvas.GetType().GetProperty("HighlightCriticalPath",
                               BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                           .GetValue(layoutCanvas) ?? false)
                    && ((dynamic)layoutCriticalPath).Checked,
                "The ribbon critical-path toggle should enable route highlighting and show as on.");
            ClickButton(layoutCriticalPath);
            Application.DoEvents();
            Require(!(bool)(layoutCanvas.GetType().GetProperty("HighlightCriticalPath",
                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                            .GetValue(layoutCanvas) ?? true),
                "The Layout Designer critical-path action should toggle route highlighting off.");
            var firstAirSystemCell = (DataGridViewComboBoxCell)grid.Rows[0].Cells["AirSystem"];
            Require(firstAirSystemCell.DisplayStyle == DataGridViewComboBoxDisplayStyle.Nothing,
                "Air-system cells should not show a dropdown arrow until the cell is double-clicked.");
            grid.CurrentCell = firstAirSystemCell;
            RaiseCellDoubleClick(grid, grid.Columns["AirSystem"].Index, 0);
            Application.DoEvents();
            Require(firstAirSystemCell.DisplayStyle == DataGridViewComboBoxDisplayStyle.DropDownButton,
                "Double-clicking an air-system cell should reveal its dropdown control.");
            grid.EndEdit();
            grid.CurrentCell = grid.Rows[0].Cells["Id"];
            Application.DoEvents();
            Require(firstAirSystemCell.DisplayStyle == DataGridViewComboBoxDisplayStyle.Nothing,
                "Leaving an air-system cell should hide its dropdown arrow again.");
            var grdDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutEquipmentDialog")
                                ?? throw new InvalidOperationException("Could not find the equipment editor.");
            var grdNode = new SavedLayoutNode
            {
                Sequence = 999,
                Type = LayoutNodeType.Grd,
                GrdType = "Diffuser",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(450),
                PressureDropPa = UnitConversions.InchesWaterToPa(.08),
                DeltaXM = UnitConversions.FeetToM(12),
                AirSystemType = "Supply Air"
            };
            using (var grdDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { grdNode }, null)
                   ?? throw new InvalidOperationException("Could not create the GRD editor."))
            {
                var locationX = Find<TextBox>(grdDialog, "LayoutEquipmentDeltaXText");
                var locationY = Find<TextBox>(grdDialog, "LayoutEquipmentDeltaYText");
                var locationZ = Find<TextBox>(grdDialog, "LayoutEquipmentDeltaZText");
                Require(Find<ComboBox>(grdDialog, "LayoutGrdTypeCombo").Items.Count == 3
                        && !Find<ComboBox>(grdDialog, "LayoutGrdTypeCombo").Items.Cast<object>()
                            .Any(item => string.Equals(Convert.ToString(item), "Register", StringComparison.OrdinalIgnoreCase))
                        && Find<TextBox>(grdDialog, "LayoutEquipmentAirflowText").Text == "450"
                        && Find<TextBox>(grdDialog, "LayoutEquipmentPressureDropText").Text == "0.199"
                        && Find<ComboBox>(grdDialog, "LayoutGrdFaceSizeCombo").Items.Count == 3
                        && Find<ComboBox>(grdDialog, "LayoutGrdNeckDiameterCombo").Items.Count >= 3
                        && Find<ComboBox>(grdDialog, "LayoutGrdLengthCombo").Items.Count == 3
                        && Find<ComboBox>(grdDialog, "LayoutGrdSlotWidthCombo").Items.Count == 3
                        && Find<ComboBox>(grdDialog, "LayoutGrdSlotCountCombo").Items.Count == 4
                        && Find<ComboBox>(grdDialog, "LayoutGrdTitus350RSizeCombo").Items.Count == 78
                        && Find<Label>(grdDialog, "LayoutGrdPerformanceResult").Text.Contains("NC 36", StringComparison.Ordinal)
                        && locationX.Text == "12"
                        && locationX.Parent == locationY.Parent && locationY.Parent == locationZ.Parent
                        && locationX.Top == locationY.Top && locationY.Top == locationZ.Top
                        && Descendants<Label>(grdDialog).Any(label => label.Text == "Relative ΔX / ΔY / ΔZ (ft)")
                        && grdDialog.ClientSize.Width <= 520,
                    "The GRD popup should provide generic and manufacturer-table GRD choices, airflow, published selection inputs/results, pressure drop, air system, and coordinates.");
            }
            var reservoirNode = new SavedLayoutNode
            {
                Sequence = 1000,
                Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Source",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(1200),
                TemperatureC = UnitConversions.FahrenheitToCelsius(74),
                PressurePa = UnitConversions.InchesWaterToPa(.12),
                AirSystemType = "Return Air"
            };
            using (var reservoirDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { reservoirNode }, null)
                   ?? throw new InvalidOperationException("Could not create the space reservoir editor."))
            {
                Require(Find<ComboBox>(reservoirDialog, "LayoutSpaceRoleCombo").Items.Count == 2
                        && !Find<ComboBox>(reservoirDialog, "LayoutSpaceRoleCombo").Enabled
                        && Find<TextBox>(reservoirDialog, "LayoutEquipmentAirflowText").Text == "1200"
                        && Find<TextBox>(reservoirDialog, "LayoutEquipmentPressureDropText").Text == "0.12"
                        && Find<ComboBox>(reservoirDialog, "LayoutEquipmentAirSystemCombo").SelectedItem?.ToString() == "Return Air",
                    "The Space Source popup should provide airflow, temperature, pressure, and air-system inputs.");
            }
            var fanInputNode = new SavedLayoutNode
            {
                Sequence = 1001, Type = LayoutNodeType.Fan,
                AirflowM3PerS = UnitConversions.CfmToM3PerS(1500),
                TemperatureC = UnitConversions.FahrenheitToCelsius(70),
                FanStaticPressurePa = UnitConversions.InchesWaterToPa(1.5), FanEfficiencyPercent = 72.5
            };
            using (var fanDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { fanInputNode }, null)
                   ?? throw new InvalidOperationException("Could not create the fan editor."))
            {
                // Round V: the external static pressure is calculated (shown, not entered); internal static is entered.
                Require(Find<Label>(fanDialog, "LayoutExternalStaticLabel").Text.StartsWith("1.5 in. w.c. — calculated", StringComparison.Ordinal)
                        && Descendants<TextBox>(fanDialog).All(box => box.Name != "LayoutEquipmentPressureDropText")
                        && Find<TextBox>(fanDialog, "LayoutInternalStaticText").Text == "0"
                        && Find<TextBox>(fanDialog, "LayoutFanEfficiencyText").Text == "72.5"
                        && Descendants<Label>(fanDialog).Any(label => label.Text == "%")
                        && Descendants<Label>(fanDialog).Any(label => label.Text == "Internal Static Pressure (in. w.c.)"),
                    "The fan popup should show the calculated external static pressure and offer internal static pressure and percent-efficiency inputs.");
            }
            var customInputNode = new SavedLayoutNode
            {
                Sequence = 1002, Type = LayoutNodeType.CustomComponent,
                Description = "UV air cleaner", PressureDropPa = UnitConversions.InchesWaterToPa(.18)
            };
            using (var customDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { customInputNode }, null)
                   ?? throw new InvalidOperationException("Could not create the custom-component editor."))
            {
                Require(Find<TextBox>(customDialog, "LayoutCustomDescriptionText").Text == "UV air cleaner"
                        && Find<TextBox>(customDialog, "LayoutEquipmentPressureDropText").Text == "0.18"
                        && Descendants<Label>(customDialog).Any(label => label.Text == "Component Description")
                        && Descendants<Label>(customDialog).Any(label => label.Text.Contains("Static Pressure Drop", StringComparison.Ordinal)),
                    "The Custom Component popup should provide description and static-pressure-drop inputs.");
            }
            var negativeSource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Source", GridX = 80, GridY = 80,
                AirSystemType = "Return Air", BranchId = "Main",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(800),
                TemperatureC = UnitConversions.FahrenheitToCelsius(74),
                PressurePa = UnitConversions.InchesWaterToPa(-0.12)
            };
            var fanAfterNegativeSource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                ParentNodeId = negativeSource.Id, GridX = 88, GridY = 80,
                AirSystemType = "Return Air", BranchId = "Main",
                FanStaticPressurePa = UnitConversions.InchesWaterToPa(0.05)
            };
            var negativeDuctSequence = document.ReserveSequence();
            var negativeDuct = SavedDuct.FromResult(negativeDuctSequence, "Negative-pressure fan inlet",
                document.Ducts.First().Calculate(), "Return Air", "Main");
            negativeDuct.AirflowM3PerS = negativeSource.AirflowM3PerS;
            fanAfterNegativeSource.ConnectionDuctSequence = negativeDuctSequence;
            document.Ducts.Add(negativeDuct);
            document.LayoutNodes.AddRange(new[] { negativeSource, fanAfterNegativeSource });
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            var fanInletPressure = (double)(UiMember.Method("SystemInletPressureForNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { fanAfterNegativeSource }) ?? double.NaN);
            Require(fanInletPressure < 0
                    && Math.Abs(fanAfterNegativeSource.PressurePa
                                - (fanInletPressure + fanAfterNegativeSource.FanStaticPressurePa)) < 1e-9
                    && Math.Abs(fanAfterNegativeSource.AirflowM3PerS - negativeSource.AirflowM3PerS) < 1e-12,
                "A fan should accept negative upstream pressure, inherit airflow, and add its entered positive system static pressure at the outlet.");
            document.LayoutNodes.Remove(negativeSource);
            document.LayoutNodes.Remove(fanAfterNegativeSource);
            document.Ducts.Remove(negativeDuct);

            var synchronizedFitting = document.Fittings.Single();
            var synchronizedFlow = UnitConversions.CfmToM3PerS(1200);
            synchronizedFitting.AirflowM3PerS = synchronizedFlow;
            var synchronizedLoss = synchronizedFitting.Calculate().PressureLossPa;
            synchronizedFitting.AirflowM3PerS = UnitConversions.CfmToM3PerS(100);
            var pressureSource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Source", AirflowM3PerS = synchronizedFlow, AirflowUserSpecified = true,
                GridX = 82, GridY = 82, IsDefined = true
            };
            var pressureFitting = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Elbows", ParentNodeId = pressureSource.Id,
                LinkedFittingSequence = synchronizedFitting.Sequence, GridX = 86, GridY = 82, IsDefined = true
            };
            var pressureFan = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                ParentNodeId = pressureFitting.Id, FanStaticPressurePa = synchronizedLoss,
                GridX = 90, GridY = 82, IsDefined = true
            };
            var pressureSink = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Sink", ParentNodeId = pressureFan.Id,
                GridX = 94, GridY = 82, IsDefined = true
            };
            document.LayoutNodes.AddRange(new[] { pressureSource, pressureFitting, pressureFan, pressureSink });
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Require(double.IsNaN(pressureSink.PressurePa)
                    && Math.Abs(synchronizedFitting.AirflowM3PerS - synchronizedFlow) < 1e-12,
                "Pressures downstream of an unsized connection must be not available, never a zero-loss value.");
            var pressureDucts = new[] { pressureFitting, pressureFan, pressureSink }
                .Select(node => SizeConnection(document, node, document.Ducts.First())).ToArray();
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            // Round X: the drawn elbow takes the connected duct's size, and the bare duct into the space adds its exit
            // loss (one velocity pressure); the matching fan pressure closes the loop at zero.
            var lastDuctResult = pressureDucts[^1].Calculate();
            pressureFan.FanStaticPressurePa = synchronizedFitting.Calculate().PressureLossPa
                + pressureDucts.Sum(duct => duct.Calculate().TotalPressureLossUpperPa)
                + 0.5 * lastDuctResult.Air.DensityKgPerM3 * lastDuctResult.ActualVelocityMPerS * lastDuctResult.ActualVelocityMPerS;
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Require(Math.Abs(pressureSink.PressurePa) < 1e-8
                    && Math.Abs(synchronizedFitting.AirflowM3PerS - synchronizedFlow) < 1e-12,
                "Linked fitting airflow must synchronize before loss propagation so matching fan static closes the terminal gauge pressure to zero.");
            foreach (var node in new[] { pressureSource, pressureFitting, pressureFan, pressureSink })
                document.LayoutNodes.Remove(node);
            foreach (var duct in pressureDucts)
                document.Ducts.Remove(duct);

            var flowTemplate = document.Ducts.First().Calculate();
            var splitSource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                AirflowM3PerS = UnitConversions.CfmToM3PerS(1000), AirflowUserSpecified = true,
                GridX = 100, GridY = 100, IsDefined = true
            };
            var splitNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Diverting Fittings", ParentNodeId = splitSource.Id,
                GridX = 108, GridY = 100, IsDefined = true
            };
            var splitInlet = SavedDuct.FromResult(document.ReserveSequence(), "Split inlet", flowTemplate);
            splitInlet.AirflowM3PerS = splitSource.AirflowM3PerS;
            splitNode.ConnectionDuctSequence = splitInlet.Sequence;
            var splitMain = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = splitNode.Id, BranchId = "Main", GridX = 116, GridY = 100, IsDefined = true,
                ConnectionAirflowUserSpecified = true
            };
            var mainDuct = SavedDuct.FromResult(document.ReserveSequence(), "Split main", flowTemplate);
            mainDuct.AirflowM3PerS = UnitConversions.CfmToM3PerS(600);
            splitMain.ConnectionDuctSequence = mainDuct.Sequence;
            var splitBranch = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = splitNode.Id, BranchId = "B-900", GridX = 108, GridY = 92, IsDefined = true
            };
            var branchDuct = SavedDuct.FromResult(document.ReserveSequence(), "Split branch", flowTemplate);
            branchDuct.AirflowM3PerS = UnitConversions.CfmToM3PerS(1000);
            splitBranch.ConnectionDuctSequence = branchDuct.Sequence;
            document.LayoutNodes.AddRange(new[] { splitSource, splitNode, splitMain, splitBranch });
            document.Ducts.AddRange(new[] { splitInlet, mainDuct, branchDuct });

            var mergeSourceA = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                AirflowM3PerS = UnitConversions.CfmToM3PerS(300), AirflowUserSpecified = true,
                GridX = 100, GridY = 120, IsDefined = true
            };
            var mergeSourceB = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                AirflowM3PerS = UnitConversions.CfmToM3PerS(400), AirflowUserSpecified = true,
                GridX = 100, GridY = 128, IsDefined = true
            };
            var mergeNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Converging Fittings", ParentNodeId = mergeSourceA.Id,
                SecondaryParentNodeId = mergeSourceB.Id, GridX = 108, GridY = 124, IsDefined = true
            };
            var mergeDuctA = SavedDuct.FromResult(document.ReserveSequence(), "Merge inlet A", flowTemplate);
            var mergeDuctB = SavedDuct.FromResult(document.ReserveSequence(), "Merge inlet B", flowTemplate);
            mergeNode.ConnectionDuctSequence = mergeDuctA.Sequence;
            mergeNode.SecondaryConnectionDuctSequence = mergeDuctB.Sequence;
            var mergeOutlet = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = mergeNode.Id, GridX = 116, GridY = 124, IsDefined = true
            };
            var mergeOutletDuct = SavedDuct.FromResult(document.ReserveSequence(), "Merge outlet", flowTemplate);
            mergeOutlet.ConnectionDuctSequence = mergeOutletDuct.Sequence;
            document.LayoutNodes.AddRange(new[] { mergeSourceA, mergeSourceB, mergeNode, mergeOutlet });
            document.Ducts.AddRange(new[] { mergeDuctA, mergeDuctB, mergeOutletDuct });

            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Require(Math.Abs(UnitConversions.M3PerSToCfm(mainDuct.AirflowM3PerS) - 600) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(branchDuct.AirflowM3PerS) - 400) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(splitNode.AirflowM3PerS) - 1000) < .01,
                $"A diverting fitting should retain one defined outlet and calculate the other so both equal the inlet airflow. " +
                $"Actual main={UnitConversions.M3PerSToCfm(mainDuct.AirflowM3PerS):0.###}, " +
                $"branch={UnitConversions.M3PerSToCfm(branchDuct.AirflowM3PerS):0.###}, " +
                $"inlet={UnitConversions.M3PerSToCfm(splitNode.AirflowM3PerS):0.###}.");
            Require(Math.Abs(UnitConversions.M3PerSToCfm(mergeDuctA.AirflowM3PerS) - 300) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(mergeDuctB.AirflowM3PerS) - 400) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(mergeOutletDuct.AirflowM3PerS) - 700) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(mergeNode.AirflowM3PerS) - 700) < .01,
                "A converging fitting should automatically sum both inlet airflows into its outlet duct.");

            foreach (var flowNode in new[] { splitSource, splitNode, splitMain, splitBranch,
                         mergeSourceA, mergeSourceB, mergeNode, mergeOutlet })
                document.LayoutNodes.Remove(flowNode);
            foreach (var flowDuct in new[] { splitInlet, mainDuct, branchDuct, mergeDuctA, mergeDuctB, mergeOutletDuct })
                document.Ducts.Remove(flowDuct);
            var sinkNode = new SavedLayoutNode
            {
                Sequence = 1001,
                Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Sink",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(900),
                TemperatureC = UnitConversions.FahrenheitToCelsius(55),
                AirSystemType = "Supply Air"
            };
            using (var sinkDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { sinkNode }, null)
                   ?? throw new InvalidOperationException("Could not create the Space Sink editor."))
            {
                Require(!Find<TextBox>(sinkDialog, "LayoutEquipmentAirflowText").ReadOnly
                        && Descendants<TextBox>(sinkDialog).Any(text => text.Text == "55" && text.ReadOnly),
                    "The Space Sink should allow its design airflow to be edited while retaining calculated temperature as read-only.");
            }
            var boundarySource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Source", AirflowM3PerS = UnitConversions.CfmToM3PerS(900),
                AirflowUserSpecified = true, IsDefined = true
            };
            var boundaryMiddle = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = boundarySource.Id, IsDefined = true
            };
            var boundarySink = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Sink", ParentNodeId = boundaryMiddle.Id,
                AirflowM3PerS = UnitConversions.CfmToM3PerS(650), AirflowUserSpecified = true,
                IsDefined = true
            };
            var boundaryDuctA = SavedDuct.FromResult(document.ReserveSequence(), "Boundary A", flowTemplate);
            var boundaryDuctB = SavedDuct.FromResult(document.ReserveSequence(), "Boundary B", flowTemplate);
            boundaryDuctA.AirflowM3PerS = UnitConversions.CfmToM3PerS(900);
            boundaryDuctB.AirflowM3PerS = UnitConversions.CfmToM3PerS(900);
            boundaryMiddle.ConnectionDuctSequence = boundaryDuctA.Sequence;
            boundarySink.ConnectionDuctSequence = boundaryDuctB.Sequence;
            document.LayoutNodes.AddRange(new[] { boundarySource, boundaryMiddle, boundarySink });
            document.Ducts.AddRange(new[] { boundaryDuctA, boundaryDuctB });
            UiMember.Method("ApplyReservoirAirflowChange", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { boundarySink });
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Require(Math.Abs(UnitConversions.M3PerSToCfm(boundaryDuctA.AirflowM3PerS) - 650) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(boundaryDuctB.AirflowM3PerS) - 650) < .01
                    && Math.Abs(UnitConversions.M3PerSToCfm(boundarySource.AirflowM3PerS) - 650) < .01
                    && boundaryDuctA.RequiresResizing && boundaryDuctB.RequiresResizing,
                "Changing a Space Sink airflow should set the source to total sink demand, propagate upstream, and mark connected ductwork for recalculation.");
            var sourceSinkEditorType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.SourceSinkEditorDialog")
                                       ?? throw new InvalidOperationException("Could not find the Source and Sink Editor.");
            using (var sourceSinkEditor = (Form?)Activator.CreateInstance(sourceSinkEditorType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { new[] { boundarySource, boundarySink } }, null)
                   ?? throw new InvalidOperationException("Could not create the Source and Sink Editor."))
            {
                sourceSinkEditor.Show(form);
                Application.DoEvents();
                var status = Find<Label>(sourceSinkEditor, "SourceSinkBalanceStatusLabel");
                var saveFlows = Find<Button>(sourceSinkEditor, "SourceSinkSaveButton");
                var sourceInput = Find<TextBox>(sourceSinkEditor, $"SourceSinkAirflow_{boundarySource.Sequence}");
                var sinkInput = Find<TextBox>(sourceSinkEditor, $"SourceSinkAirflow_{boundarySink.Sequence}");
                var sourcePressure = Find<Label>(sourceSinkEditor, $"SourceSinkPressure_{boundarySource.Sequence}");
                var sinkPressure = Find<Label>(sourceSinkEditor, $"SourceSinkPressure_{boundarySink.Sequence}");
                sourceInput.Text = "700";
                sinkInput.Text = "650";
                Application.DoEvents();
                Require(!saveFlows.Enabled && status.ForeColor == Color.Firebrick
                        && status.Text.Contains("do not balance", StringComparison.OrdinalIgnoreCase)
                        && ScreenBounds(status).Top < ScreenBounds(sourceInput).Top
                        && sourcePressure.Text.EndsWith("in. w.c.", StringComparison.Ordinal)
                        && sinkPressure.Text.EndsWith("in. w.c.", StringComparison.Ordinal),
                    "The Source and Sink Editor should flag mismatched net airflow in red and prevent saving.");
                sinkInput.Text = "700";
                Application.DoEvents();
                Require(saveFlows.Enabled && status.ForeColor != Color.Firebrick
                        && sourceInput.ReadOnly == false && sinkInput.ReadOnly == false,
                    "Balanced source and sink values should be editable and saveable from the boundary-flow editor.");
            }
            document.LayoutNodes.Remove(boundarySource);
            document.LayoutNodes.Remove(boundaryMiddle);
            document.LayoutNodes.Remove(boundarySink);
            document.Ducts.Remove(boundaryDuctA);
            document.Ducts.Remove(boundaryDuctB);
            var coilAbsolute = LayoutCoordinateResolver.Resolve(document)[coilNode.Sequence];
            using (var connectedEquipmentDialog = (Form?)Activator.CreateInstance(grdDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[]
                       {
                           coilNode, document.LayoutNodes,
                           (Func<SavedLayoutNode, string>)(item => item.Id), coilAbsolute
                       }, null)
                   ?? throw new InvalidOperationException("Could not create the connected component editor."))
            {
                var coordinateInputs = new[]
                {
                    Find<TextBox>(connectedEquipmentDialog, "LayoutEquipmentDeltaXText"),
                    Find<TextBox>(connectedEquipmentDialog, "LayoutEquipmentDeltaYText"),
                    Find<TextBox>(connectedEquipmentDialog, "LayoutEquipmentDeltaZText")
                };
                Require(coordinateInputs.All(input => !input.ReadOnly)
                        && Descendants<Label>(connectedEquipmentDialog)
                            .Any(label => label.Text == "Absolute position X / Y / Z (ft)"),
                    "A connected component editor should expose editable absolute X, Y, and Z coordinates.");
            }
            var placementDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutComponentPlacementDialog")
                                      ?? throw new InvalidOperationException("Could not find the location-only placement dialog.");
            var undefinedPlacementNode = new SavedLayoutNode
            {
                Sequence = 1002, Type = LayoutNodeType.Fitting, FittingGroup = "Elbows", IsDefined = false,
                DeltaXM = UnitConversions.FeetToM(5), DeltaYM = UnitConversions.FeetToM(10)
            };
            using (var placementDialog = (Form?)Activator.CreateInstance(placementDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { undefinedPlacementNode }, null)
                   ?? throw new InvalidOperationException("Could not create the location-only placement dialog."))
            {
                var placementInputs = Descendants<TextBox>(placementDialog).ToArray();
                Require(placementDialog.Text == "Confirm Relative Coordinates"
                        && placementInputs.Length == 3
                        && Descendants<ComboBox>(placementDialog).Count() == 0
                        && Descendants<Label>(placementDialog).Any(label => label.Text == "Relative ΔX / ΔY / ΔZ (ft)")
                        && undefinedPlacementNode.AirflowM3PerS == 0
                        && !undefinedPlacementNode.IsDefined,
                    "Initial component placement should request only X/Y/Z location and leave the component undefined without airflow or sizing data.");
                var placementCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_PLACEMENT_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(placementCapturePath))
                {
                    placementDialog.Show(form);
                    Application.DoEvents();
                    CaptureForm(placementDialog, placementCapturePath);
                    placementDialog.Hide();
                }
            }
            using (var constrainedPlacementDialog = (Form?)Activator.CreateInstance(placementDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { undefinedPlacementNode, fanNode.Id, UnitSystem.Imperial, "X" }, null)
                   ?? throw new InvalidOperationException("Could not create the duct-insertion placement dialog."))
            {
                var xInput = Find<TextBox>(constrainedPlacementDialog, "LayoutPlacementDeltaXText");
                var yInput = Find<TextBox>(constrainedPlacementDialog, "LayoutPlacementDeltaYText");
                var zInput = Find<TextBox>(constrainedPlacementDialog, "LayoutPlacementDeltaZText");
                Require(!xInput.ReadOnly && yInput.ReadOnly && zInput.ReadOnly,
                    "Inserting a component on an X-axis duct should permit only the X coordinate to be adjusted.");
            }
            undefinedPlacementNode.ParentNodeId = fanNode.Id;
            undefinedPlacementNode.DeltaYM = 0;
            undefinedPlacementNode.ConnectionDeltaConfirmed = true;
            undefinedPlacementNode.GridX = fanNode.GridX + 4;
            undefinedPlacementNode.GridY = fanNode.GridY + 4;
            document.LayoutNodes.Add(undefinedPlacementNode);
            var ductCountBeforeUndefinedPlacement = document.Ducts.Count;
            UiMember.Method("EnsureInitialConnectionDuct", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { undefinedPlacementNode, fanNode, false });
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            Require(document.Ducts.Count == ductCountBeforeUndefinedPlacement
                    && undefinedPlacementNode.ConnectionDuctSequence is null
                    && !grid.Rows.Cast<DataGridViewRow>().Any(row =>
                        string.Equals(Convert.ToString(row.Cells["Id"].Value), undefinedPlacementNode.Id, StringComparison.OrdinalIgnoreCase)),
                "An undefined newly placed component should remain off the populated schedule and should not automatically size its connection duct.");
            ClickButton(layoutAutoSize);
            Application.DoEvents();
            Require(undefinedPlacementNode.IsDefined
                    && undefinedPlacementNode.LinkedFittingSequence is not null
                    && undefinedPlacementNode.ConnectionDuctSequence is not null
                    && document.Ducts.Single(x => x.Sequence == undefinedPlacementNode.ConnectionDuctSequence).LengthM > 0
                    && document.Ducts.Single(x => x.Sequence == undefinedPlacementNode.ConnectionDuctSequence).AirflowM3PerS > 0,
                "Size Undefined Components should apply the saved elbow default and create a graph-length duct using connected airflow.");
            if (undefinedPlacementNode.LinkedFittingSequence is int automaticFittingSequence)
                document.Fittings.RemoveAll(x => x.Sequence == automaticFittingSequence);
            if (undefinedPlacementNode.ConnectionDuctSequence is int automaticDuctSequence)
                document.Ducts.RemoveAll(x => x.Sequence == automaticDuctSequence);
            document.LayoutNodes.Remove(undefinedPlacementNode);
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            layoutCanvas.GetType().GetMethod("SelectNode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                .Invoke(layoutCanvas, new object?[] { null });
            UiMember.Method("ClearLayoutHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var mouseWheelMethod = layoutCanvas.GetType().GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic)
                                   ?? throw new InvalidOperationException("Could not inspect Designer mouse-wheel zoom.");
            mouseWheelMethod.Invoke(layoutCanvas, new object[]
            {
                new MouseEventArgs(MouseButtons.None, 0, layoutCanvas.Width / 2, layoutCanvas.Height / 2, 120)
            });
            Application.DoEvents();
            Require(zoomValue.Text == "110%", "Scrolling the mouse wheel up should enlarge the Designer grid view.");
            mouseWheelMethod.Invoke(layoutCanvas, new object[]
            {
                new MouseEventArgs(MouseButtons.None, 0, layoutCanvas.Width / 2, layoutCanvas.Height / 2, -120)
            });
            Application.DoEvents();
            Require(zoomValue.Text == "100%", "Scrolling the mouse wheel down should restore the Designer grid view.");
            var originalFanDeltaX = fanNode.DeltaXM;
            layoutScale.SelectedItem = layoutScale.Items.Cast<object>().Single(item => item.ToString()!.StartsWith("2 ft", StringComparison.Ordinal));
            Application.DoEvents();
            Require(document.LayoutShowToScale && Math.Abs(document.LayoutFeetPerGridCell - 2.0) < 1e-9
                    && Math.Abs(fanNode.DeltaXM - originalFanDeltaX) < 1e-12,
                "Grid scale should persist feet-per-grid-cell while leaving engineering coordinates unchanged.");
            var scaledPlacement = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = fanNode.GridX + 4, GridY = fanNode.GridY + 2
            };
            UiMember.Method("SetDefaultNodeDeltas", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { scaledPlacement, fanNode });
            Require(Math.Abs(UnitConversions.MToFeet(scaledPlacement.DeltaXM) - 6.0) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(scaledPlacement.DeltaYM) + 2.0) < 1e-9
                    && Math.Abs(scaledPlacement.DeltaZM) < 1e-12,
                "Scaled component placement should prefill relative X/Y from the selected grid point and always default Z to zero.");
            var sameXAxisPlacement = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = fanNode.GridX + 4, GridY = fanNode.GridY + 4
            };
            UiMember.Method("SetDefaultNodeDeltas", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { sameXAxisPlacement, fanNode });
            Require(Math.Abs(UnitConversions.MToFeet(sameXAxisPlacement.DeltaXM) - 8.0) < 1e-9
                    && Math.Abs(sameXAxisPlacement.DeltaYM) < 1e-12,
                "Moving along one isometric X grid line should modify only the X coordinate.");
            var sameYAxisPlacement = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = fanNode.GridX - 4, GridY = fanNode.GridY + 4
            };
            UiMember.Method("SetDefaultNodeDeltas", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { sameYAxisPlacement, fanNode });
            Require(Math.Abs(sameYAxisPlacement.DeltaXM) < 1e-12
                    && Math.Abs(UnitConversions.MToFeet(sameYAxisPlacement.DeltaYM) - 8.0) < 1e-9,
                "Moving along one isometric Y grid line should modify only the Y coordinate.");
            var priorCoordinateOrigin = document.CoordinateOriginNodeId;
            var priorCoordinateX = document.CoordinateOriginXM;
            var priorCoordinateY = document.CoordinateOriginYM;
            var priorCoordinateZ = document.CoordinateOriginZM;
            var insertionParent = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 80, GridY = 80, IsDefined = true
            };
            var insertionDownstream = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = insertionParent.Id, DeltaXM = UnitConversions.FeetToM(20),
                GridX = 90, GridY = 90, ConnectionDeltaConfirmed = true, IsDefined = true
            };
            var insertionNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Elbows", GridX = 5, GridY = 5
            };
            document.LayoutNodes.Add(insertionParent);
            document.LayoutNodes.Add(insertionDownstream);
            document.CoordinateOriginNodeId = insertionParent.Id;
            document.CoordinateOriginXM = document.CoordinateOriginYM = document.CoordinateOriginZM = 0;
            var layoutConnectionRefType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutConnectionRef")
                                          ?? throw new InvalidOperationException("Could not find the layout connection reference type.");
            var insertionAxis = (string)UiMember.Method("SetInsertionNodeDeltas",
                                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { insertionNode, insertionParent, insertionDownstream })!;
            Require(insertionAxis == "X"
                    && Math.Abs(UnitConversions.MToFeet(insertionNode.DeltaXM) - 10.0) < 1e-9
                    && Math.Abs(insertionNode.DeltaYM) < 1e-12
                    && Math.Abs(insertionNode.DeltaZM) < 1e-12,
                "Dropping onto an X-axis duct should infer the absolute midpoint, preserve Z, and expose only X movement.");
            var inheritedDuct = new SavedDuct
            {
                Sequence = document.ReserveSequence(),
                Description = $"Designer duct {insertionParent.Id} to {insertionDownstream.Id}",
                AirSystemType = "Supply Air",
                BranchId = "Main",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(1350),
                TemperatureC = UnitConversions.FahrenheitToCelsius(58),
                RelativeHumidity = 0.45,
                AtmosphericPressurePa = 99_500,
                Shape = DuctShape.Rectangular,
                WidthM = UnitConversions.InchesToM(24),
                HeightM = UnitConversions.InchesToM(12),
                MaterialName = "Galvanized Rectangular Duct",
                RoughnessFeet = 0.0005,
                EquivalentDiameterMethod = EquivalentDiameterMethod.Huebscher,
                PressureLossBasis = PressureLossBasis.ActualDuctArea,
                LengthM = UnitConversions.FeetToM(20),
                DeltaXM = UnitConversions.FeetToM(20)
            };
            document.Ducts.Add(inheritedDuct);
            insertionDownstream.ConnectionDuctSequence = inheritedDuct.Sequence;
            insertionNode.ParentNodeId = insertionParent.Id;
            insertionNode.ConnectionDeltaConfirmed = true;
            insertionNode.AirflowM3PerS = inheritedDuct.AirflowM3PerS;
            insertionNode.TemperatureC = inheritedDuct.TemperatureC;
            document.LayoutNodes.Add(insertionNode);
            var insertionConnection = Activator.CreateInstance(layoutConnectionRefType,
                                          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                          null, new object[] { insertionDownstream, false }, null)
                                      ?? throw new InvalidOperationException("Could not create a layout connection reference.");
            UiMember.Method("InsertLayoutNodeIntoConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new[] { insertionNode, insertionConnection });
            var firstSplitDuct = document.Ducts.Single(x => x.Sequence == insertionNode.ConnectionDuctSequence);
            var secondSplitDuct = document.Ducts.Single(x => x.Sequence == insertionDownstream.ConnectionDuctSequence);
            Require(insertionDownstream.ParentNodeId == insertionNode.Id
                    && firstSplitDuct.Sequence == inheritedDuct.Sequence
                    && secondSplitDuct.Sequence != inheritedDuct.Sequence
                    && firstSplitDuct.Shape == inheritedDuct.Shape && secondSplitDuct.Shape == inheritedDuct.Shape
                    && firstSplitDuct.MaterialName == inheritedDuct.MaterialName
                    && secondSplitDuct.MaterialName == inheritedDuct.MaterialName
                    && firstSplitDuct.EquivalentDiameterMethod == inheritedDuct.EquivalentDiameterMethod
                    && secondSplitDuct.PressureLossBasis == inheritedDuct.PressureLossBasis
                    && Math.Abs(firstSplitDuct.TemperatureC - inheritedDuct.TemperatureC) < 1e-12
                    && Math.Abs(firstSplitDuct.AirflowM3PerS - inheritedDuct.AirflowM3PerS) < 1e-12
                    && Math.Abs(secondSplitDuct.AirflowM3PerS - inheritedDuct.AirflowM3PerS) < 1e-12
                    && Math.Abs(UnitConversions.MToFeet(firstSplitDuct.LengthM) - 10) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(secondSplitDuct.LengthM) - 10) < 1e-9
                    && firstSplitDuct.RequiresResizing && secondSplitDuct.RequiresResizing,
                "Explicit duct insertion should split the run at the selected grid point and inherit its airflow, temperature, geometry, material, and calculation basis into both updated-length segments.");
            var insertedUpstreamConnection = Activator.CreateInstance(layoutConnectionRefType,
                                                 BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                 null, new object[] { insertionNode, false }, null)
                                             ?? throw new InvalidOperationException("Could not create the inserted upstream connection reference.");
            var autoSizeConnectionDuct = UiMember.Method("AutoSizeConnectionDuct",
                                             BindingFlags.Instance | BindingFlags.NonPublic)
                                         ?? throw new InvalidOperationException("Could not find connection duct auto-sizing.");
            autoSizeConnectionDuct.Invoke(form, new[] { insertedUpstreamConnection });
            autoSizeConnectionDuct.Invoke(form, new[] { insertionConnection });
            Require(!firstSplitDuct.RequiresResizing && !secondSplitDuct.RequiresResizing,
                "Both inherited split segments should remain editable and clear their re-sizing flag after recalculation.");
            document.LayoutNodes.Remove(insertionNode);
            document.Ducts.Remove(firstSplitDuct);
            document.Ducts.Remove(secondSplitDuct);
            document.Ducts.Add(inheritedDuct);
            insertionDownstream.ParentNodeId = insertionParent.Id;
            insertionDownstream.ConnectionDuctSequence = inheritedDuct.Sequence;
            insertionDownstream.DeltaXM = UnitConversions.FeetToM(20);
            insertionDownstream.DeltaYM = insertionDownstream.DeltaZM = 0;
            var coordinateMiddle = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = insertionParent.Id, DeltaXM = UnitConversions.FeetToM(10),
                ConnectionDeltaConfirmed = true, IsDefined = true
            };
            var coordinateChild = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Coil,
                ParentNodeId = coordinateMiddle.Id, DeltaXM = UnitConversions.FeetToM(10),
                ConnectionDeltaConfirmed = true, IsDefined = true
            };
            document.LayoutNodes.AddRange(new[] { coordinateMiddle, coordinateChild });
            UiMember.Method("ApplyAbsoluteCoordinateEdit", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[]
                {
                    coordinateMiddle,
                    new LayoutCoordinate(UnitConversions.FeetToM(10), 0, 0),
                    new LayoutCoordinate(UnitConversions.FeetToM(12), 0, UnitConversions.FeetToM(5))
                });
            var shiftedCoordinates = LayoutCoordinateResolver.Resolve(document);
            Require(Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[insertionParent.Sequence].ZM) - 5) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[insertionDownstream.Sequence].ZM) - 5) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[coordinateMiddle.Sequence].XM) - 12) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[coordinateMiddle.Sequence].ZM) - 5) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[coordinateChild.Sequence].XM) - 20) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(shiftedCoordinates[coordinateChild.Sequence].ZM) - 5) < 1e-9,
                "Editing a component on a straight X run should preserve downstream X positions, while a Z edit translates every connected upstream and downstream component together.");
            var resolvedCoordinateField = layoutCanvas.GetType().GetField("_resolvedCoordinates",
                                              BindingFlags.Instance | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("Could not inspect resolved Designer coordinates.");
            resolvedCoordinateField.SetValue(layoutCanvas, shiftedCoordinates);
            var childVisualBeforeRemoval = (Point)visualLocationMethod.Invoke(layoutCanvas,
                new object[] { coordinateChild })!;
            var parentBeforeRemoval = shiftedCoordinates[insertionParent.Sequence];
            UiMember.Method("RemoveLayoutNodePreservingCoordinates",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { coordinateMiddle });
            var coordinatesAfterComponentRemoval = LayoutCoordinateResolver.Resolve(document);
            resolvedCoordinateField.SetValue(layoutCanvas, coordinatesAfterComponentRemoval);
            var childVisualAfterComponentRemoval = (Point)visualLocationMethod.Invoke(layoutCanvas,
                new object[] { coordinateChild })!;
            Require(!document.LayoutNodes.Contains(coordinateMiddle)
                    && string.IsNullOrWhiteSpace(coordinateChild.ParentNodeId)
                    && Math.Abs(UnitConversions.MToFeet(coordinatesAfterComponentRemoval[coordinateChild.Sequence].XM) - 20) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(coordinatesAfterComponentRemoval[coordinateChild.Sequence].ZM) - 5) < 1e-9
                    && coordinatesAfterComponentRemoval[insertionParent.Sequence] == parentBeforeRemoval
                    && childVisualAfterComponentRemoval == childVisualBeforeRemoval,
                "Removing a connected component should leave every surviving upstream and downstream absolute and visual coordinate unchanged.");
            coordinateChild.ParentNodeId = insertionParent.Id;
            coordinateChild.DeltaXM = coordinatesAfterComponentRemoval[coordinateChild.Sequence].XM
                                      - coordinatesAfterComponentRemoval[insertionParent.Sequence].XM;
            coordinateChild.DeltaYM = coordinatesAfterComponentRemoval[coordinateChild.Sequence].YM
                                      - coordinatesAfterComponentRemoval[insertionParent.Sequence].YM;
            coordinateChild.DeltaZM = coordinatesAfterComponentRemoval[coordinateChild.Sequence].ZM
                                      - coordinatesAfterComponentRemoval[insertionParent.Sequence].ZM;
            coordinateChild.ConnectionDeltaConfirmed = true;
            coordinateChild.HasAbsoluteCoordinateAnchor = false;
            var removableConnection = Activator.CreateInstance(layoutConnectionRefType,
                                          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                          null, new object[] { coordinateChild, false }, null)
                                      ?? throw new InvalidOperationException("Could not create a layout connection reference.");
            UiMember.Method("RemoveLayoutConnectionPreservingCoordinates",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new[] { removableConnection });
            var coordinatesAfterDuctRemoval = LayoutCoordinateResolver.Resolve(document);
            resolvedCoordinateField.SetValue(layoutCanvas, coordinatesAfterDuctRemoval);
            var childVisualAfterDuctRemoval = (Point)visualLocationMethod.Invoke(layoutCanvas,
                new object[] { coordinateChild })!;
            Require(string.IsNullOrWhiteSpace(coordinateChild.ParentNodeId)
                    && coordinateChild.HasAbsoluteCoordinateAnchor
                    && Math.Abs(UnitConversions.MToFeet(coordinatesAfterDuctRemoval[coordinateChild.Sequence].XM) - 20) < 1e-9
                    && Math.Abs(UnitConversions.MToFeet(coordinatesAfterDuctRemoval[coordinateChild.Sequence].ZM) - 5) < 1e-9
                    && coordinatesAfterDuctRemoval[insertionParent.Sequence] == parentBeforeRemoval
                    && childVisualAfterDuctRemoval == childVisualBeforeRemoval,
                "Removing a duct should anchor the newly disconnected system without moving either side of the removed connection.");
            document.LayoutNodes.Remove(coordinateChild);
            document.LayoutNodes.Remove(insertionDownstream);
            document.LayoutNodes.Remove(insertionParent);
            document.Ducts.Remove(inheritedDuct);
            document.CoordinateOriginNodeId = priorCoordinateOrigin;
            document.CoordinateOriginXM = priorCoordinateX;
            document.CoordinateOriginYM = priorCoordinateY;
            document.CoordinateOriginZM = priorCoordinateZ;
            Require(layoutScale.Width >= 90 && layoutScale.Items.Count >= 8
                    && layoutScale.Items.Cast<object>().Any(item => item.ToString() == "1 ft / cell"),
                "The grid scale should be a clean dropdown of feet-per-cell choices.");

            var fanCountBeforeDuplicate = document.LayoutNodes.Count(node => node.Type == LayoutNodeType.Fan);
            UiMember.Method("PlaceLayoutNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { LayoutNodeType.Fan, fanNode.GridX + 20, fanNode.GridY + 20, null });
            Require(document.LayoutNodes.Count(node => node.Type == LayoutNodeType.Fan) == fanCountBeforeDuplicate
                    && Find<Label>(form, "LayoutDesignerStatus").Text.Contains("Only one fan", StringComparison.Ordinal),
                "The Layout Designer should reject a second system fan with a clear error.");

            var tryAutoSizeFan = UiMember.Method("TryAutoSizeFan", BindingFlags.Instance | BindingFlags.NonPublic)
                                  ?? throw new InvalidOperationException("Could not find fan auto-sizing.");
            var autoSizeArguments = new object?[] { null };
            var mainDuctForSystem = document.Ducts.Single(x => x.Sequence == 1);
            var unsizedOnFanSystem = document.LayoutNodes
                .Where(node => node.Type != LayoutNodeType.CalculationPoint
                               && document.LayoutNodes.Any(parent => parent.Id == node.ParentNodeId)
                               && node.ConnectionDuctSequence is null)
                .ToArray();
            if (unsizedOnFanSystem.Length > 0)
            {
                var priorFanStatic = fanNode.FanStaticPressurePa;
                var refusedArguments = new object?[] { null };
                Require(!(bool)tryAutoSizeFan.Invoke(form, refusedArguments)!
                        && fanNode.FanStaticPressurePa == priorFanStatic
                        && Convert.ToString(refusedArguments[0])!.Contains("no sized duct", StringComparison.Ordinal),
                    "Auto-size Fan must refuse, leaving the fan unchanged, while a connection on the system is unsized. "
                    + $"Result: {refusedArguments[0]}");
                foreach (var unsized in unsizedOnFanSystem)
                    SizeConnection(document, unsized, mainDuctForSystem);
            }
            var autoSized = (bool)tryAutoSizeFan.Invoke(form, autoSizeArguments)!;
            Require(!autoSized || (bool)UiMember.Field("_criticalPathFilterEnabled",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!,
                "After Auto-size Fan the Duct System table should isolate the critical path used to size the fan.");
            var criticalLoss = (double)(UiMember.Field("_criticalPathPressureLossPa",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form) ?? 0.0);
            Require(autoSized && fanNode.FanStaticPressurePa > 0
                    && Math.Abs(fanNode.FanStaticPressurePa - criticalLoss) < 1e-9
                    && fanNode.AirflowM3PerS > 0
                    && Convert.ToString(autoSizeArguments[0])!.Contains(fanNode.Id, StringComparison.Ordinal),
                "Auto-size Fan should set the single fan to the required system airflow and current critical-path net loss. "
                + $"Result: {autoSizeArguments[0]}");
            var fanInformationMethod = UiMember.Method("TryGetFanInformation",
                                           BindingFlags.Instance | BindingFlags.NonPublic)
                                       ?? throw new InvalidOperationException("Could not inspect fan information.");
            var fanInformationArguments = new object?[] { null };
            Require((bool)fanInformationMethod.Invoke(form, fanInformationArguments)!
                    && Convert.ToString(fanInformationArguments[0])!.Contains(fanNode.Id, StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("Airflow:", StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("External static pressure (ESP):", StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("Internal static pressure:", StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("Total static pressure:", StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("Fan efficiency:", StringComparison.Ordinal)
                    && Convert.ToString(fanInformationArguments[0])!.Contains("Required motor power:", StringComparison.Ordinal),
                "View Fan Information should report the fan ID, airflow, external, internal and total static pressure, efficiency, and horsepower.");
            var fanInformationDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.FanInformationDialog")
                                           ?? throw new InvalidOperationException("Could not find the fan information dialog.");
            using (var fanInformationDialog = (Form?)Activator.CreateInstance(fanInformationDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { Convert.ToString(fanInformationArguments[0])! }, null)
                   ?? throw new InvalidOperationException("Could not create the fan information dialog."))
            {
                fanInformationDialog.PerformLayout();
                var fanInformationIcon = Find<PictureBox>(fanInformationDialog, "FanInformationIcon");
                var fanInformationDetails = Find<Label>(fanInformationDialog, "FanInformationDetails");
                Require(fanInformationIcon.Width == fanInformationIcon.Height
                        && fanInformationDetails.ClientSize.Height >= fanInformationDetails.PreferredHeight,
                    "Fan Information should use a square icon canvas and provide enough height for the complete horsepower line.");
                var fanInformationCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_FAN_INFORMATION_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(fanInformationCapturePath))
                {
                    fanInformationDialog.Show();
                    Application.DoEvents();
                    using var fanInformationPreview = new Bitmap(fanInformationDialog.Width, fanInformationDialog.Height);
                    fanInformationDialog.DrawToBitmap(fanInformationPreview,
                        new Rectangle(Point.Empty, fanInformationDialog.Size));
                    fanInformationPreview.Save(fanInformationCapturePath);
                    fanInformationDialog.Hide();
                }
            }
            document.LayoutNodes.Remove(fanNode);
            var noFanArguments = new object?[] { null };
            Require(!(bool)tryAutoSizeFan.Invoke(form, noFanArguments)!
                    && Convert.ToString(noFanArguments[0])!.Contains("Add a fan in the Layout Designer", StringComparison.Ordinal),
                "Auto-size Fan should direct the user to the Layout Designer when no fan exists.");
            document.LayoutNodes.Add(fanNode);
            Require(!layoutUndo.Enabled && !layoutRedo.Enabled && !layoutDelete.Enabled,
                "Designer Undo, Redo, and selection Delete should begin disabled when there is no edit history or selection.");

            var nodeBoundsMethod = layoutCanvas.GetType().GetMethod("NodeBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                                   ?? throw new InvalidOperationException("Could not find Designer node bounds.");
            var provisionalNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 44, GridY = 56, AirSystemType = "Supply Air", BranchId = "Main"
            };
            document.LayoutNodes.Add(provisionalNode);
            var provisionalBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { provisionalNode })!;
            document.LayoutNodes.Remove(provisionalNode);
            // Round Z: components have no connector dots; ducts are drawn with the Duct Segment tools.
            Require(layoutCanvas.GetType().GetMethod("CandidatePortPoints", BindingFlags.Instance | BindingFlags.NonPublic) is null
                    && layoutCanvas.GetType().GetMethod("DrawPorts", BindingFlags.Instance | BindingFlags.NonPublic) is null
                    && layoutCanvas.GetType().GetMethod("HitTestPort", BindingFlags.Instance | BindingFlags.NonPublic) is null,
                "Components should no longer show connector dots; ducts are drawn centre to centre with Duct Segment.");
            var gridSize = (int)(layoutCanvas.GetType().GetProperty("GridSize", BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?.GetValue(layoutCanvas) ?? 0);
            var provisionalCenter = new Point(provisionalBounds.Left + provisionalBounds.Width / 2,
                provisionalBounds.Top + provisionalBounds.Height / 2);
            var gridPointMethod = layoutCanvas.GetType().GetMethod("GridPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                                  ?? throw new InvalidOperationException("Could not inspect Designer grid intersections.");
            var expectedProvisionalCenter = (Point)gridPointMethod.Invoke(layoutCanvas,
                new object[] { new Point(provisionalNode.GridX, provisionalNode.GridY) })!;
            Require(gridSize >= 56
                    && provisionalCenter == expectedProvisionalCenter,
                "Placed components should snap exactly to intersections of the enlarged AFT-style isometric grid.");
            var snapPlacement = layoutCanvas.GetType().GetMethod("SnapPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?? throw new InvalidOperationException("Could not inspect Designer snap targeting.");
            var setSnapPreview = layoutCanvas.GetType().GetMethod("SetSnapPreview", BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?? throw new InvalidOperationException("Could not inspect Designer snap preview.");
            var expectedSnapTarget = new Point(7, 5);
            var expectedSnapCanvas = (Point)gridPointMethod.Invoke(layoutCanvas, new object[] { expectedSnapTarget })!;
            var snapTarget = (Point)snapPlacement.Invoke(layoutCanvas,
                new object[] { new Point(expectedSnapCanvas.X + 4, expectedSnapCanvas.Y + 3) })!;
            setSnapPreview.Invoke(layoutCanvas, new object?[] { snapTarget });
            var previewTarget = (Point?)layoutCanvas.GetType().GetField("_snapPreviewGrid", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(layoutCanvas);
            Require(snapTarget == expectedSnapTarget && previewTarget == snapTarget,
                "Dragging a component should expose one highlighted grid intersection as its exact drop target.");
            var snapPreviewCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SNAP_PREVIEW_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(snapPreviewCapturePath))
            {
                CaptureForm(layoutCanvas, snapPreviewCapturePath);
            }
            setSnapPreview.Invoke(layoutCanvas, new object?[] { null });
            var undefinedCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_UNDEFINED_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(undefinedCapturePath))
            {
                var undefinedNode = new SavedLayoutNode
                {
                    Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                    FittingGroup = "Elbows", GridX = 30, GridY = 32, ParentNodeId = filterNode.Id,
                    BranchId = "Main", AirSystemType = "Supply Air", IsDefined = false
                };
                document.LayoutNodes.Add(undefinedNode);
                UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Application.DoEvents();
                CaptureForm(layoutCanvas, undefinedCapturePath);
                document.LayoutNodes.Remove(undefinedNode);
                UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            }

            var hitTestNode = layoutCanvas.GetType().GetMethod("HitTest", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var hitTestConnection = layoutCanvas.GetType().GetMethod("HitTestConnection", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var blankPoint = Enumerable.Range(1, 8)
                .SelectMany(row => Enumerable.Range(1, 8).Select(column =>
                    new Point(column * layoutCanvas.ClientSize.Width / 9, row * layoutCanvas.ClientSize.Height / 9)))
                .First(point => hitTestNode.Invoke(layoutCanvas, new object[] { point }) is null
                                && hitTestConnection.Invoke(layoutCanvas, new object[] { point }) is null);
            layoutCanvas.GetType().GetProperty("SelectedTool", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                .SetValue(layoutCanvas, null);
            var offsetBeforePan = (PointF)viewOffsetProperty.GetValue(layoutCanvas)!;
            layoutCanvas.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, blankPoint.X, blankPoint.Y, 0) });
            layoutCanvas.GetType().GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 0, blankPoint.X + 120, blankPoint.Y + 90, 0) });
            var pannedOffset = (PointF)viewOffsetProperty.GetValue(layoutCanvas)!;
            Require(Math.Sqrt(Math.Pow(pannedOffset.X - offsetBeforePan.X, 2)
                              + Math.Pow(pannedOffset.Y - offsetBeforePan.Y, 2)) > 80,
                $"Dragging blank Designer space should pan the virtual workspace. Before={offsetBeforePan}, after={pannedOffset}, point={blankPoint}.");
            var axisLegendBoundsProperty = layoutCanvas.GetType().GetProperty("AxisLegendBounds",
                                               BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                           ?? throw new InvalidOperationException("Could not inspect the fixed XYZ legend.");
            var axisLegendBefore = (Rectangle)axisLegendBoundsProperty.GetValue(layoutCanvas)!;
            Require(axisLegendBefore.Right <= layoutCanvas.ClientSize.Width
                    && axisLegendBefore.Bottom <= layoutCanvas.ClientSize.Height
                    && axisLegendBefore.Left > layoutCanvas.ClientSize.Width / 2
                    && axisLegendBefore.Width == axisLegendBefore.Height,
                "The XYZ orientation legend should remain centered inside a square lower-right overlay.");
            layoutCanvas.GetType().GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, blankPoint.X + 120, blankPoint.Y + 90, 0) });
            layoutCenter.PerformClick();
            Application.DoEvents();
            var centeredOffset = (PointF)viewOffsetProperty.GetValue(layoutCanvas)!;
            // Start the pan on empty drawing space near the centre (a component under the pointer would be dragged
            // instead, which made this check depend on the screen size).
            // Also clear of the view cube and axis legend (a cube corner turns the view) and of any label.
            var overlayCanvas = (LayoutDesignerCanvas)layoutCanvas;
            bool IsBlank(Point point) => hitTestNode.Invoke(layoutCanvas, new object[] { point }) is null
                                         && hitTestConnection.Invoke(layoutCanvas, new object[] { point }) is null
                                         && !Rectangle.Inflate(overlayCanvas.ViewCubeBounds, 12, 12).Contains(point)
                                         && !Rectangle.Inflate(overlayCanvas.AxisLegendBounds, 12, 12).Contains(point)
                                         && overlayCanvas.LabelAt(point) is null;
            var preferredPanPoint = new Point(layoutCanvas.ClientSize.Width / 2, layoutCanvas.ClientSize.Height / 2 + 100);
            var reversePanPoint = IsBlank(preferredPanPoint)
                ? preferredPanPoint
                : Enumerable.Range(1, 8).SelectMany(row => Enumerable.Range(1, 8).Select(column =>
                        new Point(column * layoutCanvas.ClientSize.Width / 9, row * layoutCanvas.ClientSize.Height / 9)))
                    .Where(IsBlank)
                    .OrderBy(point => Math.Abs(point.X - preferredPanPoint.X) + Math.Abs(point.Y - preferredPanPoint.Y))
                    .First();
            var canvasOriginBefore = layoutCanvas.PointToScreen(Point.Empty);
            layoutCanvas.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, reversePanPoint.X, reversePanPoint.Y, 0) });
            // The canvas follows the pointer in screen terms; if pressing it scrolled it into view, the pointer's screen
            // movement differs from the 140 x 110 px moved on the canvas by that shift.
            var canvasShift = new Size(layoutCanvas.PointToScreen(Point.Empty).X - canvasOriginBefore.X,
                layoutCanvas.PointToScreen(Point.Empty).Y - canvasOriginBefore.Y);
            layoutCanvas.GetType().GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 0, reversePanPoint.X + 140, reversePanPoint.Y + 110, 0) });
            Application.DoEvents();
            var reverseOffset = (PointF)viewOffsetProperty.GetValue(layoutCanvas)!;
            var axisLegendAfter = (Rectangle)axisLegendBoundsProperty.GetValue(layoutCanvas)!;
            Require(axisLegendAfter == axisLegendBefore,
                "Panning the Designer should not move the fixed XYZ orientation legend.");
            // Unclamped: the view moved exactly as far as the pointer did on screen.
            Require(Math.Abs(reverseOffset.X - centeredOffset.X - (140 + canvasShift.Width)) < 1.5
                    && Math.Abs(reverseOffset.Y - centeredOffset.Y - (110 + canvasShift.Height)) < 1.5
                    && Math.Abs(reverseOffset.X - centeredOffset.X) + Math.Abs(reverseOffset.Y - centeredOffset.Y) > 0,
                $"The centered Designer workspace should pan upward and leftward without a canvas boundary. Start={reversePanPoint}, before={centeredOffset}, after={reverseOffset}, canvas={layoutCanvas.ClientSize}, shift={canvasShift}.");
            layoutCanvas.GetType().GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, reversePanPoint.X + 140, reversePanPoint.Y + 110, 0) });
            layoutCenter.PerformClick();

            var fanBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { fanNode })!;
            layoutCanvas.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, fanBounds.Left + fanBounds.Width / 2, fanBounds.Top + fanBounds.Height / 2, 0) });
            Application.DoEvents();
            Require(layoutDelete.Enabled,
                "Clicking a Designer component should visibly select it and enable the Delete action.");

            var connectionPathMethod = layoutCanvas.GetType().GetMethod("ConnectionPath", BindingFlags.Instance | BindingFlags.NonPublic)
                                       ?? throw new InvalidOperationException("Could not inspect Designer duct routing.");
            var layoutConnectionType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutConnectionRef")
                                       ?? throw new InvalidOperationException("Could not inspect Designer connection references.");
            var connectionReference = Activator.CreateInstance(layoutConnectionType, coilNode, false)
                                      ?? throw new InvalidOperationException("Could not create a Designer connection reference.");
            var routedDuct = (IReadOnlyList<Point>)connectionPathMethod.Invoke(layoutCanvas,
                new object[] { connectionReference, document.LayoutNodes })!;
            // Round Z: a duct is one straight line between the two component centres (diagonals allowed).
            var routedFanBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { fanNode })!;
            var routedCoilBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { coilNode })!;
            var routedFrom = new Point(routedFanBounds.Left + routedFanBounds.Width / 2, routedFanBounds.Top + routedFanBounds.Height / 2);
            var routedTo = new Point(routedCoilBounds.Left + routedCoilBounds.Width / 2, routedCoilBounds.Top + routedCoilBounds.Height / 2);
            bool OnCentreLine(Point point) =>
                Math.Abs((routedTo.X - routedFrom.X) * (point.Y - routedFrom.Y) - (routedTo.Y - routedFrom.Y) * (point.X - routedFrom.X))
                <= 2.0 * Math.Sqrt(Math.Pow(routedTo.X - routedFrom.X, 2) + Math.Pow(routedTo.Y - routedFrom.Y, 2));
            Require(routedDuct.Count == 2 && routedDuct.All(OnCentreLine),
                $"Designer ducts should be drawn as one straight line from component centre to component centre. Path={string.Join(" ", routedDuct)}, from={routedFrom}, to={routedTo}.");
            var ductMidpoint = new Point((routedDuct[0].X + routedDuct[1].X) / 2,
                (routedDuct[0].Y + routedDuct[1].Y) / 2);
            layoutCanvas.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, ductMidpoint.X, ductMidpoint.Y, 0) });
            Application.DoEvents();
            var selectedConnectionKey = layoutCanvas.GetType().GetField("_selectedConnectionKey",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(layoutCanvas) as string;
            Require(!string.IsNullOrWhiteSpace(selectedConnectionKey) && layoutDelete.Enabled,
                "Clicking a graphical duct should select and highlight the duct and enable the Delete action.");
            var ductSelectionCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_DUCT_SELECTION_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(ductSelectionCapturePath))
            {
                CaptureForm(form, ductSelectionCapturePath);
            }

            var isolatedNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 100, GridY = 200, AirSystemType = "Supply Air", BranchId = "Main",
                HasAbsoluteCoordinateAnchor = true,
                AbsoluteAnchorXM = UnitConversions.FeetToM(50), AbsoluteAnchorYM = UnitConversions.FeetToM(50)
            };
            document.LayoutNodes.Add(isolatedNode);
            var moveLayoutNode = UiMember.Method("MoveLayoutNode", BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?? throw new InvalidOperationException("Could not inspect Designer move history.");
            moveLayoutNode.Invoke(form, new object[] { isolatedNode, fanNode.GridX, fanNode.GridY });
            Require(string.IsNullOrWhiteSpace(isolatedNode.ParentNodeId)
                    && string.IsNullOrWhiteSpace(isolatedNode.SecondaryParentNodeId)
                    && isolatedNode.ConnectionDuctSequence is null,
                "Moving an unconnected component near an existing component must not create a proximity connection.");
            document.LayoutNodes.Remove(isolatedNode);
            UiMember.Method("ClearLayoutHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            var originalFilterX = filterNode.GridX;
            var movedFilterX = originalFilterX - 2;
            var originalFilterParent = filterNode.ParentNodeId;
            var originalFilterDuct = filterNode.ConnectionDuctSequence;
            ClickButton(layoutRearrange);
            Application.DoEvents();
            Require(((dynamic)layoutRearrange).Checked
                    && (bool)(layoutCanvas.GetType().GetProperty("RearrangeMode", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(layoutCanvas) ?? false),
                "Clicking Move should enter a visible, toggleable move-only mode.");
            moveLayoutNode.Invoke(form, new object[] { filterNode, movedFilterX, filterNode.GridY });
            Application.DoEvents();
            Require(layoutUndo.Enabled && filterNode.GridX == movedFilterX
                    && filterNode.ParentNodeId == originalFilterParent
                    && filterNode.ConnectionDuctSequence == originalFilterDuct,
                "Rearranging a Designer component should create an Undo state without changing its connection or duct data.");
            layoutUndo.PerformClick();
            Application.DoEvents();
            var restoredDocument = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var restoredFilter = restoredDocument.LayoutNodes.FirstOrDefault(x => x.Sequence == filterNode.Sequence);
            Require(restoredFilter is not null && restoredFilter.GridX == originalFilterX && layoutRedo.Enabled,
                "Undo should restore the previous Designer component position and enable Redo.");
            layoutRedo.PerformClick();
            Application.DoEvents();
            restoredDocument = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            restoredFilter = restoredDocument.LayoutNodes.FirstOrDefault(x => x.Sequence == filterNode.Sequence);
            Require(restoredFilter is not null && restoredFilter.GridX == movedFilterX,
                "Redo should restore the reapplied Designer component position.");
            var processShortcut = UiMember.Method("ProcessCmdKey",
                                      BindingFlags.Instance | BindingFlags.NonPublic)
                                  ?? throw new InvalidOperationException("Could not inspect Designer keyboard shortcuts.");
            var undoShortcutArgs = new object[]
            {
                Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.Z
            };
            Require((bool)(processShortcut.Invoke(form, undoShortcutArgs) ?? false),
                "Ctrl+Z should be handled as the Layout Designer Undo shortcut.");
            Application.DoEvents();
            restoredDocument = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            restoredFilter = restoredDocument.LayoutNodes.FirstOrDefault(x => x.Sequence == filterNode.Sequence);
            Require(restoredFilter is not null && restoredFilter.GridX == originalFilterX,
                "Ctrl+Z should restore the previous Designer component position.");
            var redoShortcutArgs = new object[]
            {
                Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.Y
            };
            Require((bool)(processShortcut.Invoke(form, redoShortcutArgs) ?? false),
                "Ctrl+Y should be handled as the Layout Designer Redo shortcut.");
            Application.DoEvents();
            restoredDocument = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            restoredFilter = restoredDocument.LayoutNodes.FirstOrDefault(x => x.Sequence == filterNode.Sequence);
            Require(restoredFilter is not null && restoredFilter.GridX == movedFilterX,
                "Ctrl+Y should restore the reapplied Designer component position.");
            ClickButton(layoutRearrange);
            Application.DoEvents();
            Require(!((dynamic)layoutRearrange).Checked && ((dynamic)Find<Button>(form, "RibbonSelectButton")).Checked,
                "Clicking Move again should return the Designer to normal (Select) editing mode.");
            document = restoredDocument;
            fanNode = document.LayoutNodes.Single(x => x.Sequence == fanNode.Sequence);
            coilNode = document.LayoutNodes.Single(x => x.Sequence == coilNode.Sequence);
            filterNode = document.LayoutNodes.Single(x => x.Sequence == filterNode.Sequence);
            var orderedIds = grid.Rows.Cast<DataGridViewRow>()
                .Select(row => Convert.ToString(row.Cells["Id"].Value) ?? string.Empty).ToList();
            Require(orderedIds.IndexOf(fanNode.Id) < orderedIds.IndexOf("D-001")
                    && orderedIds.IndexOf("D-001") < orderedIds.IndexOf(coilNode.Id)
                    && orderedIds.IndexOf(coilNode.Id) < orderedIds.IndexOf(filterNode.Id),
                "System rows should follow the drawn graph from upstream component through duct and downstream components.");

            filterNode.AirflowOverride = true;
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var overrideRow = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                Convert.ToString(row.Cells["Id"].Value) == filterNode.Id);
            Require(overrideRow.DefaultCellStyle.BackColor == Color.FromArgb(255, 237, 213),
                "An accepted airflow-continuity override should be highlighted orange in the system schedule.");
            filterNode.AirflowOverride = false;

            var pressurePoint = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.CalculationPoint,
                CalculationPointType = "Pressure", CalculationConnectionNodeId = coilNode.Id,
                CalculationConnectionFraction = 0.5, ObservedDuctSequence = 1,
                GridX = (fanNode.GridX + coilNode.GridX) / 2, GridY = (fanNode.GridY + coilNode.GridY) / 2,
                AirSystemType = coilNode.AirSystemType, BranchId = coilNode.BranchId,
                IsDefined = true
            };
            document.LayoutNodes.Add(pressurePoint);
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var pressurePointRow = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                Convert.ToString(row.Cells["Id"].Value) == pressurePoint.Id);
            var pressureOrderedIds = grid.Rows.Cast<DataGridViewRow>()
                .Select(row => Convert.ToString(row.Cells["Id"].Value) ?? string.Empty).ToList();
            Require(Convert.ToString(pressurePointRow.Cells["StaticDrop"].Value) == "—"
                    && Convert.ToString(pressurePointRow.Cells["PressureOut"].Value) != "—"
                    && Convert.ToString(pressurePointRow.Cells["Airflow"].Value) == "—"
                    && Convert.ToString(pressurePointRow.Cells["Velocity"].Value) == "—"
                    && Convert.ToString(pressurePointRow.Cells["Temperature"].Value) == "—"
                    && pressureOrderedIds.IndexOf(pressurePoint.Id) == pressureOrderedIds.IndexOf("D-001") + 1
                    && pressurePoint.ParentNodeId.Length == 0,
                "A pressure calculation point should observe the existing duct without splitting it, expose pressure in the pressure column, and appear immediately after that duct.");
            var connectedDuctRow = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                Convert.ToString(row.Cells["Id"].Value) == "D-001");
            Require(grid.Rows.Cast<DataGridViewRow>().Any(row =>
                        Convert.ToString(row.Cells["Description"].Value) == "Coil"
                        && Convert.ToString(row.Cells["StaticDrop"].Value) == "0.25")
                    && grid.Rows.Cast<DataGridViewRow>().Any(row =>
                        Convert.ToString(row.Cells["Description"].Value) == "Filter"
                        && Convert.ToString(row.Cells["StaticDrop"].Value) == "0.1")
                    && Convert.ToString(connectedDuctRow.Cells["UpstreamId"].Value) == fanNode.Id
                    && Convert.ToString(connectedDuctRow.Cells["DownstreamId"].Value) == coilNode.Id
                    && Convert.ToString(connectedDuctRow.Cells["Airflow"].Value) != "—"
                    && Convert.ToString(connectedDuctRow.Cells["Shape"].Value) == "Round"
                    && Convert.ToString(connectedDuctRow.Cells["Temperature"].Value) != "—"
                    && !total.Text.EndsWith("—", StringComparison.Ordinal),
                "Layout equipment and connection topology should appear in the system schedule with correctly aligned airflow-through-temperature cells and contribute pressure drops.");
            var sinkForPressureBalance = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir,
                SpaceRole = "Space Sink", ParentNodeId = filterNode.Id,
                GridX = 30, GridY = 16, AirSystemType = "Supply Air", BranchId = "Main", IsDefined = true
            };
            document.LayoutNodes.Add(sinkForPressureBalance);
            SizeConnection(document, sinkForPressureBalance, mainDuct1());
            // Solve once so the new duct carries the system airflow and temperature before losses are summed.
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            var routeDuctLossPa = new[] { coilNode, filterNode, sinkForPressureBalance }
                .Select(node => document.Ducts.Single(x => x.Sequence == node.ConnectionDuctSequence))
                .Sum(duct => duct.Calculate().TotalPressureLossUpperPa);
            // Plus the exit loss of the bare duct into the space (one velocity pressure; round X).
            var sinkDuctResult = document.Ducts.Single(x => x.Sequence == sinkForPressureBalance.ConnectionDuctSequence).Calculate();
            var expectedNetLossPa = routeDuctLossPa + coilNode.PressureDropPa + filterNode.PressureDropPa
                                    + 0.5 * sinkDuctResult.Air.DensityKgPerM3 * sinkDuctResult.ActualVelocityMPerS * sinkDuctResult.ActualVelocityMPerS;
            fanNode.FanStaticPressurePa = expectedNetLossPa;
            UiMember.Method("UpdateCalculatedLayoutValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Require(Math.Abs(sinkForPressureBalance.PressurePa) < 1e-8,
                "When fan static equals the critical-path net loss, the Space Sink pressure should solve to 0 in. w.c. "
                + $"fan {fanNode.PressurePa:R} (parent '{fanNode.ParentNodeId}'), coil {coilNode.PressurePa:R}, filter {filterNode.PressurePa:R}, "
                + $"sink {sinkForPressureBalance.PressurePa:R}, expected loss {expectedNetLossPa:R}, ducts "
                + string.Join(",", new[] { coilNode, filterNode, sinkForPressureBalance }.Select(x => x.ConnectionDuctSequence)));
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            tabs.SelectedTab = tabs.TabPages[2];
            Application.DoEvents();
            var criticalLabel = Find<Label>(form, "SystemCriticalPathLabel");
            var expectedCriticalInWg = UnitConversions.PaToInchesWater(expectedNetLossPa).ToString("0.###");
            var allRowsInWg = UnitConversions.PaToInchesWater(
                routeDuctLossPa
                + coilNode.PressureDropPa + filterNode.PressureDropPa
                + document.Fittings.Single().Calculate().PressureLossPa).ToString("0.###");
            Require(criticalPathFilter.Enabled
                    && criticalLabel.Text.StartsWith("Route:", StringComparison.Ordinal)
                    && total.Text.StartsWith("Critical-path net pressure loss:", StringComparison.Ordinal)
                    && !total.Text.Contains("fan positive static", StringComparison.OrdinalIgnoreCase)
                    && total.Text.Contains(expectedCriticalInWg, StringComparison.Ordinal)
                    && !total.Text.Contains(allRowsInWg, StringComparison.Ordinal)
                    && grid.Rows.Cast<DataGridViewRow>().Any(row => row.DefaultCellStyle.BackColor == Color.FromArgb(232, 244, 253)),
                $"The system summary should report critical route {expectedCriticalInWg}, not all-row sum {allRowsInWg}. Actual: {total.Text}");
            var unfilteredCount = grid.Rows.Count;
            criticalPathFilter.PerformClick();
            Application.DoEvents();
            Require(criticalPathFilter.Text == "Show All Items" && grid.Rows.Count <= unfilteredCount,
                "The critical-path filter should hide noncritical rows and offer to restore all items.");
            criticalPathFilter.PerformClick();
            Application.DoEvents();

            var greenSaveBasePath = Path.Combine(testDirectory, "green-save-project");
            AutomationOptions.ExportBasePath = greenSaveBasePath;
            try
            {
                UiMember.Method("ExportDuctSystem", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, null);
            }
            finally
            {
                AutomationOptions.ExportBasePath = null;
            }
            var greenSavePdfPath = greenSaveBasePath + ".pdf";
            var greenSaveCsvPath = greenSaveBasePath + ".csv";
            var greenSaveProjectPath = greenSaveBasePath + ProjectFile.Extension;
            Require(File.Exists(greenSavePdfPath) && new FileInfo(greenSavePdfPath).Length > 10_000
                    && File.Exists(greenSaveCsvPath) && new FileInfo(greenSaveCsvPath).Length > 100
                    && File.Exists(greenSaveProjectPath),
                "The green Save button should create the project file, PDF, and CSV at the selected base path.");
            var sampleProjectCopy = Environment.GetEnvironmentVariable("DUCTSIZER_SAMPLE_PROJECT_PATH");
            if (!string.IsNullOrWhiteSpace(sampleProjectCopy))
            {
                File.Copy(greenSaveProjectPath, sampleProjectCopy, overwrite: true);
                File.Copy(greenSavePdfPath, Path.ChangeExtension(sampleProjectCopy, ".pdf"), overwrite: true);
            }
            {
                var savedModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                var expectedNodes = savedModel.LayoutNodes.Count;
                var expectedDucts = savedModel.Ducts.Count;
                var expectedFittings = savedModel.Fittings.Count;
                var reopened = ProjectFile.Load(greenSaveProjectPath);
                Require(reopened.LayoutNodes.Count == expectedNodes && reopened.Ducts.Count == expectedDucts
                        && reopened.Fittings.Count == expectedFittings && expectedNodes > 0,
                    "The saved project file should carry the full model, including the graphical layout.");

            }
            var greenSavePdf = PdfText.Plain(File.ReadAllBytes(greenSavePdfPath));
            var greenProjectSummaryIndex = greenSavePdf.IndexOf("Project Summary", StringComparison.Ordinal);
            var greenFirstViewIndex = greenSavePdf.IndexOf("Layout Designer - Isometric Positive", StringComparison.Ordinal);
            var greenFirstComponentIndex = greenSavePdf.IndexOf($"{fanNode.Id} - ", StringComparison.Ordinal);
            var resultTextMethod = typeof(DuctSystemPdf).GetMethod("CalculationResultText",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not inspect solved-result report labeling.");
            var densityResultText = (string)resultTextMethod.Invoke(null, new object[]
            {
                new DuctSystemPdfCalculationLine("\\rho = \\rho_{moist}(T)", "0.07453 lbm/ft3", true)
            })!;
            var frictionResultText = (string)resultTextMethod.Invoke(null, new object[]
            {
                new DuctSystemPdfCalculationLine("Colebrook(\\epsilon_{r}, Re) \\rightarrow f", "0.018659", true)
            })!;
            var pressureResultText = (string)resultTextMethod.Invoke(null, new object[]
            {
                new DuctSystemPdfCalculationLine("\\Delta P = rate \\times length", "0.0458 in. w.c.", true)
            })!;
            var reynoldsResultText = (string)resultTextMethod.Invoke(null, new object[]
            {
                new DuctSystemPdfCalculationLine("Re = \\frac{\\rho VD}{\\mu}", "116475", true)
            })!;
            var referenceBasisResultText = (string)resultTextMethod.Invoke(null, new object[]
            {
                new DuctSystemPdfCalculationLine("Reference velocity basis =", "upstream/reference duct")
            })!;
            Require(greenSavePdf.Contains("Project Summary", StringComparison.Ordinal)
                    && greenSavePdf.Contains("/View0", StringComparison.Ordinal)
                    && greenSavePdf.Contains("GRAPHIC SCALE", StringComparison.Ordinal)
                    && greenSavePdf.Contains("GRID SPACING:", StringComparison.Ordinal)
                    && densityResultText == "Air density (rho) = 0.07453 lbm/ft3"
                    && frictionResultText == "Darcy friction factor (f) = 0.018659"
                    && pressureResultText == "Pressure drop (Delta P) = 0.0458 in. w.c."
                    && reynoldsResultText == "Reynolds number (Re) = 116475"
                    && referenceBasisResultText == "upstream/reference duct"
                    && greenProjectSummaryIndex >= 0
                    && greenFirstViewIndex > greenProjectSummaryIndex
                    && greenFirstComponentIndex > greenFirstViewIndex
                    && Find<Label>(form, "SystemStorageStatusLabel").Text.StartsWith("Saved ", StringComparison.Ordinal),
                "The green Save button should place scaled Designer views after the cover and before component calculations, label solved report values, then provide a visible success status.");

            var rendererType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.PdfPageRenderSession")
                               ?? throw new InvalidOperationException("Could not find the PDF page renderer.");
            using var renderSession = (IDisposable?)(Activator.CreateInstance(rendererType,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { greenSavePdfPath }, null))
                ?? throw new InvalidOperationException("Could not open an exported PDF for background rendering.");
            var pageCount = (int)(rendererType.GetProperty("PageCount",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(renderSession) ?? 0);
            using var renderedPage = (Bitmap?)(rendererType.GetMethod("RenderPage",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(renderSession, new object[] { 0, 900 }))
                ?? throw new InvalidOperationException("Could not render the selected PDF page.");
            var pageInfo = rendererType.GetMethod("GetPageInfo",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(renderSession, new object[] { 0 })
                ?? throw new InvalidOperationException("Could not read the selected PDF page dimensions.");
            var pageInfoType = pageInfo.GetType();
            var pageWidthPoints = Convert.ToSingle(pageInfoType.GetProperty("WidthPoints")!.GetValue(pageInfo));
            var pageHeightPoints = Convert.ToSingle(pageInfoType.GetProperty("HeightPoints")!.GetValue(pageInfo));
            Require(pageCount >= 1 && renderedPage.Width > 100 && renderedPage.Height > 100
                    && pageWidthPoints > 0 && pageHeightPoints > 0,
                "The embedded PDF renderer should open, inspect, and rasterize an exported project sheet.");
            using var pagePng = new MemoryStream();
            renderedPage.Save(pagePng, System.Drawing.Imaging.ImageFormat.Png);
            var priorPdfTestViewMode = document.LayoutViewMode;
            var priorPdfTestFeetPerGridCell = document.LayoutFeetPerGridCell;
            document.LayoutPdfBackground = new LayoutPdfBackground
            {
                SourceFileName = Path.GetFileName(greenSavePdfPath),
                PageNumber = 1,
                PageCount = pageCount,
                PageWidthPoints = pageWidthPoints,
                PageHeightPoints = pageHeightPoints,
                PagePngBase64 = Convert.ToBase64String(pagePng.ToArray()),
                PaperInches = 0.25,
                DrawingFeet = 1,
                CenterXFeet = 0,
                CenterYFeet = 0,
                CenterZFeet = 0,
                Visible = true
            };
            document.LayoutViewMode = LayoutViewMode.TopDown;
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            Application.DoEvents();
            Require(layoutPdfBackground.Text == "PDF Background" && layoutViewMode.SelectedIndex == 2,
                "An imported construction sheet should activate the Top-Down overlay and expose its management menu.");
            var renderSchematicMethod = UiMember.Method("RenderPdfSchematic",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not capture a report schematic.");
            var importedBackground = document.LayoutPdfBackground;
            var planWithUnderlay = (DuctSystemPdfSchematic)renderSchematicMethod.Invoke(form,
                new object[] { "Layout Designer - Top-Down", LayoutViewMode.TopDown, false })!;
            document.LayoutPdfBackground = null;
            var planWithoutUnderlay = (DuctSystemPdfSchematic)renderSchematicMethod.Invoke(form,
                new object[] { "Layout Designer - Top-Down", LayoutViewMode.TopDown, false })!;
            document.LayoutPdfBackground = importedBackground;
            Require(planWithUnderlay.ImageJpegBase64 != planWithoutUnderlay.ImageJpegBase64
                    && planWithUnderlay.FeetPerGridCell > 0 && planWithUnderlay.PixelsPerFoot > 0,
                "The exported Top-Down report view should include an imported PDF underlay and retain enough scale metadata for its graphical scale bar.");
            var pdfBackgroundCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_PDF_BACKGROUND_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(pdfBackgroundCapturePath))
            {
                ClickButton(layoutNavigation);
                Application.DoEvents();
                CaptureForm(form, pdfBackgroundCapturePath);
            }
            var pdfDialogCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_PDF_DIALOG_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(pdfDialogCapturePath))
            {
                var dialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.PdfBackgroundImportDialog")
                                 ?? throw new InvalidOperationException("Could not find the PDF import dialog.");
                using var importDialog = (Form?)(Activator.CreateInstance(dialogType,
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { greenSavePdfPath, renderSession }, null))
                    ?? throw new InvalidOperationException("Could not create the PDF import dialog.");
                importDialog.Show(form);
                Application.DoEvents();
                CaptureForm(importDialog, pdfDialogCapturePath);
                importDialog.Hide();
            }
            var pdfBoundsMethod = layoutCanvas.GetType().GetMethod("PdfBackgroundBounds",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not inspect PDF background scaling.");
            var feetPerGridProperty = layoutCanvas.GetType().GetProperty("FeetPerGridCell",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not inspect the Designer grid scale.");
            feetPerGridProperty.SetValue(layoutCanvas, 1D);
            var oneFootBounds = (Rectangle)pdfBoundsMethod.Invoke(layoutCanvas,
                new object[] { document.LayoutPdfBackground })!;
            feetPerGridProperty.SetValue(layoutCanvas, 2D);
            var twoFootBounds = (Rectangle)pdfBoundsMethod.Invoke(layoutCanvas,
                new object[] { document.LayoutPdfBackground })!;
            Require(oneFootBounds.Width >= twoFootBounds.Width * 1.9
                    && oneFootBounds.Height >= twoFootBounds.Height * 1.9,
                "The PDF background should remain at its stated drawing scale when the grid feet-per-cell scale changes.");

            var zoomProperty = layoutCanvas.GetType().GetProperty("ZoomFactor",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not inspect the Designer zoom factor.");
            var zoomMethod = layoutCanvas.GetType().GetMethod("ZoomBy",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not exercise Designer zooming.");
            var originalZoom = (float)zoomProperty.GetValue(layoutCanvas)!;
            var originalViewOffset = (PointF)(layoutCanvas.GetType().GetProperty("ViewOffset",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(layoutCanvas) ?? PointF.Empty);
            for (var zoomStep = 0; zoomStep < 20; zoomStep++)
                zoomMethod.Invoke(layoutCanvas, new object[] { -0.1F });
            Require((float)zoomProperty.GetValue(layoutCanvas)! <= 0.101F,
                "Mouse-wheel zoom should continue out to 10% instead of stopping at the former 35% limit.");
            var zoomedOutCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_ZOOMED_OUT_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(zoomedOutCapturePath))
            {
                ClickButton(layoutNavigation);
                Application.DoEvents();
                CaptureForm(form, zoomedOutCapturePath);
            }
            zoomProperty.SetValue(layoutCanvas, originalZoom);
            layoutCanvas.GetType().GetField("_viewOffset", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(layoutCanvas, originalViewOffset);

            var managePdfMethod = UiMember.Method("ManageLayoutPdfBackground",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not open the PDF background commands.");
            managePdfMethod.Invoke(form, null);
            Application.DoEvents();
            var pdfMenu = PrivateField<ContextMenuStrip>(form, "_layoutPdfMenu");
            pdfMenu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Hide PDF").PerformClick();
            pdfMenu.Close();
            Application.DoEvents();
            Require(document.LayoutPdfBackground is { Visible: false }
                    && layoutPdfBackground.Text == "PDF Hidden",
                "The PDF menu should hide the background without disposing its command before it runs.");

            managePdfMethod.Invoke(form, null);
            Application.DoEvents();
            pdfMenu = PrivateField<ContextMenuStrip>(form, "_layoutPdfMenu");
            pdfMenu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Show PDF").PerformClick();
            pdfMenu.Close();
            Application.DoEvents();
            Require(document.LayoutPdfBackground is { Visible: true }
                    && layoutPdfBackground.Text == "PDF Background",
                "The PDF menu should restore a hidden background without an unexpected error.");

            managePdfMethod.Invoke(form, null);
            Application.DoEvents();
            pdfMenu = PrivateField<ContextMenuStrip>(form, "_layoutPdfMenu");
            using (var propertiesTimer = new System.Windows.Forms.Timer { Interval = 60 })
            {
                propertiesTimer.Tick += (_, _) =>
                {
                    var propertiesDialog = Application.OpenForms.Cast<Form>()
                        .FirstOrDefault(open => open.Name == "PdfBackgroundPropertiesDialog");
                    if (propertiesDialog is null)
                        return;
                    Find<TextBox>(propertiesDialog, "PdfEditCenterXInput").Text = "6";
                    Find<Button>(propertiesDialog, "PdfBackgroundPropertiesSaveButton").PerformClick();
                };
                propertiesTimer.Start();
                pdfMenu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Move / Change Scale...")
                    .PerformClick();
                propertiesTimer.Stop();
            }
            pdfMenu.Close();
            Application.DoEvents();
            Require(Math.Abs(document.LayoutPdfBackground!.CenterXFeet - 6) < 0.001,
                "Move / Change Scale should update the PDF center without an unexpected menu-disposal error.");

            managePdfMethod.Invoke(form, null);
            Application.DoEvents();
            pdfMenu = PrivateField<ContextMenuStrip>(form, "_layoutPdfMenu");
            pdfMenu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Delete PDF").PerformClick();
            pdfMenu.Close();
            Application.DoEvents();
            Require(document.LayoutPdfBackground is null && layoutPdfBackground.Text == "Upload PDF",
                "Delete PDF should remove only the background and leave the Designer available.");
            document.LayoutViewMode = priorPdfTestViewMode;
            document.LayoutFeetPerGridCell = priorPdfTestFeetPerGridCell;
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);

            var pdfReportItems = (IReadOnlyList<DuctSystemPdfReportItem>)(UiMember.Method("BuildPdfReportItems", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null) ?? Array.Empty<DuctSystemPdfReportItem>());
            Require(pdfReportItems.Count >= 5
                    && pdfReportItems[0].Id == fanNode.Id
                    && pdfReportItems[1].Id == mainDuct1().Id
                    && pdfReportItems[2].Id == pressurePoint.Id
                    && pdfReportItems[3].Id == coilNode.Id
                    && pdfReportItems.Any(item => item.Id == mainDuct1().Id
                        && item.Inputs.Any(field => field.Label == "Equivalent diameter method")
                        && item.QuickCheck?.Select(field => field.Label).SequenceEqual(new[]
                        {
                            "Duct size", "Airflow", "Duct length", "Duct velocity", "Pressure rate", "Pressure drop"
                        }) == true
                        && item.QuickCheck![4].Value.EndsWith("in. w.c./100 ft", StringComparison.Ordinal)
                        && item.Outputs.Any(field => field.Label == "Reynolds number")
                        && item.Outputs.Any(field => field.Label == "Total pressure in")
                        && item.Outputs.Any(field => field.Label == "Static pressure out")
                        && item.Calculations?.Any(section => section.Heading == "4. DARCY FRICTION FACTOR"
                            && section.Lines.Any(line => line.Formula.Contains("Colebrook", StringComparison.Ordinal))) == true)
                    && pdfReportItems.Any(item => item.Id == mainDuct1().Id
                        // Round T: the Colebrook equation is written out in step 4, and every symbol is defined with its source.
                        && item.Calculations?.Any(section => section.Heading == "4. DARCY FRICTION FACTOR"
                            && section.Lines.Any(line => line.Formula.StartsWith("\\frac{1}{\\sqrt{f}} = -2\\log_{10}(", StringComparison.Ordinal))) == true
                        && item.Calculations?.Any(section => section.Heading == "VARIABLES AND WHERE THEY COME FROM"
                            && new[] { "Q", "Re", "f", "\\rho", "\\mu", "VP", "L", "\\Delta P" }.All(symbol => section.Lines.Any(line =>
                                line.Kind == DuctSystemPdfLineKind.Variable && line.Formula == symbol && line.HighlightValue.Length > 10))) == true
                        && item.Calculations?.All(section => section.Heading != "REQUIRED EQUATIONS"
                            && section.Lines.All(line => !line.Formula.Contains("[", StringComparison.Ordinal))) == true
                        && item.Calculations?.Any(section => section.Heading == "INPUT VALUES"
                            && section.Lines.Any(line => line.HighlightAsInput)) == true
                        && item.Calculations?.Any(section => section.Heading == "5. VELOCITY PRESSURE AND DUCT LOSS"
                            && section.Lines.Any(line => !string.IsNullOrWhiteSpace(line.InputSubstitution))) == true)
                    && pdfReportItems.Any(item => item.Id == pressurePoint.Id
                        && item.Inputs.Any(field => field.Label == "Observed duct" && field.Value == "D-001")
                        && item.Outputs.Any(field => field.Label == "Calculated value"))
                    && pdfReportItems.Any(item => item.Id == document.Fittings.Single().Id
                        && item.Outputs.Any(field => field.Label == "Loss coefficient C")
                        && item.QuickCheck?.Select(field => field.Label).SequenceEqual(new[]
                        {
                            "Fitting type / name", "Fitting C-value", "Pressure drop"
                        }) == true
                        && System.Text.RegularExpressions.Regex.IsMatch(item.QuickCheck![1].Value, @"^\d+\.\d{3}$")
                        && item.Calculations?.Any(section => section.Heading == "COEFFICIENT AND PRESSURE LOSS") == true
                        && item.Calculations?.Any(section => section.Heading == "VARIABLES AND WHERE THEY COME FROM"
                            && section.Lines.Any(line => line.Formula == "C")) == true
                        && !string.IsNullOrWhiteSpace(item.ReferenceImageJpegBase64)
                        && item.ReferenceImagePixelWidth > 0 && item.ReferenceImagePixelHeight > 0),
                "The PDF detail report should follow connected-system order and include comprehensive duct, fitting, and equipment inputs and calculated outputs.");
            var titusPdfSections = (IReadOnlyList<DuctSystemPdfCalculationSection>)(UiMember.Method("BuildFittingPdfCalculations", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { titusCalculation }) ?? Array.Empty<DuctSystemPdfCalculationSection>());
            Require(titusPdfSections.Any(section => section.Heading == "PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)" && section.Wide
                                                    && section.Lines.Count >= 8
                                                    && section.Lines.Count(line => line.Kind == DuctSystemPdfLineKind.TableRowSelected) == 1)
                    && titusPdfSections.SelectMany(section => section.Lines)
                        .Any(line => line.HighlightValue.Contains("courtesy of Titus", StringComparison.OrdinalIgnoreCase))
                    && titusPdfSections.SelectMany(section => section.Lines)
                        .Any(line => line.Formula.Contains("Automatically selected inlet", StringComparison.Ordinal)),
                "The exported Titus calculation should include the selected performance chart, inlet, and manufacturer reference.");
            var titus350RPdfSections = (IReadOnlyList<DuctSystemPdfCalculationSection>)(UiMember.Method("BuildFittingPdfCalculations", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { titus350RCalculation }) ?? Array.Empty<DuctSystemPdfCalculationSection>());
            Require(titus350RPdfSections.Any(section => section.Heading == "PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)"
                                                         && section.Lines.Count == 10
                                                         && section.Lines[0].Kind == DuctSystemPdfLineKind.TableHeader
                                                         && section.Lines[0].Cells.Contains("Static pressure (in. w.c.)")
                                                         && section.Lines.Skip(1).All(line => line.Cells[3].StartsWith('-')))
                    && titus350RPdfSections.Any(section => section.Heading == "SELECTION FROM THE TABLE"
                                                         && section.Lines.Single().Formula.Contains("published", StringComparison.Ordinal))
                    && titus350RPdfSections.SelectMany(section => section.Lines)
                        .Any(line => line.HighlightValue.Contains("courtesy of Titus", StringComparison.OrdinalIgnoreCase)),
                "The exported Titus 350R calculation should include its nine-point chart, negative static pressure, and manufacturer reference.");
            var reportItemsWithTitus = pdfReportItems.Concat(new[]
            {
                new DuctSystemPdfReportItem("T-TBD80D", "Linear Diffuser - Titus TBD-80 D", "GRD",
                    new[]
                    {
                        new DuctSystemPdfField("Requested airflow", "500 cfm"),
                        new DuctSystemPdfField("Length", "4 ft"),
                        new DuctSystemPdfField("Slot size", "1 in"),
                        new DuctSystemPdfField("Number of slots", "3")
                    },
                    new[]
                    {
                        new DuctSystemPdfField("Selected airflow", "535 cfm"),
                        new DuctSystemPdfField("Total pressure", "0.178 in. w.c."),
                        new DuctSystemPdfField("Sound", "NC 36"),
                        new DuctSystemPdfField("Throw", "25-30-43 ft")
                    }, titusPdfSections),
                new DuctSystemPdfReportItem("T-350R", "Grille - Titus 350R", "GRD",
                    new[]
                    {
                        new DuctSystemPdfField("Requested airflow", "500 cfm"),
                        new DuctSystemPdfField("Nominal duct size", "12x12 in")
                    },
                    new[]
                    {
                        new DuctSystemPdfField("Selected airflow", "528 cfm"),
                        new DuctSystemPdfField("Pressure drop", "0.073 in. w.c."),
                        new DuctSystemPdfField("Sound", "NC 19"),
                        new DuctSystemPdfField("Core velocity", "600 fpm")
                    }, titus350RPdfSections)
            }).ToArray();
            var fullReportPath = Environment.GetEnvironmentVariable("DUCTSIZER_FULL_REPORT_SAMPLE_PATH");
            if (!string.IsNullOrWhiteSpace(fullReportPath))
            {
                document.LayoutPdfBackground = importedBackground;
                document.CompanyLogoPngBase64 = CreateTestCompanyLogo();
                var logo = ((string Base64, int Width, int Height))(UiMember.Method("BuildReportLogo", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, null) ?? (string.Empty, 0, 0));
                Require(!string.IsNullOrWhiteSpace(logo.Base64) && logo.Width > 0 && logo.Height > 0,
                    "The embedded project logo should be converted for the PDF report.");
                var pdfLayout = (DuctSystemPdfLayout?)UiMember.Method("BuildPdfLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                var pdfSchematics = (IReadOnlyList<DuctSystemPdfSchematic>)(UiMember.Method("BuildPdfSchematics", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, null) ?? Array.Empty<DuctSystemPdfSchematic>());
                Require(pdfSchematics.Count == 4
                        && pdfSchematics.Select(item => item.Title).SequenceEqual(new[]
                        {
                            "Layout Designer - Isometric Positive", "Layout Designer - Isometric Negative",
                            "Layout Designer - Top-Down", "Layout Designer - Critical Path"
                        })
                        && pdfSchematics.All(item => item.PixelWidth > 0 && item.PixelHeight > 0
                                                       && item.FeetPerGridCell > 0 && item.PixelsPerFoot > 0
                                                       && !string.IsNullOrWhiteSpace(item.ImageJpegBase64)),
                    "The exported project report should capture all three Designer views plus the highlighted critical path from the live canvas.");
                var reportHeaders = new[]
                {
                    "Item ID", "Description", "Airflow (cfm)", "Static pressure in (in. w.c.)",
                    "Static pressure out (in. w.c.)", "Air system", "Temperature (deg F)",
                    "Pressure rate (in. w.c./100 ft)", "Velocity (fpm)", "Length (ft)", "Duct size"
                };
                var reportRows = grid.Rows.Cast<DataGridViewRow>().Select(row => (IReadOnlyList<string>)new[]
                {
                    Convert.ToString(row.Cells["Id"].Value) ?? "-",
                    Convert.ToString(row.Cells["Description"].Value) ?? "-",
                    Convert.ToString(row.Cells["Airflow"].Value) ?? "-",
                    Convert.ToString(row.Cells["PressureIn"].Value) ?? "-",
                    Convert.ToString(row.Cells["PressureOut"].Value) ?? "-",
                    Convert.ToString(row.Cells["AirSystem"].Value) ?? "-",
                    Convert.ToString(row.Cells["Temperature"].Value) ?? "-",
                    Convert.ToString(row.Cells["PressureRate"].Value) ?? "-",
                    Convert.ToString(row.Cells["Velocity"].Value) ?? "-",
                    Convert.ToString(row.Cells["Length"].Value) ?? "-",
                    Convert.ToString(row.Cells["Size"].Value) ?? "-"
                }).ToArray();
                DuctSystemPdf.Export(fullReportPath, "Duct System Schedule", "Full report verification",
                    reportHeaders, reportRows, total.Text.Replace("\r", string.Empty).Replace("\n", " — "),
                    new DuctSystemPdfMetadata("Alex Designer", "North Clinic Renovation", "M-24017", "September 27, 2026",
                        logo.Base64, logo.Width, logo.Height, "September 27, 2026 3:30 PM", fanNode.Id,
                        "1,000 cfm", "0.350 in. w.c.", "70%", "0.079 hp", "L-001 to L-006"),
                    pdfLayout, reportItemsWithTitus, pdfSchematics);
                var exportedPdf = File.ReadAllBytes(fullReportPath);
                var exportedText = PdfText.Plain(exportedPdf);
                var projectSummaryIndex = exportedText.IndexOf("Project Summary", StringComparison.Ordinal);
                var fanSheetIndex = exportedText.IndexOf($"{fanNode.Id} - FAN", StringComparison.Ordinal);
                var firstDuctSheetIndex = exportedText.IndexOf("D-001 - STRAIGHT DUCT", StringComparison.Ordinal);
                var firstSchematicIndex = exportedText.IndexOf("Layout Designer - Isometric Positive", StringComparison.Ordinal);
                var scheduleIndex = exportedText.LastIndexOf("Duct System Schedule", StringComparison.Ordinal);
                Require(exportedText.Contains("/Logo", StringComparison.Ordinal)
                        && exportedText.Contains("/DCTDecode", StringComparison.Ordinal)
                        && exportedText.Contains("/View0", StringComparison.Ordinal)
                        && exportedText.Contains("Project Summary", StringComparison.Ordinal)
                        && exportedText.Contains("Isometric Positive", StringComparison.Ordinal)
                        && exportedText.Contains("Critical Path", StringComparison.Ordinal)
                        && exportedText.Contains("GRAPHIC SCALE", StringComparison.Ordinal)
                        && exportedText.Contains("GRID SPACING:", StringComparison.Ordinal)
                        && exportedText.Contains("SHEET SCALE:", StringComparison.Ordinal)
                        && projectSummaryIndex >= 0
                        && firstSchematicIndex > projectSummaryIndex
                        && fanSheetIndex > firstSchematicIndex
                        && firstDuctSheetIndex > fanSheetIndex
                        && scheduleIndex > firstDuctSheetIndex,
                    "The PDF report should start with the overall summary and scaled system views, then retain the fan and remaining components in source-to-sink order before the schedules.");
                document.LayoutPdfBackground = null;
            }

            document.LayoutNodes.Remove(pressurePoint);
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);

            var layoutCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_LAYOUT_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(layoutCapturePath))
            {
                ClickButton(layoutNavigation);
                Application.DoEvents();
                CaptureForm(form, layoutCapturePath);
            }
            var directDuctEditorType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutDuctEditDialog")
                                       ?? throw new InvalidOperationException("Could not find the direct designer duct editor.");
            using (var directDuctEditor = (Form?)Activator.CreateInstance(
                       directDuctEditorType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object?[] { mainDuct1(), UnitSystem.Imperial,
                           document.DefaultSizingIncrementM, document.DefaultSizeRoundingDirection }, null)
                   ?? throw new InvalidOperationException("Could not create the direct designer duct editor."))
            {
                var designerSizingMethod = Find<ComboBox>(directDuctEditor, "LayoutDuctSizingMethodCombo");
                Require(Find<Button>(directDuctEditor, "LayoutDuctDirectUpdateButton").Text == "Update Duct"
                        && !Find<Button>(directDuctEditor, "LayoutDuctDirectUpdateButton").Enabled
                        && Find<Button>(directDuctEditor, "LayoutDuctCalculateButton").Text == "Calculate"
                        && Descendants<TextBox>(directDuctEditor).Count() >= 6
                        && designerSizingMethod.Items.Count == 3
                        && designerSizingMethod.SelectedIndex == 1
                        && designerSizingMethod.Items.Cast<object>().Select(Convert.ToString)
                            .SequenceEqual(new[] { "Check Existing", "Autosize (SP Loss)", "Autosize (Velocity)" })
                        && tabs.SelectedTab?.Name != "CalculatorTab",
                    "A designer duct should support check-size, static-pressure autosize, and velocity autosize without leaving Layout Designer.");
                directDuctEditor.Show(form);
                Application.DoEvents();
                Find<Button>(directDuctEditor, "LayoutDuctCalculateButton").PerformClick();
                Application.DoEvents();
                Require(Find<Button>(directDuctEditor, "LayoutDuctDirectUpdateButton").Enabled,
                    "The designer duct Update action should enable only after a successful calculation.");
                directDuctEditor.Hide();
                var directDuctCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_DIRECT_DUCT_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(directDuctCapturePath))
                {
                    directDuctEditor.Show(form);
                    Application.DoEvents();
                    CaptureForm(directDuctEditor, directDuctCapturePath);
                    directDuctEditor.Hide();
                }
            }

            var equipmentDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutEquipmentDialog")
                                      ?? throw new InvalidOperationException("Could not find the layout equipment editor.");
            Func<SavedLayoutNode, string> testCaption = layoutNode => layoutNode.Id;
            using (var equipmentDialog = (Form?)Activator.CreateInstance(
                       equipmentDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object?[] { coilNode, document.LayoutNodes, testCaption },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the layout equipment editor."))
            {
                var upstreamPicker = Find<ComboBox>(equipmentDialog, "LayoutUpstreamItemCombo");
                var downstreamItems = Find<Label>(equipmentDialog, "LayoutDownstreamItemsLabel");
                var airSystemPicker = Descendants<ComboBox>(equipmentDialog).Single(combo => combo != upstreamPicker);
                Require(upstreamPicker.Items.Count >= 2 && downstreamItems.Text.Contains(filterNode.Id, StringComparison.Ordinal)
                        && airSystemPicker.Items.Contains("Exhaust Air"),
                    "Opening a placed icon should show an editable upstream item and its downstream items.");
                Require(upstreamPicker.Items.Cast<object>().All(item =>
                            item.ToString() == "(No upstream item)"
                            || document.LayoutNodes.Any(node => node.Id == item.ToString()))
                        && downstreamItems.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .All(id => document.LayoutNodes.Any(node => node.Id == id)),
                    "Component relationship selectors should display only item ID numbers.");
                var equipmentDialogCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_EQUIPMENT_DIALOG_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(equipmentDialogCapturePath))
                {
                    equipmentDialog.Show(form);
                    Application.DoEvents();
                    CaptureForm(equipmentDialog, equipmentDialogCapturePath);
                    equipmentDialog.Hide();
                }
            }

            // Positions are stored absolute, but the dialogs show and accept the offset from the upstream component,
            // or from a downstream component when there is no upstream one.
            {
                var referenceType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutPositionReference")
                                    ?? throw new InvalidOperationException("Could not find the position reference type.");
                var absolute = new LayoutCoordinate(UnitConversions.FeetToM(30), UnitConversions.FeetToM(12), UnitConversions.FeetToM(9));
                var upstreamAt = new LayoutCoordinate(UnitConversions.FeetToM(20), UnitConversions.FeetToM(12), UnitConversions.FeetToM(9));
                foreach (var downstreamReference in new[] { false, true })
                {
                    var reference = Activator.CreateInstance(referenceType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new object[] { "REF-1", upstreamAt, downstreamReference }, null)!;
                    using var offsetDialog = (Form)Activator.CreateInstance(equipmentDialogType,
                        BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new object?[] { coilNode, document.LayoutNodes, testCaption, absolute, new LayoutEditableAxes(true, true, true), reference },
                        null)!;
                    var caption = Find<Label>(offsetDialog, "LayoutLocationCaption");
                    var xBox = Descendants<TextBox>(offsetDialog).First(box => box.Text == "10");
                    Require(caption.Text.StartsWith("Offset from REF-1", StringComparison.Ordinal)
                            && caption.Text.Contains("downstream", StringComparison.Ordinal) == downstreamReference,
                        $"The equipment dialog should label the position as an offset from the reference component. Caption: {caption.Text}");
                    xBox.Text = "14";
                    var save = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutEquipmentDialog")!
                        .GetMethod("SaveInputs", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(save is not null, "The equipment dialog save method should exist.");
                    {
                        var priorDelta = (coilNode.DeltaXM, coilNode.DeltaYM, coilNode.DeltaZM);
                        save!.Invoke(offsetDialog, null);
                        var desired = (LayoutCoordinate)equipmentDialogType.GetProperty("DesiredAbsoluteCoordinate",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(offsetDialog)!;
                        Require(Math.Abs(UnitConversions.MToFeet(desired.XM) - 34) < 1e-9
                                && Math.Abs(UnitConversions.MToFeet(desired.YM) - 12) < 1e-9,
                            "An entered offset should be converted back to an absolute position (reference + offset).");
                        (coilNode.DeltaXM, coilNode.DeltaYM, coilNode.DeltaZM) = priorDelta;
                    }
                }
            }

            var fittingDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutFittingNodeDialog")
                                    ?? throw new InvalidOperationException("Could not find the layout fitting editor.");
            var longDefinition = FittingCatalog.All.OrderByDescending(x => x.Description.Length).First();
            var longFitting = new SavedFitting
            {
                Sequence = 999,
                FittingCode = longDefinition.Code,
                Description = longDefinition.Description
            };
            var fittingLayoutNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(),
                Type = LayoutNodeType.Fitting,
                ParentNodeId = fanNode.Id,
                BranchId = "B-001",
                FittingGroup = "Diverting Fittings",
                BranchOutletBelow = true,
                LinkedFittingSequence = longFitting.Sequence
            };
            using (var fittingDialog = (Form?)Activator.CreateInstance(
                       fittingDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object?[] { fittingLayoutNode, document.LayoutNodes, testCaption, longFitting },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the layout fitting editor."))
            {
                fittingDialog.Show(form);
                Application.DoEvents();
                var description = Descendants<Label>(fittingDialog).Single(label =>
                    label.Text.Contains(longDefinition.Description, StringComparison.Ordinal));
                var locationLabel = Descendants<Label>(fittingDialog).Single(label => label.Text == "Relative ΔX / ΔY / ΔZ (ft)");
                var note = Descendants<Label>(fittingDialog).Single(label =>
                    label.Text.StartsWith("Changing the upstream item", StringComparison.Ordinal));
                var editFitting = Find<Button>(fittingDialog, "LayoutFittingEditButton");
                var measurement = TextRenderer.MeasureText(description.Text, description.Font,
                    new Size(Math.Max(1, description.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                var noteMeasurement = TextRenderer.MeasureText(note.Text, note.Font,
                    new Size(Math.Max(1, note.ClientSize.Width - note.Padding.Horizontal), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                Require(!description.AutoEllipsis && measurement.Height <= description.ClientSize.Height
                        && TextRenderer.MeasureText(locationLabel.Text, locationLabel.Font).Width <= locationLabel.ClientSize.Width
                        && !note.AutoEllipsis && noteMeasurement.Height <= note.ClientSize.Height - note.Padding.Vertical
                        && fittingDialog.Controls.Find("LayoutBranchOutletSideCombo", true).Length == 0
                        && editFitting.Text == "Edit",
                    $"The component popup should show the complete fitting description without clipping " +
                    $"(description needed {measurement.Height}px, available {description.ClientSize.Height}px; " +
                    $"note needed {noteMeasurement.Height}px, available {note.ClientSize.Height - note.Padding.Vertical}px; " +
                    $"location width {TextRenderer.MeasureText(locationLabel.Text, locationLabel.Font).Width}/{locationLabel.ClientSize.Width}; " +
                    $"ellipsis {description.AutoEllipsis}/{note.AutoEllipsis}; edit {editFitting.Text}).");
                var fittingDialogCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_FITTING_LAYOUT_DIALOG_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(fittingDialogCapturePath))
                {
                    CaptureForm(fittingDialog, fittingDialogCapturePath);
                }
                fittingDialog.Hide();
            }
            var savedDesignerFitting = document.Fittings.Single();
            var directFittingEditorType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutFittingEditDialog")
                                          ?? throw new InvalidOperationException("Could not find the direct designer fitting editor.");
            using (var directFittingEditor = (Form?)Activator.CreateInstance(
                       directFittingEditorType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object?[]
                       {
                           savedDesignerFitting,
                           FittingCatalog.SelectionGroup(FittingCatalog.Find(savedDesignerFitting.FittingCode)),
                           UnitSystem.Imperial,
                           null
                       }, null)
                   ?? throw new InvalidOperationException("Could not create the direct designer fitting editor."))
            {
                Require(Find<Button>(directFittingEditor, "LayoutFittingDirectUpdateButton").Text == "Update Fitting"
                        && !Find<Button>(directFittingEditor, "LayoutFittingDirectUpdateButton").Enabled
                        && Find<Button>(directFittingEditor, "LayoutFittingCalculateButton").Text == "Calculate"
                        && Find<Button>(directFittingEditor, "LayoutFittingGalleryButton").Text == "Fitting Gallery"
                        && Descendants<ComboBox>(directFittingEditor).Any()
                        && tabs.SelectedTab?.Name != "FittingTab",
                    "A designer fitting should use a compact Update editor without leaving Layout Designer.");
                directFittingEditor.Show(form);
                Application.DoEvents();
                Find<Button>(directFittingEditor, "LayoutFittingCalculateButton").PerformClick();
                Application.DoEvents();
                Require(Find<Button>(directFittingEditor, "LayoutFittingDirectUpdateButton").Enabled,
                    "The designer fitting Update action should enable only after a successful calculation.");
                directFittingEditor.Hide();
                var directFittingCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_DIRECT_FITTING_CAPTURE_PATH");
                if (!string.IsNullOrWhiteSpace(directFittingCapturePath))
                {
                    directFittingEditor.Show(form);
                    Application.DoEvents();
                    CaptureForm(directFittingEditor, directFittingCapturePath);
                    directFittingEditor.Hide();
                }
            }
            UiMember.Method("LoadFittingIntoCalculator", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { savedDesignerFitting });
            var loadedDefinition = fittingType.SelectedItem as FittingDefinition;
            Require(loadedDefinition is not null && loadedDefinition.Code == savedDesignerFitting.FittingCode
                    && Math.Abs(double.Parse(fittingAirflow.Text, System.Globalization.CultureInfo.CurrentCulture)
                                - UnitConversions.M3PerSToCfm(savedDesignerFitting.AirflowM3PerS)) < 0.01,
                "Opening a designer fitting should repopulate its fitting number, airflow, and saved calculation inputs.");
            fittingLayoutNode.GridX = 24;
            fittingLayoutNode.GridY = 32;
            var mainPortNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 32, GridY = 32, ParentNodeId = fittingLayoutNode.Id, BranchId = fittingLayoutNode.BranchId
            };
            var branchPortNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 24, GridY = 16, ParentNodeId = fittingLayoutNode.Id, BranchId = "B-002"
            };
            document.LayoutNodes.AddRange(new[] { fittingLayoutNode, mainPortNode, branchPortNode });
            // Round Z (no ports): each duct leaves the split straight toward the component it connects to.
            Point DuctStart(SavedLayoutNode downstream) => ((IReadOnlyList<Point>)connectionPathMethod.Invoke(layoutCanvas,
                new object[] { Activator.CreateInstance(layoutConnectionType, downstream, false)!, document.LayoutNodes })!)[0];
            var splitBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { fittingLayoutNode })!;
            var splitCentre = new Point(splitBounds.Left + splitBounds.Width / 2, splitBounds.Top + splitBounds.Height / 2);
            bool LeavesToward(SavedLayoutNode downstream)
            {
                var start = DuctStart(downstream);
                var targetBounds = (Rectangle)nodeBoundsMethod.Invoke(layoutCanvas, new object[] { downstream })!;
                var target = new Point(targetBounds.Left + targetBounds.Width / 2, targetBounds.Top + targetBounds.Height / 2);
                var along = (start.X - splitCentre.X) * (target.X - splitCentre.X) + (start.Y - splitCentre.Y) * (target.Y - splitCentre.Y);
                return along > 0;
            }
            var mainLeaves = LeavesToward(mainPortNode);
            var upperLeaves = LeavesToward(branchPortNode);
            var upperStart = DuctStart(branchPortNode);
            branchPortNode.GridY = 48;
            var lowerLeaves = LeavesToward(branchPortNode);
            var lowerStart = DuctStart(branchPortNode);
            Require(mainLeaves && upperLeaves && lowerLeaves && upperStart != lowerStart,
                "Each duct should leave a split toward the component it connects to (no connector ports).");
            document.LayoutNodes.Remove(fittingLayoutNode);
            document.LayoutNodes.Remove(mainPortNode);
            document.LayoutNodes.Remove(branchPortNode);

            var branchDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutBranchChoiceDialog")
                                   ?? throw new InvalidOperationException("Could not find the branch-path selector.");
            using (var branchDialog = (Form?)Activator.CreateInstance(
                       branchDialogType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object?[] { "L-100", "L-101" }, null)
                   ?? throw new InvalidOperationException("Could not create the branch-path selector."))
            {
                Require(Find<RadioButton>(branchDialog, "LayoutMainPathRadio").Checked
                        && Find<RadioButton>(branchDialog, "LayoutNewBranchRadio").Text.Contains("Branch path"),
                    "Diverting connections should use explicit Main path and Branch path radio choices.");
            }

            var ductDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutDuctConnectionDialog")
                                 ?? throw new InvalidOperationException("Could not find the layout duct editor.");
            using (var ductDialog = (Form?)Activator.CreateInstance(
                       ductDialogType,
                       BindingFlags.Instance | BindingFlags.NonPublic,
                       binder: null,
                       args: new object?[] { fanNode.Id, coilNode.Id, "Supply Air", true },
                       culture: null)
                   ?? throw new InvalidOperationException("Could not create the layout duct editor."))
            {
                Require(Find<Button>(ductDialog, "LayoutDuctEditButton").Text == "Edit",
                    "A sized designer duct popup should use the concise Edit action.");
                Require(ductDialog.Controls.Find("LayoutDuctReverseButton", true).Length == 0,
                    "Flow direction follows the sources and sinks: a duct offers no Reverse Flow Direction button (round Y).");
            }

            var rewire = UiMember.Method("RewireLayoutNode", BindingFlags.Instance | BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException("Could not access layout rewiring behavior.");
            var rewired = (bool)rewire.Invoke(form, new object[] { filterNode, fanNode.Id, true })!;
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            UiMember.Method("RefreshDuctSystemGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            Application.DoEvents();
            var rewireElbow = document.LayoutNodes.FirstOrDefault(x =>
                x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows"
                && x.ParentNodeId == filterNode.Id && coilNode.ParentNodeId == x.Id);
            // Round Z: elbows are never added on their own; a turn left at a component that cannot turn is a model error.
            Require(rewired && filterNode.ParentNodeId == fanNode.Id && rewireElbow is null
                    && coilNode.ParentNodeId == filterNode.Id
                    && filterNode.ConnectionDuctSequence is not null
                    && document.Ducts.All(x => x.Sequence != 1)
                    && document.Ducts.Where(x => x.Sequence == filterNode.ConnectionDuctSequence)
                        .All(x => Math.Abs(x.AirflowM3PerS - fanNode.AirflowM3PerS) < 1e-12),
                $"Changing an icon's upstream item should preserve the path, size the replacement run, and add no elbow by itself. " +
                $"rewired={rewired}; parents={filterNode.ParentNodeId}/{coilNode.ParentNodeId}; " +
                $"duct links={filterNode.ConnectionDuctSequence}/{coilNode.ConnectionDuctSequence}; " +
                $"ducts={string.Join(',', document.Ducts.Select(x => $"{x.Sequence}:{UnitConversions.M3PerSToCfm(x.AirflowM3PerS):0}"))}; " +
                $"fittings={string.Join(',', document.LayoutNodes.Where(x => x.Type == LayoutNodeType.Fitting).Select(x => $"{x.Id}<{x.ParentNodeId}[{x.FittingGroup}]"))}.");

            // Round Z: elbows go in only when the user chooses "add automatically"; they are then placed at the
            // corners of straight axis runs that continue the upstream inlet and meet the downstream outlet.
            var automaticElbowMethod = UiMember.Method("AddElbowsAlongSegments",
                                          BindingFlags.Instance | BindingFlags.NonPublic)
                                      ?? throw new InvalidOperationException("Could not inspect multi-axis routing behavior.");
            var automaticSegmentsMethod = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutDesignerScreen")!
                                              .GetMethod("AutomaticElbowSegments", BindingFlags.Static | BindingFlags.NonPublic)
                                          ?? throw new InvalidOperationException("Could not inspect automatic elbow routing.");
            IReadOnlyList<(double XM, double YM, double ZM)>? Segments(LayoutVector offset, LayoutVector? inlet, LayoutVector? outlet) =>
                (IReadOnlyList<(double XM, double YM, double ZM)>?)automaticSegmentsMethod.Invoke(null, new object?[] { offset, inlet, outlet });
            var tenFeet = UnitConversions.FeetToM(10);
            var inlineFirst = Segments(new LayoutVector(tenFeet, tenFeet, 0), new LayoutVector(0, 1, 0), null);
            var outletLast = Segments(new LayoutVector(tenFeet, tenFeet, 0), null, new LayoutVector(0, 1, 0));
            Require(inlineFirst is { Count: 2 } && Math.Abs(inlineFirst[0].YM - tenFeet) < 1e-10 && Math.Abs(inlineFirst[1].XM - tenFeet) < 1e-10
                    && outletLast is { Count: 2 } && Math.Abs(outletLast[1].YM - tenFeet) < 1e-10
                    && Segments(new LayoutVector(tenFeet, tenFeet, 0), new LayoutVector(0, 1, 0), new LayoutVector(0, 1, 0)) is null
                    && Segments(new LayoutVector(tenFeet, 0, 0), null, null) is null,
                "Automatic elbow routing should continue the inlet first, meet the outlet last, and refuse impossible routes.");
            var routeStart = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                GridX = 100, GridY = 100, AirSystemType = "Supply Air", BranchId = "Main",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(600), IsDefined = true
            };
            var routeEnd = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 100, GridY = 104, ParentNodeId = routeStart.Id,
                DeltaXM = UnitConversions.FeetToM(10), DeltaYM = UnitConversions.FeetToM(10),
                ConnectionDeltaConfirmed = true, AirSystemType = "Supply Air", BranchId = "Main", IsDefined = true
            };
            var preservedDownstream = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Coil,
                GridX = 102, GridY = 106, ParentNodeId = routeEnd.Id,
                DeltaXM = UnitConversions.FeetToM(10), ConnectionDeltaConfirmed = true,
                AirSystemType = "Supply Air", BranchId = "Main", IsDefined = true
            };
            document.LayoutNodes.AddRange(new[] { routeStart, routeEnd, preservedDownstream });
            var insertedElbows = ((IEnumerable<SavedLayoutNode>)automaticElbowMethod.Invoke(form,
                new object[] { routeEnd, false, new List<(double XM, double YM, double ZM)> { (tenFeet, 0, 0), (0, tenFeet, 0) } })!).ToArray();
            Require(insertedElbows.Length == 1
                    && insertedElbows[0].Type == LayoutNodeType.Fitting
                    && insertedElbows[0].FittingGroup == "Elbows"
                    && insertedElbows[0].ParentNodeId == routeStart.Id
                    && routeEnd.ParentNodeId == insertedElbows[0].Id
                    && preservedDownstream.ParentNodeId == routeEnd.Id
                    && Math.Abs(insertedElbows[0].DeltaXM - tenFeet) < 1e-10
                    && Math.Abs(insertedElbows[0].DeltaYM) < 1e-10
                    && Math.Abs(insertedElbows[0].ElbowAngleDegrees - 90.0) < 1e-10
                    && Math.Abs(routeEnd.DeltaXM) < 1e-10
                    && Math.Abs(routeEnd.DeltaYM - tenFeet) < 1e-10,
                "Adding elbows automatically should give two axial duct runs separated by one 90° elbow without disturbing downstream components.");

            var threeAxisEnd = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                GridX = 102, GridY = 102, ParentNodeId = routeStart.Id,
                DeltaXM = UnitConversions.FeetToM(5), DeltaYM = UnitConversions.FeetToM(-5),
                DeltaZM = UnitConversions.FeetToM(5), ConnectionDeltaConfirmed = true,
                AirSystemType = "Supply Air", BranchId = "Main", IsDefined = true
            };
            document.LayoutNodes.Add(threeAxisEnd);
            var fiveFeet = UnitConversions.FeetToM(5);
            var threeAxisElbows = ((IEnumerable<SavedLayoutNode>)automaticElbowMethod.Invoke(form,
                new object[] { threeAxisEnd, false, new List<(double XM, double YM, double ZM)> { (fiveFeet, 0, 0), (0, -fiveFeet, 0), (0, 0, fiveFeet) } })!).ToArray();
            Require(threeAxisElbows.Length == 2
                    && threeAxisElbows[0].ParentNodeId == routeStart.Id
                    && threeAxisElbows[1].ParentNodeId == threeAxisElbows[0].Id
                    && threeAxisEnd.ParentNodeId == threeAxisElbows[1].Id
                    && Math.Abs(threeAxisEnd.DeltaZM - fiveFeet) < 1e-10,
                "A three-axis connection should receive two elbows and three sequential axial runs when added automatically.");

            var gridPlacementDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutComponentPlacementDialog")
                                          ?? throw new InvalidOperationException("Could not inspect coordinate-grid validation.");
            var gridAlignment = gridPlacementDialogType.GetMethod("IsGridAligned",
                                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                                ?? throw new InvalidOperationException("Could not inspect coordinate-grid validation.");
            Require((bool)gridAlignment.Invoke(null, new object[] { 10.0, 5.0 })!
                    && !(bool)gridAlignment.Invoke(null, new object[] { 7.0, 5.0 })!
                    && (bool)gridAlignment.Invoke(null, new object[] { -15.0, 5.0 })!,
                "Typed relative coordinates should accept signed grid increments and reject between-grid values.");
            foreach (var testNode in insertedElbows.Concat(threeAxisElbows)
                         .Concat(new[] { routeStart, routeEnd, preservedDownstream, threeAxisEnd }).ToArray())
                document.LayoutNodes.Remove(testNode);

            var splitPreviewNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Diverting Fittings", GridX = 30, GridY = 12,
                AirSystemType = "Supply Air", BranchId = "Main", BranchOutletBelow = true
            };
            var splitSameBranch = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Terminals", GridX = 36, GridY = 12, ParentNodeId = splitPreviewNode.Id,
                AirSystemType = "Supply Air", BranchId = "Main"
            };
            var splitNewBranch = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Terminals", GridX = 30, GridY = 28, ParentNodeId = splitPreviewNode.Id,
                AirSystemType = "Supply Air", BranchId = "B-001"
            };
            var outletIndexFor = UiMember.Method("OutletIndexFor", BindingFlags.Instance | BindingFlags.NonPublic)
                                 ?? throw new InvalidOperationException("Could not inspect split outlet selection.");
            document.LayoutNodes.AddRange(new[] { splitPreviewNode, splitSameBranch });
            var afterMain = (int)outletIndexFor.Invoke(form, new object[] { splitPreviewNode, new LayoutVector(0, 1, 0) })!;
            document.LayoutNodes.Remove(splitSameBranch);
            document.LayoutNodes.Add(splitNewBranch);
            var afterBranch = (int)outletIndexFor.Invoke(form, new object[] { splitPreviewNode, new LayoutVector(1, 0, 0) })!;
            document.LayoutNodes.Remove(splitPreviewNode);
            document.LayoutNodes.Remove(splitNewBranch);
            Require(afterMain == 1 && afterBranch == 0,
                "A second split connection should automatically use the only remaining main or branch outlet.");
            document.LayoutNodes.AddRange(new[] { splitPreviewNode, splitSameBranch, splitNewBranch });
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            ClickButton(layoutNavigation);
            Application.DoEvents();
            var splitCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SPLIT_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(splitCapturePath))
            {
                CaptureForm(form, splitCapturePath);
            }
            document.LayoutNodes.Remove(splitPreviewNode);
            document.LayoutNodes.Remove(splitSameBranch);
            document.LayoutNodes.Remove(splitNewBranch);

            var secondarySource = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
                GridX = 14, GridY = 36, AirSystemType = "Supply Air", BranchId = "Main",
                AirflowM3PerS = UnitConversions.CfmToM3PerS(400)
            };
            var convergingNode = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Converging Fittings", GridX = 28, GridY = 24,
                ParentNodeId = coilNode.Id, AirSystemType = "Supply Air", BranchId = "Main",
                DeltaXM = UnitConversions.FeetToM(10), DeltaYM = 0,
                ConnectionDeltaConfirmed = true
            };
            document.LayoutNodes.Add(secondarySource);
            document.LayoutNodes.Add(convergingNode);
            var convergingBeforeSecondInlet = LayoutCoordinateResolver.Resolve(document)[convergingNode.Sequence];
            var primaryDeltaBeforeSecondInlet = (convergingNode.DeltaXM, convergingNode.DeltaYM,
                convergingNode.DeltaZM, convergingNode.ParentNodeId);
            // Round Z: a duct drawn with Duct Segment from the fan to the merge joins them directly (centre to centre,
            // diagonal if need be) through the merge's free second inlet; no elbow is added on its own.
            var elbowsBeforeSecondInlet = document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows");
            Require((bool)UiMember.Method("DrawDuctBetween", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, new object[] { secondarySource, convergingNode, false })!,
                "Drawing a duct from a fan to a merge's free second inlet should connect them.");
            Application.DoEvents();
            var secondaryPathElbow = document.LayoutNodes.FirstOrDefault(x =>
                x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows"
                && x.ParentNodeId == secondarySource.Id && convergingNode.SecondaryParentNodeId == x.Id);
            var convergingAfterSecondInlet = LayoutCoordinateResolver.Resolve(document)[convergingNode.Sequence];
            Require(secondaryPathElbow is null
                    && convergingNode.SecondaryParentNodeId == secondarySource.Id
                    && document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows") == elbowsBeforeSecondInlet
                    && convergingNode.ParentNodeId == primaryDeltaBeforeSecondInlet.ParentNodeId
                    && Math.Abs(convergingNode.DeltaXM - primaryDeltaBeforeSecondInlet.DeltaXM) < 1e-10
                    && Math.Abs(convergingNode.DeltaYM - primaryDeltaBeforeSecondInlet.DeltaYM) < 1e-10
                    && Math.Abs(convergingNode.DeltaZM - primaryDeltaBeforeSecondInlet.DeltaZM) < 1e-10
                    && Math.Abs(convergingAfterSecondInlet.XM - convergingBeforeSecondInlet.XM) < 1e-10
                    && Math.Abs(convergingAfterSecondInlet.YM - convergingBeforeSecondInlet.YM) < 1e-10
                    && Math.Abs(convergingAfterSecondInlet.ZM - convergingBeforeSecondInlet.ZM) < 1e-10
                    && (Math.Abs(convergingNode.SecondaryDeltaXM) > 1e-10
                        || Math.Abs(convergingNode.SecondaryDeltaYM) > 1e-10
                        || Math.Abs(convergingNode.SecondaryDeltaZM) > 1e-10)
                    && FittingCatalog.InletPortCount(convergingNode.FittingGroup) == 2
                    && FittingCatalog.OutletPortCount("Converging Fittings") == 1
                    && FittingCatalog.InletPortCount("Diverting Fittings") == 1
                    && FittingCatalog.OutletPortCount("Diverting Fittings") == 2
                    && FittingCatalog.InletPortCount("Elbows") == 1
                    && FittingCatalog.OutletPortCount("Elbows") == 1,
                "A duct drawn to a merge's second inlet should join it directly, keep the first inlet and the fitting's position, and add no elbow.");

            var convergingOutletTarget = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Elbows", GridX = 40, GridY = 24,
                HasAbsoluteCoordinateAnchor = true,
                AbsoluteAnchorXM = convergingAfterSecondInlet.XM + UnitConversions.FeetToM(10),
                AbsoluteAnchorYM = convergingAfterSecondInlet.YM,
                AbsoluteAnchorZM = convergingAfterSecondInlet.ZM,
                AirSystemType = "Supply Air", BranchId = "Main"
            };
            var existingOutletChild = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
                ParentNodeId = convergingOutletTarget.Id,
                DeltaXM = UnitConversions.FeetToM(5), ConnectionDeltaConfirmed = true,
                AirSystemType = "Supply Air", BranchId = "Main"
            };
            document.LayoutNodes.AddRange(new[] { convergingOutletTarget, existingOutletChild });
            var outletCoordinatesBefore = LayoutCoordinateResolver.Resolve(document);
            var targetBefore = outletCoordinatesBefore[convergingOutletTarget.Sequence];
            var childBefore = outletCoordinatesBefore[existingOutletChild.Sequence];
            var elbowCountBeforeOutletConnection = document.LayoutNodes.Count(x =>
                x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows");
            UiMember.Method("ConnectLayoutPorts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { convergingNode, convergingOutletTarget, 0, 0 });
            Application.DoEvents();
            var outletCoordinatesAfter = LayoutCoordinateResolver.Resolve(document);
            var targetAfter = outletCoordinatesAfter[convergingOutletTarget.Sequence];
            var childAfter = outletCoordinatesAfter[existingOutletChild.Sequence];
            var ensureCoordinates = UiMember.Method("EnsureConnectedCoordinateDeltas",
                                        BindingFlags.Instance | BindingFlags.NonPublic)
                                    ?? throw new InvalidOperationException("Could not inspect edit-time coordinate stability.");
            Require((bool)ensureCoordinates.Invoke(form, new object[] { convergingOutletTarget })!,
                "An already connected converging outlet should remain valid when its fitting is edited.");
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null);
            var outletCoordinatesAfterEdit = LayoutCoordinateResolver.Resolve(document);
            Require(convergingOutletTarget.ParentNodeId == convergingNode.Id
                    && Math.Abs(targetAfter.XM - targetBefore.XM) < 1e-10
                    && Math.Abs(targetAfter.YM - targetBefore.YM) < 1e-10
                    && Math.Abs(targetAfter.ZM - targetBefore.ZM) < 1e-10
                    && Math.Abs(childAfter.XM - childBefore.XM) < 1e-10
                    && Math.Abs(childAfter.YM - childBefore.YM) < 1e-10
                    && Math.Abs(childAfter.ZM - childBefore.ZM) < 1e-10
                    && Math.Abs(outletCoordinatesAfterEdit[convergingOutletTarget.Sequence].XM - targetBefore.XM) < 1e-10
                    && Math.Abs(outletCoordinatesAfterEdit[existingOutletChild.Sequence].XM - childBefore.XM) < 1e-10
                    && document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting
                                                       && x.FittingGroup == "Elbows") == elbowCountBeforeOutletConnection,
                "Connecting and then editing a converging outlet must preserve the target and its existing downstream coordinates without inserting another elbow.");
            document.LayoutNodes.Remove(existingOutletChild);
            document.LayoutNodes.Remove(convergingOutletTarget);

            var directlyBelowTarget = new SavedLayoutNode
            {
                Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting,
                FittingGroup = "Elbows", GridX = 28, GridY = 44,
                HasAbsoluteCoordinateAnchor = true,
                AbsoluteAnchorXM = convergingAfterSecondInlet.XM + UnitConversions.FeetToM(10),
                AbsoluteAnchorYM = convergingAfterSecondInlet.YM + UnitConversions.FeetToM(10),
                AbsoluteAnchorZM = convergingAfterSecondInlet.ZM,
                AirSystemType = "Supply Air", BranchId = "Main"
            };
            document.LayoutNodes.Add(directlyBelowTarget);
            var belowBefore = LayoutCoordinateResolver.Resolve(document)[directlyBelowTarget.Sequence];
            var belowElbowCount = document.LayoutNodes.Count(x =>
                x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows");
            UiMember.Method("ConnectLayoutPorts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { convergingNode, directlyBelowTarget, 0, 0 });
            Application.DoEvents();
            var belowAfter = LayoutCoordinateResolver.Resolve(document)[directlyBelowTarget.Sequence];
            static (double Column, double Row) Project(LayoutCoordinate coordinate)
            {
                var x = UnitConversions.MToFeet(coordinate.XM);
                var y = UnitConversions.MToFeet(coordinate.YM);
                var z = UnitConversions.MToFeet(coordinate.ZM);
                return (x - y, x + y - 2.0 * z);
            }
            var belowProjectionBefore = Project(belowBefore);
            var belowProjectionAfter = Project(belowAfter);
            Require(Math.Abs(directlyBelowTarget.DeltaXM) < 1e-10
                    && Math.Abs(directlyBelowTarget.DeltaYM) < 1e-10
                    && Math.Abs(UnitConversions.MToFeet(directlyBelowTarget.DeltaZM) + 10.0) < 1e-9
                    && Math.Abs(belowProjectionAfter.Column - belowProjectionBefore.Column) < 1e-9
                    && Math.Abs(belowProjectionAfter.Row - belowProjectionBefore.Row) < 1e-9
                    && document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting
                                                       && x.FittingGroup == "Elbows") == belowElbowCount,
                "A component placed directly below a converging outlet should resolve as one Z-axis run, stay at the same drawn location, and not receive a redundant elbow.");
            document.LayoutNodes.Remove(directlyBelowTarget);

            // Round Z: Duct Segment / Flex Duct Segment. Press on a component, drag to another, release: one straight duct,
            // centre to centre. A diagonal joins two elbows directly (no elbow added); a sloping duct and an elbow
            // needed at a component that cannot turn the air are asked about first.
            var ductSegmentButton = Find<Button>(form, "RibbonDuctSegmentButton");
            var flexSegmentButton = Find<Button>(form, "RibbonFlexDuctSegmentButton");
            Require(ductSegmentButton.Text == "Duct\nSegment" && flexSegmentButton.Text == "Flex Duct\nSegment"
                    && Find<Control>(form, "RibbonDuctworkGroup").Contains(ductSegmentButton),
                "The ribbon should offer Duct Segment and Flex Duct Segment in a Ductwork group.");
            var ductOrigin = LayoutCoordinateResolver.Resolve(document)[convergingNode.Sequence];
            SavedLayoutNode Anchored(LayoutNodeType type, string group, double xFeet, double yFeet, double zFeet) => new()
            {
                Sequence = document.ReserveLayoutSequence(), Type = type, FittingGroup = group,
                HasAbsoluteCoordinateAnchor = true,
                AbsoluteAnchorXM = ductOrigin.XM + UnitConversions.FeetToM(xFeet),
                AbsoluteAnchorYM = ductOrigin.YM + UnitConversions.FeetToM(yFeet),
                AbsoluteAnchorZM = ductOrigin.ZM + UnitConversions.FeetToM(zFeet),
                AirSystemType = "Supply Air", BranchId = "Main"
            };
            var drawFan = Anchored(LayoutNodeType.Fan, string.Empty, 40, 40, 0);
            var drawElbowA = Anchored(LayoutNodeType.Fitting, "Elbows", 40, 20, 0);
            var drawElbowB = Anchored(LayoutNodeType.Fitting, "Elbows", 60, 0, 0);
            document.LayoutNodes.AddRange(new[] { drawFan, drawElbowA, drawElbowB });
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            ClickButton(flexSegmentButton);
            Application.DoEvents();
            Require(form.LayoutScreen.ActiveDuctTool == LayoutDuctTool.Flex
                    && (LayoutDuctTool)layoutCanvas.GetType().GetProperty("DuctTool", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layoutCanvas)! == LayoutDuctTool.Flex
                    && flexSegmentButton is RibbonButton { Checked: true } && !Find<RibbonButton>(form, "RibbonSelectButton").Checked,
                "Flex Duct Segment should switch the drawing to flex duct drawing.");
            var centerMethod = layoutCanvas.GetType().GetMethod("Center", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(SavedLayoutNode) }, null)!;
            void DragDuct(SavedLayoutNode from, SavedLayoutNode to)
            {
                var start = (Point)centerMethod.Invoke(layoutCanvas, new object[] { from })!;
                var end = (Point)centerMethod.Invoke(layoutCanvas, new object[] { to })!;
                layoutCanvas.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0) });
                layoutCanvas.GetType().GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0) });
                layoutCanvas.GetType().GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(layoutCanvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0) });
                Application.DoEvents();
            }
            DragDuct(drawFan, drawElbowA);
            DragDuct(drawElbowA, drawElbowB);
            var elbowCountAfterDiagonal = document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows");
            Require(drawElbowA.ParentNodeId == drawFan.Id && drawElbowB.ParentNodeId == drawElbowA.Id
                    && drawElbowA.ConnectionFlexDuct && drawElbowB.ConnectionFlexDuct
                    && Math.Abs(UnitConversions.MToFeet(drawElbowB.DeltaXM) - 20) < 1e-6
                    && Math.Abs(UnitConversions.MToFeet(drawElbowB.DeltaYM) + 20) < 1e-6
                    && Math.Abs(drawElbowB.DeltaZM) < 1e-9,
                $"Dragging from one component to the next should draw one straight flex duct centre to centre (here a 45° diagonal). Elbow B parent={drawElbowB.ParentNodeId}, offset=({UnitConversions.MToFeet(drawElbowB.DeltaXM):0.##}, {UnitConversions.MToFeet(drawElbowB.DeltaYM):0.##}, {UnitConversions.MToFeet(drawElbowB.DeltaZM):0.##}) ft.");
            form.LayoutScreen.UpdateCalculatedLayoutValues();
            Require(Math.Abs(drawElbowA.ElbowAngleDegrees - 45) < 0.05,
                $"The elbow between a straight run and the 45° diagonal should be a 45° elbow from the drawing. Was {drawElbowA.ElbowAngleDegrees}°.");
            ClickButton(Find<Button>(form, "RibbonSelectButton"));
            Require(form.LayoutScreen.ActiveDuctTool == LayoutDuctTool.None, "Select should end duct drawing.");

            // An elbow needed at a transition: declined → nothing drawn; accepted → elbows added at the corners.
            var drawTransition = Anchored(LayoutNodeType.Fitting, "Transitions", 100, 0, 0);
            var drawAfterTransition = Anchored(LayoutNodeType.Fitting, "Dampers", 100, 20, 0);
            var drawOffset = Anchored(LayoutNodeType.Fitting, "Elbows", 120, 30, 0);
            document.LayoutNodes.AddRange(new[] { drawTransition, drawAfterTransition, drawOffset });
            Require(form.LayoutScreen.DrawDuctBetween(drawTransition, drawAfterTransition, flex: false),
                "A straight duct from a transition to a damper should draw.");
            form.LayoutScreen.AutomatedConnectionAnswer = false;
            Require(!form.LayoutScreen.DrawDuctBetween(drawAfterTransition, drawOffset, flex: false)
                    && string.IsNullOrEmpty(drawOffset.ParentNodeId),
                "Declining the elbow question should draw nothing.");
            form.LayoutScreen.AutomatedConnectionAnswer = true;
            var elbowsBeforeAutomatic = document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows");
            Require(form.LayoutScreen.DrawDuctBetween(drawAfterTransition, drawOffset, flex: false)
                    && document.LayoutNodes.Count(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows") == elbowsBeforeAutomatic + 1
                    && document.LayoutNodes.Single(x => x.Id == drawOffset.ParentNodeId) is { FittingGroup: "Elbows" } addedElbow
                    && addedElbow.ParentNodeId == drawAfterTransition.Id
                    && Math.Abs(UnitConversions.MToFeet(addedElbow.DeltaYM) - 10) < 1e-6 && Math.Abs(addedElbow.DeltaXM) < 1e-9,
                "Accepting should add one elbow, continuing straight out of the damper first and then turning.");
            Require(LayoutDirections.MissingElbows(document).All(x => x.Node != drawAfterTransition),
                "After the automatic elbow the damper no longer turns the air.");

            // A sloping duct is drawn only when accepted.
            var slopeStart = Anchored(LayoutNodeType.Fan, string.Empty, 160, 0, 0);
            var slopeEnd = Anchored(LayoutNodeType.Fitting, "Elbows", 170, 0, 5);
            document.LayoutNodes.AddRange(new[] { slopeStart, slopeEnd });
            form.LayoutScreen.AutomatedConnectionAnswer = false;
            Require(!form.LayoutScreen.DrawDuctBetween(slopeStart, slopeEnd, flex: false) && string.IsNullOrEmpty(slopeEnd.ParentNodeId),
                "Declining the slope question should draw nothing.");
            form.LayoutScreen.AutomatedConnectionAnswer = true;
            Require(form.LayoutScreen.DrawDuctBetween(slopeStart, slopeEnd, flex: false) && slopeEnd.ParentNodeId == slopeStart.Id
                    && Math.Abs(UnitConversions.MToFeet(slopeEnd.DeltaZM) - 5) < 1e-6,
                "Accepting the slope should draw the sloping duct.");
            foreach (var drawn in new[] { drawFan, drawElbowA, drawElbowB, drawTransition, drawAfterTransition, drawOffset, slopeStart, slopeEnd })
                document.LayoutNodes.Remove(drawn);
            document.LayoutNodes.RemoveAll(x => x.Type == LayoutNodeType.Fitting && x.FittingGroup == "Elbows"
                                                && (x.ParentNodeId == drawAfterTransition.Id));
            UiMember.Method("RefreshLayoutDesigner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var portCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_PORT_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(portCapturePath))
            {
                ClickButton(layoutNavigation);
                form.Size = Sz(form, 1280, 820);
                Application.DoEvents();
                CaptureForm(form, portCapturePath);
            }

            var capturePath = Environment.GetEnvironmentVariable("DUCTSIZER_UI_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                tabs.SelectedTab = tabs.TabPages[0];
                mode.SelectedIndex = 1;
                form.Size = Sz(form, 1280, 820);
                form.Show();
                Application.DoEvents();
                CaptureForm(form, capturePath);
            }
            var systemCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SYSTEM_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(systemCapturePath))
            {
                tabs.SelectedTab = tabs.TabPages[2];
                form.Size = Sz(form, 1280, 820);
                Application.DoEvents();
                CaptureForm(form, systemCapturePath);
            }
            var fittingCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_FITTING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(fittingCapturePath))
            {
                tabs.SelectedTab = tabs.TabPages[1];
                fittingGroup.SelectedItem = "Diverting Fittings";
                Application.DoEvents();
                fittingType.SelectedIndex = fittingType.Items.Cast<object>()
                    .Select((item, index) => (item, index))
                    .First(x => x.item.ToString()?.StartsWith("5-24 ", StringComparison.Ordinal) == true).index;
                form.Size = Sz(form, 1280, 820);
                UiMember.Method("ResetFittingInputScroll", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, null);
                Application.DoEvents();
                Application.DoEvents();
                var selectorBounds = ScreenBounds(fittingGroup);
                var scrollBounds = ScreenBounds(fittingInputScroll);
                Require(selectorBounds.Top >= scrollBounds.Top && selectorBounds.Bottom <= scrollBounds.Bottom,
                    "Changing to a junction fitting should retain the fitting selector and common inputs at the top of the panel.");
                var selectorRowIndex = fittingBaseInputs.GetRow(fittingSelectorPanel);
                Require(ScreenBounds(fittingSelectorPanel).Height >= 220
                        && fittingBaseInputs.GetRowHeights()[selectorRowIndex] >= 220,
                    $"The fitting selector row must not collapse and clip its group, number, common inputs, or preview image. " +
                    $"Selector row={fittingBaseInputs.GetRow(fittingSelectorPanel)}, height={ScreenBounds(fittingSelectorPanel).Height}, " +
                    $"rows={string.Join(',', fittingBaseInputs.GetRowHeights())}, styles={string.Join(',', fittingBaseInputs.RowStyles.Cast<RowStyle>().Select(style => $"{style.SizeType}:{style.Height}"))}.");
                CaptureForm(form, fittingCapturePath);
            }
            var spacingCapturePath = Environment.GetEnvironmentVariable("DUCTSIZER_SPACING_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(spacingCapturePath))
            {
                tabs.SelectedTab = tabs.TabPages[1];
                fittingGroup.SelectedItem = "Elbows";
                Application.DoEvents();
                fittingType.SelectedIndex = fittingType.Items.Cast<FittingDefinition>()
                    .Select((definition, index) => (definition, index))
                    .First(item => item.definition.Code == "3-5").index;
                form.Size = Sz(form, 1280, 820);
                Application.DoEvents();
                fittingInputScroll.AutoScrollPosition = new Point(0, 255);
                Application.DoEvents();
                CaptureForm(form, spacingCapturePath);
            }
            UiMember.Method("ClearAllSystemData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            Application.DoEvents();
            Require(document.Ducts.Count == 0 && document.Fittings.Count == 0 && document.LayoutNodes.Count == 0
                    && grid.Rows.Count == 0,
                "Deleting all system items should clear the table and its graphical layout together.");
            form.Hide();

            // Open Project (run last: it replaces the form's model object that earlier checks hold).
            {
                var savedProject = ProjectFile.Load(greenSaveProjectPath);
                AutomationOptions.OpenProjectPath = greenSaveProjectPath;
                try
                {
                    UiMember.Method("OpenProject", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(form, null);
                }
                finally
                {
                    AutomationOptions.OpenProjectPath = null;
                }
                Application.DoEvents();
                var openedModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                Require(openedModel.LayoutNodes.Count == savedProject.LayoutNodes.Count
                        && openedModel.LayoutNodes.Count > 0
                        && openedModel.Ducts.Count == savedProject.Ducts.Count
                        && openedModel.Fittings.Count == savedProject.Fittings.Count
                        && (string)UiMember.Field("_lastExportBasePath",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)! == greenSaveBasePath,
                    "Open Project should restore the full model and make later saves write back to the opened project.");
            }

            // v0.96 ribbon commands: tabs, Round Sizes, Auto Size, Calculate, Highlight, New Project.
            var mainRibbon = typeof(MainForm).GetProperty("Ribbon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            string ActiveRibbonTab() => mainRibbon.GetType().GetProperty("ActiveTab", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(mainRibbon)!.ToString()!;
            ClickButton(Find<Button>(form, "TopTabAnalysis"));
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "DuctSystemTab" && ActiveRibbonTab() == "Analysis",
                $"The Analysis tab should show the system table with the Analysis ribbon. Page={tabs.SelectedTab?.Name}, Ribbon={ActiveRibbonTab()}");
            ClickButton(Find<Button>(form, "TopTabReports"));
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "ReportsTab" && ActiveRibbonTab() == "Reports"
                    && Find<Button>(form, "RibbonPrintReportButton").Text.Replace("\n", " ", StringComparison.Ordinal) == "Print Report",
                "The Reports tab should show the report page with Print Report.");
            ClickButton(Find<Button>(form, "RibbonFittingCalculatorButton"));
            Application.DoEvents();
            ClickButton(Find<Button>(form, "TopTabProject"));
            Application.DoEvents();
            ClickButton(Find<Button>(form, "TopTabQuickCalc"));
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "FittingLossTab"
                    && ((dynamic)Find<Button>(form, "RibbonFittingCalculatorButton")).Checked,
                "Quick Calc should return to the calculator last used.");
            ClickButton(Find<Button>(form, "TopTabProject"));
            Application.DoEvents();
            Require(tabs.SelectedTab?.Name == "LayoutDesignerTab", "The Project tab should show the Layout Designer.");

            var ribbonModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            // Round X: duct size rounding and the calculation method are project-wide pages of Auto Sizing Criteria.
            var projectSizingType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.ProjectSizingSettings")!;
            var projectSizing = Activator.CreateInstance(projectSizingType, UnitConversions.InchesToM(0.5), SizeRoundingDirection.Down,
                EquivalentDiameterMethod.HydraulicDiameter, PressureLossBasis.ActualDuctArea)!;
            var sizingDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.AutomaticSizingInputsDialog")!;
            using (var criteria = (Form)Activator.CreateInstance(sizingDialogType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { new AutomaticSizingSettings(), UnitSystem.Imperial, projectSizing }, null)!)
            {
                criteria.Show(form);
                Application.DoEvents();
                var pagesList = Find<ListBox>(criteria, "AutomaticSizingComponentList");
                Require(pagesList.Items.Cast<object>().Select(item => item.ToString()).Take(5)
                            .SequenceEqual(new[] { "Project-wide", "Duct Size Rounding", "Calculation Method", "Component defaults", "Duct" })
                        && criteria.Text == "Auto Sizing Criteria",
                    "Auto Sizing Criteria should list the project-wide Duct Size Rounding and Calculation Method pages above the component defaults.");
                pagesList.SelectedItem = "Calculation Method";
                Application.DoEvents();
                Require(Find<ComboBox>(criteria, "SizingEquivalentMethod").SelectedItem?.ToString() == "Hydraulic Diameter"
                        && Find<ComboBox>(criteria, "SizingPressureBasis").SelectedItem?.ToString() == "Actual Duct Area",
                    "The Calculation Method page should restore the project's equivalent-diameter and duct-area methods.");
                pagesList.SelectedItem = "Duct Size Rounding";
                Application.DoEvents();
                var incrementBox = Find<ComboBox>(criteria, "SizingRoundingIncrement");
                Require(incrementBox.Text == "0.5" && Find<ComboBox>(criteria, "SizingRoundingDirection").SelectedItem?.ToString() == "Round Down",
                    "The Duct Size Rounding page should restore the project's increment and rounding direction.");
                incrementBox.Text = "13";
                var saveSizing = Find<Button>(criteria, "AutomaticSizingSaveButton");
                var refused = (bool)sizingDialogType.GetMethod("SaveCurrent", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(criteria, new object[] { false })!;
                Require(!refused, "A rounding increment above 12 in. should be refused.");
                incrementBox.Text = "2";
                ClickButton(saveSizing);
                Application.DoEvents();
                dynamic savedSizing = sizingDialogType.GetProperty("ProjectSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(criteria)!;
                Require(criteria.DialogResult == DialogResult.OK
                        && Math.Abs((double)savedSizing.IncrementM - UnitConversions.InchesToM(2)) < 1e-12
                        && (EquivalentDiameterMethod)savedSizing.EquivalentDiameterMethod == EquivalentDiameterMethod.HydraulicDiameter,
                    "Saving Auto Sizing Criteria should return the new rounding increment and keep the calculation method.");
            }

            ClickButton(Find<Button>(form, "RibbonAutoSizeButton"));
            Application.DoEvents();
            var layoutStatus = Find<Label>(form, "LayoutDesignerStatus");
            Require(layoutStatus.Text.StartsWith("Auto Size Ductwork", StringComparison.Ordinal)
                    || layoutStatus.Text.StartsWith("There are no", StringComparison.Ordinal),
                $"Auto Size Ductwork should report what it sized. Status={layoutStatus.Text}");

            // Round Y: size locks. Auto Size Ductwork keeps a locked duct's size; the Component Lock Editor lists every
            // duct and component, with Lock All / Unlock All.
            var lockModel = form.DuctSystemScreen._ductSystem;
            var lockTarget = lockModel.LayoutNodes.Where(node => node.ConnectionDuctSequence is not null)
                .Select(node => lockModel.Ducts.FirstOrDefault(duct => duct.Sequence == node.ConnectionDuctSequence))
                .FirstOrDefault(duct => duct is not null);
            if (lockTarget is not null)
            {
                lockTarget.Shape = DuctShape.Round;
                lockTarget.DiameterM = UnitConversions.InchesToM(7.3);
                form.LayoutScreen.ApplySizeLocks(new Dictionary<string, bool> { [DuctSystemScreen.DuctKey(lockTarget.Sequence)] = true });
                ClickButton(Find<Button>(form, "RibbonAutoSizeButton"));
                Application.DoEvents();
                Require(lockTarget.SizeLocked && Math.Abs(UnitConversions.MToInches(lockTarget.DiameterM) - 7.3) < 1e-9
                        && layoutStatus.Text.Contains("locked item", StringComparison.Ordinal),
                    $"Auto Size Ductwork should keep a locked duct's size and say so. Size={UnitConversions.MToInches(lockTarget.DiameterM):0.###} in, Status={layoutStatus.Text}");
                form.LayoutScreen.ApplySizeLocks(new Dictionary<string, bool> { [DuctSystemScreen.DuctKey(lockTarget.Sequence)] = false });
                ClickButton(Find<Button>(form, "RibbonAutoSizeButton"));
                Application.DoEvents();
                Require(!lockTarget.SizeLocked && Math.Abs(UnitConversions.MToInches(lockTarget.DiameterM) - 7.3) > 1e-6,
                    "Once unlocked, Auto Size Ductwork re-sizes the duct from the criteria.");
            }
            var lockRows = form.LayoutScreen.ComponentLockRows();
            Require(lockRows.Count > 0 && lockRows.Any(row => row.Type == "Duct")
                    && lockRows.All(row => row.Type != "Fan" && !row.ItemId.StartsWith("S-", StringComparison.Ordinal)),
                "The Component Lock Editor should list the ducts and components Auto Size can change (not fans or calculation points).");
            Require(Find<Control>(form, "LayoutComponentLockEditorButton").Parent == Find<Control>(form, "LayoutSourceSinkEditorButton").Parent
                    && Find<Control>(form, "LayoutComponentLockEditorButton").Parent!.Controls.GetChildIndex(Find<Control>(form, "LayoutComponentLockEditorButton"))
                       == Find<Control>(form, "LayoutSourceSinkEditorButton").Parent!.Controls.GetChildIndex(Find<Control>(form, "LayoutSourceSinkEditorButton")) + 1,
                "The Component Lock Editor button should sit next to the Source / Sink Editor.");
            var lockDialogType = typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.ComponentLockEditorDialog")!;
            using (var lockDialog = (Form)Activator.CreateInstance(lockDialogType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                       new object[] { lockRows }, null)!)
            {
                lockDialog.CreateControl();
                var lockGrid = Find<DataGridView>(lockDialog, "ComponentLockGrid");
                Require(lockGrid.Rows.Count == lockRows.Count && Find<Button>(lockDialog, "ComponentLockAllButton").Text == "Lock All"
                        && Find<Button>(lockDialog, "ComponentUnlockAllButton").Text == "Unlock All",
                    "The Component Lock Editor should list every row with Lock All and Unlock All at the top.");
                ClickButton(Find<Button>(lockDialog, "ComponentLockAllButton"));
                Require(Find<Label>(lockDialog, "ComponentLockCount").Text == $"{lockRows.Count} of {lockRows.Count} locked",
                    "Lock All should tick every row.");
                ClickButton(Find<Button>(lockDialog, "ComponentLockSaveButton"));
                var locks = (IReadOnlyDictionary<string, bool>)lockDialogType.GetProperty("Locks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lockDialog)!;
                Require(locks.Count == lockRows.Count && locks.Values.All(value => value), "Save Locks should return every row locked.");
                form.LayoutScreen.ApplySizeLocks(locks);
                Require(lockRows.All(row => row.Key.StartsWith("D:", StringComparison.Ordinal)
                        ? lockModel.Ducts.Single(duct => DuctSystemScreen.DuctKey(duct.Sequence) == row.Key).SizeLocked
                        : lockModel.LayoutNodes.Single(node => DuctSystemScreen.LayoutKey(node.Sequence) == row.Key).SizeLocked),
                    "Applying the editor's locks should lock every listed item.");
                form.LayoutScreen.ApplySizeLocks(locks.ToDictionary(pair => pair.Key, _ => false));
                Require(lockModel.Ducts.All(duct => !duct.SizeLocked) && lockModel.LayoutNodes.All(node => !node.SizeLocked),
                    "Unlock All should clear every lock.");
            }
            var highlightButton = Find<Button>(form, "RibbonHighlightCriticalPathButton");
            if (((dynamic)highlightButton).Checked)
                ClickButton(highlightButton);
            var calculated = (bool)UiMember.Method("CalculateSystem", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, null)!;
            Application.DoEvents();
            Require(calculated
                        ? (bool)UiMember.Field("_criticalPathCalculated", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!
                          && ((dynamic)highlightButton).Checked
                          && layoutStatus.Text.StartsWith("Calculated.", StringComparison.Ordinal)
                        : layoutStatus.ForeColor == Color.DarkOrange && !((dynamic)highlightButton).Checked,
                $"Calculate should size the fan and highlight the critical path, or explain why it cannot. Status={layoutStatus.Text}");

            // Analysis summary and report preview (v0.96 stage 4).
            ClickButton(Find<Button>(form, "TopTabAnalysis"));
            Application.DoEvents();
            var topLossGrid = Find<DataGridView>(form, "AnalysisTopLossGrid");
            var expectedTop = form.DuctSystemScreen.CriticalPathDrops();
            var criticalProfile = SystemPathProfiles.Build(form.DuctSystemScreen._ductSystem).Paths.FirstOrDefault(path => path.IsCritical);
            Require(topLossGrid.Rows.Count == expectedTop.Count
                    && topLossGrid.Columns["Critical"] is null
                    && (criticalProfile is null || expectedTop.Count == 0
                        || expectedTop.Count == criticalProfile.Steps.Count
                           && Math.Abs(expectedTop.Sum(entry => entry.LossPa) - criticalProfile.TotalLossPa) < 1e-6)
                    && Find<Label>(form, "AnalysisAirflowValue").Text.Length > 0
                    && Find<Label>(form, "AnalysisExternalStaticValue").Text.Length > 0
                    && Find<Label>(form, "AnalysisTotalStaticValue").Text.Length > 0
                    && Find<Label>(form, "AnalysisRouteValue").Text.Length > 0
                    && ScreenBounds(Find<Control>(form, "AnalysisSummary")).Bottom <= ScreenBounds(grid).Top,
                "The Analysis tab should list every critical-path item (adding up to the critical-path total) above the full table.");
            // Round Y: the net path pressure graph is on the Analysis tab too, as its own card (not merged with the
            // pressure-drop graph).
            var analysisNetChart = Find<Control>(form, "AnalysisNetPathChart");
            var analysisNetPaths = (System.Collections.IList)analysisNetChart.GetType().GetProperty("Paths", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(analysisNetChart)!;
            Require(Find<Control>(form, "AnalysisNetPathCard").Contains(analysisNetChart)
                    && !Find<Control>(form, "AnalysisProfileCard").Contains(analysisNetChart)
                    && analysisNetPaths.Count == PathPressureProfiles.Build(form.DuctSystemScreen._ductSystem).Count
                    && Find<CheckBox>(form, "AnalysisNetPathVelocityCheck").Checked
                    && Find<ComboBox>(form, "AnalysisNetPathPathCombo").Items.Count == analysisNetPaths.Count + 1,
                "The Analysis tab should show the net path pressure graph in its own card, with every path.");
            var lossValues = expectedTop.Select(entry => entry.LossPa).ToArray();
            Require(lossValues.Zip(lossValues.Skip(1)).All(pair => pair.First >= pair.Second),
                "The top pressure drops should be listed from highest to lowest.");
            if (criticalProfile is { IsComplete: true, SuctionLossPa: double suctionSide, DischargeLossPa: double dischargeSide })
            {
                // ESP = the critical-path loss = negative (suction) + positive (discharge); TSP = ESP + ISP.
                var formatTotal = UiMember.Method("FormatSystemTotal", BindingFlags.Instance | BindingFlags.NonPublic)!;
                string Total(double pa) => (string)formatTotal.Invoke(form, new object[] { pa })!;
                var analysisFan = form.DuctSystemScreen._ductSystem.LayoutNodes.Single(node => node.Type == LayoutNodeType.Fan);
                Require(Find<Label>(form, "AnalysisExternalStaticValue").Text == Total(criticalProfile.TotalLossPa)
                        && Find<Label>(form, "AnalysisPositiveStaticValue").Text == "+" + Total(dischargeSide)
                        && Find<Label>(form, "AnalysisNegativeStaticValue").Text == (suctionSide > 0 ? "−" + Total(suctionSide) : Total(0))
                        && Find<Label>(form, "AnalysisTotalStaticValue").Text == Total(criticalProfile.TotalLossPa + analysisFan.FanInternalStaticPressurePa),
                    $"Static pressure block: ESP {Find<Label>(form, "AnalysisExternalStaticValue").Text}, negative {Find<Label>(form, "AnalysisNegativeStaticValue").Text}, positive {Find<Label>(form, "AnalysisPositiveStaticValue").Text}, TSP {Find<Label>(form, "AnalysisTotalStaticValue").Text}.");
            }

            // Analysis Settings, the "within expected range" column and the pressure drop along each path (round Q).
            var analysisScreen = form.DuctSystemScreen;
            var analysisSettingsButton = Find<Control>(form, "RibbonAnalysisSettingsButton");
            var settingsButtonPage = analysisSettingsButton.Parent;
            while (settingsButtonPage is not null && settingsButtonPage.Name != "RibbonAnalysisPage")
                settingsButtonPage = settingsButtonPage.Parent;
            Require(settingsButtonPage is not null && !topLossGrid.Columns["Range"]!.Visible,
                "The Analysis ribbon should offer Analysis Settings, and the range column should stay hidden until a range is saved.");
            using (var settings = new AnalysisSettingsDialog(analysisScreen._ductSystem.AnalysisExpectations, UnitSystem.Imperial))
            {
                settings.Show(form);
                Application.DoEvents();
                Find<TextBox>(settings, "AnalysisSettingsDuctPressureRateLow").Text = "0.05";
                Find<TextBox>(settings, "AnalysisSettingsDuctPressureRateHigh").Text = "0.1";
                Find<TextBox>(settings, "AnalysisSettingsDuctVelocityHigh").Text = "1500";
                Find<TextBox>(settings, "AnalysisSettingsFittingLossHigh").Text = "0.05";
                ClickButton(Find<Button>(settings, "AnalysisSettingsSaveButton"));
                Application.DoEvents();
                var saved = settings.Expectations;
                Require(settings.DialogResult == DialogResult.OK
                        && Math.Abs(saved.DuctPressureRatePaPerM.Low!.Value - UnitConversions.InchesWaterPer100FeetToPaPerM(0.05)) < 1e-12
                        && Math.Abs(saved.DuctPressureRatePaPerM.High!.Value - UnitConversions.InchesWaterPer100FeetToPaPerM(0.1)) < 1e-12
                        && saved.DuctVelocityMPerS.Low is null
                        && Math.Abs(saved.DuctVelocityMPerS.High!.Value - UnitConversions.FpmToMps(1500)) < 1e-12
                        && !saved.FittingVelocityMPerS.IsSet
                        && Math.Abs(saved.FittingLossPa.High!.Value - UnitConversions.InchesWaterToPa(0.05)) < 1e-9,
                    "Analysis Settings should save each entered bound, converted to SI, and leave empty bounds empty.");
                analysisScreen._ductSystem.AnalysisExpectations = saved;
            }
            analysisScreen.RefreshDuctSystemGrid();
            Application.DoEvents();
            var rangedTop = analysisScreen.CriticalPathDrops();
            Require(topLossGrid.Columns["Range"]!.Visible && topLossGrid.Columns["Range"]!.HeaderText == "Within expected range"
                    && topLossGrid.Rows.Count == rangedTop.Count
                    && rangedTop.Select((entry, index) => (entry, index)).All(pair =>
                        Convert.ToString(topLossGrid.Rows[pair.index].Cells["Range"].Value) == pair.entry.Range.Status switch
                        {
                            RangeCheckStatus.Within => "Yes",
                            RangeCheckStatus.Outside => "No",
                            RangeCheckStatus.NotSet => "Not set",
                            RangeCheckStatus.NotAvailable => "Not available",
                            _ => "—"
                        }
                        && topLossGrid.Rows[pair.index].Cells["Range"].ToolTipText.Length > 0)
                    && rangedTop.Where(entry => analysisScreen._ductSystem.Ducts.Any(duct => duct.Id == entry.Id))
                        .All(entry => entry.Range.Status is RangeCheckStatus.Within or RangeCheckStatus.Outside),
                "Saving ranges should add the 'Within expected range' column, with Yes/No for every duct and the reason in a tooltip.");

            var profileChart = Find<PressureProfileChart>(form, "AnalysisPressureProfileChart");
            var routeProfiles = SystemPathProfiles.Build(analysisScreen._ductSystem);
            var shownProfile = profileChart.Profile!;
            var imperialUnits = form._currentUnits == UnitSystem.Imperial;
            var criticalRoute = routeProfiles.Paths.SingleOrDefault(path => path.IsCritical);
            var criticalSeries = shownProfile.Series.SingleOrDefault(series => series.IsCritical);
            Require(shownProfile.PathCount == routeProfiles.Paths.Count && routeProfiles.Paths.Count > 0
                    && shownProfile.Series.Count == routeProfiles.CompletePathCount
                    && (criticalSeries is null
                        || criticalRoute is not null
                           && criticalSeries.Values.Count == criticalRoute.Steps.Count
                           && Math.Abs(criticalSeries.Values[^1] - (imperialUnits
                               ? UnitConversions.PaToInchesWater(criticalRoute.TotalLossPa) : criticalRoute.TotalLossPa)) < 1e-9)
                    && ScreenBounds(profileChart).Top >= ScreenBounds(Find<Control>(form, "AnalysisSummary")).Bottom
                    && ScreenBounds(profileChart).Bottom <= ScreenBounds(grid).Top
                    && profileChart.Height > 60,
                $"The Analysis tab should graph every complete path below the summary, the critical path's last point at its total. Paths={routeProfiles.Paths.Count}, series={shownProfile.Series.Count}.");
            var nextRoute = routeProfiles.Paths.Where(path => !path.IsCritical).OrderByDescending(path => path.TotalLossPa).FirstOrDefault();
            Require(criticalSeries is null || nextRoute is null
                        ? string.IsNullOrEmpty(shownProfile.Comparison)
                        : shownProfile.Comparison.Contains(nextRoute.EndId, StringComparison.Ordinal)
                          && Find<Label>(form, "AnalysisProfileComparison").Text == shownProfile.Comparison,
                $"The Analysis tab should name the path closest to the critical path when there is one. Comparison={shownProfile.Comparison}");
            // Round U: paths are numbered (Path 1 = critical, then by decreasing total); one path can be isolated, with its
            // total, gap to the critical path and route beside the graph; a picked point is described; a table row rings the item.
            var numberedPaths = routeProfiles.Paths.OrderBy(path => path.Number).ToArray();
            Require(numberedPaths.Select(path => path.Number).SequenceEqual(Enumerable.Range(1, numberedPaths.Length))
                    && (criticalRoute is null || criticalRoute.Number == 1)
                    && numberedPaths.Where(path => path.IsComplete).Zip(numberedPaths.Where(path => path.IsComplete).Skip(1))
                        .All(pair => pair.First.IsCritical || pair.First.TotalLossPa >= pair.Second.TotalLossPa - 1e-12)
                    && shownProfile.Series.All(series => series.Label.StartsWith("Path ", StringComparison.Ordinal) && series.Route.Contains(" to ", StringComparison.Ordinal)),
                "Paths should be named by number: Path 1 is the critical path, then decreasing totals; the route is separate.");
            var pathSelector = Find<ComboBox>(form, "AnalysisProfilePathSelector");
            Require(pathSelector.Items.Count == shownProfile.Series.Count + 1 && Convert.ToString(pathSelector.Items[0]) == "All paths"
                    && pathSelector.SelectedIndex == 0 && profileChart.IsolatedSeries is null,
                "The path graph should have a selector: All paths, then each path.");
            if (shownProfile.Series.Count >= 2)
            {
                pathSelector.SelectedIndex = 2;
                Application.DoEvents();
                using (var chartPaint = new Bitmap(Math.Max(1, profileChart.Width), Math.Max(1, profileChart.Height)))
                using (var chartGraphics = Graphics.FromImage(chartPaint))
                    typeof(PressureProfileChart).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(profileChart, new object[] { new PaintEventArgs(chartGraphics, new Rectangle(Point.Empty, chartPaint.Size)) });
                var isolatedLine = shownProfile.Series[1];
                var gap = shownProfile.Series[0].Values[^1] - isolatedLine.Values[^1];
                Require(profileChart.IsolatedSeries == 1 && Find<Control>(form, "AnalysisPathPanel").Visible
                        && Find<Label>(form, "AnalysisPathName").Text.StartsWith("Path 2", StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisPathTotal").Text.Contains(isolatedLine.Values[^1].ToString(imperialUnits ? "0.###" : "0.#", System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisPathBelowCritical").Text.Contains(gap.ToString(imperialUnits ? "0.###" : "0.#", System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisPathRoute").Text.Contains(isolatedLine.Route, StringComparison.Ordinal),
                    "Isolating a path should show its total, how far below the critical path it is, and its route.");
                var pickAt = profileChart.PointOf(1, isolatedLine.Values.Count - 1);
                typeof(PressureProfileChart).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(profileChart, new object[] { new MouseEventArgs(MouseButtons.Left, 1, (int)pickAt.X, (int)pickAt.Y, 0) });
                Application.DoEvents();
                var lastId = isolatedLine.ItemIds![^1];
                Require(profileChart.SelectedPoint == (1, isolatedLine.Values.Count - 1)
                        && Find<Label>(form, "AnalysisPathPick").Text.Contains($"Component {isolatedLine.Values.Count} of {isolatedLine.Values.Count}: {lastId}", StringComparison.Ordinal),
                    $"Clicking a point of the isolated path should describe that component. Pick={Find<Label>(form, "AnalysisPathPick").Text}");
                analysisScreen.GetType().GetMethod("HighlightOnPathGraph", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(analysisScreen, new object?[] { lastId });
                Require(profileChart.HighlightItemId == lastId && profileChart.ShowsItem(lastId)
                        && !profileChart.ShowsItem("NOT-AN-ITEM"),
                    "Choosing an item in a table should ring it on the path graph when it lies on a path shown.");
                pathSelector.SelectedIndex = 0;
                Application.DoEvents();
                Require(profileChart.IsolatedSeries is null && !Find<Control>(form, "AnalysisPathPanel").Visible,
                    "Choosing All paths should show every path again.");
            }
            {
                // Round V: with all paths shown, the left panel lists every path's total (scrollable); a row isolates it.
                var allPaths = Find<FlowLayoutPanel>(form, "AnalysisAllPathsPanel");
                var rows = allPaths.Controls.OfType<Label>().Where(label => label.Name.StartsWith("AnalysisAllPathsRow", StringComparison.Ordinal)).ToArray();
                var firstTotal = shownProfile.Series[0].Values[^1].ToString(imperialUnits ? "0.###" : "0.#", System.Globalization.CultureInfo.CurrentCulture);
                Require((!form.Visible || allPaths.Visible) && allPaths.AutoScroll && rows.Length == shownProfile.Series.Count
                        && rows[0].Text.StartsWith(shownProfile.Series[0].IsCritical ? "Path 1 (critical)" : "Path 1", StringComparison.Ordinal)
                        && rows[0].Text.Contains(firstTotal, StringComparison.Ordinal),
                    $"All paths should list each path's total beside the graph. Rows {rows.Length} of {shownProfile.Series.Count}, first '{rows.FirstOrDefault()?.Text}', total {firstTotal}, visible {allPaths.Visible}, scroll {allPaths.AutoScroll}; chain {string.Join(" < ", Ancestors(allPaths))}.");
                // Hover label: the component, its drop and the drop from the start (no path name, no path total).
                var hoverAt = profileChart.PointOf(0, shownProfile.Series[0].Values.Count - 1);
                var hover = profileChart.HitText(new Point((int)Math.Round(hoverAt.X), (int)Math.Round(hoverAt.Y)));
                var hoverLines = hover.Split('\n');
                Require(hoverLines.Length == 3 && hoverLines[0].StartsWith("Component ", StringComparison.Ordinal)
                        && hoverLines[1].StartsWith("Drop: ", StringComparison.Ordinal)
                        && hoverLines[2].StartsWith("From the start: ", StringComparison.Ordinal)
                        && !hover.Contains("Path", StringComparison.Ordinal),
                    $"The hover label should give the component, its drop and the drop from the start only. Got: {hover}");
                // Negative / positive static sides: shaded at the fan on Path 1; the check box hides them.
                var sides = Find<CheckBox>(form, "AnalysisPressureSidesCheck");
                var critical = shownProfile.Series.FirstOrDefault(series => series.IsCritical) ?? shownProfile.Series[0];
                Require(sides.Checked && profileChart.ShowPressureSides
                        && profileChart.PressureSideFanComponent == (critical.FanIndex >= 0 ? critical.FanIndex + 1 : null),
                    $"The suction/discharge sides should be marked at the fan on Path 1 (fan index {critical.FanIndex}, shown {profileChart.PressureSideFanComponent}).");
                sides.Checked = false;
                Application.DoEvents();
                Require(!profileChart.ShowPressureSides && profileChart.PressureSideFanComponent is null,
                    "Clearing the check box should hide the suction/discharge sides.");
                sides.Checked = true;
                Application.DoEvents();
            }
            // Round U: air handling unit. Placed from the Equipment group like a fan (one fan or AHU per system); its
            // ID is AHU-nnn; the Analysis tile, Fan Information and the report show external, internal and total static.
            {
                var ahuTool = form.Controls.Find("LayoutToolAirHandlingUnit", true);
                Require(ahuTool.Length == 1 && ahuTool[0].Parent?.Name == "RibbonEquipmentGroupTools",
                    $"The Project tab's Equipment group should offer an AHU tool next to the fan. Found {ahuTool.Length}.");
                var existingFan = analysisScreen._ductSystem.LayoutNodes.Single(node => node.Type == LayoutNodeType.Fan);
                var fanIndex = analysisScreen._ductSystem.LayoutNodes.IndexOf(existingFan);
                var placeNode = UiMember.Method("PlaceLayoutNode", BindingFlags.Instance | BindingFlags.NonPublic)!;
                placeNode.Invoke(form, new object?[] { LayoutNodeType.Fan, existingFan.GridX + 31, existingFan.GridY + 37, LayoutFanData.AirHandlingUnitTool });
                Require(analysisScreen._ductSystem.LayoutNodes.Count(node => node.Type == LayoutNodeType.Fan) == 1
                        && Find<Label>(form, "LayoutDesignerStatus").Text.Contains("Only one fan or air handling unit", StringComparison.Ordinal),
                    "An air handling unit cannot be added while the system has a fan.");
                analysisScreen._ductSystem.LayoutNodes.Remove(existingFan);
                placeNode.Invoke(form, new object?[] { LayoutNodeType.Fan, existingFan.GridX + 31, existingFan.GridY + 37, LayoutFanData.AirHandlingUnitTool });
                var ahu = analysisScreen._ductSystem.LayoutNodes.Single(node => node.Type == LayoutNodeType.Fan);
                Require(ahu.IsAirHandlingUnit && ahu.Id == $"AHU-{ahu.Sequence:000}",
                    $"The AHU tool should place an air handling unit named AHU-nnn; got {ahu.Id}.");
                ahu.IsDefined = true;
                ahu.AirflowM3PerS = UnitConversions.CfmToM3PerS(2000);
                ahu.FanStaticPressurePa = UnitConversions.InchesWaterToPa(1.0);
                ahu.FanInternalStaticPressurePa = UnitConversions.InchesWaterToPa(1.5);
                ahu.FanEfficiencyPercent = 65;
                var ahuInformation = new object?[] { null };
                UiMember.Method("TryGetFanInformation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, ahuInformation);
                var ahuText = Convert.ToString(ahuInformation[0])!;
                Require(ahuText.Contains("Air Handling Unit", StringComparison.Ordinal)
                        && ahuText.Contains("External static pressure (ESP):", StringComparison.Ordinal)
                        && ahuText.Contains("Internal static pressure:", StringComparison.Ordinal)
                        && ahuText.Contains("Total static pressure:", StringComparison.Ordinal)
                        && ahuText.Contains("1.21", StringComparison.Ordinal),
                    $"Fan Information for an AHU should show ESP, internal and total static pressure, and motor power on the total (1.21 hp). Got: {ahuText}");
                ClickButton(Find<Button>(form, "TopTabAnalysis"));
                Application.DoEvents();
                analysisScreen.GetType().GetMethod("RefreshAnalysisSummary", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(analysisScreen, null);
                // Round V: the same static pressure block for a fan or an AHU: TSP = ISP + ESP, ESP split into negative
                // (suction) and positive (discharge), and the unit's set ESP beside them.
                Require(Find<Label>(form, "AnalysisStaticHeading").Text.Contains(ahu.Id, StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisStaticHeading").Text.Contains("air handling unit", StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisInternalStaticValue").Text.StartsWith("1.5", StringComparison.Ordinal)
                        && Find<Label>(form, "AnalysisFanSetCaption").Text == $"{ahu.Id} set to (ESP)"
                        && Find<Label>(form, "AnalysisFanSetValue").Text.StartsWith("1 ", StringComparison.Ordinal),
                    $"The Analysis static pressure block should show the AHU's internal static and its set ESP. Heading={Find<Label>(form, "AnalysisStaticHeading").Text}; ISP={Find<Label>(form, "AnalysisInternalStaticValue").Text}; set={Find<Label>(form, "AnalysisFanSetValue").Text}.");
                var ahuSheet = analysisScreen.GetType().GetMethod("BuildEquipmentPdfCalculations", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { ahu, (0.0, ahu.FanStaticPressurePa) }) as IReadOnlyList<DuctSystemPdfCalculationSection>;
                Require(ahuSheet is not null && ahuSheet.SelectMany(section => section.Lines)
                            .Any(line => line.Formula == "TSP = ESP + ISP" && line.InputSubstitution.StartsWith("ESP = 1; ISP = 1.5", StringComparison.Ordinal) && line.HighlightValue.StartsWith("2.5 ", StringComparison.Ordinal)),
                    "The AHU worksheet should add the internal static pressure to the ESP.");
                using (var ahuIcon = new Bitmap(48, 48))
                using (var ahuIconGraphics = Graphics.FromImage(ahuIcon))
                {
                    ahuIconGraphics.Clear(Color.White);
                    typeof(MainForm).Assembly.GetType("DuctSizer.WinForms.LayoutIconPainter")!
                        .GetMethod("Draw", BindingFlags.Static | BindingFlags.NonPublic)!
                        .Invoke(null, new object?[] { ahuIconGraphics, LayoutNodeType.Fan, new Rectangle(4, 4, 40, 40), Color.FromArgb(10, 88, 187), LayoutFanData.AirHandlingUnitTool });
                    var inked = 0;
                    for (var x = 0; x < 48; x++)
                        for (var y = 0; y < 48; y++)
                            if (ahuIcon.GetPixel(x, y).GetBrightness() < 0.9F)
                                inked++;
                    Require(inked > 150, $"The AHU symbol should be drawn (cabinet, filter, fan, coil); {inked} inked pixels.");
                }
                analysisScreen._ductSystem.LayoutNodes.Remove(ahu);
                analysisScreen._ductSystem.LayoutNodes.Insert(fanIndex, existingFan);
                analysisScreen.SaveDuctSystem();
                analysisScreen.RefreshDuctSystemGrid();
                analysisScreen.GetType().GetMethod("RefreshAnalysisSummary", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(analysisScreen, null);
                Require(Find<Label>(form, "AnalysisFanSetCaption").Text == $"{existingFan.Id} set to (ESP)"
                        && Find<Label>(form, "AnalysisStaticHeading").Text.Contains("(fan)", StringComparison.Ordinal),
                    "With a fan again the static pressure block should name the fan (same template).");
            }
            // SMACNA pressure class, seal class and leakage; donut; whole-page scroll with the full-height table.
            var leakageSummary = analysisScreen.BuildLeakageSummary();
            var leakageResult = SmacnaLeakage.Analyze(analysisScreen._ductSystem);
            Require(Find<Label>(form, "AnalysisPressureClassValue").Text == leakageSummary.PressureClass
                    && Find<Label>(form, "AnalysisPressureClassNote").Text.Contains("SMACNA HVAC Duct Construction Standards", StringComparison.Ordinal)
                    && Find<Label>(form, "AnalysisSealClassValue").Text == leakageSummary.SealClass
                    && Find<Label>(form, "AnalysisLeakageValue").Text == leakageSummary.Leakage
                    && (leakageResult.PressureClassInWc is null
                        || leakageSummary.PressureClass.Contains(leakageResult.PressureClassInWc.Value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal))
                    && (!leakageResult.IsAvailable
                        || Math.Abs(leakageResult.TotalLeakageCfm - leakageResult.ByShape.Sum(x => x.FactorCfmPer100Ft2 * x.AreaFt2 / 100)) < 1e-9),
                $"The overview should show the SMACNA pressure class, seal class and allowable leakage. Class={leakageSummary.PressureClass}, leakage={leakageSummary.Leakage}, note={leakageSummary.Note}");
            // Round W: a column per side of the fan; with nothing upstream of the fan the negative column says so.
            var (negativeSide, _) = analysisScreen.BuildLeakageSides();
            Require(Find<Label>(form, "AnalysisNegativePressureClassValue").Text
                    == (negativeSide?.PressureClass ?? "No ductwork upstream of the fan"),
                $"The negative (suction) column should show its own class. Got {Find<Label>(form, "AnalysisNegativePressureClassValue").Text}.");
            var donut = Find<SystemSummaryDonut>(form, "AnalysisSystemSummaryDonut");
            Require(donut.Counts == analysisScreen.SystemItemCounts() && donut.Counts.Ducts == analysisScreen._ductSystem.Ducts.Count,
                "The Analysis tab should show the system summary donut with the duct, fitting and component counts.");
            var pageScroll = Find<Panel>(form, "AnalysisPageScroll");
            var visibleRows = grid.Rows.Cast<DataGridViewRow>().Where(row => row.Visible).Sum(row => row.Height);
            Require(pageScroll.AutoScroll && grid.ScrollBars == ScrollBars.Horizontal
                    && grid.Height >= grid.ColumnHeadersHeight + visibleRows
                    && grid.Columns["SurfaceArea"] is { } surfaceColumn && surfaceColumn.HeaderText.StartsWith("Surface area", StringComparison.Ordinal),
                $"The Analysis page should scroll as a whole, with the full table (every row, surface area column) on it. Grid height={grid.Height}, rows={visibleRows}.");
            var firstDuct = analysisScreen._ductSystem.Ducts.FirstOrDefault();
            if (firstDuct is not null)
            {
                var ductRow = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => Convert.ToString(row.Cells["Id"].Value) == firstDuct.Id);
                var ductItem = analysisScreen.BuildDuctReportItem(firstDuct);
                var headings = ductItem.Calculations!.Select(section => section.Heading).ToArray();
                Require((ductRow is null || Convert.ToString(ductRow.Cells["SurfaceArea"].Value) == analysisScreen.FormatSurfaceArea(SurfaceAreaCalculator.ForDuct(firstDuct)))
                        && headings.First() == "DUCT SIZING CALCULATION"
                        && Array.IndexOf(headings, "SURFACE AREA CALCULATION") > Array.IndexOf(headings, "ASSUMPTIONS")
                        && ductItem.Calculations!.Single(section => section.Heading == "SURFACE AREA CALCULATION").IsTitle,
                    $"A duct's worksheet should be titled, and its surface area calculation should follow the assumptions. Headings: {string.Join(", ", headings)}");
            }
            analysisScreen._ductSystem.AnalysisExpectations = new AnalysisExpectations();
            analysisScreen.RefreshDuctSystemGrid();
            Application.DoEvents();
            ClickButton(Find<Button>(form, "TopTabReports"));
            Application.DoEvents();
            var reportPreview = Find<Control>(form, "ReportsPage");
            // Round V: the report is written and its pages drawn in the background; the window stays responsive.
            var previewWait = System.Diagnostics.Stopwatch.StartNew();
            var previewReady = (bool)reportPreview.GetType().GetMethod("WaitUntilReady", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(reportPreview, new object[] { 120_000 })!;
            Console.WriteLine($"Report preview ready in {previewWait.ElapsedMilliseconds} ms.");
            var previewPages = (int)reportPreview.GetType().GetProperty("PageCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(reportPreview)!;
            Require(previewReady && previewPages >= 1 && Find<Control>(form, "ReportPreviewPage1") is PictureBox { Image: not null },
                $"The Reports tab should preview the report page by page. Status: {Find<Label>(form, "ReportPreviewStatus").Text}");
            {
                // Zoom: + and − step 100 % -> 110 % -> 100 %; the page grows with it; Fit width fills the window.
                var firstPage = Find<PictureBox>(form, "ReportPreviewPage1");
                var widthAt100 = firstPage.Width;
                ClickButton(Find<Button>(form, "ReportZoomIn"));
                Application.DoEvents();
                Require(Find<Label>(form, "ReportZoomLabel").Text == "110%" && firstPage.Width > widthAt100,
                    $"Zoom in should enlarge the pages to 110 %. Label {Find<Label>(form, "ReportZoomLabel").Text}, width {widthAt100} -> {firstPage.Width}.");
                var setZoom = reportPreview.GetType().GetMethod("SetZoom", BindingFlags.Instance | BindingFlags.NonPublic)!;
                setZoom.Invoke(reportPreview, new object[] { 2.0 });
                Application.DoEvents();
                Require(Math.Abs(firstPage.Width - 2.0 * widthAt100) <= 2 && Find<Label>(form, "ReportZoomLabel").Text == "200%",
                    $"At 200 % the page should be twice as wide ({widthAt100} -> {firstPage.Width}).");
                var sharpClock = System.Diagnostics.Stopwatch.StartNew();
                while (sharpClock.ElapsedMilliseconds < 30_000 && (firstPage.Image?.Width ?? 0) < firstPage.Width)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }
                Require((firstPage.Image?.Width ?? 0) >= firstPage.Width,
                    $"Zoomed-in pages near the view should be redrawn sharp ({firstPage.Image?.Width} px image for a {firstPage.Width} px page).");
                ClickButton(Find<Button>(form, "ReportZoomOut"));
                Application.DoEvents();
                Require(Find<Label>(form, "ReportZoomLabel").Text == "175%", "Zoom out steps down to 175 %.");
                setZoom.Invoke(reportPreview, new object[] { 1.0 });
                Application.DoEvents();
                // Reopening the tab with the project unchanged keeps the preview (no rebuild).
                ClickButton(Find<Button>(form, "TopTabProject"));
                Application.DoEvents();
                ClickButton(Find<Button>(form, "TopTabReports"));
                Application.DoEvents();
                Require(Find<Control>(form, "ReportPreviewPage1") == firstPage
                        && Find<Label>(form, "ReportPreviewStatus").Text.Contains("up to date", StringComparison.Ordinal),
                    $"An unchanged project should keep its preview. Status: {Find<Label>(form, "ReportPreviewStatus").Text}");
            }
            var printedReport = Path.Combine(testDirectory, "printed-report.pdf");
            AutomationOptions.ReportPdfPath = printedReport;
            try
            {
                ClickButton(Find<Button>(form, "RibbonPrintReportButton"));
                Application.DoEvents();
            }
            finally
            {
                AutomationOptions.ReportPdfPath = null;
            }
            Require(File.Exists(printedReport) && new FileInfo(printedReport).Length > 10_000
                    && File.ReadAllBytes(printedReport).Take(5).SequenceEqual("%PDF-"u8.ToArray()),
                "Print Report should write the report PDF.");
            var printedText = PdfText.Plain(File.ReadAllBytes(printedReport));
            Require(printedText.Contains("(DUCT PRESSURE CLASS AND LEAKAGE \\(SMACNA\\))", StringComparison.Ordinal)
                    && printedText.Contains("(SURFACE AREA CALCULATION)", StringComparison.Ordinal)
                    && printedText.Contains("(PRESSURE DROP ALONG EACH PATH)", StringComparison.Ordinal)
                    && printedText.Contains("(SYSTEM SUMMARY)", StringComparison.Ordinal)
                    && printedText.Contains("(Duct segments)", StringComparison.Ordinal)
                    && printedText.Contains("(Quantity ", StringComparison.Ordinal),
                "The report's title page should show the paths graph and the system summary donut with labelled quantities.");
            ClickButton(Find<Button>(form, "TopTabProject"));
            Application.DoEvents();

            // In-place editing in the Selection Inspector (polish round). Run last on this project because an applied
            // edit confirms positions, which the drawing checks above do not expect.
            var editedComponent = ribbonModel.LayoutNodes.FirstOrDefault(node => node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.CustomComponent)
                                  ?? ribbonModel.LayoutNodes.First(node => node.Type == LayoutNodeType.Fan);
            double ComponentDropPa() => editedComponent.Type == LayoutNodeType.Fan ? editedComponent.FanStaticPressurePa : editedComponent.PressureDropPa;
            form.LayoutScreen.SelectLayoutItemById(editedComponent.Id);
            Application.DoEvents();
            TextBox InspectorBox(string name) => Descendants<TextBox>(Find<Control>(form, "InspectorDescriptionPage")).Single(box => box.Name == name);
            Require(Find<Label>(form, "InspectorTitle").Text.StartsWith(editedComponent.Id, StringComparison.Ordinal)
                    && !InspectorBox("LayoutEquipmentPressureDropText").ReadOnly
                    && Descendants<Label>(Find<Control>(form, "InspectorDescriptionPage")).Any(label => label.Name == "InspectorValue" && label.Text == editedComponent.Id)
                    && !Descendants<TextBox>(Find<Control>(form, "InspectorDescriptionPage")).Any(box => box.Text == editedComponent.Id),
                "The inspector should show fixed values as text and the component's pressure drop as an editable box.");
            var dropBefore = ComponentDropPa();
            InspectorBox("LayoutEquipmentPressureDropText").Text = "0.37";
            Application.DoEvents();
            var inspectorPanel = Find<Control>(form, "SelectionInspector");
            Require((bool)inspectorPanel.GetType().GetField("_sessionDirty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(inspectorPanel)!
                    && Math.Abs(ComponentDropPa() - dropBefore) < 1e-9,
                "Typing in the inspector should mark the edit as not yet applied and leave the project unchanged.");
            ClickButton(Find<Button>(form, "InspectorUpdateButton"));
            Application.DoEvents();
            Require(Math.Abs(ComponentDropPa() - UnitConversions.InchesWaterToPa(0.37)) < 1e-6
                    && Find<Label>(form, "InspectorTitle").Text.StartsWith(editedComponent.Id, StringComparison.Ordinal)
                    && Find<Button>(form, "LayoutUndoButton").Enabled,
                "Update in the inspector should apply the edit through the component editor, keep the item shown, and be undoable.");
            InspectorBox("LayoutEquipmentPressureDropText").Text = "-1";
            Application.DoEvents();
            ClickButton(Find<Button>(form, "InspectorUpdateButton"));
            Application.DoEvents();
            Require(Math.Abs(ComponentDropPa() - UnitConversions.InchesWaterToPa(0.37)) < 1e-6
                    && Find<Label>(form, "LayoutEquipmentInlineStatus").Text.Contains("nonnegative", StringComparison.OrdinalIgnoreCase),
                "An invalid value in the inspector should be refused (with the reason shown) and leave the project unchanged.");
            ClickButton(Find<Button>(form, "InspectorCancelButton"));
            Application.DoEvents();
            Require(InspectorBox("LayoutEquipmentPressureDropText").Text == "0.37",
                "Cancel should discard unapplied inspector edits.");
            var inlineDuct = ribbonModel.LayoutNodes.FirstOrDefault(node => !string.IsNullOrWhiteSpace(node.ParentNodeId) && node.ConnectionDuctSequence is not null);
            if (inlineDuct is not null)
            {
                form.LayoutScreen.SelectLayoutItemById(ribbonModel.Ducts.Single(duct => duct.Sequence == inlineDuct.ConnectionDuctSequence).Id);
                Application.DoEvents();
                Require(Find<Button>(form, "InspectorUpdateButton").Text == "Update Duct"
                        && Descendants<ComboBox>(Find<Control>(form, "InspectorDescriptionPage")).Any(combo => combo.Name == "LayoutDuctSizingMethodCombo")
                        && form.Controls.Find("InspectorReverseFlowButton", true).Length == 0,
                    "A duct should be editable in the inspector with the duct editor's own inputs (no Reverse Flow Direction).");
                // Round Y: the inspector's "Lock current size" box locks the duct against Auto Size Ductwork (undoable).
                var lockedDuct = ribbonModel.Ducts.Single(duct => duct.Sequence == inlineDuct.ConnectionDuctSequence);
                var lockCheck = Find<CheckBox>(form, "InspectorSizeLockCheck");
                Require(!lockCheck.Checked && !lockedDuct.SizeLocked, "A duct starts unlocked.");
                lockCheck.Checked = true;
                Application.DoEvents();
                Application.DoEvents();
                Require(lockedDuct.SizeLocked && Find<CheckBox>(form, "InspectorSizeLockCheck").Checked,
                    "Ticking Lock current size in the inspector should lock the duct.");
                form.LayoutScreen.ApplySizeLocks(new Dictionary<string, bool> { [DuctSystemScreen.DuctKey(lockedDuct.Sequence)] = false });
                Application.DoEvents();
                Require(!lockedDuct.SizeLocked, "Size locks can be cleared again.");
            }

            // Designer shortcuts (round Q): Esc deselects, Delete removes the selection at once (undoable), and the
            // right-click menu's Edit opens the Selection Inspector on the item.
            ClickButton(Find<Button>(form, "TopTabProject"));
            Application.DoEvents();
            var shortcutCanvas = (LayoutDesignerCanvas)Find<Control>(form, "LayoutDesignerCanvas");
            var shortcutModel = form.DuctSystemScreen._ductSystem;
            var shortcutNode = shortcutModel.LayoutNodes.Last(node => node.Type != LayoutNodeType.CalculationPoint);
            form.LayoutScreen.SelectLayoutItemById(shortcutNode.Id);
            Application.DoEvents();
            Require(shortcutCanvas.SelectedNodeId == shortcutNode.Id
                    && form.LayoutScreen.TryHandleCommandKey(Keys.Escape) && !shortcutCanvas.HasSelection,
                "Esc should deselect the selected component.");
            Require(!form.LayoutScreen.TryHandleCommandKey(Keys.Escape),
                "Esc with nothing selected should be left to the rest of the window.");
            form.LayoutScreen.SelectLayoutItemById(shortcutNode.Id);
            Application.DoEvents();
            UiMember.Method("ShowLayoutItemMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { new Point(40, 40) });
            var itemMenu = (ContextMenuStrip)UiMember.Field("_layoutItemMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(itemMenu.Items.Cast<ToolStripItem>().Select(item => item.Text).SequenceEqual(new[] { "Edit", "Delete" }),
                "Right-clicking a component should offer Edit and Delete.");
            itemMenu.Items["LayoutItemMenuEdit"]!.PerformClick();
            Application.DoEvents();
            var shortcutInspector = Find<Control>(form, "SelectionInspector");
            Require(!(bool)shortcutInspector.GetType().GetField("_collapsed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shortcutInspector)!
                    && Find<Label>(form, "InspectorTitle").Text.StartsWith(shortcutNode.Id, StringComparison.Ordinal)
                    && shortcutCanvas.SelectedNodeId == shortcutNode.Id,
                $"Edit should open the Selection Inspector with that component selected. title={Find<Label>(form, "InspectorTitle").Text}, selected={shortcutCanvas.SelectedNodeId}, node={shortcutNode.Id}");
            var nodesBeforeDelete = shortcutModel.LayoutNodes.Count;
            Require(form.LayoutScreen.TryHandleCommandKey(Keys.Delete)
                    && form.DuctSystemScreen._ductSystem.LayoutNodes.Count == nodesBeforeDelete - 1
                    && form.DuctSystemScreen._ductSystem.LayoutNodes.All(node => node.Id != shortcutNode.Id)
                    && Find<Label>(form, "LayoutDesignerStatus").Text.StartsWith($"Deleted {shortcutNode.Id}", StringComparison.Ordinal),
                "Delete should remove the selected component without a prompt and say how to undo it.");
            ClickButton(Find<Button>(form, "LayoutUndoButton"));
            Application.DoEvents();
            Require(form.DuctSystemScreen._ductSystem.LayoutNodes.Count == nodesBeforeDelete
                    && form.DuctSystemScreen._ductSystem.LayoutNodes.Any(node => node.Id == shortcutNode.Id),
                "Undo should restore a component removed with the Delete key.");
            ClickButton(Find<Button>(form, "LayoutOverviewTab"));
            Application.DoEvents();
            // Round Y: System Overview rows have a right-click menu with Edit and Remove.
            var overviewGrid = Find<DataGridView>(form, "LayoutSystemSummaryGrid");
            Require(overviewGrid.ContextMenuStrip is { Name: "LayoutOverviewMenu" } overviewMenu
                    && overviewMenu.Items.Cast<ToolStripItem>().Select(item => item.Text).SequenceEqual(new[] { "Edit", "Remove" }),
                "Right-clicking a System Overview row should offer Edit and Remove.");
            var overviewDuctRow = overviewGrid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(row => Convert.ToString(row.Cells["Id"].Value)!.StartsWith("D-", StringComparison.Ordinal));
            if (overviewDuctRow is not null)
            {
                var overviewDuctId = Convert.ToString(overviewDuctRow.Cells["Id"].Value)!;
                overviewGrid.ClearSelection();
                overviewDuctRow.Selected = true;
                form.LayoutScreen.EditSystemOverviewRow();
                Application.DoEvents();
                Require(Find<Label>(form, "InspectorTitle").Text.StartsWith(overviewDuctId, StringComparison.Ordinal),
                    $"Edit on a System Overview row should open {overviewDuctId} in the Selection Inspector.");
                var ductsBefore = form.DuctSystemScreen._ductSystem.Ducts.Count(duct => !SystemMembership.IsStandalone(form.DuctSystemScreen._ductSystem, duct));
                overviewGrid.ClearSelection();
                overviewGrid.Rows.Cast<DataGridViewRow>().First(row => Convert.ToString(row.Cells["Id"].Value) == overviewDuctId).Selected = true;
                form.LayoutScreen.RemoveSystemOverviewRow();
                Application.DoEvents();
                var removedNow = form.DuctSystemScreen._ductSystem;
                Require(removedNow.Ducts.All(duct => duct.Id != overviewDuctId || SystemMembership.IsStandalone(removedNow, duct)),
                    $"Remove on a System Overview row should delete {overviewDuctId} from the drawing.");
                UiMember.Method("UndoLayoutChange", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Application.DoEvents();
                var restored = form.DuctSystemScreen._ductSystem;
                Require(restored.Ducts.Count(duct => !SystemMembership.IsStandalone(restored, duct)) == ductsBefore,
                    "Undo should bring the removed duct back.");
            }

            // Round S: Pressure labels, draggable labels with leaders, free rotation and the view cube.
            var pressureCheck = Find<CheckBox>(form, "LayoutShowPressureCheck");
            Require(pressureCheck.Text == "Pressure" && !pressureCheck.Checked,
                "The Project tab should have a Pressure checkbox (off by default) beside Labels, Flow and Sizes.");
            var pressureCaptions = shortcutModel.Ducts
                .Select(duct => form.DuctSystemScreen.DuctInletPressureCaption(duct.Sequence))
                .ToArray();
            Require(pressureCaptions.Any(caption => caption.Length > 0)
                    && pressureCaptions.Where(caption => caption.Length > 0).All(caption => caption.StartsWith("SP ", StringComparison.Ordinal)
                        && (caption.EndsWith(" in. w.c.", StringComparison.Ordinal) || caption.EndsWith(" Pa", StringComparison.Ordinal))),
                $"Duct pressure labels should read 'SP <value> in. w.c.' from the last calculation. First: {pressureCaptions.FirstOrDefault()}");
            pressureCheck.Checked = true;
            Application.DoEvents();
            using (var labelPaint = new Bitmap(Math.Max(1, shortcutCanvas.Width), Math.Max(1, shortcutCanvas.Height)))
            using (var labelGraphics = Graphics.FromImage(labelPaint))
                typeof(LayoutDesignerCanvas).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(shortcutCanvas, new object[] { new PaintEventArgs(labelGraphics, new Rectangle(Point.Empty, labelPaint.Size)) });
            var paintedLabels = ((System.Collections.IEnumerable)typeof(LayoutDesignerCanvas)
                    .GetField("_paintedLabels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shortcutCanvas)!)
                .Cast<object>().ToArray();
            Require(paintedLabels.Length > 0, $"Shown labels should be recorded so they can be dragged. canvas={shortcutCanvas.Size}, labels={shortcutCanvas.ShowLabels}, visible={shortcutCanvas.Visible}");
            var firstLabel = paintedLabels[0];
            var labelKey = (string)firstLabel.GetType().GetProperty("Key")!.GetValue(firstLabel)!;
            var labelBounds = (Rectangle)firstLabel.GetType().GetProperty("Bounds")!.GetValue(firstLabel)!;
            var labelStart = new Point(labelBounds.Left + labelBounds.Width / 2, labelBounds.Top + labelBounds.Height / 2);
            void CanvasMouse(string method, MouseButtons button, Point point) =>
                typeof(LayoutDesignerCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(shortcutCanvas, new object[] { new MouseEventArgs(button, 1, point.X, point.Y, 0) });
            var placementsBefore = shortcutModel.LayoutLabelPlacements.Count;
            CanvasMouse("OnMouseDown", MouseButtons.Left, labelStart);
            CanvasMouse("OnMouseMove", MouseButtons.Left, new Point(labelStart.X + 40, labelStart.Y + 25));
            CanvasMouse("OnMouseUp", MouseButtons.Left, new Point(labelStart.X + 40, labelStart.Y + 25));
            Application.DoEvents();
            var dragModel = form.DuctSystemScreen._ductSystem;
            Require(dragModel.LayoutLabelPlacements.TryGetValue(labelKey, out var dragged)
                    && (Math.Abs(dragged.OffsetX) > 1 || Math.Abs(dragged.OffsetY) > 1) && !dragged.HasLeader,
                $"Dragging a label should save its new offset. key={labelKey}");
            var withLeader = LayoutDesignerScreen.ToggleLeader(dragged!);
            var withoutLeader = LayoutDesignerScreen.ToggleLeader(withLeader);
            var fresh = LayoutDesignerScreen.ToggleLeader(new LayoutLabelPlacement());
            Require(withLeader.HasLeader && !withoutLeader.HasLeader
                    && withLeader.OffsetX == dragged!.OffsetX && withLeader.OffsetY == dragged.OffsetY
                    && fresh.HasLeader && (fresh.OffsetX != 0 || fresh.OffsetY != 0),
                "Add leader should keep a moved label where it is (and move a label in its default place out so the leader shows); Remove leader should drop it.");
            ClickButton(Find<Button>(form, "LayoutUndoButton"));
            Application.DoEvents();
            Require(form.DuctSystemScreen._ductSystem.LayoutLabelPlacements.Count == placementsBefore
                    && !form.DuctSystemScreen._ductSystem.LayoutLabelPlacements.ContainsKey(labelKey),
                "Undo should put a dragged label back.");
            pressureCheck.Checked = false;
            Application.DoEvents();

            var viewSelector = Find<ComboBox>(form, "LayoutViewModeSelector");
            viewSelector.SelectedIndex = 2;
            Application.DoEvents();
            viewSelector.SelectedIndex = 0;
            Application.DoEvents();
            var isoAngles = shortcutCanvas.CurrentViewAngles;
            Require(shortcutCanvas.OrbitView is null && Math.Abs(isoAngles.Azimuth - 45) < 0.01 && Math.Abs(isoAngles.Elevation - 30) < 0.01,
                $"Isometric Positive should be the 45 degree / 30 degree view. Got {isoAngles}.");
            var underlay = new LayoutPdfBackground
            {
                PageWidthPoints = 1224, PageHeightPoints = 792, PaperInches = 0.25, DrawingFeet = 1, CenterXFeet = 20, CenterYFeet = 10, CenterZFeet = 0
            };
            var isoCorners = shortcutCanvas.PdfBackgroundCorners(underlay);
            Require(isoCorners.All(corner => float.IsFinite(corner.X) && float.IsFinite(corner.Y))
                    && Math.Abs(isoCorners[0].Y - isoCorners[1].Y) > 1,
                "In an isometric view the underlay should be drawn as a slanted sheet in the X-Y plane at its Z elevation.");
            underlay.CenterZFeet = 10;
            var raisedCorners = shortcutCanvas.PdfBackgroundCorners(underlay);
            Require(raisedCorners[0].Y < isoCorners[0].Y - 1 && Math.Abs(raisedCorners[0].X - isoCorners[0].X) < 0.5,
                "Raising the underlay's Center Z should move it straight up on screen in an isometric view.");
            shortcutCanvas.ViewFromCubeCorner(-1, 1, -1);
            Application.DoEvents();
            Require(shortcutCanvas.OrbitView is { } cubeView && Math.Abs(cubeView.Elevation + 30) < 0.01
                    && viewSelector.Items.Count == 4 && viewSelector.SelectedIndex == 3
                    && Convert.ToString(viewSelector.SelectedItem) == "Free view"
                    && shortcutCanvas.ViewCubeCorners().Count == 8,
                $"Clicking a view-cube corner should rotate to that corner and show 'Free view'. view={shortcutCanvas.OrbitView}, items={viewSelector.Items.Count}");
            viewSelector.SelectedIndex = 2;
            Application.DoEvents();
            Require(shortcutCanvas.OrbitView is null && viewSelector.Items.Count == 3 && shortcutCanvas.ViewMode == LayoutViewMode.TopDown,
                "Choosing a view in the list should snap back from a free rotation and remove 'Free view'.");
            Console.WriteLine("PASS  Pressure labels, label drag and leaders, view cube and underlay in all views");

            // Review aid: capture the remaining pop-up windows when asked (not a check).
            var moreDialogsDirectory = Environment.GetEnvironmentVariable("DUCTSIZER_MORE_DIALOGS_DIR");
            if (!string.IsNullOrWhiteSpace(moreDialogsDirectory))
            {
                var captureModel = form.DuctSystemScreen._ductSystem;
                foreach (var (name, create) in new (string, Func<Form>)[]
                         {
                             ("pdfprops", () => new PdfBackgroundPropertiesDialog(new LayoutPdfBackground { SourceFileName = "M-101 Mechanical Plan.pdf", PageNumber = 1, PageCount = 3, CenterZFeet = 0 })),
                             ("sourcesink", () => new SourceSinkEditorDialog(Enumerable.Range(0, 9).Select(index => new SavedLayoutNode
                             {
                                 Sequence = 900 + index, Type = LayoutNodeType.Reservoir,
                                 SpaceRole = index == 0 ? "Space Source" : "Space Sink",
                                 AirflowM3PerS = UnitConversions.CfmToM3PerS(index == 0 ? 2400 : 300), PressurePa = index == 0 ? 0 : -30
                             }).ToArray())),
                             ("ductclass", () => new DuctClassDialog(SmacnaLeakage.AnalyzeSides(captureModel), captureModel.AnalysisExpectations, UnitSystem.Imperial)),
                             ("analysissettings", () => new AnalysisSettingsDialog(captureModel.AnalysisExpectations, UnitSystem.Imperial)),
                             ("faninfo2", () => new FanInformationDialog("Fan-001\r\n2,400 cfm at 1.25 in. w.c. total static\r\n0.8 bhp; select a 1 hp motor"))
                         })
                {
                    using var captureDialog = create();
                    captureDialog.Show(form);
                    Application.DoEvents();
                    Thread.Sleep(150);
                    Application.DoEvents();
                    CaptureForm(captureDialog, Path.Combine(moreDialogsDirectory, name + ".png"));
                    captureDialog.Hide();
                }
            }
            var userBeforeNew = ribbonModel.UserName;
            ClickButton(Find<Button>(form, "RibbonNewProjectButton"));
            Application.DoEvents();
            var newModel = (DuctSystemDocument)UiMember.Field("_ductSystem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(!ReferenceEquals(newModel, ribbonModel) && newModel.LayoutNodes.Count == 0 && newModel.Ducts.Count == 0
                    && newModel.Fittings.Count == 0 && newModel.UserName == userBeforeNew
                    && string.IsNullOrEmpty(newModel.ProjectName)
                    && tabs.SelectedTab?.Name == "LayoutDesignerTab",
                "New Project should start an empty project, keep the user name, and show the Project tab.");
            Console.WriteLine("PASS  Responsive wide layout");
            Console.WriteLine("PASS  Responsive stacked layout");
            Console.WriteLine("PASS  Calculation method dropdowns remain enabled");
            Console.WriteLine("PASS  Automatic sizing round-down selection");
            Console.WriteLine("PASS  Duct system add and local save");
            Console.WriteLine("PASS  ASHRAE fitting tab and geometry-driven system add");
            Console.WriteLine("PASS  Duct description editing");
            Console.WriteLine("PASS  Compact system-table layout");
            Console.WriteLine("PASS  Duct edit and update workflow");
            Console.WriteLine("PASS  Junction path and fitting-specific dropdown inputs");
            Console.WriteLine("PASS  Project file save/open, PDF/CSV export, and CSV import actions");
            Console.WriteLine("PASS  Requested dropdown labels and fitting-description autofill");
            Console.WriteLine("PASS  Embedded ASHRAE fitting-image selector");
            Console.WriteLine("PASS  Complete fitting images, visual gallery, and editable fitting IDs");
            Console.WriteLine("PASS  Snap-grid layout designer and system branch metadata");
            Console.WriteLine("PASS  Layout rewiring and delete-all workflows");
            Console.WriteLine("PASS  Main tabs and Project ribbon commands");
            Console.WriteLine("PASS  Selection Inspector: description, calculations, notes");
            Console.WriteLine("PASS  Project Explorer, Errors list and Net Path Static Pressure graph");
            Console.WriteLine("PASS  Analysis summary, report preview and Print Report");
            Console.WriteLine("PASS  Display scaling, resizable panels and in-place editing in the inspector");
            Console.WriteLine("PASS  Analysis Settings, expected-range column and pressure drop along each path");
            Console.WriteLine("PASS  Designer shortcuts: Esc deselects, Delete removes, right-click Edit / Delete");
            Console.WriteLine("PASS  SMACNA pressure class and leakage, surface areas, donut and scrolling Analysis page");
            Console.WriteLine("24/24 UI verification checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            var failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            Console.WriteLine($"FAIL  {failure.Message}");
            Console.WriteLine(failure.StackTrace);
            return 1;
        }
        finally
        {
            AutomationOptions.Reset();
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Gives the connection into <paramref name="downstream"/> a sized duct (a copy of <paramref name="template"/>,
    /// 1 ft long). Since v0.95 an unsized connection blocks pressure results instead of counting as zero loss.
    /// </summary>
    private static SavedDuct SizeConnection(DuctSystemDocument document, SavedLayoutNode downstream, SavedDuct template)
    {
        var duct = SavedDuct.FromResult(document.ReserveSequence(), $"Connection into {downstream.Id}",
            template.Calculate(), downstream.AirSystemType, downstream.BranchId);
        duct.LengthM = UnitConversions.FeetToM(1);
        document.Ducts.Add(duct);
        downstream.ConnectionDuctSequence = duct.Sequence;
        return duct;
    }

    private static IEnumerable<string> Ancestors(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
            yield return $"{current.GetType().Name}:{current.Name}:{current.Visible}";
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        var matches = root.Controls.Find(name, true);
        if (matches.Length != 1 || matches[0] is not T typed)
            throw new InvalidOperationException($"Could not find {typeof(T).Name} named {name}.");
        return typed;
    }

    /// <summary>Invokes a button's click handlers whether or not its ribbon page is on screen.</summary>
    private static void ClickButton(Control button) =>
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(button, new object[] { EventArgs.Empty });

    private static T PrivateField<T>(object instance, string name) where T : class
    {
        var field = instance is MainForm
            ? UiMember.Field(name, BindingFlags.Instance | BindingFlags.NonPublic)
              ?? throw new InvalidOperationException($"Could not find private field {name}.")
            : new UiField(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                          ?? throw new InvalidOperationException($"Could not find private field {name}."), null);
        return field.GetValue(instance) as T
               ?? throw new InvalidOperationException($"Private field {name} was not a {typeof(T).Name}.");
    }

    private static void RaiseCellDoubleClick(DataGridView grid, int columnIndex, int rowIndex)
    {
        var method = typeof(DataGridView).GetMethod("OnCellDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException("Could not access DataGridView double-click behavior.");
        method.Invoke(grid, new object[] { new DataGridViewCellEventArgs(columnIndex, rowIndex) });
    }

    private static bool IsDescendantOf(Control control, Control ancestor)
    {
        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    private static Rectangle ScreenBounds(Control control) =>
        new(control.PointToScreen(Point.Empty), control.Size);

    private static string CreateTestCompanyLogo()
    {
        using var bitmap = new Bitmap(420, 96);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var blue = new SolidBrush(Color.FromArgb(28, 69, 108));
            using var green = new SolidBrush(Color.FromArgb(42, 135, 91));
            graphics.FillEllipse(green, 8, 8, 80, 80);
            using var titleFont = new Font("Segoe UI Semibold", 23F);
            graphics.DrawString("NORTH HVAC", titleFont, blue, 102, 26);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T typed)
                yield return typed;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    /// <summary>A 96-DPI pixel length at the display scale the checks run at (100 % in CI, 150 % on a laptop).</summary>
    private static int Px(int pixels) => DpiScaling.Scale(pixels);

    /// <summary>A designed window size at the display scale, kept within the screen's working area.</summary>
    private static Size Sz(Form form, int width, int height)
    {
        var area = Screen.FromControl(form).WorkingArea;
        return new Size(Math.Min(Px(width), area.Width), Math.Min(Px(height), area.Height));
    }

    /// <summary>Saves what is on screen inside a window (review aid; DrawToBitmap misses child windows under Wine).</summary>
    private static void CaptureForm(Control window, string path)
    {
        window.FindForm()?.BringToFront();
        window.Refresh();
        Application.DoEvents();
        Thread.Sleep(300);
        Application.DoEvents();
        var bounds = window is Form ? window.Bounds : window.RectangleToScreen(window.ClientRectangle);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path);
    }

    private static void Require(bool condition, string message)
    {
        if (condition)
            return;
        // Review aid: FANWORKS_UI_CONTINUE lists every failed check in one run instead of stopping at the first.
        if (Environment.GetEnvironmentVariable("FANWORKS_UI_CONTINUE") is { Length: > 0 })
        {
            Console.WriteLine($"SOFTFAIL {message}");
            return;
        }
        throw new InvalidOperationException(message);
    }
}


/// <summary>
/// Finds a non-public member of the main window. Since step 9 each tab is its own screen class owned by the window,
/// so a member is looked up on <see cref="MainForm"/> first and then on its screen objects; Invoke/GetValue/SetValue
/// take the window as target and redirect to the owning screen.
/// </summary>
internal static class UiMember
{
    private static IEnumerable<(Type Type, Func<object?, object?> Owner)> Owners()
    {
        yield return (typeof(MainForm), target => target);
        foreach (var property in typeof(MainForm).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
                     .Where(property => property.PropertyType.Name.EndsWith("Screen", StringComparison.Ordinal)))
            yield return (property.PropertyType, target => target is null ? null : property.GetValue(target));
    }

    internal static UiMethod? Method(string name, BindingFlags flags)
    {
        foreach (var (type, owner) in Owners())
            if (type.GetMethod(name, flags) is { } method)
                return new UiMethod(method, method.IsStatic ? _ => null : owner);
        return null;
    }

    internal static UiMethod? Method(string name, BindingFlags flags, Binder? binder, Type[] types, ParameterModifier[]? modifiers)
    {
        foreach (var (type, owner) in Owners())
            if (type.GetMethod(name, flags, binder, types, modifiers) is { } method)
                return new UiMethod(method, method.IsStatic ? _ => null : owner);
        return null;
    }

    internal static UiField? Field(string name, BindingFlags flags)
    {
        foreach (var (type, owner) in Owners())
            if (type.GetField(name, flags) is { } field)
                return new UiField(field, field.IsStatic ? _ => null : owner);
        return null;
    }
}

internal sealed class UiMethod(MethodInfo method, Func<object?, object?> owner)
{
    public MethodInfo Info => method;
    public object? Invoke(object? target, object?[]? arguments) => method.Invoke(owner(target), arguments);
    public ParameterInfo[] GetParameters() => method.GetParameters();
    public Type ReturnType => method.ReturnType;
}

internal sealed class UiField(FieldInfo field, Func<object?, object?>? owner)
{
    public Type FieldType => field.FieldType;
    public object? GetValue(object? target) => field.GetValue(owner is null ? target : owner(target));
    public void SetValue(object? target, object? value) => field.SetValue(owner is null ? target : owner(target), value);
}

/// <summary>A PDF's bytes with its compressed (FlateDecode) page streams expanded, for text checks.</summary>
internal static class PdfText
{
    public static string Plain(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var result = new StringBuilder();
        var position = 0;
        while (true)
        {
            var marker = text.IndexOf(">>\nstream\n", position, StringComparison.Ordinal);
            if (marker < 0)
            {
                result.Append(text, position, text.Length - position);
                break;
            }
            var dataStart = marker + ">>\nstream\n".Length;
            var end = text.IndexOf("\nendstream", dataStart, StringComparison.Ordinal);
            var dictionaryStart = text.LastIndexOf("<<", marker, StringComparison.Ordinal);
            var dictionary = dictionaryStart >= 0 ? text[dictionaryStart..marker] : string.Empty;
            result.Append(text, position, dataStart - position);
            if (dictionary.Contains("/FlateDecode", StringComparison.Ordinal) && !dictionary.Contains("/Image", StringComparison.Ordinal)
                && !dictionary.Contains("/Length1", StringComparison.Ordinal))
            {
                using var input = new MemoryStream(pdf, dataStart, end - dataStart);
                using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                result.Append(Encoding.Latin1.GetString(output.ToArray()));
            }
            else
                result.Append(text, dataStart, end - dataStart);
            position = end;
        }
        return result.ToString();
    }
}

