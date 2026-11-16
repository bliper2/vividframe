using System;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(VividFrame.VividFrameMod), "VividFrame", "1.9.0", "mcperr")]
[assembly: MelonGame("VRChat", "VRChat")]

namespace VividFrame
{
    public sealed class VividFrameMod : MelonMod
    {
        private const float LayerRefreshSeconds = 2f;

        private Settings _settings;
        private AutoFocus _autoFocus;
        private ProfileStore _profiles;
        private GraphicsVolume _volume;
        private Menu _menu;
        private float _saveDue = -1f;
        private float _nextLayerRefresh;
        private float _lastAppliedFocus = -1f;
        private bool _ready;
        private bool _guiErrorLogged;
        private string _cachedKeyName;
        private KeyCode _cachedKey;
        private bool _cachedKeyValid;
        private WorldProfiles _worlds;
        private readonly HotKey _compareKey = new HotKey();
        private readonly HotKey _masterKey = new HotKey();
        private readonly HotKey _profileKey = new HotKey();
        private int _profileCycle = -1;
        private MiscTweaks _misc;
        private VrWorldMenu _vrMenu;
        private readonly HotKey _shotKey = new HotKey();
        private bool _shotWaitClosed;
        private int _shotFrames = -1;
        private int _shotNotifyFrames;
        private string _shotName;

        /// <summary>Resolves a KeyCode name once and reuses it, so polling a key costs no allocations.</summary>
        private sealed class HotKey
        {
            private string _name;
            private KeyCode _code;
            private bool _valid;

            public bool Down(string name)
            {
                return Resolve(name) && SafeKey(true);
            }

            public bool Held(string name)
            {
                return Resolve(name) && SafeKey(false);
            }

            private bool Resolve(string name)
            {
                if (string.IsNullOrEmpty(name) || string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (_name != name)
                {
                    _name = name;
                    _valid = Enum.TryParse(name, true, out _code) && _code != KeyCode.None;
                }
                return _valid;
            }

            private bool SafeKey(bool down)
            {
                try { return down ? Input.GetKeyDown(_code) : Input.GetKey(_code); }
                catch { return false; }
            }
        }

        public override void OnInitializeMelon()
        {
            _settings = new Settings();
            _settings.Register();
            CrashLog.Init(LoggerInstance, StateText);
            Updater.Init(LoggerInstance, _settings);
            _autoFocus = new AutoFocus(LoggerInstance);
            _profiles = new ProfileStore(LoggerInstance);
            _worlds = new WorldProfiles(LoggerInstance);
            _misc = new MiscTweaks(_settings, LoggerInstance);
            _vrMenu = new VrWorldMenu(_settings, LoggerInstance);
            _volume = new GraphicsVolume(_settings, LoggerInstance, _autoFocus);
            _menu = new Menu(_settings, OnSettingsChanged, LoggerInstance, _autoFocus, _profiles, _worlds, _misc);
            LoggerInstance.Msg("VividFrame {0} ready. Press {1} for the centred glass menu. Profiles: {2}", VividFrameInfo.Version, _settings.MenuKey.Value, _profiles.DirectoryPath);
        }

        private string _sceneName = "none";

        private string StateText()
        {
            string menu = _menu != null ? _menu.StateText() : "menu=none";
            return "scene=" + _sceneName + " world=" + (_worlds != null && !string.IsNullOrEmpty(_worlds.CurrentName) ? _worlds.CurrentName : "?")
                   + " " + menu + " | " + (_settings != null ? _settings.StateSummary() : string.Empty);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _sceneName = sceneName;
            CrashLog.Mark("scene loaded: " + sceneName);
            TryReady("scene " + sceneName);
            _nextLayerRefresh = 0f;
            _misc?.OnSceneLoaded();
        }

        public override void OnLateUpdate()
        {
            _misc?.LateTick();
            CrashLog.Tick();
            string updateNotice = Updater.Tick();
            if (updateNotice != null)
                _menu?.Notify(updateNotice);
        }

