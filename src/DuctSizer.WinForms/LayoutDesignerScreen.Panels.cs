using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>An entry of the Project Explorer: a drawing component, a drawing connection, or a record off the drawing.</summary>
internal sealed record ExplorerEntry(string Text, SavedLayoutNode? Node, LayoutConnectionRef? Connection,
    (bool IsFitting, int Sequence)? Record);

/// <summary>A collapsible group of the Project Explorer.</summary>
internal sealed record ExplorerGroup(string Name, string Title, IReadOnlyList<ExplorerEntry> Entries);

/// <summary>A row of the Errors list.</summary>
internal sealed record ProjectIssue(string Severity, string Source, string ItemId, string Message);

/// <summary>
/// Project-tab panels around the drawing (v0.96 stage 3): the bottom panel (System Overview, Errors, Validation)
/// and the data behind the Project Explorer. Everything here is read-only: it lists what the model and the
/// calculations already hold and lets the user find the item on the drawing.
/// </summary>
internal sealed partial class LayoutDesignerScreen
{
    private readonly DataGridView _layoutErrorsGrid = new();
    private readonly DataGridView _layoutValidationGrid = new();
    private readonly Dictionary<string, InspectorStyleTab> _bottomTabs = new();
    private readonly Panel _bottomContent = new() { Name = "LayoutBottomContent", Dock = DockStyle.Fill, BackColor = Color.White };
    private string _activeBottomTab = "Overview";

    /// <summary>The Project Explorer on the left of the Project tab (null until the page is built).</summary>
    internal ProjectExplorer? Explorer => _explorer;

    private Control BuildLayoutBottomPanel()
    {
        var panel = new Panel { Name = "LayoutBottomPanel", Dock = DockStyle.Fill, BackColor = Color.White };
        var strip = new FlowLayoutPanel
        {
            Name = "LayoutBottomTabs",
            Dock = DockStyle.Top,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 3, 4, 0),
            BackColor = Color.FromArgb(236, 241, 246)
        };
        AddBottomTab(strip, "Overview", "LayoutOverviewTab", "System Overview", RibbonIcon.Grid);
        AddBottomTab(strip, "Errors", "LayoutErrorsTab", "Errors (0)", RibbonIcon.Warning);
        AddBottomTab(strip, "Validation", "LayoutValidationTab", "Validation", RibbonIcon.Check);
        var collapse = new Button
        {
            Name = "LayoutSummaryCollapseButton",
            Text = "Hide",
            Size = new Size(54, 26),
            Margin = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(16, 45, 84),
            Font = new Font("Segoe UI", 8.5F),
            AccessibleName = "Hide the bottom panel"
        };
        collapse.FlatAppearance.BorderColor = RibbonPalette.RibbonBorder;
        collapse.Click += (_, _) => SetLayoutSummaryCollapsed(true);
        _layoutToolTips.SetToolTip(collapse, "Hide the System Overview, Errors and Validation panel");
        strip.Controls.Add(collapse);

        BuildLayoutSystemSummaryPanel();
        ConfigureListGrid(_layoutErrorsGrid, "LayoutErrorsGrid", new[]
        {
            ("Severity", "Type", 80), ("Source", "Found by", 110), ("Item", "Item", 90), ("Message", "Description", 600)
        });
        _layoutErrorsGrid.Columns["Message"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        // Navigate only on a user's click or arrow key: a grid selects its first row by itself when filled or shown.
        _layoutErrorsGrid.CellClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
                SelectFromListGrid(_layoutErrorsGrid);
        };
        _layoutErrorsGrid.KeyUp += (_, args) =>
        {
            if (args.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End)
                SelectFromListGrid(_layoutErrorsGrid);
        };
        _layoutErrorsGrid.VisibleChanged += (_, _) => _layoutErrorsGrid.ClearSelection();
        ConfigureListGrid(_layoutValidationGrid, "LayoutValidationGrid", new[]
        {
            ("Item", "Item", 80), ("Kind", "Kind", 110), ("Size", "Size", 110), ("Airflow", "Airflow", 80),
            ("Velocity", "Velocity", 80), ("Rate", "Static pressure rate", 130), ("Coefficient", "Loss coefficient C", 110),
            ("Loss", "Loss", 90), ("Status", "Status", 200)
        });
        _layoutValidationGrid.Columns["Status"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        // Navigate only on a user's click or arrow key: a grid selects its first row by itself when filled or shown.
        _layoutValidationGrid.CellClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
                SelectFromListGrid(_layoutValidationGrid);
        };
        _layoutValidationGrid.KeyUp += (_, args) =>
        {
            if (args.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End)
                SelectFromListGrid(_layoutValidationGrid);
        };
        _layoutValidationGrid.VisibleChanged += (_, _) => _layoutValidationGrid.ClearSelection();

