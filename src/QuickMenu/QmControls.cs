using System;
using System.Globalization;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace VividFrame.QuickMenu
{
    /// <summary>A control that shows a setting and can re-read it when the setting changes elsewhere.</summary>
    internal interface IQmBound
    {
        void Refresh();
    }

    /// <summary>A native Quick Menu button with a centred label.</summary>
    internal sealed class QmButton
    {
        public readonly GameObject Root;
        public readonly Button Button;
        public readonly TextMeshProUGUI Label;

        public QmButton(QmKit kit, Transform group, string label, Action onClick, bool jump)
        {
            Root = kit.Clone(kit.ButtonTemplate, group, "VF_Button", go =>
            {
                QmKit.HideBadges(go.transform);
                if (jump)
                {
                    Transform badge = QmUtil.FindDeep(go.transform, "Badge_MMJump");
                    if (badge != null)
                        badge.gameObject.SetActive(true);
                }
                // Text-only button: hide the template's icon and centre the label, as native text buttons do.
                Transform icons = QmUtil.FindDeep(go.transform, "Icons");
                if (icons != null)
                    icons.gameObject.SetActive(false);
                TextMeshProUGUI text = QmUtil.FirstText(go.transform);
                if (text != null)
                {
                    RectTransform rt = text.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = new Vector2(8f, 6f);
                    rt.offsetMax = new Vector2(-8f, -6f);
                    text.alignment = TextAlignmentOptions.Center;
                }
            });
            Button = kit.EnsureButton(Root);
            Button.onClick = new Button.ButtonClickedEvent();
            Button.interactable = true;
            Label = QmUtil.FirstText(Root.transform);
            QmUtil.SetText(Label, label);
            if (onClick != null)
                Button.onClick.AddListener((Action)onClick);
        }

        public void SetLabel(string text)
        {
            QmUtil.SetText(Label, text);
        }
    }

    /// <summary>
    /// A toggle bound to a bool setting. Uses a native settings toggle (Icon_On / Icon_Off) when one
    /// exists, otherwise a native button whose icon shows the Quick Menu's check or cross.
    /// </summary>
    internal sealed class QmToggle : IQmBound
    {
        private readonly MelonPreferences_Entry<bool> _entry;
        private readonly Action _changed;
        private readonly GameObject _onIcon;
        private readonly GameObject _offIcon;
        private readonly Image _icon;
        private readonly Sprite _onSprite;
        private readonly Sprite _offSprite;
        private readonly Color _iconColor;
        private bool _shown;

        public QmToggle(QmKit kit, Transform group, string label, MelonPreferences_Entry<bool> entry, Action changed)
        {
            _entry = entry;
            _changed = changed;
            GameObject go;
            if (kit.ToggleTemplate != null)
            {
                go = kit.Clone(kit.ToggleTemplate, group, "VF_Toggle", g =>
                {
                    // Our click handler owns the state; a Unity Toggle would flip it a second time.
                    Toggle t = g.GetComponent<Toggle>();
                    if (t != null)
                        UnityEngine.Object.DestroyImmediate(t);
                    QmKit.HideBadges(g.transform);
                });
                _onIcon = QmUtil.FindDeep(go.transform, "Icon_On").gameObject;
                _offIcon = QmUtil.FindDeep(go.transform, "Icon_Off").gameObject;
            }
            else
            {
                kit.FindToggleSprites();
                go = kit.Clone(kit.ButtonTemplate, group, "VF_Toggle", g => QmKit.HideBadges(g.transform));
                Transform iconT = QmUtil.FindDeep(go.transform, "Icon");
                _icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (_icon != null)
                {
                    _icon.gameObject.SetActive(true);
                    _icon.preserveAspect = true;
                    _iconColor = _icon.color;
                }
                _onSprite = kit.OnSprite;
                _offSprite = kit.OffSprite;
            }
            QmUtil.SetText(QmUtil.FirstText(go.transform), label);
            Button b = kit.EnsureButton(go);
            b.onClick = new Button.ButtonClickedEvent();
            b.interactable = true;
            b.onClick.AddListener((Action)Flip);
            Show(entry.Value);
        }

        private void Flip()
        {
            _entry.Value = !_entry.Value;
            _changed();
            Show(_entry.Value);
        }

        private void Show(bool v)
        {
            _shown = v;
            if (_onIcon != null)
            {
                _onIcon.SetActive(v);
                _offIcon.SetActive(!v);
            }
            else if (_icon != null)
            {
                Sprite s = v ? _onSprite : _offSprite;
                _icon.sprite = s;
                _icon.overrideSprite = s;
                _icon.color = new Color(_iconColor.r, _iconColor.g, _iconColor.b, v ? _iconColor.a : _iconColor.a * 0.45f);
            }
        }

        public void Refresh()
        {
            bool v = _entry.Value;
            if (v != _shown)
                Show(v);
        }
    }

    /// <summary>A button that steps through the options of an int setting and shows the current one.</summary>
    internal sealed class QmCycle : IQmBound
    {
        private readonly MelonPreferences_Entry<int> _entry;
        private readonly Action _changed;
        private readonly QmButton _button;
        private readonly string[] _labels;
        private int _shown = int.MinValue;

        public QmCycle(QmKit kit, Transform group, string label, string[] options, MelonPreferences_Entry<int> entry, Action changed)
        {
            _entry = entry;
            _changed = changed;
            _labels = new string[options.Length];
            for (int i = 0; i < options.Length; i++)
                _labels[i] = label + "\n<size=75%><alpha=#B3>" + options[i] + "</size>";
            _button = new QmButton(kit, group, label, Next, false);
            Refresh();
        }

        private void Next()
        {
            int v = _entry.Value + 1;
            if (v < 0 || v >= _labels.Length)
                v = 0;
            _entry.Value = v;
            _changed();
            Refresh();
        }

        public void Refresh()
        {
            int v = _entry.Value;
            if (v == _shown)
                return;
            _shown = v;
            _button.SetLabel(_labels[Mathf.Clamp(v, 0, _labels.Length - 1)]);
        }
    }

    /// <summary>
    /// A native slider row bound to a float or int setting. While the user drags, the setting
    /// follows the slider and outside resyncs pause, so the handle never jumps back.
    /// Without a slider template it becomes minus / value / plus buttons.
    /// </summary>
    internal sealed class QmSlider : IQmBound
    {
        private const float DragHold = 0.35f;

        private readonly MelonPreferences_Entry<float> _float;
        private readonly MelonPreferences_Entry<int> _int;
        private readonly float _min;
        private readonly float _max;
        private readonly string _format;
        private readonly Action _changed;
        private readonly Slider _slider;
        private readonly TextMeshProUGUI _value;
        private readonly QmButton _fallbackValue;
        private readonly string _label;
        private float _shown = float.NaN;
        private float _lastUserChange = -10f;

        public QmSlider(QmKit kit, Transform content, Transform fallbackGroup, string label, MelonPreferences_Entry<float> entry,
            float min, float max, string format, Action changed)
            : this(kit, content, fallbackGroup, label, entry, null, min, max, format, changed)
        {
        }

        public QmSlider(QmKit kit, Transform content, Transform fallbackGroup, string label, MelonPreferences_Entry<int> entry,
            int min, int max, Action changed)
            : this(kit, content, fallbackGroup, label, null, entry, min, max, "0", changed)
        {
        }

        private QmSlider(QmKit kit, Transform content, Transform fallbackGroup, string label, MelonPreferences_Entry<float> f,
            MelonPreferences_Entry<int> n, float min, float max, string format, Action changed)
        {
            _float = f;
            _int = n;
            _min = min;
            _max = max;
            _format = format;
            _changed = changed;
            _label = label;

            if (kit.SliderTemplate != null)
            {
                GameObject row = kit.Clone(kit.SliderTemplate, content, "VF_Slider", r =>
                {
                    // The row's side button belongs to VRChat's setting; ours has no use for it.
                    for (int i = 0; i < r.transform.childCount; i++)
                    {
                        Transform child = r.transform.GetChild(i);
                        if (child.name == "Button" && child.GetComponentInChildren<Slider>(true) == null)
                            child.gameObject.SetActive(false);
                    }
                    Transform rightC = QmUtil.FindDeep(r.transform, "RightItemContainer");
                    if (rightC != null)
                        for (int i = 0; i < rightC.childCount; i++)
                        {
                            Transform child = rightC.GetChild(i);
                            if (child.name == "Button")
                                child.gameObject.SetActive(false);
                        }
                    QmKit.KeepHeight(r);
                });
                _slider = row.GetComponentInChildren<Slider>(true);
                Transform left = QmUtil.FindDeep(row.transform, "LeftItemContainer");
                Transform titleT = left != null ? QmUtil.FindDeep(left, "Title") : null;
                QmUtil.SetText(titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : QmUtil.FirstText(left != null ? left : row.transform), label);
                Transform right = QmUtil.FindDeep(row.transform, "RightItemContainer");
                Transform valueText = QmUtil.FindDeep(row.transform, "Text_MM_H3");
                _value = valueText != null ? valueText.GetComponent<TextMeshProUGUI>() : (right != null ? QmUtil.FirstText(right) : null);
                if (_value != null)
                    _value.gameObject.SetActive(true);

                _slider.onValueChanged = new Slider.SliderEvent();
                _slider.interactable = true;
                _slider.wholeNumbers = false;
                _slider.minValue = min;
                _slider.maxValue = max;
                _slider.wholeNumbers = n != null;
                ForceShow();
                _slider.onValueChanged.AddListener((Action<float>)OnSlide);
            }
            else
            {
                float step = n != null ? 1f : (max - min) / 20f;
                new QmButton(kit, fallbackGroup, label + "\n<size=75%>-</size>", () => Nudge(-step), false);
                _fallbackValue = new QmButton(kit, fallbackGroup, label, null, false);
                new QmButton(kit, fallbackGroup, label + "\n<size=75%>+</size>", () => Nudge(step), false);
                ForceShow();
            }
        }

        private float Read()
        {
            return _int != null ? _int.Value : _float.Value;
        }

        private void Write(float v)
        {
            v = Mathf.Clamp(v, _min, _max);
            if (_int != null)
            {
                int iv = Mathf.RoundToInt(v);
                if (_int.Value == iv)
                    return;
                _int.Value = iv;
            }
            else
            {
                if (_float.Value == v)
                    return;
                _float.Value = v;
            }
            _changed();
        }

        private void OnSlide(float v)
        {
            _lastUserChange = Time.unscaledTime;
            Write(v);
            ShowValue(Read());
        }

        private void Nudge(float delta)
        {
            Write(Read() + delta);
            ForceShow();
        }

        private void ShowValue(float v)
        {
            if (v == _shown)
                return;
            _shown = v;
            string text = v.ToString(_format, CultureInfo.InvariantCulture);
            if (_value != null)
                QmUtil.SetText(_value, text);
            else if (_fallbackValue != null)
                _fallbackValue.SetLabel(_label + "\n<size=75%><alpha=#B3>" + text + "</size>");
        }

        private void ForceShow()
        {
            float v = Read();
            if (_slider != null)
                _slider.SetValueWithoutNotify(v);
            ShowValue(v);
        }

        public void Refresh()
        {
            // Hands off while the user is dragging; the setting already follows the handle.
            if (Time.unscaledTime - _lastUserChange < DragHold)
                return;
            float v = Read();
            if (v == _shown)
                return;
            ForceShow();
        }

        /// <summary>Re-reads the setting regardless of drag state, when a page opens.</summary>
        public void Resync()
        {
            _lastUserChange = -10f;
            float v = Read();
            if (_slider != null && _slider.value != v)
                _slider.SetValueWithoutNotify(v);
            ShowValue(v);
        }
    }
}
