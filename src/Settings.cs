using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;

namespace VividFrame
{
    internal enum TonemapOption
    {
        WorldDefault = 0,
        None = 1,
        Neutral = 2,
        Aces = 3
    }

    internal enum AoModeOption
    {
        Scalable = 0,
        MultiScale = 1
    }

    /// <summary>
    /// MelonPreferences-backed graphics settings and the built-in presets.
    /// </summary>
    internal sealed class Settings
    {
        private MelonPreferences_Category _category;
        private MelonPreferences_Entry<bool> _menuKeyMigratedFromF8;

        public MelonPreferences_Entry<bool> MasterEnabled;
        public MelonPreferences_Entry<string> MenuKey;

        public MelonPreferences_Entry<float> Saturation;
        public MelonPreferences_Entry<float> Vibrance;
        public MelonPreferences_Entry<float> Contrast;
        public MelonPreferences_Entry<float> Brightness;
        public MelonPreferences_Entry<float> Gamma;
        public MelonPreferences_Entry<float> Temperature;
        public MelonPreferences_Entry<float> Tint;
        public MelonPreferences_Entry<float> HueShift;
        public MelonPreferences_Entry<float> Sharpen;
        public MelonPreferences_Entry<int> Tonemapper;

        public MelonPreferences_Entry<bool> MotionBlur;
        public MelonPreferences_Entry<float> MotionBlurShutter;
        public MelonPreferences_Entry<int> MotionBlurSamples;

        public MelonPreferences_Entry<bool> Bloom;
        public MelonPreferences_Entry<float> BloomIntensity;
        public MelonPreferences_Entry<float> BloomThreshold;
        public MelonPreferences_Entry<float> BloomSoftKnee;
        public MelonPreferences_Entry<float> BloomDiffusion;

        public MelonPreferences_Entry<bool> Vignette;
        public MelonPreferences_Entry<float> VignetteIntensity;
        public MelonPreferences_Entry<float> VignetteSmoothness;
        public MelonPreferences_Entry<float> VignetteRoundness;

        public MelonPreferences_Entry<bool> Grain;
        public MelonPreferences_Entry<float> GrainIntensity;
        public MelonPreferences_Entry<float> GrainSize;
        public MelonPreferences_Entry<bool> GrainColored;

        public MelonPreferences_Entry<bool> ChromaticAberration;
        public MelonPreferences_Entry<float> ChromaticAberrationIntensity;

        public MelonPreferences_Entry<bool> AmbientOcclusion;
        public MelonPreferences_Entry<float> AoIntensity;
        public MelonPreferences_Entry<float> AoRadius;
        public MelonPreferences_Entry<int> AoMode;

        public MelonPreferences_Entry<bool> DepthOfField;
        public MelonPreferences_Entry<float> DofFocusDistance;
        public MelonPreferences_Entry<float> DofAperture;
        public MelonPreferences_Entry<float> DofFocalLength;
        public MelonPreferences_Entry<int> DofKernelSize;
        public MelonPreferences_Entry<int> FocusMode;

        public MelonPreferences_Entry<bool> LensDistortion;
        public MelonPreferences_Entry<float> LensDistortionIntensity;

        public MelonPreferences_Entry<float> RenderScale;
        public MelonPreferences_Entry<bool> AnimationsEnabled;
        public MelonPreferences_Entry<float> AnimationSpeed;
        public MelonPreferences_Entry<bool> LowCostUi;
        public MelonPreferences_Entry<float> UiScale;

        public MelonPreferences_Entry<float> GlassOpacity;
        public MelonPreferences_Entry<int> AccentIndex;
        public MelonPreferences_Entry<bool> FrostBlur;
        public MelonPreferences_Entry<string> CompareKey;
        public MelonPreferences_Entry<string> MasterKey;
        public MelonPreferences_Entry<string> ProfileKey;
        public MelonPreferences_Entry<bool> AutoProfiles;
        public MelonPreferences_Entry<string> Favourites;
        public MelonPreferences_Entry<bool> AutoUpdate;
        public MelonPreferences_Entry<float> VrPanelSize;
        public MelonPreferences_Entry<bool> VrFollow;
        public MelonPreferences_Entry<bool> QuickMenuEnabled;

        public MelonPreferences_Entry<bool> FpsOverlay;
        public MelonPreferences_Entry<int> FpsCorner;
        public MelonPreferences_Entry<bool> Crosshair;
        public MelonPreferences_Entry<int> CrosshairStyle;
        public MelonPreferences_Entry<float> CrosshairSize;
        public MelonPreferences_Entry<float> CrosshairGap;
        public MelonPreferences_Entry<float> CrosshairOpacity;
        public MelonPreferences_Entry<bool> FovOverride;
        public MelonPreferences_Entry<float> Fov;
        public MelonPreferences_Entry<bool> FovResetInMenus;
        public MelonPreferences_Entry<bool> QualityOverride;
        public MelonPreferences_Entry<int> FpsCap;
        public MelonPreferences_Entry<bool> VSync;
        public MelonPreferences_Entry<float> LodBias;
        public MelonPreferences_Entry<float> ShadowDistance;
        public MelonPreferences_Entry<bool> Anisotropic;
        public MelonPreferences_Entry<int> MsaaIndex;
        public MelonPreferences_Entry<string> ScreenshotKey;

