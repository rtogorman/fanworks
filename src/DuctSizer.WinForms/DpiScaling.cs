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
        // WinForms scales "no limit" (0) in a minimum or maximum size as if it were a length, which left controls
        // with a maximum height of a couple of pixels. Remember the unlimited sides and restore them afterwards.
        var limits = new List<(Control Control, Size Minimum, Size Maximum)>();
        CollectSizeLimits(root, limits);
        root.Scale(new SizeF(Factor, Factor));
        foreach (var (control, minimum, maximum) in limits)
        {
            control.MaximumSize = new Size(maximum.Width == 0 ? 0 : Scale(maximum.Width), maximum.Height == 0 ? 0 : Scale(maximum.Height));
            control.MinimumSize = new Size(minimum.Width == 0 ? 0 : Scale(minimum.Width), minimum.Height == 0 ? 0 : Scale(minimum.Height));
        }
        ScaleExtras(root);
        // Docked children of containers that were not laid out after scaling (pages not yet shown) keep their
        // scaled size; lay every container out again so they fit their parents.
        RelayoutTree(root);
    }

    private static void CollectSizeLimits(Control control, List<(Control, Size, Size)> limits)
    {
        if (control is not Form && (!control.MinimumSize.IsEmpty || !control.MaximumSize.IsEmpty))
            limits.Add((control, control.MinimumSize, control.MaximumSize));
        foreach (Control child in control.Controls)
            CollectSizeLimits(child, limits);
    }

    private static void RelayoutTree(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls)
            RelayoutTree(child);
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

    /// <summary>Whether the shared pop-up field layout (<see cref="DialogStyle"/>) applies. The main window lays out its own pages.</summary>
    protected virtual bool UsesDialogStyle => true;

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
        if (UsesDialogStyle)
        {
            // The FanWorks icon instead of the default window icon, and the shared field and input styling.
            if (WindowLook.ApplicationIcon is Icon icon)
            {
                Icon = icon;
                ShowIcon = true;
            }
            DialogStyle.Attach(this);
        }
        base.OnLoad(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowLook.ApplyTitleBar(this);
    }
}

/// <summary>Window chrome shared by every FanWorks window: the application icon and the title-bar colours.</summary>
internal static class WindowLook
{
    /// <summary>FanWorks navy, used for the title bar of every window (Windows 11; earlier Windows keep the system colour).</summary>
    internal static readonly Color TitleBar = Color.FromArgb(24, 62, 92);

    private static Icon? _icon;
    private static bool _iconLoaded;

    internal static Icon? ApplicationIcon
    {
        get
        {
            if (_iconLoaded)
                return _icon;
            _iconLoaded = true;
            try
            {
                using var stream = typeof(WindowLook).Assembly.GetManifestResourceStream(
                    "DuctSizer.WinForms.Assets.DuctSizerCentrifugalIcon.ico");
                if (stream is not null)
                    _icon = new Icon(stream);
            }
            catch (ArgumentException)
            {
                _icon = null;
            }
            return _icon;
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private const int BorderColorAttribute = 34;
    private const int CaptionColorAttribute = 35;
    private const int TextColorAttribute = 36;

    /// <summary>Sets the title bar to FanWorks navy with white text (no effect where Windows does not support it).</summary>
    internal static void ApplyTitleBar(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || !form.IsHandleCreated)
            return;
        try
        {
            var caption = ToColorRef(TitleBar);
            var text = ToColorRef(Color.White);
            DwmSetWindowAttribute(form.Handle, CaptionColorAttribute, ref caption, sizeof(int));
            DwmSetWindowAttribute(form.Handle, TextColorAttribute, ref text, sizeof(int));
            DwmSetWindowAttribute(form.Handle, BorderColorAttribute, ref caption, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older Windows: the system title bar stays.
        }
    }

    private static int ToColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
}
