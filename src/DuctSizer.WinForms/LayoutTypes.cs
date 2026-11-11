using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal readonly record struct LayoutConnectionRef(SavedLayoutNode Downstream, bool Secondary);

internal enum LayoutDuctInsertionChoice
{
    Insert,
    PlaceUnconnected,
    Cancel
}
internal readonly record struct LayoutPortRef(SavedLayoutNode Node, bool IsOutput, int Index, Point? Anchor = null);
internal readonly record struct LayoutPortConnectionRequest(SavedLayoutNode Upstream, SavedLayoutNode Downstream,
    int OutputIndex, int InputIndex);
internal readonly record struct LayoutEditableAxes(bool X, bool Y, bool Z);

/// <summary>
/// The component a position is entered relative to: the upstream component, or, when there is none, a downstream
/// component. The stored position is absolute; dialogs show and accept the offset of the edited component from this
/// reference (edited minus reference).
/// </summary>
internal sealed record LayoutPositionReference(string Id, LayoutCoordinate Coordinate, bool IsDownstream)
{
    internal string Caption => IsDownstream
        ? $"Offset from {Id} (downstream), ΔX / ΔY / ΔZ (ft)"
        : $"Offset from {Id}, ΔX / ΔY / ΔZ (ft)";

    internal static string CaptionFor(LayoutPositionReference? reference) =>
        reference?.Caption ?? "Absolute position X / Y / Z (ft)";

    /// <summary>Converts an absolute position to the offset shown to the user.</summary>
    internal static LayoutCoordinate ToDisplay(LayoutCoordinate absolute, LayoutPositionReference? reference) =>
        reference is null ? absolute
            : new LayoutCoordinate(absolute.XM - reference.Coordinate.XM, absolute.YM - reference.Coordinate.YM,
                absolute.ZM - reference.Coordinate.ZM);

    /// <summary>Converts the offset the user entered back to an absolute position.</summary>
    internal static LayoutCoordinate FromDisplay(LayoutCoordinate entered, LayoutPositionReference? reference) =>
        reference is null ? entered
            : new LayoutCoordinate(entered.XM + reference.Coordinate.XM, entered.YM + reference.Coordinate.YM,
                entered.ZM + reference.Coordinate.ZM);
}
