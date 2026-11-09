namespace DuctSizer.Core;

/// <summary>
/// One drawn connection (duct run) between two layout nodes. Connections are stored on the
/// downstream node: <see cref="SavedLayoutNode.ParentNodeId"/> for the primary inlet and
/// <see cref="SavedLayoutNode.SecondaryParentNodeId"/> for a converging fitting's second inlet.
/// </summary>
public readonly record struct LayoutConnection(SavedLayoutNode Upstream, SavedLayoutNode Downstream, bool Secondary);

/// <summary>
/// A calculation that could not be completed for a system item. A system result that carries
/// any issue is incomplete and must not be presented as a valid total.
/// </summary>
public sealed record SystemCalculationIssue(string ItemId, string Message);

/// <summary>Read-only graph helpers over the saved Layout Designer model.</summary>
public static class LayoutTopology
{
    /// <summary>True for a fitting node with two outlets (a diverging tee or wye).</summary>
    public static bool IsDiverging(SavedLayoutNode node) =>
        node.Type == LayoutNodeType.Fitting && FittingCatalog.OutletPortCount(node.FittingGroup) > 1;

    /// <summary>True for a fitting node with two inlets (a converging tee or wye).</summary>
    public static bool IsConverging(SavedLayoutNode node) =>
        node.Type == LayoutNodeType.Fitting && FittingCatalog.InletPortCount(node.FittingGroup) > 1;

    /// <summary>Every connection leaving <paramref name="upstream"/>, primary and secondary (calculation points excluded).</summary>
    public static IEnumerable<LayoutConnection> ConnectionsFrom(DuctSystemDocument document, SavedLayoutNode upstream) =>
        ConnectionsFrom(document, upstream, includeCalculationPoints: false);

    /// <summary>Every connection leaving <paramref name="upstream"/>, primary and secondary.</summary>
    public static IEnumerable<LayoutConnection> ConnectionsFrom(DuctSystemDocument document, SavedLayoutNode upstream,
        bool includeCalculationPoints)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(upstream);
        foreach (var node in document.LayoutNodes)
        {
            if (!includeCalculationPoints && node.Type == LayoutNodeType.CalculationPoint)
                continue;
            if (string.Equals(node.ParentNodeId, upstream.Id, StringComparison.OrdinalIgnoreCase))
                yield return new LayoutConnection(upstream, node, false);
            if (string.Equals(node.SecondaryParentNodeId, upstream.Id, StringComparison.OrdinalIgnoreCase))
                yield return new LayoutConnection(upstream, node, true);
        }
    }

    /// <summary>The duct record drawn on the connection into <paramref name="downstream"/>, if any.</summary>
    public static SavedDuct? DuctForConnection(DuctSystemDocument document, SavedLayoutNode downstream, bool secondary)
    {
        var sequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        return sequence is int value ? document.Ducts.FirstOrDefault(x => x.Sequence == value) : null;
    }

    /// <summary>The fitting calculation record linked to a layout node, if any.</summary>
    public static SavedFitting? LinkedFitting(DuctSystemDocument document, SavedLayoutNode node) =>
        node.LinkedFittingSequence is int sequence ? document.Fittings.FirstOrDefault(x => x.Sequence == sequence) : null;

    /// <summary>
    /// The straight-through (main) outlet of a diverging fitting is the downstream connection that keeps
    /// the splitter's branch ID; the other outlet starts a new branch. This is the same rule the
    /// Designer uses when it numbers branches.
    /// </summary>
    public static bool IsMainOutlet(SavedLayoutNode splitter, SavedLayoutNode downstream) =>
        string.Equals(downstream.BranchId, splitter.BranchId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sequences of every node reachable from <paramref name="start"/> through drawn connections in either direction.</summary>
    public static HashSet<int> ConnectedNodeSequences(DuctSystemDocument document, SavedLayoutNode start)
    {
        var nodesById = document.LayoutNodes
            .Where(candidate => candidate.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase);
        var connected = new HashSet<int>();
        var pending = new Queue<SavedLayoutNode>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!connected.Add(current.Sequence))
                continue;
            foreach (var parentId in new[] { current.ParentNodeId, current.SecondaryParentNodeId })
                if (!string.IsNullOrWhiteSpace(parentId) && nodesById.TryGetValue(parentId, out var parent))
                    pending.Enqueue(parent);
            foreach (var child in nodesById.Values.Where(candidate =>
                         string.Equals(candidate.ParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(candidate.SecondaryParentNodeId, current.Id, StringComparison.OrdinalIgnoreCase)))
                pending.Enqueue(child);
        }
        return connected;
    }

    /// <summary>Calculation points (P/T/V/Q sensors) drawn on the connection into <paramref name="downstream"/>.</summary>
    public static IEnumerable<SavedLayoutNode> CalculationPointsForConnection(
        DuctSystemDocument document, SavedLayoutNode downstream, bool secondary)
    {
        var ductSequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
        return document.LayoutNodes
            .Where(x => x.Type == LayoutNodeType.CalculationPoint
                        && ((string.Equals(x.CalculationConnectionNodeId, downstream.Id, StringComparison.OrdinalIgnoreCase)
                             && x.CalculationConnectionSecondary == secondary)
                            || ductSequence is int sequence && x.ObservedDuctSequence == sequence))
            .OrderBy(x => x.CalculationConnectionFraction)
            .ThenBy(x => x.Sequence);
    }
}

