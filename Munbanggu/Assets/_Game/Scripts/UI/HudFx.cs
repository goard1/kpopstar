using System;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 화면 연출: 동전 날아가기, "+N" 떠오르는 글자, 상단 띠 배너.
    /// 오브젝트는 미리 만들어 두고 돌려씀 (Update에서 할당 없음).
    /// </summary>
    public class HudFx : MonoBehaviour
    {
        struct Coin
        {
            public RectTransform rt;
            public Vector2 from, ctrl, to;
            public float t, duration;
            public int value;
            public bool last, active;
        }

        struct Pop
        {
            public Text text;
            public RectTransform rt;
            public Vector2 from;
            public float t;
            public bool active;
        }

        const int CoinPool = 28;
        const int PopPool = 10;
        const float PopDuration = 0.95f;
        const float BannerIn = 0.38f, BannerHold = 1.7f, BannerOut = 0.32f;

        /// <summary>동전 하나 도착 (값, 마지막 동전인지). 돈 숫자·펀치는 HUD가 처리.</summary>
        public Action<int, bool> onCoinArrived;
        /// <summary>동전이 날아갈 곳 (돈 아이콘).</summary>
        public RectTransform coinTarget;

        RectTransform layer;
        Canvas canvas;
        Camera worldCam;
        readonly Coin[] coins = new Coin[CoinPool];
        readonly Pop[] pops = new Pop[PopPool];

        RectTransform banner;
        Image bannerBg;
        Text bannerTitle, bannerSub;
        UIJuice bannerJuice;
        float bannerT = -1f;
        bool bannerLanded;

        Camera UiCam => canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        public void Init(RectTransform fxLayer, RectTransform bannerParent, Canvas rootCanvas, Camera shopCamera)
        {
            layer = fxLayer;
            canvas = rootCanvas;
            worldCam = shopCamera;

            for (int i = 0; i < CoinPool; i++)
            {
                var img = UIKit.Coin(layer, 62f);
                UIKit.AddShadow(img, UIKit.ShadowColor, 5f);
                img.gameObject.SetActive(false);
                coins[i].rt = img.rectTransform;
            }
            for (int i = 0; i < PopPool; i++)
            {
                var t = UIKit.Label("Pop", layer, "", 54, UIKit.Blue, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.sizeDelta = new Vector2(400, 90);
                UIKit.AddOutline(t, Color.white, 4f);
                UIKit.AddShadow(t, UIKit.ShadowColor, 5f);
                t.gameObject.SetActive(false);
                pops[i].text = t;
                pops[i].rt = t.rectTransform;
            }
            BuildBanner(bannerParent);
        }

        void BuildBanner(RectTransform parent)
        {
            bannerBg = UIKit.CardPanel("Banner", parent, UIKit.Red, 34, false, 12);
            banner = bannerBg.rectTransform;
            UIKit.Place(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(900, 170));
            banner.localRotation = Quaternion.Euler(0, 0, -3f);
            UIKit.Ring(banner, new Color(1, 1, 1, 0.92f), 34, -10);
            UIKit.TapeStrip(banner, new Vector2(0, 1), new Vector2(60, -4), 14);
            UIKit.TapeStrip(banner, new Vector2(1, 1), new Vector2(-60, -4), -12);
            bannerTitle = UIKit.Label("Title", banner, "", 76, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(bannerTitle.rectTransform, 20, 52, 20, 8);
            UIKit.AddShadow(bannerTitle, new Color(0, 0, 0, 0.3f), 5f);
            bannerSub = UIKit.Label("Sub", banner, "", 32, UIKit.Yellow, TextAnchor.MiddleCenter);
            UIKit.Stretch(bannerSub.rectTransform, 20, 14, 20, 112);
            bannerJuice = banner.gameObject.AddComponent<UIJuice>();
            banner.gameObject.SetActive(false);
        }

        // ───────────── 좌표 변환 ─────────────

        public Vector2 ScreenToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, UiCam, out var local);
            return local;
        }

        public Vector2 WorldToLocal(Vector3 world)
        {
            Vector3 sp = worldCam != null ? worldCam.WorldToScreenPoint(world) : Vector3.zero;
            return ScreenToLocal(sp);
        }

        public Vector2 RectToLocal(RectTransform rt)
        {
            Vector3 center = rt.TransformPoint(rt.rect.center);
            return ScreenToLocal(RectTransformUtility.WorldToScreenPoint(UiCam, center));
        }

        // ───────────── 동전 ─────────────

        /// <summary>월드 위치에서 돈 아이콘으로 동전 여러 개를 날림. 값은 동전들에 나눠 담김.</summary>
        public void CoinBurst(Vector3 worldPos, int value)
        {
            if (coinTarget == null) { onCoinArrived?.Invoke(value, true); return; }
            int n = Mathf.Clamp(2 + value / 250, 3, 7);
            Vector2 from = WorldToLocal(worldPos);
            Vector2 to = RectToLocal(coinTarget);
            int each = value / n;
            int spawned = 0;
            for (int i = 0; i < CoinPool && spawned < n; i++)
            {
                if (coins[i].active) continue;
                bool last = spawned == n - 1;
                ref var c = ref coins[i];
                c.active = true;
                c.last = last;
                c.value = last ? value - each * (n - 1) : each;
                c.from = from + UnityEngine.Random.insideUnitCircle * 30f;
                Vector2 mid = (c.from + to) * 0.5f;
                Vector2 side = Vector2.Perpendicular((to - c.from).normalized);
                c.ctrl = mid + side * UnityEngine.Random.Range(-260f, 260f) + new Vector2(0, UnityEngine.Random.Range(60f, 220f));
                c.to = to;
                c.t = -spawned * 0.06f;
                c.duration = UnityEngine.Random.Range(0.55f, 0.72f);
                c.rt.anchoredPosition = c.from;
                c.rt.localScale = Vector3.zero;
                c.rt.gameObject.SetActive(true);
                spawned++;
            }
            // 동전이 모자라면 남은 값은 바로 반영
            if (spawned == 0) onCoinArrived?.Invoke(value, true);
            else if (spawned < n)
            {
                // 풀이 모자라 마지막 동전이 안 나갔음 → 남은 값은 바로 반영하고 연출은 그대로
                int rest = value - each * spawned;
                onCoinArrived?.Invoke(rest, true);
            }
        }

        // ───────────── 떠오르는 글자 ─────────────

        public void PopText(Vector2 local, string text, Color color, int size = 54)
        {
            int idx = 0;
            float oldest = -1f;
            for (int i = 0; i < PopPool; i++)
            {
                if (!pops[i].active) { idx = i; oldest = float.MaxValue; break; }
                if (pops[i].t > oldest) { oldest = pops[i].t; idx = i; }
            }
            ref var p = ref pops[idx];
            p.active = true;
            p.t = 0f;
            p.from = local;
            p.text.text = text;
            p.text.color = color;
            p.text.fontSize = size;
            p.rt.anchoredPosition = local;
            p.rt.localScale = Vector3.zero;
            p.text.gameObject.SetActive(true);
        }

        public void PopTextAtWorld(Vector3 world, string text, Color color, int size = 54)
            => PopText(WorldToLocal(world), text, color, size);

        // ───────────── 배너 ─────────────

        public void Banner(string title, string sub, Color color)
        {
            bannerTitle.text = title;
            bannerSub.text = sub;
            bannerBg.color = color;
            bannerT = 0f;
            bannerLanded = false;
            banner.gameObject.SetActive(true);
            banner.anchoredPosition = new Vector2(1300, banner.anchoredPosition.y);
        }

        // ───────────── 매 프레임 ─────────────

        void Update() => Tick(Time.unscaledDeltaTime);

        public void Tick(float dt)
        {
            for (int i = 0; i < CoinPool; i++)
            {
                ref var c = ref coins[i];
                if (!c.active) continue;
                c.t += dt;
                if (c.t < 0f) continue;
                float k = Mathf.Clamp01(c.t / c.duration);
                float e = k * k * (1.6f - 0.6f * k); // 처음엔 천천히 뿅, 끝엔 빨려 들어감
                Vector2 a = Vector2.LerpUnclamped(c.from, c.ctrl, e);
                Vector2 b = Vector2.LerpUnclamped(c.ctrl, c.to, e);
                c.rt.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
                float s = k < 0.18f ? Mathf.Lerp(0.2f, 1.15f, k / 0.18f) : Mathf.Lerp(1.15f, 0.65f, (k - 0.18f) / 0.82f);
                c.rt.localScale = new Vector3(s, s, 1f);
                c.rt.localRotation = Quaternion.Euler(0, 0, c.t * 540f);
                if (k >= 1f)
                {
                    c.active = false;
                    c.rt.gameObject.SetActive(false);
                    onCoinArrived?.Invoke(c.value, c.last);
                }
            }

            for (int i = 0; i < PopPool; i++)
            {
                ref var p = ref pops[i];
                if (!p.active) continue;
                p.t += dt;
                float k = p.t / PopDuration;
                if (k >= 1f)
                {
                    p.active = false;
                    p.text.gameObject.SetActive(false);
                    continue;
                }
                float rise = 1f - (1f - k) * (1f - k);
                p.rt.anchoredPosition = p.from + new Vector2(0, rise * 120f);
                float s = k < 0.15f ? Mathf.Lerp(0.3f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Min(1f, (k - 0.15f) / 0.2f));
                p.rt.localScale = new Vector3(s, s, 1f);
                var col = p.text.color;
                col.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                p.text.color = col;
            }

            if (bannerT >= 0f)
            {
                bannerT += dt;
                float y = banner.anchoredPosition.y;
                float x;
                if (bannerT < BannerIn)
                {
                    float k = bannerT / BannerIn;
                    x = Mathf.LerpUnclamped(1300f, 0f, BackOut(k));
                }
                else if (bannerT < BannerIn + BannerHold)
                {
                    if (!bannerLanded) { bannerLanded = true; bannerJuice.Punch(0.12f); }
                    x = 0f;
                }
                else
                {
                    float k = (bannerT - BannerIn - BannerHold) / BannerOut;
                    x = Mathf.Lerp(0f, -1300f, k * k);
                    if (k >= 1f)
                    {
                        bannerT = -1f;
                        banner.gameObject.SetActive(false);
                    }
                }
                banner.anchoredPosition = new Vector2(x, y);
            }
        }

        /// <summary>스크린샷용: 배너를 가운데에 멈춘 상태로.</summary>
        public void FreezeBannerForCapture()
        {
            if (bannerT < 0f) return;
            bannerT = BannerIn + 0.01f;
            banner.anchoredPosition = new Vector2(0, banner.anchoredPosition.y);
        }

        static float BackOut(float t)
        {
            const float c1 = 1.5f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
