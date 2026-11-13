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
    /// <summary>Labels drawn in the last paint, for dragging them and their leaders.</summary>
    private readonly List<PaintedLabel> _paintedLabels = new();
    private string? _dragLabelKey;
    private bool _dragLeader;
    private Point _dragLabelMouse;
    private LayoutLabelPlacement? _dragLabelOriginal;
    private LayoutLabelPlacement? _dragLabelCurrent;

    private sealed record PaintedLabel(string Key, Rectangle Bounds, PointF? LeaderEnd);
    private IReadOnlyDictionary<int, LayoutCoordinate> _resolvedCoordinates =
        new Dictionary<int, LayoutCoordinate>();
    private (double Azimuth, double Elevation)? _orbitView;
    private bool _orbiting;
    private Point _orbitMouse;
    private (double Azimuth, double Elevation) _orbitStartAngles;
    private int _hoverCubeCorner = -1;
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
    /// <summary>Saved label positions and leaders (<see cref="DuctSystemDocument.LayoutLabelPlacements"/>).</summary>
    internal Func<IReadOnlyDictionary<string, LayoutLabelPlacement>> LabelPlacementSource { get; init; } =
        () => new Dictionary<string, LayoutLabelPlacement>();

    /// <summary>A label was dragged, or its leader moved: the new placement (null = back to the default place).</summary>
    internal event Action<string, LayoutLabelPlacement?>? LabelPlacementChanged;

    /// <summary>A label was right-clicked: its key and the pointer position.</summary>
    internal event Action<string, Point>? LabelMenuRequested;
    internal LayoutNodeType? SelectedTool { get; set; }
    internal bool RearrangeMode { get; set; }
    internal bool ShowToScale { get; set; }
    internal double FeetPerGridCell { get; set; } = 1.0;
    internal LayoutViewMode ViewMode { get; set; } = LayoutViewMode.Isometric;

    /// <summary>
    /// A free view (Ctrl + middle-drag, or a view-cube corner): azimuth and elevation in degrees. Null shows the
    /// standard view (<see cref="ViewMode"/>). Azimuth 45 / elevation 30 is exactly Isometric Positive and -45 / 30
    /// Isometric Negative; in a free view components can be selected, edited and connected, but are placed and moved
    /// in a standard view (the grid snapping is planar).
    /// </summary>
    internal (double Azimuth, double Elevation)? OrbitView
    {
        get => _orbitView;
        set
        {
            _orbitView = value is (double azimuth, double elevation)
                ? (NormalizeAzimuth(azimuth), Math.Clamp(elevation, -89.0, 89.0))
                : null;
            Invalidate();
        }
    }

    /// <summary>Raised when the user rotates the view (drag or view cube).</summary>
    internal event Action? OrbitChanged;
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

    /// <summary>ID of the selected component, or null when none (or a duct) is selected.</summary>
    internal string? SelectedNodeId => _selectedNodeId is not null && NodeSource().Any(node => node.Id == _selectedNodeId)
        ? _selectedNodeId : null;

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
            if (_orbitView is not null)
            {
                // Components are placed on the grid of a standard view.
                args.Effect = DragDropEffects.None;
                return;
            }
            args.Effect = DragDropEffects.Copy;
            SetSnapPreview(SnapPlacement(PointToClient(new Point(args.X, args.Y))));
        };
        DragLeave += (_, _) => SetSnapPreview(null);
        DragDrop += (_, args) =>
        {
            if (_orbitView is not null || !TryReadTool(args.Data, out var type, out var fittingGroup))
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
        if (PdfBackgroundSource() is { Visible: true } background
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
        _paintedLabels.Clear();
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
        DrawViewCube(e.Graphics);
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
        if (PdfBackgroundSource() is not { Visible: true } background
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

        // The sheet lies flat at its Center Z elevation; in the isometric and free views it is drawn as the
        // parallelogram its corners project to (an affine image transform), so it lines up with the ductwork.
        var corners = PdfBackgroundCorners(background);
        var priorInterpolation = graphics.InterpolationMode;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = 0.82F };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(_pdfBackgroundImage, new[] { corners[0], corners[1], corners[2] },
            new RectangleF(0, 0, _pdfBackgroundImage.Width, _pdfBackgroundImage.Height), GraphicsUnit.Pixel, attributes);
        graphics.InterpolationMode = priorInterpolation;
    }

    /// <summary>
    /// Screen positions of the underlay's top-left, top-right, bottom-left and bottom-right corners: the sheet is
    /// centred on (Center X, Center Y) at elevation Center Z, with +X to the right of the sheet and +Y down it.
    /// </summary>
    internal PointF[] PdfBackgroundCorners(LayoutPdfBackground background)
    {
        var drawingFeetPerPaperInch = background.DrawingFeet / Math.Max(0.001, background.PaperInches);
        var halfWidth = background.PageWidthPoints / 72.0 * drawingFeetPerPaperInch / 2;
        var halfHeight = background.PageHeightPoints / 72.0 * drawingFeetPerPaperInch / 2;
        PointF Corner(double dx, double dy)
        {
            var point = CoordinatePoint(new LayoutCoordinate(
                UnitConversions.FeetToM(background.CenterXFeet + dx),
                UnitConversions.FeetToM(background.CenterYFeet + dy),
                UnitConversions.FeetToM(background.CenterZFeet)));
            return new PointF(point.X, point.Y);
        }
        return new[]
        {
            Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight),
            Corner(-halfWidth, halfHeight), Corner(halfWidth, halfHeight)
        };
    }

    internal Rectangle PdfBackgroundBounds(LayoutPdfBackground background)
    {
        if (ViewMode != LayoutViewMode.TopDown || _orbitView is not null)
        {
            var corners = PdfBackgroundCorners(background);
            var left = corners.Min(c => c.X);
            var top = corners.Min(c => c.Y);
            return Rectangle.Round(RectangleF.FromLTRB(left, top, corners.Max(c => c.X), corners.Max(c => c.Y)));
        }
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
        // View cube corner: turn the model to look from that corner.
        if (e.Button == MouseButtons.Left && HitTestCubeCorner(e.Location) is var cubeIndex && cubeIndex >= 0)
        {
            var corner = ViewCubeCorners()[cubeIndex];
            ViewFromCubeCorner(corner.X, corner.Y, corner.Z);
            return;
        }
        if (ViewCubeBounds.Contains(e.Location))
            return;
        // Ctrl + middle button: rotate the model freely.
        if (e.Button == MouseButtons.Middle && (ModifierKeys & Keys.Control) == Keys.Control)
        {
            _orbiting = true;
            _orbitMouse = e.Location;
            _orbitStartAngles = CurrentViewAngles;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return;
        }
        // Labels first: drag a label, or the end of its leader; right-click offers the label menu.
        if (SelectedTool is null && !RearrangeMode && HitTestLabel(e.Location, out var onLeaderHandle) is string labelKey)
        {
            if (e.Button == MouseButtons.Right)
            {
                LabelMenuRequested?.Invoke(labelKey, e.Location);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                _dragLabelKey = labelKey;
                _dragLeader = onLeaderHandle;
                _dragLabelMouse = e.Location;
                _dragLabelOriginal = LabelPlacementSource().TryGetValue(labelKey, out var saved) ? saved.Copy() : new LayoutLabelPlacement();
                _dragLabelCurrent = _dragLabelOriginal.Copy();
                Capture = true;
                Cursor = Cursors.SizeAll;
                return;
            }
        }
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
        if (_orbiting)
        {
            _orbiting = false;
            Capture = false;
            Cursor = Cursors.Hand;
            return;
        }
        if (_dragLabelKey is string draggedKey)
        {
            var moved = Math.Abs(e.X - _dragLabelMouse.X) + Math.Abs(e.Y - _dragLabelMouse.Y) > 2;
            var placement = _dragLabelCurrent;
            _dragLabelKey = null;
            _dragLabelCurrent = null;
            Capture = false;
            Cursor = Cursors.Hand;
            if (moved && placement is not null)
                LabelPlacementChanged?.Invoke(draggedKey, placement);
            Invalidate();
            return;
        }
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
        if (e.Button == MouseButtons.Left && _dragNode is not null && _orbitView is null
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
        if (_orbiting)
        {
            // 0.4 degrees per pixel: across turns about Z, up/down tilts.
            RotateViewTo(_orbitStartAngles.Azimuth - (e.X - _orbitMouse.X) * 0.4,
                _orbitStartAngles.Elevation + (e.Y - _orbitMouse.Y) * 0.4);
            return;
        }
        var cubeHover = HitTestCubeCorner(e.Location);
        if (cubeHover != _hoverCubeCorner)
        {
            _hoverCubeCorner = cubeHover;
            Invalidate(ViewCubeBounds);
        }
        if (cubeHover >= 0)
        {
            Cursor = Cursors.Hand;
            return;
        }
        if (_dragLabelKey is not null && _dragLabelOriginal is not null && _dragLabelCurrent is not null)
        {
            // Offsets are kept at 100 % zoom so a label stays put relative to its item when zooming.
            var dx = (e.X - _dragLabelMouse.X) / Math.Max(0.05, ZoomFactor);
            var dy = (e.Y - _dragLabelMouse.Y) / Math.Max(0.05, ZoomFactor);
            if (_dragLeader)
            {
                _dragLabelCurrent.LeaderX = _dragLabelOriginal.LeaderX + dx;
                _dragLabelCurrent.LeaderY = _dragLabelOriginal.LeaderY + dy;
            }
            else
            {
                _dragLabelCurrent.OffsetX = _dragLabelOriginal.OffsetX + dx;
                _dragLabelCurrent.OffsetY = _dragLabelOriginal.OffsetY + dy;
            }
            Invalidate();
            return;
        }
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
        if (_orbitView is not null)
            SetSnapPreview(null);
        else if (_dragNode is not null && e.Button == MouseButtons.Left
            && Math.Abs(e.X - _mouseDown.X) + Math.Abs(e.Y - _mouseDown.Y) > 6)
            SetSnapPreview(SnapPlacement(e.Location));
        else if (SelectedTool is not null && _dragNode is null)
            SetSnapPreview(SnapPlacement(e.Location));
        else if (_dragNode is null)
            SetSnapPreview(null);
        Cursor = SelectedTool is null && !RearrangeMode && HitTestLabel(e.Location, out _) is not null
            ? Cursors.SizeAll
            : HitTest(e.Location) is not null && RearrangeMode
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
        if (SelectedTool is null || _orbitView is not null || ViewCubeBounds.Contains(e.Location))
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
            using var selectionPen = new Pen(Color.FromArgb(14, 111, 230), Math.Max(1.4F, 1.8F * ZoomFactor));
            graphics.FillRectangle(selectionFill, selectionBounds);
            graphics.DrawRectangle(selectionPen, selectionBounds);
        }
        var iconColor = node.AirflowOverride
            ? Color.FromArgb(222, 126, 34)
            : node.IsDefined ? Color.FromArgb(10, 88, 187) : Color.FromArgb(196, 48, 48);
        var iconSize = Math.Max(22, (int)Math.Round(34 * ZoomFactor));
        var iconBounds = node.Type == LayoutNodeType.Duct
            ? new Rectangle(bounds.Left + 3, bounds.Top + 3, bounds.Width - 6, Math.Max(14, bounds.Height - 6))
            : new Rectangle(bounds.Left + bounds.Width / 2 - iconSize / 2,
                bounds.Top + bounds.Height / 2 - iconSize / 2, iconSize, iconSize);
        LayoutIconPainter.Draw(graphics, node.Type, iconBounds, iconColor,
            node.Type == LayoutNodeType.Grd ? node.GrdType
            : node.Type == LayoutNodeType.CalculationPoint ? node.CalculationPointType
            : node.Type == LayoutNodeType.Reservoir ? node.SpaceRole
            : node.IsAirHandlingUnit ? LayoutFanData.AirHandlingUnitTool
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
        var textBounds = PlaceLabel(graphics, LayoutLabelPlacement.NodeKey(node.Sequence),
            new Rectangle(bounds.Right + Math.Max(5, (int)Math.Round(7 * ZoomFactor)),
                bounds.Top - Math.Max(2, (int)Math.Round(3 * ZoomFactor)), measured.Width + 8, measured.Height + 3),
            Center(node));
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
        if (_orbitView is not null)
        {
            // The floor grid (Z = 0) around the drawing, projected with the free view.
            var nodes = NodeSource();
            var cellFeet = ShowToScale ? Math.Max(0.1, FeetPerGridCell) : 5.0;
            var cells = nodes.Select(WorldCoordinate)
                .Select(c => (X: UnitConversions.MToFeet(c.XM) / cellFeet, Y: UnitConversions.MToFeet(c.YM) / cellFeet)).ToArray();
            var minX = (int)Math.Floor(cells.Length == 0 ? -10 : cells.Min(c => c.X)) - 10;
            var maxX = (int)Math.Ceiling(cells.Length == 0 ? 10 : cells.Max(c => c.X)) + 10;
            var minY = (int)Math.Floor(cells.Length == 0 ? -10 : cells.Min(c => c.Y)) - 10;
            var maxY = (int)Math.Ceiling(cells.Length == 0 ? 10 : cells.Max(c => c.Y)) + 10;
            var stride = Math.Max(GridLineStride(GridSize / 2.2F), (int)Math.Ceiling(Math.Max(maxX - minX, maxY - minY) / 160.0));
            for (var x = minX; x <= maxX; x++)
                if (x % stride == 0)
                    graphics.DrawLine(x % 5 == 0 ? major : minor, OrbitProject(x, minY, 0), OrbitProject(x, maxY, 0));
            for (var y = minY; y <= maxY; y++)
                if (y % stride == 0)
                    graphics.DrawLine(y % 5 == 0 ? major : minor, OrbitProject(minX, y, 0), OrbitProject(maxX, y, 0));
            return;
        }
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
        using var zPen = new Pen(Color.FromArgb(14, 111, 230), 2F) { EndCap = LineCap.ArrowAnchor };
        if (_orbitView is (double, double) angles)
        {
            var legendOrigin = new PointF(axisOrigin.X, axisOrigin.Y);
            PointF End(double x, double y, double z) => OrbitProject(x, y, z / OrbitZScale, angles, legendOrigin, axisLength);
            var ox = End(1, 0, 0);
            var oy = End(0, 1, 0);
            var oz = End(0, 0, 1);
            graphics.DrawLine(xPen, legendOrigin, ox);
            graphics.DrawLine(yPen, legendOrigin, oy);
            graphics.DrawLine(zPen, legendOrigin, oz);
            using var orbitAxisFont = new Font("Segoe UI Semibold", 8F);
            graphics.DrawString("X", orbitAxisFont, Brushes.Firebrick, ox.X + 2, ox.Y - 5);
            graphics.DrawString("Y", orbitAxisFont, Brushes.SeaGreen, oy.X + 2, oy.Y - 5);
            graphics.DrawString("Z", orbitAxisFont, Brushes.SteelBlue, oz.X + 3, oz.Y - 12);
            return;
        }
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
        using var dot = new SolidBrush(Color.FromArgb(14, 111, 230));
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
            using var selected = new Pen(Color.FromArgb(14, 111, 230), Math.Max(6F, 7F * ZoomFactor))
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
        var labelBounds = PlaceLabel(graphics,
            LayoutLabelPlacement.ConnectionKey(connectionRef.Downstream.Sequence, connectionRef.Secondary),
            new Rectangle(center.X - labelWidth / 2, center.Y - labelHeight / 2, labelWidth, labelHeight),
            PathMidpoint(path));
        using var labelBackground = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
        graphics.FillRectangle(labelBackground, labelBounds);
        TextRenderer.DrawText(graphics, ductLabel, labelFont,
            labelBounds, Color.FromArgb(20, 44, 65), TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
    }

    /// <summary>
    /// Where a label is drawn: its default place (kept clear of other labels when that option is on), or where the
    /// engineer dragged it. Draws its leader, if it has one, from the label to the point it was dragged to.
    /// </summary>
    private Rectangle PlaceLabel(Graphics graphics, string key, Rectangle preferred, Point reference)
    {
        var placement = CurrentLabelPlacement(key);
        Rectangle bounds;
        if (placement is null || placement.OffsetX == 0 && placement.OffsetY == 0 && !placement.HasLeader)
            bounds = ReserveLabelBounds(preferred);
        else
        {
            bounds = preferred;
            bounds.Offset((int)Math.Round(placement.OffsetX * ZoomFactor), (int)Math.Round(placement.OffsetY * ZoomFactor));
            _paintLabelBounds.Add(Rectangle.Inflate(bounds, 3, 2));
        }
        PointF? leaderEnd = null;
        if (placement is { HasLeader: true })
        {
            var end = new PointF(reference.X + (float)(placement.LeaderX * ZoomFactor), reference.Y + (float)(placement.LeaderY * ZoomFactor));
            leaderEnd = end;
            var start = new PointF(Math.Clamp(end.X, bounds.Left, bounds.Right), Math.Clamp(end.Y, bounds.Top, bounds.Bottom));
            using var leader = new Pen(Color.FromArgb(70, 92, 112), Math.Max(1F, 1.2F * ZoomFactor))
            {
                CustomEndCap = new AdjustableArrowCap(3.2F, 4.2F, true)
            };
            if (Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y) > 3)
                graphics.DrawLine(leader, start, end);
            // Handle to drag the leader's end onto the duct or fitting it points to.
            var radius = Math.Max(3F, 3.5F * ZoomFactor);
            using var handleFill = new SolidBrush(Color.White);
            using var handlePen = new Pen(Color.FromArgb(14, 111, 230), 1.2F);
            graphics.FillEllipse(handleFill, end.X - radius, end.Y - radius, radius * 2, radius * 2);
            graphics.DrawEllipse(handlePen, end.X - radius, end.Y - radius, radius * 2, radius * 2);
        }
        _paintedLabels.Add(new PaintedLabel(key, bounds, leaderEnd));
        return bounds;
    }

    private LayoutLabelPlacement? CurrentLabelPlacement(string key) =>
        string.Equals(key, _dragLabelKey, StringComparison.Ordinal)
            ? _dragLabelCurrent
            : LabelPlacementSource().TryGetValue(key, out var saved) ? saved : null;

    /// <summary>The label (or, when <paramref name="leaderHandle"/>, the leader end handle) under the pointer.</summary>
    private string? HitTestLabel(Point point, out bool leaderHandle)
    {
        var handleRadius = Math.Max(6F, 6F * ZoomFactor);
        for (var index = _paintedLabels.Count - 1; index >= 0; index--)
        {
            var label = _paintedLabels[index];
            if (label.LeaderEnd is PointF end && Math.Abs(end.X - point.X) <= handleRadius && Math.Abs(end.Y - point.Y) <= handleRadius)
            {
                leaderHandle = true;
                return label.Key;
            }
        }
        for (var index = _paintedLabels.Count - 1; index >= 0; index--)
            if (_paintedLabels[index].Bounds.Contains(point))
            {
                leaderHandle = false;
                return _paintedLabels[index].Key;
            }
        leaderHandle = false;
        return null;
    }

    /// <summary>The label key under the pointer, if any (used by the Designer for its label menu).</summary>
    internal string? LabelAt(Point point) => HitTestLabel(point, out _);

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
        LayoutCoordinate parentCoordinate = default!, downstreamCoordinate = default!;
        var useCoordinates = _orbitView is not null;
        if (useCoordinates)
        {
            parentCoordinate = WorldCoordinate(parent);
            downstreamCoordinate = WorldCoordinate(downstream);
        }
        else if (UsesResolvedCoordinate(parent) && UsesResolvedCoordinate(downstream)
                 && _resolvedCoordinates.TryGetValue(parent.Sequence, out var resolvedParent)
                 && _resolvedCoordinates.TryGetValue(downstream.Sequence, out var resolvedDownstream))
        {
            parentCoordinate = resolvedParent;
            downstreamCoordinate = resolvedDownstream;
            useCoordinates = true;
        }
        if (useCoordinates)
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

    private PointF[] AxisDirections => _orbitView is not null ? OrbitAxisDirections() : ViewMode switch
    {
        LayoutViewMode.TopDown => TopDownAxisDirections,
        LayoutViewMode.IsometricNegative => NegativeIsoAxisDirections,
        _ => IsoAxisDirections
    };

    /// <summary>Screen directions of +X, +Y, -Z, -X, -Y, +Z in the free view (axes seen end-on are left out).</summary>
    private PointF[] OrbitAxisDirections()
    {
        var origin = OrbitProject(0, 0, 0);
        return new (double X, double Y, double Z)[] { (1, 0, 0), (0, 1, 0), (0, 0, -1), (-1, 0, 0), (0, -1, 0), (0, 0, 1) }
            .Select(axis => OrbitProject(axis.X, axis.Y, axis.Z))
            .Select(point => new PointF(point.X - origin.X, point.Y - origin.Y))
            .Where(direction => Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y) > GridSize * 0.05)
            .ToArray();
    }

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
        if (_orbitView is not null)
        {
            var projected = OrbitProject(x, y, z);
            return new Point((int)Math.Round(projected.X), (int)Math.Round(projected.Y));
        }
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
        _orbitView is not null
            ? CoordinatePoint(WorldCoordinate(node))
            : UsesResolvedCoordinate(node) && _resolvedCoordinates.TryGetValue(node.Sequence, out var coordinate)
                ? CoordinatePoint(coordinate)
                : GridPoint(DisplayGridPoint(node));

    /// <summary>
    /// A component's position in the drawing (m): its resolved coordinate, or, for a component placed only on the
    /// grid, the point under its grid position at Z = 0 (the inverse of the Isometric Positive grid mapping).
    /// </summary>
    private LayoutCoordinate WorldCoordinate(SavedLayoutNode node)
    {
        if (UsesResolvedCoordinate(node) && _resolvedCoordinates.TryGetValue(node.Sequence, out var coordinate))
            return coordinate;
        var cellFeet = ShowToScale ? Math.Max(0.1, FeetPerGridCell) : 5.0;
        var x = (node.GridX + node.GridY) / 2.0 * cellFeet;
        var y = (node.GridY - node.GridX) / 2.0 * cellFeet;
        return new LayoutCoordinate(UnitConversions.FeetToM(x), UnitConversions.FeetToM(y), 0);
    }

    // ---- Free view (orbit) -------------------------------------------------------------------------------------

    /// <summary>Vertical scale of Z in the free view, chosen so azimuth ±45° / elevation 30° equals the isometric views.</summary>
    private const double OrbitZScale = 0.816496580927726;

    private static double NormalizeAzimuth(double degrees)
    {
        var value = degrees % 360.0;
        if (value <= -180)
            value += 360;
        if (value > 180)
            value -= 360;
        return value;
    }

    /// <summary>The angles equivalent to the current view (free or standard).</summary>
    internal (double Azimuth, double Elevation) CurrentViewAngles => _orbitView ?? ViewMode switch
    {
        LayoutViewMode.TopDown => (0.0, 89.0),
        LayoutViewMode.IsometricNegative => (-45.0, 30.0),
        _ => (45.0, 30.0)
    };

    /// <summary>Projects a point given in grid cells (x, y, z) with the free view's angles.</summary>
    private PointF OrbitProject(double x, double y, double z) => OrbitProject(x, y, z, _orbitView ?? CurrentViewAngles,
        new PointF(OriginX, OriginY), GridSize / Math.Sqrt(2));

    private static PointF OrbitProject(double x, double y, double z, (double Azimuth, double Elevation) angles,
        PointF origin, double scale)
    {
        var theta = angles.Azimuth * Math.PI / 180;
        var phi = angles.Elevation * Math.PI / 180;
        var across = x * Math.Cos(theta) - y * Math.Sin(theta);
        var depth = x * Math.Sin(theta) + y * Math.Cos(theta);
        return new PointF(
            (float)(origin.X + across * scale),
            (float)(origin.Y + (depth * Math.Sin(phi) - z * Math.Cos(phi) * OrbitZScale) * scale));
    }

    /// <summary>
    /// Changes the free view's angles while keeping the drawing's centre where it is on screen, so the model turns
    /// about its own middle rather than the origin.
    /// </summary>
    internal void RotateViewTo(double azimuth, double elevation)
    {
        var nodes = NodeSource();
        _resolvedCoordinates = CoordinateSource();
        LayoutCoordinate? pivot = null;
        if (nodes.Count > 0)
        {
            // Turn about the components in view (all of them when none is), so what you are looking at stays put.
            var view = new Rectangle(0, 0, Width, Height);
            var visible = nodes.Where(node => view.Contains(VisualLocation(node))).ToArray();
            var world = (visible.Length > 0 ? visible : nodes.ToArray()).Select(WorldCoordinate).ToArray();
            pivot = new LayoutCoordinate((world.Min(c => c.XM) + world.Max(c => c.XM)) / 2,
                (world.Min(c => c.YM) + world.Max(c => c.YM)) / 2, (world.Min(c => c.ZM) + world.Max(c => c.ZM)) / 2);
        }
        if (_orbitView is null)
            _orbitView = CurrentViewAngles;
        var before = pivot is LayoutCoordinate p0 ? CoordinatePoint(p0) : Point.Empty;
        _orbitView = (NormalizeAzimuth(azimuth), Math.Clamp(elevation, -89.0, 89.0));
        if (pivot is LayoutCoordinate p1)
        {
            var after = CoordinatePoint(p1);
            _viewOffset = new PointF(_viewOffset.X + before.X - after.X, _viewOffset.Y + before.Y - after.Y);
        }
        Invalidate();
        OrbitChanged?.Invoke();
    }

    /// <summary>The view cube, just left of the XYZ legend.</summary>
    internal Rectangle ViewCubeBounds
    {
        get
        {
            var legend = AxisLegendBounds;
            return new Rectangle(legend.Left - legend.Width - 8, legend.Top, legend.Width, legend.Height);
        }
    }

    /// <summary>Screen positions of the view cube's 8 corners (sx, sy, sz = ±1) in the current view.</summary>
    internal IReadOnlyList<(int X, int Y, int Z, PointF Point, double Depth)> ViewCubeCorners()
    {
        var bounds = ViewCubeBounds;
        var center = new PointF(bounds.Left + bounds.Width / 2F, bounds.Top + bounds.Height / 2F + 4);
        var angles = CurrentViewAngles;
        var theta = angles.Azimuth * Math.PI / 180;
        var phi = angles.Elevation * Math.PI / 180;
        var scale = bounds.Width * 0.24;
        var corners = new List<(int, int, int, PointF, double)>();
        foreach (var sz in new[] { 1, -1 })
        foreach (var sx in new[] { 1, -1 })
        foreach (var sy in new[] { 1, -1 })
        {
            // Toward the viewer: (sinθ cosφ, cosθ cosφ, sinφ).
            var depth = sx * Math.Sin(theta) * Math.Cos(phi) + sy * Math.Cos(theta) * Math.Cos(phi) + sz * Math.Sin(phi);
            corners.Add((sx, sy, sz, OrbitProject(sx, sy, sz / OrbitZScale, angles, center, scale), depth));
        }
        return corners;
    }

    /// <summary>The view-cube corner under the pointer (index into <see cref="ViewCubeCorners"/>), or -1.</summary>
    private int HitTestCubeCorner(Point point)
    {
        if (!ViewCubeBounds.Contains(point))
            return -1;
        var corners = ViewCubeCorners();
        var best = -1;
        var bestDistance = 9.0;
        for (var index = 0; index < corners.Count; index++)
        {
            var distance = Math.Sqrt(Math.Pow(corners[index].Point.X - point.X, 2) + Math.Pow(corners[index].Point.Y - point.Y, 2));
            // Prefer the corner nearer the viewer where two overlap.
            if (distance < bestDistance - (corners[index].Depth > 0 ? 0.5 : 0))
            {
                bestDistance = distance;
                best = index;
            }
        }
        return best;
    }

    /// <summary>Turns the model so the clicked cube corner faces the viewer (an isometric view from that corner).</summary>
    internal void ViewFromCubeCorner(int sx, int sy, int sz) =>
        RotateViewTo(Math.Atan2(sx, sy) * 180 / Math.PI, sz * 30.0);

    private void DrawViewCube(Graphics graphics)
    {
        var bounds = ViewCubeBounds;
        using var fill = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        using var border = new Pen(Color.FromArgb(165, 184, 195), 1F);
        graphics.FillRectangle(fill, bounds);
        graphics.DrawRectangle(border, bounds);
        var corners = ViewCubeCorners();
        // Faces: the three facing the viewer are shaded; edges drawn back to front.
        var faces = new (int Axis, int Sign)[] { (0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1) };
        var angles = CurrentViewAngles;
        var theta = angles.Azimuth * Math.PI / 180;
        var phi = angles.Elevation * Math.PI / 180;
        var toward = new[] { Math.Sin(theta) * Math.Cos(phi), Math.Cos(theta) * Math.Cos(phi), Math.Sin(phi) };
        foreach (var (axis, sign) in faces)
        {
            if (toward[axis] * sign <= 0.02)
                continue;
            var faceCorners = corners.Where(c => (axis == 0 ? c.X : axis == 1 ? c.Y : c.Z) == sign).ToArray();
            // Order the four corners around the face.
            var cx = faceCorners.Average(c => c.Point.X);
            var cy = faceCorners.Average(c => c.Point.Y);
            var ordered = faceCorners.OrderBy(c => Math.Atan2(c.Point.Y - cy, c.Point.X - cx)).Select(c => c.Point).ToArray();
            var shade = axis == 2 ? Color.FromArgb(214, 230, 242) : axis == 0 ? Color.FromArgb(232, 240, 247) : Color.FromArgb(222, 234, 244);
            using var faceBrush = new SolidBrush(shade);
            using var facePen = new Pen(Color.FromArgb(105, 140, 168), 1.1F);
            graphics.FillPolygon(faceBrush, ordered);
            graphics.DrawPolygon(facePen, ordered);
            if (axis == 2 && sign == 1 && ordered.Length == 4)
            {
                using var font = new Font("Segoe UI Semibold", 6.5F);
                var size = graphics.MeasureString("TOP", font);
                graphics.DrawString("TOP", font, Brushes.SteelBlue, (float)cx - size.Width / 2, (float)cy - size.Height / 2);
            }
        }
        for (var index = 0; index < corners.Count; index++)
        {
            var corner = corners[index];
            if (corner.Depth < -0.05)
                continue;
            var hover = index == _hoverCubeCorner;
            var radius = hover ? 5.5F : 3.6F;
            using var dot = new SolidBrush(hover ? Color.FromArgb(14, 111, 230) : Color.FromArgb(70, 110, 140));
            graphics.FillEllipse(dot, corner.Point.X - radius, corner.Point.Y - radius, radius * 2, radius * 2);
        }
        using var captionFont = new Font("Segoe UI", 6.5F);
        TextRenderer.DrawText(graphics, _orbitView is null ? "Click a corner" : "Free view", captionFont,
            new Rectangle(bounds.Left, bounds.Top + 2, bounds.Width, 12), Color.FromArgb(92, 107, 120),
            TextFormatFlags.HorizontalCenter);
    }

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
