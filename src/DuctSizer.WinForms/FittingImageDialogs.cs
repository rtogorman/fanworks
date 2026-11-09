using System.Drawing.Drawing2D;
using System.Reflection;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal static class FittingImageResources
{
    internal static Bitmap Load(string fittingCode)
    {
        if (fittingCode.StartsWith("GRD-", StringComparison.OrdinalIgnoreCase))
            return CreateGrdImage(fittingCode);
        if (fittingCode.StartsWith("VAV-", StringComparison.OrdinalIgnoreCase))
            return CreateVavImage(fittingCode);
        var resourceName = $"DuctSizer.WinForms.Assets.Fittings.{fittingCode}.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded fitting image for {fittingCode} is missing.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Bitmap CreateVavImage(string fittingCode)
    {
        var image = new Bitmap(1100, 760);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var blue = new Pen(Color.FromArgb(30, 82, 126), 4F)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fine = new Pen(Color.FromArgb(75, 96, 112), 1.5F);
        using var titleFont = new Font("Arial", 20F, FontStyle.Bold);
        using var textFont = new Font("Arial", 12F);
        using var smallFont = new Font("Arial", 10F);
        var withReheat = fittingCode.Equals(VavPerformanceCatalog.WithReheatCode, StringComparison.OrdinalIgnoreCase);
        graphics.DrawString(withReheat ? "TITUS DESV - VAV WITH REHEAT" : "TITUS DESV - VAV WITHOUT REHEAT",
            titleFont, Brushes.Black, 42, 30);
        graphics.DrawLine(blue, 42, 76, 1058, 76);

        var centerY = 285;
        graphics.DrawLine(blue, 115, centerY - 58, 985, centerY - 58);
        graphics.DrawLine(blue, 115, centerY + 58, 985, centerY + 58);
        graphics.DrawLine(blue, 115, centerY - 58, 115, centerY + 58);
        graphics.DrawLine(blue, 985, centerY - 58, 985, centerY + 58);
        graphics.FillRectangle(Brushes.Black, 465, centerY - 85, 170, 170);
        graphics.DrawRectangle(blue, 465, centerY - 85, 170, 170);
        graphics.DrawString(withReheat ? "VAV + HW COIL" : "VAV / DAMPER OPEN", textFont,
            Brushes.Black, withReheat ? 465 : 452, 405);

        var headings = withReheat
            ? new[] { "VAV size", "Coil rows", "Airflow (cfm)", "Airside ΔPs" }
            : new[] { "VAV size", "Airflow (cfm)", "Damper position", "Min ΔPs" };
        DrawPerformanceLegend(graphics, fine, smallFont, 315, 505, headings);
        graphics.DrawString("Published Titus DESV performance lookup with in-range linear interpolation.",
            smallFont, Brushes.Black, 310, 615);
        return image;
    }

    private static Bitmap CreateGrdImage(string fittingCode)
    {
        var image = new Bitmap(1100, 760);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var line = new Pen(Color.FromArgb(20, 28, 36), 3F);
        using var fine = new Pen(Color.FromArgb(45, 52, 59), 1.6F);
        using var dimension = new Pen(Color.FromArgb(80, 86, 92), 1.25F) { DashStyle = DashStyle.Dash };
        using var titleFont = new Font("Arial", 20F, FontStyle.Bold);
        using var textFont = new Font("Arial", 11F, FontStyle.Regular);
        using var smallFont = new Font("Arial", 9F, FontStyle.Regular);
        var title = fittingCode switch
        {
            "GRD-DIFFUSER" => "PRICE SCD - SQUARE CONE DIFFUSER",
            "GRD-LINEAR-DIFFUSER" => "TITUS TBD-80 D - LINEAR SLOT DIFFUSER",
            "GRD-GRILLE" => "TITUS 350R - RETURN / EXHAUST GRILLE",
            _ => "GRD - ENGINEERING SYMBOL"
        };
        graphics.DrawString(title, titleFont, Brushes.Black, 42, 30);
        graphics.DrawLine(line, 42, 72, 1058, 72);

        if (fittingCode.Equals("GRD-DIFFUSER", StringComparison.OrdinalIgnoreCase))
        {
            var plan = new Rectangle(90, 135, 360, 360);
            graphics.DrawRectangle(line, plan);
            for (var inset = 48; inset <= 144; inset += 48)
                graphics.DrawRectangle(fine, Rectangle.Inflate(plan, -inset, -inset));
            graphics.DrawLine(dimension, plan.Left + plan.Width / 2, plan.Top - 28, plan.Left + plan.Width / 2, plan.Bottom + 28);
            graphics.DrawLine(dimension, plan.Left - 28, plan.Top + plan.Height / 2, plan.Right + 28, plan.Top + plan.Height / 2);
            graphics.DrawString("PLAN VIEW", textFont, Brushes.Black, 220, 515);

            var y = 250;
            graphics.DrawLine(line, 560, y + 120, 1020, y + 120);
            graphics.DrawLine(line, 590, y + 120, 640, y + 30);
            graphics.DrawLine(line, 640, y + 30, 940, y + 30);
            graphics.DrawLine(line, 940, y + 30, 990, y + 120);
            graphics.DrawEllipse(line, 720, y - 30, 140, 60);
            graphics.DrawLine(line, 720, y, 720, y + 30);
            graphics.DrawLine(line, 860, y, 860, y + 30);
            graphics.DrawString("NECK", smallFont, Brushes.Black, 765, y - 58);
            graphics.DrawString("SECTION", textFont, Brushes.Black, 760, y + 148);
            DrawPerformanceLegend(graphics, fine, smallFont, 555, 500,
                new[] { "Face size", "Neck dia.", "Flow rate", "Total pressure", "NC", "Throw" });
        }
        else if (fittingCode.Equals("GRD-LINEAR-DIFFUSER", StringComparison.OrdinalIgnoreCase))
        {
            var body = new Rectangle(80, 155, 710, 145);
            graphics.DrawRectangle(line, body);
            graphics.DrawLine(line, body.Left + 20, body.Top + body.Height / 2, body.Right - 20, body.Top + body.Height / 2);
            graphics.DrawLine(fine, body.Left + 25, body.Top + 32, body.Right - 25, body.Top + 32);
            graphics.DrawLine(fine, body.Left + 25, body.Bottom - 32, body.Right - 25, body.Bottom - 32);
            graphics.DrawLine(dimension, body.Left, body.Bottom + 42, body.Right, body.Bottom + 42);
            graphics.DrawLine(fine, body.Left, body.Bottom + 28, body.Left, body.Bottom + 55);
            graphics.DrawLine(fine, body.Right, body.Bottom + 28, body.Right, body.Bottom + 55);
            graphics.DrawString("2 ft, 4 ft, OR 5 ft NOMINAL LENGTH", textFont, Brushes.Black, 260, body.Bottom + 50);

            graphics.DrawEllipse(line, 855, 135, 150, 150);
            graphics.DrawRectangle(line, 900, 270, 60, 150);
            graphics.DrawLine(fine, 915, 285, 915, 405);
            graphics.DrawLine(fine, 945, 285, 945, 405);
            graphics.DrawString("6-12 in INLET / 1-4 SLOTS", smallFont, Brushes.Black, 820, 446);
            graphics.DrawString("3/4, 1, OR 1-1/2 in SLOT WIDTH", smallFont, Brushes.Black, 785, 468);
            DrawPerformanceLegend(graphics, fine, smallFont, 80, 525,
                new[] { "Length", "Flow rate", "Total pressure", "Static pressure", "NC", "Throw" });
        }
        else
        {
            var square = new Rectangle(240, 130, 620, 440);
            graphics.DrawRectangle(line, square);
            for (var index = 1; index < 7; index++)
            {
                var x = square.Left + index * square.Width / 7;
                graphics.DrawLine(fine, x, square.Top + 28, x, square.Bottom - 28);
            }
            graphics.DrawString("FACE VIEW", textFont, Brushes.Black, 500, 598);
        }
        return image;
    }

    private static void DrawPerformanceLegend(Graphics graphics, Pen pen, Font font, int left, int top,
        IReadOnlyList<string> headings)
    {
        const int width = 470;
        const int rowHeight = 28;
        var columnWidth = width / 2;
        for (var row = 0; row <= 3; row++)
            graphics.DrawLine(pen, left, top + row * rowHeight, left + width, top + row * rowHeight);
        graphics.DrawLine(pen, left, top, left, top + 3 * rowHeight);
        graphics.DrawLine(pen, left + columnWidth, top, left + columnWidth, top + 3 * rowHeight);
        graphics.DrawLine(pen, left + width, top, left + width, top + 3 * rowHeight);
        for (var index = 0; index < Math.Min(6, headings.Count); index++)
        {
            var column = index % 2;
            var row = index / 2;
            graphics.DrawString(headings[index], font, Brushes.Black,
                left + column * columnWidth + 8, top + row * rowHeight + 6);
        }
    }

    internal static Bitmap CreateThumbnail(string fittingCode, Size size)
    {
        using var source = Load(fittingCode);
        var thumbnail = new Bitmap(size.Width, size.Height);
        using var graphics = Graphics.FromImage(thumbnail);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        var scale = Math.Min(size.Width / (double)source.Width, size.Height / (double)source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        graphics.DrawImage(source, (size.Width - width) / 2, (size.Height - height) / 2, width, height);
        return thumbnail;
    }
}

internal sealed class FittingImageViewer : Form
{
    private readonly PictureBox _picture = new();
    private readonly Panel _scrollArea = new();

    internal FittingImageViewer(FittingDefinition definition)
    {
        Text = definition.Code.StartsWith("GRD-", StringComparison.OrdinalIgnoreCase)
            ? $"{definition.Code} - {definition.Description}"
            : $"ASHRAE {definition.Code} - {definition.Description}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1040, 820);
        MinimumSize = new Size(700, 560);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var header = new Label
        {
            Name = "FittingViewerHeader",
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(18, 8, 18, 6),
            Text = definition.Code.StartsWith("GRD-", StringComparison.OrdinalIgnoreCase)
                ? $"{definition.Code} - {definition.Description}\r\nTechnical GRD reference drawing and published-performance identification"
                : $"{definition.Code} - {definition.Description}\r\nComplete reference section from the supplied ASHRAE fitting-loss PDF",
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        };
        var close = new Button
        {
            Text = "Close",
            Dock = DockStyle.Bottom,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 69, 108),
            ForeColor = Color.White
        };
        close.FlatAppearance.BorderSize = 0;
        UiPolish.RoundButton(close, 7);
        close.Click += (_, _) => Close();

        _scrollArea.Dock = DockStyle.Fill;
        _scrollArea.AutoScroll = true;
        _scrollArea.BackColor = Color.FromArgb(235, 239, 243);
        _scrollArea.Padding = new Padding(12);
        _picture.Image = FittingImageResources.Load(definition.Code);
        _picture.SizeMode = PictureBoxSizeMode.Zoom;
        _picture.BackColor = Color.White;
        _picture.BorderStyle = BorderStyle.FixedSingle;
        _scrollArea.Controls.Add(_picture);
        _scrollArea.Resize += (_, _) => FitImageToViewerWidth();

        Controls.Add(_scrollArea);
        Controls.Add(close);
        Controls.Add(header);
        void ResizeHeaderToFit()
        {
            var availableWidth = Math.Max(220, ClientSize.Width - header.Padding.Horizontal - 8);
            var measured = TextRenderer.MeasureText(
                header.Text,
                header.Font,
                new Size(availableWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            header.Height = measured.Height + header.Padding.Vertical + 6;
        }
        Resize += (_, _) => ResizeHeaderToFit();
        Shown += (_, _) =>
        {
            ResizeHeaderToFit();
            FitImageToViewerWidth();
        };
        ResizeHeaderToFit();
    }

    private void FitImageToViewerWidth()
    {
        if (_picture.Image is null)
            return;
        var width = Math.Max(200, _scrollArea.ClientSize.Width - 42);
        var height = Math.Max(120, (int)Math.Round(width * _picture.Image.Height / (double)_picture.Image.Width));
        _picture.Size = new Size(width, height);
        _picture.Location = new Point(12, 12);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var image = _picture.Image;
            _picture.Image = null;
            image?.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class FittingGalleryDialog : Form
{
    private readonly List<Image> _thumbnails = new();
    private readonly ToolTip _toolTip = new();
    private readonly IReadOnlyList<FittingDefinition> _definitions;
    private readonly FlowLayoutPanel _gallery = new();
    private readonly ComboBox _groupFilter = new();
    private readonly string? _selectedCode;
    internal FittingDefinition? SelectedDefinition { get; private set; }

    internal FittingGalleryDialog(IReadOnlyList<FittingDefinition> definitions, string? selectedCode)
    {
        _definitions = definitions;
        _selectedCode = selectedCode;
        Text = "Select a Fitting or GRD";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1260, 900);
        MinimumSize = new Size(860, 640);
        BackColor = Color.FromArgb(242, 245, 248);
        Font = new Font("Segoe UI", 9F);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        var instruction = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(18, 8, 18, 6),
            Text = "Select a fitting image. Its description appears above the image and its fitting number appears below.",
            ForeColor = Color.FromArgb(28, 69, 108),
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var filterHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(18, 6, 18, 6),
            BackColor = BackColor
        };
        var filterLabel = new Label
        {
            Text = "Fitting Group",
            Dock = DockStyle.Left,
            Width = 130,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(34, 62, 88)
        };
        _groupFilter.Name = "FittingGalleryGroupCombo";
        _groupFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _groupFilter.Dock = DockStyle.Left;
        _groupFilter.Width = 220;
        _groupFilter.DropDownWidth = 240;
        _groupFilter.MaxDropDownItems = 8;
        _groupFilter.Items.Add("All Fittings");
        _groupFilter.Items.AddRange(FittingCatalog.SelectionGroups.Cast<object>().ToArray());
        _groupFilter.SelectedIndexChanged += (_, _) => PopulateGallery();
        filterHost.Controls.Add(_groupFilter);
        filterHost.Controls.Add(filterLabel);

        _gallery.Name = "FittingGalleryPanel";
        _gallery.Dock = DockStyle.Fill;
        _gallery.AutoScroll = true;
        _gallery.WrapContents = true;
        _gallery.Padding = new Padding(12);
        _gallery.BackColor = Color.FromArgb(242, 245, 248);
        var cancel = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Bottom,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(34, 62, 88)
        };
        cancel.Click += (_, _) => Close();
        UiPolish.RoundButton(cancel, 7);

        _groupFilter.SelectedIndex = 0;
        Controls.Add(_gallery);
        Controls.Add(cancel);
        Controls.Add(filterHost);
        Controls.Add(instruction);
    }

    private void PopulateGallery()
    {
        _gallery.SuspendLayout();
        _gallery.Controls.Clear();
        foreach (var thumbnail in _thumbnails)
            thumbnail.Dispose();
        _thumbnails.Clear();
        var group = Convert.ToString(_groupFilter.SelectedItem) ?? "All Fittings";
        var definitions = group == "All Fittings"
            ? _definitions
            : _definitions.Where(definition => FittingCatalog.SelectionGroup(definition) == group).ToArray();
        foreach (var definition in definitions)
            _gallery.Controls.Add(CreateGalleryTile(definition,
                string.Equals(definition.Code, _selectedCode, StringComparison.OrdinalIgnoreCase)));
        _gallery.ResumeLayout(true);
    }

    private Control CreateGalleryTile(FittingDefinition definition, bool selected)
    {
        var descriptionFont = new Font("Segoe UI", 10.2F, FontStyle.Bold);
        var measuredDescription = TextRenderer.MeasureText(
            definition.Description,
            descriptionFont,
            new Size(346, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var descriptionHeight = Math.Max(124, measuredDescription.Height + 16);
        var tile = new RoundedPanel
        {
            Name = $"FittingGalleryTile_{definition.Code}",
            Size = new Size(380, descriptionHeight + 356),
            Margin = new Padding(8),
            Padding = new Padding(15),
            BackColor = selected ? Color.FromArgb(218, 236, 250) : Color.White,
            CornerRadius = 12,
            BorderColor = selected ? Color.FromArgb(74, 139, 190) : Color.FromArgb(200, 212, 222),
            Cursor = Cursors.Hand,
            AccessibleName = $"Select fitting {definition.Code}"
        };
        tile.Tag = definition;
        var image = FittingImageResources.CreateThumbnail(definition.Code, new Size(348, 290));
        _thumbnails.Add(image);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = tile.BackColor,
            Cursor = Cursors.Hand
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, descriptionHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 290));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var picture = new PictureBox
        {
            Name = $"FittingGalleryImage_{definition.Code}",
            Dock = DockStyle.Fill,
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = Padding.Empty
        };
        var number = new Label
        {
            Name = $"FittingGalleryNumber_{definition.Code}",
            Dock = DockStyle.Fill,
            Text = definition.Code,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(28, 69, 108),
            Cursor = Cursors.Hand,
            Margin = Padding.Empty
        };
        var description = new Label
        {
            Name = $"FittingGalleryDescription_{definition.Code}",
            Dock = DockStyle.Fill,
            Text = definition.Description,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = descriptionFont,
            ForeColor = Color.FromArgb(54, 70, 86),
            Cursor = Cursors.Hand,
            AutoEllipsis = false,
            UseMnemonic = false,
            Margin = Padding.Empty,
            Padding = new Padding(2, 4, 2, 4)
        };
        _toolTip.SetToolTip(tile, definition.Description);
        _toolTip.SetToolTip(layout, definition.Description);
        _toolTip.SetToolTip(picture, definition.Description);
        _toolTip.SetToolTip(number, definition.Description);
        _toolTip.SetToolTip(description, definition.Description);
        void SelectFitting(object? _, EventArgs __)
        {
            SelectedDefinition = definition;
            DialogResult = DialogResult.OK;
            Close();
        }
        tile.Click += SelectFitting;
        layout.Click += SelectFitting;
        picture.Click += SelectFitting;
        number.Click += SelectFitting;
        description.Click += SelectFitting;
        layout.Controls.Add(description, 0, 0);
        layout.Controls.Add(picture, 0, 1);
        layout.Controls.Add(number, 0, 2);
        tile.Controls.Add(layout);
        return tile;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            foreach (var thumbnail in _thumbnails)
                thumbnail.Dispose();
            _thumbnails.Clear();
        }
        base.Dispose(disposing);
    }
}
