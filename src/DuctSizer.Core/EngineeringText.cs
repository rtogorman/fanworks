using System.Text.RegularExpressions;

namespace DuctSizer.Core;

/// <summary>
/// Turns the plain-text engineering notation used in worksheet substitutions ("rho = 0.075 lbm/ft3; T = 55 deg F")
/// into typeset notation ("ρ = 0.075 lbm/ft³; T = 55 °F") for display. Values and their order are unchanged;
/// only symbol and unit spellings are replaced, so the text a reviewer checks stays the same text.
/// </summary>
public static class EngineeringText
{
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    {
        (new Regex(@"\bdeg ?F\b", RegexOptions.Compiled), "°F"),
        (new Regex(@"\bdeg ?C\b", RegexOptions.Compiled), "°C"),
        (new Regex(@"(?<=\d) ?deg\b(?! [FC]\b)", RegexOptions.Compiled), "°"),
        (new Regex(@"\b(ft|in|m|mm|cm)2\b", RegexOptions.Compiled), "$1²"),
        (new Regex(@"\b(ft|in|m|mm|cm)3\b", RegexOptions.Compiled), "$1³"),
        (new Regex(@"\bepsilon_r\b", RegexOptions.Compiled), "εr"),
        (new Regex(@"\bepsilon\b", RegexOptions.Compiled), "ε"),
        (new Regex(@"\brho\b", RegexOptions.Compiled), "ρ"),
        (new Regex(@"\bmu\b", RegexOptions.Compiled), "μ"),
        (new Regex(@"\btheta\b", RegexOptions.Compiled), "θ"),
        (new Regex(@"\bpi\b", RegexOptions.Compiled), "π"),
        (new Regex(@"\bDelta ?P", RegexOptions.Compiled), "ΔP"),
        (new Regex(@"\bDelta\b ?", RegexOptions.Compiled), "Δ"),
        (new Regex(@"(?<=\d) x (?=[\d(])", RegexOptions.Compiled), " × "),
        (new Regex(@" -> ", RegexOptions.Compiled), " → "),
        (new Regex(@" >= ", RegexOptions.Compiled), " ≥ "),
        (new Regex(@" <= ", RegexOptions.Compiled), " ≤ ")
    };

    /// <summary>The display form of <paramref name="text"/> (see the class summary).</summary>
    public static string Pretty(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var result = text;
        foreach (var (pattern, replacement) in Rules)
            result = pattern.Replace(result, replacement);
        return result;
    }
}
