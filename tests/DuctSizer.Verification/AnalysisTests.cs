using DuctSizer.Core;

/// <summary>
/// Checks for round Q: expected ranges (Analysis Settings, schema 16) and the per-route pressure profiles behind the
/// Analysis graph and the report title page.
/// </summary>
internal static class AnalysisTests
{
    /// <summary>Hand-checked: values on, inside and outside each bound; one-sided ranges; no ranges = not set.</summary>
    public static void RangesCompareInclusiveAndOneSided()
    {
        var range = new ExpectedRange { Low = 2.0, High = 5.0 };
        Require(range.Contains(2.0) && range.Contains(5.0) && range.Contains(3.3), "Bounds are inclusive.");
        Require(!range.Contains(1.999) && !range.Contains(5.001), "Values beyond a bound are outside.");
        Require(!range.Contains(double.NaN), "A value that is not a number is never inside a range.");
        Require(new ExpectedRange { High = 5 }.Contains(0) && !new ExpectedRange { High = 5 }.Contains(6),
            "A range with only a high value checks only the high value.");
        Require(new ExpectedRange { Low = 5 }.Contains(1e6) && !new ExpectedRange { Low = 5 }.Contains(4),
            "A range with only a low value checks only the low value.");
        // A bound entered in imperial units and converted to SI still contains the same value converted the same way.
        var rate = UnitConversions.InchesWaterPer100FeetToPaPerM(0.1);
        Require(new ExpectedRange { High = rate }.Contains(UnitConversions.InchesWaterPer100FeetToPaPerM(0.1)),
            "A value equal to a converted bound is inside the range.");
        Require(new ExpectedRange { Low = 5, High = 2 }.Problem is not null, "A low value above the high value is reported.");
        Require(new ExpectedRange { Low = -1 }.Problem is not null, "A negative bound is reported.");
        Require(!new ExpectedRange().IsSet && new AnalysisExpectations().AnySet == false,
            "There are no default ranges.");
    }

    /// <summary>A duct and a fitting from the reference system checked against ranges either side of their values.</summary>
    public static void DuctsAndFittingsAreCheckedAgainstTheirOwnRanges()
    {
        var system = SystemTests.BuildDivergingSystem();
        var duct = system.CommonDuct.Calculate();
        var fitting = system.Fitting.Calculate();
        var expectations = new AnalysisExpectations();

        Require(AnalysisRangeCheck.CheckDuct(expectations, duct).Status == RangeCheckStatus.NotSet,
            "With no ranges entered, a duct is 'not set', never 'within'.");
        expectations.FittingLossPa = new ExpectedRange { High = fitting.PressureLossPa * 2 };
        Require(AnalysisRangeCheck.CheckDuct(expectations, duct).Status == RangeCheckStatus.NotSet,
            "A fitting range does not apply to a duct.");
        Require(AnalysisRangeCheck.CheckFitting(expectations, fitting).Status == RangeCheckStatus.Within,
            "A fitting below its high loss value is within range.");

        expectations.DuctVelocityMPerS = new ExpectedRange { Low = duct.ActualVelocityMPerS * 0.9, High = duct.ActualVelocityMPerS * 1.1 };
        expectations.DuctPressureRatePaPerM = new ExpectedRange { Low = duct.PressureLossPaPerM * 1.5 };
        var outside = AnalysisRangeCheck.CheckDuct(expectations, duct);
        Require(outside.Status == RangeCheckStatus.Outside && outside.Findings.Count == 2
                && outside.Findings.Single(x => x.Quantity == ExpectedQuantity.DuctPressureRate).Within == false
                && outside.Findings.Single(x => x.Quantity == ExpectedQuantity.DuctVelocity).Within,
            "A duct whose friction rate is below the low value is outside the range, even with velocity inside.");

        expectations.DuctPressureRatePaPerM = new ExpectedRange { Low = duct.PressureLossPaPerM * 0.5, High = duct.PressureLossPaPerM * 2 };
        Require(AnalysisRangeCheck.CheckDuct(expectations, duct).Status == RangeCheckStatus.Within,
            "A duct inside both ranges is within range.");

        expectations.FittingVelocityMPerS = new ExpectedRange { High = fitting.ReferenceVelocityMPerS * 0.5 };
        Require(AnalysisRangeCheck.CheckFitting(expectations, fitting).Status == RangeCheckStatus.Outside,
            "A fitting faster than the high velocity is outside the range.");

        expectations.FittingVelocityMPerS = new ExpectedRange { Low = 10, High = 1 };
        var invalid = AnalysisRangeCheck.CheckFitting(expectations, fitting);
        Require(invalid.Status == RangeCheckStatus.NotAvailable && invalid.Problem is not null,
            "An invalid range is reported, not treated as passing.");

        system.CommonDuct.DiameterM = 0;
        Require(AnalysisRangeCheck.CheckDuct(expectations, system.CommonDuct).Status == RangeCheckStatus.NotAvailable,
            "A duct that cannot be calculated is not available, never within range.");
    }

