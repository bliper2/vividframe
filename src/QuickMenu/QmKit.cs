using System;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>
    /// The Quick Menu objects VividFrame clones from. Names follow recent VRChat builds; each lookup
    /// tries a few names because they move between updates. Only the page and button templates are
    /// required; everything else has a fallback.
    /// </summary>
    internal sealed class QmKit
    {
        public Transform QmRoot;
        public Transform Window;
        public Canvas RootCanvas;
        public Transform PageTemplate;
        public Transform ButtonTemplate;
        public Transform GroupTemplate;
        public Transform HeaderTemplate;
        public Transform ToggleTemplate;
        public Transform SliderTemplate;
        public Transform TabBar;
        public Transform TabTemplate;

        /// <summary>Hover and press colours of a native button, for controls that need a new Button.</summary>
        public ColorBlock ButtonColors;
        public Selectable.Transition ButtonTransition = Selectable.Transition.ColorTint;

        /// <summary>The Quick Menu's own check and cross artwork, when it can be found.</summary>
        public Sprite OnSprite;
        public Sprite OffSprite;

        /// <summary>Inactive parent for fresh clones so their scripts stay asleep until stripped.</summary>
        public Transform Staging;

        public string Missing;

        public static GameObject FindQmRoot()
        {
            GameObject go = GameObject.Find("Canvas_QuickMenu(Clone)");
            if (go == null)
                go = GameObject.Find("UserInterface/Canvas_QuickMenu(Clone)");
            return go;
        }

        public static QmKit Discover(GameObject qm)
        {
            var kit = new QmKit { QmRoot = qm.transform };
            Transform root = kit.QmRoot;
            kit.Window = QmUtil.FindAny(root, "Window", "QMParent") ?? root;
            kit.RootCanvas = qm.GetComponent<Canvas>();

            kit.PageTemplate = QmUtil.FindAny(root, "Menu_Camera", "Menu_Dashboard");
            if (kit.PageTemplate == null) { kit.Missing = "page template (Menu_Camera / Menu_Dashboard)"; return kit; }
            Transform content = FindContent(kit.PageTemplate);
            if (content == null) { kit.Missing = "page content (Scrollrect/Viewport/VerticalLayoutGroup)"; return kit; }

            kit.ButtonTemplate = QmUtil.FindAny(root, "Button_Respawn", "Button_NewInstance", "Button_GoHome");
            if (kit.ButtonTemplate == null) { kit.Missing = "button template (Button_Respawn)"; return kit; }
            Button nativeButton = kit.ButtonTemplate.GetComponent<Button>();
            if (nativeButton == null) { kit.Missing = "button component on " + kit.ButtonTemplate.name; return kit; }
            kit.ButtonColors = nativeButton.colors;
            kit.ButtonTransition = nativeButton.transition;

            // Groups and headers from the page we clone, so spacing matches that page exactly.
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (kit.GroupTemplate == null && child.GetComponent<GridLayoutGroup>() != null)
                    kit.GroupTemplate = child;
                else if (kit.HeaderTemplate == null && child.name.StartsWith("Header", StringComparison.Ordinal) && QmUtil.FirstText(child) != null)
                    kit.HeaderTemplate = child;
            }
            if (kit.GroupTemplate == null)
                kit.GroupTemplate = kit.ButtonTemplate.parent;
            if (kit.GroupTemplate == null || kit.GroupTemplate.GetComponent<LayoutGroup>() == null)
            {
                kit.Missing = "button group layout";
                return kit;
            }
            if (kit.HeaderTemplate == null)
            {
                kit.HeaderTemplate = QmUtil.FindAny(root, "Header_H3", "Header_QuickActions");
                if (kit.HeaderTemplate != null && QmUtil.FirstText(kit.HeaderTemplate) == null)
                    kit.HeaderTemplate = null;
            }

            // Native settings toggles are buttons with Icon_On / Icon_Off children, not Unity Toggles.
            kit.ToggleTemplate = QmUtil.FindAny(root, "Button_ToggleQMInfo", "Button_ToggleTooltips");
            if (kit.ToggleTemplate != null && (QmUtil.FindDeep(kit.ToggleTemplate, "Icon_On") == null || QmUtil.FindDeep(kit.ToggleTemplate, "Icon_Off") == null))
                kit.ToggleTemplate = null;

            kit.SliderTemplate = QmUtil.FindAny(root, "VolumeSlider_Master", "ScreenBrightness");
            if (kit.SliderTemplate != null && kit.SliderTemplate.GetComponentInChildren<Slider>(true) == null)
                kit.SliderTemplate = null;

            Transform tabs = QmUtil.FindAny(root, "Page_Buttons_QM");
            if (tabs != null)
            {
                Transform row = QmUtil.FindDeep(tabs, "HorizontalLayoutGroup") ?? tabs;
                Transform tab = QmUtil.FindDeep(row, "Page_Settings") ?? QmUtil.FindDeep(row, "Page_DevTools");
                if (tab != null && tab.parent == row && tab.GetComponent<Button>() != null)
                {
                    kit.TabBar = row;
                    kit.TabTemplate = tab;
                }
            }
            return kit;
        }

        /// <summary>Looks up the Quick Menu's check and cross sprites once, for recreated toggles.</summary>
        public void FindToggleSprites()
        {
            if (OnSprite != null && OffSprite != null)
                return;
            try
            {
                var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < sprites.Length && (OnSprite == null || OffSprite == null); i++)
                {
                    Sprite s = sprites[i];
                    if (s == null)
                        continue;
                    string n = s.name;
                    if (OnSprite == null && n == "Checkmark")
                        OnSprite = s;
                    else if (OffSprite == null && n == "Decline")
                        OffSprite = s;
                }
            }
            catch
            {
            }
            if (OnSprite == null)
                OnSprite = QmUtil.MakeMark(true);
            if (OffSprite == null)
                OffSprite = QmUtil.MakeMark(false);
        }

        public string Describe()
        {
            return "page " + PageTemplate.name + ", button " + ButtonTemplate.name + ", group " + GroupTemplate.name
                   + ", header " + (HeaderTemplate != null ? HeaderTemplate.name : "none")
                   + ", toggle " + (ToggleTemplate != null ? ToggleTemplate.name : "recreated")
                   + ", slider " + (SliderTemplate != null ? SliderTemplate.name : "steppers")
                   + ", tab " + (TabTemplate != null ? TabTemplate.name : "dashboard button");
        }

        /// <summary>True while the Quick Menu window is on screen.</summary>
        public bool IsVisible()
        {
            if (QmRoot == null || Window == null || !Window.gameObject.activeInHierarchy)
                return false;
            return RootCanvas == null || RootCanvas.enabled;
        }

        /// <summary>The vertical list inside a page's scroll view.</summary>
        public static Transform FindContent(Transform page)
        {
            var all = page.GetComponentsInChildren<VerticalLayoutGroup>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i].transform;
                if (t.parent != null && t.parent.name == "Viewport")
                    return t;
            }
            return null;
        }

        public void EnsureStaging()
        {
            if (Staging != null)
                return;
            var go = new GameObject("VF_QmStaging");
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            Staging = go.transform;
        }

        /// <summary>
        /// Clones <paramref name="template"/> asleep, strips it, lets <paramref name="prepare"/> adjust it
        /// while it is still asleep, then parents it under <paramref name="parent"/> and activates it.
        /// </summary>
        public GameObject Clone(Transform template, Transform parent, string name, Action<GameObject> prepare = null)
        {
            EnsureStaging();
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, Staging, false);
            go.name = name;
            QmUtil.Strip(go);
            go.SetActive(true);
            prepare?.Invoke(go);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Hides decorations (badges, secondary icons) a cloned control brought along.</summary>
        public static void HideBadges(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t != root && (t.name.StartsWith("Badge", StringComparison.Ordinal) || t.name == "Icon_Secondary"))
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>A plain Button with native hover and press colours on a clone that has none.</summary>
        public Button EnsureButton(GameObject go)
        {
            Button b = go.GetComponent<Button>();
            if (b != null)
                return b;
            b = go.AddComponent<Button>();
            Transform bg = QmUtil.FindDeep(go.transform, "Background");
            Graphic target = bg != null ? bg.GetComponent<Graphic>() : null;
            if (target == null)
                target = go.GetComponent<Graphic>() ?? go.GetComponentInChildren<Image>(true);
            b.targetGraphic = target;
            b.transition = ButtonTransition;
            b.colors = ButtonColors;
            return b;
        }

        /// <summary>
        /// Gives a row a fixed height when it has nothing that reports one, so the page's vertical
        /// layout cannot collapse it.
        /// </summary>
        public static void KeepHeight(GameObject go)
        {
            if (go.GetComponent<LayoutElement>() != null || go.GetComponent<LayoutGroup>() != null)
                return;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                return;
            float h = rt.rect.height;
            if (h <= 1f)
                return;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
        }

        public void DestroyStaging()
        {
            if (Staging != null)
                UnityEngine.Object.Destroy(Staging.gameObject);
            Staging = null;
        }
    }
}
