using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace VividFrame
{
    /// <summary>
    /// Owns the high-priority global Post Process Stack v2 volume and applies settings onto it.
    /// </summary>
    internal sealed class GraphicsVolume
    {
        public const float VolumePriority = 10000f;
        private const string ObjectName = "VividFrameVolume";

        private readonly Settings _settings;
        private readonly MelonLogger.Instance _log;
        private readonly AutoFocus _autoFocus;

        // Auto-focus keeps faces in a wider sharp zone than a cinematic portrait rig would.
        private const float AutoApertureMin = 8f;
        private const float AutoApertureMax = 16f;
        private const float AutoFocalMin = 24f;
        private const float AutoFocalMax = 50f;
        private GameObject _go;
        private PostProcessVolume _volume;
        private PostProcessProfile _profile;
        private float _lastLoggedMissingLayers;
        private bool _warnedMissingLayer;
        private int _lastLoggedStereoLayers = -1;
        private bool _didResizeBuffers;
        private float _lastAppliedScale = 1f;

        public GraphicsVolume(Settings settings, MelonLogger.Instance log, AutoFocus autoFocus)
        {
            _settings = settings;
            _log = log;
            _autoFocus = autoFocus;
        }

        public void Ensure()
        {
            try
            {
                if (_go == null || _volume == null || _profile == null)
                    Create();

                BindLayers();
            }
            catch (Exception ex)
            {
                _log.Error("Ensure failed: {0}", ex);
            }
        }

        public void Teardown()
        {
            RestoreRenderScale();
            if (_go != null)
                UnityEngine.Object.Destroy(_go);
            _go = null;
            _volume = null;
            _profile = null;
        }

        private void Create()
        {
            if (_go != null)
                UnityEngine.Object.Destroy(_go);

            _go = new GameObject(ObjectName);
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            _go.layer = 0;

            _profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            _profile.name = "VividFrameProfile";
            _profile.hideFlags = HideFlags.DontSave;

            _volume = _go.AddComponent<PostProcessVolume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.weight = 1f;
            _volume.blendDistance = 0f;
            _volume.sharedProfile = _profile;

            _log.Msg("Created global PostProcessVolume (priority {0}).", VolumePriority);
        }

        public void Apply()
        {
            if (_volume == null || _profile == null)
                return;

            try
            {
                bool on = _settings.MasterEnabled.Value && !_settings.CompareHeld;
                _volume.enabled = on;
                _volume.weight = on ? 1f : 0f;
                _volume.priority = VolumePriority;
                _volume.isGlobal = true;
                if (_volume.sharedProfile != _profile)
                    _volume.sharedProfile = _profile;

                if (!on)
                {
                    RestoreRenderScale();
                    return;
                }

                ApplyColorGrading();
                ApplyToggleEffect(_settings.MotionBlur.Value, ApplyMotionBlur, Remove<MotionBlur>);
                ApplyToggleEffect(_settings.Bloom.Value, ApplyBloom, Remove<Bloom>);
                ApplyToggleEffect(_settings.Vignette.Value, ApplyVignette, Remove<Vignette>);
                ApplyToggleEffect(_settings.Grain.Value, ApplyGrain, Remove<Grain>);
                ApplyToggleEffect(_settings.ChromaticAberration.Value, ApplyChromaticAberration, Remove<ChromaticAberration>);
                ApplyToggleEffect(_settings.AmbientOcclusion.Value, ApplyAmbientOcclusion, Remove<AmbientOcclusion>);
                ApplyToggleEffect(_settings.DepthOfField.Value, ApplyDepthOfField, Remove<DepthOfField>);
                ApplyToggleEffect(_settings.LensDistortion.Value, ApplyLensDistortion, Remove<LensDistortion>);
                ApplyRenderScale();

                _profile.isDirty = true;
            }
            catch (Exception ex)
            {
                _log.Error("Failed to apply graphics settings: {0}", ex);
            }
        }

        private static void ApplyToggleEffect(bool enabled, Action apply, Action remove)
        {
            if (enabled) apply();
            else remove();
        }

        private void ApplyColorGrading()
        {
            if (!_settings.HasColorGradingOverrides())
            {
                Remove<ColorGrading>();
                return;
            }

            ColorGrading cg = GetOrAdd<ColorGrading>();
            cg.active = true;
            SetBool(cg.enabled, true);

            _settings.GetGradingPush(out float saturation, out float contrast);
            SetFloat(cg.saturation, saturation);
            SetFloat(cg.contrast, contrast);
            SetFloat(cg.postExposure, _settings.Brightness.Value);
            // Leave LDR brightness alone so it cannot shift midtones toward orange.
            if (cg.brightness != null)
                cg.brightness.overrideState = false;
            SetFloat(cg.temperature, _settings.Temperature.Value);
            SetFloat(cg.tint, _settings.Tint.Value);
            SetFloat(cg.hueShift, _settings.HueShift.Value);

            // Trackball gamma: default is (1,1,1,0). A user gamma of 1 is identity.
            float gammaDelta = _settings.Gamma.Value - 1f;
            if (Math.Abs(gammaDelta) > 0.001f)
                SetVector(cg.gamma, new Vector4(1f, 1f, 1f, gammaDelta));
            else if (cg.gamma != null)
                cg.gamma.overrideState = false;

            // Gentle contrast S-curve via lift/gain so midtones (skin) stay natural.
            float curve = Mathf.Clamp(contrast / 100f, 0f, 1f);
            if (curve > 0.01f)
            {
                SetVector(cg.lift, new Vector4(1f, 1f, 1f, -0.025f * curve));
                SetVector(cg.gain, new Vector4(1f, 1f, 1f, 0.030f * curve));
            }
            else
            {
                if (cg.lift != null) cg.lift.overrideState = false;
                if (cg.gain != null) cg.gain.overrideState = false;
            }

            var option = (TonemapOption)_settings.Tonemapper.Value;
            if (option == TonemapOption.WorldDefault)
            {
                if (cg.tonemapper != null)
                    cg.tonemapper.overrideState = false;
            }
            else
            {
                Tonemapper mapper = option switch
                {
                    TonemapOption.None => UnityEngine.Rendering.PostProcessing.Tonemapper.None,
                    TonemapOption.Neutral => UnityEngine.Rendering.PostProcessing.Tonemapper.Neutral,
                    TonemapOption.Aces => UnityEngine.Rendering.PostProcessing.Tonemapper.ACES,
                    _ => UnityEngine.Rendering.PostProcessing.Tonemapper.None
                };
                if (cg.tonemapper != null)
                {
                    cg.tonemapper.overrideState = true;
                    cg.tonemapper.value = mapper;
                }
            }
        }

        private void ApplyMotionBlur()
        {
            MotionBlur blur = GetOrAdd<MotionBlur>();
            blur.active = true;
            SetBool(blur.enabled, true);
            SetFloat(blur.shutterAngle, Mathf.Clamp(_settings.MotionBlurShutter.Value, 0f, 360f));
            SetInt(blur.sampleCount, Mathf.Clamp(_settings.MotionBlurSamples.Value, 4, 32));
        }

        private void ApplyBloom()
        {
            Bloom bloom = GetOrAdd<Bloom>();
            bloom.active = true;
            SetBool(bloom.enabled, true);
            SetFloat(bloom.intensity, Mathf.Max(0f, _settings.BloomIntensity.Value));
            SetFloat(bloom.threshold, Mathf.Max(0f, _settings.BloomThreshold.Value));
            SetFloat(bloom.softKnee, Mathf.Clamp01(_settings.BloomSoftKnee.Value));
            SetFloat(bloom.diffusion, Mathf.Clamp(_settings.BloomDiffusion.Value, 1f, 10f));
        }

        private void ApplyVignette()
        {
            Vignette vignette = GetOrAdd<Vignette>();
            vignette.active = true;
            SetBool(vignette.enabled, true);
            if (vignette.mode != null)
            {
                vignette.mode.overrideState = true;
                vignette.mode.value = VignetteMode.Classic;
            }
            SetFloat(vignette.intensity, Mathf.Clamp01(_settings.VignetteIntensity.Value));
            SetFloat(vignette.smoothness, Mathf.Clamp(_settings.VignetteSmoothness.Value, 0.01f, 1f));
            SetFloat(vignette.roundness, Mathf.Clamp01(_settings.VignetteRoundness.Value));
        }

        private void ApplyGrain()
        {
            Grain grain = GetOrAdd<Grain>();
            grain.active = true;
            SetBool(grain.enabled, true);
            SetFloat(grain.intensity, Mathf.Clamp01(_settings.GrainIntensity.Value));
            SetFloat(grain.size, Mathf.Clamp(_settings.GrainSize.Value, 0.3f, 3f));
            SetBool(grain.colored, _settings.GrainColored.Value);
        }

        private void ApplyChromaticAberration()
        {
            ChromaticAberration ca = GetOrAdd<ChromaticAberration>();
            ca.active = true;
            SetBool(ca.enabled, true);
            SetFloat(ca.intensity, Mathf.Clamp01(_settings.ChromaticAberrationIntensity.Value));
        }

        private void ApplyAmbientOcclusion()
        {
            AmbientOcclusion ao = GetOrAdd<AmbientOcclusion>();
            ao.active = true;
            SetBool(ao.enabled, true);
            SetFloat(ao.intensity, Mathf.Max(0f, _settings.AoIntensity.Value));
            SetFloat(ao.radius, Mathf.Max(0.01f, _settings.AoRadius.Value));
            if (ao.mode != null)
            {
                ao.mode.overrideState = true;
                ao.mode.value = _settings.AoMode.Value == (int)AoModeOption.MultiScale
                    ? AmbientOcclusionMode.MultiScaleVolumetricObscurance
                    : AmbientOcclusionMode.ScalableAmbientObscurance;
            }
            if (ao.quality != null)
            {
                ao.quality.overrideState = true;
                ao.quality.value = AmbientOcclusionQuality.Medium;
            }
        }

        private void ApplyDepthOfField()
        {
            DepthOfField dof = GetOrAdd<DepthOfField>();
            dof.active = true;
            SetBool(dof.enabled, true);

            var mode = (FocusMode)_settings.FocusMode.Value;
            float distance = mode == FocusMode.Manual || _autoFocus == null
                ? _settings.DofFocusDistance.Value
                : _autoFocus.Current;
            SetFloat(dof.focusDistance, Mathf.Max(0.1f, distance));

            float aperture = _settings.DofAperture.Value;
            float focal = _settings.DofFocalLength.Value;
            if (mode != FocusMode.Manual)
            {
                // Wider in-focus range around the subject so faces stay sharp.
                aperture = Mathf.Clamp(aperture, AutoApertureMin, AutoApertureMax);
                focal = Mathf.Clamp(focal, AutoFocalMin, AutoFocalMax);
            }
            else
            {
                aperture = Mathf.Clamp(aperture, 0.05f, 32f);
                focal = Mathf.Clamp(focal, 1f, 300f);
            }
            SetFloat(dof.aperture, aperture);
            SetFloat(dof.focalLength, focal);
            if (dof.kernelSize != null)
            {
                int size = Mathf.Clamp(_settings.DofKernelSize.Value, 0, 3);
                dof.kernelSize.overrideState = true;
                dof.kernelSize.value = (KernelSize)size;
            }
        }

        private void ApplyLensDistortion()
        {
            LensDistortion distortion = GetOrAdd<LensDistortion>();
            distortion.active = true;
            SetBool(distortion.enabled, true);
            SetFloat(distortion.intensity, Mathf.Clamp(_settings.LensDistortionIntensity.Value, -100f, 100f));
        }

        private void ApplyRenderScale()
        {
            // Default is 1: do not touch ScalableBufferManager unless the user changes it.
            float scale = Mathf.Clamp(_settings.RenderScale.Value, 0.5f, 1.5f);
            if (Math.Abs(scale - 1f) < 0.001f)
            {
                RestoreRenderScale();
                return;
            }

            if (Math.Abs(_lastAppliedScale - scale) < 0.001f)
                return;

            try
            {
                ScalableBufferManager.ResizeBuffers(scale, scale);
                _didResizeBuffers = true;
                _lastAppliedScale = scale;
            }
            catch (Exception ex)
            {
                _log.Error("ResizeBuffers({0}) failed: {1}", scale, ex);
            }
        }

        private void RestoreRenderScale()
        {
            if (!_didResizeBuffers && Math.Abs(_lastAppliedScale - 1f) < 0.001f)
                return;

            try
            {
                ScalableBufferManager.ResizeBuffers(1f, 1f);
            }
            catch (Exception ex)
            {
                _log.Error("ResizeBuffers(1) restore failed: {0}", ex);
            }

            _didResizeBuffers = false;
            _lastAppliedScale = 1f;
        }

        private void BindLayers()
        {
            try
            {
            var layers = UnityEngine.Object.FindObjectsByType<PostProcessLayer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int found = 0;
            int stereoFound = 0;
            int volumeBit = 1 << (_go != null ? _go.layer : 0);

            if (layers != null)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    PostProcessLayer layer = layers[i];
                    if (layer == null)
                        continue;

                    found++;
                    Camera camera = layer.GetComponent<Camera>();
                    if (camera != null && camera.stereoEnabled)
                        stereoFound++;
                    int mask = layer.volumeLayer;
                    if ((mask & volumeBit) == 0)
                        layer.volumeLayer = mask | volumeBit;
                }
            }

            if (VrSupport.Active && stereoFound != _lastLoggedStereoLayers)
            {
                _lastLoggedStereoLayers = stereoFound;
                if (stereoFound > 0)
                    _log.Msg("Bound VividFrame's shared post-processing profile to {0} stereo camera layer(s); both eyes use the same settings.", stereoFound);
                else
                    _log.Warning("VR is active but no stereo PostProcessLayer was found; VividFrame visual effects may be unavailable in the headset.");
            }

            if (found == 0)
            {
                float now = Time.unscaledTime;
                if (!_warnedMissingLayer || now - _lastLoggedMissingLayers > 30f)
                {
                    _log.Warning("No PostProcessLayer found on loaded cameras. VividFrame's volume cannot run until a world or camera has one.");
                    _warnedMissingLayer = true;
                    _lastLoggedMissingLayers = now;
                }
            }
            }
            catch (Exception ex)
            {
                _log.Error("Failed to bind PostProcessLayer masks: {0}", ex);
            }
        }

        private T GetOrAdd<T>() where T : PostProcessEffectSettings
        {
            if (_profile.HasSettings<T>())
                return _profile.GetSetting<T>();
            return _profile.AddSettings<T>();
        }

        private void Remove<T>() where T : PostProcessEffectSettings
        {
            if (_profile != null && _profile.HasSettings<T>())
                _profile.RemoveSettings<T>();
        }

        private static void SetFloat(FloatParameter parameter, float value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SetInt(IntParameter parameter, int value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SetBool(BoolParameter parameter, bool value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SetVector(Vector4Parameter parameter, Vector4 value)
        {
            if (parameter == null)
                return;
            parameter.overrideState = true;
            parameter.value = value;
        }
    }
}
