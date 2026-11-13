using DuctSizer.Core;

/// <summary>
/// Round T: every table look-up is recorded so the worksheet can show its interpolation (or why none was used), a
/// value between two non-interpolable columns takes the conservative (larger) coefficient, and only the selected
/// junction path's table is read. Expected values are read by hand from the published tables in FittingCatalog.
/// </summary>
internal static class TableLookupTests
{
    private static FittingCalculationResult Elbow(double radiusRatio, double angle) => FittingCalculator.Calculate(
        FittingCatalog.Find("3-1"),
        new Dictionary<string, double> { ["rd"] = radiusRatio, ["angle"] = angle },
        UnitConversions.CfmToM3PerS(1000),
        GeometryCalculator.Round(UnitConversions.InchesToM(12)),
        UnitConversions.FahrenheitToCelsius(70));

    public static void ElbowFactorsAreNamedAndCombined()
    {
        // ASHRAE 3-1, r/D 1.25 and 50°, by hand:
        //   C'o = 0.22 + (1.25 - 1.0)/(1.5 - 1.0) x (0.15 - 0.22) = 0.185      (base coefficient, by R/D)
        //   Kθ  = 0.60 + (50 - 45)/(60 - 45) x (0.78 - 0.60)     = 0.66       (angle factor, by θ)
        //   C   = Kθ x C'o = 0.66 x 0.185                       = 0.1221
        var elbow = Elbow(1.25, 50);
        Near(elbow.Coefficient, .1221, 1e-12, "3-1 at r/D 1.25, 50°");
        var factors = elbow.CoefficientFactors;
        Require(factors.Count == 2 && factors[0].Symbol == "K_{\\theta}" && factors[1].Symbol == "C'_{o}",
            $"3-1 should record its factors as Kθ then C'o; got {string.Join(", ", factors.Select(x => x.Symbol))}.");
        Near(factors[0].Value, .66, 1e-12, "Kθ");
        Near(factors[1].Value, .185, 1e-12, "C'o");
        Require(elbow.TableLookups.Count == 2
                && elbow.TableLookups.Any(step => step.Symbol == "C'_{o}" && Math.Abs(step.Result - .185) < 1e-12)
                && elbow.TableLookups.Any(step => step.Symbol == "K_{\\theta}" && Math.Abs(step.Result - .66) < 1e-12),
            "Each look-up should carry the symbol of the value it gives.");

        // A single-table fitting has no factors (its one look-up gives C directly).
        var tee = FittingCalculator.Calculate(FittingCatalog.Find("3-4"),
            new Dictionary<string, double> { ["ld"] = 1.2 }, UnitConversions.CfmToM3PerS(1000),
            GeometryCalculator.Round(UnitConversions.InchesToM(12)),
            UnitConversions.FahrenheitToCelsius(70));
        Require(tee.CoefficientFactors.Count == 0 && tee.TableLookups.All(step => step.Symbol.Length == 0),
            "A fitting read from one table has no named factors.");
    }

    public static void InterpolationIsRecordedWithItsTablePoints()
    {
        // ASHRAE 3-1: C'o at r/D 1.0 = 0.22 and 1.5 = 0.15; r/D = 1.25 is halfway: 0.22 + 0.5 (0.15 - 0.22) = 0.185.
        // The angle factor at 90° is the published point K = 1.00.
        var halfway = Elbow(1.25, 90);
        Near(halfway.Coefficient, .185, 1e-12, "3-1 at r/D 1.25");
        var linear = halfway.TableLookups.SingleOrDefault(step => step.Kind == TableLookupKind.Linear);
        Require(linear is not null && linear.Axes.Count == 1, "The r/D look-up should be recorded as a linear interpolation.");
        Near(linear!.Axes[0].Lower, 1.0, 1e-12, "lower r/D point");
        Near(linear.Axes[0].Upper, 1.5, 1e-12, "upper r/D point");
        Near(linear.Values[0], .22, 1e-12, "C'o at r/D 1.0");
        Near(linear.Values[1], .15, 1e-12, "C'o at r/D 1.5");
        Near(linear.Result, .185, 1e-12, "interpolated C'o");
        Near(linear.Axes[0].Fraction, .5, 1e-12, "position between the points");
        Require(linear.Axes[0].Name.Contains("R/D", StringComparison.OrdinalIgnoreCase) || linear.Axes[0].Name.Contains("radius", StringComparison.OrdinalIgnoreCase),
            $"The axis should be named after its input, not '{linear.Axes[0].Name}'.");
        Require(halfway.TableLookups.Any(step => step.Kind == TableLookupKind.Exact && Math.Abs(step.Result - 1.0) < 1e-12),
            "The 90° angle factor is a published point and should be recorded as used as printed.");

        // On a published point nothing is interpolated.
        var exact = Elbow(1.5, 90);
        Near(exact.Coefficient, .15, 1e-12, "3-1 at r/D 1.5");
        Require(exact.TableLookups.Count > 0 && exact.TableLookups.All(step => step.Kind == TableLookupKind.Exact),
            "Inputs on published points should be recorded as exact table entries.");

        // Beyond the table the edge is held, flagged and never extrapolated. 5-10 branch: Vb/Vs is published up to 2.0;
        // a derived ratio of 2.25 (not range-checked like a typed input) uses the 2.0 value.
        var beyond = FittingCalculator.Calculate(FittingCatalog.Find("5-10"),
            new Dictionary<string, double> { ["path"] = 0, ["vbvs"] = 2.25 },
            UnitConversions.CfmToM3PerS(600), GeometryCalculator.Round(UnitConversions.InchesToM(10)), UnitConversions.FahrenheitToCelsius(70));
        var atEdge = FittingCalculator.Calculate(FittingCatalog.Find("5-10"),
            new Dictionary<string, double> { ["path"] = 0, ["vbvs"] = 2.0 },
            UnitConversions.CfmToM3PerS(600), GeometryCalculator.Round(UnitConversions.InchesToM(10)), UnitConversions.FahrenheitToCelsius(70));
        Near(beyond.Coefficient, atEdge.Coefficient, 1e-12, "5-10 branch beyond the table equals the edge value");
        Require(beyond.TableLookups.Any(step => step.Kind == TableLookupKind.HeldAtEdge && step.Axes[0].OutsideTable)
                && beyond.TableRangeWarnings.Count > 0,
            "An input beyond the table should be recorded as held at the edge and raise a review warning.");
        Require(atEdge.TableLookups.All(step => step.Kind == TableLookupKind.Exact) && atEdge.TableRangeWarnings.Count == 0,
            "The last published point itself is an exact entry, not an edge hold.");
    }

