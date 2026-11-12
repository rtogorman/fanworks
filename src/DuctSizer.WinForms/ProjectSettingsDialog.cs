using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class ProjectSettingsDialog : DpiScaledForm
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
    private readonly UnitSystem _units;
    private readonly RadioButton _imperialUnits = new() { Name = "SettingsImperialUnitsRadio", Text = "Imperial (in., ft, cfm, in. w.c., °F)", AutoSize = true };
    private readonly RadioButton _metricUnits = new() { Name = "SettingsMetricUnitsRadio", Text = "Metric (mm, m, L/s, Pa, °C)", AutoSize = true };
    private readonly TextBox _sizingIncrement = new() { Name = "SettingsSizingIncrementText", TextAlign = HorizontalAlignment.Right };
    private readonly ComboBox _roundingMethod = new() { Name = "SettingsRoundingMethodCombo", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _equivalentMethod = new() { Name = "SettingsEquivalentMethodCombo", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _pressureBasis = new() { Name = "SettingsPressureBasisCombo", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TabControl _pages = new() { Name = "ProjectSettingsPages", Dock = DockStyle.Fill };

    internal UnitSystem SelectedUnits => _metricUnits.Checked ? UnitSystem.Metric : UnitSystem.Imperial;
    internal double SizingIncrementM { get; private set; }
    internal SizeRoundingDirection RoundingDirection => Choice(_roundingMethod, SizeRoundingDirection.Automatic);
    internal EquivalentDiameterMethod EquivalentDiameterMethod => Choice(_equivalentMethod, EquivalentDiameterMethod.Huebscher);
    internal PressureLossBasis PressureLossBasis => Choice(_pressureBasis, PressureLossBasis.EquivalentRoundArea);

    /// <summary>Which page to show first.</summary>
    internal enum Page { ProjectInformation, Units, CalculationSettings }

    internal string UserName => _userName.Text.Trim();
    internal string ProjectName => _projectName.Text.Trim();
    internal string ProjectNumber => _projectNumber.Text.Trim();
    internal string ProjectDate => _projectDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    internal string CompanyLogoPngBase64 => _companyLogoPngBase64;
    /// <summary>
    /// One form for everything that belongs to the project: identity (user, project name and number, date,
    /// company logo), display units, and the calculation settings used by sizing (autosize increment and
    /// rounding, equivalent-diameter method, duct area basis). Values are applied only when the user saves.
    /// </summary>
    internal ProjectSettingsDialog(DuctSystemDocument document, UnitSystem units, Page firstPage = Page.ProjectInformation)
    {
        _units = units;
        _companyLogoPngBase64 = document.CompanyLogoPngBase64;
        Text = "Project Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(680, 480);
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

        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var projectPage = new TabPage("Project Information") { Name = "ProjectInformationPage", BackColor = BackColor };
        projectPage.Controls.Add(table);

        _imperialUnits.Checked = units == UnitSystem.Imperial;
        _metricUnits.Checked = units == UnitSystem.Metric;
        var unitsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 22, 20, 20),
            BackColor = BackColor
        };
        unitsLayout.Controls.Add(new Label
        {
            Text = "Units used for input and display. Calculations are always done in SI internally, so switching "
                   + "units converts the displayed values without changing any result.",
            AutoSize = false,
            Size = new Size(600, 48),
            ForeColor = Color.FromArgb(48, 65, 80)
        });
        _imperialUnits.Margin = new Padding(4, 8, 0, 4);
        _metricUnits.Margin = new Padding(4, 4, 0, 4);
        unitsLayout.Controls.Add(_imperialUnits);
        unitsLayout.Controls.Add(_metricUnits);
        var unitsPage = new TabPage("Units") { Name = "UnitsPage", BackColor = BackColor };
        unitsPage.Controls.Add(unitsLayout);

        _sizingIncrement.Text = (units == UnitSystem.Imperial
            ? UnitConversions.MToInches(document.DefaultSizingIncrementM)
            : UnitConversions.MToMillimeters(document.DefaultSizingIncrementM)).ToString("0.####", CultureInfo.CurrentCulture);
        _roundingMethod.Items.AddRange(new object[]
        {
            new SettingChoice<SizeRoundingDirection>("Automatic (.5 rounds up)", SizeRoundingDirection.Automatic),
            new SettingChoice<SizeRoundingDirection>("Round Down", SizeRoundingDirection.Down)
        });
        _equivalentMethod.Items.AddRange(new object[]
        {
            new SettingChoice<EquivalentDiameterMethod>("Huebscher", EquivalentDiameterMethod.Huebscher),
            new SettingChoice<EquivalentDiameterMethod>("Hydraulic Diameter", EquivalentDiameterMethod.HydraulicDiameter)
        });
        _pressureBasis.Items.AddRange(new object[]
        {
            new SettingChoice<PressureLossBasis>("Equivalent Round Area", PressureLossBasis.EquivalentRoundArea),
            new SettingChoice<PressureLossBasis>("Actual Duct Area", PressureLossBasis.ActualDuctArea)
        });
        Select(_roundingMethod, document.DefaultSizeRoundingDirection);
        Select(_equivalentMethod, document.DefaultEquivalentDiameterMethod);
        Select(_pressureBasis, document.DefaultPressureLossBasis);
        var calculationTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), BackColor = BackColor };
        calculationTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        calculationTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(calculationTable, 0, units == UnitSystem.Imperial ? "Autosize Increment (in)" : "Autosize Increment (mm)", _sizingIncrement);
        AddRow(calculationTable, 1, "Autosize Rounding", _roundingMethod);
        AddRow(calculationTable, 2, "Equivalent Diameter Method", _equivalentMethod);
        AddRow(calculationTable, 3, "Duct Area Calculation Method", _pressureBasis);
        calculationTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        calculationTable.Controls.Add(new Label
        {
            Text = "The autosize increment is the same value as the ribbon's Auto Duct Size Rounding choice.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(4, 10, 0, 0),
            ForeColor = Color.FromArgb(72, 92, 110)
        }, 0, 4);
        calculationTable.SetColumnSpan(calculationTable.GetControlFromPosition(0, 4)!, 2);
        var calculationPage = new TabPage("Calculation Settings") { Name = "CalculationSettingsPage", BackColor = BackColor };
        calculationPage.Controls.Add(calculationTable);

        _pages.TabPages.Add(projectPage);
        _pages.TabPages.Add(unitsPage);
        _pages.TabPages.Add(calculationPage);
        _pages.SelectedIndex = (int)firstPage;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(0, 8, 16, 8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = BackColor
        };
        var save = new Button { Text = "Save", Width = 94, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(14, 111, 230), ForeColor = Color.White };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 94, Height = 34, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(save, 8);
        UiPolish.RoundButton(cancel, 8);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        save.Name = "ProjectSettingsSaveButton";
        save.Click += (_, _) => SaveSettings();
        Controls.Add(_pages);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        UiPolish.ApplyDialogSurface(this);
    }

    private void SaveSettings()
    {
        if ((!double.TryParse(_sizingIncrement.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.CurrentCulture, out var displayed)
             && !double.TryParse(_sizingIncrement.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.InvariantCulture, out displayed))
            || !double.IsFinite(displayed) || displayed < 0)
        {
            _pages.SelectedIndex = (int)Page.CalculationSettings;
            MessageBox.Show(this, "Enter a nonnegative autosize increment.", "Invalid Calculation Setting",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _sizingIncrement.Focus();
            _sizingIncrement.SelectAll();
            return;
        }
        SizingIncrementM = _units == UnitSystem.Imperial
            ? UnitConversions.InchesToM(displayed) : UnitConversions.MillimetersToM(displayed);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static T Choice<T>(ComboBox combo, T fallback) where T : struct =>
        (combo.SelectedItem as SettingChoice<T>)?.Value ?? fallback;

    private static void Select<T>(ComboBox combo, T value) where T : struct =>
        combo.SelectedItem = combo.Items.Cast<SettingChoice<T>>().First(x => EqualityComparer<T>.Default.Equals(x.Value, value));

    private sealed record SettingChoice<T>(string Label, T Value) { public override string ToString() => Label; }

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
