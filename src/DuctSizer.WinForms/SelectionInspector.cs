using System.Drawing.Drawing2D;
using DuctSizer.Core;

namespace DuctSizer.WinForms;

/// <summary>
/// Right-hand panel of the Project tab: describes the item selected on the drawing. Three tabs:
/// Description (what the item is, its inputs and its outputs), Calculations (every equation and substituted value
/// used for it, the same content as its page in the PDF report) and Notes (free text saved with the project).
/// The panel only displays; inputs are changed through the item's existing editor (Edit Inputs), so all validation
/// and the rules that keep the system consistent stay in one place.
/// </summary>
internal sealed class SelectionInspector : Panel
{
    internal const int ExpandedWidth = 340;
    internal const int CollapsedWidth = 30;

    private enum InspectorTab { Description, Calculations, Notes }

    private readonly MainForm _host;
    private readonly Panel _body = new() { Name = "InspectorBody", Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly VerticalTextButton _reopenButton = new() { Name = "InspectorReopenButton", Text = "SELECTION INSPECTOR" };
    private readonly Label _title = new() { Name = "InspectorTitle" };
    private readonly Label _subtitle = new() { Name = "InspectorSubtitle" };
    private readonly InspectorIcon _icon = new() { Name = "InspectorIcon" };
    private readonly Dictionary<InspectorTab, InspectorTabButton> _tabButtons = new();
    private readonly Panel _descriptionPage = new() { Name = "InspectorDescriptionPage", Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
    private readonly RichTextBox _calculations = new() { Name = "InspectorCalculations" };
    private readonly Panel _calculationsPage = new() { Name = "InspectorCalculationsPage", Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10, 8, 6, 8) };
    private readonly TextBox _notes = new() { Name = "InspectorNotes" };
    private readonly Panel _notesPage = new() { Name = "InspectorNotesPage", Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 10, 12, 12) };
    private readonly Label _notesHint = new() { Name = "InspectorNotesHint" };
    private readonly Label _empty = new() { Name = "InspectorEmptyMessage" };
    private readonly Button _editButton = new() { Name = "InspectorEditButton" };
    private readonly ToolTip _toolTips = new() { AutoPopDelay = 9000, InitialDelay = 400 };
    private readonly System.Windows.Forms.Timer _notesSaveTimer = new() { Interval = 1200 };
    private InspectorTab _activeTab = InspectorTab.Description;
    private SavedLayoutNode? _node;
    private LayoutConnectionRef? _connection;
    private (bool IsFitting, int Sequence)? _record;
    private string? _notesKey;
    private bool _loadingNotes;
    private bool _collapsed;

    internal SelectionInspector(MainForm host)
    {
        _host = host;
        Name = "SelectionInspector";
        Dock = DockStyle.Fill;
        Margin = new Padding(6, 0, 0, 0);
        BackColor = Color.White;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Build();
        _notesSaveTimer.Tick += (_, _) => SaveNotesNow();
        ShowNothing();
    }

    /// <summary>Raised when the inspector is collapsed or reopened (its host resizes the column).</summary>
    internal event Action<bool>? CollapsedChanged;

    internal bool IsCollapsed => _collapsed;

    /// <summary>The key of the item being shown, or null (used by checks and by the notes table).</summary>
    internal string? CurrentItemKey { get; private set; }

    internal string CurrentTitle => _title.Text;

    private DuctSystemScreen SystemScreen => _host.DuctSystemScreen;

