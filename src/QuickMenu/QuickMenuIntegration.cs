using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>
    /// VividFrame inside VRChat's Quick Menu: a tab (or, without a tab bar, a dashboard button) that
    /// opens a root page with the same sections as the glass menu. Works in desktop and VR because it
    /// is the game's own menu. If anything it needs is missing, it logs one warning and stays out of
    /// the way; the glass menu and VR panel do not depend on it.
    /// </summary>
    internal sealed class QuickMenuIntegration : IDisposable
    {
        private const float RetrySeconds = 2f;
        private const int MaxAttempts = 90;

        private static readonly string[] Tonemappers = { "World", "None", "Neutral", "ACES" };
        private static readonly string[] AoModes = { "Scalable", "Multi-scale" };
        private static readonly string[] FocusModes = { "Auto", "Centre", "Manual" };
        private static readonly string[] Kernels = { "Small", "Medium", "Large", "Very large" };
        private static readonly string[] ProfileSlots = { "Profile 1", "Profile 2", "Profile 3" };

        private readonly Settings _settings;
        private readonly MelonLogger.Instance _log;
        private readonly Action _onChanged;
        private readonly ProfileStore _profiles;
        private readonly MiscTweaks _misc;
        private readonly Action _changedOnce;

        private const float FadeInSeconds = 0.14f;
        private const float FadeOutSeconds = 0.10f;
        private const float ApplyInterval = 0.05f;

        private static readonly HashSet<IntPtr> HookedTabs = new HashSet<IntPtr>();

        private Menu _menu;
        private QmKit _kit;
        private GameObject _tab;
        private QmButton _launcher;
        private QmPage _rootPage;
        private readonly List<QmPage> _pages = new List<QmPage>();
        private QmPage _current;
        private QmPage _closing;
        private float _fade;
        private Sprite _icon;
        private bool _built;
        private bool _failed;
        private bool _warned;
        private bool _wasQmVisible;
        private bool _dirty;
        private float _nextApply;
        private int _attempts;
        private float _nextAttempt;
        private float _qmSeenAt = -1f;

        // Native pages whose Canvas we switched off while our page covers them.
        private readonly List<GameObject> _hiddenPages = new List<GameObject>();
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

        public QuickMenuIntegration(Settings settings, MelonLogger.Instance log, Action onChanged, ProfileStore profiles, MiscTweaks misc)
        {
            _settings = settings;
            _log = log;
            _onChanged = onChanged;
            _profiles = profiles;
            _misc = misc;
            _changedOnce = MarkDirty;
            _instance = this;
        }

        public bool Built => _built;

        private void MarkDirty()
        {
            _dirty = true;
        }

        public void Tick(Menu menu)
        {
            _menu = menu;
            try
            {
                TickInner();
            }
            catch (Exception ex)
            {
                QmUtil.LogOnce(ref _warned, _log, "Quick Menu integration stopped: " + ex.Message);
                _failed = true;
                TearDown();
            }
        }

        private void TickInner()
        {
            // Apply at most 20 times a second while a slider streams changes, and once more at the end.
            // Saving is left to the mod, which debounces it after the last change.
            if (_dirty && Time.unscaledTime >= _nextApply)
            {
                _dirty = false;
                _nextApply = Time.unscaledTime + ApplyInterval;
                _onChanged();
            }

            if (!_settings.QuickMenuEnabled.Value)
            {
                if (_built)
                    TearDown();
                _failed = false;
                _attempts = 0;
                return;
            }

            bool entryGone = _built && (_kit.TabTemplate != null ? _tab == null : _launcher == null || _launcher.Root == null);
            if (_built && (entryGone || _rootPage == null || _rootPage.Root == null))
            {
                // Our objects went away with the Quick Menu (rebuild after a UI reload).
                TearDown();
                _attempts = 0;
            }

            if (!_built)
            {
                TryBuild();
                return;
            }

            Animate();

            bool qmVisible = _kit.IsVisible();
            if (_wasQmVisible && !qmVisible)
                CloseNow();
            _wasQmVisible = qmVisible;

            if (_current == null)
                return;
            if (!qmVisible || !_current.Active || NativeNavigated())
            {
                // The menu closed, something else hid our page, or VRChat changed page: reset cleanly.
                CloseNow();
                return;
            }
            _current.Refresh();
        }

        private void TryBuild()
        {
            if (_failed || Time.unscaledTime < _nextAttempt)
                return;
            _nextAttempt = Time.unscaledTime + RetrySeconds;
            GameObject qm = QmKit.FindQmRoot();
            if (qm == null)
            {
                _qmSeenAt = -1f;
                if (++_attempts == MaxAttempts)
                    _log.Msg("Quick Menu not found yet; still waiting for it to add the VividFrame tab.");
                if (_attempts >= MaxAttempts)
                    _nextAttempt = Time.unscaledTime + RetrySeconds * 5f;
                return;
            }
            // Let the menu finish building itself before cloning from it.
            if (_qmSeenAt < 0f)
            {
                _qmSeenAt = Time.unscaledTime;
                return;
            }

            QmKit kit = QmKit.Discover(qm);
            if (kit.Missing != null)
            {
                QmUtil.LogOnce(ref _warned, _log, "Quick Menu layout not recognised (missing " + kit.Missing + "); skipping the VividFrame Quick Menu tab.");
                _failed = true;
                return;
            }

            _kit = kit;
            try
            {
                Build();
                _built = true;
                _log.Msg("Quick Menu tab ready ({0}).", kit.Describe());
            }
            catch (Exception ex)
            {
                QmUtil.LogOnce(ref _warned, _log, "Could not build the VividFrame Quick Menu tab: " + ex.Message);
                _failed = true;
                TearDown();
            }
            finally
            {
                kit.DestroyStaging();
            }
        }

        private void Build()
        {
            _rootPage = NewPage("VividFrame", Close);
            QmPage visuals = NewPage("Visuals", OpenRoot);
            QmPage colour = NewPage("Colour", OpenRoot);
            QmPage effects = NewPage("Effects", OpenRoot);
            QmPage camera = NewPage("Camera", OpenRoot);
            QmPage presets = NewPage("Presets", OpenRoot);
            QmPage configs = NewPage("Configs", OpenRoot);
            QmPage settings = NewPage("Settings", OpenRoot);
            QmPage misc = NewPage("Misc", OpenRoot);

            Settings s = _settings;
            Action ch = _changedOnce;

            // Root
            _rootPage.Header("Quick");
            _rootPage.AddToggle("VividFrame", s.MasterEnabled, ch);
            _rootPage.AddButton("Glass\nmenu", OpenGlassMenu, false);
            _rootPage.Header("Sections");
            _rootPage.AddButton("Visuals", () => Open(visuals), true);
            _rootPage.AddButton("Colour", () => Open(colour), true);
            _rootPage.AddButton("Effects", () => Open(effects), true);
            _rootPage.AddButton("Camera", () => Open(camera), true);
            _rootPage.AddButton("Presets", () => Open(presets), true);
            _rootPage.AddButton("Configs", () => Open(configs), true);
            _rootPage.AddButton("Settings", () => Open(settings), true);
            _rootPage.AddButton("Misc", () => Open(misc), true);

            // Visuals
            visuals.Header("Effects");
            visuals.AddToggle("VividFrame", s.MasterEnabled, ch);
            visuals.AddToggle("Bloom", s.Bloom, ch);
            visuals.AddToggle("Motion\nblur", s.MotionBlur, ch);
            visuals.AddToggle("Vignette", s.Vignette, ch);
            visuals.AddToggle("Film\ngrain", s.Grain, ch);
            visuals.AddToggle("Chromatic\naberration", s.ChromaticAberration, ch);
            visuals.AddToggle("Ambient\nocclusion", s.AmbientOcclusion, ch);
            visuals.AddToggle("Depth of\nfield", s.DepthOfField, ch);
            visuals.AddToggle("Lens\ndistortion", s.LensDistortion, ch);
            visuals.Header("Resolution");
            visuals.AddSlider("Render scale", s.RenderScale, 0.5f, 1.5f, "0.00", ch);
            visuals.Header("Reset");
            visuals.AddButton("Reset all\nsettings", () => { s.ResetToDefaults(); ch(); Notify("Settings reset"); }, false);

            // Colour
            colour.Header("Grading");
            colour.AddSlider("Saturation", s.Saturation, -100f, 100f, "0", ch);
            colour.AddSlider("Vibrance", s.Vibrance, -100f, 100f, "0", ch);
            colour.AddSlider("Contrast", s.Contrast, -100f, 100f, "0", ch);
            colour.AddSlider("Brightness", s.Brightness, -2f, 2f, "0.00", ch);
            colour.AddSlider("Gamma", s.Gamma, 0.3f, 2.2f, "0.00", ch);
            colour.Header("White balance");
            colour.AddSlider("Temperature", s.Temperature, -100f, 100f, "0", ch);
            colour.AddSlider("Tint", s.Tint, -100f, 100f, "0", ch);
            colour.AddSlider("Hue shift", s.HueShift, -180f, 180f, "0", ch);
            colour.AddSlider("Sharpen", s.Sharpen, 0f, 100f, "0", ch);
            colour.Header("Tone");
            colour.AddCycle("Tonemapper", Tonemappers, s.Tonemapper, ch);
            colour.AddButton("Reset\ncolour", () => { s.ResetColour(); ch(); Notify("Colour reset"); }, false);

            // Effects
            effects.Header("Bloom");
            effects.AddToggle("Bloom", s.Bloom, ch);
            effects.AddSlider("Bloom intensity", s.BloomIntensity, 0f, 5f, "0.00", ch);
            effects.AddSlider("Bloom threshold", s.BloomThreshold, 0f, 3f, "0.00", ch);
            effects.AddSlider("Bloom soft knee", s.BloomSoftKnee, 0f, 1f, "0.00", ch);
            effects.AddSlider("Bloom diffusion", s.BloomDiffusion, 1f, 10f, "0.0", ch);
            effects.Header("Motion blur");
            effects.AddToggle("Motion\nblur", s.MotionBlur, ch);
            effects.AddSlider("Shutter angle", s.MotionBlurShutter, 0f, 360f, "0", ch);
            effects.AddSlider("Samples", s.MotionBlurSamples, 4, 32, ch);
            effects.Header("Vignette");
            effects.AddToggle("Vignette", s.Vignette, ch);
            effects.AddSlider("Vignette intensity", s.VignetteIntensity, 0f, 1f, "0.00", ch);
            effects.AddSlider("Vignette smoothness", s.VignetteSmoothness, 0.05f, 1f, "0.00", ch);
            effects.AddSlider("Vignette roundness", s.VignetteRoundness, 0f, 1f, "0.00", ch);
            effects.Header("Film grain");
            effects.AddToggle("Film\ngrain", s.Grain, ch);
            effects.AddToggle("Coloured\ngrain", s.GrainColored, ch);
            effects.AddSlider("Grain intensity", s.GrainIntensity, 0f, 1f, "0.00", ch);
            effects.AddSlider("Grain size", s.GrainSize, 0.3f, 3f, "0.00", ch);
            effects.Header("Lens");
            effects.AddToggle("Chromatic\naberration", s.ChromaticAberration, ch);
            effects.AddToggle("Lens\ndistortion", s.LensDistortion, ch);
            effects.AddSlider("Chromatic aberration", s.ChromaticAberrationIntensity, 0f, 1f, "0.00", ch);
            effects.AddSlider("Lens distortion", s.LensDistortionIntensity, -100f, 100f, "0", ch);
            effects.Header("Ambient occlusion");
            effects.AddToggle("Ambient\nocclusion", s.AmbientOcclusion, ch);
            effects.AddCycle("AO mode", AoModes, s.AoMode, ch);
            effects.AddSlider("AO intensity", s.AoIntensity, 0f, 4f, "0.00", ch);
            effects.AddSlider("AO radius", s.AoRadius, 0.05f, 2f, "0.00", ch);

            // Camera
            camera.Header("Depth of field");
            camera.AddToggle("Depth of\nfield", s.DepthOfField, ch);
            camera.AddCycle("Focus", FocusModes, s.FocusMode, ch);
            camera.AddCycle("Bokeh size", Kernels, s.DofKernelSize, ch);
            camera.AddSlider("Focus distance", s.DofFocusDistance, 0.1f, 50f, "0.0", ch);
            camera.AddSlider("Aperture", s.DofAperture, 0.1f, 32f, "0.0", ch);
            camera.AddSlider("Focal length", s.DofFocalLength, 1f, 300f, "0", ch);
            camera.Header("Field of view");
            camera.AddToggle("FOV\noverride", s.FovOverride, ch);
            camera.AddToggle("Reset FOV\nin menus", s.FovResetInMenus, ch);
            camera.AddSlider("Field of view", s.Fov, 40f, 130f, "0", ch);
            camera.AddSlider("Zoom FOV", s.ZoomFov, 5f, 90f, "0", ch);
            camera.Header("Clipping");
            camera.AddToggle("Near clip\noverride", s.NearClipOverride, ch);
            camera.AddToggle("Far clip\noverride", s.FarClipOverride, ch);
            camera.AddSlider("Near clip", s.NearClip, 0.01f, 2f, "0.00", ch);
            camera.AddSlider("Far clip", s.FarClip, 50f, 5000f, "0", ch);

            // Presets
            presets.Header("Looks");
            presets.AddButton("Vivid", () => Preset(s.ApplyPresetVivid, "Vivid"), false);
            presets.AddButton("Cinematic", () => Preset(s.ApplyPresetCinematic, "Cinematic"), false);
            presets.AddButton("Natural+", () => Preset(s.ApplyPresetNaturalPlus, "Natural+"), false);
            presets.AddButton("Night", () => Preset(s.ApplyPresetNight, "Night"), false);
            presets.Header("Utility");
            presets.AddButton("Performance", () => Preset(s.ApplyPresetPerformance, "Performance"), false);
            presets.AddButton("Off", () => Preset(s.ApplyPresetOff, "Off"), false);

            // Configs
            configs.Header("Save");
            for (int i = 0; i < ProfileSlots.Length; i++)
            {
                string slot = ProfileSlots[i];
                configs.AddButton("Save\n" + slot, () => SaveProfile(slot), false);
            }
            configs.Header("Load");
            for (int i = 0; i < ProfileSlots.Length; i++)
            {
                string slot = ProfileSlots[i];
                configs.AddButton("Load\n" + slot, () => LoadProfile(slot), false);
            }
            string[] saved = SafeProfileNames();
            if (saved.Length > 0)
            {
                configs.Header("Saved profiles");
                int shown = 0;
                for (int i = 0; i < saved.Length && shown < 16; i++)
                {
                    string name = saved[i];
                    if (Array.IndexOf(ProfileSlots, name) >= 0)
                        continue;
                    configs.AddButton(name, () => LoadProfile(name), false);
                    shown++;
                }
            }

            // Settings
            settings.Header("Quick Menu");
            settings.AddToggle("Quick Menu\ntab", s.QuickMenuEnabled, ch);
            settings.AddButton("Glass\nmenu", OpenGlassMenu, false);
            settings.Header("Glass menu");
            settings.AddToggle("Animations", s.AnimationsEnabled, ch);
            settings.AddToggle("Low-cost\nUI", s.LowCostUi, ch);
            settings.AddSlider("UI scale", s.UiScale, 0.75f, 2f, "0.00", ch);
            settings.AddSlider("Glass opacity", s.GlassOpacity, 0.45f, 1f, "0.00", ch);
            settings.Header("VR panel");
            settings.AddToggle("Keep panel\nin view", s.VrFollow, ch);
            settings.AddSlider("VR panel size", s.VrPanelSize, 0.7f, 1.4f, "0.00", ch);
            settings.Header("Profiles and updates");
            settings.AddToggle("Auto-load\nper world", s.AutoProfiles, ch);
            settings.AddToggle("Auto\nupdate", s.AutoUpdate, ch);
            settings.AddButton("Check for\nupdates", CheckUpdates, false);

            // Misc
            misc.Header("Overlays");
            misc.AddToggle("FPS\ncounter", s.FpsOverlay, ch);
            misc.AddToggle("Players", s.StatPlayers, ch);
            misc.AddToggle("Clock", s.StatClock, ch);
            misc.AddToggle("Session\ntimer", s.StatTimer, ch);
            misc.AddToggle("Position", s.StatPosition, ch);
            misc.AddToggle("RAM", s.StatRam, ch);
            misc.AddToggle("Crosshair", s.Crosshair, ch);
            misc.AddToggle("Cinema\nbars", s.CinemaBars, ch);
            misc.Header("Performance");
            misc.AddToggle("Quality\noverride", s.QualityOverride, ch);
            misc.AddToggle("VSync", s.VSync, ch);
            misc.AddSlider("FPS cap (0 = off)", s.FpsCap, 0, 360, ch);
            misc.AddSlider("LOD bias", s.LodBias, 0.3f, 4f, "0.00", ch);
            misc.AddSlider("Shadow distance", s.ShadowDistance, 0f, 150f, "0", ch);
            misc.Header("System");
            misc.AddToggle("Run in\nbackground", s.RunInBackground, ch);
            misc.AddToggle("Mute when\nunfocused", s.MuteUnfocused, ch);
            misc.AddToggle("Fog off", s.FogOff, ch);
            misc.AddButton("Free\nmemory", FreeMemory, false);

            BuildEntryPoint();
        }

        private QmPage NewPage(string title, Action onBack)
        {
            var page = new QmPage(_kit, title, onBack);
            _pages.Add(page);
            return page;
        }

        private void BuildEntryPoint()
        {
            if (_kit.TabTemplate != null)
            {
                _tab = _kit.Clone(_kit.TabTemplate, _kit.TabBar, "VF_Tab", go => QmKit.HideBadges(go.transform));
                _tab.transform.SetAsLastSibling();
                Transform iconT = QmUtil.FindDeep(_tab.transform, "Icon");
                Image icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (icon != null)
                {
                    if (_icon == null)
                        _icon = QmUtil.MakeIcon();
                    icon.sprite = _icon;
                    icon.overrideSprite = _icon;
                    icon.preserveAspect = true;
                }
                Transform labelT = QmUtil.FindDeep(_tab.transform, "Label");
                QmUtil.SetText(labelT != null ? QmUtil.FirstText(labelT) : QmUtil.FirstText(_tab.transform), "VividFrame");
                Button b = _kit.EnsureButton(_tab);
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener((Action)OpenRoot);

                // Picking any native tab leaves our page. Hooked once per tab for the whole session.
                for (int i = 0; i < _kit.TabBar.childCount; i++)
                {
                    Transform t = _kit.TabBar.GetChild(i);
                    if (t == _tab.transform)
                        continue;
                    Button native = t.GetComponent<Button>();
                    if (native != null && HookedTabs.Add(native.Pointer))
                        native.onClick.AddListener((Action)OnNativeTab);
                }
            }
            else
            {
                _launcher = new QmButton(_kit, _kit.GroupTemplate, "VividFrame", OpenRoot, true);
            }
        }

        private static QuickMenuIntegration _instance;

        private static void OnNativeTab()
        {
            QuickMenuIntegration self = _instance;
            if (self != null && self._built)
                self.CloseNow();
        }

        private void OpenRoot()
        {
            Open(_rootPage);
        }

        /// <summary>Shows one of our pages, covering the native page on first open.</summary>
        private void Open(QmPage page)
        {
            if (page == null || page.Root == null)
                return;
            if (_closing != null)
            {
                _closing.Hide();
                _closing = null;
            }
            if (_current == page)
                return;
            if (_current == null)
                HideNativePages();
            else
                _current.Hide();
            _current = page;
            _fade = 0f;
            page.SetFade(0f);
            page.Show(_hiddenPages.Count == 0);
        }

        /// <summary>Fades our page out and gives the native page back.</summary>
        private void Close()
        {
            if (_current == null)
                return;
            RestoreNativePages();
            _closing = _current;
            _current = null;
            _fade = 1f;
        }

        /// <summary>Immediate close, for native navigation and the Quick Menu closing.</summary>
        private void CloseNow()
        {
            if (_closing != null)
            {
                _closing.Hide();
                _closing = null;
            }
            if (_current != null)
            {
                _current.Hide();
                _current = null;
            }
            RestoreNativePages();
        }

        /// <summary>
        /// Switches off the Canvas of each visible native page next to ours, so the window shows our
        /// page on the Quick Menu's own background. Nothing else on those pages is touched.
        /// </summary>
        private void HideNativePages()
        {
            _hiddenPages.Clear();
            _hiddenCanvases.Clear();
            Transform parent = _rootPage.Root.transform.parent;
            if (parent == null)
                return;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                GameObject go = t.gameObject;
                if (!go.activeSelf || t.name.StartsWith("VF_", StringComparison.Ordinal))
                    continue;
                Canvas c = t.GetComponent<Canvas>();
                if (c == null || !c.enabled || !IsMenuPage(t))
                    continue;
                c.enabled = false;
                _hiddenPages.Add(go);
                _hiddenCanvases.Add(c);
            }
        }

        /// <summary>True for objects carrying VRChat's page component, so window decoration is never hidden.</summary>
        private static bool IsMenuPage(Transform t)
        {
            var comps = t.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                    continue;
                string name;
                try { name = c.GetIl2CppType().Name; }
                catch { continue; }
                if (name == "UIPage" || name.EndsWith("Page", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void RestoreNativePages()
        {
            for (int i = 0; i < _hiddenCanvases.Count; i++)
            {
                Canvas c = _hiddenCanvases[i];
                try
                {
                    if (c != null && !c.enabled)
                        c.enabled = true;
                }
                catch
                {
                }
            }
            _hiddenPages.Clear();
            _hiddenCanvases.Clear();
        }

        /// <summary>
        /// VRChat (or another mod) moved to a different page while ours was up: a covered page was
        /// closed or switched its Canvas back on. Step aside instead of fighting over the window.
        /// </summary>
        private bool NativeNavigated()
        {
            for (int i = 0; i < _hiddenPages.Count; i++)
            {
                GameObject go = _hiddenPages[i];
                Canvas c = _hiddenCanvases[i];
                if (go == null || c == null || !go.activeSelf || c.enabled)
                    return true;
            }
            return false;
        }

        private void Animate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_current != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + dt / FadeInSeconds);
                _current.SetFade(EaseOut(_fade));
            }
            else if (_closing != null)
            {
                _fade = Mathf.Max(0f, _fade - dt / FadeOutSeconds);
                _closing.SetFade(EaseOut(_fade));
                if (_fade <= 0f)
                {
                    _closing.Hide();
                    _closing = null;
                }
            }
        }

        private static float EaseOut(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private void Notify(string text)
        {
            _menu?.Notify(text);
        }

        private void Preset(Action apply, string name)
        {
            apply();
            _changedOnce();
            Notify("Applied " + name);
        }

        private void OpenGlassMenu()
        {
            _menu?.SetOpen(true);
        }

        private void CheckUpdates()
        {
            if (Updater.Busy)
            {
                Notify("Already checking for updates");
                return;
            }
            Updater.CheckNow(true);
            Notify("Checking for updates");
        }

        private void FreeMemory()
        {
            string result = _misc != null ? _misc.FreeMemory() : null;
            if (!string.IsNullOrEmpty(result))
                Notify(result);
        }

        private string[] SafeProfileNames()
        {
            try { return _profiles != null ? _profiles.ListNames() ?? Array.Empty<string>() : Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        private void SaveProfile(string name)
        {
            if (_profiles != null && _profiles.Save(name, _settings))
                Notify("Saved " + name);
            else
                Notify("Could not save " + name);
        }

        private void LoadProfile(string name)
        {
            if (_profiles != null && _profiles.Load(name, _settings))
            {
                _changedOnce();
                Notify("Loaded " + name);
            }
            else
            {
                Notify(name + " is empty");
            }
        }

        private void TearDown()
        {
            CloseNow();
            for (int i = 0; i < _pages.Count; i++)
            {
                try { _pages[i].Destroy(); }
                catch { }
            }
            _pages.Clear();
            _rootPage = null;
            if (_tab != null)
            {
                try { UnityEngine.Object.Destroy(_tab); }
                catch { }
            }
            _tab = null;
            if (_launcher != null && _launcher.Root != null)
            {
                try { UnityEngine.Object.Destroy(_launcher.Root); }
                catch { }
            }
            _launcher = null;
            _kit?.DestroyStaging();
            _kit = null;
            _built = false;
            _wasQmVisible = false;
            _qmSeenAt = -1f;
        }

        public void Dispose()
        {
            if (_instance == this)
                _instance = null;
            try { TearDown(); }
            catch { }
            if (_icon != null)
            {
                Texture2D tex = _icon.texture;
                UnityEngine.Object.Destroy(_icon);
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
                _icon = null;
            }
        }
    }
}
