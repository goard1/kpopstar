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

            Header("직원 아이템 (한 번 사면 계속, 한 명에게 하나씩)");
            foreach (var item in StaffRules.Items) ItemCard(item);
        }

        // ───── 카드들 ─────

        void MemberCard(StaffMember m)
        {
            var card = Card(m.name, 268);
            var apron = UIKit.Panel("Apron", card.transform, StaffAgent.ApronColor(m.job), false);
            UIKit.Round(apron, 14);
            UIKit.Place(apron.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -24), new Vector2(28, 28));

            Text(card, $"{m.name}  Lv{m.level}", 36, UIKit.Ink, 66, 18, 300, 70, TextAnchor.UpperLeft);
            Text(card, $"{StaffRules.JobName(m.job)} · 월급 {UIKit.Won(StaffRules.DailySalary(m))}/일", 26, UIKit.Muted, 300, 22, 26, 70, TextAnchor.UpperRight);
            Text(card, StatLine(m), 27, UIKit.Ink, 26, 66, 26, 34, TextAnchor.UpperLeft);
            string exp = m.level >= StaffRules.MaxLevel ? "최고 레벨!" : $"경험치 {m.exp}/{StaffRules.ExpToNext(m.level)}";
            var item = StaffRules.FindItem(m.item);
            Text(card, $"{exp}  ·  아이템: {(item != null ? item.name : "없음")}", 24, UIKit.Muted, 26, 106, 26, 34, TextAnchor.UpperLeft);

            // 버튼 줄: 일 바꾸기 / 아이템 / 교육 / 내보내기
            var member = m;
            RowButton(card, 0, 4, $"일: {StaffRules.JobName(m.job)}", UIKit.Blue, Color.white, true, () => { gm.CycleStaffJob(member); Rebuild(); });
            RowButton(card, 1, 4, "아이템 바꾸기", UIKit.Card, UIKit.Ink, gm.Staff.ownedItems.Count > 0, () => { gm.CycleStaffItem(member); Rebuild(); });
            bool maxed = m.level >= StaffRules.MaxLevel;
            int cost = StaffRules.TrainCost(m);
            RowButton(card, 2, 4, maxed ? "교육 끝" : $"교육 {UIKit.Won(cost)}", UIKit.Green, Color.white,
                !maxed && gm.Money >= cost, () => { gm.TrainStaff(member); Rebuild(); });
            RowButton(card, 3, 4, "내보내기", UIKit.Muted, Color.white, true, () => { gm.FireStaff(member); Rebuild(); });
        }

        void CandidateCard(StaffMember c, bool full)
        {
            var card = Card("Candidate " + c.name, 176);
            var apron = UIKit.Panel("Apron", card.transform, StaffAgent.ApronColor(c.job), false);
            UIKit.Round(apron, 14);
            UIKit.Place(apron.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -24), new Vector2(28, 28));
            Text(card, $"{c.name}  ({StaffRules.JobName(c.job)} 지원)", 34, UIKit.Ink, 66, 18, 260, 70, TextAnchor.UpperLeft);
            Text(card, StatLine(c), 26, UIKit.Ink, 26, 64, 260, 34, TextAnchor.UpperLeft);
            Text(card, $"월급 {UIKit.Won(StaffRules.DailySalary(c))}/일 · {StaffRules.JobDesc(c.job)}", 21, UIKit.Muted, 26, 104, 260, 20, TextAnchor.UpperLeft);
            var cand = c;
            SideButton(card, full ? "자리 없음" : "고용", UIKit.Green, !full, () => { gm.HireStaff(cand); Rebuild(); });
        }

        void ItemCard(StaffItemDef item)
        {
            var card = Card("Item " + item.id, 118);
            Text(card, item.name, 34, UIKit.Ink, 26, 14, 260, 54, TextAnchor.UpperLeft);
            Text(card, item.desc, 24, UIKit.Muted, 26, 62, 260, 16, TextAnchor.UpperLeft);
            bool owned = gm.Staff.ownedItems.Contains(item.id);
            var id = item.id;
            SideButton(card, owned ? "있음" : UIKit.Won(item.price), owned ? UIKit.Muted : UIKit.Green,
                !owned && gm.Money >= item.price, () => { gm.BuyStaffItem(id); Rebuild(); });
        }

        static string StatLine(StaffMember m)
        {
            string S(int baseV, int eff) => eff != baseV ? $"{eff}(+{eff - baseV})" : eff.ToString();
            return $"손 빠르기 {S(m.speed, StaffRules.Speed(m))}   친절 {S(m.kindness, StaffRules.Kindness(m))}   눈썰미 {S(m.sharp, StaffRules.Sharp(m))}";
        }

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
