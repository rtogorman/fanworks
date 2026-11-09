using DuctSizer.Core;

/// <summary>
/// Checks for the Layout Designer system solver (<see cref="SystemSolver"/>), moved out of the main window in
/// step 7 of the correction plan. Expected values are built independently from the individual item calculations.
/// </summary>
internal static class SolverTests
{
    public static void DivergingJunctionConservesAirflow()
    {
        var system = SystemTests.BuildDivergingSystem();
        SystemSolver.Solve(system.Document);
        var total = UnitConversions.CfmToM3PerS(2_000);
        AssertNear(system.Splitter.AirflowM3PerS, total, 1e-12);
        AssertNear(system.MainDuct.AirflowM3PerS + system.BranchDuct.AirflowM3PerS, total, 1e-12);
        // Unlocked outlets split in proportion to the junction's stored straight and branch flows (1,400 / 600).
        AssertNear(system.MainDuct.AirflowM3PerS, UnitConversions.CfmToM3PerS(1_400), 1e-12);
        AssertNear(system.BranchDuct.AirflowM3PerS, UnitConversions.CfmToM3PerS(600), 1e-12);
        AssertNear(system.MainSink.AirflowM3PerS, system.MainDuct.AirflowM3PerS, 1e-12);
        AssertNear(system.BranchSink.AirflowM3PerS, system.BranchDuct.AirflowM3PerS, 1e-12);
    }

    public static void PressureFollowsEachJunctionPath()
    {
        var system = SystemTests.BuildDivergingSystem();
        var solution = SystemSolver.Solve(system.Document);
        var common = system.CommonDuct.Calculate().TotalPressureLossUpperPa;
        var main = system.MainDuct.Calculate().TotalPressureLossUpperPa;
        var branch = system.BranchDuct.Calculate().TotalPressureLossUpperPa;
        var straightPath = system.Fitting.CalculateForPath(JunctionPath.Straight).PressureLossPa;
        var branchPath = system.Fitting.CalculateForPath(JunctionPath.Branch).PressureLossPa;
        if (Math.Abs(straightPath - branchPath) < 1e-6)
            throw new Exception("Test system must have different straight and branch junction losses.");

        var sourcePressure = system.Source.PressurePa; // a Space Source keeps its entered pressure
        if (!solution.TryGetInletPressure(system.Splitter.Id, out var splitterInlet))
            throw new Exception("The solver must report the junction's inlet pressure.");
        AssertNear(splitterInlet, sourcePressure - common, 1e-9);
        AssertNear(system.Splitter.PressurePa, splitterInlet - straightPath, 1e-9);
        AssertNear(system.MainSink.PressurePa, splitterInlet - straightPath - main, 1e-9);
        AssertNear(system.BranchSink.PressurePa, splitterInlet - branchPath - branch, 1e-9);
    }

