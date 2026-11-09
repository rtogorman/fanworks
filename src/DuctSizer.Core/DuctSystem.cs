using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuctSizer.Core;

public sealed class SavedDuct
{
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AirSystemType { get; set; } = "Supply Air";
    public string BranchId { get; set; } = "Main";
    public double AirflowM3PerS { get; set; }
    public double TemperatureC { get; set; }
    public double RelativeHumidity { get; set; } = 0.50;
    public double AtmosphericPressurePa { get; set; } = 101_325.0;
    public DuctShape Shape { get; set; }
    public double DiameterM { get; set; }
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public double RoughnessFeet { get; set; }
    public EquivalentDiameterMethod EquivalentDiameterMethod { get; set; }
    public PressureLossBasis PressureLossBasis { get; set; }
    public double LengthM { get; set; }
    public double DeltaXM { get; set; }
    public double DeltaYM { get; set; }
    public double DeltaZM { get; set; }
    public bool RequiresResizing { get; set; }

    [JsonIgnore]
    public string Id => $"D-{Sequence:000}";

    public DuctGeometry CreateGeometry() => Shape == DuctShape.Round
        ? GeometryCalculator.Round(DiameterM)
        : GeometryCalculator.Rectangular(WidthM, HeightM);

    public DuctCalculationInput CreateCalculationInput() => new(
        AirflowM3PerS,
        TemperatureC,
        RelativeHumidity,
        AtmosphericPressurePa,
        CreateGeometry(),
        new DuctMaterial(MaterialName, RoughnessFeet),
        EquivalentDiameterMethod,
        PressureLossBasis,
        LengthM);

    public DuctCalculationResult Calculate() => DuctCalculator.Calculate(CreateCalculationInput());

    public static SavedDuct FromResult(int sequence, string description, DuctCalculationResult result,
        string airSystemType = "Supply Air", string branchId = "Main")
    {
        var input = result.Input;
        return new SavedDuct
        {
            Sequence = sequence,
            Description = description,
            AirSystemType = airSystemType,
            BranchId = branchId,
            AirflowM3PerS = input.AirflowM3PerS,
            TemperatureC = input.TemperatureC,
            RelativeHumidity = input.RelativeHumidity,
            AtmosphericPressurePa = input.AtmosphericPressurePa,
            Shape = input.Geometry.Shape,
            DiameterM = input.Geometry.DiameterM,
            WidthM = input.Geometry.WidthM,
            HeightM = input.Geometry.HeightM,
            MaterialName = input.Material.Name,
            RoughnessFeet = input.Material.RoughnessFeet,
            EquivalentDiameterMethod = input.EquivalentDiameterMethod,
            PressureLossBasis = input.PressureLossBasis,
            LengthM = input.LengthM
        };
    }
}

public sealed class DuctSystemDocument
{
    public int SchemaVersion { get; set; } = 12;
    public int NextSequence { get; set; } = 1;
    public int NextFittingSequence { get; set; } = 1;
    public int NextLayoutSequence { get; set; } = 1;
    public int NextBranchSequence { get; set; } = 1;
    public string DefaultAirSystemType { get; set; } = "Supply Air";
    public string DefaultBranchId { get; set; } = "Main";
    public string UserName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectDate { get; set; } = string.Empty;
    public string LastEditedUtc { get; set; } = string.Empty;
    public string CompanyLogoPngBase64 { get; set; } = string.Empty;
    public double DefaultSizingIncrementM { get; set; }
    public SizeRoundingDirection DefaultSizeRoundingDirection { get; set; } = SizeRoundingDirection.Automatic;
    public EquivalentDiameterMethod DefaultEquivalentDiameterMethod { get; set; } = EquivalentDiameterMethod.Huebscher;
    public PressureLossBasis DefaultPressureLossBasis { get; set; } = PressureLossBasis.EquivalentRoundArea;
    public string CoordinateOriginNodeId { get; set; } = string.Empty;
    public double CoordinateOriginXM { get; set; }
    public double CoordinateOriginYM { get; set; }
    public double CoordinateOriginZM { get; set; }
    public bool LayoutShowToScale { get; set; } = true;
    public double LayoutFeetPerGridCell { get; set; } = 1.0;
    public LayoutViewMode LayoutViewMode { get; set; } = LayoutViewMode.Isometric;
    public LayoutPdfBackground? LayoutPdfBackground { get; set; }
    public AutomaticSizingSettings AutomaticSizing { get; set; } = new();
    public List<SavedDuct> Ducts { get; set; } = new();
    public List<SavedFitting> Fittings { get; set; } = new();
    public List<SavedLayoutNode> LayoutNodes { get; set; } = new();

    public int ReserveSequence()
    {
        var highestExisting = Ducts.Count == 0 ? 0 : Ducts.Max(x => x.Sequence);
        NextSequence = Math.Max(NextSequence, highestExisting + 1);
        return NextSequence++;
    }

    public int ReserveFittingSequence()
    {
        var highestExisting = Fittings.Count == 0 ? 0 : Fittings.Max(x => x.Sequence);
        NextFittingSequence = Math.Max(NextFittingSequence, highestExisting + 1);
        return NextFittingSequence++;
    }

    public int ReserveLayoutSequence()
    {
        var highestExisting = LayoutNodes.Count == 0 ? 0 : LayoutNodes.Max(x => x.Sequence);
        NextLayoutSequence = Math.Max(NextLayoutSequence, highestExisting + 1);
        return NextLayoutSequence++;
    }

    public string ReserveBranchId() => $"B-{NextBranchSequence++:000}";
}

