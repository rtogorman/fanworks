using DuctSizer.Core;

/// <summary>
/// Checks for the corrections made after the 2026-11-12 engineering review of the report (round X, v0.105). Expected
/// values are worked by hand in the comments, independently of the code under test.
/// </summary>
internal static class ReviewCorrectionTests
{
    /// <summary>
    /// Review P0-03 / P1-05: a fitting drawn in the system takes its reference size from the connected duct, and a
    /// transition its area ratio Ao/A1 from its upstream (o) and downstream (1) ducts, unless entered by hand.
    /// </summary>
    public static void FittingSizeFollowsConnectedDucts()
    {
        // Fan -> 11 in x 20 ft (500 cfm) -> transition 4-1 -> 8.5 in x 20 ft -> elbow 3-1 -> 8.5 in x 10 ft -> space.
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(500);
        fan.AirflowUserSpecified = true;
        var upstream = SystemTests.AddDuct(document, 11, 500, 20);
        var transitionNode = SystemTests.AddNode(document, LayoutNodeType.Fitting, fan, upstream);
        transitionNode.FittingGroup = "Transitions";
        var transition = AddFitting(document, transitionNode, "4-1", new() { ["area"] = 1, ["angle"] = 30 });
        var downstream = SystemTests.AddDuct(document, 8.5, 500, 20);
        var elbowNode = SystemTests.AddNode(document, LayoutNodeType.Fitting, transitionNode, downstream);
        elbowNode.FittingGroup = "Elbows";
        var elbow = AddFitting(document, elbowNode, "3-1", new() { ["rd"] = 1.5, ["angle"] = 90 });
        var last = SystemTests.AddDuct(document, 8.5, 500, 10);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, elbowNode, last);
        sink.SpaceRole = "Space Sink";
        SystemSolver.Solve(document);

        // Hand check: Ao/A1 = (11 / 8.5)^2 = 1.674740; reference section o = the 11 in upstream duct.
        Near(transition.Inputs["area"], Math.Pow(11.0 / 8.5, 2), 1e-12, "transition area ratio Ao/A1");
        Near(UnitConversions.MToInches(transition.DiameterM), 11, 1e-9, "transition reference = upstream duct");
        Near(UnitConversions.MToInches(elbow.DiameterM), 8.5, 1e-9, "elbow reference = upstream duct");
        Require(transition.ReferenceSizeSource == upstream.Id && elbow.ReferenceSizeSource == downstream.Id,
            "The fitting records which duct its size came from.");
        // 500 cfm in 8.5 in round: A = π(8.5/12)²/4 = 0.394068 ft²; V = 1268.8 fpm (the elbow's reference velocity).
        Near(UnitConversions.MpsToFpm(elbow.Calculate().ReferenceVelocityMPerS), 500 / (Math.PI * Math.Pow(8.5 / 12, 2) / 4), 1e-6,
            "elbow reference velocity from the connected 8.5 in duct");
        Require(transition.Calculate().PressureLossPa > 0, "A contraction from 11 in to 8.5 in has a loss (not C = 0).");
        Require(transition.Id == transitionNode.Id && elbow.Id == elbowNode.Id,
            "A linked fitting is shown by its layout component's ID (review P1-07).");

