using DuctSizer.Core;



/// <summary>
/// Air handling units (schema 18): a fan component with an internal static pressure. The duct system sees only the
/// external static pressure (ESP); the fan develops ESP + internal (total static pressure), and the motor power is
/// based on the total.
/// </summary>
internal static class AirHandlingUnitTests
{
    public static void TotalStaticIsExternalPlusInternalAndSetsMotorPower()
    {
        var document = new DuctSystemDocument();
        var ahu = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        ahu.IsAirHandlingUnit = true;
        ahu.FanStaticPressurePa = UnitConversions.InchesWaterToPa(1.0);
        ahu.FanInternalStaticPressurePa = UnitConversions.InchesWaterToPa(1.5);
        ahu.FanEfficiencyPercent = 65;
        Require(ahu.Id == $"AHU-{ahu.Sequence:000}" && ahu.FanEquipmentName == "Air Handling Unit",
            $"An air handling unit is named AHU-nnn; got {ahu.Id}.");
        Near(UnitConversions.PaToInchesWater(ahu.FanTotalStaticPressurePa), 2.5, 1e-12, "TSP = ESP + ISP = 1.0 + 1.5");

        // Hand check: BHP = 2000 cfm x 2.5 in. w.c. / (6356 x 0.65) = 5000 / 4131.4 = 1.210243 hp.
        var bhp = FanPerformance.BrakeHorsepower(UnitConversions.CfmToM3PerS(2000), ahu.FanTotalStaticPressurePa, 65);
        Near(bhp, 5000.0 / (6356 * 0.65), 1e-9, "AHU brake horsepower on total static pressure");
        Near(bhp, 1.210243, 1e-6, "AHU brake horsepower hand value");

        // From v0.103 a fan uses the same template: total = external + internal (internal is 0 unless entered).
        var fanDocument = new DuctSystemDocument();
        var fan = SystemTests.AddNode(fanDocument, LayoutNodeType.Fan, parent: null, duct: null);
        fan.FanStaticPressurePa = 300;
        Require(fan.Id == $"Fan-{fan.Sequence:000}" && !fan.IsAirHandlingUnit && fan.FanTotalStaticPressurePa == 300,
            "A fan with no internal static pressure: total = external; its ID stays Fan-nnn.");
        fan.FanInternalStaticPressurePa = 100;
        Require(fan.FanTotalStaticPressurePa == 400, "A fan's internal static pressure is added to its total.");
    }

    public static void DuctSystemSeesOnlyTheExternalStaticPressure()
    {
        // AHU -> 14 in x 30 ft -> sink. The AHU outlet pressure is its inlet pressure plus ESP; the internal static
        // pressure is used inside the unit and must not change any duct pressure or the duct pressure class.
        var document = new DuctSystemDocument();
        var ahu = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        ahu.IsAirHandlingUnit = true;
        ahu.AirflowM3PerS = UnitConversions.CfmToM3PerS(1500);
        ahu.FanStaticPressurePa = UnitConversions.InchesWaterToPa(3.4);
        var duct = SystemTests.AddDuct(document, 14, 1500, 30);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, ahu, duct);
        sink.SpaceRole = "Space Sink";
        sink.AirflowM3PerS = duct.AirflowM3PerS;

        ahu.FanInternalStaticPressurePa = 0;
        SystemSolver.Solve(document);
        var outletWithoutInternal = ahu.PressurePa;
        var sinkWithoutInternal = sink.PressurePa;
        var classWithoutInternal = SmacnaLeakage.Analyze(document).CalculatedPressureClassInWc;

