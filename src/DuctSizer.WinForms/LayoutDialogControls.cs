using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed record LayoutNodeChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

internal sealed record LayoutValueChoice<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

internal static class LayoutDialogControls
{
    internal static Control CreateInputWithUnit(TextBox input, string unit)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        input.Dock = DockStyle.Fill;
        panel.Controls.Add(input, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = unit,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0),
            ForeColor = Color.FromArgb(48, 65, 80)
        }, 1, 0);
        return panel;
    }

    internal static Control CreateLocationEditor(TextBox x, TextBox y, TextBox z) =>
        CreateLocationEditor(x, y, z, "X", "Y", "Z");

    /// <summary>Position boxes labelled ΔX/ΔY/ΔZ when the caption describes an offset, otherwise X/Y/Z.</summary>
    internal static Control CreatePositionEditor(string caption, TextBox x, TextBox y, TextBox z) =>
        caption.Contains('Δ')
            ? CreateLocationEditor(x, y, z, "ΔX", "ΔY", "ΔZ")
            : CreateLocationEditor(x, y, z, "X", "Y", "Z");

    /// <summary>
    /// Three short labelled boxes on one row. Each label column is sized to its text (so "Dia"
    /// never wraps) and sits directly beside its own box.
    /// </summary>
    internal static Control CreateLocationEditor(TextBox x, TextBox y, TextBox z, string xLabel, string yLabel, string zLabel)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        for (var index = 0; index < 3; index++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        }
        AddAxis(xLabel, x, 0);
        AddAxis(yLabel, y, 2);
        AddAxis(zLabel, z, 4);
        return row;

        void AddAxis(string label, TextBox input, int column)
        {
            row.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(column == 0 ? 0 : 8, 0, 2, 0),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(48, 65, 80),
                Font = new Font("Segoe UI Semibold", 8F)
            }, column, 0);
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(1, 5, 4, 5);
            row.Controls.Add(input, column + 1, 0);
        }
    }
}
