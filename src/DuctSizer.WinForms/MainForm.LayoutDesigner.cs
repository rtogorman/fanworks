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
    private readonly Stack<string> _layoutUndoHistory = new();
    private readonly Stack<string> _layoutRedoHistory = new();
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
        // The rendered construction sheet can be several megabytes. Keep it persistent in
        // the project file, but do not duplicate it into every in-memory move/connection undo entry.
        var pdfBackground = _ductSystem.LayoutPdfBackground;
        _ductSystem.LayoutPdfBackground = null;
        try
        {
            return JsonSerializer.Serialize(_ductSystem, LayoutHistoryJsonOptions);
        }
        finally
        {
            _ductSystem.LayoutPdfBackground = pdfBackground;
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
                    EditableAxesForNode(node))
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
                linkedFitting, absoluteCoordinate, EditableAxesForNode(node));
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

internal readonly record struct LayoutConnectionRef(SavedLayoutNode Downstream, bool Secondary);

internal enum LayoutDuctInsertionChoice
{
    Insert,
    PlaceUnconnected,
    Cancel
}
internal readonly record struct LayoutPortRef(SavedLayoutNode Node, bool IsOutput, int Index, Point? Anchor = null);
internal readonly record struct LayoutPortConnectionRequest(SavedLayoutNode Upstream, SavedLayoutNode Downstream,
    int OutputIndex, int InputIndex);
internal readonly record struct LayoutEditableAxes(bool X, bool Y, bool Z);

internal sealed class LayoutDesignerCanvas : Panel
{
    private const int BaseGridSize = 56;
    private SavedLayoutNode? _dragNode;
    private Point _mouseDown;
    private LayoutPortRef? _connectionStart;
    private Point _connectionMouse;
    private string? _selectedNodeId;
    private string? _selectedConnectionKey;
    private LayoutConnectionRef? _selectedConnection;
    private bool _panning;
    private Point _panStart;
    private PointF _panOffsetStart;
    private PointF _viewOffset;
    private Point? _snapPreviewGrid;
    private readonly List<Rectangle> _paintLabelBounds = new();
    private IReadOnlyDictionary<int, LayoutCoordinate> _resolvedCoordinates =
        new Dictionary<int, LayoutCoordinate>();
    private Image? _pdfBackgroundImage;
    private string _pdfBackgroundImageData = string.Empty;

    internal Func<IReadOnlyList<SavedLayoutNode>> NodeSource { get; init; } = () => Array.Empty<SavedLayoutNode>();
    internal Func<IReadOnlyDictionary<int, LayoutCoordinate>> CoordinateSource { get; init; } =
        () => new Dictionary<int, LayoutCoordinate>();
    internal Func<SavedLayoutNode, string> CaptionSource { get; init; } = node => node.Id;
    internal Func<LayoutConnectionRef, string> ConnectionCaptionSource { get; init; } = _ => string.Empty;
    internal Func<LayoutConnectionRef, string> ConnectionSizeCaptionSource { get; init; } = _ => string.Empty;
    internal Func<LayoutConnectionRef, bool> ConnectionSizedSource { get; init; } = _ => false;
    internal Func<LayoutConnectionRef, bool> ConnectionFlowReversedSource { get; init; } = _ => false;
    internal Func<SavedLayoutNode, bool> CriticalPathNodeSource { get; init; } = _ => false;
    internal Func<LayoutConnectionRef, bool> CriticalPathConnectionSource { get; init; } = _ => false;
    internal Func<LayoutPdfBackground?> PdfBackgroundSource { get; init; } = () => null;
    internal LayoutNodeType? SelectedTool { get; set; }
    internal bool RearrangeMode { get; set; }
    internal bool ShowToScale { get; set; }
    internal double FeetPerGridCell { get; set; } = 1.0;
    internal LayoutViewMode ViewMode { get; set; } = LayoutViewMode.Isometric;
    internal bool HighlightCriticalPath { get; set; }
    internal bool AvoidLabelOverlap { get; set; }
    internal bool ShowLabels { get; set; } = true;
    internal bool ShowDuctSizes { get; set; }
    internal float ZoomFactor { get; private set; } = 1.0F;
    internal PointF ViewOffset => _viewOffset;
    private int GridSize => Math.Max(6, (int)Math.Round(BaseGridSize * ZoomFactor));
    internal double PixelsPerDrawingFoot => GridSize / (2D * Math.Max(0.01, FeetPerGridCell));
    private float OriginX => Width / 2F + _viewOffset.X;
    private float OriginY => Height / 2F + _viewOffset.Y;
    internal event Action<LayoutNodeType, string?, int, int>? ToolDropped;
    internal event Action<SavedLayoutNode, int, int>? NodeMoved;
    internal event Action<SavedLayoutNode>? NodeActivated;
    internal event Action<LayoutConnectionRef>? ConnectionActivated;
    internal event Action<LayoutConnectionRef>? ConnectionDeleteRequested;
    internal event Action<SavedLayoutNode, SavedLayoutNode, int, int>? PortConnectionRequested;
    internal event Action<SavedLayoutNode>? NodeDeleteRequested;
    internal event Action<SavedLayoutNode?>? SelectionChanged;
    internal event Action<LayoutConnectionRef?>? ConnectionSelectionChanged;
    internal event Action<Point>? EmptyGridClicked;
    internal event Action<float>? ZoomChanged;

    internal LayoutDesignerCanvas()
    {
        DoubleBuffered = true;
        AllowDrop = true;
        TabStop = true;
        DragEnter += (sender, args) =>
        {
            if (TryReadTool(args.Data, out _, out _))
                args.Effect = DragDropEffects.Copy;
        };
        DragOver += (sender, args) =>
        {
            if (!TryReadTool(args.Data, out _, out _))
                return;
            args.Effect = DragDropEffects.Copy;
            SetSnapPreview(SnapPlacement(PointToClient(new Point(args.X, args.Y))));
        };
        DragLeave += (_, _) => SetSnapPreview(null);
        DragDrop += (_, args) =>
        {
            if (!TryReadTool(args.Data, out var type, out var fittingGroup))
                return;
            var point = PointToClient(new Point(args.X, args.Y));
            var grid = SnapPlacement(point);
            SetSnapPreview(null);
            ToolDropped?.Invoke(type, fittingGroup, grid.X, grid.Y);
        };
    }

    internal void ZoomBy(float change)
    {
        ZoomAt(new Point(Width / 2, Height / 2), change);
    }

    private void ZoomAt(Point anchor, float change)
    {
        var oldGridSize = GridSize;
        var next = Math.Clamp(ZoomFactor + change, 0.10F, 3.0F);
        if (Math.Abs(next - ZoomFactor) < 0.0001F)
            return;
        ZoomFactor = next;
        var ratio = GridSize / (float)oldGridSize;
        _viewOffset = new PointF(
            anchor.X - Width / 2F - (anchor.X - Width / 2F - _viewOffset.X) * ratio,
            anchor.Y - Height / 2F - (anchor.Y - Height / 2F - _viewOffset.Y) * ratio);
        ZoomChanged?.Invoke(ZoomFactor);
        Invalidate();
    }

    internal void CenterOnGrid(int gridX, int gridY)
    {
        if (ViewMode == LayoutViewMode.TopDown)
        {
            var x = (gridX + gridY) / 2.0;
            var y = (gridY - gridX) / 2.0;
            _viewOffset = new PointF(-(float)(x * GridSize / 2.0), -(float)(y * GridSize / 2.0));
        }
        else
        {
            _viewOffset = new PointF(
                -(float)(gridX * GridSize / 2.0),
                -(float)(gridY * GridSize / 4.0));
        }
        Invalidate();
    }

    internal void FitContentToViewport(int margin = 78)
    {
        var nodes = NodeSource();
        if (nodes.Count == 0 || Width < 100 || Height < 100)
            return;
        _resolvedCoordinates = CoordinateSource();
        ZoomFactor = 1.0F;
        _viewOffset = PointF.Empty;
        var initialBounds = ContentBounds(nodes);
        var usableWidth = Math.Max(100, Width - margin * 2);
        var usableHeight = Math.Max(100, Height - margin * 2);
        var factor = Math.Min(usableWidth / (float)Math.Max(1, initialBounds.Width),
            usableHeight / (float)Math.Max(1, initialBounds.Height));
        ZoomFactor = Math.Clamp(factor, 0.10F, 2.15F);
        _viewOffset = PointF.Empty;
        var fittedBounds = ContentBounds(nodes);
        _viewOffset = new PointF(Width / 2F - (fittedBounds.Left + fittedBounds.Width / 2F),
            Height / 2F - (fittedBounds.Top + fittedBounds.Height / 2F));
        Invalidate();
    }

    private Rectangle ContentBounds(IReadOnlyList<SavedLayoutNode> nodes)
    {
        var bounds = NodeBounds(nodes[0]);
        foreach (var node in nodes.Skip(1))
            bounds = Rectangle.Union(bounds, NodeBounds(node));
        if (ViewMode == LayoutViewMode.TopDown
            && PdfBackgroundSource() is { Visible: true } background
            && !string.IsNullOrWhiteSpace(background.PagePngBase64)
            && background.PaperInches > 0 && background.DrawingFeet > 0)
            bounds = Rectangle.Union(bounds, PdfBackgroundBounds(background));
        // Captions are placed beside components and duct IDs sit just off their line.
        bounds.Inflate(Math.Max(70, (int)Math.Round(84 * ZoomFactor)),
            Math.Max(52, (int)Math.Round(62 * ZoomFactor)));
        return bounds;
    }

    internal void KeepNodeAtScreenPosition(SavedLayoutNode node, Point desiredPosition)
    {
        var currentPosition = VisualLocation(node);
        _viewOffset = new PointF(_viewOffset.X + desiredPosition.X - currentPosition.X,
            _viewOffset.Y + desiredPosition.Y - currentPosition.Y);
        Invalidate();
    }

    internal void SelectNode(SavedLayoutNode? node) => SetSelectedNode(node);

    internal void CancelToolPlacement()
    {
        SelectedTool = null;
        SetSnapPreview(null);
        Cursor = Cursors.Hand;
        Invalidate();
    }

    internal SavedLayoutNode? SelectedNode =>
        NodeSource().FirstOrDefault(x => string.Equals(x.Id, _selectedNodeId, StringComparison.OrdinalIgnoreCase));

    internal void SelectConnection(LayoutConnectionRef? connection) => SetSelectedConnection(connection);

    internal void DeleteSelectedNode()
    {
        var node = NodeSource().FirstOrDefault(x => x.Id == _selectedNodeId);
        if (node is null)
            return;
        NodeDeleteRequested?.Invoke(node);
        SetSelectedNode(null);
    }

    internal void DeleteSelectedItem()
    {
        var node = NodeSource().FirstOrDefault(x => x.Id == _selectedNodeId);
        if (node is not null)
        {
            NodeDeleteRequested?.Invoke(node);
            SetSelectedNode(null);
            return;
        }
        if (_selectedConnection is LayoutConnectionRef connection)
        {
            ConnectionDeleteRequested?.Invoke(connection);
            SetSelectedConnection(null);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        _resolvedCoordinates = CoordinateSource();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawPdfBackground(e.Graphics);
        DrawDesignerGrid(e.Graphics);
        DrawSnapPreview(e.Graphics);

        var nodes = NodeSource();
        _paintLabelBounds.Clear();
        if (AvoidLabelOverlap)
            _paintLabelBounds.AddRange(nodes.Select(node => Rectangle.Inflate(NodeBounds(node), 4, 4)));
        foreach (var connection in Connections(nodes))
            DrawConnection(e.Graphics, connection, nodes);
        foreach (var node in nodes)
            DrawNode(e.Graphics, node, Connections(nodes).Any(x => ParentId(x) == node.Id || x.Downstream.Id == node.Id));
        if (_connectionStart is LayoutPortRef startPort)
        {
            using var preview = new Pen(Color.FromArgb(43, 150, 96), Math.Max(2F, 3F * ZoomFactor)) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawLine(preview, startPort.Anchor ?? PortPoint(startPort.Node, true, startPort.Index), _connectionMouse);
        }
        DrawAxisLegend(e.Graphics);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pdfBackgroundImage?.Dispose();
            _pdfBackgroundImage = null;
            _pdfBackgroundImageData = string.Empty;
        }
        base.Dispose(disposing);
    }