    private void Build()
    {
        var header = new InspectorHeader { Name = "InspectorHeader", Dock = DockStyle.Top, Height = 34 };
        var headerText = new Label
        {
            Text = "Selection Inspector",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent
        };
        var collapse = new Button
        {
            Name = "InspectorCollapseButton",
            Text = "›",
            Dock = DockStyle.Right,
            Width = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 12F),
            AccessibleName = "Collapse Selection Inspector"
        };
        collapse.FlatAppearance.BorderSize = 0;
        collapse.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 255, 255, 255);
        collapse.Click += (_, _) => SetCollapsed(true);
        _toolTips.SetToolTip(collapse, "Collapse the Selection Inspector");
        header.Controls.Add(headerText);
        header.Controls.Add(collapse);

        var identity = new TableLayoutPanel
        {
            Name = "InspectorIdentity",
            Dock = DockStyle.Top,
            Height = 58,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10, 8, 8, 4),
            BackColor = Color.White
        };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _icon.Dock = DockStyle.Fill;
        _icon.Margin = new Padding(0, 0, 6, 0);
        _title.Dock = DockStyle.Fill;
        _title.Font = new Font("Segoe UI Semibold", 11F);
        _title.ForeColor = RibbonPalette.Ink;
        _title.TextAlign = ContentAlignment.BottomLeft;
        _title.AutoEllipsis = true;
        _subtitle.Dock = DockStyle.Fill;
        _subtitle.Font = new Font("Segoe UI", 8.75F);
        _subtitle.ForeColor = RibbonPalette.Caption;
        _subtitle.TextAlign = ContentAlignment.TopLeft;
        _subtitle.AutoEllipsis = true;
        identity.Controls.Add(_icon, 0, 0);
        identity.SetRowSpan(_icon, 2);
        identity.Controls.Add(_title, 1, 0);
        identity.Controls.Add(_subtitle, 1, 1);

        var tabs = new TableLayoutPanel
        {
            Name = "InspectorTabs",
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 2, 8, 0),
            BackColor = Color.White
        };
        for (var column = 0; column < 3; column++)
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        AddTab(tabs, InspectorTab.Description, "InspectorDescriptionTab", "Description", 0);
        AddTab(tabs, InspectorTab.Calculations, "InspectorCalculationsTab", "Calculations", 1);
        AddTab(tabs, InspectorTab.Notes, "InspectorNotesTab", "Notes", 2);

        _calculations.Dock = DockStyle.Fill;
        _calculations.ReadOnly = true;
        _calculations.BorderStyle = BorderStyle.None;
        _calculations.BackColor = Color.White;
        _calculations.DetectUrls = false;
        _calculations.ScrollBars = RichTextBoxScrollBars.Vertical;
        _calculations.Font = new Font("Segoe UI", 9F);
        _calculationsPage.Controls.Add(_calculations);

        _notes.Dock = DockStyle.Fill;
        _notes.Multiline = true;
        _notes.AcceptsReturn = true;
        _notes.ScrollBars = ScrollBars.Vertical;
        _notes.Font = new Font("Segoe UI", 9.5F);
        _notes.BorderStyle = BorderStyle.FixedSingle;
        _notes.TextChanged += (_, _) => NotesChanged();
        _notes.Leave += (_, _) => SaveNotesNow();
        _notesHint.Dock = DockStyle.Top;
        _notesHint.Height = 40;
        _notesHint.ForeColor = RibbonPalette.Caption;
        _notesHint.Font = new Font("Segoe UI", 8.5F);
        _notesHint.Text = "Notes for this item. They are saved with the project.";
        _notesPage.Controls.Add(_notes);
        _notesPage.Controls.Add(_notesHint);

        _empty.Dock = DockStyle.Fill;
        _empty.TextAlign = ContentAlignment.TopCenter;
        _empty.Padding = new Padding(16, 40, 16, 0);
        _empty.ForeColor = RibbonPalette.Caption;
        _empty.Font = new Font("Segoe UI", 9.5F);
        _empty.Text = "Select an item on the drawing to see its description, inputs, outputs, calculations and notes.";

        _editButton.Text = "Edit Inputs…";
        _editButton.Height = 32;
        _editButton.FlatStyle = FlatStyle.Flat;
        _editButton.BackColor = Color.FromArgb(31, 111, 168);
        _editButton.ForeColor = Color.White;
        _editButton.Font = new Font("Segoe UI Semibold", 9F);
        _editButton.FlatAppearance.BorderSize = 0;
        _editButton.Click += (_, _) => EditSelected();
        _toolTips.SetToolTip(_editButton, "Open this item's editor (same as double-clicking it on the drawing).");

        _reopenButton.Dock = DockStyle.Fill;
        _reopenButton.Visible = false;
        _reopenButton.FlatStyle = FlatStyle.Flat;
        _reopenButton.ForeColor = Color.FromArgb(28, 69, 108);
        _reopenButton.BackColor = Color.FromArgb(235, 242, 247);
        _reopenButton.Font = new Font("Segoe UI Semibold", 8.2F);
        _reopenButton.Click += (_, _) => SetCollapsed(false);

        Controls.Add(_body);
        Controls.Add(tabs);
        Controls.Add(identity);
        Controls.Add(header);
        Controls.Add(_reopenButton);
    }

    private void AddTab(TableLayoutPanel tabs, InspectorTab tab, string name, string text, int column)
    {
        var button = new InspectorTabButton(text) { Name = name, Dock = DockStyle.Fill, Margin = new Padding(1, 0, 1, 0) };
        button.Click += (_, _) => SelectTab(tab);
        _tabButtons[tab] = button;
        tabs.Controls.Add(button, column, 0);
    }

    private void SelectTab(InspectorTab tab)
    {
        if (_activeTab == InspectorTab.Notes && tab != InspectorTab.Notes)
            SaveNotesNow();
        _activeTab = tab;
        ShowActivePage();
    }

    private void ShowActivePage()
    {
        foreach (var (tab, button) in _tabButtons)
            button.Selected = tab == _activeTab;
        _body.SuspendLayout();
        _body.Controls.Clear();
        if (CurrentItemKey is null && _node is null && _connection is null && _record is null)
            _body.Controls.Add(_empty);
        else
            _body.Controls.Add(_activeTab switch
            {
                InspectorTab.Calculations => _calculationsPage,
                InspectorTab.Notes => _notesPage,
                _ => _descriptionPage
            });
        _body.ResumeLayout();
    }

    internal void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;
        if (collapsed)
            SaveNotesNow();
        _collapsed = collapsed;
        foreach (Control control in Controls)
            control.Visible = collapsed ? control == _reopenButton : control != _reopenButton;
        CollapsedChanged?.Invoke(collapsed);
    }

    /// <summary>Shows a component on the drawing (or nothing when <paramref name="node"/> is null).</summary>
    internal void ShowNode(SavedLayoutNode? node)
    {
        if (node is null)
        {
            ShowNothing();
            return;
        }
        SaveNotesNow();
        _node = node;
        _connection = null;
        _record = null;
        RefreshContent();
    }

    /// <summary>Shows the duct on a drawing connection.</summary>
    internal void ShowConnection(LayoutConnectionRef? connection)
    {
        if (connection is null)
        {
            ShowNothing();
            return;
        }
        SaveNotesNow();
        _node = null;
        _connection = connection;
        _record = null;
        RefreshContent();
    }

    /// <summary>
    /// Shows a duct or fitting record that has no item on the drawing (for example one added from a calculator),
    /// chosen from a list rather than on the grid.
    /// </summary>
    internal void ShowRecord(bool isFitting, int sequence)
    {
        SaveNotesNow();
        _node = null;
        _connection = null;
        _record = (isFitting, sequence);
        RefreshContent();
    }

    internal void ShowNothing()
    {
        SaveNotesNow();
        _node = null;
        _connection = null;
        CurrentItemKey = null;
        _notesKey = null;
        _record = null;
        _title.Text = "No selection";
        _subtitle.Text = string.Empty;
        _icon.Kind = null;
        _icon.Invalidate();
        ShowActivePage();
    }

    /// <summary>Rebuilds the panel from the current project (after edits, sizing or a new calculation).</summary>
    internal void RefreshContent()
    {
        var document = SystemScreen._ductSystem;
        if (_node is SavedLayoutNode node && !document.LayoutNodes.Contains(node))
            _node = document.LayoutNodes.FirstOrDefault(x => x.Sequence == node.Sequence);
        if (_connection is LayoutConnectionRef connection && !document.LayoutNodes.Contains(connection.Downstream))
        {
            var downstream = document.LayoutNodes.FirstOrDefault(x => x.Sequence == connection.Downstream.Sequence);
            _connection = downstream is null ? null : new LayoutConnectionRef(downstream, connection.Secondary);
        }
        if (_node is null && _connection is null && _record is null)
        {
            ShowNothing();
            return;
        }

        DuctSystemPdfReportItem? item;
        string itemKey;
        string notesKey;
        LayoutNodeType iconType;
        string? iconGroup = null;
        FittingDefinition? fittingDefinition = null;
        string missingMessage = string.Empty;
        if (_record is var (recordIsFitting, recordSequence))
        {
            iconType = recordIsFitting ? LayoutNodeType.Fitting : LayoutNodeType.Duct;
            if (recordIsFitting && document.Fittings.FirstOrDefault(x => x.Sequence == recordSequence) is { } recordFitting)
            {
                item = SystemScreen.BuildFittingReportItem(recordFitting);
                itemKey = DuctSystemScreen.FittingKey(recordSequence);
                fittingDefinition = FittingCatalog.All.FirstOrDefault(x =>
                    string.Equals(x.Code, recordFitting.FittingCode, StringComparison.OrdinalIgnoreCase));
            }
            else if (!recordIsFitting && document.Ducts.FirstOrDefault(x => x.Sequence == recordSequence) is { } recordDuct)
            {
                item = SystemScreen.BuildDuctReportItem(recordDuct);
                itemKey = DuctSystemScreen.DuctKey(recordSequence);
            }
            else
            {
                ShowNothing();
                return;
            }
            notesKey = itemKey;
        }
        else if (_connection is LayoutConnectionRef selectedConnection)
        {
            var duct = _host.LayoutScreen.DuctForConnection(selectedConnection.Downstream, selectedConnection.Secondary);
            iconType = LayoutNodeType.Duct;
            if (duct is null)
            {
                item = null;
                itemKey = $"C:{selectedConnection.Downstream.Sequence}:{(selectedConnection.Secondary ? 1 : 0)}";
                notesKey = string.Empty;
                missingMessage = "This connection has no sized duct yet. Use Edit Inputs to size it, or Auto Size to size all ductwork. "
                                 + "Until it is sized, the system total is not available.";
            }
            else
            {
                item = SystemScreen.BuildDuctReportItem(duct);
                itemKey = DuctSystemScreen.DuctKey(duct.Sequence);
                notesKey = itemKey;
            }
        }
        else
        {
            var selectedNode = _node!;
            iconType = selectedNode.Type;
            iconGroup = selectedNode.FittingGroup;
            notesKey = DuctSystemScreen.LayoutKey(selectedNode.Sequence);
            if (selectedNode.Type == LayoutNodeType.Fitting
                && selectedNode.LinkedFittingSequence is int fittingSequence
                && document.Fittings.FirstOrDefault(x => x.Sequence == fittingSequence) is { } fitting)
            {
                item = SystemScreen.BuildFittingReportItem(fitting);
                itemKey = DuctSystemScreen.FittingKey(fitting.Sequence);
                fittingDefinition = FittingCatalog.All.FirstOrDefault(x =>
                    string.Equals(x.Code, fitting.FittingCode, StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedNode.Type == LayoutNodeType.Duct
                     && selectedNode.LinkedDuctSequence is int ductSequence
                     && document.Ducts.FirstOrDefault(x => x.Sequence == ductSequence) is { } linkedDuct)
            {
                item = SystemScreen.BuildDuctReportItem(linkedDuct);
                itemKey = DuctSystemScreen.DuctKey(linkedDuct.Sequence);
            }
            else
            {
                item = SystemScreen.BuildNodeReportItem(selectedNode);
                itemKey = DuctSystemScreen.LayoutKey(selectedNode.Sequence);
            }
        }

        CurrentItemKey = itemKey;
        _icon.Kind = iconType;
        _icon.Group = iconGroup;
        _icon.Invalidate();
        if (item is null)
        {
            var downstream = _connection!.Value.Downstream;
            var parentId = _connection.Value.Secondary ? downstream.SecondaryParentNodeId : downstream.ParentNodeId;
            _title.Text = "Unsized duct";
            _subtitle.Text = $"Connection {parentId} → {downstream.Id}";
        }
        else
        {
            _title.Text = $"{item.Id}  ·  {FriendlyType(item.ItemType)}";
            _subtitle.Text = string.IsNullOrWhiteSpace(item.Description) ? FriendlyType(item.ItemType) : item.Description;
        }
        _toolTips.SetToolTip(_title, _title.Text);
        _toolTips.SetToolTip(_subtitle, _subtitle.Text);

        BuildDescriptionPage(item, missingMessage, fittingDefinition);
        BuildCalculationsPage(item, missingMessage);
        LoadNotes(notesKey);
        ShowActivePage();
    }

    /// <summary>The report uses plain-ASCII unit names for its PDF fonts; the screen shows the usual symbols.</summary>
    private static string DisplayUnits(string value) => value
        .Replace("degF", "°F", StringComparison.Ordinal)
        .Replace("degC", "°C", StringComparison.Ordinal);

    private static string FriendlyType(string reportType)
    {
        if (string.IsNullOrWhiteSpace(reportType))
            return "Item";
        var lower = reportType.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    private void BuildDescriptionPage(DuctSystemPdfReportItem? item, string missingMessage, FittingDefinition? definition)
    {
        _descriptionPage.SuspendLayout();
        // The Edit button is reused across selections: take it out before the old rows are disposed.
        _editButton.Parent?.Controls.Remove(_editButton);
        foreach (Control old in _descriptionPage.Controls.Cast<Control>().ToArray())
        {
            _descriptionPage.Controls.Remove(old);
            old.Dispose();
        }
        var stack = new FlowLayoutPanel
        {
            Name = "InspectorDescriptionStack",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8, 6, 8, 12),
            BackColor = Color.White
        };
        var contentWidth = ExpandedWidth - 36;

        if (item is null)
        {
            stack.Controls.Add(new Label
            {
                Name = "InspectorMissingMessage",
                Text = missingMessage,
                AutoSize = false,
                Size = new Size(contentWidth, 72),
                ForeColor = Color.FromArgb(150, 74, 0),
                Font = new Font("Segoe UI", 9F)
            });
        }
        else
        {
            var details = new List<DuctSystemPdfField>
            {
                new("Item type", FriendlyType(item.ItemType)),
                new("Item ID", item.Id),
                new("Description", string.IsNullOrWhiteSpace(item.Description) ? "-" : item.Description)
            };
            details.AddRange((item.QuickCheck ?? Array.Empty<DuctSystemPdfField>())
                .Where(field => details.All(existing => existing.Label != field.Label)));
            var detailSection = AddSection(stack, "InspectorDetailsSection", "Details", details, contentWidth);
            if (!string.IsNullOrWhiteSpace(item.ReferenceImageJpegBase64))
                AddReferenceImage(detailSection, item, definition, contentWidth);
            var inputs = AddSection(stack, "InspectorInputsSection", "Inputs", item.Inputs, contentWidth);
            _editButton.Width = contentWidth - 12;
            _editButton.Margin = new Padding(6, 6, 0, 4);
            inputs.Controls.Add(_editButton);
            AddSection(stack, "InspectorOutputsSection", "Outputs", item.Outputs, contentWidth);
        }
        if (item is null)
        {
            _editButton.Width = contentWidth - 12;
            _editButton.Margin = new Padding(6, 6, 0, 4);
            stack.Controls.Add(_editButton);
        }
        _descriptionPage.Controls.Add(stack);
        _descriptionPage.ResumeLayout();
    }

    /// <summary>A collapsible section: a header bar with a chevron, then label/value rows.</summary>
    private FlowLayoutPanel AddSection(FlowLayoutPanel stack, string name, string heading,
        IReadOnlyList<DuctSystemPdfField> fields, int width)
    {
        var rows = new FlowLayoutPanel
        {
            Name = name + "Rows",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White
        };
        var header = new SectionHeaderButton(heading) { Name = name, Size = new Size(width, 26), Margin = new Padding(0, 4, 0, 4) };
        header.Click += (_, _) =>
        {
            header.Expanded = !header.Expanded;
            rows.Visible = header.Expanded;
        };
        stack.Controls.Add(header);
        foreach (var field in fields)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Size = new Size(width, 26),
                Margin = new Padding(0, 0, 0, 2),
                BackColor = Color.White
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            var label = new Label
            {
                Text = field.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(62, 78, 94),
                Font = new Font("Segoe UI", 8.5F),
                AutoEllipsis = true,
                Margin = new Padding(6, 0, 2, 0)
            };
            var value = new TextBox
            {
                Text = DisplayUnits(field.Value),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = RibbonPalette.Ink,
                Font = new Font("Segoe UI", 8.75F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                TabStop = false
            };
            if (field.Value.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase)
                || field.Value.StartsWith("Not available", StringComparison.OrdinalIgnoreCase))
                value.ForeColor = Color.Firebrick;
            _toolTips.SetToolTip(label, field.Label);
            _toolTips.SetToolTip(value, field.Value);
            row.Controls.Add(label, 0, 0);
            row.Controls.Add(value, 1, 0);
            rows.Controls.Add(row);
        }
        stack.Controls.Add(rows);
        return rows;
    }

    private void AddReferenceImage(FlowLayoutPanel section, DuctSystemPdfReportItem item, FittingDefinition? definition, int width)
    {
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(item.ReferenceImageJpegBase64));
            using var image = Image.FromStream(stream);
            var picture = new PictureBox
            {
                Name = "InspectorReferenceImage",
                Image = new Bitmap(image),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(width - 12, 150),
                Margin = new Padding(6, 6, 0, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Cursor = definition is null ? Cursors.Default : Cursors.Hand,
                AccessibleName = "Fitting reference picture"
            };
            if (definition is not null)
            {
                _toolTips.SetToolTip(picture, "Click to enlarge");
                picture.Click += (_, _) =>
                {
                    if (AutomationOptions.SuppressDialogs)
                        return;
                    using var viewer = new FittingImageViewer(definition);
                    viewer.ShowDialog(FindForm());
                };
            }
            section.Controls.Add(picture);
            section.Controls.Add(new Label
            {
                Text = definition is null ? string.Empty : "Click the picture to enlarge it.",
                AutoSize = true,
                ForeColor = RibbonPalette.Caption,
                Font = new Font("Segoe UI", 8F),
                Margin = new Padding(6, 0, 0, 4)
            });
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OutOfMemoryException)
        {
            // A damaged reference picture is not an engineering result; leave it out rather than fail the panel.
        }
    }

    private void BuildCalculationsPage(DuctSystemPdfReportItem? item, string missingMessage)
    {
        _calculations.Clear();
        var heading = new Font("Segoe UI Semibold", 9.5F);
        var body = new Font("Segoe UI", 9F);
        var value = new Font("Segoe UI Semibold", 9F);
        void Write(string text, Font font, Color color)
        {
            _calculations.SelectionStart = _calculations.TextLength;
            _calculations.SelectionLength = 0;
            _calculations.SelectionFont = font;
            _calculations.SelectionColor = color;
            _calculations.AppendText(text);
        }

        if (item is null)
        {
            Write(missingMessage, body, Color.FromArgb(150, 74, 0));
            return;
        }
        var sections = item.Calculations ?? Array.Empty<DuctSystemPdfCalculationSection>();
        if (sections.Count == 0)
        {
            Write("Manual entry\r\n", heading, RibbonPalette.Ink);
            Write("No equations apply to this item: its values are entered directly (for example a manufacturer's "
                  + "pressure drop) or taken from the connected system.\r\n\r\n", body, RibbonPalette.Caption);
            Write("Outputs\r\n", heading, RibbonPalette.Ink);
            foreach (var field in item.Outputs)
            {
                Write($"{field.Label}: ", body, RibbonPalette.Caption);
                Write($"{field.Value}\r\n", value, RibbonPalette.Ink);
            }
            return;
        }
        foreach (var section in sections)
        {
            Write(section.Heading.ToUpperInvariant() + "\r\n", heading, Color.FromArgb(31, 78, 121));
            foreach (var line in section.Lines)
            {
                // Report lines often end with "=": the value then follows on the same line.
                var formula = line.Formula.TrimEnd();
                var endsWithEquals = formula.EndsWith('=');
                if (line.IsEquation)
                {
                    // Equations are written in the worksheet math markup; render them as the calculators do.
                    _calculations.SelectionStart = _calculations.TextLength;
                    var parts = new List<(string Markup, EngineeringMathRichText.MathTone Tone)>
                        { (formula, EngineeringMathRichText.MathTone.Formula) };
                    if (!string.IsNullOrWhiteSpace(line.HighlightValue) && endsWithEquals)
                    {
                        parts.Add((" " + line.HighlightValue, line.HighlightAsInput
                            ? EngineeringMathRichText.MathTone.Input : EngineeringMathRichText.MathTone.Solved));
                        EngineeringMathRichText.AppendStyledEquation(_calculations, parts.ToArray());
                        if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
                            Write($"    {DisplayUnits(line.InputSubstitution)}\r\n", body, Color.FromArgb(62, 78, 94));
                        continue;
                    }
                    EngineeringMathRichText.AppendStyledEquation(_calculations, parts.ToArray());
                    if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
                        Write($"    = {DisplayUnits(line.InputSubstitution)}\r\n", body, Color.FromArgb(62, 78, 94));
                    if (!string.IsNullOrWhiteSpace(line.HighlightValue))
                        Write($"    = {DisplayUnits(line.HighlightValue)}\r\n", value,
                            line.HighlightAsInput ? Color.FromArgb(42, 110, 60) : Color.FromArgb(31, 78, 121));
                    continue;
                }
                Write(DisplayUnits(formula), body, RibbonPalette.Ink);
                if (!string.IsNullOrWhiteSpace(line.InputSubstitution))
                {
                    Write(endsWithEquals ? $" {DisplayUnits(line.InputSubstitution)}" : $"\r\n    = {DisplayUnits(line.InputSubstitution)}",
                        body, Color.FromArgb(62, 78, 94));
                    endsWithEquals = false;
                }
                if (!string.IsNullOrWhiteSpace(line.HighlightValue))
                    Write(endsWithEquals ? $" {DisplayUnits(line.HighlightValue)}" : $"\r\n    = {DisplayUnits(line.HighlightValue)}", value,
                        line.HighlightAsInput ? Color.FromArgb(42, 110, 60) : Color.FromArgb(31, 78, 121));
                Write("\r\n", body, RibbonPalette.Ink);
            }
            Write("\r\n", body, RibbonPalette.Ink);
        }
        _calculations.SelectionStart = 0;
        _calculations.ScrollToCaret();
    }

    private void LoadNotes(string notesKey)
    {
        _loadingNotes = true;
        try
        {
            _notesKey = string.IsNullOrEmpty(notesKey) ? null : notesKey;
            _notes.Enabled = _notesKey is not null;
            _notes.Text = _notesKey is not null && SystemScreen._ductSystem.ItemNotes.TryGetValue(_notesKey, out var text) ? text : string.Empty;
            _notesHint.Text = _notesKey is null
                ? "Size this connection first; notes are kept with its duct."
                : "Notes for this item. They are saved with the project and kept when the item is re-sized.";
        }
        finally
        {
            _loadingNotes = false;
        }
    }

    private void NotesChanged()
    {
        if (_loadingNotes || _notesKey is null)
            return;
        var notes = SystemScreen._ductSystem.ItemNotes;
        if (string.IsNullOrWhiteSpace(_notes.Text))
            notes.Remove(_notesKey);
        else
            notes[_notesKey] = _notes.Text;
        _notesSaveTimer.Stop();
        _notesSaveTimer.Start();
    }

    /// <summary>Writes pending note edits to the working file.</summary>
    internal void SaveNotesNow()
    {
        if (!_notesSaveTimer.Enabled)
            return;
        _notesSaveTimer.Stop();
        SystemScreen.SaveDuctSystem();
    }

    private void EditSelected()
    {
        if (_record is var (isFitting, sequence))
            SystemScreen.EditSystemRecord(isFitting, sequence);
        else if (_connection is LayoutConnectionRef connection)
            _host.LayoutScreen.EditLayoutConnection(connection);
        else if (_node is SavedLayoutNode node)
            _host.LayoutScreen.EditLayoutNode(node);
        RefreshContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notesSaveTimer.Dispose();
            _toolTips.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Dark header band with the panel title.</summary>
    private sealed class InspectorHeader : Panel
    {
        internal InspectorHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var fill = new SolidBrush(Color.FromArgb(46, 82, 120));
            e.Graphics.FillRectangle(fill, ClientRectangle);
        }
    }

    /// <summary>The selected item's drawing icon (same artwork as on the grid).</summary>
    private sealed class InspectorIcon : Control
    {
        internal InspectorIcon()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        internal LayoutNodeType? Kind { get; set; }

        internal string? Group { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Kind is not LayoutNodeType kind)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var size = Math.Min(Width, Height) - 4;
            var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
            if (kind == LayoutNodeType.Duct)
            {
                using var pen = new Pen(Color.FromArgb(21, 83, 99), 2F);
                using var fill = new SolidBrush(Color.FromArgb(220, 235, 247));
                var duct = new Rectangle(bounds.Left, bounds.Top + bounds.Height / 3, bounds.Width, bounds.Height / 3);
                e.Graphics.FillRectangle(fill, duct);
                e.Graphics.DrawRectangle(pen, duct);
                return;
            }
            LayoutIconPainter.Draw(e.Graphics, kind, bounds, Color.FromArgb(21, 83, 99), Group);
        }
    }

    /// <summary>One of the Description / Calculations / Notes tabs (selected tab has a blue underline).</summary>
    private sealed class InspectorTabButton : Button
    {
        private bool _selected;

        internal InspectorTabButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 9F);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTab;
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(_selected ? Color.FromArgb(240, 246, 252) : Color.White);
            using var border = new Pen(_selected ? RibbonPalette.CheckedBorder : Color.FromArgb(214, 222, 230));
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            if (_selected)
            {
                using var underline = new SolidBrush(Color.FromArgb(31, 111, 168));
                e.Graphics.FillRectangle(underline, 1, Height - 3, Width - 2, 3);
            }
            using var font = new Font("Segoe UI", 9F, _selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle,
                _selected ? Color.FromArgb(22, 65, 102) : RibbonPalette.Caption,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(3, 3, Width - 6, Height - 6));
        }
    }

    /// <summary>Collapsible section header: light band, chevron and bold caption.</summary>
    private sealed class SectionHeaderButton : Button
    {
        private bool _expanded = true;

        internal SectionHeaderButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            AccessibleName = text;
        }

        internal bool Expanded
        {
            get => _expanded;
            set
            {
                _expanded = value;
                AccessibleDescription = value ? "Expanded" : "Collapsed";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.White);
            using (var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), 4))
            using (var fill = new SolidBrush(Color.FromArgb(232, 239, 246)))
                e.Graphics.FillPath(fill, path);
            using var arrow = new SolidBrush(Color.FromArgb(31, 78, 121));
            var cy = Height / 2f;
            e.Graphics.FillPolygon(arrow, _expanded
                ? new[] { new PointF(9, cy - 2), new PointF(17, cy - 2), new PointF(13, cy + 3) }
                : new[] { new PointF(11, cy - 4), new PointF(11, cy + 4), new PointF(16, cy) });
            using var font = new Font("Segoe UI Semibold", 9F);
            TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(24, 0, Width - 28, Height),
                Color.FromArgb(22, 50, 78), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4));
        }
    }
}
