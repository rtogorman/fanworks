using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Drawing ducts (round Z): the Duct Segment and Flex Duct Segment tools join two components centre to centre, and
/// the rules that decide what is drawn. Ducts run straight between components (a diagonal in plan is fine); a sloping
/// duct is drawn only when the user agrees, and an elbow is added only when the user asks for it.
/// </summary>
internal sealed partial class LayoutDesignerScreen
{
    /// <summary>The duct tool chosen on the ribbon (none while placing, selecting or moving).</summary>
    internal LayoutDuctTool ActiveDuctTool { get; private set; }

    /// <summary>
    /// Answer given to the slope and elbow questions while the automated interface checks run (no dialogs are shown):
    /// true accepts the slope and adds the elbows automatically.
    /// </summary>
    internal bool AutomatedConnectionAnswer { get; set; } = true;

    /// <summary>Ribbon Duct Segment / Flex Duct Segment: draw ducts until Select is chosen or Esc is pressed.</summary>
    internal void UseDuctTool(bool flex)
    {
        if (_layoutRearrangeMode)
            ToggleLayoutRearrangeMode();
        _selectedLayoutTool = null;
        _selectedLayoutFittingGroup = null;
        ActiveDuctTool = flex ? LayoutDuctTool.Flex : LayoutDuctTool.Rigid;
        if (_layoutCanvas is not null)
        {
            _layoutCanvas.SelectedTool = null;
            _layoutCanvas.DuctTool = ActiveDuctTool;
        }
        _layoutStatusLabel.Text = (flex ? "Flex Duct Segment" : "Duct Segment")
                                  + ": press on a component, drag to the next component and release. Esc or Select ends duct drawing.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(14, 111, 230);
        NotifyLayoutCommandStateChanged();
    }

    /// <summary>Ends duct drawing (Select, Esc, or another tool). Returns false when no duct tool was on.</summary>
    internal bool EndDuctTool()
    {
        if (ActiveDuctTool == LayoutDuctTool.None)
            return false;
        ActiveDuctTool = LayoutDuctTool.None;
        if (_layoutCanvas is not null)
            _layoutCanvas.DuctTool = LayoutDuctTool.None;
        NotifyLayoutCommandStateChanged();
        return true;
    }

    /// <summary>
    /// A duct drawn on the canvas from <paramref name="from"/> to <paramref name="to"/>. The air flows from the first
    /// component to the second when that is possible; otherwise (for example drawing from a sink back to a duct, or
    /// from a component with no free outlet) the other way round.
    /// </summary>
    internal bool DrawDuctBetween(SavedLayoutNode from, SavedLayoutNode to, bool flex)
    {
        var forward = DuctDirectionProblem(from, to);
        var backward = DuctDirectionProblem(to, from);
        var (upstream, downstream) = forward is null || backward is not null ? (from, to) : (to, from);
        if (forward is not null && backward is not null)
        {
            if (!AutomatedLayoutVerification)
                MessageBox.Show(_host, forward, "Duct Not Drawn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _layoutStatusLabel.Text = forward;
            _layoutStatusLabel.ForeColor = Color.DarkOrange;
            return false;
        }
        var vector = DrawnOffset(upstream, downstream);
        return ConnectLayoutNodes(upstream, downstream, OutletIndexFor(upstream, vector), InletIndexFor(downstream), flex);
    }

    /// <summary>Why air cannot flow from <paramref name="upstream"/> to <paramref name="downstream"/>, or null when it can.</summary>
    private string? DuctDirectionProblem(SavedLayoutNode upstream, SavedLayoutNode downstream)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        if (downstream.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(downstream.SpaceRole))
            return $"{downstream.Id} is a Space Source: air leaves it, so no duct can lead into it.";
        if (upstream.Type == LayoutNodeType.Reservoir && IsSpaceSinkRole(upstream.SpaceRole))
            return $"{upstream.Id} is a Space Sink: air ends there, so no duct can leave it.";
        if (IsLayoutDescendant(upstream, downstream) || upstream.Id == downstream.Id)
            return $"A duct from {upstream.Id} to {downstream.Id} would close a loop.";
        if (ConnectedLayoutInputCount(downstream) >= LayoutInletCapacity(downstream))
            return $"{downstream.Id} has no free inlet.";
        var outlets = LayoutTopology.ConnectionsFrom(document, upstream).Count();
        var capacity = upstream.Type == LayoutNodeType.Fitting ? FittingCatalog.OutletPortCount(upstream.FittingGroup) : 1;
        if (outlets >= capacity)
            return $"{upstream.Id} has no free outlet.";
        return null;
    }