        public MelonPreferences_Entry<bool> StatClock, StatTimer, StatPosition, StatRam, StatPlayers;
        public MelonPreferences_Entry<int> StatsCorner;
        public MelonPreferences_Entry<bool> CinemaBars;
        public MelonPreferences_Entry<float> CinemaThickness, CinemaOpacity;
        public MelonPreferences_Entry<string> ZoomKey;
        public MelonPreferences_Entry<float> ZoomFov;
        public MelonPreferences_Entry<bool> NearClipOverride, FarClipOverride;
        public MelonPreferences_Entry<float> NearClip, FarClip;
        public MelonPreferences_Entry<int> ScreenshotScale;
        public MelonPreferences_Entry<int> TextureLimit, ShadowMode, ShadowRes, ShadowCascades, PixelLights, SkinWeights;
        public MelonPreferences_Entry<bool> SoftParticles, ReflectionProbes;
        public MelonPreferences_Entry<bool> RunInBackground, MuteUnfocused, VolumeOverride, LoadPriorityOverride;
        public MelonPreferences_Entry<float> MasterVolume;
        public MelonPreferences_Entry<int> LoadPriority;
        public MelonPreferences_Entry<bool> FogOff, AmbientOverride;
        public MelonPreferences_Entry<float> AmbientIntensity;

        /// <summary>True while the "hold to compare" key is down. Not persisted.</summary>
        public bool CompareHeld;

