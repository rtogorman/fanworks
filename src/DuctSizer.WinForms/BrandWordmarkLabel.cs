using System.Drawing.Drawing2D;

namespace DuctSizer.WinForms;

/// <summary>
/// The "fan works" wordmark in the top bar, drawn as glyph outlines in the brand typeface so its letters can be as
/// tall as the application icon beside it (the ascenders of "f" and "k" to the baseline fill
/// <see cref="GlyphHeight"/>) without the line spacing of a large font making the top bar taller.
/// </summary>
internal sealed class BrandWordmarkLabel : Control
{
    /// <summary>Height of the letters (ascender to baseline) at 100 % display scale, in pixels.</summary>
    internal const int GlyphHeight = 30;

    internal BrandWordmarkLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    /// <summary>Sizes the control to the wordmark at the current display scale (its table column follows).</summary>
    internal void FitToWordmark() => Size = GetPreferredSize(Size.Empty);

    /// <summary>The letter height in pixels at the current display scale, never taller than the control allows.</summary>
    internal float ScaledGlyphHeight => Math.Min(DpiScaling.Scale(GlyphHeight), Math.Max(8, ClientSize.Height - DpiScaling.Scale(6)));

    public override Size GetPreferredSize(Size proposedSize)
    {
        var (bounds, _) = Outline(DpiScaling.Scale(GlyphHeight));
        return new Size((int)Math.Ceiling(bounds.Width) + Padding.Horizontal + DpiScaling.Scale(4),
            DpiScaling.Scale(GlyphHeight) + DpiScaling.Scale(6));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (BackColor.A > 0 && BackColor != Color.Transparent)
            e.Graphics.Clear(BackColor);
        var (bounds, path) = Outline(ScaledGlyphHeight);
        using (path)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var move = new Matrix();
            move.Translate(Padding.Left - bounds.Left, (ClientSize.Height - bounds.Height) / 2F - bounds.Top);
            path.Transform(move);
            using var brush = new SolidBrush(ForeColor);
            e.Graphics.FillPath(brush, path);
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        FitToWordmark();
        Invalidate();
    }

    /// <summary>The text outline scaled so its letters are <paramref name="glyphHeight"/> pixels tall.</summary>
    private (RectangleF Bounds, GraphicsPath Path) Outline(float glyphHeight)
    {
        var path = new GraphicsPath();
        var family = BrandFonts.Family;
        var ownsFamily = family is null;
        family ??= new FontFamily("Segoe UI");
        try
        {
            var style = ownsFamily ? FontStyle.Bold : FontStyle.Regular;
            path.AddString(string.IsNullOrEmpty(Text) ? " " : Text, family, (int)style, 100F, PointF.Empty,
                StringFormat.GenericTypographic);
        }
        finally
        {
            if (ownsFamily)
                family.Dispose();
        }
        var unscaled = path.GetBounds();
        if (unscaled.Height <= 0)
            return (RectangleF.Empty, path);
        var scale = glyphHeight / unscaled.Height;
        using var resize = new Matrix();
        resize.Scale(scale, scale);
        path.Transform(resize);
        return (path.GetBounds(), path);
    }
}
