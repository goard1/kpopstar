using System;
using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>직원이 맡는 일 (docs/06 1장).</summary>
    public enum StaffJob { Cashier, Stocker, Guard, Counselor }

    /// <summary>직원 한 명. 능력치는 1~10 (아이템 보너스는 Effective*로 더함).</summary>
    [Serializable]
    public class StaffMember
    {
        public string name;
        public int seed;          // 외형 (CustomerVisual)
        public int level = 1;
        public int exp;
        public int speed;         // 손 빠르기
        public int kindness;      // 친절
        public int sharp;         // 눈썰미
        public StaffJob job;
        public string item = "";  // 장착 아이템 id ("" = 없음)
    }

    /// <summary>직원 아이템: 한 번 사면 계속, 직원 한 명에게 하나 장착.</summary>
    public sealed class StaffItemDef
    {
        public readonly string id, name, desc;
        public readonly int price, speed, kindness, sharp;

        public StaffItemDef(string id, string name, int price, int speed, int kindness, int sharp, string desc)
        {
            this.id = id; this.name = name; this.price = price;
            this.speed = speed; this.kindness = kindness; this.sharp = sharp; this.desc = desc;
        }
    }

    /// <summary>직원 규칙·수치 (밸런스는 여기서).</summary>
    public static class StaffRules
    {
        public const int MaxLevel = 10;
        public const int StartMaxStaff = 2;

        public static readonly StaffItemDef[] Items =
        {
            new StaffItemDef("calculator", "전자계산기", 2500, 2, 0, 0, "손 빠르기 +2"),
            new StaffItemDef("sneakers", "운동화", 2000, 1, 0, 1, "손 빠르기 +1, 눈썰미 +1"),
            new StaffItemDef("nametag", "예쁜 명찰", 1800, 0, 2, 0, "친절 +2"),
            new StaffItemDef("magnifier", "돋보기", 2200, 0, 0, 2, "눈썰미 +2"),
            new StaffItemDef("whistle", "호루라기", 1500, 0, 0, 1, "눈썰미 +1 (경비에 딱)"),
            new StaffItemDef("apron", "문방구 앞치마", 3500, 1, 1, 1, "모든 능력 +1"),
        };

        public static StaffItemDef FindItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var i in Items) if (i.id == id) return i;
            return null;
        }

        public static string JobName(StaffJob j) => j switch
        {
            StaffJob.Cashier => "계산·진열",
            StaffJob.Stocker => "진열·계산",
            StaffJob.Guard => "경비",
            _ => "상담 보조",
        };

        public static string JobDesc(StaffJob j) => j switch
        {
            StaffJob.Cashier => "돌아다니며 계산·진열, 계산 먼저 (손 빠르기·친절)",
            StaffJob.Stocker => "돌아다니며 진열·계산, 진열 먼저 (손 빠르기)",
            StaffJob.Guard => "몰래 나가는 아이를 문 앞에서 잡음 (눈썰미)",
            _ => "고르기 창에 ★추천을 붙여 줌 (눈썰미), 상담 손님 인내심↑ (친절)",
        };

        public static int Speed(StaffMember m) => Mathf.Clamp(m.speed + (FindItem(m.item)?.speed ?? 0), 1, 14);
        public static int Kindness(StaffMember m) => Mathf.Clamp(m.kindness + (FindItem(m.item)?.kindness ?? 0), 1, 14);
        public static int Sharp(StaffMember m) => Mathf.Clamp(m.sharp + (FindItem(m.item)?.sharp ?? 0), 1, 14);

        public static int ExpToNext(int level) => 8 + level * 6;
        public static int DailySalary(StaffMember m) => 300 + m.level * 120;
        public static int TrainCost(StaffMember m) => 800 + m.level * 500;

        /// <summary>계산 간격(초): 빠르기 1 → 7초, 10 → 3초.</summary>
        public static float CashierInterval(StaffMember m) => Mathf.Max(2.2f, 7.4f - 0.44f * Speed(m));
        /// <summary>진열 간격(초).</summary>
        public static float StockerInterval(StaffMember m) => Mathf.Max(2.5f, 9f - 0.6f * Speed(m));
        /// <summary>한 번에 채우는 개수.</summary>
        public static int StockerBatch(StaffMember m) => 3 + m.level / 2;
        /// <summary>계산할 때 손님이 팁을 줄 확률.</summary>
        public static float TipChance(StaffMember m) => 0.03f * Kindness(m);
        /// <summary>경비가 몰래 나가는 아이를 잡을 확률.</summary>
        public static float CatchChance(StaffMember m) => Mathf.Min(0.95f, 0.25f + 0.06f * Sharp(m));
        /// <summary>상담 보조의 ★추천이 맞을 확률.</summary>
        public static float RecommendAccuracy(StaffMember m) => Mathf.Min(0.97f, 0.45f + 0.05f * Sharp(m));
        /// <summary>상담 손님 인내심 배율 (상담 보조 친절).</summary>
        public static float CounselPatience(StaffMember m) => 1f + 0.05f * Kindness(m);

        /// <summary>레벨업: 맡은 일의 주 능력치 +1, 짝수 레벨엔 나머지 중 하나 +1.</summary>
        public static void LevelUp(StaffMember m, System.Random rng)
        {
            m.level++;
            switch (m.job)
            {
                case StaffJob.Cashier:
                case StaffJob.Stocker: m.speed = Mathf.Min(10, m.speed + 1); break;
                case StaffJob.Guard: m.sharp = Mathf.Min(10, m.sharp + 1); break;
                default:
                    if (m.level % 2 == 0) m.sharp = Mathf.Min(10, m.sharp + 1);
                    else m.kindness = Mathf.Min(10, m.kindness + 1);
                    break;
            }
            if (m.level % 2 == 0)
            {
                int r = rng.Next(3);
                if (r == 0) m.speed = Mathf.Min(10, m.speed + 1);
                else if (r == 1) m.kindness = Mathf.Min(10, m.kindness + 1);
                else m.sharp = Mathf.Min(10, m.sharp + 1);
            }
        }

        static readonly string[] Names =
        {
            "민수", "지영", "동훈", "은정", "성호", "미경", "재석", "혜진", "상우", "수진", "준호", "보람",
            "경민", "선영", "태식", "윤희", "진우", "현주",
        };

        /// <summary>그날의 지원자 (날짜로 고정 → 다시 켜도 같은 사람).</summary>
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
                    speed = 1 + rng.Next(5),
                    kindness = 1 + rng.Next(5),
                    sharp = 1 + rng.Next(5),
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
        public List<string> ownedItems = new List<string>();
        public List<string> hiredToday = new List<string>(); // 오늘 지원자 중 이미 고용한 이름
        public int hiredDay;
        public int maxStaff = StaffRules.StartMaxStaff;
    }
}
