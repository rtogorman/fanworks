namespace DuctSizer.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        DiagnosticLog.Start();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            // Non-UI thread failure: the runtime will terminate the process after this handler.
            if (args.ExceptionObject is Exception exception)
                DiagnosticLog.WriteCrashReport(exception, "background thread; application terminated");
        };

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => ReportUnexpectedError(args.Exception);
        Application.Run(new MainForm());
        DiagnosticLog.Info("FanWorks closed normally.");
    }

    private static void ReportUnexpectedError(Exception exception)
    {
        var reportPath = DiagnosticLog.WriteCrashReport(exception, "UI thread; application kept running");
        var detail = reportPath is null
            ? "A crash report could not be written."
            : $"Details were saved to:\n{reportPath}\n\nPlease include this file when reporting the problem.";
        MessageBox.Show(
            "FanWorks encountered an unexpected error. The operation that was running did not complete, " +
            "so check the most recent results before relying on them. Save your project under a new name " +
            $"before continuing.\n\n{exception.Message}\n\n{detail}",
            "FanWorks",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
