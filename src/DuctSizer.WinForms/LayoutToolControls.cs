using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

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

    /// <summary>Draws the tool's short name under its icon (used in the ribbon).</summary>
    internal bool ShowCaption { get; set; }

    internal string Title => _title;

    private bool _hover;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (!ShowCaption)
        {
            var iconSize = Math.Min(40, Math.Max(28, Height - 8));
            LayoutIconPainter.Draw(e.Graphics, _type,
                new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize),
                Color.FromArgb(21, 83, 99), _fittingGroup);
            return;
        }
        if (_hover)
        {
            using var hover = UiPolish.CreateRoundedPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 4);
            using var fill = new SolidBrush(RibbonPalette.Hover);
            e.Graphics.FillPath(fill, hover);
        }
        const int captionHeight = 13;
        var compactIcon = Math.Max(16, Math.Min(24, Height - captionHeight - 3));
        LayoutIconPainter.Draw(e.Graphics, _type,
            new Rectangle((Width - compactIcon) / 2, 2, compactIcon, compactIcon),
            Color.FromArgb(21, 83, 99), _fittingGroup);
        using var font = new Font("Segoe UI", 7.5F);
        TextRenderer.DrawText(e.Graphics, _title, font, new Rectangle(0, Height - captionHeight - 1, Width, captionHeight),
            RibbonPalette.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding
                               | TextFormatFlags.EndEllipsis);
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
