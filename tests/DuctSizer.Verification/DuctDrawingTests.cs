using DuctSizer.Core;

/// <summary>
/// Round Z (v0.107): ducts run straight between component centres, so an elbow's angle is the angle between its drawn
/// ducts, a component that cannot turn the air is reported when it does, and a sloping duct is recognised. Expected
/// values are worked by hand in the comments.
/// </summary>
internal static class DuctDrawingTests
{
    private static SavedLayoutNode Node(DuctSystemDocument document, LayoutNodeType type, SavedLayoutNode? parent,
        double dxFeet, double dyFeet, double dzFeet, string? group = null)
    {
        var node = SystemTests.AddNode(document, type, parent, duct: null);
        node.FittingGroup = group ?? string.Empty;
        node.DeltaXM = UnitConversions.FeetToM(dxFeet);
        node.DeltaYM = UnitConversions.FeetToM(dyFeet);
        node.DeltaZM = UnitConversions.FeetToM(dzFeet);
        node.ConnectionDeltaConfirmed = parent is not null;
        return node;
    }

    /// <summary>
    /// Fan → 10 ft along +Y → elbow A → 10 ft along +X and 10 ft along +Y (a 45° diagonal in plan) → elbow B → 10 ft
    /// along +X → elbow C → 10 ft straight down → space.
    /// A: between (0,1,0) and (1,1,0)/√2, cos θ = 1/√2, θ = 45°. B: between (1,1,0)/√2 and (1,0,0), θ = 45°.
    /// C: between (1,0,0) and (0,0,-1), θ = 90°. The angle is written to the elbow and to its linked fitting's
    /// "angle" input.
    /// </summary>
    public static void ElbowAngleFollowsTheDrawnDucts()
    {
        var document = new DuctSystemDocument();
        var fan = Node(document, LayoutNodeType.Fan, null, 0, 0, 0);
        var a = Node(document, LayoutNodeType.Fitting, fan, 0, 10, 0, "Elbows");
        var b = Node(document, LayoutNodeType.Fitting, a, 10, 10, 0, "Elbows");
        var c = Node(document, LayoutNodeType.Fitting, b, 10, 0, 0, "Elbows");
        var space = Node(document, LayoutNodeType.Reservoir, c, 0, 0, -10);
        space.SpaceRole = "Space Sink";
        var fitting = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(),
            FittingCode = "3-5",
            Inputs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["angle"] = 90 }
        };
        document.Fittings.Add(fitting);
        a.LinkedFittingSequence = fitting.Sequence;

        Near(LayoutDirections.TurnDegreesThrough(document, a) ?? double.NaN, 45, 1e-9, "elbow A turn");
        Near(LayoutDirections.TurnDegreesThrough(document, b) ?? double.NaN, 45, 1e-9, "elbow B turn");
        Near(LayoutDirections.TurnDegreesThrough(document, c) ?? double.NaN, 90, 1e-9, "elbow C turn");
        SystemSolver.Solve(document);
        Near(a.ElbowAngleDegrees, 45, 1e-9, "elbow A angle after solving");
        Near(b.ElbowAngleDegrees, 45, 1e-9, "elbow B angle after solving");
        Near(c.ElbowAngleDegrees, 90, 1e-9, "elbow C angle after solving");
        Near(fitting.Inputs["angle"], 45, 1e-9, "the linked fitting's angle input");

        // An elbow with only its inlet drawn keeps its stored angle.
        var open = Node(document, LayoutNodeType.Fitting, space, 5, 0, 0, "Elbows");
        open.ElbowAngleDegrees = 60;
        SystemSolver.Solve(document);
        Require(LayoutDirections.TurnDegreesThrough(document, open) is null && open.ElbowAngleDegrees == 60,
            "An elbow without an outlet duct keeps its angle.");
    }

    /// <summary>
    /// A transition whose outlet duct runs at right angles to its inlet turns the air 90° although it cannot: the model
    /// check reports a missing elbow. A damper with its outlet in line (both along +X) is fine.
    /// </summary>
    public static void TurnAtAComponentThatCannotTurnIsReported()
    {
        var document = new DuctSystemDocument();
        var fan = Node(document, LayoutNodeType.Fan, null, 0, 0, 0);
        var transition = Node(document, LayoutNodeType.Fitting, fan, 0, 8, 0, "Transitions");
        var damper = Node(document, LayoutNodeType.Fitting, transition, 6, 0, 0, "Dampers");
        var space = Node(document, LayoutNodeType.Reservoir, damper, 4, 0, 0);
        space.SpaceRole = "Space Sink";

        var missing = LayoutDirections.MissingElbows(document);
        Require(missing.Count == 1 && missing[0].Node == transition && Math.Abs(missing[0].TurnDegrees - 90) < 1e-9,
            $"Only the transition should be reported, turning 90°. Got {string.Join(", ", missing.Select(x => $"{x.Node.Id} {x.TurnDegrees:0.#}"))}.");
        Require(LayoutModelCheck.Check(document).Any(issue => issue.ItemId == transition.Id && issue.Message.Contains("turns 90°", StringComparison.Ordinal)),
            "The model check should name the transition and the 90° turn.");
        Require(!LayoutModelCheck.Check(document).Any(issue => issue.ItemId == damper.Id),
            "A damper with its outlet in line is not reported.");
        Require(LayoutDirections.CannotTurn(damper) && !LayoutDirections.CannotTurn(fan)
                && !LayoutDirections.CannotTurn(new SavedLayoutNode { Type = LayoutNodeType.Fitting, FittingGroup = "Elbows" })
                && !LayoutDirections.CannotTurn(new SavedLayoutNode { Type = LayoutNodeType.Fitting, FittingGroup = "Diverting Fittings" }),
            "Elbows, splits and fans may turn the air; dampers may not.");
    }

    /// <summary>
    /// Slope: 10 ft across and 5 ft up rises at atan(5/10) = 26.565°; a level diagonal and a vertical riser are not
    /// "sloped". Turn angles: straight on 0°, a reversal 180°.
    /// </summary>
    public static void SlopeAndTurnAngles()
    {
        var up = new LayoutVector(UnitConversions.FeetToM(10), 0, UnitConversions.FeetToM(5));
        Near(LayoutDirections.SlopeDegrees(up) ?? double.NaN, 26.565051177077990, 1e-9, "slope of 5 ft rise over 10 ft");
        Near(LayoutDirections.SlopeDegrees(new LayoutVector(0, 3, -3)) ?? double.NaN, -45, 1e-9, "45° fall");
        Require(LayoutDirections.IsSloped(up) && !LayoutDirections.IsSloped(new LayoutVector(3, 3, 0))
                && !LayoutDirections.IsSloped(new LayoutVector(0, 0, 3)),
            "Only a duct that both rises and runs across is sloped.");
        Near(LayoutDirections.TurnDegrees(new LayoutVector(1, 0, 0), new LayoutVector(5, 0, 0)) ?? double.NaN, 0, 1e-9, "straight on");
        Near(LayoutDirections.TurnDegrees(new LayoutVector(1, 0, 0), new LayoutVector(-2, 0, 0)) ?? double.NaN, 180, 1e-9, "reversal");
        Require(LayoutDirections.TurnDegrees(new LayoutVector(0, 0, 0), new LayoutVector(1, 0, 0)) is null,
            "A zero-length offset has no direction.");
    }

    private static void Near(double actual, double expected, double tolerance, string what)
    {
        if (!(Math.Abs(actual - expected) <= tolerance))
            throw new Exception($"{what}: expected {expected}, got {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
