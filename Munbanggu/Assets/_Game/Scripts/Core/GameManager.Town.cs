using System.Collections.Generic;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.UI;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>
    /// 동네 살이 (docs/08): 학교 달력·날씨, 동네 대회, 라이벌 가게, 2층 만화방 운영, 단골 성장, 도감.
    /// GameManager 본체에서는 아침(TownMorning)·문 열기(TownOpen)·영업 중(TickTown)·정산(TownSettle)만 부른다.
    /// </summary>
    public partial class GameManager
    {
        public TownSave Town { get; private set; } = new TownSave();
        public DayInfo Today { get; private set; } = SchoolCalendar.For(1);
        public DayInfo Tomorrow => SchoolCalendar.For(Day + 1);

        /// <summary>어른이 되어 찾아온 단골이 더 내고 가는 돈.</summary>
        public const int AdultGift = 5000;

        string RegularTitle(RegularProfile r) => $"{r.name} · {r.title} ({regulars.Grade(r, Day)}학년)";

        void AddReputation(int delta)
        {
            if (delta == 0) return;
            Reputation = Mathf.Clamp(Reputation + delta, 0, 100);
            Stats.reputationDelta += delta;
        }

        /// <summary>달력·대회·만화방(간식)이 손님이 찾는 물건에 주는 배율.</summary>
        float TownDemand(ProductData p)
        {
            float m = Today.Demand(p);
            var t = TournamentToday;
            if (t != null && p != null && p.id == t.productId) m *= TournamentTable.DemandMultiplier;
            return m;
        }

        /// <summary>달력·대회·라이벌이 손님 수에 주는 배율.</summary>
        float TownSpawnMultiplier => Today.SpawnMultiplier * (TournamentToday != null ? TournamentTable.SpawnMultiplier : 1f) * RivalSpawnMultiplier;

        // ───────────── 하루 흐름 ─────────────

        void TownMorning()
        {
            Today = SchoolCalendar.For(Day);
            UpdateRival();
            hasComicReaders = false;
            RefreshComicReaders(0);
        }

        void TownMorningNews()
        {
            var lines = new List<string> { $"{Day}일차 {Today.WeekName}요일 · {Today.WeatherIcon} · {Today.Title}" };
            if (!string.IsNullOrEmpty(Today.Hint)) lines.Add(Today.Hint);
            var tm = Tomorrow;
            if (tm.kind != SchoolDay.Normal || tm.weather == Weather.Rain)
                lines.Add($"내일은 {tm.Title}{(tm.weather == Weather.Rain ? ", 비 소식" : "")}! 미리 물건을 챙겨 둬.");
            Dialogue.Show("오늘의 달력", string.Join("\n", lines), UIKit.Blue, "달");

            foreach (var line in rivalNews) Dialogue.Show(RivalTable.Name(District), line, UIKit.Red, "적");
            rivalNews.Clear();

            if (HasComicRoom)
            {
                var fresh = ComicTable.NewToday(Day);
                if (fresh != null)
                    Dialogue.Show("만화 총판", $"「{fresh.name}」 {ComicTable.Released(fresh, Day)}권 신간이 나왔어요! 오늘 들여놓으면 아이들이 줄을 설 거예요. (동네 → 만화방)", UIKit.Accent, "만");
            }
        }

        void TownOpen()
        {
            tournamentResolved = false;
            PlanComicDay();
            if (TournamentToday != null) Hud.Toast($"오늘은 {TournamentToday.name}! 아이들이 몰려와요");
        }

        void TickTown()
        {
            TickTournament();
            TickComic();
        }

        /// <summary>정산 때: 만화방 수입(들어온 돈 반환), 대회 평판, 라이벌 힘 변화, 도감 보상.</summary>
        int TownSettle()
        {
            int income = SettleComic();
            SettleRivalDay();
            CheckCollectionRewards();
            return income;
        }

        void AddTownSettlementLines(List<SettlementLine> lines)
        {
            lines.Add(SettlementLine.TextLine($"{Today.WeekName}요일 · {Today.WeatherIcon} · {Today.Title}", UIKit.Muted));
            if (Stats.tournament != null) lines.Add(SettlementLine.TextLine($"{Stats.tournament} 개최! 평판 +{TournamentTable.Reputation}", UIKit.Green));
            if (Stats.comicReaders > 0) lines.Add(SettlementLine.Count("2층 만화방 손님", Stats.comicReaders));
            if (Town.rival.active && !Town.rival.closed)
                lines.Add(SettlementLine.TextLine($"{RivalTable.Name(District)} 기세 {Town.rival.strength}", Town.rival.strength >= 60 ? UIKit.Red : UIKit.Muted));
            var tm = Tomorrow;
            if (tm.kind != SchoolDay.Normal || tm.weather == Weather.Rain)
                lines.Add(SettlementLine.Tip($"내일: {tm.Title}{(tm.weather == Weather.Rain ? " · 비" : "")} — {(tm.kind == SchoolDay.PicnicEve || tm.kind == SchoolDay.SportsEve ? "간식을 넉넉히!" : tm.kind == SchoolDay.ExamWeek ? "학용품을 넉넉히!" : tm.weather == Weather.Rain ? "실내 놀이를 넉넉히!" : "미리 준비해요")}"));
        }

        // ───────────── 동네 대회 ─────────────

        bool tournamentResolved;

        public TournamentDef TournamentToday => Town.tournamentDay == Day ? TournamentTable.Find(Town.tournamentToday) : null;

        /// <summary>평상이 있거나(학교 앞) 이사한 동네(놀이터·광장)면 대회를 열 수 있다.</summary>
        public bool TournamentsUnlocked => District > 0 || ExpansionTable.Has(District, Expansion, ExpansionFeature.Pyeongsang);

        public string TournamentBlocker(TournamentDef t)
        {
            if (!TournamentsUnlocked) return "가게 앞 평상을 지으면 열 수 있어요";
            if (TournamentToday != null) return $"오늘은 이미 {TournamentToday.name}!";
            int wait = Town.lastTournamentDay + TournamentTable.Cooldown - Day;
            if (wait > 0) return $"{wait}일 뒤에 또 열 수 있어요";
            var p = Catalog.Find(t.productId);
            if (p == null || !IsUnlocked(p)) return "아직 안 들어온 물건이에요";
            if (Stock(p) < t.prize) return $"상품으로 {p.displayName} {t.prize}개가 창고에 있어야 해요 (지금 {Stock(p)}개)";
            return null;
        }

        public bool HoldTournament(string id)
        {
            var t = TournamentTable.Find(id);
            if (t == null || !MorningOnly("대회 열기")) return false;
            string b = TournamentBlocker(t);
            if (b != null) { Fail(b); return false; }
            var p = Catalog.Find(t.productId);
            RemoveStock(p, t.prize);
            Town.tournamentToday = id;
            Town.tournamentDay = Day;
            Town.lastTournamentDay = Day;
            Hud.Toast($"{t.name} 열기로 했어요! 상품 {p.displayName} {t.prize}개");
            Dialogue.Show("동네 대회", $"오늘 가게 앞에서 {t.name}! 아이들이 {p.displayName} 연습하려고 잔뜩 사 갈 거야. 매대에 넉넉히 채워 둬.", UIKit.Green, "대");
            Save();
            Notify();
            return true;
        }

        void TickTournament()
        {
            var t = TournamentToday;
            if (t == null || tournamentResolved || OpenProgress < 0.65f) return;
            tournamentResolved = true;
            Stats.tournament = t.name;
            AddReputation(TournamentTable.Reputation);
            rivalHit += 10;
            string winner = TournamentTable.Winner(Day);
            // 단골이 좋아하는 종목이면 단골이 우승
            foreach (var r in RegularRoster.All)
                if (r.favoriteId == t.productId && regulars.StateOf(r).visits > 0 && !regulars.StateOf(r).graduated) winner = r.name;
            Dialogue.Show(t.name, $"{t.line}\n우승은 {winner}! 동네에 할머니 가게 소문이 쫙 났어요. (평판 +{TournamentTable.Reputation})", UIKit.Green, "★");
            GameEvents.RaiseRushStarted();
            ScheduleFriends(3);
            Notify();
        }

        // ───────────── 라이벌 가게 ─────────────

        readonly List<string> rivalNews = new List<string>();
        int rivalHit; // 오늘 라이벌을 깎은 양 (상담 딱 맞음, 대회, 콤보…)

        float RivalSpawnMultiplier
        {
            get
            {
                var r = Town.rival;
                if (!r.active || r.closed || r.district != District) return 1f;
                return (1f - RivalTable.MaxSteal * r.strength / 100f) * (Day % 5 == 0 ? 0.85f : 1f); // 5일마다 할인 행사
            }
        }

        /// <summary>아침: 라이벌 등장·성장 (하루 한 번만).</summary>
        void UpdateRival()
        {
            var r = Town.rival;
            if (r.district != District)
            {
                // 새 동네: 라이벌 처음부터
                Town.rival = r = new RivalState { district = District };
            }
            if (r.closed) return;
            if (!r.active)
            {
                if (Day >= Town.districtSinceDay + RivalTable.AppearAfter)
                {
                    r.active = true;
                    r.openedDay = Day;
                    r.strength = RivalTable.StartStrength;
                    r.updatedDay = Day;
                    rivalNews.Add($"길 건너에 「{RivalTable.Name(District)}」이(가) 문을 열었어요! 손님을 뺏어 가요.\n상담을 딱 맞히고, 대회를 열고, 콤보를 만들면 아이들이 다시 돌아와요.");
                }
                return;
            }
            if (r.updatedDay == Day) return;
            r.updatedDay = Day;
            int before = r.strength;
            r.strength = Mathf.Clamp(r.strength + RivalTable.DailyGrowth, 0, 100);
            if (Day % 5 == 0) rivalNews.Add("오늘 「" + RivalTable.Name(District) + "」 할인 행사래요… 손님이 좀 줄겠어요.");
            r.lastDelta = r.strength - before;
        }

        /// <summary>정산: 오늘 한 일만큼 라이벌 기세를 꺾는다.</summary>
        void SettleRivalDay()
        {
            var r = Town.rival;
            if (!r.active || r.closed || r.district != District) { rivalHit = 0; return; }
            int hit = rivalHit + Stats.counselPerfect * 2 + Mathf.Min(5, slotCombo.Count) + (Reputation >= 80 ? 3 : 0);
            rivalHit = 0;
            r.strength = Mathf.Max(0, r.strength - hit);
            if (r.strength <= 0)
            {
                r.closed = true;
                AddReputation(10);
                Dialogue.Show("동네 소식", $"「{RivalTable.Name(District)}」이(가) 문을 닫았어요! 아이들이 다 할머니 가게로 돌아왔어요. (평판 +10)", UIKit.Green, "★");
            }
        }


        // ───────────── 2층 만화방 ─────────────

        public bool HasComicRoom => ExpansionTable.Has(District, Expansion, ExpansionFeature.ComicRoom);

        int comicReadersToday, comicReadersShown, comicSpillLeft;
        float comicSpillAcc;
        bool comicEventDone, hasComicReaders;
        float comicEventAt;
        GameObject comicReadersGo;
        CustomerBubble comicBubble;
        Transform comicAnchor;

        /// <summary>만화방 매력: 가진 권 수 × 인기 (완결 시리즈 ×1.5, 오늘 신간을 샀으면 ×1.5).</summary>
        public float ComicAppeal()
        {
            float a = 0f;
            foreach (var s in ComicTable.All)
            {
                int owned = Town.comic.Owned(s.id);
                if (owned <= 0) continue;
                float v = owned * s.popularity;
                if (owned >= s.total) v *= 1.5f;
                a += v;
            }
            if (Town.comic.newBoughtDay == Day) a *= 1.5f;
            return a;
        }

        /// <summary>오늘 만화방에 올 아이 수 예상 (자리 수까지).</summary>
        public int PlannedComicReaders()
        {
            if (!HasComicRoom) return 0;
            float appeal = ComicAppeal();
            if (appeal <= 0f) return 0;
            float fee = Town.comic.feeIndex == 0 ? 1.3f : Town.comic.feeIndex == 2 ? 0.7f : 1f;
            float expected = Mathf.Max(10, Stats.visitors > 0 ? Stats.visitors : Town.comic.lastReaders * 4);
            float demand = (2f + expected * 0.15f) * Today.ComicMultiplier * Mathf.Min(1.6f, 0.4f + appeal / 12f) * fee;
            return Mathf.Clamp(Mathf.RoundToInt(demand), 0, Town.comic.Seats);
        }

        void PlanComicDay()
        {
            comicReadersToday = PlannedComicReaders();
            comicReadersShown = -1;
            comicSpillLeft = Mathf.RoundToInt(comicReadersToday * 0.6f);
            comicSpillAcc = 0f;
            comicEventDone = comicReadersToday < 3 || Random.value > 0.4f;
            comicEventAt = Random.Range(0.35f, 0.7f);
        }

        void TickComic()
        {
            if (!HasComicRoom) { RefreshComicReaders(0); return; }
            // 진행도에 따라 자리가 차고, 끝 무렵 빠진다
            float p = OpenProgress;
            int now = Mathf.RoundToInt(comicReadersToday * Mathf.Clamp01(p * 2f) * (p > 0.9f ? Mathf.Clamp01((1f - p) * 10f) : 1f));
            RefreshComicReaders(now);

            // 만화 보다 배고파서 내려오는 아이 (간식 찾음)
            if (comicSpillLeft > 0 && p > 0.15f && p < 0.9f)
            {
                comicSpillAcc += Time.deltaTime * comicSpillLeft / Mathf.Max(10f, OpenDuration * (0.9f - p));
                if (comicSpillAcc >= 1f && customers.Count < maxCustomersInShop + 2 * ExpansionTable.Bays(District, Expansion))
                {
                    comicSpillAcc = 0f;
                    comicSpillLeft--;
                    var snack = RandomStockedSnack();
                    if (snack != null)
                        SpawnCustomer(null, CustomerKind.Normal, snack, $"만화 보다 배고파요! {snack.displayName} 있어요?", "comic");
                }
            }

            // 만화방 사건 (하루 한 번쯤)
            if (!comicEventDone && p >= comicEventAt && !Hud.IsChoiceOpen)
            {
                comicEventDone = true;
                ComicIncident();
            }
        }

        ProductData RandomStockedSnack()
        {
            var list = new List<ProductData>();
            foreach (var s in Layout.Slots)
                if (s != null && !s.IsBoard && s.Product != null && s.Count > 0 && s.Product.category == ProductCategory.Snack) list.Add(s.Product);
            if (list.Count > 0) return list[Random.Range(0, list.Count)];
            foreach (var p in Catalog.products)
                if (IsUnlocked(p) && p.category == ProductCategory.Snack) list.Add(p);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
        }

        void ComicIncident()
        {
            // 가진 책 중 하나
            ComicSeries book = null;
            foreach (var s in ComicTable.All) if (Town.comic.Owned(s.id) > 0 && (book == null || Random.value < 0.5f)) book = s;
            if (book == null) return;
            if (Random.value < 0.5f)
            {
                Hud.ShowChoice("만화책에 낙서!", $"만화방 아이가 「{book.name}」에 크레파스로 낙서를 했어요.\n\n혼내면 책값을 받지만 아이가 시무룩해져요.\n괜찮다고 하면 그 책은 못 쓰게 돼요.",
                    $"책값 받기 (+{UIKit.Won(book.pricePerVolume)})", "괜찮다고 하기",
                    yes =>
                    {
                        if (yes) { Money += book.pricePerVolume; Stats.revenue += book.pricePerVolume; AddReputation(-1); Hud.Toast("책값은 받았지만 아이가 시무룩해요 (평판 -1)"); }
                        else { Town.comic.SetOwned(book.id, Mathf.Max(0, Town.comic.Owned(book.id) - 1)); AddReputation(2); Hud.Toast($"「{book.name}」 한 권을 잃었지만 동네에 좋은 소문이 났어요 (평판 +2)"); }
                        Notify();
                    });
            }
            else
            {
                Hud.ShowChoice("만화책을 몰래…", $"「{book.name}」을(를) 가방에 넣어 가려던 아이를 붙잡았어요.\n\n타이르고 돌려받을까요, 엄마한테 전화할까요?",
                    "타이르고 돌려받기", "엄마한테 전화하기",
                    yes =>
                    {
                        if (yes) { AddReputation(1); Hud.Toast("아이가 다시는 안 그런대요 (평판 +1)"); }
                        else { AddReputation(-1); Town.comic.lastReaders += 1; Hud.Toast("엄마가 사과하러 오셨어요. 아이는 좀 무서워하네요 (평판 -1)"); }
                        Notify();
                    });
            }
        }

        int SettleComic()
        {
            if (!HasComicRoom) return 0;
            int readers = comicReadersToday;
            Stats.comicReaders = readers;
            Town.comic.lastReaders = readers;
            // 완결 보너스 (시리즈마다 한 번)
            foreach (var s in ComicTable.All)
                if (Town.comic.Owned(s.id) >= s.total && !Town.comic.completed.Contains(s.id))
                {
                    Town.comic.completed.Add(s.id);
                    AddReputation(3);
                    rivalHit += 5;
                    Dialogue.Show("만화방", $"「{s.name}」 전권을 다 모았어요! 완결까지 본 아이들이 감동했대요. (평판 +3)", UIKit.Accent, "만");
                }
            RefreshComicReaders(0);
            return readers * Town.comic.Fee * 2; // 두 시간씩
        }

        public int ComicVolumeCost(ComicSeries s) => s.pricePerVolume;

        public string ComicBuyBlocker(ComicSeries s)
        {
            int owned = Town.comic.Owned(s.id), released = ComicTable.Released(s, Day);
            if (owned >= s.total) return "완결까지 다 있어요";
            if (owned >= released) return "다음 권이 아직 안 나왔어요";
            return null;
        }

        public bool BuyComicVolume(string id)
        {
            var s = ComicTable.Find(id);
            if (s == null || !MorningOnly("만화책 사기")) return false;
            string b = ComicBuyBlocker(s);
            if (b != null) { Fail(b); return false; }
            int cost = ComicVolumeCost(s);
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.invested += cost;
            int owned = Town.comic.Owned(s.id) + 1;
            Town.comic.SetOwned(s.id, owned);
            var fresh = ComicTable.NewToday(Day);
            if (fresh == s && owned == ComicTable.Released(s, Day))
            {
                Town.comic.newBoughtDay = Day;
                Hud.Toast($"신간 「{s.name}」 {owned}권 입고! 오늘 만화방 손님 ×1.5");
            }
            else Hud.Toast($"「{s.name}」 {owned}권을 들여놨어요");
            Notify();
            return true;
        }

        public bool UpgradeComicSeats()
        {
            if (!MorningOnly("만화방 자리 늘리기")) return false;
            int lv = Town.comic.seatLevel;
            if (lv >= ComicTable.SeatCosts.Length) { Fail("자리를 다 늘렸어요"); return false; }
            int cost = ComicTable.SeatCosts[lv];
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.invested += cost;
            Town.comic.seatLevel++;
            Hud.Toast($"만화방 {ComicTable.SeatNames[Town.comic.seatLevel]}!");
            Notify();
            return true;
        }

        public void SetComicFee(int index)
        {
            Town.comic.feeIndex = Mathf.Clamp(index, 0, ComicTable.Fees.Length - 1);
            Notify();
        }

        /// <summary>2층 창문에 앉은 아이 머리 + 위에 "만화방 n/m" 말풍선.</summary>
        void RefreshComicReaders(int count)
        {
            if (count == comicReadersShown && (count > 0) == hasComicReaders) return;
            comicReadersShown = count;
            hasComicReaders = count > 0;
            if (comicReadersGo != null) { if (Application.isPlaying) Destroy(comicReadersGo); else DestroyImmediate(comicReadersGo); }
            comicReadersGo = null;
            if (count <= 0 || !HasComicRoom)
            {
                if (comicBubble != null) comicBubble.Hide();
                return;
            }
            comicReadersGo = Visual.ExpansionVisuals.BuildReaders(Layout.Root, Materials, Mathf.Min(count, 6));
            if (comicAnchor == null)
            {
                comicAnchor = new GameObject("Comic Anchor").transform;
                comicAnchor.SetParent(Layout.Root, false);
            }
            comicAnchor.localPosition = new Vector3(-5.3f, ShopDecor_WallH + 2.3f, ShopLayout.BayBack(1) - 0.3f);
            if (comicBubble == null && Hud != null) comicBubble = Hud.CreateBubble(comicAnchor);
            if (comicBubble != null) comicBubble.Show($"만화방 {count}/{Town.comic.Seats}명", UIKit.BubbleNormal, true);
        }

        const float ShopDecor_WallH = 3f;

        // ───────────── 도감 ─────────────

        void RecordSold(ProductData p)
        {
            if (p != null) Town.collection.AddSold(p.id);
        }

        void RecordCustomer(string key)
        {
            if (Town.collection.AddCustomer(key) && Phase == DayPhase.Open)
            {
                foreach (var k in CustomerDex.Kinds)
                    if (k.key == key) { Hud.Toast($"손님 도감에 「{k.name}」 등록!"); break; }
            }
        }

        /// <summary>도감 진행: 0 상품, 1 손님, 2 콤보, 3 만화.</summary>
        public (int have, int total) DexProgress(int tab)
        {
            switch (tab)
            {
                case 0:
                {
                    int have = 0, total = 0;
                    foreach (var p in Catalog.products) { total++; if (Town.collection.Sold(p.id) > 0) have++; }
                    return (have, total);
                }
                case 1:
                {
                    int have = 0;
                    foreach (var k in CustomerDex.Kinds) if (Town.collection.customers.Contains(k.key)) have++;
                    return (have, CustomerDex.Kinds.Length);
                }
                case 2: return (DiscoveredComboNames().Count, TotalComboCount);
                default:
                {
                    int have = 0;
                    foreach (var s in ComicTable.All) if (Town.comic.Owned(s.id) >= s.total) have++;
                    return (have, ComicTable.All.Length);
                }
            }
        }

        static readonly string[] DexNames = { "상품 도감", "손님 도감", "콤보 도감", "만화 도감" };

        void CheckCollectionRewards()
        {
            for (int t = 0; t < 4; t++)
            {
                var (have, total) = DexProgress(t);
                string key = "dex" + t;
                if (total <= 0 || have < total || Town.collection.rewarded.Contains(key)) continue;
                Town.collection.rewarded.Add(key);
                Money += 3000;
                Stats.revenue += 3000;
                AddReputation(5);
                Dialogue.Show("도감 완성!", $"{DexNames[t]}을 다 채웠어요! 동네 신문에 났대요. (+3,000원, 평판 +5)", UIKit.Accent, "★");
            }
        }

        // ───────────── 저장 ─────────────

        void LoadTown(TownSave t)
        {
            Town = t ?? new TownSave();
            Town.comic ??= new ComicState();
            Town.rival ??= new RivalState();
            Town.collection ??= new CollectionState();
            Today = SchoolCalendar.For(Day);
        }

#if UNITY_EDITOR
        /// <summary>스크린샷 도구용: 만화방·대회·라이벌 상태를 채운다.</summary>
        public void DebugTownForCapture()
        {
            Money = Mathf.Max(Money, 50000);
            foreach (var s in ComicTable.All) Town.comic.SetOwned(s.id, Mathf.Min(ComicTable.Released(s, Day), s.id == "bungae" ? 8 : 2));
            Town.comic.seatLevel = 2;
            Town.rival = new RivalState { district = District, active = true, strength = 62, openedDay = Day - 3, updatedDay = Day };
            Town.collection.AddCustomer("normal"); Town.collection.AddCustomer("counsel"); Town.collection.AddCustomer("regular_cheolsu");
            foreach (var p in Catalog.products) if (p.unlockDay <= 2) Town.collection.AddSold(p.id);
            Notify();
        }

        public void DebugComicReaders(int n)
        {
            comicReadersToday = n;
            RefreshComicReaders(n);
        }
#endif
    }
}
