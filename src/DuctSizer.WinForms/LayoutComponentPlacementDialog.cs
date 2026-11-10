using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutComponentPlacementDialog : DpiScaledForm
{
    private readonly SavedLayoutNode _node;
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();

    private readonly UnitSystem _units;
    private readonly double _gridIncrement;

    internal LayoutComponentPlacementDialog(SavedLayoutNode node)
        : this(node, string.Empty, UnitSystem.Imperial)
    {
    }

    internal LayoutComponentPlacementDialog(SavedLayoutNode node, string connectedItemId = "", UnitSystem units = UnitSystem.Imperial,
        string? editableAxis = null)
        : this(node, connectedItemId, units, editableAxis, 0.0)
    {
    }

    internal LayoutComponentPlacementDialog(SavedLayoutNode node, string connectedItemId, UnitSystem units,
        string? editableAxis, double gridIncrement)
    {
        _node = node;
        _units = units;
        _gridIncrement = gridIncrement;
        Text = "Confirm Relative Coordinates";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, gridIncrement > 0 ? 214 : 180);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _deltaX.Name = "LayoutPlacementDeltaXText";
        _deltaY.Name = "LayoutPlacementDeltaYText";
        _deltaZ.Name = "LayoutPlacementDeltaZText";
        _deltaX.Text = Display(node.DeltaXM).ToString("0.#####", CultureInfo.CurrentCulture);
        _deltaY.Text = Display(node.DeltaYM).ToString("0.#####", CultureInfo.CurrentCulture);
        _deltaZ.Text = Display(node.DeltaZM).ToString("0.#####", CultureInfo.CurrentCulture);
        if (!string.IsNullOrWhiteSpace(editableAxis))
        {
            ConfigureAxis(_deltaX, "X");
            ConfigureAxis(_deltaY, "Y");
            ConfigureAxis(_deltaZ, "Z");
        }
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = gridIncrement > 0 ? 4 : 3,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Component", new Label
        {
            Text = string.IsNullOrWhiteSpace(connectedItemId)
                ? node.Id
                : string.IsNullOrWhiteSpace(editableAxis)
                    ? $"{node.Id} relative to {connectedItemId} (signed values)"
                    : $"{node.Id} on {connectedItemId} duct — adjust {editableAxis} only",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F)
        });
        AddRow(table, 1, units == UnitSystem.Imperial ? "RELATIVE Δ (ft)" : "RELATIVE Δ (m)",
            LayoutDialogControls.CreateLocationEditor(_deltaX, _deltaY, _deltaZ));
        var buttonRow = 2;
        if (gridIncrement > 0)
        {
            var unit = units == UnitSystem.Imperial ? "ft" : "m";
            var gridNote = new Label
            {
                Text = $"Coordinates must follow the designer grid in {gridIncrement:0.#####} {unit} increments.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(74, 92, 106),
                Font = new Font("Segoe UI", 8.5F)
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.Controls.Add(gridNote, 0, 2);
            table.SetColumnSpan(gridNote, 2);
            buttonRow = 3;
        }
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Confirm Coordinates", Width = 152, Height = 32, BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        save.Click += (_, _) => SaveInputs();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, buttonRow);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
        return;

        void ConfigureAxis(TextBox input, string axis)
        {
            input.ReadOnly = !string.Equals(editableAxis, axis, StringComparison.OrdinalIgnoreCase);
            if (input.ReadOnly)
                input.BackColor = Color.FromArgb(232, 237, 241);
        }
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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

    private void SaveInputs()
    {
        if (!TryNumber(_deltaX.Text, out var x) || !TryNumber(_deltaY.Text, out var y)
            || !TryNumber(_deltaZ.Text, out var z))
        {
            MessageBox.Show(this, "Enter finite ΔX, ΔY, and ΔZ distances.", "Invalid Coordinate Delta",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_gridIncrement > 0
            && (!IsGridAligned(x, _gridIncrement) || !IsGridAligned(y, _gridIncrement)
                || !IsGridAligned(z, _gridIncrement)))
        {
            var unit = _units == UnitSystem.Imperial ? "ft" : "m";
            MessageBox.Show(this,
                $"Enter ΔX, ΔY, and ΔZ in {_gridIncrement:0.#####} {unit} grid increments. Negative values are allowed.",
                "Coordinates Must Match Grid", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _node.DeltaXM = Stored(x);
        _node.DeltaYM = Stored(y);
        _node.DeltaZM = Stored(z);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000;

    internal static bool IsGridAligned(double value, double increment)
    {
        if (!double.IsFinite(value) || !double.IsFinite(increment) || increment <= 0)
            return false;
        var cells = value / increment;
        return Math.Abs(cells - Math.Round(cells)) <= 1e-5;
    }

    private double Display(double meters) => _units == UnitSystem.Imperial ? UnitConversions.MToFeet(meters) : meters;
    private double Stored(double displayed) => _units == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
}
