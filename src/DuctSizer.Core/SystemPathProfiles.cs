namespace DuctSizer.Core;

/// <summary>One item along a route: its own loss and the running total from the start of the route.</summary>
/// <param name="ItemKey">Key of the item (<see cref="CriticalPathAnalyzer.DuctKey"/> and so on).</param>
/// <param name="ItemId">The item's displayed ID (D-001, 3-1, FAN-001 …).</param>
/// <param name="Kind">"Duct", "Fitting", or the component type.</param>
/// <param name="LossPa">The item's pressure loss on this route, Pa (0 when it could not be calculated; see <see cref="Calculated"/>).</param>
/// <param name="CumulativeLossPa">Pressure loss from the start of the route through this item, Pa.</param>
/// <param name="Calculated">False when the item's loss is unknown (failed calculation or unsized connection).</param>
public sealed record PathStep(string ItemKey, string ItemId, string Kind, double LossPa, double CumulativeLossPa, bool Calculated);

/// <summary>A route from a start component (no upstream connection) to an end component (no downstream connection).</summary>
/// <param name="Number">
/// The path's number: 1 is the critical path, then 2, 3 … in order of decreasing total loss (complete paths before
/// incomplete ones). The number is the path's name; the route (<see cref="Route"/>) describes it.
/// </param>
public sealed record SystemPathProfile(string StartId, string EndId, IReadOnlyList<PathStep> Steps, bool IsCritical,
    int Number = 0)
{
    /// <summary>"Path 1", "Path 2" …</summary>
    public string Name => $"Path {Number}";

    /// <summary>The components the path runs between, e.g. "FAN-001 → L-010".</summary>
    public string Route => string.Equals(StartId, EndId, StringComparison.OrdinalIgnoreCase) ? StartId : $"{StartId} → {EndId}";

    /// <summary>Total pressure loss along the route, Pa.</summary>
    public double TotalLossPa => Steps.Count == 0 ? 0.0 : Steps[^1].CumulativeLossPa;

    /// <summary>True when every item on the route was calculated; an incomplete route must not be shown as a result.</summary>
    public bool IsComplete => Steps.All(step => step.Calculated);
}

/// <summary>Every route through the drawn system, in the order the critical-path analysis visits them.</summary>
/// <param name="Truncated">True when the system has more than <see cref="SystemPathProfiles.MaximumPaths"/> routes; only the first are listed.</param>
public sealed record SystemPathProfileResult(IReadOnlyList<SystemPathProfile> Paths, bool Truncated)
{
    public int CompletePathCount => Paths.Count(path => path.IsComplete);
}

/// <summary>
/// Pressure-loss profiles along every route of the drawn system (Analysis tab graph and report title page). Each
/// route runs from a component with no upstream connection to one with no downstream connection, and lists the
/// items in flow order with the running total of pressure loss.
/// </summary>
/// <remarks>
/// The losses charged are exactly those of <see cref="CriticalPathAnalyzer.Analyze"/>: junction fittings per route
/// (main outlet or primary inlet = straight path, otherwise branch path), duct friction at the transitional upper
/// bound, and the entered drop of coils, filters, GRDs and custom components. The route with the greatest total is
/// the critical path (the first such route in visiting order, as in the analyzer); verification checks that the two
/// agree. Items whose loss is unknown are marked, and their route is incomplete.
/// </remarks>
public static class SystemPathProfiles
{
    /// <summary>Upper limit on the routes listed, so a very large system cannot stall the interface.</summary>
    public const int MaximumPaths = 500;

