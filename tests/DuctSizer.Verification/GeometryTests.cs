using DuctSizer.Core;

/// <summary>Checks for <see cref="LayoutGeometryCheck"/> (step 8 of the correction plan, finding 7).</summary>
internal static class GeometryTests
{
    public static void ConsistentLayoutHasNoGeometryIssues()
    {
        var system = SystemTests.BuildDivergingSystem();
        Place(system.Splitter, system.CommonDuct, 6, 0, 0);
        Place(system.MainSink, system.MainDuct, 9, 0, 0);
        Place(system.BranchSink, system.BranchDuct, 0, 4, 0);
        var issues = LayoutGeometryCheck.Check(system.Document);
        if (issues.Count != 0)
            throw new Exception("A consistent layout must not report geometry issues: " + string.Join("; ", issues.Select(x => x.Message)));
    }

    public static void LoopThatDoesNotCloseIsFlagged()
    {
        var document = new DuctSystemDocument();
        var source = SystemTests.AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        source.SpaceRole = "Space Source";
        var elbowA = SystemTests.AddNode(document, LayoutNodeType.Fitting, source, duct: null);
        elbowA.DeltaXM = 3;
        var elbowB = SystemTests.AddNode(document, LayoutNodeType.Fitting, source, duct: null);
        elbowB.DeltaYM = 3;
        var merger = SystemTests.AddNode(document, LayoutNodeType.Fitting, elbowA, duct: null);
        merger.FittingGroup = "Converging Fittings";
        merger.DeltaYM = 3;                          // A + (0, 3) = (3, 3)
        merger.SecondaryParentNodeId = elbowB.Id;
        merger.SecondaryDeltaXM = 3.5;               // B + (3.5, 0) = (3.5, 3): 0.5 m short of closing
        var issues = LayoutGeometryCheck.Check(document);
        var loop = issues.SingleOrDefault(x => x.Kind == LayoutGeometryIssueKind.ConnectionDoesNotClose);
        if (loop is null || loop.ItemId != merger.Id || Math.Abs(loop.MismatchM - 0.5) > 1e-9)
            throw new Exception("A converging loop whose offsets do not close must be flagged with its mismatch.");

        merger.SecondaryDeltaXM = 3.0;
        if (LayoutGeometryCheck.Check(document).Count != 0)
            throw new Exception("A loop that closes must not be flagged.");
    }

    public static void DuctOffsetAndLengthAreCheckedAgainstTheConnection()
    {
        var system = SystemTests.BuildDivergingSystem();
        Place(system.Splitter, system.CommonDuct, 6, 0, 0);
        Place(system.MainSink, system.MainDuct, 9, 0, 0);
        Place(system.BranchSink, system.BranchDuct, 0, 4, 0);

        system.MainDuct.DeltaXM = 8;
        var offset = LayoutGeometryCheck.Check(system.Document);
        if (!offset.Any(x => x.Kind == LayoutGeometryIssueKind.DuctOffsetDiffers && x.ItemId == system.MainDuct.Id))
            throw new Exception("A duct whose offset differs from its drawn connection must be flagged.");
        system.MainDuct.DeltaXM = 9;

        system.BranchDuct.LengthM = 3.0;             // straight run is 4 m
        var shortDuct = LayoutGeometryCheck.Check(system.Document);
        if (!shortDuct.Any(x => x.Kind == LayoutGeometryIssueKind.DuctShorterThanRun && x.ItemId == system.BranchDuct.Id))
            throw new Exception("A duct shorter than the straight run between its components must be flagged.");
        system.BranchDuct.LengthM = 7.0;             // a routed duct longer than the run is normal
        if (LayoutGeometryCheck.Check(system.Document).Count != 0)
            throw new Exception("A duct longer than the straight run must not be flagged.");
    }

