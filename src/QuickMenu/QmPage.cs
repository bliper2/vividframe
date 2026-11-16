using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>
    /// One VividFrame page inside the Quick Menu window: a stripped clone of a native page that sits
    /// in the same place as VRChat's pages. VividFrame shows and hides it itself; VRChat's page stack
    /// never learns about it, so the game's menu state is untouched.
    /// </summary>
    internal sealed class QmPage
    {
        public readonly GameObject Root;
        public readonly Transform Content;
        private readonly QmKit _kit;
        private readonly ScrollRect _scroll;
        private readonly CanvasGroup _group;
        private readonly RectTransform _rect;
        private readonly Vector2 _basePos;
        private readonly GameObject _backdrop;
        private readonly List<IQmBound> _bound = new List<IQmBound>();
        private readonly List<QmSlider> _sliders = new List<QmSlider>();
        private Transform _grid;

        public QmPage(QmKit kit, string title, Action onBack)
        {
            _kit = kit;
            Transform template = kit.PageTemplate;
            Transform content = null;
            Button back = null;
            GameObject backdrop = null;
            ScrollRect scroll = null;
            Root = kit.Clone(template, template.parent, "VF_Page", page =>
            {
                page.SetActive(false);
                content = QmKit.FindContent(page.transform);
                QmUtil.ClearChildren(content);
                // Rows report their height to the list (the same single switch native pages use).
                VerticalLayoutGroup list = content.GetComponent<VerticalLayoutGroup>();
                if (list != null)
                    list.childControlHeight = true;

                Transform header = null;
                for (int i = 0; i < page.transform.childCount; i++)
                {
                    Transform child = page.transform.GetChild(i);
                    if (header == null && child.name.StartsWith("Header", StringComparison.Ordinal))
                        header = child;
                    else if (!content.IsChildOf(child))
                        child.gameObject.SetActive(false);
                }

                if (header != null)
                {
                    Transform rightItems = QmUtil.FindDeep(header, "RightItemContainer");
                    if (rightItems != null)
                        for (int i = 0; i < rightItems.childCount; i++)
                            rightItems.GetChild(i).gameObject.SetActive(false);
                    Transform titleT = QmUtil.FindDeep(header, "Text_Title");
                    QmUtil.SetText(titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : QmUtil.FirstText(header), title);
                    Transform backT = QmUtil.FindDeep(header, "Button_Back");
                    if (backT != null)
                    {
                        backT.gameObject.SetActive(true);
                        back = kit.EnsureButton(backT.gameObject);
                    }
                }

                scroll = SetUpScrolling(content);
                backdrop = AddBackdrop(page.transform);

                Canvas canvas = page.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.enabled = true;
                CanvasGroup cg = page.GetComponent<CanvasGroup>() ?? page.AddComponent<CanvasGroup>();
                cg.enabled = true;
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                GraphicRaycaster ray = page.GetComponent<GraphicRaycaster>();
                if (ray != null)
                    ray.enabled = true;
            });

            Content = content;
            _backdrop = backdrop;
            _scroll = scroll;
            _group = Root.GetComponent<CanvasGroup>();
            _rect = Root.GetComponent<RectTransform>();
            _basePos = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            Root.transform.SetAsLastSibling();

            if (back != null)
            {
                back.onClick = new Button.ButtonClickedEvent();
                back.interactable = true;
                back.onClick.AddListener((Action)onBack);
            }
            else
            {
                AddButton("Back", onBack, false);
            }
        }

        /// <summary>
        /// Native pages scroll through VRChat's own scroll component, which is removed with the rest of
        /// its scripts. A plain ScrollRect, RectMask2D and size fitter take its place.
        /// </summary>
        private static ScrollRect SetUpScrolling(Transform content)
        {
            Transform viewport = content.parent;
            Transform scrollRoot = viewport != null ? viewport.parent : null;
            if (scrollRoot == null)
                return null;

            RectMask2D mask = viewport.GetComponent<RectMask2D>();
            if (mask != null && mask.GetIl2CppType().FullName != "UnityEngine.UI.RectMask2D")
            {
                UnityEngine.Object.DestroyImmediate(mask);
                mask = null;
            }
            if (mask == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            var contentRect = content.GetComponent<RectTransform>();
            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                var fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);

            ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>() ?? scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 30f;
            Transform barT = QmUtil.FindDeep(scrollRoot, "Scrollbar");
            Scrollbar bar = barT != null ? barT.GetComponent<Scrollbar>() : null;
            if (bar != null)
            {
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }

            // A transparent hit area so drags on the gaps between rows still scroll.
            if (viewport.GetComponent<Graphic>() == null)
            {
                Image hit = viewport.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
            }
            return scroll;
        }

        /// <summary>
        /// A dark panel behind the controls. Off normally, because the native page underneath is
        /// hidden while ours is up; only used when there is no native page to hide.
        /// </summary>
        private static GameObject AddBackdrop(Transform page)
        {
            var go = new GameObject("VF_Backdrop");
            go.transform.SetParent(page, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            img.color = new Color(0.035f, 0.07f, 0.075f, 0.96f);
            go.SetActive(false);
            return go;
        }

        public bool Active => Root != null && Root.activeSelf;

        /// <summary>Activates the page, refreshes every control and resets the scroll to the top.</summary>
        public void Show(bool backdrop)
        {
            if (_backdrop != null)
                _backdrop.SetActive(backdrop);
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
            if (_scroll != null)
            {
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = 1f;
            }
            for (int i = 0; i < _sliders.Count; i++)
                _sliders[i].Resync();
            Refresh();
        }

        public void Hide()
        {
            if (Root != null)
                Root.SetActive(false);
            SetFade(1f);
        }

        /// <summary>0 = hidden, 1 = shown. Fades the page and slides it up a little.</summary>
        public void SetFade(float t)
        {
            if (_group != null)
            {
                _group.alpha = t;
                _group.interactable = t > 0.6f;
            }
            if (_rect != null)
                _rect.anchoredPosition = new Vector2(_basePos.x, _basePos.y - 16f * (1f - t));
        }

        public void Refresh()
        {
            for (int i = 0; i < _bound.Count; i++)
                _bound[i].Refresh();
        }

        /// <summary>Starts a titled section; following buttons and toggles go into a new grid.</summary>
        public void Header(string title)
        {
            if (_kit.HeaderTemplate != null)
            {
                GameObject h = _kit.Clone(_kit.HeaderTemplate, Content, "VF_Header", QmKit.KeepHeight);
                QmUtil.SetText(QmUtil.FirstText(h.transform), title);
            }
            _grid = null;
        }

        private Transform Grid()
        {
            if (_grid != null)
                return _grid;
            GameObject g = _kit.Clone(_kit.GroupTemplate, Content, "VF_Group", go => QmUtil.ClearChildren(go.transform));
            _grid = g.transform;
            return _grid;
        }

        public QmButton AddButton(string label, Action onClick, bool jump)
        {
            return new QmButton(_kit, Grid(), label, onClick, jump);
        }

        public void AddToggle(string label, MelonPreferences_Entry<bool> entry, Action changed)
        {
            _bound.Add(new QmToggle(_kit, Grid(), label, entry, changed));
        }

        public void AddCycle(string label, string[] options, MelonPreferences_Entry<int> entry, Action changed)
        {
            _bound.Add(new QmCycle(_kit, Grid(), label, options, entry, changed));
        }

        public void AddSlider(string label, MelonPreferences_Entry<float> entry, float min, float max, string format, Action changed)
        {
            bool native = _kit.SliderTemplate != null;
            if (native)
                _grid = null;
            var s = new QmSlider(_kit, Content, native ? null : Grid(), label, entry, min, max, format, changed);
            _bound.Add(s);
            _sliders.Add(s);
        }

        public void AddSlider(string label, MelonPreferences_Entry<int> entry, int min, int max, Action changed)
        {
            bool native = _kit.SliderTemplate != null;
            if (native)
                _grid = null;
            var s = new QmSlider(_kit, Content, native ? null : Grid(), label, entry, min, max, changed);
            _bound.Add(s);
            _sliders.Add(s);
        }

        public void Destroy()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }
    }
}