    /// <summary>A transitional duct is within range only when its whole friction band is.</summary>
    public static void TransitionalDuctUsesBothBounds()
    {
        var document = new DuctSystemDocument();
        // A small, slow duct whose Reynolds number falls in the transitional band.
        var duct = SystemTests.AddDuct(document, 4, 6, 10);
        var result = duct.Calculate();
        Require(result.Friction.Regime == FlowRegime.Transitional,
            $"The test duct must be in transitional flow (Re = {result.ReynoldsNumber:0}).");
        Require(result.PressureLossUpperPaPerM > result.PressureLossLowerPaPerM, "The transitional band has width.");
        var midpoint = (result.PressureLossLowerPaPerM + result.PressureLossUpperPaPerM) / 2;
        var expectations = new AnalysisExpectations { DuctPressureRatePaPerM = new ExpectedRange { High = midpoint } };
        var check = AnalysisRangeCheck.CheckDuct(expectations, result);
        Require(check.Status == RangeCheckStatus.Outside && check.Findings[0].UpperValue is not null,
            "A high value inside the transitional band leaves the duct outside the range.");
        expectations.DuctPressureRatePaPerM.High = result.PressureLossUpperPaPerM;
        Require(AnalysisRangeCheck.CheckDuct(expectations, result).Status == RangeCheckStatus.Within,
            "A range covering the whole band is within range.");
    }

    public static void VavRangesCheckInletPressureAndDrop()
    {
        // Titus DESV with reheat, size 8, 1 row, 850 cfm: published drop 0.045 in. w.c. (verified elsewhere).
        var vav = VavPerformanceCatalog.CalculateFittingResult(
            FittingCatalog.Find(VavPerformanceCatalog.WithReheatCode),
            new Dictionary<string, double> { ["sizeIndex"] = 8, ["rows"] = 1 },
            UnitConversions.CfmToM3PerS(850), UnitConversions.FahrenheitToCelsius(70));
        var expectations = new AnalysisExpectations
        {
            VavPressureDropPa = new ExpectedRange { Low = UnitConversions.InchesWaterToPa(0.03), High = UnitConversions.InchesWaterToPa(0.05) },
            VavInletPressurePa = new ExpectedRange { Low = UnitConversions.InchesWaterToPa(0.5), High = UnitConversions.InchesWaterToPa(1.5) }
        };
        var inside = AnalysisRangeCheck.CheckFitting(expectations, vav, UnitConversions.InchesWaterToPa(0.8));
        Require(inside.Status == RangeCheckStatus.Within && inside.Findings.Count == 2
                && inside.Findings.Any(x => x.Quantity == ExpectedQuantity.VavPressureDrop)
                && inside.Findings.Any(x => x.Quantity == ExpectedQuantity.VavInletPressure),
            "A VAV with 0.045 in. w.c. drop and 0.8 in. w.c. inlet pressure is inside 0.03-0.05 and 0.5-1.5.");
        var lowInlet = AnalysisRangeCheck.CheckFitting(expectations, vav, UnitConversions.InchesWaterToPa(0.3));
        Require(lowInlet.Status == RangeCheckStatus.Outside
                && !lowInlet.Findings.Single(x => x.Quantity == ExpectedQuantity.VavInletPressure).Within
                && lowInlet.Findings.Single(x => x.Quantity == ExpectedQuantity.VavPressureDrop).Within,
            "A VAV inlet pressure below the low value is outside the range.");
        Require(AnalysisRangeCheck.CheckFitting(expectations, vav, null).Status == RangeCheckStatus.NotAvailable,
            "An unknown inlet pressure cannot be checked (not available, never 'within').");
        // VAV ranges do not apply to other fittings.
        var elbow = FittingCalculator.Calculate(FittingCatalog.Find("3-1"),
            new Dictionary<string, double> { ["rd"] = 1.5, ["angle"] = 90 }, UnitConversions.CfmToM3PerS(800),
            GeometryCalculator.Round(UnitConversions.InchesToM(12)), UnitConversions.FahrenheitToCelsius(70));
        Require(AnalysisRangeCheck.CheckFitting(expectations, elbow, 0).Status == RangeCheckStatus.NotSet,
            "VAV ranges apply only to VAV boxes.");
        var copy = expectations.Copy();
        Require(copy.VavInletPressurePa.High == expectations.VavInletPressurePa.High && copy.AnySet,
            "The VAV ranges are copied and count as set.");
    }

