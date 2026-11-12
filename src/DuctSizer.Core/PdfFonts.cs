using System.Text;

namespace DuctSizer.Core;

/// <summary>
/// Fonts used by the PDF report and their character widths, so text can be measured exactly rather than estimated.
/// The report embeds the brand typeface Nunito (SIL Open Font License; files in Fonts/) as TrueType fonts with
/// WinAnsi encoding, and uses the PDF standard Symbol font for Greek letters and mathematical signs (ρ, μ, ε, Δ, π,
/// θ, →, ≤ ...), so formulas use real typeset glyphs instead of hand-drawn shapes.
/// </summary>
internal static class PdfFonts
{
    /// <summary>Resource names in the content streams.</summary>
    internal enum Face { Regular, Bold, Italic, Symbol, Brand }

    internal static string ResourceName(Face face) => face switch
    {
        Face.Regular => "F1",
        Face.Bold => "F2",
        Face.Italic => "F3",
        Face.Symbol => "F4",
        _ => "F5"
    };

    // Advance widths (1/1000 em) of WinAnsi codes 32..255, read from the embedded Nunito files.
    private static readonly short[] RegularWidths =
    {
        258, 228, 392, 600, 600, 930, 693, 221, 317, 317, 450, 600, 228, 424, 228, 283,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 228, 228, 600, 600, 600, 443,
        946, 729, 676, 673, 742, 583, 548, 726, 761, 257, 324, 625, 543, 855, 740, 767,
        633, 767, 669, 615, 602, 728, 689, 1101, 650, 596, 589, 315, 283, 315, 600, 500,
        356, 530, 583, 463, 583, 532, 333, 586, 568, 232, 236, 500, 296, 856, 568, 556,
        583, 583, 358, 481, 350, 561, 515, 841, 525, 514, 463, 352, 265, 352, 600, 0,
        600, 0, 228, 0, 394, 686, 0, 0, 472, 0, 0, 260, 1046, 0, 0, 0,
        0, 228, 228, 394, 394, 524, 500, 1000, 555, 925, 0, 260, 910, 0, 0, 0,
        258, 228, 600, 600, 600, 600, 265, 545, 467, 815, 327, 441, 600, 424, 815, 501,
        371, 600, 380, 380, 356, 600, 588, 228, 402, 380, 339, 441, 893, 937, 893, 443,
        729, 729, 729, 729, 729, 729, 981, 673, 583, 583, 583, 583, 257, 257, 257, 257,
        742, 740, 767, 767, 767, 767, 767, 600, 767, 728, 728, 728, 728, 596, 634, 615,
        530, 530, 530, 530, 530, 530, 861, 463, 532, 532, 532, 532, 232, 232, 232, 232,
        560, 568, 556, 556, 556, 556, 556, 600, 556, 561, 561, 561, 561, 514, 583, 514,
    };

    private static readonly short[] BoldWidths =
    {
        271, 248, 448, 600, 600, 945, 726, 243, 358, 358, 453, 600, 248, 434, 248, 313,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 248, 248, 600, 600, 600, 459,
        950, 744, 688, 680, 762, 597, 562, 736, 773, 282, 354, 665, 562, 868, 748, 785,
        652, 785, 686, 631, 621, 738, 713, 1113, 672, 618, 605, 354, 313, 354, 600, 500,
        377, 547, 600, 472, 600, 542, 364, 604, 585, 255, 259, 536, 319, 877, 585, 576,
        600, 600, 392, 488, 384, 579, 527, 853, 546, 526, 474, 391, 288, 391, 600, 0,
        600, 0, 248, 0, 443, 745, 0, 0, 494, 0, 0, 287, 1056, 0, 0, 0,
        0, 248, 248, 443, 443, 524, 500, 1000, 582, 954, 0, 287, 915, 0, 0, 0,
        271, 248, 600, 600, 600, 600, 288, 570, 512, 815, 339, 494, 600, 434, 815, 525,
        379, 600, 380, 380, 377, 600, 624, 248, 421, 380, 353, 494, 912, 960, 912, 459,
        744, 744, 744, 744, 744, 744, 993, 680, 597, 597, 597, 597, 282, 282, 282, 282,
        762, 748, 785, 785, 785, 785, 785, 600, 785, 738, 738, 738, 738, 618, 655, 653,
        547, 547, 547, 547, 547, 547, 865, 472, 542, 542, 542, 542, 255, 255, 255, 255,
        581, 585, 576, 576, 576, 576, 576, 600, 576, 579, 579, 579, 579, 526, 600, 526,
    };

