using System.Globalization;
using System.Reflection;

namespace DuctSizer.Core;

public sealed record GrdPerformancePoint(
    double AirflowCfm,
    double TotalPressureInWg,
    string SoundNc,
    string ThrowFt,
    double? StaticPressureInWg = null,
    double? CoreVelocityFpm = null,
    double? VelocityPressureInWg = null);

public sealed record GrdPerformanceSelection(
    string Product,
    string Configuration,
    double RequestedAirflowCfm,
    GrdPerformancePoint Selected,
    IReadOnlyList<GrdPerformancePoint> PublishedTable,
    int? InletDiameterIn = null,
    int? SlotCount = null,
    double? SlotWidthIn = null,
    int? LengthFt = null,
    string SourceReference = "",
    string NominalDuctSizeIn = "",
    double? NominalDuctAreaFt2 = null,
    double? CoreAreaFt2 = null)
{
    public bool AirflowWasRoundedUp => Selected.AirflowCfm > RequestedAirflowCfm + 1e-9;
}

public readonly record struct TitusTbd80Configuration(
    int InletDiameterIn,
    int SlotCount,
    double SlotWidthIn,
    int LengthFt);

public readonly record struct Titus350RConfiguration(
    string NominalDuctSizeIn,
    double NominalDuctAreaFt2,
    double CoreAreaFt2,
    int SourcePage);

/// <summary>
/// Manufacturer-published GRD performance data. Selection is conservative: the requested
/// airflow is rounded upward to the first published airflow value for the chosen configuration.
/// </summary>
public static class GrdPerformanceCatalog
{
    public const string PriceScdCode = "GRD-DIFFUSER";
    public const string TitusTbd80Code = "GRD-LINEAR-DIFFUSER";
    public const string Titus350RCode = "GRD-GRILLE";
    public const string TitusTbd80Reference =
        "Performance data courtesy of Titus, TBD-80 D Plenum Slot Diffusers (2019).";
    public const string Titus350RReference =
        "Performance data courtesy of Titus, 350R / 350F / 350R-SS Grilles (2017).";

