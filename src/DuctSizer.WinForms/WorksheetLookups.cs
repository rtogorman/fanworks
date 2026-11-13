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

    /// <summary>
    /// The straight-line interpolation with the numbers substituted, in worksheet markup:
    /// y1 + (x − x1)/(x2 − x1) × (y2 − y1), e.g. "0.22 + \frac{1.25 - 1}{1.5 - 1}(0.15 - 0.22)".
    /// Negative numbers are bracketed.
    /// </summary>
    internal static string Interpolation(double y1, double x, double x1, double x2, double y2)
    {
        static string P(double value) => value < 0 ? $"({N(value)})" : N(value);
        return $"{P(y1)} + \\frac{{{P(x)} - {P(x1)}}}{{{P(x2)} - {P(x1)}}}({P(y2)} - {P(y1)})";
    }

    /// <summary>An axis name for use inside a sentence ("Elbow angle" -> "elbow angle"; "R/D" unchanged).</summary>
    private static string Lower(string name) =>
        name.Length > 1 && char.IsUpper(name[0]) && char.IsLower(name[1]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    /// <summary>The look-up lines for <paramref name="result"/>, empty when no table was read.</summary>
    internal static IReadOnlyList<DuctSystemPdfCalculationLine> Lines(FittingCalculationResult result)
    {
        var lookups = result.TableLookups;
        var lines = new List<DuctSystemPdfCalculationLine>();
        if (lookups.Count == 0)
            return lines;
        lines.Add(L("Straight-line interpolation (never extrapolated): x is the input, x1 and x2 the published points either " +
                    "side of it, y1 and y2 the published values there."));
        // The rule once in symbols; each look-up below then shows it with its numbers substituted and the result.
        if (lookups.Any(step => step.Kind is TableLookupKind.Linear or TableLookupKind.Bilinear))
            lines.Add(E("y = y_{1} + \\frac{x - x_{1}}{x_{2} - x_{1}}(y_{2} - y_{1})", string.Empty, string.Empty));
        for (var index = 0; index < lookups.Count; index++)
        {
            var step = lookups[index];
            var symbol = OutputSymbol(result, step);
            var y = Plain(symbol);
            // The value's meaning is in the variables list; here only its name.
            var number = (lookups.Count > 1 ? $"Look-up {index + 1}: " : string.Empty)
                         + (string.IsNullOrEmpty(step.Symbol) ? string.Empty : $"{y}. ");
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
                    lines.Add(E(symbol + " = " + Interpolation(step.Values[0], axis.Value, axis.Lower, axis.Upper, step.Values[1]),
                        N(step.Result), $"x = {Lower(axis.Name)} = {N(axis.Value)}"));
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
                    // Along the columns in each bracketing row, then between the two rows; each with its numbers.
                    lines.Add(E("y_{r1} = " + Interpolation(v[0], column.Value, column.Lower, column.Upper, v[1]), N(rowLow),
                        $"row r1 = {N(row.Lower)}; x = c = {N(column.Value)}"));
                    lines.Add(E("y_{r2} = " + Interpolation(v[2], column.Value, column.Lower, column.Upper, v[3]), N(rowHigh),
                        $"row r2 = {N(row.Upper)}; x = c = {N(column.Value)}"));
                    lines.Add(E(symbol + " = " + Interpolation(rowLow, row.Value, row.Lower, row.Upper, rowHigh), N(step.Result),
                        $"between rows: x = r = {N(row.Value)}"));
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
