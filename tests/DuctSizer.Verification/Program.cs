using DuctSizer.Core;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("Unit conversions round-trip", TestUnitConversions),
    ("Absolute layout coordinates from signed deltas", TestAbsoluteLayoutCoordinates),
    ("Air properties at 70 F", TestAirProperties),
    ("Fan brake horsepower", TestFanHorsepower),
    ("Round duct geometry and velocity", TestRoundGeometry),
    ("Rectangular geometry", TestRectangularGeometry),
    ("ASHRAE rectangular equivalent-diameter table", TestAshraeEquivalentDiameter),
    ("Round laminar friction", TestRoundLaminar),
    ("Rectangular laminar friction", TestRectangularLaminar),
    ("Implicit Colebrook residual", TestColebrook),
    ("Transitional flow is unblended", TestTransitional),
    ("ASHRAE round friction anchor", TestAshraeRoundFrictionAnchor),
    ("NIST Darcy-Colebrook anchors", TestNistAnchors),
    ("McQuay legacy reference anchor", TestMcQuayAnchor),
    ("Check-size mode reference", TestCheckSizeMode),
    ("Exact velocity sizing", TestExactVelocitySizing),
    ("Automatic sizing uses standard 0.5 rounding", TestAutomaticRounding),
    ("Velocity and pressure sizing can round down", TestRoundDownSizing),
    ("Round pressure sizing", TestRoundPressureSizing),
    ("Rectangular pressure sizing", TestRectangularPressureSizing),
    ("Friction sizing is bracketed", TestFrictionSizing),
    ("Calculation cross-check matrix", TestCalculationMatrix),
    ("All sizing modes cross-check matrix", TestSizingMatrix),
    ("Material roughness catalog", TestMaterialCatalog),
    ("ASHRAE fitting catalog", TestFittingCatalog),
    ("Manufacturer GRD performance selection", TestGrdPerformanceSelection),
    ("Manufacturer VAV performance interpolation", TestVavPerformanceSelection),
    ("ASHRAE fitting interpolation and geometry-driven pressure loss", TestFittingCalculation),
    ("ASHRAE junction paths, signed coefficients, and dropdown tables", TestJunctionCalculation),
    ("Local duct-system persistence", TestDuctSystemPersistence),
    ("Duct-system CSV round trip and PDF report", TestDuctSystemExchange),
    ("Invalid inputs fail safely", TestInvalidInput),
    ("Junction legs use the connected duct sizes", SystemTests.JunctionLegsUseConnectedDuctSizes),
    ("Critical path charges each junction outlet its own coefficient", SystemTests.CriticalPathChargesEachOutletItsOwnCoefficient),
    ("A failed item blocks the critical-path total", SystemTests.FailedItemBlocksCriticalPathTotal),
    ("An unsized connection blocks the total", SystemTests.UnsizedConnectionBlocksTheTotal),
    ("Project file round-trips the full model", SystemTests.ProjectFileRoundTripsTheFullModel),
    ("Out-of-range table lookups are flagged", SystemTests.OutOfRangeTableLookupsAreFlagged),
    ("Transitional flow uses the upper bound everywhere", SystemTests.TransitionalFlowUsesTheUpperBoundEverywhere),
    ("Fan power rejects invalid inputs", SystemTests.FanPowerRejectsInvalidInputs),
    ("Fan airflow comes from its own system", SystemTests.FanAirflowComesFromItsOwnSystem),
    ("System solver conserves airflow at a diverging junction", SolverTests.DivergingJunctionConservesAirflow),
    ("System solver follows each junction path's pressure loss", SolverTests.PressureFollowsEachJunctionPath),
    ("System solver: fan static closes the loop to zero", SolverTests.FanAddsStaticPressureAndLossesCloseToZero),
    ("System solver flags an overflowing locked outlet", SolverTests.LockedOutletOverflowIsFlagged),
    ("System solver: failed duct makes downstream pressure not available", SolverTests.FailedDuctMakesDownstreamPressureNotAvailable),
    ("System solver: unsized connection makes downstream pressure not available", SolverTests.UnsizedConnectionMakesDownstreamPressureNotAvailable),
    ("System solver sums converging inlets", SolverTests.ConvergingJunctionSumsItsInlets),
    ("System solver is repeatable", SolverTests.SolvingTwiceGivesTheSameResult),
    ("Layout geometry: consistent layout is clean", GeometryTests.ConsistentLayoutHasNoGeometryIssues),
    ("Layout geometry: loop that does not close is flagged", GeometryTests.LoopThatDoesNotCloseIsFlagged),
    ("Layout geometry: duct offset and length checked", GeometryTests.DuctOffsetAndLengthAreCheckedAgainstTheConnection),
    ("Layout geometry: unplaced component flagged unless anchored", GeometryTests.UnplacedComponentIsFlaggedUnlessAnchored),
    ("Undo history is bounded", HistoryTests.UndoHistoryIsBounded),
    ("Layout model check: valid system is clean", ModelCheckTests.ValidSystemHasNoModelIssues),
    ("Layout model check: invalid combinations reported", ModelCheckTests.InvalidCombinationsAreReported),
    ("Layout model check: closed upstream loop reported", ModelCheckTests.ClosedUpstreamLoopIsReported),
    ("Exports do not depend on regional settings", ExportCultureTests.ExportsDoNotDependOnRegionalSettings),
    ("Positions: saved file stores absolute positions", PositionTests.SavedFileStoresAbsolutePositions),
    ("Positions: stored absolute positions win over offsets", PositionTests.StoredAbsolutePositionsWinOverOffsets),
    ("Positions: older files migrate without moving components", PositionTests.OlderFilesMigrateWithoutMovingComponents),
    ("Component model: each type saves only its own data", ComponentModelTests.EachComponentTypeSavesOnlyItsOwnData),
    ("Component model: older flat fields move into type blocks", ComponentModelTests.FlatFieldsFromOlderFilesMoveIntoTheirBlocks),
    ("Item notes: saved with the project and kept per item", ItemNotesTests.NotesRoundTripAndFollowTheirItem),
    ("Item notes: older files open with no notes", ItemNotesTests.OlderFilesOpenWithNoNotes),
    ("Expected ranges: inclusive, one-sided, no defaults", AnalysisTests.RangesCompareInclusiveAndOneSided),
    ("Expected ranges: ducts and fittings checked against their own ranges", AnalysisTests.DuctsAndFittingsAreCheckedAgainstTheirOwnRanges),
    ("VAV expected ranges: inlet static pressure and pressure drop", AnalysisTests.VavRangesCheckInletPressureAndDrop),
    ("Expected ranges: transitional duct checked across its whole band", AnalysisTests.TransitionalDuctUsesBothBounds),
    ("Expected ranges: saved with the project; older files open empty", AnalysisTests.ExpectationsRoundTripAndOlderFilesOpenEmpty),
    ("Route profiles: totals and order match the critical-path analysis", AnalysisTests.RouteProfilesMatchTheCriticalPath),
    ("Route profiles: unsized connection makes its route incomplete", AnalysisTests.UnsizedConnectionMakesItsRouteIncomplete),
    ("SMACNA: pressure, seal and leakage classes; F = CL x P^0.65", LeakageTests.PressureSealAndLeakageClassesFollowSmacna),
    ("Surface area of ducts and fittings", LeakageTests.SurfaceAreasOfDuctsAndFittings),
    ("System leakage: class from pressure, F x area", LeakageTests.SystemLeakageIsSumOfFactorTimesArea),
    ("Air handling unit: total static = external + internal; motor power on total", AirHandlingUnitTests.TotalStaticIsExternalPlusInternalAndSetsMotorPower),
    ("Air handling unit: duct system sees only the external static pressure", AirHandlingUnitTests.DuctSystemSeesOnlyTheExternalStaticPressure),
    ("Air handling unit: saved in format 18; format-17 fans open unchanged", AirHandlingUnitTests.AirHandlingUnitRoundTripsAndOlderFilesOpenAsFans),
    ("External static pressure splits at the fan: suction (negative) + discharge (positive)", AirHandlingUnitTests.ExternalStaticSplitsAtTheFan),
    ("Duct class, seal class and leakage for each side of the fan (negative suction, positive discharge)", AirHandlingUnitTests.DuctClassIsSetForEachSideOfTheFan),
    ("Review: fitting size and transition area ratio follow the connected ducts", ReviewCorrectionTests.FittingSizeFollowsConnectedDucts),
    ("Review: static pressure = total − velocity pressure (contraction, regain)", ReviewCorrectionTests.StaticPressureIsTotalLessVelocityPressure),
    ("Review: exit loss of a bare duct into a space is one velocity pressure", ReviewCorrectionTests.ExitLossIsOneVelocityPressureOfABareDuct),
    ("Review: standalone items stay out of system area and leakage", ReviewCorrectionTests.StandaloneItemsStayOutOfSystemTotals),
    ("Review: a fitting with no airflow or size is invalid", ReviewCorrectionTests.FittingWithoutAirflowOrSizeIsInvalid),
    ("Review: a split without a junction fitting is reported", ReviewCorrectionTests.SplitsWithoutAJunctionAreReported),
    ("VAV with reheat: entered terminal pressure drop is added to the coil drop", ReviewCorrectionTests.VavReheatAddsTheEnteredTerminalDrop),
    ("Auto Size: components are taken in flow order, from the sources to the sinks", SizeLockTests.FlowOrderRunsFromSourcesToSinks),
    ("Size lock: a locked fitting keeps its size; its airflow still follows the system", SizeLockTests.LockedFittingKeepsItsSize),
    ("File format 22: size locks are saved; manual flow reversal is retired", SizeLockTests.LocksAreSavedAndReversalIsRetired),
    ("Drawn ducts: an elbow's angle is the angle between its drawn ducts (45° diagonal, 90° riser)", DuctDrawingTests.ElbowAngleFollowsTheDrawnDucts),
    ("Drawn ducts: a transition that turns the air is reported as a missing elbow", DuctDrawingTests.TurnAtAComponentThatCannotTurnIsReported),
    ("Drawn ducts: slope and turn angles", DuctDrawingTests.SlopeAndTurnAngles),
    ("Table look-ups: interpolation recorded with its table points", TableLookupTests.InterpolationIsRecordedWithItsTablePoints),
    ("Elbow factors named (Kθ, C'o) and combined as C = Kθ × C'o", TableLookupTests.ElbowFactorsAreNamedAndCombined),
    ("Table look-ups: conservative value between columns", TableLookupTests.BetweenColumnsTheConservativeValueIsUsed),
    ("Junctions read only the selected path's table", TableLookupTests.OnlyTheSelectedJunctionPathIsRead),
    ("Engineering text typeset for display", TableLookupTests.EngineeringTextIsTypeset)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL  {test.Name}: {ex}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} verification checks passed.");
if (failures.Count > 0)
    Environment.Exit(1);

static void TestUnitConversions()
{
    AssertNear(UnitConversions.M3PerSToCfm(UnitConversions.CfmToM3PerS(1234.5)), 1234.5, 1e-10);
    AssertNear(UnitConversions.PaPerMToInchesWaterPer100Feet(
        UnitConversions.InchesWaterPer100FeetToPaPerM(0.08)), 0.08, 1e-12);
}

