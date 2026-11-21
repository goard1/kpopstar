using System;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>컷신 한 장면의 그림 (UI로 그린 삽화, 또는 3D 가게를 그대로 보여 주기).</summary>
    public enum StoryArt
    {
        Black,        // 까만 화면 + 글
        Drawer,       // 할아버지 책상 서랍 · 열쇠 · 쪽지
        Note,         // 접힌 쪽지 (note 글)
        ShutterOpen,  // 파란 셔터가 스르륵 올라가는 가게 앞
        ShopFront,    // 셔터가 다 열린 가게 앞
        ShopKid,      // 가게 앞 + 기웃거리는 아이
        Photo,        // 젊은 할머니·할아버지 사진
        Notebook,     // 할아버지의 수첩 (note 글)
        Farewell,     // 골목 아이들이 손 흔드는 이사 날
        Street,       // 큰길가 새 가게
        Grownups,     // 어른이 된 단골 셋 + 할머니
        Letter,       // 할아버지의 편지 (note 글)
        World,        // 3D 가게 그대로 (위아래 검은 띠), focus로 카메라
        Credits,      // 크레딧 (위로 흐름)
    }

    public sealed class StoryBeat
    {
        public StoryArt art;
        public string speaker;   // null이면 해설
        public string text;
        public string note;      // 쪽지·수첩·편지에 적힌 글
        public Color color = new Color(0.89f, 0.26f, 0.22f);
        public Vector3? focus;   // World일 때 카메라가 볼 곳 (가게 로컬 좌표)
        public float zoom = 2f;
    }

    public sealed class StoryChapter
    {
        public string id, title;
        public StoryBeat[] beats;
    }

    /// <summary>
    /// 「추억의 문방구」 이야기 (docs/11). 할아버지가 할머니를 위해 몰래 마련한 골목 가게를, 두 분이 떠난 뒤 손주가 연다.
    /// 할아버지의 수첩을 한 장씩 이뤄 가다가, 마지막에 편지(와 할머니가 덧붙인 글)를 연다.
    /// 대만판은 번역표에서 阿嬤·阿公·國小對面巷口·藍色鐵捲門으로 바꿔 쓴다.
    /// </summary>
    public static class StoryTable
    {
        public const string Prologue = "prologue", Expand = "ch_expand", Move1 = "ch_move1", Move2 = "ch_move2", Ending = "ending";

        static readonly Color HeroColor = new Color(0.89f, 0.26f, 0.22f);   // 주인공 이름표
        static readonly Color Grandma = new Color(0.62f, 0.45f, 0.70f);
        static readonly Color Kid = new Color(0.18f, 0.42f, 0.80f);
        static readonly Color Girl = new Color(0.90f, 0.40f, 0.62f);
        static readonly Color Rich = new Color(0.20f, 0.62f, 0.36f);
        static readonly Color Grandpa = new Color(0.45f, 0.35f, 0.25f);

        /// <summary>주인공이 앉아 있는 계산대 옆 (가게 로컬).</summary>
        static readonly Vector3 OwnerSpot = new Vector3(-3.4f, 0.8f, -1.2f);

        static StoryBeat N(StoryArt a, string text, string note = null) => new StoryBeat { art = a, text = text, note = note };
        static StoryBeat S(StoryArt a, string who, Color c, string text, string note = null) => new StoryBeat { art = a, speaker = who, color = c, text = text, note = note };

        const string Me = Munbanggu.Core.Hero.NameTok;   // 주인공 (손주)

        public static readonly StoryChapter[] All =
        {
            new StoryChapter
            {
                id = Prologue, title = "프롤로그 · 파란 셔터",
                beats = new[]
                {
                    N(StoryArt.Black, "할아버지가 돌아가시고, 며칠 뒤…\n할머니도 할아버지를 따라 떠나셨어요."),
                    N(StoryArt.Drawer, "할머니 방을 정리하다가, 서랍 맨 위에서 낡은 열쇠와 쪽지를 찾았어요."),
                    S(StoryArt.Note, Me, HeroColor, "…할머니 글씨다.",
                        "우리 강아지에게\n\n할아버지가 준비해 둔 가게가 있단다.\n할미 대신 문 좀 열어 줄래?\n\n— 학교 앞 골목, 파란 셔터"),
                    N(StoryArt.ShutterOpen, "쪽지에 적힌 골목 끝에, 정말 파란 셔터가 있었어요."),
                    S(StoryArt.ShopFront, Me, HeroColor, "세상에… 문방구잖아."),
                    N(StoryArt.ShopFront, "텅 빈 진열대, 새로 칠한 간판. 할아버지가 할머니를 위해 몰래 준비한 가게였어요. 할머니는 끝내 이 문을 열지 못하셨고요."),
                    S(StoryArt.Photo, Me, HeroColor, "아이들 웃음소리 나는 가게 하나 하고 싶다… 할머니가 늘 하시던 말씀이었지."),
                    S(StoryArt.ShopKid, "철수", Kid, "〔CALL〕! 여기 가게 해요? 언제 열어요?"),
                    new StoryBeat { art = StoryArt.World, speaker = Me, color = HeroColor, focus = OwnerSpot, zoom = 2.2f,
                        text = "응… 오늘부터 열 거야. 도매상 다녀올 테니까 이따 친구들 데리고 와!" },
                },
            },
            new StoryChapter
            {
                id = Expand, title = "1장 · 할아버지의 수첩",
                beats = new[]
                {
                    N(StoryArt.Notebook, "가게를 넓히던 날, 선반 뒤에서 할아버지의 수첩이 나왔어요.", Note1),
                    S(StoryArt.Notebook, Me, HeroColor, "할아버지, 하고 싶은 게 많으셨네요. …제가 하나씩 해 볼게요.", Note1),
                },
            },
            new StoryChapter
            {
                id = Move1, title = "2장 · 이사 가는 날",
                beats = new[]
                {
                    N(StoryArt.Farewell, "이사 가는 날, 골목 아이들이 다 나왔어요."),
                    S(StoryArt.Farewell, "철수", Kid, "〔CALL〕, 이사 가도 놀러 갈게요! 딱지 들고!"),
                    S(StoryArt.Farewell, "영희", Girl, "새 가게에도 종이인형 꼭 팔아 주세요, 〔CALLG〕!"),
                    N(StoryArt.Notebook, "짐을 싸다 보니 수첩 둘째 장이 펼쳐져 있었어요.", Note2),
                    S(StoryArt.Notebook, Me, HeroColor, "잊을 리가요. 거기가 우리 가게의 시작인데.", Note2),
                },
            },
            new StoryChapter
            {
                id = Move2, title = "3장 · 큰길가 문방구",
                beats = new[]
                {
                    N(StoryArt.Street, "큰길가 새 가게. 간판이 세 배는 커졌어요."),
                    N(StoryArt.Notebook, "수첩의 마지막 장에는 이렇게 적혀 있었어요.", Note3),
                    S(StoryArt.Notebook, Me, HeroColor, "편지? …아, 서랍 맨 아래 그 봉투!", Note3),
                    S(StoryArt.Street, Me, HeroColor, "아직은 안 열래요. 이 가게를 끝까지 키운 다음에."),
                },
            },
            new StoryChapter
            {
                id = Ending, title = "마지막 장 · 할아버지의 편지",
                beats = new[]
                {
                    new StoryBeat { art = StoryArt.World, focus = new Vector3(0f, 1f, 0f), zoom = 1.2f,
                        text = "큰길 문방구를 마침내 끝까지 넓힌 날 저녁." },
                    N(StoryArt.Grownups, "가게 문을 닫으려는데, 낯익은 어른 셋이 들어왔어요."),
                    S(StoryArt.Grownups, "철수", Kid, "〔CALL〕! 저예요, 딱지 대장 철수. 이제 체육 선생님이에요."),
                    S(StoryArt.Grownups, "영희", Girl, "저는 옷을 만들어요. 〔CALLG〕가 골라 준 종이인형 덕분에요."),
                    S(StoryArt.Grownups, "민수", Rich, "저는 장난감 회사를 해요. 여기가 제 첫 장난감 가게였어요."),
                    S(StoryArt.Grownups, Me, HeroColor, "다들 이렇게 컸구나…"),
                    N(StoryArt.Letter, "그날 밤, 서랍 맨 아래 봉투를 열었어요. 할아버지가 할머니께 쓴 편지였어요.", "여보,"),
                    S(StoryArt.Letter, "할아버지의 편지", Grandpa, "", Letter),
                    N(StoryArt.Letter, "편지 뒤에, 할머니 글씨로 몇 줄이 더 적혀 있었어요.", "우리 강아지야,"),
                    S(StoryArt.Letter, "할머니의 글씨", Grandma, "", GrandmaNote),
                    S(StoryArt.Photo, Me, HeroColor, "할머니, 할아버지… 보고 계시죠? 아이들이 다 커서 찾아왔어요."),
                    S(StoryArt.ShopFront, Me, HeroColor, "내일도 셔터 올려야지. 우리 가게에 올 아이들이 아직 많으니까."),
                    N(StoryArt.Credits, ""),
                    N(StoryArt.Black, "그리고 오늘도, 문방구 셔터가 올라갑니다.\n\n(계속 장사할 수 있어요 · 앨범에서 이야기를 다시 볼 수 있어요)"),
                },
            },
        };

        const string Note1 = "할아버지의 수첩 · 첫 장\n\n· 아이들 이름을 하나씩 불러 줄 것\n· 가게 앞에 평상을 놓아 줄 것\n· 2층엔 만화책을 잔뜩";
        const string Note2 = "할아버지의 수첩 · 둘째 장\n\n· 더 많은 아이들을 만날 것\n· 그래도 처음 골목은 잊지 말 것";
        const string Note3 = "할아버지의 수첩 · 마지막 장\n\n· 가장 큰 거리에서, 가장 따뜻한 가게를\n· 다 하면, 편지를 열어 볼 것";
        const string Letter = "여보,\n아이들 웃음소리 나는 가게를 하고 싶다던 당신 꿈… 내가 대신 이뤄 볼게.\n\n가게는 내가 마련해 둘 테니, 문은 당신이 열어요.\n아이들 이름 불러 주고, 덤도 하나씩 주고.\n\n당신이라면 분명 동네에서 제일 따뜻한 가게가 될 거야.\n— 영감이";
        const string GrandmaNote = "우리 강아지야,\n할미는 끝내 이 문을 못 열었지만\n네가 대신 열어 줘서 고맙다.\n\n할아버지 꿈도, 할미 꿈도\n네 덕분에 다 이뤘구나.\n\n밥 잘 챙겨 먹고.\n— 할미가";

        public static StoryChapter Find(string id) => Array.Find(All, c => c.id == id);

        /// <summary>크레딧 (위로 흐르는 글).</summary>
        public const string CreditsText =
            "추억의 문방구\n\n\n" +
            "이야기\n할아버지와 할머니,\n그 가게를 연 손주, 그리고 골목 아이들\n\n" +
            "만든 이\n문방구 만드는 사람들\n\n" +
            "글꼴\nJua (SIL OFL)\njf open 粉圓 (SIL OFL)\n\n" +
            "소리\n코드로 만든 칩튠\n\n\n" +
            "그리고\n어릴 적 문방구를 기억하는\n모든 어른이들에게\n\n\n" +
            "고맙습니다";
    }
}
