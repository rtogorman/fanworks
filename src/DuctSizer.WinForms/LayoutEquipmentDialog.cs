using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

internal sealed class LayoutEquipmentDialog : DpiScaledForm, IInlineEditable
{
    private readonly List<InlineEditRow> _inlineRows = new();
    private IWin32Window? _messageOwner;
    private readonly SavedLayoutNode _node;
    private readonly TextBox _componentDescription = new();
    private readonly TextBox _airflow = new();
    private readonly TextBox _temperature = new();
    private readonly TextBox _pressureDrop = new();
    private readonly TextBox _fanEfficiency = new();
    private readonly TextBox _ahuInternalStatic = new();
    private readonly TextBox _deltaX = new();
    private readonly TextBox _deltaY = new();
    private readonly TextBox _deltaZ = new();
    private readonly ComboBox _airSystem = new();
    private readonly ComboBox _grdType = new();
    private readonly ComboBox _grdTitus350RSize = new();
    private readonly ComboBox _grdFaceSize = new();
    private readonly ComboBox _grdNeckDiameter = new();
    private readonly ComboBox _grdLength = new();
    private readonly ComboBox _grdSlotWidth = new();
    private readonly ComboBox _grdSlotCount = new();
    private readonly Label _grdPerformanceResult = new();
    private readonly ComboBox _spaceRole = new();
    private readonly TextBox _calculatedValue = new();
    private readonly ComboBox _upstream = new();
    private readonly Label _downstream = new();
    private readonly bool _showTopology;
    private readonly bool _absoluteLocation;
    private readonly LayoutEditableAxes? _editableAxes;
    private LayoutCoordinate _desiredAbsoluteCoordinate;
    private readonly LayoutPositionReference? _reference;

    internal string SelectedParentNodeId =>
        (_upstream.SelectedItem as LayoutNodeChoice)?.Id ?? _node.ParentNodeId;
    internal string SelectedAirSystemType =>
        Convert.ToString(_airSystem.SelectedItem, CultureInfo.CurrentCulture) ?? _node.AirSystemType;
    internal LayoutCoordinate DesiredAbsoluteCoordinate => _desiredAbsoluteCoordinate;

    internal LayoutEquipmentDialog(SavedLayoutNode node)
        : this(node, Array.Empty<SavedLayoutNode>(), _ => string.Empty, false, null, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource)
        : this(node, nodes, captionSource, true, null, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, LayoutCoordinate absoluteCoordinate)
        : this(node, nodes, captionSource, true, absoluteCoordinate, null)
    {
    }

