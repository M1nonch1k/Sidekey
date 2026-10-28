using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace Sidekey
{
    class StatRow
    {
        public string Key, Name;
        public double Seconds;
    }

    class Stats
    {
        public double Total;
        public List<StatRow> Rows = new List<StatRow>();
    }

    // Учёт времени: раз в секунду смотрит, какая программа на переднем плане,
    // и прибавляет секунду к её имени. Заголовки окон, ввод и содержимое не читаются.
    class Tracker
    {
        readonly UsageData data;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Timer timer = new Timer { Interval = 1000 };
        readonly Dictionary<uint, string> pathByPid = new Dictionary<uint, string>();
        readonly Dictionary<string, string> nameByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long lastTick, lastSave, cacheReset;
        bool dirty, locked;

        public bool Enabled = true;
        public int IdleMinutes = 5;
        public string State = "none"; // active, paused, idle, locked, none
        public string CurrentName;

        public Tracker(UsageData usage)
        {
            data = usage;
            if (data.Days == null) data.Days = new Dictionary<string, Dictionary<string, double>>();
            if (data.Names == null) data.Names = new Dictionary<string, string>();
            timer.Tick += (s, e) => Tick();
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        public void Start() { lastTick = clock.ElapsedMilliseconds; timer.Start(); }
        public void Stop() { timer.Stop(); SystemEvents.SessionSwitch -= OnSessionSwitch; }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect || e.Reason == SessionSwitchReason.RemoteDisconnect)
                locked = true;
            else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
                locked = false;
        }

        public void Tick()
        {
            long now = clock.ElapsedMilliseconds;
            double delta = (now - lastTick) / 1000.0;
            lastTick = now;
            // Большой разрыв означает сон/гибернацию — это время не считаем.
            if (delta > 5 || delta < 0) delta = 0;

            CurrentName = null;
            if (!Enabled) State = "paused";
            else if (locked) State = "locked";
            else if (IdleMinutes > 0 && Native.IdleMilliseconds() >= IdleMinutes * 60000L) State = "idle";
            else
            {
                string key, name;
                if (Resolve(out key, out name))
                {
                    State = "active";
                    CurrentName = name;
                    Add(key, name, delta);
                }
                else State = "none";
            }

            if (now - cacheReset > 10 * 60 * 1000) { pathByPid.Clear(); cacheReset = now; }
            if (dirty && now - lastSave > 60 * 1000) Save();
        }

        void Add(string key, string name, double seconds)
        {
            if (seconds <= 0) return;
            string day = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Dictionary<string, double> apps;
            if (!data.Days.TryGetValue(day, out apps)) { apps = new Dictionary<string, double>(); data.Days[day] = apps; }
            double v;
            apps.TryGetValue(key, out v);
            apps[key] = v + seconds;
            data.Names[key] = name;
            dirty = true;
        }

        public void Save()
        {
            lastSave = clock.ElapsedMilliseconds;
            if (!dirty) return;
            try { Store.Save("usage.json", data); dirty = false; }
            catch (Exception ex) { Store.Log("Could not save statistics: " + ex.Message); }
        }

        public bool Resolve(out string key, out string name)
        {
            key = name = null;
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return false;

            string path = PathFor(pid);
            // Приложения из Microsoft Store показываются через ApplicationFrameHost —
            // настоящий процесс находится в дочернем окне.
            if (path != null && Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            {
                uint child = FindChildProcess(hwnd, pid);
                if (child != 0) { pid = child; path = PathFor(pid); }
            }

            if (path == null)
            {
                try
                {
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        key = p.ProcessName.ToLowerInvariant() + ".exe";
                        name = p.ProcessName;
                    }
                    return true;
                }
                catch { return false; }
            }
            key = Path.GetFileName(path).ToLowerInvariant();
            name = DisplayName(path);
            return true;
        }

        string PathFor(uint pid)
        {
            string path;
            if (pathByPid.TryGetValue(pid, out path)) return path;
            path = Native.ProcessPath(pid);
            if (path != null) pathByPid[pid] = path;
            return path;
        }

        string DisplayName(string path)
        {
            string name;
            if (nameByPath.TryGetValue(path, out name)) return name;
            name = Path.GetFileNameWithoutExtension(path);
            try
            {
                string d = (FileVersionInfo.GetVersionInfo(path).FileDescription ?? "").Trim();
                if (d.Length > 0 && d.Length <= 60) name = d;
            }
            catch { }
            nameByPath[path] = name;
            return name;
        }

        static uint FindChildProcess(IntPtr hwnd, uint hostPid)
        {
            uint found = 0;
            Native.EnumChildWindows(hwnd, (h, l) =>
            {
                uint cp;
                Native.GetWindowThreadProcessId(h, out cp);
                if (cp != 0 && cp != hostPid) { found = cp; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public Stats GetStats(DateTime from, DateTime to)
        {
            var sum = new Dictionary<string, double>();
            foreach (var day in data.Days)
            {
                DateTime d;
                if (!DateTime.TryParseExact(day.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                if (d < from.Date || d > to.Date) continue;
                foreach (var app in day.Value)
                {
                    double v;
                    sum.TryGetValue(app.Key, out v);
                    sum[app.Key] = v + app.Value;
                }
            }
            var stats = new Stats();
            foreach (var kv in sum.OrderByDescending(x => x.Value))
            {
                string name;
                if (!data.Names.TryGetValue(kv.Key, out name)) name = kv.Key;
                stats.Rows.Add(new StatRow { Key = kv.Key, Name = name, Seconds = kv.Value });
                stats.Total += kv.Value;
            }
            return stats;
        }

        public void ClearAll()
        {
            data.Days.Clear();
            data.Names.Clear();
            dirty = true;
            Save();
        }
    }
}
