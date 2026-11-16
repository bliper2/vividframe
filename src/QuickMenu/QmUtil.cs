using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>
    /// Small helpers for working with clones of VRChat's Quick Menu objects. Clones are made under an
    /// inactive staging object so none of their scripts wake up, then stripped to plain Unity UI before
    /// they go live, so a cloned button can never run the game action it was copied from.
    /// </summary>
    internal static class QmUtil
    {
        /// <summary>First descendant (or the root itself) with the given name, including inactive ones.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name == name)
                    return t;
            }
            return null;
        }

        /// <summary>The first of several candidate names that exists under <paramref name="root"/>.</summary>
        public static Transform FindAny(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = FindDeep(root, names[i]);
                if (t != null)
                    return t;
            }
            return null;
        }

        public static TextMeshProUGUI FirstText(Transform root)
        {
            return root != null ? root.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        }

        public static void SetText(TextMeshProUGUI text, string value)
        {
            if (text == null)
                return;
            text.richText = true;
            text.text = value;
        }

        /// <summary>Destroys every child of <paramref name="t"/>. Only call on staged (inactive) clones.</summary>
        public static void ClearChildren(Transform t)
        {
            if (t == null)
                return;
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        /// <summary>
        /// Keeps plain Unity UI, TextMeshPro, graphics, layout, scrolling and VRChat's style components.
        /// VRChat logic (page registration, tab navigation, setting bindings, tooltips, localisation) is
        /// removed. Buttons, toggles and sliders that are VRChat subclasses become plain Unity ones, and
        /// their serialized click and value events are dropped with them.
        /// </summary>
        public static void Strip(GameObject root)
        {
            var comps = root.GetComponentsInChildren<Component>(true);
            var replaced = new HashSet<IntPtr>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                    continue;
                string full;
                try { full = c.GetIl2CppType().FullName ?? string.Empty; }
                catch { continue; }

                if (IsExact(full, "UnityEngine.UI.Button"))
                {
                    c.Cast<Button>().onClick = new Button.ButtonClickedEvent();
                    continue;
                }
                if (IsExact(full, "UnityEngine.UI.Toggle"))
                {
                    c.Cast<Toggle>().onValueChanged = new Toggle.ToggleEvent();
                    continue;
                }
                if (IsExact(full, "UnityEngine.UI.Slider"))
                {
                    c.Cast<Slider>().onValueChanged = new Slider.SliderEvent();
                    continue;
                }

                Slider slider = c.TryCast<Slider>();
                if (slider != null)
                {
                    replaced.Add(slider.gameObject.Pointer);
                    ReplaceSlider(slider);
                    continue;
                }
                Toggle toggle = c.TryCast<Toggle>();
                if (toggle != null)
                {
                    replaced.Add(toggle.gameObject.Pointer);
                    ReplaceToggle(toggle);
                    continue;
                }
                Button button = c.TryCast<Button>();
                if (button != null)
                {
                    replaced.Add(button.gameObject.Pointer);
                    ReplaceButton(button);
                    continue;
                }

                if (Keep(c, full))
                    continue;
                UnityEngine.Object.DestroyImmediate(c);
            }

            if (replaced.Count == 0)
                return;
            // Style components on a replaced control still point at the destroyed one; drop them.
            comps = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null || !replaced.Contains(c.gameObject.Pointer))
                    continue;
                string full;
                try { full = c.GetIl2CppType().FullName ?? string.Empty; }
                catch { continue; }
                if (!full.StartsWith("UnityEngine.", StringComparison.Ordinal) && !full.StartsWith("TMPro.", StringComparison.Ordinal)
                    && c.TryCast<Graphic>() == null && c.TryCast<Selectable>() == null)
                    UnityEngine.Object.DestroyImmediate(c);
            }
        }

        private static bool IsExact(string full, string name)
        {
            return string.Equals(full, name, StringComparison.Ordinal);
        }

        private static bool Keep(Component c, string full)
        {
            if (full.StartsWith("UnityEngine.", StringComparison.Ordinal) || full.StartsWith("TMPro.", StringComparison.Ordinal))
                return true;
            if (c.TryCast<Graphic>() != null || c.TryCast<ScrollRect>() != null || c.TryCast<Scrollbar>() != null
                || c.TryCast<RectMask2D>() != null || c.TryCast<Mask>() != null || c.TryCast<BaseRaycaster>() != null
                || c.TryCast<LayoutGroup>() != null || c.TryCast<LayoutElement>() != null || c.TryCast<ContentSizeFitter>() != null
                || c.TryCast<CanvasGroup>() != null || c.TryCast<Canvas>() != null)
                return true;
            // VRChat's theme component; it only recolours graphics.
            return full.IndexOf("Style", StringComparison.Ordinal) >= 0;
        }

        private static void ReplaceButton(Button old)
        {
            GameObject go = old.gameObject;
            Graphic target = old.targetGraphic;
            Selectable.Transition transition = old.transition;
            ColorBlock colors = old.colors;
            UnityEngine.Object.DestroyImmediate(old);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = target;
            b.transition = transition;
            b.colors = colors;
        }

        private static void ReplaceToggle(Toggle old)
        {
            GameObject go = old.gameObject;
            Graphic target = old.targetGraphic;
            Graphic check = old.graphic;
            Selectable.Transition transition = old.transition;
            ColorBlock colors = old.colors;
            UnityEngine.Object.DestroyImmediate(old);
            Toggle t = go.AddComponent<Toggle>();
            t.targetGraphic = target;
            t.graphic = check;
            t.transition = transition;
            t.colors = colors;
        }

        private static void ReplaceSlider(Slider old)
        {
            GameObject go = old.gameObject;
            RectTransform fill = old.fillRect;
            RectTransform handle = old.handleRect;
            Graphic target = old.targetGraphic;
            Slider.Direction direction = old.direction;
            Selectable.Transition transition = old.transition;
            ColorBlock colors = old.colors;
            UnityEngine.Object.DestroyImmediate(old);
            Slider s = go.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = target;
            s.direction = direction;
            s.transition = transition;
            s.colors = colors;
        }

        /// <summary>
        /// A simple white aperture mark (ring and centre dot) for the VividFrame tab. Built once.
        /// </summary>
        public static Sprite MakeIcon()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Ring from r=42 to r=54 and a dot of r=18, with a one-pixel soft edge.
                    float ring = Mathf.Clamp01(Mathf.Min(d - 41.5f, 54.5f - d));
                    float dot = Mathf.Clamp01(18.5f - d);
                    float a = Mathf.Max(ring, dot);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static void LogOnce(ref bool flag, MelonLogger.Instance log, string message)
        {
            if (flag)
                return;
            flag = true;
            log?.Warning(message);
        }
    }
}
