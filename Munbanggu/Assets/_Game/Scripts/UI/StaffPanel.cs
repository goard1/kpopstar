using System;
using Munbanggu.Core;
using Munbanggu.Customers;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 직원 관리 창 (아침): 우리 직원(일 바꾸기·아이템 달기·교육·내보내기), 오늘의 지원자(고용), 직원 아이템(구매).
    /// 바뀔 때마다 목록을 통째로 다시 만든다 (항목이 적어서 충분히 가볍다).
    /// </summary>
    public sealed class StaffPanel
    {
        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        RectTransform content;
        Text money;

        StaffPanel(GameManager gm, GameObject modal, UIJuice juice)
        {
            this.gm = gm;
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        public static StaffPanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new StaffPanel(gm, modal, juice);
            p.BuildCard(card);
            return p;
        }

        public void Open()
        {
            if (modal.activeSelf) return;
            Rebuild();
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        void BuildCard(RectTransform card)
        {
            var title = UIKit.Label("Title", card, "직원", 60, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 80, 40, 44);
            var sub = UIKit.Label("Sub", card, "직원은 영업 중에 알아서 일하고, 일할수록 레벨업해요.", 27, UIKit.Muted);
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

            var viewport = UIKit.Rect("List", card);
            UIKit.Stretch(viewport, 22, 176, 22, 188);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            content = UIKit.Rect("Content", viewport);
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

            var close = UIKit.Button("Close", card, "다 정했어요!", 42, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)close.transform, 112, 44, 44);
            close.onClick.AddListener(Close);
        }

        /// <summary>돈·직원·지원자·아이템 목록을 다시 그린다.</summary>
        public void Rebuild()
        {
            money.text = UIKit.Won(gm.Money);
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }

            var staff = gm.Staff;
            Header($"우리 직원 ({staff.members.Count}/{staff.maxStaff}명) · 하루 월급 {UIKit.Won(gm.DailySalaryTotal())}");
            if (staff.members.Count == 0)
                Note("아직 직원이 없어요. 아래 지원자 중에서 뽑아 보세요!");
            foreach (var m in staff.members) MemberCard(m);

            Header("오늘의 지원자");
            bool full = staff.members.Count >= staff.maxStaff;
            var candidates = gm.StaffCandidates();
            if (candidates.Count == 0) Note("오늘은 지원자가 더 없어요. 내일 또 와요.");
            foreach (var c in candidates) CandidateCard(c, full);

            // 보따리 상인 (3일마다 와서 3가지만, 하나씩)
            var offer = gm.MerchantOfferToday();
            if (offer.Count > 0)
            {
                Header("보따리 상인이 왔어요! (오늘만, 하나씩)");
                foreach (var item in offer) MerchantCard(item);
            }
            else
            {
                Header("보따리 상인");
                int d = StaffRules.DaysToMerchant(gm.Day);
                Note($"직원 물건 파는 상인은 {StaffRules.MerchantEvery}일마다 와요. 다음 방문: {d}일 뒤");
            }

            // 가진 아이템
            if (staff.ownedItems.Count > 0)
            {
                var counts = new System.Collections.Generic.List<string>();
                foreach (var def in StaffRules.Items)
                {
                    int n = staff.OwnedCount(def.id);
                    if (n > 0) counts.Add(n > 1 ? $"{def.name} ×{n}" : def.name);
                }
                Header("가진 직원 아이템 (직원 카드의 「아이템 바꾸기」로 달아요)");
                Note(string.Join(" · ", counts));
            }
        }

        // ───── 카드들 ─────

        void MemberCard(StaffMember m)
        {
            bool perk = m.perkChoices > 0;
            float h = 400 + (perk ? 120 : 0);
            var card = Card(m.name, h);
            var apron = UIKit.Panel("Apron", card.transform, StaffAgent.ApronColor(m.job), false);
            UIKit.Round(apron, 14);
            UIKit.Place(apron.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -24), new Vector2(28, 28));

            Text(card, $"{m.name}  Lv{m.level}", 36, UIKit.Ink, 66, 18, 300, 70, TextAnchor.UpperLeft);
            Text(card, $"{StaffRules.JobName(m.job)} · 월급 {UIKit.Won(StaffRules.DailySalary(m))}/일", 26, UIKit.Muted, 300, 22, 26, 70, TextAnchor.UpperRight);

            // 능력치 2×2 (포인트가 있으면 + 버튼)
            var member = m;
            StaffStat[] stats = { StaffStat.Speed, StaffStat.Walk, StaffStat.Kindness, StaffStat.Sharp };
            for (int i = 0; i < 4; i++)
            {
                var s = stats[i];
                int b = StaffRules.Base(m, s), e = StaffRules.Effective(m, s);
                int col = i % 2, row = i / 2;
                float left = 26 + col * 0.5f, top = 66 + row * 52;
                var t = UIKit.Label("Stat", card.transform, $"{StaffRules.StatName(s)} {Bar(b)} {(e != b ? $"{e}(+{e - b})" : e.ToString())}", 25, UIKit.Ink, TextAnchor.MiddleLeft);
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(col * 0.5f, 1); rt.anchorMax = new Vector2(col * 0.5f + 0.5f, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(26, -top - 46); rt.offsetMax = new Vector2(m.points > 0 ? -70 : -10, -top);
                if (m.points > 0)
                {
                    var plus = UIKit.Button("Plus", card.transform, "+", 30, UIKit.Green, Color.white, out _);
                    var prt = (RectTransform)plus.transform;
                    prt.anchorMin = prt.anchorMax = new Vector2(col * 0.5f + 0.5f, 1);
                    prt.pivot = new Vector2(1, 1);
                    prt.sizeDelta = new Vector2(56, 44);
                    prt.anchoredPosition = new Vector2(-14, -top - 1);
                    plus.interactable = b < StaffRules.StatCap;
                    plus.onClick.AddListener(() => { gm.AllocateStaffStat(member, s); Rebuild(); });
                }
            }

            string exp = m.level >= StaffRules.MaxLevel ? "최고 레벨!" : $"경험치 {m.exp}/{StaffRules.ExpToNext(m.level)}";
            var item = StaffRules.FindItem(m.item);
            string pts = m.points > 0 ? $"  ·  <color=#2e9e5b>포인트 {m.points} (+로 올려요)</color>" : "";
            var line = Text(card, $"{exp}  ·  아이템: {(item != null ? item.name : "없음")}{pts}", 23, UIKit.Muted, 26, 172, 26, 34, TextAnchor.UpperLeft);
            line.supportRichText = true;
            string perkText = m.perks != null && m.perks.Count > 0
                ? "특기: " + string.Join(", ", m.perks.ConvertAll(id => StaffRules.FindPerk(id)?.name ?? id))
                : "특기: 없음 (Lv3·6·9에 하나씩 골라요)";
            Text(card, $"{perkText}  ·  걷는 속도 {StaffRules.WalkSpeed(m):0.0}m/s", 23, UIKit.Muted, 26, 208, 26, 34, TextAnchor.UpperLeft);

            if (perk)
            {
                var offer = StaffRules.PerkOffer(m);
                Text(card, "★ 특기를 하나 골라요!", 25, UIKit.Red, 26, 248, 26, 30, TextAnchor.UpperLeft);
                for (int i = 0; i < offer.Count; i++)
                {
                    var p = offer[i];
                    var btn = UIKit.Button("Perk", card.transform, $"{p.name}\n<size=20>{p.desc}</size>", 26, UIKit.Accent, Color.white, out var lbl);
                    lbl.supportRichText = true;
                    var brt = (RectTransform)btn.transform;
                    brt.anchorMin = new Vector2(i * 0.5f, 1); brt.anchorMax = new Vector2(i * 0.5f + 0.5f, 1);
                    brt.pivot = new Vector2(0.5f, 1);
                    brt.offsetMin = new Vector2(i == 0 ? 20 : 6, -366); brt.offsetMax = new Vector2(i == 1 ? -20 : -6, -282);
                    string pid = p.id;
                    btn.onClick.AddListener(() => { gm.ChooseStaffPerk(member, pid); Rebuild(); });
                }
            }

            // 버튼 줄: 일 바꾸기 / 아이템 / 교육 / 내보내기
            RowButton(card, 0, 4, $"일: {StaffRules.JobName(m.job)}", UIKit.Blue, Color.white, true, () => { gm.CycleStaffJob(member); Rebuild(); });
            RowButton(card, 1, 4, "아이템 바꾸기", UIKit.Card, UIKit.Ink, gm.Staff.ownedItems.Count > 0, () => { gm.CycleStaffItem(member); Rebuild(); });
            bool maxed = m.level >= StaffRules.MaxLevel;
            int cost = StaffRules.TrainCost(m);
            RowButton(card, 2, 4, maxed ? "교육 끝" : $"교육 {UIKit.Won(cost)}", UIKit.Green, Color.white,
                !maxed && gm.Money >= cost, () => { gm.TrainStaff(member); Rebuild(); });
            RowButton(card, 3, 4, "내보내기", UIKit.Muted, Color.white, true, () => { gm.FireStaff(member); Rebuild(); });
        }

        static string Bar(int v)
        {
            int n = Mathf.Clamp(v, 0, 10);
            return new string('●', n / 2) + (n % 2 == 1 ? "◐" : "") + new string('○', (10 - n) / 2);
        }

        void CandidateCard(StaffMember c, bool full)
        {
            var card = Card("Candidate " + c.name, 176);
            var apron = UIKit.Panel("Apron", card.transform, StaffAgent.ApronColor(c.job), false);
            UIKit.Round(apron, 14);
            UIKit.Place(apron.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -24), new Vector2(28, 28));
            Text(card, $"{c.name}  ({StaffRules.JobName(c.job)} 지원)", 34, UIKit.Ink, 66, 18, 260, 70, TextAnchor.UpperLeft);
            Text(card, StatLine(c), 24, UIKit.Ink, 26, 64, 260, 34, TextAnchor.UpperLeft);
            Text(card, $"월급 {UIKit.Won(StaffRules.DailySalary(c))}/일 · {StaffRules.JobDesc(c.job)}", 21, UIKit.Muted, 26, 104, 260, 20, TextAnchor.UpperLeft);
            var cand = c;
            SideButton(card, full ? "자리 없음" : "고용", UIKit.Green, !full, () => { gm.HireStaff(cand); Rebuild(); });
        }

        static readonly Color[] RarityColors = { new Color(0.55f, 0.55f, 0.6f), new Color(0.35f, 0.55f, 0.95f), new Color(0.95f, 0.6f, 0.15f) };

        void MerchantCard(StaffItemDef item)
        {
            var card = Card("Item " + item.id, 124);
            var tag = UIKit.CardPanel("Rarity", card.transform, RarityColors[item.rarity], 12, false, 3);
            UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(76, 36));
            var tl = UIKit.Label("Text", tag.transform, item.RarityName, 22, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(tl.rectTransform);
            Text(card, item.name, 32, UIKit.Ink, 114, 14, 260, 48, TextAnchor.UpperLeft);
            Text(card, $"{item.desc}  ·  가진 것 {gm.Staff.OwnedCount(item.id)}개", 22, UIKit.Muted, 26, 66, 260, 40, TextAnchor.UpperLeft);
            bool sold = gm.MerchantSold(item.id);
            var id = item.id;
            SideButton(card, sold ? "산 물건" : UIKit.Won(item.price), sold ? UIKit.Muted : UIKit.Green,
                !sold && gm.Money >= item.price, () => { gm.BuyMerchantItem(id); Rebuild(); });
        }

        static string StatLine(StaffMember m) =>
            $"손 {StaffRules.Speed(m)}  걸음 {StaffRules.Walk(m)}  친절 {StaffRules.Kindness(m)}  눈썰미 {StaffRules.Sharp(m)}";

        // ───── 작은 도우미 ─────

        Image Card(string name, float height)
        {
            var card = UIKit.CardPanel(name, content, UIKit.Card, 24, true, 6);
            UIKit.Layout(card, -1, height);
            return card;
        }

        void Header(string text)
        {
            var h = UIKit.Label("Header", content, text, 30, UIKit.Accent, TextAnchor.LowerLeft);
            UIKit.Layout(h, -1, 56);
        }

        void Note(string text)
        {
            var n = UIKit.Label("Note", content, text, 26, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Layout(n, -1, 70);
        }

        static Text Text(Image card, string text, int size, Color color, float left, float top, float right, float height, TextAnchor anchor)
        {
            var t = UIKit.Label("Text", card.transform, text, size, color, anchor);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return t;
        }

        /// <summary>카드 아래쪽 버튼 줄 (count칸 중 index번째).</summary>
        static void RowButton(Image card, int index, int count, string label, Color bg, Color fg, bool interactable, Action onClick)
        {
            var b = UIKit.Button("Btn" + index, card.transform, label, 26, bg, fg, out _);
            var rt = (RectTransform)b.transform;
            float w = 1f / count;
            rt.anchorMin = new Vector2(index * w, 0);
            rt.anchorMax = new Vector2((index + 1) * w, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(index == 0 ? 20 : 6, 18);
            rt.offsetMax = new Vector2(index == count - 1 ? -20 : -6, 92);
            b.interactable = interactable;
            b.onClick.AddListener(() => onClick());
        }

        /// <summary>카드 오른쪽 세로 꽉 찬 버튼 (고용·구매).</summary>
        static void SideButton(Image card, string label, Color bg, bool interactable, Action onClick)
        {
            var b = UIKit.Button("Side", card.transform, label, 30, bg, Color.white, out _);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-226, 20);
            rt.offsetMax = new Vector2(-16, -20);
            b.interactable = interactable;
            b.onClick.AddListener(() => onClick());
        }
    }
}