    private static readonly IReadOnlyDictionary<(int Face, int Neck), GrdPerformancePoint[]> PriceScd =
        new Dictionary<(int, int), GrdPerformancePoint[]>
        {
            [(12, 4)] = P("35,.013,-,1-2-4|44,.021,-,1-2-4|52,.030,-,2-3-5|61,.041,-,2-3-6|70,.054,-,2-4-6|78,.068,-,3-4-7|87,.084,-,3-4-7|104,.121,20,4-5-8|122,.165,25,4-6-9|139,.215,29,5-6-9"),
            [(12, 5)] = P("54,.015,-,2-2-5|68,.024,-,2-3-6|82,.035,-,2-4-7|95,.047,-,3-4-8|109,.062,-,3-5-8|122,.078,16,4-5-9|136,.097,19,4-6-9|163,.139,25,5-7-10|190,.189,30,5-8-11|218,.247,34,6-8-11"),
            [(12, 6)] = P("78,.018,-,2-3-6|98,.028,-,2-4-7|118,.040,-,3-4-8|137,.055,-,3-5-9|157,.072,16,4-6-10|176,.091,20,4-7-10|196,.112,23,5-7-11|235,.162,29,6-8-12|274,.220,34,7-9-13|314,.287,38,8-10-14"),
            [(12, 7)] = P("107,.022,-,2-4-7|134,.035,-,3-4-9|160,.050,-,4-5-10|187,.069,15,4-6-11|214,.090,19,5-7-11|240,.114,23,5-8-12|267,.140,26,6-9-13|320,.202,32,7-10-14|374,.275,37,8-11-15|427,.359,41,9-11-16"),
            [(12, 8)] = P("140,.029,-,3-4-8|175,.045,-,3-5-10|209,.065,-,4-6-11|244,.089,18,5-7-12|279,.116,22,6-8-13|314,.146,26,6-9-14|349,.181,29,7-10-15|419,.260,35,8-11-16|489,.354,40,10-12-17|558,.463,44,11-13-18"),

            [(20, 6)] = P("78,.013,-,0-1-3|98,.020,-,1-2-4|118,.029,-,1-2-4|137,.040,16,1-3-5|157,.052,19,2-3-6|176,.066,23,2-3-6|196,.081,26,2-4-6|235,.117,31,3-4-7|274,.159,35,3-5-7|314,.207,39,4-6-8"),
            [(20, 8)] = P("140,.017,-,1-2-5|175,.026,-,2-3-6|209,.038,-,2-4-6|244,.052,18,3-4-7|279,.068,22,3-5-7|314,.086,25,4-5-8|349,.106,28,4-6-8|419,.153,33,5-6-9|489,.208,37,6-7-10|558,.271,41,6-7-11"),
            [(20, 10)] = P("218,.022,-,2-3-6|273,.034,-,3-4-7|327,.049,15,3-5-8|382,.067,20,4-6-9|436,.088,23,4-6-9|491,.111,27,5-7-10|545,.137,30,5-7-10|654,.198,35,6-8-11|763,.269,39,7-9-12|872,.351,43,8-9-13"),

            [(24, 6)] = P("78,.015,-,1-2-4|98,.023,-,1-2-4|118,.034,-,2-3-5|137,.046,-,2-3-6|157,.060,15,2-4-7|176,.076,19,3-4-7|196,.094,22,3-4-7|235,.135,28,4-5-8|274,.183,33,4-6-9|314,.239,37,5-7-9"),
            [(24, 8)] = P("140,.016,-,2-2-5|175,.025,-,2-3-6|209,.037,-,2-4-7|244,.050,-,3-4-8|279,.065,19,3-5-9|314,.082,22,4-6-9|349,.102,26,4-6-10|419,.146,31,5-7-11|489,.199,36,6-8-12|558,.260,40,7-9-12"),
            [(24, 10)] = P("218,.019,-,2-3-6|273,.030,-,3-4-8|327,.044,-,3-5-9|382,.060,17,4-6-10|436,.078,21,4-6-11|491,.098,25,5-7-12|545,.122,28,5-8-12|654,.175,34,6-9-13|763,.238,39,8-10-14|872,.311,43,9-11-15"),
            [(24, 12)] = P("314,.023,-,3-4-8|393,.036,-,3-5-10|471,.051,-,4-6-11|550,.070,19,5-7-12|628,.091,24,5-8-13|707,.115,27,6-9-14|785,.142,30,7-10-15|942,.205,36,8-11-16|1099,.279,41,9-12-17|1256,.364,45,11-13-19"),
            [(24, 14)] = P("428,.026,-,3-5-10|535,.041,-,4-6-12|641,.058,16,5-7-13|748,.079,21,6-9-14|855,.104,25,6-10-15|962,.131,29,7-11-16|1069,.162,32,8-12-17|1283,.233,38,10-13-19|1497,.318,43,11-14-20|1710,.415,47,12-15-22"),
            [(24, 15)] = P("491,.028,-,4-5-11|614,.044,-,4-7-13|736,.064,17,5-8-14|859,.087,22,6-9-15|982,.114,26,7-11-16|1104,.144,30,8-12-17|1227,.178,33,9-13-18|1472,.256,39,11-14-20|1718,.348,43,12-15-22|1963,.455,47,13-16-23")
        };

    private static readonly IReadOnlyDictionary<TitusTbd80Configuration, GrdPerformancePoint[]> TitusTbd80 =
        LoadTitusTbd80();
    private static readonly IReadOnlyDictionary<Titus350RConfiguration, GrdPerformancePoint[]> Titus350R =
        LoadTitus350R();

    public static IReadOnlyList<int> PriceFaceSizes { get; } = new[] { 12, 20, 24 };
    public static IReadOnlyList<int> TitusLengthsFt { get; } = new[] { 2, 4, 5 };
    public static IReadOnlyList<double> TitusSlotWidthsIn { get; } = new[] { 0.75, 1.0, 1.5 };
    public static IReadOnlyList<int> TitusSlotCounts { get; } = new[] { 1, 2, 3, 4 };
    public static IReadOnlyList<int> TitusInletDiametersIn { get; } = new[] { 6, 8, 10, 12 };
    public static IReadOnlyList<TitusTbd80Configuration> TitusConfigurations { get; } = TitusTbd80.Keys
        .OrderBy(value => value.SlotCount).ThenBy(value => value.InletDiameterIn)
        .ThenBy(value => value.SlotWidthIn).ThenBy(value => value.LengthFt).ToArray();
    public static IReadOnlyList<Titus350RConfiguration> Titus350RConfigurations { get; } = Titus350R.Keys
        .OrderBy(value => value.NominalDuctAreaFt2).ThenBy(value => value.NominalDuctSizeIn,
            StringComparer.OrdinalIgnoreCase).ToArray();
    public static IReadOnlyList<string> Titus350RSizes { get; } = Titus350RConfigurations
        .Select(value => value.NominalDuctSizeIn).ToArray();

