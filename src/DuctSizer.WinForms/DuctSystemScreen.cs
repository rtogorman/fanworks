using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed partial class DuctSystemScreen
{
    private readonly MainForm _host;

    internal DuctSystemScreen(MainForm host)
    {
        _host = host;
    }

    internal readonly PolishedTabControl _mainTabs = new();
    internal readonly Button _saveButton = new();
    private readonly PictureBox _projectHeaderAppIcon = new();
    private readonly BrandWordmarkLabel _projectHeaderBrand = new();
    private readonly Label _projectHeaderLabel = new();
    private readonly PictureBox _projectHeaderLogo = new();
    internal string _lastExportBasePath = string.Empty;
    internal readonly TabPage _calculatorTab = new("Duct Calculator");
    internal readonly TabPage _fittingTab = new("Fitting Calculator");
    internal readonly TabPage _reportsTab = new("Reports");
    internal readonly TabPage _systemTab = new("Duct System");
    internal readonly TabPage _layoutTab = new("Layout Designer");
    internal readonly DataGridView _ductGrid = new();
    private readonly ContextMenuStrip _ductGridContextMenu = new();
    private readonly ToolStripMenuItem _editSystemItemMenuItem = new("Edit item")
        { Name = "DuctGridEditItemMenuItem" };
    private readonly ToolStripMenuItem _removeSystemItemMenuItem = new("Remove item")
        { Name = "DuctGridRemoveItemMenuItem" };
    private readonly Label _systemTotalLabel = new();
    private readonly Label _systemCriticalLabel = new();
    private readonly Label _systemCountLabel = new();
    internal readonly Label _systemStorageStatusLabel = new();
    internal readonly Button _addDuctButton = new();
    private readonly Button _deleteAllSystemButton = new();
    private readonly Button _exportSystemButton = new();
    private readonly Button _importSystemButton = new();
    private readonly Button _autoSizeFanButton = new();
    private readonly Button _viewFanInformationButton = new();
    private readonly Button _criticalPathFilterButton = new();
    internal DuctSystemDocument _ductSystem = new();
    internal DuctCalculationResult? _lastCalculation;
    internal int? _editingDuctSequence;
    internal int? _editingFittingSequence;
    internal const string DefaultDuctActionText = "Save Duct Changes";
    internal const string DefaultFittingActionText = "Save Fitting Changes";
    internal bool _criticalPathCalculated;
    internal bool _criticalPathFilterEnabled;
    internal HashSet<string> _criticalPathItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private double _criticalPathPressureLossPa;
    private string _criticalPathRoute = string.Empty;
    private IReadOnlyList<SystemCalculationIssue> _criticalPathIssues = Array.Empty<SystemCalculationIssue>();
    internal sealed record SystemRowTag(bool IsFitting, int Sequence);

    private string DuctSystemStoragePath
    {
        get
        {
            var overridePath = AutomationOptions.SystemStoragePath;
            if (!string.IsNullOrWhiteSpace(overridePath))
                return overridePath;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DuctSizer",
                "duct-system.json");
        }
    }

    internal void InitializeTabsAndSystem(Control calculatorRoot)
    {
        _mainTabs.Name = "MainTabs";
        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.Margin = Padding.Empty;

        calculatorRoot.Dock = DockStyle.Fill;
        _calculatorTab.Name = "CalculatorTab";
        _calculatorTab.Padding = new Padding(0);
        _calculatorTab.BackColor = _host.BackColor;
        _calculatorTab.Controls.Add(calculatorRoot);

        _systemTab.Name = "DuctSystemTab";
        _systemTab.Padding = new Padding(0);
        _systemTab.BackColor = _host.BackColor;
        _systemTab.Controls.Add(BuildDuctSystemPage());

        _fittingTab.Name = "FittingLossTab";
        _fittingTab.Padding = new Padding(0);
        _fittingTab.BackColor = _host.BackColor;
        _fittingTab.Controls.Add(_host.FittingScreen.BuildFittingLossPage());

        _layoutTab.Name = "LayoutDesignerTab";
        _layoutTab.Padding = new Padding(0);
        _layoutTab.BackColor = _host.BackColor;
        _layoutTab.Controls.Add(_host.LayoutScreen.BuildLayoutDesignerPage());

        _reportsTab.Name = "ReportsTab";
        _reportsTab.Padding = new Padding(0);
        _reportsTab.BackColor = _host.BackColor;
        _reportsTab.Controls.Add(_host.Ribbon.BuildReportsPage());

        // Page order is kept from earlier versions (code and checks address pages by position); which page shows
        // is chosen by the top tabs: Project = Layout Designer, Analysis = Duct System, Reports, Quick Calc =
        // the duct or fitting calculator.
        _mainTabs.TabPages.Add(_calculatorTab);
        _mainTabs.TabPages.Add(_fittingTab);
        _mainTabs.TabPages.Add(_systemTab);
        _mainTabs.TabPages.Add(_layoutTab);
        _mainTabs.TabPages.Add(_reportsTab);

        var topBar = BuildProjectIdentityHeader();
        var ribbon = _host.Ribbon.BuildRibbon();
        var shell = new TableLayoutPanel
        {
            Name = "MainShell",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = _host.BackColor
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, MainRibbon.RibbonHeight));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(topBar, 0, 0);
        shell.Controls.Add(ribbon, 0, 1);
        shell.Controls.Add(_mainTabs, 0, 2);
        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            SynchronizeSelectedSystemView();
            if (_mainTabs.SelectedTab == _fittingTab)
                _host.FittingScreen.ResetFittingInputScroll();
            _host.Ribbon.SynchronizeWithPage();
        };
        _host.Controls.Add(shell);
        // The application opens on the Project tab (the Layout Designer).
        _mainTabs.SelectedTab = _layoutTab;
        _host.Ribbon.SynchronizeWithPage();
        RefreshProjectIdentityHeader();
    }

    /// <summary>
    /// The dark top bar: application icon and name, the main tabs (Project, Analysis, Reports, Quick Calc), and the
    /// project identity (name, number, creator, last edit) with the company logo.
    /// </summary>
    private Control BuildProjectIdentityHeader()
    {
        var header = new ProjectIdentityHeaderPanel
        {
            Name = "ProjectIdentityHeader",
            Dock = DockStyle.Fill,
            BackColor = RibbonPalette.TopBar,
            Padding = new Padding(10, 0, 10, 0),
            Margin = Padding.Empty,
            ColumnCount = 5,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _projectHeaderAppIcon.Name = "ProjectHeaderAppIcon";
        _projectHeaderAppIcon.Dock = DockStyle.Fill;
        _projectHeaderAppIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _projectHeaderAppIcon.Margin = new Padding(4, 5, 2, 5);
        _projectHeaderAppIcon.BackColor = Color.Transparent;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream(
                   "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.png"))
        using (var sourceIcon = iconStream is null ? null : Image.FromStream(iconStream))
            _projectHeaderAppIcon.Image = sourceIcon is null ? _host.Icon?.ToBitmap() : new Bitmap(sourceIcon);

        _projectHeaderBrand.Name = "ProjectHeaderBrand";
        // The brand wordmark (Nunito ExtraBold outlines), its letters about as tall as the icon beside it. Its column
        // is sized to the wordmark, so the name is never cut off at any display scale.
        _projectHeaderBrand.Anchor = AnchorStyles.Left;
        _projectHeaderBrand.Text = "fan works";
        _projectHeaderBrand.ForeColor = Color.White;
        _projectHeaderBrand.Font = BrandFonts.Wordmark(16F);
        _projectHeaderBrand.Margin = new Padding(4, 0, 18, 0);
        _projectHeaderBrand.BackColor = Color.Transparent;

        _projectHeaderLogo.Name = "ProjectHeaderLogo";
        _projectHeaderLogo.Size = new Size(96, 34);
        _projectHeaderLogo.SizeMode = PictureBoxSizeMode.Zoom;
        _projectHeaderLogo.Margin = new Padding(8, 6, 0, 6);
        _projectHeaderLogo.Padding = new Padding(3);
        _projectHeaderLogo.BackColor = Color.White;
        _projectHeaderLabel.Name = "ProjectHeaderLabel";
        _projectHeaderLabel.Dock = DockStyle.Fill;
        _projectHeaderLabel.Padding = new Padding(8, 0, 4, 0);
        _projectHeaderLabel.TextAlign = ContentAlignment.MiddleRight;
        _projectHeaderLabel.ForeColor = RibbonPalette.TopBarText;
        _projectHeaderLabel.Font = new Font("Segoe UI", 8.5F);
        _projectHeaderLabel.AutoEllipsis = true;
        _projectHeaderLabel.BackColor = Color.Transparent;
        header.Controls.Add(_projectHeaderAppIcon, 0, 0);
        header.Controls.Add(_projectHeaderBrand, 1, 0);
        header.Controls.Add(_host.Ribbon.BuildTopTabs(), 2, 0);
        header.Controls.Add(_projectHeaderLabel, 3, 0);
        header.Controls.Add(_projectHeaderLogo, 4, 0);
        return header;
    }

    private void RefreshProjectIdentityHeader()
    {
        if (_projectHeaderLabel.IsDisposed)
            return;
        var project = string.IsNullOrWhiteSpace(_ductSystem.ProjectName) ? "Untitled Project" : _ductSystem.ProjectName;
        var number = string.IsNullOrWhiteSpace(_ductSystem.ProjectNumber) ? "No project number" : _ductSystem.ProjectNumber;
        var creator = string.IsNullOrWhiteSpace(_ductSystem.UserName) ? "No creator" : _ductSystem.UserName;
        var edited = DateTime.TryParse(_ductSystem.LastEditedUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var timestamp)
            ? timestamp.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)
            : "Not yet edited";
        _projectHeaderLabel.Text = $"{project}   •   {number}\r\n{creator}   •   Edited {edited}";
        _host.Text = $"FanWorks — {project}";
        var previous = _projectHeaderLogo.Image;
        _projectHeaderLogo.Image = null;
        previous?.Dispose();
        _projectHeaderLogo.Visible = false;
        if (string.IsNullOrWhiteSpace(_ductSystem.CompanyLogoPngBase64))
            return;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(_ductSystem.CompanyLogoPngBase64));
            using var image = Image.FromStream(stream);
            _projectHeaderLogo.Image = CropLogoWhitespace(image);
            _projectHeaderLogo.Visible = true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OutOfMemoryException)
        {
            _projectHeaderLogo.Visible = false;
        }
    }

    private static Bitmap CropLogoWhitespace(Image image)
    {
        using var source = new Bitmap(image);
        var left = source.Width;
        var top = source.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var visibleContent = pixel.A > 16 && (pixel.R < 246 || pixel.G < 246 || pixel.B < 246);
                if (!visibleContent)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
            return new Bitmap(source);

        const int breathingRoom = 2;
        left = Math.Max(0, left - breathingRoom);
        top = Math.Max(0, top - breathingRoom);
        right = Math.Min(source.Width - 1, right + breathingRoom);
        bottom = Math.Min(source.Height - 1, bottom + breathingRoom);
        var crop = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        return source.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    /// <summary>Opens Project Settings (project information, units, calculation settings) and applies a save.</summary>
    internal void EditProjectSettings(ProjectSettingsDialog.Page firstPage = ProjectSettingsDialog.Page.ProjectInformation)
    {
        using var dialog = new ProjectSettingsDialog(_ductSystem, _host._currentUnits, firstPage);
        if (dialog.ShowDialog(_host) != DialogResult.OK)
            return;
        _ductSystem.UserName = dialog.UserName;
        _ductSystem.ProjectName = dialog.ProjectName;
        _ductSystem.ProjectNumber = dialog.ProjectNumber;
        _ductSystem.ProjectDate = dialog.ProjectDate;
        _ductSystem.CompanyLogoPngBase64 = dialog.CompanyLogoPngBase64;
        _ductSystem.DefaultSizingIncrementM = dialog.SizingIncrementM;
        _ductSystem.DefaultSizeRoundingDirection = dialog.RoundingDirection;
        _ductSystem.DefaultEquivalentDiameterMethod = dialog.EquivalentDiameterMethod;
        _ductSystem.DefaultPressureLossBasis = dialog.PressureLossBasis;
        ApplyProjectCalculationSettings();
        SaveDuctSystem();
        RefreshProjectIdentityHeader();
        if (dialog.SelectedUnits != _host._currentUnits)
            _host._unitsCombo.SelectedIndex = dialog.SelectedUnits == UnitSystem.Imperial ? 0 : 1;
        _host.CalculatorScreen.CalculateAndDisplay();
        _host.Ribbon.SynchronizeWithPage();
    }

    /// <summary>Sets the autosize increment (ribbon Auto Duct Size Rounding); same field as Project Settings.</summary>
    internal void SetSizingIncrement(double incrementM)
    {
        if (!(incrementM > 0) || Math.Abs(_ductSystem.DefaultSizingIncrementM - incrementM) < 1e-12)
            return;
        _ductSystem.DefaultSizingIncrementM = incrementM;
        ApplyProjectCalculationSettings();
        SaveDuctSystem();
        _host.CalculatorScreen.CalculateAndDisplay();
    }

    internal void EditAutomaticSizingInputs()
    {
        using var dialog = new AutomaticSizingInputsDialog(_ductSystem.AutomaticSizing, _host._currentUnits);
        if (dialog.ShowDialog(_host) != DialogResult.OK)
            return;
        _ductSystem.AutomaticSizing = dialog.Settings;
        SaveDuctSystem();
        _host.LayoutScreen._layoutStatusLabel.Text = "Automatic sizing defaults were saved for ducts and supported Designer components.";
        _host.LayoutScreen._layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    internal void ApplyProjectCalculationSettings()
    {
        _host._updatingControls = true;
        try
        {
            _host.CalculatorScreen._sizeIncrementText.Text = (_host._currentUnits == UnitSystem.Imperial
                ? UnitConversions.MToInches(_ductSystem.DefaultSizingIncrementM)
                : UnitConversions.MToMillimeters(_ductSystem.DefaultSizingIncrementM))
                .ToString("0.####", CultureInfo.CurrentCulture);
            SelectChoice(_host.CalculatorScreen._equivalentMethodCombo, _ductSystem.DefaultEquivalentDiameterMethod);
            SelectChoice(_host.CalculatorScreen._pressureBasisCombo, _ductSystem.DefaultPressureLossBasis);
        }
        finally
        {
            _host._updatingControls = false;
        }
    }

    private void SynchronizeSelectedSystemView()
    {
        if (_mainTabs.SelectedTab == _systemTab)
        {
            if (_ductSystem.LayoutNodes.Count > 0)
            {
                foreach (var node in _ductSystem.LayoutNodes)
                    _host.LayoutScreen.SyncLinkedItemMetadata(node);
            }
            RefreshDuctSystemGrid();
        }
        else if (_mainTabs.SelectedTab == _layoutTab)
        {
            if (_ductSystem.LayoutNodes.Count > 0)
                SynchronizeLayoutMetadataFromSystem();
            _host.LayoutScreen.RefreshLayoutDesigner();
            _host.LayoutScreen.EnsureLayoutViewportPositioned();
        }
    }

    private void SynchronizeLayoutMetadataFromSystem()
    {
        foreach (var node in _ductSystem.LayoutNodes)
        {
            if (node.LinkedFittingSequence is int fittingSequence
                && _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence) is { } fitting)
            {
                node.AirflowM3PerS = fitting.AirflowM3PerS;
                node.TemperatureC = fitting.TemperatureC;
                node.AirSystemType = fitting.AirSystemType;
                node.BranchId = fitting.BranchId;
                node.DeltaXM = fitting.DeltaXM;
                node.DeltaYM = fitting.DeltaYM;
                node.DeltaZM = fitting.DeltaZM;
                continue;
            }

            var ductSequence = node.LinkedDuctSequence ?? node.ConnectionDuctSequence ?? node.SecondaryConnectionDuctSequence;
            if (ductSequence is not int sequence
                || _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == sequence) is not { } duct)
                continue;
            node.AirflowM3PerS = duct.AirflowM3PerS;
            node.AirSystemType = duct.AirSystemType;
            node.BranchId = duct.BranchId;
            node.DeltaXM = duct.DeltaXM;
            node.DeltaYM = duct.DeltaYM;
            node.DeltaZM = duct.DeltaZM;
            if (node.Type == LayoutNodeType.Duct)
                node.TemperatureC = duct.TemperatureC;
        }
    }

    private Control BuildDuctSystemPage()
    {
        // Analysis tab: one page that scrolls as a whole. Summary (overview + top ten pressure drops), the pressure drop
        // along each path with the system summary donut, table actions, the full table (as tall as its rows, so it
        // has no scroll bar of its own), status. The top-ten list keeps its own scroll bar.
        var page = new Panel
        {
            Name = "AnalysisPageScroll",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _host.BackColor
        };
        var root = new TableLayoutPanel
        {
            Name = "AnalysisPageContent",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = _host.BackColor,
            Padding = new Padding(14)
        };
        _analysisPageScroll = page;
        _analysisPageContent = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 570F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 350F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
        _systemTotalLabel.Name = "SystemTotalLabel";
        _systemTotalLabel.Dock = DockStyle.Fill;
        _systemTotalLabel.TextAlign = ContentAlignment.MiddleLeft;
        _systemTotalLabel.ForeColor = Color.FromArgb(11, 31, 59);
        _systemTotalLabel.Font = new Font("Segoe UI Semibold", 10.5F);
        _systemTotalLabel.AutoEllipsis = false;
        _systemCriticalLabel.Name = "SystemCriticalPathLabel";
        _systemCriticalLabel.Dock = DockStyle.Fill;
        _systemCriticalLabel.TextAlign = ContentAlignment.MiddleLeft;
        _systemCriticalLabel.ForeColor = Color.FromArgb(14, 111, 230);
        _systemCriticalLabel.Font = new Font("Segoe UI Semibold", 10F);
        _systemCriticalLabel.Text = "Critical path: not calculated";
        _systemCriticalLabel.AutoEllipsis = true;
        _systemCountLabel.Dock = DockStyle.Fill;
        _systemCountLabel.TextAlign = ContentAlignment.MiddleRight;
        _systemCountLabel.ForeColor = Color.FromArgb(72, 100, 124);
        _systemCountLabel.Font = new Font("Segoe UI Semibold", 10F);
        summary.Controls.Add(_systemTotalLabel, 0, 0);
        summary.Controls.Add(_systemCriticalLabel, 1, 0);
        summary.Controls.Add(_systemCountLabel, 2, 0);
        var analysisSummary = BuildAnalysisSummary(summary);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 6),
            Margin = Padding.Empty,
            BackColor = _host.BackColor
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        var primaryActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = _host.BackColor
        };
        var destructiveActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = _host.BackColor
        };
        ConfigureSystemButton(_deleteAllSystemButton, "Delete all", 102);
        ConfigureSystemButton(_autoSizeFanButton, "Auto-size Fan", 135);
        ConfigureSystemButton(_viewFanInformationButton, "View Fan Information", 175);
        ConfigureSystemButton(_criticalPathFilterButton, "Isolate Critical Path", 185);
        _deleteAllSystemButton.Name = "DeleteAllSystemButton";
        _autoSizeFanButton.Name = "AutoSizeFanButton";
        _viewFanInformationButton.Name = "ViewFanInformationButton";
        _criticalPathFilterButton.Name = "CriticalPathFilterButton";
        _deleteAllSystemButton.Click += (_, _) => DeleteAllSystemItems();
        _autoSizeFanButton.Click += (_, _) => AutoSizeFan();
        _viewFanInformationButton.Click += (_, _) => ViewFanInformation();
        _criticalPathFilterButton.Click += (_, _) => ToggleCriticalPathFilter();
        _autoSizeFanButton.BackColor = Color.FromArgb(14, 111, 230);
        _autoSizeFanButton.ForeColor = Color.White;
        _autoSizeFanButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _criticalPathFilterButton.Font = new Font("Segoe UI Semibold", 7.5F);
        _autoSizeFanButton.FlatAppearance.BorderColor = Color.FromArgb(14, 111, 230);
        _criticalPathFilterButton.Enabled = false;
        // Calculate (fan sizing) and Fan Information are ribbon commands since v0.96; the filter stays with the table.
        primaryActions.Controls.Add(_criticalPathFilterButton);
        destructiveActions.Controls.Add(_deleteAllSystemButton);
        actions.Controls.Add(primaryActions, 0, 0);
        actions.Controls.Add(destructiveActions, 1, 0);

        ConfigureDuctGrid();

        _systemStorageStatusLabel.Name = "SystemStorageStatusLabel";
        _systemStorageStatusLabel.Dock = DockStyle.Fill;
        _systemStorageStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(80, 96, 112);
        _systemStorageStatusLabel.AutoEllipsis = true;
        _systemStorageStatusLabel.Text = "Saved automatically on this computer. No database or network connection is used.";

        root.Controls.Add(analysisSummary, 0, 0);
        var graphs = new TableLayoutPanel
        {
            Name = "AnalysisGraphs",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = _host.BackColor
        };
        graphs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        graphs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        graphs.Controls.Add(BuildPressureProfileCard(), 0, 0);
        graphs.Controls.Add(BuildSystemSummaryCard(), 1, 0);
        root.Controls.Add(graphs, 0, 1);
        root.Controls.Add(actions, 0, 2);
        root.Controls.Add(_ductGrid, 0, 3);
        root.Controls.Add(_systemStorageStatusLabel, 0, 4);
        // The full table shows every row; the wheel over it scrolls the page.
        _ductGrid.ScrollBars = ScrollBars.Horizontal;
        _ductGrid.MouseWheel += (_, args) =>
        {
            if (_ductGrid.ScrollBars == ScrollBars.Both || args is not HandledMouseEventArgs handled)
                return;
            handled.Handled = true;
            ScrollAnalysisPage(-args.Delta / 120 * DpiScaling.Scale(64));
        };
        page.Controls.Add(root);
        return page;
    }

    private Panel? _analysisPageScroll;
    private TableLayoutPanel? _analysisPageContent;

    /// <summary>Scrolls the Analysis page by <paramref name="pixels"/> (positive = down).</summary>
    internal void ScrollAnalysisPage(int pixels)
    {
        if (_analysisPageScroll is null)
            return;
        var current = -_analysisPageScroll.AutoScrollPosition.Y;
        _analysisPageScroll.AutoScrollPosition = new Point(-_analysisPageScroll.AutoScrollPosition.X, Math.Max(0, current + pixels));
    }

    /// <summary>
    /// Makes the Analysis table as tall as its header and rows, so the whole table is on the page and the page scroll
    /// bar moves through it. A very large table (beyond about 30,000 pixels) keeps its own scroll bar instead.
    /// </summary>
    private void FitAnalysisTableHeight()
    {
        if (_analysisPageContent is null || _analysisPageContent.RowStyles.Count < 4)
            return;
        var rows = 0;
        foreach (DataGridViewRow row in _ductGrid.Rows)
            if (row.Visible)
                rows += row.Height;
        var needed = _ductGrid.ColumnHeadersHeight + rows + SystemInformation.HorizontalScrollBarHeight + 6;
        needed = Math.Max(needed, DpiScaling.Scale(140));
        const int largest = 30_000;
        _ductGrid.ScrollBars = needed > largest ? ScrollBars.Both : ScrollBars.Horizontal;
        var height = Math.Min(needed, largest);
        if (Math.Abs(_analysisPageContent.RowStyles[3].Height - height) > 0.5)
            _analysisPageContent.RowStyles[3].Height = height;
    }

    private static void ConfigureSystemButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Size = new Size(width, 34);
        button.Margin = new Padding(0, 0, 6, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Segoe UI Semibold", 8.2F);
        button.BackColor = Color.White;
        button.FlatAppearance.BorderColor = Color.FromArgb(180, 194, 207);
        UiPolish.RoundButton(button);
    }

    private void ConfigureDuctGrid()
    {
        _ductGrid.Name = "DuctSystemGrid";
        _ductGrid.Dock = DockStyle.Fill;
        _ductGrid.BackgroundColor = Color.White;
        _ductGrid.BorderStyle = BorderStyle.None;
        _ductGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _ductGrid.GridColor = Color.FromArgb(228, 234, 240);
        _ductGrid.AllowUserToAddRows = false;
        _ductGrid.AllowUserToDeleteRows = false;
        _ductGrid.AllowUserToResizeRows = false;
        _ductGrid.AllowUserToOrderColumns = true;
        _ductGrid.AutoGenerateColumns = false;
        _ductGrid.MultiSelect = false;
        _ductGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _ductGrid.RowHeadersVisible = false;
        _ductGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 236, 250);
        _ductGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(18, 53, 65);
        _ductGrid.DefaultCellStyle.BackColor = Color.White;
        _ductGrid.DefaultCellStyle.ForeColor = Color.FromArgb(39, 55, 64);
        _ductGrid.DefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
        _ductGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8.1F);
        _ductGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 251);
        _ductGrid.EnableHeadersVisualStyles = false;
        _ductGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 64, 78);
        _ductGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _ductGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 7.1F);
        _ductGrid.ColumnHeadersHeight = 50;
        _ductGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _ductGrid.RowTemplate.Height = 25;
        _ductGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        ConfigureDuctGridContextMenu();

        AddGridColumn("Id", "Item ID", 68, false);
        AddGridColumn("Description", "Description", 166, false);
        AddGridColumn("StaticDrop", "Static drop", 96, true);
        AddGridColumn("PressureIn", "Static pressure in", 108, true);
        AddGridColumn("PressureOut", "Static pressure out", 108, true);
        AddAirSystemGridColumn();
        AddGridColumn("BranchId", "Branch ID", 78, false);
        AddGridColumn("UpstreamId", "Upstream item ID", 88, true);
        AddGridColumn("DownstreamId", "Downstream item ID", 96, true);
        AddGridColumn("DeltaX", "ΔX", 68, false);
        AddGridColumn("DeltaY", "ΔY", 68, false);
        AddGridColumn("DeltaZ", "ΔZ", 68, false);
        AddGridColumn("AbsoluteX", "Absolute X", 82, true);
        AddGridColumn("AbsoluteY", "Absolute Y", 82, true);
        AddGridColumn("AbsoluteZ", "Absolute Z", 82, true);
        AddGridColumn("Airflow", "Airflow", 78, false);
        AddGridColumn("Shape", "Shape", 70, true);
        AddGridColumn("Size", "Duct size", 96, true);
        AddGridColumn("Length", "Length", 70, true);
        AddGridColumn("Area", "Area", 82, true);
        AddGridColumn("Velocity", "Velocity", 86, true);
        AddGridColumn("PressureRate", "Pressure rate", 98, true);
        AddGridColumn("Material", "Material", 130, true);
        AddGridColumn("Method", "Eq. diameter", 94, true);
        AddGridColumn("Basis", "Pressure basis", 104, true);
        AddGridColumn("Temperature", "Temperature", 84, true);
        AddGridColumn("SurfaceArea", "Surface area", 84, true);
        var displayOrder = new[]
        {
            "Id", "Description", "Airflow", "PressureIn", "PressureOut", "AirSystem", "Temperature",
            "PressureRate", "Velocity", "Length", "Size", "SurfaceArea", "StaticDrop", "BranchId", "UpstreamId",
            "DownstreamId", "DeltaX", "DeltaY", "DeltaZ", "AbsoluteX", "AbsoluteY", "AbsoluteZ",
            "Shape", "Area", "Material", "Method", "Basis"
        };
        for (var displayIndex = 0; displayIndex < displayOrder.Length; displayIndex++)
            _ductGrid.Columns[displayOrder[displayIndex]].DisplayIndex = displayIndex;
        _ductGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "GraphOrder",
            Visible = false,
            ValueType = typeof(int),
            SortMode = DataGridViewColumnSortMode.Automatic
        });

        _ductGrid.CellBeginEdit += (_, args) =>
        {
            if (args.RowIndex < 0 || args.ColumnIndex < 0)
                return;
            var columnName = _ductGrid.Columns[args.ColumnIndex].Name;
            var fittingId = columnName == "Id" && _ductGrid.Rows[args.RowIndex].Tag is SystemRowTag { IsFitting: true };
            if (_ductGrid.Rows[args.RowIndex].Tag is SavedLayoutNode
                && columnName is not ("AirSystem" or "BranchId" or "Airflow" or "DeltaX" or "DeltaY" or "DeltaZ"))
            {
                args.Cancel = true;
                return;
            }
            if (columnName != "Description" && columnName != "AirSystem" && columnName != "BranchId"
                && columnName != "Airflow" && columnName is not ("DeltaX" or "DeltaY" or "DeltaZ") && !fittingId)
                args.Cancel = true;
        };
        _ductGrid.CellValidating += ValidateEditedSystemCell;
        _ductGrid.CellEndEdit += (_, args) => SaveEditedSystemCell(args.RowIndex, args.ColumnIndex);
        _ductGrid.CellLeave += (_, args) =>
        {
            if (args.RowIndex >= 0 && args.ColumnIndex >= 0
                && _ductGrid.Columns[args.ColumnIndex].Name == "AirSystem"
                && _ductGrid.Rows[args.RowIndex].Cells[args.ColumnIndex] is DataGridViewComboBoxCell comboCell)
                comboCell.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
        };
        _ductGrid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex < 0 || args.ColumnIndex < 0)
                return;
            var columnName = _ductGrid.Columns[args.ColumnIndex].Name;
            var fittingId = columnName == "Id" && _ductGrid.Rows[args.RowIndex].Tag is SystemRowTag { IsFitting: true };
            if (columnName is "Description" or "AirSystem" or "BranchId" or "Airflow" or "DeltaX" or "DeltaY" or "DeltaZ" || fittingId)
            {
                _ductGrid.CurrentCell = _ductGrid.Rows[args.RowIndex].Cells[args.ColumnIndex];
                if (columnName == "AirSystem"
                    && _ductGrid.CurrentCell is DataGridViewComboBoxCell airSystemCell)
                    airSystemCell.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
                _ductGrid.BeginEdit(selectAll: true);
            }
            else
            {
                EditSelectedDuct();
            }
        };
        _ductGrid.SelectionChanged += (_, _) => UpdateSystemActionState();
        // Clicking an item's row rings it on the Analysis path graph (where it lies on a path shown).
        _ductGrid.CellClick += (_, args) =>
        {
            if (args.RowIndex >= 0 && _ductGrid.Columns.Contains("Id") && _ductGrid.Rows[args.RowIndex].Cells["Id"].Value is string id)
                HighlightOnPathGraph(id);
        };
    }

    private void ConfigureDuctGridContextMenu()
    {
        _editSystemItemMenuItem.Click += (_, _) => EditSelectedDuct();
        _removeSystemItemMenuItem.Click += (_, _) => RemoveSelectedDuct();
        _ductGridContextMenu.Items.AddRange(new ToolStripItem[]
        {
            _editSystemItemMenuItem,
            _removeSystemItemMenuItem
        });
        _ductGridContextMenu.Opening += (_, args) =>
        {
            UpdateSystemActionState();
            args.Cancel = !_editSystemItemMenuItem.Enabled;
        };
        _ductGrid.ContextMenuStrip = _ductGridContextMenu;
        _ductGrid.CellMouseDown += (_, args) =>
        {
            if (args.Button != MouseButtons.Right || args.RowIndex < 0 || args.ColumnIndex < 0)
                return;
            _ductGrid.ClearSelection();
            _ductGrid.Rows[args.RowIndex].Selected = true;
            _ductGrid.CurrentCell = _ductGrid.Rows[args.RowIndex].Cells[args.ColumnIndex];
            UpdateSystemActionState();
        };
    }

    private void ValidateEditedSystemCell(object? sender, DataGridViewCellValidatingEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0
            || _ductGrid.Columns[args.ColumnIndex].Name != "Id"
            || _ductGrid.Rows[args.RowIndex].Tag is not SystemRowTag { IsFitting: true } tag)
            return;

        var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
        if (fitting is null)
            return;
        var proposedId = Convert.ToString(args.FormattedValue, CultureInfo.CurrentCulture)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(proposedId))
        {
            MessageBox.Show(_host, "Enter a fitting ID.", "Fitting ID Required",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            args.Cancel = true;
            return;
        }
        if (string.Equals(proposedId, fitting.Id, StringComparison.OrdinalIgnoreCase))
            return;

        var duplicate = _ductSystem.Ducts.Any(x => string.Equals(x.Id, proposedId, StringComparison.OrdinalIgnoreCase))
                        || _ductSystem.Fittings.Any(x => x.Sequence != fitting.Sequence
                            && string.Equals(x.Id, proposedId, StringComparison.OrdinalIgnoreCase));
        if (!duplicate)
            return;

        var response = MessageBox.Show(
            _host,
            $"The item ID '{proposedId}' is already in use. Allow this duplicate ID?",
            "Duplicate Item ID",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            args.Cancel = true;
    }

    private void AddGridColumn(string name, string header, int width, bool readOnly)
    {
        _ductGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            MinimumWidth = 60,
            ReadOnly = readOnly,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private void AddAirSystemGridColumn()
    {
        var column = new DataGridViewComboBoxColumn
        {
            Name = "AirSystem",
            HeaderText = "Air type",
            Width = 98,
            MinimumWidth = 86,
            ReadOnly = false,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            DisplayStyleForCurrentCellOnly = false
        };
        column.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _ductGrid.Columns.Add(column);
    }

    internal void LoadDuctSystem()
    {
        try
        {
            _ductSystem = DuctSystemStore.Load(DuctSystemStoragePath);
            var itemCount = _ductSystem.Ducts.Count + _ductSystem.Fittings.Count;
            _systemStorageStatusLabel.Text = itemCount == 0
                ? "No saved items yet. Add a calculated duct or fitting to start the system. Data is stored locally with no database."
                : $"Loaded {itemCount} locally saved system item(s). Changes are saved automatically.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(80, 96, 112);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            // Never let the next automatic save overwrite a file we could not read:
            // move it aside so it can be recovered or sent with a problem report.
            DiagnosticLog.Error($"The working project {DuctSystemStoragePath} could not be loaded.", ex);
            var preservedPath = PreserveUnreadableWorkingFile();
            _ductSystem = new DuctSystemDocument();
            _systemStorageStatusLabel.Text = preservedPath is null
                ? $"The local duct system could not be loaded: {ex.Message}"
                : $"The local duct system could not be loaded ({ex.Message}). It was kept as {Path.GetFileName(preservedPath)} and a new empty system was started.";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
        }
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
        ApplyProjectCalculationSettings();
    }

    private string? PreserveUnreadableWorkingFile()
    {
        try
        {
            var path = DuctSystemStoragePath;
            if (!File.Exists(path))
                return null;
            var preserved = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(path)}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(path)}");
            File.Move(path, preserved);
            DiagnosticLog.Warning($"Unreadable working project moved to {preserved}.");
            return preserved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Error("The unreadable working project could not be moved aside.", ex);
            return null;
        }
    }

    internal void SaveDuctSystem()
    {
        try
        {
            _ductSystem.LastEditedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            DuctSystemStore.Save(DuctSystemStoragePath, _ductSystem);
            RefreshProjectIdentityHeader();
            _systemStorageStatusLabel.Text = "Saved automatically on this computer. No database or network connection is used.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            DiagnosticLog.Error($"Automatic save to {DuctSystemStoragePath} failed.", ex);
            _systemStorageStatusLabel.Text = $"The duct system is still open, but could not be saved locally: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
        }
    }

    internal void AddOrUpdateCurrentDuct()
    {
        var returnToDesigner = _host.LayoutScreen.PendingConnectionNode() is not null || _host.LayoutScreen.PendingLayoutNode(LayoutNodeType.Duct) is not null;
        var historyState = returnToDesigner ? _host.LayoutScreen.CaptureLayoutHistoryState() : null;
        _host.CalculatorScreen.CalculateAndDisplay();
        if (_lastCalculation is null)
            return;

        if (_editingDuctSequence is int sequence)
        {
            var index = _ductSystem.Ducts.FindIndex(x => x.Sequence == sequence);
            if (index >= 0)
            {
                var existing = _ductSystem.Ducts[index];
                var layoutNode = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                    x.LinkedDuctSequence == sequence || x.ConnectionDuctSequence == sequence
                                                     || x.SecondaryConnectionDuctSequence == sequence);
                if (!ConfirmAirflowOverride(layoutNode, _lastCalculation.Input.AirflowM3PerS, existing.Id))
                    return;
                var updated = SavedDuct.FromResult(sequence, existing.Description, _lastCalculation,
                    existing.AirSystemType, existing.BranchId);
                updated.DeltaXM = existing.DeltaXM;
                updated.DeltaYM = existing.DeltaYM;
                updated.DeltaZM = existing.DeltaZM;
                if (_host.LayoutScreen.PendingConnectionNode() is not null || _host.LayoutScreen.PendingLayoutNode(LayoutNodeType.Duct) is not null)
                    _host.LayoutScreen.ApplyLayoutMetadata(updated);
                _ductSystem.Ducts[index] = updated;
                _host._statusLabel.Text = $"Updated duct D-{sequence:000} in the duct system.";
            }
            else
            {
                AddNewDuct(_lastCalculation);
            }
        }
        else
        {
            AddNewDuct(_lastCalculation);
        }

        _editingDuctSequence = null;
        _addDuctButton.Text = DefaultDuctActionText;
        _addDuctButton.Visible = false;
        _host.CalculatorScreen._ductEditingLabel.Visible = false;
        if (historyState is not null)
            _host.LayoutScreen.CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _mainTabs.SelectedTab = returnToDesigner ? _layoutTab : _systemTab;
        if (returnToDesigner)
            _host.LayoutScreen._layoutStatusLabel.Text = "Duct configured and linked. Continue placing components on the grid.";
    }

    private void AddNewDuct(DuctCalculationResult result)
    {
        var sequence = _ductSystem.ReserveSequence();
        var duct = SavedDuct.FromResult(sequence, CreateAutomaticDescription(result), result,
            _ductSystem.DefaultAirSystemType, _ductSystem.DefaultBranchId);
        _host.LayoutScreen.ApplyLayoutMetadata(duct);
        _ductSystem.Ducts.Add(duct);
        _host._statusLabel.Text = $"Added duct D-{sequence:000} to the duct system.";
    }

    private string CreateAutomaticDescription(DuctCalculationResult result)
    {
        var shape = result.Input.Geometry.Shape == DuctShape.Round ? "Round" : "Rectangular";
        var airflow = _host._currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.M3PerSToCfm(result.Input.AirflowM3PerS):N0} cfm"
            : $"{UnitConversions.M3PerSToLps(result.Input.AirflowM3PerS):N0} L/s";
        return $"{shape} {_host.CalculatorScreen.FormatGeometry(result.Input.Geometry)} – {airflow}";
    }

    /// <summary>
    /// Ribbon "Calculate": sizes the fan to the critical path (as Auto-size Fan did on the Duct System tab), then
    /// highlights the critical path on the drawing. If the system cannot be calculated, the reasons are shown and
    /// nothing is changed.
    /// </summary>
    internal bool CalculateSystem()
    {
        if (!TryAutoSizeFan(out var message))
        {
            _host.LayoutScreen._layoutStatusLabel.Text = message.Replace("\r\n", " ", StringComparison.Ordinal);
            _host.LayoutScreen._layoutStatusLabel.ForeColor = Color.DarkOrange;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host, message, "Calculate", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _host.Ribbon.SynchronizeWithPage();
            return false;
        }

        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _systemStorageStatusLabel.Text = message;
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(14, 111, 230);
        _host.LayoutScreen.ShowLayoutCriticalPathHighlight();
        _host.LayoutScreen.RefreshLayoutDesigner();
        _host.LayoutScreen._layoutStatusLabel.Text = $"Calculated. {message} Critical path: {_criticalPathRoute}.";
        _host.LayoutScreen._layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        _host.Ribbon.SynchronizeWithPage();
        return true;
    }

    internal void AutoSizeFan()
    {
        if (!TryAutoSizeFan(out var message))
        {
            MessageBox.Show(_host, message, "Auto-size Fan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _systemStorageStatusLabel.Text = message;
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(14, 111, 230);
    }

    private bool TryAutoSizeFan(out string message)
    {
        var fans = _ductSystem.LayoutNodes.Where(x => x.Type == LayoutNodeType.Fan).ToArray();
        if (fans.Length == 0)
        {
            message = "No fan has been added. Add a fan in the Layout Designer before using Auto-size Fan.";
            return false;
        }
        if (fans.Length > 1)
        {
            message = "Only one fan is allowed in the duct system. Remove the extra fan before auto-sizing.";
            return false;
        }

        _host.LayoutScreen.UpdateCalculatedLayoutValues();
        var fan = fans[0];
        var requiredAirflow = FanSizing.RequiredAirflowM3PerS(_ductSystem, fan);
        if (requiredAirflow <= 0.0)
        {
            message = "A required system airflow could not be determined. Define an airflow in the fan's connected system first.";
            return false;
        }

        var priorAirflow = (fan.AirflowM3PerS, fan.AirflowUserSpecified, fan.AirflowOverride);
        fan.AirflowM3PerS = requiredAirflow;
        fan.AirflowUserSpecified = true;
        fan.AirflowOverride = false;
        _host.LayoutScreen.UpdateCalculatedLayoutValues();

        var route = ComputeLongestRoute();
        if (!route.IsComplete)
        {
            (fan.AirflowM3PerS, fan.AirflowUserSpecified, fan.AirflowOverride) = priorAirflow;
            _host.LayoutScreen.UpdateCalculatedLayoutValues();
            var reasons = route.Issues.Take(6).Select(issue => $"• {issue.ItemId}: {issue.Message}");
            message = route.ItemKeys.Count == 0 && route.Issues.Count == 0
                ? "A critical route could not be determined. Define the connected ducts and components first."
                : "The fan was not sized because the critical path is incomplete. Define or size these items first:\r\n"
                  + string.Join("\r\n", reasons)
                  + (route.Issues.Count > 6 ? $"\r\n• and {route.Issues.Count - 6} more" : string.Empty);
            return false;
        }

        fan.FanStaticPressurePa = Math.Max(0.0, route.PressureLossPa);
        fan.IsDefined = true;
        _host.LayoutScreen.UpdateCalculatedLayoutValues();
        _criticalPathCalculated = true;
        // Show exactly the items whose losses set the fan static pressure (same as "Isolate Critical Path").
        _criticalPathFilterEnabled = true;
        ApplyCriticalRoute(ComputeLongestRoute());
        var horsepower = TryBrakeHorsepower(requiredAirflow, fan.FanTotalStaticPressurePa, fan.FanEfficiencyPercent);
        // Same template for a fan and an air handling unit: external + internal = total static pressure.
        message = $"{fan.Id} was sized to {PdfAirflow(requiredAirflow)}, {FormatSystemTotal(fan.FanStaticPressurePa)} external static pressure "
                  + $"+ {FormatSystemTotal(fan.FanInternalStaticPressurePa)} internal = {FormatSystemTotal(fan.FanTotalStaticPressurePa)} total static pressure, "
                  + $"and {FormatHorsepower(horsepower)} at {fan.FanEfficiencyPercent:0.#}% efficiency.";
        return true;
    }

    /// <summary>Brake horsepower, or NaN when the fan inputs are invalid (shown as "not available").</summary>
    private static double TryBrakeHorsepower(double airflowM3PerS, double staticPressurePa, double efficiencyPercent)
    {
        try
        {
            return FanPerformance.BrakeHorsepower(airflowM3PerS, staticPressurePa, efficiencyPercent);
        }
        catch (ArgumentOutOfRangeException)
        {
            return double.NaN;
        }
    }

    private static string FormatHorsepower(double horsepower) => double.IsFinite(horsepower)
        ? $"{horsepower:0.###} hp"
        : "not available (check fan airflow, static pressure and efficiency)";

    internal void ViewFanInformation()
    {
        if (!TryGetFanInformation(out var information))
        {
            MessageBox.Show(_host, information, "Fan Information", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        using var dialog = new FanInformationDialog(information, _ductSystem.LayoutNodes.Any(x => x.IsAirHandlingUnit));
        dialog.ShowDialog(_host);
    }

    private bool TryGetFanInformation(out string information)
    {
        var fans = _ductSystem.LayoutNodes.Where(x => x.Type == LayoutNodeType.Fan).ToArray();
        if (fans.Length == 0)
        {
            information = "No fan has been added. Add a fan in the Layout Designer to view its selection information.";
            return false;
        }
        if (fans.Length > 1)
        {
            information = "Only one fan is allowed in the duct system. Remove the extra fan before viewing fan information.";
            return false;
        }

        _host.LayoutScreen.UpdateCalculatedLayoutValues();
        var fan = fans[0];
        var horsepower = TryBrakeHorsepower(fan.AirflowM3PerS, fan.FanTotalStaticPressurePa,
            fan.FanEfficiencyPercent);
        information = $"{fan.Id} — {fan.FanEquipmentName}\r\n\r\nAirflow: {PdfAirflow(fan.AirflowM3PerS)}\r\n"
                      + FanStaticPressureLines(fan, "\r\n")
                      + $"Fan efficiency: {fan.FanEfficiencyPercent:0.#}%\r\n" +
                      $"Required motor power: {FormatHorsepower(horsepower)}";
        return true;
    }

    /// <summary>
    /// Static pressure lines for a fan or air handling unit (the same for both), each ending in
    /// <paramref name="newLine"/>: external static pressure (the duct system, set by Auto-size), internal static
    /// pressure (an input; normally 0 for a bare fan) and their sum, the total static pressure the fan develops.
    /// </summary>
    internal string FanStaticPressureLines(SavedLayoutNode fan, string newLine) =>
        $"External static pressure (ESP): {FormatSystemTotal(fan.FanStaticPressurePa)}{newLine}"
        + $"Internal static pressure: {FormatSystemTotal(fan.FanInternalStaticPressurePa)}{newLine}"
        + $"Total static pressure: {FormatSystemTotal(fan.FanTotalStaticPressurePa)}{newLine}";

    internal void ToggleCriticalPathFilter()
    {
        if (!_criticalPathCalculated)
            return;
        _criticalPathFilterEnabled = !_criticalPathFilterEnabled;
        RefreshDuctSystemGrid();
    }

    internal void ResetCriticalPath()
    {
        _criticalPathCalculated = false;
        _criticalPathFilterEnabled = false;
        _criticalPathPressureLossPa = 0.0;
        _criticalPathRoute = string.Empty;
        _criticalPathItemKeys.Clear();
        _criticalPathIssues = Array.Empty<SystemCalculationIssue>();
    }

    private void ApplyCriticalRoute(CriticalPathResult route)
    {
        _criticalPathPressureLossPa = route.PressureLossPa;
        _criticalPathItemKeys = new HashSet<string>(route.ItemKeys, StringComparer.OrdinalIgnoreCase);
        _criticalPathIssues = route.Issues;
        _criticalPathRoute = route.StartId == route.EndId ? route.StartId : $"{route.StartId} → {route.EndId}";
    }

    /// <summary>
    /// The critical route through the drawn system. The analysis lives in DuctSizer.Core
    /// (<see cref="CriticalPathAnalyzer"/>) so it is covered by the engineering verification suite.
    /// </summary>
    private CriticalPathResult ComputeLongestRoute() => CriticalPathAnalyzer.Analyze(_ductSystem);

    /// <summary>
    /// Pressure loss charged at <paramref name="node"/> for a route that arrives through
    /// <paramref name="arrivedSecondary"/> and leaves toward <paramref name="nextChild"/>.
    /// Junction fittings use the ASHRAE path for that route. Returns NaN when the item cannot be calculated.
    /// </summary>
    private double NodePressureLoss(SavedLayoutNode node, bool arrivedSecondary = false, SavedLayoutNode? nextChild = null) =>
        SystemSolver.NodeLoss(_ductSystem, node, arrivedSecondary, nextChild);

    internal static string DuctKey(int sequence) => CriticalPathAnalyzer.DuctKey(sequence);
    internal static string FittingKey(int sequence) => CriticalPathAnalyzer.FittingKey(sequence);
    internal static string LayoutKey(int sequence) => CriticalPathAnalyzer.LayoutKey(sequence);

    /// <summary>One line naming the items that kept the critical path from being calculated.</summary>
    private string CriticalPathIssueSummary()
    {
        if (_criticalPathIssues.Count == 0)
            return string.Empty;
        var names = _criticalPathIssues.Select(x => x.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var listed = string.Join(", ", names.Take(6)) + (names.Length > 6 ? $" and {names.Length - 6} more" : string.Empty);
        return $"Incomplete: {listed} could not be calculated";
    }

    /// <summary>Layout geometry problems found at the last schedule refresh (see <see cref="LayoutGeometryCheck"/>).</summary>
    private IReadOnlyList<LayoutGeometryIssue> _layoutGeometryIssues = Array.Empty<LayoutGeometryIssue>();

    private string _lastLoggedLayoutReview = string.Empty;

    /// <summary>Layout model problems found at the last schedule refresh (see <see cref="LayoutModelCheck"/>).</summary>
    private IReadOnlyList<SystemCalculationIssue> _layoutModelIssues = Array.Empty<SystemCalculationIssue>();

    /// <summary>Model problems first (they can affect results), then geometry problems.</summary>
    private IReadOnlyList<string> LayoutReviewMessages() =>
        _layoutModelIssues.Select(issue => issue.Message)
            .Concat(_layoutGeometryIssues.Select(issue => issue.Message)).ToArray();

    /// <summary>"Layout: N items to review" preceded by <paramref name="separator"/>, or empty when the layout checks are clean.</summary>
    private string LayoutGeometryNote(string separator)
    {
        var count = _layoutModelIssues.Count + _layoutGeometryIssues.Count;
        return count == 0 ? string.Empty
            : $"{separator}Layout: {count} item{(count == 1 ? string.Empty : "s")} to review";
    }

    private bool ShowCriticalItem(string key) => !_criticalPathFilterEnabled || _criticalPathItemKeys.Contains(key);

    private void ApplyCriticalRowStyle(DataGridViewRow row, string key)
    {
        if (!_criticalPathCalculated || !_criticalPathItemKeys.Contains(key))
            return;
        row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 253);
        row.DefaultCellStyle.ForeColor = Color.FromArgb(11, 31, 59);
    }

    internal void RefreshDuctSystemGrid()
    {
        RefreshDuctSystemGridCore();
        FitAnalysisTableHeight();
        RefreshAnalysisSummary();
    }

    private void RefreshDuctSystemGridCore()
{
        if (_ductGrid.Columns.Count == 0)
            return;

        _host.LayoutScreen.UpdateCalculatedLayoutValues();
        UpdateSystemGridHeaders();
        var availableItemCount = _ductSystem.Ducts.Count + _ductSystem.Fittings.Count
                                 + _ductSystem.LayoutNodes.Count(x =>
                                     x.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
                                         or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                                         or LayoutNodeType.CustomComponent);
        if (availableItemCount > 0)
        {
            _criticalPathCalculated = true;
            ApplyCriticalRoute(ComputeLongestRoute());
        }
        else
        {
            ResetCriticalPath();
        }
        var selectedTag = _ductGrid.SelectedRows.Count > 0 ? _ductGrid.SelectedRows[0].Tag as SystemRowTag : null;
        _ductGrid.Rows.Clear();
        var totalUpperPa = 0.0;

        foreach (var duct in _ductSystem.Ducts.OrderBy(x => x.Sequence))
        {
            var criticalKey = DuctKey(duct.Sequence);
            var ductTopology = LayoutTopologyForDuct(duct.Sequence);
            try
            {
                var result = duct.Calculate();
                var ductPressure = SystemPressureForDuctPair(duct.Sequence);
                totalUpperPa += result.TotalPressureLossUpperPa;
                if (!ShowCriticalItem(criticalKey))
                    continue;
                var rowIndex = _ductGrid.Rows.Add(
                    duct.Id,
                    duct.Description,
                    FormatSystemStaticDrop(result),
                    FormatSystemPressureValue(ductPressure.Inlet),
                    FormatSystemPressureValue(ductPressure.Outlet),
                    duct.AirSystemType,
                    duct.BranchId,
                    ductTopology.Upstream,
                    ductTopology.Downstream,
                    FormatSystemDelta(duct.DeltaXM),
                    FormatSystemDelta(duct.DeltaYM),
                    FormatSystemDelta(duct.DeltaZM),
                    "—", "—", "—",
                    FormatSystemAirflow(duct.AirflowM3PerS),
                    duct.Shape.ToString(),
                    FormatSystemGeometry(result.Input.Geometry),
                    FormatSystemLength(duct.LengthM),
                    FormatSystemArea(result.Input.Geometry.ActualAreaM2),
                    FormatSystemVelocity(result.ActualVelocityMPerS),
                    FormatSystemPressureRate(result),
                    duct.MaterialName,
                    duct.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher ? "Huebscher" : "Hydraulic",
                    duct.PressureLossBasis == PressureLossBasis.EquivalentRoundArea ? "Equivalent round" : "Actual area");
                _ductGrid.Rows[rowIndex].Tag = new SystemRowTag(false, duct.Sequence);
                _ductGrid.Rows[rowIndex].Cells["Temperature"].Value = FormatSystemTemperature(duct.TemperatureC);
                _ductGrid.Rows[rowIndex].Cells["SurfaceArea"].Value = FormatSurfaceArea(SurfaceAreaCalculator.ForDuct(duct));
                ApplyCriticalRowStyle(_ductGrid.Rows[rowIndex], criticalKey);
                ApplyAirflowOverrideStyle(_ductGrid.Rows[rowIndex], duct.Sequence, isFitting: false);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                var ductPressure = SystemPressureForDuctPair(duct.Sequence);
                var rowIndex = _ductGrid.Rows.Add(duct.Id, duct.Description, "Invalid",
                    FormatSystemPressureValue(ductPressure.Inlet), FormatSystemPressureValue(ductPressure.Outlet),
                    duct.AirSystemType, duct.BranchId,
                    ductTopology.Upstream, ductTopology.Downstream,
                    FormatSystemDelta(duct.DeltaXM), FormatSystemDelta(duct.DeltaYM), FormatSystemDelta(duct.DeltaZM),
                    "—", "—", "—",
                    "—", duct.Shape, "—", "—", "—", "—", "—", ex.Message, "—", "—");
                _ductGrid.Rows[rowIndex].Tag = new SystemRowTag(false, duct.Sequence);
                _ductGrid.Rows[rowIndex].Cells["Temperature"].Value = FormatSystemTemperature(duct.TemperatureC);
                _ductGrid.Rows[rowIndex].Cells["SurfaceArea"].Value = FormatSurfaceArea(SurfaceAreaCalculator.ForDuct(duct));
                _ductGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Firebrick;
            }
        }

        foreach (var fitting in _ductSystem.Fittings.OrderBy(x => x.Sequence))
        {
            var criticalKey = FittingKey(fitting.Sequence);
            var fittingTopology = LayoutTopologyForFitting(fitting.Sequence);
            try
            {
                var result = fitting.Calculate();
                var fittingPressure = SystemPressureForFittingPair(fitting.Sequence);
                totalUpperPa += result.PressureLossPa;
                if (!ShowCriticalItem(criticalKey))
                    continue;
                var fittingGeometry = result.ReferenceGeometry;
                var rowIndex = _ductGrid.Rows.Add(
                    fitting.Id,
                    fitting.Description,
                    FormatSystemFittingDrop(result.PressureLossPa),
                    FormatSystemPressureValue(fittingPressure.Inlet),
                    FormatSystemPressureValue(fittingPressure.Outlet),
                    fitting.AirSystemType,
                    fitting.BranchId,
                    fittingTopology.Upstream,
                    fittingTopology.Downstream,
                    FormatSystemDelta(fitting.DeltaXM),
                    FormatSystemDelta(fitting.DeltaYM),
                    FormatSystemDelta(fitting.DeltaZM),
                    "—", "—", "—",
                    result.AirflowM3PerS is double fittingAirflow ? FormatSystemAirflow(fittingAirflow) : "—",
                    fittingGeometry is null ? "Fitting" : $"{fittingGeometry.Shape} fit",
                    fittingGeometry is null ? fitting.FittingCode : $"{fitting.FittingCode} / {FormatSystemGeometry(fittingGeometry)}",
                    "—",
                    "—",
                    FormatSystemVelocity(fitting.ReferenceVelocityMPerS),
                    "—",
                    result.Definition.Category,
                    result.Definition.IsAutomated ? "Built-in table" : "Entered C",
                    $"{result.Definition.ReferenceVelocity} VP");
                _ductGrid.Rows[rowIndex].Tag = new SystemRowTag(true, fitting.Sequence);
                _ductGrid.Rows[rowIndex].Cells["Temperature"].Value = FormatSystemTemperature(fitting.TemperatureC);
                _ductGrid.Rows[rowIndex].Cells["SurfaceArea"].Value = FormatSurfaceArea(SurfaceAreaCalculator.ForFitting(fitting));
                ApplyCriticalRowStyle(_ductGrid.Rows[rowIndex], criticalKey);
                ApplyAirflowOverrideStyle(_ductGrid.Rows[rowIndex], fitting.Sequence, isFitting: true);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            {
                var fittingPressure = SystemPressureForFittingPair(fitting.Sequence);
                var rowIndex = _ductGrid.Rows.Add(fitting.Id, fitting.Description, "Invalid",
                    FormatSystemPressureValue(fittingPressure.Inlet), FormatSystemPressureValue(fittingPressure.Outlet),
                    fitting.AirSystemType, fitting.BranchId,
                    fittingTopology.Upstream, fittingTopology.Downstream,
                    FormatSystemDelta(fitting.DeltaXM), FormatSystemDelta(fitting.DeltaYM), FormatSystemDelta(fitting.DeltaZM),
                    "—", "—", "—",
                    "—", "Fitting", fitting.FittingCode,
                    "—", "—", "—", "—", ex.Message, "—", "—");
                _ductGrid.Rows[rowIndex].Tag = new SystemRowTag(true, fitting.Sequence);
                _ductGrid.Rows[rowIndex].Cells["Temperature"].Value = FormatSystemTemperature(fitting.TemperatureC);
                _ductGrid.Rows[rowIndex].Cells["SurfaceArea"].Value = FormatSurfaceArea(SurfaceAreaCalculator.ForFitting(fitting));
                _ductGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Firebrick;
            }
        }

        foreach (var equipment in _ductSystem.LayoutNodes
                     .Where(x => x.IsDefined && (x.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan
                         or LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                         or LayoutNodeType.CustomComponent))
                     .OrderBy(x => x.Sequence))
        {
            var criticalKey = LayoutKey(equipment.Sequence);
            if (equipment.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent)
                totalUpperPa += equipment.PressureDropPa;
            if (!ShowCriticalItem(criticalKey))
                continue;
            var equipmentTopology = LayoutTopologyForNode(equipment);
            var equipmentPressure = SystemPressureForEquipmentPair(equipment);
            var rowIndex = _ductGrid.Rows.Add(
                equipment.Id,
                equipment.Type == LayoutNodeType.Reservoir ? equipment.SpaceRole
                    : equipment.Type == LayoutNodeType.CalculationPoint ? $"{equipment.CalculationPointType} Calculation Point"
                    : equipment.Type == LayoutNodeType.Fan ? equipment.FanEquipmentName : equipment.Type == LayoutNodeType.Coil ? "Coil"
                    : equipment.Type == LayoutNodeType.Filter ? "Filter"
                    : equipment.Type == LayoutNodeType.CustomComponent ? equipment.Description : equipment.GrdType,
                equipment.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent
                        ? FormatSystemFittingDrop(equipment.PressureDropPa) : "—",
                FormatSystemPressureValue(equipmentPressure.Inlet),
                FormatSystemPressureValue(equipmentPressure.Outlet),
                equipment.AirSystemType,
                equipment.BranchId,
                equipmentTopology.Upstream,
                equipmentTopology.Downstream,
                FormatSystemDelta(equipment.DeltaXM),
                FormatSystemDelta(equipment.DeltaYM),
                FormatSystemDelta(equipment.DeltaZM),
                "—", "—", "—",
                equipment.Type == LayoutNodeType.CalculationPoint
                    && !string.Equals(equipment.CalculationPointType, "Airflow", StringComparison.OrdinalIgnoreCase)
                        ? "—"
                        : equipment.AirflowM3PerS > 0 ? FormatSystemAirflow(equipment.AirflowM3PerS) : "—",
                equipment.Type == LayoutNodeType.CalculationPoint ? "Calculation point" : "Equipment",
                equipment.Type == LayoutNodeType.Grd ? equipment.GrdType
                    : equipment.Type == LayoutNodeType.CustomComponent ? equipment.Description
                    : equipment.Type == LayoutNodeType.Reservoir ? equipment.SpaceRole
                    : equipment.IsAirHandlingUnit ? "AHU" : equipment.Type.ToString(),
                "—", "—",
                equipment.Type == LayoutNodeType.CalculationPoint
                    && string.Equals(equipment.CalculationPointType, "Velocity", StringComparison.OrdinalIgnoreCase)
                        ? FormatSystemVelocity(equipment.VelocityMPerS) : "—",
                "—",
                equipment.Type == LayoutNodeType.Coil
                    ? $"Leaving {UnitConversions.CelsiusToFahrenheit(equipment.TemperatureC):0.#}°F"
                    : equipment.Type == LayoutNodeType.Reservoir
                        ? $"Space {UnitConversions.CelsiusToFahrenheit(equipment.TemperatureC):0.#}°F"
                    : equipment.Type == LayoutNodeType.Fan
                        ? $"Air {UnitConversions.CelsiusToFahrenheit(equipment.TemperatureC):0.#}°F; internal static {FormatSystemTotal(equipment.FanInternalStaticPressurePa)}"
                        : equipment.Type == LayoutNodeType.Grd ? "User-entered GRD loss"
                        : equipment.Type == LayoutNodeType.CustomComponent ? "User-entered custom component loss" : "Filter loss",
                "—", "—");
            _ductGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(247, 251, 248);
            _ductGrid.Rows[rowIndex].Tag = equipment;
            _ductGrid.Rows[rowIndex].Cells["SurfaceArea"].Value = "—";
            _ductGrid.Rows[rowIndex].Cells["Temperature"].Value = equipment.Type == LayoutNodeType.CalculationPoint
                && !string.Equals(equipment.CalculationPointType, "Temperature", StringComparison.OrdinalIgnoreCase)
                    ? "—" : FormatSystemTemperature(equipment.TemperatureC);
            ApplyCriticalRowStyle(_ductGrid.Rows[rowIndex], criticalKey);
            if (equipment.AirflowOverride)
                ApplyOrangeOverrideStyle(_ductGrid.Rows[rowIndex]);
        }

        var itemCount = _ductSystem.Ducts.Count + _ductSystem.Fittings.Count
                        + _ductSystem.LayoutNodes.Count(x => x.IsDefined && (x.Type is LayoutNodeType.Reservoir
                            or LayoutNodeType.Fan or LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                            or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent));
        ApplyGraphicalSystemRowOrder();
        var absoluteCoordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        foreach (DataGridViewRow row in _ductGrid.Rows)
        {
            var node = _host.LayoutScreen.LayoutNodeForSystemTag(row.Tag);
            if (node is null || !absoluteCoordinates.TryGetValue(node.Sequence, out var coordinate))
            {
                row.Cells["AbsoluteX"].Value = "—";
                row.Cells["AbsoluteY"].Value = "—";
                row.Cells["AbsoluteZ"].Value = "—";
                continue;
            }
            row.Cells["AbsoluteX"].Value = FormatSystemDelta(coordinate.XM);
            row.Cells["AbsoluteY"].Value = FormatSystemDelta(coordinate.YM);
            row.Cells["AbsoluteZ"].Value = FormatSystemDelta(coordinate.ZM);
        }
        _systemTotalLabel.Text = itemCount == 0
            ? "Critical-path net pressure loss: —"
            : _criticalPathIssues.Count > 0
                ? $"Critical-path net pressure loss: not available ({CriticalPathIssueSummary()})"
                : $"Critical-path net pressure loss: {FormatSystemTotal(_criticalPathPressureLossPa)}";
        _systemTotalLabel.ForeColor = _criticalPathIssues.Count > 0 ? Color.Firebrick : Color.FromArgb(11, 31, 59);
        _systemCountLabel.Text = itemCount == 1 ? "1 item" : $"{itemCount} items";
        _layoutGeometryIssues = LayoutGeometryCheck.Check(_ductSystem);
        _layoutModelIssues = LayoutModelCheck.Check(_ductSystem);
        _systemCriticalLabel.Text = (!_criticalPathCalculated
            ? "Critical route: not available"
            : $"Route: {_criticalPathRoute}") + LayoutGeometryNote(" · ");
        var layoutReview = LayoutReviewMessages();
        _systemCriticalLabel.ForeColor = layoutReview.Count > 0
            ? Color.FromArgb(150, 85, 0) : Color.FromArgb(14, 111, 230);
        _host.LayoutScreen._layoutToolTips.SetToolTip(_systemCriticalLabel, layoutReview.Count == 0
            ? string.Empty
            : "Layout items to review:\r\n" + string.Join("\r\n", layoutReview.Take(12).Select(message => "• " + message))
              + (layoutReview.Count > 12 ? $"\r\n• and {layoutReview.Count - 12} more" : string.Empty));
        var reviewSignature = string.Join("|", layoutReview);
        if (layoutReview.Count > 0 && !string.Equals(reviewSignature, _lastLoggedLayoutReview, StringComparison.Ordinal))
            DiagnosticLog.Info($"Layout review: {layoutReview.Count} item(s). " + string.Join(" | ", layoutReview.Take(20)));
        _lastLoggedLayoutReview = reviewSignature;
        _criticalPathFilterButton.Enabled = _criticalPathCalculated;
        _criticalPathFilterButton.Text = _criticalPathFilterEnabled ? "Show All Items" : "Isolate Critical Path";
        _systemTab.Text = "Duct System";

        if (selectedTag is not null)
        {
            foreach (DataGridViewRow row in _ductGrid.Rows)
            {
                if (row.Tag is SystemRowTag tag && tag == selectedTag)
                {
                    row.Selected = true;
                    _ductGrid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }
        _host.LayoutScreen.RefreshLayoutSystemSummary();
        UpdateSystemActionState();
    }

    private (string Upstream, string Downstream) LayoutTopologyForDuct(int ductSequence)
    {
        var connectionNode = _ductSystem.LayoutNodes.FirstOrDefault(x => x.ConnectionDuctSequence == ductSequence);
        if (connectionNode is not null)
        {
            var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == connectionNode.ParentNodeId);
            var topology = (Upstream: parent is null ? "—" : SystemItemIdForNode(parent),
                Downstream: SystemItemIdForNode(connectionNode));
            return connectionNode.ConnectionFlowReversed
                ? (topology.Downstream, topology.Upstream)
                : topology;
        }
        connectionNode = _ductSystem.LayoutNodes.FirstOrDefault(x => x.SecondaryConnectionDuctSequence == ductSequence);
        if (connectionNode is not null)
        {
            var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == connectionNode.SecondaryParentNodeId);
            var topology = (Upstream: parent is null ? "—" : SystemItemIdForNode(parent),
                Downstream: SystemItemIdForNode(connectionNode));
            return connectionNode.SecondaryConnectionFlowReversed
                ? (topology.Downstream, topology.Upstream)
                : topology;
        }
        var legacyNode = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedDuctSequence == ductSequence);
        return legacyNode is null ? ("—", "—") : LayoutTopologyForNode(legacyNode);
    }

    private void ApplyGraphicalSystemRowOrder()
    {
        if (!_ductGrid.Columns.Contains("GraphOrder"))
            return;
        var orderedKeys = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) && visited.Add(key))
                orderedKeys.Add(key);
        }

        void Walk(SavedLayoutNode node, HashSet<string> visitedNodes)
        {
            if (!visitedNodes.Add(node.Id))
                return;
            if (node.LinkedFittingSequence is int fittingSequence)
                Add(FittingKey(fittingSequence));
            else if (node.LinkedDuctSequence is int linkedDuctSequence)
                Add(DuctKey(linkedDuctSequence));
            else if (node.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
                     or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                     or LayoutNodeType.CustomComponent)
                Add(LayoutKey(node.Sequence));

            foreach (var child in _ductSystem.LayoutNodes
                         .Where(x => x.ParentNodeId == node.Id || x.SecondaryParentNodeId == node.Id)
                         .OrderBy(x => string.Equals(x.BranchId, node.BranchId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(x => x.Sequence))
            {
                var secondary = string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
                var connectionSequence = secondary ? child.SecondaryConnectionDuctSequence : child.ConnectionDuctSequence;
                if (connectionSequence is int ductSequence)
                    Add(DuctKey(ductSequence));
                foreach (var point in CalculationPointsForConnection(child, secondary))
                    Add(LayoutKey(point.Sequence));
                Walk(child, visitedNodes);
            }
        }

        foreach (var root in _ductSystem.LayoutNodes
                     .Where(x => x.Type != LayoutNodeType.CalculationPoint
                                 && string.IsNullOrWhiteSpace(x.ParentNodeId)
                                 && string.IsNullOrWhiteSpace(x.SecondaryParentNodeId))
                     .OrderBy(x => x.Sequence))
            Walk(root, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        foreach (var duct in _ductSystem.Ducts.OrderBy(x => x.Sequence)) Add(DuctKey(duct.Sequence));
        foreach (var fitting in _ductSystem.Fittings.OrderBy(x => x.Sequence)) Add(FittingKey(fitting.Sequence));
        foreach (var node in _ductSystem.LayoutNodes.OrderBy(x => x.Sequence)) Add(LayoutKey(node.Sequence));

        var rank = orderedKeys.Select((key, index) => (key, index))
            .ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _ductGrid.Rows)
        {
            var key = SystemRowKey(row);
            row.Cells["GraphOrder"].Value = key is not null && rank.TryGetValue(key, out var order)
                ? order : int.MaxValue;
        }
        _ductGrid.Sort(_ductGrid.Columns["GraphOrder"], System.ComponentModel.ListSortDirection.Ascending);
    }

    private static string? SystemRowKey(DataGridViewRow row) => row.Tag switch
    {
        SystemRowTag { IsFitting: true } tag => FittingKey(tag.Sequence),
        SystemRowTag tag => DuctKey(tag.Sequence),
        SavedLayoutNode node => LayoutKey(node.Sequence),
        _ => null
    };

    private void ApplyAirflowOverrideStyle(DataGridViewRow row, int sequence, bool isFitting)
    {
        var node = isFitting
            ? _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == sequence)
            : _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedDuctSequence == sequence
                                                           || x.ConnectionDuctSequence == sequence
                                                           || x.SecondaryConnectionDuctSequence == sequence);
        var overridden = node?.AirflowOverride == true;
        if (!isFitting && node is not null)
        {
            overridden |= node.ConnectionDuctSequence == sequence && node.ConnectionAirflowOverride;
            overridden |= node.SecondaryConnectionDuctSequence == sequence && node.SecondaryConnectionAirflowOverride;
        }
        if (overridden)
            ApplyOrangeOverrideStyle(row);
    }

    private static void ApplyOrangeOverrideStyle(DataGridViewRow row)
    {
        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 237, 213);
        row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 74, 0);
    }

    private (string Upstream, string Downstream) LayoutTopologyForFitting(int fittingSequence)
    {
        var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fittingSequence);
        return node is null ? ("—", "—") : LayoutTopologyForNode(node);
    }

    private (string Upstream, string Downstream) LayoutTopologyForNode(SavedLayoutNode node)
    {
        if (node.Type == LayoutNodeType.CalculationPoint && node.ObservedDuctSequence is int observedDuct)
            return LayoutTopologyForDuct(observedDuct);
        var parents = new[] { node.ParentNodeId, node.SecondaryParentNodeId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(id => _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == id))
            .Where(x => x is not null)
            .Select(x => SystemItemIdForNode(x!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var children = _ductSystem.LayoutNodes.Where(x => x.ParentNodeId == node.Id || x.SecondaryParentNodeId == node.Id)
            .OrderBy(x => x.Sequence)
            .Select(SystemItemIdForNode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return (parents.Length == 0 ? "—" : string.Join(", ", parents), children.Length == 0 ? "—" : string.Join(", ", children));
    }

    private IEnumerable<SavedLayoutNode> CalculationPointsForConnection(SavedLayoutNode downstream, bool secondary)
    {
        var ductSequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        return _ductSystem.LayoutNodes
            .Where(x => x.Type == LayoutNodeType.CalculationPoint
                        && ((string.Equals(x.CalculationConnectionNodeId, downstream.Id, StringComparison.OrdinalIgnoreCase)
                             && x.CalculationConnectionSecondary == secondary)
                            || ductSequence is int sequence && x.ObservedDuctSequence == sequence))
            .OrderBy(x => x.CalculationConnectionFraction)
            .ThenBy(x => x.Sequence);
    }

    private string SystemItemIdForNode(SavedLayoutNode node)
    {
        if (node.LinkedFittingSequence is int fittingSequence)
            return _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence)?.Id ?? node.Id;
        if (node.LinkedDuctSequence is int ductSequence)
            return _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductSequence)?.Id ?? node.Id;
        return node.Id;
    }

    private string FormatSystemAirflow(double cubicMetersPerSecond) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.M3PerSToCfm(cubicMetersPerSecond):N0}"
        : $"{UnitConversions.M3PerSToLps(cubicMetersPerSecond):N0}";

    private string FormatSystemGeometry(DuctGeometry geometry) => _host._currentUnits == UnitSystem.Imperial
        ? geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToInches(geometry.DiameterM):0.##} in round"
            : $"{UnitConversions.MToInches(geometry.WidthM):0.##} × {UnitConversions.MToInches(geometry.HeightM):0.##} in"
        : geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToMillimeters(geometry.DiameterM):0.#} mm round"
            : $"{UnitConversions.MToMillimeters(geometry.WidthM):0.#} × {UnitConversions.MToMillimeters(geometry.HeightM):0.#} mm";

    private string FormatSystemLength(double meters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.#}"
        : $"{meters:0.##}";

    private string FormatSystemDelta(double meters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.##}"
        : $"{meters:0.##}";

    private string FormatSystemArea(double squareMeters) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.SquareMetersToSquareFeet(squareMeters):0.###}"
        : $"{squareMeters:0.####}";

    /// <summary>Surface area for the tables: "—" for equipment (no ductwork area); fittings, estimated, carry "≈".</summary>
    internal string FormatSurfaceArea(ItemSurfaceArea? area)
    {
        if (area is null)
            return "—";
        if (area.Problem is not null)
            return "Not available";
        var text = _host._currentUnits == UnitSystem.Imperial
            ? $"{area.AreaFt2:0.##}"
            : $"{area.AreaM2:0.###}";
        return area.IsEstimate ? "≈ " + text : text;
    }

    private string FormatSystemVelocity(double metersPerSecond) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MpsToFpm(metersPerSecond):N0}"
        : $"{metersPerSecond:0.##}";

    private string FormatSystemTemperature(double celsius) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.CelsiusToFahrenheit(celsius):0.#}"
        : $"{celsius:0.#}";

    internal string FormatSystemPressureRate(DuctCalculationResult result)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossLowerPaPerM):0.###}–{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM):0.###}"
                : $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM):0.###}";
        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.PressureLossLowerPaPerM:0.###}–{result.PressureLossUpperPaPerM:0.###}"
            : $"{result.PressureLossPaPerM:0.###}";
    }

    private string FormatSystemStaticDrop(DuctCalculationResult result)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaToInchesWater(result.TotalPressureLossLowerPa):0.###}–{UnitConversions.PaToInchesWater(result.TotalPressureLossUpperPa):0.###}"
                : $"{UnitConversions.PaToInchesWater(result.TotalPressureLossPa):0.###}";
        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.TotalPressureLossLowerPa:0.##}–{result.TotalPressureLossUpperPa:0.##}"
            : $"{result.TotalPressureLossPa:0.##}";
    }

    private string FormatSystemFittingDrop(double pascals)
    {
        if (_host._currentUnits == UnitSystem.Imperial)
        {
            var inchesWater = NormalizeRoundedZero(UnitConversions.PaToInchesWater(pascals), 3);
            return $"{inchesWater:0.###}";
        }

        return $"{NormalizeRoundedZero(pascals, 2):0.##}";
    }

    private string FormatSystemPressureValue(double pascals) => double.IsFinite(pascals)
        ? FormatSystemFittingDrop(pascals)
        : "—";

    private static double NormalizeRoundedZero(double value, int decimalPlaces)
    {
        var halfDisplayIncrement = 0.5 * Math.Pow(10.0, -decimalPlaces);
        return Math.Abs(value) < halfDisplayIncrement ? 0.0 : value;
    }

    /// <summary>The Designer's Pressure label for a duct: static pressure at its inlet, e.g. "SP 0.85 in. w.c.".</summary>
    internal string DuctInletPressureCaption(int ductSequence)
    {
        var inlet = SystemPressureForDuctPair(ductSequence).Inlet;
        if (!double.IsFinite(inlet))
            return string.Empty;
        return _host._currentUnits == UnitSystem.Imperial
            ? $"SP {UnitConversions.PaToInchesWater(inlet).ToString("0.###", CultureInfo.CurrentCulture)} in. w.c."
            : $"SP {inlet.ToString("0", CultureInfo.CurrentCulture)} Pa";
    }

    private (double Inlet, double Outlet) SystemPressureForDuctPair(int ductSequence)
    {
        var downstream = _ductSystem.LayoutNodes.FirstOrDefault(x => x.ConnectionDuctSequence == ductSequence);
        var secondary = false;
        if (downstream is null)
        {
            downstream = _ductSystem.LayoutNodes.FirstOrDefault(x => x.SecondaryConnectionDuctSequence == ductSequence);
            secondary = downstream is not null;
        }
        if (downstream is null)
            return (double.NaN, double.NaN);
        var parentId = secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
        if (parent is null)
            return (double.NaN, double.NaN);
        var loss = _host.LayoutScreen.SafeDuctPressureLoss(ductSequence);
        var inlet = _host.LayoutScreen.OutletPressureToward(parent, downstream);
        return (inlet, inlet - loss);
    }

    private (double Inlet, double Outlet) SystemPressureForFittingPair(int fittingSequence)
    {
        var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fittingSequence);
        return node is null ? (double.NaN, double.NaN) : (SystemInletPressureForNode(node), node.PressurePa);
    }

    private (double Inlet, double Outlet) SystemPressureForEquipmentPair(SavedLayoutNode node) =>
        (SystemInletPressureForNode(node), node.PressurePa);

    private double SystemInletPressureForNode(SavedLayoutNode node)
    {
        var incomingPressures = new List<double>(2);
        AddIncoming(node.ParentNodeId, node.ConnectionDuctSequence);
        AddIncoming(node.SecondaryParentNodeId, node.SecondaryConnectionDuctSequence);
        if (incomingPressures.Count > 0)
            return incomingPressures.Min();
        if (node.Type == LayoutNodeType.Fan)
            return node.PressurePa - Math.Max(0.0, node.FanStaticPressurePa);
        return node.PressurePa;

        void AddIncoming(string parentId, int? ductSequence)
        {
            if (string.IsNullOrWhiteSpace(parentId))
                return;
            var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
                return;
            // An unsized connection or a duct that cannot be calculated: the inlet pressure is not available (NaN).
            var ductLoss = ductSequence is int sequence ? _host.LayoutScreen.SafeDuctPressureLoss(sequence) : double.NaN;
            incomingPressures.Add(_host.LayoutScreen.OutletPressureToward(parent, node) - ductLoss);
        }
    }

    private void UpdateSystemGridHeaders()
    {
        if (_ductGrid.Columns.Count == 0)
            return;
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        _ductGrid.Columns["StaticDrop"].HeaderText = imperial ? "Static drop\n(in. w.c.)" : "Static drop\n(Pa)";
        _ductGrid.Columns["PressureIn"].HeaderText = imperial ? "Static pressure in\n(in. w.c.)" : "Static pressure in\n(Pa)";
        _ductGrid.Columns["PressureOut"].HeaderText = imperial ? "Static pressure out\n(in. w.c.)" : "Static pressure out\n(Pa)";
        _ductGrid.Columns["Airflow"].HeaderText = imperial ? "Airflow\n(cfm)" : "Airflow\n(L/s)";
        _ductGrid.Columns["DeltaX"].HeaderText = imperial ? "ΔX\n(ft)" : "ΔX\n(m)";
        _ductGrid.Columns["DeltaY"].HeaderText = imperial ? "ΔY\n(ft)" : "ΔY\n(m)";
        _ductGrid.Columns["DeltaZ"].HeaderText = imperial ? "ΔZ\n(ft)" : "ΔZ\n(m)";
        _ductGrid.Columns["AbsoluteX"].HeaderText = imperial ? "Absolute X\n(ft)" : "Absolute X\n(m)";
        _ductGrid.Columns["AbsoluteY"].HeaderText = imperial ? "Absolute Y\n(ft)" : "Absolute Y\n(m)";
        _ductGrid.Columns["AbsoluteZ"].HeaderText = imperial ? "Absolute Z\n(ft)" : "Absolute Z\n(m)";
        _ductGrid.Columns["Length"].HeaderText = imperial ? "Length\n(ft)" : "Length\n(m)";
        _ductGrid.Columns["Area"].HeaderText = imperial ? "Area\n(ft²)" : "Area\n(m²)";
        _ductGrid.Columns["SurfaceArea"].HeaderText = imperial ? "Surface area\n(ft²)" : "Surface area\n(m²)";
        _ductGrid.Columns["Velocity"].HeaderText = imperial ? "Velocity\n(fpm)" : "Velocity\n(m/s)";
        _ductGrid.Columns["Temperature"].HeaderText = imperial ? "Temperature\n(°F)" : "Temperature\n(°C)";
        _ductGrid.Columns["PressureRate"].HeaderText = imperial ? "Pressure rate\n(in. w.c./100 ft)" : "Pressure rate\n(Pa/m)";
    }

    private string FormatSystemTotal(double pascals) => _host._currentUnits == UnitSystem.Imperial
        ? $"{NormalizeRoundedZero(UnitConversions.PaToInchesWater(pascals), 3):0.###} in. w.c."
        : $"{NormalizeRoundedZero(pascals, 2):0.##} Pa";

    private SavedDuct? SelectedDuct()
    {
        if (_ductGrid.SelectedRows.Count == 0 || _ductGrid.SelectedRows[0].Tag is not SystemRowTag { IsFitting: false } tag)
            return null;
        return _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == tag.Sequence);
    }

    private SavedFitting? SelectedFitting()
    {
        if (_ductGrid.SelectedRows.Count == 0 || _ductGrid.SelectedRows[0].Tag is not SystemRowTag { IsFitting: true } tag)
            return null;
        return _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
    }

    private void UpdateSystemActionState()
    {
        var hasSelection = SelectedDuct() is not null || SelectedFitting() is not null;
        _editSystemItemMenuItem.Enabled = hasSelection;
        _removeSystemItemMenuItem.Enabled = hasSelection;
    }

    private void SaveEditedSystemCell(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _ductGrid.Rows.Count || columnIndex < 0)
            return;
        var columnName = _ductGrid.Columns[columnIndex].Name;
        if (columnName == "Description")
        {
            SaveEditedDescription(rowIndex);
            return;
        }
        if (columnName is "AirSystem" or "BranchId")
        {
            SaveEditedSystemMetadata(rowIndex, columnName);
            return;
        }
        if (columnName == "Airflow")
        {
            SaveEditedSystemAirflow(rowIndex);
            return;
        }
        if (columnName is "DeltaX" or "DeltaY" or "DeltaZ")
        {
            SaveEditedSystemDelta(rowIndex, columnName);
            return;
        }
        if (columnName != "Id" || _ductGrid.Rows[rowIndex].Tag is not SystemRowTag { IsFitting: true } tag)
            return;

        var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
        if (fitting is null)
            return;
        var editedId = Convert.ToString(_ductGrid.Rows[rowIndex].Cells["Id"].Value, CultureInfo.CurrentCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(editedId))
        {
            _ductGrid.Rows[rowIndex].Cells["Id"].Value = fitting.Id;
            return;
        }
        fitting.CustomId = editedId;
        _ductGrid.Rows[rowIndex].Cells["Id"].Value = fitting.Id;
        SaveDuctSystem();
        _systemStorageStatusLabel.Text = $"Saved fitting ID '{fitting.Id}'. Double-click the ID again to change it.";
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private void SaveEditedSystemAirflow(int rowIndex)
    {
        var row = _ductGrid.Rows[rowIndex];
        var text = Convert.ToString(row.Cells["Airflow"].Value, CultureInfo.CurrentCulture)?.Trim();
        if ((!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var displayed)
             && !double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out displayed))
            || !double.IsFinite(displayed) || displayed <= 0)
        {
            RefreshDuctSystemGrid();
            _systemStorageStatusLabel.Text = "Airflow must be a positive number.";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            return;
        }
        var airflow = _host._currentUnits == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(displayed)
            : UnitConversions.LpsToM3PerS(displayed);
        if (!ConfirmAirflowOverride(row, airflow))
        {
            RefreshDuctSystemGrid();
            return;
        }
        if (row.Tag is SavedLayoutNode equipment)
        {
            equipment.AirflowM3PerS = airflow;
        }
        else if (row.Tag is SystemRowTag tag && tag.IsFitting)
        {
            var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
            if (fitting is null) return;
            var oldFlow = fitting.AirflowM3PerS;
            fitting.AirflowM3PerS = airflow;
            if (fitting.Junction is not null)
            {
                var scale = oldFlow > 0 ? airflow / oldFlow : 1.0;
                fitting.Junction = fitting.Junction with
                {
                    CommonAirflowM3PerS = airflow,
                    StraightAirflowM3PerS = fitting.Junction.StraightAirflowM3PerS * scale,
                    BranchAirflowM3PerS = fitting.Junction.BranchAirflowM3PerS * scale
                };
            }
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fitting.Sequence);
            if (node is not null) node.AirflowM3PerS = airflow;
        }
        else if (row.Tag is SystemRowTag ductTag)
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductTag.Sequence);
            if (duct is null) return;
            duct.AirflowM3PerS = airflow;
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedDuctSequence == duct.Sequence
                                                                    || x.ConnectionDuctSequence == duct.Sequence
                                                                    || x.SecondaryConnectionDuctSequence == duct.Sequence);
            if (node?.LinkedDuctSequence == duct.Sequence)
            {
                node.AirflowM3PerS = airflow;
                node.AirflowUserSpecified = true;
            }
        }
        _host.LayoutScreen.UpdateCalculatedLayoutValues();
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
    }

    internal bool ConfirmAirflowOverride(DataGridViewRow row, double proposedAirflow)
    {
        if (row.Tag is SystemRowTag { IsFitting: false } connectionDuctTag)
        {
            var connectionNode = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                x.ConnectionDuctSequence == connectionDuctTag.Sequence || x.SecondaryConnectionDuctSequence == connectionDuctTag.Sequence);
            if (connectionNode is not null)
            {
                var secondary = connectionNode.SecondaryConnectionDuctSequence == connectionDuctTag.Sequence;
                return ConfirmConnectionAirflowOverride(new LayoutConnectionRef(connectionNode, secondary),
                    proposedAirflow, Convert.ToString(row.Cells["Id"].Value, CultureInfo.CurrentCulture) ?? "this duct");
            }
        }
        var node = row.Tag switch
        {
            SavedLayoutNode savedNode => savedNode,
            SystemRowTag { IsFitting: true } fittingTag => _ductSystem.LayoutNodes
                .FirstOrDefault(x => x.LinkedFittingSequence == fittingTag.Sequence),
            SystemRowTag ductTag => _ductSystem.LayoutNodes.FirstOrDefault(x =>
                x.LinkedDuctSequence == ductTag.Sequence || x.ConnectionDuctSequence == ductTag.Sequence
                                                        || x.SecondaryConnectionDuctSequence == ductTag.Sequence),
            _ => null
        };
        return ConfirmAirflowOverride(node, proposedAirflow,
            Convert.ToString(row.Cells["Id"].Value, CultureInfo.CurrentCulture) ?? "this item");
    }

    internal bool ConfirmAirflowOverride(SavedLayoutNode? node, double proposedAirflow, string itemId)
    {
        if (node is null)
            return true;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == node.ParentNodeId);
        if (LayoutDesignerScreen.IsConvergingLayoutNode(node))
        {
            node.AirflowOverride = false;
            node.AirflowUserSpecified = false;
            return true;
        }
        if (LayoutDesignerScreen.IsDivergingLayoutNode(node) && parent is not null)
        {
            var incomingDuct = _host.LayoutScreen.DuctForConnection(node, secondary: false);
            var expected = incomingDuct?.AirflowM3PerS > 0 ? incomingDuct.AirflowM3PerS : parent.AirflowM3PerS;
            var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), expected * .01);
            if (Math.Abs(proposedAirflow - expected) <= tolerance)
            {
                node.AirflowOverride = false;
                node.AirflowUserSpecified = false;
                return true;
            }
            var expectedText = _host._currentUnits == UnitSystem.Imperial
                ? $"{UnitConversions.M3PerSToCfm(expected):N0} cfm"
                : $"{UnitConversions.M3PerSToLps(expected):N0} L/s";
            var splitResponse = MessageBox.Show(_host,
                $"{itemId} common airflow does not match its calculated inlet airflow of {expectedText}.\r\n\r\nAllow this value anyway and mark the fitting orange?",
                "Airflow Balance Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (splitResponse != DialogResult.Yes)
                return false;
            node.AirflowOverride = true;
            node.AirflowUserSpecified = true;
            return true;
        }
        var allowsFlowChange = AllowsAirflowChange(node) || parent is not null && AllowsAirflowChange(parent);
        var referenceFlows = new List<double>();
        if (parent?.AirflowM3PerS > 0)
            referenceFlows.Add(parent.AirflowM3PerS);
        referenceFlows.AddRange(_ductSystem.LayoutNodes
            .Where(x => x.ParentNodeId == node.Id || x.SecondaryParentNodeId == node.Id)
            .Select(x => x.AirflowM3PerS)
            .Where(x => x > 0));
        var mismatch = !allowsFlowChange && referenceFlows.Any(flow =>
            Math.Abs(flow - proposedAirflow) > Math.Max(UnitConversions.CfmToM3PerS(1), flow * .01));
        if (!mismatch)
        {
            node.AirflowOverride = false;
            return true;
        }
        var response = MessageBox.Show(_host,
            $"{itemId} has an airflow that differs from the connected system without a fan, Space Source, converging fitting, or diverting fitting between the values.\r\n\r\nAllow the override and mark the affected item orange?",
            "Airflow Continuity Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return false;
        node.AirflowOverride = true;
        node.AirflowUserSpecified = true;
        return true;
    }

    internal bool ConfirmConnectionAirflowOverride(LayoutConnectionRef connection, double proposedAirflow,
        string itemId)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var upstream = _ductSystem.LayoutNodes.FirstOrDefault(x => string.Equals(x.Id, parentId,
            StringComparison.OrdinalIgnoreCase));
        var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), Math.Max(proposedAirflow, upstream?.AirflowM3PerS ?? 0) * .01);
        var mismatch = false;
        var expected = upstream?.AirflowM3PerS ?? proposedAirflow;

        if (upstream is not null && LayoutDesignerScreen.IsDivergingLayoutNode(upstream))
        {
            var siblings = _host.LayoutScreen.LayoutConnectionsFrom(upstream).Where(x =>
                !(ReferenceEquals(x.Downstream, downstream) && x.Secondary == connection.Secondary)).ToArray();
            var fixedSiblingTotal = siblings
                .Where(x => LayoutDesignerScreen.ConnectionAirflowUserSpecified(x.Downstream, x.Secondary))
                .Sum(x => _host.LayoutScreen.DuctForConnection(x.Downstream, x.Secondary)?.AirflowM3PerS
                          ?? Math.Max(0.0, x.Downstream.AirflowM3PerS));
            var unlockedSiblingCount = siblings.Count(x => !LayoutDesignerScreen.ConnectionAirflowUserSpecified(x.Downstream, x.Secondary));
            unlockedSiblingCount += Math.Max(0,
                FittingCatalog.OutletPortCount(upstream.FittingGroup) - siblings.Length - 1);
            var totalIfAccepted = proposedAirflow + fixedSiblingTotal;
            mismatch = totalIfAccepted > upstream.AirflowM3PerS + tolerance
                       || unlockedSiblingCount == 0 && Math.Abs(totalIfAccepted - upstream.AirflowM3PerS) > tolerance;
            expected = Math.Max(0.0, upstream.AirflowM3PerS - fixedSiblingTotal);
        }
        else if (upstream is not null)
        {
            mismatch = Math.Abs(proposedAirflow - expected) > tolerance;
        }

        if (!mismatch)
        {
            LayoutDesignerScreen.SetConnectionAirflowUserSpecified(downstream, connection.Secondary,
                upstream is not null && LayoutDesignerScreen.IsDivergingLayoutNode(upstream)
                || Math.Abs(proposedAirflow - expected) > tolerance);
            LayoutDesignerScreen.SetConnectionAirflowOverride(downstream, connection.Secondary, false);
            return true;
        }

        var expectedText = _host._currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.M3PerSToCfm(expected):N0} cfm"
            : $"{UnitConversions.M3PerSToLps(expected):N0} L/s";
        var response = MessageBox.Show(_host,
            $"{itemId} does not match the automatically balanced airflow of {expectedText}.\r\n\r\nAllow this value anyway and mark the duct orange?",
            "Airflow Balance Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return false;
        LayoutDesignerScreen.SetConnectionAirflowUserSpecified(downstream, connection.Secondary, true);
        LayoutDesignerScreen.SetConnectionAirflowOverride(downstream, connection.Secondary, true);
        return true;
    }

    private static bool AllowsAirflowChange(SavedLayoutNode node) =>
        node.Type == LayoutNodeType.Fan
        || node.Type == LayoutNodeType.Reservoir && LayoutDesignerScreen.IsSpaceSourceRole(node.SpaceRole)
        || node.Type == LayoutNodeType.Fitting
        && (string.Equals(node.FittingGroup, "Converging Fittings", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.FittingGroup, "Diverting Fittings", StringComparison.OrdinalIgnoreCase));

    private void SaveEditedSystemDelta(int rowIndex, string columnName)
    {
        var row = _ductGrid.Rows[rowIndex];
        var text = Convert.ToString(row.Cells[columnName].Value, CultureInfo.CurrentCulture)?.Trim();
        if ((!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var displayed)
             && !double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out displayed))
            || !double.IsFinite(displayed) || Math.Abs(displayed) > 100_000)
        {
            RefreshDuctSystemGrid();
            _systemStorageStatusLabel.Text = "ΔX, ΔY, and ΔZ must be finite values between -100,000 and 100,000.";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            return;
        }
        var meters = _host._currentUnits == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
        void Assign(Action<double> setX, Action<double> setY, Action<double> setZ)
        {
            if (columnName == "DeltaX") setX(meters);
            else if (columnName == "DeltaY") setY(meters);
            else setZ(meters);
        }

        if (row.Tag is SavedLayoutNode equipment)
        {
            Assign(value => equipment.DeltaXM = value, value => equipment.DeltaYM = value, value => equipment.DeltaZM = value);
            if (!string.IsNullOrWhiteSpace(equipment.ParentNodeId))
                equipment.ConnectionDeltaConfirmed = true;
            _host.LayoutScreen.SyncConnectionDuctGeometry(equipment);
            _host.LayoutScreen.SyncLinkedItemMetadata(equipment);
        }
        else if (row.Tag is SystemRowTag { IsFitting: true } fittingTag)
        {
            var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == fittingTag.Sequence);
            if (fitting is null) return;
            Assign(value => fitting.DeltaXM = value, value => fitting.DeltaYM = value, value => fitting.DeltaZM = value);
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fitting.Sequence);
            if (node is not null)
            {
                Assign(value => node.DeltaXM = value, value => node.DeltaYM = value, value => node.DeltaZM = value);
                if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
                    node.ConnectionDeltaConfirmed = true;
                _host.LayoutScreen.SyncConnectionDuctGeometry(node);
            }
        }
        else if (row.Tag is SystemRowTag ductTag)
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductTag.Sequence);
            if (duct is null) return;
            Assign(value => duct.DeltaXM = value, value => duct.DeltaYM = value, value => duct.DeltaZM = value);
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedDuctSequence == duct.Sequence
                                                                    || x.ConnectionDuctSequence == duct.Sequence
                                                                    || x.SecondaryConnectionDuctSequence == duct.Sequence);
            if (node is not null)
            {
                Assign(value => node.DeltaXM = value, value => node.DeltaYM = value, value => node.DeltaZM = value);
                if (node.SecondaryConnectionDuctSequence == duct.Sequence
                    && !string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
                    node.SecondaryConnectionDeltaConfirmed = true;
                else if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
                    node.ConnectionDeltaConfirmed = true;
                _host.LayoutScreen.SyncConnectionDuctGeometry(node);
            }
            else
            {
                var length = Math.Sqrt(duct.DeltaXM * duct.DeltaXM + duct.DeltaYM * duct.DeltaYM + duct.DeltaZM * duct.DeltaZM);
                if (length > 0)
                    duct.LengthM = length;
            }
        }
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
    }

    private void SaveEditedSystemMetadata(int rowIndex, string columnName)
    {
        var row = _ductGrid.Rows[rowIndex];
        var fallback = columnName == "AirSystem" ? _ductSystem.DefaultAirSystemType : _ductSystem.DefaultBranchId;
        if (row.Tag is SavedLayoutNode equipment)
        {
            var equipmentValue = Convert.ToString(row.Cells[columnName].Value, CultureInfo.CurrentCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(equipmentValue))
                equipmentValue = fallback;
            if (columnName == "AirSystem") equipment.AirSystemType = equipmentValue;
            else equipment.BranchId = equipmentValue;
            row.Cells[columnName].Value = equipmentValue;
            SaveDuctSystem();
            _host.LayoutScreen.RefreshLayoutDesigner();
            return;
        }
        if (row.Tag is not SystemRowTag tag)
            return;
        var edited = Convert.ToString(row.Cells[columnName].Value, CultureInfo.CurrentCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(edited))
            edited = fallback;
        if (tag.IsFitting)
        {
            var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
            if (fitting is null) return;
            if (columnName == "AirSystem") fitting.AirSystemType = edited;
            else fitting.BranchId = edited;
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fitting.Sequence);
            if (node is not null)
            {
                if (columnName == "AirSystem") node.AirSystemType = edited;
                else node.BranchId = edited;
            }
        }
        else
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == tag.Sequence);
            if (duct is null) return;
            if (columnName == "AirSystem") duct.AirSystemType = edited;
            else duct.BranchId = edited;
            var node = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedDuctSequence == duct.Sequence
                                                                    || x.ConnectionDuctSequence == duct.Sequence
                                                                    || x.SecondaryConnectionDuctSequence == duct.Sequence);
            if (node is not null)
            {
                if (columnName == "AirSystem") node.AirSystemType = edited;
                else node.BranchId = edited;
            }
        }
        row.Cells[columnName].Value = edited;
        SaveDuctSystem();
        _host.LayoutScreen.RefreshLayoutDesigner();
    }

    private void SaveEditedDescription(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _ductGrid.Rows.Count)
            return;
        var row = _ductGrid.Rows[rowIndex];
        if (row.Tag is not SystemRowTag tag)
            return;

        var description = Convert.ToString(row.Cells["Description"].Value, CultureInfo.CurrentCulture)?.Trim();
        if (tag.IsFitting)
        {
            var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == tag.Sequence);
            if (fitting is null) return;
            if (string.IsNullOrWhiteSpace(description))
                description = $"{fitting.FittingCode} — {FittingCatalog.Find(fitting.FittingCode).Description}";
            fitting.Description = description;
        }
        else
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == tag.Sequence);
            if (duct is null) return;
            if (string.IsNullOrWhiteSpace(description))
                description = CreateAutomaticDescription(duct.Calculate());
            duct.Description = description;
        }
        row.Cells["Description"].Value = description;
        SaveDuctSystem();
    }

    private void EditSelectedDuct()
    {
        var duct = SelectedDuct();
        var fitting = SelectedFitting();
        if (fitting is not null)
            EditSystemRecord(isFitting: true, fitting.Sequence);
        else if (duct is not null)
            EditSystemRecord(isFitting: false, duct.Sequence);
    }

    /// <summary>
    /// Opens a duct or fitting record in its calculator for editing (system grid "Edit item", and the Selection
    /// Inspector for records that are not on the drawing).
    /// </summary>
    internal void EditSystemRecord(bool isFitting, int sequence)
    {
        var duct = isFitting ? null : _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == sequence);
        var fitting = isFitting ? _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == sequence) : null;
        if (duct is null && fitting is null)
            return;

        if (fitting is not null)
        {
            _editingDuctSequence = null;
            _editingFittingSequence = fitting.Sequence;
            _host.FittingScreen.LoadFittingIntoCalculator(fitting);
            _host.FittingScreen._fittingDescription = fitting.Description;
            _host.FittingScreen._fittingEditingLabel.Text = $"Editing {fitting.Id} — {fitting.Description}";
            _host.FittingScreen._fittingEditingLabel.Visible = true;
            _host.FittingScreen._addFittingButton.Text = DefaultFittingActionText;
            _host.FittingScreen._addFittingButton.Visible = true;
            _mainTabs.SelectedTab = _fittingTab;
            _host.FittingScreen._fittingStatusLabel.Text = $"Editing {fitting.Id}. Change the inputs, then click {DefaultFittingActionText}.";
            _host.FittingScreen._fittingStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
            return;
        }

        _editingFittingSequence = null;
        _editingDuctSequence = duct!.Sequence;
        LoadDuctIntoCalculator(duct);
        _host.CalculatorScreen._ductEditingLabel.Text = $"Editing {duct.Id} — {duct.Description}";
        _host.CalculatorScreen._ductEditingLabel.Visible = true;
        _addDuctButton.Text = DefaultDuctActionText;
        _addDuctButton.Visible = true;
        _mainTabs.SelectedTab = _calculatorTab;
        _host._statusLabel.Text = $"Editing {duct.Id}. Change any input, then click {DefaultDuctActionText}.";
        _host._statusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    internal void ClearDuctEditingContext()
    {
        _editingDuctSequence = null;
        _host.CalculatorScreen._ductEditingLabel.Visible = false;
        _addDuctButton.Text = DefaultDuctActionText;
        _addDuctButton.Visible = false;
    }

    internal void ClearFittingEditingContext()
    {
        _editingFittingSequence = null;
        _host.FittingScreen._fittingEditingLabel.Visible = false;
        _host.FittingScreen._addFittingButton.Text = DefaultFittingActionText;
        _host.FittingScreen._addFittingButton.Visible = false;
    }

    private void LoadDuctIntoCalculator(SavedDuct duct)
    {
        _host._updatingControls = true;
        try
        {
            SelectChoice(_host.CalculatorScreen._modeCombo, CalculationMode.SizeByTargetFriction);
            SelectChoice(_host.CalculatorScreen._shapeCombo, duct.Shape);
            SelectChoice(_host.CalculatorScreen._equivalentMethodCombo, duct.EquivalentDiameterMethod);
            SelectChoice(_host.CalculatorScreen._pressureBasisCombo, duct.PressureLossBasis);
            SelectMaterial(duct.MaterialName, duct.RoughnessFeet);

            if (_host._currentUnits == UnitSystem.Imperial)
            {
                _host.CalculatorScreen._airflowText.Text = UnitConversions.M3PerSToCfm(duct.AirflowM3PerS).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._temperatureText.Text = UnitConversions.CelsiusToFahrenheit(duct.TemperatureC).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._lengthText.Text = UnitConversions.MToFeet(duct.LengthM).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._diameterText.Text = UnitConversions.MToInches(duct.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._widthText.Text = UnitConversions.MToInches(duct.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._heightText.Text = UnitConversions.MToInches(duct.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
            }
            else
            {
                _host.CalculatorScreen._airflowText.Text = UnitConversions.M3PerSToLps(duct.AirflowM3PerS).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._temperatureText.Text = duct.TemperatureC.ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._lengthText.Text = duct.LengthM.ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._diameterText.Text = UnitConversions.MToMillimeters(duct.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._widthText.Text = UnitConversions.MToMillimeters(duct.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
                _host.CalculatorScreen._heightText.Text = UnitConversions.MToMillimeters(duct.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
            }
            SelectChoice(_host.CalculatorScreen._rectangularFixedDimensionCombo, RectangularFixedDimension.Width);
            _host.CalculatorScreen._rectangularFixedDimensionText.Text = duct.Shape == DuctShape.Rectangular
                ? (_host._currentUnits == UnitSystem.Imperial
                    ? UnitConversions.MToInches(duct.WidthM)
                    : UnitConversions.MToMillimeters(duct.WidthM)).ToString("0.####", CultureInfo.CurrentCulture)
                : (_host._currentUnits == UnitSystem.Imperial ? "16" : "400");
        }
        finally
        {
            _host._updatingControls = false;
        }
        _host.CalculatorScreen.UpdateControlState();
        _host.CalculatorScreen.CalculateAndDisplay();
    }

    internal static void SelectChoice<T>(ComboBox combo, T value) where T : struct
    {
        for (var index = 0; index < combo.Items.Count; index++)
        {
            if (combo.Items[index] is MainForm.Choice<T> choice && EqualityComparer<T>.Default.Equals(choice.Value, value))
            {
                combo.SelectedIndex = index;
                return;
            }
        }
    }

    private void SelectMaterial(string name, double roughnessFeet)
    {
        for (var index = 0; index < _host.CalculatorScreen._materialCombo.Items.Count; index++)
        {
            if (_host.CalculatorScreen._materialCombo.Items[index] is DuctMaterial material
                && (string.Equals(material.Name, name, StringComparison.OrdinalIgnoreCase)
                    || Math.Abs(material.RoughnessFeet - roughnessFeet) <= 1e-12))
            {
                _host.CalculatorScreen._materialCombo.SelectedIndex = index;
                return;
            }
        }
    }

    private void RemoveSelectedDuct()
    {
        var duct = SelectedDuct();
        var fitting = SelectedFitting();
        if (duct is null && fitting is null)
            return;
        var id = duct?.Id ?? fitting!.Id;
        var itemDescription = duct?.Description ?? fitting!.Description;
        var response = MessageBox.Show(
            _host,
            $"Remove {id} ({itemDescription}) from this duct system?",
            "Remove system item",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return;

        if (duct is not null)
        {
            foreach (var node in _ductSystem.LayoutNodes.Where(x => x.LinkedDuctSequence == duct.Sequence
                                                                     || x.ConnectionDuctSequence == duct.Sequence
                                                                      || x.SecondaryConnectionDuctSequence == duct.Sequence
                                                                      || x.ObservedDuctSequence == duct.Sequence))
            {
                node.LinkedDuctSequence = null;
                node.ConnectionDuctSequence = null;
                node.SecondaryConnectionDuctSequence = null;
                if (node.ObservedDuctSequence == duct.Sequence)
                {
                    node.ObservedDuctSequence = null;
                    node.CalculationConnectionNodeId = string.Empty;
                }
            }
            _ductSystem.Ducts.Remove(duct);
            if (_editingDuctSequence == duct.Sequence)
            {
                _editingDuctSequence = null;
                _addDuctButton.Text = DefaultDuctActionText;
                _addDuctButton.Visible = false;
                _host.CalculatorScreen._ductEditingLabel.Visible = false;
            }
        }
        else
        {
            var removedFitting = fitting!;
            foreach (var node in _ductSystem.LayoutNodes.Where(x => x.LinkedFittingSequence == removedFitting.Sequence))
                node.LinkedFittingSequence = null;
            _ductSystem.Fittings.Remove(removedFitting);
            if (_editingFittingSequence == removedFitting.Sequence)
            {
                _editingFittingSequence = null;
                _host.FittingScreen._addFittingButton.Text = DefaultFittingActionText;
                _host.FittingScreen._addFittingButton.Visible = false;
                _host.FittingScreen._fittingEditingLabel.Visible = false;
            }
        }
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
    }

    private void DeleteAllSystemItems()
    {
        if (_ductSystem.Ducts.Count == 0 && _ductSystem.Fittings.Count == 0 && _ductSystem.LayoutNodes.Count == 0)
            return;
        var response = MessageBox.Show(_host,
            "Delete every duct, fitting, fan, coil, filter, and graphical layout item?\r\n\r\nThis cannot be undone.",
            "Delete Entire Duct System", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return;
        ClearAllSystemData();
        _host.LayoutScreen.ClearLayoutHistory();
    }

    internal void ClearAllSystemData()
    {
        _lastExportBasePath = string.Empty;
        ResetCriticalPath();
        _ductSystem.Ducts.Clear();
        _ductSystem.Fittings.Clear();
        _ductSystem.LayoutNodes.Clear();
        _ductSystem.NextSequence = 1;
        _ductSystem.NextFittingSequence = 1;
        _ductSystem.NextLayoutSequence = 1;
        _ductSystem.NextBranchSequence = 1;
        _ductSystem.CoordinateOriginNodeId = string.Empty;
        _ductSystem.CoordinateOriginXM = 0;
        _ductSystem.CoordinateOriginYM = 0;
        _ductSystem.CoordinateOriginZM = 0;
        _host.LayoutScreen._layoutViewportInitialized = false;
        _editingDuctSequence = null;
        _editingFittingSequence = null;
        _host.LayoutScreen._pendingLayoutNodeId = null;
        _host.LayoutScreen._pendingConnectionNodeId = null;
        _host.LayoutScreen._pendingConnectionSecondary = false;
        _addDuctButton.Text = DefaultDuctActionText;
        _host.FittingScreen._addFittingButton.Text = DefaultFittingActionText;
        _addDuctButton.Visible = false;
        _host.FittingScreen._addFittingButton.Visible = false;
        _host.CalculatorScreen._ductEditingLabel.Visible = false;
        _host.FittingScreen._fittingEditingLabel.Visible = false;
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
        _host.LayoutScreen.EnsureLayoutViewportPositioned(force: true);
        _systemStorageStatusLabel.Text = "All Duct System and Layout Designer items were deleted.";
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(140, 54, 54);
    }

    private void CopyDuctSystem()
    {
        if (_ductSystem.Ducts.Count == 0 && _ductSystem.Fittings.Count == 0 && _ductSystem.LayoutNodes.Count == 0)
            return;
        var builder = new StringBuilder();
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        builder.AppendLine(imperial
            ? "Item ID\tDescription\tAir system type\tBranch ID\tUpstream item ID\tDownstream item ID\tDelta X (ft)\tDelta Y (ft)\tDelta Z (ft)\tAirflow (cfm)\tType\tSize/code\tLength (ft)\tVelocity (fpm)\tPressure rate (in. w.c./100 ft)\tStatic pressure drop (in. w.c.)"
            : "Item ID\tDescription\tAir system type\tBranch ID\tUpstream item ID\tDownstream item ID\tDelta X (m)\tDelta Y (m)\tDelta Z (m)\tAirflow (L/s)\tType\tSize/code\tLength (m)\tVelocity (m/s)\tPressure rate (Pa/m)\tStatic pressure drop (Pa)");
        foreach (var duct in _ductSystem.Ducts.OrderBy(x => x.Sequence))
        {
            var result = duct.Calculate();
            var topology = LayoutTopologyForDuct(duct.Sequence);
            builder.AppendLine(string.Join('\t',
                duct.Id,
                duct.Description,
                duct.AirSystemType,
                duct.BranchId,
                topology.Upstream,
                topology.Downstream,
                FormatSystemDelta(duct.DeltaXM), FormatSystemDelta(duct.DeltaYM), FormatSystemDelta(duct.DeltaZM),
                FormatSystemAirflow(duct.AirflowM3PerS),
                duct.Shape,
                FormatSystemGeometry(result.Input.Geometry),
                FormatSystemLength(duct.LengthM),
                FormatSystemVelocity(result.ActualVelocityMPerS),
                FormatSystemPressureRate(result),
                FormatSystemStaticDrop(result)));
        }
        foreach (var fitting in _ductSystem.Fittings.OrderBy(x => x.Sequence))
        {
            var result = fitting.Calculate();
            var topology = LayoutTopologyForFitting(fitting.Sequence);
            builder.AppendLine(string.Join('\t', fitting.Id, fitting.Description,
                fitting.AirSystemType, fitting.BranchId,
                topology.Upstream, topology.Downstream,
                FormatSystemDelta(fitting.DeltaXM), FormatSystemDelta(fitting.DeltaYM), FormatSystemDelta(fitting.DeltaZM),
                result.AirflowM3PerS is double fittingAirflow ? FormatSystemAirflow(fittingAirflow) : "—",
                result.ReferenceGeometry is null ? "Fitting" : $"{result.ReferenceGeometry.Shape} fitting",
                result.ReferenceGeometry is null ? fitting.FittingCode : $"{fitting.FittingCode} / {FormatSystemGeometry(result.ReferenceGeometry)}",
                "—", FormatSystemVelocity(fitting.ReferenceVelocityMPerS), "—", FormatSystemFittingDrop(result.PressureLossPa)));
        }
        foreach (var equipment in _ductSystem.LayoutNodes.Where(x => x.IsDefined && (x.Type is LayoutNodeType.Reservoir
                     or LayoutNodeType.Fan or LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                     or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent)))
        {
            var topology = LayoutTopologyForNode(equipment);
            builder.AppendLine(string.Join('\t', equipment.Id,
                equipment.Type == LayoutNodeType.Reservoir ? equipment.SpaceRole
                    : equipment.Type == LayoutNodeType.CalculationPoint ? $"{equipment.CalculationPointType} Calculation Point"
                    : equipment.Type == LayoutNodeType.Fan ? equipment.FanEquipmentName : equipment.Type == LayoutNodeType.Coil ? "Coil"
                    : equipment.Type == LayoutNodeType.Filter ? "Filter"
                    : equipment.Type == LayoutNodeType.CustomComponent ? equipment.Description : equipment.GrdType,
                equipment.AirSystemType, equipment.BranchId,
                topology.Upstream, topology.Downstream,
                FormatSystemDelta(equipment.DeltaXM), FormatSystemDelta(equipment.DeltaYM), FormatSystemDelta(equipment.DeltaZM),
                equipment.AirflowM3PerS > 0 ? FormatSystemAirflow(equipment.AirflowM3PerS) : "—",
                equipment.Type == LayoutNodeType.CalculationPoint ? "Calculation point" : "Equipment",
                equipment.Type == LayoutNodeType.Grd ? equipment.GrdType
                    : equipment.Type == LayoutNodeType.CustomComponent ? equipment.Description
                    : equipment.Type == LayoutNodeType.Reservoir ? equipment.SpaceRole
                    : equipment.Type == LayoutNodeType.CalculationPoint ? equipment.CalculationPointType : equipment.Type,
                "—", equipment.Type == LayoutNodeType.CalculationPoint
                    && string.Equals(equipment.CalculationPointType, "Velocity", StringComparison.OrdinalIgnoreCase)
                        ? FormatSystemVelocity(equipment.VelocityMPerS) : "—", "—",
                equipment.Type == LayoutNodeType.CalculationPoint
                    && string.Equals(equipment.CalculationPointType, "Pressure", StringComparison.OrdinalIgnoreCase)
                        ? FormatSystemFittingDrop(equipment.PressurePa)
                        : equipment.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent
                            ? FormatSystemFittingDrop(equipment.PressureDropPa) : "0"));
        }
        builder.AppendLine(_criticalPathIssues.Count > 0
            ? $"Critical-path total pressure loss\tnot available ({CriticalPathIssueSummary()})"
            : $"Critical-path total pressure loss\t{FormatSystemTotal(_criticalPathPressureLossPa)}");
        try
        {
            Clipboard.SetText(builder.ToString());
            _systemStorageStatusLabel.Text = "Duct system copied to the clipboard.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (ExternalException ex)
        {
            _systemStorageStatusLabel.Text = $"The clipboard is unavailable: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
        }
    }

    /// <summary>
    /// Writes the project report (the PDF that Save produces next to the project file) to <paramref name="pdfPath"/>.
    /// Used by Save, Print Report and the Reports tab preview, so all three show the same document.
    /// </summary>
    /// <summary>Writes the project report PDF (the same report Print Report and Save write).</summary>
    internal void WriteReportPdf(string pdfPath) => PrepareReportPdf()(pdfPath);

    /// <summary>
    /// Gathers everything the report shows from the current project and window (on the UI thread) and returns the
    /// step that writes the PDF from those values alone, which may run on a background thread.
    /// </summary>
    internal Action<string> PrepareReportPdf()
    {
        RefreshDuctSystemGrid();
        var headers = new[]
        {
            _host._currentUnits == UnitSystem.Imperial ? "Airflow (cfm)" : "Airflow (L/s)",
            _host._currentUnits == UnitSystem.Imperial ? "Static Pressure In (in. w.c.)" : "Static Pressure In (Pa)",
            _host._currentUnits == UnitSystem.Imperial ? "Static Pressure Out (in. w.c.)" : "Static Pressure Out (Pa)",
            "Air System", _host._currentUnits == UnitSystem.Imperial ? "Temperature (deg F)" : "Temperature (deg C)",
            _host._currentUnits == UnitSystem.Imperial ? "Pressure Rate (in. w.c./100 ft)" : "Pressure Rate (Pa/m)",
            _host._currentUnits == UnitSystem.Imperial ? "Velocity (fpm)" : "Velocity (m/s)",
            _host._currentUnits == UnitSystem.Imperial ? "Length (ft)" : "Length (m)", "Duct Size"
        };
        headers = new[] { "Item ID", "Description" }.Concat(headers).ToArray();
        var rows = _ductGrid.Rows.Cast<DataGridViewRow>()
            .Select(row => (IReadOnlyList<string>)new[]
            {
                Cell(row, "Id"), Cell(row, "Description"), Cell(row, "Airflow"), Cell(row, "PressureIn"),
                Cell(row, "PressureOut"), Cell(row, "AirSystem"), Cell(row, "Temperature"),
                Cell(row, "PressureRate"), Cell(row, "Velocity"), Cell(row, "Length"), Cell(row, "Size")
            }).ToArray();
        var projectDate = DateTime.TryParse(_ductSystem.ProjectDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var parsedDate)
            ? parsedDate.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
            : string.IsNullOrWhiteSpace(_ductSystem.ProjectDate)
                ? DateTime.Today.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
                : _ductSystem.ProjectDate;
        var logo = BuildReportLogo();
        var fan = _ductSystem.LayoutNodes.SingleOrDefault(x => x.Type == LayoutNodeType.Fan);
        var fanHorsepower = fan is null ? 0.0 : TryBrakeHorsepower(fan.AirflowM3PerS,
            fan.FanTotalStaticPressurePa, fan.FanEfficiencyPercent);
        var metadata = new DuctSystemPdfMetadata(_ductSystem.UserName, _ductSystem.ProjectName,
            _ductSystem.ProjectNumber, projectDate, logo.Base64, logo.Width, logo.Height,
            DateTime.Now.ToString("MMMM d, yyyy h:mm tt", CultureInfo.CurrentCulture),
            fan?.Id ?? "No fan added", fan is null ? "Not available" : PdfAirflow(fan.AirflowM3PerS),
            fan is null ? "Not available" : FormatSystemTotal(fan.FanStaticPressurePa),
            fan is null ? "Not available" : $"{fan.FanEfficiencyPercent:0.#}%",
            fan is null ? "Not available" : FormatHorsepower(fanHorsepower),
            !_criticalPathCalculated
                ? "Not calculated"
                : _criticalPathIssues.Count > 0
                    ? $"Not available. {CriticalPathIssueSummary()}."
                    : $"{_criticalPathRoute} ({FormatSystemTotal(_criticalPathPressureLossPa)} total loss)"
                      + LayoutGeometryNote(". ")
                      + (LayoutReviewMessages() is { Count: > 0 } review
                          ? ": " + string.Join(" ", review.Take(6))
                          : string.Empty),
            BuildPressureProfile(),
            BuildLeakageSummary(),
            fan?.IsAirHandlingUnit == true,
            fan is null ? "Not available" : FormatSystemTotal(fan.FanInternalStaticPressurePa),
            fan is null ? "Not available" : FormatSystemTotal(fan.FanTotalStaticPressurePa));
        var layout = BuildPdfLayout();
        var reportItems = BuildPdfReportItems();
        var schematics = BuildPdfSchematics();
        var total = _systemTotalLabel.Text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " — ", StringComparison.Ordinal);
        var subtitle = $"Exported {DateTime.Now:MMMM d, yyyy h:mm tt} - {_host._currentUnits} units";
        // Everything above reads the window's state; the PDF itself is built from these values only, so it can be
        // written on another thread.
        return path => DuctSystemPdf.Export(path, "Duct System Schedule", subtitle, headers, rows, total,
            metadata, layout, reportItems, schematics);
    }

    internal void ExportDuctSystem()
    {
        var automatedExportPath = AutomationOptions.ExportBasePath;
        var basePath = string.IsNullOrWhiteSpace(automatedExportPath) ? _lastExportBasePath : automatedExportPath;
        if (string.IsNullOrWhiteSpace(basePath))
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save FanWorks project, report, and schedule",
                Filter = "FanWorks project (*.fanworks)|*.fanworks|FanWorks report (*.pdf)|*.pdf",
                DefaultExt = "fanworks",
                AddExtension = true,
                FileName = $"DuctSystem-{DateTime.Now:yyyyMMdd-HHmm}{ProjectFile.Extension}",
                OverwritePrompt = true,
                CheckPathExists = true,
                ValidateNames = true,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(_host) != DialogResult.OK)
                return;
            basePath = Path.Combine(Path.GetDirectoryName(dialog.FileName) ?? "",
                Path.GetFileNameWithoutExtension(dialog.FileName));
        }
        var projectPath = basePath + ProjectFile.Extension;
        var csvPath = basePath + ".csv";
        var pdfPath = basePath + ".pdf";
        // The project file is written first and on its own, so a failure while
        // building the PDF report can never cost the user their model.
        try
        {
            _ductSystem.LastEditedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            ProjectFile.Save(projectPath, _ductSystem);
            _lastExportBasePath = basePath;
            DiagnosticLog.Info($"Project saved to {projectPath}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException or System.Security.SecurityException)
        {
            DiagnosticLog.Error($"Project save to {projectPath} failed.", ex);
            _systemStorageStatusLabel.Text = $"The project could not be saved: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host,
                    $"The project could not be saved.\r\n\r\n{ex.Message}\r\n\r\nAny earlier copy of this project file was left unchanged. Choose another location and save again.",
                    "FanWorks Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var targetDirectory = Path.GetDirectoryName(pdfPath);
        var temporaryStem = $".{Path.GetFileName(basePath)}-{Guid.NewGuid():N}";
        var temporaryPdfPath = Path.Combine(targetDirectory ?? string.Empty, temporaryStem + ".pdf.tmp");
        var temporaryCsvPath = Path.Combine(targetDirectory ?? string.Empty, temporaryStem + ".csv.tmp");

        try
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new InvalidOperationException("Choose a folder for the project report.");
            Directory.CreateDirectory(targetDirectory);
            _saveButton.Enabled = false;
            _saveButton.Text = "Saving...";
            _host.UseWaitCursor = true;
            _systemStorageStatusLabel.Text = "Creating the project PDF and CSV...";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(14, 111, 230);
            Application.DoEvents();

            WriteReportPdf(temporaryPdfPath);
            DuctSystemCsv.Export(temporaryCsvPath, _ductSystem);
            File.Move(temporaryPdfPath, pdfPath, overwrite: true);
            File.Move(temporaryCsvPath, csvPath, overwrite: true);
            _systemStorageStatusLabel.Text = $"Saved {Path.GetFileName(projectPath)}, {Path.GetFileName(pdfPath)}, and {Path.GetFileName(csvPath)}. Future saves overwrite these files.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host,
                    $"Project saved successfully.\r\n\r\nProject: {projectPath}\r\nPDF report: {pdfPath}\r\nCSV schedule: {csvPath}",
                    "FanWorks Project Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error($"Report export to {pdfPath} failed after the project was saved.", ex);
            _systemStorageStatusLabel.Text = $"The project was saved, but the PDF/CSV report could not be created: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host,
                    $"The project file was saved:\r\n{projectPath}\r\n\r\nThe PDF report and CSV schedule could not be created.\r\n\r\n{ex.Message}\r\n\r\nEarlier copies of the report were left unchanged.",
                    "FanWorks Report Not Created", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            foreach (var temporaryPath in new[] { temporaryPdfPath, temporaryCsvPath })
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; the original export error remains the actionable result.
                }
            }
            _host.UseWaitCursor = false;
            _saveButton.Text = "Save";
            _saveButton.Enabled = true;
        }
    }

    private (string Base64, int Width, int Height) BuildReportLogo()
    {
        if (string.IsNullOrWhiteSpace(_ductSystem.CompanyLogoPngBase64))
            return (string.Empty, 0, 0);
        try
        {
            using var sourceStream = new MemoryStream(Convert.FromBase64String(_ductSystem.CompanyLogoPngBase64));
            using var source = Image.FromStream(sourceStream);
            var scale = Math.Min(1.0, Math.Min(1000.0 / source.Width, 400.0 / source.Height));
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            using var jpegStream = new MemoryStream();
            bitmap.Save(jpegStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            return (Convert.ToBase64String(jpegStream.ToArray()), width, height);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OutOfMemoryException)
        {
            return (string.Empty, 0, 0);
        }
    }

    private IReadOnlyList<DuctSystemPdfSchematic> BuildPdfSchematics()
    {
        if (_ductSystem.LayoutNodes.Count == 0)
            return Array.Empty<DuctSystemPdfSchematic>();
        return new[]
        {
            RenderPdfSchematic("Layout Designer - Isometric Positive", LayoutViewMode.Isometric, false),
            RenderPdfSchematic("Layout Designer - Isometric Negative", LayoutViewMode.IsometricNegative, false),
            RenderPdfSchematic("Layout Designer - Top-Down", LayoutViewMode.TopDown, false),
            RenderPdfSchematic("Layout Designer - Critical Path", LayoutViewMode.Isometric, true)
        };
    }

    private DuctSystemPdfSchematic RenderPdfSchematic(string title, LayoutViewMode viewMode,
        bool highlightCriticalPath)
    {
        const int width = 1920;
        const int height = 1120;
        var sourceBackground = viewMode == LayoutViewMode.TopDown ? _ductSystem.LayoutPdfBackground : null;
        var reportBackground = sourceBackground is null ? null : new LayoutPdfBackground
        {
            SourceFileName = sourceBackground.SourceFileName,
            PageNumber = sourceBackground.PageNumber,
            PageCount = sourceBackground.PageCount,
            PageWidthPoints = sourceBackground.PageWidthPoints,
            PageHeightPoints = sourceBackground.PageHeightPoints,
            PagePngBase64 = sourceBackground.PagePngBase64,
            PaperInches = sourceBackground.PaperInches,
            DrawingFeet = sourceBackground.DrawingFeet,
            CenterXFeet = sourceBackground.CenterXFeet,
            CenterYFeet = sourceBackground.CenterYFeet,
            CenterZFeet = sourceBackground.CenterZFeet,
            Visible = true
        };
        using var canvas = new LayoutDesignerCanvas
        {
            Size = new Size(width, height),
            BackColor = Color.FromArgb(250, 252, 252),
            NodeSource = () => _ductSystem.LayoutNodes,
            CoordinateSource = () => LayoutCoordinateResolver.Resolve(_ductSystem),
            CaptionSource = _host.LayoutScreen.LayoutNodeCaption,
            ConnectionCaptionSource = _host.LayoutScreen.LayoutConnectionCaption,
            ConnectionFlowReversedSource = LayoutDesignerScreen.LayoutConnectionFlowReversed,
            CriticalPathNodeSource = _host.LayoutScreen.IsLayoutNodeInCalculatedCriticalPath,
            CriticalPathConnectionSource = _host.LayoutScreen.IsLayoutConnectionInCalculatedCriticalPath,
            PdfBackgroundSource = () => reportBackground,
            ViewMode = viewMode,
            ShowToScale = true,
            FeetPerGridCell = Math.Max(0.1, _ductSystem.LayoutFeetPerGridCell),
            HighlightCriticalPath = highlightCriticalPath,
            AvoidLabelOverlap = true
        };
        canvas.CreateControl();
        canvas.FitContentToViewport(110);
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        bitmap.SetResolution(150, 150);
        canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        return new DuctSystemPdfSchematic(title, Convert.ToBase64String(stream.ToArray()), width, height,
            highlightCriticalPath, canvas.FeetPerGridCell, canvas.PixelsPerDrawingFoot);
    }

    private static (string Base64, int Width, int Height) BuildFittingReportImage(string fittingCode)
    {
        try
        {
            using var source = FittingImageResources.Load(fittingCode);
            var scale = Math.Min(1.0, 1400.0 / Math.Max(1, source.Width));
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            return (Convert.ToBase64String(stream.ToArray()), width, height);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OutOfMemoryException)
        {
            return (string.Empty, 0, 0);
        }
    }

    private DuctSystemPdfLayout? BuildPdfLayout()
    {
        if (_ductSystem.LayoutNodes.Count == 0)
            return null;
        var nodes = _ductSystem.LayoutNodes.Select(node =>
        {
            var inletPorts = node.Type == LayoutNodeType.CalculationPoint ? 0
                : node.Type == LayoutNodeType.Fitting
                ? FittingCatalog.InletPortCount(node.FittingGroup)
                : node.Type == LayoutNodeType.Reservoir && LayoutDesignerScreen.IsSpaceSourceRole(node.SpaceRole) ? 0 : 1;
            var outletPorts = node.Type == LayoutNodeType.CalculationPoint ? 0
                : node.Type == LayoutNodeType.Fitting
                ? FittingCatalog.OutletPortCount(node.FittingGroup)
                : node.Type == LayoutNodeType.Reservoir && LayoutDesignerScreen.IsSpaceSinkRole(node.SpaceRole) ? 0 : 1;
            return new DuctSystemPdfLayoutNode(node.Id, _host.LayoutScreen.LayoutNodeCaption(node),
                node.Type.ToString(), node.Type == LayoutNodeType.Grd ? node.GrdType
                    : node.Type == LayoutNodeType.Reservoir ? node.SpaceRole
                    : node.Type == LayoutNodeType.CalculationPoint ? node.CalculationPointType
                    : node.Type == LayoutNodeType.CustomComponent ? node.Description
                    : node.IsAirHandlingUnit ? LayoutFanData.AirHandlingUnitTool : node.FittingGroup,
                node.GridX, node.GridY, inletPorts, outletPorts, node.AirSystemType, node.BranchId,
                node.BranchOutletBelow, node.IsDefined, node.AirflowOverride);
        }).ToArray();
        var connections = new List<DuctSystemPdfLayoutConnection>();
        foreach (var node in _ductSystem.LayoutNodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
            {
                var primaryDuct = node.ConnectionDuctSequence is int primaryDuctSequence
                    ? _ductSystem.Ducts.FirstOrDefault(duct => duct.Sequence == primaryDuctSequence)
                    : null;
                connections.Add(new DuctSystemPdfLayoutConnection(node.ParentNodeId, node.Id, false,
                    primaryDuct is { RequiresResizing: false },
                    node.ConnectionDuctSequence is int primarySequence ? $"D-{primarySequence:000}" : string.Empty,
                    node.BranchId,
                    _host.LayoutScreen.LayoutConnectionCaption(new LayoutConnectionRef(node, false)).Replace("\r", string.Empty)
                        .Split('\n').ElementAtOrDefault(1) ?? "—"));
            }
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
            {
                var secondaryDuct = node.SecondaryConnectionDuctSequence is int secondaryDuctSequence
                    ? _ductSystem.Ducts.FirstOrDefault(duct => duct.Sequence == secondaryDuctSequence)
                    : null;
                connections.Add(new DuctSystemPdfLayoutConnection(node.SecondaryParentNodeId, node.Id, true,
                    secondaryDuct is { RequiresResizing: false },
                    node.SecondaryConnectionDuctSequence is int secondarySequence ? $"D-{secondarySequence:000}" : string.Empty,
                    node.BranchId,
                    _host.LayoutScreen.LayoutConnectionCaption(new LayoutConnectionRef(node, true)).Replace("\r", string.Empty)
                        .Split('\n').ElementAtOrDefault(1) ?? "—"));
            }
        }
        return new DuctSystemPdfLayout(nodes, connections);
    }

    private IReadOnlyList<DuctSystemPdfReportItem> BuildPdfReportItems()
    {
        var items = new List<(string Key, DuctSystemPdfReportItem Item)>();
        var leakage = SmacnaLeakage.Analyze(_ductSystem);
        foreach (var duct in _ductSystem.Ducts.OrderBy(x => x.Sequence))
            items.Add((DuctKey(duct.Sequence), BuildDuctReportItem(duct, leakage)));
        foreach (var fitting in _ductSystem.Fittings.OrderBy(x => x.Sequence))
            items.Add((FittingKey(fitting.Sequence), BuildFittingReportItem(fitting, leakage)));
        foreach (var node in _ductSystem.LayoutNodes
                     .Where(x => x.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
                                     or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                                     or LayoutNodeType.CustomComponent
                                 || x.Type == LayoutNodeType.Fitting && !x.IsDefined && x.LinkedFittingSequence is null)
                     .OrderBy(x => x.Sequence))
            items.Add((LayoutKey(node.Sequence), BuildNodeReportItem(node)));

        return OrderPdfReportItems(items);
    }

    internal DuctSystemPdfReportItem BuildDuctReportItem(SavedDuct duct, SystemLeakageResult? leakage = null)
    {
        var topology = LayoutTopologyForDuct(duct.Sequence);
        var inputs = new List<DuctSystemPdfField>
        {
            PdfField("Air system", duct.AirSystemType), PdfField("Branch", duct.BranchId),
            PdfField("Upstream item", topology.Upstream), PdfField("Downstream item", topology.Downstream),
            PdfField("Airflow", PdfAirflow(duct.AirflowM3PerS)), PdfField("Air temperature", PdfTemperature(duct.TemperatureC)),
            PdfField("Relative humidity", $"{duct.RelativeHumidity * 100:0.#}%"), PdfField("Atmospheric pressure", $"{duct.AtmosphericPressurePa:N0} Pa"),
            PdfField("Duct shape", duct.Shape.ToString()), PdfField("Entered size", SafeDuctSize(duct)),
            PdfField("Duct length", PdfLength(duct.LengthM)), PdfField("Material", duct.MaterialName),
            PdfField("Absolute roughness", PdfRoughness(duct.RoughnessFeet)),
            PdfField("Equivalent diameter method", duct.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher ? "Huebscher" : "Hydraulic diameter"),
            PdfField("Pressure-loss basis", duct.PressureLossBasis == PressureLossBasis.EquivalentRoundArea ? "Equivalent round area" : "Actual area"),
            PdfField("Relative delta X", PdfDelta(duct.DeltaXM)), PdfField("Relative delta Y", PdfDelta(duct.DeltaYM)),
            PdfField("Relative delta Z", PdfDelta(duct.DeltaZM))
        };
        var outputs = new List<DuctSystemPdfField>();
        var calculations = new List<DuctSystemPdfCalculationSection>();
        try
        {
            var result = duct.Calculate();
            var pressure = SystemPressureForDuctPair(duct.Sequence);
            outputs.AddRange(new[]
            {
                PdfField("Actual area", PdfArea(result.Input.Geometry.ActualAreaM2)),
                PdfField("Equivalent diameter", PdfDiameter(result.EquivalentDiameterM)),
                PdfField("Equivalent round area", PdfArea(result.EquivalentAreaM2)),
                PdfField("Actual velocity", PdfVelocity(result.ActualVelocityMPerS)),
                PdfField("Equivalent velocity", PdfVelocity(result.EquivalentVelocityMPerS)),
                PdfField("Calculation velocity", PdfVelocity(result.CalculationVelocityMPerS)),
                PdfField("Reynolds number", $"{result.ReynoldsNumber:N0}"),
                PdfField("Flow regime", result.Friction.Regime.ToString()),
                PdfField("Darcy friction factor", PdfFriction(result.Friction)),
                PdfField("Pressure-loss rate", PdfPressureRate(result)),
                PdfField("Total pressure loss", PdfPressure(result.TotalPressureLossUpperPa)),
                PdfField("Static pressure in", PdfPressure(pressure.Inlet)),
                PdfField("Static pressure out", PdfPressure(pressure.Outlet)),
                PdfField("Calculation basis", result.CalculationBasisDescription)
            });
            calculations.Add(DuctSystemPdfCalculationSection.Title("DUCT SIZING CALCULATION"));
            calculations.AddRange(BuildDuctPdfCalculations(result));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            outputs.Add(PdfField("Calculation status", $"Invalid - {ex.Message}"));
        }
        var surfaceArea = SurfaceAreaCalculator.ForDuct(duct);
        outputs.Add(PdfField("Surface area", FormatSurfaceAreaWithUnit(surfaceArea)));
        calculations.AddRange(BuildSurfaceAreaSections(surfaceArea, leakage ?? SmacnaLeakage.Analyze(_ductSystem)));
        var quickCheck = new[]
        {
            PdfField("Duct size", SafeDuctSize(duct)),
            PdfField("Airflow", PdfAirflow(duct.AirflowM3PerS)),
            PdfField("Duct length", PdfLength(duct.LengthM)),
            PdfField("Duct velocity", outputs.FirstOrDefault(field => field.Label == "Actual velocity")?.Value
                ?? "Not calculated"),
            PdfField("Pressure rate", outputs.FirstOrDefault(field => field.Label == "Pressure-loss rate")?.Value
                ?? "Not calculated"),
            PdfField("Pressure drop", outputs.FirstOrDefault(field => field.Label == "Total pressure loss")?.Value
                ?? "Not calculated")
        };
        return new DuctSystemPdfReportItem(duct.Id, duct.Description, "STRAIGHT DUCT", inputs, outputs, calculations,
                QuickCheck: quickCheck);
    }

    internal DuctSystemPdfReportItem BuildFittingReportItem(SavedFitting fitting, SystemLeakageResult? leakage = null)
    {
        var topology = LayoutTopologyForFitting(fitting.Sequence);
        var layoutNode = _ductSystem.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fitting.Sequence);
        var inputs = new List<DuctSystemPdfField>
        {
            PdfField("Layout item", layoutNode?.Id ?? "-"), PdfField("Air system", fitting.AirSystemType),
            PdfField("Branch", fitting.BranchId), PdfField("Upstream item", topology.Upstream),
            PdfField("Downstream item", topology.Downstream), PdfField("Fitting number", fitting.FittingCode),
            PdfField("Airflow", fitting.AirflowM3PerS > 0 ? PdfAirflow(fitting.AirflowM3PerS) : "-"),
            PdfField("Air temperature", PdfTemperature(fitting.TemperatureC)),
            PdfField("Reference duct", SafeFittingSize(fitting)),
            PdfField("Relative delta X", PdfDelta(fitting.DeltaXM)), PdfField("Relative delta Y", PdfDelta(fitting.DeltaYM)),
            PdfField("Relative delta Z", PdfDelta(fitting.DeltaZM))
        };
        var outputs = new List<DuctSystemPdfField>();
        var calculations = new List<DuctSystemPdfCalculationSection>();
        var referenceImage = BuildFittingReportImage(fitting.FittingCode);
        double? quickCheckCoefficient = null;
        try
        {
            var result = fitting.Calculate();
            quickCheckCoefficient = result.Coefficient;
            var pressure = SystemPressureForFittingPair(fitting.Sequence);
            foreach (var pair in fitting.Inputs.OrderBy(x => x.Key))
            {
                if (pair.Key is "selectedCfm" or "soundNc" or "soundBelow15" or "throw150" or "throw100" or "throw50" or "staticPressure" or "selectedInlet"
                    or "selectedPressureDropInWg" or "lowerCfm" or "lowerPressureDropInWg" or "upperCfm" or "upperPressureDropInWg")
                    continue;
                var definitionInput = result.Definition.Inputs.FirstOrDefault(x =>
                    string.Equals(x.Key, pair.Key, StringComparison.OrdinalIgnoreCase));
                var inputLabel = (definitionInput?.Label ?? pair.Key).Replace(" (ratio)", string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                inputs.Add(PdfField(inputLabel, $"{pair.Value:0.####}"));
            }
            if (fitting.Junction is JunctionFlowData junction)
            {
                inputs.Add(PdfField("Calculated path", junction.Path.ToString()));
                inputs.Add(PdfField("Common airflow", PdfAirflow(junction.CommonAirflowM3PerS)));
                inputs.Add(PdfField("Straight airflow", PdfAirflow(junction.StraightAirflowM3PerS)));
                inputs.Add(PdfField("Branch airflow", PdfAirflow(junction.BranchAirflowM3PerS)));
            }
            outputs.AddRange(new[]
            {
                PdfField("Fitting description", result.Definition.Description),
                PdfField("Category", result.Definition.Category),
                PdfField("Loss coefficient C", $"{result.Coefficient:0.####}"),
                PdfField("Reference velocity", PdfVelocity(result.ReferenceVelocityMPerS)),
                PdfField("Velocity pressure", PdfPressure(result.VelocityPressurePa)),
                PdfField("Fitting pressure loss", PdfPressure(result.PressureLossPa)),
                PdfField("Static pressure in", PdfPressure(pressure.Inlet)),
                PdfField("Static pressure out", PdfPressure(pressure.Outlet)),
                PdfField("Air density", $"{result.Air.DensityKgPerM3:0.####} kg/m3"),
                PdfField("Calculation source", result.Definition.IsAutomated ? "Built-in table/equation" : "Entered coefficient")
            });
            if (IsPublishedGrdCode(result.Definition.Code))
            {
                var selection = GrdSelectionFromResult(result);
                outputs.Add(PdfField("Selected published airflow", $"{selection.Selected.AirflowCfm:0} cfm"));
                if (selection.InletDiameterIn is int inletDiameter)
                    outputs.Add(PdfField("Automatically selected inlet", $"{inletDiameter} in"));
                outputs.Add(PdfField("Manufacturer total pressure", $"{selection.Selected.TotalPressureInWg:0.###} in. w.c."));
                outputs.Add(PdfField("Sound", $"NC {selection.Selected.SoundNc}"));
                outputs.Add(PdfField("Throw (150/100/50 fpm)", $"{selection.Selected.ThrowFt} ft"));
            }
            if (IsPublishedVavCode(result.Definition.Code))
            {
                var selection = VavSelectionFromResult(result);
                outputs.Add(PdfField("VAV configuration", selection.Configuration));
                outputs.Add(PdfField("Manufacturer airside pressure drop", $"{selection.PressureDropInWg:0.###} in. w.c."));
                outputs.Add(PdfField("Lookup method", selection.WasInterpolated
                    ? $"Linear interpolation: {selection.LowerPoint.AirflowCfm:0} to {selection.UpperPoint.AirflowCfm:0} cfm"
                    : "Exact published airflow point"));
            }
            calculations.Add(DuctSystemPdfCalculationSection.Title(SurfaceAreaCalculator.IsTerminalOrBox(fitting.FittingCode)
                ? "COMPONENT SIZING CALCULATION" : "FITTING SIZING CALCULATION"));
            calculations.AddRange(BuildFittingPdfCalculations(result));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        {
            outputs.Add(PdfField("Calculation status", $"Invalid - {ex.Message}"));
        }
        if (SurfaceAreaCalculator.ForFitting(fitting) is { } fittingArea)
        {
            outputs.Add(PdfField("Surface area (estimate)", FormatSurfaceAreaWithUnit(fittingArea)));
            calculations.AddRange(BuildSurfaceAreaSections(fittingArea, leakage ?? SmacnaLeakage.Analyze(_ductSystem)));
        }
        var quickCheck = new[]
        {
            PdfField("Fitting type / name", outputs.FirstOrDefault(field => field.Label == "Fitting description")?.Value
                ?? $"{fitting.FittingCode} - {fitting.Description}"),
            // Rounded to the thousandths for a quick read; the full value is in the outputs and the calculation.
            PdfField("Fitting C-value", quickCheckCoefficient is double coefficient
                ? coefficient.ToString("0.000", CultureInfo.InvariantCulture) : "Not calculated"),
            PdfField("Pressure drop", outputs.FirstOrDefault(field => field.Label == "Fitting pressure loss")?.Value
                ?? "Not calculated")
        };
        return new DuctSystemPdfReportItem(fitting.Id, fitting.Description, "FITTING", inputs, outputs, calculations,
                referenceImage.Base64, referenceImage.Width, referenceImage.Height, quickCheck);
    }

    internal DuctSystemPdfReportItem BuildNodeReportItem(SavedLayoutNode node)
    {
        var topology = LayoutTopologyForNode(node);
        var description = LayoutReportDescription(node);
        var inputs = new List<DuctSystemPdfField>
        {
            PdfField("Definition status", node.IsDefined ? "Defined" : "Undefined - double-click in Designer"),
            PdfField("Air system", node.IsDefined ? node.AirSystemType : "-"), PdfField("Branch", node.BranchId),
            PdfField("Upstream item", topology.Upstream), PdfField("Downstream item", topology.Downstream),
            PdfField("Relative delta X", PdfDelta(node.DeltaXM)), PdfField("Relative delta Y", PdfDelta(node.DeltaYM)),
            PdfField("Relative delta Z", PdfDelta(node.DeltaZM))
        };
        if (node.IsDefined)
        {
            inputs.Add(PdfField("Airflow", node.AirflowM3PerS > 0 ? PdfAirflow(node.AirflowM3PerS) : "-"));
            inputs.Add(PdfField("Air temperature", PdfTemperature(node.TemperatureC)));
            if (node.Type == LayoutNodeType.Reservoir)
                inputs.Add(PdfField("Space role", node.SpaceRole));
            if (node.Type == LayoutNodeType.Grd)
            {
                inputs.Add(PdfField("GRD type", node.GrdType));
                if (string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase))
                    inputs.Add(PdfField("Nominal duct size", $"{node.GrdNominalDuctSizeIn} in"));
                else if (string.Equals(node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase))
                {
                    inputs.Add(PdfField("Face size", $"{node.GrdFaceSizeIn:0} x {node.GrdFaceSizeIn:0} in"));
                    inputs.Add(PdfField("Neck diameter", $"{node.GrdNeckDiameterIn:0} in"));
                }
                else if (string.Equals(node.GrdType, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase))
                {
                    inputs.Add(PdfField("Diffuser length", $"{node.GrdLengthFt:0} ft"));
                    inputs.Add(PdfField("Slot size", $"{node.GrdSlotWidthIn:0.##} in"));
                    inputs.Add(PdfField("Number of slots", $"{node.GrdSlotCount}"));
                    inputs.Add(PdfField("Selected inlet", $"{node.GrdInletDiameterIn:0} in"));
                }
            }
            if (node.Type == LayoutNodeType.CustomComponent)
                inputs.Add(PdfField("Component description", node.Description));
            if (node.Type == LayoutNodeType.Fan)
            {
                // Same for a fan and an air handling unit.
                inputs.Add(PdfField("External static pressure (ESP)", PdfPressure(node.FanStaticPressurePa)));
                inputs.Add(PdfField("Internal static pressure", PdfPressure(node.FanInternalStaticPressurePa)));
                inputs.Add(PdfField("Fan efficiency", $"{node.FanEfficiencyPercent:0.##}%"));
            }
            if (node.Type == LayoutNodeType.CalculationPoint)
            {
                inputs.Add(PdfField("Calculation point", node.CalculationPointType));
                inputs.Add(PdfField("Observed duct", node.ObservedDuctSequence is int observed
                    ? $"D-{observed:000}" : "Unassigned"));
                inputs.Add(PdfField("Position along duct", $"{node.CalculationConnectionFraction * 100:0.#}%"));
            }
        }
        var pressure = SystemPressureForEquipmentPair(node);
        var outputs = new List<DuctSystemPdfField>
        {
            PdfField("Component pressure loss", node.IsDefined && (node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent)
                ? PdfPressure(node.PressureDropPa) : node.IsDefined ? "0" : "Not calculated"),
            PdfField("Static pressure in", node.IsDefined ? PdfPressure(pressure.Inlet) : "Not calculated"),
            PdfField("Static pressure out", node.IsDefined ? PdfPressure(pressure.Outlet) : "Not calculated")
        };
        if (node.IsDefined && node.Type == LayoutNodeType.CalculationPoint)
            outputs.Add(PdfField("Calculated value", node.CalculationPointType switch
            {
                "Temperature" => PdfTemperature(node.TemperatureC),
                "Velocity" => PdfVelocity(node.VelocityMPerS),
                "Airflow" => PdfAirflow(node.AirflowM3PerS),
                _ => PdfPressure(node.PressurePa)
            }));
        if (node.IsDefined && node.Type == LayoutNodeType.Grd && node.GrdSelectedAirflowCfm > 0)
        {
            outputs.Add(PdfField("Selected published airflow", $"{node.GrdSelectedAirflowCfm:0} cfm"));
            if (string.Equals(node.GrdType, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase))
                outputs.Add(PdfField("Automatically selected inlet", $"{node.GrdInletDiameterIn:0} in"));
            outputs.Add(PdfField("Sound", $"NC {node.GrdSoundNc}"));
            if (!string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase))
                outputs.Add(PdfField("Throw (150/100/50 fpm)", $"{node.GrdThrowFt} ft"));
        }
        if (node.IsDefined && node.Type == LayoutNodeType.Fan)
            outputs.Add(PdfField("Total static pressure", PdfPressure(node.FanTotalStaticPressurePa)));
        var reportType = node.Type == LayoutNodeType.CalculationPoint
            ? "CALCULATION POINT"
            : node.IsAirHandlingUnit ? "AIR HANDLING UNIT"
            : node.Type.ToString().ToUpperInvariant();
        var quickCheck = BuildEquipmentPdfQuickCheck(node, description, inputs, outputs);
        var equipmentCalculations = BuildEquipmentPdfCalculations(node, pressure);
        if (equipmentCalculations.Count > 0)
            equipmentCalculations = new[] { DuctSystemPdfCalculationSection.Title("COMPONENT SIZING CALCULATION") }
                .Concat(equipmentCalculations).ToArray();
        return new DuctSystemPdfReportItem(node.Id, description, reportType, inputs, outputs,
                equipmentCalculations, QuickCheck: quickCheck);
    }


    private static IReadOnlyList<DuctSystemPdfField> BuildEquipmentPdfQuickCheck(SavedLayoutNode node,
        string description, IReadOnlyList<DuctSystemPdfField> inputs, IReadOnlyList<DuctSystemPdfField> outputs)
    {
        string Input(string label, string fallback = "-") =>
            inputs.FirstOrDefault(field => field.Label == label)?.Value ?? fallback;
        string Output(string label, string fallback = "-") =>
            outputs.FirstOrDefault(field => field.Label == label)?.Value ?? fallback;

        if (!node.IsDefined)
            return new[]
            {
                PdfField("Component", description),
                PdfField("Definition status", Input("Definition status", "Undefined")),
                PdfField("Calculation status", "Not calculated")
            };

        return node.Type switch
        {
            LayoutNodeType.Fan => new[]
            {
                PdfField("Component", description), PdfField("Airflow", Input("Airflow")),
                PdfField("External static pressure", Input("External static pressure (ESP)")),
                PdfField("Internal static pressure", Input("Internal static pressure")),
                PdfField("Total static pressure", Output("Total static pressure")),
                PdfField("Fan efficiency", Input("Fan efficiency"))
            },
            LayoutNodeType.Reservoir => new[]
            {
                PdfField("Space role", Input("Space role")), PdfField("Airflow", Input("Airflow")),
                PdfField("Air temperature", Input("Air temperature")),
                PdfField("Static pressure", Output("Static pressure out"))
            },
            LayoutNodeType.Grd => new[]
            {
                PdfField("GRD type / name", $"{Input("GRD type")} - {description}"),
                PdfField("Airflow", Input("Airflow")),
                PdfField("Pressure drop", Output("Component pressure loss")),
                PdfField("Sound", Output("Sound", "-"))
            },
            LayoutNodeType.CalculationPoint => new[]
            {
                PdfField("Calculation point", Input("Calculation point")),
                PdfField("Calculated value", Output("Calculated value", "Not calculated")),
                PdfField("Observed duct", Input("Observed duct"))
            },
            _ => new[]
            {
                PdfField("Component", description), PdfField("Airflow", Input("Airflow")),
                PdfField("Air temperature", Input("Air temperature")),
                PdfField("Pressure drop", Output("Component pressure loss"))
            }
        };
    }

    private static IReadOnlyList<DuctSystemPdfCalculationSection> BuildDuctPdfCalculations(DuctCalculationResult result)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        static DuctSystemPdfCalculationLine Input(string label, string value) =>
            new(label, value, HighlightAsInput: true);
        static DuctSystemPdfCalculationLine E(string formula, string value = "", string substitution = "") =>
            new(formula, value, true, InputSubstitution: substitution);
        var geometry = result.Input.Geometry;
        var qCfm = UnitConversions.M3PerSToCfm(result.Input.AirflowM3PerS);
        var temperatureF = UnitConversions.CelsiusToFahrenheit(result.Input.TemperatureC);
        var areaFt2 = UnitConversions.SquareMetersToSquareFeet(geometry.ActualAreaM2);
        var equivalentAreaFt2 = UnitConversions.SquareMetersToSquareFeet(result.EquivalentAreaM2);
        var diameterFt = UnitConversions.MToFeet(result.CalculationDiameterM);
        var diameterIn = UnitConversions.MToInches(result.CalculationDiameterM);
        var velocityFpm = UnitConversions.MpsToFpm(result.CalculationVelocityMPerS);
        var actualVelocityFpm = UnitConversions.MpsToFpm(result.ActualVelocityMPerS);
        var velocityFtS = velocityFpm / 60.0;
        var density = result.Air.DensityKgPerM3 * 0.0624279606;
        var viscosity = result.Air.DynamicViscosityPaS * 0.671968975;
        var roughnessFt = result.Input.Material.RoughnessFeet;
        var relativeRoughness = roughnessFt / Math.Max(diameterFt, 1e-12);
        var factor = result.Friction.Factor ?? result.Friction.UpperFactor;
        var rate = UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM);
        var lengthFt = UnitConversions.MToFeet(result.Input.LengthM);
        var total = UnitConversions.PaToInchesWater(result.TotalPressureLossUpperPa);
        var velocityPressure = Math.Pow(velocityFpm / 4005.0, 2) * density / 0.075;
        var finalSize = geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToInches(geometry.DiameterM):0.###} in round"
            : $"{UnitConversions.MToInches(geometry.WidthM):0.###} x {UnitConversions.MToInches(geometry.HeightM):0.###} in";
        // The size substituted into step 1, with its symbols: "D = 14.5 in round" or "W = 24 in; H = 12 in".
        var sizeSubstitution = geometry.Shape == DuctShape.Round
            ? $"D = {finalSize}"
            : $"W = {UnitConversions.MToInches(geometry.WidthM):0.###} in; H = {UnitConversions.MToInches(geometry.HeightM):0.###} in";
        var geometryEquation = geometry.Shape == DuctShape.Round
            ? "A = \\frac{\\pi D^{2}}{4}"
            : "A = \\frac{W \\times H}{144}";
        var equivalentEquation = geometry.Shape == DuctShape.Round
            ? "D_{e} = D"
            : result.Input.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher
                ? "D_{e} = \\frac{1.30(WH)^{0.625}}{(W+H)^{0.25}}"
                : "D_{h} = \\frac{2WH}{W+H}";
        var frictionLines = new List<DuctSystemPdfCalculationLine>
        {
            L(result.Friction.Regime == FlowRegime.Laminar
                ? "Reynolds number identifies laminar flow, so the direct Darcy-factor correlation is used."
                : "Reynolds number identifies turbulent flow, so the implicit Colebrook equation is solved by safeguarded bisection."),
            E(result.Friction.Regime == FlowRegime.Laminar
                    ? "f = \\frac{64}{Re}"
                    : "\\frac{1}{\\sqrt{f}} = -2\\log_{10}(\\frac{\\epsilon_{r}}{3.7} + \\frac{2.51}{Re\\sqrt{f}})",
                $"{factor:0.000000}",
                result.Friction.Regime == FlowRegime.Laminar
                    ? $"Re = {result.ReynoldsNumber:0}"
                    : $"epsilon_r = {relativeRoughness:0.########}; Re = {result.ReynoldsNumber:0}")
        };
        if (result.Friction.Regime != FlowRegime.Laminar)
        {
            frictionLines.Add(L($"Solved by bisection between f = {result.Friction.InitialLowerBound:0.########} and {result.Friction.InitialUpperBound:0.########}: " +
                $"{result.Friction.Iterations} iterations, residual {result.Friction.FinalResidual:0.0E+00}, " +
                (result.Friction.Converged ? "converged." : "NOT converged - review.")));
        }
        return new[]
        {
            new DuctSystemPdfCalculationSection("INPUT VALUES", new[]
            {
                Input("Airflow Q", $"{qCfm:0.##} cfm"),
                Input("Air temperature T", $"{temperatureF:0.##} deg F"),
                Input("Relative humidity RH", $"{result.Input.RelativeHumidity:P0}"),
                Input("Atmospheric pressure p_atm", $"{result.Input.AtmosphericPressurePa * 0.000145037738:0.###} psia"),
                Input("Duct length L", $"{lengthFt:0.###} ft"),
                Input("Final duct geometry", finalSize),
                Input("Absolute roughness epsilon", $"{roughnessFt:0.######} ft"),
                Input("Pressure unit", $"1 in. w.c. = {UnitConversions.InchesWaterToPascals:0.#####} Pa (water at 39.2 deg F)")
            }),
            new DuctSystemPdfCalculationSection("1. FINAL DUCT GEOMETRY", new[]
            {
                L("Project sizing and rounding establish the constructible duct size before velocity and loss are evaluated."),
                E(geometryEquation, $"{areaFt2:0.####} ft2", sizeSubstitution),
                E(equivalentEquation, $"{diameterIn:0.###} in", sizeSubstitution),
                E("A_{calc} = \\frac{\\pi D_{calc}^{2}}{4}", $"{equivalentAreaFt2:0.####} ft2", $"Dcalc = {diameterIn:0.###} in")
            }),
            new DuctSystemPdfCalculationSection("2. AIR PROPERTIES", new[]
            {
                L("Moist-air density uses temperature, relative humidity, and atmospheric pressure. Viscosity uses the Sutherland relation."),
                E("\\rho = \\rho_{moist}(T, RH, p_{atm})", $"{density:0.#####} lbm/ft3",
                    $"T = {temperatureF:0.##} deg F; RH = {result.Input.RelativeHumidity:P0}; p_atm = {result.Input.AtmosphericPressurePa * 0.000145037738:0.###} psia"),
                E("\\mu = \\mu_{Sutherland}(T)", $"{viscosity:0.########} lbm/(ft-s)", $"T = {temperatureF:0.##} deg F")
            }),
            new DuctSystemPdfCalculationSection("3. VELOCITY AND FLOW REGIME", new[]
            {
                E("V_{actual} = \\frac{Q}{A_{actual}}", $"{actualVelocityFpm:0.#} fpm", $"Q = {qCfm:0.##} cfm; Aactual = {areaFt2:0.####} ft2"),
                E("V_{calc} = \\frac{Q}{A_{calc}}", $"{velocityFpm:0.#} fpm", $"Q = {qCfm:0.##} cfm; Acalc = {equivalentAreaFt2:0.####} ft2"),
                E("\\epsilon_{r} = \\frac{\\epsilon}{D_{calc}}", $"{relativeRoughness:0.########}", $"epsilon = {roughnessFt:0.######} ft; Dcalc = {diameterFt:0.####} ft"),
                E("Re = \\frac{\\rho V_{calc}D_{calc}}{\\mu}", $"{result.ReynoldsNumber:0}", $"rho = {density:0.#####}; V = {velocityFtS:0.####} ft/s; D = {diameterFt:0.####} ft; mu = {viscosity:0.########}"),
                L("Flow-regime conclusion", result.Friction.Regime.ToString())
            }),
            new DuctSystemPdfCalculationSection("4. DARCY FRICTION FACTOR", frictionLines),
            new DuctSystemPdfCalculationSection("5. VELOCITY PRESSURE AND DUCT LOSS", new[]
            {
                E("VP = (\\frac{V_{calc}}{4005})^{2}(\\frac{\\rho}{0.075})", $"{velocityPressure:0.####} in. w.c.", $"Vcalc = {velocityFpm:0.#} fpm; rho = {density:0.#####} lbm/ft3"),
                E("\\frac{\\Delta P}{100 ft} = f(\\frac{100 ft}{D_{calc}})VP", $"{rate:0.####} in. w.c./100 ft", $"f = {factor:0.000000}; Dcalc = {diameterFt:0.####} ft; VP = {velocityPressure:0.####} in. w.c."),
                E("\\Delta P = (\\frac{\\Delta P}{100 ft})(\\frac{L}{100 ft})", $"{total:0.####} in. w.c.", $"rate = {rate:0.####} in. w.c./100 ft; L = {lengthFt:0.###} ft")
            }),
            new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", DuctVariables(result)),
            new DuctSystemPdfCalculationSection("ASSUMPTIONS", new[]
            {
                L("Steady, fully developed airflow; Darcy friction factor; moist-air properties at the entered temperature, RH, and pressure."),
                L($"Calculation basis: {result.CalculationBasisDescription}")
            })
        };
    }

    /// <summary>Every symbol in the duct worksheet: what it is, its units and where its value comes from.</summary>
    private static IReadOnlyList<DuctSystemPdfCalculationLine> DuctVariables(DuctCalculationResult result)
    {
        static DuctSystemPdfCalculationLine V(string symbol, string meaning) => DuctSystemPdfCalculationLine.Variable(symbol, meaning);
        var round = result.Input.Geometry.Shape == DuctShape.Round;
        var huebscher = result.Input.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher;
        var lines = new List<DuctSystemPdfCalculationLine>
        {
            V("Q", "Airflow, cfm: input (the airflow carried by this duct in the system)."),
            V("T", "Air temperature, °F: input."),
            V("RH", "Relative humidity: project setting."),
            V("p_{atm}", "Atmospheric pressure, psia: project setting."),
            round
                ? V("D", "Duct diameter, in: the final (rounded, constructible) size, step 1.")
                : V("W, H", "Duct width and height, in: the final (rounded, constructible) size, step 1."),
            V("A, A_{actual}", "Inside area of the final duct, ft²: step 1."),
            round
                ? V("D_{e}", "Equivalent diameter, in: equal to D for a round duct, step 1.")
                : huebscher
                    ? V("D_{e}", "Equivalent diameter, in: Huebscher's equation (equal friction), step 1.")
                    : V("D_{h}", "Hydraulic diameter, in: 2WH / (W + H), step 1."),
            V("D_{calc}", "Diameter used for velocity and friction, in: the equivalent (or hydraulic) diameter, step 1."),
            V("A_{calc}", "Area of a circle of diameter Dcalc, ft²: step 1."),
            V("\\rho", "Moist-air density, lbm/ft³: step 2, from T, RH and patm."),
            V("\\mu", "Dynamic viscosity, lbm/(ft·s): step 2, Sutherland's law at T."),
            V("V_{actual}", "Velocity in the actual duct area, fpm: step 3."),
            V("V_{calc}", "Velocity used for the friction loss, fpm: step 3 (Q / Acalc)."),
            V("\\epsilon", $"Absolute roughness, ft: duct material ({result.Input.Material.Name})."),
            V("\\epsilon_{r}", "Relative roughness ε / Dcalc: step 3."),
            V("Re", "Reynolds number: step 3; decides laminar, transitional or turbulent flow."),
            V("f", result.Friction.Regime == FlowRegime.Laminar
                ? "Darcy friction factor: step 4, 64 / Re (laminar)."
                : "Darcy friction factor: step 4, solved from the Colebrook equation."),
            V("VP", "Velocity pressure, in. w.c.: step 5."),
            V("4005, 0.075", "Constants: velocity (fpm) of standard air at 1 in. w.c. VP; standard air density (lbm/ft³)."),
            V("\\Delta P/100 ft", "Friction rate, in. w.c. per 100 ft: step 5."),
            V("L", "Duct length, ft: input (from the drawing)."),
            V("\\Delta P", "Pressure loss of this duct, in. w.c.: step 5.")
        };
        return lines;
    }

    private string FormatSurfaceAreaWithUnit(ItemSurfaceArea area) => area.Problem is not null
        ? $"Not available - {area.Problem}"
        : _host._currentUnits == UnitSystem.Imperial ? $"{area.AreaFt2:0.##} ft2" : $"{area.AreaM2:0.###} m2";

    /// <summary>
    /// The secondary worksheet of a duct or fitting: its outer surface area (perimeter × length), and, when the
    /// system's SMACNA pressure class is known, the allowable leakage of this item. Worksheet values are in
    /// inch-pound units like the rest of the worksheet.
    /// </summary>
    internal static IReadOnlyList<DuctSystemPdfCalculationSection> BuildSurfaceAreaSections(ItemSurfaceArea area,
        SystemLeakageResult? system)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        static DuctSystemPdfCalculationLine E(string formula, string value = "", string substitution = "") =>
            new(formula, value, true, InputSubstitution: substitution);
        var sections = new List<DuctSystemPdfCalculationSection> { DuctSystemPdfCalculationSection.Title("SURFACE AREA CALCULATION") };
        var round = area.Shape == DuctShape.Round;
        var perimeterEquation = round ? "P = \\pi D" : "P = 2(W + H)";
        var lengthEquation = area.ArcAngleDegrees is not null ? "L_{eq} = \\frac{\\theta}{360} 2\\pi r" : area.Kind == "Fitting"
            ? round ? "L_{eq} = D" : "L_{eq} = max(W, H)"
            : "L = L_{duct}";
        var areaEquation = area.Kind == "Fitting" ? "A_{s} = P \\times L_{eq}" : "A_{s} = P \\times L";
        if (area.Problem is not null)
        {
            sections.Add(new DuctSystemPdfCalculationSection("SURFACE AREA", new[] { L($"Not calculated: {area.Problem}") }));
            return sections;
        }
        var diameterIn = UnitConversions.MToInches(area.DiameterM);
        var widthIn = UnitConversions.MToInches(area.WidthM);
        var heightIn = UnitConversions.MToInches(area.HeightM);
        var perimeterFt = UnitConversions.MToFeet(area.PerimeterM);
        var lengthFt = UnitConversions.MToFeet(area.LengthM);
        var sizeText = round ? $"D = {diameterIn:0.###} in = {diameterIn / 12:0.####} ft"
            : $"W = {widthIn:0.###} in = {widthIn / 12:0.####} ft; H = {heightIn:0.###} in = {heightIn / 12:0.####} ft";
        var work = new List<DuctSystemPdfCalculationLine>
        {
            E(perimeterEquation, $"{perimeterFt:0.####} ft", sizeText)
        };
        if (area.ArcAngleDegrees is double angle && area.CenterlineRadiusM is double radius)
        {
            var radiusFt = UnitConversions.MToFeet(radius);
            work.Add(E("r = (\\frac{r}{" + (round ? "D" : "W") + "})" + (round ? "D" : "W"), $"{radiusFt:0.####} ft",
                $"r/{(round ? "D" : "W")} = {area.RadiusRatio:0.###}; {(round ? $"D = {diameterIn / 12:0.####}" : $"W = {widthIn / 12:0.####}")} ft"));
            work.Add(E(lengthEquation, $"{lengthFt:0.####} ft", $"theta = {angle:0.#} deg; r = {radiusFt:0.####} ft"));
        }
        else if (area.Kind == "Fitting")
            work.Add(E(lengthEquation, $"{lengthFt:0.####} ft", round ? $"D = {diameterIn / 12:0.####} ft"
                : $"W = {widthIn / 12:0.####} ft; H = {heightIn / 12:0.####} ft"));
        work.Add(E(areaEquation, $"{area.AreaFt2:0.###} ft2", $"P = {perimeterFt:0.####} ft; L = {lengthFt:0.####} ft"));
        sections.Add(new DuctSystemPdfCalculationSection(area.Kind == "Fitting" ? "FITTING SURFACE AREA (ESTIMATE)" : "DUCT SURFACE AREA", work));

        if (system is { PressureClassInWc: double pressureClass, SealClass: SmacnaSealClass seal })
        {
            var leakageClass = SmacnaLeakage.LeakageClass(seal, area.Shape);
            var factor = SmacnaLeakage.AllowableLeakageFactor(leakageClass, pressureClass);
            sections.Add(new DuctSystemPdfCalculationSection("DUCT LEAKAGE OF THIS ITEM (SMACNA)", new[]
            {
                E("F = C_{L} P_{c}^{0.65}", $"{factor:0.###} cfm/100 ft2",
                    $"CL = {leakageClass} (Seal Class {seal}, {(round ? "round" : "rectangular")}); Pc = {pressureClass:0.#} in. w.c."),
                E("Q_{leak} = F \\frac{A_{s}}{100}", $"{factor * area.AreaFt2 / 100:0.###} cfm", $"F = {factor:0.###} cfm/100 ft2; As = {area.AreaFt2:0.###} ft2")
            }));
        }
        static DuctSystemPdfCalculationLine V(string symbol, string meaning) => DuctSystemPdfCalculationLine.Variable(symbol, meaning);
        var variables = new List<DuctSystemPdfCalculationLine>
        {
            V("P", "Perimeter of the section, ft: from the size above (πD or 2(W + H))."),
            area.Kind == "Fitting"
                ? V("L_{eq}", "Estimated length of the fitting, ft: " + area.LengthBasis + ".")
                : V("L", $"Duct length, ft: from the drawing ({lengthFt:0.###} ft).")
        };
        if (area.ArcAngleDegrees is not null)
            variables.Add(V("\\theta, r", "Elbow angle (degrees) and centerline radius (ft): from the fitting inputs."));
        variables.Add(V("A_{s}", "Outer surface area, ft²: P × length."));
        if (system is { PressureClassInWc: not null, SealClass: not null })
        {
            variables.Add(V("P_{c}", "System SMACNA pressure class, in. w.c.: from the system design pressure (Analysis tab)."));
            variables.Add(V("C_{L}", "SMACNA leakage class, cfm/100 ft² at 1 in. w.c.: from the seal class and duct shape."));
            variables.Add(V("F", "Leakage factor, cfm per 100 ft² of duct surface."));
            variables.Add(V("Q_{leak}", "Duct leakage of this item, cfm."));
        }
        sections.Add(new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", variables));
        sections.Add(new DuctSystemPdfCalculationSection("SURFACE AREA ASSUMPTIONS", new[]
        {
            L("Surface area: nominal (inside) dimensions, sheet-metal thickness neglected"
              + (area.Kind == "Fitting" ? "; fitting length estimated (ASHRAE gives none)." : "."))
        }));
        return sections;
    }

    private static IReadOnlyList<DuctSystemPdfCalculationSection> BuildFittingPdfCalculations(FittingCalculationResult result)
    {
        if (IsPublishedGrdCode(result.Definition.Code))
            return BuildGrdPdfCalculations(result, GrdSelectionFromResult(result));
        if (IsPublishedVavCode(result.Definition.Code))
            return BuildVavPdfCalculations(result, VavSelectionFromResult(result));
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        static DuctSystemPdfCalculationLine Input(string label, string value) =>
            new(label, value, HighlightAsInput: true);
        static DuctSystemPdfCalculationLine E(string formula, string value = "", string substitution = "") =>
            new(formula, value, true, InputSubstitution: substitution);
        static DuctSystemPdfCalculationLine V(string symbol, string meaning) => DuctSystemPdfCalculationLine.Variable(symbol, meaning);
        var sections = new List<DuctSystemPdfCalculationSection>
        {
            new("ASHRAE SOURCE AND SELECTION", new[]
            {
                L($"ASHRAE {result.Definition.Code}: {result.Definition.SourceNote}"),
                L($"Reference velocity basis: {result.Definition.ReferenceVelocity}. Coefficient: " + (result.Definition.IsAutomated
                    ? "read from the published table (see the table look-up)."
                    : "entered from the cited ASHRAE entry."))
            })
        };
        var tableLines = result.Definition.Inputs.Count == 0
            ? Array.Empty<DuctSystemPdfCalculationLine>()
            : result.Definition.Inputs.Select(spec =>
            {
                result.Inputs.TryGetValue(spec.Key, out var value);
                var choice = spec.Choices?.OrderBy(item => Math.Abs(item.Value - value)).FirstOrDefault();
                var selected = choice is not null && Math.Abs(choice.Value - value) < 1e-9
                    ? $"{choice.Label} ({value:0.####})"
                    : $"{value:0.####}{(string.IsNullOrWhiteSpace(spec.Unit) ? "" : " " + spec.Unit)}";
                return Input($"{spec.Label} [{spec.Key}], range {spec.Minimum:0.####} to {spec.Maximum:0.####}", selected);
            }).ToArray();
        if (tableLines.Length > 0)
            sections.Add(new DuctSystemPdfCalculationSection("PUBLISHED TABLE / EQUATION INPUTS", tableLines));

        var flow = new List<DuctSystemPdfCalculationLine>();
        if (result.AirflowM3PerS is double airflow)
            flow.Add(Input("Reference airflow Q", $"{UnitConversions.M3PerSToCfm(airflow):0.##} cfm"));
        flow.AddRange(FittingGeometryInputLines(result));
        if (result.ReferenceGeometry is { } geometry)
        {
            var geometryText = geometry.Shape == DuctShape.Round
                ? $"diameter {UnitConversions.MToInches(geometry.DiameterM):0.###} in"
                : $"{UnitConversions.MToInches(geometry.WidthM):0.###} x {UnitConversions.MToInches(geometry.HeightM):0.###} in";
            flow.Add(E("A_{ref} = A(geometry)", $"{UnitConversions.SquareMetersToSquareFeet(geometry.ActualAreaM2):0.####} ft2", geometryText));
            flow.Add(E("D_{h} = \\frac{4A_{ref}}{P_{wetted}}", $"{UnitConversions.MToInches(geometry.HydraulicDiameterM):0.###} in", geometryText));
            if (geometry.Shape == DuctShape.Rectangular)
                flow.Add(E("\\frac{W}{H}", $"{geometry.WidthM / geometry.HeightM:0.####}",
                    $"W = {UnitConversions.MToInches(geometry.WidthM):0.###} in; H = {UnitConversions.MToInches(geometry.HeightM):0.###} in"));
        }
        flow.Add(E("V_{ref} = \\frac{Q}{A_{ref}}", $"{UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS):0.#} fpm",
            $"Q = {(result.AirflowM3PerS is double q ? UnitConversions.M3PerSToCfm(q) : 0):0.##} cfm; Aref = {(result.ReferenceGeometry is { } g ? UnitConversions.SquareMetersToSquareFeet(g.ActualAreaM2) : 0):0.####} ft2"));
        sections.Add(new DuctSystemPdfCalculationSection("FLOW AND GEOMETRY", flow));

        var intermediates = new List<DuctSystemPdfCalculationLine>();
        var density = result.Air.DensityKgPerM3 * 0.0624279606;
        // Reynolds number only for a fitting whose coefficient depends on it (an "re" input, e.g. a K_Re correction).
        var usesReynolds = result.Inputs.Keys.Any(key => key.Equals("re", StringComparison.OrdinalIgnoreCase));
        if (usesReynolds && result.ReferenceGeometry is { } reference)
        {
            var velocityFtS = UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS) / 60.0;
            var dhFt = UnitConversions.MToFeet(reference.HydraulicDiameterM);
            var viscosity = result.Air.DynamicViscosityPaS * 0.671968975;
            var reynolds = density * velocityFtS * dhFt / viscosity;
            intermediates.Add(E("Re = \\frac{\\rho V D_{h}}{\\mu}", $"{reynolds:0}",
                $"rho = {density:0.#####}; V = {velocityFtS:0.####} ft/s; Dh = {dhFt:0.####} ft; mu = {viscosity:0.########}"));
        }
        // Derived ratios (junction legs: b branch, s straight, c common), two per row.
        var publishedKeys = result.Definition.Inputs.Select(spec => spec.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var derived = result.Inputs.Where(pair => !publishedKeys.Contains(pair.Key) && !pair.Key.Equals("path", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key).ToArray();
        for (var index = 0; index < derived.Length; index += 2)
        {
            var cells = new List<string> { FittingCalculatorScreen.FittingIntermediateSymbol(derived[index].Key), $"{derived[index].Value:0.####}" };
            if (index + 1 < derived.Length)
                cells.AddRange(new[] { FittingCalculatorScreen.FittingIntermediateSymbol(derived[index + 1].Key), $"{derived[index + 1].Value:0.####}" });
            else
                cells.AddRange(new[] { string.Empty, string.Empty });
            intermediates.Add(DuctSystemPdfCalculationLine.Row(DuctSystemPdfLineKind.TableRow, cells.ToArray()));
        }
        sections.Add(new DuctSystemPdfCalculationSection("INTERMEDIATE CALCULATIONS", intermediates.Count > 0 ? intermediates : new[] { L("No additional derived ratio is required for this fitting.") }));

        var lookupLines = WorksheetLookups.Lines(result);
        if (lookupLines.Count > 0)
            sections.Add(new DuctSystemPdfCalculationSection("TABLE LOOK-UP (HOW C WAS READ FROM THE TABLE)", lookupLines));
        else if (!result.Definition.IsAutomated)
            sections.Add(new DuctSystemPdfCalculationSection("COEFFICIENT SOURCE", new[]
            {
                L("The loss coefficient was entered from the cited ASHRAE entry; FanWorks does not estimate missing coefficients.")
            }));
        var velocityPressure = UnitConversions.PaToInchesWater(result.VelocityPressurePa);
        var coefficientBasis = lookupLines.Count == 0
            ? result.Definition.IsAutomated ? "fixed published coefficient" : "entered coefficient"
            : result.TableLookups.Count == 1 ? "table look-up above" : "table look-ups above, combined as the source note states";
        sections.Add(new DuctSystemPdfCalculationSection("COEFFICIENT AND PRESSURE LOSS", new[]
        {
            CoefficientLine(result, coefficientBasis),
            E("VP = (\\frac{V}{4005})^{2}(\\frac{\\rho}{0.075})", $"{velocityPressure:0.####} in. w.c.",
                $"V = {UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS):0.#} fpm; rho = {density:0.#####} lbm/ft3"),
            E("\\Delta P_{t} = C \\times VP", $"{UnitConversions.PaToInchesWater(result.PressureLossPa):0.####} in. w.c.",
                $"C = {result.Coefficient:0.####}; VP = {velocityPressure:0.####} in. w.c.")
        }));
        var fittingVariables = new List<DuctSystemPdfCalculationLine>
        {
            V("Q", "Reference airflow, cfm: the airflow through the fitting's reference section (from the connected system)."),
            V("A_{ref}", "Reference area, ft²: from the reference duct size listed above."),
            V("P_{wetted}", "Wetted perimeter of the reference section, in (πD round; 2(W + H) rectangular)."),
            V("D_{h}", "Hydraulic diameter, in: 4 Aref / Pwetted."),
            V("V_{ref}", $"Reference velocity, fpm: Q / Aref; basis: {result.Definition.ReferenceVelocity}."),
            V("\\rho", "Moist-air density, lbm/ft³: from the air temperature, relative humidity and atmospheric pressure."),
            V("\\mu", "Dynamic viscosity, lbm/(ft·s): Sutherland's law at the air temperature."),
            V("Re", "Reynolds number: ρ V Dh / μ (used by fittings whose coefficient depends on it)."),
            V("C", "ASHRAE loss coefficient referred to Vref: from the published table or equation for this fitting."),
            V("VP", "Velocity pressure, in. w.c.: (V / 4005)² (ρ / 0.075)."),
            V("4005, 0.075", "Constants: velocity (fpm) of standard air at 1 in. w.c. VP; standard air density (lbm/ft³)."),
            V("\\Delta P_{t}", "Total-pressure loss of the fitting, in. w.c.: C × VP.")
        };
        if (result.ReferenceGeometry is { Shape: DuctShape.Rectangular })
            fittingVariables.Insert(3, V("W, H", "Width and height of the rectangular reference section, in."));
        // D, W, H and R are shown as inputs (D = ... in round; R = R/D × D) at the top of the flow section.
        foreach (var factor in result.CoefficientFactors)
            fittingVariables.Insert(fittingVariables.FindIndex(line => line.Formula == "C") + 1, V(factor.Symbol, factor.Meaning));
        if (!usesReynolds)
            fittingVariables.RemoveAll(line => line.Formula is "Re" or "\\mu");
        if (derived.Length > 0)
            fittingVariables.Add(V("Q, A, V ratios", "Airflow, area and velocity ratios of the junction legs (b branch, s straight, c common): from the system airflows and duct sizes."));
        sections.Add(new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", fittingVariables));
        sections.Add(new DuctSystemPdfCalculationSection("ASSUMPTIONS", new[]
        {
            L("Steady airflow; ASHRAE reference-velocity convention; tables are not extrapolated; duct roughness does not enter C.")
        }));
        if (result.TableRangeWarnings.Count > 0)
        {
            // Reviewer flag: at least one lookup was held at a table edge instead of interpolated.
            sections.Add(new DuctSystemPdfCalculationSection("REVIEW REQUIRED - OUTSIDE PUBLISHED TABLE",
                result.TableRangeWarnings.Select(warning => L(warning)).ToArray()));
        }
        return sections;
    }

    /// <summary>The key of an elbow's centerline-radius ratio input (rd = R/D, rw = R/W), or null.</summary>
    private static string? RadiusRatioKey(FittingCalculationResult result)
    {
        if (result.ReferenceGeometry is not { } geometry)
            return null;
        if (geometry.Shape == DuctShape.Round && result.Inputs.ContainsKey("rd"))
            return "rd";
        return geometry.Shape == DuctShape.Rectangular && result.Inputs.ContainsKey("rw") ? "rw" : null;
    }

    /// <summary>
    /// The reference section's size as inputs (D, or W and H) and, for an elbow, its centerline radius R from the
    /// entered R/D or R/W, so a reader sees the actual dimensions behind the table ratios.
    /// </summary>
    internal static IReadOnlyList<DuctSystemPdfCalculationLine> FittingGeometryInputLines(FittingCalculationResult result)
    {
        var lines = new List<DuctSystemPdfCalculationLine>();
        if (result.ReferenceGeometry is not { } geometry)
            return lines;
        var round = geometry.Shape == DuctShape.Round;
        var diameter = UnitConversions.MToInches(geometry.DiameterM);
        var width = UnitConversions.MToInches(geometry.WidthM);
        var height = UnitConversions.MToInches(geometry.HeightM);
        if (round)
            lines.Add(new DuctSystemPdfCalculationLine("D =", $"{diameter:0.###} in round", IsEquation: true, HighlightAsInput: true));
        else
        {
            lines.Add(new DuctSystemPdfCalculationLine("W =", $"{width:0.###} in", IsEquation: true, HighlightAsInput: true));
            lines.Add(new DuctSystemPdfCalculationLine("H =", $"{height:0.###} in", IsEquation: true, HighlightAsInput: true));
        }
        if (RadiusRatioKey(result) is { } key)
        {
            var ratio = result.Inputs[key];
            var basis = round ? diameter : width;
            lines.Add(new DuctSystemPdfCalculationLine(round ? "R = \\frac{R}{D} \\times D" : "R = \\frac{R}{W} \\times W",
                $"{ratio * basis:0.###} in", true,
                InputSubstitution: round ? $"R/D = {ratio:0.###}; D = {diameter:0.###} in" : $"R/W = {ratio:0.###}; W = {width:0.###} in"));
        }
        return lines;
    }

    /// <summary>
    /// The coefficient line: for a coefficient that is a product of named factors, the combining equation with each
    /// factor substituted (for example C = Kθ × C'o = 0.66 × 0.185); otherwise C = C_ASHRAE with its basis.
    /// </summary>
    internal static DuctSystemPdfCalculationLine CoefficientLine(FittingCalculationResult result, string basis)
    {
        var factors = result.CoefficientFactors;
        if (factors.Count < 2)
            return new DuctSystemPdfCalculationLine("C = C_{ASHRAE}", $"{result.Coefficient:0.####}", true, InputSubstitution: basis);
        return new DuctSystemPdfCalculationLine("C = " + string.Join(" \\times ", factors.Select(factor => factor.Symbol)),
            $"{result.Coefficient:0.####}", true,
            InputSubstitution: string.Join("; ", factors.Select(factor =>
                $"{WorksheetLookups.Plain(factor.Symbol)} = {factor.Value.ToString("0.####", CultureInfo.InvariantCulture)}")));
    }

    private static bool IsPublishedGrdCode(string code) =>
        string.Equals(code, GrdPerformanceCatalog.Titus350RCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, GrdPerformanceCatalog.TitusTbd80Code, StringComparison.OrdinalIgnoreCase);

    private static bool IsPublishedVavCode(string code) =>
        string.Equals(code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, VavPerformanceCatalog.WithoutReheatCode, StringComparison.OrdinalIgnoreCase);

    private static VavPerformanceSelection VavSelectionFromResult(FittingCalculationResult result)
    {
        var cfm = result.AirflowM3PerS is double airflow ? UnitConversions.M3PerSToCfm(airflow) : 0;
        if (string.Equals(result.Definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase))
        {
            var size = VavPerformanceCatalog.WithReheatSizes[Convert.ToInt32(result.Inputs["sizeIndex"])];
            return VavPerformanceCatalog.SelectWithReheat(cfm, size, Convert.ToInt32(result.Inputs["rows"]));
        }
        var coolingSize = VavPerformanceCatalog.WithoutReheatSizes[Convert.ToInt32(result.Inputs["sizeIndex"])];
        return VavPerformanceCatalog.SelectWithoutReheat(cfm, coolingSize);
    }

    private static IReadOnlyList<DuctSystemPdfCalculationSection> BuildVavPdfCalculations(
        FittingCalculationResult result, VavPerformanceSelection selection)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        static DuctSystemPdfCalculationLine Input(string label, string value) =>
            new(label, value, HighlightAsInput: true);
        static DuctSystemPdfCalculationLine Solved(string label, string value, string substitution = "") =>
            new(label, value, IsEquation: true, InputSubstitution: substitution);

        var reheat = string.Equals(result.Definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase);
        var table = new List<DuctSystemPdfCalculationLine>
        {
            DuctSystemPdfCalculationLine.Row(DuctSystemPdfLineKind.TableHeader, "Published airflow (cfm)",
                reheat ? "Airside ΔPs (in. w.c.)" : "Min ΔPs (in. w.c.)", "Used")
        };
        table.AddRange(selection.PublishedTable.Select(point =>
        {
            var bound = point == selection.LowerPoint || point == selection.UpperPoint;
            return DuctSystemPdfCalculationLine.Row(bound ? DuctSystemPdfLineKind.TableRowSelected : DuctSystemPdfLineKind.TableRow,
                $"{point.AirflowCfm:0}", $"{point.PressureDropInWg:0.###}",
                !bound ? "" : selection.WasInterpolated ? (point == selection.LowerPoint ? "← Q1 (below)" : "← Q2 (above)") : "← exact");
        }));

        var interpolation = selection.WasInterpolated
            ? new[]
            {
                Input("Requested airflow Q", $"{selection.RequestedAirflowCfm:0.##} cfm"),
                Input("Lower published point", $"Q1 = {selection.LowerPoint.AirflowCfm:0} cfm; ΔP1 = {selection.LowerPoint.PressureDropInWg:0.###} in. w.c."),
                Input("Upper published point", $"Q2 = {selection.UpperPoint.AirflowCfm:0} cfm; ΔP2 = {selection.UpperPoint.PressureDropInWg:0.###} in. w.c."),
                Solved("\\Delta P = \\Delta P_{1} + \\frac{Q-Q_{1}}{Q_{2}-Q_{1}}(\\Delta P_{2}-\\Delta P_{1})",
                    $"{selection.PressureDropInWg:0.###} in. w.c.",
                    $"Q={selection.RequestedAirflowCfm:0.##}; Q1={selection.LowerPoint.AirflowCfm:0}; Q2={selection.UpperPoint.AirflowCfm:0}; ΔP1={selection.LowerPoint.PressureDropInWg:0.###}; ΔP2={selection.UpperPoint.PressureDropInWg:0.###}")
            }
            : new[]
            {
                Input("Requested airflow Q", $"{selection.RequestedAirflowCfm:0.##} cfm"),
                Solved("\\Delta P = \\Delta P_{published}(Q)", $"{selection.PressureDropInWg:0.###} in. w.c.",
                    "Requested airflow exactly matches a published point")
            };

        return new[]
        {
            new DuctSystemPdfCalculationSection("MANUFACTURER SOURCE AND CONFIGURATION", new[]
            {
                L(selection.Product), L(selection.Configuration), L(selection.SourceReference)
            }),
            new DuctSystemPdfCalculationSection("PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)", table, Wide: true),
            new DuctSystemPdfCalculationSection("INPUT / INTERPOLATION / SOLVED RESULT", interpolation),
            new DuctSystemPdfCalculationSection("PRESSURE-LOSS RESULT", new[]
            {
                Solved("\\Delta P_{VAV}", $"{UnitConversions.PaToInchesWater(result.PressureLossPa):0.###} in. w.c."),
                L(string.Equals(result.Definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
                    ? "Airside ΔPs is the selected hot-water coil air pressure drop."
                    : "Min ΔPs is the terminal inlet-to-outlet pressure drop with the primary damper full open."),
                L("Interpolation is restricted to the supplied published airflow range; no extrapolation is performed.")
            }),
            new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", new[]
            {
                DuctSystemPdfCalculationLine.Variable("Q", "Requested airflow, cfm: the airflow through this terminal from the system."),
                DuctSystemPdfCalculationLine.Variable("Q_{1}, Q_{2}", "Published airflows either side of Q, cfm: from the table above."),
                DuctSystemPdfCalculationLine.Variable("\\Delta P_{1}, \\Delta P_{2}", "Published pressure drops at Q1 and Q2, in. w.c."),
                DuctSystemPdfCalculationLine.Variable("\\Delta P", "Pressure drop at Q, in. w.c.: linear interpolation between the two published points.")
            })
        };
    }

    private static GrdPerformanceSelection GrdSelectionFromResult(FittingCalculationResult result)
    {
        var cfm = result.AirflowM3PerS is double airflow ? UnitConversions.M3PerSToCfm(airflow) : 0;
        return string.Equals(result.Definition.Code, GrdPerformanceCatalog.Titus350RCode, StringComparison.OrdinalIgnoreCase)
            ? GrdPerformanceCatalog.SelectTitus350R(cfm,
                GrdPerformanceCatalog.Titus350RSizes[Convert.ToInt32(result.Inputs["sizeIndex"])])
            : string.Equals(result.Definition.Code, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
                ? GrdPerformanceCatalog.SelectPriceScd(cfm, Convert.ToInt32(result.Inputs["face"]), Convert.ToInt32(result.Inputs["neck"]))
                : GrdPerformanceCatalog.SelectTitusTbd80(cfm, Convert.ToInt32(result.Inputs["length"]),
                    result.Inputs["slotWidth"], Convert.ToInt32(result.Inputs["slotCount"]));
    }

    /// <summary>The manufacturer's published table as worksheet table rows, with the row used highlighted.</summary>
    private static List<DuctSystemPdfCalculationLine> GrdTableLines(GrdPerformanceSelection selection, bool grille)
    {
        var table = new List<DuctSystemPdfCalculationLine>
        {
            grille
                ? DuctSystemPdfCalculationLine.Row(DuctSystemPdfLineKind.TableHeader, "Core velocity (fpm)", "Airflow (cfm)",
                    "Velocity pressure (in. w.c.)", "Static pressure (in. w.c.)", "Sound (NC)", "Used")
                : DuctSystemPdfCalculationLine.Row(DuctSystemPdfLineKind.TableHeader, "Airflow (cfm)", "Total pressure (in. w.c.)",
                    "Static pressure (in. w.c.)", "Sound (NC)", "Throw (ft to 150/100/50 fpm)", "Used")
        };
        table.AddRange(selection.PublishedTable.Select(point =>
        {
            var used = point == selection.Selected;
            var kind = used ? DuctSystemPdfLineKind.TableRowSelected : DuctSystemPdfLineKind.TableRow;
            return grille
                ? DuctSystemPdfCalculationLine.Row(kind, $"{point.CoreVelocityFpm:0}", $"{point.AirflowCfm:0}",
                    $"{point.VelocityPressureInWg:0.###}", $"-{point.TotalPressureInWg:0.###}", $"{point.SoundNc}", used ? "← used" : "")
                : DuctSystemPdfCalculationLine.Row(kind, $"{point.AirflowCfm:0}", $"{point.TotalPressureInWg:0.###}",
                    $"{(point.StaticPressureInWg ?? 0):0.###}", $"{point.SoundNc}", $"{point.ThrowFt}", used ? "← used" : "");
        }));
        return table;
    }

    /// <summary>States how the requested airflow was matched to the table (rounded up to the next published airflow).</summary>
    private static DuctSystemPdfCalculationLine GrdRoundingLine(GrdPerformanceSelection selection)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        return selection.AirflowWasRoundedUp
            ? L($"The requested {selection.RequestedAirflowCfm:0.##} cfm is not a published airflow. It was rounded UP to the next published point, " +
                $"{selection.Selected.AirflowCfm:0} cfm (conservative: the higher airflow has the higher pressure drop and sound level). No interpolation is used.")
            : L($"The requested {selection.RequestedAirflowCfm:0.##} cfm is a published airflow point; its values are used as printed.");
    }

    private static DuctSystemPdfCalculationLine[] GrdVariables(bool grille) => grille
                ? new[]
                {
                    DuctSystemPdfCalculationLine.Variable("Q", "Requested airflow, cfm: the airflow delivered to this grille by the system."),
                    DuctSystemPdfCalculationLine.Variable("Q_{pub}", "Published airflow used, cfm: the first table airflow at or above Q."),
                    DuctSystemPdfCalculationLine.Variable("V_{core}", "Core velocity, fpm: published, for the selected nominal duct size."),
                    DuctSystemPdfCalculationLine.Variable("VP", "Velocity pressure, in. w.c.: published."),
                    DuctSystemPdfCalculationLine.Variable("SP", "Static pressure, in. w.c.: published (negative); its magnitude is the pressure loss."),
                    DuctSystemPdfCalculationLine.Variable("NC", "Noise criterion: published sound rating at Q_pub.")
                }
                : new[]
                {
                    DuctSystemPdfCalculationLine.Variable("Q", "Requested airflow, cfm: the airflow delivered to this outlet by the system."),
                    DuctSystemPdfCalculationLine.Variable("Q_{pub}", "Published airflow used, cfm: the first table airflow at or above Q."),
                    DuctSystemPdfCalculationLine.Variable("TP", "Total pressure, in. w.c.: published; used as the pressure loss."),
                    DuctSystemPdfCalculationLine.Variable("SP", "Static pressure, in. w.c.: published."),
                    DuctSystemPdfCalculationLine.Variable("NC", "Noise criterion: published sound rating at Q_pub."),
                    DuctSystemPdfCalculationLine.Variable("Throw", "Distance, ft, to terminal velocities of 150, 100 and 50 fpm: published.")
                };

    private static IReadOnlyList<DuctSystemPdfCalculationSection> BuildGrdPdfCalculations(
        FittingCalculationResult result, GrdPerformanceSelection selection)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        static DuctSystemPdfCalculationLine Input(string label, string value) =>
            new(label, value, HighlightAsInput: true);
        static DuctSystemPdfCalculationLine Solved(string label, string value) =>
            new(label, value, IsEquation: true);
        var grille = string.Equals(result.Definition.Code, GrdPerformanceCatalog.Titus350RCode,
            StringComparison.OrdinalIgnoreCase);
        var table = GrdTableLines(selection, grille);
        var roundingLine = GrdRoundingLine(selection);
        var configurationInputs = grille
            ? new[] { Input("Nominal duct size", $"{selection.NominalDuctSizeIn} in") }
            : string.Equals(result.Definition.Code, GrdPerformanceCatalog.PriceScdCode,
                StringComparison.OrdinalIgnoreCase)
                ? new[]
            {
                Input("Face size", $"{result.Inputs["face"]:0} x {result.Inputs["face"]:0} in"),
                Input("Neck diameter", $"{result.Inputs["neck"]:0} in")
            }
                : new[]
            {
                Input("Length", $"{result.Inputs["length"]:0} ft"),
                Input("Slot size", $"{result.Inputs["slotWidth"]:0.##} in"),
                Input("Number of slots", $"{result.Inputs["slotCount"]:0}"),
                Solved("Automatically selected inlet", $"{selection.InletDiameterIn:0} in")
            };
        return new[]
        {
            new DuctSystemPdfCalculationSection("MANUFACTURER SOURCE AND METHOD", new[]
            {
                L(selection.Product), L(selection.Configuration),
                L("Selection method", grille
                    ? "Use the selected nominal duct-size chart and round requested airflow upward to the next published airflow point."
                    : selection.InletDiameterIn is null
                    ? "Round requested airflow upward to the first published airflow value."
                    : "Select the smallest published inlet covering the requested airflow, then round upward to the next listed cfm."),
                L("Test basis", "Manufacturer performance data; ASHRAE Standard 70 test method."),
                L("Reference", string.IsNullOrWhiteSpace(selection.SourceReference)
                    ? "Published manufacturer performance data." : selection.SourceReference)
            }),
            new DuctSystemPdfCalculationSection("INPUT PARAMETERS", configurationInputs.Prepend(
                Input("Requested airflow", $"{selection.RequestedAirflowCfm:0.##} cfm")).ToArray()),
            new DuctSystemPdfCalculationSection("PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)", table, Wide: true),
            new DuctSystemPdfCalculationSection("SELECTION FROM THE TABLE", new[] { roundingLine }),
            new DuctSystemPdfCalculationSection("SELECTED OUTPUTS", new[]
            {
                Solved("Published airflow", $"{selection.Selected.AirflowCfm:0} cfm"),
                Solved(grille ? "Negative static pressure / pressure drop" : "Total pressure drop",
                    $"{selection.Selected.TotalPressureInWg:0.###} in. w.c."),
                Solved(grille ? "Core velocity" : "Reference velocity",
                    grille ? $"{selection.Selected.CoreVelocityFpm:0} fpm" : $"{UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS):0} fpm"),
                Solved("Sound", $"NC {selection.Selected.SoundNc}"),
                Solved("Throw", grille ? "Not published for 350R" : $"{selection.Selected.ThrowFt} ft to 150/100/50 fpm")
            }),
            new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", GrdVariables(grille)),
            new DuctSystemPdfCalculationSection("NOTES", new[]
            {
                L(grille
                    ? "Titus 350R source static pressures are negative; their positive magnitude is used as system pressure loss. Source NC dashes are reported as NC <10. Performance data courtesy of Titus."
                    : selection.Product.StartsWith("Price", StringComparison.OrdinalIgnoreCase)
                    ? "Price SCD source dashes are reported as NC <15. Throw applies to a ceiling-mounted diffuser; exposed-duct throw uses the manufacturer's 0.70 factor."
                    : "Titus TBD-80 D source dashes are reported as NC <10. Throw is the one-way pattern value listed by the manufacturer. Performance data courtesy of Titus.")
            })
        };
    }

    private static IReadOnlyList<DuctSystemPdfCalculationSection> BuildEquipmentPdfCalculations(
        SavedLayoutNode node, (double Inlet, double Outlet) pressure)
    {
        static DuctSystemPdfCalculationLine L(string formula, string value = "") => new(formula, value);
        var inlet = UnitConversions.PaToInchesWater(pressure.Inlet);
        var outlet = UnitConversions.PaToInchesWater(pressure.Outlet);
        if (!node.IsDefined)
            return new[] { new DuctSystemPdfCalculationSection("CALCULATION STATUS", new[] { L("Component is undefined; no engineering calculation has been applied.") }) };
        static DuctSystemPdfCalculationLine Given(string symbol, string value) =>
            new(symbol + " =", value, IsEquation: true, HighlightAsInput: true);
        static DuctSystemPdfCalculationLine E(string formula, string value, string substitution) =>
            new(formula, value, true, InputSubstitution: substitution);
        static string W(double inWc) => $"{inWc:0.####} in. w.c.";
        if (node.Type == LayoutNodeType.Fan)
        {
            // A fan adds its static pressure to the duct system. An air handling unit adds its external static
            // pressure (ESP); its fan also develops the internal static pressure (ISP) used inside the unit, so the
            // motor power is based on the total TSP = ESP + ISP.
            var airHandlingUnit = node.IsAirHandlingUnit;
            var airflowCfm = UnitConversions.M3PerSToCfm(node.AirflowM3PerS);
            var external = UnitConversions.PaToInchesWater(node.FanStaticPressurePa);
            var internalStatic = UnitConversions.PaToInchesWater(node.FanInternalStaticPressurePa);
            var total = UnitConversions.PaToInchesWater(node.FanTotalStaticPressurePa);
            var efficiency = node.FanEfficiencyPercent / 100.0;
            var horsepower = TryBrakeHorsepower(node.AirflowM3PerS, node.FanTotalStaticPressurePa, node.FanEfficiencyPercent);
            var horsepowerText = double.IsFinite(horsepower) ? $"{horsepower:0.####} hp" : FormatHorsepower(horsepower);
            // The same template for a fan and an air handling unit (a bare fan's ISP is normally 0).
            var pressureLines = new[]
            {
                Given("ESP", W(external)),
                Given("ISP", W(internalStatic)),
                E("TSP = ESP + ISP", W(total), $"ESP = {external:0.####}; ISP = {internalStatic:0.####} in. w.c."),
                E("SP_{out} = SP_{in} + ESP", W(outlet), $"SPin = {inlet:0.####}; ESP = {external:0.####} in. w.c."),
                L("The duct system sees only the external static pressure; the internal static pressure is used inside the unit.")
            };
            var power = new[]
            {
                E("BHP = \\frac{Q \\times TSP}{6356 \\eta}", horsepowerText,
                    $"Q = {airflowCfm:0.##} cfm; TSP = {total:0.####} in. w.c.; η = {efficiency:0.####}"),
                L("Positive fan pressure is a source term and is excluded from the reported critical-path total pressure loss.")
            };
            var variables = new List<DuctSystemPdfCalculationLine>
            {
                DuctSystemPdfCalculationLine.Variable("ESP", "External static pressure, in. w.c.: the pressure available to the ductwork outside the unit. Auto-size sets it to the critical-path pressure loss."),
                DuctSystemPdfCalculationLine.Variable("ISP", airHandlingUnit
                    ? "Internal static pressure, in. w.c.: drop across the unit's filters, coils and other internal components; entered from the manufacturer's data."
                    : "Internal static pressure, in. w.c.: drop inside the fan's own housing or accessories; entered (normally 0 for a bare fan)."),
                DuctSystemPdfCalculationLine.Variable("TSP", "Total static pressure, in. w.c.: what the fan must develop."),
                DuctSystemPdfCalculationLine.Variable("SP_{in}, SP_{out}", "Static pressure entering and leaving, in. w.c.: from the system calculation."),
                DuctSystemPdfCalculationLine.Variable("Q", "Airflow through the fan, cfm."),
                DuctSystemPdfCalculationLine.Variable("\\eta", "Fan efficiency, entered (as a fraction)."),
                DuctSystemPdfCalculationLine.Variable("6356", "Unit constant: cfm x in. w.c. per horsepower (ASHRAE Handbook, Fundamentals, Fans).")
            };
            return new[]
            {
                new DuctSystemPdfCalculationSection(airHandlingUnit ? "AIR HANDLING UNIT STATIC PRESSURE" : "FAN STATIC PRESSURE", pressureLines),
                new DuctSystemPdfCalculationSection("MOTOR POWER", power),
                new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", variables)
            };
        }
        if (node.Type == LayoutNodeType.Grd
            && (string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.GrdType, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase)))
        {
            var requestedCfm = UnitConversions.M3PerSToCfm(node.AirflowM3PerS);
            var grille = string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase);
            var selection = grille
                ? GrdPerformanceCatalog.SelectTitus350R(requestedCfm, node.GrdNominalDuctSizeIn)
                : string.Equals(node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase)
                    ? GrdPerformanceCatalog.SelectPriceScd(requestedCfm, Convert.ToInt32(node.GrdFaceSizeIn), Convert.ToInt32(node.GrdNeckDiameterIn))
                    : GrdPerformanceCatalog.SelectTitusTbd80(requestedCfm, Convert.ToInt32(node.GrdLengthFt),
                        node.GrdSlotWidthIn, node.GrdSlotCount);
            var method = grille
                ? "The selected nominal duct-size chart is used and the airflow is rounded upward to the next published point."
                : selection.InletDiameterIn is null
                    ? "The requested airflow is rounded upward to the first published airflow at or above the request."
                    : "The smallest published inlet covering the requested airflow is selected, then the airflow is rounded upward to the next listed cfm.";
            var configuration = new List<DuctSystemPdfCalculationLine>
            {
                L("Requested airflow Q", $"{requestedCfm:0.##} cfm")
            };
            if (grille)
                configuration.Add(L("Nominal duct size", $"{selection.NominalDuctSizeIn} in"));
            else if (selection.InletDiameterIn is int inletDiameter)
                configuration.Add(L("Selected inlet", $"{inletDiameter:0} in"));
            var pressureLoss = selection.Selected.TotalPressureInWg;
            // The system uses the pressure drop stored on the outlet when it was last defined or edited. If the airflow has
            // changed since, the stored value no longer matches the table at the current airflow: say so, never hide it.
            var storedLoss = UnitConversions.PaToInchesWater(node.PressureDropPa);
            var stale = Math.Abs(storedLoss - pressureLoss) > 0.0005;
            var review = stale
                ? new[]
                {
                    new DuctSystemPdfCalculationSection("REVIEW REQUIRED - STORED PRESSURE DROP IS OUT OF DATE", new[]
                    {
                        L($"The system calculation used {storedLoss:0.###} in. w.c., stored when this outlet was last defined, but the published " +
                          $"value at the current {requestedCfm:0.##} cfm is {pressureLoss:0.###} in. w.c. Open the outlet and save it to update the stored value.")
                    })
                }
                : Array.Empty<DuctSystemPdfCalculationSection>();
            return review.Concat(new[]
            {
                new DuctSystemPdfCalculationSection("MANUFACTURER SOURCE AND METHOD", new[]
                {
                    L(selection.Product), L(selection.Configuration), L($"Selection method: {method}"),
                    L($"Reference: {(string.IsNullOrWhiteSpace(selection.SourceReference) ? "Published manufacturer performance data." : selection.SourceReference)}")
                }),
                new DuctSystemPdfCalculationSection("INPUT PARAMETERS", configuration),
                new DuctSystemPdfCalculationSection("PUBLISHED PERFORMANCE TABLE (MANUFACTURER DATA)", GrdTableLines(selection, grille), Wide: true),
                new DuctSystemPdfCalculationSection("SELECTION FROM THE TABLE", new[] { GrdRoundingLine(selection) }),
                new DuctSystemPdfCalculationSection("SELECTED PERFORMANCE AND PRESSURE", new[]
                {
                    L("Published airflow used Q_pub", $"{selection.Selected.AirflowCfm:0} cfm"),
                    L(grille ? "Pressure loss |SP| at Q_pub" : "Pressure loss TP at Q_pub", $"{pressureLoss:0.###} in. w.c."),
                    L(grille ? "Core velocity" : "Throw (ft to 150/100/50 fpm)", grille ? $"{selection.Selected.CoreVelocityFpm:0} fpm" : $"{selection.Selected.ThrowFt}"),
                    L("Sound", $"NC {selection.Selected.SoundNc}"),
                    new DuctSystemPdfCalculationLine("SP_{out} = SP_{in} - \\Delta P", $"{outlet:0.####} in. w.c.", true,
                        InputSubstitution: $"SPin = {inlet:0.####} in. w.c.; ΔP = {storedLoss:0.####} in. w.c. (used by the system)")
                }),
                new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", GrdVariables(grille)
                    .Append(DuctSystemPdfCalculationLine.Variable("SP_{in}, SP_{out}", "Static pressure entering and leaving the outlet, in. w.c.: from the system calculation."))
                    .Append(DuctSystemPdfCalculationLine.Variable("\\Delta P", "Pressure drop of the outlet used by the system, in. w.c.: stored from the table when the outlet was defined."))
                    .ToArray())
            }).ToArray();
        }
        if (node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent)
        {
            var drop = UnitConversions.PaToInchesWater(node.PressureDropPa);
            return new[]
            {
                new DuctSystemPdfCalculationSection("COMPONENT PRESSURE LOSS", new[]
                {
                    Given("\\Delta P", W(drop)),
                    E("SP_{out} = SP_{in} - \\Delta P", W(outlet), $"SPin = {inlet:0.####}; ΔP = {drop:0.####} in. w.c.")
                }),
                new DuctSystemPdfCalculationSection("VARIABLES AND WHERE THEY COME FROM", new[]
                {
                    DuctSystemPdfCalculationLine.Variable("\\Delta P", "Pressure drop across the component, in. w.c.: entered (from the manufacturer's data or the design)."),
                    DuctSystemPdfCalculationLine.Variable("SP_{in}, SP_{out}", "Static pressure entering and leaving, in. w.c.: from the system calculation.")
                })
            };
        }
        if (node.Type == LayoutNodeType.CalculationPoint)
            return new[] { new DuctSystemPdfCalculationSection("CALCULATION POINT", new[]
            {
                L("The point samples its assigned duct without splitting the duct or adding pressure loss."),
                L("Position along observed duct", $"{node.CalculationConnectionFraction * 100:0.#}%"),
                L("Calculated point value", node.CalculationPointType switch
                {
                    "Temperature" => $"{UnitConversions.CelsiusToFahrenheit(node.TemperatureC):0.##} degF",
                    "Velocity" => $"{UnitConversions.MpsToFpm(node.VelocityMPerS):0.#} fpm",
                    "Airflow" => $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):0.##} cfm",
                    _ => $"{UnitConversions.PaToInchesWater(node.PressurePa):0.####} in. w.c."
                })
            }) };
        return new[] { new DuctSystemPdfCalculationSection("FLOW CONTINUITY AND PRESSURE", new[]
        {
            L("Component airflow", $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):0.##} cfm"),
            L("No component pressure drop is assigned."),
            E("SP_{out} = SP_{in}", W(outlet), $"SPin = {inlet:0.####} in. w.c.")
        }) };
    }

    private IReadOnlyList<DuctSystemPdfReportItem> OrderPdfReportItems(
        IReadOnlyList<(string Key, DuctSystemPdfReportItem Item)> availableItems)
    {
        var byKey = availableItems
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<DuctSystemPdfReportItem>(byKey.Count);
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodes = _ductSystem.LayoutNodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var roots = nodes.Values
            .Where(node => node.Type != LayoutNodeType.CalculationPoint
                           && (string.IsNullOrWhiteSpace(node.ParentNodeId) || !nodes.ContainsKey(node.ParentNodeId))
                           && (string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) || !nodes.ContainsKey(node.SecondaryParentNodeId)))
            .OrderBy(SourceToSinkRootPriority)
            .ThenBy(node => node.Sequence)
            .ToArray();

        foreach (var root in roots)
            Visit(root);

        foreach (var entry in availableItems)
            Emit(entry.Key);
        return ordered;

        void Visit(SavedLayoutNode node)
        {
            if (!UpstreamParentsVisited(node))
                return;
            if (!visitedNodes.Add(node.Id))
                return;
            var nodeKey = node.LinkedFittingSequence is int fittingSequence
                ? FittingKey(fittingSequence)
                : node.LinkedDuctSequence is int ductSequence
                    ? DuctKey(ductSequence)
                    : LayoutKey(node.Sequence);
            Emit(nodeKey);

            var children = nodes.Values
                .Where(child => string.Equals(child.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(child => string.Equals(child.BranchId, node.BranchId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(child => child.Sequence)
                .ToArray();
            foreach (var child in children)
            {
                var secondary = string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
                var connectionSequence = secondary
                    ? child.SecondaryConnectionDuctSequence
                    : child.ConnectionDuctSequence;
                if (connectionSequence is int connectionDuctSequence)
                    Emit(DuctKey(connectionDuctSequence));
                foreach (var point in CalculationPointsForConnection(child, secondary))
                    Emit(LayoutKey(point.Sequence));
                Visit(child);
            }
        }

        bool UpstreamParentsVisited(SavedLayoutNode node)
        {
            return ParentVisited(node.ParentNodeId) && ParentVisited(node.SecondaryParentNodeId);
        }

        bool ParentVisited(string parentId)
        {
            return string.IsNullOrWhiteSpace(parentId)
                   || !nodes.ContainsKey(parentId)
                   || visitedNodes.Contains(parentId);
        }

        static int SourceToSinkRootPriority(SavedLayoutNode node)
        {
            if (node.Type == LayoutNodeType.Reservoir
                && node.SpaceRole.Contains("Source", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (node.Type == LayoutNodeType.Fan)
                return 1;
            return 2;
        }

        void Emit(string key)
        {
            if (!emitted.Add(key) || !byKey.TryGetValue(key, out var item))
                return;
            ordered.Add(item);
        }
    }

    private static DuctSystemPdfField PdfField(string label, string value) => new(label, string.IsNullOrWhiteSpace(value) ? "-" : value);

    private string PdfAirflow(double value) => $"{FormatSystemAirflow(value)} {(_host._currentUnits == UnitSystem.Imperial ? "cfm" : "L/s")}";
    private string PdfTemperature(double value) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.CelsiusToFahrenheit(value):0.##} degF" : $"{value:0.##} degC";
    private string PdfLength(double value) => $"{FormatSystemLength(value)} {(_host._currentUnits == UnitSystem.Imperial ? "ft" : "m")}";
    private string PdfDelta(double value) => $"{FormatSystemDelta(value)} {(_host._currentUnits == UnitSystem.Imperial ? "ft" : "m")}";
    private string PdfArea(double value) => $"{FormatSystemArea(value)} {(_host._currentUnits == UnitSystem.Imperial ? "ft2" : "m2")}";
    private string PdfVelocity(double value) => $"{FormatSystemVelocity(value)} {(_host._currentUnits == UnitSystem.Imperial ? "fpm" : "m/s")}";
    private string PdfDiameter(double value) => _host._currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToInches(value):0.###} in" : $"{UnitConversions.MToMillimeters(value):0.##} mm";
    private string PdfPressure(double value) => !double.IsFinite(value)
        ? "Not connected"
        : _host._currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.PaToInchesWater(value):0.####} in. w.c."
            : $"{value:0.##} Pa";
    private string PdfPressureRate(DuctCalculationResult result) => $"{FormatSystemPressureRate(result)} {(_host._currentUnits == UnitSystem.Imperial ? "in. w.c./100 ft" : "Pa/m")}";
    private string PdfRoughness(double feet) => _host._currentUnits == UnitSystem.Imperial
        ? $"{feet:0.######} ft" : $"{UnitConversions.FeetToM(feet) * 1000:0.###} mm";
    private static string PdfFriction(FrictionFactorResult friction) => friction.Factor is double factor
        ? $"{factor:0.######}" : $"{friction.LowerFactor:0.######} to {friction.UpperFactor:0.######}";
    private string SafeDuctSize(SavedDuct duct)
    {
        try { return FormatSystemGeometry(duct.CreateGeometry()); }
        catch (ArgumentException) { return "Invalid entered size"; }
    }
    private string SafeFittingSize(SavedFitting fitting)
    {
        try { return fitting.CreateGeometry() is DuctGeometry geometry ? FormatSystemGeometry(geometry) : "-"; }
        catch (ArgumentException) { return "Invalid entered size"; }
    }
    private static string LayoutReportDescription(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.Reservoir => node.SpaceRole,
        LayoutNodeType.Fan => node.FanEquipmentName,
        LayoutNodeType.Coil => "Cooling / Heating Coil",
        LayoutNodeType.Filter => "Air Filter",
        LayoutNodeType.Grd => node.GrdType,
        LayoutNodeType.CustomComponent => node.Description,
        LayoutNodeType.CalculationPoint => $"{node.CalculationPointType} Calculation Point",
        LayoutNodeType.Fitting => $"Undefined {node.FittingGroup} fitting",
        _ => node.Type.ToString()
    };

    /// <summary>
    /// Opens a saved .fanworks project (the full model: layout, connections, fan,
    /// settings). A CSV schedule can also be chosen; it carries ducts and fittings only,
    /// so the user is told that layout and connections will not be restored.
    /// </summary>
    internal void OpenProject()
    {
        var path = AutomationOptions.OpenProjectPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open a FanWorks project",
                Filter = $"FanWorks project (*{ProjectFile.Extension})|*{ProjectFile.Extension}"
                         + "|FanWorks CSV schedule - ducts and fittings only (*.csv)|*.csv",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(_host) != DialogResult.OK)
                return;
            path = dialog.FileName;
        }
        if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ImportDuctSystem(path);
            return;
        }

        DuctSystemDocument opened;
        try
        {
            opened = ProjectFile.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or ArgumentException or NotSupportedException
                                       or System.Text.Json.JsonException)
        {
            DiagnosticLog.Error($"Opening project {path} failed.", ex);
            _systemStorageStatusLabel.Text = $"The project could not be opened: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host,
                    $"The project could not be opened.\r\n\r\n{path}\r\n\r\n{ex.Message}\r\n\r\nThe current project was left unchanged.",
                    "FanWorks Open Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!ConfirmReplaceCurrentSystem($"Open {Path.GetFileName(path)}?"))
            return;
        ReplaceCurrentSystem(opened);
        _lastExportBasePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty,
            Path.GetFileNameWithoutExtension(path));
        DiagnosticLog.Info($"Project opened from {path}.");
        _systemStorageStatusLabel.Text = $"Opened {Path.GetFileName(path)}. Save writes back to this project.";
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    internal bool ConfirmReplaceCurrentSystem(string question)
    {
        var existingCount = _ductSystem.Ducts.Count + _ductSystem.Fittings.Count + _ductSystem.LayoutNodes.Count;
        if (existingCount == 0 || AutomationOptions.SuppressDialogs)
            return true;
        return MessageBox.Show(_host,
                   $"{question}\r\n\r\nThe current system ({existingCount} item(s)) will be closed. "
                   + "Anything not saved with Save will be lost.",
                   "Replace Current System", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                   MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    /// <summary>Makes <paramref name="document"/> the working system and resets all editing state.</summary>
    internal void ReplaceCurrentSystem(DuctSystemDocument document)
    {
        _host.LayoutScreen.Inspector?.ShowNothing();
        _ductSystem = document;
        _lastExportBasePath = string.Empty;
        ResetCriticalPath();
        _host.LayoutScreen.ClearLayoutHistory();
        _editingDuctSequence = null;
        _editingFittingSequence = null;
        _addDuctButton.Text = DefaultDuctActionText;
        _host.FittingScreen._addFittingButton.Text = DefaultFittingActionText;
        _addDuctButton.Visible = false;
        _host.FittingScreen._addFittingButton.Visible = false;
        _host.CalculatorScreen._ductEditingLabel.Visible = false;
        _host.FittingScreen._fittingEditingLabel.Visible = false;
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _host.LayoutScreen.RefreshLayoutDesigner();
        ApplyProjectCalculationSettings();
    }

    private void ImportDuctSystem(string path)
    {
        try
        {
            var imported = DuctSystemCsv.Import(path);
            var count = imported.Ducts.Count + imported.Fittings.Count;
            if (!AutomationOptions.SuppressDialogs && MessageBox.Show(_host,
                    $"{Path.GetFileName(path)} is a CSV schedule. It contains {count} duct and fitting item(s) only: "
                    + "the graphical layout, connections, fan, and equipment are not stored in CSV and will not be restored."
                    + $"\r\n\r\nTo reopen a complete project, choose its {ProjectFile.Extension} file instead.\r\n\r\nImport the CSV anyway?",
                    "Import CSV Schedule", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            if (!ConfirmReplaceCurrentSystem($"Import {count} item(s) from {Path.GetFileName(path)}?"))
                return;
            imported.UserName = _ductSystem.UserName;
            imported.ProjectName = _ductSystem.ProjectName;
            imported.ProjectNumber = _ductSystem.ProjectNumber;
            imported.ProjectDate = _ductSystem.ProjectDate;
            imported.LastEditedUtc = _ductSystem.LastEditedUtc;
            imported.CompanyLogoPngBase64 = _ductSystem.CompanyLogoPngBase64;
            imported.DefaultSizingIncrementM = _ductSystem.DefaultSizingIncrementM;
            imported.DefaultSizeRoundingDirection = _ductSystem.DefaultSizeRoundingDirection;
            imported.DefaultEquivalentDiameterMethod = _ductSystem.DefaultEquivalentDiameterMethod;
            imported.DefaultPressureLossBasis = _ductSystem.DefaultPressureLossBasis;
            ReplaceCurrentSystem(imported);
            DiagnosticLog.Info($"CSV schedule imported from {path} ({count} items).");
            _systemStorageStatusLabel.Text = $"Imported {count} item(s) from {Path.GetFileName(path)}. Layout and connections were not in the CSV.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                   or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            DiagnosticLog.Error($"CSV import from {path} failed.", ex);
            _systemStorageStatusLabel.Text = $"The CSV could not be imported: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
        }
    }

    private static string Cell(DataGridViewRow row, string columnName) =>
        Convert.ToString(row.Cells[columnName].Value, CultureInfo.CurrentCulture) ?? string.Empty;
}