/// <summary>Junction flow data built from the actual drawn duct sizes, plus the legs whose size had to be assumed.</summary>
/// <param name="Junction">Airflow and geometry for the common, straight and branch legs.</param>
/// <param name="AssumedLegs">
/// Legs ("common", "straight", "branch") with no sized duct drawn. Those legs use the fitting's own reference
/// size, so their area ratios are provisional until the duct is sized.
/// </param>
public sealed record JunctionLegResult(JunctionFlowData Junction, IReadOnlyList<string> AssumedLegs);

/// <summary>
/// Builds ASHRAE junction inputs (ASHRAE fittings 5-x) from the ducts actually connected to each leg.
/// The junction coefficient tables are functions of leg area and velocity ratios (Ab/Ac, As/Ac, Vb/Vc, Vs/Vc),
/// so every leg must carry its own duct size; using the fitting's single reference size for all three legs
/// would force every area ratio to 1.0.
/// </summary>
public static class JunctionLegBuilder
{
    /// <summary>
    /// Diverging junction: common = duct entering the splitter, straight = main outlet duct,
    /// branch = branch outlet duct (see <see cref="LayoutTopology.IsMainOutlet"/>).
    /// </summary>
    public static JunctionLegResult ForDiverging(DuctSystemDocument document, SavedLayoutNode splitter,
        DuctGeometry referenceGeometry, JunctionPath storedPath,
        double commonAirflowM3PerS, double straightAirflowM3PerS, double branchAirflowM3PerS)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(splitter);
        ArgumentNullException.ThrowIfNull(referenceGeometry);
        var outlets = LayoutTopology.ConnectionsFrom(document, splitter).ToArray();
        var main = outlets.FirstOrDefault(x => LayoutTopology.IsMainOutlet(splitter, x.Downstream));
        var branch = outlets.FirstOrDefault(x => !LayoutTopology.IsMainOutlet(splitter, x.Downstream));
        var assumed = new List<string>();
        var common = LegGeometry(LayoutTopology.DuctForConnection(document, splitter, false), "common");
        var straight = LegGeometry(main.Downstream is null ? null
            : LayoutTopology.DuctForConnection(document, main.Downstream, main.Secondary), "straight");
        var branchGeometry = LegGeometry(branch.Downstream is null ? null
            : LayoutTopology.DuctForConnection(document, branch.Downstream, branch.Secondary), "branch");
        return new JunctionLegResult(new JunctionFlowData(storedPath,
            commonAirflowM3PerS, common, straightAirflowM3PerS, straight, branchAirflowM3PerS, branchGeometry), assumed);

