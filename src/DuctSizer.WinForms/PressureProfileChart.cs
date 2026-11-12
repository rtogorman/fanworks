using System.Drawing.Drawing2D;
using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Line graph of the running pressure loss along every route (Analysis tab): x is the component number along the
/// route (1, 2, 3 …), y the pressure loss from the start of the route. The critical path is drawn on top in amber;
/// other routes in a muted blue. Hovering a point names the route, the component and the value.
/// </summary>
internal sealed class PressureProfileChart : Control
{
    internal static readonly Color CriticalColor = Color.FromArgb(214, 128, 0);
    internal static readonly Color PathColor = Color.FromArgb(82, 136, 178);
    private static readonly Color AxisInk = Color.FromArgb(74, 91, 106);
    private static readonly Color GridLine = Color.FromArgb(228, 234, 240);

    private readonly ToolTip _toolTip = new() { InitialDelay = 150, ReshowDelay = 50 };
    private DuctSystemPdfPressureProfile? _profile;
    private string _emptyMessage = "Connect the system components to see the pressure along each path.";
    private Rectangle _plot;
    private double _yMax = 1;
    private int _xMax = 1;
    private string _hoverText = string.Empty;

    internal PressureProfileChart()
    {
        Name = "AnalysisPressureProfileChart";
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5F);
    }

    /// <summary>The routes shown (null or no series shows <see cref="EmptyMessage"/>).</summary>
    internal DuctSystemPdfPressureProfile? Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            Invalidate();
        }
    }

    internal string EmptyMessage
    {
        get => _emptyMessage;
        set
        {
            _emptyMessage = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var series = _profile?.Series ?? Array.Empty<DuctSystemPdfProfileSeries>();
        if (series.Count == 0 || series.All(x => x.Values.Count == 0))
        {
            TextRenderer.DrawText(graphics, _emptyMessage, Font, ClientRectangle, AxisInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var unit = _profile!.UnitLabel;
        _xMax = Math.Max(2, series.Max(x => x.Values.Count));
        var dataMax = series.SelectMany(x => x.Values).DefaultIfEmpty(0).Max();
        var lineHeight = TextRenderer.MeasureText("0", Font).Height;
        var legendHeight = lineHeight + DpiScaling.Scale(6);
        var top = legendHeight + DpiScaling.Scale(6);
        var bottom = ClientSize.Height - lineHeight * 2 - DpiScaling.Scale(12);
        // About one gridline per 30 px of height (two to five intervals), so labels never crowd.
        var intervals = Math.Clamp((bottom - top) / DpiScaling.Scale(30), 2, 5);
        var (yMax, yStep) = NiceAxis(dataMax, intervals);
        _yMax = yMax;
        var xStep = NiceIntegerStep(_xMax);
        var yLabels = Enumerable.Range(0, (int)Math.Round(yMax / yStep) + 1).Select(i => i * yStep).ToArray();
        var decimals = Decimals(yStep);
        var yTextWidth = yLabels.Max(v => TextRenderer.MeasureText(Format(v, decimals), Font).Width);
        _plot = Rectangle.FromLTRB(
            yTextWidth + DpiScaling.Scale(10) + lineHeight + DpiScaling.Scale(4),
            top,
            ClientSize.Width - DpiScaling.Scale(14),
            bottom);
        if (_plot.Width < 40 || _plot.Height < 30)
            return;

        using var gridPen = new Pen(GridLine, 1f);
        using var axisPen = new Pen(Color.FromArgb(160, 176, 190), 1f);
        foreach (var value in yLabels)
        {
            var y = Y(value);
            graphics.DrawLine(value == 0 ? axisPen : gridPen, _plot.Left, y, _plot.Right, y);
            var text = Format(value, decimals);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point(_plot.Left - size.Width - DpiScaling.Scale(4), (int)(y - size.Height / 2f)), AxisInk);
        }
        for (var x = 1; x <= _xMax; x += xStep)
        {
            var px = X(x);
            graphics.DrawLine(gridPen, px, _plot.Top, px, _plot.Bottom);
            var text = x.ToString(CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point((int)(px - size.Width / 2f), _plot.Bottom + DpiScaling.Scale(3)), AxisInk);
        }
        graphics.DrawLine(axisPen, _plot.Left, _plot.Top, _plot.Left, _plot.Bottom);

        var xTitle = "Component number along the path";
        var xTitleSize = TextRenderer.MeasureText(xTitle, Font);
        TextRenderer.DrawText(graphics, xTitle, Font,
            new Point(_plot.Left + (_plot.Width - xTitleSize.Width) / 2, _plot.Bottom + lineHeight + DpiScaling.Scale(6)), AxisInk);
        var yTitle = $"Pressure drop ({unit})";
        var state = graphics.Save();
        graphics.TranslateTransform(DpiScaling.Scale(2), _plot.Top + _plot.Height / 2f);
        graphics.RotateTransform(-90);
        var yTitleSize = graphics.MeasureString(yTitle, Font);
        using (var ink = new SolidBrush(AxisInk))
            graphics.DrawString(yTitle, Font, ink, -yTitleSize.Width / 2, 0);
        graphics.Restore(state);

        using var pathPen = new Pen(Color.FromArgb(150, PathColor), DpiScaling.Scale(1.6f)) { LineJoin = LineJoin.Round };
        using var criticalPen = new Pen(CriticalColor, DpiScaling.Scale(2.6f)) { LineJoin = LineJoin.Round };
        using var pathDot = new SolidBrush(PathColor);
        using var criticalDot = new SolidBrush(CriticalColor);
        foreach (var line in series.Where(x => !x.IsCritical))
            DrawSeries(graphics, line, pathPen, pathDot, DpiScaling.Scale(2.4f));
        foreach (var line in series.Where(x => x.IsCritical))
            DrawSeries(graphics, line, criticalPen, criticalDot, DpiScaling.Scale(3.2f));

        // Legend (top left of the plot).
        var critical = series.FirstOrDefault(x => x.IsCritical);
        var others = series.Count(x => !x.IsCritical);
        var legendX = _plot.Left;
        var legendY = DpiScaling.Scale(2) + lineHeight / 2;
        if (critical is not null)
            legendX = LegendEntry(graphics, legendX, legendY, criticalPen, $"Critical path ({critical.Label})");
        if (others > 0)
            LegendEntry(graphics, legendX, legendY, pathPen, others == 1 ? "Other path" : $"Other paths ({others})");
    }

    private int LegendEntry(Graphics graphics, int x, int y, Pen pen, string text)
    {
        var swatch = DpiScaling.Scale(18);
        graphics.DrawLine(pen, x, y, x + swatch, y);
        var size = TextRenderer.MeasureText(text, Font);
        TextRenderer.DrawText(graphics, text, Font, new Point(x + swatch + DpiScaling.Scale(5), y - size.Height / 2), Color.FromArgb(48, 65, 80));
        return x + swatch + size.Width + DpiScaling.Scale(18);
    }

    private void DrawSeries(Graphics graphics, DuctSystemPdfProfileSeries line, Pen pen, Brush dot, float radius)
    {
        if (line.Values.Count == 0)
            return;
        var points = line.Values.Select((value, index) => new PointF(X(index + 1), Y(value))).ToArray();
        if (points.Length > 1)
            graphics.DrawLines(pen, points);
        if (points.Length <= 60)
            foreach (var point in points)
                graphics.FillEllipse(dot, point.X - radius, point.Y - radius, radius * 2, radius * 2);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var text = HitText(e.Location);
        if (text == _hoverText)
            return;
        _hoverText = text;
        if (text.Length == 0)
            _toolTip.Hide(this);
        else
            _toolTip.Show(text, this, e.X + DpiScaling.Scale(14), e.Y + DpiScaling.Scale(14));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverText = string.Empty;
        _toolTip.Hide(this);
    }

    /// <summary>The nearest plotted point within a few pixels, described for the tooltip; empty when none.</summary>
    internal string HitText(Point location)
    {
        if (_profile is null || _plot.Width <= 0)
            return string.Empty;
        var best = double.MaxValue;
        var text = string.Empty;
        // Critical path first, so it wins where routes share points.
        foreach (var line in _profile.Series.OrderByDescending(x => x.IsCritical))
            for (var index = 0; index < line.Values.Count; index++)
            {
                var dx = X(index + 1) - location.X;
                var dy = Y(line.Values[index]) - location.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance >= best - 0.5 || distance > DpiScaling.Scale(8))
                    continue;
                best = distance;
                var item = line.ItemIds is { } ids && index < ids.Count ? $" ({ids[index]})" : string.Empty;
                text = $"{(line.IsCritical ? "Critical path " : "Path ")}{line.Label}\n"
                       + $"Component {index + 1}{item}: {line.Values[index].ToString("0.####", CultureInfo.CurrentCulture)} {_profile.UnitLabel}";
            }
        return text;
    }

    private float X(int component) => _plot.Left + (component - 1) * (float)_plot.Width / Math.Max(1, _xMax - 1);

    private float Y(double value) => (float)(_plot.Bottom - value / _yMax * _plot.Height);

    private static string Format(double value, int decimals) => value.ToString("F" + decimals, CultureInfo.CurrentCulture);

    private static int Decimals(double step) => step >= 1 ? 0 : Math.Min(4, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));

    /// <summary>A rounded axis maximum (≥ the data) and a 1-2-2.5-5 step giving about <paramref name="intervals"/> intervals.</summary>
    internal static (double Max, double Step) NiceAxis(double dataMax, int intervals = 5)
    {
        if (!double.IsFinite(dataMax) || dataMax <= 0)
            return (1, 1.0 / Math.Max(1, intervals));
        var rough = dataMax / Math.Max(1, intervals);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(x => x * magnitude).First(x => x >= rough);
        return (Math.Ceiling(dataMax / step - 1e-9) * step, step);
    }

    /// <summary>A whole-number step for the component axis giving at most about twelve labels.</summary>
    internal static int NiceIntegerStep(int count)
    {
        foreach (var step in new[] { 1, 2, 5, 10, 20, 25, 50, 100 })
            if (count / step <= 12)
                return step;
        return Math.Max(1, count / 10);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();
        base.Dispose(disposing);
    }
}
