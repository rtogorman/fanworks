using System.Drawing.Drawing2D;
using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Line graph of the running pressure loss along every route (Analysis tab): x is the component number along the
/// route (1, 2, 3 …), y the pressure loss from the start of the route. Path 1 (the critical path) is drawn on top in
/// amber; the other paths in a muted blue. One path can be isolated (<see cref="IsolatedSeries"/>); clicking a point
/// of the isolated path selects that component (<see cref="SelectedPoint"/>). An item chosen elsewhere
/// (<see cref="HighlightItemId"/>) is ringed wherever it appears on the paths shown. Hovering names the point.
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
    private int? _isolated;
    private string? _highlightItemId;
    private (int Series, int Index)? _selected;

    /// <summary>Raised when the selected component (click on the isolated path) changes.</summary>
    internal event EventHandler? SelectionChanged;

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
            if (_isolated is int index && (value is null || index >= value.Series.Count))
                _isolated = null;
            _selected = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>Index into the profile's series of the one path shown alone; null shows every path.</summary>
    internal int? IsolatedSeries
    {
        get => _isolated;
        set
        {
            var valid = value is int index && _profile is not null && index >= 0 && index < _profile.Series.Count ? value : null;
            if (valid == _isolated)
                return;
            _isolated = valid;
            _selected = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>An item ID to ring on the paths shown (chosen in a table); null for none.</summary>
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

    /// <summary>The component picked on the isolated path (series index, 0-based position); null for none.</summary>
    internal (int Series, int Index)? SelectedPoint
    {
        get => _selected;
        set
        {
            _selected = value;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>The series drawn: the isolated one, or all.</summary>
    private IEnumerable<(DuctSystemPdfProfileSeries Line, int Index)> ShownSeries()
    {
        if (_profile is null)
            yield break;
        for (var index = 0; index < _profile.Series.Count; index++)
            if (_isolated is null || _isolated == index)
                yield return (_profile.Series[index], index);
    }

    /// <summary>True when <paramref name="itemId"/> lies on a path shown.</summary>
    internal bool ShowsItem(string itemId) =>
        ShownSeries().Any(item => item.Line.ItemIds?.Any(id => string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase)) == true);

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
        var shown = ShownSeries().Select(item => item.Line).ToArray();
        _xMax = Math.Max(2, shown.Max(x => x.Values.Count));
        var dataMax = shown.SelectMany(x => x.Values).DefaultIfEmpty(0).Max();
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
        using var isolatedPen = new Pen(PathColor, DpiScaling.Scale(2.6f)) { LineJoin = LineJoin.Round };
        using var criticalPen = new Pen(CriticalColor, DpiScaling.Scale(2.6f)) { LineJoin = LineJoin.Round };
        using var pathDot = new SolidBrush(PathColor);
        using var criticalDot = new SolidBrush(CriticalColor);
        var visible = ShownSeries().ToArray();
        foreach (var (line, _) in visible.Where(x => !x.Line.IsCritical))
            DrawSeries(graphics, line, _isolated is null ? pathPen : isolatedPen, pathDot,
                DpiScaling.Scale(_isolated is null ? 2.4f : 3.2f));
        foreach (var (line, _) in visible.Where(x => x.Line.IsCritical))
            DrawSeries(graphics, line, criticalPen, criticalDot, DpiScaling.Scale(3.2f));

        // An item chosen in a table: a ring wherever it lies on a path shown.
        if (!string.IsNullOrWhiteSpace(_highlightItemId))
        {
            using var ring = new Pen(Color.FromArgb(14, 111, 230), DpiScaling.Scale(2.2f));
            using var halo = new SolidBrush(Color.FromArgb(70, 14, 111, 230));
            var r = DpiScaling.Scale(7f);
            foreach (var (line, _) in visible)
                for (var index = 0; index < line.Values.Count; index++)
                    if (line.ItemIds is { } ids && index < ids.Count
                        && string.Equals(ids[index], _highlightItemId, StringComparison.OrdinalIgnoreCase))
                    {
                        var point = new PointF(X(index + 1), Y(line.Values[index]));
                        graphics.FillEllipse(halo, point.X - r, point.Y - r, r * 2, r * 2);
                        graphics.DrawEllipse(ring, point.X - r, point.Y - r, r * 2, r * 2);
                    }
        }
        // The component picked on the isolated path.
        if (_selected is { } picked && _profile.Series.Count > picked.Series && picked.Index < _profile.Series[picked.Series].Values.Count)
        {
            var line = _profile.Series[picked.Series];
            var point = new PointF(X(picked.Index + 1), Y(line.Values[picked.Index]));
            var r = DpiScaling.Scale(6f);
            using var fill = new SolidBrush(Color.White);
            using var edge = new Pen(Color.FromArgb(11, 31, 59), DpiScaling.Scale(2.4f));
            graphics.FillEllipse(fill, point.X - r, point.Y - r, r * 2, r * 2);
            graphics.DrawEllipse(edge, point.X - r, point.Y - r, r * 2, r * 2);
            using var guide = new Pen(Color.FromArgb(120, 11, 31, 59), 1f) { DashStyle = DashStyle.Dash };
            graphics.DrawLine(guide, point.X, point.Y + r, point.X, _plot.Bottom);
        }

        // Legend (top left of the plot): Path 1 is the critical path.
        var legendX = _plot.Left;
        var legendY = DpiScaling.Scale(2) + lineHeight / 2;
        if (_isolated is int only)
        {
            var line = _profile.Series[only];
            legendX = LegendEntry(graphics, legendX, legendY, line.IsCritical ? criticalPen : isolatedPen,
                $"{line.Label}{(line.IsCritical ? " (critical)" : string.Empty)}: {line.Route}");
            return;
        }
        var critical = series.FirstOrDefault(x => x.IsCritical);
        var others = series.Count(x => !x.IsCritical);
        if (critical is not null)
            legendX = LegendEntry(graphics, legendX, legendY, criticalPen, $"{critical.Label} (critical)");
        if (others > 0)
            LegendEntry(graphics, legendX, legendY, pathPen, critical is null
                ? $"Paths ({others})"
                : others == 1 ? "Path 2" : $"Paths 2 to {others + 1}");
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

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        // Only the isolated path's components can be picked (the panel beside the graph describes the pick).
        if (_isolated is not int only || _profile is null || _plot.Width <= 0)
            return;
        var line = _profile.Series[only];
        var best = double.MaxValue;
        int? nearest = null;
        for (var index = 0; index < line.Values.Count; index++)
        {
            var dx = X(index + 1) - e.X;
            var dy = Y(line.Values[index]) - e.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < best && distance <= DpiScaling.Scale(12))
            {
                best = distance;
                nearest = index;
            }
        }
        if (nearest is int picked)
            SelectedPoint = (only, picked);
    }

    /// <summary>For checks: the screen point of a series' component (0-based), in client pixels.</summary>
    internal PointF PointOf(int series, int index) =>
        _profile is null ? PointF.Empty : new PointF(X(index + 1), Y(_profile.Series[series].Values[index]));

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
        foreach (var line in ShownSeries().Select(item => item.Line).OrderByDescending(x => x.IsCritical))
            for (var index = 0; index < line.Values.Count; index++)
            {
                var dx = X(index + 1) - location.X;
                var dy = Y(line.Values[index]) - location.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance >= best - 0.5 || distance > DpiScaling.Scale(8))
                    continue;
                best = distance;
                var item = line.ItemIds is { } ids && index < ids.Count ? $" ({ids[index]})" : string.Empty;
                var total = line.Values[^1];
                var critical = _profile.Series.FirstOrDefault(x => x.IsCritical);
                var versus = line.IsCritical || critical is null || critical.Values.Count == 0
                    ? string.Empty
                    : $" ({(critical.Values[^1] - total).ToString("0.####", CultureInfo.CurrentCulture)} {_profile.UnitLabel} below the critical path)";
                var drop = line.Values[index] - (index == 0 ? 0 : line.Values[index - 1]);
                text = $"{line.Label}{(line.IsCritical ? " (critical)" : string.Empty)}: {line.Route}\n"
                       + $"Component {index + 1}{item}: {drop.ToString("0.####", CultureInfo.CurrentCulture)} {_profile.UnitLabel} drop; "
                       + $"{line.Values[index].ToString("0.####", CultureInfo.CurrentCulture)} {_profile.UnitLabel} from the start\n"
                       + $"Path total: {total.ToString("0.####", CultureInfo.CurrentCulture)} {_profile.UnitLabel}{versus}";
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
