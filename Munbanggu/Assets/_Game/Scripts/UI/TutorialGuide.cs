using Munbanggu.Core;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 1일차 단계별 안내: 도매상에서 사기 → 창고에서 고르기 → 매대 탭 → 문 열기 → 말풍선 탭해서 계산.
    /// 진행은 GameEvents + GameManager 상태로 감지. 한 번 끝나면 PlayerPrefs에 기록해서 다시 안 뜬다.
    /// 화살표·하이라이트·안내 쪽지는 모두 raycastTarget=false (탭을 막지 않음).
    /// </summary>
    public class TutorialGuide : MonoBehaviour
    {
        const string PrefKey = "munbanggu.tutorial.day1.done";
        const int StepCount = 5;
        const float SelectShowTime = 2.2f;
        const float HintMaxWidth = 760f;

        public static bool Done => PlayerPrefs.GetInt(PrefKey, 0) == 1;

        /// <summary>튜토리얼을 다시 보고 싶을 때 (디버그 메뉴 등에서 호출).</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        HUD hud;
        GameManager gm;
        RectTransform layer;
        RectTransform hintRt, arrowRt, highlightRt;
        Text hintText, stepText;
        GameObject stepChip;
        UIJuice hintJuice;

        bool bought, placed, checkedOut, finished, selectSeen;
        float selectTimer, finishTimer, time;
        int lastStep = -1;
        string lastText;
        RectTransform lastScrollTarget;
        bool ignorePrefs;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>아직 튜토리얼을 안 끝냈으면 만든다 (날짜는 첫 Tick에서 확인 — HUD는 세이브 로드 전에 만들어짐).</summary>
        public static TutorialGuide Create(HUD hud, GameManager gm, RectTransform parent, bool force = false)
        {
            if (!force && Done) return null;
            var rt = UIKit.Rect("Tutorial", parent);
            UIKit.Stretch(rt);
            var t = rt.gameObject.AddComponent<TutorialGuide>();
            t.hud = hud;
            t.gm = gm;
            t.layer = rt;
            t.ignorePrefs = force;
            t.Build();
            GameEvents.BoxBought += t.OnBoxBought;
            GameEvents.ItemsPlaced += t.OnItemsPlaced;
            GameEvents.CheckedOut += t.OnCheckedOut;
            GameEvents.PhaseChanged += t.OnPhaseChanged;
            return t;
        }

        void OnDestroy()
        {
            GameEvents.BoxBought -= OnBoxBought;
            GameEvents.ItemsPlaced -= OnItemsPlaced;
            GameEvents.CheckedOut -= OnCheckedOut;
            GameEvents.PhaseChanged -= OnPhaseChanged;
        }

        void Build()
        {
            // 목표 주변 반짝이는 테두리
            var hl = UIKit.Ring(layer, UIKit.Yellow, 30);
            highlightRt = hl.rectTransform;
            highlightRt.anchorMin = highlightRt.anchorMax = new Vector2(0.5f, 0.5f);
            var hl2 = UIKit.Ring(highlightRt, UIKit.Red, 30, -8);
            hl2.color = UIKit.WithAlpha(UIKit.Red, 0.9f);

            // 빨간 화살표 (흰 테두리)
            var arrow = UIKit.Rect("Arrow", layer).gameObject.AddComponent<Image>();
            arrow.sprite = UIKit.ArrowSprite;
            arrow.color = UIKit.Red;
            arrow.raycastTarget = false;
            UIKit.AddOutline(arrow, Color.white, 4f);
            UIKit.AddShadow(arrow, UIKit.ShadowColor, 6f);
            arrowRt = arrow.rectTransform;
            UIKit.Place(arrowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(76, 96));

            // 노란 쪽지 (테이프로 붙인 느낌)
            var note = UIKit.CardPanel("Hint", layer, new Color(1f, 0.94f, 0.58f), 18, false, 8);
            hintRt = note.rectTransform;
            UIKit.Place(hintRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(600, 110));
            UIKit.TapeStrip(hintRt, new Vector2(0.5f, 1), new Vector2(0, -2), -4, 120);
            hintText = UIKit.Label("Text", hintRt, "", 34, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Stretch(hintText.rectTransform, 30, 14, 26, 26);
            hintText.lineSpacing = 1.05f;
            hintJuice = note.gameObject.AddComponent<UIJuice>();

            var chip = UIKit.CardPanel("Step", hintRt, UIKit.Red, 22, false, 3);
            UIKit.Place(chip.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(10, 4), new Vector2(78, 44));
            chip.rectTransform.localRotation = Quaternion.Euler(0, 0, -10f);
            UIKit.Ring(chip.transform, Color.white, 22, -3);
            stepText = UIKit.Label("Text", chip.transform, "", 25, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(stepText.rectTransform, 0, 2, 0, 0);
            stepText.horizontalOverflow = HorizontalWrapMode.Overflow;
            stepChip = chip.gameObject;

            SetVisible(false, false);
        }

        // ───────────── 이벤트 ─────────────

        void OnBoxBought(ProductData p) => bought = true;
        void OnItemsPlaced(ShelfSlot s, ProductData p, int n) => placed = true;

        void OnCheckedOut(Customer c, int price)
        {
            if (checkedOut || finished) return;
            checkedOut = true;
            Finish("참 잘했어요! ★\n이제 혼자서도 척척 할 수 있어요", 3f);
        }

        void OnPhaseChanged(DayPhase phase)
        {
            // 계산을 한 번도 못 하고 하루가 끝나도 다시 띄우지는 않음
            if (phase == DayPhase.Settlement && !finished) Finish(null, 0f);
        }

        void Finish(string message, float seconds)
        {
            finished = true;
            if (!ignorePrefs)
            {
                PlayerPrefs.SetInt(PrefKey, 1);
                PlayerPrefs.Save();
            }
            finishTimer = seconds;
            if (string.IsNullOrEmpty(message) || seconds <= 0f)
            {
                SetVisible(false, false);
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            SetStep(-1, message);
            SetVisible(true, false);
            PlaceHintFree();
        }

        // ───────────── 매 프레임 ─────────────

        void Update() => Tick(Time.unscaledDeltaTime);

        public void Tick(float dt)
        {
            time += dt;
            if (finished)
            {
                finishTimer -= dt;
                if (finishTimer <= 0f && Application.isPlaying) Destroy(gameObject);
                return;
            }
            if (gm == null || hud == null) return;
            if (gm.Day > 1 && !ignorePrefs) { Finish(null, 0f); return; }
            if (gm.Day < 1) { SetVisible(false, false); return; } // 아직 세이브 로드 전

            int step = -1;
            string text = null;
            RectTransform target = null;
            bool hasWorld = false;
            Vector3 world = default;

            if (gm.Phase == DayPhase.Morning)
            {
                if (hud.SettlementOpen) { SetVisible(false, false); return; }
                if (!bought && gm.TotalShelfStock() == 0 && !AnyStock())
                {
                    step = 1;
                    if (hud.WholesaleOpen)
                    {
                        target = hud.FirstAffordableBuyButton();
                        text = "도매상에서 물건을 상자로 사요\n<size=28>초록 가격 버튼을 탭!</size>";
                    }
                    else
                    {
                        target = hud.WholesaleButtonRect;
                        text = "도매상을 열어 물건을 사요";
                    }
                }
                else if (hud.WholesaleOpen)
                {
                    step = 1;
                    target = hud.CloseWholesaleRect;
                    text = "잘했어요! 다 샀으면\n닫고 진열하러 가요";
                }
                else if (!placed && gm.TotalShelfStock() == 0)
                {
                    var sel = gm.Selected;
                    if (sel == null || gm.Stock(sel) <= 0)
                    {
                        step = 2;
                        target = hud.FirstStockedCard();
                        text = "창고에서 진열할 상품을 골라요";
                        if (target == null) { target = hud.WholesaleButtonRect; text = "창고가 비었어요. 도매상에서 사 와요"; step = 1; }
                    }
                    else if (!selectSeen)
                    {
                        step = 2;
                        target = hud.InvCardFor(sel);
                        text = "창고에서 고른 상품은\n노랗게 빛나요";
                        selectTimer += dt;
                        if (selectTimer >= SelectShowTime) selectSeen = true;
                    }
                    else
                    {
                        step = 3;
                        var slot = PickSlot(sel);
                        if (slot != null) { hasWorld = true; world = slot.transform.position + Vector3.up * 0.7f; }
                        text = "매대를 탭해서 진열해요";
                    }
                }
                else
                {
                    step = 4;
                    target = hud.OpenButtonRect;
                    text = "준비 끝! 가게 문을 열어요";
                }
            }
            else if (gm.Phase == DayPhase.Open || gm.Phase == DayPhase.Closing)
            {
                step = 5;
                var bubble = hud.FirstTappableBubble();
                if (bubble != null)
                {
                    target = (RectTransform)bubble.transform;
                    text = "말풍선을 탭해서 계산해요!";
                }
                else text = "손님이 물건을 고르면\n계산대에 줄을 서요";
            }
            else
            {
                SetVisible(false, false);
                return;
            }

            SetStep(step, text);
            if (target != null && target.gameObject.activeInHierarchy)
            {
                if (step == 2 && target != lastScrollTarget) { lastScrollTarget = target; hud.ScrollInventoryTo(target); }
                GetLocalRect(target, out var min, out var max);
                Point(min, max);
            }
            else if (hasWorld)
            {
                Vector2 c = WorldToLocal(world);
                Point(c - new Vector2(110, 80), c + new Vector2(110, 80));
            }
            else
            {
                SetVisible(true, false);
                PlaceHintFree();
            }
        }

        bool AnyStock()
        {
            foreach (var p in gm.Catalog.products)
                if (gm.Stock(p) > 0) return true;
            return false;
        }

        ShelfSlot PickSlot(ProductData sel)
        {
            ShelfSlot empty = null;
            foreach (var s in gm.Layout.Slots)
            {
                if (s.Product == sel && s.Count < s.Capacity) return s;
                if (empty == null && s.Product == null) empty = s;
            }
            return empty != null ? empty : gm.Layout.Slots.Count > 0 ? gm.Layout.Slots[0] : null;
        }

        // ───────────── 배치 ─────────────

        void SetStep(int step, string text)
        {
            bool changed = step != lastStep || text != lastText;
            if (!changed) return;
            bool stepChanged = step != lastStep;
            lastStep = step;
            lastText = text;
            hintText.text = text;
            stepChip.SetActive(step > 0);
            if (step > 0) stepText.text = $"{step}/{StepCount}";

            // 글 길이에 맞춰 쪽지 크기 조정 (바뀔 때만)
            float w = Mathf.Min(HintMaxWidth, hintText.preferredWidth + 60f);
            w = Mathf.Max(w, 360f);
            hintRt.sizeDelta = new Vector2(w, 100f);
            float h = hintText.preferredHeight + 48f;
            hintRt.sizeDelta = new Vector2(w, Mathf.Max(96f, h));
            if (stepChanged) hintJuice.Punch(0.12f);
        }

        void SetVisible(bool hint, bool pointer)
        {
            if (hintRt.gameObject.activeSelf != hint) hintRt.gameObject.SetActive(hint);
            if (arrowRt.gameObject.activeSelf != pointer) arrowRt.gameObject.SetActive(pointer);
            if (highlightRt.gameObject.activeSelf != pointer) highlightRt.gameObject.SetActive(pointer);
        }

        /// <summary>목표 사각형(레이어 로컬 좌표)을 가리킴: 하이라이트 + 화살표 + 쪽지.</summary>
        void Point(Vector2 min, Vector2 max)
        {
            SetVisible(true, true);
            var bounds = layer.rect;
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;

            float pulse = 1f + Mathf.Sin(time * 6f) * 0.03f;
            highlightRt.anchoredPosition = center;
            highlightRt.sizeDelta = size + new Vector2(28, 28);
            highlightRt.localScale = new Vector3(pulse, pulse, 1f);

            float bob = Mathf.Abs(Mathf.Sin(time * 5f)) * 18f;
            float hintH = hintRt.sizeDelta.y, hintW = hintRt.sizeDelta.x;
            float arrowH = arrowRt.sizeDelta.y;
            // 위쪽에 자리가 있으면 위에서 아래로 가리키고, 없으면 아래에서 위로
            bool above = max.y + 24 + arrowH + 16 + hintH < bounds.yMax - 210f;
            float x = Mathf.Clamp(center.x, bounds.xMin + hintW * 0.5f + 16f, bounds.xMax - hintW * 0.5f - 16f);
            float ax = Mathf.Clamp(center.x, bounds.xMin + 50f, bounds.xMax - 50f);
            if (above)
            {
                arrowRt.localRotation = Quaternion.identity;
                arrowRt.anchoredPosition = new Vector2(ax, max.y + 18 + bob);
                hintRt.pivot = new Vector2(0.5f, 0f);
                hintRt.anchoredPosition = new Vector2(x, max.y + 18 + arrowH + 22);
            }
            else
            {
                arrowRt.localRotation = Quaternion.Euler(0, 0, 180f);
                arrowRt.anchoredPosition = new Vector2(ax, min.y - 18 - bob);
                hintRt.pivot = new Vector2(0.5f, 1f);
                hintRt.anchoredPosition = new Vector2(x, min.y - 18 - arrowH - 22);
            }
        }

        /// <summary>가리킬 곳이 없을 때: 상단 바 아래 가운데.</summary>
        void PlaceHintFree()
        {
            var bounds = layer.rect;
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0, bounds.yMax - 470f);
        }

        void GetLocalRect(RectTransform target, out Vector2 min, out Vector2 max)
        {
            target.GetWorldCorners(corners);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            var uiCam = UiCamera();
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, uiCam, out var local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }

        Vector2 WorldToLocal(Vector3 world)
        {
            var cam = hud.ShopCamera;
            Vector3 sp = cam != null ? cam.WorldToScreenPoint(world) : Vector3.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, UiCamera(), out var local);
            return local;
        }

        Camera UiCamera()
        {
            var c = hud.RootCanvas;
            return c == null || c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
        }
    }
}
