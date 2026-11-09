namespace DuctSizer.Core;

public static class DuctSizingSolver
{
    public static DuctSizingResult Size(DuctSizingInput input)
    {
        ValidateInput(input);

        var continuousScalar = input.Mode switch
        {
            CalculationMode.SizeByMaximumVelocity => SizeForVelocity(input),
            CalculationMode.SizeByTargetFriction => SizeForFriction(input),
            _ => throw new ArgumentException("A sizing mode is required.", nameof(input))
        };

        var continuous = GeometryFromScalar(input, continuousScalar);
        var recommended = input.SizeIncrementM > 0.0
            ? RoundGeometry(continuous, input.SizeIncrementM, input.RoundingDirection, input.FixedDimension)
            : continuous;
        var calculation = DuctCalculator.Calculate(CreateCalculationInput(input, recommended));
        return new DuctSizingResult(continuous, recommended, calculation);
    }

    private static double SizeForVelocity(DuctSizingInput input)
    {
        if (input.Shape == DuctShape.Round)
            return Math.Sqrt(4.0 * input.AirflowM3PerS / (Math.PI * input.MaximumVelocityMPerS));

        return input.FixedDimension == RectangularFixedDimension.None
            ? Math.Sqrt(input.AirflowM3PerS
                        / (input.MaximumVelocityMPerS * input.RectangularWidthToHeightRatio))
            : input.AirflowM3PerS / (input.MaximumVelocityMPerS * input.FixedDimensionM);
    }

    private static double SizeForFriction(DuctSizingInput input)
    {
        double Loss(double scalar)
        {
            try
            {
                var geometry = GeometryFromScalar(input, scalar);
                var result = DuctCalculator.Calculate(CreateCalculationInput(input, geometry));
                return result.PressureLossUpperPaPerM;
            }
            catch (InvalidOperationException)
            {
                // Extremely small trial sizes can fall outside Colebrook's physical
                // roughness domain. They still represent the high-loss side of the
                // outer sizing bracket, so keep bisecting toward a valid size.
                return double.PositiveInfinity;
            }
        }

        var lower = 1e-5;
        var upper = 0.10;
        var lowerLoss = Loss(lower);
        var upperLoss = Loss(upper);

        for (var i = 0; i < 80 && lowerLoss < input.TargetFrictionPaPerM; i++)
        {
            lower *= 0.5;
            lowerLoss = Loss(lower);
        }

        for (var i = 0; i < 80 && upperLoss > input.TargetFrictionPaPerM; i++)
        {
            upper *= 2.0;
            upperLoss = Loss(upper);
        }

        if (lowerLoss < input.TargetFrictionPaPerM || upperLoss > input.TargetFrictionPaPerM)
            throw new InvalidOperationException("A stable duct-size bracket could not be found for the requested friction rate.");

        for (var iteration = 0; iteration < 256; iteration++)
        {
            var midpoint = 0.5 * (lower + upper);
            var midpointLoss = Loss(midpoint);

            if (Math.Abs(midpointLoss - input.TargetFrictionPaPerM)
                <= Math.Max(1e-10, input.TargetFrictionPaPerM * 1e-10)
                || upper - lower <= Math.Max(1e-10, midpoint * 1e-10))
                return midpoint;

            if (midpointLoss > input.TargetFrictionPaPerM)
                lower = midpoint;
            else
                upper = midpoint;
        }

        return 0.5 * (lower + upper);
    }

    private static DuctGeometry GeometryFromScalar(DuctSizingInput input, double scalarM)
    {
        if (input.Shape == DuctShape.Round)
            return GeometryCalculator.Round(scalarM);
        return input.FixedDimension switch
        {
            RectangularFixedDimension.Width =>
                GeometryCalculator.Rectangular(input.FixedDimensionM, scalarM),
            RectangularFixedDimension.Height =>
                GeometryCalculator.Rectangular(scalarM, input.FixedDimensionM),
            _ => GeometryCalculator.Rectangular(
                scalarM * input.RectangularWidthToHeightRatio, scalarM)
        };
    }

