namespace DuctSizer.Core;

/// <summary>
/// Friction pressure loss of a straight duct (Darcy-Weisbach).
/// </summary>
/// <remarks>
/// <para>dp/L = f / D x rho V^2 / 2, total loss = dp/L x L. Air properties come from <see cref="AirProperties"/> and f
/// from <see cref="FrictionModel"/>.</para>
/// <para>Velocity and diameter basis:</para>
/// <list type="bullet">
/// <item>Round duct: actual area, actual velocity, actual diameter.</item>
/// <item>Rectangular duct, <see cref="PressureLossBasis.EquivalentRoundArea"/> (default): the equivalent-round diameter
/// (Huebscher by default, see <see cref="GeometryCalculator"/>), its area and the corresponding velocity; round-duct
/// friction. This is the ASHRAE Duct Design chapter practice.</item>
/// <item>Rectangular duct, <see cref="PressureLossBasis.ActualDuctArea"/>: actual area and velocity with the hydraulic
/// diameter 4A/P and the rectangular laminar correlation.</item>
/// </list>
/// <para>Transitional flow returns lower and upper bounds; the headline rate and total are the upper bound.</para>
/// </remarks>
public static class DuctCalculator
{
    /// <summary>Calculates the friction loss of one duct run (see class remarks).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Airflow or length is not positive, or the geometry has no area.</exception>
    /// <exception cref="InvalidOperationException">A result is outside the supported numerical range.</exception>
    public static DuctCalculationResult Calculate(DuctCalculationInput input)
    {
        ValidateInput(input);

        var air = AirProperties.Calculate(
            input.TemperatureC,
            input.RelativeHumidity,
            input.AtmosphericPressurePa);

        var geometry = input.Geometry;
        var equivalentDiameter = geometry.EquivalentDiameter(input.EquivalentDiameterMethod);
        var equivalentArea = Math.PI * equivalentDiameter * equivalentDiameter / 4.0;
        var actualVelocity = input.AirflowM3PerS / geometry.ActualAreaM2;
        var equivalentVelocity = input.AirflowM3PerS / equivalentArea;

        double calculationVelocity;
        double calculationDiameter;
        DuctShape frictionShape;
        string basisDescription;

        if (geometry.Shape == DuctShape.Rectangular
            && input.PressureLossBasis == PressureLossBasis.ActualDuctArea)
        {
            calculationVelocity = actualVelocity;
            calculationDiameter = geometry.HydraulicDiameterM;
            frictionShape = DuctShape.Rectangular;
            basisDescription = "Actual rectangular area, actual velocity, and hydraulic diameter";
        }
        else
        {
            calculationVelocity = equivalentVelocity;
            calculationDiameter = equivalentDiameter;
            frictionShape = DuctShape.Round;
            basisDescription = geometry.Shape == DuctShape.Round
                ? "Actual round area and diameter"
                : "Equivalent-round area and velocity";
        }

        var reynolds = air.DensityKgPerM3 * calculationVelocity * calculationDiameter
                       / air.DynamicViscosityPaS;
        var friction = FrictionModel.Calculate(
            reynolds,
            input.Material.RoughnessMeters,
            calculationDiameter,
            frictionShape,
            geometry.AspectRatio);

        var velocityPressure = air.DensityKgPerM3 * calculationVelocity * calculationVelocity / 2.0;
        var lowerRate = friction.LowerFactor / calculationDiameter * velocityPressure;
        var upperRate = friction.UpperFactor / calculationDiameter * velocityPressure;
        // Transitional flow (2300 < Re <= 4000) has no single accepted friction factor. The laminar and turbulent
        // results are reported as bounds, and the headline rate is the conservative upper bound so that the
        // calculator, the system schedule and the critical-path total always agree. No blended value is used.
        var rate = friction.Factor.HasValue
            ? friction.Factor.Value / calculationDiameter * velocityPressure
            : Math.Max(lowerRate, upperRate);

        EnsureFinitePositive(rate, "pressure loss");
        EnsureFinitePositive(lowerRate, "lower pressure-loss bound");
        EnsureFinitePositive(upperRate, "upper pressure-loss bound");

        return new DuctCalculationResult(
            input,
            air,
            equivalentDiameter,
            equivalentArea,
            actualVelocity,
            equivalentVelocity,
            calculationVelocity,
            calculationDiameter,
            reynolds,
            friction,
            rate,
            Math.Min(lowerRate, upperRate),
            Math.Max(lowerRate, upperRate),
            rate * input.LengthM,
            Math.Min(lowerRate, upperRate) * input.LengthM,
            Math.Max(lowerRate, upperRate) * input.LengthM,
            basisDescription);
    }

    private static void ValidateInput(DuctCalculationInput input)
    {
        if (!double.IsFinite(input.AirflowM3PerS) || input.AirflowM3PerS <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(input.AirflowM3PerS), "Airflow must be positive.");
        if (!double.IsFinite(input.LengthM) || input.LengthM <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(input.LengthM), "Duct length must be positive.");
        if (input.Geometry.ActualAreaM2 <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(input.Geometry), "Duct geometry must have a positive area.");
    }

    private static void EnsureFinitePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new InvalidOperationException($"The calculated {name} is outside the supported numerical range.");
    }
}
