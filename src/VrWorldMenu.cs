using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace VividFrame
{
    /// <summary>
    /// The VR version of the menu. The desktop glass menu is drawn into a RenderTexture (see
    /// <see cref="Menu.DrawVr"/>) and shown on one world-space quad, so every tab and control matches the
    /// desktop exactly. The right controller points at it with a laser; the trigger clicks, the right
    /// stick scrolls and dragging the title bar moves it. Overlays go on a second, head-locked quad.
    /// </summary>
    internal sealed class VrWorldMenu
    {
        private static readonly string[] ShaderNames = { "UI/Default", "Unlit/Transparent", "Sprites/Default", "Unlit/Texture" };

        private const float PanelWidth = 0.62f;     // metres at panel size 1
        private const float PanelDistance = 0.75f;
        private const float PanelDrop = 0.10f;       // a little below eye level, like a tablet held in front
        private const float FollowAngle = 50f;
        private const float FollowDistance = 1.6f;
        private const float FollowDelay = 0.8f;
        private const float HudDistance = 1.2f;
        private const float HudWidth = 1.0f;
        private const int HudPixelsW = 1280;
        private const int HudPixelsH = 720;
        private const float HudScale = 1.3f;
        private const float LaserWidth = 0.003f;
        private const float ScrollRate = 40f;        // wheel units per second at full stick

        private readonly Settings _settings;
        private readonly MelonLogger.Instance _log;

        private Shader _shader;
        private bool _shaderSearched;
        private Mesh _quadMesh;
        private Mesh _laserMesh;

        private GameObject _panel;
        private Material _panelMat;
        private RenderTexture _panelRt;
        private float _panelScale;

        private GameObject _laser;
        private Material _laserMat;
        private Texture2D _laserTex;
        private int _laserTone = -1;
        private int _colorId;

        private GameObject _hud;
        private Material _hudMat;
        private RenderTexture _hudRt;
        private Transform _hudParent;
        private bool _hudVisible;
        private bool _hideHud;

        private bool _open;
        private Vector3 _targetPos;
        private Quaternion _targetRot = Quaternion.identity;
        private float _awaySince = -1f;
        private bool _grabbing;
        private Vector3 _grabLocal;

        // Pointer state handed to OnGUI. Edges are latched until the next Repaint consumes them.
        private Vector2 _pointer;
        private bool _pointerOn;
        private bool _held;
        private bool _down;
        private bool _up;
        private float _scroll;

        private bool _failed;

        public VrWorldMenu(Settings settings, MelonLogger.Instance log)
        {
            _settings = settings;
            _log = log;
        }

        /// <summary>Call every frame from OnUpdate, after VrSupport.Tick and Menu.Tick.</summary>
        public void Tick(Menu menu)
        {
            if (_failed || menu == null || !VrSupport.Active)
            {
                HideAll();
                return;
            }

            try
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    HideAll();
                    return;
                }
                Transform head = camera.transform;
                TickHud(menu, head);

                if (!menu.Visible || menu.VrFailed)
                {
                    ClosePanel();
                    return;
                }

                if (!EnsurePanel())
                    return;
                if (!_panel.activeSelf)
                    _panel.SetActive(true);
                ApplySize();
                if (!_open)
                {
                    _open = true;
                    PlaceInFront(head, true);
                }

                if (menu.VrGrabRequested)
                {
                    menu.VrGrabRequested = false;
                    if (VrSupport.TriggerHeld && VrSupport.HasPointer)
                        BeginGrab();
                }

                if (_grabbing)
                    UpdateGrab(head);
                else if (_settings.VrFollow.Value && !_held && !_pointerOn)
                    UpdateFollow(head);
                else
                    _awaySince = -1f;

                Transform t = _panel.transform;
                float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
                float k = 1f - Mathf.Exp((_grabbing ? -24f : -9f) * dt);
                t.SetPositionAndRotation(Vector3.Lerp(t.position, _targetPos, k), Quaternion.Slerp(t.rotation, _targetRot, k));

                UpdatePointer(head, dt);
            }
            catch (Exception ex)
            {
                Fail("VR panel failed and was hidden: " + ex.Message);
            }
        }

        /// <summary>Call from OnGUI. Draws into the VR textures on the Repaint event.</summary>
        public void RenderGui(Menu menu)
        {
            if (_failed || menu == null)
                return;
            Event ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint)
                return;

            if (_open && _panelRt != null && _panel != null && _panel.activeSelf)
            {
                menu.DrawVr(_panelRt, _pointer, _pointerOn, _down, _held, _up, _scroll);
                _down = false;
                _up = false;
                _scroll = 0f;
            }
            if (_hudVisible && _hudRt != null)
                menu.DrawVrHud(_hudRt, HudScale);
        }

        public void HideHud(bool hide)
        {
            _hideHud = hide;
            if (hide && _hud != null)
                _hud.SetActive(false);
        }

        public void Dispose()
        {
            DestroyObject(_panel);
            DestroyObject(_laser);
            DestroyObject(_hud);
            DestroyObject(_panelMat);
            DestroyObject(_laserMat);
            DestroyObject(_hudMat);
            DestroyObject(_laserTex);
            DestroyObject(_quadMesh);
            DestroyObject(_laserMesh);
            ReleaseRt(ref _panelRt);
            ReleaseRt(ref _hudRt);
            _panel = null;
            _laser = null;
            _hud = null;
        }

        // ------------------------------------------------------------------ panel

        private bool EnsurePanel()
        {
            float scale = Menu.VrSurfaceScale();
            if (_panelRt == null || !_panelRt.IsCreated() || Math.Abs(scale - _panelScale) > 0.001f)
            {
                ReleaseRt(ref _panelRt);
                _panelScale = scale;
                _panelRt = MakeRt(Menu.VrSurfaceWidth(scale), Menu.VrSurfaceHeight(scale), true);
                if (_panelMat != null)
                    _panelMat.mainTexture = _panelRt;
                CrashLog.Mark("vr: panel texture " + _panelRt.width + "x" + _panelRt.height);
            }

            if (_panel != null)
                return true;
            Shader shader = FindShader();
            if (shader == null)
                return false;
            _panelMat = MakeMaterial(shader, _panelRt, 4000);
            _panel = MakeQuad("VividFrame VR Panel", _panelMat);
            _panel.SetActive(false);
            return true;
        }

        private void ApplySize()
        {
            float size = Mathf.Clamp(_settings.VrPanelSize.Value, 0.7f, 1.4f);
            float width = PanelWidth * size * AvatarScale();
            float height = width * _panelRt.height / Mathf.Max(1f, _panelRt.width);
            _panel.transform.localScale = new Vector3(width, height, 1f);
        }

        private void ClosePanel()
        {
            _open = false;
            _grabbing = false;
            _awaySince = -1f;
            _pointerOn = false;
            _held = false;
            _down = false;
            _up = false;
            _scroll = 0f;
            if (_panel != null && _panel.activeSelf)
                _panel.SetActive(false);
            if (_laser != null && _laser.activeSelf)
                _laser.SetActive(false);
        }

        private void HideAll()
        {
            ClosePanel();
            _hudVisible = false;
            if (_hud != null && _hud.activeSelf)
                _hud.SetActive(false);
        }

        private static float AvatarScale()
        {
            // VRChat scales the tracking space with the avatar; keep the panel the same size in the eye.
            try
            {
                Camera camera = Camera.main;
                return camera != null ? Mathf.Clamp(camera.transform.lossyScale.y, 0.1f, 10f) : 1f;
            }
            catch
            {
                return 1f;
            }
        }

        private void PlaceInFront(Transform head, bool snap)
        {
            float k = AvatarScale();
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = head.up * -1f;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            _targetPos = head.position + forward * (PanelDistance * k) + Vector3.down * (PanelDrop * k);
            _targetRot = FaceHead(_targetPos, head.position);
            if (snap)
                _panel.transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private static Quaternion FaceHead(Vector3 position, Vector3 headPosition)
        {
            // The quad's readable side faces -Z, so +Z points away from the viewer.
            Vector3 away = position - headPosition;
            if (away.sqrMagnitude < 0.000001f)
                away = Vector3.forward;
            return Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private void UpdateFollow(Transform head)
        {
            float k = AvatarScale();
            Vector3 to = _targetPos - head.position;
            float distance = to.magnitude / k;
            bool away = Vector3.Angle(head.forward, to) > FollowAngle || distance > FollowDistance || distance < 0.25f;
            if (!away)
            {
                _awaySince = -1f;
                return;
            }
            float now = Time.unscaledTime;
            if (_awaySince < 0f)
            {
                _awaySince = now;
            }
            else if (now - _awaySince > FollowDelay)
            {
                _awaySince = -1f;
                PlaceInFront(head, false);
            }
        }

        private void BeginGrab()
        {
            Ray ray = VrSupport.RightControllerRay;
            _grabLocal = Quaternion.Inverse(VrSupport.RightControllerRotation) * (_panel.transform.position - ray.origin);
            _grabbing = true;
            _held = false;
            _pointerOn = false;
            _awaySince = -1f;
            CrashLog.Mark("vr: panel grab");
        }

        private void UpdateGrab(Transform head)
        {
            if (!VrSupport.TriggerHeld || !VrSupport.HasPointer)
            {
                _grabbing = false;
                return;
            }
            Ray ray = VrSupport.RightControllerRay;
            _targetPos = ray.origin + VrSupport.RightControllerRotation * _grabLocal;
            _targetRot = FaceHead(_targetPos, head.position);
        }

        // ------------------------------------------------------------------ pointer

        private void UpdatePointer(Transform head, float dt)
        {
            float k = AvatarScale();
            bool hasRay = VrSupport.HasPointer && !_grabbing;
            bool onPanel = false;
            float length = 1.2f * k;
            Ray ray = VrSupport.RightControllerRay;

            if (hasRay)
            {
                Transform t = _panel.transform;
                Vector3 normal = t.forward;
                float facing = Vector3.Dot(ray.direction, normal);
                if (facing > 0.0001f)
                {
                    float distance = Vector3.Dot(t.position - ray.origin, normal) / facing;
                    if (distance > 0f)
                    {
                        Vector3 local = t.InverseTransformPoint(ray.origin + ray.direction * distance);
                        float u = local.x + 0.5f;
                        float v = local.y + 0.5f;
                        onPanel = u >= 0f && u <= 1f && v >= 0f && v <= 1f;
                        if (onPanel || _held)
                        {
                            // GUI space has its origin at the top left.
                            _pointer = new Vector2(u * _panelRt.width, (1f - v) * _panelRt.height);
                            length = distance;
                        }
                    }
                }
            }
            _pointerOn = onPanel;

            if (VrSupport.TriggerDown && onPanel && !_held)
            {
                _held = true;
                _down = true;
            }
            else if (_held && (!VrSupport.TriggerHeld || !hasRay))
            {
                _held = false;
                _up = true;
            }

            if (onPanel && Mathf.Abs(VrSupport.StickY) > 0f)
            {
                float stick = VrSupport.StickY;
                _scroll -= stick * Mathf.Abs(stick) * ScrollRate * dt;
            }

            UpdateLaser(hasRay, ray, length, onPanel, k);
        }

        private void UpdateLaser(bool show, Ray ray, float length, bool onPanel, float k)
        {
            if (!show)
            {
                if (_laser != null && _laser.activeSelf)
                    _laser.SetActive(false);
                return;
            }
            if (_laser == null)
            {
                Shader shader = FindShader();
                if (shader == null)
                    return;
                _laserTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _laserTex.hideFlags = HideFlags.HideAndDontSave;
                Color c = new Color(1f, 1f, 1f, 1f);
                _laserTex.SetPixels(new[] { c, c, c, c });
                _laserTex.Apply(false, true);
                _laserMat = MakeMaterial(shader, _laserTex, 4001);
                _laser = new GameObject("VividFrame VR Laser");
                _laser.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_laser);
                _laser.AddComponent<MeshFilter>().sharedMesh = LaserMesh();
                SetupRenderer(_laser.AddComponent<MeshRenderer>(), _laserMat);
            }
            if (!_laser.activeSelf)
                _laser.SetActive(true);
            int tone = onPanel ? 1 : 0;
            if (tone != _laserTone)
            {
                _laserTone = tone;
                if (_colorId == 0)
                    _colorId = Shader.PropertyToID("_Color");
                _laserMat.SetColor(_colorId, new Color(1f, 1f, 1f, onPanel ? 0.55f : 0.25f));
            }
            Transform t = _laser.transform;
            t.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction, Vector3.up));
            float w = LaserWidth * k;
            t.localScale = new Vector3(w, w, Mathf.Max(0.01f, length));
        }

        // ------------------------------------------------------------------ HUD

        private void TickHud(Menu menu, Transform head)
        {
            bool want = !_hideHud && menu.VrHudWanted;
            if (!want)
            {
                _hudVisible = false;
                if (_hud != null && _hud.activeSelf)
                    _hud.SetActive(false);
                return;
            }

            if (_hudRt == null || !_hudRt.IsCreated())
            {
                ReleaseRt(ref _hudRt);
                _hudRt = MakeRt(HudPixelsW, HudPixelsH, false);
                if (_hudMat != null)
                    _hudMat.mainTexture = _hudRt;
            }
            if (_hud == null)
            {
                Shader shader = FindShader();
                if (shader == null)
                    return;
                _hudMat = MakeMaterial(shader, _hudRt, 3999);
                _hud = MakeQuad("VividFrame VR HUD", _hudMat);
            }
            if (_hudParent != head)
            {
                // Head-locked: parented to the eye camera, which changes on every world load.
                _hudParent = head;
                _hud.transform.SetParent(head, false);
                _hud.transform.localPosition = new Vector3(0f, 0f, HudDistance);
                _hud.transform.localRotation = Quaternion.identity;
                _hud.transform.localScale = new Vector3(HudWidth, HudWidth * HudPixelsH / HudPixelsW, 1f);
            }
            if (!_hud.activeSelf)
                _hud.SetActive(true);
            _hudVisible = true;
        }

        // ------------------------------------------------------------------ helpers

        private Shader FindShader()
        {
            if (_shaderSearched)
                return _shader;
            _shaderSearched = true;
            for (int i = 0; i < ShaderNames.Length; i++)
            {
                try
                {
                    Shader s = Shader.Find(ShaderNames[i]);
                    if (s != null && s.isSupported)
                    {
                        _shader = s;
                        _log?.Msg("VR panel shader: {0}", ShaderNames[i]);
                        return s;
                    }
                }
                catch
                {
                    // try the next one
                }
            }
            Fail("No usable shader for the VR panel was found, so it is turned off.");
            return null;
        }

        private static Material MakeMaterial(Shader shader, Texture texture, int queue)
        {
            Material mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.mainTexture = texture;
            mat.renderQueue = queue;
            mat.SetColor("_Color", Color.white);
            // UI/Default reads its depth test from this property; Always keeps the panel above walls and avatars.
            mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            return mat;
        }

        private GameObject MakeQuad(string name, Material mat)
        {
            GameObject go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            SetupRenderer(go.AddComponent<MeshRenderer>(), mat);
            return go;
        }

        private static void SetupRenderer(MeshRenderer renderer, Material mat)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Mesh QuadMesh()
        {
            if (_quadMesh != null)
                return _quadMesh;
            Mesh mesh = new Mesh();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            // Clockwise when seen from -Z, the side that faces the viewer.
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            _quadMesh = mesh;
            return mesh;
        }

        private Mesh LaserMesh()
        {
            if (_laserMesh != null)
                return _laserMesh;
            // Two crossed strips along +Z, each with both windings, so the beam reads from any angle.
            Mesh mesh = new Mesh();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
                new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 1f), new Vector3(0f, 0.5f, 1f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            Color w = Color.white;
            mesh.colors = new[] { w, w, w, w, w, w, w, w };
            mesh.triangles = new[]
            {
                0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3,
                4, 6, 5, 6, 7, 5, 4, 5, 6, 6, 5, 7
            };
            mesh.RecalculateBounds();
            _laserMesh = mesh;
            return mesh;
        }

        private static RenderTexture MakeRt(int width, int height, bool mips)
        {
            RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.hideFlags = HideFlags.HideAndDontSave;
            rt.useMipMap = mips;
            rt.autoGenerateMips = mips;
            rt.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            rt.anisoLevel = mips ? 4 : 1;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            return rt;
        }

        private static void ReleaseRt(ref RenderTexture rt)
        {
            if (rt == null)
                return;
            try
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
            catch
            {
                // already gone
            }
            rt = null;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            try
            {
                if (obj != null)
                    UnityEngine.Object.Destroy(obj);
            }
            catch
            {
                // already gone
            }
        }

        private void Fail(string message)
        {
            if (!_failed)
                _log?.Warning(message);
            _failed = true;
            HideAll();
        }
    }
}
