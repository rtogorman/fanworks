namespace DuctSizer.Core;

public static class FanPerformance
{
    private const double AirHorsepowerDenominator = 6356.0;

    public static double BrakeHorsepower(double airflowM3PerS, double staticPressurePa,
        double efficiencyPercent)
    {
        if (!double.IsFinite(airflowM3PerS) || airflowM3PerS <= 0.0
            || !double.IsFinite(staticPressurePa) || staticPressurePa <= 0.0
            || !double.IsFinite(efficiencyPercent) || efficiencyPercent <= 0.0)
            return 0.0;

        var airflowCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        var staticPressureInWc = UnitConversions.PaToInchesWater(staticPressurePa);
        var efficiency = Math.Clamp(efficiencyPercent / 100.0, 0.0001, 1.0);
        return airflowCfm * staticPressureInWc / (AirHorsepowerDenominator * efficiency);
    }
}
