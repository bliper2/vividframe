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

    /// <summary>A native-looking Quick Menu button.</summary>
    internal sealed class QmButton
    {
        public readonly GameObject Root;
        public readonly Button Button;
        public readonly TextMeshProUGUI Label;

        public QmButton(QmKit kit, Transform group, string label, Action onClick, bool jump)
        {
            Root = kit.Clone(kit.ButtonTemplate, group, "VF_Button");
            Button = Root.GetComponent<Button>() ?? Root.AddComponent<Button>();
            Button.onClick = new Button.ButtonClickedEvent();
            Button.interactable = true;
            Label = QmUtil.FirstText(Root.transform);
            QmKit.HideBadges(Root.transform);
            if (jump)
            {
                Transform badge = QmUtil.FindDeep(Root.transform, "Badge_MMJump");
                if (badge != null)
                    badge.gameObject.SetActive(true);
            }
            // Text-only button: drop the template's icon and centre the label like native text buttons.
            Transform icons = QmUtil.FindDeep(Root.transform, "Icons");
            if (icons != null)
                icons.gameObject.SetActive(false);
            if (Label != null)
                Label.transform.localPosition = Vector3.zero;
            QmUtil.SetText(Label, label);
            if (onClick != null)
                Button.onClick.AddListener((Action)onClick);
        }

        public void SetLabel(string text)
        {
            QmUtil.SetText(Label, text);
        }
    }

    /// <summary>A toggle bound to a bool setting. Falls back to a button with an On/Off label.</summary>
    internal sealed class QmToggle : IQmBound
    {
        private readonly MelonPreferences_Entry<bool> _entry;
        private readonly Action _changed;
        private readonly Toggle _toggle;
        private readonly QmButton _fallback;
        private readonly string _onLabel;
        private readonly string _offLabel;
        private bool _shown;

        public QmToggle(QmKit kit, Transform group, string label, MelonPreferences_Entry<bool> entry, Action changed)
        {
            _entry = entry;
            _changed = changed;
            _shown = entry.Value;
            if (kit.ToggleTemplate != null)
            {
                GameObject go = kit.Clone(kit.ToggleTemplate, group, "VF_Toggle");
                _toggle = go.GetComponent<Toggle>() ?? go.AddComponent<Toggle>();
                _toggle.onValueChanged = new Toggle.ToggleEvent();
                _toggle.group = null;
                _toggle.interactable = true;
                QmKit.HideBadges(go.transform);
                QmUtil.SetText(QmUtil.FirstText(go.transform), label);
                _toggle.SetIsOnWithoutNotify(_shown);
                _toggle.onValueChanged.AddListener((Action<bool>)OnToggle);
            }
            else
            {
                _onLabel = label + "\n<size=70%><color=#7FE0D0>On</color></size>";
                _offLabel = label + "\n<size=70%><color=#8A9A9A>Off</color></size>";
                _fallback = new QmButton(kit, group, _shown ? _onLabel : _offLabel, Flip, false);
            }
        }

        private void OnToggle(bool value)
        {
            _shown = value;
            if (_entry.Value == value)
                return;
            _entry.Value = value;
            _changed();
        }

        private void Flip()
        {
            _entry.Value = !_entry.Value;
            _changed();
            Refresh();
        }

        public void Refresh()
        {
            bool v = _entry.Value;
            if (v == _shown)
                return;
            _shown = v;
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(v);
            else
                _fallback.SetLabel(v ? _onLabel : _offLabel);
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
                _labels[i] = label + "\n<size=70%><color=#7FE0D0>" + options[i] + "</color></size>";
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
    /// A slider row bound to a float or int setting. Without a slider template it becomes a row of
    /// minus / value / plus buttons.
    /// </summary>
    internal sealed class QmSlider : IQmBound
    {
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
                GameObject row = kit.Clone(kit.SliderTemplate, content, "VF_Slider");
                _slider = row.GetComponentInChildren<Slider>(true);
                Transform left = QmUtil.FindDeep(row.transform, "LeftItemContainer");
                QmUtil.SetText(QmUtil.FirstText(left != null ? left : row.transform), label);
                Transform right = QmUtil.FindDeep(row.transform, "RightItemContainer");
                Transform valueText = QmUtil.FindDeep(row.transform, "Text_MM_H3");
                _value = valueText != null ? valueText.GetComponent<TextMeshProUGUI>() : (right != null ? QmUtil.FirstText(right) : null);
                // The native row's side button belongs to VRChat's setting; ours has no use for it.
                for (int i = 0; i < row.transform.childCount; i++)
                {
                    Transform child = row.transform.GetChild(i);
                    if (child.name == "Button" && child.GetComponentInChildren<Slider>(true) == null)
                        child.gameObject.SetActive(false);
                }
                _slider.onValueChanged = new Slider.SliderEvent();
                _slider.interactable = true;
                _slider.minValue = min;
                _slider.maxValue = max;
                _slider.wholeNumbers = n != null;
                Refresh();
                _slider.onValueChanged.AddListener((Action<float>)OnSlide);
            }
            else
            {
                float step = n != null ? 1f : (max - min) / 20f;
                new QmButton(kit, fallbackGroup, label + "\n<size=70%>-</size>", () => Nudge(-step), false);
                _fallbackValue = new QmButton(kit, fallbackGroup, label, null, false);
                new QmButton(kit, fallbackGroup, label + "\n<size=70%>+</size>", () => Nudge(step), false);
                Refresh();
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
                if (Mathf.Approximately(_float.Value, v))
                    return;
                _float.Value = v;
            }
            _changed();
        }

        private void OnSlide(float v)
        {
            Write(v);
            ShowValue(Read());
        }

        private void Nudge(float delta)
        {
            Write(Read() + delta);
            Refresh();
        }

        private void ShowValue(float v)
        {
            _shown = v;
            string text = v.ToString(_format, CultureInfo.InvariantCulture);
            if (_value != null)
                QmUtil.SetText(_value, text);
            else if (_fallbackValue != null)
                _fallbackValue.SetLabel(_label + "\n<size=70%><color=#7FE0D0>" + text + "</color></size>");
        }

        public void Refresh()
        {
            float v = Read();
            if (v == _shown)
                return;
            if (_slider != null)
                _slider.SetValueWithoutNotify(v);
            ShowValue(v);
        }
    }
}
