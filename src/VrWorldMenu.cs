using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace VividFrame
{
    /// <summary>A tracked, world-space settings panel so VR users are not dependent on IMGUI.</summary>
    internal sealed class VrWorldMenu
    {
        private sealed class Preference
        {
            public object Entry;
            public PropertyInfo BoxedValue;
            public string Id;
            public string Label;
            public string Page;
            public Type ValueType;
        }

        private static readonly string[] Pages = { "COLOR", "EFFECTS", "CAMERA", "HUD", "QUALITY", "SYSTEM", "ALL" };
        private static readonly string[] Keys = { "None", "Home", "Insert", "F1", "F2", "F5", "F6", "F7", "F8", "F9", "End", "Delete", "LeftAlt", "LeftControl", "RightControl", "Z", "X", "C", "V", "B", "N", "Tab" };
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static bool _loggedShader;
        private const int RowsVisible = 9;
        private const float PanelWidth = 0.86f;
        private const float PanelHeight = 0.66f;
        private const float RowHeight = 0.044f;
        private const float RowStartY = 0.18f;
        private const float HudHalfWidth = 0.5f;
        private const float HudHalfHeight = 0.32f;

        // TextMesh draws a glyph about characterSize * fontSize * 0.1 world units tall, so sizes that
        // were written as characterSize * fontSize (metres) must be scaled up. With a panel row about
        // 4 cm tall this gives roughly 2 cm text, readable in a headset at arm's length.
        private const float TextScale = 20f;

        private readonly Settings _settings;
        private readonly MiscTweaks _misc;
        private readonly Action _onChanged;
        private readonly Action _requestScreenshot;
        private readonly MelonLogger.Instance _log;
        private readonly Dictionary<Collider, Action> _actions = new Dictionary<Collider, Action>();
        private readonly List<Preference> _preferences = new List<Preference>(128);
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _root;
        private Transform _hudRoot;
        private TextMesh _hudText;
        private TextMesh _crosshairText;
        private GameObject _topBar;
        private GameObject _bottomBar;
        private GameObject _pointerDot;
        private Camera _camera;
        private Camera _hudCamera;
        private string _page = "COLOR";
        private int _offset;
        private float _nextBuild;
        private bool _needsBuild = true;
        private bool _loggedFailure;
        private bool _hideHud;
        private float _nextHudUpdate;

        public VrWorldMenu(Settings settings, MiscTweaks misc, Action onChanged, Action requestScreenshot, MelonLogger.Instance log)
        {
            _settings = settings;
            _misc = misc;
            _onChanged = onChanged;
            _requestScreenshot = requestScreenshot;
            _log = log;
            LoadPreferences();
        }

        public void Tick(bool requested)
        {
            bool show = VrSupport.Active && requested;
            if (VrSupport.Active)
            {
                try { UpdateHud(); }
                catch (Exception ex)
                {
                    if (!_loggedFailure)
                    {
                        _loggedFailure = true;
                        _log?.Warning("VR HUD failed and was hidden: {0}", ex.Message);
                    }
                    if (_hudRoot != null)
                        _hudRoot.gameObject.SetActive(false);
                }
            }
            else if (_hudRoot != null)
                _hudRoot.gameObject.SetActive(false);
            if (!show)
            {
                if (_root != null)
                    _root.gameObject.SetActive(false);
                if (_pointerDot != null)
                    _pointerDot.SetActive(false);
                return;
            }

            try
            {
                EnsureRoot();
                _root.gameObject.SetActive(true);
                EnsurePointerDot();
                if (_camera != null)
                {
                    _root.localPosition = new Vector3(0f, -0.03f, 1.15f);
                    _root.localRotation = Quaternion.identity;
                }

                if (_needsBuild)
                    BuildPage();
                if (VrSupport.ScrollDelta != 0f && Time.unscaledTime >= _nextBuild)
                {
                    _offset = Mathf.Max(0, _offset + (VrSupport.ScrollDelta < 0f ? 1 : -1));
                    _nextBuild = Time.unscaledTime + 0.12f;
                    _needsBuild = true;
                    BuildPage();
                }

                UpdatePointer();
            }
            catch (Exception ex)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    _log?.Warning("VR settings panel failed and was hidden: {0}", ex.Message);
                }
                if (_root != null)
                    _root.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
            if (_hudRoot != null)
                UnityEngine.Object.Destroy(_hudRoot.gameObject);
            if (_pointerDot != null)
                UnityEngine.Object.Destroy(_pointerDot);
            _root = null;
            _hudRoot = null;
            _actions.Clear();
        }

        public void HideHud(bool hide)
        {
            _hideHud = hide;
            if (hide && _hudRoot != null)
                _hudRoot.gameObject.SetActive(false);
        }

        private void UpdateHud()
        {
            bool wantsHud = !_hideHud && (_settings.FpsOverlay.Value || _settings.Crosshair.Value || _settings.CinemaBars.Value
                            || _settings.StatClock.Value || _settings.StatTimer.Value || _settings.StatPosition.Value
                            || _settings.StatRam.Value || _settings.StatPlayers.Value);
            if (!wantsHud)
            {
                if (_hudRoot != null)
                    _hudRoot.gameObject.SetActive(false);
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
                return;
            if (_hudRoot == null || _hudCamera != camera)
            {
                if (_hudRoot != null)
                    UnityEngine.Object.Destroy(_hudRoot.gameObject);
                _hudCamera = camera;
                var hud = new GameObject("VividFrame VR HUD");
                hud.hideFlags = HideFlags.HideAndDontSave;
                hud.transform.SetParent(camera.transform, false);
                hud.transform.localRotation = Quaternion.identity;
                _hudRoot = hud.transform;
                _hudText = CreateHudText("VividFrame VR Stats", new Vector3(-HudHalfWidth, HudHalfHeight, 1.48f), TextAnchor.UpperLeft, 16);
                _crosshairText = CreateHudText("VividFrame VR Crosshair", new Vector3(0f, 0f, 1.2f), TextAnchor.MiddleCenter, 26);
                _crosshairText.text = "+";
                _topBar = CreateHudBar("VividFrame VR Top Bar", new Vector3(0f, 0.31f, 1.5f));
                _bottomBar = CreateHudBar("VividFrame VR Bottom Bar", new Vector3(0f, -0.31f, 1.5f));
            }

            _hudRoot.gameObject.SetActive(true);
            _crosshairText.gameObject.SetActive(_settings.Crosshair.Value);
            _crosshairText.text = _settings.CrosshairStyle.Value == 0 ? "•" : (_settings.CrosshairStyle.Value == 1 ? "+" : "+ ·");
            _crosshairText.characterSize = Mathf.Clamp(_settings.CrosshairSize.Value, 2f, 24f) * 0.0013f;
            Color crossColor = Color.white;
            crossColor.a = Mathf.Clamp01(_settings.CrosshairOpacity.Value);
            _crosshairText.color = crossColor;
            _topBar.SetActive(_settings.CinemaBars.Value);
            _bottomBar.SetActive(_settings.CinemaBars.Value);
            float barHeight = Mathf.Clamp(_settings.CinemaThickness.Value, 0.02f, 0.3f) * 1.2f;
            float screenHeight = 3f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float screenWidth = screenHeight * Screen.width / Mathf.Max(1f, Screen.height);
            _topBar.transform.localPosition = new Vector3(0f, screenHeight * 0.5f - barHeight * 0.5f, 1.5f);
            _bottomBar.transform.localPosition = new Vector3(0f, -screenHeight * 0.5f + barHeight * 0.5f, 1.5f);
            _topBar.transform.localScale = new Vector3(screenWidth, barHeight, 1f);
            _bottomBar.transform.localScale = new Vector3(screenWidth, barHeight, 1f);
            if (Time.unscaledTime < _nextHudUpdate)
                return;
            _nextHudUpdate = Time.unscaledTime + 0.25f;
            var lines = new System.Text.StringBuilder(160);
            if (_settings.FpsOverlay.Value)
            {
                float dt = Time.unscaledDeltaTime;
                lines.Append(dt > 0.0001f ? (1f / dt).ToString("0") : "-").Append(" FPS\n");
            }
            if (_settings.StatClock.Value)
                lines.Append(DateTime.Now.ToString("HH:mm:ss")).Append('\n');
            if (_settings.StatTimer.Value)
                lines.Append("Session  ").Append(TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"hh\:mm\:ss")).Append('\n');
            if (_settings.StatPlayers.Value && _misc != null)
                lines.Append(_misc.PlayersText()).Append('\n');
            if (_settings.StatRam.Value && _misc != null)
                lines.Append(_misc.RamMegabytes().ToString("0")).Append(" MB RAM\n");
            if (_settings.StatPosition.Value && _misc != null)
                lines.Append(_misc.CameraPositionText()).Append('\n');
            _hudText.text = lines.ToString();
            int corner = Mathf.Clamp(_settings.StatsCorner.Value, 0, 3);
            bool right = corner == 1 || corner == 3;
            bool bottom = corner >= 2;
            _hudText.anchor = bottom ? (right ? TextAnchor.LowerRight : TextAnchor.LowerLeft)
                                     : (right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            _hudText.alignment = right ? TextAlignment.Right : TextAlignment.Left;
            _hudText.transform.localPosition = new Vector3(right ? HudHalfWidth : -HudHalfWidth, bottom ? -HudHalfHeight : HudHalfHeight, 1.48f);
        }

        private TextMesh CreateHudText(string name, Vector3 position, TextAnchor anchor, int size)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_hudRoot, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.fontSize = size;
            mesh.characterSize = 0.0007f * TextScale;
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            mesh.color = new Color(0.95f, 0.97f, 1f, 1f);
            mesh.richText = false;
            return mesh;
        }

        private GameObject CreateHudBar(string name, Vector3 position)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = name;
            bar.hideFlags = HideFlags.HideAndDontSave;
            bar.transform.SetParent(_hudRoot, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = new Vector3(0.86f, 0.055f, 1f);
            Renderer renderer = bar.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial(Color.black);
            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
            return bar;
        }

        private void EnsurePointerDot()
        {
            if (_pointerDot != null)
                return;
            _pointerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _pointerDot.name = "VividFrame VR Pointer";
            _pointerDot.hideFlags = HideFlags.HideAndDontSave;
            _pointerDot.transform.localScale = Vector3.one * 0.012f;
            Renderer renderer = _pointerDot.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial(new Color(0.4f, 0.85f, 1f, 1f));
            Collider collider = _pointerDot.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
            _pointerDot.SetActive(false);
        }

        private void EnsureRoot()
        {
            Camera current = Camera.main;
            if (current == null)
                return;
            if (_root != null && _camera == current)
                return;
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
            _camera = current;
            var panel = new GameObject("VividFrame VR Panel");
            panel.hideFlags = HideFlags.HideAndDontSave;
            panel.transform.SetParent(current.transform, false);
            panel.transform.localPosition = new Vector3(0f, -0.03f, 1.15f);
            // Unity quads and TextMesh are readable from their -Z side, and the camera looks down +Z,
            // so no rotation is needed. (A 180 degree turn hid the quads and mirrored the text.)
            panel.transform.localRotation = Quaternion.identity;
            _root = panel.transform;
            BuildPage();
        }

        private void LoadPreferences()
        {
            foreach (FieldInfo field in typeof(Settings).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object entry = field.GetValue(_settings);
                if (entry == null)
                    continue;
                Type type = entry.GetType();
                if (!type.IsGenericType || !type.Name.StartsWith("MelonPreferences_Entry", StringComparison.Ordinal))
                    continue;
                PropertyInfo boxed = type.GetProperty("BoxedValue");
                PropertyInfo hidden = type.GetProperty("IsHidden");
                if (boxed == null || (hidden != null && hidden.GetValue(entry) is bool isHidden && isHidden))
                    continue;
                Type valueType = type.GetGenericArguments()[0];
                if (valueType != typeof(bool) && valueType != typeof(float) && valueType != typeof(double)
                    && valueType != typeof(int) && valueType != typeof(string))
                    continue;
                string id = Convert.ToString(type.GetProperty("Identifier")?.GetValue(entry)) ?? field.Name;
                if (valueType == typeof(string) && !IsKeySetting(id))
                    continue;
                string label = Convert.ToString(type.GetProperty("DisplayName")?.GetValue(entry));
                if (string.IsNullOrWhiteSpace(label))
                    label = SplitName(id);
                _preferences.Add(new Preference
                {
                    Entry = entry,
                    BoxedValue = boxed,
                    Id = id,
                    Label = label,
                    Page = Category(id),
                    ValueType = valueType
                });
            }
        }

        private void BuildPage()
        {
            if (_root == null)
                return;
            _needsBuild = false;
            _actions.Clear();
            for (int i = _root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);

            MakeQuad("Panel", Vector3.zero, new Vector3(PanelWidth, PanelHeight, 1f), new Color(0.035f, 0.045f, 0.065f, 0.98f), null);
            MakeText("VividFrame  /  VR", new Vector3(-0.38f, 0.285f, 0.006f), 0.001f, 28, Color.white, TextAnchor.MiddleLeft);

            float tabWidth = 0.116f;
            float tabsX = -0.405f;
            for (int i = 0; i < Pages.Length; i++)
            {
                string page = Pages[i];
                float x = tabsX + i * (tabWidth + 0.002f);
                Collider tab = MakeQuad("Tab " + page, new Vector3(x, 0.235f, 0.004f), new Vector3(tabWidth, 0.045f, 1f),
                    page == _page ? new Color(0.2f, 0.45f, 0.7f, 1f) : new Color(0.12f, 0.15f, 0.2f, 1f), () =>
                    {
                        _page = page;
                        _offset = 0;
                        BuildPage();
                    });
                MakeText(page, new Vector3(x, 0.235f, 0.009f), 0.00055f, 17, Color.white, TextAnchor.MiddleCenter);
            }

            var list = new List<Preference>();
            for (int i = 0; i < _preferences.Count; i++)
                if (_page == "ALL" || _preferences[i].Page == _page)
                    list.Add(_preferences[i]);
            int maxOffset = Mathf.Max(0, list.Count - RowsVisible);
            _offset = Mathf.Clamp(_offset, 0, maxOffset);
            int end = Mathf.Min(list.Count, _offset + RowsVisible);
            for (int i = _offset; i < end; i++)
                BuildRow(list[i], i - _offset);

            if (_page == "CAMERA")
            {
                MakeQuad("Take screenshot", new Vector3(0.22f, -0.235f, 0.004f), new Vector3(0.2f, 0.034f, 1f),
                    new Color(0.2f, 0.45f, 0.7f, 1f), () => _requestScreenshot?.Invoke());
                MakeText("TAKE SCREENSHOT", new Vector3(0.22f, -0.235f, 0.009f), 0.00055f, 15, Color.white, TextAnchor.MiddleCenter);
            }

            MakeText(list.Count == 0 ? "No controls on this page" : (_offset + 1) + "–" + end + " / " + list.Count + "   ·   right stick to scroll",
                new Vector3(-0.38f, -0.305f, 0.008f), 0.0005f, 13, new Color(0.72f, 0.78f, 0.86f), TextAnchor.MiddleLeft);
        }

        private void BuildRow(Preference pref, int row)
        {
            float y = RowStartY - row * RowHeight;
            MakeQuad("Row", new Vector3(0f, y, 0.002f), new Vector3(0.79f, RowHeight - 0.003f, 1f), new Color(0.08f, 0.105f, 0.14f, 1f), null);
            MakeText(pref.Label, new Vector3(-0.375f, y, 0.009f), 0.00062f, 17, Color.white, TextAnchor.MiddleLeft);

            if (pref.Id.IndexOf("Fov", StringComparison.OrdinalIgnoreCase) >= 0
                || pref.Id.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MakeText("Headset controls projection", new Vector3(0.27f, y, 0.009f), 0.00048f, 13,
                    new Color(0.72f, 0.78f, 0.86f), TextAnchor.MiddleCenter);
                return;
            }

            object value = pref.BoxedValue.GetValue(pref.Entry);
            if (pref.ValueType == typeof(bool))
            {
                string text = (value is bool b && b) ? "ON" : "OFF";
                MakeText(text, new Vector3(0.33f, y, 0.009f), 0.0007f, 18,
                    text == "ON" ? new Color(0.5f, 1f, 0.72f) : new Color(0.72f, 0.75f, 0.8f), TextAnchor.MiddleCenter);
                AddAction(new Vector3(0.32f, y, 0.012f), new Vector3(0.11f, RowHeight - 0.003f, 0.02f), () => SetValue(pref, !(bool)value));
                return;
            }

            if (pref.ValueType == typeof(string))
            {
                MakeText(Convert.ToString(value), new Vector3(0.27f, y, 0.009f), 0.00052f, 14, new Color(0.78f, 0.86f, 1f), TextAnchor.MiddleCenter);
                AddAction(new Vector3(0.285f, y, 0.012f), new Vector3(0.17f, RowHeight - 0.003f, 0.02f), () => CycleKey(pref, 1));
                AddAction(new Vector3(0.185f, y, 0.012f), new Vector3(0.035f, RowHeight - 0.003f, 0.02f), () => CycleKey(pref, -1));
                return;
            }

            string shown = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            MakeText(shown, new Vector3(0.27f, y, 0.009f), 0.00054f, 15, new Color(0.78f, 0.86f, 1f), TextAnchor.MiddleCenter);
            MakeText("−", new Vector3(0.17f, y, 0.009f), 0.0008f, 22, Color.white, TextAnchor.MiddleCenter);
            MakeText("+", new Vector3(0.37f, y, 0.009f), 0.0008f, 22, Color.white, TextAnchor.MiddleCenter);
            AddAction(new Vector3(0.17f, y, 0.012f), new Vector3(0.055f, RowHeight - 0.003f, 0.02f), () => ChangeNumber(pref, -1));
            AddAction(new Vector3(0.37f, y, 0.012f), new Vector3(0.055f, RowHeight - 0.003f, 0.02f), () => ChangeNumber(pref, 1));
        }

        private void UpdatePointer()
        {
            int count = Physics.RaycastNonAlloc(VrSupport.RightControllerRay, _hits, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Collider target = null;
            float nearest = float.MaxValue;
            Vector3 hitPoint = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i].collider;
                if (hit != null && _hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    target = _actions.ContainsKey(hit) ? hit : null;
                    hitPoint = _hits[i].point;
                }
            }
            if (_pointerDot != null)
            {
                bool hovering = target != null;
                _pointerDot.SetActive(hovering);
                if (hovering)
                    _pointerDot.transform.position = hitPoint - VrSupport.RightControllerRay.direction * 0.006f;
            }
            if (VrSupport.TriggerDown && target != null)
            {
                _actions[target]();
                _onChanged?.Invoke();
                _settings.Save();
            }
            VrSupport.ConsumeTriggerDown();
        }

        private void ChangeNumber(Preference pref, int direction)
        {
            object value = pref.BoxedValue.GetValue(pref.Entry);
            if (pref.ValueType == typeof(int))
            {
                int current = Convert.ToInt32(value);
                int next = Mathf.Clamp(current + direction, 0, IntMax(pref.Id));
                if (pref.Id == "ScreenshotScale") next = Mathf.Clamp(current + direction, 1, 4);
                SetValue(pref, next);
            }
            else
            {
                float current = Convert.ToSingle(value);
                float step = Mathf.Max(0.01f, Mathf.Abs(current) * 0.05f);
                if (IsColorValue(pref.Id)) step = 1f;
                if (pref.Id.IndexOf("Fov", StringComparison.OrdinalIgnoreCase) >= 0) step = 2f;
                float next = Mathf.Clamp(current + direction * step, -10000f, 10000f);
                SetValue(pref, pref.ValueType == typeof(double) ? (object)(double)next : next);
            }
            BuildPage();
        }

        private static int IntMax(string id)
        {
            if (id == "Tonemapper") return 3;
            if (id == "AoMode") return 1;
            if (id == "FocusMode") return 2;
            if (id == "DofKernelSize") return 3;
            if (id == "CrosshairStyle") return 2;
            if (id == "FpsCorner" || id == "StatsCorner") return 3;
            if (id == "AccentIndex") return 3;
            if (id == "MsaaIndex") return 4;
            if (id == "ShadowMode" || id == "ShadowRes" || id == "TextureLimit" || id == "SkinWeights") return 3;
            if (id == "ShadowCascades") return 4;
            return 10000;
        }

        private void CycleKey(Preference pref, int direction)
        {
            string current = Convert.ToString(pref.BoxedValue.GetValue(pref.Entry)) ?? "None";
            int index = Array.IndexOf(Keys, current);
            index = (index + direction + Keys.Length) % Keys.Length;
            SetValue(pref, Keys[index]);
            BuildPage();
        }

        private void SetValue(Preference pref, object value)
        {
            try { pref.BoxedValue.SetValue(pref.Entry, value); }
            catch (Exception ex) { _log?.Warning("VR control could not set {0}: {1}", pref.Id, ex.Message); }
        }

        private Collider MakeQuad(string name, Vector3 position, Vector3 size, Color color, Action action)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_root, false);
            // Positive z in the layout means "in front of the panel", which is toward the camera (-z).
            go.transform.localPosition = new Vector3(position.x, position.y, -position.z);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial(color);
            Collider original = go.GetComponent<Collider>();
            if (original != null)
                UnityEngine.Object.Destroy(original);
            if (action == null)
                return null;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1f, 0.03f);
            _actions[collider] = action;
            return collider;
        }

        private static Material GetMaterial(Color color)
        {
            Color32 c = color;
            int key = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            if (Materials.TryGetValue(key, out Material material) && material != null)
                return material;
            // Try shaders in order of how likely the player build is to contain them. A lit shader
            // would render dark in a world without lights, so unlit ones come first.
            Shader shader = null;
            string[] candidates = { "Unlit/Color", "Sprites/Default", "UI/Default", "Hidden/Internal-Colored", "Standard" };
            for (int i = 0; i < candidates.Length && shader == null; i++)
            {
                Shader found = Shader.Find(candidates[i]);
                if (found != null && found.isSupported)
                    shader = found;
            }
            if (shader == null)
                return null;
            if (!_loggedShader)
            {
                _loggedShader = true;
                MelonLogger.Msg("[VividFrame] VR panel shader: {0}", shader.name);
            }
            material = new Material(shader);
            material.color = color;
            // Visible from either side. Depth testing stays on so the stacked layers (panel, rows,
            // tabs, text) order themselves by their small z offsets.
            try { material.SetInt("_Cull", 0); } catch { }
            material.hideFlags = HideFlags.HideAndDontSave;
            Materials[key] = material;
            return material;
        }

        private void AddAction(Vector3 position, Vector3 size, Action action)
        {
            GameObject go = new GameObject("VR Control");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -position.z);
            go.transform.localScale = Vector3.one;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            _actions[collider] = action;
        }

        private void MakeText(string text, Vector3 position, float characterSize, int fontSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("VR Text");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -position.z);
            go.transform.localRotation = Quaternion.identity;
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text ?? string.Empty;
            mesh.fontSize = fontSize;
            mesh.characterSize = characterSize * TextScale;
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            mesh.color = color;
            mesh.richText = false;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static bool IsKeySetting(string id)
        {
            return id.EndsWith("Key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsColorValue(string id)
        {
            return id.IndexOf("Saturation", StringComparison.OrdinalIgnoreCase) >= 0
                   || id.IndexOf("Contrast", StringComparison.OrdinalIgnoreCase) >= 0
                   || id.IndexOf("Vibrance", StringComparison.OrdinalIgnoreCase) >= 0
                   || id.IndexOf("Temperature", StringComparison.OrdinalIgnoreCase) >= 0
                   || id.IndexOf("Tint", StringComparison.OrdinalIgnoreCase) >= 0
                   || id.IndexOf("HueShift", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Category(string id)
        {
            if (IsColorValue(id) || id == "Brightness" || id == "Gamma" || id == "Sharpen" || id == "Tonemapper") return "COLOR";
            if (Contains(id, "Bloom", "MotionBlur", "Vignette", "Grain", "ChromaticAberration", "AmbientOcclusion", "Ao", "DepthOfField", "Dof", "LensDistortion")) return "EFFECTS";
            if (Contains(id, "Fov", "Zoom", "Clip", "ScreenshotScale", "ScreenshotKey")) return "CAMERA";
            if (Contains(id, "Overlay", "Crosshair", "Cinema", "Stat", "StatsCorner")) return "HUD";
            if (Contains(id, "Quality", "FpsCap", "VSync", "Lod", "Shadow", "Texture", "Anisotropic", "Msaa", "PixelLights", "SkinWeights", "SoftParticles", "ReflectionProbes")) return "QUALITY";
            if (Contains(id, "RunInBackground", "Mute", "Volume", "LoadPriority", "Fog", "Ambient", "AutoUpdate")) return "SYSTEM";
            return "ALL";
        }

        private static bool Contains(string value, params string[] fragments)
        {
            for (int i = 0; i < fragments.Length; i++)
                if (value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static string SplitName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var result = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
                    result.Append(' ');
                result.Append(value[i]);
            }
            return result.ToString();
        }
    }
}
