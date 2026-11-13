using System.Diagnostics;
using System.Runtime.InteropServices;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The Reports tab: a page-by-page preview of the project report (the same PDF that Save and Print Report write),
/// rendered with the PDF engine FanWorks already uses for drawing underlays.
/// <para>
/// Speed: the report's values are gathered on the UI thread, then the PDF is written and its pages rendered on a
/// background thread, visible pages first, each page appearing as soon as it is ready (placeholders hold the
/// layout). Reopening the tab when the project has not changed (same document fingerprint and units) keeps the
/// current preview instead of rebuilding it; Refresh Preview always rebuilds.
/// </para>
/// <para>
/// Zoom: − / + buttons, Fit width, or Ctrl + mouse wheel, 50 % to 300 %. Pages near the view are re-rendered at the
/// zoomed resolution; a few sharp pages are kept and the rest stay at the normal resolution, so memory stays bounded.
/// </para>
/// </summary>
internal sealed class ReportPreview : Panel
{
    private const int BasePageWidth = 820;
    private const int MaximumSharpPages = 6;
    private static readonly double[] ZoomSteps = { .5, .67, .75, .9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3 };
    private readonly MainForm _host;
    private readonly Label _status = new() { Name = "ReportPreviewStatus" };
    private readonly Label _zoomLabel = new() { Name = "ReportZoomLabel" };
    private readonly FlowLayoutPanel _pages = new() { Name = "ReportPreviewPages" };
    private readonly List<PageSlot> _slots = new();
    private readonly AutoResetEvent _wake = new(false);
    private double _zoom = 1;
    private int _generation;
    private PdfPageRenderSession? _session;
    private string? _sessionPath;
    private string? _previewKey;
    private bool _busy;

    /// <summary>One report page: its picture box, the page shape, and the image resolutions it holds.</summary>
    private sealed class PageSlot
    {
        internal required PictureBox Box { get; init; }
        internal required double AspectRatio { get; init; } // height / width
        internal Bitmap? Normal { get; set; }
        internal Bitmap? Sharp { get; set; }
        internal int SharpWidth { get; set; }
    }

    internal ReportPreview(MainForm host)
    {
        _host = host;
        Name = "ReportsPage";
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(214, 221, 228);

        var bar = new TableLayoutPanel
        {
            Name = "ReportPreviewBar", Dock = DockStyle.Top, Height = 36, BackColor = Color.White, ColumnCount = 5,
            RowCount = 1, Padding = new Padding(12, 3, 12, 3)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var column = 0; column < 4; column++)
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = RibbonPalette.Caption;
        _status.Font = new Font("Segoe UI", 9F);
        _status.AutoEllipsis = true;
        _status.Text = "Open this tab to preview the project report.";
        bar.Controls.Add(_status, 0, 0);
        bar.Controls.Add(ZoomButton("ReportZoomOut", "−", "Zoom out (Ctrl + mouse wheel)", () => StepZoom(-1)), 1, 0);
        _zoomLabel.AutoSize = false;
        _zoomLabel.Size = new Size(52, 26);
        _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
        _zoomLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _zoomLabel.ForeColor = RibbonPalette.Ink;
        _zoomLabel.Anchor = AnchorStyles.None;
        bar.Controls.Add(_zoomLabel, 2, 0);
        bar.Controls.Add(ZoomButton("ReportZoomIn", "+", "Zoom in (Ctrl + mouse wheel)", () => StepZoom(1)), 3, 0);
        bar.Controls.Add(ZoomButton("ReportZoomFit", "Fit width", "Fit the page width to the window", FitWidth, wide: true), 4, 0);

        _pages.Dock = DockStyle.Fill;
        _pages.AutoScroll = true;
        _pages.FlowDirection = FlowDirection.TopDown;
        _pages.WrapContents = false;
        _pages.BackColor = BackColor;
        _pages.Padding = new Padding(20, 16, 20, 24);
        _pages.Resize += (_, _) => CenterPages();
        _pages.Scroll += (_, _) => _wake.Set();
        _pages.MouseWheel += OnPagesMouseWheel;
        _pages.MouseEnter += (_, _) => FocusPages();

        Controls.Add(_pages);
        Controls.Add(bar);
        UpdateZoomLabel();
    }

