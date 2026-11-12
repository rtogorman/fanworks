using System.Drawing.Drawing2D;

namespace DuctSizer.WinForms;

/// <summary>
/// Building blocks for the redesigned pop-up windows (round S): a title with a subtitle, white rounded cards, labelled
/// field rows with units, a primary/secondary button bar, and a scroll area with a slim rounded scroll bar. Inputs
/// get their rounded frames from <see cref="DialogStyle"/>.
/// </summary>
internal static class FormKit
{
    internal static readonly Color Background = Color.FromArgb(244, 247, 250);
    internal static readonly Color Title = Color.FromArgb(18, 46, 72);
    internal static readonly Color Muted = Color.FromArgb(92, 107, 120);
    internal static readonly Color LabelInk = Color.FromArgb(48, 65, 80);

    /// <summary>Common window settings: fixed dialog, centred on its owner, soft background.</summary>
    internal static void SetUp(Form form, string title, string name, Size clientSize)
    {
        form.Text = title;
        form.Name = name;
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.ClientSize = clientSize;
        form.Font = new Font("Segoe UI", 9F);
        form.BackColor = Background;
    }

    /// <summary>A heading with an optional line of explanation under it.</summary>
    internal static Control Header(string title, string? subtitle = null)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Background };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = title, Dock = DockStyle.Fill, ForeColor = Title,
            Font = new Font("Segoe UI Semibold", 12F), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
        }, 0, 0);
        if (!string.IsNullOrWhiteSpace(subtitle))
            panel.Controls.Add(new Label
            {
                Text = subtitle, Dock = DockStyle.Fill, ForeColor = Muted, Margin = new Padding(0, 2, 0, 0),
                TextAlign = ContentAlignment.TopLeft
            }, 0, 1);
        return panel;
    }

    /// <summary>
    /// Turns a list box into a side navigation list: no border, tall rows, and a soft blue rounded highlight with an
    /// accent bar on the selected item.
    /// </summary>
    internal static void NavigationList(ListBox list, int itemHeight = 34)
    {
        list.BorderStyle = BorderStyle.None;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = itemHeight;
        list.BackColor = Color.White;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0)
                return;
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var clear = new SolidBrush(list.BackColor))
                graphics.FillRectangle(clear, e.Bounds);
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = Rectangle.Inflate(e.Bounds, -4, -2);
            if (selected)
            {
                using var path = UiPolish.CreateRoundedPath(bounds, 7);
                using var fill = new SolidBrush(Color.FromArgb(226, 238, 249));
                graphics.FillPath(fill, path);
                using var bar = new SolidBrush(DialogStyle.Accent);
                graphics.FillRectangle(bar, bounds.Left + 1, bounds.Top + 7, 3, Math.Max(2, bounds.Height - 14));
            }
            var text = list.GetItemText(list.Items[e.Index]);
            using var font = new Font("Segoe UI", 9.5F, selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(graphics, text, font, new Rectangle(bounds.Left + 14, bounds.Top, bounds.Width - 16, bounds.Height),
                selected ? Color.FromArgb(20, 76, 128) : LabelInk,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        list.SelectedIndexChanged += (_, _) => list.Invalidate();
    }

    /// <summary>A white rounded card holding <paramref name="content"/>.</summary>
    internal static RoundedPanel Card(Control content, Padding? padding = null)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 10,
            ClipToShape = false,
            BorderColor = UiPalette.CardBorder,
            Padding = padding ?? new Padding(16, 10, 16, 10),
            Margin = new Padding(0, 6, 0, 6)
        };
        content.Dock = DockStyle.Fill;
        card.Controls.Add(content);
        return card;
    }

    /// <summary>A table of label / input / unit rows (label column sized to the longest label).</summary>
    internal static TableLayoutPanel FieldTable(int rowHeight, params (string Label, Control Input, string Unit)[] rows)
    {
        var table = new TableLayoutPanel { ColumnCount = 3, RowCount = rows.Length, BackColor = Color.White, Margin = Padding.Empty };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < rows.Length; index++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            table.Controls.Add(new Label
            {
                Text = rows[index].Label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = LabelInk,
                Font = new Font("Segoe UI Semibold", 9F), Margin = new Padding(0, 0, 18, 0)
            }, 0, index);
            rows[index].Input.Anchor = AnchorStyles.Left;
            table.Controls.Add(rows[index].Input, 1, index);
            table.Controls.Add(new Label
            {
                Text = rows[index].Unit, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Muted, Margin = new Padding(4, 0, 0, 0)
            }, 2, index);
        }
        return table;
    }

    /// <summary>A right-aligned button bar: Cancel, then the primary action (blue, the window's Accept button).</summary>
    internal static FlowLayoutPanel Buttons(Form form, string primaryText, string primaryName, Action primary, string cancelText = "Cancel")
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Background };
        var ok = new Button
        {
            Name = primaryName, Text = primaryText, AutoSize = false, Height = 34,
            Width = Math.Max(104, TextRenderer.MeasureText(primaryText, form.Font).Width + 36),
            BackColor = DialogStyle.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F), Margin = new Padding(8, 4, 0, 0)
        };
        var cancel = new Button
        {
            Text = cancelText, DialogResult = DialogResult.Cancel, Height = 34, Width = 96, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, ForeColor = UiPalette.Ink, Margin = new Padding(8, 4, 0, 0)
        };
        UiPolish.RoundButton(ok, 8);
        UiPolish.RoundButton(cancel, 8);
        ok.Click += (_, _) => primary();
        bar.Controls.Add(ok);
        bar.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return bar;
    }

    /// <summary>A small rounded "pill" label (status, type tags).</summary>
    internal static Label Pill(string text, Color back, Color fore, string? name = null)
    {
        var pill = new Label
        {
            Name = name ?? string.Empty, Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 4, 0, 4)
        };
        void Shape()
        {
            if (pill.Width < 4 || pill.Height < 4)
                return;
            using var path = UiPolish.CreateRoundedPath(new RectangleF(0, 0, pill.Width, pill.Height), pill.Height / 2F);
            pill.Region = new Region(path);
        }
        pill.Resize += (_, _) => Shape();
        Shape();
        return pill;
    }
}

