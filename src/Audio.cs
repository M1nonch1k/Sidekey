using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sidekey
{
    // Программа в микшере. Если у программы несколько звуковых сессий (например, у браузера),
    // они показываются одной строкой и меняются вместе.
    class AudioApp
    {
        public string Key, Name, Path;
        public float Volume;   // 0…1, доля от общей громкости
        public bool Muted;
        internal readonly List<ISimpleAudioVolume> Sessions = new List<ISimpleAudioVolume>();
    }

    // Громкость через Core Audio Windows: общая громкость устройства вывода по умолчанию
    // и громкость каждой программы на нём. Ничего не записывается и не сохраняется.
    static class Audio
    {
        static IAudioEndpointVolume master;
        static string deviceId;
        static Guid context = Guid.Empty;
        static readonly Dictionary<string, Icon> icons = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

        public static List<AudioApp> Apps = new List<AudioApp>();
        public static string DeviceName = "";
        public static bool HasDevice { get { return master != null; } }

        // Перечитывает устройство и список программ. Вызывается по таймеру, пока вкладка открыта.
        public static void Refresh()
        {
            Release();
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device) != 0 || device == null) return;
                string id;
                device.GetId(out id);
                if (id != deviceId) { deviceId = id; DeviceName = FriendlyName(device); }

                object o;
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out o);
                master = (IAudioEndpointVolume)o;

                iid = typeof(IAudioSessionManager2).GUID;
                device.Activate(ref iid, 1, IntPtr.Zero, out o);
                var manager = (IAudioSessionManager2)o;
                try { Apps = ReadSessions(manager); }
                finally { Free(manager); }
            }
            catch (COMException) { Release(); }
            catch (InvalidCastException) { Release(); }
            finally
            {
                Free(device);
                Free(enumerator);
            }
        }

        static List<AudioApp> ReadSessions(IAudioSessionManager2 manager)
        {
            var byKey = new Dictionary<string, AudioApp>();
            var order = new List<AudioApp>();
            IAudioSessionEnumerator sessions;
            manager.GetSessionEnumerator(out sessions);
            try
            {
                int count;
                sessions.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 control;
                    sessions.GetSession(i, out control);
                    bool keep = false;
                    try
                    {
                        int state;
                        control.GetState(out state);
                        if (state == 2) continue; // AudioSessionStateExpired: программа уже закрыта

                        string key, name, path = null;
                        if (control.IsSystemSoundsSession() == 0)
                        {
                            key = "#system";
                            name = L.T("mix.system");
                        }
                        else
                        {
                            uint pid;
                            control.GetProcessId(out pid);
                            path = Native.ProcessPath(pid);
                            key = path != null ? path.ToLowerInvariant() : "#" + pid;
                            name = path != null ? App.Tracker.DisplayName(path) : ProcessName(pid, control);
                        }

                        var volume = (ISimpleAudioVolume)control;
                        float v;
                        bool muted;
                        volume.GetMasterVolume(out v);
                        volume.GetMute(out muted);

                        AudioApp app;
                        if (!byKey.TryGetValue(key, out app))
                        {
                            app = new AudioApp { Key = key, Name = name, Path = path, Volume = v, Muted = muted };
                            byKey[key] = app;
                            order.Add(app);
                        }
                        else
                        {
                            app.Volume = Math.Max(app.Volume, v);
                            app.Muted = app.Muted && muted;
                        }
                        app.Sessions.Add(volume);
                        keep = true;
                    }
                    catch (COMException) { }
                    catch (InvalidCastException) { }
                    finally { if (!keep) Free(control); }
                }
            }
            finally { Free(sessions); }
            // Системные звуки — внизу, остальные по алфавиту.
            return order.OrderBy(a => a.Key == "#system" ? 1 : 0).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static string ProcessName(uint pid, IAudioSessionControl2 control)
        {
            try { using (var p = Process.GetProcessById((int)pid)) return p.ProcessName; }
            catch { }
            string name = null;
            try { control.GetDisplayName(out name); } catch (COMException) { }
            return !string.IsNullOrEmpty(name) && !name.StartsWith("@") ? name : L.T("mix.unknown");
        }

        static string FriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            try
            {
                device.OpenPropertyStore(0 /* STGM_READ */, out store);
                var key = new PropertyKey { Fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 14 }; // PKEY_Device_FriendlyName
                PropVariant value;
                store.GetValue(ref key, out value);
                try { return value.Vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""; }
                finally { PropVariantClear(ref value); }
            }
            catch (COMException) { return ""; }
            finally { Free(store); }
        }

        // --- общая громкость

        public static float MasterVolume
        {
            get
            {
                float v = 0;
                if (master != null) try { master.GetMasterVolumeLevelScalar(out v); } catch (COMException) { }
                return v;
            }
            set
            {
                if (master != null) try { master.SetMasterVolumeLevelScalar(Clamp(value), ref context); } catch (COMException) { }
            }
        }

        public static bool MasterMuted
        {
            get
            {
                bool m = false;
                if (master != null) try { master.GetMute(out m); } catch (COMException) { }
                return m;
            }
            set
            {
                if (master != null) try { master.SetMute(value, ref context); } catch (COMException) { }
            }
        }

        // --- громкость программы

        public static void SetVolume(AudioApp app, float value)
        {
            app.Volume = Clamp(value);
            foreach (var s in app.Sessions)
                try { s.SetMasterVolume(app.Volume, ref context); } catch (COMException) { }
        }

        public static void SetMute(AudioApp app, bool muted)
        {
            app.Muted = muted;
            foreach (var s in app.Sessions)
                try { s.SetMute(muted, ref context); } catch (COMException) { }
        }

        public static Icon IconFor(AudioApp app)
        {
            if (app.Path == null) return null;
            Icon icon;
            if (icons.TryGetValue(app.Path, out icon)) return icon;
            try { icon = Icon.ExtractAssociatedIcon(app.Path); } catch { icon = null; }
            icons[app.Path] = icon;
            return icon;
        }

        static float Clamp(float v) { return Math.Max(0f, Math.Min(1f, v)); }

        public static void Release()
        {
            foreach (var a in Apps) foreach (var s in a.Sessions) Free(s);
            Apps = new List<AudioApp>();
            Free(master);
            master = null;
        }

        static void Free(object com)
        {
            if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant value);
    }

    // ---------------------------------------------------------------- COM-интерфейсы Core Audio
    // Объявлены только нужные методы; порядок совпадает с заголовками Windows SDK.

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        void OpenPropertyStore(int access, out IPropertyStore store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey { public Guid Fmtid; public int Pid; }

    [StructLayout(LayoutKind.Sequential)]
    struct PropVariant { public short Vt, R1, R2, R3; public IntPtr Pointer, Extra; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out int count);
        void GetAt(int index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr notify);
        void UnregisterControlChangeNotify(IntPtr notify);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float db, ref Guid context);
        void SetMasterVolumeLevelScalar(float level, ref Guid context);
        void GetMasterVolumeLevel(out float db);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        void GetChannelVolumeLevel(uint channel, out float db);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        void GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
        void GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        void GetGroupingParam(out Guid param);
        void SetGroupingParam(ref Guid param, ref Guid context);
        void RegisterAudioSessionNotification(IntPtr notify);
        void UnregisterAudioSessionNotification(IntPtr notify);
        // IAudioSessionControl2
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, ref Guid context);
        void GetMasterVolume(out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
