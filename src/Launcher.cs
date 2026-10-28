using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sidekey
{
    // Запуск наборов быстрого запуска. Права не повышаются автоматически:
    // если программе нужны права администратора, Windows сама покажет запрос.
    static class Launcher
    {
        public static string Expand(string path)
        {
            return Environment.ExpandEnvironmentVariables((path ?? "").Trim().Trim('"'));
        }

        public static string NormalizeUrl(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0 || s.Contains(" ")) return null;
            if (!s.Contains("://") && s.Contains(".")) s = "https://" + s;
            Uri u;
            if (!Uri.TryCreate(s, UriKind.Absolute, out u)) return null;
            string scheme = u.Scheme.ToLowerInvariant();
            if (scheme == "file" || scheme == "javascript" || scheme == "data") return null;
            if ((scheme == "http" || scheme == "https") && string.IsNullOrEmpty(u.Host)) return null;
            return s;
        }

        // Проверка без запуска: null, если всё в порядке, иначе понятное сообщение.
        public static string Check(LaunchItem item)
        {
            if (item.IsUrl())
                return NormalizeUrl(item.Path) == null ? L.F("launch.badUrl", item.Path) : null;
            string path = Expand(item.Path);
            if (path.Length == 0) return L.F("launch.noPath", item.Title());
            if (!File.Exists(path) && !Directory.Exists(path))
                return L.F("launch.notFound", item.Title(), path);
            return null;
        }

        // Запускает один пункт набора: null, если запуск прошёл, иначе понятное сообщение.
        public static string Start(LaunchItem item)
        {
            string error = Check(item);
            if (error != null) return error;
            try
            {
                ProcessStartInfo psi;
                if (item.IsUrl())
                    psi = new ProcessStartInfo(NormalizeUrl(item.Path));
                else
                {
                    string path = Expand(item.Path);
                    psi = new ProcessStartInfo(path, item.Args ?? "");
                    string dir = Path.GetDirectoryName(path);
                    if (File.Exists(path) && !string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
                }
                psi.UseShellExecute = true;
                using (Process.Start(psi)) { }
                return null;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    return L.F("launch.uacCancelled", item.Title());
                return L.F("launch.failed", item.Title(), ex.Message);
            }
            catch (Exception ex)
            {
                return L.F("launch.failed", item.Title(), ex.Message);
            }
        }

    }

    // Запуск набора по очереди. У каждого пункта своя задержка (Delay, в секундах):
    // сколько подождать перед его запуском после предыдущего. При нулевых задержках
    // всё запускается сразу, как раньше. Ожидание идёт по таймеру и не блокирует окно.
    class LaunchRun
    {
        public static LaunchRun Current;
        public Func<LaunchItem, string> Starter = Launcher.Start; // подменяется в самопроверке

        readonly LaunchSet set;
        readonly Action<string> progress;
        readonly Action<int, List<string>> finished; // (сколько запущено, ошибки)
        readonly List<string> errors = new List<string>();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        int index, waited, started;

        public LaunchRun(LaunchSet set, Action<string> progress, Action<int, List<string>> finished)
        {
            this.set = set;
            this.progress = progress;
            this.finished = finished;
            timer.Tick += (s, e) => Tick();
        }

        public void Start()
        {
            Current = this;
            Next();
        }

        void Next()
        {
            while (index < set.Items.Count)
            {
                var item = set.Items[index];
                if (item.Delay > 0 && waited < item.Delay)
                {
                    progress(L.F("launch.countdown", item.Delay - waited, item.Title()));
                    timer.Start();
                    return;
                }
                string error = Starter(item);
                if (error != null) errors.Add(error);
                else started++;
                index++;
                waited = 0;
            }
            timer.Stop();
            timer.Dispose();
            if (Current == this) Current = null;
            finished(started, errors);
        }

        void Tick()
        {
            timer.Stop();
            waited++;
            Next();
        }
    }
}
