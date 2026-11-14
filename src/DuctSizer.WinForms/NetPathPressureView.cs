using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// The net path pressure graph with its controls (round Y), used on the Project tab (bottom panel) and the Analysis tab:
/// Static / Total / Velocity pressure check boxes that show or hide each line, a Show list that picks all paths or one
/// path, and the graph (<see cref="NetStaticPressureChart"/>). Clicking a point raises <see cref="ItemClicked"/>; the
/// host selects that item and rings the item selected elsewhere with <see cref="HighlightItemId"/>.
/// </summary>
internal sealed class NetPathPressureView : Panel
{
    private readonly ComboBox _pathCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly CheckBox _static = new();
    private readonly CheckBox _total = new();
    private readonly CheckBox _velocity = new();
    private bool _filling;
    private readonly ToolTip _explanation = new() { AutoPopDelay = 12000 };

    /// <param name="prefix">Prefix of the control names, so two views can be found apart ("NetStatic", "AnalysisNetPath").</param>
    /// <param name="toolTip">Explanation shown over the graph.</param>
    internal NetPathPressureView(string prefix, string toolTip)
    {
        Name = prefix + "View";
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Chart.Name = prefix == "NetStatic" ? "NetStaticPressureChart" : prefix + "Chart";
        Chart.Dock = DockStyle.Fill;
        _pathCombo.Name = prefix + "PathCombo";
        _static.Name = prefix + "StaticCheck";
        _total.Name = prefix + "TotalCheck";
        _velocity.Name = prefix + "VelocityCheck";

        var header = new TableLayoutPanel
        {
            Name = prefix + "Header",
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(8, 4, 10, 0)
        };
        for (var column = 0; column < 5; column++)
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label
        {
            Name = prefix + "LinesCaption",
            Text = "Lines:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(74, 91, 106),
            Margin = new Padding(0, 7, 0, 0)
        }, 0, 0);
        ConfigureCheck(_static, "Static", NetStaticPressureChart.SelectedColor,
            "Show the static pressure line (gauge, solid): total pressure less velocity pressure.");
        ConfigureCheck(_total, "Total", NetStaticPressureChart.SelectedColor,
            "Show the total pressure line (gauge, dashed).");
        ConfigureCheck(_velocity, "Velocity", NetStaticPressureChart.VelocityColor,
            "Show the velocity pressure of the section leaving each component.");
        _static.CheckedChanged += (_, _) => Chart.ShowStatic = _static.Checked;
        _total.CheckedChanged += (_, _) => Chart.ShowTotal = _total.Checked;
        _velocity.CheckedChanged += (_, _) => Chart.ShowVelocity = _velocity.Checked;
        header.Controls.Add(_static, 1, 0);
        header.Controls.Add(_total, 2, 0);
        header.Controls.Add(_velocity, 3, 0);
        header.Controls.Add(new Label
        {
            Text = "Show",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            ForeColor = Color.FromArgb(74, 91, 106),
            Margin = new Padding(16, 7, 4, 0)
        }, 4, 0);
        _pathCombo.Margin = new Padding(0, 3, 0, 0);
        _pathCombo.Dock = DockStyle.Fill;
        _pathCombo.MinimumSize = new Size(140, 0);
        _pathCombo.MaximumSize = new Size(420, 0);
        _pathCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_filling)
                return;
            Chart.SelectedPath = _pathCombo.SelectedIndex <= 0 ? null : _pathCombo.SelectedIndex - 1;
        };
        header.Controls.Add(_pathCombo, 5, 0);
        Chart.ItemClicked += id => ItemClicked?.Invoke(id);
        _explanation.SetToolTip(header, toolTip);
        Controls.Add(Chart);
        Controls.Add(header);
        DialogStyle.AttachFieldInputs(header);
    }

    /// <summary>Raised when a point on the graph is clicked, with the item's ID.</summary>
    internal event Action<string>? ItemClicked;

    internal NetStaticPressureChart Chart { get; } = new();

    internal ComboBox PathCombo => _pathCombo;

    /// <summary>The item ringed on the graph (the item selected on the drawing or in a table).</summary>
    internal string? HighlightItemId
    {
        get => Chart.HighlightItemId;
        set => Chart.HighlightItemId = value;
    }

    private static void ConfigureCheck(CheckBox check, string text, Color accent, string toolTip)
    {
        check.Text = text;
        check.Checked = true;
        check.AutoSize = true;
        check.Anchor = AnchorStyles.Left;
        check.Margin = new Padding(8, 6, 0, 0);
        check.Font = new Font("Segoe UI", 8.5F);
        check.ForeColor = Color.FromArgb(Math.Max(0, accent.R - 10), Math.Max(0, accent.G - 30), Math.Max(0, accent.B - 40));
        new ToolTip().SetToolTip(check, toolTip);
    }

    /// <summary>
    /// Rebuilds the graph and the Show list from <paramref name="document"/>. The picked path is kept when the same
    /// route still exists.
    /// </summary>
    internal void RefreshFrom(DuctSystemDocument document, bool imperial)
    {
        IReadOnlyList<PathPressureProfile> paths;
        try
        {
            paths = document.LayoutNodes.Count == 0 ? Array.Empty<PathPressureProfile>() : PathPressureProfiles.Build(document);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        {
            DiagnosticLog.Error("Net path pressure could not be built.", ex);
            paths = Array.Empty<PathPressureProfile>();
        }
        var previousRoute = Chart.SelectedPath is int previous && previous < Chart.Paths.Count
            ? Chart.Paths[previous].Path.Route
            : null;
        Chart.SetPaths(paths, imperial);
        Chart.EmptyMessage = document.LayoutNodes.Count == 0
            ? "Draw and connect the system to see the pressure along each path."
            : "Connect the system components to see the pressure along each path.";
        _filling = true;
        try
        {
            _pathCombo.BeginUpdate();
            _pathCombo.Items.Clear();
            _pathCombo.Items.Add(Chart.Paths.Count == 1 ? "All paths (1)" : $"All paths ({Chart.Paths.Count})");
            foreach (var profile in Chart.Paths)
                _pathCombo.Items.Add($"{profile.Path.Name}{(profile.Path.IsCritical ? " (critical)" : string.Empty)}: {profile.Path.Route}"
                                     + (profile.IsComplete ? string.Empty : " (incomplete)"));
            _pathCombo.EndUpdate();
            var keep = previousRoute is null ? -1 : Chart.Paths.ToList().FindIndex(x => x.Path.Route == previousRoute);
            _pathCombo.SelectedIndex = keep >= 0 ? keep + 1 : 0;
            Chart.SelectedPath = keep >= 0 ? keep : null;
            MainForm.ResizeComboDropDown(_pathCombo);
        }
        finally
        {
            _filling = false;
        }
    }
}
