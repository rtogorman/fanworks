using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// When true, Auto Size Ductwork keeps this duct's shape and size (round Y). Its airflow, velocity and pressure
    /// loss are still recalculated from the system's sources, like any other duct.
    /// </summary>
    public bool SizeLocked { get; set; }

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
    /// <summary>Saved-format version written by this build (see <see cref="DuctSystemStore.Load"/> for upgrades).</summary>
    public const int CurrentSchemaVersion = 23;

    /// <summary>Saved-format version of this document.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Free-text notes the engineer keeps with individual items (Selection Inspector, Notes tab), keyed by the
    /// item key used throughout the system model (<see cref="CriticalPathAnalyzer.DuctKey"/>,
    /// <see cref="CriticalPathAnalyzer.FittingKey"/>, <see cref="CriticalPathAnalyzer.LayoutKey"/>). Keeping them
    /// here rather than on each record means re-sizing or re-creating a duct or fitting with the same ID never loses
    /// its notes. Notes of deleted items are dropped when the project is saved. Schema 15.
    /// </summary>
    public Dictionary<string, string> ItemNotes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Expected ranges the engineer enters in Analysis Settings (duct friction rate and velocity, fitting velocity
    /// and loss), used only to flag items on the Analysis tab. Empty by default. Schema 16.
    /// </summary>
    public AnalysisExpectations AnalysisExpectations { get; set; } = new();

    /// <summary>
    /// Designer labels the engineer dragged away from their default place, and their leaders, keyed by
    /// <see cref="LayoutLabelPlacement.NodeKey"/> or <see cref="LayoutLabelPlacement.ConnectionKey"/>. Schema 17.
    /// </summary>
    public Dictionary<string, LayoutLabelPlacement> LayoutLabelPlacements { get; set; } = new(StringComparer.Ordinal);
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

/// <summary>
/// Where a Designer label sits relative to its default place, and its optional leader. Offsets are screen pixels at
/// 100 % zoom (they scale with zoom). The leader runs from the label to a point offset from the item's reference
/// point (a component's centre or a duct's midpoint).
/// </summary>
public sealed class LayoutLabelPlacement
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool HasLeader { get; set; }
    public double LeaderX { get; set; }
    public double LeaderY { get; set; }

    public bool IsFinite => double.IsFinite(OffsetX) && double.IsFinite(OffsetY)
                            && double.IsFinite(LeaderX) && double.IsFinite(LeaderY);

    /// <summary>Key of a component's label (by its sequence, which never changes).</summary>
    public static string NodeKey(int nodeSequence) => $"N:{nodeSequence}";

    /// <summary>Key of a duct connection's label (the downstream component and which inlet).</summary>
    public static string ConnectionKey(int downstreamSequence, bool secondary) => $"C:{downstreamSequence}:{(secondary ? 2 : 1)}";

    public LayoutLabelPlacement Copy() => new()
    {
        OffsetX = OffsetX, OffsetY = OffsetY, HasLeader = HasLeader, LeaderX = LeaderX, LeaderY = LeaderY
    };
}

public enum LayoutViewMode
{
    Isometric,
    IsometricNegative,
    TopDown
}

/// <summary>
/// Fan-only data of a layout component (schema 14). From schema 18 the same block also describes an air handling
/// unit (AHU): a fan inside a cabinet with its own filters, coils and other internal components.
/// </summary>
public sealed class LayoutFanData
{
    /// <summary>Toolbox tag that places a fan component as an air handling unit.</summary>
    public const string AirHandlingUnitTool = "Air Handling Unit";

    /// <summary>
    /// Static pressure the fan adds to the duct system (Pa), 0 or more. For an air handling unit this is the
    /// external static pressure (ESP): the pressure available to the ductwork outside the unit. Auto-size sets it
    /// to the critical-path pressure loss.
    /// </summary>
    public double StaticPressurePa { get; set; }

    /// <summary>Fan total efficiency (%), above 0 and at most 100.</summary>
    public double EfficiencyPercent { get; set; } = 70.0;

    /// <summary>
    /// True when the component is an air handling unit rather than a bare fan (schema 18). Set when the component is
    /// placed and not changed afterwards, because the component ID ("AHU-" or "Fan-") depends on it.
    /// </summary>
    public bool IsAirHandlingUnit { get; set; }

