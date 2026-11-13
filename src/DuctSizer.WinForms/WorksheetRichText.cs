using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Writes a calculation worksheet (the sections printed in the PDF report) into a rich-text box, with the
/// equations drawn in the worksheet math style. Used by the Project tab's Calculations page and by the Quick
/// Calc calculators (round W), so every on-screen worksheet reads like the report.
/// </summary>
internal static class WorksheetRichText
{
    internal static void Render(RichTextBox box, IReadOnlyList<DuctSystemPdfCalculationSection> sections, bool append = false)
    {
        if (!append)
            box.Clear();
        var heading = new Font("Segoe UI Semibold", 9.5F);
        var body = new Font("Segoe UI", 9F);
        var value = new Font("Segoe UI Semibold", 9F);
        void Write(string text, Font font, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionFont = font;
            box.SelectionColor = color;
            EngineeringMathRichText.AppendAtEnd(box, text);
        }

        var title = new Font("Segoe UI Semibold", 11F);
        var first = true;
        foreach (var section in sections)
        {
            if (section.IsTitle)
            {
                // A worksheet title; a later one (the surface-area calculation) follows a divider line.
                if (!first)
                    Write(new string('─', 34) + "\r\n", body, Color.FromArgb(120, 140, 158));
                Write(section.Heading.ToUpperInvariant() + "\r\n\r\n", title, RibbonPalette.Ink);
                first = false;
                continue;
            }
            first = false;
            Write(section.Heading.ToUpperInvariant() + "\r\n", heading, Color.FromArgb(18, 52, 96));
            foreach (var line in section.Lines)
            {
                if (line.Kind == DuctSystemPdfLineKind.Variable)
                {
                    // A variable definition: the symbol, then what it is and where its value comes from.
                    box.SelectionStart = box.TextLength;
                    EngineeringMathRichText.AppendStyledEquation(box,
                        (line.Formula + "  ", EngineeringMathRichText.MathTone.Formula));
                    Write($"    {EngineeringText.Pretty(line.HighlightValue)}\r\n", body, RibbonPalette.Caption);
                    continue;
                }
                if (line.Kind is DuctSystemPdfLineKind.TableHeader or DuctSystemPdfLineKind.TableRow or DuctSystemPdfLineKind.TableRowSelected)
                {
                    var cells = string.Join("   ", line.Cells.Where(cell => cell.Length > 0));
                    Write(EngineeringText.Pretty(cells) + "\r\n", line.Kind == DuctSystemPdfLineKind.TableRow ? body : value,
                        line.Kind == DuctSystemPdfLineKind.TableRowSelected ? Color.FromArgb(14, 111, 230) : RibbonPalette.Ink);
                    continue;
                }
                // One line per result: "A = πD²/4 = 1.2743 ft²" (the equation, an equals sign and the value on the
                // same line), with the substituted inputs under it; text lines read "Airflow Q = 1200 cfm".
                var formula = line.Formula.TrimEnd();
                var endsWithEquals = formula.EndsWith('=');
                var valueTone = line.HighlightAsInput ? EngineeringMathRichText.MathTone.Input : EngineeringMathRichText.MathTone.Solved;
                if (line.IsEquation)
                {
                    // Equations are written in the worksheet math markup; render them as the calculators do.
                    box.SelectionStart = box.TextLength;
                    var parts = new List<(string Markup, EngineeringMathRichText.MathTone Tone)>
                        { (formula, EngineeringMathRichText.MathTone.Formula) };
                    if (!string.IsNullOrWhiteSpace(line.HighlightValue))
                        parts.Add(((endsWithEquals ? " " : " = ") + EngineeringText.Pretty(line.HighlightValue), valueTone));
                    EngineeringMathRichText.AppendStyledEquation(box, parts.ToArray());
                    if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
                        Write($"    with {EngineeringText.Pretty(line.InputSubstitution)}\r\n", body, Color.FromArgb(62, 78, 94));
                    continue;
                }
                Write(EngineeringText.Pretty(formula), body, RibbonPalette.Ink);
                if (!string.IsNullOrWhiteSpace(line.HighlightValue))
                    Write((endsWithEquals ? " " : " = ") + EngineeringText.Pretty(line.HighlightValue), value,
                        line.HighlightAsInput ? Color.FromArgb(42, 110, 60) : Color.FromArgb(18, 52, 96));
                if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
                    Write($"\r\n    with {EngineeringText.Pretty(line.InputSubstitution)}", body, Color.FromArgb(62, 78, 94));
                Write("\r\n", body, RibbonPalette.Ink);
            }
            Write("\r\n", body, RibbonPalette.Ink);
        }
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }
}