static void TestAbsoluteLayoutCoordinates()
{
    var document = new DuctSystemDocument();
    var origin = new SavedLayoutNode { Sequence = 1, Type = LayoutNodeType.Fan };
    var child = new SavedLayoutNode
    {
        Sequence = 2, Type = LayoutNodeType.Coil, ParentNodeId = origin.Id,
        DeltaXM = UnitConversions.FeetToM(12), DeltaYM = UnitConversions.FeetToM(-7),
        DeltaZM = UnitConversions.FeetToM(3)
    };
    var grandchild = new SavedLayoutNode
    {
        Sequence = 3, Type = LayoutNodeType.Filter, ParentNodeId = child.Id,
        DeltaXM = UnitConversions.FeetToM(-2), DeltaYM = UnitConversions.FeetToM(5),
        DeltaZM = UnitConversions.FeetToM(-1)
    };
    var detachedRoot = new SavedLayoutNode
    {
        Sequence = 4, Type = LayoutNodeType.Filter, HasAbsoluteCoordinateAnchor = true,
        AbsoluteAnchorXM = UnitConversions.FeetToM(-20), AbsoluteAnchorYM = UnitConversions.FeetToM(30),
        AbsoluteAnchorZM = UnitConversions.FeetToM(4)
    };
    var detachedChild = new SavedLayoutNode
    {
        Sequence = 5, Type = LayoutNodeType.Coil, ParentNodeId = detachedRoot.Id,
        DeltaXM = UnitConversions.FeetToM(6), DeltaYM = UnitConversions.FeetToM(2),
        DeltaZM = UnitConversions.FeetToM(-3)
    };
    var mergeSecondarySource = new SavedLayoutNode
    {
        Sequence = 6, Type = LayoutNodeType.Fan
    };
    var merge = new SavedLayoutNode
    {
        Sequence = 7, Type = LayoutNodeType.Fitting, FittingGroup = "Converging Fittings",
        ParentNodeId = grandchild.Id, SecondaryParentNodeId = mergeSecondarySource.Id,
        DeltaXM = UnitConversions.FeetToM(4), DeltaYM = UnitConversions.FeetToM(2),
        SecondaryDeltaXM = UnitConversions.FeetToM(-10),
        SecondaryDeltaYM = UnitConversions.FeetToM(6),
        SecondaryDeltaZM = UnitConversions.FeetToM(1)
    };
    document.LayoutNodes.AddRange(new[]
        { origin, child, grandchild, detachedRoot, detachedChild, mergeSecondarySource, merge });
    document.CoordinateOriginNodeId = child.Id;
    document.CoordinateOriginXM = UnitConversions.FeetToM(100);
    document.CoordinateOriginYM = UnitConversions.FeetToM(50);
    document.CoordinateOriginZM = UnitConversions.FeetToM(8);
    var coordinates = LayoutCoordinateResolver.Resolve(document);
    AssertNear(UnitConversions.MToFeet(coordinates[origin.Sequence].XM), 88, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[origin.Sequence].YM), 57, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[grandchild.Sequence].XM), 98, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[grandchild.Sequence].YM), 55, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[grandchild.Sequence].ZM), 7, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[detachedRoot.Sequence].XM), -20, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[detachedChild.Sequence].XM), -14, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[detachedChild.Sequence].YM), 32, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[detachedChild.Sequence].ZM), 1, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[merge.Sequence].XM), 102, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[merge.Sequence].YM), 57, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[mergeSecondarySource.Sequence].XM), 112, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[mergeSecondarySource.Sequence].YM), 51, 1e-10);
    AssertNear(UnitConversions.MToFeet(coordinates[mergeSecondarySource.Sequence].ZM), 6, 1e-10);
}

static void TestAirProperties()
{
    var air = AirProperties.Calculate(UnitConversions.FahrenheitToCelsius(70.0), 0.50, 101_325.0);
    AssertBetween(air.DensityKgPerM3, 1.18, 1.21, "density");
    AssertBetween(air.DynamicViscosityPaS, 1.80e-5, 1.84e-5, "dynamic viscosity");
}

static void TestFanHorsepower()
{
    var horsepower = FanPerformance.BrakeHorsepower(UnitConversions.CfmToM3PerS(1_000),
        UnitConversions.InchesWaterToPa(2.0), 50.0);
    AssertNear(horsepower, 1_000.0 * 2.0 / (6356.0 * 0.5), 1e-12);
    AssertNear(FanPerformance.BrakeHorsepower(0, UnitConversions.InchesWaterToPa(2.0), 50.0), 0, 1e-12);
}

static void TestRoundGeometry()
{
    var geometry = GeometryCalculator.Round(UnitConversions.InchesToM(12.0));
    AssertNear(UnitConversions.SquareMetersToSquareFeet(geometry.ActualAreaM2), Math.PI / 4.0, 1e-10);
    var velocity = UnitConversions.CfmToM3PerS(1000.0) / geometry.ActualAreaM2;
    AssertNear(UnitConversions.MpsToFpm(velocity), 4000.0 / Math.PI, 1e-6);
}

static void TestRectangularGeometry()
{
    var geometry = GeometryCalculator.Rectangular(UnitConversions.InchesToM(24), UnitConversions.InchesToM(12));
    AssertNear(UnitConversions.MToInches(geometry.HydraulicDiameterM), 16.0, 1e-10);
    if (geometry.HuebscherDiameterM <= geometry.HydraulicDiameterM)
        throw new Exception("Huebscher diameter should exceed hydraulic diameter for a 2:1 rectangle.");
}

static void TestAshraeEquivalentDiameter()
{
    var geometry = GeometryCalculator.Rectangular(0.400, 0.200);
    AssertNear(geometry.HuebscherDiameterM * 1000.0, 305.0, 1.0);
}

static void TestRoundLaminar()
{
    var result = FrictionModel.Calculate(1000.0, 0.001, 0.25, DuctShape.Round, 1.0);
    AssertNear(result.Factor ?? double.NaN, 0.064, 1e-12);
    if (result.Regime != FlowRegime.Laminar)
        throw new Exception("Expected laminar flow.");
}

static void TestRectangularLaminar()
{
    var result = FrictionModel.Calculate(1000.0, 0.001, 0.25, DuctShape.Rectangular, 1.0);
    AssertNear((result.Factor ?? double.NaN) * 1000.0, 56.9184, 1e-8);
}

static void TestColebrook()
{
    const double reynolds = 100_000.0;
    const double relativeRoughness = 0.0003;
    var factor = FrictionModel.SolveColebrook(reynolds, relativeRoughness, 1.0);
    var residual = 1.0 / Math.Sqrt(factor)
                   + 2.0 * Math.Log10(relativeRoughness / 3.7 + 2.51 / (reynolds * Math.Sqrt(factor)));
    AssertNear(residual, 0.0, 1e-10);
    AssertBetween(factor, 0.019, 0.020, "Colebrook friction factor");
    var diagnostics = FrictionModel.SolveColebrookDetailed(reynolds, relativeRoughness, 1.0);
    if (!diagnostics.Converged || diagnostics.Iterations <= 0 || diagnostics.Iterations > 256
        || Math.Abs(diagnostics.FinalResidual) > 1e-10)
        throw new Exception("Colebrook convergence diagnostics were not retained accurately.");
}

static void TestTransitional()
{
    var result = FrictionModel.Calculate(3000.0, 0.0001, 0.25, DuctShape.Round, 1.0);
    if (result.Regime != FlowRegime.Transitional || result.Factor.HasValue)
        throw new Exception("Transition result must contain bounds without a blended factor.");
    if (!(result.LowerFactor < result.UpperFactor))
        throw new Exception("Transition bounds are not ordered.");
}

static void TestAshraeRoundFrictionAnchor()
{
    var result = DuctCalculator.Calculate(new DuctCalculationInput(
        UnitConversions.CfmToM3PerS(8000.0),
        20.0,
        0.50,
        101_325.0,
        GeometryCalculator.Round(UnitConversions.InchesToM(26.0)),
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0)));

    var rate = UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM);
    AssertNear(rate, 0.200, 0.006);
    AssertNear(UnitConversions.MpsToFpm(result.ActualVelocityMPerS), 2169.78, 0.5);
}

static void TestNistAnchors()
{
    var material = new DuctMaterial(
        "NIST reference",
        0.00015 / UnitConversions.FeetToMeters);
    var air = AirProperties.Calculate(20.0, 0.50, 101_325.0);

    var low = DuctCalculator.Calculate(new DuctCalculationInput(
        0.240 / air.DensityKgPerM3,
        20.0,
        0.50,
        101_325.0,
        GeometryCalculator.Round(0.250),
        material,
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        1.0));
    AssertNear(low.PressureLossPaPerM, 0.90, 0.12);

    var high = DuctCalculator.Calculate(new DuctCalculationInput(
        6.190 / air.DensityKgPerM3,
        20.0,
        0.50,
        101_325.0,
        GeometryCalculator.Round(0.630),
        material,
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        1.0));
    AssertNear(high.PressureLossPaPerM, 4.00, 0.12);
}

static void TestMcQuayAnchor()
{
    var result = DuctCalculator.Calculate(new DuctCalculationInput(
        UnitConversions.CfmToM3PerS(120.0),
        UnitConversions.FahrenheitToCelsius(68.0),
        0.50,
        101_325.0,
        GeometryCalculator.Round(UnitConversions.InchesToM(6.2)),
        new DuctMaterial("McQuay legacy reference", 0.00070),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.EquivalentRoundArea,
        UnitConversions.FeetToM(100.0)));

    AssertNear(UnitConversions.SquareMetersToSquareFeet(result.Input.Geometry.ActualAreaM2), 0.2097, 0.0002);
    AssertNear(UnitConversions.MpsToFpm(result.ActualVelocityMPerS), 572.2, 1.0);
    AssertNear(result.ReynoldsNumber, 30_798.0, 700.0);
    AssertNear(result.Friction.Factor ?? double.NaN, 0.02665, 0.00030);
    AssertNear(UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM), 0.105, 0.002);
}

static void TestCheckSizeMode()
{
    var result = DuctCalculator.Calculate(new DuctCalculationInput(
        UnitConversions.CfmToM3PerS(1000.0),
        20.0,
        0.50,
        101_325.0,
        GeometryCalculator.Round(UnitConversions.InchesToM(16.0)),
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0)));

    AssertNear(UnitConversions.MpsToFpm(result.ActualVelocityMPerS), 716.20, 0.10);
    AssertNear(UnitConversions.PaPerMToInchesWaterPer100Feet(result.PressureLossPaPerM), 0.0459, 0.0010);
}

static void TestExactVelocitySizing()
{
    var sizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000.0),
        20.0,
        0.50,
        101_325.0,
        DuctShape.Round,
        1.0,
        DuctMaterialCatalog.All[0],
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByMaximumVelocity,
        1.0,
        UnitConversions.FpmToMps(1500.0),
        0.0));

    AssertBetween(UnitConversions.MToInches(sizing.RecommendedGeometry.DiameterM), 11.0, 11.1, "exact velocity-sized diameter");
    AssertNear(UnitConversions.MpsToFpm(sizing.Calculation.ActualVelocityMPerS), 1500.0, 1e-7);
}

static void TestAutomaticRounding()
{
    var material = DuctMaterialCatalog.All[0];
    var sizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000),
        UnitConversions.FahrenheitToCelsius(70),
        0.50,
        101_325,
        DuctShape.Round,
        1.0,
        material,
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100),
        CalculationMode.SizeByMaximumVelocity,
        1.0,
        UnitConversions.FpmToMps(1500),
        UnitConversions.InchesToM(1)));
    AssertNear(UnitConversions.MToInches(sizing.RecommendedGeometry.DiameterM), 11.0, 1e-9);

    DuctSizingResult AtExactDiameter(double diameterIn)
    {
        const double airflow = 0.5;
        var diameterM = UnitConversions.InchesToM(diameterIn);
        var targetVelocity = airflow / (Math.PI * diameterM * diameterM / 4.0);
        return DuctSizingSolver.Size(new DuctSizingInput(
            airflow, 20.0, 0.50, 101_325.0, DuctShape.Round, 1.0, material,
            EquivalentDiameterMethod.Huebscher, PressureLossBasis.ActualDuctArea,
            UnitConversions.FeetToM(100.0), CalculationMode.SizeByMaximumVelocity,
            1.0, targetVelocity, UnitConversions.InchesToM(1.0),
            SizeRoundingDirection.Automatic));
    }

    AssertNear(UnitConversions.MToInches(AtExactDiameter(11.49).RecommendedGeometry.DiameterM), 11.0, 1e-8);
    AssertNear(UnitConversions.MToInches(AtExactDiameter(11.50).RecommendedGeometry.DiameterM), 12.0, 1e-8);
}

