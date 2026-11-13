using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Worksheet lines that show how each coefficient was read from its published table: the interpolation equation with
/// the bracketing table points, inputs and output; a published point used as printed; an input held at the table edge;
/// or, where a table cannot be interpolated, the conservative (higher-loss) choice of the two neighbouring entries.
/// Shared by the report, the Selection Inspector and the Fitting Calculator.
/// </summary>
internal static class WorksheetLookups
{
    private static string N(double value) => double.IsNaN(value) ? "not published"
        : value.ToString(Math.Abs(value) >= 1000 ? "#,##0.##" : "0.####", CultureInfo.InvariantCulture);

    private static DuctSystemPdfCalculationLine L(string text, string value = "") => new(text, value);

    private static DuctSystemPdfCalculationLine E(string formula, string value, string substitution) =>
        new(formula, value, true, InputSubstitution: substitution);

    /// <summary>Worksheet markup (<c>C'_{o}</c>, <c>K_{\theta}</c>) as plain text (C'o, Kθ).</summary>
    internal static string Plain(string markup) => markup
        .Replace("\\theta", "θ", StringComparison.Ordinal).Replace("_{", string.Empty, StringComparison.Ordinal)
        .Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// The symbol of the value a look-up gives: its own name (C'o, Kθ, ...), else C when the fitting's one look-up
    /// gives the coefficient itself, else y.
    /// </summary>
    private static string OutputSymbol(FittingCalculationResult result, TableLookupStep step) =>
        !string.IsNullOrEmpty(step.Symbol) ? step.Symbol
        : result.TableLookups.Count == 1 && Math.Abs(step.Result - result.Coefficient) <= 1e-9 * Math.Max(1, Math.Abs(result.Coefficient)) ? "C"
        : "y";

    /// <summary>The look-up lines for <paramref name="result"/>, empty when no table was read.</summary>
    internal static IReadOnlyList<DuctSystemPdfCalculationLine> Lines(FittingCalculationResult result)
    {
        var lookups = result.TableLookups;
        var lines = new List<DuctSystemPdfCalculationLine>();
        if (lookups.Count == 0)
            return lines;
        lines.Add(L("Between published points a value is interpolated in a straight line: x is the input; x1 and x2 are the " +
                    "published points either side of it; y1 and y2 are the published values at those points. Never extrapolated."));
        for (var index = 0; index < lookups.Count; index++)
        {
            var step = lookups[index];
            var symbol = OutputSymbol(result, step);
            var y = Plain(symbol);
            var number = (lookups.Count > 1 ? $"Look-up {index + 1}: " : string.Empty)
                         + (string.IsNullOrEmpty(step.Meaning) ? string.Empty : $"{y}, {char.ToLowerInvariant(step.Meaning[0])}{step.Meaning[1..].TrimEnd('.')}. ");
            var axes = step.Axes;
            switch (step.Kind)
            {
                case TableLookupKind.Exact:
                    lines.Add(L($"{number}{string.Join("; ", axes.Select(axis => $"{axis.Name} = {N(axis.Value)}"))} is a published table point; " +
                                "the table value is used as printed (no interpolation or rounding).", $"{y} = {N(step.Result)}"));
                    break;
                case TableLookupKind.HeldAtEdge:
                    lines.Add(L($"{number}{axes[0].Name} = {N(axes[0].Value)} is outside the published table (edge {N(axes[0].Lower)}); " +
                                "the edge value is used without extrapolation. Review required.", $"{y} = {N(step.Result)}"));
                    break;
                case TableLookupKind.Linear:
                {
                    var axis = axes[0];
                    lines.Add(L($"{number}{axis.Name}: linear interpolation between published points {N(axis.Lower)} and {N(axis.Upper)}."
                                + (string.IsNullOrWhiteSpace(step.Note) ? string.Empty : " " + step.Note)));
                    lines.Add(E(symbol + " = y_{1} + \\frac{x - x_{1}}{x_{2} - x_{1}}(y_{2} - y_{1})", N(step.Result),
                        $"x = {N(axis.Value)}; x1 = {N(axis.Lower)}; x2 = {N(axis.Upper)}; y1 = {N(step.Values[0])}; y2 = {N(step.Values[1])}"));
                    break;
                }
                case TableLookupKind.Bilinear:
                {
                    var (row, column) = (axes[0], axes[1]);
                    var v = step.Values;
                    var rowLow = v[0] + column.Fraction * (v[1] - v[0]);
                    var rowHigh = v[2] + column.Fraction * (v[3] - v[2]);
                    lines.Add(L($"{number}two-way interpolation. Rows r: {row.Name} = {N(row.Value)} between {N(row.Lower)} and {N(row.Upper)}" +
                                $"{(row.OutsideTable ? " (outside the table: edge row used)" : string.Empty)}; columns c: {column.Name} = {N(column.Value)} " +
                                $"between {N(column.Lower)} and {N(column.Upper)}{(column.OutsideTable ? " (outside the table: edge column used)" : string.Empty)}."));
                    lines.Add(E("y_{r1} = z_{11} + \\frac{c - c_{1}}{c_{2} - c_{1}}(z_{12} - z_{11})", N(rowLow),
                        $"c = {N(column.Value)}; c1 = {N(column.Lower)}; c2 = {N(column.Upper)}; z11 = {N(v[0])}; z12 = {N(v[1])} (row r1 = {N(row.Lower)})"));
                    lines.Add(E("y_{r2} = z_{21} + \\frac{c - c_{1}}{c_{2} - c_{1}}(z_{22} - z_{21})", N(rowHigh),
                        $"z21 = {N(v[2])}; z22 = {N(v[3])} (row r2 = {N(row.Upper)})"));
                    lines.Add(E(symbol + " = y_{r1} + \\frac{r - r_{1}}{r_{2} - r_{1}}(y_{r2} - y_{r1})", N(step.Result),
                        $"r = {N(row.Value)}; r1 = {N(row.Lower)}; r2 = {N(row.Upper)}"));
                    break;
                }
                case TableLookupKind.Trilinear:
                    lines.Add(L($"{number}three-way interpolation: the straight-line rule applied along each table axis in turn (" +
                                string.Join("; ", axes.Select(axis => $"{axis.Name} = {N(axis.Value)} between {N(axis.Lower)} and {N(axis.Upper)}" +
                                                                      (axis.OutsideTable ? " (edge used)" : string.Empty))) + ")."));
                    lines.Add(L($"Published corner values: {string.Join(", ", step.Values.Select(N))}", $"{y} = {N(step.Result)}"));
                    break;
                case TableLookupKind.ConservativeColumn:
                {
                    var axis = axes[0];
                    lines.Add(L($"{number}{axis.Name} = {N(axis.Value)} lies between the published columns {N(axis.Lower)} and {N(axis.Upper)}. " +
                                "These columns are not interpolated (they end at different values), so the value was rounded to the " +
                                "conservative column: the larger coefficient of the two is used." +
                                (string.IsNullOrWhiteSpace(step.Note) ? string.Empty : " " + step.Note)));
                    lines.Add(E("C = max(C_{1}, C_{2})", N(step.Result),
                        $"C1 = {N(step.Values[0])} (column {N(axis.Lower)}); C2 = {N(step.Values[1])} (column {N(axis.Upper)})"));
                    break;
                }
            }
        }
        return lines;
    }
}
