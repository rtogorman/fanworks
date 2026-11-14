using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Size locks (round Y): a duct or component whose size is locked keeps its shape, size and selection when Auto Size
/// Ductwork runs (<see cref="SavedDuct.SizeLocked"/>, <see cref="SavedLayoutNode.SizeLocked"/>). A lock is set from
/// the Selection Inspector (one item) or the Component Lock Editor (every item). Each change is one undo step.
/// </summary>
internal sealed partial class LayoutDesignerScreen
{
    /// <summary>
    /// True for drawn components that Auto Size Ductwork can change, so a lock means something: fittings (VAV boxes
    /// and air terminals included), GRDs, coils, filters and custom components. Fans are sized by Calculate; spaces
    /// and calculation points have no size.
    /// </summary>
    internal static bool CanLockSize(SavedLayoutNode node) => node.Type is LayoutNodeType.Fitting or LayoutNodeType.Grd
        or LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.CustomComponent;

    /// <summary>
    /// The ducts and components of the drawing, in flow order (each component after the ducts that feed it), for the
    /// Component Lock Editor.
    /// </summary>
    internal IReadOnlyList<ComponentLockRow> ComponentLockRows()
    {
        var document = _host.DuctSystemScreen._ductSystem;
        var screen = _host.DuctSystemScreen;
        var rows = new List<ComponentLockRow>();
        var listedDucts = new HashSet<int>();
        foreach (var node in LayoutTopology.FlowOrder(document))
        {
            foreach (var secondary in new[] { false, true })
            {
                var parentId = secondary ? node.SecondaryParentNodeId : node.ParentNodeId;
                if (string.IsNullOrWhiteSpace(parentId) || DuctForConnection(node, secondary) is not { } duct
                    || !listedDucts.Add(duct.Sequence))
                    continue;
                rows.Add(new ComponentLockRow(DuctSystemScreen.DuctKey(duct.Sequence), duct.Id, "Duct",
                    SafeDuctSize(duct), $"{parentId} to {node.Id}", duct.SizeLocked));
            }
            if (!CanLockSize(node))
                continue;
            var fitting = LayoutTopology.LinkedFitting(document, node);
            var size = fitting is not null ? screen.SafeFittingSize(fitting)
                : node.Type == LayoutNodeType.Grd ? GrdSizeCaption(node)
                : node.IsDefined ? "—" : "Not defined";
            var description = fitting is not null ? fitting.Description
                : node.Type == LayoutNodeType.CustomComponent ? node.Description
                : node.Type == LayoutNodeType.Grd ? node.GrdType
                : string.Empty;
            rows.Add(new ComponentLockRow(DuctSystemScreen.LayoutKey(node.Sequence), node.Id, LockTypeCaption(node), size,
                description, node.SizeLocked));
        }
        return rows;

        string SafeDuctSize(SavedDuct duct)
        {
            try { return screen.FormatSystemGeometry(duct.CreateGeometry()); }
            catch (ArgumentException) { return "Invalid size"; }
        }
    }

    private static string LockTypeCaption(SavedLayoutNode node) => node.Type switch
    {
        LayoutNodeType.Fitting when string.Equals(node.FittingGroup, FittingCatalog.VavSelectionGroup, StringComparison.OrdinalIgnoreCase) => "VAV box",
        LayoutNodeType.Fitting when FittingCatalog.IsTerminalSelectionGroup(node.FittingGroup) => "Terminal",
        LayoutNodeType.Fitting => node.FittingGroup switch
        {
            "Elbows" => "Elbow",
            "Transitions" => "Transition",
            "Diverting Fittings" => "Split",
            "Converging Fittings" => "Merge",
            "Dampers" => "Damper",
            _ => "Fitting"
        },
        LayoutNodeType.Grd => "GRD",
        LayoutNodeType.Coil => "Coil",
        LayoutNodeType.Filter => "Filter",
        LayoutNodeType.CustomComponent => "Custom",
        _ => node.Type.ToString()
    };