static void TestRoundDownSizing()
{
    var velocitySizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000.0),
        20.0,
        0.50,
        101_325.0,
        DuctShape.Round,
        1.0,
        DuctMaterialCatalog.All[0],
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByMaximumVelocity,
        1.0,
        UnitConversions.FpmToMps(1500.0),
        UnitConversions.InchesToM(1.0),
        SizeRoundingDirection.Down));
    AssertNear(UnitConversions.MToInches(velocitySizing.RecommendedGeometry.DiameterM), 11.0, 1e-9);
    if (UnitConversions.MpsToFpm(velocitySizing.Calculation.ActualVelocityMPerS) <= 1500.0)
        throw new Exception("Round-down velocity sizing should use the smaller nominal duct.");

    var pressureTarget = UnitConversions.InchesWaterPer100FeetToPaPerM(0.20);
    var pressureSizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(8000.0),
        20.0,
        0.50,
        101_325.0,
        DuctShape.Round,
        1.0,
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByTargetFriction,
        pressureTarget,
        1.0,
        UnitConversions.InchesToM(1.0),
        SizeRoundingDirection.Down));
    AssertNear(UnitConversions.MToInches(pressureSizing.RecommendedGeometry.DiameterM), 26.0, 1e-9);
    if (pressureSizing.Calculation.PressureLossPaPerM <= pressureTarget)
        throw new Exception("Round-down pressure sizing should use the smaller nominal duct.");
}

static void TestRoundPressureSizing()
{
    var target = UnitConversions.InchesWaterPer100FeetToPaPerM(0.20);
    var sizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(8000.0),
        20.0,
        0.50,
        101_325.0,
        DuctShape.Round,
        1.0,
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByTargetFriction,
        target,
        1.0,
        0.0));

    AssertBetween(UnitConversions.MToInches(sizing.RecommendedGeometry.DiameterM), 25.8, 26.2, "round pressure-sized diameter");
    AssertNear(UnitConversions.PaPerMToInchesWaterPer100Feet(sizing.Calculation.PressureLossPaPerM), 0.20, 1e-7);
}

static void TestRectangularPressureSizing()
{
    var target = UnitConversions.InchesWaterPer100FeetToPaPerM(0.20);
    var sizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(23_500.0),
        20.0,
        0.50,
        101_325.0,
        DuctShape.Rectangular,
        2.0,
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.EquivalentRoundArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByTargetFriction,
        target,
        1.0,
        UnitConversions.InchesToM(1.0)));

    AssertBetween(UnitConversions.MToInches(sizing.ContinuousGeometry.WidthM), 51.4, 51.6, "continuous rectangular pressure-sized width");
    AssertBetween(UnitConversions.MToInches(sizing.ContinuousGeometry.HeightM), 25.7, 25.8, "continuous rectangular pressure-sized height");
    AssertNear(UnitConversions.MToInches(sizing.RecommendedGeometry.WidthM), 51.0, 1e-9);
    AssertNear(UnitConversions.MToInches(sizing.RecommendedGeometry.HeightM), 26.0, 1e-9);
    AssertBetween(
        UnitConversions.PaPerMToInchesWaterPer100Feet(sizing.Calculation.PressureLossPaPerM),
        0.19,
        0.21,
        "rounded rectangular pressure rate");

    var fixedWidthSizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000.0),
        UnitConversions.FahrenheitToCelsius(70.0),
        0.50,
        101_325.0,
        DuctShape.Rectangular,
        1.0,
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.EquivalentRoundArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByTargetFriction,
        UnitConversions.InchesWaterPer100FeetToPaPerM(0.08),
        1.0,
        UnitConversions.InchesToM(1.0),
        SizeRoundingDirection.Automatic,
        RectangularFixedDimension.Width,
        UnitConversions.InchesToM(16.0)));
    AssertNear(UnitConversions.MToInches(fixedWidthSizing.RecommendedGeometry.WidthM), 16.0, 1e-9);
    AssertNear(UnitConversions.MToInches(fixedWidthSizing.RecommendedGeometry.HeightM),
        Math.Round(UnitConversions.MToInches(fixedWidthSizing.ContinuousGeometry.HeightM)), 1e-9);

    var fixedHeightSizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000.0),
        UnitConversions.FahrenheitToCelsius(70.0),
        0.50,
        101_325.0,
        DuctShape.Rectangular,
        1.0,
        new DuctMaterial("ASHRAE galvanized reference", 0.00030),
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.EquivalentRoundArea,
        UnitConversions.FeetToM(100.0),
        CalculationMode.SizeByMaximumVelocity,
        1.0,
        UnitConversions.FpmToMps(1000.0),
        0.0,
        SizeRoundingDirection.Automatic,
        RectangularFixedDimension.Height,
        UnitConversions.InchesToM(12.0)));
    AssertNear(UnitConversions.MToInches(fixedHeightSizing.RecommendedGeometry.HeightM), 12.0, 1e-9);
    AssertNear(UnitConversions.MpsToFpm(fixedHeightSizing.Calculation.ActualVelocityMPerS), 1000.0, 1e-7);
}

static void TestFrictionSizing()
{
    var target = UnitConversions.InchesWaterPer100FeetToPaPerM(0.08);
    var sizing = DuctSizingSolver.Size(new DuctSizingInput(
        UnitConversions.CfmToM3PerS(1000),
        UnitConversions.FahrenheitToCelsius(70),
        0.50,
        101_325,
        DuctShape.Rectangular,
        2.0,
        DuctMaterialCatalog.All[1],
        EquivalentDiameterMethod.Huebscher,
        PressureLossBasis.ActualDuctArea,
        UnitConversions.FeetToM(100),
        CalculationMode.SizeByTargetFriction,
        target,
        1.0,
        0.0));
    AssertNear(sizing.Calculation.PressureLossUpperPaPerM, target, target * 2e-8);
}

static void TestCalculationMatrix()
{
    var flows = new[] { 250.0, 1_000.0, 8_000.0 };
    var temperaturesC = new[] { -20.0, 20.0, 60.0 };
    var geometries = new[]
    {
        GeometryCalculator.Round(UnitConversions.InchesToM(14.0)),
        GeometryCalculator.Rectangular(UnitConversions.InchesToM(24.0), UnitConversions.InchesToM(12.0))
    };
    var methods = Enum.GetValues<EquivalentDiameterMethod>();
    var bases = Enum.GetValues<PressureLossBasis>();

    foreach (var flowCfm in flows)
    foreach (var temperatureC in temperaturesC)
    foreach (var geometry in geometries)
    foreach (var method in methods)
    foreach (var basis in bases)
    {
        var result = DuctCalculator.Calculate(new DuctCalculationInput(
            UnitConversions.CfmToM3PerS(flowCfm),
            temperatureC,
            0.50,
            101_325.0,
            geometry,
            DuctMaterialCatalog.All[0],
            method,
            basis,
            UnitConversions.FeetToM(100.0)));

        AssertNear(
            result.ActualVelocityMPerS,
            result.Input.AirflowM3PerS / geometry.ActualAreaM2,
            Math.Max(1e-12, result.ActualVelocityMPerS * 1e-12));
        AssertNear(
            result.EquivalentVelocityMPerS,
            result.Input.AirflowM3PerS / result.EquivalentAreaM2,
            Math.Max(1e-12, result.EquivalentVelocityMPerS * 1e-12));

        var expectedLower = result.Friction.LowerFactor / result.CalculationDiameterM
                            * result.Air.DensityKgPerM3
                            * result.CalculationVelocityMPerS * result.CalculationVelocityMPerS / 2.0;
        var expectedUpper = result.Friction.UpperFactor / result.CalculationDiameterM
                            * result.Air.DensityKgPerM3
                            * result.CalculationVelocityMPerS * result.CalculationVelocityMPerS / 2.0;
        AssertNear(result.PressureLossLowerPaPerM, Math.Min(expectedLower, expectedUpper), Math.Max(1e-12, expectedLower * 1e-12));
        AssertNear(result.PressureLossUpperPaPerM, Math.Max(expectedLower, expectedUpper), Math.Max(1e-12, expectedUpper * 1e-12));
    }
}

static void TestSizingMatrix()
{
    var shapes = Enum.GetValues<DuctShape>();
    var methods = Enum.GetValues<EquivalentDiameterMethod>();
    var bases = Enum.GetValues<PressureLossBasis>();
    var flowsCfm = new[] { 250.0, 1_000.0, 8_000.0 };

    foreach (var shape in shapes)
    foreach (var method in methods)
    foreach (var basis in bases)
    foreach (var flowCfm in flowsCfm)
    {
        var velocityTarget = UnitConversions.FpmToMps(1_500.0);
        var velocitySizing = DuctSizingSolver.Size(new DuctSizingInput(
            UnitConversions.CfmToM3PerS(flowCfm), 20.0, 0.50, 101_325.0,
            shape, 2.0, DuctMaterialCatalog.All[0], method, basis,
            UnitConversions.FeetToM(100.0), CalculationMode.SizeByMaximumVelocity,
            1.0, velocityTarget, 0.0));
        AssertNear(
            velocitySizing.Calculation.ActualVelocityMPerS,
            velocityTarget,
            velocityTarget * 1e-10);

        var pressureTarget = UnitConversions.InchesWaterPer100FeetToPaPerM(0.20);
        var pressureSizing = DuctSizingSolver.Size(new DuctSizingInput(
            UnitConversions.CfmToM3PerS(flowCfm), 20.0, 0.50, 101_325.0,
            shape, 2.0, DuctMaterialCatalog.All[0], method, basis,
            UnitConversions.FeetToM(100.0), CalculationMode.SizeByTargetFriction,
            pressureTarget, 1.0, 0.0));
        AssertNear(
            pressureSizing.Calculation.PressureLossUpperPaPerM,
            pressureTarget,
            pressureTarget * 2e-8);
    }
}

static void TestMaterialCatalog()
{
    AssertNear(DuctMaterialCatalog.All[0].RoughnessFeet, 0.00070, 1e-12);
    AssertNear(DuctMaterialCatalog.All.Single(x => x.Name.StartsWith("Galvanized Spiral")).RoughnessFeet, 0.00030, 1e-12);
    AssertNear(DuctMaterialCatalog.All.Single(x => x.Name.StartsWith("Flexible HVAC")).RoughnessFeet, 0.00300, 1e-12);
}