    public static SystemPathProfileResult Build(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.LayoutNodes
            .Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var routes = new List<(string Start, string End, List<PathStep> Steps)>();
        // An item's loss is the same on every route through it (a junction's per flow path), so each is calculated once.
        var calculated = new Dictionary<string, PathStep>(StringComparer.Ordinal);
        var truncated = false;
        if (nodes.Count == 0)
            return new SystemPathProfileResult(Array.Empty<SystemPathProfile>(), false);

        var roots = nodes.Values.Where(node =>
                (string.IsNullOrWhiteSpace(node.ParentNodeId) || !nodes.ContainsKey(node.ParentNodeId))
                && (string.IsNullOrWhiteSpace(node.SecondaryParentNodeId) || !nodes.ContainsKey(node.SecondaryParentNodeId)))
            .OrderBy(x => x.Sequence).ToArray();
        foreach (var root in roots)
            Walk(root, arrivedSecondary: false, new List<PathStep>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                root.Id);

        var criticalIndex = -1;
        var highest = double.NegativeInfinity;
        for (var index = 0; index < routes.Count; index++)
        {
            var total = routes[index].Steps.Count == 0 ? 0.0 : routes[index].Steps[^1].CumulativeLossPa;
            if (criticalIndex < 0 || total > highest)
            {
                criticalIndex = index;
                highest = total;
            }
        }
        var paths = routes.Select((route, index) =>
            new SystemPathProfile(route.Start, route.End, route.Steps, index == criticalIndex)).ToArray();
        // Numbers: the critical path is 1; the rest follow by decreasing total (complete paths first), ties in
        // visiting order. The list itself stays in visiting order.
        var numbered = paths.Select((path, index) => (Path: path, Index: index))
            .OrderByDescending(item => item.Path.IsCritical)
            .ThenByDescending(item => item.Path.IsComplete)
            .ThenByDescending(item => item.Path.TotalLossPa)
            .ThenBy(item => item.Index)
            .Select((item, rank) => (item.Index, Number: rank + 1))
            .ToDictionary(item => item.Index, item => item.Number);
        paths = paths.Select((path, index) => path with { Number = numbered[index] }).ToArray();
        return new SystemPathProfileResult(paths, truncated);

        void Walk(SavedLayoutNode node, bool arrivedSecondary, List<PathStep> stepsBefore, HashSet<string> visited,
            string startId)
        {
            if (truncated || !visited.Add(node.Id))
                return;
            var steps = new List<PathStep>(stepsBefore);
            var children = LayoutTopology.ConnectionsFrom(document, node).OrderBy(x => x.Downstream.Sequence).ToArray();
            var diverging = LayoutTopology.IsDiverging(node);
            if (!diverging || children.Length == 0)
                Add(steps, NodeStep(node, arrivedSecondary, nextChild: null));

            if (children.Length == 0)
            {
                if (routes.Count >= MaximumPaths)
                {
                    truncated = true;
                    return;
                }
                routes.Add((startId, node.Id, steps));
                return;
            }

            foreach (var child in children)
            {
                var childSteps = new List<PathStep>(steps);
                if (diverging)
                    Add(childSteps, NodeStep(node, arrivedSecondary, child.Downstream));
                if (ConnectionStep(child.Downstream, child.Secondary) is { } connection)
                    Add(childSteps, connection);
                Walk(child.Downstream, child.Secondary, childSteps, new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase),
                    startId);
            }
        }

        static void Add(List<PathStep> steps, PathStep step)
        {
            var before = steps.Count == 0 ? 0.0 : steps[^1].CumulativeLossPa;
            steps.Add(step with { CumulativeLossPa = before + step.LossPa });
        }

        PathStep NodeStep(SavedLayoutNode node, bool arrivedSecondary, SavedLayoutNode? nextChild)
        {
            if (node.LinkedFittingSequence is int fittingSequence
                && document.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence) is { } fitting)
            {
                var path = CriticalPathAnalyzer.JunctionPathForRoute(node, arrivedSecondary, nextChild);
                return Step(CriticalPathAnalyzer.FittingKey(fittingSequence) + "|" + path, fitting.Id, "Fitting", () => path is JunctionPath value
                    ? fitting.CalculateForPath(value).PressureLossPa
                    : fitting.Calculate().PressureLossPa);
            }
            if (node.LinkedDuctSequence is int ductSequence
                && document.Ducts.FirstOrDefault(x => x.Sequence == ductSequence) is { } duct)
                return Step(CriticalPathAnalyzer.DuctKey(ductSequence), duct.Id, "Duct",
                    () => duct.Calculate().TotalPressureLossUpperPa);
            var loss = node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                or LayoutNodeType.CustomComponent
                ? node.PressureDropPa
                : 0.0;
            return new PathStep(CriticalPathAnalyzer.LayoutKey(node.Sequence), node.Id, KindOf(node), loss, 0.0, true);
        }

        PathStep? ConnectionStep(SavedLayoutNode downstream, bool secondary)
        {
            var sequence = secondary ? downstream.SecondaryConnectionDuctSequence : downstream.ConnectionDuctSequence;
            if (sequence is null)
                return new PathStep($"C:{downstream.Sequence}:{(secondary ? 2 : 1)}", $"Connection into {downstream.Id}",
                    "Unsized connection", 0.0, 0.0, false);
            var duct = LayoutTopology.DuctForConnection(document, downstream, secondary);
            if (duct is null)
                return new PathStep(CriticalPathAnalyzer.DuctKey(sequence.Value), $"D-{sequence:000}", "Duct", 0.0, 0.0, false);
            return Step(CriticalPathAnalyzer.DuctKey(duct.Sequence), duct.Id, "Duct",
                () => duct.Calculate().TotalPressureLossUpperPa);
        }

        // cacheKey is the item key, plus "|path" for a fitting so a junction's two flow paths are kept apart.
        PathStep Step(string cacheKey, string id, string kind, Func<double> calculate)
        {
            if (calculated.TryGetValue(cacheKey, out var known))
                return known;
            var key = cacheKey.Split('|')[0];
            PathStep step;
            try
            {
                var value = calculate();
                step = double.IsFinite(value)
                    ? new PathStep(key, id, kind, value, 0.0, true)
                    : new PathStep(key, id, kind, 0.0, 0.0, false);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or KeyNotFoundException or OverflowException)
            {
                step = new PathStep(key, id, kind, 0.0, 0.0, false);
            }
            calculated[cacheKey] = step;
            return step;
        }

        static string KindOf(SavedLayoutNode node) => node.Type switch
        {
            LayoutNodeType.Grd => "GRD",
            LayoutNodeType.CustomComponent => "Custom component",
            LayoutNodeType.Reservoir => "Source/Sink",
            LayoutNodeType.Fan when node.IsAirHandlingUnit => "Air handling unit",
            _ => node.Type.ToString()
        };
    }
}