    public static void UnplacedComponentIsFlaggedUnlessAnchored()
    {
        var system = SystemTests.BuildDivergingSystem();
        var loose = SystemTests.AddNode(system.Document, LayoutNodeType.Coil, parent: null, duct: null);
        var issues = LayoutGeometryCheck.Check(system.Document);
        if (!issues.Any(x => x.Kind == LayoutGeometryIssueKind.PositionUndefined && x.ItemId == loose.Id))
            throw new Exception("A component with no route to the origin and no anchor must be flagged.");
        loose.HasAbsoluteCoordinateAnchor = true;
        loose.AbsoluteAnchorXM = 20;
        if (LayoutGeometryCheck.Check(system.Document).Any(x => x.ItemId == loose.Id))
            throw new Exception("An anchored component has a defined position and must not be flagged.");
    }

    private static void Place(SavedLayoutNode node, SavedDuct duct, double x, double y, double z)
    {
        node.DeltaXM = x;
        node.DeltaYM = y;
        node.DeltaZM = z;
        duct.DeltaXM = x;
        duct.DeltaYM = y;
        duct.DeltaZM = z;
        duct.LengthM = Math.Max(duct.LengthM, Math.Sqrt(x * x + y * y + z * z));
    }
}

/// <summary>Checks for <see cref="BoundedHistory{T}"/> (undo/redo capacity, finding 14).</summary>
internal static class HistoryTests
{
    public static void UndoHistoryIsBounded()
    {
        var history = new BoundedHistory<int>(3);
        for (var value = 1; value <= 5; value++)
            history.Push(value);
        if (history.Count != 3 || history.Pop() != 5 || history.Pop() != 4 || history.Pop() != 3 || history.Count != 0)
            throw new Exception("A full history must keep the newest entries and discard the oldest.");
        var empty = false;
        try { history.Pop(); }
        catch (InvalidOperationException) { empty = true; }
        if (!empty)
            throw new Exception("Popping an empty history must fail, not return a default value.");
    }
}

/// <summary>Checks for <see cref="LayoutModelCheck"/> (per-type model rules, finding 13).</summary>
internal static class ModelCheckTests
{
    public static void ValidSystemHasNoModelIssues()
    {
        var system = SystemTests.BuildDivergingSystem();
        var issues = LayoutModelCheck.Check(system.Document);
        if (issues.Count != 0)
            throw new Exception("A valid system must not report model issues: " + string.Join("; ", issues.Select(x => x.Message)));
    }

    public static void InvalidCombinationsAreReported()
    {
        var system = SystemTests.BuildDivergingSystem();
        var document = system.Document;
        system.MainSink.SecondaryParentNodeId = system.Source.Id;      // only converging fittings have two inlets
        system.BranchSink.ConnectionDuctSequence = 999;               // dangling duct reference
        system.Source.SpaceRole = "Lobby";                            // neither source nor sink
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.FanEfficiencyPercent = 120;
        var coil = SystemTests.AddNode(document, LayoutNodeType.Coil, parent: null, duct: null);
        coil.PressureDropPa = -10;
        var issues = LayoutModelCheck.Check(document);
        Expect(system.MainSink.Id, "second inlet");
        Expect(system.BranchSink.Id, "D-999");
        Expect(system.Source.Id, "space role");
        Expect(fan.Id, "efficiency");
        Expect(coil.Id, "pressure drop");
        return;

        void Expect(string itemId, string text)
        {
            if (!issues.Any(x => x.ItemId == itemId && x.Message.Contains(text, StringComparison.OrdinalIgnoreCase)))
                throw new Exception($"Expected a model issue on {itemId} mentioning \"{text}\".");
        }
    }

    public static void ClosedUpstreamLoopIsReported()
    {
        var document = new DuctSystemDocument();
        var a = SystemTests.AddNode(document, LayoutNodeType.Coil, parent: null, duct: null);
        var b = SystemTests.AddNode(document, LayoutNodeType.Filter, a, duct: null);
        a.ParentNodeId = b.Id;
        var issues = LayoutModelCheck.Check(document);
        if (!issues.Any(x => x.ItemId == a.Id && x.Message.Contains("loop", StringComparison.Ordinal)))
            throw new Exception("Components whose upstream connections lead back to themselves must be reported.");
        SystemSolver.Solve(document);                                  // and the solver must still terminate
    }
}
