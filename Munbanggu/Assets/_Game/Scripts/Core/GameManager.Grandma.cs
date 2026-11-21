using System.Collections.Generic;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.UI;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>할머니 스킬 하나. active면 영업 중에 버튼으로 쓰고(하루 uses번), 아니면 늘 켜져 있음.</summary>
    public sealed class GrandmaSkillDef
    {
        public readonly string id, name, desc, line;
        public readonly int cost, minDay, minReputation, uses;
        public readonly bool active;

        public GrandmaSkillDef(string id, string name, bool active, int uses, int cost, int minDay, int minReputation, string desc, string line)
        {
            this.id = id; this.name = name; this.active = active; this.uses = uses; this.cost = cost;
            this.minDay = minDay; this.minReputation = minReputation; this.desc = desc; this.line = line;
        }
    }

    public static class GrandmaSkills
    {
        public static readonly GrandmaSkillDef[] All =
        {
            // ── 늘 켜짐 ──
            new GrandmaSkillDef("sixthsense", "눈치 백단", false, 0, 5000, 4, 0, "몰래 나가는 아이가 30% 더 느려져요", "할머니 눈은 못 속인다!"),
            new GrandmaSkillDef("haggle", "흥정 9단", false, 0, 6000, 5, 0, "도매가 10% 더 깎아요", "아이고 사장님, 좀 깎아 줘~"),
            new GrandmaSkillDef("storyteller", "이야기 할머니", false, 0, 7000, 6, 0, "줄 선 손님 인내심 +25% (옛날얘기 들으며 기다려요)", "옛날 옛적에~"),
            new GrandmaSkillDef("warmheart", "정 많은 할머니", false, 0, 8000, 7, 55, "상담 딱 맞히면 평판 +1 더, 달래 준 아이가 친구를 2명 더 데려와요", "우리 강아지, 괜찮아~"),
            new GrandmaSkillDef("memory", "단골 기억력", false, 0, 10000, 9, 60, "단골 팁 ×2", "철수 왔구나! 딱지 또 사러 왔지?"),
            // ── 영업 중 버튼 ──
            new GrandmaSkillDef("shout", "할머니 호통!", true, 1, 4000, 3, 0, "몰래 나가는 아이를 전부 붙잡고, 줄 선 손님 인내심을 가득 채워요 (하루 1번)", "이 녀석들! 줄 똑바로 서!"),
            new GrandmaSkillDef("flash", "번개 계산", true, 1, 9000, 6, 0, "줄 선 손님을 전부 바로 계산해요 (하루 1번)", "자자, 빨리빨리~ 딸깍딸깍!"),
            new GrandmaSkillDef("megaphone", "동네 확성기", true, 1, 12000, 8, 60, "동네에 방송! 손님 6명이 바로 몰려와요 (하루 1번)", "아~ 아~ 문방구에 새 물건 들어왔어요~"),
            new GrandmaSkillDef("bonus", "덤 하나 더", true, 2, 15000, 10, 65, "다음 손님 5명이 하나씩 더 사 가요 (매대에 있으면, 하루 2번)", "하나 더 가져가, 할머니가 깎아 줄게!"),
        };

        public static GrandmaSkillDef Find(string id) => System.Array.Find(All, s => s.id == id);
    }

    /// <summary>할머니 스킬 (docs/09): 사기·쓰기·효과.</summary>
    public partial class GameManager
    {
        readonly Dictionary<string, int> skillUsesToday = new Dictionary<string, int>();
        int bonusBuysLeft;

        public bool HasGrandmaSkill(string id) => Town.grandmaSkills != null && Town.grandmaSkills.Contains(id);

        public string GrandmaSkillBlocker(GrandmaSkillDef s)
        {
            if (HasGrandmaSkill(s.id)) return "배웠어요";
            if (Day < s.minDay) return $"{s.minDay}일차부터 배울 수 있어요";
            if (Reputation < s.minReputation) return $"평판 {s.minReputation} 이상 필요 (지금 {Reputation})";
            return null;
        }

        public bool BuyGrandmaSkill(string id)
        {
            var s = GrandmaSkills.Find(id);
            if (s == null || !MorningOnly("할머니 스킬 배우기")) return false;
            string b = GrandmaSkillBlocker(s);
            if (b != null) { Fail(b); return false; }
            if (Money < s.cost) { Fail("돈이 부족해요"); return false; }
            Money -= s.cost;
            Stats.invested += s.cost;
            Town.grandmaSkills.Add(id);
            Dialogue.Show("할머니", $"「{s.name}」을 익혔어! {s.line}", UIKit.Accent, "할");
            Save();
            Notify();
            return true;
        }

        public int GrandmaUsesLeft(GrandmaSkillDef s)
        {
            if (!s.active || !HasGrandmaSkill(s.id)) return 0;
            skillUsesToday.TryGetValue(s.id, out int used);
            return Mathf.Max(0, s.uses - used);
        }

        /// <summary>영업 중에 쓸 수 있는 배운 스킬들.</summary>
        public List<GrandmaSkillDef> ActiveGrandmaSkills()
        {
            var list = new List<GrandmaSkillDef>();
            foreach (var s in GrandmaSkills.All) if (s.active && HasGrandmaSkill(s.id)) list.Add(s);
            return list;
        }

        public bool UseGrandmaSkill(string id)
        {
            var s = GrandmaSkills.Find(id);
            if (s == null || !s.active || !HasGrandmaSkill(id)) return false;
            if (Phase != DayPhase.Open && Phase != DayPhase.Closing) { Fail("영업 중에만 쓸 수 있어요"); return false; }
            if (GrandmaUsesLeft(s) <= 0) { Fail("오늘은 다 썼어요"); return false; }
            int done = 0;
            switch (id)
            {
                case "shout":
                    foreach (var c in customers.ToArray())
                    {
                        if (c == null) continue;
                        if (c.GrandmaCatch("할머니: 이 녀석!")) done++;
                        c.RefillPatience();
                    }
                    break;
                case "flash":
                    foreach (var c in new List<Customer>(Layout.Queue.Line))
                        if (c != null && c.InstantCheckout("할머니 번개 계산!")) done++;
                    break;
                case "megaphone":
                    ScheduleFriends(6);
                    done = 6;
                    break;
                case "bonus":
                    bonusBuysLeft += 5;
                    done = 5;
                    break;
            }
            skillUsesToday[id] = (skillUsesToday.TryGetValue(id, out int u) ? u : 0) + 1;
            Hud.Toast($"{s.name} {s.line}");
            GameEvents.RaiseRushStarted();
            Notify();
            return true;
        }

        void GrandmaMorning()
        {
            skillUsesToday.Clear();
            bonusBuysLeft = 0;
        }

        /// <summary>덤 하나 더: 계산할 때 같은 물건을 매대에서 하나 더 사 감 (그 값을 돌려줌).</summary>
        int GrandmaBonusSale(ProductData p)
        {
            if (bonusBuysLeft <= 0 || p == null) return 0;
            foreach (var s in Layout.Slots)
            {
                if (s == null || s.IsBoard || s.Product != p || s.Count <= 0) continue;
                if (!s.TakeOne()) continue;
                bonusBuysLeft--;
                RecordSold(p);
                return p.retailPrice;
            }
            return 0;
        }

        // ── 패시브 배율 ──
        float GrandmaBoxMultiplier => HasGrandmaSkill("haggle") ? 0.9f : 1f;
        float GrandmaSneakMultiplier => HasGrandmaSkill("sixthsense") ? 0.7f : 1f;
        float GrandmaPatienceMultiplier => HasGrandmaSkill("storyteller") ? 1.25f : 1f;
        int GrandmaCounselRepBonus => HasGrandmaSkill("warmheart") ? 1 : 0;
        int GrandmaExtraFriends => HasGrandmaSkill("warmheart") ? 2 : 0;
        int GrandmaRegularTipMultiplier => HasGrandmaSkill("memory") ? 2 : 1;

        /// <summary>달래 준 아이가 데려오는 친구 (정 많은 할머니면 +2).</summary>
        public void ScheduleComfortFriends(int n) => ScheduleFriends(n + GrandmaExtraFriends);

#if UNITY_EDITOR
        /// <summary>스크린샷 도구용: 할머니 스킬을 몇 개 배운 상태로.</summary>
        public void DebugGrandmaForCapture()
        {
            Money = Mathf.Max(Money, 30000);
            foreach (var id in new[] { "shout", "flash", "megaphone", "sixthsense", "haggle" })
                if (!Town.grandmaSkills.Contains(id)) Town.grandmaSkills.Add(id);
            Notify();
        }
#endif
    }
}
