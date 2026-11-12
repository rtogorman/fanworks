namespace DuctSizer.WinForms;

/// <summary>
/// One consistent field layout for every pop-up window (v0.98). The windows are built in code with each input
/// docked to fill its table cell, which stretched short values (an airflow, a pressure, a yes/no choice) across the
/// whole window, and set single-line boxes against the top of taller rows so they sat below their labels.
///
/// This pass, run when a window opens and again whenever content is added to it (pages that are rebuilt when a
/// choice changes), gives every labelled field:
/// <list type="bullet">
/// <item>a width that suits its content: numbers about 8 characters, text fields such as names wider, drop-downs as
/// wide as their longest choice (never wider than the cell);</item>
/// <item>vertical centring on its label, rows tall enough for the control and for a label that wraps;</item>
/// <item>lists with roomy, clearly selected items.</item>
/// </list>
/// It only changes sizes and alignment; it never changes values, events or which controls exist. Fields placed
/// deliberately (not docked to fill) are left alone.
/// </summary>
internal static class DialogStyle
{
    private const int NumberWidth = 112;
    private const int TextWidth = 340;
    private const int MinimumChoiceWidth = 130;

    private static readonly string[] FreeTextWords =
    {
        "name", "description", "designer", "company", "project", "notes", "title", "path", "file", "folder",
        "comment", "label", "caption", "manufacturer", "model", "tag", "remarks"
    };

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Hooked = new();

    /// <summary>Styles the window now and whenever controls are added to it later.</summary>
    internal static void Attach(Form form)
    {
        Hook(form, form);
        Apply(form);
    }

    private static void Hook(Control control, Form form)
    {
        if (Hooked.TryGetValue(control, out _))
            return;
        Hooked.Add(control, new object());
        control.ControlAdded += (_, args) =>
        {
            if (args.Control is null)
                return;
            Hook(args.Control, form);
            foreach (var descendant in Descendants(args.Control))
                Hook(descendant, form);
            QueueApply(form);
        };
        foreach (Control child in control.Controls)
            Hook(child, form);
    }

    private static readonly HashSet<Form> Pending = new();

    private static void QueueApply(Form form)
    {
        if (!form.IsHandleCreated || form.IsDisposed || !Pending.Add(form))
            return;
        form.BeginInvoke(new Action(() =>
        {
            Pending.Remove(form);
            if (!form.IsDisposed)
                Apply(form);
        }));
    }

    /// <summary>Applies the field layout to every labelled field and list under <paramref name="root"/>.</summary>
    internal static void Apply(Control root)
    {
        root.SuspendLayout();
        try
        {
            foreach (var table in new[] { root }.Concat(Descendants(root)).OfType<TableLayoutPanel>().ToArray())
                StyleTable(table);
            foreach (var list in new[] { root }.Concat(Descendants(root)).OfType<ListBox>())
                StyleList(list);
            StyleInputs(root);
        }
        finally
        {
            root.ResumeLayout(true);
        }
    }