        public void Register()
        {
            _category = MelonPreferences.CreateCategory("VividFrame", "VividFrame");

            MasterEnabled = Entry("MasterEnabled", true, "Master enabled", "When off, VividFrame's volume is disabled and the world's original look is restored.");
            MenuKey = Entry("MenuKey", "Home", "Menu key", "Unity KeyCode name used to toggle the in-game menu.");
            _menuKeyMigratedFromF8 = _category.CreateEntry(
                "MenuKeyMigratedFromF8",
                false,
                "Menu key migrated from F8",
                "Internal flag: the old F8 default was moved to Home once.",
                is_hidden: true);
            MigrateLegacyMenuKey();

            Saturation = Entry("Saturation", 0f, "Saturation", "ColorGrading.saturation, -100 to 100.");
            Vibrance = Entry("Vibrance", 0f, "Vibrance", "Approximated through ColorGrading; not a native effect.");
            Contrast = Entry("Contrast", 0f, "Contrast", "ColorGrading.contrast, -100 to 100.");
            Brightness = Entry("Brightness", 0f, "Brightness / exposure", "Written to postExposure.");
            Gamma = Entry("Gamma", 1f, "Gamma", "1 is identity. Mapped onto ColorGrading.gamma.");
            Temperature = Entry("Temperature", 0f, "Temperature", "ColorGrading.temperature, -100 to 100. 0 is neutral white.");
            Tint = Entry("Tint", 0f, "Tint", "ColorGrading.tint, -100 to 100.");
            HueShift = Entry("HueShift", 0f, "Hue shift", "ColorGrading.hueShift, -180 to 180.");
            Sharpen = Entry("Sharpen", 0f, "Sharpen", "Midtone-contrast approximation; not an unsharp mask.");
            Tonemapper = Entry("Tonemapper", (int)TonemapOption.WorldDefault, "Tonemapper", "0 = world default, 1 = None, 2 = Neutral, 3 = ACES.");

            MotionBlur = Entry("MotionBlur", false, "Motion blur", "Enables the MotionBlur effect.");
            MotionBlurShutter = Entry("MotionBlurShutter", 90f, "Shutter angle", "MotionBlur.shutterAngle. Lower values smear faces less. Default 90.");
            MotionBlurSamples = Entry("MotionBlurSamples", 8, "Motion blur samples", "MotionBlur.sampleCount.");

            Bloom = Entry("Bloom", false, "Bloom", "Enables the Bloom effect.");
            BloomIntensity = Entry("BloomIntensity", 0.15f, "Bloom intensity", "Bloom.intensity.");
            BloomThreshold = Entry("BloomThreshold", 1.15f, "Bloom threshold", "Bloom.threshold.");
            BloomSoftKnee = Entry("BloomSoftKnee", 0.5f, "Bloom soft knee", "Bloom.softKnee.");
            BloomDiffusion = Entry("BloomDiffusion", 6f, "Bloom diffusion", "Bloom.diffusion.");

            Vignette = Entry("Vignette", false, "Vignette", "Enables the Vignette effect.");
            VignetteIntensity = Entry("VignetteIntensity", 0.12f, "Vignette intensity", "Vignette.intensity.");
            VignetteSmoothness = Entry("VignetteSmoothness", 0.45f, "Vignette smoothness", "Vignette.smoothness.");
            VignetteRoundness = Entry("VignetteRoundness", 1f, "Vignette roundness", "Vignette.roundness.");

            Grain = Entry("Grain", false, "Film grain", "Enables the Grain effect.");
            GrainIntensity = Entry("GrainIntensity", 0.08f, "Grain intensity", "Grain.intensity.");
            GrainSize = Entry("GrainSize", 0.8f, "Grain size", "Grain.size.");
            GrainColored = Entry("GrainColored", false, "Colored grain", "Grain.colored.");

            ChromaticAberration = Entry("ChromaticAberration", false, "Chromatic aberration", "Enables ChromaticAberration.");
            ChromaticAberrationIntensity = Entry("ChromaticAberrationIntensity", 0.06f, "CA intensity", "ChromaticAberration.intensity.");

            AmbientOcclusion = Entry("AmbientOcclusion", false, "Ambient occlusion", "Enables AmbientOcclusion at medium quality.");
            AoIntensity = Entry("AoIntensity", 0.35f, "AO intensity", "AmbientOcclusion.intensity.");
            AoRadius = Entry("AoRadius", 0.25f, "AO radius", "AmbientOcclusion.radius.");
            AoMode = Entry("AoMode", (int)AoModeOption.Scalable, "AO mode", "0 = Scalable AO, 1 = Multi-scale VO.");

            DepthOfField = Entry("DepthOfField", false, "Depth of field", "Enables DepthOfField with optional auto-focus on avatar heads.");
            DofFocusDistance = Entry("DofFocusDistance", 3f, "DoF focus distance", "Used in Manual focus mode.");
            DofAperture = Entry("DofAperture", 11f, "DoF aperture", "Higher values keep faces sharper. Auto-focus clamps this.");
            DofFocalLength = Entry("DofFocalLength", 35f, "DoF focal length", "Shorter values give a wider in-focus range. Auto-focus clamps this.");
            DofKernelSize = Entry("DofKernelSize", 1, "DoF kernel size", "0 Small, 1 Medium, 2 Large, 3 VeryLarge.");
            FocusMode = Entry("FocusMode", (int)VividFrame.FocusMode.Auto, "Focus mode", "0 = Auto (avatars/faces), 1 = Screen centre, 2 = Manual.");

            LensDistortion = Entry("LensDistortion", false, "Lens distortion", "Enables LensDistortion.");
            LensDistortionIntensity = Entry("LensDistortionIntensity", 0f, "Lens distortion intensity", "LensDistortion.intensity.");

            RenderScale = Entry("RenderScale", 1f, "Render scale", "ScalableBufferManager width/height scale. Does not change FOV.");
            AnimationsEnabled = Entry("AnimationsEnabled", true, "Animations", "Play menu motion. When off, transitions are instant.");
            AnimationSpeed = Entry("AnimationSpeed", 1f, "Animation speed", "Multiplies animation speed. 1 is the default.");
            LowCostUi = Entry("LowCostUi", false, "Low-cost UI", "Disables menu shadows and animations to cut IMGUI cost.");
            UiScale = Entry("UiScale", 1f, "UI scale", "Extra menu scale on top of the 1080p screen scale. 1 is the default.");

            GlassOpacity = Entry("GlassOpacity", 1f, "Glass opacity", "How solid the menu glass is, 0.45 to 1.");
            AccentIndex = Entry("AccentIndex", 0, "Accent colour", "0 White, 1 Ice, 2 Violet, 3 Rose, 4 Mint.");
            FrostBlur = Entry("FrostBlur", false, "Frosted blur", "Blur a snapshot of the screen behind the menu. Takes the snapshot when the menu opens.");
            CompareKey = Entry("CompareKey", "None", "Compare key", "Hold to temporarily show the world without VividFrame.");
            MasterKey = Entry("MasterKey", "None", "Master key", "Press to toggle VividFrame on or off.");
            ProfileKey = Entry("ProfileKey", "None", "Profile key", "Press to load the next saved profile.");
            AutoProfiles = Entry("AutoProfiles", true, "World profiles", "Load the profile pinned to a world when you join it.");
            Favourites = Entry("Favourites", "", "Favourites", "Comma-separated list of pinned controls.");
            AutoUpdate = Entry("AutoUpdate", true, "Auto update", "Download new VividFrame releases from GitHub and install them when the game closes.");
            VrPanelSize = Entry("VrPanelSize", 1f, "VR panel size", "Size of the VR panel, 0.7 to 1.4. 1 is about 62 cm wide.");
            VrFollow = Entry("VrFollow", true, "Keep VR panel in view", "Move the VR panel back in front of you when you turn or walk away from it.");
            QuickMenuEnabled = Entry("QuickMenuEnabled", true, "Quick Menu tab", "Add a VividFrame tab with sliders, toggles and buttons to VRChat's Quick Menu.");

            FpsOverlay = Entry("FpsOverlay", false, "FPS counter", "Show a small FPS and frame time readout.");
            FpsCorner = Entry("FpsCorner", 1, "FPS corner", "0 top left, 1 top right, 2 bottom left, 3 bottom right.");
            Crosshair = Entry("Crosshair", false, "Crosshair", "Draw a crosshair at the centre of the screen.");
            CrosshairStyle = Entry("CrosshairStyle", 0, "Crosshair style", "0 Dot, 1 Cross, 2 Plus with dot.");
            CrosshairSize = Entry("CrosshairSize", 8f, "Crosshair size", "Size in pixels at 1080p.");
            CrosshairGap = Entry("CrosshairGap", 4f, "Crosshair gap", "Gap at the centre of the cross.");
            CrosshairOpacity = Entry("CrosshairOpacity", 0.9f, "Crosshair opacity", "0 to 1.");
            FovOverride = Entry("FovOverride", false, "Override FOV", "Force the main camera's field of view.");
            Fov = Entry("Fov", 90f, "Field of view", "Vertical field of view in degrees.");
            FovResetInMenus = Entry("FovResetInMenus", true, "Reset FOV in menus", "Ease back to the normal FOV while the cursor is free, so menu buttons line up with the mouse.");
            QualityOverride = Entry("QualityOverride", false, "Override quality", "Apply the quality values below. Turning it off restores the game's own values.");
            FpsCap = Entry("FpsCap", 0, "FPS cap", "0 leaves the game's cap alone.");
            VSync = Entry("VSync", false, "VSync", "Only used while quality override is on.");
            LodBias = Entry("LodBias", 2f, "LOD bias", "Higher keeps detailed models visible for longer.");
            ShadowDistance = Entry("ShadowDistance", 60f, "Shadow distance", "How far real-time shadows are drawn.");
            Anisotropic = Entry("Anisotropic", false, "Force anisotropic filtering", "Sharper textures at an angle.");
            MsaaIndex = Entry("MsaaIndex", 0, "MSAA", "0 Off, 1 2x, 2 4x, 3 8x.");
            ScreenshotKey = Entry("ScreenshotKey", "None", "Screenshot key", "Press to save a clean screenshot.");

            StatClock = Entry("StatClock", false, "Clock overlay", "Show the system time.");
            StatTimer = Entry("StatTimer", false, "Session timer overlay", "Show how long the game has been running.");
            StatPosition = Entry("StatPosition", false, "Position overlay", "Show the camera's world position.");
            StatRam = Entry("StatRam", false, "Memory overlay", "Show the game's memory use.");
            StatPlayers = Entry("StatPlayers", false, "Players overlay", "Show how many players are in the instance.");
            StatsCorner = Entry("StatsCorner", 0, "Stats corner", "0 top left, 1 top right, 2 bottom left, 3 bottom right.");
            CinemaBars = Entry("CinemaBars", false, "Cinema bars", "Black bars at the top and bottom of the screen.");
            CinemaThickness = Entry("CinemaThickness", 0.12f, "Cinema bar size", "Bar height as a fraction of the screen.");
            CinemaOpacity = Entry("CinemaOpacity", 1f, "Cinema bar opacity", "0 to 1.");
            ZoomKey = Entry("ZoomKey", "None", "Zoom key", "Hold to zoom in.");
            ZoomFov = Entry("ZoomFov", 30f, "Zoom FOV", "Field of view while zooming.");
            var zoomDefaulted = _category.CreateEntry("ZoomKeyDefaulted", false, "Zoom key defaulted", "Internal flag.", is_hidden: true);
            if (!zoomDefaulted.Value)
            {
                zoomDefaulted.Value = true;
                if (string.IsNullOrEmpty(ZoomKey.Value) || string.Equals(ZoomKey.Value, "None", StringComparison.OrdinalIgnoreCase))
                    ZoomKey.Value = "LeftAlt";
            }
            NearClipOverride = Entry("NearClipOverride", false, "Override near clip", "Force the main camera's near clip plane.");
            NearClip = Entry("NearClip", 0.05f, "Near clip", "Metres.");
            FarClipOverride = Entry("FarClipOverride", false, "Override far clip", "Force the main camera's far clip plane.");
            FarClip = Entry("FarClip", 1000f, "Far clip", "Metres.");
            ScreenshotScale = Entry("ScreenshotScale", 1, "Screenshot scale", "1 to 4 times the screen size.");
            TextureLimit = Entry("TextureLimit", 0, "Texture quality", "0 Full, 1 Half, 2 Quarter, 3 Eighth.");
            ShadowMode = Entry("ShadowMode", 2, "Shadows", "0 Off, 1 Hard, 2 Soft.");
            ShadowRes = Entry("ShadowRes", 2, "Shadow resolution", "0 Low, 1 Medium, 2 High, 3 Very high.");
            ShadowCascades = Entry("ShadowCascades", 2, "Shadow cascades", "0 is 1 cascade, 1 is 2, 2 is 4.");
            PixelLights = Entry("PixelLights", 4, "Pixel lights", "Maximum per-pixel lights per object.");
            SkinWeights = Entry("SkinWeights", 2, "Skin weights", "0 is 1 bone, 1 is 2, 2 is 4, 3 is unlimited.");
            SoftParticles = Entry("SoftParticles", true, "Soft particles", "Fade particles where they meet surfaces.");
            ReflectionProbes = Entry("ReflectionProbes", true, "Real-time reflections", "Real-time reflection probes.");
            RunInBackground = Entry("RunInBackground", false, "Run in background", "Keep the game running when the window loses focus.");
            MuteUnfocused = Entry("MuteUnfocused", false, "Mute when unfocused", "Silence the game while its window is in the background.");
            VolumeOverride = Entry("VolumeOverride", false, "Override master volume", "Apply the master volume below.");
            MasterVolume = Entry("MasterVolume", 1f, "Master volume", "0 to 1.");
            LoadPriorityOverride = Entry("LoadPriorityOverride", false, "Override loading priority", "Change the background loading thread priority.");
            LoadPriority = Entry("LoadPriority", 2, "Loading priority", "0 Low, 1 Below normal, 2 Normal, 3 High.");
            FogOff = Entry("FogOff", false, "Disable fog", "Turn off the world's fog.");
            AmbientOverride = Entry("AmbientOverride", false, "Override ambient light", "Scale the world's ambient light.");
            AmbientIntensity = Entry("AmbientIntensity", 1f, "Ambient intensity", "Multiplier on ambient light.");

            ApplyFirstRunDefaults();
        }

