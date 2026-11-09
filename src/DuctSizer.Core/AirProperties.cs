namespace DuctSizer.Core;

public static class AirProperties
{
    private const double DryAirGasConstant = 287.058;
    private const double WaterVaporGasConstant = 461.495;

    public static AirState Calculate(
        double temperatureC,
        double relativeHumidity = 0.50,
        double pressurePa = 101_325.0)
    {
        if (!double.IsFinite(temperatureC) || temperatureC <= -273.15)
            throw new ArgumentOutOfRangeException(nameof(temperatureC), "Air temperature must be above absolute zero.");
        if (!double.IsFinite(relativeHumidity) || relativeHumidity < 0.0 || relativeHumidity > 1.0)
            throw new ArgumentOutOfRangeException(nameof(relativeHumidity), "Relative humidity must be between 0 and 100 percent.");
        if (!double.IsFinite(pressurePa) || pressurePa <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(pressurePa), "Atmospheric pressure must be positive.");

        var temperatureK = temperatureC + 273.15;
        var saturationPressure = SaturationVaporPressurePa(temperatureC);
        var vaporPressure = relativeHumidity * saturationPressure;

        if (!double.IsFinite(vaporPressure) || vaporPressure >= pressurePa)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperatureC),
                "The selected temperature and fixed relative humidity are not physically possible at the assumed atmospheric pressure.");
        }

        var dryAirPressure = pressurePa - vaporPressure;
        var density = dryAirPressure / (DryAirGasConstant * temperatureK)
                    + vaporPressure / (WaterVaporGasConstant * temperatureK);

        // Sutherland's law for dynamic viscosity of air.
        const double referenceViscosity = 1.716e-5;
        const double referenceTemperatureK = 273.15;
        const double sutherlandConstantK = 111.0;
        var viscosity = referenceViscosity
                        * Math.Pow(temperatureK / referenceTemperatureK, 1.5)
                        * (referenceTemperatureK + sutherlandConstantK)
                        / (temperatureK + sutherlandConstantK);

        if (!double.IsFinite(density) || density <= 0.0 || !double.IsFinite(viscosity) || viscosity <= 0.0)
            throw new InvalidOperationException("Air properties could not be calculated for the supplied conditions.");

        return new AirState(
            temperatureC,
            relativeHumidity,
            pressurePa,
            density,
            viscosity,
            viscosity / density,
            vaporPressure);
    }

    private static double SaturationVaporPressurePa(double temperatureC)
    {
        // Buck equations over water and ice. Suitable for HVAC psychrometric conditions.
        return temperatureC >= 0.0
            ? 611.21 * Math.Exp((18.678 - temperatureC / 234.5) * (temperatureC / (257.14 + temperatureC)))
            : 611.15 * Math.Exp((23.036 - temperatureC / 333.7) * (temperatureC / (279.82 + temperatureC)));
    }
}
