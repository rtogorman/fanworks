using System.Drawing.Drawing2D;
using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The "Net path static pressure" graph (round X, extended in round Y): static, total and velocity pressure along every
/// path against the component number along the path. Static and total pressure are gauge values, from 0 in the source
/// space (or at the fan inlet when a path starts at the fan); static falls with each loss, jumps up at the fan, drops
/// where the air speeds up and is regained where it slows down (see <see cref="SystemPressures"/>). Velocity pressure is
/// that of the section leaving each component (0 in a space).
/// <para>Each of the three lines can be shown or hidden. With one path picked, its static pressure is the solid blue
/// line, its total pressure the dashed blue line and its velocity pressure the green line; other paths stay faint behind
/// it. With all paths shown, the critical path is orange and the others blue-grey (solid static, dashed total), and
/// velocity pressure is green. A component picked elsewhere is ringed (<see cref="HighlightItemId"/>), and clicking a
/// point raises <see cref="ItemClicked"/>.</para>
/// </summary>
internal sealed class NetStaticPressureChart : Control
{
    internal static readonly Color SelectedColor = Color.FromArgb(14, 111, 230);
    internal static readonly Color VelocityColor = Color.FromArgb(18, 140, 96);
    private static readonly Color CriticalColor = PressureProfileChart.CriticalColor;
    private static readonly Color PathColor = PressureProfileChart.PathColor;
    private static readonly Color AxisInk = Color.FromArgb(74, 91, 106);
    private static readonly Color GridLine = Color.FromArgb(228, 234, 240);
    private static readonly Color HighlightColor = Color.FromArgb(200, 30, 60);

    private readonly ToolTip _toolTip = new() { InitialDelay = 150, ReshowDelay = 50 };
    private IReadOnlyList<PathPressureProfile> _paths = Array.Empty<PathPressureProfile>();
    private int? _selected;
    private bool _imperial = true;
    private bool _showStatic = true;
    private bool _showTotal = true;
    private bool _showVelocity = true;
    private string? _highlightItemId;
    private Rectangle _plot;
    private double _yMin = -1, _yMax = 1;
    private int _xMax = 1;
    private string _hoverText = string.Empty;

    /// <summary>The three pressures that can be drawn for each station.</summary>
    internal enum Series { Static, Total, Velocity }

