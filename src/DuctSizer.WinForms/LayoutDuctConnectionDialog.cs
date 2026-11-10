using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutDuctConnectionDialog : DpiScaledForm, IInlineEditable
{
    private readonly ComboBox _airSystem = new();
    internal string SelectedAirSystemType => Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? "Supply Air";
    internal bool ReverseRequested { get; private set; }

    internal LayoutDuctConnectionDialog(string upstreamId, string downstreamId, string airSystemType,
        bool hasSavedDuct = false)
    {
        Text = "Duct Connection Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 190);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);
        _airSystem.Name = "LayoutDuctAirSystemCombo";
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = MainForm.AirSystemTypes.FirstOrDefault(x =>
            string.Equals(x, airSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18), BackColor = BackColor };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Connection", new Label { Text = $"{upstreamId}  →  {downstreamId}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(28, 69, 108), Font = new Font("Segoe UI Semibold", 9F) });
        AddRow(table, 1, "Air System Type", _airSystem);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var open = new Button
        {
            Name = "LayoutDuctEditButton",
            Text = hasSavedDuct ? "Edit" : "Size Duct",
            DialogResult = DialogResult.OK,
            Width = 125,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        var reverse = new Button
        {
            Name = "LayoutDuctReverseButton",
            Text = "Reverse Flow Direction",
            Width = 165,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White
        };
        reverse.Click += (_, _) =>
        {
            ReverseRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        UiPolish.RoundButton(open, 7);
        UiPolish.RoundButton(reverse, 7);
        UiPolish.RoundButton(cancel, 7);
        buttons.Controls.Add(open);
        buttons.Controls.Add(reverse);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(buttons, 0, 3);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = open;
        CancelButton = cancel;
    }

    public IReadOnlyList<InlineEditRow> InlineRows => new[] { new InlineEditRow("Air system", _airSystem) };

    public event Action? InlineRowsChanged
    {
        add { }
        remove { }
    }

    public Control? InlineStatus => null;

    public void UseInline(IWin32Window messageOwner)
    {
    }

    public bool TryAcceptInline() => true;

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(48, 65, 80) }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }
}