static void TestFittingCatalog()
{
    if (FittingCatalog.All.Count != 108)
        throw new Exception($"Expected 103 ASHRAE fittings plus three published GRD and two VAV entries; found {FittingCatalog.All.Count}.");
    if (FittingCatalog.All.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != FittingCatalog.All.Count)
        throw new Exception("Fitting codes must be unique.");
    if (FittingCatalog.All.Count(x => x.IsAutomated) != 106)
        throw new Exception($"Expected 101 safely digitized ASHRAE fittings plus five manufacturer selectors; found {FittingCatalog.All.Count(x => x.IsAutomated)}.");
    var grouped = FittingCatalog.All.GroupBy(FittingCatalog.SelectionGroup).ToDictionary(x => x.Key, x => x.Count());
    if (FittingCatalog.SelectionGroups.Any(group => !grouped.ContainsKey(group)) || grouped.Values.Sum() != 108)
        throw new Exception("Every ASHRAE fitting and GRD should appear in exactly one requested selection group.");
    if (!grouped.TryGetValue("GRDs", out var grdCount) || grdCount != 3
        || FittingCatalog.All.Any(x => x.Code == "GRD-REGISTER" || x.Description.Contains("Register", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("The GRD group must contain only the Titus 350R grille, Price SCD diffuser, and Titus TBD-80 D linear diffuser.");
    if (!grouped.TryGetValue(FittingCatalog.VavSelectionGroup, out var vavCount) || vavCount != 2)
        throw new Exception("The VAV group must contain the Titus DESV with-reheat and without-reheat selectors.");
    if (!grouped.ContainsKey(FittingCatalog.TerminalSelectionGroup)
        || grouped.ContainsKey("Terminals")
        || !FittingCatalog.IsTerminalSelectionGroup("Terminals"))
        throw new Exception("Terminal and takeoff fittings should use the new label while accepting legacy saved groups.");
    if (new[] { "6-7", "6-8", "6-9" }.Any(code => FittingCatalog.SelectionGroup(FittingCatalog.Find(code)) != "Dampers"))
        throw new Exception("ASHRAE 6-7 through 6-9 should be grouped with Dampers.");
    if (new[] { "6-10", "6-11" }.Any(code => FittingCatalog.SelectionGroup(FittingCatalog.Find(code)) != "Transitions"))
        throw new Exception("ASHRAE 6-10 and 6-11 should be grouped with Transitions.");
    if (!grouped.ContainsKey(FittingCatalog.FanExitsAndPlenumsSelectionGroup)
        || grouped.ContainsKey("Others")
        || !FittingCatalog.IsFanExitsAndPlenumsSelectionGroup("Others"))
        throw new Exception("Fan exits and plenums should use the new label while accepting legacy saved groups.");
    if (FittingCatalog.InletPortCount("Converging Fittings") != 2
        || FittingCatalog.OutletPortCount("Diverting Fittings") != 2)
        throw new Exception("Converging and diverting fittings should expose their two-port sides.");
    if (!FittingCatalog.Find("3-1").IsAutomated || !FittingCatalog.Find("5-1").IsAutomated || FittingCatalog.Find("5-4").IsAutomated)
        throw new Exception("Automated and reference-coefficient entries are not identified correctly.");
    if (FittingCatalog.Find("3-1").ReferenceDuctShape != DuctShape.Round
        || FittingCatalog.Find("3-5").ReferenceDuctShape != DuctShape.Rectangular
        || FittingCatalog.Find("3-13").ReferenceDuctShape is not null)
        throw new Exception("Fitting reference-duct shape inference should distinguish round-only, rectangular-only, and dual-shape entries.");
}

static void TestGrdPerformanceSelection()
{
    if (GrdPerformanceCatalog.Titus350RConfigurations.Count != 78
        || GrdPerformanceCatalog.Titus350RConfigurations.Sum(configuration =>
            GrdPerformanceCatalog.Titus350RTable(configuration.NominalDuctSizeIn).Count) != 702)
        throw new Exception("The complete Titus 350R catalog should contain 78 nominal-size charts and 702 performance points.");
    var grille = GrdPerformanceCatalog.SelectTitus350R(500, "12x12");
    if (grille.Selected.AirflowCfm != 528 || grille.Selected.CoreVelocityFpm != 600
        || grille.Selected.SoundNc != "19" || !grille.AirflowWasRoundedUp)
        throw new Exception("Titus 350R lookup did not round upward to the expected 12x12 published airflow row.");
    AssertNear(grille.Selected.VelocityPressureInWg ?? 0, .022, 1e-12);
    AssertNear(grille.Selected.TotalPressureInWg, .073, 1e-12);
    if (!grille.SourceReference.Contains("courtesy of Titus", StringComparison.OrdinalIgnoreCase))
        throw new Exception("The Titus 350R performance result should retain its manufacturer reference.");
    var grilleResult = GrdPerformanceCatalog.CalculateFittingResult(
        FittingCatalog.Find(GrdPerformanceCatalog.Titus350RCode),
        new Dictionary<string, double> { ["sizeIndex"] = GrdPerformanceCatalog.Titus350RSizeIndex("12x12") },
        UnitConversions.CfmToM3PerS(500), UnitConversions.FahrenheitToCelsius(70));
    AssertNear(UnitConversions.PaToInchesWater(grilleResult.PressureLossPa), .073, 1e-12);
    AssertNear(UnitConversions.MpsToFpm(grilleResult.ReferenceVelocityMPerS), 600, 1e-9);

    var price = GrdPerformanceCatalog.SelectPriceScd(450, 24, 8);
    AssertNear(price.Selected.AirflowCfm, 489, 1e-12);
    AssertNear(price.Selected.TotalPressureInWg, .199, 1e-12);
    if (price.Selected.SoundNc != "36" || price.Selected.ThrowFt != "6-8-12" || !price.AirflowWasRoundedUp)
        throw new Exception("Price SCD lookup did not round upward to the expected published performance row.");
    var exact = GrdPerformanceCatalog.SelectPriceScd(545, 20, 10);
    AssertNear(exact.Selected.AirflowCfm, 545, 1e-12);
    if (exact.AirflowWasRoundedUp)
        throw new Exception("An exact Price SCD airflow match should not be marked as rounded.");
    var titus = GrdPerformanceCatalog.SelectTitusTbd80(76, 4);
    AssertNear(titus.Selected.AirflowCfm, 90, 1e-12);
    AssertNear(titus.Selected.TotalPressureInWg, .129, 1e-12);
    if (titus.Selected.SoundNc != "31" || titus.Selected.ThrowFt != "10-12-17")
        throw new Exception("Titus TBD-80 lookup did not select the expected 4-foot performance row.");
    if (GrdPerformanceCatalog.TitusConfigurations.Count != 132
        || GrdPerformanceCatalog.TitusConfigurations.SelectMany(configuration =>
            GrdPerformanceCatalog.TitusTable(configuration.LengthFt, configuration.SlotWidthIn,
                configuration.SlotCount, configuration.InletDiameterIn)).Count() != 924)
        throw new Exception("The complete Titus TBD-80 D catalog should contain 132 tables and 924 performance points.");
    var titusMultiSlot = GrdPerformanceCatalog.SelectTitusTbd80(500, 4, 1.0, 3);
    if (titusMultiSlot.InletDiameterIn != 12 || titusMultiSlot.Selected.AirflowCfm != 535
        || titusMultiSlot.Selected.SoundNc != "36" || titusMultiSlot.Selected.ThrowFt != "25-30-43")
        throw new Exception("Titus TBD-80 D multi-slot lookup did not select the expected inlet and performance row.");
    AssertNear(titusMultiSlot.Selected.TotalPressureInWg, .178, 1e-12);
    AssertNear(titusMultiSlot.Selected.StaticPressureInWg ?? 0, .139, 1e-12);
    if (!titusMultiSlot.SourceReference.Contains("courtesy of Titus", StringComparison.OrdinalIgnoreCase))
        throw new Exception("The Titus performance result should retain its manufacturer reference.");
    var result = GrdPerformanceCatalog.CalculateFittingResult(FittingCatalog.Find(GrdPerformanceCatalog.PriceScdCode),
        new Dictionary<string, double> { ["face"] = 24, ["neck"] = 8 },
        UnitConversions.CfmToM3PerS(450), UnitConversions.FahrenheitToCelsius(70));
    AssertNear(UnitConversions.PaToInchesWater(result.PressureLossPa), .199, 1e-12);
}

static void TestVavPerformanceSelection()
{
    var exactReheat = VavPerformanceCatalog.SelectWithReheat(1000, "14", 1);
    AssertNear(exactReheat.PressureDropInWg, .06, 1e-12);
    if (exactReheat.WasInterpolated)
        throw new Exception("An exact Titus DESV hot-water coil table point should not be interpolated.");

    var interpolatedReheat = VavPerformanceCatalog.SelectWithReheat(850, "14", 1);
    AssertNear(interpolatedReheat.PressureDropInWg, .045, 1e-12);
    if (!interpolatedReheat.WasInterpolated
        || interpolatedReheat.LowerPoint.AirflowCfm != 700
        || interpolatedReheat.UpperPoint.AirflowCfm != 1000)
        throw new Exception("Titus DESV reheat interpolation did not retain its published bracket.");

    var exactCooling = VavPerformanceCatalog.SelectWithoutReheat(400, "6");
    AssertNear(exactCooling.PressureDropInWg, .13, 1e-12);
    var interpolatedCooling = VavPerformanceCatalog.SelectWithoutReheat(375, "6");
    AssertNear(interpolatedCooling.PressureDropInWg, .115, 1e-12);

    var result = VavPerformanceCatalog.CalculateFittingResult(
        FittingCatalog.Find(VavPerformanceCatalog.WithReheatCode),
        new Dictionary<string, double> { ["sizeIndex"] = 8, ["rows"] = 1 },
        UnitConversions.CfmToM3PerS(850), UnitConversions.FahrenheitToCelsius(70));
    AssertNear(UnitConversions.PaToInchesWater(result.PressureLossPa), .045, 1e-12);

    var rejected = false;
    try { _ = VavPerformanceCatalog.SelectWithReheat(100, "14", 1); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    if (!rejected)
        throw new Exception("VAV lookup must reject airflow outside the published range instead of extrapolating.");

    rejected = false;
    try { _ = VavPerformanceCatalog.SelectWithReheat(200, "6", 1); }
    catch (ArgumentException) { rejected = true; }
    if (!rejected)
        throw new Exception("VAV lookup must reject unsupported size and coil-row combinations.");
}

static void TestFittingCalculation()
{
    var elbow = FittingCalculator.Calculate(
        FittingCatalog.Find("3-1"),
        new Dictionary<string, double> { ["rd"] = 1.5, ["angle"] = 90 },
        UnitConversions.CfmToM3PerS(1000),
        GeometryCalculator.Round(UnitConversions.InchesToM(12)),
        UnitConversions.FahrenheitToCelsius(70));
    AssertNear(elbow.Coefficient, .15, 1e-12);
    AssertNear(UnitConversions.MpsToFpm(elbow.ReferenceVelocityMPerS), 4000.0 / Math.PI, 1e-6);
    AssertNear(elbow.PressureLossPa, elbow.Coefficient * elbow.VelocityPressurePa, 1e-12);

    var plate = FittingCatalog.Find("6-8").CalculateCoefficient(
        new Dictionary<string, double> { ["td"] = .2, ["free"] = .5 });
    AssertNear(plate, 3.8, 1e-12);

    var gate = FittingCatalog.Find("6-3").CalculateCoefficient(
        new Dictionary<string, double> { ["hd"] = .8 });
    AssertNear(gate, .17, 1e-12);

    var parallelDamper = FittingCatalog.Find("6-5").CalculateCoefficient(
        new Dictionary<string, double> { ["lr"] = .3, ["angle"] = 80 });
    AssertNear(parallelDamper, 116, 1e-12);

    AssertNear(FittingCatalog.Find("2-11").CalculateCoefficient(
        new Dictionary<string, double> { ["ld"] = .3 }), .8, 1e-12);
    AssertNear(FittingCatalog.Find("3-5").CalculateCoefficient(
        new Dictionary<string, double> { ["rw"] = .5, ["hw"] = .25, ["angle"] = 90, ["re"] = 140_000 }), 1.3, 1e-12);
    AssertNear(FittingCatalog.Find("6-1").CalculateCoefficient(
        new Dictionary<string, double> { ["dd"] = 1, ["angle"] = 60 }), 113, 1e-12);
    AssertNear(FittingCatalog.Find("6-2").CalculateCoefficient(
        new Dictionary<string, double> { ["hw"] = 1, ["angle"] = 70 }), 210, 1e-12);
    AssertNear(FittingCatalog.Find("6-7").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 1, ["free"] = .7 }), .58, 1e-12);
    AssertNear(FittingCatalog.Find("6-9").CalculateCoefficient(
        new Dictionary<string, double> { ["re"] = 100, ["blocked"] = .10, ["offset"] = 0 }), .21, 1e-12);
    AssertNear(FittingCatalog.Find("6-11").CalculateCoefficient(
        new Dictionary<string, double> { ["wh"] = 4, ["lh"] = .25 }), .22, 1e-12);
    AssertNear(FittingCatalog.Find("7-11").CalculateCoefficient(
        new Dictionary<string, double> { ["angle"] = 20, ["area"] = 2.5 }), .13, 1e-12);
    AssertNear(FittingCatalog.Find("7-15").CalculateCoefficient(
        new Dictionary<string, double> { ["angle"] = 30, ["area"] = 4 }), .69, 1e-12);

    AssertNear(FittingCatalog.Find("1-8").CalculateCoefficient(
        new Dictionary<string, double> { ["re"] = 20_000, ["area"] = .5 }), 4.9, 1e-12);
    AssertNear(FittingCatalog.Find("2-2").CalculateCoefficient(
        new Dictionary<string, double> { ["shape"] = 0, ["aspectBand"] = 1, ["angle"] = 60, ["velocityRatio"] = .5 }), .9, 1e-12);
    AssertNear(FittingCatalog.Find("2-3").CalculateCoefficient(new Dictionary<string, double>()), .88, 1e-12);
    AssertNear(FittingCatalog.Find("2-4").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 10, ["angle"] = 30 }), .68, 1e-12);
    AssertNear(FittingCatalog.Find("2-5").CalculateCoefficient(
        new Dictionary<string, double> { ["ld"] = 6 }), .14, 1e-12);
    AssertNear(FittingCatalog.Find("2-6").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 4, ["angle"] = 45 }), .91, 1e-12);
    AssertNear(FittingCatalog.Find("2-7").CalculateCoefficient(
        new Dictionary<string, double> { ["lh"] = 8 }), .22, 1e-12);
    AssertNear(FittingCatalog.Find("2-8").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 6, ["angle"] = 20 }), .77, 1e-12);
    AssertNear(FittingCatalog.Find("2-9").CalculateCoefficient(
        new Dictionary<string, double> { ["ld"] = 10 }), .16, 1e-12);
    AssertNear(FittingCatalog.Find("2-10").CalculateCoefficient(
        new Dictionary<string, double> { ["shape"] = 1, ["rw"] = 1, ["lw"] = 4, ["ld"] = 1 }), 1.2, 1e-12);
    AssertNear(FittingCatalog.Find("2-12").CalculateCoefficient(
        new Dictionary<string, double> { ["dd"] = .7 }), 4.4, 1e-12);
    AssertNear(FittingCatalog.Find("3-7").CalculateCoefficient(
        new Dictionary<string, double> { ["vanes"] = 1, ["rw"] = .1, ["hw"] = 1, ["angle"] = 90 }), .25, 1e-12);
    AssertNear(FittingCatalog.Find("3-8").CalculateCoefficient(
        new Dictionary<string, double> { ["design"] = 2 }), .15, 1e-12);
    AssertNear(FittingCatalog.Find("3-9").CalculateCoefficient(
        new Dictionary<string, double> { ["design"] = 4, ["velocity"] = 4_000 }), .16, 1e-12);
    AssertNear(FittingCatalog.Find("3-11").CalculateCoefficient(
        new Dictionary<string, double> { ["lh"] = 1, ["wh"] = 1, ["re"] = 140_000 }), 2.6, 1e-12);
    AssertNear(FittingCatalog.Find("3-12").CalculateCoefficient(
        new Dictionary<string, double> { ["lw"] = .6, ["hw"] = 1, ["re"] = 140_000 }), 2.9, 1e-12);
    AssertNear(FittingCatalog.Find("3-13").CalculateCoefficient(
        new Dictionary<string, double> { ["base"] = .5, ["angle"] = 45, ["ld"] = 4 }), .6, 1e-12);
    AssertNear(FittingCatalog.Find("3-14").CalculateCoefficient(
        new Dictionary<string, double> { ["base"] = .5, ["angle"] = 60, ["ld"] = 8 }), .625, 1e-12);
    AssertNear(FittingCatalog.Find("3-15").CalculateCoefficient(
        new Dictionary<string, double> { ["velocity"] = 1_600 }), .24, 1e-12);
    AssertNear(FittingCatalog.Find("4-1").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = .25, ["angle"] = 45 }), .60, 1e-12);
    AssertNear(FittingCatalog.Find("4-2").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 4, ["angle"] = 120 }), 4.3, 1e-12);
    AssertNear(FittingCatalog.Find("4-3").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = .5, ["angle"] = 90 }), .38, 1e-12);
    AssertNear(FittingCatalog.Find("4-4").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 2, ["angle"] = 120 }), .74, 1e-12);
    AssertNear(FittingCatalog.Find("4-5").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = 6, ["angle"] = 60 }), .78, 1e-12);
    AssertNear(FittingCatalog.Find("4-6").CalculateCoefficient(
        new Dictionary<string, double> { ["direction"] = 0, ["re"] = 100_000, ["ld"] = 3, ["b"] = 10 }), .30, 1e-12);
    AssertNear(FittingCatalog.Find("4-6").CalculateCoefficient(
        new Dictionary<string, double> { ["direction"] = 1, ["re"] = 200_000, ["ld"] = 4, ["b"] = 50 }), .09, 1e-12);
    AssertNear(FittingCatalog.Find("4-7").CalculateCoefficient(
        new Dictionary<string, double> { ["area"] = .5, ["lh"] = .1, ["angle"] = 60 }), .11, 1e-12);
    AssertNear(FittingCatalog.Find("7-1").CalculateCoefficient(
        new Dictionary<string, double> { ["blast"] = .6, ["length"] = .25 }), .33, 1e-12);
    AssertNear(FittingCatalog.Find("7-2").CalculateCoefficient(
        new Dictionary<string, double> { ["blast"] = .8, ["position"] = 1, ["length"] = .25 }), .52, 1e-12);
    AssertNear(FittingCatalog.Find("7-3").CalculateCoefficient(
        new Dictionary<string, double> { ["blast"] = .4, ["position"] = 3, ["length"] = .12 }), 4.1, 1e-12);
    AssertNear(FittingCatalog.Find("7-4").CalculateCoefficient(
        new Dictionary<string, double> { ["rd"] = 1.5, ["ld"] = 2 }), .60, 1e-12);
    AssertNear(FittingCatalog.Find("7-5").CalculateCoefficient(
        new Dictionary<string, double> { ["type"] = 1, ["rd"] = .75, ["ld"] = 5 }), .47, 1e-12);
    AssertNear(FittingCatalog.Find("7-6").CalculateCoefficient(
        new Dictionary<string, double> { ["type"] = 1, ["rd"] = 2, ["ld"] = 0 }), 1.26, 1e-12);
    AssertNear(FittingCatalog.Find("7-7").CalculateCoefficient(
        new Dictionary<string, double> { ["ld"] = .4 }), .53, 1e-12);
    AssertNear(FittingCatalog.Find("7-8").CalculateCoefficient(
        new Dictionary<string, double> { ["blast"] = .8 }), .47, 1e-12);
}