    public static IReadOnlyList<int> PriceNeckDiameters(int faceSizeIn) => PriceScd.Keys
        .Where(key => key.Face == faceSizeIn).Select(key => key.Neck).OrderBy(value => value).ToArray();

    public static GrdPerformanceSelection SelectPriceScd(double requestedCfm, int faceSizeIn, int neckDiameterIn)
    {
        if (!PriceScd.TryGetValue((faceSizeIn, neckDiameterIn), out var table))
            throw new ArgumentException($"No Price SCD table is published for a {faceSizeIn} x {faceSizeIn} in face with a {neckDiameterIn} in neck.");
        return Select("Price SCD square cone diffuser", $"{faceSizeIn} x {faceSizeIn} in face; {neckDiameterIn} in neck",
            requestedCfm, table);
    }

    public static GrdPerformanceSelection SelectTitus350R(double requestedCfm, string nominalDuctSizeIn)
    {
        var matching = Titus350R.FirstOrDefault(pair => string.Equals(pair.Key.NominalDuctSizeIn,
            nominalDuctSizeIn, StringComparison.OrdinalIgnoreCase));
        if (matching.Value is null)
            throw new ArgumentException($"No Titus 350R table is published for nominal duct size {nominalDuctSizeIn} in.");
        var selection = Select("Titus 350R grille",
            $"{matching.Key.NominalDuctSizeIn} in nominal duct; {matching.Key.CoreAreaFt2:0.##} ft² core area",
            requestedCfm, matching.Value);
        return selection with
        {
            SourceReference = Titus350RReference,
            NominalDuctSizeIn = matching.Key.NominalDuctSizeIn,
            NominalDuctAreaFt2 = matching.Key.NominalDuctAreaFt2,
            CoreAreaFt2 = matching.Key.CoreAreaFt2
        };
    }

    public static GrdPerformanceSelection AutoSelectTitus350R(double requestedCfm, string preferredNominalSizeIn) =>
        SelectTitus350R(requestedCfm, preferredNominalSizeIn);

