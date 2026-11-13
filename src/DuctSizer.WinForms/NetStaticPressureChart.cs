using System.Drawing.Drawing2D;
using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The Project tab's "Net path static pressure" graph (round X): gauge static pressure along every path, from 0 in the
/// source space (or at the fan inlet when a path starts at the fan), against the component number along the path.
/// Static pressure falls with each loss, jumps up at the fan, drops where the air speeds up (a contraction, the entry
/// into a duct) and is regained where it slows down (an expansion); see <see cref="SystemPressures"/>. One path can be
/// picked: it is drawn in blue over the others (shown faint), with its total pressure as a dashed line.
/// </summary>
internal sealed class NetStaticPressureChart : Control
{
    internal static readonly Color SelectedColor = Color.FromArgb(14, 111, 230);
    private static readonly Color CriticalColor = PressureProfileChart.CriticalColor;
    private static readonly Color PathColor = PressureProfileChart.PathColor;
    private static readonly Color AxisInk = Color.FromArgb(74, 91, 106);
    private static readonly Color GridLine = Color.FromArgb(228, 234, 240);

    private readonly ToolTip _toolTip = new() { InitialDelay = 150, ReshowDelay = 50 };
    private IReadOnlyList<PathPressureProfile> _paths = Array.Empty<PathPressureProfile>();
    private int? _selected;
    private bool _imperial = true;
    private Rectangle _plot;
    private double _yMin = -1, _yMax = 1;
    private int _xMax = 1;
    private string _hoverText = string.Empty;

