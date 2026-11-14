using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace VividFrame
{
    internal enum Shape
    {
        Panel, Popup, Bar, Pill, PillHover, PillActive, Field, Track, Fill, SwitchOff, SwitchOn, Knob, Shadow
    }

    /// <summary>
    /// Procedural glass look. Every rounded shape is an exact-size, anti-aliased
    /// texture (signed-distance rounded rect) cached by shape+size, so there are no
    /// 9-slice seams. Visible chrome is GUI.DrawTexture only; GUI.skin is never used
    /// for backgrounds. Mathf.Min/Max are only called with two arguments.
    /// </summary>
    internal sealed class GlassTheme
    {
        public const float BaseWidth = 700f;
        public const float BaseHeight = 440f;
        public const float Grid = 8f;
        public const float Pad = 16f;
        public const float RowGap = 8f;
        public const float SliderRow = 38f;
        public const float SwitchRow = 28f;
        public const float TitleHeight = 36f;
        public const float TabHeight = 28f;
        public const float SubTabHeight = 26f;
        public const float FooterHeight = 28f;
        public const float PanelRadius = 16f;
        private const float ShadowSpread = 30f;
        private const float ShadowBlur = 26f;
        private const int CacheLimit = 120;

        public readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.98f, 1f);
        public readonly Color TextSecondary = new Color(0.70f, 0.70f, 0.74f, 1f);
        public readonly Color TextMuted = new Color(0.60f, 0.60f, 0.64f, 1f);

        public GUIStyle Title;
        public GUIStyle Header;
        public GUIStyle Label;
        public GUIStyle Value;
        public GUIStyle Hint;
        public GUIStyle Status;
        public GUIStyle Center;
        public GUIStyle CenterMuted;
        public GUIStyle Field;
        public GUIStyle Invisible;

        public Texture2D Pixel;
        public Texture2D PixelClear;
        public Texture2D Logo;
        public Texture2D Chevron;

        public bool HasPixel { get; private set; }
        public bool StylesReady { get; private set; }
        public bool Ready { get { return StylesReady; } }
        public float BuiltScale { get; private set; } = -1f;

        private readonly Dictionary<long, Texture2D> _cache = new Dictionary<long, Texture2D>(96);
        private bool _basicsAttempted;
        private bool _loggedTextureError;
        private bool _loggedStyleError;
        private bool _setPixels32Broken;
        private MelonLogger.Instance _log;

        public static float Min3(float a, float b, float c)
        {
            return Mathf.Min(a, Mathf.Min(b, c));
        }

        public static bool Alive(UnityEngine.Object obj)
        {
            return obj != null;
        }

        public void Ensure(float uiScale, MelonLogger.Instance log)
        {
            _log = log;
            if (_graveyard.Count > 0)
                SweepGraveyard(false);
            try
            {
                if (!_basicsAttempted || !Alive(Pixel))
                {
                    _basicsAttempted = true;
                    try
                    {
                        Pixel = Solid(2, 2, Color.white);
                        PixelClear = Solid(2, 2, new Color(0f, 0f, 0f, 0f));
                        HasPixel = Alive(Pixel);
                        Logo = MakeLogo(96);
                        Chevron = MakeChevron(24);
                    }
                    catch (Exception ex)
                    {
                        HasPixel = Alive(Pixel);
                        LogOnce(ref _loggedTextureError, "UI texture build failed. Using flat fallbacks.", ex);
                    }
                }

                bool scaleChanged = Math.Abs(BuiltScale - uiScale) > 0.04f;
                if (Title == null || scaleChanged || !StylesReady)
                {
                    try
                    {
                        BuildStyles(uiScale);
                        StylesReady = Invisible != null && Label != null;
                        BuiltScale = uiScale;
                    }
                    catch (Exception ex)
                    {
                        StylesReady = Invisible != null;
                        LogOnce(ref _loggedStyleError, "Text style build failed.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogOnce(ref _loggedStyleError, "Theme Ensure failed.", ex);
            }
        }

        public void Dispose()
        {
            ClearCache();
            SweepGraveyard(true);
            Destroy(ref Pixel); Destroy(ref PixelClear); Destroy(ref Logo); Destroy(ref Chevron);
            HasPixel = false;
            StylesReady = false;
            _basicsAttempted = false;
            BuiltScale = -1f;
            Title = null;
            Invisible = null;
        }

        private void ClearCache()
        {
            foreach (var pair in _cache)
                Bury(pair.Value);
            _cache.Clear();
        }

        // Textures that were just drawn can still be referenced by IMGUI commands queued for this
        // frame, so they are destroyed a while later instead of immediately.
        private readonly List<KeyValuePair<Texture2D, int>> _graveyard = new List<KeyValuePair<Texture2D, int>>(64);

        private void Bury(Texture2D tex)
        {
            if (tex != null)
                _graveyard.Add(new KeyValuePair<Texture2D, int>(tex, Time.frameCount));
        }

        private void SweepGraveyard(bool everything)
        {
            int now = Time.frameCount;
            for (int i = _graveyard.Count - 1; i >= 0; i--)
            {
                if (!everything && now - _graveyard[i].Value < 90)
                    continue;
                Texture2D t = _graveyard[i].Key;
                if (t != null)
                    UnityEngine.Object.Destroy(t);
                _graveyard.RemoveAt(i);
            }
        }

        /// <summary>
        /// Animated pills create many small one-off sizes. Drop those first so the big
        /// (expensive) panel textures survive; clear everything only as a last resort.
        /// </summary>
        private void Evict()
        {
            CrashLog.MarkEvery("evict", 5f, "texture cache evict (" + _cache.Count + " cached)");
            var drop = new List<long>();
            foreach (var pair in _cache)
            {
                Texture2D t = pair.Value;
                if (t == null || (long)t.width * t.height < 30000L)
                    drop.Add(pair.Key);
            }
            for (int i = 0; i < drop.Count; i++)
            {
                Bury(_cache[drop[i]]);
                _cache.Remove(drop[i]);
            }
            if (_cache.Count >= CacheLimit)
            {
                ClearCache();
                _failed.Clear();
            }
        }

        public Texture2D White()
        {
            if (Alive(Pixel))
                return Pixel;
            try
            {
                Texture2D w = Texture2D.whiteTexture;
                if (Alive(w))
                    return w;
            }
            catch { }
            return null;
        }

        public void FillRect(Rect rect, Color color)
        {
            Texture2D tex = White();
            if (tex == null || rect.width <= 0f || rect.height <= 0f)
                return;
            Color prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * prev.a);
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        // ------------------------------------------------------------------ drawing

        public void Draw(Shape shape, Rect rect, float radius)
        {
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (w < 2 || h < 2)
                return;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int r = Mathf.RoundToInt(radius);
            int half = Mathf.Min(w, h) / 2;
            if (r > half) r = half;
            if (r < 0) r = 0;

            Texture2D tex = Get(shape, w, h, r);
            Rect snapped = new Rect(x, y, w, h);
            if (tex != null)
                GUI.DrawTexture(snapped, tex);
            else
                FillRounded(snapped, r, Fallback(shape));
        }

        public void DrawKnob(Rect circle)
        {
            const float pad = 4f;
            Draw(Shape.Knob, new Rect(circle.x - pad, circle.y - pad, circle.width + pad * 2f, circle.height + pad * 2f), 0f);
        }

        public Color Accent = new Color(1f, 1f, 1f, 1f);

        /// <summary>Draws a white-based shape tinted with the accent colour.</summary>
        public void DrawAccent(Shape shape, Rect rect, float radius, float alpha)
        {
            Color prev = GUI.color;
            GUI.color = new Color(Accent.r * prev.r, Accent.g * prev.g, Accent.b * prev.b, prev.a * Mathf.Clamp01(alpha));
            Draw(shape, rect, radius);
            GUI.color = prev;
        }

        public void DrawShadow(Rect area, float radius)
        {
            Draw(Shape.Shadow, new Rect(area.x - ShadowSpread, area.y - ShadowSpread, area.width + ShadowSpread * 2f, area.height + ShadowSpread * 2f), radius);
        }

        public void DrawPanel(Rect area, float radius, bool shadow)
        {
            if (shadow)
                DrawShadow(area, radius);
            Draw(Shape.Panel, area, radius);
        }

        private static Color Fallback(Shape shape)
        {
            switch (shape)
            {
                case Shape.Panel: return new Color(0.05f, 0.05f, 0.07f, 0.62f);
                case Shape.Popup: return new Color(0.08f, 0.08f, 0.10f, 0.94f);
                case Shape.Bar: return new Color(0f, 0f, 0f, 0.26f);
                case Shape.PillActive: return new Color(1f, 1f, 1f, 0.28f);
                case Shape.PillHover: return new Color(1f, 1f, 1f, 0.17f);
                case Shape.SwitchOn: return new Color(0.88f, 0.90f, 0.96f, 0.55f);
                case Shape.Fill: return new Color(1f, 1f, 1f, 0.95f);
                case Shape.Shadow: return new Color(0f, 0f, 0f, 0f);
                default: return new Color(1f, 1f, 1f, 0.12f);
            }
        }

        public void FillRounded(Rect area, float radius, Color color)
        {
            float r = radius;
            float halfW = area.width * 0.5f;
            float halfH = area.height * 0.5f;
            if (r > halfW) r = halfW;
            if (r > halfH) r = halfH;
            if (r < 2f)
            {
                FillRect(area, color);
                return;
            }
            FillRect(new Rect(area.x + r, area.y, area.width - r * 2f, area.height), color);
            FillRect(new Rect(area.x, area.y + r, r, area.height - r * 2f), color);
            FillRect(new Rect(area.xMax - r, area.y + r, r, area.height - r * 2f), color);
            float s = r * 0.2929f;
            FillRect(new Rect(area.x + s, area.y + s, r - s, r - s), color);
            FillRect(new Rect(area.xMax - r, area.y + s, r - s, r - s), color);
            FillRect(new Rect(area.x + s, area.yMax - r, r - s, r - s), color);
            FillRect(new Rect(area.xMax - r, area.yMax - r, r - s, r - s), color);
        }

        public void DrawApproxRim(Rect area, Color color)
        {
            FillRect(new Rect(area.x, area.y, area.width, 1f), color);
            FillRect(new Rect(area.x, area.yMax - 1f, area.width, 1f), color);
            FillRect(new Rect(area.x, area.y, 1f, area.height), color);
            FillRect(new Rect(area.xMax - 1f, area.y, 1f, area.height), color);
        }

        // ------------------------------------------------------------------ cache

        private Texture2D Get(Shape shape, int w, int h, int r)
        {
            if (w * (long)h > 3000000L)
                return null;

            long key = ((long)(int)shape << 52) | ((long)(w & 0xFFFF) << 36) | ((long)(h & 0xFFFF) << 20) | (long)(r & 0xFFFFF);
            if (_cache.TryGetValue(key, out Texture2D tex))
            {
                if (tex != null)
                    return tex;
                if (_failed.Contains(key))
                    return null;
                // Destroyed by the engine; rebuild below.
            }

            if (_cache.Count >= CacheLimit)
                Evict();

            try
            {
                tex = Build(shape, w, h, r);
                _cache[key] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                LogOnce(ref _loggedTextureError, "Glass texture build failed (" + shape + " " + w + "x" + h + "). Using flat fallbacks.", ex);
                _cache[key] = null;
                _failed.Add(key);
                return null;
            }
        }

        private readonly HashSet<long> _failed = new HashSet<long>();

        // ------------------------------------------------------------------ texture generation

        private Texture2D Build(Shape shape, int w, int h, int rad)
        {
            int tw = w, th = h;
            if ((long)w * h > 30000L)
                CrashLog.Mark("build texture " + shape + " " + w + "x" + h);
            if (shape == Shape.Shadow)
            {
                tw = Mathf.Max(4, w / 4);
                th = Mathf.Max(4, h / 4);
            }

            var tex = Create(tw, th);
            var px = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    Color c = ShadePixel(shape, x, y, tw, th, w, h, rad);
                    Color32 p = default;
                    p.r = ToByte(c.r);
                    p.g = ToByte(c.g);
                    p.b = ToByte(c.b);
                    p.a = ToByte(c.a);
                    px[y * tw + x] = p;
                }
            }
            Upload(tex, px, tw, th);
            tex.Apply(false, false);
            return tex;
        }

        private void Upload(Texture2D tex, Color32[] px, int tw, int th)
        {
            if (!_setPixels32Broken)
            {
                try
                {
                    tex.SetPixels32(px);
                    return;
                }
                catch (Exception)
                {
                    _setPixels32Broken = true;
                }
            }
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    Color32 p = px[y * tw + x];
                    tex.SetPixel(x, y, new Color(p.r / 255f, p.g / 255f, p.b / 255f, p.a / 255f));
                }
            }
        }

        private static byte ToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            return (byte)(v * 255f + 0.5f);
        }

        private static float Sm(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Straight-alpha "over" composite of top onto bottom.</summary>
        private static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0.0001f)
                return new Color(0f, 0f, 0f, 0f);
            float inv = 1f / a;
            float tb = bottom.a * (1f - top.a);
            return new Color(
                (top.r * top.a + bottom.r * tb) * inv,
                (top.g * top.a + bottom.g * tb) * inv,
                (top.b * top.a + bottom.b * tb) * inv,
                a);
        }

        private static float RoundedSdf(float px, float py, float hx, float hy, float r)
        {
            float dx = Mathf.Abs(px) - (hx - r);
            float dy = Mathf.Abs(py) - (hy - r);
            float ox = Mathf.Max(dx, 0f);
            float oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        private static Color W(float a)
        {
            return new Color(1f, 1f, 1f, a);
        }

        private static Color ShadePixel(Shape shape, int x, int y, int tw, int th, int w, int h, int rad)
        {
            // Pixel centre relative to the shape centre; y grows upwards in texture space.
            float px = x + 0.5f - tw * 0.5f;
            float py = y + 0.5f - th * 0.5f;
            float v = (y + 0.5f) / th;      // 1 at the top edge of the rect
            float u = (x + 0.5f) / tw;

            if (shape == Shape.Shadow)
                return ShadowPixel(u, v, w, h, rad);

            if (shape == Shape.Knob)
                return KnobPixel(px, py, tw);

            float d = RoundedSdf(px, py, tw * 0.5f, th * 0.5f, rad);
            float cover = Mathf.Clamp01(0.5f - d);
            if (cover <= 0f)
                return new Color(0f, 0f, 0f, 0f);
            float inside = -d;
            float rim = 1f - Sm(0.35f, 1.55f, inside);

            Color fill;
            float rimA;
            switch (shape)
            {
                case Shape.Panel:
                {
                    Color top = new Color(0.17f, 0.17f, 0.20f, 0.44f);
                    Color bottom = new Color(0.05f, 0.05f, 0.07f, 0.60f);
                    fill = Color.Lerp(bottom, top, v);
                    float dx = u;
                    float dy = 1f - v;
                    float dist = Mathf.Sqrt(dx * dx * 0.55f + dy * dy);
                    float glow = 0.14f * Mathf.Clamp01(1f - dist / 1.15f);
                    fill = Over(new Color(0.92f, 0.94f, 1f, glow), fill);
                    rimA = 0.14f + 0.36f * v * v;
                    break;
                }
                case Shape.Popup:
                    fill = Color.Lerp(new Color(0.06f, 0.06f, 0.08f, 0.95f), new Color(0.11f, 0.11f, 0.14f, 0.95f), v);
                    rimA = 0.22f;
                    break;
                case Shape.Bar:
                    fill = new Color(0f, 0f, 0f, 0.26f);
                    rimA = 0.09f;
                    break;
                case Shape.Pill:
                    fill = W(Mathf.Lerp(0.07f, 0.14f, v));
                    rimA = 0.20f;
                    break;
                case Shape.PillHover:
                    fill = W(Mathf.Lerp(0.14f, 0.23f, v));
                    rimA = 0.36f;
                    break;
                case Shape.PillActive:
                    fill = W(Mathf.Lerp(0.20f, 0.36f, v));
                    rimA = 0.62f;
                    break;
                case Shape.Field:
                    fill = new Color(0f, 0f, 0f, 0.30f);
                    rimA = 0.17f;
                    break;
                case Shape.Track:
                    fill = W(0.20f);
                    rimA = 0f;
                    break;
                case Shape.Fill:
                    fill = W(Mathf.Lerp(0.82f, 1f, v));
                    rimA = 0f;
                    break;
                case Shape.SwitchOff:
                    fill = W(0.14f);
                    rimA = 0.28f;
                    break;
                default: // SwitchOn
                    fill = new Color(0.86f, 0.88f, 0.95f, Mathf.Lerp(0.50f, 0.62f, v));
                    rimA = 0.62f;
                    break;
            }

            Color col = rimA > 0f ? Over(W(rimA * rim), fill) : fill;
            col.a *= cover;
            return col;
        }

        private static Color KnobPixel(float px, float py, int size)
        {
            const float pad = 4f;
            float r = size * 0.5f - pad;
            float dist = Mathf.Sqrt(px * px + py * py);
            // Soft drop shadow, nudged downwards (texture y is up, so subtract).
            float sdist = Mathf.Sqrt(px * px + (py + 1.4f) * (py + 1.4f));
            float sh = 0.34f * (1f - Sm(0f, pad + 1f, sdist - r));
            Color shadow = new Color(0f, 0f, 0f, sh);
            float cover = Mathf.Clamp01(r - dist + 0.5f);
            float edge = Mathf.Clamp01((r - dist) / 1.2f);
            Color body = Color.Lerp(new Color(0.78f, 0.78f, 0.82f, 1f), new Color(0.98f, 0.98f, 1f, 1f), edge);
            body.a = cover;
            return Over(body, shadow);
        }

        private static Color ShadowPixel(float u, float v, int w, int h, int rad)
        {
            // Full-resolution coordinates relative to the centre.
            float fx = (u - 0.5f) * w;
            float fy = (v - 0.5f) * h;
            float hx = w * 0.5f - ShadowSpread;
            float hy = h * 0.5f - ShadowSpread;
            float sdf = RoundedSdf(fx, fy + 8f, hx, hy, rad);   // offset downwards
            float plain = RoundedSdf(fx, fy, hx, hy, rad);
            float a = (1f - Sm(0f, ShadowBlur, sdf)) * 0.50f;
            a *= Mathf.Clamp01(plain / 3f);                       // keep the glass interior clear
            return new Color(0f, 0f, 0f, a);
        }

        // ------------------------------------------------------------------ text styles

        private void LogOnce(ref bool flagged, string message, Exception ex)
        {
            if (flagged)
                return;
            flagged = true;
            if (ex != null)
                _log?.Error("VividFrame {0} {1}", message, ex);
            else
                _log?.Warning("VividFrame {0}", message);
        }

        private void BuildStyles(float uiScale)
        {
            float s = Mathf.Max(0.75f, uiScale);
            Font font = null;
            try
            {
                if (GUI.skin != null && GUI.skin.label != null)
                    font = GUI.skin.label.font;
            }
            catch { }

            Title = TextStyle(font, Mathf.RoundToInt(17f * s), FontStyle.Bold, TextPrimary, false, TextAnchor.MiddleLeft);
            Header = TextStyle(font, Mathf.RoundToInt(11f * s), FontStyle.Bold, TextMuted, false, TextAnchor.MiddleLeft);
            Label = TextStyle(font, Mathf.RoundToInt(13f * s), FontStyle.Normal, TextPrimary, false, TextAnchor.MiddleLeft);
            Center = TextStyle(font, Mathf.RoundToInt(12f * s), FontStyle.Normal, TextPrimary, false, TextAnchor.MiddleCenter);
            CenterMuted = TextStyle(font, Mathf.RoundToInt(12f * s), FontStyle.Normal, TextSecondary, false, TextAnchor.MiddleCenter);
            Value = TextStyle(font, Mathf.RoundToInt(13f * s), FontStyle.Normal, TextSecondary, false, TextAnchor.MiddleRight);
            Hint = TextStyle(font, Mathf.RoundToInt(11f * s), FontStyle.Normal, TextMuted, true, TextAnchor.UpperLeft);
            Status = TextStyle(font, Mathf.RoundToInt(12f * s), FontStyle.Normal, TextSecondary, false, TextAnchor.MiddleLeft);
            Field = TextStyle(font, Mathf.RoundToInt(12f * s), FontStyle.Normal, TextPrimary, false, TextAnchor.MiddleLeft);
            try { SetPadding(Field, 10, 8, 2, 2); } catch { }

            Invisible = new GUIStyle();
            try { if (font != null) Invisible.font = font; } catch { }
            try { Invisible.fontSize = Mathf.RoundToInt(12f * s); } catch { }
            try { Invisible.alignment = TextAnchor.MiddleCenter; } catch { }
            try { Invisible.border = new RectOffset(0, 0, 0, 0); } catch { }
            try { Invisible.padding = new RectOffset(0, 0, 0, 0); } catch { }
            Texture2D clear = Alive(PixelClear) ? PixelClear : null;
            PaintText(Invisible, TextPrimary, clear);
            if (Field != null)
                PaintText(Field, TextPrimary, clear);
        }

        private static GUIStyle TextStyle(Font font, int size, FontStyle weight, Color color, bool wrap, TextAnchor align)
        {
            var style = new GUIStyle();
            try { if (font != null) style.font = font; } catch { }
            try { style.fontSize = size; } catch { }
            try { style.fontStyle = weight; } catch { }
            try { style.wordWrap = wrap; } catch { }
            try { style.richText = false; } catch { }
            try { style.alignment = align; } catch { }
            PaintText(style, color, null);
            return style;
        }

        private static void PaintText(GUIStyle style, Color color, Texture2D background)
        {
            PaintState(style.normal, color, background);
            PaintState(style.hover, color, background);
            PaintState(style.active, color, background);
            PaintState(style.onNormal, color, background);
        }

        private static void PaintState(GUIStyleState state, Color color, Texture2D background)
        {
            if (state == null)
                return;
            try
            {
                if (background != null)
                    state.background = background;
                state.textColor = color;
            }
            catch { }
        }

        private static void SetPadding(GUIStyle style, int left, int right, int top, int bottom)
        {
            try
            {
                RectOffset o = style.padding;
                if (o != null)
                {
                    o.left = left; o.right = right; o.top = top; o.bottom = bottom;
                    return;
                }
            }
            catch { }
            try { style.padding = new RectOffset(left, right, top, bottom); } catch { }
        }

        // ------------------------------------------------------------------ small fixed icons

        private static Texture2D MakeLogo(int size)
        {
            // Glass badge with a bold "V" and a small spark. Drawn in unit space (y up).
            var tex = Create(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float aa = size;                                   // unit distance -> pixels

                    float badge = RoundedSdf(u - 0.5f, v - 0.5f, 0.5f, 0.5f, 0.28f) * aa;
                    float cover = Mathf.Clamp01(0.5f - badge);
                    if (cover <= 0f)
                    {
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                        continue;
                    }

                    // Frosted fill, brighter towards the top, with a thin bright rim.
                    Color col = new Color(1f, 1f, 1f, Mathf.Lerp(0.10f, 0.26f, v));
                    float rim = 1f - Sm(0.4f, 1.8f, -badge);
                    col = Over(new Color(1f, 1f, 1f, 0.55f * rim), col);

                    // The V: two rounded strokes meeting at the bottom.
                    float dv = Mathf.Min(SegDist(u, v, 0.27f, 0.73f, 0.5f, 0.27f), SegDist(u, v, 0.5f, 0.27f, 0.73f, 0.73f)) - 0.075f;
                    float vCover = Mathf.Clamp01(0.5f - dv * aa);
                    col = Over(new Color(0.98f, 0.98f, 1f, vCover), col);

                    // Spark dot, top right.
                    float ds = Mathf.Sqrt((u - 0.79f) * (u - 0.79f) + (v - 0.80f) * (v - 0.80f)) - 0.055f;
                    col = Over(new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - ds * aa) * 0.95f), col);

                    col.a *= cover;
                    tex.SetPixel(x, y, col);
                }
            }
            return Finish(tex);
        }

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Texture2D MakeChevron(int size)
        {
            var tex = Create(size, size);
            float cx = size * 0.5f, cy = size * 0.55f;
            float arm = size * 0.26f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - cx;
                    float py = y + 0.5f - cy;
                    // Downward-pointing chevron; texture y is up, so the arms rise from the tip.
                    float line = Mathf.Clamp01(1f - Mathf.Abs(py - Mathf.Abs(px) + arm * 0.5f) / 1.1f);
                    float a = Mathf.Abs(px) <= arm + 0.5f ? line * 0.9f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            return Finish(tex);
        }

        private static Texture2D Solid(int w, int h, Color color)
        {
            var tex = Create(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            return Finish(tex);
        }

        private static Texture2D Create(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "VividFrameGlass144";
            return tex;
        }

        private static Texture2D Finish(Texture2D tex)
        {
            tex.Apply(false, false);
            return tex;
        }

        private static void Destroy(ref Texture2D tex)
        {
            if (tex == null)
                return;
            UnityEngine.Object.Destroy(tex);
            tex = null;
        }
    }
}