    internal NetStaticPressureChart()
    {
        Name = "NetStaticPressureChart";
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5F);
        Cursor = Cursors.Hand;
    }

    /// <summary>Raised when a point is clicked: the station's item ID (D-012, F-004 …).</summary>
    internal event Action<string>? ItemClicked;

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

    internal bool ShowStatic
    {
        get => _showStatic;
        set { _showStatic = value; Invalidate(); }
    }

    internal bool ShowTotal
    {
        get => _showTotal;
        set { _showTotal = value; Invalidate(); }
    }

    internal bool ShowVelocity
    {
        get => _showVelocity;
        set { _showVelocity = value; Invalidate(); }
    }

    /// <summary>The item ringed on the graph wherever it lies on a path shown (the item selected elsewhere); null for none.</summary>
    internal string? HighlightItemId
    {
        get => _highlightItemId;
        set
        {
            if (string.Equals(value, _highlightItemId, StringComparison.OrdinalIgnoreCase))
                return;
            _highlightItemId = value;
            Invalidate();
        }
    }

    private bool IsShown(Series series) => series switch
    {
        Series.Static => _showStatic,
        Series.Total => _showTotal,
        _ => _showVelocity
    };

    private static double Value(PathPressureStation station, Series series) => series switch
    {
        Series.Static => station.StaticPa,
        Series.Total => station.TotalPa,
        _ => station.VelocityPa
    };

    private double Display(double pascals) => _imperial ? UnitConversions.PaToInchesWater(pascals) : pascals;

    private string Unit => _imperial ? "in. w.c." : "Pa";

    /// <summary>The paths drawn in full: the picked one, or all of them.</summary>
    private IEnumerable<PathPressureProfile> ShownPaths => _selected is int chosen ? new[] { _paths[chosen] } : _paths;

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
        var series = Enum.GetValues<Series>().Where(IsShown).ToArray();
        if (series.Length == 0)
        {
            TextRenderer.DrawText(graphics, "Tick Static, Total or Velocity pressure to draw a line.", Font, ClientRectangle, AxisInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var values = ShownPaths.SelectMany(path => path.Stations.SelectMany(station => series.Select(s => Display(Value(station, s)))))
            .Concat(_selected is not null && _showStatic ? _paths.SelectMany(path => path.Stations.Select(s => Display(s.StaticPa))) : Array.Empty<double>())
            .Where(double.IsFinite).DefaultIfEmpty(0).ToArray();
        var lineHeight = TextRenderer.MeasureText("0", Font).Height;
        var top = lineHeight + DpiScaling.Scale(12);
        var bottom = ClientSize.Height - lineHeight * 2 - DpiScaling.Scale(12);
        var intervals = Math.Clamp((bottom - top) / DpiScaling.Scale(28), 2, 8);
        (_yMin, _yMax, var step) = NiceRange(Math.Min(0, values.Min()), Math.Max(0, values.Max()), intervals);
        _xMax = Math.Max(1, _paths.Max(x => x.Stations.Count - 1));
        // Enough decimals to show every step exactly (0.25 needs two, not "0.3").
        var decimals = 0;
        while (decimals < 4 && Math.Abs(step * Math.Pow(10, decimals) - Math.Round(step * Math.Pow(10, decimals))) > 1e-6)
            decimals++;
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
        var yTitle = $"Pressure ({Unit})";
        var state = graphics.Save();
        graphics.TranslateTransform(DpiScaling.Scale(2), _plot.Top + _plot.Height / 2f);
        graphics.RotateTransform(-90);
        var yTitleSize = graphics.MeasureString(yTitle, Font);
        using (var ink = new SolidBrush(AxisInk))
            graphics.DrawString(yTitle, Font, ink, -yTitleSize.Width / 2, 0);
        graphics.Restore(state);

        DrawHighlightBand(graphics);
        var legend = new List<(Pen Pen, string Text)>();
        using var faintPen = new Pen(Color.FromArgb(60, PathColor), DpiScaling.Scale(1.3f)) { LineJoin = LineJoin.Round };
        using var selectedStatic = new Pen(SelectedColor, DpiScaling.Scale(3f)) { LineJoin = LineJoin.Round };
        using var selectedTotal = new Pen(Color.FromArgb(190, SelectedColor), DpiScaling.Scale(1.6f)) { DashStyle = DashStyle.Dash, LineJoin = LineJoin.Round };
        using var selectedVelocity = new Pen(VelocityColor, DpiScaling.Scale(2.2f)) { LineJoin = LineJoin.Round };
        using var staticDot = new SolidBrush(SelectedColor);
        using var velocityDot = new SolidBrush(VelocityColor);
        if (_selected is int picked)
        {
            // The other paths' static pressure stays faint behind the picked path, for comparison.
            if (_showStatic)
                for (var index = 0; index < _paths.Count; index++)
                    if (index != picked)
                        DrawLine(graphics, _paths[index], Series.Static, faintPen, null, 0);
            var path = _paths[picked];
            if (_showTotal)
            {
                DrawLine(graphics, path, Series.Total, selectedTotal, null, 0);
                legend.Add((selectedTotal, "Total pressure"));
            }
            if (_showVelocity)
            {
                DrawLine(graphics, path, Series.Velocity, selectedVelocity, velocityDot, DpiScaling.Scale(2.6f));
                legend.Add((selectedVelocity, "Velocity pressure"));
            }
            if (_showStatic)
            {
                DrawLine(graphics, path, Series.Static, selectedStatic, staticDot, DpiScaling.Scale(3.4f));
                legend.Insert(0, (selectedStatic, "Static pressure"));
            }
        }
        else
        {
            using var pathStatic = new Pen(Color.FromArgb(170, PathColor), DpiScaling.Scale(1.6f)) { LineJoin = LineJoin.Round };
            using var pathTotal = new Pen(Color.FromArgb(120, PathColor), DpiScaling.Scale(1.2f)) { DashStyle = DashStyle.Dash, LineJoin = LineJoin.Round };
            using var pathVelocity = new Pen(Color.FromArgb(110, VelocityColor), DpiScaling.Scale(1.2f)) { LineJoin = LineJoin.Round };
            using var criticalStatic = new Pen(CriticalColor, DpiScaling.Scale(2.4f)) { LineJoin = LineJoin.Round };
            using var criticalTotal = new Pen(Color.FromArgb(200, CriticalColor), DpiScaling.Scale(1.5f)) { DashStyle = DashStyle.Dash, LineJoin = LineJoin.Round };
            using var criticalVelocity = new Pen(VelocityColor, DpiScaling.Scale(2f)) { LineJoin = LineJoin.Round };
            // Ordinary paths first, the critical path on top.
            foreach (var path in _paths.OrderBy(x => x.Path.IsCritical))
            {
                var critical = path.Path.IsCritical;
                if (_showTotal)
                    DrawLine(graphics, path, Series.Total, critical ? criticalTotal : pathTotal, null, 0);
                if (_showVelocity)
                    DrawLine(graphics, path, Series.Velocity, critical ? criticalVelocity : pathVelocity, null, 0);
                if (_showStatic)
                    DrawLine(graphics, path, Series.Static, critical ? criticalStatic : pathStatic, null, 0);
            }
            var hasCritical = _paths.Any(x => x.Path.IsCritical);
            var others = _paths.Count - (hasCritical ? 1 : 0);
            if (hasCritical)
                legend.Add((criticalStatic, "Path 1 (critical)"));
            if (others > 0)
                legend.Add((pathStatic, others == 1 ? (hasCritical ? "Path 2" : "Path 1") : hasCritical ? $"Paths 2 to {_paths.Count}" : "Paths"));
            if (_showStatic && _showTotal)
            {
                legend.Add((criticalStatic, "solid: static"));
                legend.Add((criticalTotal, "dashed: total"));
            }
            else if (_showTotal && !_showStatic)
                legend.Add((criticalTotal, "dashed: total pressure"));
            if (_showVelocity)
                legend.Add((criticalVelocity, "Velocity pressure"));
            DrawLegend(graphics, legend);
            DrawHighlightRings(graphics);
            return;
        }
        DrawLegend(graphics, legend);
        DrawHighlightRings(graphics);
    }

    private void DrawLegend(Graphics graphics, IReadOnlyList<(Pen Pen, string Text)> entries)
    {
        var x = _plot.Left;
        var y = DpiScaling.Scale(4) + TextRenderer.MeasureText("0", Font).Height / 2;
        foreach (var (pen, text) in entries)
            x = LegendEntry(graphics, x, y, pen, text);
    }

    private void DrawLine(Graphics graphics, PathPressureProfile path, Series series, Pen pen, Brush? dot, float radius)
    {
        var points = path.Stations.Select((station, index) => (value: Display(Value(station, series)), index))
            .Where(x => double.IsFinite(x.value))
            .Select(x => new PointF(X(x.index), Y(x.value))).ToArray();
        if (points.Length >= 2)
            graphics.DrawLines(pen, points);
        if (dot is null)
            return;
        foreach (var point in points)
            graphics.FillEllipse(dot, point.X - radius, point.Y - radius, radius * 2, radius * 2);
    }

    /// <summary>The stations of the highlighted item on the paths shown.</summary>
    private IEnumerable<(PathPressureProfile Path, PathPressureStation Station)> HighlightedStations() =>
        string.IsNullOrEmpty(_highlightItemId)
            ? Array.Empty<(PathPressureProfile, PathPressureStation)>()
            : ShownPaths.SelectMany(path => path.Stations
                .Where(station => string.Equals(station.ItemId, _highlightItemId, StringComparison.OrdinalIgnoreCase))
                .Select(station => (path, station)));

    private void DrawHighlightBand(Graphics graphics)
    {
        using var band = new SolidBrush(Color.FromArgb(28, HighlightColor));
        var half = DpiScaling.Scale(6f);
        foreach (var x in HighlightedStations().Select(item => X(item.Station.Index)).Distinct())
            graphics.FillRectangle(band, x - half, _plot.Top, half * 2, _plot.Height);
    }

    private void DrawHighlightRings(Graphics graphics)
    {
        using var ring = new Pen(HighlightColor, DpiScaling.Scale(2f));
        var radius = DpiScaling.Scale(6.5f);
        foreach (var (_, station) in HighlightedStations())
            foreach (var series in Enum.GetValues<Series>().Where(IsShown))
            {
                var value = Display(Value(station, series));
                if (!double.IsFinite(value))
                    continue;
                var x = X(station.Index);
                var y = Y(value);
                graphics.DrawEllipse(ring, x - radius, y - radius, radius * 2, radius * 2);
            }
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

    /// <summary>The point under <paramref name="location"/> on a visible line of a path shown, or null.</summary>
    internal (PathPressureProfile Path, PathPressureStation Station)? HitStation(Point location)
    {
        if (_plot.Width <= 0 || !_plot.Contains(location))
            return null;
        var index = (int)Math.Round((location.X - _plot.Left) / (double)_plot.Width * _xMax);
        var series = Enum.GetValues<Series>().Where(IsShown).ToArray();
        var best = ShownPaths.Where(path => index >= 0 && index < path.Stations.Count)
            .SelectMany(path => series.Select(s => (path, station: path.Stations[index],
                distance: Math.Abs(Y(Display(Value(path.Stations[index], s))) - location.Y))))
            .Where(item => float.IsFinite(item.distance))
            .OrderBy(item => item.distance)
            .FirstOrDefault();
        if (best.path is null || best.distance > DpiScaling.Scale(18))
            return null;
        return (best.path, best.station);
    }

    /// <summary>"Path 2, component 4: D-012" with its static, total and velocity pressure, for the point under the mouse.</summary>
    internal string HitText(Point location)
    {
        if (HitStation(location) is not { } hit)
            return string.Empty;
        var (path, station) = hit;
        var decimals = _imperial ? 3 : 0;
        string P(double pa) => Display(pa).ToString("0." + new string('0', Math.Max(1, decimals)), CultureInfo.CurrentCulture);
        return $"{path.Path.Name}, component {station.Index}: {station.ItemId}\nStatic {P(station.StaticPa)} {Unit}"
               + $" (total {P(station.TotalPa)}, velocity {P(station.VelocityPa)})\nClick to select it.";
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

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || HitStation(e.Location) is not { } hit)
            return;
        var station = hit.Station;
        HighlightItemId = station.ItemId;
        ItemClicked?.Invoke(station.ItemId);
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