        // A size entered by hand is kept.
        elbow.ReferenceSizeManual = true;
        elbow.DiameterM = UnitConversions.InchesToM(12);
        SystemSolver.Solve(document);
        Near(UnitConversions.MToInches(elbow.DiameterM), 12, 1e-9, "manual reference size is kept");
        Require(elbow.ReferenceSizeSource is null, "A manual size has no duct source.");
    }

    /// <summary>
    /// Review P0-01: total pressure only falls across passive items; static = total − velocity pressure, so static
    /// pressure falls through a contraction and is regained in an expansion.
    /// </summary>
    public static void StaticPressureIsTotalLessVelocityPressure()
    {
        // Fan -> 14 in x 10 ft (1,000 cfm) -> transition -> 10 in x 10 ft -> transition -> 16 in x 10 ft -> space.
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(1000);
        fan.AirflowUserSpecified = true;
        var d14 = SystemTests.AddDuct(document, 14, 1000, 10);
        var contraction = SystemTests.AddNode(document, LayoutNodeType.Fitting, fan, d14);
        contraction.FittingGroup = "Transitions";
        AddFitting(document, contraction, "4-1", new() { ["area"] = 1, ["angle"] = 30 });
        var d10 = SystemTests.AddDuct(document, 10, 1000, 10);
        var expansion = SystemTests.AddNode(document, LayoutNodeType.Fitting, contraction, d10);
        expansion.FittingGroup = "Transitions";
        AddFitting(document, expansion, "4-1", new() { ["area"] = 1, ["angle"] = 30 });
        var d16 = SystemTests.AddDuct(document, 16, 1000, 10);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, expansion, d16);
        sink.SpaceRole = "Space Sink";
        SystemSolver.Solve(document);
        var profile = PathPressureProfiles.Build(document).Single();
        var stations = profile.Stations;

        // Fan, 14 in duct, contraction, 10 in duct, expansion, 16 in duct, space (+ start).
        Require(stations.Count == 8 && profile.IsComplete, "Seven steps plus the start station.");
        for (var index = 2; index < stations.Count; index++)
            Require(stations[index].TotalPa <= stations[index - 1].TotalPa + 1e-12,
                "Total pressure never rises across a passive item.");
        foreach (var station in stations)
            Near(station.StaticPa, station.TotalPa - station.VelocityPa, 1e-12, "Ps = Pt − Pv");
        Near(stations[3].VelocityPa, SolverTests.VelocityPressure(d10), 1e-12, "after the contraction: 10 in section");
        Near(stations[5].VelocityPa, SolverTests.VelocityPressure(d16), 1e-12, "after the expansion: 16 in section");
        // Contraction: Pv rises from 14 in to 10 in (by (14/10)^4 = 3.84 times), so static falls by more than the loss.
        Require(stations[3].StaticPa < stations[2].StaticPa - (stations[2].TotalPa - stations[3].TotalPa),
            "Static pressure falls through a contraction by more than its loss.");
        // Expansion: Pv falls to (10/16)^4 = 0.153 of the 10 in value; the regain exceeds the expansion loss here.
        Require(stations[5].StaticPa > stations[4].StaticPa, "Static pressure is regained in the expansion.");
        // In the space the air is still: static = total; the bare duct's exit costs one velocity pressure.
        Near(stations[^1].VelocityPa, 0, 0, "still air in the space");
        Near(stations[^2].TotalPa - stations[^1].TotalPa, SolverTests.VelocityPressure(d16), 1e-12, "exit loss = Pv of the 16 in duct");
    }

    /// <summary>Review P1-09: exit loss into a space, worked by hand; none when a GRD delivers the air.</summary>
    public static void ExitLossIsOneVelocityPressureOfABareDuct()
    {
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(800);
        fan.AirflowUserSpecified = true;
        var duct = SystemTests.AddDuct(document, 10, 800, 25);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, fan, duct);
        sink.SpaceRole = "Space Sink";
        SystemSolver.Solve(document);
        // 800 cfm in 10 in round: A = π(10/12)²/4 = 0.545415 ft²; V = 1466.8 fpm; Pv = (V/4005)² (ρ/0.075) in. w.c.
        var result = duct.Calculate();
        var densityLbFt3 = result.Air.DensityKgPerM3 * 0.0624279606;
        var velocityFpm = 800 / (Math.PI * Math.Pow(10.0 / 12, 2) / 4);
        var handPvInWc = Math.Pow(velocityFpm / 4005, 2) * densityLbFt3 / 0.075;
        Near(UnitConversions.PaToInchesWater(SystemPressures.ExitLossPa(document, sink)), handPvInWc, handPvInWc * 0.003,
            "exit loss = one velocity pressure (the 4005 constant is rounded; agrees within 0.3 %)");
        var analysis = CriticalPathAnalyzer.Analyze(document);
        Near(analysis.PressureLossPa, result.TotalPressureLossUpperPa + SolverTests.VelocityPressure(duct), 1e-9,
            "critical path = friction + exit loss");

        // The same space fed through a GRD: the terminal's catalog total pressure includes discharge, no exit loss.
        var withGrd = new DuctSystemDocument();
        var fan2 = SystemTests.AddNode(withGrd, LayoutNodeType.Fan, parent: null, duct: null);
        fan2.AirflowM3PerS = UnitConversions.CfmToM3PerS(800);
        var d1 = SystemTests.AddDuct(withGrd, 10, 800, 25);
        var grd = SystemTests.AddNode(withGrd, LayoutNodeType.Grd, fan2, d1);
        grd.PressureDropPa = UnitConversions.InchesWaterToPa(0.05);
        var d2 = SystemTests.AddDuct(withGrd, 10, 800, 1);
        var room = SystemTests.AddNode(withGrd, LayoutNodeType.Reservoir, grd, d2);
        room.SpaceRole = "Space Sink";
        Require(!SystemPressures.ExitLossApplies(withGrd, room), "No exit loss after an air terminal.");
    }

    /// <summary>Review P1-11: a standalone fitting or duct does not change the system's area or leakage.</summary>
    public static void StandaloneItemsStayOutOfSystemTotals()
    {
        var system = SystemTests.BuildDivergingSystem();
        SystemSolver.Solve(system.Document);
        var before = SmacnaLeakage.SystemAreas(system.Document).Sum(x => x.AreaFt2);
        system.Document.Fittings.Add(new SavedFitting
        {
            Sequence = system.Document.ReserveFittingSequence(), FittingCode = "3-1",
            Inputs = new() { ["rd"] = 1.5, ["angle"] = 90 }, AirflowM3PerS = UnitConversions.CfmToM3PerS(400),
            DiameterM = UnitConversions.InchesToM(12), TemperatureC = 20
        });
        system.Document.Ducts.Add(new SavedDuct
        {
            Sequence = system.Document.ReserveSequence(), AirflowM3PerS = UnitConversions.CfmToM3PerS(400),
            DiameterM = UnitConversions.InchesToM(10), LengthM = UnitConversions.FeetToM(50), TemperatureC = 20,
            MaterialName = "Galvanized Spiral Duct", RoughnessFeet = 0.0003
        });
        var after = SmacnaLeakage.SystemAreas(system.Document).Sum(x => x.AreaFt2);
        Near(after, before, 1e-12, "system area with standalone items added");
        Require(SystemMembership.IsStandalone(system.Document, system.Document.Fittings[^1])
                && !SystemMembership.IsStandalone(system.Document, system.Fitting),
            "The added fitting is standalone; the drawn tee is not.");
    }

    /// <summary>Review P2-18: no airflow or no size is an invalid input, never a velocity carried over from before.</summary>
    public static void FittingWithoutAirflowOrSizeIsInvalid()
    {
        var fitting = new SavedFitting
        {
            Sequence = 1, FittingCode = "3-1", Inputs = new() { ["rd"] = 1.5, ["angle"] = 90 },
            ReferenceVelocityMPerS = UnitConversions.FpmToMps(1273.2), DiameterM = 0, AirflowM3PerS = 0, TemperatureC = 20
        };
        Throws(() => fitting.Calculate(), "a fitting with no size and no airflow");
        fitting.DiameterM = UnitConversions.InchesToM(12);
        Throws(() => fitting.Calculate(), "a fitting with a size but no airflow");
        fitting.AirflowM3PerS = UnitConversions.CfmToM3PerS(1000);
        // 1,000 cfm in 12 in round: V = 1000 / 0.785398 = 1273.24 fpm, now from its own inputs.
        Near(UnitConversions.MpsToFpm(fitting.Calculate().ReferenceVelocityMPerS), 1000 / (Math.PI / 4), 1e-6, "velocity from Q / A");
    }

    /// <summary>Review P1-04: a split must be a junction fitting, or its junction losses are silently zero.</summary>
    public static void SplitsWithoutAJunctionAreReported()
    {
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        var coil = SystemTests.AddNode(document, LayoutNodeType.Coil, fan, SystemTests.AddDuct(document, 14, 1000, 5));
        SystemTests.AddNode(document, LayoutNodeType.Reservoir, coil, SystemTests.AddDuct(document, 10, 500, 5)).SpaceRole = "Space Sink";
        SystemTests.AddNode(document, LayoutNodeType.Reservoir, coil, SystemTests.AddDuct(document, 10, 500, 5), "B-001").SpaceRole = "Space Sink";
        Require(LayoutModelCheck.Check(document).Any(issue => issue.ItemId == coil.Id && issue.Message.Contains("not a tee or wye", StringComparison.Ordinal)),
            "A component feeding two connections is reported.");

        var system = SystemTests.BuildDivergingSystem();
        system.Fitting.FittingCode = "3-1"; // a tee calculated as an elbow
        Require(LayoutModelCheck.Check(system.Document).Any(issue => issue.ItemId == system.Splitter.Id && issue.Message.Contains("not a junction", StringComparison.Ordinal)),
            "A diverging junction linked to a non-junction fitting is reported.");
    }

    private static SavedFitting AddFitting(DuctSystemDocument document, SavedLayoutNode node, string code,
        Dictionary<string, double> inputs)
    {
        var fitting = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(), FittingCode = code, Inputs = inputs,
            DiameterM = UnitConversions.InchesToM(12), TemperatureC = node.TemperatureC
        };
        document.Fittings.Add(fitting);
        node.LinkedFittingSequence = fitting.Sequence;
        node.IsDefined = true;
        return fitting;
    }

    private static void Throws(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return;
        }
        throw new Exception($"{what} should be rejected.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Near(double actual, double expected, double tolerance, string what)
    {
        if (!(Math.Abs(actual - expected) <= tolerance))
            throw new Exception($"{what}: expected {expected}, got {actual}.");
    }
}
