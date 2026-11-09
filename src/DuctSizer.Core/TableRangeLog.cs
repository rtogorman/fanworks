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
