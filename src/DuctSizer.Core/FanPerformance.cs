namespace DuctSizer.Core;

/// <summary>Fan power from the fan law for air horsepower.</summary>
public static class FanPerformance
{
    /// <summary>
    /// Conversion constant for air horsepower in US units: hp = cfm x in. w.c. / 6356
    /// (ASHRAE Handbook, Fundamentals, Fans chapter).
    /// </summary>
    private const double AirHorsepowerDenominator = 6356.0;

    /// <summary>
    /// Brake horsepower = cfm x static pressure (in. w.c.) / (6356 x efficiency).
    /// Zero airflow or zero static pressure gives zero horsepower.
    /// </summary>
    /// <param name="airflowM3PerS">Fan airflow, m3/s. Must be finite and not negative.</param>
    /// <param name="staticPressurePa">Fan static pressure, Pa. Must be finite and not negative.</param>
    /// <param name="efficiencyPercent">Fan static efficiency, percent. Must be greater than 0 and at most 100.</param>
    /// <exception cref="ArgumentOutOfRangeException">An input is outside its valid range.</exception>
    public static double BrakeHorsepower(double airflowM3PerS, double staticPressurePa,
        double efficiencyPercent)
    {
        if (!double.IsFinite(airflowM3PerS) || airflowM3PerS < 0.0)
            throw new ArgumentOutOfRangeException(nameof(airflowM3PerS), "Fan airflow must be zero or positive.");
        if (!double.IsFinite(staticPressurePa) || staticPressurePa < 0.0)
            throw new ArgumentOutOfRangeException(nameof(staticPressurePa), "Fan static pressure must be zero or positive.");
        if (!double.IsFinite(efficiencyPercent) || efficiencyPercent <= 0.0 || efficiencyPercent > 100.0)
            throw new ArgumentOutOfRangeException(nameof(efficiencyPercent), "Fan efficiency must be greater than 0% and no more than 100%.");

        var airflowCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        var staticPressureInWc = UnitConversions.PaToInchesWater(staticPressurePa);
        return airflowCfm * staticPressureInWc / (AirHorsepowerDenominator * efficiencyPercent / 100.0);
    }
}
