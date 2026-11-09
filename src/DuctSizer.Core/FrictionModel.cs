namespace DuctSizer.Core;

public static class FrictionModel
{
    public const double LaminarUpperReynolds = 2300.0;
    public const double TurbulentLowerReynolds = 4000.0;

    public static FrictionFactorResult Calculate(
        double reynoldsNumber,
        double absoluteRoughnessM,
        double characteristicDiameterM,
        DuctShape physicalShape,
        double rectangularAspectRatio)
    {
        if (!double.IsFinite(reynoldsNumber) || reynoldsNumber <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(reynoldsNumber), "Reynolds number must be positive.");
        if (!double.IsFinite(absoluteRoughnessM) || absoluteRoughnessM < 0.0)
            throw new ArgumentOutOfRangeException(nameof(absoluteRoughnessM), "Absolute roughness cannot be negative.");
        if (!double.IsFinite(characteristicDiameterM) || characteristicDiameterM <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(characteristicDiameterM), "Characteristic diameter must be positive.");

        var laminar = LaminarDarcyFactor(reynoldsNumber, physicalShape, rectangularAspectRatio);

        if (reynoldsNumber < LaminarUpperReynolds)
        {
            var method = physicalShape == DuctShape.Rectangular
                ? "Shah-London rectangular laminar correlation"
                : "Hagen-Poiseuille (64/Re)";
            return new FrictionFactorResult(FlowRegime.Laminar, laminar, laminar, laminar, method);
        }

        var colebrook = SolveColebrookDetailed(reynoldsNumber, absoluteRoughnessM, characteristicDiameterM);
        var turbulent = colebrook.Factor;

        if (reynoldsNumber <= TurbulentLowerReynolds)
        {
            return new FrictionFactorResult(
                FlowRegime.Transitional,
                null,
                Math.Min(laminar, turbulent),
                Math.Max(laminar, turbulent),
                "Unblended laminar and iterative Colebrook bounds",
                colebrook.Iterations,
                colebrook.FinalResidual,
                colebrook.Converged,
                colebrook.InitialLowerBound,
                colebrook.InitialUpperBound);
        }

        return new FrictionFactorResult(
            FlowRegime.Turbulent,
            turbulent,
            turbulent,
            turbulent,
            "Implicit Colebrook solved by safeguarded bisection",
            colebrook.Iterations,
            colebrook.FinalResidual,
            colebrook.Converged,
            colebrook.InitialLowerBound,
            colebrook.InitialUpperBound);
    }

    public static double LaminarDarcyFactor(
        double reynoldsNumber,
        DuctShape physicalShape,
        double rectangularAspectRatio)
    {
        if (physicalShape == DuctShape.Round)
            return 64.0 / reynoldsNumber;

        var alpha = Math.Clamp(rectangularAspectRatio, 0.0, 1.0);
        var alpha2 = alpha * alpha;
        var alpha3 = alpha2 * alpha;
        var alpha4 = alpha3 * alpha;
        var alpha5 = alpha4 * alpha;
        var coefficient = 1.0
                          - 1.3553 * alpha
                          + 1.9467 * alpha2
                          - 1.7012 * alpha3
                          + 0.9564 * alpha4
                          - 0.2537 * alpha5;
        return 96.0 * coefficient / reynoldsNumber;
    }

    public static double SolveColebrook(
        double reynoldsNumber,
        double absoluteRoughnessM,
        double characteristicDiameterM) =>
        SolveColebrookDetailed(reynoldsNumber, absoluteRoughnessM, characteristicDiameterM).Factor;

    public static ColebrookSolveResult SolveColebrookDetailed(
        double reynoldsNumber,
        double absoluteRoughnessM,
        double characteristicDiameterM)
    {
        if (reynoldsNumber <= 0.0 || characteristicDiameterM <= 0.0 || absoluteRoughnessM < 0.0)
            throw new ArgumentOutOfRangeException(nameof(reynoldsNumber), "Colebrook inputs must be physically valid.");

        static double Residual(double factor, double reynolds, double relativeRoughness)
        {
            var root = Math.Sqrt(factor);
            var argument = relativeRoughness / 3.7 + 2.51 / (reynolds * root);
            if (argument <= 0.0 || !double.IsFinite(argument))
                return double.NaN;
            return 1.0 / root + 2.0 * Math.Log10(argument);
        }

        var relativeRoughness = absoluteRoughnessM / characteristicDiameterM;
        var lower = 1e-8;
        var upper = 1.0;
        var lowerResidual = Residual(lower, reynoldsNumber, relativeRoughness);
        var upperResidual = Residual(upper, reynoldsNumber, relativeRoughness);

        while (double.IsFinite(upperResidual) && Math.Sign(lowerResidual) == Math.Sign(upperResidual) && upper < 256.0)
        {
            upper *= 2.0;
            upperResidual = Residual(upper, reynoldsNumber, relativeRoughness);
        }

        if (!double.IsFinite(lowerResidual) || !double.IsFinite(upperResidual)
            || Math.Sign(lowerResidual) == Math.Sign(upperResidual))
        {
            throw new InvalidOperationException(
                "The Colebrook equation could not be bracketed. Check the duct size and roughness selection.");
        }

        var initialLower = lower;
        var initialUpper = upper;

        for (var iteration = 0; iteration < 256; iteration++)
        {
            var midpoint = 0.5 * (lower + upper);
            var midpointResidual = Residual(midpoint, reynoldsNumber, relativeRoughness);
            if (!double.IsFinite(midpointResidual))
                throw new InvalidOperationException("The Colebrook calculation produced a non-finite value.");

            if (Math.Abs(midpointResidual) <= 1e-13
                || (upper - lower) <= Math.Max(1e-14, midpoint * 1e-12))
                return new ColebrookSolveResult(
                    midpoint, iteration + 1, midpointResidual, true, initialLower, initialUpper);

            if (Math.Sign(midpointResidual) == Math.Sign(lowerResidual))
            {
                lower = midpoint;
                lowerResidual = midpointResidual;
            }
            else
            {
                upper = midpoint;
                upperResidual = midpointResidual;
            }
        }

        var factor = 0.5 * (lower + upper);
        return new ColebrookSolveResult(
            factor, 256, Residual(factor, reynoldsNumber, relativeRoughness), false,
            initialLower, initialUpper);
    }
}
