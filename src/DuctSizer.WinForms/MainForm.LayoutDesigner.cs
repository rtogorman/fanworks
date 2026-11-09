using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

public sealed partial class MainForm
{
    private LayoutDesignerCanvas? _layoutCanvas;
    private readonly Label _layoutStatusLabel = new();
    private LayoutNodeType? _selectedLayoutTool;
    private string? _selectedLayoutFittingGroup;
    private string? _pendingLayoutNodeId;
    private string? _pendingConnectionNodeId;
    private bool _pendingConnectionSecondary;
    private readonly Button _layoutUndoButton = new();
    private readonly Button _layoutRedoButton = new();
    private readonly Button _layoutDeleteButton = new();
    private readonly Button _layoutRearrangeButton = new();
    private readonly Button _layoutLabelsButton = new();
    private readonly Button _layoutDuctSizesButton = new();
    private readonly Button _layoutCenterButton = new();
    private readonly Button _layoutCoordinateOriginButton = new();
    private readonly Button _layoutSourceSinkEditorButton = new();
    private readonly Button _layoutSizeUndefinedButton = new();
    private readonly Button _layoutRecalculateDuctworkButton = new();
    private readonly Button _layoutCriticalPathButton = new();
    private readonly Button _layoutPdfButton = new();
    private ContextMenuStrip? _layoutPdfMenu;
    private readonly ComboBox _layoutViewModeSelector = new();
    private bool _updatingLayoutViewMode;
    private readonly TextBox _layoutScaleInput = new();
    private readonly DataGridView _layoutSystemSummaryGrid = new();
    private SplitContainer? _layoutWorkspaceSplitter;
    private SplitContainer? _layoutToolboxSplitter;
    private readonly VerticalTextButton _layoutSummaryReopenButton = new();
    private readonly VerticalTextButton _layoutToolboxReopenButton = new();
    private int _layoutSummaryWidth = 330;
    private int _layoutToolboxWidth = 110;
    private bool _layoutSummaryInitialized;
    private bool _layoutToolboxInitialized;
    private bool _updatingLayoutSummarySplitter;
    private bool _updatingLayoutToolboxSplitter;
    private bool _syncingLayoutSelection;
    private Control? _layoutCanvasHost;
    private bool _layoutViewportInitialized;
    private string _layoutViewportStartSignature = string.Empty;
    private bool _layoutRearrangeMode;
    private bool _layoutCriticalPathHighlighted;
    /// <summary>Designer undo/redo depth. Each entry is a project snapshot without the PDF underlay or logo.</summary>
    private const int LayoutHistoryCapacity = 100;
    private readonly BoundedHistory<string> _layoutUndoHistory = new(LayoutHistoryCapacity);
    private readonly BoundedHistory<string> _layoutRedoHistory = new(LayoutHistoryCapacity);
    private readonly ToolTip _layoutToolTips = new() { AutoPopDelay = 8000, InitialDelay = 350, ReshowDelay = 100 };
    private static readonly JsonSerializerOptions LayoutHistoryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    private Control BuildLayoutDesignerPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var header = new SectionTitlePanel
        {
            Name = "DesignerSectionHeader",
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Blue
        };
        header.Controls.Add(new Label
        {
            Text = "Layout Designer",
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(22, 2, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15F),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        });
        header.Controls.Add(new Label
        {
            Text = "Drag components onto the grid, then drag between connector circles to create ducts. Drop directly on a duct to choose whether to insert the component.",
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(24, 0, 0, 5),
            ForeColor = Color.FromArgb(215, 229, 242),
            Font = new Font("Segoe UI", 8.3F),
            BackColor = Color.Transparent
        });

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = BackColor
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        leftHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var zoomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 3),
            BackColor = BackColor,
            AutoScroll = true
        };
        var zoomValue = new Label
        {
            Name = "LayoutZoomValue",
            Text = "100%",
            Size = new Size(34, 32),
            Visible = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI Semibold", 9F),
        };
        zoomValue.Margin = new Padding(0, 0, 2, 0);
        _layoutToolTips.SetToolTip(zoomValue, "Mouse-wheel zoom");
        zoomBar.Controls.Add(zoomValue);
        _layoutUndoButton.Name = "LayoutUndoButton";
        _layoutUndoButton.Size = new Size(36, 32);
        _layoutUndoButton.Margin = new Padding(1, 0, 1, 0);
        _layoutUndoButton.FlatStyle = FlatStyle.Flat;
        _layoutRedoButton.Name = "LayoutRedoButton";
        _layoutRedoButton.Size = new Size(36, 32);
        _layoutRedoButton.Margin = new Padding(0, 0, 1, 0);
        _layoutRedoButton.FlatStyle = FlatStyle.Flat;
        _layoutDeleteButton.Name = "LayoutDeleteButton";
        _layoutDeleteButton.Text = "×";
        _layoutDeleteButton.Size = new Size(34, 32);
        _layoutDeleteButton.Margin = new Padding(0, 0, 1, 0);
        _layoutDeleteButton.FlatStyle = FlatStyle.Flat;
        _layoutDeleteButton.Enabled = false;
        _layoutRearrangeButton.Name = "LayoutRearrangeButton";
        _layoutRearrangeButton.Text = "Move";
        _layoutRearrangeButton.Size = new Size(65, 32);
        _layoutRearrangeButton.Margin = new Padding(1, 0, 1, 0);
        _layoutRearrangeButton.FlatStyle = FlatStyle.Flat;
        _layoutLabelsButton.Name = "LayoutLabelsButton";
        _layoutLabelsButton.Text = "Hide Labels";
        _layoutLabelsButton.Size = new Size(105, 34);
        _layoutLabelsButton.Margin = new Padding(1, 0, 1, 0);
        _layoutLabelsButton.FlatStyle = FlatStyle.Flat;
        _layoutDuctSizesButton.Name = "LayoutDuctSizesButton";
        _layoutDuctSizesButton.Text = "Show Duct Sizes";
        _layoutDuctSizesButton.Size = new Size(140, 34);
        _layoutDuctSizesButton.Margin = new Padding(1, 0, 1, 0);
        _layoutDuctSizesButton.FlatStyle = FlatStyle.Flat;
        _layoutCenterButton.Name = "LayoutCenterButton";
        _layoutCenterButton.Text = "Center";
        _layoutCenterButton.Size = new Size(70, 32);
        _layoutCenterButton.Margin = new Padding(1, 0, 1, 0);
        _layoutCenterButton.FlatStyle = FlatStyle.Flat;
        _layoutCoordinateOriginButton.Name = "LayoutCoordinateOriginButton";
        _layoutCoordinateOriginButton.Text = "Origin";
        _layoutCoordinateOriginButton.Size = new Size(70, 32);
        _layoutCoordinateOriginButton.Margin = new Padding(0, 0, 1, 0);
        _layoutCoordinateOriginButton.FlatStyle = FlatStyle.Flat;
        _layoutCoordinateOriginButton.Enabled = false;
        _layoutSourceSinkEditorButton.Name = "LayoutSourceSinkEditorButton";
        _layoutSourceSinkEditorButton.Text = "Source/Sink Editor";
        _layoutSourceSinkEditorButton.Size = new Size(148, 34);
        _layoutSourceSinkEditorButton.Margin = new Padding(1, 0, 1, 0);
        _layoutSourceSinkEditorButton.FlatStyle = FlatStyle.Flat;
        _layoutSourceSinkEditorButton.BackColor = Color.White;
        _layoutSourceSinkEditorButton.ForeColor = Color.FromArgb(28, 69, 108);
        _layoutSizeUndefinedButton.Name = "LayoutSizeUndefinedButton";
        _layoutSizeUndefinedButton.Text = "Size Undefined Components";
        _layoutSizeUndefinedButton.Size = new Size(245, 34);
        _layoutSizeUndefinedButton.Margin = new Padding(1, 0, 1, 0);
        _layoutSizeUndefinedButton.FlatStyle = FlatStyle.Flat;
        _layoutSizeUndefinedButton.Font = new Font("Segoe UI Semibold", 8F);
        _layoutSizeUndefinedButton.UseCompatibleTextRendering = true;
        _layoutSizeUndefinedButton.BackColor = Color.FromArgb(31, 111, 168);
        _layoutSizeUndefinedButton.ForeColor = Color.White;
        _layoutRecalculateDuctworkButton.Name = "LayoutRecalculateDuctworkButton";
        _layoutRecalculateDuctworkButton.Text = "Recalculate All Ductwork";
        _layoutRecalculateDuctworkButton.Size = new Size(185, 34);
        _layoutRecalculateDuctworkButton.Margin = new Padding(1, 0, 1, 0);
        _layoutRecalculateDuctworkButton.FlatStyle = FlatStyle.Flat;
        _layoutRecalculateDuctworkButton.Font = new Font("Segoe UI Semibold", 8F);
        _layoutRecalculateDuctworkButton.UseCompatibleTextRendering = true;
        _layoutRecalculateDuctworkButton.BackColor = Color.FromArgb(42, 135, 91);
        _layoutRecalculateDuctworkButton.ForeColor = Color.White;
        foreach (var compactButton in new[]
                 {
                     _layoutDeleteButton, _layoutRearrangeButton, _layoutLabelsButton,
                     _layoutDuctSizesButton, _layoutCenterButton,
                     _layoutCoordinateOriginButton, _layoutSourceSinkEditorButton, _layoutSizeUndefinedButton,
                     _layoutRecalculateDuctworkButton, _layoutCriticalPathButton
                 })
        {
            compactButton.Font = new Font("Segoe UI Semibold", 7F);
            compactButton.UseCompatibleTextRendering = false;
        }
        _layoutSourceSinkEditorButton.Font = new Font("Segoe UI Semibold", 7.2F);
        _layoutSizeUndefinedButton.Font = new Font("Segoe UI Semibold", 7.2F);
        _layoutRecalculateDuctworkButton.Font = new Font("Segoe UI Semibold", 7F);
        _layoutCriticalPathButton.Name = "LayoutCriticalPathButton";
        _layoutCriticalPathButton.Text = "Highlight Critical Path";
        _layoutCriticalPathButton.Size = new Size(180, 34);
        _layoutCriticalPathButton.Margin = new Padding(1, 0, 1, 0);
        _layoutCriticalPathButton.FlatStyle = FlatStyle.Flat;
        _layoutCriticalPathButton.Font = new Font("Segoe UI Semibold", 7F);
        _layoutCriticalPathButton.UseCompatibleTextRendering = false;
        _layoutCriticalPathButton.BackColor = Color.White;
        _layoutCriticalPathButton.ForeColor = Color.FromArgb(28, 69, 108);
        _layoutViewModeSelector.Name = "LayoutViewModeSelector";
        _layoutViewModeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _layoutViewModeSelector.Items.AddRange(new object[]
        {
            "Isometric Positive", "Isometric Negative", "Top-Down"
        });
        _layoutViewModeSelector.Size = new Size(138, 30);
        _layoutViewModeSelector.Margin = new Padding(1, 1, 3, 0);
        _layoutPdfButton.Name = "LayoutPdfBackgroundButton";
        _layoutPdfButton.Text = "Upload PDF";
        _layoutPdfButton.Size = new Size(136, 28);
        _layoutPdfButton.Margin = new Padding(2, 0, 4, 0);
        _layoutPdfButton.FlatStyle = FlatStyle.Flat;
        _layoutPdfButton.BackColor = Color.White;
        _layoutPdfButton.ForeColor = Color.FromArgb(28, 69, 108);
        _layoutPdfButton.Font = new Font("Segoe UI Semibold", 7.4F);
        UiPolish.RoundButton(_layoutUndoButton, 7);
        UiPolish.RoundButton(_layoutRedoButton, 7);
        UiPolish.ApplyHistoryGlyph(_layoutUndoButton, redo: false);
        UiPolish.ApplyHistoryGlyph(_layoutRedoButton, redo: true);
        _layoutToolTips.SetToolTip(_layoutUndoButton, "Undo (Ctrl+Z)");
        _layoutToolTips.SetToolTip(_layoutRedoButton, "Redo (Ctrl+Y)");
        _layoutToolTips.SetToolTip(_layoutDeleteButton, "Delete selected item");
        _layoutToolTips.SetToolTip(_layoutCenterButton, "Center the view on the system start");
        UiPolish.RoundButton(_layoutDeleteButton, 7);
        UiPolish.RoundButton(_layoutRearrangeButton, 7);
        UiPolish.RoundButton(_layoutLabelsButton, 7);
        UiPolish.RoundButton(_layoutDuctSizesButton, 7);
        UiPolish.RoundButton(_layoutCenterButton, 7);
        UiPolish.RoundButton(_layoutCoordinateOriginButton, 7);
        UiPolish.RoundButton(_layoutSourceSinkEditorButton, 7);
        UiPolish.RoundButton(_layoutSizeUndefinedButton, 7);
        UiPolish.RoundButton(_layoutRecalculateDuctworkButton, 7);
        UiPolish.RoundButton(_layoutCriticalPathButton, 7);
        UiPolish.RoundButton(_layoutPdfButton, 7);
        _layoutUndoButton.Click += (_, _) => UndoLayoutChange();
        _layoutRedoButton.Click += (_, _) => RedoLayoutChange();
        _layoutDeleteButton.Click += (_, _) => _layoutCanvas?.DeleteSelectedItem();
        _layoutRearrangeButton.Click += (_, _) => ToggleLayoutRearrangeMode();
        _layoutLabelsButton.Click += (_, _) => ToggleLayoutLabels();
        _layoutDuctSizesButton.Click += (_, _) => ToggleLayoutDuctSizes();
        _layoutCenterButton.Click += (_, _) => CenterLayoutOnStart(showStatus: true);
        _layoutCoordinateOriginButton.Click += (_, _) => ChangeLayoutCoordinateOrigin();
        _layoutSourceSinkEditorButton.Click += (_, _) => OpenSourceSinkEditor();
        _layoutSizeUndefinedButton.Click += (_, _) => SizeUndefinedLayoutItems();
        _layoutRecalculateDuctworkButton.Click += (_, _) => RecalculateAllLayoutDuctwork();
        _layoutCriticalPathButton.Click += (_, _) => ToggleLayoutCriticalPathHighlight();
        _layoutPdfButton.Click += (_, _) => ManageLayoutPdfBackground();
        _layoutViewModeSelector.SelectedIndexChanged += (_, _) => ApplySelectedLayoutViewMode();
        zoomBar.Controls.Add(_layoutUndoButton);
        zoomBar.Controls.Add(_layoutRedoButton);
        zoomBar.Controls.Add(_layoutDeleteButton);
        zoomBar.Controls.Add(_layoutRearrangeButton);
        zoomBar.Controls.Add(_layoutLabelsButton);
        zoomBar.Controls.Add(_layoutDuctSizesButton);
        zoomBar.Controls.Add(_layoutCenterButton);
        zoomBar.Controls.Add(_layoutCoordinateOriginButton);
        zoomBar.Controls.Add(_layoutSourceSinkEditorButton);
        zoomBar.Controls.Add(_layoutSizeUndefinedButton);
        zoomBar.Controls.Add(_layoutRecalculateDuctworkButton);
        var scaleTitle = new Label
        {
            Name = "LayoutScaleTitleLabel",
            Text = "Grid Scale:",
            AutoSize = true,
            Margin = new Padding(2, 8, 1, 0),
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI Semibold", 8F)
        };
        _layoutScaleInput.Name = "LayoutScaleInput";
        _layoutScaleInput.Text = "1";
        _layoutScaleInput.TextAlign = HorizontalAlignment.Right;
        _layoutScaleInput.Size = new Size(72, 28);
        _layoutScaleInput.Margin = new Padding(2, 2, 2, 0);
        var scaleLabel = new Label
        {
            Name = "LayoutScaleLabel",
            Text = "ft/cell",
            AutoSize = true,
            Margin = new Padding(2, 8, 0, 0),
            ForeColor = Color.FromArgb(72, 92, 110)
        };
        _layoutViewModeSelector.Font = new Font("Segoe UI", 7.2F);
        zoomBar.Controls.Add(_layoutCriticalPathButton);
        UpdateLayoutHistoryButtons();

        var canvasHost = new Panel
        {
            Name = "LayoutCanvasHost",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(250, 252, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };
        _layoutCanvasHost = canvasHost;
        _layoutCanvas = new LayoutDesignerCanvas
        {
            Name = "LayoutDesignerCanvas",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(250, 252, 252),
            NodeSource = () => _ductSystem.LayoutNodes,
            CoordinateSource = () => LayoutCoordinateResolver.Resolve(_ductSystem),
            CaptionSource = LayoutNodeCaption,
            ConnectionCaptionSource = LayoutConnectionCaption,
            ConnectionSizeCaptionSource = LayoutConnectionSizeCaption,
            ConnectionSizedSource = connection =>
                DuctForConnection(connection.Downstream, connection.Secondary) is { RequiresResizing: false },
            ConnectionFlowReversedSource = LayoutConnectionFlowReversed,
            CriticalPathNodeSource = IsLayoutNodeOnCriticalPath,
            CriticalPathConnectionSource = IsLayoutConnectionOnCriticalPath,
            PdfBackgroundSource = () => _ductSystem.LayoutPdfBackground
        };
        _layoutCanvas.ToolDropped += (type, group, x, y) => PlaceLayoutNode(type, x, y, group);
        _layoutCanvas.NodeMoved += MoveLayoutNode;
        _layoutCanvas.NodeActivated += ActivateLayoutNode;
        _layoutCanvas.ConnectionActivated += ActivateLayoutConnection;
        _layoutCanvas.ConnectionDeleteRequested += RemoveLayoutConnection;
        _layoutCanvas.PortConnectionRequested += ConnectLayoutPorts;
        _layoutCanvas.NodeDeleteRequested += RemoveLayoutNode;
        _layoutCanvas.SelectionChanged += node =>
        {
            _layoutDeleteButton.Enabled = node is not null;
            _layoutCoordinateOriginButton.Enabled = node is not null && node.Type != LayoutNodeType.CalculationPoint;
            SyncLayoutSummarySelection(node);
            if (node is not null)
                _layoutStatusLabel.Text = $"Selected {node.Id}. Press Delete or use the Delete button to remove it.";
        };
        _layoutCanvas.ConnectionSelectionChanged += connection =>
        {
            if (connection is LayoutConnectionRef selected)
            {
                SyncLayoutSummarySelection(connection: selected);
                _layoutDeleteButton.Enabled = true;
                var ductId = LayoutConnectionCaption(selected).Replace("\r", string.Empty)
                    .Split('\n').FirstOrDefault();
                _layoutStatusLabel.Text = $"Selected {(!string.IsNullOrWhiteSpace(ductId) ? ductId : "unsized duct")}. Double-click to edit or press Delete to remove it.";
            }
        };
        _layoutCanvas.ZoomChanged += factor => zoomValue.Text = $"{factor:P0}";
        _layoutCanvas.EmptyGridClicked += point =>
        {
            if (_selectedLayoutTool is not LayoutNodeType type)
                return;
            PlaceLayoutNode(type, point.X, point.Y, _selectedLayoutFittingGroup);
            _selectedLayoutTool = null;
            _selectedLayoutFittingGroup = null;
            _layoutCanvas.SelectedTool = null;
        };
        canvasHost.Controls.Add(_layoutCanvas);
        canvasHost.VisibleChanged += (_, _) => EnsureLayoutViewportPositioned();
        canvasHost.Resize += (_, _) =>
        {
            if (!_layoutViewportInitialized)
                EnsureLayoutViewportPositioned();
        };
        _layoutScaleInput.Validated += (_, _) => ApplyLayoutScaleInput();
        _layoutScaleInput.KeyDown += (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            ApplyLayoutScaleInput();
            args.Handled = true;
            args.SuppressKeyPress = true;
        };
        leftHost.Controls.Add(canvasHost, 0, 0);

        var palette = new RoundedPanel
        {
            Name = "LayoutToolPalette",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderColor = UiPalette.CardBorder,
            CornerRadius = 12,
            Padding = new Padding(8)
        };
        var paletteLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        paletteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var paletteHeader = new CardHeaderPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        paletteHeader.Controls.Add(new Label
        {
            Text = "TOOLS",
            Dock = DockStyle.Left,
            Width = 70,
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI Semibold", 7.2F),
            TextAlign = ContentAlignment.MiddleLeft
        });
        var collapseToolbox = new Button
        {
            Name = "LayoutToolboxCollapseButton",
            Text = ">",
            Dock = DockStyle.Right,
            Width = 22,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleCenter
        };
        collapseToolbox.UseCompatibleTextRendering = true;
        collapseToolbox.Padding = Padding.Empty;
        collapseToolbox.FlatAppearance.BorderSize = 0;
        collapseToolbox.Click += (_, _) => SetLayoutToolboxCollapsed(true);
        paletteHeader.Controls.Add(collapseToolbox);
        collapseToolbox.BringToFront();
        paletteLayout.Controls.Add(paletteHeader, 0, 0);
        var toolList = new FlowLayoutPanel
        {
            Name = "LayoutToolList",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(2),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        AddLayoutTool(toolList, LayoutNodeType.Reservoir, "Source", "Space Source: airflow, temperature, and source pressure", "Space Source");
        AddLayoutTool(toolList, LayoutNodeType.Reservoir, "Sink", "Space Sink: calculated terminal airflow and temperature", "Space Sink");
        AddLayoutTool(toolList, LayoutNodeType.Fan, "Fan", "Airflow, temperature, air system");
        AddLayoutTool(toolList, LayoutNodeType.Coil, "Coil", "Temperature and pressure drop");
        AddLayoutTool(toolList, LayoutNodeType.Filter, "Filter", "Pressure-drop input");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Elbow", "ASHRAE elbow", "Elbows");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Transition", "ASHRAE transition", "Transitions");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Split", "Diverting fitting: one inlet, two outlets", "Diverting Fittings");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Merge", "Converging fitting: two inlets, one outlet", "Converging Fittings");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Damper", "ASHRAE damper", "Dampers");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "VAV", "Titus DESV VAV with or without hot-water reheat",
            FittingCatalog.VavSelectionGroup);
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Terminal", "Entries, exits, terminals and takeoffs",
            FittingCatalog.TerminalSelectionGroup);
        AddLayoutTool(toolList, LayoutNodeType.Grd, "GRD", "Grille, square diffuser, or linear diffuser", "GRDs");
        AddLayoutTool(toolList, LayoutNodeType.Fitting, "Fan/Plenum", "Fan exits and plenum connections",
            FittingCatalog.FanExitsAndPlenumsSelectionGroup);
        AddLayoutTool(toolList, LayoutNodeType.CustomComponent, "Custom", "User-defined component label and static pressure drop");
        AddLayoutTool(toolList, LayoutNodeType.CalculationPoint, "Pressure", "Pressure calculation point", "Pressure");
        AddLayoutTool(toolList, LayoutNodeType.CalculationPoint, "Temp", "Temperature calculation point", "Temperature");
        AddLayoutTool(toolList, LayoutNodeType.CalculationPoint, "Velocity", "Velocity calculation point", "Velocity");
        AddLayoutTool(toolList, LayoutNodeType.CalculationPoint, "Airflow", "Airflow calculation point", "Airflow");
        paletteLayout.Controls.Add(toolList, 0, 1);
        palette.Controls.Add(paletteLayout);

        _layoutWorkspaceSplitter = new SplitContainer
        {
            Name = "LayoutWorkspaceSplitter",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 330,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1,
            BackColor = Color.FromArgb(215, 224, 231)
        };
        _layoutWorkspaceSplitter.SizeChanged += (_, _) =>
        {
            if (_layoutWorkspaceSplitter.Width < 650)
                return;
            _layoutWorkspaceSplitter.Panel1MinSize = 180;
            _updatingLayoutSummarySplitter = true;
            try
            {
                if (!_layoutWorkspaceSplitter.Panel1Collapsed && !_layoutSummaryInitialized)
                {
                    _layoutWorkspaceSplitter.SplitterDistance = Math.Clamp(_layoutSummaryWidth, 180,
                        Math.Max(180, _layoutWorkspaceSplitter.Width - 360 - _layoutWorkspaceSplitter.SplitterWidth));
                    _layoutSummaryInitialized = true;
                }
                else if (!_layoutWorkspaceSplitter.Panel1Collapsed)
                    _layoutWorkspaceSplitter.SplitterDistance = Math.Clamp(_layoutSummaryWidth,
                        180, Math.Max(180, _layoutWorkspaceSplitter.Width - 360 - _layoutWorkspaceSplitter.SplitterWidth));
                _layoutWorkspaceSplitter.Panel2MinSize = 360;
            }
            finally
            {
                _updatingLayoutSummarySplitter = false;
            }
        };
        _layoutWorkspaceSplitter.SplitterMoved += (_, _) =>
        {
            if (_layoutSummaryInitialized && !_updatingLayoutSummarySplitter && !_layoutWorkspaceSplitter.Panel1Collapsed
                && _layoutWorkspaceSplitter.Width >= 650)
                _layoutSummaryWidth = _layoutWorkspaceSplitter.SplitterDistance;
        };
        _layoutWorkspaceSplitter.Panel1.Controls.Add(BuildLayoutSystemSummaryPanel());
        var canvasArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _layoutSummaryReopenButton.Name = "LayoutSummaryReopenButton";
        _layoutSummaryReopenButton.Text = "SYSTEM QUICKVIEW";
        _layoutSummaryReopenButton.Dock = DockStyle.Fill;
        _layoutSummaryReopenButton.Visible = false;
        _layoutSummaryReopenButton.FlatStyle = FlatStyle.Flat;
        _layoutSummaryReopenButton.ForeColor = Color.FromArgb(28, 69, 108);
        _layoutSummaryReopenButton.BackColor = Color.FromArgb(235, 242, 247);
        _layoutSummaryReopenButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _layoutSummaryReopenButton.Click += (_, _) => SetLayoutSummaryCollapsed(false);
        _layoutToolboxReopenButton.Name = "LayoutToolboxReopenButton";
        _layoutToolboxReopenButton.Text = "TOOLBOX";
        _layoutToolboxReopenButton.Dock = DockStyle.Fill;
        _layoutToolboxReopenButton.Visible = false;
        _layoutToolboxReopenButton.FlatStyle = FlatStyle.Flat;
        _layoutToolboxReopenButton.ForeColor = Color.FromArgb(28, 69, 108);
        _layoutToolboxReopenButton.BackColor = Color.FromArgb(235, 242, 247);
        _layoutToolboxReopenButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _layoutToolboxReopenButton.Click += (_, _) => SetLayoutToolboxCollapsed(false);
        canvasArea.Controls.Add(_layoutSummaryReopenButton, 0, 0);
        canvasArea.Controls.Add(leftHost, 1, 0);
        canvasArea.Controls.Add(_layoutToolboxReopenButton, 2, 0);

        _layoutToolboxSplitter = new SplitContainer
        {
            Name = "LayoutToolboxSplitter",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel2,
            BackColor = Color.FromArgb(215, 224, 231)
        };
        _layoutToolboxSplitter.Panel1MinSize = 360;
        _layoutToolboxSplitter.Panel2MinSize = 88;
        _layoutToolboxSplitter.Panel1.Controls.Add(canvasArea);
        _layoutToolboxSplitter.Panel2.Controls.Add(palette);
        _layoutToolboxSplitter.SizeChanged += (_, _) => InitializeLayoutToolboxSplitter();
        _layoutToolboxSplitter.SplitterMoved += (_, _) =>
        {
            if (_layoutToolboxInitialized && !_updatingLayoutToolboxSplitter
                && !_layoutToolboxSplitter.Panel2Collapsed && _layoutToolboxSplitter.Width >= 560)
                _layoutToolboxWidth = Math.Max(96,
                    _layoutToolboxSplitter.Width - _layoutToolboxSplitter.SplitterDistance
                    - _layoutToolboxSplitter.SplitterWidth);
        };
        _layoutWorkspaceSplitter.Panel2.Controls.Add(_layoutToolboxSplitter);

        workspace.Controls.Add(zoomBar, 0, 0);
        workspace.Controls.Add(_layoutWorkspaceSplitter, 0, 1);

        _layoutStatusLabel.Name = "LayoutDesignerStatus";
        _layoutStatusLabel.Dock = DockStyle.Fill;
        _layoutStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _layoutStatusLabel.ForeColor = Color.FromArgb(72, 92, 110);
        _layoutStatusLabel.Text = "Drag between green outlet and inlet circles to connect items. Red ducts are valid but unsized; green ducts are sized. Double-click to edit.";
        _layoutStatusLabel.AutoEllipsis = true;

        var statusBar = new TableLayoutPanel
        {
            Name = "LayoutDesignerStatusBar",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = BackColor
        };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var viewControls = new FlowLayoutPanel
        {
            Name = "LayoutViewControls",
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        scaleTitle.Margin = new Padding(2, 6, 1, 0);
        _layoutScaleInput.Margin = new Padding(2, 0, 2, 0);
        scaleLabel.Margin = new Padding(2, 6, 6, 0);
        _layoutViewModeSelector.Margin = new Padding(2, 0, 0, 0);
        viewControls.Controls.Add(_layoutPdfButton);
        viewControls.Controls.Add(scaleTitle);
        viewControls.Controls.Add(_layoutScaleInput);
        viewControls.Controls.Add(scaleLabel);
        viewControls.Controls.Add(_layoutViewModeSelector);
        statusBar.Controls.Add(_layoutStatusLabel, 0, 0);
        statusBar.Controls.Add(viewControls, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(workspace, 0, 1);
        root.Controls.Add(statusBar, 0, 2);
        return root;
    }

    private Control BuildLayoutSystemSummaryPanel()
    {
        var panel = new RoundedPanel
        {
            Name = "LayoutSystemSummaryPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderColor = UiPalette.CardBorder,
            CornerRadius = 10,
            Padding = new Padding(8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new CardHeaderPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        header.Controls.Add(new Label
        {
            Text = "DUCT SYSTEM QUICK VIEW",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9.2F),
            ForeColor = Color.FromArgb(34, 62, 88)
        });
        var collapse = new Button
        {
            Name = "LayoutSummaryCollapseButton",
            Text = "‹",
            Dock = DockStyle.Right,
            Width = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(28, 69, 108)
        };
        collapse.Click += (_, _) => SetLayoutSummaryCollapsed(true);
        header.Controls.Add(collapse);
        layout.Controls.Add(header, 0, 0);

        _layoutSystemSummaryGrid.Name = "LayoutSystemSummaryGrid";
        _layoutSystemSummaryGrid.Dock = DockStyle.Fill;
        _layoutSystemSummaryGrid.ReadOnly = true;
        _layoutSystemSummaryGrid.AllowUserToAddRows = false;
        _layoutSystemSummaryGrid.AllowUserToDeleteRows = false;
        _layoutSystemSummaryGrid.AllowUserToResizeRows = false;
        _layoutSystemSummaryGrid.RowHeadersVisible = false;
        _layoutSystemSummaryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _layoutSystemSummaryGrid.MultiSelect = false;
        _layoutSystemSummaryGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _layoutSystemSummaryGrid.RowTemplate.Height = 27;
        _layoutSystemSummaryGrid.BackgroundColor = Color.White;
        _layoutSystemSummaryGrid.BorderStyle = BorderStyle.None;
        _layoutSystemSummaryGrid.EnableHeadersVisualStyles = false;
        _layoutSystemSummaryGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 77, 109);
        _layoutSystemSummaryGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _layoutSystemSummaryGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F);
        _layoutSystemSummaryGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8F);
        _layoutSystemSummaryGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 246);
        _layoutSystemSummaryGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 49, 69);
        _layoutSystemSummaryGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 252);
        _layoutSystemSummaryGrid.SelectionChanged += (_, _) => SelectLayoutItemFromSummary();
        AddColumn("Id", "Item ID", 72);
        AddColumn("StaticDrop", "SP Drop", 72);
        AddColumn("Airflow", "Airflow", 70);
        AddColumn("Upstream", "Upstream", 76);
        AddColumn("Downstream", "Downstream", 82);
        AddColumn("AbsoluteX", "Abs X", 56);
        AddColumn("AbsoluteY", "Abs Y", 56);
        AddColumn("AbsoluteZ", "Abs Z", 56);
        layout.Controls.Add(_layoutSystemSummaryGrid, 0, 1);
        panel.Controls.Add(layout);
        return panel;

        void AddColumn(string name, string headerText, int width)
        {
            _layoutSystemSummaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
    }

    private void SetLayoutSummaryCollapsed(bool collapsed)
    {
        if (_layoutWorkspaceSplitter is null)
            return;
        var restoreWidth = _layoutSummaryWidth;
        if (collapsed && !_layoutWorkspaceSplitter.Panel1Collapsed)
        {
            _layoutSummaryWidth = Math.Max(180, _layoutWorkspaceSplitter.SplitterDistance);
            restoreWidth = _layoutSummaryWidth;
        }
        _updatingLayoutSummarySplitter = true;
        try
        {
            _layoutWorkspaceSplitter.Panel1Collapsed = collapsed;
            if (!collapsed && _layoutWorkspaceSplitter.Width >= 650)
                _layoutWorkspaceSplitter.SplitterDistance = Math.Clamp(restoreWidth, 180,
                    Math.Max(180, _layoutWorkspaceSplitter.Width - 360 - _layoutWorkspaceSplitter.SplitterWidth));
        }
        finally
        {
            _updatingLayoutSummarySplitter = false;
        }
        _layoutSummaryReopenButton.Visible = collapsed;
        if (_layoutSummaryReopenButton.Parent is TableLayoutPanel canvasArea)
            canvasArea.ColumnStyles[0].Width = collapsed ? 34 : 0;
    }

    private void InitializeLayoutToolboxSplitter()
    {
        if (_layoutToolboxSplitter is null || _layoutToolboxSplitter.Width < 560
            || _layoutToolboxSplitter.Panel2Collapsed)
            return;
        _updatingLayoutToolboxSplitter = true;
        try
        {
            var width = Math.Clamp(_layoutToolboxWidth, 96,
                Math.Max(96, _layoutToolboxSplitter.Width - 360 - _layoutToolboxSplitter.SplitterWidth));
            _layoutToolboxSplitter.SplitterDistance = _layoutToolboxSplitter.Width
                                                     - width - _layoutToolboxSplitter.SplitterWidth;
            _layoutToolboxInitialized = true;
        }
        finally
        {
            _updatingLayoutToolboxSplitter = false;
        }
    }

    private void SetLayoutToolboxCollapsed(bool collapsed)
    {
        if (_layoutToolboxSplitter is null)
            return;
        if (collapsed && !_layoutToolboxSplitter.Panel2Collapsed)
            _layoutToolboxWidth = Math.Max(96,
                _layoutToolboxSplitter.Width - _layoutToolboxSplitter.SplitterDistance
                - _layoutToolboxSplitter.SplitterWidth);
        _updatingLayoutToolboxSplitter = true;
        try
        {
            _layoutToolboxSplitter.Panel2Collapsed = collapsed;
            if (!collapsed)
                InitializeLayoutToolboxSplitter();
        }
        finally
        {
            _updatingLayoutToolboxSplitter = false;
        }
        _layoutToolboxReopenButton.Visible = collapsed;
        if (_layoutToolboxReopenButton.Parent is TableLayoutPanel canvasArea)
            canvasArea.ColumnStyles[2].Width = collapsed ? 34 : 0;
    }

    private void RefreshLayoutSystemSummary()
    {
        if (_layoutSystemSummaryGrid.Columns.Count == 0 || _ductGrid.Columns.Count == 0)
            return;
        _layoutSystemSummaryGrid.Rows.Clear();
        foreach (DataGridViewRow source in _ductGrid.Rows)
        {
            if (source.IsNewRow)
                continue;
            var rowIndex = _layoutSystemSummaryGrid.Rows.Add(
                source.Cells["Id"].Value,
                source.Cells["StaticDrop"].Value,
                source.Cells["Airflow"].Value,
                source.Cells["UpstreamId"].Value,
                source.Cells["DownstreamId"].Value,
                source.Cells["AbsoluteX"].Value,
                source.Cells["AbsoluteY"].Value,
                source.Cells["AbsoluteZ"].Value);
            _layoutSystemSummaryGrid.Rows[rowIndex].Tag = source.Tag;
            if (source.DefaultCellStyle.BackColor == Color.FromArgb(255, 237, 213))
            {
                _layoutSystemSummaryGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 237, 213);
                _layoutSystemSummaryGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(150, 74, 0);
            }
        }
        SyncLayoutSummarySelection();
    }

    private void SelectLayoutItemFromSummary()
    {
        if (_syncingLayoutSelection || _layoutCanvas is null || _layoutSystemSummaryGrid.SelectedRows.Count == 0)
            return;

        var tag = _layoutSystemSummaryGrid.SelectedRows[0].Tag;
        _syncingLayoutSelection = true;
        try
        {
            if (tag is SystemRowTag { IsFitting: false } ductTag)
            {
                var connectionNode = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                    x.ConnectionDuctSequence == ductTag.Sequence || x.SecondaryConnectionDuctSequence == ductTag.Sequence);
                if (connectionNode is not null)
                {
                    var secondary = connectionNode.SecondaryConnectionDuctSequence == ductTag.Sequence;
                    _layoutCanvas.SelectConnection(new LayoutConnectionRef(connectionNode, secondary));
                    return;
                }
            }

            _layoutCanvas.SelectNode(LayoutNodeForSystemTag(tag));
        }
        finally
        {
            _syncingLayoutSelection = false;
        }
    }

    private SavedLayoutNode? LayoutNodeForSystemTag(object? tag) => tag switch
    {
        SavedLayoutNode node => node,
        SystemRowTag { IsFitting: true } fittingTag => _ductSystem.LayoutNodes
            .FirstOrDefault(x => x.LinkedFittingSequence == fittingTag.Sequence),
        SystemRowTag ductTag => _ductSystem.LayoutNodes.FirstOrDefault(x =>
            x.LinkedDuctSequence == ductTag.Sequence || x.ConnectionDuctSequence == ductTag.Sequence
                                                    || x.SecondaryConnectionDuctSequence == ductTag.Sequence),
        _ => null
    };

    private void SyncLayoutSummarySelection(SavedLayoutNode? node = null, LayoutConnectionRef? connection = null)
    {
        if (_syncingLayoutSelection || _layoutSystemSummaryGrid.Columns.Count == 0)
            return;

        var ductSequence = connection is LayoutConnectionRef selectedConnection
            ? (selectedConnection.Secondary
                ? selectedConnection.Downstream.SecondaryConnectionDuctSequence
                : selectedConnection.Downstream.ConnectionDuctSequence)
            : null;
        _syncingLayoutSelection = true;
        try
        {
            _layoutSystemSummaryGrid.ClearSelection();
            foreach (DataGridViewRow row in _layoutSystemSummaryGrid.Rows)
            {
                var matches = ductSequence is int selectedDuct
                    ? row.Tag is SystemRowTag { IsFitting: false } ductTag && ductTag.Sequence == selectedDuct
                    : node is not null && LayoutSystemTagMatchesNode(row.Tag, node);
                if (!matches)
                    continue;
                row.Selected = true;
                if (row.Cells.Count > 0)
                    _layoutSystemSummaryGrid.CurrentCell = row.Cells[0];
                if (row.Index >= 0)
                    _layoutSystemSummaryGrid.FirstDisplayedScrollingRowIndex = row.Index;
                break;
            }
        }
        finally
        {
            _syncingLayoutSelection = false;
        }
    }

    private static bool LayoutSystemTagMatchesNode(object? tag, SavedLayoutNode node) => tag switch
    {
        SavedLayoutNode savedNode => ReferenceEquals(savedNode, node),
        SystemRowTag { IsFitting: true } fittingTag => node.LinkedFittingSequence == fittingTag.Sequence,
        SystemRowTag { IsFitting: false } ductTag => node.LinkedDuctSequence == ductTag.Sequence,
        _ => false
    };

    private void AddLayoutTool(Control parent, LayoutNodeType type, string title, string detail, string? fittingGroup = null)
    {
        var tool = new LayoutToolCard(type, title, detail, fittingGroup)
        {
            Name = fittingGroup is null
                ? $"LayoutTool{type}"
                : $"LayoutTool{new string(fittingGroup.Where(char.IsLetterOrDigit).ToArray())}",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 5, 5)
        };
        _layoutToolTips.SetToolTip(tool, $"{title}\r\n{detail}");
        tool.ToolClicked += (clickedType, clickedGroup) =>
        {
            _selectedLayoutTool = clickedType;
            _selectedLayoutFittingGroup = clickedGroup;
            if (_layoutCanvas is not null)
                _layoutCanvas.SelectedTool = clickedType;
            _layoutStatusLabel.Text = $"{title} selected. Click a grid intersection to place it, or drag this card onto the grid.";
        };
        if (parent is TableLayoutPanel table)
        {
            var index = table.Controls.Count;
            table.Controls.Add(tool, index % 2, index / 2);
        }
        else if (parent is FlowLayoutPanel)
        {
            tool.Dock = DockStyle.None;
            tool.Size = new Size(46, 46);
            tool.Margin = new Padding(1, 0, 3, 0);
            parent.Controls.Add(tool);
        }
        else
        {
            parent.Controls.Add(tool);
        }
    }

    private string CaptureLayoutHistoryState()
    {
        // The rendered construction sheet can be several megabytes and the company logo is project
        // identity, not a Designer edit. Keep both persistent in the project file, but do not duplicate
        // them into every in-memory undo entry (RestoreLayoutHistoryState restores both from the current project).
        var pdfBackground = _ductSystem.LayoutPdfBackground;
        var logo = _ductSystem.CompanyLogoPngBase64;
        _ductSystem.LayoutPdfBackground = null;
        _ductSystem.CompanyLogoPngBase64 = string.Empty;
        try
        {
            return JsonSerializer.Serialize(_ductSystem, LayoutHistoryJsonOptions);
        }
        finally
        {
            _ductSystem.LayoutPdfBackground = pdfBackground;
            _ductSystem.CompanyLogoPngBase64 = logo;
        }
    }

    private void CommitLayoutHistoryState(string beforeState)
    {
        var afterState = CaptureLayoutHistoryState();
        if (string.Equals(beforeState, afterState, StringComparison.Ordinal))
            return;
        _layoutUndoHistory.Push(beforeState);
        _layoutRedoHistory.Clear();
        UpdateLayoutHistoryButtons();
    }

    private void UndoLayoutChange()
    {
        if (_layoutUndoHistory.Count == 0)
            return;
        _layoutRedoHistory.Push(CaptureLayoutHistoryState());
        RestoreLayoutHistoryState(_layoutUndoHistory.Pop(), "Undid the last Designer change.");
    }

    private void RedoLayoutChange()
    {
        if (_layoutRedoHistory.Count == 0)
            return;
        _layoutUndoHistory.Push(CaptureLayoutHistoryState());
        RestoreLayoutHistoryState(_layoutRedoHistory.Pop(), "Redid the Designer change.");
    }

    private void RestoreLayoutHistoryState(string state, string status)
    {
        var pdfBackground = _ductSystem.LayoutPdfBackground;
        var restored = JsonSerializer.Deserialize<DuctSystemDocument>(state, LayoutHistoryJsonOptions)
                       ?? new DuctSystemDocument();
        restored.LayoutPdfBackground = pdfBackground;
        restored.UserName = _ductSystem.UserName;
        restored.ProjectName = _ductSystem.ProjectName;
        restored.ProjectNumber = _ductSystem.ProjectNumber;
        restored.ProjectDate = _ductSystem.ProjectDate;
        restored.LastEditedUtc = _ductSystem.LastEditedUtc;
        restored.CompanyLogoPngBase64 = _ductSystem.CompanyLogoPngBase64;
        restored.DefaultSizingIncrementM = _ductSystem.DefaultSizingIncrementM;
        restored.DefaultSizeRoundingDirection = _ductSystem.DefaultSizeRoundingDirection;
        restored.DefaultEquivalentDiameterMethod = _ductSystem.DefaultEquivalentDiameterMethod;
        restored.DefaultPressureLossBasis = _ductSystem.DefaultPressureLossBasis;
        _ductSystem = restored;
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
        ResetCriticalPath();
        ApplyProjectCalculationSettings();
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        _layoutStatusLabel.Text = status;
        _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
        UpdateLayoutHistoryButtons();
    }

    private void UpdateLayoutHistoryButtons()
    {
        _layoutUndoButton.Enabled = _layoutUndoHistory.Count > 0;
        _layoutRedoButton.Enabled = _layoutRedoHistory.Count > 0;
    }

    private void ToggleLayoutRearrangeMode()
    {
        _layoutRearrangeMode = !_layoutRearrangeMode;
        if (_layoutCanvas is not null)
        {
            _layoutCanvas.RearrangeMode = _layoutRearrangeMode;
            _layoutCanvas.SelectedTool = null;
        }
        _selectedLayoutTool = null;
        _selectedLayoutFittingGroup = null;
        _layoutRearrangeButton.Text = _layoutRearrangeMode ? "Done" : "Move";
        _layoutRearrangeButton.BackColor = _layoutRearrangeMode
            ? Color.FromArgb(31, 120, 180)
            : Color.White;
        _layoutRearrangeButton.ForeColor = _layoutRearrangeMode
            ? Color.White
            : Color.FromArgb(34, 62, 88);
        _layoutRearrangeButton.FlatAppearance.BorderColor = _layoutRearrangeMode
            ? Color.FromArgb(31, 120, 180)
            : Color.FromArgb(180, 194, 207);
        _layoutStatusLabel.Text = _layoutRearrangeMode
            ? "Move mode is active. Drag components to clean up the drawing; all existing connections and calculations remain unchanged."
            : "Move mode ended. Component placement, connection, selection, and editing are active again.";
        _layoutStatusLabel.ForeColor = _layoutRearrangeMode
            ? Color.FromArgb(31, 120, 180)
            : Color.FromArgb(72, 92, 110);
    }

    private void ToggleLayoutLabels()
    {
        if (_layoutCanvas is null)
            return;
        _layoutCanvas.ShowLabels = !_layoutCanvas.ShowLabels;
        _layoutLabelsButton.Text = _layoutCanvas.ShowLabels ? "Hide Labels" : "Show Labels";
        _layoutCanvas.Invalidate();
        _layoutStatusLabel.Text = _layoutCanvas.ShowLabels
            ? "Component and duct item labels are visible."
            : "Component and duct item labels are hidden.";
    }

    private void ToggleLayoutDuctSizes()
    {
        if (_layoutCanvas is null)
            return;
        _layoutCanvas.ShowDuctSizes = !_layoutCanvas.ShowDuctSizes;
        _layoutDuctSizesButton.Text = _layoutCanvas.ShowDuctSizes ? "Hide Duct Sizes" : "Show Duct Sizes";
        _layoutCanvas.Invalidate();
        _layoutStatusLabel.Text = _layoutCanvas.ShowDuctSizes
            ? "Duct sizes are displayed beside their runs."
            : "Duct size labels are hidden.";
    }

    private void ManageLayoutPdfBackground()
    {
        if (_ductSystem.LayoutPdfBackground is null)
        {
            ImportLayoutPdfBackground();
            return;
        }

        var background = _ductSystem.LayoutPdfBackground;
        _layoutPdfMenu?.Dispose();
        var menu = new ContextMenuStrip
        {
            Font = new Font("Segoe UI", 9F),
            ShowImageMargin = false,
            Renderer = new ToolStripProfessionalRenderer()
        };
        _layoutPdfMenu = menu;
        menu.Items.Add(new ToolStripMenuItem(
            $"{background.SourceFileName} - page {background.PageNumber} of {background.PageCount}")
        {
            Enabled = false,
            Font = new Font("Segoe UI Semibold", 8.5F)
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(background.Visible ? "Hide PDF" : "Show PDF", null,
            (_, _) => RunLayoutPdfCommand(ToggleLayoutPdfVisibility));
        menu.Items.Add("Move / Change Scale...", null,
            (_, _) => RunLayoutPdfCommand(EditLayoutPdfBackground));
        if (_ductSystem.LayoutViewMode != LayoutViewMode.TopDown)
            menu.Items.Add("Switch to Top-Down View", null,
                (_, _) => RunLayoutPdfCommand(SwitchLayoutToTopDown));
        menu.Items.Add("Replace PDF...", null,
            (_, _) => RunLayoutPdfCommand(ImportLayoutPdfBackground));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Delete PDF", null,
            (_, _) => RunLayoutPdfCommand(DeleteLayoutPdfBackground));
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_layoutPdfMenu, menu))
                _layoutPdfMenu = null;
            // A ToolStrip item raises Click while its owner is closing. Disposing the
            // menu synchronously here invalidates the item before its command runs.
            // Queue disposal until the current UI event has completed.
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(menu.Dispose));
            else
                menu.Dispose();
        };
        menu.Show(_layoutPdfButton, new Point(0, _layoutPdfButton.Height));
    }

    private void RunLayoutPdfCommand(Action command)
    {
        try
        {
            command();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or InvalidOperationException or ArgumentException)
        {
            _layoutStatusLabel.Text = $"The PDF background could not be updated: {ex.Message}";
            _layoutStatusLabel.ForeColor = Color.Firebrick;
            MessageBox.Show(this, ex.Message, "PDF Background",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportLayoutPdfBackground()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Select a construction-sheet PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            using var session = new PdfPageRenderSession(picker.FileName);
            using var dialog = new PdfBackgroundImportDialog(picker.FileName, session);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Background is null)
                return;
            _ductSystem.LayoutPdfBackground = dialog.Background;
            _ductSystem.LayoutViewMode = LayoutViewMode.TopDown;
            ApplyLayoutScaleSettings();
            SaveDuctSystem();
            RefreshLayoutDesigner();
            _layoutStatusLabel.Text = $"PDF page {dialog.Background.PageNumber} imported at "
                                      + $"{dialog.Background.PaperInches:0.###} in = {dialog.Background.DrawingFeet:0.###} ft. "
                                      + "The top-down grid and system are overlaid on the sheet.";
            _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException
                                   or DllNotFoundException or BadImageFormatException)
        {
            MessageBox.Show(this, ex.Message, "PDF Background", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleLayoutPdfVisibility()
    {
        if (_ductSystem.LayoutPdfBackground is not { } background)
            return;
        background.Visible = !background.Visible;
        SaveDuctSystem();
        RefreshLayoutDesigner();
        _layoutStatusLabel.Text = background.Visible
            ? "The PDF background is visible in Top-Down view."
            : "The PDF background is hidden.";
    }

    private void EditLayoutPdfBackground()
    {
        if (_ductSystem.LayoutPdfBackground is not { } background)
            return;
        using var dialog = new PdfBackgroundPropertiesDialog(background);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        dialog.ApplyTo(background);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        _layoutStatusLabel.Text = "PDF scale and center location updated. The sheet remains proportional to the current grid scale.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private void DeleteLayoutPdfBackground()
    {
        if (_ductSystem.LayoutPdfBackground is null)
            return;
        var confirmed = AutomationOptions.SuppressDialogs
                        || MessageBox.Show(this, "Remove the imported PDF background from this project?",
                            "Delete PDF Background", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) == DialogResult.Yes;
        if (!confirmed)
            return;
        _ductSystem.LayoutPdfBackground = null;
        SaveDuctSystem();
        RefreshLayoutDesigner();
        _layoutStatusLabel.Text = "The PDF background was removed. Layout components and coordinates were not changed.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(72, 92, 110);
    }

    private void SwitchLayoutToTopDown()
    {
        if (_ductSystem.LayoutViewMode == LayoutViewMode.TopDown)
            return;
        _layoutViewModeSelector.SelectedIndex = 2;
    }

    private void UpdateLayoutPdfButton()
    {
        var background = _ductSystem.LayoutPdfBackground;
        _layoutPdfButton.Text = background is null ? "Upload PDF"
            : background.Visible ? "PDF Background" : "PDF Hidden";
        _layoutPdfButton.BackColor = background is null ? Color.White
            : background.Visible ? Color.FromArgb(225, 239, 247) : Color.FromArgb(239, 241, 243);
        _layoutToolTips.SetToolTip(_layoutPdfButton, background is null
            ? "Import a PDF sheet as a scaled Top-Down background"
            : "Show, hide, move, rescale, replace, or delete the imported PDF background");
    }

    private void EnsureLayoutViewportPositioned(bool force = false)
    {
        if (_layoutCanvasHost is not { IsDisposed: false, Visible: true } host
            || _layoutCanvas is not { IsDisposed: false } canvas
            || host.ClientSize.Width <= 0 || host.ClientSize.Height <= 0)
            return;

        var start = LayoutStartNode();
        var startSignature = LayoutStartSignature(start);
        var firstSystemPlacement = _layoutViewportInitialized
                                   && string.Equals(_layoutViewportStartSignature, "empty", StringComparison.Ordinal)
                                   && !string.Equals(startSignature, "empty", StringComparison.Ordinal);
        if (_layoutViewportInitialized && !force && !firstSystemPlacement)
            return;
        var startGrid = start is null ? Point.Empty : EffectiveLayoutGridPoint(start);
        canvas.CenterOnGrid(startGrid.X, startGrid.Y);
        _layoutViewportInitialized = true;
        _layoutViewportStartSignature = startSignature;
    }

    private SavedLayoutNode? LayoutStartNode() => _ductSystem.LayoutNodes
        .Where(node => string.IsNullOrWhiteSpace(node.ParentNodeId)
                       && string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
        .OrderBy(node => node.Sequence)
        .FirstOrDefault();

    private static string LayoutStartSignature(SavedLayoutNode? start) => start is null
        ? "empty"
        : $"{start.Id}:{start.GridX}:{start.GridY}";

    private void CenterLayoutOnStart(bool showStatus)
    {
        if (_layoutCanvasHost is not { IsDisposed: false } host
            || _layoutCanvas is not { IsDisposed: false } canvas
            || host.ClientSize.Width <= 0 || host.ClientSize.Height <= 0)
            return;

        var start = LayoutStartNode();
        var startGrid = start is null ? Point.Empty : EffectiveLayoutGridPoint(start);
        canvas.CenterOnGrid(startGrid.X, startGrid.Y);
        _layoutViewportInitialized = true;
        _layoutViewportStartSignature = LayoutStartSignature(start);
        if (showStatus)
            _layoutStatusLabel.Text = start is null
                ? "Centered the empty Designer workspace."
                : $"Centered the Designer on system start {start.Id}.";
    }

    private void ChangeLayoutCoordinateOrigin()
    {
        var selected = _layoutCanvas?.SelectedNode;
        if (selected is null || selected.Type == LayoutNodeType.CalculationPoint)
        {
            MessageBox.Show(this, "Select a component on the graph, then click Origin.",
                "Select Coordinate Origin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var coordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        coordinates.TryGetValue(selected.Sequence, out var current);
        using var dialog = new LayoutCoordinateOriginDialog(selected.Id, current, _currentUnits);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var historyState = CaptureLayoutHistoryState();
        _ductSystem.CoordinateOriginNodeId = selected.Id;
        _ductSystem.CoordinateOriginXM = dialog.Coordinate.XM;
        _ductSystem.CoordinateOriginYM = dialog.Coordinate.YM;
        _ductSystem.CoordinateOriginZM = dialog.Coordinate.ZM;
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        _layoutCanvas?.SelectNode(selected);
        _layoutStatusLabel.Text = $"{selected.Id} is now the coordinate origin/reference point.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private void ClearLayoutHistory()
    {
        _layoutUndoHistory.Clear();
        _layoutRedoHistory.Clear();
        UpdateLayoutHistoryButtons();
    }

    private void PlaceLayoutNode(LayoutNodeType type, int gridX, int gridY, string? fittingGroup = null)
    {
        if (type == LayoutNodeType.Duct)
            return;
        if (type == LayoutNodeType.Fan && _ductSystem.LayoutNodes.Any(x => x.Type == LayoutNodeType.Fan))
        {
            const string message = "Only one fan is allowed in the duct system. Remove the existing fan before adding another.";
            _layoutStatusLabel.Text = message;
            _layoutStatusLabel.ForeColor = Color.Firebrick;
            if (!AutomatedLayoutVerification)
                MessageBox.Show(this, message, "Only One Fan Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var historyState = CaptureLayoutHistoryState();
        var displayGrid = new Point(gridX, gridY);
        var canonicalGrid = CanonicalGridPointFromDisplay(displayGrid);
        gridX = canonicalGrid.X;
        gridY = canonicalGrid.Y;
        var spaceRole = type == LayoutNodeType.Reservoir
            ? string.Equals(fittingGroup, "Space Source", StringComparison.OrdinalIgnoreCase) ? "Space Source" : "Space Sink"
            : string.Empty;
        var sourceBoundary = type == LayoutNodeType.Reservoir && IsSpaceSourceRole(spaceRole);
        // Calculation points remain observational. Other insertable components can explicitly split
        // a duct when they are dropped directly on its line; proximity alone never changes topology.
        var insertionConnection = FindNearestLayoutConnection(displayGrid.X, displayGrid.Y, null);
        if (type == LayoutNodeType.CalculationPoint && insertionConnection is null)
        {
            MessageBox.Show(this,
                "Place a calculation point directly on an existing duct line. It will observe that duct without splitting it.",
                "Duct Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var insertIntoDuct = false;
        if (type != LayoutNodeType.CalculationPoint && insertionConnection is LayoutConnectionRef candidate
            && CanInsertLayoutNodeIntoDuct(type, spaceRole))
        {
            switch (ConfirmLayoutDuctInsertion(type, fittingGroup, candidate))
            {
                case LayoutDuctInsertionChoice.Cancel:
                    return;
                case LayoutDuctInsertionChoice.Insert:
                    insertIntoDuct = true;
                    break;
            }
        }
        if (!insertIntoDuct && type != LayoutNodeType.CalculationPoint)
            insertionConnection = null;
        var parent = insertionConnection is LayoutConnectionRef insertion
            ? _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == (insertion.Secondary
                ? insertion.Downstream.SecondaryParentNodeId : insertion.Downstream.ParentNodeId))
            : null;
        var branch = parent?.BranchId ?? _ductSystem.DefaultBranchId;
        var firstComponent = !_ductSystem.LayoutNodes.Any(x => x.Type != LayoutNodeType.CalculationPoint);
        var node = new SavedLayoutNode
        {
            Sequence = _ductSystem.ReserveLayoutSequence(),
            Type = type,
            GridX = gridX,
            GridY = gridY,
            ParentNodeId = string.Empty,
            FittingGroup = type == LayoutNodeType.Fitting
                ? fittingGroup ?? FittingCatalog.FanExitsAndPlenumsSelectionGroup
                : type == LayoutNodeType.Grd ? "GRDs" : string.Empty,
            GrdType = type == LayoutNodeType.Grd ? "Grille" : string.Empty,
            Description = type == LayoutNodeType.CustomComponent ? "Custom Component" : string.Empty,
            SpaceRole = spaceRole,
            CalculationPointType = type == LayoutNodeType.CalculationPoint ? fittingGroup ?? "Pressure" : string.Empty,
            AirSystemType = parent?.AirSystemType ?? _ductSystem.DefaultAirSystemType,
            BranchId = branch,
            AirflowM3PerS = insertIntoDuct
                ? DuctForConnection(insertionConnection!.Value.Downstream, insertionConnection.Value.Secondary)?.AirflowM3PerS
                  ?? parent?.AirflowM3PerS ?? 0.0
                : 0.0,
            TemperatureC = insertIntoDuct
                ? DuctForConnection(insertionConnection!.Value.Downstream, insertionConnection.Value.Secondary)?.TemperatureC
                  ?? parent?.TemperatureC ?? UnitConversions.FahrenheitToCelsius(70)
                : UnitConversions.FahrenheitToCelsius(70),
            IsDefined = type == LayoutNodeType.CalculationPoint
        };
        if (sourceBoundary)
            node.BranchId = "Main";
        if (!firstComponent && type != LayoutNodeType.CalculationPoint)
            SetAbsoluteAnchorFromCanonicalGrid(node, canonicalGrid);
        _ductSystem.LayoutNodes.Add(node);
        if (firstComponent && type != LayoutNodeType.CalculationPoint)
        {
            _ductSystem.CoordinateOriginNodeId = node.Id;
            _ductSystem.CoordinateOriginXM = 0;
            _ductSystem.CoordinateOriginYM = 0;
            _ductSystem.CoordinateOriginZM = 0;
        }
        if (type == LayoutNodeType.CalculationPoint && insertionConnection is LayoutConnectionRef connection)
            AttachCalculationPointToConnection(node, connection);
        else if (insertIntoDuct && insertionConnection is LayoutConnectionRef ductConnection && parent is not null)
        {
            node.ParentNodeId = parent.Id;
            node.HasAbsoluteCoordinateAnchor = false;
            var insertionAxis = SetInsertionNodeDeltasForConnection(node, parent, ductConnection.Downstream,
                ductConnection.Secondary);
            var snappedGrid = GridPointAfterAxisDelta(CanonicalLayoutGridPoint(parent),
                node.DeltaXM, node.DeltaYM, node.DeltaZM);
            node.GridX = snappedGrid.X;
            node.GridY = snappedGrid.Y;
            node.ConnectionDeltaConfirmed = true;
            InsertLayoutNodeIntoConnection(node, ductConnection);
            _layoutStatusLabel.Text = $"Inserted {node.Id} into the existing duct. "
                                      + $"It was split along the {insertionAxis} axis; both segments inherited its inputs and updated lengths. Review or re-size both segments as needed.";
            _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
        }
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();

        if (!insertIntoDuct)
        {
            _layoutStatusLabel.Text = type == LayoutNodeType.CalculationPoint
                ? $"Placed {node.Id} on {LayoutConnectionCaption(insertionConnection!.Value)} without splitting the duct."
                : $"Placed {node.Id} at the selected location without connecting it. Drag between connector circles to create a duct.";
            _layoutStatusLabel.ForeColor = type == LayoutNodeType.CalculationPoint
                ? Color.FromArgb(31, 120, 180)
                : Color.Firebrick;
        }
    }

    private static bool CanInsertLayoutNodeIntoDuct(LayoutNodeType type, string spaceRole) =>
        type != LayoutNodeType.Duct
        && type != LayoutNodeType.CalculationPoint
        && !(type == LayoutNodeType.Reservoir
             && (IsSpaceSourceRole(spaceRole) || IsSpaceSinkRole(spaceRole)));

    private LayoutDuctInsertionChoice ConfirmLayoutDuctInsertion(LayoutNodeType type, string? fittingGroup,
        LayoutConnectionRef connection)
    {
        var automatedChoice = AutomationOptions.DuctInsertionChoice;
        if (AutomatedLayoutVerification)
            return string.Equals(automatedChoice, "1", StringComparison.Ordinal)
                ? LayoutDuctInsertionChoice.Insert
                : LayoutDuctInsertionChoice.PlaceUnconnected;

        var componentName = type == LayoutNodeType.Fitting && !string.IsNullOrWhiteSpace(fittingGroup)
            ? fittingGroup.TrimEnd('s')
            : type == LayoutNodeType.Grd ? "GRD" : type.ToString();
        var ductName = LayoutConnectionCaption(connection).Replace("\r", string.Empty)
            .Split('\n').FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ductName))
            ductName = "this unsized duct";
        var result = MessageBox.Show(this,
            $"Insert this {componentName} into {ductName}?\r\n\r\n"
            + "Yes — split the duct at this grid point and inherit its engineering inputs.\r\n"
            + "No — place the component here without connecting it.\r\n"
            + "Cancel — do not place the component.",
            "Insert Component into Duct", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
        return result switch
        {
            DialogResult.Yes => LayoutDuctInsertionChoice.Insert,
            DialogResult.No => LayoutDuctInsertionChoice.PlaceUnconnected,
            _ => LayoutDuctInsertionChoice.Cancel
        };
    }

    private bool IsSplittingFitting(SavedLayoutNode node)
    {
        if (node.Type != LayoutNodeType.Fitting)
            return false;
        if (string.Equals(node.FittingGroup, "Diverting Fittings", StringComparison.OrdinalIgnoreCase))
            return true;
        if (node.LinkedFittingSequence is not int sequence)
            return false;
        var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == sequence);
        if (fitting is null)
            return false;
        try
        {
            var definition = FittingCatalog.Find(fitting.FittingCode);
            return definition.IsJunction
                   && (definition.Description.Contains("diverging", StringComparison.OrdinalIgnoreCase)
                       || definition.Description.Contains("split", StringComparison.OrdinalIgnoreCase)
                       || definition.Description.Contains("extractor", StringComparison.OrdinalIgnoreCase)
                       || definition.Description.Contains("dovetail", StringComparison.OrdinalIgnoreCase));
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private LayoutConnectionRef? FindNearestLayoutConnection(int gridX, int gridY, SavedLayoutNode? excluded)
    {
        LayoutConnectionRef? best = null;
        var bestDistance = double.MaxValue;
        foreach (var downstream in _ductSystem.LayoutNodes)
        {
            foreach (var connection in new[]
                     {
                         new LayoutConnectionRef(downstream, false),
                         new LayoutConnectionRef(downstream, true)
                     })
            {
                var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
                if (string.IsNullOrWhiteSpace(parentId))
                    continue;
                var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
                if (parent is null || excluded is not null
                    && (parent.Id == excluded.Id || downstream.Id == excluded.Id
                        || IsLayoutDescendant(parent, excluded) || IsLayoutDescendant(downstream, excluded)))
                    continue;
                var parentGrid = EffectiveLayoutGridPoint(parent);
                var downstreamGrid = EffectiveLayoutGridPoint(downstream);
                var distance = DistanceToGridSegment(gridX / 2.0, gridY / 4.0,
                    parentGrid.X / 2.0, parentGrid.Y / 4.0,
                    downstreamGrid.X / 2.0, downstreamGrid.Y / 4.0);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = connection;
                }
            }
        }
        // A component interrupts a duct only when it is dropped essentially on the line.
        return bestDistance <= 0.35 ? best : null;
    }

    private static bool IsSpaceSourceRole(string? role) => SpaceRoles.IsSource(role);

    private static bool IsSpaceSinkRole(string? role) => SpaceRoles.IsSink(role);

    private static int ConnectedLayoutInputCount(SavedLayoutNode node) =>
        (string.IsNullOrWhiteSpace(node.ParentNodeId) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) ? 0 : 1);

    private static int LayoutInletCapacity(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.CalculationPoint => 0,
        LayoutNodeType.Fitting => FittingCatalog.InletPortCount(node.FittingGroup),
        LayoutNodeType.Reservoir when IsSpaceSourceRole(node.SpaceRole) => 0,
        _ => 1
    };

    private static int LayoutOutletCapacity(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.CalculationPoint => 0,
        LayoutNodeType.Fitting => FittingCatalog.OutletPortCount(node.FittingGroup),
        LayoutNodeType.Reservoir when IsSpaceSinkRole(node.SpaceRole) => 0,
        _ => 1
    };

    private static double DistanceToGridSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
            return Math.Sqrt(Math.Pow(px - ax, 2) + Math.Pow(py - ay, 2));
        var t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(px - (ax + t * dx), 2) + Math.Pow(py - (ay + t * dy), 2));
    }

    private static double LayoutGridDistance(int firstX, int firstY, int secondX, int secondY) =>
        Math.Sqrt(Math.Pow((firstX - secondX) / 2.0, 2) + Math.Pow((firstY - secondY) / 4.0, 2));

    private double LayoutFeetPerGridCell =>
        double.IsFinite(_ductSystem.LayoutFeetPerGridCell) && _ductSystem.LayoutFeetPerGridCell > 0
            ? Math.Clamp(_ductSystem.LayoutFeetPerGridCell, 0.1, 1000.0)
            : 1.0;

    private Point CanonicalLayoutGridPoint(SavedLayoutNode node)
    {
        var coordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        if (!UsesResolvedLayoutCoordinate(node)
            || !coordinates.TryGetValue(node.Sequence, out var coordinate))
            return new Point(node.GridX, node.GridY);
        var cellFeet = LayoutFeetPerGridCell;
        var x = UnitConversions.MToFeet(coordinate.XM) / cellFeet;
        var y = UnitConversions.MToFeet(coordinate.YM) / cellFeet;
        var z = UnitConversions.MToFeet(coordinate.ZM) / cellFeet;
        return new Point((int)Math.Round(x - y), (int)Math.Round(x + y - 2.0 * z));
    }

    private Point EffectiveLayoutGridPoint(SavedLayoutNode node) =>
        DisplayGridPointFromCanonical(CanonicalLayoutGridPoint(node));

    private Point DisplayGridPointFromCanonical(Point canonical) =>
        _ductSystem.LayoutViewMode == LayoutViewMode.IsometricNegative
            ? new Point(canonical.Y, -canonical.X)
            : canonical;

    private Point CanonicalGridPointFromDisplay(Point display) =>
        _ductSystem.LayoutViewMode == LayoutViewMode.IsometricNegative
            ? new Point(-display.Y, display.X)
            : display;

    private void SetAbsoluteAnchorFromCanonicalGrid(SavedLayoutNode node, Point canonical)
    {
        var cellFeet = LayoutFeetPerGridCell;
        var xFeet = (canonical.X + canonical.Y) / 2.0 * cellFeet;
        var yFeet = (canonical.Y - canonical.X) / 2.0 * cellFeet;
        node.HasAbsoluteCoordinateAnchor = true;
        node.AbsoluteAnchorXM = UnitConversions.FeetToM(xFeet);
        node.AbsoluteAnchorYM = UnitConversions.FeetToM(yFeet);
        node.AbsoluteAnchorZM = 0.0;
    }

    private bool UsesResolvedLayoutCoordinate(SavedLayoutNode node) =>
        node.HasAbsoluteCoordinateAnchor || node.ConnectionDeltaConfirmed || node.SecondaryConnectionDeltaConfirmed
        || _ductSystem.LayoutNodes.Any(child =>
            (child.ConnectionDeltaConfirmed
             && string.Equals(child.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
            || (child.SecondaryConnectionDeltaConfirmed
                && string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)));

    private void SetDefaultNodeDeltas(SavedLayoutNode node, SavedLayoutNode? parent)
    {
        if (parent is null)
            return;
        var parentGrid = EffectiveLayoutGridPoint(parent);
        var nodeGrid = EffectiveLayoutGridPoint(node);
        SetNodeDeltasFromDisplayGrid(node, parentGrid, nodeGrid);
    }

    private void SetNodeDeltasFromDisplayGrid(SavedLayoutNode node, Point parentGrid, Point nodeGrid)
    {
        var delta = ConnectionDeltaFromDisplayGrid(parentGrid, nodeGrid);
        node.DeltaXM = delta.XM;
        node.DeltaYM = delta.YM;
        node.DeltaZM = delta.ZM;
    }

    private (double XM, double YM, double ZM) ConnectionDeltaFromDisplayGrid(Point parentGrid, Point nodeGrid)
    {
        var deltaColumn = nodeGrid.X - parentGrid.X;
        var deltaRow = nodeGrid.Y - parentGrid.Y;
        var feetPerCell = LayoutFeetPerGridCell;
        if (_ductSystem.LayoutViewMode == LayoutViewMode.IsometricNegative)
        {
            return (UnitConversions.FeetToM((deltaColumn - deltaRow) / 2.0 * feetPerCell),
                UnitConversions.FeetToM((deltaColumn + deltaRow) / 2.0 * feetPerCell), 0.0);
        }
        return (UnitConversions.FeetToM((deltaColumn + deltaRow) / 2.0 * feetPerCell),
            UnitConversions.FeetToM((deltaRow - deltaColumn) / 2.0 * feetPerCell), 0.0);
    }

    private (double XM, double YM, double ZM) ConnectionDeltaPreservingAbsolutePosition(
        SavedLayoutNode upstream, SavedLayoutNode downstream)
    {
        if (_ductSystem.LayoutViewMode != LayoutViewMode.TopDown)
        {
            var parentGrid = CanonicalLayoutGridPoint(upstream);
            var nodeGrid = CanonicalLayoutGridPoint(downstream);
            var deltaColumn = nodeGrid.X - parentGrid.X;
            var deltaRow = nodeGrid.Y - parentGrid.Y;
            var cellM = UnitConversions.FeetToM(LayoutFeetPerGridCell);
            if (deltaColumn == deltaRow)
                return (deltaColumn * cellM, 0.0, 0.0);
            if (deltaColumn == -deltaRow)
                return (0.0, deltaRow * cellM, 0.0);
            if (deltaColumn == 0 && deltaRow % 2 == 0)
                return (0.0, 0.0, -deltaRow / 2.0 * cellM);
        }

        var coordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        if (coordinates.TryGetValue(upstream.Sequence, out var upstreamCoordinate)
            && coordinates.TryGetValue(downstream.Sequence, out var downstreamCoordinate))
        {
            return (downstreamCoordinate.XM - upstreamCoordinate.XM,
                downstreamCoordinate.YM - upstreamCoordinate.YM,
                downstreamCoordinate.ZM - upstreamCoordinate.ZM);
        }
        return ConnectionDeltaFromDisplayGrid(EffectiveLayoutGridPoint(upstream),
            EffectiveLayoutGridPoint(downstream));
    }

    private static (double XM, double YM, double ZM) ConnectionDelta(SavedLayoutNode downstream,
        bool secondary) => secondary
        ? (downstream.SecondaryDeltaXM, downstream.SecondaryDeltaYM, downstream.SecondaryDeltaZM)
        : (downstream.DeltaXM, downstream.DeltaYM, downstream.DeltaZM);

    private static void SetConnectionDelta(SavedLayoutNode downstream, bool secondary,
        double deltaXM, double deltaYM, double deltaZM)
    {
        if (secondary)
        {
            downstream.SecondaryDeltaXM = deltaXM;
            downstream.SecondaryDeltaYM = deltaYM;
            downstream.SecondaryDeltaZM = deltaZM;
        }
        else
        {
            downstream.DeltaXM = deltaXM;
            downstream.DeltaYM = deltaYM;
            downstream.DeltaZM = deltaZM;
        }
    }

    private string SetInsertionNodeDeltas(SavedLayoutNode node, SavedLayoutNode parent,
        SavedLayoutNode downstream) => SetInsertionNodeDeltasForConnection(node, parent, downstream,
        secondary: false);

    private string SetInsertionNodeDeltasForConnection(SavedLayoutNode node, SavedLayoutNode parent,
        SavedLayoutNode downstream, bool secondary)
    {
        var parentGrid = EffectiveLayoutGridPoint(parent);
        var downstreamGrid = EffectiveLayoutGridPoint(downstream);
        var nodeGrid = EffectiveLayoutGridPoint(node);
        var fraction = ProjectionFractionOnGridSegment(nodeGrid.X, nodeGrid.Y,
            parentGrid.X, parentGrid.Y, downstreamGrid.X, downstreamGrid.Y);
        var fullDelta = ConnectionDelta(downstream, secondary);
        var connectionDelta = new[]
        {
            (Axis: "X", Value: fullDelta.XM),
            (Axis: "Y", Value: fullDelta.YM),
            (Axis: "Z", Value: fullDelta.ZM)
        };
        var axis = connectionDelta.OrderByDescending(item => Math.Abs(item.Value)).First().Axis;
        node.DeltaXM = axis == "X" ? fullDelta.XM * fraction : 0.0;
        node.DeltaYM = axis == "Y" ? fullDelta.YM * fraction : 0.0;
        node.DeltaZM = axis == "Z" ? fullDelta.ZM * fraction : 0.0;
        return axis;
    }

    private bool ConfirmConnectionCoordinates(SavedLayoutNode downstream, SavedLayoutNode upstream, bool secondary,
        string? editableAxis = null)
    {
        if (AutomatedLayoutVerification)
        {
            if (secondary)
                downstream.SecondaryConnectionDeltaConfirmed = true;
            else
                downstream.ConnectionDeltaConfirmed = true;
            return true;
        }
        var displayedGridIncrement = _currentUnits == UnitSystem.Imperial
            ? LayoutFeetPerGridCell
            : UnitConversions.FeetToM(LayoutFeetPerGridCell);
        var target = downstream;
        if (secondary)
        {
            var delta = ConnectionDelta(downstream, true);
            target = new SavedLayoutNode
            {
                Sequence = downstream.Sequence,
                Type = downstream.Type,
                DeltaXM = delta.XM,
                DeltaYM = delta.YM,
                DeltaZM = delta.ZM
            };
        }
        using var dialog = new LayoutComponentPlacementDialog(target, upstream.Id, _currentUnits, editableAxis,
            displayedGridIncrement);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return false;
        if (secondary)
        {
            SetConnectionDelta(downstream, true, target.DeltaXM, target.DeltaYM, target.DeltaZM);
            downstream.SecondaryConnectionDeltaConfirmed = true;
        }
        else
            downstream.ConnectionDeltaConfirmed = true;
        return true;
    }

    private IReadOnlyList<SavedLayoutNode> AddAutomaticElbowsForMultiAxisConnection(
        SavedLayoutNode downstream, bool secondary)
    {
        var parentId = secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
            string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
        if (parent is null || !(secondary
                ? downstream.SecondaryConnectionDeltaConfirmed
                : downstream.ConnectionDeltaConfirmed))
            return Array.Empty<SavedLayoutNode>();

        var connectionDelta = ConnectionDelta(downstream, secondary);
        var segments = AxisSegments(connectionDelta.XM, connectionDelta.YM, connectionDelta.ZM);
        if (segments.Count <= 1)
            return Array.Empty<SavedLayoutNode>();

        InvalidateConnectionDuct(downstream, secondary);
        var inserted = new List<SavedLayoutNode>(segments.Count - 1);
        var previous = parent;
        var previousGrid = CanonicalLayoutGridPoint(parent);
        for (var index = 0; index < segments.Count - 1; index++)
        {
            var segment = segments[index];
            previousGrid = GridPointAfterAxisDelta(previousGrid, segment.XM, segment.YM, segment.ZM);
            var elbow = new SavedLayoutNode
            {
                Sequence = _ductSystem.ReserveLayoutSequence(),
                Type = LayoutNodeType.Fitting,
                FittingGroup = "Elbows",
                GridX = previousGrid.X,
                GridY = previousGrid.Y,
                ParentNodeId = previous.Id,
                AirSystemType = downstream.AirSystemType,
                BranchId = downstream.BranchId,
                AirflowM3PerS = downstream.AirflowM3PerS > 0
                    ? downstream.AirflowM3PerS
                    : previous.AirflowM3PerS,
                TemperatureC = downstream.TemperatureC,
                DeltaXM = segment.XM,
                DeltaYM = segment.YM,
                DeltaZM = segment.ZM,
                ElbowAngleDegrees = AutomaticElbowAngleDegrees(segments, index),
                ConnectionDeltaConfirmed = true,
                IsDefined = false
            };
            _ductSystem.LayoutNodes.Add(elbow);
            inserted.Add(elbow);
            EnsureInitialConnectionDuct(elbow, previous, secondary: false);
            SyncLinkedItemMetadata(elbow);
            previous = elbow;
        }

        var finalSegment = segments[^1];
        if (secondary)
            downstream.SecondaryParentNodeId = previous.Id;
        else
            downstream.ParentNodeId = previous.Id;
        SetConnectionDelta(downstream, secondary, finalSegment.XM, finalSegment.YM, finalSegment.ZM);
        if (secondary)
            downstream.SecondaryConnectionDeltaConfirmed = true;
        else
            downstream.ConnectionDeltaConfirmed = true;
        EnsureInitialConnectionDuct(downstream, previous, secondary);
        SyncLinkedItemMetadata(downstream);
        return inserted;
    }

    private static double AutomaticElbowAngleDegrees(
        IReadOnlyList<(double XM, double YM, double ZM)> segments, int elbowIndex)
    {
        var incoming = segments[elbowIndex];
        var outgoing = segments[elbowIndex + 1];
        var incomingLength = Math.Sqrt(incoming.XM * incoming.XM + incoming.YM * incoming.YM
                                      + incoming.ZM * incoming.ZM);
        var outgoingLength = Math.Sqrt(outgoing.XM * outgoing.XM + outgoing.YM * outgoing.YM
                                      + outgoing.ZM * outgoing.ZM);
        if (incomingLength <= 1e-8 || outgoingLength <= 1e-8)
            return 90.0;
        var offsetAngle = Math.Atan2(Math.Min(incomingLength, outgoingLength),
            Math.Max(incomingLength, outgoingLength)) * 180.0 / Math.PI;
        return Math.Abs(offsetAngle - 45.0) <= 1.0 ? 45.0 : 90.0;
    }

    private static IReadOnlyList<(double XM, double YM, double ZM)> AxisSegments(
        double deltaXM, double deltaYM, double deltaZM)
    {
        const double tolerance = 1e-8;
        var segments = new List<(double XM, double YM, double ZM)>(3);
        if (Math.Abs(deltaXM) > tolerance)
            segments.Add((deltaXM, 0.0, 0.0));
        if (Math.Abs(deltaYM) > tolerance)
            segments.Add((0.0, deltaYM, 0.0));
        if (Math.Abs(deltaZM) > tolerance)
            segments.Add((0.0, 0.0, deltaZM));
        return segments;
    }

    private Point GridPointAfterAxisDelta(Point start, double deltaXM, double deltaYM, double deltaZM)
    {
        var cellFeet = LayoutFeetPerGridCell;
        var x = UnitConversions.MToFeet(deltaXM) / cellFeet;
        var y = UnitConversions.MToFeet(deltaYM) / cellFeet;
        var z = UnitConversions.MToFeet(deltaZM) / cellFeet;
        return new Point(
            start.X + (int)Math.Round(x - y),
            start.Y + (int)Math.Round(x + y - 2.0 * z));
    }

    private bool EnsureConnectedCoordinateDeltas(SavedLayoutNode node)
    {
        foreach (var (parentId, secondary) in new[]
                 {
                     (node.ParentNodeId, false), (node.SecondaryParentNodeId, true)
                 })
        {
            if (string.IsNullOrWhiteSpace(parentId))
                continue;
            var confirmed = secondary ? node.SecondaryConnectionDeltaConfirmed : node.ConnectionDeltaConfirmed;
            var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
            if (!confirmed && parent is not null && !ConfirmConnectionCoordinates(node, parent, secondary))
                return false;
        }

        foreach (var child in _ductSystem.LayoutNodes.Where(x =>
                     string.Equals(x.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(x.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var secondary = string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
            var confirmed = secondary ? child.SecondaryConnectionDeltaConfirmed : child.ConnectionDeltaConfirmed;
            if (!confirmed && !ConfirmConnectionCoordinates(child, node, secondary))
                return false;
        }
        return true;
    }

    private void InsertLayoutNodeIntoConnection(SavedLayoutNode node, LayoutConnectionRef connection)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
        if (parent is null)
            return;
        var inheritedSequence = connection.Secondary
            ? downstream.SecondaryConnectionDuctSequence
            : downstream.ConnectionDuctSequence;
        var inheritedDuct = inheritedSequence is int sequence
            ? _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == sequence)
            : null;
        var inheritedSnapshot = inheritedDuct is null ? null : CloneDuct(inheritedDuct);
        var observedPoints = inheritedSequence is int observedSequence
            ? _ductSystem.LayoutNodes.Where(point => point.Type == LayoutNodeType.CalculationPoint
                                                     && point.ObservedDuctSequence == observedSequence)
                .Select(point => (Point: point, Fraction: point.CalculationConnectionFraction)).ToArray()
            : Array.Empty<(SavedLayoutNode Point, double Fraction)>();
        var nodeGrid = EffectiveLayoutGridPoint(node);
        var parentGrid = EffectiveLayoutGridPoint(parent);
        var downstreamGrid = EffectiveLayoutGridPoint(downstream);
        var splitFraction = Math.Clamp(ProjectionFractionOnGridSegment(
            nodeGrid.X, nodeGrid.Y, parentGrid.X, parentGrid.Y, downstreamGrid.X, downstreamGrid.Y), 0.01, 0.99);
        var oldDelta = ConnectionDelta(downstream, connection.Secondary);
        InvalidateConnectionDuct(downstream, connection.Secondary);
        if (connection.Secondary)
            downstream.SecondaryParentNodeId = node.Id;
        else
            downstream.ParentNodeId = node.Id;
        SetConnectionDelta(downstream, connection.Secondary,
            oldDelta.XM - node.DeltaXM, oldDelta.YM - node.DeltaYM, oldDelta.ZM - node.DeltaZM);
        downstream.ConnectionDeltaConfirmed = connection.Secondary
            ? downstream.ConnectionDeltaConfirmed : true;
        downstream.SecondaryConnectionDeltaConfirmed = connection.Secondary
            ? true : downstream.SecondaryConnectionDeltaConfirmed;
        if (inheritedSnapshot is not null)
        {
            var upstreamSegment = CreateInheritedDuctSegment(inheritedSnapshot, inheritedSnapshot.Sequence,
                parent, node, node.DeltaXM, node.DeltaYM, node.DeltaZM);
            var remainingDelta = ConnectionDelta(downstream, connection.Secondary);
            var downstreamSegment = CreateInheritedDuctSegment(inheritedSnapshot, _ductSystem.ReserveSequence(),
                node, downstream, remainingDelta.XM, remainingDelta.YM, remainingDelta.ZM);
            _ductSystem.Ducts.Add(upstreamSegment);
            _ductSystem.Ducts.Add(downstreamSegment);
            node.ConnectionDuctSequence = upstreamSegment.Sequence;
            if (connection.Secondary)
                downstream.SecondaryConnectionDuctSequence = downstreamSegment.Sequence;
            else
                downstream.ConnectionDuctSequence = downstreamSegment.Sequence;
            RestoreCalculationPointsAfterDuctSplit(observedPoints, node, downstream, connection.Secondary,
                upstreamSegment.Sequence, downstreamSegment.Sequence, splitFraction);
        }
        else
        {
            EnsureInitialConnectionDuct(node, parent, secondary: false);
            EnsureInitialConnectionDuct(downstream, node, connection.Secondary);
        }
        SyncLinkedItemMetadata(node);
        SyncLinkedItemMetadata(downstream);
    }

    private static SavedDuct CreateInheritedDuctSegment(SavedDuct source, int sequence,
        SavedLayoutNode upstream, SavedLayoutNode downstream, double deltaXM, double deltaYM, double deltaZM)
    {
        var segment = CloneDuct(source);
        segment.Sequence = sequence;
        segment.Description = $"Designer duct {upstream.Id} to {downstream.Id}";
        segment.AirSystemType = downstream.AirSystemType;
        segment.BranchId = downstream.BranchId;
        segment.DeltaXM = deltaXM;
        segment.DeltaYM = deltaYM;
        segment.DeltaZM = deltaZM;
        segment.LengthM = Math.Sqrt(deltaXM * deltaXM + deltaYM * deltaYM + deltaZM * deltaZM);
        segment.RequiresResizing = true;
        return segment;
    }

    private static void RestoreCalculationPointsAfterDuctSplit(
        IReadOnlyList<(SavedLayoutNode Point, double Fraction)> observedPoints,
        SavedLayoutNode inserted, SavedLayoutNode originalDownstream, bool originalSecondary,
        int upstreamDuctSequence, int downstreamDuctSequence, double splitFraction)
    {
        foreach (var observation in observedPoints)
        {
            var onUpstreamSegment = observation.Fraction <= splitFraction;
            observation.Point.CalculationConnectionNodeId = onUpstreamSegment ? inserted.Id : originalDownstream.Id;
            observation.Point.CalculationConnectionSecondary = onUpstreamSegment ? false : originalSecondary;
            observation.Point.ObservedDuctSequence = onUpstreamSegment ? upstreamDuctSequence : downstreamDuctSequence;
            observation.Point.CalculationConnectionFraction = onUpstreamSegment
                ? Math.Clamp(observation.Fraction / splitFraction, 0.0, 1.0)
                : Math.Clamp((observation.Fraction - splitFraction) / (1.0 - splitFraction), 0.0, 1.0);
            observation.Point.IsDefined = true;
        }
    }

    private void AttachCalculationPointToConnection(SavedLayoutNode point, LayoutConnectionRef connection)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
        if (parent is null)
            return;

        point.ParentNodeId = string.Empty;
        point.SecondaryParentNodeId = string.Empty;
        point.ConnectionDuctSequence = null;
        point.SecondaryConnectionDuctSequence = null;
        point.CalculationConnectionNodeId = downstream.Id;
        point.CalculationConnectionSecondary = connection.Secondary;
        point.ObservedDuctSequence = connection.Secondary
            ? downstream.SecondaryConnectionDuctSequence
            : downstream.ConnectionDuctSequence;
        var parentGrid = EffectiveLayoutGridPoint(parent);
        var downstreamGrid = EffectiveLayoutGridPoint(downstream);
        point.CalculationConnectionFraction = ProjectionFractionOnGridSegment(
            point.GridX, point.GridY, parentGrid.X, parentGrid.Y, downstreamGrid.X, downstreamGrid.Y);
        point.AirSystemType = downstream.AirSystemType;
        point.BranchId = downstream.BranchId;
        point.IsDefined = true;
        UpdateCalculatedLayoutValues();
    }

    private static double ProjectionFractionOnGridSegment(int px, int py, int ax, int ay, int bx, int by)
    {
        var pointX = px / 2.0;
        var pointY = py / 4.0;
        var startX = ax / 2.0;
        var startY = ay / 4.0;
        var endX = bx / 2.0;
        var endY = by / 4.0;
        var dx = endX - startX;
        var dy = endY - startY;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-12)
            return 0.5;
        return Math.Clamp(((pointX - startX) * dx + (pointY - startY) * dy) / lengthSquared, 0.0, 1.0);
    }

    private void EnsureInitialConnectionDuct(SavedLayoutNode downstream, SavedLayoutNode upstream, bool secondary)
    {
        var existing = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        if (existing.HasValue || !upstream.IsDefined || !downstream.IsDefined || upstream.AirflowM3PerS <= 0)
            return;
        try
        {
            var delta = ConnectionDelta(downstream, secondary);
            var length = Math.Sqrt(delta.XM * delta.XM + delta.YM * delta.YM + delta.ZM * delta.ZM);
            if (length < UnitConversions.FeetToM(0.5))
            {
                var downstreamGrid = EffectiveLayoutGridPoint(downstream);
                var upstreamGrid = EffectiveLayoutGridPoint(upstream);
                var gridDistance = LayoutGridDistance(downstreamGrid.X, downstreamGrid.Y,
                    upstreamGrid.X, upstreamGrid.Y);
                length = UnitConversions.FeetToM(Math.Max(1.0, gridDistance * 5.0));
            }
            var material = DuctMaterialCatalog.All.First(x => x.Name == "Galvanized Spiral Duct");
            var increment = _ductSystem.DefaultSizingIncrementM > 0
                ? _ductSystem.DefaultSizingIncrementM
                : UnitConversions.InchesToM(0.5);
            var sizing = DuctSizingSolver.Size(new DuctSizingInput(
                upstream.AirflowM3PerS,
                upstream.TemperatureC,
                FixedRelativeHumidity,
                FixedAtmosphericPressurePa,
                DuctShape.Round,
                1.0,
                material,
                _ductSystem.DefaultEquivalentDiameterMethod,
                _ductSystem.DefaultPressureLossBasis,
                length,
                CalculationMode.SizeByTargetFriction,
                UnitConversions.InchesWaterPer100FeetToPaPerM(0.08),
                1.0,
                increment,
                _ductSystem.DefaultSizeRoundingDirection));
            var sequence = _ductSystem.ReserveSequence();
            var duct = SavedDuct.FromResult(sequence, $"Designer duct {upstream.Id} to {downstream.Id}",
                sizing.Calculation, downstream.AirSystemType, downstream.BranchId);
            duct.DeltaXM = delta.XM;
            duct.DeltaYM = delta.YM;
            duct.DeltaZM = delta.ZM;
            _ductSystem.Ducts.Add(duct);
            if (secondary)
                downstream.SecondaryConnectionDuctSequence = sequence;
            else
                downstream.ConnectionDuctSequence = sequence;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            _layoutStatusLabel.Text = $"The connection was created but could not be initially sized: {ex.Message}";
            _layoutStatusLabel.ForeColor = Color.Firebrick;
        }
    }

    private void MoveLayoutNode(SavedLayoutNode node, int gridX, int gridY)
    {
        var historyState = CaptureLayoutHistoryState();
        var displayGrid = new Point(gridX, gridY);
        var canonicalGrid = CanonicalGridPointFromDisplay(displayGrid);
        node.GridX = canonicalGrid.X;
        node.GridY = canonicalGrid.Y;
        if (node.Type == LayoutNodeType.CalculationPoint)
        {
            var connection = FindNearestLayoutConnection(displayGrid.X, displayGrid.Y, node);
            if (connection is LayoutConnectionRef observedConnection)
            {
                AttachCalculationPointToConnection(node, observedConnection);
                CommitLayoutHistoryState(historyState);
                SaveDuctSystem();
                RefreshLayoutDesigner();
                RefreshDuctSystemGrid();
                _layoutStatusLabel.Text = $"Moved {node.Id} onto {LayoutConnectionCaption(observedConnection)} without changing the duct topology.";
                _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
            }
            else
            {
                RestoreLayoutHistoryState(historyState, "Calculation points must remain on a duct line.");
                _layoutStatusLabel.Text = "Calculation points must remain on a duct line.";
                _layoutStatusLabel.ForeColor = Color.Firebrick;
            }
            return;
        }
        if (_layoutRearrangeMode)
        {
            CommitLayoutHistoryState(historyState);
            SaveDuctSystem();
            RefreshLayoutDesigner();
            _layoutStatusLabel.Text = $"Moved {node.Id} visually. Existing connections, duct data, and fitting data were preserved.";
            _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
            return;
        }
        var parentId = !string.IsNullOrWhiteSpace(node.ParentNodeId)
            ? node.ParentNodeId : node.SecondaryParentNodeId;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
            string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
        if (parent is null)
        {
            SetAbsoluteAnchorFromCanonicalGrid(node, canonicalGrid);
        }
        else
        {
            var secondaryConnection = string.Equals(node.SecondaryParentNodeId, parent.Id,
                                          StringComparison.OrdinalIgnoreCase)
                                      && string.IsNullOrWhiteSpace(node.ParentNodeId);
            var parentGrid = EffectiveLayoutGridPoint(parent);
            var movedDelta = ConnectionDeltaFromDisplayGrid(parentGrid, displayGrid);
            SetConnectionDelta(node, secondaryConnection, movedDelta.XM, movedDelta.YM, movedDelta.ZM);
            if (!secondaryConnection && !string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
            {
                var secondaryParent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                    string.Equals(x.Id, node.SecondaryParentNodeId, StringComparison.OrdinalIgnoreCase));
                if (secondaryParent is not null)
                {
                    var secondaryDelta = ConnectionDeltaFromDisplayGrid(
                        EffectiveLayoutGridPoint(secondaryParent), displayGrid);
                    SetConnectionDelta(node, true, secondaryDelta.XM, secondaryDelta.YM, secondaryDelta.ZM);
                    node.SecondaryConnectionDeltaConfirmed = true;
                    InvalidateConnectionDuct(node, secondary: true);
                }
            }
            node.HasAbsoluteCoordinateAnchor = false;
            if (secondaryConnection)
                node.SecondaryConnectionDeltaConfirmed = true;
            else
                node.ConnectionDeltaConfirmed = true;
            InvalidateConnectionDuct(node, secondaryConnection);
            var elbows = AddAutomaticElbowsForMultiAxisConnection(node, secondaryConnection);
            if (elbows.Count == 0)
                EnsureInitialConnectionDuct(node, parent, secondaryConnection);
        }
        SyncLinkedItemMetadata(node);
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        _layoutStatusLabel.Text = $"Moved {node.Id} without creating or changing any nearby connection.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
    }

    private void ActivateLayoutNode(SavedLayoutNode node)
    {
        if (node.Type == LayoutNodeType.CalculationPoint)
        {
            UpdateCalculatedLayoutValues();
            using var calculationDialog = new LayoutEquipmentDialog(node, _ductSystem.LayoutNodes, LayoutNodeCaption);
            calculationDialog.ShowDialog(this);
            RefreshLayoutDesigner();
            RefreshDuctSystemGrid();
            return;
        }
        if (node.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
            or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent)
        {
            var historyState = CaptureLayoutHistoryState();
            if (node.Type == LayoutNodeType.Reservoir && LayoutSpaceSources().Length > 1)
            {
                if (ShowSourceSinkEditor())
                {
                    CommitLayoutHistoryState(historyState);
                    ResetCriticalPath();
                    SaveDuctSystem();
                    RefreshLayoutDesigner();
                    RefreshDuctSystemGrid();
                }
                return;
            }
            var boundaryAirflows = node.Type == LayoutNodeType.Reservoir
                ? LayoutBoundaryNodes().ToDictionary(boundary => boundary.Sequence, boundary => boundary.AirflowM3PerS)
                : new Dictionary<int, double>();
            var previousAirflow = node.AirflowM3PerS;
            UpdateCalculatedLayoutValues();
            var resolvedCoordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
            using var dialog = resolvedCoordinates.TryGetValue(node.Sequence, out var absoluteCoordinate)
                ? new LayoutEquipmentDialog(node, _ductSystem.LayoutNodes, LayoutNodeCaption, absoluteCoordinate,
                    EditableAxesForNode(node), PositionReferenceFor(node, resolvedCoordinates))
                : new LayoutEquipmentDialog(node, _ductSystem.LayoutNodes, LayoutNodeCaption);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var selectedParent = node.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(node.SpaceRole)
                    ? string.Empty
                    : dialog.SelectedParentNodeId;
                if (!RewireLayoutNode(node, selectedParent, reposition: true))
                    return;
                if (resolvedCoordinates.ContainsKey(node.Sequence))
                    ApplyAbsoluteCoordinateEdit(node, absoluteCoordinate, dialog.DesiredAbsoluteCoordinate);
                node.AirSystemType = dialog.SelectedAirSystemType;
                node.IsDefined = true;
                if (!EnsureConnectedCoordinateDeltas(node))
                {
                    node.IsDefined = false;
                    return;
                }
                if (node.Type == LayoutNodeType.Reservoir
                    && Math.Abs(node.AirflowM3PerS - previousAirflow) > 1e-9)
                    ReconcileBoundaryAirflowAfterDirectEdit(node, boundaryAirflows);
                UpdateCalculatedLayoutValues();
                SyncConnectionDuctGeometry(node);
                SyncLinkedItemMetadata(node);
                if (node.Type == LayoutNodeType.Fan)
                    _ductSystem.DefaultAirSystemType = node.AirSystemType;
                CommitLayoutHistoryState(historyState);
                SaveDuctSystem();
                RefreshLayoutDesigner();
                RefreshDuctSystemGrid();
            }
            return;
        }

        if (node.Type == LayoutNodeType.Duct && node.LinkedDuctSequence is int ductSequence)
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductSequence);
            if (duct is not null)
            {
                var historyState = CaptureLayoutHistoryState();
                using var editor = new LayoutDuctEditDialog(CloneDuct(duct), _currentUnits,
                    _ductSystem.DefaultSizingIncrementM, _ductSystem.DefaultSizeRoundingDirection);
                if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is not SavedDuct edited)
                    return;
                if (!ConfirmAirflowOverride(node, edited.AirflowM3PerS, duct.Id))
                    return;
                CopyDuctValues(edited, duct);
                duct.RequiresResizing = false;
                node.AirflowM3PerS = duct.AirflowM3PerS;
                node.TemperatureC = duct.TemperatureC;
                SyncLinkedItemMetadata(node);
                CommitLayoutHistoryState(historyState);
                SaveDuctSystem();
                RefreshDuctSystemGrid();
                RefreshLayoutDesigner();
                _layoutStatusLabel.Text = $"Updated {duct.Id} without leaving Layout Designer.";
                _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
                return;
            }
        }

        if (node.Type == LayoutNodeType.Fitting)
        {
            var historyState = CaptureLayoutHistoryState();
            var linkedFitting = node.LinkedFittingSequence is int sequence
                ? _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == sequence)
                : null;
            var resolvedCoordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
            LayoutCoordinate? absoluteCoordinate = resolvedCoordinates.TryGetValue(node.Sequence, out var resolved)
                ? resolved : null;
            using var dialog = new LayoutFittingNodeDialog(node, _ductSystem.LayoutNodes, LayoutNodeCaption,
                linkedFitting, absoluteCoordinate, EditableAxesForNode(node),
                PositionReferenceFor(node, resolvedCoordinates));
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            var draft = linkedFitting is null
                ? CreateLayoutFittingDraft(node)
                : CloneFitting(linkedFitting);
            using var editor = new LayoutFittingEditDialog(draft, node.FittingGroup, _currentUnits);
            if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is not SavedFitting edited)
                return;
            if (!ConfirmAirflowOverride(node, edited.AirflowM3PerS,
                    linkedFitting?.Id ?? node.Id))
                return;
            if (!ConfirmAndApplyJunctionFlows(node, edited, linkedFitting?.Id ?? node.Id))
                return;
            if (!RewireLayoutNode(node, dialog.SelectedParentNodeId, reposition: true))
                return;
            node.AirSystemType = dialog.SelectedAirSystemType;
            if (absoluteCoordinate is LayoutCoordinate originalCoordinate)
                ApplyAbsoluteCoordinateEdit(node, originalCoordinate, dialog.DesiredAbsoluteCoordinate);
            else
            {
                node.DeltaXM = dialog.DeltaXM;
                node.DeltaYM = dialog.DeltaYM;
                node.DeltaZM = dialog.DeltaZM;
            }
            var previousNodeId = node.Id;
            node.FittingGroup = FittingCatalog.SelectionGroup(FittingCatalog.Find(edited.FittingCode));
            if (string.Equals(node.FittingGroup, "Elbows", StringComparison.OrdinalIgnoreCase)
                && edited.Inputs.TryGetValue("angle", out var editedElbowAngle))
                node.ElbowAngleDegrees = editedElbowAngle;
            UpdateLayoutNodeReferences(previousNodeId, node.Id);
            node.AirflowM3PerS = edited.AirflowM3PerS;
            node.TemperatureC = edited.TemperatureC;
            node.AirflowUserSpecified = node.AirflowOverride;
            node.IsDefined = true;
            if (!EnsureConnectedCoordinateDeltas(node))
            {
                node.IsDefined = false;
                return;
            }
            if (linkedFitting is null)
            {
                edited.Sequence = _ductSystem.ReserveFittingSequence();
                edited.AirSystemType = node.AirSystemType;
                edited.BranchId = node.BranchId;
                edited.DeltaXM = node.DeltaXM;
                edited.DeltaYM = node.DeltaYM;
                edited.DeltaZM = node.DeltaZM;
                _ductSystem.Fittings.Add(edited);
                node.LinkedFittingSequence = edited.Sequence;
                linkedFitting = edited;
            }
            else
                CopyFittingValues(edited, linkedFitting);
            SyncConnectionDuctGeometry(node);
            SyncLinkedItemMetadata(node);
            UpdateCalculatedLayoutValues();
            CommitLayoutHistoryState(historyState);
            SaveDuctSystem();
            RefreshLayoutDesigner();
            RefreshDuctSystemGrid();
            _layoutStatusLabel.Text = $"Updated {linkedFitting.Id} without leaving Layout Designer.";
            _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        }
    }

    private bool ConfirmAndApplyJunctionFlows(SavedLayoutNode node, SavedFitting edited, string itemId)
    {
        if (edited.Junction is not JunctionFlowData junction)
            return true;
        var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), junction.CommonAirflowM3PerS * .01);
        var balanced = Math.Abs(junction.CommonAirflowM3PerS
                                - junction.StraightAirflowM3PerS - junction.BranchAirflowM3PerS) <= tolerance;
        if (!balanced)
        {
            var response = MessageBox.Show(this,
                $"{itemId} does not balance: the two path airflows must add up to the common airflow.\r\n\r\nAllow this value anyway and mark the fitting orange?",
                "Airflow Balance Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (response != DialogResult.Yes)
                return false;
            node.AirflowOverride = true;
        }
        else if (!node.AirflowOverride)
            node.AirflowOverride = false;

        if (IsDivergingLayoutNode(node))
        {
            var outlets = LayoutConnectionsFrom(node).OrderBy(x => x.Downstream.Sequence).ToArray();
            foreach (var outlet in outlets)
            {
                var mainPath = string.Equals(outlet.Downstream.BranchId, node.BranchId,
                    StringComparison.OrdinalIgnoreCase);
                var flow = mainPath ? junction.StraightAirflowM3PerS : junction.BranchAirflowM3PerS;
                if (!ConfirmConnectionAirflowOverride(outlet, flow, DuctForConnection(outlet.Downstream, outlet.Secondary)?.Id
                    ?? $"duct from {node.Id}"))
                    return false;
                SetConnectionFlow(outlet, flow);
            }
        }
        else if (IsConvergingLayoutNode(node))
        {
            var inputs = new[]
            {
                new LayoutConnectionRef(node, false),
                new LayoutConnectionRef(node, true)
            }.Where(x => !string.IsNullOrWhiteSpace(x.Secondary
                ? x.Downstream.SecondaryParentNodeId : x.Downstream.ParentNodeId)).ToArray();
            for (var index = 0; index < inputs.Length; index++)
            {
                var flow = index == 0 ? junction.StraightAirflowM3PerS : junction.BranchAirflowM3PerS;
                if (!ConfirmConnectionAirflowOverride(inputs[index], flow,
                        DuctForConnection(node, inputs[index].Secondary)?.Id ?? $"inlet duct to {node.Id}"))
                    return false;
                SetConnectionFlow(inputs[index], flow);
            }
            edited.AirflowM3PerS = junction.StraightAirflowM3PerS + junction.BranchAirflowM3PerS;
        }
        return true;

        void SetConnectionFlow(LayoutConnectionRef connection, double flow)
        {
            var duct = DuctForConnection(connection.Downstream, connection.Secondary);
            if (duct is not null)
                duct.AirflowM3PerS = flow;
            if (!connection.Downstream.AirflowUserSpecified)
                connection.Downstream.AirflowM3PerS = flow;
        }
    }

    private SavedFitting CreateLayoutFittingDraft(SavedLayoutNode node)
    {
        var definition = FittingCatalog.All.FirstOrDefault(x =>
                             string.Equals(FittingCatalog.SelectionGroup(x), node.FittingGroup, StringComparison.OrdinalIgnoreCase))
                         ?? FittingCatalog.All.First();
        var airflow = node.AirflowM3PerS > 0 ? node.AirflowM3PerS : UnitConversions.CfmToM3PerS(1_000);
        var diameter = UnitConversions.InchesToM(12);
        var inputs = definition.Inputs.ToDictionary(x => x.Key, x => x.DefaultValue, StringComparer.OrdinalIgnoreCase);
        if (string.Equals(node.FittingGroup, "Elbows", StringComparison.OrdinalIgnoreCase)
            && inputs.ContainsKey("angle"))
            inputs["angle"] = node.ElbowAngleDegrees;
        return new SavedFitting
        {
            FittingCode = definition.Code,
            Description = $"{definition.Code} — {definition.Description}",
            AirSystemType = node.AirSystemType,
            BranchId = node.BranchId,
            Inputs = inputs,
            AirflowM3PerS = airflow,
            Shape = DuctShape.Round,
            DiameterM = diameter,
            TemperatureC = node.TemperatureC,
            RelativeHumidity = FixedRelativeHumidity,
            AtmosphericPressurePa = FixedAtmosphericPressurePa
        };
    }

    private static SavedFitting CloneFitting(SavedFitting fitting) => new()
    {
        Sequence = fitting.Sequence,
        CustomId = fitting.CustomId,
        Description = fitting.Description,
        AirSystemType = fitting.AirSystemType,
        BranchId = fitting.BranchId,
        FittingCode = fitting.FittingCode,
        Inputs = new Dictionary<string, double>(fitting.Inputs, StringComparer.OrdinalIgnoreCase),
        AirflowM3PerS = fitting.AirflowM3PerS,
        Shape = fitting.Shape,
        DiameterM = fitting.DiameterM,
        WidthM = fitting.WidthM,
        HeightM = fitting.HeightM,
        ReferenceVelocityMPerS = fitting.ReferenceVelocityMPerS,
        TemperatureC = fitting.TemperatureC,
        RelativeHumidity = fitting.RelativeHumidity,
        AtmosphericPressurePa = fitting.AtmosphericPressurePa,
        Junction = fitting.Junction,
        DeltaXM = fitting.DeltaXM,
        DeltaYM = fitting.DeltaYM,
        DeltaZM = fitting.DeltaZM
    };

    private static void CopyFittingValues(SavedFitting source, SavedFitting target)
    {
        target.Description = source.Description;
        target.FittingCode = source.FittingCode;
        target.Inputs = new Dictionary<string, double>(source.Inputs, StringComparer.OrdinalIgnoreCase);
        target.AirflowM3PerS = source.AirflowM3PerS;
        target.Shape = source.Shape;
        target.DiameterM = source.DiameterM;
        target.WidthM = source.WidthM;
        target.HeightM = source.HeightM;
        target.ReferenceVelocityMPerS = source.ReferenceVelocityMPerS;
        target.TemperatureC = source.TemperatureC;
        target.RelativeHumidity = source.RelativeHumidity;
        target.AtmosphericPressurePa = source.AtmosphericPressurePa;
        target.Junction = source.Junction;
    }

    private void ActivateLayoutConnection(SavedLayoutNode downstream) =>
        ActivateLayoutConnectionCore(new LayoutConnectionRef(downstream, false), showConnectionDialog: false);

    private void ActivateLayoutConnection(LayoutConnectionRef connection)
        => ActivateLayoutConnectionCore(connection, showConnectionDialog: true);

    private void ActivateLayoutConnectionCore(LayoutConnectionRef connection, bool showConnectionDialog)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        if (string.IsNullOrWhiteSpace(parentId))
            return;
        var linkedDuctSequence = connection.Secondary
            ? downstream.SecondaryConnectionDuctSequence
            : downstream.ConnectionDuctSequence;
        var linkedDuct = linkedDuctSequence is int existingSequence
            ? _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == existingSequence)
            : null;
        if (showConnectionDialog)
        {
            using var connectionDialog = new LayoutDuctConnectionDialog(parentId, downstream.Id,
                downstream.AirSystemType, linkedDuct is not null);
            if (connectionDialog.ShowDialog(this) != DialogResult.OK)
                return;
            if (connectionDialog.ReverseRequested)
            {
                var reverseFlowHistoryState = CaptureLayoutHistoryState();
                if (connection.Secondary)
                    downstream.SecondaryConnectionFlowReversed = !downstream.SecondaryConnectionFlowReversed;
                else
                    downstream.ConnectionFlowReversed = !downstream.ConnectionFlowReversed;
                CommitLayoutHistoryState(reverseFlowHistoryState);
                SaveDuctSystem();
                RefreshDuctSystemGrid();
                RefreshLayoutDesigner();
                var direction = LayoutConnectionFlowReversed(connection)
                    ? $"{downstream.Id} to {parentId}"
                    : $"{parentId} to {downstream.Id}";
                _layoutStatusLabel.Text = $"Flow direction changed to {direction}.";
                _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
                return;
            }
            downstream.AirSystemType = connectionDialog.SelectedAirSystemType;
            if (linkedDuct is not null)
                linkedDuct.AirSystemType = downstream.AirSystemType;
            SaveDuctSystem();
            RefreshDuctSystemGrid();
            RefreshLayoutDesigner();
        }
        var historyState = CaptureLayoutHistoryState();
        var upstream = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
        var draft = linkedDuct is null
            ? CreateLayoutDuctDraftForConnection(upstream, downstream, connection.Secondary)
            : CloneDuct(linkedDuct);
        using var editor = new LayoutDuctEditDialog(draft, _currentUnits,
            _ductSystem.DefaultSizingIncrementM, _ductSystem.DefaultSizeRoundingDirection);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is not SavedDuct edited)
            return;
        if (!ConfirmConnectionAirflowOverride(connection, edited.AirflowM3PerS,
                linkedDuct?.Id ?? $"duct {parentId} to {downstream.Id}"))
            return;

        if (linkedDuct is null)
        {
            edited.Sequence = _ductSystem.ReserveSequence();
            edited.Description = $"Designer duct {parentId} to {downstream.Id}";
            _ductSystem.Ducts.Add(edited);
            if (connection.Secondary)
                downstream.SecondaryConnectionDuctSequence = edited.Sequence;
            else
                downstream.ConnectionDuctSequence = edited.Sequence;
        }
        else
        {
            CopyDuctValues(edited, linkedDuct);
            linkedDuct.RequiresResizing = false;
        }

        var savedDuct = linkedDuct ?? edited;
        savedDuct.AirSystemType = downstream.AirSystemType;
        savedDuct.BranchId = downstream.BranchId;
        var connectionDelta = ConnectionDelta(downstream, connection.Secondary);
        savedDuct.DeltaXM = connectionDelta.XM;
        savedDuct.DeltaYM = connectionDelta.YM;
        savedDuct.DeltaZM = connectionDelta.ZM;
        downstream.AirflowM3PerS = savedDuct.AirflowM3PerS;
        downstream.TemperatureC = savedDuct.TemperatureC;
        UpdateCalculatedLayoutValues();
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        _layoutStatusLabel.Text = $"Updated {savedDuct.Id} without leaving Layout Designer.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
    }

    private SavedDuct CreateLayoutDuctDraft(SavedLayoutNode? upstream, SavedLayoutNode downstream) =>
        CreateLayoutDuctDraftForConnection(upstream, downstream, secondary: false);

    private SavedDuct CreateLayoutDuctDraftForConnection(SavedLayoutNode? upstream, SavedLayoutNode downstream,
        bool secondary)
    {
        var airflow = downstream.AirflowM3PerS > 0
            ? downstream.AirflowM3PerS
            : upstream?.AirflowM3PerS > 0 ? upstream.AirflowM3PerS : UnitConversions.CfmToM3PerS(1_000);
        var temperature = downstream.TemperatureC;
        var delta = ConnectionDelta(downstream, secondary);
        var length = Math.Sqrt(delta.XM * delta.XM + delta.YM * delta.YM + delta.ZM * delta.ZM);
        if (length <= 0)
            length = UnitConversions.FeetToM(10);
        var material = DuctMaterialCatalog.All.First(x => x.Name == "Galvanized Spiral Duct");
        var increment = _ductSystem.DefaultSizingIncrementM > 0
            ? _ductSystem.DefaultSizingIncrementM
            : UnitConversions.InchesToM(0.5);
        var sizing = DuctSizingSolver.Size(new DuctSizingInput(
            airflow, temperature, FixedRelativeHumidity, FixedAtmosphericPressurePa,
            DuctShape.Round, 1.0, material, _ductSystem.DefaultEquivalentDiameterMethod,
            _ductSystem.DefaultPressureLossBasis, length, CalculationMode.SizeByTargetFriction,
            UnitConversions.InchesWaterPer100FeetToPaPerM(0.08), 1.0, increment,
            _ductSystem.DefaultSizeRoundingDirection));
        return SavedDuct.FromResult(0, string.Empty, sizing.Calculation, downstream.AirSystemType, downstream.BranchId);
    }

    private static SavedDuct CloneDuct(SavedDuct duct) => new()
    {
        Sequence = duct.Sequence,
        Description = duct.Description,
        AirSystemType = duct.AirSystemType,
        BranchId = duct.BranchId,
        AirflowM3PerS = duct.AirflowM3PerS,
        TemperatureC = duct.TemperatureC,
        RelativeHumidity = duct.RelativeHumidity,
        AtmosphericPressurePa = duct.AtmosphericPressurePa,
        Shape = duct.Shape,
        DiameterM = duct.DiameterM,
        WidthM = duct.WidthM,
        HeightM = duct.HeightM,
        MaterialName = duct.MaterialName,
        RoughnessFeet = duct.RoughnessFeet,
        EquivalentDiameterMethod = duct.EquivalentDiameterMethod,
        PressureLossBasis = duct.PressureLossBasis,
        LengthM = duct.LengthM,
        DeltaXM = duct.DeltaXM,
        DeltaYM = duct.DeltaYM,
        DeltaZM = duct.DeltaZM,
        RequiresResizing = duct.RequiresResizing
    };

    private static void CopyDuctValues(SavedDuct source, SavedDuct target)
    {
        target.AirflowM3PerS = source.AirflowM3PerS;
        target.TemperatureC = source.TemperatureC;
        target.RelativeHumidity = source.RelativeHumidity;
        target.AtmosphericPressurePa = source.AtmosphericPressurePa;
        target.Shape = source.Shape;
        target.DiameterM = source.DiameterM;
        target.WidthM = source.WidthM;
        target.HeightM = source.HeightM;
        target.MaterialName = source.MaterialName;
        target.RoughnessFeet = source.RoughnessFeet;
        target.EquivalentDiameterMethod = source.EquivalentDiameterMethod;
        target.PressureLossBasis = source.PressureLossBasis;
        target.LengthM = source.LengthM;
        target.RequiresResizing = source.RequiresResizing;
    }

    private void LoadConnectionConditionsIntoCalculator(SavedLayoutNode downstream)
    {
        var parentId = _pendingConnectionSecondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var upstream = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
        var airflow = downstream.AirflowM3PerS > 0 ? downstream.AirflowM3PerS : upstream?.AirflowM3PerS ?? 0.0;
        var temperature = downstream.TemperatureC;
        _updatingControls = true;
        try
        {
            if (airflow > 0)
                _airflowText.Text = (_currentUnits == UnitSystem.Imperial
                    ? UnitConversions.M3PerSToCfm(airflow)
                    : UnitConversions.M3PerSToLps(airflow)).ToString("0.##", CultureInfo.CurrentCulture);
            _temperatureText.Text = (_currentUnits == UnitSystem.Imperial
                ? UnitConversions.CelsiusToFahrenheit(temperature)
                : temperature).ToString("0.##", CultureInfo.CurrentCulture);
        }
        finally
        {
            _updatingControls = false;
        }
        UpdateControlState();
        CalculateAndDisplay();
    }

    private void RemoveLayoutNode(SavedLayoutNode node)
    {
        var answer = MessageBox.Show(this,
            $"Remove {node.Id} from the graphical layout? Its calculated Duct System record will be retained.",
            "Remove Layout Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;
        var historyState = CaptureLayoutHistoryState();
        RemoveLayoutNodePreservingCoordinates(node);
        CommitLayoutHistoryState(historyState);
        if (_pendingLayoutNodeId == node.Id)
            _pendingLayoutNodeId = null;
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
    }

    private void RemoveLayoutNodePreservingCoordinates(SavedLayoutNode node)
    {
        var coordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        InvalidateConnectionDuct(node, secondary: false);
        InvalidateConnectionDuct(node, secondary: true);
        foreach (var child in _ductSystem.LayoutNodes.Where(x => x.ParentNodeId == node.Id).ToArray())
        {
            InvalidateConnectionDuct(child);
            child.ParentNodeId = string.Empty;
            child.ConnectionDeltaConfirmed = false;
            PreserveNodeCoordinateAgainstParent(child, null, coordinates);
        }
        foreach (var child in _ductSystem.LayoutNodes.Where(x => x.SecondaryParentNodeId == node.Id).ToArray())
        {
            InvalidateConnectionDuct(child, secondary: true);
            child.SecondaryParentNodeId = string.Empty;
            child.SecondaryConnectionDeltaConfirmed = false;
            PreserveNodeCoordinateAgainstParent(child, child.ParentNodeId, coordinates);
        }
        _ductSystem.LayoutNodes.Remove(node);
        if (string.Equals(_ductSystem.CoordinateOriginNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
        {
            var replacementOrigin = _ductSystem.LayoutNodes
                .Where(x => x.Type != LayoutNodeType.CalculationPoint && coordinates.ContainsKey(x.Sequence))
                .OrderBy(x => x.Sequence).FirstOrDefault();
            _ductSystem.CoordinateOriginNodeId = replacementOrigin?.Id ?? string.Empty;
            if (replacementOrigin is not null && coordinates.TryGetValue(replacementOrigin.Sequence, out var originCoordinate))
            {
                _ductSystem.CoordinateOriginXM = originCoordinate.XM;
                _ductSystem.CoordinateOriginYM = originCoordinate.YM;
                _ductSystem.CoordinateOriginZM = originCoordinate.ZM;
                replacementOrigin.HasAbsoluteCoordinateAnchor = false;
            }
        }
        RebaseLayoutToAbsoluteCoordinates(coordinates);
    }

    private void RemoveLayoutConnection(LayoutConnectionRef connection)
    {
        var parentId = connection.Secondary
            ? connection.Downstream.SecondaryParentNodeId
            : connection.Downstream.ParentNodeId;
        if (string.IsNullOrWhiteSpace(parentId))
            return;
        var ductSequence = connection.Secondary
            ? connection.Downstream.SecondaryConnectionDuctSequence
            : connection.Downstream.ConnectionDuctSequence;
        var ductLabel = ductSequence is int sequence ? $"D-{sequence:000}" : "this unsized duct";
        var answer = MessageBox.Show(this,
            $"Remove {ductLabel} between {parentId} and {connection.Downstream.Id}? The graphical connection and its Duct System row will be deleted.",
            "Remove Duct Connection", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;
        var historyState = CaptureLayoutHistoryState();
        RemoveLayoutConnectionPreservingCoordinates(connection);
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
    }

    private void RemoveLayoutConnectionPreservingCoordinates(LayoutConnectionRef connection)
    {
        var coordinates = LayoutCoordinateResolver.Resolve(_ductSystem);
        InvalidateConnectionDuct(connection.Downstream, connection.Secondary);
        if (connection.Secondary)
        {
            connection.Downstream.SecondaryParentNodeId = string.Empty;
            connection.Downstream.SecondaryConnectionDeltaConfirmed = false;
        }
        else
        {
            connection.Downstream.ParentNodeId = string.Empty;
            connection.Downstream.ConnectionDeltaConfirmed = false;
        }
        PreserveNodeCoordinateAgainstParent(connection.Downstream,
            !string.IsNullOrWhiteSpace(connection.Downstream.ParentNodeId)
                ? connection.Downstream.ParentNodeId : connection.Downstream.SecondaryParentNodeId,
            coordinates);
        RebaseLayoutToAbsoluteCoordinates(coordinates);
    }

    private void PreserveNodeCoordinateAgainstParent(SavedLayoutNode node, string? parentId,
        IReadOnlyDictionary<int, LayoutCoordinate> coordinates)
    {
        if (!coordinates.TryGetValue(node.Sequence, out var coordinate))
            return;
        var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
            string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
        if (parent is not null && coordinates.TryGetValue(parent.Sequence, out var parentCoordinate))
        {
            var secondary = string.Equals(parentId, node.SecondaryParentNodeId,
                StringComparison.OrdinalIgnoreCase);
            SetConnectionDelta(node, secondary, coordinate.XM - parentCoordinate.XM,
                coordinate.YM - parentCoordinate.YM, coordinate.ZM - parentCoordinate.ZM);
            node.HasAbsoluteCoordinateAnchor = false;
            SyncConnectionDuctGeometry(node);
            SyncLinkedItemMetadata(node);
            return;
        }
        node.HasAbsoluteCoordinateAnchor = true;
        node.AbsoluteAnchorXM = coordinate.XM;
        node.AbsoluteAnchorYM = coordinate.YM;
        node.AbsoluteAnchorZM = coordinate.ZM;
    }

    private void AnchorDisconnectedLayoutRoots(IReadOnlyDictionary<int, LayoutCoordinate> coordinates)
    {
        var nodeIds = _ductSystem.LayoutNodes.Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _ductSystem.LayoutNodes.Where(x => x.Type != LayoutNodeType.CalculationPoint
                     && !string.Equals(x.Id, _ductSystem.CoordinateOriginNodeId, StringComparison.OrdinalIgnoreCase)
                     && (string.IsNullOrWhiteSpace(x.ParentNodeId) || !nodeIds.Contains(x.ParentNodeId))
                     && (string.IsNullOrWhiteSpace(x.SecondaryParentNodeId) || !nodeIds.Contains(x.SecondaryParentNodeId))))
        {
            if (!coordinates.TryGetValue(root.Sequence, out var coordinate))
                continue;
            root.HasAbsoluteCoordinateAnchor = true;
            root.AbsoluteAnchorXM = coordinate.XM;
            root.AbsoluteAnchorYM = coordinate.YM;
            root.AbsoluteAnchorZM = coordinate.ZM;
        }
    }

    private void RebaseLayoutToAbsoluteCoordinates(IReadOnlyDictionary<int, LayoutCoordinate> coordinates)
    {
        var nodes = _ductSystem.LayoutNodes
            .Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (nodes.Count == 0)
            return;

        if (!nodes.TryGetValue(_ductSystem.CoordinateOriginNodeId, out var origin)
            || !coordinates.TryGetValue(origin.Sequence, out var originCoordinate))
        {
            origin = nodes.Values.Where(x => coordinates.ContainsKey(x.Sequence))
                .OrderBy(x => x.Sequence).FirstOrDefault();
            if (origin is not null && coordinates.TryGetValue(origin.Sequence, out originCoordinate))
                _ductSystem.CoordinateOriginNodeId = origin.Id;
        }

        if (origin is not null && coordinates.TryGetValue(origin.Sequence, out originCoordinate))
        {
            _ductSystem.CoordinateOriginXM = originCoordinate.XM;
            _ductSystem.CoordinateOriginYM = originCoordinate.YM;
            _ductSystem.CoordinateOriginZM = originCoordinate.ZM;
        }

        foreach (var node in nodes.Values)
        {
            if (!coordinates.TryGetValue(node.Sequence, out var coordinate))
                continue;

            var cellFeet = LayoutFeetPerGridCell;
            var x = UnitConversions.MToFeet(coordinate.XM) / cellFeet;
            var y = UnitConversions.MToFeet(coordinate.YM) / cellFeet;
            var z = UnitConversions.MToFeet(coordinate.ZM) / cellFeet;
            node.GridX = (int)Math.Round(x - y);
            node.GridY = (int)Math.Round(x + y - 2.0 * z);

            var primaryParent = nodes.TryGetValue(node.ParentNodeId, out var primary) ? primary : null;
            var secondaryParent = nodes.TryGetValue(node.SecondaryParentNodeId, out var secondary) ? secondary : null;
            var hasResolvedParent = !string.Equals(node.Id, origin?.Id, StringComparison.OrdinalIgnoreCase)
                                    && (primaryParent is not null || secondaryParent is not null);
            if (hasResolvedParent)
            {
                if (primaryParent is not null
                    && coordinates.TryGetValue(primaryParent.Sequence, out var primaryCoordinate))
                {
                    SetConnectionDelta(node, false, coordinate.XM - primaryCoordinate.XM,
                        coordinate.YM - primaryCoordinate.YM, coordinate.ZM - primaryCoordinate.ZM);
                    node.ConnectionDeltaConfirmed = true;
                }
                if (secondaryParent is not null
                    && coordinates.TryGetValue(secondaryParent.Sequence, out var secondaryCoordinate))
                {
                    SetConnectionDelta(node, true, coordinate.XM - secondaryCoordinate.XM,
                        coordinate.YM - secondaryCoordinate.YM, coordinate.ZM - secondaryCoordinate.ZM);
                    node.SecondaryConnectionDeltaConfirmed = true;
                }
                node.HasAbsoluteCoordinateAnchor = false;
                SyncConnectionDuctGeometry(node);
                SyncLinkedItemMetadata(node);
            }
            else if (!string.Equals(node.Id, origin?.Id, StringComparison.OrdinalIgnoreCase))
            {
                node.HasAbsoluteCoordinateAnchor = true;
                node.AbsoluteAnchorXM = coordinate.XM;
                node.AbsoluteAnchorYM = coordinate.YM;
                node.AbsoluteAnchorZM = coordinate.ZM;
            }
            else
            {
                node.HasAbsoluteCoordinateAnchor = false;
            }
        }

        AnchorDisconnectedLayoutRoots(coordinates);
    }

    private void ClearLayoutDesigner()
    {
        if (_ductSystem.LayoutNodes.Count == 0)
            return;
        var answer = MessageBox.Show(this,
            "Clear every item from the graphical layout?\r\n\r\nYes = clear the layout AND delete all Duct System table values\r\nNo = clear the layout only and retain calculated duct and fitting rows\r\nCancel = keep everything",
            "Clear Layout", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button3);
        if (answer == DialogResult.Cancel)
            return;
        var historyState = CaptureLayoutHistoryState();
        if (answer == DialogResult.Yes)
        {
            ClearAllSystemData();
            CommitLayoutHistoryState(historyState);
            return;
        }
        _ductSystem.LayoutNodes.Clear();
        _ductSystem.CoordinateOriginNodeId = string.Empty;
        _ductSystem.CoordinateOriginXM = 0;
        _ductSystem.CoordinateOriginYM = 0;
        _ductSystem.CoordinateOriginZM = 0;
        _layoutViewportInitialized = false;
        ResetCriticalPath();
        _pendingLayoutNodeId = null;
        _pendingConnectionNodeId = null;
        _pendingConnectionSecondary = false;
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        EnsureLayoutViewportPositioned(force: true);
    }

    private bool RewireLayoutNode(SavedLayoutNode node, string newParentId, bool reposition)
    {
        newParentId = newParentId?.Trim() ?? string.Empty;
        if (string.Equals(node.ParentNodeId, newParentId, StringComparison.OrdinalIgnoreCase))
            return true;

        var newParent = string.IsNullOrWhiteSpace(newParentId)
            ? null
            : _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == newParentId);
        if (!string.IsNullOrWhiteSpace(newParentId) && newParent is null)
        {
            MessageBox.Show(this, "The selected upstream item no longer exists.", "Upstream Item Not Found",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        if (newParent is not null && (newParent.Id == node.Id || IsLayoutDescendant(newParent, node)))
        {
            MessageBox.Show(this, "That change would create a circular layout connection.", "Invalid Upstream Item",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var existingChildren = newParent is null
            ? Array.Empty<SavedLayoutNode>()
            : _ductSystem.LayoutNodes.Where(x => x.ParentNodeId == newParent.Id && x.Id != node.Id).ToArray();
        var splittingParent = newParent is not null && IsSplittingFitting(newParent);
        if (splittingParent && existingChildren.Length >= 2)
        {
            MessageBox.Show(this, $"{newParent!.Id} already has two downstream connections.",
                "Two Outputs Already Used", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var oldGrid = new Point(node.GridX, node.GridY);
        var displacedChild = !splittingParent && existingChildren.Length == 1
            ? existingChildren[0]
            : null;
        var createNewBranch = false;
        if (splittingParent && existingChildren.Length == 1)
        {
            createNewBranch = string.Equals(existingChildren[0].BranchId, newParent!.BranchId,
                StringComparison.OrdinalIgnoreCase);
        }
        else if (splittingParent)
        {
            using var branchDialog = new LayoutBranchChoiceDialog(newParent!.Id, node.Id);
            if (branchDialog.ShowDialog(this) != DialogResult.OK)
                return false;
            createNewBranch = branchDialog.NewBranchSelected;
        }

        Point? target = null;
        if (newParent is not null && reposition)
            target = LayoutInsertionPoint(newParent, displacedChild, existingChildren.Length, node);
        if (target is Point insertionPoint)
            MoveLayoutSubtree(node, insertionPoint.X - oldGrid.X, insertionPoint.Y - oldGrid.Y);

        if (newParent is not null)
        {
            var oldDelta = (node.DeltaXM, node.DeltaYM, node.DeltaZM, node.ConnectionDeltaConfirmed);
            SetDefaultNodeDeltas(node, newParent);
            node.ConnectionDeltaConfirmed = false;
            if ((node.IsDefined || newParent.IsDefined) && !ConfirmConnectionCoordinates(node, newParent, false))
            {
                MoveLayoutSubtree(node, oldGrid.X - node.GridX, oldGrid.Y - node.GridY);
                node.DeltaXM = oldDelta.DeltaXM;
                node.DeltaYM = oldDelta.DeltaYM;
                node.DeltaZM = oldDelta.DeltaZM;
                node.ConnectionDeltaConfirmed = oldDelta.ConnectionDeltaConfirmed;
                return false;
            }
        }

        InvalidateConnectionDuct(node);
        node.ParentNodeId = newParent?.Id ?? string.Empty;
        if (newParent is null)
            node.ConnectionDeltaConfirmed = false;

        if (newParent is not null)
        {
            node.AirSystemType = newParent.AirSystemType;
            node.BranchId = newParent.BranchId;
            if (newParent.AirflowM3PerS > 0)
                node.AirflowM3PerS = newParent.AirflowM3PerS;

            if (createNewBranch)
                node.BranchId = _ductSystem.ReserveBranchId();

            if (displacedChild is not null)
            {
                InvalidateConnectionDuct(displacedChild);
                displacedChild.ParentNodeId = node.Id;
                displacedChild.ConnectionDeltaConfirmed = false;
                if (node.AirflowM3PerS > 0)
                    displacedChild.AirflowM3PerS = node.AirflowM3PerS;
            }

        }

        SyncLinkedItemMetadata(node);
        if (newParent is not null)
        {
            var elbows = AddAutomaticElbowsForMultiAxisConnection(node, secondary: false);
            if (elbows.Count == 0)
                EnsureInitialConnectionDuct(node, newParent, secondary: false);
        }
        if (displacedChild is not null)
        {
            SetDefaultNodeDeltas(displacedChild, node);
            displacedChild.ConnectionDeltaConfirmed = true;
            var elbows = AddAutomaticElbowsForMultiAxisConnection(displacedChild, secondary: false);
            if (elbows.Count == 0)
                EnsureInitialConnectionDuct(displacedChild, node, secondary: false);
            SyncLinkedItemMetadata(displacedChild);
        }
        return true;
    }

    private void SyncConnectionDuctGeometry(SavedLayoutNode downstream)
    {
        Sync(downstream.ConnectionDuctSequence, secondary: false);
        Sync(downstream.SecondaryConnectionDuctSequence, secondary: true);
        return;

        void Sync(int? sequence, bool secondary)
        {
            if (sequence is not int ductSequence)
                return;
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductSequence);
            if (duct is null)
                return;
            var delta = ConnectionDelta(downstream, secondary);
            duct.DeltaXM = delta.XM;
            duct.DeltaYM = delta.YM;
            duct.DeltaZM = delta.ZM;
            var length = Math.Sqrt(duct.DeltaXM * duct.DeltaXM + duct.DeltaYM * duct.DeltaYM
                                   + duct.DeltaZM * duct.DeltaZM);
            if (length > 0)
                duct.LengthM = length;
        }
    }

    private static LayoutEditableAxes EditableAxesForNode(SavedLayoutNode node) => new(true, true, true);

    /// <summary>
    /// The component whose position an edit of <paramref name="node"/> is entered relative to: its upstream
    /// component (primary, else second inlet); when it has none, its first downstream component; null when it is
    /// not connected to any positioned component (the position is then entered as absolute X/Y/Z).
    /// </summary>
    private LayoutPositionReference? PositionReferenceFor(SavedLayoutNode node,
        IReadOnlyDictionary<int, LayoutCoordinate> coordinates)
    {
        var byId = _ductSystem.LayoutNodes
            .Where(candidate => candidate.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var parentId in new[] { node.ParentNodeId, node.SecondaryParentNodeId })
        {
            if (!string.IsNullOrWhiteSpace(parentId) && byId.TryGetValue(parentId, out var parent)
                && coordinates.TryGetValue(parent.Sequence, out var parentCoordinate))
                return new LayoutPositionReference(parent.Id, parentCoordinate, IsDownstream: false);
        }
        var downstream = _ductSystem.LayoutNodes
            .Where(candidate => candidate.Type != LayoutNodeType.CalculationPoint
                                && (string.Equals(candidate.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(candidate.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(candidate => candidate.Sequence)
            .FirstOrDefault(candidate => coordinates.ContainsKey(candidate.Sequence));
        return downstream is null ? null
            : new LayoutPositionReference(downstream.Id, coordinates[downstream.Sequence], IsDownstream: true);
    }

    private void ApplyAbsoluteCoordinateEdit(SavedLayoutNode node, LayoutCoordinate original,
        LayoutCoordinate requested)
    {
        const double tolerance = 1e-8;
        var dx = requested.XM - original.XM;
        var dy = requested.YM - original.YM;
        var dz = requested.ZM - original.ZM;
        if (Math.Abs(dx) <= tolerance && Math.Abs(dy) <= tolerance && Math.Abs(dz) <= tolerance)
            return;

        var hasParent = !string.IsNullOrWhiteSpace(node.ParentNodeId)
                        || !string.IsNullOrWhiteSpace(node.SecondaryParentNodeId);
        if (hasParent)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
                SetConnectionDelta(node, false, node.DeltaXM + dx, node.DeltaYM + dy, node.DeltaZM);
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
                SetConnectionDelta(node, true, node.SecondaryDeltaXM + dx, node.SecondaryDeltaYM + dy,
                    node.SecondaryDeltaZM);
        }
        else
        {
            _ductSystem.CoordinateOriginNodeId = node.Id;
            _ductSystem.CoordinateOriginXM += dx;
            _ductSystem.CoordinateOriginYM += dy;
        }

        var children = _ductSystem.LayoutNodes.Where(child =>
            string.Equals(child.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var child in children)
        {
            // Keep downstream absolute X/Y positions fixed while the selected item slides along a straight run.
            var secondary = string.Equals(child.SecondaryParentNodeId, node.Id,
                StringComparison.OrdinalIgnoreCase);
            var delta = ConnectionDelta(child, secondary);
            SetConnectionDelta(child, secondary, delta.XM - dx, delta.YM - dy, delta.ZM);
            SyncConnectionDuctGeometry(child);
            SyncLinkedItemMetadata(child);
        }
        SyncConnectionDuctGeometry(node);
        SyncLinkedItemMetadata(node);

        if (Math.Abs(dz) > tolerance)
        {
            // Elevation is a connected-system property in the Designer. Translating one connected
            // component therefore translates every component on both sides of it, while retaining
            // all relative duct geometry and leaving disconnected systems untouched.
            var connectedSequences = ConnectedLayoutNodeSequences(node);
            var translatedCoordinates = LayoutCoordinateResolver.Resolve(_ductSystem)
                .ToDictionary(pair => pair.Key, pair => connectedSequences.Contains(pair.Key)
                    ? new LayoutCoordinate(pair.Value.XM, pair.Value.YM, pair.Value.ZM + dz)
                    : pair.Value);
            RebaseLayoutToAbsoluteCoordinates(translatedCoordinates);
        }
    }

    private HashSet<int> ConnectedLayoutNodeSequences(SavedLayoutNode start)
    {
        var nodesById = _ductSystem.LayoutNodes
            .Where(candidate => candidate.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase);
        var connected = new HashSet<int>();
        var pending = new Queue<SavedLayoutNode>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!connected.Add(current.Sequence))
                continue;

            foreach (var parentId in new[] { current.ParentNodeId, current.SecondaryParentNodeId }
                         .Where(parentId => !string.IsNullOrWhiteSpace(parentId)))
                if (nodesById.TryGetValue(parentId, out var parent))
                    pending.Enqueue(parent);

            foreach (var child in nodesById.Values.Where(candidate =>
                         string.Equals(candidate.ParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(candidate.SecondaryParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                pending.Enqueue(child);
        }
        return connected;
    }

    private void ToggleLayoutCriticalPathHighlight()
    {
        if (!_criticalPathCalculated || _criticalPathItemKeys.Count == 0)
            RefreshDuctSystemGrid();
        if (!_criticalPathCalculated || _criticalPathItemKeys.Count == 0)
        {
            _layoutStatusLabel.Text = "Add and define a connected system before highlighting its critical path.";
            _layoutStatusLabel.ForeColor = Color.DarkOrange;
            return;
        }

        _layoutCriticalPathHighlighted = !_layoutCriticalPathHighlighted;
        if (_layoutCanvas is not null)
            _layoutCanvas.HighlightCriticalPath = _layoutCriticalPathHighlighted;
        UpdateLayoutCriticalPathButton();
        _layoutCanvas?.Invalidate();
        _layoutStatusLabel.Text = _layoutCriticalPathHighlighted
            ? "Critical-path components and ducts are highlighted in amber."
            : "Critical-path highlighting is off.";
        _layoutStatusLabel.ForeColor = _layoutCriticalPathHighlighted
            ? Color.FromArgb(174, 104, 0) : Color.FromArgb(72, 92, 110);
    }

    private void UpdateLayoutCriticalPathButton()
    {
        _layoutCriticalPathButton.Text = _layoutCriticalPathHighlighted
            ? "Unhighlight Critical Path" : "Highlight Critical Path";
        _layoutCriticalPathButton.BackColor = _layoutCriticalPathHighlighted
            ? Color.FromArgb(230, 145, 31) : Color.White;
        _layoutCriticalPathButton.ForeColor = _layoutCriticalPathHighlighted
            ? Color.White : Color.FromArgb(28, 69, 108);
    }

    private bool IsLayoutNodeOnCriticalPath(SavedLayoutNode node)
    {
        if (!_layoutCriticalPathHighlighted || !_criticalPathCalculated)
            return false;
        return IsLayoutNodeInCalculatedCriticalPath(node);
    }

    private bool IsLayoutNodeInCalculatedCriticalPath(SavedLayoutNode node)
    {
        if (!_criticalPathCalculated)
            return false;
        if (node.LinkedFittingSequence is int fittingSequence
            && _criticalPathItemKeys.Contains(FittingKey(fittingSequence)))
            return true;
        if (node.LinkedDuctSequence is int ductSequence
            && _criticalPathItemKeys.Contains(DuctKey(ductSequence)))
            return true;
        return _criticalPathItemKeys.Contains(LayoutKey(node.Sequence));
    }

    private bool IsLayoutConnectionOnCriticalPath(LayoutConnectionRef connection)
    {
        if (!_layoutCriticalPathHighlighted || !_criticalPathCalculated)
            return false;
        return IsLayoutConnectionInCalculatedCriticalPath(connection);
    }

    private bool IsLayoutConnectionInCalculatedCriticalPath(LayoutConnectionRef connection)
    {
        if (!_criticalPathCalculated)
            return false;
        var sequence = connection.Secondary
            ? connection.Downstream.SecondaryConnectionDuctSequence
            : connection.Downstream.ConnectionDuctSequence;
        return sequence is int ductSequence && _criticalPathItemKeys.Contains(DuctKey(ductSequence));
    }

    private bool IsLayoutDescendant(SavedLayoutNode possibleDescendant, SavedLayoutNode ancestor)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<SavedLayoutNode>();
        pending.Enqueue(possibleDescendant);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current.Id))
                continue;
            foreach (var parentId in new[] { current.ParentNodeId, current.SecondaryParentNodeId }.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (string.Equals(parentId, ancestor.Id, StringComparison.OrdinalIgnoreCase))
                    return true;
                var parent = _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == parentId);
                if (parent is not null)
                    pending.Enqueue(parent);
            }
        }
        return false;
    }

    private Point LayoutInsertionPoint(SavedLayoutNode parent, SavedLayoutNode? downstream, int siblingCount,
        SavedLayoutNode movingNode)
    {
        if (downstream is not null)
        {
            var dx = downstream.GridX - parent.GridX;
            var dy = downstream.GridY - parent.GridY;
            if (Math.Abs(dx) >= Math.Abs(dy) && Math.Abs(dx) < 8)
            {
                ShiftLayoutSubtreeExcept(downstream, movingNode, Math.Sign(dx == 0 ? 1 : dx) * (8 - Math.Abs(dx)), 0);
                dx = downstream.GridX - parent.GridX;
                dy = downstream.GridY - parent.GridY;
            }
            else if (Math.Abs(dy) < 8)
            {
                ShiftLayoutSubtreeExcept(downstream, movingNode, 0, Math.Sign(dy == 0 ? 1 : dy) * (8 - Math.Abs(dy)));
                dx = downstream.GridX - parent.GridX;
                dy = downstream.GridY - parent.GridY;
            }
            return new Point(parent.GridX + dx / 2, parent.GridY + dy / 2);
        }
        return new Point(parent.GridX + 8, parent.GridY + siblingCount * 12);
    }

    private void MoveLayoutSubtree(SavedLayoutNode root, int deltaX, int deltaY)
    {
        root.GridX += deltaX;
        root.GridY += deltaY;
        foreach (var child in _ductSystem.LayoutNodes.Where(x => x.ParentNodeId == root.Id).ToArray())
            MoveLayoutSubtree(child, deltaX, deltaY);
    }

    private void ShiftLayoutSubtreeExcept(SavedLayoutNode root, SavedLayoutNode excluded, int deltaX, int deltaY)
    {
        if (root.Id == excluded.Id)
            return;
        root.GridX += deltaX;
        root.GridY += deltaY;
        foreach (var child in _ductSystem.LayoutNodes.Where(x => x.ParentNodeId == root.Id).ToArray())
            ShiftLayoutSubtreeExcept(child, excluded, deltaX, deltaY);
    }

    private void InvalidateConnectionDuct(SavedLayoutNode downstream, bool secondary = false)
    {
        var linkedSequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        if (linkedSequence is not int ductSequence)
        {
            SetConnectionAirflowUserSpecified(downstream, secondary, false);
            SetConnectionAirflowOverride(downstream, secondary, false);
            return;
        }
        var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductSequence);
        if (duct is not null)
            _ductSystem.Ducts.Remove(duct);
        foreach (var point in _ductSystem.LayoutNodes.Where(x => x.Type == LayoutNodeType.CalculationPoint
                                                                  && (x.ObservedDuctSequence == ductSequence
                                                                      || string.Equals(x.CalculationConnectionNodeId,
                                                                          downstream.Id, StringComparison.OrdinalIgnoreCase)
                                                                      && x.CalculationConnectionSecondary == secondary)))
        {
            point.ObservedDuctSequence = null;
            point.CalculationConnectionNodeId = string.Empty;
            point.IsDefined = false;
        }
        if (_editingDuctSequence == ductSequence)
        {
            _editingDuctSequence = null;
            _addDuctButton.Text = DefaultDuctActionText;
            _addDuctButton.Visible = false;
            _ductEditingLabel.Visible = false;
        }
        if (secondary)
            downstream.SecondaryConnectionDuctSequence = null;
        else
            downstream.ConnectionDuctSequence = null;
        SetConnectionAirflowUserSpecified(downstream, secondary, false);
        SetConnectionAirflowOverride(downstream, secondary, false);
    }

    private void ConnectLayoutPorts(SavedLayoutNode upstream, SavedLayoutNode downstream,
        int outputIndex, int inputIndex)
    {
        if (downstream.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(downstream.SpaceRole))
        {
            MessageBox.Show(this, "A Space Source cannot have an upstream duct.",
                "Invalid Space Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (upstream.Id == downstream.Id || IsLayoutDescendant(upstream, downstream))
        {
            MessageBox.Show(this, "That port connection would create a circular layout.", "Invalid Connection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var inputCapacity = LayoutInletCapacity(downstream);
        if (inputIndex >= inputCapacity || ConnectedLayoutInputCount(downstream) >= inputCapacity)
        {
            MessageBox.Show(this, $"{downstream.Id} has no unused inlet ports.", "No Inlet Available",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var secondary = inputIndex == 1;
        if (secondary && FittingCatalog.InletPortCount(downstream.FittingGroup) < 2)
            return;
        var existingOutputs = _ductSystem.LayoutNodes.Count(x =>
            (x.ParentNodeId == upstream.Id && !(x.Id == downstream.Id && !secondary))
            || (x.SecondaryParentNodeId == upstream.Id && !(x.Id == downstream.Id && secondary)));
        var outputCapacity = upstream.Type == LayoutNodeType.Fitting
            ? FittingCatalog.OutletPortCount(upstream.FittingGroup)
            : upstream.Type == LayoutNodeType.Reservoir && IsSpaceSinkRole(upstream.SpaceRole)
                ? 0
                : 1;
        if (existingOutputs >= outputCapacity)
        {
            MessageBox.Show(this, $"{upstream.Id} has no unused outlet ports.", "No Outlet Available",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var oldDelta = (downstream.DeltaXM, downstream.DeltaYM, downstream.DeltaZM,
            downstream.SecondaryDeltaXM, downstream.SecondaryDeltaYM, downstream.SecondaryDeltaZM,
            downstream.ConnectionDeltaConfirmed, downstream.SecondaryConnectionDeltaConfirmed);
        var delta = ConnectionDeltaPreservingAbsolutePosition(upstream, downstream);
        SetConnectionDelta(downstream, secondary, delta.XM, delta.YM, delta.ZM);
        if (!ConfirmConnectionCoordinates(downstream, upstream, secondary))
        {
            downstream.DeltaXM = oldDelta.DeltaXM;
            downstream.DeltaYM = oldDelta.DeltaYM;
            downstream.DeltaZM = oldDelta.DeltaZM;
            downstream.SecondaryDeltaXM = oldDelta.SecondaryDeltaXM;
            downstream.SecondaryDeltaYM = oldDelta.SecondaryDeltaYM;
            downstream.SecondaryDeltaZM = oldDelta.SecondaryDeltaZM;
            downstream.ConnectionDeltaConfirmed = oldDelta.ConnectionDeltaConfirmed;
            downstream.SecondaryConnectionDeltaConfirmed = oldDelta.SecondaryConnectionDeltaConfirmed;
            return;
        }

        var historyState = CaptureLayoutHistoryState();
        InvalidateConnectionDuct(downstream, secondary);
        if (secondary)
            downstream.SecondaryParentNodeId = upstream.Id;
        else
            downstream.ParentNodeId = upstream.Id;
        if (!secondary)
            downstream.HasAbsoluteCoordinateAnchor = false;
        downstream.AirSystemType = upstream.AirSystemType;
        if (FittingCatalog.OutletPortCount(upstream.FittingGroup) > 1)
        {
            if (outputIndex == 0)
                downstream.BranchId = upstream.BranchId;
            else
            {
                var existingBranch = _ductSystem.LayoutNodes
                    .Where(x => x.ParentNodeId == upstream.Id && x.Id != downstream.Id
                                && !string.Equals(x.BranchId, upstream.BranchId, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.BranchId)
                    .FirstOrDefault();
                downstream.BranchId = string.IsNullOrWhiteSpace(existingBranch)
                    ? _ductSystem.ReserveBranchId()
                    : existingBranch;
            }
        }
        else if (string.IsNullOrWhiteSpace(downstream.BranchId))
            downstream.BranchId = upstream.BranchId;
        if (downstream.AirflowM3PerS <= 0)
            downstream.AirflowM3PerS = upstream.AirflowM3PerS;
        var elbows = AddAutomaticElbowsForMultiAxisConnection(downstream, secondary);
        if (elbows.Count == 0)
            EnsureInitialConnectionDuct(downstream, upstream, secondary);
        SyncLinkedItemMetadata(downstream);
        CommitLayoutHistoryState(historyState);
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        _layoutStatusLabel.Text = $"Connected outlet {outputIndex + 1} on {upstream.Id} to inlet {inputIndex + 1} on {downstream.Id}. The new duct is red until sized.";
    }

    private string LayoutNodeCaption(SavedLayoutNode node) => node.Id;

    private string LayoutConnectionCaption(LayoutConnectionRef connection)
    {
        var sequence = connection.Secondary
            ? connection.Downstream.SecondaryConnectionDuctSequence
            : connection.Downstream.ConnectionDuctSequence;
        return sequence is int ductSequence
            ? $"D-{ductSequence:000}"
            : string.Empty;
    }

    private string LayoutConnectionSizeCaption(LayoutConnectionRef connection)
    {
        var duct = DuctForConnection(connection.Downstream, connection.Secondary);
        if (duct is null)
            return string.Empty;
        return duct.Shape == DuctShape.Round
            ? duct.DiameterM > 0 ? $"{UnitConversions.MToInches(duct.DiameterM):0.##}\"" : string.Empty
            : duct.WidthM > 0 && duct.HeightM > 0
                ? $"{UnitConversions.MToInches(duct.WidthM):0.##}\" / {UnitConversions.MToInches(duct.HeightM):0.##}\""
                : string.Empty;
    }

    private static bool LayoutConnectionFlowReversed(LayoutConnectionRef connection) => connection.Secondary
        ? connection.Downstream.SecondaryConnectionFlowReversed
        : connection.Downstream.ConnectionFlowReversed;

    /// <summary>Result of the last system calculation (inlet static pressure of each node).</summary>
    private SystemSolution _systemSolution = SystemSolution.Empty;

    /// <summary>
    /// Static pressure leaving <paramref name="parent"/> toward <paramref name="child"/>
    /// (see <see cref="SystemSolver.OutletPressureToward"/>).
    /// </summary>
    private double OutletPressureToward(SavedLayoutNode parent, SavedLayoutNode child) =>
        SystemSolver.OutletPressureToward(_ductSystem, _systemSolution, parent, child);

    /// <summary>
    /// Recalculates airflow, temperature and static pressure through the drawn system. The solver lives in
    /// DuctSizer.Core (<see cref="SystemSolver"/>) so it is covered by the engineering verification suite.
    /// </summary>
    private void UpdateCalculatedLayoutValues() => _systemSolution = SystemSolver.Solve(_ductSystem);

    private SavedLayoutNode[] LayoutBoundaryNodes() => _ductSystem.LayoutNodes
        .Where(node => node.Type == LayoutNodeType.Reservoir)
        .OrderBy(node => node.Sequence).ToArray();

    private SavedLayoutNode[] LayoutSpaceSources() => LayoutBoundaryNodes()
        .Where(node => IsSpaceSourceRole(node.SpaceRole)).ToArray();

    private SavedLayoutNode[] LayoutSpaceSinks() => LayoutBoundaryNodes()
        .Where(node => !IsSpaceSourceRole(node.SpaceRole)).ToArray();

    private void ReconcileBoundaryAirflowAfterDirectEdit(SavedLayoutNode edited,
        IReadOnlyDictionary<int, double> originalAirflows)
    {
        var sources = LayoutSpaceSources();
        var sinks = LayoutSpaceSinks();
        if (!IsSpaceSourceRole(edited.SpaceRole) && sources.Length == 1)
        {
            sources[0].AirflowM3PerS = sinks.Sum(sink => Math.Max(0.0, sink.AirflowM3PerS));
            sources[0].AirflowUserSpecified = true;
            ApplyReservoirAirflowChange(edited);
            return;
        }
        if (IsSpaceSourceRole(edited.SpaceRole) && sinks.Length == 1)
        {
            sinks[0].AirflowM3PerS = sources.Sum(source => Math.Max(0.0, source.AirflowM3PerS));
            sinks[0].AirflowUserSpecified = true;
            ApplyReservoirAirflowChange(sinks[0]);
            return;
        }
        if (ShowSourceSinkEditor())
            return;
        foreach (var boundary in LayoutBoundaryNodes())
            if (originalAirflows.TryGetValue(boundary.Sequence, out var airflow))
                boundary.AirflowM3PerS = airflow;
    }

    private bool ShowSourceSinkEditor()
    {
        var boundaries = LayoutBoundaryNodes();
        if (boundaries.Length == 0)
            return false;
        using var editor = new SourceSinkEditorDialog(boundaries);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return false;
        foreach (var boundary in boundaries)
        {
            if (!editor.AirflowsCfm.TryGetValue(boundary.Sequence, out var cfm))
                continue;
            boundary.AirflowM3PerS = UnitConversions.CfmToM3PerS(cfm);
            boundary.AirflowUserSpecified = true;
            boundary.AirflowOverride = false;
        }
        ApplyBalancedBoundaryAirflows(boundaries);
        _layoutStatusLabel.Text = "Source and sink airflow balance updated. Connected ductwork is red until re-sized.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
        return true;
    }

    private void OpenSourceSinkEditor()
    {
        var boundaries = LayoutBoundaryNodes();
        if (boundaries.Length == 0)
        {
            MessageBox.Show(this,
                "Add at least one Space Source and one Space Sink in the Layout Designer before opening the Source/Sink Editor.",
                "Source/Sink Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!boundaries.Any(boundary => IsSpaceSourceRole(boundary.SpaceRole))
            || !boundaries.Any(boundary => !IsSpaceSourceRole(boundary.SpaceRole)))
        {
            MessageBox.Show(this,
                "The Source/Sink Editor requires at least one source and one sink.",
                "Source/Sink Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        UpdateCalculatedLayoutValues();
        var historyState = CaptureLayoutHistoryState();
        if (!ShowSourceSinkEditor())
            return;
        CommitLayoutHistoryState(historyState);
        ResetCriticalPath();
        SaveDuctSystem();
        RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
    }

    private void ApplyBalancedBoundaryAirflows(IReadOnlyCollection<SavedLayoutNode> boundaries)
    {
        foreach (var node in _ductSystem.LayoutNodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
                SetConnectionAirflowUserSpecified(node, false, false);
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
                SetConnectionAirflowUserSpecified(node, true, false);
        }
        var connectedSequences = boundaries.SelectMany(ConnectedLayoutNodeSequences).ToHashSet();
        foreach (var node in _ductSystem.LayoutNodes.Where(node => connectedSequences.Contains(node.Sequence)))
            foreach (var duct in new[] { DuctForConnection(node, false), DuctForConnection(node, true) }
                         .Where(duct => duct is not null))
                duct!.RequiresResizing = true;
        foreach (var sink in boundaries.Where(boundary => !IsSpaceSourceRole(boundary.SpaceRole)))
        {
            if (!string.IsNullOrWhiteSpace(sink.ParentNodeId))
            {
                var duct = DuctForConnection(sink, false);
                if (duct is not null)
                    duct.AirflowM3PerS = sink.AirflowM3PerS;
                SetConnectionAirflowUserSpecified(sink, false, true);
            }
            if (!string.IsNullOrWhiteSpace(sink.SecondaryParentNodeId))
            {
                var duct = DuctForConnection(sink, true);
                if (duct is not null)
                    duct.AirflowM3PerS = sink.AirflowM3PerS;
                SetConnectionAirflowUserSpecified(sink, true, true);
            }
        }
        UpdateCalculatedLayoutValues();
        foreach (var node in _ductSystem.LayoutNodes)
            SyncLinkedItemMetadata(node);
    }

    private void ApplyReservoirAirflowChange(SavedLayoutNode reservoir)
    {
        var connectedSequences = ConnectedLayoutNodeSequences(reservoir);
        var connectedNodes = _ductSystem.LayoutNodes
            .Where(node => connectedSequences.Contains(node.Sequence))
            .ToArray();
        foreach (var node in connectedNodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
            {
                SetConnectionAirflowUserSpecified(node, false, false);
                SetConnectionAirflowOverride(node, false, false);
            }
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
            {
                SetConnectionAirflowUserSpecified(node, true, false);
                SetConnectionAirflowOverride(node, true, false);
            }
        }

        foreach (var duct in connectedNodes.SelectMany(node => new[]
                 {
                     DuctForConnection(node, false), DuctForConnection(node, true)
                 }).Where(duct => duct is not null).Distinct())
            duct!.RequiresResizing = true;

        if (IsSpaceSourceRole(reservoir.SpaceRole))
            return;

        var connectedIds = connectedNodes.Select(node => node.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var memo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in connectedNodes.Where(node =>
                     string.IsNullOrWhiteSpace(node.ParentNodeId)
                     && string.IsNullOrWhiteSpace(node.SecondaryParentNodeId)))
            RequiredFlow(root);

        double RequiredFlow(SavedLayoutNode node)
        {
            if (memo.TryGetValue(node.Id, out var cached))
                return cached;
            if (!active.Add(node.Id))
                return 0.0;

            var isSpecifiedSink = node.Type == LayoutNodeType.Reservoir
                                  && !IsSpaceSourceRole(node.SpaceRole)
                                  && node.AirflowUserSpecified
                                  && node.AirflowM3PerS > 0;
            var connections = LayoutConnectionsFrom(node)
                .Where(connection => connectedIds.Contains(connection.Downstream.Id))
                .ToArray();
            var required = isSpecifiedSink
                ? node.AirflowM3PerS
                : connections.Sum(connection =>
                {
                    var childFlow = RequiredFlow(connection.Downstream);
                    if (childFlow <= 0)
                        return 0.0;
                    var duct = DuctForConnection(connection.Downstream, connection.Secondary);
                    if (duct is not null)
                    {
                        duct.AirflowM3PerS = childFlow;
                        duct.RequiresResizing = true;
                    }
                    SetConnectionAirflowUserSpecified(connection.Downstream, connection.Secondary, true);
                    return childFlow;
                });
            if (required > 0)
                node.AirflowM3PerS = required;
            active.Remove(node.Id);
            memo[node.Id] = required;
            return required;
        }
    }

    private static bool IsDivergingLayoutNode(SavedLayoutNode node) => LayoutTopology.IsDiverging(node);

    private static bool IsConvergingLayoutNode(SavedLayoutNode node) => LayoutTopology.IsConverging(node);

    private IEnumerable<LayoutConnectionRef> LayoutConnectionsFrom(SavedLayoutNode upstream)
    {
        foreach (var node in _ductSystem.LayoutNodes)
        {
            if (string.Equals(node.ParentNodeId, upstream.Id, StringComparison.OrdinalIgnoreCase))
                yield return new LayoutConnectionRef(node, false);
            if (string.Equals(node.SecondaryParentNodeId, upstream.Id, StringComparison.OrdinalIgnoreCase))
                yield return new LayoutConnectionRef(node, true);
        }
    }

    private SavedDuct? DuctForConnection(SavedLayoutNode downstream, bool secondary)
    {
        var sequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        return sequence is int value ? _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == value) : null;
    }

    private static bool ConnectionAirflowUserSpecified(SavedLayoutNode downstream, bool secondary) =>
        SystemSolver.ConnectionAirflowUserSpecified(downstream, secondary);

    private static void SetConnectionAirflowUserSpecified(SavedLayoutNode downstream, bool secondary, bool value)
    {
        if (secondary)
            downstream.SecondaryConnectionAirflowUserSpecified = value;
        else
            downstream.ConnectionAirflowUserSpecified = value;
    }

    private static bool ConnectionAirflowOverride(SavedLayoutNode downstream, bool secondary) => secondary
        ? downstream.SecondaryConnectionAirflowOverride
        : downstream.ConnectionAirflowOverride;

    private static void SetConnectionAirflowOverride(SavedLayoutNode downstream, bool secondary, bool value)
    {
        if (secondary)
            downstream.SecondaryConnectionAirflowOverride = value;
        else
            downstream.ConnectionAirflowOverride = value;
    }

    private double SafeDuctPressureLoss(int sequence) => SystemSolver.DuctLoss(_ductSystem, sequence);

    private double SafeFittingPressureLoss(int sequence) => SystemSolver.FittingLoss(_ductSystem, sequence);

    private string FormatLayoutPressure(double pascals)
    {
        if (!double.IsFinite(pascals))
            return "—";
        return _currentUnits == UnitSystem.Imperial
            ? $"{UnitConversions.PaToInchesWater(pascals):0.###} in.w.g."
            : $"{pascals:0.##} Pa";
    }

    private void RefreshLayoutDesigner()
    {
        ApplyLayoutScaleSettings();
        UpdateLayoutPdfButton();
        _layoutCanvas?.Invalidate();
    }

    private bool EnsureExplicitElbowsForAxisChanges()
    {
        var changed = false;
        var connections = _ductSystem.LayoutNodes.SelectMany(downstream => new[]
        {
            string.IsNullOrWhiteSpace(downstream.ParentNodeId)
                ? (LayoutConnectionRef?)null : new LayoutConnectionRef(downstream, false),
            string.IsNullOrWhiteSpace(downstream.SecondaryParentNodeId)
                ? (LayoutConnectionRef?)null : new LayoutConnectionRef(downstream, true)
        }).Where(connection => connection.HasValue).Select(connection => connection!.Value).ToArray();
        foreach (var connection in connections)
        {
            var downstream = connection.Downstream;
            var delta = ConnectionDelta(downstream, connection.Secondary);
            if (AxisSegments(delta.XM, delta.YM, delta.ZM).Count <= 1)
                continue;
            if (connection.Secondary)
                downstream.SecondaryConnectionDeltaConfirmed = true;
            else
                downstream.ConnectionDeltaConfirmed = true;
            if (AddAutomaticElbowsForMultiAxisConnection(downstream, connection.Secondary).Count > 0)
                changed = true;
        }
        return changed;
    }

    private void ApplyLayoutScaleSettings()
    {
        if (_layoutCanvas is null)
            return;
        var feetPerCell = double.IsFinite(_ductSystem.LayoutFeetPerGridCell)
                          && _ductSystem.LayoutFeetPerGridCell > 0
            ? Math.Clamp(_ductSystem.LayoutFeetPerGridCell, 0.1, 1000.0)
            : 1.0;
        _ductSystem.LayoutShowToScale = true;
        _layoutCanvas.ShowToScale = true;
        _layoutCanvas.FeetPerGridCell = feetPerCell;
        _layoutCanvas.ViewMode = _ductSystem.LayoutViewMode;
        _updatingLayoutViewMode = true;
        try
        {
            _layoutViewModeSelector.SelectedIndex = _ductSystem.LayoutViewMode switch
            {
                LayoutViewMode.IsometricNegative => 1,
                LayoutViewMode.TopDown => 2,
                _ => 0
            };
        }
        finally
        {
            _updatingLayoutViewMode = false;
        }
        var formattedScale = feetPerCell.ToString("0.##", CultureInfo.CurrentCulture);
        if (!_layoutScaleInput.Focused && _layoutScaleInput.Text != formattedScale)
            _layoutScaleInput.Text = formattedScale;
        _layoutScaleInput.Enabled = true;
    }

    private void ApplySelectedLayoutViewMode()
    {
        if (_updatingLayoutViewMode || _layoutViewModeSelector.SelectedIndex < 0)
            return;
        var selected = _layoutViewModeSelector.SelectedIndex switch
        {
            1 => LayoutViewMode.IsometricNegative,
            2 => LayoutViewMode.TopDown,
            _ => LayoutViewMode.Isometric
        };
        if (_ductSystem.LayoutViewMode == selected)
            return;
        var viewAnchor = _layoutCanvas?.SelectedNode ?? LayoutStartNode();
        var anchorScreenPosition = viewAnchor is null || _layoutCanvas is null
            ? (Point?)null : _layoutCanvas.VisualLocation(viewAnchor);
        var historyState = CaptureLayoutHistoryState();
        _ductSystem.LayoutViewMode = selected;
        CommitLayoutHistoryState(historyState);
        ApplyLayoutScaleSettings();
        if (viewAnchor is not null && anchorScreenPosition is Point desiredAnchorPosition)
            _layoutCanvas?.KeepNodeAtScreenPosition(viewAnchor, desiredAnchorPosition);
        SaveDuctSystem();
        _layoutCanvas?.Invalidate();
        _layoutStatusLabel.Text = _ductSystem.LayoutViewMode switch
        {
            LayoutViewMode.TopDown => "Top-down view active: +X runs right, +Y runs down, and component Z remains editable.",
            LayoutViewMode.IsometricNegative => "Isometric Negative view active: the positive grid is rotated 90 degrees counterclockwise (+Y follows the former +X direction; +X follows the former -Y direction).",
            _ => "Isometric Positive view active."
        };
        _layoutStatusLabel.ForeColor = Color.FromArgb(31, 120, 180);
    }

    private bool ApplyLayoutScaleInput()
    {
        if (!double.TryParse(_layoutScaleInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                out var feetPerCell)
            || !double.IsFinite(feetPerCell) || feetPerCell < 0.1 || feetPerCell > 1000.0)
        {
            _layoutScaleInput.Text = _ductSystem.LayoutFeetPerGridCell.ToString("0.##", CultureInfo.CurrentCulture);
            _layoutStatusLabel.Text = "Scale must be a number from 0.1 through 1000 feet per grid cell.";
            _layoutStatusLabel.ForeColor = Color.Firebrick;
            return false;
        }

        _ductSystem.LayoutFeetPerGridCell = feetPerCell;
        ApplyLayoutScaleSettings();
        SaveDuctSystem();
        _layoutStatusLabel.Text = $"Drawing scale set to {feetPerCell:0.##} feet per grid cell.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        return true;
    }

    private void RecalculateAllLayoutDuctwork()
    {
        var historyState = CaptureLayoutHistoryState();
        var resized = 0;
        var failures = new List<string>();
        UpdateCalculatedLayoutValues();
        var connections = _ductSystem.LayoutNodes.OrderBy(node => node.Sequence)
            .SelectMany(node => new[]
            {
                string.IsNullOrWhiteSpace(node.ParentNodeId)
                    ? (LayoutConnectionRef?)null : new LayoutConnectionRef(node, false),
                string.IsNullOrWhiteSpace(node.SecondaryParentNodeId)
                    ? (LayoutConnectionRef?)null : new LayoutConnectionRef(node, true)
            })
            .Where(connection => connection.HasValue)
            .Select(connection => connection!.Value)
            .ToArray();
        foreach (var connection in connections)
        {
            try
            {
                AutoSizeConnectionDuct(connection);
                resized++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                failures.Add($"duct to {connection.Downstream.Id}: {ex.Message}");
            }
        }

        UpdateCalculatedLayoutValues();
        CommitLayoutHistoryState(historyState);
        ResetCriticalPath();
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        _layoutStatusLabel.Text = resized == 0
            ? "No connected ductwork was available to recalculate."
            : $"Recalculated {resized} duct section(s) from the current source and sink airflows."
              + (failures.Count > 0 ? $" {failures.Count} section(s) need attention." : string.Empty);
        _layoutStatusLabel.ForeColor = failures.Count == 0
            ? Color.FromArgb(30, 100, 55) : Color.DarkOrange;
        if (failures.Count > 0 && !AutomatedLayoutVerification)
            MessageBox.Show(this, string.Join("\r\n", failures.Take(12)), "Ductwork Recalculation Notices",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void SizeUndefinedLayoutItems()
    {
        var historyState = CaptureLayoutHistoryState();
        var componentCount = 0;
        var ductCount = 0;
        var failures = new List<string>();
        UpdateCalculatedLayoutValues();

        foreach (var node in _ductSystem.LayoutNodes
                     .Where(x => !x.IsDefined && IsAutomaticSizingComponent(x))
                     .OrderBy(x => x.Sequence).ToArray())
        {
            try
            {
                if (node.Type == LayoutNodeType.Fitting)
                    AutoSizeFittingNode(node);
                else
                    AutoSizeEquipmentNode(node);
                componentCount++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or KeyNotFoundException or OverflowException)
            {
                failures.Add($"{node.Id}: {ex.Message}");
            }
        }

        UpdateCalculatedLayoutValues();
        foreach (var connection in _ductSystem.LayoutNodes.OrderBy(x => x.Sequence)
                     .SelectMany(node => new[]
                     {
                         string.IsNullOrWhiteSpace(node.ParentNodeId) ? (LayoutConnectionRef?)null : new LayoutConnectionRef(node, false),
                         string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) ? (LayoutConnectionRef?)null : new LayoutConnectionRef(node, true)
                     }).Where(x => x.HasValue).Select(x => x!.Value).ToArray())
        {
            var existingDuct = DuctForConnection(connection.Downstream, connection.Secondary);
            if (existingDuct is not null && !existingDuct.RequiresResizing)
                continue;
            try
            {
                AutoSizeConnectionDuct(connection);
                ductCount++;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                failures.Add($"duct to {connection.Downstream.Id}: {ex.Message}");
            }
        }

        if (componentCount == 0 && ductCount == 0)
        {
            var message = failures.Count == 0
                ? "There are no supported undefined components or ducts to size."
                : "No undefined items could be sized. " + string.Join("  ", failures.Take(3));
            if (!AutomatedLayoutVerification)
                MessageBox.Show(this, message, "Size Undefined Components",
                    MessageBoxButtons.OK, failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            _layoutStatusLabel.Text = message;
            _layoutStatusLabel.ForeColor = failures.Count == 0 ? Color.FromArgb(30, 100, 55) : Color.DarkOrange;
            return;
        }

        UpdateCalculatedLayoutValues();
        CommitLayoutHistoryState(historyState);
        ResetCriticalPath();
        SaveDuctSystem();
        RefreshLayoutDesigner();
        RefreshDuctSystemGrid();
        _layoutStatusLabel.Text = $"Automatically sized {componentCount} component(s) and {ductCount} duct(s)."
                                  + (failures.Count > 0 ? $" {failures.Count} item(s) still need attention." : string.Empty);
        _layoutStatusLabel.ForeColor = failures.Count == 0 ? Color.FromArgb(30, 100, 55) : Color.DarkOrange;
        if (failures.Count > 0 && !AutomatedLayoutVerification)
            MessageBox.Show(this,
                "Some items remain undefined:\r\n\r\n" + string.Join("\r\n", failures.Take(12)),
                "Automatic Sizing Completed with Notices", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static bool AutomatedLayoutVerification => AutomationOptions.AcceptDefaultCoordinates;

    private static bool IsAutomaticSizingComponent(SavedLayoutNode node)
    {
        if (node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd)
            return true;
        if (node.Type != LayoutNodeType.Fitting)
            return false;
        return node.FittingGroup is "Elbows" or "Transitions" or "Diverting Fittings"
            or "Converging Fittings" or "Dampers" or "Terminals" or FittingCatalog.TerminalSelectionGroup;
    }

    private void AutoSizeEquipmentNode(SavedLayoutNode node)
    {
        var state = AutomaticFlowState(node);
        if (state.AirflowM3PerS <= 0)
            throw new InvalidOperationException("No connected defined item provides a positive airflow.");
        node.AirflowM3PerS = state.AirflowM3PerS;
        node.AirflowUserSpecified = false;
        node.AirflowOverride = false;
        node.TemperatureC = state.TemperatureC;
        switch (node.Type)
        {
            case LayoutNodeType.Coil:
                node.TemperatureC = _ductSystem.AutomaticSizing.Coil.TemperatureC;
                node.PressureDropPa = _ductSystem.AutomaticSizing.Coil.PressureDropPa;
                break;
            case LayoutNodeType.Filter:
                node.PressureDropPa = _ductSystem.AutomaticSizing.Filter.PressureDropPa;
                break;
            case LayoutNodeType.Grd:
                node.GrdType = _ductSystem.AutomaticSizing.Grd.GrdType;
                node.GrdNominalDuctSizeIn = _ductSystem.AutomaticSizing.Grd.NominalDuctSizeIn;
                node.GrdFaceSizeIn = _ductSystem.AutomaticSizing.Grd.FaceSizeIn;
                node.GrdNeckDiameterIn = _ductSystem.AutomaticSizing.Grd.NeckDiameterIn;
                node.GrdLengthFt = _ductSystem.AutomaticSizing.Grd.LengthFt;
                node.GrdSlotWidthIn = _ductSystem.AutomaticSizing.Grd.SlotWidthIn;
                node.GrdSlotCount = _ductSystem.AutomaticSizing.Grd.SlotCount;
                var grdCfm = UnitConversions.M3PerSToCfm(node.AirflowM3PerS);
                if (string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase))
                {
                    var selection = GrdPerformanceCatalog.AutoSelectTitus350R(grdCfm,
                        node.GrdNominalDuctSizeIn);
                    ApplyGrdPerformance(node, selection);
                }
                else if (string.Equals(node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase))
                {
                    var selection = GrdPerformanceCatalog.SelectPriceScd(grdCfm,
                        Convert.ToInt32(node.GrdFaceSizeIn), Convert.ToInt32(node.GrdNeckDiameterIn));
                    ApplyGrdPerformance(node, selection);
                }
                else if (string.Equals(node.GrdType, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase))
                {
                    var selection = GrdPerformanceCatalog.SelectTitusTbd80(grdCfm,
                        Convert.ToInt32(node.GrdLengthFt), node.GrdSlotWidthIn, node.GrdSlotCount);
                    ApplyGrdPerformance(node, selection);
                }
                else
                    node.PressureDropPa = _ductSystem.AutomaticSizing.Grd.PressureDropPa;
                break;
        }
        node.IsDefined = true;
        SyncLinkedItemMetadata(node);

        static void ApplyGrdPerformance(SavedLayoutNode target, GrdPerformanceSelection selection)
        {
            target.PressureDropPa = UnitConversions.InchesWaterToPa(selection.Selected.TotalPressureInWg);
            target.GrdInletDiameterIn = selection.InletDiameterIn ?? target.GrdInletDiameterIn;
            target.GrdSelectedAirflowCfm = selection.Selected.AirflowCfm;
            target.GrdSoundNc = selection.Selected.SoundNc;
            target.GrdThrowFt = selection.Selected.ThrowFt;
            if (!string.IsNullOrWhiteSpace(selection.NominalDuctSizeIn))
                target.GrdNominalDuctSizeIn = selection.NominalDuctSizeIn;
        }
    }

    private void AutoSizeFittingNode(SavedLayoutNode node)
    {
        var group = string.Equals(node.FittingGroup, "Terminals", StringComparison.OrdinalIgnoreCase)
            ? FittingCatalog.TerminalSelectionGroup
            : node.FittingGroup;
        var defaults = _ductSystem.AutomaticSizing.Fitting(group);
        var definition = FittingCatalog.All.FirstOrDefault(x =>
                             string.Equals(x.Code, defaults.FittingCode, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(FittingCatalog.SelectionGroup(x), group, StringComparison.OrdinalIgnoreCase))
                         ?? FittingCatalog.All.First(x =>
                             string.Equals(FittingCatalog.SelectionGroup(x), group, StringComparison.OrdinalIgnoreCase));
        var state = AutomaticFlowState(node);
        if (state.AirflowM3PerS <= 0)
            throw new InvalidOperationException("No connected defined item provides a positive airflow.");
        var shape = definition.ReferenceDuctShape ?? defaults.Shape;
        var geometry = shape == DuctShape.Round
            ? GeometryCalculator.Round(defaults.DiameterM)
            : GeometryCalculator.Rectangular(defaults.WidthM, defaults.HeightM);
        var inputs = definition.Inputs.ToDictionary(x => x.Key,
            x => defaults.Inputs.GetValueOrDefault(x.Key, x.DefaultValue), StringComparer.OrdinalIgnoreCase);
        if (string.Equals(group, "Elbows", StringComparison.OrdinalIgnoreCase)
            && inputs.ContainsKey("angle"))
            inputs["angle"] = node.ElbowAngleDegrees;
        FittingCalculationResult result;
        if (definition.IsJunction)
        {
            var branchFraction = Math.Clamp(defaults.BranchFraction, 0.01, 0.99);
            var common = state.AirflowM3PerS;
            double straight;
            double branch;
            if (string.Equals(group, "Converging Fittings", StringComparison.OrdinalIgnoreCase))
            {
                var incoming = ParentNodes(node).Select(x => Math.Max(0.0, x.AirflowM3PerS))
                    .Where(x => x > 0).Take(2).ToArray();
                straight = incoming.Length > 0 ? incoming[0] : common * (1.0 - branchFraction);
                branch = incoming.Length > 1 ? incoming[1] : common * branchFraction;
                common = straight + branch;
            }
            else
            {
                straight = common * (1.0 - branchFraction);
                branch = common * branchFraction;
            }
            var junction = new JunctionFlowData(defaults.JunctionPath, common, geometry,
                straight, geometry, branch, geometry);
            result = FittingCalculator.Calculate(definition, inputs, junction, state.TemperatureC,
                FixedRelativeHumidity, FixedAtmosphericPressurePa);
        }
        else
            result = FittingCalculator.Calculate(definition, inputs, state.AirflowM3PerS, geometry,
                state.TemperatureC, FixedRelativeHumidity, FixedAtmosphericPressurePa);

        var fitting = SavedFitting.FromResult(_ductSystem.ReserveFittingSequence(),
            $"{definition.Code} — {definition.Description}", result, node.AirSystemType, node.BranchId);
        _ductSystem.Fittings.Add(fitting);
        node.LinkedFittingSequence = fitting.Sequence;
        node.FittingGroup = group;
        node.AirflowM3PerS = result.AirflowM3PerS ?? state.AirflowM3PerS;
        node.TemperatureC = state.TemperatureC;
        node.PressureDropPa = result.PressureLossPa;
        node.AirflowUserSpecified = false;
        node.AirflowOverride = false;
        node.IsDefined = true;
        SyncLinkedItemMetadata(node);
    }

    private void AutoSizeConnectionDuct(LayoutConnectionRef connection)
    {
        var downstream = connection.Downstream;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        var upstream = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                           string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException("The upstream component was not found.");
        var state = AutomaticConnectionFlowState(upstream, downstream);
        if (state.AirflowM3PerS <= 0)
            throw new InvalidOperationException("No connected defined item provides a positive airflow.");
        var connectionDelta = ConnectionDelta(downstream, connection.Secondary);
        var length = Math.Sqrt(connectionDelta.XM * connectionDelta.XM
                               + connectionDelta.YM * connectionDelta.YM
                               + connectionDelta.ZM * connectionDelta.ZM);
        if (!double.IsFinite(length) || length <= 0)
            throw new InvalidOperationException("The graph connection has no measurable length.");

        var defaults = _ductSystem.AutomaticSizing.Duct;
        var material = DuctMaterialCatalog.All.FirstOrDefault(x =>
                           string.Equals(x.Name, defaults.MaterialName, StringComparison.OrdinalIgnoreCase))
                       ?? DuctMaterialCatalog.All.First(x => x.Name == "Galvanized Spiral Duct");
        var shape = RequiredShapeForMaterial(material) ?? defaults.Shape;
        DuctCalculationResult calculation;
        if (defaults.Mode == CalculationMode.CheckEnteredSize)
        {
            var geometry = shape == DuctShape.Round
                ? GeometryCalculator.Round(defaults.DiameterM)
                : GeometryCalculator.Rectangular(defaults.WidthM, defaults.HeightM);
            calculation = DuctCalculator.Calculate(new DuctCalculationInput(state.AirflowM3PerS,
                state.TemperatureC, FixedRelativeHumidity, FixedAtmosphericPressurePa, geometry, material,
                _ductSystem.DefaultEquivalentDiameterMethod, _ductSystem.DefaultPressureLossBasis, length));
        }
        else
        {
            var sizing = DuctSizingSolver.Size(new DuctSizingInput(state.AirflowM3PerS, state.TemperatureC,
                FixedRelativeHumidity, FixedAtmosphericPressurePa, shape, 1.0, material,
                _ductSystem.DefaultEquivalentDiameterMethod, _ductSystem.DefaultPressureLossBasis, length,
                defaults.Mode, defaults.TargetFrictionPaPerM, defaults.MaximumVelocityMPerS,
                _ductSystem.DefaultSizingIncrementM, _ductSystem.DefaultSizeRoundingDirection,
                shape == DuctShape.Rectangular ? RectangularFixedDimension.Height : RectangularFixedDimension.None,
                shape == DuctShape.Rectangular ? defaults.HeightM : 0.0));
            calculation = sizing.Calculation;
        }

        var existingDuct = DuctForConnection(downstream, connection.Secondary);
        var duct = SavedDuct.FromResult(existingDuct?.Sequence ?? _ductSystem.ReserveSequence(),
            $"Designer duct {upstream.Id} to {downstream.Id}", calculation,
            downstream.AirSystemType, downstream.BranchId);
        duct.DeltaXM = connectionDelta.XM;
        duct.DeltaYM = connectionDelta.YM;
        duct.DeltaZM = connectionDelta.ZM;
        duct.RequiresResizing = false;
        if (existingDuct is null)
        {
            _ductSystem.Ducts.Add(duct);
            if (connection.Secondary)
                downstream.SecondaryConnectionDuctSequence = duct.Sequence;
            else
                downstream.ConnectionDuctSequence = duct.Sequence;
        }
        else
        {
            CopyDuctValues(duct, existingDuct);
            existingDuct.Description = duct.Description;
            existingDuct.AirSystemType = duct.AirSystemType;
            existingDuct.BranchId = duct.BranchId;
            existingDuct.DeltaXM = duct.DeltaXM;
            existingDuct.DeltaYM = duct.DeltaYM;
            existingDuct.DeltaZM = duct.DeltaZM;
            existingDuct.RequiresResizing = false;
        }
        var preserveSinkBoundary = downstream.Type == LayoutNodeType.Reservoir
                                   && !IsSpaceSourceRole(downstream.SpaceRole)
                                   && downstream.AirflowUserSpecified;
        SetConnectionAirflowUserSpecified(downstream, connection.Secondary, preserveSinkBoundary);
        SetConnectionAirflowOverride(downstream, connection.Secondary, false);
    }

    private (double AirflowM3PerS, double TemperatureC) AutomaticFlowState(SavedLayoutNode node)
    {
        if (node.AirflowM3PerS > 0)
            return (node.AirflowM3PerS, node.TemperatureC);
        var connected = ParentNodes(node)
            .Concat(_ductSystem.LayoutNodes.Where(x =>
                string.Equals(x.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(x => x.Id)
            .OrderByDescending(x => x.IsDefined)
            .ThenBy(x => x.Sequence);
        var source = connected.FirstOrDefault(x => x.IsDefined && x.AirflowM3PerS > 0)
                     ?? connected.FirstOrDefault(x => x.AirflowM3PerS > 0);
        return source is null ? (0.0, node.TemperatureC) : (source.AirflowM3PerS, source.TemperatureC);
    }

    private (double AirflowM3PerS, double TemperatureC) AutomaticConnectionFlowState(
        SavedLayoutNode upstream, SavedLayoutNode downstream)
    {
        if (upstream.IsDefined && upstream.AirflowM3PerS > 0)
            return (upstream.AirflowM3PerS, upstream.TemperatureC);
        if (downstream.IsDefined && downstream.AirflowM3PerS > 0)
            return (downstream.AirflowM3PerS, downstream.TemperatureC);
        if (upstream.AirflowM3PerS > 0)
            return (upstream.AirflowM3PerS, upstream.TemperatureC);
        return AutomaticFlowState(downstream);
    }

    private IEnumerable<SavedLayoutNode> ParentNodes(SavedLayoutNode node)
    {
        foreach (var parentId in new[] { node.ParentNodeId, node.SecondaryParentNodeId })
        {
            if (string.IsNullOrWhiteSpace(parentId))
                continue;
            var parent = _ductSystem.LayoutNodes.FirstOrDefault(x =>
                string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
            if (parent is not null)
                yield return parent;
        }
    }

    private SavedLayoutNode? PendingLayoutNode(LayoutNodeType type) =>
        _pendingLayoutNodeId is null
            ? null
            : _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == _pendingLayoutNodeId && x.Type == type);

    private SavedLayoutNode? PendingConnectionNode() =>
        _pendingConnectionNodeId is null
            ? null
            : _ductSystem.LayoutNodes.FirstOrDefault(x => x.Id == _pendingConnectionNodeId
                                                          && !string.IsNullOrWhiteSpace(_pendingConnectionSecondary
                                                              ? x.SecondaryParentNodeId : x.ParentNodeId));

    private void ApplyLayoutMetadata(SavedDuct duct)
    {
        var connectionNode = PendingConnectionNode();
        var node = connectionNode ?? PendingLayoutNode(LayoutNodeType.Duct);
        duct.AirSystemType = node?.AirSystemType ?? _ductSystem.DefaultAirSystemType;
        duct.BranchId = node?.BranchId ?? _ductSystem.DefaultBranchId;
        if (node is not null)
        {
            var delta = ConnectionDelta(node, connectionNode is not null && _pendingConnectionSecondary);
            duct.DeltaXM = delta.XM;
            duct.DeltaYM = delta.YM;
            duct.DeltaZM = delta.ZM;
            if (connectionNode is not null)
            {
                if (_pendingConnectionSecondary)
                    node.SecondaryConnectionDuctSequence = duct.Sequence;
                else
                    node.ConnectionDuctSequence = duct.Sequence;
            }
            else
                node.LinkedDuctSequence = duct.Sequence;
            node.AirflowM3PerS = duct.AirflowM3PerS;
            node.TemperatureC = duct.TemperatureC;
            _pendingLayoutNodeId = null;
            _pendingConnectionNodeId = null;
            _pendingConnectionSecondary = false;
        }
        RefreshLayoutDesigner();
    }

    private void ApplyLayoutMetadata(SavedFitting fitting, FittingCalculationResult result,
        bool allowBranchReservation = true)
    {
        var node = PendingLayoutNode(LayoutNodeType.Fitting);
        var airSystem = node?.AirSystemType ?? _ductSystem.DefaultAirSystemType;
        var branch = node?.BranchId ?? _ductSystem.DefaultBranchId;
        if (allowBranchReservation && node is not null && result.Definition.IsJunction
            && result.Junction?.Path == JunctionPath.Branch)
        {
            branch = _ductSystem.ReserveBranchId();
            node.BranchId = branch;
        }
        fitting.AirSystemType = airSystem;
        fitting.BranchId = branch;
        if (node is not null)
        {
            fitting.DeltaXM = node.DeltaXM;
            fitting.DeltaYM = node.DeltaYM;
            fitting.DeltaZM = node.DeltaZM;
            node.LinkedFittingSequence = fitting.Sequence;
            var previousNodeId = node.Id;
            node.FittingGroup = FittingCatalog.SelectionGroup(result.Definition);
            UpdateLayoutNodeReferences(previousNodeId, node.Id);
            node.AirflowM3PerS = fitting.AirflowM3PerS;
            node.TemperatureC = fitting.TemperatureC;
            _pendingLayoutNodeId = null;
        }
        RefreshLayoutDesigner();
    }

    private void UpdateLayoutNodeReferences(string previousId, string currentId)
    {
        if (string.Equals(previousId, currentId, StringComparison.OrdinalIgnoreCase))
            return;
        foreach (var layoutNode in _ductSystem.LayoutNodes)
        {
            if (string.Equals(layoutNode.ParentNodeId, previousId, StringComparison.OrdinalIgnoreCase))
                layoutNode.ParentNodeId = currentId;
            if (string.Equals(layoutNode.SecondaryParentNodeId, previousId, StringComparison.OrdinalIgnoreCase))
                layoutNode.SecondaryParentNodeId = currentId;
            if (string.Equals(layoutNode.CalculationConnectionNodeId, previousId, StringComparison.OrdinalIgnoreCase))
                layoutNode.CalculationConnectionNodeId = currentId;
        }
        if (string.Equals(_pendingLayoutNodeId, previousId, StringComparison.OrdinalIgnoreCase))
            _pendingLayoutNodeId = currentId;
        if (string.Equals(_pendingConnectionNodeId, previousId, StringComparison.OrdinalIgnoreCase))
            _pendingConnectionNodeId = currentId;
        if (string.Equals(_ductSystem.CoordinateOriginNodeId, previousId, StringComparison.OrdinalIgnoreCase))
            _ductSystem.CoordinateOriginNodeId = currentId;
    }

    private void SyncLinkedItemMetadata(SavedLayoutNode node)
    {
        void ApplyToDuct(SavedDuct duct, bool secondary = false)
        {
            duct.AirSystemType = node.AirSystemType;
            duct.BranchId = node.BranchId;
            if (node.AirflowM3PerS > 0)
                duct.AirflowM3PerS = node.AirflowM3PerS;
            var delta = ConnectionDelta(node, secondary);
            duct.DeltaXM = delta.XM;
            duct.DeltaYM = delta.YM;
            duct.DeltaZM = delta.ZM;
            var length = Math.Sqrt(delta.XM * delta.XM + delta.YM * delta.YM + delta.ZM * delta.ZM);
            if (length > 0)
                duct.LengthM = length;
        }

        if (node.LinkedDuctSequence is int ductSequence)
        {
            var duct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == ductSequence);
            if (duct is not null)
                ApplyToDuct(duct);
        }
        if (node.ConnectionDuctSequence is int connectionDuctSequence)
        {
            var connectionDuct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == connectionDuctSequence);
            if (connectionDuct is not null)
                ApplyToDuct(connectionDuct);
        }
        if (node.SecondaryConnectionDuctSequence is int secondaryDuctSequence)
        {
            var secondaryDuct = _ductSystem.Ducts.FirstOrDefault(x => x.Sequence == secondaryDuctSequence);
            if (secondaryDuct is not null)
                ApplyToDuct(secondaryDuct, secondary: true);
        }
        if (node.LinkedFittingSequence is int fittingSequence)
        {
            var fitting = _ductSystem.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence);
            if (fitting is not null)
            {
                fitting.AirSystemType = node.AirSystemType;
                fitting.BranchId = node.BranchId;
                if (node.AirflowM3PerS > 0)
                {
                    var oldAirflow = fitting.AirflowM3PerS;
                    fitting.AirflowM3PerS = node.AirflowM3PerS;
                    if (fitting.Junction is not null && oldAirflow > 0)
                    {
                        var scale = fitting.AirflowM3PerS / oldAirflow;
                        fitting.Junction = fitting.Junction with
                        {
                            CommonAirflowM3PerS = fitting.AirflowM3PerS,
                            StraightAirflowM3PerS = fitting.Junction.StraightAirflowM3PerS * scale,
                            BranchAirflowM3PerS = fitting.Junction.BranchAirflowM3PerS * scale
                        };
                    }
                }
                fitting.TemperatureC = node.TemperatureC;
                fitting.DeltaXM = node.DeltaXM;
                fitting.DeltaYM = node.DeltaYM;
                fitting.DeltaZM = node.DeltaZM;
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (_mainTabs.SelectedTab == _layoutTab && !LayoutTextEditorHasFocus())
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                UndoLayoutChange();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Y))
            {
                RedoLayoutChange();
                return true;
            }
            if (keyData == Keys.Escape && CancelSelectedLayoutTool())
                return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private bool CancelSelectedLayoutTool()
    {
        if (_selectedLayoutTool is null)
            return false;
        _selectedLayoutTool = null;
        _selectedLayoutFittingGroup = null;
        _layoutCanvas?.CancelToolPlacement();
        _layoutStatusLabel.Text = "Component placement canceled.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(72, 92, 110);
        return true;
    }

    private bool LayoutTextEditorHasFocus()
    {
        Control? active = ActiveControl;
        while (active is ContainerControl container && container.ActiveControl is not null)
            active = container.ActiveControl;
        return active is TextBoxBase or ComboBox or NumericUpDown;
    }
}
