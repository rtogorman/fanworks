namespace DuctSizer.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
            MessageBox.Show(
                $"FanWorks encountered an unexpected error.\n\n{args.Exception.Message}",
                "FanWorks",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        Application.Run(new MainForm());
    }
}