    /// <summary>The first free inlet: the second inlet of a converging fitting once the first is used.</summary>
    private static int InletIndexFor(SavedLayoutNode downstream) => string.IsNullOrWhiteSpace(downstream.ParentNodeId) ? 0 : 1;

    /// <summary>
    /// The outlet a new duct leaves from: 0 (main, straight through) or 1 (branch) on a diverging fitting. With both
    /// free, a duct in line with the fitting's inlet is the main run and any other direction is the branch.
    /// </summary>
    private int OutletIndexFor(SavedLayoutNode upstream, LayoutVector offset)
    {
        if (upstream.Type != LayoutNodeType.Fitting || FittingCatalog.OutletPortCount(upstream.FittingGroup) <= 1)
            return 0;
        var document = _host.DuctSystemScreen._ductSystem;
        var outlets = LayoutTopology.ConnectionsFrom(document, upstream).ToArray();
        var mainUsed = outlets.Any(x => string.Equals(x.Downstream.BranchId, upstream.BranchId, StringComparison.OrdinalIgnoreCase));
        var branchUsed = outlets.Any(x => !string.Equals(x.Downstream.BranchId, upstream.BranchId, StringComparison.OrdinalIgnoreCase));
        if (mainUsed != branchUsed)
            return mainUsed ? 1 : 0;
        if (string.IsNullOrWhiteSpace(upstream.ParentNodeId))
            return 0;
        var turn = LayoutDirections.TurnDegrees(LayoutDirections.ConnectionVector(upstream, secondary: false), offset);
        return turn is double degrees && degrees > LayoutDirections.StraightToleranceDegrees ? 1 : 0;
    }

    /// <summary>The offset from one component to another as drawn (before the user confirms it).</summary>
    private LayoutVector DrawnOffset(SavedLayoutNode upstream, SavedLayoutNode downstream)
    {
        var delta = ConnectionDeltaPreservingAbsolutePosition(upstream, downstream);
        return new LayoutVector(delta.XM, delta.YM, delta.ZM);
    }

    /// <summary>Asks whether a sloping duct is intended. Yes draws it.</summary>
    private bool ConfirmSlopedDuct(SavedLayoutNode upstream, SavedLayoutNode downstream, LayoutVector offset)
    {
        if (AutomatedLayoutVerification)
            return AutomatedConnectionAnswer;
        var slope = LayoutDirections.SlopeDegrees(offset) ?? 0.0;
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        string Length(double metres) => imperial
            ? $"{UnitConversions.MToFeet(metres):0.##} ft"
            : $"{metres:0.###} m";
        var message = $"The duct from {upstream.Id} to {downstream.Id} slopes {(slope > 0 ? "upward" : "downward")} at "
                      + $"{Math.Abs(slope):0.#}° ({(slope > 0 ? "rising" : "falling")} {Length(Math.Abs(offset.ZM))} over "
                      + $"{Length(offset.HorizontalLength)} horizontally).\r\n\r\nIs that OK?";
        return MessageBox.Show(_host, message, "Sloped Duct", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
               == DialogResult.Yes;
    }

    /// <summary>
    /// Whether the new duct would turn the air at a component that cannot turn it: at the upstream component (its
    /// inlet is not in line with the new duct) or at the downstream one (its outlet is not). Null when no elbow is needed.
    /// </summary>
    private (string Message, LayoutVector? UpstreamInlet, LayoutVector? DownstreamOutlet)? ElbowNeededFor(
        SavedLayoutNode upstream, SavedLayoutNode downstream, LayoutVector offset)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        LayoutVector? upstreamInlet = null;
        LayoutVector? downstreamOutlet = null;
        var places = new List<string>();
        if (LayoutDirections.CannotTurn(upstream) && !string.IsNullOrWhiteSpace(upstream.ParentNodeId))
        {
            var inlet = LayoutDirections.ConnectionVector(upstream, secondary: false);
            if (LayoutDirections.TurnDegrees(inlet, offset) is double turn && turn > LayoutDirections.StraightToleranceDegrees)
            {
                upstreamInlet = inlet;
                places.Add($"{upstream.Id} ({ComponentKind(upstream)}): the air would turn {turn:0.#}°");
            }
        }
        if (LayoutDirections.CannotTurn(downstream)
            && LayoutTopology.ConnectionsFrom(document, downstream).Cast<LayoutConnection?>().FirstOrDefault() is { } outlet)
        {
            var outletVector = LayoutDirections.ConnectionVector(outlet.Downstream, outlet.Secondary);
            if (LayoutDirections.TurnDegrees(offset, outletVector) is double turn && turn > LayoutDirections.StraightToleranceDegrees)
            {
                downstreamOutlet = outletVector;
                places.Add($"{downstream.Id} ({ComponentKind(downstream)}): the air would turn {turn:0.#}°");
            }
        }
        if (places.Count == 0)
            return null;
        return ($"An elbow is needed: {string.Join("; ", places)}, and this component cannot turn the air.",
            upstreamInlet, downstreamOutlet);
    }