        /// <summary>
        /// A brand-new install starts on Natural+ instead of an all-zero profile. Existing
        /// configs are never overwritten: only a config with no graphics changes qualifies.
        /// </summary>
        private void ApplyFirstRunDefaults()
        {
            var done = _category.CreateEntry("FirstRunDone", false, "First run done", "Internal flag.", is_hidden: true);
            if (done.Value)
                return;
            done.Value = true;
            bool untouched = !HasColorGradingOverrides() && !Bloom.Value && !Vignette.Value && !Grain.Value
                             && !MotionBlur.Value && !AmbientOcclusion.Value && !DepthOfField.Value
                             && Math.Abs(RenderScale.Value - 1f) < 0.001f;
            if (untouched)
                ApplyPresetNaturalPlus();
            else
                Save();
        }

        /// <summary>One line describing which features are on, for crash reports.</summary>
        public string StateSummary()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("master=").Append(MasterEnabled.Value ? 1 : 0);
            sb.Append(" compare=").Append(CompareHeld ? 1 : 0);
            sb.Append(" bloom=").Append(Bloom.Value ? 1 : 0);
            sb.Append(" mblur=").Append(MotionBlur.Value ? 1 : 0);
            sb.Append(" ao=").Append(AmbientOcclusion.Value ? 1 : 0);
            sb.Append(" dof=").Append(DepthOfField.Value ? 1 : 0);
            sb.Append(" lens=").Append(LensDistortion.Value ? 1 : 0);
            sb.Append(" scale=").Append(RenderScale.Value.ToString("0.00"));
            sb.Append(" | blur=").Append(FrostBlur.Value ? 1 : 0);
            sb.Append(" fov=").Append(FovOverride.Value ? 1 : 0);
            sb.Append(" clip=").Append(NearClipOverride.Value || FarClipOverride.Value ? 1 : 0);
            sb.Append(" quality=").Append(QualityOverride.Value ? 1 : 0);
            sb.Append(" bg=").Append(RunInBackground.Value ? 1 : 0);
            sb.Append(" vol=").Append(VolumeOverride.Value || MuteUnfocused.Value ? 1 : 0);
            sb.Append(" load=").Append(LoadPriorityOverride.Value ? 1 : 0);
            sb.Append(" fog=").Append(FogOff.Value ? 1 : 0);
            sb.Append(" amb=").Append(AmbientOverride.Value ? 1 : 0);
            sb.Append(" | fps=").Append(FpsOverlay.Value ? 1 : 0);
            sb.Append(" cross=").Append(Crosshair.Value ? 1 : 0);
            sb.Append(" cinema=").Append(CinemaBars.Value ? 1 : 0);
            sb.Append(" stats=").Append(StatClock.Value || StatTimer.Value || StatPosition.Value || StatRam.Value || StatPlayers.Value ? 1 : 0);
            return sb.ToString();
        }

