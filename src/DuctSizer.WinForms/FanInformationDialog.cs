using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class FanInformationDialog : DpiScaledForm
{
    internal FanInformationDialog(string information)
    {
        Text = "Fan Information";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(570, 304);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9.5F);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var icon = new PictureBox
        {
            Name = "FanInformationIcon",
            Size = new Size(132, 132),
            Anchor = AnchorStyles.None,
            Margin = new Padding(14),
            BackColor = Color.White
        };
        icon.Paint += (_, args) =>
        {
            var side = Math.Max(1, Math.Min(icon.ClientSize.Width, icon.ClientSize.Height) - 28);
            var square = new Rectangle((icon.ClientSize.Width - side) / 2, (icon.ClientSize.Height - side) / 2,
                side, side);
            LayoutIconPainter.Draw(args.Graphics, LayoutNodeType.Fan, square, Color.FromArgb(31, 120, 180));
        };

        var details = new Label
        {
            Name = "FanInformationDetails",
            Text = information,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 28, 18, 12),
            ForeColor = Color.FromArgb(34, 62, 88),
            Font = new Font("Segoe UI", 10.5F),
            TextAlign = ContentAlignment.TopLeft
        };
        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 92,
            BackColor = Color.FromArgb(31, 120, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        UiPolish.RoundButton(close, 7);
        var buttons = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 18, 12) };
        buttons.Controls.Add(close);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(details, 1, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }
}
