using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The main tabs (Project, Analysis, Reports, Quick Calc) and the ribbon under them. The ribbon only
/// invokes existing commands of the screens; it holds no engineering logic and no project state of its own.
/// </summary>
internal sealed class MainRibbon
{
    internal const int RibbonHeight = 100;

    internal enum TopTab { Project, Analysis, Reports, QuickCalc }

    private readonly MainForm _host;
    private readonly Dictionary<TopTab, AppTabButton> _tabButtons = new();
    private readonly Dictionary<TopTab, Control> _pages = new();
    private TopTab _activeTab = TopTab.Project;
    private TabPage? _lastQuickCalcPage;

    private RibbonButton? _selectButton;
    private RibbonButton? _moveButton;
    private RibbonButton? _highlightButton;
    private RibbonButton? _criticalOnlyButton;
    private RibbonButton? _ductCalculatorButton;
    private RibbonButton? _fittingCalculatorButton;
    private readonly ToolTip _toolTips = new() { AutoPopDelay = 9000, InitialDelay = 400, ReshowDelay = 100 };

    internal MainRibbon(MainForm host)
    {
        _host = host;
    }

    private DuctSystemScreen SystemScreen => _host.DuctSystemScreen;
    private LayoutDesignerScreen Layout => _host.LayoutScreen;

    internal TopTab ActiveTab => _activeTab;

    /// <summary>The four main tabs, placed in the dark top bar.</summary>
    internal Control BuildTopTabs()
    {
        var strip = new FlowLayoutPanel
        {
            Name = "MainTopTabs",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(12, 0, 0, 0),
            Padding = Padding.Empty,
            BackColor = RibbonPalette.TopBar
        };
        AddTab(TopTab.Project, "TopTabProject", "Project", RibbonIcon.Project);
        AddTab(TopTab.Analysis, "TopTabAnalysis", "Analysis", RibbonIcon.Analysis);
        AddTab(TopTab.Reports, "TopTabReports", "Reports", RibbonIcon.Reports);
        AddTab(TopTab.QuickCalc, "TopTabQuickCalc", "Quick Calc", RibbonIcon.QuickCalc);
        return strip;

        void AddTab(TopTab tab, string name, string text, RibbonIcon icon)
        {
            var button = new AppTabButton(text, icon) { Name = name };
            button.Click += (_, _) => ShowTab(tab);
            _tabButtons[tab] = button;
            strip.Controls.Add(button);
        }
    }

    /// <summary>The ribbon: one row of command groups per main tab.</summary>
    internal Control BuildRibbon()
    {
        var ribbon = new RibbonHostPanel
        {
            Name = "MainRibbon",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(4, 3, 4, 1),
            BackColor = RibbonPalette.Ribbon
        };
        _pages[TopTab.Project] = BuildProjectRibbon();
        _pages[TopTab.Analysis] = BuildAnalysisRibbon();
        _pages[TopTab.Reports] = BuildReportsRibbon();
        _pages[TopTab.QuickCalc] = BuildQuickCalcRibbon();
        foreach (var page in _pages.Values)
            ribbon.Controls.Add(page);
        Layout.LayoutCommandStateChanged += SynchronizeWithPage;
        return ribbon;
    }

    private static RibbonPage NewRibbonPage(string name) => new(name);

    private static RibbonGroup AddGroup(RibbonPage page, string name, string caption)
    {
        var group = new RibbonGroup(name, caption);
        page.Controls.Add(group);
        return group;
    }

    private RibbonButton AddButton(RibbonGroup group, string name, string text, RibbonIcon icon, string toolTip,
        Action action, int width = 60)
    {
        var button = new RibbonButton(name, text, icon, toolTip, width);
        button.Click += (_, _) =>
        {
            if (button.DropDownMenu is null)
                action();
        };
        _toolTips.SetToolTip(button, toolTip);
        group.Content.Controls.Add(button);
        return button;
    }