        ahu.FanInternalStaticPressurePa = UnitConversions.InchesWaterToPa(5.0);
        SystemSolver.Solve(document);
        Near(ahu.PressurePa, outletWithoutInternal, 1e-9, "AHU outlet pressure (ESP only)");
        Near(sink.PressurePa, sinkWithoutInternal, 1e-9, "downstream pressure (internal static not in the ductwork)");
        Near(UnitConversions.PaToInchesWater(ahu.PressurePa), 3.4, 1e-9, "AHU outlet = 0 + ESP");
        var leakage = SmacnaLeakage.Analyze(document);
        Require(leakage.CalculatedPressureClassInWc == classWithoutInternal && leakage.CalculatedPressureClassInWc == 4
                && leakage.DesignPressureBasis == "air handling unit external static pressure",
            $"The duct pressure class follows the ESP (3.4 -> 4 in. w.c.), not ESP + internal; got {leakage.CalculatedPressureClassInWc} ({leakage.DesignPressureBasis}).");
    }

    public static void AirHandlingUnitRoundTripsAndOlderFilesOpenAsFans()
    {
        var document = new DuctSystemDocument();
        var ahu = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        ahu.IsAirHandlingUnit = true;
        ahu.FanInternalStaticPressurePa = 211.5;
        ahu.FanStaticPressurePa = 180;
        var coil = SystemTests.AddNode(document, LayoutNodeType.Coil, ahu, duct: null);
        var path = Path.Combine(Path.GetTempPath(), $"fanworks-ahu-{Guid.NewGuid():N}.json");
        try
        {
            DuctSystemStore.Save(path, document);
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Require((int)json["SchemaVersion"]! == DuctSystemDocument.CurrentSchemaVersion && DuctSystemDocument.CurrentSchemaVersion >= 18,
                "Air handling units are saved in file format 18.");
            var loaded = DuctSystemStore.Load(path);
            var loadedAhu = loaded.LayoutNodes.Single(x => x.Sequence == ahu.Sequence);
            var loadedCoil = loaded.LayoutNodes.Single(x => x.Sequence == coil.Sequence);
            Require(loadedAhu.IsAirHandlingUnit && loadedAhu.FanInternalStaticPressurePa == 211.5
                    && loadedAhu.FanStaticPressurePa == 180 && loadedAhu.Id == ahu.Id
                    && loadedCoil.ParentNodeId == ahu.Id,
                "The AHU flag, internal and external static pressure, its ID and connections must round-trip.");

            // A format-17 file has no AHU fields: its fan opens as a plain fan with no internal static pressure.
            var node = json["LayoutNodes"]!.AsArray().Single(x => (int)x!["Sequence"]! == ahu.Sequence)!.AsObject();
            var fanBlock = node["Fan"]!.AsObject();
            fanBlock.Remove("IsAirHandlingUnit");
            fanBlock.Remove("InternalStaticPressurePa");
            json["SchemaVersion"] = 17;
            foreach (var other in json["LayoutNodes"]!.AsArray())
                if ((string?)other!["ParentNodeId"] == ahu.Id)
                    other["ParentNodeId"] = $"Fan-{ahu.Sequence:000}";
            File.WriteAllText(path, json.ToJsonString());
            var older = DuctSystemStore.Load(path);
            var olderFan = older.LayoutNodes.Single(x => x.Sequence == ahu.Sequence);
            Require(older.SchemaVersion == DuctSystemDocument.CurrentSchemaVersion && !olderFan.IsAirHandlingUnit && olderFan.FanInternalStaticPressurePa == 0
                    && olderFan.Id == $"Fan-{ahu.Sequence:000}" && olderFan.FanTotalStaticPressurePa == 180,
                "A format-17 fan must open as a plain fan, unchanged.");

            // A negative or non-finite internal static pressure in a hand-edited file opens as 0.
            json["SchemaVersion"] = 18;
            fanBlock["IsAirHandlingUnit"] = true;
            fanBlock["InternalStaticPressurePa"] = -40;
            File.WriteAllText(path, json.ToJsonString());
            Require(DuctSystemStore.Load(path).LayoutNodes.Single(x => x.Sequence == ahu.Sequence).FanInternalStaticPressurePa == 0,
                "A negative internal static pressure opens as 0.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static void ExternalStaticSplitsAtTheFan()
    {
        // Return space -> 12 in x 40 ft -> AHU -> 14 in x 30 ft -> supply space. The critical path's external static
        // pressure divides at the AHU: negative (suction) = the return duct's loss, positive (discharge) = the supply's.
        var document = new DuctSystemDocument();
        var source = SystemTests.AddNode(document, LayoutNodeType.Reservoir, parent: null, duct: null);
        source.SpaceRole = "Space Source";
        source.AirflowM3PerS = UnitConversions.CfmToM3PerS(1500);
        var returnDuct = SystemTests.AddDuct(document, 12, 1500, 40);
        var ahu = SystemTests.AddNode(document, LayoutNodeType.Fan, source, returnDuct);
        ahu.IsAirHandlingUnit = true;
        ahu.AirflowM3PerS = UnitConversions.CfmToM3PerS(1500);
        var supplyDuct = SystemTests.AddDuct(document, 14, 1500, 30);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, ahu, supplyDuct);
        sink.SpaceRole = "Space Sink";
        sink.AirflowM3PerS = supplyDuct.AirflowM3PerS;
        SystemSolver.Solve(document);
        var profiles = SystemPathProfiles.Build(document);
        var critical = profiles.Paths.Single(path => path.IsCritical);
        var returnLoss = returnDuct.Calculate().TotalPressureLossUpperPa;
        var supplyLoss = supplyDuct.Calculate().TotalPressureLossUpperPa;
        Require(critical.FanStepIndex >= 0, "The critical path passes through the AHU.");
        Near(critical.SuctionLossPa!.Value, returnLoss, 1e-9, "negative ESP (suction) = return duct loss");
        Near(critical.DischargeLossPa!.Value, supplyLoss, 1e-9, "positive ESP (discharge) = supply duct loss");
        Near(critical.SuctionLossPa.Value + critical.DischargeLossPa.Value, critical.TotalLossPa, 1e-9, "suction + discharge = ESP");

        // A route that starts at the fan has no suction side.
        var simple = new DuctSystemDocument();
        var fan = SystemTests.AddNode(simple, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(800);
        var duct = SystemTests.AddDuct(simple, 10, 800, 25);
        var end = SystemTests.AddNode(simple, LayoutNodeType.Reservoir, fan, duct);
        end.SpaceRole = "Space Sink";
        end.AirflowM3PerS = duct.AirflowM3PerS;
        SystemSolver.Solve(simple);
        var path = SystemPathProfiles.Build(simple).Paths.Single(item => item.IsCritical);
        Require(path.SuctionLossPa == 0.0 && Math.Abs(path.DischargeLossPa!.Value - path.TotalLossPa) < 1e-12,
            "A route that starts at the fan is all discharge (positive pressure).");
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
