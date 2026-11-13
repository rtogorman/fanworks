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
    // Static pressure (round V): the same six values for a fan or an air handling unit.
    private readonly Label _analysisTotalStaticValue = new() { Name = "AnalysisTotalStaticValue" };
    private readonly Label _analysisInternalStaticValue = new() { Name = "AnalysisInternalStaticValue" };
    private readonly Label _analysisExternalStaticValue = new() { Name = "AnalysisExternalStaticValue" };
    private readonly Label _analysisNegativeStaticValue = new() { Name = "AnalysisNegativeStaticValue" };
    private readonly Label _analysisPositiveStaticValue = new() { Name = "AnalysisPositiveStaticValue" };
    private readonly Label _analysisFanSetValue = new() { Name = "AnalysisFanSetValue" };
    private readonly Label _analysisFanSetCaption = new() { Name = "AnalysisFanSetCaption" };
    private readonly Label _analysisFanSetNote = new() { Name = "AnalysisFanSetNote" };
    private readonly Label _analysisStaticHeading = new() { Name = "AnalysisStaticHeading" };
    private readonly Label _analysisRouteValue = new() { Name = "AnalysisRouteValue" };
    private readonly Label _analysisPressureClassValue = new() { Name = "AnalysisPressureClassValue" };
    private readonly Label _analysisPressureClassNote = new() { Name = "AnalysisPressureClassNote" };
    private readonly Label _analysisSealClassValue = new() { Name = "AnalysisSealClassValue" };
    private readonly Label _analysisLeakageClassValue = new() { Name = "AnalysisLeakageClassValue" };
    private readonly Label _analysisFactorValue = new() { Name = "AnalysisLeakageFactorValue" };
    private readonly Label _analysisLeakageValue = new() { Name = "AnalysisLeakageValue" };
    // The negative (suction) side's column: ductwork upstream of the fan, classed separately (round W).
    private readonly Label _analysisNegativeClassValue = new() { Name = "AnalysisNegativePressureClassValue" };
    private readonly Label _analysisNegativeSealValue = new() { Name = "AnalysisNegativeSealClassValue" };
    private readonly Label _analysisNegativeLeakageClassValue = new() { Name = "AnalysisNegativeLeakageClassValue" };
    private readonly Label _analysisNegativeFactorValue = new() { Name = "AnalysisNegativeLeakageFactorValue" };
    private readonly Label _analysisNegativeLeakageValue = new() { Name = "AnalysisNegativeLeakageValue" };
    private readonly Label _analysisLeakageDetail = new() { Name = "AnalysisLeakageDetail" };
    private readonly SystemSummaryDonut _analysisDonut = new();
    private readonly DataGridView _analysisTopLossGrid = new();
    private readonly PressureProfileChart _analysisProfileChart = new();
    private readonly Label _analysisProfileSubtitle = new() { Name = "AnalysisProfileSubtitle" };
    private readonly Label _analysisProfileComparison = new() { Name = "AnalysisProfileComparison" };
    private readonly ComboBox _analysisPathSelector = new() { Name = "AnalysisProfilePathSelector", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TableLayoutPanel _analysisPathPanel = new() { Name = "AnalysisPathPanel" };
    private readonly Label _analysisPathName = new() { Name = "AnalysisPathName" };
    private readonly Label _analysisPathTotal = new() { Name = "AnalysisPathTotal" };
    private readonly Label _analysisPathBelow = new() { Name = "AnalysisPathBelowCritical" };
    private readonly Label _analysisPathRoute = new() { Name = "AnalysisPathRoute" };
    private readonly Label _analysisPathPick = new() { Name = "AnalysisPathPick" };
    private readonly CheckBox _analysisPressureSidesCheck = new() { Name = "AnalysisPressureSidesCheck" };
    /// <summary>With all paths shown: every path's total, scrollable for large systems; a row selects that path.</summary>
    private readonly FlowLayoutPanel _analysisAllPathsPanel = new() { Name = "AnalysisAllPathsPanel" };
    private bool _updatingPathSelector;

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
        overviewLayout.RowCount = 8;
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154)); // static pressure: heading + 2 rows of 3
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184)); // SMACNA classes, both sides of the fan
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        overviewLayout.Controls.Add(SectionTitle("System overview"), 0, 0);
        overviewLayout.SetColumnSpan(overviewLayout.GetControlFromPosition(0, 0)!, 2);
        overviewLayout.Controls.Add(Tile("Airflow", _analysisAirflowValue), 0, 1);
        overviewLayout.Controls.Add(Tile("Items on Path 1 (critical path)", _analysisRouteValue), 1, 1);
        // Static pressure, the same for a fan or an air handling unit: total = internal + external, and the external
        // static pressure split at the fan into its negative (suction) and positive (discharge) sides.
        var staticGrid = new TableLayoutPanel
        {
            Name = "AnalysisStaticPressureGrid", Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3,
            Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.White
        };
        for (var column = 0; column < 3; column++)
            staticGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
        staticGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        staticGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        staticGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        _analysisStaticHeading.Dock = DockStyle.Fill;
        _analysisStaticHeading.Font = new Font("Segoe UI Semibold", 8.75F);
        _analysisStaticHeading.ForeColor = Color.FromArgb(14, 111, 230);
        _analysisStaticHeading.TextAlign = ContentAlignment.BottomLeft;
        _analysisStaticHeading.AutoEllipsis = true;
        _analysisStaticHeading.Text = "Static pressure";
        staticGrid.Controls.Add(_analysisStaticHeading, 0, 0);
        staticGrid.SetColumnSpan(_analysisStaticHeading, 3);
        staticGrid.Controls.Add(Tile("Total static (TSP)", _analysisTotalStaticValue, valueSize: 12F), 0, 1);
        staticGrid.Controls.Add(Tile("Internal static (ISP)", _analysisInternalStaticValue, valueSize: 12F), 1, 1);
        staticGrid.Controls.Add(Tile("External static (ESP)", _analysisExternalStaticValue, valueSize: 12F), 2, 1);
        staticGrid.Controls.Add(Tile("Negative ESP (suction)", _analysisNegativeStaticValue, valueSize: 12F), 0, 2);
        staticGrid.Controls.Add(Tile("Positive ESP (discharge)", _analysisPositiveStaticValue, valueSize: 12F), 1, 2);
        staticGrid.Controls.Add(Tile("Fan set to (ESP)", _analysisFanSetValue, _analysisFanSetNote, _analysisFanSetCaption, valueSize: 12F), 2, 2);
        _analysisFanSetNote.Height = 16;
        overviewLayout.Controls.Add(staticGrid, 0, 2);
        overviewLayout.SetColumnSpan(staticGrid, 2);
        // SMACNA duct construction and leakage (pressure class, seal class, allowable leakage).
        // SMACNA duct construction, one column per side of the fan: the negative (suction) side upstream of it and the
        // positive (discharge) side downstream are classed separately, each for its own pressure.
        var classGrid = new TableLayoutPanel
        {
            Name = "AnalysisDuctClassGrid", Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 8,
            Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.White
        };
        classGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        classGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        classGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        classGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        classGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        for (var row = 0; row < 5; row++)
            classGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        classGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        classGrid.Controls.Add(new Label
        {
            Text = "Duct construction and leakage (SMACNA)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI Semibold", 8.75F), ForeColor = Color.FromArgb(14, 111, 230), AutoEllipsis = true
        }, 0, 0);
        classGrid.SetColumnSpan(classGrid.GetControlFromPosition(0, 0)!, 3);
        Label Header(string text) => new()
        {
            Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = Color.White, BackColor = Color.FromArgb(11, 31, 59),
            Margin = new Padding(0, 2, 1, 1), Padding = new Padding(6, 0, 0, 0)
        };
        classGrid.Controls.Add(Header(string.Empty), 0, 1);
        classGrid.Controls.Add(Header("Negative (suction)"), 1, 1);
        classGrid.Controls.Add(Header("Positive (discharge)"), 2, 1);
        var classRows = new (string Caption, Label Negative, Label Positive)[]
        {
            ("Pressure class", _analysisNegativeClassValue, _analysisPressureClassValue),
            ("Seal class", _analysisNegativeSealValue, _analysisSealClassValue),
            ("Leakage class", _analysisNegativeLeakageClassValue, _analysisLeakageClassValue),
            ("Leakage factor F", _analysisNegativeFactorValue, _analysisFactorValue),
            ("Duct leakage", _analysisNegativeLeakageValue, _analysisLeakageValue)
        };
        for (var row = 0; row < classRows.Length; row++)
        {
            var shade = row % 2 == 0 ? Color.FromArgb(244, 248, 251) : Color.White;
            classGrid.Controls.Add(new Label
            {
                Text = classRows[row].Caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(74, 91, 106), BackColor = shade,
                Margin = new Padding(0, 0, 1, 0), Padding = new Padding(6, 0, 0, 0)
            }, 0, row + 2);
            foreach (var (value, column) in new[] { (classRows[row].Negative, 1), (classRows[row].Positive, 2) })
            {
                value.Dock = DockStyle.Fill;
                value.TextAlign = ContentAlignment.MiddleLeft;
                value.AutoEllipsis = true;
                value.Font = new Font("Segoe UI Semibold", 9.5F);
                value.ForeColor = Color.FromArgb(11, 31, 59);
                value.BackColor = shade;
                value.Margin = new Padding(0, 0, 1, 0);
                value.Padding = new Padding(6, 0, 0, 0);
                classGrid.Controls.Add(value, column, row + 2);
            }
        }
        _analysisPressureClassNote.Dock = DockStyle.Fill;
        _analysisPressureClassNote.TextAlign = ContentAlignment.MiddleLeft;
        _analysisPressureClassNote.AutoEllipsis = true;
        _analysisPressureClassNote.Font = new Font("Segoe UI", 8F);
        _analysisPressureClassNote.ForeColor = Color.FromArgb(74, 91, 106);
        classGrid.Controls.Add(_analysisPressureClassNote, 0, 7);
        classGrid.SetColumnSpan(_analysisPressureClassNote, 3);
        overviewLayout.Controls.Add(classGrid, 0, 3);
        overviewLayout.SetColumnSpan(classGrid, 2);
        _analysisLeakageDetail.Dock = DockStyle.Fill;
        _analysisLeakageDetail.Margin = new Padding(0, 4, 8, 0);
        _analysisLeakageDetail.Font = new Font("Segoe UI", 8.25F);
        _analysisLeakageDetail.ForeColor = Color.FromArgb(62, 78, 94);
        _analysisLeakageDetail.TextAlign = ContentAlignment.TopLeft;
        overviewLayout.Controls.Add(_analysisLeakageDetail, 0, 5);
        overviewLayout.SetColumnSpan(_analysisLeakageDetail, 2);
        totalsRow.Dock = DockStyle.Fill;
        totalsRow.Margin = new Padding(0, 4, 0, 0);
        overviewLayout.Controls.Add(totalsRow, 0, 6);
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
        topLayout.Controls.Add(SectionTitle("Critical path pressure drops"), 0, 0);
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
                     ("Range", "Within expected range", 110)
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
            {
                SelectSystemGridRow(id);
                HighlightOnPathGraph(id);
            }
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
            ForeColor = Color.FromArgb(11, 31, 59),
            TextAlign = ContentAlignment.MiddleLeft
        };

        static Control Tile(string caption, Label value, Label? note = null, Label? captionLabel = null, float valueSize = 13F)
        {
            var tile = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 8, 3),
                Padding = new Padding(10, 6, 8, 4),
                BackColor = Color.FromArgb(244, 248, 251)
            };
            value.Dock = DockStyle.Fill;
            value.Font = new Font("Segoe UI Semibold", valueSize);
            value.ForeColor = Color.FromArgb(11, 31, 59);
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.AutoEllipsis = true;
            value.Text = "—";
            tile.Controls.Add(value);
            if (note is not null)
            {
                // The criteria or basis, under the value (for example "Per SMACNA HVAC Duct Construction Standards").
                note.Dock = DockStyle.Bottom;
                note.Height = 28;
                note.Font = new Font("Segoe UI", 7.75F);
                note.ForeColor = Color.FromArgb(74, 91, 106);
                note.AutoEllipsis = true;
                tile.Controls.Add(note);
            }
            captionLabel ??= new Label();
            captionLabel.Text = caption;
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = 18;
            captionLabel.Font = new Font("Segoe UI", 8.5F);
            captionLabel.ForeColor = Color.FromArgb(74, 91, 106);
            captionLabel.AutoEllipsis = true;
            tile.Controls.Add(captionLabel);
            return tile;
        }
    }

    /// <summary>
    /// The components with the highest pressure drop: ducts (friction, transitional upper bound), fittings (their
    /// saved flow path) and coils, filters, GRDs and custom components (entered or selected drop). Items whose loss
    /// cannot be calculated are left out here; they are listed on the Project tab's Errors list.
    /// </summary>
    /// <summary>
    /// Every item on the critical path with its pressure drop on that path (a junction fitting at the critical
    /// path's flow path), highest first. The drops add up to the critical-path total. Empty when no complete critical
    /// path exists.
    /// </summary>
    /// <summary>
    /// The static pressure block, the same for a fan or an air handling unit:
    /// ESP = the critical-path loss (calculated), split at the fan into its negative (suction) and positive
    /// (discharge) sides; ISP = the entered internal static pressure; TSP = ESP + ISP. The fan's set ESP (what
    /// Auto-size wrote; what the motor power and the solver use) is shown beside them, flagged when it differs.
    /// </summary>
    private void RefreshStaticPressureTiles(SavedLayoutNode? fan)
    {
        var ink = Color.FromArgb(11, 31, 59);
        var unit = fan?.IsAirHandlingUnit == true ? "AHU" : "fan";
        _analysisStaticHeading.Text = fan is null
            ? "Static pressure"
            : $"Static pressure — {fan.Id} ({(fan.IsAirHandlingUnit ? "air handling unit" : "fan")}), along Path 1 (critical path)";
        _analysisFanSetCaption.Text = fan is null ? "Fan set to (ESP)" : $"{fan.Id} set to (ESP)";
        _analysisInternalStaticValue.Text = fan is null ? "—" : FormatSystemTotal(fan.FanInternalStaticPressurePa);
        _analysisFanSetValue.Text = fan is { IsDefined: true } ? FormatSystemTotal(fan.FanStaticPressurePa) : "—";

        SystemPathProfile? critical = null;
        if (_criticalPathCalculated && _criticalPathIssues.Count == 0 && fan is not null)
        {
            var profiles = SystemPathProfiles.Build(_ductSystem);
            critical = profiles.Paths.FirstOrDefault(path => path.IsCritical && path.IsComplete);
        }
        var notReady = !_criticalPathCalculated ? "Not calculated" : "Not available";
        foreach (var label in new[] { _analysisTotalStaticValue, _analysisExternalStaticValue, _analysisNegativeStaticValue, _analysisPositiveStaticValue })
            label.ForeColor = _criticalPathCalculated && _criticalPathIssues.Count > 0 ? Color.Firebrick : ink;
        if (critical is null || fan is null)
        {
            _analysisExternalStaticValue.Text = fan is null ? "No fan" : notReady;
            _analysisTotalStaticValue.Text = fan is null ? "No fan" : notReady;
            _analysisNegativeStaticValue.Text = fan is null ? "—" : notReady;
            _analysisPositiveStaticValue.Text = fan is null ? "—" : notReady;
            _analysisFanSetNote.Text = string.Empty;
            return;
        }
        var external = critical.TotalLossPa;
        _analysisExternalStaticValue.Text = FormatSystemTotal(external);
        _analysisTotalStaticValue.Text = FormatSystemTotal(external + fan.FanInternalStaticPressurePa);
        if (critical.SuctionLossPa is double suction && critical.DischargeLossPa is double discharge)
        {
            // Static pressure is below atmospheric on the suction side of the fan and above it on the discharge side.
            _analysisNegativeStaticValue.Text = suction > 0 ? $"−{FormatSystemTotal(suction)}" : FormatSystemTotal(0);
            _analysisPositiveStaticValue.Text = $"+{FormatSystemTotal(discharge)}";
        }
        else
        {
            _analysisNegativeStaticValue.Text = "Fan not on Path 1";
            _analysisPositiveStaticValue.Text = "Fan not on Path 1";
        }
        var differs = Math.Abs(fan.FanStaticPressurePa - external) > Math.Max(0.5, 0.005 * external);
        _analysisFanSetNote.Text = differs ? $"Differs from the ESP above: run Auto-size to update the {unit}." : "Matches the ESP above.";
        _analysisFanSetNote.ForeColor = differs ? Color.FromArgb(176, 92, 0) : Color.FromArgb(30, 110, 60);
    }

    internal IReadOnlyList<PressureDropEntry> CriticalPathDrops()
    {
        var profiles = SystemPathProfiles.Build(_ductSystem);
        if (profiles.Truncated || profiles.CompletePathCount != profiles.Paths.Count
            || profiles.Paths.FirstOrDefault(path => path.IsCritical) is not { } critical)
            return Array.Empty<PressureDropEntry>();
        var ducts = _ductSystem.Ducts.ToDictionary(duct => DuctKey(duct.Sequence), StringComparer.Ordinal);
        var fittings = _ductSystem.Fittings.ToDictionary(fitting => FittingKey(fitting.Sequence), StringComparer.Ordinal);
        return critical.Steps
            .Select((step, order) =>
            {
                var kind = step.Kind;
                var range = NoRange;
                if (ducts.TryGetValue(step.ItemKey, out var duct))
                    range = AnalysisRangeCheck.CheckDuct(_ductSystem.AnalysisExpectations, duct);
                else if (fittings.TryGetValue(step.ItemKey, out var fitting))
                {
                    range = AnalysisRangeCheck.CheckFitting(_ductSystem.AnalysisExpectations, fitting,
                        SystemPressureForFittingPair(fitting.Sequence).Inlet);
                    try
                    {
                        kind = fitting.Calculate().Definition.Category;
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException
                                                   or KeyNotFoundException)
                    {
                        kind = "Fitting";
                    }
                }
                return (Entry: new PressureDropEntry(step.ItemId, kind, step.LossPa, true, range), Order: order);
            })
            .OrderByDescending(item => item.Entry.LossPa)
            .ThenBy(item => item.Order)
            .Select(item => item.Entry)
            .ToArray();
    }

    /// <summary>Row shading for the critical-path list: strongest for the largest drop, fading to white.</summary>
    private static Color DropShade(double lossPa, double largestPa)
    {
        var share = largestPa > 0 ? Math.Clamp(lossPa / largestPa, 0, 1) : 0;
        static int Blend(int strong, double t) => (int)Math.Round(255 + (strong - 255) * t);
        return Color.FromArgb(Blend(250, share), Blend(214, share), Blend(170, share));
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
        RefreshStaticPressureTiles(fan);
        _analysisRouteValue.Text = !_criticalPathCalculated || _criticalPathItemKeys.Count == 0
            ? "—"
            : $"{_criticalPathItemKeys.Count}  ·  {_criticalPathRoute}";

        _analysisTopLossGrid.Rows.Clear();
        _analysisTopLossGrid.Columns["Range"]!.Visible = _ductSystem.AnalysisExpectations.AnySet;
        var rank = 1;
        var drops = CriticalPathDrops();
        var largest = drops.Count == 0 ? 0.0 : drops[0].LossPa;
        foreach (var entry in drops)
        {
            var index = _analysisTopLossGrid.Rows.Add(rank++, entry.Id, entry.Kind, FormatSystemTotal(entry.LossPa),
                RangeText(entry.Range.Status));
            var row = _analysisTopLossGrid.Rows[index];
            row.DefaultCellStyle.BackColor = DropShade(entry.LossPa, largest);
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
        RefreshLeakageSummary();
        _analysisDonut.SetCounts(SystemItemCounts());
    }

    /// <summary>Duct segments, fittings and components, counted as the report counts its item sheets.</summary>
    internal (int Ducts, int Fittings, int Components) SystemItemCounts() => (
        _ductSystem.Ducts.Count,
        _ductSystem.Fittings.Count,
        _ductSystem.LayoutNodes.Count(x => x.Type is LayoutNodeType.Reservoir or LayoutNodeType.Fan or LayoutNodeType.Coil
                                           or LayoutNodeType.Filter or LayoutNodeType.Grd or LayoutNodeType.CalculationPoint
                                           or LayoutNodeType.CustomComponent
                                       || x.Type == LayoutNodeType.Fitting && !x.IsDefined && x.LinkedFittingSequence is null));

    private void RefreshLeakageSummary()
    {
        var (negative, positive) = BuildLeakageSides();
        var ink = Color.FromArgb(11, 31, 59);
        void Fill(DuctSystemPdfLeakageSummary? side, Label pressureClass, Label seal, Label leakageClass, Label factor, Label leakage)
        {
            if (side is null)
            {
                // No ductwork upstream of the fan: nothing is under negative pressure.
                pressureClass.Text = "No ductwork upstream of the fan";
                foreach (var label in new[] { seal, leakageClass, factor, leakage })
                    label.Text = "—";
                foreach (var label in new[] { pressureClass, seal, leakageClass, factor, leakage })
                    label.ForeColor = Color.FromArgb(120, 134, 148);
                return;
            }
            pressureClass.Text = side.PressureClass + (side.PressureBasis.StartsWith("Pressure class set", StringComparison.Ordinal) ? " (override)" : string.Empty);
            seal.Text = side.SealClass + (side.SealDetail.Contains("set in Customize Duct Class", StringComparison.Ordinal) ? " (override)" : string.Empty);
            leakageClass.Text = side.LeakageClass;
            factor.Text = side.Factor;
            leakage.Text = side.Leakage;
            foreach (var label in new[] { pressureClass, seal, leakageClass, factor })
                label.ForeColor = ink;
            leakage.ForeColor = side.Available ? ink : Color.Firebrick;
        }
        Fill(negative, _analysisNegativeClassValue, _analysisNegativeSealValue, _analysisNegativeLeakageClassValue,
            _analysisNegativeFactorValue, _analysisNegativeLeakageValue);
        Fill(positive, _analysisPressureClassValue, _analysisSealClassValue, _analysisLeakageClassValue,
            _analysisFactorValue, _analysisLeakageValue);
        _analysisPressureClassNote.Text = "Per SMACNA HVAC Duct Construction Standards and Air Duct Leakage Test Manual; "
                                          + "F = CL × P^0.65. Override either side with Customize Duct Class.";
        var detail = new List<string>();
        foreach (var (name, side) in new[] { ("Negative (suction)", negative), ("Positive (discharge)", positive) })
        {
            if (side is null)
                continue;
            var prefix = negative is null ? string.Empty : name + ": ";
            if (side.PressureBasis.Length > 0)
                detail.Add(prefix + side.PressureBasis + ". " + side.SealDetail + ".");
            detail.AddRange(side.CalculationLines.Select(line => prefix + line));
            if (!string.IsNullOrEmpty(side.LeakageDetail))
                detail.Add(prefix + "duct leakage is " + side.LeakageDetail + ".");
            if (!string.IsNullOrEmpty(side.Note))
                detail.Add(prefix + "not available: " + side.Note);
        }
        if (negative is { Available: true } && positive.Available)
        {
            var sides = SmacnaLeakage.AnalyzeSides(_ductSystem);
            detail.Add($"Total duct leakage, both sides: {sides.TotalLeakageCfm.ToString("0.#", CultureInfo.CurrentCulture)} cfm.");
        }
        detail.Add(positive.Criteria);
        _analysisLeakageDetail.Text = string.Join(Environment.NewLine, detail);
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
            ExpectedQuantity.VavInletPressure => ("VAV inlet static pressure", imperial ? "in. w.c." : "Pa", imperial ? "0.####" : "0.##",
                imperial ? UnitConversions.PaToInchesWater : value => value),
            ExpectedQuantity.VavPressureDrop => ("Pressure drop across VAV", imperial ? "in. w.c." : "Pa", imperial ? "0.####" : "0.##",
                imperial ? UnitConversions.PaToInchesWater : value => value),
            _ => ("Loss", imperial ? "in. w.c." : "Pa", imperial ? "0.####" : "0.##",
                imperial ? UnitConversions.PaToInchesWater : value => value)
        };
    }

    /// <summary>Customize Duct Class: shows the calculated classes and saves any overrides, then recalculates.</summary>
    internal void EditDuctClass()
    {
        var expectations = _ductSystem.AnalysisExpectations;
        var withoutOverrides = expectations.Copy();
        withoutOverrides.PressureClassOverrideInWc = null;
        withoutOverrides.SealClassOverride = null;
        withoutOverrides.SuctionPressureClassOverrideInWc = null;
        withoutOverrides.SuctionSealClassOverride = null;
        // The dialog shows what FanWorks calculates on its own, so analyse without the saved overrides.
        _ductSystem.AnalysisExpectations = withoutOverrides;
        SystemLeakageSides calculated;
        try
        {
            calculated = SmacnaLeakage.AnalyzeSides(_ductSystem);
        }
        finally
        {
            _ductSystem.AnalysisExpectations = expectations;
        }
        using var dialog = new DuctClassDialog(calculated, expectations, _host._currentUnits);
        if (dialog.ShowDialog(_host) != DialogResult.OK)
            return;
        expectations.PressureClassOverrideInWc = dialog.PressureClassOverride;
        expectations.SealClassOverride = dialog.SealClassOverride;
        expectations.SuctionPressureClassOverrideInWc = dialog.SuctionPressureClassOverride;
        expectations.SuctionSealClassOverride = dialog.SuctionSealClassOverride;
        SaveDuctSystem();
        RefreshAnalysisSummary();
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
        // In path-number order: Path 1 (the critical path), then 2, 3 … by decreasing total.
        var series = result.Paths.Where(path => path.IsComplete).OrderBy(path => path.Number)
            .Select(path => new DuctSystemPdfProfileSeries(path.Name,
                path.Steps.Select(step => imperial ? UnitConversions.PaToInchesWater(step.CumulativeLossPa) : step.CumulativeLossPa).ToArray(),
                allComplete && path.IsCritical,
                path.Steps.Select(step => step.ItemId).ToArray(),
                path.Route,
                path.FanStepIndex))
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
            comparison = $"Closest to the critical path: {nextPath.Name} ({nextPath.Route}), "
                         + $"{Display(nextPath.TotalLossPa).ToString(format, CultureInfo.CurrentCulture)} {unit} "
                         + $"({Display(gap).ToString(format, CultureInfo.CurrentCulture)} {unit} or {percent.ToString("0", CultureInfo.CurrentCulture)} % below "
                         + $"{Display(criticalPath.TotalLossPa).ToString(format, CultureInfo.CurrentCulture)} {unit})";
        }
        return new DuctSystemPdfPressureProfile(unit, series, result.Paths.Count, string.Join(" ", notes), comparison);
    }

    /// <summary>
    /// The system's SMACNA pressure class, seal class, leakage class and allowable leakage, worded for the Analysis
    /// tab and the report title page. The leakage formula is defined in inch-pound units (cfm per 100 ft², in. w.c.),
    /// so the worked lines are in those units; headline values follow the project units.
    /// </summary>
    /// <summary>
    /// The SMACNA summary for each side of the fan: negative (suction; null when no ductwork is upstream of the fan)
    /// and positive (discharge; the whole system when nothing is upstream).
    /// </summary>
    internal (DuctSystemPdfLeakageSummary? Negative, DuctSystemPdfLeakageSummary Positive) BuildLeakageSides()
    {
        var sides = SmacnaLeakage.AnalyzeSides(_ductSystem);
        return (sides.Negative is null ? null : BuildLeakageSummary(sides.Negative), BuildLeakageSummary(sides.Positive));
    }

    internal DuctSystemPdfLeakageSummary BuildLeakageSummary(SystemLeakageResult? computed = null)
    {
        var result = computed ?? SmacnaLeakage.AnalyzeSides(_ductSystem).Positive;
        var imperial = _host._currentUnits == UnitSystem.Imperial;
        const string criteria = "Per SMACNA HVAC Duct Construction Standards - Metal and Flexible (pressure and seal class) and "
                                + "SMACNA HVAC Air Duct Leakage Test Manual (leakage class CL); leakage factor F = CL x P^0.65.";
        string Pressure(double inWc) => imperial
            ? $"{inWc.ToString("0.###", CultureInfo.CurrentCulture)} in. w.c."
            : $"{UnitConversions.InchesWaterToPa(inWc).ToString("0", CultureInfo.CurrentCulture)} Pa";
        if (result.PressureClassInWc is not double pressureClass || result.SealClass is not SmacnaSealClass seal)
            return new DuctSystemPdfLeakageSummary(false, "Not available", string.Empty, "—", string.Empty, "—", "—",
                "Not available", string.Empty, Array.Empty<string>(), criteria, result.Problem ?? "Not available.");

        // SMACNA's metric classes: 125, 250, 500, 750, 1000, 1500 and 2500 Pa.
        var classText = imperial
            ? $"{pressureClass.ToString("0.#", CultureInfo.CurrentCulture)} in. w.c."
            : $"{(pressureClass switch { 0.5 => 125, 1 => 250, 2 => 500, 3 => 750, 4 => 1000, 6 => 1500, _ => 2500 })} Pa ({pressureClass:0.#} in. w.c.)";
        var basis = result.PressureClassOverridden
            ? "Pressure class set in Customize Duct Class"
              + (result.CalculatedPressureClassInWc is double calculatedClass
                  ? $" (calculated: {calculatedClass.ToString("0.#", CultureInfo.CurrentCulture)} in. w.c.)" : string.Empty)
            : $"Design pressure {Pressure(result.DesignPressureInWc ?? 0)} ({result.DesignPressureBasis}), rounded up to the next SMACNA class";
        var sealDetail = seal switch
        {
            SmacnaSealClass.A => "All transverse joints, longitudinal seams and duct wall penetrations sealed",
            SmacnaSealClass.B => "All transverse joints and longitudinal seams sealed",
            _ => "Transverse joints sealed"
        } + (result.SealClassOverridden && result.RecommendedSealClass is SmacnaSealClass recommendedSeal && recommendedSeal != seal
            ? $" (set in Customize Duct Class; recommended: Seal Class {recommendedSeal})"
            : result.SealClassOverridden ? " (set in Customize Duct Class)" : string.Empty);
        static string ShapeName(DuctShape shape) => shape == DuctShape.Round ? "round" : "rectangular";
        var shapes = result.ByShape.Count > 0
            ? result.ByShape.Select(x => x.Shape).ToArray()
            : new[] { DuctShape.Round, DuctShape.Rectangular };
        var leakageClass = string.Join(", ", shapes.Select(shape => $"CL {SmacnaLeakage.LeakageClass(seal, shape)} {ShapeName(shape)}"));
        // One duct type: "12 cfm/100 ft²"; both: "12 / 24 cfm/100 ft²" (round / rectangular).
        var factor = string.Join(" / ", shapes.Select(shape =>
            SmacnaLeakage.AllowableLeakageFactor(SmacnaLeakage.LeakageClass(seal, shape), pressureClass).ToString("0.##", CultureInfo.CurrentCulture)))
                     + " cfm/100 ft²";
        var lines = result.ByShape.Select(x =>
            $"{(x.Shape == DuctShape.Round ? "Round" : "Rectangular")}: F = {x.LeakageClass} x {pressureClass:0.#}^0.65 = {x.FactorCfmPer100Ft2:0.##} cfm/100 ft²; "
            + $"{x.FactorCfmPer100Ft2:0.##} x {x.AreaFt2:0.#} ft² / 100 = {x.LeakageCfm:0.##} cfm").ToList();
        if (!result.IsAvailable)
            return new DuctSystemPdfLeakageSummary(false, classText, basis, $"Seal Class {seal}", sealDetail, leakageClass,
                factor, "Not available", string.Empty, lines, criteria, result.Problem ?? string.Empty);

        var totalCfm = result.TotalLeakageCfm;
        if (result.ByShape.Count > 1)
            lines.Add($"Total duct leakage = {string.Join(" + ", result.ByShape.Select(x => x.LeakageCfm.ToString("0.##", CultureInfo.InvariantCulture)))} = {totalCfm:0.##} cfm");
        var leakage = imperial
            ? $"{totalCfm.ToString("0.#", CultureInfo.CurrentCulture)} cfm"
            : $"{UnitConversions.M3PerSToLps(UnitConversions.CfmToM3PerS(totalCfm)).ToString("0.#", CultureInfo.CurrentCulture)} L/s";
        var fans = _ductSystem.LayoutNodes.Where(node => node.Type == LayoutNodeType.Fan).ToArray();
        var airflowCfm = fans.Length == 1 ? UnitConversions.M3PerSToCfm(fans[0].AirflowM3PerS) : 0;
        var areaText = imperial
            ? $"{result.TotalAreaFt2.ToString("N0", CultureInfo.CurrentCulture)} ft²"
            : $"{(result.TotalAreaFt2 / UnitConversions.SquareMetersToSquareFeet(1)).ToString("N1", CultureInfo.CurrentCulture)} m²";
        var detail = (airflowCfm > 0
                         ? $"{(totalCfm / airflowCfm * 100).ToString("0.0", CultureInfo.CurrentCulture)} % of the fan airflow; "
                         : string.Empty)
                     + $"{areaText} of duct and fittings (fitting areas estimated)";
        return new DuctSystemPdfLeakageSummary(true, classText, basis, $"Seal Class {seal}", sealDetail, leakageClass, factor,
            leakage, detail, lines, criteria);
    }

    private void RefreshPressureProfile()
    {
        var profile = BuildPressureProfile();
        var previous = _analysisPathSelector.SelectedIndex > 0 ? Convert.ToString(_analysisPathSelector.SelectedItem) : null;
        _analysisProfileChart.Profile = profile;
        _updatingPathSelector = true;
        try
        {
            _analysisPathSelector.Items.Clear();
            _analysisPathSelector.Items.Add("All paths");
            foreach (var line in profile.Series)
                _analysisPathSelector.Items.Add(line.IsCritical ? $"{line.Label} (critical)" : line.Label);
            var keep = previous is null ? -1 : _analysisPathSelector.Items.IndexOf(previous);
            _analysisPathSelector.SelectedIndex = keep > 0 ? keep : 0;
            _analysisPathSelector.Enabled = profile.Series.Count > 1;
        }
        finally
        {
            _updatingPathSelector = false;
        }
        ApplyPathSelection();
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

    /// <summary>The Analysis tab's system summary donut (duct segments, fittings, components), beside the path graph.</summary>
    private Control BuildSystemSummaryCard()
    {
        var card = new RoundedPanel
        {
            Name = "AnalysisSystemSummaryCard",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 10,
            BorderColor = UiPalette.CardBorder,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(10, 10, 0, 0)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "System summary",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(11, 31, 59),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _analysisDonut.Dock = DockStyle.Fill;
        layout.Controls.Add(_analysisDonut, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    /// <summary>
    /// The Analysis tab's pressure-profile card: title with the path selector (all paths, or one path isolated) at the
    /// upper right; when a path is isolated, a panel on the left gives its total, how far it is below the critical path,
    /// its route and the component picked on the graph.
    /// </summary>
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var titleRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        titleRow.Controls.Add(new Label
        {
            Text = "Pressure drop along each path",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(11, 31, 59),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        // Negative (suction) and positive (discharge) static pressure sides of the fan: shading on the graph.
        _analysisPressureSidesCheck.Text = "Show − / + static sides";
        _analysisPressureSidesCheck.AutoSize = true;
        _analysisPressureSidesCheck.Checked = true;
        _analysisPressureSidesCheck.Anchor = AnchorStyles.Right;
        _analysisPressureSidesCheck.Margin = new Padding(0, 0, 14, 0);
        _analysisPressureSidesCheck.Font = new Font("Segoe UI", 8.5F);
        _analysisPressureSidesCheck.ForeColor = Color.FromArgb(48, 65, 80);
        new ToolTip().SetToolTip(_analysisPressureSidesCheck,
            "Shade the graph blue before the fan or AHU (negative static pressure, suction side) and amber after it (positive, discharge side).");
        _analysisPressureSidesCheck.CheckedChanged += (_, _) =>
            _analysisProfileChart.ShowPressureSides = _analysisPressureSidesCheck.Checked;
        titleRow.Controls.Add(_analysisPressureSidesCheck, 1, 0);
        titleRow.Controls.Add(new Label
        {
            Text = "Show", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 6, 0),
            ForeColor = Color.FromArgb(74, 91, 106), Font = new Font("Segoe UI", 8.5F)
        }, 2, 0);
        _analysisPathSelector.Dock = DockStyle.Fill;
        _analysisPathSelector.Margin = new Padding(0, 3, 0, 3);
        _analysisPathSelector.Font = new Font("Segoe UI", 9F);
        _analysisPathSelector.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingPathSelector)
                ApplyPathSelection();
        };
        titleRow.Controls.Add(_analysisPathSelector, 3, 0);
        layout.Controls.Add(titleRow, 0, 0);
        _analysisProfileSubtitle.Dock = DockStyle.Fill;
        _analysisProfileSubtitle.TextAlign = ContentAlignment.MiddleLeft;
        _analysisProfileSubtitle.AutoEllipsis = true;
        _analysisProfileSubtitle.Font = new Font("Segoe UI", 8.5F);
        layout.Controls.Add(_analysisProfileSubtitle, 0, 1);
        // The path closest to the critical one: paths of similar loss are better balanced.
        _analysisProfileComparison.AutoSize = true;
        _analysisProfileComparison.Margin = new Padding(0, 2, 0, 0);
        _analysisProfileComparison.Font = new Font("Segoe UI Semibold", 8.5F);
        _analysisProfileComparison.ForeColor = Color.FromArgb(11, 31, 59);
        _analysisProfileComparison.Visible = false;
        layout.Controls.Add(_analysisProfileComparison, 0, 2);

        var body = new TableLayoutPanel { Name = "AnalysisProfileBody", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        BuildPathPanel();
        // The left column holds the isolated path's panel, or with all paths shown the list of every path's total.
        var side = new Panel { Name = "AnalysisProfileSide", Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.White };
        _analysisAllPathsPanel.Dock = DockStyle.Fill;
        _analysisAllPathsPanel.FlowDirection = FlowDirection.TopDown;
        _analysisAllPathsPanel.WrapContents = false;
        _analysisAllPathsPanel.AutoScroll = true;
        _analysisAllPathsPanel.BackColor = Color.FromArgb(244, 248, 253);
        _analysisAllPathsPanel.Padding = new Padding(10, 8, 4, 8);
        _analysisAllPathsPanel.Visible = false;
        side.Controls.Add(_analysisPathPanel);
        side.Controls.Add(_analysisAllPathsPanel);
        body.Controls.Add(side, 0, 0);
        _analysisProfileChart.Dock = DockStyle.Fill;
        _analysisProfileChart.Margin = new Padding(0, 4, 0, 0);
        _analysisProfileChart.SelectionChanged += (_, _) => DescribePickedComponent();
        body.Controls.Add(_analysisProfileChart, 1, 0);
        layout.Controls.Add(body, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private void BuildPathPanel()
    {
        var panel = _analysisPathPanel;
        panel.Dock = DockStyle.Fill;
        panel.ColumnCount = 1;
        panel.RowCount = 6;
        panel.BackColor = Color.FromArgb(244, 248, 253);
        panel.Margin = Padding.Empty;
        panel.Padding = new Padding(10, 8, 8, 8);
        panel.Visible = false;
        void Row(Label label, float size, bool bold, Color color, int row)
        {
            label.AutoSize = true;
            label.MaximumSize = new Size(DpiScaling.Scale(200), 0);
            label.Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size);
            label.ForeColor = color;
            label.Margin = new Padding(0, 0, 0, DpiScaling.Scale(6));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(label, 0, row);
        }
        Row(_analysisPathName, 11F, true, Color.FromArgb(11, 31, 59), 0);
        Row(_analysisPathTotal, 9F, true, Color.FromArgb(10, 88, 187), 1);
        Row(_analysisPathBelow, 8.5F, false, Color.FromArgb(48, 65, 80), 2);
        Row(_analysisPathRoute, 8.5F, false, Color.FromArgb(48, 65, 80), 3);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 6));
        panel.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(214, 225, 236), Margin = Padding.Empty }, 0, 4);
        Row(_analysisPathPick, 8.5F, false, Color.FromArgb(11, 31, 59), 5);
    }

    /// <summary>Shows all paths, or isolates the chosen one with its summary beside the graph.</summary>
    private void ApplyPathSelection()
    {
        var profile = _analysisProfileChart.Profile;
        var index = _analysisPathSelector.SelectedIndex - 1;
        var isolated = profile is not null && index >= 0 && index < profile.Series.Count;
        _analysisProfileChart.IsolatedSeries = isolated ? index : null;
        var listAll = !isolated && profile is { Series.Count: > 0 };
        if (_analysisPathPanel.Parent?.Parent is TableLayoutPanel body)
            body.ColumnStyles[0].Width = isolated || listAll ? DpiScaling.Scale(236) : 0;
        _analysisPathPanel.Visible = isolated;
        _analysisAllPathsPanel.Visible = listAll;
        if (listAll)
            FillAllPathsPanel(profile!);
        if (!isolated)
            return;
        var line = profile!.Series[index];
        var unit = profile.UnitLabel;
        var format = unit == "Pa" ? "0.#" : "0.###";
        string Show(double value) => $"{value.ToString(format, CultureInfo.CurrentCulture)} {unit}";
        var total = line.Values.Count == 0 ? 0 : line.Values[^1];
        var critical = profile.Series.FirstOrDefault(series => series.IsCritical);
        _analysisPathName.Text = line.IsCritical ? $"{line.Label} (critical path)" : line.Label;
        _analysisPathTotal.Text = $"Total path: {Show(total)}";
        if (line.IsCritical)
            _analysisPathBelow.Text = "The highest total: this path sets the fan's required pressure.";
        else if (critical is { Values.Count: > 0 })
        {
            var gap = critical.Values[^1] - total;
            var percent = critical.Values[^1] > 0 ? gap / critical.Values[^1] * 100 : 0;
            _analysisPathBelow.Text = $"{Show(gap)} below the critical path ({percent.ToString("0", CultureInfo.CurrentCulture)} %)";
        }
        else
            _analysisPathBelow.Text = "The critical path is not known until every path can be calculated.";
        _analysisPathRoute.Text = $"Route: {line.Route}\n{line.Values.Count} components";
        DescribePickedComponent();
    }

    /// <summary>
    /// With all paths shown: a heading, then one row per path (name, total, gap to the critical path, route), in
    /// path-number order. The panel scrolls for large systems; clicking a row isolates that path.
    /// </summary>
    private void FillAllPathsPanel(DuctSystemPdfPressureProfile profile)
    {
        var panel = _analysisAllPathsPanel;
        panel.SuspendLayout();
        foreach (Control old in panel.Controls.Cast<Control>().ToArray())
        {
            panel.Controls.Remove(old);
            old.Dispose();
        }
        var unit = profile.UnitLabel;
        var format = unit == "Pa" ? "0.#" : "0.###";
        string Show(double value) => $"{value.ToString(format, CultureInfo.CurrentCulture)} {unit}";
        var width = DpiScaling.Scale(200);
        panel.Controls.Add(new Label
        {
            Name = "AnalysisAllPathsHeading", AutoSize = true, MaximumSize = new Size(width, 0),
            Text = $"All paths ({profile.Series.Count})", Font = new Font("Segoe UI Semibold", 10.5F),
            ForeColor = Color.FromArgb(11, 31, 59), Margin = new Padding(0, 0, 0, DpiScaling.Scale(4))
        });
        panel.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(width, 0), Text = "Total pressure drop of each path. Click one to show it alone.",
            Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(74, 91, 106), Margin = new Padding(0, 0, 0, DpiScaling.Scale(8))
        });
        var critical = profile.Series.FirstOrDefault(series => series.IsCritical);
        for (var index = 0; index < profile.Series.Count; index++)
        {
            var line = profile.Series[index];
            var total = line.Values.Count == 0 ? 0 : line.Values[^1];
            var gap = line.IsCritical || critical is not { Values.Count: > 0 } ? string.Empty
                : $"  ·  {Show(critical.Values[^1] - total)} below";
            var row = new Label
            {
                Name = $"AnalysisAllPathsRow{index + 1}",
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Text = $"{line.Label}{(line.IsCritical ? " (critical)" : string.Empty)}:  {Show(total)}{gap}\n{line.Route}",
                Font = new Font(line.IsCritical ? "Segoe UI Semibold" : "Segoe UI", 8.5F),
                ForeColor = line.IsCritical ? Color.FromArgb(150, 82, 0) : Color.FromArgb(11, 31, 59),
                Cursor = Cursors.Hand,
                Padding = new Padding(DpiScaling.Scale(4), DpiScaling.Scale(3), 0, DpiScaling.Scale(3)),
                Margin = new Padding(0, 0, 0, DpiScaling.Scale(2)),
                Tag = index + 1
            };
            row.MouseEnter += (_, _) => row.BackColor = Color.FromArgb(222, 235, 250);
            row.MouseLeave += (_, _) => row.BackColor = Color.Transparent;
            row.Click += (_, _) =>
            {
                if (row.Tag is int item && item < _analysisPathSelector.Items.Count)
                    _analysisPathSelector.SelectedIndex = item;
            };
            panel.Controls.Add(row);
        }
        panel.ResumeLayout();
    }

    /// <summary>The component picked on the isolated path: its position, ID and pressure.</summary>
    private void DescribePickedComponent()
    {
        var profile = _analysisProfileChart.Profile;
        if (profile is null || _analysisProfileChart.SelectedPoint is not { } picked || picked.Series >= profile.Series.Count)
        {
            _analysisPathPick.Text = "Click a point on the graph to see that component.";
            _analysisPathPick.ForeColor = Color.FromArgb(110, 124, 138);
            return;
        }
        var line = profile.Series[picked.Series];
        var unit = profile.UnitLabel;
        var format = unit == "Pa" ? "0.#" : "0.####";
        var drop = line.Values[picked.Index] - (picked.Index == 0 ? 0 : line.Values[picked.Index - 1]);
        var id = line.ItemIds is { } ids && picked.Index < ids.Count ? ids[picked.Index] : "—";
        _analysisPathPick.ForeColor = Color.FromArgb(11, 31, 59);
        _analysisPathPick.Text = $"Component {picked.Index + 1} of {line.Values.Count}: {id}\n"
                                 + $"Pressure drop: {drop.ToString(format, CultureInfo.CurrentCulture)} {unit}\n"
                                 + $"From the start: {line.Values[picked.Index].ToString(format, CultureInfo.CurrentCulture)} {unit}";
    }

    /// <summary>Rings the item on the path graph (when it lies on a path shown).</summary>
    private void HighlightOnPathGraph(string? itemId) => _analysisProfileChart.HighlightItemId = itemId;

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