    private Control BuildProjectRibbon()
    {
        var page = NewRibbonPage("RibbonProjectPage");
        page.FillWidth = true;

        // Round X: Project holds the file commands and Project Settings; Auto Size holds the sizing criteria (which now
        // include the rounding and calculation method) beside the command that uses them.
        var project = AddGroup(page, "RibbonProjectGroup", "Project");
        AddButton(project, "RibbonNewProjectButton", "New\nProject", RibbonIcon.NewProject,
            "Start a new project. You are asked before the current project is closed.", NewProject, 50);
        AddButton(project, "RibbonOpenButton", "Open", RibbonIcon.Open,
            "Open a FanWorks project (.fanworks), or import a CSV schedule.", () => SystemScreen.OpenProject(), 44);
        AddButton(project, "RibbonSaveButton", "Save", RibbonIcon.Save,
            "Save the project (.fanworks) with its PDF report and CSV schedule.", () => SystemScreen.ExportDuctSystem(), 42);
        AddButton(project, "RibbonSaveAsButton", "Save As", RibbonIcon.SaveAs,
            "Save the project under a new name or location.", SaveProjectAs, 48);
        AddButton(project, "RibbonProjectSettingsButton", "Project\nSettings", RibbonIcon.ProjectSettings,
            "Project information (name, number, designer, date, company logo) and units.",
            () => SystemScreen.EditProjectSettings(), 52);

        var autoSize = AddGroup(page, "RibbonAutoSizeGroup", "Auto Size");
        AddButton(autoSize, "RibbonSizingCriteriaButton", "Auto Sizing\nCriteria", RibbonIcon.SizingCriteria,
            "Duct size rounding, calculation method and the defaults Auto Size Ductwork uses for ducts and components.",
            () => SystemScreen.EditAutomaticSizingInputs(), 68);
        AddButton(autoSize, "RibbonAutoSizeButton", "Auto Size\nDuctwork", RibbonIcon.AutoSize,
            "Re-size all ductwork and fittings from the sources to the sinks with the Auto Sizing Criteria. Locked items keep their size.",
            () => Layout.AutoSizeLayout(), 62);

        var edit = AddGroup(page, "RibbonEditGroup", "Edit");
        _selectButton = AddButton(edit, "RibbonSelectButton", "Select", RibbonIcon.Select,
            "Select, connect and edit items on the grid.", () => Layout.UseLayoutSelectMode(), 44);
        _moveButton = AddButton(edit, "RibbonMoveButton", "Move", RibbonIcon.Move,
            "Move mode: drag components to tidy the drawing. Connections and calculations do not change.",
            () =>
            {
                Layout.ToggleLayoutRearrangeMode();
                SynchronizeWithPage();
            }, 44);

        AddComponentGroup(page, "RibbonSpacesGroup", "Spaces", new (LayoutNodeType, string, string, string?)[]
        {
            (LayoutNodeType.Reservoir, "Source", "Space Source: airflow, temperature, and source pressure", "Space Source"),
            (LayoutNodeType.Reservoir, "Sink", "Space Sink: calculated terminal airflow and temperature", "Space Sink")
        });
        AddComponentGroup(page, "RibbonEquipmentGroup", "Equipment", new (LayoutNodeType, string, string, string?)[]
        {
            (LayoutNodeType.Fan, "Fan", "Airflow, temperature, air system", null),
            (LayoutNodeType.Fan, "AHU", "Air handling unit: fan with filters and coils; internal and external static pressure",
                LayoutFanData.AirHandlingUnitTool),
            (LayoutNodeType.Coil, "Coil", "Temperature and pressure drop", null),
            (LayoutNodeType.Filter, "Filter", "Pressure-drop input", null),
            (LayoutNodeType.Fitting, "VAV", "Titus DESV VAV with or without hot-water reheat", FittingCatalog.VavSelectionGroup),
            (LayoutNodeType.CustomComponent, "Custom", "User-defined component label and static pressure drop", null)
        });
        AddComponentGroup(page, "RibbonFittingsGroup", "Fittings", new (LayoutNodeType, string, string, string?)[]
        {
            (LayoutNodeType.Fitting, "Elbow", "ASHRAE elbow", "Elbows"),
            (LayoutNodeType.Fitting, "Transition", "ASHRAE transition", "Transitions"),
            (LayoutNodeType.Fitting, "Split", "Diverting fitting: one inlet, two outlets", "Diverting Fittings"),
            (LayoutNodeType.Fitting, "Merge", "Converging fitting: two inlets, one outlet", "Converging Fittings"),
            (LayoutNodeType.Fitting, "Damper", "ASHRAE damper", "Dampers"),
            (LayoutNodeType.Fitting, "Fan/Plenum", "Fan exits and plenum connections", FittingCatalog.FanExitsAndPlenumsSelectionGroup)
        });
        AddComponentGroup(page, "RibbonTerminalsGroup", "Terminals", new (LayoutNodeType, string, string, string?)[]
        {
            (LayoutNodeType.Fitting, "Terminal", "Entries, exits, terminals and takeoffs", FittingCatalog.TerminalSelectionGroup),
            (LayoutNodeType.Grd, "GRD", "Grille, square diffuser, or linear diffuser", "GRDs")
        });
        AddComponentGroup(page, "RibbonPointsGroup", "Points", new (LayoutNodeType, string, string, string?)[]
        {
            (LayoutNodeType.CalculationPoint, "Pressure", "Pressure calculation point", "Pressure"),
            (LayoutNodeType.CalculationPoint, "Temp", "Temperature calculation point", "Temperature"),
            (LayoutNodeType.CalculationPoint, "Velocity", "Velocity calculation point", "Velocity"),
            (LayoutNodeType.CalculationPoint, "Airflow", "Airflow calculation point", "Airflow")
        });

        var calculate = AddGroup(page, "RibbonCalculateGroup", "Calculate");
        AddButton(calculate, "RibbonCalculateButton", "Calculate\nCritical Path", RibbonIcon.Calculate,
            "Calculate the system: find the critical path, size the fan to it and highlight it.",
            () => SystemScreen.CalculateSystem(), 72);
        _highlightButton = AddButton(calculate, "RibbonHighlightCriticalPathButton", "Highlight\nCritical Path",
            RibbonIcon.CriticalPath, "Highlight or unhighlight the critical path on the drawing.",
            () => Layout.ToggleLayoutCriticalPathHighlight(), 76);
        return page;
    }

