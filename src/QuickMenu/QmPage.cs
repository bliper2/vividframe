using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>
    /// One VividFrame page inside the Quick Menu window. It is a stripped clone of a native page laid
    /// over the current one, shown and hidden by VividFrame itself; VRChat's page stack never learns
    /// about it, which keeps the game's menu state untouched.
    /// </summary>
    internal sealed class QmPage
    {
        public readonly GameObject Root;
        public readonly Transform Content;
        private readonly QmKit _kit;
        private readonly ScrollRect _scroll;
        private readonly List<IQmBound> _bound = new List<IQmBound>();
        private Transform _group;

        public QmPage(QmKit kit, string title, Action onBack)
        {
            _kit = kit;
            Transform template = kit.PageTemplate;
            Root = kit.Clone(template, template.parent, "VF_Page");
            Root.SetActive(false);
            Root.transform.SetAsLastSibling();

            Content = QmKit.FindContent(Root.transform);
            QmUtil.ClearChildren(Content);
            _scroll = Content.GetComponentInParent<ScrollRect>();

            // Keep only the header and the scroll view; native pages carry extra panels.
            Transform header = null;
            for (int i = 0; i < Root.transform.childCount; i++)
            {
                Transform child = Root.transform.GetChild(i);
                if (header == null && child.name.StartsWith("Header", StringComparison.Ordinal))
                    header = child;
                else if (!Content.IsChildOf(child))
                    child.gameObject.SetActive(false);
            }

            if (header != null)
            {
                Transform right = QmUtil.FindDeep(header, "RightItemContainer");
                if (right != null)
                    for (int i = 0; i < right.childCount; i++)
                        right.GetChild(i).gameObject.SetActive(false);
                Transform titleT = QmUtil.FindDeep(header, "Text_Title");
                QmUtil.SetText(titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : QmUtil.FirstText(header), title);
                Transform back = QmUtil.FindDeep(header, "Button_Back");
                Button backButton = back != null ? back.GetComponent<Button>() : null;
                if (backButton != null)
                {
                    back.gameObject.SetActive(true);
                    backButton.onClick = new Button.ButtonClickedEvent();
                    backButton.onClick.AddListener((Action)onBack);
                }
                else
                {
                    AddButton("Back", onBack, false);
                }
            }
            else
            {
                AddButton("Back", onBack, false);
            }

            AddBackdrop();

            Canvas canvas = Root.GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = true;
            CanvasGroup cg = Root.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            GraphicRaycaster ray = Root.GetComponent<GraphicRaycaster>();
            if (ray != null)
                ray.enabled = true;
        }

        /// <summary>An opaque panel behind our controls so the native page underneath does not show through.</summary>
        private void AddBackdrop()
        {
            var go = new GameObject("VF_Backdrop");
            go.transform.SetParent(Root.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            Image src = _kit.BackdropSource;
            if (src != null && src.sprite != null)
            {
                img.sprite = src.sprite;
                img.type = src.type;
                img.color = new Color(src.color.r, src.color.g, src.color.b, 1f);
            }
            else
            {
                // Close to the QM's dark teal glass.
                img.color = new Color(0.035f, 0.07f, 0.075f, 0.98f);
            }
        }

        public bool Visible => Root != null && Root.activeSelf;

        public void Show(bool on)
        {
            if (Root == null)
                return;
            Root.SetActive(on);
            if (!on)
                return;
            Root.transform.SetAsLastSibling();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < _bound.Count; i++)
                _bound[i].Refresh();
        }

        /// <summary>Starts a titled group; following buttons and toggles go into its grid.</summary>
        public void Header(string title)
        {
            if (_kit.HeaderTemplate != null)
            {
                GameObject h = _kit.Clone(_kit.HeaderTemplate, Content, "VF_Header");
                QmUtil.SetText(QmUtil.FirstText(h.transform), title);
            }
            _group = null;
        }

        private Transform Group()
        {
            if (_group != null)
                return _group;
            GameObject g = _kit.Clone(_kit.GroupTemplate, Content, "VF_Group");
            QmUtil.ClearChildren(g.transform);
            _group = g.transform;
            return _group;
        }

        public QmButton AddButton(string label, Action onClick, bool jump)
        {
            return new QmButton(_kit, Group(), label, onClick, jump);
        }

        public void AddToggle(string label, MelonPreferences_Entry<bool> entry, Action changed)
        {
            _bound.Add(new QmToggle(_kit, Group(), label, entry, changed));
        }

        public void AddCycle(string label, string[] options, MelonPreferences_Entry<int> entry, Action changed)
        {
            _bound.Add(new QmCycle(_kit, Group(), label, options, entry, changed));
        }

        public void AddSlider(string label, MelonPreferences_Entry<float> entry, float min, float max, string format, Action changed)
        {
            _group = _kit.SliderTemplate != null ? null : _group;
            _bound.Add(new QmSlider(_kit, Content, _kit.SliderTemplate != null ? null : Group(), label, entry, min, max, format, changed));
        }

        public void AddSlider(string label, MelonPreferences_Entry<int> entry, int min, int max, Action changed)
        {
            _group = _kit.SliderTemplate != null ? null : _group;
            _bound.Add(new QmSlider(_kit, Content, _kit.SliderTemplate != null ? null : Group(), label, entry, min, max, changed));
        }

        public void Destroy()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }
    }
}
