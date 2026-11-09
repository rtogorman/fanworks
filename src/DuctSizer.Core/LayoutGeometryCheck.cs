namespace DuctSizer.Core;

/// <summary>Kind of layout geometry problem found by <see cref="LayoutGeometryCheck"/>.</summary>
public enum LayoutGeometryIssueKind
{
    /// <summary>
    /// A connection's stored X/Y/Z offset disagrees with the positions of its two ends. This happens when a
    /// component is reached by two routes (for example a converging fitting) whose offsets do not close.
    /// </summary>
    ConnectionDoesNotClose,

    /// <summary>The duct record drawn on a connection carries a different X/Y/Z offset than the connection.</summary>
    DuctOffsetDiffers,

    /// <summary>
    /// A duct's entered length is shorter than the straight-line distance between the components it joins, so its
    /// friction loss is understated. A longer length (a routed duct) is normal and is not flagged.
    /// </summary>
    DuctShorterThanRun,

    /// <summary>
    /// A component is not connected to the coordinate origin and has no absolute anchor, so it has no defined
    /// drawing position (the Designer shows it at its screen-grid position).
    /// </summary>
    PositionUndefined
}

/// <summary>One layout geometry problem.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="ItemId">The component or duct concerned.</param>
/// <param name="Message">Plain-language description with the measured mismatch.</param>
/// <param name="MismatchM">Size of the mismatch in metres (0 for <see cref="LayoutGeometryIssueKind.PositionUndefined"/>).</param>
public sealed record LayoutGeometryIssue(LayoutGeometryIssueKind Kind, string ItemId, string Message, double MismatchM);

/// <summary>
/// Checks that the stored layout geometry is self-consistent (finding 7 of the v0.92 review).
/// </summary>
/// <remarks>
/// Positions are stored as offsets from each component's upstream component; absolute positions are derived by
/// <see cref="LayoutCoordinateResolver"/>, which walks the connections from the coordinate origin in document order
/// and keeps the first position it reaches for each component. When a component can be reached by two routes and
/// the offsets disagree, the second route is silently ignored. This check reports every such disagreement, plus
/// duct records whose offsets or lengths do not match the drawn connection. It changes nothing.
/// </remarks>
public static class LayoutGeometryCheck
{
    /// <summary>Mismatches smaller than this (1 mm) are rounding, not errors.</summary>
    public const double ToleranceM = 0.001;

    /// <summary>Returns every geometry problem in the document, ordered by component sequence.</summary>
    public static IReadOnlyList<LayoutGeometryIssue> Check(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<LayoutGeometryIssue>();
        var coordinates = LayoutCoordinateResolver.Resolve(document);
        var nodes = document.LayoutNodes
            .Where(x => x.Type != LayoutNodeType.CalculationPoint)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.Values.OrderBy(x => x.Sequence))
        {
            if (!coordinates.TryGetValue(node.Sequence, out var position))
            {
                issues.Add(new LayoutGeometryIssue(LayoutGeometryIssueKind.PositionUndefined, node.Id,
                    $"{node.Id} is not connected to the coordinate origin and has no fixed position.", 0.0));
                continue;
            }
            CheckConnection(node, position, secondary: false);
            CheckConnection(node, position, secondary: true);
        }
        return issues;

        void CheckConnection(SavedLayoutNode node, LayoutCoordinate position, bool secondary)
        {
            var parentId = secondary ? node.SecondaryParentNodeId : node.ParentNodeId;
            if (!nodes.TryGetValue(parentId, out var parent) || !coordinates.TryGetValue(parent.Sequence, out var parentPosition))
                return;
            var stored = secondary
                ? (X: node.SecondaryDeltaXM, Y: node.SecondaryDeltaYM, Z: node.SecondaryDeltaZM)
                : (X: node.DeltaXM, Y: node.DeltaYM, Z: node.DeltaZM);
            var actual = (X: position.XM - parentPosition.XM, Y: position.YM - parentPosition.YM, Z: position.ZM - parentPosition.ZM);
            var closure = Distance(stored.X - actual.X, stored.Y - actual.Y, stored.Z - actual.Z);
            var route = $"{parent.Id} → {node.Id}{(secondary ? " (second inlet)" : string.Empty)}";
            if (closure > ToleranceM)
                issues.Add(new LayoutGeometryIssue(LayoutGeometryIssueKind.ConnectionDoesNotClose, node.Id,
                    $"Connection {route}: its entered offset does not close with the rest of the layout " +
                    $"(off by {FormatLength(closure)}). Positions follow the first route drawn.", closure));

            var duct = LayoutTopology.DuctForConnection(document, node, secondary);
            if (duct is null)
                return;
            var ductOffsetDifference = Distance(duct.DeltaXM - stored.X, duct.DeltaYM - stored.Y, duct.DeltaZM - stored.Z);
            if (ductOffsetDifference > ToleranceM)
                issues.Add(new LayoutGeometryIssue(LayoutGeometryIssueKind.DuctOffsetDiffers, duct.Id,
                    $"{duct.Id} on {route}: the duct's X/Y/Z offset differs from the drawn connection by {FormatLength(ductOffsetDifference)}.",
                    ductOffsetDifference));
            var run = Distance(actual.X, actual.Y, actual.Z);
            if (double.IsFinite(duct.LengthM) && duct.LengthM > 0 && duct.LengthM < run - ToleranceM)
                issues.Add(new LayoutGeometryIssue(LayoutGeometryIssueKind.DuctShorterThanRun, duct.Id,
                    $"{duct.Id} on {route}: entered length {FormatLength(duct.LengthM)} is shorter than the " +
                    $"{FormatLength(run)} straight run between the components; its friction loss is understated.",
                    run - duct.LengthM));
        }
    }

    private static double Distance(double x, double y, double z) => Math.Sqrt(x * x + y * y + z * z);

    private static string FormatLength(double metres) =>
        $"{UnitConversions.MToFeet(metres):0.##} ft ({metres:0.###} m)";
}
