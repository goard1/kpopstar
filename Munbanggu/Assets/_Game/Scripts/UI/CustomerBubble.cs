using System;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>손님 머리 위 말풍선. 계산 가능할 때는 탭하는 버튼이 된다.</summary>
    public class CustomerBubble : MonoBehaviour
    {
        public Action onClick;

        Image bg;
        Text label;
        RectTransform fill;
        GameObject fillRoot;
        Button button;
        string lastText;
        float flashTimer;
        bool interactable;

        public void Setup(Image background, Text text, GameObject fillBar, RectTransform fillRect, Button btn)
        {
            bg = background;
            label = text;
            fillRoot = fillBar;
            fill = fillRect;
            button = btn;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            gameObject.SetActive(false);
        }

        /// <summary>계속 보여줌 (매 프레임 호출해도 됨). fill01이 0보다 작으면 인내심 바 숨김.</summary>
        public void Show(string text, Color color, bool canTap, float fill01 = -1f)
        {
            flashTimer = 0f;
            Apply(text, color, canTap, fill01);
        }

        /// <summary>잠깐 보여주고 사라짐.</summary>
        public void Flash(string text, Color color, float seconds)
        {
            Apply(text, color, false, -1f);
            flashTimer = seconds;
        }

        public void Hide()
        {
            if (flashTimer > 0f) return;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Apply(string text, Color color, bool canTap, float fill01)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (lastText != text) { label.text = text; lastText = text; }
            bg.color = color;
            interactable = canTap;
            button.interactable = canTap;
            bg.raycastTarget = canTap;
            bool showFill = fill01 >= 0f;
            if (fillRoot.activeSelf != showFill) fillRoot.SetActive(showFill);
            if (showFill) fill.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
        }

        void Update()
        {
            transform.localScale = Vector3.one * (interactable ? 1.08f + Mathf.Sin(Time.time * 8f) * 0.05f : 1f);
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) gameObject.SetActive(false);
            }
        }
    }
}
