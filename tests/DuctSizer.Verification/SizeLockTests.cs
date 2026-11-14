using DuctSizer.Core;

/// <summary>
/// Round Y (v0.106): flow order for Auto Size Ductwork, size locks, and the schema 22 upgrade (size locks saved, manual
/// flow reversal retired). Expected values are worked by hand in the comments.
/// </summary>
internal static class SizeLockTests
{
    /// <summary>
    /// Components are ordered from the sources to the sinks: each one after every component that feeds it, ties in
    /// sequence order, whatever order they were drawn in.
    /// </summary>
    public static void FlowOrderRunsFromSourcesToSinks()
    {
        // Drawn out of order: the sink (sequence 1) is created before the split (2), the fan (3) and the branches.
        var document = new DuctSystemDocument();
        var sinkA = new SavedLayoutNode { Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir, SpaceRole = "Space Sink" };
        var split = new SavedLayoutNode { Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fitting, FittingGroup = "Diverting Fittings" };
        var fan = new SavedLayoutNode { Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Fan };
        var sinkB = new SavedLayoutNode { Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.Reservoir, SpaceRole = "Space Sink" };
        var point = new SavedLayoutNode { Sequence = document.ReserveLayoutSequence(), Type = LayoutNodeType.CalculationPoint };
        document.LayoutNodes.AddRange(new[] { sinkA, split, fan, sinkB, point });
        split.ParentNodeId = fan.Id;
        sinkA.ParentNodeId = split.Id;
        sinkB.ParentNodeId = split.Id;

        var order = LayoutTopology.FlowOrder(document).Select(x => x.Id).ToArray();
        // Hand order: Fan (no upstream), then the split, then both sinks in sequence order (sinkA = 1, sinkB = 4);
        // the calculation point is not a component of the flow.
        Require(order.SequenceEqual(new[] { fan.Id, split.Id, sinkA.Id, sinkB.Id }),
            $"Flow order should be fan, split, sink A, sink B. Was {string.Join(", ", order)}.");

        // A loop cannot be ordered: its members follow in sequence order, nothing is dropped.
        fan.ParentNodeId = sinkB.Id;
        var looped = LayoutTopology.FlowOrder(document);
        Require(looped.Count == 4 && looped.Select(x => x.Sequence).SequenceEqual(new[] { 1, 2, 3, 4 }),
            "Components on a loop are all listed, in sequence order.");
    }

    /// <summary>
    /// A locked in-line fitting keeps its size when the duct it is drawn on is re-sized; unlocked, it follows the duct
    /// again on the next solve. Its airflow follows the system either way.
    /// </summary>
    public static void LockedFittingKeepsItsSize()
    {
        // Fan (600 cfm) -> 10 in x 20 ft -> elbow -> 10 in x 10 ft -> space.
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(600);
        fan.AirflowUserSpecified = true;
        var inlet = SystemTests.AddDuct(document, 10, 600, 20);
        var elbowNode = SystemTests.AddNode(document, LayoutNodeType.Fitting, fan, inlet);
        elbowNode.FittingGroup = "Elbows";
        var elbow = new SavedFitting
        {
            Sequence = document.ReserveFittingSequence(),
            FittingCode = "3-1",
            Description = "3-1 elbow",
            Inputs = new Dictionary<string, double> { ["rd"] = 1.5, ["angle"] = 90 },
            AirflowM3PerS = UnitConversions.CfmToM3PerS(600),
            TemperatureC = 20,
            Shape = DuctShape.Round,
            DiameterM = UnitConversions.InchesToM(10)
        };
        document.Fittings.Add(elbow);
        elbowNode.LinkedFittingSequence = elbow.Sequence;
        var outlet = SystemTests.AddDuct(document, 10, 600, 10);
        var sink = SystemTests.AddNode(document, LayoutNodeType.Reservoir, elbowNode, outlet);
        sink.SpaceRole = "Space Sink";
        SystemSolver.Solve(document);
        Near(UnitConversions.MToInches(elbow.DiameterM), 10, 1e-9, "the elbow starts at the 10 in duct size");

        // Lock the elbow, then re-size the duct it is drawn on to 12 in and change the airflow to 800 cfm.
        elbowNode.SizeLocked = true;
        inlet.DiameterM = UnitConversions.InchesToM(12);
        fan.AirflowM3PerS = UnitConversions.CfmToM3PerS(800);
        SystemSolver.Solve(document);
        Near(UnitConversions.MToInches(elbow.DiameterM), 10, 1e-9, "a locked elbow keeps its 10 in size");
        Near(UnitConversions.M3PerSToCfm(elbow.AirflowM3PerS), 800, 1e-9, "a locked elbow still carries the system airflow");
        Require(elbow.ReferenceSizeSource is null, "A locked size does not name a duct as its source.");

        // Unlocked, it follows the 12 in duct again.
        elbowNode.SizeLocked = false;
        SystemSolver.Solve(document);
        Near(UnitConversions.MToInches(elbow.DiameterM), 12, 1e-9, "an unlocked elbow follows the re-sized duct");
        Require(SystemSolver.TakesConnectedDuctSize(elbow), "An elbow takes the connected duct's size.");
    }

    /// <summary>
    /// File format 22: locks are saved and read back; a format-21 file opens with nothing locked, and any manual flow
    /// reversal it stored is cleared (flow follows the drawn sources and sinks).
    /// </summary>
    public static void LocksAreSavedAndReversalIsRetired()
    {
        var document = new DuctSystemDocument();
        var fan = SystemTests.AddNode(document, LayoutNodeType.Fan, parent: null, duct: null);
        var duct = SystemTests.AddDuct(document, 10, 500, 10);
        duct.SizeLocked = true;
        var coil = SystemTests.AddNode(document, LayoutNodeType.Coil, fan, duct);
        coil.SizeLocked = true;
        var path = Path.Combine(Path.GetTempPath(), $"fanworks-locks-{Guid.NewGuid():N}.json");
        try
        {
            DuctSystemStore.Save(path, document);
            var loaded = DuctSystemStore.Load(path);
            Require(DuctSystemDocument.CurrentSchemaVersion >= 22 && loaded.SchemaVersion == DuctSystemDocument.CurrentSchemaVersion
                    && loaded.Ducts.Single().SizeLocked && loaded.LayoutNodes.Single(x => x.Type == LayoutNodeType.Coil).SizeLocked
                    && !loaded.LayoutNodes.Single(x => x.Type == LayoutNodeType.Fan).SizeLocked,
                "Size locks are saved and read back unchanged.");

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            json["SchemaVersion"] = 21;
            json["Ducts"]![0]!.AsObject().Remove("SizeLocked");
            foreach (var node in json["LayoutNodes"]!.AsArray())
            {
                node!.AsObject().Remove("SizeLocked");
                node.AsObject()["ConnectionFlowReversed"] = true;
            }
            File.WriteAllText(path, json.ToJsonString());
            var older = DuctSystemStore.Load(path);
            Require(older.SchemaVersion == DuctSystemDocument.CurrentSchemaVersion
                    && older.Ducts.All(x => !x.SizeLocked) && older.LayoutNodes.All(x => !x.SizeLocked)
                    && older.LayoutNodes.All(x => !x.ConnectionFlowReversed && !x.SecondaryConnectionFlowReversed),
                "A format-21 project opens with nothing locked and no reversed flow.");
        }
        finally
        {
            File.Delete(path);
        }
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