static void TestJunctionCalculation()
{
    var common = GeometryCalculator.Round(UnitConversions.InchesToM(12));
    var straight = GeometryCalculator.Round(UnitConversions.InchesToM(12));
    var branch = GeometryCalculator.Round(UnitConversions.InchesToM(12 * Math.Sqrt(.6)));
    var commonFlow = UnitConversions.CfmToM3PerS(1_000);
    var branchFlow = UnitConversions.CfmToM3PerS(400);
    var straightFlow = UnitConversions.CfmToM3PerS(600);

    var branchResult = FittingCalculator.Calculate(FittingCatalog.Find("5-1"),
        new Dictionary<string, double>(),
        new JunctionFlowData(JunctionPath.Branch, commonFlow, common, straightFlow, straight, branchFlow, branch),
        UnitConversions.FahrenheitToCelsius(70));
    AssertNear(branchResult.Coefficient, .19, 1e-10);
    AssertNear(branchResult.PressureLossPa, .19 * branchResult.VelocityPressurePa, 1e-12);

    var straightResult = FittingCalculator.Calculate(FittingCatalog.Find("5-1"),
        new Dictionary<string, double>(),
        new JunctionFlowData(JunctionPath.Straight, commonFlow, common, straightFlow, straight, branchFlow, branch),
        UnitConversions.FahrenheitToCelsius(70));
    AssertNear(straightResult.Coefficient, .17, 1e-10);

    var negativeBranch = GeometryCalculator.Round(UnitConversions.InchesToM(12 * Math.Sqrt(.1)));
    var signedResult = FittingCalculator.Calculate(FittingCatalog.Find("5-1"),
        new Dictionary<string, double>(),
        new JunctionFlowData(JunctionPath.Straight, commonFlow, common,
            UnitConversions.CfmToM3PerS(200), straight, UnitConversions.CfmToM3PerS(800), negativeBranch),
        UnitConversions.FahrenheitToCelsius(70));
    AssertNear(signedResult.Coefficient, -10, 1e-10);
    if (signedResult.PressureLossPa >= 0)
        throw new Exception("Published negative junction coefficients must remain signed.");

    var angleInput = FittingCatalog.Find("5-24").Inputs.Single(x => x.Key == "angle");
    if (angleInput.Choices?.Count != 5)
        throw new Exception("Fitting 5-24 must expose its five published branch angles as a dropdown.");
    var angleResult = FittingCalculator.Calculate(FittingCatalog.Find("5-24"),
        new Dictionary<string, double> { ["angle"] = 45 },
        new JunctionFlowData(JunctionPath.Branch, commonFlow, common, straightFlow, straight,
            branchFlow, common), UnitConversions.FahrenheitToCelsius(70));
    AssertNear(angleResult.Coefficient, .54, 1e-10);

    AssertNear(FittingCatalog.Find("5-6").CalculateCoefficient(new Dictionary<string, double>
        { ["configuration"] = 0, ["path"] = 0, ["qbqc"] = .1, ["qbqs"] = .1 }), -1.2, 1e-12);
    AssertNear(FittingCatalog.Find("5-7").CalculateCoefficient(new Dictionary<string, double>
        { ["path"] = 0, ["vcfpm"] = 1_000, ["qbqc"] = .5, ["qsqc"] = .5 }), .78, 1e-12);
    AssertNear(FittingCatalog.Find("5-25").CalculateCoefficient(new Dictionary<string, double>
        { ["path"] = 0, ["vbvc"] = 1, ["qbqs"] = .5, ["vsvc"] = .5 }), 1.26, 1e-12);
    AssertNear(FittingCatalog.Find("5-31").CalculateCoefficient(new Dictionary<string, double>
        { ["path"] = 1, ["vbvc"] = 1, ["qbqc"] = .5 }), .13, 1e-12);
    AssertNear(FittingCatalog.Find("5-33").CalculateCoefficient(new Dictionary<string, double>
        { ["direction"] = 0, ["angle"] = 30, ["path"] = 0, ["qbqc"] = .5, ["vbvc"] = 1 }), .28, 1e-12);
    AssertNear(FittingCatalog.Find("5-34").CalculateCoefficient(new Dictionary<string, double>
        { ["direction"] = 0, ["branchArea"] = .4, ["secondFlow"] = 1, ["path"] = 0, ["qbqc"] = .3, ["qsqc"] = .7 }), .55, 1e-12);
    AssertNear(FittingCatalog.Find("5-35").CalculateCoefficient(new Dictionary<string, double>
        { ["direction"] = 0, ["branchArea"] = .4, ["secondFlow"] = 1, ["path"] = 1, ["qbqc"] = .3, ["qsqc"] = .7 }), .72, 1e-12);
}

