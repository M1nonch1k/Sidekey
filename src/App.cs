using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Sidekey
{
    static class App
    {
        public static AppSettings Settings;
        public static NotesData Notes;
        public static LaunchData Launch;
        public static Tracker Tracker;
        public static MainForm Main;
        public static NotifyIcon Tray;
        public static Icon AppIcon;
        public static bool SelfTesting, Exiting, RestartRequested;

        // Выход с последующим запуском новой копии (после того как текущая освободит мьютекс).
        public static void Restart()
        {
            RestartRequested = true;
            Exit();
        }

        const int HotkeyId = 0x5041;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "Sidekey";
        const string OldRunName = "PC Assistant"; // имя до переименования (версия 0.1)
        static ToolStripMenuItem trackingItem;

        // Окно-владелец для диалогов: активный диалог или видимая панель.
        public static IWin32Window Owner
        {
            get
            {
                if (Form.ActiveForm != null) return Form.ActiveForm;
                return Main != null && Main.Visible ? Main : null;
            }
        }

        public static void Start(bool hidden)
        {
            Settings = Store.Load<AppSettings>("settings.json");
            // Язык по умолчанию — английский; у тех, кто уже пользовался программой до появления
            // выбора языка (интерфейс был только русским), остаётся русский.
            if (string.IsNullOrEmpty(Settings.Language)) Settings.Language = Settings.FirstRunDone ? "ru" : "en";
            L.Load(Settings.Language);
            Theme.UseFontsFor(L.Code);
            Notes = Store.Load<NotesData>("notes.json");
            if (Notes.Notes == null) Notes.Notes = new List<Note>();
            Launch = Store.Load<LaunchData>("launch.json");
            if (Launch.Sets == null) Launch.Sets = new List<LaunchSet>();
            foreach (var s in Launch.Sets) if (s.Items == null) s.Items = new List<LaunchItem>();

            AppIcon = Theme.MakeIcon(Theme.S(32));
            if (!SelfTesting) MigrateAutostart();
            Tracker = new Tracker(Store.Load<UsageData>("usage.json"));
            Tracker.Enabled = Settings.TrackingEnabled;
            Tracker.IdleMinutes = Settings.IdleMinutes;
            Tracker.Start();

            Main = new MainForm();
            IntPtr handle = Main.Handle; // окно нужно для горячей клавиши, даже если панель скрыта
            if (!SelfTesting) CreateTray();

            bool hotkeyOk = TryHotkey(Settings.HotkeyMods, Settings.HotkeyVk);
            var launchFailed = RegisterLaunchHotkeys();
            SystemEvents.SessionEnding += (s, e) => SaveAll();
            Application.ApplicationExit += (s, e) => SaveAll();
            if (SelfTesting) return;

            if (!Settings.FirstRunDone)
            {
                Main.ShowPanel();
                Main.BeginInvoke(new Action(FirstRun));
            }
            else if (!hidden) Main.ShowPanel();

            if (!hotkeyOk)
                Notify(L.T("notify.hotkeyFailTitle"), L.F("notify.hotkeyFail", HotkeyText()));
            else if (launchFailed.Count > 0)
                Notify(L.T("notify.launchFailTitle"), L.F("notify.launchFail", string.Join(", ", launchFailed)));
        }

        static void CreateTray()
        {
            var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Theme.Base, ShowImageMargin = false };
            var open = new ToolStripMenuItem(L.T("tray.open"), null, (s, e) => Main.ShowPanel()) { Font = Theme.Bold };
            var settings = new ToolStripMenuItem(L.T("tray.settings"), null, (s, e) => OpenSettings());
            trackingItem = new ToolStripMenuItem("", null, (s, e) => SetTracking(!Settings.TrackingEnabled));
            var exit = new ToolStripMenuItem(L.T("tray.exit"), null, (s, e) => Exit());
            menu.Items.AddRange(new ToolStripItem[] { open, settings, trackingItem, new ToolStripSeparator(), exit });
            foreach (ToolStripItem i in menu.Items) i.ForeColor = Theme.Text;

            Tray = new NotifyIcon { Icon = Theme.MakeIcon(SystemInformation.SmallIconSize.Width), ContextMenuStrip = menu, Visible = true };
            Tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Main.TogglePanel(); };
            UpdateTrackingUi();
            SetTrayStatus(null);
        }

        static void FirstRun()
        {
            Settings.FirstRunDone = true;
            SaveSettings();
            bool pick = Dlg.Ask(L.F("welcome.text", HotkeyText()), L.T("welcome.pickCity"), L.T("welcome.later"));
            if (pick) PickCity(Owner);
        }

        public static string HotkeyText() { return Fmt.Hotkey(Settings.HotkeyMods, Settings.HotkeyVk); }

        public static bool TryHotkey(uint mods, uint vk)
        {
            Native.UnregisterHotKey(Main.Handle, HotkeyId);
            return Native.RegisterHotKey(Main.Handle, HotkeyId, mods | Native.MOD_NOREPEAT, vk);
        }

        // Пока открыто окно выбора сочетания, все сочетания сняты, чтобы их можно было нажать в поле.
        public static void SuspendHotkey()
        {
            Native.UnregisterHotKey(Main.Handle, HotkeyId);
            UnregisterLaunchHotkeys();
        }

        public static bool ResumeHotkey()
        {
            bool ok = TryHotkey(Settings.HotkeyMods, Settings.HotkeyVk);
            RegisterLaunchHotkeys();
            Main.UpdateHotkeyHint();
            return ok;
        }

        // ---- Сочетания клавиш кнопок быстрого запуска
        const int LaunchHotkeyBase = 0x5100;
        static readonly Dictionary<int, LaunchSet> launchHotkeys = new Dictionary<int, LaunchSet>();

        static void UnregisterLaunchHotkeys()
        {
            foreach (int id in launchHotkeys.Keys) Native.UnregisterHotKey(Main.Handle, id);
            launchHotkeys.Clear();
        }

        // Регистрирует сочетания всех кнопок; возвращает те, что заняты другими программами.
        public static List<string> RegisterLaunchHotkeys()
        {
            UnregisterLaunchHotkeys();
            var failed = new List<string>();
            int id = LaunchHotkeyBase;
            foreach (var set in Launch.Sets)
            {
                if (!set.HasHotkey()) continue;
                if (Native.RegisterHotKey(Main.Handle, id, set.HotkeyMods | Native.MOD_NOREPEAT, set.HotkeyVk))
                    launchHotkeys[id++] = set;
                else
                    failed.Add(L.F("hk.buttonFailed", Fmt.Hotkey(set.HotkeyMods, set.HotkeyVk), set.Name));
            }
            return failed;
        }

        public static LaunchSet LaunchSetForHotkey(int id)
        {
            LaunchSet set;
            return launchHotkeys.TryGetValue(id, out set) ? set : null;
        }

        public static void OnHotkey(int id)
        {
            if (id == HotkeyId) { Main.TogglePanel(); return; }
            var set = LaunchSetForHotkey(id);
            if (set != null) Main.LaunchPage.Run(set);
        }

        // Кому уже принадлежит сочетание внутри программы (null — свободно).
        // exceptSetId — кнопка, которую сейчас редактируют; mainToo — проверять ли сочетание панели.
        public static string HotkeyOwner(uint mods, uint vk, string exceptSetId, bool mainToo)
        {
            if (mainToo && mods == Settings.HotkeyMods && vk == Settings.HotkeyVk) return L.T("hk.ownerPanel");
            foreach (var set in Launch.Sets)
                if (set.Id != exceptSetId && set.HasHotkey() && set.HotkeyMods == mods && set.HotkeyVk == vk)
                    return L.F("hk.ownerButton", set.Name);
            return null;
        }

        // Проверяет, не занято ли сочетание другой программой (все наши сочетания при этом сняты).
        public static bool HotkeyFree(uint mods, uint vk)
        {
            const int probe = 0x5FFF;
            if (!Native.RegisterHotKey(Main.Handle, probe, mods | Native.MOD_NOREPEAT, vk)) return false;
            Native.UnregisterHotKey(Main.Handle, probe);
            return true;
        }

        public static void OpenSettings()
        {
            foreach (Form f in Application.OpenForms)
                if (f.Modal && f.Visible) { f.Activate(); return; }
            Main.ShowPanel();
            using (var f = new SettingsForm()) f.ShowDialog(Main);
            Main.TimePage.RefreshData();
        }

        public static bool PickCity(IWin32Window owner)
        {
            using (var f = new CityPickerForm(""))
            {
                if (f.ShowDialog(owner) != DialogResult.OK || f.Result == null) return false;
                Settings.CityName = f.Result.Name;
                Settings.CityLabel = f.Result.Label;
                Settings.Lat = f.Result.Lat;
                Settings.Lon = f.Result.Lon;
                SaveSettings();
                Main.WeatherPage.CityChanged();
                Main.SelectTab(4);
                return true;
            }
        }

        public static void SetTracking(bool on)
        {
            Settings.TrackingEnabled = on;
            Tracker.Enabled = on;
            Tracker.Tick();
            SaveSettings();
            UpdateTrackingUi();
            if (Main != null) Main.TimePage.RefreshData();
        }

        static void UpdateTrackingUi()
        {
            if (trackingItem != null)
                trackingItem.Text = Settings.TrackingEnabled ? L.T("tray.pause") : L.T("tray.resume");
        }

        public static void ClearStats()
        {
            if (!Dlg.Ask(L.T("stats.clearAsk"), L.T("common.delete"))) return;
            Tracker.ClearAll();
            Main.TimePage.RefreshData();
            Dlg.Info(L.T("stats.cleared"));
        }

        public static void SetTrayStatus(string status)
        {
            if (Tray == null) return;
            string text = L.T("app.name") + " — " + (status ?? HotkeyText());
            Tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        public static void Notify(string title, string text)
        {
            if (Tray != null) Tray.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
        }

        public static void PlayAlarm()
        {
            if (SelfTesting) return;
            try
            {
                string wav = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Media\Alarm01.wav");
                if (File.Exists(wav)) new SoundPlayer(wav).Play();
                else SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        // Если автозапуск был включён под старым именем, переносим его на новый .exe
        // (пользователь уже давал на него согласие; старая запись указывает на несуществующий файл).
        static void MigrateAutostart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null || k.GetValue(OldRunName) == null) return;
                    k.DeleteValue(OldRunName, false);
                    k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --hidden");
                }
            }
            catch (Exception ex) { Store.Log("Could not migrate autostart: " + ex.Message); }
        }

        public static bool AutostartEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            catch { return false; }
        }

        public static void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --hidden");
                else if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
            }
        }

        public static void SaveSettings() { Try(L.T("save.settings"), () => Store.Save("settings.json", Settings)); }
        public static bool SaveNotes() { return Try(L.T("save.notes"), () => Store.Save("notes.json", Notes)); }
        public static void SaveLaunch() { Try(L.T("save.launch"), () => Store.Save("launch.json", Launch)); }

        static bool Try(string what, Action save)
        {
            try { save(); return true; }
            catch (Exception ex)
            {
                Store.Log("Could not save " + what + ": " + ex);
                if (!SelfTesting) Notify(L.T("save.errorTitle"), L.F("save.error", what, ex.Message));
                return false;
            }
        }

        public static void SaveAll()
        {
            if (Main != null) Main.NotesPage.Flush();
            Tracker.Save();
            SaveSettings();
        }

        public static void Exit()
        {
            Exiting = true;
            Tracker.Stop();
            SaveAll();
            if (Main != null && Main.IsHandleCreated) SuspendHotkey();
            if (Tray != null) { Tray.Visible = false; Tray.Dispose(); }
            Application.Exit();
        }
    }
}
