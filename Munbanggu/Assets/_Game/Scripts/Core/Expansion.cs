using Munbanggu.Data;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>증축 한 단계가 가게에 더하는 것.</summary>
    public enum ExpansionFeature
    {
        Bay,        // 옆칸 하나 더 (왼쪽으로 2.5m, 진열대 2 + 옆 진열대 1)
        Pyeongsang, // 가게 앞 평상: 인내심 +20%, 손님 +10%
        ComicRoom,  // 2층 만화방: 매일 수입, 손님 +15%
        Freezer,    // 아이스크림 냉동고: 간식 수요 ×1.4, 손님 +10%
        Neon,       // 네온 간판: 손님 +20%
        Arcade2F,   // 2층 오락실: 매일 수입(더 큼), 손님 +10%
    }

    /// <summary>가게 증축 한 단계. 조건(날짜·평판)과 값을 채우면 아침에 살 수 있다. (docs/06·07)</summary>
    public sealed class ExpansionDef
    {
        public readonly string name;
        public readonly ExpansionFeature feature;
        public readonly int cost, minDay, minReputation, upkeepAdd;
        public readonly string[] effects;   // 목록에 보여 줄 효과 줄
        public readonly string celebrate;   // 다 지었을 때 할머니 대사

        public ExpansionDef(string name, ExpansionFeature feature, int cost, int minDay, int minReputation, int upkeepAdd,
            string[] effects, string celebrate)
        {
            this.name = name; this.feature = feature; this.cost = cost; this.minDay = minDay;
            this.minReputation = minReputation; this.upkeepAdd = upkeepAdd; this.effects = effects; this.celebrate = celebrate;
        }
    }

    /// <summary>가게 색 (바닥 체크무늬·벽·징두리·차양). 동네마다 건물이 다르다.</summary>
    public sealed class DistrictPalette
    {
        public Color floorA, floorB, wallUpper, wainscot, wainscotLine, awning;
    }

    /// <summary>
    /// 상권(동네). 동네를 옮길 때마다 더 큰 건물로 이사한다 (경영 시뮬레이션 느낌).
    /// 건물 기본 크기(옆칸 수)·직원 정원·손님 수·잘 팔리는 종류·월세가 다르고, 동네마다 증축 단계 3개.
    /// </summary>
    public sealed class DistrictDef
    {
        public readonly string id, name, blurb;
        public readonly int baseBays, baseStaff, baseRent;
        public readonly float spawnMultiplier;
        public readonly float snack, stationery, toy, play;   // 종류별 수요 배율
        public readonly int moveCost, moveMinReputation;       // 이 동네로 이사 오는 값·조건
        public readonly string[] traits;                       // 이사 안내에 보여 줄 줄
        public readonly DistrictPalette palette;
        public readonly ExpansionDef[] steps;

        public DistrictDef(string id, string name, string blurb, int baseBays, int baseStaff, int baseRent, float spawn,
            float snack, float stationery, float toy, float play, int moveCost, int moveMinReputation,
            string[] traits, DistrictPalette palette, ExpansionDef[] steps)
        {
            this.id = id; this.name = name; this.blurb = blurb; this.baseBays = baseBays; this.baseStaff = baseStaff;
            this.baseRent = baseRent; spawnMultiplier = spawn; this.snack = snack; this.stationery = stationery;
            this.toy = toy; this.play = play; this.moveCost = moveCost; this.moveMinReputation = moveMinReputation;
            this.traits = traits; this.palette = palette; this.steps = steps;
        }

        public float Demand(ProductCategory c) => c switch
        {
            ProductCategory.Snack => snack,
            ProductCategory.Stationery => stationery,
            ProductCategory.Toy => toy,
            _ => play,
        };
    }

    /// <summary>
    /// 동네·증축 표. District = 지금 동네(0~), level = 그 동네에서 지은 증축 단계(0~3).
    /// 동네 증축을 다 지으면 다음 동네로 이사할 수 있다 (평판 일부만 이어짐, 증축은 새 동네 0단계부터).
    /// </summary>
    public static class ExpansionTable
    {
        public static readonly DistrictDef[] Districts =
        {
            new DistrictDef("school", "학교 앞 골목", "초등학교 정문 앞 작은 문방구. 여기서 시작!",
                baseBays: 0, baseStaff: 2, baseRent: 0, spawn: 1f,
                snack: 1f, stationery: 1f, toy: 1f, play: 1f, moveCost: 0, moveMinReputation: 0,
                traits: new string[0],
                palette: new DistrictPalette
                {
                    floorA = new Color(0.96f, 0.90f, 0.77f), floorB = new Color(0.80f, 0.50f, 0.42f),
                    wallUpper = new Color(0.98f, 0.93f, 0.81f), wainscot = new Color(0.56f, 0.78f, 0.70f),
                    wainscotLine = new Color(0.46f, 0.67f, 0.60f), awning = new Color(0.89f, 0.27f, 0.24f),
                },
                steps: new[]
                {
                    new ExpansionDef("옆칸 터서 넓히기", ExpansionFeature.Bay, 12000, 4, 0, 300,
                        new[] { "가게가 왼쪽으로 넓어져요", "진열대 2개 추가", "직원 +1명", "관리비 하루 +300원" },
                        "옆집 칸을 텄더니 가게가 훤하네! 진열대를 더 놓을 수 있겠어."),
                    new ExpansionDef("가게 앞 평상", ExpansionFeature.Pyeongsang, 25000, 7, 60, 300,
                        new[] { "평상에서 쉬어 가니 손님 인내심 +20%", "손님 +10%", "진열대 1개 추가", "직원 +1명", "관리비 하루 +300원" },
                        "평상을 놓았더니 아이들이 모여 앉아 수다를 떠네. 동네 사랑방이 됐어!"),
                    new ExpansionDef("2층 만화방", ExpansionFeature.ComicRoom, 45000, 10, 70, 400,
                        new[] { "만화방 운영: 만화책 모으기·자리·요금 (동네 → 만화방)", "손님 +15%", "직원 +1명", "관리비 하루 +400원" },
                        "2층에 만화방을 열었어! 만화 보러 왔다가 내려와서 군것질도 하겠지?"),
                }),

            new DistrictDef("apartment", "아파트 단지 상가", "새로 생긴 아파트 단지 상가 1층. 옆칸이 붙은 넓은 가게!",
                baseBays: 1, baseStaff: 3, baseRent: 800, spawn: 1.25f,
                snack: 1f, stationery: 1.3f, toy: 1.2f, play: 0.9f, moveCost: 60000, moveMinReputation: 70,
                traits: new[] { "처음부터 옆칸이 붙은 큰 가게 (진열대 +3)", "손님 ×1.25", "학용품 ×1.3 · 장난감 ×1.2 잘 팔림", "직원 3명부터", "월세 하루 800원" },
                palette: new DistrictPalette
                {
                    floorA = new Color(0.90f, 0.92f, 0.93f), floorB = new Color(0.58f, 0.70f, 0.82f),
                    wallUpper = new Color(0.95f, 0.97f, 0.94f), wainscot = new Color(0.98f, 0.76f, 0.55f),
                    wainscotLine = new Color(0.90f, 0.64f, 0.44f), awning = new Color(0.30f, 0.66f, 0.44f),
                },
                steps: new[]
                {
                    new ExpansionDef("옆칸 하나 더", ExpansionFeature.Bay, 30000, 0, 0, 400,
                        new[] { "가게가 왼쪽으로 한 칸 더", "진열대 3개 추가", "직원 +1명", "관리비 하루 +400원" },
                        "상가 옆 가게가 빠져서 터 버렸지! 이제 진짜 큰 문방구네."),
                    new ExpansionDef("아이스크림 냉동고", ExpansionFeature.Freezer, 40000, 0, 65, 400,
                        new[] { "가게 앞 냉동고: 간식 ×1.4 잘 팔림", "손님 +10%", "직원 +1명", "관리비 하루 +400원" },
                        "냉동고를 들였더니 하교길 아이들이 줄을 서네. 여름엔 대박이겠어!"),
                    new ExpansionDef("2층 만화방", ExpansionFeature.ComicRoom, 60000, 0, 75, 500,
                        new[] { "만화방 운영: 만화책 모으기·자리·요금 (동네 → 만화방)", "손님 +15%", "직원 +1명", "관리비 하루 +500원" },
                        "아파트 아이들이 2층 만화방에 모여 숙제도 하고 만화도 보네!"),
                }),

            new DistrictDef("downtown", "번화가 대로변", "버스 정류장 앞 대로변 2칸짜리 건물. 손님이 끝없이 와요!",
                baseBays: 2, baseStaff: 4, baseRent: 2000, spawn: 1.5f,
                snack: 1.2f, stationery: 0.9f, toy: 1.3f, play: 1.3f, moveCost: 120000, moveMinReputation: 80,
                traits: new[] { "처음부터 옆칸 2개 붙은 대형 가게 (진열대 +6)", "손님 ×1.5", "장난감·놀이 ×1.3 · 간식 ×1.2", "직원 4명부터", "월세 하루 2,000원" },
                palette: new DistrictPalette
                {
                    floorA = new Color(0.95f, 0.92f, 0.86f), floorB = new Color(0.36f, 0.36f, 0.44f),
                    wallUpper = new Color(0.98f, 0.92f, 0.88f), wainscot = new Color(0.32f, 0.40f, 0.64f),
                    wainscotLine = new Color(0.26f, 0.32f, 0.54f), awning = new Color(0.22f, 0.42f, 0.80f),
                },
                steps: new[]
                {
                    new ExpansionDef("옆칸 하나 더", ExpansionFeature.Bay, 60000, 0, 0, 600,
                        new[] { "가게가 왼쪽으로 한 칸 더 (3칸)", "진열대 3개 추가", "직원 +1명", "관리비 하루 +600원" },
                        "대로변 3칸짜리 문방구라니! 동네에서 제일 큰 가게가 됐어."),
                    new ExpansionDef("네온 간판", ExpansionFeature.Neon, 80000, 0, 70, 500,
                        new[] { "번쩍이는 네온 간판: 손님 +20%", "직원 +1명", "관리비 하루 +500원" },
                        "밤에도 번쩍번쩍! 멀리서도 우리 가게가 보이겠어."),
                    new ExpansionDef("2층 오락실", ExpansionFeature.Arcade2F, 120000, 0, 80, 800,
                        new[] { "매일 오락실 수입 (만화방보다 큼)", "손님 +10%", "직원 +1명", "관리비 하루 +800원" },
                        "2층 오락실까지! 이제 이 동네 아이들은 다 우리 가게로 오겠구나."),
                }),
        };

        public static int Max => 3; // 동네마다 증축 단계 수

        public static DistrictDef District(int d) => Districts[Mathf.Clamp(d, 0, Districts.Length - 1)];
        public static DistrictDef NextDistrict(int d) => d + 1 < Districts.Length ? Districts[d + 1] : null;

        /// <summary>다음 단계 (다 지었으면 null).</summary>
        public static ExpansionDef Next(int district, int level)
        {
            var s = District(district).steps;
            return level < s.Length ? s[level] : null;
        }

        /// <summary>단계 이름 (0 = 동네 처음 가게).</summary>
        public static string Name(int district, int level) =>
            level <= 0 ? District(district).name : District(district).steps[level - 1].name;

        /// <summary>이 동네에서 level까지 지었을 때 feature가 있는지.</summary>
        public static bool Has(int district, int level, ExpansionFeature f)
        {
            var s = District(district).steps;
            for (int i = 0; i < level && i < s.Length; i++)
                if (s[i].feature == f) return true;
            return false;
        }

        /// <summary>옆칸 수 = 동네 기본 + 지은 옆칸.</summary>
        public static int Bays(int district, int level)
        {
            int n = District(district).baseBays;
            var s = District(district).steps;
            for (int i = 0; i < level && i < s.Length; i++)
                if (s[i].feature == ExpansionFeature.Bay) n++;
            return n;
        }

        /// <summary>
        /// 옆칸 k(1부터)의 옆 진열대가 있는지. 처음 동네는 평상 단계에서, 다른 동네는 옆칸이 생길 때 같이.
        /// </summary>
        public static bool HasSideShelf(int district, int level, int k)
        {
            if (k > Bays(district, level)) return false;
            return district > 0 || Has(district, level, ExpansionFeature.Pyeongsang);
        }

        /// <summary>
        /// 옆칸 k의 깊이(뒷벽 z). 동네마다 가게 모양이 다르다:
        /// 학교 앞 = 네모(4), 아파트 = 옆칸이 뒤로 깊은 ㄱ자(6), 번화가 = 첫 옆칸은 얕고 그다음부터 깊은 계단 모양(4 → 6.5).
        /// </summary>
        public static float BayBack(int district, int k) => district switch
        {
            1 => 6f,
            2 => k == 1 ? 4f : 6.5f,
            _ => 4f,
        };

        public static int MaxStaff(int district, int level) => District(district).baseStaff + level;

        /// <summary>매일 정산 때 나가는 월세 + 관리비.</summary>
        public static int Upkeep(int district, int level)
        {
            var d = District(district);
            int u = d.baseRent;
            for (int i = 0; i < level && i < d.steps.Length; i++) u += d.steps[i].upkeepAdd;
            return u;
        }

        /// <summary>손님 수 배율 (동네 × 증축).</summary>
        public static float SpawnMultiplier(int district, int level)
        {
            float m = District(district).spawnMultiplier;
            if (Has(district, level, ExpansionFeature.Pyeongsang)) m *= 1.1f;
            if (Has(district, level, ExpansionFeature.ComicRoom)) m *= 1.15f;
            if (Has(district, level, ExpansionFeature.Freezer)) m *= 1.1f;
            if (Has(district, level, ExpansionFeature.Neon)) m *= 1.2f;
            if (Has(district, level, ExpansionFeature.Arcade2F)) m *= 1.1f;
            return m;
        }

        /// <summary>줄 선 손님 인내심 배율 (평상).</summary>
        public static float PatienceMultiplier(int district, int level) =>
            Has(district, level, ExpansionFeature.Pyeongsang) ? 1.2f : 1f;

        /// <summary>종류별 수요 배율 (동네 성향 × 냉동고).</summary>
        public static float Demand(int district, int level, ProductCategory c)
        {
            float m = District(district).Demand(c);
            if (c == ProductCategory.Snack && Has(district, level, ExpansionFeature.Freezer)) m *= 1.4f;
            return m;
        }

        /// <summary>2층 하루 수입: 만화방 600 + 손님당 40, 오락실 1,000 + 손님당 60.</summary>
        public static int UpstairsIncome(int district, int level, int visitors, out string label)
        {
            label = null;
            if (Has(district, level, ExpansionFeature.Arcade2F)) { label = "2층 오락실 수입"; return 1000 + 60 * visitors; }
            if (Has(district, level, ExpansionFeature.ComicRoom)) { label = "2층 만화방 수입"; return 600 + 40 * visitors; }
            return 0;
        }
    }
}
