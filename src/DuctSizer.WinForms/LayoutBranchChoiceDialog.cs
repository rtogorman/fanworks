using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutBranchChoiceDialog : Form
{
    private readonly RadioButton _mainPath = new();
    private readonly RadioButton _newBranch = new();
    internal bool NewBranchSelected => _newBranch.Checked;

    internal LayoutBranchChoiceDialog(string parentId, string? nodeId)
    {
        Text = "Select Connection Path";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 220);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var prompt = string.IsNullOrWhiteSpace(nodeId)
            ? $"Choose how the new component connects to diverting fitting {parentId}."
            : $"Choose how {nodeId} connects to diverting fitting {parentId}.";
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = prompt + " The connector side is inferred from where the component is placed.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(48, 65, 80),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _mainPath.Name = "LayoutMainPathRadio";
        _mainPath.Text = "Main path (same branch)";
        _mainPath.Checked = true;
        _mainPath.Dock = DockStyle.Fill;
        _newBranch.Name = "LayoutNewBranchRadio";
        _newBranch.Text = "Branch path (new branch)";
        _newBranch.Dock = DockStyle.Fill;
        layout.Controls.Add(_mainPath, 0, 1);
        layout.Controls.Add(_newBranch, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var connect = new Button
        {
            Name = "LayoutBranchConnectButton",
            Text = "Connect",
            DialogResult = DialogResult.OK,
            Size = new Size(110, 32),
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 32), FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(connect, 7);
        UiPolish.RoundButton(cancel, 7);
        buttons.Controls.Add(connect);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = connect;
        CancelButton = cancel;
    }
}