    private static readonly short[] ItalicWidths =
    {
        258, 228, 392, 600, 600, 930, 693, 221, 317, 317, 450, 600, 228, 424, 228, 283,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 228, 228, 600, 600, 600, 443,
        946, 729, 676, 673, 742, 583, 548, 726, 761, 257, 324, 625, 543, 856, 739, 767,
        633, 767, 669, 615, 602, 728, 688, 1101, 650, 596, 589, 315, 283, 315, 600, 500,
        392, 530, 583, 463, 583, 532, 333, 586, 568, 232, 236, 500, 296, 856, 568, 556,
        583, 583, 358, 481, 350, 561, 515, 841, 525, 514, 463, 352, 265, 352, 600, 0,
        600, 0, 228, 0, 394, 686, 0, 0, 472, 0, 0, 260, 1046, 0, 0, 0,
        0, 228, 228, 394, 394, 524, 500, 1000, 544, 925, 0, 260, 910, 0, 0, 0,
        258, 228, 600, 600, 600, 600, 265, 545, 467, 815, 327, 441, 600, 424, 815, 510,
        371, 600, 380, 380, 354, 600, 588, 228, 399, 380, 339, 441, 893, 937, 893, 443,
        729, 729, 729, 729, 729, 729, 981, 673, 583, 583, 583, 583, 257, 257, 257, 257,
        742, 739, 767, 767, 767, 767, 767, 600, 767, 728, 728, 728, 728, 596, 634, 615,
        530, 530, 530, 530, 530, 530, 861, 463, 532, 532, 532, 532, 232, 232, 232, 232,
        560, 568, 556, 556, 556, 556, 556, 600, 556, 561, 561, 561, 561, 514, 583, 514,
    };

    private static readonly short[] BrandWidths =
    {
        279, 260, 481, 600, 600, 954, 745, 256, 382, 382, 454, 600, 260, 440, 260, 331,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 260, 260, 600, 600, 600, 468,
        952, 753, 695, 684, 774, 605, 570, 742, 780, 297, 372, 688, 573, 876, 753, 796,
        664, 796, 696, 641, 632, 743, 727, 1120, 685, 631, 615, 377, 331, 377, 600, 500,
        388, 557, 610, 478, 610, 549, 382, 615, 595, 268, 273, 558, 333, 889, 595, 589,
        610, 610, 412, 492, 404, 590, 534, 861, 559, 533, 480, 414, 302, 414, 600, 0,
        600, 0, 260, 0, 472, 780, 0, 0, 508, 0, 0, 303, 1062, 0, 0, 0,
        0, 260, 260, 472, 472, 524, 500, 1000, 597, 971, 0, 303, 918, 0, 0, 0,
        279, 260, 600, 600, 600, 600, 302, 584, 538, 815, 346, 525, 600, 440, 815, 539,
        384, 600, 380, 380, 388, 600, 645, 260, 432, 380, 361, 525, 923, 973, 923, 468,
        753, 753, 753, 753, 753, 753, 1000, 684, 605, 605, 605, 605, 297, 297, 297, 297,
        774, 753, 796, 796, 796, 796, 796, 600, 796, 743, 743, 743, 743, 631, 667, 676,
        557, 557, 557, 557, 557, 557, 868, 478, 549, 549, 549, 549, 268, 268, 268, 268,
        593, 595, 589, 589, 589, 589, 589, 600, 589, 590, 590, 590, 590, 533, 610, 533,
    };

    /// <summary>Font file, PostScript name and descriptor values for each embedded face.</summary>
    internal sealed record EmbeddedFace(Face Face, string Resource, string PostScriptName, short[] Widths,
        int Ascent, int Descent, int CapHeight, int[] BoundingBox, double ItalicAngle, int Flags, int StemV);