    private static DuctGeometry RoundGeometry(
        DuctGeometry geometry,
        double incrementM,
        SizeRoundingDirection direction,
        RectangularFixedDimension fixedDimension)
    {
        static double RoundDimension(double value, double increment, SizeRoundingDirection direction)
        {
            if (value < increment)
                return value;
            var scaled = value / increment;
            return direction == SizeRoundingDirection.Down
                ? Math.Floor(scaled + 1e-12) * increment
                : Math.Floor(scaled + 0.5 + 1e-12) * increment;
        }

        return geometry.Shape == DuctShape.Round
            ? GeometryCalculator.Round(RoundDimension(geometry.DiameterM, incrementM, direction))
            : GeometryCalculator.Rectangular(
                fixedDimension == RectangularFixedDimension.Width
                    ? geometry.WidthM
                    : RoundDimension(geometry.WidthM, incrementM, direction),
                fixedDimension == RectangularFixedDimension.Height
                    ? geometry.HeightM
                    : RoundDimension(geometry.HeightM, incrementM, direction));
    }

    private static DuctCalculationInput CreateCalculationInput(DuctSizingInput input, DuctGeometry geometry) =>
        new(
            input.AirflowM3PerS,
            input.TemperatureC,
            input.RelativeHumidity,
            input.AtmosphericPressurePa,
            geometry,
            input.Material,
            input.EquivalentDiameterMethod,
            input.PressureLossBasis,
            input.LengthM);

    private static void ValidateInput(DuctSizingInput input)
    {
        if (!double.IsFinite(input.AirflowM3PerS) || input.AirflowM3PerS <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(input.AirflowM3PerS), "Airflow must be positive.");
        if (!Enum.IsDefined(input.FixedDimension))
            throw new ArgumentOutOfRangeException(nameof(input.FixedDimension), "Select Width or Height for rectangular sizing.");
        if (input.Shape == DuctShape.Rectangular
            && input.FixedDimension != RectangularFixedDimension.None
            && (!double.IsFinite(input.FixedDimensionM) || input.FixedDimensionM <= 0.0))
            throw new ArgumentOutOfRangeException(nameof(input.FixedDimensionM), "The selected rectangular dimension must be positive.");
        if (input.FixedDimension == RectangularFixedDimension.None
            && (!double.IsFinite(input.RectangularWidthToHeightRatio)
            || input.RectangularWidthToHeightRatio < 1.0
            || input.RectangularWidthToHeightRatio > 100.0))
            throw new ArgumentOutOfRangeException(nameof(input.RectangularWidthToHeightRatio), "Rectangular width-to-height ratio must be from 1 to 100.");
        if (!double.IsFinite(input.SizeIncrementM) || input.SizeIncrementM < 0.0)
            throw new ArgumentOutOfRangeException(nameof(input.SizeIncrementM), "Size increment cannot be negative.");
        if (!Enum.IsDefined(input.RoundingDirection))
            throw new ArgumentOutOfRangeException(nameof(input.RoundingDirection), "Select a valid rounding direction.");
        if (input.Mode == CalculationMode.SizeByTargetFriction
            && (!double.IsFinite(input.TargetFrictionPaPerM) || input.TargetFrictionPaPerM <= 0.0))
            throw new ArgumentOutOfRangeException(nameof(input.TargetFrictionPaPerM), "Target friction must be positive.");
        if (input.Mode == CalculationMode.SizeByMaximumVelocity
            && (!double.IsFinite(input.MaximumVelocityMPerS) || input.MaximumVelocityMPerS <= 0.0))
            throw new ArgumentOutOfRangeException(nameof(input.MaximumVelocityMPerS), "Maximum velocity must be positive.");
    }
}
