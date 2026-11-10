using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DuctSizer.WinForms;

/// <summary>Colours of the window frame: top tab bar, ribbon and their borders.</summary>
internal static class RibbonPalette
{
    internal static readonly Color TopBar = Color.FromArgb(27, 58, 90);
    internal static readonly Color TopBarText = Color.FromArgb(214, 226, 238);
    internal static readonly Color TopBarHover = Color.FromArgb(42, 78, 114);
    internal static readonly Color Ribbon = Color.White;
    internal static readonly Color RibbonBorder = Color.FromArgb(201, 214, 226);
    internal static readonly Color GroupSeparator = Color.FromArgb(220, 227, 234);
    internal static readonly Color Caption = Color.FromArgb(74, 91, 106);
    internal static readonly Color Ink = Color.FromArgb(27, 39, 51);
    internal static readonly Color Icon = Color.FromArgb(31, 78, 121);
    internal static readonly Color IconAccent = Color.FromArgb(66, 139, 202);
    internal static readonly Color Hover = Color.FromArgb(234, 241, 248);
    internal static readonly Color Checked = Color.FromArgb(220, 235, 247);
    internal static readonly Color CheckedBorder = Color.FromArgb(127, 174, 219);
    internal static readonly Color Disabled = Color.FromArgb(160, 170, 180);
}

internal enum RibbonIcon
{
    Project, Analysis, Reports, QuickCalc,
    NewProject, Open, Save, SaveAs, ProjectSettings, SizingCriteria, RoundSizes,
    Select, Move, AutoSize, Calculate, CriticalPath, PrintReport, DuctCalculator, FittingCalculator,
    Filter, Fan, Warning, Check, Undo, Redo, Delete, ZoomIn, ZoomOut, ZoomFit, PdfImport, Grid, Origin, SourceSink
}

