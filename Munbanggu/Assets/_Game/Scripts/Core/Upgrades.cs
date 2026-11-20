using System;
using System.Collections.Generic;

namespace Munbanggu.Core
{
    public enum UpgradeCategory { Shop, Customers, Helpers, Money }

    /// <summary>가게 업그레이드 하나 (단계마다 비용과 설명).</summary>
    public sealed class UpgradeDef
    {
        public readonly string id;
        public readonly string name;
        public readonly UpgradeCategory category;
        public readonly int[] costs;          // costs[i] = i+1단계로 올리는 값
        public readonly string[] effects;     // effects[i] = i+1단계 효과 설명
        public readonly string blurb;

        public int MaxLevel => costs.Length;

        public UpgradeDef(string id, string name, UpgradeCategory category, string blurb, int[] costs, string[] effects)
        {
            this.id = id; this.name = name; this.category = category; this.blurb = blurb;
            this.costs = costs; this.effects = effects;
        }
    }

    /// <summary>
    /// 업그레이드 목록과 효과 수치. 효과는 여기 함수로만 꺼내 쓴다 (밸런스 조정은 이 파일에서).
    /// 진행 상태(단계)는 Upgrades가 들고 세이브된다.
    /// </summary>
    public static class UpgradeTable
    {
        public const string ShelfSize = "shelf_size";
        public const string Island = "island";
        public const string Sign = "sign";
        public const string Fan = "fan";
        public const string Hours = "hours";
        public const string Helper = "helper";
        public const string Mirror = "mirror";
        public const string Ledger = "ledger";
        public const string Wholesale = "wholesale";
        public const string Arcade = "arcade";

        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef(ShelfSize, "매대 넓히기", UpgradeCategory.Shop, "매대 한 칸에 더 많이 올려요.",
                new[] { 4000, 12000 }, new[] { "매대 한 칸 12 → 15개", "매대 한 칸 15 → 18개" }),
            new UpgradeDef(Island, "진열대 추가", UpgradeCategory.Shop, "창가와 왼쪽 벽에 진열대를 하나씩 놓아요.",
                new[] { 9000 }, new[] { "매대 2칸 추가" }),
            new UpgradeDef(Sign, "새 간판", UpgradeCategory.Customers, "번쩍이는 간판으로 아이들을 끌어요.",
                new[] { 3000, 9000, 20000 }, new[] { "손님 +15%", "손님 +30%", "손님 +45%" }),
            new UpgradeDef(Fan, "선풍기·의자", UpgradeCategory.Customers, "시원하면 줄 서서도 덜 짜증 내요.",
                new[] { 2500, 7000 }, new[] { "손님 인내심 +25%", "손님 인내심 +50%" }),
            new UpgradeDef(Hours, "영업시간 연장", UpgradeCategory.Customers, "문을 조금 더 늦게 닫아요.",
                new[] { 5000, 14000 }, new[] { "영업 +20초", "영업 +40초" }),
            new UpgradeDef(Helper, "알바생", UpgradeCategory.Helpers, "계산 줄 맨 앞 손님을 알아서 계산해 줘요.",
                new[] { 10000, 25000 }, new[] { "6초마다 한 명 계산", "3.5초마다 한 명 계산" }),
            new UpgradeDef(Mirror, "방범 거울", UpgradeCategory.Helpers, "몰래 나가는 아이가 눈치 보느라 느려져요.",
                new[] { 3000, 8000 }, new[] { "슬쩍 아이 속도 -25%", "슬쩍 아이 속도 -45%" }),
            new UpgradeDef(Ledger, "외상 장부 관리", UpgradeCategory.Helpers, "외상 장부에 집 전화번호까지 적어 둬요.",
                new[] { 2000, 6000 }, new[] { "외상 갚을 확률 60 → 75%", "외상 갚을 확률 75 → 90%" }),
            new UpgradeDef(Wholesale, "도매상 단골", UpgradeCategory.Money, "자주 가면 도매상이 깎아 줘요.",
                new[] { 6000, 18000 }, new[] { "도매가 -10%", "도매가 -20%" }),
            new UpgradeDef(Arcade, "가게 앞 오락기", UpgradeCategory.Money, "영업 중에 아이들이 와서 한 판씩 하고 가요.",
                new[] { 7000, 16000 }, new[] { "오락기 1대 (영업 중 동전 수입)", "오락기 2대" }),
        };

        public static UpgradeDef Find(string id)
        {
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        // ───── 효과 수치 (단계 → 값) ─────
        public static int ShelfCapacity(int lv) => 12 + 3 * lv;               // 12 / 15 / 18
        public static float SpawnMultiplier(int lv) => 1f + 0.15f * lv;
        public static float PatienceMultiplier(int lv) => 1f + 0.25f * lv;
        public static float ExtraOpenSeconds(int lv) => 20f * lv;
        public static float HelperInterval(int lv) => lv <= 0 ? 0f : lv == 1 ? 6f : 3.5f;
        public static float SneakSpeedMultiplier(int lv) => lv <= 0 ? 1f : lv == 1 ? 0.75f : 0.55f;
        public static float CreditRepayChance(int lv) => lv <= 0 ? 0.6f : lv == 1 ? 0.75f : 0.9f;
        public static float WholesaleMultiplier(int lv) => 1f - 0.1f * lv;
        public static int ArcadeMachines(int lv) => lv;
        public const float ArcadePlayInterval = 7f; // 오락기 한 대가 한 판 벌어 오는 간격(초)
        public const int ArcadeCoin = 100;
    }

    [Serializable]
    public class UpgradeLevel
    {
        public string id;
        public int level;
    }

    /// <summary>업그레이드 진행 상태 (id → 단계). 세이브됨.</summary>
    public class Upgrades
    {
        readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public int Level(string id) => levels.TryGetValue(id, out var lv) ? lv : 0;

        public bool IsMaxed(UpgradeDef d) => Level(d.id) >= d.MaxLevel;

        /// <summary>다음 단계 비용 (최대면 0).</summary>
        public int NextCost(UpgradeDef d) => IsMaxed(d) ? 0 : d.costs[Level(d.id)];

        public void Raise(string id) => levels[id] = Level(id) + 1;

        public List<UpgradeLevel> Save()
        {
            var list = new List<UpgradeLevel>();
            foreach (var kv in levels) list.Add(new UpgradeLevel { id = kv.Key, level = kv.Value });
            return list;
        }

        public void Load(List<UpgradeLevel> saved)
        {
            levels.Clear();
            if (saved == null) return;
            foreach (var e in saved)
            {
                var d = UpgradeTable.Find(e.id);
                if (d != null) levels[e.id] = Math.Max(0, Math.Min(d.MaxLevel, e.level));
            }
        }
    }
}
