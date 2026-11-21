namespace Munbanggu.Core
{
    /// <summary>가게 증축 한 단계. 조건(날짜·평판)과 값을 채우면 아침에 살 수 있다. (docs/06 3단계)</summary>
    public sealed class ExpansionDef
    {
        public readonly string name;
        public readonly int cost, minDay, minReputation;
        public readonly string[] effects;   // 목록에 보여 줄 효과 줄
        public readonly string celebrate;   // 다 지었을 때 할머니 대사

        public ExpansionDef(string name, int cost, int minDay, int minReputation, string[] effects, string celebrate)
        {
            this.name = name; this.cost = cost; this.minDay = minDay; this.minReputation = minReputation;
            this.effects = effects; this.celebrate = celebrate;
        }
    }

    /// <summary>
    /// 가게 증축 단계표. 0 = 처음 가게.
    /// 1 옆칸 터서 넓히기 (진열대 2 + 직원 3명) → 2 가게 앞 평상 (진열대 1 + 인내심·손님 + 직원 4명)
    /// → 3 2층 만화방 (매일 만화방 수입 + 손님 + 직원 5명). 가게가 클수록 매일 관리비가 나간다.
    /// </summary>
    public static class ExpansionTable
    {
        public static readonly ExpansionDef[] Steps =
        {
            new ExpansionDef("옆칸 터서 넓히기", 12000, 4, 0,
                new[] { "가게가 왼쪽으로 넓어져요", "진열대 2개 추가", "직원 3명까지", "관리비 하루 300원" },
                "옆집 칸을 텄더니 가게가 훤하네! 진열대를 더 놓을 수 있겠어."),
            new ExpansionDef("가게 앞 평상", 25000, 7, 60,
                new[] { "평상에서 쉬어 가니 손님 인내심 +20%", "손님 +10%", "진열대 1개 추가", "직원 4명까지", "관리비 하루 600원" },
                "평상을 놓았더니 아이들이 모여 앉아 수다를 떠네. 동네 사랑방이 됐어!"),
            new ExpansionDef("2층 만화방", 45000, 10, 70,
                new[] { "매일 만화방 수입 (손님이 많을수록 더)", "손님 +15%", "직원 5명까지", "관리비 하루 1,000원" },
                "2층에 만화방을 열었어! 만화 보러 왔다가 내려와서 군것질도 하겠지?"),
        };

        public static int Max => Steps.Length;

        /// <summary>다음 단계 (다 지었으면 null).</summary>
        public static ExpansionDef Next(int level) => level < Steps.Length ? Steps[level] : null;

        /// <summary>단계 이름 (0 = 처음 가게).</summary>
        public static string Name(int level) => level <= 0 ? "처음 가게" : Steps[level - 1].name;

        public static int MaxStaff(int level) => StaffRules.StartMaxStaff + level;

        /// <summary>매일 정산 때 나가는 관리비.</summary>
        public static int Upkeep(int level) => level switch { 0 => 0, 1 => 300, 2 => 600, _ => 1000 };

        /// <summary>손님 수 배율.</summary>
        public static float SpawnMultiplier(int level) => 1f + (level >= 2 ? 0.1f : 0f) + (level >= 3 ? 0.15f : 0f);

        /// <summary>줄 선 손님 인내심 배율 (평상).</summary>
        public static float PatienceMultiplier(int level) => level >= 2 ? 1.2f : 1f;

        /// <summary>만화방 하루 수입: 기본 600원 + 오늘 손님 1명당 40원.</summary>
        public static int ComicIncome(int level, int visitors) => level >= 3 ? 600 + 40 * visitors : 0;
    }
}