    public static int Titus350RSizeIndex(string nominalDuctSizeIn)
    {
        var defaultIndex = 0;
        for (var index = 0; index < Titus350RSizes.Count; index++)
        {
            if (string.Equals(Titus350RSizes[index], "12x12", StringComparison.OrdinalIgnoreCase))
                defaultIndex = index;
            if (string.Equals(Titus350RSizes[index], nominalDuctSizeIn, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return defaultIndex;
    }

    public static GrdPerformanceSelection SelectTitusTbd80(double requestedCfm, int lengthFt) =>
        SelectTitusTbd80(requestedCfm, lengthFt, 0.75, 1);

    public static GrdPerformanceSelection SelectTitusTbd80(double requestedCfm, int lengthFt,
        double slotWidthIn, int slotCount)
    {
        if (!TitusLengthsFt.Contains(lengthFt))
            throw new ArgumentException("Titus TBD-80 D length must be 2 ft, 4 ft, or 5 ft.");
        if (!TitusSlotWidthsIn.Any(value => Math.Abs(value - slotWidthIn) < 1e-9))
            throw new ArgumentException("Titus TBD-80 D slot width must be 3/4 in, 1 in, or 1-1/2 in.");
        if (!TitusSlotCounts.Contains(slotCount))
            throw new ArgumentException("Titus TBD-80 D must have between one and four slots.");
        if (!double.IsFinite(requestedCfm) || requestedCfm <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedCfm), "Airflow must be greater than zero.");

        var matching = TitusTbd80
            .Where(pair => pair.Key.LengthFt == lengthFt
                           && pair.Key.SlotCount == slotCount
                           && Math.Abs(pair.Key.SlotWidthIn - slotWidthIn) < 1e-9)
            .OrderBy(pair => pair.Key.InletDiameterIn)
            .ToArray();
        var selectedTable = matching.FirstOrDefault(pair => pair.Value[^1].AirflowCfm + 1e-9 >= requestedCfm);
        if (selectedTable.Value is null)
        {
            var maximum = matching.Length == 0 ? 0 : matching.Max(pair => pair.Value[^1].AirflowCfm);
            throw new ArgumentOutOfRangeException(nameof(requestedCfm),
                $"Requested airflow exceeds the published maximum of {maximum:0} cfm for the selected Titus TBD-80 D length, slot width, and slot count.");
        }

        var widthText = slotWidthIn switch
        {
            0.75 => "3/4 in",
            1.5 => "1-1/2 in",
            _ => "1 in"
        };
        var selection = Select("Titus TBD-80 D linear diffuser",
            $"{selectedTable.Key.InletDiameterIn} in inlet; {slotCount} slot{(slotCount == 1 ? "" : "s")}; {widthText} slot width; {lengthFt} ft length",
            requestedCfm, selectedTable.Value);
        return selection with
        {
            InletDiameterIn = selectedTable.Key.InletDiameterIn,
            SlotCount = slotCount,
            SlotWidthIn = slotWidthIn,
            LengthFt = lengthFt,
            SourceReference = TitusTbd80Reference
        };
    }

    public static IReadOnlyList<GrdPerformancePoint> PriceTable(int faceSizeIn, int neckDiameterIn) =>
        PriceScd.TryGetValue((faceSizeIn, neckDiameterIn), out var table) ? table : Array.Empty<GrdPerformancePoint>();

    public static IReadOnlyList<GrdPerformancePoint> TitusTable(int lengthFt) =>
        TitusTable(lengthFt, 0.75, 1, 6);

    public static IReadOnlyList<GrdPerformancePoint> TitusTable(int lengthFt, double slotWidthIn, int slotCount,
        int inletDiameterIn) =>
        TitusTbd80.TryGetValue(new TitusTbd80Configuration(inletDiameterIn, slotCount, slotWidthIn, lengthFt),
            out var table) ? table : Array.Empty<GrdPerformancePoint>();

    public static IReadOnlyList<GrdPerformancePoint> Titus350RTable(string nominalDuctSizeIn) =>
        Titus350R.FirstOrDefault(pair => string.Equals(pair.Key.NominalDuctSizeIn,
            nominalDuctSizeIn, StringComparison.OrdinalIgnoreCase)).Value ?? Array.Empty<GrdPerformancePoint>();

    public static FittingCalculationResult CalculateFittingResult(FittingDefinition definition,
        IReadOnlyDictionary<string, double> inputs, double airflowM3PerS, double temperatureC,
        double relativeHumidity = 0.50, double atmosphericPressurePa = 101_325.0)
    {
        var requestedCfm = UnitConversions.M3PerSToCfm(airflowM3PerS);
        GrdPerformanceSelection selection;
        DuctGeometry referenceGeometry;
        if (string.Equals(definition.Code, Titus350RCode, StringComparison.OrdinalIgnoreCase))
        {
            var sizeIndex = Convert.ToInt32(inputs.GetValueOrDefault("sizeIndex", Titus350RSizeIndex("12x12")));
            if (sizeIndex < 0 || sizeIndex >= Titus350RSizes.Count)
                throw new ArgumentOutOfRangeException(nameof(inputs), "Select a published Titus 350R nominal duct size.");
            var nominalSize = Titus350RSizes[sizeIndex];
            selection = SelectTitus350R(requestedCfm, nominalSize);
            var dimensions = ParseRectangularSize(nominalSize);
            referenceGeometry = GeometryCalculator.Rectangular(UnitConversions.InchesToM(dimensions.WidthIn),
                UnitConversions.InchesToM(dimensions.HeightIn));
        }
        else if (string.Equals(definition.Code, PriceScdCode, StringComparison.OrdinalIgnoreCase))
        {
            var face = Convert.ToInt32(inputs.GetValueOrDefault("face", 24));
            var neck = Convert.ToInt32(inputs.GetValueOrDefault("neck", 8));
            selection = SelectPriceScd(requestedCfm, face, neck);
            referenceGeometry = GeometryCalculator.Round(UnitConversions.InchesToM(neck));
        }
        else if (string.Equals(definition.Code, TitusTbd80Code, StringComparison.OrdinalIgnoreCase))
        {
            var length = Convert.ToInt32(inputs.GetValueOrDefault("length", 2));
            var slotWidth = inputs.GetValueOrDefault("slotWidth", 0.75);
            var slotCount = Convert.ToInt32(inputs.GetValueOrDefault("slotCount", 1));
            selection = SelectTitusTbd80(requestedCfm, length, slotWidth, slotCount);
            referenceGeometry = GeometryCalculator.Round(UnitConversions.InchesToM(selection.InletDiameterIn ?? 6));
        }
        else
        {
            throw new ArgumentException($"{definition.Code} is not a published GRD performance-table fitting.");
        }

        var air = AirProperties.Calculate(temperatureC, relativeHumidity, atmosphericPressurePa);
        var selectedAirflow = UnitConversions.CfmToM3PerS(selection.Selected.AirflowCfm);
        var referenceVelocity = selection.Selected.CoreVelocityFpm is double coreVelocity
            ? UnitConversions.FpmToMps(coreVelocity)
            : selectedAirflow / referenceGeometry.ActualAreaM2;
        var velocityPressurePa = selection.Selected.VelocityPressureInWg is double publishedVelocityPressure
            ? UnitConversions.InchesWaterToPa(publishedVelocityPressure)
            : air.DensityKgPerM3 * referenceVelocity * referenceVelocity / 2.0;
        var pressureLossPa = UnitConversions.InchesWaterToPa(selection.Selected.TotalPressureInWg);
        var coefficient = velocityPressurePa > 0 ? pressureLossPa / velocityPressurePa : 0;
        var belowSoundRange = selection.Selected.SoundNc.StartsWith('<');
        var soundValue = belowSoundRange
            ? double.Parse(selection.Selected.SoundNc[1..], CultureInfo.InvariantCulture) - 1
            : double.Parse(selection.Selected.SoundNc, CultureInfo.InvariantCulture);
        var resultInputs = new Dictionary<string, double>(inputs, StringComparer.OrdinalIgnoreCase)
        {
            ["selectedCfm"] = selection.Selected.AirflowCfm,
            ["soundNc"] = soundValue,
            ["soundBelow15"] = belowSoundRange ? 1 : 0,
            ["throw150"] = ParseThrow(selection.Selected.ThrowFt, 0),
            ["throw100"] = ParseThrow(selection.Selected.ThrowFt, 1),
            ["throw50"] = ParseThrow(selection.Selected.ThrowFt, 2)
        };
        if (selection.Selected.StaticPressureInWg is double staticPressure)
            resultInputs["staticPressure"] = staticPressure;
        if (selection.InletDiameterIn is int inletDiameter)
            resultInputs["selectedInlet"] = inletDiameter;
        if (!string.IsNullOrWhiteSpace(selection.NominalDuctSizeIn))
        {
            resultInputs["selectedSizeIndex"] = Titus350RSizeIndex(selection.NominalDuctSizeIn);
            resultInputs["nominalDuctArea"] = selection.NominalDuctAreaFt2 ?? 0;
            resultInputs["coreArea"] = selection.CoreAreaFt2 ?? 0;
            resultInputs["coreVelocity"] = selection.Selected.CoreVelocityFpm ?? 0;
            resultInputs["publishedVelocityPressure"] = selection.Selected.VelocityPressureInWg ?? 0;
        }

        return new FittingCalculationResult(definition, resultInputs, referenceVelocity, air, coefficient,
            velocityPressurePa, pressureLossPa, airflowM3PerS, referenceGeometry);
    }

    private static GrdPerformanceSelection Select(string product, string configuration, double requestedCfm,
        IReadOnlyList<GrdPerformancePoint> table)
    {
        if (!double.IsFinite(requestedCfm) || requestedCfm <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedCfm), "Airflow must be greater than zero.");
        var selected = table.FirstOrDefault(point => point.AirflowCfm + 1e-9 >= requestedCfm)
                       ?? throw new ArgumentOutOfRangeException(nameof(requestedCfm),
                           $"Requested airflow exceeds the published maximum of {table[^1].AirflowCfm:0} cfm for {configuration}.");
        return new GrdPerformanceSelection(product, configuration, requestedCfm, selected, table);
    }

    private static double ParseThrow(string throwText, int index)
    {
        var values = throwText.Split('-');
        return index < values.Length && double.TryParse(values[index], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static (double WidthIn, double HeightIn) ParseRectangularSize(string size)
    {
        var dimensions = size.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dimensions.Length != 2
            || !double.TryParse(dimensions[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(dimensions[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            throw new InvalidDataException($"Invalid Titus 350R nominal size '{size}'.");
        return (width, height);
    }

    private static GrdPerformancePoint[] P(string encoded) => encoded.Split('|')
        .Select(row => row.Split(','))
        .Select(values => new GrdPerformancePoint(
            double.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture),
            values[2] == "-" ? "<15" : values[2],
            values[3],
            values.Length > 4
                ? double.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture)
                : null))
        .ToArray();

    private static IReadOnlyDictionary<TitusTbd80Configuration, GrdPerformancePoint[]> LoadTitusTbd80()
    {
        const string resourceName = "DuctSizer.Core.Data.TitusTbd80D.csv";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Embedded Titus performance resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        _ = reader.ReadLine();
        var points = new Dictionary<TitusTbd80Configuration, List<GrdPerformancePoint>>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var fields = line.Split(',');
            if (fields.Length < 9)
                throw new InvalidDataException($"Invalid Titus TBD-80 D performance row: {line}");
            var configuration = new TitusTbd80Configuration(
                int.Parse(fields[0], CultureInfo.InvariantCulture),
                int.Parse(fields[1], CultureInfo.InvariantCulture),
                double.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture));
            if (!points.TryGetValue(configuration, out var table))
            {
                table = new List<GrdPerformancePoint>();
                points[configuration] = table;
            }
            table.Add(new GrdPerformancePoint(
                double.Parse(fields[4], CultureInfo.InvariantCulture),
                double.Parse(fields[5], CultureInfo.InvariantCulture),
                fields[7],
                fields[8],
                double.Parse(fields[6], CultureInfo.InvariantCulture)));
        }
        if (points.Count != 132 || points.Values.Any(table => table.Count != 7))
            throw new InvalidDataException("The embedded Titus TBD-80 D catalog must contain 132 configurations with seven performance points each.");
        return points.ToDictionary(pair => pair.Key,
            pair => pair.Value.OrderBy(point => point.AirflowCfm).ToArray());
    }

    private static IReadOnlyDictionary<Titus350RConfiguration, GrdPerformancePoint[]> LoadTitus350R()
    {
        const string resourceName = "DuctSizer.Core.Data.Titus350R.csv";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Embedded Titus performance resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        _ = reader.ReadLine();
        var points = new Dictionary<Titus350RConfiguration, List<GrdPerformancePoint>>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var fields = line.Split(',');
            if (fields.Length != 9)
                throw new InvalidDataException($"Invalid Titus 350R performance row: {line}");
            var configuration = new Titus350RConfiguration(fields[0],
                double.Parse(fields[1], CultureInfo.InvariantCulture),
                double.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[8], CultureInfo.InvariantCulture));
            if (!points.TryGetValue(configuration, out var table))
            {
                table = new List<GrdPerformancePoint>();
                points[configuration] = table;
            }
            var negativeStaticPressure = double.Parse(fields[5], CultureInfo.InvariantCulture);
            table.Add(new GrdPerformancePoint(
                double.Parse(fields[6], CultureInfo.InvariantCulture),
                negativeStaticPressure,
                fields[7],
                string.Empty,
                negativeStaticPressure,
                double.Parse(fields[3], CultureInfo.InvariantCulture),
                double.Parse(fields[4], CultureInfo.InvariantCulture)));
        }
        if (points.Count != 78 || points.Values.Any(table => table.Count != 9))
            throw new InvalidDataException("The embedded Titus 350R catalog must contain 78 nominal sizes with nine performance points each.");
        return points.ToDictionary(pair => pair.Key,
            pair => pair.Value.OrderBy(point => point.CoreVelocityFpm).ToArray());
    }
}
