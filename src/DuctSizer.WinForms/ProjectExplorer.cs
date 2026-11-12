namespace DuctSizer.WinForms;

/// <summary>
/// Left-hand panel of the Project tab: every item of the project's system, grouped by kind (equipment, sources,
/// sinks, terminals, dampers, fittings, duct segments, calculation points). Groups start collapsed. Clicking an
/// entry selects it on the drawing, brings it into view and shows it in the Selection Inspector. A search box
/// filters the lists. The explorer only reads the model.
/// </summary>
internal sealed class ProjectExplorer : Panel
{
    internal const int ExpandedWidth = 236;
    internal const int CollapsedWidth = 30;

    private readonly MainForm _host;
    private readonly TreeView _tree = new() { Name = "ProjectExplorerTree" };
    private readonly TextBox _search = new() { Name = "ProjectExplorerSearch" };
    private readonly VerticalTextButton _reopenButton = new() { Name = "ProjectExplorerReopenButton", Text = "PROJECT EXPLORER" };
    private readonly HashSet<string> _expandedGroups = new(StringComparer.Ordinal);
    private IReadOnlyList<ExplorerGroup> _groups = Array.Empty<ExplorerGroup>();
    private bool _populating;
    private bool _collapsed;

    internal ProjectExplorer(MainForm host)
    {
        _host = host;
        Name = "ProjectExplorer";
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 6, 0);
        BackColor = Color.White;

        var header = new Panel { Name = "ProjectExplorerHeader", Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(46, 82, 120) };
        var title = new Label
        {
            Text = "Project Explorer",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        var collapse = new Button
        {
            Name = "ProjectExplorerCollapseButton",
            Text = "‹",
            Dock = DockStyle.Right,
            Width = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(46, 82, 120),
            Font = new Font("Segoe UI Semibold", 12F),
            AccessibleName = "Collapse Project Explorer"
        };
        collapse.FlatAppearance.BorderSize = 0;
        collapse.Click += (_, _) => SetCollapsed(true);
        header.Controls.Add(title);
        header.Controls.Add(collapse);

        var searchHost = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 7, 8, 5), BackColor = Color.White };
        _search.Dock = DockStyle.Fill;
        _search.PlaceholderText = "Search project…";
        _search.Font = new Font("Segoe UI", 9F);
        _search.AccessibleName = "Search project items";
        _search.TextChanged += (_, _) => Rebuild();
        searchHost.Controls.Add(_search);

        _tree.Dock = DockStyle.Fill;
        _tree.BorderStyle = BorderStyle.None;
        _tree.Font = new Font("Segoe UI", 9F);
        _tree.HideSelection = false;
        _tree.FullRowSelect = true;
        _tree.ShowLines = false;
        _tree.ItemHeight = 22;
        _tree.Indent = 14;
        _tree.AfterExpand += (_, args) =>
        {
            if (!_populating && args.Node?.Tag is ExplorerGroup group)
                _expandedGroups.Add(group.Name);
        };
        _tree.AfterCollapse += (_, args) =>
        {
            if (!_populating && args.Node?.Tag is ExplorerGroup group)
                _expandedGroups.Remove(group.Name);
        };
        _tree.AfterSelect += (_, args) =>
        {
            if (_populating || args.Node?.Tag is not ExplorerEntry entry)
                return;
            _host.LayoutScreen.SelectExplorerEntry(entry);
        };
        var treeHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 2, 4), BackColor = Color.White };
        treeHost.Controls.Add(_tree);

        _reopenButton.Dock = DockStyle.Fill;
        _reopenButton.Visible = false;
        _reopenButton.FlatStyle = FlatStyle.Flat;
        _reopenButton.ForeColor = Color.FromArgb(16, 45, 84);
        _reopenButton.BackColor = Color.FromArgb(235, 242, 247);
        _reopenButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _reopenButton.Click += (_, _) => SetCollapsed(false);

        Controls.Add(treeHost);
        Controls.Add(searchHost);
        Controls.Add(header);
        Controls.Add(_reopenButton);
    }

    internal event Action<bool>? CollapsedChanged;

    internal TreeView Tree => _tree;

    internal void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;
        _collapsed = collapsed;
        foreach (Control control in Controls)
            control.Visible = collapsed ? control == _reopenButton : control != _reopenButton;
        CollapsedChanged?.Invoke(collapsed);
    }

    /// <summary>Shows the current project's items; group expansion and the search text are kept.</summary>
    internal void Populate(IReadOnlyList<ExplorerGroup> groups)
    {
        _groups = groups;
        Rebuild();
    }

    private void Rebuild()
    {
        var filter = _search.Text.Trim();
        var searching = filter.Length > 0;
        _populating = true;
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            foreach (var group in _groups)
            {
                var entries = searching
                    ? group.Entries.Where(entry => entry.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray()
                    : group.Entries;
                if (searching && entries.Count == 0)
                    continue;
                var groupNode = new TreeNode($"{group.Title} ({entries.Count})")
                {
                    Name = "ExplorerGroup" + group.Name,
                    Tag = group,
                    NodeFont = new Font("Segoe UI Semibold", 9F)
                };
                foreach (var entry in entries)
                {
                    var child = new TreeNode(entry.Text) { Tag = entry, ToolTipText = entry.Text };
                    groupNode.Nodes.Add(child);
                }
                _tree.Nodes.Add(groupNode);
                // Groups start collapsed; the user's choice is kept, and a search opens the matching groups.
                if (searching || _expandedGroups.Contains(group.Name))
                    groupNode.Expand();
            }
        }
        finally
        {
            _tree.EndUpdate();
            _populating = false;
        }
    }
}
