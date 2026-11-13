namespace DuctSizer.Core;

/// <summary>
/// Outer surface area of one duct segment or duct fitting (the area that can leak). Equipment and terminals (coils,
/// filters, GRDs, VAV boxes, custom components) have no surface area here: their leakage is taken as 0 cfm/ft².
/// </summary>
/// <param name="ItemKey">Item key (<see cref="CriticalPathAnalyzer.DuctKey"/> or <see cref="CriticalPathAnalyzer.FittingKey"/>).</param>
/// <param name="Shape">Round or rectangular: decides the SMACNA leakage class applied to this area.</param>
/// <param name="PerimeterM">Duct perimeter: πD (round) or 2(W + H) (rectangular), m.</param>
/// <param name="LengthM">Duct length, or the fitting's estimated equivalent length, m.</param>
/// <param name="IsEstimate">True for fittings: their length is estimated (see <see cref="SurfaceAreaCalculator"/>).</param>
/// <param name="LengthBasis">How the length was found, in words, for the worksheet.</param>
/// <param name="Problem">Why the area could not be found (missing size or length); the area is then 0 and must not be used.</param>
public sealed record ItemSurfaceArea(
    string ItemKey,
    string ItemId,
    string Kind,
    DuctShape Shape,
    double DiameterM,
    double WidthM,
    double HeightM,
    double PerimeterM,
    double LengthM,
    double AreaM2,
    bool IsEstimate,
    string LengthBasis,
    string? Problem = null,
    double? ArcAngleDegrees = null,
    double? CenterlineRadiusM = null,
    double? RadiusRatio = null)
{
    public double AreaFt2 => UnitConversions.SquareMetersToSquareFeet(AreaM2);
}

/// <summary>
/// Surface area of ducts and fittings.
/// </summary>
/// <remarks>
/// Ducts: A = πDL (round) or 2(W + H)L (rectangular), from the duct's saved size and length (nominal inside
/// dimensions; sheet thickness is neglected).
/// Fittings (estimate): A = P × L_eq, with P the reference duct perimeter and L_eq an equivalent length. For an elbow
/// with a radius ratio (r/D or r/W) L_eq is the centerline arc, θ/360 × 2π × r with r = ratio × D (or W), and never
/// less than one reference dimension. For every other fitting L_eq is one reference dimension: D for round, the
/// larger of W and H for rectangular. ASHRAE fitting data carry no fitting length, so fitting areas are estimates.
/// </remarks>
public static class SurfaceAreaCalculator
{
    public static ItemSurfaceArea ForDuct(SavedDuct duct)
    {
        ArgumentNullException.ThrowIfNull(duct);
        var key = CriticalPathAnalyzer.DuctKey(duct.Sequence);
        var perimeter = Perimeter(duct.Shape, duct.DiameterM, duct.WidthM, duct.HeightM);
        if (perimeter is null)
            return Failed(key, duct.Id, "Duct", duct.Shape, "The duct has no valid size.");
        if (!double.IsFinite(duct.LengthM) || duct.LengthM <= 0)
            return Failed(key, duct.Id, "Duct", duct.Shape, "The duct has no length.");
        return new ItemSurfaceArea(key, duct.Id, "Duct", duct.Shape, duct.DiameterM, duct.WidthM, duct.HeightM,
            perimeter.Value, duct.LengthM, perimeter.Value * duct.LengthM, false, "Duct length");
    }

