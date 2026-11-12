using System.Drawing.Imaging;
using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class PdfBackgroundImportDialog : DpiScaledForm
{
    private readonly string _path;
    private readonly PdfPageRenderSession _session;
    private readonly NumericUpDown _page = new();
    private readonly PictureBox _preview = new();
    private readonly Label _pageSummary = new();
    private readonly TextBox _paperInches = Input("0.25", "PdfPaperInchesInput");
    private readonly TextBox _drawingFeet = Input("1", "PdfDrawingFeetInput");
    private readonly TextBox _centerX = Input("0", "PdfCenterXInput");
    private readonly TextBox _centerY = Input("0", "PdfCenterYInput");
    private readonly TextBox _centerZ = Input("0", "PdfCenterZInput");
    private readonly Button _import = new();

    internal PdfBackgroundImportDialog(string path, PdfPageRenderSession session)
    {
        _path = path;
        _session = session;
        Text = "Import PDF Background";
        Name = "PdfBackgroundImportDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 620);
        ClientSize = new Size(900, 690);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(244, 247, 249);
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var heading = new Label
        {
            Text = "PDF PLAN BACKGROUND\r\nChoose a page, define its architectural scale, and place its center in the system coordinates.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(28, 69, 108),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        var previewCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderColor = UiPalette.CardBorder,
            CornerRadius = 10,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 12, 0)
        };
        _preview.Name = "PdfPagePreview";
        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(231, 235, 239);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        previewCard.Controls.Add(_preview);
        root.Controls.Add(previewCard, 0, 1);

        var settingsCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderColor = UiPalette.CardBorder,
            CornerRadius = 10,
            Padding = new Padding(14),
            Margin = Padding.Empty
        };
        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 12,
            BackColor = Color.White,
            AutoScroll = true
        };
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        settings.Controls.Add(new Label
        {
            Text = Path.GetFileName(path), Dock = DockStyle.Fill,
            AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(34, 62, 88), TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        settings.Controls.Add(Field("PDF page", PageControl()), 0, 1);
        settings.Controls.Add(_pageSummary, 0, 2);
        _pageSummary.Dock = DockStyle.Fill;
        _pageSummary.ForeColor = Color.FromArgb(72, 92, 110);
        _pageSummary.TextAlign = ContentAlignment.MiddleLeft;
        settings.Controls.Add(Section("CUSTOM DRAWING SCALE"), 0, 3);
        settings.Controls.Add(ScaleControl(), 0, 4);
        settings.Controls.Add(new Label
        {
            Text = "Example: 0.25 inches = 1 foot is a 1/4\" = 1'-0\" drawing scale.",
            Dock = DockStyle.Fill, AutoSize = false, Height = 42,
            ForeColor = Color.FromArgb(72, 92, 110)
        }, 0, 5);
        settings.Controls.Add(Section("PDF CENTER LOCATION"), 0, 6);
        settings.Controls.Add(Field("X (ft)", _centerX), 0, 7);
        settings.Controls.Add(Field("Y (ft)", _centerY), 0, 8);
        settings.Controls.Add(Field("Z (ft)", _centerZ), 0, 9);
        settings.Controls.Add(new Label
        {
            Text = "The default 0, 0, 0 places the center of the sheet at the graph origin. The background is visible only in Top-Down view; the grid and system remain on top.",
            Dock = DockStyle.Fill, AutoSize = false, Height = 68,
            ForeColor = Color.FromArgb(72, 92, 110)
        }, 0, 10);
        settingsCard.Controls.Add(settings);
        root.Controls.Add(settingsCard, 1, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        _import.Name = "PdfImportButton";
        _import.Text = "Import Background";
        _import.Size = new Size(200, 32);
        _import.BackColor = Color.FromArgb(42, 135, 91);
        _import.ForeColor = Color.White;
        _import.FlatStyle = FlatStyle.Flat;
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32), FlatStyle = FlatStyle.Flat
        };
        UiPolish.RoundButton(_import, 7);
        UiPolish.RoundButton(cancel, 7);
        _import.Click += (_, _) => ImportBackground();
        actions.Controls.Add(_import);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 2);
        root.SetColumnSpan(actions, 2);
        Controls.Add(root);
        CancelButton = cancel;
        AcceptButton = _import;
        _page.ValueChanged += (_, _) => RenderPreview();
        Shown += (_, _) => RenderPreview();
        FormClosed += (_, _) =>
        {
            var image = _preview.Image;
            _preview.Image = null;
            image?.Dispose();
        };
    }

    internal LayoutPdfBackground? Background { get; private set; }

    private Control PageControl()
    {
        _page.Name = "PdfPageNumberInput";
        _page.Minimum = 1;
        _page.Maximum = _session.PageCount;
        _page.Value = 1;
        _page.Width = 86;
        _page.TextAlign = HorizontalAlignment.Right;
        return _page;
    }

    private Control ScaleControl()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoSize = true, Margin = Padding.Empty
        };
        _paperInches.Width = 64;
        _drawingFeet.Width = 64;
        row.Controls.Add(_paperInches);
        row.Controls.Add(Unit("inches  ="));
        row.Controls.Add(_drawingFeet);
        row.Controls.Add(Unit("feet"));
        return row;
    }

    private void RenderPreview()
    {
        try
        {
            var pageIndex = (int)_page.Value - 1;
            var info = _session.GetPageInfo(pageIndex);
            using var rendered = _session.RenderPage(pageIndex, 1100);
            var replacement = new Bitmap(rendered);
            var previous = _preview.Image;
            _preview.Image = replacement;
            previous?.Dispose();
            _pageSummary.Text = $"Page {(int)_page.Value} of {_session.PageCount} - "
                                + $"{info.WidthPoints / 72F:0.##} x {info.HeightPoints / 72F:0.##} in";
        }
        catch (Exception ex)
        {
            _pageSummary.Text = ex.Message;
            _pageSummary.ForeColor = Color.Firebrick;
        }
    }

    private void ImportBackground()
    {
        if (!TryReadValues(out var paperInches, out var drawingFeet,
                out var centerX, out var centerY, out var centerZ))
            return;
        try
        {
            var pageIndex = (int)_page.Value - 1;
            var info = _session.GetPageInfo(pageIndex);
            using var rendered = _session.RenderPage(pageIndex, 4096);
            using var png = new MemoryStream();
            rendered.Save(png, ImageFormat.Png);
            Background = new LayoutPdfBackground
            {
                SourceFileName = Path.GetFileName(_path),
                PageNumber = pageIndex + 1,
                PageCount = _session.PageCount,
                PageWidthPoints = info.WidthPoints,
                PageHeightPoints = info.HeightPoints,
                PagePngBase64 = Convert.ToBase64String(png.ToArray()),
                PaperInches = paperInches,
                DrawingFeet = drawingFeet,
                CenterXFeet = centerX,
                CenterYFeet = centerY,
                CenterZFeet = centerZ,
                Visible = true
            };
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Import PDF Background",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryReadValues(out double paperInches, out double drawingFeet,
        out double centerX, out double centerY, out double centerZ)
    {
        paperInches = drawingFeet = centerX = centerY = centerZ = 0;
        if (!Positive(_paperInches, "Enter a positive number of paper inches.", out paperInches)
            || !Positive(_drawingFeet, "Enter a positive number of drawing feet.", out drawingFeet)
            || !Finite(_centerX, "Enter a valid X coordinate in feet.", out centerX)
            || !Finite(_centerY, "Enter a valid Y coordinate in feet.", out centerY)
            || !Finite(_centerZ, "Enter a valid Z coordinate in feet.", out centerZ))
            return false;
        return true;
    }

    private bool Positive(TextBox input, string message, out double value)
    {
        if (Finite(input, message, out value) && value > 0)
            return true;
        MessageBox.Show(this, message, "PDF Scale", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        input.Focus();
        return false;
    }

    private bool Finite(TextBox input, string message, out double value)
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value))
            return true;
        MessageBox.Show(this, message, "PDF Background", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        input.Focus();
        return false;
    }

    private static TextBox Input(string text, string name) => new()
    {
        Name = name, Text = text, TextAlign = HorizontalAlignment.Right,
        Width = 96, Margin = new Padding(4, 4, 4, 4)
    };

    private static Label Unit(string text) => new()
    {
        Text = text, AutoSize = true, Margin = new Padding(2, 8, 5, 0),
        ForeColor = Color.FromArgb(72, 92, 110)
    };

    private static Label Section(string text) => new()
    {
        Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft,
        Font = new Font("Segoe UI Semibold", 8F), ForeColor = Color.FromArgb(28, 69, 108),
        Padding = new Padding(0, 8, 0, 2)
    };

    private static Control Field(string label, Control input)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, Height = 38
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        row.Controls.Add(new Label
        {
            Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(34, 62, 88), Padding = new Padding(0, 0, 8, 0)
        }, 0, 0);
        input.Dock = DockStyle.Fill;
        row.Controls.Add(input, 1, 0);
        return row;
    }
}