static void TestDuctSystemPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), "DuctSizer-tests", Guid.NewGuid().ToString("N"));
    var path = Path.Combine(directory, "duct-system.json");
    try
    {
        var result = DuctCalculator.Calculate(new DuctCalculationInput(
            UnitConversions.CfmToM3PerS(1_000.0), 20.0, 0.50, 101_325.0,
            GeometryCalculator.Round(UnitConversions.InchesToM(16.0)),
            DuctMaterialCatalog.All[0], EquivalentDiameterMethod.Huebscher,
            PressureLossBasis.EquivalentRoundArea, UnitConversions.FeetToM(100.0)));
        var document = new DuctSystemDocument
        {
            UserName = "Alex Designer",
            ProjectName = "North Clinic Renovation",
            ProjectNumber = "M-24017",
            ProjectDate = "2026-09-25",
            DefaultSizingIncrementM = UnitConversions.InchesToM(0.5),
            DefaultSizeRoundingDirection = SizeRoundingDirection.Down,
            DefaultEquivalentDiameterMethod = EquivalentDiameterMethod.HydraulicDiameter,
            DefaultPressureLossBasis = PressureLossBasis.ActualDuctArea,
            LayoutShowToScale = true,
            LayoutFeetPerGridCell = 2.5,
            LayoutViewMode = LayoutViewMode.IsometricNegative,
            LayoutPdfBackground = new LayoutPdfBackground
            {
                SourceFileName = "floor-plan.pdf", PageNumber = 2, PageCount = 5,
                PageWidthPoints = 2592, PageHeightPoints = 1728,
                PagePngBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
                PaperInches = 0.25, DrawingFeet = 1,
                CenterXFeet = 12, CenterYFeet = -8, CenterZFeet = 0,
                Visible = false
            }
        };
        document.AutomaticSizing.Duct.Mode = CalculationMode.SizeByMaximumVelocity;
        document.AutomaticSizing.Duct.MaximumVelocityMPerS = UnitConversions.FpmToMps(850);
        document.AutomaticSizing.Duct.Shape = DuctShape.Rectangular;
        document.AutomaticSizing.Duct.HeightM = UnitConversions.InchesToM(10);
        document.AutomaticSizing.Duct.MaterialName = "Galvanized Rectangular Duct";
        document.AutomaticSizing.Coil.PressureDropPa = UnitConversions.InchesWaterToPa(0.3);
        document.AutomaticSizing.Grd.GrdType = "Grille";
        document.AutomaticSizing.Grd.NominalDuctSizeIn = "12x12";
        document.AutomaticSizing.Fitting("Elbows").FittingCode = "3-1";
        document.AutomaticSizing.Fitting("Elbows").Inputs["rd"] = 1.5;
        var sequence = document.ReserveSequence();
        document.Ducts.Add(SavedDuct.FromResult(sequence, "Main supply", result, "Supply Air", "Main"));
        var fittingResult = FittingCalculator.Calculate(
            FittingCatalog.Find("2-1"), new Dictionary<string, double>(),
            UnitConversions.CfmToM3PerS(1000), GeometryCalculator.Round(UnitConversions.InchesToM(12)), 20.0);
        var savedExit = SavedFitting.FromResult(document.ReserveFittingSequence(), "Abrupt exit", fittingResult,
            "Supply Air", "Main");
        savedExit.CustomId = "FIT-EXH-1";
        document.Fittings.Add(savedExit);
        var junctionResult = FittingCalculator.Calculate(
            FittingCatalog.Find("5-24"), new Dictionary<string, double> { ["angle"] = 45 },
            new JunctionFlowData(JunctionPath.Branch,
                UnitConversions.CfmToM3PerS(1000), GeometryCalculator.Round(UnitConversions.InchesToM(12)),
                UnitConversions.CfmToM3PerS(600), GeometryCalculator.Round(UnitConversions.InchesToM(12)),
                UnitConversions.CfmToM3PerS(400), GeometryCalculator.Round(UnitConversions.InchesToM(12))), 20.0);
        var savedWye = SavedFitting.FromResult(document.ReserveFittingSequence(), "Diverging wye", junctionResult);
        savedWye.CustomId = "FIT-EXH-1";
        document.Fittings.Add(savedWye);
        var fanNode = new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan, GridX = 4, GridY = 12,
            AirSystemType = "Supply Air", BranchId = "Main", AirflowM3PerS = UnitConversions.CfmToM3PerS(1000),
            TemperatureC = UnitConversions.FahrenheitToCelsius(70),
            FanStaticPressurePa = UnitConversions.InchesWaterToPa(1.75), FanEfficiencyPercent = 67.5
        };
        document.LayoutNodes.Add(fanNode);
        var coilNode = new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Coil, GridX = 10, GridY = 12,
            ParentNodeId = fanNode.Id, AirSystemType = "Supply Air", BranchId = "Main",
            AirflowM3PerS = fanNode.AirflowM3PerS, TemperatureC = UnitConversions.FahrenheitToCelsius(55),
            PressureDropPa = UnitConversions.InchesWaterToPa(.25), ConnectionDuctSequence = sequence
        };
        coilNode.HasAbsoluteCoordinateAnchor = true;
        coilNode.AbsoluteAnchorXM = UnitConversions.FeetToM(18);
        coilNode.AbsoluteAnchorYM = UnitConversions.FeetToM(-4);
        coilNode.AbsoluteAnchorZM = UnitConversions.FeetToM(7);
        document.LayoutNodes.Add(coilNode);
        document.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Filter, GridX = 16, GridY = 12,
            ParentNodeId = coilNode.Id, AirSystemType = "Supply Air", BranchId = "Main",
            AirflowM3PerS = fanNode.AirflowM3PerS, PressureDropPa = UnitConversions.InchesWaterToPa(.10),
            BranchOutletBelow = true
        });
        document.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir, GridX = 2, GridY = 28,
            SpaceRole = "Return Pickup", AirSystemType = "Return Air", BranchId = "Main",
            AirflowM3PerS = UnitConversions.CfmToM3PerS(1000), TemperatureC = UnitConversions.FahrenheitToCelsius(74),
            PressurePa = UnitConversions.InchesWaterToPa(-0.05)
        });
        document.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.CalculationPoint, GridX = 8, GridY = 28,
            CalculationPointType = "Velocity", ParentNodeId = document.LayoutNodes[3].Id,
            AirSystemType = "Return Air", BranchId = "Main", VelocityMPerS = 6.25,
            AirflowM3PerS = UnitConversions.CfmToM3PerS(1000), AirflowOverride = true
        });

        DuctSystemStore.Save(path, document);
        var restored = DuctSystemStore.Load(path);
        var restoredResult = restored.Ducts.Single().Calculate();

        if (restored.Ducts.Single().Id != "D-001" || restored.Ducts.Single().Description != "Main supply")
            throw new Exception("Saved duct identity was not retained.");
        AssertNear(restoredResult.TotalPressureLossPa, result.TotalPressureLossPa, 1e-12);
        AssertNear(restored.Fittings[0].Calculate().PressureLossPa, fittingResult.PressureLossPa, 1e-12);
        AssertNear(restored.Fittings[1].Calculate().PressureLossPa, junctionResult.PressureLossPa, 1e-12);
        if (restored.Fittings[0].Id != "FIT-EXH-1" || restored.Fittings[1].Id != "FIT-EXH-1")
            throw new Exception("Custom and intentionally duplicated fitting IDs were not retained.");
        if (restored.Fittings[1].Junction?.Path != JunctionPath.Branch)
            throw new Exception("Saved junction path and geometry were not retained.");
        if (restored.LayoutNodes.Count != 5 || restored.LayoutNodes[1].ParentNodeId != restored.LayoutNodes[0].Id
            || restored.LayoutNodes[1].BranchId != "Main")
            throw new Exception("Layout nodes, connections, and branch metadata were not retained.");
        if (restored.LayoutNodes[0].Id != "Fan-001" || restored.LayoutNodes[1].Id != "C-002"
            || restored.LayoutNodes[3].Id != "L-004" || restored.LayoutNodes[4].Id != "S-005"
            || new SavedLayoutNode { Sequence = 6, Type = LayoutNodeType.Fitting, FittingGroup = "Elbows" }.Id != "F-006"
            || new SavedLayoutNode { Sequence = 7, Type = LayoutNodeType.Grd }.Id != "T-007"
            || new SavedLayoutNode { Sequence = 8, Type = LayoutNodeType.Fitting, FittingGroup = "Terminals" }.Id != "T-008"
            || new SavedLayoutNode { Sequence = 9, Type = LayoutNodeType.Fitting,
                FittingGroup = FittingCatalog.TerminalSelectionGroup }.Id != "T-009")
            throw new Exception("Designer IDs were not assigned by component type.");
        AssertNear(restored.LayoutNodes[1].PressureDropPa, UnitConversions.InchesWaterToPa(.25), 1e-12);
        AssertNear(restored.LayoutNodes[0].FanStaticPressurePa, UnitConversions.InchesWaterToPa(1.75), 1e-12);
        AssertNear(restored.LayoutNodes[0].FanEfficiencyPercent, 67.5, 1e-12);
        if (restored.LayoutNodes[1].ConnectionDuctSequence != sequence || restored.LayoutNodes[2].Type != LayoutNodeType.Filter
            || !restored.LayoutNodes[2].BranchOutletBelow)
            throw new Exception("Sized connections and filter components were not retained.");
        if (restored.LayoutNodes[3].Type != LayoutNodeType.Reservoir
            || restored.LayoutNodes[3].SpaceRole != "Space Source"
            || restored.LayoutNodes[3].AirSystemType != "Return Air")
            throw new Exception("Legacy return-pickup spaces should migrate to Space Source while retaining return-air metadata.");
        AssertNear(restored.LayoutNodes[3].PressurePa, UnitConversions.InchesWaterToPa(-0.05), 1e-12);
        if (restored.LayoutNodes[4].Type != LayoutNodeType.CalculationPoint
            || restored.LayoutNodes[4].CalculationPointType != "Velocity"
            || !restored.LayoutNodes[4].AirflowOverride)
            throw new Exception("Calculation-point type and airflow-override state were not retained.");
        AssertNear(restored.LayoutNodes[4].VelocityMPerS, 6.25, 1e-12);
        if (restored.UserName != "Alex Designer" || restored.ProjectName != "North Clinic Renovation"
            || restored.ProjectNumber != "M-24017" || restored.ProjectDate != "2026-09-25")
            throw new Exception("Project settings were not retained in local storage.");
        AssertNear(restored.DefaultSizingIncrementM, UnitConversions.InchesToM(0.5), 1e-12);
        if (restored.DefaultSizeRoundingDirection != SizeRoundingDirection.Down
            || restored.DefaultEquivalentDiameterMethod != EquivalentDiameterMethod.HydraulicDiameter
            || restored.DefaultPressureLossBasis != PressureLossBasis.ActualDuctArea)
            throw new Exception("Project calculation defaults were not retained in local storage.");
        if (!restored.LayoutShowToScale || Math.Abs(restored.LayoutFeetPerGridCell - 2.5) > 1e-12
            || restored.LayoutViewMode != LayoutViewMode.IsometricNegative)
            throw new Exception("The always-scaled Designer feet-per-grid-cell value was not retained in local storage.");
        if (restored.LayoutPdfBackground is not { SourceFileName: "floor-plan.pdf", PageNumber: 2,
                PageCount: 5, Visible: false } restoredPdf
            || restoredPdf.PagePngBase64 != Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
            || Math.Abs(restoredPdf.PaperInches - 0.25) > 1e-12
            || Math.Abs(restoredPdf.DrawingFeet - 1) > 1e-12
            || Math.Abs(restoredPdf.CenterXFeet - 12) > 1e-12
            || Math.Abs(restoredPdf.CenterYFeet + 8) > 1e-12)
            throw new Exception("The selected PDF page, custom scale, visibility, and absolute center were not retained.");
        if (!restored.LayoutNodes[1].HasAbsoluteCoordinateAnchor
            || Math.Abs(restored.LayoutNodes[1].AbsoluteAnchorXM - UnitConversions.FeetToM(18)) > 1e-12
            || Math.Abs(restored.LayoutNodes[1].AbsoluteAnchorYM - UnitConversions.FeetToM(-4)) > 1e-12
            || Math.Abs(restored.LayoutNodes[1].AbsoluteAnchorZM - UnitConversions.FeetToM(7)) > 1e-12)
            throw new Exception("Detached-system absolute coordinate anchors were not retained in local storage.");
        if (restored.AutomaticSizing.Duct.Mode != CalculationMode.SizeByMaximumVelocity
            || restored.AutomaticSizing.Duct.Shape != DuctShape.Rectangular
            || restored.AutomaticSizing.Duct.MaterialName != "Galvanized Rectangular Duct"
            || restored.AutomaticSizing.Grd.GrdType != "Grille"
            || restored.AutomaticSizing.Grd.NominalDuctSizeIn != "12x12"
            || restored.AutomaticSizing.Fitting("Elbows").FittingCode != "3-1")
            throw new Exception("Automatic duct and component sizing defaults were not retained in local storage.");
        AssertNear(restored.AutomaticSizing.Duct.MaximumVelocityMPerS, UnitConversions.FpmToMps(850), 1e-12);
        AssertNear(restored.AutomaticSizing.Duct.HeightM, UnitConversions.InchesToM(10), 1e-12);
        AssertNear(restored.AutomaticSizing.Coil.PressureDropPa, UnitConversions.InchesWaterToPa(0.3), 1e-12);
        if (restored.ReserveSequence() != 2)
            throw new Exception("Automatic duct numbering was not retained.");

        var legacyPointPath = Path.Combine(directory, "legacy-calculation-point.json");
        var legacyPointDocument = new DuctSystemDocument { SchemaVersion = 3 };
        var legacySource = new SavedLayoutNode
        {
            Sequence = legacyPointDocument.ReserveLayoutSequence(), Type = LayoutNodeType.Fan,
            GridX = 0, GridY = 0, AirflowM3PerS = result.Input.AirflowM3PerS
        };
        var incomingDuct = SavedDuct.FromResult(legacyPointDocument.ReserveSequence(), "Before point", result);
        incomingDuct.LengthM = UnitConversions.FeetToM(20);
        legacyPointDocument.Ducts.Add(incomingDuct);
        var legacyPoint = new SavedLayoutNode
        {
            Sequence = legacyPointDocument.ReserveLayoutSequence(), Type = LayoutNodeType.CalculationPoint,
            ParentNodeId = "L-001", ConnectionDuctSequence = incomingDuct.Sequence,
            GridX = 4, GridY = 4, DeltaXM = UnitConversions.FeetToM(20),
            CalculationPointType = "Pressure"
        };
        var outgoingDuct = SavedDuct.FromResult(legacyPointDocument.ReserveSequence(), "After point", result);
        outgoingDuct.LengthM = UnitConversions.FeetToM(30);
        legacyPointDocument.Ducts.Add(outgoingDuct);
        var legacySink = new SavedLayoutNode
        {
            Sequence = legacyPointDocument.ReserveLayoutSequence(), Type = LayoutNodeType.Filter,
            ParentNodeId = "L-002", ConnectionDuctSequence = outgoingDuct.Sequence,
            GridX = 10, GridY = 4, DeltaXM = UnitConversions.FeetToM(30)
        };
        legacyPointDocument.LayoutNodes.AddRange(new[] { legacySource, legacyPoint, legacySink });
        DuctSystemStore.Save(legacyPointPath, legacyPointDocument);
        var migratedPointDocument = DuctSystemStore.Load(legacyPointPath);
        var migratedPoint = migratedPointDocument.LayoutNodes.Single(x => x.Type == LayoutNodeType.CalculationPoint);
        var migratedSink = migratedPointDocument.LayoutNodes.Single(x => x.Type == LayoutNodeType.Filter);
        if (migratedPointDocument.SchemaVersion != DuctSystemDocument.CurrentSchemaVersion || migratedPointDocument.Ducts.Count != 1
            || migratedPoint.ParentNodeId.Length != 0 || migratedPoint.CalculationConnectionNodeId != migratedSink.Id
            || migratedSink.ParentNodeId != legacySource.Id || migratedPoint.ObservedDuctSequence is null)
            throw new Exception("Legacy calculation points were not converted to non-destructive duct annotations.");
        AssertNear(migratedPoint.CalculationConnectionFraction, 0.4, 1e-12);
        AssertNear(migratedPointDocument.Ducts.Single().LengthM, UnitConversions.FeetToM(50), 1e-12);

        var legacyPath = Path.Combine(directory, "legacy-layout.json");
        var legacyDocument = new DuctSystemDocument { SchemaVersion = 1 };
        legacyDocument.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = legacyDocument.ReserveLayoutSequence(), Type = LayoutNodeType.Fan, GridX = 2, GridY = 3
        });
        DuctSystemStore.Save(legacyPath, legacyDocument);
        var upgradedLayout = DuctSystemStore.Load(legacyPath);
        if (upgradedLayout.SchemaVersion != DuctSystemDocument.CurrentSchemaVersion || upgradedLayout.LayoutNodes[0].GridX != 4
            || upgradedLayout.LayoutNodes[0].GridY != 12 || !upgradedLayout.LayoutNodes[0].IsDefined)
            throw new Exception("Legacy Designer coordinates were not upgraded to the full isometric intersection lattice.");

        var versionEightPath = Path.Combine(directory, "version-eight-fan-id.json");
        var versionEight = new DuctSystemDocument { SchemaVersion = 8, CoordinateOriginNodeId = "C-001" };
        versionEight.LayoutNodes.Add(new SavedLayoutNode { Sequence = 1, Type = LayoutNodeType.Fan });
        versionEight.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = 2, Type = LayoutNodeType.Coil, ParentNodeId = "C-001"
        });
        // Schema 14 stores the observed connection only on calculation points.
        versionEight.LayoutNodes.Add(new SavedLayoutNode
        {
            Sequence = 3, Type = LayoutNodeType.CalculationPoint, CalculationConnectionNodeId = "C-001"
        });
        DuctSystemStore.Save(versionEightPath, versionEight);
        var migratedFanId = DuctSystemStore.Load(versionEightPath);
        if (migratedFanId.CoordinateOriginNodeId != "Fan-001"
            || migratedFanId.LayoutNodes[1].ParentNodeId != "Fan-001"
            || migratedFanId.LayoutNodes[2].CalculationConnectionNodeId != "Fan-001")
            throw new Exception("Version-8 fan references were not migrated to Fan-### identifiers.");

        restored.Fittings[0].FittingCode = "A7-F";
        restored.Fittings[0].Inputs = new Dictionary<string, double>
        {
            ["rw"] = 1, ["hw"] = 1, ["angle"] = 90
        };
        DuctSystemStore.Save(path, restored);
        var migrated = DuctSystemStore.Load(path).Fittings[0];
        if (migrated.FittingCode != "3-5" || !migrated.Inputs.ContainsKey("re"))
            throw new Exception("Compatible saved fitting codes were not upgraded to ASHRAE numbering.");
        _ = migrated.Calculate();
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

