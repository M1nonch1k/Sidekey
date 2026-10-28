using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sidekey
{
    class SettingsForm : DarkForm
    {
        readonly HotkeyBox hotkey = new HotkeyBox();
        readonly Label hotkeyWarn, cityLabel;
        readonly CheckBox tracking, autostart;
        readonly NumberBox idle;
        readonly ComboBox language = new DarkCombo(), theme = new DarkCombo();

        public SettingsForm() : base(L.T("set.title"), 470)
        {
            var s = App.Settings;

            Section(L.T("set.language"));
            for (int i = 0; i < L.Count; i++) language.Items.Add(L.Languages[i, 1]);
            language.SelectedIndex = Math.Max(0, L.IndexOf(L.Code));
            language.Width = Theme.S(220);
            language.Margin = Theme.P(0, 0, 0, 4);
            Body.Auto(language);
            Body.Auto(Theme.Wrap(L.T("set.languageHint"), Theme.Small, Theme.Muted));

            Section(L.T("set.theme"));
            foreach (var t in Theme.Names) theme.Items.Add(L.T("theme." + t));
            theme.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.Names, s.Theme));
            theme.Width = Theme.S(220);
            theme.Margin = Theme.P(0, 0, 0, 4);
            Body.Auto(theme);
            Body.Auto(Theme.Wrap(L.T("set.themeHint"), Theme.Small, Theme.Muted));
            var resetPanel = Theme.Btn(L.T("set.resetPanel"));
            resetPanel.Click += (o, e) => App.Main.ResetPlacement();
            Body.Auto(resetPanel).Margin = Theme.P(0, 4, 0, 0);

            Section(L.T("set.hotkey"));
            var defaultBtn = Theme.Btn(L.T("set.default"));
            defaultBtn.Click += (o, e) => hotkey.Set(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.T);
            Body.Auto(Theme.Line(hotkey, defaultBtn));
            Body.Auto(Theme.Wrap(L.T("set.hotkeyHint"), Theme.Small, Theme.Muted));
            hotkeyWarn = Body.Auto(Theme.Wrap("", Theme.Small, Theme.Warn));
            hotkey.HotkeyChanged += (o, e) => UpdateWarning();
            hotkey.Set(s.HotkeyMods, s.HotkeyVk);

            Section(L.T("set.tracking"));
            tracking = Body.Auto(Theme.Check(L.T("set.trackingCheck"), s.TrackingEnabled));
            idle = Theme.Number(0, 240, s.IdleMinutes);
            Body.Auto(Theme.Flow(Theme.Inline(L.T("set.idle")), idle, Theme.Inline(L.T("common.min"))));
            Body.Auto(Theme.Wrap(L.T("set.idleHint"), Theme.Small, Theme.Muted));

            Section(L.T("set.weather"));
            cityLabel = Theme.Lbl(s.HasCity() ? s.CityLabel : L.T("wp.noCity"), Theme.Base, Theme.Text);
            cityLabel.AutoEllipsis = true;
            var cityBtn = Theme.Btn(L.T("common.change"));
            cityBtn.Click += (o, e) => { if (App.PickCity(this)) cityLabel.Text = App.Settings.CityLabel; };
            Body.Auto(Theme.Line(cityLabel, cityBtn));

            Section(L.T("set.autostart"));
            autostart = Body.Auto(Theme.Check(L.T("set.autostartCheck"), App.AutostartEnabled()));
            Body.Auto(Theme.Wrap(L.T("set.autostartHint"), Theme.Small, Theme.Muted));

            Section(L.T("set.data"));
            Body.Auto(Theme.Wrap(L.F("set.dataHint", Store.DataDir), Theme.Small, Theme.Muted));
            var openBtn = Theme.Btn(L.T("set.openFolder"));
            openBtn.Click += (o, e) =>
            {
                Directory.CreateDirectory(Store.DataDir);
                Process.Start("explorer.exe", "\"" + Store.DataDir + "\"");
            };
            var clearBtn = Theme.Btn(L.T("time.clear"));
            clearBtn.Click += (o, e) => App.ClearStats();
            Body.Auto(Theme.Flow(openBtn, clearBtn));

            var save = Theme.Btn(L.T("common.save"), true);
            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            save.Click += (o, e) => Save();
            Buttons(save, cancel);
        }

        void UpdateWarning()
        {
            if (!Fmt.ValidHotkey(hotkey.Mods, hotkey.Vk))
            {
                hotkeyWarn.Text = L.T("set.needMod");
                return;
            }
            hotkeyWarn.Text = Fmt.HotkeyConflict(hotkey.Mods, hotkey.Vk) ?? "";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Пока окно открыто, текущее сочетание снято, чтобы его можно было нажать в поле.
            App.SuspendHotkey();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            App.ResumeHotkey();
            base.OnFormClosed(e);
        }

        void Save()
        {
            var s = App.Settings;
            if (!Fmt.ValidHotkey(hotkey.Mods, hotkey.Vk))
            {
                Dlg.Info(L.T("hk.invalid"));
                return;
            }
            string owner = App.HotkeyOwner(hotkey.Mods, hotkey.Vk, null, false);
            if (owner != null)
            {
                Dlg.Info(L.F("hk.taken", Fmt.Hotkey(hotkey.Mods, hotkey.Vk), owner));
                return;
            }
            bool changed = hotkey.Mods != s.HotkeyMods || hotkey.Vk != s.HotkeyVk;
            string conflict = Fmt.HotkeyConflict(hotkey.Mods, hotkey.Vk);
            if (changed && conflict != null && !Dlg.Ask(L.F("hk.assignAsk", conflict), L.T("hk.assign"), L.T("hk.chooseOther")))
                return;
            if (!App.TryHotkey(hotkey.Mods, hotkey.Vk))
            {
                App.SuspendHotkey();
                Dlg.Info(L.F("hk.busy", Fmt.Hotkey(hotkey.Mods, hotkey.Vk)));
                return;
            }
            s.HotkeyMods = hotkey.Mods;
            s.HotkeyVk = hotkey.Vk;
            s.IdleMinutes = (int)idle.Value;
            App.Tracker.IdleMinutes = s.IdleMinutes;
            if (autostart.Checked != App.AutostartEnabled())
            {
                try { App.SetAutostart(autostart.Checked); }
                catch (Exception ex) { Dlg.Info(L.F("set.autostartFail", ex.Message)); }
            }
            App.SetTracking(tracking.Checked);
            string lang = L.Languages[Math.Max(0, language.SelectedIndex), 0];
            bool langChanged = lang != s.Language;
            s.Language = lang;
            string themeName = Theme.Names[Math.Max(0, theme.SelectedIndex)];
            bool themeChanged = themeName != (s.Theme ?? "dark");
            s.Theme = themeName;
            App.SaveSettings();
            App.SetTrayStatus(null);
            DialogResult = DialogResult.OK;
            Close();
            // Интерфейс собирается при запуске, поэтому новый язык и тема применяются после перезапуска.
            if ((langChanged || themeChanged) && !App.SelfTesting && Dlg.Ask(L.T("set.restartAsk"), L.T("set.restart"), L.T("welcome.later")))
                App.Restart();
        }
    }

    class CityPickerForm : DarkForm
    {
        public CityResult Result;
        readonly TextBox query = Theme.Field();
        readonly Button find = Theme.Btn(L.T("city.find"), true), ok = Theme.Btn(L.T("city.choose"), true);
        readonly ListBox results = new ListBox();
        readonly Label status;
        readonly string initial;

        public CityPickerForm(string initialQuery) : base(L.T("city.title"), 440)
        {
            initial = initialQuery;
            Body.Auto(Theme.Lbl(L.T("city.prompt"), Theme.Base, Theme.Text));
            Body.Auto(Theme.Line(query, find));
            results.BackColor = Theme.Surface;
            results.ForeColor = Theme.Text;
            results.BorderStyle = BorderStyle.None;
            results.Font = Theme.Base;
            results.IntegralHeight = false;
            results.ItemHeight = Theme.S(22);
            results.Margin = Theme.P(0, 4, 0, 8);
            results.HandleCreated += (s, e) => Native.DarkTheme(results.Handle, "DarkMode_Explorer");
            Body.Fixed(results, Theme.S(150));
            status = Body.Auto(Theme.Wrap(L.T("city.hint"), Theme.Small, Theme.Muted));

            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            ok.Enabled = false;
            ok.Click += (s, e) => Choose();
            Buttons(ok, cancel);

            find.Click += (s, e) => Search();
            query.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Search(); }
            };
            results.SelectedIndexChanged += (s, e) => ok.Enabled = results.SelectedItem != null;
            results.DoubleClick += (s, e) => Choose();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            query.Focus();
            if (!string.IsNullOrWhiteSpace(initial)) { query.Text = initial; Search(); }
        }

        void Choose()
        {
            Result = results.SelectedItem as CityResult;
            if (Result == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        void Search()
        {
            string q = query.Text.Trim();
            status.ForeColor = Theme.Muted;
            if (q.Length < 2) { status.Text = L.T("city.short"); return; }
            find.Enabled = false;
            status.Text = L.T("city.searching");
            Background.Run(this, () => Weather.FindCity(q), list =>
            {
                find.Enabled = true;
                results.Items.Clear();
                foreach (var c in list) results.Items.Add(c);
                if (list.Count == 0) status.Text = L.T("city.none");
                else
                {
                    results.SelectedIndex = 0;
                    status.Text = L.F("city.found", list.Count);
                }
            }, ex =>
            {
                find.Enabled = true;
                status.ForeColor = Theme.Warn;
                status.Text = Weather.Explain(ex);
            });
        }
    }

    class LaunchEditForm : DarkForm
    {
        public LaunchSet Result;
        readonly string id;
        readonly List<LaunchItem> items;
        readonly TextBox name = Theme.Field();
        readonly TwoLineList list = new TwoLineList();
        readonly Label error;
        readonly Button hotkeyBtn;
        uint hotkeyMods, hotkeyVk;

        public LaunchEditForm(LaunchSet set) : base(set == null ? L.T("le.new") : L.T("le.edit"), 540)
        {
            id = set != null ? set.Id : Guid.NewGuid().ToString("N");
            items = set != null ? set.Items.Select(i => i.Clone()).ToList() : new List<LaunchItem>();

            Body.Auto(Theme.Lbl(L.T("le.name"), Theme.Base, Theme.Text));
            name.Text = set != null ? set.Name : "";
            name.Dock = DockStyle.Fill;
            name.Margin = Theme.P(0, 0, 0, 10);
            Body.Auto(name);

            Body.Auto(Theme.Lbl(L.T("le.items"), Theme.Base, Theme.Text));
            list.First = o =>
            {
                var it = (LaunchItem)o;
                return it.KindLabel() + ": " + it.Title() + (it.Delay > 0 ? "   ·   " + L.F("le.delay", it.Delay) : "");
            };
            list.Second = o =>
            {
                var it = (LaunchItem)o;
                return it.Path + (string.IsNullOrEmpty(it.Args) ? "" : "   " + it.Args);
            };
            list.Margin = Theme.P(0, 0, 0, 8);
            list.DoubleClick += (s, e) => EditItem();
            Body.Fixed(list, Theme.S(176));

            var addApp = Theme.Btn(L.T("le.addApp"));
            var addUrl = Theme.Btn(L.T("le.addUrl"));
            var edit = Theme.Btn(L.T("le.editItem"));
            var remove = Theme.Btn(L.T("le.remove"));
            var delay = Theme.Btn(L.T("le.delayBtn"));
            Theme.Tips.SetToolTip(delay, L.T("le.delayTip"));
            delay.Click += (s, e) => SetDelay();
            hotkeyMods = set != null ? set.HotkeyMods : 0;
            hotkeyVk = set != null ? set.HotkeyVk : 0;
            hotkeyBtn = Theme.Btn("");
            Theme.Tips.SetToolTip(hotkeyBtn, L.T("le.hotkeyTip"));
            hotkeyBtn.Click += (s, e) => SetHotkey();
            UpdateHotkeyButton();
            var up = Theme.IconBtn("", L.T("le.up"), Theme.Input);
            var down = Theme.IconBtn("", L.T("le.down"), Theme.Input);
            up.Margin = Theme.P(0, 0, 6, 6);
            down.Margin = Theme.P(0, 0, 6, 6);
            addApp.Click += (s, e) => AddItem(false);
            addUrl.Click += (s, e) => AddItem(true);
            edit.Click += (s, e) => EditItem();
            remove.Click += (s, e) => RemoveItem();
            up.Click += (s, e) => MoveItem(-1);
            down.Click += (s, e) => MoveItem(1);
            Body.Auto(Theme.Flow(addApp, addUrl, edit, remove, up, down, delay, hotkeyBtn));
            Body.Auto(Theme.Wrap(L.T("le.hint"), Theme.Small, Theme.Muted));
            error = Body.Auto(Theme.Wrap("", Theme.Small, Theme.Danger));

            var save = Theme.Btn(L.T("common.save"), true);
            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            save.Click += (s, e) => Save();
            Buttons(save, cancel);
            RefreshList(-1);
        }

        void RefreshList(int select)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var it in items) list.Items.Add(it);
            list.EndUpdate();
            if (select >= 0 && select < list.Items.Count) list.SelectedIndex = select;
        }

        void AddItem(bool url)
        {
            using (var f = new ItemEditForm(null, url))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                items.Add(f.Result);
                RefreshList(items.Count - 1);
                error.Text = "";
            }
        }

        void EditItem()
        {
            int i = list.SelectedIndex;
            if (i < 0) { error.Text = L.T("le.selectRow"); return; }
            using (var f = new ItemEditForm(items[i], items[i].IsUrl()))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                f.Result.Delay = items[i].Delay;
                items[i] = f.Result;
                RefreshList(i);
            }
        }

        void UpdateHotkeyButton()
        {
            hotkeyBtn.Text = L.F("le.hotkeyBtn", hotkeyVk == 0 ? L.T("le.none") : Fmt.Hotkey(hotkeyMods, hotkeyVk));
        }

        void SetHotkey()
        {
            using (var f = new LaunchHotkeyForm(id, hotkeyMods, hotkeyVk))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                hotkeyMods = f.Mods;
                hotkeyVk = f.Vk;
                UpdateHotkeyButton();
            }
            // Сочетание работает, только пока программа запущена — предлагаем автозапуск.
            if (hotkeyVk != 0 && !App.SelfTesting && !App.AutostartEnabled() &&
                Dlg.Ask(L.F("le.autostartAsk", Fmt.Hotkey(hotkeyMods, hotkeyVk)), L.T("le.enableAutostart"), L.T("le.notNow")))
            {
                try { App.SetAutostart(true); }
                catch (Exception ex) { Dlg.Info(L.F("set.autostartFail", ex.Message)); }
            }
        }

        void SetDelay()
        {
            int i = list.SelectedIndex;
            if (i < 0) { error.Text = L.T("le.selectRow"); return; }
            using (var f = new DelayForm(items[i]))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                items[i].Delay = f.Seconds;
                RefreshList(i);
            }
        }

        void RemoveItem()
        {
            int i = list.SelectedIndex;
            if (i < 0) { error.Text = L.T("le.selectRow"); return; }
            items.RemoveAt(i);
            RefreshList(Math.Min(i, items.Count - 1));
        }

        void MoveItem(int dir)
        {
            int i = list.SelectedIndex, j = i + dir;
            if (i < 0 || j < 0 || j >= items.Count) return;
            var x = items[i];
            items[i] = items[j];
            items[j] = x;
            RefreshList(j);
        }

        void Save()
        {
            string n = name.Text.Trim();
            if (n.Length == 0) { error.Text = L.T("le.needName"); name.Focus(); return; }
            if (items.Count == 0) { error.Text = L.T("le.needItem"); return; }
            Result = new LaunchSet { Id = id, Name = n, Items = items, HotkeyMods = hotkeyMods, HotkeyVk = hotkeyVk };
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    class ItemEditForm : DarkForm
    {
        public LaunchItem Result;
        readonly bool isUrl;
        readonly TextBox path = Theme.Field(), args = Theme.Field();
        readonly Label error;

        public ItemEditForm(LaunchItem item, bool url) : base(url ? L.T("ie.urlTitle") : L.T("ie.appTitle"), 500)
        {
            isUrl = url;
            if (!url)
            {
                Body.Auto(Theme.Lbl(L.T("ie.appLabel"), Theme.Base, Theme.Text));
                var browse = Theme.Btn(L.T("ie.browse"));
                browse.Click += (s, e) => Browse();
                Body.Auto(Theme.Line(path, browse));
                Body.Auto(Theme.Lbl(L.T("ie.args"), Theme.Base, Theme.Text));
                args.Dock = DockStyle.Fill;
                Body.Auto(args);
                Body.Auto(Theme.Wrap(L.T("ie.argsHint"), Theme.Small, Theme.Muted));
            }
            else
            {
                Body.Auto(Theme.Lbl(L.T("ie.urlLabel"), Theme.Base, Theme.Text));
                path.Dock = DockStyle.Fill;
                Body.Auto(path);
                Body.Auto(Theme.Wrap(L.T("ie.urlHint"), Theme.Small, Theme.Muted));
            }
            error = Body.Auto(Theme.Wrap("", Theme.Small, Theme.Danger));

            if (item != null)
            {
                path.Text = item.Path;
                args.Text = item.Args ?? "";
            }

            var ok = Theme.Btn(L.T("common.done"), true);
            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            AcceptButton = ok;
            ok.Click += (s, e) => Ok();
            Buttons(ok, cancel);
        }

        void Browse()
        {
            using (var d = new OpenFileDialog
            {
                Title = L.T("ie.dialogTitle"),
                Filter = L.T("ie.filter"),
                DereferenceLinks = false,
                CheckFileExists = true
            })
            {
                try
                {
                    string current = Launcher.Expand(path.Text);
                    if (current.Length > 0 && File.Exists(current)) d.InitialDirectory = Path.GetDirectoryName(current);
                }
                catch { }
                if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName;
            }
        }

        void Ok()
        {
            string p = path.Text.Trim().Trim('"');
            if (p.Length == 0) { error.Text = isUrl ? L.T("ie.needUrl") : L.T("ie.needPath"); return; }
            if (isUrl)
            {
                string u = Launcher.NormalizeUrl(p);
                if (u == null) { error.Text = L.T("ie.badUrl"); return; }
                p = u;
            }
            else
            {
                string full = Launcher.Expand(p);
                if (!File.Exists(full) && !Directory.Exists(full) &&
                    !Dlg.Ask(L.F("ie.notFoundAsk", full), L.T("common.save"), L.T("ie.fix")))
                    return;
            }
            Result = new LaunchItem { Kind = isUrl ? "url" : "app", Path = p, Args = isUrl ? "" : args.Text.Trim() };
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // Задержка перед запуском одного пункта кнопки, в секундах (0 — без задержки).
    class DelayForm : DarkForm
    {
        public int Seconds;
        readonly NumberBox value;

        public DelayForm(LaunchItem item) : base(L.T("delay.title"), 420)
        {
            Body.Auto(Theme.Wrap(L.F("delay.text", item.Title()), Theme.Base, Theme.Text));
            value = Theme.Number(0, 600, item.Delay);
            Body.Auto(Theme.Flow(value, Theme.Inline(L.T("delay.seconds"))));
            Body.Auto(Theme.Wrap(L.T("delay.hint"), Theme.Small, Theme.Muted));

            var ok = Theme.Btn(L.T("common.done"), true);
            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            AcceptButton = ok;
            ok.Click += (s, e) =>
            {
                Seconds = (int)value.Value;
                DialogResult = DialogResult.OK;
                Close();
            };
            Buttons(ok, cancel);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            value.Focus();
        }
    }

    // Сочетание клавиш для кнопки быстрого запуска (по умолчанию нет).
    class LaunchHotkeyForm : DarkForm
    {
        public uint Mods, Vk;
        readonly HotkeyBox box = new HotkeyBox();
        readonly Label warn;
        readonly string setId;

        public LaunchHotkeyForm(string setId, uint mods, uint vk) : base(L.T("lh.title"), 440)
        {
            this.setId = setId;
            Body.Auto(Theme.Wrap(L.T("lh.text"), Theme.Base, Theme.Text));
            var clear = Theme.Btn(L.T("lh.clear"));
            clear.Click += (s, e) => { box.Mods = 0; box.Vk = 0; box.Text = L.T("lh.notSet"); warn.Text = ""; };
            Body.Auto(Theme.Line(box, clear));
            Body.Auto(Theme.Wrap(L.T("lh.hint"), Theme.Small, Theme.Muted));
            warn = Body.Auto(Theme.Wrap("", Theme.Small, Theme.Warn));
            box.HotkeyChanged += (s, e) =>
            {
                if (!Fmt.ValidHotkey(box.Mods, box.Vk)) warn.Text = L.T("lh.needMod");
                else warn.Text = Fmt.HotkeyConflict(box.Mods, box.Vk) ?? "";
            };
            if (vk != 0) box.Set(mods, vk);
            else box.Text = L.T("lh.notSet");

            var ok = Theme.Btn(L.T("common.done"), true);
            var cancel = Theme.Btn(L.T("common.cancel"));
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            ok.Click += (s, e) => Ok();
            Buttons(ok, cancel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            App.SuspendHotkey();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            App.ResumeHotkey();
            base.OnFormClosed(e);
        }

        void Ok()
        {
            if (box.Vk != 0)
            {
                if (!Fmt.ValidHotkey(box.Mods, box.Vk)) { Dlg.Info(L.T("hk.invalid")); return; }
                string owner = App.HotkeyOwner(box.Mods, box.Vk, setId, true);
                if (owner != null) { Dlg.Info(L.F("hk.taken", Fmt.Hotkey(box.Mods, box.Vk), owner)); return; }
                if (!App.HotkeyFree(box.Mods, box.Vk))
                {
                    Dlg.Info(L.F("hk.busy", Fmt.Hotkey(box.Mods, box.Vk)));
                    return;
                }
                string conflict = Fmt.HotkeyConflict(box.Mods, box.Vk);
                if (conflict != null && !Dlg.Ask(L.F("hk.assignAsk", conflict), L.T("hk.assign"), L.T("hk.chooseOther"))) return;
            }
            Mods = box.Vk == 0 ? 0 : box.Mods;
            Vk = box.Vk;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
