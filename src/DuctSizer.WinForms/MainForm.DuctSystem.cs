using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

public sealed partial class MainForm
{
    private readonly PolishedTabControl _mainTabs = new();
    private readonly Button _calculatorNavigationButton = new();
    private readonly Button _fittingNavigationButton = new();
    private readonly Button _systemNavigationButton = new();
    private readonly Button _layoutNavigationButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _settingsButton = new();
    private readonly PictureBox _projectHeaderAppIcon = new();
    private readonly Label _projectHeaderBrand = new();
    private readonly Label _projectHeaderLabel = new();
    private readonly PictureBox _projectHeaderLogo = new();
    private readonly ContextMenuStrip _settingsMenu = new();
    private string _lastExportBasePath = string.Empty;
    private ToolStripMenuItem? _imperialUnitsMenuItem;
    private ToolStripMenuItem? _metricUnitsMenuItem;
    private readonly TabPage _calculatorTab = new("Duct Calculator");
    private readonly TabPage _fittingTab = new("Fitting Calculator");
    private readonly TabPage _systemTab = new("Duct System");
    private readonly TabPage _layoutTab = new("Layout Designer");
    private readonly DataGridView _ductGrid = new();
    private readonly ContextMenuStrip _ductGridContextMenu = new();
    private readonly ToolStripMenuItem _editSystemItemMenuItem = new("Edit item")
        { Name = "DuctGridEditItemMenuItem" };
    private readonly ToolStripMenuItem _removeSystemItemMenuItem = new("Remove item")
        { Name = "DuctGridRemoveItemMenuItem" };
    private readonly Label _systemTotalLabel = new();
    private readonly Label _systemCriticalLabel = new();
    private readonly Label _systemCountLabel = new();
    private readonly Label _systemStorageStatusLabel = new();
    private readonly Button _addDuctButton = new();
    private readonly Button _deleteAllSystemButton = new();
    private readonly Button _exportSystemButton = new();
    private readonly Button _importSystemButton = new();
    private readonly Button _autoSizeFanButton = new();
    private readonly Button _viewFanInformationButton = new();
    private readonly Button _criticalPathFilterButton = new();
    private DuctSystemDocument _ductSystem = new();
    private DuctCalculationResult? _lastCalculation;
    private int? _editingDuctSequence;
    private int? _editingFittingSequence;
    private const string DefaultDuctActionText = "Save Duct Changes";
    private const string DefaultFittingActionText = "Save Fitting Changes";
    private bool _criticalPathCalculated;
    private bool _criticalPathFilterEnabled;
    private HashSet<string> _criticalPathItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private double _criticalPathPressureLossPa;
    private string _criticalPathRoute = string.Empty;
    private IReadOnlyList<SystemCalculationIssue> _criticalPathIssues = Array.Empty<SystemCalculationIssue>();
    private IReadOnlyList<SystemCalculationIssue> _criticalPathAssumptions = Array.Empty<SystemCalculationIssue>();
    private sealed record SystemRowTag(bool IsFitting, int Sequence);

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

