namespace DuctSizer.WinForms;

/// <summary>
/// A vertical drag bar between two columns of a <see cref="TableLayoutPanel"/> that resizes one fixed-width
/// column (the Project Explorer or the Selection Inspector). Dragging changes only that column's width, within
/// <see cref="MinimumWidth"/> and a limit that always leaves <see cref="MinimumRemainder"/> pixels for the drawing.
/// Double-clicking restores the designed width. Widths are in pixels at the current display scale.
/// </summary>
internal sealed class PanelSplitter : Control
{
    private readonly TableLayoutPanel _table;
    private readonly int _column;
    private readonly bool _columnIsLeft;
    private bool _dragging;
    private int _dragStartX;
    private float _dragStartWidth;
    private bool _hover;

    /// <param name="table">The table whose column is resized.</param>
    /// <param name="column">Index of the fixed-width column this bar resizes.</param>
    /// <param name="columnIsLeft">True when the column is left of the bar (dragging right widens it).</param>
    /// <param name="designedWidth">Width restored on double-click, in 96-DPI pixels.</param>
    internal PanelSplitter(string name, TableLayoutPanel table, int column, bool columnIsLeft, int designedWidth)
    {
        Name = name;
        _table = table;
        _column = column;
        _columnIsLeft = columnIsLeft;
        DesignedWidth = designedWidth;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Cursor = Cursors.VSplit;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        BackColor = table.BackColor;
        AccessibleName = "Resize panel";
        AccessibleRole = AccessibleRole.Separator;
    }

    /// <summary>Designed (default) width of the column, in 96-DPI pixels.</summary>
    internal int DesignedWidth { get; }

    internal int MinimumWidth { get; set; } = 170;

    internal int MinimumRemainder { get; set; } = 360;

    /// <summary>Width the column had before it was collapsed; restored when it reopens.</summary>
    internal float ExpandedWidth { get; set; }

    /// <summary>Raised after the user finished resizing the column.</summary>
    internal event Action<float>? WidthChanged;

    internal float CurrentWidth => _table.ColumnStyles[_column].Width;

    /// <summary>Sets the column width, kept between the minimum and what leaves room for the drawing.</summary>
    internal void SetColumnWidth(float width)
    {
        var others = 0F;
        for (var index = 0; index < _table.ColumnStyles.Count; index++)
        {
            if (index != _column && _table.ColumnStyles[index].SizeType == SizeType.Absolute)
                others += _table.ColumnStyles[index].Width;
        }
        var maximum = Math.Max(DpiScaling.Scale(MinimumWidth),
            _table.ClientSize.Width - _table.Padding.Horizontal - others - DpiScaling.Scale(MinimumRemainder));
        _table.ColumnStyles[_column].Width = Math.Clamp(width, DpiScaling.Scale(MinimumWidth), maximum);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled)
            return;
        _dragging = true;
        _dragStartX = Cursor.Position.X;
        _dragStartWidth = CurrentWidth;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
            return;
        var delta = Cursor.Position.X - _dragStartX;
        SetColumnWidth(_dragStartWidth + (_columnIsLeft ? delta : -delta));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging)
            return;
        _dragging = false;
        Capture = false;
        ExpandedWidth = CurrentWidth;
        WidthChanged?.Invoke(CurrentWidth);
        Invalidate();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (!Enabled)
            return;
        SetColumnWidth(DpiScaling.Scale(DesignedWidth));
        ExpandedWidth = CurrentWidth;
        WidthChanged?.Invoke(CurrentWidth);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Enabled)
            return;
        // A grip of three dots in the middle, and a faint line while hovering or dragging.
        var centreX = Width / 2F;
        if (_hover || _dragging)
        {
            using var line = new SolidBrush(Color.FromArgb(_dragging ? 150 : 90, 66, 139, 202));
            e.Graphics.FillRectangle(line, centreX - DpiScaling.Scale(1.5F), 0, DpiScaling.Scale(3F), Height);
        }
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var dot = new SolidBrush(Color.FromArgb(132, 152, 170));
        var size = DpiScaling.Scale(3F);
        var spacing = DpiScaling.Scale(6F);
        for (var index = -1; index <= 1; index++)
            e.Graphics.FillEllipse(dot, centreX - size / 2, Height / 2F + index * spacing - size / 2, size, size);
    }
}
