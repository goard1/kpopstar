using Munbanggu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 고양이 치즈 선물 룰렛. 칸은 같은 크기로 그리고(보기 좋게), 실제 확률은 CatRules 가중치.
    /// "돌리기!" → GameManager가 칸을 정하고 보상을 준 뒤, 바퀴가 그 칸에 멈추는 연출.
    /// </summary>
    public sealed class RoulettePanel
    {
        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        RectTransform wheel;
        Text result;
        Button spin, close;
        RouletteSpin spinner;

        RoulettePanel(GameManager gm, GameObject modal, UIJuice juice) { this.gm = gm; this.modal = modal; this.juice = juice; }

        public bool IsOpen => modal != null && modal.activeSelf;

        public static RoulettePanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new RoulettePanel(gm, modal, juice);
            p.BuildCard(card);
            return p;
        }

        public void Open()
        {
            bool ready = gm.CatGiftReady;
            result.text = ready ? "치즈가 물어 온 선물은 뭘까? 돌려 봐요!" : "오늘은 선물이 없어요. 밥을 주면 내일 아침에 물어 와요.";
            spin.interactable = ready;
            spin.gameObject.SetActive(true);
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        void BuildCard(RectTransform card)
        {
            var title = UIKit.Label("Title", card, "치즈의 선물 룰렛", 52, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Top(title.rectTransform, 80, 40, 30);

            // 바퀴
            float size = 760f;
            var wheelImg = new GameObject("Wheel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            wheelImg.transform.SetParent(card, false);
            wheelImg.sprite = MakeWheelSprite();
            wheelImg.raycastTarget = false;
            wheel = wheelImg.rectTransform;
            wheel.anchorMin = wheel.anchorMax = new Vector2(0.5f, 1f);
            wheel.pivot = new Vector2(0.5f, 0.5f);
            wheel.sizeDelta = new Vector2(size, size);
            wheel.anchoredPosition = new Vector2(0f, -160f - size * 0.5f);

            var slices = CatRules.Slices;
            float step = 360f / slices.Length;
            for (int i = 0; i < slices.Length; i++)
            {
                float c = (i + 0.5f) * step; // 위에서 시계 방향
                float rad = c * Mathf.Deg2Rad;
                var t = UIKit.Label("Slice", wheel, slices[i].label, 26, UIKit.Ink, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(180, 80);
                rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * size * 0.33f;
                rt.localRotation = Quaternion.Euler(0f, 0f, -c);
            }
            // 가운데 단추 (고양이 발바닥)
            var hub = UIKit.CardPanel("Hub", wheel, new Color(1f, 0.74f, 0.36f), 60, false, 6);
            UIKit.Round(hub, 60);
            var hrt = hub.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(120, 120);
            var paw = UIKit.Label("Paw", hub.transform, "냥", 44, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(paw.rectTransform);

            // 바늘 (위쪽, 아래를 가리키는 마름모)
            var pin = UIKit.Panel("Pointer", card, UIKit.Red, false);
            var prt = pin.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(64, 64);
            prt.anchoredPosition = new Vector2(0f, -170f);
            prt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            result = UIKit.Label("Result", card, "", 32, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Top(result.rectTransform, 110, 160 + size + 30, 50);

            spin = UIKit.Button("Spin", card, "돌리기!", 46, new Color(1f, 0.6f, 0.3f), Color.white, out _);
            var srt = (RectTransform)spin.transform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(0.5f, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.offsetMin = new Vector2(44, 40); srt.offsetMax = new Vector2(-8, 140);
            spin.onClick.AddListener(Spin);

            close = UIKit.Button("Close", card, "닫기", 42, UIKit.Red, Color.white, out _);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.5f, 0); crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(0.5f, 0);
            crt.offsetMin = new Vector2(8, 40); crt.offsetMax = new Vector2(-44, 140);
            close.onClick.AddListener(Close);

            spinner = wheel.gameObject.AddComponent<RouletteSpin>();
        }

        void Spin()
        {
            if (spinner.Spinning) return;
            int idx = gm.SpinCatRoulette(out string text);
            if (idx < 0) { result.text = "오늘은 선물이 없어요"; return; }
            spin.interactable = false;
            close.interactable = false;
            result.text = "두구두구두구…";
            float step = 360f / CatRules.Slices.Length;
            float target = (idx + 0.5f) * step + Random.Range(-step * 0.3f, step * 0.3f);
            spinner.SpinTo(target, 3.2f, () =>
            {
                result.text = text;
                close.interactable = true;
                if (juice != null) juice.Punch(0.08f);
                GameEvents.RaiseRushStarted();
            });
        }

        /// <summary>바퀴 그림: 같은 크기 칸 + 흰 테두리·칸선, 바깥은 투명.</summary>
        static Sprite MakeWheelSprite()
        {
            const int n = 512;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            var slices = CatRules.Slices;
            float step = 360f / slices.Length;
            float r0 = n * 0.5f - 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0, 0, 0, 0);
                    if (r <= r0)
                    {
                        float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // 위에서 시계 방향
                        if (ang < 0) ang += 360f;
                        int i = Mathf.Min(slices.Length - 1, (int)(ang / step));
                        c = slices[i].color;
                        float edge = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / step) * step));
                        if (edge * Mathf.Deg2Rad * r < 2.5f && r > 30f) c = Color.white;    // 칸선
                        if (r > r0 - 14f) c = Color.white;                                   // 바깥 테
                        if (r > r0 - 6f) c = new Color(1f, 0.6f, 0.3f);                      // 주황 테
                        c.a = Mathf.Clamp01(r0 - r + 0.5f);
                    }
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>바퀴 돌리기 (게임 속도와 상관없이 실제 시간으로).</summary>
    public class RouletteSpin : MonoBehaviour
    {
        float from, to, duration, t;
        System.Action done;
        public bool Spinning { get; private set; }

        public void SpinTo(float targetDeg, float seconds, System.Action onDone)
        {
            from = transform.localEulerAngles.z;
            to = targetDeg + 360f * 6f;
            while (to - from < 360f * 5f) to += 360f;
            duration = seconds;
            t = 0f;
            done = onDone;
            Spinning = true;
        }

        void Update()
        {
            if (!Spinning) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - k, 3f); // 끝에서 천천히
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, to, e));
            if (k >= 1f)
            {
                Spinning = false;
                var d = done;
                done = null;
                d?.Invoke();
            }
        }
    }
}
