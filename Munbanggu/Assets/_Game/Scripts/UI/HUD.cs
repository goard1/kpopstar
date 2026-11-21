using System;
using System.Collections;
using System.Collections.Generic;
using Munbanggu.Core;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.Visual;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 에디터 스크린샷 도구(Editor/UIScreenshot.cs)가 internal 접근자를 쓰도록
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace Munbanggu.UI
{
    /// <summary>
    /// 화면 UI 전체 (세로 1080 기준, 90년대 문방구 감성).
    /// 위: 날짜·돈·유행·시간 (≤200) / 아래: 행동 버튼 + 창고 (≤430) / 모달: 도매상, 정산(장부) / 매대 가격표·손님 말풍선.
    /// 연출은 GameEvents 구독으로만 붙인다 (게임 로직은 건드리지 않음).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        /// <summary>
        /// 정산(장부) 줄 추가 훅. 기본 줄(매출·매입·순이익·손님) 다음, '가진 돈' 앞에 들어간다.
        /// 예) HUD.CollectSettlementLines += (gm, lines) => lines.Add(SettlementLine.Count("단골 방문", gm.Stats.regularsVisited));
        /// </summary>
        public static event Action<GameManager, List<SettlementLine>> CollectSettlementLines;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => CollectSettlementLines = null;

        // 레이아웃 예산 (1080 기준)
        const float TopBarHeight = 184f, TopBarY = 12f;            // 합 196 ≤ 200
        const float BackroomHeight = 284f, BackroomY = 8f;         // 창고 바
        const float ActionHeight = 116f, ActionY = 304f;           // 합 420 ≤ 430
        const float CardWidth = 188f;

        class InvItem
        {
            public ProductData product;
            public int catalogIndex;
            public Button button;
            public Image bg;
            public Text name, count;
            public Image ring;
            public GameObject trendTag, newTag;
            public CanvasGroup group;
            public UIJuice juice;
        }

        class WholesaleRow
        {
            public Button buy;
            public Text buyLabel;
            public Text info;
            public GameObject trendTag, newTag;
            public UIJuice juice;
        }

        class SlotLabel
        {
            public Image bg;
            public Text text;
            public Image icon;
            public UIJuice juice;
            public ProductData product;
            public int count = -1;
            public bool shown, morning;
            public string combo;
        }

        class SettlementRow
        {
            public SettlementLine line;
            public Text value;
            public CanvasGroup group;
            public UIJuice juice;
        }

        GameManager gm;
        Camera cam;
        Canvas canvas;
        RectTransform worldLayer, overlaySafe, fxLayer;
        HudFx fx;
        TutorialGuide tutorial;

        // 상단
        Text dayText, phaseText, moneyText, trendText;
        RectTransform trendTag, timeFill, timeKnob;
        Image trendIcon;
        UIJuice moneyJuice, coinJuice, dayJuice, phaseJuice, timeJuice;
        float shownMoney;
        int shownMoneyInt = int.MinValue;
        int pendingMoney;
        float lastProgress = -1f;
        string lastTrendText;

        // 하단
        Text invHint;
        UIJuice backroomTabJuice;
        RectTransform invContent;
        ScrollRect invScroll;
        readonly List<InvItem> inv = new List<InvItem>();
        readonly Dictionary<ProductData, InvItem> invByProduct = new Dictionary<ProductData, InvItem>();
        readonly List<InvItem> invOrder = new List<InvItem>();
        GameObject actionRow;
        Button wholesaleButton, openButton, speedButton, upgradeButton, staffButton, townButton;
        TownPanel townPanel;
        public TownPanel Town => townPanel;
        StaffPanel staffPanel;
        UpgradePanel upgradePanel;
        Text speedLabel;
        int shownSpeed = -1;

        // 모달
        SettingsPanel settings;
        Button gearButton;
        GameObject wholesaleModal, settlementModal;
        UIJuice wholesaleCardJuice, settlementCardJuice;
        Text wholesaleMoney, wholesaleFooter;
        ScrollRect wholesaleScroll;
        RectTransform wholesaleCard;
        Button closeWholesaleButton;
        readonly Dictionary<ProductData, WholesaleRow> wholesale = new Dictionary<ProductData, WholesaleRow>();
        Text settlementTitle, settlementSub;
        RectTransform settlementCard, settlementRows;
        GameObject stamp;
        Text stampText;
        Image stampRing;
        UIJuice stampJuice;
        readonly List<SettlementRow> settlementRowList = new List<SettlementRow>();
        readonly List<SettlementLine> lineBuffer = new List<SettlementLine>();
        Coroutine settlementAnim;
        bool settlementSkip;

        // 매대·말풍선
        readonly Dictionary<ShelfSlot, SlotLabel> slotLabels = new Dictionary<ShelfSlot, SlotLabel>();
        readonly List<CustomerBubble> bubbles = new List<CustomerBubble>();
        static readonly Predicate<CustomerBubble> IsDead = b => b == null;

        // 토스트
        Image toastBg;
        Text toastText;
        CanvasGroup toastGroup;
        UIJuice toastJuice;
        float toastTimer, toastRedTimer;
        static readonly Color ToastColor = new Color(0.20f, 0.15f, 0.14f, 0.94f);

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
            hud.canvas = canvas;
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
            { var m = Modal("Upgrade Modal", out var upCard, 1640, out var upJuice); upgradePanel = UpgradePanel.Build(gm, m, upCard, upJuice, EnterDecorMode, EnterMoveMode); }
            { var m = Modal("Decor Modal", out var dcCard, 1500, out var dcJuice); decorPanel = DecorPanel.Build(gm, m, dcCard, dcJuice); }
            { var m = Modal("Staff Modal", out var sfCard, 1640, out var sfJuice); staffPanel = StaffPanel.Build(gm, m, sfCard, sfJuice); }
            { var m = Modal("Town Modal", out var twCard, 1640, out var twJuice); townPanel = TownPanel.Build(gm, m, twCard, twJuice); }
            BuildSettlementModal();

            // 모달 위에 뜨는 것들: 토스트·배너·튜토리얼·동전
            overlaySafe = UIKit.Rect("Overlay Safe Area", transform);
            UIKit.Stretch(overlaySafe);
            overlaySafe.gameObject.AddComponent<SafeArea>();
            BuildToast(overlaySafe);

            // 설정: 톱니 버튼은 모달 위(overlay)에, 설정 창은 그 위에 (아침 도매상 창·정산 중에도 열 수 있게)
            BuildSettings();
            BuildChoice();
            { var m = Modal("Picker Modal", out var pkCard, 1640, out var pkJuice); picker = PickerPanel.Build(gm, m, pkCard, pkJuice); }

            fxLayer = UIKit.Rect("FX Layer", transform);
            UIKit.Stretch(fxLayer);
            fx = gameObject.AddComponent<HudFx>();
            fx.Init(fxLayer, overlaySafe, canvas, cam);
            fx.coinTarget = coinJuice != null ? (RectTransform)coinJuice.transform : null;
            fx.onCoinArrived = OnCoinArrived;

            tutorial = TutorialGuide.Create(this, gm, overlaySafe);
            // 튜토리얼은 토스트보다 아래, 동전보다 아래에 그려지도록 FX를 맨 위로
            fxLayer.SetAsLastSibling();

            shownMoney = gm.Money;
            gm.Changed += Refresh;
            GameEvents.CheckedOut += OnCheckedOut;
            GameEvents.ItemsPlaced += OnItemsPlaced;
            GameEvents.BoxBought += OnBoxBought;
            GameEvents.InvalidAction += OnInvalidAction;
            GameEvents.RushStarted += OnRushStarted;
            GameEvents.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (gm != null) gm.Changed -= Refresh;
            GameEvents.CheckedOut -= OnCheckedOut;
            GameEvents.ItemsPlaced -= OnItemsPlaced;
            GameEvents.BoxBought -= OnBoxBought;
            GameEvents.InvalidAction -= OnInvalidAction;
            GameEvents.RushStarted -= OnRushStarted;
            GameEvents.PhaseChanged -= OnPhaseChanged;
        }

        // ───────────── 상단 ─────────────

        void BuildTopBar(Transform parent)
        {
            var bar = UIKit.CardPanel("Top Bar", parent, UIKit.Kraft, 30, true, 8);
            UIKit.Top(bar.rectTransform, TopBarHeight, TopBarY, 14);
            var stitch = UIKit.DashLine(bar.transform, new Color(1f, 1f, 1f, 0.45f), 4);
            UIKit.Top(stitch.rectTransform, 4, 9, 30);

            // 날짜: 빨간 스티커
            var day = UIKit.CardPanel("Day", bar.transform, UIKit.Red, 26, false, 6);
            UIKit.Place(day.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(22 + 100, -60), new Vector2(200, 80));
            day.rectTransform.localRotation = Quaternion.Euler(0, 0, 2.5f);
            UIKit.Ring(day.transform, new Color(1, 1, 1, 0.9f), 26, -5);
            dayText = UIKit.Label("Text", day.transform, "", 46, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(dayText.rectTransform, 6, 4, 6, 0);
            dayJuice = day.gameObject.AddComponent<UIJuice>();

            phaseText = UIKit.Label("Phase", bar.transform, "", 30, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place(phaseText.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(240, -60), new Vector2(300, 80));
            phaseText.lineSpacing = 0.95f;
            phaseJuice = phaseText.gameObject.AddComponent<UIJuice>();

            // 돈: 크림색 지갑 띠 + 동전
            var pill = UIKit.CardPanel("Money", bar.transform, UIKit.Cream, 40, false, 6);
            UIKit.Place(pill.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-22, -60), new Vector2(360, 80));
            UIKit.Ring(pill.transform, UIKit.WithAlpha(UIKit.KraftDark, 0.6f), 40);
            moneyJuice = pill.gameObject.AddComponent<UIJuice>();
            var coin = UIKit.Coin(pill.transform, 58);
            UIKit.Place(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44, 0), new Vector2(58, 58));
            coinJuice = coin.gameObject.AddComponent<UIJuice>();
            moneyText = UIKit.Label("Text", pill.transform, "", 50, UIKit.Ink, TextAnchor.MiddleRight);
            UIKit.Stretch(moneyText.rectTransform, 80, 2, 26, 0);
            moneyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 유행: 파란 이름표 (글 길이에 맞춰 늘어남)
            var tag = UIKit.CardPanel("Trend", bar.transform, UIKit.Blue, 20, false, 5);
            trendTag = tag.rectTransform;
            UIKit.Place(trendTag, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -108), new Vector2(400, 40));
            trendText = UIKit.Label("Text", tag.transform, "", 27, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(trendText.rectTransform, 14, 2, 14, 0);
            trendText.horizontalOverflow = HorizontalWrapMode.Overflow;
            trendIcon = Icon(tag.transform, null, new Vector2(0, 0.5f), new Vector2(18, 2), 54);
            trendIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 빨리감기: 1 → 2 → 3배속 (영업 중에만 보임)
            speedButton = UIKit.Button("Speed", bar.transform, "", 30, UIKit.Cream, UIKit.Ink, out speedLabel);
            UIKit.Place((RectTransform)speedButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -102), new Vector2(150, 44));
            UIKit.Round((Image)speedButton.targetGraphic, 22f);
            UIKit.Ring(speedButton.transform, UIKit.WithAlpha(UIKit.KraftDark, 0.6f), 22);
            speedButton.onClick.AddListener(gm.CycleSpeed);
            speedButton.gameObject.SetActive(false);

            // 시간 띠 (러시 구간은 노란 사선)
            var timeBg = UIKit.Panel("Time", bar.transform, new Color(0.36f, 0.24f, 0.13f, 0.38f), false);
            UIKit.Round(timeBg, 11);
            UIKit.Bottom(timeBg.rectTransform, 20, 13, 26);
            timeJuice = timeBg.gameObject.AddComponent<UIJuice>();
            var rush = UIKit.Rect("Rush", timeBg.transform);
            rush.anchorMin = new Vector2(GameManager.RushStart, 0);
            rush.anchorMax = new Vector2(GameManager.RushEnd, 1);
            rush.offsetMin = rush.offsetMax = Vector2.zero;
            UIKit.Stripes(rush, new Color(1f, 0.82f, 0.2f, 0.9f), 20);
            var fill = UIKit.Panel("Fill", timeBg.transform, UIKit.Red, false);
            UIKit.Round(fill, 10);
            timeFill = fill.rectTransform;
            timeFill.anchorMin = Vector2.zero;
            timeFill.anchorMax = new Vector2(0, 1);
            timeFill.offsetMin = timeFill.offsetMax = Vector2.zero;
            var knob = UIKit.Panel("Knob", timeBg.transform, UIKit.Cream, false);
            UIKit.Round(knob, 17);
            UIKit.AddShadow(knob, UIKit.ShadowColor, 3);
            UIKit.Ring(knob.transform, UIKit.Red, 17);
            timeKnob = knob.rectTransform;
            UIKit.Place(timeKnob, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
        }

        // ───────────── 하단: 행동 버튼 + 창고 ─────────────

        void BuildBottom(Transform parent)
        {
            var row = UIKit.Rect("Action Row", parent);
            UIKit.Bottom(row, ActionHeight, ActionY, 22);
            actionRow = row.gameObject;

            wholesaleButton = UIKit.Button("Wholesale", row, "도매상", 36, UIKit.Blue, Color.white, out _);
            wholesaleButton.onClick.AddListener(() => OpenModal(wholesaleModal, wholesaleCardJuice));
            var shopRt = (RectTransform)wholesaleButton.transform;
            shopRt.anchorMin = new Vector2(0, 0); shopRt.anchorMax = new Vector2(0.2f, 1);
            shopRt.offsetMin = new Vector2(0, 8); shopRt.offsetMax = new Vector2(-10, 0);

            // 가게 꾸미기 (업그레이드 상점)
            upgradeButton = UIKit.Button("Upgrade", row, "꾸미기", 36, UIKit.Yellow, UIKit.Ink, out _);
            upgradeButton.onClick.AddListener(() => upgradePanel.Open());
            var upRt = (RectTransform)upgradeButton.transform;
            upRt.anchorMin = new Vector2(0.2f, 0); upRt.anchorMax = new Vector2(0.38f, 1);
            upRt.offsetMin = new Vector2(8, 8); upRt.offsetMax = new Vector2(-8, 0);

            // 직원 관리
            staffButton = UIKit.Button("Staff", row, "직원", 36, UIKit.Green, Color.white, out _);
            staffButton.onClick.AddListener(() => staffPanel.Open());
            var stRt = (RectTransform)staffButton.transform;
            stRt.anchorMin = new Vector2(0.38f, 0); stRt.anchorMax = new Vector2(0.53f, 1);
            stRt.offsetMin = new Vector2(8, 8); stRt.offsetMax = new Vector2(-8, 0);

            // 동네 (달력·대회·라이벌·만화방·도감)
            townButton = UIKit.Button("Town", row, "동네", 36, new Color(0.56f, 0.40f, 0.80f), Color.white, out _);
            townButton.onClick.AddListener(() => townPanel.Open());
            var twRt = (RectTransform)townButton.transform;
            twRt.anchorMin = new Vector2(0.53f, 0); twRt.anchorMax = new Vector2(0.68f, 1);
            twRt.offsetMin = new Vector2(8, 8); twRt.offsetMax = new Vector2(-8, 0);

            openButton = UIKit.Button("Open", row, "문 열기!", 42, UIKit.Red, Color.white, out _);
            openButton.onClick.AddListener(gm.OpenShop);
            var openRt = (RectTransform)openButton.transform;
            openRt.anchorMin = new Vector2(0.68f, 0); openRt.anchorMax = new Vector2(1, 1);
            openRt.offsetMin = new Vector2(10, 8); openRt.offsetMax = Vector2.zero;
            UIKit.TapeStrip(openButton.transform, new Vector2(1, 1), new Vector2(-46, -6), -18, 96);

            // 창고: 공책 한 장
            var panel = UIKit.CardPanel("Backroom", parent, UIKit.Paper, 30, true, 8);
            UIKit.Bottom(panel.rectTransform, BackroomHeight, BackroomY, 12);
            var lines = UIKit.NoteLines(panel.transform, 50);
            UIKit.Stretch(lines.rectTransform, 14, 14, 14, 14);
            var margin = UIKit.Square(UIKit.Panel("Margin", panel.transform, UIKit.Margin, false));
            margin.rectTransform.anchorMin = new Vector2(0, 0);
            margin.rectTransform.anchorMax = new Vector2(0, 1);
            margin.rectTransform.offsetMin = new Vector2(140, 10);
            margin.rectTransform.offsetMax = new Vector2(143, -10);

            var tab = UIKit.CardPanel("Tab", panel.transform, UIKit.Red, 16, false, 4);
            UIKit.Place(tab.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(72, -34), new Vector2(110, 46));
            tab.rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);
            var tabText = UIKit.Label("Text", tab.transform, "창고", 30, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tabText.rectTransform, 0, 2, 0, 0);
            backroomTabJuice = tab.gameObject.AddComponent<UIJuice>();

            invHint = UIKit.Label("Hint", panel.transform, "", 28, UIKit.Muted, TextAnchor.MiddleLeft);
            var hrt = invHint.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.offsetMin = new Vector2(156, -58); hrt.offsetMax = new Vector2(-20, -10);

            var viewport = UIKit.Rect("Viewport", panel.transform);
            UIKit.Stretch(viewport, 10, 8, 10, 62);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, 0); // 빈 곳 드래그로도 스크롤

            invContent = UIKit.Rect("Content", viewport);
            invContent.anchorMin = new Vector2(0, 0);
            invContent.anchorMax = new Vector2(0, 1);
            invContent.pivot = new Vector2(0, 0.5f);
            invContent.offsetMin = invContent.offsetMax = Vector2.zero;
            var hlg = invContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.padding = new RectOffset(8, 8, 10, 14);
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var fitter = invContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            invScroll = panel.gameObject.AddComponent<ScrollRect>();
            invScroll.viewport = viewport;
            invScroll.content = invContent;
            invScroll.horizontal = true;
            invScroll.vertical = false;
            invScroll.movementType = ScrollRect.MovementType.Elastic;
            invScroll.elasticity = 0.12f;
            invScroll.decelerationRate = 0.12f;

            int index = 0;
            foreach (var p in gm.Catalog.products)
            {
                if (p == null) continue;
                var item = BuildInvCard(p, index++);
                inv.Add(item);
                invByProduct[p] = item;
            }
        }

        InvItem BuildInvCard(ProductData p, int index)
        {
            var product = p;
            var btn = UIKit.Button(p.id, invContent, "", 30, UIKit.Card, UIKit.Ink, out var nameLabel);
            var shadow = btn.GetComponent<Shadow>();
            if (shadow != null) { shadow.effectColor = UIKit.ShadowColor; shadow.effectDistance = new Vector2(0, -7); }
            UIKit.Layout(btn, CardWidth);
            btn.onClick.AddListener(() => gm.Select(product));

            var ring = UIKit.Ring(btn.transform, UIKit.Red, 28, 3);
            ring.gameObject.SetActive(false);

            var swatch = UIKit.Panel("Swatch", btn.transform, Tint(p.color), false);
            UIKit.Round(swatch, 18);
            UIKit.Ring(swatch.transform, new Color(1, 1, 1, 0.95f), 18);
            UIKit.Place(swatch.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(84, 58));
            Icon(btn.transform, p, new Vector2(0.5f, 1), new Vector2(0, -2), 86);

            nameLabel.text = p.displayName;
            nameLabel.alignment = TextAnchor.MiddleCenter;
            nameLabel.fontSize = p.displayName.Length > 5 ? 25 : 29;
            UIKit.Stretch(nameLabel.rectTransform, 6, 50, 6, 80);

            var count = UIKit.Label("Count", btn.transform, "", 29, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Stretch(count.rectTransform, 6, 10, 6, 136);

            var trendTag = Sticker(btn.transform, "유행", UIKit.Red, new Vector2(0, 1), new Vector2(34, -14), -14);
            var newTag = Sticker(btn.transform, "NEW", UIKit.Green, new Vector2(1, 1), new Vector2(-32, -14), 12);

            return new InvItem
            {
                product = p,
                catalogIndex = index,
                button = btn,
                bg = (Image)btn.targetGraphic,
                name = nameLabel,
                count = count,
                ring = ring,
                trendTag = trendTag,
                newTag = newTag,
                group = btn.gameObject.AddComponent<CanvasGroup>(),
                juice = btn.GetComponent<UIJuice>()
            };
        }

        /// <summary>상품 아이콘 이미지 (스티커처럼 흰 테두리, 위쪽 기준 배치). 아이콘이 없으면 숨김.</summary>
        static Image Icon(Transform parent, ProductData p, Vector2 anchor, Vector2 pos, float size)
        {
            var img = UIKit.Rect("Icon", parent).gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.sprite = ProductIcons.Get(p);
            img.enabled = img.sprite != null;
            UIKit.Place(img.rectTransform, anchor, new Vector2(0.5f, anchor.y), pos, new Vector2(size, size));
            var sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = UIKit.WithAlpha(UIKit.ShadowColor, 0.5f);
            sh.effectDistance = new Vector2(0, -3);
            return img;
        }

        /// <summary>아이콘 뒤 받침: 상품 색을 연하게.</summary>
        static Color Tint(Color c) => Color.Lerp(c, Color.white, 0.62f);

        /// <summary>작은 기울어진 스티커 ("유행", "NEW" 등).</summary>
        static GameObject Sticker(Transform parent, string text, Color color, Vector2 anchor, Vector2 pos, float angle)
        {
            var s = UIKit.CardPanel("Sticker " + text, parent, color, 14, false, 3);
            UIKit.Place(s.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(70, 32));
            s.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            UIKit.Ring(s.transform, new Color(1, 1, 1, 0.9f), 14, -3);
            var t = UIKit.Label("Text", s.transform, text, 21, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform, 0, 1, 0, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.gameObject.SetActive(false);
            return s.gameObject;
        }

        // ───────────── 매대 가격표 ─────────────

        void BuildSlotLabels()
        {
            // 이사로 치운 매대의 이름표는 지운다 (Destroy는 프레임 끝이라 Slots에 없는 것 기준으로)
            var gone = new List<ShelfSlot>();
            foreach (var kv in slotLabels)
                if (kv.Key == null || !gm.Layout.Slots.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var s in gone)
            {
                var l = slotLabels[s];
                if (l.bg != null)
                {
                    if (Application.isPlaying) Destroy(l.bg.gameObject);
                    else DestroyImmediate(l.bg.gameObject);
                }
                slotLabels.Remove(s);
            }
            foreach (var slot in gm.Layout.Slots)
            {
                if (slotLabels.ContainsKey(slot)) continue; // 업그레이드로 새로 생긴 매대만 추가
                MakeSlotLabel(slot);
            }
        }

        /// <summary>업그레이드로 매대가 늘었을 때 이름표 추가.</summary>
        public void RebuildSlotLabels() => BuildSlotLabels();

        void MakeSlotLabel(ShelfSlot slot)
            {
                var bg = UIKit.Panel($"Slot Label {slot.Index}", worldLayer, UIKit.Cream, false);
                UIKit.Round(bg, 16);
                UIKit.AddShadow(bg, UIKit.ShadowColor, 4);
                bg.rectTransform.sizeDelta = new Vector2(170, 46);
                var follower = bg.gameObject.AddComponent<WorldFollower>();
                follower.target = slot.transform;
                follower.worldOffset = new Vector3(0, slot.IsBoard ? 1.9f : 1.25f, 0); // 뽑기판은 키가 커서 더 위
                follower.cam = cam;
                var label = UIKit.Label("Text", bg.transform, "", 26, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Stretch(label.rectTransform, 4, 2, 4, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var icon = Icon(bg.transform, null, new Vector2(0, 0.5f), new Vector2(20, 2), 58);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                slotLabels[slot] = new SlotLabel { bg = bg, text = label, icon = icon, juice = bg.gameObject.AddComponent<UIJuice>() };
            }

        void UpdateSlotLabel(ShelfSlot slot, SlotLabel l)
        {
            bool morning = gm.Phase == DayPhase.Morning;
            string combo = gm.SlotCombo(slot.Index);
            if (l.shown && l.product == slot.Product && l.count == slot.Count && l.morning == morning && l.combo == combo) return;
            l.combo = combo;
            l.shown = true;
            l.product = slot.Product;
            l.count = slot.Count;
            l.morning = morning;
            // 빈 매대 표시는 진열하는 아침에만 (영업 중엔 가게가 덜 복잡하게)
            bool visible = (slot.Product != null || morning) && !decorMode; // 장식 모드에선 장식 표시가 대신
            // 뽑기판은 번호 뽑기가 해금되기 전엔 이름표를 숨김 ("빈 매대"로 헷갈리지 않게)
            if (slot.IsBoard && !gm.IsUnlocked(gm.Catalog.Find(GameManager.BoardProductId))) visible = false;
            if (l.bg.gameObject.activeSelf != visible) l.bg.gameObject.SetActive(visible);
            if (!visible) return;
            l.text.fontSize = slot.Product == null ? 22 : 26;
            if (slot.Product == null)
            {
                l.text.text = "빈 매대";
                l.text.color = UIKit.Muted;
                l.bg.color = new Color(1f, 1f, 1f, 0.45f);
            }
            else if (slot.Count == 0)
            {
                l.text.text = $"{slot.Product.displayName} 품절";
                l.text.color = Color.white;
                l.bg.color = UIKit.Red;
            }
            else
            {
                l.text.text = $"{slot.Product.displayName} <color={RedHex}>{slot.Count}</color>";
                l.text.color = UIKit.Ink;
                l.bg.color = combo != null ? UIKit.Yellow : UIKit.Cream; // 콤보 중인 진열대는 노란 이름표
                if (combo != null) l.text.text = $"★{l.text.text}";
            }
            // 아이콘은 이름표 왼쪽 끝에 걸쳐 붙고, 글자는 그만큼 오른쪽으로
            var sprite = ProductIcons.Get(slot.Product);
            l.icon.sprite = sprite;
            l.icon.enabled = sprite != null;
            float iconPad = sprite != null ? 40f : 0f;
            l.text.rectTransform.offsetMin = new Vector2(4f + iconPad, l.text.rectTransform.offsetMin.y);
            float w = Mathf.Clamp(l.text.preferredWidth + 30f + iconPad, 120f, 360f);
            l.bg.rectTransform.sizeDelta = new Vector2(w, 46f);
        }

        static readonly string RedHex = UIKit.Hex(UIKit.Red);

        // ───────────── 손님 말풍선 ─────────────

        const float TailHeight = 26f;

        public CustomerBubble CreateBubble(Transform target)
        {
            var root = UIKit.Rect("Bubble", worldLayer);
            root.sizeDelta = new Vector2(300, 92 + TailHeight);
            root.pivot = new Vector2(0.5f, 0f);
            var follower = root.gameObject.AddComponent<WorldFollower>();
            follower.target = target;
            follower.worldOffset = new Vector3(0, 1.45f, 0);
            follower.cam = cam;

            var ink = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.9f);
            var tailBorder = UIKit.Rect("Tail Border", root).gameObject.AddComponent<Image>();
            tailBorder.sprite = UIKit.TriangleSprite;
            tailBorder.color = ink;
            tailBorder.raycastTarget = false;
            UIKit.Place(tailBorder.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(56, 44));

            var body = UIKit.Panel("Body", root, UIKit.BubbleNormal, false);
            UIKit.Round(body, 30);
            UIKit.AddShadow(body, UIKit.ShadowColor, 6);
            UIKit.Stretch(body.rectTransform, 0, TailHeight, 0, 0);
            UIKit.Ring(body.transform, ink, 30);

            var label = UIKit.Label("Text", body.transform, "", 32, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Stretch(label.rectTransform, 14, 20, 14, 2);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var icon = Icon(body.transform, null, new Vector2(0, 0.5f), new Vector2(46, 8), 76);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var fillBg = UIKit.Panel("Patience", body.transform, new Color(0, 0, 0, 0.13f), false);
            UIKit.Round(fillBg, 6);
            UIKit.Bottom(fillBg.rectTransform, 11, 12, 24);
            var fill = UIKit.Panel("Fill", fillBg.transform, UIKit.Red, false);
            UIKit.Round(fill, 5);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var tailFill = UIKit.Rect("Tail", root).gameObject.AddComponent<Image>();
            tailFill.sprite = UIKit.TriangleSprite;
            tailFill.color = UIKit.BubbleNormal;
            tailFill.raycastTarget = false;
            UIKit.Place(tailFill.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(40, 34));

            // "탭!" 스티커 (계산 가능할 때만)
            var tap = UIKit.CardPanel("Tap", root, UIKit.Red, 20, false, 4);
            UIKit.Place(tap.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-4, -2), new Vector2(70, 42));
            tap.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
            UIKit.Ring(tap.transform, Color.white, 20, -3);
            var tapText = UIKit.Label("Text", tap.transform, "탭!", 25, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tapText.rectTransform, 0, 2, 0, 0);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = body;
            var bubble = root.gameObject.AddComponent<CustomerBubble>();
            bubble.Setup(body, label, fillBg.gameObject, fill.rectTransform, btn);
            bubble.SetDecor(tailFill, tap.gameObject);
            bubble.SetIconImage(icon);

            bubbles.RemoveAll(IsDead);
            bubbles.Add(bubble);
            return bubble;
        }

        // ───────────── 모달 ─────────────

        GameObject Modal(string name, out RectTransform card, float height, out UIJuice juice)
        {
            var dim = UIKit.Square(UIKit.Panel(name, transform, UIKit.Dim));
            UIKit.Stretch(dim.rectTransform);
            var c = UIKit.CardPanel("Card", dim.transform, UIKit.Cream, 36, true, 14);
            card = c.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(984, height);
            UIKit.TapeStrip(card, new Vector2(0, 1), new Vector2(84, -6), 14);
            UIKit.TapeStrip(card, new Vector2(1, 1), new Vector2(-84, -6), -12);
            juice = c.gameObject.AddComponent<UIJuice>();
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        /// <summary>도매상 카드 높이: 줄 수에 맞추되 화면을 넘으면 스크롤.</summary>
        static float WholesaleHeight(int rows) => Mathf.Clamp(220f + rows * 130f + 26f + 60f + 190f, 760f, 1640f);

        static void OpenModal(GameObject modal, UIJuice cardJuice)
        {
            if (modal.activeSelf) return;
            modal.SetActive(true);
            if (cardJuice != null) cardJuice.Punch(0.06f);
        }

        void BuildWholesaleModal()
        {
            int rows = gm.Catalog.products.Count;
            wholesaleModal = Modal("Wholesale Modal", out var card, WholesaleHeight(rows), out wholesaleCardJuice);
            wholesaleCard = card;

            var title = UIKit.Label("Title", card, "도매상", 60, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 80, 40, 44);
            var sub = UIKit.Label("Sub", card, "상자로 사면 창고에 들어가요. 창고에서 골라 매대를 탭!", 27, UIKit.Muted);
            UIKit.Top(sub.rectTransform, 44, 122, 44);

            var pill = UIKit.CardPanel("Money", card, UIKit.Yellow, 34, false, 5);
            UIKit.Place(pill.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, -80), new Vector2(340, 70));
            var coin = UIKit.Coin(pill.transform, 46);
            UIKit.Place(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(46, 46));
            wholesaleMoney = UIKit.Label("Text", pill.transform, "", 38, UIKit.Ink, TextAnchor.MiddleRight);
            UIKit.Stretch(wholesaleMoney.rectTransform, 64, 2, 22, 0);
            wholesaleMoney.horizontalOverflow = HorizontalWrapMode.Overflow;

            var divider = UIKit.DashLine(card, UIKit.WithAlpha(UIKit.KraftDark, 0.6f), 4);
            UIKit.Top(divider.rectTransform, 4, 178, 40);

            // 스크롤 목록 (상품이 20종 이상이어도 OK)
            var viewport = UIKit.Rect("List", card);
            UIKit.Stretch(viewport, 22, 176, 22, 188);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(8, 8, 10, 16);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            wholesaleScroll = card.gameObject.AddComponent<ScrollRect>();
            wholesaleScroll.viewport = viewport;
            wholesaleScroll.content = content;
            wholesaleScroll.horizontal = false;
            wholesaleScroll.vertical = true;
            wholesaleScroll.movementType = ScrollRect.MovementType.Elastic;
            wholesaleScroll.elasticity = 0.12f;
            wholesaleScroll.scrollSensitivity = 30f;

            foreach (var p in gm.Catalog.products)
            {
                if (p == null) continue;
                wholesale[p] = BuildWholesaleRow(p, content);
            }

            wholesaleFooter = UIKit.Label("Footer", content, "", 26, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Layout(wholesaleFooter, -1, 60);

            closeWholesaleButton = UIKit.Button("Close", card, "다 샀어요! 진열하러 가기", 42, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)closeWholesaleButton.transform, 112, 44, 44);
            closeWholesaleButton.onClick.AddListener(() => wholesaleModal.SetActive(false));
        }

        WholesaleRow BuildWholesaleRow(ProductData p, Transform list)
        {
            var product = p;
            var row = UIKit.CardPanel(p.id, list, UIKit.Card, 24, true, 6);
            UIKit.Layout(row, -1, 118);

            var sw = UIKit.Panel("Swatch", row.transform, Tint(p.color), false);
            UIKit.Round(sw, 20);
            UIKit.Ring(sw.transform, new Color(1, 1, 1, 0.95f), 20);
            UIKit.Place(sw.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(78, 78));
            var icon = Icon(row.transform, p, new Vector2(0, 0.5f), new Vector2(61, 2), 96);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var name = UIKit.Label("Name", row.transform, p.displayName, 36, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(name.rectTransform, 116, 0, 250, 16);
            var info = UIKit.Label("Info", row.transform, "", 24, UIKit.Muted, TextAnchor.LowerLeft);
            UIKit.Stretch(info.rectTransform, 116, 16, 244, 0);

            var trendTag = Sticker(row.transform, "유행", UIKit.Red, new Vector2(0, 1), new Vector2(34, -12), -14);
            var newTag = Sticker(row.transform, "NEW", UIKit.Green, new Vector2(0, 0), new Vector2(34, 14), 10);

            var buy = UIKit.Button("Buy", row.transform, UIKit.Won(gm.BoxCost(p)), 32, UIKit.Green, Color.white, out var buyLabel);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = new Vector2(1, 0); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(1, 0.5f);
            brt.offsetMin = new Vector2(-226, 20); brt.offsetMax = new Vector2(-16, -14);
            buy.onClick.AddListener(() => gm.BuyBox(product));

            return new WholesaleRow { buy = buy, buyLabel = buyLabel, info = info, trendTag = trendTag, newTag = newTag, juice = buy.GetComponent<UIJuice>() };
        }

        void BuildSettlementModal()
        {
            settlementModal = Modal("Settlement Modal", out settlementCard, 1200, out settlementCardJuice);

            settlementTitle = UIKit.Label("Title", settlementCard, "", 62, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Top(settlementTitle.rectTransform, 84, 44, 40);
            settlementSub = UIKit.Label("Sub", settlementCard, "오늘 장사 끝! 장부를 정리해요", 28, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Top(settlementSub.rectTransform, 40, 128, 40);

            var lines = UIKit.NoteLines(settlementCard, 62);
            UIKit.Stretch(lines.rectTransform, 24, 186, 24, 186);
            var margin = UIKit.Square(UIKit.Panel("Margin", settlementCard, UIKit.Margin, false));
            margin.rectTransform.anchorMin = new Vector2(0, 0);
            margin.rectTransform.anchorMax = new Vector2(0, 1);
            margin.rectTransform.offsetMin = new Vector2(84, 186);
            margin.rectTransform.offsetMax = new Vector2(87, -186);

            settlementRows = UIKit.Rect("Rows", settlementCard);
            UIKit.Stretch(settlementRows, 110, 200, 64, 196);
            var vlg = settlementRows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 아무 데나 탭하면 카운트업 건너뛰기
            var skip = UIKit.Rect("Skip", settlementCard);
            UIKit.Stretch(skip, 0, 180, 0, 0);
            var skipImg = skip.gameObject.AddComponent<Image>();
            skipImg.color = new Color(0, 0, 0, 0);
            skip.gameObject.AddComponent<Button>().onClick.AddListener(() => settlementSkip = true);

            // "참 잘했어요" 도장
            stampRing = UIKit.Ring(UIKit.Rect("Stamp", settlementCard), UIKit.Red, 110);
            var stampRt = (RectTransform)stampRing.transform.parent;
            // 제목 오른쪽 위 구석 (숫자를 가리지 않게)
            UIKit.Place(stampRt, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-130, -112), new Vector2(190, 190));
            stampRt.localRotation = Quaternion.Euler(0, 0, -14f);
            stampText = UIKit.Label("Text", stampRt, "", 34, UIKit.Red, TextAnchor.MiddleCenter);
            UIKit.Stretch(stampText.rectTransform, 16, 16, 16, 16);
            stampText.lineSpacing = 0.9f;
            stamp = stampRt.gameObject;
            stampJuice = stamp.AddComponent<UIJuice>();
            stamp.SetActive(false);

            var next = UIKit.Button("Next", settlementCard, "다음 날로!", 46, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)next.transform, 112, 48, 64);
            next.onClick.AddListener(() =>
            {
                StopSettlementAnim();
                settlementModal.SetActive(false);
                gm.NextDay();
            });
        }

        void BuildSettings()
        {
            // 위치: 상단 바 안, 단계 글자와 돈 띠 사이 (돈 띠 왼쪽). 속도 버튼(오른쪽 아래)과 겹치지 않음
            gearButton = SettingsPanel.BuildGearButton(overlaySafe, new Vector2(-456, -72), 88, () => settings.Toggle());
#if UNITY_EDITOR || MUNBANGGU_CHEATS
            var modal = Modal("Settings Modal", out var card, 1060, out var juice); // 테스트 버튼 줄만큼 더 높게
#else
            var modal = Modal("Settings Modal", out var card, 900, out var juice);
#endif
            settings = SettingsPanel.Build(modal, card, juice);
        }

        void BuildToast(Transform parent)
        {
            toastBg = UIKit.CardPanel("Toast", parent, ToastColor, 40, false, 8);
            var rt = toastBg.rectTransform;
            UIKit.Place(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 500), new Vector2(900, 96));
            toastText = UIKit.Label("Text", toastBg.transform, "", 34, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(toastText.rectTransform, 24, 2, 24, 0);
            toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastGroup = toastBg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toastJuice = toastBg.gameObject.AddComponent<UIJuice>();
        }

        // ───────────── 외부에서 호출 ─────────────

        public void Toast(string message, float seconds = 1.8f)
        {
            toastJuice.StopShake();
            toastText.text = message;
            float w = Mathf.Clamp(toastText.preferredWidth + 80f, 360f, 1020f);
            toastBg.rectTransform.sizeDelta = new Vector2(w, 96f);
            toastBg.color = ToastColor;
            toastRedTimer = 0f;
            toastTimer = seconds;
            toastGroup.alpha = 1f;
            toastJuice.Punch(0.1f);
        }

        public void ShowMorning()
        {
            StopSettlementAnim();
            settlementModal.SetActive(false);
            if (wholesaleScroll != null) wholesaleScroll.verticalNormalizedPosition = 1f;
            OpenModal(wholesaleModal, wholesaleCardJuice);
        }

        public bool IsSettingsOpen => settings != null && settings.IsOpen;

        // ───────────── 두 갈래 선택 창 (외상 등 돌발 사건) ─────────────

        GameObject choiceModal;
        UIJuice choiceJuice;
        Text choiceTitle, choiceBody, choiceYesLabel, choiceNoLabel;
        Action<bool> choiceCallback;

        /// <summary>선택 창이 열려 있으면 게임 시간이 멈춘다 (GameManager.ApplySpeed).</summary>
        public bool IsChoiceOpen => choiceModal != null && choiceModal.activeSelf;

        // ───────────── 상담 고르기 창 ─────────────

        PickerPanel picker;

        /// <summary>상담 고르기 창이 열려 있으면 게임 시간이 멈춘다.</summary>
        public bool IsPickerOpen => picker != null && picker.IsOpen;

        /// <summary>상담 손님 고민을 보여 주고 매대 상품 중 하나를 고르게 한다 (null = 맞는 게 없음).</summary>
        public void ShowPicker(Munbanggu.Customers.CounselRequest request, Action<ProductData> onPick) => picker.Open(request, onPick);

        /// <summary>고르기 창을 아무것도 안 고른 것으로 닫는다 (스크린샷 도구 등).</summary>
        public void CancelPicker() => picker.Cancel();

        void BuildChoice()
        {
            choiceModal = Modal("Choice Modal", out var card, 620, out choiceJuice);
            card.sizeDelta = new Vector2(920, 620);

            choiceTitle = UIKit.Label("Title", card, "", 56, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(choiceTitle.rectTransform, 84, 44, 56);

            choiceBody = UIKit.Label("Body", card, "", 34, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(choiceBody.rectTransform, 56, 190, 56, 150);
            choiceBody.lineSpacing = 1.15f;

            var no = UIKit.Button("No", card, "", 40, UIKit.Red, Color.white, out choiceNoLabel);
            var noRt = (RectTransform)no.transform;
            noRt.anchorMin = new Vector2(0, 0); noRt.anchorMax = new Vector2(0.4f, 0);
            noRt.pivot = new Vector2(0.5f, 0);
            noRt.offsetMin = new Vector2(48, 48); noRt.offsetMax = new Vector2(-12, 160);
            no.onClick.AddListener(() => PickChoice(false));

            var yes = UIKit.Button("Yes", card, "", 40, UIKit.Green, Color.white, out choiceYesLabel);
            var yesRt = (RectTransform)yes.transform;
            yesRt.anchorMin = new Vector2(0.4f, 0); yesRt.anchorMax = new Vector2(1, 0);
            yesRt.pivot = new Vector2(0.5f, 0);
            yesRt.offsetMin = new Vector2(12, 48); yesRt.offsetMax = new Vector2(-48, 160);
            yes.onClick.AddListener(() => PickChoice(true));
        }

        /// <summary>두 갈래 선택 창을 띄운다. 고르면 onPick(true=yes) 후 닫힘. 열린 동안 게임 시간 정지.</summary>
        public void ShowChoice(string title, string body, string yesLabel, string noLabel, Action<bool> onPick)
        {
            // 이미 열린 선택이 있으면 그건 '아니오'로 정리하고 새로 연다
            if (IsChoiceOpen) PickChoice(false);
            choiceTitle.text = title;
            choiceBody.text = body;
            choiceYesLabel.text = yesLabel;
            choiceNoLabel.text = noLabel;
            choiceCallback = onPick;
            OpenModal(choiceModal, choiceJuice);
        }

        /// <summary>열린 선택 창을 '아니오'로 닫는다 (스크린샷 도구 등).</summary>
        public void CloseChoice() { if (IsChoiceOpen) PickChoice(false); }

        void PickChoice(bool yes)
        {
            var cb = choiceCallback;
            choiceCallback = null;
            choiceModal.SetActive(false);
            cb?.Invoke(yes);
        }
        public void OpenSettings() => settings.Open();
        public void CloseSettings() => settings.Close();

        public void OpenUpgrades() => upgradePanel.Open();
        public void OpenStaff() => staffPanel.Open();

        // ───────────── 장식 배치 모드 ─────────────

        DecorPanel decorPanel;
        bool decorMode;
        GameObject decorModeRoot;
        readonly List<(ShelfSlot slot, Text label, Image bg)> decorMarkers = new List<(ShelfSlot, Text, Image)>();

        public bool InDecorMode => decorMode;

        public void OpenDecor(int slotIndex) => decorPanel.Open(slotIndex);

        /// <summary>
        /// 장식 배치 모드: 진열대마다 위에 "＋ 장식" 표시가 뜨고, 탭하면 그 진열대 장식 창이 열린다.
        /// 위쪽 띠의 "완료"로 끝. 아침에만.
        /// </summary>
        public void EnterDecorMode()
        {
            if (gm.Phase != DayPhase.Morning) return;
            ExitDecorMode();
            decorMode = true;
            foreach (var l in slotLabels.Values) l.shown = false;
            // 위쪽 안내 띠 + 완료 버튼
            var banner = UIKit.CardPanel("Decor Mode Banner", overlaySafe, UIKit.Blue, 28, true, 6);
            decorModeRoot = banner.gameObject;
            UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -214), new Vector2(980, 120));
            var t = UIKit.Label("Text", banner.transform, "장식할 진열대를 탭하세요", 36, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.rectTransform, 36, 4, 260, 0);
            var done = UIKit.Button("Done", banner.transform, "완료", 38, UIKit.Yellow, UIKit.Ink, out _);
            var drt = (RectTransform)done.transform;
            drt.anchorMin = new Vector2(1, 0); drt.anchorMax = new Vector2(1, 1);
            drt.pivot = new Vector2(1, 0.5f);
            drt.offsetMin = new Vector2(-220, 16); drt.offsetMax = new Vector2(-20, -16);
            done.onClick.AddListener(ExitDecorMode);

            // 진열대 위 표시 (뽑기판 제외)
            foreach (var slot in gm.Layout.Slots)
            {
                if (slot.IsBoard) continue;
                var s = slot;
                var btn = UIKit.Button($"Decor Marker {slot.Index}", worldLayer, "", 21, UIKit.Cream, UIKit.Ink, out var label);
                var rt = (RectTransform)btn.transform;
                rt.SetParent(worldLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(164, 66);
                label.lineSpacing = 0.9f;
                var follower = btn.gameObject.AddComponent<WorldFollower>();
                follower.target = slot.transform;
                follower.worldOffset = new Vector3(0, 1.75f, 0);
                follower.cam = cam;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                btn.onClick.AddListener(() => decorPanel.Open(s.Index));
                decorMarkers.Add((slot, label, (Image)btn.targetGraphic));
            }
            UpdateDecorMarkers();
        }

        public void ExitDecorMode()
        {
            decorMode = false;
            foreach (var l in slotLabels.Values) l.shown = false;
            foreach (var (_, _, bg) in decorMarkers)
                if (bg != null) Kill(bg.gameObject);
            decorMarkers.Clear();
            if (decorModeRoot != null) Kill(decorModeRoot);
            decorModeRoot = null;
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        void UpdateDecorMarkers()
        {
            if (gm.Phase != DayPhase.Morning) { ExitDecorMode(); return; }
            // 장식 창이 열려 있으면 띠·표시를 숨겨 창 제목을 가리지 않게
            bool panelOpen = decorPanel.IsOpen;
            if (decorModeRoot != null && decorModeRoot.activeSelf == panelOpen) decorModeRoot.SetActive(!panelOpen);
            foreach (var (slot, label, bg) in decorMarkers)
            {
                if (bg.gameObject.activeSelf == panelOpen) bg.gameObject.SetActive(!panelOpen);
                var placed = gm.Facilities.At(slot.Index);
                var def = placed != null ? FacilityTable.FindDecor(placed.id) : null;
                string top = slot.Product != null ? slot.Product.displayName : "빈 매대";
                string mark = gm.SlotCombo(slot.Index) != null ? "★" : "";
                string text = $"<size=17>{mark}{top}</size>\n" + (def != null ? $"{def.name} Lv{placed.level}" : "＋ 장식");
                if (label.text != text) label.text = text;
                var color = gm.SlotCombo(slot.Index) != null ? UIKit.Yellow : def != null ? UIKit.Card : UIKit.Cream;
                if (bg.color != color) bg.color = color;
            }
        }

        // ───────────── 진열대 옮기기 모드 ─────────────

        bool moveMode;
        ShelfSlot moveSel;
        GameObject moveRoot, moveMarker;
        Text moveText;
        Button moveRotate;

        public bool InMoveMode => moveMode;

        /// <summary>
        /// 진열대 옮기기: 진열대를 탭해서 고르고 → 바닥을 탭하면 그 자리로, "돌리기"로 90°씩 방향 바꾸기.
        /// 길을 막거나 겹치면 안 되고 이유를 알려 준다. 아침에만.
        /// </summary>
        public void EnterMoveMode()
        {
            if (gm.Phase != DayPhase.Morning) return;
            CloseModals();
            moveMode = true;
            moveSel = null;
            var banner = UIKit.CardPanel("Move Mode Banner", overlaySafe, UIKit.Green, 28, true, 6);
            moveRoot = banner.gameObject;
            UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -214), new Vector2(1000, 190));
            moveText = UIKit.Label("Text", banner.transform, "", 30, Color.white, TextAnchor.UpperLeft);
            UIKit.Stretch(moveText.rectTransform, 30, 80, 30, 14);
            moveText.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button Btn(string name, string label, float x0, float x1, Color bg, Color fg, UnityEngine.Events.UnityAction a)
            {
                var b = UIKit.Button(name, banner.transform, label, 30, bg, fg, out _);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(x0, 0); rt.anchorMax = new Vector2(x1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.offsetMin = new Vector2(10, 14); rt.offsetMax = new Vector2(-10, 80);
                b.onClick.AddListener(a);
                return b;
            }
            moveRotate = Btn("Rotate", "방향 돌리기", 0f, 0.36f, UIKit.Yellow, UIKit.Ink, RotateSelected);
            Btn("Reset", "처음대로", 0.36f, 0.68f, UIKit.Cream, UIKit.Ink, () =>
                ShowChoice("진열대를 처음 자리로?", "옮긴 진열대를 전부 처음 자리·방향으로 되돌려요.", "되돌려요", "아니요",
                    yes => { if (yes) { gm.ResetShelfPlacements(); SelectForMove(null); Toast("진열대를 처음 자리로 되돌렸어요"); } }));
            Btn("Done", "완료", 0.68f, 1f, UIKit.Red, Color.white, ExitMoveMode);
            SelectForMove(null);
        }

        public void ExitMoveMode()
        {
            moveMode = false;
            moveSel = null;
            if (moveRoot != null) Kill(moveRoot);
            if (moveMarker != null) Kill(moveMarker);
            moveRoot = moveMarker = null;
        }

        void SelectForMove(ShelfSlot s)
        {
            moveSel = s;
            if (moveMarker != null) Kill(moveMarker);
            moveMarker = null;
            if (moveRotate != null) moveRotate.interactable = s != null;
            if (s == null)
            {
                moveText.text = "옮길 진열대를 탭하세요";
                return;
            }
            string name = s.Product != null ? s.Product.displayName : "빈";
            moveText.text = $"{name} 진열대 선택! 놓을 바닥을 탭하세요 (앞쪽에 손님 설 자리가 있어야 해요)";
            // 고른 진열대 위 표시
            var tag = UIKit.Panel("Move Marker", worldLayer, UIKit.Green, false);
            UIKit.Round(tag, 16);
            tag.rectTransform.sizeDelta = new Vector2(150, 50);
            var tl = UIKit.Label("Text", tag.transform, "▼ 옮기는 중", 24, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tl.rectTransform);
            tl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = tag.gameObject.AddComponent<WorldFollower>();
            f.target = s.transform;
            f.worldOffset = new Vector3(0, 1.8f, 0);
            f.cam = cam;
            moveMarker = tag.gameObject;
        }

        void RotateSelected()
        {
            if (moveSel == null) return;
            var t = moveSel.transform;
            if (gm.TryMoveShelf(moveSel, t.position, t.eulerAngles.y + 90f, out string reason)) Toast("방향을 돌렸어요");
            else Toast(reason);
        }

        /// <summary>옮기기 모드일 때 가게 화면 탭을 여기서 처리 (처리했으면 true).</summary>
        public bool HandleWorldTap(Vector2 screenPos)
        {
            if (!moveMode) return false;
            if (gm.Phase != DayPhase.Morning) { ExitMoveMode(); return false; }
            Physics.SyncTransforms(); // 방금 옮긴 진열대 콜라이더도 맞게
            var ray = cam.ScreenPointToRay(screenPos);
            ShelfSlot hit = null;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(ray, 200f))
            {
                var s = h.collider.GetComponentInParent<ShelfSlot>();
                if (s != null && !s.IsBoard && h.distance < best) { hit = s; best = h.distance; }
            }
            if (hit != null)
            {
                SelectForMove(hit == moveSel ? null : hit);
                return true;
            }
            if (moveSel == null) { Toast("먼저 옮길 진열대를 탭하세요"); return true; }
            // 바닥(y=0) 위 지점
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return true;
            float tHit = -ray.origin.y / ray.direction.y;
            Vector3 p = ray.origin + ray.direction * tHit;
            if (gm.TryMoveShelf(moveSel, p, moveSel.transform.eulerAngles.y, out string reason))
                Toast("여기로 옮겼어요!");
            else
                Toast(reason);
            return true;
        }

        public void CloseModals()
        {
            wholesaleModal.SetActive(false);
            upgradePanel.Close();
            staffPanel.Close();
            decorPanel.Close();
            townPanel.Close();
            ExitDecorMode();
            ExitMoveMode();
            StopSettlementAnim();
            settlementModal.SetActive(false);
        }

        public void ShowSettlement()
        {
            settlementTitle.text = $"{gm.Day}일차 장부";
            BuildSettlementLines(lineBuffer);
            BuildSettlementRows(lineBuffer);
            stamp.SetActive(false);
            OpenModal(settlementModal, settlementCardJuice);
            StopSettlementAnim();
            settlementSkip = false;
            if (isActiveAndEnabled && Application.isPlaying) settlementAnim = StartCoroutine(CountUpSettlement());
            else FinishSettlementInstant();
        }

        // ───────────── 정산 (장부) ─────────────

        void BuildSettlementLines(List<SettlementLine> lines)
        {
            lines.Clear();
            var s = gm.Stats;
            lines.Add(SettlementLine.Money("매출", s.revenue));
            lines.Add(SettlementLine.Money("매입", s.cost, true, UIKit.Red));
            lines.Add(SettlementLine.Total("순이익", s.Profit));
            lines.Add(SettlementLine.Gap());
            lines.Add(SettlementLine.Count("찾아온 손님", s.visitors));
            lines.Add(SettlementLine.Count("계산한 손님", s.served, "명", UIKit.Blue));
            if (s.lostNoStock > 0) lines.Add(SettlementLine.Count("물건이 없어 그냥 간 손님", s.lostNoStock, "명", UIKit.Muted));
            if (s.lostImpatient > 0) lines.Add(SettlementLine.Count("기다리다 간 손님", s.lostImpatient, "명", UIKit.Muted));
            try { CollectSettlementLines?.Invoke(gm, lines); }
            catch (Exception e) { Debug.LogException(e); }
            lines.Add(SettlementLine.Gap());
            lines.Add(SettlementLine.Money("가진 돈", gm.Money, false, UIKit.Ink));

            if (s.lostNoStock > s.served / 4 + 1)
                lines.Add(SettlementLine.Tip("팁: 유행 상품과 인기 상품을 넉넉히 진열해 보세요"));
            else if (s.lostImpatient > 2)
                lines.Add(SettlementLine.Tip("팁: 줄 선 손님 말풍선을 빨리 탭해서 계산하세요"));
        }

        void BuildSettlementRows(List<SettlementLine> lines)
        {
            for (int i = settlementRows.childCount - 1; i >= 0; i--)
            {
                var child = settlementRows.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            settlementRowList.Clear();

            float total = 0f;
            foreach (var line in lines)
            {
                var row = new SettlementRow { line = line };
                switch (line.kind)
                {
                    case SettlementLineKind.Gap:
                    {
                        var gap = UIKit.Rect("Gap", settlementRows);
                        UIKit.Layout(gap, -1, 24);
                        total += 24;
                        continue;
                    }
                    case SettlementLineKind.Tip:
                    {
                        var t = UIKit.Label("Tip", settlementRows, line.label, 28, UIKit.Muted, TextAnchor.MiddleLeft);
                        UIKit.Layout(t, -1, 84);
                        row.group = t.gameObject.AddComponent<CanvasGroup>();
                        total += 84;
                        break;
                    }
                    case SettlementLineKind.Text:
                    {
                        var t = UIKit.Label("Text", settlementRows, line.label, 32, line.color.a > 0f ? line.color : UIKit.Ink, TextAnchor.MiddleLeft);
                        UIKit.Layout(t, -1, 56);
                        row.group = t.gameObject.AddComponent<CanvasGroup>();
                        total += 56;
                        break;
                    }
                    default:
                    {
                        bool big = line.kind == SettlementLineKind.Total;
                        float h = big ? 96f : 62f;
                        var rt = UIKit.Rect(line.label, settlementRows);
                        UIKit.Layout(rt, -1, h);
                        if (big)
                        {
                            var dash = UIKit.DashLine(rt, UIKit.WithAlpha(UIKit.Ink, 0.5f), 4);
                            UIKit.Top(dash.rectTransform, 4, 6, 0);
                        }
                        var label = UIKit.Label("Label", rt, line.label, big ? 44 : 34, UIKit.Ink, TextAnchor.MiddleLeft);
                        UIKit.Stretch(label.rectTransform, 0, 0, 300, big ? 8 : 0);
                        Color vc = line.color.a > 0f ? line.color
                            : big ? (line.value >= 0 ? UIKit.Green : UIKit.Red) : UIKit.Ink;
                        var value = UIKit.Label("Value", rt, line.Format(0), big ? 54 : 38, vc, TextAnchor.MiddleRight);
                        UIKit.Stretch(value.rectTransform, 200, 0, 0, big ? 8 : 0);
                        value.horizontalOverflow = HorizontalWrapMode.Overflow;
                        row.value = value;
                        row.juice = value.gameObject.AddComponent<UIJuice>();
                        row.group = rt.gameObject.AddComponent<CanvasGroup>();
                        total += h;
                        break;
                    }
                }
                row.group.alpha = 0f;
                settlementRowList.Add(row);
            }

            float height = Mathf.Clamp(200f + total + 200f, 900f, 1700f);
            settlementCard.sizeDelta = new Vector2(984, height);
        }

        IEnumerator CountUpSettlement()
        {
            yield return Wait(0.25f);
            foreach (var row in settlementRowList)
            {
                if (settlementSkip) break;
                // 한 줄씩 나타나기
                for (float t = 0f; t < 0.14f && !settlementSkip; t += Time.unscaledDeltaTime)
                {
                    row.group.alpha = t / 0.14f;
                    yield return null;
                }
                row.group.alpha = 1f;
                if (row.value == null) { yield return Wait(0.08f); continue; }

                int target = row.line.value;
                float dur = Mathf.Clamp(0.25f + Mathf.Log10(Mathf.Abs(target) + 1f) * 0.1f, 0.25f, 0.65f);
                int shown = int.MinValue;
                for (float t = 0f; t < dur && !settlementSkip; t += Time.unscaledDeltaTime)
                {
                    float k = 1f - Mathf.Pow(1f - t / dur, 3f);
                    int v = Mathf.RoundToInt(target * k);
                    if (v != shown) { shown = v; row.value.text = row.line.Format(v); }
                    yield return null;
                }
                row.value.text = row.line.Format(target);
                row.juice.Punch(row.line.kind == SettlementLineKind.Total ? 0.3f : 0.15f);
                yield return Wait(row.line.kind == SettlementLineKind.Total ? 0.3f : 0.1f);
            }
            FinishSettlementInstant();
            stampJuice.Punch(0.35f);
            settlementAnim = null;
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !settlementSkip; t += Time.unscaledDeltaTime) yield return null;
        }

        void FinishSettlementInstant()
        {
            foreach (var row in settlementRowList)
            {
                row.group.alpha = 1f;
                if (row.value != null) row.value.text = row.line.Format(row.line.value);
            }
            bool good = gm.Stats.Profit > 0;
            stampText.text = good ? "참\n잘했어요" : "내일은\n더 잘해요";
            stampText.color = good ? UIKit.Red : UIKit.Blue;
            stampRing.color = good ? UIKit.Red : UIKit.Blue;
            stamp.SetActive(true);
        }

        void StopSettlementAnim()
        {
            if (settlementAnim != null) StopCoroutine(settlementAnim);
            settlementAnim = null;
        }

        // ───────────── 연출 (GameEvents 구독) ─────────────

        void OnCheckedOut(Customer c, int price)
        {
            if (c == null) return;
            pendingMoney += price;
            fx.CoinBurst(c.transform.position + Vector3.up * 1.5f, price);
        }

        void OnCoinArrived(int value, bool last)
        {
            pendingMoney = Mathf.Max(0, pendingMoney - value);
            coinJuice.Punch(0.3f);
            if (last)
            {
                moneyJuice.Punch(0.12f);
                fx.PopText(fx.RectToLocal((RectTransform)moneyJuice.transform) + new Vector2(0, -70), "+" + UIKit.Won(value), UIKit.Green, 40);
            }
        }

        void OnItemsPlaced(ShelfSlot slot, ProductData product, int count)
        {
            if (slot == null) return;
            fx.PopTextAtWorld(slot.transform.position + Vector3.up * 1.7f, $"+{count}", UIKit.Blue, 58);
            if (slotLabels.TryGetValue(slot, out var l)) l.juice.Punch(0.25f);
        }

        void OnBoxBought(ProductData p)
        {
            backroomTabJuice.Punch(0.3f);
            if (p != null && invByProduct.TryGetValue(p, out var item)) item.juice.Punch(0.22f);
            if (p != null && wholesale.TryGetValue(p, out var row))
            {
                row.juice.Punch(0.15f);
                fx.PopText(fx.RectToLocal((RectTransform)row.buy.transform) + new Vector2(0, 40), $"+{p.boxSize}개", UIKit.Green, 40);
            }
            moneyJuice.Punch(0.08f);
        }

        void OnInvalidAction()
        {
            // GameManager가 Toast를 먼저 띄우고 이 이벤트를 보냄 → 방금 뜬 토스트를 흔든다
            if (toastGroup.alpha <= 0f) Toast("그건 안 돼요");
            toastJuice.Shake(22f);
            toastRedTimer = 0.4f;
        }

        void OnRushStarted()
        {
            // 배너가 같은 내용을 크게 보여주므로 토스트는 숨김
            toastTimer = 0f;
            toastGroup.alpha = 0f;
            fx.Banner("하교 러시!", "손님이 몰려와요! 말풍선을 빨리 탭!", UIKit.Red);
            timeJuice.Punch(0.15f);
        }

        void OnPhaseChanged(DayPhase phase)
        {
            phaseJuice.Punch(0.15f);
            if (phase == DayPhase.Open) fx.Banner("영업 시작!", "손님이 오면 말풍선을 탭해서 계산해요", UIKit.Blue);
            if (phase == DayPhase.Morning) dayJuice.Punch(0.25f);
        }

        // ───────────── 갱신 ─────────────

        void Refresh()
        {
            dayText.text = $"{gm.Day}일차";
            if (upgradePanel.IsOpen) upgradePanel.Refresh();
            RefreshTrend();
            actionRow.SetActive(gm.Phase == DayPhase.Morning);

            invHint.text = gm.Selected != null
                ? $"<color={RedHex}>{gm.Selected.displayName}</color> 고름! 매대를 탭해서 진열"
                : "상품을 고른 뒤 매대를 탭하세요";

            foreach (var item in inv)
            {
                var p = item.product;
                int n = gm.Stock(p);
                bool unlocked = gm.IsUnlocked(p);
                bool visible = unlocked || n > 0;
                if (item.button.gameObject.activeSelf != visible) item.button.gameObject.SetActive(visible);
                if (!visible) continue;

                bool selected = gm.Selected == p;
                item.count.text = n > 0 ? $"{n}개" : "없음";
                item.count.color = selected ? UIKit.Red : n > 0 ? UIKit.Ink : UIKit.Muted;
                item.bg.color = selected ? UIKit.Yellow : n > 0 ? UIKit.Card : new Color(0.90f, 0.87f, 0.82f);
                item.group.alpha = n > 0 || selected ? 1f : 0.45f;
                if (item.ring.gameObject.activeSelf != selected) item.ring.gameObject.SetActive(selected);
                item.juice.restScale = selected ? 1.07f : n > 0 ? 1f : 0.94f;
                SetActive(item.trendTag, gm.Trend == p);
                SetActive(item.newTag, gm.IsNew(p));
            }
            SortInventory();

            foreach (var kv in wholesale)
            {
                var p = kv.Key;
                var row = kv.Value;
                bool unlocked = gm.IsUnlocked(p);
                var rowGo = row.info.transform.parent.gameObject;
                if (rowGo.activeSelf != unlocked) rowGo.SetActive(unlocked);
                if (!unlocked) continue;
                row.info.text = $"{p.boxSize}개 들이, 개당 {UIKit.Won(p.retailPrice)}에 판매  |  창고 {gm.Stock(p)}";
                row.buy.interactable = gm.Money >= gm.BoxCost(p);
                string cost = UIKit.Won(gm.BoxCost(p));
                if (row.buyLabel.text != cost) row.buyLabel.text = cost; // 도매상 단골 할인
                SetActive(row.trendTag, gm.Trend == p);
                SetActive(row.newTag, gm.IsNew(p));
            }
            int nextDay = gm.NextUnlockDay();
            wholesaleFooter.text = nextDay > 0 ? $"새 물건은 {nextDay}일차에 또 들어와요" : "도매상 물건을 모두 들여왔어요!";

            int visibleRows = 0;
            foreach (var kv in wholesale) if (gm.IsUnlocked(kv.Key)) visibleRows++;
            float h = WholesaleHeight(visibleRows);
            if (Mathf.Abs(wholesaleCard.sizeDelta.y - h) > 0.5f) wholesaleCard.sizeDelta = new Vector2(wholesaleCard.sizeDelta.x, h);
        }

        void RefreshTrend()
        {
            string hint = gm.TrendHint;
            string text = !string.IsNullOrEmpty(hint) ? "★ " + hint
                : gm.Trend != null ? $"★ 오늘의 유행: {gm.Trend.displayName}"
                : "★ 오늘은 유행이 잠잠해요";
            if (text == lastTrendText) return;
            lastTrendText = text;
            trendText.text = text;
            // 지금 유행(없으면 소문난) 상품 아이콘을 이름표 왼쪽에
            var sprite = ProductIcons.Get(gm.Trend != null ? gm.Trend : gm.TrendRumor);
            trendIcon.sprite = sprite;
            trendIcon.enabled = sprite != null;
            float iconPad = sprite != null ? 36f : 0f;
            trendText.rectTransform.offsetMin = new Vector2(14f + iconPad, trendText.rectTransform.offsetMin.y);
            float w = Mathf.Clamp(trendText.preferredWidth + 34f + iconPad, 200f, 1000f);
            trendTag.sizeDelta = new Vector2(w, trendTag.sizeDelta.y);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>재고 있는 상품을 앞으로, 없는 상품은 뒤로 (같으면 상품 목록 순서).</summary>
        void SortInventory()
        {
            invOrder.Clear();
            foreach (var item in inv) if (gm.Stock(item.product) > 0) invOrder.Add(item);
            foreach (var item in inv) if (gm.Stock(item.product) <= 0) invOrder.Add(item);
            for (int i = 0; i < invOrder.Count; i++)
                if (invOrder[i].button.transform.GetSiblingIndex() != i)
                    invOrder[i].button.transform.SetSiblingIndex(i);
        }

        void Update()
        {
            // 손님이 물건을 집어가는 건 Changed 이벤트가 없으므로 가격표만 가볍게 매 프레임 확인 (바뀔 때만 문자열 생성)
            foreach (var kv in slotLabels) UpdateSlotLabel(kv.Key, kv.Value);
            if (decorMode) UpdateDecorMarkers();
            if (moveMode && gm.Phase != DayPhase.Morning) ExitMoveMode();

            phaseText.text = gm.Phase switch
            {
                DayPhase.Morning => "아침\n물건 사고 진열하기",
                DayPhase.Open when gm.IsRush => "하교 러시!\n빨리 계산해요",
                DayPhase.Open when gm.OpenProgress < GameManager.RushStart => "오전 장사",
                DayPhase.Open => "방과 후",
                DayPhase.Closing => "마감 중\n남은 손님 계산",
                _ => "정산"
            };

            bool speedVisible = gm.SpeedActive;
            if (speedButton.gameObject.activeSelf != speedVisible) speedButton.gameObject.SetActive(speedVisible);
            if (speedVisible && shownSpeed != gm.Speed)
            {
                shownSpeed = gm.Speed;
                speedLabel.text = gm.Speed == 1 ? "1배속" : $"<color={RedHex}>{gm.Speed}배속</color>";
                var bg = (Image)speedButton.targetGraphic;
                bg.color = gm.Speed == 1 ? UIKit.Cream : UIKit.Yellow;
            }

            float progress = gm.OpenProgress;
            if (Mathf.Abs(progress - lastProgress) > 0.0015f)
            {
                lastProgress = progress;
                timeFill.anchorMax = new Vector2(progress, 1);
                timeKnob.anchorMin = timeKnob.anchorMax = new Vector2(progress, 0.5f);
            }

            UpdateMoney(Time.unscaledDeltaTime);

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
            }
            if (toastRedTimer > 0f)
            {
                toastRedTimer -= Time.unscaledDeltaTime;
                toastBg.color = Color.Lerp(ToastColor, UIKit.Red, Mathf.Clamp01(toastRedTimer / 0.25f));
            }
        }

        void UpdateMoney(float dt)
        {
            int target = gm.Money - pendingMoney;
            float diff = target - shownMoney;
            if (Mathf.Abs(diff) > 0.01f)
            {
                float step = Mathf.Max(Mathf.Abs(diff) * 9f, 400f) * dt;
                shownMoney = Mathf.MoveTowards(shownMoney, target, step);
            }
            int si = Mathf.RoundToInt(shownMoney);
            if (si == shownMoneyInt) return;
            shownMoneyInt = si;
            string s = UIKit.Won(si);
            moneyText.text = s;
            wholesaleMoney.text = s;
        }

        // ───────────── 튜토리얼·스크린샷용 (같은 어셈블리 내부) ─────────────

        internal Canvas RootCanvas => canvas;
        internal Camera ShopCamera => cam;
        internal bool WholesaleOpen => wholesaleModal.activeSelf;
        internal bool SettlementOpen => settlementModal.activeSelf;
        internal RectTransform WholesaleButtonRect => (RectTransform)wholesaleButton.transform;
        internal RectTransform OpenButtonRect => (RectTransform)openButton.transform;
        internal RectTransform CloseWholesaleRect => (RectTransform)closeWholesaleButton.transform;
        internal TutorialGuide Tutorial => tutorial;
        internal RectTransform OverlayLayer => overlaySafe;

        /// <summary>스크린샷용: 저장된 진행과 상관없이 튜토리얼을 띄움.</summary>
        internal TutorialGuide ForceTutorial()
        {
            if (tutorial == null) tutorial = TutorialGuide.Create(this, gm, overlaySafe, true);
            fxLayer.SetAsLastSibling();
            return tutorial;
        }
        internal HudFx Fx => fx;

        /// <summary>살 수 있는 첫 상품의 구매 버튼 (없으면 null).</summary>
        internal RectTransform FirstAffordableBuyButton()
        {
            foreach (var p in gm.Catalog.products)
                if (p != null && wholesale.TryGetValue(p, out var row) && gm.IsUnlocked(p) && gm.Money >= gm.BoxCost(p))
                    return (RectTransform)row.buy.transform;
            return null;
        }

        internal RectTransform InvCardFor(ProductData p)
            => p != null && invByProduct.TryGetValue(p, out var item) && item.button.gameObject.activeInHierarchy
                ? (RectTransform)item.button.transform : null;

        /// <summary>재고 있는 첫 창고 카드.</summary>
        internal RectTransform FirstStockedCard()
        {
            foreach (var item in invOrder)
                if (gm.Stock(item.product) > 0 && item.button.gameObject.activeInHierarchy)
                    return (RectTransform)item.button.transform;
            return null;
        }

        internal CustomerBubble FirstTappableBubble()
        {
            foreach (var b in bubbles)
                if (b != null && b.CanTap) return b;
            return null;
        }

        /// <summary>창고 카드가 보이도록 스크롤.</summary>
        internal void ScrollInventoryTo(RectTransform card)
        {
            if (card == null || invScroll == null) return;
            var viewport = invScroll.viewport;
            float contentW = invContent.rect.width, viewW = viewport.rect.width;
            if (contentW <= viewW) return;
            float x = -card.anchoredPosition.x + viewW * 0.5f;
            float min = viewW - contentW;
            x = Mathf.Clamp(x, min, 0f);
            invScroll.horizontalNormalizedPosition = Mathf.Clamp01(x / min);
        }

        /// <summary>에디트 모드 스크린샷용: 애니메이션 없이 지금 상태로 맞춤.</summary>
        internal void SnapForCapture()
        {
            Refresh();
            Update();
            shownMoney = gm.Money - pendingMoney;
            shownMoneyInt = int.MinValue;
            UpdateMoney(0f);
            foreach (var f in GetComponentsInChildren<WorldFollower>(true)) f.Snap();
            foreach (var j in GetComponentsInChildren<UIJuice>(true)) j.Snap();
            if (tutorial != null) tutorial.Tick(0.016f);
        }
    }
}