    internal NetStaticPressureChart()
    {
        Name = "NetStaticPressureChart";
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5F);
    }

    internal string EmptyMessage { get; set; } = "Connect the system components to see the static pressure along each path.";

    /// <summary>The paths, in path-number order (Path 1, the critical path, first).</summary>
    internal IReadOnlyList<PathPressureProfile> Paths => _paths;

    internal void SetPaths(IReadOnlyList<PathPressureProfile> paths, bool imperial)
    {
        _paths = paths.OrderBy(x => x.Path.Number).ToArray();
        _imperial = imperial;
        if (_selected is int index && index >= _paths.Count)
            _selected = null;
        Invalidate();
    }

    /// <summary>Index into <see cref="Paths"/> of the path drawn in blue; null draws every path alike.</summary>
    internal int? SelectedPath
    {
        get => _selected;
        set
        {
            var valid = value is int index && index >= 0 && index < _paths.Count ? value : null;
            if (valid == _selected)
                return;
            _selected = valid;
            Invalidate();
        }
    }

    private double Display(double pascals) => _imperial ? UnitConversions.PaToInchesWater(pascals) : pascals;

    private string Unit => _imperial ? "in. w.c." : "Pa";

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (_paths.Count == 0 || _paths.All(x => x.Stations.Count < 2))
        {
            TextRenderer.DrawText(graphics, EmptyMessage, Font, ClientRectangle, AxisInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var values = _paths.SelectMany(path => path.Stations.Select(station => Display(station.StaticPa)))
            .Concat(_selected is int chosen ? _paths[chosen].Stations.Select(station => Display(station.TotalPa)) : Array.Empty<double>())
            .Where(double.IsFinite).DefaultIfEmpty(0).ToArray();
        var lineHeight = TextRenderer.MeasureText("0", Font).Height;
        var top = lineHeight + DpiScaling.Scale(12);
        var bottom = ClientSize.Height - lineHeight * 2 - DpiScaling.Scale(12);
        var intervals = Math.Clamp((bottom - top) / DpiScaling.Scale(28), 2, 8);
        (_yMin, _yMax, var step) = NiceRange(Math.Min(0, values.Min()), Math.Max(0, values.Max()), intervals);
        _xMax = Math.Max(1, _paths.Max(x => x.Stations.Count - 1));
        var decimals = step >= 1 ? 0 : Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 1, 4);
        var labels = new List<double>();
        for (var value = _yMin; value <= _yMax + step * 0.01; value += step)
            labels.Add(Math.Round(value, 6));
        var labelWidth = labels.Max(v => TextRenderer.MeasureText(Format(v, decimals), Font).Width);
        _plot = Rectangle.FromLTRB(labelWidth + lineHeight + DpiScaling.Scale(16), top,
            ClientSize.Width - DpiScaling.Scale(14), bottom);
        if (_plot.Width < 60 || _plot.Height < 30)
            return;

        using var gridPen = new Pen(GridLine, 1f);
        using var axisPen = new Pen(Color.FromArgb(160, 176, 190), 1f);
        using var zeroPen = new Pen(Color.FromArgb(120, 140, 158), 1.2f) { DashStyle = DashStyle.Dash };
        foreach (var value in labels)
        {
            var y = Y(value);
            graphics.DrawLine(Math.Abs(value) < step * 1e-6 ? zeroPen : gridPen, _plot.Left, y, _plot.Right, y);
            var text = Format(value, decimals);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point(_plot.Left - size.Width - DpiScaling.Scale(4), (int)(y - size.Height / 2f)), AxisInk);
        }
        var xStep = PressureProfileChart.NiceIntegerStep(_xMax + 1);
        for (var x = 0; x <= _xMax; x += xStep)
        {
            var px = X(x);
            graphics.DrawLine(gridPen, px, _plot.Top, px, _plot.Bottom);
            var text = x.ToString(CultureInfo.CurrentCulture);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point((int)(px - size.Width / 2f), _plot.Bottom + DpiScaling.Scale(3)), AxisInk);
        }
        graphics.DrawLine(axisPen, _plot.Left, _plot.Top, _plot.Left, _plot.Bottom);
        var xTitle = "Component number along the path (0 = start)";
        var xTitleSize = TextRenderer.MeasureText(xTitle, Font);
        TextRenderer.DrawText(graphics, xTitle, Font,
            new Point(_plot.Left + (_plot.Width - xTitleSize.Width) / 2, _plot.Bottom + lineHeight + DpiScaling.Scale(6)), AxisInk);
        var yTitle = $"Static pressure, gauge ({Unit})";
        var state = graphics.Save();
        graphics.TranslateTransform(DpiScaling.Scale(2), _plot.Top + _plot.Height / 2f);
        graphics.RotateTransform(-90);
        var yTitleSize = graphics.MeasureString(yTitle, Font);
        using (var ink = new SolidBrush(AxisInk))
            graphics.DrawString(yTitle, Font, ink, -yTitleSize.Width / 2, 0);
        graphics.Restore(state);

        using var faintPen = new Pen(Color.FromArgb(70, PathColor), DpiScaling.Scale(1.4f)) { LineJoin = LineJoin.Round };
        using var pathPen = new Pen(Color.FromArgb(170, PathColor), DpiScaling.Scale(1.6f)) { LineJoin = LineJoin.Round };
        using var criticalPen = new Pen(CriticalColor, DpiScaling.Scale(2.4f)) { LineJoin = LineJoin.Round };
        using var selectedPen = new Pen(SelectedColor, DpiScaling.Scale(3f)) { LineJoin = LineJoin.Round };
        using var totalPen = new Pen(Color.FromArgb(160, SelectedColor), DpiScaling.Scale(1.4f)) { DashStyle = DashStyle.Dash };
        using var selectedDot = new SolidBrush(SelectedColor);
        for (var index = 0; index < _paths.Count; index++)
        {
            if (index == _selected)
                continue;
            var pen = _selected is not null ? faintPen : _paths[index].Path.IsCritical ? criticalPen : pathPen;
            DrawLine(graphics, _paths[index].Stations.Select(s => Display(s.StaticPa)).ToArray(), pen, null, 0);
        }
        if (_selected is null && _paths.FirstOrDefault(x => x.Path.IsCritical) is { } critical)
            DrawLine(graphics, critical.Stations.Select(s => Display(s.StaticPa)).ToArray(), criticalPen, null, 0);
        if (_selected is int picked)
        {
            var path = _paths[picked];
            DrawLine(graphics, path.Stations.Select(s => Display(s.TotalPa)).ToArray(), totalPen, null, 0);
            DrawLine(graphics, path.Stations.Select(s => Display(s.StaticPa)).ToArray(), selectedPen, selectedDot, DpiScaling.Scale(3.4f));
        }

        // Legend.
        var legendX = _plot.Left;
        var legendY = DpiScaling.Scale(4) + lineHeight / 2;
        if (_selected is int only)
        {
            var path = _paths[only].Path;
            legendX = LegendEntry(graphics, legendX, legendY, selectedPen, $"{path.Name}{(path.IsCritical ? " (critical)" : string.Empty)}: {path.Route}, static");
            LegendEntry(graphics, legendX, legendY, totalPen, "total pressure");
            return;
        }
        legendX = LegendEntry(graphics, legendX, legendY, criticalPen, "Path 1 (critical)");
        if (_paths.Count > 1)
            LegendEntry(graphics, legendX, legendY, pathPen, _paths.Count == 2 ? "Path 2" : $"Paths 2 to {_paths.Count}");
    }

    private void DrawLine(Graphics graphics, IReadOnlyList<double> values, Pen pen, Brush? dot, float radius)
    {
        var points = values.Select((value, index) => (value, index)).Where(x => double.IsFinite(x.value))
            .Select(x => new PointF(X(x.index), Y(x.value))).ToArray();
        if (points.Length >= 2)
            graphics.DrawLines(pen, points);
        if (dot is null)
            return;
        foreach (var point in points)
            graphics.FillEllipse(dot, point.X - radius, point.Y - radius, radius * 2, radius * 2);
    }

    private int LegendEntry(Graphics graphics, int x, int y, Pen pen, string text)
    {
        var swatch = DpiScaling.Scale(18);
        graphics.DrawLine(pen, x, y, x + swatch, y);
        var size = TextRenderer.MeasureText(text, Font);
        TextRenderer.DrawText(graphics, text, Font, new Point(x + swatch + DpiScaling.Scale(4), y - size.Height / 2), AxisInk);
        return x + swatch + size.Width + DpiScaling.Scale(16);
    }

    private float X(int index) => _plot.Left + (float)index / Math.Max(1, _xMax) * _plot.Width;

    private float Y(double value) => _plot.Bottom - (float)((value - _yMin) / Math.Max(1e-12, _yMax - _yMin) * _plot.Height);

    private static string Format(double value, int decimals) =>
        value.ToString("0." + new string('0', decimals), CultureInfo.CurrentCulture).Replace("-0.", "−0.").Replace("-", "−");

    /// <summary>A rounded axis range covering [min, max] (which includes 0) in about <paramref name="intervals"/> steps.</summary>
    internal static (double Min, double Max, double Step) NiceRange(double min, double max, int intervals)
    {
        var span = Math.Max(max - min, 1e-6);
        var raw = span / Math.Max(1, intervals);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = new[] { 1.0, 2.0, 2.5, 5.0, 10.0 }.Select(x => x * magnitude).First(x => x >= raw - 1e-12);
        return (Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    /// <summary>"Path 2, component 4: D-012, static 0.215 in. w.c. (total 0.247)" for the station under the mouse.</summary>
    internal string HitText(Point location)
    {
        if (_plot.Width <= 0 || !_plot.Contains(location))
            return string.Empty;
        var index = (int)Math.Round((location.X - _plot.Left) / (double)_plot.Width * _xMax);
        var candidates = _selected is int chosen ? new[] { _paths[chosen] } : _paths.ToArray();
        var best = candidates.Where(path => index >= 0 && index < path.Stations.Count)
            .Select(path => (path, station: path.Stations[index]))
            .OrderBy(item => Math.Abs(Y(Display(item.station.StaticPa)) - location.Y))
            .FirstOrDefault();
        if (best.path is null || Math.Abs(Y(Display(best.station.StaticPa)) - location.Y) > DpiScaling.Scale(18))
            return string.Empty;
        var decimals = _imperial ? 3 : 0;
        string P(double pa) => Display(pa).ToString("0." + new string('0', Math.Max(1, decimals)), CultureInfo.CurrentCulture);
        return $"{best.path.Path.Name}, component {index}: {best.station.ItemId}\nStatic {P(best.station.StaticPa)} {Unit}"
               + $" (total {P(best.station.TotalPa)}, velocity {P(best.station.VelocityPa)})";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var text = HitText(e.Location);
        if (text == _hoverText)
            return;
        _hoverText = text;
        if (string.IsNullOrEmpty(text))
            _toolTip.Hide(this);
        else
            _toolTip.Show(text, this, e.X + DpiScaling.Scale(14), e.Y + DpiScaling.Scale(10));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverText = string.Empty;
        _toolTip.Hide(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();
        base.Dispose(disposing);
    }
}