    private static string ComponentKind(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.Coil => "coil",
        LayoutNodeType.Filter => "filter",
        LayoutNodeType.CustomComponent => "custom component",
        LayoutNodeType.Fitting when string.Equals(node.FittingGroup, FittingCatalog.VavSelectionGroup, StringComparison.OrdinalIgnoreCase) => "VAV box",
        LayoutNodeType.Fitting when node.FittingGroup == "Transitions" => "transition",
        LayoutNodeType.Fitting when node.FittingGroup == "Dampers" => "damper",
        _ => "component"
    };

    /// <summary>
    /// Straight runs along the X, Y and Z axes that make up <paramref name="offset"/>, ordered so the duct first carries
    /// straight on from <paramref name="upstreamInlet"/> and arrives in line with <paramref name="downstreamOutlet"/>,
    /// with 90° elbows at the corners. Null when that is not possible (for example the duct leaves at right angles to
    /// an inlet that runs along one axis only): then the elbow has to be placed by hand.
    /// </summary>
    internal static IReadOnlyList<(double XM, double YM, double ZM)>? AutomaticElbowSegments(LayoutVector offset,
        LayoutVector? upstreamInlet, LayoutVector? downstreamOutlet)
    {
        var runs = new List<(double XM, double YM, double ZM)>();
        if (Math.Abs(offset.XM) >= LayoutDirections.MinimumLengthM) runs.Add((offset.XM, 0, 0));
        if (Math.Abs(offset.YM) >= LayoutDirections.MinimumLengthM) runs.Add((0, offset.YM, 0));
        if (Math.Abs(offset.ZM) >= LayoutDirections.MinimumLengthM) runs.Add((0, 0, offset.ZM));
        if (runs.Count < 2)
            return null;
        bool InLine(LayoutVector direction, (double XM, double YM, double ZM) run) =>
            LayoutDirections.TurnDegrees(direction, new LayoutVector(run.XM, run.YM, run.ZM)) is double turn
            && turn <= LayoutDirections.StraightToleranceDegrees;
        if (upstreamInlet is LayoutVector inlet)
        {
            var first = runs.FindIndex(run => InLine(inlet, run));
            if (first < 0)
                return null;
            var run = runs[first];
            runs.RemoveAt(first);
            runs.Insert(0, run);
        }
        if (downstreamOutlet is LayoutVector outlet)
        {
            var last = runs.FindLastIndex(run => InLine(outlet, run));
            if (last < 0 || (upstreamInlet is not null && last == 0))
                return null;
            var run = runs[last];
            runs.RemoveAt(last);
            runs.Add(run);
        }
        return runs;
    }

    /// <summary>
    /// Asks whether to add the elbows automatically. Returns true to add them; false when the user will place the
    /// elbow (nothing is drawn). When automatic elbows are not possible the user is told to place the elbow.
    /// </summary>
    private bool AskToAddElbows(string need, bool automaticPossible)
    {
        if (AutomatedLayoutVerification)
            return automaticPossible && AutomatedConnectionAnswer;
        if (!automaticPossible)
        {
            MessageBox.Show(_host, need + "\r\n\r\nPlace an elbow where the duct turns, then draw the ducts to it.",
                "Elbow Needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        return MessageBox.Show(_host, need
                                      + "\r\n\r\nAdd the elbows automatically? 90° elbows are placed at the corners of a route along the X, Y and Z axes."
                                      + "\r\n\r\nYes: add them automatically.\r\nNo: I will add the elbow myself (no duct is drawn now).",
                   "Elbow Needed", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
               == DialogResult.Yes;
    }

    /// <summary>The duct material for a connection: fully extended flex duct when it was drawn as Flex Duct Segment.</summary>
    internal static DuctMaterial? FlexMaterialFor(SavedLayoutNode downstream, bool secondary) =>
        (secondary ? downstream.SecondaryConnectionFlexDuct : downstream.ConnectionFlexDuct)
            ? DuctMaterialCatalog.All.First(x => x.Name.StartsWith("Flexible", StringComparison.OrdinalIgnoreCase))
            : null;
}
