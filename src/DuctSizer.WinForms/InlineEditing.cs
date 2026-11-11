namespace DuctSizer.WinForms;

/// <summary>One editable input of an item, shown in the Selection Inspector with the editor's own control.</summary>
/// <param name="Label">Caption shown to the left of the control (sentence case, with the unit).</param>
/// <param name="Input">The editor's control (text box, drop-down, or a small composite such as X/Y/Z).</param>
/// <param name="IsShown">Whether the row applies at the moment (for example Diameter only for round ducts).</param>
internal sealed record InlineEditRow(string Label, Control Input, Func<bool>? IsShown = null)
{
    /// <summary>For a position row: the X, Y and Z boxes, laid out by the inspector with a label beside each.</summary>
    internal TextBox[]? Axes { get; init; }

    /// <summary>Labels of the three boxes ("ΔX", "ΔY", "ΔZ" for an offset; "X", "Y", "Z" for a location).</summary>
    internal string[] AxisLabels { get; init; } = { "X", "Y", "Z" };

    /// <summary>
    /// A position row from the editor's caption: "OFFSET FROM C-039 ΔX / ΔY / ΔZ (ft)" becomes the caption
    /// "Offset from C-039 (ft)" with the boxes labelled ΔX, ΔY and ΔZ.
    /// </summary>
    internal static InlineEditRow ForPosition(string editorCaption, Control group, TextBox x, TextBox y, TextBox z)
    {
        var offset = editorCaption.Contains('Δ', StringComparison.Ordinal)
                     || editorCaption.Contains("DELTA", StringComparison.OrdinalIgnoreCase)
                     || editorCaption.Contains("OFFSET", StringComparison.OrdinalIgnoreCase);
        var caption = editorCaption.Replace("ΔX / ΔY / ΔZ", string.Empty, StringComparison.Ordinal)
            .Replace("X / Y / Z", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("  ", " ", StringComparison.Ordinal).Trim();
        return new InlineEditRow(InlineText.SentenceCase(caption), group)
        {
            Axes = new[] { x, y, z },
            AxisLabels = offset ? new[] { "ΔX", "ΔY", "ΔZ" } : new[] { "X", "Y", "Z" }
        };
    }

    internal bool Shown => IsShown?.Invoke() ?? true;
}

/// <summary>
/// An editor window whose inputs can also be edited in place in the Selection Inspector. The inspector hosts the
/// editor's own controls, so every rule of the editor (which inputs apply, drop-down choices, unit handling and
/// validation) is the same whether the user edits in the window or in the inspector.
/// </summary>
internal interface IInlineEditable
{
    /// <summary>The editable rows, in display order.</summary>
    IReadOnlyList<InlineEditRow> InlineRows { get; }

    /// <summary>Raised when rows appear, disappear or are replaced (for example a new fitting type).</summary>
    event Action? InlineRowsChanged;

    /// <summary>A line for calculation previews and validation messages, or null.</summary>
    Control? InlineStatus { get; }

    /// <summary>Switches wording meant for the window (such as "click Calculate") to wording for the inspector.</summary>
    void UseInline(IWin32Window messageOwner);

    /// <summary>Runs the same checks as the editor's OK / Update button. False leaves the project unchanged.</summary>
    bool TryAcceptInline();
}

/// <summary>
/// An edit of one selected item from the Selection Inspector: its editors, the button caption, and the step that
/// applies the accepted editor results to the project (the same code the editor windows use).
/// </summary>
internal sealed class InlineEditSession : IDisposable
{
    private readonly Func<string, bool> _apply;
    private bool _disposed;

    /// <param name="updateText">Caption of the commit button, for example "Update Fitting".</param>
    /// <param name="editors">Editors in the order their checks run (also their display order).</param>
    /// <param name="apply">Applies the accepted results; receives the undo state captured just before the checks.
    /// Returns false when the user declined a confirmation (nothing was changed).</param>
    internal InlineEditSession(string updateText, IReadOnlyList<IInlineEditable> editors, Func<string, bool> apply)
    {
        UpdateText = updateText;
        Editors = editors;
        _apply = apply;
    }

    internal string UpdateText { get; }

    internal IReadOnlyList<IInlineEditable> Editors { get; }

    /// <summary>Extra commands for this item (for example Reverse Flow Direction).</summary>
    internal List<(string Name, string Text, Action Run)> Actions { get; } = new();

    /// <summary>Captures the undo state used if the edit is applied.</summary>
    internal Func<string>? CaptureHistory { get; init; }

    /// <summary>
    /// Puts the project back to the captured state when the apply step declined after an editor had already
    /// written to the project (the component editor writes while it checks).
    /// </summary>
    internal Action<string>? RestoreWhenDeclined { get; init; }

    /// <summary>Checks every editor, then applies the edit. False when a check failed or the user declined.</summary>
    internal bool Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Undo state is taken before any editor writes to the project (the component editor writes on accept).
        var history = CaptureHistory?.Invoke() ?? string.Empty;
        foreach (var editor in Editors)
        {
            if (!editor.TryAcceptInline())
                return false;
        }
        if (_apply(history))
            return true;
        if (!string.IsNullOrEmpty(history))
            RestoreWhenDeclined?.Invoke(history);
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var editor in Editors)
            (editor as IDisposable)?.Dispose();
    }
}

internal static class InlineText
{
    /// <summary>
    /// "Space Temperature (°F)" → "Space temperature (°F)"; acronyms such as GRD stay in capitals and the text in
    /// brackets (units) is kept as written. Used to show the editor windows' captions in the inspector's style.
    /// </summary>
    internal static string SentenceCase(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return caption;
        var bracket = caption.IndexOf('(', StringComparison.Ordinal);
        var head = bracket >= 0 ? caption[..bracket] : caption;
        var tail = bracket >= 0 ? caption[bracket..] : string.Empty;
        var shouting = head.Split(' ').Where(word => !word.Any(char.IsDigit)).SelectMany(word => word.Where(char.IsLetter))
            .All(char.IsUpper);
        var words = head.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            var word = words[index];
            if (word.Length == 0)
                continue;
            // Item IDs (Fan-001) and short acronyms (GRD, SP) stay as written.
            if (word.Any(char.IsDigit) || word.Any(character => character > 127)) // Fan-001, ΔX
                continue;
            var letters = word.Where(char.IsLetter).ToArray();
            var acronym = !shouting && letters.Length is > 1 and <= 3 && letters.All(char.IsUpper);
            if (acronym || letters.Length == 0)
                continue;
            var lower = word.ToLowerInvariant();
            words[index] = index == 0 ? char.ToUpperInvariant(lower[0]) + lower[1..] : lower;
        }
        return string.Join(' ', words) + tail;
    }
}
