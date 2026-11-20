using System;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>손님 머리 위 말풍선. 계산 가능할 때는 탭하는 버튼이 된다.</summary>
    public class CustomerBubble : MonoBehaviour
    {
        public Action onClick;

        /// <summary>지금 탭해서 계산할 수 있는 상태인지 (튜토리얼 화살표용).</summary>
        public bool CanTap => interactable && gameObject.activeInHierarchy;

        const float MinWidth = 150f, MaxWidth = 560f, TextPadding = 52f, IconPad = 74f;
        const float PopDuration = 0.28f;

        Image bg;
        Text label;
        RectTransform fill;
        GameObject fillRoot;
        Button button;
        Image tail;
        Image icon;
        GameObject tapBadge;
        RectTransform root;
        string lastText;
        float flashTimer;
        bool interactable;
        float appear = 1f;

        public void Setup(Image background, Text text, GameObject fillBar, RectTransform fillRect, Button btn)
        {
            bg = background;
            label = text;
            fillRoot = fillBar;
            fill = fillRect;
            button = btn;
            root = (RectTransform)transform;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            gameObject.SetActive(false);
        }

        /// <summary>꼬리·"탭!" 스티커 연결 (HUD가 만든 장식).</summary>
        public void SetDecor(Image tailFill, GameObject tapSticker)
        {
            tail = tailFill;
            tapBadge = tapSticker;
        }

        /// <summary>말풍선 왼쪽 상품 아이콘 자리 연결 (HUD가 만든 이미지).</summary>
        public void SetIconImage(Image image)
        {
            icon = image;
            if (icon != null) icon.enabled = false;
        }

        /// <summary>말풍선에 보여줄 상품 아이콘 (null이면 숨김). 글자는 아이콘만큼 오른쪽으로 밀린다.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon == null || (icon.sprite == sprite && icon.enabled == (sprite != null))) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(sprite != null ? 14f + IconPad : 14f, lrt.offsetMin.y);
            if (lastText != null) Resize();
        }

        void Resize()
        {
            float pad = TextPadding + (icon != null && icon.enabled ? IconPad : 0f);
            float w = Mathf.Clamp(label.preferredWidth + pad, MinWidth, MaxWidth + IconPad);
            if (root != null && Mathf.Abs(root.sizeDelta.x - w) > 0.5f) root.sizeDelta = new Vector2(w, root.sizeDelta.y);
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
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                appear = 0f; // 뿅 하고 나타나기
            }
            if (lastText != text)
            {
                if (lastText != null && appear >= 1f) appear = 0.55f; // 내용이 바뀌면 살짝 다시 튐
                label.text = text;
                lastText = text;
                Resize();
            }
            if (bg.color != color)
            {
                bg.color = color;
                if (tail != null) tail.color = color;
            }
            interactable = canTap;
            button.interactable = canTap;
            bg.raycastTarget = canTap;
            if (tapBadge != null && tapBadge.activeSelf != canTap) tapBadge.SetActive(canTap);
            bool showFill = fill01 >= 0f;
            if (fillRoot.activeSelf != showFill) fillRoot.SetActive(showFill);
            if (showFill) fill.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
        }

        void Update()
        {
            float s = interactable ? 1.08f + Mathf.Sin(Time.time * 8f) * 0.05f : 1f;
            if (appear < 1f)
            {
                appear = Mathf.Min(1f, appear + Time.unscaledDeltaTime / PopDuration);
                s *= BackOut(appear);
            }
            transform.localScale = new Vector3(s, s, 1f);
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) gameObject.SetActive(false);
            }
        }

        /// <summary>살짝 넘쳤다 돌아오는 곡선 (0→1).</summary>
        static float BackOut(float t)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