        panel.Controls.Add(_bottomContent);
        panel.Controls.Add(strip);
        ShowBottomTab("Overview");
        return panel;
    }

    private void AddBottomTab(FlowLayoutPanel strip, string key, string name, string text, RibbonIcon icon)
    {
        var tab = new InspectorStyleTab(text, icon) { Name = name, Margin = new Padding(0, 0, 2, 0) };
        tab.Click += (_, _) => ShowBottomTab(key);
        _bottomTabs[key] = tab;
        strip.Controls.Add(tab);
    }

    internal void ShowBottomTab(string key)
    {
        _activeBottomTab = key;
        foreach (var (tabKey, tab) in _bottomTabs)
            tab.Selected = tabKey == key;
        if (_bottomContent.Controls.Count == 0)
            _bottomContent.Controls.AddRange(new Control[] { _layoutSystemSummaryGrid, _layoutErrorsGrid, _layoutValidationGrid });
        _layoutSystemSummaryGrid.Visible = key == "Overview";
        _layoutErrorsGrid.Visible = key == "Errors";
        _layoutValidationGrid.Visible = key == "Validation";
        // Lists are rebuilt when shown as well as after each edit, so they can never be out of date when read.
        if (key == "Errors")
            RefreshErrorsList();
        else if (key == "Validation")
            RefreshValidationList();
    }

    private void ConfigureListGrid(DataGridView grid, string name, IEnumerable<(string Name, string Header, int Width)> columns)
    {
        grid.Name = name;
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowTemplate.Height = 25;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 77, 109);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F);
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 246);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 49, 69);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 252);
        foreach (var (columnName, header, width) in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = columnName,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
    }

    /// <summary>Rebuilds the Errors and Validation lists and the Project Explorer from the current project.</summary>
    internal void RefreshLayoutBottomPanel()
    {
        RefreshErrorsList();
        RefreshValidationList();
        _explorer?.Populate(BuildExplorerGroups());
    }

    /// <summary>
    /// Every problem in the project, from the checks that already exist: the critical-path analysis (items that
    /// block the system total), the layout model check, the layout geometry check, items whose own calculation
    /// fails, and components that are still undefined.
    /// </summary>
    internal IReadOnlyList<ProjectIssue> BuildProjectIssues()
    {
        var document = _host.DuctSystemScreen._ductSystem;
        var issues = new List<ProjectIssue>();
        if (document.LayoutNodes.Count > 0)
        {
            foreach (var issue in CriticalPathAnalyzer.Analyze(document).Issues)
                issues.Add(new ProjectIssue("Error", "System calculation", issue.ItemId, issue.Message));
            foreach (var issue in LayoutModelCheck.Check(document))
                issues.Add(new ProjectIssue("Error", "Layout model", issue.ItemId, issue.Message));
            foreach (var issue in LayoutGeometryCheck.Check(document))
                issues.Add(new ProjectIssue("Warning", "Layout geometry", issue.ItemId, issue.Message));
            foreach (var node in document.LayoutNodes.Where(node => !node.IsDefined && node.Type != LayoutNodeType.Duct))
                issues.Add(new ProjectIssue("Error", "Definition", node.Id,
                    "This component is not defined yet. Double-click it, use Edit Inputs, or run Auto Size."));
        }
        foreach (var duct in document.Ducts)
        {
            try
            {
                duct.Calculate();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
            {
                issues.Add(new ProjectIssue("Error", "Duct calculation", duct.Id, ex.Message));
            }
        }
        foreach (var fitting in document.Fittings)
        {
            try
            {
                fitting.Calculate();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
                                           or KeyNotFoundException)
            {
                issues.Add(new ProjectIssue("Error", "Fitting calculation", fitting.Id, ex.Message));
            }
        }
        return issues
            .GroupBy(issue => (issue.ItemId, issue.Message))
            .Select(group => group.First())
            .OrderBy(issue => issue.Severity == "Error" ? 0 : 1)
            .ThenBy(issue => issue.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RefreshErrorsList()
    {
        if (_layoutErrorsGrid.Columns.Count == 0)
            return;
        var issues = BuildProjectIssues();
        _syncingLayoutSelection = true;
        try
        {
            _layoutErrorsGrid.Rows.Clear();
            foreach (var issue in issues)
            {
                var index = _layoutErrorsGrid.Rows.Add(issue.Severity, issue.Source, issue.ItemId, issue.Message);
                _layoutErrorsGrid.Rows[index].Tag = issue.ItemId;
                if (issue.Severity == "Error")
                    _layoutErrorsGrid.Rows[index].Cells["Severity"].Style.ForeColor = Color.Firebrick;
            }
            _layoutErrorsGrid.ClearSelection();
        }
        finally
        {
            _syncingLayoutSelection = false;
        }
        if (_bottomTabs.TryGetValue("Errors", out var tab))
            tab.Text = $"Errors ({issues.Count})";
    }

    /// <summary>
    /// QA/QC list: every duct with its size, airflow, velocity and static-pressure rate, and every fitting with its
    /// loss coefficient and pressure loss, so a reviewer can scan for values outside the expected range. Values come
    /// straight from each item's own calculation; nothing is judged or changed here.
    /// </summary>
    private void RefreshValidationList()
    {
        if (_layoutValidationGrid.Columns.Count == 0)
            return;
        var document = _host.DuctSystemScreen._ductSystem;
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        string Airflow(double m3PerS) => imperial
            ? $"{UnitConversions.M3PerSToCfm(m3PerS):N0} cfm" : $"{UnitConversions.M3PerSToLps(m3PerS):N0} L/s";
        string Velocity(double mPerS) => imperial
            ? $"{UnitConversions.MpsToFpm(mPerS):N0} fpm" : $"{mPerS:0.00} m/s";
        string Pressure(double pa) => imperial
            ? $"{UnitConversions.PaToInchesWater(pa):0.####} in. w.c." : $"{pa:0.##} Pa";
        // Same formatting as the Duct System schedule (a transitional-flow duct shows its lower–upper bounds).
        string Rate(DuctCalculationResult result) =>
            $"{_host.DuctSystemScreen.FormatSystemPressureRate(result)} {(imperial ? "in. w.c./100 ft" : "Pa/m")}";

        _syncingLayoutSelection = true;
        try
        {
            _layoutValidationGrid.Rows.Clear();
            foreach (var duct in document.Ducts.OrderBy(x => x.Sequence))
            {
                string size, velocity = "-", rate = "-", loss = "-", status;
                size = duct.Shape == DuctShape.Round
                    ? (imperial ? $"{UnitConversions.MToInches(duct.DiameterM):0.##} in. round" : $"{UnitConversions.MToMillimeters(duct.DiameterM):0} mm round")
                    : (imperial
                        ? $"{UnitConversions.MToInches(duct.WidthM):0.##} × {UnitConversions.MToInches(duct.HeightM):0.##} in."
                        : $"{UnitConversions.MToMillimeters(duct.WidthM):0} × {UnitConversions.MToMillimeters(duct.HeightM):0} mm");
                try
                {
                    var result = duct.Calculate();
                    velocity = Velocity(result.ActualVelocityMPerS);
                    rate = Rate(result);
                    loss = Pressure(result.TotalPressureLossUpperPa);
                    status = duct.RequiresResizing ? "Needs re-sizing" : "Calculated";
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
                {
                    status = $"Not calculated: {ex.Message}";
                }
                var index = _layoutValidationGrid.Rows.Add(duct.Id, "Duct", size, Airflow(duct.AirflowM3PerS), velocity,
                    rate, "-", loss, status);
                _layoutValidationGrid.Rows[index].Tag = duct.Id;
            }
            foreach (var fitting in document.Fittings.OrderBy(x => x.Sequence))
            {
                string coefficient = "-", loss = "-", status, kind = "Fitting";
                try
                {
                    var result = fitting.Calculate();
                    kind = result.Definition.Category;
                    coefficient = result.Coefficient.ToString("0.####", CultureInfo.CurrentCulture);
                    loss = Pressure(result.PressureLossPa);
                    status = result.Definition.Description;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
                                               or KeyNotFoundException)
                {
                    status = $"Not calculated: {ex.Message}";
                }
                var index = _layoutValidationGrid.Rows.Add(fitting.Id, kind, fitting.FittingCode,
                    fitting.AirflowM3PerS > 0 ? Airflow(fitting.AirflowM3PerS) : "-", "-", "-", coefficient, loss, status);
                _layoutValidationGrid.Rows[index].Tag = fitting.Id;
            }
            _layoutValidationGrid.ClearSelection();
        }
        finally
        {
            _syncingLayoutSelection = false;
        }
    }

    private void SelectFromListGrid(DataGridView grid)
    {
        if (_syncingLayoutSelection || grid.SelectedRows.Count == 0 || grid.SelectedRows[0].Tag is not string itemId)
            return;
        SelectLayoutItemById(itemId);
    }

    /// <summary>Selects the item with this ID on the drawing (or shows it in the inspector when it is not drawn).</summary>
    internal bool SelectLayoutItemById(string itemId)
    {
        var document = _host.DuctSystemScreen._ductSystem;
        if (document.LayoutNodes.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase)) is { } node)
            return SelectExplorerEntry(new ExplorerEntry(node.Id, node, null, null));
        if (document.Ducts.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase)) is { } duct)
        {
            var connectionNode = document.LayoutNodes.FirstOrDefault(x =>
                x.ConnectionDuctSequence == duct.Sequence || x.SecondaryConnectionDuctSequence == duct.Sequence);
            return SelectExplorerEntry(connectionNode is null
                ? new ExplorerEntry(duct.Id, null, null, (false, duct.Sequence))
                : new ExplorerEntry(duct.Id, null,
                    new LayoutConnectionRef(connectionNode, connectionNode.SecondaryConnectionDuctSequence == duct.Sequence), null));
        }
        if (document.Fittings.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase)) is { } fitting)
        {
            var fittingNode = document.LayoutNodes.FirstOrDefault(x => x.LinkedFittingSequence == fitting.Sequence);
            return SelectExplorerEntry(fittingNode is null
                ? new ExplorerEntry(fitting.Id, null, null, (true, fitting.Sequence))
                : new ExplorerEntry(fitting.Id, fittingNode, null, null));
        }
        return false;
    }

    /// <summary>Selects an explorer entry on the drawing, scrolls it into view and shows it in the inspector.</summary>
    internal bool SelectExplorerEntry(ExplorerEntry entry)
    {
        if (_layoutCanvas is null)
            return false;
        if (entry.Node is SavedLayoutNode node)
        {
            _layoutCanvas.SelectNode(node);
            _layoutCanvas.CenterOnGrid(node.GridX, node.GridY);
            _inspector?.ShowNode(node);
            return true;
        }
        if (entry.Connection is LayoutConnectionRef connection)
        {
            _layoutCanvas.SelectConnection(connection);
            _layoutCanvas.CenterOnGrid(connection.Downstream.GridX, connection.Downstream.GridY);
            _inspector?.ShowConnection(connection);
            return true;
        }
        if (entry.Record is var (isFitting, sequence))
        {
            _layoutCanvas.SelectNode(null);
            _inspector?.ShowRecord(isFitting, sequence);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Explorer groups: every item of the single system in this project, sorted by kind. A project holds one system,
    /// so there is no system or level tree.
    /// </summary>
    internal IReadOnlyList<ExplorerGroup> BuildExplorerGroups()
    {
        var document = _host.DuctSystemScreen._ductSystem;
        var nodes = document.LayoutNodes.OrderBy(x => x.Sequence).ToArray();
        ExplorerEntry NodeEntry(SavedLayoutNode node)
        {
            var description = !string.IsNullOrWhiteSpace(node.Description) ? node.Description
                : node.Type == LayoutNodeType.Fitting && !string.IsNullOrWhiteSpace(node.FittingGroup) ? node.FittingGroup
                : node.IsAirHandlingUnit ? "Air Handling Unit"
                : node.Type.ToString();
            return new ExplorerEntry($"{node.Id}  {description}{(node.IsDefined ? string.Empty : "  (undefined)")}", node, null, null);
        }
        bool IsGroup(SavedLayoutNode node, string group) => node.Type == LayoutNodeType.Fitting
            && string.Equals(node.FittingGroup, group, StringComparison.OrdinalIgnoreCase);

        var equipment = nodes.Where(node => node.Type is LayoutNodeType.Fan or LayoutNodeType.Coil or LayoutNodeType.Filter
                                                or LayoutNodeType.CustomComponent
                                            || IsGroup(node, FittingCatalog.VavSelectionGroup));
        var sources = nodes.Where(node => node.Type == LayoutNodeType.Reservoir && IsSpaceSourceRole(node.SpaceRole));
        var sinks = nodes.Where(node => node.Type == LayoutNodeType.Reservoir && !IsSpaceSourceRole(node.SpaceRole));
        var terminals = nodes.Where(node => node.Type == LayoutNodeType.Grd
                                            || node.Type == LayoutNodeType.Fitting
                                            && FittingCatalog.IsTerminalSelectionGroup(node.FittingGroup));
        var dampers = nodes.Where(node => IsGroup(node, "Dampers"));
        var fittings = nodes.Where(node => node.Type == LayoutNodeType.Fitting
                                           && !IsGroup(node, FittingCatalog.VavSelectionGroup)
                                           && !IsGroup(node, "Dampers")
                                           && !FittingCatalog.IsTerminalSelectionGroup(node.FittingGroup));
        var points = nodes.Where(node => node.Type == LayoutNodeType.CalculationPoint);

        var segments = new List<ExplorerEntry>();
        foreach (var node in nodes)
        {
            foreach (var secondary in new[] { false, true })
            {
                var parentId = secondary ? node.SecondaryParentNodeId : node.ParentNodeId;
                if (string.IsNullOrWhiteSpace(parentId))
                    continue;
                var connection = new LayoutConnectionRef(node, secondary);
                var duct = DuctForConnection(node, secondary);
                var size = duct is null ? "unsized" : LayoutConnectionSizeText(duct);
                segments.Add(new ExplorerEntry($"{duct?.Id ?? "Unsized"}  {parentId} → {node.Id}  ({size})", null, connection, null));
            }
        }
        var drawnDucts = nodes.SelectMany(node => new[] { node.ConnectionDuctSequence, node.SecondaryConnectionDuctSequence, node.LinkedDuctSequence })
            .OfType<int>().ToHashSet();
        segments.AddRange(document.Ducts.Where(duct => !drawnDucts.Contains(duct.Sequence)).OrderBy(duct => duct.Sequence)
            .Select(duct => new ExplorerEntry($"{duct.Id}  {duct.Description}  (not on drawing)", null, null, (false, duct.Sequence))));
        var drawnFittings = nodes.Select(node => node.LinkedFittingSequence).OfType<int>().ToHashSet();
        var offDrawingFittings = document.Fittings.Where(fitting => !drawnFittings.Contains(fitting.Sequence))
            .OrderBy(fitting => fitting.Sequence)
            .Select(fitting => new ExplorerEntry($"{fitting.Id}  {fitting.Description}  (not on drawing)", null, null, (true, fitting.Sequence)));

        return new[]
        {
            new ExplorerGroup("Equipment", "Equipment", equipment.Select(NodeEntry).ToArray()),
            new ExplorerGroup("Sources", "Sources", sources.Select(NodeEntry).ToArray()),
            new ExplorerGroup("Sinks", "Sinks", sinks.Select(NodeEntry).ToArray()),
            new ExplorerGroup("Terminals", "Terminals", terminals.Select(NodeEntry).ToArray()),
            new ExplorerGroup("Dampers", "Dampers", dampers.Select(NodeEntry).ToArray()),
            new ExplorerGroup("Fittings", "Fittings", fittings.Select(NodeEntry).Concat(offDrawingFittings).ToArray()),
            new ExplorerGroup("DuctSegments", "Duct Segments", segments),
            new ExplorerGroup("CalculationPoints", "Calculation Points", points.Select(NodeEntry).ToArray())
        };
    }

    private static string LayoutConnectionSizeText(SavedDuct duct) => duct.Shape == DuctShape.Round
        ? duct.DiameterM > 0 ? $"{UnitConversions.MToInches(duct.DiameterM):0.##} in." : "no size"
        : duct.WidthM > 0 && duct.HeightM > 0
            ? $"{UnitConversions.MToInches(duct.WidthM):0.##} × {UnitConversions.MToInches(duct.HeightM):0.##} in."
            : "no size";
}

/// <summary>A tab of the bottom panel: icon and caption, raised white when selected.</summary>
internal sealed class InspectorStyleTab : Button
{
    private bool _selected;

    internal InspectorStyleTab(string text, RibbonIcon icon)
    {
        Text = text;
        IconKind = icon;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 9F);
        Size = new Size(TextRenderer.MeasureText("System Overview (000)", Font).Width + 34, 29);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PageTab;
    }

    internal RibbonIcon IconKind { get; }

    internal bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            AccessibleDescription = value ? "Selected" : null;
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? Color.White);
        if (_selected)
        {
            using var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, Width - 1, Height + 6), 5);
            using var fill = new SolidBrush(Color.White);
            graphics.FillPath(fill, path);
            using var border = new Pen(RibbonPalette.RibbonBorder);
            graphics.DrawPath(border, path);
        }
        RibbonIcons.Draw(graphics, IconKind, new Rectangle(9, (Height - 16) / 2, 16, 16),
            _selected ? RibbonPalette.Icon : RibbonPalette.Caption, RibbonPalette.IconAccent);
        using var font = new Font("Segoe UI", 9F, _selected ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(graphics, Text, font, new Rectangle(30, 0, Width - 32, Height),
            _selected ? RibbonPalette.Ink : RibbonPalette.Caption,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(3, 3, Width - 6, Height - 6));
    }
}
