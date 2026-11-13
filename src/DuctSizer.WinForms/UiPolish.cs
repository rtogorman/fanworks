using System.Drawing.Drawing2D;

namespace DuctSizer.WinForms;

internal static class UiPalette
{
    internal static readonly Color Canvas = Color.FromArgb(238, 243, 247);
    internal static readonly Color Surface = Color.FromArgb(253, 254, 255);
    internal static readonly Color SurfaceShade = Color.FromArgb(246, 249, 251);
    internal static readonly Color CardBorder = Color.FromArgb(184, 202, 215);
    internal static readonly Color CardHighlight = Color.FromArgb(255, 255, 255);
    internal static readonly Color CardShadow = Color.FromArgb(128, 151, 168);
    internal static readonly Color Navy = Color.FromArgb(11, 31, 59);
    internal static readonly Color Blue = Color.FromArgb(18, 52, 96);
    internal static readonly Color Teal = Color.FromArgb(14, 111, 230);
    internal static readonly Color Cyan = Color.FromArgb(126, 199, 255);
    internal static readonly Color Ink = Color.FromArgb(11, 31, 59);
}

internal sealed class PolishedTabControl : TabControl
{
    internal PolishedTabControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        Appearance = TabAppearance.FlatButtons;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(0, 1);
        Multiline = true;
    }
}

internal sealed class RoundedPanel : Panel
{
    internal int CornerRadius { get; set; } = 10;
    internal int BorderThickness { get; set; } = 1;
    internal Color BorderColor { get; set; } = UiPalette.CardBorder;
    internal bool Elevated { get; set; } = true;

    /// <summary>
    /// When false the panel paints its parent's colour in the corners instead of clipping to a window
    /// region, so the anti-aliased border is not cut off (used by the pop-up window cards).
    /// </summary>
    internal bool ClipToShape { get; set; } = true;

    internal RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        if (Width <= 1 || Height <= 1)
        {
            base.OnPaintBackground(eventArgs);
            return;
        }

        if (!ClipToShape)
        {
            using var corners = new SolidBrush(Parent?.BackColor ?? UiPalette.Surface);
            eventArgs.Graphics.FillRectangle(corners, ClientRectangle);
        }
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), CornerRadius);
        using var fill = new SolidBrush(UiPalette.Surface);
        eventArgs.Graphics.FillPath(fill, path);
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        UpdateRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (BorderThickness <= 0 || Width <= 1 || Height <= 1)
            return;
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiPolish.CreateRoundedPath(
            new RectangleF(BorderThickness / 2F, BorderThickness / 2F,
                Width - BorderThickness, Height - BorderThickness),
            CornerRadius);
        using var pen = new Pen(BorderColor, BorderThickness);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRoundedRegion()
    {
        if (Width <= 1 || Height <= 1 || !ClipToShape)
            return;
        using var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, Width, Height), CornerRadius);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }
}

internal sealed class ProjectIdentityHeaderPanel : TableLayoutPanel
{
    internal ProjectIdentityHeaderPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        // The top bar of the window: a flat dark band that holds the brand, the main tabs and the project identity.
        using var fill = new SolidBrush(RibbonPalette.TopBar);
        eventArgs.Graphics.FillRectangle(fill, ClientRectangle);
    }
}

/// <summary>
/// The title band of the Quick Calc calculators (round W): a rounded brand-navy band with the calculator's
/// icon on a primary-blue tile, a white title and a light-blue subtitle. It replaced a band with a motif of
/// horizontal lines and a version badge. The band lays out its own title and subtitle labels so they stay
/// beside the icon at any width and display scale.
/// </summary>
internal sealed class SectionTitlePanel : Panel
{
    internal static readonly Color SubtitleInk = Color.FromArgb(178, 218, 255);

    internal SectionTitlePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    /// <summary>The calculator's icon, drawn on the tile at the left of the band.</summary>
    internal RibbonIcon? Icon { get; set; }

    internal Label? TitleLabel { get; set; }

    internal Label? SubtitleLabel { get; set; }

    private Rectangle IconTile
    {
        get
        {
            var size = Math.Min(DpiScaling.Scale(34), Math.Max(0, ClientSize.Height - DpiScaling.Scale(12)));
            return new Rectangle(DpiScaling.Scale(14), (ClientSize.Height - size) / 2, size, size);
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var left = Icon is null ? DpiScaling.Scale(20) : IconTile.Right + DpiScaling.Scale(12);
        var width = Math.Max(DpiScaling.Scale(120), ClientSize.Width - left - DpiScaling.Scale(20));
        var titleHeight = TitleLabel is null ? 0 : TitleLabel.Font.Height + DpiScaling.Scale(2);
        var subtitleHeight = SubtitleLabel is null ? 0 : SubtitleLabel.Font.Height + DpiScaling.Scale(1);
        var top = Math.Max(0, (ClientSize.Height - titleHeight - subtitleHeight) / 2);
        TitleLabel?.SetBounds(left, top, width, titleHeight);
        SubtitleLabel?.SetBounds(left + DpiScaling.Scale(1), top + titleHeight, width, subtitleHeight);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.Clear(Parent?.BackColor ?? UiPalette.Canvas);
        if (ClientSize.Width < 4 || ClientSize.Height < 4)
            return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var band = new RectangleF(0, 0, ClientSize.Width - 1F, ClientSize.Height - 1F);
        using var path = UiPolish.CreateRoundedPath(band, DpiScaling.Scale(10F));
        using (var fill = new LinearGradientBrush(ClientRectangle, UiPalette.Navy, Color.FromArgb(22, 56, 102),
                   LinearGradientMode.Horizontal))
            graphics.FillPath(fill, path);

        // A soft light-blue glow at the right end gives the band depth without drawing lines across it.
        var state = graphics.Save();
        graphics.SetClip(path);
        var glow = new RectangleF(ClientSize.Width - ClientSize.Height * 4F, -ClientSize.Height * 1.5F,
            ClientSize.Height * 5F, ClientSize.Height * 4F);
        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(glow);
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(46, UiPalette.Cyan),
                SurroundColors = new[] { Color.FromArgb(0, UiPalette.Cyan) }
            };
            graphics.FillEllipse(glowBrush, glow);
        }
        graphics.Restore(state);

