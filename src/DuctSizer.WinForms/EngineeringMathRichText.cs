using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace DuctSizer.WinForms;

/// <summary>
/// Renders the small LaTeX-style subset used by the engineering worksheets as
/// display math. Equations are inserted as sharp vector-image runs so fractions can be
/// built vertically with full-size numerators and denominators instead of a
/// diagonal fraction slash. A hidden plain-text copy keeps the worksheet
/// searchable and accessible to automated verification.
/// </summary>
internal static class EngineeringMathRichText
{
    internal enum MathTone
    {
        Formula,
        Input,
        Solved
    }

    private const float BasePointSize = 10.8F;
    private static readonly Color EquationColor = Color.FromArgb(39, 58, 75);
    private static readonly Color InputColor = Color.FromArgb(190, 44, 44);
    private static readonly Color SolvedColor = Color.FromArgb(0, 105, 154);

    private abstract record MathElement(MathTone Tone);
    private sealed record MathText(string Value, MathTone Tone, bool Symbol = false) : MathElement(Tone);
    private sealed record MathScript(IReadOnlyList<MathElement> Value, bool Superscript, MathTone Tone)
        : MathElement(Tone);
    private sealed record MathFraction(IReadOnlyList<MathElement> Numerator,
        IReadOnlyList<MathElement> Denominator, MathTone Tone) : MathElement(Tone);
    private sealed record MathRoot(IReadOnlyList<MathElement> Radicand, MathTone Tone) : MathElement(Tone);
    private sealed record Metrics(float Width, float Ascent, float Descent)
    {
        internal float Height => Ascent + Descent;
    }

    internal static void AppendEquation(RichTextBox box, params (string Markup, bool Highlight)[] parts)
        => AppendStyledEquation(box, parts.Select(part =>
            (part.Markup, part.Highlight ? MathTone.Input : MathTone.Formula)).ToArray());

    internal static void AppendStyledEquation(RichTextBox box, params (string Markup, MathTone Tone)[] parts)
    {
        var elements = new List<MathElement>();
        foreach (var part in parts)
        {
            var index = 0;
            elements.AddRange(ParseMath(part.Markup, ref index, false, part.Tone));
        }

        var worksheetBackground = box.BackColor;
        using var image = RenderEquation(elements, Math.Max(240, box.ClientSize.Width - 28), worksheetBackground);
        InsertImage(box, image, PlainText(string.Concat(parts.Select(part => part.Markup))), worksheetBackground);
    }

    private static Bitmap RenderEquation(IReadOnlyList<MathElement> elements, int maximumWidth, Color background)
    {
        const float renderDpi = 192F;
        var maximumPixelWidth = maximumWidth * renderDpi / 96F;
        var pointSize = BasePointSize;
        using var measuring = new Bitmap(1, 1);
        measuring.SetResolution(renderDpi, renderDpi);
        using var measureGraphics = Graphics.FromImage(measuring);
        ConfigureGraphics(measureGraphics);
        var metrics = Measure(measureGraphics, elements, pointSize);
        if (metrics.Width > maximumPixelWidth && metrics.Width > 0)
        {
            pointSize = Math.Max(8.8F, pointSize * maximumPixelWidth / metrics.Width);
            metrics = Measure(measureGraphics, elements, pointSize);
        }

        var padding = renderDpi / 16F;
        var width = Math.Max(8, (int)Math.Ceiling(Math.Min(maximumPixelWidth, metrics.Width) + padding));
        var height = Math.Max(36, (int)Math.Ceiling(metrics.Height + padding));
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(renderDpi, renderDpi);
        using var graphics = Graphics.FromImage(bitmap);
        ConfigureGraphics(graphics);
        graphics.Clear(background);
        Draw(graphics, elements, padding / 2, padding / 2 + metrics.Ascent, pointSize);
        return bitmap;
    }

