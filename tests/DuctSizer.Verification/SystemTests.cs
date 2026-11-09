using DuctSizer.Core;

/// <summary>
/// System-level checks for the v0.93 corrections: junction leg sizing, per-outlet junction losses on the
/// critical path, failures that must not count as zero, the portable project file, table-range warnings,
/// transitional-flow consistency and fan sizing inputs.
/// </summary>
internal static class SystemTests
{
    internal static readonly double Temperature = UnitConversions.FahrenheitToCelsius(70);

    /// <summary>
    /// Source -> 16 in duct -> diverging tee (ASHRAE 5-10) -> 14 in main duct -> sink A (1,400 cfm)
    ///                                                   \-> 8 in branch duct -> sink B (600 cfm)
    /// </summary>
    internal sealed record DivergingSystem(
        DuctSystemDocument Document,
        SavedLayoutNode Source,
        SavedLayoutNode Splitter,
        SavedLayoutNode MainSink,
        SavedLayoutNode BranchSink,
        SavedDuct CommonDuct,
        SavedDuct MainDuct,
        SavedDuct BranchDuct,
        SavedFitting Fitting);

    internal static DivergingSystem BuildDivergingSystem()
    {
        var document = new DuctSystemDocument();
        var commonDuct = AddDuct(document, 16, 2_000, 20);
        var mainDuct = AddDuct(document, 14, 1_400, 30);
        var branchDuct = AddDuct(document, 8, 600, 15);

        var reference = GeometryCalculator.Round(UnitConversions.InchesToM(16));
        var fitting = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(),
            Description = "Main tee",
            FittingCode = "5-10",
            AirflowM3PerS = UnitConversions.CfmToM3PerS(2_000),
            Shape = DuctShape.Round,
            DiameterM = reference.DiameterM,
            TemperatureC = Temperature
        };
        document.Fittings.Add(fitting);

        var source = AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        source.SpaceRole = "Space Source";
        source.AirflowM3PerS = UnitConversions.CfmToM3PerS(2_000);
        var splitter = AddNode(document, LayoutNodeType.Fitting, source, commonDuct);
        splitter.FittingGroup = "Diverting Fittings";
        splitter.LinkedFittingSequence = fitting.Sequence;
        var mainSink = AddNode(document, LayoutNodeType.Reservoir, splitter, mainDuct);
        mainSink.SpaceRole = "Space Sink";
        var branchSink = AddNode(document, LayoutNodeType.Reservoir, splitter, branchDuct, branchId: "B-001");
        branchSink.SpaceRole = "Space Sink";