    /// <summary>The fitting's estimated surface area, or null when the record is a terminal or box (no ductwork area).</summary>
    public static ItemSurfaceArea? ForFitting(SavedFitting fitting)
    {
        ArgumentNullException.ThrowIfNull(fitting);
        if (IsTerminalOrBox(fitting.FittingCode))
            return null;
        var key = CriticalPathAnalyzer.FittingKey(fitting.Sequence);
        var perimeter = Perimeter(fitting.Shape, fitting.DiameterM, fitting.WidthM, fitting.HeightM);
        if (perimeter is null)
            return Failed(key, fitting.Id, "Fitting", fitting.Shape, "The fitting has no valid reference duct size.");
        var reference = fitting.Shape == DuctShape.Round ? fitting.DiameterM : Math.Max(fitting.WidthM, fitting.HeightM);
        var length = reference;
        var basis = fitting.Shape == DuctShape.Round
            ? "Estimate: one reference diameter, L = D"
            : "Estimate: one reference dimension, L = max(W, H)";
        string category;
        try
        {
            category = FittingCatalog.Find(fitting.FittingCode).Category;
        }
        catch (KeyNotFoundException)
        {
            category = string.Empty;
        }
        if (string.Equals(category, "Elbows", StringComparison.OrdinalIgnoreCase)
            && fitting.Inputs.TryGetValue("angle", out var angle) && double.IsFinite(angle) && angle > 0)
        {
            var hasRatio = fitting.Inputs.TryGetValue("rd", out var ratio) || fitting.Inputs.TryGetValue("rw", out ratio);
            if (hasRatio && double.IsFinite(ratio) && ratio > 0)
            {
                var bendDimension = fitting.Shape == DuctShape.Round ? fitting.DiameterM : fitting.WidthM;
                var radius = ratio * bendDimension;
                var arc = angle / 360.0 * 2.0 * Math.PI * radius;
                if (arc > reference)
                    return new ItemSurfaceArea(key, fitting.Id, "Fitting", fitting.Shape, fitting.DiameterM, fitting.WidthM,
                        fitting.HeightM, perimeter.Value, arc, perimeter.Value * arc, true,
                        $"Estimate: elbow centerline arc, r = {ratio:0.###} x {(fitting.Shape == DuctShape.Round ? "D" : "W")}",
                        ArcAngleDegrees: angle, CenterlineRadiusM: radius, RadiusRatio: ratio);
            }
        }
        return new ItemSurfaceArea(key, fitting.Id, "Fitting", fitting.Shape, fitting.DiameterM, fitting.WidthM,
            fitting.HeightM, perimeter.Value, length, perimeter.Value * length, true, basis);
    }

    /// <summary>GRDs and VAV boxes are terminals/equipment stored as fitting records: no ductwork area.</summary>
    public static bool IsTerminalOrBox(string fittingCode) =>
        string.Equals(fittingCode, GrdPerformanceCatalog.Titus350RCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fittingCode, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fittingCode, GrdPerformanceCatalog.TitusTbd80Code, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fittingCode, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fittingCode, VavPerformanceCatalog.WithoutReheatCode, StringComparison.OrdinalIgnoreCase);

    private static double? Perimeter(DuctShape shape, double diameter, double width, double height)
    {
        if (shape == DuctShape.Round)
            return double.IsFinite(diameter) && diameter > 0 ? Math.PI * diameter : null;
        return double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0 ? 2.0 * (width + height) : null;
    }

    private static ItemSurfaceArea Failed(string key, string id, string kind, DuctShape shape, string problem) =>
        new(key, id, kind, shape, 0, 0, 0, 0, 0, 0, kind == "Fitting", string.Empty, problem);
}

/// <summary>SMACNA seal classes (HVAC Duct Construction Standards, Table 1-2 "Standard duct sealing requirements").</summary>
public enum SmacnaSealClass
{
    /// <summary>All transverse joints, longitudinal seams and duct wall penetrations (4 in. w.c. and up).</summary>
    A,
    /// <summary>All transverse joints and longitudinal seams (3 in. w.c.).</summary>
    B,
    /// <summary>Transverse joints only (2 in. w.c.; recommended for the 1 and ½ in. w.c. classes).</summary>
    C
}

