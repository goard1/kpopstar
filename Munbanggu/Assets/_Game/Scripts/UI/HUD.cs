using System.Collections.Generic;
using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 화면 UI 전체 (세로 1080 기준).
    /// 위: 날짜·돈·유행·시간 / 아래: 행동 버튼 + 창고 / 모달: 도매상, 정산 / 매대 라벨·손님 말풍선.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        class InvItem
        {
            public Image bg;
            public Text count;
        }

        class WholesaleRow
        {
            public Button buy;
            public Text info;
        }

        GameManager gm;
        Camera cam;
        RectTransform worldLayer;

        Text dayText, phaseText, moneyText, trendText;
        RectTransform timeFill;
        Text invHint;
        readonly Dictionary<ProductData, InvItem> inv = new Dictionary<ProductData, InvItem>();

        GameObject actionRow;
        GameObject wholesaleModal, settlementModal;
        Text wholesaleMoney, settlementTitle, settlementBody;
        readonly Dictionary<ProductData, WholesaleRow> wholesale = new Dictionary<ProductData, WholesaleRow>();
        readonly Dictionary<ShelfSlot, Text> slotLabels = new Dictionary<ShelfSlot, Text>();

        Text toastText;
        CanvasGroup toastGroup;
        float toastTimer;

        public static HUD Build(GameManager gm)
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(gm.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 세로 화면: 가로폭 기준
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var hud = go.AddComponent<HUD>();
            hud.Setup(gm);
            return hud;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void Setup(GameManager gameManager)
        {
            gm = gameManager;
            cam = gm.Layout.Camera;

            worldLayer = UIKit.Rect("World Layer", transform);
            UIKit.Stretch(worldLayer);

            var safe = UIKit.Rect("Safe Area", transform);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildTopBar(safe);
            BuildBottom(safe);
            BuildSlotLabels();
            BuildWholesaleModal();
            BuildSettlementModal();
            BuildToast();

            gm.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (gm != null) gm.Changed -= Refresh;
        }

        // ───────────── 상단 ─────────────

        void BuildTopBar(Transform parent)
        {
            var bar = UIKit.Panel("Top Bar", parent, UIKit.Paper);
            UIKit.Top(bar.rectTransform, 190);

            dayText = UIKit.Label("Day", bar.transform, "", 50, UIKit.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(dayText.rectTransform, 36, 0, 540, 26);
            phaseText = UIKit.Label("Phase", bar.transform, "", 32, UIKit.Muted, TextAnchor.UpperLeft);
            UIKit.Stretch(phaseText.rectTransform, 36, 0, 400, 100);

            moneyText = UIKit.Label("Money", bar.transform, "", 56, UIKit.Ink, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Stretch(moneyText.rectTransform, 400, 0, 36, 22);
            trendText = UIKit.Label("Trend", bar.transform, "", 32, UIKit.Accent, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Stretch(trendText.rectTransform, 400, 0, 36, 100);

            var timeBg = UIKit.Panel("Time", bar.transform, new Color(0, 0, 0, 0.12f), false);
            UIKit.Bottom(timeBg.rectTransform, 14);
            var fill = UIKit.Panel("Fill", timeBg.transform, UIKit.Accent, false);
            timeFill = fill.rectTransform;
            timeFill.anchorMin = Vector2.zero;
            timeFill.anchorMax = new Vector2(0, 1);
            timeFill.offsetMin = timeFill.offsetMax = Vector2.zero;
            // 하교 러시 구간 표시
            var rush = UIKit.Panel("Rush", timeBg.transform, new Color(1f, 0.75f, 0.2f, 0.45f), false);
            rush.rectTransform.anchorMin = new Vector2(GameManager.RushStart, 0);
            rush.rectTransform.anchorMax = new Vector2(GameManager.RushEnd, 1);
            rush.rectTransform.offsetMin = rush.rectTransform.offsetMax = Vector2.zero;
        }

        // ───────────── 하단: 행동 버튼 + 창고 ─────────────

        void BuildBottom(Transform parent)
        {
            var row = UIKit.Rect("Action Row", parent);
            UIKit.Bottom(row, 120, 300, 24);
            actionRow = row.gameObject;

            var shopBtn = UIKit.Button("Wholesale", row, "도매상", 40, UIKit.Blue, Color.white, out _);
            shopBtn.onClick.AddListener(() => wholesaleModal.SetActive(true));
            var shopRt = (RectTransform)shopBtn.transform;
            shopRt.anchorMin = new Vector2(0, 0); shopRt.anchorMax = new Vector2(0.4f, 1);
            shopRt.offsetMin = Vector2.zero; shopRt.offsetMax = new Vector2(-12, 0);

            var openBtn = UIKit.Button("Open", row, "가게 문 열기 ▶", 40, UIKit.Accent, Color.white, out _);
            openBtn.onClick.AddListener(gm.OpenShop);
            var openRt = (RectTransform)openBtn.transform;
            openRt.anchorMin = new Vector2(0.4f, 0); openRt.anchorMax = new Vector2(1, 1);
            openRt.offsetMin = new Vector2(12, 0); openRt.offsetMax = Vector2.zero;

            var panel = UIKit.Panel("Backroom", parent, UIKit.Paper);
            UIKit.Bottom(panel.rectTransform, 290);

            invHint = UIKit.Label("Hint", panel.transform, "", 28, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Top(invHint.rectTransform, 56, 6, 32);

            var viewport = UIKit.Rect("Viewport", panel.transform);
            UIKit.Stretch(viewport, 16, 16, 16, 66);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, 0); // 빈 곳 드래그로도 스크롤

            var content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            foreach (var p in gm.Catalog.products)
            {
                var product = p;
                var btn = UIKit.Button(p.id, content, "", 30, UIKit.Card, UIKit.Ink, out var nameLabel);
                UIKit.Layout(btn, 200);
                btn.onClick.AddListener(() => gm.Select(product));

                var swatch = UIKit.Panel("Swatch", btn.transform, p.color, false);
                var sw = swatch.rectTransform;
                sw.anchorMin = sw.anchorMax = new Vector2(0.5f, 1);
                sw.pivot = new Vector2(0.5f, 1);
                sw.sizeDelta = new Vector2(70, 56);
                sw.anchoredPosition = new Vector2(0, -16);

                nameLabel.text = p.displayName;
                nameLabel.alignment = TextAnchor.MiddleCenter;
                UIKit.Stretch(nameLabel.rectTransform, 6, 52, 6, 76);

                var count = UIKit.Label("Count", btn.transform, "", 30, UIKit.Muted, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Stretch(count.rectTransform, 6, 12, 6, 120);

                inv[p] = new InvItem { bg = (Image)btn.targetGraphic, count = count };
            }
        }

        // ───────────── 매대 라벨 ─────────────

        void BuildSlotLabels()
        {
            foreach (var slot in gm.Layout.Slots)
            {
                var bg = UIKit.Panel($"Slot Label {slot.Index}", worldLayer, new Color(1, 1, 1, 0.82f), false);
                bg.rectTransform.sizeDelta = new Vector2(170, 46);
                var follower = bg.gameObject.AddComponent<WorldFollower>();
                follower.target = slot.transform;
                follower.worldOffset = new Vector3(0, 1.25f, 0);
                follower.cam = cam;
                var label = UIKit.Label("Text", bg.transform, "", 26, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(label.rectTransform, 4, 0, 4, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                slotLabels[slot] = label;
            }
        }

        // ───────────── 손님 말풍선 ─────────────

        public CustomerBubble CreateBubble(Transform target)
        {
            var bg = UIKit.Panel("Bubble", worldLayer, Color.white);
            var rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(300, 92);
            rt.pivot = new Vector2(0.5f, 0f);
            var follower = bg.gameObject.AddComponent<WorldFollower>();
            follower.target = target;
            follower.worldOffset = new Vector3(0, 1.45f, 0);
            follower.cam = cam;

            var label = UIKit.Label("Text", bg.transform, "", 30, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(label.rectTransform, 8, 14, 8, 4);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var fillBg = UIKit.Panel("Patience", bg.transform, new Color(0, 0, 0, 0.15f), false);
            UIKit.Bottom(fillBg.rectTransform, 10, 6, 14);
            var fill = UIKit.Panel("Fill", fillBg.transform, UIKit.Accent, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var bubble = bg.gameObject.AddComponent<CustomerBubble>();
            bubble.Setup(bg, label, fillBg.gameObject, fill.rectTransform, btn);
            return bubble;
        }

        // ───────────── 모달 ─────────────

        GameObject Modal(string name, out RectTransform card, float height)
        {
            var dim = UIKit.Panel(name, transform, UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);
            var c = UIKit.Panel("Card", dim.transform, UIKit.Card);
            card = c.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(980, height);
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        void BuildWholesaleModal()
        {
            int rows = gm.Catalog.products.Count;
            float height = 200 + rows * 132 + 170;
            wholesaleModal = Modal("Wholesale Modal", out var card, height);

            var title = UIKit.Label("Title", card, "도매상 · 아침 매입", 48, UIKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Top(title.rectTransform, 80, 30, 40);
            wholesaleMoney = UIKit.Label("Money", card, "", 36, UIKit.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Top(wholesaleMoney.rectTransform, 80, 30, 40);
            var sub = UIKit.Label("Sub", card, "상자로 사면 창고에 들어가요. 창고에서 골라 매대를 탭!", 28, UIKit.Muted);
            UIKit.Top(sub.rectTransform, 60, 112, 40);

            var list = UIKit.Rect("List", card);
            UIKit.Stretch(list, 30, 170, 30, 190);
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            foreach (var p in gm.Catalog.products)
            {
                var product = p;
                var row = UIKit.Panel(p.id, list, UIKit.Slot, false);
                UIKit.Layout(row, -1, 120);

                var sw = UIKit.Panel("Swatch", row.transform, p.color, false).rectTransform;
                sw.anchorMin = sw.anchorMax = new Vector2(0, 0.5f);
                sw.pivot = new Vector2(0, 0.5f);
                sw.sizeDelta = new Vector2(64, 64);
                sw.anchoredPosition = new Vector2(24, 0);

                var name = UIKit.Label("Name", row.transform, p.displayName, 36, UIKit.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Stretch(name.rectTransform, 110, 0, 250, 16);
                var info = UIKit.Label("Info", row.transform, "", 26, UIKit.Muted, TextAnchor.LowerLeft);
                UIKit.Stretch(info.rectTransform, 110, 14, 250, 0);

                var buy = UIKit.Button("Buy", row.transform, UIKit.Won(p.BoxCost), 32, UIKit.Green, Color.white, out _);
                var brt = (RectTransform)buy.transform;
                brt.anchorMin = new Vector2(1, 0); brt.anchorMax = new Vector2(1, 1);
                brt.pivot = new Vector2(1, 0.5f);
                brt.offsetMin = new Vector2(-226, 14); brt.offsetMax = new Vector2(-16, -14);
                buy.onClick.AddListener(() => gm.BuyBox(product));

                wholesale[p] = new WholesaleRow { buy = buy, info = info };
            }

            var close = UIKit.Button("Close", card, "닫고 진열하기", 40, UIKit.Accent, Color.white, out _);
            UIKit.Bottom((RectTransform)close.transform, 110, 36, 40);
            close.onClick.AddListener(() => wholesaleModal.SetActive(false));
        }

        void BuildSettlementModal()
        {
            settlementModal = Modal("Settlement Modal", out var card, 1000);
            settlementTitle = UIKit.Label("Title", card, "", 56, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Top(settlementTitle.rectTransform, 100, 40, 40);
            settlementBody = UIKit.Label("Body", card, "", 38, UIKit.Ink, TextAnchor.UpperLeft);
            settlementBody.lineSpacing = 1.25f;
            settlementBody.supportRichText = true;
            UIKit.Stretch(settlementBody.rectTransform, 80, 200, 80, 170);

            var next = UIKit.Button("Next", card, "다음 날 ▶", 44, UIKit.Accent, Color.white, out _);
            UIKit.Bottom((RectTransform)next.transform, 120, 44, 60);
            next.onClick.AddListener(() =>
            {
                settlementModal.SetActive(false);
                gm.NextDay();
            });
        }

        void BuildToast()
        {
            var bg = UIKit.Panel("Toast", transform, new Color(0.15f, 0.12f, 0.1f, 0.85f), false);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.70f);
            rt.sizeDelta = new Vector2(900, 100);
            toastText = UIKit.Label("Text", bg.transform, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(toastText.rectTransform, 20, 0, 20, 0);
            toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
        }

        // ───────────── 외부에서 호출 ─────────────

        public void Toast(string message, float seconds = 1.8f)
        {
            toastText.text = message;
            toastTimer = seconds;
            toastGroup.alpha = 1f;
        }

        public void ShowMorning()
        {
            settlementModal.SetActive(false);
            wholesaleModal.SetActive(true);
        }

        public void CloseModals()
        {
            wholesaleModal.SetActive(false);
            settlementModal.SetActive(false);
        }

        public void ShowSettlement()
        {
            var s = gm.Stats;
            settlementTitle.text = $"{gm.Day}일차 정산";
            string profitColor = s.Profit >= 0 ? "#3D9A59" : "#E5543D";
            string body =
                $"매출  {UIKit.Won(s.revenue)}\n" +
                $"매입  -{UIKit.Won(s.cost)}\n" +
                $"<b>순이익  <color={profitColor}>{UIKit.Won(s.Profit)}</color></b>\n\n" +
                $"찾아온 손님  {s.visitors}명 · 계산  {s.served}명\n" +
                $"물건이 없어 그냥 간 손님  {s.lostNoStock}명\n" +
                $"기다리다 간 손님  {s.lostImpatient}명\n\n" +
                $"가진 돈  <b>{UIKit.Won(gm.Money)}</b>";
            if (s.lostNoStock > s.served / 4 + 1)
                body += "\n\n<size=30><color=#857669>팁: 유행 상품과 인기 상품을 넉넉히 진열해 보세요</color></size>";
            else if (s.lostImpatient > 2)
                body += "\n\n<size=30><color=#857669>팁: 줄 선 손님 말풍선을 빨리 탭해서 계산하세요</color></size>";
            settlementBody.text = body;
            settlementModal.SetActive(true);
        }

        // ───────────── 갱신 ─────────────

        void Refresh()
        {
            dayText.text = $"{gm.Day}일차";
            moneyText.text = UIKit.Won(gm.Money);
            trendText.text = gm.Trend != null ? $"오늘의 유행: {gm.Trend.displayName}" : "";
            actionRow.SetActive(gm.Phase == DayPhase.Morning);

            invHint.text = gm.Selected != null
                ? $"<b>{gm.Selected.displayName}</b> 선택됨 → 매대를 탭해서 진열"
                : "창고 · 상품을 고른 뒤 매대를 탭하세요";
            invHint.supportRichText = true;

            foreach (var kv in inv)
            {
                int n = gm.Stock(kv.Key);
                kv.Value.count.text = $"×{n}";
                bool selected = gm.Selected == kv.Key;
                kv.Value.bg.color = selected ? new Color(1f, 0.85f, 0.45f) : n > 0 ? UIKit.Card : new Color(0.88f, 0.85f, 0.8f);
            }

            wholesaleMoney.text = UIKit.Won(gm.Money);
            foreach (var kv in wholesale)
            {
                var p = kv.Key;
                kv.Value.info.text = $"{p.boxSize}개 들이 · 개당 {UIKit.Won(p.retailPrice)}에 판매 · 창고 {gm.Stock(p)}";
                kv.Value.buy.interactable = gm.Money >= p.BoxCost;
            }

            foreach (var kv in slotLabels)
            {
                var slot = kv.Key;
                if (slot.Product == null) { kv.Value.text = "빈 매대"; kv.Value.color = UIKit.Muted; }
                else if (slot.Count == 0) { kv.Value.text = $"{slot.Product.displayName} 품절"; kv.Value.color = UIKit.Accent; }
                else { kv.Value.text = $"{slot.Product.displayName} {slot.Count}"; kv.Value.color = UIKit.Ink; }
            }
        }

        void Update()
        {
            // 손님이 물건을 집어가는 건 Changed 이벤트가 없으므로 라벨만 가볍게 매 프레임 확인
            foreach (var kv in slotLabels)
            {
                var slot = kv.Key;
                string want = slot.Product == null ? "빈 매대"
                    : slot.Count == 0 ? $"{slot.Product.displayName} 품절"
                    : $"{slot.Product.displayName} {slot.Count}";
                if (kv.Value.text != want)
                {
                    kv.Value.text = want;
                    kv.Value.color = slot.Product == null ? UIKit.Muted : slot.Count == 0 ? UIKit.Accent : UIKit.Ink;
                }
            }

            phaseText.text = gm.Phase switch
            {
                DayPhase.Morning => "아침 · 물건 사고 진열하기",
                DayPhase.Open when gm.IsRush => "하교 러시!",
                DayPhase.Open when gm.OpenProgress < GameManager.RushStart => "오전 장사",
                DayPhase.Open => "방과 후",
                DayPhase.Closing => "마감 중 · 남은 손님 계산",
                _ => "정산"
            };
            timeFill.anchorMax = new Vector2(gm.OpenProgress, 1);

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
            }
        }
    }
}