        public void ResetColour()
        {
            Saturation.ResetToDefault();
            Vibrance.ResetToDefault();
            Contrast.ResetToDefault();
            Brightness.ResetToDefault();
            Gamma.ResetToDefault();
            Temperature.ResetToDefault();
            Tint.ResetToDefault();
            HueShift.ResetToDefault();
            Sharpen.ResetToDefault();
            Tonemapper.ResetToDefault();
        }

        private MelonPreferences_Entry<T> Entry<T>(string id, T defaultValue, string display, string description)
        {
            return _category.CreateEntry(id, defaultValue, display, description);
        }

        /// <summary>
        /// The first builds defaulted to F8. Move a leftover saved F8 to Home once,
        /// then leave later user changes (including a deliberate F8) alone.
        /// </summary>
        private void MigrateLegacyMenuKey()
        {
            if (_menuKeyMigratedFromF8.Value)
                return;
            if (MenuKey != null && string.Equals(MenuKey.Value, "F8", StringComparison.OrdinalIgnoreCase))
                MenuKey.Value = "Home";
            _menuKeyMigratedFromF8.Value = true;
            Save();
        }

        public void Save()
        {
            MelonPreferences.Save();
        }

        public void ResetToDefaults()
        {
            MasterEnabled.ResetToDefault();
            Saturation.ResetToDefault();
            Vibrance.ResetToDefault();
            Contrast.ResetToDefault();
            Brightness.ResetToDefault();
            Gamma.ResetToDefault();
            Temperature.ResetToDefault();
            Tint.ResetToDefault();
            HueShift.ResetToDefault();
            Sharpen.ResetToDefault();
            Tonemapper.ResetToDefault();
            MotionBlur.ResetToDefault();
            MotionBlurShutter.ResetToDefault();
            MotionBlurSamples.ResetToDefault();
            Bloom.ResetToDefault();
            BloomIntensity.ResetToDefault();
            BloomThreshold.ResetToDefault();
            BloomSoftKnee.ResetToDefault();
            BloomDiffusion.ResetToDefault();
            Vignette.ResetToDefault();
            VignetteIntensity.ResetToDefault();
            VignetteSmoothness.ResetToDefault();
            VignetteRoundness.ResetToDefault();
            Grain.ResetToDefault();
            GrainIntensity.ResetToDefault();
            GrainSize.ResetToDefault();
            GrainColored.ResetToDefault();
            ChromaticAberration.ResetToDefault();
            ChromaticAberrationIntensity.ResetToDefault();
            AmbientOcclusion.ResetToDefault();
            AoIntensity.ResetToDefault();
            AoRadius.ResetToDefault();
            AoMode.ResetToDefault();
            DepthOfField.ResetToDefault();
            DofFocusDistance.ResetToDefault();
            DofAperture.ResetToDefault();
            DofFocalLength.ResetToDefault();
            DofKernelSize.ResetToDefault();
            FocusMode.ResetToDefault();
            LensDistortion.ResetToDefault();
            LensDistortionIntensity.ResetToDefault();
            RenderScale.ResetToDefault();
            Save();
        }