    public static void FanAddsStaticPressureAndLossesCloseToZero()
    {
        var document = new DuctSystemDocument();
        var duct = SystemTests.AddDuct(document, 12, 800, 40);
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(800);
        fan.AirflowUserSpecified = true;
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, fan, duct);
        sink.SpaceRole = "Space Sink";
        SystemSolver.Solve(document);
        fan.FanStaticPressurePa = duct.Calculate().TotalPressureLossUpperPa;
        SystemSolver.Solve(document);
        AssertNear(fan.PressurePa, fan.FanStaticPressurePa, 1e-12);
        AssertNear(sink.PressurePa, 0.0, 1e-9);
    }

    public static void LockedOutletOverflowIsFlagged()
    {
        var balanced = SystemTests.BuildDivergingSystem();
        balanced.MainSink.ConnectionAirflowUserSpecified = true;
        balanced.MainDuct.AirflowM3PerS = UnitConversions.CfmToM3PerS(1_900);
        SystemSolver.Solve(balanced.Document);
        AssertNear(balanced.BranchDuct.AirflowM3PerS, UnitConversions.CfmToM3PerS(100), 1e-12);
        if (balanced.MainSink.ConnectionAirflowOverride)
            throw new Exception("A locked outlet that still balances with the inlet must not be flagged.");

        var overflowing = SystemTests.BuildDivergingSystem();
        overflowing.MainSink.ConnectionAirflowUserSpecified = true;
        overflowing.MainDuct.AirflowM3PerS = UnitConversions.CfmToM3PerS(2_100);
        SystemSolver.Solve(overflowing.Document);
        AssertNear(overflowing.BranchDuct.AirflowM3PerS, 0.0, 1e-12);
        if (!overflowing.MainSink.ConnectionAirflowOverride)
            throw new Exception("A locked outlet carrying more than the inlet airflow must be flagged as an override.");
    }

    public static void FailedDuctMakesDownstreamPressureNotAvailable()
    {
        var system = SystemTests.BuildDivergingSystem();
        system.BranchDuct.DiameterM = 0;
        SystemSolver.Solve(system.Document);
        if (!double.IsNaN(system.BranchSink.PressurePa))
            throw new Exception("Pressure downstream of a duct that cannot be calculated must be NaN, not a number.");
        if (!double.IsFinite(system.MainSink.PressurePa))
            throw new Exception("A failure on one branch must not affect the other branch.");
    }

    public static void UnsizedConnectionIsCloseCoupled()
    {
        var system = SystemTests.BuildDivergingSystem();
        system.MainSink.ConnectionDuctSequence = null;
        var solution = SystemSolver.Solve(system.Document);
        solution.TryGetInletPressure(system.Splitter.Id, out var splitterInlet);
        var straightPath = system.Fitting.CalculateForPath(JunctionPath.Straight).PressureLossPa;
        AssertNear(system.MainSink.PressurePa, splitterInlet - straightPath, 1e-9);
    }

    public static void ConvergingJunctionSumsItsInlets()
    {
        var document = new DuctSystemDocument();
        var ductA = SystemTests.AddDuct(document, 10, 500, 10);
        var ductB = SystemTests.AddDuct(document, 8, 300, 10);
        var sourceA = SystemTests.AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        sourceA.SpaceRole = "Space Source";
        sourceA.AirflowM3PerS = UnitConversions.CfmToM3PerS(500);
        var sourceB = SystemTests.AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        sourceB.SpaceRole = "Space Source";
        sourceB.AirflowM3PerS = UnitConversions.CfmToM3PerS(300);
        sourceB.PressurePa = -5;
        var merger = SystemTests.AddNode(document, LayoutNodeType.Fitting, sourceA, ductA);
        merger.FittingGroup = "Converging Fittings";
        merger.SecondaryParentNodeId = sourceB.Id;
        merger.SecondaryConnectionDuctSequence = ductB.Sequence;
        SystemSolver.Solve(document);
        AssertNear(merger.AirflowM3PerS, UnitConversions.CfmToM3PerS(800), 1e-12);
        var viaA = sourceA.PressurePa - ductA.Calculate().TotalPressureLossUpperPa;
        var viaB = sourceB.PressurePa - ductB.Calculate().TotalPressureLossUpperPa;
        AssertNear(merger.PressurePa, Math.Min(viaA, viaB), 1e-9);
    }

    public static void SolvingTwiceGivesTheSameResult()
    {
        // Same inputs give bit-identical results (the reference-project comparison checks that). A second solve
        // starts from the first solve's stored outlet flows, which the diverging split uses as its weights, so
        // the last floating-point digit may move; anything beyond rounding would be a defect.
        var system = SystemTests.BuildDivergingSystem();
        SystemSolver.Solve(system.Document);
        var first = system.Document.LayoutNodes.Select(x => (x.PressurePa, x.AirflowM3PerS)).ToArray();
        SystemSolver.Solve(system.Document);
        var second = system.Document.LayoutNodes.Select(x => (x.PressurePa, x.AirflowM3PerS)).ToArray();
        for (var index = 0; index < first.Length; index++)
        {
            AssertNear(second[index].PressurePa, first[index].PressurePa, 1e-9);
            AssertNear(second[index].AirflowM3PerS, first[index].AirflowM3PerS, 1e-12);
        }

        var again = SystemTests.BuildDivergingSystem();
        SystemSolver.Solve(again.Document);
        var repeated = again.Document.LayoutNodes.Select(x => (x.PressurePa, x.AirflowM3PerS)).ToArray();
        if (!first.SequenceEqual(repeated))
            throw new Exception("Solving identical inputs must reproduce bit-identical results.");
    }

    private static void AssertNear(double actual, double expected, double tolerance)
    {
        if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception($"Expected {expected:R}, got {actual:R} (tolerance {tolerance:R}).");
    }
}