    private static void StyleTable(TableLayoutPanel table)
    {
        var rowNeeds = new Dictionary<int, int>();
        foreach (Control control in table.Controls)
        {
            var position = table.GetPositionFromControl(control);
            if (position.Column < 1 || position.Row < 0)
                continue;
            if (table.GetControlFromPosition(position.Column - 1, position.Row) is not Label label || label is LinkLabel)
                continue;
            var fitted = FitField(control, label, table, position.Column);
            if (!fitted)
            {
                // Composite fields (X / Y / Z, value with unit): keep the label level with them.
                if (control is Panel && label.Dock == DockStyle.Fill)
                    label.TextAlign = ContentAlignment.MiddleLeft;
                continue;
            }

            // Label: vertically centred beside its field, wrapping when long.
            label.TextAlign = label.TextAlign switch
            {
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft
            };
            if (label.Dock == DockStyle.None && !label.AutoSize)
                label.Dock = DockStyle.Fill;

            var labelWidth = Math.Max(1, label.Width > 0 ? label.Width - label.Padding.Horizontal : DpiScaling.Scale(180));
            var labelHeight = TextRenderer.MeasureText(label.Text, label.Font, new Size(labelWidth, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + label.Margin.Vertical + DpiScaling.Scale(6);
            var need = Math.Max(labelHeight, control.Height + DpiScaling.Scale(12));
            rowNeeds[position.Row] = Math.Max(rowNeeds.GetValueOrDefault(position.Row), need);
        }

        // Fixed-height rows grow if a control or a wrapped label would not fit (never shrink).
        foreach (var (row, need) in rowNeeds)
        {
            if (row >= table.RowStyles.Count || table.RowStyles[row].SizeType != SizeType.Absolute)
                continue;
            if (table.RowStyles[row].Height < need)
                table.RowStyles[row].Height = need;
        }
    }

    /// <summary>Sizes one field next to its label. False when the field is not one this pass handles.</summary>
    private static bool FitField(Control control, Label label, TableLayoutPanel table, int column)
    {
        if (control.Dock != DockStyle.Fill && !(control.Dock == DockStyle.None && control.Anchor.HasFlag(AnchorStyles.Left)
                                                 && control.Anchor.HasFlag(AnchorStyles.Right)))
        {
            // Placed deliberately; just centre it on the row.
            if (control is TextBox or ComboBox or NumericUpDown && control.Dock == DockStyle.None)
                control.Anchor = AnchorStyles.Left;
            return control is TextBox or ComboBox or NumericUpDown;
        }
        var cell = CellWidth(table, column) - control.Margin.Horizontal;
        var cellWidth = cell > DpiScaling.Scale(40) ? cell : int.MaxValue; // not laid out yet: no limit
        int? width = control switch
        {
            TextBox { Multiline: true } => null,
            TextBox box => IsFreeText(label.Text, box) ? DpiScaling.Scale(TextWidth) : DpiScaling.Scale(NumberWidth),
            NumericUpDown => DpiScaling.Scale(NumberWidth),
            ComboBox combo => ChoiceWidth(combo),
            TableLayoutPanel unitGroup when IsValueWithUnit(unitGroup) => DpiScaling.Scale(NumberWidth) + UnitColumnWidth(unitGroup),
            _ => null
        };
        if (width is not int target)
            return false;
        if (control.MaximumSize.Width > 0)
            target = Math.Min(target, control.MaximumSize.Width);
        target = Math.Max(Math.Min(target, cellWidth), Math.Min(cellWidth, control.MinimumSize.Width));
        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left;
        control.Width = target;
        if (control is TableLayoutPanel group)
            control.Height = group.Controls.OfType<TextBox>().Select(box => box.PreferredHeight).DefaultIfEmpty(control.Height).Max()
                             + DpiScaling.Scale(4);
        return true;
    }

    private static int CellWidth(TableLayoutPanel table, int column)
    {
        var widths = table.GetColumnWidths();
        if (column >= widths.Length)
            return table.ClientSize.Width;
        var span = 0;
        var columns = 1;
        if (table.GetControlFromPosition(column, 0) is Control first)
            columns = Math.Max(1, table.GetColumnSpan(first));
        for (var index = column; index < Math.Min(widths.Length, column + columns); index++)
            span += widths[index];
        return span > 0 ? span : table.ClientSize.Width;
    }

    private static bool IsFreeText(string label, TextBox box)
    {
        var text = (label + " " + box.Name).ToLowerInvariant();
        if (FreeTextWords.Any(word => text.Contains(word, StringComparison.Ordinal)))
            return !text.Contains("number", StringComparison.Ordinal) || text.Contains("name", StringComparison.Ordinal);
        return false;
    }

    private static int ChoiceWidth(ComboBox combo)
    {
        var widest = combo.Items.Cast<object>()
            .Select(item => TextRenderer.MeasureText(combo.GetItemText(item) ?? string.Empty, combo.Font).Width)
            .DefaultIfEmpty(0).Max();
        if (!string.IsNullOrEmpty(combo.Text))
            widest = Math.Max(widest, TextRenderer.MeasureText(combo.Text, combo.Font).Width);
        return Math.Max(DpiScaling.Scale(MinimumChoiceWidth), widest + SystemInformation.VerticalScrollBarWidth + DpiScaling.Scale(14));
    }

    private static bool IsValueWithUnit(TableLayoutPanel group) =>
        group.Controls.Count == 2 && group.Controls.OfType<TextBox>().Count() == 1 && group.Controls.OfType<Label>().Count() == 1;

    private static int UnitColumnWidth(TableLayoutPanel group)
    {
        var unit = group.Controls.OfType<Label>().First();
        return TextRenderer.MeasureText(unit.Text, unit.Font).Width + unit.Padding.Horizontal + DpiScaling.Scale(8);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBox, object> StyledLists = new();

    /// <summary>Roomier list items with a clear selected state (component lists, page lists).</summary>
    private static void StyleList(ListBox list)
    {
        if (list.DrawMode != DrawMode.Normal || StyledLists.TryGetValue(list, out _))
            return;
        StyledLists.Add(list, new object());
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = TextRenderer.MeasureText("Ag", list.Font).Height + DpiScaling.Scale(10);
        list.BorderStyle = BorderStyle.FixedSingle;
        list.IntegralHeight = false;
        var boldFont = new Font(list.Font, FontStyle.Bold);
        list.Disposed += (_, _) => boldFont.Dispose();
        list.DrawItem += (_, args) =>
        {
            if (args.Index < 0)
                return;
            var selected = (args.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(selected ? Color.FromArgb(222, 236, 248) : list.BackColor))
                args.Graphics.FillRectangle(fill, args.Bounds);
            if (selected)
            {
                using var accent = new SolidBrush(Color.FromArgb(31, 108, 168));
                args.Graphics.FillRectangle(accent, args.Bounds.Left, args.Bounds.Top, DpiScaling.Scale(3), args.Bounds.Height);
            }
            var text = list.GetItemText(list.Items[args.Index]);
            var textBounds = Rectangle.FromLTRB(args.Bounds.Left + DpiScaling.Scale(10), args.Bounds.Top, args.Bounds.Right - 4, args.Bounds.Bottom);
            TextRenderer.DrawText(args.Graphics, text, selected ? boldFont : list.Font, textBounds,
                selected ? Color.FromArgb(20, 49, 69) : UiPalette.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((args.State & DrawItemState.Focus) != 0 && (args.State & DrawItemState.NoFocusRect) == 0)
                ControlPaint.DrawFocusRectangle(args.Graphics, args.Bounds);
        };
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    // ---- Web-style inputs, cards and buttons (round S) ------------------------------------------------------------

    internal static readonly Color InputBorder = Color.FromArgb(196, 208, 220);
    internal static readonly Color InputFocus = Color.FromArgb(31, 108, 168);
    internal static readonly Color InputDisabled = Color.FromArgb(243, 246, 249);
    internal static readonly Color Accent = Color.FromArgb(31, 108, 168);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> StyledInputs = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> PaintedParents = new();

    /// <summary>
    /// Rounded, softly bordered inputs (text boxes, number boxes, drop-downs), card-style group boxes and rounded
    /// buttons with a blue primary (the window's Accept button). The rounded frame is painted by the input's
    /// container around the input, which keeps its own events, values and size behaviour.
    /// </summary>
    internal static void StyleInputs(Control root)
    {
        foreach (var control in new[] { root }.Concat(Descendants(root)).ToArray())
        {
            switch (control)
            {
                case TextBox textBox when !InsideGrid(textBox):
                    StyleInput(textBox, () => textBox.BorderStyle = BorderStyle.None);
                    break;
                case NumericUpDown numeric:
                    StyleInput(numeric, () => numeric.BorderStyle = BorderStyle.None);
                    break;
                case ComboBox combo when !InsideGrid(combo):
                    StyleInput(combo, () =>
                    {
                        combo.FlatStyle = FlatStyle.Flat;
                        if (combo.Enabled)
                            combo.BackColor = Color.White;
                        ComboBorderEraser.Attach(combo);
                    });
                    break;
                case GroupBox group:
                    StyleGroup(group);
                    break;
                case Button button:
                    StyleButton(button);
                    break;
            }
        }
    }

    private static bool InsideGrid(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent is DataGridView)
                return true;
        return false;
    }

    private static void StyleInput(Control input, Action makeBorderless)
    {
        if (StyledInputs.TryGetValue(input, out _))
            return;
        StyledInputs.Add(input, new object());
        makeBorderless();
        if (input is TextBox { ReadOnly: false } or NumericUpDown && input.Enabled)
            input.BackColor = Color.White;
        input.ForeColor = UiPalette.Ink;
        // Room for the rounded frame around the input.
        var margin = input.Margin;
        input.Margin = new Padding(Math.Max(margin.Left, 6), Math.Max(margin.Top, 5), Math.Max(margin.Right, 6), Math.Max(margin.Bottom, 5));
        void Repaint(object? sender, EventArgs args) => input.Parent?.Invalidate(Rectangle.Inflate(input.Bounds, 8, 8));
        input.Enter += Repaint;
        input.Leave += Repaint;
        input.EnabledChanged += Repaint;
        input.VisibleChanged += Repaint;
        input.LocationChanged += (_, _) => input.Parent?.Invalidate();
        input.SizeChanged += (_, _) => input.Parent?.Invalidate();
        input.ParentChanged += (_, _) => HookParentPaint(input.Parent);
        HookParentPaint(input.Parent);
    }

    private static void HookParentPaint(Control? parent)
    {
        if (parent is null || PaintedParents.TryGetValue(parent, out _))
            return;
        PaintedParents.Add(parent, new object());
        parent.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || !StyledInputs.TryGetValue(child, out _))
                    continue;
                var frame = child is ComboBox ? Rectangle.Inflate(child.Bounds, 3, 3) : Rectangle.Inflate(child.Bounds, 5, 4);
                using var path = UiPolish.CreateRoundedPath(new RectangleF(frame.X + 0.5F, frame.Y + 0.5F, frame.Width - 1, frame.Height - 1), 6);
                var readOnly = child is TextBox { ReadOnly: true } || !child.Enabled;
                using (var fill = new SolidBrush(readOnly ? InputDisabled : Color.White))
                    args.Graphics.FillPath(fill, path);
                var focused = child.ContainsFocus;
                using var pen = new Pen(focused ? InputFocus : InputBorder, focused ? 1.6F : 1F);
                args.Graphics.DrawPath(pen, path);
            }
        };
        parent.Invalidate();
    }

    /// <summary>Group boxes drawn as soft white cards with a blue title.</summary>
    private static void StyleGroup(GroupBox group)
    {
        if (StyledInputs.TryGetValue(group, out _))
            return;
        StyledInputs.Add(group, new object());
        group.Paint += (_, args) =>
        {
            var graphics = args.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(group.Parent?.BackColor ?? UiPalette.Canvas);
            using var titleFont = new Font("Segoe UI Semibold", 9F);
            var titleHeight = TextRenderer.MeasureText("Ag", titleFont).Height;
            var card = new RectangleF(0.5F, titleHeight / 2F, group.Width - 1.5F, group.Height - titleHeight / 2F - 1.5F);
            using (var path = UiPolish.CreateRoundedPath(card, 8))
            {
                using var fill = new SolidBrush(Color.White);
                using var pen = new Pen(UiPalette.CardBorder, 1F);
                graphics.FillPath(fill, path);
                graphics.DrawPath(pen, path);
            }
            if (!string.IsNullOrEmpty(group.Text))
            {
                var size = TextRenderer.MeasureText(group.Text, titleFont);
                using var titleBack = new SolidBrush(Color.White);
                graphics.FillRectangle(titleBack, 10, 0, size.Width + 6, titleHeight);
                TextRenderer.DrawText(graphics, group.Text, titleFont, new Point(13, 0), Accent);
            }
        };
        group.Invalidate();
    }

    /// <summary>Rounded flat buttons; the window's Accept button is the blue primary action.</summary>
    private static void StyleButton(Button button)
    {
        if (StyledInputs.TryGetValue(button, out _) || button.Parent is null)
            return;
        StyledInputs.Add(button, new object());
        var form = button.FindForm();
        var primary = form is not null && ReferenceEquals(form.AcceptButton, button);
        if (primary)
        {
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", button.Font.Size);
        }
        else if (button.BackColor.GetBrightness() > 0.8F || button.BackColor == SystemColors.Control || button.UseVisualStyleBackColor)
        {
            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.White;
            button.ForeColor = UiPalette.Ink;
        }
        if (button.Region is null && button.Width > 8 && button.Height > 8 && button.FlatStyle != FlatStyle.Flat)
            UiPolish.RoundButton(button, 7);
        else if (button.Region is null && button.FlatStyle == FlatStyle.Flat && button.FlatAppearance.BorderSize > 0)
            UiPolish.RoundButton(button, 7);
        else if (primary)
        {
            button.FlatAppearance.MouseOverBackColor = UiPolish.Lighten(Accent, 10);
            button.FlatAppearance.MouseDownBackColor = UiPolish.Darken(Accent, 10);
        }
    }

    /// <summary>
    /// Paints over a flat drop-down's own square border with its background, so only the rounded frame painted by
    /// its container shows.
    /// </summary>
    private sealed class ComboBorderEraser : NativeWindow
    {
        private readonly ComboBox _combo;

        private ComboBorderEraser(ComboBox combo)
        {
            _combo = combo;
            if (combo.IsHandleCreated)
                AssignHandle(combo.Handle);
            combo.HandleCreated += (_, _) => AssignHandle(combo.Handle);
            combo.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        internal static void Attach(ComboBox combo) => _ = new ComboBorderEraser(combo);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int paint = 0x000F;
            if (m.Msg != paint || _combo.IsDisposed)
                return;
            using var graphics = Graphics.FromHwnd(Handle);
            using var pen = new Pen(_combo.Enabled ? _combo.BackColor : InputDisabled, 2F);
            graphics.DrawRectangle(pen, 0, 0, _combo.Width - 1, _combo.Height - 1);
        }
    }
}
