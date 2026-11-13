namespace DuctSizer.Core;

public sealed record VavPerformancePoint(double AirflowCfm, double PressureDropInWg);

public sealed record VavPerformanceSelection(
    string Product,
    string Configuration,
    double RequestedAirflowCfm,
    double PressureDropInWg,
    IReadOnlyList<VavPerformancePoint> PublishedTable,
    VavPerformancePoint LowerPoint,
    VavPerformancePoint UpperPoint,
    string SourceReference)
{
    public bool WasInterpolated => Math.Abs(LowerPoint.AirflowCfm - UpperPoint.AirflowCfm) > 1e-9;
}

/// <summary>
/// Titus DESV manufacturer performance data transcribed from the supplied performance sheets.
/// Values between published airflow points are linearly interpolated; values outside the published
/// range are rejected rather than extrapolated.
/// </summary>
public static class VavPerformanceCatalog
{
    public const string WithReheatCode = "VAV-REHEAT";
    public const string WithoutReheatCode = "VAV-COOLING";
    public const string WithReheatReference =
        "Titus PESV, AESV, DESV Hot Water Coil Capacity, pages M20-M22; airside ΔPs data. Coil performance in accordance with AHRI 410-2001.";
    public const string WithoutReheatReference =
        "Titus PESV, AESV, DESV Discharge Sound Performance, page M18; Min ΔPs is the inlet-to-outlet static pressure drop with the primary damper full open. Tests in accordance with ASHRAE 130-2008 and AHRI 880-2011.";

    public static IReadOnlyList<string> WithReheatSizes { get; } =
        new[] { "4", "5", "6", "7", "8", "9", "10", "12", "14", "16", "24x16" };
    public static IReadOnlyList<string> WithoutReheatSizes { get; } =
        new[] { "4", "5", "6", "7", "8", "9", "10", "12", "14", "16", "40" };
    public static IReadOnlyList<int> CoilRows { get; } = new[] { 1, 2, 3, 4 };

    private static readonly IReadOnlyDictionary<(string Size, int Rows), VavPerformancePoint[]> WithReheat =
        BuildWithReheat();
    private static readonly IReadOnlyDictionary<string, VavPerformancePoint[]> WithoutReheat =
        BuildWithoutReheat();

    public static IReadOnlyList<int> AvailableRows(string size) => WithReheat.Keys
        .Where(key => string.Equals(key.Size, size, StringComparison.OrdinalIgnoreCase))
        .Select(key => key.Rows).OrderBy(value => value).ToArray();

    public static VavPerformanceSelection SelectWithReheat(double requestedCfm, string size, int rows)
    {
        if (!WithReheat.TryGetValue((size, rows), out var table))
        {
            var available = AvailableRows(size);
            var suffix = available.Count == 0 ? "No table is published for that size."
                : $"Published coil-row choices for size {size} are {string.Join(", ", available)}.";
            throw new ArgumentException($"No Titus DESV hot-water coil airside ΔPs table is published for size {size} with {rows} rows. {suffix}");
        }
        return Interpolate("Titus DESV VAV with Reheat", $"Size {size}; {rows}-row hot-water coil",
            requestedCfm, table, WithReheatReference);
    }

    public static VavPerformanceSelection SelectWithoutReheat(double requestedCfm, string size)
    {
        if (!WithoutReheat.TryGetValue(size, out var table))
            throw new ArgumentException($"No Titus DESV minimum-pressure table is published for size {size}.");
        return Interpolate("Titus DESV VAV without Reheat", $"Size {size}; primary damper full open",
            requestedCfm, table, WithoutReheatReference);
    }

    /// <summary>
    /// Input key of the VAV-with-reheat terminal pressure drop (in. w.c.) entered by the engineer: the terminal's own
    /// resistance (inlet sensor, body, damper) from the manufacturer's selection, added to the published coil drop.
    /// </summary>
    public const string TerminalDropKey = "terminalDropInWg";

