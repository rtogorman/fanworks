namespace DuctSizer.Core;

public sealed record FittingInputSpec(
    string Key,
    string Label,
    string Unit,
    double DefaultValue,
    double Minimum,
    double Maximum,
    string Help = "",
    IReadOnlyList<FittingInputChoice>? Choices = null);

public sealed record FittingInputChoice(string Label, double Value);

public enum JunctionPath
{
    Branch,
    Straight
}

public sealed class FittingDefinition
{
    private readonly Func<IReadOnlyDictionary<string, double>, double> _coefficientCalculator;

    internal FittingDefinition(
        string code,
        string category,
        string description,
        string referenceVelocity,
        string sourceNote,
        bool automated,
        IReadOnlyList<FittingInputSpec> inputs,
        Func<IReadOnlyDictionary<string, double>, double> coefficientCalculator)
    {
        Code = code;
        Category = category;
        Description = description;
        ReferenceVelocity = referenceVelocity;
        SourceNote = sourceNote;
        IsAutomated = automated;
        Inputs = inputs;
        _coefficientCalculator = coefficientCalculator;
    }

    public string Code { get; }
    public string Category { get; }
    public string Description { get; }
    public string ReferenceVelocity { get; }
    public string SourceNote { get; }
    public bool IsAutomated { get; }
    public bool IsJunction => string.Equals(Category, "Junctions", StringComparison.OrdinalIgnoreCase);
    public IReadOnlyList<FittingInputSpec> Inputs { get; }
    public string DisplayName => $"{Code} — {Description}";
    public DuctShape? ReferenceDuctShape
    {
        get
        {
            if (Description.Contains("rectangular main", StringComparison.OrdinalIgnoreCase))
                return DuctShape.Rectangular;
            if (Description.Contains("round main", StringComparison.OrdinalIgnoreCase))
                return DuctShape.Round;

            var mentionsRound = Description.Contains("round", StringComparison.OrdinalIgnoreCase);
            var mentionsRectangular = Description.Contains("rectangular", StringComparison.OrdinalIgnoreCase)
                                      || Description.Contains("square", StringComparison.OrdinalIgnoreCase);
            if (mentionsRound == mentionsRectangular)
                return null;
            return mentionsRectangular ? DuctShape.Rectangular : DuctShape.Round;
        }
    }

    public double CalculateCoefficient(IReadOnlyDictionary<string, double> values)
    {
        foreach (var input in Inputs)
        {
            if (!values.TryGetValue(input.Key, out var value) || !double.IsFinite(value))
                throw new ArgumentException($"{input.Label} is required.");
            if (value < input.Minimum || value > input.Maximum)
                throw new ArgumentOutOfRangeException(input.Key, $"{input.Label} must be between {input.Minimum:g} and {input.Maximum:g}.");
        }

        var coefficient = _coefficientCalculator(values);
        if (!double.IsFinite(coefficient))
            throw new InvalidOperationException("The fitting coefficient could not be calculated for these inputs.");
        return coefficient;
    }

    public override string ToString() => DisplayName;
}

public sealed record FittingCalculationResult(
    FittingDefinition Definition,
    IReadOnlyDictionary<string, double> Inputs,
    double ReferenceVelocityMPerS,
    AirState Air,
    double Coefficient,
    double VelocityPressurePa,
    double PressureLossPa,
    double? AirflowM3PerS = null,
    DuctGeometry? ReferenceGeometry = null,
    JunctionFlowData? Junction = null,
    IReadOnlyList<string>? RangeWarnings = null,
    IReadOnlyList<TableLookupStep>? Lookups = null,
    IReadOnlyList<CoefficientFactor>? Factors = null)
{
    /// <summary>
    /// When the coefficient is a product of named factors (for example C = Kθ × C'o for a smooth-radius elbow), the
    /// factors in calculation order; their product is <see cref="Coefficient"/>. Empty otherwise.
    /// </summary>
    public IReadOnlyList<CoefficientFactor> CoefficientFactors => Factors ?? Array.Empty<CoefficientFactor>();

    /// <summary>
    /// The table look-ups behind the coefficient, in calculation order: each says whether the value was a published
    /// point, an interpolation (with the bracketing points), held at a table edge, or the conservative choice of two
    /// published columns. Empty for fixed coefficients and equation-only fittings.
    /// </summary>
    public IReadOnlyList<TableLookupStep> TableLookups => Lookups ?? Array.Empty<TableLookupStep>();

    /// <summary>
    /// Inputs that fell outside a published ASHRAE table and were held at the table edge. Empty when every lookup
    /// stayed inside the published range. A non-empty list means the coefficient is an edge value, not an
    /// interpolated table value, and must be reviewed.
    /// </summary>
    public IReadOnlyList<string> TableRangeWarnings => RangeWarnings ?? Array.Empty<string>();
}

public sealed record JunctionFlowData(
    JunctionPath Path,
    double CommonAirflowM3PerS,
    DuctGeometry CommonGeometry,
    double StraightAirflowM3PerS,
    DuctGeometry StraightGeometry,
    double BranchAirflowM3PerS,
    DuctGeometry BranchGeometry)
{
    public double CommonVelocityMPerS => CommonAirflowM3PerS / CommonGeometry.ActualAreaM2;
    public double StraightVelocityMPerS => StraightAirflowM3PerS / StraightGeometry.ActualAreaM2;
    public double BranchVelocityMPerS => BranchAirflowM3PerS / BranchGeometry.ActualAreaM2;

    public Dictionary<string, double> DerivedInputs() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = Path == JunctionPath.Branch ? 0 : 1,
        ["qbqc"] = BranchAirflowM3PerS / CommonAirflowM3PerS,
        ["qsqc"] = StraightAirflowM3PerS / CommonAirflowM3PerS,
        ["qbqs"] = BranchAirflowM3PerS / StraightAirflowM3PerS,
        ["abac"] = BranchGeometry.ActualAreaM2 / CommonGeometry.ActualAreaM2,
        ["asac"] = StraightGeometry.ActualAreaM2 / CommonGeometry.ActualAreaM2,
        ["abas"] = BranchGeometry.ActualAreaM2 / StraightGeometry.ActualAreaM2,
        ["vbvc"] = BranchVelocityMPerS / CommonVelocityMPerS,
        ["vsvc"] = StraightVelocityMPerS / CommonVelocityMPerS,
        ["vbvs"] = BranchVelocityMPerS / StraightVelocityMPerS,
        ["vcfpm"] = UnitConversions.MpsToFpm(CommonVelocityMPerS)
    };
}

public static class FittingCalculator
{
    public static FittingCalculationResult Calculate(
        FittingDefinition definition,
        IReadOnlyDictionary<string, double> inputs,
        JunctionFlowData junction,
        double temperatureC,
        double relativeHumidity = 0.50,
        double atmosphericPressurePa = 101_325.0)
    {
        ArgumentNullException.ThrowIfNull(junction);
        if (!definition.IsJunction)
            throw new ArgumentException("A junction flow model can only be used with an ASHRAE junction fitting.", nameof(definition));
        if (junction.CommonAirflowM3PerS <= 0 || junction.StraightAirflowM3PerS <= 0 || junction.BranchAirflowM3PerS <= 0)
            throw new ArgumentOutOfRangeException(nameof(junction), "Common, straight, and branch airflows must be positive.");

        var combined = new Dictionary<string, double>(inputs, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in junction.DerivedInputs())
            combined[pair.Key] = pair.Value;
        var result = Calculate(definition, combined, junction.CommonVelocityMPerS, temperatureC,
            relativeHumidity, atmosphericPressurePa);
        return result with
        {
            AirflowM3PerS = junction.CommonAirflowM3PerS,
            ReferenceGeometry = junction.CommonGeometry,
            Junction = junction
        };
    }

    public static FittingCalculationResult Calculate(
        FittingDefinition definition,
        IReadOnlyDictionary<string, double> inputs,
        double airflowM3PerS,
        DuctGeometry referenceGeometry,
        double temperatureC,
        double relativeHumidity = 0.50,
        double atmosphericPressurePa = 101_325.0)
    {
        ArgumentNullException.ThrowIfNull(referenceGeometry);
        if (!double.IsFinite(airflowM3PerS) || airflowM3PerS <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(airflowM3PerS), "Airflow must be positive.");
        if (!double.IsFinite(referenceGeometry.ActualAreaM2) || referenceGeometry.ActualAreaM2 <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(referenceGeometry), "Reference duct area must be positive.");
        var result = Calculate(definition, inputs, airflowM3PerS / referenceGeometry.ActualAreaM2,
            temperatureC, relativeHumidity, atmosphericPressurePa);
        return result with { AirflowM3PerS = airflowM3PerS, ReferenceGeometry = referenceGeometry };
    }

    public static FittingCalculationResult Calculate(
        FittingDefinition definition,
        IReadOnlyDictionary<string, double> inputs,
        double referenceVelocityMPerS,
        double temperatureC,
        double relativeHumidity = 0.50,
        double atmosphericPressurePa = 101_325.0)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(inputs);
        if (!double.IsFinite(referenceVelocityMPerS) || referenceVelocityMPerS < 0.0)
            throw new ArgumentOutOfRangeException(nameof(referenceVelocityMPerS), "Reference velocity cannot be negative.");

        var air = AirProperties.Calculate(temperatureC, relativeHumidity, atmosphericPressurePa);
        double coefficient;
        IReadOnlyList<string> rangeWarnings;
        IReadOnlyList<TableLookupStep> lookups;
        IReadOnlyList<CoefficientFactor> factors;
        using (var rangeLog = TableRangeLog.Begin())
        {
            coefficient = definition.CalculateCoefficient(inputs);
            rangeWarnings = rangeLog.Describe(definition, inputs);
            lookups = rangeLog.Lookups(definition, inputs);
            factors = rangeLog.Factors;
        }
        // The factors are shown as C = K1 × K2 × ...; only when their product is the coefficient (a fitting that
        // combines its factors differently shows its look-ups without a combining equation).
        if (factors.Count < 2
            || Math.Abs(factors.Aggregate(1.0, (product, factor) => product * factor.Value) - coefficient)
               > 1e-9 * Math.Max(1.0, Math.Abs(coefficient)))
            factors = Array.Empty<CoefficientFactor>();
        var velocityPressure = air.DensityKgPerM3 * referenceVelocityMPerS * referenceVelocityMPerS / 2.0;
        return new FittingCalculationResult(
            definition,
            new Dictionary<string, double>(inputs),
            referenceVelocityMPerS,
            air,
            coefficient,
            velocityPressure,
            coefficient * velocityPressure,
            RangeWarnings: rangeWarnings,
            Lookups: lookups,
            Factors: factors);
    }
}

public static class FittingCatalog
{
    public const string TerminalSelectionGroup = "Terminals & Takeoffs";
    public const string VavSelectionGroup = "VAVs";
    public const string FanExitsAndPlenumsSelectionGroup = "Fan Exits and Plenums";

    public static IReadOnlyList<string> SelectionGroups { get; } = new[]
    {
        "Elbows", "Transitions", "Diverting Fittings", "Converging Fittings", "Dampers",
        TerminalSelectionGroup, VavSelectionGroup, "GRDs", FanExitsAndPlenumsSelectionGroup
    };

    public static bool IsTerminalSelectionGroup(string? group) =>
        string.Equals(group, TerminalSelectionGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(group, "Terminals", StringComparison.OrdinalIgnoreCase);

    public static bool IsFanExitsAndPlenumsSelectionGroup(string? group) =>
        string.Equals(group, FanExitsAndPlenumsSelectionGroup, StringComparison.OrdinalIgnoreCase)
        || string.Equals(group, "Others", StringComparison.OrdinalIgnoreCase);

    private static readonly FittingInputSpec ManualCoefficient =
        new("c", "ASHRAE coefficient C", "", 0.50, 0.0, 10_000.0,
            "Enter C from the indicated ASHRAE table. This entry is not yet safely digitized.");
    private static readonly FittingInputSpec SignedManualCoefficient =
        new("c", "ASHRAE coefficient C", "", 0.50, -10_000.0, 10_000.0,
            "Enter the signed branch or straight coefficient from the indicated ASHRAE junction table.");

    /// <summary>
    /// Every fitting, terminal and VAV definition, in display order.
    /// </summary>
    /// <remarks>
    /// Each definition's <see cref="FittingDefinition.Code"/> is the ASHRAE fitting number (ASHRAE Duct Fitting
    /// Database / Handbook - Fundamentals, Duct Design chapter) or a manufacturer catalog code, and its
    /// <see cref="FittingDefinition.SourceNote"/> names the source. Digitized tables are interpolated, never
    /// extrapolated: an input outside a table uses the edge value and the result carries a
    /// <see cref="FittingCalculationResult.TableRangeWarnings"/> entry. The transcriptions have not yet had an
    /// independent second check; docs/TABLE_VERIFICATION.md is the register for that check, with a row per entry.
    /// </remarks>
    public static IReadOnlyList<FittingDefinition> All { get; } = Build();

    private static readonly IReadOnlyDictionary<string, string> LegacyCodeMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A12-A"] = "1-1", ["A12-B"] = "1-2", ["A12-C"] = "1-3", ["A12-D"] = "1-4",
            ["A12-E"] = "1-5", ["A12-F"] = "1-6", ["A12-G"] = "1-7", ["A13-J"] = "2-1",
            ["A7-A"] = "3-1", ["A7-B"] = "3-2", ["A7-C"] = "3-3", ["A7-K"] = "3-4",
            ["A7-F"] = "3-5", ["A7-D"] = "3-6", ["A7-E"] = "3-10", ["A7-J"] = "3-12",
            ["A15-C"] = "6-3", ["A15-D"] = "6-4", ["A15-E"] = "6-5", ["A15-F"] = "6-6",
            ["A14-A"] = "6-7", ["A14-B"] = "6-8", ["A15-I"] = "6-10", ["A15-J"] = "6-11", ["A15-K"] = "3-15",
            ["A13-G"] = "7-9", ["A13-H"] = "7-10"
        };

    public static FittingDefinition Find(string code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Unknown ASHRAE fitting code {code}.");

    public static bool TryMapLegacyCode(string legacyCode, out string ashraeCode) =>
        LegacyCodeMap.TryGetValue(legacyCode, out ashraeCode!);

    public static string SelectionGroup(FittingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Code is "6-7" or "6-8" or "6-9")
            return "Dampers";
        if (definition.Code is "6-10" or "6-11")
            return "Transitions";
        if (definition.IsJunction)
            return definition.Description.Contains("converging", StringComparison.OrdinalIgnoreCase)
                ? "Converging Fittings"
                : "Diverting Fittings";
        if (string.Equals(definition.Category, "Elbows", StringComparison.OrdinalIgnoreCase))
            return "Elbows";
        if (string.Equals(definition.Category, "Transitions", StringComparison.OrdinalIgnoreCase))
            return "Transitions";
        if (definition.Description.Contains("damper", StringComparison.OrdinalIgnoreCase))
            return "Dampers";
        if (definition.Category is "Entries" or "Exits")
            return TerminalSelectionGroup;
        if (string.Equals(definition.Category, "GRDs", StringComparison.OrdinalIgnoreCase))
            return "GRDs";
        if (string.Equals(definition.Category, "VAVs", StringComparison.OrdinalIgnoreCase))
            return VavSelectionGroup;
        return FanExitsAndPlenumsSelectionGroup;
    }