        DuctGeometry LegGeometry(SavedDuct? duct, string leg)
        {
            var geometry = TryGeometry(duct);
            if (geometry is not null)
                return geometry;
            assumed.Add(leg);
            return referenceGeometry;
        }
    }

    /// <summary>
    /// Converging junction: straight = primary inlet duct, branch = secondary inlet duct,
    /// common = the duct leaving the fitting.
    /// </summary>
    public static JunctionLegResult ForConverging(DuctSystemDocument document, SavedLayoutNode merger,
        DuctGeometry referenceGeometry, JunctionPath storedPath,
        double straightAirflowM3PerS, double branchAirflowM3PerS)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentNullException.ThrowIfNull(referenceGeometry);
        var assumed = new List<string>();
        var outlet = LayoutTopology.ConnectionsFrom(document, merger).FirstOrDefault();
        var common = LegGeometry(outlet.Downstream is null ? null
            : LayoutTopology.DuctForConnection(document, outlet.Downstream, outlet.Secondary), "common");
        var straight = LegGeometry(LayoutTopology.DuctForConnection(document, merger, false), "straight");
        var branch = LegGeometry(LayoutTopology.DuctForConnection(document, merger, true), "branch");
        return new JunctionLegResult(new JunctionFlowData(storedPath,
            straightAirflowM3PerS + branchAirflowM3PerS, common,
            straightAirflowM3PerS, straight, branchAirflowM3PerS, branch), assumed);

        DuctGeometry LegGeometry(SavedDuct? duct, string leg)
        {
            var geometry = TryGeometry(duct);
            if (geometry is not null)
                return geometry;
            assumed.Add(leg);
            return referenceGeometry;
        }
    }

    private static DuctGeometry? TryGeometry(SavedDuct? duct)
    {
        if (duct is null)
            return null;
        try
        {
            return duct.CreateGeometry();
        }
        catch (ArgumentOutOfRangeException)
        {
            // An unsized duct (zero or missing dimension) has no geometry yet.
            return null;
        }
    }
}

/// <summary>Result of the critical-path (longest root-to-terminal route) pressure-loss analysis.</summary>
/// <param name="PressureLossPa">Net loss along the critical route in Pa. Positive fan static is not counted as a loss.</param>
/// <param name="ItemKeys">Keys (<see cref="CriticalPathAnalyzer.DuctKey"/> etc.) of every item on the critical route.</param>
/// <param name="StartId">ID of the route's first component.</param>
/// <param name="EndId">ID of the route's last component.</param>
/// <param name="Issues">
/// Items on any route whose loss could not be calculated. When this is not empty the true critical route is
/// unknown, so the total must not be used for fan sizing.
/// </param>
/// <param name="IsSeriesTotal">
/// True when no Layout Designer system exists and the total is the series sum of every scheduled duct and fitting
/// (the schedule-only workflow).
/// </param>
public sealed record CriticalPathResult(
    double PressureLossPa,
    IReadOnlySet<string> ItemKeys,
    string StartId,
    string EndId,
    IReadOnlyList<SystemCalculationIssue> Issues,
    bool IsSeriesTotal)
{
    /// <summary>True when every item that could lie on the critical route was calculated.</summary>
    public bool IsComplete => Issues.Count == 0 && ItemKeys.Count > 0;
}

/// <summary>
/// Finds the critical (highest net loss) route through the drawn system, summing duct friction, fitting,
/// coil, filter, GRD and custom-component losses from each root to each terminal.
/// </summary>
/// <remarks>
/// Junction fittings are charged per route: a route leaving a diverging fitting through its main outlet is charged
/// the ASHRAE straight-path coefficient, and a route through the branch outlet the branch-path coefficient.
/// A route entering a converging fitting through its primary inlet is charged the straight path, and through its
/// secondary inlet the branch path. Duct friction uses the conservative upper bound in transitional flow.
/// Any item that fails to calculate is reported in <see cref="CriticalPathResult.Issues"/> and is never counted
/// as a zero loss. A drawn connection with no duct assigned (unsized, shown red in the Designer) has an unknown loss:
/// it is reported as an issue and blocks the total until it is sized.
/// </remarks>
public static class CriticalPathAnalyzer
{
    /// <summary>Key for a scheduled duct.</summary>
    public static string DuctKey(int sequence) => $"D:{sequence}";

    /// <summary>Key for a scheduled fitting.</summary>
    public static string FittingKey(int sequence) => $"F:{sequence}";

    /// <summary>Key for a layout node (equipment, space, sensor).</summary>
    public static string LayoutKey(int sequence) => $"L:{sequence}";

