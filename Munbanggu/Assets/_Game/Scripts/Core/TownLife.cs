using System;
using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Core
{
    // ───────────── 2층 만화방 ─────────────

    /// <summary>만화 시리즈 (전부 이 게임에서 지어낸 작품).</summary>
    public sealed class ComicSeries
    {
        public readonly string id, name, blurb;
        public readonly int total, startReleased, pricePerVolume;
        public readonly float popularity;
        public readonly Color color;

        public ComicSeries(string id, string name, int total, int startReleased, int price, float popularity, Color color, string blurb)
        {
            this.id = id; this.name = name; this.total = total; this.startReleased = startReleased;
            pricePerVolume = price; this.popularity = popularity; this.color = color; this.blurb = blurb;
        }
    }

    public static class ComicTable
    {
        public static readonly ComicSeries[] All =
        {
            new ComicSeries("bungae", "우주소년 번개", 8, 3, 400, 1.3f, new Color(0.30f, 0.50f, 0.90f), "번개 머리 소년이 로켓 자전거로 은하계를 누비는 모험물"),
            new ComicSeries("ppyong", "꼬마 마법사 뿅뿅", 6, 2, 350, 1.0f, new Color(0.75f, 0.45f, 0.90f), "주문을 자꾸 틀리는 꼬마 마법사의 좌충우돌"),
            new ComicSeries("hana", "태권 소녀 하나", 7, 3, 400, 1.1f, new Color(0.95f, 0.40f, 0.40f), "동네 도장 막내가 전국 대회까지 가는 이야기"),
            new ComicSeries("dudu", "명탐정 두두", 6, 2, 450, 0.9f, new Color(0.40f, 0.65f, 0.45f), "안경 쓴 강아지 탐정의 골목 사건 수첩"),
            new ComicSeries("kwang", "로봇 대장 쾅", 8, 2, 500, 1.2f, new Color(0.85f, 0.65f, 0.20f), "고물상 로봇이 우주 침략자를 막아 낸다"),
            new ComicSeries("halmae", "요리왕 할머니", 5, 2, 300, 0.8f, new Color(0.95f, 0.75f, 0.55f), "시장 골목 할머니의 따뜻한 요리 대결"),
        };

        public static ComicSeries Find(string id) => Array.Find(All, s => s.id == id);

        /// <summary>
        /// day까지 나온 권 수. 매주 수요일(day % 7 == 3)마다 시리즈 하나씩 돌아가며 신간 한 권.
        /// </summary>
        public static int Released(ComicSeries s, int day)
        {
            int idx = Array.IndexOf(All, s);
            int n = s.startReleased;
            // 수요일들: 3, 10, 17, … (k번째 수요일 → 시리즈 k % 개수)
            for (int wed = 3, k = 0; wed <= day; wed += 7, k++)
                if (k % All.Length == idx) n++;
            return Mathf.Min(s.total, n);
        }

        /// <summary>오늘 신간이 나온 시리즈 (없으면 null).</summary>
        public static ComicSeries NewToday(int day)
        {
            if (day < 3 || (day - 3) % 7 != 0) return null;
            var s = All[((day - 3) / 7) % All.Length];
            return Released(s, day) > Released(s, day - 1) ? s : null;
        }

        public static readonly int[] Seats = { 4, 6, 9, 12 };
        public static readonly int[] SeatCosts = { 3000, 7000, 15000 }; // 다음 단계로
        public static readonly int[] Fees = { 100, 200, 300 };
        public static readonly string[] SeatNames = { "방석 4자리", "방석 6자리", "소파 9자리", "다락까지 12자리" };
    }

    [Serializable]
    public class ComicOwned
    {
        public string id;
        public int owned;
    }

    /// <summary>만화방 진행 (세이브됨).</summary>
    [Serializable]
    public class ComicState
    {
        public List<ComicOwned> books = new List<ComicOwned>();
        public int seatLevel;
        public int feeIndex = 1;               // 200원
        public List<string> completed = new List<string>(); // 완결까지 다 모은 시리즈 (보너스 한 번)
        public int lastReaders;
        public int newBoughtDay;               // 신간 나온 날 바로 산 날

        public int Owned(string id)
        {
            foreach (var b in books) if (b.id == id) return b.owned;
            return 0;
        }

        public void SetOwned(string id, int n)
        {
            foreach (var b in books) if (b.id == id) { b.owned = n; return; }
            books.Add(new ComicOwned { id = id, owned = n });
        }

        public int Seats => ComicTable.Seats[Mathf.Clamp(seatLevel, 0, ComicTable.Seats.Length - 1)];
        public int Fee => ComicTable.Fees[Mathf.Clamp(feeIndex, 0, ComicTable.Fees.Length - 1)];
    }

    // ───────────── 동네 대회 ─────────────

    public sealed class TournamentDef
    {
        public readonly string id, name, productId, line;
        public readonly int prize;   // 상품으로 나가는 재고 개수
        public TournamentDef(string id, string name, string productId, int prize, string line)
        {
            this.id = id; this.name = name; this.productId = productId; this.prize = prize; this.line = line;
        }
    }

    public static class TournamentTable
    {
        public static readonly TournamentDef[] All =
        {
            new TournamentDef("ddakji", "딱지치기 대회", "ddakji", 10, "딱! 딱! 평상 앞이 딱지 소리로 시끌벅적!"),
            new TournamentDef("marble", "구슬치기 대회", "marble", 10, "땅에 구멍 파 놓고 다들 엎드려서 구슬 튕기는 중!"),
            new TournamentDef("card", "카드 배틀 대회", "card", 10, "반짝이 카드 한 장에 함성이 터져요!"),
            new TournamentDef("yoyo", "요요 기술 대회", "yoyo", 3, "강아지 산책! 세계 일주! 기술 나올 때마다 박수!"),
            new TournamentDef("rubberband", "고무줄놀이 대회", "rubberband", 8, "무릎, 허리, 어깨… 고무줄이 점점 높아져요!"),
        };

        public static TournamentDef Find(string id) => Array.Find(All, t => t.id == id);
        public const int Cooldown = 4;      // 대회 사이 최소 날짜
        public const int Reputation = 3;    // 대회 한 번에 오르는 평판
        public const float SpawnMultiplier = 1.4f;
        public const float DemandMultiplier = 3f;

        static readonly string[] Winners = { "철수", "영수", "지영", "민호", "수진", "동훈", "은지", "태식", "미경", "준호" };
        public static string Winner(int day) => Winners[(day * 7 + 3) % Winners.Length];
    }

    // ───────────── 라이벌 가게 ─────────────

    /// <summary>동네마다 나타나는 라이벌 가게 (세이브됨).</summary>
    [Serializable]
    public class RivalState
    {
        public int district = -1;   // 어느 동네의 라이벌인지 (이사하면 새로)
        public bool active, closed;
        public int strength;        // 0~100: 손님을 뺏는 정도
        public int openedDay;
        public int lastDelta;       // 어제 바뀐 양 (아침 소식용)
        public int updatedDay;      // 마지막으로 하루 성장을 한 날 (다시 켜도 두 번 안 크게)
    }

    public static class RivalTable
    {
        public static readonly string[] Names = { "왕문구 할인마트", "단지 앞 24시 편의점", "메가 팬시 백화점" };
        /// <summary>그 동네로 온 뒤(또는 1일차부터) 며칠 뒤에 생기나.</summary>
        public const int AppearAfter = 11;
        public const int StartStrength = 40, DailyGrowth = 4;
        /// <summary>손님을 뺏는 최대 비율 (힘 100일 때).</summary>
        public const float MaxSteal = 0.35f;
        public static string Name(int district) => Names[Mathf.Clamp(district, 0, Names.Length - 1)];
    }

    // ───────────── 도감 ─────────────

    [Serializable]
    public class SoldCount
    {
        public string id;
        public int count;
    }

    /// <summary>도감 진행 (세이브됨).</summary>
    [Serializable]
    public class CollectionState
    {
        public List<SoldCount> sold = new List<SoldCount>();
        public List<string> customers = new List<string>(); // 만나 본 손님 종류
        public List<string> rewarded = new List<string>();  // 다 채워서 보상 받은 도감

        public int Sold(string id)
        {
            foreach (var s in sold) if (s.id == id) return s.count;
            return 0;
        }

        public void AddSold(string id)
        {
            foreach (var s in sold) if (s.id == id) { s.count++; return; }
            sold.Add(new SoldCount { id = id, count = 1 });
        }

        public bool AddCustomer(string key)
        {
            if (customers.Contains(key)) return false;
            customers.Add(key);
            return true;
        }
    }

    /// <summary>손님 도감 항목.</summary>
    public static class CustomerDex
    {
        public static readonly (string key, string name, string desc)[] Kinds =
        {
            ("normal", "동네 꼬마", "찾는 물건을 말하고 매대로 가요."),
            ("counsel", "고민 많은 아이", "\"할머니, 고민 있어요!\" 마음을 읽어 골라 줘요."),
            ("sneaky", "슬쩍 하는 아이", "보라 말풍선. 문 나가기 전에 잡아요."),
            ("credit", "외상 하는 아이", "\"내일 갚을게요…\" 믿어 줄까요?"),
            ("crying", "우는 아이", "돈을 잃어버렸대요. 달래 주면 친구를 데려와요."),
            ("comic", "만화방 손님", "2층에서 만화 보다 배고파 내려왔어요."),
            ("regular_cheolsu", "철수 (딱지 대장)", "딱지를 사랑하는 골목대장."),
            ("regular_younghee", "영희 (종이인형 소녀)", "옷 만드는 사람이 꿈."),
            ("regular_minsu", "민수 (부잣집 도련님)", "미니카는 언제나 제일 비싼 걸로."),
            ("adult_cheolsu", "어른이 된 철수", "체육 선생님이 되어 돌아왔어요."),
            ("adult_younghee", "어른이 된 영희", "디자이너가 되어 돌아왔어요."),
            ("adult_minsu", "어른이 된 민수", "장난감 회사 사장님이 되어 돌아왔어요."),
        };
    }

    // ───────────── 저장 묶음 ─────────────

    /// <summary>동네 살이 전체 (만화방·대회·라이벌·도감) 세이브.</summary>
    [Serializable]
    public class TownSave
    {
        public ComicState comic = new ComicState();
        public RivalState rival = new RivalState();
        public CollectionState collection = new CollectionState();
        public int lastTournamentDay = -99;
        public string tournamentToday;   // 오늘 연 대회 id (아침에 열고 저장했다 다시 켜도 유지)
        public int tournamentDay;
        public int districtSinceDay = 1; // 지금 동네로 온 날
        public List<string> grandmaSkills = new List<string>(); // 배운 할머니 스킬
        public DiscoverySave discovery = new DiscoverySave();   // 레시피·깜짝 손님·앨범·고양이
    }
}