    private void DrawPdfBackground(Graphics graphics)
    {
        if (ViewMode != LayoutViewMode.TopDown || PdfBackgroundSource() is not { Visible: true } background
            || string.IsNullOrWhiteSpace(background.PagePngBase64)
            || background.PaperInches <= 0 || background.DrawingFeet <= 0
            || background.PageWidthPoints <= 0 || background.PageHeightPoints <= 0)
            return;
        if (!string.Equals(_pdfBackgroundImageData, background.PagePngBase64, StringComparison.Ordinal))
        {
            try
            {
                var bytes = Convert.FromBase64String(background.PagePngBase64);
                using var stream = new MemoryStream(bytes, writable: false);
                using var source = Image.FromStream(stream);
                var replacement = new Bitmap(source);
                _pdfBackgroundImage?.Dispose();
                _pdfBackgroundImage = replacement;
                _pdfBackgroundImageData = background.PagePngBase64;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OutOfMemoryException)
            {
                _pdfBackgroundImage?.Dispose();
                _pdfBackgroundImage = null;
                _pdfBackgroundImageData = string.Empty;
                return;
            }
        }
        if (_pdfBackgroundImage is null)
            return;

        var destination = PdfBackgroundBounds(background);
        var priorInterpolation = graphics.InterpolationMode;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = 0.82F };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(_pdfBackgroundImage, destination, 0, 0,
            _pdfBackgroundImage.Width, _pdfBackgroundImage.Height, GraphicsUnit.Pixel, attributes);
        graphics.InterpolationMode = priorInterpolation;
    }

    internal Rectangle PdfBackgroundBounds(LayoutPdfBackground background)
    {
        var feetPerCell = Math.Max(0.1, FeetPerGridCell);
        var drawingFeetPerPaperInch = background.DrawingFeet / Math.Max(0.001, background.PaperInches);
        var drawingWidthFeet = background.PageWidthPoints / 72.0 * drawingFeetPerPaperInch;
        var drawingHeightFeet = background.PageHeightPoints / 72.0 * drawingFeetPerPaperInch;
        var pixelsPerFoot = GridSize / (2.0 * feetPerCell);
        var width = Math.Max(1, (int)Math.Round(drawingWidthFeet * pixelsPerFoot));
        var height = Math.Max(1, (int)Math.Round(drawingHeightFeet * pixelsPerFoot));
        var center = CoordinatePoint(new LayoutCoordinate(
            UnitConversions.FeetToM(background.CenterXFeet),
            UnitConversions.FeetToM(background.CenterYFeet),
            UnitConversions.FeetToM(background.CenterZFeet)));
        return new Rectangle(center.X - width / 2, center.Y - height / 2, width, height);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.Location, e.Delta > 0 ? 0.10F : -0.10F);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Focus();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _mouseDown = e.Location;
        if (!RearrangeMode && e.Button == MouseButtons.Left
            && HitTestPort(e.Location) is LayoutPortRef port)
        {
            _connectionStart = port;
            _connectionMouse = e.Location;
            _dragNode = null;
            Invalidate();
            return;
        }
        _dragNode = HitTest(e.Location);
        if (e.Button == MouseButtons.Left && _dragNode is not null)
            SetSelectedNode(_dragNode);
        else if (e.Button == MouseButtons.Left && HitTestConnection(e.Location) is LayoutConnectionRef connection)
        {
            SetSelectedConnection(connection);
            return;
        }
        else if (e.Button == MouseButtons.Left)
            SetSelectedNode(null);
        if (e.Button == MouseButtons.Left && _dragNode is null && SelectedTool is null)
        {
            _panning = true;
            _panStart = PointToScreen(e.Location);
            _panOffsetStart = _viewOffset;
            Cursor = Cursors.SizeAll;
            Capture = true;
            return;
        }
        if (e.Button == MouseButtons.Right && _dragNode is not null)
        {
            NodeDeleteRequested?.Invoke(_dragNode);
            _dragNode = null;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panning)
        {
            _panning = false;
            Capture = false;
            Cursor = Cursors.Hand;
            return;
        }
        if (e.Button == MouseButtons.Left && _connectionStart is LayoutPortRef source)
        {
            var target = HitTestPort(e.Location);
            _connectionStart = null;
            Invalidate();
            if (target is LayoutPortRef destination
                && destination.Node.Id != source.Node.Id)
            {
                var request = CreateGestureConnectionRequest(source.Node, destination.Node);
                PortConnectionRequested?.Invoke(request.Upstream, request.Downstream,
                    request.OutputIndex, request.InputIndex);
            }
            return;
        }
        if (e.Button == MouseButtons.Left && _dragNode is not null
            && Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 6)
        {
            var grid = SnapPlacement(e.Location);
            NodeMoved?.Invoke(_dragNode, grid.X, grid.Y);
        }
        SetSnapPreview(null);
        _dragNode = null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panning)
        {
            var pointer = PointToScreen(e.Location);
            _viewOffset = new PointF(
                _panOffsetStart.X + pointer.X - _panStart.X,
                _panOffsetStart.Y + pointer.Y - _panStart.Y);
            Invalidate();
            Cursor = Cursors.SizeAll;
            return;
        }
        if (_connectionStart is not null)
        {
            _connectionMouse = e.Location;
            Cursor = Cursors.Cross;
            Invalidate();
            return;
        }
        if (_dragNode is not null && e.Button == MouseButtons.Left
            && Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 6)
            SetSnapPreview(SnapPlacement(e.Location));
        else if (SelectedTool is not null && _dragNode is null)
            SetSnapPreview(SnapPlacement(e.Location));
        else if (_dragNode is null)
            SetSnapPreview(null);
        Cursor = HitTest(e.Location) is not null && RearrangeMode
            ? Cursors.SizeAll
            : (!RearrangeMode && HitTestPort(e.Location) is not null) || HitTest(e.Location) is not null || HitTestConnection(e.Location) is not null
            ? Cursors.Hand
            : SelectedTool is not null ? Cursors.Cross : Cursors.Hand;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || HitTest(e.Location) is not null
            || HitTestConnection(e.Location) is not null)
            return;
        SetSelectedNode(null);
        if (SelectedTool is null)
            return;
        var grid = SnapPlacement(e.Location);
        SetSnapPreview(null);
        EmptyGridClicked?.Invoke(grid);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Delete)
            return;
        DeleteSelectedItem();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (RearrangeMode)
            return;
        var node = HitTest(e.Location);
        if (node is not null)
        {
            NodeActivated?.Invoke(node);
            return;
        }
        var connection = HitTestConnection(e.Location);
        if (connection is not null)
            ConnectionActivated?.Invoke(connection.Value);
    }

    private void DrawNode(Graphics graphics, SavedLayoutNode node, bool connected)
    {
        var bounds = NodeBounds(node);
        if (HighlightCriticalPath && CriticalPathNodeSource(node))
        {
            var criticalBounds = Rectangle.Inflate(bounds, Math.Max(7, (int)Math.Round(9 * ZoomFactor)),
                Math.Max(7, (int)Math.Round(9 * ZoomFactor)));
            using var criticalPen = new Pen(Color.FromArgb(230, 145, 31), Math.Max(3F, 4.5F * ZoomFactor));
            graphics.DrawRectangle(criticalPen, criticalBounds);
        }
        if (string.Equals(node.Id, _selectedNodeId, StringComparison.OrdinalIgnoreCase))
        {
            var selectionBounds = Rectangle.Inflate(bounds, Math.Max(6, (int)Math.Round(8 * ZoomFactor)),
                Math.Max(6, (int)Math.Round(8 * ZoomFactor)));
            using var selectionFill = new SolidBrush(Color.FromArgb(28, 45, 105, 170));
            using var selectionPen = new Pen(Color.FromArgb(45, 105, 170), Math.Max(1.4F, 1.8F * ZoomFactor));
            graphics.FillRectangle(selectionFill, selectionBounds);
            graphics.DrawRectangle(selectionPen, selectionBounds);
        }
        var iconColor = node.AirflowOverride
            ? Color.FromArgb(222, 126, 34)
            : node.IsDefined ? Color.FromArgb(21, 83, 99) : Color.FromArgb(196, 48, 48);
        var iconSize = Math.Max(22, (int)Math.Round(34 * ZoomFactor));
        var iconBounds = node.Type == LayoutNodeType.Duct
            ? new Rectangle(bounds.Left + 3, bounds.Top + 3, bounds.Width - 6, Math.Max(14, bounds.Height - 6))
            : new Rectangle(bounds.Left + bounds.Width / 2 - iconSize / 2,
                bounds.Top + bounds.Height / 2 - iconSize / 2, iconSize, iconSize);
        LayoutIconPainter.Draw(graphics, node.Type, iconBounds, iconColor,
            node.Type == LayoutNodeType.Grd ? node.GrdType
            : node.Type == LayoutNodeType.CalculationPoint ? node.CalculationPointType
            : node.Type == LayoutNodeType.Reservoir ? node.SpaceRole
            : node.FittingGroup);
        if (!ShowLabels)
        {
            DrawPorts(graphics, node);
            return;
        }
        var id = CaptionSource(node).Replace("\r", string.Empty).Split('\n').FirstOrDefault() ?? node.Id;
        using var idFont = new Font("Segoe UI Semibold", Math.Max(5.5F, 7F * ZoomFactor));
        var measured = TextRenderer.MeasureText(id, idFont, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var textBounds = ReserveLabelBounds(new Rectangle(bounds.Right + Math.Max(5, (int)Math.Round(7 * ZoomFactor)),
            bounds.Top - Math.Max(2, (int)Math.Round(3 * ZoomFactor)), measured.Width + 8, measured.Height + 3));
        using var labelBackground = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
        graphics.FillRectangle(labelBackground, textBounds);
        TextRenderer.DrawText(graphics, id, idFont, textBounds, Color.FromArgb(20, 44, 65),
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        DrawPorts(graphics, node);
    }

    private SavedLayoutNode? HitTest(Point point) =>
        NodeSource().Reverse().FirstOrDefault(node => NodeBounds(node).Contains(point));

    private void SetSelectedNode(SavedLayoutNode? node)
    {
        var id = node?.Id;
        if (string.Equals(_selectedNodeId, id, StringComparison.OrdinalIgnoreCase)
            && _selectedConnectionKey is null)
            return;
        _selectedNodeId = id;
        _selectedConnectionKey = null;
        _selectedConnection = null;
        SelectionChanged?.Invoke(node);
        ConnectionSelectionChanged?.Invoke(null);
        Invalidate();
    }

    private void SetSelectedConnection(LayoutConnectionRef? connection)
    {
        var key = connection is LayoutConnectionRef value ? ConnectionKey(value) : null;
        if (string.Equals(_selectedConnectionKey, key, StringComparison.OrdinalIgnoreCase)
            && _selectedNodeId is null)
            return;
        _selectedNodeId = null;
        _selectedConnectionKey = key;
        _selectedConnection = connection;
        SelectionChanged?.Invoke(null);
        ConnectionSelectionChanged?.Invoke(connection);
        Invalidate();
    }

    private static string ConnectionKey(LayoutConnectionRef connection) =>
        $"{connection.Downstream.Id}:{(connection.Secondary ? 1 : 0)}";

    private void DrawDesignerGrid(Graphics graphics)
    {
        using var minor = new Pen(Color.FromArgb(190, 222, 227), 0.75F);
        using var major = new Pen(Color.FromArgb(111, 183, 194), 1.10F);
        if (ViewMode == LayoutViewMode.TopDown)
        {
            var spacing = GridSize / 2F;
            var stride = GridLineStride(spacing);
            var firstX = (int)Math.Floor(-OriginX / spacing) - 1;
            var lastX = (int)Math.Ceiling((Width - OriginX) / spacing) + 1;
            var firstY = (int)Math.Floor(-OriginY / spacing) - 1;
            var lastY = (int)Math.Ceiling((Height - OriginY) / spacing) + 1;
            for (var index = firstX; index <= lastX; index++)
            {
                if (Math.Abs(index) % stride != 0)
                    continue;
                var x = OriginX + index * spacing;
                graphics.DrawLine(Math.Abs(index) % 5 == 0 ? major : minor, x, 0, x, Height);
            }
            for (var index = firstY; index <= lastY; index++)
            {
                if (Math.Abs(index) % stride != 0)
                    continue;
                var y = OriginY + index * spacing;
                graphics.DrawLine(Math.Abs(index) % 5 == 0 ? major : minor, 0, y, Width, y);
            }
        }
        else
        {
            // Each isometric line family needs its own viewport intersection range. A shared
            // range can drop one family after a deep pointer-centered zoom or a long pan.
            var stride = GridLineStride(GridSize / 2.2F);
            var risingFirst = (int)Math.Floor((-2F * Height - OriginX + 2F * OriginY) / GridSize) - 2;
            var risingLast = (int)Math.Ceiling((Width - OriginX + 2F * OriginY) / GridSize) + 2;
            for (var gridIndex = risingFirst; gridIndex <= risingLast; gridIndex++)
            {
                if (Math.Abs(gridIndex) % stride != 0)
                    continue;
                var offset = gridIndex * GridSize;
                var pen = Math.Abs(gridIndex) % 5 == 0 ? major : minor;
                var rising = OriginX + offset - 2 * OriginY;
                graphics.DrawLine(pen, rising, 0, rising + Height * 2, Height);
            }
            var fallingFirst = (int)Math.Floor((-OriginX - 2F * OriginY) / GridSize) - 2;
            var fallingLast = (int)Math.Ceiling((Width + 2F * Height - OriginX - 2F * OriginY) / GridSize) + 2;
            for (var gridIndex = fallingFirst; gridIndex <= fallingLast; gridIndex++)
            {
                if (Math.Abs(gridIndex) % stride != 0)
                    continue;
                var offset = gridIndex * GridSize;
                var pen = Math.Abs(gridIndex) % 5 == 0 ? major : minor;
                var falling = OriginX + offset + 2 * OriginY;
                graphics.DrawLine(pen, falling, 0, falling - Height * 2, Height);
            }
        }

    }

    private static int GridLineStride(float projectedSpacing) =>
        Math.Max(1, (int)Math.Ceiling(8F / Math.Max(1F, projectedSpacing)));

    private void DrawAxisLegend(Graphics graphics)
    {
        var legend = AxisLegendBounds;
        using var legendFill = new SolidBrush(Color.White);
        using var legendBorder = new Pen(Color.FromArgb(165, 184, 195), 1F);
        graphics.FillRectangle(legendFill, legend);
        graphics.DrawRectangle(legendBorder, legend);
        var axisOrigin = new Point(legend.Left + legend.Width / 2, legend.Top + legend.Height / 2 + 7);
        const int axisLength = 25;
        using var xPen = new Pen(Color.FromArgb(195, 65, 65), 2F) { EndCap = LineCap.ArrowAnchor };
        using var yPen = new Pen(Color.FromArgb(42, 135, 91), 2F) { EndCap = LineCap.ArrowAnchor };
        using var zPen = new Pen(Color.FromArgb(45, 105, 170), 2F) { EndCap = LineCap.ArrowAnchor };
        var xEnd = ViewMode == LayoutViewMode.TopDown
            ? new Point(axisOrigin.X + axisLength, axisOrigin.Y)
            : ViewMode == LayoutViewMode.IsometricNegative
                ? new Point(axisOrigin.X + axisLength, axisOrigin.Y - axisLength / 2)
                : new Point(axisOrigin.X + axisLength, axisOrigin.Y + axisLength / 2);
        var yEnd = ViewMode == LayoutViewMode.TopDown
            ? new Point(axisOrigin.X, axisOrigin.Y + axisLength)
            : ViewMode == LayoutViewMode.IsometricNegative
                ? new Point(axisOrigin.X + axisLength, axisOrigin.Y + axisLength / 2)
                : new Point(axisOrigin.X - axisLength, axisOrigin.Y + axisLength / 2);
        var zEnd = new Point(axisOrigin.X, axisOrigin.Y - axisLength);
        graphics.DrawLine(xPen, axisOrigin, xEnd);
        graphics.DrawLine(yPen, axisOrigin, yEnd);
        graphics.DrawLine(zPen, axisOrigin, zEnd);
        using var axisFont = new Font("Segoe UI Semibold", 8F);
        graphics.DrawString("X", axisFont, Brushes.Firebrick, xEnd.X + 2, xEnd.Y - 5);
        graphics.DrawString("Y", axisFont, Brushes.SeaGreen, yEnd.X - 5, yEnd.Y + 2);
        graphics.DrawString("Z", axisFont, Brushes.SteelBlue, axisOrigin.X + 3, axisOrigin.Y - axisLength - 12);
    }

    internal Rectangle AxisLegendBounds
    {
        get
        {
            const int side = 112;
            return new Rectangle(Math.Max(8, Width - side - 12), Math.Max(8, Height - side - 12), side, side);
        }
    }

    private void DrawSnapPreview(Graphics graphics)
    {
        if (_snapPreviewGrid is not Point grid)
            return;
        var center = GridPoint(grid);
        var haloRadius = Math.Max(7, (int)Math.Round(9 * ZoomFactor));
        var dotRadius = Math.Max(3, (int)Math.Round(4 * ZoomFactor));
        using var halo = new SolidBrush(Color.FromArgb(55, 31, 120, 180));
        using var dot = new SolidBrush(Color.FromArgb(31, 120, 180));
        using var outline = new Pen(Color.White, Math.Max(1F, 1.5F * ZoomFactor));
        graphics.FillEllipse(halo, center.X - haloRadius, center.Y - haloRadius, haloRadius * 2, haloRadius * 2);
        graphics.FillEllipse(dot, center.X - dotRadius, center.Y - dotRadius, dotRadius * 2, dotRadius * 2);
        graphics.DrawEllipse(outline, center.X - dotRadius, center.Y - dotRadius, dotRadius * 2, dotRadius * 2);
    }

    private void SetSnapPreview(Point? grid)
    {
        if (_snapPreviewGrid == grid)
            return;
        _snapPreviewGrid = grid;
        Invalidate();
    }

    private LayoutConnectionRef? HitTestConnection(Point point)
    {
        var nodes = NodeSource();
        foreach (var connection in Connections(nodes).Reverse())
        {
            var path = ConnectionPath(connection, nodes);
            for (var index = 1; index < path.Count; index++)
                if (DistanceToSegment(point, path[index - 1], path[index]) <= Math.Max(7, 9 * ZoomFactor))
                    return connection;
        }
        return null;
    }

    private static IEnumerable<LayoutConnectionRef> Connections(IReadOnlyList<SavedLayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId))
                yield return new LayoutConnectionRef(node, false);
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
                yield return new LayoutConnectionRef(node, true);
        }
    }

    private static string ParentId(LayoutConnectionRef connection) =>
        connection.Secondary ? connection.Downstream.SecondaryParentNodeId : connection.Downstream.ParentNodeId;

    private void DrawConnection(Graphics graphics, LayoutConnectionRef connectionRef, IReadOnlyList<SavedLayoutNode> nodes)
    {
        var parent = nodes.FirstOrDefault(x => x.Id == ParentId(connectionRef));
        if (parent is null)
            return;
        var path = ConnectionPath(connectionRef, nodes).ToArray();
        if (path.Length < 2)
            return;
        var isSized = ConnectionSizedSource(connectionRef);
        var connectionColor = (connectionRef.Secondary
                ? connectionRef.Downstream.SecondaryConnectionAirflowOverride
                : connectionRef.Downstream.ConnectionAirflowOverride)
            ? Color.FromArgb(222, 126, 34)
            : isSized ? Color.FromArgb(112, 123, 128) : Color.FromArgb(205, 61, 61);
        if (HighlightCriticalPath && CriticalPathConnectionSource(connectionRef))
        {
            using var critical = new Pen(Color.FromArgb(230, 145, 31), Math.Max(7F, 8F * ZoomFactor))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLines(critical, path);
        }
        using var connection = new Pen(connectionColor, Math.Max(1.6F, 2.4F * ZoomFactor))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            DashStyle = isSized ? DashStyle.Solid : DashStyle.Dash
        };
        if (string.Equals(_selectedConnectionKey, ConnectionKey(connectionRef), StringComparison.OrdinalIgnoreCase))
        {
            using var selected = new Pen(Color.FromArgb(45, 105, 170), Math.Max(6F, 7F * ZoomFactor))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLines(selected, path);
        }
        graphics.DrawLines(connection, path);
        DrawFlowArrow(graphics,
            ConnectionFlowReversedSource(connectionRef) ? path.Reverse().ToArray() : path,
            connectionColor);
        var ductId = ShowLabels
            ? ConnectionCaptionSource(connectionRef).Replace("\r", string.Empty).Split('\n').FirstOrDefault() ?? string.Empty
            : string.Empty;
        var ductSize = ShowDuctSizes ? ConnectionSizeCaptionSource(connectionRef) : string.Empty;
        var ductLabel = !string.IsNullOrWhiteSpace(ductId) && !string.IsNullOrWhiteSpace(ductSize)
            ? $"{ductId}  {ductSize}"
            : !string.IsNullOrWhiteSpace(ductId) ? ductId : ductSize;
        if (string.IsNullOrWhiteSpace(ductLabel))
            return;
        using var labelFont = new Font("Segoe UI Semibold", Math.Max(5.5F, 6.8F * ZoomFactor));
        var ductMeasured = TextRenderer.MeasureText(ductLabel, labelFont, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var labelWidth = ductMeasured.Width + 8;
        var labelHeight = ductMeasured.Height + 3;
        var center = AdjacentLabelPoint(path, labelHeight);
        var labelBounds = ReserveLabelBounds(new Rectangle(center.X - labelWidth / 2,
            center.Y - labelHeight / 2, labelWidth, labelHeight));
        using var labelBackground = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
        graphics.FillRectangle(labelBackground, labelBounds);
        TextRenderer.DrawText(graphics, ductLabel, labelFont,
            labelBounds, Color.FromArgb(20, 44, 65), TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
    }

    private Rectangle ReserveLabelBounds(Rectangle preferred)
    {
        if (!AvoidLabelOverlap)
            return preferred;
        var horizontal = Math.Max(10, preferred.Width / 2 + 6);
        var vertical = preferred.Height + 5;
        var offsets = new[]
        {
            Point.Empty,
            new Point(0, -vertical), new Point(0, vertical),
            new Point(horizontal, 0), new Point(-horizontal, 0),
            new Point(horizontal, -vertical), new Point(-horizontal, -vertical),
            new Point(horizontal, vertical), new Point(-horizontal, vertical),
            new Point(0, -vertical * 2), new Point(0, vertical * 2)
        };
        foreach (var offset in offsets)
        {
            if (TryReserve(offset, out var candidate))
                return candidate;
        }
        for (var radius = 2; radius <= 6; radius++)
        {
            for (var xStep = -radius; xStep <= radius; xStep++)
            for (var yStep = -radius; yStep <= radius; yStep++)
            {
                if (Math.Max(Math.Abs(xStep), Math.Abs(yStep)) != radius)
                    continue;
                if (TryReserve(new Point(xStep * horizontal, yStep * vertical), out var candidate))
                    return candidate;
            }
        }
        var fallback = preferred;
        fallback.X = Math.Clamp(fallback.X, 4, Math.Max(4, Width - fallback.Width - 4));
        fallback.Y = Math.Clamp(fallback.Y, 4, Math.Max(4, Height - fallback.Height - 4));
        _paintLabelBounds.Add(Rectangle.Inflate(fallback, 3, 2));
        return fallback;

        bool TryReserve(Point offset, out Rectangle candidate)
        {
            candidate = preferred;
            candidate.Offset(offset);
            candidate.X = Math.Clamp(candidate.X, 4, Math.Max(4, Width - candidate.Width - 4));
            candidate.Y = Math.Clamp(candidate.Y, 4, Math.Max(4, Height - candidate.Height - 4));
            var clearance = Rectangle.Inflate(candidate, 3, 2);
            if (_paintLabelBounds.Any(existing => existing.IntersectsWith(clearance)))
                return false;
            _paintLabelBounds.Add(clearance);
            return true;
        }
    }

    private Point AdjacentLabelPoint(IReadOnlyList<Point> path, int labelHeight)
    {
        var segment = Enumerable.Range(1, path.Count - 1)
            .Select(index => (Start: path[index - 1], End: path[index], Length: Distance(path[index - 1], path[index])))
            .OrderByDescending(item => item.Length)
            .First();
        if (segment.Length < 1)
            return PathMidpoint(path);
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var nx = -dy / segment.Length;
        var ny = dx / segment.Length;
        if (ny > 0 || Math.Abs(ny) < .05 && nx < 0)
        {
            nx *= -1;
            ny *= -1;
        }
        var midpoint = new Point((segment.Start.X + segment.End.X) / 2, (segment.Start.Y + segment.End.Y) / 2);
        var offset = Math.Max(10.0, labelHeight / 2.0 + 7 * ZoomFactor);
        return new Point((int)Math.Round(midpoint.X + nx * offset), (int)Math.Round(midpoint.Y + ny * offset));
    }

    private void DrawFlowArrow(Graphics graphics, IReadOnlyList<Point> path, Color color)
    {
        var longest = Enumerable.Range(1, path.Count - 1)
            .Select(index => new { Start = path[index - 1], End = path[index], Length = Distance(path[index - 1], path[index]) })
            .OrderByDescending(segment => segment.Length)
            .First();
        var start = longest.Start;
        var end = longest.End;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 24)
            return;
        var ux = dx / length;
        var uy = dy / length;
        var center = new PointF((float)(start.X + dx * .62), (float)(start.Y + dy * .62));
        var arrowLength = Math.Max(6F, 8F * ZoomFactor);
        var halfWidth = Math.Max(3F, 4F * ZoomFactor);
        var tip = new PointF(center.X + (float)ux * arrowLength, center.Y + (float)uy * arrowLength);
        var left = new PointF(center.X - (float)ux * arrowLength * .35F - (float)uy * halfWidth,
            center.Y - (float)uy * arrowLength * .35F + (float)ux * halfWidth);
        var right = new PointF(center.X - (float)ux * arrowLength * .35F + (float)uy * halfWidth,
            center.Y - (float)uy * arrowLength * .35F - (float)ux * halfWidth);
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, new[] { tip, left, right });
    }

    private Point ConnectionStart(LayoutConnectionRef connectionRef, SavedLayoutNode parent,
        IReadOnlyList<SavedLayoutNode> nodes)
    {
        return ConnectionPath(connectionRef, nodes).First();
    }

    private Point ConnectionEnd(LayoutConnectionRef connectionRef, IReadOnlyList<SavedLayoutNode> nodes) =>
        ConnectionPath(connectionRef, nodes).Last();

    private IReadOnlyList<Point> ConnectionPath(LayoutConnectionRef connectionRef, IReadOnlyList<SavedLayoutNode> nodes)
    {
        var parent = nodes.FirstOrDefault(x => string.Equals(x.Id, ParentId(connectionRef), StringComparison.OrdinalIgnoreCase));
        if (parent is null)
            return Array.Empty<Point>();
        var downstream = connectionRef.Downstream;
        var centerStart = Center(parent);
        var centerEnd = Center(downstream);
        if (UsesResolvedCoordinate(parent) && UsesResolvedCoordinate(downstream)
            && _resolvedCoordinates.TryGetValue(parent.Sequence, out var parentCoordinate)
            && _resolvedCoordinates.TryGetValue(downstream.Sequence, out var downstreamCoordinate))
        {
            var centersToScale = new List<Point>
            {
                centerStart,
                CoordinatePoint(new LayoutCoordinate(downstreamCoordinate.XM, parentCoordinate.YM, parentCoordinate.ZM)),
                CoordinatePoint(new LayoutCoordinate(downstreamCoordinate.XM, downstreamCoordinate.YM, parentCoordinate.ZM)),
                centerEnd
            };
            centersToScale = centersToScale.Where((point, index) => index == 0 || point != centersToScale[index - 1]).ToList();
            if (centersToScale.Count == 1)
                centersToScale.Add(centerEnd == centerStart ? new Point(centerEnd.X + 1, centerEnd.Y) : centerEnd);
            var scaledPath = new List<Point> { PortPointToward(parent, centersToScale[1]) };
            for (var index = 1; index < centersToScale.Count - 1; index++)
                scaledPath.Add(centersToScale[index]);
            scaledPath.Add(PortPointToward(downstream, centersToScale[^2]));
            return scaledPath;
        }
        var parentGrid = DisplayGridPoint(parent);
        var downstreamGrid = DisplayGridPoint(downstream);
        var deltaColumn = downstreamGrid.X - parentGrid.X;
        var deltaRow = downstreamGrid.Y - parentGrid.Y;
        var centers = new List<Point> { centerStart };
        if (deltaColumn != 0 && deltaColumn != deltaRow && deltaColumn != -deltaRow)
        {
            var branchFirst = connectionRef.Secondary
                              || !string.Equals(downstream.BranchId, parent.BranchId, StringComparison.OrdinalIgnoreCase);
            int bendGridColumn;
            int bendRow;
            if (ViewMode == LayoutViewMode.IsometricNegative)
            {
                var axisXSteps = (deltaColumn - deltaRow) / 2;
                var axisYSteps = (deltaColumn + deltaRow) / 2;
                bendGridColumn = branchFirst ? axisYSteps : axisXSteps;
                bendRow = branchFirst ? axisYSteps : -axisXSteps;
            }
            else
            {
                var axisXSteps = (deltaColumn + deltaRow) / 2;
                var axisYSteps = (deltaRow - deltaColumn) / 2;
                bendGridColumn = branchFirst ? -axisYSteps : axisXSteps;
                bendRow = branchFirst ? axisYSteps : axisXSteps;
            }
            var bendGrid = new Point(parentGrid.X + bendGridColumn, parentGrid.Y + bendRow);
            var bend = GridPoint(bendGrid);
            if (bend != centerStart && bend != centerEnd)
                centers.Add(bend);
        }
        centers.Add(centerEnd);
        var path = new List<Point>(centers.Count)
        {
            PortPointToward(parent, centers[1])
        };
        for (var index = 1; index < centers.Count - 1; index++)
            path.Add(centers[index]);
        path.Add(PortPointToward(downstream, centers[^2]));
        return path;
    }

    private void DrawPorts(Graphics graphics, SavedLayoutNode node)
    {
        var radius = Math.Max(3, (int)Math.Round(3.4 * ZoomFactor));
        using var fill = new SolidBrush(Color.FromArgb(39, 116, 119));
        using var outline = new Pen(Color.White, Math.Max(1F, 1.5F * ZoomFactor));
        var points = new HashSet<Point>();
        var nodes = NodeSource();
        foreach (var connection in Connections(nodes))
        {
            if (string.Equals(connection.Downstream.Id, node.Id, StringComparison.OrdinalIgnoreCase))
                points.Add(ConnectionEnd(connection, nodes));
            if (string.Equals(ParentId(connection), node.Id, StringComparison.OrdinalIgnoreCase))
                points.Add(ConnectionStart(connection, node, nodes));
        }
        if (ConnectedInputCount(node) < InletPortCount(node)
            || ConnectedOutputCount(node, nodes) < OutletPortCount(node))
        {
            foreach (var candidate in CandidatePortPoints(node))
                points.Add(candidate);
        }
        foreach (var point in points)
            DrawPort(point);
        return;

        void DrawPort(Point center)
        {
            var circle = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            graphics.FillEllipse(fill, circle);
            graphics.DrawEllipse(outline, circle);
        }
    }

    private LayoutPortRef? HitTestPort(Point point, bool? preferOutput = null)
    {
        var tolerance = Math.Max(9, (int)Math.Round(11 * ZoomFactor));
        foreach (var node in NodeSource().Reverse())
        {
            var nodes = NodeSource();
            if (preferOutput is not false && ConnectedOutputCount(node, nodes) < OutletPortCount(node))
            {
                var outputIndex = FirstAvailableOutputIndex(node, nodes);
                foreach (var candidate in CandidatePortPoints(node))
                    if (Math.Abs(point.X - candidate.X) <= tolerance && Math.Abs(point.Y - candidate.Y) <= tolerance)
                        return new LayoutPortRef(node, true, outputIndex, candidate);
            }
            if (preferOutput is not true && ConnectedInputCount(node) < InletPortCount(node))
            {
                var inputIndex = string.IsNullOrWhiteSpace(node.ParentNodeId) ? 0 : 1;
                foreach (var candidate in CandidatePortPoints(node))
                    if (Math.Abs(point.X - candidate.X) <= tolerance && Math.Abs(point.Y - candidate.Y) <= tolerance)
                        return new LayoutPortRef(node, false, inputIndex, candidate);
            }
        }
        return null;
    }

    private static int ConnectedInputCount(SavedLayoutNode node) =>
        (string.IsNullOrWhiteSpace(node.ParentNodeId) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) ? 0 : 1);

    private static int ConnectedOutputCount(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes) =>
        Connections(nodes).Count(x => string.Equals(ParentId(x), node.Id, StringComparison.OrdinalIgnoreCase));

    private static int FirstAvailableOutputIndex(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes)
    {
        if (OutletPortCount(node) <= 1)
            return 0;
        var hasMain = Connections(nodes).Any(x => string.Equals(ParentId(x), node.Id, StringComparison.OrdinalIgnoreCase)
                                                  && string.Equals(x.Downstream.BranchId, node.BranchId, StringComparison.OrdinalIgnoreCase));
        return hasMain ? 1 : 0;
    }

    private LayoutPortConnectionRequest CreateGestureConnectionRequest(SavedLayoutNode source,
        SavedLayoutNode destination)
    {
        var nodes = NodeSource();
        var outputIndex = FirstAvailableOutputIndex(source, nodes);
        var inputIndex = string.IsNullOrWhiteSpace(destination.ParentNodeId) ? 0 : 1;
        return new LayoutPortConnectionRequest(source, destination, outputIndex, inputIndex);
    }

    private IReadOnlyList<Point> CandidatePortPoints(SavedLayoutNode node)
    {
        var bounds = NodeBounds(node);
        var center = Center(node);
        var directions = AxisDirections;
        return directions.Select(direction => RayToBounds(bounds, center, direction)).Distinct().ToArray();
    }

    private Point PortPoint(SavedLayoutNode node, bool output, int index)
    {
        var count = output ? OutletPortCount(node) : InletPortCount(node);
        index = Math.Clamp(index, 0, Math.Max(0, count - 1));
        var nodes = NodeSource();
        if (!output)
        {
            var connection = new LayoutConnectionRef(node, index == 1);
            if (!string.IsNullOrWhiteSpace(ParentId(connection)))
                return ConnectionPath(connection, nodes).Last();
        }
        else
        {
            var outgoing = Connections(nodes)
                .Where(x => string.Equals(ParentId(x), node.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Downstream.Sequence).ThenBy(x => x.Secondary).ToArray();
            if (index < outgoing.Length)
                return ConnectionPath(outgoing[index], nodes).First();
        }

        var candidates = CandidatePortPoints(node);
        if (output)
            return candidates[Math.Min(index, candidates.Count - 1)];
        return candidates[(3 + Math.Min(index, 2)) % candidates.Count];
    }

    private SavedLayoutNode? ConnectedPeer(SavedLayoutNode node, bool output, int index)
    {
        var nodes = NodeSource();
        if (!output)
        {
            var parentId = index == 1 ? node.SecondaryParentNodeId : node.ParentNodeId;
            return nodes.FirstOrDefault(x => string.Equals(x.Id, parentId, StringComparison.OrdinalIgnoreCase));
        }

        var outgoing = Connections(nodes)
            .Where(x => string.Equals(ParentId(x), node.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Downstream.Sequence).ThenBy(x => x.Secondary).ToArray();
        if (OutletPortCount(node) > 1)
            return outgoing.Select(connection => new
                {
                    Connection = connection,
                    Port = string.Equals(connection.Downstream.BranchId, node.BranchId,
                        StringComparison.OrdinalIgnoreCase) ? 0 : 1
                })
                .FirstOrDefault(x => x.Port == index)?.Connection.Downstream;
        return index >= 0 && index < outgoing.Length ? outgoing[index].Downstream : null;
    }

    private static readonly PointF[] IsoAxisDirections =
    {
        new(2F, 1F), new(-2F, 1F), new(0F, 1F),
        new(-2F, -1F), new(2F, -1F), new(0F, -1F)
    };

    private static readonly PointF[] TopDownAxisDirections =
    {
        new(1F, 0F), new(0F, 1F), new(-1F, 0F), new(0F, -1F),
        new(1F, 0F), new(-1F, 0F)
    };

    private static readonly PointF[] NegativeIsoAxisDirections =
    {
        new(2F, -1F), new(2F, 1F), new(0F, 1F),
        new(-2F, 1F), new(-2F, -1F), new(0F, -1F)
    };

    private PointF[] AxisDirections => ViewMode switch
    {
        LayoutViewMode.TopDown => TopDownAxisDirections,
        LayoutViewMode.IsometricNegative => NegativeIsoAxisDirections,
        _ => IsoAxisDirections
    };

    private Point PortPointToward(SavedLayoutNode node, Point target)
    {
        var bounds = NodeBounds(node);
        var center = Center(node);
        var dx = target.X - center.X;
        var dy = target.Y - center.Y;
        if (dx == 0 && dy == 0)
            return new Point(center.X, bounds.Top);
        var length = Math.Sqrt(dx * dx + dy * dy);
        var directions = AxisDirections;
        var direction = directions
            .OrderByDescending(axis => (dx * axis.X + dy * axis.Y)
                                       / (length * Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y)))
            .First();
        return RayToBounds(bounds, center, direction);
    }

    private static Point RayToBounds(Rectangle bounds, Point center, PointF direction)
    {
        var halfWidth = Math.Max(1.0, bounds.Width / 2.0);
        var halfHeight = Math.Max(1.0, bounds.Height / 2.0);
        var scaleX = Math.Abs(direction.X) < 1e-6 ? double.MaxValue : halfWidth / Math.Abs(direction.X);
        var scaleY = Math.Abs(direction.Y) < 1e-6 ? double.MaxValue : halfHeight / Math.Abs(direction.Y);
        var scale = Math.Min(scaleX, scaleY);
        return new Point(
            center.X + (int)Math.Round(direction.X * scale),
            center.Y + (int)Math.Round(direction.Y * scale));
    }

    private static int InletPortCount(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.CalculationPoint => 0,
        LayoutNodeType.Fitting => FittingCatalog.InletPortCount(node.FittingGroup),
        LayoutNodeType.Reservoir when IsSourceSpace(node.SpaceRole) => 0,
        _ => 1
    };

    private static int OutletPortCount(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.CalculationPoint => 0,
        LayoutNodeType.Fitting => FittingCatalog.OutletPortCount(node.FittingGroup),
        LayoutNodeType.Reservoir when IsSinkSpace(node.SpaceRole) => 0,
        _ => 1
    };

    private static bool IsSourceSpace(string? role) =>
        string.Equals(role, "Space Source", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Return Pickup", StringComparison.OrdinalIgnoreCase);

    private static bool IsSinkSpace(string? role) =>
        string.Equals(role, "Space Sink", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Supply Discharge", StringComparison.OrdinalIgnoreCase);

    private Point Snap(Point point)
    {
        if (ViewMode == LayoutViewMode.TopDown)
        {
            var topDownX = 2.0 * (point.X - OriginX) / GridSize;
            var topDownY = 2.0 * (point.Y - OriginY) / GridSize;
            var topDownSnappedX = (int)Math.Round(topDownX);
            var topDownSnappedY = (int)Math.Round(topDownY);
            return new Point(topDownSnappedX - topDownSnappedY, topDownSnappedX + topDownSnappedY);
        }
        var column = 2.0 * (point.X - OriginX) / GridSize;
        var row = 4.0 * (point.Y - OriginY) / GridSize;
        var axisX = (column - row) / 2.0;
        var axisY = (column + row) / 2.0;
        var snappedX = (int)Math.Round(axisX);
        var snappedY = (int)Math.Round(axisY);
        return new Point(snappedX + snappedY, snappedY - snappedX);
    }

    private Point SnapPlacement(Point point)
    {
        return Snap(point);
    }

    private Point GridPoint(Point grid)
    {
        if (ViewMode == LayoutViewMode.TopDown)
        {
            var x = (grid.X + grid.Y) / 2.0;
            var y = (grid.Y - grid.X) / 2.0;
            return new Point((int)Math.Round(OriginX + x * GridSize / 2.0),
                (int)Math.Round(OriginY + y * GridSize / 2.0));
        }
        return new Point((int)Math.Round(OriginX + grid.X * GridSize / 2.0),
            (int)Math.Round(OriginY + grid.Y * GridSize / 4.0));
    }

    internal Point GridLocation(int gridX, int gridY) => GridPoint(new Point(gridX, gridY));

    private Point CoordinatePoint(LayoutCoordinate coordinate)
    {
        var cellFeet = ShowToScale ? Math.Max(0.1, FeetPerGridCell) : 5.0;
        var x = UnitConversions.MToFeet(coordinate.XM) / cellFeet;
        var y = UnitConversions.MToFeet(coordinate.YM) / cellFeet;
        var z = UnitConversions.MToFeet(coordinate.ZM) / cellFeet;
        if (ViewMode == LayoutViewMode.TopDown)
            return new Point((int)Math.Round(OriginX + x * GridSize / 2.0),
                (int)Math.Round(OriginY + y * GridSize / 2.0));
        if (ViewMode == LayoutViewMode.IsometricNegative)
            return new Point(
                (int)Math.Round(OriginX + (x + y) * GridSize / 2.0),
                (int)Math.Round(OriginY + (-x + y) * GridSize / 4.0 - z * GridSize / 2.0));
        return new Point(
            (int)Math.Round(OriginX + (x - y) * GridSize / 2.0),
            (int)Math.Round(OriginY + (x + y) * GridSize / 4.0 - z * GridSize / 2.0));
    }

    internal Point VisualLocation(SavedLayoutNode node) =>
        UsesResolvedCoordinate(node) && _resolvedCoordinates.TryGetValue(node.Sequence, out var coordinate)
            ? CoordinatePoint(coordinate)
            : GridPoint(DisplayGridPoint(node));

    private Point DisplayGridPoint(SavedLayoutNode node) => ViewMode == LayoutViewMode.IsometricNegative
        ? new Point(node.GridY, -node.GridX)
        : new Point(node.GridX, node.GridY);

    private bool UsesResolvedCoordinate(SavedLayoutNode node)
    {
        if (node.HasAbsoluteCoordinateAnchor || node.ConnectionDeltaConfirmed || node.SecondaryConnectionDeltaConfirmed)
            return true;
        return NodeSource().Any(child =>
            (child.ConnectionDeltaConfirmed
             && string.Equals(child.ParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
            || (child.SecondaryConnectionDeltaConfirmed
                && string.Equals(child.SecondaryParentNodeId, node.Id, StringComparison.OrdinalIgnoreCase)));
    }

    private Rectangle NodeBounds(SavedLayoutNode node)
    {
        var center = VisualLocation(node);
        var nodeSize = Math.Max(28, (int)Math.Round(40 * ZoomFactor));
        var half = nodeSize / 2;
        return node.Type == LayoutNodeType.Duct
            ? new Rectangle(center.X - Math.Max(2, (int)Math.Round(4 * ZoomFactor)), center.Y - half / 2,
                GridSize * 2 + Math.Max(4, (int)Math.Round(8 * ZoomFactor)), Math.Max(22, nodeSize / 2))
            : new Rectangle(center.X - half, center.Y - half, nodeSize, nodeSize);
    }

    private Point Center(SavedLayoutNode node)
    {
        var bounds = NodeBounds(node);
        return new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (dx == 0 && dy == 0)
            return Math.Sqrt(Math.Pow(point.X - start.X, 2) + Math.Pow(point.Y - start.Y, 2));
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (double)(dx * dx + dy * dy), 0, 1);
        var projectedX = start.X + t * dx;
        var projectedY = start.Y + t * dy;
        return Math.Sqrt(Math.Pow(point.X - projectedX, 2) + Math.Pow(point.Y - projectedY, 2));
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private static Point PathMidpoint(IReadOnlyList<Point> path)
    {
        var total = Enumerable.Range(1, path.Count - 1).Sum(index => Distance(path[index - 1], path[index]));
        var target = total / 2.0;
        var traversed = 0.0;
        for (var index = 1; index < path.Count; index++)
        {
            var length = Distance(path[index - 1], path[index]);
            if (traversed + length >= target && length > 0)
            {
                var ratio = (target - traversed) / length;
                return new Point(
                    (int)Math.Round(path[index - 1].X + (path[index].X - path[index - 1].X) * ratio),
                    (int)Math.Round(path[index - 1].Y + (path[index].Y - path[index - 1].Y) * ratio));
            }
            traversed += length;
        }
        return path[^1];
    }

    private static bool TryReadTool(IDataObject? data, out LayoutNodeType type, out string? fittingGroup)
    {
        type = default;
        fittingGroup = null;
        var value = data?.GetData(LayoutToolCard.DataFormat) as string;
        var parts = value?.Split('|', 2) ?? Array.Empty<string>();
        if (parts.Length == 0 || !Enum.TryParse(parts[0], out type))
            return false;
        fittingGroup = parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
        return true;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class VerticalTextButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Enabled ? BackColor : SystemColors.Control);
        e.Graphics.FillRectangle(background, ClientRectangle);
        using var border = new Pen(FlatAppearance.BorderColor.IsEmpty
            ? Color.FromArgb(180, 194, 207) : FlatAppearance.BorderColor);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(Width / 2F, Height / 2F);
        e.Graphics.RotateTransform(-90F);
        var textBounds = new RectangleF(-Height / 2F, -Width / 2F, Height, Width);
        using var textBrush = new SolidBrush(Enabled ? ForeColor : SystemColors.GrayText);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        e.Graphics.DrawString(Text, Font, textBrush, textBounds, format);
        e.Graphics.Restore(state);
    }
}

internal sealed class LayoutToolCard : Control
{
    internal const string DataFormat = "DuctSizer.LayoutNodeType";
    private readonly LayoutNodeType _type;
    private readonly string _title;
    private readonly string _detail;
    private readonly string? _fittingGroup;
    private Point _mouseDown;
    private bool _dragStarted;
    internal event Action<LayoutNodeType, string?>? ToolClicked;

    internal LayoutToolCard(LayoutNodeType type, string title, string detail, string? fittingGroup = null)
    {
        _type = type;
        _title = title;
        _detail = detail;
        _fittingGroup = fittingGroup;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        DoubleBuffered = true;
        AccessibleName = $"Add {title}";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var iconSize = Math.Min(40, Math.Max(28, Height - 8));
        LayoutIconPainter.Draw(e.Graphics, _type,
            new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize),
            Color.FromArgb(21, 83, 99), _fittingGroup);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _mouseDown = e.Location;
        _dragStarted = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button != MouseButtons.Left || _dragStarted
            || Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) < 8)
            return;
        _dragStarted = true;
        var data = new DataObject();
        data.SetData(DataFormat, $"{_type}|{_fittingGroup}");
        DoDragDrop(data, DragDropEffects.Copy);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!_dragStarted)
            ToolClicked?.Invoke(_type, _fittingGroup);
    }

}

