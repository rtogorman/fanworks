namespace DuctSizer.WinForms;

/// <summary>
/// Switches used only by the automated UI verification program
/// (tests/DuctSizer.UiVerification). They replace the DUCTSIZER_* environment
/// variables that earlier versions read at run time, so a user's environment
/// can no longer silently suppress confirmations or redirect where the working
/// project is stored.
///
/// The class is internal; the test assembly reaches it through
/// InternalsVisibleTo. In the shipped application every value keeps its
/// default, which means normal interactive behaviour.
/// </summary>
internal static class AutomationOptions
{
    /// <summary>Overrides where the working project (duct-system.json) is stored. Null = %LOCALAPPDATA%\DuctSizer.</summary>
    public static string? SystemStoragePath { get; set; }

    /// <summary>When true, informational and confirmation dialogs take their default answer instead of waiting for a user.</summary>
    public static bool SuppressDialogs { get; set; }

    /// <summary>When true, layout coordinate prompts accept the proposed coordinates without showing a dialog.</summary>
    public static bool AcceptDefaultCoordinates { get; set; }

    /// <summary>
    /// Answer used for the duct-insertion prompt when <see cref="AcceptDefaultCoordinates"/> is set.
    /// Null keeps the previous automated default (place the item unconnected).
    /// </summary>
    public static string? DuctInsertionChoice { get; set; }

    /// <summary>When set, the green Save writes to this base path instead of showing a Save dialog.</summary>
    public static string? ExportBasePath { get; set; }

    /// <summary>When set, Open Project loads this file instead of showing an Open dialog.</summary>
    public static string? OpenProjectPath { get; set; }

    /// <summary>When set, Print Report writes the PDF here instead of showing a Save dialog (and does not open it).</summary>
    public static string? ReportPdfPath { get; set; }

    /// <summary>Restores every switch to its interactive default.</summary>
    public static void Reset()
    {
        SystemStoragePath = null;
        SuppressDialogs = false;
        AcceptDefaultCoordinates = false;
        DuctInsertionChoice = null;
        ExportBasePath = null;
        OpenProjectPath = null;
        ReportPdfPath = null;
    }
}
