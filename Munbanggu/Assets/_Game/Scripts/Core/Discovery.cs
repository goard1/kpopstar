using System;
using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Core
{
    // ───────────── 숨은 레시피 (작업대) ─────────────

    /// <summary>재료 두 개 → 특제 상품. 만들어 보기 전엔 ???.</summary>
    public sealed class RecipeDef
    {
        public readonly string id, a, b, name, blurb, hint;
        public readonly Color color;
        public RecipeDef(string id, string a, string b, string name, Color color, string blurb, string hint)
        {
            this.id = id; this.a = a; this.b = b; this.name = name; this.color = color; this.blurb = blurb; this.hint = hint;
        }
        public bool Matches(string x, string y) => (x == a && y == b) || (x == b && y == a);
        /// <summary>도감·카탈로그에 쓰이는 특제 상품 id.</summary>
        public string ProductId => "sp_" + id;
    }

    public static class RecipeTable
    {
        public const int Need = 3;     // 재료마다 창고에서 쓰는 개수
        public const int Yield = 5;    // 나오는 특제 상품 개수

        public static readonly RecipeDef[] All =
        {
            new RecipeDef("shinyddakji", "ddakji", "sticker", "반짝 딱지", new Color(1f, 0.82f, 0.3f), "스티커로 반짝반짝 꾸민 딱지. 치면 무지개가 번쩍!", "철수: \"딱지에 반짝이 붙이면 세질 것 같아요!\""),
            new RecipeDef("sweetjjondeugi", "jjondeugi", "beercandy", "꿀 쫀드기", new Color(1f, 0.62f, 0.36f), "사탕을 녹여 바른 달콤 쫀드기. 연탄불 향이 솔솔.", "동네 소문: 사탕이랑 쫀드기를 같이 구우면…?"),
            new RecipeDef("princessdoll", "paperdoll", "sticker", "공주 종이인형", new Color(1f, 0.66f, 0.86f), "보석 스티커 드레스를 입은 종이인형.", "영희: \"종이인형 옷에 스티커 붙이면 공주 같아요\""),
            new RecipeDef("rainbowmarble", "marble", "bubble", "무지개 구슬", new Color(0.7f, 0.85f, 1f), "비눗방울 물에 담갔더니 무지갯빛이 도는 구슬.", "만화 「꼬마 마법사 뿅뿅」 3권: 비눗방울 + 구슬 = 무지개 마법!"),
            new RecipeDef("studyset", "pencil", "eraser", "새 학기 필기 세트", new Color(0.55f, 0.82f, 0.6f), "캐릭터 연필과 향기 지우개를 예쁘게 묶은 세트.", "엄마들 소문: 연필이랑 지우개를 묶어 팔면 선물로 딱!"),
            new RecipeDef("squishyball", "bouncyball", "slime", "말랑 탱탱볼", new Color(0.55f, 0.95f, 0.7f), "액체괴물을 채워 말랑말랑 튀는 공.", "만화 「로봇 대장 쾅」: 말랑한 공은 안 깨진다!"),
            new RecipeDef("holocard", "card", "sticker", "홀로그램 카드", new Color(0.8f, 0.7f, 1f), "빛에 따라 그림이 바뀌는 반짝 카드. 반 아이들이 줄을 선다.", "민수: \"카드에 반짝이 스티커 붙이면 진짜 희귀 카드 같아요\""),
            new RecipeDef("snackgift", "apollo", "ssaldaerong", "과자 종합 선물", new Color(1f, 0.85f, 0.45f), "아폴로·쌀대롱을 한 봉지에! 소풍 필수품.", "동네 소문: 소풍엔 과자를 섞어 담은 봉지가 최고래요"),
            new RecipeDef("turbocar", "minicar", "yoyo", "터보 미니카", new Color(1f, 0.4f, 0.4f), "요요 줄을 감아 쏘는 터보 미니카. 골목 최고 속도!", "민수: \"요요 줄로 미니카를 쏘면 어떨까요?\""),
            new RecipeDef("bubblegun", "watergun", "bubble", "버블 물총", new Color(0.55f, 0.85f, 1f), "쏘면 비눗방울이 펑펑! 여름 골목의 주인공.", "만화 「태권 소녀 하나」 2권: 물총에 비눗물을 넣었더니…"),
        };

        public static RecipeDef Find(string a, string b) => Array.Find(All, r => r.Matches(a, b));
        public static RecipeDef ById(string id) => Array.Find(All, r => r.id == id);
    }

    // ───────────── 깜짝 손님 ─────────────

    public sealed class SpecialVisitorDef
    {
        public readonly string id, name, intro, ask;
        public readonly int minDay, minReputation;
        public readonly bool rainOnly;
        public SpecialVisitorDef(string id, string name, int minDay, int minReputation, bool rainOnly, string intro, string ask)
        {
            this.id = id; this.name = name; this.minDay = minDay; this.minReputation = minReputation;
            this.rainOnly = rainOnly; this.intro = intro; this.ask = ask;
        }
        public string DexKey => "special_" + id;
    }

    public static class SpecialVisitors
    {
        public static readonly SpecialVisitorDef[] All =
        {
            new SpecialVisitorDef("banjang", "반장 아저씨", 3, 0, false, "동네 반장 아저씨가 왔어요! 반상회 선물을 한꺼번에 사 가려나 봐요.", "반상회용으로 왕창 주세요!"),
            new SpecialVisitorDef("reporter", "TV 리포터", 5, 55, false, "\"동네 명물 문방구\" 취재 왔대요! 카메라가 돌아가요~ (오늘 평판 +5, 손님 +30%)", "여기가 그 유명한 문방구죠?"),
            new SpecialVisitorDef("transfer", "전학생", 4, 0, false, "서울에서 전학 온 아이래요. 처음 보는 신기한 물건 이야기를 해 줬어요!", "서울에선 이런 거 팔던데…"),
            new SpecialVisitorDef("dokkaebi", "도깨비 꼬마", 6, 0, true, "비 오는 날에만 나타난다는 도깨비 꼬마?! 뿔이 쫑긋!", "뚝딱! 이거 줘!"),
            new SpecialVisitorDef("neighbor", "옆집 할아버지", 8, 0, false, "옆집 할아버지가 마실 오셨어요. 옛날 사진을 한 장 들고 오셨네요.", "허허, 이 가게도 참 오래됐지"),
            new SpecialVisitorDef("mayor", "구청장님", 12, 75, false, "구청장님이 직접 오셨어요! 모범 가게를 뽑는대요.", "아이들이 좋아하는 가게라고 소문이 났더군요"),
        };

        public static SpecialVisitorDef Find(string id) => Array.Find(All, v => v.id == id);
    }

    // ───────────── 할머니 추억 앨범 ─────────────

    public sealed class AlbumPage
    {
        public readonly string id, title, line;
        public AlbumPage(string id, string title, string line) { this.id = id; this.title = title; this.line = line; }
    }

    public static class Album
    {
        public static readonly AlbumPage[] Pages =
        {
            new AlbumPage("first_day", "첫 장사", "셔터를 올리던 손이 떨리더라. 첫 손님이 쫀드기 하나 사 갔지."),
            new AlbumPage("first_regular", "첫 단골", "매일 오는 아이가 생겼어. 이름을 불러 주니 얼마나 좋아하던지."),
            new AlbumPage("first_perfect", "마음을 읽다", "\"〔CALL〕, 어떻게 알았어요?\" 아이 눈이 동그래졌지."),
            new AlbumPage("first_combo", "진열의 비밀", "옆에 나란히 놓았더니 둘 다 잘 팔리네. 물건에도 짝꿍이 있나 봐."),
            new AlbumPage("rainy", "비 오는 날의 문방구", "비가 오면 아이들이 처마 밑에 모여 딱지를 쳤어."),
            new AlbumPage("first_expand", "가게가 넓어진 날", "옆칸 벽을 허물던 날, 아이들이 구경하느라 장사가 안 됐어."),
            new AlbumPage("tournament", "평상 위의 함성", "딱지 한 장에 온 동네가 들썩이더라."),
            new AlbumPage("special_visitor", "깜짝 손님", "문방구엔 별별 손님이 다 와. 그래서 재밌지."),
            new AlbumPage("first_recipe", "할머니표 특제", "이것저것 섞어 보다가 그만 대박이 났네!"),
            new AlbumPage("cat", "치즈가 온 날", "문 앞에 노란 고양이가 앉아 있었어. 그날부터 우리 식구야."),
            new AlbumPage("comic_complete", "만화 완결", "마지막 권을 덮은 아이들이 박수를 쳤어."),
            new AlbumPage("comic_legend", "할머니의 문방구", "만화 속 할머니가 꼭 우리 할머니 같았어. 아이들이 그 만화를 들고 와서 웃더라."),
            new AlbumPage("graduation", "첫 졸업", "\"〔CALL〕, 안녕히 계세요.\" 가방 멘 뒷모습이 어찌나 컸는지."),
            new AlbumPage("rival_closed", "골목을 지키다", "큰 가게가 와도 아이들은 우리 가게로 왔어."),
            new AlbumPage("first_move", "이사 가는 날", "정든 골목을 떠나며 셔터를 쓰다듬었지."),
            new AlbumPage("adult", "어른이 되어 돌아온 아이", "그 꼬맹이가 양복을 입고 왔더라. 눈물이 다 나더라."),
            new AlbumPage("day30", "한 달", "하루하루 쌓이니 벌써 한 달이네."),
            new AlbumPage("ending", "할아버지의 편지",
                "\"여보, 아이들 웃음소리 나는 가게를 하고 싶다던 당신 꿈… 내가 대신 이뤄 볼게.\"\n" +
                "할아버지가 할머니께 남긴 편지. 이 문방구는 두 분의 꿈이었어.\n" +
                "매일 너희가 와 준 덕분에, 그 꿈이 이뤄졌어. 고마워."),
        };

        public static AlbumPage Find(string id) => Array.Find(Pages, p => p.id == id);
        /// <summary>엔딩 장을 빼고 이만큼 모으면 마지막 장이 열린다.</summary>
        public static int EndingNeeds => Pages.Length - 1;
    }

    // ───────────── 고양이 「치즈」 + 선물 룰렛 ─────────────

    [Serializable]
    public class CatState
    {
        public bool met, adopted;
        public int affection;      // 밥 준 횟수
        public int lastFedDay;
        public int lastGiftDay;
    }

    public sealed class RouletteSlice
    {
        public readonly string label;
        public readonly int weight;
        public readonly Color color;
        public RouletteSlice(string label, int weight, Color color) { this.label = label; this.weight = weight; this.color = color; }
    }

    public static class CatRules
    {
        public const int FirstDay = 5;       // 이날부터 문 앞에 나타남
        public const int FeedCost = 300;
        public const int AdoptAfter = 3;     // 밥을 이만큼 주면 가게 식구가 됨

        public static readonly RouletteSlice[] Slices =
        {
            new RouletteSlice("동전\n500원", 25, new Color(1f, 0.88f, 0.5f)),
            new RouletteSlice("동전\n1,500원", 15, new Color(1f, 0.75f, 0.45f)),
            new RouletteSlice("황금 동전\n5,000원", 4, new Color(1f, 0.6f, 0.2f)),
            new RouletteSlice("직원 아이템\n(보통)", 11, new Color(0.6f, 0.82f, 1f)),
            new RouletteSlice("직원 아이템\n(희귀)", 4, new Color(0.5f, 0.6f, 1f)),
            new RouletteSlice("평판\n+3", 12, new Color(0.65f, 0.9f, 0.65f)),
            new RouletteSlice("오늘 손님\n+20%", 11, new Color(1f, 0.7f, 0.82f)),
            new RouletteSlice("레시피\n힌트", 10, new Color(0.82f, 0.7f, 1f)),
            new RouletteSlice("생선뼈\n(꽝)", 8, new Color(0.82f, 0.82f, 0.86f)),
        };

        /// <summary>가중치대로 한 칸 (친해질수록 꽝이 줄어듦).</summary>
        public static int Pick(System.Random rng, int affection)
        {
            int total = 0;
            int[] w = new int[Slices.Length];
            for (int i = 0; i < Slices.Length; i++)
            {
                w[i] = Slices[i].weight;
                if (i == Slices.Length - 1) w[i] = Mathf.Max(2, w[i] - affection / 3);
                total += w[i];
            }
            int r = rng.Next(total);
            for (int i = 0; i < w.Length; i++) { if (r < w[i]) return i; r -= w[i]; }
            return 0;
        }
    }

    /// <summary>발견 요소 저장 (레시피·깜짝 손님·앨범·고양이).</summary>
    [Serializable]
    public class DiscoverySave
    {
        public List<string> recipes = new List<string>();      // 발견한 레시피 id
        public List<string> hints = new List<string>();        // 힌트가 열린 레시피 id
        public List<string> album = new List<string>();        // 모은 앨범 장 id
        public List<string> visitorsMet = new List<string>();
        public CatState cat = new CatState();
        public List<SoldCount> early = new List<SoldCount>();  // 전학생 덕에 일찍 들어오는 상품 (id, 그날부터)
    }
}
