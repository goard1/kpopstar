using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>코드로 uGUI를 만들기 위한 작은 도우미 + 색상표.</summary>
    public static class UIKit
    {
        public static readonly Color Paper = new Color(1f, 0.96f, 0.87f, 0.97f);
        public static readonly Color Card = new Color(1f, 0.98f, 0.94f, 1f);
        public static readonly Color Ink = new Color(0.23f, 0.18f, 0.16f);
        public static readonly Color Muted = new Color(0.52f, 0.46f, 0.41f);
        public static readonly Color Accent = new Color(0.90f, 0.33f, 0.24f);
        public static readonly Color Green = new Color(0.24f, 0.60f, 0.35f);
        public static readonly Color Blue = new Color(0.27f, 0.47f, 0.78f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color Slot = new Color(0.94f, 0.88f, 0.76f, 1f);

        public static readonly Color BubbleNormal = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color BubbleReady = new Color(1f, 0.93f, 0.55f, 1f);
        public static readonly Color BubbleUrgent = new Color(1f, 0.62f, 0.52f, 1f);
        public static readonly Color BubbleSad = new Color(0.82f, 0.85f, 0.92f, 0.95f);
        public static readonly Color BubbleMoney = new Color(0.62f, 0.90f, 0.62f, 1f);

        static Font font;

        /// <summary>한글이 나오는 폰트. OS 한글 폰트 → 없으면 내장 폰트(OS 대체 글꼴 사용).</summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                string[] wanted = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
                var installed = Font.GetOSInstalledFontNames();
                foreach (var name in wanted)
                    if (System.Array.IndexOf(installed, name) >= 0)
                        return font = Font.CreateDynamicFontFromOSFont(name, 40);
                return font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public static string Won(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + "원";

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>위쪽에 붙는 고정 높이 띠.</summary>
        public static void Top(RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(side, -y - height);
            rt.offsetMax = new Vector2(-side, -y);
        }

        /// <summary>아래쪽에 붙는 고정 높이 띠.</summary>
        public static void Bottom(RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(side, y);
            rt.offsetMax = new Vector2(-side, y + height);
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, int size, Color bg, Color fg, out Text label)
        {
            var img = Panel(name, parent, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Munbanggu.Core.GameEvents.RaiseUiClick);
            label = Label("Label", img.transform, text, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 8, 4, 8, 4);
            return btn;
        }

        public static LayoutElement Layout(Component c, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            return le;
        }
    }
}
