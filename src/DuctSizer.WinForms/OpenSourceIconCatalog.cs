using System.Drawing.Imaging;
using System.Reflection;

namespace DuctSizer.WinForms;

/// <summary>
/// Draws the vendored layout-designer icon set. Most component symbols use the
/// supplied blue engineering-toolbox artwork; remaining legacy glyphs retain
/// the prior single-color treatment.
/// </summary>
internal static class OpenSourceIconCatalog
{
    private static readonly Dictionary<string, Image> Images = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ToolboxArtwork = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan", "duct", "coil", "filter", "elbow", "transition", "diverging", "converging",
        "damper", "terminal", "space-source", "space-sink", "sensor-airflow", "sensor-pressure", "other"
    };
    private static readonly HashSet<string> UnifiedBlueArtwork = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan", "space-source", "space-sink"
    };
    private static readonly object Sync = new();

    internal static bool Draw(Graphics graphics, string key, Rectangle bounds, Color color)
    {
        var image = GetImage(key);
        if (image is null)
            return false;

        var inset = Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 18);
        var target = Rectangle.Inflate(bounds, -inset, -inset);
        if (ToolboxArtwork.Contains(key))
        {
            target = FitRectangle(target, image.Size);
            var state = graphics.Save();
            if (string.Equals(key, "space-source", StringComparison.OrdinalIgnoreCase))
            {
                graphics.TranslateTransform(target.Left + target.Width / 2F, target.Top + target.Height / 2F);
                graphics.RotateTransform(180F);
                graphics.TranslateTransform(-(target.Left + target.Width / 2F), -(target.Top + target.Height / 2F));
            }
            graphics.DrawImage(image, target);
            graphics.Restore(state);
            if (color.R > 150 && color.G < 150)
            {
                using var statusPen = new Pen(color,
                    Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 15F, 1.8F, 3.2F))
                {
                    DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                };
                graphics.DrawRectangle(statusPen, Rectangle.Inflate(bounds, -1, -1));
            }
            return true;
        }
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[]
        {
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { 0F, 0F, 0F, 1F, 0F },
            new[] { color.R / 255F, color.G / 255F, color.B / 255F, 0F, 1F }
        }));
        graphics.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        return true;
    }

    private static Image? GetImage(string key)
    {
        lock (Sync)
        {
            if (Images.TryGetValue(key, out var cached))
                return cached;

            var resourceName = $"DuctSizer.WinForms.Assets.OpenSourceIcons.png.{key}.png";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;
            using var source = Image.FromStream(stream);
            using var sourceBitmap = new Bitmap(source);
            Image image;
            if (ToolboxArtwork.Contains(key))
            {
                using var trimmed = TrimTransparent(sourceBitmap);
                image = UnifiedBlueArtwork.Contains(key) ? RecolorBlue(trimmed) : new Bitmap(trimmed);
            }
            else
                image = new Bitmap(sourceBitmap);
            Images[key] = image;
            return image;
        }
    }

    private static Bitmap RecolorBlue(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (pixel.A == 0)
                    continue;
                var luminance = .299 * pixel.R + .587 * pixel.G + .114 * pixel.B;
                if (luminance < 48)
                {
                    result.SetPixel(x, y, Color.FromArgb(pixel.A, 5, 44, 72));
                    continue;
                }
                var blend = Math.Clamp((luminance - 48) / 207.0, 0.0, 1.0);
                result.SetPixel(x, y, Color.FromArgb(pixel.A,
                    (int)Math.Round(24 + 83 * blend),
                    (int)Math.Round(107 + 93 * blend),
                    (int)Math.Round(168 + 75 * blend)));
            }
        }
        return result;
    }

    private static Rectangle FitRectangle(Rectangle bounds, Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
            return bounds;

        var scale = Math.Min(bounds.Width / (float)imageSize.Width, bounds.Height / (float)imageSize.Height);
        var width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
        var height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
        return new Rectangle(
            bounds.Left + (bounds.Width - width) / 2,
            bounds.Top + (bounds.Height - height) / 2,
            width,
            height);
    }

    private static Bitmap TrimTransparent(Bitmap source)
    {
        var left = source.Width;
        var top = source.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (source.GetPixel(x, y).A <= 8)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
            return new Bitmap(source);

        const int padding = 3;
        left = Math.Max(0, left - padding);
        top = Math.Max(0, top - padding);
        right = Math.Min(source.Width - 1, right + padding);
        bottom = Math.Min(source.Height - 1, bottom + padding);
        return source.Clone(
            Rectangle.FromLTRB(left, top, right + 1, bottom + 1),
            PixelFormat.Format32bppArgb);
    }
}