        private void TickScreenshot()
        {
            if (_menu == null)
                return;

            if (_menu.ScreenshotRequested)
            {
                _menu.ScreenshotRequested = false;
                _menu.SetOpen(false);
                _shotWaitClosed = true;
            }
            else if (!_menu.Open && _shotFrames < 0 && !_shotWaitClosed && _shotNotifyFrames <= 0 && _shotKey.Down(_settings.ScreenshotKey.Value))
            {
                _shotFrames = 2;
            }

            if (_shotWaitClosed && !_menu.Visible)
            {
                _shotWaitClosed = false;
                _shotFrames = 2;
            }

            if (_shotFrames >= 0)
            {
                _menu.HideOverlays = true;
                _vrMenu?.HideHud(true);
                if (_shotFrames == 0)
                {
                    _shotName = _misc != null ? _misc.TakeScreenshot() : null;
                    _shotFrames = -1;
                    _shotNotifyFrames = 3;
                }
                else
                {
                    _shotFrames--;
                }
            }
            else if (_shotNotifyFrames > 0)
            {
                // Overlays stay hidden through the capture frame, then the result is shown.
                _shotNotifyFrames--;
                if (_shotNotifyFrames == 0)
                {
                    _menu.HideOverlays = false;
                    _vrMenu?.HideHud(false);
                    _menu.Notify(_shotName != null ? "Screenshot saved" : "Screenshot failed");
                }
            }
        }

        public override void OnUpdate()
        {
            if (_settings == null)
                return;

            VrSupport.Tick();
            _menu?.Tick();
            _misc?.SetMenuVisible(_menu != null && _menu.Visible);
            _vrMenu?.Tick(_menu);

            if (_menu != null && VrSupport.MenuToggleDown)
            {
                // The controller chord is not a keyboard key, so it works even while a key is being bound.
                VrSupport.ConsumeMenuToggleDown();
                _menu.Toggle();
            }
            else if (_menu != null && !_menu.IsCapturingKey)
            {
                if (WasMenuKeyPressed())
                    _menu.Toggle();
            }

            if (_menu != null && !_menu.Open)
                HandleHotkeys();
            PollWorld();
            TickScreenshot();

            try
            {
                _autoFocus?.Tick(_settings);
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Auto-focus tick failed: {0}", ex);
            }

            if (_ready && _settings.DepthOfField.Value && _autoFocus != null)
            {
                float focus = _autoFocus.Current;
                if (Math.Abs(focus - _lastAppliedFocus) > 0.01f)
                {
                    _lastAppliedFocus = focus;
                    try
                    {
                        _volume.Apply();
                    }
                    catch (Exception ex)
                    {
                        LoggerInstance.Error("Failed to apply auto-focus: {0}", ex);
                    }
                }
            }

            if (_saveDue > 0f && Time.unscaledTime >= _saveDue)
            {
                _saveDue = -1f;
                try
                {
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error("Could not save preferences: {0}", ex);
                }
            }

            if (_ready && Time.unscaledTime >= _nextLayerRefresh)
            {
                _nextLayerRefresh = Time.unscaledTime + LayerRefreshSeconds;
                _volume.Ensure();
            }
        }