    private string GrdSizeCaption(SavedLayoutNode node)
    {
        if (!node.IsDefined)
            return "Not defined";
        if (string.Equals(node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase))
            return $"{node.GrdNominalDuctSizeIn} in";
        if (string.Equals(node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase))
            return $"{node.GrdFaceSizeIn:0} in face, {node.GrdNeckDiameterIn:0} in neck";
        if (string.Equals(node.GrdType, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase))
            return $"{node.GrdLengthFt:0} ft, {node.GrdSlotCount} slot";
        return "—";
    }

    /// <summary>Opens the Component Lock Editor and applies the locks it returns as one undo step.</summary>
    internal void OpenComponentLockEditor()
    {
        var rows = ComponentLockRows();
        if (rows.Count == 0)
        {
            if (!AutomatedLayoutVerification)
                MessageBox.Show(_host, "Draw and connect ducts or components before locking their sizes.",
                    "Component Lock Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var editor = new ComponentLockEditorDialog(rows);
        if (editor.ShowDialog(_host) != DialogResult.OK)
            return;
        ApplySizeLocks(editor.Locks);
    }

    /// <summary>
    /// Sets the size lock of the listed items ("D:{sequence}" ducts, "L:{sequence}" components) as one undo step, and
    /// saves the project. Returns the number of items whose lock changed.
    /// </summary>
    internal int ApplySizeLocks(IReadOnlyDictionary<string, bool> locks)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        var historyState = CaptureLayoutHistoryState();
        var changed = 0;
        foreach (var (key, locked) in locks)
        {
            if (key.Length < 3 || !int.TryParse(key.AsSpan(2), out var sequence))
                continue;
            if (key.StartsWith("D:", StringComparison.Ordinal)
                && document.Ducts.FirstOrDefault(x => x.Sequence == sequence) is { } duct && duct.SizeLocked != locked)
            {
                duct.SizeLocked = locked;
                changed++;
            }
            else if (key.StartsWith("L:", StringComparison.Ordinal)
                     && document.LayoutNodes.FirstOrDefault(x => x.Sequence == sequence) is { } node
                     && CanLockSize(node) && node.SizeLocked != locked)
            {
                node.SizeLocked = locked;
                changed++;
            }
        }
        if (changed == 0)
            return 0;
        UpdateCalculatedLayoutValues();
        CommitLayoutHistoryState(historyState);
        _host.DuctSystemScreen.SaveDuctSystem();
        _host.DuctSystemScreen.RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        var lockedTotal = document.Ducts.Count(x => x.SizeLocked) + document.LayoutNodes.Count(x => x.SizeLocked);
        _layoutStatusLabel.Text = $"Size locks updated: {lockedTotal} item(s) locked. Auto Size Ductwork keeps their sizes.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        return changed;
    }

    /// <summary>The size-lock key of the item shown in the Selection Inspector, or null when it cannot be locked.</summary>
    internal string? SizeLockKey(SavedLayoutNode? node, LayoutConnectionRef? connection)
    {
        if (connection is LayoutConnectionRef selected)
            return DuctForConnection(selected.Downstream, selected.Secondary) is { } duct ? DuctSystemScreen.DuctKey(duct.Sequence) : null;
        if (node is null)
            return null;
        if (node.Type == LayoutNodeType.Duct && node.LinkedDuctSequence is int ductSequence)
            return DuctSystemScreen.DuctKey(ductSequence);
        return CanLockSize(node) ? DuctSystemScreen.LayoutKey(node.Sequence) : null;
    }

    /// <summary>Whether the item with this size-lock key is locked.</summary>
    internal bool IsSizeLocked(string key)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        if (key.Length < 3 || !int.TryParse(key.AsSpan(2), out var sequence))
            return false;
        return key.StartsWith("D:", StringComparison.Ordinal)
            ? document.Ducts.FirstOrDefault(x => x.Sequence == sequence)?.SizeLocked == true
            : document.LayoutNodes.FirstOrDefault(x => x.Sequence == sequence)?.SizeLocked == true;
    }
}
