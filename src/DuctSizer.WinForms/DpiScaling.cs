namespace DuctSizer.WinForms;

/// <summary>
/// Display scaling. Every window is built in code with sizes in 96-DPI pixels, while text is sized in points and
/// so already grows with the Windows display scale (125 %, 150 %, 175 % ...). Without this class the text outgrew
/// its buttons, columns and panels on scaled displays and was cut off.
///
/// The application is system-DPI aware (<c>ApplicationConfiguration.Initialize</c>), so one factor applies to every
/// window for the life of the process. Forms derive from <see cref="DpiScaledForm"/>, which scales the whole form
/// once before it is first shown; controls and painting that are created or measured later use <see cref="Scale(int)"/>.
/// At 100 % the factor is exactly 1 and nothing changes.
/// </summary>
internal static class DpiScaling
{
    private static float? _factor;

    /// <summary>System DPI / 96 (1.0 at 100 % display scale).</summary>
    internal static float Factor
    {
        get
        {
            if (_factor is null)
            {
                try
                {
                    using var graphics = Graphics.FromHwnd(IntPtr.Zero);
                    _factor = graphics.DpiX > 0 ? graphics.DpiX / 96F : 1F;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    _factor = 1F;
                }
            }
            return _factor.Value;
        }
    }

    internal static bool IsScaled => Math.Abs(Factor - 1F) > 0.01F;

    /// <summary>A 96-DPI pixel length at the current display scale.</summary>
    internal static int Scale(int pixels) => (int)Math.Round(pixels * Factor, MidpointRounding.AwayFromZero);

    internal static float Scale(float pixels) => pixels * Factor;

    internal static Size Scale(Size size) => new(Scale(size.Width), Scale(size.Height));

    internal static Point Scale(Point point) => new(Scale(point.X), Scale(point.Y));

    internal static Padding Scale(Padding padding) =>
        new(Scale(padding.Left), Scale(padding.Top), Scale(padding.Right), Scale(padding.Bottom));

    /// <summary>
    /// Scales a control tree built at 96-DPI pixel sizes: bounds, margins, padding and fixed table rows and
    /// columns (WinForms <see cref="Control.Scale(SizeF)"/>), plus the extras it leaves alone (grid column widths
    /// and row heights, split-container minimum sizes and splitter positions).
    /// </summary>
    internal static void ScaleControlTree(Control root)
    {
        if (!IsScaled)
            return;
        root.Scale(new SizeF(Factor, Factor));
        ScaleExtras(root);
    }

    private static void ScaleExtras(Control control)
    {
        switch (control)
        {
            case DataGridView grid:
                ScaleGrid(grid);
                break;
            case TreeView tree:
                tree.ItemHeight = Scale(tree.ItemHeight);
                tree.Indent = Scale(tree.Indent);
                break;
            case SplitContainer split:
                split.Panel1MinSize = Scale(split.Panel1MinSize);
                split.Panel2MinSize = Scale(split.Panel2MinSize);
                break;
        }
        foreach (Control child in control.Controls)
            ScaleExtras(child);
    }

    /// <summary>Scales a grid's fixed column widths and row heights (call once, after its columns are added).</summary>
    internal static void ScaleGrid(DataGridView grid)
    {
        if (!IsScaled || ScaledGrids.TryGetValue(grid, out _))
            return;
        ScaledGrids.Add(grid, new object());
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth = Math.Max(2, Scale(column.MinimumWidth));
            if (column.AutoSizeMode is DataGridViewAutoSizeColumnMode.None or DataGridViewAutoSizeColumnMode.NotSet)
                column.Width = Scale(column.Width);
        }
        grid.RowTemplate.Height = Scale(grid.RowTemplate.Height);
        foreach (DataGridViewRow row in grid.Rows)
            row.Height = Math.Max(row.MinimumHeight, Scale(row.Height));
        if (grid.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize)
            grid.ColumnHeadersHeight = Scale(grid.ColumnHeadersHeight);
        if (grid.RowHeadersVisible)
            grid.RowHeadersWidth = Scale(grid.RowHeadersWidth);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, object> ScaledGrids = new();
}

/// <summary>
/// A window built in code at 96-DPI pixel sizes and scaled once to the display scale just before it is first
/// shown (see <see cref="DpiScaling"/>). After scaling it is re-centred and kept on the working area.
/// </summary>
public class DpiScaledForm : Form
{
    private bool _scaledToDisplay;

    protected override void OnLoad(EventArgs e)
    {
        if (!_scaledToDisplay)
        {
            _scaledToDisplay = true;
            if (DpiScaling.IsScaled)
            {
                DpiScaling.ScaleControlTree(this);
                var area = Screen.FromControl(this).WorkingArea;
                if (Width > area.Width || Height > area.Height)
                    Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                if (StartPosition == FormStartPosition.CenterParent && Owner is not null)
                    CenterToParent();
                else if (StartPosition is FormStartPosition.CenterScreen or FormStartPosition.CenterParent)
                    CenterToScreen();
            }
        }
        base.OnLoad(e);
    }
}
