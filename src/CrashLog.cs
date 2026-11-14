using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Leaves evidence behind when the game dies. A native crash cannot be caught, so instead the
    /// mod keeps a short ring of recent actions and flushes it to disk about once a second, next to
    /// a "running" flag that a clean exit removes. If the flag is still there on the next start,
    /// the previous session did not exit cleanly and a crash report is written from that ring.
    /// Managed exceptions that escape everything else are written immediately.
    /// </summary>
    internal static class CrashLog
    {
        private const int RingSize = 60;
        private const int KeepReports = 8;

        private static readonly string[] Ring = new string[RingSize];
        private static int _head;
        private static int _count;
        private static bool _dirty;
        private static float _nextFlush;
        private static string _dir;
        private static string _flagPath;
        private static string _livePath;
        private static MelonLogger.Instance _log;
        private static Func<string> _stateProvider;
        private static bool _ready;
        private static bool _shutDown;

        public static string LastReport { get; private set; }
        public static string Folder { get { return _dir; } }
        public static int ReportCount { get; private set; }

        public static void Init(MelonLogger.Instance log, Func<string> stateProvider)
        {
            _log = log;
            _stateProvider = stateProvider;
            try
            {
                _dir = Path.Combine(MelonEnvironment.UserDataDirectory, "VividFrame", "crash");
                Directory.CreateDirectory(_dir);
                _flagPath = Path.Combine(_dir, "running.flag");
                _livePath = Path.Combine(_dir, "lastrun.txt");

                if (File.Exists(_flagPath))
                    WriteReportFromPreviousRun();

                File.WriteAllText(_flagPath, "started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " v" + VividFrameInfo.Version);
                CountReports();
                _ready = true;
                _shutDown = false;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
                Mark("session start v" + VividFrameInfo.Version);
                Flush(true);
            }
            catch (Exception ex)
            {
                _ready = false;
                _log?.Warning("Crash logging could not start: {0}", ex.Message);
            }
        }

        /// <summary>Records a recent action. Cheap: a string and a ring write, no disk access.</summary>
        public static void Mark(string what)
        {
            if (!_ready || _shutDown)
                return;
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + what;
            Ring[_head] = line;
            _head = (_head + 1) % RingSize;
            if (_count < RingSize)
                _count++;
            _dirty = true;
        }

        private static readonly Dictionary<string, float> Throttle = new Dictionary<string, float>(16);

        /// <summary>Like <see cref="Mark"/>, but at most once per interval for the same key (for noisy events).</summary>
        public static void MarkEvery(string key, float seconds, string what)
        {
            if (!_ready || _shutDown)
                return;
            float now = Time.unscaledTime;
            if (Throttle.TryGetValue(key, out float next) && now < next)
                return;
            Throttle[key] = now + seconds;
            Mark(what);
        }

        /// <summary>Call every frame. Flushes the ring to disk at most once a second.</summary>
        public static void Tick()
        {
            if (!_ready || _shutDown)
                return;
            if (Time.unscaledTime < _nextFlush)
                return;
            _nextFlush = Time.unscaledTime + 1f;
            Flush(false);
        }

        /// <summary>Call on a clean exit so the next start does not report a crash.</summary>
        public static void Shutdown()
        {
            if (!_ready || _shutDown)
                return;
            Mark("clean exit");
            Flush(true);
            _shutDown = true;
            try
            {
                if (File.Exists(_flagPath))
                    File.Delete(_flagPath);
            }
            catch { }
            try { AppDomain.CurrentDomain.UnhandledException -= OnUnhandled; }
            catch { }
        }

        private static void Flush(bool force)
        {
            if (!_dirty && !force && Time.unscaledTime - _lastHeartbeat < 5f)
                return;
            _lastHeartbeat = Time.unscaledTime;
            _dirty = false;
            try
            {
                var sb = new StringBuilder(4096);
                sb.Append("VividFrame ").Append(VividFrameInfo.Version).Append(" live log, written ")
                  .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append('\n');
                sb.Append("uptime ").Append(Time.realtimeSinceStartup.ToString("0.0")).Append("s\n");
                try
                {
                    string state = _stateProvider != null ? _stateProvider() : null;
                    if (!string.IsNullOrEmpty(state))
                        sb.Append("state: ").Append(state).Append('\n');
                }
                catch { }
                sb.Append("--- recent actions (oldest first) ---\n");
                int start = _count < RingSize ? 0 : _head;
                for (int i = 0; i < _count; i++)
                    sb.Append(Ring[(start + i) % RingSize]).Append('\n');
                File.WriteAllText(_livePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static float _lastHeartbeat;

        private static void WriteReportFromPreviousRun()
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(_dir, "crash_" + stamp + ".txt");
                var sb = new StringBuilder();
                sb.Append("VividFrame crash report\n");
                sb.Append("Created ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" by v").Append(VividFrameInfo.Version).Append('\n');
                sb.Append("The previous session did not exit cleanly. That means the game crashed, was force-closed, or lost power.\n");
                sb.Append("Only the last actions VividFrame took are listed. If the last lines are unrelated to the crash, VividFrame is probably not the cause.\n\n");
                try { sb.Append("Run flag: ").Append(File.ReadAllText(_flagPath)).Append("\n\n"); }
                catch { }
                if (File.Exists(_livePath))
                    sb.Append(File.ReadAllText(_livePath, Encoding.UTF8));
                else
                    sb.Append("(no live log was left behind)\n");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                LastReport = Path.GetFileName(path);
                _log?.Warning("The previous session ended unexpectedly. A report was saved to {0}", path);
                PruneReports();
            }
            catch (Exception ex)
            {
                _log?.Warning("Could not write a crash report: {0}", ex.Message);
            }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (!_ready)
                    return;
                Mark("UNHANDLED EXCEPTION: " + e.ExceptionObject);
                Flush(true);
                string path = Path.Combine(_dir, "exception_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(path, "Unhandled exception (terminating: " + e.IsTerminating + ")\n" + e.ExceptionObject + "\n\n"
                                        + (File.Exists(_livePath) ? File.ReadAllText(_livePath) : string.Empty), Encoding.UTF8);
                LastReport = Path.GetFileName(path);
            }
            catch { }
        }

        private static void CountReports()
        {
            try
            {
                ReportCount = Directory.GetFiles(_dir, "crash_*.txt").Length + Directory.GetFiles(_dir, "exception_*.txt").Length;
            }
            catch
            {
                ReportCount = 0;
            }
        }

        private static void PruneReports()
        {
            try
            {
                var files = new List<string>(Directory.GetFiles(_dir, "crash_*.txt"));
                files.Sort(StringComparer.Ordinal);
                while (files.Count > KeepReports)
                {
                    File.Delete(files[0]);
                    files.RemoveAt(0);
                }
            }
            catch { }
        }
    }
}
