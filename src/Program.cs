using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyTitle("Sidekey")]
[assembly: System.Reflection.AssemblyProduct("Sidekey")]
[assembly: System.Reflection.AssemblyVersion("0.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.2.0.0")]

namespace Sidekey
{
    static class Program
    {
        // Сообщение «покажи панель» для уже запущенной копии программы.
        public static readonly int ShowMessage = Native.RegisterWindowMessage("Sidekey.ShowPanel.v1");

        [STAThread]
        static int Main(string[] args)
        {
            bool hidden = false;
            string selfTest = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--hidden") hidden = true;
                else if (a == "--data" && i + 1 < args.Length) Store.DataDir = Path.GetFullPath(args[++i]);
                else if (a == "--selftest" && i + 1 < args.Length) selfTest = Path.GetFullPath(args[++i]);
            }
            if (selfTest != null) Store.DataDir = Path.Combine(selfTest, "data");

            try { Native.SetProcessDPIAware(); } catch { }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.Init();

            if (selfTest != null) return SelfTest.Run(selfTest);

            bool created;
            string mutexName = "Local\\Sidekey." + Store.DataDir.ToLowerInvariant().GetHashCode().ToString("X8");
            using (var mutex = new Mutex(true, mutexName, out created))
            {
                if (!created)
                {
                    // Программа уже запущена — просим её показать панель.
                    Native.PostMessage(Native.HWND_BROADCAST, ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    Store.Log("Error: " + e.Exception);
                    Dlg.Info(L.F("error.unexpected", e.Exception.Message));
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Store.Log("Fatal error: " + e.ExceptionObject);

                Store.MigrateFromOldName();
                App.Start(hidden);
                Application.Run();
                GC.KeepAlive(mutex);
            }
            if (App.RestartRequested)
            {
                var keep = new System.Collections.Generic.List<string>();
                for (int i = 0; i < args.Length; i++)
                    if (args[i].ToLowerInvariant() != "--hidden") keep.Add("\"" + args[i] + "\"");
                System.Diagnostics.Process.Start(Application.ExecutablePath, string.Join(" ", keep));
            }
            return 0;
        }
    }
}
