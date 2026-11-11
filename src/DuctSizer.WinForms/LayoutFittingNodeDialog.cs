using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutFittingNodeDialog : DpiScaledForm, IInlineEditable
{
    private readonly List<InlineEditRow> _inlineRows = new();
    private IWin32Window? _messageOwner;
    private readonly ComboBox _upstream = new();
    private readonly ComboBox _airSystem = new();
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();
    internal string SelectedParentNodeId => (_upstream.SelectedItem as LayoutNodeChoice)?.Id ?? string.Empty;
    internal string SelectedAirSystemType => Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? "Supply Air";
    internal double DeltaXM => _absoluteLocation
        ? _node.DeltaXM : UnitConversions.FeetToM(ReadDelta(_deltaX, _node.DeltaXM));
    internal double DeltaYM => _absoluteLocation
        ? _node.DeltaYM : UnitConversions.FeetToM(ReadDelta(_deltaY, _node.DeltaYM));
    internal double DeltaZM => _absoluteLocation
        ? _node.DeltaZM : UnitConversions.FeetToM(ReadDelta(_deltaZ, _node.DeltaZM));
    private readonly SavedLayoutNode _node;
    private readonly bool _absoluteLocation;
    private LayoutCoordinate _desiredAbsoluteCoordinate;
    private readonly LayoutPositionReference? _reference;
    internal LayoutCoordinate DesiredAbsoluteCoordinate => _desiredAbsoluteCoordinate;

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting)
        : this(node, nodes, captionSource, fitting, null, null)
    {
    }

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting,
        LayoutCoordinate? absoluteCoordinate)
        : this(node, nodes, captionSource, fitting, absoluteCoordinate, null)
    {
    }

    internal LayoutFittingNodeDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, SavedFitting? fitting,
        LayoutCoordinate? absoluteCoordinate, LayoutEditableAxes? editableAxes,
        LayoutPositionReference? reference = null)
    {
        _node = node;
        _reference = absoluteCoordinate.HasValue ? reference : null;
        _absoluteLocation = absoluteCoordinate.HasValue;
        Text = "Fitting Layout Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(580, 420);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var downstream = new Label();
        LayoutEquipmentDialog.PopulateRelationshipControls(node, nodes, captionSource, _upstream, downstream);
        _upstream.Name = "LayoutUpstreamItemCombo";
        _airSystem.Name = "LayoutFittingAirSystemCombo";
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = MainForm.AirSystemTypes.FirstOrDefault(x =>
            string.Equals(x, node.AirSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";
        var location = absoluteCoordinate ?? new LayoutCoordinate(node.DeltaXM, node.DeltaYM, node.DeltaZM);
        _desiredAbsoluteCoordinate = location;
        // Positions are stored absolute; the user sees and enters the offset from the reference component.
        if (absoluteCoordinate.HasValue)
            location = LayoutPositionReference.ToDisplay(location, _reference);
        _deltaX.Text = UnitConversions.MToFeet(location.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaY.Text = UnitConversions.MToFeet(location.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaZ.Text = UnitConversions.MToFeet(location.ZM).ToString("0.##", CultureInfo.CurrentCulture);
        var coordinateInputs = new[]
        {
            (Input: _deltaX, Editable: editableAxes?.X ?? true),
            (Input: _deltaY, Editable: editableAxes?.Y ?? true),
            (Input: _deltaZ, Editable: editableAxes?.Z ?? true)
        };
        foreach (var coordinateInput in coordinateInputs)
        {
            coordinateInput.Input.ReadOnly = _absoluteLocation && !coordinateInput.Editable;
            if (coordinateInput.Input.ReadOnly)
                coordinateInput.Input.BackColor = Color.FromArgb(235, 241, 246);
        }
        downstream.Name = "LayoutDownstreamItemsLabel";
        var fittingText = fitting is null
            ? "Fitting has not been defined yet."
            : $"{fitting.Id} — {fitting.FittingCode} — {FittingCatalog.Find(fitting.FittingCode).Description}";

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDialogRow(table, 0, "Selected Fitting", new Label
        {
            Text = fittingText,
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F),
            Padding = new Padding(0, 5, 0, 5)
        }, 112);
        var row = 1;
        AddDialogRow(table, row++, "Air System Type", _airSystem, 42);
        AddDialogRow(table, row++, "Upstream Item", _upstream, 42);
        AddDialogRow(table, row++, "Downstream Item(s)", downstream, 42);
        AddDialogRow(table, row++, _absoluteLocation ? LayoutPositionReference.CaptionFor(_reference) : "Relative ΔX / ΔY / ΔZ (ft)",
            LayoutDialogControls.CreateLocationEditor(_deltaX, _deltaY, _deltaZ), 42);
        if (table.GetControlFromPosition(0, row - 1) is Label locationCaption)
        {
            locationCaption.Name = "LayoutLocationCaption";
            _inlineRows.AddRange(new[]
            {
                new InlineEditRow("Air system", _airSystem),
                new InlineEditRow("Upstream item", _upstream),
                new InlineEditRow("Downstream item(s)", downstream),
                InlineEditRow.ForPosition(locationCaption.Text, table.GetControlFromPosition(1, row - 1)!, _deltaX, _deltaY, _deltaZ)
            });
        }

        var note = new Label
        {
            Text = "Changing the upstream item rewires the layout. New connection ducts are red until sized.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoEllipsis = false,
            UseMnemonic = false,
            Padding = new Padding(0, 4, 0, 4),
            ForeColor = Color.FromArgb(92, 76, 42),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        table.Controls.Add(note, 0, row);
        table.SetColumnSpan(note, 2);
        row++;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var open = new Button
        {
            Name = "LayoutFittingEditButton",
            Text = fitting is null ? "Define Fitting" : "Edit",
            DialogResult = DialogResult.None,
            Width = 135,
            Height = 32,
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(open, 7);
        UiPolish.RoundButton(cancel, 7);
        open.Click += (_, _) =>
        {
            if (_absoluteLocation)
            {
                if (!TryReadLocation(out _desiredAbsoluteCoordinate))
                    return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(open);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, row);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = open;
        CancelButton = cancel;
    }

    public IReadOnlyList<InlineEditRow> InlineRows => _inlineRows;

    public event Action? InlineRowsChanged
    {
        add { }
        remove { }
    }

    private readonly Label _inlineStatus = new() { Name = "LayoutFittingNodeInlineStatus", ForeColor = Color.Firebrick };

    public Control? InlineStatus => _inlineStatus;

    public void UseInline(IWin32Window messageOwner) => _messageOwner = messageOwner;

    /// <summary>The Edit button's check: a valid location.</summary>
    public bool TryAcceptInline()
    {
        _inlineStatus.Text = string.Empty;
        return !_absoluteLocation || TryReadLocation(out _desiredAbsoluteCoordinate);
    }

    private static double ReadDelta(TextBox input, double fallbackMeters) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000
            ? value
            : UnitConversions.MToFeet(fallbackMeters);

    private bool TryReadLocation(out LayoutCoordinate coordinate)
    {
        coordinate = _desiredAbsoluteCoordinate;
        var kind = _reference is null ? "absolute" : "offset";
        if (!TryRead(_deltaX, $"{kind} X", out var x)
            || !TryRead(_deltaY, $"{kind} Y", out var y)
            || !TryRead(_deltaZ, $"{kind} Z", out var z))
            return false;
        coordinate = LayoutPositionReference.FromDisplay(new LayoutCoordinate(UnitConversions.FeetToM(x),
            UnitConversions.FeetToM(y), UnitConversions.FeetToM(z)), _reference);
        return true;
    }

    private bool TryRead(TextBox input, string name, out double value)
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value) && Math.Abs(value) <= 100_000)
            return true;
        if (_messageOwner is not null)
            _inlineStatus.Text = $"Enter a finite {name} coordinate between -100,000 ft and 100,000 ft.";
        else
            MessageBox.Show(this, $"Enter a finite {name} coordinate between -100,000 ft and 100,000 ft.",
                "Invalid Coordinate", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private static void AddDialogRow(TableLayoutPanel table, int row, string label, Control input, int height)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }
}