    private static FittingCalculationResult FiveTwentyFourMain(double straightAreaRatio, double straightVelocityRatio,
        double branchVelocityRatio = .8) => FittingCalculator.Calculate(
        FittingCatalog.Find("5-24"),
        new Dictionary<string, double>
        {
            ["angle"] = 90, ["path"] = 1, ["asac"] = straightAreaRatio, ["vsvc"] = straightVelocityRatio, ["vbvc"] = branchVelocityRatio
        },
        UnitConversions.CfmToM3PerS(1000),
        GeometryCalculator.Round(UnitConversions.InchesToM(12)),
        UnitConversions.FahrenheitToCelsius(70));

    public static void BetweenColumnsTheConservativeValueIsUsed()
    {
        // 5-24 main (90°) at Vs/Vc = 0.8: As/Ac column 0.5 gives 0.17 and column 0.6 gives 0.10. The columns are not
        // interpolated; As/Ac = 0.58 now takes the larger, 0.17 (it used to round to the nearest column, 0.10).
        var between = FiveTwentyFourMain(.58, .8);
        Near(between.Coefficient, .17, 1e-12, "5-24 main between columns");
        var conservative = between.TableLookups.SingleOrDefault(step => step.Kind == TableLookupKind.ConservativeColumn);
        Require(conservative is not null, "The column choice should be recorded as a conservative rounding.");
        Near(conservative!.Values[0], .17, 1e-12, "C at As/Ac 0.5");
        Near(conservative.Values[1], .10, 1e-12, "C at As/Ac 0.6");
        Near(conservative.Axes[0].Lower, .5, 1e-12, "lower column");
        Near(conservative.Axes[0].Upper, .6, 1e-12, "upper column");

        // On a published column there is no choice to make.
        var onColumn = FiveTwentyFourMain(.6, .8);
        Near(onColumn.Coefficient, .10, 1e-12, "5-24 main on the 0.6 column");
        Require(onColumn.TableLookups.All(step => step.Kind != TableLookupKind.ConservativeColumn),
            "A ratio on a published column should not be reported as rounded.");

        // Where only one neighbouring column is published at this Vs/Vc (0.7 ends at 1.4, 0.8 at 1.2), that one is used:
        // column 0.7 at Vs/Vc 1.4 is 0.39.
        var single = FiveTwentyFourMain(.75, 1.4);
        Near(single.Coefficient, .39, 1e-12, "5-24 main at As/Ac 0.75, Vs/Vc 1.4 (column 0.7 only)");
        Require(single.TableLookups.Any(step => step.Kind == TableLookupKind.ConservativeColumn && !string.IsNullOrEmpty(step.Note)
                                               && double.IsNaN(step.Values[1])),
            "When only one column is published the record should say so.");
    }

    public static void OnlyTheSelectedJunctionPathIsRead()
    {
        // Straight path selected: an out-of-range branch ratio must neither fail the calculation nor add warnings.
        var straight = FiveTwentyFourMain(.6, .8, branchVelocityRatio: 99);
        Require(straight.TableRangeWarnings.Count == 0
                && straight.TableLookups.All(step => step.Axes.All(axis => Math.Abs(axis.Value - 99) > 1e-9)),
            "The unused branch table should not be read when the straight path is selected.");
    }

    public static void EngineeringTextIsTypeset()
    {
        var pretty = EngineeringText.Pretty("rho = 0.075 lbm/ft3; T = 55 deg F; t = 70 degF; A = 2.5 ft2; 3 x 4; mu = 1; Delta P = 0.1; epsilon_r = 0.0002");
        Require(pretty == "ρ = 0.075 lbm/ft³; T = 55 °F; t = 70 °F; A = 2.5 ft²; 3 × 4; μ = 1; ΔP = 0.1; εr = 0.0002",
            $"Unexpected typeset text: {pretty}");
        Require(EngineeringText.Pretty("Fittings 3-1 to 3-9, pipe and Max W") == "Fittings 3-1 to 3-9, pipe and Max W",
            "Ordinary words must not be changed.");
    }

    private static void Near(double actual, double expected, double tolerance, string what)
    {
        if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception($"{what}: expected {expected:R}, got {actual:R}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
