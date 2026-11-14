using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Editing an item from the Selection Inspector. Each edit uses the same editor classes as the editor windows
/// (their controls are shown in the inspector) and the same apply steps, so a value accepted in the inspector is
/// checked, applied, saved and undoable exactly as if it had been entered in the window.
/// </summary>
internal sealed partial class LayoutDesignerScreen
{
    /// <summary>A component editor and the state its apply step needs.</summary>
    private sealed record EquipmentEdit(LayoutEquipmentDialog Dialog, Dictionary<int, double> BoundaryAirflows,
        double PreviousAirflow, bool HasAbsoluteCoordinate, LayoutCoordinate AbsoluteCoordinate);

    /// <summary>A fitting's layout editor and the state its apply step needs.</summary>
    private sealed record FittingEdit(LayoutFittingNodeDialog NodeDialog, SavedFitting? LinkedFitting,
        LayoutCoordinate? AbsoluteCoordinate);

    /// <summary>Sequence of the component selected on the drawing, or null.</summary>
    internal int? CanvasSelectedNodeSequence => _layoutCanvas?.SelectedNode?.Sequence;

    private EquipmentEdit BeginEquipmentEdit(SavedLayoutNode node)
    {
        var boundaryAirflows = node.Type == LayoutNodeType.Reservoir
            ? LayoutBoundaryNodes().ToDictionary(boundary => boundary.Sequence, boundary => boundary.AirflowM3PerS)
            : new Dictionary<int, double>();
        var previousAirflow = node.AirflowM3PerS;
        UpdateCalculatedLayoutValues();
        var resolvedCoordinates = LayoutCoordinateResolver.Resolve(_host.DuctSystemScreen._ductSystem);
        var hasAbsolute = resolvedCoordinates.TryGetValue(node.Sequence, out var absoluteCoordinate);
        var dialog = hasAbsolute
            ? new LayoutEquipmentDialog(node, _host.DuctSystemScreen._ductSystem.LayoutNodes, LayoutNodeCaption, absoluteCoordinate,
                EditableAxesForNode(node), PositionReferenceFor(node, resolvedCoordinates))
            : new LayoutEquipmentDialog(node, _host.DuctSystemScreen._ductSystem.LayoutNodes, LayoutNodeCaption);
        return new EquipmentEdit(dialog, boundaryAirflows, previousAirflow, hasAbsolute, absoluteCoordinate);
    }

    /// <summary>Applies an accepted component edit. False when the new wiring or position was rejected.</summary>
    private bool ApplyEquipmentEdit(SavedLayoutNode node, EquipmentEdit edit, string historyState)
    {
        var dialog = edit.Dialog;
        var selectedParent = node.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(node.SpaceRole)
            ? string.Empty
            : dialog.SelectedParentNodeId;
        if (!RewireLayoutNode(node, selectedParent, reposition: true))
            return false;
        if (edit.HasAbsoluteCoordinate)
            ApplyAbsoluteCoordinateEdit(node, edit.AbsoluteCoordinate, dialog.DesiredAbsoluteCoordinate);
        node.AirSystemType = dialog.SelectedAirSystemType;
        node.IsDefined = true;
        if (!EnsureConnectedCoordinateDeltas(node))
        {
            node.IsDefined = false;
            return false;
        }
        if (node.Type == LayoutNodeType.Reservoir
            && Math.Abs(node.AirflowM3PerS - edit.PreviousAirflow) > 1e-9)
            ReconcileBoundaryAirflowAfterDirectEdit(node, edit.BoundaryAirflows);
        UpdateCalculatedLayoutValues();
        SyncConnectionDuctGeometry(node);
        SyncLinkedItemMetadata(node);
        if (node.Type == LayoutNodeType.Fan)
            _host.DuctSystemScreen._ductSystem.DefaultAirSystemType = node.AirSystemType;
        CommitLayoutHistoryState(historyState);
        _host.DuctSystemScreen.SaveDuctSystem();
        RefreshLayoutDesigner();
        _host.DuctSystemScreen.RefreshDuctSystemGrid();
        return true;
    }

    /// <summary>Applies an accepted edit of a duct drawn as its own item. False when the user declined.</summary>
    private bool ApplyLinkedDuctEdit(SavedLayoutNode node, SavedDuct duct, SavedDuct edited, string historyState)
    {
        if (!_host.DuctSystemScreen.ConfirmAirflowOverride(node, edited.AirflowM3PerS, duct.Id))
            return false;
        CopyDuctValues(edited, duct);
        duct.RequiresResizing = false;
        node.AirflowM3PerS = duct.AirflowM3PerS;
        node.TemperatureC = duct.TemperatureC;
        SyncLinkedItemMetadata(node);
        CommitLayoutHistoryState(historyState);
        _host.DuctSystemScreen.SaveDuctSystem();
        _host.DuctSystemScreen.RefreshDuctSystemGrid();
        RefreshLayoutDesigner();
        _layoutStatusLabel.Text = $"Updated {duct.Id} without leaving Layout Designer.";
        _layoutStatusLabel.ForeColor = Color.FromArgb(30, 100, 55);
        return true;
    }

