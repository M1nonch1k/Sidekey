using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sidekey
{
    // Компактная панель у правого края экрана. Если открыть замок в заголовке, панель можно
    // перетащить за заголовок в любое место и растянуть за края — но только от −10 % до +20 % обычного
    // размера: сильнее уменьшенная панель уже тесна для вкладки «Время». Закрытый замок закрепляет панель.
    class MainForm : Form
    {
        public const double Shrink = 0.10, Grow = 0.20;
        static readonly Size BaseSize = new Size(Theme.S(400), Theme.S(660));
        public static readonly Size MinPanel = Scaled(1 - Shrink), MaxPanel = Scaled(1 + Grow);
        static Size Scaled(double k) { return new Size((int)Math.Round(BaseSize.Width * k), (int)Math.Round(BaseSize.Height * k)); }
        int Grip { get { return Theme.S(6); } } // ширина края, за который тянут
        readonly Button lockBtn;
        readonly Panel header;

        public readonly TimePage TimePage = new TimePage();
        public readonly NotesPage NotesPage = new NotesPage();
        public readonly LaunchPage LaunchPage = new LaunchPage();
        public readonly ToolsPage ToolsPage = new ToolsPage();
        public TimerPage TimerPage { get { return ToolsPage.Timer; } }
        public readonly WeatherPage WeatherPage = new WeatherPage();
        public readonly MixerPage MixerPage = new MixerPage();
        public readonly PlayerPage PlayerPage = new PlayerPage();
        readonly Control[] pages;
        readonly TabStrip tabs = new TabStrip(new[] { L.T("tab.time"), L.T("tab.notes"), L.T("tab.launch"), L.T("tab.timer"), L.T("tab.weather"), L.T("tab.mixer"), L.T("tab.player") });
        readonly Button hideBtn;

        public MainForm()
        {
            Text = L.T("app.name");
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            KeyPreview = true;
            Padding = new Padding(1);
            Icon = App.AppIcon;
            MinimumSize = MinPanel;
            MaximumSize = MaxPanel;
            ClientSize = SavedSize();

            header = new Panel { Dock = DockStyle.Top, Height = Theme.S(46), BackColor = Theme.Bg, Padding = Theme.P(16, 0, 8, 0) };
            var title = new Label { Text = L.T("app.name"), Font = Theme.Title, ForeColor = Theme.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Padding = Theme.P(0, 8, 0, 0) };
            lockBtn = Theme.IconBtn("", "", Theme.Bg);
            var settingsBtn = Theme.IconBtn("", L.T("main.settings"), Theme.Bg);
            hideBtn = Theme.IconBtn("", "", Theme.Bg);
            lockBtn.Margin = settingsBtn.Margin = hideBtn.Margin = Padding.Empty;
            lockBtn.Click += (s, e) => SetLocked(!App.Settings.PanelLocked);
            settingsBtn.Click += (s, e) => App.OpenSettings();
            hideBtn.Click += (s, e) => HidePanel();
            buttons.Controls.Add(lockBtn);
            buttons.Controls.Add(settingsBtn);
            buttons.Controls.Add(hideBtn);
            header.Controls.Add(title);
            header.Controls.Add(buttons);
            // Перетаскивание за заголовок, пока замок открыт.
            header.MouseDown += (s, e) => StartDrag(e);
            title.MouseDown += (s, e) => StartDrag(e);

            tabs.Dock = DockStyle.Top;
            tabs.RowCount = 2;
            tabs.Height = Theme.S(34) * tabs.RowCount;

            var content = new Panel { Dock = DockStyle.Fill, Padding = Theme.P(14, 12, 14, 12), BackColor = Theme.Bg };
            pages = new Control[] { TimePage, NotesPage, LaunchPage, ToolsPage, WeatherPage, MixerPage, PlayerPage };
            foreach (var p in pages)
            {
                p.Dock = DockStyle.Fill;
                p.Visible = false;
                content.Controls.Add(p);
            }

            Controls.Add(content);
            Controls.Add(tabs);
            Controls.Add(header);

            tabs.SelectedChanged += (s, e) => ShowPage(tabs.SelectedIndex);
            int last = App.Settings.LastTab;
            tabs.SelectedIndex = last >= 0 && last < pages.Length ? last : 0;
            UpdateHotkeyHint();
            UpdateLock();
        }

        static Size SavedSize()
        {
            var s = App.Settings;
            if (s.PanelWidth <= 0 || s.PanelHeight <= 0) return BaseSize;
            return new Size(Math.Max(MinPanel.Width, Math.Min(MaxPanel.Width, s.PanelWidth)), Math.Max(MinPanel.Height, Math.Min(MaxPanel.Height, s.PanelHeight)));
        }

        public bool Locked { get { return App.Settings.PanelLocked; } }

        public void SetLocked(bool locked)
        {
            App.Settings.PanelLocked = locked;
            if (locked) RememberBounds();
            App.SaveSettings();
            UpdateLock();
        }

        // Открытый замок: рамка цвета акцента шире — за неё можно тянуть, а заголовок двигает панель.
        void UpdateLock()
        {
            bool locked = Locked;
            lockBtn.Text = locked ? "" : "";
            lockBtn.ForeColor = locked ? Theme.Text : Theme.Accent;
            Theme.Tips.SetToolTip(lockBtn, locked ? L.T("main.unlockTip") : L.T("main.lockTip"));
            Padding = locked ? new Padding(1) : new Padding(Grip);
            header.Cursor = locked ? Cursors.Default : Cursors.SizeAll;
            Invalidate();
        }

        void StartDrag(MouseEventArgs e)
        {
            if (Locked || e.Button != MouseButtons.Left) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        }

        // Запомнить, где стоит панель и какого она размера, чтобы показывать её там же.
        void RememberBounds()
        {
            var s = App.Settings;
            s.PanelMoved = true;
            s.PanelX = Left;
            s.PanelY = Top;
            s.PanelWidth = Width;
            s.PanelHeight = Height;
        }

        // Вернуть панель к правому краю и обычному размеру (кнопка в настройках).
        public void ResetPlacement()
        {
            var s = App.Settings;
            s.PanelMoved = false;
            s.PanelWidth = s.PanelHeight = 0;
            s.PanelX = s.PanelY = 0;
            App.SaveSettings();
            Size = BaseSize;
            if (Visible) ShowPanel();
        }

        public void UpdateHotkeyHint()
        {
            Theme.Tips.SetToolTip(hideBtn, L.F("main.hideTip", App.HotkeyText()));
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW: не показывать в Alt+Tab
                cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW: тень вокруг панели
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return App.SelfTesting; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Locked)
                using (var edge = new SolidBrush(Theme.Mix(Theme.Bg, Theme.Accent, 0.35)))
                {
                    e.Graphics.FillRectangle(edge, 0, 0, Width, Padding.Top);
                    e.Graphics.FillRectangle(edge, 0, Height - Padding.Bottom, Width, Padding.Bottom);
                    e.Graphics.FillRectangle(edge, 0, 0, Padding.Left, Height);
                    e.Graphics.FillRectangle(edge, Width - Padding.Right, 0, Padding.Right, Height);
                }
            using (var pen = new Pen(Locked ? Theme.Border : Theme.Accent)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        public void SelectTab(int index) { tabs.SelectedIndex = index; }
        public bool TabsFit() { return tabs.Fits(ClientSize.Width - Padding.Horizontal); }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
            App.Settings.LastTab = index;
            var page = pages[index] as IPage;
            if (page != null && Visible) page.OnPageShown();
        }

        public void ShowPanel()
        {
            var s = App.Settings;
            var size = SavedSize();
            if (s.PanelMoved)
            {
                // Там, где панель оставили; если тот монитор отключили — на ближайшем, целиком на экране.
                var saved = new Rectangle(s.PanelX, s.PanelY, size.Width, size.Height);
                var area = Screen.FromRectangle(saved).WorkingArea;
                int w = Math.Min(size.Width, area.Width), h = Math.Min(size.Height, area.Height);
                int x = Math.Max(area.Left, Math.Min(saved.X, area.Right - w));
                int y = Math.Max(area.Top, Math.Min(saved.Y, area.Bottom - h));
                Bounds = new Rectangle(x, y, w, h);
            }
            else
            {
                var area = Screen.FromPoint(Cursor.Position).WorkingArea;
                int h = Math.Min(size.Height, area.Height - Theme.S(24));
                Bounds = new Rectangle(area.Right - size.Width - Theme.S(12), area.Top + (area.Height - h) / 2, size.Width, h);
            }
            if (!Visible) Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            var page = pages[tabs.SelectedIndex] as IPage;
            if (page != null) page.OnPageShown();
        }

        // Панель остаётся поверх всех окон, пока её не скроют кнопкой, Esc или сочетанием клавиш.
        // Другие окна «поверх всех» (оверлеи, мини-плееры) могут её перекрыть, когда активируются,
        // поэтому, уходя в фон, панель снова встаёт на самый верх, а таймер подстраховывает это.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible) BeginInvoke(new Action(() => { if (Visible) Native.BringToTopmost(Handle); }));
        }

        readonly Timer keepOnTop = new Timer { Interval = 1500 };

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (keepOnTop.Tag == null)
            {
                keepOnTop.Tag = true;
                keepOnTop.Tick += (s, a) =>
                {
                    // Пока открыт свой диалог (настройки и т.п.), его не перекрываем.
                    foreach (Form f in Application.OpenForms) if (f != this && f.Visible && f.Owner == this) return;
                    if (Visible && Native.GetForegroundWindow() != Handle) Native.BringToTopmost(Handle);
                };
            }
            if (Visible) keepOnTop.Start(); else keepOnTop.Stop();
        }

        public void HidePanel()
        {
            NotesPage.Flush();
            Hide();
        }

        public void TogglePanel()
        {
            // Если открыт диалог (настройки и т.п.), просто показываем его.
            foreach (Form f in Application.OpenForms)
                if (f != this && f.Visible && f.Modal) { f.Activate(); return; }
            if (Visible && Native.GetForegroundWindow() == Handle) HidePanel();
            else ShowPanel();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) { App.OnHotkey(m.WParam.ToInt32()); return; }
            if (m.Msg == Program.ShowMessage && Program.ShowMessage != 0) { ShowPanel(); return; }
            if (m.Msg == Native.WM_NCHITTEST && !Locked)
            {
                // Края и углы растягивают панель; пределы задают MinimumSize/MaximumSize.
                var p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                bool l = p.X < Grip, r = p.X >= Width - Grip, t = p.Y < Grip, b = p.Y >= Height - Grip;
                int hit = t && l ? Native.HTTOPLEFT : t && r ? Native.HTTOPRIGHT : b && l ? Native.HTBOTTOMLEFT : b && r ? Native.HTBOTTOMRIGHT
                        : l ? Native.HTLEFT : r ? Native.HTRIGHT : t ? Native.HTTOP : b ? Native.HTBOTTOM : 0;
                if (hit != 0) { m.Result = (IntPtr)hit; return; }
            }
            if (m.Msg == Native.WM_EXITSIZEMOVE && !Locked) { RememberBounds(); App.SaveSettings(); }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { HidePanel(); return true; }
            if (keyData == (Keys.Control | Keys.Tab)) { SelectTab((tabs.SelectedIndex + 1) % pages.Length); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)) { SelectTab((tabs.SelectedIndex + pages.Length - 1) % pages.Length); return true; }
            for (int i = 0; i < pages.Length; i++)
                if (keyData == (Keys.Control | (Keys.D1 + i))) { SelectTab(i); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 только скрывает панель; выход — через меню значка в трее.
            if (e.CloseReason == CloseReason.UserClosing && !App.Exiting)
            {
                e.Cancel = true;
                HidePanel();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
