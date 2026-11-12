using System.Globalization;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>A row of the Analysis tab's "highest pressure drops" list.</summary>
/// <param name="Range">The item checked against the expected ranges (Analysis Settings).</param>
internal sealed record PressureDropEntry(string Id, string Kind, double LossPa, bool OnCriticalPath, RangeCheckResult Range);

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
    private readonly PressureProfileChart _analysisProfileChart = new();
    private readonly Label _analysisProfileSubtitle = new() { Name = "AnalysisProfileSubtitle" };
    private readonly Label _analysisProfileComparison = new() { Name = "AnalysisProfileComparison" };

    private static readonly RangeCheckResult NoRange = new(RangeCheckStatus.NotApplicable, Array.Empty<RangeFinding>());

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
        _analysisTopLossGrid.ColumnHeadersHeight = 34;
        _analysisTopLossGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
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
                     ("Critical", "Critical path", 90), ("Range", "Within expected range", 110)
                 })
            _analysisTopLossGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable
            });
        _analysisTopLossGrid.Columns["Kind"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _analysisTopLossGrid.Columns["Range"]!.Visible = false;
        _analysisTopLossGrid.ShowCellToolTips = true;
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
                _criticalPathItemKeys.Contains(DuctKey(duct.Sequence)),
                AnalysisRangeCheck.CheckDuct(_ductSystem.AnalysisExpectations, duct)));
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
                _criticalPathItemKeys.Contains(FittingKey(fitting.Sequence)),
                AnalysisRangeCheck.CheckFitting(_ductSystem.AnalysisExpectations, fitting)));
        }
        foreach (var node in _ductSystem.LayoutNodes.Where(node => node.IsDefined
                     && node.Type is LayoutNodeType.Coil or LayoutNodeType.Filter or LayoutNodeType.Grd
                         or LayoutNodeType.CustomComponent))
            entries.Add(new PressureDropEntry(node.Id, node.Type == LayoutNodeType.Grd ? "GRD"
                    : node.Type == LayoutNodeType.CustomComponent ? "Custom component" : node.Type.ToString(),
                node.PressureDropPa, _criticalPathItemKeys.Contains(LayoutKey(node.Sequence)), NoRange));
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
        _analysisTopLossGrid.Columns["Range"]!.Visible = _ductSystem.AnalysisExpectations.AnySet;
        var rank = 1;
        foreach (var entry in HighestPressureDrops())
        {
            var index = _analysisTopLossGrid.Rows.Add(rank++, entry.Id, entry.Kind, FormatSystemTotal(entry.LossPa),
                entry.OnCriticalPath ? "Yes" : string.Empty, RangeText(entry.Range.Status));
            var row = _analysisTopLossGrid.Rows[index];
            if (entry.OnCriticalPath)
                row.Cells["Critical"].Style.ForeColor = Color.FromArgb(174, 104, 0);
            var rangeCell = row.Cells["Range"];
            rangeCell.ToolTipText = RangeToolTip(entry.Range);
            rangeCell.Style.ForeColor = entry.Range.Status switch
            {
                RangeCheckStatus.Within => Color.FromArgb(30, 110, 60),
                RangeCheckStatus.Outside => Color.Firebrick,
                _ => Color.FromArgb(110, 124, 136)
            };
            if (entry.Range.Status == RangeCheckStatus.Outside)
                rangeCell.Style.Font = new Font("Segoe UI Semibold", 8.5F);
        }
        _analysisTopLossGrid.ClearSelection();
        RefreshPressureProfile();
    }

    private static string RangeText(RangeCheckStatus status) => status switch
    {
        RangeCheckStatus.Within => "Yes",
        RangeCheckStatus.Outside => "No",
        RangeCheckStatus.NotSet => "Not set",
        RangeCheckStatus.NotAvailable => "Not available",
        _ => "—"
    };

    /// <summary>Each comparison made, in project units, e.g. "Velocity 1,250 fpm: expected 600 to 1,000 fpm (outside)".</summary>
    internal string RangeToolTip(RangeCheckResult result)
    {
        if (result.Status == RangeCheckStatus.NotApplicable)
            return "No expected range applies to this kind of item.";
        if (result.Status == RangeCheckStatus.NotSet)
            return "No expected range is entered for this kind of item (Analysis Settings).";
        var lines = result.Findings.Select(finding =>
        {
            var (name, unit, format, convert) = QuantityDisplay(finding.Quantity);
            var value = finding.UpperValue is double upper
                ? $"{convert(finding.Value).ToString(format, CultureInfo.CurrentCulture)} to {convert(upper).ToString(format, CultureInfo.CurrentCulture)} (transitional flow)"
                : convert(finding.Value).ToString(format, CultureInfo.CurrentCulture);
            var expected = finding.Range switch
            {
                { Low: double low, High: double high } => $"{convert(low).ToString(format, CultureInfo.CurrentCulture)} to {convert(high).ToString(format, CultureInfo.CurrentCulture)}",
                { Low: double low } => $"at least {convert(low).ToString(format, CultureInfo.CurrentCulture)}",
                { High: double high } => $"at most {convert(high).ToString(format, CultureInfo.CurrentCulture)}",
                _ => "not set"
            };
            return $"{name} {value} {unit}: expected {expected} {unit} ({(finding.Within ? "within" : "outside")})";
        }).ToList();
        if (result.Problem is not null)
            lines.Add("Not checked: " + result.Problem);
        return string.Join(Environment.NewLine, lines);
    }

    private (string Name, string Unit, string Format, Func<double, double> Convert) QuantityDisplay(ExpectedQuantity quantity)
    {
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        return quantity switch
        {
            ExpectedQuantity.DuctPressureRate => ("Pressure drop rate", imperial ? "in. w.c./100 ft" : "Pa/m", "0.####",
                imperial ? UnitConversions.PaPerMToInchesWaterPer100Feet : value => value),
            ExpectedQuantity.DuctVelocity => ("Velocity", imperial ? "fpm" : "m/s", imperial ? "N0" : "0.##",
                imperial ? UnitConversions.MpsToFpm : value => value),
            ExpectedQuantity.FittingVelocity => ("Velocity", imperial ? "fpm" : "m/s", imperial ? "N0" : "0.##",
                imperial ? UnitConversions.MpsToFpm : value => value),
            _ => ("Loss", imperial ? "in. w.c." : "Pa", imperial ? "0.####" : "0.##",
                imperial ? UnitConversions.PaToInchesWater : value => value)
        };
    }

    /// <summary>Opens Analysis Settings (expected ranges) and applies a save.</summary>
    internal void EditAnalysisSettings()
    {
        using var dialog = new AnalysisSettingsDialog(_ductSystem.AnalysisExpectations, _host._currentUnits);
        if (dialog.ShowDialog(_host) != DialogResult.OK)
            return;
        _ductSystem.AnalysisExpectations = dialog.Expectations;
        SaveDuctSystem();
        RefreshAnalysisSummary();
    }

    /// <summary>
    /// Running pressure loss along every route, in project units: complete routes only, with a note naming what was
    /// left out. The critical path is marked only when every route is complete (otherwise it cannot be known).
    /// </summary>
    internal DuctSystemPdfPressureProfile BuildPressureProfile()
    {
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        var unit = imperial ? "in. w.c." : "Pa";
        var result = SystemPathProfiles.Build(_ductSystem);
        var allComplete = result.CompletePathCount == result.Paths.Count && !result.Truncated;
        var series = result.Paths.Where(path => path.IsComplete)
            .Select(path => new DuctSystemPdfProfileSeries($"{path.StartId} → {path.EndId}",
                path.Steps.Select(step => imperial ? UnitConversions.PaToInchesWater(step.CumulativeLossPa) : step.CumulativeLossPa).ToArray(),
                allComplete && path.IsCritical,
                path.Steps.Select(step => step.ItemId).ToArray()))
            .ToArray();
        var notes = new List<string>();
        var incomplete = result.Paths.Count - result.CompletePathCount;
        if (incomplete > 0)
            notes.Add($"{incomplete} path{(incomplete == 1 ? " is" : "s are")} not shown: an item on {(incomplete == 1 ? "it" : "them")} has no calculated loss (see the Errors list).");
        if (result.Truncated)
            notes.Add($"Only the first {SystemPathProfiles.MaximumPaths} paths are shown.");
        if (!allComplete && series.Length > 0)
            notes.Add("The critical path is not marked until every path can be calculated.");
        // How close the next-highest path comes to the critical path (paths of similar loss are better balanced).
        var comparison = string.Empty;
        var criticalPath = allComplete ? result.Paths.FirstOrDefault(path => path.IsCritical) : null;
        var nextPath = criticalPath is null ? null : result.Paths.Where(path => !path.IsCritical)
            .OrderByDescending(path => path.TotalLossPa).FirstOrDefault();
        if (criticalPath is not null && nextPath is not null)
        {
            var format = imperial ? "0.###" : "0.#";
            double Display(double pa) => imperial ? UnitConversions.PaToInchesWater(pa) : pa;
            var gap = criticalPath.TotalLossPa - nextPath.TotalLossPa;
            var percent = criticalPath.TotalLossPa > 0 ? gap / criticalPath.TotalLossPa * 100 : 0;
            comparison = $"Closest to critical: {nextPath.StartId} → {nextPath.EndId}, "
                         + $"{Display(nextPath.TotalLossPa).ToString(format, CultureInfo.CurrentCulture)} {unit} "
                         + $"({Display(gap).ToString(format, CultureInfo.CurrentCulture)} {unit} or {percent.ToString("0", CultureInfo.CurrentCulture)} % below "
                         + $"{Display(criticalPath.TotalLossPa).ToString(format, CultureInfo.CurrentCulture)} {unit})";
        }
        return new DuctSystemPdfPressureProfile(unit, series, result.Paths.Count, string.Join(" ", notes), comparison);
    }

    private void RefreshPressureProfile()
    {
        var profile = BuildPressureProfile();
        _analysisProfileChart.Profile = profile;
        _analysisProfileChart.EmptyMessage = profile.PathCount == 0
            ? "Draw and connect the system in the Designer to see the pressure drop along each path."
            : "No path can be shown yet. " + profile.Note;
        var summary = profile.PathCount == 1 ? "1 path" : $"{profile.PathCount} paths";
        _analysisProfileSubtitle.Text = $"Running pressure drop from the start of the system to each terminal outlet  ·  {summary}"
                                        + (string.IsNullOrEmpty(profile.Note) ? string.Empty : "  ·  " + profile.Note);
        _analysisProfileComparison.Text = profile.Comparison;
        _analysisProfileComparison.Visible = profile.Comparison.Length > 0;
        _analysisProfileSubtitle.ForeColor = string.IsNullOrEmpty(profile.Note)
            ? Color.FromArgb(74, 91, 106) : Color.FromArgb(160, 90, 0);
    }

    /// <summary>The Analysis tab's pressure-profile card (below the overview and the top-10 list).</summary>
    private Control BuildPressureProfileCard()
    {
        var card = new RoundedPanel
        {
            Name = "AnalysisProfileCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 10,
            BorderColor = UiPalette.CardBorder,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 10, 0, 0)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.White };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "Pressure drop along each path",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(22, 50, 78),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _analysisProfileSubtitle.Dock = DockStyle.Fill;
        _analysisProfileSubtitle.TextAlign = ContentAlignment.MiddleLeft;
        _analysisProfileSubtitle.AutoEllipsis = true;
        _analysisProfileSubtitle.Font = new Font("Segoe UI", 8.5F);
        layout.Controls.Add(_analysisProfileSubtitle, 0, 1);
        // The path closest to the critical one: paths of similar loss are better balanced.
        _analysisProfileComparison.AutoSize = true;
        _analysisProfileComparison.Margin = new Padding(0, 2, 0, 0);
        _analysisProfileComparison.Font = new Font("Segoe UI Semibold", 8.5F);
        _analysisProfileComparison.ForeColor = Color.FromArgb(22, 65, 102);
        _analysisProfileComparison.Visible = false;
        layout.Controls.Add(_analysisProfileComparison, 0, 2);
        _analysisProfileChart.Dock = DockStyle.Fill;
        _analysisProfileChart.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_analysisProfileChart, 0, 3);
        card.Controls.Add(layout);
        return card;
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
