using System.Collections.Generic;
using Munbanggu.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 가게 꾸미기 (업그레이드 상점). HUD.Modal 카드 위에 분야별 목록을 만든다.
    /// 각 줄: 이름 + 단계 점(●○) + 지금/다음 효과 + 구매 버튼(다음 단계 값 / 최고 단계).
    /// </summary>
    public sealed class UpgradePanel
    {
        sealed class Row
        {
            public UpgradeDef def;
            public Text level, effect, buyLabel;
            public Button buy;
            public Image buyBg;
        }

        static readonly Dictionary<UpgradeCategory, string> CategoryNames = new Dictionary<UpgradeCategory, string>
        {
            { UpgradeCategory.Shop, "매대·가게 확장" },
            { UpgradeCategory.Customers, "손님 끌기" },
            { UpgradeCategory.Helpers, "일손 덜기" },
            { UpgradeCategory.Money, "도매·수익" },
        };

        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        readonly List<Row> rows = new List<Row>();
        Text money;

        UpgradePanel(GameManager gm, GameObject modal, UIJuice juice)
        {
            this.gm = gm;
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        System.Action onDecorate;
        Text comboText;
        Text expTitle, expBody, expBuyLabel;
        Button expBuy;
        Image expBuyBg;

        public static UpgradePanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice, System.Action onDecorate = null)
        {
            var p = new UpgradePanel(gm, modal, juice) { onDecorate = onDecorate };
            p.BuildCard(card);
            return p;
        }

        public void Open()
        {
            if (modal.activeSelf) return;
            Refresh();
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        void BuildCard(RectTransform card)
        {
            var title = UIKit.Label("Title", card, "가게 꾸미기", 60, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 80, 40, 44);
            var sub = UIKit.Label("Sub", card, "사면 바로 가게에 반영돼요. 번 돈으로 문방구를 키워요!", 27, UIKit.Muted);
            UIKit.Top(sub.rectTransform, 44, 122, 44);

            var pill = UIKit.CardPanel("Money", card, UIKit.Yellow, 34, false, 5);
            UIKit.Place(pill.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, -80), new Vector2(340, 70));
            var coin = UIKit.Coin(pill.transform, 46);
            UIKit.Place(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38, 0), new Vector2(46, 46));
            money = UIKit.Label("Text", pill.transform, "", 38, UIKit.Ink, TextAnchor.MiddleRight);
            UIKit.Stretch(money.rectTransform, 64, 2, 22, 0);
            money.horizontalOverflow = HorizontalWrapMode.Overflow;

            var divider = UIKit.DashLine(card, UIKit.WithAlpha(UIKit.KraftDark, 0.6f), 4);
            UIKit.Top(divider.rectTransform, 4, 178, 40);

            // 스크롤 목록
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

            var scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;

            // 가게 증축 (단계별: 옆칸 → 평상 → 2층 만화방)
            {
                var header = UIKit.Label("Header", content, "가게 증축", 32, UIKit.Accent, TextAnchor.LowerLeft);
                UIKit.Layout(header, -1, 56);
                var row = UIKit.CardPanel("Expansion", content, UIKit.Card, 24, true, 6);
                UIKit.Layout(row, -1, 300);
                expTitle = UIKit.Label("Name", row.transform, "", 34, UIKit.Ink, TextAnchor.UpperLeft);
                UIKit.Stretch(expTitle.rectTransform, 28, 230, 250, 16);
                expTitle.supportRichText = true;
                expBody = UIKit.Label("Body", row.transform, "", 24, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Stretch(expBody.rectTransform, 28, 16, 250, 72);
                expBody.supportRichText = true;
                expBuy = UIKit.Button("Build", row.transform, "", 30, UIKit.Red, Color.white, out expBuyLabel);
                var ert = (RectTransform)expBuy.transform;
                ert.anchorMin = new Vector2(1, 0); ert.anchorMax = new Vector2(1, 1);
                ert.pivot = new Vector2(1, 0.5f);
                ert.offsetMin = new Vector2(-226, 70); ert.offsetMax = new Vector2(-16, -70);
                expBuyBg = (Image)expBuy.targetGraphic;
                expBuy.onClick.AddListener(() => { if (gm.BuyExpansion()) Refresh(); });
            }

            // 진열대 장식 & 콤보 (배치 모드로 들어가기)
            {
                var header = UIKit.Label("Header", content, "진열대 장식 & 콤보", 32, UIKit.Accent, TextAnchor.LowerLeft);
                UIKit.Layout(header, -1, 56);
                var row = UIKit.CardPanel("Decor", content, UIKit.Card, 24, true, 6);
                UIKit.Layout(row, -1, 170);
                comboText = UIKit.Label("Combos", row.transform, "", 24, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Stretch(comboText.rectTransform, 28, 16, 250, 18);
                var go = UIKit.Button("Go", row.transform, "장식하기", 32, UIKit.Blue, Color.white, out _);
                var grt = (RectTransform)go.transform;
                grt.anchorMin = new Vector2(1, 0); grt.anchorMax = new Vector2(1, 1);
                grt.pivot = new Vector2(1, 0.5f);
                grt.offsetMin = new Vector2(-226, 26); grt.offsetMax = new Vector2(-16, -20);
                go.onClick.AddListener(() => { Close(); onDecorate?.Invoke(); });
            }

            UpgradeCategory? lastCat = null;
            foreach (var def in UpgradeTable.All)
            {
                if (lastCat != def.category)
                {
                    lastCat = def.category;
                    var header = UIKit.Label("Header", content, CategoryNames[def.category], 32, UIKit.Accent, TextAnchor.LowerLeft);
                    UIKit.Layout(header, -1, 56);
                }
                rows.Add(BuildRow(def, content));
            }

            var close = UIKit.Button("Close", card, "다 꾸몄어요!", 42, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)close.transform, 112, 44, 44);
            close.onClick.AddListener(Close);
        }

        Row BuildRow(UpgradeDef def, Transform list)
        {
            var row = UIKit.CardPanel(def.id, list, UIKit.Card, 24, true, 6);
            UIKit.Layout(row, -1, 150);

            var name = UIKit.Label("Name", row.transform, def.name, 36, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(name.rectTransform, 28, 0, 250, 18);
            var level = UIKit.Label("Level", row.transform, "", 30, UIKit.Red, TextAnchor.UpperRight);
            UIKit.Stretch(level.rectTransform, 28, 0, 258, 20);
            var effect = UIKit.Label("Effect", row.transform, "", 24, UIKit.Muted, TextAnchor.LowerLeft);
            UIKit.Stretch(effect.rectTransform, 28, 18, 250, 62);

            var buy = UIKit.Button("Buy", row.transform, "", 32, UIKit.Green, Color.white, out var buyLabel);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = new Vector2(1, 0); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(1, 0.5f);
            brt.offsetMin = new Vector2(-226, 26); brt.offsetMax = new Vector2(-16, -20);
            var id = def.id;
            buy.onClick.AddListener(() => { if (gm.BuyUpgrade(id)) Refresh(); });

            return new Row { def = def, level = level, effect = effect, buy = buy, buyLabel = buyLabel, buyBg = (Image)buy.targetGraphic };
        }

        void RefreshExpansion()
        {
            int lv = gm.Expansion;
            var next = ExpansionTable.Next(lv);
            string dots = new string('●', lv) + new string('○', ExpansionTable.Max - lv);
            string red = UIKit.Hex(UIKit.Red);
            if (next == null)
            {
                expTitle.text = $"{ExpansionTable.Name(lv)}  <color={red}>{dots}</color>";
                expBody.text = "가게를 끝까지 넓혔어요!\n다음엔 다른 동네로 가게를 옮길 수 있게 될 거예요.";
                expBuyLabel.text = "다 지음";
                expBuy.interactable = false;
                expBuyBg.color = UIKit.Muted;
                return;
            }
            expTitle.text = $"다음: {next.name}  <color={red}>{dots}</color>";
            string blocker = gm.ExpansionBlocker();
            string cond = $"조건: {next.minDay}일차~" + (next.minReputation > 0 ? $" · 평판 {next.minReputation}+" : "");
            expBody.text = "· " + string.Join("\n· ", next.effects) + "\n"
                + (blocker != null ? $"<color={red}>{blocker}</color>" : cond);
            expBuyLabel.text = blocker != null ? "아직 못 지어요" : UIKit.Won(next.cost);
            expBuy.interactable = blocker == null && gm.Money >= next.cost && gm.Phase == DayPhase.Morning;
            expBuyBg.color = blocker != null ? UIKit.Muted : UIKit.Red;
        }

        /// <summary>돈·단계·버튼 상태 갱신 (열 때, 살 때, 돈이 바뀔 때).</summary>
        public void Refresh()
        {
            money.text = UIKit.Won(gm.Money);
            if (comboText != null)
            {
                var found = gm.DiscoveredComboNames();
                comboText.text = "진열대 위에 장식을 놓아 \"○○ 코너\"를, 옆 진열대에 짝 상품을 놓아 \"세트\"를 만들어요.\n"
                    + $"발견한 콤보 {found.Count}/{GameManager.TotalComboCount}"
                    + (found.Count > 0 ? ": " + string.Join(", ", found) : "");
            }
            if (expTitle != null) RefreshExpansion();
            var up = gm.Upgrades;
            foreach (var r in rows)
            {
                int lv = up.Level(r.def.id);
                bool maxed = up.IsMaxed(r.def);
                r.level.text = new string('●', lv) + new string('○', r.def.MaxLevel - lv);
                string now = lv > 0 ? $"지금: {r.def.effects[lv - 1]}" : r.def.blurb;
                r.effect.text = maxed ? $"{now}  (최고 단계)" : $"{now}\n다음: {r.def.effects[lv]}";
                int cost = up.NextCost(r.def);
                r.buyLabel.text = maxed ? "최고 단계" : UIKit.Won(cost);
                r.buy.interactable = !maxed && gm.Money >= cost && gm.Phase == DayPhase.Morning;
                r.buyBg.color = maxed ? UIKit.Muted : UIKit.Green;
            }
        }
    }
}
