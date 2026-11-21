using System;
using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>직원이 맡는 일 (docs/06 1장).</summary>
    public enum StaffJob { Cashier, Stocker, Guard, Counselor }

    /// <summary>능력치 종류 (레벨업 포인트를 나눠 줄 때).</summary>
    public enum StaffStat { Speed, Walk, Kindness, Sharp }

    /// <summary>직원 한 명. 능력치는 1~10 (아이템·특기 보너스는 Effective로 더함).</summary>
    [Serializable]
    public class StaffMember
    {
        public string name;
        public int seed;          // 외형 (CustomerVisual)
        public int level = 1;
        public int exp;
        public int speed;         // 손 빠르기 (계산·진열 간격)
        public int walk;          // 걸음 (가게 안을 걷는 속도) — 처음엔 느림
        public int kindness;      // 친절
        public int sharp;         // 눈썰미
        public StaffJob job;
        public string item = "";  // 장착 아이템 id ("" = 없음)
        public int points;        // 아직 안 나눠 준 능력 포인트
        public int perkChoices;   // 아직 안 고른 특기 수 (Lv3·6·9)
        public List<string> perks = new List<string>();
    }

    /// <summary>직원 아이템 (보따리 상인이 팖). 등급: 0 보통, 1 희귀, 2 전설.</summary>
    public sealed class StaffItemDef
    {
        public readonly string id, name, desc;
        public readonly int price, speed, walk, kindness, sharp, batch, rarity;

        public StaffItemDef(string id, string name, int price, int rarity, int speed, int walk, int kindness, int sharp, int batch, string desc)
        {
            this.id = id; this.name = name; this.price = price; this.rarity = rarity;
            this.speed = speed; this.walk = walk; this.kindness = kindness; this.sharp = sharp; this.batch = batch; this.desc = desc;
        }

        public string RarityName => rarity == 2 ? "전설" : rarity == 1 ? "희귀" : "보통";
    }

    /// <summary>특기: Lv3·6·9에 두 개 중 하나를 고른다.</summary>
    public sealed class StaffPerkDef
    {
        public readonly string id, name, desc;
        public StaffPerkDef(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
    }

    /// <summary>직원 규칙·수치 (밸런스는 여기서).</summary>
    public static class StaffRules
    {
        public const int MaxLevel = 10;
        public const int StartMaxStaff = 2;
        public const int StatCap = 10;

        public static readonly StaffItemDef[] Items =
        {
            //                   id           이름           값   등급 손 걸음 친절 눈 진열
            new StaffItemDef("calculator", "전자계산기",    2500, 0, 2, 0, 0, 0, 0, "손 빠르기 +2"),
            new StaffItemDef("sneakers",   "운동화",        2000, 0, 0, 2, 0, 0, 0, "걸음 +2"),
            new StaffItemDef("nametag",    "예쁜 명찰",     1800, 0, 0, 0, 2, 0, 0, "친절 +2"),
            new StaffItemDef("magnifier",  "돋보기",        2200, 0, 0, 0, 0, 2, 0, "눈썰미 +2"),
            new StaffItemDef("whistle",    "호루라기",      1500, 0, 0, 1, 0, 1, 0, "걸음 +1, 눈썰미 +1"),
            new StaffItemDef("apron",      "문방구 앞치마", 3500, 1, 1, 1, 1, 1, 0, "모든 능력 +1"),
            new StaffItemDef("roller",     "롤러스케이트",  5500, 1, 0, 4, 0, 0, 0, "걸음 +4 (쌩쌩!)"),
            new StaffItemDef("cart",       "꼬마 손수레",   5000, 1, 0, 1, 0, 0, 3, "한 번에 3개 더 채움, 걸음 +1"),
            new StaffItemDef("candybag",   "주머니 사탕",   4000, 1, 0, 0, 3, 0, 0, "친절 +3 (아이들한테 하나씩)"),
            new StaffItemDef("radio",      "무전기",        4500, 1, 0, 1, 0, 2, 0, "눈썰미 +2, 걸음 +1"),
            new StaffItemDef("goldpen",    "금빛 만년필",   9000, 2, 3, 0, 1, 0, 0, "손 빠르기 +3, 친절 +1"),
            new StaffItemDef("cape",       "히어로 망토",  12000, 2, 2, 2, 2, 2, 0, "모든 능력 +2"),
        };

        public static readonly StaffPerkDef[] Perks =
        {
            new StaffPerkDef("quickhands", "손이 척척", "손 빠르기 +2"),
            new StaffPerkDef("shortcut", "지름길 박사", "걸음 +2"),
            new StaffPerkDef("smile", "함박웃음", "친절 +2, 팁 확률 +5%"),
            new StaffPerkDef("eagle", "매의 눈", "눈썰미 +2, 잡을 확률 +10%"),
            new StaffPerkDef("bigarms", "두 팔 가득", "진열할 때 한 번에 3개 더"),
            new StaffPerkDef("mental", "암산왕", "계산이 25% 빨라짐"),
            new StaffPerkDef("listener", "잘 들어줌", "상담 손님 인내심 +20%"),
            new StaffPerkDef("reader", "마음 읽기", "★추천 정확도 +15%"),
        };

        static readonly string[][] PerkPools =
        {
            new[] { "mental", "smile", "quickhands", "shortcut", "bigarms" }, // 계산·진열
            new[] { "bigarms", "shortcut", "quickhands", "mental", "smile" }, // 진열·계산
            new[] { "eagle", "shortcut", "smile", "quickhands" },             // 경비
            new[] { "reader", "listener", "smile", "eagle" },                 // 상담 보조
        };

        public static StaffItemDef FindItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var i in Items) if (i.id == id) return i;
            return null;
        }

        public static StaffPerkDef FindPerk(string id)
        {
            foreach (var p in Perks) if (p.id == id) return p;
            return null;
        }

        public static bool Has(StaffMember m, string perk) => m.perks != null && m.perks.Contains(perk);

        public static string JobName(StaffJob j) => j switch
        {
            StaffJob.Cashier => "계산·진열",
            StaffJob.Stocker => "진열·계산",
            StaffJob.Guard => "경비",
            _ => "상담 보조",
        };

        public static string JobDesc(StaffJob j) => j switch
        {
            StaffJob.Cashier => "돌아다니며 계산·진열, 계산 먼저 (손 빠르기·걸음·친절)",
            StaffJob.Stocker => "돌아다니며 진열·계산, 진열 먼저 (걸음·손 빠르기)",
            StaffJob.Guard => "몰래 나가는 아이를 문 앞에서 잡음 (눈썰미)",
            _ => "고르기 창에 ★추천을 붙여 줌 (눈썰미), 상담 손님 인내심↑ (친절)",
        };

        public static string StatName(StaffStat s) => s switch
        {
            StaffStat.Speed => "손 빠르기",
            StaffStat.Walk => "걸음",
            StaffStat.Kindness => "친절",
            _ => "눈썰미",
        };

        public static int Base(StaffMember m, StaffStat s) => s switch
        {
            StaffStat.Speed => m.speed,
            StaffStat.Walk => Mathf.Max(1, m.walk),
            StaffStat.Kindness => m.kindness,
            _ => m.sharp,
        };

        public static int Effective(StaffMember m, StaffStat s) => s switch
        {
            StaffStat.Speed => Speed(m),
            StaffStat.Walk => Walk(m),
            StaffStat.Kindness => Kindness(m),
            _ => Sharp(m),
        };

        public static int Speed(StaffMember m) => Mathf.Clamp(m.speed + (FindItem(m.item)?.speed ?? 0) + (Has(m, "quickhands") ? 2 : 0), 1, 16);
        /// <summary>가게 전체 걸음 보너스 (만화 「로봇 대장 쾅」 완결 등).</summary>
        public static int GlobalWalkBonus;
        public static int Walk(StaffMember m) => Mathf.Clamp(Mathf.Max(1, m.walk) + (FindItem(m.item)?.walk ?? 0) + (Has(m, "shortcut") ? 2 : 0) + GlobalWalkBonus, 1, 17);
        public static int Kindness(StaffMember m) => Mathf.Clamp(m.kindness + (FindItem(m.item)?.kindness ?? 0) + (Has(m, "smile") ? 2 : 0), 1, 16);
        public static int Sharp(StaffMember m) => Mathf.Clamp(m.sharp + (FindItem(m.item)?.sharp ?? 0) + (Has(m, "eagle") ? 2 : 0), 1, 16);

        public static int ExpToNext(int level) => 8 + level * 6;
        public static int DailySalary(StaffMember m) => 300 + m.level * 120;
        public static int TrainCost(StaffMember m) => 800 + m.level * 500;

        /// <summary>가게 안 걷는 속도(m/s): 걸음 1 → 1.0 (꽤 느림), 10 → 2.5, 16 → 3.4.</summary>
        public static float WalkSpeed(StaffMember m) => 0.85f + 0.16f * Walk(m);
        /// <summary>계산 간격(초): 빠르기 1 → 7초, 10 → 3초. 암산왕 -25%.</summary>
        public static float CashierInterval(StaffMember m) => Mathf.Max(1.8f, 7.4f - 0.44f * Speed(m)) * (Has(m, "mental") ? 0.75f : 1f);
        /// <summary>진열 간격(초).</summary>
        public static float StockerInterval(StaffMember m) => Mathf.Max(2.5f, 9f - 0.6f * Speed(m));
        /// <summary>한 번에 채우는 개수.</summary>
        public static int StockerBatch(StaffMember m) => 2 + m.level / 2 + (FindItem(m.item)?.batch ?? 0) + (Has(m, "bigarms") ? 3 : 0);
        /// <summary>매대 앞에서 물건을 채우는 데 걸리는 시간(초).</summary>
        public static float RestockTime(StaffMember m) => Mathf.Max(0.3f, 2.2f - 0.18f * Speed(m));
        /// <summary>계산할 때 손님이 팁을 줄 확률.</summary>
        public static float TipChance(StaffMember m) => 0.03f * Kindness(m) + (Has(m, "smile") ? 0.05f : 0f);
        /// <summary>경비가 몰래 나가는 아이를 잡을 확률.</summary>
        public static float CatchChance(StaffMember m) => Mathf.Min(0.97f, 0.25f + 0.06f * Sharp(m) + (Has(m, "eagle") ? 0.1f : 0f));
        /// <summary>상담 보조의 ★추천이 맞을 확률.</summary>
        public static float RecommendAccuracy(StaffMember m) => Mathf.Min(0.98f, 0.45f + 0.05f * Sharp(m) + (Has(m, "reader") ? 0.15f : 0f));
        /// <summary>상담 손님 인내심 배율 (상담 보조 친절).</summary>
        public static float CounselPatience(StaffMember m) => (1f + 0.05f * Kindness(m)) * (Has(m, "listener") ? 1.2f : 1f);

        /// <summary>
        /// 레벨업: 능력 포인트 +1 (5·10레벨은 +2)을 직접 나눠 준다. 3·6·9레벨엔 특기 고르기 +1.
        /// 맡은 일의 주 능력치는 홀수 레벨마다 저절로 +1 (아무것도 안 해도 조금은 큼).
        /// </summary>
        public static void LevelUp(StaffMember m, System.Random rng)
        {
            m.level++;
            m.points += m.level % 5 == 0 ? 2 : 1;
            if (m.level % 3 == 0) m.perkChoices++;
            if (m.level % 2 == 1)
            {
                switch (m.job)
                {
                    case StaffJob.Cashier: m.speed = Mathf.Min(StatCap, m.speed + 1); break;
                    case StaffJob.Stocker: m.walk = Mathf.Min(StatCap, Mathf.Max(1, m.walk) + 1); break;
                    case StaffJob.Guard: m.sharp = Mathf.Min(StatCap, m.sharp + 1); break;
                    default: m.kindness = Mathf.Min(StatCap, m.kindness + 1); break;
                }
            }
        }

        /// <summary>포인트 하나로 능력치 +1.</summary>
        public static bool Allocate(StaffMember m, StaffStat s)
        {
            if (m.points <= 0 || Base(m, s) >= StatCap) return false;
            switch (s)
            {
                case StaffStat.Speed: m.speed++; break;
                case StaffStat.Walk: m.walk = Mathf.Max(1, m.walk) + 1; break;
                case StaffStat.Kindness: m.kindness++; break;
                default: m.sharp++; break;
            }
            m.points--;
            return true;
        }

        /// <summary>지금 고를 수 있는 특기 두 개 (레벨·이름으로 고정, 이미 가진 건 뺌).</summary>
        public static List<StaffPerkDef> PerkOffer(StaffMember m)
        {
            var pool = new List<string>(PerkPools[(int)m.job]);
            pool.RemoveAll(id => Has(m, id));
            var rng = new System.Random(m.seed + m.level * 31 + (m.perks?.Count ?? 0) * 7);
            var list = new List<StaffPerkDef>();
            while (list.Count < 2 && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                list.Add(FindPerk(pool[i]));
                pool.RemoveAt(i);
            }
            return list;
        }

        // ───────────── 보따리 상인 ─────────────

        public const int MerchantEvery = 3;     // 며칠에 한 번
        public const int MerchantOfferCount = 3; // 한 번에 파는 가짓수 (각 1개씩)

        public static bool IsMerchantDay(int day) => day >= MerchantEvery && day % MerchantEvery == 0;

        public static int DaysToMerchant(int day)
        {
            for (int d = day; d < day + MerchantEvery + 1; d++) if (IsMerchantDay(d)) return d - day;
            return MerchantEvery;
        }

        /// <summary>그날 상인이 가져온 물건 3가지 (날짜로 고정). 희귀 25%·전설 6% 정도.</summary>
        public static List<StaffItemDef> MerchantOffer(int day)
        {
            var rng = new System.Random(day * 4481 + 91);
            var list = new List<StaffItemDef>();
            int guard = 0;
            while (list.Count < MerchantOfferCount && guard++ < 100)
            {
                double r = rng.NextDouble();
                int rarity = r < 0.06 + Math.Min(0.06, day * 0.002) ? 2 : r < 0.32 ? 1 : 0;
                var pool = Array.FindAll(Items, i => i.rarity == rarity);
                var pick = pool[rng.Next(pool.Length)];
                if (!list.Contains(pick)) list.Add(pick);
            }
            return list;
        }

        static readonly string[] Names =
        {
            "민수", "지영", "동훈", "은정", "성호", "미경", "재석", "혜진", "상우", "수진", "준호", "보람",
            "경민", "선영", "태식", "윤희", "진우", "현주",
        };

        /// <summary>그날의 지원자 (날짜로 고정 → 다시 켜도 같은 사람). 걸음은 처음엔 느리다 (1~3).</summary>
        public static List<StaffMember> Candidates(int day, int count, ICollection<string> taken)
        {
            var rng = new System.Random(day * 7919 + 17);
            var list = new List<StaffMember>();
            int guard = 0;
            while (list.Count < count && guard++ < 50)
            {
                string name = Names[rng.Next(Names.Length)];
                if (taken.Contains(name) || list.Exists(c => c.name == name)) continue;
                var m = new StaffMember
                {
                    name = name,
                    seed = rng.Next(1, int.MaxValue),
                    speed = 1 + rng.Next(4),
                    walk = 1 + rng.Next(3),
                    kindness = 1 + rng.Next(4),
                    sharp = 1 + rng.Next(4),
                };
                // 제일 높은 능력에 맞는 일로 지원
                m.job = m.sharp >= m.speed && m.sharp >= m.kindness ? (rng.Next(2) == 0 ? StaffJob.Guard : StaffJob.Counselor)
                    : m.speed >= m.kindness ? (rng.Next(2) == 0 ? StaffJob.Cashier : StaffJob.Stocker)
                    : StaffJob.Counselor;
                list.Add(m);
            }
            return list;
        }
    }

    /// <summary>고용한 직원 + 가진 아이템 + 그날 고용한 지원자 기록. 세이브됨.</summary>
    [Serializable]
    public class StaffState
    {
        public List<StaffMember> members = new List<StaffMember>();
        public List<string> ownedItems = new List<string>();   // 같은 아이템 여러 개 가능
        public List<string> hiredToday = new List<string>();   // 오늘 지원자 중 이미 고용한 이름
        public int hiredDay;
        public int maxStaff = StaffRules.StartMaxStaff;
        public int merchantDay;                                 // 상인 판매 기록이 몇 일차 것인지
        public List<string> merchantBought = new List<string>(); // 오늘 상인에게서 산 것

        public int OwnedCount(string id)
        {
            int n = 0;
            foreach (var i in ownedItems) if (i == id) n++;
            return n;
        }
    }
}
