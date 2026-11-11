using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

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

    /// <summary>Raised when the selected item was right-clicked, with the pointer position on the drawing.</summary>
    internal event Action<Point>? ItemMenuRequested;

    internal bool HasSelection => _selectedNodeId is not null && NodeSource().Any(node => node.Id == _selectedNodeId)
                                  || _selectedConnection is not null;

    /// <summary>Deselects the selected component or duct. False when nothing was selected.</summary>
    internal bool ClearSelection()
    {
        if (!HasSelection)
            return false;
        SetSelectedNode(null);
        SetSelectedConnection(null);
        Invalidate();
        return true;
    }
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
        if (e.Button == MouseButtons.Right)
        {
            // Right-click selects the item under the pointer and offers Edit / Delete.
            if (_dragNode is not null)
            {
                var node = _dragNode;
                _dragNode = null;
                SetSelectedNode(node);
                ItemMenuRequested?.Invoke(e.Location);
            }
            else if (HitTestConnection(e.Location) is LayoutConnectionRef rightClicked)
            {
                SetSelectedConnection(rightClicked);
                ItemMenuRequested?.Invoke(e.Location);
            }
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
