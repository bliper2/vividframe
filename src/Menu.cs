using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// 1.4.4 monochrome glass menu. Uses only verified GUI.* overloads with explicit
    /// Rects. OnGUI Layout events are skipped; textures and labels are issued only on
    /// Repaint; animation and post-process applies tick once per frame in Tick().
    /// </summary>
    internal sealed class Menu
    {
        private static readonly string[] Tabs = { "Visuals", "Colour", "Effects", "Camera", "Presets", "Configs", "Settings", "Favourites", "Misc" };
        private static readonly string[][] SubTabs =
        {
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "Bloom", "Blur", "Lens", "AO" },
            new[] { "Focus", "Output" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "Overlays", "Camera", "Quality", "System" }
        };
        private static readonly string[] ScaleNames = { "1x", "2x", "3x", "4x" };
        private static readonly string[] TextureNames = { "Full", "Half", "Quarter", "Eighth" };
        private static readonly string[] ShadowModeNames = { "Off", "Hard", "Soft" };
        private static readonly string[] ShadowResNames = { "Low", "Medium", "High", "Ultra" };
        private static readonly string[] CascadeNames = { "1", "2", "4" };
        private static readonly string[] SkinNames = { "1 bone", "2 bones", "4 bones", "Max" };
        private static readonly string[] PriorityNames = { "Low", "Below", "Normal", "High" };
        private static readonly string[] FpsCornerNames = { "Top left", "Top right", "Bottom left", "Bottom right" };
        private static readonly string[] CrosshairNames = { "Dot", "Cross", "Plus" };
        private static readonly string[] MsaaNames = { "Off", "2x", "4x", "8x" };
        private static readonly string[] AccentNames = { "White", "Ice", "Violet", "Rose", "Mint" };
        private static readonly Color[] AccentColors =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(0.62f, 0.80f, 1f, 1f),
            new Color(0.76f, 0.64f, 1f, 1f),
            new Color(1f, 0.66f, 0.78f, 1f),
            new Color(0.62f, 1f, 0.82f, 1f)
        };
        private static readonly string[] HotKeyChoices = { "None", "Z", "X", "C", "V", "B", "N", "Tab", "LeftAlt", "F5", "F6", "F7" };
        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "Sharpen", "Adds midtone contrast. It is not an unsharp mask." },
            { "Vibrance", "Boosts saturation and a little contrast. Not a native effect; it is approximated with colour grading." },
            { "Saturation", "Very high values push worlds orange. Natural+ keeps it low." },
            { "RenderScale", "Needs dynamic resolution to be on in the game, otherwise it does nothing." },
            { "AoIntensity", "How dark the contact shadows get." },
            { "AoRadius", "How far ambient occlusion reaches from each surface." },
            { "BloomThreshold", "Only pixels brighter than this glow. Lower means more glow." },
            { "BloomIntensity", "Overall strength of the glow." },
            { "MotionBlurShutter", "Lower shutter angles smear less. Default is 90." },
            { "DofAperture", "Higher values keep more of the scene sharp." },
            { "DofFocusDistance", "Only used in Manual focus mode." },
            { "VignetteIntensity", "How dark the screen edges get." },
            { "GrainIntensity", "Strength of the film grain." },
            { "HueShift", "Rotates every colour around the wheel." },
            { "Tint", "Moves the white balance between green and magenta." },
            { "Temperature", "Negative is cooler and bluer. Positive is warmer." }
        };
        private static readonly string[] PresetNames = { "Vivid", "Cinematic", "Performance", "Natural+", "Night", "Off" };
        private static readonly string[] TonemapperNames = { "World", "None", "Neutral", "ACES" };
        private static readonly string[] AoModeNames = { "Scalable", "Multi-scale" };
        private static readonly string[] FocusNames = { "Auto", "Centre", "Manual" };
        private static readonly string[] KernelNames = { "Small", "Medium", "Large", "Very large" };
        private static readonly string[] ProfileSlots = { "Profile 1", "Profile 2", "Profile 3" };
        private static readonly string[] MenuKeys = { "Home", "Insert", "F1", "F2", "F8", "F9", "End", "Delete" };
        private const string Empty = "";
        private const int IdTabX = 11;
        private const int IdTabW = 12;
        private const int IdSubX = 14;
        private const int IdSubW = 15;
        private const int IdDrop = 16;
        private const float OpenSeconds = 0.22f;
        private const float CloseSeconds = 0.15f;
        private const float TabSeconds = 0.18f;
        private const float ToastSeconds = 1.6f;

        private readonly Settings _settings;
        private readonly Action _onChanged;
        private readonly MelonLogger.Instance _log;
        private readonly AutoFocus _autoFocus;
        private readonly ProfileStore _profiles;
        private readonly GlassTheme _theme = new GlassTheme();
        private readonly Anim _anim = new Anim();
        private readonly Rect[] _tabRects = new Rect[9];
        private readonly int[] _subTab = new int[9];
        private readonly Dictionary<int, float> _fmtValue = new Dictionary<int, float>(48);
        private readonly Dictionary<int, string> _fmtText = new Dictionary<int, string>(48);
        private readonly GUIContent _content = new GUIContent();

        private Rect _area;
        private Rect _bodyRect;
        private Vector2 _dragOffset;
        private float _scroll;
        private float _contentH;
        private int _tab;
        private int _tabFrom;
        private int _lastWidth;
        private int _lastHeight;
        private float _uiScale = 1f;
        private float _openT;
        private float _tabMix = 1f;
        private float _toastAge = 99f;
        private string _toast = Empty;
        private string _profileName = Empty;
        private string _selectedProfile;
        private string _activePreset;
        private string _lastConfigName;
        private string _status = Empty;
        private string[] _profileList = Array.Empty<string>();
        private bool _wantOpen;
        private bool _userDragged;
        private bool _dragging;
        private bool _cursorStolen;
        private bool _guiErrorLogged;
        private bool _textFieldMissing;
        private bool _matrixOk = true;
        private bool _paint;
        private bool _lowCost;
        private bool _applyQueued;
        private bool _captureKey;
        private int _activeSlider;
        private int _dropId;
        private int _dropIndex;
        private string[] _dropOptions;
        private Rect _dropHeader;
        private Rect _panelArea;
        private Action<int> _dropPick;
        private bool _dropJustOpened;
        private CursorLockMode _savedLock;
        private bool _savedVisible;

        private readonly WorldProfiles _worlds;
        private delegate void RowDrawer(float x, ref float y, float w);
        private readonly Dictionary<string, RowDrawer> _favDrawers = new Dictionary<string, RowDrawer>(48);
        private readonly List<string> _favOrder = new List<string>(48);
        private readonly HashSet<string> _favSet = new HashSet<string>(StringComparer.Ordinal);
        private bool _dirty;

        // Right-click to type an exact slider value.
        private string _editKey;
        private string _editText = Empty;
        private Rect _editRect;
        private bool _editFocusPending;
        private bool _focusApiMissing;

        // Hover tooltips.
        private string _tipFrameKey;
        private string _tipHoverKey;
        private float _tipSince;
        private Rect _tipRect;

        // Frosted blur snapshot.
        private Texture2D _blurTex;
        private readonly List<KeyValuePair<Texture2D, int>> _retiredBlurTextures = new List<KeyValuePair<Texture2D, int>>(2);
        private bool _captureWait;
        private float _captureDeadline;
        private bool _blurBroken;
        private bool _blurLogged;
        private bool _sentHighSatWarning;

        public bool Open { get { return _wantOpen; } }
        public bool Visible { get { return _wantOpen || _openT > 0.001f; } }
        public bool IsCapturingKey { get { return _captureKey; } }
        public bool HudActive { get { return !Visible && !string.IsNullOrEmpty(_toast) && _toastAge < ToastSeconds; } }
        public bool HideOverlays;
        public bool ScreenshotRequested;
        public bool OverlaysActive
        {
            get
            {
                return !HideOverlays && (_settings.FpsOverlay.Value || _settings.Crosshair.Value || _settings.CinemaBars.Value
                                         || _settings.StatClock.Value || _settings.StatTimer.Value || _settings.StatPosition.Value || _settings.StatRam.Value
                                         || _settings.StatPlayers.Value);
            }
        }

        private float _fpsSmooth = 60f;
        private float _fpsTextAt;
        private string _fpsText = "60 FPS";

        private void DrawOverlays()
        {
            if (HideOverlays || !_paint)
                return;
            float s = _uiScale;
            if (_settings.CinemaBars.Value)
            {
                float bar = Mathf.Clamp(_settings.CinemaThickness.Value, 0.02f, 0.3f) * Screen.height;
                Color c = new Color(0f, 0f, 0f, Mathf.Clamp01(_settings.CinemaOpacity.Value));
                _theme.FillRect(new Rect(0f, 0f, Screen.width, bar), c);
                _theme.FillRect(new Rect(0f, Screen.height - bar, Screen.width, bar), c);
            }

            for (int i = 0; i < 4; i++)
                _cornerUsed[i] = 0f;
            if (_settings.FpsOverlay.Value)
                OverlayPill(Mathf.Clamp(_settings.FpsCorner.Value, 0, 3), _fpsText, 132f * s, s);
            int statsCorner = Mathf.Clamp(_settings.StatsCorner.Value, 0, 3);
            if (_settings.StatClock.Value)
                OverlayPill(statsCorner, DateTime.Now.ToString("HH:mm:ss"), 132f * s, s);
            if (_settings.StatTimer.Value)
                OverlayPill(statsCorner, "Session  " + TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"hh\:mm\:ss"), 150f * s, s);
            if (_settings.StatPlayers.Value && _misc != null)
                OverlayPill(statsCorner, _misc.PlayersText(), 132f * s, s);
            if (_settings.StatRam.Value && _misc != null)
                OverlayPill(statsCorner, _misc.RamMegabytes().ToString("0") + " MB RAM", 132f * s, s);
            if (_settings.StatPosition.Value && _misc != null)
                OverlayPill(statsCorner, _misc.CameraPositionText(), 230f * s, s);

            if (_settings.Crosshair.Value)
                DrawCrosshair(s);
        }

        private readonly float[] _cornerUsed = new float[4];

        private void OverlayPill(int corner, string text, float w, float s)
        {
            float h = 24f * s;
            float m = 14f * s;
            float gap = 6f * s;
            bool left = corner == 0 || corner == 2;
            bool top = corner == 0 || corner == 1;
            float x = left ? m : Screen.width - w - m;
            float offset = _cornerUsed[corner];
            float y = top ? m + offset : Screen.height - h - m - offset;
            Rect r = new Rect(x, y, w, h);
            _theme.Draw(Shape.Popup, r, h * 0.5f);
            Label(r, text, Style(_theme.Center));
            _cornerUsed[corner] = offset + h + gap;
        }

        private void DrawCrosshair(float s)
        {
            float cx = Mathf.Round(Screen.width * 0.5f);
            float cy = Mathf.Round(Screen.height * 0.5f);
            float size = Mathf.Max(2f, _settings.CrosshairSize.Value * s);
            float gap = Mathf.Max(0f, _settings.CrosshairGap.Value * s);
            float t = Mathf.Max(2f, Mathf.Round(1.6f * s));
            float a = Mathf.Clamp01(_settings.CrosshairOpacity.Value);
            Color main = new Color(_theme.Accent.r, _theme.Accent.g, _theme.Accent.b, a);
            Color edge = new Color(0f, 0f, 0f, a * 0.65f);
            int style = _settings.CrosshairStyle.Value;
            if (style == 0 || style == 2)
            {
                float d = style == 0 ? Mathf.Max(4f, size * 0.7f) : Mathf.Max(3f, t * 1.8f);
                Color prev = GUI.color;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * a);
                _theme.DrawKnob(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d));
                GUI.color = prev;
            }
            if (style == 1 || style == 2)
            {
                float o = 1f;
                Rect[] bars =
                {
                    new Rect(cx - gap - size, cy - t * 0.5f, size, t),
                    new Rect(cx + gap, cy - t * 0.5f, size, t),
                    new Rect(cx - t * 0.5f, cy - gap - size, t, size),
                    new Rect(cx - t * 0.5f, cy + gap, t, size)
                };
                for (int i = 0; i < bars.Length; i++)
                {
                    Rect b = bars[i];
                    _theme.FillRect(new Rect(b.x - o, b.y - o, b.width + o * 2f, b.height + o * 2f), edge);
                    _theme.FillRect(b, main);
                }
            }
        }

        private void TickFps()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
                _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / dt, 0.08f);
            if (Time.unscaledTime >= _fpsTextAt)
            {
                _fpsTextAt = Time.unscaledTime + 0.25f;
                _fpsText = Mathf.RoundToInt(_fpsSmooth) + " FPS  " + (1000f / Mathf.Max(1f, _fpsSmooth)).ToString("0.0") + "ms";
            }
        }

        private readonly MiscTweaks _misc;

        public Menu(Settings settings, Action onChanged, MelonLogger.Instance log, AutoFocus autoFocus, ProfileStore profiles, WorldProfiles worlds, MiscTweaks misc)
        {
            _misc = misc;
            _settings = settings;
            _onChanged = onChanged;
            _log = log;
            _autoFocus = autoFocus;
            _profiles = profiles;
            _worlds = worlds;
            RegisterFavourites();
            LoadFavourites();
        }

        public string StateText()
        {
            return "menu=" + (_wantOpen ? "open" : "closed") + " tab=" + Tabs[Mathf.Clamp(_tab, 0, Tabs.Length - 1)]
                   + (_resizing ? " resizing" : string.Empty) + (_captureWait ? " capturing" : string.Empty)
                   + " ui=" + _uiScale.ToString("0.00");
        }

        /// <summary>Shows a short message even when the menu is closed.</summary>
        public void Notify(string message)
        {
            Toast(message);
        }

        private void RegisterFavourites()
        {
            FavSlider("Saturation", _settings.Saturation, -100f, 100f, "0");
            FavSlider("Vibrance", _settings.Vibrance, -100f, 100f, "0");
            FavSlider("Contrast", _settings.Contrast, -100f, 100f, "0");
            FavSlider("Brightness", _settings.Brightness, -2f, 2f, "0.00");
            FavSlider("Gamma", _settings.Gamma, 0.3f, 2.2f, "0.00");
            FavSlider("Temperature", _settings.Temperature, -100f, 100f, "0");
            FavSlider("Tint", _settings.Tint, -100f, 100f, "0");
            FavSlider("Hue shift", _settings.HueShift, -180f, 180f, "0");
            FavSlider("Sharpen", _settings.Sharpen, 0f, 100f, "0");
            FavSlider("Bloom intensity", _settings.BloomIntensity, 0f, 5f, "0.00");
            FavSlider("Bloom threshold", _settings.BloomThreshold, 0f, 3f, "0.00");
            FavSlider("Vignette intensity", _settings.VignetteIntensity, 0f, 1f, "0.00");
            FavSlider("Grain intensity", _settings.GrainIntensity, 0f, 1f, "0.00");
            FavSlider("CA intensity", _settings.ChromaticAberrationIntensity, 0f, 1f, "0.00");
            FavSlider("AO intensity", _settings.AoIntensity, 0f, 4f, "0.00");
            FavSlider("AO radius", _settings.AoRadius, 0.05f, 2f, "0.00");
            FavSlider("Aperture", _settings.DofAperture, 0.1f, 32f, "0.0");
            FavSlider("Focus distance", _settings.DofFocusDistance, 0.1f, 50f, "0.0");
            FavSlider("Focal length", _settings.DofFocalLength, 1f, 300f, "0");
            FavSlider("Shutter angle", _settings.MotionBlurShutter, 0f, 360f, "0");
            FavSlider("Lens distortion", _settings.LensDistortionIntensity, -100f, 100f, "0");
            FavSlider("Render scale", _settings.RenderScale, 0.5f, 1.5f, "0.00");
            FavSwitch("Enabled", _settings.MasterEnabled);
            FavSwitch("Bloom", _settings.Bloom);
            FavSwitch("Motion blur", _settings.MotionBlur);
            FavSwitch("Vignette", _settings.Vignette);
            FavSwitch("Film grain", _settings.Grain);
            FavSwitch("Chromatic aberration", _settings.ChromaticAberration);
            FavSwitch("Ambient occlusion", _settings.AmbientOcclusion);
            FavSwitch("Depth of field", _settings.DepthOfField);
            FavSwitch("Lens distortion", _settings.LensDistortion);
        }

        private void FavSlider(string label, MelonPreferences_Entry<float> entry, float min, float max, string format)
        {
            string key = entry.Identifier;
            _favOrder.Add(key);
            _favDrawers[key] = (float x, ref float y, float w) => Slider(x, ref y, w, label, entry, min, max, format);
        }

        private void FavSwitch(string label, MelonPreferences_Entry<bool> entry)
        {
            string key = entry.Identifier;
            _favOrder.Add(key);
            _favDrawers[key] = (float x, ref float y, float w) => Switch(x, ref y, w, label, entry);
        }

        private void LoadFavourites()
        {
            _favSet.Clear();
            string raw = _settings.Favourites != null ? _settings.Favourites.Value : null;
            if (string.IsNullOrEmpty(raw))
                return;
            foreach (string part in raw.Split(','))
            {
                string key = part.Trim();
                if (key.Length > 0)
                    _favSet.Add(key);
            }
        }

        private void ToggleFavourite(string key)
        {
            if (!_favDrawers.ContainsKey(key))
            {
                Toast("Can't pin that control");
                return;
            }
            bool added = _favSet.Add(key);
            if (!added)
                _favSet.Remove(key);
            _settings.Favourites.Value = string.Join(",", _favSet);
            _dirty = true;
            Toast(added ? "Pinned to Favourites" : "Removed from Favourites");
        }

        public void Toggle()
        {
            SetOpen(!_wantOpen);
        }

        public void SetOpen(bool open)
        {
            if (_wantOpen == open)
                return;
            CrashLog.Mark(open ? "menu opened" : "menu closed");
            _wantOpen = open;
            _captureKey = false;
            CloseDropdown();
            if (open)
            {
                if (!_userDragged)
                    Recenter();
                else
                    ClampToScreen();
                RefreshProfiles();
                StealCursor();
                _editKey = null;
                BeginBlurCapture();
                WarnIfColoursExtreme();
            }
            else
            {
                _dragging = false;
                _resizing = false;
                _activeSlider = 0;
                _editKey = null;
                _captureWait = false;
                Autosave();
                RestoreCursor();
            }
        }

        public void Dispose()
        {
            RestoreCursor();
            if (_blurTex != null)
                UnityEngine.Object.Destroy(_blurTex);
            _blurTex = null;
            for (int i = 0; i < _retiredBlurTextures.Count; i++)
            {
                Texture2D retired = _retiredBlurTextures[i].Key;
                if (retired != null)
                    UnityEngine.Object.Destroy(retired);
            }
            _retiredBlurTextures.Clear();
            _theme.Dispose();
        }

        private void WarnIfColoursExtreme()
        {
            if (_sentHighSatWarning)
                return;
            if (Mathf.Abs(_settings.Saturation.Value) >= 80f || Mathf.Abs(_settings.Vibrance.Value) >= 80f)
            {
                _sentHighSatWarning = true;
                Toast("Colours are very high. Try Reset colour");
            }
        }

        public void Tick()
        {
            SweepRetiredBlurTextures();
            SyncMotion();
            int accent = _settings.AccentIndex != null ? Mathf.Clamp(_settings.AccentIndex.Value, 0, AccentColors.Length - 1) : 0;
            _theme.Accent = AccentColors[accent];

            if (_captureWait && Time.unscaledTime > _captureDeadline)
                _captureWait = false;
            if (_settings.FpsOverlay.Value)
                TickFps();

            float duration = _wantOpen ? OpenSeconds : CloseSeconds;
            if (_captureWait)
                _openT = 0f;
            else if (_anim.Enabled)
                _openT = Mathf.MoveTowards(_openT, _wantOpen ? 1f : 0f, _anim.Dt / duration);
            else
                _openT = _wantOpen ? 1f : 0f;

            if (_tabMix < 1f)
                _tabMix = _anim.Enabled ? Mathf.MoveTowards(_tabMix, 1f, _anim.Dt / TabSeconds) : 1f;

            if (!string.IsNullOrEmpty(_toast) || _toastAge < ToastSeconds)
                _toastAge += _anim.Enabled ? _anim.Dt : Time.unscaledDeltaTime;

            if (_applyQueued)
            {
                _applyQueued = false;
                try { _onChanged(); }
                catch (Exception ex) { _log?.Error("Failed to apply settings: {0}", ex); }
            }

            if (_dropId != 0)
                _anim.Move(IdDrop, 1f, 0.16f, 0f);
        }

        private void DrawHud()
        {
            Event ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint)
                return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            _theme.Ensure(s, _log);
            _paint = true;
            _uiScale = s;
            DrawOverlays();
            if (!HudActive)
                return;
            float inT = Anim.EaseOutCubic(Mathf.Clamp01(_toastAge / 0.18f));
            float outT = _toastAge > ToastSeconds - 0.35f
                ? 1f - Anim.EaseOutCubic(Mathf.Clamp01((_toastAge - (ToastSeconds - 0.35f)) / 0.35f))
                : 1f;
            float alpha = Mathf.Min(inT, outT);
            if (alpha <= 0.01f)
                return;
            float w = 260f * s;
            float h = 30f * s;
            Rect r = new Rect((Screen.width - w) * 0.5f, 28f * s + (1f - inT) * -8f * s, w, h);
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            _theme.Draw(Shape.Popup, r, h * 0.5f);
            _theme.DrawAccent(Shape.PillActive, r, h * 0.5f, 0.55f);
            Label(r, _toast, Style(_theme.Center));
            GUI.color = prev;
        }

        public void Draw()
        {
            if (!Visible)
            {
                if (HudActive || OverlaysActive)
                    DrawHud();
                return;
            }

            Event ev = Event.current;
            if (ev == null)
                return;

            EventType type = ev.type;
            if (type == EventType.Layout)
                return;

            _paint = type == EventType.Repaint;
            bool input = type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag
                || type == EventType.ScrollWheel || type == EventType.KeyDown;
            if (!_paint && !input)
                return;

            try
            {
                if (_openT <= 0.001f)
                    return;

                UpdateLayout();
                _theme.Ensure(_uiScale, _log);
                if (_paint)
                    DrawOverlays();
                HandleInput(ev);
                DrawChrome();
            }
            catch (Exception ex)
            {
                if (!_guiErrorLogged)
                {
                    _guiErrorLogged = true;
                    _log?.Error("VividFrame menu failed and was closed so it cannot take the game down: {0}", ex);
                }
                try { SetOpen(false); } catch { _wantOpen = false; }
            }
        }

        private void SyncMotion()
        {
            _lowCost = _settings != null && _settings.LowCostUi != null && _settings.LowCostUi.Value;
            bool animate = !_lowCost && _settings != null && _settings.AnimationsEnabled != null && _settings.AnimationsEnabled.Value;
            _anim.Enabled = animate;
            _anim.Speed = (_settings != null && _settings.AnimationSpeed != null) ? _settings.AnimationSpeed.Value : 1f;
        }

        private void HandleInput(Event ev)
        {
            if (_dropId != 0 && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                CloseDropdown();
                ev.Use();
                return;
            }

            if (_captureKey && ev.type == EventType.KeyDown)
            {
                KeyCode key = ev.keyCode;
                if (key != KeyCode.None && key != KeyCode.Escape && key != KeyCode.Mouse0 && key != KeyCode.Mouse1)
                {
                    _settings.MenuKey.Value = key.ToString();
                    _captureKey = false;
                    Toast("Menu key  " + key);
                    ev.Use();
                    return;
                }
                if (key == KeyCode.Escape)
                {
                    _captureKey = false;
                    ev.Use();
                    return;
                }
            }

            Vector2 mouse = MouseGui();
            if (_editKey != null && ev.type == EventType.MouseDown && !_editRect.Contains(mouse))
                _editKey = null;
            if (_activeSlider != 0 && !MouseHeld())
                _activeSlider = 0;

            if (_resizing)
            {
                if (MouseHeld())
                    ApplyResize(mouse);
                else
                    EndResize();
                if (ev.type == EventType.MouseDrag || ev.type == EventType.MouseUp)
                    ev.Use();
                return;
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && _dropId == 0 && _openT > 0.95f && !_dragging)
            {
                int corner = CornerAt(mouse);
                if (corner >= 0)
                {
                    BeginResize(corner);
                    ev.Use();
                    return;
                }
            }

            if (_dragging)
            {
                if (MouseHeld())
                {
                    Vector2 next = mouse - _dragOffset;
                    if ((next - new Vector2(_area.x, _area.y)).sqrMagnitude > 4f)
                        _userDragged = true;
                    _area.x = next.x;
                    _area.y = next.y;
                    ClampToScreen();
                }
                else
                {
                    _dragging = false;
                }
            }

            if (ev.type == EventType.ScrollWheel && _bodyRect.width > 0f && _bodyRect.Contains(mouse))
            {
                if (_dropId != 0)
                {
                    CloseDropdown();
                    ev.Use();
                    return;
                }
                _scroll += ev.delta.y * 18f * _uiScale;
                ev.Use();
            }

            float maxScroll = Mathf.Max(0f, _contentH - _bodyRect.height);
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);
        }

        // ------------------------------------------------------------------ corner resize

        private bool _resizing;
        private int _resizeCorner;
        private Vector2 _resizeAnchor;

        private Rect CornerRect(int corner)
        {
            float z = 16f * _uiScale;
            float x = (corner == 0 || corner == 2) ? _area.x : _area.xMax - z;
            float y = (corner == 0 || corner == 1) ? _area.y : _area.yMax - z;
            return new Rect(x, y, z, z);
        }

        private int CornerAt(Vector2 mouse)
        {
            for (int i = 0; i < 4; i++)
            {
                if (CornerRect(i).Contains(mouse))
                    return i;
            }
            return -1;
        }

        private void BeginResize(int corner)
        {
            CrashLog.Mark("resize start corner " + corner);
            _resizing = true;
            _resizeCorner = corner;
            _userDragged = true;
            _dragging = false;
            _editKey = null;
            CloseDropdown();
            // The opposite corner stays put while this one is dragged.
            float ax = (corner == 0 || corner == 2) ? _area.xMax : _area.x;
            float ay = (corner == 0 || corner == 1) ? _area.yMax : _area.y;
            _resizeAnchor = new Vector2(ax, ay);
        }

        private void ApplyResize(Vector2 mouse)
        {
            float screenFactor = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float rawW = Mathf.Abs(mouse.x - _resizeAnchor.x) / GlassTheme.BaseWidth;
            float rawH = Mathf.Abs(mouse.y - _resizeAnchor.y) / GlassTheme.BaseHeight;
            float total = Mathf.Max(rawW, rawH);
            float fit = Mathf.Min((Screen.width - 16f) / GlassTheme.BaseWidth, (Screen.height - 16f) / GlassTheme.BaseHeight);
            float hi = Mathf.Min(fit, 2f * screenFactor);
            float lo = Mathf.Min(0.75f * screenFactor, hi);
            total = Mathf.Clamp(total, lo, hi);
            total = Mathf.Clamp(Mathf.Round(total / 0.05f) * 0.05f, lo, hi);   // steps keep texture rebuilds rare

            _uiScale = total;
            _settings.UiScale.Value = Mathf.Clamp(total / screenFactor, 0.75f, 2f);
            float width = GlassTheme.BaseWidth * total;
            float height = GlassTheme.BaseHeight * total;
            float x = (_resizeCorner == 0 || _resizeCorner == 2) ? _resizeAnchor.x - width : _resizeAnchor.x;
            float y = (_resizeCorner == 0 || _resizeCorner == 1) ? _resizeAnchor.y - height : _resizeAnchor.y;
            _area = new Rect(x, y, width, height);
        }

        private void EndResize()
        {
            CrashLog.Mark("resize end ui=" + _uiScale.ToString("0.00"));
            _resizing = false;
            ClampToScreen();
            _dirty = true;
            _fmtValue.Clear();
            _fmtText.Clear();
        }

        private void DrawGrips(Rect area)
        {
            if (!_paint)
                return;
            Vector2 mouse = MouseGui();
            for (int i = 0; i < 4; i++)
            {
                bool hot = _resizing ? _resizeCorner == i : CornerRect(i).Contains(mouse);
                float len = 10f * _uiScale;
                float t = Mathf.Max(1.5f, 1.6f * _uiScale);
                float inset = 7f * _uiScale;
                bool left = i == 0 || i == 2;
                bool top = i == 0 || i == 1;
                float cx = left ? area.x + inset : area.xMax - inset;
                float cy = top ? area.y + inset : area.yMax - inset;
                Color c = new Color(1f, 1f, 1f, hot ? 0.85f : 0.22f);
                _theme.FillRect(new Rect(left ? cx : cx - len, top ? cy : cy - t, len, t), c);
                _theme.FillRect(new Rect(left ? cx : cx - t, top ? cy : cy - len, t, len), c);
            }
        }

        private void UpdateLayout()
        {
            int w = Screen.width;
            int h = Screen.height;
            float user = (_settings != null && _settings.UiScale != null) ? Mathf.Clamp(_settings.UiScale.Value, 0.75f, 2f) : 1f;
            float scale = Mathf.Clamp(h / 1080f, 0.75f, 2f) * user;
            if (w != _lastWidth || h != _lastHeight || Math.Abs(scale - _uiScale) > 0.01f)
            {
                _lastWidth = w;
                _lastHeight = h;
                _uiScale = scale;
                if (!_userDragged)
                {
                    Recenter();
                }
                else
                {
                    // Keep the user's position but follow the new scale.
                    _area.width = Mathf.Max(480f, GlassTheme.BaseWidth * scale);
                    _area.height = Mathf.Max(320f, GlassTheme.BaseHeight * scale);
                    ClampToScreen();
                }
            }
            else if (_area.width < 32f)
            {
                Recenter();
            }
        }

        private void Recenter()
        {
            float scale = _uiScale;
            if (scale < 0.1f)
                scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            _uiScale = scale;
            float width = Mathf.Min(GlassTheme.BaseWidth * scale, Screen.width - 24f);
            float height = Mathf.Min(GlassTheme.BaseHeight * scale, Screen.height - 24f);
            width = Mathf.Max(480f, width);
            height = Mathf.Max(320f, height);
            _area = new Rect(
                Mathf.Round((Screen.width - width) * 0.5f),
                Mathf.Round((Screen.height - height) * 0.5f),
                width,
                height);
            ClampToScreen();
        }

        private void ClampToScreen()
        {
            float width = Mathf.Min(_area.width, Screen.width - 16f);
            float height = Mathf.Min(_area.height, Screen.height - 16f);
            float x = Mathf.Clamp(_area.x, 8f, Mathf.Max(8f, Screen.width - width - 8f));
            float y = Mathf.Clamp(_area.y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
            _area = new Rect(x, y, width, height);
        }

        private void DrawChrome()
        {
            float fade = Anim.EaseOutCubic(Mathf.Clamp01(_openT));
            float easedScale = _wantOpen ? Anim.EaseOutBack(_openT) : Anim.EaseOutCubic(_openT);
            float scale = Mathf.Lerp(0.94f, 1f, easedScale);
            float slide = (1f - fade) * (14f * _uiScale);

            Rect area = _area;
            area.y += slide;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = default;
            bool pushed = false;
            if (_paint)
                GUI.color = new Color(1f, 1f, 1f, fade);
            if (_paint && !_lowCost)
                pushed = PushScale(area, scale, ref prevMatrix);
            DrawPanel(area);
            DrawGrips(area);

            float pad = GlassTheme.Pad * _uiScale;
            float titleH = GlassTheme.TitleHeight * _uiScale;
            float tabH = GlassTheme.TabHeight * _uiScale;
            float subH = HasSubTabs(_tab) ? GlassTheme.SubTabHeight * _uiScale : 0f;
            float footerH = GlassTheme.FooterHeight * _uiScale;
            float gap = 6f * _uiScale;

            Rect top = new Rect(area.x + pad, area.y + gap, area.width - pad * 2f, titleH);
            DrawTopBar(top);

            Rect tabs = new Rect(area.x + pad, top.yMax + 2f * _uiScale, area.width - pad * 2f, tabH);
            DrawTabs(tabs);

            float bodyTop = tabs.yMax + gap;
            if (subH > 0f)
            {
                Rect subs = new Rect(area.x + pad, bodyTop, area.width - pad * 2f, subH);
                DrawSubTabs(subs);
                bodyTop = subs.yMax + gap;
            }

            Rect footer = new Rect(area.x + pad, area.yMax - pad - footerH, area.width - pad * 2f, footerH);
            _bodyRect = new Rect(area.x + pad, bodyTop, area.width - pad * 2f, footer.y - gap - bodyTop);
            if (_paint)
                _tipFrameKey = null;
            if (_bodyRect.height > 8f)
                DrawBody(_bodyRect);
            _panelArea = area;
            DrawFooter(footer);
            DrawToast(footer);
            if (_dropId != 0)
                DrawDropdownOverlay();
            else
                DrawTooltip();

            if (pushed)
                GUI.matrix = prevMatrix;
            if (_paint)
                GUI.color = prevColor;
        }

        private bool PushScale(Rect area, float scale, ref Matrix4x4 previous)
        {
            if (!_matrixOk || Math.Abs(scale - 1f) <= 0.0005f)
                return false;
            try
            {
                previous = GUI.matrix;
                Vector3 pivot = new Vector3(area.x + area.width * 0.5f, area.y + area.height * 0.5f, 0f);
                GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(scale, scale, 1f))
                    * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one)
                    * GUI.matrix;
                return true;
            }
            catch
            {
                _matrixOk = false;
                return false;
            }
        }

        private void DrawPanel(Rect area)
        {
            if (!_paint)
                return;
            float radius = GlassTheme.PanelRadius * _uiScale;
            if (_resizing)
            {
                // Building a full-size glass texture every step would stutter; draw a cheap stand-in.
                _theme.FillRounded(area, radius, new Color(0.07f, 0.07f, 0.09f, 0.62f));
                _theme.DrawApproxRim(area, new Color(1f, 1f, 1f, 0.28f));
                return;
            }
            if (!_lowCost)
                _theme.DrawShadow(area, radius);

            if (_blurTex != null && _settings.FrostBlur.Value && !_blurBroken)
            {
                // Inset by ~0.29 of the corner radius so the square snapshot stays inside the rounded glass.
                float inset = Mathf.Ceil(radius * 0.3f);
                Rect ir = new Rect(area.x + inset, area.y + inset, area.width - inset * 2f, area.height - inset * 2f);
                float sw = Mathf.Max(1f, Screen.width);
                float sh = Mathf.Max(1f, Screen.height);
                Rect uv = new Rect(ir.x / sw, 1f - ir.yMax / sh, ir.width / sw, ir.height / sh);
                try
                {
                    GUI.DrawTextureWithTexCoords(ir, _blurTex, uv);
                }
                catch (Exception ex)
                {
                    _blurBroken = true;
                    _log?.Warning("Frosted blur drawing failed and was turned off: {0}", ex.Message);
                }
            }

            float opacity = Mathf.Clamp(_settings.GlassOpacity.Value, 0.2f, 1f);
            if (opacity >= 0.995f)
                _theme.Draw(Shape.Panel, area, radius);
            else
                DrawFaded(Shape.Panel, area, radius, opacity);
        }

        // ------------------------------------------------------------------ frosted blur snapshot

        private void BeginBlurCapture()
        {
            if (_blurBroken || _settings.FrostBlur == null || !_settings.FrostBlur.Value)
                return;
            CrashLog.Mark("blur capture start");
            _captureWait = true;
            _captureDeadline = Time.unscaledTime + 0.6f;
            try
            {
                MelonCoroutines.Start(CaptureBlur());
            }
            catch (Exception ex)
            {
                _captureWait = false;
                FailBlur("could not start the capture", ex);
            }
        }

        private System.Collections.IEnumerator CaptureBlur()
        {
            yield return new WaitForEndOfFrame();
            try
            {
                BuildBlurTexture();
            }
            catch (Exception ex)
            {
                FailBlur("capture failed", ex);
            }
            _captureWait = false;
        }

        private void FailBlur(string what, Exception ex)
        {
            _blurBroken = true;
            if (_blurLogged)
                return;
            _blurLogged = true;
            _log?.Warning("Frosted blur was turned off ({0}): {1}", what, ex != null ? ex.Message : string.Empty);
        }

        private void BuildBlurTexture()
        {
            CrashLog.Mark("blur: taking screenshot texture");
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null)
                throw new InvalidOperationException("no screenshot");
            CrashLog.Mark("blur: shrinking " + shot.width + "x" + shot.height);
            int sw = shot.width;
            int sh = shot.height;
            int aw = Mathf.Max(8, sw / 4), ah = Mathf.Max(8, sh / 4);
            int bw = Mathf.Max(8, sw / 16), bh = Mathf.Max(8, sh / 16);

            RenderTexture a = RenderTexture.GetTemporary(aw, ah, 0);
            RenderTexture b = RenderTexture.GetTemporary(bw, bh, 0);
            RenderTexture prev = RenderTexture.active;
            Texture2D small = null;
            try
            {
                a.filterMode = FilterMode.Bilinear;
                b.filterMode = FilterMode.Bilinear;
                Graphics.Blit(shot, a);
                Graphics.Blit(a, b);
                RenderTexture.active = b;
                small = new Texture2D(bw, bh, TextureFormat.RGBA32, false);
                small.ReadPixels(new Rect(0f, 0f, bw, bh), 0, 0);
                small.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(a);
                RenderTexture.ReleaseTemporary(b);
                UnityEngine.Object.Destroy(shot);
            }

            Color32[] px = small.GetPixels32();
            BoxBlur(px, bw, bh, 2);
            BoxBlur(px, bw, bh, 2);
            small.SetPixels32(px);
            small.Apply(false, false);
            small.hideFlags = HideFlags.HideAndDontSave;
            small.filterMode = FilterMode.Bilinear;
            small.wrapMode = TextureWrapMode.Clamp;

            if (_blurTex != null)
                _retiredBlurTextures.Add(new KeyValuePair<Texture2D, int>(_blurTex, Time.frameCount));
            _blurTex = small;
            CrashLog.Mark("blur: ready");
        }

        private void SweepRetiredBlurTextures()
        {
            int frame = Time.frameCount;
            for (int i = _retiredBlurTextures.Count - 1; i >= 0; i--)
            {
                if (frame - _retiredBlurTextures[i].Value < 90)
                    continue;
                Texture2D retired = _retiredBlurTextures[i].Key;
                if (retired != null)
                    UnityEngine.Object.Destroy(retired);
                _retiredBlurTextures.RemoveAt(i);
            }
        }

        private static void BoxBlur(Color32[] px, int w, int h, int radius)
        {
            var tmp = new Color32[px.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, bl = 0, n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int xx = Mathf.Clamp(x + k, 0, w - 1);
                        Color32 c = px[y * w + xx];
                        r += c.r; g += c.g; bl += c.b; n++;
                    }
                    Color32 o = default;
                    o.r = (byte)(r / n); o.g = (byte)(g / n); o.b = (byte)(bl / n); o.a = 255;
                    tmp[y * w + x] = o;
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, bl = 0, n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int yy = Mathf.Clamp(y + k, 0, h - 1);
                        Color32 c = tmp[yy * w + x];
                        r += c.r; g += c.g; bl += c.b; n++;
                    }
                    Color32 o = default;
                    o.r = (byte)(r / n); o.g = (byte)(g / n); o.b = (byte)(bl / n); o.a = 255;
                    px[y * w + x] = o;
                }
            }
        }

        /// <summary>Smoothed 0..1 hover amount for a control (instant when animations are off).</summary>
        private float HoverAmount(Rect rect, int id)
        {
            bool over = _dropId == 0 && !_dragging && rect.Contains(MouseGui());
            return Anim.EaseOutCubic(_anim.Move(6000 + id, over ? 1f : 0f, 0.14f, 0f));
        }

        private void DrawPillBase(Rect rect, float radius, bool active, float hover, float flash)
        {
            _theme.Draw(Shape.Pill, rect, radius);
            if (hover > 0.01f && !active)
                DrawFaded(Shape.PillHover, rect, radius, hover);
            if (active)
                _theme.DrawAccent(Shape.PillActive, rect, radius, 1f);
            else if (flash > 0.01f)
                _theme.DrawAccent(Shape.PillActive, rect, radius, flash * 0.7f);
        }

        private void DrawFaded(Shape shape, Rect rect, float radius, float alpha)
        {
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * Mathf.Clamp01(alpha));
            _theme.Draw(shape, rect, radius);
            GUI.color = prev;
        }

        private void DrawTopBar(Rect rect)
        {
            float icon = 26f * _uiScale;
            if (_paint && GlassTheme.Alive(_theme.Logo))
                GUI.DrawTexture(new Rect(rect.x, rect.y + (rect.height - icon) * 0.5f, icon, icon), _theme.Logo);
            float titleX = rect.x + icon + 8f * _uiScale;
            float titleW = 110f * _uiScale;
            if (_paint)
                Label(new Rect(titleX, rect.y, titleW, rect.height), VividFrameInfo.Title, Style(_theme.Title));

            float hideW = 44f * _uiScale;
            float keyW = 56f * _uiScale;
            float gap = 6f * _uiScale;
            Rect hide = new Rect(rect.xMax - hideW, rect.y + (rect.height - 22f * _uiScale) * 0.5f, hideW, 22f * _uiScale);
            Rect key = new Rect(hide.x - gap - keyW, hide.y, keyW, hide.height);
            string keyName = _settings != null && _settings.MenuKey != null ? _settings.MenuKey.Value : "Home";
            if (Chip(key, keyName, false, 70))
                { }
            if (Chip(hide, "hide", false, 71))
                SetOpen(false);

            HandleTitlePress(new Rect(rect.x, rect.y, key.x - rect.x - 8f, rect.height));
        }

        private void HandleTitlePress(Rect drag)
        {
            if (_dropId != 0)
                return;
            if (Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0 && drag.Contains(MouseGui()))
            {
                _dragging = true;
                _dragOffset = MouseGui() - new Vector2(_area.x, _area.y);
                CloseDropdown();
            }
        }

        private void DrawTabs(Rect rect)
        {
            float gap = 4f * _uiScale;
            float w = (rect.width - gap * (Tabs.Length - 1)) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                _tabRects[i] = new Rect(rect.x + i * (w + gap), rect.y, w, rect.height);

            Rect target = _tabRects[Mathf.Clamp(_tab, 0, _tabRects.Length - 1)];
            float ix = _anim.Spring(IdTabX, target.x, target.x, 18f, 0.85f);
            float iw = _anim.Spring(IdTabW, target.width, target.width, 18f, 0.85f);
            Rect active = new Rect(ix, rect.y, iw, rect.height);
            if (_paint)
            {
                _theme.Draw(Shape.Bar, rect, rect.height * 0.5f);
                for (int i = 0; i < Tabs.Length; i++)
                {
                    if (i == _tab)
                        continue;
                    float hv = HoverAmount(_tabRects[i], 100 + i);
                    if (hv > 0.01f)
                        DrawFaded(Shape.PillHover, _tabRects[i], _tabRects[i].height * 0.5f, hv * 0.8f);
                }
                _theme.DrawAccent(Shape.PillActive, active, active.height * 0.5f, 1f);
            }

            for (int i = 0; i < Tabs.Length; i++)
            {
                Rect r = _tabRects[i];
                if (Hit(r) && _tab != i)
                    SwitchTab(i);
                if (_paint)
                    Label(r, Tabs[i], Style(_tab == i ? _theme.Center : _theme.CenterMuted));
            }
        }

        private bool HasSubTabs(int tab)
        {
            return tab >= 0 && tab < SubTabs.Length && SubTabs[tab].Length > 1;
        }

        private void DrawSubTabs(Rect rect)
        {
            string[] names = SubTabs[_tab];
            if (_paint)
                _theme.Draw(Shape.Bar, rect, rect.height * 0.5f);

            float pad = 3f * _uiScale;
            float inner = rect.width - pad * 2f;
            float cw = inner / names.Length;
            int sel = Mathf.Clamp(_subTab[_tab], 0, names.Length - 1);
            float tx = rect.x + pad + sel * cw;
            float ix = _anim.Spring(IdSubX, tx, tx, 18f, 0.85f);
            float iw = _anim.Spring(IdSubW, cw, cw, 18f, 0.85f);
            Rect seg = new Rect(ix, rect.y + pad, iw, rect.height - pad * 2f);
            if (_paint)
                _theme.DrawAccent(Shape.PillActive, seg, seg.height * 0.5f, 1f);

            for (int i = 0; i < names.Length; i++)
            {
                Rect r = new Rect(rect.x + pad + i * cw, rect.y, cw, rect.height);
                if (Hit(r) && _subTab[_tab] != i)
                {
                    _subTab[_tab] = i;
                    _scroll = 0f;
                    CloseDropdown();
                }
                if (_paint)
                    Label(r, names[i], Style(_theme.Center));
            }
        }

        private void SwitchTab(int next)
        {
            if (next == _tab)
                return;
            CrashLog.Mark("tab " + Tabs[Mathf.Clamp(next, 0, Tabs.Length - 1)]);
            _tabFrom = _tab;
            _tab = next;
            _tabMix = 0f;
            _scroll = 0f;
            _editKey = null;
            CloseDropdown();
            if (next == 5)
                RefreshProfiles();
        }

        private void DrawBody(Rect rect)
        {
            float alpha = 1f;
            float offsetX = 0f;
            if (_anim.Enabled && _tabMix < 0.999f)
            {
                float eased = Anim.EaseOutCubic(_tabMix);
                float dir = _tab >= _tabFrom ? 1f : -1f;
                offsetX = dir * 18f * _uiScale * (1f - eased);
                alpha = eased;
            }

            Color prev = GUI.color;
            if (_paint)
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);

            GUI.BeginClip(rect);
            float y = 2f * _uiScale - _scroll;
            float w = rect.width;
            DrawTab(_tab, offsetX, ref y, w);
            _contentH = y + _scroll + 8f * _uiScale;
            float maxScroll = Mathf.Max(0f, _contentH - rect.height);
            if (_scroll > maxScroll)
                _scroll = maxScroll;
            GUI.EndClip();

            if (_paint)
                GUI.color = prev;
        }

        private void DrawTab(int tab, float x, ref float y, float w)
        {
            switch (tab)
            {
                case 0: DrawVisuals(x, ref y, w); break;
                case 1: DrawColour(x, ref y, w); break;
                case 2: DrawEffects(x, ref y, w); break;
                case 3: DrawCamera(x, ref y, w); break;
                case 4: DrawPresets(x, ref y, w); break;
                case 5: DrawConfigs(x, ref y, w); break;
                case 7: DrawFavourites(x, ref y, w); break;
                case 8: DrawMisc(x, ref y, w); break;
                default: DrawSettings(x, ref y, w); break;
            }
        }

        private void Split(float x, float w, out float x0, out float x1, out float cw)
        {
            float gap = 18f * _uiScale;
            cw = (w - gap) * 0.5f;
            x0 = x;
            x1 = x + cw + gap;
        }

        private void DrawVisuals(float x, ref float y, float w)
        {
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            Section(x0, ref y0, cw, "POST PROCESS");
            Switch(x0, ref y0, cw, "Enabled", _settings.MasterEnabled);
            Hint(x0, ref y0, cw, "Turns the VividFrame volume off and restores the world's look.");
            Switch(x0, ref y0, cw, "Bloom", _settings.Bloom);
            Switch(x0, ref y0, cw, "Motion blur", _settings.MotionBlur);
            Switch(x0, ref y0, cw, "Vignette", _settings.Vignette);
            Switch(x0, ref y0, cw, "Film grain", _settings.Grain);
            Switch(x0, ref y0, cw, "Chromatic aberration", _settings.ChromaticAberration);
            Switch(x0, ref y0, cw, "Ambient occlusion", _settings.AmbientOcclusion);
            Switch(x0, ref y0, cw, "Depth of field", _settings.DepthOfField);
            Switch(x0, ref y0, cw, "Lens distortion", _settings.LensDistortion);

            Section(x1, ref y1, cw, "OUTPUT");
            Slider(x1, ref y1, cw, "Render scale", _settings.RenderScale, 0.5f, 1.5f, "0.00");
            Hint(x1, ref y1, cw, "Does nothing if the game has not enabled dynamic resolution.");
            if (WideButton(x1, ref y1, cw, "Reset visuals", 91))
            {
                _settings.ResetToDefaults();
                _activePreset = null;
                Changed(true);
            }
            y = Mathf.Max(y0, y1);
        }

        private void DrawColour(float x, ref float y, float w)
        {
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            Section(x0, ref y0, cw, "COLOUR");
            Slider(x0, ref y0, cw, "Saturation", _settings.Saturation, -100f, 100f, "0");
            Slider(x0, ref y0, cw, "Vibrance", _settings.Vibrance, -100f, 100f, "0");
            Slider(x0, ref y0, cw, "Contrast", _settings.Contrast, -100f, 100f, "0");
            Slider(x0, ref y0, cw, "Brightness", _settings.Brightness, -2f, 2f, "0.00");
            Slider(x0, ref y0, cw, "Gamma", _settings.Gamma, 0.3f, 2.2f, "0.00");
            Hint(x0, ref y0, cw, "Vibrance is a colour-grading approximation, not a custom shader.");

            Section(x1, ref y1, cw, "WHITE BALANCE");
            Slider(x1, ref y1, cw, "Temperature", _settings.Temperature, -100f, 100f, "0");
            Slider(x1, ref y1, cw, "Tint", _settings.Tint, -100f, 100f, "0");
            Slider(x1, ref y1, cw, "Hue shift", _settings.HueShift, -180f, 180f, "0");
            Slider(x1, ref y1, cw, "Sharpen", _settings.Sharpen, 0f, 100f, "0");
            Dropdown(x1, ref y1, cw, "Tonemapper", TonemapperNames, _settings.Tonemapper, 501);
            Hint(x1, ref y1, cw, "Sharpen is midtone contrast, not an unsharp-mask pass.");
            if (WideButton(x1, ref y1, cw, "Reset colour", 94))
            {
                _settings.ResetColour();
                _activePreset = null;
                Changed(true);
                Toast("Colour reset");
            }
            y = Mathf.Max(y0, y1);
        }

        private void DrawMisc(float x, ref float y, float w)
        {
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            int sub = Mathf.Clamp(_subTab[8], 0, 3);
            if (sub == 0)
                MiscOverlays(x0, ref y0, x1, ref y1, cw);
            else if (sub == 1)
                MiscCamera(x0, ref y0, x1, ref y1, cw);
            else if (sub == 2)
                MiscQuality(x0, ref y0, x1, ref y1, cw);
            else
                MiscSystem(x0, ref y0, x1, ref y1, cw);
            y = Mathf.Max(y0, y1);
        }

        /// <summary>A labelled row of chips bound to an int setting (the chip index is the value).</summary>
        private void ChipSetting(float x, ref float y, float w, string label, string[] names, MelonPreferences_Entry<int> entry, int columns)
        {
            if (!string.IsNullOrEmpty(label))
            {
                Label(new Rect(x, y, w, 20f * _uiScale), label, Style(_theme.Label));
                y += 22f * _uiScale;
            }
            string active = names[Mathf.Clamp(entry.Value, 0, names.Length - 1)];
            ChipRow(x, ref y, w, names, active, picked =>
            {
                entry.Value = Array.IndexOf(names, picked);
                Changed(true);
            }, columns);
        }

        private void MiscOverlays(float x0, ref float y0, float x1, ref float y1, float cw)
        {
            Section(x0, ref y0, cw, "FPS");
            Switch(x0, ref y0, cw, "FPS counter", _settings.FpsOverlay);
            if (_settings.FpsOverlay.Value)
                ChipSetting(x0, ref y0, cw, null, FpsCornerNames, _settings.FpsCorner, 2);

            Section(x0, ref y0, cw, "STATS");
            Switch(x0, ref y0, cw, "Players in instance", _settings.StatPlayers);
            Switch(x0, ref y0, cw, "Clock", _settings.StatClock);
            Switch(x0, ref y0, cw, "Session timer", _settings.StatTimer);
            Switch(x0, ref y0, cw, "Camera position", _settings.StatPosition);
            Switch(x0, ref y0, cw, "Memory use", _settings.StatRam);
            ChipSetting(x0, ref y0, cw, "Stats corner", FpsCornerNames, _settings.StatsCorner, 2);

            Section(x1, ref y1, cw, "CROSSHAIR");
            Switch(x1, ref y1, cw, "Crosshair", _settings.Crosshair);
            if (_settings.Crosshair.Value)
            {
                ChipSetting(x1, ref y1, cw, null, CrosshairNames, _settings.CrosshairStyle, 3);
                Slider(x1, ref y1, cw, "Size", _settings.CrosshairSize, 2f, 40f, "0");
                if (_settings.CrosshairStyle.Value != 0)
                    Slider(x1, ref y1, cw, "Gap", _settings.CrosshairGap, 0f, 20f, "0");
                Slider(x1, ref y1, cw, "Opacity", _settings.CrosshairOpacity, 0.1f, 1f, "0.00");
            }

            Section(x1, ref y1, cw, "CINEMA BARS");
            Switch(x1, ref y1, cw, "Cinema bars", _settings.CinemaBars);
            if (_settings.CinemaBars.Value)
            {
                Slider(x1, ref y1, cw, "Bar size", _settings.CinemaThickness, 0.02f, 0.3f, "0.00");
                Slider(x1, ref y1, cw, "Bar opacity", _settings.CinemaOpacity, 0.2f, 1f, "0.00");
            }
        }

        private void MiscCamera(float x0, ref float y0, float x1, ref float y1, float cw)
        {
            Section(x0, ref y0, cw, "FIELD OF VIEW");
            Switch(x0, ref y0, cw, "Override FOV", _settings.FovOverride);
            if (_settings.FovOverride.Value)
            {
                Slider(x0, ref y0, cw, "Field of view", _settings.Fov, 40f, 130f, "0");
                Switch(x0, ref y0, cw, "Reset FOV in menus", _settings.FovResetInMenus);
            }
            Hint(x0, ref y0, cw, "With 'Reset FOV in menus' on, FOV eases back to normal while the cursor is free so menu buttons line up with the mouse. VR ignores this.");

            Section(x0, ref y0, cw, "ZOOM");
            HotkeyRow(x0, ref y0, cw, "Zoom key", _settings.ZoomKey);
            Slider(x0, ref y0, cw, "Zoom FOV", _settings.ZoomFov, 5f, 90f, "0");
            Hint(x0, ref y0, cw, "Hold the zoom key (Left Alt by default), then scroll the mouse wheel to zoom in or out. It eases smoothly and remembers your zoom level.");

            Section(x1, ref y1, cw, "CLIP PLANES");
            Switch(x1, ref y1, cw, "Override near clip", _settings.NearClipOverride);
            if (_settings.NearClipOverride.Value)
                Slider(x1, ref y1, cw, "Near clip", _settings.NearClip, 0.01f, 2f, "0.00");
            Switch(x1, ref y1, cw, "Override far clip", _settings.FarClipOverride);
            if (_settings.FarClipOverride.Value)
                Slider(x1, ref y1, cw, "Far clip", _settings.FarClip, 50f, 5000f, "0");
            Hint(x1, ref y1, cw, "A smaller near clip stops walls cutting through when you get close. A shorter far clip can help performance.");

            Section(x1, ref y1, cw, "SCREENSHOT");
            HotkeyRow(x1, ref y1, cw, "Screenshot key", _settings.ScreenshotKey);
            ChipSetting(x1, ref y1, cw, "Scale", ScaleNames, _settings.ScreenshotScale, 4, 1);
            if (WideButton(x1, ref y1, cw, "Take screenshot", 97))
            {
                ScreenshotRequested = true;
                Toast("Saving after the menu closes");
            }
            Hint(x1, ref y1, cw, "Saved without the menu or overlays to UserData/VividFrame/screenshots.");
        }

        private void MiscQuality(float x0, ref float y0, float x1, ref float y1, float cw)
        {
            Section(x0, ref y0, cw, "OVERRIDE");
            Switch(x0, ref y0, cw, "Override quality", _settings.QualityOverride);
            Hint(x0, ref y0, cw, "Applies everything on this page. Turning it off restores the game's own values. The game resets some of them, so they are re-applied every few seconds.");
            if (!_settings.QualityOverride.Value)
                return;

            Section(x0, ref y0, cw, "FRAME RATE");
            IntSlider(x0, ref y0, cw, "FPS cap (0 = game)", _settings.FpsCap, 0, 360);
            Switch(x0, ref y0, cw, "VSync", _settings.VSync);

            Section(x0, ref y0, cw, "DETAIL");
            Slider(x0, ref y0, cw, "LOD bias", _settings.LodBias, 0.3f, 4f, "0.0");
            ChipSetting(x0, ref y0, cw, "Texture quality", TextureNames, _settings.TextureLimit, 4);
            Switch(x0, ref y0, cw, "Force anisotropic", _settings.Anisotropic);
            ChipSetting(x0, ref y0, cw, "MSAA", MsaaNames, _settings.MsaaIndex, 4);

            Section(x1, ref y1, cw, "SHADOWS");
            ChipSetting(x1, ref y1, cw, "Shadows", ShadowModeNames, _settings.ShadowMode, 3);
            Slider(x1, ref y1, cw, "Shadow distance", _settings.ShadowDistance, 0f, 150f, "0");
            ChipSetting(x1, ref y1, cw, "Shadow resolution", ShadowResNames, _settings.ShadowRes, 4);
            ChipSetting(x1, ref y1, cw, "Cascades", CascadeNames, _settings.ShadowCascades, 3);

            Section(x1, ref y1, cw, "SCENE");
            IntSlider(x1, ref y1, cw, "Pixel lights", _settings.PixelLights, 0, 8);
            ChipSetting(x1, ref y1, cw, "Skin weights (avatars)", SkinNames, _settings.SkinWeights, 4);
            Switch(x1, ref y1, cw, "Soft particles", _settings.SoftParticles);
            Switch(x1, ref y1, cw, "Real-time reflections", _settings.ReflectionProbes);
        }

        private void MiscSystem(float x0, ref float y0, float x1, ref float y1, float cw)
        {
            Section(x0, ref y0, cw, "WINDOW AND AUDIO");
            Switch(x0, ref y0, cw, "Run in background", _settings.RunInBackground);
            Switch(x0, ref y0, cw, "Mute when unfocused", _settings.MuteUnfocused);
            Switch(x0, ref y0, cw, "Override master volume", _settings.VolumeOverride);
            if (_settings.VolumeOverride.Value)
                Slider(x0, ref y0, cw, "Master volume", _settings.MasterVolume, 0f, 1f, "0.00");

            Section(x0, ref y0, cw, "WORLD LIGHTING");
            Switch(x0, ref y0, cw, "Disable fog", _settings.FogOff);
            Switch(x0, ref y0, cw, "Override ambient light", _settings.AmbientOverride);
            if (_settings.AmbientOverride.Value)
                Slider(x0, ref y0, cw, "Ambient intensity", _settings.AmbientIntensity, 0f, 4f, "0.00");
            Hint(x0, ref y0, cw, "Fog and ambient are reset by each world, so they are re-applied after you join one.");

            Section(x1, ref y1, cw, "LOADING");
            Switch(x1, ref y1, cw, "Override loading priority", _settings.LoadPriorityOverride);
            if (_settings.LoadPriorityOverride.Value)
                ChipSetting(x1, ref y1, cw, null, PriorityNames, _settings.LoadPriority, 4);
            Hint(x1, ref y1, cw, "Higher priority can cut hitching while worlds and avatars stream in.");

            Section(x1, ref y1, cw, "MEMORY");
            if (WideButton(x1, ref y1, cw, "Free unused memory", 98))
                Toast(_misc != null ? _misc.FreeMemory() : "Unavailable");
            Hint(x1, ref y1, cw, "Runs a garbage collection and unloads assets nothing uses. It can cause a short hitch.");

            Section(x1, ref y1, cw, "SYSTEM");
            string info = _misc != null ? _misc.SystemInfoText() : "Unavailable";
            int infoLines = 1;
            for (int i = 0; i < info.Length; i++)
            {
                if (info[i] == '\n')
                    infoLines++;
            }
            float h = (infoLines * 15f + 6f) * _uiScale;
            if (_paint)
                Label(new Rect(x1, y1, cw, h), info, Style(_theme.Hint));
            y1 += h;
        }

        private void ChipSetting(float x, ref float y, float w, string label, string[] names, MelonPreferences_Entry<int> entry, int columns, int offset)
        {
            // Chips whose names start at a value other than 0 (screenshot scale is 1 to 4).
            if (!string.IsNullOrEmpty(label))
            {
                Label(new Rect(x, y, w, 20f * _uiScale), label, Style(_theme.Label));
                y += 22f * _uiScale;
            }
            int index = Mathf.Clamp(entry.Value - offset, 0, names.Length - 1);
            ChipRow(x, ref y, w, names, names[index], picked =>
            {
                entry.Value = Array.IndexOf(names, picked) + offset;
                Changed(true);
            }, columns);
        }

        private void DrawFavourites(float x, ref float y, float w)
        {
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            Section(x0, ref y0, cw, "PINNED");
            Section(x1, ref y1, cw, "PINNED");
            int shown = 0;
            for (int i = 0; i < _favOrder.Count; i++)
            {
                string key = _favOrder[i];
                if (!_favSet.Contains(key))
                    continue;
                RowDrawer draw = _favDrawers[key];
                if (shown % 2 == 0)
                    draw(x0, ref y0, cw);
                else
                    draw(x1, ref y1, cw);
                shown++;
            }
            if (shown == 0)
            {
                Hint(x0, ref y0, cw, "Nothing pinned yet. Shift-click any slider or switch to pin it here.");
            }
            else
            {
                float yh = Mathf.Max(y0, y1);
                Hint(x0, ref yh, w, "Shift-click a pinned control to remove it. Double-click a slider to reset it. Right-click to type a value.");
                y0 = yh;
            }
            y = Mathf.Max(y0, y1);
        }

        private void DrawEffects(float x, ref float y, float w)
        {
            int sub = Mathf.Clamp(_subTab[2], 0, 3);
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            if (sub == 0)
            {
                Section(x0, ref y0, cw, "BLOOM");
                Switch(x0, ref y0, cw, "Enable bloom", _settings.Bloom);
                if (_settings.Bloom.Value)
                {
                    Slider(x0, ref y0, cw, "Intensity", _settings.BloomIntensity, 0f, 5f, "0.00");
                    Slider(x0, ref y0, cw, "Threshold", _settings.BloomThreshold, 0f, 3f, "0.00");
                }
                Section(x1, ref y1, cw, "SHAPE");
                if (_settings.Bloom.Value)
                {
                    Slider(x1, ref y1, cw, "Soft knee", _settings.BloomSoftKnee, 0f, 1f, "0.00");
                    Slider(x1, ref y1, cw, "Diffusion", _settings.BloomDiffusion, 1f, 10f, "0.0");
                }
                else
                    Hint(x1, ref y1, cw, "Enable bloom to edit its shape.");
            }
            else if (sub == 1)
            {
                Section(x0, ref y0, cw, "MOTION BLUR");
                Switch(x0, ref y0, cw, "Enable motion blur", _settings.MotionBlur);
                if (_settings.MotionBlur.Value)
                {
                    Slider(x0, ref y0, cw, "Shutter angle", _settings.MotionBlurShutter, 0f, 360f, "0");
                    IntSlider(x0, ref y0, cw, "Samples", _settings.MotionBlurSamples, 4, 32);
                }
                Section(x1, ref y1, cw, "HINT");
                Hint(x1, ref y1, cw, "Lower shutter angles smear faces less. The default is 90.");
            }
            else if (sub == 2)
            {
                Section(x0, ref y0, cw, "LENS");
                Switch(x0, ref y0, cw, "Vignette", _settings.Vignette);
                if (_settings.Vignette.Value)
                {
                    Slider(x0, ref y0, cw, "Intensity", _settings.VignetteIntensity, 0f, 1f, "0.00");
                    Slider(x0, ref y0, cw, "Smoothness", _settings.VignetteSmoothness, 0.05f, 1f, "0.00");
                    Slider(x0, ref y0, cw, "Roundness", _settings.VignetteRoundness, 0f, 1f, "0.00");
                }
                Switch(x0, ref y0, cw, "Film grain", _settings.Grain);
                if (_settings.Grain.Value)
                {
                    Slider(x0, ref y0, cw, "Intensity", _settings.GrainIntensity, 0f, 1f, "0.00");
                    Slider(x0, ref y0, cw, "Size", _settings.GrainSize, 0.3f, 3f, "0.00");
                    Switch(x0, ref y0, cw, "Colored grain", _settings.GrainColored);
                }
                Section(x1, ref y1, cw, "OPTICS");
                Switch(x1, ref y1, cw, "Chromatic aberration", _settings.ChromaticAberration);
                if (_settings.ChromaticAberration.Value)
                    Slider(x1, ref y1, cw, "Intensity", _settings.ChromaticAberrationIntensity, 0f, 1f, "0.00");
                Switch(x1, ref y1, cw, "Lens distortion", _settings.LensDistortion);
                if (_settings.LensDistortion.Value)
                    Slider(x1, ref y1, cw, "Intensity", _settings.LensDistortionIntensity, -100f, 100f, "0");
            }
            else
            {
                Section(x0, ref y0, cw, "AMBIENT OCCLUSION");
                Switch(x0, ref y0, cw, "Ambient occlusion", _settings.AmbientOcclusion);
                if (_settings.AmbientOcclusion.Value)
                {
                    Slider(x0, ref y0, cw, "Intensity", _settings.AoIntensity, 0f, 4f, "0.00");
                    Slider(x0, ref y0, cw, "Radius", _settings.AoRadius, 0.05f, 2f, "0.00");
                }
                Section(x1, ref y1, cw, "MODE");
                if (_settings.AmbientOcclusion.Value)
                    Dropdown(x1, ref y1, cw, "AO mode", AoModeNames, _settings.AoMode, 502);
                else
                    Hint(x1, ref y1, cw, "Enable ambient occlusion to choose a mode.");
            }
            y = Mathf.Max(y0, y1);
        }

        private void DrawCamera(float x, ref float y, float w)
        {
            int sub = Mathf.Clamp(_subTab[3], 0, 1);
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            if (sub == 0)
            {
                Section(x0, ref y0, cw, "DEPTH OF FIELD");
                Switch(x0, ref y0, cw, "Depth of field", _settings.DepthOfField);
                if (_settings.DepthOfField.Value)
                {
                    Dropdown(x0, ref y0, cw, "Focus", FocusNames, _settings.FocusMode, 503);
                    var mode = (FocusMode)_settings.FocusMode.Value;
                    if (mode == FocusMode.Manual)
                        Slider(x0, ref y0, cw, "Focus distance", _settings.DofFocusDistance, 0.1f, 50f, "0.0");
                    else
                        Hint(x0, ref y0, cw, _autoFocus != null ? _autoFocus.Status : "Auto-focus");
                    Slider(x0, ref y0, cw, "Aperture", _settings.DofAperture, 0.1f, 32f, "0.0");
                }
                Section(x1, ref y1, cw, "CAMERA / FOCUS");
                if (_settings.DepthOfField.Value)
                {
                    Slider(x1, ref y1, cw, "Focal length", _settings.DofFocalLength, 1f, 300f, "0");
                    Dropdown(x1, ref y1, cw, "Kernel", KernelNames, _settings.DofKernelSize, 504);
                    Hint(x1, ref y1, cw, "Auto focuses on avatar faces. Aperture is clamped for a wider sharp zone.");
                }
                else
                    Hint(x1, ref y1, cw, "Enable depth of field to edit focus.");
            }
            else
            {
                Section(x0, ref y0, cw, "OUTPUT");
                Slider(x0, ref y0, cw, "Render scale", _settings.RenderScale, 0.5f, 1.5f, "0.00");
                Hint(x0, ref y0, cw, "FOV-free buffer scale. Does nothing if dynamic resolution is off.");
                Section(x1, ref y1, cw, "MOTION BLUR");
                Switch(x1, ref y1, cw, "Enable motion blur", _settings.MotionBlur);
                if (_settings.MotionBlur.Value)
                {
                    Slider(x1, ref y1, cw, "Shutter angle", _settings.MotionBlurShutter, 0f, 360f, "0");
                    IntSlider(x1, ref y1, cw, "Samples", _settings.MotionBlurSamples, 4, 32);
                }
            }
            y = Mathf.Max(y0, y1);
        }

        private void DrawPresets(float x, ref float y, float w)
        {
            Section(x, ref y, w, "LOOKS");
            Hint(x, ref y, w, "Natural+ is the everyday starting point. Vivid stays neutral, without an orange cast.");
            ChipRow(x, ref y, w, PresetNames, _activePreset, ApplyNamedPreset, 3);
            if (WideButton(x, ref y, w, "Reset visuals", 92))
            {
                _settings.ResetToDefaults();
                _activePreset = null;
                Changed(true);
            }
        }

        private void DrawConfigs(float x, ref float y, float w)
        {
            Section(x, ref y, w, "PROFILES");
            Hint(x, ref y, w, "Saved as JSON in UserData/VividFrame/profiles. Copy a file to export or import it.");

            float row = 26f * _uiScale;
            if (!_textFieldMissing)
            {
                if (_paint)
                    Label(new Rect(x, y, 90f * _uiScale, row), "Name", Style(_theme.Label));
                DrawTextField(new Rect(x + 96f * _uiScale, y, w - 96f * _uiScale, row + 4f * _uiScale));
                y += row + GlassTheme.RowGap * _uiScale;
            }
            else
            {
                Hint(x, ref y, w, "Text fields are unavailable. Use the Profile 1-3 slots.");
                ChipRow(x, ref y, w, ProfileSlots, _profileName, slot => { _profileName = slot; _selectedProfile = slot; }, 3);
            }

            float gap = 8f * _uiScale;
            float aw = (w - gap * 3f) / 4f;
            float bh = 28f * _uiScale;
            if (Chip(new Rect(x, y, aw, bh), "Save as", false, 80))
                SaveProfile(false);
            if (Chip(new Rect(x + aw + gap, y, aw, bh), "Load", false, 81))
                LoadSelected();
            if (Chip(new Rect(x + (aw + gap) * 2f, y, aw, bh), "Overwrite", false, 82))
                SaveProfile(true);
            if (Chip(new Rect(x + (aw + gap) * 3f, y, aw, bh), "Delete", false, 83))
                DeleteSelected();
            y += bh + GlassTheme.RowGap * _uiScale;
            if (WideButton(x, ref y, w, "Save", 84))
            {
                try
                {
                    _settings.Save();
                    _dirty = false;
                    if (string.IsNullOrEmpty(_lastConfigName))
                        _status = "Saved preferences";
                    else
                        _status = "Saved config '" + _lastConfigName + "'";
                    Toast("Saved  ✓");
                }
                catch (Exception ex)
                {
                    _log?.Error("Could not save preferences: {0}", ex);
                    Toast("Save failed");
                }
            }

            if (_profileList.Length == 0)
                Hint(x, ref y, w, "No saved profiles yet.");
            else
            {
                Hint(x, ref y, w, "Click a profile, then Load, Overwrite or Delete.");
                ChipRow(x, ref y, w, _profileList, _selectedProfile, picked =>
                {
                    _selectedProfile = picked;
                    _profileName = picked;
                }, 3);
            }
        }

        private void DrawSettings(float x, ref float y, float w)
        {
            Split(x, w, out float x0, out float x1, out float cw);
            float y0 = y, y1 = y;
            Section(x0, ref y0, cw, "MENU");
            Hint(x0, ref y0, cw, "Current key: " + (_settings.MenuKey != null ? _settings.MenuKey.Value : "Home"));
            ChipRow(x0, ref y0, cw, MenuKeys, _settings.MenuKey.Value, picked =>
            {
                _settings.MenuKey.Value = picked;
                _captureKey = false;
                Toast("Menu key  " + picked);
            }, 4);
            if (WideButton(x0, ref y0, cw, _captureKey ? "Press a key..." : "Press key to bind", 93))
                _captureKey = !_captureKey;

            Section(x1, ref y1, cw, "INTERFACE");
            Switch(x1, ref y1, cw, "Animations", _settings.AnimationsEnabled);
            if (_settings.AnimationsEnabled.Value && !_lowCost)
                Slider(x1, ref y1, cw, "Animation speed", _settings.AnimationSpeed, 0.5f, 2f, "0.00");
            Switch(x1, ref y1, cw, "Low-cost UI", _settings.LowCostUi);
            Slider(x1, ref y1, cw, "UI scale", _settings.UiScale, 0.75f, 2f, "0.00");
            Hint(x1, ref y1, cw, "Low-cost UI turns off shadows and animations. Change scale if the menu is too large or small.");

            Section(x1, ref y1, cw, "APPEARANCE");
            Slider(x1, ref y1, cw, "Glass opacity", _settings.GlassOpacity, 0.2f, 1f, "0.00");
            Label(new Rect(x1, y1, cw, 20f * _uiScale), "Accent colour", Style(_theme.Label));
            y1 += 22f * _uiScale;
            ChipRow(x1, ref y1, cw, AccentNames, AccentNames[Mathf.Clamp(_settings.AccentIndex.Value, 0, AccentNames.Length - 1)], picked =>
            {
                for (int i = 0; i < AccentNames.Length; i++)
                {
                    if (AccentNames[i] == picked)
                        _settings.AccentIndex.Value = i;
                }
                _dirty = true;
            }, 5);
            Switch(x1, ref y1, cw, "Frosted blur", _settings.FrostBlur);
            Hint(x1, ref y1, cw, _blurBroken
                ? "Blur is unavailable on this build and was turned off."
                : "Blurs a snapshot of the screen taken when the menu opens. Takes effect next time you open it.");

            Section(x0, ref y0, cw, "HOTKEYS");
            HotkeyRow(x0, ref y0, cw, "Hold to compare", _settings.CompareKey);
            HotkeyRow(x0, ref y0, cw, "Toggle VividFrame", _settings.MasterKey);
            HotkeyRow(x0, ref y0, cw, "Next profile", _settings.ProfileKey);
            Hint(x0, ref y0, cw, "Hotkeys work while the menu is closed.");

            Section(x1, ref y1, cw, "WORLD PROFILES");
            Switch(x1, ref y1, cw, "Auto-load per world", _settings.AutoProfiles);
            if (_worlds != null && _worlds.Available)
            {
                _worlds.Refresh();
                string world = string.IsNullOrEmpty(_worlds.CurrentName) ? "Not in a world" : _worlds.CurrentName;
                string pinned = _worlds.PinnedForCurrent();
                Hint(x1, ref y1, cw, world + (string.IsNullOrEmpty(pinned) ? "  -  no profile pinned" : "  -  pinned: " + pinned));
                float gap = 8f * _uiScale;
                float half = (cw - gap) * 0.5f;
                float bh = 28f * _uiScale;
                if (Chip(new Rect(x1, y1, half, bh), "Pin selected", false, 95))
                {
                    string pick = _selectedProfile ?? _profileName;
                    if (string.IsNullOrEmpty(pick))
                        Toast("Select a profile in Configs");
                    else
                        Toast(_worlds.Pin(pick) ? "Pinned  " + pick : "Join a world first");
                }
                if (Chip(new Rect(x1 + half + gap, y1, half, bh), "Unpin", false, 96))
                    Toast(_worlds.Unpin() ? "Unpinned" : "Nothing pinned");
                y1 += bh + GlassTheme.RowGap * _uiScale;
            }
            else
            {
                Hint(x1, ref y1, cw, "World detection is unavailable on this game version.");
            }

            Section(x1, ref y1, cw, "UPDATES");
            Switch(x1, ref y1, cw, "Auto update", _settings.AutoUpdate);
            Hint(x1, ref y1, cw, Updater.Status);
            if (WideButton(x1, ref y1, cw, Updater.Busy ? "Checking..." : "Check for updates", 99) && !Updater.Busy)
                Updater.CheckNow(true);
            Hint(x1, ref y1, cw, "Releases come from github.com/" + Updater.Owner + "/" + Updater.Repo + " and are checked against a published SHA-256 before use.");

            Section(x1, ref y1, cw, "DIAGNOSTICS");
            string crashText = CrashLog.ReportCount == 0
                ? "No crash reports. If the game dies, VividFrame saves what it was doing last to UserData/VividFrame/crash."
                : CrashLog.ReportCount + " crash report(s) in UserData/VividFrame/crash" + (string.IsNullOrEmpty(CrashLog.LastReport) ? "." : ". Newest this session: " + CrashLog.LastReport);
            Hint(x1, ref y1, cw, crashText);

            Section(x0, ref y0, cw, "CREDITS");
            float creditH = 24f * _uiScale;
            if (_paint)
            {
                Label(new Rect(x0, y0, cw, creditH), "Made by mcperr", Style(_theme.Label));
            }
            y0 += creditH;
            Hint(x0, ref y0, cw, "VividFrame v" + VividFrameInfo.Version);
            y = Mathf.Max(y0, y1);
        }

        private void HotkeyRow(float x, ref float y, float w, string name, MelonPreferences_Entry<string> entry)
        {
            float h = 26f * _uiScale;
            if (_paint)
                Label(new Rect(x, y, w * 0.55f, h), name, Style(_theme.Label));
            Rect field = new Rect(x + w * 0.55f, y, w * 0.45f, h);
            int index = Array.IndexOf(HotKeyChoices, entry.Value);
            if (index < 0)
                index = 0;
            string label = HotKeyChoices[index];
            int id = 700 + (name.GetHashCode() & 0xFF);
            if (_paint)
            {
                DrawPillBase(field, 10f * _uiScale, _dropId == id, HoverAmount(field, id), 0f);
                Label(new Rect(field.x + 12f * _uiScale, field.y, field.width - 34f * _uiScale, field.height), label, Style(_theme.Label));
                if (GlassTheme.Alive(_theme.Chevron))
                {
                    float c = 14f * _uiScale;
                    GUI.DrawTexture(new Rect(field.xMax - c - 8f * _uiScale, field.y + (field.height - c) * 0.5f, c, c), _theme.Chevron);
                }
            }
            Rect header = ToUnclipped(field);
            if (_dropId == id)
            {
                _dropHeader = header;
                _dropOptions = HotKeyChoices;
                _dropIndex = index;
            }
            if (HeaderClicked(field))
            {
                Event ev = Event.current;
                if (ev != null)
                    ev.Use();
                if (_dropId == id)
                    CloseDropdown();
                else
                    OpenDropdown(id, HotKeyChoices, index, header, picked =>
                    {
                        entry.Value = HotKeyChoices[picked];
                        _dirty = true;
                    });
            }
            y += h + GlassTheme.RowGap * _uiScale;
        }

        private void DrawFooter(Rect rect)
        {
            if (_paint)
                _theme.Draw(Shape.Bar, rect, rect.height * 0.5f);

            string status = !string.IsNullOrEmpty(_status)
                ? _status
                : (string.IsNullOrEmpty(_lastConfigName) ? "No config saved this session" : "Saved config '" + _lastConfigName + "'");
            float textX = rect.x + 8f * _uiScale;
            if (_paint && _dirty)
            {
                float d = 7f * _uiScale;
                _theme.DrawKnob(new Rect(rect.x + 10f * _uiScale, rect.y + (rect.height - d) * 0.5f, d, d));
                textX += 14f * _uiScale;
                status += "  (unsaved, saves on close)";
            }
            if (_paint)
                Label(new Rect(textX, rect.y, rect.width - 80f * _uiScale - (textX - rect.x), rect.height), status, Style(_theme.Status));

            float cw = 64f * _uiScale;
            float ch = 20f * _uiScale;
            Rect close = new Rect(rect.xMax - cw - 6f * _uiScale, rect.y + (rect.height - ch) * 0.5f, cw, ch);
            if (Chip(close, "close", false, 72))
                SetOpen(false);
        }

        private void DrawToast(Rect footer)
        {
            if (string.IsNullOrEmpty(_toast) && _toastAge > ToastSeconds)
                return;
            float inT = Anim.EaseOutCubic(Mathf.Clamp01(_toastAge / 0.18f));
            float outT = _toastAge > ToastSeconds - 0.35f
                ? 1f - Anim.EaseOutCubic(Mathf.Clamp01((_toastAge - (ToastSeconds - 0.35f)) / 0.35f))
                : 1f;
            float alpha = Mathf.Min(inT, outT);
            if (alpha <= 0.01f)
                return;
            float h = 26f * _uiScale;
            float tw = 180f * _uiScale;
            float slide = (1f - inT) * 8f * _uiScale;
            Rect toast = new Rect(footer.xMax - tw - 72f * _uiScale, footer.y - h - 6f * _uiScale + slide, tw, h);
            if (_paint)
            {
                Color prev = GUI.color;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
                _theme.DrawAccent(Shape.PillActive, toast, toast.height * 0.5f, 1f);
                Label(toast, _toast, Style(_theme.Label));
                GUI.color = prev;
            }
        }

        private void Section(float x, ref float y, float w, string title)
        {
            y += 4f * _uiScale;
            float h = 16f * _uiScale;
            if (_paint)
            {
                Label(new Rect(x, y, w, h), title, Style(_theme.Header));
                _theme.FillRect(new Rect(x, y + h + 1f, w, 1f), new Color(1f, 1f, 1f, 0.14f));
            }
            y += h + 8f * _uiScale;
        }

        private void Hint(float x, ref float y, float w, string text)
        {
            float h = 28f * _uiScale;
            if (_paint)
                Label(new Rect(x, y, w, h), text, Style(_theme.Hint));
            y += h + 2f * _uiScale;
        }

        private void DrawTooltip()
        {
            if (!_paint)
                return;
            if (_tipFrameKey != _tipHoverKey)
            {
                _tipHoverKey = _tipFrameKey;
                _tipSince = Time.unscaledTime;
            }
            if (_tipHoverKey == null || _dragging || Time.unscaledTime - _tipSince < 0.55f)
                return;
            if (!Tips.TryGetValue(_tipHoverKey, out string text))
                return;

            float s = _uiScale;
            float width = 230f * s;
            float charsPerLine = Mathf.Max(8f, (width - 20f * s) / (6.1f * s));
            int lines = Mathf.CeilToInt(text.Length / charsPerLine);
            float height = lines * 15f * s + 14f * s;
            Vector2 mouse = MouseGui();
            float x = Mathf.Clamp(mouse.x + 14f * s, _panelArea.x + 6f * s, _panelArea.xMax - width - 6f * s);
            float y = _tipRect.yMax + 4f * s;
            if (y + height > _panelArea.yMax - 6f * s)
                y = _tipRect.y - height - 4f * s;
            Rect box = new Rect(x, y, width, height);
            _theme.Draw(Shape.Popup, box, 10f * s);
            Label(new Rect(box.x + 10f * s, box.y + 4f * s, box.width - 20f * s, box.height - 8f * s), text, Style(_theme.Hint));
        }

        private bool PinClick(Rect row, string key)
        {
            Event ev = Event.current;
            if (ev == null || _dropId != 0 || ev.type != EventType.MouseDown || ev.button != 0 || !ev.shift)
                return false;
            if (!row.Contains(MouseGui()))
                return false;
            ToggleFavourite(key);
            ev.Use();
            return true;
        }

        private void Tip(Rect row, string key)
        {
            if (!_paint || _dropId != 0 || !Tips.ContainsKey(key))
                return;
            if (!row.Contains(MouseGui()))
                return;
            _tipFrameKey = key;
            _tipRect = ToUnclipped(row);
        }

        private bool RightClicked(Rect row)
        {
            Event ev = Event.current;
            if (ev == null || _dropId != 0 || ev.type != EventType.MouseDown || ev.button != 1)
                return false;
            if (!row.Contains(MouseGui()))
                return false;
            ev.Use();
            return true;
        }

        private void StartEdit(string key, float value)
        {
            _editKey = key;
            _editText = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            _editFocusPending = true;
        }

        /// <summary>Draws the inline value editor. Returns true when Enter accepted a number.</summary>
        private bool DrawEditField(Rect field, out float parsed)
        {
            parsed = 0f;
            Event ev = Event.current;
            _editRect = ToUnclipped(field);
            if (ev != null && ev.type == EventType.KeyDown)
            {
                if (ev.keyCode == KeyCode.Escape)
                {
                    _editKey = null;
                    ev.Use();
                    return false;
                }
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                {
                    bool ok = float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                              || float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out parsed);
                    _editKey = null;
                    ev.Use();
                    return ok;
                }
            }
            if (_paint)
                _theme.Draw(Shape.Field, field, 8f * _uiScale);
            if (_textFieldMissing)
                return false;
            try
            {
                if (!_focusApiMissing)
                {
                    try { GUI.SetNextControlName("vf_edit"); }
                    catch (Exception) { _focusApiMissing = true; }
                }
                _editText = GUI.TextField(field, _editText ?? Empty, 12, Style(_theme.Field));
                if (_editFocusPending && !_focusApiMissing)
                {
                    _editFocusPending = false;
                    try { GUI.FocusControl("vf_edit"); }
                    catch (Exception) { _focusApiMissing = true; }
                }
            }
            catch (MissingMethodException)
            {
                _textFieldMissing = true;
                _editKey = null;
            }
            return false;
        }

        private void Slider(float x, ref float y, float w, string name, MelonPreferences_Entry<float> entry, float min, float max, string format)
        {
            string key = entry.Identifier;
            int id = 3000 + name.GetHashCode();
            float labelH = 16f * _uiScale;
            float total = GlassTheme.SliderRow * _uiScale;
            Rect row = new Rect(x, y, w, total);
            bool editing = _editKey == key;
            if (!PinClick(new Rect(x, y, w, labelH + 4f * _uiScale), key) && RightClicked(row))
                StartEdit(key, entry.Value);
            Tip(row, key);
            if (_paint)
            {
                Label(new Rect(x, y, w - 56f * _uiScale, labelH), name, Style(_theme.Label));
                if (!editing)
                    Label(new Rect(x + w - 56f * _uiScale, y, 56f * _uiScale, labelH), FormatCached(id, entry.Value, format), Style(_theme.Value));
            }
            if (editing && DrawEditField(new Rect(x + w - 74f * _uiScale, y - 2f * _uiScale, 74f * _uiScale, labelH + 4f * _uiScale), out float typed))
            {
                entry.Value = Mathf.Clamp(typed, min, max);
                _fmtValue.Remove(id);
                _anim.Set(id, entry.Value);
                _activePreset = null;
                Changed(false);
            }
            float shown = DrawSlider(new Rect(x, y + labelH + 2f * _uiScale, w, 10f * _uiScale), entry.Value, min, max, id, entry.DefaultValue);
            y += total + 2f * _uiScale;
            if (_sliderDirty)
            {
                entry.Value = _sliderLive;
                _fmtValue.Remove(id);
                _activePreset = null;
                Changed(false);
                _sliderDirty = false;
            }
            else if (!_anim.Enabled && Math.Abs(shown - entry.Value) > 0.0001f)
            {
                entry.Value = shown;
                _fmtValue.Remove(id);
                _activePreset = null;
                Changed(false);
            }
        }

        private void IntSlider(float x, ref float y, float w, string name, MelonPreferences_Entry<int> entry, int min, int max)
        {
            string key = entry.Identifier;
            int id = 3000 + (name.GetHashCode() ^ 17);
            float labelH = 16f * _uiScale;
            float total = GlassTheme.SliderRow * _uiScale;
            Rect row = new Rect(x, y, w, total);
            bool editing = _editKey == key;
            if (RightClicked(row))
                StartEdit(key, entry.Value);
            if (_paint)
            {
                Label(new Rect(x, y, w - 56f * _uiScale, labelH), name, Style(_theme.Label));
                if (!editing)
                    Label(new Rect(x + w - 56f * _uiScale, y, 56f * _uiScale, labelH), FormatCached(id, entry.Value, "0"), Style(_theme.Value));
            }
            if (editing && DrawEditField(new Rect(x + w - 74f * _uiScale, y - 2f * _uiScale, 74f * _uiScale, labelH + 4f * _uiScale), out float typed))
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(typed), min, max);
                _fmtValue.Remove(id);
                _anim.Set(id, entry.Value);
                Changed(false);
            }
            DrawSlider(new Rect(x, y + labelH + 2f * _uiScale, w, 10f * _uiScale), entry.Value, min, max, id, entry.DefaultValue);
            y += total + 2f * _uiScale;
            if (_sliderDirty)
            {
                entry.Value = Mathf.RoundToInt(_sliderLive);
                _fmtValue.Remove(id);
                _activePreset = null;
                Changed(false);
                _sliderDirty = false;
            }
        }

        private bool _sliderDirty;
        private float _sliderLive;

        private float DrawSlider(Rect track, float value, float min, float max, int id, float def)
        {
            Rect fat = new Rect(track.x, track.y - 6f * _uiScale, track.width, track.height + 12f * _uiScale);
            float next = value;
            Event ev = Event.current;
            Vector2 mouse = MouseGui();
            int cid = ControlId(id, fat);
            bool hot = IsHot(cid, id);
            if (ev != null && _dropId == 0)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && fat.Contains(mouse))
                {
                    if (ev.clickCount >= 2)
                    {
                        // Double-click resets to the default value.
                        next = Mathf.Clamp(def, min, max);
                    }
                    else
                    {
                        Grab(cid, id);
                        next = SliderValue(fat, mouse.x, min, max);
                    }
                    ev.Use();
                }
                else if (hot && (ev.type == EventType.MouseDrag || MouseHeld()))
                {
                    next = SliderValue(fat, mouse.x, min, max);
                    if (ev.type == EventType.MouseDrag)
                        ev.Use();
                }
                if (hot && ev.type == EventType.MouseUp)
                {
                    Release(cid, id);
                    ev.Use();
                }
            }

            if (Math.Abs(next - value) > 0.0001f)
            {
                _sliderLive = next;
                _sliderDirty = true;
                _anim.Set(id, next);
            }

            if (_paint)
            {
                float trackH = 4f * _uiScale;
                Rect thin = new Rect(track.x, track.y + (track.height - trackH) * 0.5f, track.width, trackH);
                _theme.Draw(Shape.Track, thin, trackH * 0.5f);
                float t = max <= min ? 0f : Mathf.InverseLerp(min, max, next);
                float thumbBase = 14f * _uiScale;
                float grow = Anim.EaseOutCubic(_anim.Move(7000 + id, (hot || fat.Contains(mouse)) && _dropId == 0 ? 1f : 0f, 0.12f, 0f));
                float thumb = thumbBase * (1f + 0.14f * grow);
                float tx = thin.x + (thin.width - thumbBase) * t + (thumbBase - thumb) * 0.5f;
                float centreX = tx + thumb * 0.5f;
                float fillW = centreX - thin.x;
                if (fillW > 1f)
                    _theme.DrawAccent(Shape.Fill, new Rect(thin.x, thin.y, fillW, thin.height), trackH * 0.5f, 1f);
                float ty = thin.y + (thin.height - thumb) * 0.5f;
                _theme.DrawKnob(new Rect(tx, ty, thumb, thumb));
            }
            return next;
        }

        private static float SliderValue(Rect fat, float mouseX, float min, float max)
        {
            float t = Mathf.Clamp01((mouseX - fat.x) / Mathf.Max(1f, fat.width));
            return Mathf.Lerp(min, max, t);
        }

        private bool _hotControlOk = true;

        private int ControlId(int id, Rect rect)
        {
            if (!_hotControlOk)
                return id;
            try
            {
                return GUIUtility.GetControlID(id, FocusType.Passive, rect);
            }
            catch
            {
                _hotControlOk = false;
                return id;
            }
        }

        private bool IsHot(int cid, int id)
        {
            if (_hotControlOk)
            {
                try { return GUIUtility.hotControl == cid; }
                catch { _hotControlOk = false; }
            }
            return _activeSlider == id;
        }

        private void Grab(int cid, int id)
        {
            if (_hotControlOk)
            {
                try { GUIUtility.hotControl = cid; return; }
                catch { _hotControlOk = false; }
            }
            _activeSlider = id;
        }

        private void Release(int cid, int id)
        {
            if (_hotControlOk)
            {
                try
                {
                    if (GUIUtility.hotControl == cid)
                        GUIUtility.hotControl = 0;
                    return;
                }
                catch { _hotControlOk = false; }
            }
            if (_activeSlider == id)
                _activeSlider = 0;
        }

        private void Switch(float x, ref float y, float w, string name, MelonPreferences_Entry<bool> entry)
        {
            float h = GlassTheme.SwitchRow * _uiScale;
            Label(new Rect(x, y, w - 56f * _uiScale, h), name, Style(_theme.Label));
            string key = entry.Identifier;
            Rect rowRect = new Rect(x, y, w, h);
            Tip(rowRect, key);
            bool pinned = PinClick(rowRect, key);
            int id = 2000 + name.GetHashCode();
            bool on = entry.Value;
            float pillW = 36f * _uiScale;
            float pillH = 20f * _uiScale;
            Rect pill = new Rect(x + w - pillW, y + (h - pillH) * 0.5f, pillW, pillH);
            if (_paint)
            {
                float t = Anim.EaseOutCubic(_anim.Move(id + 3, on ? 1f : 0f, 0.18f, on ? 1f : 0f));
                _theme.Draw(Shape.SwitchOff, pill, pillH * 0.5f);
                if (t > 0.01f)
                    _theme.DrawAccent(Shape.SwitchOn, pill, pillH * 0.5f, t);
                float knob = pillH - 6f * _uiScale;
                float pad = 3f * _uiScale;
                float kx = Mathf.Lerp(pill.x + pad, pill.xMax - knob - pad, t);
                _theme.DrawKnob(new Rect(kx, pill.y + (pill.height - knob) * 0.5f, knob, knob));
            }
            if (!pinned && Hit(rowRect))
            {
                entry.Value = !on;
                if (!on)
                    _anim.Fire(id + 7);
                _activePreset = null;
                Changed(true);
            }
            y += h + GlassTheme.RowGap * _uiScale;
        }

        private void Dropdown(float x, ref float y, float w, string name, string[] options, MelonPreferences_Entry<int> entry, int id)
        {
            float h = 26f * _uiScale;
            if (_paint)
                Label(new Rect(x, y, 110f * _uiScale, h), name, Style(_theme.Label));
            Rect field = new Rect(x + 118f * _uiScale, y, Mathf.Max(80f, w - 118f * _uiScale), h);
            int index = Mathf.Clamp(entry.Value, 0, options.Length - 1);
            string label = options[index];
            if (_paint)
            {
                bool open = _dropId == id;
                DrawPillBase(field, 10f * _uiScale, open, HoverAmount(field, id), 0f);
                Label(new Rect(field.x + 12f * _uiScale, field.y, field.width - 34f * _uiScale, field.height), label, Style(_theme.Label));
                if (GlassTheme.Alive(_theme.Chevron))
                {
                    float c = 14f * _uiScale;
                    GUI.DrawTexture(new Rect(field.xMax - c - 8f * _uiScale, field.y + (field.height - c) * 0.5f, c, c), _theme.Chevron);
                }
            }

            Rect header = ToUnclipped(field);
            if (_dropId == id)
            {
                _dropHeader = header;
                _dropOptions = options;
                _dropIndex = index;
            }

            if (HeaderClicked(field))
            {
                Event ev = Event.current;
                if (ev != null)
                    ev.Use();
                if (_dropId == id)
                    CloseDropdown();
                else
                    OpenDropdown(id, options, index, header, picked =>
                    {
                        entry.Value = picked;
                        _activePreset = null;
                        Changed(true);
                    });
            }
            y += h + GlassTheme.RowGap * _uiScale;
        }

        private void OpenDropdown(int id, string[] options, int index, Rect header, Action<int> pick)
        {
            _dropId = id;
            _dropOptions = options;
            _dropIndex = index;
            _dropHeader = header;
            _dropPick = pick;
            _dropJustOpened = true;
            _anim.Set(IdDrop, 0f);
        }

        private void DrawDropdownOverlay()
        {
            if (_dropId == 0 || _dropOptions == null || _dropOptions.Length == 0)
                return;

            Event ev = Event.current;
            Vector2 mouse = MouseGui();
            Rect header = _dropHeader;
            float rowH = 26f * _uiScale;
            float pad = 4f * _uiScale;
            float fullH = pad * 2f + _dropOptions.Length * rowH;
            float openT = _anim.Enabled ? Anim.EaseOutCubic(Mathf.Clamp01(_anim.Get(IdDrop, 0f))) : 1f;
            float height = Mathf.Max(0f, fullH * openT);

            float gap = 4f * _uiScale;
            float bottomLimit = _panelArea.yMax - 8f * _uiScale;
            float topLimit = _panelArea.y + 8f * _uiScale;
            bool flip = header.yMax + gap + fullH > bottomLimit && header.y - gap - fullH >= topLimit;
            if (!flip && header.yMax + gap + fullH > bottomLimit)
            {
                float below = bottomLimit - (header.yMax + gap);
                float above = header.y - gap - topLimit;
                flip = above > below;
            }

            Rect list = flip
                ? new Rect(header.x, header.y - gap - height, header.width, height)
                : new Rect(header.x, header.yMax + gap, header.width, height);

            if (ev != null && ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (_dropJustOpened)
                {
                    _dropJustOpened = false;
                }
                else if (list.Contains(mouse))
                {
                    float contentY = flip ? (height - fullH) : 0f;
                    float localY = mouse.y - list.y - contentY - pad;
                    int i = Mathf.FloorToInt(localY / rowH);
                    ev.Use();
                    if (i >= 0 && i < _dropOptions.Length)
                    {
                        if (_dropPick != null)
                            _dropPick(i);
                        CloseDropdown();
                        return;
                    }
                }
                else if (!header.Contains(mouse))
                {
                    CloseDropdown();
                    ev.Use();
                    return;
                }
            }
            else if (_dropJustOpened && ev != null && ev.type != EventType.MouseDown)
                _dropJustOpened = false;

            if (!_paint || height < 1f)
                return;

            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * (0.4f + 0.6f * openT));

            GUI.BeginClip(list);
            float originY = flip ? (height - fullH) : 0f;
            // Fixed-size popup texture inside the clip, so the open animation never rebuilds it.
            _theme.Draw(Shape.Popup, new Rect(0f, originY, list.width, fullH), 12f * _uiScale);
            Vector2 localMouse = new Vector2(mouse.x - list.x, mouse.y - list.y);
            for (int i = 0; i < _dropOptions.Length; i++)
            {
                Rect row = new Rect(pad, originY + pad + i * rowH, list.width - pad * 2f, rowH);
                bool selected = i == _dropIndex;
                bool hover = row.Contains(localMouse);
                if (hover && !selected)
                    _theme.Draw(Shape.PillHover, row, 8f * _uiScale);
                if (selected)
                {
                    _theme.DrawAccent(Shape.PillActive, row, 8f * _uiScale, 1f);
                    Label(new Rect(row.x + 4f * _uiScale, row.y, 16f * _uiScale, row.height), "✓", Style(_theme.Center));
                }
                Label(new Rect(row.x + 22f * _uiScale, row.y, row.width - 28f * _uiScale, row.height), _dropOptions[i], Style(_theme.Label));
            }
            GUI.EndClip();
            GUI.color = prev;
        }

        private Rect ToUnclipped(Rect local)
        {
            return new Rect(local.x + _bodyRect.x, local.y + _bodyRect.y, local.width, local.height);
        }

        private bool HeaderClicked(Rect field)
        {
            Event ev = Event.current;
            return ev != null && ev.type == EventType.MouseDown && ev.button == 0 && field.Contains(MouseGui());
        }

        private void CloseDropdown()
        {
            _dropId = 0;
            _dropOptions = null;
            _dropPick = null;
            _dropJustOpened = false;
        }

        private bool WideButton(float x, ref float y, float w, string text, int id)
        {
            float h = 28f * _uiScale;
            bool clicked = Chip(new Rect(x, y, w, h), text, false, id);
            y += h + GlassTheme.RowGap * _uiScale;
            return clicked;
        }

        private void ChipRow(float x, ref float y, float w, string[] names, string active, Action<string> onPick, int columns)
        {
            float h = 26f * _uiScale;
            float gap = 6f * _uiScale;
            int cols = Mathf.Min(columns, names.Length);
            float cw = (w - gap * (cols - 1)) / cols;
            for (int i = 0; i < names.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Rect r = new Rect(x + col * (cw + gap), y + row * (h + gap), cw, h);
                bool on = string.Equals(active, names[i], StringComparison.OrdinalIgnoreCase);
                if (Chip(r, names[i], on, 200 + i) && onPick != null)
                    onPick(names[i]);
            }
            int rows = (names.Length + cols - 1) / cols;
            y += rows * (h + gap) + 4f * _uiScale;
        }

        private bool Chip(Rect rect, string text, bool active, int id)
        {
            if (_paint)
            {
                DrawPillBase(rect, rect.height * 0.5f, active, HoverAmount(rect, 400 + id), _anim.Decay(4200 + id, 0.28f));
                Label(rect, text, Style(_theme.Center));
            }
            bool clicked = Hit(rect);
            if (clicked)
                _anim.Fire(4200 + id);
            return clicked;
        }

        private bool Hit(Rect rect)
        {
            if (_dropId != 0)
                return false;
            _content.text = Empty;
            _content.tooltip = Empty;
            GUIStyle inv = _theme.Invisible;
            if (inv != null)
                return GUI.Button(rect, _content, inv);
            return Clicked(rect);
        }

        private void DrawTextField(Rect field)
        {
            try
            {
                if (_paint)
                {
                    _theme.Draw(Shape.Field, field, 10f * _uiScale);
                }
                GUIStyle style = Style(_theme.Field);
                string current = _profileName ?? Empty;
                string next = style != null
                    ? GUI.TextField(field, current, 40, style)
                    : GUI.TextField(field, current, 40);
                if (next != _profileName)
                    _profileName = next;
            }
            catch (MissingMethodException)
            {
                _textFieldMissing = true;
            }
        }

        private bool SaveProfile(bool overwrite)
        {
            string name = ProfileStore.Sanitize(string.IsNullOrWhiteSpace(_profileName) ? _selectedProfile : _profileName);
            if (string.IsNullOrEmpty(name))
            {
                Toast("Name a profile first");
                return false;
            }
            if (!overwrite)
            {
                for (int i = 0; i < _profileList.Length; i++)
                {
                    if (string.Equals(_profileList[i], name, StringComparison.OrdinalIgnoreCase))
                    {
                        Toast("Already exists — use Overwrite");
                        return false;
                    }
                }
            }
            if (_profiles != null && _profiles.Save(name, _settings))
            {
                _selectedProfile = name;
                _profileName = name;
                _lastConfigName = name;
                _status = "Saved config '" + name + "'";
                _settings.Save();
                RefreshProfiles();
                Toast("Saved  ✓");
                return true;
            }
            Toast("Save failed");
            return false;
        }

        private void LoadSelected()
        {
            string name = _selectedProfile ?? _profileName;
            if (string.IsNullOrEmpty(name) || _profiles == null)
            {
                Toast("Select a profile");
                return;
            }
            if (_profiles.Load(name, _settings))
            {
                _activePreset = null;
                _lastConfigName = name;
                _status = "Loaded config '" + name + "'";
                Changed(true);
                _settings.Save();
                Toast("Loaded  " + name);
            }
            else
            {
                Toast("Load failed");
            }
        }

        private void DeleteSelected()
        {
            string name = _selectedProfile ?? _profileName;
            if (string.IsNullOrEmpty(name) || _profiles == null)
            {
                Toast("Select a profile");
                return;
            }
            if (_profiles.Delete(name))
            {
                if (string.Equals(_selectedProfile, name, StringComparison.OrdinalIgnoreCase))
                    _selectedProfile = null;
                if (string.Equals(_lastConfigName, name, StringComparison.OrdinalIgnoreCase))
                    _lastConfigName = null;
                RefreshProfiles();
                Toast("Deleted  " + name);
            }
            else
            {
                Toast("Delete failed");
            }
        }

        private void RefreshProfiles()
        {
            _profileList = _profiles != null ? _profiles.ListNames() : Array.Empty<string>();
        }

        private void ApplyNamedPreset(string name)
        {
            switch (name)
            {
                case "Vivid": _settings.ApplyPresetVivid(); break;
                case "Cinematic": _settings.ApplyPresetCinematic(); break;
                case "Performance": _settings.ApplyPresetPerformance(); break;
                case "Natural+": _settings.ApplyPresetNaturalPlus(); break;
                case "Night": _settings.ApplyPresetNight(); break;
                case "Off": _settings.ApplyPresetOff(); break;
                default: return;
            }
            _activePreset = name;
            _lastConfigName = name;
            _status = "Saved config '" + name + "'";
            Changed(true);
            Toast("Applied  " + name);
        }

        private string FormatCached(int id, float value, string format)
        {
            if (_fmtText.TryGetValue(id, out string text) && _fmtValue.TryGetValue(id, out float prev) && Math.Abs(prev - value) < 0.0005f)
                return text;
            text = value.ToString(format);
            _fmtValue[id] = value;
            _fmtText[id] = text;
            return text;
        }

        private void Label(Rect rect, string text, GUIStyle style)
        {
            if (style != null)
                GUI.Label(rect, text, style);
            else
                GUI.Label(rect, text);
        }

        private GUIStyle Style(GUIStyle preferred)
        {
            return preferred;
        }

        private bool Clicked(Rect rect)
        {
            return MouseDown() && rect.Contains(MouseGui());
        }

        private static Vector2 MouseGui()
        {
            try
            {
                Event ev = Event.current;
                if (ev != null)
                    return ev.mousePosition;
            }
            catch { }
            try
            {
                Vector3 p = Input.mousePosition;
                return new Vector2(p.x, Screen.height - p.y);
            }
            catch
            {
                return Vector2.zero;
            }
        }

        private static bool MouseDown()
        {
            try
            {
                Event ev = Event.current;
                if (ev != null)
                    return ev.type == EventType.MouseDown && ev.button == 0;
                return Input.GetMouseButtonDown(0);
            }
            catch
            {
                return false;
            }
        }

        private static bool MouseHeld()
        {
            try { return Input.GetMouseButton(0); }
            catch { return false; }
        }

        private void Toast(string message)
        {
            CrashLog.Mark("toast: " + message);
            _toast = message;
            _toastAge = 0f;
        }

        private void Autosave()
        {
            try
            {
                _settings.Save();
                _dirty = false;
                if (string.IsNullOrEmpty(_status))
                    _status = string.IsNullOrEmpty(_lastConfigName)
                        ? "Saved preferences"
                        : "Saved config '" + _lastConfigName + "'";
            }
            catch (Exception ex)
            {
                _log?.Error("Autosave on close failed: {0}", ex);
            }
        }

        private void Changed(bool immediate)
        {
            _dirty = true;
            if (immediate)
                _onChanged();
            else
                _applyQueued = true;
        }

        private void StealCursor()
        {
            try
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cursorStolen = true;
            }
            catch
            {
                _cursorStolen = false;
            }
        }

        private void RestoreCursor()
        {
            if (!_cursorStolen)
                return;
            try
            {
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
            }
            catch { }
            _cursorStolen = false;
        }
    }
}