    /// <summary>Analyzes the document's drawn system.</summary>
    public static CriticalPathResult Analyze(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.LayoutNodes
            .Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (nodes.Count == 0)
            return SeriesTotal(document);

        var issues = new Dictionary<string, SystemCalculationIssue>(StringComparer.OrdinalIgnoreCase);
        var roots = nodes.Values.Where(node =>
                (string.IsNullOrWhiteSpace(node.ParentNodeId) || !nodes.ContainsKey(node.ParentNodeId))
                && (string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) || !nodes.ContainsKey(node.SecondaryParentNodeId)))
            .OrderBy(x => x.Sequence).ToArray();
        (double Loss, HashSet<string> Keys, string Start, string End)? best = null;

        foreach (var root in roots)
            Walk(root, arrivedSecondary: false, 0.0, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), root.Id);

        if (best is null || best.Value.Keys.Count == 0)
        {
            issues.TryAdd("System", new SystemCalculationIssue("System",
                "No connected route was found. Connect the system components with ducts."));
            return new CriticalPathResult(0.0, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                string.Empty, string.Empty, issues.Values.ToArray(), false);
        }

        return new CriticalPathResult(best.Value.Loss, best.Value.Keys, best.Value.Start, best.Value.End,
            issues.Values.ToArray(), false);

        void Walk(SavedLayoutNode node, bool arrivedSecondary, double lossBefore, HashSet<string> keysBefore,
            HashSet<string> visited, string startId)
        {
            if (!visited.Add(node.Id))
                return;
            var keys = new HashSet<string>(keysBefore, StringComparer.OrdinalIgnoreCase);
            var children = LayoutTopology.ConnectionsFrom(document, node).OrderBy(x => x.Downstream.Sequence).ToArray();
            var diverging = LayoutTopology.IsDiverging(node);

            // A diverging fitting's loss depends on the outlet taken, so it is charged per child below.
            var loss = lossBefore;
            if (!diverging || children.Length == 0)
                loss += NodeLoss(node, arrivedSecondary, nextChild: null, keys);

            if (children.Length == 0)
            {
                if (best is null || loss > best.Value.Loss)
                    best = (loss, keys, startId, node.Id);
                return;
            }

            foreach (var child in children)
            {
                var childKeys = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
                var childLoss = loss;
                if (diverging)
                    childLoss += NodeLoss(node, arrivedSecondary, child.Downstream, childKeys);
                childLoss += ConnectionLoss(child.Downstream, child.Secondary, childKeys);
                foreach (var point in LayoutTopology.CalculationPointsForConnection(document, child.Downstream, child.Secondary))
                    childKeys.Add(LayoutKey(point.Sequence));
                Walk(child.Downstream, child.Secondary, childLoss, childKeys,
                    new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase), startId);
            }
        }

        double NodeLoss(SavedLayoutNode node, bool arrivedSecondary, SavedLayoutNode? nextChild, HashSet<string> keys)
        {
            if (node.LinkedFittingSequence is int fittingSequence)
            {
                var fitting = document.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence);
                if (fitting is not null)
                {
                    keys.Add(FittingKey(fittingSequence));
                    var path = JunctionPathForRoute(node, arrivedSecondary, nextChild);
                    return Safe(fitting.Id, () => path is JunctionPath value
                        ? fitting.CalculateForPath(value).PressureLossPa
                        : fitting.Calculate().PressureLossPa);
                }
            }
            if (node.LinkedDuctSequence is int ductSequence)
            {
                var duct = document.Ducts.FirstOrDefault(x => x.Sequence == ductSequence);
                if (duct is not null)
                {
                    keys.Add(DuctKey(ductSequence));
                    return Safe(duct.Id, () => duct.Calculate().TotalPressureLossUpperPa);
                }
            }
            keys.Add(LayoutKey(node.Sequence));
            return node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                or LayoutNodeType.CustomComponent
                ? node.PressureDropPa
                : 0.0;
        }

        double ConnectionLoss(SavedLayoutNode downstream, bool secondary, HashSet<string> keys)
        {
            var sequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
            if (sequence is null)
            {
                // A drawn but unsized connection has an unknown loss; it blocks the total until a duct is sized.
                Report(downstream.Id, "The connection into this component has no sized duct; size it to complete the total.");
                return 0.0;
            }
            var duct = LayoutTopology.DuctForConnection(document, downstream, secondary);
            if (duct is null)
            {
                Report(downstream.Id, $"The connection into this component refers to duct D-{sequence:000}, which does not exist.");
                return 0.0;
            }
            keys.Add(DuctKey(duct.Sequence));
            return Safe(duct.Id, () => duct.Calculate().TotalPressureLossUpperPa);
        }

        double Safe(string itemId, Func<double> calculate)
        {
            try
            {
                var value = calculate();
                if (double.IsFinite(value))
                    return value;
                Report(itemId, "The calculated pressure loss is not a finite number.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or KeyNotFoundException or OverflowException)
            {
                Report(itemId, ex.Message);
            }
            return 0.0;
        }

        void Report(string itemId, string message) =>
            issues.TryAdd(itemId, new SystemCalculationIssue(itemId, message));
    }

    /// <summary>
    /// The ASHRAE junction path a route takes through <paramref name="node"/>, or null when the node is not a
    /// two-path junction (the stored calculation then applies).
    /// </summary>
    public static JunctionPath? JunctionPathForRoute(SavedLayoutNode node, bool arrivedSecondary, SavedLayoutNode? nextChild)
    {
        if (LayoutTopology.IsDiverging(node) && nextChild is not null)
            return LayoutTopology.IsMainOutlet(node, nextChild) ? JunctionPath.Straight : JunctionPath.Branch;
        if (LayoutTopology.IsConverging(node))
            return arrivedSecondary ? JunctionPath.Branch : JunctionPath.Straight;
        return null;
    }

    private static CriticalPathResult SeriesTotal(DuctSystemDocument document)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<SystemCalculationIssue>();
        var total = 0.0;
        foreach (var duct in document.Ducts)
        {
            keys.Add(DuctKey(duct.Sequence));
            try { total += duct.Calculate().TotalPressureLossUpperPa; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                issues.Add(new SystemCalculationIssue(duct.Id, ex.Message));
            }
        }
        foreach (var fitting in document.Fittings)
        {
            keys.Add(FittingKey(fitting.Sequence));
            try { total += fitting.Calculate().PressureLossPa; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or KeyNotFoundException or OverflowException)
            {
                issues.Add(new SystemCalculationIssue(fitting.Id, ex.Message));
            }
        }
        return new CriticalPathResult(total, keys, "System start", "System end", issues, true);
    }
}

