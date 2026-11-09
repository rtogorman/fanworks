namespace DuctSizer.Core;

/// <summary>
/// Properties of moist air at a given temperature, relative humidity and barometric pressure.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Density: ideal-gas mixture, rho = (p - pv) / (R_da T) + pv / (R_v T), with R_da = 287.058 J/(kg K) and
/// R_v = 461.495 J/(kg K).</item>
/// <item>Vapor pressure: pv = RH x p_sat(t), with p_sat from the Buck (1981, revised 1996) equations over water
/// (t &gt;= 0 C) and over ice (t &lt; 0 C).</item>
/// <item>Dynamic viscosity: Sutherland's law with mu0 = 1.716e-5 Pa s at T0 = 273.15 K and S = 111 K (applied to dry
/// air; humidity's effect on viscosity is neglected). The often-quoted S = 110.4 K changes mu by less than 0.03%
/// between -10 C and 60 C.</item>
/// </list>
/// Defaults: RH = 50%, p = 101,325 Pa (standard sea-level pressure).
/// </remarks>
public static class AirProperties
{
    private const double DryAirGasConstant = 287.058;
    private const double WaterVaporGasConstant = 461.495;

    /// <summary>Calculates air density, viscosity and vapor pressure (see class remarks).</summary>
    /// <param name="temperatureC">Dry-bulb temperature (C).</param>
    /// <param name="relativeHumidity">Relative humidity as a fraction, 0 to 1.</param>
    /// <param name="pressurePa">Barometric pressure (Pa).</param>
    /// <exception cref="ArgumentOutOfRangeException">An input is out of range, or the vapor pressure would reach the total pressure.</exception>
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
