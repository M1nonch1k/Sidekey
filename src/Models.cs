using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Sidekey
{
    public class AppSettings
    {
        public uint HotkeyMods = Native.MOD_CONTROL | Native.MOD_ALT;
        public uint HotkeyVk = (uint)Keys.T;
        public bool TrackingEnabled = true;
        public int IdleMinutes = 5;
        public string Language; // код языка интерфейса (en, ru, …); пусто — выбрать по умолчанию
        public string CityName = "";
        public string CityLabel = "";
        public double Lat, Lon;
        public bool FirstRunDone;
        public int TimerMinutes = 5, TimerSeconds = 0;
        public int LastTab;
        public string MusicFolder = "";     // папка для «Плеер → Мои треки»
        public int PlayerVolume = 80;       // громкость своего плеера, 0…100
        public bool PlayerShuffle;
        public string PlayerRepeat = "off"; // off, list, track
        public string Theme = "dark";       // dark, light, blue, red, green
        public bool PanelLocked = true;     // замок: пока закрыт, панель нельзя двигать и растягивать
        public bool PanelMoved;             // панель перенесли: показывать там, а не у правого края
        public int PanelX, PanelY, PanelWidth, PanelHeight; // 0 — размер по умолчанию

        public bool HasCity() { return !string.IsNullOrEmpty(CityName); }
    }

    // Статистика: день ("2026-10-28") -> имя файла программы -> секунды.
    public class UsageData
    {
        public Dictionary<string, Dictionary<string, double>> Days = new Dictionary<string, Dictionary<string, double>>();
        public Dictionary<string, string> Names = new Dictionary<string, string>();
    }

    public class Note
    {
        public string Id;
        public string Text = "";
        public string Modified; // "yyyy-MM-dd HH:mm:ss", местное время
    }

    public class NotesData
    {
        public List<Note> Notes = new List<Note>();
    }

    public class LaunchItem
    {
        public string Kind = "app"; // "app" или "url"
        public string Path = "";
        public string Args = "";
        public int Delay; // секунд ожидания перед запуском этого пункта

        public bool IsUrl() { return Kind == "url"; }
        public string KindLabel() { return IsUrl() ? L.T("item.url") : L.T("item.app"); }

        public string Title()
        {
            if (IsUrl())
            {
                Uri u;
                if (Uri.TryCreate(Launcher.NormalizeUrl(Path) ?? Path, UriKind.Absolute, out u) && !string.IsNullOrEmpty(u.Host))
                    return u.Host.StartsWith("www.") ? u.Host.Substring(4) : u.Host;
                return Path;
            }
            try { return System.IO.Path.GetFileNameWithoutExtension(Path.Trim().Trim('"')); }
            catch { return Path; }
        }

        public LaunchItem Clone() { return new LaunchItem { Kind = Kind, Path = Path, Args = Args, Delay = Delay }; }
    }

    public class LaunchSet
    {
        public string Id;
        public string Name = "";
        public List<LaunchItem> Items = new List<LaunchItem>();
        public uint HotkeyMods, HotkeyVk; // глобальное сочетание для запуска кнопки; 0 — не задано

        public bool HasHotkey() { return HotkeyVk != 0; }
    }

    public class LaunchData
    {
        public List<LaunchSet> Sets = new List<LaunchSet>();
    }

    // Локальное хранение в JSON-файлах. Запись идёт через временный файл,
    // чтобы при сбое питания не испортить сохранённые данные.
    static class Store
    {
        static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string DataDir = System.IO.Path.Combine(LocalAppData, "Sidekey", "data");

        // До версии 0.2 программа называлась «PC Assistant» и хранила данные в другой папке.
        // При первом запуске под новым именем данные копируются (старая папка не трогается).
        public static void MigrateFromOldName()
        {
            string old = System.IO.Path.Combine(LocalAppData, "PC Assistant", "data");
            string defaultDir = System.IO.Path.Combine(LocalAppData, "Sidekey", "data");
            if (DataDir != defaultDir || Directory.Exists(DataDir) || !Directory.Exists(old)) return;
            try
            {
                Directory.CreateDirectory(DataDir);
                foreach (var f in Directory.GetFiles(old, "*.json"))
                    File.Copy(f, System.IO.Path.Combine(DataDir, System.IO.Path.GetFileName(f)));
                Log("Data copied from " + old);
            }
            catch (Exception ex) { Log("Could not copy old data: " + ex.Message); }
        }

        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static T Load<T>(string file) where T : class, new()
        {
            string path = System.IO.Path.Combine(DataDir, file);
            if (!File.Exists(path)) return new T();
            try
            {
                return Json.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)) ?? new T();
            }
            catch (Exception ex)
            {
                Log("Could not read " + file + ": " + ex.Message);
                try { File.Copy(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
                return new T();
            }
        }

        public static void Save(string file, object data)
        {
            Directory.CreateDirectory(DataDir);
            string path = System.IO.Path.Combine(DataDir, file);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Json.Serialize(data), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(System.IO.Path.Combine(DataDir, "log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
