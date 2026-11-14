using System;
using MelonLoader;
using UnityEngine;

namespace VividFrame
{
    internal enum FocusMode
    {
        Auto = 0,
        ScreenCentre = 1,
        Manual = 2
    }

    /// <summary>
    /// Tracks a depth-of-field focus distance, preferring avatar heads in view.
    /// Scans are throttled; the output is smoothed every frame.
    /// </summary>
    internal sealed class AutoFocus
    {
        private const float ScanInterval = 0.15f;
        private const float AnimatorRefresh = 2.0f;
        private const float SmoothSpeed = 7f;
        private const float FirstPersonCutoff = 0.55f;
        private const float MaxRayDistance = 80f;
        private const float FallbackHeadHeight = 1.6f;

        private readonly MelonLogger.Instance _log;
        private Animator[] _animators = Array.Empty<Animator>();
        private Camera _camera;
        private float _nextScan;
        private float _nextAnimatorRefresh;
        private float _nextCameraRefresh;
        private float _target = 10f;
        private float _current = 10f;
        private int _statusKey = int.MinValue;
        private int _statusMode = int.MinValue;
        private bool _loggedBoneFallback;
        private bool _boneApiMissing;
        private bool _loggedError;

        public AutoFocus(MelonLogger.Instance log)
        {
            _log = log;
        }

        public float Current => _current;
        public string Status { get; private set; } = "Manual";

        public void Tick(Settings settings)
        {
            if (settings == null || !settings.DepthOfField.Value)
                return;

            try
            {
                var mode = (FocusMode)settings.FocusMode.Value;
                if (mode == FocusMode.Manual)
                {
                    _current = Mathf.Max(0.1f, settings.DofFocusDistance.Value);
                    _target = _current;
                    SetStatus((int)mode, "Manual ", _current);
                    return;
                }

                float now = Time.unscaledTime;
                if (now >= _nextScan)
                {
                    _nextScan = now + ScanInterval;
                    Camera camera = GetCamera();
                    if (camera != null)
                    {
                        float measured = mode == FocusMode.ScreenCentre
                            ? MeasureScreenCentre(camera)
                            : MeasureNearestHead(camera);
                        if (measured > 0f)
                            _target = measured;
                    }
                }

                float dt = Time.unscaledDeltaTime;
                float t = 1f - Mathf.Exp(-SmoothSpeed * Mathf.Max(dt, 0f));
                _current = Mathf.Lerp(_current, _target, t);
                SetStatus((int)mode, mode == FocusMode.ScreenCentre ? "Centre " : "Avatar ", _current);
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    _log?.Error("Auto-focus failed and will keep the last distance: {0}", ex);
                }
            }
        }

        public void Reset(float distance)
        {
            _current = distance;
            _target = distance;
        }

        private float MeasureNearestHead(Camera camera)
        {
            RefreshAnimatorsIfNeeded();
            Vector3 camPos = camera.transform.position;
            float bestScore = float.MaxValue;
            float bestDistance = -1f;
            int seen = 0;

            for (int i = 0; i < _animators.Length; i++)
            {
                Animator animator = _animators[i];
                if (animator == null || !animator.isActiveAndEnabled)
                    continue;
                if (!TryGetHeadPosition(animator, out Vector3 head))
                    continue;

                Vector3 view = camera.WorldToViewportPoint(head);
                if (view.z < FirstPersonCutoff)
                    continue;

                float worldDistance = Vector3.Distance(camPos, head);
                if (worldDistance < FirstPersonCutoff)
                    continue;

                bool onScreen = view.x > -0.05f && view.x < 1.05f && view.y > -0.05f && view.y < 1.05f;
                if (!onScreen)
                    continue;

                seen++;
                float dx = view.x - 0.5f;
                float dy = view.y - 0.5f;
                float score = dx * dx + dy * dy;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestDistance = worldDistance;
                }
            }

            if (bestDistance > 0f)
                return bestDistance;

