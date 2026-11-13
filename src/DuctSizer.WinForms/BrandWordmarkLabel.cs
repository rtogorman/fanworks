using System.Drawing.Drawing2D;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The "fan works" wordmark in the top bar, drawn from the brand board's own letter outlines so its letters can be as
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

    /// <summary>
    /// The wordmark outline (<see cref="BrandWordmark"/>: the brand board's own letters) scaled so its letters are
    /// <paramref name="glyphHeight"/> pixels tall, with its bounds.
    /// </summary>
    private static (RectangleF Bounds, GraphicsPath Path) Outline(float glyphHeight)
    {
        var path = new GraphicsPath(FillMode.Alternate);
        var scale = (float)(glyphHeight / BrandWordmark.Height);
        PointF P(double x, double y) => new((float)x * scale, (float)y * scale);
        var current = PointF.Empty;
        foreach (var (op, args) in BrandWordmark.Commands)
        {
            switch (op)
            {
                case 'M':
                    path.StartFigure();
                    current = P(args[0], args[1]);
                    break;
                case 'L':
                    var next = P(args[0], args[1]);
                    path.AddLine(current, next);
                    current = next;
                    break;
                case 'C':
                    var end = P(args[4], args[5]);
                    path.AddBezier(current, P(args[0], args[1]), P(args[2], args[3]), end);
                    current = end;
                    break;
                case 'Z':
                    path.CloseFigure();
                    break;
            }
        }
        return (new RectangleF(0, 0, (float)BrandWordmark.Width * scale, glyphHeight), path);
    }
}