/// <summary>The leakage allowance of one duct type (round or rectangular) in the system.</summary>
/// <param name="LeakageClass">C_L, cfm per 100 ft² at 1 in. w.c.</param>
/// <param name="FactorCfmPer100Ft2">F = C_L × P^0.65, cfm per 100 ft².</param>
/// <param name="LeakageCfm">F × A / 100, cfm.</param>
public sealed record LeakageByShape(DuctShape Shape, double AreaFt2, int LeakageClass, double FactorCfmPer100Ft2, double LeakageCfm);

/// <summary>The system's SMACNA pressure class, seal class and allowable duct leakage.</summary>
/// <param name="DesignPressureInWc">The pressure the class is chosen for: the larger of the critical-path loss and the fan static pressure.</param>
/// <param name="DesignPressureBasis">Which value governed, in words.</param>
/// <param name="PressureClassInWc">Smallest SMACNA static pressure class at or above the design pressure.</param>
/// <param name="Problem">Why the result is not available (incomplete critical path, a duct without a size or length, pressure above 10 in. w.c.).</param>
/// <param name="CalculatedPressureClassInWc">The class from the design pressure, before any override (null when not known).</param>
/// <param name="RecommendedSealClass">The seal class SMACNA recommends for the pressure class in use.</param>
public sealed record SystemLeakageResult(
    double? DesignPressureInWc,
    string DesignPressureBasis,
    double? PressureClassInWc,
    SmacnaSealClass? SealClass,
    IReadOnlyList<ItemSurfaceArea> Areas,
    IReadOnlyList<LeakageByShape> ByShape,
    string? Problem,
    double? CalculatedPressureClassInWc = null,
    SmacnaSealClass? RecommendedSealClass = null,
    bool PressureClassOverridden = false,
    bool SealClassOverridden = false,
    PressureSide Side = PressureSide.Positive)
{
    public bool IsAvailable => Problem is null && PressureClassInWc is not null;
    public double TotalAreaFt2 => Areas.Where(x => x.Problem is null).Sum(x => x.AreaFt2);
    public double TotalLeakageCfm => ByShape.Sum(x => x.LeakageCfm);
}

/// <summary>The side of the fan a duct is on: negative static pressure upstream (suction), positive downstream (discharge).</summary>
public enum PressureSide
{
    /// <summary>Upstream of the fan or air handling unit: static pressure below atmospheric (suction).</summary>
    Negative,
    /// <summary>Downstream of the fan or air handling unit: static pressure above atmospheric (discharge).</summary>
    Positive
}

/// <summary>
/// SMACNA pressure class, seal class and leakage for each side of the fan. Ductwork upstream of the fan (suction,
/// negative static pressure) and downstream (discharge, positive) are classed separately, each for its own design
/// pressure, with its own overrides.
/// </summary>
/// <param name="Negative">The suction side; null when no ductwork is upstream of the fan.</param>
/// <param name="Positive">The discharge side (the whole system when there is no fan or no suction ductwork).</param>
public sealed record SystemLeakageSides(SystemLeakageResult? Negative, SystemLeakageResult Positive)
{
    /// <summary>The result that applies to the duct or fitting with this key (its side of the fan).</summary>
    public SystemLeakageResult For(string itemKey) =>
        Negative is not null && Negative.Areas.Any(area => string.Equals(area.ItemKey, itemKey, StringComparison.Ordinal))
            ? Negative : Positive;

    /// <summary>True when every side present is available.</summary>
    public bool IsAvailable => Positive.IsAvailable && (Negative?.IsAvailable ?? true);

    /// <summary>Total allowable leakage of both sides, cfm.</summary>
    public double TotalLeakageCfm => (Negative?.TotalLeakageCfm ?? 0) + Positive.TotalLeakageCfm;

    /// <summary>Total duct and fitting surface area of both sides, ft².</summary>
    public double TotalAreaFt2 => (Negative?.TotalAreaFt2 ?? 0) + Positive.TotalAreaFt2;
}