        fitting.Junction = JunctionLegBuilder.ForDiverging(document, splitter, reference, JunctionPath.Straight,
            UnitConversions.CfmToM3PerS(2_000), UnitConversions.CfmToM3PerS(1_400),
            UnitConversions.CfmToM3PerS(600)).Junction;
        return new DivergingSystem(document, source, splitter, mainSink, branchSink,
            commonDuct, mainDuct, branchDuct, fitting);
    }

    internal static SavedDuct AddDuct(DuctSystemDocument document, double diameterInches, double cfm, double lengthFeet)
    {
        var duct = new SavedDuct
        {
            Sequence = document.ReserveSequence(),
            Description = $"{diameterInches} in duct",
            AirflowM3PerS = UnitConversions.CfmToM3PerS(cfm),
            TemperatureC = Temperature,
            Shape = DuctShape.Round,
            DiameterM = UnitConversions.InchesToM(diameterInches),
            MaterialName = "Galvanized Spiral Duct",
            RoughnessFeet = 0.0003,
            EquivalentDiameterMethod = EquivalentDiameterMethod.Huebscher,
            PressureLossBasis = PressureLossBasis.ActualDuctArea,
            LengthM = UnitConversions.FeetToM(lengthFeet)
        };
        document.Ducts.Add(duct);
        return duct;
    }

    internal static SavedLayoutNode AddNode(DuctSystemDocument document, LayoutNodeType type,
        SavedLayoutNode? parent, SavedDuct? duct, string branchId = "Main")
    {
        var node = new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(),
            Type = type,
            BranchId = branchId,
            ParentNodeId = parent?.Id ?? string.Empty,
            ConnectionDuctSequence = duct?.Sequence,
            TemperatureC = Temperature
        };
        document.LayoutNodes.Add(node);
        return node;
    }

    public static void JunctionLegsUseConnectedDuctSizes()
    {
        var system = BuildDivergingSystem();
        var junction = system.Fitting.Junction ?? throw new Exception("The junction was not built.");
        AssertNear(UnitConversions.MToInches(junction.CommonGeometry.DiameterM), 16, 1e-9);
        AssertNear(UnitConversions.MToInches(junction.StraightGeometry.DiameterM), 14, 1e-9);
        AssertNear(UnitConversions.MToInches(junction.BranchGeometry.DiameterM), 8, 1e-9);
        AssertNear(junction.DerivedInputs()["abac"], 0.25, 1e-12);

        system.BranchDuct.DiameterM = 0;
        var provisional = JunctionLegBuilder.ForDiverging(system.Document, system.Splitter,
            GeometryCalculator.Round(UnitConversions.InchesToM(16)), JunctionPath.Straight,
            junction.CommonAirflowM3PerS, junction.StraightAirflowM3PerS, junction.BranchAirflowM3PerS);
        if (!provisional.AssumedLegs.SequenceEqual(new[] { "branch" }))
            throw new Exception("An unsized branch duct must be reported as an assumed leg.");
    }

    public static void CriticalPathChargesEachOutletItsOwnCoefficient()
    {
        var system = BuildDivergingSystem();
        var straight = system.Fitting.CalculateForPath(JunctionPath.Straight);
        var branch = system.Fitting.CalculateForPath(JunctionPath.Branch);
        if (Math.Abs(straight.Coefficient - branch.Coefficient) < 1e-6)
            throw new Exception("The test needs different branch and straight coefficients to be meaningful.");

        var common = system.CommonDuct.Calculate().TotalPressureLossUpperPa;
        var mainRoute = common + straight.PressureLossPa + system.MainDuct.Calculate().TotalPressureLossUpperPa;
        var branchRoute = common + branch.PressureLossPa + system.BranchDuct.Calculate().TotalPressureLossUpperPa;

        var result = CriticalPathAnalyzer.Analyze(system.Document);
        if (!result.IsComplete)
            throw new Exception("A fully sized system must give a complete critical path.");
        AssertNear(result.PressureLossPa, Math.Max(mainRoute, branchRoute), 1e-9);
        var expectedEnd = branchRoute > mainRoute ? system.BranchSink.Id : system.MainSink.Id;
        if (result.EndId != expectedEnd)
            throw new Exception($"The critical route should end at {expectedEnd}, not {result.EndId}.");
    }

    public static void FailedItemBlocksCriticalPathTotal()
    {
        var system = BuildDivergingSystem();
        system.BranchDuct.DiameterM = 0;
        var result = CriticalPathAnalyzer.Analyze(system.Document);
        if (result.IsComplete || !result.Issues.Any(x => x.ItemId == system.BranchDuct.Id))
            throw new Exception("A duct that cannot be calculated must make the critical path incomplete and be named.");

        var dangling = BuildDivergingSystem();
        dangling.BranchSink.ConnectionDuctSequence = 999;
        var missingDuct = CriticalPathAnalyzer.Analyze(dangling.Document);
        if (missingDuct.IsComplete || !missingDuct.Issues.Any(x => x.ItemId == dangling.BranchSink.Id))
            throw new Exception("A connection that refers to a missing duct record must make the critical path incomplete.");
    }

    public static void UnsizedConnectionBlocksTheTotal()
    {
        // Owner decision (2026-11-09): a drawn connection with no sized duct has an unknown loss. It must block the
        // critical-path total and be named; it must never be counted as zero loss.
        var system = BuildDivergingSystem();
        system.BranchSink.ConnectionDuctSequence = null;
        var result = CriticalPathAnalyzer.Analyze(system.Document);
        if (result.IsComplete)
            throw new Exception("An unsized connection must make the critical path incomplete.");
        if (!result.Issues.Any(x => x.ItemId == system.BranchSink.Id && x.Message.Contains("no sized duct", StringComparison.Ordinal)))
            throw new Exception("The unsized connection must be named in the critical-path issues.");
    }

    public static void ProjectFileRoundTripsTheFullModel()
    {
        var system = BuildDivergingSystem();
        var document = system.Document;
        document.ProjectName = "Round trip";
        document.LayoutPdfBackground = new LayoutPdfBackground
        {
            SourceFileName = "M-101.pdf",
            PagePngBase64 = "iVBORw0KGgo=",
            PaperInches = 0.125,
            DrawingFeet = 1
        };
        document.AutomaticSizing.Duct.TargetFrictionPaPerM = UnitConversions.InchesWaterPer100FeetToPaPerM(0.1);
        system.BranchSink.PressurePa = double.NaN;

        var directory = Path.Combine(Path.GetTempPath(), "fanworks-project-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "Round trip" + ProjectFile.Extension);
        try
        {
            ProjectFile.Save(path, document);
            var loaded = ProjectFile.Load(path);
            if (loaded.LayoutNodes.Count != document.LayoutNodes.Count
                || loaded.Ducts.Count != document.Ducts.Count
                || loaded.Fittings.Count != document.Fittings.Count)
                throw new Exception("The project file must keep every component, duct and fitting.");
            var splitter = loaded.LayoutNodes.Single(x => x.Sequence == system.Splitter.Sequence);
            if (splitter.ParentNodeId != system.Source.Id || splitter.ConnectionDuctSequence != system.CommonDuct.Sequence)
                throw new Exception("The project file must keep connections.");
            if (loaded.Fittings.Single().Junction is null)
                throw new Exception("The project file must keep junction leg data.");
            if (loaded.LayoutPdfBackground?.PagePngBase64 != "iVBORw0KGgo=" || loaded.ProjectName != "Round trip")
                throw new Exception("The project file must keep the PDF background and project settings.");
            AssertNear(loaded.AutomaticSizing.Duct.TargetFrictionPaPerM,
                UnitConversions.InchesWaterPer100FeetToPaPerM(0.1), 1e-12);
            // A calculated "not available" (NaN) pressure must not break saving; it is recalculated after loading.
            if (!double.IsFinite(loaded.LayoutNodes.Single(x => x.Sequence == system.BranchSink.Sequence).PressurePa))
                throw new Exception("Loaded pressures must be finite until the system is recalculated.");

            var missing = false;
            try { ProjectFile.Load(Path.Combine(directory, "missing" + ProjectFile.Extension)); }
            catch (FileNotFoundException) { missing = true; }
            if (!missing)
                throw new Exception("Opening a missing project file must fail, not return an empty project.");

            // A project from a newer FanWorks must be refused with a clear message, not half-read.
            var newerPath = Path.Combine(directory, "newer" + ProjectFile.Extension);
            File.WriteAllText(newerPath, File.ReadAllText(path)
                .Replace("\"SchemaVersion\": 12", "\"SchemaVersion\": 99", StringComparison.Ordinal));
            var refused = false;
            try { ProjectFile.Load(newerPath); }
            catch (InvalidDataException ex) when (ex.Message.Contains("newer version", StringComparison.Ordinal))
            { refused = true; }
            if (!refused)
                throw new Exception("A project saved by a newer FanWorks must be refused with an update message.");

            // A second save keeps the previous file as a backup.
            ProjectFile.Save(path, document);
            if (!File.Exists(path + ".bak"))
                throw new Exception("Saving over a project must keep the previous version as a backup.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    public static void OutOfRangeTableLookupsAreFlagged()
    {
        var common = GeometryCalculator.Round(UnitConversions.InchesToM(12));
        var oversizedBranch = GeometryCalculator.Round(UnitConversions.InchesToM(17));
        var result = FittingCalculator.Calculate(FittingCatalog.Find("5-1"), new Dictionary<string, double>(),
            new JunctionFlowData(JunctionPath.Branch, UnitConversions.CfmToM3PerS(1_000), common,
                UnitConversions.CfmToM3PerS(600), common, UnitConversions.CfmToM3PerS(400), oversizedBranch),
            Temperature);
        if (!result.TableRangeWarnings.Any(x => x.Contains("Ab/Ac", StringComparison.Ordinal)))
            throw new Exception("An area ratio beyond the published table must be flagged on the result.");

        var inRange = FittingCalculator.Calculate(FittingCatalog.Find("5-1"), new Dictionary<string, double>(),
            new JunctionFlowData(JunctionPath.Branch, UnitConversions.CfmToM3PerS(1_000), common,
                UnitConversions.CfmToM3PerS(600), common, UnitConversions.CfmToM3PerS(400),
                GeometryCalculator.Round(UnitConversions.InchesToM(12 * Math.Sqrt(.6)))),
            Temperature);
        if (inRange.TableRangeWarnings.Count != 0)
            throw new Exception("An in-range junction must not carry table-range warnings.");

        // Every automated non-junction fitting at its default inputs should stay inside its own tables.
        var noisy = new List<string>();
        foreach (var definition in FittingCatalog.All.Where(x => x.IsAutomated && !x.IsJunction
                     && x.Category is not ("GRDs" or "VAVs")))
        {
            var defaults = definition.Inputs.ToDictionary(x => x.Key, x => x.DefaultValue);
            try
            {
                var calculated = FittingCalculator.Calculate(definition, defaults, 5.0, Temperature);
                if (calculated.TableRangeWarnings.Count > 0)
                    noisy.Add($"{definition.Code}: {calculated.TableRangeWarnings[0]}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Defaults that need geometry-derived inputs are covered by the fitting tests.
            }
        }
        if (noisy.Count > 0)
            throw new Exception("Default inputs should not trigger table-range warnings: " + string.Join(" | ", noisy));
    }

    public static void TransitionalFlowUsesTheUpperBoundEverywhere()
    {
        var result = DuctCalculator.Calculate(new DuctCalculationInput(
            UnitConversions.CfmToM3PerS(10), Temperature, 0.5, 101_325,
            GeometryCalculator.Round(UnitConversions.InchesToM(6)),
            DuctMaterialCatalog.All[1], EquivalentDiameterMethod.Huebscher, PressureLossBasis.ActualDuctArea,
            UnitConversions.FeetToM(10)));
        if (result.Friction.Regime != FlowRegime.Transitional)
            throw new Exception($"The test case should be transitional (Re = {result.ReynoldsNumber:0}).");
        AssertNear(result.PressureLossPaPerM, result.PressureLossUpperPaPerM, 1e-15);
        AssertNear(result.TotalPressureLossPa, result.TotalPressureLossUpperPa, 1e-15);
    }

    public static void FanPowerRejectsInvalidInputs()
    {
        var airflow = UnitConversions.CfmToM3PerS(1_000);
        var pressure = UnitConversions.InchesWaterToPa(1);
        ExpectOutOfRange(() => FanPerformance.BrakeHorsepower(airflow, pressure, 0));
        ExpectOutOfRange(() => FanPerformance.BrakeHorsepower(airflow, pressure, 120));
        ExpectOutOfRange(() => FanPerformance.BrakeHorsepower(airflow, -pressure, 60));
        ExpectOutOfRange(() => FanPerformance.BrakeHorsepower(double.NaN, pressure, 60));
        AssertNear(FanPerformance.BrakeHorsepower(airflow, 0, 60), 0, 1e-15);

        static void ExpectOutOfRange(Action action)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException) { return; }
            throw new Exception("An invalid fan input must raise an error instead of returning a horsepower.");
        }
    }

    public static void FanAirflowComesFromItsOwnSystem()
    {
        var document = new DuctSystemDocument();
        var fanDuct = AddDuct(document, 12, 1_000, 10);
        var fan = AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        var sink = AddNode(document, LayoutNodeType.Reservoir, fan, fanDuct);
        sink.SpaceRole = "Space Sink";
        sink.AirflowM3PerS = UnitConversions.CfmToM3PerS(1_000);

        var otherDuct = AddDuct(document, 30, 5_000, 10);
        var otherSource = AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        otherSource.SpaceRole = "Space Source";
        otherSource.AirflowM3PerS = UnitConversions.CfmToM3PerS(5_000);
        var otherSink = AddNode(document, LayoutNodeType.Reservoir, otherSource, otherDuct);
        otherSink.SpaceRole = "Space Sink";

        AssertNear(UnitConversions.M3PerSToCfm(FanSizing.RequiredAirflowM3PerS(document, fan)), 1_000, 1e-9);
    }

    private static void AssertNear(double actual, double expected, double tolerance)
    {
        if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception($"Expected {expected:R}; received {actual:R}; tolerance {tolerance:R}.");
    }
}
