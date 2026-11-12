using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Custom automatic duct size rounding (ribbon Auto Duct Size Rounding > Custom): the increment automatically sized
/// ducts round to, in inches (imperial) or millimetres (metric).
/// </summary>
internal sealed class RoundingIncrementDialog : DpiScaledForm
{
    private readonly TextBox _increment = new() { Name = "RoundingIncrementText", Width = 92, TextAlign = HorizontalAlignment.Right };
    private readonly bool _imperial;

    /// <summary>The increment entered, m (valid after OK).</summary>
    internal double IncrementM { get; private set; }

    internal RoundingIncrementDialog(double currentIncrementM, UnitSystem units)
    {
        _imperial = units == UnitSystem.Imperial;
        Text = "Auto Duct Size Rounding";
        Name = "RoundingIncrementDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 200);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        IncrementM = currentIncrementM;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 16, 20, 14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label
        {
            Text = "Custom rounding increment",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(11, 31, 59),
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Automatically sized ducts round up to a multiple of this size.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(74, 91, 106)
        }, 0, 1);
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        row.Controls.Add(new Label
        {
            Text = "Round duct sizes to",
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 0),
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        });
        _increment.Text = (_imperial ? UnitConversions.MToInches(currentIncrementM) : UnitConversions.MToMillimeters(currentIncrementM))
            .ToString("0.####", CultureInfo.CurrentCulture);
        _increment.Margin = new Padding(0, 4, 8, 0);
        row.Controls.Add(_increment);
        row.Controls.Add(new Label
        {
            Text = _imperial ? "in." : "mm",
            AutoSize = true,
            Margin = new Padding(0, 7, 0, 0),
            ForeColor = Color.FromArgb(74, 91, 106)
        });
        root.Controls.Add(row, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button
        {
            Name = "RoundingIncrementSaveButton", Text = "Save", Width = 100, Height = 32,
            BackColor = Color.FromArgb(14, 111, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        save.Click += (_, _) => SaveIncrement();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    /// <summary>The entered text as an increment in metres, or null when it is not a positive size (0.01 in. to 12 in.).</summary>
    internal static double? Parse(string text, bool imperial)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || !double.IsFinite(value) || value <= 0)
            return null;
        var metres = imperial ? UnitConversions.InchesToM(value) : value / 1000.0;
        return metres < UnitConversions.InchesToM(0.01) || metres > UnitConversions.InchesToM(12) ? null : metres;
    }

    private void SaveIncrement()
    {
        if (Parse(_increment.Text, _imperial) is not double metres)
        {
            MessageBox.Show(this, _imperial
                    ? "Enter a rounding increment between 0.01 in. and 12 in."
                    : "Enter a rounding increment between 0.25 mm and 305 mm.",
                "Check Rounding Increment", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _increment.Focus();
            _increment.SelectAll();
            return;
        }
        IncrementM = metres;
        DialogResult = DialogResult.OK;
        Close();
    }
}