    private static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    private static IReadOnlyList<MathElement> ParseMath(string markup, ref int index,
        bool stopAtBrace, MathTone tone)
    {
        var elements = new List<MathElement>();
        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length == 0) return;
            elements.Add(new MathText(text.ToString().Replace('-', '−'), tone));
            text.Clear();
        }

        while (index < markup.Length)
        {
            var current = markup[index];
            if (current == '}' && stopAtBrace)
            {
                index++;
                break;
            }
            if (current is '_' or '^' && index + 1 < markup.Length && markup[index + 1] == '{')
            {
                Flush();
                var superscript = current == '^';
                index += 2;
                elements.Add(new MathScript(ParseMath(markup, ref index, true, tone), superscript, tone));
                continue;
            }
            if (current == '\\')
            {
                Flush();
                var start = ++index;
                while (index < markup.Length && char.IsLetter(markup[index])) index++;
                var command = markup[start..index];
                if (command == "frac" && index < markup.Length && markup[index] == '{')
                {
                    index++;
                    var numerator = ParseMath(markup, ref index, true, tone);
                    if (index < markup.Length && markup[index] == '{') index++;
                    var denominator = ParseMath(markup, ref index, true, tone);
                    elements.Add(new MathFraction(numerator, denominator, tone));
                    continue;
                }
                if (command == "sqrt" && index < markup.Length && markup[index] == '{')
                {
                    index++;
                    elements.Add(new MathRoot(ParseMath(markup, ref index, true, tone), tone));
                    continue;
                }

                var symbol = command switch
                {
                    "rho" => "ρ", "mu" => "μ", "epsilon" => "ε", "pi" => "π",
                    "Delta" => "Δ", "theta" => "θ", _ => string.Empty
                };
                elements.Add(symbol.Length > 0
                    ? new MathText(symbol, tone, Symbol: true)
                    : new MathText(command switch
                    {
                        "times" => " × ", "cdot" => " · ", _ => command
                    }, tone));
                continue;
            }
            text.Append(current);
            index++;
        }
        Flush();
        return elements;
    }

    private static Metrics Measure(Graphics graphics, IReadOnlyList<MathElement> elements, float pointSize)
    {
        var width = 0F;
        var ascent = 0F;
        var descent = 0F;
        foreach (var element in elements)
        {
            var metrics = Measure(graphics, element, pointSize);
            width += metrics.Width;
            ascent = Math.Max(ascent, metrics.Ascent);
            descent = Math.Max(descent, metrics.Descent);
        }
        return new Metrics(width, ascent, descent);
    }

    private static Metrics Measure(Graphics graphics, MathElement element, float pointSize)
    {
        switch (element)
        {
            case MathText text:
            {
                using var font = EquationFont(pointSize, text.Tone != MathTone.Formula && !text.Symbol);
                var width = graphics.MeasureString(text.Value, font, int.MaxValue,
                    StringFormat.GenericTypographic).Width;
                var (ascent, descent) = FontMetrics(font, graphics.DpiY);
                return new Metrics(width, ascent, descent);
            }
            case MathScript script:
            {
                var child = Measure(graphics, script.Value, pointSize * .74F);
                var lift = pointSize * (script.Superscript ? .58F : .12F);
                return script.Superscript
                    ? new Metrics(child.Width, child.Ascent + lift, Math.Max(0, child.Descent - lift))
                    : new Metrics(child.Width, Math.Max(0, child.Ascent - lift), child.Descent + lift);
            }
            case MathFraction fraction:
            {
                var numerator = Measure(graphics, fraction.Numerator, pointSize);
                var denominator = Measure(graphics, fraction.Denominator, pointSize);
                var gap = Math.Max(2F, pointSize * .22F);
                return new Metrics(Math.Max(numerator.Width, denominator.Width) + 8,
                    numerator.Height + gap + 1, denominator.Height + gap + 1);
            }
            case MathRoot root:
            {
                var child = Measure(graphics, root.Radicand, pointSize);
                return new Metrics(child.Width + pointSize * .95F, child.Ascent + pointSize * .16F,
                    child.Descent + pointSize * .05F);
            }
            default:
                return new Metrics(0, 0, 0);
        }
    }

    private static float Draw(Graphics graphics, IReadOnlyList<MathElement> elements,
        float x, float baseline, float pointSize)
    {
        var current = x;
        foreach (var element in elements)
            current += Draw(graphics, element, current, baseline, pointSize);
        return current - x;
    }

    private static float Draw(Graphics graphics, MathElement element, float x, float baseline, float pointSize)
    {
        switch (element)
        {
            case MathText text:
            {
                using var font = EquationFont(pointSize, text.Tone != MathTone.Formula && !text.Symbol);
                using var brush = new SolidBrush(ColorFor(text.Tone));
                var metrics = Measure(graphics, element, pointSize);
                graphics.DrawString(text.Value, font, brush, x, baseline - metrics.Ascent,
                    StringFormat.GenericTypographic);
                return metrics.Width;
            }
            case MathScript script:
            {
                var childSize = pointSize * .74F;
                var offset = pointSize * (script.Superscript ? .58F : .12F);
                Draw(graphics, script.Value, x, baseline + (script.Superscript ? -offset : offset), childSize);
                return Measure(graphics, script.Value, childSize).Width;
            }
            case MathFraction fraction:
            {
                var numerator = Measure(graphics, fraction.Numerator, pointSize);
                var denominator = Measure(graphics, fraction.Denominator, pointSize);
                var width = Math.Max(numerator.Width, denominator.Width) + 8;
                var gap = Math.Max(2F, pointSize * .22F);
                var barY = baseline - 1;
                Draw(graphics, fraction.Numerator, x + (width - numerator.Width) / 2,
                    barY - gap - numerator.Descent, pointSize);
                Draw(graphics, fraction.Denominator, x + (width - denominator.Width) / 2,
                    barY + gap + denominator.Ascent, pointSize);
                using var pen = new Pen(ColorFor(fraction.Tone), 1.15F);
                graphics.DrawLine(pen, x + 2, barY, x + width - 2, barY);
                return width;
            }
            case MathRoot root:
            {
                var child = Measure(graphics, root.Radicand, pointSize);
                var rootWidth = pointSize * .95F;
                using var pen = new Pen(ColorFor(root.Tone), Math.Max(1.2F, pointSize * .09F))
                    { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                var top = baseline - child.Ascent - pointSize * .10F;
                graphics.DrawLines(pen, new[]
                {
                    new PointF(x + pointSize * .03F, baseline - pointSize * .04F),
                    new PointF(x + pointSize * .22F, baseline + pointSize * .18F),
                    new PointF(x + pointSize * .47F, top),
                    new PointF(x + rootWidth + child.Width + pointSize * .04F, top)
                });
                Draw(graphics, root.Radicand, x + rootWidth, baseline, pointSize);
                return rootWidth + child.Width;
            }
            default:
                return 0;
        }
    }

    private static Font EquationFont(float pointSize, bool bold) =>
        new("Cambria Math", pointSize, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);

    private static Color ColorFor(MathTone tone) => tone switch
    {
        MathTone.Input => InputColor,
        MathTone.Solved => SolvedColor,
        _ => EquationColor
    };

    private static (float Ascent, float Descent) FontMetrics(Font font, float dpi)
    {
        var family = font.FontFamily;
        var style = font.Style;
        var em = family.GetEmHeight(style);
        var pixels = font.SizeInPoints * dpi / 72F;
        return (pixels * family.GetCellAscent(style) / em, pixels * family.GetCellDescent(style) / em);
    }

    private static void InsertImage(RichTextBox box, Bitmap image, string accessibleText, Color background)
    {
        var metafileBytes = ToEnhancedMetafile(image, background);
        var hex = Convert.ToHexString(metafileBytes);
        var safeText = EscapeRtf(accessibleText);
        var rtf = "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0\\fnil Segoe UI;}}" +
                  $"{{\\pict\\emfblip\\picw{image.Width}\\pich{image.Height}" +
                  $"\\picwgoal{Math.Round(image.Width * 1440D / image.HorizontalResolution):0}" +
                  $"\\pichgoal{Math.Round(image.Height * 1440D / image.VerticalResolution):0} {hex}}}" +
                  $"{{\\v\\f0\\fs1 {safeText}}}\\par}}";
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectedRtf = rtf;
        box.SelectionColor = box.ForeColor;
        box.SelectionFont = box.Font;
    }

    private static byte[] ToEnhancedMetafile(Bitmap image, Color background)
    {
        using var stream = new MemoryStream();
        using var reference = Graphics.FromHwnd(IntPtr.Zero);
        var hdc = reference.GetHdc();
        try
        {
            using (var metafile = new Metafile(stream, hdc,
                       new Rectangle(0, 0, image.Width, image.Height),
                       MetafileFrameUnit.Pixel, EmfType.EmfPlusDual))
            using (var graphics = Graphics.FromImage(metafile))
            {
                graphics.Clear(background);
                graphics.DrawImageUnscaled(image, 0, 0);
            }
        }
        finally
        {
            reference.ReleaseHdc(hdc);
        }
        return stream.ToArray();
    }

    private static string PlainText(string markup)
    {
        var value = markup.Replace("\\frac", string.Empty)
            .Replace("\\sqrt", "√")
            .Replace("\\rho", "ρ").Replace("\\mu", "μ").Replace("\\epsilon", "ε")
            .Replace("\\pi", "π").Replace("\\Delta", "Δ").Replace("\\theta", "θ")
            .Replace("\\times", "×").Replace("\\cdot", "·")
            .Replace("^{2}", "²").Replace("^{3}", "³")
            .Replace("^{", "^").Replace("_{", "_")
            .Replace("}", string.Empty).Replace("{", string.Empty);
        return value.Replace('-', '−');
    }

    private static string EscapeRtf(string value) => value.Replace("\\", "\\\\")
        .Replace("{", "\\{").Replace("}", "\\}")
        .Replace("\r", string.Empty).Replace("\n", "\\line ");
}