static void TestDuctSystemExchange()
{
    var directory = Path.Combine(Path.GetTempPath(), "DuctSizer-exchange-tests", Guid.NewGuid().ToString("N"));
    var csvPath = Path.Combine(directory, "system.csv");
    var pdfPath = Path.Combine(directory, "system.pdf");
    try
    {
        var document = new DuctSystemDocument
        {
            UserName = "Alex Designer",
            ProjectName = "North Clinic Renovation",
            ProjectNumber = "M-24017",
            ProjectDate = "2026-09-25"
        };
        var ductResult = DuctCalculator.Calculate(new DuctCalculationInput(
            UnitConversions.CfmToM3PerS(1_250), 20, .50, 101_325,
            GeometryCalculator.Rectangular(UnitConversions.InchesToM(20), UnitConversions.InchesToM(12)),
            DuctMaterialCatalog.All[0], EquivalentDiameterMethod.Huebscher,
            PressureLossBasis.EquivalentRoundArea, UnitConversions.FeetToM(85)));
        var exportedDuct = SavedDuct.FromResult(document.ReserveSequence(), "Main, \"north\"", ductResult,
            "Return Air", "B-002");
        exportedDuct.DeltaXM = UnitConversions.FeetToM(60);
        exportedDuct.DeltaYM = UnitConversions.FeetToM(-20);
        exportedDuct.DeltaZM = UnitConversions.FeetToM(5);
        document.Ducts.Add(exportedDuct);
        var fittingResult = FittingCalculator.Calculate(FittingCatalog.Find("2-1"),
            new Dictionary<string, double>(), UnitConversions.CfmToM3PerS(1_250),
            GeometryCalculator.Round(UnitConversions.InchesToM(14)), 20);
        var exportedFitting = SavedFitting.FromResult(document.ReserveFittingSequence(), "Roof\nexit", fittingResult,
            "Return Air", "B-002");
        exportedFitting.CustomId = "FIT-ROOF-A";
        exportedFitting.DeltaXM = UnitConversions.FeetToM(8);
        exportedFitting.DeltaYM = UnitConversions.FeetToM(2);
        exportedFitting.DeltaZM = UnitConversions.FeetToM(-1);
        document.Fittings.Add(exportedFitting);

        DuctSystemCsv.Export(csvPath, document);
        var imported = DuctSystemCsv.Import(csvPath);
        if (imported.Ducts.Count != 1 || imported.Fittings.Count != 1)
            throw new Exception("CSV import did not restore both system item types.");
        if (imported.Ducts[0].Description != "Main, \"north\"" || imported.Fittings[0].Description != "Roof\nexit")
            throw new Exception("CSV quoting did not preserve descriptions.");
        if (imported.Fittings[0].Id != "FIT-ROOF-A")
            throw new Exception("CSV round trip did not preserve the custom fitting ID.");
        if (imported.Ducts[0].AirSystemType != "Return Air" || imported.Ducts[0].BranchId != "B-002"
            || imported.Fittings[0].AirSystemType != "Return Air" || imported.Fittings[0].BranchId != "B-002")
            throw new Exception("CSV round trip did not preserve air-system and branch metadata.");
        AssertNear(imported.Ducts[0].DeltaXM, exportedDuct.DeltaXM, 1e-12);
        AssertNear(imported.Ducts[0].DeltaYM, exportedDuct.DeltaYM, 1e-12);
        AssertNear(imported.Ducts[0].DeltaZM, exportedDuct.DeltaZM, 1e-12);
        AssertNear(imported.Fittings[0].DeltaXM, exportedFitting.DeltaXM, 1e-12);
        AssertNear(imported.Ducts[0].Calculate().TotalPressureLossUpperPa, ductResult.TotalPressureLossUpperPa, 1e-12);
        AssertNear(imported.Fittings[0].Calculate().PressureLossPa, fittingResult.PressureLossPa, 1e-12);

        var headers = new[]
        {
            "Item ID", "Description", "Air System", "Branch ID", "Upstream Item", "Downstream Item",
            "Delta X (ft)", "Delta Y (ft)", "Delta Z (ft)", "Airflow (cfm)", "Component Type", "Size / Fitting", "Length (ft)",
            "Velocity (fpm)", "Pressure Rate (in. w.c./100 ft)", "Static Pressure Drop (in. w.c.)",
            "Static Pressure In (in. w.c.)", "Static Pressure Out (in. w.c.)"
        };
        var rows = Enumerable.Range(1, 25).Select(index => (IReadOnlyList<string>)new[]
        {
            $"D-{index:000}", $"Supply duct {index}", "Supply Air", "Main", $"L-{index:000}", $"L-{index + 1:000}",
            "20", "0", "4", "1250", "Rectangular", "20 x 12 in", "85", "750", ".08", ".068", "1.75", "1.682"
        }).ToArray();
        var reportItems = new[]
        {
            new DuctSystemPdfReportItem("D-001", "Main supply duct", "STRAIGHT DUCT",
                new[]
                {
                    new DuctSystemPdfField("Airflow", "1,250 cfm"),
                    new DuctSystemPdfField("Entered size", "20 x 12 in"),
                    new DuctSystemPdfField("Material", "Galvanized Spiral Duct"),
                    new DuctSystemPdfField("Duct length", "85 ft")
                },
                new[]
                {
                    new DuctSystemPdfField("Actual velocity", "750 fpm"),
                    new DuctSystemPdfField("Pressure-loss rate", "0.080 in. w.c./100 ft"),
                    new DuctSystemPdfField("Total pressure loss", "0.068 in. w.c.")
                },
                new[]
                {
                    new DuctSystemPdfCalculationSection("FLOW AND GEOMETRY", new[]
                    {
                        new DuctSystemPdfCalculationLine("Airflow Q =", "1,250 cfm"),
                        new DuctSystemPdfCalculationLine(@"V = \frac{Q}{A}", "750 fpm", IsEquation: true)
                    }),
                    new DuctSystemPdfCalculationSection("PRESSURE LOSS", new[]
                    {
                        new DuctSystemPdfCalculationLine(@"\Delta P = f \frac{L}{D} \frac{\rho V^{2}}{2g_{c}}", "0.068 in. w.c.", IsEquation: true)
                    })
                }, QuickCheck: new[]
                {
                    new DuctSystemPdfField("Duct size", "20 x 12 in"),
                    new DuctSystemPdfField("Airflow", "1,250 cfm"),
                    new DuctSystemPdfField("Duct length", "85 ft"),
                    new DuctSystemPdfField("Pressure drop", "0.068 in. w.c.")
                }),
            new DuctSystemPdfReportItem("L-002", "Cooling coil", "COIL",
                new[]
                {
                    new DuctSystemPdfField("Airflow", "1,250 cfm"),
                    new DuctSystemPdfField("Leaving temperature", "55 degF")
                },
                new[] { new DuctSystemPdfField("Component pressure loss", "0.250 in. w.c.") })
        };
        var pdfMetadata = new DuctSystemPdfMetadata(document.UserName, document.ProjectName,
            document.ProjectNumber, document.ProjectDate, CriticalPath: "L-001 → L-010");
        DuctSystemPdf.Export(pdfPath, "Duct System Schedule", "Verification sample", headers, rows,
            "Critical-path total pressure loss: 1.700 in. w.c.",
            metadata: pdfMetadata, reportItems: reportItems);
        var pdf = File.ReadAllBytes(pdfPath);
        if (pdf.Length < 1_000 || Encoding.ASCII.GetString(pdf, 0, 8) != "%PDF-1.4")
            throw new Exception("The PDF report is missing or invalid.");
        var pdfText = PdfPlainText(pdf);
        var summaryIndex = pdfText.IndexOf("Project Summary", StringComparison.Ordinal);
        var ductSheetIndex = pdfText.IndexOf("D-001 - STRAIGHT DUCT", StringComparison.Ordinal);
        var coilSheetIndex = pdfText.IndexOf("L-002 - COIL", StringComparison.Ordinal);
        var firstReviewHeadingIndex = pdfText.IndexOf("(ENGINEERING CALCULATION)", StringComparison.Ordinal);
        var firstInputHeadingIndex = pdfText.IndexOf("(INPUTS)", StringComparison.Ordinal);
        var finalScheduleIndex = pdfText.LastIndexOf("Duct System Schedule", StringComparison.Ordinal);
        if (!pdfText.Contains("/Count 7", StringComparison.Ordinal)
            || !pdfText.Contains("Project Summary", StringComparison.Ordinal)
            // The arrow in "L-001 → L-010" is set in the embedded symbol font (code 0xAE), never as '?'.
            || !pdfText.Contains("(L-001 ) Tj /F4", StringComparison.Ordinal)
            || !pdfText.Contains("(\\256) Tj", StringComparison.Ordinal)
            || pdfText.Contains("L-001 ? L-010", StringComparison.Ordinal)
            || !pdfText.Contains("CALCULATED OUTPUTS", StringComparison.Ordinal)
            || !pdfText.Contains("QUICK CHECK", StringComparison.Ordinal)
            || !pdfText.Contains("(ENGINEERING CALCULATION)", StringComparison.Ordinal)
            || !pdfText.Contains("(Inputs substituted)", StringComparison.Ordinal)
            || !pdfText.Contains("(Solved result)", StringComparison.Ordinal)
            || !pdfText.Contains("HVAC AIRSIDE DESIGN", StringComparison.Ordinal)
            // The wordmark is the brand board's own lettering as filled vector outlines, not typeset text.
            || pdfText.Contains("(fan works)", StringComparison.Ordinal)
            || !pdfText.Contains("h f*", StringComparison.Ordinal)
            // Brand typeface (embedded TrueType) and the embedded symbol font: real glyphs, no hand-drawn symbols.
            || !pdfText.Contains("/BaseFont /Nunito-Italic", StringComparison.Ordinal)
            || !pdfText.Contains("/FontFile2", StringComparison.Ordinal)
            || !pdfText.Contains("/BaseFont /FanWorksSymbols", StringComparison.Ordinal)
            || !pdfText.Contains("/ToUnicode", StringComparison.Ordinal)
            || !pdfText.Contains("/F3 ", StringComparison.Ordinal)
            || pdfText.Contains("/BaseFont /Symbol", StringComparison.Ordinal)
            || pdfText.Contains("/BaseFont /Helvetica", StringComparison.Ordinal)
            || !pdfText.Contains("1,250 cfm", StringComparison.Ordinal)
            || summaryIndex < 0 || ductSheetIndex <= summaryIndex || coilSheetIndex <= ductSheetIndex
            || finalScheduleIndex <= coilSheetIndex
            || firstReviewHeadingIndex < 0 || firstInputHeadingIndex <= firstReviewHeadingIndex)
            throw new Exception("The PDF report did not paginate the title, schedule, and one-sheet item-detail pages with vector math typography.");
        var samplePdf = Environment.GetEnvironmentVariable("DUCTSIZER_REPORT_SAMPLE_PATH");
        if (!string.IsNullOrWhiteSpace(samplePdf))
        {
            var metadata = new DuctSystemPdfMetadata(document.UserName, document.ProjectName,
                document.ProjectNumber, "September 25, 2026");
            var layout = new DuctSystemPdfLayout(
                new[]
                {
                    new DuctSystemPdfLayoutNode("L-001", "L-001\r\n0 in.w.g.", "Fan", "", 2, 5, 1, 1, "Supply Air", "Main"),
                    new DuctSystemPdfLayoutNode("F-006", "F-006\r\n0.03 in.w.g.", "Fitting", "Transitions", 5, 5, 1, 1, "Supply Air", "Main"),
                    new DuctSystemPdfLayoutNode("L-002", "L-002\r\n0.10 in.w.g.", "Filter", "", 8, 5, 1, 1, "Supply Air", "Main"),
                    new DuctSystemPdfLayoutNode("L-003", "L-003\r\n0.04 in.w.g.", "Fitting", "Diverting Fittings", 11, 5, 1, 2, "Supply Air", "Main", true),
                    new DuctSystemPdfLayoutNode("L-004", "L-004\r\n0.02 in.w.g.", "Fitting", "Terminals", 14, 5, 1, 1, "Supply Air", "Main"),
                    new DuctSystemPdfLayoutNode("L-005", "L-005\r\n0.03 in.w.g.", "Fitting", "Terminals", 11, 9, 1, 1, "Supply Air", "B-001"),
                    new DuctSystemPdfLayoutNode("T-007", "T-007", "Grd", "Grille", 14, 9, 1, 1, "Supply Air", "B-001"),
                    new DuctSystemPdfLayoutNode("L-008", "L-008", "Reservoir", "Supply Discharge", 17, 5, 1, 0, "Supply Air", "Main"),
                    new DuctSystemPdfLayoutNode("S-009", "S-009", "CalculationPoint", "Airflow", 14, 12, 0, 0, "Supply Air", "B-001")
                },
                new[]
                {
                    new DuctSystemPdfLayoutConnection("L-001", "F-006", false, true, "D-001", "Main", "0.04 in.w.g."),
                    new DuctSystemPdfLayoutConnection("F-006", "L-002", false, true, "D-006", "Main", "0.04 in.w.g."),
                    new DuctSystemPdfLayoutConnection("L-002", "L-003", false, true, "D-002", "Main", "0.07 in.w.g."),
                    new DuctSystemPdfLayoutConnection("L-003", "L-004", false, true, "D-003", "Main", "0.05 in.w.g."),
                    new DuctSystemPdfLayoutConnection("L-003", "L-005", false, false, "", "B-001", "—"),
                    new DuctSystemPdfLayoutConnection("L-005", "L-007", false, true, "D-007", "B-001", "0.02 in.w.g."),
                    new DuctSystemPdfLayoutConnection("L-004", "L-008", false, true, "D-008", "Main", "0.01 in.w.g.")
                });
            DuctSystemPdf.Export(samplePdf, "Duct System Schedule", "DuctSizer export preview", headers, rows.Take(18).ToArray(),
                "Critical-path total pressure loss: 1.700 in. w.c.", metadata, layout, reportItems);
            DuctSystemCsv.Export(Path.ChangeExtension(samplePdf, ".csv"), document);
        }
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

/// <summary>The PDF's bytes with every FlateDecode stream decompressed in place (for text checks).</summary>
static string PdfPlainText(byte[] pdf)
{
    var text = Encoding.Latin1.GetString(pdf);
    var result = new StringBuilder();
    var position = 0;
    while (true)
    {
        var marker = text.IndexOf(">>\nstream\n", position, StringComparison.Ordinal);
        var start = marker < 0 ? -1 : marker + 3;
        if (start < 0)
        {
            result.Append(text, position, text.Length - position);
            break;
        }
        var dataStart = start + "stream\n".Length;
        var end = text.IndexOf("\nendstream", dataStart, StringComparison.Ordinal);
        var dictionaryStart = text.LastIndexOf("<<", start, StringComparison.Ordinal);
        var dictionary = dictionaryStart >= 0 ? text[dictionaryStart..start] : string.Empty;
        result.Append(text, position, dataStart - position);
        if (dictionary.Contains("/FlateDecode", StringComparison.Ordinal) && !dictionary.Contains("/Image", StringComparison.Ordinal)
            && !dictionary.Contains("/Length1", StringComparison.Ordinal))
        {
            using var input = new MemoryStream(pdf, dataStart, end - dataStart);
            using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            result.Append(Encoding.Latin1.GetString(output.ToArray()));
        }
        else
            result.Append(text, dataStart, end - dataStart);
        position = end;
    }
    return result.ToString();
}

static void TestInvalidInput()
{
    try
    {
        GeometryCalculator.Round(0.0);
    }
    catch (ArgumentOutOfRangeException)
    {
        return;
    }
    throw new Exception("Zero diameter was not rejected.");
}

static void AssertNear(double actual, double expected, double tolerance)
{
    if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
        throw new Exception($"Expected {expected:R}; received {actual:R}; tolerance {tolerance:R}.");
}

static void AssertBetween(double actual, double lower, double upper, string name)
{
    if (!double.IsFinite(actual) || actual < lower || actual > upper)
        throw new Exception($"{name} {actual:R} is outside [{lower:R}, {upper:R}].");
}
