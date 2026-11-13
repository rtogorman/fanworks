namespace DuctSizer.Core;

/// <summary>
/// Total, velocity and static pressure at the stations of the drawn system (review P0-01, P1-09).
/// </summary>
/// <remarks>
/// <para>
/// The system solver carries a running <b>total</b> pressure, gauge (relative to the still air of the spaces): it
/// starts at 0 in a source space, every duct, fitting and component subtracts its total-pressure loss (duct friction,
/// ASHRAE fitting coefficients C × Pv, entered equipment drops, catalog total pressure of air terminals) and the fan
/// adds its pressure. For low-speed, constant-density flow (ASHRAE Fundamentals, Ch. 21):
/// </para>
/// <para>Pt = Ps + Pv, Pv = ρV²/2, so Ps = Pt − Pv at any station.</para>
/// <para>
/// Static pressure therefore falls where air speeds up (a contraction, the entry from a space into a duct) and rises
/// where it slows down (static regain in an expansion) while total pressure only ever falls across a passive element.
/// The static pressure of a station is the total pressure there less the velocity pressure of the duct section at
/// that station: a duct's own section; for a fitting or component, the section leaving it; 0 in a space.
/// </para>
/// <para>
/// <b>Exit into a space.</b> Air leaving a bare duct into a space keeps its velocity pressure as a jet that is
/// dissipated in the room, so the duct end must hold Pt = Pv above the room: an exit loss of one velocity pressure
/// (ASHRAE fitting 2-1, abrupt exit, C = 1.0). It is charged at the sink when the air reaches it straight from a duct.
/// It is not charged when an air terminal (GRD), an ASHRAE exit fitting or a VAV-terminal-style outlet delivers the
/// air, because the catalog total pressure of a diffuser or grille, or the exit fitting's own coefficient, already
/// includes the discharge: the discharge energy is counted exactly once. Entry from a source space adds no total
/// pressure loss unless an entry fitting is drawn (its coefficient is then charged like any fitting); the static
/// pressure still drops by the duct's velocity pressure as the air accelerates.
/// </para>
/// </remarks>
public static class SystemPressures
{
    /// <summary>Velocity pressure ρV²/2 (Pa) in a duct's actual section, or NaN when the duct cannot be calculated.</summary>
    public static double DuctVelocityPressurePa(SavedDuct? duct)
    {
        if (duct is null)
            return double.NaN;
        try
        {
            var result = duct.Calculate();
            return 0.5 * result.Air.DensityKgPerM3 * result.ActualVelocityMPerS * result.ActualVelocityMPerS;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return double.NaN;
        }
    }

    /// <summary>Velocity pressure (Pa) of the duct feeding <paramref name="node"/> (its primary inlet); 0 for a space or a start component.</summary>
    public static double InletVelocityPressurePa(DuctSystemDocument document, SavedLayoutNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (node.Type == LayoutNodeType.Reservoir)
            return 0.0;
        var duct = LayoutTopology.DuctForConnection(document, node, false);
        return duct is null ? 0.0 : DuctVelocityPressurePa(duct);
    }

    /// <summary>
    /// Velocity pressure (Pa) of the duct leaving <paramref name="node"/> toward <paramref name="nextChild"/> (or its
    /// first outlet); 0 for a space or an end component.
    /// </summary>
    public static double OutletVelocityPressurePa(DuctSystemDocument document, SavedLayoutNode node, SavedLayoutNode? nextChild = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (node.Type == LayoutNodeType.Reservoir)
            return 0.0;
        var connection = LayoutTopology.ConnectionsFrom(document, node)
            .OrderBy(x => nextChild is not null && x.Downstream.Id == nextChild.Id ? 0 : LayoutTopology.IsMainOutlet(node, x.Downstream) ? 1 : 2)
            .ThenBy(x => x.Downstream.Sequence)
            .Cast<LayoutConnection?>()
            .FirstOrDefault();
        if (connection is not { } value)
            return 0.0;
        var duct = LayoutTopology.DuctForConnection(document, value.Downstream, value.Secondary);
        return duct is null ? 0.0 : DuctVelocityPressurePa(duct);
    }

