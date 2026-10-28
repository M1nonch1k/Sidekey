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
        // Цвета текущей темы. Задаются один раз при запуске (Apply), до создания окон:
        // окна берут цвета при создании, поэтому новая тема применяется после перезапуска.
        public static Color Bg, Surface, Input, Border, Hover, Text, Muted, Accent, AccentHover, Danger, Warn, Ok;
        public static Color OnAccent;  // текст на кнопке цвета Accent
        public static Color Soft;      // между фоном и карточкой: наведение на вкладку
        public static Color Faint;     // между фоном и серым текстом: самые второстепенные подписи
        public static bool IsDark = true;

        // Код темы для настроек; названия — в переводах (theme.<код>).
        public static readonly string[] Names = { "dark", "light", "blue", "red", "green" };

        static Theme() { Apply("dark"); }

        public static void Apply(string name)
        {
            IsDark = name != "light";
            Danger = Color.FromArgb(240, 110, 100);
            Warn = Color.FromArgb(232, 180, 80);
            Ok = Color.FromArgb(98, 190, 110);
            OnAccent = Color.White;
            switch (name)
            {
                case "light":
                    Set(Rgb(243, 244, 247), Rgb(255, 255, 255), Rgb(233, 235, 240), Rgb(206, 210, 218), Rgb(222, 226, 233),
                        Rgb(28, 30, 35), Rgb(98, 104, 116), Rgb(44, 108, 228), Rgb(66, 126, 238));
                    Danger = Rgb(205, 60, 50);
                    Warn = Rgb(176, 112, 0);
                    Ok = Rgb(30, 136, 60);
                    break;
                // Цветные темы — сильно затемнённые, без «кислотных» оттенков.
                case "blue":
                    Set(Rgb(15, 21, 33), Rgb(23, 32, 49), Rgb(31, 43, 64), Rgb(48, 64, 92), Rgb(39, 53, 78),
                        Rgb(226, 232, 242), Rgb(140, 154, 178), Rgb(92, 148, 245), Rgb(116, 166, 250));
                    break;
                case "red":
                    Set(Rgb(29, 16, 18), Rgb(43, 24, 27), Rgb(57, 32, 36), Rgb(84, 50, 55), Rgb(71, 40, 45),
                        Rgb(240, 229, 230), Rgb(180, 152, 155), Rgb(222, 92, 92), Rgb(236, 116, 116));
                    break;
                case "green":
                    Set(Rgb(14, 25, 19), Rgb(21, 37, 29), Rgb(28, 50, 39), Rgb(44, 73, 59), Rgb(36, 61, 48),
                        Rgb(226, 238, 230), Rgb(144, 170, 155), Rgb(64, 178, 112), Rgb(86, 196, 132));
                    break;
                default:
                    Set(Rgb(30, 31, 34), Rgb(43, 45, 49), Rgb(54, 57, 63), Rgb(72, 75, 82), Rgb(64, 67, 74),
                        Rgb(232, 233, 236), Rgb(158, 162, 170), Rgb(88, 140, 245), Rgb(112, 158, 250));
                    break;
            }
            Soft = Mix(Bg, Surface, 0.45);
            Faint = Mix(Bg, Muted, 0.7);
        }

        static void Set(Color bg, Color surface, Color input, Color border, Color hover, Color text, Color muted, Color accent, Color accentHover)
        {
            Bg = bg; Surface = surface; Input = input; Border = border; Hover = hover;
            Text = text; Muted = muted; Accent = accent; AccentHover = accentHover;
        }

        static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

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
            var b = new SoftButton
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Input,
                ForeColor = primary ? OnAccent : Text,
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

        public static NumberBox Number(int min, int max, int value)
        {
            return new NumberBox { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Margin = P(0, 2, 6, 6) };
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
            var p = new Panel { BackColor = Surface, Dock = DockStyle.Fill, Padding = P(12, 10, 10, 10), Margin = Padding.Empty };
            // У цифр нет «хвостов» вниз, а строка шрифта оставляет под них место, поэтому по центру
            // поля текст выглядит опущенным; отступ снизу поднимает его на визуальный центр.
            value = new Label { Font = Big, ForeColor = Text, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Text = "—", AutoEllipsis = true, Padding = P(0, 0, 0, 6) };
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

    // Мягкая кнопка со скруглёнными углами в цветах темы. Цвета берутся из обычных свойств кнопки:
    // BackColor, FlatAppearance.MouseOverBackColor / MouseDownBackColor / BorderColor / BorderSize.
    class SoftButton : Button
    {
        bool hover, down;
        public int Radius = Theme.S(8);

        public SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // Цвет под скруглёнными углами — фон ближайшего непрозрачного родителя.
        Color Behind()
        {
            for (var c = Parent; c != null; c = c.Parent)
                if (c.BackColor.A == 255) return c.BackColor;
            return Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Behind());
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = !Enabled ? BackColor : down ? FlatAppearance.MouseDownBackColor : hover ? FlatAppearance.MouseOverBackColor : BackColor;
            if (fill.IsEmpty) fill = BackColor;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float radius = Math.Min(Radius, Math.Min(Width, Height) / 2f);
            using (var path = Theme.RoundRect(r, radius))
            {
                if (fill.A > 0) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                bool border = FlatAppearance.BorderSize > 0 && !FlatAppearance.BorderColor.IsEmpty && FlatAppearance.BorderColor != fill;
                if (border || (Focused && ShowFocusCues))
                    using (var pen = new Pen(Focused && ShowFocusCues ? Theme.Accent : FlatAppearance.BorderColor)) g.DrawPath(pen, path);
            }
            var color = Enabled ? ForeColor : Theme.Muted;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Padding.Left, 0, Width - Padding.Horizontal, Height), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
        }
    }

    // Поле с числом в цветах темы: [−] число [+]. Стрелки и колесо — ±1, PageUp/PageDown — ±10,
    // цифры можно просто набрать. Кнопки −/+ при удержании повторяются.
    class NumberBox : Control
    {
        decimal value, minimum, maximum = 100;
        string typed;                 // набираемое с клавиатуры число; null — не набирается
        int hover = -1, pressed = -1; // 0 — «−», 1 — «+»
        readonly Timer repeat = new Timer();
        public event EventHandler ValueChanged;

        public NumberBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = new Size(Theme.S(100), Theme.S(30));
            Font = Theme.Base;
            TabStop = true;
            repeat.Tick += (s, e) => { repeat.Interval = 60; Step(pressed == 0 ? -1 : 1); };
        }

        public decimal Minimum { get { return minimum; } set { minimum = value; Value = this.value; } }
        public decimal Maximum { get { return maximum; } set { maximum = value; Value = this.value; } }

        public decimal Value
        {
            get { return value; }
            set
            {
                decimal v = Math.Max(minimum, Math.Min(maximum, Math.Round(value)));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        void Step(int delta) { typed = null; Value = value + delta; }

        int ZoneWidth { get { return Height; } } // ширина зон «−» и «+»

        int ZoneAt(Point p) { return p.X < ZoneWidth ? 0 : p.X >= Width - ZoneWidth ? 1 : -1; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Behind());
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundRect(r, Theme.S(8)))
            {
                using (var b = new SolidBrush(Theme.Input)) g.FillPath(b, path);
                for (int zone = 0; zone < 2; zone++)
                {
                    if (!Enabled || (hover != zone && pressed != zone)) continue;
                    var state = g.Save();
                    g.SetClip(path);
                    using (var hb = new SolidBrush(pressed == zone ? Theme.Border : Theme.Hover))
                        g.FillRectangle(hb, zone == 0 ? 0 : Width - ZoneWidth, 0, ZoneWidth, Height);
                    g.Restore(state);
                }
                using (var pen = new Pen(Focused ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
            }
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            var sign = Enabled ? Theme.Muted : Theme.Border;
            TextRenderer.DrawText(g, "−", Theme.Title, new Rectangle(0, 0, ZoneWidth, Height), value > minimum ? sign : Theme.Border, flags);
            TextRenderer.DrawText(g, "+", Theme.Title, new Rectangle(Width - ZoneWidth, 0, ZoneWidth, Height), value < maximum ? sign : Theme.Border, flags);
            string text = typed ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TextRenderer.DrawText(g, text, Font, new Rectangle(ZoneWidth, 0, Width - 2 * ZoneWidth, Height), Enabled ? Theme.Text : Theme.Muted, flags);
        }

        Color Behind()
        {
            for (var c = Parent; c != null; c = c.Parent)
                if (c.BackColor.A == 255) return c.BackColor;
            return Theme.Bg;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int z = ZoneAt(e.Location);
            if (z != hover) { hover = z; Invalidate(); }
            Cursor = z >= 0 ? Cursors.Hand : Cursors.IBeam;
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
            Focus();
            if (e.Button != MouseButtons.Left) return;
            pressed = ZoneAt(e.Location);
            if (pressed < 0) return;
            Step(pressed == 0 ? -1 : 1);
            repeat.Interval = 400;
            repeat.Start();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            repeat.Stop();
            pressed = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Step(Math.Sign(e.Delta));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up: case Keys.Right: Step(1); break;
                case Keys.Down: case Keys.Left: Step(-1); break;
                case Keys.PageUp: Step(10); break;
                case Keys.PageDown: Step(-10); break;
                case Keys.Home: typed = null; Value = minimum; break;
                case Keys.End: typed = null; Value = maximum; break;
                case Keys.Back:
                    if (typed == null) typed = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    typed = typed.Length > 1 ? typed.Substring(0, typed.Length - 1) : "";
                    if (typed != "") Value = decimal.Parse(typed, System.Globalization.CultureInfo.InvariantCulture);
                    Invalidate();
                    break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (!char.IsDigit(e.KeyChar)) return;
            // Первая цифра начинает новое число, следующие дописываются.
            string next = (typed ?? "") + e.KeyChar;
            decimal v;
            if (decimal.TryParse(next, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out v) && v <= maximum)
            {
                typed = next.TrimStart('0') == "" ? "0" : next.TrimStart('0');
                Value = v;
                Invalidate();
            }
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); typed = null; Invalidate(); }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            typed = null; // недонабранное число уже применено (с учётом минимума)
            repeat.Stop();
            pressed = -1;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    // Кнопка с датой; по щелчку открывается календарь в цветах темы.
    class DateBox : SoftButton
    {
        DateTime value = DateTime.Today;
        DateTime closedAt;
        public event EventHandler ValueChanged;

        public DateBox()
        {
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderColor = Theme.Border;
            FlatAppearance.MouseOverBackColor = Theme.Hover;
            FlatAppearance.MouseDownBackColor = Theme.Border;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(124), Theme.S(30));
            ShowText();
            Click += (s, e) => OpenCalendar();
        }

        public DateTime Value
        {
            get { return value; }
            set
            {
                var v = value.Date;
                if (v == this.value) return;
                this.value = v;
                ShowText();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        void ShowText() { Text = value.ToString("d", L.Culture) + "  ▾"; }

        void OpenCalendar()
        {
            // Щелчок по кнопке при открытом календаре закрывает его, а не открывает заново.
            if ((DateTime.Now - closedAt).TotalMilliseconds < 250) return;
            var popup = new CalendarPopup(value);
            popup.Picked += (s, d) => Value = d;
            popup.FormClosed += (s, e) => closedAt = DateTime.Now;
            var screen = PointToScreen(new Point(0, Height + Theme.S(4)));
            var area = Screen.FromControl(this).WorkingArea;
            popup.Location = new Point(Math.Min(screen.X, area.Right - popup.Width), screen.Y + popup.Height > area.Bottom ? PointToScreen(Point.Empty).Y - popup.Height - Theme.S(4) : screen.Y);
            popup.Show(FindForm());
        }
    }

    // Всплывающий календарь: закрывается после выбора дня, по Esc или при щелчке мимо.
    class CalendarPopup : Form
    {
        readonly CalendarView view;
        public event EventHandler<DateTime> Picked;

        public CalendarPopup(DateTime value)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Border;
            Padding = new Padding(1);
            KeyPreview = true;
            view = new CalendarView(value) { Dock = DockStyle.Fill };
            view.Picked += (s, d) => { var h = Picked; if (h != null) h(this, d); Close(); };
            Controls.Add(view);
            ClientSize = new Size(view.PreferredWidth + 2, view.PreferredHeight + 2);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return App.SelfTesting; } }
        public CalendarView View { get { return view; } }

        protected override void OnShown(EventArgs e) { base.OnShown(e); view.Focus(); }
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!App.SelfTesting) Close(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // Месяц сеткой 6×7 в цветах темы. Стрелками ◀ ▶ — другой месяц; клавиши: стрелки, Enter.
    class CalendarView : Control
    {
        DateTime month, selected, focus;
        int hoverDay = -1, hoverNav;  // hoverNav: -1 — «назад», 1 — «вперёд»
        public event EventHandler<DateTime> Picked;

        public CalendarView(DateTime value)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            selected = focus = value.Date;
            month = new DateTime(value.Year, value.Month, 1);
        }

        int Cell { get { return Theme.S(36); } }
        int Pad { get { return Theme.S(10); } }
        int HeaderH { get { return Theme.S(36); } }
        int WeekH { get { return Theme.S(24); } }
        public int PreferredWidth { get { return Cell * 7 + Pad * 2; } }
        public int PreferredHeight { get { return HeaderH + WeekH + Cell * 6 + Pad * 2; } }
        public DateTime Month { get { return month; } }

        DayOfWeek FirstDay { get { return L.Culture.DateTimeFormat.FirstDayOfWeek; } }

        DateTime GridStart
        {
            get
            {
                int shift = ((int)month.DayOfWeek - (int)FirstDay + 7) % 7;
                return month.AddDays(-shift);
            }
        }

        Rectangle CellRect(int i) { return new Rectangle(Pad + i % 7 * Cell, Pad + HeaderH + WeekH + i / 7 * Cell, Cell, Cell); }
        Rectangle PrevRect { get { return new Rectangle(Pad, Pad, Cell, HeaderH); } }
        Rectangle NextRect { get { return new Rectangle(Pad + Cell * 6, Pad, Cell, HeaderH); } }

        public void ShiftMonth(int delta)
        {
            month = month.AddMonths(delta);
            focus = new DateTime(month.Year, month.Month, Math.Min(focus.Day, DateTime.DaysInMonth(month.Year, month.Month)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            var dtf = L.Culture.DateTimeFormat;

            string title = month.ToString("MMMM yyyy", L.Culture);
            if (title.Length > 0) title = char.ToUpper(title[0], L.Culture) + title.Substring(1);
            TextRenderer.DrawText(g, title, Theme.Bold, new Rectangle(Pad + Cell, Pad, Cell * 5, HeaderH), Theme.Text, center);
            foreach (int nav in new[] { -1, 1 })
            {
                var r = nav < 0 ? PrevRect : NextRect;
                if (hoverNav == nav) using (var p = Theme.RoundRect(new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6), Theme.S(8))) using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, p);
                TextRenderer.DrawText(g, nav < 0 ? "‹" : "›", Theme.Title, r, Theme.Text, center);
            }

            for (int i = 0; i < 7; i++)
            {
                var day = (DayOfWeek)(((int)FirstDay + i) % 7);
                TextRenderer.DrawText(g, dtf.ShortestDayNames[(int)day], Theme.Small, new Rectangle(Pad + i * Cell, Pad + HeaderH, Cell, WeekH), Theme.Muted, center);
            }

            var start = GridStart;
            for (int i = 0; i < 42; i++)
            {
                var d = start.AddDays(i);
                var r = CellRect(i);
                var inner = new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
                bool isSel = d == selected, isToday = d == DateTime.Today, inMonth = d.Month == month.Month;
                using (var p = Theme.RoundRect(inner, Theme.S(10)))
                {
                    if (isSel) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
                    else if (hoverDay == i || (Focused && d == focus)) using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, p);
                    if (isToday && !isSel) using (var pen = new Pen(Theme.Accent)) g.DrawPath(pen, p);
                }
                var color = isSel ? Theme.OnAccent : inMonth ? Theme.Text : Theme.Faint;
                TextRenderer.DrawText(g, d.Day.ToString(), Theme.Base, r, color, center);
            }
        }

        int DayAt(Point p)
        {
            for (int i = 0; i < 42; i++) if (CellRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int d = DayAt(e.Location), nav = PrevRect.Contains(e.Location) ? -1 : NextRect.Contains(e.Location) ? 1 : 0;
            if (d != hoverDay || nav != hoverNav) { hoverDay = d; hoverNav = nav; Invalidate(); }
            Cursor = d >= 0 || nav != 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverDay = -1; hoverNav = 0; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (PrevRect.Contains(e.Location)) { ShiftMonth(-1); return; }
            if (NextRect.Contains(e.Location)) { ShiftMonth(1); return; }
            int i = DayAt(e.Location);
            if (i >= 0) Pick(GridStart.AddDays(i));
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ShiftMonth(e.Delta > 0 ? -1 : 1); }

        public void Pick(DateTime d)
        {
            selected = d.Date;
            var h = Picked;
            if (h != null) h(this, selected);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right: case Keys.Enter: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int move = e.KeyCode == Keys.Left ? -1 : e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Up ? -7 : e.KeyCode == Keys.Down ? 7 : 0;
            if (move != 0)
            {
                focus = focus.AddDays(move);
                if (focus.Month != month.Month || focus.Year != month.Year) month = new DateTime(focus.Year, focus.Month, 1);
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown) { ShiftMonth(e.KeyCode == Keys.PageUp ? -1 : 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Pick(focus); e.Handled = true; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Набор «таблеток» для выбора одного варианта (например, периода на вкладке «Время»).
    class ChipGroup : FlowLayoutPanel
    {
        readonly List<SoftButton> chips = new List<SoftButton>();
        int selected = -1;
        public event EventHandler SelectedIndexChanged;

        public ChipGroup(params string[] items)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = true;
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            for (int i = 0; i < items.Length; i++)
            {
                var b = (SoftButton)Theme.Btn(items[i]);
                b.Radius = Theme.S(14);
                b.MinimumSize = new Size(Theme.S(30), Theme.S(28));
                b.Padding = Theme.P(8, 0, 8, 0);
                b.Margin = Theme.P(0, 0, 6, 6);
                b.FlatAppearance.BorderSize = 0;
                int index = i;
                b.Click += (s, e) => SelectedIndex = index;
                chips.Add(b);
                Controls.Add(b);
            }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected) return;
                selected = value;
                for (int i = 0; i < chips.Count; i++)
                {
                    bool on = i == selected;
                    chips[i].BackColor = on ? Theme.Accent : Theme.Input;
                    chips[i].ForeColor = on ? Theme.OnAccent : Theme.Text;
                    chips[i].FlatAppearance.MouseOverBackColor = on ? Theme.AccentHover : Theme.Hover;
                    chips[i].FlatAppearance.MouseDownBackColor = on ? Theme.Accent : Theme.Border;
                    chips[i].Invalidate();
                }
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
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
            // Окно создаётся низким и вырастает под содержимое только здесь, поэтому Windows
            // центрирует его по старой высоте и оно оказывается слишком низко. Центрируем заново.
            if (StartPosition == FormStartPosition.CenterScreen) CenterOnScreen();
        }

        void CenterOnScreen()
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
        }

        // Высота по содержимому, но не выше экрана: если не помещается, появляется прокрутка.
        protected void FitHeight()
        {
            PerformLayout();
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int frame = Height - ClientSize.Height;
            int need = Body.Bottom - AutoScrollPosition.Y + Padding.Bottom;
            int max = area.Height - frame - Theme.S(16);
            AutoScroll = need > max;
            if (AutoScroll) AutoScrollMinSize = new Size(0, need); // прикреплённый сверху Body сам прокрутку не включает
            ClientSize = new Size(ClientSize.Width, Math.Min(need, max));
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

        // Сколько рядов вкладок; вкладки делятся между рядами поровну (7 → 4 + 3).
        public int RowCount = 1;

        int PerRow { get { return (items.Length + RowCount - 1) / RowCount; } }

        int RowWidth(int row, Font f)
        {
            int total = 0;
            for (int i = row * PerRow; i < Math.Min(items.Length, (row + 1) * PerRow); i++) total += TextRenderer.MeasureText(items[i], f).Width;
            return total;
        }

        bool AllRowsFit(Font f, int width)
        {
            for (int r = 0; r < RowCount; r++) if (RowWidth(r, f) > width - Theme.S(8) * 2) return false;
            return true;
        }

        // Если названия не помещаются обычным шрифтом (длинные языки), все вкладки пишутся мельче.
        bool Compact { get { return !AllRowsFit(Theme.Bold, Width); } }
        Font LabelFont(bool selected, bool compact) { return compact ? Theme.Small : selected ? Theme.Bold : Theme.Base; }

        Rectangle[] Slots()
        {
            bool compact = Compact;
            int pad = Theme.S(8), per = PerRow, rowH = Height / RowCount;
            var rects = new Rectangle[items.Length];
            for (int r = 0; r < RowCount; r++)
            {
                int first = r * per, last = Math.Min(items.Length, first + per);
                if (first >= last) break;
                var widths = new int[last - first];
                int total = 0;
                for (int i = first; i < last; i++)
                {
                    widths[i - first] = TextRenderer.MeasureText(items[i], compact ? Theme.Small : Theme.Bold).Width;
                    total += widths[i - first];
                }
                int extra = Math.Max(0, (Width - pad * 2 - total) / (last - first));
                int x = pad;
                for (int i = first; i < last; i++)
                {
                    rects[i] = new Rectangle(x, r * rowH, widths[i - first] + extra, rowH);
                    x += widths[i - first] + extra;
                }
            }
            return rects;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using (var line = new SolidBrush(Theme.Border))
                e.Graphics.FillRectangle(line, 0, Height - 1, Width, 1);
            var rects = Slots();
            bool compact = Compact;
            for (int i = 0; i < items.Length; i++)
            {
                var r = rects[i];
                if (i == selected || i == hover)
                    using (var bg = new SolidBrush(i == selected ? Theme.Surface : Theme.Soft))
                    using (var path = Theme.RoundRect(new RectangleF(r.X + Theme.S(2), r.Y + Theme.S(3), r.Width - Theme.S(4), r.Height - Theme.S(6)), Theme.S(6)))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillPath(bg, path);
                    }
                var color = i == selected || i == hover ? Theme.Text : Theme.Muted;
                TextRenderer.DrawText(e.Graphics, items[i], LabelFont(i == selected, compact), r, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                if (i == selected)
                    using (var accent = new SolidBrush(Theme.Accent))
                        e.Graphics.FillRectangle(accent, r.X + Theme.S(12), r.Bottom - Theme.S(5), r.Width - Theme.S(24), Theme.S(2));
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

        // Помещаются ли названия вкладок без обрезки обычным шрифтом (для самопроверки).
        public bool Fits(int width) { return AllRowsFit(Theme.Bold, width); }
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
            int half = e.Bounds.Height / 2, textX = e.Bounds.X + Theme.S(10);
            var r1 = new Rectangle(textX, e.Bounds.Y + Theme.S(2), e.Bounds.Right - textX - Theme.S(6), half);
            var r2 = new Rectangle(r1.X, e.Bounds.Y + half - Theme.S(1), r1.Width, half);
            TextRenderer.DrawText(g, First(item), Theme.Bold, r1, Theme.Text, flags);
            TextRenderer.DrawText(g, Second(item), Theme.Small, r2, Theme.Muted, flags);
        }
    }

    // Ползунок 0…Maximum (громкость, позиция в треке): мышь, колесо, стрелки, PageUp/PageDown, Home/End.
    class VolumeSlider : Control
    {
        int value, maximum = 100;
        bool dim;
        public bool Dragging { get; private set; }
        public event EventHandler Scrolled;  // только когда значение меняет пользователь
        public event EventHandler Committed; // пользователь закончил: отпустил мышь или нажал клавишу

        public int Maximum
        {
            get { return maximum; }
            set { maximum = Math.Max(1, value); if (this.value > maximum) this.value = maximum; Invalidate(); }
        }

        public VolumeSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Height = Theme.S(30);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public int Value
        {
            get { return value; }
            set { int v = Math.Max(0, Math.Min(maximum, value)); if (v != this.value) { this.value = v; Invalidate(); } }
        }

        // Звук выключен: полоска серая, но громкость остаётся видна.
        public bool Dim { get { return dim; } set { if (value != dim) { dim = value; Invalidate(); } } }

        int Radius { get { return Theme.S(7); } }

        void UserSet(int v)
        {
            int old = value;
            Value = v;
            var h = Scrolled;
            if (value != old && h != null) h(this, EventArgs.Empty);
        }

        int ValueAt(int x)
        {
            int left = Radius, width = Math.Max(1, Width - 2 * Radius);
            return (int)Math.Round((x - left) * (double)maximum / width);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int r = Radius, cy = Height / 2, h = Theme.S(4);
            float left = r, width = Width - 2 * r, x = left + width * value / (float)maximum;
            using (var track = Theme.RoundRect(new RectangleF(left, cy - h / 2f, width, h), h / 2f))
            using (var b = new SolidBrush(Theme.Input)) g.FillPath(b, track);
            var color = dim ? Theme.Muted : Theme.Accent;
            if (x - left >= 1)
                using (var fill = Theme.RoundRect(new RectangleF(left, cy - h / 2f, x - left, h), h / 2f))
                using (var b = new SolidBrush(color)) g.FillPath(b, fill);
            if (Focused)
                using (var ring = new SolidBrush(Color.FromArgb(70, color))) g.FillEllipse(ring, x - r - Theme.S(3), cy - r - Theme.S(3), 2 * r + Theme.S(6), 2 * r + Theme.S(6));
            using (var b = new SolidBrush(dim ? Theme.Muted : Theme.Text)) g.FillEllipse(b, x - r, cy - r, 2 * r, 2 * r);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            Dragging = true;
            UserSet(ValueAt(e.X));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Dragging) UserSet(ValueAt(e.X));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndDrag();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            EndDrag();
        }

        void EndDrag()
        {
            if (!Dragging) return;
            Dragging = false;
            Commit();
        }

        void Commit()
        {
            var h = Committed;
            if (h != null) h(this, EventArgs.Empty);
        }

        // Шаги в долях от максимума: для громкости 2 % / 1 / 10, для трека — секунды.
        int Step { get { return Math.Max(1, maximum / 50); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            UserSet(value + Math.Sign(e.Delta) * Step);
            Commit();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: UserSet(value - Math.Max(1, maximum / 100)); break;
                case Keys.Right: case Keys.Up: UserSet(value + Math.Max(1, maximum / 100)); break;
                case Keys.PageDown: UserSet(value - Math.Max(1, maximum / 10)); break;
                case Keys.PageUp: UserSet(value + Math.Max(1, maximum / 10)); break;
                case Keys.Home: UserSet(0); break;
                case Keys.End: UserSet(maximum); break;
                default: return;
            }
            e.Handled = true;
            Commit();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // «Сейчас играет» как на экране блокировки iPhone: большая обложка на фоне её цвета,
    // под ней название и исполнитель.
    class CoverView : Control
    {
        Image cover;
        Color tint = Theme.Bg;
        string title = "", artist = "", credit = "";

        public CoverView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void Set(string title, string artist, string credit)
        {
            this.title = title ?? "";
            this.artist = artist ?? "";
            this.credit = credit ?? "";
            Invalidate();
        }

        public Image Cover
        {
            get { return cover; }
            set { cover = value; tint = value != null ? Tint(value) : Theme.Bg; Invalidate(); }
        }

        // Средний цвет обложки, приглушённый к тёмному фону, — для подложки.
        static Color Tint(Image img)
        {
            try
            {
                using (var one = new Bitmap(1, 1))
                {
                    using (var g = Graphics.FromImage(one))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(img, new Rectangle(0, 0, 1, 1));
                    }
                    var c = one.GetPixel(0, 0);
                    return Color.FromArgb((c.R * 45 + Theme.Bg.R * 55) / 100, (c.G * 45 + Theme.Bg.G * 55) / 100, (c.B * 45 + Theme.Bg.B * 55) / 100);
                }
            }
            catch { return Theme.Bg; }
        }

        int TextHeight { get { return Theme.S(70); } }

        Rectangle CoverRect
        {
            get
            {
                int size = Math.Max(Theme.S(60), Math.Min(Width - Theme.S(24), Height - TextHeight - Theme.S(12)));
                return new Rectangle((Width - size) / 2, Theme.S(10), size, size);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var bg = new LinearGradientBrush(ClientRectangle.IsEmpty ? new Rectangle(0, 0, 1, 1) : ClientRectangle, tint, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(bg, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var r = CoverRect;
            float radius = Theme.S(12);
            using (var shadow = Theme.RoundRect(new RectangleF(r.X + Theme.S(2), r.Y + Theme.S(5), r.Width, r.Height), radius))
            using (var sb = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(sb, shadow);
            using (var path = Theme.RoundRect(new RectangleF(r.X, r.Y, r.Width, r.Height), radius))
            {
                if (cover != null)
                {
                    var state = g.Save();
                    g.SetClip(path);
                    g.DrawImage(cover, r);
                    g.Restore(state);
                }
                else
                {
                    using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                    using (var note = new Font("Segoe MDL2 Assets", Math.Max(12f, r.Height / 5f / Theme.Scale)))
                        TextRenderer.DrawText(g, "", note, r, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            var flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int y = r.Bottom + Theme.S(12);
            TextRenderer.DrawText(g, title, Theme.Title, new Rectangle(r.X, y, r.Width, Theme.S(24)), Theme.Text, flags);
            TextRenderer.DrawText(g, artist, Theme.Base, new Rectangle(r.X, y + Theme.S(24), r.Width, Theme.S(20)), Theme.Muted, flags);
            TextRenderer.DrawText(g, credit, Theme.Small, new Rectangle(r.X, y + Theme.S(44), r.Width, Theme.S(18)), Theme.Faint, flags);
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
