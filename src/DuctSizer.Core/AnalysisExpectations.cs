namespace DuctSizer.Core;

/// <summary>
/// An expected range the engineer enters for one quantity (Analysis Settings, schema 16). Values are SI. Either bound
/// may be left empty (null); an empty bound is not checked. Nothing here is a design criterion supplied by FanWorks:
/// there are no default ranges, and a quantity with no bounds is reported as "not set" rather than as passing.
/// </summary>
public sealed class ExpectedRange
{
    public double? Low { get; set; }
    public double? High { get; set; }

    /// <summary>True when at least one bound is entered.</summary>
    public bool IsSet => Low is not null || High is not null;

    /// <summary>
    /// Null when the range can be checked, otherwise why not: a bound that is not a finite, non-negative number, or a
    /// low bound above the high bound.
    /// </summary>
    public string? Problem =>
        Low is double low && (!double.IsFinite(low) || low < 0) ? "The low value is not a valid non-negative number."
        : High is double high && (!double.IsFinite(high) || high < 0) ? "The high value is not a valid non-negative number."
        : Low is double l && High is double h && l > h ? "The low value is above the high value."
        : null;

    /// <summary>
    /// True when <paramref name="value"/> lies inside the entered bounds (inclusive). A relative allowance of one part
    /// in a billion keeps a value equal to a bound, entered in display units and converted to SI, inside the range.
    /// </summary>
    public bool Contains(double value)
    {
        if (!double.IsFinite(value))
            return false;
        if (Low is double low && value < low - Allowance(low))
            return false;
        if (High is double high && value > high + Allowance(high))
            return false;
        return true;

        static double Allowance(double bound) => Math.Max(Math.Abs(bound), 1e-12) * 1e-9;
    }

    public ExpectedRange Copy() => new() { Low = Low, High = High };
}

/// <summary>The quantities an engineer can give an expected range for.</summary>
public enum ExpectedQuantity
{
    /// <summary>Duct friction rate, Pa/m (in. w.c. per 100 ft in imperial projects).</summary>
    DuctPressureRate,
    /// <summary>Mean air velocity in a duct, m/s (fpm).</summary>
    DuctVelocity,
    /// <summary>Reference velocity of a fitting (the velocity its loss coefficient applies to), m/s (fpm).</summary>
    FittingVelocity,
    /// <summary>Total pressure loss across a fitting at its saved flow path, Pa (in. w.c.).</summary>
    FittingLoss
}

/// <summary>Expected ranges for the Analysis tab (schema 16). All ranges are optional and empty by default.</summary>
public sealed class AnalysisExpectations
{
    public ExpectedRange DuctPressureRatePaPerM { get; set; } = new();
    public ExpectedRange DuctVelocityMPerS { get; set; } = new();
    public ExpectedRange FittingVelocityMPerS { get; set; } = new();
    public ExpectedRange FittingLossPa { get; set; } = new();

    /// <summary>
    /// Engineer's SMACNA static pressure class, in. w.c., replacing the calculated one (Customize Duct Class). Null
    /// uses the calculated class. Must be one of <see cref="SmacnaLeakage.PressureClassesInWc"/>. Schema 17.
    /// </summary>
    public double? PressureClassOverrideInWc { get; set; }

    /// <summary>Engineer's seal class replacing the one recommended for the pressure class. Null uses the recommendation. Schema 17.</summary>
    public SmacnaSealClass? SealClassOverride { get; set; }

    /// <summary>True when any range has a bound entered.</summary>
    public bool AnySet => DuctPressureRatePaPerM.IsSet || DuctVelocityMPerS.IsSet
                          || FittingVelocityMPerS.IsSet || FittingLossPa.IsSet;

    public ExpectedRange RangeFor(ExpectedQuantity quantity) => quantity switch
    {
        ExpectedQuantity.DuctPressureRate => DuctPressureRatePaPerM,
        ExpectedQuantity.DuctVelocity => DuctVelocityMPerS,
        ExpectedQuantity.FittingVelocity => FittingVelocityMPerS,
        ExpectedQuantity.FittingLoss => FittingLossPa,
        _ => throw new ArgumentOutOfRangeException(nameof(quantity))
    };

    /// <summary>Replaces missing range objects (older or hand-edited files) with empty ranges. Values are kept.</summary>
    public void Normalize()
    {
        // A pressure class that is not a SMACNA class (hand-edited file) is dropped rather than guessed at.
        if (PressureClassOverrideInWc is double overrideClass
            && !SmacnaLeakage.PressureClassesInWc.Any(value => Math.Abs(value - overrideClass) < 1e-9))
            PressureClassOverrideInWc = null;
        DuctPressureRatePaPerM ??= new ExpectedRange();
        DuctVelocityMPerS ??= new ExpectedRange();
        FittingVelocityMPerS ??= new ExpectedRange();
        FittingLossPa ??= new ExpectedRange();
    }

    public AnalysisExpectations Copy() => new()
    {
        DuctPressureRatePaPerM = DuctPressureRatePaPerM.Copy(),
        DuctVelocityMPerS = DuctVelocityMPerS.Copy(),
        FittingVelocityMPerS = FittingVelocityMPerS.Copy(),
        FittingLossPa = FittingLossPa.Copy(),
        PressureClassOverrideInWc = PressureClassOverrideInWc,
        SealClassOverride = SealClassOverride
    };
}

