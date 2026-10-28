using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Sidekey
{
    // ---------------------------------------------------------------- Время
    class TimePage : Rows, IPage
    {
        readonly ChipGroup period = new ChipGroup(L.T("time.p.today"), L.T("time.p.yesterday"), L.T("time.p.7"), L.T("time.p.30"), L.T("time.p.all"), L.T("time.p.custom"));
        readonly DateBox from = new DateBox(), to = new DateBox();
        readonly FlowLayoutPanel customRow;
        readonly Label todayValue, periodValue, periodCaption, current, status;
        readonly DarkListView list = new DarkListView { BarColumn = 2 };
        readonly Button pauseBtn, clearBtn;
        readonly Timer refresh = new Timer { Interval = 3000 };

        public TimePage()
        {
            period.Margin = Theme.P(0, 0, 0, 4);
            period.SelectedIndex = 0;
            Auto(period);

            foreach (var d in new[] { from, to })
            {
                d.Width = Theme.S(124);
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
            Fixed(cards, Theme.S(92));

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
        readonly NumberBox minutes, seconds;
        readonly Button startBtn, resetBtn;
        readonly Timer tick = new Timer { Interval = 200 };
        readonly Stopwatch watch = new Stopwatch();
        TimeSpan total;

        public bool Finished { get; private set; }

        public TimerPage()
        {
            display = Fixed(new Label { Font = Theme.Huge, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false }, Theme.S(72));
            state = Fixed(new Label { Font = Theme.Base, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false }, Theme.S(24));

            minutes = Theme.Number(0, 999, App.Settings.TimerMinutes);
            seconds = Theme.Number(0, 59, App.Settings.TimerSeconds);
            minutes.ValueChanged += (s, e) => InputsChanged();
            seconds.ValueChanged += (s, e) => InputsChanged();
            // Короткие подписи после полей, чтобы строка помещалась и в уменьшенной панели.
            Theme.Tips.SetToolTip(minutes, L.T("timer.minutes").TrimEnd(':', ' ', '：'));
            Theme.Tips.SetToolTip(seconds, L.T("timer.seconds").TrimEnd(':', ' ', '：'));
            Centered(Theme.Flow(minutes, Theme.Inline(L.T("common.min") + "   "), seconds, Theme.Inline(L.T("common.sec"))));

            var presets = new List<Control>();
            foreach (int m in new[] { 1, 5, 10, 25, 60 })
            {
                var b = Theme.Btn(L.F("timer.preset", m));
                b.Padding = Theme.P(4, 0, 4, 0);
                ((SoftButton)b).Radius = Theme.S(14); // мягкие «таблетки», как выбор периода на вкладке «Время»
                int value = m;
                b.Click += (s, e) => Preset(value);
                presets.Add(b);
            }
            // Пять кнопок делят ширину поровну — помещаются и при уменьшенной панели.
            var presetRow = new TableLayoutPanel { ColumnCount = presets.Count, RowCount = 1, Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = Theme.P(0, 4, 0, 4), Padding = Padding.Empty, Height = Theme.S(34) };
            foreach (var b in presets)
            {
                presetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / presets.Count));
                b.AutoSize = false;
                b.Dock = DockStyle.Fill;
                b.Margin = Theme.P(2, 0, 2, 0);
                presetRow.Controls.Add(b);
            }
            Fixed(presetRow, Theme.S(38));

            startBtn = Theme.Btn(L.T("timer.start"), true);
            startBtn.MinimumSize = new Size(Theme.S(120), Theme.S(34));
            resetBtn = Theme.Btn(L.T("timer.reset"));
            resetBtn.MinimumSize = new Size(Theme.S(120), Theme.S(34));
            startBtn.Click += (s, e) => { if (watch.IsRunning) Pause(); else Start(); };
            resetBtn.Click += (s, e) => Reset();
            var controls = Centered(Theme.Flow(startBtn, resetBtn));
            controls.Margin = Theme.P(0, 6, 0, 4);

            Theme.Tips.SetToolTip(startBtn, L.T("timer.hint"));
            AutoSize = true; // высота по содержимому: под таймером на той же подвкладке — секундомер
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

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

    // ---------------------------------------------------------------- Секундомер
    class StopwatchPanel : Rows
    {
        readonly Label display;
        readonly Button startBtn, lapBtn, resetBtn;
        readonly LapList laps = new LapList();
        readonly Timer tick = new Timer { Interval = 50 };
        readonly Stopwatch watch = new Stopwatch();
        TimeSpan lastLap;

        public StopwatchPanel()
        {
            var title = Auto(Theme.Lbl(L.T("sw.title"), Theme.Bold, Theme.Text));
            title.Margin = Theme.P(0, 8, 0, 0);
            display = Fixed(new Label { Font = new Font("Segoe UI Light", 26f), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, UseMnemonic = false }, Theme.S(50));

            startBtn = Theme.Btn(L.T("timer.start"), true);
            lapBtn = Theme.Btn(L.T("sw.lap"));
            resetBtn = Theme.Btn(L.T("timer.reset"));
            foreach (var b in new[] { startBtn, lapBtn, resetBtn }) b.MinimumSize = new Size(Theme.S(96), Theme.S(32));
            startBtn.Click += (s, e) => { if (watch.IsRunning) Pause(); else Start(); };
            lapBtn.Click += (s, e) => Lap();
            resetBtn.Click += (s, e) => Reset();
            var buttons = Theme.Flow(startBtn, lapBtn, resetBtn);
            buttons.Dock = DockStyle.None;
            buttons.Anchor = AnchorStyles.None;
            buttons.WrapContents = false;
            buttons.Margin = Theme.P(0, 2, 0, 4);
            Auto(buttons);
            Fill(laps);

            tick.Tick += (s, e) => ShowValue();
            Reset();
        }

        public TimeSpan Elapsed { get { return watch.Elapsed; } }
        public bool Running { get { return watch.IsRunning; } }
        public int LapCount { get { return laps.Items.Count; } }

        public static string Format(TimeSpan t)
        {
            int cs = (int)(t.Ticks / TimeSpan.TicksPerMillisecond / 10) % 100;
            return (t.TotalHours >= 1 ? ((int)t.TotalHours) + ":" + t.Minutes.ToString("00") : t.Minutes.ToString("00")) + ":" + t.Seconds.ToString("00") + "." + cs.ToString("00");
        }

        void ShowValue() { display.Text = Format(watch.Elapsed); }

        void UpdateButtons()
        {
            startBtn.Text = watch.IsRunning ? L.T("timer.pause") : watch.ElapsedTicks > 0 ? L.T("timer.resume") : L.T("timer.start");
            lapBtn.Enabled = watch.IsRunning;
            resetBtn.Enabled = !watch.IsRunning && watch.ElapsedTicks > 0;
        }

        public void Start() { watch.Start(); tick.Start(); UpdateButtons(); }
        public void Pause() { watch.Stop(); tick.Stop(); ShowValue(); UpdateButtons(); }

        public void Reset()
        {
            watch.Reset();
            tick.Stop();
            lastLap = TimeSpan.Zero;
            laps.Items.Clear();
            ShowValue();
            UpdateButtons();
        }

        // Новый круг сверху: номер, время круга и общее время.
        public void Lap()
        {
            var now = watch.Elapsed;
            laps.Items.Insert(0, new[] { L.F("sw.lapN", laps.Items.Count + 1), "+" + Format(now - lastLap), Format(now) });
            lastLap = now;
        }
    }

    // Список кругов: слева номер, справа время круга и общее время.
    class LapList : ListBox
    {
        public LapList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Theme.S(26);
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            IntegralHeight = false;
            Font = Theme.Base;
            SelectionMode = SelectionMode.None;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTheme(Handle, "DarkMode_Explorer");
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var parts = (string[])Items[e.Index];
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, e.Bounds);
            using (var sep = new SolidBrush(Theme.Bg)) g.FillRectangle(sep, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Width, 1);
            var r = new Rectangle(e.Bounds.X + Theme.S(10), e.Bounds.Y, e.Bounds.Width - Theme.S(20), e.Bounds.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, parts[0], Theme.Base, r, Theme.Muted, flags | TextFormatFlags.Left);
            TextRenderer.DrawText(g, parts[2], Theme.Base, r, Theme.Text, flags | TextFormatFlags.Right);
            var mid = new Rectangle(r.X, r.Y, r.Width - Theme.S(100), r.Height);
            TextRenderer.DrawText(g, parts[1], Theme.Base, mid, Theme.Muted, flags | TextFormatFlags.Right);
        }
    }

    // ---------------------------------------------------------------- Калькулятор
    // Логика отдельно от окна, чтобы её можно было проверить. Умножение и деление выполняются
    // раньше сложения и вычитания, как на iPhone: 2 + 3 × 4 = 14.
    class Calculator
    {
        readonly List<object> tokens = new List<object>(); // числа (decimal) и знаки (char)
        string entry = "";
        bool done, error;
        decimal last;

        public const char Plus = '+', Minus = '−', Times = '×', Divide = '÷';
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        // Большая строка: вводимое число или результат.
        public string Display
        {
            get
            {
                if (error) return L.T("calc.error");
                if (entry != "") return Local(entry);
                // Только что нажат знак: показываем последнее введённое число.
                for (int i = tokens.Count - 1; i >= 0; i--)
                    if (tokens[i] is decimal) return Local(Text((decimal)tokens[i]));
                return Local(Text(last));
            }
        }

        // Маленькая строка сверху: выражение целиком.
        public string Expression { get; private set; }

        public Calculator() { Expression = ""; }

        public void Press(string key)
        {
            if (error && key != "C") Clear();
            switch (key)
            {
                case "C": Clear(); return;
                case "⌫":
                    if (done) return;
                    if (entry.Length > 0) entry = entry.Substring(0, entry.Length - 1);
                    if (entry == "-") entry = "";
                    break;
                case ".":
                    if (done) { tokens.Clear(); entry = ""; done = false; }
                    if (!entry.Contains(".")) entry = (entry == "" || entry == "-" ? entry + "0" : entry) + ".";
                    break;
                case "±":
                    if (entry == "" && done) entry = Text(last);
                    if (entry == "") return;
                    entry = entry.StartsWith("-") ? entry.Substring(1) : "-" + entry;
                    break;
                case "%":
                    if (entry == "" && done) entry = Text(last);
                    if (entry == "") return;
                    entry = Text(Parse(entry) / 100m);
                    break;
                case "=": Evaluate(); return;
                default:
                    if (key.Length == 1 && char.IsDigit(key[0]))
                    {
                        if (done) { tokens.Clear(); entry = ""; done = false; }
                        if (entry.TrimStart('-').Replace(".", "").Length >= 16) return;
                        entry = entry == "0" ? key : entry == "-0" ? "-" + key : entry + key;
                    }
                    else if (key.Length == 1 && "+−×÷".IndexOf(key[0]) >= 0) Operator(key[0]);
                    break;
            }
            UpdateExpression();
        }

        void Operator(char op)
        {
            if (entry != "") { tokens.Add(Parse(entry)); entry = ""; }
            else if (tokens.Count > 0 && tokens[tokens.Count - 1] is char) tokens.RemoveAt(tokens.Count - 1); // сменить знак
            else if (tokens.Count == 0) tokens.Add(last);                                                       // продолжить от результата
            tokens.Add(op);
            done = false;
        }

        void Evaluate()
        {
            if (entry != "") { tokens.Add(Parse(entry)); entry = ""; }
            if (tokens.Count > 0 && tokens[tokens.Count - 1] is char) tokens.RemoveAt(tokens.Count - 1);
            if (tokens.Count == 0) return;
            string expr = Join(tokens);
            try
            {
                // Сначала × и ÷, потом + и −.
                var t = new List<object>(tokens);
                for (int i = 1; i < t.Count; i += 2)
                {
                    char op = (char)t[i];
                    if (op != Times && op != Divide) continue;
                    decimal a = (decimal)t[i - 1], b = (decimal)t[i + 1];
                    if (op == Divide && b == 0) throw new DivideByZeroException();
                    t[i - 1] = op == Times ? a * b : a / b;
                    t.RemoveRange(i, 2);
                    i -= 2;
                }
                decimal result = (decimal)t[0];
                for (int i = 1; i < t.Count; i += 2)
                    result = (char)t[i] == Plus ? result + (decimal)t[i + 1] : result - (decimal)t[i + 1];
                last = Math.Round(result, 12);
                Expression = expr + " =";
            }
            catch (Exception ex)
            {
                if (!(ex is DivideByZeroException) && !(ex is OverflowException)) throw;
                error = true;
                Expression = expr + " =";
            }
            tokens.Clear();
            done = true;
        }

        void Clear()
        {
            tokens.Clear();
            entry = "";
            last = 0;
            done = error = false;
            Expression = "";
        }

        void UpdateExpression()
        {
            var t = new List<object>(tokens);
            string s = Join(t);
            if (entry != "") s = (s == "" ? "" : s + " ") + Local(entry);
            Expression = done ? Expression : s;
        }

        static string Join(List<object> t)
        {
            var parts = new List<string>();
            foreach (var x in t) parts.Add(x is char ? x.ToString() : Local(Text((decimal)x)));
            return string.Join(" ", parts);
        }

        static decimal Parse(string s)
        {
            if (s == "" || s == "-" || s == "." || s == "-.") return 0;
            return decimal.Parse(s.TrimEnd('.'), System.Globalization.NumberStyles.Number, Inv);
        }

        static string Text(decimal v) { return v.ToString("0.############", Inv); }

        // Десятичный разделитель как принято в языке интерфейса (в русском — запятая).
        static string Local(string s) { return s.Replace(".", L.Culture.NumberFormat.NumberDecimalSeparator); }
    }

    class CalcPage : Rows
    {
        readonly Calculator calc = new Calculator();
        readonly Label expression, display;
        Button equals;

        public CalcPage()
        {
            var screen = new Panel { BackColor = Theme.Surface, Padding = Theme.P(12, 6, 12, 6), Margin = Theme.P(0, 8, 0, 8) };
            expression = new Label { Dock = DockStyle.Top, Height = Theme.S(22), Font = Theme.Base, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, UseMnemonic = false };
            display = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI Light", 28f), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
            screen.Controls.Add(display);
            screen.Controls.Add(expression);
            Fixed(screen, Theme.S(96));

            string[,] keys =
            {
                { "C", "±", "%", "÷" },
                { "7", "8", "9", "×" },
                { "4", "5", "6", "−" },
                { "1", "2", "3", "+" },
                { "⌫", "0", ".", "=" },
            };
            var grid = new TableLayoutPanel { ColumnCount = 4, RowCount = 5, BackColor = Theme.Bg, Margin = Padding.Empty, Padding = Padding.Empty };
            for (int c = 0; c < 4; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            for (int r = 0; r < 5; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            var opColor = Theme.Mix(Theme.Input, Theme.Accent, 0.3);
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 4; c++)
                {
                    string key = keys[r, c];
                    bool isOp = c == 3, isFn = r == 0 && c < 3;
                    var b = (SoftButton)Theme.Btn(key == "." ? L.Culture.NumberFormat.NumberDecimalSeparator : key, key == "=");
                    b.AutoSize = false;
                    b.Dock = DockStyle.Fill;
                    b.Margin = Theme.P(3, 3, 3, 3);
                    b.Radius = Theme.S(12);
                    b.Font = new Font(Theme.Base.FontFamily, 14f);
                    b.FlatAppearance.BorderSize = 0;
                    if (key != "=")
                    {
                        b.BackColor = isOp ? opColor : isFn ? Theme.Hover : Theme.Input;
                        b.FlatAppearance.MouseOverBackColor = Theme.Mix(b.BackColor, Theme.Text, 0.12);
                        b.FlatAppearance.MouseDownBackColor = Theme.Border;
                    }
                    b.Click += (s, e) => Press(key);
                    if (key == "=") equals = b;
                    grid.Controls.Add(b, c, r);
                }
            Fill(grid);
            Theme.Tips.SetToolTip(display, L.T("calc.keysHint"));
            ShowValue();
        }

        public Calculator Logic { get { return calc; } }

        public void FocusKeys() { if (equals != null) equals.Focus(); }

        void Press(string key)
        {
            calc.Press(key);
            ShowValue();
        }

        void ShowValue()
        {
            expression.Text = calc.Expression;
            string text = calc.Display;
            display.Text = text;
            // Длинное число — шрифт мельче, чтобы оно помещалось целиком.
            float size = text.Length > 14 ? 18f : text.Length > 10 ? 22f : 28f;
            if (Math.Abs(display.Font.Size - size) > 0.1f) display.Font = new Font("Segoe UI Light", size);
        }

        // Клавиатура: цифры, + − * /, Enter или =, Backspace, Delete — сброс, точка или запятая.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            string key = KeyFor(keyData);
            if (key != null) { Press(key); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static string KeyFor(Keys k)
        {
            bool shift = (k & Keys.Shift) != 0;
            Keys code = k & Keys.KeyCode;
            if ((k & (Keys.Control | Keys.Alt)) != 0) return null;
            if (code >= Keys.NumPad0 && code <= Keys.NumPad9) return ((int)(code - Keys.NumPad0)).ToString();
            if (!shift && code >= Keys.D0 && code <= Keys.D9) return ((int)(code - Keys.D0)).ToString();
            if (shift && code == Keys.D8 || code == Keys.Multiply) return "×";
            if (shift && code == Keys.D5) return "%";
            if (shift && code == Keys.Oemplus || code == Keys.Add) return "+";
            if (code == Keys.OemMinus || code == Keys.Subtract) return "−";
            if (code == Keys.Divide || code == Keys.OemQuestion) return "÷";
            if (code == Keys.Return || (!shift && code == Keys.Oemplus)) return "=";
            if (code == Keys.Decimal || code == Keys.OemPeriod || code == Keys.Oemcomma) return ".";
            if (code == Keys.Back) return "⌫";
            if (code == Keys.Delete) return "C";
            return null;
        }
    }

    // ---------------------------------------------------------------- Таймер, секундомер, калькулятор
    class ToolsPage : Panel, IPage
    {
        public readonly TimerPage Timer = new TimerPage { Dock = DockStyle.Fill };
        public readonly StopwatchPanel Stopwatch = new StopwatchPanel();
        public readonly CalcPage Calc = new CalcPage();
        readonly Rows timerView = new Rows();
        readonly TabStrip sub = new TabStrip(new[] { L.T("tools.timer"), L.T("tools.calc") }) { Dock = DockStyle.Top };

        public ToolsPage()
        {
            BackColor = Theme.Bg;
            sub.Height = Theme.S(32);
            timerView.Auto(Timer);
            timerView.Fill(Stopwatch);
            Calc.Dock = DockStyle.Fill;
            Controls.Add(timerView);
            Controls.Add(Calc);
            Controls.Add(sub);
            sub.SelectedChanged += (s, e) => ShowSub();
            sub.SelectedIndex = 0;
        }

        public void SelectSub(int index) { sub.SelectedIndex = index; }

        void ShowSub()
        {
            timerView.Visible = sub.SelectedIndex == 0;
            Calc.Visible = sub.SelectedIndex == 1;
            if (Calc.Visible) Calc.FocusKeys();
        }

        public void OnPageShown() { if (Calc.Visible) Calc.FocusKeys(); }
    }

    // ---------------------------------------------------------------- Погода
    class WeatherPage : Panel, IPage
    {
        readonly Rows content = new Rows(), empty = new Rows(), days = new Rows();
        readonly Label city, temp, desc, details, status;
        readonly Label clock, date;
        readonly Button refreshBtn;
        readonly Timer clockTimer = new Timer { Interval = 1000 };
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

            // Текущее время и дата вверху вкладки; обновляются, только пока вкладка видна.
            clock = new Label { Font = Theme.Big, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = Theme.S(34), TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            date = new Label { Font = Theme.Small, ForeColor = Theme.Muted, Dock = DockStyle.Top, Height = Theme.S(28), TextAlign = ContentAlignment.TopLeft, UseMnemonic = false };
            clockTimer.Tick += (s, e) => UpdateClock();
            VisibleChanged += (s, e) =>
            {
                if (Visible) { UpdateClock(); clockTimer.Start(); }
                else clockTimer.Stop();
            };

            Controls.Add(content);
            Controls.Add(empty);
            Controls.Add(date);
            Controls.Add(clock);
            UpdateClock();
            UpdateMode();
        }

        void UpdateClock()
        {
            var now = DateTime.Now;
            clock.Text = now.ToString("T", L.Culture);
            string d = now.ToString("D", L.Culture);
            date.Text = d.Length > 0 ? char.ToUpper(d[0], L.Culture) + d.Substring(1) : d;
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

    // ---------------------------------------------------------------- Микшер громкости
    // Строка громкости: кнопка выключения звука, ползунок и проценты.
    class VolumeRow : Panel
    {
        public readonly VolumeSlider Slider = new VolumeSlider { Dock = DockStyle.Fill };
        readonly Button muteBtn;
        readonly Label percent;
        bool muted;
        public event EventHandler VolumeChanged, MuteClicked;

        public VolumeRow()
        {
            BackColor = Theme.Surface;
            Height = Theme.S(32);
            muteBtn = Theme.IconBtn("", "", Theme.Surface);
            muteBtn.Dock = DockStyle.Left;
            muteBtn.Click += (s, e) => { var h = MuteClicked; if (h != null) h(this, EventArgs.Empty); };
            percent = new Label { Dock = DockStyle.Right, Width = Theme.S(46), Font = Theme.Base, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
            Slider.Margin = Padding.Empty;
            Slider.Scrolled += (s, e) =>
            {
                percent.Text = Slider.Value + "%";
                var h = VolumeChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
            Controls.Add(Slider);
            Controls.Add(percent);
            Controls.Add(muteBtn);
            Show(0, false);
        }

        public float Volume { get { return Slider.Value / 100f; } }

        public void Show(float volume, bool isMuted)
        {
            if (!Slider.Dragging) Slider.Value = (int)Math.Round(volume * 100);
            percent.Text = Slider.Value + "%";
            Slider.Dim = isMuted;
            percent.ForeColor = isMuted ? Theme.Muted : Theme.Text;
            if (isMuted == muted && muteBtn.Text != "") return;
            muted = isMuted;
            muteBtn.Text = isMuted ? "" : "";
            muteBtn.ForeColor = isMuted ? Theme.Warn : Theme.Text;
            Theme.Tips.SetToolTip(muteBtn, isMuted ? L.T("mix.unmute") : L.T("mix.mute"));
        }
    }

    class MixerPage : Rows, IPage
    {
        readonly Label deviceLabel;
        readonly VolumeRow masterRow = new VolumeRow { Dock = DockStyle.Top };
        readonly FlowLayoutPanel cards;
        readonly Timer refresh = new Timer { Interval = 1000 };
        // Карточки программ по ключу; у карточки в Tag — текущий AudioApp.
        readonly Dictionary<string, Panel> byKey = new Dictionary<string, Panel>();
        List<string> shownKeys = new List<string>();

        public MixerPage()
        {
            var master = new Panel { BackColor = Theme.Surface, Padding = Theme.P(12, 8, 8, 6), Margin = Theme.P(0, 0, 0, 12) };
            var masterTitle = new Label { Text = L.T("mix.master"), Font = Theme.Bold, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = Theme.S(22), AutoEllipsis = true, UseMnemonic = false };
            deviceLabel = new Label { Font = Theme.Small, ForeColor = Theme.Muted, Dock = DockStyle.Top, Height = Theme.S(20), AutoEllipsis = true, UseMnemonic = false };
            masterRow.VolumeChanged += (s, e) => { Audio.MasterVolume = masterRow.Volume; };
            masterRow.MuteClicked += (s, e) => { Audio.MasterMuted = !Audio.MasterMuted; ShowMaster(); };
            master.Controls.Add(masterRow);
            master.Controls.Add(deviceLabel);
            master.Controls.Add(masterTitle);
            Fixed(master, Theme.S(92));

            var appsTitle = Auto(Theme.Lbl(L.T("mix.apps"), Theme.Title, Theme.Text));
            appsTitle.Margin = Theme.P(0, 0, 0, 6);

            cards = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
                BackColor = Theme.Bg, Margin = Theme.P(0, 0, 0, 6), Padding = Padding.Empty
            };
            cards.HandleCreated += (s, e) => Native.DarkTheme(cards.Handle, "DarkMode_Explorer");
            cards.Resize += (s, e) => FitCards();
            Fill(cards);

            Auto(Theme.Wrap(L.T("mix.hint"), Theme.Small, Theme.Muted));

            refresh.Tick += (s, e) => { if (Visible) RefreshData(); };
            VisibleChanged += (s, e) =>
            {
                if (Visible) { RefreshData(); refresh.Start(); }
                else { refresh.Stop(); Audio.Release(); }
            };
        }

        public void OnPageShown() { RefreshData(); }

        public void RefreshData()
        {
            Audio.Refresh();
            ShowMaster();

            var keys = Audio.Apps.Select(a => a.Key).ToList();
            bool dragging = byKey.Values.Any(c => ((VolumeRow)c.Controls[0]).Slider.Dragging);
            if (!dragging && !keys.SequenceEqual(shownKeys)) Rebuild();
            foreach (var app in Audio.Apps)
            {
                Panel card;
                if (!byKey.TryGetValue(app.Key, out card)) continue;
                card.Tag = app;
                ((VolumeRow)card.Controls[0]).Show(app.Volume, app.Muted);
            }
        }

        void ShowMaster()
        {
            bool has = Audio.HasDevice;
            deviceLabel.Text = has ? Audio.DeviceName : L.T("mix.noDevice");
            deviceLabel.ForeColor = has ? Theme.Muted : Theme.Warn;
            masterRow.Enabled = has;
            masterRow.Show(Audio.MasterVolume, Audio.MasterMuted);
        }

        void Rebuild()
        {
            cards.SuspendLayout();
            while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
            byKey.Clear();
            if (Audio.Apps.Count == 0)
            {
                var empty = Theme.Lbl(Audio.HasDevice ? L.T("mix.empty") : "", Theme.Base, Theme.Muted);
                empty.AutoSize = false;
                empty.Height = Theme.S(60);
                cards.Controls.Add(empty);
            }
            foreach (var app in Audio.Apps)
            {
                var card = Card(app);
                byKey[app.Key] = card;
                cards.Controls.Add(card);
            }
            shownKeys = Audio.Apps.Select(a => a.Key).ToList();
            cards.ResumeLayout();
            FitCards();
        }

        void FitCards()
        {
            int w = cards.ClientSize.Width - Theme.S(2);
            foreach (Control c in cards.Controls) c.Width = w;
        }

        Panel Card(AudioApp app)
        {
            var card = new Panel { BackColor = Theme.Surface, Height = Theme.S(66), Padding = Theme.P(12, 6, 8, 2), Margin = Theme.P(0, 0, 0, 8), Tag = app };
            var row = new VolumeRow { Dock = DockStyle.Top };
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.S(24), BackColor = Theme.Surface };
            var name = new Label { Text = app.Name, Font = Theme.Bold, ForeColor = Theme.Text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };
            header.Controls.Add(name);
            var icon = Audio.IconFor(app);
            if (icon != null)
            {
                int size = Theme.S(16);
                var pic = new PictureBox { Dock = DockStyle.Left, Width = size + Theme.S(8), SizeMode = PictureBoxSizeMode.CenterImage, Image = new Bitmap(icon.ToBitmap(), size, size) };
                header.Controls.Add(pic);
            }
            if (app.Path != null) Theme.Tips.SetToolTip(name, app.Path);

            row.VolumeChanged += (s, e) => Audio.SetVolume((AudioApp)card.Tag, row.Volume);
            row.MuteClicked += (s, e) =>
            {
                var a = (AudioApp)card.Tag;
                Audio.SetMute(a, !a.Muted);
                row.Show(a.Volume, a.Muted);
            };
            row.Show(app.Volume, app.Muted);
            card.Controls.Add(row);    // Controls[0] — строка громкости
            card.Controls.Add(header);
            return card;
        }
    }

    // ---------------------------------------------------------------- Плеер
    // Общий блок управления: перемотка со временем, пять кнопок как на iPhone, громкость.
    class MediaControls : Rows
    {
        static readonly Font BigIcons = new Font("Segoe MDL2 Assets", 16f), MidIcons = new Font("Segoe MDL2 Assets", 12f);
        public const string GlyphPlay = "", GlyphPause = "";
        public readonly VolumeSlider Seek = new VolumeSlider { BackColor = Theme.Bg, Height = Theme.S(22) };
        public readonly VolumeSlider Volume = new VolumeSlider { BackColor = Theme.Bg, Dock = DockStyle.Fill };
        public readonly Button Shuffle, Previous, Play, Next, Repeat;
        readonly Label elapsed, remaining;
        readonly Panel volumeRow;

        public MediaControls(bool withVolume)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Fixed(Seek, Theme.S(22)).Margin = Theme.P(0, 4, 0, 0);
            elapsed = Theme.Lbl("", Theme.Small, Theme.Muted);
            remaining = Theme.Lbl("", Theme.Small, Theme.Muted);
            Auto(Theme.Line(elapsed, remaining)).Margin = Theme.P(4, 0, 4, 0);

            Shuffle = Btn("", L.T("pl.shuffle"), false, MidIcons);
            Previous = Btn("", L.T("pl.prev"), false, BigIcons);
            Play = Btn(GlyphPlay, L.T("pl.play"), true, BigIcons);
            Next = Btn("", L.T("pl.next"), false, BigIcons);
            Repeat = Btn("", L.T("pl.repeat.off"), false, MidIcons);
            var row = new TableLayoutPanel { ColumnCount = 5, RowCount = 1, BackColor = Theme.Bg, Margin = Padding.Empty, Padding = Padding.Empty };
            foreach (var b in new[] { Shuffle, Previous, Play, Next, Repeat })
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                b.Anchor = AnchorStyles.None;
                row.Controls.Add(b);
            }
            Fixed(row, Theme.S(52));

            volumeRow = new Panel { BackColor = Theme.Bg, Margin = Theme.P(0, 2, 0, 2), Padding = Theme.P(4, 0, 4, 0), Visible = withVolume };
            var low = new Label { Text = "", Font = Theme.Icons, ForeColor = Theme.Muted, Dock = DockStyle.Left, Width = Theme.S(24), TextAlign = ContentAlignment.MiddleLeft };
            var high = new Label { Text = "", Font = Theme.Icons, ForeColor = Theme.Muted, Dock = DockStyle.Right, Width = Theme.S(24), TextAlign = ContentAlignment.MiddleRight };
            volumeRow.Controls.Add(Volume);
            volumeRow.Controls.Add(low);
            volumeRow.Controls.Add(high);
            Fixed(volumeRow, Theme.S(28));
            Seek.Scrolled += (s, e) => ShowTimes(TimeSpan.FromSeconds(Seek.Value), TimeSpan.FromSeconds(Seek.Maximum));
        }

        static Button Btn(string glyph, string tip, bool primary, Font font)
        {
            var b = primary ? Theme.Btn(glyph, true) : Theme.IconBtn(glyph, tip, Theme.Bg);
            b.Font = font;
            b.AutoSize = false;
            b.Size = primary ? new Size(Theme.S(60), Theme.S(46)) : new Size(Theme.S(46), Theme.S(42));
            b.Padding = Padding.Empty;
            b.Margin = Padding.Empty;
            if (primary) Theme.Tips.SetToolTip(b, tip);
            return b;
        }

        // Позиция и длительность; если длительность неизвестна (например, прямой эфир), перемотка выключена.
        public void ShowPosition(TimeSpan pos, TimeSpan duration, bool canSeek)
        {
            bool known = duration > TimeSpan.Zero;
            Seek.Enabled = known && canSeek;
            Seek.Dim = !known || !canSeek;
            Seek.Maximum = known ? Math.Max(1, (int)duration.TotalSeconds) : 1;
            if (!Seek.Dragging) Seek.Value = known ? (int)pos.TotalSeconds : 0;
            if (!Seek.Dragging) ShowTimes(pos, duration);
        }

        void ShowTimes(TimeSpan pos, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) { elapsed.Text = pos > TimeSpan.Zero ? Fmt.Clock(pos) : ""; remaining.Text = ""; return; }
            elapsed.Text = Fmt.Clock(pos);
            remaining.Text = "−" + Fmt.Clock(duration > pos ? duration - pos : TimeSpan.Zero);
        }

        public void ShowState(bool playing, bool canPlay, bool canPrev, bool canNext, bool shuffle, bool canShuffle, string repeat, bool canRepeat)
        {
            Play.Text = playing ? GlyphPause : GlyphPlay;
            Theme.Tips.SetToolTip(Play, playing ? L.T("pl.pause") : L.T("pl.play"));
            Play.Enabled = canPlay;
            Previous.Enabled = canPrev;
            Next.Enabled = canNext;
            Shuffle.Enabled = canShuffle;
            Shuffle.ForeColor = shuffle ? Theme.Accent : Theme.Muted;
            Repeat.Enabled = canRepeat;
            Repeat.Text = repeat == "track" ? "" : "";
            Repeat.ForeColor = repeat == "off" ? Theme.Muted : Theme.Accent;
            Theme.Tips.SetToolTip(Repeat, L.T("pl.repeat." + repeat));
        }

        public void ShowVolume(int percent, string tip)
        {
            bool has = percent >= 0;
            Volume.Enabled = has;
            Volume.Dim = !has;
            if (has && !Volume.Dragging) Volume.Value = percent;
            Theme.Tips.SetToolTip(Volume, tip);
        }
    }

    // «Плеер»: «Сейчас играет» — управление тем, что играет в любой программе (через Windows);
    // «Мои треки» — плеер для папки с музыкой. Ни аккаунтов, ни интернета.
    class PlayerPage : Panel, IPage
    {
        readonly TabStrip sub = new TabStrip(new[] { L.T("pl.tab.now"), L.T("pl.tab.files") }) { Dock = DockStyle.Top };
        readonly Rows nowView = new Rows(), filesView = new Rows();
        readonly Timer tick = new Timer { Interval = 500 };

        // --- «Сейчас играет»
        readonly DarkCombo apps = new DarkCombo();
        readonly CoverView cover = new CoverView();
        readonly MediaControls nowCtl = new MediaControls(true);
        NowPlaying now;
        string chosenApp;
        bool fetching, busy;
        int sinceFetch, sinceApps;
        DateTime holdUntil; // после команды Windows ещё мгновение может отдавать старое состояние

        // --- «Мои треки»
        readonly Label folderLabel, filesStatus, miniTitle, miniArtist;
        readonly TwoLineList list = new TwoLineList();
        readonly PictureBox miniCover;
        readonly MediaControls fileCtl = new MediaControls(true);
        bool scanned, tagsLoading;
        LocalTrack shownTrack;

        public PlayerPage()
        {
            BackColor = Theme.Bg;
            sub.Height = Theme.S(32);

            // --- «Сейчас играет»
            apps.Margin = Theme.P(0, 6, 0, 0);
            apps.Dock = DockStyle.Fill;
            Theme.Tips.SetToolTip(apps, L.T("pl.source"));
            apps.SelectionChangeCommitted += (s, e) =>
            {
                var a = apps.SelectedItem as AppItem;
                chosenApp = a != null ? a.Id : null;
                FetchNow();
            };
            nowView.Auto(apps);
            cover.Margin = Theme.P(0, 6, 0, 0);
            nowView.Fill(cover);
            nowView.Auto(nowCtl);
            nowCtl.Play.Click += (s, e) => NowCommand(SystemMedia.PlayPause, () => { if (now != null) { now.Playing = !now.Playing; } });
            nowCtl.Next.Click += (s, e) => NowCommand(SystemMedia.Next, null);
            nowCtl.Previous.Click += (s, e) => NowCommand(SystemMedia.Previous, null);
            nowCtl.Shuffle.Click += (s, e) =>
            {
                if (now == null) return;
                bool on = !now.Shuffle;
                NowCommand(a => SystemMedia.SetShuffle(a, on), () => now.Shuffle = on);
            };
            nowCtl.Repeat.Click += (s, e) =>
            {
                if (now == null) return;
                string mode = SystemMedia.NextRepeat(now.Repeat);
                NowCommand(a => SystemMedia.SetRepeat(a, mode), () => now.Repeat = mode);
            };
            nowCtl.Seek.Committed += (s, e) =>
            {
                if (now == null) return;
                var pos = TimeSpan.FromSeconds(nowCtl.Seek.Value);
                NowCommand(a => SystemMedia.Seek(a, pos), () => { now.Position = pos; now.Fetched = DateTime.UtcNow; });
            };
            nowCtl.Volume.Scrolled += (s, e) => SetAppVolume();
            nowCtl.Volume.Committed += (s, e) => SetAppVolume();

            // --- «Мои треки»
            folderLabel = Theme.Lbl("", Theme.Bold, Theme.Text);
            folderLabel.AutoEllipsis = true;
            var pick = Theme.Btn(L.T("pl.pickFolder"));
            pick.Click += (s, e) => PickFolder();
            filesView.Auto(Theme.Line(folderLabel, pick)).Margin = Theme.P(0, 6, 0, 2);
            filesStatus = filesView.Auto(Theme.Wrap("", Theme.Small, Theme.Muted));
            list.First = o => ((LocalTrack)o).Title;
            list.Second = o =>
            {
                var t = (LocalTrack)o;
                var parts = new List<string>();
                if (t.Artist != "") parts.Add(t.Artist);
                if (t.Album != "") parts.Add(t.Album);
                if (t.Duration > TimeSpan.Zero) parts.Add(Fmt.Clock(t.Duration));
                return (t == LocalPlayer.Current ? "▶  " : "") + string.Join("  ·  ", parts);
            };
            list.DoubleClick += (s, e) => PlayFile(list.SelectedIndex);
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; PlayFile(list.SelectedIndex); } };
            list.Margin = Theme.P(0, 2, 0, 6);
            filesView.Fill(list);

            var mini = new Panel { BackColor = Theme.Surface, Padding = Theme.P(8, 8, 8, 8), Margin = Theme.P(0, 0, 0, 2) };
            miniCover = new PictureBox { Dock = DockStyle.Left, Width = Theme.S(48), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Input };
            var texts = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = Theme.P(10, 2, 0, 0) };
            miniTitle = new Label { Font = Theme.Bold, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = Theme.S(22), AutoEllipsis = true, UseMnemonic = false };
            miniArtist = new Label { Font = Theme.Small, ForeColor = Theme.Muted, Dock = DockStyle.Top, Height = Theme.S(20), AutoEllipsis = true, UseMnemonic = false };
            texts.Controls.Add(miniArtist);
            texts.Controls.Add(miniTitle);
            mini.Controls.Add(texts);
            mini.Controls.Add(miniCover);
            filesView.Fixed(mini, Theme.S(64));
            filesView.Auto(fileCtl);
            fileCtl.Play.Click += (s, e) => { LocalPlayer.PlayPause(); ShowFiles(); };
            fileCtl.Next.Click += (s, e) => { LocalPlayer.Next(); ShowFiles(); };
            fileCtl.Previous.Click += (s, e) => { LocalPlayer.Previous(); ShowFiles(); };
            fileCtl.Shuffle.Click += (s, e) =>
            {
                LocalPlayer.SetShuffle(!LocalPlayer.Shuffle);
                App.Settings.PlayerShuffle = LocalPlayer.Shuffle;
                ShowFiles();
            };
            fileCtl.Repeat.Click += (s, e) =>
            {
                LocalPlayer.Repeat = SystemMedia.NextRepeat(LocalPlayer.Repeat);
                App.Settings.PlayerRepeat = LocalPlayer.Repeat;
                ShowFiles();
            };
            fileCtl.Seek.Committed += (s, e) => LocalPlayer.Seek(TimeSpan.FromSeconds(fileCtl.Seek.Value));
            fileCtl.Volume.Scrolled += (s, e) => SetFileVolume();
            fileCtl.Volume.Committed += (s, e) => SetFileVolume();
            LocalPlayer.Shuffle = App.Settings.PlayerShuffle;
            LocalPlayer.Repeat = App.Settings.PlayerRepeat == "list" || App.Settings.PlayerRepeat == "track" ? App.Settings.PlayerRepeat : "off";

            Controls.Add(nowView);
            Controls.Add(filesView);
            Controls.Add(sub);
            sub.SelectedChanged += (s, e) => ShowSub();
            sub.SelectedIndex = 0;

            tick.Tick += (s, e) => Tick();
            VisibleChanged += (s, e) =>
            {
                if (Visible) { tick.Start(); OnPageShown(); }
                else { tick.Stop(); Audio.Release(); }
            };
            ShowNow(null);
            ShowFiles();
        }

        class AppItem
        {
            public string Id, Name;
            public override string ToString() { return Name; }
        }

        public void SelectSub(int index) { sub.SelectedIndex = index; }

        void ShowSub()
        {
            nowView.Visible = sub.SelectedIndex == 0;
            filesView.Visible = sub.SelectedIndex == 1;
            if (filesView.Visible) EnsureScanned();
        }

        public void OnPageShown()
        {
            if (nowView.Visible) { FetchNow(); FetchApps(); }
            if (filesView.Visible) EnsureScanned();
        }

        void Tick()
        {
            if (!Visible) return;
            if (nowView.Visible)
            {
                if (++sinceFetch >= 2) FetchNow();
                if (++sinceApps >= 6) FetchApps();
                ShowNowProgress();
            }
            var what = LocalPlayer.Poll();
            if (what == "failed" && LocalPlayer.Current != null) SetStatus(L.F("pl.cantPlay", LocalPlayer.Current.Title), Theme.Warn);
            if (filesView.Visible || what != null) ShowFiles();
            if (tagsLoading) list.Invalidate();
        }

        // --- «Сейчас играет»

        void FetchNow()
        {
            sinceFetch = 0;
            if (fetching || busy) return;
            if (!SystemMedia.Supported) { ShowNow(null); return; }
            fetching = true;
            string app = chosenApp;
            Background.Run(this, () => SystemMedia.Read(app), n => { fetching = false; if (DateTime.UtcNow >= holdUntil) ShowNow(n); }, ex => { fetching = false; });
        }

        void FetchApps()
        {
            sinceApps = 0;
            if (!SystemMedia.Supported) return;
            Background.Run(this, () => SystemMedia.Apps(), ShowApps, ex => { });
        }

        void ShowApps(List<string> ids)
        {
            var items = ids.Select(i => new AppItem { Id = i, Name = SystemMedia.AppName(i) }).ToList();
            var current = now != null ? now.AppId : chosenApp;
            if (apps.Items.Count == items.Count && items.Select(i => i.Id).SequenceEqual(apps.Items.Cast<AppItem>().Select(i => i.Id)))
            {
                var same = apps.Items.Cast<AppItem>().FirstOrDefault(i => i.Id == current);
                if (same != null && apps.SelectedItem != same && !apps.DroppedDown) apps.SelectedItem = same;
                return;
            }
            if (apps.DroppedDown) return;
            apps.BeginUpdate();
            apps.Items.Clear();
            foreach (var i in items) apps.Items.Add(i);
            apps.EndUpdate();
            var pick = items.FirstOrDefault(i => i.Id == current);
            if (pick != null) apps.SelectedItem = pick;
            apps.Enabled = items.Count > 1;
            apps.Visible = items.Count > 0;
        }

        public void ShowNow(NowPlaying n)
        {
            now = n;
            if (n == null)
            {
                cover.Cover = null;
                cover.Set(L.T("pl.nothing"), SystemMedia.Supported ? L.T("pl.nothingHint") : L.T("pl.unsupported"), "");
                nowCtl.ShowState(false, false, false, false, false, false, "off", false);
                nowCtl.ShowPosition(TimeSpan.Zero, TimeSpan.Zero, false);
                nowCtl.ShowVolume(-1, L.T("pl.noVolume"));
                apps.Visible = false;
                return;
            }
            if (cover.Cover != n.Cover) cover.Cover = n.Cover;
            SyncApp(n.AppId);
            string title = n.Title != "" ? n.Title : n.AppName;
            string artist = n.Artist + (n.Album != "" && n.Album != n.Title ? (n.Artist != "" ? "  ·  " : "") + n.Album : "");
            cover.Set(title, artist, n.AppName);
            nowCtl.ShowState(n.Playing, n.CanPlayPause, n.CanPrevious, n.CanNext, n.Shuffle, n.CanShuffle, n.Repeat, n.CanRepeat);
            ShowNowProgress();
            ShowAppVolume();
        }

        // В списке программ выбрана та, что сейчас показана.
        void SyncApp(string appId)
        {
            if (apps.DroppedDown) return;
            var item = apps.Items.Cast<AppItem>().FirstOrDefault(i => i.Id == appId);
            if (item != null && apps.SelectedItem != item) apps.SelectedItem = item;
            apps.Visible = apps.Items.Count > 0;
        }

        void ShowNowProgress()
        {
            if (now == null) return;
            var pos = now.Position;
            if (now.Playing) pos += DateTime.UtcNow - now.Fetched;
            if (now.Duration > TimeSpan.Zero && pos > now.Duration) pos = now.Duration;
            nowCtl.ShowPosition(pos, now.Duration, now.CanSeek);
        }

        // Команда программе в фоне; optimistic — сразу показать ожидаемый результат.
        void NowCommand(Action<string> command, Action optimistic)
        {
            if (busy || now == null) return;
            busy = true;
            if (optimistic != null) { optimistic(); ShowNow(now); }
            string app = now.AppId;
            holdUntil = DateTime.UtcNow.AddMilliseconds(800);
            Background.Run(this, () => { command(app); return true; }, ok => { busy = false; sinceFetch = 1; }, ex => { busy = false; sinceFetch = 1; });
        }

        // Громкость программы, которая играет, — её строка в микшере Windows (как ползунок в самом Spotify).
        AudioApp MixerApp()
        {
            if (now == null) return null;
            string want = now.AppId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? now.AppId.Substring(0, now.AppId.Length - 4) : now.AppId;
            return Audio.Apps.FirstOrDefault(a => a.Path != null &&
                (string.Equals(System.IO.Path.GetFileNameWithoutExtension(a.Path), want, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(System.IO.Path.GetFileNameWithoutExtension(a.Path), now.AppName, StringComparison.OrdinalIgnoreCase)));
        }

        void ShowAppVolume()
        {
            if (nowCtl.Volume.Dragging) return;
            Audio.Refresh();
            var a = MixerApp();
            nowCtl.ShowVolume(a != null ? (int)Math.Round(a.Volume * 100) : -1, a != null ? L.F("pl.volume", now.AppName) : L.T("pl.noVolume"));
        }

        void SetAppVolume()
        {
            var a = MixerApp();
            if (a != null) Audio.SetVolume(a, nowCtl.Volume.Value / 100f);
        }

        // --- «Мои треки»

        void SetStatus(string text, Color color)
        {
            filesStatus.Text = text;
            filesStatus.ForeColor = color;
            filesStatus.Visible = text != "";
        }

        void EnsureScanned()
        {
            if (scanned) return;
            scanned = true;
            string folder = App.Settings.MusicFolder;
            if (string.IsNullOrEmpty(folder)) { ShowFolder(); SetStatus(L.T("pl.pickHint"), Theme.Muted); return; }
            Scan(folder);
        }

        void ShowFolder()
        {
            string folder = App.Settings.MusicFolder;
            // Только имя папки, чтобы строка не разъезжалась; полный путь — во всплывающей подсказке.
            string name = string.IsNullOrEmpty(folder) ? "" : Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            folderLabel.Text = string.IsNullOrEmpty(folder) ? L.T("pl.noFolder") : (name != "" ? name : folder);
            Theme.Tips.SetToolTip(folderLabel, folder ?? "");
        }

        void PickFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = L.T("pl.folder"), ShowNewFolderButton = false })
            {
                if (Directory.Exists(App.Settings.MusicFolder)) dlg.SelectedPath = App.Settings.MusicFolder;
                else dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                if (dlg.ShowDialog(App.Owner) != DialogResult.OK) return;
                App.Settings.MusicFolder = dlg.SelectedPath;
                App.SaveSettings();
                Scan(dlg.SelectedPath);
            }
        }

        public void Scan(string folder)
        {
            ShowFolder();
            if (!Directory.Exists(folder)) { SetStatus(L.F("pl.folderGone", folder), Theme.Warn); return; }
            SetStatus(L.T("pl.scanning"), Theme.Muted);
            Background.Run(this, () => LocalPlayer.Scan(folder), tracks =>
            {
                LocalPlayer.SetTracks(tracks);
                list.BeginUpdate();
                list.Items.Clear();
                foreach (var t in tracks) list.Items.Add(t);
                list.EndUpdate();
                SetStatus(tracks.Count == 0 ? L.T("pl.empty") : L.F("pl.count", tracks.Count), Theme.Muted);
                ShowFiles();
                // Теги (название, исполнитель, длительность) дочитываются в фоне, список обновляется по таймеру.
                tagsLoading = true;
                Background.Run(this, () => { foreach (var t in tracks) if (!t.TagsLoaded) LocalPlayer.LoadTags(t); return true; },
                    ok => { tagsLoading = false; list.Invalidate(); ShowFiles(); }, ex => { tagsLoading = false; });
            }, ex => SetStatus(L.F("pl.folderGone", folder), Theme.Warn));
        }

        void PlayFile(int index)
        {
            if (index < 0) return;
            if (filesStatus.ForeColor == Theme.Warn) SetStatus(L.F("pl.count", LocalPlayer.Tracks.Count), Theme.Muted);
            LocalPlayer.Play(index);
            ShowFiles();
        }

        void SetFileVolume()
        {
            LocalPlayer.Volume = fileCtl.Volume.Value / 100.0;
            App.Settings.PlayerVolume = fileCtl.Volume.Value;
        }

        public void ShowFiles()
        {
            var t = LocalPlayer.Current;
            bool any = LocalPlayer.Tracks.Count > 0;
            fileCtl.ShowState(LocalPlayer.Playing, any, t != null, any, LocalPlayer.Shuffle, any, LocalPlayer.Repeat, any);
            fileCtl.ShowPosition(LocalPlayer.Position, LocalPlayer.Duration, t != null);
            fileCtl.ShowVolume((int)Math.Round(LocalPlayer.Volume * 100), L.T("pl.fileVolume"));
            if (t != shownTrack)
            {
                shownTrack = t;
                miniCover.Image = null;
                if (t != null)
                {
                    if (list.SelectedIndex != LocalPlayer.Index && LocalPlayer.Index < list.Items.Count) list.SelectedIndex = LocalPlayer.Index;
                    var track = t;
                    Background.Run(this, () => LocalPlayer.LoadCover(track), img => { if (shownTrack == track) miniCover.Image = img; }, ex => { });
                }
                list.Invalidate();
            }
            miniTitle.Text = t != null ? t.Title : L.T("pl.pickTrack");
            miniArtist.Text = t != null ? t.Artist : "";
        }
    }
}
