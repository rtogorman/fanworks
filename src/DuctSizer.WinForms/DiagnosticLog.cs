using System.Reflection;
using System.Text;

namespace DuctSizer.WinForms;

/// <summary>
/// Plain-text diagnostic log written to %LOCALAPPDATA%\FanWorks\logs.
/// One file per day; files older than 30 days are removed at start-up.
/// Logging never throws: a failure to write the log must not take down the
/// application or hide the original problem.
/// </summary>
internal static class DiagnosticLog
{
    private static readonly object Gate = new();
    private const int RetentionDays = 30;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FanWorks",
        "logs");

    public static string CurrentLogPath =>
        Path.Combine(LogDirectory, $"fanworks-{DateTime.Now:yyyy-MM-dd}.log");

    public static string ApplicationVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    public static void Start()
    {
        PruneOldLogs();
        Info($"FanWorks {ApplicationVersion} started. OS {Environment.OSVersion}, " +
             $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}, .NET {Environment.Version}.");
    }

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Warning(string message, Exception? exception = null) => Write("WARN ", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>
    /// Writes a full crash report (exception chain and stack traces) to its own
    /// file and returns that file's path, or null when the report could not be written.
    /// </summary>
    public static string? WriteCrashReport(Exception exception, string context)
    {
        Error($"Unhandled exception ({context}).", exception);
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var report = new StringBuilder()
                .AppendLine($"FanWorks {ApplicationVersion} crash report")
                .AppendLine($"Time: {DateTimeOffset.Now:O}")
                .AppendLine($"Context: {context}")
                .AppendLine($"OS: {Environment.OSVersion}")
                .AppendLine($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}")
                .AppendLine($".NET: {Environment.Version}")
                .AppendLine()
                .AppendLine(exception.ToString());
            File.WriteAllText(path, report.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append(' ').Append(level).Append(' ').Append(message);
            if (exception is not null)
                line.AppendLine().Append(exception);
            line.AppendLine();
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(CurrentLogPath, line.ToString());
            }
        }
        catch
        {
            // Deliberately ignored: see class remarks.
        }
    }

    private static void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
                return;
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(LogDirectory))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // Pruning is housekeeping only.
        }
    }
}
