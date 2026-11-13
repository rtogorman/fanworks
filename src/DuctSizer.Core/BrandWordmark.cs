using System.Globalization;
using System.Text;

namespace DuctSizer.Core;

/// <summary>
/// The "fan works" wordmark as vector outlines, traced from the supplied FanWorks lettering (single-storey "a", no
/// spurs or serifs) so the application and the report draw exactly the brand letters rather than a similar font.
/// Coordinates are in wordmark units with the origin at the top-left of the letters' bounding box and y downwards;
/// the letters have no descenders, so the bottom of the box is the baseline. Fill with the even-odd rule.
/// </summary>
public static class BrandWordmark
{
    /// <summary>Width of the letters' bounding box in wordmark units.</summary>
    public const double Width = 828.40;

    /// <summary>Height of the letters (ascender top to baseline) in wordmark units.</summary>
    public const double Height = 151.76;

    private static readonly Lazy<IReadOnlyList<(char Op, double[] Args)>> Parsed = new(Parse);

    /// <summary>Path commands: 'M' (x, y), 'L' (x, y), 'C' (x1, y1, x2, y2, x, y) and 'Z'.</summary>
    public static IReadOnlyList<(char Op, double[] Args)> Commands => Parsed.Value;

    /// <summary>
    /// PDF content-stream operators that fill the wordmark in the current fill colour, its bottom-left corner at
    /// (<paramref name="x"/>, <paramref name="y"/>) in PDF points and its letters <paramref name="height"/> points tall.
    /// </summary>
    public static string PdfFill(double x, double y, double height)
    {
        var scale = height / Height;
        var builder = new StringBuilder();
        string P(double px, double py) => string.Create(CultureInfo.InvariantCulture,
            $"{x + px * scale:0.###} {y + (Height - py) * scale:0.###}");
        foreach (var (op, args) in Commands)
        {
            switch (op)
            {
                case 'M': builder.Append(P(args[0], args[1])).Append(" m "); break;
                case 'L': builder.Append(P(args[0], args[1])).Append(" l "); break;
                case 'C':
                    builder.Append(P(args[0], args[1])).Append(' ').Append(P(args[2], args[3])).Append(' ')
                        .Append(P(args[4], args[5])).Append(" c ");
                    break;
                case 'Z': builder.Append("h "); break;
            }
        }
        builder.Append("f*\n");
        return builder.ToString();
    }

    private static IReadOnlyList<(char Op, double[] Args)> Parse()
    {
        var assembly = typeof(BrandWordmark).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(resource => resource.EndsWith("wordmark.path", StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("The wordmark outline resource is missing.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var commands = new List<(char, double[])>();
        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];
            if (c is 'M' or 'L' or 'C' or 'Z')
            {
                index++;
                var count = c switch { 'M' or 'L' => 2, 'C' => 6, _ => 0 };
                var args = new double[count];
                for (var argument = 0; argument < count; argument++)
                {
                    while (index < text.Length && text[index] is ' ' or ',')
                        index++;
                    var start = index;
                    while (index < text.Length && (char.IsDigit(text[index]) || text[index] is '.' or '-'))
                        index++;
                    args[argument] = double.Parse(text.AsSpan(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                commands.Add((c, args));
            }
            else
                index++;
        }
        return commands;
    }
}
