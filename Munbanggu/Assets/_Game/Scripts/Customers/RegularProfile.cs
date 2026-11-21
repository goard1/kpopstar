using UnityEngine;

namespace Munbanggu.Customers
{
    /// <summary>단골 이야기 한 단계. 친밀도가 affection 이상이 되면 다음 계산 때 나온다.</summary>
    public struct StoryBeat
    {
        public int affection;
        public string line;
        public StoryBeat(int affection, string line) { this.affection = affection; this.line = line; }
    }

    /// <summary>단골 손님 정의 (코드 데이터). 추가할 땐 RegularRoster.All에 하나 더 넣으면 된다.</summary>
    public class RegularProfile
    {
        public string id;             // 세이브 키. 바꾸지 말 것
        public string name;
        public string title;          // 별명 (대화 패널에 표시)
        public string favoriteId;     // 좋아하는 상품 id
        public Color color;           // 대화 패널 초상 색

        [Header("방문 조건")]
        public int minDay = 1;               // 이 날부터 옴 (좋아하는 상품이 해금돼 있어야 함)
        public float visitChance = 0.7f;     // 하루에 올 확률
        public float windowStart = 0.1f;     // 영업 진행도(0~1) 중 오는 시간대
        public float windowEnd = 0.9f;
        public float favoriteChance = 0.8f;  // 좋아하는 상품을 찾을 확률 (나머지는 일반 손님처럼)

        [Header("성격")]
        public float patienceMultiplier = 1f;
        public int tip;                      // 계산할 때 얹어 주는 돈

        [Header("말풍선 (짧게! 10자 안팎)")]
        public string askFavorite;           // 좋아하는 상품 찾을 때
        public string happyLine;
        public string sadLine;

        [Header("이야기")]
        public StoryBeat[] story;            // [0] = 첫 만남 (입장 때), 나머지는 계산 때
        public string completeToast;         // 이야기를 다 보면 한 번

        [Header("성장 (docs/08)")]
        public string farewellLine;          // 이야기를 끝내고 며칠 뒤, 졸업하며 하는 인사
        public string adultLine;             // 한참 뒤 어른이 되어 다시 찾아왔을 때
        public string adultTitle;            // 어른이 된 뒤 별명

        /// <summary>항상 같은 외형이 나오도록 id로 만든 고정 seed.</summary>
        public int Seed
        {
            get
            {
                unchecked
                {
                    int h = 17;
                    foreach (char c in id) h = h * 31 + c;
                    return h & int.MaxValue;
                }
            }
        }
    }

