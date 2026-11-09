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
    internal static readonly Color Navy = Color.FromArgb(8, 32, 50);
    internal static readonly Color Blue = Color.FromArgb(19, 72, 108);
    internal static readonly Color Teal = Color.FromArgb(21, 144, 156);
    internal static readonly Color Cyan = Color.FromArgb(55, 213, 221);
    internal static readonly Color Ink = Color.FromArgb(30, 55, 78);
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
        if (Width <= 1 || Height <= 1)
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
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var gradient = new LinearGradientBrush(ClientRectangle,
                   Color.FromArgb(4, 25, 42), Color.FromArgb(14, 69, 91), LinearGradientMode.Horizontal))
            graphics.FillRectangle(gradient, ClientRectangle);

        // The brand deck remains visually separate from project metadata while
        // the upper-to-lower line bank reads as diminishing airflow velocity.
        var leftDeck = new PointF[]
        {
            new(0, 0), new(Math.Min(330, Width * .29F), 0),
            new(Math.Min(390, Width * .34F), Height), new(0, Height)
        };
        using (var deck = new LinearGradientBrush(ClientRectangle,
                   Color.FromArgb(6, 36, 57), Color.FromArgb(9, 52, 70), LinearGradientMode.Horizontal))
            graphics.FillPolygon(deck, leftDeck);

        var lineEnd = Width - 18F;
        var maximumLength = Math.Max(120F, Width - 36F);
        const int lineCount = 7;
        for (var index = 0; index < lineCount; index++)
        {
            var progress = index / (lineCount - 1F);
            var length = maximumLength * (1F - progress * .26F);
            var alpha = (int)Math.Round(72 - progress * 46);
            var y = 12F + index * Math.Max(8F, (Height - 28F) / (lineCount - 1F));
            using var airflowLine = new Pen(Color.FromArgb(alpha, 73, 220, 226), index == 0 ? 2F : 1.25F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(airflowLine, lineEnd, y, lineEnd - length, y);
        }

        using var indexPen = new Pen(Color.FromArgb(32, 123, 209, 217), 1F);
        for (var x = lineEnd; x > lineEnd - maximumLength; x -= 32F)
        {
            graphics.DrawLine(indexPen, x, 5, x, 9);
        }

        using var lowerEdge = new Pen(Color.FromArgb(120, 5, 31, 48), 1F);
        graphics.DrawLine(lowerEdge, 0, Height - 1, Width, Height - 1);
    }
}

internal sealed class SectionTitlePanel : Panel
{
    internal SectionTitlePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(15, 58, 92), Color.FromArgb(24, 91, 122), LinearGradientMode.Horizontal);
        graphics.FillRectangle(fill, ClientRectangle);
        using var underlay = new SolidBrush(Color.FromArgb(30, 3, 28, 46));
        graphics.FillRectangle(underlay, 0, Height - 7, Width, 7);

        var motifRight = Width - 16F;
        var maximumLength = Math.Max(100F, Width - 32F);
        const int lineCount = 6;
        for (var index = 0; index < lineCount; index++)
        {
            var progress = index / (lineCount - 1F);
            var length = maximumLength * (1F - progress * .28F);
            var alpha = (int)Math.Round(62 - progress * 40);
            var y = 11F + index * Math.Max(9F, (Height - 27F) / (lineCount - 1F));
            using var line = new Pen(Color.FromArgb(alpha, 78, 222, 226), index == 0 ? 1.8F : 1.1F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(line, motifRight, y, motifRight - length, y);
        }
        using var edge = new Pen(Color.FromArgb(128, 8, 39, 61), 1F);
        graphics.DrawLine(edge, 0, Height - 1, Width, Height - 1);
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
        foreach (var control in Descendants(form))
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = UiPalette.Ink;
                    break;
                case ComboBox comboBox:
                    comboBox.FlatStyle = FlatStyle.Standard;
                    comboBox.BackColor = UiPalette.SurfaceShade;
                    comboBox.ForeColor = UiPalette.Ink;
                    break;
                case NumericUpDown numeric:
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    numeric.BackColor = Color.White;
                    numeric.ForeColor = UiPalette.Ink;
                    break;
            }
        }
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
