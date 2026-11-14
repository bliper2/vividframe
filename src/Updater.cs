using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Checks the project's GitHub releases for a newer build and, if Auto update is on, downloads it
    /// and installs it when the game closes (a running DLL cannot be overwritten, so the swap happens
    /// either by renaming it right away or by a tiny helper that waits for the game to exit).
    ///
    /// Safety rules: only the official release URL is used, the download must match a SHA-256 published
    /// with the release, and it must be a VividFrame assembly of the expected version. Nothing is sent
    /// to the server except a plain HTTPS request with a generic user agent. All network work runs on a
    /// background thread and never touches Unity objects.
    /// </summary>
    internal static class Updater
    {
        public const string Owner = "bliper2";
        public const string Repo = "vividframe";
        public const string ReleasesPage = "https://github.com/" + Owner + "/" + Repo + "/releases";
        private const string LatestApi = "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";
        private const float RecheckSeconds = 6f * 3600f;

        private static readonly HttpClient Http = CreateClient();
        private static MelonLogger.Instance _log;
        private static Settings _settings;
        private static string _dllPath;
        private static string _updatePath;
        private static string _oldPath;
        private static volatile bool _busy;
        private static volatile string _status = "Not checked yet";
        private static volatile string _notice;
        private static volatile string _readyVersion;
        private static float _nextCheck;
        private static bool _scheduledSwap;

        public static string Status { get { return _status; } }
        public static bool Busy { get { return _busy; } }
        public static bool UpdateReady { get { return _readyVersion != null; } }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VividFrame-Updater");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static void Init(MelonLogger.Instance log, Settings settings)
        {
            _log = log;
            _settings = settings;
            try
            {
                string mods = MelonEnvironment.ModsDirectory;
                _dllPath = Path.Combine(mods, "VividFrame.dll");
                _updatePath = _dllPath + ".update";
                _oldPath = _dllPath + ".old";
                CleanUpAfterPreviousUpdate();
                _nextCheck = Time.unscaledTime + 20f;   // let the game finish loading first
            }
            catch (Exception ex)
            {
                _status = "Updater unavailable";
                _log?.Warning("Updater could not start: {0}", ex.Message);
            }
        }

        /// <summary>Call every frame. Starts the periodic check and returns a message worth showing, if any.</summary>
        public static string Tick()
        {
            if (_settings != null && _settings.AutoUpdate.Value && !_busy && Time.unscaledTime >= _nextCheck && _dllPath != null)
            {
                _nextCheck = Time.unscaledTime + RecheckSeconds;
                CheckNow(false);
            }
            string notice = _notice;
            if (notice != null)
            {
                _notice = null;
                return notice;
            }
            return null;
        }

        /// <summary>Starts a check in the background. <paramref name="manual"/> also reports "up to date".</summary>
        public static void CheckNow(bool manual)
        {
            if (_busy || _dllPath == null)
                return;
            _busy = true;
            _status = "Checking for updates...";
            Task.Run(() => RunCheck(manual));
        }

        /// <summary>Call when the game is quitting so a downloaded update gets installed.</summary>
        public static void OnQuit()
        {
            try
            {
                if (_readyVersion == null || !File.Exists(_updatePath) || _scheduledSwap)
                    return;
                if (TrySwapNow())
                    return;
                StartSwapHelper();
            }
            catch (Exception ex)
            {
                _log?.Warning("Could not finish installing the update: {0}", ex.Message);
            }
        }

        private static async Task RunCheck(bool manual)
        {
            try
            {
                string json = await Http.GetStringAsync(LatestApi).ConfigureAwait(false);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
                    if (!TryParseVersion(tag, out Version latest))
                    {
                        _status = "Latest release has an unreadable version";
                        return;
                    }
                    Version current = ParseOrZero(VividFrameInfo.Version);
                    if (latest <= current)
                    {
                        _status = "Up to date (v" + VividFrameInfo.Version + ")";
                        if (manual)
                            _notice = "VividFrame is up to date";
                        return;
                    }

                    string dllUrl = null;
                    string digest = null;
                    string shaUrl = null;
                    if (root.TryGetProperty("assets", out JsonElement assets))
                    {
                        foreach (JsonElement asset in assets.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                            string url = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() : null;
                            if (name == "VividFrame.dll")
                            {
                                dllUrl = url;
                                if (asset.TryGetProperty("digest", out JsonElement d) && d.ValueKind == JsonValueKind.String)
                                    digest = d.GetString();
                            }
                            else if (name == "VividFrame.dll.sha256")
                            {
                                shaUrl = url;
                            }
                        }
                    }

                    if (dllUrl == null || !IsOfficialUrl(dllUrl))
                    {
                        _status = "v" + latest + " is out, but the release has no DLL";
                        return;
                    }

                    string expected = ExtractSha(digest);
                    if (expected == null && shaUrl != null && IsOfficialUrl(shaUrl))
                        expected = ExtractSha(await Http.GetStringAsync(shaUrl).ConfigureAwait(false));
                    if (expected == null)
                    {
                        _status = "v" + latest + " is out. No checksum was published, so it was not downloaded";
                        _notice = "VividFrame " + latest + " is available on GitHub";
                        return;
                    }

                    if (_settings != null && !_settings.AutoUpdate.Value)
                    {
                        _status = "v" + latest + " is available. Auto update is off";
                        _notice = "VividFrame " + latest + " is available";
                        return;
                    }

                    _status = "Downloading v" + latest + "...";
                    byte[] bytes = await Http.GetByteArrayAsync(dllUrl).ConfigureAwait(false);
                    string actual = Hex(SHA256.HashData(bytes));
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        _status = "Download failed the checksum, so it was discarded";
                        _log?.Warning("Update v{0} failed its SHA-256 check and was discarded.", latest);
                        return;
                    }

                    File.WriteAllBytes(_updatePath, bytes);
                    if (!LooksLikeVividFrame(_updatePath, latest))
                    {
                        TryDelete(_updatePath);
                        _status = "Download was not a valid VividFrame build, so it was discarded";
                        return;
                    }

                    _readyVersion = latest.ToString();
                    _status = "v" + latest + " is ready and installs when the game closes";
                    _notice = "VividFrame " + latest + " downloaded. It installs when you close the game";
                    _log?.Msg("Update v{0} downloaded and verified. It installs when the game closes.", latest);
                }
            }
            catch (Exception ex)
            {
                _status = "Update check failed (" + Short(ex) + ")";
                _log?.Warning("Update check failed: {0}", ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        // ------------------------------------------------------------------ installing

        private static bool TrySwapNow()
        {
            // A loaded DLL can usually be renamed even though it cannot be overwritten.
            try
            {
                TryDelete(_oldPath);
                File.Move(_dllPath, _oldPath);
                try
                {
                    File.Move(_updatePath, _dllPath);
                }
                catch
                {
                    File.Move(_oldPath, _dllPath);   // put the working copy back
                    throw;
                }
                _status = "v" + _readyVersion + " installed. Restart the game to use it";
                _log?.Msg("Installed update v{0}.", _readyVersion);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StartSwapHelper()
        {
            // Waits for this game process to exit, then moves the new DLL into place.
            int pid = Process.GetCurrentProcess().Id;
            string script = "Wait-Process -Id " + pid + " -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; "
                            + "if (Test-Path -LiteralPath '" + Q(_updatePath) + "') { Move-Item -LiteralPath '" + Q(_updatePath) + "' -Destination '" + Q(_dllPath) + "' -Force }";
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-ExecutionPolicy");
            info.ArgumentList.Add("Bypass");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add(script);
            Process.Start(info);
            _scheduledSwap = true;
            _log?.Msg("Update v{0} will be installed after the game exits.", _readyVersion);
        }

        private static string Q(string path)
        {
            return path.Replace("'", "''");
        }

        private static void CleanUpAfterPreviousUpdate()
        {
            TryDelete(_oldPath);
            // A staged file that is not newer than what is running is stale.
            if (File.Exists(_updatePath))
            {
                try
                {
                    AssemblyName staged = AssemblyName.GetAssemblyName(_updatePath);
                    if (staged.Version == null || staged.Version <= ParseOrZero(VividFrameInfo.Version))
                        TryDelete(_updatePath);
                    else
                    {
                        // A newer build was staged last session but never swapped in.
                        _readyVersion = staged.Version.ToString(3);
                        _status = "v" + _readyVersion + " is ready and installs when the game closes";
                    }
                }
                catch
                {
                    TryDelete(_updatePath);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private static bool IsOfficialUrl(string url)
        {
            // Release assets are served from github.com/<owner>/<repo>/releases/download/...
            string prefix = "https://github.com/" + Owner + "/" + Repo + "/releases/download/";
            return url != null && url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeVividFrame(string path, Version expected)
        {
            try
            {
                AssemblyName name = AssemblyName.GetAssemblyName(path);
                if (!string.Equals(name.Name, "VividFrame", StringComparison.Ordinal) || name.Version == null)
                    return false;
                return name.Version.Major == expected.Major && name.Version.Minor == expected.Minor
                       && name.Version.Build == Math.Max(0, expected.Build);
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractSha(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string t = text.Trim();
            if (t.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(7);
            int space = t.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            if (space > 0)
                t = t.Substring(0, space);
            return t.Length == 64 && IsHex(t) ? t : null;
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }

        private static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static bool TryParseVersion(string tag, out Version version)
        {
            string t = (tag ?? string.Empty).Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);
            int dash = t.IndexOf('-');
            if (dash > 0)
                t = t.Substring(0, dash);
            return Version.TryParse(t, out version);
        }

        private static Version ParseOrZero(string text)
        {
            return TryParseVersion(text, out Version v) ? v : new Version(0, 0, 0);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static string Short(Exception ex)
        {
            string m = ex.Message ?? ex.GetType().Name;
            return m.Length > 60 ? m.Substring(0, 60) : m;
        }
    }
}
