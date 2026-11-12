using System.Drawing.Drawing2D;
using System.Globalization;

namespace DuctSizer.WinForms;

/// <summary>
/// Donut chart of the system's duct segments, fittings and components (Analysis tab; the report title page draws
/// the same chart). Each segment is labelled at the side with its name and quantity and an arrow pointing to it.
/// </summary>
internal sealed class SystemSummaryDonut : Control
{
    internal static readonly Color DuctColor = Color.FromArgb(41, 110, 148);
    internal static readonly Color FittingColor = Color.FromArgb(31, 168, 184);
    internal static readonly Color ComponentColor = Color.FromArgb(140, 163, 186);
    private static readonly Color Ink = Color.FromArgb(15, 51, 79);
    private static readonly Color Muted = Color.FromArgb(92, 107, 120);

    private (int Ducts, int Fittings, int Components) _counts;

    internal SystemSummaryDonut()
    {
        Name = "AnalysisSystemSummaryDonut";
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5F);
    }

    internal (int Ducts, int Fittings, int Components) Counts => _counts;

    internal void SetCounts((int Ducts, int Fittings, int Components) counts)
    {
        if (counts == _counts)
            return;
        _counts = counts;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var segments = new[]
        {
            ("Duct segments", _counts.Ducts, DuctColor),
            ("Fittings", _counts.Fittings, FittingColor),
            ("Components", _counts.Components, ComponentColor)
        };
        var total = segments.Sum(x => x.Item2);
        // Room for the labels: the donut's radius and centre leave the widest label on each side fully visible.
        using var measureFont = new Font("Segoe UI Semibold", 8.5F);
        var labelWidth = segments.Max(x => Math.Max(TextRenderer.MeasureText(x.Item1, measureFont).Width,
            TextRenderer.MeasureText($"Quantity {x.Item2}", Font).Width)) + DpiScaling.Scale(16);
        var gap = DpiScaling.Scale(28);
        var outer = Math.Max(10f, Math.Min(ClientSize.Height / 2f - DpiScaling.Scale(18),
            (ClientSize.Width - 2 * (labelWidth + gap)) / 2f - DpiScaling.Scale(4)));
        var inner = outer * 0.58f;
        var center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
        if (total == 0)
        {
            using var empty = new Pen(Color.FromArgb(225, 231, 237), outer - inner);
            var middle = (outer + inner) / 2;
            graphics.DrawEllipse(empty, center.X - middle, center.Y - middle, middle * 2, middle * 2);
            TextRenderer.DrawText(graphics, "No items yet", Font, Rectangle.Round(new RectangleF(center.X - outer, center.Y - 10, outer * 2, 20)),
                Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        // Clockwise from 12 o'clock (GDI+ angles run clockwise from 3 o'clock).
        var start = -90f;
        var labels = new List<(string Name, int Count, Color Color, float Mid)>();
        foreach (var (name, count, color) in segments)
        {
            if (count <= 0)
                continue;
            var sweep = 360f * count / total;
            using var path = new GraphicsPath();
            path.AddArc(center.X - outer, center.Y - outer, outer * 2, outer * 2, start, sweep);
            path.AddArc(center.X - inner, center.Y - inner, inner * 2, inner * 2, start + sweep, -sweep);
            path.CloseFigure();
            using (var fill = new SolidBrush(color))
                graphics.FillPath(fill, path);
            using (var separator = new Pen(Color.White, DpiScaling.Scale(1.5f)))
                graphics.DrawPath(separator, path);
            labels.Add((name, count, color, start + sweep / 2));
            start += sweep;
        }
        var totalText = total.ToString(CultureInfo.CurrentCulture);
        using (var totalFont = new Font("Segoe UI Semibold", 15F))
        {
            var size = TextRenderer.MeasureText(totalText, totalFont);
            TextRenderer.DrawText(graphics, totalText, totalFont, new Point((int)(center.X - size.Width / 2f), (int)(center.Y - size.Height + DpiScaling.Scale(3))), Ink);
        }
        using (var smallFont = new Font("Segoe UI Semibold", 7F))
        {
            var size = TextRenderer.MeasureText("ITEMS", smallFont);
            TextRenderer.DrawText(graphics, "ITEMS", smallFont, new Point((int)(center.X - size.Width / 2f), (int)(center.Y + DpiScaling.Scale(2))), Muted);
        }

        // Labels at the sides, spread so they never overlap, each with a leader ending in an arrowhead.
        using var nameFont = new Font("Segoe UI Semibold", 8.5F);
        var lineHeight = TextRenderer.MeasureText("Ag", nameFont).Height;
        var spacing = lineHeight * 2 + DpiScaling.Scale(4);
        foreach (var right in new[] { true, false })
        {
            var side = labels.Where(x => Math.Cos(x.Mid * Math.PI / 180) >= 0 == right)
                .Select(x => (x.Name, x.Count, x.Color, x.Mid, Y: center.Y + (outer + DpiScaling.Scale(10)) * (float)Math.Sin(x.Mid * Math.PI / 180)))
                .OrderBy(x => x.Y).ToList();
            for (var index = 1; index < side.Count; index++)
                if (side[index].Y - side[index - 1].Y < spacing)
                    side[index] = side[index] with { Y = side[index - 1].Y + spacing };
            var overflow = side.Count == 0 ? 0 : side[^1].Y + lineHeight * 1.2f - ClientSize.Height;
            if (overflow > 0)
                side = side.Select(x => x with { Y = x.Y - overflow }).ToList();
            foreach (var label in side)
            {
                var radians = label.Mid * Math.PI / 180;
                var tip = new PointF(center.X + (outer + 2) * (float)Math.Cos(radians), center.Y + (outer + 2) * (float)Math.Sin(radians));
                var elbow = new PointF(center.X + (outer + DpiScaling.Scale(14)) * (float)Math.Cos(radians), label.Y);
                var textX = right ? center.X + outer + DpiScaling.Scale(28) : center.X - outer - DpiScaling.Scale(28);
                using (var leader = new Pen(Color.FromArgb(110, 126, 140), DpiScaling.Scale(1f)))
                {
                    leader.CustomEndCap = new AdjustableArrowCap(3.5f, 4.5f, true);
                    graphics.DrawLines(leader, new[] { new PointF(textX + (right ? -4 : 4), label.Y), elbow, tip });
                }
                var countText = $"Quantity {label.Count.ToString(CultureInfo.CurrentCulture)}";
                var width = Math.Max(TextRenderer.MeasureText(label.Name, nameFont).Width, TextRenderer.MeasureText(countText, Font).Width)
                            + DpiScaling.Scale(14);
                var left = right ? textX : textX - width;
                using (var swatch = new SolidBrush(label.Color))
                    graphics.FillRectangle(swatch, left, label.Y - lineHeight / 2f + 2, DpiScaling.Scale(9), DpiScaling.Scale(9));
                TextRenderer.DrawText(graphics, label.Name, nameFont, new Point((int)left + DpiScaling.Scale(13), (int)(label.Y - lineHeight / 2f)), Ink);
                TextRenderer.DrawText(graphics, countText, Font, new Point((int)left + DpiScaling.Scale(13), (int)(label.Y + lineHeight / 2f - 1)), Muted);
            }
        }
    }
}
