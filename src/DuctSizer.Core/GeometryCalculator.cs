namespace DuctSizer.Core;

public static class GeometryCalculator
{
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