/// <summary>
/// SMACNA duct pressure class, seal class and allowable leakage.
/// </summary>
/// <remarks>
/// Sources, as applied here:
/// <list type="bullet">
/// <item>Static pressure classes ½, 1, 2, 3, 4, 6 and 10 in. w.c.; a class covers operating pressures above the next
/// lower class up to its own value (SMACNA HVAC Duct Construction Standards — Metal and Flexible, Table 1-1). The class
/// is the smallest one at or above the design pressure (rounded up, never down).</item>
/// <item>Seal class: A for 4 in. w.c. and up, B for 3 in. w.c., C for 2 in. w.c.; SMACNA recommends Seal Class C for the
/// 1 and ½ in. w.c. classes (Duct Construction Standards, "Standard duct sealing requirements").</item>
/// <item>Leakage class C_L (cfm/100 ft² at 1 in. w.c.): Seal Class A rectangular 6, round 3; B rectangular 12, round 6;
/// C rectangular 24, round 12 (SMACNA HVAC Air Duct Leakage Test Manual, duct leakage classification). Fittings use
/// the class of their reference duct shape.</item>
/// <item>Allowable leakage F = C_L × P^0.65 (cfm/100 ft²) with P the pressure class in in. w.c.; leakage = F × A / 100.</item>
/// </list>
/// Newer SMACNA editions recommend Seal Class A for all ductwork; this follows the seal class by pressure class as
/// specified for FanWorks.
/// </remarks>
public static class SmacnaLeakage
{
    public static readonly IReadOnlyList<double> PressureClassesInWc = new[] { 0.5, 1, 2, 3, 4, 6, 10 };

    public const double PressureExponent = 0.65;

    public const string Reference = "SMACNA HVAC Duct Construction Standards - Metal and Flexible (pressure and seal classes); "
                                    + "SMACNA HVAC Air Duct Leakage Test Manual (leakage classes)";

    /// <summary>Smallest pressure class at or above |<paramref name="pressureInWc"/>|; null above 10 in. w.c. or when not finite.</summary>
    public static double? PressureClassFor(double pressureInWc)
    {
        if (!double.IsFinite(pressureInWc))
            return null;
        var magnitude = Math.Abs(pressureInWc);
        foreach (var pressureClass in PressureClassesInWc)
            if (magnitude <= pressureClass + 1e-9)
                return pressureClass;
        return null;
    }

    public static SmacnaSealClass SealClassFor(double pressureClassInWc) => pressureClassInWc >= 4 - 1e-9
        ? SmacnaSealClass.A
        : pressureClassInWc >= 3 - 1e-9 ? SmacnaSealClass.B : SmacnaSealClass.C;

    public static int LeakageClass(SmacnaSealClass sealClass, DuctShape shape) => (sealClass, shape) switch
    {
        (SmacnaSealClass.A, DuctShape.Round) => 3,
        (SmacnaSealClass.A, _) => 6,
        (SmacnaSealClass.B, DuctShape.Round) => 6,
        (SmacnaSealClass.B, _) => 12,
        (SmacnaSealClass.C, DuctShape.Round) => 12,
        _ => 24
    };

    /// <summary>F = C_L × P^0.65, cfm per 100 ft² of duct surface.</summary>
    public static double AllowableLeakageFactor(int leakageClass, double pressureClassInWc) =>
        leakageClass * Math.Pow(pressureClassInWc, PressureExponent);

    /// <summary>
    /// Surface areas of the ducts and fittings that belong to the drawn system (<see cref="SystemMembership"/>). A
    /// standalone fitting or duct kept in the project for reference is not ductwork of this system and never changes
    /// its area or leakage (review P1-11).
    /// </summary>
    public static ItemSurfaceArea[] SystemAreas(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return SystemMembership.Ducts(document).Select(SurfaceAreaCalculator.ForDuct)
            .Concat(SystemMembership.Fittings(document).Select(SurfaceAreaCalculator.ForFitting)
                .Where(x => x is not null).Select(x => x!))
            .ToArray();
    }