    public static FittingCalculationResult CalculateFittingResult(FittingDefinition definition,
        IReadOnlyDictionary<string, double> inputs, double airflowM3PerS, double temperatureC,
        double relativeHumidity = 0.50, double atmosphericPressurePa = 101_325.0)
    {
        var requestedCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        VavPerformanceSelection selection;
        string size;
        if (string.Equals(definition.Code, WithReheatCode, StringComparison.OrdinalIgnoreCase))
        {
            var index = Convert.ToInt32(inputs.GetValueOrDefault("sizeIndex", 8));
            if (index < 0 || index >= WithReheatSizes.Count)
                throw new ArgumentOutOfRangeException(nameof(inputs), "Select a published VAV size.");
            size = WithReheatSizes[index];
            selection = SelectWithReheat(requestedCfm, size, Convert.ToInt32(inputs.GetValueOrDefault("rows", 3)));
        }
        else if (string.Equals(definition.Code, WithoutReheatCode, StringComparison.OrdinalIgnoreCase))
        {
            var index = Convert.ToInt32(inputs.GetValueOrDefault("sizeIndex", 6));
            if (index < 0 || index >= WithoutReheatSizes.Count)
                throw new ArgumentOutOfRangeException(nameof(inputs), "Select a published VAV size.");
            size = WithoutReheatSizes[index];
            selection = SelectWithoutReheat(requestedCfm, size);
        }
        else
            throw new ArgumentException($"{definition.Code} is not a Titus VAV performance-table fitting.");

        var geometry = GeometryForSize(size);
        var air = AirProperties.Calculate(temperatureC, relativeHumidity, atmosphericPressurePa);
        var velocity = airflowM3PerS / geometry.ActualAreaM2;
        var velocityPressurePa = air.DensityKgPerM3 * velocity * velocity / 2.0;
        var terminalDropInWg = string.Equals(definition.Code, WithReheatCode, StringComparison.OrdinalIgnoreCase)
            ? inputs.GetValueOrDefault(TerminalDropKey, 0.0)
            : 0.0;
        if (!double.IsFinite(terminalDropInWg) || terminalDropInWg < 0)
            throw new ArgumentOutOfRangeException(nameof(inputs), "The terminal pressure drop must be 0 or more.");
        // Whole-terminal drop = published coil airside drop + the terminal's own drop entered from the selection.
        var pressureLossPa = UnitConversions.InchesWaterToPa(selection.PressureDropInWg + terminalDropInWg);
        var coefficient = velocityPressurePa > 0 ? pressureLossPa / velocityPressurePa : 0;
        var resultInputs = new Dictionary<string, double>(inputs, StringComparer.OrdinalIgnoreCase)
        {
            ["selectedPressureDropInWg"] = selection.PressureDropInWg,
            ["lowerCfm"] = selection.LowerPoint.AirflowCfm,
            ["lowerPressureDropInWg"] = selection.LowerPoint.PressureDropInWg,
            ["upperCfm"] = selection.UpperPoint.AirflowCfm,
            ["upperPressureDropInWg"] = selection.UpperPoint.PressureDropInWg
        };
        return new FittingCalculationResult(definition, resultInputs, velocity, air, coefficient,
            velocityPressurePa, pressureLossPa, airflowM3PerS, geometry);
    }

    public static IReadOnlyList<VavPerformancePoint> WithReheatTable(string size, int rows) =>
        WithReheat.TryGetValue((size, rows), out var table) ? table : Array.Empty<VavPerformancePoint>();

    public static IReadOnlyList<VavPerformancePoint> WithoutReheatTable(string size) =>
        WithoutReheat.TryGetValue(size, out var table) ? table : Array.Empty<VavPerformancePoint>();

    private static VavPerformanceSelection Interpolate(string product, string configuration, double requestedCfm,
        IReadOnlyList<VavPerformancePoint> table, string source)
    {
        if (!double.IsFinite(requestedCfm) || requestedCfm <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedCfm), "Airflow must be greater than zero.");
        if (requestedCfm < table[0].AirflowCfm - 1e-9 || requestedCfm > table[^1].AirflowCfm + 1e-9)
            throw new ArgumentOutOfRangeException(nameof(requestedCfm),
                $"Airflow for {configuration} must be within the published range of {table[0].AirflowCfm:0} to {table[^1].AirflowCfm:0} cfm.");

