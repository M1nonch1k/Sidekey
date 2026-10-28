using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Sidekey
{
    // ---------------------------------------------------------------- Время
    class TimePage : Rows, IPage
    {
        readonly ComboBox period = new DarkCombo();
        readonly DateTimePicker from = new DateTimePicker(), to = new DateTimePicker();
        readonly FlowLayoutPanel customRow;
        readonly Label todayValue, periodValue, periodCaption, current, status;
        readonly DarkListView list = new DarkListView { BarColumn = 2 };
        readonly Button pauseBtn, clearBtn;
        readonly Timer refresh = new Timer { Interval = 3000 };

        public TimePage()
        {
            period.Width = Theme.S(190);
            period.Margin = Theme.P(0, 2, 0, 8);
            period.Items.AddRange(new object[] { L.T("time.p.today"), L.T("time.p.yesterday"), L.T("time.p.7"), L.T("time.p.30"), L.T("time.p.all"), L.T("time.p.custom") });
            period.SelectedIndex = 0;
            Auto(Theme.Flow(Theme.Inline(L.T("time.period")), period));

            foreach (var d in new[] { from, to })
            {
                d.Format = DateTimePickerFormat.Short;
                d.Width = Theme.S(118);
                d.Margin = Theme.P(0, 2, 6, 8);
                d.Value = DateTime.Today;
            }
            from.Value = DateTime.Today.AddDays(-6);
            from.ValueChanged += (s, e) => RefreshData();
            to.ValueChanged += (s, e) => RefreshData();
            customRow = Auto(Theme.Flow(Theme.Inline(L.T("time.from")), from, Theme.Inline(L.T("time.to")), to));
            customRow.Visible = false;

            var cards = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = Theme.Bg, Margin = Theme.P(0, 0, 0, 10) };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Label dummy;
            var todayCard = Theme.Card(L.T("time.todayCard"), out todayValue, out dummy);
            var periodCard = Theme.Card(L.T("time.periodCard"), out periodValue, out periodCaption);
            todayCard.Margin = Theme.P(0, 0, 5, 0);
            periodCard.Margin = Theme.P(5, 0, 0, 0);
            cards.Controls.Add(todayCard, 0, 0);
            cards.Controls.Add(periodCard, 1, 0);
            Fixed(cards, Theme.S(80));

            list.Columns.Add(L.T("time.col.app"));
            list.Columns.Add(L.T("time.col.time"));
            list.Columns.Add(L.T("time.col.share"));
            list.Resize += (s, e) => FitColumns();
            Fill(list);

            current = Auto(Theme.Wrap("", Theme.Small, Theme.Muted));
            current.Margin = Theme.P(0, 6, 0, 4);

            status = Theme.Inline("");
            pauseBtn = Theme.Btn(L.T("time.pause"));
            clearBtn = Theme.Btn(L.T("time.clear"));
            pauseBtn.Click += (s, e) => App.SetTracking(!App.Settings.TrackingEnabled);
            clearBtn.Click += (s, e) => App.ClearStats();
            Auto(Theme.Flow(status, pauseBtn, clearBtn));

            Auto(Theme.Wrap(L.T("time.privacy"), Theme.Small, Theme.Muted));

            period.SelectedIndexChanged += (s, e) =>
            {
                customRow.Visible = period.SelectedIndex == 5;
                RefreshData();
            };
            refresh.Tick += (s, e) => { if (Visible) RefreshData(); };
            VisibleChanged += (s, e) => { if (Visible) refresh.Start(); else refresh.Stop(); };
        }

        // Крупный шрифт, а если текст не помещается в карточку — шрифт поменьше.
        static void SetCardValue(Label label, string text)
        {
            label.Text = text;
            bool fits = label.Width <= 0 || TextRenderer.MeasureText(text, Theme.Big).Width <= label.Width;
            label.Font = fits ? Theme.Big : Theme.Title;
        }

        void FitColumns()
        {
            int w = list.ClientSize.Width;
            if (w < Theme.S(150)) return; // таблица ещё не разложена
            // Ширина колонки «Время» — по самому длинному значению (в некоторых языках «ч»/«мин» длиннее).
            int timeW = Theme.S(92), pctW = Theme.S(96);
            foreach (ListViewItem item in list.Items)
                timeW = Math.Max(timeW, TextRenderer.MeasureText(item.SubItems[1].Text, Theme.Base).Width + Theme.S(20));
            timeW = Math.Min(timeW, w / 2);
            list.Columns[1].Width = timeW;
            list.Columns[2].Width = pctW;
            list.Columns[0].Width = Math.Max(Theme.S(60), w - timeW - pctW - Theme.S(6));
            // Колонки всегда умещаются по ширине; тёмная тема иногда оставляет лишнюю горизонтальную полосу.
            if (list.IsHandleCreated) Native.ShowScrollBar(list.Handle, Native.SB_HORZ, false);
        }

        void GetRange(out DateTime f, out DateTime t, out string caption)
        {
            DateTime today = DateTime.Today;
            switch (period.SelectedIndex)
            {
                case 1: f = t = today.AddDays(-1); caption = L.T("time.p.yesterday"); break;
                case 2: f = today.AddDays(-6); t = today; caption = L.T("time.p.7"); break;
                case 3: f = today.AddDays(-29); t = today; caption = L.T("time.p.30"); break;
                case 4: f = DateTime.MinValue; t = DateTime.MaxValue; caption = L.T("time.p.all"); break;
                case 5:
                    f = from.Value.Date; t = to.Value.Date;
                    if (f > t) { var x = f; f = t; t = x; }
                    caption = f.ToString("d", L.Culture) + " – " + t.ToString("d", L.Culture);
                    break;
                default: f = t = today; caption = L.T("time.p.today"); break;
            }
        }

        public void OnPageShown() { RefreshData(); }

        public void RefreshData()
        {
            DateTime f, t;
            string caption;
            GetRange(out f, out t, out caption);
            var today = App.Tracker.GetStats(DateTime.Today, DateTime.Today);
            var stats = App.Tracker.GetStats(f, t);
            SetCardValue(todayValue, Fmt.Duration(today.Total));
            SetCardValue(periodValue, Fmt.Duration(stats.Total));
            periodCaption.Text = caption;

            int top = list.TopItem != null ? list.TopItem.Index : 0;
            string selectedKey = list.SelectedItems.Count > 0 ? (string)list.SelectedItems[0].Name : null;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var r in stats.Rows)
            {
                double pct = stats.Total > 0 ? r.Seconds * 100.0 / stats.Total : 0;
                var item = new ListViewItem(r.Name) { Name = r.Key, Tag = pct, ToolTipText = r.Name + " (" + r.Key + ")" };
                item.SubItems.Add(Fmt.Duration(r.Seconds));
                item.SubItems.Add(pct < 1 && pct > 0 ? "<1%" : pct.ToString("0") + "%");
                if (r.Key == selectedKey) item.Selected = true;
                list.Items.Add(item);
            }
            list.EndUpdate();
            FitColumns();
            if (list.Items.Count == 0)
                current.Text = L.T("time.noData");
            if (top > 0 && top < list.Items.Count) list.TopItem = list.Items[top];

            bool on = App.Settings.TrackingEnabled;
            status.Text = on ? L.T("time.on") : L.T("time.off");
            status.ForeColor = on ? Theme.Ok : Theme.Warn;
            pauseBtn.Text = on ? L.T("time.pause") : L.T("time.resume");

            string now;
            switch (App.Tracker.State)
            {
                case "active": now = L.F("time.now.active", App.Tracker.CurrentName); break;
                case "idle": now = L.F("time.now.idle", App.Settings.IdleMinutes); break;
                case "locked": now = L.T("time.now.locked"); break;
                case "paused": now = L.T("time.now.paused"); break;
                default: now = ""; break;
            }
            if (list.Items.Count > 0 || now != "") current.Text = now;
        }
    }

    // ---------------------------------------------------------------- Заметки
    class NotesPage : Rows, IPage
    {
        readonly TwoLineList list = new TwoLineList();
        readonly TextBox editor;
        readonly Label status;
        readonly Button deleteBtn;
        readonly Timer saveTimer = new Timer { Interval = 700 };
        Note current;
        bool loading;

        public NotesPage()
        {
            var addBtn = Theme.Btn(L.T("notes.new"), true);
            deleteBtn = Theme.Btn(L.T("common.delete"));
            addBtn.Click += (s, e) => AddNote();
            deleteBtn.Click += (s, e) => DeleteNote();
            Auto(Theme.Flow(addBtn, deleteBtn));

            list.First = o => NoteTitle((Note)o);
            list.Second = o => NoteSubtitle((Note)o);
            list.Margin = Theme.P(0, 2, 0, 8);
            list.SelectedIndexChanged += (s, e) => Select(list.SelectedItem as Note);
            Fill(list, 38);

            editor = new TextBox
            {
                Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
                BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f), Margin = Padding.Empty
            };
            editor.HandleCreated += (s, e) => Native.DarkTheme(editor.Handle, "DarkMode_Explorer");
            editor.TextChanged += (s, e) => Edited();
            // Небольшой отступ вокруг текста.
            var editorBox = new Panel { BackColor = Theme.Surface, Padding = Theme.P(10, 8, 4, 8), Margin = Padding.Empty };
            editor.Dock = DockStyle.Fill;
            editorBox.Controls.Add(editor);
            Fill(editorBox, 62);

            status = Auto(Theme.Wrap("", Theme.Small, Theme.Muted));
            status.Margin = Theme.P(0, 6, 0, 0);

            saveTimer.Tick += (s, e) => Flush();
            Reload(null);
        }

        static string NoteTitle(Note n)
        {
            foreach (var line in (n.Text ?? "").Split('\n'))
            {
                string t = line.Trim();
                if (t.Length > 0) return t.Length > 80 ? t.Substring(0, 80) : t;
            }
            return L.T("notes.untitled");
        }

        static string NoteSubtitle(Note n)
        {
            string when = "";
            DateTime d;
            if (DateTime.TryParseExact(n.Modified ?? "", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                when = d.Date == DateTime.Today ? L.F("notes.today", d.ToString("t", L.Culture)) : d.Date == DateTime.Today.AddDays(-1) ? L.F("notes.yesterday", d.ToString("t", L.Culture)) : d.ToString("g", L.Culture);
            var lines = (n.Text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1).Take(1).ToList();
            return lines.Count > 0 ? when + "  ·  " + lines[0] : when;
        }

        static string Now() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }

        public void OnPageShown()
        {
            if (current != null) editor.Focus();
        }

        public void Reload(Note select)
        {
            var notes = App.Notes.Notes.OrderByDescending(n => n.Modified ?? "").ToList();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var n in notes) list.Items.Add(n);
            list.EndUpdate();
            if (select != null && list.Items.Contains(select)) list.SelectedItem = select;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            else Select(null);
        }

        void Select(Note note)
        {
            Flush();
            current = note;
            loading = true;
            editor.Text = note != null ? (note.Text ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n") : "";
            editor.ReadOnly = note == null;
            editor.Select(editor.TextLength, 0);
            loading = false;
            deleteBtn.Enabled = note != null;
            status.Text = note == null
                ? L.T("notes.empty")
                : L.T("notes.autosave");
        }

        void Edited()
        {
            if (loading || current == null) return;
            current.Text = editor.Text;
            current.Modified = Now();
            list.Invalidate();
            saveTimer.Stop();
            saveTimer.Start();
            status.Text = L.T("notes.saving");
        }

        public void Flush()
        {
            if (!saveTimer.Enabled) return;
            saveTimer.Stop();
            if (App.SaveNotes()) status.Text = L.T("notes.saved");
        }

        void AddNote()
        {
            Flush();
            var note = new Note { Id = Guid.NewGuid().ToString("N"), Text = "", Modified = Now() };
            App.Notes.Notes.Add(note);
            App.SaveNotes();
            Reload(note);
            editor.Focus();
        }

        void DeleteNote()
        {
            if (current == null) return;
            if (!Dlg.Ask(L.F("notes.deleteAsk", NoteTitle(current)), L.T("common.delete"))) return;
            saveTimer.Stop();
            App.Notes.Notes.Remove(current);
            current = null;
            App.SaveNotes();
            Reload(null);
        }
    }

    // ---------------------------------------------------------------- Быстрый запуск
    class LaunchPage : Rows, IPage
    {
        readonly FlowLayoutPanel cards;
        readonly Label status;

        public LaunchPage()
        {
            var addBtn = Theme.Btn(L.T("lp.new"), true);
            addBtn.Click += (s, e) => Edit(null);
            Auto(Theme.Flow(addBtn));

            cards = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                BackColor = Theme.Bg, Margin = Theme.P(0, 2, 0, 6), Padding = Padding.Empty
            };
            cards.HandleCreated += (s, e) => Native.DarkTheme(cards.Handle, "DarkMode_Explorer");
            cards.Resize += (s, e) => FitCards();
            Fill(cards);

            status = Auto(Theme.Wrap("", Theme.Small, Theme.Muted));
            Auto(Theme.Wrap(L.T("lp.hint"), Theme.Small, Theme.Muted));
            Rebuild();
        }

        public void OnPageShown() { }

        public void Rebuild()
        {
            cards.SuspendLayout();
            while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
            if (App.Launch.Sets.Count == 0)
            {
                var empty = Theme.Lbl(L.T("lp.empty"), Theme.Base, Theme.Muted);
                empty.AutoSize = false;
                empty.Height = Theme.S(60);
                cards.Controls.Add(empty);
            }
            foreach (var set in App.Launch.Sets) cards.Controls.Add(Card(set));
            cards.ResumeLayout();
            FitCards();
        }

        void FitCards()
        {
            int w = cards.ClientSize.Width - Theme.S(2);
            foreach (Control c in cards.Controls) c.Width = w;
        }

        Control Card(LaunchSet set)
        {
            var card = new Panel { BackColor = Theme.Surface, Height = Theme.S(62), Padding = Theme.P(12, 8, 6, 6), Margin = Theme.P(0, 0, 0, 8) };
            var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Padding = Theme.P(0, 6, 0, 0) };
            var run = Theme.Btn(L.T("lp.run"), true);
            run.Margin = Theme.P(0, 0, 4, 0);
            var edit = Theme.IconBtn("", L.T("lp.editTip"), Theme.Surface);
            var del = Theme.IconBtn("", L.T("lp.deleteTip"), Theme.Surface);
            edit.Margin = del.Margin = Padding.Empty;
            right.Controls.AddRange(new Control[] { run, edit, del });

            string title = set.Name + (set.HasHotkey() ? "   ·   " + Fmt.Hotkey(set.HotkeyMods, set.HotkeyVk) : "");
            var name = new Label { Text = title, Font = Theme.Bold, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = Theme.S(24), AutoEllipsis = true, UseMnemonic = false };
            string summary = set.Items.Count == 0 ? L.T("lp.emptySet") : string.Join(", ", set.Items.Select(i => i.Title()));
            var sub = new Label { Text = summary, Font = Theme.Small, ForeColor = Theme.Muted, Dock = DockStyle.Top, Height = Theme.S(20), AutoEllipsis = true, UseMnemonic = false };
            Theme.Tips.SetToolTip(sub, string.Join("\n", set.Items.Select(i => i.KindLabel() + ": " + i.Path + (string.IsNullOrEmpty(i.Args) ? "" : " " + i.Args))));
            card.Controls.Add(sub);
            card.Controls.Add(name);
            card.Controls.Add(right);

            run.Click += (s, e) => Run(set);
            edit.Click += (s, e) => Edit(set);
            del.Click += (s, e) =>
            {
                if (!Dlg.Ask(L.F("lp.deleteAsk", set.Name), L.T("common.delete"))) return;
                App.Launch.Sets.Remove(set);
                App.SaveLaunch();
                App.RegisterLaunchHotkeys();
                Rebuild();
            };
            return card;
        }

        void Edit(LaunchSet set)
        {
            using (var f = new LaunchEditForm(set))
            {
                if (f.ShowDialog(App.Owner) != DialogResult.OK) return;
                int i = set == null ? -1 : App.Launch.Sets.IndexOf(set);
                if (i >= 0) App.Launch.Sets[i] = f.Result;
                else App.Launch.Sets.Add(f.Result);
                App.SaveLaunch();
                var failed = App.RegisterLaunchHotkeys();
                Rebuild();
                if (failed.Count > 0)
                    Dlg.Info(L.F("lp.hotkeyFailed", string.Join(", ", failed)));
            }
        }

        // Запуск кнопки: по нажатию «Запустить» или по её сочетанию клавиш.
        public void Run(LaunchSet set)
        {
            if (LaunchRun.Current != null)
            {
                status.ForeColor = Theme.Warn;
                status.Text = L.T("lp.busy");
                return;
            }
            var run = new LaunchRun(set,
                text =>
                {
                    status.ForeColor = Theme.Muted;
                    status.Text = L.F("lp.progress", set.Name, text);
                    App.SetTrayStatus(text);
                },
                (started, errors) =>
                {
                    App.SetTrayStatus(null);
                    status.ForeColor = errors.Count == 0 ? Theme.Ok : Theme.Warn;
                    status.Text = L.F("lp.result", set.Name, started, set.Items.Count);
                    if (errors.Count > 0)
                        Dlg.Info(L.F("lp.errors", string.Join("\n• ", errors)), L.T("tab.launch"));
                    else
                        App.Main.HidePanel();
                });
            // С задержками запуск идёт в фоне — панель можно сразу убрать.
            if (set.Items.Any(i => i.Delay > 0)) App.Main.HidePanel();
            run.Start();
        }
    }

    // ---------------------------------------------------------------- Таймер
    class TimerPage : Rows
    {
        readonly Label display, state;
        readonly NumericUpDown minutes, seconds;
        readonly Button startBtn, resetBtn;
        readonly Timer tick = new Timer { Interval = 200 };
        readonly Stopwatch watch = new Stopwatch();
        TimeSpan total;

        public bool Finished { get; private set; }

        public TimerPage()
        {
            display = Fixed(new Label { Font = Theme.Huge, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false }, Theme.S(96));
            state = Fixed(new Label { Font = Theme.Base, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false }, Theme.S(28));

            minutes = Theme.Number(0, 999, App.Settings.TimerMinutes);
            seconds = Theme.Number(0, 59, App.Settings.TimerSeconds);
            minutes.ValueChanged += (s, e) => InputsChanged();
            seconds.ValueChanged += (s, e) => InputsChanged();
            Centered(Theme.Flow(Theme.Inline(L.T("timer.minutes")), minutes, Theme.Inline("  " + L.T("timer.seconds")), seconds));

            var presets = new List<Control>();
            foreach (int m in new[] { 1, 5, 10, 25, 60 })
            {
                var b = Theme.Btn(L.F("timer.preset", m));
                b.Padding = Theme.P(4, 0, 4, 0);
                int value = m;
                b.Click += (s, e) => Preset(value);
                presets.Add(b);
            }
            Centered(Theme.Flow(presets.ToArray()));

            startBtn = Theme.Btn(L.T("timer.start"), true);
            startBtn.MinimumSize = new Size(Theme.S(120), Theme.S(36));
            resetBtn = Theme.Btn(L.T("timer.reset"));
            resetBtn.MinimumSize = new Size(Theme.S(120), Theme.S(36));
            startBtn.Click += (s, e) => { if (watch.IsRunning) Pause(); else Start(); };
            resetBtn.Click += (s, e) => Reset();
            var controls = Centered(Theme.Flow(startBtn, resetBtn));
            controls.Margin = Theme.P(0, 10, 0, 14);

            Auto(Theme.Wrap(L.T("timer.hint"), Theme.Small, Theme.Muted));

            tick.Tick += (s, e) => Tick();
            Reset();
        }

        FlowLayoutPanel Centered(FlowLayoutPanel f)
        {
            f.Dock = DockStyle.None;
            f.Anchor = AnchorStyles.None;
            f.WrapContents = false;
            f.Margin = Theme.P(0, 4, 0, 4);
            return Auto(f);
        }

        TimeSpan InputTime() { return TimeSpan.FromSeconds((double)(minutes.Value * 60 + seconds.Value)); }
        bool Paused { get { return !watch.IsRunning && watch.ElapsedTicks > 0; } }

        void InputsChanged()
        {
            if (watch.IsRunning || Paused) return;
            Finished = false;
            display.Text = Fmt.Clock(InputTime());
            SetState(L.T("timer.ready"), Theme.Muted);
        }

        void Preset(int m)
        {
            if (watch.IsRunning) return;
            if (Paused) Reset();
            minutes.Value = m;
            seconds.Value = 0;
        }

        void SetState(string text, Color color) { state.Text = text; state.ForeColor = color; }

        public void Start()
        {
            if (watch.ElapsedTicks == 0)
            {
                total = InputTime();
                if (total.TotalSeconds < 1) { SetState(L.T("timer.zero"), Theme.Warn); return; }
                App.Settings.TimerMinutes = (int)minutes.Value;
                App.Settings.TimerSeconds = (int)seconds.Value;
            }
            Finished = false;
            watch.Start();
            tick.Start();
            minutes.Enabled = seconds.Enabled = false;
            startBtn.Text = L.T("timer.pause");
            SetState(L.T("timer.running"), Theme.Muted);
            Tick();
        }

        void Pause()
        {
            watch.Stop();
            tick.Stop();
            startBtn.Text = L.T("timer.resume");
            SetState(L.T("timer.paused"), Theme.Warn);
            App.SetTrayStatus(L.F("timer.trayPaused", Fmt.Clock(total - watch.Elapsed)));
        }

        void Reset()
        {
            watch.Reset();
            tick.Stop();
            minutes.Enabled = seconds.Enabled = true;
            startBtn.Text = L.T("timer.start");
            Finished = false;
            display.Text = Fmt.Clock(InputTime());
            SetState(L.T("timer.ready"), Theme.Muted);
            App.SetTrayStatus(null);
        }

        void Tick()
        {
            var left = total - watch.Elapsed;
            if (left <= TimeSpan.Zero) { Finish(); return; }
            display.Text = Fmt.Clock(left);
            App.SetTrayStatus(L.F("timer.tray", Fmt.Clock(left)));
        }

        void Finish()
        {
            watch.Reset();
            tick.Stop();
            minutes.Enabled = seconds.Enabled = true;
            startBtn.Text = L.T("timer.start");
            display.Text = "00:00";
            SetState(L.T("timer.done"), Theme.Accent);
            Finished = true;
            App.SetTrayStatus(null);
            App.Notify(L.T("timer.doneTitle"), L.F("timer.doneText", Fmt.Clock(total)));
            App.PlayAlarm();
        }
    }

    // ---------------------------------------------------------------- Погода
    class WeatherPage : Panel, IPage
    {
        readonly Rows content = new Rows(), empty = new Rows(), days = new Rows();
        readonly Label city, temp, desc, details, status;
        readonly Button refreshBtn;
        WeatherInfo last;
        bool loading;

        public WeatherPage()
        {
            BackColor = Theme.Bg;

            city = Theme.Lbl("", Theme.Title, Theme.Text);
            city.AutoEllipsis = true;
            var change = Theme.Btn(L.T("wp.change"));
            change.Click += (s, e) => App.PickCity(App.Owner);
            content.Auto(Theme.Line(city, change));

            temp = content.Auto(Theme.Lbl("", Theme.Huge, Theme.Text));
            temp.Margin = Theme.P(0, 4, 0, 0);
            desc = content.Auto(Theme.Lbl("", Theme.Title, Theme.Text));
            details = content.Auto(Theme.Wrap("", Theme.Base, Theme.Muted));
            details.Margin = Theme.P(0, 2, 0, 12);

            days.AutoSize = true;
            days.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            days.Margin = Theme.P(0, 0, 0, 10);
            content.Auto(days);

            refreshBtn = Theme.Btn(L.T("wp.refresh"));
            refreshBtn.Click += (s, e) => LoadWeather(true);
            content.Auto(refreshBtn);
            status = content.Auto(Theme.Wrap("", Theme.Small, Theme.Muted));
            content.Auto(Theme.Wrap(L.T("wp.source"), Theme.Small, Theme.Muted));

            empty.Auto(Theme.Lbl(L.T("wp.noCity"), Theme.Title, Theme.Text));
            empty.Auto(Theme.Wrap(L.T("wp.noCityHint"), Theme.Base, Theme.Muted));
            var pick = Theme.Btn(L.T("wp.pick"), true);
            pick.Click += (s, e) => App.PickCity(App.Owner);
            empty.Auto(pick);

            Controls.Add(content);
            Controls.Add(empty);
            UpdateMode();
        }

        void UpdateMode()
        {
            bool has = App.Settings.HasCity();
            content.Visible = has;
            empty.Visible = !has;
            if (has) city.Text = App.Settings.CityLabel;
        }

        public void CityChanged()
        {
            last = null;
            temp.Text = desc.Text = details.Text = "";
            days.ClearRows();
            UpdateMode();
            LoadWeather(true);
        }

        public void OnPageShown()
        {
            UpdateMode();
            LoadWeather(false);
        }

        public bool IsLoading { get { return loading; } }

        void LoadWeather(bool force)
        {
            if (!App.Settings.HasCity() || loading) return;
            if (!force && last != null && (DateTime.Now - last.Fetched).TotalMinutes < 15) return;
            loading = true;
            refreshBtn.Enabled = false;
            status.ForeColor = Theme.Muted;
            status.Text = L.T("wp.loading");
            double lat = App.Settings.Lat, lon = App.Settings.Lon;
            Background.Run(this, () => Weather.Fetch(lat, lon), w =>
            {
                loading = false;
                refreshBtn.Enabled = true;
                ShowData(w);
            }, ex =>
            {
                loading = false;
                refreshBtn.Enabled = true;
                status.ForeColor = Theme.Warn;
                status.Text = Weather.Explain(ex) + (last != null ? " " + L.F("wp.stale", last.Fetched.ToString("t", L.Culture)) : "");
            });
        }

        public void ShowData(WeatherInfo w)
        {
            last = w;
            temp.Text = Weather.Deg(w.Temp);
            desc.Text = Weather.Describe(w.Code);
            details.Text = L.F("wp.details", Weather.Deg(w.Feels), Math.Round(w.Humidity), Math.Round(w.Wind, 1));
            days.ClearRows();
            var ru = L.Culture;
            foreach (var d in w.Days)
            {
                string name = d.Date == DateTime.Today ? L.T("wp.today") : d.Date == DateTime.Today.AddDays(1) ? L.T("wp.tomorrow") : ru.TextInfo.ToTitleCase(ru.DateTimeFormat.GetDayName(d.Date.DayOfWeek));
                var left = Theme.Lbl(name, Theme.Base, Theme.Text);
                var right = Theme.Lbl(Weather.Deg(d.Min) + " … " + Weather.Deg(d.Max) + ",  " + Weather.Describe(d.Code), Theme.Base, Theme.Muted);
                var line = Theme.Line(left, right);
                line.Margin = Theme.P(0, 0, 0, 4);
                days.Auto(line);
            }
            status.ForeColor = Theme.Muted;
            status.Text = L.F("wp.updated", w.Fetched.ToString("t", L.Culture));
        }
    }
}