internal sealed class PdfBackgroundPropertiesDialog : DpiScaledForm
{
    private readonly TextBox _paperInches;
    private readonly TextBox _drawingFeet;
    private readonly TextBox _centerX;
    private readonly TextBox _centerY;
    private readonly TextBox _centerZ;

    internal PdfBackgroundPropertiesDialog(LayoutPdfBackground background)
    {
        FormKit.SetUp(this, "PDF Underlay Scale and Location", "PdfBackgroundPropertiesDialog", new Size(520, 430));
        _paperInches = Input(background.PaperInches, "PdfEditPaperInchesInput");
        _drawingFeet = Input(background.DrawingFeet, "PdfEditDrawingFeetInput");
        _centerX = Input(background.CenterXFeet, "PdfEditCenterXInput");
        _centerY = Input(background.CenterYFeet, "PdfEditCenterYInput");
        _centerZ = Input(background.CenterZFeet, "PdfEditCenterZInput");

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22, 16, 22, 14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.Controls.Add(FormKit.Header("Underlay scale and location",
            $"{background.SourceFileName}, page {background.PageNumber}. The sheet keeps its proportions when the grid scale changes."), 0, 0);

        // Scale: paper inches = drawing feet.
        var scale = new FlowLayoutPanel { WrapContents = false, BackColor = Color.White, Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0) };
        scale.Controls.Add(new Label { Text = "Scale", AutoSize = true, Margin = new Padding(0, 9, 18, 0), ForeColor = FormKit.LabelInk, Font = new Font("Segoe UI Semibold", 9F) });
        scale.Controls.Add(_paperInches);
        scale.Controls.Add(new Label { Text = "in. on paper  =", AutoSize = true, Margin = new Padding(2, 9, 6, 0), ForeColor = FormKit.Muted });
        scale.Controls.Add(_drawingFeet);
        scale.Controls.Add(new Label { Text = "ft", AutoSize = true, Margin = new Padding(2, 9, 0, 0), ForeColor = FormKit.Muted });
        root.Controls.Add(FormKit.Card(scale), 0, 1);

