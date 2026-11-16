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
        public Transform PageTemplate;
        public Transform ButtonTemplate;
        public Transform GroupTemplate;
        public Transform HeaderTemplate;
        public Transform ToggleTemplate;
        public Transform SliderTemplate;
        public Transform TabBar;
        public Transform TabTemplate;
        public Image BackdropSource;
        public Canvas RootCanvas;

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
            kit.ButtonTemplate = QmUtil.FindAny(root, "Button_Respawn", "Button_NewInstance", "Button_GoHome");
            if (kit.PageTemplate == null) { kit.Missing = "page template (Menu_Camera / Menu_Dashboard)"; return kit; }
            if (kit.ButtonTemplate == null) { kit.Missing = "button template (Button_Respawn)"; return kit; }
            if (FindContent(kit.PageTemplate) == null) { kit.Missing = "page content (ScrollRect/Viewport/VerticalLayoutGroup)"; return kit; }

            kit.GroupTemplate = kit.ButtonTemplate.parent;
            if (kit.GroupTemplate == null || kit.GroupTemplate.GetComponent<LayoutGroup>() == null)
            {
                kit.Missing = "button group layout";
                return kit;
            }

            kit.HeaderTemplate = QmUtil.FindAny(root, "Header_QuickActions", "Header_H3");
            if (kit.HeaderTemplate != null && QmUtil.FirstText(kit.HeaderTemplate) == null)
                kit.HeaderTemplate = null;

            kit.ToggleTemplate = QmUtil.FindAny(root, "Button_ToggleQMInfo", "Button_ToggleTooltips");
            if (kit.ToggleTemplate != null && kit.ToggleTemplate.GetComponent<Toggle>() == null)
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

            Transform bg = QmUtil.FindAny(kit.Window, "Background_QM", "BackgroundLayer01", "Panel_QM_Background");
            if (bg != null)
                kit.BackdropSource = bg.GetComponent<Image>();
            return kit;
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

        /// <summary>Clones <paramref name="template"/> asleep, strips it, then parents it under <paramref name="parent"/>.</summary>
        public GameObject Clone(Transform template, Transform parent, string name)
        {
            EnsureStaging();
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, Staging, false);
            go.name = name;
            QmUtil.Strip(go);
            go.transform.SetParent(parent, false);
            go.SetActive(true);
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

        public void DestroyStaging()
        {
            if (Staging != null)
                UnityEngine.Object.Destroy(Staging.gameObject);
            Staging = null;
        }
    }
}