    private FittingEdit BeginFittingEdit(SavedLayoutNode node)
    {
        var linkedFitting = node.LinkedFittingSequence is int sequence
            ? _host.DuctSystemScreen._ductSystem.Fittings.FirstOrDefault(x => x.Sequence == sequence)
            : null;
        var resolvedCoordinates = LayoutCoordinateResolver.Resolve(_host.DuctSystemScreen._ductSystem);
        LayoutCoordinate? absoluteCoordinate = resolvedCoordinates.TryGetValue(node.Sequence, out var resolved)
            ? resolved : null;
        var dialog = new LayoutFittingNodeDialog(node, _host.DuctSystemScreen._ductSystem.LayoutNodes, LayoutNodeCaption,
            linkedFitting, absoluteCoordinate, EditableAxesForNode(node),
            PositionReferenceFor(node, resolvedCoordinates));
        return new FittingEdit(dialog, linkedFitting, absoluteCoordinate);
    }

    private LayoutFittingEditDialog NewFittingEditor(SavedFitting? linkedFitting, SavedLayoutNode node)
    {
        var draft = linkedFitting is null
            ? CreateLayoutFittingDraft(node)
            : CloneFitting(linkedFitting);
        // Round Z: an elbow's angle comes from its drawn ducts, so the inspector shows it read-only.
        var drawnAngle = LayoutDirections.IsElbow(node)
            ? LayoutDirections.TurnDegreesThrough(_host.DuctSystemScreen._ductSystem, node)
            : null;
        return new LayoutFittingEditDialog(draft, node.FittingGroup, _host._currentUnits,
            drawnAngle is double angle ? Math.Round(angle, 1) : null);
    }

    /// <summary>
    /// The inspector edit for a drawn component, or null when it has no inputs to edit in place (a calculation
    /// point, or a space source when several sources are balanced in the Source/Sink editor).
    /// </summary>
    internal InlineEditSession? CreateInlineEditSession(SavedLayoutNode node)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        if (!document.LayoutNodes.Contains(node))
            return null;
        switch (node.Type)
        {
            case LayoutNodeType.CalculationPoint:
                return null;
            case LayoutNodeType.Reservoir when LayoutSpaceSources().Length > 1:
                return null;
            case LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil or LayoutNodeType.Filter
                or LayoutNodeType.Grd or LayoutNodeType.CustomComponent:
            {
                var edit = BeginEquipmentEdit(node);
                return new InlineEditSession("Update Component", new IInlineEditable[] { edit.Dialog },
                    history => ApplyEquipmentEdit(node, edit, history))
                {
                    CaptureHistory = CaptureLayoutHistoryState,
                    RestoreWhenDeclined = history => RestoreLayoutHistoryState(history, "The component edit was not applied.")
                };
            }
            case LayoutNodeType.Duct when node.LinkedDuctSequence is int ductSequence
                                          && document.Ducts.FirstOrDefault(x => x.Sequence == ductSequence) is { } duct:
            {
                var editor = NewDuctEditor(CloneDuct(duct));
                return new InlineEditSession("Update Duct", new IInlineEditable[] { editor },
                    history => editor.Result is SavedDuct edited && ApplyLinkedDuctEdit(node, duct, edited, history))
                {
                    CaptureHistory = CaptureLayoutHistoryState
                };
            }
            case LayoutNodeType.Fitting:
            {
                var edit = BeginFittingEdit(node);
                var editor = NewFittingEditor(edit.LinkedFitting, node);
                return new InlineEditSession(edit.LinkedFitting is null ? "Define Fitting" : "Update Fitting",
                    new IInlineEditable[] { edit.NodeDialog, editor },
                    history => editor.Result is SavedFitting edited && ApplyFittingEdit(node, edit, edited, history))
                {
                    CaptureHistory = CaptureLayoutHistoryState
                };
            }
            default:
                return null;
        }
    }

    /// <summary>The inspector edit for a drawn connection: its air system and its duct (sized when new).</summary>
    internal InlineEditSession? CreateInlineEditSession(LayoutConnectionRef connection)
    {
        var downstream = connection.Downstream;
        if (!_host.DuctSystemScreen._ductSystem.LayoutNodes.Contains(downstream))
            return null;
        var parentId = connection.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
        if (string.IsNullOrWhiteSpace(parentId))
            return null;
        var linkedDuct = LinkedConnectionDuct(connection);
        var connectionEditor = new LayoutDuctConnectionDialog(parentId, downstream.Id, downstream.AirSystemType, linkedDuct is not null);
        var ductEditor = NewDuctEditor(ConnectionDuctDraft(connection));
        var session = new InlineEditSession(linkedDuct is null ? "Size Duct" : "Update Duct",
            new IInlineEditable[] { connectionEditor, ductEditor },
            history =>
            {
                if (ductEditor.Result is not SavedDuct edited)
                    return false;
                var previousAirSystem = downstream.AirSystemType;
                ApplyConnectionAirSystem(connection, connectionEditor.SelectedAirSystemType);
                if (ApplyConnectionDuctEdit(connection, edited, history))
                    return true;
                ApplyConnectionAirSystem(connection, previousAirSystem); // declined: leave the project as it was
                return false;
            })
        {
            CaptureHistory = CaptureLayoutHistoryState
        };
        return session;
    }
}