        public void ApplyPresetVivid()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = true;
            Saturation.Value = 6f;
            Vibrance.Value = 14f;
            Contrast.Value = 10f;
            Brightness.Value = 0.04f;
            Temperature.Value = 0f;
            Tint.Value = 0f;
            Sharpen.Value = 8f;
            Tonemapper.Value = (int)TonemapOption.Neutral;
            Bloom.Value = true;
            BloomIntensity.Value = 0.14f;
            BloomThreshold.Value = 1.15f;
            Vignette.Value = true;
            VignetteIntensity.Value = 0.12f;
            AmbientOcclusion.Value = true;
            AoIntensity.Value = 0.35f;
            AoRadius.Value = 0.22f;
            Save();
        }

        public void ApplyPresetCinematic()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = true;
            Saturation.Value = 2f;
            Vibrance.Value = 8f;
            Contrast.Value = 12f;
            Temperature.Value = 0f;
            Tint.Value = 0f;
            Tonemapper.Value = (int)TonemapOption.Aces;
            Sharpen.Value = 5f;
            Bloom.Value = true;
            BloomIntensity.Value = 0.18f;
            BloomThreshold.Value = 1.08f;
            Vignette.Value = true;
            VignetteIntensity.Value = 0.20f;
            Grain.Value = true;
            GrainIntensity.Value = 0.07f;
            GrainSize.Value = 0.8f;
            GrainColored.Value = false;
            ChromaticAberration.Value = true;
            ChromaticAberrationIntensity.Value = 0.04f;
            MotionBlur.Value = true;
            MotionBlurShutter.Value = 80f;
            MotionBlurSamples.Value = 6;
            AmbientOcclusion.Value = true;
            AoIntensity.Value = 0.4f;
            DepthOfField.Value = true;
            DofAperture.Value = 11f;
            DofFocalLength.Value = 35f;
            DofKernelSize.Value = 1;
            FocusMode.Value = (int)VividFrame.FocusMode.Auto;
            Save();
        }

        public void ApplyPresetPerformance()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = true;
            Saturation.Value = 4f;
            Vibrance.Value = 8f;
            Contrast.Value = 6f;
            Temperature.Value = 0f;
            Tint.Value = 0f;
            Tonemapper.Value = (int)TonemapOption.Neutral;
            RenderScale.Value = 0.85f;
            Save();
        }

        public void ApplyPresetNaturalPlus()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = true;
            Saturation.Value = 3f;
            Vibrance.Value = 10f;
            Contrast.Value = 8f;
            Brightness.Value = 0f;
            Temperature.Value = 0f;
            Tint.Value = 0f;
            Sharpen.Value = 6f;
            Tonemapper.Value = (int)TonemapOption.Neutral;
            Bloom.Value = true;
            BloomIntensity.Value = 0.10f;
            BloomThreshold.Value = 1.2f;
            Vignette.Value = true;
            VignetteIntensity.Value = 0.08f;
            AmbientOcclusion.Value = true;
            AoIntensity.Value = 0.30f;
            AoRadius.Value = 0.22f;
            Save();
        }

        public void ApplyPresetNight()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = true;
            Saturation.Value = 2f;
            Vibrance.Value = 8f;
            Contrast.Value = 12f;
            Brightness.Value = -0.18f;
            Gamma.Value = 0.96f;
            Temperature.Value = -4f;
            Tint.Value = 0f;
            Sharpen.Value = 5f;
            Tonemapper.Value = (int)TonemapOption.Aces;
            Bloom.Value = true;
            BloomIntensity.Value = 0.22f;
            BloomThreshold.Value = 0.95f;
            Vignette.Value = true;
            VignetteIntensity.Value = 0.26f;
            Grain.Value = true;
            GrainIntensity.Value = 0.06f;
            GrainColored.Value = false;
            AmbientOcclusion.Value = true;
            AoIntensity.Value = 0.40f;
            Save();
        }

        public void ApplyPresetOff()
        {
            ResetVisualsKeepMaster();
            MasterEnabled.Value = false;
            Save();
        }

        private void ResetVisualsKeepMaster()
        {
            bool master = MasterEnabled.Value;
            string key = MenuKey.Value;
            ResetToDefaults();
            MasterEnabled.Value = master;
            MenuKey.Value = key;
        }

        /// <summary>
        /// Approximates vibrance (and sharpen) through ColorGrading, because PPSv2 has neither effect.
        /// Vibrance is mostly added to saturation, with a smaller contrast lift so muted colours pick up more punch.
        /// The mix is kept conservative so skin tones are not pushed into orange or neon.
        /// Sharpen is a midtone-contrast / clarity stand-in, not an unsharp mask.
        /// </summary>
        public void GetGradingPush(out float saturation, out float contrast)
        {
            float vibrance = Vibrance.Value;
            float sharpen = Sharpen.Value;
            saturation = Saturation.Value + vibrance * 0.70f;
            contrast = Contrast.Value + Math.Abs(vibrance) * 0.10f + sharpen * 0.35f;
        }

        public bool HasColorGradingOverrides()
        {
            GetGradingPush(out float sat, out float contrast);
            return Math.Abs(sat) > 0.001f
                   || Math.Abs(contrast) > 0.001f
                   || Math.Abs(Brightness.Value) > 0.0001f
                   || Math.Abs(Gamma.Value - 1f) > 0.001f
                   || Math.Abs(Temperature.Value) > 0.001f
                   || Math.Abs(Tint.Value) > 0.001f
                   || Math.Abs(HueShift.Value) > 0.001f
                   || Tonemapper.Value != (int)TonemapOption.WorldDefault;
        }

        public Dictionary<string, string> ExportValues()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            Put(values, "MasterEnabled", MasterEnabled);
            Put(values, "Saturation", Saturation);
            Put(values, "Vibrance", Vibrance);
            Put(values, "Contrast", Contrast);
            Put(values, "Brightness", Brightness);
            Put(values, "Gamma", Gamma);
            Put(values, "Temperature", Temperature);
            Put(values, "Tint", Tint);
            Put(values, "HueShift", HueShift);
            Put(values, "Sharpen", Sharpen);
            Put(values, "Tonemapper", Tonemapper);
            Put(values, "MotionBlur", MotionBlur);
            Put(values, "MotionBlurShutter", MotionBlurShutter);
            Put(values, "MotionBlurSamples", MotionBlurSamples);
            Put(values, "Bloom", Bloom);
            Put(values, "BloomIntensity", BloomIntensity);
            Put(values, "BloomThreshold", BloomThreshold);
            Put(values, "BloomSoftKnee", BloomSoftKnee);
            Put(values, "BloomDiffusion", BloomDiffusion);
            Put(values, "Vignette", Vignette);
            Put(values, "VignetteIntensity", VignetteIntensity);
            Put(values, "VignetteSmoothness", VignetteSmoothness);
            Put(values, "VignetteRoundness", VignetteRoundness);
            Put(values, "Grain", Grain);
            Put(values, "GrainIntensity", GrainIntensity);
            Put(values, "GrainSize", GrainSize);
            Put(values, "GrainColored", GrainColored);
            Put(values, "ChromaticAberration", ChromaticAberration);
            Put(values, "ChromaticAberrationIntensity", ChromaticAberrationIntensity);
            Put(values, "AmbientOcclusion", AmbientOcclusion);
            Put(values, "AoIntensity", AoIntensity);
            Put(values, "AoRadius", AoRadius);
            Put(values, "AoMode", AoMode);
            Put(values, "DepthOfField", DepthOfField);
            Put(values, "DofFocusDistance", DofFocusDistance);
            Put(values, "DofAperture", DofAperture);
            Put(values, "DofFocalLength", DofFocalLength);
            Put(values, "DofKernelSize", DofKernelSize);
            Put(values, "FocusMode", FocusMode);
            Put(values, "LensDistortion", LensDistortion);
            Put(values, "LensDistortionIntensity", LensDistortionIntensity);
            Put(values, "RenderScale", RenderScale);
            Put(values, "AnimationsEnabled", AnimationsEnabled);
            Put(values, "AnimationSpeed", AnimationSpeed);
            return values;
        }

        public void ImportValues(Dictionary<string, string> values)
        {
            if (values == null)
                return;
            Set(MasterEnabled, values, "MasterEnabled");
            Set(Saturation, values, "Saturation");
            Set(Vibrance, values, "Vibrance");
            Set(Contrast, values, "Contrast");
            Set(Brightness, values, "Brightness");
            Set(Gamma, values, "Gamma");
            Set(Temperature, values, "Temperature");
            Set(Tint, values, "Tint");
            Set(HueShift, values, "HueShift");
            Set(Sharpen, values, "Sharpen");
            Set(Tonemapper, values, "Tonemapper");
            Set(MotionBlur, values, "MotionBlur");
            Set(MotionBlurShutter, values, "MotionBlurShutter");
            Set(MotionBlurSamples, values, "MotionBlurSamples");
            Set(Bloom, values, "Bloom");
            Set(BloomIntensity, values, "BloomIntensity");
            Set(BloomThreshold, values, "BloomThreshold");
            Set(BloomSoftKnee, values, "BloomSoftKnee");
            Set(BloomDiffusion, values, "BloomDiffusion");
            Set(Vignette, values, "Vignette");
            Set(VignetteIntensity, values, "VignetteIntensity");
            Set(VignetteSmoothness, values, "VignetteSmoothness");
            Set(VignetteRoundness, values, "VignetteRoundness");
            Set(Grain, values, "Grain");
            Set(GrainIntensity, values, "GrainIntensity");
            Set(GrainSize, values, "GrainSize");
            Set(GrainColored, values, "GrainColored");
            Set(ChromaticAberration, values, "ChromaticAberration");
            Set(ChromaticAberrationIntensity, values, "ChromaticAberrationIntensity");
            Set(AmbientOcclusion, values, "AmbientOcclusion");
            Set(AoIntensity, values, "AoIntensity");
            Set(AoRadius, values, "AoRadius");
            Set(AoMode, values, "AoMode");
            Set(DepthOfField, values, "DepthOfField");
            Set(DofFocusDistance, values, "DofFocusDistance");
            Set(DofAperture, values, "DofAperture");
            Set(DofFocalLength, values, "DofFocalLength");
            Set(DofKernelSize, values, "DofKernelSize");
            Set(FocusMode, values, "FocusMode");
            Set(LensDistortion, values, "LensDistortion");
            Set(LensDistortionIntensity, values, "LensDistortionIntensity");
            Set(RenderScale, values, "RenderScale");
            Set(AnimationsEnabled, values, "AnimationsEnabled");
            Set(AnimationSpeed, values, "AnimationSpeed");
        }

        private static void Put<T>(Dictionary<string, string> values, string key, MelonPreferences_Entry<T> entry)
        {
            if (entry == null)
                return;
            T value = entry.Value;
            if (value is float f)
                values[key] = f.ToString("R", CultureInfo.InvariantCulture);
            else if (value is IFormattable formattable)
                values[key] = formattable.ToString(null, CultureInfo.InvariantCulture);
            else
                values[key] = value != null ? value.ToString() : string.Empty;
        }

        private static void Set<T>(MelonPreferences_Entry<T> entry, Dictionary<string, string> values, string key)
        {
            if (entry == null || values == null || !values.TryGetValue(key, out string raw) || raw == null)
                return;
            try
            {
                Type type = typeof(T);
                if (type == typeof(bool))
                    entry.Value = (T)(object)(raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase));
                else if (type == typeof(int))
                    entry.Value = (T)(object)int.Parse(raw, CultureInfo.InvariantCulture);
                else if (type == typeof(float))
                    entry.Value = (T)(object)float.Parse(raw, CultureInfo.InvariantCulture);
                else if (type == typeof(string))
                    entry.Value = (T)(object)raw;
            }
            catch
            {
                // Leave the existing value if a profile field is malformed.
            }
        }
    }
}