    public static int InletPortCount(string? selectionGroup) =>
        string.Equals(selectionGroup, "Converging Fittings", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    public static int OutletPortCount(string? selectionGroup) =>
        string.Equals(selectionGroup, "Diverting Fittings", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static IReadOnlyList<FittingDefinition> Build()
    {
        var legacy = BuildLegacy().ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var items = new List<FittingDefinition>();

        AddNumberedManualGroup(items, 1, "Entries", "downstream/reference duct", new[]
        {
            "Duct mounted in wall (hood, nonenclosing, flanged and unflanged)",
            "Smooth converging bellmouth without end wall", "Smooth converging bellmouth with end wall",
            "Conical converging bellmouth without end wall, round and rectangular",
            "Conical converging bellmouth with end wall, round and rectangular", "Intake hood",
            "Hood, tapered, flanged or unflanged", "Orifice, sharp-edged, inlet duct"
        });
        AddNumberedManualGroup(items, 2, "Exits", "upstream/reference duct", new[]
        {
            "Exit, abrupt, round and rectangular", "Exit, abrupt, round and rectangular, with end",
            "Exit, duct flush with wall, flow along wall", "Exit, round, diverging",
            "Exit, round, with end wall transition", "Exit, rectangular, two sides parallel, diverging, symmetrical",
            "Exit, rectangular, with wall, two sides parallel, symmetrical, diverging",
            "Exit, rectangular, pyramidal, diverging", "Exit, rectangular, with wall, pyramidal, diverging",
            "Exit, discharge to atmosphere from a 90° elbow, rectangular and round", "Exhaust hood", "Stackhead"
        });
        AddNumberedManualGroup(items, 3, "Elbows", "upstream/reference duct", new[]
        {
            "Elbow, smooth radius (die stamped), round", "Elbows, 5-, 4-, and 3-piece, round",
            "Elbow, mitered, round", "Elbows, 30° Z-shaped, round", "Elbow, without vanes, rectangular",
            "Elbow, mitered, rectangular", "Elbow, smooth radius with splitter vanes, rectangular",
            "Elbow, mitered, with single-thickness vanes, rectangular",
            "Elbow, mitered, with double-thickness vanes, rectangular",
            "Elbow, variable inlet/outlet areas, rectangular", "Elbows, 90° Z-shaped, rectangular",
            "Combined 90° elbows lying in different planes, rectangular",
            "Offset, S-shaped (gooseneck), rectangular and round",
            "Offset, S-shaped in two planes 90° apart, rectangular and round",
            "Four 45° smooth-radius rectangular elbows arranged around an obstruction"
        });
        AddNumberedManualGroup(items, 4, "Transitions", "section o/reference duct", new[]
        {
            "Transition, round", "Transition, rectangular, two sides parallel, symmetrical",
            "Transition, rectangular, three sides straight", "Transition, rectangular, pyramidal",
            "Transition, round/rectangular", "Transition, rectangular to round",
            "Transition, rectangular to round, stepped, conical"
        });
        AddNumberedManualGroup(items, 5, "Junctions", "common/reference duct", new[]
        {
            "Wye, 30°, converging", "Wye, 45°, converging, round", "Tee, converging, round",
            "Wye, 30°, converging, round, conical main", "Wye, 45°, converging, round, conical main",
            "Tee, converging, rectangular", "Tee, converging, round tap to rectangular main",
            "Tee, converging, rectangular main and tap", "Converging rectangular main and tap, 45° entry",
            "Tee, diverging, round, conical branch", "Wye, 45°, diverging, round, conical branch",
            "Tee, diverging, round, with 90° elbow, branch 90° to main",
            "Tee, diverging, round, with 45° elbow, branch 90° to main",
            "Tee, diverging, round, conical branch with 45° elbow, branch 90° to main",
            "Wye, 45°, round, with 60° elbow, branch 90° to main",
            "Wye, 45°, diverging, round conical branch with 60° elbow, branch 90° to main",
            "Wye, 48°, diverging, conical main and branch with 45° elbow, branch 90° to main",
            "Tee, diverging, round, with 60° elbow, branch 45° to main",
            "Tee, diverging, round conical branch with 60° elbow, branch 45° to main",
            "Wye, 45°, diverging, round, with 30° elbow, branch 45° to main",
            "Wye, 45°, diverging, round conical branch with 30° elbow, branch 45° to main",
            "Tee, diverging, rectangular", "Wye, diverging, rectangular and round",
            "Diverging wye, rectangular", "Tee, diverging, rectangular main to round tap",
            "Tee, diverging, rectangular main to round tap, conical",
            "Tee, diverging, rectangular main and tap, 45° entry",
            "Tee, diverging, rectangular main and tap", "Tee, diverging, rectangular main and 45° tap, with damper",
            "Tee, diverging, rectangular main and tap, with damper",
            "Tee, diverging, rectangular, with extractor", "Symmetrical wye, dovetail, rectangular",
            "Wye, rectangular and round", "Double 45° wye, rectangular and round", "Cross, 90°, rectangular and round"
        });
        AddNumberedManualGroup(items, 6, "Obstructions", "upstream/reference duct", new[]
        {
            "Damper, butterfly, round", "Damper, butterfly, rectangular", "Damper, gate, round",
            "Damper, gate, rectangular", "Damper, rectangular, parallel blades",
            "Damper, rectangular, opposed blades", "Obstruction, screen, round and rectangular",
            "Obstruction, perforated plate, thick, round and rectangular",
            "Obstruction, smooth cylinder in round and rectangular ducts",
            "Round duct, depressed to avoid an obstruction", "Rectangular duct, depressed to avoid an obstruction"
        });
        AddNumberedManualGroup(items, 7, "Fan-system connections", "fan outlet/reference duct", new[]
        {
            "Fans discharging into a plenum", "Single-width single-inlet fan with an outlet duct elbow",
            "Double-width double-inlet fan with an outlet duct elbow",
            "Nonuniform elbow into a fan inlet induced by a 90° round smooth-radius elbow without vanes",
            "Nonuniform elbow into a fan inlet induced by 90° mitered and multipiece elbows without vanes",
            "Nonuniform elbow into a fan inlet induced by a 90° square smooth-radius elbow",
            "Fans located in plenums and cabinet enclosures", "Fan without an outlet diffuser",
            "Plane asymmetric diffuser at fan outlet without ductwork", "Pyramidal diffuser at fan outlet without ductwork",
            "Plane symmetric diffuser at fan outlet with ductwork",
            "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 1",
            "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 2",
            "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 3",
            "Pyramidal diffuser at fan outlet with ductwork"
        });

        // Calculators below are direct matches to tables that are clear in the supplied ASHRAE supplement.
        Map(items, legacy, "A12-A", "1-1");
        Map(items, legacy, "A12-B", "1-2");
        Map(items, legacy, "A12-C", "1-3");
        Map(items, legacy, "A12-D", "1-4");
        Map(items, legacy, "A12-E", "1-5");
        Map(items, legacy, "A12-F", "1-6");
        Map(items, legacy, "A12-G", "1-7");
        Map(items, legacy, "A13-J", "2-1");
        Map(items, legacy, "A7-A", "3-1");
        Map(items, legacy, "A7-B", "3-2");
        Map(items, legacy, "A7-C", "3-3");
        Map(items, legacy, "A7-K", "3-4");
        Map(items, legacy, "A7-F", "3-5");
        Map(items, legacy, "A7-D", "3-6");
        Map(items, legacy, "A7-J", "3-12");
        Map(items, legacy, "A15-C", "6-3");
        Map(items, legacy, "A15-D", "6-4");
        Map(items, legacy, "A15-E", "6-5");
        Map(items, legacy, "A15-F", "6-6");
        Map(items, legacy, "A14-B", "6-8");
        Map(items, legacy, "A15-I", "6-10");
        Map(items, legacy, "A15-J", "6-11");
        Map(items, legacy, "A13-G", "7-9");
        Map(items, legacy, "A13-H", "7-10");

        Replace(items, Auto("2-11", "Exits", "Exhaust hood", "upstream/reference duct", "ASHRAE supplemental fitting 2-11.",
            new[] { Axis("ld", "Length ratio L/D", "ratio", .3, .1, .5) },
            v => I1(new[] { .1,.2,.25,.3,.35,.4,.5 }, new[] { 2.6,1.2,1,.8,.7,.65,.6 }, v["ld"])));
        Replace(items, Auto("1-8", "Entries", "Orifice, sharp-edged, inlet duct", "downstream/reference duct", "ASHRAE supplemental fitting 1-8; bilinear interpolation by Reynolds number and orifice/duct area ratio.",
            new[] { Axis("re", "Reynolds number", "", 20_000, 4_000, 100_000), Axis("area", "Orifice/duct area ratio Aor/Ao", "ratio", .5, .2, .9) },
            v => I2(A1_8_Re, A1_8_Area, A1_8, v["re"], v["area"])));
        Replace(items, Auto("2-2", "Exits", "Exit, abrupt, round and rectangular, with end", "upstream/reference duct", "ASHRAE supplemental fitting 2-2; shape and published rectangular aspect-ratio band are discrete selections.",
            new[] { Axis("shape", "Shape (0 round, 1 rectangular)", "", 0, 0, 1), Axis("aspectBand", "Rectangular aspect band (0 <=0.2, 1 = 0.5-2, 2 >=5)", "", 1, 0, 2), Axis("angle", "End angle", "degrees", 90, 45, 150), Axis("velocityRatio", "External/duct velocity ratio V/Vo", "ratio", 1, 0, 2) },
            ExitWithEnd));
        Replace(items, Auto("2-3", "Exits", "Exit, duct flush with wall, flow along wall", "upstream/reference duct", "ASHRAE supplemental fitting 2-3.",
            Array.Empty<FittingInputSpec>(), _ => .88));
        Replace(items, Auto("2-4", "Exits", "Exit, round, diverging", "upstream/reference duct", "ASHRAE supplemental fitting 2-4.",
            new[] { Axis("area", "Area ratio A1/Ao", "ratio", 4, 2, 16), Axis("angle", "Included angle", "degrees", 20, 8, 60) },
            v => I2(A2_4_Area, A2_4_Angle, A2_4, v["area"], v["angle"])));
        Replace(items, Auto("2-5", "Exits", "Exit, round, with end wall transition", "upstream/reference duct", "ASHRAE supplemental fitting 2-5 at the published optimum angle.",
            new[] { Axis("ld", "Length ratio L/D", "ratio", 3, .5, 14) },
            v => I1(A2_5_Ld, A2_5, v["ld"])));
        Replace(items, Auto("2-6", "Exits", "Exit, rectangular, two sides parallel, diverging, symmetrical", "upstream/reference duct", "ASHRAE supplemental fitting 2-6; valid for 0.5 <= H/W <= 2.0.",
            new[] { Axis("area", "Area ratio A1/Ao", "ratio", 4, 2, 6), Axis("angle", "Included angle", "degrees", 20, 8, 60) },
            v => I2(A2_6_Area, A2_6_Angle, A2_6, v["area"], v["angle"])));
        Replace(items, Auto("2-7", "Exits", "Exit, rectangular, with wall, two sides parallel, symmetrical, diverging", "upstream/reference duct", "ASHRAE supplemental fitting 2-7 at the published optimum angle.",
            new[] { Axis("lh", "Length ratio L/H", "ratio", 3, .5, 14) },
            v => I1(A2_7_Lh, A2_7, v["lh"])));
        Replace(items, Auto("2-8", "Exits", "Exit, rectangular, pyramidal, diverging", "upstream/reference duct", "ASHRAE supplemental fitting 2-8.",
            new[] { Axis("area", "Area ratio A1/Ao", "ratio", 4, 2, 10), Axis("angle", "Larger included angle", "degrees", 20, 8, 60) },
            v => I2(A2_8_Area, A2_8_Angle, A2_8, v["area"], v["angle"])));
        Replace(items, Auto("2-9", "Exits", "Exit, rectangular, with wall, pyramidal, diverging", "upstream/reference duct", "ASHRAE supplemental fitting 2-9 at the published optimum angle.",
            new[] { Axis("ld", "Length ratio L/D", "ratio", 3, .5, 14) },
            v => I1(A2_9_Ld, A2_9, v["ld"])));
        Replace(items, Auto("2-10", "Exits", "Exit, discharge to atmosphere from a 90° elbow, rectangular and round", "upstream/reference duct", "ASHRAE supplemental fitting 2-10; includes elbow loss.",
            new[] { Axis("shape", "Shape (0 round, 1 rectangular)", "", 0, 0, 1), Axis("rw", "Rectangular radius ratio r/W", "ratio", 1, 0, 2.5), Axis("lw", "Rectangular length ratio L/W", "ratio", 2, 0, 12), Axis("ld", "Round length ratio L/D", "ratio", 1, .9, 1.3) },
            ExitFromElbow));
        Replace(items, Auto("2-12", "Exits", "Stackhead", "upstream/reference duct", "ASHRAE supplemental fitting 2-12.",
            new[] { Axis("dd", "Inner/outer diameter ratio d/Do", "ratio", .7, .3, 1) },
            v => I1(A2_12_Dd, A2_12, v["dd"])));
        Replace(items, Auto("3-5", "Elbows", "Elbow, without vanes, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-5; Co = K-angle × K-Re × C'o.",
            new[] { Axis("rw", "Centerline radius / width (r/W)", "ratio", 1, .5, 2), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8), Axis("angle", "Elbow angle", "degrees", 90, 0, 180), Axis("re", "Reynolds number", "", 200_000, 10_000, 2_000_000) },
            v => AngleFactorK(v["angle"])
                 * Named(ReynoldsFactor, "Reynolds-number correction factor, from the ASHRAE table by r/W and Re.", () => RectSmoothElbowRe(v["rw"], v["re"]))
                 * Named(BaseCoefficient, "Base loss coefficient of the 90° elbow, from the ASHRAE table by r/W and H/W.",
                     () => I2(new[] { .5,.75,1d,1.5,2 }, new[] { .25,.5,.75,1d,1.5,2,3,4,5,6,8 }, A7F, v["rw"], v["hw"]))));
        Replace(items, Auto("3-6", "Elbows", "Elbow, mitered, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-6; Co = K-Re × C'o.",
            new[] { Axis("angle", "Elbow angle", "degrees", 90, 20, 90), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8), Axis("re", "Reynolds number", "", 140_000, 10_000, 2_000_000) },
            v => Named(ReynoldsFactor, "Reynolds-number correction factor, from the ASHRAE table by Re.", () => CommonReFactor(v["re"]))
                 * Named(BaseCoefficient, "Base loss coefficient of the mitered elbow, from the ASHRAE table by elbow angle θ and H/W.",
                     () => I2(A7D_Angles, A7D_Hw, A7D, v["angle"], v["hw"]))));
        Replace(items, Auto("3-10", "Elbows", "Elbow, variable inlet/outlet areas, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-10; Co = K-Re × C'o.",
            new[] { Axis("hw", "Inlet aspect ratio Ho/Wo", "ratio", 1, .25, 100), Axis("w1w", "Width ratio W1/Wo", "ratio", 1.2, .6, 2), Axis("re", "Reynolds number", "", 140_000, 10_000, 2_000_000) },
            v => I2(new[] { .25,1d,4,100 }, new[] { .6,.8,1.2,1.4,1.6,2d }, A7E, v["hw"], v["w1w"]) * CommonReFactor(v["re"])));
        Replace(items, Auto("3-7", "Elbows", "Elbow, smooth radius with splitter vanes, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-7; discrete vane count with table interpolation and angle correction.",
            new[] { Axis("vanes", "Number of splitter vanes (1, 2, or 3)", "", 1, 1, 3), Axis("rw", "Throat radius ratio R/W", "ratio", .1, .05, .5), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8), Axis("angle", "Elbow angle", "degrees", 90, 0, 180) },
            SplitterElbow));
        Replace(items, Auto("3-8", "Elbows", "Elbow, mitered, with single-thickness vanes, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-8; design number is a discrete selection.",
            new[] { Axis("design", "Vane design number (1, 2, or 3)", "", 1, 1, 3) },
            v => new[] { .12,.15,.18 }[(int)Math.Round(v["design"]) - 1]));
        Replace(items, Auto("3-9", "Elbows", "Elbow, mitered, with double-thickness vanes, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-9; design number is discrete and velocity is interpolated.",
            new[] { Axis("design", "Vane design number (1 through 4)", "", 1, 1, 4), Axis("velocity", "Duct velocity", "fpm", 2_000, 1_000, 4_000) },
            v => I1(A3_9_Velocity, GetRow(A3_9, (int)Math.Round(v["design"]) - 1), v["velocity"])));
        Replace(items, Auto("3-11", "Elbows", "Elbows, 90° Z-shaped, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-11; Co = K-aspect × K-Re × C'o.",
            new[] { Axis("lh", "Spacing ratio L/H", "ratio", 1, 0, 10), Axis("wh", "Aspect ratio W/H", "ratio", 1, .25, 8), Axis("re", "Reynolds number", "", 140_000, 10_000, 2_000_000) },
            v => I1(A3_11_Length, A3_11, v["lh"]) * I1(A3_Aspect, A3_AspectFactor, v["wh"]) * CommonReFactor(v["re"])));
        Replace(items, Auto("3-12", "Elbows", "Combined 90° elbows lying in different planes, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 3-12; Co = K-aspect × K-Re × C'o.",
            new[] { Axis("lw", "Spacing ratio L/W", "ratio", 1, 0, 10), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8), Axis("re", "Reynolds number", "", 140_000, 10_000, 2_000_000) },
            v => I1(A3_12_Length, A3_12, v["lw"]) * I1(A3_Aspect, A3_AspectFactor, v["hw"]) * CommonReFactor(v["re"])));
        Replace(items, Auto("3-13", "Elbows", "Offset, S-shaped (gooseneck), rectangular and round", "upstream/reference duct", "ASHRAE supplemental fitting 3-13; enter the applicable single-elbow C'o from fitting 3-1, 3-2, or 3-5, then Co = K × C'o.",
            new[] { Axis("base", "Single-elbow coefficient C'o", "", .2, 0, 10_000), Axis("angle", "Offset elbow angle", "degrees", 45, 15, 90), Axis("ld", "Spacing ratio L/D", "ratio", 4, 0, 40) },
            v => v["base"] * I2(A3_13_Angle, A3_13_Length, A3_13, v["angle"], v["ld"])));
        Replace(items, Auto("3-14", "Elbows", "Offset, S-shaped in two planes 90° apart, rectangular and round", "upstream/reference duct", "ASHRAE supplemental fitting 3-14; enter the applicable single-elbow C'o, then Co = K × C'o.",
            new[] { Axis("base", "Single-elbow coefficient C'o", "", .2, 0, 10_000), Axis("angle", "Offset elbow angle", "degrees", 60, 60, 90), Axis("ld", "Spacing ratio L/D", "ratio", 4, 0, 40) },
            v => v["base"] * I2(A3_14_Angle, A3_14_Length, A3_14, v["angle"], v["ld"])));
        Replace(items, Auto("3-15", "Elbows", "Four 45° smooth-radius rectangular elbows arranged around an obstruction", "upstream/reference duct", "ASHRAE supplemental fitting 3-15; valid for W/H = 4, r/H = 1.5, and L/H = 1.5.",
            new[] { Axis("velocity", "Duct velocity", "fpm", 1_600, 800, 2_400) },
            v => I1(A3_15_Velocity, A3_15, v["velocity"])));

        Replace(items, Auto("4-1", "Transitions", "Transition, round", "section o/reference duct", "ASHRAE supplemental fitting 4-1.", CreateTransitionInputs(),
            v => I2(A4_Area, A4_Angle, A4_1, v["area"], v["angle"])));
        Replace(items, Auto("4-2", "Transitions", "Transition, rectangular, two sides parallel, symmetrical", "section o/reference duct", "ASHRAE supplemental fitting 4-2.", CreateTransitionInputs(),
            v => I2(A4_Area, A4_Angle, A4_2, v["area"], v["angle"])));
        Replace(items, Auto("4-3", "Transitions", "Transition, rectangular, three sides straight", "section o/reference duct", "ASHRAE supplemental fitting 4-3; published table is tentative.",
            new[] { Axis("area", "Area ratio Ao/A1", "ratio", 1, .06, 10), Axis("angle", "Included angle", "degrees", 30, 10, 90) },
            v => I2(A4_Area, A4_3_Angle, A4_3, v["area"], v["angle"])));
        Replace(items, Auto("4-4", "Transitions", "Transition, rectangular, pyramidal", "section o/reference duct", "ASHRAE supplemental fitting 4-4.", CreateTransitionInputs(),
            v => I2(A4_Area, A4_Angle, A4_4, v["area"], v["angle"])));
        Replace(items, Auto("4-5", "Transitions", "Transition, round/rectangular", "section o/reference duct", "ASHRAE supplemental fitting 4-5.",
            new[] { Axis("area", "Area ratio A1/Ao", "ratio", 4, 2, 10), Axis("angle", "Included angle", "degrees", 30, 8, 180) },
            v => I2(A4_5_Area, A4_5_Angle, A4_5, v["area"], v["angle"])));
        Replace(items, Auto("4-6", "Transitions", "Transition, rectangular to round", "section o/reference duct", "ASHRAE supplemental fitting 4-6; trilinear interpolation by Reynolds number, L/D, and B for the selected area direction.",
            new[]
            {
                ChoiceAxis("direction", "Area direction", 0,
                    ("Ao > A1 (round outlet larger)", 0d), ("Ao < A1 (round outlet smaller)", 1d)),
                Axis("re", "Round-section Reynolds number", "", 100_000, 10_000, 500_000),
                Axis("ld", "Length ratio L/D", "ratio", 3, 1, 5),
                Axis("b", "Shape/area parameter B", "", 1, .1, 50)
            }, v => Math.Round(v["direction"]) == 0
                ? I3(A4_6_Re, A4_6_LargeLength, A4_6_LargeB, A4_6_Large, v["re"], v["ld"], v["b"])
                : I3(A4_6_Re, A4_6_SmallLength, A4_6_SmallB, A4_6_Small, v["re"], v["ld"], v["b"])));
        Replace(items, Auto("4-7", "Transitions", "Transition, rectangular to round, stepped, conical", "section o/reference duct", "ASHRAE supplemental fitting 4-7; trilinear interpolation by area ratio, length ratio, and angle.",
            new[] { Axis("area", "Area ratio Ao/A1", "ratio", .5, .1, .8), Axis("lh", "Length ratio L/H", "ratio", .1, .025, .6), Axis("angle", "Included angle", "degrees", 60, 0, 180) },
            v => I3(A4_7_Area, A4_7_Length, A4_7_Angle, A4_7, v["area"], v["lh"], v["angle"])));

        AddJunctionCalculators(items);

        Replace(items, Auto("6-1", "Obstructions", "Damper, butterfly, round", "upstream/reference duct", "ASHRAE supplemental fitting 6-1; built-in range is limited to the complete 0°-60° portion of the table.",
            new[] { Axis("dd", "Blade/duct diameter ratio D/Do", "ratio", .8, .5, 1), Axis("angle", "Blade angle", "degrees", 30, 0, 60) },
            v => I2(new[] { .5,.6,.7,.8,.9,1d }, new[] { 0d,10,20,30,40,50,60 }, A6_1, v["dd"], v["angle"])));
        Replace(items, Auto("6-2", "Obstructions", "Damper, butterfly, rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 6-2.",
            new[] { Axis("hw", "Aspect ratio H/W", "ratio", 1, .05, 10), Axis("angle", "Blade angle", "degrees", 30, 0, 70) },
            v => RectangularButterfly(v["hw"], v["angle"])));
        Replace(items, Auto("6-7", "Obstructions", "Obstruction, screen, round and rectangular", "upstream/reference duct", "ASHRAE supplemental fitting 6-7.",
            new[] { Axis("area", "Screen/duct area ratio A1/Ao", "ratio", 1, .2, 6), Axis("free", "Screen free-area ratio n", "ratio", .7, .3, 1) },
            v => I2(ScreenAreaRatio, ScreenFreeRatio, A6_7, v["area"], v["free"])));
        Replace(items, Auto("6-9", "Obstructions", "Obstruction, smooth cylinder in round and rectangular ducts", "upstream/reference duct", "ASHRAE supplemental fitting 6-9; Co = K × C'o.",
            new[] { Axis("re", "Cylinder Reynolds number Re'", "", 100_000, .1, 1_000_000), Axis("blocked", "Blocked-area ratio Sm/Ao", "ratio", .1, .05, .2), Axis("offset", "Offset ratio y/D or y/H", "ratio", 0, 0, .4) },
            v => CylinderBase(v["re"], v["blocked"]) * I1(CylinderOffset, CylinderOffsetFactor, v["offset"])));
        Replace(items, Auto("7-1", "Fan-system connections", "Fans discharging into a plenum", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-1.",
            new[] { Axis("blast", "Blast/outlet area ratio Ab/Ao", "ratio", .7, .4, 1), Axis("length", "Effective length ratio L/Le", "ratio", .25, 0, 1) },
            v => I2(A7_FanArea, A7_FanLength, A7_1, v["blast"], v["length"])));
        Replace(items, Auto("7-2", "Fan-system connections", "Single-width single-inlet fan with an outlet duct elbow", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-2; outlet position is discrete: 0=A, 1=B, 2=C, 3=D.", CreateFanElbowInputs(),
            v => FanOutletElbow(A7_2, v)));
        Replace(items, Auto("7-3", "Fan-system connections", "Double-width double-inlet fan with an outlet duct elbow", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-3; outlet position is discrete: 0=A, 1=B, 2=C, 3=D.", CreateFanElbowInputs(),
            v => FanOutletElbow(A7_3, v)));
        Replace(items, Auto("7-4", "Fan-system connections", "Nonuniform elbow into a fan inlet induced by a 90° round smooth-radius elbow without vanes", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-4.",
            new[] { Axis("rd", "Elbow radius ratio r/D", "ratio", 1.5, .75, 3), Axis("ld", "Straight length ratio L/D", "ratio", 2, 0, 5) },
            v => I2(A7_4_Radius, A7_InletLength, A7_4, v["rd"], v["ld"])));
        Replace(items, Auto("7-5", "Fan-system connections", "Nonuniform elbow into a fan inlet induced by 90° mitered and multipiece elbows without vanes", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-5; elbow type is discrete: 0=mitered, 1=three-piece, 2=four-piece or more.",
            new[] { Axis("type", "Elbow type (0 mitered, 1 three-piece, 2 four-plus)", "", 1, 0, 2), Axis("rd", "Elbow radius ratio r/D", "ratio", 1.5, .5, 3), Axis("ld", "Straight length ratio L/D", "ratio", 2, 0, 5) },
            FanInletMultipiece));
        Replace(items, Auto("7-6", "Fan-system connections", "Nonuniform elbow into a fan inlet induced by a 90° square smooth-radius elbow", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-6; vane type is discrete: 0=no vanes, 1=full-radius vanes, 2=short vanes.",
            new[] { Axis("type", "Vane type (0 none, 1 full-radius, 2 short)", "", 0, 0, 2), Axis("rd", "Elbow radius ratio r/D", "ratio", 1, .5, 2), Axis("ld", "Straight length ratio L/D", "ratio", 2.5, 0, 6) },
            FanInletSquare));
        Replace(items, Auto("7-7", "Fan-system connections", "Fans located in plenums and cabinet enclosures", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-7.",
            new[] { Axis("ld", "Clearance ratio L/D", "ratio", .5, .2, .75) },
            v => I1(A7_7_Length, A7_7, v["ld"])));
        Replace(items, Auto("7-8", "Fan-system connections", "Fan without an outlet diffuser", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-8; poor configuration noted in the source.",
            new[] { Axis("blast", "Blast/outlet area ratio Ab/Ao", "ratio", .7, .4, 1) },
            v => I1(A7_8_Area, A7_8, v["blast"])));
        Replace(items, Auto("7-11", "Fan-system connections", "Plane symmetric diffuser at fan outlet with ductwork", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-11.", CreateFanDiffuserInputs(35),
            v => I2(FanAngle, FanAreaRatio, A7_11, v["angle"], v["area"])));
        Replace(items, Auto("7-12", "Fan-system connections", "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 1", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-12.", CreateFanDiffuserInputs(35),
            v => I2(FanAngle, FanAreaRatio, A7_12, v["angle"], v["area"])));
        Replace(items, Auto("7-13", "Fan-system connections", "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 2", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-13.", CreateFanDiffuserInputs(35),
            v => I2(FanAngle, FanAreaRatio, A7_13, v["angle"], v["area"])));
        Replace(items, Auto("7-14", "Fan-system connections", "Plane asymmetric diffuser at fan outlet with ductwork - arrangement 3", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-14.", CreateFanDiffuserInputs(35),
            v => I2(FanAngle, FanAreaRatio, A7_14, v["angle"], v["area"])));
        Replace(items, Auto("7-15", "Fan-system connections", "Pyramidal diffuser at fan outlet with ductwork", "fan outlet/reference duct", "ASHRAE supplemental fitting 7-15.", CreateFanDiffuserInputs(30),
            v => I2(FanAngle30, FanAreaRatio, A7_15, v["angle"], v["area"])));

        items.Add(new FittingDefinition("GRD-GRILLE", "GRDs", "Grille - Titus 350R", "published grille core velocity",
            "Titus 350R published performance tables. Select a nominal duct size; requested airflow is rounded up to the next listed cfm value. Performance data courtesy of Titus.", true,
            new[]
            {
                new FittingInputSpec("sizeIndex", "Nominal Duct Size", "in",
                    GrdPerformanceCatalog.Titus350RSizeIndex("12x12"), 0,
                    GrdPerformanceCatalog.Titus350RSizes.Count - 1, "Published Titus 350R nominal duct size.",
                    GrdPerformanceCatalog.Titus350RSizes.Select((size, index) =>
                        new FittingInputChoice($"{size} in", index)).ToArray())
            }, _ => 0));
        items.Add(new FittingDefinition("GRD-DIFFUSER", "GRDs", "Diffuser - Price SCD Square Cone", "neck velocity",
            "Price SCD published performance table. Requested airflow is rounded up to the next published cfm value for the selected face and neck size.", true,
            new[]
            {
                new FittingInputSpec("face", "Face Size", "in", 24, 12, 24, "Square face size.",
                    new[] { new FittingInputChoice("12 x 12", 12), new FittingInputChoice("20 x 20", 20), new FittingInputChoice("24 x 24", 24) }),
                new FittingInputSpec("neck", "Neck Diameter", "in", 8, 4, 15, "Available neck diameters depend on the selected face size.",
                    new[] { 4d, 5, 6, 7, 8, 10, 12, 14, 15 }.Select(value => new FittingInputChoice($"{value:0} in", value)).ToArray())
            }, _ => 0));
        items.Add(new FittingDefinition("GRD-LINEAR-DIFFUSER", "GRDs", "Linear Diffuser - Titus TBD-80 D", "published diffuser airflow",
            "Titus TBD-80 D published performance tables. The smallest published inlet that covers the requested airflow is selected, then airflow is rounded up to the next listed cfm value. Performance data courtesy of Titus.", true,
            new[]
            {
                new FittingInputSpec("length", "Diffuser Length", "ft", 4, 2, 5, "Published diffuser length.",
                    new[] { new FittingInputChoice("2 ft", 2), new FittingInputChoice("4 ft", 4), new FittingInputChoice("5 ft", 5) }),
                new FittingInputSpec("slotWidth", "Slot Size", "in", 0.75, 0.75, 1.5, "Published slot width.",
                    new[] { new FittingInputChoice("3/4 in", 0.75), new FittingInputChoice("1 in", 1), new FittingInputChoice("1-1/2 in", 1.5) }),
                new FittingInputSpec("slotCount", "Number of Slots", "", 1, 1, 4, "Number of linear diffuser slots.",
                    new[] { new FittingInputChoice("1 slot", 1), new FittingInputChoice("2 slots", 2), new FittingInputChoice("3 slots", 3), new FittingInputChoice("4 slots", 4) })
            }, _ => 0));
        items.Add(new FittingDefinition(VavPerformanceCatalog.WithReheatCode, "VAVs", "VAV with Reheat - Titus DESV", "unit inlet airflow",
            VavPerformanceCatalog.WithReheatReference + " Airside pressure drop is linearly interpolated between published airflow points.", true,
            new[]
            {
                new FittingInputSpec("sizeIndex", "VAV Size", "in", 8, 0,
                    VavPerformanceCatalog.WithReheatSizes.Count - 1, "Published Titus DESV terminal size.",
                    VavPerformanceCatalog.WithReheatSizes.Select((size, index) => new FittingInputChoice(size == "24x16" ? "24 x 16 in" : $"{size} in", index)).ToArray()),
                new FittingInputSpec("rows", "Hot-Water Coil Rows", "rows", 3, 1, 4,
                    "Published coil-row count. Sizes 4 through 12 are available in the supplied table with 3 or 4 rows; sizes 14, 16, and 24 x 16 support 1 through 4 rows.",
                    VavPerformanceCatalog.CoilRows.Select(rows => new FittingInputChoice($"{rows} row{(rows == 1 ? "" : "s")}", rows)).ToArray())
            }, _ => 0));
        items.Add(new FittingDefinition(VavPerformanceCatalog.WithoutReheatCode, "VAVs", "VAV without Reheat - Titus DESV", "unit inlet airflow",
            VavPerformanceCatalog.WithoutReheatReference + " Minimum pressure drop is linearly interpolated between published airflow points.", true,
            new[]
            {
                new FittingInputSpec("sizeIndex", "VAV Size", "", 6, 0,
                    VavPerformanceCatalog.WithoutReheatSizes.Count - 1, "Published Titus DESV terminal size.",
                    VavPerformanceCatalog.WithoutReheatSizes.Select((size, index) => new FittingInputChoice($"Size {size}", index)).ToArray())
            }, _ => 0));
        return items;
    }

    private static IReadOnlyList<FittingDefinition> BuildLegacy()
    {
        var items = new List<FittingDefinition>();

        // Table A-7 — elbows. Values are from pages A.15-A.19 of the supplied fourth edition.
        items.Add(Auto("A7-A", "Elbows", "Elbow, smooth radius (die stamped), round", "upstream",
            "Table A-7A; interpolate R/D and multiply by the angle factor.",
            new[]
            {
                Axis("rd", "Centerline radius / diameter (R/D)", "ratio", 1.5, 0.5, 2.5),
                Axis("angle", "Elbow angle", "degrees", 90, 0, 180)
            }, v => AngleFactorK(v["angle"]) * Named(BaseCoefficient, "Base loss coefficient of the 90° elbow, from the ASHRAE table by R/D.",
                () => I1(RoundElbowRd, RoundElbowC, v["rd"]))));
        items.Add(Auto("A7-B", "Elbows", "Elbow, round, 3 to 5 piece — 90°", "upstream", "Table A-7B. Piece count is limited to 3, 4, or 5.",
            new[] { Axis("pieces", "Number of pieces", "", 5, 3, 5), Axis("rd", "Centerline radius / diameter (R/D)", "ratio", 1.5, .5, 2) },
            v => RoundSegmentedElbow(v["pieces"], v["rd"])));
        items.Add(Auto("A7-C", "Elbows", "Elbow, round, mitered", "upstream",
            "Table A-7C; angle coefficient with Reynolds-number correction.",
            new[]
            {
                Axis("angle", "Elbow angle", "degrees", 90, 20, 90),
                Axis("re", "Reynolds number", "", 140_000, 10_000, 2_000_000)
            }, v => Named(ReynoldsFactor, "Reynolds-number correction factor, from the ASHRAE table by Re.",
                        () => I1(new[] { 10_000d, 20_000, 30_000, 40_000, 60_000, 80_000, 100_000, 140_000, 2_000_000 },
                            new[] { 1.40, 1.26, 1.19, 1.14, 1.09, 1.06, 1.04, 1.0, 1.0 }, v["re"]))
                    * Named(BaseCoefficient, "Base loss coefficient of the mitered elbow, from the ASHRAE table by elbow angle θ.",
                        () => I1(new[] { 20d, 30, 45, 60, 75, 90 }, new[] { .08, .16, .34, .55, .81, 1.2 }, v["angle"]))));
        items.Add(Auto("A7-D", "Elbows", "Elbow, rectangular, mitered", "upstream",
            "Table A-7D; bilinear interpolation by angle and H/W.",
            new[] { Axis("angle", "Elbow angle", "degrees", 90, 20, 90), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8) },
            v => I2(A7D_Angles, A7D_Hw, A7D, v["angle"], v["hw"])));
        items.Add(Auto("A7-E", "Elbows", "Elbow, rectangular, mitered with converging or diverging flow", "upstream",
            "Table A-7E; bilinear interpolation by H/W and W1/W.",
            new[] { Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 100), Axis("w1w", "Width ratio W1/W", "ratio", 1.2, .6, 2) },
            v => I2(new[] { .25, 1d, 4, 100 }, new[] { .6, .8, 1.2, 1.4, 1.6, 2d }, A7E, v["hw"], v["w1w"])));
        items.Add(Auto("A7-F", "Elbows", "Elbow, rectangular, smooth radius without vanes", "upstream",
            "Table A-7F base coefficient. For non-90° fittings apply A7-A's angle factor; Reynolds correction is not applied below its published range.",
            new[] { Axis("rw", "Centerline radius / width (R/W)", "ratio", 1.0, .5, 2), Axis("hw", "Aspect ratio H/W", "ratio", 1, .25, 8), Axis("angle", "Elbow angle", "degrees", 90, 0, 180) },
            v => AngleFactorK(v["angle"])
                 * Named(BaseCoefficient, "Base loss coefficient of the 90° elbow, from the ASHRAE table by R/W and H/W.",
                     () => I2(new[] { .5, .75, 1d, 1.5, 2 }, new[] { .25, .5, .75, 1d, 1.5, 2, 3, 4, 5, 6, 8 }, A7F, v["rw"], v["hw"]))));
        items.Add(Manual("A7-G", "Elbows", "Elbow, rectangular, smooth radius with splitter vanes", "upstream", "Table A-7G; select vane count, R/W, curve ratio, and H/W."));
        items.Add(Manual("A7-H", "Elbows", "Elbow, rectangular, mitered with single-thickness turning vanes", "upstream", "Table A-7H."));
        items.Add(Manual("A7-I", "Elbows", "Elbow, rectangular, mitered with double-thickness turning vanes", "upstream", "Table A-7I."));
        items.Add(Auto("A7-J", "Elbows", "Elbows, 90°, rectangular in different planes", "upstream",
            "Table A-7J for H/W = 1.0.", new[] { Axis("lw", "Spacing ratio L/W", "ratio", 1, 0, 10) },
            v => I1(new[] { 0d, .4, .6, .8, 1, 1.2, 1.4, 1.6, 1.8, 2, 2.4, 2.8, 3.2, 4, 5, 6, 7, 9, 10 },
                    new[] { 1.2, 2.4, 2.9, 3.3, 3.4, 3.4, 3.4, 3.3, 3.2, 3.1, 3.2, 3.2, 3.2, 3.0, 2.9, 2.8, 2.7, 2.5, 2.4 }, v["lw"])));
        items.Add(Auto("A7-K", "Elbows", "Elbows, 30°, round, offset", "upstream", "Table A-7K.",
            new[] { Axis("ld", "Spacing ratio L/D", "ratio", 1, 0, 3) },
            v => I1(new[] { 0d, .5, 1, 1.5, 2, 2.5, 3 }, new[] { 0d, .15, .15, .16, .16, .16, .16 }, v["ld"])));
        items.Add(Manual("A7-L", "Elbows", "Elbows, 90°, rectangular wye or tee shape", "upstream", "Table A-7L; use the coefficient for the geometrically similar elbow when V = V1 = V2."));

        AddManualGroup(items, "A8", "Diverging transitions", "upstream", new[]
        {
            "Transition, round, conical", "Transition, rectangular, pyramidal", "Transition, round to rectangular",
            "Transition, rectangular to round", "Transition, rectangular sides straight", "Transition, symmetric at fan with duct sides straight",
            "Transition, asymmetric at fan with duct sides straight, top level", "Transition, asymmetric at fan with duct sides straight, top 10° down",
            "Transition, asymmetric at fan with duct sides straight, top 10° up", "Transition, pyramidal at fan with duct sides straight"
        });
        Replace(items, Auto("A8-B", "Diverging transitions", "Transition, rectangular, pyramidal", "upstream", "Table A-8B.",
            new[] { Axis("area", "Area ratio A1/A", "ratio", 4, 2, 10), Axis("angle", "Included angle", "degrees", 30, 16, 180) },
            v => I2(new[] { 2d, 4, 6, 10 }, new[] { 16d, 20, 30, 45, 60, 90, 120, 180 }, A8B, v["area"], v["angle"])));

        items.Add(Auto("A9-A", "Converging transitions", "Contraction, round and rectangular, gradual to abrupt", "downstream",
            "Table A-9A; bilinear interpolation by entering/leaving area ratio and included angle.",
            new[] { Axis("area", "Area ratio A1/A", "ratio", 2, 2, 10), Axis("angle", "Included angle", "degrees", 30, 10, 180) },
            v => I2(new[] { 2d, 4, 6, 10 }, new[] { 10d, 27.5, 55, 90, 120, 150, 180 }, A9A, v["area"], v["angle"])));
        items.Add(Auto("A9-B", "Converging transitions", "Contraction, conical, round and rectangular", "downstream", "Table A-9B; C = K × Co.",
            new[] { Axis("area", "Area ratio A/A1", "ratio", .5, 0, 1), Axis("ld", "Length ratio L/D", "ratio", .1, .025, .6), Axis("angle", "Included angle", "degrees", 30, 0, 180) },
            v => I1(new[] { 0d,.2,.4,.6,.8,.9,1 }, new[] { 1d,.85,.68,.50,.30,.18,0 }, v["area"])
                 * I2(new[] { .025,.05,.075,.1,.15,.6 }, EntryAngle, A9B, v["ld"], v["angle"])));
        items.Add(Auto("A9-C", "Converging transitions", "Contraction, rectangular slot to round", "downstream",
            "Table A-9C; interpolation by Reynolds number.", new[] { Axis("re", "Reynolds number", "", 100_000, 10_000, 400_000) },
            v => I1(new[] { 10_000d, 20_000, 40_000, 60_000, 80_000, 100_000, 200_000, 400_000 },
                    new[] { .27, .25, .20, .17, .14, .11, .04, 0d }, v["re"])));

        AddManualGroup(items, "A10", "Converging junctions", "downstream", new[]
        {
            "Converging wye, round", "Converging tee, 90°, round", "Converging tee, round branch to rectangular main",
            "Converging tee, rectangular main and branch", "Converging wye, conical round", "Converging tee, 45° entry branch to rectangular main",
            "Symmetrical wye, dovetail, rectangular", "Converging wye, rectangular", "Wye, rectangular and round"
        });
        AddManualGroup(items, "A11", "Diverging junctions", "upstream", new[]
        {
            "Tee or wye, 30° to 90°, round", "90° conical tee, round", "45° conical wye, round",
            "90° tee, round, rolled 45° with 45° elbow, branch 90° to main", "90° tee, round, with 90° elbow, branch 90° to main",
            "90° tee, round, rolled 45° with 60° elbow, branch 45° to main", "90° conical tee, round, rolled 45° with 45° elbow, branch 90° to main",
            "90° conical tee, round, rolled 45° with 60° elbow, branch 45° to main", "45° wye, round, rolled 45° with 60° elbow, branch 90° to main",
            "45° conical wye, round, rolled 45° with 60° elbow, branch 90° to main", "45° wye, round, rolled 45° with 30° elbow, branch 45° to main",
            "45° conical wye, round, rolled 45° with 30° elbow, branch 45° to main", "45° conical main and branch with 45° elbow, branch 90° to main",
            "Tee, 45° rectangular main and branch", "Tee, 45° entry, rectangular main and branch with damper", "Tee, rectangular main and branch",
            "Tee, rectangular main and branch with damper", "Tee, rectangular main and branch with extractor", "Tee, rectangular main to round branch",
            "Wye, rectangular", "Tee, rectangular main to conical branch", "Wye, rectangular", "Symmetrical wye, dovetail, rectangular",
            "Wye, rectangular and round", "Tee, rectangular reducing, 45° entry branch"
        });

        items.Add(Auto("A12-A", "Entries", "Duct mounted in wall, round and rectangular", "downstream", "Table A-12A.",
            new[] { Axis("td", "Wall thickness ratio t/D", "ratio", .01, 0, .05), Axis("ld", "Duct projection ratio L/D", "ratio", .2, 0, 1) },
            v => I2(new[] { 0d, .02, .05 }, new[] { 0d, .002, .01, .05, .2, .5, 1 }, A12A, v["td"], v["ld"])));
        items.Add(Auto("A12-B", "Entries", "Smooth covering bellmouth, round, without end wall", "downstream", "Table A-12B.",
            new[] { Axis("rd", "Bellmouth radius ratio R/D", "ratio", .1, 0, .2) }, v => I1(BellmouthRd, A12B, v["rd"])));
        items.Add(Auto("A12-C", "Entries", "Smooth covering bellmouth, round, with end wall", "downstream", "Table A-12C.",
            new[] { Axis("rd", "Bellmouth radius ratio R/D", "ratio", .1, 0, .2) }, v => I1(BellmouthRd, A12C, v["rd"])));
        items.Add(Auto("A12-D", "Entries", "Conical converging bellmouth, round and rectangular without end wall", "downstream", "Table A-12D.",
            new[] { Axis("ld", "Length ratio L/D", "ratio", .1, .025, .6), Axis("angle", "Included angle", "degrees", 30, 0, 180) },
            v => I2(new[] { .025, .05, .1, .25, .6 }, EntryAngle, A12D, v["ld"], v["angle"])));
        items.Add(Auto("A12-E", "Entries", "Conical converging bellmouth, round and rectangular with end wall", "downstream", "Table A-12E.",
            new[] { Axis("ld", "Length ratio L/D", "ratio", .1, .025, .6), Axis("angle", "Included angle", "degrees", 30, 0, 180) },
            v => I2(new[] { .025, .05, .075, .1, .15, .6 }, EntryAngle, A12E, v["ld"], v["angle"])));
        items.Add(Auto("A12-F", "Entries", "Intake hood", "downstream", "Table A-12F.",
            new[] { Axis("angle", "Hood angle", "degrees", 15, 0, 15), Axis("ld", "Length ratio L/D", "ratio", .5, .1, .9) },
            v => I2(new[] { 0d, 15 }, new[] { .1, .2, .3, .4, .5, .6, .7, .8, .9 }, A12F, v["angle"], v["ld"])));
        items.Add(Auto("A12-G", "Entries", "Hood, tapered, flanged or unflanged", "downstream", "Table A-12G. Shape selector: 0 = round, 1 = rectangular.",
            new[] { Axis("shape", "Shape (0 round, 1 rectangular)", "", 0, 0, 1), Axis("angle", "Included angle", "degrees", 40, 0, 180) },
            v => I2(new[] { 0d, 1 }, new[] { 0d, 20, 40, 60, 80, 100, 120, 140, 160, 180 }, A12G, Math.Round(v["shape"]), v["angle"])));
        items.Add(Manual("A12-H", "Entries", "Hood, canopy island or range", "downstream", "Table A-12H gives minimum airflow and velocity criteria; C is 0.5 plus clean filter resistance and 0.1 in. w.g."));
        items.Add(Manual("A12-I", "Entries", "Hood, slot (dishwasher)", "downstream", "Table A-12I combines slot velocity pressure and A12-G duct loss."));

        items.Add(Auto("A13-A", "Exits", "Exhaust hood", "upstream", "Table A-13A.",
            new[] { Axis("angle", "Hood angle", "degrees", 15, 0, 15), Axis("ld", "Length ratio L/D", "ratio", .5, .1, 1) },
            v => I2(new[] { 0d, 15 }, new[] { .1, .2, .25, .3, .35, .4, .5, .6, .8, 1 }, A13A, v["angle"], v["ld"])));
        items.Add(Manual("A13-B", "Exits", "Exit, conical, round, with or without a wall", "upstream", "Table A-13B."));
        items.Add(Auto("A13-C", "Exits", "Exit, plane diffuser, rectangular with or without a wall", "upstream", "Table A-13C.",
            new[] { Axis("area", "Area ratio As/A", "ratio", 4, 2, 6), Axis("angle", "Included angle", "degrees", 30, 14, 90) },
            v => I2(new[] { 2d, 4, 6 }, new[] { 14d, 20, 30, 45, 60, 90 }, A13C, v["area"], v["angle"])));
        items.Add(Auto("A13-D", "Exits", "Exit, pyramidal diffuser, rectangular with or without a wall", "upstream", "Table A-13D.",
            new[] { Axis("area", "Area ratio As/A", "ratio", 4, 2, 10), Axis("angle", "Included angle", "degrees", 30, 10, 60) },
            v => I2(new[] { 2d, 4, 6, 10 }, new[] { 10d, 14, 20, 30, 45, 60 }, A13D, v["area"], v["angle"])));
        items.Add(Manual("A13-E", "Exits", "Exit, discharge to atmosphere from a 90° elbow, round and rectangular", "upstream", "Table A-13E."));
        items.Add(Manual("A13-F", "Exits", "Exit, duct flush with wall, flow along wall", "upstream", "Table A-13F."));
        items.Add(Auto("A13-G", "Exits", "Plane asymmetric diffuser at fan outlet without ductwork", "upstream", "Table A-13G.",
            new[] { Axis("angle", "Included angle", "degrees", 20, 10, 35), Axis("area", "Area ratio A1/A", "ratio", 2, 1.5, 4) },
            v => I2(new[] { 10d, 15, 20, 25, 30, 35 }, new[] { 1.5, 2d, 2.5, 3, 3.5, 4 }, A13G, v["angle"], v["area"])));
        items.Add(Auto("A13-H", "Exits", "Pyramidal diffuser at fan outlet without ductwork", "upstream", "Table A-13H.",
            new[] { Axis("angle", "Included angle", "degrees", 20, 10, 30), Axis("area", "Area ratio A1/A", "ratio", 2, 1.5, 4) },
            v => I2(new[] { 10d, 15, 20, 25, 30 }, new[] { 1.5, 2d, 2.5, 3, 3.5, 4 }, A13H, v["angle"], v["area"])));
        items.Add(Auto("A13-I", "Exits", "Fan, free discharge, plenum", "upstream", "Table A-13I. Wheel selector: 0 = airfoil/backward curved; 1 = backward inclined; 2 = forward curved.",
            new[] { Axis("wheel", "Wheel type (0, 1, or 2)", "", 0, 0, 2) }, v => new[] { 1.0, 1.5, 1.8 }[(int)Math.Round(v["wheel"])]));
        items.Add(Auto("A13-J", "Exits", "Exit, abrupt, round and rectangular, with or without a wall", "upstream", "Table A-13J.", Array.Empty<FittingInputSpec>(), _ => 1.0));

        items.Add(Auto("A14-A", "Screens and plates", "Screen in duct, round and rectangular", "upstream", "Table A-14A.",
            new[] { Axis("free", "Free area ratio As/A", "ratio", .7, .3, 1) },
            v => I1(new[] { .3, .4, .5, .55, .6, .65, .7, .75, .8, .9, 1d }, new[] { 6.2, 3, 1.7, 1.3, .97, .75, .58, .44, .32, .14, 0 }, v["free"])));
        items.Add(Auto("A14-B", "Screens and plates", "Perforated plate in duct, thick, round and rectangular", "upstream", "Table A-14B.",
            new[] { Axis("td", "Plate thickness / hole diameter (t/D)", "ratio", .2, .015, .6), Axis("free", "Free area ratio", "ratio", .5, .2, .9) },
            v => I2(new[] { .015, .2, .4, .6 }, new[] { .2, .25, .3, .4, .5, .6, .7, .8, .9 }, A14B, v["td"], v["free"])));

        items.Add(Auto("A15-A", "Obstructions", "Damper, butterfly, thin plate, round", "upstream", "Table A-15A; 0° is fully open.",
            new[] { Axis("angle", "Blade angle from fully open", "degrees", 0, 0, 60) }, v => I1(DamperAngle, A15A, v["angle"])));
        items.Add(Auto("A15-B", "Obstructions", "Damper, butterfly, thin plate, rectangular", "upstream", "Table A-15B; 0° is fully open.",
            new[] { Axis("angle", "Blade angle from fully open", "degrees", 0, 0, 60) }, v => I1(DamperAngle, A15B, v["angle"])));
        items.Add(Auto("A15-C", "Obstructions", "Damper, gate, round", "upstream", "Table A-15C.",
            new[] { Axis("hd", "Open height ratio h/D", "ratio", .8, .2, .9) },
            v => I1(new[] { .2, .3, .4, .5, .6, .7, .8, .9 }, new[] { 35d, 10, 4.6, 2.1, .98, .44, .17, .06 }, v["hd"])));
        items.Add(Auto("A15-D", "Obstructions", "Damper, gate, rectangular", "upstream", "Table A-15D.",
            new[] { Axis("hw", "Aspect ratio H/W", "ratio", 1, .5, 2), Axis("hh", "Open height ratio h/H", "ratio", .8, .3, .9) },
            v => I2(new[] { .5, 1d, 1.5, 2 }, new[] { .3, .4, .5, .6, .7, .8, .9 }, A15D, v["hw"], v["hh"])));
        items.Add(Auto("A15-E", "Obstructions", "Damper, rectangular, parallel blades", "upstream", "Table A-15E; 0° is fully open.",
            new[] { Axis("lr", "Blade-length/perimeter ratio L/R", "ratio", .6, .3, 1.5), Axis("angle", "Blade angle", "degrees", 30, 0, 80) },
            v => I2(DamperLr, DamperAngleWide, A15E, v["lr"], v["angle"])));
        items.Add(Auto("A15-F", "Obstructions", "Damper, rectangular, opposed blades", "upstream", "Table A-15F; 0° is fully open.",
            new[] { Axis("lr", "Blade-length/perimeter ratio L/R", "ratio", .6, .3, 1.5), Axis("angle", "Blade angle", "degrees", 30, 0, 80) },
            v => I2(DamperLr, DamperAngleWide, A15F, v["lr"], v["angle"])));
        items.Add(Auto("A15-G", "Obstructions", "Damper, rectangular, opposed blades (alternate construction)", "upstream", "Table A-15G.",
            new[] { Axis("angle", "Blade angle from fully open", "degrees", 0, 0, 60) },
            v => I1(DamperAngle, new[] { .50, .65, 1.6, 4.0, 9.4, 24d, 67d }, v["angle"])));
        items.Add(Manual("A15-H", "Obstructions", "Obstruction, smooth cylinder in round and rectangular ducts", "upstream", "Table A-15H; C = K × Co by Reynolds number, blocked area, and offset."));
        items.Add(Auto("A15-I", "Obstructions", "Round duct, depressed to avoid an obstruction", "upstream", "Table A-15I, valid at L/D = 0.33.", Array.Empty<FittingInputSpec>(), _ => .24));
        items.Add(Auto("A15-J", "Obstructions", "Rectangular duct, depressed to avoid an obstruction", "upstream", "Table A-15J.",
            new[] { Axis("wh", "Aspect ratio W/H", "ratio", 1, 1, 4), Axis("lh", "Depression ratio L/H", "ratio", .25, .125, .3) },
            v => I2(new[] { 1d, 4 }, new[] { .125, .15, .25, .30 }, new[,] { { .26, .30, .33, .35 }, { .10, .14, .22, .30 } }, v["wh"], v["lh"])));
        items.Add(Auto("A15-K", "Obstructions", "Rectangular duct with four 45° smooth-radius elbows to avoid obstruction", "upstream", "Table A-15K; valid for W/H = 4, R/H = 1, L/H = 1.5.",
            new[] { Axis("velocity", "Duct velocity", "fpm", 1200, 800, 2400) },
            v => I1(new[] { 800d, 1200, 1600, 2000, 2400 }, new[] { .18, .22, .24, .25, .26 }, v["velocity"])));
        items.Add(Manual("A15-L", "Obstructions", "Rectangular duct with four 90° mitered elbows to avoid obstruction", "upstream", "Table A-15L; select vane type, L/H, and velocity band."));

        return items;
    }

    private static FittingDefinition Auto(string code, string category, string description, string referenceVelocity,
        string sourceNote, IReadOnlyList<FittingInputSpec> inputs, Func<IReadOnlyDictionary<string, double>, double> calculator) =>
        new(code, category, description, referenceVelocity, sourceNote, true, inputs, calculator);

    private static FittingDefinition Manual(string code, string category, string description, string referenceVelocity, string sourceNote) =>
        new(code, category, description, referenceVelocity, sourceNote + " Reference coefficient entry is provided because the source logic has not been safely digitized.",
            false, new[] { category == "Junctions" ? SignedManualCoefficient : ManualCoefficient }, v => v["c"]);

    private static void AddManualGroup(List<FittingDefinition> items, string table, string category, string referenceVelocity, IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            var letter = (char)('A' + i);
            items.Add(Manual($"{table}-{letter}", category, names[i], referenceVelocity, $"Table {table}-{letter}."));
        }
    }

    private static void AddNumberedManualGroup(List<FittingDefinition> items, int categoryNumber, string category,
        string referenceVelocity, IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            var code = $"{categoryNumber}-{i + 1}";
            items.Add(Manual(code, category, names[i], referenceVelocity,
                $"ASHRAE supplemental fitting {code}."));
        }
    }

    private static void Map(List<FittingDefinition> items, IReadOnlyDictionary<string, FittingDefinition> legacy,
        string legacyCode, string ashraeCode)
    {
        var index = items.FindIndex(x => string.Equals(x.Code, ashraeCode, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || !legacy.TryGetValue(legacyCode, out var source))
            throw new InvalidOperationException($"Cannot map fitting {legacyCode} to {ashraeCode}.");
        var target = items[index];
        items[index] = new FittingDefinition(
            target.Code,
            target.Category,
            target.Description,
            target.ReferenceVelocity,
            $"ASHRAE supplemental fitting {ashraeCode}; coefficients are interpolated from its published table.",
            true,
            source.Inputs,
            source.CalculateCoefficient);
    }

    private static void Replace(List<FittingDefinition> items, FittingDefinition replacement)
    {
        var index = items.FindIndex(x => string.Equals(x.Code, replacement.Code, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new InvalidOperationException($"Cannot replace missing fitting {replacement.Code}.");
        items[index] = replacement;
    }

    private static double RoundSegmentedElbow(double pieces, double rd)
    {
        return (int)Math.Round(pieces) switch
        {
            3 => I1(new[] { .5,.75,1d,1.5,2 }, new[] { .98,.54,.42,.34,.33 }, rd),
            4 => I1(new[] { .75,1d,1.5,2 }, new[] { .50,.37,.27,.24 }, rd),
            5 => I1(new[] { .75,1d,1.5,2 }, new[] { .46,.33,.24,.19 }, rd),
            _ => throw new ArgumentOutOfRangeException(nameof(pieces), "Piece count must be 3, 4, or 5.")
        };
    }

    private static FittingInputSpec Axis(string key, string label, string unit, double defaultValue, double min, double max) =>
        new(key, label, unit, defaultValue, min, max);

    private static FittingInputSpec ChoiceAxis(string key, string label, double defaultValue,
        params (string Label, double Value)[] choices) =>
        new(key, label, "", defaultValue, choices.Min(x => x.Value), choices.Max(x => x.Value), "",
            choices.Select(x => new FittingInputChoice(x.Label, x.Value)).ToArray());

    private static void AddJunctionCalculators(List<FittingDefinition> items)
    {
        FittingDefinition Junction(string code, IReadOnlyList<FittingInputSpec> inputs,
            Func<IReadOnlyDictionary<string, double>, double> calculator, string detail = "")
        {
            var original = items.First(x => x.Code == code);
            return Auto(code, "Junctions", original.Description, "common/reference duct",
                $"ASHRAE supplemental fitting {code}; branch and straight/main coefficients are selected separately and interpolated from the published table.{detail}",
                inputs, calculator);
        }

        Replace(items, Junction("5-1", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v,
                () => I2(JunctionFlowFraction, JunctionAreaRatio, A5_1_Branch, v["qbqc"], v["abac"]),
                () => I2(JunctionFlowFraction, JunctionAreaRatio, A5_1_Main, v["qbqc"], v["abac"]))));
        Replace(items, Junction("5-2", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v,
                () => I2(JunctionFlowFraction, JunctionAreaRatio, A5_2_Branch, v["qbqc"], v["abac"]),
                () => I2(JunctionFlowFraction, JunctionAreaRatio, A5_2_Main, v["qbqc"], v["abac"]))));
        Replace(items, Junction("5-3", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v,
                () => I2(JunctionFlowFraction, JunctionAreaRatio, A5_3_Branch, v["qsqc"], v["abac"]),
                () => I1(JunctionFlowFraction, A5_3_Main, v["qsqc"]))));

        var convergingRectangularConfigurations = new[]
        {
            ("Ab/As 0.33; Ab/Ac 0.25", 0d), ("Ab/As 0.50; Ab/Ac 0.50", 1d),
            ("Ab/As 0.67; Ab/Ac 0.50", 2d), ("Ab/As 1.00; Ab/Ac 0.50", 3d),
            ("Ab/As 1.00; Ab/Ac 1.00", 4d), ("Ab/As 1.33; Ab/Ac 1.00", 5d),
            ("Ab/As 2.00; Ab/Ac 1.00", 6d)
        };
        Replace(items, Junction("5-6", new[]
            {
                ChoiceAxis("configuration", "Published area-ratio configuration", 4, convergingRectangularConfigurations)
            }, v =>
            {
                var row = (int)Math.Round(v["configuration"]);
                return JunctionPathValue(v,
                    () => I1(A5_6_Flow, GetRow(A5_6_Branch, row), v["qbqc"]),
                    () => I1(A5_6_Flow, GetRow(A5_6_Main, row), v["qbqs"]));
            }, " Select the published Ab/As and Ab/Ac row."));
        Replace(items, Junction("5-7", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => JunctionVelocityBand(v, A5_7_Branch),
                () => I1(JunctionFlowFraction, A5_3_Main, v["qsqc"]))));
        Replace(items, Junction("5-8", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => JunctionVelocityBand(v, A5_8_Branch),
                () => I1(JunctionFlowFraction, A5_3_Main, v["qsqc"]))));
        Replace(items, Junction("5-9", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => JunctionVelocityBand(v, A5_9_Branch),
                () => I1(JunctionFlowFraction, A5_3_Main, v["qsqc"]))));

        Replace(items, Junction("5-10", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_10_Branch, v["vbvs"]), () => JunctionMain(v))));
        Replace(items, Junction("5-11", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_11_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-12", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_12_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-13", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_13_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-14", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_14_Branch, v["vbvs"]), () => JunctionMain(v))));
        Replace(items, Junction("5-15", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_15_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-16", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_16_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-17", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(A5_17_BranchAxis, A5_17_Branch, v["vbvc"]),
                () => I1(A5_17_MainAxis, A5_17_Main, v["vsvc"]))));
        Replace(items, Junction("5-18", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_18_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-19", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_19_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-20", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_20_Branch, v["vbvc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-21", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => I1(JunctionVelocityRatio, A5_21_Branch, v["vbvc"]), () => JunctionMain(v))));

        var configurations = new[]
        {
            ("Ab/As 0.25; Ab/Ac 0.25", 0d), ("Ab/As 0.33; Ab/Ac 0.25", 1d),
            ("Ab/As 0.50; Ab/Ac 0.50", 2d), ("Ab/As 0.67; Ab/Ac 0.50", 3d),
            ("Ab/As 1.00; Ab/Ac 0.50", 4d), ("Ab/As 1.00; Ab/Ac 1.00", 5d),
            ("Ab/As 1.33; Ab/Ac 1.00", 6d), ("Ab/As 2.00; Ab/Ac 1.00", 7d)
        };
        Replace(items, Junction("5-22", new[]
            {
                ChoiceAxis("configuration", "Published area-ratio configuration", 5, configurations)
            }, v =>
            {
                var row = (int)Math.Round(v["configuration"]);
                return JunctionPathValue(v,
                    () => I1(A5_22_Flow, GetRow(A5_22_Branch, row), v["qbqc"]),
                    () => I1(A5_22_Flow, GetRow(A5_22_Main, row), v["qbqc"]));
            }, " Select the published Ab/As and Ab/Ac row."));

        Replace(items, Junction("5-23", new[]
            {
                ChoiceAxis("angle", "Branch angle", 45,
                    ("30°", 30d), ("45°", 45d), ("60°", 60d), ("90°", 90d))
            }, v => JunctionPathValue(v, () => JunctionFiveTwentyThreeBranch(v), () => JunctionMain(v)),
            " The complete branch interpolation domain is 0.2 <= Ab/Ac <= 0.8; the partial 0.1 row is intentionally excluded."));

        Replace(items, Junction("5-24", new[]
            {
                ChoiceAxis("angle", "Branch angle", 45, ("15°", 15d), ("30°", 30d), ("45°", 45d), ("60°", 60d), ("90°", 90d))
            }, v => JunctionPathValue(v,
                () => I2(A5_24_Angle, A5_24_Velocity, A5_24_Branch, v["angle"], v["vbvc"]),
                () => JunctionFiveTwentyFourMain(v))));

        Replace(items, Junction("5-25", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_25_Branch, v["vbvc"], v["qbqs"]), () => JunctionMain(v))));
        Replace(items, Junction("5-26", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v,
                () => I1(new[] { .4,.5,.75,1,1.3,1.5 }, new[] { .80,.83,.90,1,1.1,1.4 }, v["vbvc"]),
                () => JunctionMain(v))));
        Replace(items, Junction("5-27", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_27_Branch, v["vbvc"], v["qbqs"]), () => JunctionMain(v))));
        Replace(items, Junction("5-28", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_28_Branch, v["vbvc"], v["qbqc"]), () => JunctionMain(v))));
        Replace(items, Junction("5-29", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_29_Branch, v["vbvc"], v["qbqc"]), () => JunctionFiveThirtyOneMain(v)),
            " ASHRAE labels this dampered arrangement poor and says it should not be used."));
        Replace(items, Junction("5-30", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_30_Branch, v["vbvc"], v["qbqc"]), () => JunctionFiveThirtyOneMain(v)),
            " ASHRAE labels this dampered arrangement poor and says it should not be used."));
        Replace(items, Junction("5-31", Array.Empty<FittingInputSpec>(), v =>
            JunctionPathValue(v, () => RaggedJunction(A5_31_Branch, v["vbvc"], v["qbqc"]), () => JunctionFiveThirtyOneMain(v)),
            " ASHRAE labels this extractor arrangement poor and says it should not be used."));
        Replace(items, Junction("5-32", new[]
            {
                ChoiceAxis("direction", "Flow direction", 1, ("Converging", 0d), ("Diverging", 1d))
            }, v => I1(new[] { .5,1d }, Math.Round(v["direction"]) == 0
                    ? new[] { .23,.07 }
                    : new[] { .30,.25 }, v["abac"]),
            " The published geometry has two equal branches, each at Qb/Qc = 0.5."));
        Replace(items, Junction("5-33", new[]
            {
                ChoiceAxis("direction", "Flow direction", 1, ("Converging", 0d), ("Diverging", 1d)),
                ChoiceAxis("angle", "Half-angle θ", 45,
                    ("15°", 15d), ("30°", 30d), ("45°", 45d), ("60°", 60d), ("90°", 90d))
            }, JunctionFiveThirtyThree,
            " The coefficient applies to either symmetrical branch."));

        Replace(items, Junction("5-34", new[]
            {
                ChoiceAxis("direction", "Flow direction", 0, ("Converging", 0d), ("Diverging", 1d)),
                ChoiceAxis("branchArea", "First branch area ratio A1b/Ac", .4,
                    ("0.2", .2), ("0.4", .4), ("0.6", .6), ("1.0", 1d)),
                ChoiceAxis("secondFlow", "Second/first branch flow Q2b/Q1b", 1,
                    ("0.5", .5), ("1.0", 1d), ("2.0", 2d))
            }, v => JunctionDoubleWye(v, cross: false),
            " For converging flow, select the published second-branch flow and first-branch area ratios; for diverging flow, fitting 5-23 is used."));
        Replace(items, Junction("5-35", new[]
            {
                ChoiceAxis("direction", "Flow direction", 0, ("Converging", 0d), ("Diverging", 1d)),
                ChoiceAxis("branchArea", "First branch area ratio A1b/Ac", .4,
                    ("0.2", .2), ("0.4", .4), ("0.6", .6), ("0.8", .8), ("1.0", 1d)),
                ChoiceAxis("secondFlow", "Second/first branch flow Q2b/Q1b", 1,
                    ("0.5", .5), ("1.0", 1d), ("2.0", 2d))
            }, v => JunctionDoubleWye(v, cross: true),
            " For converging flow, select the published second-branch flow and first-branch area ratios; for diverging flow, fitting 5-23 is used."));
    }

    /// <summary>
    /// The coefficient of the selected junction path. Only that path's table is read, so the other path can neither
    /// fail the calculation nor add its look-ups or range warnings to this one.
    /// </summary>
    private static double JunctionPathValue(IReadOnlyDictionary<string, double> values, Func<double> branch, Func<double> straight) =>
        values["path"] < .5 ? branch() : straight();

    private static double JunctionMain(IReadOnlyDictionary<string, double> values) =>
        I1(A5_23_MainAxis, A5_23_Main, values["vsvc"]);

    private static double JunctionVelocityBand(IReadOnlyDictionary<string, double> values, double[,] table) =>
        I1(A5_VelocityBandFlow, GetRow(table, values["vcfpm"] < 1200 ? 0 : 1), values["qbqc"]);

    private static double JunctionFiveTwentyThreeBranch(IReadOnlyDictionary<string, double> values)
    {
        var areaRatio = values["abac"];
        var angle = (int)Math.Round(values["angle"]);
        if (angle is 30 or 45)
        {
            if (areaRatio < .2 || areaRatio > .8)
                throw new ArgumentOutOfRangeException("abac", "For 30° and 45°, fitting 5-23 requires Ab/Ac between 0.2 and 0.8.");
            return I2(A5_23_Area, A5_23_Flow, angle == 30 ? A5_23_Branch30 : A5_23_Branch45,
                areaRatio, values["qbqc"]);
        }
        if (areaRatio < .3 || areaRatio > .8)
            throw new ArgumentOutOfRangeException("abac", "For 60° and 90°, the complete fitting 5-23 table requires Ab/Ac between 0.3 and 0.8.");
        return I2(A5_23_LargeAngleArea, A5_23_Flow, angle == 60 ? A5_23_Branch60 : A5_23_Branch90,
            areaRatio, values["qbqc"]);
    }

    private static double JunctionFiveTwentyFourMain(IReadOnlyDictionary<string, double> values)
    {
        if (Math.Round(values["angle"]) != 90)
            return FiveTwentyFourMainColumn(0, values["vsvc"]);

        // At 90° the main coefficient is published in As/Ac columns (0.4, 0.5, 0.6, 0.7, 0.8; 0.4 also covers
        // smaller ratios). The columns end at different Vs/Vc, so they are not interpolated: a ratio between two
        // columns uses both and keeps the larger (conservative) coefficient.
        var area = values["asac"];
        var columnRatios = A5_24_MainAreaColumns;
        if (area <= columnRatios[0] || area >= columnRatios[^1]
            || columnRatios.Any(ratio => Math.Abs(ratio - area) < 1e-9))
        {
            var exact = area <= columnRatios[0] ? 0
                : area >= columnRatios[^1] ? columnRatios.Length - 1
                : Array.FindIndex(columnRatios, ratio => Math.Abs(ratio - area) < 1e-9);
            return FiveTwentyFourMainColumn(exact, values["vsvc"]);
        }
        var upper = Array.FindIndex(columnRatios, ratio => ratio > area);
        var lower = upper - 1;
        var candidates = new List<(int Column, double Value)>();
        foreach (var column in new[] { lower, upper })
            if (values["vsvc"] <= A5_24_MainMaximumVelocity[column])
                candidates.Add((column, FiveTwentyFourMainColumn(column, values["vsvc"])));
        if (candidates.Count == 0)
            throw new ArgumentOutOfRangeException("vsvc",
                $"The 5-24 main columns As/Ac = {columnRatios[lower]:g} and {columnRatios[upper]:g} end at Vs/Vc = {A5_24_MainMaximumVelocity[lower]:g} and {A5_24_MainMaximumVelocity[upper]:g}.");
        var chosen = candidates.MaxBy(candidate => candidate.Value);
        // NaN marks a column that is not published at this Vs/Vc.
        double ValueAt(int column) => candidates.Any(candidate => candidate.Column == column)
            ? candidates.First(candidate => candidate.Column == column).Value
            : double.NaN;
        TableRangeLog.Lookup(new TableLookupStep(TableLookupKind.ConservativeColumn,
            new[] { new TableLookupAxis("", area, columnRatios[lower], columnRatios[upper], false) },
            new[] { ValueAt(lower), ValueAt(upper) },
            chosen.Value,
            candidates.Count == 1 ? "Only one of the two columns is published at this Vs/Vc; that column is used." : null));
        return chosen.Value;
    }

    private static readonly double[] A5_24_MainAreaColumns = { .4, .5, .6, .7, .8 };

    private static double FiveTwentyFourMainColumn(int column, double vsvc)
    {
        var maximumVelocity = A5_24_MainMaximumVelocity[column];
        if (vsvc > maximumVelocity)
            throw new ArgumentOutOfRangeException("vsvc", $"The selected 5-24 main table ends at Vs/Vc = {maximumVelocity:g}.");
        return I1(A5_24_MainVelocity, GetColumn(A5_24_Main, column), vsvc);
    }

    private static double JunctionFiveThirtyOneMain(IReadOnlyDictionary<string, double> values) =>
        I1(A5_31_MainAxis, A5_31_Main, values["vbvc"]);

    private static double JunctionFiveThirtyThree(IReadOnlyDictionary<string, double> values)
    {
        var angle = values["angle"];
        if (Math.Round(values["direction"]) == 0)
        {
            if (angle > 45)
                throw new ArgumentOutOfRangeException("angle", "The converging 5-33 table is published only for 15°, 30°, and 45°.");
            return I2(A5_33_ConvergingAngle, A5_33_ConvergingFlow, A5_33_Converging,
                angle, values["qbqc"]);
        }
        return I2(A5_24_Angle, A5_24_Velocity, A5_24_Branch, angle, values["vbvc"]);
    }

    private static double JunctionDoubleWye(IReadOnlyDictionary<string, double> values, bool cross)
    {
        if (Math.Round(values["direction"]) == 1)
        {
            var diverging = new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase)
            {
                ["angle"] = cross ? 90 : 45
            };
            return JunctionPathValue(values, () => JunctionFiveTwentyThreeBranch(diverging), () => JunctionMain(values));
        }

        var areaIndex = Array.IndexOf(cross ? A5_35_AreaChoices : A5_34_AreaChoices, values["branchArea"]);
        var flowIndex = Array.IndexOf(A5_DoubleBranchFlowChoices, values["secondFlow"]);
        if (areaIndex < 0 || flowIndex < 0)
            throw new ArgumentOutOfRangeException("configuration", "Select a published area and second-branch flow ratio.");
        if (values["path"] < .5)
        {
            var row = (cross ? A5_35_Branch : A5_34_Branch)[areaIndex][flowIndex];
            var axis = A5_DoublePrimaryFlow.Take(row.Length).ToArray();
            if (values["qbqc"] > axis[^1])
                throw new ArgumentOutOfRangeException("qbqc", $"This published branch row ends at Q1b/Qc = {axis[^1]:g}.");
            return I1(axis, row, values["qbqc"]);
        }
        if (cross)
            return I1(JunctionFlowFraction, A5_35_Main, values["qsqc"]);
        var mainRow = flowIndex == 1 ? 1 : 0;
        return I1(JunctionFlowFraction, A5_34_Main[areaIndex][mainRow], values["qsqc"]);
    }

    private static double RaggedJunction(double[][] rows, double outerValue, double innerValue)
    {
        if (rows.Length != A5_RaggedVelocity.Length)
            throw new InvalidOperationException("Invalid ragged junction table.");
        TableRangeLog.Note(outerValue, A5_RaggedVelocity[0], A5_RaggedVelocity[^1]);
        outerValue = Math.Clamp(outerValue, A5_RaggedVelocity[0], A5_RaggedVelocity[^1]);
        var (lower, upper, fraction) = Bracket(A5_RaggedVelocity, outerValue);
        double At(int row)
        {
            var innerAxis = A5_RaggedFlow.Take(rows[row].Length).ToArray();
            if (innerValue < innerAxis[0] || innerValue > innerAxis[^1])
                throw new ArgumentOutOfRangeException("flow ratio",
                    $"The published row at Vb/Vc = {A5_RaggedVelocity[row]:g} covers a flow ratio from {innerAxis[0]:g} to {innerAxis[^1]:g}.");
            return I1(innerAxis, rows[row], innerValue);
        }
        if (lower == upper || fraction <= 1e-12)
            return At(lower);
        if (fraction >= 1 - 1e-12)
            return At(upper);
        var low = At(lower);
        var high = At(upper);
        var blended = low + fraction * (high - low);
        TableRangeLog.Lookup(new TableLookupStep(TableLookupKind.Linear,
            new[] { new TableLookupAxis("", outerValue, A5_RaggedVelocity[lower], A5_RaggedVelocity[upper], false) },
            new[] { low, high }, blended,
            "Between the two published rows: the row values are the two look-ups just above."));
        return blended;
    }

    private static double[] GetColumn(double[,] table, int column)
    {
        if (column < 0 || column >= table.GetLength(1))
            throw new ArgumentOutOfRangeException(nameof(column));
        var values = new double[table.GetLength(0)];
        for (var row = 0; row < values.Length; row++)
            values[row] = table[row, column];
        return values;
    }

    private static double I1(double[] axis, double[] values, double x)
    {
        if (axis.Length != values.Length || axis.Length == 0)
            throw new InvalidOperationException("Invalid fitting lookup table.");
        TableRangeLog.Note(x, axis[0], axis[^1]);
        if (x <= axis[0] || x >= axis[^1])
        {
            var edge = x <= axis[0] ? 0 : axis.Length - 1;
            var outside = x < axis[0] || x > axis[^1];
            TableRangeLog.Lookup(new TableLookupStep(outside ? TableLookupKind.HeldAtEdge : TableLookupKind.Exact,
                new[] { new TableLookupAxis("", x, axis[edge], axis[edge], outside) }, new[] { values[edge] }, values[edge]));
            return values[edge];
        }
        var upper = Array.FindIndex(axis, a => a >= x);
        var lower = upper - 1;
        var fraction = (x - axis[lower]) / (axis[upper] - axis[lower]);
        var result = values[lower] + fraction * (values[upper] - values[lower]);
        if (fraction >= 1 - 1e-12)
            TableRangeLog.Lookup(new TableLookupStep(TableLookupKind.Exact,
                new[] { new TableLookupAxis("", x, axis[upper], axis[upper], false) }, new[] { values[upper] }, values[upper]));
        else
            TableRangeLog.Lookup(new TableLookupStep(TableLookupKind.Linear,
                new[] { new TableLookupAxis("", x, axis[lower], axis[upper], false) },
                new[] { values[lower], values[upper] }, result));
        return result;
    }

    private static double I2(double[] rows, double[] columns, double[,] values, double rowValue, double columnValue)
    {
        if (values.GetLength(0) != rows.Length || values.GetLength(1) != columns.Length)
            throw new InvalidOperationException("Invalid fitting lookup matrix.");
        TableRangeLog.Note(rowValue, rows[0], rows[^1]);
        TableRangeLog.Note(columnValue, columns[0], columns[^1]);
        var originalRow = rowValue;
        var originalColumn = columnValue;
        rowValue = Math.Clamp(rowValue, rows[0], rows[^1]);
        columnValue = Math.Clamp(columnValue, columns[0], columns[^1]);
        var r1 = rowValue >= rows[^1] ? rows.Length - 1 : Math.Max(1, Array.FindIndex(rows, x => x >= rowValue));
        var c1 = columnValue >= columns[^1] ? columns.Length - 1 : Math.Max(1, Array.FindIndex(columns, x => x >= columnValue));
        var r0 = r1 == 0 ? 0 : r1 - 1;
        var c0 = c1 == 0 ? 0 : c1 - 1;
        if (r0 == r1 && c0 == c1)
        {
            TableRangeLog.Lookup(new TableLookupStep(TableLookupKind.Exact,
                new[]
                {
                    new TableLookupAxis("", originalRow, rows[r0], rows[r0], originalRow < rows[0] || originalRow > rows[^1]),
                    new TableLookupAxis("", originalColumn, columns[c0], columns[c0], originalColumn < columns[0] || originalColumn > columns[^1])
                }, new[] { values[r0, c0] }, values[r0, c0]));
            return values[r0, c0];
        }
        var rf = r0 == r1 ? 0 : (rowValue - rows[r0]) / (rows[r1] - rows[r0]);
        var cf = c0 == c1 ? 0 : (columnValue - columns[c0]) / (columns[c1] - columns[c0]);
        var low = values[r0, c0] + cf * (values[r0, c1] - values[r0, c0]);
        var high = values[r1, c0] + cf * (values[r1, c1] - values[r1, c0]);
        var result = low + rf * (high - low);
        TableRangeLog.Lookup(new TableLookupStep(
            (rf <= 1e-12 || rf >= 1 - 1e-12) && (cf <= 1e-12 || cf >= 1 - 1e-12) ? TableLookupKind.Exact : TableLookupKind.Bilinear,
            new[]
            {
                new TableLookupAxis("", originalRow, rows[r0], rows[r1], originalRow < rows[0] || originalRow > rows[^1]),
                new TableLookupAxis("", originalColumn, columns[c0], columns[c1], originalColumn < columns[0] || originalColumn > columns[^1])
            },
            new[] { values[r0, c0], values[r0, c1], values[r1, c0], values[r1, c1] }, result));
        return result;
    }

    /// <summary>Worksheet symbol of an elbow's base (90°) loss coefficient, C'o.</summary>
    internal const string BaseCoefficient = "C'_{o}";
    /// <summary>Worksheet symbol of the elbow angle correction factor, Kθ.</summary>
    internal const string AngleCorrection = "K_{\\theta}";
    /// <summary>Worksheet symbol of the Reynolds-number correction factor, K_Re.</summary>
    internal const string ReynoldsFactor = "K_{Re}";

    private static double Named(string symbol, string meaning, Func<double> evaluate) =>
        TableRangeLog.Named(symbol, meaning, evaluate);

    /// <summary>Elbow angle correction factor Kθ (1.0 at 90°), from the ASHRAE table by elbow angle.</summary>
    private static double AngleFactorK(double angleDegrees) =>
        Named(AngleCorrection, "Angle correction factor (1.0 at 90°), from the ASHRAE table by elbow angle θ.",
            () => I1(AngleAxis, AngleFactor, angleDegrees));

    private static double CommonReFactor(double reynoldsNumber) =>
        I1(CommonReAxis, CommonReValues, reynoldsNumber);

    private static double RectSmoothElbowRe(double radiusRatio, double reynoldsNumber) =>
        I2(new[] { .5,.75 }, SmoothElbowReAxis, SmoothElbowReValues,
            Math.Clamp(radiusRatio, .5, .75), reynoldsNumber);

    private static double RectangularButterfly(double aspectRatio, double angle)
    {
        var values = aspectRatio < .25 ? A6_2_Short : aspectRatio <= 1 ? A6_2_Medium : A6_2_Tall;
        return I1(A6_2_Angle, values, angle);
    }

    private static double CylinderBase(double reynoldsNumber, double blockedAreaRatio) =>
        I2(CylinderRe, CylinderBlocked, CylinderCoefficient, reynoldsNumber, blockedAreaRatio);

    private static FittingInputSpec[] CreateTransitionInputs() =>
    [
        Axis("area", "Area ratio Ao/A1", "ratio", 1, .06, 10),
        Axis("angle", "Included angle", "degrees", 30, 10, 180)
    ];

    private static FittingInputSpec[] CreateFanElbowInputs() =>
    [
        Axis("blast", "Blast/outlet area ratio Ab/Ao", "ratio", .7, .4, 1),
        Axis("position", "Outlet elbow position (0=A, 1=B, 2=C, 3=D)", "", 0, 0, 3),
        Axis("length", "Effective length ratio L/Le", "ratio", .25, 0, 1)
    ];

    private static double ExitWithEnd(IReadOnlyDictionary<string, double> values)
    {
        var velocityRatio = values["velocityRatio"];
        var angle = values["angle"];
        if (Math.Round(values["shape"]) == 0)
            return I2(A2_2_RoundAngle, A2_2_Velocity, A2_2_Round, angle, velocityRatio);

        var band = (int)Math.Round(values["aspectBand"]);
        if (band == 2 && angle > 120)
            throw new ArgumentOutOfRangeException("angle", "The >=5 aspect-ratio band is published only through 120 degrees.");
        return band switch
        {
            0 => I2(A2_2_LowAngle, A2_2_Velocity, A2_2_Low, angle, velocityRatio),
            1 => I2(A2_2_RoundAngle, A2_2_Velocity, A2_2_Round, angle, velocityRatio),
            2 => I2(A2_2_HighAngle, A2_2_Velocity, A2_2_High, angle, velocityRatio),
            _ => throw new ArgumentOutOfRangeException("aspectBand")
        };
    }

    private static double ExitFromElbow(IReadOnlyDictionary<string, double> values) =>
        Math.Round(values["shape"]) == 0
            ? I1(new[] { .9,1.3 }, new[] { 1.5,1.4 }, values["ld"])
            : I2(A2_10_Radius, A2_10_Length, A2_10, values["rw"], values["lw"]);

    private static double SplitterElbow(IReadOnlyDictionary<string, double> values)
    {
        var count = (int)Math.Round(values["vanes"]);
        var radiusRatio = values["rw"];
        var (axis, table, maximum) = count switch
        {
            1 => (A3_7_OneRadius, A3_7_One, .5),
            2 => (A3_7_TwoRadius, A3_7_Two, .3),
            3 => (A3_7_ThreeRadius, A3_7_Three, .1),
            _ => throw new ArgumentOutOfRangeException("vanes")
        };
        if (radiusRatio > maximum)
            throw new ArgumentOutOfRangeException("rw", $"The published table for {count} splitter vane(s) ends at R/W = {maximum:g}.");
        return AngleFactorK(values["angle"])
               * Named(BaseCoefficient, $"Base loss coefficient of the 90° elbow with {count} splitter vane(s), from the ASHRAE table by R/W and H/W.",
                   () => I2(axis, A3_7_Aspect, table, radiusRatio, values["hw"]));
    }

    private static double FanOutletElbow(double[,,] table, IReadOnlyDictionary<string, double> values) =>
        I3(A7_FanArea, A7_FanPosition, A7_FanLength, table,
            values["blast"], Math.Round(values["position"]), values["length"]);

    private static double FanInletMultipiece(IReadOnlyDictionary<string, double> values)
    {
        var type = (int)Math.Round(values["type"]);
        if (type == 0)
            return I1(A7_InletLength, new[] { 3.2,2,1 }, values["ld"]);
        return I2(A7_5_Radius, A7_InletLength, type == 1 ? A7_5_Three : A7_5_Four,
            values["rd"], values["ld"]);
    }

    private static double FanInletSquare(IReadOnlyDictionary<string, double> values)
    {
        var type = (int)Math.Round(values["type"]);
        return type switch
        {
            0 => I2(A7_6_NoVaneRadius, A7_SquareLength, A7_6_NoVane, values["rd"], values["ld"]),
            1 => I2(A7_6_VaneRadius, A7_SquareLength, A7_6_FullVane, values["rd"], values["ld"]),
            2 => I2(A7_6_VaneRadius, A7_SquareLength, A7_6_ShortVane, values["rd"], values["ld"]),
            _ => throw new ArgumentOutOfRangeException("type")
        };
    }

    private static double[] GetRow(double[,] table, int row)
    {
        if (row < 0 || row >= table.GetLength(0))
            throw new ArgumentOutOfRangeException(nameof(row));
        var values = new double[table.GetLength(1)];
        for (var column = 0; column < values.Length; column++)
            values[column] = table[row, column];
        return values;
    }

    private static double I3(double[] firstAxis, double[] secondAxis, double[] thirdAxis, double[,,] values,
        double first, double second, double third)
    {
        if (values.GetLength(0) != firstAxis.Length || values.GetLength(1) != secondAxis.Length || values.GetLength(2) != thirdAxis.Length)
            throw new InvalidOperationException("Invalid fitting lookup cube.");
        TableRangeLog.Note(first, firstAxis[0], firstAxis[^1]);
        TableRangeLog.Note(second, secondAxis[0], secondAxis[^1]);
        TableRangeLog.Note(third, thirdAxis[0], thirdAxis[^1]);
        var (firstInput, secondInput, thirdInput) = (first, second, third);
        first = Math.Clamp(first, firstAxis[0], firstAxis[^1]);
        second = Math.Clamp(second, secondAxis[0], secondAxis[^1]);
        third = Math.Clamp(third, thirdAxis[0], thirdAxis[^1]);
        var (a0, a1, af) = Bracket(firstAxis, first);
        var (b0, b1, bf) = Bracket(secondAxis, second);
        var (c0, c1, cf) = Bracket(thirdAxis, third);
        double Blend(int ai, int bi) => values[ai, bi, c0] + cf * (values[ai, bi, c1] - values[ai, bi, c0]);
        var lowA = Blend(a0, b0) + bf * (Blend(a0, b1) - Blend(a0, b0));
        var highA = Blend(a1, b0) + bf * (Blend(a1, b1) - Blend(a1, b0));
        var result = lowA + af * (highA - lowA);
        static bool Outside(double value, double[] axis) => value < axis[0] || value > axis[^1];
        TableRangeLog.Lookup(new TableLookupStep(
            af <= 1e-12 && bf <= 1e-12 && cf <= 1e-12 ? TableLookupKind.Exact : TableLookupKind.Trilinear,
            new[]
            {
                new TableLookupAxis("", firstInput, firstAxis[a0], firstAxis[a1], Outside(firstInput, firstAxis)),
                new TableLookupAxis("", secondInput, secondAxis[b0], secondAxis[b1], Outside(secondInput, secondAxis)),
                new TableLookupAxis("", thirdInput, thirdAxis[c0], thirdAxis[c1], Outside(thirdInput, thirdAxis))
            },
            new[]
            {
                values[a0, b0, c0], values[a0, b0, c1], values[a0, b1, c0], values[a0, b1, c1],
                values[a1, b0, c0], values[a1, b0, c1], values[a1, b1, c0], values[a1, b1, c1]
            }, result));
        return result;
    }

    private static (int Lower, int Upper, double Fraction) Bracket(double[] axis, double value)
    {
        if (value <= axis[0]) return (0, 0, 0);
        if (value >= axis[^1]) return (axis.Length - 1, axis.Length - 1, 0);
        var upper = Array.FindIndex(axis, x => x >= value);
        var lower = upper - 1;
        return (lower, upper, (value - axis[lower]) / (axis[upper] - axis[lower]));
    }

    private static FittingInputSpec[] CreateFanDiffuserInputs(double maximumAngle) =>
    [
        Axis("angle", "Included angle", "degrees", 20, 10, maximumAngle),
        Axis("area", "Area ratio A1/Ao", "ratio", 2, 1.5, 4)
    ];

    private static readonly double[] A1_8_Re = { 4_000,10_000,20_000,100_000 };
    private static readonly double[] A1_8_Area = { .2,.3,.4,.5,.6,.7,.8,.9 };
    private static readonly double[,] A1_8 =
    {
        { 45,18,7.9,3.9,2.3,1.3,.83,.51 }, { 49,20,9.2,4.4,2.7,1.5,.96,.59 },
        { 50,21,9.3,4.9,2.9,1.6,1.1,.65 }, { 55,23,11,5.6,3.3,1.9,1.2,.75 }
    };

    private static readonly double[] A2_2_Velocity = { 0,.5,1,1.5,2 };
    private static readonly double[] A2_2_RoundAngle = { 45,60,90,120,150 };
    private static readonly double[,] A2_2_Round =
    {
        { 1,1,1.1,1.3,1.6 }, { 1,.90,1.1,1.4,1.6 }, { 1,.80,.95,1.4,1.7 },
        { 1,.80,.95,1.3,1.7 }, { 1,.82,.83,1,1.3 }
    };
    private static readonly double[] A2_2_LowAngle = { 45,90,120,150 };
    private static readonly double[,] A2_2_Low =
    {
        { 1,.95,1.2,1.5,1.8 }, { 1,.95,1.2,1.5,1.8 },
        { 1,1.1,1.1,1.4,1.9 }, { 1,.95,.95,1.4,1.8 }
    };
    private static readonly double[] A2_2_HighAngle = { 45,60,90,120 };
    private static readonly double[,] A2_2_High =
    {
        { 1,.92,.93,1.1,1.3 }, { 1,.87,.87,1,1.3 },
        { 1,.82,.80,.97,1.2 }, { 1,.80,.76,.90,.98 }
    };
    private static readonly double[] A2_4_Area = { 2,4,6,10,16 };
    private static readonly double[] A2_4_Angle = { 8,10,14,20,30,45,60 };
    private static readonly double[,] A2_4 =
    {
        { .36,.33,.37,.51,.90,1,1 }, { .24,.21,.28,.40,.70,.99,1 },
        { .20,.19,.26,.37,.67,.99,1 }, { .18,.16,.24,.36,.68,.99,1 },
        { .16,.16,.20,.36,.66,.99,1 }
    };
    private static readonly double[] A2_5_Ld = { .5,1,2,3,4,5,6,8,10,12,14 };
    private static readonly double[] A2_5 = { .41,.32,.24,.20,.17,.15,.14,.12,.11,.11,.10 };
    private static readonly double[] A2_6_Area = { 2,4,6 };
    private static readonly double[] A2_6_Angle = { 8,10,14,20,30,45,60 };
    private static readonly double[,] A2_6 =
    {
        { .50,.51,.56,.63,.80,.96,1 }, { .34,.38,.48,.63,.76,.91,1 },
        { .32,.34,.41,.56,.70,.84,.96 }
    };
    private static readonly double[] A2_7_Lh = { .5,1,2,3,4,5,6,8,10,12,14 };
    private static readonly double[] A2_7 = { .53,.44,.35,.31,.28,.25,.24,.22,.20,.19,.19 };
    private static readonly double[] A2_8_Area = { 2,4,6,10 };
    private static readonly double[] A2_8_Angle = { 8,10,14,20,30,45,60 };
    private static readonly double[,] A2_8 =
    {
        { .65,.68,.74,.82,.92,1.1,1.1 }, { .53,.60,.69,.78,.90,1,1.1 },
        { .50,.57,.66,.77,.91,1,1.1 }, { .45,.53,.64,.74,.85,.97,1.1 }
    };
    private static readonly double[] A2_9_Ld = { .5,1,2,3,4,5,6,8,10,12,14 };
    private static readonly double[] A2_9 = { .49,.40,.30,.26,.23,.21,.19,.17,.16,.15,.14 };
    private static readonly double[] A2_10_Radius = { 0,.75,1,1.5,2.5 };
    private static readonly double[] A2_10_Length = { 0,.5,1,1.5,2,3,4,6,8,12 };
    private static readonly double[,] A2_10 =
    {
        { 3,3.1,3.2,3,2.7,2.4,2.2,2.1,2.1,2 }, { 2.2,2.2,2.1,1.8,1.7,1.6,1.6,1.5,1.5,1.5 },
        { 1.8,1.5,1.4,1.4,1.3,1.3,1.2,1.2,1.2,1.2 }, { 1.5,1.2,1.1,1.1,1.1,1.1,1.1,1.1,1.1,1.1 },
        { 1.2,1.1,1.1,1,1,1,1,1,1,1 }
    };
    private static readonly double[] A2_12_Dd = { .3,.4,.5,.6,.7,.8,.9,1 };
    private static readonly double[] A2_12 = { 130,41,17,8.1,4.4,2.6,1.6,1 };

    private static readonly double[] A3_7_Aspect = { .25,.5,1,1.5,2,3,4,5,6,7,8 };
    private static readonly double[] A3_7_OneRadius = { .05,.10,.15,.20,.25,.30,.35,.40,.45,.50 };
    private static readonly double[,] A3_7_One =
    {
        { .52,.40,.43,.49,.55,.66,.75,.84,.93,1,1.1 }, { .36,.27,.25,.24,.30,.35,.39,.42,.46,.49,.52 },
        { .28,.21,.18,.19,.20,.22,.25,.26,.28,.30,.32 }, { .22,.16,.14,.14,.15,.16,.17,.18,.19,.20,.21 },
        { .18,.13,.11,.11,.11,.12,.13,.14,.14,.15,.15 }, { .15,.11,.09,.09,.09,.09,.10,.10,.11,.11,.12 },
        { .13,.09,.08,.07,.07,.08,.08,.08,.08,.09,.09 }, { .11,.08,.07,.06,.06,.06,.06,.07,.07,.07,.07 },
        { .10,.07,.06,.05,.05,.05,.05,.05,.06,.06,.06 }, { .09,.06,.05,.05,.04,.04,.04,.05,.05,.05,.05 }
    };
    private static readonly double[] A3_7_TwoRadius = { .05,.10,.15,.20,.25,.30 };
    private static readonly double[,] A3_7_Two =
    {
        { .26,.20,.22,.25,.28,.33,.37,.41,.45,.48,.51 }, { .17,.13,.11,.12,.13,.15,.16,.17,.19,.20,.21 },
        { .12,.09,.08,.08,.08,.09,.10,.10,.11,.11,.11 }, { .09,.07,.06,.05,.06,.06,.06,.06,.07,.07,.07 },
        { .08,.05,.04,.04,.04,.04,.05,.05,.05,.05,.05 }, { .06,.04,.03,.03,.03,.03,.03,.03,.04,.04,.04 }
    };
    private static readonly double[] A3_7_ThreeRadius = { .05,.10 };
    private static readonly double[,] A3_7_Three =
    {
        { .11,.10,.12,.13,.14,.16,.18,.19,.21,.22,.23 },
        { .07,.05,.06,.06,.06,.07,.07,.08,.08,.08,.09 }
    };
    private static readonly double[] A3_9_Velocity = { 1_000,2_000,3_000,4_000 };
    private static readonly double[,] A3_9 =
    {
        { .27,.22,.19,.17 }, { .33,.29,.26,.23 }, { .38,.31,.27,.24 }, { .26,.21,.18,.16 }
    };
    private static readonly double[] A3_Aspect = { .25,.5,.75,1,1.5,2,3,4,6,8 };
    private static readonly double[] A3_AspectFactor = { 1.10,1.07,1.04,1,.95,.90,.83,.78,.72,.70 };
    private static readonly double[] A3_11_Length = { 0,.4,.6,.8,1,1.2,1.4,1.6,1.8,2,2.4,2.8,3.2,4,5,6,7,9,10 };
    private static readonly double[] A3_11 = { 0,.62,.90,1.6,2.6,3.6,4,4.2,4.2,4.2,3.7,3.3,3.2,3.1,2.9,2.9,2.8,2.7,2.5 };
    private static readonly double[] A3_12_Length = { 0,.4,.6,.8,1,1.2,1.4,1.6,1.8,2,2.4,2.8,3.2,4,5,6,7,9,10 };
    private static readonly double[] A3_12 = { 1.2,2.4,2.9,3.3,3.4,3.4,3.4,3.3,3.2,3.1,3.2,3.2,3.2,3,2.9,2.8,2.7,2.5,2.4 };
    private static readonly double[] A3_13_Angle = { 15,30,45,60,75,90 };
    private static readonly double[] A3_13_Length = { 0,1,2,3,4,6,8,10,12,14,16,18,20,25,40 };
    private static readonly double[,] A3_13 =
    {
        { .20,.42,.60,.78,.94,1.16,1.20,1.15,1.08,1.05,1.02,1,1.10,1.25,2 },
        { .40,.65,.88,1.16,1.20,1.18,1.12,1.06,1.06,1.15,1.28,1.40,1.50,1.70,2 },
        { .60,1.06,1.20,1.23,1.20,1.08,1.03,1.08,1.17,1.30,1.42,1.55,1.65,1.80,2 },
        { 1.05,1.38,1.37,1.28,1.15,1.06,1.16,1.30,1.42,1.54,1.66,1.76,1.85,1.95,2 },
        { 1.50,1.58,1.46,1.30,1.27,1.30,1.37,1.47,1.57,1.68,1.75,1.80,1.88,1.97,2 },
        { 1.70,1.67,1.40,1.37,1.38,1.47,1.55,1.63,1.70,1.76,1.82,1.88,1.92,1.98,2 }
    };
    private static readonly double[] A3_14_Angle = { 60,90 };
    private static readonly double[] A3_14_Length = { 0,1,2,3,4,6,8,10,12,14,20,25,40 };
    private static readonly double[,] A3_14 =
    {
        { 2,1.90,1.50,1.35,1.30,1.20,1.25,1.50,1.63,1.73,1.85,1.95,2 },
        { 2,1.80,1.60,1.55,1.55,1.65,1.80,1.90,1.93,1.98,2,2,2 }
    };
    private static readonly double[] A3_15_Velocity = { 800,1_200,1_600,2_000,2_400 };
    private static readonly double[] A3_15 = { .18,.22,.24,.25,.26 };

    private static readonly double[] A4_Area = { .06,.1,.25,.5,1,2,4,6,10 };
    private static readonly double[] A4_Angle = { 10,15,20,30,45,60,90,120,150,180 };
    private static readonly double[,] A4_1 =
    {
        { .26,.27,.40,.56,.71,.86,1,.99,.98,.98 }, { .24,.26,.36,.53,.69,.82,.93,.93,.92,.91 },
        { .17,.19,.22,.42,.60,.68,.70,.69,.67,.66 }, { .14,.13,.15,.24,.35,.37,.38,.37,.36,.35 },
        { 0,0,0,0,0,0,0,0,0,0 }, { .23,.20,.20,.20,.24,.28,.54,.78,1,1.1 },
        { .81,.64,.64,.64,.88,1.1,2.8,4.4,5.7,6.6 }, { 1.8,1.4,1.4,1.4,2,2.5,6.6,10,13,15 },
        { 5,5,5,5,6.5,8,19,29,37,43 }
    };
    private static readonly double[,] A4_2 =
    {
        { .21,.29,.38,.60,.84,.88,.88,.88,.88,.88 }, { .21,.28,.38,.59,.76,.80,.83,.84,.83,.83 },
        { .16,.22,.30,.46,.61,.68,.64,.63,.62,.62 }, { .11,.13,.19,.32,.33,.33,.32,.31,.30,.30 },
        { 0,0,0,0,0,0,0,0,0,0 }, { .20,.20,.20,.20,.22,.24,.48,.72,.96,1 },
        { .80,.64,.64,.64,.88,1.1,2.7,4.3,5.6,6.6 }, { 1.8,1.4,1.4,1.4,2,2.5,6.5,10,13,15 },
        { 5,5,5,5,6.5,8,19,29,37,43 }
    };
    private static readonly double[] A4_3_Angle = { 10,15,20,30,45,60,90 };
    private static readonly double[,] A4_3 =
    {
        { .26,.27,.40,.56,.71,.86,1 }, { .24,.26,.36,.53,.69,.82,.93 },
        { .17,.19,.22,.42,.60,.68,.70 }, { .14,.13,.15,.24,.35,.37,.38 },
        { 0,0,0,0,0,0,0 }, { .23,.20,.20,.20,.24,.28,.54 },
        { .81,.64,.64,.64,.88,1.1,2.8 }, { 1.8,1.4,1.4,1.4,2,2.5,6.6 },
        { 5,5,5,5,6.5,8,19 }
    };
    private static readonly double[,] A4_4 =
    {
        { .26,.30,.44,.54,.53,.65,.77,.88,.95,.98 }, { .24,.30,.43,.50,.53,.64,.75,.84,.89,.91 },
        { .20,.25,.34,.36,.45,.52,.58,.62,.64,.64 }, { .14,.15,.20,.21,.25,.30,.33,.33,.33,.32 },
        { 0,0,0,0,0,0,0,0,0,0 }, { .23,.22,.21,.20,.22,.20,.49,.74,.99,1.1 },
        { .84,.68,.68,.64,.88,1.1,2.7,4.3,5.6,6.6 }, { 1.8,1.5,1.5,1.4,2,2.5,6.5,10,13,15 },
        { 5,5,5.1,5,6.5,8,19,29,37,43 }
    };
    private static readonly double[] A4_5_Area = { 2,4,6,10 };
    private static readonly double[] A4_5_Angle = { 8,10,14,20,30,45,60,90,180 };
    private static readonly double[,] A4_5 =
    {
        { .14,.15,.20,.25,.30,.33,.33,.33,.30 }, { .20,.25,.34,.45,.52,.58,.62,.64,.64 },
        { .21,.30,.42,.53,.63,.72,.78,.79,.79 }, { .24,.30,.43,.53,.64,.75,.84,.89,.88 }
    };
    private static readonly double[] A4_6_Re = { 10_000,20_000,50_000,100_000,200_000,500_000 };
    private static readonly double[] A4_6_LargeLength = { 1,1.5,2,3,4,5 };
    private static readonly double[] A4_6_LargeB = { .1,.5,1,2,5,10,20,50 };
    private static readonly double[,,] A4_6_Large =
    {
        {
            { .46,.48,.50,.55,.70,.94,1.4,2.9 }, { .46,.47,.49,.53,.64,.82,1.2,2.3 },
            { .46,.47,.48,.50,.58,.71,.98,1.8 }, { .45,.46,.46,.48,.51,.57,.69,1 },
            { .45,.46,.46,.46,.48,.51,.56,.73 }, { .45,.46,.46,.47,.49,.53,.61,.84 }
        },
        {
            { .41,.43,.46,.51,.66,.90,1.4,2.9 }, { .41,.43,.45,.48,.59,.78,1.1,2.2 },
            { .41,.42,.44,.46,.54,.67,.93,1.7 }, { .41,.42,.42,.43,.47,.53,.64,.99 },
            { .41,.41,.41,.42,.44,.46,.52,.69 }, { .41,.41,.42,.43,.45,.49,.57,.80 }
        },
        {
            { .31,.33,.35,.40,.55,.79,1.3,2.8 }, { .31,.32,.34,.38,.49,.67,1,2.1 },
            { .31,.32,.33,.36,.43,.57,.83,1.6 }, { .30,.31,.31,.33,.36,.42,.54,.88 },
            { .30,.30,.31,.31,.33,.36,.41,.58 }, { .30,.31,.31,.32,.34,.38,.46,.70 }
        },
        {
            { .19,.21,.23,.28,.43,.68,1.2,2.6 }, { .19,.20,.22,.26,.37,.55,.92,2 },
            { .19,.20,.21,.24,.31,.45,.71,1.5 }, { .19,.19,.20,.21,.24,.30,.42,.77 },
            { .18,.19,.19,.19,.21,.24,.29,.46 }, { .18,.19,.19,.20,.22,.26,.34,.58 }
        },
        {
            { .07,.09,.12,.17,.31,.56,1.1,2.5 }, { .07,.09,.10,.14,.25,.43,.80,.9 },
            { .07,.08,.09,.12,.20,.33,.59,1.4 }, { .07,.07,.08,.09,.13,.18,.30,.65 },
            { .07,.07,.07,.08,.10,.12,.18,.34 }, { .07,.07,.08,.08,.11,.15,.22,.46 }
        },
        {
            { .01,.03,.05,.10,.25,.30,.99,2.5 }, { .01,.02,.04,.08,.19,.37,.74,1.8 },
            { .01,.02,.03,.06,.13,.27,.53,1.3 }, { 0,.01,.02,.03,.06,.12,.24,.58 },
            { 0,.01,.01,.01,.03,.06,.11,.28 }, { 0,.01,.01,.02,.04,.08,.16,.40 }
        }
    };
    private static readonly double[] A4_6_SmallLength = { 1,3,4,5 };
    private static readonly double[] A4_6_SmallB = { .1,1,5,10,20,50 };
    private static readonly double[,,] A4_6_Small =
    {
        {
            { .27,.27,.28,.29,.31,.37 }, { .27,.27,.28,.29,.30,.33 },
            { .27,.27,.28,.28,.29,.32 }, { .27,.27,.27,.27,.27,.27 }
        },
        {
            { .25,.25,.26,.27,.29,.35 }, { .25,.25,.25,.26,.28,.33 },
            { .25,.25,.25,.26,.27,.30 }, { .25,.25,.25,.25,.25,.25 }
        },
        {
            { .18,.18,.19,.20,.22,.28 }, { .18,.18,.19,.20,.22,.27 },
            { .18,.18,.19,.19,.20,.23 }, { .18,.18,.18,.18,.18,.18 }
        },
        {
            { .11,.11,.12,.13,.15,.21 }, { .11,.11,.12,.13,.14,.19 },
            { .11,.11,.12,.12,.13,.16 }, { .11,.11,.11,.11,.11,.11 }
        },
        {
            { .04,.04,.05,.06,.08,.14 }, { .04,.04,.05,.06,.07,.12 },
            { .04,.04,.05,.05,.06,.09 }, { .04,.04,.04,.04,.04,.04 }
        },
        {
            { 0,0,.01,.02,.04,.10 }, { 0,0,.01,.02,.04,.09 },
            { 0,0,.01,.01,.02,.05 }, { 0,0,0,0,0,0 }
        }
    };
    private static readonly double[] A4_7_Area = { .1,.25,.5,.8 };
    private static readonly double[] A4_7_Length = { .025,.05,.075,.1,.15,.3,.6 };
    private static readonly double[] A4_7_Angle = { 0,10,20,30,45,60,90,120,150,180 };
    private static readonly double[,,] A4_7 =
    {
        {
            { .46,.43,.42,.40,.38,.37,.38,.40,.43,.46 }, { .46,.42,.38,.33,.30,.28,.31,.36,.41,.46 },
            { .46,.39,.32,.28,.23,.21,.26,.32,.39,.46 }, { .46,.36,.30,.23,.19,.17,.23,.30,.38,.46 },
            { .46,.34,.25,.18,.15,.14,.21,.29,.37,.46 }, { .46,.31,.22,.16,.13,.13,.20,.28,.37,.46 },
            { .46,.25,.17,.12,.10,.11,.19,.27,.36,.46 }
        },
        {
            { .40,.38,.36,.35,.33,.32,.33,.35,.37,.40 }, { .40,.36,.33,.29,.26,.24,.27,.31,.35,.40 },
            { .40,.34,.28,.24,.20,.19,.23,.28,.34,.40 }, { .40,.31,.26,.20,.17,.14,.20,.26,.33,.40 },
            { .40,.30,.22,.16,.13,.12,.18,.25,.32,.40 }, { .40,.27,.19,.14,.11,.11,.18,.25,.32,.40 },
            { .40,.22,.14,.10,.09,.10,.16,.24,.32,.40 }
        },
        {
            { .30,.28,.27,.25,.24,.24,.25,.26,.27,.30 }, { .30,.27,.24,.21,.19,.18,.20,.23,.26,.30 },
            { .30,.25,.21,.18,.15,.14,.17,.21,.25,.30 }, { .30,.23,.19,.15,.12,.11,.15,.19,.24,.30 },
            { .30,.22,.16,.12,.09,.09,.13,.18,.24,.30 }, { .30,.20,.14,.10,.08,.08,.13,.18,.24,.30 },
            { .30,.16,.11,.08,.07,.07,.12,.17,.23,.30 }
        },
        {
            { .15,.14,.13,.13,.12,.12,.12,.13,.14,.15 }, { .15,.13,.12,.11,.10,.09,.10,.12,.13,.15 },
            { .15,.13,.10,.09,.08,.07,.08,.10,.13,.15 }, { .15,.12,.10,.07,.06,.05,.07,.10,.12,.15 },
            { .15,.11,.08,.06,.05,.04,.07,.09,.12,.15 }, { .15,.10,.07,.05,.04,.04,.07,.09,.12,.15 },
            { .15,.08,.05,.04,.03,.04,.06,.09,.12,.15 }
        }
    };

    private static readonly double[] JunctionFlowFraction = { 0,.1,.2,.3,.4,.5,.6,.7,.8,.9,1 };
    private static readonly double[] JunctionAreaRatio = { .1,.2,.3,.4,.6,.8,1 };
    private static readonly double[,] A5_1_Branch =
    {
        { -1,-1,-1,-.9,-.9,-.9,-.9 }, { .21,-.46,-.57,-.51,.53,-.54,-.54 },
        { 3.1,.37,-.06,-.16,.23,-.24,-.28 }, { 7.6,1.5,.50,.15,.04,-.06,-.08 },
        { 14,3,1.2,.42,.19,.13,.12 }, { 21,4.6,1.8,.53,.24,.19,.15 },
        { 30,6.4,2.6,.77,.35,.25,.17 }, { 41,8.5,3.4,.99,.42,.28,.22 },
        { 54,12,4.2,1.2,.47,.29,.25 }, { 58,14,5.3,1.4,.49,.29,.22 },
        { 84,17,6.3,1.6,.49,.21,.15 }
    };
    private static readonly double[,] A5_1_Main =
    {
        { 0,0,0,0,0,0,0 }, { .02,.11,.13,.15,.16,.17,.17 },
        { -.33,.01,.13,.19,.24,.27,.29 }, { -1.1,-.25,-.01,.10,.22,.30,.35 },
        { -2.2,-.75,-.30,-.05,.17,.26,.36 }, { -3.6,-1.4,-.70,-.35,0,.21,.32 },
        { -5.4,-2.4,-1.3,-.70,-.20,.06,.25 }, { -7.6,-3.4,-2,-1.2,-.50,-.15,.10 },
        { -10,-4.6,-2.7,-1.8,-.90,-.43,-.15 }, { -13,-6.2,-3.7,-2.6,-1.4,-.80,-.45 },
        { -16,-7.7,-4.8,-3.4,-1.9,-1.2,-.75 }
    };
    private static readonly double[,] A5_2_Branch =
    {
        { -1,-1,-1,-.9,-.9,-.8,-.9 }, { .24,-.45,-.56,-.50,-.52,-.53,-.53 },
        { 3.2,.54,-.02,-.14,-.21,-.23,-.23 }, { 8,1.6,.60,.23,.06,0,-.02 },
        { 14,3.2,1.3,.52,.25,.18,.15 }, { 22,5,2.1,.65,.33,.25,.22 },
        { 32,7,3,.91,.81,.61,.51 }, { 43,9.2,3.9,1.2,.56,.39,.33 },
        { 56,12,4.9,1.5,.66,.39,.36 }, { 71,15,6.2,1.8,.72,.44,.35 },
        { 87,19,7.4,2,.78,.44,.32 }
    };
    private static readonly double[,] A5_2_Main =
    {
        { 0,0,0,0,0,0,0 }, { .05,.12,.14,.16,.17,.17,.17 },
        { -.20,.17,.22,.27,.27,.29,.31 }, { -.76,-.13,.08,.20,.28,.32,.40 },
        { -1.7,-.50,-.12,.08,.26,.36,.41 }, { -2.8,-1,-.49,-.13,.16,.30,.40 },
        { -4.3,-1.7,-.87,-.45,-.04,.20,.33 }, { -6.1,-2.6,-1.4,-.85,-.25,.08,.25 },
        { -8.1,3.6,-2.1,-1.3,-.55,-.17,.06 }, { -10,4.8,-2.8,-1.9,-.88,-.40,-.18 },
        { -13,-6.1,-3.7,-2.6,-1.4,-.77,-.42 }
    };
    private static readonly double[,] A5_3_Branch =
    {
        { -1,-1,-1,-.9,-.9,-.9,-.9 }, { .40,-.37,-.51,-.46,-.50,-.51,-.52 },
        { 3.8,.72,.17,-.02,-.14,-.18,-.24 }, { 9.2,2.3,1,.44,.21,.11,-.08 },
        { 16,4.3,2.1,.94,.54,.40,.32 }, { 26,6.8,3.2,1.1,.66,.49,.42 },
        { 37,9.7,4.7,1.6,.92,.69,.57 }, { 43,13,6.3,2.1,1.2,.88,.72 },
        { 65,17,7.9,2.7,1.5,1.1,.86 }, { 82,21,9.7,3.4,1.8,1.2,.99 },
        { 101,26,12,4,2.1,1.4,1.1 }
    };
    private static readonly double[] A5_3_Main = { 0,.16,.27,.38,.46,.53,.57,.59,.60,.59,.55 };

    private static readonly double[] A5_6_Flow = { .1,.2,.3,.4,.5,.6,.7,.8,.9 };
    private static readonly double[,] A5_6_Branch =
    {
        { -1.2,-.40,.40,1.6,3,4.8,6.8,8.9,11 }, { -.50,-.20,0,.25,.45,.70,1,1.5,2 },
        { -1,-.60,-.20,.10,.30,.60,1,1.5,2 }, { -2.2,-1.5,-.95,-.50,0,.40,.80,1.3,1.9 },
        { -.60,-.30,-.10,-.04,.13,.21,.29,.36,.42 }, { -1.2,-.80,-.40,-.20,0,.16,.24,.32,.38 },
        { -2.1,-1.4,-.90,-.50,-.20,0,.20,.25,.30 }
    };
    private static readonly double[,] A5_6_Main =
    {
        { .30,.30,.20,-.10,-.45,-.92,-1.5,-2,-2.6 }, { .17,.16,.10,0,-.08,-.18,-.27,-.37,-.46 },
        { .27,.35,.32,.25,.12,-.03,-.23,-.42,-.58 }, { 1.2,1.1,.90,.65,.35,0,-.40,-.80,-1.3 },
        { .18,.24,.27,.26,.23,.18,.10,0,-.12 }, { .75,.36,.38,.35,.27,.18,.05,-.08,-.22 },
        { .80,.87,.80,.68,.55,.40,.25,.08,-.10 }
    };
    private static readonly double[] A5_VelocityBandFlow = { .1,.2,.3,.4,.5,.6,.7,.8,.9,1 };
    private static readonly double[,] A5_7_Branch =
    {
        { -.63,-.55,.13,.23,.78,1.30,1.93,3.10,4.88,5.60 },
        { -.49,-.21,.23,.60,1.27,2.06,2.75,3.70,4.93,5.95 }
    };
    private static readonly double[,] A5_8_Branch =
    {
        { -.75,-.53,-.03,.33,1.03,1.10,2.15,2.93,4.18,4.78 },
        { -.69,-.21,.23,.67,1.17,1.66,2.67,3.36,3.93,5.13 }
    };
    private static readonly double[,] A5_9_Branch =
    {
        { -.83,-.68,-.30,.28,.55,1.03,1.50,1.93,2.50,3.03 },
        { -.72,-.52,-.23,.34,.76,1.14,1.83,2.01,2.90,3.63 }
    };

    private static readonly double[] JunctionVelocityRatio = { 0,.2,.4,.6,.8,1,1.2,1.4,1.6,1.8,2 };
    private static readonly double[] A5_10_Branch = { 1,.85,.74,.62,.52,.42,.36,.32,.32,.37,.52 };
    private static readonly double[] A5_11_Branch = { 1,.84,.61,.41,.27,.17,.12,.12,.14,.18,.27 };
    private static readonly double[] A5_12_Branch = { 1,1.03,1.08,1.18,1.33,1.56,1.86,2.2,2.6,3,3.4 };
    private static readonly double[] A5_13_Branch = { 1,1.32,1.51,1.60,1.65,1.74,1.87,2,2.2,2.5,2.7 };
    private static readonly double[] A5_14_Branch = { 1,.94,.88,.84,.80,.82,.84,.87,.90,.95,1.02 };
    private static readonly double[] A5_15_Branch = { 1,.88,.77,.68,.65,.69,.73,.88,1.14,1.54,2.2 };
    private static readonly double[] A5_16_Branch = { 1,.82,.63,.52,.45,.42,.41,.40,.41,.45,.56 };
    private static readonly double[] A5_17_BranchAxis = { .2,.4,.6,.7,.8,.9,1,1.1,1.2,1.4,1.6,1.8,2,2.2,2.4,2.6,2.8,3 };
    private static readonly double[] A5_17_Branch = { .76,.60,.52,.50,.51,.52,.56,.61,.68,.86,1.1,1.4,1.8,2.2,2.6,3.1,3.7,4.2 };
    private static readonly double[] A5_17_MainAxis = { .2,.4,.6,.8,1,1.2,1.4,1.6,1.8,2 };
    private static readonly double[] A5_17_Main = { .14,.06,.05,.09,.18,.30,.46,.64,.84,1 };
    private static readonly double[] A5_18_Branch = { 1,1.06,1.15,1.29,1.45,1.65,1.89,2.2,2.5,2.9,3.3 };
    private static readonly double[] A5_19_Branch = { 1,.95,.90,.86,.81,.79,.79,.81,.86,.96,1.10 };
    private static readonly double[] A5_20_Branch = { 1,.84,.72,.62,.54,.50,.56,.71,.92,1.22,1.66 };
    private static readonly double[] A5_21_Branch = { 1,.93,.71,.55,.44,.42,.42,.44,.47,.54,.62 };

    private static readonly double[] A5_22_Flow = { .1,.2,.3,.4,.5,.6,.7,.8,.9 };
    private static readonly double[,] A5_22_Branch =
    {
        { .55,.50,.60,.85,1.2,1.8,3.1,4.4,6 }, { .35,.35,.50,.80,1.3,2,2.8,3.8,5 },
        { .62,.48,.40,.40,.48,.60,.78,1.1,1.5 }, { .52,.40,.32,.30,.34,.44,.62,.92,1.4 },
        { .44,.38,.38,.41,.52,.68,.92,1.2,1.6 }, { .67,.55,.46,.37,.32,.29,.29,.30,.37 },
        { .70,.60,.51,.42,.34,.28,.26,.26,.29 }, { .60,.52,.43,.33,.24,.17,.15,.17,.21 }
    };
    private static readonly double[,] A5_22_Main =
    {
        { -.01,-.03,-.01,.05,.13,.21,.29,.38,.46 }, { .08,0,-.02,-.01,.02,.08,.16,.24,.34 },
        { -.03,-.06,-.05,0,.06,.12,.19,.27,.35 }, { .04,-.02,-.04,-.03,-.01,.04,.12,.23,.37 },
        { .72,.48,.28,.13,.05,.04,.09,.18,.30 }, { -.02,-.04,-.04,-.01,.06,.13,.22,.30,.38 },
        { .10,0,.01,-.03,-.01,.03,.10,.20,.30 }, { .62,.38,.23,.23,.08,.05,.06,.10,.20 }
    };
    private static readonly double[] A5_23_MainAxis = { 0,.1,.2,.3,.4,.5,.6,.8,1 };
    private static readonly double[] A5_23_Main = { .40,.32,.26,.20,.14,.10,.06,.02,0 };
    private static readonly double[] A5_23_Area = { .2,.3,.4,.5,.6,.7,.8 };
    private static readonly double[] A5_23_Flow = { .1,.2,.3,.4,.5,.6,.7,.8,.9 };
    private static readonly double[,] A5_23_Branch30 =
    {
        { .40,.26,.58,1.3,2.5,4.1,6.1,8.6,11 }, { .55,.28,.24,.38,.76,1.3,2,3,4.1 },
        { .59,.33,.21,.20,.27,.40,.62,.92,1.3 }, { .65,.41,.26,.19,.18,.22,.32,.47,.67 },
        { .69,.46,.31,.21,.17,.16,.20,.28,.39 }, { .72,.51,.36,.25,.18,.15,.16,.20,.26 },
        { .75,.55,.40,.28,.21,.16,.15,.16,.19 }
    };
    private static readonly double[,] A5_23_Branch45 =
    {
        { .56,.56,1,1.8,3.2,4.9,7.1,9.6,13 }, { .66,.48,.52,.73,1.2,1.8,2.7,3.7,4.9 },
        { .66,.47,.40,.43,.54,.69,.95,1.3,1.7 }, { .71,.52,.41,.38,.40,.45,.59,.78,1 },
        { .74,.56,.44,.37,.35,.36,.43,.54,.68 }, { .71,.59,.47,.38,.34,.32,.35,.41,.50 },
        { .78,.62,.49,.40,.34,.31,.32,.35,.40 }
    };
    private static readonly double[] A5_23_LargeAngleArea = { .3,.4,.5,.6,.7,.8 };
    private static readonly double[,] A5_23_Branch60 =
    {
        { .80,.75,.89,1.2,1.8,2.6,3.5,4.6,6 }, { .76,.65,.65,.74,.89,1.1,1.4,1.8,2.3 },
        { .79,.66,.61,.62,.68,.76,.94,1.2,1.5 }, { .81,.68,.60,.58,.58,.61,.72,.87,1.1 },
        { .82,.69,.61,.56,.54,.54,.60,.70,.82 }, { .83,.71,.62,.56,.52,.50,.53,.60,.68 }
    };
    private static readonly double[,] A5_23_Branch90 =
    {
        { 1.1,1.4,1.8,2.3,3.2,4.3,5.5,6.9,8.5 }, { .99,1.1,1.3,1.5,1.7,2,2.4,3,3.6 },
        { .97,1,1.1,1.2,1.4,1.5,1.8,2.1,2.5 }, { .96,.97,1,1.1,1.1,1.2,1.4,1.7,2 },
        { .95,.94,.95,.98,1,1.1,1.2,1.4,1.6 }, { .95,.92,.92,.93,.94,.95,1.1,1.2,1.4 }
    };
    private static readonly double[] A5_24_Angle = { 15,30,45,60,90 };
    private static readonly double[] A5_24_Velocity = { .1,.2,.3,.4,.5,.6,.8,1,1.2,1.4,1.6,1.8,2 };
    private static readonly double[,] A5_24_Branch =
    {
        { .81,.65,.51,.38,.28,.20,.11,.06,.14,.30,.51,.76,1 },
        { .84,.69,.56,.44,.34,.26,.19,.15,.15,.30,.51,.76,1 },
        { .87,.74,.63,.54,.45,.38,.29,.24,.23,.30,.51,.76,1 },
        { .90,.82,.79,.66,.59,.53,.43,.36,.33,.39,.51,.76,1 },
        { 1,1,1,1,1,1,1,1,1,1,1,1,1 }
    };
    private static readonly double[] A5_24_MainVelocity = { 0,.1,.2,.3,.4,.5,.6,.8,1,1.2,1.4,1.6,1.8,2 };
    private static readonly double[] A5_24_MainMaximumVelocity = { 2,2,1.6,1.4,1.2 };
    private static readonly double[,] A5_24_Main =
    {
        { 1,1,1,1,1 }, { .81,.81,.81,.81,.81 }, { .64,.64,.64,.64,.64 },
        { .50,.52,.52,.50,.50 }, { .36,.40,.38,.37,.36 }, { .25,.30,.28,.27,.25 },
        { .16,.23,.20,.18,.16 }, { .04,.17,.10,.07,.04 }, { 0,.20,.10,.05,0 },
        { .07,.36,.21,.14,.07 }, { .39,.79,.59,.39,0 }, { .90,1.4,1.2,0,0 },
        { 1.8,2.4,0,0,0 }, { 3.2,4,0,0,0 }
    };
    private static readonly double[] A5_RaggedVelocity = { .2,.4,.6,.8,1,1.2,1.4,1.6,1.8 };
    private static readonly double[] A5_RaggedFlow = { .1,.2,.3,.4,.5,.6,.7,.8,.9 };
    private static readonly double[][] A5_25_Branch =
    {
        new[] { 1d }, new[] { 1.01,1.07 }, new[] { 1.14,1.10,1.08 },
        new[] { 1.18,1.31,1.12,1.13 }, new[] { 1.30,1.38,1.20,1.23,1.26 },
        new[] { 1.46,1.58,1.45,1.31,1.39,1.48 }, new[] { 1.70,1.82,1.65,1.51,1.56,1.64,1.71 },
        new[] { 1.93,2.06,2,1.85,1.70,1.76,1.80,1.88 }, new[] { 2.06,2.17,2.10,2.13,2.06,1.98,1.99,2,2.07 }
    };
    private static readonly double[][] A5_27_Branch =
    {
        new[] { .91 }, new[] { .81,.79 }, new[] { .77,.72,.70 }, new[] { .78,.73,.69,.66 },
        new[] { .78,.98,.85,.79,.74 }, new[] { .90,1.11,1.16,1.23,1.03,.86 },
        new[] { 1.19,1.22,1.26,1.29,1.54,.25,.92 }, new[] { 1.35,1.42,1.55,1.59,1.63,1.50,1.31,1.09 },
        new[] { 1.44,1.50,1.75,1.74,1.72,2.24,1.63,1.40,1.17 }
    };
    private static readonly double[][] A5_28_Branch =
    {
        new[] { 1.03 }, new[] { 1.04,1.01 }, new[] { 1.11,1.03,1.05 }, new[] { 1.16,1.21,1.17,1.12 },
        new[] { 1.38,1.40,1.30,1.36,1.27 }, new[] { 1.52,1.61,1.68,1.91,1.47,1.66 },
        new[] { 1.79,2.01,1.90,2.31,2.28,2.20,1.95 }, new[] { 2.07,2.28,2.13,2.71,2.99,2.81,2.09,2.20 },
        new[] { 2.32,2.54,2.64,3.09,3.72,2.48,2.21,2.57,2.32 }
    };
    private static readonly double[][] A5_29_Branch =
    {
        new[] { .61 }, new[] { .46,.61 }, new[] { .43,.50,.54 }, new[] { .39,.43,.62,.53 },
        new[] { .34,.57,.77,.73,.68 }, new[] { .37,.64,.85,.98,1.07,.83 },
        new[] { .57,.71,1.04,1.16,1.54,1.36,1.18 }, new[] { .89,1.08,1.28,1.30,1.69,2.09,1.81,1.47 },
        new[] { 1.33,1.34,2.04,1.78,1.90,2.40,2.77,2.23,1.92 }
    };
    private static readonly double[][] A5_30_Branch =
    {
        new[] { .58 }, new[] { .67,.64 }, new[] { .78,.76,.75 }, new[] { .88,.98,.81,1.01 },
        new[] { 1.12,1.05,1.08,1.18,1.29 }, new[] { 1.49,1.48,1.40,1.51,1.70,1.91 },
        new[] { 2.10,2.21,2.25,2.29,2.32,2.48,2.53 }, new[] { 2.72,3.30,2.84,3.09,3.30,3.19,3.29,3.16 },
        new[] { 3.42,4.58,3.65,3.92,4.20,4.15,4.14,4.10,4.05 }
    };
    private static readonly double[] A5_31_MainAxis = { .2,.4,.6,.8,1,1.2,1.4,1.6,1.8 };
    private static readonly double[] A5_31_Main = { .03,.04,.07,.12,.13,.14,.27,.30,.25 };
    private static readonly double[][] A5_31_Branch =
    {
        new[] { .60 }, new[] { .62,.69 }, new[] { .74,.80,.82 }, new[] { .99,1.10,.95,.90 },
        new[] { 1.48,1.12,1.41,1.24,1.21 }, new[] { 1.91,1.33,1.43,1.52,1.55,1.64 },
        new[] { 2.47,1.67,1.70,2.04,1.86,1.98,2.47 }, new[] { 3.17,2.40,2.33,2.53,2.31,2.51,3.13,3.25 },
        new[] { 3.85,3.37,2.89,3.23,3.09,3.03,3.30,3.74,4.11 }
    };
    private static readonly double[] A5_33_ConvergingAngle = { 15,30,45 };
    private static readonly double[] A5_33_ConvergingFlow = { 0,.1,.2,.3,.4,.5,.6,.7,.8,.9,1 };
    private static readonly double[,] A5_33_Converging =
    {
        { -2.6,-1.9,-1.3,-.77,-.30,.10,.41,.67,.85,.97,1 },
        { -2.1,-1.5,-1,-.53,-.10,.28,.69,.91,1.1,1.4,1.6 },
        { -1.3,-.93,-.55,-.16,.20,.56,.92,1.3,1.6,2,2.3 }
    };
    private static readonly double[] A5_34_AreaChoices = { .2,.4,.6,1 };
    private static readonly double[] A5_35_AreaChoices = { .2,.4,.6,.8,1 };
    private static readonly double[] A5_DoubleBranchFlowChoices = { .5,1,2 };
    private static readonly double[] A5_DoublePrimaryFlow = { 0,.1,.2,.3,.4,.5,.6 };
    private static readonly double[][][] A5_34_Branch =
    {
        new[]
        {
            new[] { -1d,-.36,.59,1.8,3.2,4.9,6.8 }, new[] { -1d,-.24,.63,1.7,2.6,3.7 },
            new[] { -1d,-.19,.21,.04 }
        },
        new[]
        {
            new[] { -1d,-.48,-.02,.58,.92,1.3,1.6 }, new[] { -1d,-.36,.17,.55,.72,.78 },
            new[] { -1d,-.18,.16,-.06 }
        },
        new[]
        {
            new[] { -1d,-.50,-.07,.31,.60,.82,.92 }, new[] { -1d,-.37,.12,.55,.60,.52 },
            new[] { -1d,-.18,.26,.16 }
        },
        new[]
        {
            new[] { -1d,-.51,-.09,.25,.50,.65,.64 }, new[] { -1d,-.37,.13,.46,.61,.54 },
            new[] { -1d,-.15,.38,.42 }
        }
    };
    private static readonly double[][][] A5_34_Main =
    {
        new[]
        {
            new[] { -2.9,-1.9,-1.3,-.80,-.56,-.23,-.01,.16,.22,.15,0 },
            new[] { -2.5,-1.9,-1.3,-.80,-.42,-.12,.08,.20,.22,.15,0 }
        },
        new[]
        {
            new[] { -.98,-.61,-.30,-.05,.14,.26,.33,.34,.28,.17,0 },
            new[] { -.77,-.44,-.16,.05,.21,.31,.36,.35,.29,.17,0 }
        },
        new[]
        {
            new[] { -.32,.08,.11,.27,.37,.43,.44,.40,.31,.18,0 },
            new[] { -.18,-.04,.21,.34,.42,.46,.46,.41,.31,.18,0 }
        },
        new[]
        {
            new[] { .11,.36,.46,.53,.57,.56,.52,.44,.33,.18,0 },
            new[] { .29,.42,.51,.57,.58,.58,.54,.45,.33,.18,0 }
        }
    };
    private static readonly double[][][] A5_35_Branch =
    {
        new[]
        {
            new[] { -.85,-.10,1.1,2.7,4.8,7.3,10d }, new[] { -.85,-.05,1.4,3.1,5.1,7.4 },
            new[] { -.85,-.31,1.8,3.4 }
        },
        new[]
        {
            new[] { -.85,-.29,.34,1,1.8,2.6,3.4 }, new[] { -.85,-.14,.60,1.3,2.1,2.7 },
            new[] { -.85,.12,1,1.7 }
        },
        new[]
        {
            new[] { -.85,-.32,.20,.72,1.2,1.7,2.1 }, new[] { -.85,-.18,.46,1,1.5,1.9 },
            new[] { -.85,.09,.88,1.4 }
        },
        new[]
        {
            new[] { -.85,-.33,.13,.61,1,1.4,1.7 }, new[] { -.85,-.18,.41,.91,1.3,1.5 },
            new[] { -.85,.08,.83,1.3 }
        },
        new[]
        {
            new[] { -.85,-.34,.13,.56,.93,1.3,1.5 }, new[] { -.85,-.19,.39,.86,1.2,1.4 },
            new[] { -.85,.07,.81,1.2 }
        }
    };
    private static readonly double[] A5_35_Main = { 1.2,1.2,1.2,1.1,1.1,.96,.85,.72,.56,.39,.20 };

    private static readonly double[] A7_FanArea = { .4,.5,.6,.7,.8,.9,1 };
    private static readonly double[] A7_FanPosition = { 0,1,2,3 };
    private static readonly double[] A7_FanLength = { 0,.12,.25,.5,1 };
    private static readonly double[,] A7_1 =
    {
        { 2,1,.40,.18,0 }, { 2,1,.40,.18,0 }, { 1,.66,.33,.14,0 },
        { .8,.40,.14,0,0 }, { .47,.22,.10,0,0 }, { .22,.14,0,0,0 }, { 0,0,0,0,0 }
    };
    private static readonly double[,,] A7_2 =
    {
        { { 3.2,2.7,1.8,.84,0 }, { 4,3.3,2.2,1,0 }, { 5.8,4.8,3.2,1.5,0 }, { 5.8,4.8,3.2,1.5,0 } },
        { { 2.3,1.9,1.3,.60,0 }, { 2.8,2.4,1.6,.72,0 }, { 4,3.3,2.2,1,0 }, { 4,3.3,2.2,1,0 } },
        { { 1.6,1.3,.88,.40,0 }, { 2,1.7,1.1,.52,0 }, { 2.9,2.4,1.6,.76,0 }, { 2.9,2.4,1.6,.76,0 } },
        { { 1.1,.88,.60,.28,0 }, { 1.3,1.1,.72,.36,0 }, { 2,1.6,1.1,.52,0 }, { 2,1.6,1.1,.52,0 } },
        { { .76,.64,.44,.20,0 }, { .96,.80,.52,.24,0 }, { 1.4,1.2,.76,.36,0 }, { 1.4,1.2,.76,.36,0 } },
        { { .60,.48,.32,.16,0 }, { .76,.64,.44,.20,0 }, { 1.1,.92,.64,.28,0 }, { 1.1,.92,.64,.28,0 } },
        { { .56,.48,.32,.16,0 }, { .68,.56,.36,.16,0 }, { 1,.84,.56,.26,0 }, { 1,.84,.56,.16,0 } }
    };
    private static readonly double[,,] A7_3 =
    {
        { { 3.2,2.7,1.8,.84,0 }, { 5,4.2,2.8,1.3,0 }, { 5.8,4.8,3.2,1.5,0 }, { 4.9,4.1,2.7,1.3,0 } },
        { { 2.3,1.9,1.3,.60,0 }, { 3.6,3,2,.90,0 }, { 4,3.3,2.2,1,0 }, { 3.4,2.8,1.9,.88,0 } },
        { { 1.6,1.3,.88,.40,0 }, { 2.5,2.1,1.4,.65,0 }, { 2.9,2.4,1.6,.76,0 }, { 2.5,2.1,1.4,.65,0 } },
        { { 1.1,.88,.60,.28,0 }, { 1.7,1.4,.90,.45,0 }, { 2,1.6,1.1,.52,0 }, { 1.7,1.4,.92,.44,0 } },
        { { .76,.64,.44,.20,0 }, { 1.2,1,.65,.30,0 }, { 1.4,1.2,.76,.36,0 }, { 1.2,.99,.65,.31,0 } },
        { { .60,.48,.32,.16,0 }, { .95,.80,.55,.25,0 }, { 1.1,.92,.64,.28,0 }, { .95,.78,.54,.24,0 } },
        { { .56,.48,.32,.16,0 }, { .85,.70,.45,.20,0 }, { 1,.84,.56,.28,0 }, { .85,.71,.48,.24,0 } }
    };
    private static readonly double[] A7_4_Radius = { .75,1,1.5,2,3 };
    private static readonly double[] A7_InletLength = { 0,2,5 };
    private static readonly double[,] A7_4 =
    {
        { 1.4,.80,.40 }, { 1.2,.66,.33 }, { 1.1,.60,.33 }, { 1,.53,.33 }, { .66,.40,.22 }
    };
    private static readonly double[] A7_5_Radius = { .5,.75,1,1.5,2,3 };
    private static readonly double[,] A7_5_Three =
    {
        { 2.5,1.6,.80 }, { 1.6,1,.47 }, { 1.2,.66,.33 },
        { 1.1,.60,.33 }, { 1,.53,.33 }, { .80,.47,.26 }
    };
    private static readonly double[,] A7_5_Four =
    {
        { 1.8,1,.53 }, { 1.4,.80,.40 }, { 1.2,.66,.33 },
        { 1.1,.60,.33 }, { 1,.53,.33 }, { .66,.40,.22 }
    };
    private static readonly double[] A7_SquareLength = { 0,2.5,6 };
    private static readonly double[] A7_6_NoVaneRadius = { .5,.75,1,1.5,2 };
    private static readonly double[,] A7_6_NoVane =
    {
        { 2.5,1.6,.80 }, { 2,1.2,.66 }, { 1.2,.66,.33 }, { 1,.57,.30 }, { .8,.47,.26 }
    };
    private static readonly double[] A7_6_VaneRadius = { .5,1,1.5,2 };
    private static readonly double[,] A7_6_FullVane =
    {
        { .80,.47,.26 }, { .53,.33,.18 }, { .40,.28,.16 }, { 1.26,.22,.14 }
    };
    private static readonly double[,] A7_6_ShortVane =
    {
        { .80,.47,.26 }, { .53,.33,.18 }, { .40,.28,.16 }, { .26,.22,.14 }
    };
    private static readonly double[] A7_7_Length = { .2,.3,.4,.5,.75 };
    private static readonly double[] A7_7 = { 1.2,.80,.53,.40,.22 };
    private static readonly double[] A7_8_Area = { .4,.5,.6,.7,.8,.9,1 };
    private static readonly double[] A7_8 = { 2,2,1,.80,.47,.22,0 };

    private static readonly double[] CommonReAxis = { 10_000,20_000,30_000,40_000,60_000,80_000,100_000,140_000,2_000_000 };
    private static readonly double[] CommonReValues = { 1.40,1.26,1.19,1.14,1.09,1.06,1.04,1,1 };
    private static readonly double[] SmoothElbowReAxis = { 10_000,20_000,30_000,40_000,60_000,80_000,100_000,140_000,200_000,2_000_000 };
    private static readonly double[,] SmoothElbowReValues =
    {
        { 1.4,1.26,1.19,1.14,1.09,1.06,1.04,1,1,1 },
        { 2,1.77,1.64,1.56,1.46,1.38,1.30,1.15,1,1 }
    };

    private static readonly double[,] A6_1 =
    {
        { .19,.27,.37,.49,.61,.74,.86 }, { .19,.32,.48,.69,.94,1.2,1.5 },
        { .19,.37,.64,1.0,1.5,2.1,2.8 }, { .19,.45,.87,1.6,2.6,4.1,6.1 },
        { .19,.54,1.2,2.5,5.0,9.6,17 }, { .19,.67,1.8,4.4,11,32,113 }
    };
    private static readonly double[] A6_2_Angle = { 0,10,20,30,40,50,60,65,70 };
    private static readonly double[] A6_2_Short = { .04,.30,1.1,3.0,8,23,60,100,190 };
    private static readonly double[] A6_2_Medium = { .08,.33,1.2,3.3,9,26,70,128,210 };
    private static readonly double[] A6_2_Tall = { .13,.35,1.3,3.6,10,29,80,155,230 };

    private static readonly double[] ScreenAreaRatio = { .2,.3,.4,.6,.8,1,1.2,1.4,1.6,2,2.5,3,4,6 };
    private static readonly double[] ScreenFreeRatio = { .3,.4,.5,.6,.7,.8,.9,1 };
    private static readonly double[,] A6_7 =
    {
        { 155,75,42,24,15,8,3.5,0 }, { 69,33,19,11,6.4,3.6,1.6,0 },
        { 39,19,10,6.1,3.6,2,.88,0 }, { 17,8.3,4.7,2.7,1.6,.89,.39,0 },
        { 9.7,4.7,2.7,1.5,.91,.50,.22,0 }, { 6.2,3,1.7,.97,.58,.32,.14,0 },
        { 4.3,2.1,1.2,.67,.40,.22,.10,0 }, { 3.2,1.5,.87,.49,.30,.16,.07,0 },
        { 2.4,1.2,.66,.38,.23,.12,.05,0 }, { 1.6,.75,.43,.24,.15,.08,.04,0 },
        { .99,.48,.27,.16,.09,.05,.02,0 }, { .69,.33,.19,.11,.06,.04,.02,0 },
        { .39,.19,.11,.06,.04,.02,.01,0 }, { .17,.08,.05,.03,.02,.01,0,0 }
    };

    private static readonly double[] CylinderRe = { .1,.5,1,5,10,50,100,500,200_000,300_000,400_000,500_000,600_000,1_000_000 };
    private static readonly double[] CylinderBlocked = { .05,.10,.15,.20 };
    private static readonly double[,] CylinderCoefficient =
    {
        { 3.9,8.4,14,19 }, { 1.5,3.2,5.2,7.1 }, { .66,1.4,2.3,3.2 },
        { .30,.64,1.1,1.4 }, { .17,.38,.62,.84 }, { .11,.24,.38,.52 },
        { .10,.21,.35,.47 }, { .07,.15,.24,.33 }, { .07,.15,.24,.33 },
        { .07,.16,.26,.35 }, { .05,.11,.19,.25 }, { .04,.09,.14,.19 },
        { .02,.05,.07,.10 }, { .02,.05,.07,.10 }
    };
    private static readonly double[] CylinderOffset = { 0,.05,.10,.15,.20,.25,.30,.35,.40 };
    private static readonly double[] CylinderOffsetFactor = { 1,.97,.93,.89,.84,.79,.74,.67,.58 };

    private static readonly double[] FanAngle = { 10,15,20,25,30,35 };
    private static readonly double[] FanAngle30 = { 10,15,20,25,30 };
    private static readonly double[] FanAreaRatio = { 1.5,2,2.5,3,3.5,4 };
    private static readonly double[,] A7_11 =
    {
        { .05,.07,.09,.10,.11,.11 }, { .06,.09,.11,.13,.13,.14 },
        { .07,.10,.13,.15,.16,.16 }, { .08,.13,.16,.19,.21,.23 },
        { .16,.29,.39,.32,.34,.35 }, { .24,.34,.39,.44,.48,.50 }
    };
    private static readonly double[,] A7_12 =
    {
        { .08,.09,.10,.10,.11,.11 }, { .10,.11,.12,.13,.14,.15 },
        { .12,.14,.15,.16,.17,.18 }, { .15,.18,.21,.23,.25,.26 },
        { .18,.25,.30,.33,.35,.35 }, { .21,.31,.38,.41,.43,.44 }
    };
    private static readonly double[,] A7_13 =
    {
        { .05,.08,.11,.13,.13,.14 }, { .06,.10,.12,.14,.15,.15 },
        { .07,.11,.14,.15,.16,.16 }, { .09,.14,.18,.20,.21,.22 },
        { .13,.18,.23,.26,.28,.29 }, { .15,.23,.28,.33,.35,.36 }
    };
    private static readonly double[,] A7_14 =
    {
        { .11,.13,.14,.14,.14,.14 }, { .13,.15,.16,.17,.18,.18 },
        { .19,.22,.24,.26,.28,.30 }, { .29,.32,.35,.37,.39,.40 },
        { .36,.42,.46,.49,.51,.51 }, { .44,.54,.61,.64,.66,.66 }
    };
    private static readonly double[,] A7_15 =
    {
        { .10,.18,.21,.23,.24,.25 }, { .23,.33,.38,.40,.42,.44 },
        { .31,.43,.48,.53,.56,.58 }, { .36,.49,.55,.58,.62,.64 },
        { .42,.53,.59,.64,.67,.69 }
    };

    private static readonly double[] RoundElbowRd = { .5, .75, 1, 1.5, 2, 2.5 };
    private static readonly double[] RoundElbowC = { .71, .33, .22, .15, .13, .12 };
    private static readonly double[] AngleAxis = { 0, 20, 30, 45, 60, 75, 90, 110, 130, 150, 180 };
    private static readonly double[] AngleFactor = { 0, .31, .45, .60, .78, .90, 1, 1.13, 1.20, 1.28, 1.40 };
    private static readonly double[] A7D_Angles = { 20, 30, 45, 60, 75, 90 };
    private static readonly double[] A7D_Hw = { .25, .5, .75, 1, 1.5, 2, 4, 6, 8 };
    private static readonly double[,] A7D =
    {
        { .08,.08,.08,.07,.07,.07,.06,.05,.05 }, { .18,.17,.17,.16,.15,.15,.13,.12,.11 },
        { .38,.37,.36,.34,.33,.31,.27,.25,.24 }, { .60,.59,.57,.55,.52,.49,.43,.39,.38 },
        { .89,.87,.84,.81,.77,.73,.63,.58,.57 }, { 1.3,1.3,1.2,1.2,1.1,1.1,.92,.85,.83 }
    };
    private static readonly double[,] A7E =
    {
        { 1.8,1.4,1.1,1.1,1.1,1.1 }, { 1.7,1.1,1.0,.95,.90,.84 },
        { 1.5,1.0,.81,.76,.72,.66 }, { 1.5,1.0,.69,.63,.60,.55 }
    };
    private static readonly double[,] A7F =
    {
        { 1.3,1.3,1.2,1.2,1.1,1.0,1.0,1.1,1.1,1.2,1.2 },
        { .57,.52,.48,.44,.40,.39,.39,.40,.42,.43,.44 },
        { .27,.25,.23,.21,.19,.18,.18,.19,.20,.21,.21 },
        { .22,.20,.19,.17,.15,.14,.14,.15,.16,.17,.17 },
        { .20,.18,.16,.15,.14,.13,.13,.14,.14,.15,.15 }
    };
    private static readonly double[,] A9A =
    {
        { .05,.05,.06,.12,.18,.24,.26 }, { .05,.04,.07,.17,.27,.35,.41 },
        { .05,.04,.07,.18,.28,.36,.42 }, { .05,.05,.08,.19,.29,.37,.43 }
    };
    private static readonly double[,] A8B =
    {
        { .18,.22,.25,.29,.31,.32,.33,.30 }, { .36,.43,.50,.56,.61,.63,.63,.63 },
        { .42,.47,.58,.68,.72,.76,.76,.75 }, { .42,.49,.59,.70,.80,.87,.85,.86 }
    };
    private static readonly double[,] A9B =
    {
        { .60,.50,.45,.43,.41,.40,.42,.45,.50 }, { .50,.47,.41,.36,.33,.30,.35,.42,.50 },
        { .50,.45,.35,.30,.26,.23,.30,.40,.50 }, { .50,.42,.32,.25,.22,.18,.27,.38,.50 },
        { .50,.39,.27,.20,.16,.15,.25,.37,.50 }, { .50,.37,.18,.13,.11,.12,.23,.36,.50 }
    };
    private static readonly double[,] A12A =
    {
        { .50,.57,.68,.80,.92,1,1 }, { .50,.51,.52,.55,.66,.72,.72 }, { .50,.50,.50,.50,.50,.50,.50 }
    };
    private static readonly double[] BellmouthRd = { 0,.01,.02,.03,.04,.05,.06,.08,.10,.12,.16,.20 };
    private static readonly double[] A12B = { 1,.87,.74,.61,.51,.40,.32,.20,.15,.10,.06,.03 };
    private static readonly double[] A12C = { .50,.43,.36,.31,.26,.22,.20,.15,.12,.09,.06,.03 };
    private static readonly double[] EntryAngle = { 0,10,20,30,40,60,100,140,180 };
    private static readonly double[,] A12D =
    {
        { 1,.96,.93,.90,.86,.80,.69,.59,.50 }, { 1,.93,.86,.80,.75,.67,.58,.53,.50 },
        { 1,.80,.67,.55,.48,.41,.41,.44,.50 }, { 1,.68,.45,.30,.22,.17,.22,.34,.50 },
        { 1,.46,.27,.18,.14,.13,.21,.33,.50 }
    };
    private static readonly double[,] A12E =
    {
        { .60,.50,.45,.43,.41,.40,.42,.45,.50 }, { .50,.47,.41,.36,.33,.30,.35,.42,.50 },
        { .50,.45,.35,.30,.26,.23,.30,.40,.50 }, { .50,.42,.32,.25,.22,.18,.27,.38,.50 },
        { .50,.39,.27,.20,.16,.15,.25,.37,.50 }, { .50,.37,.18,.13,.11,.12,.23,.36,.50 }
    };
    private static readonly double[,] A12F = { { 2.6,1.8,1.5,1.4,1.3,1.2,1.2,1.1,1.1 }, { 1.3,.77,.60,.48,.41,.30,.29,.28,.25 } };
    private static readonly double[,] A12G = { { 1,.11,.06,.09,.14,.18,.27,.32,.43,.50 }, { 1,.19,.13,.16,.21,.27,.33,.43,.53,.62 } };
    private static readonly double[,] A13A = { { 4,2.3,1.9,1.6,1.4,1.3,1.2,1.1,1,1 }, { 2.6,1.2,1,.80,.70,.65,.60,.60,.60,.60 } };
    private static readonly double[,] A13C = { { .37,.38,.50,.75,.90,1.1 }, { .25,.37,.57,.82,1,1.1 }, { .28,.47,.64,.87,1,1.1 } };
    private static readonly double[,] A13D = { { .44,.58,.70,.86,1,1.1 }, { .31,.48,.61,.76,.94,1.1 }, { .29,.47,.62,.74,.94,1.1 }, { .26,.45,.60,.73,.89,1 } };
    private static readonly double[,] A13G =
    {
        { .51,.34,.25,.21,.18,.17 }, { .54,.36,.27,.24,.22,.20 }, { .55,.38,.31,.27,.25,.24 },
        { .59,.43,.37,.35,.33,.33 }, { .63,.50,.46,.44,.43,.42 }, { .65,.56,.53,.52,.51,.50 }
    };
    private static readonly double[,] A13H =
    {
        { .54,.42,.37,.34,.32,.31 }, { .67,.58,.53,.51,.50,.51 }, { .75,.67,.65,.64,.64,.65 },
        { .80,.74,.72,.70,.70,.72 }, { .85,.78,.76,.75,.75,.76 }
    };
    private static readonly double[,] A14B =
    {
        { 52,30,18,8.2,4,2,.97,.42,.13 }, { 48,28,17,7.7,3.8,1.9,.91,.40,.13 },
        { 46,27,17,7.4,3.6,1.8,.88,.39,.13 }, { 42,24,15,6.6,3.2,1.6,.80,.36,.13 }
    };
    private static readonly double[] DamperAngle = { 0,10,20,30,40,50,60 };
    private static readonly double[] A15A = { .20,.52,1.5,4.5,11,29,108 };
    private static readonly double[] A15B = { .04,.33,1.2,3.3,9,26,70 };
    private static readonly double[,] A15D =
    {
        { 14,6.9,3.3,1.7,.83,.32,.09 }, { 19,8.8,4.5,2.4,1.2,.55,.17 },
        { 20,9.1,4.7,2.7,1.2,.47,.11 }, { 18,8.8,4.5,2.3,1.1,.51,.13 }
    };
    private static readonly double[] DamperLr = { .3,.4,.5,.6,.8,1,1.5 };
    private static readonly double[] DamperAngleWide = { 0,10,20,30,40,50,60,70,80 };
    private static readonly double[,] A15E =
    {
        { .52,.79,1.4,2.3,5,9,14,32,116 }, { .52,.85,1.5,2.4,6,9,16,38,152 },
        { .52,.92,1.5,2.4,6,9,18,45,188 }, { .52,.92,1.5,2.4,5.4,9,21,45,245 },
        { .52,.92,1.5,2.5,5.4,9,22,55,284 }, { .52,1,1.6,2.6,5.4,10,24,65,361 },
        { .52,1,1.6,2.7,5.4,10,28,102,576 }
    };
    private static readonly double[,] A15F =
    {
        { .52,.85,2.1,4.1,9,21,73,284,807 }, { .52,.92,2.2,5,11,28,100,332,915 },
        { .52,1,2.3,5.4,13,33,122,377,1045 }, { .52,1,2.3,6,14,38,148,411,1121 },
        { .52,1.1,2.4,6.6,18,54,188,495,1299 }, { .52,1.2,2.7,7.3,21,65,245,547,1521 },
        { .52,1.4,3.2,9,28,107,361,677,1654 }
    };
}