/// <summary>
/// A scroll area with a slim rounded scroll bar in the FanWorks blue-grey, instead of the system bar. Put rows in
/// <see cref="Content"/>; the mouse wheel anywhere over the area scrolls it.
/// </summary>
internal sealed class SlimScrollArea : Panel
{
    private readonly Panel _viewport = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly SlimScrollBar _bar;
    private int _offset;

    internal SlimScrollArea()
    {
        Name = "SlimScrollArea";
        BackColor = Color.White;
        Content = new Panel { Location = Point.Empty, BackColor = Color.White, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _bar = new SlimScrollBar(this) { Dock = DockStyle.Right, Width = 12 };
        _viewport.Controls.Add(Content);
        Controls.Add(_viewport);
        Controls.Add(_bar);
        _viewport.Resize += (_, _) => Relayout();
        Content.SizeChanged += (_, _) => Relayout();
        HookWheel(this);
        Content.ControlAdded += (_, args) =>
        {
            if (args.Control is not null)
                HookWheel(args.Control);
        };
    }

    /// <summary>The scrolled content (rows, a table); it is as wide as the visible area.</summary>
    internal Panel Content { get; }

    internal int Offset => _offset;
    internal int Range => Math.Max(0, Content.Height - _viewport.Height);
    internal int ViewportHeight => _viewport.Height;

    internal void ScrollTo(int offset)
    {
        _offset = Math.Clamp(offset, 0, Range);
        Content.Top = -_offset;
        _bar.Invalidate();
    }

    private void Relayout()
    {
        Content.Width = _viewport.ClientSize.Width;
        foreach (Control child in Content.Controls)
            if (child.Dock == DockStyle.None && child.Anchor.HasFlag(AnchorStyles.Right))
                child.Width = Content.Width - child.Left;
        _bar.Visible = Range > 0;
        ScrollTo(_offset);
    }

    private void HookWheel(Control control)
    {
        control.MouseWheel += (_, args) =>
        {
            if (Range == 0)
                return;
            if (args is HandledMouseEventArgs handled)
                handled.Handled = true;
            ScrollTo(_offset - args.Delta / 120 * DpiScaling.Scale(40));
        };
        foreach (Control child in control.Controls)
            HookWheel(child);
    }

    private sealed class SlimScrollBar : Control
    {
        private readonly SlimScrollArea _owner;
        private bool _dragging;
        private bool _hover;
        private int _dragStartY;
        private int _dragStartOffset;

        internal SlimScrollBar(SlimScrollArea owner)
        {
            _owner = owner;
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        private RectangleF Thumb
        {
            get
            {
                var track = Height - 8f;
                var total = Math.Max(1, _owner.Content.Height);
                var size = Math.Max(DpiScaling.Scale(28), track * _owner.ViewportHeight / total);
                var room = Math.Max(1, track - size);
                var top = 4 + (_owner.Range == 0 ? 0 : room * _owner.Offset / _owner.Range);
                return new RectangleF(3, top, Width - 6, size);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var trackPath = UiPolish.CreateRoundedPath(new RectangleF(3, 4, Width - 6, Height - 8), (Width - 6) / 2F))
            using (var track = new SolidBrush(Color.FromArgb(236, 241, 246)))
                e.Graphics.FillPath(track, trackPath);
            using var thumbPath = UiPolish.CreateRoundedPath(Thumb, (Width - 6) / 2F);
            using var thumb = new SolidBrush(_dragging || _hover ? Color.FromArgb(31, 108, 168) : Color.FromArgb(150, 178, 202));
            e.Graphics.FillPath(thumb, thumbPath);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var thumb = Thumb;
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartOffset = _owner.Offset;
                Capture = true;
            }
            else
                _owner.ScrollTo(_owner.Offset + (e.Y < thumb.Top ? -1 : 1) * _owner.ViewportHeight);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging)
                return;
            var room = Math.Max(1, Height - 8 - Thumb.Height);
            _owner.ScrollTo(_dragStartOffset + (int)Math.Round((e.Y - _dragStartY) * _owner.Range / room));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }
    }
}