/// <summary>Fan selection quantities derived from the drawn system.</summary>
public static class FanSizing
{
    /// <summary>
    /// The airflow the fan must deliver: the largest airflow on any component, duct or linked fitting in the
    /// fan's own connected system. Items in other, unconnected systems are ignored.
    /// </summary>
    public static double RequiredAirflowM3PerS(DuctSystemDocument document, SavedLayoutNode fan)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fan);
        var connected = LayoutTopology.ConnectedNodeSequences(document, fan);
        var nodes = document.LayoutNodes.Where(x => connected.Contains(x.Sequence)).ToArray();
        var flows = new List<double>();
        foreach (var node in nodes)
        {
            if (node.Type != LayoutNodeType.Fan)
                flows.Add(node.AirflowM3PerS);
            if (LayoutTopology.DuctForConnection(document, node, false) is { } primary)
                flows.Add(primary.AirflowM3PerS);
            if (LayoutTopology.DuctForConnection(document, node, true) is { } secondary)
                flows.Add(secondary.AirflowM3PerS);
            if (LayoutTopology.LinkedFitting(document, node) is { } fitting)
                flows.Add(fitting.AirflowM3PerS);
        }
        return flows.Where(double.IsFinite).Select(x => Math.Max(0.0, x)).DefaultIfEmpty(0.0).Max();
    }
}

/// <summary>
/// A complete, portable FanWorks project file (<c>.fanworks</c>): the full saved model, including the Layout Designer
/// components, connections, coordinates, imported PDF background, project settings and sizing defaults.
/// It uses the same versioned JSON format and migration rules as the local working file.
/// </summary>
public static class ProjectFile
{
    /// <summary>File extension for project files.</summary>
    public const string Extension = ".fanworks";

    /// <summary>Writes the project crash-safely (temporary file, previous version kept as <c>.bak</c>).</summary>
    public static void Save(string path, DuctSystemDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        DuctSystemStore.Save(Path.GetFullPath(path), document);
    }

    /// <summary>Reads and migrates a project file. Unlike the local working file, a missing file is an error.</summary>
    public static DuctSystemDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("The project file was not found.", path);
        return DuctSystemStore.Load(path);
    }
}