    internal static readonly IReadOnlyList<EmbeddedFace> Embedded = new[]
    {
        new EmbeddedFace(Face.Regular, "Nunito-Regular.ttf", "Nunito-Regular", RegularWidths, 1011, -353, 705,
            new[] { -246, -195, 1065, 1039 }, 0.0, 32, 80),
        new EmbeddedFace(Face.Bold, "Nunito-Bold.ttf", "Nunito-Bold", BoldWidths, 1011, -353, 705,
            new[] { -260, -209, 1080, 1047 }, 0.0, 262176, 140),
        new EmbeddedFace(Face.Italic, "Nunito-Italic.ttf", "Nunito-Italic", ItalicWidths, 1011, -353, 705,
            new[] { -295, -197, 1131, 1039 }, -9.0, 96, 80),
        new EmbeddedFace(Face.Brand, "Nunito-ExtraBold.ttf", "Nunito-ExtraBold", BrandWidths, 1011, -353, 705,
            new[] { -268, -219, 1089, 1052 }, 0.0, 262176, 165),
    };

    /// <summary>The font file bytes (embedded resource of this assembly).</summary>
    internal static byte[] FontFile(EmbeddedFace face)
    {
        var assembly = typeof(PdfFonts).Assembly;
        var name = assembly.GetManifestResourceNames()
            .First(resource => resource.EndsWith(face.Resource, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The brand typeface file (Nunito ExtraBold) for the application's wordmark.</summary>
    internal static byte[] BrandFontFile() => FontFile(Embedded.First(face => face.Face == Face.Brand));

    // Symbol font: Unicode -> (code, width) for the characters the report uses.
    // FanWorksSymbols (built from M PLUS Rounded 1c, SIL OFL; Fonts/FanWorksSymbols.ttf): Unicode -> (code, width).
    // The font maps these single-byte codes to the Greek letters and signs, so every viewer shows the same glyphs.
    private static readonly Dictionary<char, (byte Code, short Width)> SymbolGlyphs = new()
    {
        ['Α'] = (0x41, 639), ['Β'] = (0x42, 598), ['Γ'] = (0x47, 556), ['Δ'] = (0x44, 631), ['Θ'] = (0x51, 714), ['Λ'] = (0x4C, 639),
        ['Π'] = (0x50, 686), ['Σ'] = (0x53, 567), ['Φ'] = (0x46, 710), ['Ψ'] = (0x59, 774), ['Ω'] = (0x57, 770), ['α'] = (0x61, 608),
        ['β'] = (0x62, 580), ['γ'] = (0x67, 532), ['δ'] = (0x64, 556), ['ε'] = (0x65, 505), ['ζ'] = (0x7A, 540), ['η'] = (0x68, 565),
        ['θ'] = (0x71, 560), ['κ'] = (0x6B, 518), ['λ'] = (0x6C, 521), ['µ'] = (0x6D, 565), ['μ'] = (0x6D, 565), ['ν'] = (0x6E, 540), ['ξ'] = (0x78, 545),
        ['π'] = (0x70, 630), ['ρ'] = (0x72, 564), ['σ'] = (0x73, 608), ['τ'] = (0x74, 584), ['φ'] = (0x66, 674), ['χ'] = (0x63, 514),
        ['ψ'] = (0x79, 700), ['ω'] = (0x77, 780), ['→'] = (0xAE, 1000), ['←'] = (0xAC, 1000), ['⇒'] = (0xDE, 1000), ['≤'] = (0xA3, 754),
        ['≥'] = (0xB3, 754), ['≈'] = (0xBB, 744), ['≠'] = (0xB9, 754), ['√'] = (0xD6, 758), ['∞'] = (0xA5, 846), ['∑'] = (0xE5, 694),
        ['∂'] = (0xB6, 602), ['∝'] = (0xB5, 722), ['∫'] = (0xF2, 571), ['−'] = (0x2D, 734)
    };

    internal const short SymbolSpaceWidth = 270;

    /// <summary>The symbol font's code -> Unicode map, for the PDF ToUnicode table (copy and search).</summary>
    internal static IEnumerable<(byte Code, char Character)> SymbolCodes =>
        SymbolGlyphs.Where(pair => pair.Key != 'µ').Select(pair => (pair.Value.Code, pair.Key));

    /// <summary>Widths of codes 32..255 in the symbol font (0 where the font has no glyph).</summary>
    internal static short[] SymbolWidths()
    {
        var widths = new short[224];
        widths[0] = SymbolSpaceWidth;
        foreach (var (code, width) in SymbolGlyphs.Values)
            widths[code - 32] = width;
        return widths;
    }

    internal static readonly int[] SymbolBoundingBox = { 25, -230, 858, 810 };
    internal const int SymbolAscent = 1075, SymbolDescent = -320;

    /// <summary>True for characters set in the Symbol font (Greek letters and mathematical signs).</summary>
    internal static bool IsSymbolCharacter(char character) => SymbolGlyphs.ContainsKey(character);

    private static readonly Dictionary<char, byte> WinAnsiHigh = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87,
        ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91,
        ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98,
        ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B, ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F
    };

    private static byte WinAnsiCode(char character) =>
        character is >= ' ' and <= '~' or >= '\u00A0' and <= '\u00FF' ? (byte)character
        : WinAnsiHigh.TryGetValue(character, out var code) ? code : (byte)'?';

    /// <summary>A piece of text in one face: the escaped PDF string and its width at 1 pt.</summary>
    internal readonly record struct Run(Face Face, string PdfString, double WidthAtOnePoint);

    /// <summary>
    /// Splits <paramref name="text"/> into runs: WinAnsi characters in <paramref name="face"/>, Greek letters and
    /// mathematical signs in the Symbol font. Characters neither font has become '?'.
    /// </summary>
    internal static IReadOnlyList<Run> Runs(string text, Face face)
    {
        var runs = new List<Run>();
        var current = new StringBuilder();
        var currentFace = face;
        double width = 0;
        void Flush()
        {
            if (current.Length > 0)
                runs.Add(new Run(currentFace, current.ToString(), width / 1000.0));
            current.Clear();
            width = 0;
        }
        foreach (var raw in text)
        {
            var character = raw switch { '\t' => ' ', '\u2009' or '\u202F' or '\u00A0' => ' ', _ => raw };
            if (face != Face.Symbol && SymbolGlyphs.TryGetValue(character, out var glyph) && character != '-')
            {
                if (currentFace != Face.Symbol) { Flush(); currentFace = Face.Symbol; }
                current.Append(Escape(glyph.Code));
                width += glyph.Width;
                continue;
            }
            if (currentFace == Face.Symbol && face != Face.Symbol) { Flush(); currentFace = face; }
            var code = WinAnsiCode(character);
            if (code < 32) code = (byte)' ';
            var advance = face == Face.Symbol ? (short)(code == 32 ? SymbolSpaceWidth : 0) : WidthOf(code, face);
            if (advance == 0) { code = (byte)'?'; advance = WidthOf(code, face); }
            current.Append(Escape(code));
            width += advance;
        }
        Flush();
        return runs;
    }

    /// <summary>Width of <paramref name="text"/> in points at <paramref name="size"/>.</summary>
    internal static double Measure(string text, double size, Face face) =>
        Runs(text, face).Sum(run => run.WidthAtOnePoint) * size;

    private static short WidthOf(byte code, Face face)
    {
        var table = face switch
        {
            Face.Bold => BoldWidths,
            Face.Italic => ItalicWidths,
            Face.Brand => BrandWidths,
            _ => RegularWidths
        };
        return code is >= 32 ? table[code - 32] : (short)0;
    }

    private static string Escape(byte code) => code switch
    {
        (byte)'(' => "\\(",
        (byte)')' => "\\)",
        (byte)'\\' => "\\\\",
        >= 32 and < 127 => ((char)code).ToString(),
        _ => "\\" + Convert.ToString(code, 8).PadLeft(3, '0')
    };

    /// <summary>
    /// Content-stream operators that draw <paramref name="text"/> at (x, y): one BT/ET block, switching between
    /// the text face and the Symbol font as needed.
    /// </summary>
    internal static string Show(string text, double x, double y, double size, Face face)
    {
        var builder = new StringBuilder();
        builder.Append(FormattableString.Invariant($"BT {x:0.##} {y:0.##} Td "));
        foreach (var run in Runs(text, face))
            builder.Append(FormattableString.Invariant($"/{ResourceName(run.Face)} {size:0.##} Tf ({run.PdfString}) Tj "));
        builder.Append("ET\n");
        return builder.ToString();
    }
}

/// <summary>Brand assets shared with the application (the report embeds the same files).</summary>
public static class BrandAssets
{
    /// <summary>The "fan works" wordmark typeface: Nunito ExtraBold (TrueType, SIL Open Font License).</summary>
    public static byte[] WordmarkFont() => PdfFonts.BrandFontFile();
}
