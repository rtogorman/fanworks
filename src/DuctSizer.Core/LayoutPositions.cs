namespace DuctSizer.Core;

/// <summary>
/// Keeps the stored absolute positions of Layout Designer components and the offsets used for editing consistent
/// (step 8 of the correction plan).
/// </summary>
/// <remarks>
/// <para>
/// From schema 13 every positioned component stores its absolute X/Y/Z (<see cref="SavedLayoutNode.XM"/> etc.), and
/// those stored positions are the authoritative geometry: when a file is loaded, each connection's offset is
/// rebuilt from the absolute positions of its two ends (<see cref="DeriveOffsetsFromAbsolute"/>), so an offset can
/// never contradict a position. Users still enter positions relative to a neighbouring component; the Designer
/// edits offsets and <see cref="StoreAbsolute"/> turns them into absolute positions before every save.
/// </para>
/// <para>
/// Because each component has exactly one position, a component reached by two routes (a converging fitting) cannot
/// have offsets that fail to close: <see cref="StoreAbsolute"/> rewrites the second inlet's offset (and the duct
/// record on that connection) from the two positions.
/// </para>
/// </remarks>
public static class LayoutPositions
{
    /// <summary>
    /// Resolves every component's absolute position (<see cref="LayoutCoordinateResolver"/>), stores it on the
    /// component, and makes second-inlet offsets agree with the stored positions. Components with no defined
    /// position are marked <see cref="SavedLayoutNode.HasStoredPosition"/> = false.
    /// </summary>
    public static void StoreAbsolute(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var coordinates = LayoutCoordinateResolver.Resolve(document);
        var nodes = document.LayoutNodes.Where(x => x.Type != LayoutNodeType.CalculationPoint).ToArray();
        foreach (var node in nodes)
        {
            if (coordinates.TryGetValue(node.Sequence, out var position))
            {
                node.XM = position.XM;
                node.YM = position.YM;
                node.ZM = position.ZM;
                node.HasStoredPosition = true;
            }
            else
            {
                node.HasStoredPosition = false;
            }
        }

        var byId = nodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes.Where(x => x.HasStoredPosition))
        {
            if (!byId.TryGetValue(node.SecondaryParentNodeId, out var secondaryParent) || !secondaryParent.HasStoredPosition)
                continue;
            node.SecondaryDeltaXM = node.XM - secondaryParent.XM;
            node.SecondaryDeltaYM = node.YM - secondaryParent.YM;
            node.SecondaryDeltaZM = node.ZM - secondaryParent.ZM;
            if (LayoutTopology.DuctForConnection(document, node, secondary: true) is { } duct)
            {
                duct.DeltaXM = node.SecondaryDeltaXM;
                duct.DeltaYM = node.SecondaryDeltaYM;
                duct.DeltaZM = node.SecondaryDeltaZM;
            }
        }
    }

    /// <summary>
    /// Rebuilds each connection's offset from the stored absolute positions of its two ends, and the coordinate
    /// origin from the stored position of the origin component. Used when a schema-13 file is loaded, so the stored
    /// absolute positions win over any stored offset. An absolute anchor of a detached fragment is itself an
    /// absolute position and is kept as entered.
    /// </summary>
    public static void DeriveOffsetsFromAbsolute(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.LayoutNodes.Where(x => x.Type != LayoutNodeType.CalculationPoint).ToArray();
        var byId = nodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes.Where(x => x.HasStoredPosition && IsFinite(x)))
        {
            if (byId.TryGetValue(node.ParentNodeId, out var parent) && parent.HasStoredPosition && IsFinite(parent))
            {
                node.DeltaXM = node.XM - parent.XM;
                node.DeltaYM = node.YM - parent.YM;
                node.DeltaZM = node.ZM - parent.ZM;
            }
            if (byId.TryGetValue(node.SecondaryParentNodeId, out var secondaryParent)
                && secondaryParent.HasStoredPosition && IsFinite(secondaryParent))
            {
                node.SecondaryDeltaXM = node.XM - secondaryParent.XM;
                node.SecondaryDeltaYM = node.YM - secondaryParent.YM;
                node.SecondaryDeltaZM = node.ZM - secondaryParent.ZM;
            }
        }

        var origin = byId.TryGetValue(document.CoordinateOriginNodeId, out var selected)
            ? selected
            : nodes.OrderBy(x => x.Sequence).FirstOrDefault();
        if (origin is { HasStoredPosition: true } && IsFinite(origin))
        {
            document.CoordinateOriginXM = origin.XM;
            document.CoordinateOriginYM = origin.YM;
            document.CoordinateOriginZM = origin.ZM;
        }
    }

    private static bool IsFinite(SavedLayoutNode node) =>
        double.IsFinite(node.XM) && double.IsFinite(node.YM) && double.IsFinite(node.ZM);
}