/// <summary>Outcome of checking one item against the expected ranges.</summary>
public enum RangeCheckStatus
{
    /// <summary>No expected range applies to this kind of item (coils, filters, GRDs, custom components).</summary>
    NotApplicable,
    /// <summary>A range applies to this kind of item, but none has been entered.</summary>
    NotSet,
    /// <summary>Every entered range that applies holds.</summary>
    Within,
    /// <summary>At least one entered range that applies does not hold.</summary>
    Outside,
    /// <summary>The item could not be calculated, or an entered range is invalid, so it cannot be checked.</summary>
    NotAvailable
}

/// <summary>
/// One quantity compared with its range. <paramref name="UpperValue"/> is set for a duct in transitional flow, whose
/// friction rate is a band (lower to upper bound): the duct is within range only when the whole band is.
/// </summary>
public sealed record RangeFinding(ExpectedQuantity Quantity, double Value, double? UpperValue, ExpectedRange Range, bool Within);

/// <summary>The result of <see cref="AnalysisRangeCheck"/> for one item, with every comparison made.</summary>
public sealed record RangeCheckResult(RangeCheckStatus Status, IReadOnlyList<RangeFinding> Findings, string? Problem = null);

/// <summary>
/// Compares calculated duct and fitting results with the engineer's expected ranges. Ducts are checked for friction
/// rate and velocity, fittings for reference velocity and pressure loss; other items have no ranges. The values
/// compared are the same calculated values shown elsewhere in FanWorks: nothing is recalculated differently here.
/// </summary>
public static class AnalysisRangeCheck
{
    /// <summary>Checks a duct's friction rate (both bounds of a transitional band) and its actual mean velocity.</summary>
    public static RangeCheckResult CheckDuct(AnalysisExpectations expectations, DuctCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(result);
        var transitional = result.Friction.Regime == FlowRegime.Transitional;
        var rateLow = transitional ? result.PressureLossLowerPaPerM : result.PressureLossPaPerM;
        double? rateHigh = transitional ? result.PressureLossUpperPaPerM : null;
        return Combine(
            Compare(expectations.DuctPressureRatePaPerM, ExpectedQuantity.DuctPressureRate, rateLow, rateHigh),
            Compare(expectations.DuctVelocityMPerS, ExpectedQuantity.DuctVelocity, result.ActualVelocityMPerS, null));
    }

    /// <summary>Checks a fitting's reference velocity and its pressure loss at the saved flow path.</summary>
    public static RangeCheckResult CheckFitting(AnalysisExpectations expectations, FittingCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(result);
        return Combine(
            Compare(expectations.FittingVelocityMPerS, ExpectedQuantity.FittingVelocity, result.ReferenceVelocityMPerS, null),
            Compare(expectations.FittingLossPa, ExpectedQuantity.FittingLoss, result.PressureLossPa, null));
    }

    /// <summary>Checks a scheduled duct by its saved inputs; NotAvailable when it cannot be calculated.</summary>
    public static RangeCheckResult CheckDuct(AnalysisExpectations expectations, SavedDuct duct)
    {
        ArgumentNullException.ThrowIfNull(duct);
        try
        {
            return CheckDuct(expectations, duct.Calculate());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return new RangeCheckResult(RangeCheckStatus.NotAvailable, Array.Empty<RangeFinding>(), ex.Message);
        }
    }

    /// <summary>Checks a scheduled fitting by its saved inputs; NotAvailable when it cannot be calculated.</summary>
    public static RangeCheckResult CheckFitting(AnalysisExpectations expectations, SavedFitting fitting)
    {
        ArgumentNullException.ThrowIfNull(fitting);
        try
        {
            return CheckFitting(expectations, fitting.Calculate());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
                                       or KeyNotFoundException)
        {
            return new RangeCheckResult(RangeCheckStatus.NotAvailable, Array.Empty<RangeFinding>(), ex.Message);
        }
    }

    private sealed record Comparison(RangeFinding? Finding, string? Problem);

    private static Comparison Compare(ExpectedRange? range, ExpectedQuantity quantity, double value, double? upperValue)
    {
        if (range is null || !range.IsSet)
            return new Comparison(null, null);
        if (range.Problem is string problem)
            return new Comparison(null, problem);
        if (!double.IsFinite(value) || upperValue is double upper && !double.IsFinite(upper))
            return new Comparison(null, "The calculated value is not a finite number.");
        var within = range.Contains(value) && (upperValue is not double band || range.Contains(band));
        return new Comparison(new RangeFinding(quantity, value, upperValue, range, within), null);
    }

    private static RangeCheckResult Combine(params Comparison[] comparisons)
    {
        var findings = comparisons.Where(x => x.Finding is not null).Select(x => x.Finding!).ToArray();
        var problem = comparisons.Select(x => x.Problem).FirstOrDefault(x => x is not null);
        if (problem is not null)
            return new RangeCheckResult(RangeCheckStatus.NotAvailable, findings, problem);
        if (findings.Length == 0)
            return new RangeCheckResult(RangeCheckStatus.NotSet, findings);
        return new RangeCheckResult(findings.All(x => x.Within) ? RangeCheckStatus.Within : RangeCheckStatus.Outside,
            findings);
    }
}