    /// <summary>
    /// Internal static pressure (Pa), 0 or more (schema 18; fans as well as air handling units from v0.103): the
    /// pressure drop across the unit's own filters, coils and other internal components, as an input from the
    /// manufacturer's data. Normally 0 for a bare fan. It is not part of the duct system pressure; it is added to the
    /// external static pressure to give the fan's total static pressure.
    /// </summary>
    public double InternalStaticPressurePa { get; set; }
}

/// <summary>Grille, register, diffuser or VAV terminal data of a layout component (schema 14).</summary>
public sealed class LayoutGrdData
{
    public string Type { get; set; } = "Grille";
    public double FaceSizeIn { get; set; } = 24;
    public double NeckDiameterIn { get; set; } = 8;
    public double LengthFt { get; set; } = 2;
    public double SlotWidthIn { get; set; } = 0.75;
    public int SlotCount { get; set; } = 1;
    public double InletDiameterIn { get; set; } = 6;
    public double SelectedAirflowCfm { get; set; }
    public string SoundNc { get; set; } = string.Empty;
    public string ThrowFt { get; set; } = string.Empty;
    public string NominalDuctSizeIn { get; set; } = "12x12";
}

/// <summary>Space boundary (source or sink) data of a layout component (schema 14).</summary>
public sealed class LayoutSpaceData
{
    /// <summary>"Space Source" or "Space Sink" (legacy "Return Pickup" / "Supply Discharge" accepted).</summary>
    public string Role { get; set; } = "Supply Discharge";
}

/// <summary>Calculation point data (schema 14): which connection it observes and where along it.</summary>
public sealed class LayoutCalculationPointData
{
    public string PointType { get; set; } = "Pressure";
    public string ConnectionNodeId { get; set; } = string.Empty;
    public bool ConnectionSecondary { get; set; }
    public double ConnectionFraction { get; set; } = 0.5;
    public int? ObservedDuctSequence { get; set; }
}

/// <summary>Fitting component data (schema 14).</summary>
public sealed class LayoutFittingNodeData
{
    public string Group { get; set; } = string.Empty;
    public double ElbowAngleDegrees { get; set; } = 90.0;
}

/// <summary>
/// One Layout Designer component. Common data (identity, connections, airflow, temperature, position) is stored on
/// the component; data that applies to one component type only is stored in that type's block (<c>Fan</c>,
/// <c>Grd</c>, <c>Space</c>, <c>CalculationPoint</c>, <c>Fitting</c>), and only the block for the component's own
/// <see cref="Type"/> is written to the file (schema 14). The flat properties (for example
/// <see cref="FanStaticPressurePa"/>) read and write those blocks and are kept so existing code is unchanged.
/// </summary>
public sealed class SavedLayoutNode
{
    private LayoutFanData _fan = new();
    private LayoutGrdData _grd = new();
    private LayoutSpaceData _space = new();
    private LayoutCalculationPointData _calculationPoint = new();
    private LayoutFittingNodeData _fitting = new();

