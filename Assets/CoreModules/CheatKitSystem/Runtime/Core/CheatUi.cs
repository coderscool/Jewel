using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Factory UGUI tối giản cho cheat panel. Bọc <see cref="DefaultControls"/> (Unity tự dựng
    /// hierarchy widget CHUẨN — Button/Toggle/Slider/InputField đủ child graphics) rồi gắn
    /// <see cref="LayoutElement"/> với touch-target lớn (mobile). Dùng chung cho mọi module → DRY,
    /// và là điểm DUY NHẤT cần chỉnh nếu muốn đổi style toàn panel.
    /// </summary>
    public static class CheatUi
    {
        public const float RowHeight = 84f; // touch target tối thiểu cho mobile

        private static readonly DefaultControls.Resources Res = new DefaultControls.Resources();
        private static Font _font;
        private static Font Builtin => _font != null
            ? _font
            : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Hàng ngang chứa nhiều control (chia đều bề ngang).</summary>
        public static RectTransform Row(RectTransform parent, float height = RowHeight, float spacing = 8f)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            h.childControlWidth = true;
            h.childControlHeight = true;
            SetLayout(go, height);
            return rt;
        }

        public static Text Label(RectTransform parent, string text, int fontSize = 26, float height = 40f)
        {
            var go = DefaultControls.CreateText(Res);
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            EnsureFont(t);
            SetLayout(go, height);
            return t;
        }

        public static Button Button(RectTransform parent, string label, Color color, int fontSize = 26, float minWidth = 0f)
        {
            var go = DefaultControls.CreateButton(Res);
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (img != null) img.color = color;
            var t = go.GetComponentInChildren<Text>();
            if (t != null) { t.text = label; t.fontSize = fontSize; t.color = Color.white; EnsureFont(t); }
            SetLayout(go, RowHeight, minWidth, flexW: 1f);
            return go.GetComponent<Button>();
        }

        public static InputField Input(RectTransform parent, string placeholder, float minWidth = 90f)
        {
            var go = DefaultControls.CreateInputField(Res);
            go.transform.SetParent(parent, false);
            var inp = go.GetComponent<InputField>();
            if (inp.placeholder is Text ph) { ph.text = placeholder; ph.fontSize = 24; EnsureFont(ph); }
            if (inp.textComponent != null) { inp.textComponent.fontSize = 26; EnsureFont(inp.textComponent); }
            SetLayout(go, RowHeight, minWidth, flexW: 1f);
            return inp;
        }

        public static Toggle Toggle(RectTransform parent, string label, float height = 56f)
        {
            var go = DefaultControls.CreateToggle(Res);
            go.transform.SetParent(parent, false);
            var lbl = go.GetComponentInChildren<Text>();
            if (lbl != null) { lbl.text = label; lbl.fontSize = 24; lbl.color = Color.white; EnsureFont(lbl); }
            SetLayout(go, height);
            return go.GetComponent<Toggle>();
        }

        public static Slider Slider(RectTransform parent, float min, float max, float value, float height = 48f)
        {
            var go = DefaultControls.CreateSlider(Res);
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(value);
            SetLayout(go, height, flexW: 1f);
            return s;
        }

        private static void SetLayout(GameObject go, float height, float minWidth = 0f, float flexW = 0f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (minWidth > 0f) le.minWidth = minWidth;
            if (flexW > 0f) le.flexibleWidth = flexW;
        }

        private static void EnsureFont(Text t)
        {
            if (t != null && t.font == null) t.font = Builtin;
        }
    }
}