internal static class LayoutIconPainter
{
    internal static void Draw(Graphics graphics, LayoutNodeType type, Rectangle bounds, Color color, string? fittingGroup = null)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (type == LayoutNodeType.Fitting && FittingCatalog.IsFanExitsAndPlenumsSelectionGroup(fittingGroup))
        {
            DrawPlenumIcon(graphics, bounds, color);
            graphics.Restore(state);
            return;
        }
        if (type == LayoutNodeType.Fitting
            && string.Equals(fittingGroup, FittingCatalog.VavSelectionGroup, StringComparison.OrdinalIgnoreCase))
        {
            DrawVavIcon(graphics, bounds, color);
            graphics.Restore(state);
            return;
        }
        if (OpenSourceIconCatalog.Draw(graphics, IconKey(type, fittingGroup), bounds, color))
        {
            graphics.Restore(state);
            return;
        }
        var stroke = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 14F, 1.6F, 2.6F);
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var fillColor = color.R > 150 && color.G < 100
            ? Color.FromArgb(252, 232, 232)
            : Color.FromArgb(232, 243, 245);
        using var brush = new SolidBrush(fillColor);
        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        switch (type)
        {
            case LayoutNodeType.Reservoir:
                {
                    var room = Rectangle.Inflate(bounds, -2, -2);
                    using var roomPath = RoundedIconRectangle(room, Math.Max(2, room.Width / 10));
                    graphics.FillPath(brush, roomPath);
                    graphics.DrawPath(pen, roomPath);
                    var inset = Math.Max(5, room.Width / 4);
                    graphics.DrawLine(pen, room.Left + inset, room.Top + inset,
                        room.Right - inset, room.Top + inset);
                    graphics.DrawLine(pen, room.Left + inset, room.Top + inset,
                        room.Left + inset, room.Bottom - inset);
                    graphics.DrawLine(pen, room.Left + inset, room.Bottom - inset,
                        room.Left + room.Width / 2, room.Bottom - inset);
                    using var doorPen = new Pen(color, Math.Max(1.4F, pen.Width * .65F))
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    graphics.DrawArc(doorPen, room.Left + room.Width / 2 - inset / 2,
                        room.Bottom - inset * 2, inset, inset, 90, 90);
                }
                break;
            case LayoutNodeType.Fan:
                {
                    using var volute = new GraphicsPath();
                    var fanLeft = bounds.Left + 1F;
                    var fanTop = bounds.Top + 1F;
                    var fanRight = bounds.Right - 1F;
                    var fanBottom = bounds.Bottom - 1F;
                    var fanWidth = fanRight - fanLeft;
                    var fanHeight = fanBottom - fanTop;
                    volute.StartFigure();
                    volute.AddLine(fanLeft + fanWidth * .46F, fanTop + fanHeight * .08F, fanRight, fanTop + fanHeight * .08F);
                    volute.AddLine(fanRight, fanTop + fanHeight * .08F, fanRight, fanTop + fanHeight * .43F);
                    volute.AddLine(fanRight, fanTop + fanHeight * .43F, fanLeft + fanWidth * .62F, fanTop + fanHeight * .43F);
                    volute.AddBezier(fanLeft + fanWidth * .62F, fanTop + fanHeight * .43F,
                        fanLeft + fanWidth * .69F, fanTop + fanHeight * .60F,
                        fanLeft + fanWidth * .60F, fanTop + fanHeight * .82F,
                        fanLeft + fanWidth * .45F, fanTop + fanHeight * .90F);
                    volute.AddBezier(fanLeft + fanWidth * .45F, fanTop + fanHeight * .90F,
                        fanLeft + fanWidth * .20F, fanBottom,
                        fanLeft + fanWidth * .03F, fanTop + fanHeight * .72F,
                        fanLeft + fanWidth * .06F, fanTop + fanHeight * .43F);
                    volute.AddBezier(fanLeft + fanWidth * .06F, fanTop + fanHeight * .43F,
                        fanLeft + fanWidth * .08F, fanTop + fanHeight * .20F,
                        fanLeft + fanWidth * .25F, fanTop + fanHeight * .08F,
                        fanLeft + fanWidth * .46F, fanTop + fanHeight * .08F);
                    volute.CloseFigure();
                    graphics.FillPath(brush, volute);
                    using var smoothPen = new Pen(color, Math.Max(1.8F, pen.Width * .82F))
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    graphics.DrawPath(smoothPen, volute);
                }
                break;
            case LayoutNodeType.Coil:
                var coilBounds = Rectangle.Inflate(bounds, -2, -2);
                using (var coilPath = RoundedIconRectangle(coilBounds, Math.Max(2, bounds.Width / 12)))
                {
                    graphics.FillPath(brush, coilPath);
                    graphics.DrawPath(pen, coilPath);
                }
                var left = coilBounds.Left + 4;
                var width = coilBounds.Width - 8;
                var points = new List<Point>();
                for (var index = 0; index <= 8; index++)
                    points.Add(new Point(left + index * width / 8, coilBounds.Top + (index % 2 == 0 ? 5 : coilBounds.Height - 5)));
                graphics.DrawLines(pen, points.ToArray());
                break;
            case LayoutNodeType.Filter:
                var filterBounds = Rectangle.Inflate(bounds, -2, -2);
                using (var filterPath = RoundedIconRectangle(filterBounds, Math.Max(2, bounds.Width / 12)))
                {
                    graphics.FillPath(brush, filterPath);
                    graphics.DrawPath(pen, filterPath);
                }
                for (var index = -1; index <= 1; index++)
                {
                    var offset = index * Math.Max(4, filterBounds.Width / 5);
                    graphics.DrawLine(pen, filterBounds.Left + 5 + offset / 2, filterBounds.Bottom - 5,
                        filterBounds.Right - 5 + offset / 2, filterBounds.Top + 5);
                }
                break;
            case LayoutNodeType.Grd:
                DrawGrdIcon(graphics, bounds, color, fittingGroup);
                break;
            case LayoutNodeType.CalculationPoint:
                {
                    var diamond = new[]
                    {
                        new Point(center.X, bounds.Top + 1),
                        new Point(bounds.Right - 1, center.Y),
                        new Point(center.X, bounds.Bottom - 1),
                        new Point(bounds.Left + 1, center.Y)
                    };
                    using var sensorFill = new LinearGradientBrush(bounds,
                        Color.FromArgb(63, 137, 218), Color.FromArgb(20, 51, 148), 45F);
                    using var sensorOutline = new Pen(Color.Black,
                        Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 12F, 1.8F, 3.2F))
                    {
                        LineJoin = LineJoin.Round
                    };
                    graphics.FillPolygon(sensorFill, diamond);
                    graphics.DrawPolygon(sensorOutline, diamond);
                    var letter = fittingGroup?.StartsWith("Temperature", StringComparison.OrdinalIgnoreCase) == true ? "T"
                        : fittingGroup?.StartsWith("Velocity", StringComparison.OrdinalIgnoreCase) == true ? "V"
                        : fittingGroup?.StartsWith("Airflow", StringComparison.OrdinalIgnoreCase) == true ? "Q" : "P";
                    using var font = new Font("Segoe UI Semibold", Math.Max(7F, bounds.Height * .38F), FontStyle.Bold);
                    TextRenderer.DrawText(graphics, letter, font, bounds, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    if (color.R > 150 && color.G < 150)
                    {
                        using var statusPen = new Pen(color, Math.Max(1.8F, sensorOutline.Width * .8F))
                            { DashStyle = DashStyle.Dash };
                        graphics.DrawPolygon(statusPen, diamond);
                    }
                }
                break;
            case LayoutNodeType.Fitting:
                DrawFittingIcon(graphics, bounds, color, fittingGroup);
                break;
            case LayoutNodeType.Duct:
                graphics.DrawLine(pen, bounds.Left, bounds.Top + 7, bounds.Right, bounds.Top + 7);
                graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 7, bounds.Right, bounds.Bottom - 7);
                graphics.DrawLine(pen, bounds.Left, bounds.Top + 7, bounds.Left, bounds.Bottom - 7);
                graphics.DrawLine(pen, bounds.Right, bounds.Top + 7, bounds.Right, bounds.Bottom - 7);
                break;
        }
        graphics.Restore(state);
    }

    private static string IconKey(LayoutNodeType type, string? detail) => type switch
    {
        LayoutNodeType.Reservoir when detail?.Contains("Source", StringComparison.OrdinalIgnoreCase) == true
            => "space-source",
        LayoutNodeType.Reservoir => "space-sink",
        LayoutNodeType.Fan => "fan",
        LayoutNodeType.Coil => "coil",
        LayoutNodeType.Filter => "filter",
        LayoutNodeType.Grd when string.Equals(detail, "Grille", StringComparison.OrdinalIgnoreCase) => "toolbox-grd-grille",
        LayoutNodeType.Grd when string.Equals(detail, "Diffuser", StringComparison.OrdinalIgnoreCase) => "toolbox-grd-diffuser",
        LayoutNodeType.Grd => "toolbox-grd-diffuser",
        LayoutNodeType.CustomComponent => "other",
        LayoutNodeType.CalculationPoint when detail?.StartsWith("Temperature", StringComparison.OrdinalIgnoreCase) == true
            => "toolbox-sensor-temperature",
        LayoutNodeType.CalculationPoint when detail?.StartsWith("Velocity", StringComparison.OrdinalIgnoreCase) == true
            => "toolbox-sensor-velocity",
        LayoutNodeType.CalculationPoint when detail?.StartsWith("Airflow", StringComparison.OrdinalIgnoreCase) == true
            => "sensor-airflow",
        LayoutNodeType.CalculationPoint => "sensor-pressure",
        LayoutNodeType.Fitting when string.Equals(detail, "Transitions", StringComparison.OrdinalIgnoreCase)
            => "transition",
        LayoutNodeType.Fitting when string.Equals(detail, "Diverting Fittings", StringComparison.OrdinalIgnoreCase)
            => "diverging",
        LayoutNodeType.Fitting when string.Equals(detail, "Converging Fittings", StringComparison.OrdinalIgnoreCase)
            => "converging",
        LayoutNodeType.Fitting when string.Equals(detail, "Dampers", StringComparison.OrdinalIgnoreCase)
            => "damper",
        LayoutNodeType.Fitting when string.Equals(detail, FittingCatalog.VavSelectionGroup, StringComparison.OrdinalIgnoreCase)
            => "other",
        LayoutNodeType.Fitting when FittingCatalog.IsTerminalSelectionGroup(detail)
            => "terminal",
        LayoutNodeType.Fitting when FittingCatalog.IsFanExitsAndPlenumsSelectionGroup(detail)
            => "other",
        LayoutNodeType.Fitting => "elbow",
        LayoutNodeType.Duct => "duct",
        _ => "other"
    };

    private static void DrawPlenumIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        var square = Rectangle.Inflate(bounds, -3, -3);
        using var fill = new LinearGradientBrush(square,
            Color.FromArgb(124, 198, 245), Color.FromArgb(72, 158, 224), 45F);
        using var outline = new Pen(color,
            Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 16F, 1.4F, 2.2F))
        {
            LineJoin = LineJoin.Round
        };
        graphics.FillRectangle(fill, square);
        graphics.DrawRectangle(outline, square);
    }

    private static void DrawVavIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        // Match the existing Custom component's blue duct symbol and distinguish the VAV
        // with the requested black controller/terminal square at its center.
        if (!OpenSourceIconCatalog.Draw(graphics, "other", bounds, color))
        {
            using var ductPen = new Pen(color, Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 15F, 1.5F, 2.4F))
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var mid = bounds.Top + bounds.Height / 2;
            graphics.DrawLine(ductPen, bounds.Left + 2, mid, bounds.Right - 2, mid);
        }
        var side = Math.Max(7, (int)Math.Round(Math.Min(bounds.Width, bounds.Height) * .38));
        var square = new Rectangle(bounds.Left + (bounds.Width - side) / 2,
            bounds.Top + (bounds.Height - side) / 2, side, side);
        using var fill = new SolidBrush(Color.Black);
        using var outline = new Pen(Color.FromArgb(20, 45, 65), Math.Max(1F, side / 14F));
        graphics.FillRectangle(fill, square);
        graphics.DrawRectangle(outline, square);
    }

    private static GraphicsPath RoundedIconRectangle(Rectangle bounds, int radius)
    {
        radius = Math.Clamp(radius, 1, Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawFittingIcon(Graphics graphics, Rectangle bounds, Color color, string? group)
    {
        using var pen = new Pen(color, Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 15F, 1.5F, 2.4F))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var midY = bounds.Top + bounds.Height / 2;
        if (group == "Transitions")
        {
            using var fill = new SolidBrush(Color.FromArgb(232, 243, 245));
            var reducer = new[]
            {
                new Point(bounds.Left + 3, bounds.Top + 4), new Point(bounds.Right - 3, bounds.Top + bounds.Height / 3),
                new Point(bounds.Right - 3, bounds.Bottom - bounds.Height / 3), new Point(bounds.Left + 3, bounds.Bottom - 4)
            };
            graphics.FillPolygon(fill, reducer);
            graphics.DrawPolygon(pen, reducer);
        }
        else if (group == "Diverting Fittings")
        {
            DrawDoubleBranch(graphics, pen, bounds, converging: false);
        }
        else if (group == "Converging Fittings")
        {
            DrawDoubleBranch(graphics, pen, bounds, converging: true);
        }
        else if (group == "Dampers")
        {
            using var fill = new SolidBrush(Color.FromArgb(232, 243, 245));
            graphics.FillRectangle(fill, bounds.Left + 3, bounds.Top + 5, bounds.Width - 6, bounds.Height - 10);
            graphics.DrawRectangle(pen, bounds.Left + 3, bounds.Top + 5, bounds.Width - 6, bounds.Height - 10);
            graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 9, bounds.Right - 8, bounds.Top + 9);
        }
        else if (FittingCatalog.IsTerminalSelectionGroup(group))
        {
            using var fill = new SolidBrush(Color.FromArgb(232, 243, 245));
            graphics.FillRectangle(fill, bounds.Left + 4, bounds.Top + 6, bounds.Width - 8, bounds.Height - 12);
            graphics.DrawRectangle(pen, bounds.Left + 4, bounds.Top + 6, bounds.Width - 8, bounds.Height - 12);
            for (var index = 1; index <= 3; index++)
                graphics.DrawLine(pen, bounds.Left + 7, bounds.Top + index * bounds.Height / 4,
                    bounds.Right - 7, bounds.Top + index * bounds.Height / 4);
        }
        else if (FittingCatalog.IsFanExitsAndPlenumsSelectionGroup(group))
        {
            using var fill = new SolidBrush(Color.FromArgb(232, 243, 245));
            var diamond = new[]
            {
                new Point(bounds.Left + bounds.Width / 2, bounds.Top + 3), new Point(bounds.Right - 3, midY),
                new Point(bounds.Left + bounds.Width / 2, bounds.Bottom - 3), new Point(bounds.Left + 3, midY)
            };
            graphics.FillPolygon(fill, diamond);
            graphics.DrawPolygon(pen, diamond);
        }
        else
        {
            DrawDoubleLineElbow(graphics, pen, bounds);
        }
    }

    private static void DrawDoubleLineElbow(Graphics graphics, Pen pen, Rectangle bounds)
    {
        var inset = Math.Max(3, bounds.Width / 6);
        using var outer = new GraphicsPath();
        outer.AddLine(bounds.Left + inset, bounds.Bottom - 2, bounds.Left + inset, bounds.Top + inset * 2);
        outer.AddArc(bounds.Left + inset, bounds.Top + inset, bounds.Width - inset * 2, bounds.Height - inset * 2, 180, 90);
        outer.AddLine(bounds.Left + bounds.Width / 2, bounds.Top + inset, bounds.Right - 2, bounds.Top + inset);
        graphics.DrawPath(pen, outer);
        using var inner = new GraphicsPath();
        inner.AddLine(bounds.Left + inset * 2, bounds.Bottom - 2, bounds.Left + inset * 2, bounds.Top + inset * 2 + 2);
        inner.AddArc(bounds.Left + inset * 2, bounds.Top + inset * 2, Math.Max(4, bounds.Width - inset * 4),
            Math.Max(4, bounds.Height - inset * 4), 180, 90);
        inner.AddLine(bounds.Left + bounds.Width / 2, bounds.Top + inset * 2, bounds.Right - 2, bounds.Top + inset * 2);
        graphics.DrawPath(pen, inner);
        graphics.DrawLine(pen, bounds.Left + inset, bounds.Bottom - 2,
            bounds.Left + inset * 2, bounds.Bottom - 2);
        graphics.DrawLine(pen, bounds.Right - 2, bounds.Top + inset,
            bounds.Right - 2, bounds.Top + inset * 2);
    }

    private static void DrawDoubleBranch(Graphics graphics, Pen pen, Rectangle bounds, bool converging)
    {
        var left = bounds.Left + 2;
        var right = bounds.Right - 2;
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        var half = Math.Max(3, bounds.Height / 7);
        var top = bounds.Top + 3;
        var bottom = bounds.Bottom - 3;
        Point[] body = converging
            ? new[]
            {
                new Point(right, centerY - half), new Point(centerX + 2, centerY - half),
                new Point(left + 3, top), new Point(left, top + half * 2),
                new Point(centerX - 4, centerY + 1), new Point(left, bottom - half * 2),
                new Point(left + 3, bottom), new Point(centerX + 2, centerY + half),
                new Point(right, centerY + half)
            }
            : new[]
            {
                new Point(left, centerY - half), new Point(centerX - 2, centerY - half),
                new Point(right - 3, top), new Point(right, top + half * 2),
                new Point(centerX + 4, centerY + 1), new Point(right, bottom - half * 2),
                new Point(right - 3, bottom), new Point(centerX - 2, centerY + half),
                new Point(left, centerY + half)
            };
        using var fill = new SolidBrush(Color.FromArgb(232, 243, 245));
        graphics.FillPolygon(fill, body);
        graphics.DrawPolygon(pen, body);
    }

    private static void DrawGrdIcon(Graphics graphics, Rectangle bounds, Color color, string? kind)
    {
        using var pen = new Pen(color, Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 16F, 1.4F, 2.2F))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var square = Rectangle.Inflate(bounds, -3, -3);
        using var fill = new LinearGradientBrush(square,
            Color.FromArgb(124, 198, 245), Color.FromArgb(72, 158, 224), 45F);
        graphics.FillRectangle(fill, square);
        graphics.DrawRectangle(pen, square);
        if (string.Equals(kind, "Grille", StringComparison.OrdinalIgnoreCase))
        {
            for (var index = 1; index < 4; index++)
            {
                var x = square.Left + index * square.Width / 4;
                var y = square.Top + index * square.Height / 4;
                graphics.DrawLine(pen, x, square.Top + 2, x, square.Bottom - 2);
                graphics.DrawLine(pen, square.Left + 2, y, square.Right - 2, y);
            }
        }
        else if (string.Equals(kind, "Diffuser", StringComparison.OrdinalIgnoreCase))
        {
            graphics.DrawRectangle(pen, Rectangle.Inflate(square, -square.Width / 4, -square.Height / 4));
        }
        else
        {
            for (var index = 1; index <= 3; index++)
            {
                var y = square.Top + index * square.Height / 4;
                graphics.DrawLine(pen, square.Left + 3, y, square.Right - 3, y);
            }
        }
        if (color.R > 150 && color.G < 150)
        {
            using var statusPen = new Pen(color, Math.Max(1.8F, pen.Width * .8F)) { DashStyle = DashStyle.Dash };
            graphics.DrawRectangle(statusPen, Rectangle.Inflate(square, 2, 2));
        }
    }
}

