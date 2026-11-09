namespace DuctSizer.Core;

/// <summary>
/// Per-component-type validation of the saved Layout Designer model (finding 13 of the v0.92 review).
/// </summary>
/// <remarks>
/// <para>
/// Every layout component is stored as one <see cref="SavedLayoutNode"/> whatever its type, so the file format
/// can represent combinations the program never intends. Rather than change the saved format, this check states
/// the rules for each type in one place and reports every violation. It changes nothing.
/// </para>
/// <para>Fields that apply by type:</para>
/// <list type="table">
/// <listheader><term>Type</term><description>Meaningful fields (in addition to identity, branch, airflow, temperature, position)</description></listheader>
/// <item><term>Reservoir</term><description><c>SpaceRole</c> (source or sink), <c>PressurePa</c> for a source.</description></item>
/// <item><term>Fan</term><description><c>FanStaticPressurePa</c> (0 or more), <c>FanEfficiencyPercent</c> (above 0, at most 100).</description></item>
/// <item><term>Coil, Filter, CustomComponent</term><description><c>PressureDropPa</c> (0 or more), <c>Description</c>.</description></item>
/// <item><term>Grd</term><description><c>PressureDropPa</c> and the <c>Grd*</c> selection fields.</description></item>
/// <item><term>Fitting</term><description><c>FittingGroup</c>, <c>LinkedFittingSequence</c>, <c>ElbowAngleDegrees</c>;
/// a second inlet (<c>SecondaryParentNodeId</c>) only for a converging fitting.</description></item>
/// <item><term>Duct</term><description><c>LinkedDuctSequence</c>.</description></item>
/// <item><term>CalculationPoint</term><description><c>CalculationConnectionNodeId</c>, <c>CalculationConnectionSecondary</c>,
/// <c>CalculationConnectionFraction</c> (0 to 1), <c>CalculationPointType</c>.</description></item>
/// </list>
/// </remarks>
public static class LayoutModelCheck
{
    /// <summary>Returns every model problem, ordered by component sequence.</summary>
    public static IReadOnlyList<SystemCalculationIssue> Check(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<SystemCalculationIssue>();
        var nodes = new Dictionary<string, SavedLayoutNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.LayoutNodes.OrderBy(x => x.Sequence))
        {
            if (!nodes.TryAdd(node.Id, node))
                issues.Add(new SystemCalculationIssue(node.Id, $"{node.Id} is used by more than one component."));
        }
        var ducts = document.Ducts.Select(x => x.Sequence).ToHashSet();
        var fittings = document.Fittings.Select(x => x.Sequence).ToHashSet();

        foreach (var node in document.LayoutNodes.OrderBy(x => x.Sequence))
        {
            void Add(string message) => issues.Add(new SystemCalculationIssue(node.Id, $"{node.Id}: {message}"));

            if (!double.IsFinite(node.AirflowM3PerS) || node.AirflowM3PerS < 0)
                Add("airflow must be a number of 0 or more.");
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId) && !nodes.ContainsKey(node.ParentNodeId))
                Add($"its upstream component {node.ParentNodeId} does not exist.");
            if (!string.IsNullOrWhiteSpace(node.SecondaryParentNodeId))
            {
                if (!LayoutTopology.IsConverging(node))
                    Add("only a converging fitting can have a second inlet.");
                else if (!nodes.ContainsKey(node.SecondaryParentNodeId))
                    Add($"its second upstream component {node.SecondaryParentNodeId} does not exist.");
            }
            if (node.ConnectionDuctSequence is int primaryDuct && !ducts.Contains(primaryDuct))
                Add($"its inlet connection refers to duct D-{primaryDuct:000}, which does not exist.");
            if (node.SecondaryConnectionDuctSequence is int secondaryDuct && !ducts.Contains(secondaryDuct))
                Add($"its second inlet connection refers to duct D-{secondaryDuct:000}, which does not exist.");
            if (node.LinkedFittingSequence is int fitting && !fittings.Contains(fitting))
                Add($"it is linked to fitting F-{fitting:000}, which does not exist.");
            if (node.LinkedDuctSequence is int linkedDuct && !ducts.Contains(linkedDuct))
                Add($"it is linked to duct D-{linkedDuct:000}, which does not exist.");

            switch (node.Type)
            {
                case LayoutNodeType.Reservoir:
                    if (!SpaceRoles.IsSource(node.SpaceRole) && !SpaceRoles.IsSink(node.SpaceRole))
                        Add($"space role \"{node.SpaceRole}\" is neither a source nor a sink.");
                    break;
                case LayoutNodeType.Fan:
                    if (!double.IsFinite(node.FanStaticPressurePa) || node.FanStaticPressurePa < 0)
                        Add("fan static pressure must be a number of 0 or more.");
                    if (!double.IsFinite(node.FanEfficiencyPercent) || node.FanEfficiencyPercent <= 0 || node.FanEfficiencyPercent > 100)
                        Add("fan efficiency must be above 0% and at most 100%.");
                    break;
                case LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CustomComponent:
                    if (!double.IsFinite(node.PressureDropPa) || node.PressureDropPa < 0)
                        Add("pressure drop must be a number of 0 or more.");
                    break;
                case LayoutNodeType.CalculationPoint:
                    if (!nodes.ContainsKey(node.CalculationConnectionNodeId))
                        Add("it does not observe an existing connection.");
                    if (!double.IsFinite(node.CalculationConnectionFraction)
                        || node.CalculationConnectionFraction < 0 || node.CalculationConnectionFraction > 1)
                        Add("its position along the connection must be between 0 and 1.");
                    break;
            }
        }

        // A component that is its own upstream (directly or through others) has no defined flow direction.
        foreach (var node in document.LayoutNodes.Where(x => x.Type != LayoutNodeType.CalculationPoint).OrderBy(x => x.Sequence))
        {
            if (ReachesItself(node))
                issues.Add(new SystemCalculationIssue(node.Id,
                    $"{node.Id}: the connections upstream of it lead back to it (a closed loop with no flow direction)."));
        }
        return issues;

        bool ReachesItself(SavedLayoutNode start)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<SavedLayoutNode>();
            Push(start);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var parentId in new[] { current.ParentNodeId, current.SecondaryParentNodeId })
                {
                    if (string.IsNullOrWhiteSpace(parentId))
                        continue;
                    if (string.Equals(parentId, start.Id, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (nodes.TryGetValue(parentId, out var parent))
                        Push(parent);
                }
            }
            return false;

            void Push(SavedLayoutNode node)
            {
                if (visited.Add(node.Id))
                    stack.Push(node);
            }
        }
    }
}