public sealed class LayoutPdfBackground
{
    public string SourceFileName { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public int PageCount { get; set; } = 1;
    public float PageWidthPoints { get; set; } = 612;
    public float PageHeightPoints { get; set; } = 792;
    public string PagePngBase64 { get; set; } = string.Empty;
    public double PaperInches { get; set; } = 0.25;
    public double DrawingFeet { get; set; } = 1.0;
    public double CenterXFeet { get; set; }
    public double CenterYFeet { get; set; }
    public double CenterZFeet { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class AutomaticSizingSettings
{
    public AutomaticDuctSizingDefaults Duct { get; set; } = new();
    public AutomaticEquipmentSizingDefaults Coil { get; set; } = new()
    {
        TemperatureC = 12.7777777778,
        PressureDropPa = 0.20 * UnitConversions.InchesWaterToPascals
    };
    public AutomaticEquipmentSizingDefaults Filter { get; set; } = new()
    {
        PressureDropPa = 0.10 * UnitConversions.InchesWaterToPascals
    };
    public AutomaticGrdSizingDefaults Grd { get; set; } = new();
    public Dictionary<string, AutomaticFittingSizingDefaults> Fittings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AutomaticFittingSizingDefaults Fitting(string group)
    {
        Fittings ??= new Dictionary<string, AutomaticFittingSizingDefaults>(StringComparer.OrdinalIgnoreCase);
        if (Fittings.TryGetValue(group, out var existing) && existing is not null)
            return existing;
        var definition = FittingCatalog.All.FirstOrDefault(x =>
                             string.Equals(FittingCatalog.SelectionGroup(x), group, StringComparison.OrdinalIgnoreCase))
                         ?? FittingCatalog.All.First();
        var created = new AutomaticFittingSizingDefaults
        {
            FittingCode = definition.Code,
            Inputs = definition.Inputs.ToDictionary(x => x.Key, x => x.DefaultValue,
                StringComparer.OrdinalIgnoreCase)
        };
        Fittings[group] = created;
        return created;
    }
}

public sealed class AutomaticDuctSizingDefaults
{
    public CalculationMode Mode { get; set; } = CalculationMode.SizeByTargetFriction;
    public double TargetFrictionPaPerM { get; set; } = UnitConversions.InchesWaterPer100FeetToPaPerM(0.08);
    public double MaximumVelocityMPerS { get; set; } = UnitConversions.FpmToMps(1000);
    public DuctShape Shape { get; set; } = DuctShape.Round;
    public double DiameterM { get; set; }
    public double WidthM { get; set; }
    public double HeightM { get; set; } = 12 * UnitConversions.InchesToMeters;
    public string MaterialName { get; set; } = "Galvanized Spiral Duct";
}

public sealed class AutomaticEquipmentSizingDefaults
{
    public double TemperatureC { get; set; } = UnitConversions.FahrenheitToCelsius(70);
    public double PressureDropPa { get; set; }
}

public sealed class AutomaticGrdSizingDefaults
{
    public string GrdType { get; set; } = "Grille";
    public double PressureDropPa { get; set; } = 0.05 * UnitConversions.InchesWaterToPascals;
    public double FaceSizeIn { get; set; } = 24;
    public double NeckDiameterIn { get; set; } = 8;
    public double LengthFt { get; set; } = 2;
    public double SlotWidthIn { get; set; } = 0.75;
    public int SlotCount { get; set; } = 1;
    public string NominalDuctSizeIn { get; set; } = "12x12";
}

public sealed class AutomaticFittingSizingDefaults
{
    public string FittingCode { get; set; } = string.Empty;
    public Dictionary<string, double> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DuctShape Shape { get; set; } = DuctShape.Round;
    public double DiameterM { get; set; } = 12 * UnitConversions.InchesToMeters;
    public double WidthM { get; set; } = 18 * UnitConversions.InchesToMeters;
    public double HeightM { get; set; } = 12 * UnitConversions.InchesToMeters;
    public JunctionPath JunctionPath { get; set; } = JunctionPath.Straight;
    public double BranchFraction { get; set; } = 0.4;
}

public enum LayoutNodeType
{
    Reservoir,
    Fan,
    Coil,
    Fitting,
    Filter,
    Grd,
    CalculationPoint,
    Duct,
    CustomComponent
}

public enum LayoutViewMode
{
    Isometric,
    IsometricNegative,
    TopDown
}

public sealed class SavedLayoutNode
{
    public int Sequence { get; set; }
    public LayoutNodeType Type { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string ParentNodeId { get; set; } = string.Empty;
    public string SecondaryParentNodeId { get; set; } = string.Empty;
    public string FittingGroup { get; set; } = string.Empty;
    public string AirSystemType { get; set; } = "Supply Air";
    public string BranchId { get; set; } = "Main";
    public double AirflowM3PerS { get; set; }
    public double TemperatureC { get; set; } = 21.1111111111;
    public double PressureDropPa { get; set; }
    public double FanStaticPressurePa { get; set; }
    public double FanEfficiencyPercent { get; set; } = 70.0;
    public double PressurePa { get; set; }
    public double VelocityMPerS { get; set; }
    public string GrdType { get; set; } = "Grille";
    public string Description { get; set; } = string.Empty;
    public double GrdFaceSizeIn { get; set; } = 24;
    public double GrdNeckDiameterIn { get; set; } = 8;
    public double GrdLengthFt { get; set; } = 2;
    public double GrdSlotWidthIn { get; set; } = 0.75;
    public int GrdSlotCount { get; set; } = 1;
    public double GrdInletDiameterIn { get; set; } = 6;
    public double GrdSelectedAirflowCfm { get; set; }
    public string GrdSoundNc { get; set; } = string.Empty;
    public string GrdThrowFt { get; set; } = string.Empty;
    public string GrdNominalDuctSizeIn { get; set; } = "12x12";
    public string SpaceRole { get; set; } = "Supply Discharge";
    public string CalculationPointType { get; set; } = "Pressure";
    public string CalculationConnectionNodeId { get; set; } = string.Empty;
    public bool CalculationConnectionSecondary { get; set; }
    public double CalculationConnectionFraction { get; set; } = 0.5;
    public int? ObservedDuctSequence { get; set; }
    public double DeltaXM { get; set; }
    public double DeltaYM { get; set; }
    public double DeltaZM { get; set; }
    public double SecondaryDeltaXM { get; set; }
    public double SecondaryDeltaYM { get; set; }
    public double SecondaryDeltaZM { get; set; }
    public double ElbowAngleDegrees { get; set; } = 90.0;
    public bool HasAbsoluteCoordinateAnchor { get; set; }
    public double AbsoluteAnchorXM { get; set; }
    public double AbsoluteAnchorYM { get; set; }
    public double AbsoluteAnchorZM { get; set; }
    public int? LinkedDuctSequence { get; set; }
    public int? LinkedFittingSequence { get; set; }
    public int? ConnectionDuctSequence { get; set; }
    public int? SecondaryConnectionDuctSequence { get; set; }
    public bool ConnectionFlowReversed { get; set; }
    public bool SecondaryConnectionFlowReversed { get; set; }
    public bool ConnectionAirflowUserSpecified { get; set; }
    public bool SecondaryConnectionAirflowUserSpecified { get; set; }
    public bool ConnectionAirflowOverride { get; set; }
    public bool SecondaryConnectionAirflowOverride { get; set; }
    public bool ConnectionDeltaConfirmed { get; set; }
    public bool SecondaryConnectionDeltaConfirmed { get; set; }
    public bool AirflowUserSpecified { get; set; }
    public bool AirflowOverride { get; set; }
    public bool BranchOutletBelow { get; set; }
    public bool IsDefined { get; set; } = true;

    [JsonIgnore]
    public string Id => $"{IdPrefix}-{Sequence:000}";

    [JsonIgnore]
    public string IdPrefix => Type switch
    {
        LayoutNodeType.Fitting when FittingCatalog.IsTerminalSelectionGroup(FittingGroup) => "T",
        LayoutNodeType.Fitting => "F",
        LayoutNodeType.Grd => "T",
        LayoutNodeType.CalculationPoint => "S",
        LayoutNodeType.Reservoir => "L",
        LayoutNodeType.Fan => "Fan",
        _ => "C"
    };
}

public readonly record struct LayoutCoordinate(double XM, double YM, double ZM);

public static class LayoutCoordinateResolver
{
    public static IReadOnlyDictionary<int, LayoutCoordinate> Resolve(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.LayoutNodes
            .Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<int, LayoutCoordinate>();
        if (nodes.Count == 0)
            return result;

        var origin = nodes.TryGetValue(document.CoordinateOriginNodeId, out var selected)
            ? selected
            : nodes.Values.OrderBy(x => x.Sequence).First();
        result[origin.Sequence] = new LayoutCoordinate(
            Finite(document.CoordinateOriginXM), Finite(document.CoordinateOriginYM), Finite(document.CoordinateOriginZM));

        var queue = new Queue<SavedLayoutNode>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentCoordinate = result[current.Sequence];

            foreach (var child in nodes.Values.Where(x =>
                         string.Equals(x.ParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                Add(child, new LayoutCoordinate(currentCoordinate.XM + child.DeltaXM,
                    currentCoordinate.YM + child.DeltaYM, currentCoordinate.ZM + child.DeltaZM));
            foreach (var child in nodes.Values.Where(x =>
                         string.Equals(x.SecondaryParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                Add(child, new LayoutCoordinate(currentCoordinate.XM + child.SecondaryDeltaXM,
                    currentCoordinate.YM + child.SecondaryDeltaYM,
                    currentCoordinate.ZM + child.SecondaryDeltaZM));

            if (nodes.TryGetValue(current.ParentNodeId, out var parent))
                Add(parent, new LayoutCoordinate(currentCoordinate.XM - current.DeltaXM,
                    currentCoordinate.YM - current.DeltaYM, currentCoordinate.ZM - current.DeltaZM));
            if (nodes.TryGetValue(current.SecondaryParentNodeId, out var secondaryParent))
                Add(secondaryParent, new LayoutCoordinate(currentCoordinate.XM - current.SecondaryDeltaXM,
                    currentCoordinate.YM - current.SecondaryDeltaYM,
                    currentCoordinate.ZM - current.SecondaryDeltaZM));
        }

        // A deleted duct can intentionally leave a separate system fragment. Keep that fragment at the
        // same absolute drawing position instead of falling back to its old screen-grid coordinates.
        foreach (var anchoredRoot in nodes.Values
                     .Where(x => x.HasAbsoluteCoordinateAnchor && !result.ContainsKey(x.Sequence))
                     .OrderBy(x => x.Sequence))
        {
            Add(anchoredRoot, new LayoutCoordinate(Finite(anchoredRoot.AbsoluteAnchorXM),
                Finite(anchoredRoot.AbsoluteAnchorYM), Finite(anchoredRoot.AbsoluteAnchorZM)));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentCoordinate = result[current.Sequence];
                foreach (var child in nodes.Values.Where(x =>
                             string.Equals(x.ParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                    Add(child, new LayoutCoordinate(currentCoordinate.XM + child.DeltaXM,
                        currentCoordinate.YM + child.DeltaYM, currentCoordinate.ZM + child.DeltaZM));
                foreach (var child in nodes.Values.Where(x =>
                             string.Equals(x.SecondaryParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                    Add(child, new LayoutCoordinate(currentCoordinate.XM + child.SecondaryDeltaXM,
                        currentCoordinate.YM + child.SecondaryDeltaYM,
                        currentCoordinate.ZM + child.SecondaryDeltaZM));
            }
        }

        foreach (var point in document.LayoutNodes.Where(x => x.Type == LayoutNodeType.CalculationPoint))
        {
            if (!nodes.TryGetValue(point.CalculationConnectionNodeId, out var downstream)
                || !result.TryGetValue(downstream.Sequence, out var downstreamCoordinate))
                continue;
            var parentId = point.CalculationConnectionSecondary
                ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
            if (!nodes.TryGetValue(parentId, out var parent)
                || !result.TryGetValue(parent.Sequence, out var parentCoordinate))
                continue;
            var fraction = Math.Clamp(point.CalculationConnectionFraction, 0.0, 1.0);
            result[point.Sequence] = new LayoutCoordinate(
                parentCoordinate.XM + (downstreamCoordinate.XM - parentCoordinate.XM) * fraction,
                parentCoordinate.YM + (downstreamCoordinate.YM - parentCoordinate.YM) * fraction,
                parentCoordinate.ZM + (downstreamCoordinate.ZM - parentCoordinate.ZM) * fraction);
        }
        return result;

        void Add(SavedLayoutNode node, LayoutCoordinate coordinate)
        {
            if (result.ContainsKey(node.Sequence))
                return;
            result[node.Sequence] = coordinate;
            queue.Enqueue(node);
        }

        static double Finite(double value) => double.IsFinite(value) ? value : 0.0;
    }
}

public sealed class SavedFitting
{
    public int Sequence { get; set; }
    public string CustomId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AirSystemType { get; set; } = "Supply Air";
    public string BranchId { get; set; } = "Main";
    public string FittingCode { get; set; } = string.Empty;
    public Dictionary<string, double> Inputs { get; set; } = new();
    public double AirflowM3PerS { get; set; }
    public DuctShape Shape { get; set; } = DuctShape.Round;
    public double DiameterM { get; set; }
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public double ReferenceVelocityMPerS { get; set; }
    public double TemperatureC { get; set; }
    public double RelativeHumidity { get; set; } = 0.50;
    public double AtmosphericPressurePa { get; set; } = 101_325.0;
    public JunctionFlowData? Junction { get; set; }
    public double DeltaXM { get; set; }
    public double DeltaYM { get; set; }
    public double DeltaZM { get; set; }

    [JsonIgnore]
    public string Id => string.IsNullOrWhiteSpace(CustomId) ? $"F-{Sequence:000}" : CustomId.Trim();

    public DuctGeometry? CreateGeometry()
    {
        if (Shape == DuctShape.Round && DiameterM > 0.0)
            return GeometryCalculator.Round(DiameterM);
        if (Shape == DuctShape.Rectangular && WidthM > 0.0 && HeightM > 0.0)
            return GeometryCalculator.Rectangular(WidthM, HeightM);
        return null;
    }

    public FittingCalculationResult Calculate()
    {
        var definition = FittingCatalog.Find(FittingCode);
        if (string.Equals(FittingCode, GrdPerformanceCatalog.Titus350RCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(FittingCode, GrdPerformanceCatalog.PriceScdCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(FittingCode, GrdPerformanceCatalog.TitusTbd80Code, StringComparison.OrdinalIgnoreCase))
            return GrdPerformanceCatalog.CalculateFittingResult(definition, Inputs, AirflowM3PerS, TemperatureC,
                RelativeHumidity, AtmosphericPressurePa);
        if (string.Equals(FittingCode, VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(FittingCode, VavPerformanceCatalog.WithoutReheatCode, StringComparison.OrdinalIgnoreCase))
            return VavPerformanceCatalog.CalculateFittingResult(definition, Inputs, AirflowM3PerS, TemperatureC,
                RelativeHumidity, AtmosphericPressurePa);
        if (definition.IsJunction && Junction is not null)
            return FittingCalculator.Calculate(definition, Inputs, Junction, TemperatureC,
                RelativeHumidity, AtmosphericPressurePa);
        var geometry = CreateGeometry();
        return AirflowM3PerS > 0.0 && geometry is not null
            ? FittingCalculator.Calculate(definition, Inputs, AirflowM3PerS, geometry, TemperatureC,
                RelativeHumidity, AtmosphericPressurePa)
            : FittingCalculator.Calculate(definition, Inputs, ReferenceVelocityMPerS, TemperatureC,
                RelativeHumidity, AtmosphericPressurePa);
    }

    public static SavedFitting FromResult(int sequence, string description, FittingCalculationResult result,
        string airSystemType = "Supply Air", string branchId = "Main") => new()
    {
        Sequence = sequence,
        Description = description,
        AirSystemType = airSystemType,
        BranchId = branchId,
        FittingCode = result.Definition.Code,
        Inputs = new Dictionary<string, double>(result.Inputs),
        AirflowM3PerS = result.AirflowM3PerS ?? 0.0,
        Shape = result.ReferenceGeometry?.Shape ?? DuctShape.Round,
        DiameterM = result.ReferenceGeometry?.DiameterM ?? 0.0,
        WidthM = result.ReferenceGeometry?.WidthM ?? 0.0,
        HeightM = result.ReferenceGeometry?.HeightM ?? 0.0,
        ReferenceVelocityMPerS = result.ReferenceVelocityMPerS,
        TemperatureC = result.Air.TemperatureC,
        RelativeHumidity = result.Air.RelativeHumidity,
        AtmosphericPressurePa = result.Air.PressurePa,
        Junction = result.Junction
    };
}

public static class DuctSystemStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static DuctSystemDocument Load(string path)
    {
        if (!File.Exists(path))
            return new DuctSystemDocument();

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<DuctSystemDocument>(json, JsonOptions)
                       ?? throw new InvalidDataException("The local duct-system file is empty or invalid.");
        document.Ducts ??= new List<SavedDuct>();
        document.Fittings ??= new List<SavedFitting>();
        document.LayoutNodes ??= new List<SavedLayoutNode>();
        document.DefaultAirSystemType = NormalizedText(document.DefaultAirSystemType, "Supply Air");
        document.DefaultBranchId = NormalizedText(document.DefaultBranchId, "Main");
        document.UserName = document.UserName?.Trim() ?? string.Empty;
        document.ProjectName = document.ProjectName?.Trim() ?? string.Empty;
        document.ProjectNumber = document.ProjectNumber?.Trim() ?? string.Empty;
        document.ProjectDate = document.ProjectDate?.Trim() ?? string.Empty;
        document.LastEditedUtc = document.LastEditedUtc?.Trim() ?? string.Empty;
        document.CompanyLogoPngBase64 = document.CompanyLogoPngBase64?.Trim() ?? string.Empty;
        document.LayoutShowToScale = true;
        if (!Enum.IsDefined(document.LayoutViewMode))
            document.LayoutViewMode = LayoutViewMode.Isometric;
        if (!double.IsFinite(document.LayoutFeetPerGridCell) || document.LayoutFeetPerGridCell <= 0)
            document.LayoutFeetPerGridCell = 1.0;
        if (document.LayoutPdfBackground is { } pdfBackground)
        {
            pdfBackground.SourceFileName = pdfBackground.SourceFileName?.Trim() ?? string.Empty;
            pdfBackground.PagePngBase64 = pdfBackground.PagePngBase64?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pdfBackground.PagePngBase64))
                document.LayoutPdfBackground = null;
            else
            {
                pdfBackground.PageCount = Math.Max(1, pdfBackground.PageCount);
                pdfBackground.PageNumber = Math.Clamp(pdfBackground.PageNumber, 1, pdfBackground.PageCount);
                pdfBackground.PageWidthPoints = float.IsFinite(pdfBackground.PageWidthPoints)
                                                  && pdfBackground.PageWidthPoints > 0
                    ? pdfBackground.PageWidthPoints : 612;
                pdfBackground.PageHeightPoints = float.IsFinite(pdfBackground.PageHeightPoints)
                                                   && pdfBackground.PageHeightPoints > 0
                    ? pdfBackground.PageHeightPoints : 792;
                pdfBackground.PaperInches = double.IsFinite(pdfBackground.PaperInches)
                                             && pdfBackground.PaperInches > 0
                    ? pdfBackground.PaperInches : 0.25;
                pdfBackground.DrawingFeet = double.IsFinite(pdfBackground.DrawingFeet)
                                             && pdfBackground.DrawingFeet > 0
                    ? pdfBackground.DrawingFeet : 1.0;
                pdfBackground.CenterXFeet = FiniteOrZero(pdfBackground.CenterXFeet);
                pdfBackground.CenterYFeet = FiniteOrZero(pdfBackground.CenterYFeet);
                pdfBackground.CenterZFeet = FiniteOrZero(pdfBackground.CenterZFeet);
            }
        }
        document.AutomaticSizing ??= new AutomaticSizingSettings();
        document.AutomaticSizing.Duct ??= new AutomaticDuctSizingDefaults();
        document.AutomaticSizing.Coil ??= new AutomaticEquipmentSizingDefaults();
        document.AutomaticSizing.Filter ??= new AutomaticEquipmentSizingDefaults();
        document.AutomaticSizing.Grd ??= new AutomaticGrdSizingDefaults();
        if (document.AutomaticSizing.Grd.GrdType is not ("Grille" or "Diffuser" or "Linear Diffuser TBD-80 D"))
            document.AutomaticSizing.Grd.GrdType = "Grille";
        if (!GrdPerformanceCatalog.TitusLengthsFt.Contains(Convert.ToInt32(document.AutomaticSizing.Grd.LengthFt)))
            document.AutomaticSizing.Grd.LengthFt = 2;
        if (!GrdPerformanceCatalog.TitusSlotWidthsIn.Any(value =>
                Math.Abs(value - document.AutomaticSizing.Grd.SlotWidthIn) < 1e-9))
            document.AutomaticSizing.Grd.SlotWidthIn = 0.75;
        if (!GrdPerformanceCatalog.TitusSlotCounts.Contains(document.AutomaticSizing.Grd.SlotCount))
            document.AutomaticSizing.Grd.SlotCount = 1;
        if (!GrdPerformanceCatalog.Titus350RSizes.Contains(document.AutomaticSizing.Grd.NominalDuctSizeIn,
                StringComparer.OrdinalIgnoreCase))
            document.AutomaticSizing.Grd.NominalDuctSizeIn = "12x12";
        document.AutomaticSizing.Fittings ??=
            new Dictionary<string, AutomaticFittingSizingDefaults>(StringComparer.OrdinalIgnoreCase);
        foreach (var fittingDefaults in document.AutomaticSizing.Fittings.Values)
            fittingDefaults.Inputs ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (document.SchemaVersion <= 6)
            MigrateTypedLayoutIds(document);
        if (!double.IsFinite(document.DefaultSizingIncrementM) || document.DefaultSizingIncrementM < 0)
            document.DefaultSizingIncrementM = 0;
        if (!Enum.IsDefined(document.DefaultSizeRoundingDirection))
            document.DefaultSizeRoundingDirection = SizeRoundingDirection.Automatic;
        if (document.SchemaVersion == 1)
        {
            foreach (var node in document.LayoutNodes)
            {
                node.GridX = checked(node.GridX * 2);
                node.GridY = checked(node.GridY * 4);
            }
            document.SchemaVersion = 2;
        }
        if (document.SchemaVersion == 2)
            document.SchemaVersion = 3;
        if (document.SchemaVersion == 3)
        {
            MigrateCalculationPointsToDuctAnnotations(document);
            document.SchemaVersion = 4;
        }
        if (document.SchemaVersion == 4)
            document.SchemaVersion = 5;
        if (document.SchemaVersion == 5)
            document.SchemaVersion = 6;
        if (document.SchemaVersion == 6)
            document.SchemaVersion = 7;
        if (document.SchemaVersion == 7)
        {
            foreach (var node in document.LayoutNodes)
            {
                node.ConnectionDeltaConfirmed = !string.IsNullOrWhiteSpace(node.ParentNodeId);
                node.SecondaryConnectionDeltaConfirmed = !string.IsNullOrWhiteSpace(node.SecondaryParentNodeId);
            }
            document.SchemaVersion = 8;
        }
        if (document.SchemaVersion == 8)
        {
            MigrateFanLayoutIds(document);
            document.SchemaVersion = 9;
        }
        if (document.SchemaVersion == 9)
            document.SchemaVersion = 10;
        if (document.SchemaVersion == 10)
        {
            foreach (var node in document.LayoutNodes.Where(x =>
                         !string.IsNullOrWhiteSpace(x.SecondaryParentNodeId)))
            {
                node.SecondaryDeltaXM = node.DeltaXM;
                node.SecondaryDeltaYM = node.DeltaYM;
                node.SecondaryDeltaZM = node.DeltaZM;
            }
            document.SchemaVersion = 11;
        }
        if (document.SchemaVersion == 11)
            document.SchemaVersion = 12;
        if (document.SchemaVersion != 12)
            throw new InvalidDataException($"Unsupported duct-system file version {document.SchemaVersion}.");
        if (document.Ducts.Any(x => x.Sequence <= 0)
            || document.Ducts.Select(x => x.Sequence).Distinct().Count() != document.Ducts.Count)
            throw new InvalidDataException("The local duct-system file contains invalid or duplicate duct IDs.");
        if (document.Fittings.Any(x => x.Sequence <= 0)
            || document.Fittings.Select(x => x.Sequence).Distinct().Count() != document.Fittings.Count)
            throw new InvalidDataException("The local duct-system file contains invalid or duplicate fitting IDs.");
        if (document.LayoutNodes.Any(x => x.Sequence <= 0)
            || document.LayoutNodes.Select(x => x.Sequence).Distinct().Count() != document.LayoutNodes.Count)
            throw new InvalidDataException("The local duct-system file contains invalid or duplicate layout IDs.");

        foreach (var fitting in document.Fittings)
        {
            fitting.Inputs ??= new Dictionary<string, double>();
            if (FittingCatalog.TryMapLegacyCode(fitting.FittingCode, out var ashraeCode))
                fitting.FittingCode = ashraeCode;
            try
            {
                var definition = FittingCatalog.Find(fitting.FittingCode);
                foreach (var input in definition.Inputs)
                    fitting.Inputs.TryAdd(input.Key, input.DefaultValue);
            }
            catch (KeyNotFoundException)
            {
                // Retain unknown legacy records for inspection instead of guessing a replacement fitting.
            }
        }

        foreach (var duct in document.Ducts)
        {
            duct.AirSystemType = NormalizedText(duct.AirSystemType, document.DefaultAirSystemType);
            duct.BranchId = NormalizedText(duct.BranchId, document.DefaultBranchId);
            duct.DeltaXM = FiniteOrZero(duct.DeltaXM);
            duct.DeltaYM = FiniteOrZero(duct.DeltaYM);
            duct.DeltaZM = FiniteOrZero(duct.DeltaZM);
        }
        foreach (var fitting in document.Fittings)
        {
            fitting.AirSystemType = NormalizedText(fitting.AirSystemType, document.DefaultAirSystemType);
            fitting.BranchId = NormalizedText(fitting.BranchId, document.DefaultBranchId);
            fitting.DeltaXM = FiniteOrZero(fitting.DeltaXM);
            fitting.DeltaYM = FiniteOrZero(fitting.DeltaYM);
            fitting.DeltaZM = FiniteOrZero(fitting.DeltaZM);
        }
        foreach (var node in document.LayoutNodes)
        {
            node.AirSystemType = NormalizedText(node.AirSystemType, document.DefaultAirSystemType);
            node.BranchId = NormalizedText(node.BranchId, document.DefaultBranchId);
            node.GrdType = NormalizedText(node.GrdType, "Grille");
            if (string.Equals(node.GrdType, "Linear Diffuser TBD-80", StringComparison.OrdinalIgnoreCase))
                node.GrdType = "Linear Diffuser TBD-80 D";
            if (node.Type == LayoutNodeType.Grd && node.GrdType is not ("Grille" or "Diffuser" or "Linear Diffuser TBD-80 D"))
                node.GrdType = "Grille";
            node.Description = NormalizedText(node.Description,
                node.Type == LayoutNodeType.CustomComponent ? "Custom Component" : string.Empty);
            if (!GrdPerformanceCatalog.TitusLengthsFt.Contains(Convert.ToInt32(node.GrdLengthFt)))
                node.GrdLengthFt = 2;
            if (!GrdPerformanceCatalog.TitusSlotWidthsIn.Any(value =>
                    Math.Abs(value - node.GrdSlotWidthIn) < 1e-9))
                node.GrdSlotWidthIn = 0.75;
            if (!GrdPerformanceCatalog.TitusSlotCounts.Contains(node.GrdSlotCount))
                node.GrdSlotCount = 1;
            if (!GrdPerformanceCatalog.TitusInletDiametersIn.Contains(Convert.ToInt32(node.GrdInletDiameterIn)))
                node.GrdInletDiameterIn = 6;
            if (!GrdPerformanceCatalog.Titus350RSizes.Contains(node.GrdNominalDuctSizeIn,
                    StringComparer.OrdinalIgnoreCase))
                node.GrdNominalDuctSizeIn = "12x12";
            node.SpaceRole = NormalizedText(node.SpaceRole, "Space Sink");
            if (string.Equals(node.SpaceRole, "Supply Discharge", StringComparison.OrdinalIgnoreCase))
                node.SpaceRole = "Space Sink";
            else if (string.Equals(node.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase))
                node.SpaceRole = "Space Source";
            node.CalculationPointType = NormalizedText(node.CalculationPointType, "Pressure");
            if (FittingCatalog.IsTerminalSelectionGroup(node.FittingGroup))
                node.FittingGroup = FittingCatalog.TerminalSelectionGroup;
            node.CalculationConnectionNodeId = node.CalculationConnectionNodeId?.Trim() ?? string.Empty;
            node.CalculationConnectionFraction = double.IsFinite(node.CalculationConnectionFraction)
                ? Math.Clamp(node.CalculationConnectionFraction, 0.0, 1.0)
                : 0.5;
            node.PressurePa = FiniteOrZero(node.PressurePa);
            node.FanStaticPressurePa = Math.Max(0.0, FiniteOrZero(node.FanStaticPressurePa));
            node.FanEfficiencyPercent = double.IsFinite(node.FanEfficiencyPercent)
                                        && node.FanEfficiencyPercent > 0.0
                ? Math.Clamp(node.FanEfficiencyPercent, 0.1, 100.0)
                : 70.0;
            node.VelocityMPerS = FiniteOrZero(node.VelocityMPerS);
            node.DeltaXM = FiniteOrZero(node.DeltaXM);
            node.DeltaYM = FiniteOrZero(node.DeltaYM);
            node.DeltaZM = FiniteOrZero(node.DeltaZM);
            node.SecondaryDeltaXM = FiniteOrZero(node.SecondaryDeltaXM);
            node.SecondaryDeltaYM = FiniteOrZero(node.SecondaryDeltaYM);
            node.SecondaryDeltaZM = FiniteOrZero(node.SecondaryDeltaZM);
            node.ElbowAngleDegrees = double.IsFinite(node.ElbowAngleDegrees)
                ? Math.Clamp(node.ElbowAngleDegrees, 0.0, 180.0)
                : 90.0;
            node.AbsoluteAnchorXM = FiniteOrZero(node.AbsoluteAnchorXM);
            node.AbsoluteAnchorYM = FiniteOrZero(node.AbsoluteAnchorYM);
            node.AbsoluteAnchorZM = FiniteOrZero(node.AbsoluteAnchorZM);
            if (node.Type == LayoutNodeType.Fitting && string.IsNullOrWhiteSpace(node.FittingGroup))
            {
                var fitting = node.LinkedFittingSequence is int sequence
                    ? document.Fittings.FirstOrDefault(x => x.Sequence == sequence)
                    : null;
                node.FittingGroup = fitting is null
                    ? FittingCatalog.FanExitsAndPlenumsSelectionGroup
                    : FittingCatalog.SelectionGroup(FittingCatalog.Find(fitting.FittingCode));
            }
        }

        document.CoordinateOriginNodeId = document.CoordinateOriginNodeId?.Trim() ?? string.Empty;
        if (document.LayoutNodes.All(x => !string.Equals(x.Id, document.CoordinateOriginNodeId,
                StringComparison.OrdinalIgnoreCase)))
            document.CoordinateOriginNodeId = document.LayoutNodes
                .Where(x => x.Type != LayoutNodeType.CalculationPoint)
                .OrderBy(x => x.Sequence).Select(x => x.Id).FirstOrDefault() ?? string.Empty;
        document.CoordinateOriginXM = FiniteOrZero(document.CoordinateOriginXM);
        document.CoordinateOriginYM = FiniteOrZero(document.CoordinateOriginYM);
        document.CoordinateOriginZM = FiniteOrZero(document.CoordinateOriginZM);
        document.LayoutFeetPerGridCell = double.IsFinite(document.LayoutFeetPerGridCell)
                                           && document.LayoutFeetPerGridCell > 0.0
            ? Math.Clamp(document.LayoutFeetPerGridCell, 0.1, 1000.0)
            : 1.0;

        var highestExisting = document.Ducts.Count == 0 ? 0 : document.Ducts.Max(x => x.Sequence);
        document.NextSequence = Math.Max(document.NextSequence, highestExisting + 1);
        var highestFitting = document.Fittings.Count == 0 ? 0 : document.Fittings.Max(x => x.Sequence);
        document.NextFittingSequence = Math.Max(document.NextFittingSequence, highestFitting + 1);
        var highestLayout = document.LayoutNodes.Count == 0 ? 0 : document.LayoutNodes.Max(x => x.Sequence);
        document.NextLayoutSequence = Math.Max(document.NextLayoutSequence, highestLayout + 1);
        var highestBranch = document.LayoutNodes
            .Select(x => x.BranchId)
            .Where(x => x.StartsWith("B-", StringComparison.OrdinalIgnoreCase))
            .Select(x => int.TryParse(x.AsSpan(2), out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
        document.NextBranchSequence = Math.Max(document.NextBranchSequence, highestBranch + 1);
        return document;
    }

    private static void MigrateTypedLayoutIds(DuctSystemDocument document)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.LayoutNodes)
            replacements[$"L-{node.Sequence:000}"] = node.Id;

        static string Replace(string? value, IReadOnlyDictionary<string, string> replacements)
        {
            var normalized = value?.Trim() ?? string.Empty;
            return replacements.TryGetValue(normalized, out var replacement) ? replacement : normalized;
        }

        foreach (var node in document.LayoutNodes)
        {
            node.ParentNodeId = Replace(node.ParentNodeId, replacements);
            node.SecondaryParentNodeId = Replace(node.SecondaryParentNodeId, replacements);
            node.CalculationConnectionNodeId = Replace(node.CalculationConnectionNodeId, replacements);
        }
        document.CoordinateOriginNodeId = Replace(document.CoordinateOriginNodeId, replacements);
    }

    private static void MigrateFanLayoutIds(DuctSystemDocument document)
    {
        var replacements = document.LayoutNodes
            .Where(x => x.Type == LayoutNodeType.Fan)
            .ToDictionary(x => $"C-{x.Sequence:000}", x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (replacements.Count == 0)
            return;

        static string Replace(string? value, IReadOnlyDictionary<string, string> replacements)
        {
            var normalized = value?.Trim() ?? string.Empty;
            return replacements.TryGetValue(normalized, out var replacement) ? replacement : normalized;
        }

        foreach (var node in document.LayoutNodes)
        {
            node.ParentNodeId = Replace(node.ParentNodeId, replacements);
            node.SecondaryParentNodeId = Replace(node.SecondaryParentNodeId, replacements);
            node.CalculationConnectionNodeId = Replace(node.CalculationConnectionNodeId, replacements);
        }
        document.CoordinateOriginNodeId = Replace(document.CoordinateOriginNodeId, replacements);
    }

    private static void MigrateCalculationPointsToDuctAnnotations(DuctSystemDocument document)
    {
        foreach (var point in document.LayoutNodes.Where(x => x.Type == LayoutNodeType.CalculationPoint).ToArray())
        {
            var parentId = point.ParentNodeId;
            if (string.IsNullOrWhiteSpace(parentId))
                continue;

            var child = document.LayoutNodes
                .Where(x => string.Equals(x.ParentNodeId, point.Id, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.SecondaryParentNodeId, point.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Sequence)
                .FirstOrDefault();
            if (child is null)
                continue;

            var secondary = string.Equals(child.SecondaryParentNodeId, point.Id, StringComparison.OrdinalIgnoreCase);
            var incomingSequence = point.ConnectionDuctSequence;
            var outgoingSequence = secondary ? child.SecondaryConnectionDuctSequence : child.ConnectionDuctSequence;
            var retainedSequence = incomingSequence ?? outgoingSequence;
            var incomingLength = incomingSequence is int incoming
                ? document.Ducts.FirstOrDefault(x => x.Sequence == incoming)?.LengthM ?? 0.0
                : 0.0;
            var outgoingLength = outgoingSequence is int outgoing
                ? document.Ducts.FirstOrDefault(x => x.Sequence == outgoing)?.LengthM ?? 0.0
                : 0.0;

            if (secondary)
            {
                child.SecondaryParentNodeId = parentId;
                child.SecondaryConnectionDuctSequence = retainedSequence;
            }
            else
            {
                child.ParentNodeId = parentId;
                child.ConnectionDuctSequence = retainedSequence;
            }

            child.DeltaXM += point.DeltaXM;
            child.DeltaYM += point.DeltaYM;
            child.DeltaZM += point.DeltaZM;
            if (retainedSequence is int retained)
            {
                var duct = document.Ducts.FirstOrDefault(x => x.Sequence == retained);
                if (duct is not null)
                {
                    duct.DeltaXM = child.DeltaXM;
                    duct.DeltaYM = child.DeltaYM;
                    duct.DeltaZM = child.DeltaZM;
                    var combinedLength = incomingLength + outgoingLength;
                    if (combinedLength > 0)
                        duct.LengthM = combinedLength;
                }
            }

            point.CalculationConnectionNodeId = child.Id;
            point.CalculationConnectionSecondary = secondary;
            point.CalculationConnectionFraction = incomingLength + outgoingLength > 0
                ? Math.Clamp(incomingLength / (incomingLength + outgoingLength), 0.0, 1.0)
                : 0.5;
            point.ObservedDuctSequence = retainedSequence;
            point.ParentNodeId = string.Empty;
            point.SecondaryParentNodeId = string.Empty;
            point.ConnectionDuctSequence = null;
            point.SecondaryConnectionDuctSequence = null;
            point.DeltaXM = 0;
            point.DeltaYM = 0;
            point.DeltaZM = 0;
            point.IsDefined = true;

            var discarded = new[] { incomingSequence, outgoingSequence }
                .Where(x => x.HasValue && x != retainedSequence)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();
            foreach (var sequence in discarded)
            {
                var stillReferenced = document.LayoutNodes.Any(x => x.ConnectionDuctSequence == sequence
                                                                      || x.SecondaryConnectionDuctSequence == sequence
                                                                      || x.LinkedDuctSequence == sequence
                                                                      || x.ObservedDuctSequence == sequence);
                if (!stillReferenced)
                    document.Ducts.RemoveAll(x => x.Sequence == sequence);
            }
        }
    }

    private static string NormalizedText(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;

    public static void Save(string path, DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A local storage directory is required.", nameof(path));

        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var backupPath = path + ".bak";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
        if (File.Exists(path))
            File.Copy(path, backupPath, overwrite: true);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
