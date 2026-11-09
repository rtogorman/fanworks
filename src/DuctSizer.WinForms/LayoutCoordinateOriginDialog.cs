using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutCoordinateOriginDialog : Form
{
    private readonly UnitSystem _units;
    private readonly TextBox _x = new();
    private readonly TextBox _y = new();
    private readonly TextBox _z = new();
    internal LayoutCoordinate Coordinate { get; private set; }

    internal LayoutCoordinateOriginDialog(string itemId, LayoutCoordinate current, UnitSystem units)
    {
        _units = units;
        Text = "Change Coordinate Origin";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 188);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);
        _x.Text = Display(current.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _y.Text = Display(current.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _z.Text = Display(current.ZM).ToString("0.##", CultureInfo.CurrentCulture);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
            Padding = new Padding(18), BackColor = BackColor };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Coordinate Reference", new Label { Text = itemId, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 9F) });
        AddRow(table, 1, units == UnitSystem.Imperial ? "ABSOLUTE X / Y / Z (ft)" : "ABSOLUTE X / Y / Z (m)",
            LayoutDialogControls.CreateLocationEditor(_x, _y, _z));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var change = new Button { Text = "Change Coordinate", Width = 142, Height = 32,
            BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(change, 7);
        UiPolish.RoundButton(cancel, 7);
        change.Click += (_, _) => Save();
        buttons.Controls.Add(change);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, 2);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = change;
        CancelButton = cancel;
    }

    private void Save()
    {
        if (!Try(_x.Text, out var x) || !Try(_y.Text, out var y) || !Try(_z.Text, out var z))
        {
            MessageBox.Show(this, "Enter finite X, Y, and Z coordinates.", "Invalid Coordinate",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Coordinate = new LayoutCoordinate(Stored(x), Stored(y), Stored(z));
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80), Font = new Font("Segoe UI Semibold", 9F) }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }

    private static bool Try(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && double.IsFinite(value) && Math.Abs(value) <= 100_000;
    private double Display(double meters) => _units == UnitSystem.Imperial ? UnitConversions.MToFeet(meters) : meters;
    private double Stored(double displayed) => _units == UnitSystem.Imperial ? UnitConversions.FeetToM(displayed) : displayed;
}
