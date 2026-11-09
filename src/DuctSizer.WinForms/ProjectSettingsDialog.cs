using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class ProjectSettingsDialog : Form
{
    private readonly TextBox _userName = new() { Name = "SettingsUserNameText" };
    private readonly TextBox _projectName = new() { Name = "SettingsProjectNameText" };
    private readonly TextBox _projectNumber = new() { Name = "SettingsProjectNumberText" };
    private readonly DateTimePicker _projectDate = new()
    {
        Name = "SettingsProjectDatePicker",
        Format = DateTimePickerFormat.Long
    };
    private readonly PictureBox _logoPreview = new() { Name = "SettingsCompanyLogoPreview", SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private string _companyLogoPngBase64;

    internal string UserName => _userName.Text.Trim();
    internal string ProjectName => _projectName.Text.Trim();
    internal string ProjectNumber => _projectNumber.Text.Trim();
    internal string ProjectDate => _projectDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    internal string CompanyLogoPngBase64 => _companyLogoPngBase64;
    internal ProjectSettingsDialog(DuctSystemDocument document, UnitSystem units)
    {
        _companyLogoPngBase64 = document.CompanyLogoPngBase64;
        Text = "Project Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(660, 430);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);

        _userName.Text = document.UserName;
        _projectName.Text = document.ProjectName;
        _projectNumber.Text = document.ProjectNumber;
        _projectDate.Value = DateTime.TryParse(document.ProjectDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var date) ? date : DateTime.Today;
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "User Name", _userName);
        AddRow(table, 1, "Project Name", _projectName);
        AddRow(table, 2, "Project Number", _projectNumber);
        AddRow(table, 3, "Date", _projectDate);
        var logoEditor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        logoEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        logoEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        logoEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        _logoPreview.Dock = DockStyle.Fill;
        _logoPreview.Margin = new Padding(4, 3, 10, 3);
        LoadLogoPreview();
        var chooseLogo = new Button { Name = "SettingsChooseLogoButton", Text = "Choose Logo…", Dock = DockStyle.Fill, Margin = new Padding(0, 12, 8, 12), FlatStyle = FlatStyle.Flat };
        var removeLogo = new Button { Name = "SettingsRemoveLogoButton", Text = "Remove", Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 12), FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(chooseLogo, 7);
        UiPolish.RoundButton(removeLogo, 7);
        chooseLogo.Click += (_, _) => ChooseLogo();
        removeLogo.Click += (_, _) => { _companyLogoPngBase64 = string.Empty; LoadLogoPreview(); };
        logoEditor.Controls.Add(_logoPreview, 0, 0);
        logoEditor.Controls.Add(chooseLogo, 1, 0);
        logoEditor.Controls.Add(removeLogo, 2, 0);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        table.Controls.Add(new Label
        {
            Text = "Company Logo",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, 4);
        table.Controls.Add(logoEditor, 1, 4);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var save = new Button { Text = "Save", Width = 94, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(42, 135, 91), ForeColor = Color.White };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 94, Height = 34, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 8);
        UiPolish.RoundButton(cancel, 8);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(buttons, 0, 6);
        table.SetColumnSpan(buttons, 2);
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
        UiPolish.ApplyDialogSurface(this);
    }

    private void ChooseLogo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a company logo",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            using var source = Image.FromFile(dialog.FileName);
            var scale = Math.Min(1.0, Math.Min(1200.0 / source.Width, 600.0 / source.Height));
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var normalized = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(normalized))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            using var stream = new MemoryStream();
            normalized.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            if (stream.Length > 2_000_000)
                throw new InvalidDataException("The logo is too large after resizing. Choose a simpler image under 2 MB.");
            _companyLogoPngBase64 = Convert.ToBase64String(stream.ToArray());
            LoadLogoPreview();
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, $"The selected logo could not be used: {ex.Message}", "Company Logo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void LoadLogoPreview()
    {
        var previous = _logoPreview.Image;
        _logoPreview.Image = null;
        previous?.Dispose();
        if (string.IsNullOrWhiteSpace(_companyLogoPngBase64))
            return;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(_companyLogoPngBase64));
            using var image = Image.FromStream(stream);
            _logoPreview.Image = new Bitmap(image);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OutOfMemoryException)
        {
            _companyLogoPngBase64 = string.Empty;
        }
    }

    private static void AddRow(TableLayoutPanel table, int row, string text, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        table.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 6, 0, 6);
        table.Controls.Add(input, 1, row);
    }
}