        var location = FormKit.FieldTable(42,
            ("Center X", _centerX, "ft"),
            ("Center Y", _centerY, "ft"),
            ("Center Z (elevation)", _centerZ, "ft   (0 = floor level)"));
        root.Controls.Add(FormKit.Card(location), 0, 2);
        root.Controls.Add(new Label
        {
            Text = "The centre of the sheet sits at X, Y and Z in the system's absolute coordinates. The underlay shows in "
                   + "every view (top-down, isometric and free views) at its Z elevation.",
            Dock = DockStyle.Fill, ForeColor = FormKit.Muted, Padding = new Padding(2, 6, 2, 0)
        }, 0, 3);
        root.Controls.Add(FormKit.Buttons(this, "Update", "PdfBackgroundPropertiesSaveButton", ValidateAndClose), 0, 4);
        Controls.Add(root);
    }

    internal double PaperInches { get; private set; }
    internal double DrawingFeet { get; private set; }
    internal double CenterXFeet { get; private set; }
    internal double CenterYFeet { get; private set; }
    internal double CenterZFeet { get; private set; }

    internal void ApplyTo(LayoutPdfBackground background)
    {
        background.PaperInches = PaperInches;
        background.DrawingFeet = DrawingFeet;
        background.CenterXFeet = CenterXFeet;
        background.CenterYFeet = CenterYFeet;
        background.CenterZFeet = CenterZFeet;
    }

    private void ValidateAndClose()
    {
        if (!Read(_paperInches, true, out var paperInches)
            || !Read(_drawingFeet, true, out var drawingFeet)
            || !Read(_centerX, false, out var centerX)
            || !Read(_centerY, false, out var centerY)
            || !Read(_centerZ, false, out var centerZ))
        {
            MessageBox.Show(this, "Enter positive scale values and valid numeric X, Y, and Z coordinates.",
                "PDF Background", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        PaperInches = paperInches;
        DrawingFeet = drawingFeet;
        CenterXFeet = centerX;
        CenterYFeet = centerY;
        CenterZFeet = centerZ;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool Read(TextBox input, bool positive, out double value) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && double.IsFinite(value) && (!positive || value > 0);

    private static TextBox Input(double value, string name) => new()
    {
        Name = name, Text = value.ToString("0.###", CultureInfo.CurrentCulture),
        Width = 92, TextAlign = HorizontalAlignment.Right, Margin = new Padding(6, 6, 6, 6)
    };
}