    /// <summary>Surface areas, pressure class, seal class and allowable leakage of the whole system.</summary>
    public static SystemLeakageResult Analyze(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var areas = SystemAreas(document);
        if (areas.Length == 0)
            return new SystemLeakageResult(null, string.Empty, null, null, areas, Array.Empty<LeakageByShape>(),
                "There are no ducts or fittings.");

        // The design pressure and the class it gives (rounded up), then any override the engineer chose
        // (Customize Duct Class). An overridden class does not need a complete critical path.
        var overrides = document.AnalysisExpectations ?? new AnalysisExpectations();
        double? design = null;
        var basis = string.Empty;
        string? pressureProblem = null;
        var critical = CriticalPathAnalyzer.Analyze(document);
        if (critical.IsComplete)
        {
            var criticalInWc = UnitConversions.PaToInchesWater(critical.PressureLossPa);
            var fans = document.LayoutNodes.Where(node => node.Type == LayoutNodeType.Fan).ToArray();
            var fanInWc = fans.Length == 1 && double.IsFinite(fans[0].FanStaticPressurePa) && fans[0].FanStaticPressurePa > 0
                ? UnitConversions.PaToInchesWater(fans[0].FanStaticPressurePa)
                : 0.0;
            design = Math.Max(criticalInWc, fanInWc);
            // The ductwork sees the fan's static pressure rise (an air handling unit's external static pressure); an
            // air handling unit's internal static pressure is used inside the unit and is not duct pressure.
            basis = fanInWc > criticalInWc
                ? fans[0].IsAirHandlingUnit ? "air handling unit external static pressure" : "fan static pressure"
                : "critical-path pressure loss";
        }
        else
            pressureProblem = "The critical path is not complete, so the system pressure is not known.";
        return Classify(areas, design, basis, pressureProblem, overrides.PressureClassOverrideInWc,
            overrides.SealClassOverride, PressureSide.Positive);
    }

    /// <summary>
    /// Pressure class, seal class and leakage for each side of the fan. Ducts and fittings before the fan on any path
    /// are on the negative (suction) side; all others on the positive (discharge) side.
    /// <list type="bullet">
    /// <item>Negative design pressure: the largest suction loss on any complete path (the static pressure at the fan
    /// inlet, below atmospheric).</item>
    /// <item>Positive design pressure: the larger of the largest discharge loss on any complete path and the fan's
    /// external static pressure less the suction loss (the static pressure at the fan outlet).</item>
    /// </list>
    /// With no ductwork upstream of the fan the positive side is the whole system, exactly as <see cref="Analyze"/>.
    /// Positive-side overrides are <see cref="AnalysisExpectations.PressureClassOverrideInWc"/> and
    /// <see cref="AnalysisExpectations.SealClassOverride"/>; negative-side overrides are
    /// <see cref="AnalysisExpectations.SuctionPressureClassOverrideInWc"/> and
    /// <see cref="AnalysisExpectations.SuctionSealClassOverride"/>.
    /// </summary>
    public static SystemLeakageSides AnalyzeSides(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var areas = SystemAreas(document);
        var profiles = SystemPathProfiles.Build(document);
        var suctionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in profiles.Paths)
            for (var index = 0; index < path.FanStepIndex; index++)
                suctionKeys.Add(path.Steps[index].ItemKey);
        var suctionAreas = areas.Where(area => suctionKeys.Contains(area.ItemKey)).ToArray();
        if (suctionAreas.Length == 0)
            return new SystemLeakageSides(null, Analyze(document));

