using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class AutomaticSizingInputsDialog : Form
{
    private sealed record SizingChoice<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
    private static readonly string[] Items =
    {
        "Duct", "Coil", "Filter", "Elbow", "Transition", "Split", "Merge", "Damper", "Terminal", "GRD"
    };

    private static readonly IReadOnlyDictionary<string, string> FittingGroups =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Elbow"] = "Elbows",
            ["Transition"] = "Transitions",
            ["Split"] = "Diverting Fittings",
            ["Merge"] = "Converging Fittings",
            ["Damper"] = "Dampers",
            ["Terminal"] = FittingCatalog.TerminalSelectionGroup
        };

    private readonly UnitSystem _units;
    private readonly ListBox _selector = new();
    private readonly Panel _editorHost = new();
    private readonly Dictionary<string, Control> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Control> _fittingInputs = new(StringComparer.OrdinalIgnoreCase);
    private string _currentItem = "Duct";
    private FittingDefinition? _activeDefinition;
    private bool _loading;

    internal AutomaticSizingSettings Settings { get; }

    internal AutomaticSizingInputsDialog(AutomaticSizingSettings settings, UnitSystem units)
    {
        _units = units;
        var jsonOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        Settings = JsonSerializer.Deserialize<AutomaticSizingSettings>(
                       JsonSerializer.Serialize(settings ?? new AutomaticSizingSettings(), jsonOptions), jsonOptions)
                   ?? new AutomaticSizingSettings();

        Text = "Sizing Criteria";
        Name = "AutomaticSizingInputsDialog";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(860, 620);
        ClientSize = new Size(980, 700);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(242, 246, 249);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var heading = new Label
        {
            Text = "Sizing Criteria\r\nDefaults used by Auto Size for ducts and supported components.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Color.FromArgb(28, 69, 108),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        _selector.Name = "AutomaticSizingComponentList";
        _selector.Dock = DockStyle.Fill;
        _selector.IntegralHeight = false;
        _selector.Font = new Font("Segoe UI Semibold", 10F);
        _selector.BorderStyle = BorderStyle.FixedSingle;
        _selector.Items.AddRange(Items.Cast<object>().ToArray());
        _selector.SelectedIndex = 0;
        _selector.SelectedIndexChanged += (_, _) => ChangePage();
        root.Controls.Add(_selector, 0, 1);

        _editorHost.Name = "AutomaticSizingEditorHost";
        _editorHost.Dock = DockStyle.Fill;
        _editorHost.AutoScroll = true;
        _editorHost.BackColor = Color.White;
        _editorHost.Padding = new Padding(18);
        root.Controls.Add(_editorHost, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var save = new Button
        {
            Name = "AutomaticSizingSaveButton",
            Text = "Save Defaults",
            Width = 132,
            Height = 34,
            BackColor = Color.FromArgb(42, 135, 91),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 34 };
        UiPolish.RoundButton(save, 7);
        UiPolish.RoundButton(cancel, 7);
        save.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        ShowPage("Duct");
        UiPolish.ApplyDialogSurface(this);
    }

    private void ChangePage()
    {
        if (_selector.SelectedItem is not string next || string.Equals(next, _currentItem, StringComparison.OrdinalIgnoreCase))
            return;
        SaveCurrent(showErrors: false);
        ShowPage(next);
    }

    private void ShowPage(string item)
    {
        _currentItem = item;
        _controls.Clear();
        _fittingInputs.Clear();
        _activeDefinition = null;
        _editorHost.Controls.Clear();
        _loading = true;
        try
        {
            Control page = item switch
            {
                "Duct" => BuildDuctPage(),
                "Coil" => BuildEquipmentPage("Coil", Settings.Coil, includeTemperature: true),
                "Filter" => BuildEquipmentPage("Filter", Settings.Filter, includeTemperature: false),
                "GRD" => BuildGrdPage(),
                _ => BuildFittingPage(item, FittingGroups[item])
            };
            page.Dock = DockStyle.Top;
            _editorHost.Controls.Add(page);
            UiPolish.ApplyDialogSurface(this);
        }
        finally
        {
            _loading = false;
        }
    }

    private Control BuildDuctPage()
    {
        var value = Settings.Duct;
        var table = EditorTable("Duct Defaults",
            "Graph-derived length and connected airflow/temperature are retained; these settings control sizing and construction.");
        var mode = Combo("DuctCalculationMethod", new object[]
        {
            new SizingChoice<CalculationMode>("Autosize (SP Loss)", CalculationMode.SizeByTargetFriction),
            new SizingChoice<CalculationMode>("Autosize (Velocity)", CalculationMode.SizeByMaximumVelocity),
            new SizingChoice<CalculationMode>("Check Existing", CalculationMode.CheckEnteredSize)
        }, value.Mode);
        var target = Input("DuctTargetPressure", DisplayFriction(value.TargetFrictionPaPerM));
        var velocity = Input("DuctTargetVelocity", DisplayVelocity(value.MaximumVelocityMPerS));
        var shape = Combo("DuctShape", Enum.GetValues<DuctShape>().Cast<object>().ToArray(), value.Shape);
        var diameter = Input("DuctDiameter", OptionalDisplaySize(value.DiameterM));
        var width = Input("DuctWidth", OptionalDisplaySize(value.WidthM));
        var height = Input("DuctHeight", DisplaySize(value.HeightM).ToString("0.##", CultureInfo.CurrentCulture));
        var material = Combo("DuctMaterial", DuctMaterialCatalog.All.Cast<object>().ToArray(),
            DuctMaterialCatalog.All.FirstOrDefault(x => string.Equals(x.Name, value.MaterialName, StringComparison.OrdinalIgnoreCase))
            ?? DuctMaterialCatalog.All.First());
        AddRow(table, "Calculation Method", mode);
        AddRow(table, _units == UnitSystem.Imperial ? "Target SP (in. w.g./100 ft)" : "Target SP (Pa/m)", target);
        AddRow(table, _units == UnitSystem.Imperial ? "Maximum Velocity (fpm)" : "Maximum Velocity (m/s)", velocity);
        AddRow(table, "Duct Geometry", shape);
        AddRow(table, SizeLabel("Existing Round Diameter"), diameter);
        AddRow(table, SizeLabel("Existing Rectangular Width"), width);
        AddRow(table, SizeLabel("Rectangular Height"), height);
        AddRow(table, "Duct Material", material);
        _controls["mode"] = mode;
        _controls["target"] = target;
        _controls["velocity"] = velocity;
        _controls["shape"] = shape;
        _controls["diameter"] = diameter;
        _controls["width"] = width;
        _controls["height"] = height;
        _controls["material"] = material;
        void UpdateVisibility()
        {
            var selectedMode = SelectedChoice(mode, CalculationMode.SizeByTargetFriction);
            target.Enabled = selectedMode == CalculationMode.SizeByTargetFriction;
            velocity.Enabled = selectedMode == CalculationMode.SizeByMaximumVelocity;
            var round = shape.SelectedItem is DuctShape.Round;
            diameter.Enabled = selectedMode == CalculationMode.CheckEnteredSize && round;
            width.Enabled = selectedMode == CalculationMode.CheckEnteredSize && !round;
            height.Enabled = !round;
        }
        mode.SelectedIndexChanged += (_, _) => UpdateVisibility();
        shape.SelectedIndexChanged += (_, _) => UpdateVisibility();
        UpdateVisibility();
        return table;
    }

    private Control BuildEquipmentPage(string title, AutomaticEquipmentSizingDefaults value, bool includeTemperature)
    {
        var table = EditorTable($"{title} Defaults",
            "Airflow and entering temperature are inferred from the connected defined system item.");
        if (includeTemperature)
        {
            var temperature = Input("EquipmentTemperature",
                DisplayTemperature(value.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture));
            _controls["temperature"] = temperature;
            AddRow(table, TemperatureLabel("Leaving Temperature"), temperature);
        }
        var pressure = Input("EquipmentPressureDrop", DisplayPressure(value.PressureDropPa));
        _controls["pressure"] = pressure;
        AddRow(table, PressureLabel("Pressure Drop"), pressure);
        return table;
    }

    private Control BuildGrdPage()
    {
        var table = EditorTable("GRD Defaults", "Airflow and temperature are inferred from the connected system.");
        var type = Combo("AutomaticGrdType", new object[] { "Grille", "Diffuser", "Linear Diffuser TBD-80 D" },
            Settings.Grd.GrdType is "Grille" or "Diffuser" or "Linear Diffuser TBD-80 D" ? Settings.Grd.GrdType : "Grille");
        var pressure = Input("AutomaticGrdPressureDrop", DisplayPressure(Settings.Grd.PressureDropPa));
        var grilleSize = Combo("AutomaticGrdTitus350RSize", GrdPerformanceCatalog.Titus350RSizes.Cast<object>().ToArray(),
            GrdPerformanceCatalog.Titus350RSizes.FirstOrDefault(value => string.Equals(value,
                Settings.Grd.NominalDuctSizeIn, StringComparison.OrdinalIgnoreCase)) ?? "12x12");
        var face = Combo("AutomaticGrdFaceSize", GrdPerformanceCatalog.PriceFaceSizes.Cast<object>().ToArray(),
            GrdPerformanceCatalog.PriceFaceSizes.OrderBy(value => Math.Abs(value - Settings.Grd.FaceSizeIn)).First());
        var neckValues = GrdPerformanceCatalog.PriceNeckDiameters(Convert.ToInt32(face.SelectedItem));
        var neck = Combo("AutomaticGrdNeckDiameter", neckValues.Cast<object>().ToArray(),
            neckValues.OrderBy(value => Math.Abs(value - Settings.Grd.NeckDiameterIn)).First());
        var length = Combo("AutomaticGrdLength", GrdPerformanceCatalog.TitusLengthsFt.Cast<object>().ToArray(),
            GrdPerformanceCatalog.TitusLengthsFt.OrderBy(value => Math.Abs(value - Settings.Grd.LengthFt)).First());
        var slotWidth = Combo("AutomaticGrdSlotWidth", GrdPerformanceCatalog.TitusSlotWidthsIn.Cast<object>().ToArray(),
            GrdPerformanceCatalog.TitusSlotWidthsIn.OrderBy(value => Math.Abs(value - Settings.Grd.SlotWidthIn)).First());
        var slotCount = Combo("AutomaticGrdSlotCount", GrdPerformanceCatalog.TitusSlotCounts.Cast<object>().ToArray(),
            GrdPerformanceCatalog.TitusSlotCounts.OrderBy(value => Math.Abs(value - Settings.Grd.SlotCount)).First());
        face.SelectedIndexChanged += (_, _) =>
        {
            var previous = neck.SelectedItem is int selected ? selected : Convert.ToInt32(Settings.Grd.NeckDiameterIn);
            var available = GrdPerformanceCatalog.PriceNeckDiameters(Convert.ToInt32(face.SelectedItem));
            neck.BeginUpdate();
            neck.Items.Clear();
            neck.Items.AddRange(available.Cast<object>().ToArray());
            neck.EndUpdate();
            neck.SelectedItem = available.OrderBy(value => Math.Abs(value - previous)).First();
        };
        _controls["grdType"] = type;
        _controls["pressure"] = pressure;
        _controls["grilleSize"] = grilleSize;
        _controls["face"] = face;
        _controls["neck"] = neck;
        _controls["length"] = length;
        _controls["slotWidth"] = slotWidth;
        _controls["slotCount"] = slotCount;
        AddRow(table, "GRD Type", type);
        AddRow(table, "Titus 350R Nominal Duct Size (in)", grilleSize);
        AddRow(table, "Price SCD Face Size (in)", face);
        AddRow(table, "Price SCD Neck Diameter (in)", neck);
        AddRow(table, "Titus TBD-80 D Length (ft)", length);
        AddRow(table, "Titus Slot Size (in)", slotWidth);
        AddRow(table, "Titus Number of Slots", slotCount);
        AddRow(table, PressureLabel("Pressure Drop"), pressure);
        return table;
    }

    private Control BuildFittingPage(string title, string group)
    {
        var defaults = Settings.Fitting(group);
        var definitions = FittingCatalog.All.Where(x =>
            string.Equals(FittingCatalog.SelectionGroup(x), group, StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected = definitions.FirstOrDefault(x => string.Equals(x.Code, defaults.FittingCode,
                           StringComparison.OrdinalIgnoreCase)) ?? definitions.First();
        var table = EditorTable($"{title} Defaults",
            "Select the default ASHRAE fitting and the inputs used for every undefined component of this type.");
        var definition = Combo("AutomaticFittingDefinition", definitions.Cast<object>().ToArray(), selected);
        var shape = Combo("AutomaticFittingShape", Enum.GetValues<DuctShape>().Cast<object>().ToArray(),
            selected.ReferenceDuctShape ?? defaults.Shape);
        var diameter = Input("AutomaticFittingDiameter", DisplaySize(defaults.DiameterM).ToString("0.##", CultureInfo.CurrentCulture));
        var width = Input("AutomaticFittingWidth", DisplaySize(defaults.WidthM).ToString("0.##", CultureInfo.CurrentCulture));
        var height = Input("AutomaticFittingHeight", DisplaySize(defaults.HeightM).ToString("0.##", CultureInfo.CurrentCulture));
        _controls["group"] = new Label { Text = group };
        _controls["definition"] = definition;
        _controls["shape"] = shape;
        _controls["diameter"] = diameter;
        _controls["width"] = width;
        _controls["height"] = height;
        AddRow(table, "Default Fitting", definition);
        AddRow(table, "Reference Duct Shape", shape);
        AddRow(table, SizeLabel("Diameter"), diameter);
        AddRow(table, SizeLabel("Width"), width);
        AddRow(table, SizeLabel("Height"), height);
        if (selected.IsJunction)
        {
            var branch = Input("AutomaticFittingBranchPercent", (defaults.BranchFraction * 100).ToString("0.##", CultureInfo.CurrentCulture));
            _controls["branch"] = branch;
            AddRow(table, title == "Merge" ? "Second Inlet Flow (%)" : "Branch Outlet Flow (%)", branch);
        }
        var parameterHost = new TableLayoutPanel
        {
            Name = "AutomaticFittingParameterTable",
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 12, 0, 0)
        };
        parameterHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        parameterHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        table.Controls.Add(parameterHost, 0, table.RowCount);
        table.SetColumnSpan(parameterHost, 2);
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowCount++;

        _activeDefinition = selected;
        PopulateFittingParameters(parameterHost, selected, defaults);
        definition.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || definition.SelectedItem is not FittingDefinition next)
                return;
            SaveFittingParameterValues(defaults, _activeDefinition);
            defaults.FittingCode = next.Code;
            defaults.Inputs = next.Inputs.ToDictionary(x => x.Key, x => x.DefaultValue,
                StringComparer.OrdinalIgnoreCase);
            _activeDefinition = next;
            if (next.ReferenceDuctShape is DuctShape required)
                shape.SelectedItem = required;
            PopulateFittingParameters(parameterHost, next, defaults);
            UpdateShapeControls();
        };
        shape.SelectedIndexChanged += (_, _) => UpdateShapeControls();
        UpdateShapeControls();
        return table;

        void UpdateShapeControls()
        {
            var required = (definition.SelectedItem as FittingDefinition)?.ReferenceDuctShape;
            shape.Enabled = required is null;
            var round = (required ?? (shape.SelectedItem is DuctShape value ? value : DuctShape.Round)) == DuctShape.Round;
            diameter.Enabled = round;
            width.Enabled = !round;
            height.Enabled = !round;
        }
    }

    private void PopulateFittingParameters(TableLayoutPanel host, FittingDefinition definition,
        AutomaticFittingSizingDefaults defaults)
    {
        _fittingInputs.Clear();
        host.Controls.Clear();
        host.RowStyles.Clear();
        var inputs = definition.Inputs.Where(x => !definition.IsJunction
                                                   || x.Key is not ("qbqc" or "qsqc" or "qbqs" or "abac" or "asac" or "abas" or "vbvc" or "vsvc" or "vbvs" or "vcfpm" or "path"))
            .ToArray();
        host.RowCount = Math.Max(1, inputs.Length + 1);
        var heading = new Label
        {
            Text = "FITTING INPUTS",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(28, 69, 108),
            TextAlign = ContentAlignment.MiddleLeft
        };
        host.Controls.Add(heading, 0, 0);
        host.SetColumnSpan(heading, 2);
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        for (var index = 0; index < inputs.Length; index++)
        {
            var spec = inputs[index];
            var saved = defaults.Inputs.GetValueOrDefault(spec.Key, spec.DefaultValue);
            Control input;
            if (spec.Choices is { Count: > 0 })
            {
                var combo = Combo($"AutomaticFittingInput_{spec.Key}", spec.Choices.Cast<object>().ToArray(),
                    spec.Choices.OrderBy(x => Math.Abs(x.Value - saved)).First());
                combo.DisplayMember = nameof(FittingInputChoice.Label);
                input = combo;
            }
            else
                input = Input($"AutomaticFittingInput_{spec.Key}", saved.ToString("0.####", CultureInfo.CurrentCulture));
            _fittingInputs[spec.Key] = input;
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            host.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(spec.Unit) ? spec.Label : $"{spec.Label} ({spec.Unit})",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, index + 1);
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(4, 5, 2, 5);
            host.Controls.Add(input, 1, index + 1);
        }
    }

    private void SaveAndClose()
    {
        if (!SaveCurrent(showErrors: true))
            return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool SaveCurrent(bool showErrors)
    {
        try
        {
            switch (_currentItem)
            {
                case "Duct": SaveDuct(); break;
                case "Coil": SaveEquipment(Settings.Coil, includeTemperature: true); break;
                case "Filter": SaveEquipment(Settings.Filter, includeTemperature: false); break;
                case "GRD": SaveGrd(); break;
                default: SaveFitting(); break;
            }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            if (showErrors)
                MessageBox.Show(this, ex.Message, "Invalid Automatic Sizing Default",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
    }

    private void SaveDuct()
    {
        var value = Settings.Duct;
        value.Mode = SelectedChoice((ComboBox)_controls["mode"], CalculationMode.SizeByTargetFriction);
        value.TargetFrictionPaPerM = StoredFriction(Positive(_controls["target"], "target static pressure loss"));
        value.MaximumVelocityMPerS = StoredVelocity(Positive(_controls["velocity"], "maximum velocity"));
        value.Shape = (DuctShape)((ComboBox)_controls["shape"]).SelectedItem!;
        if (value.Mode == CalculationMode.CheckEnteredSize)
        {
            if (value.Shape == DuctShape.Round)
            {
                value.DiameterM = StoredSize(Positive(_controls["diameter"], "diameter"));
                value.WidthM = 0.0;
            }
            else
            {
                value.DiameterM = 0.0;
                value.WidthM = StoredSize(Positive(_controls["width"], "width"));
                value.HeightM = StoredSize(Positive(_controls["height"], "height"));
            }
        }
        else
        {
            value.DiameterM = 0.0;
            value.WidthM = 0.0;
            if (value.Shape == DuctShape.Rectangular)
                value.HeightM = StoredSize(Positive(_controls["height"], "height"));
        }
        value.MaterialName = (((ComboBox)_controls["material"]).SelectedItem as DuctMaterial)?.Name
                             ?? throw new InvalidOperationException("Select a duct material.");
    }

    private void SaveEquipment(AutomaticEquipmentSizingDefaults value, bool includeTemperature)
    {
        if (includeTemperature)
            value.TemperatureC = StoredTemperature(Number(_controls["temperature"], "leaving temperature"));
        value.PressureDropPa = StoredPressure(Nonnegative(_controls["pressure"], "pressure drop"));
    }

    private void SaveGrd()
    {
        Settings.Grd.GrdType = Convert.ToString(((ComboBox)_controls["grdType"]).SelectedItem,
                                       CultureInfo.CurrentCulture) ?? "Grille";
        Settings.Grd.PressureDropPa = StoredPressure(Nonnegative(_controls["pressure"], "pressure drop"));
        Settings.Grd.NominalDuctSizeIn = Convert.ToString(((ComboBox)_controls["grilleSize"]).SelectedItem,
            CultureInfo.CurrentCulture) ?? "12x12";
        Settings.Grd.FaceSizeIn = Convert.ToDouble(((ComboBox)_controls["face"]).SelectedItem, CultureInfo.CurrentCulture);
        Settings.Grd.NeckDiameterIn = Convert.ToDouble(((ComboBox)_controls["neck"]).SelectedItem, CultureInfo.CurrentCulture);
        Settings.Grd.LengthFt = Convert.ToDouble(((ComboBox)_controls["length"]).SelectedItem, CultureInfo.CurrentCulture);
        Settings.Grd.SlotWidthIn = Convert.ToDouble(((ComboBox)_controls["slotWidth"]).SelectedItem, CultureInfo.CurrentCulture);
        Settings.Grd.SlotCount = Convert.ToInt32(((ComboBox)_controls["slotCount"]).SelectedItem, CultureInfo.CurrentCulture);
    }

    private void SaveFitting()
    {
        var group = ((Label)_controls["group"]).Text;
        var defaults = Settings.Fitting(group);
        var definition = ((ComboBox)_controls["definition"]).SelectedItem as FittingDefinition
                         ?? throw new InvalidOperationException("Select a default fitting.");
        defaults.FittingCode = definition.Code;
        defaults.Shape = definition.ReferenceDuctShape
                         ?? (DuctShape)((ComboBox)_controls["shape"]).SelectedItem!;
        defaults.DiameterM = StoredSize(Positive(_controls["diameter"], "diameter"));
        defaults.WidthM = StoredSize(Positive(_controls["width"], "width"));
        defaults.HeightM = StoredSize(Positive(_controls["height"], "height"));
        if (_controls.TryGetValue("branch", out var branch))
            defaults.BranchFraction = Math.Clamp(Positive(branch, "branch flow percentage") / 100.0, 0.01, 0.99);
        SaveFittingParameterValues(defaults, definition, throwOnError: true);
    }

    private void SaveFittingParameterValues(AutomaticFittingSizingDefaults defaults,
        FittingDefinition? definition, bool throwOnError = false)
    {
        if (definition is null)
            return;
        foreach (var spec in definition.Inputs)
        {
            if (!_fittingInputs.TryGetValue(spec.Key, out var input))
                continue;
            try
            {
                var value = input switch
                {
                    ComboBox combo when combo.SelectedItem is FittingInputChoice choice => choice.Value,
                    _ => Number(input, spec.Label)
                };
                if (value < spec.Minimum || value > spec.Maximum)
                    throw new ArgumentOutOfRangeException(spec.Key,
                        $"{spec.Label} must be between {spec.Minimum:g} and {spec.Maximum:g}.");
                defaults.Inputs[spec.Key] = value;
            }
            catch when (!throwOnError)
            {
                defaults.Inputs[spec.Key] = spec.DefaultValue;
            }
        }
    }

    private TableLayoutPanel EditorTable(string title, string detail)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(4),
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var header = new Label
        {
            Text = $"{title}\r\n{detail}",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Color.FromArgb(28, 69, 108),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        table.Controls.Add(header, 0, 0);
        table.SetColumnSpan(header, 2);
        table.RowCount = 1;
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control input)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(48, 65, 80)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 6, 4, 6);
        table.Controls.Add(input, 1, row);
    }

    private static ComboBox Combo(string name, object[] values, object selected)
    {
        var combo = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values);
        combo.SelectedItem = values.FirstOrDefault(x => EqualsChoice(x, selected)) ?? values.FirstOrDefault();
        return combo;
    }

    private static bool EqualsChoice(object candidate, object selected) => candidate switch
    {
        SizingChoice<CalculationMode> choice when selected is CalculationMode value => choice.Value == value,
        _ => Equals(candidate, selected)
    };

    private static T SelectedChoice<T>(ComboBox combo, T fallback) where T : struct =>
        combo.SelectedItem is SizingChoice<T> choice ? choice.Value : fallback;

    private static TextBox Input(string name, string value) => new()
    {
        Name = name,
        Text = value,
        TextAlign = HorizontalAlignment.Right
    };

    private double Number(Control control, string name)
    {
        if (control is TextBox input
            && double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            && double.IsFinite(value))
            return value;
        throw new ArgumentException($"Enter a valid {name}.");
    }

    private double Positive(Control control, string name)
    {
        var value = Number(control, name);
        if (value <= 0)
            throw new ArgumentException($"Enter a positive {name}.");
        return value;
    }

    private double Nonnegative(Control control, string name)
    {
        var value = Number(control, name);
        if (value < 0)
            throw new ArgumentException($"Enter a nonnegative {name}.");
        return value;
    }

    private string SizeLabel(string label) => $"{label} ({(_units == UnitSystem.Imperial ? "in" : "mm")})";
    private string PressureLabel(string label) => $"{label} ({(_units == UnitSystem.Imperial ? "in. w.g." : "Pa")})";
    private string TemperatureLabel(string label) => $"{label} ({(_units == UnitSystem.Imperial ? "°F" : "°C")})";
    private double DisplaySize(double meters) => _units == UnitSystem.Imperial
        ? UnitConversions.MToInches(meters) : UnitConversions.MToMillimeters(meters);
    private string OptionalDisplaySize(double meters) => meters > 0.0
        ? DisplaySize(meters).ToString("0.##", CultureInfo.CurrentCulture)
        : string.Empty;
    private double StoredSize(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.InchesToM(value) : UnitConversions.MillimetersToM(value);
    private string DisplayFriction(double value) => (_units == UnitSystem.Imperial
        ? UnitConversions.PaPerMToInchesWaterPer100Feet(value) : value).ToString("0.####", CultureInfo.CurrentCulture);
    private double StoredFriction(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.InchesWaterPer100FeetToPaPerM(value) : value;
    private string DisplayVelocity(double value) => (_units == UnitSystem.Imperial
        ? UnitConversions.MpsToFpm(value) : value).ToString("0.##", CultureInfo.CurrentCulture);
    private double StoredVelocity(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.FpmToMps(value) : value;
    private string DisplayPressure(double value) => (_units == UnitSystem.Imperial
        ? UnitConversions.PaToInchesWater(value) : value).ToString("0.###", CultureInfo.CurrentCulture);
    private double StoredPressure(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.InchesWaterToPa(value) : value;
    private double DisplayTemperature(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.CelsiusToFahrenheit(value) : value;
    private double StoredTemperature(double value) => _units == UnitSystem.Imperial
        ? UnitConversions.FahrenheitToCelsius(value) : value;
}
