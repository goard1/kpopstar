using System.Collections.Generic;
using Munbanggu.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.Customers
{
    /// <summary>
    /// 단골 대사·아침 소식을 보여 주는 작은 카드 (화면 위쪽, 상단 바 아래).
    /// 게임을 멈추지 않음: 카드만 탭을 받고(다음 대사로), 시간이 지나면 저절로 넘어간다.
    /// HUD 모달 위에 보이도록 별도 캔버스(정렬 20)를 쓴다.
    /// </summary>
    public class DialoguePanel : MonoBehaviour
    {
        struct Entry
        {
            public string speaker;
            public string text;
            public Color color;
            public string initial;
        }

        const float FadeTime = 0.18f;
        const float SlidePixels = 40f;

        readonly Queue<Entry> queue = new Queue<Entry>();
        RectTransform card;
        CanvasGroup group;
        Image portrait;
        Text initialText, nameText, bodyText;
        float timer;      // 남은 표시 시간
        float fade;       // 0~1
        bool showing;
        bool closing;
        float baseY;

        public bool IsShowing => showing || queue.Count > 0;

        public static DialoguePanel Build(Transform parent)
        {
            var go = new GameObject("Dialogue Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<GraphicRaycaster>();
            var panel = go.AddComponent<DialoguePanel>();
            panel.Setup();
            return panel;
        }

        void Setup()
        {
            var safe = UIKit.Rect("Safe Area", transform);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var bg = UIKit.Panel("Card", safe, UIKit.Card);
            card = bg.rectTransform;
            UIKit.Top(card, 250, 205, 28);
            baseY = card.anchoredPosition.y;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Next);
            group = bg.gameObject.AddComponent<CanvasGroup>();

            // 테두리 느낌의 아래 그림자 띠
            var shade = UIKit.Panel("Shade", card, new Color(0f, 0f, 0f, 0.10f), false);
            UIKit.Bottom(shade.rectTransform, 8);

            portrait = UIKit.Panel("Portrait", card, Color.gray, false);
            var prt = portrait.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(150, 150);
            prt.anchoredPosition = new Vector2(28, 0);
            initialText = UIKit.Label("Initial", portrait.transform, "", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(initialText.rectTransform);

            nameText = UIKit.Label("Name", card, "", 34, UIKit.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(nameText.rectTransform, 204, 0, 28, 24);
            bodyText = UIKit.Label("Body", card, "", 32, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(bodyText.rectTransform, 204, 40, 32, 76);
            bodyText.lineSpacing = 1.1f;

            var hint = UIKit.Label("Hint", card, "탭 ▶", 24, UIKit.Muted, TextAnchor.LowerRight);
            UIKit.Stretch(hint.rectTransform, 0, 16, 28, 0);

            card.gameObject.SetActive(false);
        }

        /// <summary>대사를 줄에 넣는다. initial이 null이면 speaker 첫 글자.</summary>
        public void Show(string speaker, string text, Color color, string initial = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            queue.Enqueue(new Entry
            {
                speaker = speaker,
                text = text,
                color = color,
                initial = initial ?? (string.IsNullOrEmpty(speaker) ? "" : speaker.Substring(0, 1)),
            });
        }

        public void Clear()
        {
            queue.Clear();
            showing = false;
            closing = false;
            if (card != null) card.gameObject.SetActive(false);
        }

        void Next()
        {
            if (showing && !closing) closing = true;
        }

        void Begin(Entry e)
        {
            nameText.text = e.speaker;
            nameText.color = e.color;
            bodyText.text = e.text;
            portrait.color = e.color;
            initialText.text = e.initial;
            timer = Mathf.Clamp(2.5f + e.text.Length * 0.07f, 3.5f, 7f);
            fade = 0f;
            showing = true;
            closing = false;
            card.gameObject.SetActive(true);
            Apply();
        }

        void Update()
        {
            if (!showing)
            {
                if (queue.Count > 0) Begin(queue.Dequeue());
                return;
            }

            if (closing)
            {
                fade -= Time.unscaledDeltaTime / FadeTime;
                if (fade <= 0f)
                {
                    showing = false;
                    card.gameObject.SetActive(false);
                    if (queue.Count > 0) Begin(queue.Dequeue());
                    return;
                }
            }
            else
            {
                if (fade < 1f) fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / FadeTime);
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f) closing = true;
            }
            Apply();
        }

        void Apply()
        {
            group.alpha = fade;
            var p = card.anchoredPosition;
            p.y = baseY + (1f - fade) * SlidePixels;
            card.anchoredPosition = p;
        }
    }
}
