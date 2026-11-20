using System.Collections.Generic;

namespace Munbanggu.Data
{
    /// <summary>
    /// 상품 특징(태그). 상담 손님의 요청과 맞춰 본다 (docs/05-fun-redesign.md 2-2).
    /// 상품 id → 특징 목록. 표에 없는 상품은 특징 없음(상담 대상 아님).
    /// </summary>
    public static class ProductTraits
    {
        public const string Snack = "간식", Sweet = "달콤", Long = "오래", Cheap = "싸다", Play = "놀이",
            Friends = "친구랑", Cool = "멋짐", Collect = "수집", School = "학용품", Cute = "귀여움",
            Gift = "선물", Alone = "혼자", Skill = "기술", Prank = "장난", Outside = "밖", Luck = "운",
            Summer = "여름", Squishy = "촉감";

        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
        {
            { "jjondeugi", new[] { Snack, Long, Cheap } },
            { "apollo", new[] { Snack, Sweet, Cheap } },
            { "ddakji", new[] { Play, Friends, Cool } },
            { "marble", new[] { Play, Friends, Collect } },
            { "eraser", new[] { School, Cute, Collect } },
            { "sticker", new[] { Cute, Collect, Gift } },
            { "paperdoll", new[] { Cute, Alone, Gift } },
            { "minicar", new[] { Cool, Gift, Skill } },
            { "beercandy", new[] { Snack, Prank, Cheap } },
            { "pencil", new[] { School, Gift } },
            { "bouncyball", new[] { Play, Outside, Friends } },
            { "yoyo", new[] { Skill, Cool, Play } },
            { "ssaldaerong", new[] { Snack, Long, Friends } },
            { "bubble", new[] { Outside, Cute, Play } },
            { "card", new[] { Collect, Luck, Friends } },
            { "rubberband", new[] { Play, Friends, Outside } },
            { "watergun", new[] { Outside, Prank, Summer } },
            { "virtualpet", new[] { Alone, Cute, Gift } },
            { "slime", new[] { Squishy, Prank, Cute } },
        };

        static readonly string[] None = new string[0];

        public static string[] Of(ProductData p) =>
            p != null && table.TryGetValue(p.id, out var t) ? t : None;

        public static bool Has(ProductData p, string trait)
        {
            foreach (var t in Of(p)) if (t == trait) return true;
            return false;
        }

        /// <summary>상담 대상 상품인지 (특징이 있는 상품).</summary>
        public static bool Counselable(ProductData p) => Of(p).Length > 0;
    }
}