    /// <summary>Fan block; written only for fans.</summary>
    [JsonPropertyName("Fan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutFanData? FanData { get => Type == LayoutNodeType.Fan ? _fan : null; set => _fan = value ?? new(); }

    /// <summary>GRD/terminal block; written only for GRDs.</summary>
    [JsonPropertyName("Grd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutGrdData? GrdData { get => Type == LayoutNodeType.Grd ? _grd : null; set => _grd = value ?? new(); }

    /// <summary>Space block; written only for space sources and sinks.</summary>
    [JsonPropertyName("Space")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutSpaceData? SpaceData { get => Type == LayoutNodeType.Reservoir ? _space : null; set => _space = value ?? new(); }

    /// <summary>Calculation point block; written only for calculation points.</summary>
    [JsonPropertyName("CalculationPoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutCalculationPointData? CalculationPointData
    {
        get => Type == LayoutNodeType.CalculationPoint ? _calculationPoint : null;
        set => _calculationPoint = value ?? new();
    }

    /// <summary>Fitting block; written only for fittings.</summary>
    [JsonPropertyName("Fitting")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutFittingNodeData? FittingData { get => Type == LayoutNodeType.Fitting ? _fitting : null; set => _fitting = value ?? new(); }

    public int Sequence { get; set; }
    public LayoutNodeType Type { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string ParentNodeId { get; set; } = string.Empty;
    public string SecondaryParentNodeId { get; set; } = string.Empty;
    [JsonIgnore] public string FittingGroup { get => _fitting.Group; set => _fitting.Group = value; }
    public string AirSystemType { get; set; } = "Supply Air";
    public string BranchId { get; set; } = "Main";
    public double AirflowM3PerS { get; set; }
    public double TemperatureC { get; set; } = 21.1111111111;
    public double PressureDropPa { get; set; }
    [JsonIgnore] public double FanStaticPressurePa { get => _fan.StaticPressurePa; set => _fan.StaticPressurePa = value; }
    [JsonIgnore] public double FanEfficiencyPercent { get => _fan.EfficiencyPercent; set => _fan.EfficiencyPercent = value; }
    /// <summary>True for an air handling unit (a fan component placed with the AHU tool). See <see cref="LayoutFanData.IsAirHandlingUnit"/>.</summary>
    [JsonIgnore] public bool IsAirHandlingUnit { get => Type == LayoutNodeType.Fan && _fan.IsAirHandlingUnit; set => _fan.IsAirHandlingUnit = value; }
    /// <summary>Fan or air handling unit internal static pressure (Pa); see <see cref="LayoutFanData.InternalStaticPressurePa"/>.</summary>
    [JsonIgnore] public double FanInternalStaticPressurePa { get => _fan.InternalStaticPressurePa; set => _fan.InternalStaticPressurePa = value; }

    /// <summary>
    /// Total static pressure the fan must develop (Pa): the static pressure it adds to the duct system
    /// (<see cref="FanStaticPressurePa"/>, the external static pressure) plus its internal static pressure (0 unless
    /// entered; for a fan or an air handling unit alike). Brake horsepower is based on this value.
    /// </summary>
    [JsonIgnore]
    public double FanTotalStaticPressurePa => FanStaticPressurePa + (Type == LayoutNodeType.Fan ? FanInternalStaticPressurePa : 0.0);

    /// <summary>"Air Handling Unit" or "System Fan" for fans; empty for other components.</summary>
    [JsonIgnore]
    public string FanEquipmentName => Type != LayoutNodeType.Fan ? string.Empty
        : IsAirHandlingUnit ? "Air Handling Unit" : "System Fan";
    public double PressurePa { get; set; }
    public double VelocityMPerS { get; set; }
    [JsonIgnore] public string GrdType { get => _grd.Type; set => _grd.Type = value; }
    public string Description { get; set; } = string.Empty;
    [JsonIgnore] public double GrdFaceSizeIn { get => _grd.FaceSizeIn; set => _grd.FaceSizeIn = value; }
    [JsonIgnore] public double GrdNeckDiameterIn { get => _grd.NeckDiameterIn; set => _grd.NeckDiameterIn = value; }
    [JsonIgnore] public double GrdLengthFt { get => _grd.LengthFt; set => _grd.LengthFt = value; }
    [JsonIgnore] public double GrdSlotWidthIn { get => _grd.SlotWidthIn; set => _grd.SlotWidthIn = value; }
    [JsonIgnore] public int GrdSlotCount { get => _grd.SlotCount; set => _grd.SlotCount = value; }
    [JsonIgnore] public double GrdInletDiameterIn { get => _grd.InletDiameterIn; set => _grd.InletDiameterIn = value; }
    [JsonIgnore] public double GrdSelectedAirflowCfm { get => _grd.SelectedAirflowCfm; set => _grd.SelectedAirflowCfm = value; }
    [JsonIgnore] public string GrdSoundNc { get => _grd.SoundNc; set => _grd.SoundNc = value; }
    [JsonIgnore] public string GrdThrowFt { get => _grd.ThrowFt; set => _grd.ThrowFt = value; }
    [JsonIgnore] public string GrdNominalDuctSizeIn { get => _grd.NominalDuctSizeIn; set => _grd.NominalDuctSizeIn = value; }
    [JsonIgnore] public string SpaceRole { get => _space.Role; set => _space.Role = value; }
    [JsonIgnore] public string CalculationPointType { get => _calculationPoint.PointType; set => _calculationPoint.PointType = value; }
    [JsonIgnore] public string CalculationConnectionNodeId { get => _calculationPoint.ConnectionNodeId; set => _calculationPoint.ConnectionNodeId = value; }
    [JsonIgnore] public bool CalculationConnectionSecondary { get => _calculationPoint.ConnectionSecondary; set => _calculationPoint.ConnectionSecondary = value; }
    [JsonIgnore] public double CalculationConnectionFraction { get => _calculationPoint.ConnectionFraction; set => _calculationPoint.ConnectionFraction = value; }
    [JsonIgnore] public int? ObservedDuctSequence { get => _calculationPoint.ObservedDuctSequence; set => _calculationPoint.ObservedDuctSequence = value; }
    public double DeltaXM { get; set; }
    public double DeltaYM { get; set; }
    public double DeltaZM { get; set; }
    public double SecondaryDeltaXM { get; set; }
    public double SecondaryDeltaYM { get; set; }
    public double SecondaryDeltaZM { get; set; }
    [JsonIgnore] public double ElbowAngleDegrees { get => _fitting.ElbowAngleDegrees; set => _fitting.ElbowAngleDegrees = value; }
    /// <summary>Absolute X position (m) of the component. Stored from schema 13; authoritative when the file is loaded.</summary>
    public double XM { get; set; }

    /// <summary>Absolute Y position (m). See <see cref="XM"/>.</summary>
    public double YM { get; set; }

    /// <summary>Absolute Z position (elevation, m). See <see cref="XM"/>.</summary>
    public double ZM { get; set; }

    /// <summary>True when <see cref="XM"/>, <see cref="YM"/> and <see cref="ZM"/> hold a defined position.</summary>
    public bool HasStoredPosition { get; set; }

    public bool HasAbsoluteCoordinateAnchor { get; set; }
    public double AbsoluteAnchorXM { get; set; }
    public double AbsoluteAnchorYM { get; set; }
    public double AbsoluteAnchorZM { get; set; }
    public int? LinkedDuctSequence { get; set; }
    public int? LinkedFittingSequence { get; set; }
    public int? ConnectionDuctSequence { get; set; }
    public int? SecondaryConnectionDuctSequence { get; set; }
    /// <summary>
    /// True when the connection into this component was drawn as a flexible duct (Flex Duct Segment, round Z): its
    /// duct is sized and calculated as fully extended flexible duct. Schema 23; false when absent.
    /// </summary>
    public bool ConnectionFlexDuct { get; set; }
    /// <summary>See <see cref="ConnectionFlexDuct"/>, for the second inlet of a converging fitting.</summary>
    public bool SecondaryConnectionFlexDuct { get; set; }
    /// <summary>
    /// No longer used (round Y): flow runs from each connection's upstream item to its downstream item, set by the
    /// drawn sources and sinks. Kept so older files load; cleared when a file is loaded (schema 22).
    /// </summary>
    public bool ConnectionFlowReversed { get; set; }
    /// <summary>See <see cref="ConnectionFlowReversed"/>.</summary>
    public bool SecondaryConnectionFlowReversed { get; set; }
    /// <summary>
    /// When true, Auto Size Ductwork keeps this component's size and selection (round Y): a fitting keeps its
    /// reference size even when the connected duct is re-sized, a GRD keeps its selection and pressure drop. Its
    /// airflow is still recalculated from the system's sources.
    /// </summary>
    public bool SizeLocked { get; set; }
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
        LayoutNodeType.Fan when _fan.IsAirHandlingUnit => "AHU",
        LayoutNodeType.Fan => "Fan",
        _ => "C"
    };
}

public readonly record struct LayoutCoordinate(double XM, double YM, double ZM);

/// <summary>
/// Derives absolute X/Y/Z positions (metres) for Layout Designer components from the stored offsets.
/// </summary>
/// <remarks>
/// Each component stores its offset from its upstream component (<see cref="SavedLayoutNode.DeltaXM"/> etc., and
/// the <c>Secondary*</c> offsets for a converging fitting's second inlet). Starting at the coordinate origin
/// component (<see cref="DuctSystemDocument.CoordinateOriginNodeId"/>, else the lowest sequence) placed at the
/// document's origin coordinates, the resolver walks connections breadth-first, upstream and downstream, visiting
/// components in document order, and keeps the first position reached for each component. Fragments that are not
/// connected to the origin are placed from their absolute anchor when they have one; others get no position.
/// Stored absolute positions (schema 13) are applied when a file is loaded, by rebuilding these offsets from them
/// (<see cref="LayoutPositions.DeriveOffsetsFromAbsolute"/>), so the resolver always works from current offsets.
/// Calculation points are placed by their fraction along the connection they observe. The result is deterministic
/// for a given document. Offsets that disagree around a loop are not resolved here: <see cref="LayoutGeometryCheck"/>
/// reports them.
/// </remarks>
public static class LayoutCoordinateResolver
{
    /// <summary>Absolute positions by component sequence (components with no defined position are omitted).</summary>
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

    /// <summary>
    /// True when the user entered the reference size by hand (schema 21). Otherwise a fitting drawn in the system
    /// takes its reference size, and a transition its area ratio, from the connected ducts each time the system is
    /// solved (<see cref="SystemSolver"/>), so a default size can never stand in for the real section.
    /// </summary>
    public bool ReferenceSizeManual { get; set; }

    /// <summary>
    /// The ID of the layout component this record is linked to, set by <see cref="SystemSolver.Solve"/>. A linked
    /// fitting is shown by its layout ID everywhere, so the drawing, tables and report use one name for one item.
    /// </summary>
    [JsonIgnore]
    public string? LayoutId { get; set; }

    /// <summary>
    /// The duct the reference size was taken from (set by the solver), or null when the size is entered by hand or
    /// the fitting is not drawn in the system.
    /// </summary>
    [JsonIgnore]
    public string? ReferenceSizeSource { get; set; }

    [JsonIgnore]
    public string Id => !string.IsNullOrWhiteSpace(CustomId) ? CustomId.Trim()
        : !string.IsNullOrWhiteSpace(LayoutId) ? LayoutId
        : $"F-{Sequence:000}";

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
        // A fitting needs its own airflow and reference section. Missing either is an invalid input, never a
        // reason to reuse a stored velocity from an earlier definition (review P2-18).
        var geometry = CreateGeometry()
                       ?? throw new InvalidOperationException($"{Id} has no reference duct size.");
        if (!(AirflowM3PerS > 0.0) || !double.IsFinite(AirflowM3PerS))
            throw new InvalidOperationException($"{Id} has no airflow (it is not connected to the system or carries no air).");
        return FittingCalculator.Calculate(definition, Inputs, AirflowM3PerS, geometry, TemperatureC,
            RelativeHumidity, AtmosphericPressurePa);
    }

    /// <summary>
    /// Calculates a junction fitting for a specific ASHRAE path (common-to-straight or common-to-branch), using the
    /// stored leg airflows and geometry. A two-path junction has a different coefficient on each path, so a route
    /// through the branch outlet must not be charged the straight-path loss. Non-junction fittings ignore
    /// <paramref name="path"/>.
    /// </summary>
    public FittingCalculationResult CalculateForPath(JunctionPath path)
    {
        var definition = FittingCatalog.Find(FittingCode);
        if (!definition.IsJunction || Junction is null)
            return Calculate();
        return FittingCalculator.Calculate(definition, Inputs, Junction with { Path = path }, TemperatureC,
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
        // Calculated pressures are NaN downstream of an item that cannot be calculated ("not available").
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// A fingerprint of the whole document (SHA-256 of its saved form): equal fingerprints mean the saved project,
    /// including calculated values, is the same. Used to skip rebuilding a report preview that is already current.
    /// </summary>
    public static string Fingerprint(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    public static DuctSystemDocument Load(string path)
    {
        if (!File.Exists(path))
            return new DuctSystemDocument();

        var json = File.ReadAllText(path);
        var root = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidDataException("The local duct-system file is empty or invalid.");
        // Schema 14 moved type-specific component fields into per-type blocks. Older files store them flat on
        // every component; move them before reading (no value changes).
        if (root is JsonObject rootObject && ReadSchemaVersion(rootObject) < 14)
            MoveFlatComponentFieldsIntoTypeBlocks(rootObject);
        var document = root.Deserialize<DuctSystemDocument>(JsonOptions)
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
        document.LayoutLabelPlacements = document.LayoutLabelPlacements is null
            ? new Dictionary<string, LayoutLabelPlacement>(StringComparer.Ordinal)
            : new Dictionary<string, LayoutLabelPlacement>(
                document.LayoutLabelPlacements.Where(pair => pair.Value is not null && pair.Value.IsFinite),
                StringComparer.Ordinal);
        document.AnalysisExpectations ??= new AnalysisExpectations();
        document.AnalysisExpectations.Normalize();
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
        // Version 13: absolute X/Y/Z positions are stored and are authoritative. Older files only have offsets;
        // their absolute positions are derived from those offsets below (no component moves).
        var positionsFromOffsets = document.SchemaVersion == 12;
        if (document.SchemaVersion == 12)
            document.SchemaVersion = 13;
        // Version 14: per-type component blocks (moved above, before reading).
        if (document.SchemaVersion == 13)
            document.SchemaVersion = 14;
        // Version 15: optional per-item notes (ItemNotes; empty when absent).
        if (document.SchemaVersion == 14)
            document.SchemaVersion = 15;
        // Version 16: optional expected ranges for the Analysis tab (AnalysisExpectations; empty when absent).
        if (document.SchemaVersion == 15)
            document.SchemaVersion = 16;
        // Version 17: duct class overrides (AnalysisExpectations) and dragged Designer labels with leaders
        // (LayoutLabelPlacements); both empty when absent.
        if (document.SchemaVersion == 16)
            document.SchemaVersion = 17;
        // Version 18: air handling units (Fan block IsAirHandlingUnit and InternalStaticPressurePa); a fan without
        // them is a plain fan with no internal static pressure, exactly as before.
        if (document.SchemaVersion == 17)
            document.SchemaVersion = 18;
        // Version 19: VAV inlet static pressure and VAV pressure drop expected ranges (empty when absent); fans may
        // carry an internal static pressure (0 when absent, as before).
        if (document.SchemaVersion == 18)
            document.SchemaVersion = 19;
        // Version 20: suction-side (negative) pressure class and seal class overrides (none when absent); the existing
        // overrides now apply to the discharge (positive) side, which is the whole system when nothing is upstream of
        // the fan.
        if (document.SchemaVersion == 19)
            document.SchemaVersion = 20;
        // Version 21: fittings record whether their reference size was entered by hand (ReferenceSizeManual; false
        // when absent). Fittings in older files take their size from the connected ducts when the system is solved,
        // which replaces default sizes that were never matched to the drawing (review P0-03, P1-05).
        if (document.SchemaVersion == 20)
            document.SchemaVersion = 21;
        // Version 22: ducts and components can be locked against Auto Size (SizeLocked; false when absent). Flow
        // direction can no longer be reversed by hand: it follows the drawn sources and sinks, so stored reversal
        // flags are cleared (they only changed how a connection was labelled, never the calculation).
        if (document.SchemaVersion == 21)
        {
            foreach (var node in document.LayoutNodes)
            {
                node.ConnectionFlowReversed = false;
                node.SecondaryConnectionFlowReversed = false;
            }
            document.SchemaVersion = 22;
        }
        // Version 23 (round Z): a connection can be drawn as flexible duct (ConnectionFlexDuct; false when absent).
        // Elbow angles follow the drawn connections; nothing stored changes.
        if (document.SchemaVersion == 22)
            document.SchemaVersion = 23;
        if (document.SchemaVersion != DuctSystemDocument.CurrentSchemaVersion)
            throw new InvalidDataException(document.SchemaVersion > DuctSystemDocument.CurrentSchemaVersion
                ? $"This project was saved by a newer version of FanWorks (file format {document.SchemaVersion}). Update FanWorks to open it."
                : $"Unsupported duct-system file version {document.SchemaVersion}.");
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
            node.FanInternalStaticPressurePa = Math.Max(0.0, FiniteOrZero(node.FanInternalStaticPressurePa));
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
        if (!positionsFromOffsets)
            LayoutPositions.DeriveOffsetsFromAbsolute(document);
        LayoutPositions.StoreAbsolute(document);
        if (document.ItemNotes is null)
            document.ItemNotes = new Dictionary<string, string>(StringComparer.Ordinal);
        else if (document.ItemNotes.Comparer != StringComparer.Ordinal)
            document.ItemNotes = new Dictionary<string, string>(document.ItemNotes, StringComparer.Ordinal);
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

    private static readonly (string Legacy, string Block, string Name)[] FlatComponentFields =
    {
        ("FanStaticPressurePa", "Fan", "StaticPressurePa"), ("FanEfficiencyPercent", "Fan", "EfficiencyPercent"),
        ("GrdType", "Grd", "Type"), ("GrdFaceSizeIn", "Grd", "FaceSizeIn"), ("GrdNeckDiameterIn", "Grd", "NeckDiameterIn"),
        ("GrdLengthFt", "Grd", "LengthFt"), ("GrdSlotWidthIn", "Grd", "SlotWidthIn"), ("GrdSlotCount", "Grd", "SlotCount"),
        ("GrdInletDiameterIn", "Grd", "InletDiameterIn"), ("GrdSelectedAirflowCfm", "Grd", "SelectedAirflowCfm"),
        ("GrdSoundNc", "Grd", "SoundNc"), ("GrdThrowFt", "Grd", "ThrowFt"), ("GrdNominalDuctSizeIn", "Grd", "NominalDuctSizeIn"),
        ("SpaceRole", "Space", "Role"),
        ("CalculationPointType", "CalculationPoint", "PointType"),
        ("CalculationConnectionNodeId", "CalculationPoint", "ConnectionNodeId"),
        ("CalculationConnectionSecondary", "CalculationPoint", "ConnectionSecondary"),
        ("CalculationConnectionFraction", "CalculationPoint", "ConnectionFraction"),
        ("ObservedDuctSequence", "CalculationPoint", "ObservedDuctSequence"),
        ("FittingGroup", "Fitting", "Group"), ("ElbowAngleDegrees", "Fitting", "ElbowAngleDegrees")
    };

    private static int ReadSchemaVersion(JsonObject root)
    {
        foreach (var (key, value) in root)
        {
            if (string.Equals(key, nameof(DuctSystemDocument.SchemaVersion), StringComparison.OrdinalIgnoreCase)
                && value is JsonValue number && number.TryGetValue<int>(out var version))
                return version;
        }
        return 1;
    }

    /// <summary>Schema 13 and earlier: moves flat type-specific component fields into their per-type blocks.</summary>
    private static void MoveFlatComponentFieldsIntoTypeBlocks(JsonObject root)
    {
        var nodesKey = root.Select(pair => pair.Key)
            .FirstOrDefault(key => string.Equals(key, nameof(DuctSystemDocument.LayoutNodes), StringComparison.OrdinalIgnoreCase));
        if (nodesKey is null || root[nodesKey] is not JsonArray nodes)
            return;
        foreach (var node in nodes.OfType<JsonObject>())
        {
            foreach (var (legacy, block, name) in FlatComponentFields)
            {
                var key = node.Select(pair => pair.Key)
                    .FirstOrDefault(candidate => string.Equals(candidate, legacy, StringComparison.OrdinalIgnoreCase));
                if (key is null)
                    continue;
                var value = node[key];
                node.Remove(key);
                if (node[block] is not JsonObject target)
                {
                    target = new JsonObject();
                    node[block] = target;
                }
                target[name] = value;
            }
        }
    }

    /// <summary>Drops empty notes and notes whose item no longer exists.</summary>
    public static void PruneItemNotes(DuctSystemDocument document)
    {
        document.ItemNotes ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var liveKeys = document.Ducts.Select(x => CriticalPathAnalyzer.DuctKey(x.Sequence))
            .Concat(document.Fittings.Select(x => CriticalPathAnalyzer.FittingKey(x.Sequence)))
            .Concat(document.LayoutNodes.Select(x => CriticalPathAnalyzer.LayoutKey(x.Sequence)))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var key in document.ItemNotes.Keys.ToArray())
            if (!liveKeys.Contains(key) || string.IsNullOrWhiteSpace(document.ItemNotes[key]))
                document.ItemNotes.Remove(key);
    }

    public static void Save(string path, DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A local storage directory is required.", nameof(path));

        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        var backupPath = path + ".bak";
        // The saved absolute positions are the authoritative geometry: bring them up to date first.
        LayoutPositions.StoreAbsolute(document);
        PruneItemNotes(document);
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
        if (File.Exists(path))
            File.Copy(path, backupPath, overwrite: true);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
