using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>A row of the Analysis tab's "highest pressure drops" list.</summary>
internal sealed record PressureDropEntry(string Id, string Kind, double LossPa, bool OnCriticalPath);

/// <summary>
/// Analysis tab summary (v0.96 stage 4): the system's airflow, the critical-path pressure drop and the items on the
/// critical route, then the ten components with the highest pressure drop, above the full system table. All values
/// are read from the existing calculations (critical-path analysis and each item's own loss); nothing is computed
/// differently here.
/// </summary>
internal sealed partial class DuctSystemScreen
{
    private readonly Label _analysisAirflowValue = new() { Name = "AnalysisAirflowValue" };
    private readonly Label _analysisPressureValue = new() { Name = "AnalysisPressureValue" };
    private readonly Label _analysisFanStaticValue = new() { Name = "AnalysisFanStaticValue" };
    private readonly Label _analysisRouteValue = new() { Name = "AnalysisRouteValue" };
    private readonly DataGridView _analysisTopLossGrid = new();

    private Control BuildAnalysisSummary(Control totalsRow)
    {
        var host = new TableLayoutPanel
        {
            Name = "AnalysisSummary",
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = _host.BackColor
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var overview = new RoundedPanel
        {
            Name = "AnalysisOverviewCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 10,
            BorderColor = UiPalette.CardBorder,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 0, 6, 0)
        };
        var overviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        overviewLayout.Controls.Add(SectionTitle("System overview"), 0, 0);
        overviewLayout.SetColumnSpan(overviewLayout.GetControlFromPosition(0, 0)!, 2);
        overviewLayout.Controls.Add(Tile("Airflow", _analysisAirflowValue), 0, 1);
        overviewLayout.Controls.Add(Tile("Critical-path pressure drop", _analysisPressureValue), 1, 1);
        overviewLayout.Controls.Add(Tile("Fan static pressure", _analysisFanStaticValue), 0, 2);
        overviewLayout.Controls.Add(Tile("Items in the critical route", _analysisRouteValue), 1, 2);
        totalsRow.Dock = DockStyle.Fill;
        totalsRow.Margin = new Padding(0, 4, 0, 0);
        overviewLayout.Controls.Add(totalsRow, 0, 3);
        overviewLayout.SetColumnSpan(totalsRow, 2);
        overview.Controls.Add(overviewLayout);

        var topCard = new RoundedPanel
        {
            Name = "AnalysisTopLossCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 10,
            BorderColor = UiPalette.CardBorder,
            Padding = new Padding(14, 8, 14, 10),
            Margin = new Padding(6, 0, 0, 0)
        };
        var topLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        topLayout.Controls.Add(SectionTitle("Highest pressure drops (top 10)"), 0, 0);
        _analysisTopLossGrid.Name = "AnalysisTopLossGrid";
        _analysisTopLossGrid.Dock = DockStyle.Fill;
        _analysisTopLossGrid.ReadOnly = true;
        _analysisTopLossGrid.AllowUserToAddRows = false;
        _analysisTopLossGrid.AllowUserToDeleteRows = false;
        _analysisTopLossGrid.AllowUserToResizeRows = false;
        _analysisTopLossGrid.RowHeadersVisible = false;
        _analysisTopLossGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _analysisTopLossGrid.MultiSelect = false;
        _analysisTopLossGrid.RowTemplate.Height = 22;
        _analysisTopLossGrid.ColumnHeadersHeight = 26;
        _analysisTopLossGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _analysisTopLossGrid.BackgroundColor = Color.White;
        _analysisTopLossGrid.BorderStyle = BorderStyle.None;
        _analysisTopLossGrid.EnableHeadersVisualStyles = false;
        _analysisTopLossGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 77, 109);
        _analysisTopLossGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _analysisTopLossGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F);
        _analysisTopLossGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
        _analysisTopLossGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 246);
        _analysisTopLossGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 49, 69);
        foreach (var (name, header, width) in new[]
                 {
                     ("Rank", "#", 34), ("Item", "Item", 90), ("Kind", "Kind", 110), ("Loss", "Pressure drop", 120),
                     ("Critical", "Critical path", 90)
                 })
            _analysisTopLossGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _analysisTopLossGrid.Columns["Kind"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _analysisTopLossGrid.CellClick += (_, args) =>
        {
            if (args.RowIndex >= 0 && _analysisTopLossGrid.Rows[args.RowIndex].Cells["Item"].Value is string id)
                SelectSystemGridRow(id);
        };
        topLayout.Controls.Add(_analysisTopLossGrid, 0, 1);
        topCard.Controls.Add(topLayout);

        host.Controls.Add(overview, 0, 0);
        host.Controls.Add(topCard, 1, 0);
        return host;

        static Label SectionTitle(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(22, 50, 78),
            TextAlign = ContentAlignment.MiddleLeft
        };

        static Control Tile(string caption, Label value)
        {
            var tile = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 8, 3),
                Padding = new Padding(10, 6, 8, 4),
                BackColor = Color.FromArgb(244, 248, 251)
            };
            value.Dock = DockStyle.Fill;
            value.Font = new Font("Segoe UI Semibold", 13F);
            value.ForeColor = Color.FromArgb(22, 65, 102);
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.AutoEllipsis = true;
            value.Text = "—";
            tile.Controls.Add(value);
            tile.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(74, 91, 106)
            });
            return tile;
        }
    }

    /// <summary>
    /// The components with the highest pressure drop: ducts (friction, transitional upper bound), fittings (their
    /// saved flow path) and coils, filters, GRDs and custom components (entered or selected drop). Items whose loss
    /// cannot be calculated are left out here; they are listed on the Project tab's Errors list.
    /// </summary>
    internal IReadOnlyList<PressureDropEntry> HighestPressureDrops(int count = 10)
    {
        var entries = new List<PressureDropEntry>();
        foreach (var duct in _ductSystem.Ducts)
            entries.Add(new PressureDropEntry(duct.Id, "Duct", SystemSolver.DuctLoss(_ductSystem, duct.Sequence),
                _criticalPathItemKeys.Contains(DuctKey(duct.Sequence))));
        foreach (var fitting in _ductSystem.Fittings)
        {
            var kind = "Fitting";
            try
            {
                kind = fitting.Calculate().Definition.Category;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
                                           or KeyNotFoundException)
            {
                // Its loss below is NaN, so it is left out of the list.
            }
            entries.Add(new PressureDropEntry(fitting.Id, kind, SystemSolver.FittingLoss(_ductSystem, fitting.Sequence),
                _criticalPathItemKeys.Contains(FittingKey(fitting.Sequence))));
        }
        foreach (var node in _ductSystem.LayoutNodes.Where(node => node.IsDefined
                     && node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                         or LayoutNodeType.CustomComponent))
            entries.Add(new PressureDropEntry(node.Id, node.Type == LayoutNodeType.Grd ? "GRD"
                    : node.Type == LayoutNodeType.CustomComponent ? "Custom component" : node.Type.ToString(),
                node.PressureDropPa, _criticalPathItemKeys.Contains(LayoutKey(node.Sequence))));
        return entries
            .Where(entry => double.IsFinite(entry.LossPa) && entry.LossPa > 0)
            .OrderByDescending(entry => entry.LossPa)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToArray();
    }

    private void RefreshAnalysisSummary()
    {
        if (_analysisTopLossGrid.Columns.Count == 0)
            return;
        var fans = _ductSystem.LayoutNodes.Where(node => node.Type == LayoutNodeType.Fan).ToArray();
        var fan = fans.Length == 1 ? fans[0] : null;
        double requiredAirflow;
        try
        {
            requiredAirflow = fan is null ? 0.0 : FanSizing.RequiredAirflowM3PerS(_ductSystem, fan);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            requiredAirflow = 0.0;
        }
        _analysisAirflowValue.Text = fan is null
            ? fans.Length > 1 ? "More than one fan" : "No fan"
            : requiredAirflow > 0 ? PdfAirflow(requiredAirflow) : PdfAirflow(fan.AirflowM3PerS);
        _analysisPressureValue.Text = !_criticalPathCalculated
            ? "Not calculated"
            : _criticalPathIssues.Count > 0 ? "Not available" : FormatSystemTotal(_criticalPathPressureLossPa);
        _analysisPressureValue.ForeColor = _criticalPathCalculated && _criticalPathIssues.Count > 0
            ? Color.Firebrick : Color.FromArgb(22, 65, 102);
        _analysisFanStaticValue.Text = fan is { IsDefined: true } ? FormatSystemTotal(fan.FanStaticPressurePa) : "—";
        _analysisRouteValue.Text = !_criticalPathCalculated || _criticalPathItemKeys.Count == 0
            ? "—"
            : $"{_criticalPathItemKeys.Count}  ({_criticalPathRoute})";

        _analysisTopLossGrid.Rows.Clear();
        var rank = 1;
        foreach (var entry in HighestPressureDrops())
        {
            var index = _analysisTopLossGrid.Rows.Add(rank++, entry.Id, entry.Kind, FormatSystemTotal(entry.LossPa),
                entry.OnCriticalPath ? "Yes" : string.Empty);
            if (entry.OnCriticalPath)
                _analysisTopLossGrid.Rows[index].Cells["Critical"].Style.ForeColor = Color.FromArgb(174, 104, 0);
        }
        _analysisTopLossGrid.ClearSelection();
    }

    /// <summary>Selects the item's row in the full system table and scrolls it into view.</summary>
    private void SelectSystemGridRow(string itemId)
    {
        foreach (DataGridViewRow row in _ductGrid.Rows)
        {
            if (!string.Equals(Convert.ToString(row.Cells["Id"].Value), itemId, StringComparison.OrdinalIgnoreCase))
                continue;
            _ductGrid.ClearSelection();
            row.Selected = true;
            if (row.Visible && row.Cells["Id"].Visible)
            {
                _ductGrid.CurrentCell = row.Cells["Id"];
                _ductGrid.FirstDisplayedScrollingRowIndex = row.Index;
            }
            return;
        }
    }
}