/// <summary>
/// Line icons for the ribbon, tabs and canvas toolbar, drawn with GDI+ on a 24 x 24 grid so they stay sharp
/// at any size and DPI. No image files or third-party icon sets are used.
/// </summary>
internal static class RibbonIcons
{
    internal static void Draw(Graphics graphics, RibbonIcon icon, Rectangle bounds, Color stroke, Color accent)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        var scale = Math.Min(bounds.Width, bounds.Height) / 24f;
        graphics.ScaleTransform(scale, scale);
        using var pen = new Pen(stroke, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var accentPen = new Pen(accent, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var accentFill = new SolidBrush(Color.FromArgb(60, accent));
        using var solid = new SolidBrush(stroke);
        switch (icon)
        {
            case RibbonIcon.Project:
                graphics.DrawRectangle(pen, 5, 4, 14, 17);
                graphics.FillRectangle(accentFill, 8, 2.5f, 8, 3.5f);
                graphics.DrawRectangle(pen, 8, 2.5f, 8, 3.5f);
                graphics.DrawLine(pen, 8, 11, 16, 11);
                graphics.DrawLine(pen, 8, 15, 14, 15);
                break;
            case RibbonIcon.Analysis:
                graphics.DrawLine(pen, 3, 21, 21, 21);
                graphics.FillRectangle(accentFill, 5, 12, 3.5f, 9);
                graphics.DrawRectangle(pen, 5, 12, 3.5f, 9);
                graphics.FillRectangle(accentFill, 10.5f, 6, 3.5f, 15);
                graphics.DrawRectangle(pen, 10.5f, 6, 3.5f, 15);
                graphics.FillRectangle(accentFill, 16, 9, 3.5f, 12);
                graphics.DrawRectangle(pen, 16, 9, 3.5f, 12);
                break;
            case RibbonIcon.Reports:
                DrawPage(graphics, pen);
                graphics.DrawLine(pen, 8, 11, 16, 11);
                graphics.DrawLine(pen, 8, 14.5f, 16, 14.5f);
                graphics.DrawLine(pen, 8, 18, 13, 18);
                break;
            case RibbonIcon.QuickCalc:
            case RibbonIcon.DuctCalculator:
                graphics.DrawRectangle(pen, 5, 2.5f, 14, 19);
                graphics.FillRectangle(accentFill, 7.5f, 5, 9, 4);
                graphics.DrawRectangle(pen, 7.5f, 5, 9, 4);
                foreach (var y in new[] { 12.5f, 15.5f, 18.5f })
                foreach (var x in new[] { 8.5f, 12f, 15.5f })
                    graphics.FillEllipse(solid, x - 1f, y - 1f, 2f, 2f);
                break;
            case RibbonIcon.NewProject:
                DrawPage(graphics, pen);
                graphics.DrawLine(accentPen, 12, 11, 12, 18);
                graphics.DrawLine(accentPen, 8.5f, 14.5f, 15.5f, 14.5f);
                break;
            case RibbonIcon.Open:
                using (var folder = new GraphicsPath())
                {
                    folder.AddLines(new[] { new PointF(2.5f, 19), new PointF(2.5f, 5), new PointF(9, 5), new PointF(11, 7.5f), new PointF(19.5f, 7.5f), new PointF(19.5f, 10) });
                    graphics.DrawPath(pen, folder);
                }
                using (var flap = new GraphicsPath())
                {
                    flap.AddPolygon(new[] { new PointF(2.5f, 19), new PointF(6, 10.5f), new PointF(22, 10.5f), new PointF(18.5f, 19) });
                    graphics.FillPath(accentFill, flap);
                    graphics.DrawPath(pen, flap);
                }
                break;
            case RibbonIcon.Save:
            case RibbonIcon.SaveAs:
                using (var disk = new GraphicsPath())
                {
                    disk.AddLines(new[] { new PointF(3.5f, 3.5f), new PointF(16.5f, 3.5f), new PointF(20.5f, 7.5f), new PointF(20.5f, 20.5f), new PointF(3.5f, 20.5f) });
                    disk.CloseFigure();
                    graphics.DrawPath(pen, disk);
                }
                graphics.FillRectangle(accentFill, 7, 13, 10, 7.5f);
                graphics.DrawRectangle(pen, 7, 13, 10, 7.5f);
                graphics.DrawRectangle(pen, 7.5f, 3.5f, 7.5f, 5);
                if (icon == RibbonIcon.SaveAs)
                {
                    using var pencil = new Pen(accent, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Triangle };
                    graphics.FillEllipse(Brushes.White, 13, 13, 11, 11);
                    graphics.DrawLine(pencil, 22.5f, 14.5f, 15.5f, 21.5f);
                }
                break;
            case RibbonIcon.ProjectSettings:
                DrawGear(graphics, pen, accentFill, 12, 12, 8.5f, 6f);
                graphics.DrawEllipse(pen, 9.5f, 9.5f, 5, 5);
                break;
            case RibbonIcon.SizingCriteria:
                foreach (var (y, knob) in new[] { (6f, 15f), (12f, 8f), (18f, 13f) })
                {
                    graphics.DrawLine(pen, 3, y, 21, y);
                    graphics.FillEllipse(Brushes.White, knob - 2.5f, y - 2.5f, 5, 5);
                    graphics.FillEllipse(accentFill, knob - 2.5f, y - 2.5f, 5, 5);
                    graphics.DrawEllipse(pen, knob - 2.5f, y - 2.5f, 5, 5);
                }
                break;
            case RibbonIcon.RoundSizes:
                using (var ruler = new GraphicsPath())
                {
                    ruler.AddPolygon(new[] { new PointF(2.5f, 16), new PointF(16, 2.5f), new PointF(21.5f, 8), new PointF(8, 21.5f) });
                    graphics.FillPath(accentFill, ruler);
                    graphics.DrawPath(pen, ruler);
                }
                foreach (var offset in new[] { 0f, 3f, 6f, 9f })
                    graphics.DrawLine(pen, 6 + offset, 12.5f - offset, 8 + offset, 14.5f - offset);
                break;
            case RibbonIcon.Select:
                using (var cursor = new GraphicsPath())
                {
                    cursor.AddPolygon(new[] { new PointF(6, 3), new PointF(6, 19), new PointF(10, 15), new PointF(13, 21), new PointF(15.5f, 20), new PointF(12.5f, 14), new PointF(18, 14) });
                    graphics.FillPath(accentFill, cursor);
                    graphics.DrawPath(pen, cursor);
                }
                break;
            case RibbonIcon.Move:
                graphics.DrawLine(pen, 12, 3, 12, 21);
                graphics.DrawLine(pen, 3, 12, 21, 12);
                foreach (var (x1, y1, x2, y2, x3, y3) in new[]
                         {
                             (9f, 6f, 12f, 3f, 15f, 6f), (9f, 18f, 12f, 21f, 15f, 18f),
                             (6f, 9f, 3f, 12f, 6f, 15f), (18f, 9f, 21f, 12f, 18f, 15f)
                         })
                    graphics.DrawLines(pen, new[] { new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3) });
                break;
            case RibbonIcon.AutoSize:
                using (var bolt = new GraphicsPath())
                {
                    bolt.AddPolygon(new[] { new PointF(13.5f, 2), new PointF(5, 13.5f), new PointF(11, 13.5f), new PointF(9.5f, 22), new PointF(19, 9.5f), new PointF(13, 9.5f) });
                    graphics.FillPath(accentFill, bolt);
                    graphics.DrawPath(pen, bolt);
                }
                break;
            case RibbonIcon.Calculate:
                using (var play = new GraphicsPath())
                {
                    play.AddPolygon(new[] { new PointF(6, 3.5f), new PointF(20, 12), new PointF(6, 20.5f) });
                    graphics.FillPath(accentFill, play);
                    graphics.DrawPath(pen, play);
                }
                break;
            case RibbonIcon.CriticalPath:
                using (var route = new Pen(accent, 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    graphics.DrawLines(route, new[] { new PointF(4, 6), new PointF(11, 6), new PointF(11, 18), new PointF(20, 18) });
                graphics.DrawLine(pen, 11, 12, 20, 12);
                foreach (var (x, y) in new[] { (4f, 6f), (20f, 18f), (20f, 12f) })
                {
                    graphics.FillEllipse(Brushes.White, x - 2.2f, y - 2.2f, 4.4f, 4.4f);
                    graphics.DrawEllipse(pen, x - 2.2f, y - 2.2f, 4.4f, 4.4f);
                }
                break;
            case RibbonIcon.PrintReport:
                graphics.DrawRectangle(pen, 7, 3, 10, 6);
                graphics.FillRectangle(accentFill, 3, 9, 18, 8);
                graphics.DrawRectangle(pen, 3, 9, 18, 8);
                graphics.FillRectangle(Brushes.White, 7, 14, 10, 7);
                graphics.DrawRectangle(pen, 7, 14, 10, 7);
                graphics.DrawLine(pen, 9, 17.5f, 15, 17.5f);
                break;
            case RibbonIcon.FittingCalculator:
                using (var elbowOuter = new GraphicsPath())
                {
                    elbowOuter.AddLine(3, 21, 3, 13);
                    elbowOuter.AddArc(3, 3, 20, 20, 180, 90);
                    elbowOuter.AddLine(13, 3, 21, 3);
                    elbowOuter.AddLine(21, 10, 13, 10);
                    elbowOuter.AddArc(10, 10, 6, 6, 270, -90);
                    elbowOuter.AddLine(10, 13, 10, 21);
                    elbowOuter.CloseFigure();
                    graphics.FillPath(accentFill, elbowOuter);
                    graphics.DrawPath(pen, elbowOuter);
                }
                break;
            case RibbonIcon.Filter:
                using (var funnel = new GraphicsPath())
                {
                    funnel.AddPolygon(new[] { new PointF(3, 4), new PointF(21, 4), new PointF(14, 12.5f), new PointF(14, 20), new PointF(10, 18), new PointF(10, 12.5f) });
                    graphics.FillPath(accentFill, funnel);
                    graphics.DrawPath(pen, funnel);
                }
                break;
            case RibbonIcon.Fan:
                graphics.DrawEllipse(pen, 2.5f, 2.5f, 19, 19);
                for (var blade = 0; blade < 3; blade++)
                {
                    var bladeState = graphics.Save();
                    graphics.TranslateTransform(12, 12);
                    graphics.RotateTransform(blade * 120);
                    using var bladePath = new GraphicsPath();
                    bladePath.AddBezier(0, 0, 2, -3, 1, -7, -2.5f, -7.5f);
                    bladePath.AddBezier(-2.5f, -7.5f, -3, -4, -2, -2, 0, 0);
                    graphics.FillPath(accentFill, bladePath);
                    graphics.DrawPath(pen, bladePath);
                    graphics.Restore(bladeState);
                }
                break;
            case RibbonIcon.Warning:
                using (var triangle = new GraphicsPath())
                {
                    triangle.AddPolygon(new[] { new PointF(12, 3), new PointF(22, 20.5f), new PointF(2, 20.5f) });
                    graphics.FillPath(accentFill, triangle);
                    graphics.DrawPath(pen, triangle);
                }
                graphics.DrawLine(pen, 12, 9, 12, 14);
                graphics.FillEllipse(solid, 11, 16.3f, 2, 2);
                break;
            case RibbonIcon.Check:
                graphics.DrawEllipse(pen, 2.5f, 2.5f, 19, 19);
                graphics.DrawLines(accentPen, new[] { new PointF(7, 12.5f), new PointF(10.5f, 16), new PointF(17, 8.5f) });
                break;
            case RibbonIcon.Undo:
            case RibbonIcon.Redo:
                if (icon == RibbonIcon.Redo)
                {
                    graphics.TranslateTransform(24, 0);
                    graphics.ScaleTransform(-1, 1);
                }
                graphics.DrawLines(pen, new[] { new PointF(8.5f, 5), new PointF(4, 9.5f), new PointF(8.5f, 14) });
                graphics.DrawArc(pen, 4, 9.5f, 15, 10, 270, 180);
                graphics.DrawLine(pen, 4, 9.5f, 11.5f, 9.5f);
                graphics.DrawLine(pen, 11.5f, 19.5f, 8, 19.5f);
                break;
            case RibbonIcon.Delete:
                graphics.DrawLine(pen, 4, 6, 20, 6);
                graphics.DrawLine(pen, 9.5f, 6, 9.5f, 3.5f);
                graphics.DrawLine(pen, 9.5f, 3.5f, 14.5f, 3.5f);
                graphics.DrawLine(pen, 14.5f, 3.5f, 14.5f, 6);
                using (var bin = new GraphicsPath())
                {
                    bin.AddPolygon(new[] { new PointF(6, 6), new PointF(7, 20.5f), new PointF(17, 20.5f), new PointF(18, 6) });
                    graphics.DrawPath(pen, bin);
                }
                graphics.DrawLine(pen, 10, 10, 10, 17);
                graphics.DrawLine(pen, 14, 10, 14, 17);
                break;
            case RibbonIcon.ZoomIn:
            case RibbonIcon.ZoomOut:
                graphics.DrawEllipse(pen, 3, 3, 13, 13);
                graphics.DrawLine(pen, 14, 14, 20.5f, 20.5f);
                graphics.DrawLine(pen, 6.5f, 9.5f, 12.5f, 9.5f);
                if (icon == RibbonIcon.ZoomIn)
                    graphics.DrawLine(pen, 9.5f, 6.5f, 9.5f, 12.5f);
                break;
            case RibbonIcon.ZoomFit:
                graphics.DrawLines(pen, new[] { new PointF(3, 8), new PointF(3, 3), new PointF(8, 3) });
                graphics.DrawLines(pen, new[] { new PointF(16, 3), new PointF(21, 3), new PointF(21, 8) });
                graphics.DrawLines(pen, new[] { new PointF(3, 16), new PointF(3, 21), new PointF(8, 21) });
                graphics.DrawLines(pen, new[] { new PointF(16, 21), new PointF(21, 21), new PointF(21, 16) });
                graphics.DrawEllipse(accentPen, 9, 9, 6, 6);
                break;
            case RibbonIcon.PdfImport:
                DrawPage(graphics, pen);
                using (var font = new Font("Segoe UI", 5.6f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var text = new SolidBrush(accent))
                    graphics.DrawString("PDF", font, text, 6.3f, 12.5f);
                break;
            case RibbonIcon.Grid:
                graphics.DrawRectangle(pen, 3, 3, 18, 18);
                graphics.DrawLine(pen, 9, 3, 9, 21);
                graphics.DrawLine(pen, 15, 3, 15, 21);
                graphics.DrawLine(pen, 3, 9, 21, 9);
                graphics.DrawLine(pen, 3, 15, 21, 15);
                break;
            case RibbonIcon.Origin:
                graphics.DrawEllipse(pen, 6, 6, 12, 12);
                graphics.DrawLine(pen, 12, 2, 12, 22);
                graphics.DrawLine(pen, 2, 12, 22, 12);
                graphics.FillEllipse(new SolidBrush(accent), 10, 10, 4, 4);
                break;
            case RibbonIcon.SourceSink:
                graphics.DrawRectangle(pen, 2.5f, 7, 7, 10);
                graphics.FillRectangle(accentFill, 14.5f, 7, 7, 10);
                graphics.DrawRectangle(pen, 14.5f, 7, 7, 10);
                graphics.DrawLine(accentPen, 10, 12, 14, 12);
                graphics.DrawLines(accentPen, new[] { new PointF(12.5f, 10.5f), new PointF(14, 12), new PointF(12.5f, 13.5f) });
                break;
        }
        graphics.Restore(state);
    }

    private static void DrawPage(Graphics graphics, Pen pen)
    {
        using var page = new GraphicsPath();
        page.AddLines(new[] { new PointF(5, 2.5f), new PointF(14.5f, 2.5f), new PointF(19, 7), new PointF(19, 21.5f), new PointF(5, 21.5f) });
        page.CloseFigure();
        graphics.DrawPath(pen, page);
        graphics.DrawLines(pen, new[] { new PointF(14.5f, 2.5f), new PointF(14.5f, 7), new PointF(19, 7) });
    }

    private static void DrawGear(Graphics graphics, Pen pen, Brush fill, float cx, float cy, float outer, float inner)
    {
        using var gear = new GraphicsPath();
        var points = new List<PointF>();
        const int teeth = 8;
        for (var index = 0; index < teeth * 2; index++)
        {
            var radius = index % 2 == 0 ? outer : inner + 0.6f;
            var start = (index - 0.28) * Math.PI / teeth;
            var end = (index + 0.28) * Math.PI / teeth;
            points.Add(new PointF(cx + (float)(radius * Math.Cos(start)), cy + (float)(radius * Math.Sin(start))));
            points.Add(new PointF(cx + (float)(radius * Math.Cos(end)), cy + (float)(radius * Math.Sin(end))));
        }
        gear.AddPolygon(points.ToArray());
        graphics.FillPath(fill, gear);
        graphics.DrawPath(pen, gear);
    }
}

/// <summary>A top-level tab (Project, Analysis, Reports, Quick Calc) in the dark bar above the ribbon.</summary>
internal sealed class AppTabButton : Button
{
    private bool _hover;
    private bool _selected;

    internal AppTabButton(string text, RibbonIcon icon)
    {
        Text = text;
        IconKind = icon;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI Semibold", 10.5F);
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 6, 2, 0);
        Size = new Size(TextRenderer.MeasureText(text, Font).Width + 56, 38);
        TabStop = true;
        AccessibleRole = AccessibleRole.PageTab;
    }

    internal RibbonIcon IconKind { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            AccessibleDescription = value ? "Selected" : null;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(RibbonPalette.TopBar);
        var body = new RectangleF(0, 0, Width, Height + 8);
        if (_selected || _hover)
        {
            using var path = UiPolish.CreateRoundedPath(body, 6);
            using var fill = new SolidBrush(_selected ? RibbonPalette.Ribbon : RibbonPalette.TopBarHover);
            graphics.FillPath(fill, path);
        }
        var textColor = _selected ? RibbonPalette.Ink : RibbonPalette.TopBarText;
        var iconColor = _selected ? RibbonPalette.Icon : RibbonPalette.TopBarText;
        var iconSize = 20;
        RibbonIcons.Draw(graphics, IconKind, new Rectangle(14, (Height - iconSize) / 2, iconSize, iconSize), iconColor,
            _selected ? RibbonPalette.IconAccent : Color.FromArgb(140, 190, 230));
        TextRenderer.DrawText(graphics, Text, Font, new Rectangle(40, 0, Width - 44, Height), textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(4, 4, Width - 8, Height - 8));
    }
}

/// <summary>
/// A ribbon group: its controls sit on top and its caption along the bottom, with a thin separator on the right,
/// as in Office-style ribbons. Add controls to <see cref="Content"/>.
/// </summary>
internal sealed class RibbonGroup : Panel
{
    internal const int CaptionHeight = 18;

    internal RibbonGroup(string name, string caption)
    {
        Name = name;
        Caption = caption;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
        BackColor = RibbonPalette.Ribbon;
        Margin = Padding.Empty;
        Padding = new Padding(1, 1, 3, CaptionHeight);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Content = new FlowLayoutPanel
        {
            Name = name + "Content",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = RibbonPalette.Ribbon,
            Location = new Point(1, 1)
        };
        Controls.Add(Content);
    }

    internal string Caption { get; }

    internal FlowLayoutPanel Content { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var separator = new Pen(RibbonPalette.GroupSeparator);
        e.Graphics.DrawLine(separator, Width - 1, 6, Width - 1, Height - 6);
        using var font = new Font("Segoe UI", 8.25F);
        TextRenderer.DrawText(e.Graphics, Caption, font,
            new Rectangle(0, Height - CaptionHeight - 1, Width - 2, CaptionHeight), RibbonPalette.Caption,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

/// <summary>A large ribbon command: icon above a one- or two-line caption. Can act as a toggle or open a menu.</summary>
internal sealed class RibbonButton : Button
{
    private bool _hover;
    private bool _pressed;
    private bool _checked;

    internal RibbonButton(string name, string text, RibbonIcon icon, string toolTip, int width = 60)
    {
        Name = name;
        Text = text;
        IconKind = icon;
        ToolTipText = toolTip;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 8.5F);
        Size = new Size(width, 74);
        Margin = new Padding(0, 0, 1, 0);
        Cursor = Cursors.Hand;
        AccessibleName = text.Replace("\n", " ", StringComparison.Ordinal);
    }

    internal RibbonIcon IconKind { get; }

    internal string ToolTipText { get; }

    /// <summary>Shows a small arrow and opens this menu below the button when clicked.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal ContextMenuStrip? DropDownMenu { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            AccessibleDescription = value ? "On" : null;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        DropDownMenu?.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? RibbonPalette.Ribbon);
        var frame = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (Enabled && (_checked || _hover || _pressed))
        {
            using var path = UiPolish.CreateRoundedPath(frame, 5);
            using var fill = new SolidBrush(_pressed || _checked ? RibbonPalette.Checked : RibbonPalette.Hover);
            graphics.FillPath(fill, path);
            if (_checked)
            {
                using var border = new Pen(RibbonPalette.CheckedBorder);
                graphics.DrawPath(border, path);
            }
        }
        var stroke = Enabled ? RibbonPalette.Icon : RibbonPalette.Disabled;
        var accent = Enabled ? RibbonPalette.IconAccent : RibbonPalette.Disabled;
        const int iconSize = 28;
        RibbonIcons.Draw(graphics, IconKind, new Rectangle((Width - iconSize) / 2, 6, iconSize, iconSize), stroke, accent);
        var textColor = Enabled ? RibbonPalette.Ink : RibbonPalette.Disabled;
        var textBounds = new Rectangle(1, 37, Width - 2, Height - 38);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        if (DropDownMenu is not null)
        {
            // Drop-down arrow drawn as a shape so it never depends on a font having the glyph.
            var lines = Text.Split('\n');
            var last = lines[^1];
            var lastSize = TextRenderer.MeasureText(graphics, last, Font, Size.Empty, TextFormatFlags.NoPadding);
            var lineHeight = TextRenderer.MeasureText(graphics, "Ag", Font, Size.Empty, TextFormatFlags.NoPadding).Height;
            var arrowX = (Width + lastSize.Width) / 2f + 3;
            var arrowY = textBounds.Top + (lines.Length - 1) * lineHeight + lineHeight / 2f - 1;
            using var arrow = new SolidBrush(textColor);
            graphics.FillPolygon(arrow, new[]
            {
                new PointF(arrowX, arrowY - 1.5f), new PointF(arrowX + 6, arrowY - 1.5f), new PointF(arrowX + 3, arrowY + 2f)
            });
        }
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(2, 2, Width - 4, Height - 4));
    }
}

/// <summary>A small square icon button for the canvas toolbar (undo, zoom, PDF import ...).</summary>
internal sealed class ToolbarIconButton : Button
{
    private bool _hover;
    private bool _checked;

    internal ToolbarIconButton(string name, RibbonIcon icon, string accessibleName)
    {
        Name = name;
        IconKind = icon;
        AccessibleName = accessibleName;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(32, 30);
        Margin = new Padding(1, 0, 1, 0);
        Cursor = Cursors.Hand;
    }

    internal RibbonIcon IconKind { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Checked
    {
        get => _checked;
        set { _checked = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? Color.White);
        if (Enabled && (_hover || _checked))
        {
            using var path = UiPolish.CreateRoundedPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 4);
            using var fill = new SolidBrush(_checked ? RibbonPalette.Checked : RibbonPalette.Hover);
            graphics.FillPath(fill, path);
            if (_checked)
            {
                using var border = new Pen(RibbonPalette.CheckedBorder);
                graphics.DrawPath(border, path);
            }
        }
        const int iconSize = 18;
        RibbonIcons.Draw(graphics, IconKind, new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize),
            Enabled ? RibbonPalette.Icon : RibbonPalette.Disabled, Enabled ? RibbonPalette.IconAccent : RibbonPalette.Disabled);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(2, 2, Width - 4, Height - 4));
    }
}
