using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sidekey
{
    // Компактная панель у правого края экрана.
    class MainForm : Form
    {
        public readonly TimePage TimePage = new TimePage();
        public readonly NotesPage NotesPage = new NotesPage();
        public readonly LaunchPage LaunchPage = new LaunchPage();
        public readonly TimerPage TimerPage = new TimerPage();
        public readonly WeatherPage WeatherPage = new WeatherPage();
        readonly Control[] pages;
        readonly TabStrip tabs = new TabStrip(new[] { L.T("tab.time"), L.T("tab.notes"), L.T("tab.launch"), L.T("tab.timer"), L.T("tab.weather") });
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
            ClientSize = new Size(Theme.S(400), Theme.S(660));

            var header = new Panel { Dock = DockStyle.Top, Height = Theme.S(46), BackColor = Theme.Bg, Padding = Theme.P(16, 0, 8, 0) };
            var title = new Label { Text = L.T("app.name"), Font = Theme.Title, ForeColor = Theme.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Padding = Theme.P(0, 8, 0, 0) };
            var settingsBtn = Theme.IconBtn("", L.T("main.settings"), Theme.Bg);
            hideBtn = Theme.IconBtn("", "", Theme.Bg);
            settingsBtn.Margin = hideBtn.Margin = Padding.Empty;
            settingsBtn.Click += (s, e) => App.OpenSettings();
            hideBtn.Click += (s, e) => HidePanel();
            buttons.Controls.Add(settingsBtn);
            buttons.Controls.Add(hideBtn);
            header.Controls.Add(title);
            header.Controls.Add(buttons);

            tabs.Dock = DockStyle.Top;
            tabs.Height = Theme.S(38);

            var content = new Panel { Dock = DockStyle.Fill, Padding = Theme.P(14, 12, 14, 12), BackColor = Theme.Bg };
            pages = new Control[] { TimePage, NotesPage, LaunchPage, TimerPage, WeatherPage };
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
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        public void SelectTab(int index) { tabs.SelectedIndex = index; }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
            App.Settings.LastTab = index;
            var page = pages[index] as IPage;
            if (page != null && Visible) page.OnPageShown();
        }

        public void ShowPanel()
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int h = Math.Min(Theme.S(660), area.Height - Theme.S(24));
            Bounds = new Rectangle(area.Right - Width - Theme.S(12), area.Top + (area.Height - h) / 2, Width, h);
            if (!Visible) Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            var page = pages[tabs.SelectedIndex] as IPage;
            if (page != null) page.OnPageShown();
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
