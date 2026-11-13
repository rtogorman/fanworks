using System.Globalization;

namespace DuctSizer.Core;

/// <summary>
/// Records table lookups whose input fell outside the published (digitized) range during one coefficient
/// calculation. The lookup helpers hold such inputs at the table edge; this log makes that visible on the result
/// instead of letting an edge value pass silently as an interpolated value.
/// </summary>
/// <remarks>
/// A log is opened per calculation with <see cref="Begin"/> and is thread-local, so nested or concurrent
/// calculations do not mix entries. <see cref="Note"/> is a no-op when no log is open.
/// </remarks>
internal sealed class TableRangeLog : IDisposable
{
    [ThreadStatic] private static TableRangeLog? _current;

    private readonly TableRangeLog? _outer;
    private readonly List<(double Value, double Minimum, double Maximum)> _events = new();
    private readonly List<TableLookupStep> _lookups = new();
    private readonly List<CoefficientFactor> _factors = new();
    [ThreadStatic] private static Stack<(string Symbol, string Meaning)>? _names;

    private TableRangeLog(TableRangeLog? outer) => _outer = outer;

    /// <summary>Opens a log for the current thread. Dispose it to restore the previous log.</summary>
    public static TableRangeLog Begin()
    {
        var log = new TableRangeLog(_current);
        _current = log;
        return log;
    }

    /// <summary>Records <paramref name="value"/> if it lies outside [<paramref name="minimum"/>, <paramref name="maximum"/>].</summary>
    public static void Note(double value, double minimum, double maximum)
    {
        if (_current is null || !double.IsFinite(value))
            return;
        var tolerance = 1e-9 * Math.Max(1.0, Math.Abs(maximum - minimum));
        if (value < minimum - tolerance || value > maximum + tolerance)
            _current._events.Add((value, minimum, maximum));
    }

    /// <summary>
    /// Records one table look-up (how the value was taken from the published table) so the worksheet can show the
    /// interpolation with its inputs and output. Axis names are filled in later by <see cref="Lookups"/>.
    /// </summary>
    public static void Lookup(TableLookupStep step)
    {
        if (_current is null)
            return;
        if (string.IsNullOrEmpty(step.Symbol) && _names is { Count: > 0 } names)
            step = step with { Symbol = names.Peek().Symbol, Meaning = names.Peek().Meaning };
        _current._lookups.Add(step);
    }

    /// <summary>
    /// Evaluates one named factor of a fitting coefficient (for example the base coefficient C'o or the angle factor
    /// Kθ of an elbow, where C = Kθ × C'o). Table look-ups made while evaluating it carry its symbol, and the factor
    /// and its value are recorded so the worksheet can show the combining equation with every factor substituted.
    /// </summary>
    /// <param name="symbol">Symbol in worksheet markup, e.g. <c>C'_{o}</c> or <c>K_{\theta}</c>.</param>
    /// <param name="meaning">What the factor is and where it comes from.</param>
    public static double Named(string symbol, string meaning, Func<double> evaluate)
    {
        _names ??= new Stack<(string, string)>();
        _names.Push((symbol, meaning));
        double value;
        try
        {
            value = evaluate();
        }
        finally
        {
            _names.Pop();
        }
        _current?._factors.Add(new CoefficientFactor(symbol, meaning, value));
        return value;
    }

    /// <summary>The named factors (see <see cref="Named"/>) in calculation order; empty for single-value fittings.</summary>
    public IReadOnlyList<CoefficientFactor> Factors => _factors.ToArray();

    /// <summary>The recorded look-ups in calculation order, with each axis named after the matching input.</summary>
    public IReadOnlyList<TableLookupStep> Lookups(FittingDefinition definition, IReadOnlyDictionary<string, double> inputs) =>
        _lookups.Select(step => step with
        {
            Axes = step.Axes.Select(axis => string.IsNullOrEmpty(axis.Name)
                ? axis with
                {
                    Name = inputs
                        .Where(pair => !string.Equals(pair.Key, "path", StringComparison.OrdinalIgnoreCase)
                                       && Math.Abs(pair.Value - axis.Value) <= 1e-12 * Math.Max(1.0, Math.Abs(axis.Value)))
                        .Select(pair => InputName(definition, pair.Key))
                        .FirstOrDefault() ?? "Table input"
                }
                : axis).ToArray()
        }).ToArray();

