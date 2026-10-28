using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sidekey
{
    interface IPage { void OnPageShown(); }

    // Выполняет работу (например, запрос в интернет) в фоне, а результат
    // всегда передаёт обратно в поток интерфейса.
    static class Background
    {
        public static void Run<T>(Control ui, Func<T> work, Action<T> done, Action<Exception> fail)
        {
            System.Threading.Tasks.Task.Run(work).ContinueWith(t =>
            {
                Control target = ui.FindForm() ?? ui;
                if (target.IsDisposed || !target.IsHandleCreated) return;
                try
                {
                    target.BeginInvoke(new Action(() =>
                    {
                        if (ui.IsDisposed) return;
                        if (t.IsFaulted) fail(t.Exception.GetBaseException());
                        else done(t.Result);
                    }));
                }
                catch (InvalidOperationException) { } // окно успели закрыть
            });
        }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(30, 31, 34);
        public static readonly Color Surface = Color.FromArgb(43, 45, 49);
        public static readonly Color Input = Color.FromArgb(54, 57, 63);
        public static readonly Color Border = Color.FromArgb(72, 75, 82);
        public static readonly Color Hover = Color.FromArgb(64, 67, 74);
        public static readonly Color Text = Color.FromArgb(232, 233, 236);
        public static readonly Color Muted = Color.FromArgb(158, 162, 170);
        public static readonly Color Accent = Color.FromArgb(88, 140, 245);
        public static readonly Color AccentHover = Color.FromArgb(112, 158, 250);
        public static readonly Color Danger = Color.FromArgb(240, 110, 100);
        public static readonly Color Warn = Color.FromArgb(232, 180, 80);
        public static readonly Color Ok = Color.FromArgb(98, 190, 110);

        public static float Scale = 1f;
        public static Font Base, Small, Bold, Title, Big, Huge, Icons;
        public static ToolTip Tips;

        public static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            Icons = new Font("Segoe MDL2 Assets", 10f);
            Tips = new ToolTip { InitialDelay = 400 };
            UseFontsFor("en");
        }

        // В Segoe UI нет иероглифов и индийских письменностей: Windows подставляет их из других
        // шрифтов, и размеры кнопок считаются неточно. Для таких языков берём шрифт, где они есть.
        public static void UseFontsFor(string language)
        {
            string family = language == "zh" ? "Microsoft YaHei UI" : language == "hi" || language == "bn" ? "Nirmala UI" : null;
            if (family == null)
            {
                Base = new Font("Segoe UI", 9.5f);
                Small = new Font("Segoe UI", 8.5f);
                Bold = new Font("Segoe UI Semibold", 9.5f);
                Title = new Font("Segoe UI Semibold", 11.5f);
                Big = new Font("Segoe UI Semibold", 16f);
                Huge = new Font("Segoe UI Light", 36f);
                return;
            }
            Base = new Font(family, 9f);
            Small = new Font(family, 8f);
            Bold = new Font(family, 9f, FontStyle.Bold);
            Title = new Font(family, 11f, FontStyle.Bold);
            Big = new Font(family, 15f, FontStyle.Bold);
            Huge = new Font("Segoe UI Light", 36f); // только цифры таймера и температуры
        }

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static Padding P(int l, int t, int r, int b) { return new Padding(S(l), S(t), S(r), S(b)); }

        public static Button Btn(string text, bool primary = false)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Input,
                ForeColor = primary ? Color.White : Text,
                Font = Base,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(S(34), S(30)),
                Padding = P(8, 0, 8, 0),
                Margin = P(0, 0, 6, 6),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Hover;
            b.FlatAppearance.MouseDownBackColor = primary ? Accent : Border;
            if (primary)
                b.EnabledChanged += (s, e) =>
                {
                    b.BackColor = b.Enabled ? Accent : Theme.Input;
                    b.FlatAppearance.BorderColor = b.Enabled ? Accent : Border;
                };
            return b;
        }

        public static Button IconBtn(string glyph, string tip, Color back)
        {
            var b = Btn(glyph);
            b.Font = Icons;
            b.AutoSize = false;
            b.Size = new Size(S(34), S(30));
            b.Padding = Padding.Empty;
            b.BackColor = back;
            b.FlatAppearance.BorderSize = 0;
            Tips.SetToolTip(b, tip);
            return b;
        }

        public static Label Lbl(string text, Font font, Color color)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, Margin = P(0, 0, 0, 6), UseMnemonic = false };
        }

        // Подпись, которая переносится по ширине колонки.
        public static Label Wrap(string text, Font font, Color color)
        {
            var l = Lbl(text, font, color);
            l.Dock = DockStyle.Fill;
            return l;
        }

        // Подпись, выровненная по высоте с кнопками в строке.
        public static Label Inline(string text)
        {
            var l = Lbl(text, Base, Text);
            l.Margin = P(0, 6, 6, 0);
            return l;
        }

        public static TextBox Field()
        {
            return new TextBox { BackColor = Theme.Input, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle, Font = Base, Margin = P(0, 0, 6, 8) };
        }

        public static NumericUpDown Number(int min, int max, int value)
        {
            return new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)),
                Width = S(66), BackColor = Theme.Input, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle,
                Font = Base, TextAlign = HorizontalAlignment.Center, Margin = P(0, 2, 6, 6)
            };
        }

        public static CheckBox Check(string text, bool value)
        {
            return new CheckBox { Text = text, Checked = value, AutoSize = true, ForeColor = Text, Font = Base, Margin = P(0, 2, 0, 6), UseMnemonic = false };
        }

        public static FlowLayoutPanel Flow(params Control[] items)
        {
            var f = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Bg
            };
            f.Controls.AddRange(items);
            return f;
        }

        // Строка: растягиваемый элемент слева и кнопки справа.
        public static TableLayoutPanel Line(Control fill, params Control[] right)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = 1 + right.Length, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill, BackColor = Bg, Margin = P(0, 0, 0, 6), Padding = Padding.Empty
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var c in right) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fill.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            fill.Margin = new Padding(0, fill.Margin.Top, S(6), 0);
            t.Controls.Add(fill, 0, 0);
            for (int i = 0; i < right.Length; i++)
            {
                right[i].Margin = new Padding(i == 0 ? 0 : S(6), 0, 0, 0);
                right[i].Anchor = AnchorStyles.Right;
                t.Controls.Add(right[i], i + 1, 0);
            }
            return t;
        }

        public static Panel Card(string caption, out Label value, out Label captionLabel)
        {
            var p = new Panel { BackColor = Surface, Dock = DockStyle.Fill, Padding = P(12, 8, 10, 6), Margin = Padding.Empty };
            value = new Label { Font = Big, ForeColor = Text, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Text = "—", AutoEllipsis = true };
            captionLabel = new Label { Font = Small, ForeColor = Muted, Dock = DockStyle.Top, AutoSize = false, Height = S(18), Text = caption, AutoEllipsis = true };
            p.Controls.Add(value);
            p.Controls.Add(captionLabel);
            return p;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Значок программы рисуется в коде: синий квадрат с «панелью» справа.
        public static Icon MakeIcon(int size)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var path = RoundRect(new RectangleF(0, 0, size - 1, size - 1), size * 0.22f))
                    using (var b = new SolidBrush(Accent))
                        g.FillPath(b, path);
                    float m = size * 0.2f, pw = size * 0.26f, gap = size * 0.08f;
                    using (var w = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                    {
                        g.FillRectangle(w, size - m - pw, m, pw, size - 2 * m);
                        float lw = size - 2 * m - pw - gap, lh = Math.Max(1.5f, size * 0.09f);
                        for (int i = 0; i < 3; i++)
                            g.FillRectangle(w, m, m + size * 0.04f + i * size * 0.2f, i == 2 ? lw * 0.6f : lw, lh);
                    }
                }
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return icon;
            }
        }
    }

    // Колонка из строк (TableLayoutPanel) — основа разметки всех вкладок.
    class Rows : TableLayoutPanel
    {
        int count;

        public Rows()
        {
            ColumnCount = 1;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
        }

        public T Auto<T>(T c) where T : Control { return Add(c, new RowStyle(SizeType.AutoSize)); }
        public T Fixed<T>(T c, int px) where T : Control { c.Dock = DockStyle.Fill; return Add(c, new RowStyle(SizeType.Absolute, px)); }
        public T Fill<T>(T c, float percent = 100) where T : Control { c.Dock = DockStyle.Fill; return Add(c, new RowStyle(SizeType.Percent, percent)); }

        T Add<T>(T c, RowStyle style) where T : Control
        {
            RowStyles.Add(style);
            Controls.Add(c, 0, count);
            count++;
            RowCount = count;
            return c;
        }

        public void ClearRows()
        {
            while (Controls.Count > 0) Controls[0].Dispose();
            RowStyles.Clear();
            count = 0;
            RowCount = 0;
        }
    }

    class DarkForm : Form
    {
        protected readonly Rows Body = new Rows();

        public DarkForm(string title, int width)
        {
            Text = title;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            Padding = Theme.P(16, 14, 16, 12);
            ClientSize = new Size(Theme.S(width), Theme.S(200));
            Body.Dock = DockStyle.Top;
            Body.AutoSize = true;
            Body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Controls.Add(Body);
        }

        protected override bool ShowWithoutActivation { get { return App.SelfTesting; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(Handle);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitHeight();
        }

        protected void FitHeight()
        {
            PerformLayout();
            ClientSize = new Size(ClientSize.Width, Body.Bottom + Padding.Bottom);
        }

        protected Label Section(string text)
        {
            var l = Theme.Lbl(text, Theme.Title, Theme.Text);
            l.Margin = Theme.P(0, 10, 0, 6);
            return Body.Auto(l);
        }

        // Кнопки внизу справа; передаются в обычном порядке: (OK, Отмена).
        protected void Buttons(params Button[] buttons)
        {
            var f = Theme.Flow();
            f.FlowDirection = FlowDirection.RightToLeft;
            f.Margin = Theme.P(0, 10, 0, 0);
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                buttons[i].MinimumSize = new Size(Theme.S(96), Theme.S(32));
                buttons[i].Margin = Theme.P(6, 0, 0, 0);
                f.Controls.Add(buttons[i]);
            }
            Body.Auto(f);
        }
    }

    // Тёмные окна сообщений вместо стандартных светлых.
    class Dlg : DarkForm
    {
        public Dlg(string title, string text, string ok, string cancel) : base(title, 440)
        {
            var label = Theme.Wrap(text, Theme.Base, Theme.Text);
            label.Margin = Theme.P(0, 0, 0, 8);
            Body.Auto(label);
            var okBtn = Theme.Btn(ok, true);
            okBtn.DialogResult = DialogResult.OK;
            AcceptButton = okBtn;
            if (cancel != null)
            {
                var cancelBtn = Theme.Btn(cancel);
                cancelBtn.DialogResult = DialogResult.Cancel;
                CancelButton = cancelBtn;
                Buttons(okBtn, cancelBtn);
            }
            else
            {
                CancelButton = okBtn;
                Buttons(okBtn);
            }
        }

        public static void Info(string text, string title = null)
        {
            using (var d = new Dlg(title ?? L.T("app.name"), text, L.T("common.ok"), null)) d.ShowDialog(App.Owner);
        }

        public static bool Ask(string text, string yes = null, string no = null, string title = null)
        {
            using (var d = new Dlg(title ?? L.T("app.name"), text, yes ?? L.T("common.yes"), no ?? L.T("common.cancel")))
                return d.ShowDialog(App.Owner) == DialogResult.OK;
        }
    }

    class TabStrip : Control
    {
        readonly string[] items;
        int selected = -1, hover = -1;
        public event EventHandler SelectedChanged;

        public TabStrip(string[] items)
        {
            this.items = items;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected) return;
                selected = value;
                Invalidate();
                var h = SelectedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        Rectangle[] Slots()
        {
            var widths = new int[items.Length];
            int total = 0;
            for (int i = 0; i < items.Length; i++)
            {
                widths[i] = TextRenderer.MeasureText(items[i], Theme.Bold).Width;
                total += widths[i];
            }
            int pad = Theme.S(8);
            int extra = Math.Max(0, (Width - pad * 2 - total) / items.Length);
            var rects = new Rectangle[items.Length];
            int x = pad;
            for (int i = 0; i < items.Length; i++)
            {
                rects[i] = new Rectangle(x, 0, widths[i] + extra, Height);
                x += widths[i] + extra;
            }
            return rects;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using (var line = new SolidBrush(Theme.Border))
                e.Graphics.FillRectangle(line, 0, Height - 1, Width, 1);
            var rects = Slots();
            for (int i = 0; i < items.Length; i++)
            {
                var color = i == selected || i == hover ? Theme.Text : Theme.Muted;
                TextRenderer.DrawText(e.Graphics, items[i], i == selected ? Theme.Bold : Theme.Base, rects[i], color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                if (i == selected)
                    using (var accent = new SolidBrush(Theme.Accent))
                        e.Graphics.FillRectangle(accent, rects[i].X + Theme.S(6), Height - Theme.S(3), rects[i].Width - Theme.S(12), Theme.S(3));
            }
        }

        int HitTest(Point p)
        {
            var rects = Slots();
            for (int i = 0; i < rects.Length; i++) if (rects[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = HitTest(e.Location);
            if (h >= 0) SelectedIndex = h;
        }
    }

    class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            ItemHeight = Theme.S(22);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTheme(Handle, "DarkMode_CFD");
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(sel ? Theme.Hover : Theme.Input)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            var r = new Rectangle(e.Bounds.X + Theme.S(4), e.Bounds.Y, e.Bounds.Width - Theme.S(4), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, r, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // Список с тёмным заголовком и полоской процента в колонке BarColumn.
    class DarkListView : ListView
    {
        public int BarColumn = -1;

        public DarkListView()
        {
            DoubleBuffered = true;
            OwnerDraw = true;
            View = View.Details;
            FullRowSelect = true;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            MultiSelect = false;
            ShowItemToolTips = true;
            SmallImageList = new ImageList { ImageSize = new Size(1, Theme.S(28)) };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTheme(Handle, "DarkMode_Explorer"); // тёмная полоса прокрутки
            IntPtr header = Native.SendMessage(Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero) Native.DarkTheme(header, "DarkMode_ItemsView");
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + Theme.S(8), e.Bounds.Y, e.Bounds.Width - Theme.S(16), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.Small, r, Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                (e.ColumnIndex == 0 ? TextFormatFlags.Left : TextFormatFlags.Right));
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e) { }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            var bounds = e.Bounds;
            if (e.ColumnIndex == 0) bounds.Width = Columns[0].Width;
            using (var b = new SolidBrush(e.Item.Selected ? Theme.Hover : Theme.Surface)) e.Graphics.FillRectangle(b, bounds);
            var r = new Rectangle(bounds.X + Theme.S(8), bounds.Y, bounds.Width - Theme.S(16), bounds.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

            if (e.ColumnIndex == BarColumn && e.Item.Tag is double)
            {
                double pct = (double)e.Item.Tag;
                int textW = Theme.S(38);
                var track = new Rectangle(r.X, r.Y + r.Height / 2 - Theme.S(3), Math.Max(0, r.Width - textW), Theme.S(6));
                using (var tb = new SolidBrush(Theme.Input)) e.Graphics.FillRectangle(tb, track);
                int fill = (int)Math.Round(track.Width * pct / 100.0);
                if (pct > 0 && fill < 1) fill = 1;
                using (var ab = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(ab, track.X, track.Y, fill, track.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Small, r, Theme.Muted, flags | TextFormatFlags.Right);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Base, r, Theme.Text,
                    flags | (e.ColumnIndex == 0 ? TextFormatFlags.Left : TextFormatFlags.Right));
            }
        }
    }

    // Список с двумя строками в каждом элементе (заголовок и подпись).
    class TwoLineList : ListBox
    {
        public Func<object, string> First = o => o.ToString();
        public Func<object, string> Second = o => "";

        public TwoLineList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Theme.S(42);
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            IntegralHeight = false;
            Font = Theme.Base;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTheme(Handle, "DarkMode_Explorer");
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            object item = Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var g = e.Graphics;
            using (var b = new SolidBrush(sel ? Theme.Hover : Theme.Surface)) g.FillRectangle(b, e.Bounds);
            if (sel) using (var a = new SolidBrush(Theme.Accent)) g.FillRectangle(a, e.Bounds.X, e.Bounds.Y, Theme.S(3), e.Bounds.Height);
            using (var sep = new SolidBrush(Theme.Bg)) g.FillRectangle(sep, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Width, 1);
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int half = e.Bounds.Height / 2;
            var r1 = new Rectangle(e.Bounds.X + Theme.S(10), e.Bounds.Y + Theme.S(2), e.Bounds.Width - Theme.S(16), half);
            var r2 = new Rectangle(r1.X, e.Bounds.Y + half - Theme.S(1), r1.Width, half);
            TextRenderer.DrawText(g, First(item), Theme.Bold, r1, Theme.Text, flags);
            TextRenderer.DrawText(g, Second(item), Theme.Small, r2, Theme.Muted, flags);
        }
    }

    // Поле для записи сочетания клавиш: нажмите сочетание — оно появится в поле.
    class HotkeyBox : TextBox
    {
        public uint Mods, Vk;
        public event EventHandler HotkeyChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.FixedSingle;
            Font = Theme.Bold;
            TextAlign = HorizontalAlignment.Center;
            ShortcutsEnabled = false;
            Cursor = Cursors.Hand;
        }

        public void Set(uint mods, uint vk)
        {
            Mods = mods;
            Vk = vk;
            Text = Fmt.Hotkey(mods, vk);
            var h = HotkeyChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode, mods = keyData & Keys.Modifiers;
            if (mods == Keys.None && (key == Keys.Tab || key == Keys.Escape || key == Keys.Enter))
                return base.ProcessCmdKey(ref msg, keyData);
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return true;
            uint m = 0;
            if ((mods & Keys.Control) != 0) m |= Native.MOD_CONTROL;
            if ((mods & Keys.Alt) != 0) m |= Native.MOD_ALT;
            if ((mods & Keys.Shift) != 0) m |= Native.MOD_SHIFT;
            Set(m, (uint)key);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e) { e.SuppressKeyPress = true; base.OnKeyDown(e); }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            BeginInvoke(new Action(() => Select(0, 0)));
        }
    }

    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
    }

    static class Fmt
    {
        public static string Duration(double seconds)
        {
            if (seconds < 1) return L.T("dur.zero");
            if (seconds < 60) return L.T("dur.lessMin");
            long minutes = (long)(seconds / 60);
            long h = minutes / 60, m = minutes % 60;
            if (h == 0) return L.F("dur.m", m);
            return m == 0 ? L.F("dur.h", h) : L.F("dur.hm", h, m);
        }

        public static string Clock(TimeSpan t)
        {
            int s = (int)Math.Ceiling(Math.Max(0, t.TotalSeconds) - 0.001);
            int h = s / 3600, m = s % 3600 / 60, sec = s % 60;
            return h > 0 ? string.Format("{0}:{1:00}:{2:00}", h, m, sec) : string.Format("{0:00}:{1:00}", m, sec);
        }

        public static string Hotkey(uint mods, uint vk)
        {
            var parts = new List<string>();
            if ((mods & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((mods & Native.MOD_WIN) != 0) parts.Add("Win");
            if (vk != 0) parts.Add(KeyName((Keys)vk));
            return string.Join(" + ", parts);
        }

        static string KeyName(Keys k)
        {
            if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (int)(k - Keys.NumPad0);
            switch (k)
            {
                case Keys.Space: return L.T("key.space");
                case Keys.Return: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.Prior: return "PageUp";
                case Keys.Next: return "PageDown";
                case Keys.Oemtilde: return "`";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.Oem6: return "]";
                case Keys.Oem1: return ";";
                case Keys.Oem7: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.Oem5: return "\\";
            }
            return k.ToString();
        }

        public static bool ValidHotkey(uint mods, uint vk)
        {
            return (mods & (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN)) != 0 && vk != 0;
        }

        // Предупреждение о сочетаниях, которые уже используются браузерами и программами.
        public static string HotkeyConflict(uint mods, uint vk)
        {
            uint m = mods & 0xF;
            var k = (Keys)vk;
            bool letterOrDigit = (k >= Keys.A && k <= Keys.Z) || (k >= Keys.D0 && k <= Keys.D9);
            if (m == Native.MOD_CONTROL && k == Keys.T)
                return L.T("hk.ctrlT");
            if (m == (Native.MOD_CONTROL | Native.MOD_SHIFT) && k == Keys.T)
                return L.T("hk.ctrlShiftT");
            if ((m == Native.MOD_CONTROL || m == (Native.MOD_CONTROL | Native.MOD_SHIFT)) && (letterOrDigit || k == Keys.Tab || k == Keys.Space))
                return L.T("hk.ctrlLetter");
            if (m == Native.MOD_ALT && (letterOrDigit || k == Keys.Space || k == Keys.Tab || k == Keys.F4))
                return L.T("hk.altKey");
            return null;
        }
    }
}