    private Button ZoomButton(string name, string text, string tip, Action action, bool wide = false)
    {
        var button = new Button
        {
            Name = name, Text = text, FlatStyle = FlatStyle.Flat, Size = new Size(wide ? 78 : 32, 26),
            Font = new Font("Segoe UI", wide ? 8.5F : 11F, FontStyle.Bold), ForeColor = RibbonPalette.Ink,
            BackColor = Color.FromArgb(240, 245, 250), Margin = new Padding(3, 2, 3, 2), Anchor = AnchorStyles.None,
            TabStop = false, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(196, 210, 224);
        new ToolTip().SetToolTip(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    internal int PageCount => _slots.Count;

    internal string StatusText => _status.Text;

    /// <summary>Current zoom (1 = 100 %).</summary>
    internal double Zoom => _zoom;

    /// <summary>True while the report is being built or its pages are still being drawn for the first time.</summary>
    internal bool IsBusy => _busy || _slots.Any(slot => slot.Normal is null);

    /// <summary>
    /// Opens the tab: rebuilds the preview unless it already shows this project unchanged (same fingerprint and
    /// units). <paramref name="force"/> (Refresh Preview) always rebuilds.
    /// </summary>
    internal void RefreshPreview(bool force = false)
    {
        if (_busy)
            return;
        var screen = _host.DuctSystemScreen;
        string key;
        try
        {
            key = $"{DuctSystemStore.Fingerprint(screen._ductSystem)}|{_host._currentUnits}";
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            key = Guid.NewGuid().ToString("N");
        }
        if (!force && key == _previewKey && _slots.Count > 0)
        {
            _status.Text = $"Report preview: {_slots.Count} pages, up to date with the project. Refresh Preview rebuilds it.";
            _status.ForeColor = RibbonPalette.Caption;
            return;
        }

        _busy = true;
        var generation = ++_generation;
        _status.Text = "Building the report preview…";
        _status.ForeColor = RibbonPalette.Caption;
        _host.UseWaitCursor = true;
        Application.DoEvents();
        var clock = Stopwatch.StartNew();
        Action<string> write;
        try
        {
            // Reads the window and project state, so it runs here on the UI thread; the rest runs in the background.
            write = screen.PrepareReportPdf();
        }
        catch (Exception ex) when (IsReportFailure(ex))
        {
            Fail(ex);
            return;
        }
        finally
        {
            _host.UseWaitCursor = false;
        }
        var gathered = clock.ElapsedMilliseconds;
        var previewPath = Path.Combine(Path.GetTempPath(), "FanWorks", $"report-preview-{Guid.NewGuid():N}.pdf");
        Task.Run<(PdfPageRenderSession session, double[] aspects)>(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
            write(previewPath);
            var session = new PdfPageRenderSession(previewPath);
            var aspects = Enumerable.Range(0, session.PageCount).Select(page =>
            {
                var info = session.GetPageInfo(page);
                return (double)info.HeightPoints / info.WidthPoints;
            }).ToArray();
            return (session, aspects);
        }).ContinueWith(task =>
        {
            if (IsDisposed || Disposing)
            {
                if (task.IsCompletedSuccessfully)
                    task.Result.session.Dispose();
                DeleteQuietly(previewPath);
                return;
            }
            // Marshal through the main window (its handle always exists; this page's may not until it is shown).
            if (_host.IsDisposed || !_host.IsHandleCreated)
            {
                if (task.IsCompletedSuccessfully)
                    task.Result.session.Dispose();
                DeleteQuietly(previewPath);
                return;
            }
            _host.BeginInvoke(() =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    DeleteQuietly(previewPath);
                    // Any failure while writing or opening the PDF in the background is reported in the status
                    // line and logged in full; the rest of the program carries on.
                    Fail(task.Exception?.GetBaseException() ?? new InvalidOperationException("The report could not be built."));
                    return;
                }
                var (session, aspects) = task.Result;
                if (generation != _generation)
                {
                    session.Dispose();
                    DeleteQuietly(previewPath);
                    return;
                }
                ShowPages(session, previewPath, aspects);
                _previewKey = key;
                _busy = false;
                DiagnosticLog.Info($"Report preview: values gathered in {gathered} ms, PDF written and opened in {clock.ElapsedMilliseconds - gathered} ms ({aspects.Length} pages).");
                _status.Text = $"Report preview: {aspects.Length} page{(aspects.Length == 1 ? string.Empty : "s")}, built {DateTime.Now:h:mm tt}. "
                               + "Print Report saves this PDF. Zoom with − / + or Ctrl + mouse wheel.";
                var renderGeneration = generation;
                var thread = new Thread(() => RenderLoop(renderGeneration, session)) { IsBackground = true, Name = "Report preview pages" };
                thread.Start();
            });
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Waits (pumping messages) until the preview is built and every page has its first image, or the time runs out.
    /// For automated checks.
    /// </summary>
    internal bool WaitUntilReady(int timeoutMilliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (IsBusy && clock.ElapsedMilliseconds < timeoutMilliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
        return !IsBusy;
    }

    private static bool IsReportFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException
            or ArgumentException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    private void Fail(Exception ex)
    {
        DiagnosticLog.Error("The report preview could not be built.", ex);
        _status.Text = $"The report preview could not be built: {ex.GetType().Name}: {ex.Message}";
        _status.ForeColor = Color.Firebrick;
        _busy = false;
    }

    private void ShowPages(PdfPageRenderSession session, string path, IReadOnlyList<double> aspects)
    {
        var oldSession = _session;
        var oldPath = _sessionPath;
        _pages.SuspendLayout();
        ClearPages();
        _session = session;
        _sessionPath = path;
        foreach (var (aspect, index) in aspects.Select((aspect, index) => (aspect, index)))
        {
            var box = new PictureBox
            {
                Name = $"ReportPreviewPage{index + 1}",
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 0, 16),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = $"Report page {index + 1} of {aspects.Count}"
            };
            var pageNumber = index + 1;
            box.Paint += (_, e) =>
            {
                if (box.Image is null)
                    TextRenderer.DrawText(e.Graphics, $"Page {pageNumber} — drawing…", Font, box.ClientRectangle,
                        RibbonPalette.Caption, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            box.MouseWheel += OnPagesMouseWheel;
            box.MouseEnter += (_, _) => FocusPages();
            _slots.Add(new PageSlot { Box = box, AspectRatio = aspect });
            _pages.Controls.Add(box);
        }
        ApplyPageSizes();
        _pages.ResumeLayout();
        CenterPages();
        oldSession?.Dispose();
        DeleteQuietly(oldPath);
    }

    /// <summary>The page width on screen at the current zoom, in pixels.</summary>
    private int DisplayWidth => (int)Math.Round(LogicalToDeviceUnits(BasePageWidth) * _zoom);

    /// <summary>The page image width drawn at 100 % (also what every page gets first).</summary>
    private int NormalWidth => Math.Clamp(LogicalToDeviceUnits(BasePageWidth) + 120, 600, 2400);

    private void ApplyPageSizes()
    {
        var width = DisplayWidth;
        foreach (var slot in _slots)
            slot.Box.Size = new Size(width, (int)Math.Round(width * slot.AspectRatio));
    }

    /// <summary>
    /// The background page loop: asks the UI thread which page to draw next (visible pages first; first images for
    /// every page, then sharp images for pages near the view when zoomed in), draws it, and hands it back.
    /// </summary>
    private void RenderLoop(int generation, PdfPageRenderSession session)
    {
        while (generation == _generation && !IsDisposed)
        {
            (int Page, int Width, bool Sharp, int Dimension)? request;
            try
            {
                request = (ValueTuple<int, int, bool, int>?)_host.Invoke(new Func<(int, int, bool, int)?>(NextRenderRequest));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            if (request is not { } next)
            {
                try
                {
                    _wake.WaitOne(500);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                continue;
            }
            Bitmap image;
            try
            {
                image = session.RenderPage(next.Page, next.Dimension);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidDataException or InvalidOperationException
                                           or ArgumentOutOfRangeException or ExternalException)
            {
                DiagnosticLog.Error($"Report preview page {next.Page + 1} could not be drawn.", ex);
                return;
            }
            try
            {
                _host.BeginInvoke(() => ApplyImage(generation, next.Page, next.Sharp, next.Width, image));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                image.Dispose();
                return;
            }
        }
    }

    /// <summary>The next page to draw: page index, image width, whether it is a sharp (zoomed) image, and the
    /// image's larger dimension for the renderer. Null when every page is up to date.</summary>
    private (int, int, bool, int)? NextRenderRequest()
    {
        var request = NextPageToDraw();
        if (request is not { } next)
            return null;
        var aspect = _slots[next.Page].AspectRatio;
        var dimension = aspect <= 1 ? next.Width : (int)Math.Round(next.Width * aspect);
        return (next.Page, next.Width, next.Sharp, Math.Clamp(dimension, 200, 8192));
    }

    private (int Page, int Width, bool Sharp)? NextPageToDraw()
    {
        if (_slots.Count == 0)
            return null;
        var view = new Rectangle(0, 0, _pages.ClientSize.Width, _pages.ClientSize.Height);
        var near = Rectangle.Inflate(view, 0, view.Height); // one screen above and below
        var order = _slots.Select((slot, index) => (slot, index))
            .OrderBy(entry => entry.slot.Box.Bounds.IntersectsWith(view) ? 0 : 1)
            .ThenBy(entry => Math.Abs(entry.slot.Box.Top + entry.slot.Box.Height / 2 - view.Height / 2))
            .ToList();
        // Visible pages first: their first image, then a sharp one when zoomed in.
        var sharpWidth = Math.Min(4096, (int)Math.Round(DisplayWidth * 1.1));
        var wantSharp = sharpWidth > NormalWidth * 1.08;
        foreach (var (slot, index) in order.Where(entry => entry.slot.Box.Bounds.IntersectsWith(view)))
        {
            if (slot.Normal is null)
                return (index, NormalWidth, false);
            if (wantSharp && slot.SharpWidth < sharpWidth * .95)
                return (index, sharpWidth, true);
        }
        foreach (var (slot, index) in order)
            if (slot.Normal is null)
                return (index, NormalWidth, false);
        if (wantSharp)
            foreach (var (slot, index) in order.Where(entry => entry.slot.Box.Bounds.IntersectsWith(near)))
                if (slot.SharpWidth < sharpWidth * .95)
                    return (index, sharpWidth, true);
        return null;
    }

    private void ApplyImage(int generation, int page, bool sharp, int width, Bitmap image)
    {
        if (generation != _generation || page >= _slots.Count)
        {
            image.Dispose();
            return;
        }
        var slot = _slots[page];
        if (sharp)
        {
            var old = slot.Sharp;
            slot.Sharp = image;
            slot.SharpWidth = width;
            slot.Box.Image = image;
            old?.Dispose();
            // Keep only a few sharp pages (the ones nearest the view); the rest show their normal image.
            var center = _pages.ClientSize.Height / 2;
            foreach (var far in _slots.Where(other => other.Sharp is not null)
                         .OrderByDescending(other => Math.Abs(other.Box.Top + other.Box.Height / 2 - center))
                         .Take(Math.Max(0, _slots.Count(other => other.Sharp is not null) - MaximumSharpPages)).ToArray())
            {
                far.Box.Image = far.Normal;
                far.Sharp?.Dispose();
                far.Sharp = null;
                far.SharpWidth = 0;
            }
        }
        else
        {
            var old = slot.Normal;
            slot.Normal = image;
            if (slot.Sharp is null)
                slot.Box.Image = image;
            old?.Dispose();
        }
    }

    private void StepZoom(int direction) => SetZoom(NextStep(direction));

    private void FitWidth()
    {
        var available = _pages.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 40;
        SetZoom(Math.Clamp(available / (double)LogicalToDeviceUnits(BasePageWidth), ZoomSteps[0], ZoomSteps[^1]));
    }

    /// <summary>Sets the zoom (clamped to 50 % to 300 %), keeping the same part of the report in view.</summary>
    internal void SetZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, ZoomSteps[0], ZoomSteps[^1]);
        if (Math.Abs(zoom - _zoom) < 1e-6)
            return;
        var scrollRange = Math.Max(1, _pages.DisplayRectangle.Height - _pages.ClientSize.Height);
        var fraction = -_pages.AutoScrollPosition.Y / (double)scrollRange;
        _zoom = zoom;
        _pages.SuspendLayout();
        ApplyPageSizes();
        _pages.ResumeLayout();
        CenterPages();
        var newRange = Math.Max(0, _pages.DisplayRectangle.Height - _pages.ClientSize.Height);
        _pages.AutoScrollPosition = new Point(-_pages.AutoScrollPosition.X, (int)Math.Round(fraction * newRange));
        UpdateZoomLabel();
        _wake.Set();
    }

    private void UpdateZoomLabel() => _zoomLabel.Text = $"{_zoom * 100:0}%";

    private void FocusPages()
    {
        // So Ctrl + mouse wheel reaches the preview without clicking it first.
        if (!_pages.Focused && Visible)
            _pages.Focus();
    }

    private void OnPagesMouseWheel(object? sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) == 0)
        {
            _wake.Set();
            return;
        }
        SetZoom(e.Delta > 0 ? NextStep(1) : NextStep(-1));
        if (e is HandledMouseEventArgs handled)
            handled.Handled = true;
    }

    private double NextStep(int direction) => direction > 0
        ? ZoomSteps.FirstOrDefault(step => step > _zoom + 1e-6, ZoomSteps[^1])
        : ZoomSteps.LastOrDefault(step => step < _zoom - 1e-6, ZoomSteps[0]);

    private static void DeleteQuietly(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A preview file left in the temporary folder is harmless.
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
        var spare = _pages.ClientSize.Width - DisplayWidth - SystemInformation.VerticalScrollBarWidth;
        var left = Math.Max(20, spare / 2);
        if (_pages.Padding.Left != left)
            _pages.Padding = new Padding(left, 16, 20, 24);
    }

    private void ClearPages()
    {
        foreach (var slot in _slots)
        {
            _pages.Controls.Remove(slot.Box);
            slot.Box.Image = null;
            slot.Normal?.Dispose();
            slot.Sharp?.Dispose();
            slot.Box.Dispose();
        }
        _slots.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _generation++;
            _wake.Set();
            ClearPages();
            _session?.Dispose();
            DeleteQuietly(_sessionPath);
        }
        base.Dispose(disposing);
    }
}
