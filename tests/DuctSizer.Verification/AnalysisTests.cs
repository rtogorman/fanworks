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
            Require(loaded.SchemaVersion == 16 && DuctSystemDocument.CurrentSchemaVersion == 16, "Ranges are saved in file format 16.");
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
            Require(older.SchemaVersion == 16 && older.AnalysisExpectations is { AnySet: false }
                    && older.AnalysisExpectations.FittingLossPa is not null && older.LayoutNodes.Count == 1,
                "A format-15 project must open as format 16 with no ranges and nothing else changed.");

            json["SchemaVersion"] = 16;
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