        public override void OnGUI()
        {
            if (VrSupport.Active)
            {
                // In VR the same menu is drawn into the world-space panel's texture instead of the mirror.
                try
                {
                    _vrMenu?.RenderGui(_menu);
                }
                catch (Exception ex)
                {
                    if (!_guiErrorLogged)
                    {
                        _guiErrorLogged = true;
                        LoggerInstance.Error("VividFrame VR drawing failed: {0}", ex);
                    }
                }
                return;
            }
            if (_menu == null || !(_menu.Visible || _menu.HudActive || _menu.OverlaysActive))
                return;

            try
            {
                _menu.Draw();
            }
            catch (Exception ex)
            {
                if (!_guiErrorLogged)
                {
                    _guiErrorLogged = true;
                    LoggerInstance.Error("VividFrame OnGUI failed and the menu was closed so it cannot take the game down: {0}", ex);
                }

                try
                {
                    _menu?.SetOpen(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public override void OnApplicationQuit()
        {
            try { _settings?.Save(); }
            catch { }
            Updater.OnQuit();
            CrashLog.Shutdown();
        }

        private void HandleHotkeys()
        {
            bool held = _compareKey.Held(_settings.CompareKey.Value);
            if (held != _settings.CompareHeld)
            {
                _settings.CompareHeld = held;
                ApplyNow();
                if (held)
                    _menu.Notify("Showing the original look");
            }

            if (_masterKey.Down(_settings.MasterKey.Value))
            {
                _settings.MasterEnabled.Value = !_settings.MasterEnabled.Value;
                ApplyNow();
                _menu.Notify(_settings.MasterEnabled.Value ? "VividFrame on" : "VividFrame off");
                OnSettingsChanged();
            }

            if (_profileKey.Down(_settings.ProfileKey.Value))
            {
                string[] names = _profiles.ListNames();
                if (names.Length == 0)
                {
                    _menu.Notify("No saved profiles yet");
                    return;
                }
                _profileCycle = (_profileCycle + 1) % names.Length;
                string name = names[_profileCycle];
                if (_profiles.Load(name, _settings))
                {
                    ApplyNow();
                    _menu.Notify("Profile  " + name);
                    OnSettingsChanged();
                }
            }
        }

        private void PollWorld()
        {
            if (_worlds == null)
                return;
            // Always refresh so the menu shows the current world, but only load when auto-load is on.
            if (!_worlds.Refresh() || !_settings.AutoProfiles.Value)
                return;
            string profile = _worlds.PinnedForCurrent();
            if (string.IsNullOrEmpty(profile))
                return;
            if (_profiles.Load(profile, _settings))
            {
                ApplyNow();
                _menu?.Notify("World profile  " + profile);
                OnSettingsChanged();
            }
        }

        private void ApplyNow()
        {
            try
            {
                _volume?.Apply();
                _lastAppliedFocus = _autoFocus != null ? _autoFocus.Current : -1f;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Failed to apply settings: {0}", ex);
            }
        }

        public override void OnDeinitializeMelon()
        {
            _misc?.Restore();
            _vrMenu?.Dispose();
            _menu?.SetOpen(false);
            _menu?.Dispose();
            _volume?.Teardown();
            CrashLog.Shutdown();
        }

        private void TryReady(string reason)
        {
            try
            {
                _volume.Ensure();
                _volume.Apply();
                _ready = true;
            }
            catch (Exception ex)
            {
                _ready = false;
                LoggerInstance.Error("Could not create the graphics volume during {0}: {1}", reason, ex);
            }
        }

        private void OnSettingsChanged()
        {
            CrashLog.MarkEvery("settings", 2f, "settings changed, applying volume and misc tweaks");
            try
            {
                _volume?.Apply();
                _lastAppliedFocus = _autoFocus != null ? _autoFocus.Current : -1f;
                _misc?.ApplyNow();
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Failed to apply settings: {0}", ex);
            }

            // While the menu is open, saves wait for it to close (it autosaves then). Saving on
            // every slider tick wrote the config dozens of times while dragging.
            _saveDue = (_menu != null && _menu.Open) ? -1f : Time.unscaledTime + 0.4f;
        }

        private bool WasMenuKeyPressed()
        {
            string name = _settings.MenuKey.Value;
            if (string.IsNullOrEmpty(name))
                name = "Home";

            try
            {
                if (_cachedKeyName != name)
                {
                    _cachedKeyName = name;
                    _cachedKeyValid = Enum.TryParse(name, true, out _cachedKey);
                }
                if (_cachedKeyValid)
                    return Input.GetKeyDown(_cachedKey);
                return Input.GetKeyDown(name);
            }
            catch
            {
                return false;
            }
        }
    }
}
