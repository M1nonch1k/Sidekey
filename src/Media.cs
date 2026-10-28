using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Control;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Sidekey
{
    // Вызовы Windows Runtime без Windows SDK: асинхронные операции дожидаются в фоновом потоке.
    static class WinRT
    {
        public static T Wait<T>(IAsyncOperation<T> op, int timeoutMs = 10000)
        {
            var until = Environment.TickCount + timeoutMs;
            while (op.Status == AsyncStatus.Started)
            {
                if (Environment.TickCount - until > 0) { op.Cancel(); throw new TimeoutException(); }
                Thread.Sleep(5);
            }
            if (op.Status != AsyncStatus.Completed) throw op.ErrorCode ?? new InvalidOperationException(op.Status.ToString());
            return op.GetResults();
        }

        public static T Wait<T, P>(IAsyncOperationWithProgress<T, P> op)
        {
            while (op.Status == AsyncStatus.Started) Thread.Sleep(5);
            if (op.Status != AsyncStatus.Completed) throw op.ErrorCode ?? new InvalidOperationException(op.Status.ToString());
            return op.GetResults();
        }

        public static void Wait<P>(IAsyncActionWithProgress<P> op)
        {
            while (op.Status == AsyncStatus.Started) Thread.Sleep(5);
            if (op.Status != AsyncStatus.Completed) throw op.ErrorCode ?? new InvalidOperationException(op.Status.ToString());
        }

        // Картинка из потока Windows (обложка) в обычный Bitmap.
        public static Image ReadImage(IRandomAccessStreamReference reference)
        {
            if (reference == null) return null;
            using (var stream = Wait(reference.OpenReadAsync()))
                return ReadImage(stream);
        }

        public static Image ReadImage(IRandomAccessStream stream)
        {
            if (stream == null || stream.Size == 0) return null;
            using (var reader = new DataReader(stream))
            {
                uint n = Wait(reader.LoadAsync((uint)Math.Min(stream.Size, 16 * 1024 * 1024)));
                var bytes = new byte[n];
                reader.ReadBytes(bytes);
                using (var ms = new MemoryStream(bytes))
                using (var raw = Image.FromStream(ms))
                    return new Bitmap(raw);
            }
        }
    }

    // ---------------------------------------------------------------- «Сейчас играет»

    // Что играет в другой программе (Spotify, браузер, плеер), по данным Windows.
    class NowPlaying
    {
        public string AppId = "", AppName = "", Title = "", Artist = "", Album = "";
        public Image Cover;
        public bool Playing, CanPlayPause, CanNext, CanPrevious, CanSeek, CanShuffle, CanRepeat;
        public bool Shuffle;
        public string Repeat = "off"; // off, list, track
        public TimeSpan Position, Duration;
        public DateTime Fetched;
        public string Key { get { return AppId + "\n" + Title + "\n" + Artist; } }
    }

    // Управление чужим воспроизведением через системные медиаэлементы Windows (то же,
    // что плашка над регулятором громкости). Ни входа, ни интернета: команды идут прямо
    // в программу, которая играет. Нужна Windows 10 версии 1809 или новее.
    static class SystemMedia
    {
        static GlobalSystemMediaTransportControlsSessionManager manager;
        static readonly object gate = new object();
        static string coverKey;
        static Image cover;
        static bool? supported;

        public static bool Supported
        {
            get
            {
                if (supported == null)
                {
                    try { Manager(); supported = true; }
                    catch (Exception) { supported = false; }
                }
                return supported.Value;
            }
        }

        static GlobalSystemMediaTransportControlsSessionManager Manager()
        {
            lock (gate)
            {
                if (manager == null) manager = WinRT.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                return manager;
            }
        }

        // Программы, в которых сейчас что-то открыто (идентификаторы Windows).
        public static List<string> Apps()
        {
            return Manager().GetSessions().Select(s => s.SourceAppUserModelId).Distinct().ToList();
        }

        // Понятное имя программы: «Spotify.exe» → «Spotify», «Microsoft.ZuneMusic_…!Microsoft.ZuneMusic» → «ZuneMusic».
        public static string AppName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string s = id;
            int bang = s.LastIndexOf('!');
            if (bang >= 0) s = s.Substring(bang + 1);
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            int dot = s.LastIndexOf('.');
            if (dot >= 0 && dot < s.Length - 1 && bang >= 0) s = s.Substring(dot + 1);
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : id;
        }

        static GlobalSystemMediaTransportControlsSession Session(string appId)
        {
            var m = Manager();
            var all = m.GetSessions();
            if (!string.IsNullOrEmpty(appId))
                foreach (var s in all) if (s.SourceAppUserModelId == appId) return s;
            return m.GetCurrentSession() ?? all.FirstOrDefault();
        }

        // Текущее состояние выбранной программы (или той, что Windows считает главной). null — ничего не открыто.
        public static NowPlaying Read(string appId)
        {
            var s = Session(appId);
            if (s == null) return null;
            var props = WinRT.Wait(s.TryGetMediaPropertiesAsync());
            var info = s.GetPlaybackInfo();
            var timeline = s.GetTimelineProperties();
            var c = info.Controls;
            var n = new NowPlaying
            {
                AppId = s.SourceAppUserModelId,
                AppName = AppName(s.SourceAppUserModelId),
                Title = props.Title ?? "",
                Artist = !string.IsNullOrEmpty(props.Artist) ? props.Artist : props.AlbumArtist ?? "",
                Album = props.AlbumTitle ?? "",
                Playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlayPause = c.IsPlayPauseToggleEnabled || c.IsPlayEnabled || c.IsPauseEnabled,
                CanNext = c.IsNextEnabled,
                CanPrevious = c.IsPreviousEnabled,
                CanSeek = c.IsPlaybackPositionEnabled,
                CanShuffle = c.IsShuffleEnabled,
                CanRepeat = c.IsRepeatEnabled,
                Shuffle = info.IsShuffleActive == true,
                Repeat = info.AutoRepeatMode == MediaPlaybackAutoRepeatMode.Track ? "track" : info.AutoRepeatMode == MediaPlaybackAutoRepeatMode.List ? "list" : "off",
                Duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero,
                Fetched = DateTime.UtcNow
            };
            // Позиция сообщается на момент LastUpdatedTime; пока играет, она с тех пор ушла вперёд.
            var pos = timeline.Position - timeline.StartTime;
            if (n.Playing && timeline.LastUpdatedTime.Year > 2000)
                pos += DateTimeOffset.Now - timeline.LastUpdatedTime;
            n.Position = pos < TimeSpan.Zero ? TimeSpan.Zero : n.Duration > TimeSpan.Zero && pos > n.Duration ? n.Duration : pos;
            if (n.Duration == TimeSpan.Zero) n.CanSeek = false;

            // Обложка читается заново только при смене трека. Читать её нужно в фоновом потоке:
            // из потока окна (STA) Windows её не отдаёт. Неудачная попытка не запоминается.
            lock (gate)
            {
                if (coverKey != n.Key)
                {
                    try { cover = WinRT.ReadImage(props.Thumbnail); coverKey = n.Key; }
                    catch { cover = null; }
                }
                n.Cover = cover;
            }
            return n;
        }

        static void Run(string appId, Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
        {
            var s = Session(appId);
            if (s != null) WinRT.Wait(command(s));
        }

        public static void PlayPause(string appId) { Run(appId, s => s.TryTogglePlayPauseAsync()); }
        public static void Next(string appId) { Run(appId, s => s.TrySkipNextAsync()); }
        public static void Previous(string appId) { Run(appId, s => s.TrySkipPreviousAsync()); }
        public static void SetShuffle(string appId, bool on) { Run(appId, s => s.TryChangeShuffleActiveAsync(on)); }

        public static void Seek(string appId, TimeSpan position)
        {
            Run(appId, s => s.TryChangePlaybackPositionAsync((s.GetTimelineProperties().StartTime + position).Ticks));
        }

        public static void SetRepeat(string appId, string mode)
        {
            var m = mode == "track" ? MediaPlaybackAutoRepeatMode.Track : mode == "list" ? MediaPlaybackAutoRepeatMode.List : MediaPlaybackAutoRepeatMode.None;
            Run(appId, s => s.TryChangeAutoRepeatModeAsync(m));
        }

        // Как в Spotify: выкл → весь список → один трек → выкл.
        public static string NextRepeat(string mode) { return mode == "off" ? "list" : mode == "list" ? "track" : "off"; }
    }

    // ---------------------------------------------------------------- «Мои треки»

    class LocalTrack
    {
        public string Path, Title, Artist = "", Album = "";
        public TimeSpan Duration;
        public bool TagsLoaded;
        public override string ToString() { return Title; }
    }

    // Плеер для папки с музыкой на компьютере. Играет встроенный плеер Windows (Media Foundation):
    // mp3, m4a/aac, flac, wav, wma. Ничего не отправляется в интернет.
    static class LocalPlayer
    {
        public static readonly string[] Extensions = { ".mp3", ".m4a", ".aac", ".flac", ".wav", ".wma" };
        static MediaPlayer player;
        static bool wanted;   // пользователь ждёт, что играет (не на паузе)
        static int startedAt; // когда запущен текущий файл (Environment.TickCount)

        public static List<LocalTrack> Tracks = new List<LocalTrack>();
        public static int Index = -1;          // текущий трек в Tracks
        public static bool Shuffle;
        public static string Repeat = "off";   // off, list, track
        static List<int> order = new List<int>(); // порядок при случайном воспроизведении
        static readonly Random random = new Random();

        public static LocalTrack Current { get { return Index >= 0 && Index < Tracks.Count ? Tracks[Index] : null; } }

        // Все музыкальные файлы в папке и вложенных папках, по алфавиту. Теги читаются отдельно (LoadTags).
        public static List<LocalTrack> Scan(string folder)
        {
            var list = new List<LocalTrack>();
            foreach (var f in SafeFiles(folder))
            {
                if (!Extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())) continue;
                list.Add(new LocalTrack { Path = f, Title = System.IO.Path.GetFileNameWithoutExtension(f) });
                if (list.Count >= 5000) break;
            }
            return list.OrderBy(t => t.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // Обход без падения на папках, куда нет доступа.
        static IEnumerable<string> SafeFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                string[] files = new string[0], dirs = new string[0];
                try { files = Directory.GetFiles(dir); dirs = Directory.GetDirectories(dir); } catch { }
                foreach (var f in files) yield return f;
                foreach (var d in dirs) pending.Push(d);
            }
        }

        // Название, исполнитель, альбом и длительность из тегов файла (ID3 и т.п.). Выполняется в фоне.
        public static void LoadTags(LocalTrack t)
        {
            try
            {
                var file = WinRT.Wait(StorageFile.GetFileFromPathAsync(t.Path));
                var music = WinRT.Wait(file.Properties.GetMusicPropertiesAsync());
                if (!string.IsNullOrWhiteSpace(music.Title)) t.Title = music.Title.Trim();
                t.Artist = (!string.IsNullOrEmpty(music.Artist) ? music.Artist : music.AlbumArtist ?? "").Trim();
                t.Album = (music.Album ?? "").Trim();
                t.Duration = music.Duration;
            }
            catch { }
            t.TagsLoaded = true;
        }

        // Обложка, встроенная в файл; если её нет — null (значок Windows не подставляем). Выполняется в фоне.
        public static Image LoadCover(LocalTrack t)
        {
            try
            {
                var file = WinRT.Wait(StorageFile.GetFileFromPathAsync(t.Path));
                using (var thumb = WinRT.Wait(file.GetThumbnailAsync(ThumbnailMode.MusicView, 300, ThumbnailOptions.ResizeThumbnail)))
                    return thumb != null && thumb.Type == ThumbnailType.Image ? WinRT.ReadImage(thumb) : null;
            }
            catch { return null; }
        }

        static MediaPlayer Player
        {
            get
            {
                if (player == null)
                {
                    player = new MediaPlayer { AutoPlay = false, Volume = App.Settings.PlayerVolume / 100.0 };
                    player.CommandManager.IsEnabled = false; // не показываться в системной плашке «Сейчас играет»
                }
                return player;
            }
        }

        public static bool Playing
        {
            get { return player != null && player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing; }
        }

        public static TimeSpan Position { get { return player != null && Current != null ? player.PlaybackSession.Position : TimeSpan.Zero; } }

        public static TimeSpan Duration
        {
            get
            {
                if (player == null || Current == null) return TimeSpan.Zero;
                var d = player.PlaybackSession.NaturalDuration;
                return d > TimeSpan.Zero ? d : Current.Duration;
            }
        }

        public static double Volume
        {
            get { return player != null ? player.Volume : App.Settings.PlayerVolume / 100.0; }
            set { Player.Volume = Math.Max(0, Math.Min(1, value)); }
        }

        public static bool Muted { get { return player != null && player.IsMuted; } set { Player.IsMuted = value; } }

        public static void SetTracks(List<LocalTrack> tracks)
        {
            Stop();
            Tracks = tracks;
            Index = -1;
            BuildOrder();
        }

        public static void Play(int index)
        {
            if (index < 0 || index >= Tracks.Count) return;
            Index = index;
            wanted = true;
            startedAt = Environment.TickCount;
            Player.Source = MediaSource.CreateFromUri(new Uri(Tracks[index].Path));
            Player.Play();
        }

        public static void PlayPause()
        {
            if (Current == null) { if (Tracks.Count > 0) Play(Shuffle && order.Count > 0 ? order[0] : 0); return; }
            if (Playing) { Player.Pause(); wanted = false; } else { Player.Play(); wanted = true; }
        }

        public static void Stop()
        {
            if (player == null) return;
            player.Pause();
            wanted = false;
            player.Source = null;
        }

        public static void Seek(TimeSpan position)
        {
            if (player != null && Current != null) player.PlaybackSession.Position = position;
        }

        public static void Next() { int i = NextIndex(Index, true); if (i >= 0) Play(i); }

        // «Назад», как в плеерах: если трек играет дольше 3 секунд — с начала, иначе предыдущий.
        public static void Previous()
        {
            if (Current != null && Position.TotalSeconds > 3) { Seek(TimeSpan.Zero); return; }
            int pos = order.IndexOf(Index);
            int prev = pos > 0 ? order[pos - 1] : Repeat == "list" && order.Count > 0 ? order[order.Count - 1] : Index;
            if (prev >= 0) Play(prev);
        }

        // Вызывается по таймеру окна. События плеера без Windows SDK недоступны, поэтому конец трека
        // и ошибка определяются по состоянию: доиграл до конца — следующий (с учётом повтора);
        // за 5 секунд так и не открылся — «failed».
        public static string Poll()
        {
            if (player == null || Current == null || !wanted) return null;
            var session = player.PlaybackSession;
            var duration = session.NaturalDuration;
            if (duration <= TimeSpan.Zero)
            {
                if (Environment.TickCount - startedAt < 5000 || session.PlaybackState == MediaPlaybackState.Opening) return null;
                wanted = false;
                return "failed";
            }
            if (session.PlaybackState == MediaPlaybackState.Playing || session.Position < duration - TimeSpan.FromMilliseconds(400)) return null;
            int i = NextIndex(Index, false);
            if (i >= 0) Play(i); else wanted = false;
            return "advanced";
        }

        // Следующий трек: manual — пользователь нажал «вперёд» (повтор одного трека тогда не действует).
        public static int NextIndex(int current, bool manual)
        {
            if (Tracks.Count == 0) return -1;
            if (!manual && Repeat == "track" && current >= 0) return current;
            int pos = order.IndexOf(current);
            if (pos + 1 < order.Count) return order[pos + 1];
            return Repeat == "list" || manual ? order[0] : -1;
        }

        public static void SetShuffle(bool on)
        {
            Shuffle = on;
            BuildOrder();
        }

        // Порядок воспроизведения: по списку или перемешанный (текущий трек — первым).
        static void BuildOrder()
        {
            order = Enumerable.Range(0, Tracks.Count).ToList();
            if (!Shuffle) return;
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int x = order[i]; order[i] = order[j]; order[j] = x;
            }
            if (Index >= 0) { order.Remove(Index); order.Insert(0, Index); }
        }
    }
}
