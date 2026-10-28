using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Sidekey
{
    // Самопроверка: Sidekey.exe --selftest <папка>
    // Работает на тестовых данных в указанной папке, ничего не запускает,
    // сохраняет снимки вкладок и отчёт report.txt.
    static class SelfTest
    {
        static readonly StringBuilder log = new StringBuilder();
        static int failures;

        static void Check(string name, bool ok, string detail = null)
        {
            log.AppendLine((ok ? "[OK]   " : "[FAIL] ") + name + (detail != null ? " — " + detail : ""));
            if (!ok) failures++;
        }

        public static int Run(string outDir)
        {
            App.SelfTesting = true;
            Directory.CreateDirectory(outDir);
            if (Directory.Exists(Store.DataDir)) Directory.Delete(Store.DataDir, true);
            try { Run2(outDir); }
            catch (Exception ex) { Check("Без исключений", false, ex.ToString()); }
            log.AppendLine();
            log.AppendLine(failures == 0 ? "ИТОГ: все проверки пройдены" : "ИТОГ: ошибок — " + failures);
            File.WriteAllText(Path.Combine(outDir, "report.txt"), log.ToString(), Encoding.UTF8);
            return failures;
        }

        static void Run2(string outDir)
        {
            // --- тестовые данные
            Store.Save("settings.json", new AppSettings { FirstRunDone = true, Language = "en" });
            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var usage = new UsageData();
            usage.Days[today] = new Dictionary<string, double> { { "chrome.exe", 7300 }, { "code.exe", 3650 }, { "cs2.exe", 2400 }, { "telegram.exe", 290 } };
            usage.Days[yesterday] = new Dictionary<string, double> { { "chrome.exe", 3600 }, { "cs2.exe", 5400 } };
            usage.Names["chrome.exe"] = "Google Chrome";
            usage.Names["code.exe"] = "Visual Studio Code";
            usage.Names["cs2.exe"] = "Counter-Strike 2";
            usage.Names["telegram.exe"] = "Telegram Desktop";
            Store.Save("usage.json", usage);
            var notes = new NotesData();
            notes.Notes.Add(new Note { Id = "1", Text = "Buy groceries\nmilk, bread, coffee", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
            notes.Notes.Add(new Note { Id = "2", Text = "Finish the lab report by Friday", Modified = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
            Store.Save("notes.json", notes);
            string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
            var launch = new LaunchData();
            launch.Sets.Add(new LaunchSet { Id = "a", Name = "Study", Items = new List<LaunchItem> {
                new LaunchItem { Kind = "app", Path = notepad },
                new LaunchItem { Kind = "url", Path = "https://chatgpt.com" } } });
            launch.Sets.Add(new LaunchSet { Id = "b", Name = "Test: missing program", Items = new List<LaunchItem> {
                new LaunchItem { Kind = "app", Path = @"C:\No such folder\missing.exe" },
                new LaunchItem { Kind = "url", Path = "https://www.faceit.com" } } });
            Store.Save("launch.json", launch);

            // --- запуск
            App.Start(false);
            Check("Загрузка сохранённых данных", App.Notes.Notes.Count == 2 && App.Launch.Sets.Count == 2 && App.Settings.FirstRunDone);

            bool hk = App.TryHotkey(App.Settings.HotkeyMods, App.Settings.HotkeyVk);
            Check("Регистрация горячей клавиши " + App.HotkeyText(), hk, hk ? "свободна" : "занята другой программой");
            Check("Предупреждение для Ctrl + T", Fmt.HotkeyConflict(Native.MOD_CONTROL, (uint)Keys.T) != null);
            Check("Предупреждение для Ctrl + Shift + T", Fmt.HotkeyConflict(Native.MOD_CONTROL | Native.MOD_SHIFT, (uint)Keys.T) != null);
            Check("Нет предупреждения для Ctrl + Alt + T", Fmt.HotkeyConflict(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.T) == null);
            Check("Сочетание без Ctrl/Alt отклоняется", !Fmt.ValidHotkey(Native.MOD_SHIFT, (uint)Keys.T));

            string key, name;
            bool resolved = App.Tracker.Resolve(out key, out name);
            Check("Определение активной программы", resolved, resolved ? name + " (" + key + ")" : "нет активного окна");

            var st = App.Tracker.GetStats(DateTime.Today, DateTime.Today);
            Check("Статистика за сегодня", st.Rows.Count >= 4 && st.Total >= 13640 && st.Rows[0].Name == "Google Chrome",
                Fmt.Duration(st.Total) + ", первая строка: " + (st.Rows.Count > 0 ? st.Rows[0].Name + " " + Fmt.Duration(st.Rows[0].Seconds) : "-"));
            var wk = App.Tracker.GetStats(DateTime.Today.AddDays(-6), DateTime.Today);
            Check("Статистика за 7 дней включает вчера", Math.Abs(wk.Total - st.Total - 9000) < 120, Fmt.Duration(wk.Total));
            var yst = App.Tracker.GetStats(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1));
            Check("Статистика за вчера", Math.Abs(yst.Total - 9000) < 1 && yst.Rows[0].Name == "Counter-Strike 2", Fmt.Duration(yst.Total));

            App.SetTracking(false);
            App.Tracker.Tick();
            Check("Пауза учёта", App.Tracker.State == "paused");
            App.SetTracking(true);
            Thread.Sleep(1100);
            App.Tracker.Tick();
            Check("Учёт после возобновления", App.Tracker.State != "paused", "состояние: " + App.Tracker.State);
            App.Tracker.Save();
            var reloaded = Store.Load<UsageData>("usage.json");
            Check("Статистика сохраняется в файл", reloaded.Days.ContainsKey(today) && reloaded.Days.ContainsKey(yesterday));

            Check("Нормализация ссылки platonus.kz", Launcher.NormalizeUrl("platonus.kz") == "https://platonus.kz");
            Check("Отклонение неверной ссылки", Launcher.NormalizeUrl("просто текст") == null && Launcher.NormalizeUrl("javascript:alert(1)") == null);
            string missing = Launcher.Check(App.Launch.Sets[1].Items[0]);
            Check("Сообщение об отсутствующей программе", missing != null && missing.Contains("missing.exe") && missing != L.T("launch.notFound"), missing);
            Check("Существующая программа проходит проверку", Launcher.Check(App.Launch.Sets[0].Items[0]) == null, notepad);
            Check("Название ссылки в кнопке", App.Launch.Sets[1].Items[1].Title() == "faceit.com", App.Launch.Sets[1].Items[1].Title());
            TestLaunchDelays();
            TestLaunchHotkeys();

            // --- погода
            try
            {
                var cities = Weather.FindCity("London");
                Check("Поиск города", cities.Count > 0, cities.Count > 0 ? cities[0].Label : "ничего не найдено");
                if (cities.Count > 0)
                {
                    var w = Weather.Fetch(cities[0].Lat, cities[0].Lon);
                    Check("Получение погоды", w.Days.Count >= 3, Weather.Deg(w.Temp) + ", " + Weather.Describe(w.Code) + ", дней в прогнозе: " + w.Days.Count);
                    App.Settings.CityName = cities[0].Name;
                    App.Settings.CityLabel = cities[0].Label;
                    App.Settings.Lat = cities[0].Lat;
                    App.Settings.Lon = cities[0].Lon;
                }
            }
            catch (Exception ex) { Check("Погода", false, Weather.Explain(ex)); }
            try
            {
                Weather.Get("https://no-such-host.invalid/");
                Check("Сообщение при отсутствии соединения", false, "запрос неожиданно прошёл");
            }
            catch (Exception ex)
            {
                string m = Weather.Explain(ex);
                Check("Сообщение при отсутствии соединения", (m == L.T("weather.connectFail") || m == L.T("weather.offline")), m);
            }

            // --- таймер
            var timer = App.Main.TimerPage;
            SetTimerInputs(timer, 0, 2);
            timer.Start();
            var sw = Stopwatch.StartNew();
            while (!timer.Finished && sw.ElapsedMilliseconds < 4000) Pump(50);
            Check("Таймер на 2 секунды завершается", timer.Finished, sw.ElapsedMilliseconds + " мс");
            SetTimerInputs(timer, 5, 0);

            // --- снимки интерфейса
            var main = App.Main;
            main.Bounds = new Rectangle(-6000, -6000, main.Width, Theme.S(660));
            main.Show();
            string[] names = { "1-time", "2-notes", "3-launch", "4-timer", "5-weather" };
            for (int i = 0; i < names.Length; i++)
            {
                main.SelectTab(i);
                Pump(i == 4 ? 4000 : 400);
                Capture(main, Path.Combine(outDir, names[i] + ".png"));
            }
            Check("Погода на вкладке загружена", !main.WeatherPage.IsLoading);

            Snap(new SettingsForm(), Path.Combine(outDir, "6-settings.png"));
            Snap(new LaunchEditForm(App.Launch.Sets[0]), Path.Combine(outDir, "7-launch-edit.png"));
            Snap(new ItemEditForm(App.Launch.Sets[0].Items[0], false), Path.Combine(outDir, "8-item-edit.png"));
            Snap(new CityPickerForm(""), Path.Combine(outDir, "9-city.png"));
            Snap(new LaunchHotkeyForm(App.Launch.Sets[0].Id, Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.D1), Path.Combine(outDir, "11-launch-hotkey.png"));
            Snap(new Dlg(L.T("app.name"), L.F("notes.deleteAsk", "Buy groceries"), L.T("common.delete"), L.T("common.cancel")), Path.Combine(outDir, "10-dialog.png"));
            App.ResumeHotkey();

            TestTranslations(outDir);

            App.Main.NotesPage.Flush();
            App.SaveAll();
            Check("Файлы данных созданы", new[] { "settings.json", "notes.json", "launch.json", "usage.json" }.All(f => File.Exists(Path.Combine(Store.DataDir, f))));

            long mem = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            log.AppendLine("[INFO] Память процесса во время теста: " + mem + " МБ");
            App.Tracker.Stop();
            App.SuspendHotkey();
        }

        // Переводы: в каждом языке есть все ключи английского, с теми же подстановками {0}, {1}…
        // Для каждого языка сохраняются снимки панели и настроек (lang-<код>-*.png).
        static void TestTranslations(string outDir)
        {
            var en = L.Read("en");
            Check("Английский словарь загружен", en.Count > 200, "строк: " + en.Count);
            var placeholder = new System.Text.RegularExpressions.Regex(@"\{\d+\}");
            Func<string, string> marks = s => string.Join(",", placeholder.Matches(s).Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct().OrderBy(x => x));
            for (int i = 0; i < L.Count; i++)
            {
                string code = L.Languages[i, 0];
                var dict = L.Read(code);
                var missing = en.Keys.Where(k => !dict.ContainsKey(k)).ToList();
                var extra = dict.Keys.Where(k => !en.ContainsKey(k)).ToList();
                var badArgs = en.Keys.Where(k => dict.ContainsKey(k) && marks(dict[k]) != marks(en[k])).ToList();
                Check("Перевод " + code + " (" + L.Languages[i, 1] + ")", missing.Count == 0 && extra.Count == 0 && badArgs.Count == 0,
                    "строк: " + dict.Count +
                    (missing.Count > 0 ? "; нет: " + string.Join(", ", missing.Take(5)) : "") +
                    (extra.Count > 0 ? "; лишние: " + string.Join(", ", extra.Take(5)) : "") +
                    (badArgs.Count > 0 ? "; подстановки: " + string.Join(", ", badArgs.Take(5)) : ""));
            }

            string saved = L.Code;
            for (int i = 0; i < L.Count; i++)
            {
                string code = L.Languages[i, 0];
                L.Load(code);
                Theme.UseFontsFor(code);
                var form = new MainForm();
                form.Bounds = new Rectangle(-6000, -6000, form.Width, Theme.S(660));
                form.Show();
                foreach (int tab in new[] { 0, 2 })
                {
                    form.SelectTab(tab);
                    Pump(250);
                    Capture(form, Path.Combine(outDir, "lang-" + code + "-tab" + tab + ".png"));
                }
                form.Dispose();
                Snap(new SettingsForm(), Path.Combine(outDir, "lang-" + code + "-settings.png"));
                App.ResumeHotkey();
            }
            L.Load(saved);
            Theme.UseFontsFor(saved);
            App.Settings.LastTab = 0;
        }

        // Запуск с задержками. Программы не запускаются: Starter подменён заглушкой,
        // которая только записывает, что и через сколько секунд было бы запущено.
        // Сочетания клавиш кнопок: регистрация, поиск кнопки по сочетанию, конфликты.
        // Используется редкое сочетание Ctrl+Alt+Shift+F12; ничего не запускается.
        static void TestLaunchHotkeys()
        {
            var set = App.Launch.Sets[0];
            uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, vk = (uint)Keys.F12;
            Check("По умолчанию у кнопок нет сочетания", !App.Launch.Sets.Any(s => s.HasHotkey()));
            Check("Свободное сочетание проходит проверку", App.HotkeyFree(mods, vk));

            set.HotkeyMods = mods;
            set.HotkeyVk = vk;
            var failed = App.RegisterLaunchHotkeys();
            Check("Сочетание кнопки регистрируется в Windows", failed.Count == 0, string.Join(", ", failed));
            Check("Сочетание находит свою кнопку", App.LaunchSetForHotkey(0x5100) == set);
            Check("Повтор сочетания у другой кнопки обнаруживается",
                App.HotkeyOwner(mods, vk, App.Launch.Sets[1].Id, true) == L.F("hk.ownerButton", set.Name) &&
                App.HotkeyOwner(mods, vk, set.Id, true) == null);
            Check("Совпадение с сочетанием панели обнаруживается",
                App.HotkeyOwner(App.Settings.HotkeyMods, App.Settings.HotkeyVk, null, true) == L.T("hk.ownerPanel"));
            Check("Занятое сочетание не проходит проверку свободы", !App.HotkeyFree(mods, vk));

            App.SuspendHotkey();
            Check("Пока открыт выбор сочетания, оно снято", App.LaunchSetForHotkey(0x5100) == null && App.HotkeyFree(mods, vk));
            App.ResumeHotkey();
            Check("После закрытия выбора сочетание возвращается", App.LaunchSetForHotkey(0x5100) == set);

            App.SaveLaunch();
            var back = Store.Load<LaunchData>("launch.json");
            Check("Сочетание кнопки сохраняется", back.Sets[0].HotkeyVk == vk && back.Sets[0].HotkeyMods == mods && !back.Sets[1].HasHotkey());

            set.HotkeyMods = set.HotkeyVk = 0;
            App.RegisterLaunchHotkeys();
            App.SaveLaunch();
            Check("Сочетание снимается", App.LaunchSetForHotkey(0x5100) == null && App.HotkeyFree(mods, vk));
        }

        static void TestLaunchDelays()
        {
            var set = new LaunchSet { Name = "Games", Items = new List<LaunchItem> {
                new LaunchItem { Kind = "app", Path = @"C:\Program Files\FACEIT AC\faceitclient.exe" },
                new LaunchItem { Kind = "app", Path = @"C:\Program Files (x86)\Steam\steam.exe", Args = "-applaunch 730", Delay = 2 },
                new LaunchItem { Kind = "url", Path = "https://www.faceit.com/ru/matchmaking", Delay = 1 } } };

            var log = new List<string>();
            var sw = Stopwatch.StartNew();
            int startedCount = -1;
            List<string> errs = null;
            var run = new LaunchRun(set, p => { }, (n, e) => { startedCount = n; errs = e; });
            run.Starter = item => { log.Add(item.Title() + "@" + (sw.ElapsedMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)); return null; };
            run.Start();
            while (startedCount < 0 && sw.ElapsedMilliseconds < 10000) Pump(50);
            string order = string.Join(" → ", log);
            Func<int, double> at = i => double.Parse(log[i].Split('@')[1], CultureInfo.InvariantCulture);
            Check("Задержки: порядок и время запуска", startedCount == 3 && errs.Count == 0 && log.Count == 3 &&
                at(0) < 0.5 && at(1) >= 1.8 && at(1) < 3 && at(2) >= 2.8 && at(2) < 4.5, order);

            // Без задержек всё запускается сразу, как раньше.
            log.Clear();
            sw.Restart();
            startedCount = -1;
            var plain = new LaunchSet { Items = set.Items.Select(i => { var c = i.Clone(); c.Delay = 0; return c; }).ToList() };
            run = new LaunchRun(plain, p => { }, (n, e) => { startedCount = n; });
            run.Starter = item => { log.Add(item.Title()); return null; };
            run.Start();
            Check("Без задержек — всё сразу", startedCount == 3, string.Join(" → ", log));

            // Ошибка одного пункта не останавливает остальные (как и раньше).
            startedCount = -1;
            run = new LaunchRun(plain, p => { }, (n, e) => { startedCount = n; errs = e; });
            run.Starter = item => item.IsUrl() ? null : item.Title() + ": not found";
            run.Start();
            Check("Ошибки собираются, остальное запускается", startedCount == 1 && errs.Count == 2);

            // Задержка сохраняется в файл и восстанавливается.
            Store.Save("delay-test.json", new LaunchData { Sets = new List<LaunchSet> { set } });
            var back = Store.Load<LaunchData>("delay-test.json");
            Check("Задержка сохраняется", back.Sets[0].Items[1].Delay == 2 && back.Sets[0].Items[0].Delay == 0);
        }

        static void SetTimerInputs(TimerPage page, int min, int sec)
        {
            foreach (var n in AllControls(page).OfType<NumericUpDown>().ToList())
                n.Value = n.Maximum >= 999 ? min : sec;
        }

        static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var x in AllControls(c)) yield return x;
            }
        }

        static void Snap(Form f, string file)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-6000, -6000);
            f.Show();
            Pump(300);
            Capture(f, file);
            f.Close();
            f.Dispose();
        }

        static void Capture(Control c, string file)
        {
            using (var bmp = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                bmp.Save(file, ImageFormat.Png);
            }
        }

        static void Pump(int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(15); }
        }
    }
}