    /// <summary>MVP 단골 3명.</summary>
    public static class RegularRoster
    {
        public static readonly RegularProfile[] All =
        {
            new RegularProfile
            {
                id = "cheolsu", name = "철수", title = "딱지 대장", favoriteId = "ddakji",
                color = new Color(0.30f, 0.50f, 0.85f),
                minDay = 1, visitChance = 0.75f, windowStart = 0.38f, windowEnd = 0.55f, // 하교 러시 때
                patienceMultiplier = 1f,
                askFavorite = "딱지 주세요!", happyLine = "역시 여기가 최고!", sadLine = "칫, 다음엔 꼭이요!",
                story = new[]
                {
                    new StoryBeat(0, "〔CALL〕, 저 철수예요! 우리 반에서 딱지 제일 잘 쳐요. 딱지 있어요?"),
                    new StoryBeat(3, "어제 딱지로 영수 거 다 땄어요! 근데 너무 쳐서 다 찢어졌어요…"),
                    new StoryBeat(6, "〔CALL〕네 딱지가 제일 잘 넘어가요. 무슨 비밀 있어요?"),
                    new StoryBeat(9, "다음 주에 동네 딱지 대회 해요. 저 꼭 우승할 거예요!"),
                    new StoryBeat(13, "대회 우승했어요! 이거 제가 접은 왕딱지예요. 가게에 걸어 주세요!"),
                },
                completeToast = "철수가 동네에 소문냈어요! 딱지 찾는 애들이 늘어요",
                farewellLine = "〔CALL〕… 저 이제 중학교 가요. 딱지는 졸업이래요. 그래도 왕딱지는 여기 걸어 두고 갈게요!",
                adultLine = "〔CALL〕! 저 철수예요, 기억하세요? 이제 체육 선생님 됐어요. 우리 반 애들 딱지 사 주러 왔어요. 거스름돈은 넣어 두세요!",
                adultTitle = "체육 선생님",
            },
            new RegularProfile
            {
                id = "younghee", name = "영희", title = "종이인형 소녀", favoriteId = "paperdoll",
                color = new Color(0.95f, 0.55f, 0.70f),
                minDay = 2, visitChance = 0.7f, windowStart = 0.1f, windowEnd = 0.3f, // 오전, 조용할 때
                patienceMultiplier = 1.3f, // 얌전히 오래 기다림
                askFavorite = "종이인형 있어요?", happyLine = "고맙습니다…!", sadLine = "없구나…",
                story = new[]
                {
                    new StoryBeat(0, "저… 안녕하세요. 종이인형 새로 나온 거 있어요?"),
                    new StoryBeat(3, "어제 산 종이인형, 옷이 열두 벌이나 있었어요. 너무 예뻐요."),
                    new StoryBeat(6, "저는 커서 옷 만드는 사람이 될 거예요. 비밀이에요!"),
                    new StoryBeat(9, "이거 제가 직접 그린 종이인형 옷이에요. 〔CALLG〕한테 제일 먼저 보여 줄게요."),
                    new StoryBeat(13, "미술대회에서 상 받았어요! 〔CALLG〕가 늘 예쁜 걸 갖다 놔 줘서예요."),
                },
                completeToast = "영희 친구들이 종이인형을 찾으러 와요!",
                farewellLine = "저 이사 가요… 서울로요. 〔CALLG〕 가게 종이인형은 하나도 안 버리고 다 가져갈 거예요.",
                adultLine = "〔CALLG〕, 저 영희예요. 정말 옷 만드는 사람이 됐어요! 첫 패션쇼 옷 무늬, 여기 종이인형에서 따왔어요.",
                adultTitle = "디자이너",
            },
            new RegularProfile
            {
                id = "minsu", name = "민수", title = "부잣집 도련님", favoriteId = "minicar",
                color = new Color(0.85f, 0.65f, 0.20f),
                minDay = 3, visitChance = 0.6f, windowStart = 0.62f, windowEnd = 0.85f, // 학원 끝나고
                patienceMultiplier = 0.75f, // 성격 급함
                tip = 200,                  // 용돈이 넉넉해서 거스름돈은 안 받음
                askFavorite = "미니카 주세요!", happyLine = "거스름돈은 됐어요!", sadLine = "에이, 시시해!",
                story = new[]
                {
                    new StoryBeat(0, "엄마가 용돈 줬어요. 여기서 제일 비싼 거 주세요!"),
                    new StoryBeat(3, "미니카 경주하면 맨날 제가 이겨요. 모터가 제일 좋은 거거든요."),
                    new StoryBeat(6, "근데… 혼자 경주하니까 별로 재미없어요."),
                    new StoryBeat(9, "철수랑 같이 골목에 미니카 트랙 만들었어요! 같이 하니까 훨씬 재밌어요."),
                    new StoryBeat(13, "〔CALL〕, 이거 철수랑 같이 쓴 감사 편지예요. 앞으로도 자주 올게요!"),
                },
                completeToast = "민수가 친구들을 데려와요! 미니카가 더 잘 팔려요",
                farewellLine = "아빠 회사 때문에 미국 간대요. 철수한테 제 미니카 트랙 맡겨 놨어요. 꼭 다시 올게요!",
                adultLine = "〔CALL〕, 민수예요! 장난감 회사 차렸어요. 첫 제품, 제일 먼저 이 가게에 놓고 싶어서 왔어요.",
                adultTitle = "장난감 회사 사장님",
            },
        };

        public static RegularProfile Find(string id)
        {
            foreach (var r in All) if (r.id == id) return r;
            return null;
        }
    }
}
