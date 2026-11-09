namespace DuctSizer.Core;

/// <summary>Space-boundary roles used by Layout Designer reservoirs (sources and sinks).</summary>
public static class SpaceRoles
{
    /// <summary>True for a boundary that supplies air into the system ("Space Source", legacy "Return Pickup").</summary>
    public static bool IsSource(string? role) =>
        string.Equals(role, "Space Source", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Return Pickup", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a boundary that receives air from the system ("Space Sink", legacy "Supply Discharge").</summary>
    public static bool IsSink(string? role) =>
        string.Equals(role, "Space Sink", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Supply Discharge", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Result of <see cref="SystemSolver.Solve"/>: the inlet static pressure of each node, by node ID.</summary>
public sealed class SystemSolution
{
    private readonly Dictionary<string, double> _inletPressures;

    internal SystemSolution(Dictionary<string, double> inletPressures) => _inletPressures = inletPressures;

    /// <summary>An empty solution (no system calculated yet).</summary>
    public static SystemSolution Empty { get; } = new(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Inlet static pressure (Pa) of a node that has at least one upstream connection.</summary>
    public bool TryGetInletPressure(string nodeId, out double pressurePa) =>
        _inletPressures.TryGetValue(nodeId, out pressurePa);

    /// <summary>Inlet static pressures (Pa) by node ID.</summary>
    public IReadOnlyDictionary<string, double> InletPressures => _inletPressures;
}

/// <summary>
/// The Layout Designer system solver: airflow balancing and static-pressure propagation through the drawn system.
/// </summary>
/// <remarks>
/// <para>
/// Moved unchanged in behavior from the main window (v0.93) so it can be tested without the UI.
/// <see cref="Solve"/> updates the document in place:
/// </para>
/// <list type="bullet">
/// <item>Airflow: a node with no user-specified airflow takes the sum of its incoming connection flows.
/// A connection's duct carries its upstream node's flow unless the connection airflow is user-specified.
/// A diverging fitting splits its inlet flow among unlocked outlets in proportion to the fitting's stored straight
/// and branch flows (equal shares when none are stored); locked (user-specified) outlets keep their flow and are
/// marked as overridden when the outlets do not balance with the inlet (tolerance: the larger of 1 cfm and 1%).
/// A converging fitting's outlet flow is the sum of its two inlets.</item>
/// <item>Temperature: a duct takes its upstream node's temperature; a node (except a coil, which sets its own
/// leaving temperature) takes the temperature of its first incoming connection.</item>
/// <item>Junctions: each junction fitting's legs are sized from the connected ducts
/// (<see cref="JunctionLegBuilder"/>).</item>
/// <item>Static pressure (gauge, Pa): a root fan starts at its fan static pressure, a root Space Source keeps its
/// entered pressure, any other root starts at 0. Each connection subtracts its duct's total pressure loss (upper
/// bound in transitional flow). An unsized connection (no duct assigned) or a duct that cannot be calculated makes
/// downstream pressures NaN ("not available"), never a silent zero.
/// A fan adds its static pressure at the outlet; other components subtract their own loss. A diverging fitting
/// subtracts the ASHRAE path loss of the outlet each child is on (<see cref="OutletPressureToward"/>); its own
/// displayed pressure is the main-outlet value. A converging fitting charges each inlet its own path loss and its
/// outlet takes the lowest result.</item>
/// <item>Calculation points report the pressure at their fractional position along the observed duct.</item>
/// </list>
/// <para>The state walk runs twice: outlet balancing can change connection flows after the first pass, and the
/// second pass propagates losses from those final, conserved flows.</para>
/// </remarks>
public static class SystemSolver
{
    /// <summary>Solves airflow, temperature and static pressure for the whole drawn system, in place.</summary>
    public static SystemSolution Solve(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var inletPressures = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var solution = new SystemSolution(inletPressures);
        var nodes = document.LayoutNodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in document.LayoutNodes
                     .Where(x => x.Type != LayoutNodeType.CalculationPoint)
                     .OrderBy(x => x.Sequence))
            CalculateNodeState(node);

        foreach (var splitter in document.LayoutNodes.Where(LayoutTopology.IsDiverging))
            BalanceDivergingOutletFlows(document, splitter);

        // Outlet balancing can change connection airflow after the first topology pass.
        // Re-run the state walk so duct and linked-fitting losses are propagated from
        // those final conserved flows, not from the provisional values.
        completed.Clear();
        active.Clear();
        foreach (var node in document.LayoutNodes
                     .Where(x => x.Type != LayoutNodeType.CalculationPoint)
                     .OrderBy(x => x.Sequence))
            CalculateNodeState(node);

        foreach (var point in document.LayoutNodes
                     .Where(x => x.Type == LayoutNodeType.CalculationPoint)
                     .OrderBy(x => x.Sequence))
        {
            if (!nodes.TryGetValue(point.CalculationConnectionNodeId, out var downstream))
                continue;
            var secondary = point.CalculationConnectionSecondary;
            var parentId = secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
            if (!nodes.TryGetValue(parentId, out var upstream))
                continue;
            var incomingSequence = secondary
                ? downstream.SecondaryConnectionDuctSequence
                : downstream.ConnectionDuctSequence;
            var incomingDuct = incomingSequence is int sequence
                ? document.Ducts.FirstOrDefault(x => x.Sequence == sequence)
                : null;
            point.ObservedDuctSequence = incomingSequence;
            point.AirSystemType = downstream.AirSystemType;
            point.BranchId = downstream.BranchId;
            point.AirflowM3PerS = incomingDuct?.AirflowM3PerS > 0
                ? incomingDuct.AirflowM3PerS
                : upstream.AirflowM3PerS;
            point.TemperatureC = incomingDuct?.TemperatureC ?? upstream.TemperatureC;
            if (incomingDuct is not null)
            {
                try { point.VelocityMPerS = incomingDuct.Calculate().ActualVelocityMPerS; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
                {
                    point.VelocityMPerS = 0;
                }
            }
            // NaN (shown as "—") for an unsized connection or a duct that cannot be calculated; never a silent zero loss.
            var ductLoss = incomingSequence is int ductSequence ? DuctLoss(document, ductSequence) : double.NaN;
            point.PressurePa = OutletPressureToward(document, solution, upstream, downstream)
                               - ductLoss * Math.Clamp(point.CalculationConnectionFraction, 0.0, 1.0);
        }

        return solution;

        void CalculateNodeState(SavedLayoutNode node)
        {
            if (completed.Contains(node.Id) || !active.Add(node.Id))
                return;

            var parents = new List<(SavedLayoutNode Node, bool Secondary)>();
            if (nodes.TryGetValue(node.ParentNodeId, out var primaryParent))
                parents.Add((primaryParent, false));
            if (nodes.TryGetValue(node.SecondaryParentNodeId, out var secondaryParent))
                parents.Add((secondaryParent, true));
            foreach (var parent in parents)
                CalculateNodeState(parent.Node);

            if (parents.Count == 0)
            {
                SyncLinkedFittingInput(document, node);
                if (node.Type == LayoutNodeType.Fan)
                    node.PressurePa = Math.Max(0.0, node.FanStaticPressurePa);
                else if (!(node.Type == LayoutNodeType.Reservoir && SpaceRoles.IsSource(node.SpaceRole)))
                    node.PressurePa = 0.0;
            }
            else
            {
                var incoming = parents.Select(parent =>
                {
                    var sequence = parent.Secondary ? node.SecondaryConnectionDuctSequence : node.ConnectionDuctSequence;
                    var duct = sequence is int ductSequence
                        ? document.Ducts.FirstOrDefault(x => x.Sequence == ductSequence)
                        : null;
                    var airflow = ResolveConnectionAirflow(document, parent.Node, node, parent.Secondary, duct);
                    if (duct is not null)
                        duct.TemperatureC = parent.Node.TemperatureC;
                    // An unsized connection (no duct assigned) or a duct that cannot be calculated makes downstream
                    // pressures "not available" (NaN) instead of silently treating it as having no loss.
                    var loss = sequence is int lossSequence ? DuctLoss(document, lossSequence) : double.NaN;
                    return new
                    {
                        Parent = parent.Node,
                        Secondary = parent.Secondary,
                        Duct = duct,
                        Pressure = OutletPressureToward(document, solution, parent.Node, node) - loss,
                        Airflow = airflow,
                        Temperature = duct?.TemperatureC ?? parent.Node.TemperatureC
                    };
                }).ToArray();

                var calculatedAirflow = incoming.Sum(x => x.Airflow);
                if (!node.AirflowUserSpecified || LayoutTopology.IsConverging(node))
                    node.AirflowM3PerS = calculatedAirflow;
                if (node.Type != LayoutNodeType.Coil)
                    node.TemperatureC = incoming[0].Temperature;
                if (LayoutTopology.IsConverging(node))
                    UpdateConvergingJunction(document, node, incoming.Select(x => x.Airflow).ToArray());
                // Fitting loss depends on airflow and temperature. Synchronize the linked
                // fitting before pressure propagation so the route total and the displayed
                // inlet/outlet gauge pressures always use the same current calculation.
                SyncLinkedFittingInput(document, node);
                var inletPressure = incoming.Min(x => x.Pressure);
                inletPressures[node.Id] = inletPressure;
                if (node.Type == LayoutNodeType.Fan)
                    node.PressurePa = inletPressure + Math.Max(0.0, node.FanStaticPressurePa);
                else if (LayoutTopology.IsDiverging(node))
                {
                    // Each outlet has its own ASHRAE path loss (see OutletPressureToward). The node shows the
                    // main (straight-through) outlet pressure.
                    var mainOutlet = LayoutTopology.ConnectionsFrom(document, node)
                        .FirstOrDefault(x => LayoutTopology.IsMainOutlet(node, x.Downstream));
                    node.PressurePa = inletPressure - NodeLoss(document, node, false, mainOutlet.Downstream);
                }
                else
                {
                    // A converging fitting charges each inlet its own path; the outlet takes the lowest result.
                    node.PressurePa = incoming.Min(x => x.Pressure - NodeLoss(document, node, x.Secondary));
                }
            }

            active.Remove(node.Id);
            completed.Add(node.Id);
        }
    }

    /// <summary>
    /// Static pressure leaving <paramref name="parent"/> toward <paramref name="child"/>. A diverging fitting has a
    /// different ASHRAE loss on each outlet, so the pressure feeding the branch differs from the main outlet.
    /// </summary>
    public static double OutletPressureToward(DuctSystemDocument document, SystemSolution solution,
        SavedLayoutNode parent, SavedLayoutNode child)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(solution);
        if (LayoutTopology.IsDiverging(parent) && solution.TryGetInletPressure(parent.Id, out var inlet))
            return inlet - NodeLoss(document, parent, false, child);
        return parent.PressurePa;
    }

    /// <summary>
    /// Pressure loss (Pa) charged at <paramref name="node"/> for a route that arrives through
    /// <paramref name="arrivedSecondary"/> and leaves toward <paramref name="nextChild"/>. Junction fittings use the
    /// ASHRAE path for that route. Coils, filters, GRDs and custom components use their entered pressure drop.
    /// Returns NaN when the item cannot be calculated.
    /// </summary>
    public static double NodeLoss(DuctSystemDocument document, SavedLayoutNode node, bool arrivedSecondary = false,
        SavedLayoutNode? nextChild = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (LayoutTopology.LinkedFitting(document, node) is { } fitting)
        {
            var path = CriticalPathAnalyzer.JunctionPathForRoute(node, arrivedSecondary, nextChild);
            try
            {
                return path is JunctionPath value
                    ? fitting.CalculateForPath(value).PressureLossPa
                    : fitting.Calculate().PressureLossPa;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or KeyNotFoundException or OverflowException)
            {
                return double.NaN;
            }
        }
        if (node.LinkedDuctSequence is int ductSequence)
            return DuctLoss(document, ductSequence);
        return node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
            or LayoutNodeType.CustomComponent
            ? node.PressureDropPa
            : 0.0;
    }

    /// <summary>Total pressure loss (Pa, transitional upper bound) of a scheduled duct, or NaN if it is missing or fails.</summary>
    public static double DuctLoss(DuctSystemDocument document, int sequence)
    {
        ArgumentNullException.ThrowIfNull(document);
        try { return document.Ducts.FirstOrDefault(x => x.Sequence == sequence)?.Calculate().TotalPressureLossUpperPa ?? double.NaN; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException) { return double.NaN; }
    }

    /// <summary>Pressure loss (Pa) of a scheduled fitting at its stored path, or NaN if it is missing or fails.</summary>
    public static double FittingLoss(DuctSystemDocument document, int sequence)
    {
        ArgumentNullException.ThrowIfNull(document);
        try { return document.Fittings.FirstOrDefault(x => x.Sequence == sequence)?.Calculate().PressureLossPa ?? double.NaN; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or KeyNotFoundException or OverflowException) { return double.NaN; }
    }

    /// <summary>True when the connection into <paramref name="downstream"/> has a user-specified airflow.</summary>
    public static bool ConnectionAirflowUserSpecified(SavedLayoutNode downstream, bool secondary) => secondary
        ? downstream.SecondaryConnectionAirflowUserSpecified
        : downstream.ConnectionAirflowUserSpecified;

    /// <summary>Marks the connection into <paramref name="downstream"/> as (not) user-specified.</summary>
    public static void SetConnectionAirflowOverride(SavedLayoutNode downstream, bool secondary, bool value)
    {
        if (secondary)
            downstream.SecondaryConnectionAirflowOverride = value;
        else
            downstream.ConnectionAirflowOverride = value;
    }

    private static void SyncLinkedFittingInput(DuctSystemDocument document, SavedLayoutNode node)
    {
        if (node.LinkedFittingSequence is not int fittingSequence)
            return;
        var fitting = document.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence);
        if (fitting is null || node.AirflowOverride)
            return;
        fitting.AirflowM3PerS = node.AirflowM3PerS;
        fitting.TemperatureC = node.TemperatureC;
    }

    private static double ResolveConnectionAirflow(DuctSystemDocument document, SavedLayoutNode upstream,
        SavedLayoutNode downstream, bool secondary, SavedDuct? duct)
    {
        if (LayoutTopology.IsDiverging(upstream))
        {
            BalanceDivergingOutletFlows(document, upstream);
            return duct is not null
                ? Math.Max(0.0, duct.AirflowM3PerS)
                : Math.Max(0.0, downstream.AirflowM3PerS);
        }

        if (ConnectionAirflowUserSpecified(downstream, secondary))
            return duct?.AirflowM3PerS > 0 ? duct.AirflowM3PerS : Math.Max(0.0, downstream.AirflowM3PerS);

        var calculated = Math.Max(0.0, upstream.AirflowM3PerS);
        if (duct is not null)
            duct.AirflowM3PerS = calculated;
        return calculated;
    }

    /// <summary>
    /// Splits a diverging fitting's inlet airflow among its outlets (see class remarks) and updates the linked
    /// junction fitting's legs.
    /// </summary>
    public static void BalanceDivergingOutletFlows(DuctSystemDocument document, SavedLayoutNode splitter)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(splitter);
        var connections = LayoutTopology.ConnectionsFrom(document, splitter, includeCalculationPoints: true)
            .OrderBy(x => x.Downstream.Sequence).ToArray();
        if (connections.Length == 0)
            return;

        var inlet = Math.Max(0.0, splitter.AirflowM3PerS);
        var locked = connections.Where(x => ConnectionAirflowUserSpecified(x.Downstream, x.Secondary)).ToArray();
        var unlocked = connections.Except(locked).ToArray();
        var lockedTotal = locked.Sum(ConnectionFlow);
        var remaining = Math.Max(0.0, inlet - lockedTotal);
        var tolerance = Math.Max(UnitConversions.CfmToM3PerS(1), inlet * 0.01);

        if (unlocked.Length > 0)
        {
            var weights = unlocked.Select(PreferredOutletWeight).ToArray();
            var weightTotal = weights.Sum();
            if (weightTotal <= 0)
            {
                for (var index = 0; index < weights.Length; index++)
                    weights[index] = 1.0;
                weightTotal = weights.Length;
            }
            for (var index = 0; index < unlocked.Length; index++)
                SetConnectionFlow(unlocked[index], remaining * weights[index] / weightTotal);
        }

        var actualTotal = connections.Sum(ConnectionFlow);
        var hasAllOutlets = connections.Length >= FittingCatalog.OutletPortCount(splitter.FittingGroup);
        var unbalanced = actualTotal > inlet + tolerance
                         || hasAllOutlets && Math.Abs(actualTotal - inlet) > tolerance;
        foreach (var connection in connections)
            SetConnectionAirflowOverride(connection.Downstream, connection.Secondary,
                ConnectionAirflowUserSpecified(connection.Downstream, connection.Secondary) && unbalanced);

        UpdateDivergingJunction(document, splitter, connections);
        return;

        double ConnectionFlow(LayoutConnection connection)
        {
            var duct = LayoutTopology.DuctForConnection(document, connection.Downstream, connection.Secondary);
            return duct?.AirflowM3PerS > 0 ? duct.AirflowM3PerS : Math.Max(0.0, connection.Downstream.AirflowM3PerS);
        }

        void SetConnectionFlow(LayoutConnection connection, double flow)
        {
            var duct = LayoutTopology.DuctForConnection(document, connection.Downstream, connection.Secondary);
            if (duct is not null)
                duct.AirflowM3PerS = flow;
            if (!connection.Downstream.AirflowUserSpecified)
                connection.Downstream.AirflowM3PerS = flow;
        }

        double PreferredOutletWeight(LayoutConnection connection)
        {
            var fitting = splitter.LinkedFittingSequence is int sequence
                ? document.Fittings.FirstOrDefault(x => x.Sequence == sequence)
                : null;
            if (fitting?.Junction is not JunctionFlowData junction || junction.CommonAirflowM3PerS <= 0)
                return 1.0;
            var mainPath = string.Equals(connection.Downstream.BranchId, splitter.BranchId,
                StringComparison.OrdinalIgnoreCase);
            return Math.Max(0.0, mainPath ? junction.StraightAirflowM3PerS : junction.BranchAirflowM3PerS);
        }
    }

    private static void UpdateDivergingJunction(DuctSystemDocument document, SavedLayoutNode splitter,
        IReadOnlyList<LayoutConnection> connections)
    {
        if (splitter.LinkedFittingSequence is not int sequence)
            return;
        var fitting = document.Fittings.FirstOrDefault(x => x.Sequence == sequence);
        var geometry = fitting?.CreateGeometry();
        if (fitting is null || geometry is null)
            return;
        var main = connections.FirstOrDefault(x => LayoutTopology.IsMainOutlet(splitter, x.Downstream));
        var branch = connections.FirstOrDefault(x => !LayoutTopology.IsMainOutlet(splitter, x.Downstream));
        var mainFlow = main.Downstream is null ? 0.0
            : LayoutTopology.DuctForConnection(document, main.Downstream, main.Secondary)?.AirflowM3PerS
              ?? main.Downstream.AirflowM3PerS;
        var branchFlow = branch.Downstream is null ? 0.0
            : LayoutTopology.DuctForConnection(document, branch.Downstream, branch.Secondary)?.AirflowM3PerS
              ?? branch.Downstream.AirflowM3PerS;
        fitting.AirflowM3PerS = splitter.AirflowM3PerS;
        fitting.TemperatureC = splitter.TemperatureC;
        // Each leg takes the size of the duct drawn on it, so the ASHRAE area and velocity ratios are real.
        fitting.Junction = JunctionLegBuilder.ForDiverging(document, splitter, geometry,
            fitting.Junction?.Path ?? JunctionPath.Straight,
            splitter.AirflowM3PerS, Math.Max(0.0, mainFlow), Math.Max(0.0, branchFlow)).Junction;
    }

    private static void UpdateConvergingJunction(DuctSystemDocument document, SavedLayoutNode merger,
        IReadOnlyList<double> incomingFlows)
    {
        if (merger.LinkedFittingSequence is not int sequence)
            return;
        var fitting = document.Fittings.FirstOrDefault(x => x.Sequence == sequence);
        var geometry = fitting?.CreateGeometry();
        if (fitting is null || geometry is null)
            return;
        var first = incomingFlows.Count > 0 ? Math.Max(0.0, incomingFlows[0]) : 0.0;
        var second = incomingFlows.Count > 1 ? Math.Max(0.0, incomingFlows[1]) : 0.0;
        var common = first + second;
        merger.AirflowM3PerS = common;
        fitting.AirflowM3PerS = common;
        fitting.TemperatureC = merger.TemperatureC;
        fitting.Junction = JunctionLegBuilder.ForConverging(document, merger, geometry,
            fitting.Junction?.Path ?? JunctionPath.Straight, first, second).Junction;
    }
}