    private void AddComponentGroup(RibbonPage page, string name, string caption,
        IReadOnlyList<(LayoutNodeType Type, string Title, string Detail, string? Group)> tools)
    {
        var group = AddGroup(page, name, caption);
        var grid = new RibbonToolGrid(name + "Tools");
        foreach (var (type, title, detail, fittingGroup) in tools)
        {
            var tool = Layout.CreateRibbonLayoutTool(type, title, detail, fittingGroup);
            tool.Click += (_, _) => ShowTab(TopTab.Project);
            grid.Controls.Add(tool);
        }
        group.Content.Controls.Add(grid);
    }

    private Control BuildAnalysisRibbon()
    {
        var page = NewRibbonPage("RibbonAnalysisPage");
        var project = AddGroup(page, "RibbonAnalysisProjectGroup", "Project");
        AddButton(project, "RibbonAnalysisSaveButton", "Save", RibbonIcon.Save,
            "Save the project (.fanworks) with its PDF report and CSV schedule.", () => SystemScreen.ExportDuctSystem(), 52);
        // Calculate is on the Project ribbon (the same command); the Analysis tab shows its results (round X).
        var system = AddGroup(page, "RibbonAnalysisSystemGroup", "System");
        _criticalOnlyButton = AddButton(system, "RibbonCriticalPathOnlyButton", "Critical\nPath Only",
            RibbonIcon.Filter, "Show only the items on the critical path, or all items.", () =>
            {
                SystemScreen.ToggleCriticalPathFilter();
                SynchronizeWithPage();
            }, 66);
        AddButton(system, "RibbonFanInformationButton", "Fan\nInformation", RibbonIcon.Fan,
            "Fan airflow, static pressure, efficiency and brake horsepower.", () => SystemScreen.ViewFanInformation(), 70);
        var settings = AddGroup(page, "RibbonAnalysisSettingsGroup", "Settings");
        AddButton(settings, "RibbonAnalysisSettingsButton", "Analysis\nSettings", RibbonIcon.AnalysisSettings,
            "Expected ranges for duct friction rate and velocity, fitting velocity and fitting loss.",
            () => SystemScreen.EditAnalysisSettings(), 70);
        AddButton(settings, "RibbonDuctClassButton", "Customize\nDuct Class", RibbonIcon.ProjectSettings,
            "Calculated SMACNA pressure class and recommended seal class, with overrides.",
            () => SystemScreen.EditDuctClass(), 76);
        return page;
    }

    private Control BuildReportsRibbon()
    {
        var page = NewRibbonPage("RibbonReportsPage");
        var report = AddGroup(page, "RibbonReportGroup", "Report");
        AddButton(report, "RibbonPrintReportButton", "Print\nReport", RibbonIcon.PrintReport,
            "Save the project report as a PDF and open it for printing.", () => _reportPreview?.PrintReport());
        AddButton(report, "RibbonRefreshPreviewButton", "Refresh\nPreview", RibbonIcon.Reports,
            "Rebuild the report preview from the current project.", () => _reportPreview?.RefreshPreview(force: true));
        return page;
    }

    private Control BuildQuickCalcRibbon()
    {
        var page = NewRibbonPage("RibbonQuickCalcPage");
        var calculators = AddGroup(page, "RibbonCalculatorsGroup", "Calculators");
        _ductCalculatorButton = AddButton(calculators, "RibbonDuctCalculatorButton", "Duct\nCalculator",
            RibbonIcon.DuctCalculator, "Size or check a single duct without a project.",
            () => ShowQuickCalc(SystemScreen._calculatorTab, clearEditing: true), 68);
        _fittingCalculatorButton = AddButton(calculators, "RibbonFittingCalculatorButton", "Fitting\nCalculator",
            RibbonIcon.FittingCalculator, "Calculate a single ASHRAE fitting loss without a project.",
            () => ShowQuickCalc(SystemScreen._fittingTab, clearEditing: true), 68);
        return page;
    }

