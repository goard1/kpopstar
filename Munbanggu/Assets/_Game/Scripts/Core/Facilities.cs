using System;
using System.Collections.Generic;
using Munbanggu.Data;

namespace Munbanggu.Core
{
    /// <summary>진열대 위 장식 (docs/06 2장). affinity가 진열대 상품 종류와 맞으면 "○○ 코너" 콤보.</summary>
    public sealed class DecorDef
    {
        public readonly string id, name, desc;
        public readonly ProductCategory? affinity; // null = 어디에나 어울림 (손님 끌기만)
        public readonly int[] costs;               // costs[0] = 놓기, costs[1..] = 레벨업

        public int MaxLevel => costs.Length;

        public DecorDef(string id, string name, ProductCategory? affinity, int[] costs, string desc)
        {
            this.id = id; this.name = name; this.affinity = affinity; this.costs = costs; this.desc = desc;
        }
    }

    /// <summary>옆 진열대 상품 짝 콤보 ("세트").</summary>
    public sealed class SetComboDef
    {
        public readonly string id, a, b, name, flavor;

        public SetComboDef(string a, string b, string name, string flavor)
        {
            this.a = a; this.b = b; this.name = name; this.flavor = flavor;
            id = "set:" + name;
        }

        public bool Matches(string x, string y) => (x == a && y == b) || (x == b && y == a);
    }

    [Serializable]
    public class DecorPlacement
    {
        public int slot;     // 진열대 번호 (Layout.Slots 인덱스)
        public string id;
        public int level = 1;
    }

    /// <summary>시설(장식) 배치 상태. 세이브됨.</summary>
    [Serializable]
    public class FacilityState
    {
        public List<DecorPlacement> decor = new List<DecorPlacement>();
        public List<string> discovered = new List<string>(); // 발견한 콤보 id

        public DecorPlacement At(int slot) => decor.Find(d => d.slot == slot);
    }

    /// <summary>장식·콤보 목록과 수치 (밸런스는 여기서).</summary>
    public static class FacilityTable
    {
        public static readonly DecorDef[] Decors =
        {
            new DecorDef("candyjar", "사탕 유리병", ProductCategory.Snack, new[] { 1500, 4000, 9000 }, "간식 진열대에 놓으면 군것질 코너"),
            new DecorDef("robot", "양철 로봇 모형", ProductCategory.Toy, new[] { 2000, 5000, 11000 }, "장난감 진열대에 놓으면 장난감 코너"),
            new DecorDef("trophy", "딱지왕 트로피", ProductCategory.Play, new[] { 1800, 4500, 10000 }, "놀이 진열대에 놓으면 놀이 코너"),
            new DecorDef("sharpener", "연필깎이 기계", ProductCategory.Stationery, new[] { 1500, 4000, 9000 }, "학용품 진열대에 놓으면 학용품 코너"),
            new DecorDef("plant", "작은 화분", null, new[] { 1000, 3000, 7000 }, "어디에 놓아도 가게가 산뜻해져요 (손님↑)"),
            new DecorDef("radio", "복고 라디오", null, new[] { 2500, 6000, 12000 }, "흥겨운 노래로 손님을 끌어요 (손님↑↑)"),
        };

        public static readonly SetComboDef[] Sets =
        {
            new SetComboDef("jjondeugi", "apollo", "불량식품 코너", "쫀드기 옆에 아폴로, 이건 못 참지!"),
            new SetComboDef("ddakji", "marble", "골목대장 세트", "딱지 따고 구슬 따고, 오늘의 골목대장!"),
            new SetComboDef("sticker", "eraser", "필통 꾸미기 세트", "스티커 붙이고 향기 지우개 넣고!"),
            new SetComboDef("paperdoll", "sticker", "꾸미기 놀이 세트", "종이인형 옷에 스티커로 반짝반짝"),
            new SetComboDef("minicar", "yoyo", "장난감 대장 세트", "미니카 경주 끝나면 요요 대결!"),
            new SetComboDef("bouncyball", "bubble", "운동장 놀이 세트", "탱탱볼 차고 비눗방울 불고"),
            new SetComboDef("beercandy", "ssaldaerong", "주머니 간식 세트", "100원으로 둘 다 사 먹기"),
            new SetComboDef("pencil", "eraser", "새 학기 세트", "새 연필에 새 지우개, 공부할 맛 난다"),
            new SetComboDef("card", "marble", "수집왕 세트", "모으고 자랑하고 바꾸고"),
            new SetComboDef("watergun", "bubble", "여름 놀이 세트", "물총 쏘고 비눗방울 날리고"),
            new SetComboDef("virtualpet", "slime", "요즘 유행 세트", "반 애들이 다 갖고 있는 그거!"),
        };

        public static DecorDef FindDecor(string id) => Array.Find(Decors, d => d.id == id);

        public static string CornerName(ProductCategory c) => c switch
        {
            ProductCategory.Snack => "군것질 코너",
            ProductCategory.Stationery => "학용품 코너",
            ProductCategory.Toy => "장난감 코너",
            _ => "놀이 코너",
        };

        public static string CornerId(ProductCategory c) => "corner:" + c;

        /// <summary>코너 콤보 수요 배율 (장식 레벨 1 → ×1.3, 3 → ×1.5).</summary>
        public static float CornerBoost(int level) => 1.2f + 0.1f * level;
        public const float SetBoost = 1.3f;
        /// <summary>장식 하나당 손님 수 보너스 (어디나 어울림 장식은 두 배).</summary>
        public static float AppealPerLevel(DecorDef d) => d.affinity == null ? 0.03f : 0.015f;
        /// <summary>치우면 돌려받는 비율.</summary>
        public const float RefundRate = 0.5f;

        /// <summary>
        /// 이 거리(m) 안의 두 진열대는 이웃 (세트 콤보). 벽 따라 1.3m 간격, 모서리 4-5는 1.8m,
        /// 창가↔7번 1.85m, 왼쪽 가장자리↔0번 2.1m, 옆칸 뒷벽↔0번 1.75m. 방 건너편끼리는 2.7m 이상이라 안 걸림.
        /// </summary>
        public const float NeighborDistance = 2.25f;
    }
}