    /// <summary>
    /// True when the air reaching a sink is delivered by an air terminal or exit fitting whose own pressure already
    /// includes the discharge into the space (see the class remarks), so no separate exit loss applies.
    /// </summary>
    public static bool DeliveredThroughOutlet(DuctSystemDocument document, SavedLayoutNode upstream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(upstream);
        if (upstream.Type == LayoutNodeType.Grd)
            return true;
        if (LayoutTopology.LinkedFitting(document, upstream) is not { } fitting)
            return false;
        var definition = FittingCatalog.All.FirstOrDefault(x =>
            string.Equals(x.Code, fitting.FittingCode, StringComparison.OrdinalIgnoreCase));
        return definition is not null
               && (string.Equals(FittingCatalog.SelectionGroup(definition), "GRDs", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(definition.Category, "Exits", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when <paramref name="node"/> is a sink space fed straight from a sized duct (an exit loss applies).</summary>
    public static bool ExitLossApplies(DuctSystemDocument document, SavedLayoutNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (node.Type != LayoutNodeType.Reservoir || !SpaceRoles.IsSink(node.SpaceRole))
            return false;
        var upstream = document.LayoutNodes.FirstOrDefault(x =>
            string.Equals(x.Id, node.ParentNodeId, StringComparison.OrdinalIgnoreCase));
        if (upstream is null || DeliveredThroughOutlet(document, upstream))
            return false;
        // An unsized connection is reported where it is drawn; there is no duct velocity to discharge.
        return LayoutTopology.DuctForConnection(document, node, false) is not null;
    }

    /// <summary>
    /// Exit loss (Pa) charged where air leaves a bare duct into a sink space: one velocity pressure of that duct
    /// (ASHRAE 2-1, C = 1.0). 0 for any other component, and when an air terminal or exit fitting delivers the air.
    /// Throws when the duct into the sink cannot be calculated (the caller reports it).
    /// </summary>
    public static double ExitLossPa(DuctSystemDocument document, SavedLayoutNode node)
    {
        if (!ExitLossApplies(document, node))
            return 0.0;
        var duct = LayoutTopology.DuctForConnection(document, node, false)!;
        var result = duct.Calculate();
        return 0.5 * result.Air.DensityKgPerM3 * result.ActualVelocityMPerS * result.ActualVelocityMPerS;
    }
}

/// <summary>One station along a path: after a step, its total pressure, velocity pressure and static pressure (Pa, gauge).</summary>
/// <param name="Index">0 for the start of the path (in the first component), then 1, 2 … for each step.</param>
public sealed record PathPressureStation(int Index, string ItemKey, string ItemId, string Kind, double TotalPa,
    double VelocityPa, double StaticPa, bool Known);

/// <summary>Total, velocity and static pressure along one path (Project tab "Net path static pressure" graph).</summary>
public sealed record PathPressureProfile(SystemPathProfile Path, IReadOnlyList<PathPressureStation> Stations)
{
    public bool IsComplete => Path.IsComplete && Stations.All(station => station.Known);
}

public static class PathPressureProfiles
{
    /// <summary>
    /// Pressures along every path: start at 0 in the first component (a space's still air, or the fan's inlet when the
    /// path starts at the fan), subtract each step's total-pressure loss, add the fan's pressure at the fan, and take
    /// static = total − velocity pressure of the section at each station (see <see cref="SystemPressures"/>).
    /// </summary>
    public static IReadOnlyList<PathPressureProfile> Build(DuctSystemDocument document, SystemPathProfileResult? paths = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        paths ??= SystemPathProfiles.Build(document);
        var nodesBySequence = document.LayoutNodes.ToDictionary(x => x.Sequence);
        var fan = document.LayoutNodes.FirstOrDefault(x => x.Type == LayoutNodeType.Fan);
        var result = new List<PathPressureProfile>();
        foreach (var path in paths.Paths)
        {
            var stations = new List<PathPressureStation>();
            var total = 0.0;
            var known = true;
            stations.Add(new PathPressureStation(0, string.Empty, path.StartId, "Start", 0.0, 0.0, 0.0, true));
            for (var index = 0; index < path.Steps.Count; index++)
            {
                var step = path.Steps[index];
                if (SystemPathProfile.IsFanKind(step.Kind))
                    total += fan is null ? 0.0 : Math.Max(0.0, fan.FanStaticPressurePa);
                else
                    total -= step.LossPa;
                known &= step.Calculated;
                var velocity = VelocityAt(path, index);
                known &= double.IsFinite(velocity);
                stations.Add(new PathPressureStation(index + 1, step.ItemKey, step.ItemId, step.Kind, total,
                    double.IsFinite(velocity) ? velocity : 0.0, total - (double.IsFinite(velocity) ? velocity : 0.0), known));
            }
            result.Add(new PathPressureProfile(path, stations));
        }
        return result;

        double VelocityAt(SystemPathProfile path, int index)
        {
            var step = path.Steps[index];
            if (step.Kind == "Duct")
                return SystemPressures.DuctVelocityPressurePa(DuctFor(step.ItemKey));
            // A fitting or component: the section leaving it is the next duct on this path; a space has still air.
            if (step.ItemKey.StartsWith("L:", StringComparison.Ordinal)
                && int.TryParse(step.ItemKey.AsSpan(2), out var sequence)
                && nodesBySequence.TryGetValue(sequence, out var node) && node.Type == LayoutNodeType.Reservoir)
                return 0.0;
            for (var next = index + 1; next < path.Steps.Count; next++)
                if (path.Steps[next].Kind == "Duct")
                    return SystemPressures.DuctVelocityPressurePa(DuctFor(path.Steps[next].ItemKey));
            return 0.0;
        }

        SavedDuct? DuctFor(string key) =>
            key.StartsWith("D:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(2), out var sequence)
                ? document.Ducts.FirstOrDefault(x => x.Sequence == sequence)
                : null;
    }
}