    private void InitializeTabsAndSystem(Control calculatorRoot)
    {
        _mainTabs.Name = "MainTabs";
        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.Margin = Padding.Empty;

        calculatorRoot.Dock = DockStyle.Fill;
        _calculatorTab.Name = "CalculatorTab";
        _calculatorTab.Padding = new Padding(0);
        _calculatorTab.BackColor = BackColor;
        _calculatorTab.Controls.Add(calculatorRoot);

        _systemTab.Name = "DuctSystemTab";
        _systemTab.Padding = new Padding(0);
        _systemTab.BackColor = BackColor;
        _systemTab.Controls.Add(BuildDuctSystemPage());

        _fittingTab.Name = "FittingLossTab";
        _fittingTab.Padding = new Padding(0);
        _fittingTab.BackColor = BackColor;
        _fittingTab.Controls.Add(BuildFittingLossPage());

        _layoutTab.Name = "LayoutDesignerTab";
        _layoutTab.Padding = new Padding(0);
        _layoutTab.BackColor = BackColor;
        _layoutTab.Controls.Add(BuildLayoutDesignerPage());

        _mainTabs.TabPages.Add(_calculatorTab);
        _mainTabs.TabPages.Add(_fittingTab);
        _mainTabs.TabPages.Add(_systemTab);
        _mainTabs.TabPages.Add(_layoutTab);
        var navigationHost = new NavigationChromePanel
        {
            Name = "MainNavigationHost",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(236, 243, 247)
        };
        var navigation = new FlowLayoutPanel
        {
            Name = "MainNavigationBar",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 3, 242, 11),
            BackColor = Color.Transparent
        };
        ConfigureNavigationButton(_calculatorNavigationButton, "NavCalculatorButton", "Duct Calculator", 0);
        ConfigureNavigationButton(_fittingNavigationButton, "NavFittingButton", "Fitting Calculator", 1);
        ConfigureNavigationButton(_systemNavigationButton, "NavSystemButton", "Duct System", 2);
        ConfigureNavigationButton(_layoutNavigationButton, "NavLayoutButton", "Layout Designer", 3);
        navigation.Controls.Add(_calculatorNavigationButton);
        navigation.Controls.Add(_fittingNavigationButton);
        navigation.Controls.Add(_systemNavigationButton);
        navigation.Controls.Add(_layoutNavigationButton);
        ConfigureSettingsMenu();
        ConfigureSaveButton();
        var actions = new FlowLayoutPanel
        {
            Name = "MainNavigationActions",
            Dock = DockStyle.Right,
            Width = 234,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 11),
            BackColor = Color.Transparent
        };
        actions.Controls.Add(_saveButton);
        actions.Controls.Add(_settingsButton);
        navigationHost.Controls.Add(navigation);
        navigationHost.Controls.Add(actions);

        var projectHeader = BuildProjectIdentityHeader();
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(projectHeader, 0, 0);
        shell.Controls.Add(navigationHost, 0, 1);
        shell.Controls.Add(_mainTabs, 0, 2);
        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            UpdateNavigationButtonState();
            SynchronizeSelectedSystemView();
            if (_mainTabs.SelectedTab == _fittingTab)
                ResetFittingInputScroll();
        };
        Controls.Add(shell);
        UpdateNavigationButtonState();
        RefreshProjectIdentityHeader();
    }

    private Control BuildProjectIdentityHeader()
    {
        var header = new ProjectIdentityHeaderPanel
        {
            Name = "ProjectIdentityHeader",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 49, 69),
            Padding = new Padding(18, 8, 18, 8),
            ColumnCount = 5,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconFrame = new BrandIconFramePanel
        {
            Name = "ProjectHeaderIconFrame",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 1, 7, 1),
            Padding = new Padding(8)
        };
        _projectHeaderAppIcon.Name = "ProjectHeaderAppIcon";
        _projectHeaderAppIcon.Dock = DockStyle.Fill;
        _projectHeaderAppIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _projectHeaderAppIcon.Margin = Padding.Empty;
        _projectHeaderAppIcon.BackColor = Color.Transparent;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream(
                   "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.png"))
        using (var sourceIcon = iconStream is null ? null : Image.FromStream(iconStream))
            _projectHeaderAppIcon.Image = sourceIcon is null ? Icon?.ToBitmap() : new Bitmap(sourceIcon);
        iconFrame.Controls.Add(_projectHeaderAppIcon);

        var brandBlock = new TableLayoutPanel
        {
            Name = "ProjectHeaderBrandBlock",
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 1,
            RowCount = 2
        };
        brandBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brandBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        brandBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        _projectHeaderBrand.Name = "ProjectHeaderBrand";
        _projectHeaderBrand.Dock = DockStyle.Fill;
        _projectHeaderBrand.Text = "FanWorks";
        _projectHeaderBrand.ForeColor = Color.White;
        _projectHeaderBrand.Font = new Font("Segoe UI Semibold", 26F);
        _projectHeaderBrand.TextAlign = ContentAlignment.BottomLeft;
        _projectHeaderBrand.Margin = Padding.Empty;
        _projectHeaderBrand.BackColor = Color.Transparent;
        var brandTagline = new Label
        {
            Name = "ProjectHeaderTagline",
            Dock = DockStyle.Fill,
            Text = "HVAC AIRSIDE DESIGN",
            ForeColor = Color.FromArgb(107, 220, 227),
            Font = new Font("Segoe UI Semibold", 7.4F),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(11, 0, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        brandBlock.Controls.Add(_projectHeaderBrand, 0, 0);
        brandBlock.Controls.Add(brandTagline, 0, 1);

        _projectHeaderLogo.Name = "ProjectHeaderLogo";
        _projectHeaderLogo.Dock = DockStyle.Fill;
        _projectHeaderLogo.SizeMode = PictureBoxSizeMode.StretchImage;
        _projectHeaderLogo.Margin = new Padding(8, 0, 0, 0);
        _projectHeaderLogo.BackColor = Color.Transparent;
        _projectHeaderLabel.Name = "ProjectHeaderLabel";
        _projectHeaderLabel.Dock = DockStyle.Fill;
        _projectHeaderLabel.Padding = new Padding(8, 4, 8, 0);
        _projectHeaderLabel.TextAlign = ContentAlignment.MiddleRight;
        _projectHeaderLabel.ForeColor = Color.FromArgb(220, 232, 239);
        _projectHeaderLabel.Font = new Font("Segoe UI", 9F);
        _projectHeaderLabel.AutoEllipsis = true;
        _projectHeaderLabel.BackColor = Color.Transparent;
        header.Controls.Add(iconFrame, 0, 0);
        header.Controls.Add(brandBlock, 1, 0);
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

    private void ConfigureSettingsMenu()
    {
        _settingsButton.Name = "SettingsButton";
        _settingsButton.Text = "Settings  ▾";
        _settingsButton.Size = new Size(132, 40);
        _settingsButton.Width = 132;
        _settingsButton.Margin = new Padding(0);
        _settingsButton.BackColor = Color.FromArgb(235, 241, 246);
        _settingsButton.ForeColor = Color.FromArgb(34, 62, 88);
        _settingsButton.Font = new Font("Segoe UI Semibold", 9.5F);
        UiPolish.RoundButton(_settingsButton, 10);

        _imperialUnitsMenuItem = new ToolStripMenuItem("Units: Imperial");
        _metricUnitsMenuItem = new ToolStripMenuItem("Units: Metric");
        _imperialUnitsMenuItem.Click += (_, _) => _unitsCombo.SelectedIndex = 0;
        _metricUnitsMenuItem.Click += (_, _) => _unitsCombo.SelectedIndex = 1;
        var projectInformation = new ToolStripMenuItem("Project Settings…");
        projectInformation.Click += (_, _) => EditProjectSettings();
        var calculationMethodInputs = new ToolStripMenuItem("Calculation Method Inputs…")
        {
            Name = "CalculationMethodInputsMenuItem"
        };
        calculationMethodInputs.Click += (_, _) => EditSolverSettings();
        var automaticSizingInputs = new ToolStripMenuItem("Automatic Sizing Inputs…")
        {
            Name = "AutomaticSizingInputsMenuItem"
        };
        automaticSizingInputs.Click += (_, _) => EditAutomaticSizingInputs();
        // The Name is kept from v0.92 ("Import Project") so existing automation finds the item.
        var importProject = new ToolStripMenuItem("Open Project…") { Name = "ImportProjectMenuItem" };
        importProject.Click += (_, _) => OpenProject();
        _settingsMenu.Items.AddRange(new ToolStripItem[]
        {
            _imperialUnitsMenuItem,
            _metricUnitsMenuItem,
            new ToolStripSeparator(),
            projectInformation,
            calculationMethodInputs,
            automaticSizingInputs,
            importProject
        });
        _settingsMenu.Opening += (_, _) => UpdateSettingsMenuState();
        _settingsButton.Click += (_, _) =>
            _settingsMenu.Show(_settingsButton, new Point(0, _settingsButton.Height));
    }

    private void ConfigureSaveButton()
    {
        _saveButton.Name = "SaveSystemButton";
        _saveButton.Text = "Save";
        _saveButton.Size = new Size(86, 40);
        _saveButton.Margin = new Padding(0, 0, 8, 0);
        _saveButton.BackColor = Color.FromArgb(42, 135, 91);
        _saveButton.ForeColor = Color.White;
        _saveButton.Font = new Font("Segoe UI Semibold", 9.5F);
        _saveButton.FlatStyle = FlatStyle.Flat;
        _saveButton.FlatAppearance.BorderSize = 0;
        UiPolish.RoundButton(_saveButton, 10);
        _saveButton.Click += (_, _) => ExportDuctSystem();
    }

    private void UpdateSettingsMenuState()
    {
        if (_imperialUnitsMenuItem is not null)
            _imperialUnitsMenuItem.Checked = _currentUnits == UnitSystem.Imperial;
        if (_metricUnitsMenuItem is not null)
            _metricUnitsMenuItem.Checked = _currentUnits == UnitSystem.Metric;
    }

    private void EditProjectSettings()
    {
        using var dialog = new ProjectSettingsDialog(_ductSystem, _currentUnits);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _ductSystem.UserName = dialog.UserName;
        _ductSystem.ProjectName = dialog.ProjectName;
        _ductSystem.ProjectNumber = dialog.ProjectNumber;
        _ductSystem.ProjectDate = dialog.ProjectDate;
        _ductSystem.CompanyLogoPngBase64 = dialog.CompanyLogoPngBase64;
        SaveDuctSystem();
        RefreshProjectIdentityHeader();
    }

    private void EditSolverSettings()
    {
        using var dialog = new SolverSettingsDialog(_ductSystem, _currentUnits);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _ductSystem.DefaultSizingIncrementM = dialog.SizingIncrementM;
        _ductSystem.DefaultSizeRoundingDirection = dialog.RoundingDirection;
        _ductSystem.DefaultEquivalentDiameterMethod = dialog.EquivalentDiameterMethod;
        _ductSystem.DefaultPressureLossBasis = dialog.PressureLossBasis;
        ApplyProjectCalculationSettings();
        SaveDuctSystem();
        CalculateAndDisplay();
    }

    private void EditAutomaticSizingInputs()
    {
        using var dialog = new AutomaticSizingInputsDialog(_ductSystem.AutomaticSizing, _currentUnits);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _ductSystem.AutomaticSizing = dialog.Settings;
        SaveDuctSystem();
        _layoutStatusLabel.Text = "Automatic sizing defaults were saved for ducts and supported Designer components.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private void ApplyProjectCalculationSettings()
    {
        _updatingControls = true;
        try
        {
            _sizeIncrementText.Text = (_currentUnits == UnitSystem.Imperial
                ? UnitConversions.MToInches(_ductSystem.DefaultSizingIncrementM)
                : UnitConversions.MToMillimeters(_ductSystem.DefaultSizingIncrementM))
                .ToString("0.####", CultureInfo.CurrentCulture);
            SelectChoice(_equivalentMethodCombo, _ductSystem.DefaultEquivalentDiameterMethod);
            SelectChoice(_pressureBasisCombo, _ductSystem.DefaultPressureLossBasis);
        }
        finally
        {
            _updatingControls = false;
        }
    }

    private void ConfigureNavigationButton(Button button, string name, string text, int tabIndex)
    {
        button.Name = name;
        button.Text = text;
        button.Size = new Size(172, 40);
        button.Margin = new Padding(0, 0, 4, 0);
        button.BackColor = Color.FromArgb(235, 241, 246);
        button.ForeColor = Color.FromArgb(34, 62, 88);
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 205, 217);
        UiPolish.RoundButton(button, 10);
        button.Click += (_, _) =>
        {
            if (tabIndex == 0)
                ClearDuctEditingContext();
            else if (tabIndex == 1)
                ClearFittingEditingContext();
            _mainTabs.SelectedIndex = tabIndex;
        };
    }

    private void UpdateNavigationButtonState()
    {
        var buttons = new[] { _calculatorNavigationButton, _fittingNavigationButton, _systemNavigationButton, _layoutNavigationButton };
        for (var index = 0; index < buttons.Length; index++)
        {
            var selected = index == _mainTabs.SelectedIndex;
            buttons[index].BackColor = selected ? Color.FromArgb(21, 104, 119) : Color.FromArgb(235, 241, 243);
            buttons[index].ForeColor = selected ? Color.White : Color.FromArgb(34, 62, 88);
            buttons[index].FlatAppearance.MouseOverBackColor = selected
                ? Color.FromArgb(28, 121, 137)
                : Color.FromArgb(245, 249, 252);
            buttons[index].FlatAppearance.MouseDownBackColor = selected
                ? Color.FromArgb(16, 83, 96)
                : Color.FromArgb(221, 231, 239);
            buttons[index].Invalidate();
        }
    }

    private void SynchronizeSelectedSystemView()
    {
        if (_mainTabs.SelectedTab == _systemTab)
        {
            if (_ductSystem.LayoutNodes.Count > 0)
            {
                foreach (var node in _ductSystem.LayoutNodes)
                    SyncLinkedItemMetadata(node);
            }
            RefreshDuctSystemGrid();
        }
        else if (_mainTabs.SelectedTab == _layoutTab)
        {
            if (_ductSystem.LayoutNodes.Count > 0)
                SynchronizeLayoutMetadataFromSystem();
            RefreshLayoutDesigner();
            EnsureLayoutViewportPositioned();
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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackColor,
            Padding = new Padding(14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

        var header = new SectionTitlePanel
        {
            Name = "SystemSectionHeader",
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Blue
        };
        header.Controls.Add(new Label
        {
            Text = "Duct System",
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(28, 4, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18F),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        });
        header.Controls.Add(new Label
        {
            Text = "Saved components, airflow paths, and critical-route total pressure loss",
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(31, 0, 0, 7),
            ForeColor = Color.FromArgb(215, 229, 242),
            Font = new Font("Segoe UI", 9.2F),
            BackColor = Color.Transparent
        });

        var summaryHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 11,
            BorderColor = UiPalette.CardBorder,
            Padding = new Padding(14, 7, 14, 7),
            Margin = new Padding(0, 6, 0, 0)
        };
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
        _systemTotalLabel.ForeColor = Color.FromArgb(22, 65, 102);
        _systemTotalLabel.Font = new Font("Segoe UI Semibold", 10.5F);
        _systemTotalLabel.AutoEllipsis = false;
        _systemCriticalLabel.Name = "SystemCriticalPathLabel";
        _systemCriticalLabel.Dock = DockStyle.Fill;
        _systemCriticalLabel.TextAlign = ContentAlignment.MiddleLeft;
        _systemCriticalLabel.ForeColor = Color.FromArgb(31, 120, 180);
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
        summaryHost.Controls.Add(summary);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 6),
            Margin = Padding.Empty,
            BackColor = BackColor
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
            BackColor = BackColor
        };
        var destructiveActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
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
        _autoSizeFanButton.BackColor = Color.FromArgb(31, 120, 180);
        _autoSizeFanButton.ForeColor = Color.White;
        _autoSizeFanButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _criticalPathFilterButton.Font = new Font("Segoe UI Semibold", 7.5F);
        _autoSizeFanButton.FlatAppearance.BorderColor = Color.FromArgb(31, 120, 180);
        _criticalPathFilterButton.Enabled = false;
        primaryActions.Controls.Add(_criticalPathFilterButton);
        primaryActions.Controls.Add(_autoSizeFanButton);
        primaryActions.Controls.Add(_viewFanInformationButton);
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

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(summaryHost, 0, 1);
        root.Controls.Add(actions, 0, 2);
        root.Controls.Add(_ductGrid, 0, 3);
        root.Controls.Add(_systemStorageStatusLabel, 0, 4);
        return root;
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
        var displayOrder = new[]
        {
            "Id", "Description", "Airflow", "PressureIn", "PressureOut", "AirSystem", "Temperature",
            "PressureRate", "Velocity", "Length", "Size", "StaticDrop", "BranchId", "UpstreamId",
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
            MessageBox.Show(this, "Enter a fitting ID.", "Fitting ID Required",
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
            this,
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
        column.Items.AddRange(AirSystemTypes.Cast<object>().ToArray());
        _ductGrid.Columns.Add(column);
    }

    private void LoadDuctSystem()
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
        RefreshLayoutDesigner();
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

    private void SaveDuctSystem()
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

    private void AddOrUpdateCurrentDuct()
    {
        var returnToDesigner = PendingConnectionNode() is not null || PendingLayoutNode(LayoutNodeType.Duct) is not null;
        var historyState = returnToDesigner ? CaptureLayoutHistoryState() : null;
        CalculateAndDisplay();
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
                if (PendingConnectionNode() is not null || PendingLayoutNode(LayoutNodeType.Duct) is not null)
                    ApplyLayoutMetadata(updated);
                _ductSystem.Ducts[index] = updated;
                _statusLabel.Text = $"Updated duct D-{sequence:000} in the duct system.";
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
        _ductEditingLabel.Visible = false;
        if (historyState is not null)
            CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _mainTabs.SelectedTab = returnToDesigner ? _layoutTab : _systemTab;
        if (returnToDesigner)
            _layoutStatusLabel.Text = "Duct configured and linked. Continue placing components on the grid.";
    }

    private void AddNewDuct(DuctCalculationResult result)
    {
        var sequence = _ductSystem.ReserveSequence();
        var duct = SavedDuct.FromResult(sequence, CreateAutomaticDescription(result), result,
            _ductSystem.DefaultAirSystemType, _ductSystem.DefaultBranchId);
        ApplyLayoutMetadata(duct);
        _ductSystem.Ducts.Add(duct);
        _statusLabel.Text = $"Added duct D-{sequence:000} to the duct system.";
    }

    private string CreateAutomaticDescription(DuctCalculationResult result)
    {
        var shape = result.Input.Geometry.Shape == DuctShape.Round ? "Round" : "Rectangular";
        var airflow = _currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.M3PerSToCfm(result.Input.AirflowM3PerS):N0} cfm"
            : $"{UnitConversions.M3PerSToLps(result.Input.AirflowM3PerS):N0} L/s";
        return $"{shape} {FormatGeometry(result.Input.Geometry)} – {airflow}";
    }

    private void AutoSizeFan()
    {
        if (!TryAutoSizeFan(out var message))
        {
            MessageBox.Show(this, message, "Auto-size Fan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveDuctSystem();
        RefreshDuctSystemGrid();
        _systemStorageStatusLabel.Text = message;
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
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

        UpdateCalculatedLayoutValues();
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
        UpdateCalculatedLayoutValues();

        var route = ComputeLongestRoute();
        if (!route.IsComplete)
        {
            (fan.AirflowM3PerS, fan.AirflowUserSpecified, fan.AirflowOverride) = priorAirflow;
            UpdateCalculatedLayoutValues();
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
        UpdateCalculatedLayoutValues();
        _criticalPathCalculated = true;
        _criticalPathFilterEnabled = false;
        ApplyCriticalRoute(ComputeLongestRoute());
        var horsepower = TryBrakeHorsepower(requiredAirflow, fan.FanStaticPressurePa, fan.FanEfficiencyPercent);
        message = $"{fan.Id} was sized to {FormatSystemAirflow(requiredAirflow)}, {FormatSystemTotal(fan.FanStaticPressurePa)} static pressure, and {FormatHorsepower(horsepower)} at {fan.FanEfficiencyPercent:0.#}% efficiency."
                  + (_criticalPathAssumptions.Count > 0
                      ? $"\r\n\r\nNote: the critical path {CriticalPathAssumptionNote()}. Size those connections for a complete result."
                      : string.Empty);
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

    private void ViewFanInformation()
    {
        if (!TryGetFanInformation(out var information))
        {
            MessageBox.Show(this, information, "Fan Information", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        using var dialog = new FanInformationDialog(information);
        dialog.ShowDialog(this);
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

        UpdateCalculatedLayoutValues();
        var fan = fans[0];
        var horsepower = TryBrakeHorsepower(fan.AirflowM3PerS, fan.FanStaticPressurePa,
            fan.FanEfficiencyPercent);
        information = $"{fan.Id} — System Fan\r\n\r\nAirflow: {FormatSystemAirflow(fan.AirflowM3PerS)}\r\n" +
                      $"Static pressure: {FormatSystemTotal(fan.FanStaticPressurePa)}\r\n" +
                      $"Fan efficiency: {fan.FanEfficiencyPercent:0.#}%\r\n" +
                      $"Required motor power: {FormatHorsepower(horsepower)}";
        return true;
    }

    private void ToggleCriticalPathFilter()
    {
        if (!_criticalPathCalculated)
            return;
        _criticalPathFilterEnabled = !_criticalPathFilterEnabled;
        RefreshDuctSystemGrid();
    }

    private void ResetCriticalPath()
    {
        _criticalPathCalculated = false;
        _criticalPathFilterEnabled = false;
        _criticalPathPressureLossPa = 0.0;
        _criticalPathRoute = string.Empty;
        _criticalPathItemKeys.Clear();
        _criticalPathIssues = Array.Empty<SystemCalculationIssue>();
        _criticalPathAssumptions = Array.Empty<SystemCalculationIssue>();
    }

    private void ApplyCriticalRoute(CriticalPathResult route)
    {
        _criticalPathPressureLossPa = route.PressureLossPa;
        _criticalPathItemKeys = new HashSet<string>(route.ItemKeys, StringComparer.OrdinalIgnoreCase);
        _criticalPathIssues = route.Issues;
        _criticalPathAssumptions = route.RouteAssumptions;
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

    private static string DuctKey(int sequence) => CriticalPathAnalyzer.DuctKey(sequence);
    private static string FittingKey(int sequence) => CriticalPathAnalyzer.FittingKey(sequence);
    private static string LayoutKey(int sequence) => CriticalPathAnalyzer.LayoutKey(sequence);

    /// <summary>One line naming the items that kept the critical path from being calculated.</summary>
    private string CriticalPathIssueSummary()
    {
        if (_criticalPathIssues.Count == 0)
            return string.Empty;
        var names = _criticalPathIssues.Select(x => x.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var listed = string.Join(", ", names.Take(6)) + (names.Length > 6 ? $" and {names.Length - 6} more" : string.Empty);
        return $"Incomplete: {listed} could not be calculated";
    }

    /// <summary>Short note on assumptions made on the critical route, or empty when there are none.</summary>
    private string CriticalPathAssumptionNote()
    {
        if (_criticalPathAssumptions.Count == 0)
            return string.Empty;
        var names = _criticalPathAssumptions.Select(x => x.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var listed = string.Join(", ", names.Take(6)) + (names.Length > 6 ? $" and {names.Length - 6} more" : string.Empty);
        return $"assumes no loss in {names.Length} unsized connection{(names.Length == 1 ? string.Empty : "s")} (into {listed})";
    }

    private bool ShowCriticalItem(string key) => !_criticalPathFilterEnabled || _criticalPathItemKeys.Contains(key);

    private void ApplyCriticalRowStyle(DataGridViewRow row, string key)
    {
        if (!_criticalPathCalculated || !_criticalPathItemKeys.Contains(key))
            return;
        row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 253);
        row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 65, 102);
    }

    private void RefreshDuctSystemGrid()
    {
        if (_ductGrid.Columns.Count == 0)
            return;

        UpdateCalculatedLayoutValues();
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
                    : equipment.Type == LayoutNodeType.Fan ? "System Fan" : equipment.Type == LayoutNodeType.Coil ? "Coil"
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
                    : equipment.Type == LayoutNodeType.Reservoir ? equipment.SpaceRole : equipment.Type.ToString(),
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
                        ? $"Air {UnitConversions.CelsiusToFahrenheit(equipment.TemperatureC):0.#}°F"
                        : equipment.Type == LayoutNodeType.Grd ? "User-entered GRD loss"
                        : equipment.Type == LayoutNodeType.CustomComponent ? "User-entered custom component loss" : "Filter loss",
                "—", "—");
            _ductGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(247, 251, 248);
            _ductGrid.Rows[rowIndex].Tag = equipment;
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
            var node = LayoutNodeForSystemTag(row.Tag);
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
                : $"Critical-path net pressure loss: {FormatSystemTotal(_criticalPathPressureLossPa)}"
                  + (_criticalPathAssumptions.Count > 0 ? $" — {CriticalPathAssumptionNote()}" : string.Empty);
        _systemTotalLabel.ForeColor = _criticalPathIssues.Count > 0 ? Color.Firebrick
            : _criticalPathAssumptions.Count > 0 ? Color.FromArgb(150, 85, 0) : Color.FromArgb(22, 65, 102);
        _systemCountLabel.Text = itemCount == 1 ? "1 item" : $"{itemCount} items";
        _systemCriticalLabel.Text = !_criticalPathCalculated
            ? "Critical route: not available"
            : $"Route: {_criticalPathRoute}";
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
        RefreshLayoutSystemSummary();
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

    private string FormatSystemAirflow(double cubicMetersPerSecond) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.M3PerSToCfm(cubicMetersPerSecond):N0}"
        : $"{UnitConversions.M3PerSToLps(cubicMetersPerSecond):N0}";

    private string FormatSystemGeometry(DuctGeometry geometry) => _currentUnits == UnitSystem.Imperial
        ? geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToInches(geometry.DiameterM):0.##} in round"
            : $"{UnitConversions.MToInches(geometry.WidthM):0.##} × {UnitConversions.MToInches(geometry.HeightM):0.##} in"
        : geometry.Shape == DuctShape.Round
            ? $"{UnitConversions.MToMillimeters(geometry.DiameterM):0.#} mm round"
            : $"{UnitConversions.MToMillimeters(geometry.WidthM):0.#} × {UnitConversions.MToMillimeters(geometry.HeightM):0.#} mm";

    private string FormatSystemLength(double meters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.#}"
        : $"{meters:0.##}";

    private string FormatSystemDelta(double meters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToFeet(meters):0.##}"
        : $"{meters:0.##}";

    private string FormatSystemArea(double squareMeters) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.SquareMetersToSquareFeet(squareMeters):0.###}"
        : $"{squareMeters:0.####}";

    private string FormatSystemVelocity(double metersPerSecond) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MpsToFpm(metersPerSecond):N0}"
        : $"{metersPerSecond:0.##}";

    private string FormatSystemTemperature(double celsius) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.CelsiusToFahrenheit(celsius):0.#}"
        : $"{celsius:0.#}";

    private string FormatSystemPressureRate(DuctCalculationResult result)
    {
        if (_currentUnits == UnitSystem.Imperial)
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossLowerPaPerM):0.###}–{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossUpperPaPerM):0.###}"
                : $"{UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM):0.###}";
        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.PressureLossLowerPaPerM:0.###}–{result.PressureLossUpperPaPerM:0.###}"
            : $"{result.PressureLossPaPerM:0.###}";
    }

    private string FormatSystemStaticDrop(DuctCalculationResult result)
    {
        if (_currentUnits == UnitSystem.Imperial)
            return result.Friction.Regime == FlowRegime.Transitional
                ? $"{UnitConversions.PaToInchesWater(result.TotalPressureLossLowerPa):0.###}–{UnitConversions.PaToInchesWater(result.TotalPressureLossUpperPa):0.###}"
                : $"{UnitConversions.PaToInchesWater(result.TotalPressureLossPa):0.###}";
        return result.Friction.Regime == FlowRegime.Transitional
            ? $"{result.TotalPressureLossLowerPa:0.##}–{result.TotalPressureLossUpperPa:0.##}"
            : $"{result.TotalPressureLossPa:0.##}";
    }

    private string FormatSystemFittingDrop(double pascals)
    {
        if (_currentUnits == UnitSystem.Imperial)
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
        var loss = SafeDuctPressureLoss(ductSequence);
        var inlet = OutletPressureToward(parent, downstream);
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
            // Unsized connection: close-coupled, no loss. Assigned duct that fails: NaN.
            var ductLoss = ductSequence is int sequence ? SafeDuctPressureLoss(sequence) : 0.0;
            incomingPressures.Add(OutletPressureToward(parent, node) - ductLoss);
        }
    }

    private void UpdateSystemGridHeaders()
    {
        if (_ductGrid.Columns.Count == 0)
            return;
        var imperial = _currentUnits == UnitSystem.Imperial;
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
        _ductGrid.Columns["Velocity"].HeaderText = imperial ? "Velocity\n(fpm)" : "Velocity\n(m/s)";
        _ductGrid.Columns["Temperature"].HeaderText = imperial ? "Temperature\n(°F)" : "Temperature\n(°C)";
        _ductGrid.Columns["PressureRate"].HeaderText = imperial ? "Pressure rate\n(in. w.c./100 ft)" : "Pressure rate\n(Pa/m)";
    }

    private string FormatSystemTotal(double pascals) => _currentUnits == UnitSystem.Imperial
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
        var airflow = _currentUnits == UnitSystem.Imperial
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
        UpdateCalculatedLayoutValues();
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
    }

    private bool ConfirmAirflowOverride(DataGridViewRow row, double proposedAirflow)
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

    private bool ConfirmAirflowOverride(SavedLayoutNode? node, double proposedAirflow, string itemId)
    {
        if (node is null)
            return true;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == node.ParentNodeId);
        if (IsConvergingLayoutNode(node))
        {
            node.AirflowOverride = false;
            node.AirflowUserSpecified = false;
            return true;
        }
        if (IsDivergingLayoutNode(node) && parent is not null)
        {
            var incomingDuct = DuctForConnection(node, secondary: false);
            var expected = incomingDuct?.AirflowM3PerS > 0 ? incomingDuct.AirflowM3PerS : parent.AirflowM3PerS;
            var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), expected * .01);
            if (Math.Abs(proposedAirflow - expected) <= tolerance)
            {
                node.AirflowOverride = false;
                node.AirflowUserSpecified = false;
                return true;
            }
            var expectedText = _currentUnits == UnitSystem.Imperial
                ? $"{UnitConversions.M3PerSToCfm(expected):N0} cfm"
                : $"{UnitConversions.M3PerSToLps(expected):N0} L/s";
            var splitResponse = MessageBox.Show(this,
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
        var response = MessageBox.Show(this,
            $"{itemId} has an airflow that differs from the connected system without a fan, Space Source, converging fitting, or diverting fitting between the values.\r\n\r\nAllow the override and mark the affected item orange?",
            "Airflow Continuity Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return false;
        node.AirflowOverride = true;
        node.AirflowUserSpecified = true;
        return true;
    }

    private bool ConfirmConnectionAirflowOverride(LayoutConnectionRef connection, double proposedAirflow,
        string itemId)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var upstream = _ductSystem.LayoutNodes.FirstOrDefault(x => string.Equals(x.Id, parentId,
            StringComparison.OrdinalIgnoreCase));
        var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), Math.Max(proposedAirflow, upstream?.AirflowM3PerS ?? 0) * .01);
        var mismatch = false;
        var expected = upstream?.AirflowM3PerS ?? proposedAirflow;

        if (upstream is not null && IsDivergingLayoutNode(upstream))
        {
            var siblings = LayoutConnectionsFrom(upstream).Where(x =>
                !(ReferenceEquals(x.Downstream, downstream) && x.Secondary == connection.Secondary)).ToArray();
            var fixedSiblingTotal = siblings
                .Where(x => ConnectionAirflowUserSpecified(x.Downstream, x.Secondary))
                .Sum(x => DuctForConnection(x.Downstream, x.Secondary)?.AirflowM3PerS
                          ?? Math.Max(0.0, x.Downstream.AirflowM3PerS));
            var unlockedSiblingCount = siblings.Count(x => !ConnectionAirflowUserSpecified(x.Downstream, x.Secondary));
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
            SetConnectionAirflowUserSpecified(downstream, connection.Secondary,
                upstream is not null && IsDivergingLayoutNode(upstream)
                || Math.Abs(proposedAirflow - expected) > tolerance);
            SetConnectionAirflowOverride(downstream, connection.Secondary, false);
            return true;
        }

        var expectedText = _currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.M3PerSToCfm(expected):N0} cfm"
            : $"{UnitConversions.M3PerSToLps(expected):N0} L/s";
        var response = MessageBox.Show(this,
            $"{itemId} does not match the automatically balanced airflow of {expectedText}.\r\n\r\nAllow this value anyway and mark the duct orange?",
            "Airflow Balance Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return false;
        SetConnectionAirflowUserSpecified(downstream, connection.Secondary, true);
        SetConnectionAirflowOverride(downstream, connection.Secondary, true);
        return true;
    }

    private static bool AllowsAirflowChange(SavedLayoutNode node) =>
        node.Type == LayoutNodeType.Fan
        || node.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(node.SpaceRole)
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
        var meters = _currentUnits == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
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
            SyncConnectionDuctGeometry(equipment);
            SyncLinkedItemMetadata(equipment);
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
                SyncConnectionDuctGeometry(node);
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
                SyncConnectionDuctGeometry(node);
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
        RefreshLayoutDesigner();
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
            RefreshLayoutDesigner();
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
        RefreshLayoutDesigner();
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
        if (duct is null && fitting is null)
            return;

        if (fitting is not null)
        {
            _editingDuctSequence = null;
            _editingFittingSequence = fitting.Sequence;
            LoadFittingIntoCalculator(fitting);
            _fittingDescription = fitting.Description;
            _fittingEditingLabel.Text = $"Editing {fitting.Id} — {fitting.Description}";
            _fittingEditingLabel.Visible = true;
            _addFittingButton.Text = DefaultFittingActionText;
            _addFittingButton.Visible = true;
            _mainTabs.SelectedTab = _fittingTab;
            _fittingStatusLabel.Text = $"Editing {fitting.Id}. Change the inputs, then click {DefaultFittingActionText}.";
            _fittingStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
            return;
        }

        _editingFittingSequence = null;
        _editingDuctSequence = duct!.Sequence;
        LoadDuctIntoCalculator(duct);
        _ductEditingLabel.Text = $"Editing {duct.Id} — {duct.Description}";
        _ductEditingLabel.Visible = true;
        _addDuctButton.Text = DefaultDuctActionText;
        _addDuctButton.Visible = true;
        _mainTabs.SelectedTab = _calculatorTab;
        _statusLabel.Text = $"Editing {duct.Id}. Change any input, then click {DefaultDuctActionText}.";
        _statusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private void ClearDuctEditingContext()
    {
        _editingDuctSequence = null;
        _ductEditingLabel.Visible = false;
        _addDuctButton.Text = DefaultDuctActionText;
        _addDuctButton.Visible = false;
    }

    private void ClearFittingEditingContext()
    {
        _editingFittingSequence = null;
        _fittingEditingLabel.Visible = false;
        _addFittingButton.Text = DefaultFittingActionText;
        _addFittingButton.Visible = false;
    }

    private void LoadDuctIntoCalculator(SavedDuct duct)
    {
        _updatingControls = true;
        try
        {
            SelectChoice(_modeCombo, CalculationMode.SizeByTargetFriction);
            SelectChoice(_shapeCombo, duct.Shape);
            SelectChoice(_equivalentMethodCombo, duct.EquivalentDiameterMethod);
            SelectChoice(_pressureBasisCombo, duct.PressureLossBasis);
            SelectMaterial(duct.MaterialName, duct.RoughnessFeet);

            if (_currentUnits == UnitSystem.Imperial)
            {
                _airflowText.Text = UnitConversions.M3PerSToCfm(duct.AirflowM3PerS).ToString("0.####", CultureInfo.CurrentCulture);
                _temperatureText.Text = UnitConversions.CelsiusToFahrenheit(duct.TemperatureC).ToString("0.####", CultureInfo.CurrentCulture);
                _lengthText.Text = UnitConversions.MToFeet(duct.LengthM).ToString("0.####", CultureInfo.CurrentCulture);
                _diameterText.Text = UnitConversions.MToInches(duct.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
                _widthText.Text = UnitConversions.MToInches(duct.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
                _heightText.Text = UnitConversions.MToInches(duct.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
            }
            else
            {
                _airflowText.Text = UnitConversions.M3PerSToLps(duct.AirflowM3PerS).ToString("0.####", CultureInfo.CurrentCulture);
                _temperatureText.Text = duct.TemperatureC.ToString("0.####", CultureInfo.CurrentCulture);
                _lengthText.Text = duct.LengthM.ToString("0.####", CultureInfo.CurrentCulture);
                _diameterText.Text = UnitConversions.MToMillimeters(duct.DiameterM).ToString("0.####", CultureInfo.CurrentCulture);
                _widthText.Text = UnitConversions.MToMillimeters(duct.WidthM).ToString("0.####", CultureInfo.CurrentCulture);
                _heightText.Text = UnitConversions.MToMillimeters(duct.HeightM).ToString("0.####", CultureInfo.CurrentCulture);
            }
            SelectChoice(_rectangularFixedDimensionCombo, RectangularFixedDimension.Width);
            _rectangularFixedDimensionText.Text = duct.Shape == DuctShape.Rectangular
                ? (_currentUnits == UnitSystem.Imperial
                    ? UnitConversions.MToInches(duct.WidthM)
                    : UnitConversions.MToMillimeters(duct.WidthM)).ToString("0.####", CultureInfo.CurrentCulture)
                : (_currentUnits == UnitSystem.Imperial ? "16" : "400");
        }
        finally
        {
            _updatingControls = false;
        }
        UpdateControlState();
        CalculateAndDisplay();
    }

    private static void SelectChoice<T>(ComboBox combo, T value) where T : struct
    {
        for (var index = 0; index < combo.Items.Count; index++)
        {
            if (combo.Items[index] is Choice<T> choice && EqualityComparer<T>.Default.Equals(choice.Value, value))
            {
                combo.SelectedIndex = index;
                return;
            }
        }
    }

    private void SelectMaterial(string name, double roughnessFeet)
    {
        for (var index = 0; index < _materialCombo.Items.Count; index++)
        {
            if (_materialCombo.Items[index] is DuctMaterial material
                && (string.Equals(material.Name, name, StringComparison.OrdinalIgnoreCase)
                    || Math.Abs(material.RoughnessFeet - roughnessFeet) <= 1e-12))
            {
                _materialCombo.SelectedIndex = index;
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
            this,
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
                _ductEditingLabel.Visible = false;
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
                _addFittingButton.Text = DefaultFittingActionText;
                _addFittingButton.Visible = false;
                _fittingEditingLabel.Visible = false;
            }
        }
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
    }

    private void DeleteAllSystemItems()
    {
        if (_ductSystem.Ducts.Count == 0 && _ductSystem.Fittings.Count == 0 && _ductSystem.LayoutNodes.Count == 0)
            return;
        var response = MessageBox.Show(this,
            "Delete every duct, fitting, fan, coil, filter, and graphical layout item?\r\n\r\nThis cannot be undone.",
            "Delete Entire Duct System", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes)
            return;
        ClearAllSystemData();
        ClearLayoutHistory();
    }

    private void ClearAllSystemData()
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
        _layoutViewportInitialized = false;
        _editingDuctSequence = null;
        _editingFittingSequence = null;
        _pendingLayoutNodeId = null;
        _pendingConnectionNodeId = null;
        _pendingConnectionSecondary = false;
        _addDuctButton.Text = DefaultDuctActionText;
        _addFittingButton.Text = DefaultFittingActionText;
        _addDuctButton.Visible = false;
        _addFittingButton.Visible = false;
        _ductEditingLabel.Visible = false;
        _fittingEditingLabel.Visible = false;
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        EnsureLayoutViewportPositioned(force: true);
        _systemStorageStatusLabel.Text = "All Duct System and Layout Designer items were deleted.";
        _systemStorageStatusLabel.ForeColor = Color.FromArgb(140, 54, 54);
    }

    private void CopyDuctSystem()
    {
        if (_ductSystem.Ducts.Count == 0 && _ductSystem.Fittings.Count == 0 && _ductSystem.LayoutNodes.Count == 0)
            return;
        var builder = new StringBuilder();
        var imperial = _currentUnits == UnitSystem.Imperial;
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
                    : equipment.Type == LayoutNodeType.Fan ? "System Fan" : equipment.Type == LayoutNodeType.Coil ? "Coil"
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
            : $"Critical-path total pressure loss\t{FormatSystemTotal(_criticalPathPressureLossPa)}"
              + (_criticalPathAssumptions.Count > 0 ? $"\t{CriticalPathAssumptionNote()}" : string.Empty));
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

    private void ExportDuctSystem()
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
            if (dialog.ShowDialog(this) != DialogResult.OK)
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
                MessageBox.Show(this,
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
            UseWaitCursor = true;
            _systemStorageStatusLabel.Text = "Creating the project PDF and CSV...";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
            Application.DoEvents();

            RefreshDuctSystemGrid();
            var headers = new[]
            {
                _currentUnits == UnitSystem.Imperial ? "Airflow (cfm)" : "Airflow (L/s)",
                _currentUnits == UnitSystem.Imperial ? "Static Pressure In (in. w.c.)" : "Static Pressure In (Pa)",
                _currentUnits == UnitSystem.Imperial ? "Static Pressure Out (in. w.c.)" : "Static Pressure Out (Pa)",
                "Air System", _currentUnits == UnitSystem.Imperial ? "Temperature (deg F)" : "Temperature (deg C)",
                _currentUnits == UnitSystem.Imperial ? "Pressure Rate (in. w.c./100 ft)" : "Pressure Rate (Pa/m)",
                _currentUnits == UnitSystem.Imperial ? "Velocity (fpm)" : "Velocity (m/s)",
                _currentUnits == UnitSystem.Imperial ? "Length (ft)" : "Length (m)", "Duct Size"
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
                fan.FanStaticPressurePa, fan.FanEfficiencyPercent);
            var metadata = new DuctSystemPdfMetadata(_ductSystem.UserName, _ductSystem.ProjectName,
                _ductSystem.ProjectNumber, projectDate, logo.Base64, logo.Width, logo.Height,
                DateTime.Now.ToString("MMMM d, yyyy h:mm tt", CultureInfo.CurrentCulture),
                fan?.Id ?? "No fan added", fan is null ? "Not available" : FormatSystemAirflow(fan.AirflowM3PerS),
                fan is null ? "Not available" : FormatSystemTotal(fan.FanStaticPressurePa),
                fan is null ? "Not available" : $"{fan.FanEfficiencyPercent:0.#}%",
                fan is null ? "Not available" : FormatHorsepower(fanHorsepower),
                !_criticalPathCalculated
                    ? "Not calculated"
                    : _criticalPathIssues.Count > 0
                        ? $"Not available. {CriticalPathIssueSummary()}."
                        : $"{_criticalPathRoute} ({FormatSystemTotal(_criticalPathPressureLossPa)} total loss"
                          + (_criticalPathAssumptions.Count > 0 ? $"; {CriticalPathAssumptionNote()})" : ")"));
            DuctSystemPdf.Export(temporaryPdfPath, "Duct System Schedule",
                $"Exported {DateTime.Now:MMMM d, yyyy h:mm tt} - {_currentUnits} units", headers, rows,
                _systemTotalLabel.Text.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", " — ", StringComparison.Ordinal),
                metadata, BuildPdfLayout(), BuildPdfReportItems(), BuildPdfSchematics());
            DuctSystemCsv.Export(temporaryCsvPath, _ductSystem);
            File.Move(temporaryPdfPath, pdfPath, overwrite: true);
            File.Move(temporaryCsvPath, csvPath, overwrite: true);
            _systemStorageStatusLabel.Text = $"Saved {Path.GetFileName(projectPath)}, {Path.GetFileName(pdfPath)}, and {Path.GetFileName(csvPath)}. Future saves overwrite these files.";
            _systemStorageStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(this,
                    $"Project saved successfully.\r\n\r\nProject: {projectPath}\r\nPDF report: {pdfPath}\r\nCSV schedule: {csvPath}",
                    "FanWorks Project Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error($"Report export to {pdfPath} failed after the project was saved.", ex);
            _systemStorageStatusLabel.Text = $"The project was saved, but the PDF/CSV report could not be created: {ex.Message}";
            _systemStorageStatusLabel.ForeColor = Color.Firebrick;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(this,
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
            UseWaitCursor = false;
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
            CaptionSource = LayoutNodeCaption,
            ConnectionCaptionSource = LayoutConnectionCaption,
            ConnectionFlowReversedSource = LayoutConnectionFlowReversed,
            CriticalPathNodeSource = IsLayoutNodeInCalculatedCriticalPath,
            CriticalPathConnectionSource = IsLayoutConnectionInCalculatedCriticalPath,
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
                : node.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(node.SpaceRole) ? 0 : 1;
            var outletPorts = node.Type == LayoutNodeType.CalculationPoint ? 0
                : node.Type == LayoutNodeType.Fitting
                ? FittingCatalog.OutletPortCount(node.FittingGroup)
                : node.Type == LayoutNodeType.Reservoir && IsSpaceSinkRole(node.SpaceRole) ? 0 : 1;
            return new DuctSystemPdfLayoutNode(node.Id, LayoutNodeCaption(node),
                node.Type.ToString(), node.Type == LayoutNodeType.Grd ? node.GrdType
                    : node.Type == LayoutNodeType.Reservoir ? node.SpaceRole
                    : node.Type == LayoutNodeType.CalculationPoint ? node.CalculationPointType
                    : node.Type == LayoutNodeType.CustomComponent ? node.Description : node.FittingGroup,
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
                    LayoutConnectionCaption(new LayoutConnectionRef(node, false)).Replace("\r", string.Empty)
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
                    LayoutConnectionCaption(new LayoutConnectionRef(node, true)).Replace("\r", string.Empty)
                        .Split('\n').ElementAtOrDefault(1) ?? "—"));
            }
        }
        return new DuctSystemPdfLayout(nodes, connections);
    }

    private IReadOnlyList<DuctSystemPdfReportItem> BuildPdfReportItems()
    {
        var items = new List<(string Key, DuctSystemPdfReportItem Item)>();
        foreach (var duct in _ductSystem.Ducts.OrderBy(x => x.Sequence))
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
                calculations.AddRange(BuildDuctPdfCalculations(result));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                outputs.Add(PdfField("Calculation status", $"Invalid - {ex.Message}"));
            }
            var quickCheck = new[]
            {
                PdfField("Duct size", SafeDuctSize(duct)),
                PdfField("Airflow", PdfAirflow(duct.AirflowM3PerS)),
                PdfField("Duct length", PdfLength(duct.LengthM)),
                PdfField("Pressure drop", outputs.FirstOrDefault(field => field.Label == "Total pressure loss")?.Value
                    ?? "Not calculated")
            };
            items.Add((DuctKey(duct.Sequence),
                new DuctSystemPdfReportItem(duct.Id, duct.Description, "STRAIGHT DUCT", inputs, outputs, calculations,
                    QuickCheck: quickCheck)));
        }

        foreach (var fitting in _ductSystem.Fittings.OrderBy(x => x.Sequence))
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
            try
            {
                var result = fitting.Calculate();
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
                calculations.AddRange(BuildFittingPdfCalculations(result));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
            {
                outputs.Add(PdfField("Calculation status", $"Invalid - {ex.Message}"));
            }
            var quickCheck = new[]
            {
                PdfField("Fitting type / name", outputs.FirstOrDefault(field => field.Label == "Fitting description")?.Value
                    ?? $"{fitting.FittingCode} - {fitting.Description}"),
                PdfField("Fitting C-value", outputs.FirstOrDefault(field => field.Label == "Loss coefficient C")?.Value
                    ?? "Not calculated"),
                PdfField("Pressure drop", outputs.FirstOrDefault(field => field.Label == "Fitting pressure loss")?.Value
                    ?? "Not calculated")
            };
            items.Add((FittingKey(fitting.Sequence),
                new DuctSystemPdfReportItem(fitting.Id, fitting.Description, "FITTING", inputs, outputs, calculations,
                    referenceImage.Base64, referenceImage.Width, referenceImage.Height, quickCheck)));
        }

        foreach (var node in _ductSystem.LayoutNodes
                     .Where(x => x.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
                                     or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                                     or LayoutNodeType.CustomComponent
                                 || x.Type == LayoutNodeType.Fitting && !x.IsDefined && x.LinkedFittingSequence is null)
                     .OrderBy(x => x.Sequence))
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
                    inputs.Add(PdfField("Fan system static pressure", PdfPressure(node.FanStaticPressurePa)));
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
            var reportType = node.Type == LayoutNodeType.CalculationPoint
                ? "CALCULATION POINT"
                : node.Type.ToString().ToUpperInvariant();
            var quickCheck = BuildEquipmentPdfQuickCheck(node, description, inputs, outputs);
            items.Add((LayoutKey(node.Sequence),
                new DuctSystemPdfReportItem(node.Id, description, reportType, inputs, outputs,
                    BuildEquipmentPdfCalculations(node, pressure), QuickCheck: quickCheck)));
        }
        return OrderPdfReportItems(items);
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
                PdfField("Fan static pressure", Input("Fan system static pressure")),
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
        var geometryEquation = geometry.Shape == DuctShape.Round
            ? "A = \\frac{\\pi D^{2}}{4}"
            : "A = \\frac{W \\times H}{144}";
        var equivalentEquation = geometry.Shape == DuctShape.Round
            ? "D_{e} = D"
            : result.Input.EquivalentDiameterMethod == EquivalentDiameterMethod.Huebscher
                ? "D_{e} = \\frac{1.30(WH)^{0.625}}{(W+H)^{0.25}}"
                : "D_{h} = \\frac{2WH}{W+H}";
        var requiredEquations = new List<DuctSystemPdfCalculationLine>
        {
            E("\\rho = \\rho_{moist}(T, RH, p_{atm})"),
            E("\\mu = \\mu_{Sutherland}(T)"),
            E(geometryEquation),
            E(equivalentEquation),
            E("V_{calc} = \\frac{Q}{A_{calc}}"),
            E("Re = \\frac{\\rho V_{calc}D_{calc}}{\\mu}")
        };
        if (result.Friction.Regime == FlowRegime.Laminar)
            requiredEquations.Add(E("f = \\frac{64}{Re}"));
        else
        {
            requiredEquations.Add(E("\\frac{1}{\\sqrt{f}} = -2 log_{10}(X)"));
            requiredEquations.Add(E("X = \\frac{\\epsilon}{3.7D} + \\frac{2.51}{Re\\sqrt{f}}"));
        }
        requiredEquations.AddRange(new[]
        {
            E("VP = (\\frac{V_{calc}}{4005})^{2}(\\frac{\\rho}{0.075})"),
            E("\\frac{\\Delta P}{100 ft} = f(\\frac{100 ft}{D_{calc}})VP"),
            E("\\Delta P = (\\frac{\\Delta P}{100 ft})(\\frac{L}{100 ft})")
        });
        var frictionLines = new List<DuctSystemPdfCalculationLine>
        {
            L(result.Friction.Regime == FlowRegime.Laminar
                ? "Reynolds number identifies laminar flow, so the direct Darcy-factor correlation is used."
                : "Reynolds number identifies turbulent flow, so the implicit Colebrook equation is solved by safeguarded bisection."),
            E(result.Friction.Regime == FlowRegime.Laminar
                    ? "f = \\frac{64}{Re}"
                    : "Colebrook(\\epsilon_{r}, Re) \\rightarrow f",
                $"{factor:0.000000}",
                result.Friction.Regime == FlowRegime.Laminar
                    ? $"Re = {result.ReynoldsNumber:0}"
                    : $"epsilon_r = {relativeRoughness:0.########}; Re = {result.ReynoldsNumber:0}")
        };
        if (result.Friction.Regime != FlowRegime.Laminar)
        {
            frictionLines.Add(L("Starting friction-factor bracket",
                $"{result.Friction.InitialLowerBound:0.########} to {result.Friction.InitialUpperBound:0.########}"));
            frictionLines.Add(L("Convergence check",
                $"{result.Friction.Iterations} iterations; residual {result.Friction.FinalResidual:0.000E+00}; converged {result.Friction.Converged}"));
        }
        return new[]
        {
            new DuctSystemPdfCalculationSection("REQUIRED EQUATIONS", requiredEquations),
            new DuctSystemPdfCalculationSection("INPUT VALUES", new[]
            {
                Input("Airflow Q", $"{qCfm:0.##} cfm"),
                Input("Air temperature T", $"{temperatureF:0.##} deg F"),
                Input("Relative humidity RH", $"{result.Input.RelativeHumidity:P0}"),
                Input("Atmospheric pressure p_atm", $"{result.Input.AtmosphericPressurePa * 0.000145037738:0.###} psia"),
                Input("Duct length L", $"{lengthFt:0.###} ft"),
                Input("Final duct geometry", finalSize),
                Input("Absolute roughness epsilon", $"{roughnessFt:0.######} ft")
            }),
            new DuctSystemPdfCalculationSection("1. FINAL DUCT GEOMETRY", new[]
            {
                L("Project sizing and rounding establish the constructible duct size before velocity and loss are evaluated."),
                E(geometryEquation, $"{areaFt2:0.####} ft2", finalSize),
                E(equivalentEquation, $"{diameterIn:0.###} in", finalSize),
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
            new DuctSystemPdfCalculationSection("ASSUMPTIONS", new[]
            {
                L("Steady, fully developed airflow; Darcy friction factor; moist-air properties at the entered temperature, RH, and pressure."),
                L($"Calculation basis: {result.CalculationBasisDescription}")
            })
        };
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
        var sections = new List<DuctSystemPdfCalculationSection>
        {
            new("REQUIRED EQUATIONS", new[]
            {
                E("A_{ref} = A(geometry)"),
                E("D_{h} = \\frac{4A_{ref}}{P_{wetted}}"),
                E("V_{ref} = \\frac{Q}{A_{ref}}"),
                E("Re = \\frac{\\rho V_{ref}D_{h}}{\\mu}"),
                E("C = C_{ASHRAE}(table inputs)"),
                E("VP = (\\frac{V_{ref}}{4005})^{2}(\\frac{\\rho}{0.075})"),
                E("\\Delta P_{t} = C \\times VP")
            }),
            new("PARAMETERS AND SUB-EQUATIONS", new[]
            {
                L("Q = reference airflow; Aref = reference area; Vref = reference velocity"),
                L("Dh = hydraulic diameter; rho = moist-air density; mu = dynamic viscosity"),
                L("Re = Reynolds number; C = ASHRAE loss coefficient; VP = velocity pressure; DeltaPt = total-pressure loss"),
                E("\\rho = \\rho_{moist}(T, RH, p_{atm})"),
                E("\\mu = \\mu_{Sutherland}(T)")
            }),
            new("ASHRAE SOURCE AND SELECTION", new[]
            {
                L($"ASHRAE {result.Definition.Code} - {result.Definition.Description}"),
                L(result.Definition.SourceNote),
                L("Reference velocity basis =", result.Definition.ReferenceVelocity),
                L("Selection method =", result.Definition.IsAutomated
                    ? "linear/bilinear interpolation within published table limits"
                    : "user-entered coefficient from the cited ASHRAE entry")
            })
        };
        var tableLines = result.Definition.Inputs.Count == 0
            ? new[] { L("This fitting uses a fixed published coefficient; no table-axis input is required.") }
            : result.Definition.Inputs.Select(spec =>
            {
                result.Inputs.TryGetValue(spec.Key, out var value);
                var choice = spec.Choices?.OrderBy(item => Math.Abs(item.Value - value)).FirstOrDefault();
                var selected = choice is not null && Math.Abs(choice.Value - value) < 1e-9
                    ? $"{choice.Label} ({value:0.####})"
                    : $"{value:0.####}{(string.IsNullOrWhiteSpace(spec.Unit) ? "" : " " + spec.Unit)}";
                return Input($"{spec.Label} [{spec.Key}], range {spec.Minimum:0.####} to {spec.Maximum:0.####}", selected);
            }).ToArray();
        sections.Add(new DuctSystemPdfCalculationSection("PUBLISHED TABLE / EQUATION INPUTS", tableLines));

        var flow = new List<DuctSystemPdfCalculationLine>();
        if (result.AirflowM3PerS is double airflow)
            flow.Add(Input("Reference airflow Q", $"{UnitConversions.M3PerSToCfm(airflow):0.##} cfm"));
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
        if (result.ReferenceGeometry is { } reference)
        {
            var velocityFtS = UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS) / 60.0;
            var dhFt = UnitConversions.MToFeet(reference.HydraulicDiameterM);
            var viscosity = result.Air.DynamicViscosityPaS * 0.671968975;
            var reynolds = density * velocityFtS * dhFt / viscosity;
            intermediates.Add(E("Re = \\frac{\\rho V D_{h}}{\\mu}", $"{reynolds:0}",
                $"rho = {density:0.#####}; V = {velocityFtS:0.####} ft/s; Dh = {dhFt:0.####} ft; mu = {viscosity:0.########}"));
        }
        var publishedKeys = result.Definition.Inputs.Select(spec => spec.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        intermediates.AddRange(result.Inputs.Where(pair => !publishedKeys.Contains(pair.Key)).OrderBy(pair => pair.Key)
            .Select(pair => L($"{FittingIntermediateLabel(pair.Key)} =", $"{pair.Value:0.####}")));
        sections.Add(new DuctSystemPdfCalculationSection("INTERMEDIATE CALCULATIONS", intermediates.Count > 0 ? intermediates : new[] { L("No additional derived ratio is required for this fitting.") }));

        var velocityPressure = UnitConversions.PaToInchesWater(result.VelocityPressurePa);
        sections.Add(new DuctSystemPdfCalculationSection("COEFFICIENT AND PRESSURE LOSS", new[]
        {
            E("C = C_{ASHRAE}(inputs)", $"{result.Coefficient:0.####}", "selected/interpolated published table inputs"),
            E("VP = (\\frac{V}{4005})^{2}(\\frac{\\rho}{0.075})", $"{velocityPressure:0.####} in. w.c.",
                $"V = {UnitConversions.MpsToFpm(result.ReferenceVelocityMPerS):0.#} fpm; rho = {density:0.#####} lbm/ft3"),
            E("\\Delta P_{t} = C \\times VP", $"{UnitConversions.PaToInchesWater(result.PressureLossPa):0.####} in. w.c.",
                $"C = {result.Coefficient:0.####}; VP = {velocityPressure:0.####} in. w.c.")
        }));
        sections.Add(new DuctSystemPdfCalculationSection("ASSUMPTIONS", new[]
        {
            L("Steady airflow; ASHRAE reference-velocity convention; interpolation is restricted to digitized published limits."),
            L("Duct material and roughness are not used in the ASHRAE fitting-loss coefficient calculation.")
        }));
        if (result.TableRangeWarnings.Count > 0)
        {
            // Reviewer flag: at least one lookup was held at a table edge instead of interpolated.
            sections.Add(new DuctSystemPdfCalculationSection("REVIEW REQUIRED - OUTSIDE PUBLISHED TABLE",
                result.TableRangeWarnings.Select(warning => L(warning)).ToArray()));
        }
        return sections;
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

        var table = new List<DuctSystemPdfCalculationLine>
        {
            L("Published airflow (cfm)", "Airside ΔPs / Min ΔPs (in. w.c.)")
        };
        table.AddRange(selection.PublishedTable.Select(point =>
        {
            var bound = point == selection.LowerPoint || point == selection.UpperPoint;
            return L(bound ? $"> {point.AirflowCfm:0}" : $"  {point.AirflowCfm:0}",
                $"{point.PressureDropInWg:0.###}");
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
            new DuctSystemPdfCalculationSection("RECREATED PUBLISHED PERFORMANCE TABLE", table),
            new DuctSystemPdfCalculationSection("INPUT / INTERPOLATION / SOLVED RESULT", interpolation),
            new DuctSystemPdfCalculationSection("PRESSURE-LOSS RESULT", new[]
            {
                Solved("\\Delta P_{VAV}", $"{UnitConversions.PaToInchesWater(result.PressureLossPa):0.###} in. w.c."),
                L(string.Equals(result.Definition.Code, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
                    ? "Airside ΔPs is the selected hot-water coil air pressure drop."
                    : "Min ΔPs is the terminal inlet-to-outlet pressure drop with the primary damper full open."),
                L("Interpolation is restricted to the supplied published airflow range; no extrapolation is performed.")
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
        var table = new List<DuctSystemPdfCalculationLine>
        {
            L(grille
                ? "Core velocity | CFM | Velocity pressure | Negative static pressure | NC"
                : "CFM | Total pressure | Static pressure | NC | Throw (ft to 150/100/50 fpm)")
        };
        table.AddRange(selection.PublishedTable.Select(point => L(
            point == selection.Selected ? "> SELECTED" : string.Empty,
            grille
                ? $"{point.CoreVelocityFpm:0} fpm | {point.AirflowCfm:0} | {point.VelocityPressureInWg:0.###} | -{point.TotalPressureInWg:0.###} | {point.SoundNc}"
                : $"{point.AirflowCfm:0} | {point.TotalPressureInWg:0.###} | {(point.StaticPressureInWg ?? 0):0.###} | {point.SoundNc} | {point.ThrowFt}")));
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
            new DuctSystemPdfCalculationSection("RECREATED PUBLISHED PERFORMANCE TABLE", table),
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
        if (node.Type == LayoutNodeType.Fan)
        {
            var airflowCfm = UnitConversions.M3PerSToCfm(node.AirflowM3PerS);
            var staticPressure = UnitConversions.PaToInchesWater(node.FanStaticPressurePa);
            var horsepower = TryBrakeHorsepower(node.AirflowM3PerS,
                node.FanStaticPressurePa, node.FanEfficiencyPercent);
            return new[] { new DuctSystemPdfCalculationSection("FAN STATIC PRESSURE", new[]
            {
                L("Entered fan system static pressure rise =", $"{UnitConversions.PaToInchesWater(node.FanStaticPressurePa):0.####} in. w.c."),
                L($"SPout = SPin + SPfan = {inlet:0.####} + {UnitConversions.PaToInchesWater(node.FanStaticPressurePa):0.####} =", $"{outlet:0.####} in. w.c."),
                L($"BHP = CFM x SP / (6356 x efficiency) = {airflowCfm:0.##} x {staticPressure:0.####} / (6356 x {node.FanEfficiencyPercent / 100.0:0.####}) =", double.IsFinite(horsepower) ? $"{horsepower:0.####} hp" : FormatHorsepower(horsepower)),
                L("Positive fan pressure is a source term and is excluded from the reported critical-path total pressure loss.")
            }) };
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
            var lines = new List<DuctSystemPdfCalculationLine>
            {
                L(selection.Product), L(selection.Configuration),
                L("Selection method", grille
                    ? "The selected nominal duct-size chart is used and airflow is rounded upward to the next published point."
                    : selection.InletDiameterIn is null
                    ? "Requested airflow is rounded upward to the first published airflow at or above the request."
                    : "The smallest published inlet covering the requested airflow is selected before rounding upward to the next listed cfm."),
                L("Reference", string.IsNullOrWhiteSpace(selection.SourceReference)
                    ? "Published manufacturer performance data." : selection.SourceReference),
                L("Requested airflow", $"{requestedCfm:0.##} cfm"),
                L(grille
                    ? "Core velocity | CFM | Velocity pressure | Negative static pressure | NC"
                    : "CFM | Total pressure | Static pressure | NC | Throw (ft to 150/100/50 fpm)")
            };
            lines.AddRange(selection.PublishedTable.Select(point => L(point == selection.Selected ? "> SELECTED" : string.Empty,
                grille
                    ? $"{point.CoreVelocityFpm:0} fpm | {point.AirflowCfm:0} | {point.VelocityPressureInWg:0.###} | -{point.TotalPressureInWg:0.###} | {point.SoundNc}"
                    : $"{point.AirflowCfm:0} | {point.TotalPressureInWg:0.###} | {(point.StaticPressureInWg ?? 0):0.###} | {point.SoundNc} | {point.ThrowFt}")));
            lines.Add(L("Selected performance",
                grille
                    ? $"{selection.NominalDuctSizeIn} in; {selection.Selected.AirflowCfm:0} cfm; {selection.Selected.CoreVelocityFpm:0} fpm; pressure loss {selection.Selected.TotalPressureInWg:0.###} in. w.c.; NC {selection.Selected.SoundNc}"
                    : $"{selection.Selected.AirflowCfm:0} cfm; {selection.Selected.TotalPressureInWg:0.###} in. w.c.; NC {selection.Selected.SoundNc}; throw {selection.Selected.ThrowFt} ft"));
            lines.Add(L($"SPout = SPin - DeltaP = {inlet:0.####} - {selection.Selected.TotalPressureInWg:0.####} =",
                $"{outlet:0.####} in. w.c."));
            return new[] { new DuctSystemPdfCalculationSection("MANUFACTURER GRD PERFORMANCE SELECTION", lines) };
        }
        if (node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent)
            return new[] { new DuctSystemPdfCalculationSection("COMPONENT PRESSURE LOSS", new[]
            {
                L("Entered component pressure drop =", $"{UnitConversions.PaToInchesWater(node.PressureDropPa):0.####} in. w.c."),
                L($"SPout = SPin - DeltaP = {inlet:0.####} - {UnitConversions.PaToInchesWater(node.PressureDropPa):0.####} =", $"{outlet:0.####} in. w.c.")
            }) };
        if (node.Type == LayoutNodeType.CalculationPoint)
            return new[] { new DuctSystemPdfCalculationSection("CALCULATION POINT", new[]
            {
                L("The point samples its assigned duct without splitting the duct or adding pressure loss."),
                L("Position along observed duct =", $"{node.CalculationConnectionFraction * 100:0.#}%"),
                L("Calculated point value =", node.CalculationPointType switch
                {
                    "Temperature" => $"{UnitConversions.CelsiusToFahrenheit(node.TemperatureC):0.##} degF",
                    "Velocity" => $"{UnitConversions.MpsToFpm(node.VelocityMPerS):0.#} fpm",
                    "Airflow" => $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):0.##} cfm",
                    _ => $"{UnitConversions.PaToInchesWater(node.PressurePa):0.####} in. w.c."
                })
            }) };
        return new[] { new DuctSystemPdfCalculationSection("FLOW CONTINUITY AND PRESSURE", new[]
        {
            L("Component airflow =", $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):0.##} cfm"),
            L("No component pressure drop is assigned; SPout = SPin =", $"{outlet:0.####} in. w.c.")
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

    private string PdfAirflow(double value) => $"{FormatSystemAirflow(value)} {(_currentUnits == UnitSystem.Imperial ? "cfm" : "L/s")}";
    private string PdfTemperature(double value) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.CelsiusToFahrenheit(value):0.##} degF" : $"{value:0.##} degC";
    private string PdfLength(double value) => $"{FormatSystemLength(value)} {(_currentUnits == UnitSystem.Imperial ? "ft" : "m")}";
    private string PdfDelta(double value) => $"{FormatSystemDelta(value)} {(_currentUnits == UnitSystem.Imperial ? "ft" : "m")}";
    private string PdfArea(double value) => $"{FormatSystemArea(value)} {(_currentUnits == UnitSystem.Imperial ? "ft2" : "m2")}";
    private string PdfVelocity(double value) => $"{FormatSystemVelocity(value)} {(_currentUnits == UnitSystem.Imperial ? "fpm" : "m/s")}";
    private string PdfDiameter(double value) => _currentUnits == UnitSystem.Imperial
        ? $"{UnitConversions.MToInches(value):0.###} in" : $"{UnitConversions.MToMillimeters(value):0.##} mm";
    private string PdfPressure(double value) => !double.IsFinite(value)
        ? "Not connected"
        : _currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.PaToInchesWater(value):0.####} in. w.c."
            : $"{value:0.##} Pa";
    private string PdfPressureRate(DuctCalculationResult result) => $"{FormatSystemPressureRate(result)} {(_currentUnits == UnitSystem.Imperial ? "in. w.c./100 ft" : "Pa/m")}";
    private string PdfRoughness(double feet) => _currentUnits == UnitSystem.Imperial
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
        LayoutNodeType.Fan => "System Fan",
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
    private void OpenProject()
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
            if (dialog.ShowDialog(this) != DialogResult.OK)
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
                MessageBox.Show(this,
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

    private bool ConfirmReplaceCurrentSystem(string question)
    {
        var existingCount = _ductSystem.Ducts.Count + _ductSystem.Fittings.Count + _ductSystem.LayoutNodes.Count;
        if (existingCount == 0 || AutomationOptions.SuppressDialogs)
            return true;
        return MessageBox.Show(this,
                   $"{question}\r\n\r\nThe current system ({existingCount} item(s)) will be closed. "
                   + "Anything not saved with Save will be lost.",
                   "Replace Current System", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                   MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    /// <summary>Makes <paramref name="document"/> the working system and resets all editing state.</summary>
    private void ReplaceCurrentSystem(DuctSystemDocument document)
    {
        _ductSystem = document;
        _lastExportBasePath = string.Empty;
        ResetCriticalPath();
        ClearLayoutHistory();
        _editingDuctSequence = null;
        _editingFittingSequence = null;
        _addDuctButton.Text = DefaultDuctActionText;
        _addFittingButton.Text = DefaultFittingActionText;
        _addDuctButton.Visible = false;
        _addFittingButton.Visible = false;
        _ductEditingLabel.Visible = false;
        _fittingEditingLabel.Visible = false;
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        ApplyProjectCalculationSettings();
    }

    private void ImportDuctSystem(string path)
    {
        try
        {
            var imported = DuctSystemCsv.Import(path);
            var count = imported.Ducts.Count + imported.Fittings.Count;
            if (!AutomationOptions.SuppressDialogs && MessageBox.Show(this,
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

internal sealed class FanInformationDialog : Form
{
    internal FanInformationDialog(string information)
    {
        Text = "Fan Information";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(570, 304);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9.5F);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var icon = new PictureBox
        {
            Name = "FanInformationIcon",
            Size = new Size(132, 132),
            Anchor = AnchorStyles.None,
            Margin = new Padding(14),
            BackColor = Color.White
        };
        icon.Paint += (_, args) =>
        {
            var side = Math.Max(1, Math.Min(icon.ClientSize.Width, icon.ClientSize.Height) - 28);
            var square = new Rectangle((icon.ClientSize.Width - side) / 2, (icon.ClientSize.Height - side) / 2,
                side, side);
            LayoutIconPainter.Draw(args.Graphics, LayoutNodeType.Fan, square, Color.FromArgb(31, 120, 180));
        };

        var details = new Label
        {
            Name = "FanInformationDetails",
            Text = information,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 28, 18, 12),
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI", 10.5F),
            TextAlign = ContentAlignment.TopLeft
        };
        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 92,
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        UiPolish.RoundButton(close, 7);
        var buttons = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 18, 12) };
        buttons.Controls.Add(close);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(details, 1, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }
}