        var upperIndex = 0;
        while (upperIndex < table.Count && table[upperIndex].AirflowCfm < requestedCfm - 1e-9)
            upperIndex++;
        if (upperIndex >= table.Count) upperIndex = table.Count - 1;
        var upper = table[upperIndex];
        if (Math.Abs(upper.AirflowCfm - requestedCfm) <= 1e-9 || upperIndex == 0)
            return new VavPerformanceSelection(product, configuration, requestedCfm, upper.PressureDropInWg,
                table, upper, upper, source);
        var lower = table[upperIndex - 1];
        var fraction = (requestedCfm - lower.AirflowCfm) / (upper.AirflowCfm - lower.AirflowCfm);
        var pressureDrop = lower.PressureDropInWg
                           + fraction * (upper.PressureDropInWg - lower.PressureDropInWg);
        return new VavPerformanceSelection(product, configuration, requestedCfm, pressureDrop, table, lower, upper, source);
    }

    private static DuctGeometry GeometryForSize(string size)
    {
        if (string.Equals(size, "24x16", StringComparison.OrdinalIgnoreCase)
            || string.Equals(size, "40", StringComparison.OrdinalIgnoreCase))
            return GeometryCalculator.Rectangular(UnitConversions.InchesToM(24), UnitConversions.InchesToM(16));
        return GeometryCalculator.Round(UnitConversions.InchesToM(double.Parse(size,
            System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyDictionary<(string Size, int Rows), VavPerformancePoint[]> BuildWithReheat()
    {
        var result = new Dictionary<(string, int), VavPerformancePoint[]>();
        void Add(string[] sizes, int rows, string airflows, string pressureDrops)
        {
            var table = Points(airflows, pressureDrops);
            foreach (var size in sizes) result[(size, rows)] = table;
        }

        Add(new[] { "14" }, 1, "400,700,1000,1300,1600,1900,2200,2500,2800", ".01,.03,.06,.09,.13,.17,.22,.27,.33");
        Add(new[] { "16" }, 1, "600,1000,1400,1800,2200,2600,3000,3400,3800", ".02,.04,.07,.10,.14,.19,.24,.30,.36");
        Add(new[] { "24x16" }, 1, "600,1200,1800,2400,3000,3600,4200,4800,5400", ".01,.02,.05,.08,.11,.15,.20,.25,.30");
        Add(new[] { "14" }, 2, "400,700,1000,1300,1600,1900,2200,2500,2800", ".03,.07,.12,.19,.27,.36,.46,.57,.69");
        Add(new[] { "16" }, 2, "600,1000,1400,1800,2200,2600,3000,3400,3800", ".04,.08,.14,.22,.30,.40,.51,.63,.76");
        Add(new[] { "24x16" }, 2, "600,1200,1800,2400,3000,3600,4200,4800,5400", ".02,.05,.10,.16,.24,.32,.42,.52,.63");

        Add(new[] { "4", "5", "6" }, 3, "50,100,150,200,250,300,350,400,450", ".01,.04,.08,.12,.18,.24,.31,.39,.47");
        Add(new[] { "7", "8" }, 3, "100,200,300,400,500,600,700,800,900", ".02,.08,.15,.24,.35,.47,.61,.77,.93");
        Add(new[] { "9", "10" }, 3, "200,300,400,500,600,700,800,900,1000", ".04,.08,.13,.19,.25,.33,.41,.50,.59");
        Add(new[] { "12" }, 3, "300,500,700,900,1100,1300,1500,1700,1900", ".05,.11,.19,.29,.41,.54,.69,.85,1.02");
        Add(new[] { "14" }, 3, "400,700,1000,1300,1600,1900,2200,2500,2800", ".04,.10,.19,.29,.41,.54,.69,.86,1.04");
        Add(new[] { "16" }, 3, "600,1000,1400,1800,2200,2600,3000,3400,3800", ".06,.14,.24,.37,.51,.68,.86,1.06,1.28");
        Add(new[] { "24x16" }, 3, "600,1200,1800,2400,3000,3600,4200,4800,5400", ".03,.09,.17,.27,.40,.54,.70,.88,1.07");

        Add(new[] { "4", "5", "6" }, 4, "50,100,150,200,250,300,350,400,450", ".02,.05,.10,.16,.24,.32,.41,.52,.63");
        Add(new[] { "7", "8" }, 4, "100,200,300,400,500,600,700,800,900", ".03,.10,.20,.32,.47,.63,.82,1.02,1.25");
        Add(new[] { "9", "10" }, 4, "200,300,400,500,600,700,800,900,1000", ".05,.11,.17,.25,.34,.43,.54,.66,.79");
        Add(new[] { "12" }, 4, "300,500,700,900,1100,1300,1500,1700,1900", ".06,.15,.26,.39,.55,.72,.92,1.13,1.36");
        Add(new[] { "14" }, 4, "400,700,1000,1300,1600,1900,2200,2500,2800", ".05,.14,.25,.38,.54,.72,.93,1.15,1.39");
        Add(new[] { "16" }, 4, "600,1000,1400,1800,2200,2600,3000,3400,3800", ".08,.18,.32,.49,.68,.90,1.15,1.42,1.71");
        Add(new[] { "24x16" }, 4, "600,1200,1800,2400,3000,3600,4200,4800,5400", ".04,.12,.23,.37,.53,.72,.93,1.17,1.42");
        return result;
    }

    private static IReadOnlyDictionary<string, VavPerformancePoint[]> BuildWithoutReheat() =>
        new Dictionary<string, VavPerformancePoint[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["4"] = Points("100,125,150,175,200", ".02,.03,.04,.06,.08"),
            ["5"] = Points("150,200,250,300,350", ".01,.02,.03,.04,.06"),
            ["6"] = Points("300,350,400,450,500", ".07,.10,.13,.16,.20"),
            ["7"] = Points("450,500,550,600,650", ".07,.09,.10,.12,.15"),
            ["8"] = Points("600,650,700,750,800", ".02,.02,.02,.02,.03"),
            ["9"] = Points("800,850,900,950,1000", ".04,.04,.05,.06,.06"),
            ["10"] = Points("900,1000,1100,1200,1300", ".01,.01,.01,.01,.01"),
            ["12"] = Points("1200,1400,1600,1800,2000", ".01,.01,.01,.01,.01"),
            ["14"] = Points("1500,1800,2100,2400,2700", ".02,.03,.04,.05,.06"),
            ["16"] = Points("2000,2400,2800,3200,3600", ".02,.02,.03,.04,.05"),
            ["40"] = Points("3900,4600,5300,6000,6700", ".03,.04,.06,.07,.09")
        };

    private static VavPerformancePoint[] Points(string airflows, string pressureDrops)
    {
        var q = airflows.Split(',').Select(value => double.Parse(value,
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var dp = pressureDrops.Split(',').Select(value => double.Parse(value,
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (q.Length != dp.Length) throw new InvalidDataException("VAV performance table columns do not align.");
        return q.Select((value, index) => new VavPerformancePoint(value, dp[index])).ToArray();
    }
}
