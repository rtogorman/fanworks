using System.Globalization;

namespace DuctSizer.WinForms;

/// <summary>One duct or component listed in the Component Lock Editor.</summary>
/// <param name="Key">"D:{sequence}" for a duct, "L:{sequence}" for a drawn component.</param>
internal sealed record ComponentLockRow(string Key, string ItemId, string Type, string Size, string Description, bool Locked);

/// <summary>
/// Component Lock Editor (round Y): every duct and component of the system that Auto Size Ductwork can size, each with
/// a Locked check box, and Lock All / Unlock All at the top. A locked item keeps its size and geometry when Auto Size
/// Ductwork runs; its airflow is still recalculated from the sources. The changes are applied together when saved.
/// </summary>
internal sealed class ComponentLockEditorDialog : DpiScaledForm
{
    private readonly DataGridView _grid = new();
    private readonly Label _count = new();
    private readonly IReadOnlyList<ComponentLockRow> _rows;

    internal ComponentLockEditorDialog(IReadOnlyList<ComponentLockRow> rows)
    {
        _rows = rows;
        FormKit.SetUp(this, "Component Lock Editor", "ComponentLockEditorDialog",
            new Size(760, Math.Clamp(250 + rows.Count * 28, 380, 640)));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(22, 16, 22, 14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.Controls.Add(FormKit.Header("Locked sizes",
            "A locked duct or component keeps its size when you run Auto Size Ductwork. Its airflow still follows the sources."), 0, 0);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = BackColor, Margin = Padding.Empty };
        var lockAll = BarButton("ComponentLockAllButton", "Lock All");
        var unlockAll = BarButton("ComponentUnlockAllButton", "Unlock All");
        lockAll.Click += (_, _) => SetAll(true);
        unlockAll.Click += (_, _) => SetAll(false);
        _count.Name = "ComponentLockCount";
        _count.AutoSize = true;
        _count.Margin = new Padding(14, 12, 0, 0);
        _count.ForeColor = FormKit.Muted;
        _count.Font = new Font("Segoe UI Semibold", 9F);
        bar.Controls.Add(lockAll);
        bar.Controls.Add(unlockAll);
        bar.Controls.Add(_count);
        root.Controls.Add(bar, 0, 1);

        ConfigureGrid();
        root.Controls.Add(FormKit.Card(_grid, new Padding(2, 4, 2, 4)), 0, 2);
        root.Controls.Add(FormKit.Buttons(this, "Save Locks", "ComponentLockSaveButton", Save), 0, 3);
        Controls.Add(root);
        UpdateCount();
    }

    /// <summary>The lock state of every listed item after Save Locks, keyed by <see cref="ComponentLockRow.Key"/>.</summary>
    internal IReadOnlyDictionary<string, bool> Locks { get; private set; } = new Dictionary<string, bool>();

    private Button BarButton(string name, string text)
    {
        var button = new Button
        {
            Name = name, Text = text, Height = 32, Width = 104, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, ForeColor = UiPalette.Ink, Font = new Font("Segoe UI Semibold", 9F),
            Margin = new Padding(0, 6, 8, 0)
        };
        button.FlatAppearance.BorderColor = RibbonPalette.RibbonBorder;
        UiPolish.RoundButton(button, 8);
        return button;
    }

    private void ConfigureGrid()
    {
        _grid.Name = "ComponentLockGrid";
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.RowTemplate.Height = 26;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 28;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 77, 109);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F);
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 246);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 49, 69);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 252);
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Locked", HeaderText = "Locked", FillWeight = 56, MinimumWidth = 56, SortMode = DataGridViewColumnSortMode.NotSortable
        });
        foreach (var (name, header, weight, minimum) in new[]
                 {
                     ("Item", "Item", 70, 64), ("Type", "Type", 100, 80), ("Size", "Size", 110, 80), ("Description", "Description", 220, 120)
                 })
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, FillWeight = weight, MinimumWidth = minimum, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        foreach (var row in _rows)
        {
            var index = _grid.Rows.Add(row.Locked, row.ItemId, row.Type, row.Size, row.Description);
            _grid.Rows[index].Tag = row.Key;
        }
        // A check box commits at once so the count and Save see it.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, args) =>
        {
            if (args.RowIndex >= 0)
                UpdateCount();
        };
    }

    /// <summary>Ticks or clears every Locked box (Lock All / Unlock All).</summary>
    internal void SetAll(bool locked)
    {
        _grid.EndEdit();
        foreach (DataGridViewRow row in _grid.Rows)
            row.Cells["Locked"].Value = locked;
        UpdateCount();
    }

    private int LockedCount => _grid.Rows.Cast<DataGridViewRow>().Count(row => row.Cells["Locked"].Value is true);

    private void UpdateCount() =>
        _count.Text = _rows.Count == 0
            ? "There are no ducts or components to lock yet."
            : string.Format(CultureInfo.CurrentCulture, "{0} of {1} locked", LockedCount, _rows.Count);

    private void Save()
    {
        _grid.EndEdit();
        Locks = _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => row.Tag is string)
            .ToDictionary(row => (string)row.Tag!, row => row.Cells["Locked"].Value is true, StringComparer.Ordinal);
        DialogResult = DialogResult.OK;
        Close();
    }
}
