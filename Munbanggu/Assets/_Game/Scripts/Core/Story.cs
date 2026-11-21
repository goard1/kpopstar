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
    /// 「추억의 문방구」 이야기 (docs/11). 할아버지가 몰래 마련해 둔 골목 가게를 할머니가 열고,
    /// 할아버지의 수첩을 한 장씩 이뤄 가다가, 마지막에 편지를 연다.
    /// 대만판은 번역표에서 阿嬤·阿公·國小對面巷口·藍色鐵捲門으로 바꿔 쓴다.
    /// </summary>
    public static class StoryTable
    {
        public const string Prologue = "prologue", Expand = "ch_expand", Move1 = "ch_move1", Move2 = "ch_move2", Ending = "ending";

        static readonly Color Grandma = new Color(0.89f, 0.26f, 0.22f);
        static readonly Color Kid = new Color(0.18f, 0.42f, 0.80f);
        static readonly Color Girl = new Color(0.90f, 0.40f, 0.62f);
        static readonly Color Rich = new Color(0.20f, 0.62f, 0.36f);
        static readonly Color Grandpa = new Color(0.45f, 0.35f, 0.25f);

        /// <summary>할머니가 앉아 있는 계산대 옆 (가게 로컬).</summary>
        static readonly Vector3 GrandmaSpot = new Vector3(-3.4f, 0.8f, -1.2f);

        static StoryBeat N(StoryArt a, string text, string note = null) => new StoryBeat { art = a, text = text, note = note };
        static StoryBeat S(StoryArt a, string who, Color c, string text, string note = null) => new StoryBeat { art = a, speaker = who, color = c, text = text, note = note };

        public static readonly StoryChapter[] All =
        {
            new StoryChapter
            {
                id = Prologue, title = "프롤로그 · 파란 셔터",
                beats = new[]
                {
                    N(StoryArt.Black, "할아버지가 떠나고 한 달이 지났어요."),
                    N(StoryArt.Drawer, "할머니는 할아버지 책상 서랍을 정리하다가, 낡은 열쇠 하나를 찾았어요."),
                    S(StoryArt.Note, "할머니", Grandma, "…이 양반, 이게 다 뭐람?", "학교 앞 골목, 파란 셔터.\n열어 보면 알 거야."),
                    N(StoryArt.ShutterOpen, "쪽지에 적힌 골목 끝에, 정말 파란 셔터가 있었어요."),
                    S(StoryArt.ShopFront, "할머니", Grandma, "세상에… 문방구잖아."),
                    N(StoryArt.ShopFront, "텅 빈 진열대, 새로 칠한 간판. 할아버지가 몰래 준비해 둔 가게였어요."),
                    S(StoryArt.Photo, "할머니", Grandma, "젊었을 때 내가 그랬지. 아이들 웃음소리 나는 가게 하나 하고 싶다고… 그걸 여태 기억했구먼."),
                    S(StoryArt.ShopKid, "철수", Kid, "할머니! 여기 가게 해요? 언제 열어요?"),
                    new StoryBeat { art = StoryArt.World, speaker = "할머니", color = Grandma, focus = GrandmaSpot, zoom = 2.2f,
                        text = "그래… 오늘부터 열지 뭐. 할머니가 도매상 다녀올 테니 이따 친구들 데리고 와!" },
                },
            },
            new StoryChapter
            {
                id = Expand, title = "1장 · 할아버지의 수첩",
                beats = new[]
                {
                    N(StoryArt.Notebook, "가게를 넓히던 날, 선반 뒤에서 할아버지의 수첩이 나왔어요.",
                        "할아버지의 수첩 · 첫 장\n\n· 아이들 이름을 하나씩 불러 줄 것\n· 가게 앞에 평상을 놓아 줄 것\n· 2층엔 만화책을 잔뜩"),
                    S(StoryArt.Notebook, "할머니", Grandma, "이 양반, 하고 싶은 게 많았네. …그래, 하나씩 해 보자고.",
                        "할아버지의 수첩 · 첫 장\n\n· 아이들 이름을 하나씩 불러 줄 것\n· 가게 앞에 평상을 놓아 줄 것\n· 2층엔 만화책을 잔뜩"),
                },
            },
            new StoryChapter
            {
                id = Move1, title = "2장 · 이사 가는 날",
                beats = new[]
                {
                    N(StoryArt.Farewell, "이사 가는 날, 골목 아이들이 다 나왔어요."),
                    S(StoryArt.Farewell, "철수", Kid, "할머니, 이사 가도 놀러 갈게요! 딱지 들고!"),
                    S(StoryArt.Farewell, "영희", Girl, "새 가게에도 종이인형 꼭 팔아 주세요!"),
                    N(StoryArt.Notebook, "짐을 싸다 보니 수첩 둘째 장이 펼쳐져 있었어요.",
                        "할아버지의 수첩 · 둘째 장\n\n· 더 많은 아이들을 만날 것\n· 그래도 처음 골목은 잊지 말 것"),
                    S(StoryArt.Notebook, "할머니", Grandma, "잊긴 왜 잊어. 거기가 우리 시작인데.",
                        "할아버지의 수첩 · 둘째 장\n\n· 더 많은 아이들을 만날 것\n· 그래도 처음 골목은 잊지 말 것"),
                },
            },
            new StoryChapter
            {
                id = Move2, title = "3장 · 큰길가 문방구",
                beats = new[]
                {
                    N(StoryArt.Street, "큰길가 새 가게. 간판이 세 배는 커졌어요."),
                    N(StoryArt.Notebook, "수첩의 마지막 장에는 이렇게 적혀 있었어요.",
                        "할아버지의 수첩 · 마지막 장\n\n· 가장 큰 거리에서, 가장 따뜻한 가게를\n· 다 하면, 편지를 열어 볼 것"),
                    S(StoryArt.Notebook, "할머니", Grandma, "편지? 무슨 편지… 아, 서랍 맨 아래 그 봉투!",
                        "할아버지의 수첩 · 마지막 장\n\n· 가장 큰 거리에서, 가장 따뜻한 가게를\n· 다 하면, 편지를 열어 볼 것"),
                    S(StoryArt.Street, "할머니", Grandma, "아직은 안 열 거야. 이 가게를 끝까지 키운 다음에."),
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
                    S(StoryArt.Grownups, "철수", Kid, "할머니! 저예요, 딱지 대장 철수. 이제 체육 선생님이에요."),
                    S(StoryArt.Grownups, "영희", Girl, "저는 옷을 만들어요. 할머니가 골라 주신 종이인형 덕분에요."),
                    S(StoryArt.Grownups, "민수", Rich, "저는 장난감 회사를 해요. 여기가 제 첫 장난감 가게였어요."),
                    S(StoryArt.Grownups, "할머니", Grandma, "아이고… 다들 이렇게 컸구나."),
                    N(StoryArt.Letter, "그날 밤, 할머니는 서랍 맨 아래 봉투를 열었어요.", "여보,"),
                    S(StoryArt.Letter, "할아버지의 편지", Grandpa, "",
                        "여보,\n아이들 웃음소리 나는 가게를 하고 싶다던 당신 꿈… 내가 대신 이뤄 볼게.\n\n가게는 내가 마련해 둘 테니, 문은 당신이 열어요.\n아이들 이름 불러 주고, 덤도 하나씩 주고.\n\n당신이라면 분명 동네에서 제일 따뜻한 가게가 될 거야.\n— 영감이"),
                    S(StoryArt.Photo, "할머니", Grandma, "영감… 당신 말대로 됐어요. 아이들이 다 커서 찾아왔어요."),
                    S(StoryArt.ShopFront, "할머니", Grandma, "내일도 셔터 올려야지. 우리 가게에 올 아이들이 아직 많으니까."),
                    N(StoryArt.Credits, ""),
                    N(StoryArt.Black, "그리고 오늘도, 문방구 셔터가 올라갑니다.\n\n(계속 장사할 수 있어요 · 앨범에서 이야기를 다시 볼 수 있어요)"),
                },
            },
        };

        public static StoryChapter Find(string id) => Array.Find(All, c => c.id == id);

        /// <summary>크레딧 (위로 흐르는 글).</summary>
        public const string CreditsText =
            "추억의 문방구\n\n\n" +
            "이야기\n할머니와 할아버지, 그리고 골목 아이들\n\n" +
            "만든 이\n문방구 만드는 사람들\n\n" +
            "글꼴\nJua (SIL OFL)\njf open 粉圓 (SIL OFL)\n\n" +
            "소리\n코드로 만든 칩튠\n\n\n" +
            "그리고\n어릴 적 문방구를 기억하는\n모든 어른이들에게\n\n\n" +
            "고맙습니다";
    }
}