    public static void ExpectationsRoundTripAndOlderFilesOpenEmpty()
    {
        var document = new DuctSystemDocument();
        SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        document.AnalysisExpectations.DuctPressureRatePaPerM = new ExpectedRange
        {
            Low = UnitConversions.InchesWaterPer100FeetToPaPerM(0.05),
            High = UnitConversions.InchesWaterPer100FeetToPaPerM(0.1)
        };
        document.AnalysisExpectations.FittingVelocityMPerS = new ExpectedRange { High = UnitConversions.FpmToMps(1500) };
        var path = Path.Combine(Path.GetTempPath(), $"fanworks-ranges-{Guid.NewGuid():N}.json");
        try
        {
            DuctSystemStore.Save(path, document);
            var loaded = DuctSystemStore.Load(path);
            var ranges = loaded.AnalysisExpectations;
            Require(loaded.SchemaVersion == DuctSystemDocument.CurrentSchemaVersion && DuctSystemDocument.CurrentSchemaVersion >= 16,
                "Ranges are saved in file format 16 and later.");
            Require(ranges.DuctPressureRatePaPerM.Low == document.AnalysisExpectations.DuctPressureRatePaPerM.Low
                    && ranges.DuctPressureRatePaPerM.High == document.AnalysisExpectations.DuctPressureRatePaPerM.High
                    && ranges.FittingVelocityMPerS.Low is null
                    && ranges.FittingVelocityMPerS.High == UnitConversions.FpmToMps(1500)
                    && !ranges.DuctVelocityMPerS.IsSet && !ranges.FittingLossPa.IsSet,
                "Every entered bound must round-trip unchanged, and empty bounds stay empty.");

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            json["SchemaVersion"] = 15;
            json.Remove("AnalysisExpectations");
            File.WriteAllText(path, json.ToJsonString());
            var older = DuctSystemStore.Load(path);
            Require(older.SchemaVersion == DuctSystemDocument.CurrentSchemaVersion && older.AnalysisExpectations is { AnySet: false }
                    && older.AnalysisExpectations.FittingLossPa is not null && older.LayoutNodes.Count == 1,
                "A format-15 project must open in the current format with no ranges and nothing else changed.");

            json["SchemaVersion"] = DuctSystemDocument.CurrentSchemaVersion;
            json["AnalysisExpectations"] = System.Text.Json.Nodes.JsonNode.Parse("{\"DuctVelocityMPerS\":null}");
            File.WriteAllText(path, json.ToJsonString());
            Require(DuctSystemStore.Load(path).AnalysisExpectations.DuctVelocityMPerS is { IsSet: false },
                "A missing range in a hand-edited file opens as an empty range.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The diverging reference system has two routes; their totals are hand-summed and the larger is critical.</summary>
    public static void RouteProfilesMatchTheCriticalPath()
    {
        var system = SystemTests.BuildDivergingSystem();
        var profiles = SystemPathProfiles.Build(system.Document);
        Require(profiles.Paths.Count == 2 && !profiles.Truncated && profiles.CompletePathCount == 2,
            "The diverging system has two complete routes.");

        var common = system.CommonDuct.Calculate().TotalPressureLossUpperPa;
        var mainRoute = common + system.Fitting.CalculateForPath(JunctionPath.Straight).PressureLossPa
                        + system.MainDuct.Calculate().TotalPressureLossUpperPa;
        var branchRoute = common + system.Fitting.CalculateForPath(JunctionPath.Branch).PressureLossPa
                          + system.BranchDuct.Calculate().TotalPressureLossUpperPa;
        var main = profiles.Paths.Single(x => x.EndId == system.MainSink.Id);
        var branch = profiles.Paths.Single(x => x.EndId == system.BranchSink.Id);
        Near(main.TotalLossPa, mainRoute, "main route total");
        Near(branch.TotalLossPa, branchRoute, "branch route total");

        // Source, common duct, tee, main duct, sink: five items in flow order, running total never decreasing.
        Require(main.Steps.Select(x => x.ItemId).SequenceEqual(new[]
                {
                    system.Source.Id, system.CommonDuct.Id, system.Fitting.Id, system.MainDuct.Id, system.MainSink.Id
                }),
            "A route lists its items in flow order: " + string.Join(", ", main.Steps.Select(x => x.ItemId)));
        Require(main.Steps.Zip(main.Steps.Skip(1)).All(pair => pair.Second.CumulativeLossPa >= pair.First.CumulativeLossPa),
            "The running total along a route never decreases.");
        Near(main.Steps[1].CumulativeLossPa, common, "running total after the common duct");

        var analysis = CriticalPathAnalyzer.Analyze(system.Document);
        var critical = profiles.Paths.Single(x => x.IsCritical);
        Near(critical.TotalLossPa, analysis.PressureLossPa, "critical route total versus the critical-path analysis");
        Require(critical.EndId == analysis.EndId, "The critical route ends where the critical-path analysis ends.");
        Require(critical.Steps.Select(x => x.ItemKey).ToHashSet().SetEquals(analysis.ItemKeys),
            "The critical route covers the same items as the critical-path analysis.");
    }

    public static void UnsizedConnectionMakesItsRouteIncomplete()
    {
        var system = SystemTests.BuildDivergingSystem();
        system.BranchSink.ConnectionDuctSequence = null;
        var profiles = SystemPathProfiles.Build(system.Document);
        Require(profiles.Paths.Count == 2 && profiles.CompletePathCount == 1,
            "Only the route through the unsized connection is incomplete.");
        Require(!profiles.Paths.Single(x => x.EndId == system.BranchSink.Id).IsComplete,
            "A route through an unsized connection must not be shown as a result.");
        Require(SystemPathProfiles.Build(new DuctSystemDocument()).Paths.Count == 0, "An empty project has no routes.");
    }

    private static void Near(double actual, double expected, string what)
    {
        if (Math.Abs(actual - expected) > 1e-9 * Math.Max(1, Math.Abs(expected)))
            throw new Exception($"{what}: expected {expected:R}, got {actual:R}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}

/// <summary>Checks for SMACNA pressure class, seal class, surface area and allowable leakage (round Q follow-up).</summary>
internal static class LeakageTests
{
    public static void PressureSealAndLeakageClassesFollowSmacna()
    {
        // Pressure class: smallest class at or above the design pressure, rounded up; none above 10 in. w.c.
        foreach (var (pressure, expected) in new (double, double?)[]
                 {
                     (0.0, 0.5), (0.366, 0.5), (0.5, 0.5), (0.51, 1), (0.993, 1), (1.0, 1), (1.6, 2), (2.0, 2),
                     (2.01, 3), (3.5, 4), (5.2, 6), (10.0, 10), (10.01, null), (-1.2, 2), (double.NaN, null)
                 })
            Require(SmacnaLeakage.PressureClassFor(pressure) == expected,
                $"Pressure {pressure} in. w.c. should give class {expected?.ToString() ?? "none"}, not {SmacnaLeakage.PressureClassFor(pressure)}.");
        Require(new[] { 0.5, 1.0, 2.0 }.All(p => SmacnaLeakage.SealClassFor(p) == SmacnaSealClass.C)
                && SmacnaLeakage.SealClassFor(3) == SmacnaSealClass.B
                && new[] { 4.0, 6.0, 10.0 }.All(p => SmacnaLeakage.SealClassFor(p) == SmacnaSealClass.A),
            "Seal class: C up to 2 in. w.c., B at 3, A at 4 and up.");
        Require(SmacnaLeakage.LeakageClass(SmacnaSealClass.A, DuctShape.Round) == 3
                && SmacnaLeakage.LeakageClass(SmacnaSealClass.A, DuctShape.Rectangular) == 6
                && SmacnaLeakage.LeakageClass(SmacnaSealClass.B, DuctShape.Round) == 6
                && SmacnaLeakage.LeakageClass(SmacnaSealClass.B, DuctShape.Rectangular) == 12
                && SmacnaLeakage.LeakageClass(SmacnaSealClass.C, DuctShape.Round) == 12
                && SmacnaLeakage.LeakageClass(SmacnaSealClass.C, DuctShape.Rectangular) == 24,
            "Leakage classes: A 3/6, B 6/12, C 12/24 (round/rectangular).");
        // Hand-checked: F = 12 × 2^0.65 = 12 × 1.569168 = 18.830 cfm/100 ft²; F = 6 × 1^0.65 = 6.
        Near(SmacnaLeakage.AllowableLeakageFactor(12, 2), 18.830017, 1e-5, "F for C_L = 12 at 2 in. w.c.");
        Near(SmacnaLeakage.AllowableLeakageFactor(6, 1), 6, 1e-12, "F for C_L = 6 at 1 in. w.c.");
    }

    public static void SurfaceAreasOfDuctsAndFittings()
    {
        var document = new DuctSystemDocument();
        var round = SystemTests.AddDuct(document, 12, 800, 20);
        Near(SurfaceAreaCalculator.ForDuct(round).AreaFt2, Math.PI * 1.0 * 20, 1e-9, "12 in round x 20 ft = pi x 1 ft x 20 ft");
        var rectangular = SystemTests.AddDuct(document, 12, 800, 10);
        rectangular.Shape = DuctShape.Rectangular;
        rectangular.WidthM = UnitConversions.InchesToM(24);
        rectangular.HeightM = UnitConversions.InchesToM(12);
        Near(SurfaceAreaCalculator.ForDuct(rectangular).AreaFt2, 2 * (2.0 + 1.0) * 10, 1e-9, "24 x 12 in x 10 ft = 2(2 + 1) x 10");

        var elbow = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(), FittingCode = "3-1", Shape = DuctShape.Round,
            DiameterM = UnitConversions.InchesToM(12), Inputs = new Dictionary<string, double> { ["rd"] = 1.5, ["angle"] = 90 }
        };
        // Centerline arc = 90/360 × 2π × 1.5 ft = 2.3562 ft; A = π × 1 ft × 2.3562 ft = 7.4022 ft².
        var elbowArea = SurfaceAreaCalculator.ForFitting(elbow)!;
        Near(elbowArea.AreaFt2, Math.PI * (0.25 * 2 * Math.PI * 1.5), 1e-9, "round elbow area along its centerline arc");
        Require(elbowArea.IsEstimate, "A fitting area is marked as an estimate.");
        var tee = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(), FittingCode = "5-10", Shape = DuctShape.Rectangular,
            WidthM = UnitConversions.InchesToM(24), HeightM = UnitConversions.InchesToM(12)
        };
        Near(SurfaceAreaCalculator.ForFitting(tee)!.AreaFt2, 2 * (2.0 + 1.0) * 2.0, 1e-9, "other fittings: perimeter x larger side");
        Require(SurfaceAreaCalculator.ForFitting(new SavedFitting { FittingCode = GrdPerformanceCatalog.Titus350RCode }) is null,
            "A GRD has no ductwork surface area (leakage 0).");
        round.LengthM = 0;
        Require(SurfaceAreaCalculator.ForDuct(round).Problem is not null, "A duct without a length has no area and says why.");
    }

    public static void SystemLeakageIsSumOfFactorTimesArea()
    {
        var system = SystemTests.BuildDivergingSystem();
        var result = SmacnaLeakage.Analyze(system.Document);
        var critical = CriticalPathAnalyzer.Analyze(system.Document);
        var criticalInWc = UnitConversions.PaToInchesWater(critical.PressureLossPa);
        Require(result.IsAvailable && result.PressureClassInWc == SmacnaLeakage.PressureClassFor(criticalInWc)
                && result.SealClass == SmacnaSealClass.C,
            $"The system's class follows its critical-path loss ({criticalInWc:0.###} in. w.c.); class={result.PressureClassInWc}, problem={result.Problem}.");
        // Every item here is round: 16 in x 20 ft, 14 in x 30 ft, 8 in x 15 ft, and a 16 in tee (one diameter long).
        var area = Math.PI * (16.0 / 12 * 20 + 14.0 / 12 * 30 + 8.0 / 12 * 15 + 16.0 / 12 * 16.0 / 12);
        Near(result.TotalAreaFt2, area, 1e-6, "system surface area");
        var factor = 12 * Math.Pow(result.PressureClassInWc!.Value, 0.65);
        Near(result.TotalLeakageCfm, factor * area / 100, 1e-6, "leakage = F x A / 100");

        var fan = SystemTests.AddNode(system.Document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.FanStaticPressurePa = UnitConversions.InchesWaterToPa(3.4);
        var withFan = SmacnaLeakage.Analyze(system.Document);
        Require(withFan.PressureClassInWc == 4 && withFan.SealClass == SmacnaSealClass.A
                && withFan.DesignPressureBasis == "fan static pressure",
            $"A fan static pressure above the critical-path loss sets the class (3.4 in. w.c. -> 4, Seal Class A). Got {withFan.PressureClassInWc}, {withFan.Problem}.");
        Near(withFan.TotalLeakageCfm, 3 * Math.Pow(4, 0.65) * area / 100, 1e-6, "Seal Class A round leakage at 4 in. w.c.");

        // Customize Duct Class: the engineer's pressure class and seal class replace the calculated ones.
        system.Document.AnalysisExpectations.PressureClassOverrideInWc = 6;
        var classOnly = SmacnaLeakage.Analyze(system.Document);
        Require(classOnly.PressureClassInWc == 6 && classOnly.CalculatedPressureClassInWc == 4 && classOnly.PressureClassOverridden
                && classOnly.SealClass == SmacnaSealClass.A && classOnly.RecommendedSealClass == SmacnaSealClass.A,
            "An overridden pressure class is used, the calculated one is still reported, and the seal class follows the override.");
        Near(classOnly.TotalLeakageCfm, 3 * Math.Pow(6, 0.65) * area / 100, 1e-6, "leakage at the overridden class");
        system.Document.AnalysisExpectations.SealClassOverride = SmacnaSealClass.C;
        var both = SmacnaLeakage.Analyze(system.Document);
        Require(both.SealClass == SmacnaSealClass.C && both.SealClassOverridden && both.RecommendedSealClass == SmacnaSealClass.A,
            "An overridden seal class is used and the recommended one is still reported.");
        Near(both.TotalLeakageCfm, 12 * Math.Pow(6, 0.65) * area / 100, 1e-6, "Seal Class C round leakage at 6 in. w.c.");

        system.BranchSink.ConnectionDuctSequence = null;
        Require(SmacnaLeakage.Analyze(system.Document) is { IsAvailable: true, CalculatedPressureClassInWc: null, PressureClassInWc: 6 },
            "With an overridden class the leakage is still available when the critical path is incomplete.");
        system.Document.AnalysisExpectations.PressureClassOverrideInWc = null;
        system.Document.AnalysisExpectations.SealClassOverride = null;
        Require(!SmacnaLeakage.Analyze(system.Document).IsAvailable, "An incomplete critical path leaves the leakage unavailable.");

        // Overrides round-trip through the project file; a class that is not a SMACNA class is dropped on load.
        var path = Path.Combine(Path.GetTempPath(), $"fanworks-class-{Guid.NewGuid():N}.json");
        try
        {
            system.Document.AnalysisExpectations.PressureClassOverrideInWc = 3;
            system.Document.AnalysisExpectations.SealClassOverride = SmacnaSealClass.B;
            system.Document.LayoutLabelPlacements[LayoutLabelPlacement.NodeKey(system.Source.Sequence)] =
                new LayoutLabelPlacement { OffsetX = 40, OffsetY = -12.5, HasLeader = true, LeaderX = 3, LeaderY = 4 };
            DuctSystemStore.Save(path, system.Document);
            var loaded = DuctSystemStore.Load(path);
            var label = loaded.LayoutLabelPlacements[LayoutLabelPlacement.NodeKey(system.Source.Sequence)];
            Require(loaded.AnalysisExpectations.PressureClassOverrideInWc == 3
                    && loaded.AnalysisExpectations.SealClassOverride == SmacnaSealClass.B
                    && label.OffsetX == 40 && label.OffsetY == -12.5 && label.HasLeader && label.LeaderX == 3 && label.LeaderY == 4,
                "Duct class overrides and label placements must round-trip through the project file.");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            json["AnalysisExpectations"]!["PressureClassOverrideInWc"] = 2.5;
            File.WriteAllText(path, json.ToJsonString());
            Require(DuctSystemStore.Load(path).AnalysisExpectations.PressureClassOverrideInWc is null,
                "A pressure class that is not a SMACNA class is dropped, not rounded.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Near(double actual, double expected, double tolerance, string what)
    {
        if (Math.Abs(actual - expected) > tolerance * Math.Max(1, Math.Abs(expected)))
            throw new Exception($"{what}: expected {expected:R}, got {actual:R}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