    private ReportPreview? _reportPreview;

    /// <summary>The Reports page: a page-by-page preview of the project report.</summary>
    internal Control BuildReportsPage() => _reportPreview = new ReportPreview(_host);

    internal ReportPreview? Reports => _reportPreview;

    /// <summary>Shows a main tab and its ribbon.</summary>
    internal void ShowTab(TopTab tab)
    {
        switch (tab)
        {
            case TopTab.Project:
                SystemScreen._mainTabs.SelectedTab = SystemScreen._layoutTab;
                break;
            case TopTab.Analysis:
                SystemScreen._mainTabs.SelectedTab = SystemScreen._systemTab;
                break;
            case TopTab.Reports:
                SystemScreen._mainTabs.SelectedTab = SystemScreen._reportsTab;
                SynchronizeWithPage();
                _reportPreview?.RefreshPreview();
                return;
            case TopTab.QuickCalc:
                ShowQuickCalc(_lastQuickCalcPage ?? SystemScreen._calculatorTab, clearEditing: false);
                break;
        }
        SynchronizeWithPage();
    }

    private void ShowQuickCalc(TabPage page, bool clearEditing)
    {
        if (clearEditing || SystemScreen._mainTabs.SelectedTab != page)
        {
            if (page == SystemScreen._calculatorTab)
                SystemScreen.ClearDuctEditingContext();
            else
                SystemScreen.ClearFittingEditingContext();
        }
        SystemScreen._mainTabs.SelectedTab = page;
        SynchronizeWithPage();
    }

    /// <summary>Brings the tabs and ribbon in line with the page on screen and the current command states.</summary>
    internal void SynchronizeWithPage()
    {
        if (_pages.Count == 0)
            return;
        var selected = SystemScreen._mainTabs.SelectedTab;
        _activeTab = selected == SystemScreen._systemTab ? TopTab.Analysis
            : selected == SystemScreen._reportsTab ? TopTab.Reports
            : selected == SystemScreen._calculatorTab || selected == SystemScreen._fittingTab ? TopTab.QuickCalc
            : TopTab.Project;
        if (selected == SystemScreen._calculatorTab || selected == SystemScreen._fittingTab)
            _lastQuickCalcPage = selected;
        foreach (var (tab, button) in _tabButtons)
            button.Selected = tab == _activeTab;
        foreach (var (tab, page) in _pages)
            page.Visible = tab == _activeTab;

        if (_moveButton is not null)
            _moveButton.Checked = Layout._layoutRearrangeMode;
        if (_selectButton is not null)
            _selectButton.Checked = !Layout._layoutRearrangeMode;
        if (_highlightButton is not null)
            _highlightButton.Checked = Layout._layoutCriticalPathHighlighted;
        if (_criticalOnlyButton is not null)
            _criticalOnlyButton.Checked = SystemScreen._criticalPathFilterEnabled;
        if (_ductCalculatorButton is not null)
            _ductCalculatorButton.Checked = selected == SystemScreen._calculatorTab;
        if (_fittingCalculatorButton is not null)
            _fittingCalculatorButton.Checked = selected == SystemScreen._fittingTab;
    }

    private void NewProject()
    {
        if (!SystemScreen.ConfirmReplaceCurrentSystem("Start a new project?"))
            return;
        var current = SystemScreen._ductSystem;
        // The user's name and company logo carry over; everything about the old project is cleared.
        var document = new DuctSystemDocument
        {
            UserName = current.UserName,
            CompanyLogoPngBase64 = current.CompanyLogoPngBase64,
            ProjectDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        SystemScreen.ReplaceCurrentSystem(document);
        ShowTab(TopTab.Project);
        Layout._layoutStatusLabel.Text = "New project started. Enter the project details in Project Settings, then drag components onto the grid.";
        Layout._layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        if (!AutomationOptions.SuppressDialogs)
            SystemScreen.EditProjectSettings();
    }

    private void SaveProjectAs()
    {
        SystemScreen._lastExportBasePath = string.Empty;
        SystemScreen.ExportDuctSystem();
    }

    /// <summary>The ribbon background with a hairline along its bottom edge.</summary>
    private sealed class RibbonHostPanel : Panel
    {
        internal RibbonHostPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var border = new Pen(RibbonPalette.RibbonBorder);
            e.Graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
        }
    }
}
