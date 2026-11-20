using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// UI 손맛: 누르면 쏙 들어가는 스케일, 펀치(통통 튀기), 흔들기, 선택 시 살짝 커지기.
    /// 하나의 컴포넌트가 localScale·anchoredPosition을 모두 맡아서 서로 덮어쓰지 않게 한다.
    /// 할 일이 없으면 Update에서 바로 빠져나감 (할당 없음).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool pressable;
        public float pressScale = 0.92f;
        /// <summary>평소 크기 (선택된 카드는 1.06 등). 부드럽게 따라감.</summary>
        public float restScale = 1f;

        const float PunchDuration = 0.42f;
        const float ShakeDuration = 0.38f;

        RectTransform rt;
        Selectable selectable;
        bool down;
        float press;
        float shownRest = -1f;
        float punchTime = -1f, punchAmp;
        float shakeTime = -1f, shakeAmp;
        Vector2 shakeOrigin;

        RectTransform Rt => rt != null ? rt : rt = (RectTransform)transform;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!pressable) return;
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;
            down = true;
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        void Release()
        {
            if (!down) return;
            down = false;
            Punch(0.07f); // 손을 떼면 살짝 튕겨 나옴
        }

        /// <summary>통통 튀기기. amp=0.2면 20% 커졌다 돌아옴.</summary>
        public void Punch(float amp = 0.2f)
        {
            punchTime = 0f;
            punchAmp = amp;
        }

        /// <summary>좌우로 흔들기 (anchoredPosition 기준, 레이아웃 그룹 자식에는 쓰지 말 것).</summary>
        public void Shake(float amp = 18f)
        {
            if (shakeTime < 0f) shakeOrigin = Rt.anchoredPosition;
            shakeTime = 0f;
            shakeAmp = amp;
        }

        /// <summary>흔들기 중이면 원래 자리로 돌려놓음 (위치를 바꾸기 전에 호출).</summary>
        public void StopShake()
        {
            if (shakeTime < 0f) return;
            shakeTime = -1f;
            Rt.anchoredPosition = shakeOrigin;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (shownRest < 0f) shownRest = restScale;

            bool idle = !down && press <= 0f && punchTime < 0f && shakeTime < 0f
                        && Mathf.Abs(shownRest - restScale) < 0.0005f;
            if (idle)
            {
                if (shownRest != restScale)
                {
                    shownRest = restScale;
                    Rt.localScale = new Vector3(restScale, restScale, 1f);
                }
                return;
            }

            press = Mathf.MoveTowards(press, down ? 1f : 0f, dt * (down ? 16f : 9f));
            shownRest = Mathf.Lerp(shownRest, restScale, 1f - Mathf.Exp(-dt * 14f));
            if (Mathf.Abs(shownRest - restScale) < 0.0005f) shownRest = restScale;

            float s = shownRest * Mathf.Lerp(1f, pressScale, press);
            if (punchTime >= 0f)
            {
                punchTime += dt;
                float k = punchTime / PunchDuration;
                if (k >= 1f) punchTime = -1f;
                else s *= 1f + punchAmp * Mathf.Sin(k * Mathf.PI * 2.5f) * (1f - k);
            }
            Rt.localScale = new Vector3(s, s, 1f);

            if (shakeTime >= 0f)
            {
                shakeTime += dt;
                float k = shakeTime / ShakeDuration;
                if (k >= 1f) StopShake();
                else Rt.anchoredPosition = shakeOrigin + new Vector2(Mathf.Sin(shakeTime * 70f) * shakeAmp * (1f - k), 0f);
            }
        }

        /// <summary>애니메이션 없이 즉시 평소 크기로 (스크린샷·초기화용).</summary>
        public void Snap()
        {
            StopShake();
            down = false;
            press = 0f;
            punchTime = -1f;
            shownRest = restScale;
            Rt.localScale = new Vector3(restScale, restScale, 1f);
        }

        /// <summary>없으면 붙여서 돌려줌.</summary>
        public static UIJuice Of(Component c)
        {
            var j = c.GetComponent<UIJuice>();
            return j != null ? j : c.gameObject.AddComponent<UIJuice>();
        }
    }
}
