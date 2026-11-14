namespace DuctSizer.Core;

/// <summary>A direction or offset in the drawing's coordinates (metres: X and Y horizontal, Z up).</summary>
public readonly record struct LayoutVector(double XM, double YM, double ZM)
{
    public double Length => Math.Sqrt(XM * XM + YM * YM + ZM * ZM);

    /// <summary>The horizontal (plan) length.</summary>
    public double HorizontalLength => Math.Sqrt(XM * XM + YM * YM);

    public static double Dot(LayoutVector a, LayoutVector b) => a.XM * b.XM + a.YM * b.YM + a.ZM * b.ZM;
}

/// <summary>
/// Directions of the drawn connections (round Z). Ducts run straight from the centre of one component to the centre
/// of the next, along the connection's stored X/Y/Z offset, so a duct can run on a diagonal in plan or slope up or
/// down. From those directions:
/// <list type="bullet">
/// <item>an elbow's angle is the angle the air turns through it (90° for a right angle, 45° for a 45, 0° straight on);</item>
/// <item>a component that cannot turn the air (a transition, damper, VAV box, coil, filter or custom component) must
/// have its outlet in line with its inlet, or an elbow is missing;</item>
/// <item>a duct's slope is its angle above or below horizontal.</item>
/// </list>
/// Offsets shorter than <see cref="MinimumLengthM"/> carry no direction.
/// </summary>
public static class LayoutDirections
{
    /// <summary>Offsets shorter than 1 mm have no direction.</summary>
    public const double MinimumLengthM = 0.001;

    /// <summary>Turns smaller than this (degrees) are treated as straight on (drawing rounding).</summary>
    public const double StraightToleranceDegrees = 1.0;

    /// <summary>Offset of a connection, from its upstream component to <paramref name="downstream"/>.</summary>
    public static LayoutVector ConnectionVector(SavedLayoutNode downstream, bool secondary)
    {
        ArgumentNullException.ThrowIfNull(downstream);
        return secondary
            ? new LayoutVector(downstream.SecondaryDeltaXM, downstream.SecondaryDeltaYM, downstream.SecondaryDeltaZM)
            : new LayoutVector(downstream.DeltaXM, downstream.DeltaYM, downstream.DeltaZM);
    }

    /// <summary>
    /// Angle between two directions in degrees: 0 when the air carries straight on, 90 for a right-angle turn, 180 for
    /// a full reversal. Null when either offset is too short to have a direction.
    /// </summary>
    public static double? TurnDegrees(LayoutVector incoming, LayoutVector outgoing)
    {
        var lengths = incoming.Length * outgoing.Length;
        if (incoming.Length < MinimumLengthM || outgoing.Length < MinimumLengthM)
            return null;
        var cosine = Math.Clamp(LayoutVector.Dot(incoming, outgoing) / lengths, -1.0, 1.0);
        return Math.Acos(cosine) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Slope of an offset above (positive) or below (negative) horizontal, in degrees: 0 for a level duct, ±90 for a
    /// vertical riser. Null when the offset is too short.
    /// </summary>
    public static double? SlopeDegrees(LayoutVector offset) =>
        offset.Length < MinimumLengthM ? null : Math.Atan2(offset.ZM, offset.HorizontalLength) * 180.0 / Math.PI;

    /// <summary>
    /// True when a duct both rises (or falls) and runs horizontally: neither level nor a vertical riser. The Designer
    /// asks before drawing one.
    /// </summary>
    public static bool IsSloped(LayoutVector offset) =>
        Math.Abs(offset.ZM) >= MinimumLengthM && offset.HorizontalLength >= MinimumLengthM;

    /// <summary>
    /// True for components the air passes straight through, which cannot change its direction: transitions, dampers,
    /// VAV boxes, coils, filters and custom components. Elbows turn the air; tees, wyes, plenums, fans, air handling
    /// units, spaces and GRDs may have their connections in any direction.
    /// </summary>
    public static bool CannotTurn(SavedLayoutNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Type switch
        {
            LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.CustomComponent => true,
            LayoutNodeType.Fitting => node.FittingGroup is "Transitions" or "Dampers"
                                      || string.Equals(node.FittingGroup, FittingCatalog.VavSelectionGroup, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>True for an elbow component.</summary>
    public static bool IsElbow(SavedLayoutNode node) =>
        node.Type == LayoutNodeType.Fitting && string.Equals(node.FittingGroup, "Elbows", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The angle the air turns through <paramref name="node"/>: between its (first) inlet connection and its first
    /// outlet connection. Null until both are drawn with a length.
    /// </summary>
    public static double? TurnDegreesThrough(DuctSystemDocument document, SavedLayoutNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.ParentNodeId))
            return null;
        if (LayoutTopology.ConnectionsFrom(document, node).Cast<LayoutConnection?>().FirstOrDefault() is not { } outlet)
            return null;
        return TurnDegrees(ConnectionVector(node, secondary: false), ConnectionVector(outlet.Downstream, outlet.Secondary));
    }

    /// <summary>
    /// The components that turn the air although they cannot (see <see cref="CannotTurn"/>), with the turn in degrees:
    /// an elbow is missing there, so the loss of the turn is not counted.
    /// </summary>
    public static IReadOnlyList<(SavedLayoutNode Node, double TurnDegrees)> MissingElbows(DuctSystemDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var missing = new List<(SavedLayoutNode, double)>();
        foreach (var node in document.LayoutNodes.Where(CannotTurn).OrderBy(x => x.Sequence))
            if (TurnDegreesThrough(document, node) is double turn && turn > StraightToleranceDegrees)
                missing.Add((node, turn));
        return missing;
    }
}