        var overrides = document.AnalysisExpectations ?? new AnalysisExpectations();
        var dischargeAreas = areas.Where(area => !suctionKeys.Contains(area.ItemKey)).ToArray();
        double? suctionDesign = null, dischargeDesign = null;
        var dischargeBasis = string.Empty;
        string? problem = null;
        var complete = profiles.Paths.Where(path => path.IsComplete && path.FanStepIndex >= 0).ToArray();
        if (CriticalPathAnalyzer.Analyze(document).IsComplete && complete.Length > 0)
        {
            var suction = UnitConversions.PaToInchesWater(complete.Max(path => path.SuctionLossPa ?? 0));
            var discharge = UnitConversions.PaToInchesWater(complete.Max(path => path.DischargeLossPa ?? 0));
            var fans = document.LayoutNodes.Where(node => node.Type == LayoutNodeType.Fan).ToArray();
            var fanOutlet = fans.Length == 1 && double.IsFinite(fans[0].FanStaticPressurePa) && fans[0].FanStaticPressurePa > 0
                ? UnitConversions.PaToInchesWater(fans[0].FanStaticPressurePa) - suction
                : 0.0;
            suctionDesign = suction;
            dischargeDesign = Math.Max(discharge, fanOutlet);
            dischargeBasis = fanOutlet > discharge
                ? (fans[0].IsAirHandlingUnit ? "air handling unit" : "fan") + " outlet static pressure (external static pressure less the suction loss)"
                : "discharge-side pressure loss";
        }
        else
            problem = "The critical path is not complete, so the system pressure is not known.";
        var negative = Classify(suctionAreas, suctionDesign, "suction-side pressure loss up to the fan inlet",
            problem, overrides.SuctionPressureClassOverrideInWc, overrides.SuctionSealClassOverride, PressureSide.Negative);
        var positive = dischargeAreas.Length == 0
            ? new SystemLeakageResult(dischargeDesign, dischargeBasis, null, null, dischargeAreas, Array.Empty<LeakageByShape>(),
                "There is no ductwork downstream of the fan.", Side: PressureSide.Positive)
            : Classify(dischargeAreas, dischargeDesign, dischargeBasis, problem, overrides.PressureClassOverrideInWc,
                overrides.SealClassOverride, PressureSide.Positive);
        return new SystemLeakageSides(negative, positive);
    }

    /// <summary>The class from a design pressure (or an override), its seal class and the leakage of the areas.</summary>
    private static SystemLeakageResult Classify(IReadOnlyList<ItemSurfaceArea> areas, double? design, string basis,
        string? pressureProblem, double? classOverride, SmacnaSealClass? sealOverride, PressureSide side)
    {
        var calculatedClass = design is double known ? PressureClassFor(known) : null;
        if (design is not null && calculatedClass is null)
            pressureProblem = "The system pressure is above the highest SMACNA pressure class (10 in. w.c.).";
        var pressureOverridden = classOverride is not null;
        var pressureClass = classOverride ?? calculatedClass;
        if (pressureClass is null)
            return new SystemLeakageResult(design, basis, null, null, areas, Array.Empty<LeakageByShape>(),
                pressureProblem ?? "The system pressure is not known.", Side: side);
        var recommended = SealClassFor(pressureClass.Value);
        var sealOverridden = sealOverride is not null;
        var seal = sealOverride ?? recommended;
        var failed = areas.FirstOrDefault(x => x.Problem is not null);
        var byShape = areas.Where(x => x.Problem is null)
            .GroupBy(x => x.Shape)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var area = group.Sum(x => x.AreaFt2);
                var leakageClass = LeakageClass(seal, group.Key);
                var factor = AllowableLeakageFactor(leakageClass, pressureClass.Value);
                return new LeakageByShape(group.Key, area, leakageClass, factor, factor * area / 100.0);
            })
            .ToArray();
        return new SystemLeakageResult(design, basis, pressureClass, seal, areas, byShape,
            failed is null ? null : $"{failed.ItemId}: {failed.Problem}",
            calculatedClass, recommended, pressureOverridden, sealOverridden, side);
    }
}
