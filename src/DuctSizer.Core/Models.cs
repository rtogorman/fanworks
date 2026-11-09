namespace DuctSizer.Core;

public enum UnitSystem
{
    Imperial,
    Metric
}

public enum DuctShape
{
    Round,
    Rectangular
}

public enum EquivalentDiameterMethod
{
    Huebscher,
    HydraulicDiameter
}

public enum PressureLossBasis
{
    EquivalentRoundArea,
    ActualDuctArea
}

public enum FlowRegime
{
    Laminar,
    Transitional,
    Turbulent
}

public enum CalculationMode
{
    CheckEnteredSize,
    SizeByTargetFriction,
    SizeByMaximumVelocity
}

public enum SizeRoundingDirection
{
    Automatic,
    Down
}

public enum RectangularFixedDimension
{
    None,
    Width,
    Height
}

public sealed record DuctMaterial(string Name, double RoughnessFeet, string Note = "")
{
    public double RoughnessMeters => RoughnessFeet * UnitConversions.FeetToMeters;
    public override string ToString() => Name;
}

/// <summary>Duct materials and their absolute roughness, as offered in the calculator and schedule.</summary>
/// <remarks>
/// The first entry, "Legacy Reference" (0.0007 ft), is the program default so results match the McQuay DesignTools
/// reference the project was validated against. It is rougher than the value ASHRAE Handbook - Fundamentals lists for
/// typical galvanized steel duct (0.0003 ft, "Galvanized Spiral Duct" here), so friction losses with the default are
/// somewhat conservative. The material is shown with every duct result and in the report.
/// </remarks>
public static class DuctMaterialCatalog
{
    /// <summary>All materials; the first is the default.</summary>
    public static IReadOnlyList<DuctMaterial> All { get; } = new[]
    {
        new DuctMaterial("Legacy Reference", 0.00070, "Uses the classic McQuay DesignTools reference roughness of 0.0007 ft."),
        new DuctMaterial("Galvanized Spiral Duct", 0.00030, "Typical new spiral galvanized duct."),
        new DuctMaterial("Galvanized Rectangular Duct", 0.00050, "Conservative value including typical seams and joints."),
        new DuctMaterial("Internally Fiber-Lined Rectangular Duct", 0.00500, "Assumes conventional faced air-side liner."),
        new DuctMaterial("HDPE - Smooth Interior", 0.000005, "Continuous smooth-wall HDPE; not corrugated HDPE."),
        new DuctMaterial("Stainless-Steel Duct", 0.00015, "Typical new stainless-steel duct."),
        new DuctMaterial("PVC Duct", 0.00015, "Conservative end of the ASHRAE PVC range."),
        new DuctMaterial("Aluminum Duct", 0.00020, "Conservative end of the ASHRAE aluminum range."),
        new DuctMaterial("Flexible HVAC Duct - Fully Extended", 0.00300, "Valid only for fully extended nonmetallic flex duct; compression requires a separate correction.")
    };
}

public sealed record AirState(
    double TemperatureC,
    double RelativeHumidity,
    double PressurePa,
    double DensityKgPerM3,
    double DynamicViscosityPaS,
    double KinematicViscosityM2PerS,
    double WaterVaporPartialPressurePa);

public sealed record DuctGeometry(
    DuctShape Shape,
    double DiameterM,
    double WidthM,
    double HeightM,
    double ActualAreaM2,
    double HydraulicDiameterM,
    double HuebscherDiameterM)
{
    public double AspectRatio => Shape == DuctShape.Round
        ? 1.0
        : Math.Min(WidthM, HeightM) / Math.Max(WidthM, HeightM);

    public double EquivalentDiameter(EquivalentDiameterMethod method) =>
        Shape == DuctShape.Round
            ? DiameterM
            : method == EquivalentDiameterMethod.Huebscher
                ? HuebscherDiameterM
                : HydraulicDiameterM;
}

public sealed record FrictionFactorResult(
    FlowRegime Regime,
    double? Factor,
    double LowerFactor,
    double UpperFactor,
    string Method,
    int Iterations = 0,
    double FinalResidual = 0.0,
    bool Converged = true,
    double InitialLowerBound = 0.0,
    double InitialUpperBound = 0.0);

public sealed record ColebrookSolveResult(
    double Factor,
    int Iterations,
    double FinalResidual,
    bool Converged,
    double InitialLowerBound,
    double InitialUpperBound);