    /// <summary>Plain-language warnings, naming the input when its value can be matched to one.</summary>
    public IReadOnlyList<string> Describe(FittingDefinition definition, IReadOnlyDictionary<string, double> inputs)
    {
        var warnings = new List<string>();
        foreach (var (value, minimum, maximum) in _events)
        {
            var name = inputs
                .Where(pair => !string.Equals(pair.Key, "path", StringComparison.OrdinalIgnoreCase)
                               && Math.Abs(pair.Value - value) <= 1e-12 * Math.Max(1.0, Math.Abs(value)))
                .Select(pair => InputName(definition, pair.Key))
                .FirstOrDefault() ?? "A table input";
            var text = string.Format(CultureInfo.InvariantCulture,
                "{0} = {1:0.###} is outside the published table range {2:0.###} to {3:0.###}; the table edge value was used.",
                name, value, minimum, maximum);
            if (!warnings.Contains(text, StringComparer.Ordinal))
                warnings.Add(text);
        }
        return warnings;
    }

    public void Dispose() => _current = _outer;

    private static string InputName(FittingDefinition definition, string key) =>
        definition.Inputs.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
        ?? key.ToLowerInvariant() switch
        {
            "qbqc" => "Branch/common airflow ratio Qb/Qc",
            "qsqc" => "Straight/common airflow ratio Qs/Qc",
            "qbqs" => "Branch/straight airflow ratio Qb/Qs",
            "abac" => "Branch/common area ratio Ab/Ac",
            "asac" => "Straight/common area ratio As/Ac",
            "abas" => "Branch/straight area ratio Ab/As",
            "vbvc" => "Branch/common velocity ratio Vb/Vc",
            "vsvc" => "Straight/common velocity ratio Vs/Vc",
            "vbvs" => "Branch/straight velocity ratio Vb/Vs",
            "vcfpm" => "Common velocity (fpm)",
            _ => key
        };
}

/// <summary>How a coefficient was taken from a published table.</summary>
public enum TableLookupKind
{
    /// <summary>The input is a published table point; the table value is used as printed.</summary>
    Exact,
    /// <summary>Straight-line interpolation between two published points on one axis.</summary>
    Linear,
    /// <summary>Interpolation on two axes: along the columns on the two bracketing rows, then between the rows.</summary>
    Bilinear,
    /// <summary>Interpolation on three axes (the same straight-line rule applied on each axis in turn).</summary>
    Trilinear,
    /// <summary>The input is outside the published table; the edge value is used (no extrapolation).</summary>
    HeldAtEdge,
    /// <summary>
    /// The table cannot be interpolated between two published columns, so both are evaluated and the larger
    /// (conservative, higher-loss) coefficient is used.
    /// </summary>
    ConservativeColumn
}

/// <summary>One axis of a table look-up: the input value and the published points either side of it.</summary>
/// <param name="Name">The input's name (filled in from the fitting's inputs; empty while recording).</param>
/// <param name="Value">The input value used for the look-up (before any holding at the table edge).</param>
/// <param name="Lower">The published point at or below the value.</param>
/// <param name="Upper">The published point at or above the value.</param>
/// <param name="OutsideTable">True when the value is outside the published range and was held at the edge.</param>
public sealed record TableLookupAxis(string Name, double Value, double Lower, double Upper, bool OutsideTable)
{
    /// <summary>Position of the value between the two points (0 at Lower, 1 at Upper).</summary>
    public double Fraction => Math.Abs(Upper - Lower) < 1e-15 ? 0 : (Math.Clamp(Value, Math.Min(Lower, Upper), Math.Max(Lower, Upper)) - Lower) / (Upper - Lower);
}

/// <summary>
/// One table look-up. <see cref="Values"/> holds the published values used: Linear [y1, y2]; Bilinear
/// [z(r1,c1), z(r1,c2), z(r2,c1), z(r2,c2)] for axes [row, column]; Trilinear the eight corners in axis order;
/// Exact and HeldAtEdge [y]; ConservativeColumn [value at the lower column, value at the upper column].
/// </summary>
/// <param name="Symbol">The named value this look-up gives (e.g. <c>C'_{o}</c>), or empty when the look-up gives the
/// coefficient itself.</param>
/// <param name="Meaning">What <paramref name="Symbol"/> is, for the worksheet.</param>
public sealed record TableLookupStep(TableLookupKind Kind, IReadOnlyList<TableLookupAxis> Axes,
    IReadOnlyList<double> Values, double Result, string? Note = null, string Symbol = "", string Meaning = "");

/// <summary>One factor of a coefficient that is a product of factors (C = K1 × K2 × ...).</summary>
/// <param name="Symbol">Symbol in worksheet markup.</param>
/// <param name="Meaning">What the factor is and where it comes from.</param>
/// <param name="Value">The factor's value.</param>
public sealed record CoefficientFactor(string Symbol, string Meaning, double Value);

