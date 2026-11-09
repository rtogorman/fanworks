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