    internal LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, LayoutCoordinate absoluteCoordinate,
        LayoutEditableAxes editableAxes, LayoutPositionReference? reference = null)
        : this(node, nodes, captionSource, true, absoluteCoordinate, editableAxes, reference)
    {
    }

    private LayoutEquipmentDialog(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, bool showTopology, LayoutCoordinate? absoluteCoordinate,
        LayoutEditableAxes? editableAxes, LayoutPositionReference? reference = null)
    {
        _node = node;
        _reference = absoluteCoordinate.HasValue ? reference : null;
        _showTopology = showTopology;
        _absoluteLocation = absoluteCoordinate.HasValue;
        _editableAxes = editableAxes;
        var reservoir = node.Type == LayoutNodeType.Reservoir;
        var fan = node.Type == LayoutNodeType.Fan;
        var airHandlingUnit = node.IsAirHandlingUnit;
        var filter = node.Type == LayoutNodeType.Filter;
        var grd = node.Type == LayoutNodeType.Grd;
        var calculationPoint = node.Type == LayoutNodeType.CalculationPoint;
        var custom = node.Type == LayoutNodeType.CustomComponent;
        var sourceSpace = reservoir && (string.Equals(node.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(node.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase));
        Text = reservoir ? $"{(sourceSpace ? "Space Source" : "Space Sink")} Inputs"
            : calculationPoint ? $"{node.CalculationPointType} Calculation Point"
            : custom ? "Custom Component Inputs"
            : airHandlingUnit ? "Air Handling Unit Inputs"
            : fan ? "System Fan Inputs" : filter ? "Filter Inputs" : grd ? "GRD Inputs" : "Coil Inputs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 320);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Shown += (_, _) => UiPolish.ApplyDialogSurface(this);

        _airflow.Text = node.AirflowM3PerS > 0
            ? UnitConversions.M3PerSToCfm(node.AirflowM3PerS).ToString("0.##", CultureInfo.CurrentCulture)
            : "1000";
        _airflow.Name = "LayoutEquipmentAirflowText";
        _pressureDrop.Name = "LayoutEquipmentPressureDropText";
        _componentDescription.Name = "LayoutCustomDescriptionText";
        _componentDescription.Text = string.IsNullOrWhiteSpace(node.Description) ? "Custom Component" : node.Description;
        _fanEfficiency.Name = "LayoutFanEfficiencyText";
        _fanEfficiency.Text = node.FanEfficiencyPercent.ToString("0.##", CultureInfo.CurrentCulture);
        _ahuInternalStatic.Name = "LayoutAhuInternalStaticText";
        _ahuInternalStatic.Text = UnitConversions.PaToInchesWater(node.AhuInternalStaticPressurePa)
            .ToString("0.###", CultureInfo.CurrentCulture);
        _deltaX.Name = "LayoutEquipmentDeltaXText";
        _deltaY.Name = "LayoutEquipmentDeltaYText";
        _deltaZ.Name = "LayoutEquipmentDeltaZText";
        _airSystem.Name = "LayoutEquipmentAirSystemCombo";
        _grdType.Name = "LayoutGrdTypeCombo";
        _grdTitus350RSize.Name = "LayoutGrdTitus350RSizeCombo";
        _grdFaceSize.Name = "LayoutGrdFaceSizeCombo";
        _grdNeckDiameter.Name = "LayoutGrdNeckDiameterCombo";
        _grdLength.Name = "LayoutGrdLengthCombo";
        _grdSlotWidth.Name = "LayoutGrdSlotWidthCombo";
        _grdSlotCount.Name = "LayoutGrdSlotCountCombo";
        _grdPerformanceResult.Name = "LayoutGrdPerformanceResult";
        _spaceRole.Name = "LayoutSpaceRoleCombo";
        _temperature.Text = UnitConversions.CelsiusToFahrenheit(node.TemperatureC).ToString("0.##", CultureInfo.CurrentCulture);
        _pressureDrop.Text = UnitConversions.PaToInchesWater(sourceSpace ? node.PressurePa
                : fan ? node.FanStaticPressurePa : node.PressureDropPa)
            .ToString("0.###", CultureInfo.CurrentCulture);
        var location = absoluteCoordinate ?? new LayoutCoordinate(node.DeltaXM, node.DeltaYM, node.DeltaZM);
        _desiredAbsoluteCoordinate = location;
        // Positions are stored absolute; the user sees and enters the offset from the reference component.
        if (absoluteCoordinate.HasValue)
            location = LayoutPositionReference.ToDisplay(location, _reference);
        _deltaX.Text = UnitConversions.MToFeet(location.XM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaY.Text = UnitConversions.MToFeet(location.YM).ToString("0.##", CultureInfo.CurrentCulture);
        _deltaZ.Text = UnitConversions.MToFeet(location.ZM).ToString("0.##", CultureInfo.CurrentCulture);
        var coordinateInputs = new[]
        {
            (Input: _deltaX, Editable: editableAxes?.X ?? true),
            (Input: _deltaY, Editable: editableAxes?.Y ?? true),
            (Input: _deltaZ, Editable: editableAxes?.Z ?? true)
        };
        foreach (var coordinateInput in coordinateInputs)
        {
            coordinateInput.Input.ReadOnly = _absoluteLocation && !coordinateInput.Editable;
            if (coordinateInput.Input.ReadOnly)
                coordinateInput.Input.BackColor = Color.FromArgb(235, 241, 246);
        }
        _airSystem.DropDownStyle = ComboBoxStyle.DropDownList;
        _airSystem.Items.AddRange(MainForm.AirSystemTypes.Cast<object>().ToArray());
        _airSystem.SelectedItem = _airSystem.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.AirSystemType, StringComparison.OrdinalIgnoreCase)) ?? "Supply Air";
        _grdType.DropDownStyle = ComboBoxStyle.DropDownList;
        _grdType.Items.AddRange(new object[] { "Grille", "Diffuser", "Linear Diffuser TBD-80 D" });
        _grdType.SelectedItem = _grdType.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.GrdType, StringComparison.OrdinalIgnoreCase)) ?? "Grille";
        ConfigureGrdPerformanceControls(node);
        _spaceRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _spaceRole.Items.AddRange(new object[] { "Space Sink", "Space Source" });
        _spaceRole.SelectedItem = _spaceRole.Items.Cast<string>().FirstOrDefault(x =>
            string.Equals(x, node.SpaceRole, StringComparison.OrdinalIgnoreCase)) ?? (sourceSpace ? "Space Source" : "Space Sink");
        _spaceRole.Enabled = false;
        _calculatedValue.Name = "LayoutCalculationPointValue";
        _calculatedValue.ReadOnly = true;
        _calculatedValue.BackColor = Color.FromArgb(235, 241, 246);
        _calculatedValue.Text = node.CalculationPointType switch
        {
            "Temperature" => $"{UnitConversions.CelsiusToFahrenheit(node.TemperatureC):0.##} °F",
            "Velocity" => $"{UnitConversions.MpsToFpm(node.VelocityMPerS):N0} fpm",
            "Airflow" => $"{UnitConversions.M3PerSToCfm(node.AirflowM3PerS):N0} cfm",
            _ => $"{UnitConversions.PaToInchesWater(node.PressurePa):0.###} in. w.g."
        };
        if (showTopology)
        {
            PopulateRelationshipControls(node, nodes, captionSource, _upstream, _downstream);
            _upstream.Name = "LayoutUpstreamItemCombo";
            _downstream.Name = "LayoutDownstreamItemsLabel";
        }

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12 + (showTopology ? 2 : 0),
            Padding = new Padding(18),
            BackColor = BackColor
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        AddRow(table, 0, reservoir ? (sourceSpace ? "Space Source" : "Space Sink")
            : calculationPoint ? $"{node.CalculationPointType} Calculation Point"
            : custom ? "Custom Component"
            : airHandlingUnit ? "Air Handling Unit"
            : fan ? "System Fan" : filter ? "Air Filter" : grd ? "Grille / Diffuser" : "Cooling / Heating Coil", new Label
        {
            Text = reservoir ? sourceSpace
                    ? "System airflow source with entered temperature and gauge pressure"
                    : "Terminal sink with editable design airflow and temperature calculated from the upstream system"
                : calculationPoint ? "Read-only value calculated at this duct location"
                : custom ? "Enter the component description and its static pressure drop"
                : airHandlingUnit ? "Design airflow, air temperature, and external and internal static pressure"
                : fan ? "Design airflow and entering-air temperature"
                : filter ? "Enter the filter pressure drop"
                : grd ? "Select the outlet type and enter its airflow and pressure drop"
                : "Leaving-air temperature and coil pressure drop",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(16, 45, 84),
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        });
        var row = 1;
        if (reservoir)
        {
            AddRow(table, row++, "Space Connection", _spaceRole);
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Space Temperature (°F)", _temperature);
            if (sourceSpace)
                AddRow(table, row++, "Source Pressure (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
            if (!sourceSpace)
            {
                _temperature.ReadOnly = true;
                _temperature.BackColor = Color.FromArgb(235, 241, 246);
            }
        }
        else if (airHandlingUnit)
        {
            // External static pressure (the duct system; set by Auto-size) and internal static pressure (filters,
            // coils and other internal components, from the manufacturer). Total = external + internal.
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Air Temperature (°F)", _temperature);
            AddRow(table, row++, "External Static Pressure (in. w.c.)", _pressureDrop);
            AddRow(table, row++, "Internal Static Pressure (in. w.c.)", _ahuInternalStatic);
            AddRow(table, row++, "Fan Efficiency", LayoutDialogControls.CreateInputWithUnit(_fanEfficiency, "%"));
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (fan)
        {
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Air Temperature (°F)", _temperature);
            AddRow(table, row++, "System Static Pressure (in. w.c.)", _pressureDrop);
            AddRow(table, row++, "Fan Efficiency", LayoutDialogControls.CreateInputWithUnit(_fanEfficiency, "%"));
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (grd)
        {
            AddRow(table, row++, "GRD Type", _grdType);
            AddRow(table, row++, "Airflow (cfm)", _airflow);
            AddRow(table, row++, "Published Selection", BuildGrdPerformancePanel(), 232);
            AddGrdInlineRows();
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (calculationPoint)
        {
            AddRow(table, row++, "Calculated Value", _calculatedValue);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (filter)
        {
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else if (custom)
        {
            AddRow(table, row++, "Component Description", _componentDescription);
            AddRow(table, row++, "Static Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        else
        {
            AddRow(table, row++, "Leaving Temperature (°F)", _temperature);
            AddRow(table, row++, "Pressure Drop (in. w.g.)", _pressureDrop);
            AddRow(table, row++, "Air System Type", _airSystem);
        }
        var positionCaption = _absoluteLocation ? LayoutPositionReference.CaptionFor(_reference) : "Relative ΔX / ΔY / ΔZ (ft)";
        AddRow(table, row++, positionCaption,
            LayoutDialogControls.CreatePositionEditor(positionCaption, _deltaX, _deltaY, _deltaZ));
        if (table.GetControlFromPosition(0, row - 1) is Label locationCaption)
        {
            locationCaption.Name = "LayoutLocationCaption";
            // Replace the generic row recorded by AddRow with a position row for the inspector.
            _inlineRows.RemoveAt(_inlineRows.Count - 1);
            _inlineRows.Add(InlineEditRow.ForPosition(locationCaption.Text, table.GetControlFromPosition(1, row - 1)!, _deltaX, _deltaY, _deltaZ));
        }
        if (showTopology)
        {
            AddRow(table, row++, "Upstream Item / Fitting", _upstream);
            AddRow(table, row++, "Downstream Item(s)", _downstream);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.None, Width = 90, Height = 32, BackColor = Color.FromArgb(14, 111, 230), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32, FlatStyle = FlatStyle.Flat };
        UiPolish.RoundButton(ok, 7);
        UiPolish.RoundButton(cancel, 7);
        ok.Click += (_, _) => SaveInputs();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        table.Controls.Add(buttons, 0, row);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        ClientSize = new Size(520, 136 + Math.Max(0, row - 1) * 40 + (grd ? 192 : 0));
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void ConfigureGrdPerformanceControls(SavedLayoutNode node)
    {
        foreach (var combo in new[] { _grdTitus350RSize, _grdFaceSize, _grdNeckDiameter, _grdLength, _grdSlotWidth, _grdSlotCount })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        _grdTitus350RSize.Items.AddRange(GrdPerformanceCatalog.Titus350RSizes.Cast<object>().ToArray());
        _grdFaceSize.Items.AddRange(GrdPerformanceCatalog.PriceFaceSizes.Cast<object>().ToArray());
        _grdLength.Items.AddRange(GrdPerformanceCatalog.TitusLengthsFt.Cast<object>().ToArray());
        foreach (var slotWidth in GrdPerformanceCatalog.TitusSlotWidthsIn)
            _grdSlotWidth.Items.Add(new LayoutValueChoice<double>(slotWidth switch
            {
                0.75 => "3/4 in",
                1.5 => "1-1/2 in",
                _ => "1 in"
            }, slotWidth));
        foreach (var slotCount in GrdPerformanceCatalog.TitusSlotCounts)
            _grdSlotCount.Items.Add(new LayoutValueChoice<double>($"{slotCount} slot{(slotCount == 1 ? "" : "s")}", slotCount));
        _grdFaceSize.SelectedItem = GrdPerformanceCatalog.PriceFaceSizes
            .OrderBy(value => Math.Abs(value - node.GrdFaceSizeIn)).First();
        _grdTitus350RSize.SelectedItem = GrdPerformanceCatalog.Titus350RSizes.FirstOrDefault(value =>
            string.Equals(value, node.GrdNominalDuctSizeIn, StringComparison.OrdinalIgnoreCase)) ?? "12x12";
        _grdLength.SelectedItem = GrdPerformanceCatalog.TitusLengthsFt
            .OrderBy(value => Math.Abs(value - node.GrdLengthFt)).First();
        _grdSlotWidth.SelectedItem = _grdSlotWidth.Items.Cast<LayoutValueChoice<double>>()
            .OrderBy(value => Math.Abs(value.Value - node.GrdSlotWidthIn)).First();
        _grdSlotCount.SelectedItem = _grdSlotCount.Items.Cast<LayoutValueChoice<double>>()
            .OrderBy(value => Math.Abs(value.Value - node.GrdSlotCount)).First();
        PopulateGrdNeckChoices(node.GrdNeckDiameterIn);
        _grdPerformanceResult.AutoSize = true;
        _grdPerformanceResult.MaximumSize = new Size(260, 0);
        _grdPerformanceResult.ForeColor = Color.FromArgb(16, 45, 84);
        _grdPerformanceResult.Font = new Font("Segoe UI Semibold", 8F);
        // Only a GRD with a published selection takes its pressure drop from the catalog; every other component
        // (coil, filter, custom, fan static pressure, source pressure) keeps the box editable.
        _pressureDrop.ReadOnly = node.Type == LayoutNodeType.Grd && IsPublishedGrd();
        if (_pressureDrop.ReadOnly)
            _pressureDrop.BackColor = Color.FromArgb(235, 241, 246);
        _grdType.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdTitus350RSize.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdFaceSize.SelectedIndexChanged += (_, _) =>
        {
            PopulateGrdNeckChoices(node.GrdNeckDiameterIn);
            UpdateGrdPerformancePreview();
        };
        _grdNeckDiameter.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdLength.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdSlotWidth.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _grdSlotCount.SelectedIndexChanged += (_, _) => UpdateGrdPerformancePreview();
        _airflow.TextChanged += (_, _) => UpdateGrdPerformancePreview();
        UpdateGrdPerformancePreview();
    }

    private Control BuildGrdPerformancePanel()
    {
        var panel = new TableLayoutPanel
        {
            Name = "LayoutGrdPerformancePanel",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackColor
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        Add("350R Duct Size (in)", _grdTitus350RSize, "titus350RSize", 0);
        Add("Face Size (in)", _grdFaceSize, "face", 1);
        Add("Neck Diameter (in)", _grdNeckDiameter, "neck", 2);
        Add("Length (ft)", _grdLength, "length", 3);
        Add("Slot Size", _grdSlotWidth, "slotWidth", 4);
        Add("Number of Slots", _grdSlotCount, "slotCount", 5);
        Add("Selected Result", _grdPerformanceResult, "result", 6);
        UpdateRows();
        _grdType.SelectedIndexChanged += (_, _) => UpdateRows();
        return panel;

        void Add(string text, Control control, string key, int row)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 6 ? 46 : 30));
            var label = new Label
            {
                Name = $"LayoutGrd_{key}_Label",
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(48, 65, 80),
                Font = new Font("Segoe UI", 8F)
            };
            control.Name = string.IsNullOrWhiteSpace(control.Name) ? $"LayoutGrd_{key}_Input" : control.Name;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(2, 2, 0, 2);
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        void UpdateRows()
        {
            var price = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Diffuser",
                StringComparison.OrdinalIgnoreCase);
            var grille = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Grille",
                StringComparison.OrdinalIgnoreCase);
            var titus = string.Equals(Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture), "Linear Diffuser TBD-80 D",
                StringComparison.OrdinalIgnoreCase);
            SetRow(0, grille);
            SetRow(1, price);
            SetRow(2, price);
            SetRow(3, titus);
            SetRow(4, titus);
            SetRow(5, titus);
            SetRow(6, grille || price || titus);
        }

        void SetRow(int row, bool visible)
        {
            panel.RowStyles[row].Height = visible ? (row == 6 ? 46 : 30) : 0;
            foreach (Control control in panel.Controls)
                if (panel.GetRow(control) == row) control.Visible = visible;
        }
    }

    /// <summary>The published-selection inputs as separate inspector rows, each shown for its GRD type only.</summary>
    private void AddGrdInlineRows()
    {
        string Type() => Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture) ?? string.Empty;
        bool Is(string type) => string.Equals(Type(), type, StringComparison.OrdinalIgnoreCase);
        _inlineRows.AddRange(new[]
        {
            new InlineEditRow("350R duct size (in)", _grdTitus350RSize, () => Is("Grille")),
            new InlineEditRow("Face size (in)", _grdFaceSize, () => Is("Diffuser")),
            new InlineEditRow("Neck diameter (in)", _grdNeckDiameter, () => Is("Diffuser")),
            new InlineEditRow("Length (ft)", _grdLength, () => Is("Linear Diffuser TBD-80 D")),
            new InlineEditRow("Slot size", _grdSlotWidth, () => Is("Linear Diffuser TBD-80 D")),
            new InlineEditRow("Number of slots", _grdSlotCount, () => Is("Linear Diffuser TBD-80 D")),
            new InlineEditRow("Selected result", _grdPerformanceResult, IsPublishedGrd)
        });
        _grdType.SelectedIndexChanged += (_, _) => InlineRowsChanged?.Invoke();
    }

    public IReadOnlyList<InlineEditRow> InlineRows => _inlineRows;

    public event Action? InlineRowsChanged;

    private readonly Label _inlineStatus = new() { Name = "LayoutEquipmentInlineStatus", ForeColor = Color.Firebrick };

    public Control? InlineStatus => _inlineStatus;

    public void UseInline(IWin32Window messageOwner) => _messageOwner = messageOwner;

    /// <summary>A refused input: a message box in the window, a red line under the inputs in the inspector.</summary>
    private void ShowInputProblem(string message, string caption)
    {
        if (_messageOwner is not null)
        {
            _inlineStatus.Text = message;
            return;
        }
        MessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Runs the Save button's checks. The checks write to the component as they go, so a failed check puts every
    /// value back as it was: an edit that is not accepted leaves the component unchanged.
    /// </summary>
    public bool TryAcceptInline()
    {
        _inlineStatus.Text = string.Empty;
        var before = System.Text.Json.JsonSerializer.Serialize(_node);
        if (TrySaveInputs())
            return true;
        var original = System.Text.Json.JsonSerializer.Deserialize<SavedLayoutNode>(before);
        if (original is not null)
        {
            foreach (var property in typeof(SavedLayoutNode).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                    property.SetValue(_node, property.GetValue(original));
            }
        }
        return false;
    }

    private bool IsPublishedGrd()
    {
        var type = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture);
        return string.Equals(type, "Grille", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "Diffuser", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "Linear Diffuser TBD-80 D", StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateGrdNeckChoices(double preferred)
    {
        if (_grdFaceSize.SelectedItem is not int face)
            return;
        var values = GrdPerformanceCatalog.PriceNeckDiameters(face);
        _grdNeckDiameter.BeginUpdate();
        _grdNeckDiameter.Items.Clear();
        _grdNeckDiameter.Items.AddRange(values.Cast<object>().ToArray());
        _grdNeckDiameter.EndUpdate();
        _grdNeckDiameter.SelectedItem = values.OrderBy(value => Math.Abs(value - preferred)).First();
    }

    private void UpdateGrdPerformancePreview()
    {
        if (_node.Type != LayoutNodeType.Grd)
            return;
        var published = IsPublishedGrd();
        _pressureDrop.ReadOnly = published;
        _pressureDrop.BackColor = published ? Color.FromArgb(235, 241, 246) : Color.White;
        if (!published)
        {
            _grdPerformanceResult.Text = string.Empty;
            return;
        }
        if (!double.TryParse(_airflow.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm) || cfm <= 0)
        {
            _grdPerformanceResult.Text = "Enter a positive airflow.";
            return;
        }
        try
        {
            var type = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture);
            var selection = string.Equals(type, "Grille", StringComparison.OrdinalIgnoreCase)
                ? GrdPerformanceCatalog.SelectTitus350R(cfm, Convert.ToString(_grdTitus350RSize.SelectedItem,
                    CultureInfo.CurrentCulture) ?? "12x12")
                : string.Equals(type, "Diffuser", StringComparison.OrdinalIgnoreCase)
                    ? GrdPerformanceCatalog.SelectPriceScd(cfm, (int)_grdFaceSize.SelectedItem!, (int)_grdNeckDiameter.SelectedItem!)
                    : GrdPerformanceCatalog.SelectTitusTbd80(cfm, (int)_grdLength.SelectedItem!,
                        ((LayoutValueChoice<double>)_grdSlotWidth.SelectedItem!).Value,
                        Convert.ToInt32(((LayoutValueChoice<double>)_grdSlotCount.SelectedItem!).Value));
            _pressureDrop.Text = selection.Selected.TotalPressureInWg.ToString("0.###", CultureInfo.CurrentCulture);
            _grdPerformanceResult.Text = string.IsNullOrWhiteSpace(selection.NominalDuctSizeIn)
                ? $"{selection.Selected.AirflowCfm:0} cfm | {selection.InletDiameterIn:0} in inlet | NC {selection.Selected.SoundNc} | Throw {selection.Selected.ThrowFt} ft"
                : $"{selection.Selected.AirflowCfm:0} cfm | {selection.Selected.CoreVelocityFpm:0} fpm | SP -{selection.Selected.TotalPressureInWg:0.###} | NC {selection.Selected.SoundNc}";
        }
        catch (ArgumentException ex)
        {
            _grdPerformanceResult.Text = ex.Message;
        }
    }

    private void AddRow(TableLayoutPanel table, int row, string label, Control input, int rowHeight = 40)
    {
        // Row 0 is the heading; the GRD performance panel is offered row by row below (see AddGrdInlineRows).
        if (row > 0 && input is not TableLayoutPanel { Name: "LayoutGrdPerformancePanel" })
            _inlineRows.Add(new InlineEditRow(InlineText.SentenceCase(label), input));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 0 ? 48 : rowHeight));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(48, 65, 80),
            Font = new Font("Segoe UI Semibold", 9F)
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4, 5, 0, 5);
        table.Controls.Add(input, 1, row);
    }

    private void SaveInputs()
    {
        if (!TrySaveInputs())
            return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TrySaveInputs()
    {
        var temperatureF = 70.0;
        var sourceSpace = _node.Type == LayoutNodeType.Reservoir
                          && (string.Equals(_node.SpaceRole, "Space Source", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(_node.SpaceRole, "Return Pickup", StringComparison.OrdinalIgnoreCase));
        var sinkSpace = _node.Type == LayoutNodeType.Reservoir && !sourceSpace;
        if (_node.Type is not (LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent)
            && !sinkSpace
            && (!double.TryParse(_temperature.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out temperatureF)
                || temperatureF < -100 || temperatureF > 300))
        {
            ShowInputProblem("Enter a temperature between -100°F and 300°F.", "Invalid Temperature");
            return false;
        }
        if (_node.Type is not (LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint or LayoutNodeType.CustomComponent) && !sinkSpace)
            _node.TemperatureC = UnitConversions.FahrenheitToCelsius(temperatureF);
        if (_node.Type is LayoutNodeType.Fan or LayoutNodeType.Grd || sourceSpace || sinkSpace)
        {
            if (!double.TryParse(_airflow.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cfm) || cfm <= 0)
            {
                ShowInputProblem("Enter a positive airflow in cfm.", "Invalid Airflow");
                return false;
            }
            _node.AirflowM3PerS = UnitConversions.CfmToM3PerS(cfm);
            _node.AirflowUserSpecified = true;
        }
        if (_node.Type == LayoutNodeType.Fan)
        {
            if (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                    out var fanStaticPressure) || !double.IsFinite(fanStaticPressure) || fanStaticPressure < 0)
            {
                ShowInputProblem(_node.IsAirHandlingUnit
                    ? "Enter a nonnegative external static pressure in in. w.c."
                    : "Enter a nonnegative fan system static pressure in in. w.c.", "Invalid Fan Static Pressure");
                return false;
            }
            var internalStatic = 0.0;
            if (_node.IsAirHandlingUnit
                && (!double.TryParse(_ahuInternalStatic.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                        out internalStatic) || !double.IsFinite(internalStatic) || internalStatic < 0))
            {
                ShowInputProblem("Enter a nonnegative internal static pressure in in. w.c. (the drop across the unit's filters, coils and other internal components, from the manufacturer's data).",
                    "Invalid Internal Static Pressure");
                return false;
            }
            _node.FanStaticPressurePa = UnitConversions.InchesWaterToPa(fanStaticPressure);
            if (!double.TryParse(_fanEfficiency.Text, NumberStyles.Float, CultureInfo.CurrentCulture,
                    out var fanEfficiency) || !double.IsFinite(fanEfficiency)
                || fanEfficiency <= 0.0 || fanEfficiency > 100.0)
            {
                ShowInputProblem("Enter a fan efficiency greater than 0% and no more than 100%.", "Invalid Fan Efficiency");
                return false;
            }
            _node.FanEfficiencyPercent = fanEfficiency;
            if (_node.IsAirHandlingUnit)
                _node.AhuInternalStaticPressurePa = UnitConversions.InchesWaterToPa(internalStatic);
        }
        else
        {
            var drop = 0.0;
            if (sourceSpace)
            {
                if (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var sourcePressure)
                    || !double.IsFinite(sourcePressure))
                {
                    ShowInputProblem("Enter a valid source pressure in in. w.g.", "Invalid Source Pressure");
                    return false;
                }
                _node.PressurePa = UnitConversions.InchesWaterToPa(sourcePressure);
            }
            else if (_node.Type != LayoutNodeType.Reservoir && _node.Type != LayoutNodeType.CalculationPoint
                && (!double.TryParse(_pressureDrop.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out drop) || drop < 0))
            {
                ShowInputProblem("Enter a nonnegative pressure drop in in. w.g.", "Invalid Pressure Drop");
                return false;
            }
            if (!sourceSpace && _node.Type != LayoutNodeType.CalculationPoint)
                _node.PressureDropPa = UnitConversions.InchesWaterToPa(drop);
        }
        if (!_absoluteLocation)
        {
            if (!TryReadDelta(_deltaX, "ΔX", out var deltaX)
                || !TryReadDelta(_deltaY, "ΔY", out var deltaY)
                || !TryReadDelta(_deltaZ, "ΔZ", out var deltaZ))
                return false;
            _node.DeltaXM = UnitConversions.FeetToM(deltaX);
            _node.DeltaYM = UnitConversions.FeetToM(deltaY);
            _node.DeltaZM = UnitConversions.FeetToM(deltaZ);
        }
        else
        {
            var kind = _reference is null ? "absolute" : "offset";
            if (!TryReadDelta(_deltaX, $"{kind} X", out var absoluteX)
                || !TryReadDelta(_deltaY, $"{kind} Y", out var absoluteY)
                || !TryReadDelta(_deltaZ, $"{kind} Z", out var absoluteZ))
                return false;
            _desiredAbsoluteCoordinate = LayoutPositionReference.FromDisplay(new LayoutCoordinate(
                UnitConversions.FeetToM(absoluteX), UnitConversions.FeetToM(absoluteY),
                UnitConversions.FeetToM(absoluteZ)), _reference);
        }
        if (_node.Type == LayoutNodeType.Grd)
        {
            _node.GrdType = Convert.ToString(_grdType.SelectedItem, CultureInfo.CurrentCulture) ?? "Grille";
            _node.GrdNominalDuctSizeIn = Convert.ToString(_grdTitus350RSize.SelectedItem,
                CultureInfo.CurrentCulture) ?? "12x12";
            _node.GrdFaceSizeIn = _grdFaceSize.SelectedItem is int face ? face : 24;
            _node.GrdNeckDiameterIn = _grdNeckDiameter.SelectedItem is int neck ? neck : 8;
            _node.GrdLengthFt = _grdLength.SelectedItem is int length ? length : 2;
            _node.GrdSlotWidthIn = _grdSlotWidth.SelectedItem is LayoutValueChoice<double> slotWidth ? slotWidth.Value : 0.75;
            _node.GrdSlotCount = _grdSlotCount.SelectedItem is LayoutValueChoice<double> slotCount
                ? Convert.ToInt32(slotCount.Value) : 1;
            if (IsPublishedGrd())
            {
                var cfm = UnitConversions.M3PerSToCfm(_node.AirflowM3PerS);
                try
                {
                    var selection = string.Equals(_node.GrdType, "Grille", StringComparison.OrdinalIgnoreCase)
                        ? GrdPerformanceCatalog.SelectTitus350R(cfm, _node.GrdNominalDuctSizeIn)
                        : string.Equals(_node.GrdType, "Diffuser", StringComparison.OrdinalIgnoreCase)
                            ? GrdPerformanceCatalog.SelectPriceScd(cfm, (int)_node.GrdFaceSizeIn, (int)_node.GrdNeckDiameterIn)
                            : GrdPerformanceCatalog.SelectTitusTbd80(cfm, (int)_node.GrdLengthFt,
                                _node.GrdSlotWidthIn, _node.GrdSlotCount);
                    _node.PressureDropPa = UnitConversions.InchesWaterToPa(selection.Selected.TotalPressureInWg);
                    _node.GrdInletDiameterIn = selection.InletDiameterIn ?? 6;
                    _node.GrdSelectedAirflowCfm = selection.Selected.AirflowCfm;
                    _node.GrdSoundNc = selection.Selected.SoundNc;
                    _node.GrdThrowFt = selection.Selected.ThrowFt;
                }
                catch (ArgumentException ex)
                {
                    ShowInputProblem(ex.Message, "GRD Performance Selection");
                    return false;
                }
            }
            else
            {
                _node.GrdSelectedAirflowCfm = 0;
                _node.GrdSoundNc = string.Empty;
                _node.GrdThrowFt = string.Empty;
            }
        }
        if (_node.Type == LayoutNodeType.CustomComponent)
        {
            _node.Description = _componentDescription.Text.Trim();
            if (string.IsNullOrWhiteSpace(_node.Description))
            {
                ShowInputProblem("Enter a component description.", "Missing Description");
                return false;
            }
        }
        if (_node.Type == LayoutNodeType.Reservoir)
            _node.SpaceRole = Convert.ToString(_spaceRole.SelectedItem, CultureInfo.CurrentCulture) ?? "Space Sink";
        _node.AirSystemType = SelectedAirSystemType;
        return true;
    }

    private bool TryReadDelta(TextBox input, string name, out double value)
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value) && Math.Abs(value) <= 100_000)
            return true;
        ShowInputProblem($"Enter a finite {name} distance between -100,000 ft and 100,000 ft.", "Invalid Coordinate Delta");
        return false;
    }

    internal static void PopulateRelationshipControls(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes,
        Func<SavedLayoutNode, string> captionSource, ComboBox upstream, Label downstream)
    {
        upstream.DropDownStyle = ComboBoxStyle.DropDownList;
        upstream.Items.Add(new LayoutNodeChoice(string.Empty, "(No upstream item)"));
        var descendants = DescendantIds(node, nodes);
        foreach (var candidate in nodes.Where(x => x.Id != node.Id && !descendants.Contains(x.Id)).OrderBy(x => x.Sequence))
        {
            upstream.Items.Add(new LayoutNodeChoice(candidate.Id, candidate.Id));
        }
        upstream.SelectedItem = upstream.Items.Cast<LayoutNodeChoice>().FirstOrDefault(x =>
            string.Equals(x.Id, node.ParentNodeId, StringComparison.OrdinalIgnoreCase)) ?? upstream.Items[0];

        var children = nodes.Where(x => x.ParentNodeId == node.Id || x.SecondaryParentNodeId == node.Id)
            .OrderBy(x => x.Sequence).ToArray();
        downstream.Text = children.Length == 0
            ? "(No downstream items)"
            : string.Join(", ", children.Select(x => x.Id));
        downstream.Dock = DockStyle.Fill;
        downstream.AutoEllipsis = true;
        downstream.TextAlign = ContentAlignment.MiddleLeft;
        downstream.ForeColor = Color.FromArgb(48, 65, 80);
    }

    private static HashSet<string> DescendantIds(SavedLayoutNode node, IReadOnlyList<SavedLayoutNode> nodes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(node.Id);
        while (pending.Count > 0)
        {
            var parentId = pending.Dequeue();
            foreach (var child in nodes.Where(x => x.ParentNodeId == parentId || x.SecondaryParentNodeId == parentId))
            {
                if (result.Add(child.Id))
                    pending.Enqueue(child.Id);
            }
        }
        return result;
    }
}