public sealed record DuctCalculationInput(
    double AirflowM3PerS,
    double TemperatureC,
    double RelativeHumidity,
    double AtmosphericPressurePa,
    DuctGeometry Geometry,
    DuctMaterial Material,
    EquivalentDiameterMethod EquivalentDiameterMethod,
    PressureLossBasis PressureLossBasis,
    double LengthM);

public sealed record DuctCalculationResult(
    DuctCalculationInput Input,
    AirState Air,
    double EquivalentDiameterM,
    double EquivalentAreaM2,
    double ActualVelocityMPerS,
    double EquivalentVelocityMPerS,
    double CalculationVelocityMPerS,
    double CalculationDiameterM,
    double ReynoldsNumber,
    FrictionFactorResult Friction,
    double PressureLossPaPerM,
    double PressureLossLowerPaPerM,
    double PressureLossUpperPaPerM,
    double TotalPressureLossPa,
    double TotalPressureLossLowerPa,
    double TotalPressureLossUpperPa,
    string CalculationBasisDescription);

public sealed record DuctSizingInput(
    double AirflowM3PerS,
    double TemperatureC,
    double RelativeHumidity,
    double AtmosphericPressurePa,
    DuctShape Shape,
    double RectangularWidthToHeightRatio,
    DuctMaterial Material,
    EquivalentDiameterMethod EquivalentDiameterMethod,
    PressureLossBasis PressureLossBasis,
    double LengthM,
    CalculationMode Mode,
    double TargetFrictionPaPerM,
    double MaximumVelocityMPerS,
    double SizeIncrementM,
    SizeRoundingDirection RoundingDirection = SizeRoundingDirection.Automatic,
    RectangularFixedDimension FixedDimension = RectangularFixedDimension.None,
    double FixedDimensionM = 0.0);

public sealed record DuctSizingResult(
    DuctGeometry ContinuousGeometry,
    DuctGeometry RecommendedGeometry,
    DuctCalculationResult Calculation);

/// <summary>Unit conversions used at the UI and report boundaries (all calculations are SI internally).</summary>
/// <remarks>
/// Length, area and flow factors are exact definitions. 1 in. w.c. = 249.08891 Pa is the conventional inch of water
/// at 4 C (39.2 F). ASHRAE practice sometimes uses 248.84 Pa (water at 60 F); the two differ by about 0.1%, which is
/// below the precision of duct design inputs, and the factor is stated in the calculation report.
/// </remarks>
public static class UnitConversions
{
    public const double FeetToMeters = 0.3048;
    public const double InchesToMeters = 0.0254;
    public const double CubicFeetPerMinuteToCubicMetersPerSecond = 0.0004719474432;
    public const double FeetPerMinuteToMetersPerSecond = 0.00508;
    public const double InchesWaterToPascals = 249.08891;
    public const double SquareFeetToSquareMeters = 0.09290304;

    public static double FahrenheitToCelsius(double value) => (value - 32.0) * 5.0 / 9.0;
    public static double CelsiusToFahrenheit(double value) => value * 9.0 / 5.0 + 32.0;
    public static double CfmToM3PerS(double value) => value * CubicFeetPerMinuteToCubicMetersPerSecond;
    public static double M3PerSToCfm(double value) => value / CubicFeetPerMinuteToCubicMetersPerSecond;
    public static double LpsToM3PerS(double value) => value / 1000.0;
    public static double M3PerSToLps(double value) => value * 1000.0;
    public static double InchesToM(double value) => value * InchesToMeters;
    public static double MToInches(double value) => value / InchesToMeters;
    public static double MillimetersToM(double value) => value / 1000.0;
    public static double MToMillimeters(double value) => value * 1000.0;
    public static double FpmToMps(double value) => value * FeetPerMinuteToMetersPerSecond;
    public static double MpsToFpm(double value) => value / FeetPerMinuteToMetersPerSecond;
    public static double FeetToM(double value) => value * FeetToMeters;
    public static double MToFeet(double value) => value / FeetToMeters;
    public static double SquareMetersToSquareFeet(double value) => value / SquareFeetToSquareMeters;
    public static double PaToInchesWater(double value) => value / InchesWaterToPascals;
    public static double InchesWaterToPa(double value) => value * InchesWaterToPascals;
    public static double InchesWaterPer100FeetToPaPerM(double value) =>
        InchesWaterToPa(value) / (100.0 * FeetToMeters);
    public static double PaPerMToInchesWaterPer100Feet(double value) =>
        PaToInchesWater(value * 100.0 * FeetToMeters);
}