internal sealed class SourceSinkEditorDialog : Form
{
    private readonly IReadOnlyList<SavedLayoutNode> _boundaries;
    private readonly Dictionary<int, TextBox> _airflowInputs = new();
    private readonly Label _balanceStatus = new();
    private readonly Button _save = new();
    internal IReadOnlyDictionary<int, double> AirflowsCfm { get; private set; } =
        new Dictionary<int, double>();

    internal SourceSinkEditorDialog(IReadOnlyList<SavedLayoutNode> boundaries)
    {
        _boundaries = boundaries.OrderBy(node => node.Sequence).ToArray();
        Text = "Source and Sink Editor";
        Name = "SourceSinkEditorDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(690, Math.Min(580, 190 + _boundaries.Count * 38));
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(new Label
        {
            Text = "SOURCE AND SINK AIRFLOWS\r\nEdit the boundary flows so total source airflow equals total sink airflow.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _balanceStatus.Name = "SourceSinkBalanceStatusLabel";
        _balanceStatus.Dock = DockStyle.Fill;
        _balanceStatus.TextAlign = ContentAlignment.MiddleLeft;
        _balanceStatus.Font = new Font("Segoe UI Semibold", 8.5F);
        _balanceStatus.Padding = new Padding(8, 0, 8, 0);
        root.Controls.Add(_balanceStatus, 0, 1);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = _boundaries.Count + 1,
            Padding = new Padding(8),
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        AddHeader("TYPE", 0);
        AddHeader("ITEM", 1);
        AddHeader("AIRFLOW (CFM)", 2);
        AddHeader("PRESSURE (IN. W.C.)", 3);
        var row = 1;
        foreach (var boundary in _boundaries)
        {
            var source = string.Equals(boundary.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(boundary.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase);
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.Controls.Add(CellLabel(source ? "Source" : "Sink", true), 0, row);
            table.Controls.Add(CellLabel(boundary.Id, false), 1, row);
            var input = new TextBox
            {
                Name = $"SourceSinkAirflow_{boundary.Sequence}",
                Text = UnitConversions.M3PerSToCfm(boundary.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture),
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(4, 4, 8, 4)
            };
            input.TextChanged += (_, _) => ValidateBalance();
            _airflowInputs[boundary.Sequence] = input;
            table.Controls.Add(input, 2, row);
            table.Controls.Add(new Label
            {
                Name = $"SourceSinkPressure_{boundary.Sequence}",
                Text = $"{NormalizePressure(UnitConversions.PaToInchesWater(boundary.PressurePa)):0.###} in. w.c.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(28, 69, 108),
                Font = new Font("Segoe UI Semibold", 8.7F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(4, 0, 8, 0)
            }, 3, row++);
        }
        scroll.Controls.Add(table);
        table.Dock = DockStyle.Top;
        root.Controls.Add(scroll, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        _save.Name = "SourceSinkSaveButton";
        _save.Text = "Save Airflows";
        _save.Width = 118;
        _save.Height = 32;
        _save.BackColor = Color.FromArgb(42, 135, 91);
        _save.ForeColor = Color.White;
        _save.FlatStyle = FlatStyle.Flat;
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(_save, 7);
        UiPolish.RoundButton(cancel, 7);
        _save.Click += (_, _) => SaveAirflows();
        actions.Controls.Add(_save);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
        AcceptButton = _save;
        CancelButton = cancel;
        ValidateBalance();

        void AddHeader(string text, int column)
        {
            table.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(72, 92, 110),
                Font = new Font("Segoe UI Semibold", 8F),
                TextAlign = column >= 2 ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 8, 0)
            }, column, 0);
        }

        static Label CellLabel(string text, bool bold) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI" + (bold ? " Semibold" : string.Empty), 8.7F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
    }

    private static double NormalizePressure(double pressure) => Math.Abs(pressure) < 0.0005 ? 0.0 : pressure;

    private void ValidateBalance()
    {
        var valid = TryReadAirflows(out var values, out var sourceTotal, out var sinkTotal);
        var tolerance = Math.Max(0.5, Math.Max(sourceTotal, sinkTotal) * 0.001);
        var balanced = valid && Math.Abs(sourceTotal - sinkTotal) <= tolerance;
        _save.Enabled = balanced;
        _balanceStatus.ForeColor = balanced ? Color.FromArgb(30, 100, 55) : Color.Firebrick;
        _balanceStatus.Text = !valid
            ? "Enter a positive numeric airflow for every source and sink."
            : balanced
                ? $"Balanced: {sourceTotal:N0} cfm source = {sinkTotal:N0} cfm sink."
                : $"Airflows do not balance: sources {sourceTotal:N0} cfm; sinks {sinkTotal:N0} cfm; difference {sourceTotal - sinkTotal:+#,##0;-#,##0;0} cfm.";
    }

    private void SaveAirflows()
    {
        if (!TryReadAirflows(out var values, out var sourceTotal, out var sinkTotal)
            || Math.Abs(sourceTotal - sinkTotal) > Math.Max(0.5, Math.Max(sourceTotal, sinkTotal) * 0.001))
        {
            ValidateBalance();
            return;
        }
        AirflowsCfm = values;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadAirflows(out Dictionary<int, double> values, out double sourceTotal,
        out double sinkTotal)
    {
        values = new Dictionary<int, double>();
        sourceTotal = 0;
        sinkTotal = 0;
        foreach (var boundary in _boundaries)
        {
            if (!_airflowInputs.TryGetValue(boundary.Sequence, out var input)
                || !double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm)
                || !double.IsFinite(cfm) || cfm <= 0)
                return false;
            values[boundary.Sequence] = cfm;
            var source = string.Equals(boundary.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(boundary.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase);
            if (source) sourceTotal += cfm;
            else sinkTotal += cfm;
        }
        return sourceTotal > 0 && sinkTotal > 0;
    }
}

internal sealed class LayoutEquipmentDialog : Form
{
    private readonly SavedLayoutNode _node;
    private readonly TextBox _componentDescription = new();
    private readonly TextBox _airflow = new();
    private readonly TextBox _temperature = new();
    private readonly TextBox _pressureDrop = new();
    private readonly TextBox _fanEfficiency = new();
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();
    private readonly ComboBox _airSystem = new();
    private readonly ComboBox _grdType = new();
    private readonly ComboBox _grdTitus350RSize = new();
    private readonly ComboBox _grdFaceSize = new();
    private readonly ComboBox _grdNeckDiameter = new();
    private readonly ComboBox _grdLength = new();
    private readonly ComboBox _grdSlotWidth = new();
    private readonly ComboBox _grdSlotCount = new();
    private readonly Label _grdPerformanceResult = new();
    private readonly ComboBox _spaceRole = new();
    private readonly TextBox _calculatedValue = new();
    private readonly ComboBox _upstream = new();
    private readonly Label _downstream = new();
    private readonly bool _showTopology;
    private readonly bool _absoluteLocation;
    private readonly LayoutEditableAxes? _editableAxes;
    private LayoutCoordinate _desiredAbsoluteCoordinate;

    internal string SelectedParentNodeId =>
        (_upstream.SelectedItem as LayoutNodeChoice)?.Id ?? _node.ParentNodeId;
    internal string SelectedAirSystemType =>
        Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? _node.AirSystemType;
    internal LayoutCoordinate DesiredAbsoluteCoordinate => _desiredAbsoluteCoordinate;

    internal LayoutEquipmentDialog(SavedLayoutNode node)
        : this(node, Array.Empty<SavedLayoutNode>(), _ => string.Empty, false, null, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource)
        : this(node, nodes, captionSource, true, null, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, LayoutCoordinate absoluteCoordinate)
        : this(node, nodes, captionSource, true, absoluteCoordinate, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, LayoutCoordinate absoluteCoordinate,
        LayoutEditableAxes editableAxes)
        : this(node, nodes, captionSource, true, absoluteCoordinate, editableAxes)
    {
    }

    private LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, bool showTopology, LayoutCoordinate? absoluteCoordinate,
        LayoutEditableAxes? editableAxes)
    {
        _node = node;
        _showTopology = showTopology;
        _absoluteLocation = absoluteCoordinate.HasValue;
        _editableAxes = editableAxes;
        var reservoir = node.Type == LayoutNodeType.Reservoir;
        var fan = node.Type == LayoutNodeType.Fan;
        var filter = node.Type == LayoutNodeType.Filter;
        var grd = node.Type == LayoutNodeType.Grd;
        var calculationPoint = node.Type == LayoutNodeType.CalculationPoint;
        var custom = node.Type == LayoutNodeType.CustomComponent;
        var sourceSpace = reservoir && (string.Equals(node.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(node.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase));
        Text = reservoir ? $"{(sourceSpace ? "Space Source" : "Space Sink")} Inputs"
            : calculationPoint ? $"{node.CalculationPointType} Calculation Point"
            : custom ? "Custom Component Inputs"
            : fan ? "System Fan Inputs" : filter ? "Filter Inputs" : grd ? "GRD Inputs" : "Coil Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 320);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _airflow.Text = node.AirflowM3PerS > 0
            ? UnitConversions.M3PerSToCfm(node.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture)
            : "1000";
        _airflow.Name = "LayoutEquipmentAirflowText";
        _pressureDrop.Name = "LayoutEquipmentPressureDropText";
        _componentDescription.Name = "LayoutCustomDescriptionText";
        _componentDescription.Text = string.IsNullOrWhiteSpace(node.Description) ? "Custom Component" : node.Description;
        _fanEfficiency.Name = "LayoutFanEfficiencyText";
        _fanEfficiency.Text = node.FanEfficiencyPercent.ToString("0.##", CultureInfo.CurrentCulture);
        _deltaX.Name = "LayoutEquipmentDeltaXText";
        _deltaY.Name = "LayoutEquipmentDeltaYText";
        _deltaZ.Name = "LayoutEquipmentDeltaZText";
        _airSystem.Name = "LayoutEquipmentAirSystemCombo";
        _grdType.Name = "LayoutGrdTypeCombo";
        _grdTitus350RSize.Name = "LayoutGrdTitus350RSizeCombo";
        _grdFaceSize.Name = "LayoutGrdFaceSizeCombo";
        _grdNeckDiameter.Name = "LayoutGrdNeckDiameterCombo";
        _grdLength.Name = "LayoutGrdLengthCombo";
        _grdSlotWidth.Name = "LayoutGrdSlotWidthCombo";
        _grdSlotCount.Name = "LayoutGrdSlotCountCombo";
        _grdPerformanceResult.Name = "LayoutGrdPerformanceResult";
        _spaceRole.Name = "LayoutSpaceRoleCombo";
        _temperature.Text = UnitConversions.CelsiusToFahrenheit(node.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture);
        _pressureDrop.Text = UnitConversions.PaToInchesWater(sourceSpace ? node.PressurePa
                : fan ? node.FanStaticPressurePa : node.PressureDropPa)
            .ToString("0.###", CultureInfo.CurrentCulture);
        var location = absoluteCoordinate ?? new LayoutCoordinate(node.DeltaXM, node.DeltaYM, node.DeltaZM);
        _desiredAbsoluteCoordinate = location;
        _deltaX.Text = UnitConversions.MToFeet(location.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaY.Text = UnitConversions.MToFeet(location.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaZ.Text = UnitConversions.MToFeet(location.ZM).ToString("0.##", CultureInfo.CurrentCulture);
        var coordinateInputs = new[]
        {
            (Input: _deltaX, Editable: editableAxes?.X ?? true),
            (Input: _deltaY, Editable: editableAxes?.Y ?? true),
            (Input: _deltaZ, Editable: editableAxes?.Z ?? true)
        };
        foreach (var coordinateInput in coordinateInputs)
        {
            coordinateInput.Input.ReadOnly = _absoluteLocation && !coordinateInput.Editable;
            if (coordinateInput.Input.ReadOnly)
                coordinateInput.Input.BackColor = Color.FromArgb(235, 241, 246);
        }
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = _airSystem.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.AirSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";
        _grdType.DropDownStyle = ComboBoxStyle.DropDownList;
        _grdType.Items.AddRange(new object[] { "Grille", "Diffuser", "Linear Diffuser TBD-80 D" });
        _grdType.SelectedItem = _grdType.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.GrdType, StringComparison.OrdinalIgnoreCase)) ?? "Grille";
        ConfigureGrdPerformanceControls(node);
        _spaceRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _spaceRole.Items.AddRange(new object[] { "Space Sink", "Space Source" });
        _spaceRole.SelectedItem = _spaceRole.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.SpaceRole, StringComparison.OrdinalIgnoreCase)) ?? (sourceSpace ? "Space Source" : "Space Sink");
        _spaceRole.Enabled = false;
        _calculatedValue.Name = "LayoutCalculationPointValue";
        _calculatedValue.ReadOnly = true;
        _calculatedValue.BackColor = Color.FromArgb(235, 241, 246);
        _calculatedValue.Text = node.CalculationPointType switch
        {
            "Temperature" => $"{UnitConversions.CelsiusToFahrenheit(node.TemperatureC):0.##} °F",
            "Velocity" => $"{UnitConversions.MpsToFpm(node.VelocityMPerS):N0} fpm",
            "Airflow" => $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):N0} cfm",
            _ => $"{UnitConversions.PaToInchesWater(node.PressurePa):0.###} in. w.g."
        };
        if (showTopology)
        {
            PopulateRelationshipControls(node, nodes, captionSource, _upstream, _downstream);
            _upstream.Name = "LayoutUpstreamItemCombo";
            _downstream.Name = "LayoutDownstreamItemsLabel";
        }

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12 + (showTopology ? 2 : 0),
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        AddRow(table, 0, reservoir ? (sourceSpace ? "Space Source" : "Space Sink")
            : calculationPoint ? $"{node.CalculationPointType} Calculation Point"
            : custom ? "Custom Component"
            : fan ? "System Fan" : filter ? "Air Filter" : grd ? "Grille / Diffuser" : "Cooling / Heating Coil", new Label
        {
            Text = reservoir ? sourceSpace
                    ? "System airflow source with entered temperature and gauge pressure"
                    : "Terminal sink with editable design airflow and temperature calculated from the upstream system"
                : calculationPoint ? "Read-only value calculated at this duct location"
                : custom ? "Enter the component description and its static pressure drop"
                : fan ? "Design airflow and entering-air temperature"
                : filter ? "Enter the filter pressure drop"
                : grd ? "Select the outlet type and enter its airflow and pressure drop"
                : "Leaving-air temperature and coil pressure drop",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        });
        var row = 1;
        if (reservoir)
        {
            AddRow(table, row++, "Space Connection", _spaceRole);
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Space Temperature (°F)", _temperature);
            if (sourceSpace)
                AddRow(table, row++, "Source Pressure (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
            if (!sourceSpace)
            {
                _temperature.ReadOnly = true;
                _temperature.BackColor = Color.FromArgb(235, 241, 246);
            }
        }
        else if (fan)
        {
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Air Temperature (°F)", _temperature);
            AddRow(table, row++, "System Static Pressure (in. w.c.)", _pressureDrop);
            AddRow(table, row++, "Fan Efficiency", LayoutDialogControls.CreateInputWithUnit(_fanEfficiency, "%"));
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (grd)
        {
            AddRow(table, row++, "GRD Type", _grdType);
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Published Selection", BuildGrdPerformancePanel(), 232);
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (calculationPoint)
        {
            AddRow(table, row++, "Calculated Value", _calculatedValue);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (filter)
        {
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (custom)
        {
            AddRow(table, row++, "Component Description", _componentDescription);
            AddRow(table, row++, "Static Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else
        {
            AddRow(table, row++, "Leaving Temperature (°F)", _temperature);
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        AddRow(table, row++, _absoluteLocation ? "ABSOLUTE X / Y / Z (ft)" : "RELATIVE DELTA (ft)",
            LayoutDialogControls.CreateLocationEditor(_deltaX, _deltaY, _deltaZ));
        if (showTopology)
        {
            AddRow(table, row++, "Upstream Item / Fitting", _upstream);
            AddRow(table, row++, "Downstream Item(s)", _downstream);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.None, Width = 90, Height = 32, BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(ok, 7);
        UiPolish.RoundButton(cancel, 7);
        ok.Click += (_, _) => SaveInputs();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        table.Controls.Add(buttons, 0, row);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        ClientSize = new Size(520, 136 + Math.Max(0, row - 1) * 40 + (grd ? 192 : 0));
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void ConfigureGrdPerformanceControls(SavedLayoutNode node)
    {
        foreach (var combo in new[] { _grdTitus350RSize, _grdFaceSize, _grdNeckDiameter, _grdLength, _grdSlotWidth, _grdSlotCount })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        _grdTitus350RSize.Items.AddRange(GrdPerformanceCatalog.Titus350RSizes.Cast<object>().ToArray());
        _grdFaceSize.Items.AddRange(GrdPerformanceCatalog.PriceFaceSizes.Cast<object>().ToArray());
        _grdLength.Items.AddRange(GrdPerformanceCatalog.TitusLengthsFt.Cast<object>().ToArray());
        foreach (var slotWidth in GrdPerformanceCatalog.TitusSlotWidthsIn)
            _grdSlotWidth.Items.Add(new LayoutValueChoice<double>(slotWidth switch
            {
                0.75 => "3/4 in",
                1.5 => "1-1/2 in",
                _ => "1 in"
            }, slotWidth));
        foreach (var slotCount in GrdPerformanceCatalog.TitusSlotCounts)
            _grdSlotCount.Items.Add(new LayoutValueChoice<double>($"{slotCount} slot{(slotCount == 1 ? "" : "s")}", slotCount));
        _grdFaceSize.SelectedItem = GrdPerformanceCatalog.PriceFaceSizes
            .OrderBy(value => Math.Abs(value - node.GrdFaceSizeIn)).First();
        _grdTitus350RSize.SelectedItem = GrdPerformanceCatalog.Titus350RSizes.FirstOrDefault(value =>
            string.Equals(value, node.GrdNominalDuctSizeIn, StringComparison.OrdinalIgnoreCase)) ?? "12x12";
        _grdLength.SelectedItem = GrdPerformanceCatalog.TitusLengthsFt
            .OrderBy(value => Math.Abs(value - node.GrdLengthFt)).First();
        _grdSlotWidth.SelectedItem = _grdSlotWidth.Items.Cast<LayoutValueChoice<double>>()
            .OrderBy(value => Math.Abs(value.Value - node.GrdSlotWidthIn)).First();
        _grdSlotCount.SelectedItem = _grdSlotCount.Items.Cast<LayoutValueChoice<double>>()
            .OrderBy(value => Math.Abs(value.Value - node.GrdSlotCount)).First();
        PopulateGrdNeckChoices(node.GrdNeckDiameterIn);
        _grdPerformanceResult.AutoSize = true;
        _grdPerformanceResult.MaximumSize = new Size(260, 0);
        _grdPerformanceResult.ForeColor = Color.FromArgb(28, 69, 108);
        _grdPerformanceResult.Font = new Font("Segoe UI Semibold", 8F);
        _pressureDrop.ReadOnly = IsPublishedGrd();
        if (_pressureDrop.ReadOnly)
            _pressureDrop.BackColor = Color.FromArgb(235, 241, 246);
        _grdType.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdTitus350RSize.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdFaceSize.SelectedIndexChanged += (_, _) =>
        {
            PopulateGrdNeckChoices(node.GrdNeckDiameterIn);
            UpdateGrdPerformancePreview();
        };
        _grdNeckDiameter.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdLength.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdSlotWidth.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdSlotCount.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _airflow.TextChanged += (_, _) => UpdateGrdPerformancePreview();
        UpdateGrdPerformancePreview();
    }

    private Control BuildGrdPerformancePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        Add("350R Duct Size (in)", _grdTitus350RSize, "titus350RSize", 0);
        Add("Face Size (in)", _grdFaceSize, "face", 1);
        Add("Neck Diameter (in)", _grdNeckDiameter, "neck", 2);
        Add("Length (ft)", _grdLength, "length", 3);
        Add("Slot Size", _grdSlotWidth, "slotWidth", 4);
        Add("Number of Slots", _grdSlotCount, "slotCount", 5);
        Add("Selected Result", _grdPerformanceResult, "result", 6);
        UpdateRows();
        _grdType.SelectedIndexChanged += (_, _) => UpdateRows();
        return panel;

        void Add(string text, Control control, string key, int row)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 6 ? 46 : 30));
            var label = new Label
            {
                Name = $"LayoutGrd_{key}_Label",
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(48, 65, 80),
                Font = new Font("Segoe UI", 8F)
            };
            control.Name = string.IsNullOrWhiteSpace(control.Name) ? $"LayoutGrd_{key}_Input" : control.Name;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(2, 2, 0, 2);
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        void UpdateRows()
        {
            var price = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Diffuser",
                StringComparison.OrdinalIgnoreCase);
            var grille = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Grille",
                StringComparison.OrdinalIgnoreCase);
            var titus = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Linear Diffuser TBD-80 D",
                StringComparison.OrdinalIgnoreCase);
            SetRow(0, grille);
            SetRow(1, price);
            SetRow(2, price);
            SetRow(3, titus);
            SetRow(4, titus);
            SetRow(5, titus);
            SetRow(6, grille || price || titus);
        }

        void SetRow(int row, bool visible)
        {
            panel.RowStyles[row].Height = visible ? (row == 6 ? 46 : 30) : 0;
            foreach (Control control in panel.Controls)
                if (panel.GetRow(control) == row) control.Visible = visible;
        }
    }

    private bool IsPublishedGrd()
    {
        var type = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture);
        return string.Equals(type, "Grille", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "Diffuser", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateGrdNeckChoices(double preferred)
    {
        if (_grdFaceSize.SelectedItem is not int face)
            return;
        var values = GrdPerformanceCatalog.PriceNeckDiameters(face);
        _grdNeckDiameter.BeginUpdate();
        _grdNeckDiameter.Items.Clear();
        _grdNeckDiameter.Items.AddRange(values.Cast<object>().ToArray());
        _grdNeckDiameter.EndUpdate();
        _grdNeckDiameter.SelectedItem = values.OrderBy(value => Math.Abs(value - preferred)).First();
    }

    private void UpdateGrdPerformancePreview()
    {
        if (_node.Type != LayoutNodeType.Grd)
            return;
        var published = IsPublishedGrd();
        _pressureDrop.ReadOnly = published;
        _pressureDrop.BackColor = published ? Color.FromArgb(235, 241, 246) : Color.White;
        if (!published)
        {
            _grdPerformanceResult.Text = string.Empty;
            return;
        }
        if (!double.TryParse(_airflow.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm) || cfm <= 0)
        {
            _grdPerformanceResult.Text = "Enter a positive airflow.";
            return;
        }
        try
        {
            var type = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture);
            var selection = string.Equals(type, "Grille", StringComparison.OrdinalIgnoreCase)
                ? GrdPerformanceCatalog.SelectTitus350R(cfm, Convert.ToString(_grdTitus350RSize.SelectedItem,
                    CultureInfo.CurrentCulture) ?? "12x12")
                : string.Equals(type, "Diffuser", StringComparison.OrdinalIgnoreCase)
                    ? GrdPerformanceCatalog.SelectPriceScd(cfm, (int)_grdFaceSize.SelectedItem!, (int)_grdNeckDiameter.SelectedItem!)
                    : GrdPerformanceCatalog.SelectTitusTbd80(cfm, (int)_grdLength.SelectedItem!,
                        ((LayoutValueChoice<double>)_grdSlotWidth.SelectedItem!).Value,
                        Convert.ToInt32(((LayoutValueChoice<double>)_grdSlotCount.SelectedItem!).Value));
            _pressureDrop.Text = selection.Selected.TotalPressureInWg.ToString("0.###", CultureInfo.CurrentCulture);
            _grdPerformanceResult.Text = string.IsNullOrWhiteSpace(selection.NominalDuctSizeIn)
                ? $"{selection.Selected.AirflowCfm:0} cfm | {selection.InletDiameterIn:0} in inlet | NC {selection.Selected.SoundNc} | Throw {selection.Selected.ThrowFt} ft"
                : $"{selection.Selected.AirflowCfm:0} cfm | {selection.Selected.CoreVelocityFpm:0} fpm | SP -{selection.Selected.TotalPressureInWg:0.###} | NC {selection.Selected.SoundNc}";
        }
        catch (ArgumentException ex)
        {
            _grdPerformanceResult.Text = ex.Message;
        }
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input, int rowHeight = 40)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 0 ? 48 : rowHeight));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }

    private void SaveInputs()
    {
        var temperatureF = 70.0;
        var sourceSpace = _node.Type == LayoutNodeType.Reservoir
                          && (string.Equals(_node.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(_node.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase));
        var sinkSpace = _node.Type == LayoutNodeType.Reservoir && !sourceSpace;
        if (_node.Type is not (LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent)
            && !sinkSpace
            && (!double.TryParse(_temperature.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out temperatureF)
                || temperatureF < -100 || temperatureF > 300))
        {
            MessageBox.Show(this, "Enter a temperature between -100°F and 300°F.", "Invalid Temperature",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_node.Type is not (LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent) && !sinkSpace)
            _node.TemperatureC = UnitConversions.FahrenheitToCelsius(temperatureF);
        if (_node.Type is LayoutNodeType.Fan or LayoutNodeType.Grd || sourceSpace || sinkSpace)
        {
            if (!double.TryParse(_airflow.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm) || cfm <= 0)
            {
                MessageBox.Show(this, "Enter a positive airflow in cfm.", "Invalid Airflow",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _node.AirflowM3PerS = UnitConversions.CfmToM3PerS(cfm);
            _node.AirflowUserSpecified = true;
        }
        if (_node.Type == LayoutNodeType.Fan)
        {
            if (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                    out var fanStaticPressure) || !double.IsFinite(fanStaticPressure) || fanStaticPressure < 0)
            {
                MessageBox.Show(this, "Enter a nonnegative fan system static pressure in in. w.c.",
                    "Invalid Fan Static Pressure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _node.FanStaticPressurePa = UnitConversions.InchesWaterToPa(fanStaticPressure);
            if (!double.TryParse(_fanEfficiency.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                    out var fanEfficiency) || !double.IsFinite(fanEfficiency)
                || fanEfficiency <= 0.0 || fanEfficiency > 100.0)
            {
                MessageBox.Show(this, "Enter a fan efficiency greater than 0% and no more than 100%.",
                    "Invalid Fan Efficiency", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _node.FanEfficiencyPercent = fanEfficiency;
        }
        else
        {
            var drop = 0.0;
            if (sourceSpace)
            {
                if (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var sourcePressure)
                    || !double.IsFinite(sourcePressure))
                {
                    MessageBox.Show(this, "Enter a valid source pressure in in. w.g.", "Invalid Source Pressure",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _node.PressurePa = UnitConversions.InchesWaterToPa(sourcePressure);
            }
            else if (_node.Type != LayoutNodeType.Reservoir && _node.Type != LayoutNodeType.CalculationPoint
                && (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out drop) || drop < 0))
            {
                MessageBox.Show(this, "Enter a nonnegative pressure drop in in. w.g.", "Invalid Pressure Drop",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!sourceSpace && _node.Type != LayoutNodeType.CalculationPoint)
                _node.PressureDropPa = UnitConversions.InchesWaterToPa(drop);
        }
        if (!_absoluteLocation)
        {
            if (!TryReadDelta(_deltaX, "ΔX", out var deltaX)
                || !TryReadDelta(_deltaY, "ΔY", out var deltaY)
                || !TryReadDelta(_deltaZ, "ΔZ", out var deltaZ))
                return;
            _node.DeltaXM = UnitConversions.FeetToM(deltaX);
            _node.DeltaYM = UnitConversions.FeetToM(deltaY);
            _node.DeltaZM = UnitConversions.FeetToM(deltaZ);
        }
        else
        {
            if (!TryReadDelta(_deltaX, "absolute X", out var absoluteX)
                || !TryReadDelta(_deltaY, "absolute Y", out var absoluteY)
                || !TryReadDelta(_deltaZ, "absolute Z", out var absoluteZ))
                return;
            _desiredAbsoluteCoordinate = new LayoutCoordinate(UnitConversions.FeetToM(absoluteX),
                UnitConversions.FeetToM(absoluteY), UnitConversions.FeetToM(absoluteZ));
        }
        if (_node.Type == LayoutNodeType.Grd)
        {
            _node.GrdType = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture) ?? "Grille";
            _node.GrdNominalDuctSizeIn = Convert.ToString(_grdTitus350RSize.SelectedItem,
                CultureInfo.CurrentCulture) ?? "12x12";
            _node.GrdFaceSizeIn = _grdFaceSize.SelectedItem is int face ? face : 24;
            _node.GrdNeckDiameterIn = _grdNeckDiameter.SelectedItem is int neck ? neck : 8;
            _node.GrdLengthFt = _grdLength.SelectedItem is int length ? length : 2;
            _node.GrdSlotWidthIn = _grdSlotWidth.SelectedItem is LayoutValueChoice<double> slotWidth ? slotWidth.Value : 0.75;
            _node.GrdSlotCount = _grdSlotCount.SelectedItem is LayoutValueChoice<double> slotCount
                ? Convert.ToInt32(slotCount.Value) : 1;
            if (IsPublishedGrd())
            {
                var cfm = UnitConversions.M3PerSToCfm(_node.AirflowM3PerS);
                try
                {
                    var selection = string.Equals(_node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase)
                        ? GrdPerformanceCatalog.SelectTitus350R(cfm, _node.GrdNominalDuctSizeIn)
                        : string.Equals(_node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase)
                            ? GrdPerformanceCatalog.SelectPriceScd(cfm, (int)_node.GrdFaceSizeIn, (int)_node.GrdNeckDiameterIn)
                            : GrdPerformanceCatalog.SelectTitusTbd80(cfm, (int)_node.GrdLengthFt,
                                _node.GrdSlotWidthIn, _node.GrdSlotCount);
                    _node.PressureDropPa = UnitConversions.InchesWaterToPa(selection.Selected.TotalPressureInWg);
                    _node.GrdInletDiameterIn = selection.InletDiameterIn ?? 6;
                    _node.GrdSelectedAirflowCfm = selection.Selected.AirflowCfm;
                    _node.GrdSoundNc = selection.Selected.SoundNc;
                    _node.GrdThrowFt = selection.Selected.ThrowFt;
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(this, ex.Message, "GRD Performance Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                _node.GrdSelectedAirflowCfm = 0;
                _node.GrdSoundNc = string.Empty;
                _node.GrdThrowFt = string.Empty;
            }
        }
        if (_node.Type == LayoutNodeType.CustomComponent)
        {
            _node.Description = _componentDescription.Text.Trim();
            if (string.IsNullOrWhiteSpace(_node.Description))
            {
                MessageBox.Show(this, "Enter a component description.", "Missing Description",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        if (_node.Type == LayoutNodeType.Reservoir)
            _node.SpaceRole = Convert.ToString(_spaceRole.SelectedItem, CultureInfo.CurrentCulture) ?? "Space Sink";
        _node.AirSystemType = SelectedAirSystemType;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadDelta(TextBox input, string name, out double value)
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value) && Math.Abs(value) <= 100_000)
            return true;
        MessageBox.Show(this, $"Enter a finite {name} distance between -100,000 ft and 100,000 ft.",
            "Invalid Coordinate Delta", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    internal static void PopulateRelationshipControls(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, ComboBox upstream, Label downstream)
    {
        upstream.DropDownStyle = ComboBoxStyle.DropDownList;
        upstream.Items.Add(new LayoutNodeChoice(string.Empty, "(No upstream item)"));
        var descendants = DescendantIds(node, nodes);
        foreach (var candidate in nodes.Where(x => x.Id != node.Id && !descendants.Contains(x.Id)).OrderBy(x => x.Sequence))
        {
            upstream.Items.Add(new LayoutNodeChoice(candidate.Id, candidate.Id));
        }
        upstream.SelectedItem = upstream.Items.Cast<LayoutNodeChoice>().FirstOrDefault(x =>
            string.Equals(x.Id, node.ParentNodeId, StringComparison.OrdinalIgnoreCase)) ?? upstream.Items[0];

        var children = nodes.Where(x => x.ParentNodeId == node.Id || x.SecondaryParentNodeId == node.Id)
            .OrderBy(x => x.Sequence).ToArray();
        downstream.Text = children.Length == 0
            ? "(No downstream items)"
            : string.Join(", ", children.Select(x => x.Id));
        downstream.Dock = DockStyle.Fill;
        downstream.AutoEllipsis = true;
        downstream.TextAlign = ContentAlignment.MiddleLeft;
        downstream.ForeColor = Color.FromArgb(48, 65, 80);
    }

    private static HashSet<string> DescendantIds(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(node.Id);
        while (pending.Count > 0)
        {
            var parentId = pending.Dequeue();
            foreach (var child in nodes.Where(x => x.ParentNodeId == parentId || x.SecondaryParentNodeId == parentId))
            {
                if (result.Add(child.Id))
                    pending.Enqueue(child.Id);
            }
        }
        return result;
    }
}

internal sealed record LayoutNodeChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

internal sealed record LayoutValueChoice<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

internal static class LayoutDialogControls
{
    internal static Control CreateInputWithUnit(TextBox input, string unit)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        input.Dock = DockStyle.Fill;
        panel.Controls.Add(input, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = unit,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0),
            ForeColor = Color.FromArgb(48, 65, 80)
        }, 1, 0);
        return panel;
    }

    internal static Control CreateLocationEditor(TextBox x, TextBox y, TextBox z)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        foreach (var _ in new[] { "X", "Y", "Z" })
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        }
        AddAxis("X", x, 0);
        AddAxis("Y", y, 2);
        AddAxis("Z", z, 4);
        return row;

        void AddAxis(string label, TextBox input, int column)
        {
            row.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(48, 65, 80),
                Font = new Font("Segoe UI Semibold", 8F)
            }, column, 0);
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(1, 5, 4, 5);
            row.Controls.Add(input, column + 1, 0);
        }
    }
}

internal sealed class LayoutComponentPlacementDialog : Form
{
    private readonly SavedLayoutNode _node;
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();

    private readonly UnitSystem _units;
    private readonly double _gridIncrement;

    internal LayoutComponentPlacementDialog(SavedLayoutNode node)
        : this(node, string.Empty, UnitSystem.Imperial)
    {
    }

    internal LayoutComponentPlacementDialog(SavedLayoutNode node, string connectedItemId = "", UnitSystem units = UnitSystem.Imperial,
        string? editableAxis = null)
        : this(node, connectedItemId, units, editableAxis, 0.0)
    {
    }

    internal LayoutComponentPlacementDialog(SavedLayoutNode node, string connectedItemId, UnitSystem units,
        string? editableAxis, double gridIncrement)
    {
        _node = node;
        _units = units;
        _gridIncrement = gridIncrement;
        Text = "Confirm Relative Coordinates";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, gridIncrement > 0 ? 214 : 180);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _deltaX.Name = "LayoutPlacementDeltaXText";
        _deltaY.Name = "LayoutPlacementDeltaYText";
        _deltaZ.Name = "LayoutPlacementDeltaZText";
        _deltaX.Text = Display(node.DeltaXM).ToString("0.#####", CultureInfo.CurrentCulture);
        _deltaY.Text = Display(node.DeltaYM).ToString("0.#####", CultureInfo.CurrentCulture);
        _deltaZ.Text = Display(node.DeltaZM).ToString("0.#####", CultureInfo.CurrentCulture);
        if (!string.IsNullOrWhiteSpace(editableAxis))
        {
            ConfigureAxis(_deltaX, "X");
            ConfigureAxis(_deltaY, "Y");
            ConfigureAxis(_deltaZ, "Z");
        }
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = gridIncrement > 0 ? 4 : 3,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Component", new Label
        {
            Text = string.IsNullOrWhiteSpace(connectedItemId)
                ? node.Id
                : string.IsNullOrWhiteSpace(editableAxis)
                    ? $"{node.Id} relative to {connectedItemId} (signed values)"
                    : $"{node.Id} on {connectedItemId} duct — adjust {editableAxis} only",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F)
        });
        AddRow(table, 1, units == UnitSystem.Imperial ? "RELATIVE Δ (ft)" : "RELATIVE Δ (m)",
            LayoutDialogControls.CreateLocationEditor(_deltaX, _deltaY, _deltaZ));
        var buttonRow = 2;
        if (gridIncrement > 0)
        {
            var unit = units == UnitSystem.Imperial ? "ft" : "m";
            var gridNote = new Label
            {
                Text = $"Coordinates must follow the designer grid in {gridIncrement:0.#####} {unit} increments.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(74, 92, 106),
                Font = new Font("Segoe UI", 8.5F)
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.Controls.Add(gridNote, 0, 2);
            table.SetColumnSpan(gridNote, 2);
            buttonRow = 3;
        }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Confirm Coordinates", Width = 152, Height = 32, BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        save.Click += (_, _) => SaveInputs();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, buttonRow);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
        return;

        void ConfigureAxis(TextBox input, string axis)
        {
            input.ReadOnly = !string.Equals(editableAxis, axis, StringComparison.OrdinalIgnoreCase);
            if (input.ReadOnly)
                input.BackColor = Color.FromArgb(232, 237, 241);
        }
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }

    private void SaveInputs()
    {
        if (!TryNumber(_deltaX.Text, out var x) || !TryNumber(_deltaY.Text, out var y)
            || !TryNumber(_deltaZ.Text, out var z))
        {
            MessageBox.Show(this, "Enter finite ΔX, ΔY, and ΔZ distances.", "Invalid Coordinate Delta",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_gridIncrement > 0
            && (!IsGridAligned(x, _gridIncrement) || !IsGridAligned(y, _gridIncrement)
                || !IsGridAligned(z, _gridIncrement)))
        {
            var unit = _units == UnitSystem.Imperial ? "ft" : "m";
            MessageBox.Show(this,
                $"Enter ΔX, ΔY, and ΔZ in {_gridIncrement:0.#####} {unit} grid increments. Negative values are allowed.",
                "Coordinates Must Match Grid", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _node.DeltaXM = Stored(x);
        _node.DeltaYM = Stored(y);
        _node.DeltaZM = Stored(z);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000;

    internal static bool IsGridAligned(double value, double increment)
    {
        if (!double.IsFinite(value) || !double.IsFinite(increment) || increment <= 0)
            return false;
        var cells = value / increment;
        return Math.Abs(cells - Math.Round(cells)) <= 1e-5;
    }

    private double Display(double meters) => _units == UnitSystem.Imperial ? UnitConversions.MToFeet(meters) : meters;
    private double Stored(double displayed) => _units == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
}

internal sealed class LayoutCoordinateOriginDialog : Form
{
    private readonly UnitSystem _units;
    private readonly TextBox _x = new();
    private readonly TextBox _y = new();
    private readonly TextBox _z = new();
    internal LayoutCoordinate Coordinate { get; private set; }

    internal LayoutCoordinateOriginDialog(string itemId, LayoutCoordinate current, UnitSystem units)
    {
        _units = units;
        Text = "Change Coordinate Origin";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 188);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);
        _x.Text = Display(current.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _y.Text = Display(current.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _z.Text = Display(current.ZM).ToString("0.##", CultureInfo.CurrentCulture);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
            Padding = new Padding(18), BackColor = BackColor };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Coordinate Reference", new Label { Text = itemId, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F) });
        AddRow(table, 1, units == UnitSystem.Imperial ? "ABSOLUTE X / Y / Z (ft)" : "ABSOLUTE X / Y / Z (m)",
            LayoutDialogControls.CreateLocationEditor(_x, _y, _z));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var change = new Button { Text = "Change Coordinate", Width = 142, Height = 32,
            BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(change, 7);
        UiPolish.RoundButton(cancel, 7);
        change.Click += (_, _) => Save();
        buttons.Controls.Add(change);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, 2);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = change;
        CancelButton = cancel;
    }

    private void Save()
    {
        if (!Try(_x.Text, out var x) || !Try(_y.Text, out var y) || !Try(_z.Text, out var z))
        {
            MessageBox.Show(this, "Enter finite X, Y, and Z coordinates.", "Invalid Coordinate",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Coordinate = new LayoutCoordinate(Stored(x), Stored(y), Stored(z));
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80), Font = new Font("Segoe UI Semibold", 9F) }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }

    private static bool Try(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000;
    private double Display(double meters) => _units == UnitSystem.Imperial ? UnitConversions.MToFeet(meters) : meters;
    private double Stored(double displayed) => _units == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
}

internal sealed class LayoutBranchChoiceDialog : Form
{
    private readonly RadioButton _mainPath = new();
    private readonly RadioButton _newBranch = new();
    internal bool NewBranchSelected => _newBranch.Checked;

    internal LayoutBranchChoiceDialog(string parentId, string? nodeId)
    {
        Text = "Select Connection Path";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 220);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var prompt = string.IsNullOrWhiteSpace(nodeId)
            ? $"Choose how the new component connects to diverting fitting {parentId}."
            : $"Choose how {nodeId} connects to diverting fitting {parentId}.";
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = prompt + " The connector side is inferred from where the component is placed.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(48, 65, 80),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _mainPath.Name = "LayoutMainPathRadio";
        _mainPath.Text = "Main path (same branch)";
        _mainPath.Checked = true;
        _mainPath.Dock = DockStyle.Fill;
        _newBranch.Name = "LayoutNewBranchRadio";
        _newBranch.Text = "Branch path (new branch)";
        _newBranch.Dock = DockStyle.Fill;
        layout.Controls.Add(_mainPath, 0, 1);
        layout.Controls.Add(_newBranch, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var connect = new Button
        {
            Name = "LayoutBranchConnectButton",
            Text = "Connect",
            DialogResult = DialogResult.OK,
            Size = new Size(110, 32),
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 32), FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(connect, 7);
        UiPolish.RoundButton(cancel, 7);
        buttons.Controls.Add(connect);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = connect;
        CancelButton = cancel;
    }
}

internal sealed class LayoutFittingNodeDialog : Form
{
    private readonly ComboBox _upstream = new();
    private readonly ComboBox _airSystem = new();
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();
    internal string SelectedParentNodeId => (_upstream.SelectedItem as LayoutNodeChoice)?.Id ?? string.Empty;
    internal string SelectedAirSystemType => Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? "Supply Air";
    internal double DeltaXM => _absoluteLocation
        ? _node.DeltaXM : UnitConversions.FeetToM(ReadDelta(_deltaX, _node.DeltaXM));
    internal double DeltaYM => _absoluteLocation
        ? _node.DeltaYM : UnitConversions.FeetToM(ReadDelta(_deltaY, _node.DeltaYM));
    internal double DeltaZM => _absoluteLocation
        ? _node.DeltaZM : UnitConversions.FeetToM(ReadDelta(_deltaZ, _node.DeltaZM));
    private readonly SavedLayoutNode _node;
    private readonly bool _absoluteLocation;
    private LayoutCoordinate _desiredAbsoluteCoordinate;
    internal LayoutCoordinate DesiredAbsoluteCoordinate => _desiredAbsoluteCoordinate;

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting)
        : this(node, nodes, captionSource, fitting, null, null)
    {
    }

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting,
        LayoutCoordinate? absoluteCoordinate)
        : this(node, nodes, captionSource, fitting, absoluteCoordinate, null)
    {
    }

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting,
        LayoutCoordinate? absoluteCoordinate, LayoutEditableAxes? editableAxes)
    {
        _node = node;
        _absoluteLocation = absoluteCoordinate.HasValue;
        Text = "Fitting Layout Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(580, 420);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var downstream = new Label();
        LayoutEquipmentDialog.PopulateRelationshipControls(node, nodes, captionSource, _upstream, downstream);
        _upstream.Name = "LayoutUpstreamItemCombo";
        _airSystem.Name = "LayoutFittingAirSystemCombo";
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = MainForm.AirSystemTypes.FirstOrDefault(x =>
            string.Equals(x, node.AirSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";
        var location = absoluteCoordinate ?? new LayoutCoordinate(node.DeltaXM, node.DeltaYM, node.DeltaZM);
        _desiredAbsoluteCoordinate = location;
        _deltaX.Text = UnitConversions.MToFeet(location.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaY.Text = UnitConversions.MToFeet(location.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaZ.Text = UnitConversions.MToFeet(location.ZM).ToString("0.##", CultureInfo.CurrentCulture);
        var coordinateInputs = new[]
        {
            (Input: _deltaX, Editable: editableAxes?.X ?? true),
            (Input: _deltaY, Editable: editableAxes?.Y ?? true),
            (Input: _deltaZ, Editable: editableAxes?.Z ?? true)
        };
        foreach (var coordinateInput in coordinateInputs)
        {
            coordinateInput.Input.ReadOnly = _absoluteLocation && !coordinateInput.Editable;
            if (coordinateInput.Input.ReadOnly)
                coordinateInput.Input.BackColor = Color.FromArgb(235, 241, 246);
        }
        downstream.Name = "LayoutDownstreamItemsLabel";
        var fittingText = fitting is null
            ? "Fitting has not been defined yet."
            : $"{fitting.Id} — {fitting.FittingCode} — {FittingCatalog.Find(fitting.FittingCode).Description}";

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDialogRow(table, 0, "Selected Fitting", new Label
        {
            Text = fittingText,
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            Padding = new Padding(0, 5, 0, 5)
        }, 112);
        var row = 1;
        AddDialogRow(table, row++, "Air System Type", _airSystem, 42);
        AddDialogRow(table, row++, "Upstream Item", _upstream, 42);
        AddDialogRow(table, row++, "Downstream Item(s)", downstream, 42);
        AddDialogRow(table, row++, _absoluteLocation ? "ABSOLUTE X / Y / Z (ft)" : "RELATIVE DELTA (ft)",
            LayoutDialogControls.CreateLocationEditor(_deltaX, _deltaY, _deltaZ), 42);

        var note = new Label
        {
            Text = "Changing the upstream item rewires the layout. New connection ducts are red until sized.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoEllipsis = false,
            UseMnemonic = false,
            Padding = new Padding(0, 4, 0, 4),
            ForeColor = Color.FromArgb(92, 76, 42),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        table.Controls.Add(note, 0, row);
        table.SetColumnSpan(note, 2);
        row++;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var open = new Button
        {
            Name = "LayoutFittingEditButton",
            Text = fitting is null ? "Define Fitting" : "Edit",
            DialogResult = DialogResult.None,
            Width = 135,
            Height = 32,
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(open, 7);
        UiPolish.RoundButton(cancel, 7);
        open.Click += (_, _) =>
        {
            if (_absoluteLocation)
            {
                if (!TryReadLocation(out _desiredAbsoluteCoordinate))
                    return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(open);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, row);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = open;
        CancelButton = cancel;
    }

    private static double ReadDelta(TextBox input, double fallbackMeters) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000
            ? value
            : UnitConversions.MToFeet(fallbackMeters);

    private bool TryReadLocation(out LayoutCoordinate coordinate)
    {
        coordinate = _desiredAbsoluteCoordinate;
        if (!TryRead(_deltaX, "absolute X", out var x)
            || !TryRead(_deltaY, "absolute Y", out var y)
            || !TryRead(_deltaZ, "absolute Z", out var z))
            return false;
        coordinate = new LayoutCoordinate(UnitConversions.FeetToM(x), UnitConversions.FeetToM(y),
            UnitConversions.FeetToM(z));
        return true;
    }

    private bool TryRead(TextBox input, string name, out double value)
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value) && Math.Abs(value) <= 100_000)
            return true;
        MessageBox.Show(this, $"Enter a finite {name} coordinate between -100,000 ft and 100,000 ft.",
            "Invalid Coordinate", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private static void AddDialogRow(TableLayoutPanel table, int row, string label, Control input, int height)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }
}

internal sealed class LayoutFittingEditDialog : Form
{
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
        var definitionHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
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
        UiPolish.RoundButton(browseGallery, 6);
        browseGallery.Click += (_, _) =>
        {
            using var gallery = new FittingGalleryDialog(FittingCatalog.All, SelectedDefinition.Code);
            if (gallery.ShowDialog(this) == DialogResult.OK && gallery.SelectedDefinition is not null)
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
        _result.Text = "Inputs changed — click Calculate before updating the fitting.";
        _result.ForeColor = Color.FromArgb(92, 76, 42);
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

internal sealed class LayoutDuctConnectionDialog : Form
{
    private readonly ComboBox _airSystem = new();
    internal string SelectedAirSystemType => Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? "Supply Air";
    internal bool ReverseRequested { get; private set; }

    internal LayoutDuctConnectionDialog(string upstreamId, string downstreamId, string airSystemType,
        bool hasSavedDuct = false)
    {
        Text = "Duct Connection Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 190);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);
        _airSystem.Name = "LayoutDuctAirSystemCombo";
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = MainForm.AirSystemTypes.FirstOrDefault(x =>
            string.Equals(x, airSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18), BackColor = BackColor };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Connection", new Label { Text = $"{upstreamId}  →  {downstreamId}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(28, 69, 108), Font = new Font("Segoe UI Semibold", 9F) });
        AddRow(table, 1, "Air System Type", _airSystem);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var open = new Button
        {
            Name = "LayoutDuctEditButton",
            Text = hasSavedDuct ? "Edit" : "Size Duct",
            DialogResult = DialogResult.OK,
            Width = 125,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        var reverse = new Button
        {
            Name = "LayoutDuctReverseButton",
            Text = "Reverse Flow Direction",
            Width = 165,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White
        };
        reverse.Click += (_, _) =>
        {
            ReverseRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        UiPolish.RoundButton(open, 7);
        UiPolish.RoundButton(reverse, 7);
        UiPolish.RoundButton(cancel, 7);
        buttons.Controls.Add(open);
        buttons.Controls.Add(reverse);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, 3);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = open;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(48, 65, 80) }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }
}

internal sealed class LayoutDuctEditDialog : Form
{
    private readonly UnitSystem _units;
    private readonly SavedDuct _original;
    private readonly double _sizingIncrementM;
    private readonly SizeRoundingDirection _roundingDirection;
    private readonly ComboBox _mode = new();
    private readonly TextBox _airflow = new();
    private readonly TextBox _temperature = new();
    private readonly TextBox _length = new();
    private readonly ComboBox _shape = new();
    private readonly TextBox _diameter = new();
    private readonly TextBox _width = new();
    private readonly TextBox _height = new();
    private readonly TextBox _aspectRatio = new();
    private readonly TextBox _targetFriction = new();
    private readonly TextBox _maximumVelocity = new();
    private readonly ComboBox _material = new();
    private readonly Label _result = new();
    private readonly Button _updateButton = new();
    internal SavedDuct? Result { get; private set; }

    internal LayoutDuctEditDialog(SavedDuct duct, UnitSystem units, double sizingIncrementM,
        SizeRoundingDirection roundingDirection)
    {
        _original = duct;
        _units = units;
        _sizingIncrementM = sizingIncrementM > 0 ? sizingIncrementM
            : units == UnitSystem.Imperial ? UnitConversions.InchesToM(0.5) : UnitConversions.MillimetersToM(10);
        _roundingDirection = roundingDirection;
        Text = duct.Sequence > 0 ? $"Edit {duct.Id}" : "Size Duct Connection";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(650, 650);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _mode.Name = "LayoutDuctSizingMethodCombo";
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[] { "Check Existing", "Autosize (SP Loss)", "Autosize (Velocity)" });
        _mode.SelectedIndex = 1;
        _mode.SelectedIndexChanged += (_, _) => { UpdateModeInputs(); InvalidateCalculation(); };
        _shape.DropDownStyle = ComboBoxStyle.DropDownList;
        _shape.Items.AddRange(new object[] { DuctShape.Round, DuctShape.Rectangular });
        _shape.SelectedItem = duct.Shape;
        _shape.SelectedIndexChanged += (_, _) => { UpdateModeInputs(); InvalidateCalculation(); };
        _material.DropDownStyle = ComboBoxStyle.DropDownList;
        _material.Items.AddRange(DuctMaterialCatalog.All.Cast<object>().ToArray());
        _material.SelectedItem = DuctMaterialCatalog.All.FirstOrDefault(x =>
            string.Equals(x.Name, duct.MaterialName, StringComparison.OrdinalIgnoreCase))
            ?? DuctMaterialCatalog.All.First(x => x.Name == "Galvanized Spiral Duct");

        if (units == UnitSystem.Imperial)
        {
            _airflow.Text = UnitConversions.M3PerSToCfm(duct.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = UnitConversions.CelsiusToFahrenheit(duct.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture);
            _length.Text = UnitConversions.MToFeet(duct.LengthM).ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToInches(duct.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToInches(duct.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToInches(duct.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }

        else
        {
            _airflow.Text = UnitConversions.M3PerSToLps(duct.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture);
            _temperature.Text = duct.TemperatureC.ToString("0.##", CultureInfo.CurrentCulture);
            _length.Text = duct.LengthM.ToString("0.##", CultureInfo.CurrentCulture);
            _diameter.Text = UnitConversions.MToMillimeters(duct.DiameterM).ToString("0.##", CultureInfo.CurrentCulture);
            _width.Text = UnitConversions.MToMillimeters(duct.WidthM).ToString("0.##", CultureInfo.CurrentCulture);
            _height.Text = UnitConversions.MToMillimeters(duct.HeightM).ToString("0.##", CultureInfo.CurrentCulture);
        }
        _aspectRatio.Text = duct.Shape == DuctShape.Rectangular && duct.HeightM > 0
            ? (duct.WidthM / duct.HeightM).ToString("0.###", CultureInfo.CurrentCulture)
            : "1.5";
        _targetFriction.Text = units == UnitSystem.Imperial ? "0.08" : "0.65";
        _maximumVelocity.Text = units == UnitSystem.Imperial ? "1000" : "5";

        var flowUnit = units == UnitSystem.Imperial ? "cfm" : "L/s";
        var temperatureUnit = units == UnitSystem.Imperial ? "°F" : "°C";
        var lengthUnit = units == UnitSystem.Imperial ? "ft" : "m";
        var sizeUnit = units == UnitSystem.Imperial ? "in" : "mm";
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 14,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Calculation Method", _mode);
        AddRow(table, 1, $"Airflow ({flowUnit})", _airflow);
        AddRow(table, 2, $"Air Temperature ({temperatureUnit})", _temperature);
        AddRow(table, 3, $"Duct Length ({lengthUnit})", _length);
        AddRow(table, 4, "Duct Shape", _shape);
        AddRow(table, 5, $"Diameter ({sizeUnit})", _diameter);
        AddRow(table, 6, $"Width ({sizeUnit})", _width);
        AddRow(table, 7, $"Height ({sizeUnit})", _height);
        AddRow(table, 8, "Width / Height Ratio", _aspectRatio);
        AddRow(table, 9, units == UnitSystem.Imperial ? "Target SP (in. w.g./100 ft)" : "Target SP (Pa/m)", _targetFriction);
        AddRow(table, 10, units == UnitSystem.Imperial ? "Maximum Velocity (fpm)" : "Maximum Velocity (m/s)", _maximumVelocity);
        AddRow(table, 11, "Duct Material", _material);
        _result.Dock = DockStyle.Fill;
        _result.ForeColor = Color.FromArgb(28, 69, 108);
        _result.Font = new Font("Segoe UI Semibold", 9F);
        _result.TextAlign = ContentAlignment.MiddleLeft;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        table.Controls.Add(_result, 0, 12);
        table.SetColumnSpan(_result, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _updateButton.Name = "LayoutDuctDirectUpdateButton";
        _updateButton.Text = "Update Duct";
        _updateButton.Width = 120;
        _updateButton.Height = 32;
        _updateButton.BackColor = Color.FromArgb(42, 135, 91);
        _updateButton.ForeColor = Color.White;
        _updateButton.FlatStyle = FlatStyle.Flat;
        _updateButton.Enabled = false;
        var calculate = new Button
        {
            Name = "LayoutDuctCalculateButton",
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
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, 13);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = calculate;
        CancelButton = cancel;
        UpdateModeInputs();
        InvalidateCalculation();
        foreach (var input in new[] { _airflow, _temperature, _length, _diameter, _width, _height,
                     _aspectRatio, _targetFriction, _maximumVelocity })
            input.TextChanged += (_, _) => InvalidateCalculation();
        _material.SelectedIndexChanged += (_, _) => InvalidateCalculation();
    }

    private CalculationMode SelectedMode => _mode.SelectedIndex switch
    {
        1 => CalculationMode.SizeByTargetFriction,
        2 => CalculationMode.SizeByMaximumVelocity,
        _ => CalculationMode.CheckEnteredSize
    };

    private void UpdateModeInputs()
    {
        var round = _shape.SelectedItem is DuctShape.Round;
        var checking = SelectedMode == CalculationMode.CheckEnteredSize;
        _diameter.Enabled = checking && round;
        _width.Enabled = checking && !round;
        _height.Enabled = checking && !round;
        _aspectRatio.Enabled = !checking && !round;
        _targetFriction.Enabled = SelectedMode == CalculationMode.SizeByTargetFriction;
        _maximumVelocity.Enabled = SelectedMode == CalculationMode.SizeByMaximumVelocity;
    }

    private void InvalidateCalculation()
    {
        Result = null;
        _updateButton.Enabled = false;
        _result.Text = "Inputs changed — click Calculate to size the duct before updating.";
        _result.ForeColor = Color.FromArgb(92, 76, 42);
    }

    private void CalculatePreview()
    {
        try
        {
            var draft = ReadDuct();
            var calculation = draft.Calculate();
            var size = draft.Shape == DuctShape.Round
                ? (_units == UnitSystem.Imperial
                    ? $"{UnitConversions.MToInches(draft.DiameterM):0.##} in round"
                    : $"{UnitConversions.MToMillimeters(draft.DiameterM):0.#} mm round")
                : (_units == UnitSystem.Imperial
                    ? $"{UnitConversions.MToInches(draft.WidthM):0.##} × {UnitConversions.MToInches(draft.HeightM):0.##} in"
                    : $"{UnitConversions.MToMillimeters(draft.WidthM):0.#} × {UnitConversions.MToMillimeters(draft.HeightM):0.#} mm");
            _result.Text = _units == UnitSystem.Imperial
                ? $"{size}   •   {UnitConversions.MpsToFpm(calculation.ActualVelocityMPerS):0} fpm   •   {UnitConversions.PaToInchesWater(calculation.TotalPressureLossPa):0.###} in. w.g."
                : $"{size}   •   {calculation.ActualVelocityMPerS:0.##} m/s   •   {calculation.TotalPressureLossPa:0.##} Pa";
            _result.ForeColor = Color.FromArgb(28, 69, 108);
            Result = draft;
            _updateButton.Enabled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            Result = null;
            _updateButton.Enabled = false;
            _result.Text = ex.Message;
            _result.ForeColor = Color.Firebrick;
        }
    }

    private SavedDuct ReadDuct()
    {
        var airflowDisplay = Positive(_airflow, "airflow");
        var temperatureDisplay = Number(_temperature, "air temperature");
        var lengthDisplay = Positive(_length, "duct length");
        var shape = _shape.SelectedItem is DuctShape selectedShape ? selectedShape : DuctShape.Round;
        var material = _material.SelectedItem as DuctMaterial
                       ?? throw new InvalidOperationException("Select a duct material.");
        var airflow = _units == UnitSystem.Imperial
            ? UnitConversions.CfmToM3PerS(airflowDisplay)
            : UnitConversions.LpsToM3PerS(airflowDisplay);
        var temperature = _units == UnitSystem.Imperial
            ? UnitConversions.FahrenheitToCelsius(temperatureDisplay)
            : temperatureDisplay;
        var length = _units == UnitSystem.Imperial ? UnitConversions.FeetToM(lengthDisplay) : lengthDisplay;
        SavedDuct result;
        if (SelectedMode == CalculationMode.CheckEnteredSize)
        {
            var diameterDisplay = shape == DuctShape.Round ? Positive(_diameter, "diameter") : 0;
            var widthDisplay = shape == DuctShape.Rectangular ? Positive(_width, "width") : 0;
            var heightDisplay = shape == DuctShape.Rectangular ? Positive(_height, "height") : 0;
            result = new SavedDuct
            {
                AirflowM3PerS = airflow,
                TemperatureC = temperature,
                RelativeHumidity = _original.RelativeHumidity,
                AtmosphericPressurePa = _original.AtmosphericPressurePa,
                Shape = shape,
                DiameterM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(diameterDisplay) : UnitConversions.MillimetersToM(diameterDisplay),
                WidthM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(widthDisplay) : UnitConversions.MillimetersToM(widthDisplay),
                HeightM = _units == UnitSystem.Imperial ? UnitConversions.InchesToM(heightDisplay) : UnitConversions.MillimetersToM(heightDisplay),
                MaterialName = material.Name,
                RoughnessFeet = material.RoughnessFeet,
                EquivalentDiameterMethod = _original.EquivalentDiameterMethod,
                PressureLossBasis = _original.PressureLossBasis,
                LengthM = length
            };
        }
        else
        {
            var ratio = shape == DuctShape.Rectangular ? Positive(_aspectRatio, "width-to-height ratio") : 1.0;
            var targetFrictionDisplay = SelectedMode == CalculationMode.SizeByTargetFriction
                ? Positive(_targetFriction, "target static pressure loss") : 1.0;
            var maximumVelocityDisplay = SelectedMode == CalculationMode.SizeByMaximumVelocity
                ? Positive(_maximumVelocity, "maximum velocity") : 1.0;
            var targetFriction = _units == UnitSystem.Imperial
                ? UnitConversions.InchesWaterPer100FeetToPaPerM(targetFrictionDisplay)
                : targetFrictionDisplay;
            var maximumVelocity = _units == UnitSystem.Imperial
                ? UnitConversions.FpmToMps(maximumVelocityDisplay)
                : maximumVelocityDisplay;
            var sizing = DuctSizingSolver.Size(new DuctSizingInput(
                airflow, temperature, _original.RelativeHumidity, _original.AtmosphericPressurePa,
                shape, ratio, material, _original.EquivalentDiameterMethod, _original.PressureLossBasis,
                length, SelectedMode, targetFriction, maximumVelocity, _sizingIncrementM, _roundingDirection));
            result = SavedDuct.FromResult(_original.Sequence, _original.Description, sizing.Calculation,
                _original.AirSystemType, _original.BranchId);
        }
        result.Sequence = _original.Sequence;
        result.Description = _original.Description;
        result.AirSystemType = _original.AirSystemType;
        result.BranchId = _original.BranchId;
        result.DeltaXM = _original.DeltaXM;
        result.DeltaYM = _original.DeltaYM;
        result.DeltaZM = _original.DeltaZM;
        return result;
    }

    private void SaveAndClose()
    {
        if (Result is null)
        {
            MessageBox.Show(this, "Click Calculate and resolve any input errors before updating the duct.",
                "Calculate Duct First", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
}
