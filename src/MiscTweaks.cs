using System;
using System.Diagnostics;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Optional extras: camera, quality, audio and system tweaks plus screenshots. Every property
    /// write is guarded on its own so one stripped or locked setting never stops the others. The
    /// game's original values are captured the first time an override is applied and restored when
    /// it is turned off.
    /// </summary>
    internal sealed class MiscTweaks
    {
        private readonly Settings _settings;
        private readonly MelonLogger.Instance _log;
        private readonly string _shotDir;

        // Quality originals.
        private bool _qualityApplied;
        private int _origVsync, _origFrameRate, _origMsaa, _origTexLimit, _origCascades, _origPixelLights;
        private float _origLod, _origShadow;
        private AnisotropicFiltering _origAniso;
        private ShadowQuality _origShadowMode;
        private ShadowResolution _origShadowRes;
        private SkinWeights _origSkin;
        private bool _origSoft, _origProbes;

        // System originals.
        private bool _bgApplied, _origBg;
        private bool _loadApplied;
        private ThreadPriority _origLoad;
        private bool _audioApplied;
        private float _origVolume = 1f;

        // Environment originals (per world; forgotten on scene load).
        private bool _fogApplied, _origFog;
        private bool _ambApplied;
        private float _origAmb;

        // Camera.
        private bool _fovApplied;
        private float _origFov, _fovCurrent;
        private Camera _fovCamera, _clipCamera;
        private bool _nearApplied, _farApplied;
        private float _origNear, _origFar;
        private float _nextCameraLookup;
        private float _nextPass;
        private bool _menuVisible;
        private string _zoomName;
        private KeyCode _zoomCode;
        private bool _zoomValid;
        private Camera _freeCamera;
        private Transform _freeCameraParent;
        private Vector3 _freeCameraLocalPosition;
        private Quaternion _freeCameraLocalRotation;
        private CursorLockMode _freeCameraCursorLock;
        private bool _freeCameraCursorVisible;
        private float _freeCameraYaw;
        private float _freeCameraPitch;
        private float _freeCameraOriginalFov;
        private float _freeCameraTargetFov;
        private Vector3 _freeCameraVelocity;

        private float _ramAt;
        private float _ramMb;
        private string _sysInfo;
        private readonly bool[] _logged = new bool[8];

        public MiscTweaks(Settings settings, MelonLogger.Instance log)
        {
            _settings = settings;
            _log = log;
            _shotDir = Path.Combine(MelonEnvironment.UserDataDirectory, "VividFrame", "screenshots");
        }

        public string ScreenshotFolder { get { return _shotDir; } }

        public void SetMenuVisible(bool visible)
        {
            _menuVisible = visible;
        }

        // ------------------------------------------------------------------ ticking

        /// <summary>Call every frame from OnLateUpdate.</summary>
        public void LateTick()
        {
            TickFreeCamera();
            TickCamera();
            TickAudio();
            if (Time.unscaledTime >= _nextPass)
            {
                _nextPass = Time.unscaledTime + 3f;
                ApplyPeriodic();
            }
        }

        public void ApplyNow()
        {
            _nextPass = 0f;
            ApplyPeriodic();
        }

        public void OnSceneLoaded()
        {
            StopFreeCamera();
            // Fog and ambient belong to the world, so drop what we remembered about the last one.
            _fogApplied = false;
            CrashLog.Mark("misc: _fogApplied -> false");
            _ambApplied = false;
            CrashLog.Mark("misc: _ambApplied -> false");
            _fovCamera = null;
            _clipCamera = null;
            _nearApplied = false;
            CrashLog.Mark("misc: _nearApplied -> false");
            _farApplied = false;
            CrashLog.Mark("misc: _farApplied -> false");
            _nextCameraLookup = 0f;
            ApplyNow();
        }

        public bool FreeCameraActive { get { return _freeCamera != null; } }

        public string ToggleFreeCamera()
        {
            if (FreeCameraActive)
            {
                StopFreeCamera();
                return "Free camera off";
            }
            if (VrSupport.Active)
                return "Free camera is desktop only";

            Camera camera = GetCamera();
            if (camera == null)
                return "Free camera: no active camera";

            try
            {
                Transform transform = camera.transform;
                _freeCamera = camera;
                _freeCameraParent = transform.parent;
                _freeCameraLocalPosition = transform.localPosition;
                _freeCameraLocalRotation = transform.localRotation;
                _freeCameraCursorLock = Cursor.lockState;
                _freeCameraCursorVisible = Cursor.visible;
                _freeCameraOriginalFov = camera.fieldOfView;
                _freeCameraTargetFov = _freeCameraOriginalFov;
                _freeCameraVelocity = Vector3.zero;
                Vector3 angles = transform.eulerAngles;
                _freeCameraYaw = angles.y;
                _freeCameraPitch = angles.x > 180f ? angles.x - 360f : angles.x;
                transform.SetParent(null, true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return "Free camera on: WASD move, mouse look, scroll to zoom, Space/Ctrl up/down, Shift speed";
            }
            catch (Exception ex)
            {
                StopFreeCamera();
                Once(0, "Could not start free camera", ex);
                return "Free camera could not start";
            }
        }

        private void StopFreeCamera()
        {
            Camera camera = _freeCamera;
            if (camera == null)
                return;

            try
            {
                Transform transform = camera.transform;
                camera.fieldOfView = _freeCameraOriginalFov;
                if (_freeCameraParent != null)
                {
                    transform.SetParent(_freeCameraParent, false);
                    transform.localPosition = _freeCameraLocalPosition;
                    transform.localRotation = _freeCameraLocalRotation;
                }
            }
            catch { }
            try
            {
                Cursor.lockState = _freeCameraCursorLock;
                Cursor.visible = _freeCameraCursorVisible;
            }
            catch { }
            _freeCamera = null;
            _freeCameraParent = null;
        }

        private void TickFreeCamera()
        {
            Camera camera = _freeCamera;
            if (camera == null)
                return;
            try
            {
                if (!camera.isActiveAndEnabled || VrSupport.Active)
                {
                    StopFreeCamera();
                    return;
                }

                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
                _freeCameraYaw += Input.GetAxis("Mouse X") * 3f;
                _freeCameraPitch = Mathf.Clamp(_freeCameraPitch - Input.GetAxis("Mouse Y") * 3f, -89f, 89f);
                Quaternion targetRotation = Quaternion.Euler(_freeCameraPitch, _freeCameraYaw, 0f);
                camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, targetRotation, 1f - Mathf.Exp(-dt * 9f));

                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.001f)
                    _freeCameraTargetFov = Mathf.Clamp(_freeCameraTargetFov * Mathf.Pow(0.88f, scroll), 15f, 100f);
                camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, _freeCameraTargetFov, 1f - Mathf.Exp(-dt * 10f));

                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += camera.transform.forward;
                if (Input.GetKey(KeyCode.S)) move -= camera.transform.forward;
                if (Input.GetKey(KeyCode.D)) move += camera.transform.right;
                if (Input.GetKey(KeyCode.A)) move -= camera.transform.right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) move -= Vector3.up;
                float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 12f : 4f;
                if (move.sqrMagnitude > 1f) move.Normalize();
                Vector3 desiredVelocity = move * speed;
                Vector3 velocity = Vector3.SmoothDamp(_freeCameraVelocity, desiredVelocity, ref _freeCameraVelocity, 0.18f, Mathf.Infinity, dt);
                camera.transform.position += velocity * dt;

                if (Input.GetKeyDown(KeyCode.Escape))
                    StopFreeCamera();
            }
            catch (Exception ex)
            {
                Once(0, "Free camera update failed", ex);
                StopFreeCamera();
            }
        }

        private void ApplyPeriodic()
        {
            ApplyQuality();
            ApplyEnvironment();
            ApplySystem();
        }

        // ------------------------------------------------------------------ camera

        private bool ZoomHeld()
        {
            string name = _settings.ZoomKey.Value;
            if (string.IsNullOrEmpty(name) || string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                return false;
            if (_zoomName != name)
            {
                _zoomName = name;
                _zoomValid = Enum.TryParse(name, true, out _zoomCode) && _zoomCode != KeyCode.None;
            }
            if (!_zoomValid)
                return false;
            try { return Input.GetKey(_zoomCode); }
            catch { return false; }
        }

        private static bool IsCursorFree()
        {
            try { return Cursor.lockState != CursorLockMode.Locked; }
            catch { return false; }
        }

        private Camera GetCamera()
        {
            if (_fovCamera != null && _fovCamera.isActiveAndEnabled)
                return _fovCamera;
            if (Time.unscaledTime < _nextCameraLookup)
                return null;
            _nextCameraLookup = Time.unscaledTime + 1f;
            _fovCamera = Camera.main;
            return _fovCamera;
        }

        private void TickCamera()
        {
            try
            {
                if (!FreeCameraActive)
                    TickFov();
                TickClip();
            }
            catch (Exception ex)
            {
                Once(0, "Camera tweaks failed", ex);
            }
        }

        private void TickFov()
        {
            // Menus aim the mouse using the camera's normal FOV, so a wider FOV can make their
            // buttons miss the cursor. Reset for VividFrame's menu directly and use free cursor
            // state as a fallback for VRChat's menus.
            bool uiOpen = VrSupport.Active || (_settings.FovResetInMenus.Value && (_menuVisible || IsCursorFree()));
            bool zoomKey = ZoomHeld() && !uiOpen;
            bool want = (_settings.FovOverride.Value || zoomKey) && !uiOpen;
            float dt = Mathf.Max(0.0001f, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            // Exponential easing: fast at first, settling gently, independent of frame rate.
            float blend = 1f - Mathf.Exp(-dt * 11f);

            if (zoomKey && !_zooming)
            {
                _zooming = true;
                CrashLog.MarkEvery("misc-zoom", 2f, "misc: _zooming -> true");
                _zoomLive = _settings.ZoomFov.Value;
            }
            else if (!zoomKey && _zooming)
            {
                _zooming = false;
                CrashLog.MarkEvery("misc-zoom", 2f, "misc: _zooming -> false");
                if (Mathf.Abs(_settings.ZoomFov.Value - _zoomLive) > 0.01f)
                    _settings.ZoomFov.Value = _zoomLive;   // remember the zoom level you scrolled to
            }

            if (_zooming)
            {
                float scroll = 0f;
                try { scroll = Input.mouseScrollDelta.y; }
                catch { }
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    // Scroll up zooms in. Steps are proportional so it feels even at any level.
                    _zoomLive *= Mathf.Pow(0.88f, scroll);
                }
                float cap = _fovApplied ? Mathf.Max(10f, _origFov) : 90f;
                _zoomLive = Mathf.Clamp(_zoomLive, 4f, cap);
            }

            if (want)
            {
                Camera cam = GetCamera();
                if (cam == null)
                    return;
                float target = _zooming ? _zoomLive : _settings.Fov.Value;
                target = Mathf.Clamp(target, 4f, 140f);
                if (!_fovApplied)
                {
                    _origFov = cam.fieldOfView;
                    _fovCurrent = _origFov;
                    _fovApplied = true;
                    CrashLog.MarkEvery("misc-fov", 2f, "misc: _fovApplied -> true");
                }
                _fovCurrent = Mathf.Lerp(_fovCurrent, target, blend);
                cam.fieldOfView = _fovCurrent;
            }
            else if (_fovApplied)
            {
                Camera cam = GetCamera();
                if (cam == null)
                {
                    _fovApplied = false;
                    CrashLog.MarkEvery("misc-fov", 2f, "misc: _fovApplied -> false");
                    return;
                }
                _fovCurrent = Mathf.Lerp(_fovCurrent, _origFov, blend);
                cam.fieldOfView = _fovCurrent;
                if (Mathf.Abs(_fovCurrent - _origFov) < 0.02f)
                {
                    cam.fieldOfView = _origFov;
                    _fovApplied = false;
                    CrashLog.MarkEvery("misc-fov", 2f, "misc: _fovApplied -> false");
                }
            }
        }

        private bool _zooming;
        private float _zoomLive = 30f;

        private void TickClip()
        {
            bool near = _settings.NearClipOverride.Value;
            bool far = _settings.FarClipOverride.Value;
            if (!near && !far && !_nearApplied && !_farApplied)
                return;
            Camera cam = GetCamera();
            if (cam == null)
                return;
            if (_clipCamera != cam)
            {
                _clipCamera = cam;
                _nearApplied = false;
                CrashLog.Mark("misc: _nearApplied -> false");
                _farApplied = false;
                CrashLog.Mark("misc: _farApplied -> false");
            }

            if (near)
            {
                if (!_nearApplied)
                {
                    _origNear = cam.nearClipPlane;
                    _nearApplied = true;
                    CrashLog.Mark("misc: _nearApplied -> true");
                }
                cam.nearClipPlane = Mathf.Clamp(_settings.NearClip.Value, 0.01f, 5f);
            }
            else if (_nearApplied)
            {
                cam.nearClipPlane = _origNear;
                _nearApplied = false;
                CrashLog.Mark("misc: _nearApplied -> false");
            }

            if (far)
            {
                if (!_farApplied)
                {
                    _origFar = cam.farClipPlane;
                    _farApplied = true;
                    CrashLog.Mark("misc: _farApplied -> true");
                }
                cam.farClipPlane = Mathf.Clamp(_settings.FarClip.Value, 10f, 10000f);
            }
            else if (_farApplied)
            {
                cam.farClipPlane = _origFar;
                _farApplied = false;
                CrashLog.Mark("misc: _farApplied -> false");
            }
        }

        private InstanceInfo _instance;

        public string PlayersText()
        {
            if (_instance == null)
                _instance = new InstanceInfo(_log);
            int count = _instance.PlayerCount;
            if (count >= 0)
                return "Players  " + count;
            return _instance.Available ? "Players  -" : "Players  n/a";
        }

        public string CameraPositionText()
        {
            try
            {
                Camera cam = GetCamera();
                if (cam == null)
                    return "No camera";
                Vector3 p = cam.transform.position;
                return "X " + p.x.ToString("0.0") + "  Y " + p.y.ToString("0.0") + "  Z " + p.z.ToString("0.0");
            }
            catch
            {
                return "No camera";
            }
        }

        // ------------------------------------------------------------------ audio

        private void TickAudio()
        {
            try
            {
                bool mute = _settings.MuteUnfocused.Value && !Application.isFocused;
                bool want = _settings.VolumeOverride.Value || mute;
                if (want)
                {
                    if (!_audioApplied)
                    {
                        _origVolume = AudioListener.volume;
                        _audioApplied = true;
                        CrashLog.Mark("misc: _audioApplied -> true");
                    }
                    AudioListener.volume = mute ? 0f : Mathf.Clamp01(_settings.MasterVolume.Value);
                }
                else if (_audioApplied)
                {
                    AudioListener.volume = _origVolume;
                    _audioApplied = false;
                    CrashLog.Mark("misc: _audioApplied -> false");
                }
            }
            catch (Exception ex)
            {
                Once(1, "Audio tweaks failed", ex);
            }
        }

        // ------------------------------------------------------------------ system

        private void ApplySystem()
        {
            if (_settings.RunInBackground.Value)
            {
                if (!_bgApplied)
                {
                    Guard(2, "read runInBackground", () => _origBg = Application.runInBackground);
                    _bgApplied = true;
                    CrashLog.Mark("misc: _bgApplied -> true");
                }
                Guard(2, "runInBackground", () => Application.runInBackground = true);
            }
            else if (_bgApplied)
            {
                Guard(2, "restore runInBackground", () => Application.runInBackground = _origBg);
                _bgApplied = false;
                CrashLog.Mark("misc: _bgApplied -> false");
            }

            if (_settings.LoadPriorityOverride.Value)
            {
                if (!_loadApplied)
                {
                    Guard(2, "read loading priority", () => _origLoad = Application.backgroundLoadingPriority);
                    _loadApplied = true;
                    CrashLog.Mark("misc: _loadApplied -> true");
                }
                Guard(2, "loading priority", () => Application.backgroundLoadingPriority = PriorityFromIndex(_settings.LoadPriority.Value));
            }
            else if (_loadApplied)
            {
                Guard(2, "restore loading priority", () => Application.backgroundLoadingPriority = _origLoad);
                _loadApplied = false;
                CrashLog.Mark("misc: _loadApplied -> false");
            }
        }

        private static ThreadPriority PriorityFromIndex(int index)
        {
            switch (index)
            {
                case 0: return ThreadPriority.Low;
                case 1: return ThreadPriority.BelowNormal;
                case 3: return ThreadPriority.High;
                default: return ThreadPriority.Normal;
            }
        }

        public string FreeMemory()
        {
            try
            {
                GC.Collect();
                Resources.UnloadUnusedAssets();
                return "Freed unused memory";
            }
            catch (Exception ex)
            {
                Once(3, "Free memory failed", ex);
                return "Could not free memory";
            }
        }

        public float RamMegabytes()
        {
            if (Time.unscaledTime < _ramAt)
                return _ramMb;
            _ramAt = Time.unscaledTime + 1f;
            try
            {
                using (Process p = Process.GetCurrentProcess())
                    _ramMb = p.WorkingSet64 / (1024f * 1024f);
            }
            catch
            {
                _ramMb = 0f;
            }
            return _ramMb;
        }

        public string SystemInfoText()
        {
            if (_sysInfo != null && Time.unscaledTime < _sysInfoAt)
                return _sysInfo;
            _sysInfoAt = Time.unscaledTime + 5f;   // the display line can change, the rest is static
            try
            {
                string gpuName = SystemInfo.graphicsDeviceName;
                long vramMb = 0;
                string driverName = null;
                bool dedicated = TryReadDedicatedVram(gpuName, out vramMb, out driverName, out string driverVersion);
                string gpu = !string.IsNullOrEmpty(driverName) ? driverName : gpuName;
                string vram = dedicated
                    ? FormatGb(vramMb)
                    : FormatGb(SystemInfo.graphicsMemorySize) + " (includes shared memory)";

                string ram = FormatGb(TotalRamMb());
                string display = Screen.width + "x" + Screen.height;
                try
                {
                    Resolution cur = Screen.currentResolution;
                    double hz = cur.refreshRateRatio.value;
                    if (hz > 1.0)
                        display += " @ " + Math.Round(hz) + " Hz";
                }
                catch { }

                string cpu = ReadCpuName();
                string driver = string.IsNullOrEmpty(driverVersion) ? null : "Driver  " + driverVersion + "\n";
                _sysInfo = "GPU  " + gpu + "  " + vram + " VRAM\n"
                           + "CPU  " + cpu + "  " + Environment.ProcessorCount + " threads\n"
                           + "RAM  " + ram + "\n"
                           + "Display  " + display + "\n"
                           + driver
                           + "Read from Windows. Unity " + Application.unityVersion;
            }
            catch
            {
                _sysInfo = "System info unavailable";
            }
            return _sysInfo;
        }

        private float _sysInfoAt;

        /// <summary>CPU name from Windows itself, not from the game's reported value.</summary>
        private static string ReadCpuName()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    string name = key != null ? key.GetValue("ProcessorNameString") as string : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        return System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
                }
            }
            catch { }
            try { return SystemInfo.processorType.Trim(); }
            catch { return "unknown"; }
        }

        private static string FormatGb(long megabytes)
        {
            if (megabytes <= 0)
                return "unknown";
            double gb = megabytes / 1024.0;
            return gb >= 10.0 ? Math.Round(gb) + " GB" : Math.Round(gb, 1) + " GB";
        }

        /// <summary>Physical RAM in MB. The OS value is exact; Unity's figure is rounded down.</summary>
        private static long TotalRamMb()
        {
            try
            {
                long bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                if (bytes > 0)
                    return (long)Math.Round(bytes / (1024.0 * 1024.0));
            }
            catch { }
            try { return SystemInfo.systemMemorySize; }
            catch { return 0; }
        }

        /// <summary>
        /// Unity's graphicsMemorySize on Windows adds shared system memory to the card's own,
        /// so a 8 GB card shows as 24 GB. The driver's registry entry holds the real dedicated size.
        /// </summary>
        private static bool TryReadDedicatedVram(string unityName, out long megabytes, out string driverName, out string driverVersion)
        {
            megabytes = 0;
            driverName = null;
            driverVersion = null;
            try
            {
                const string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using (Microsoft.Win32.RegistryKey root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(classPath))
                {
                    if (root == null)
                        return false;
                    string wanted = Normalize(unityName);
                    long bestAny = 0;
                    string bestAnyName = null;
                    string bestAnyDriver = null;
                    foreach (string sub in root.GetSubKeyNames())
                    {
                        if (sub.Length != 4 || !char.IsDigit(sub[0]))
                            continue;
                        using (Microsoft.Win32.RegistryKey adapter = root.OpenSubKey(sub))
                        {
                            if (adapter == null)
                                continue;
                            object raw = adapter.GetValue("HardwareInformation.qwMemorySize");
                            if (raw == null)
                                raw = adapter.GetValue("HardwareInformation.MemorySize");
                            long bytes = ToBytes(raw);
                            if (bytes <= 0)
                                continue;
                            string desc = adapter.GetValue("DriverDesc") as string;
                            long mb = bytes / (1024L * 1024L);
                            if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(wanted)
                                && (Normalize(desc).Contains(wanted) || wanted.Contains(Normalize(desc))))
                            {
                                megabytes = mb;
                                driverName = desc;
                                driverVersion = adapter.GetValue("DriverVersion") as string;
                                return true;
                            }
                            if (mb > bestAny)
                            {
                                bestAny = mb;
                                bestAnyName = desc;
                                bestAnyDriver = adapter.GetValue("DriverVersion") as string;
                            }
                        }
                    }
                    if (bestAny > 0)
                    {
                        megabytes = bestAny;
                        driverName = bestAnyName;
                        driverVersion = bestAnyDriver;
                        return true;
                    }
                }
            }
            catch
            {
                // Not Windows, or the registry is locked down: the caller falls back to Unity's figure.
            }
            return false;
        }

        private static long ToBytes(object raw)
        {
            if (raw is long l)
                return l;
            if (raw is int i)
                return i < 0 ? (uint)i : i;
            if (raw is byte[] bytes && bytes.Length >= 4)
            {
                long value = 0;
                int n = Math.Min(bytes.Length, 8);
                for (int k = 0; k < n; k++)
                    value |= (long)bytes[k] << (8 * k);
                return value;
            }
            return 0;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            return name.ToLowerInvariant().Replace("nvidia", string.Empty).Replace("geforce", string.Empty)
                       .Replace("(tm)", string.Empty).Replace("(r)", string.Empty).Replace(" ", string.Empty);
        }

        // ------------------------------------------------------------------ environment

        private void ApplyEnvironment()
        {
            if (_settings.FogOff.Value)
            {
                if (!_fogApplied)
                {
                    Guard(4, "read fog", () => _origFog = RenderSettings.fog);
                    _fogApplied = true;
                    CrashLog.Mark("misc: _fogApplied -> true");
                }
                Guard(4, "fog", () => RenderSettings.fog = false);
            }
            else if (_fogApplied)
            {
                Guard(4, "restore fog", () => RenderSettings.fog = _origFog);
                _fogApplied = false;
                CrashLog.Mark("misc: _fogApplied -> false");
            }

            if (_settings.AmbientOverride.Value)
            {
                if (!_ambApplied)
                {
                    Guard(4, "read ambient", () => _origAmb = RenderSettings.ambientIntensity);
                    _ambApplied = true;
                    CrashLog.Mark("misc: _ambApplied -> true");
                }
                Guard(4, "ambient", () => RenderSettings.ambientIntensity = Mathf.Clamp(_settings.AmbientIntensity.Value, 0f, 5f));
            }
            else if (_ambApplied)
            {
                Guard(4, "restore ambient", () => RenderSettings.ambientIntensity = _origAmb);
                _ambApplied = false;
                CrashLog.Mark("misc: _ambApplied -> false");
            }
        }

        // ------------------------------------------------------------------ quality

        private void ApplyQuality()
        {
            if (_settings.QualityOverride.Value)
            {
                if (!_qualityApplied)
                {
                    CaptureQuality();
                    _qualityApplied = true;
                    CrashLog.Mark("misc: _qualityApplied -> true");
                }
                // Only write values that differ. Re-setting MSAA, shadow mode or shadow resolution makes Unity
                // rebuild render targets (eye textures in VR), which hitched every refresh and risks native crashes.
                Guard(5, "vSyncCount", () =>
                {
                    int v = _settings.VSync.Value ? 1 : 0;
                    if (QualitySettings.vSyncCount != v) QualitySettings.vSyncCount = v;
                });
                Guard(5, "targetFrameRate", () =>
                {
                    int cap = _settings.FpsCap.Value;
                    int v = cap <= 0 ? _origFrameRate : cap;
                    if (Application.targetFrameRate != v) Application.targetFrameRate = v;
                });
                Guard(5, "lodBias", () =>
                {
                    float v = Mathf.Clamp(_settings.LodBias.Value, 0.3f, 4f);
                    if (Math.Abs(QualitySettings.lodBias - v) > 0.001f) QualitySettings.lodBias = v;
                });
                Guard(5, "shadowDistance", () =>
                {
                    float v = Mathf.Clamp(_settings.ShadowDistance.Value, 0f, 200f);
                    if (Math.Abs(QualitySettings.shadowDistance - v) > 0.01f) QualitySettings.shadowDistance = v;
                });
                Guard(5, "anisotropic", () =>
                {
                    AnisotropicFiltering v = _settings.Anisotropic.Value ? AnisotropicFiltering.ForceEnable : _origAniso;
                    if (QualitySettings.anisotropicFiltering != v) QualitySettings.anisotropicFiltering = v;
                });
                Guard(5, "antiAliasing", () =>
                {
                    int v = MsaaFromIndex(_settings.MsaaIndex.Value);
                    if (QualitySettings.antiAliasing != v) QualitySettings.antiAliasing = v;
                });
                Guard(5, "texture quality", () =>
                {
                    int v = Mathf.Clamp(_settings.TextureLimit.Value, 0, 3);
                    if (QualitySettings.globalTextureMipmapLimit != v) QualitySettings.globalTextureMipmapLimit = v;
                });
                Guard(5, "shadows", () =>
                {
                    ShadowQuality v = ShadowModeFromIndex(_settings.ShadowMode.Value);
                    if (QualitySettings.shadows != v) QualitySettings.shadows = v;
                });
                Guard(5, "shadow resolution", () =>
                {
                    ShadowResolution v = ShadowResFromIndex(_settings.ShadowRes.Value);
                    if (QualitySettings.shadowResolution != v) QualitySettings.shadowResolution = v;
                });
                Guard(5, "shadow cascades", () =>
                {
                    int v = CascadesFromIndex(_settings.ShadowCascades.Value);
                    if (QualitySettings.shadowCascades != v) QualitySettings.shadowCascades = v;
                });
                Guard(5, "pixel lights", () =>
                {
                    int v = Mathf.Clamp(_settings.PixelLights.Value, 0, 16);
                    if (QualitySettings.pixelLightCount != v) QualitySettings.pixelLightCount = v;
                });
                Guard(5, "skin weights", () =>
                {
                    SkinWeights v = SkinFromIndex(_settings.SkinWeights.Value);
                    if (QualitySettings.skinWeights != v) QualitySettings.skinWeights = v;
                });
                Guard(5, "soft particles", () =>
                {
                    bool v = _settings.SoftParticles.Value;
                    if (QualitySettings.softParticles != v) QualitySettings.softParticles = v;
                });
                Guard(5, "reflection probes", () =>
                {
                    bool v = _settings.ReflectionProbes.Value;
                    if (QualitySettings.realtimeReflectionProbes != v) QualitySettings.realtimeReflectionProbes = v;
                });
            }
            else if (_qualityApplied)
            {
                Guard(5, "restore vSync", () => QualitySettings.vSyncCount = _origVsync);
                Guard(5, "restore frame rate", () => Application.targetFrameRate = _origFrameRate);
                Guard(5, "restore lod", () => QualitySettings.lodBias = _origLod);
                Guard(5, "restore shadows", () => QualitySettings.shadowDistance = _origShadow);
                Guard(5, "restore anisotropic", () => QualitySettings.anisotropicFiltering = _origAniso);
                Guard(5, "restore msaa", () => QualitySettings.antiAliasing = _origMsaa);
                Guard(5, "restore texture quality", () => QualitySettings.globalTextureMipmapLimit = _origTexLimit);
                Guard(5, "restore shadow mode", () => QualitySettings.shadows = _origShadowMode);
                Guard(5, "restore shadow resolution", () => QualitySettings.shadowResolution = _origShadowRes);
                Guard(5, "restore cascades", () => QualitySettings.shadowCascades = _origCascades);
                Guard(5, "restore pixel lights", () => QualitySettings.pixelLightCount = _origPixelLights);
                Guard(5, "restore skin weights", () => QualitySettings.skinWeights = _origSkin);
                Guard(5, "restore soft particles", () => QualitySettings.softParticles = _origSoft);
                Guard(5, "restore reflection probes", () => QualitySettings.realtimeReflectionProbes = _origProbes);
                _qualityApplied = false;
                CrashLog.Mark("misc: _qualityApplied -> false");
            }
        }

        private void CaptureQuality()
        {
            Guard(5, "read vSync", () => _origVsync = QualitySettings.vSyncCount);
            Guard(5, "read frame rate", () => _origFrameRate = Application.targetFrameRate);
            Guard(5, "read lod", () => _origLod = QualitySettings.lodBias);
            Guard(5, "read shadows", () => _origShadow = QualitySettings.shadowDistance);
            Guard(5, "read anisotropic", () => _origAniso = QualitySettings.anisotropicFiltering);
            Guard(5, "read msaa", () => _origMsaa = QualitySettings.antiAliasing);
            Guard(5, "read texture quality", () => _origTexLimit = QualitySettings.globalTextureMipmapLimit);
            Guard(5, "read shadow mode", () => _origShadowMode = QualitySettings.shadows);
            Guard(5, "read shadow resolution", () => _origShadowRes = QualitySettings.shadowResolution);
            Guard(5, "read cascades", () => _origCascades = QualitySettings.shadowCascades);
            Guard(5, "read pixel lights", () => _origPixelLights = QualitySettings.pixelLightCount);
            Guard(5, "read skin weights", () => _origSkin = QualitySettings.skinWeights);
            Guard(5, "read soft particles", () => _origSoft = QualitySettings.softParticles);
            Guard(5, "read reflection probes", () => _origProbes = QualitySettings.realtimeReflectionProbes);
        }

        private static int MsaaFromIndex(int index)
        {
            switch (index)
            {
                case 1: return 2;
                case 2: return 4;
                case 3: return 8;
                default: return 0;
            }
        }

        private static ShadowQuality ShadowModeFromIndex(int index)
        {
            switch (index)
            {
                case 0: return ShadowQuality.Disable;
                case 1: return ShadowQuality.HardOnly;
                default: return ShadowQuality.All;
            }
        }

        private static ShadowResolution ShadowResFromIndex(int index)
        {
            switch (index)
            {
                case 0: return ShadowResolution.Low;
                case 1: return ShadowResolution.Medium;
                case 3: return ShadowResolution.VeryHigh;
                default: return ShadowResolution.High;
            }
        }

        private static int CascadesFromIndex(int index)
        {
            switch (index)
            {
                case 0: return 1;
                case 1: return 2;
                default: return 4;
            }
        }

        private static SkinWeights SkinFromIndex(int index)
        {
            switch (index)
            {
                case 0: return SkinWeights.OneBone;
                case 1: return SkinWeights.TwoBones;
                case 3: return SkinWeights.Unlimited;
                default: return SkinWeights.FourBones;
            }
        }

        // ------------------------------------------------------------------ screenshots

        /// <summary>Saves a PNG. Returns the file name on success, or null.</summary>
        public string TakeScreenshot()
        {
            try
            {
                Directory.CreateDirectory(_shotDir);
                string name = "VividFrame_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                int scale = Mathf.Clamp(_settings.ScreenshotScale.Value, 1, 4);
                ScreenCapture.CaptureScreenshot(Path.Combine(_shotDir, name), scale);
                return name;
            }
            catch (Exception ex)
            {
                Once(6, "Screenshot failed", ex);
                return null;
            }
        }

        // ------------------------------------------------------------------ helpers

        public void Restore()
        {
            try
            {
                Camera cam = _fovCamera;
                if (_fovApplied && cam != null)
                    cam.fieldOfView = _origFov;
                _fovApplied = false;
                CrashLog.MarkEvery("misc-fov", 2f, "misc: _fovApplied -> false");
                if (_clipCamera != null)
                {
                    if (_nearApplied) _clipCamera.nearClipPlane = _origNear;
                    if (_farApplied) _clipCamera.farClipPlane = _origFar;
                }
                _nearApplied = _farApplied = false;
                if (_audioApplied)
                    AudioListener.volume = _origVolume;
                _audioApplied = false;
                CrashLog.Mark("misc: _audioApplied -> false");
            }
            catch { }

            bool quality = _settings.QualityOverride.Value;
            bool bg = _settings.RunInBackground.Value;
            bool load = _settings.LoadPriorityOverride.Value;
            bool fog = _settings.FogOff.Value;
            bool amb = _settings.AmbientOverride.Value;
            try
            {
                _settings.QualityOverride.Value = false;
                _settings.RunInBackground.Value = false;
                _settings.LoadPriorityOverride.Value = false;
                _settings.FogOff.Value = false;
                _settings.AmbientOverride.Value = false;
                ApplyQuality();
                ApplySystem();
                ApplyEnvironment();
            }
            catch { }
            finally
            {
                _settings.QualityOverride.Value = quality;
                _settings.RunInBackground.Value = bg;
                _settings.LoadPriorityOverride.Value = load;
                _settings.FogOff.Value = fog;
                _settings.AmbientOverride.Value = amb;
            }
        }

        private void Guard(int slot, string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Once(slot, "'" + what + "' failed and may be unavailable", ex);
            }
        }

        private void Once(int slot, string message, Exception ex)
        {
            if (slot < 0 || slot >= _logged.Length || _logged[slot])
                return;
            _logged[slot] = true;
            _log?.Warning("Misc tweak: {0}: {1}", message, ex != null ? ex.Message : string.Empty);
        }
    }
}
