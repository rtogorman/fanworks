namespace DuctSizer.Core;

/// <summary>Cross-section properties of round and rectangular ducts.</summary>
/// <remarks>
/// Rectangular duct: area A = W H; hydraulic diameter Dh = 4A / P = 2 W H / (W + H); equivalent round diameter
/// (Huebscher, 1948) De = 1.30 (W H)^0.625 / (W + H)^0.25, the duct of equal friction loss at equal airflow used in
/// the ASHRAE Duct Design chapter.
/// </remarks>
public static class GeometryCalculator
{
    /// <summary>Round duct of diameter <paramref name="diameterM"/> (m).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The diameter is not a positive finite value.</exception>
    public static DuctGeometry Round(double diameterM)
    {
        ValidateLength(diameterM, nameof(diameterM));
        var area = Math.PI * diameterM * diameterM / 4.0;
        return new DuctGeometry(
            DuctShape.Round,
            diameterM,
            0.0,
            0.0,
            area,
            diameterM,
            diameterM);
    }

    /// <summary>Rectangular duct of <paramref name="widthM"/> by <paramref name="heightM"/> (m).</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not a positive finite value.</exception>
    public static DuctGeometry Rectangular(double widthM, double heightM)
    {
        ValidateLength(widthM, nameof(widthM));
        ValidateLength(heightM, nameof(heightM));

        var area = widthM * heightM;
        var hydraulicDiameter = 2.0 * widthM * heightM / (widthM + heightM);
        var huebscherDiameter = 1.30
            * Math.Pow(widthM * heightM, 0.625)
            / Math.Pow(widthM + heightM, 0.25);

        return new DuctGeometry(
            DuctShape.Rectangular,
            0.0,
            widthM,
            heightM,
            area,
            hydraulicDiameter,
            huebscherDiameter);
    }

    private static void ValidateLength(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(name, "Duct dimensions must be positive finite values.");
    }
}
