using System.Diagnostics;

namespace DuctSizer.WinForms;

/// <summary>
/// The Reports tab: a page-by-page preview of the project report (the same PDF that Save and Print Report write),
/// rendered with the PDF engine FanWorks already uses for drawing underlays. The preview is rebuilt when the tab
/// opens and on Refresh Preview, so it always shows the current project.
/// </summary>
internal sealed class ReportPreview : Panel
{
    private const int PageWidth = 820;
    private readonly MainForm _host;
    private readonly Label _status = new() { Name = "ReportPreviewStatus" };
    private readonly FlowLayoutPanel _pages = new() { Name = "ReportPreviewPages" };

    internal ReportPreview(MainForm host)
    {
        _host = host;
        Name = "ReportsPage";
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(214, 221, 228);

        _status.Dock = DockStyle.Top;
        _status.Height = 30;
        _status.Padding = new Padding(16, 0, 0, 0);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.BackColor = Color.White;
        _status.ForeColor = RibbonPalette.Caption;
        _status.Font = new Font("Segoe UI", 9F);
        _status.Text = "Open this tab to preview the project report.";

        _pages.Dock = DockStyle.Fill;
        _pages.AutoScroll = true;
        _pages.FlowDirection = FlowDirection.TopDown;
        _pages.WrapContents = false;
        _pages.BackColor = BackColor;
        _pages.Padding = new Padding(20, 16, 20, 24);
        _pages.Resize += (_, _) => CenterPages();

        Controls.Add(_pages);
        Controls.Add(_status);
    }

    internal int PageCount => _pages.Controls.Count;

    internal string StatusText => _status.Text;

    /// <summary>Builds the report and shows its pages. Failures are reported in the status line; nothing crashes.</summary>
    internal void RefreshPreview()
    {
        var previewPath = Path.Combine(Path.GetTempPath(), "FanWorks", $"report-preview-{Guid.NewGuid():N}.pdf");
        _host.UseWaitCursor = true;
        _status.Text = "Building the report preview…";
        _status.ForeColor = RibbonPalette.Caption;
        Application.DoEvents();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
            _host.DuctSystemScreen.WriteReportPdf(previewPath);
            var images = new List<Bitmap>();
            using (var session = new PdfPageRenderSession(previewPath))
            {
                for (var page = 0; page < session.PageCount; page++)
                    images.Add(session.RenderPage(page, 1100));
            }
            ClearPages();
            foreach (var (image, index) in images.Select((image, index) => (image, index)))
            {
                var height = (int)Math.Round(PageWidth * (double)image.Height / image.Width);
                _pages.Controls.Add(new PictureBox
                {
                    Name = $"ReportPreviewPage{index + 1}",
                    Image = image,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(PageWidth, height),
                    Margin = new Padding(0, 0, 0, 16),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    AccessibleName = $"Report page {index + 1} of {images.Count}"
                });
            }
            CenterPages();
            _status.Text = $"Report preview: {images.Count} page{(images.Count == 1 ? string.Empty : "s")}, built {DateTime.Now:h:mm tt}. "
                           + "Print Report saves this PDF.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or InvalidOperationException or ArgumentException or DllNotFoundException
                                       or EntryPointNotFoundException or BadImageFormatException)
        {
            DiagnosticLog.Error("The report preview could not be built.", ex);
            _status.Text = $"The report preview could not be built: {ex.Message}";
            _status.ForeColor = Color.Firebrick;
        }
        finally
        {
            _host.UseWaitCursor = false;
            try
            {
                if (File.Exists(previewPath))
                    File.Delete(previewPath);
            }
            catch (IOException)
            {
                // A preview file left in the temporary folder is harmless.
            }
        }
    }

    /// <summary>Print Report: writes the report PDF to a file the user chooses, then opens it for printing.</summary>
    internal void PrintReport()
    {
        var target = AutomationOptions.ReportPdfPath;
        if (string.IsNullOrWhiteSpace(target))
        {
            var project = _host.DuctSystemScreen._ductSystem.ProjectName;
            using var dialog = new SaveFileDialog
            {
                Title = "Print Report — save the project report as PDF",
                Filter = "PDF report (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                AddExtension = true,
                FileName = $"{(string.IsNullOrWhiteSpace(project) ? "FanWorks report" : project)} {DateTime.Now:yyyy-MM-dd}.pdf",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(_host) != DialogResult.OK)
                return;
            target = dialog.FileName;
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(target)) ?? string.Empty;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}-{Guid.NewGuid():N}.tmp");
        try
        {
            _host.UseWaitCursor = true;
            _host.DuctSystemScreen.WriteReportPdf(temporary);
            File.Move(temporary, target, overwrite: true);
            _status.Text = $"Report saved to {target}.";
            _status.ForeColor = Color.FromArgb(30, 100, 55);
            DiagnosticLog.Info($"Report printed to {target}.");
            if (string.IsNullOrWhiteSpace(AutomationOptions.ReportPdfPath))
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or ArgumentException or System.ComponentModel.Win32Exception)
        {
            DiagnosticLog.Error($"Print Report to {target} failed.", ex);
            _status.Text = $"The report could not be saved: {ex.Message}";
            _status.ForeColor = Color.Firebrick;
            if (!AutomationOptions.SuppressDialogs)
                MessageBox.Show(_host, $"The report could not be saved.\r\n\r\n{ex.Message}", "Print Report",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _host.UseWaitCursor = false;
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (IOException)
            {
                // Best effort.
            }
        }
    }

    private void CenterPages()
    {
        var spare = _pages.ClientSize.Width - PageWidth - SystemInformation.VerticalScrollBarWidth;
        var left = Math.Max(20, spare / 2);
        if (_pages.Padding.Left != left)
            _pages.Padding = new Padding(left, 16, 20, 24);
    }

    private void ClearPages()
    {
        foreach (var picture in _pages.Controls.OfType<PictureBox>().ToArray())
        {
            _pages.Controls.Remove(picture);
            picture.Image?.Dispose();
            picture.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClearPages();
        base.Dispose(disposing);
    }
}