            Status = seen == 0 ? "No avatar in view" : Status;
            return -1f;
        }

        private float MeasureScreenCentre(Camera camera)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDistance))
            {
                float distance = hit.distance;
                if (distance < camera.nearClipPlane + 0.05f)
                    distance = camera.nearClipPlane + 0.05f;
                return distance;
            }

            return -1f;
        }

        private bool TryGetHeadPosition(Animator animator, out Vector3 head)
        {
            head = default;
            try
            {
                if (!_boneApiMissing)
                {
                    bool human = false;
                    try
                    {
                        human = animator.isHuman;
                    }
                    catch
                    {
                        human = false;
                    }

                    if (human)
                    {
                        Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
                        if (bone != null)
                        {
                            head = bone.position;
                            return true;
                        }
                    }
                }
            }
            catch (MissingMethodException ex)
            {
                _boneApiMissing = true;
                if (!_loggedBoneFallback)
                {
                    _loggedBoneFallback = true;
                    _log?.Warning("Animator.GetBoneTransform is unavailable; focusing on avatar roots instead. {0}", ex.Message);
                }
            }
            catch
            {
                // Fall through to the root estimate.
            }

            try
            {
                Transform root = null;
                try
                {
                    root = animator.avatarRoot;
                }
                catch
                {
                    root = null;
                }
                if (root == null)
                    root = animator.transform;
                if (root == null)
                    return false;
                head = root.position + Vector3.up * FallbackHeadHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshAnimatorsIfNeeded()
        {
            float now = Time.unscaledTime;
            if (now < _nextAnimatorRefresh && _animators != null && _animators.Length > 0)
                return;
            _nextAnimatorRefresh = now + AnimatorRefresh;
            try
            {
                var found = UnityEngine.Object.FindObjectsByType<Animator>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                if (found == null)
                {
                    _animators = Array.Empty<Animator>();
                    return;
                }

                var list = new Animator[found.Count];
                int n = 0;
                for (int i = 0; i < found.Count; i++)
                {
                    Animator animator = found[i];
                    if (animator != null)
                        list[n++] = animator;
                }
                if (n != list.Length)
                {
                    var trim = new Animator[n];
                    Array.Copy(list, trim, n);
                    _animators = trim;
                }
                else
                {
                    _animators = list;
                }
            }
            catch (Exception ex)
            {
                _animators = Array.Empty<Animator>();
                if (!_loggedError)
                {
                    _loggedError = true;
                    _log?.Error("Could not list Animators for auto-focus: {0}", ex);
                }
            }
        }

        private void SetStatus(int mode, string prefix, float distance)
        {
            int key = Mathf.RoundToInt(distance * 10f);
            if (key == _statusKey && mode == _statusMode)
                return;
            _statusKey = key;
            _statusMode = mode;
            Status = prefix + distance.ToString("0.0") + " m";
        }

        private Camera GetCamera()
        {
            float now = Time.unscaledTime;
            if (_camera != null && now < _nextCameraRefresh && IsUsable(_camera))
                return _camera;
            _nextCameraRefresh = now + 2f;
            _camera = FindCamera();
            return _camera;
        }

        private static Camera FindCamera()
        {
            try
            {
                Camera main = Camera.main;
                if (IsUsable(main))
                    return main;
            }
            catch
            {
                // Fall through to a manual search.
            }

            try
            {
                var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                if (cameras == null)
                    return null;

                Camera best = null;
                float bestDepth = float.NegativeInfinity;
                for (int i = 0; i < cameras.Count; i++)
                {
                    Camera camera = cameras[i];
                    if (!IsUsable(camera))
                        continue;
                    string name = camera.name ?? string.Empty;
                    if (ContainsIgnoreCase(name, "preview") || ContainsIgnoreCase(name, "UICamera"))
                        continue;
                    if (camera.depth >= bestDepth)
                    {
                        bestDepth = camera.depth;
                        best = camera;
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUsable(Camera camera)
        {
            return camera != null && camera.enabled && camera.gameObject != null && camera.gameObject.activeInHierarchy;
        }

        private static bool ContainsIgnoreCase(string value, string part)
        {
            return value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