        if (Icon is { } icon)
        {
            var tile = IconTile;
            using var tilePath = UiPolish.CreateRoundedPath(tile, DpiScaling.Scale(8F));
            using (var tileFill = new SolidBrush(UiPalette.Teal))
                graphics.FillPath(tileFill, tilePath);
            var inset = DpiScaling.Scale(6);
            RibbonIcons.Draw(graphics, icon, Rectangle.Inflate(tile, -inset, -inset), Color.White, UiPalette.Cyan);
        }
    }
}

internal sealed class CardHeaderPanel : Panel
{
    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        using var fill = new LinearGradientBrush(ClientRectangle,
            Color.White, Color.FromArgb(249, 251, 253), LinearGradientMode.Vertical);
        eventArgs.Graphics.FillRectangle(fill, ClientRectangle);
        using var line = new Pen(Color.FromArgb(218, 227, 234));
        eventArgs.Graphics.DrawLine(line, 12, Height - 1, Math.Max(12, Width - 12), Height - 1);
    }
}

internal sealed class BrandIconFramePanel : Panel
{
    internal BrandIconFramePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
    }
}

internal sealed class NavigationChromePanel : Panel
{
    internal NavigationChromePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var gradient = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(252, 254, 255), Color.FromArgb(226, 237, 244), LinearGradientMode.Vertical);
        eventArgs.Graphics.FillRectangle(gradient, ClientRectangle);
        using var topLine = new Pen(Color.White);
        using var bottomLine = new Pen(Color.FromArgb(145, 176, 197));
        eventArgs.Graphics.DrawLine(topLine, 0, 0, Width, 0);
        eventArgs.Graphics.DrawLine(bottomLine, 0, Height - 1, Width, Height - 1);

        using var lowerEdge = new Pen(Color.FromArgb(145, 176, 197));
        eventArgs.Graphics.DrawLine(lowerEdge, 0, Height - 1, Width, Height - 1);
    }
}

internal static class UiPolish
{
    internal static void ApplyDialogSurface(Form form)
    {
        form.Padding = Padding.Empty;
        form.BackColor = UiPalette.Canvas;
        // Inputs get the shared rounded look (see DialogStyle).
        DialogStyle.StyleInputs(form);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    internal static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1F, Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height)));
        var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void RoundButton(Button button, int radius = 8)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Lighten(button.BackColor, 10);
        button.FlatAppearance.MouseDownBackColor = Darken(button.BackColor, 10);
        void UpdateRegion()
        {
            if (button.Width <= 1 || button.Height <= 1)
                return;
            using var path = CreateRoundedPath(new RectangleF(0, 0, button.Width, button.Height), radius);
            var previous = button.Region;
            button.Region = new Region(path);
            previous?.Dispose();
        }
        button.Resize += (_, _) => UpdateRegion();
        button.HandleCreated += (_, _) => UpdateRegion();
        button.Paint += (_, args) =>
        {
            if (button.Width <= 2 || button.Height <= 2)
                return;
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedPath(
                new RectangleF(0.75F, 0.75F, button.Width - 1.5F, button.Height - 1.5F),
                Math.Max(2, radius - 1));
            var lightBackground = button.BackColor.GetBrightness() >= 0.72F;
            var borderColor = lightBackground ? Color.FromArgb(157, 179, 195) : Darken(button.BackColor, 25);
            using var pen = new Pen(borderColor, 1.25F);
            args.Graphics.DrawPath(pen, path);
        };
        UpdateRegion();
    }

    internal static void ApplyHistoryGlyph(Button button, bool redo)
    {
        button.Text = string.Empty;
        button.AccessibleName = redo ? "Redo" : "Undo";
        button.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var color = button.Enabled ? UiPalette.Ink : Color.FromArgb(145, 158, 169);
            using var pen = new Pen(color, 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            var state = args.Graphics.Save();
            if (redo)
            {
                args.Graphics.TranslateTransform(button.ClientSize.Width, 0);
                args.Graphics.ScaleTransform(-1, 1);
            }
            var width = button.ClientSize.Width;
            var height = button.ClientSize.Height;
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddBezier(width * .73F, height * .72F,
                width * .78F, height * .43F,
                width * .58F, height * .29F,
                width * .34F, height * .37F);
            args.Graphics.DrawPath(pen, path);
            args.Graphics.DrawLine(pen, width * .34F, height * .37F, width * .45F, height * .23F);
            args.Graphics.DrawLine(pen, width * .34F, height * .37F, width * .48F, height * .46F);
            args.Graphics.Restore(state);
        };
        button.EnabledChanged += (_, _) => button.Invalidate();
    }

    internal static Color Lighten(Color color, int amount) => Color.FromArgb(
        color.A,
        Math.Min(255, color.R + amount),
        Math.Min(255, color.G + amount),
        Math.Min(255, color.B + amount));

    internal static Color Darken(Color color, int amount) => Color.FromArgb(
        color.A,
        Math.Max(0, color.R - amount),
        Math.Max(0, color.G - amount),
        Math.Max(0, color.B - amount));
}
