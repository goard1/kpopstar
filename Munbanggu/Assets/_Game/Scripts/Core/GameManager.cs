using System;
using System.Collections.Generic;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.UI;
using UnityEngine;

namespace Munbanggu.Core
{
    public enum DayPhase
    {
        Morning,    // 아침: 도매상 매입 + 진열
        Open,       // 영업: 오전 → 하교 러시 → 방과 후
        Closing,    // 마감: 새 손님 없음, 남은 손님만 계산
        Settlement  // 정산
    }

    [Serializable]
    public class DayStats
    {
        public int revenue;
        public int cost;
        public int visitors;
        public int served;
        public int lostNoStock;    // 원하는 물건(대체품 포함)이 없어서 그냥 간 손님
        public int lostImpatient;  // 계산 기다리다 간 손님
        public int regularsVisited; // 단골 방문
        public int trendSold;      // 붐 상품 판매 개수
        public int tips;           // 단골이 얹어 준 돈 (revenue에 포함)
        // 돌발 사건
        public int stolen;         // 몰래 들고 나간 개수
        public int stolenValue;    // 그 판매가 합
        public int caught;         // 잡은 횟수
        public int credits;        // 해 준 외상 건수
        public int creditValue;    // 외상 금액 합
        public int comforted;      // 달래 준 우는 아이
        public int pulls;          // 번호 뽑기 판수
        public int jackpots;       // 1등 나온 횟수
        public int arcade;         // 오락기 수입 (revenue에 포함)
        public int invested;       // 가게 업그레이드에 쓴 돈 (cost와 별도)
        // 상담 손님
        public int counselPerfect, counselOk, counselWrong, counselNone;
        public int reputationDelta;
        public int salary;         // 직원 월급 (정산 때 빠짐)
        public int counselSpawned; // 오늘 나온 고민 손님 수 (하루 CounselPerDay명까지)
        public string tournament;  // 오늘 연 동네 대회 이름
        public int comicReaders;   // 오늘 2층 만화방 손님
        public int comicRequests;  // 오늘 들어준 만화 요청 쪽지 수
        public int upkeep;         // 가게 관리비 (증축 단계, 정산 때 빠짐)
        public int comic;          // 2층 만화방 수입 (정산 때 들어옴, revenue에 포함)
        public int Profit => revenue - cost;
    }

    /// <summary>하루 루프, 돈, 창고 재고, 손님 생성을 관리.</summary>
    public partial class GameManager : MonoBehaviour
    {
        [Header("밸런스 (나중에 Remote Config로 옮길 값)")]
        public int startMoney = 6000;
        public float openDuration = 150f;
        public float queuePatience = 16f;
        public int maxCustomersInShop = 8;
        public float baseSpawnPerSec = 0.14f;
        public float rushSpawnPerSec = 0.5f;
        [Tooltip("하루 지날 때마다 손님 증가율")]
        public float dailySpawnGrowth = 0.05f;
        public float maxSpawnGrowth = 2f;
        [Range(0f, 1f)] public float alternativeChance = 0.5f;

        [Header("유행 (붐)")]
        public TrendConfig trendConfig = new TrendConfig();

        public const float RushStart = 0.35f;
        public const float RushEnd = 0.6f;

        public ProductCatalog Catalog { get; private set; }
        public ShopLayout Layout { get; private set; }
        public HUD Hud { get; private set; }
        public MaterialCache Materials { get; private set; }

        public int Day { get; private set; }
        public int Money { get; private set; }
        public DayPhase Phase { get; private set; }
        public DayStats Stats { get; private set; } = new DayStats();

        // ───────────── 빨리감기 ─────────────

        const string SpeedPref = "game_speed";
        public const int MaxSpeed = 3;
        /// <summary>장사 시간 배속 (1~3). 아침·정산은 항상 1배. 다음 실행 때도 기억.</summary>
        public int Speed { get; private set; } = 1;
        /// <summary>지금 배속이 실제로 걸리는 단계인지 (영업·마감 중).</summary>
        public bool SpeedActive => Phase == DayPhase.Open || Phase == DayPhase.Closing;

        public void CycleSpeed()
        {
            Speed = Speed % MaxSpeed + 1;
            PlayerPrefs.SetInt(SpeedPref, Speed);
            ApplySpeed();
            Notify();
        }

        void ApplySpeed()
        {
            // 설정·선택 창이 열려 있으면 게임 시간 정지 (UI 연출은 unscaled 시간이라 계속 움직임)
            bool paused = Hud != null && (Hud.IsSettingsOpen || Hud.IsChoiceOpen || Hud.IsPickerOpen);
            float scale = paused ? 0f : SpeedActive ? Speed : 1f;
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
        }
        /// <summary>지금 붐인 상품 (없는 날은 null).</summary>
        public ProductData Trend => trends?.Current;
        /// <summary>붐 단계 (상승/절정/하락).</summary>
        public TrendStage TrendStage => trends?.Stage ?? TrendStage.None;
        /// <summary>"상승"/"절정"/"하락" 또는 "".</summary>
        public string TrendStageLabel => trends?.StageLabel ?? "";
        /// <summary>다음 붐 소문 상품 (소문이 안 났으면 null, 헛소문일 수 있음).</summary>
        public ProductData TrendRumor => trends?.Rumor;
        /// <summary>붐이 끝나 안 팔리는 상품 (없으면 null).</summary>
        public ProductData TrendCrashed => trends?.Crashed;
        /// <summary>상시 표시용 유행 한 줄 (현재 붐 상황, 없으면 소문). 없으면 "".</summary>
        public string TrendHint => trends?.StatusLine() ?? trends?.RumorLine() ?? "";
        public TrendSystem Trends => trends;
        public RegularManager Regulars => regulars;
        public DialoguePanel Dialogue { get; private set; }

        public ProductData Selected { get; private set; }

        public float OpenProgress => Phase == DayPhase.Morning ? 0f
            : OpenDuration <= 0f ? 1f : Mathf.Clamp01(openTimer / OpenDuration);
        public bool IsRush => Phase == DayPhase.Open && OpenProgress >= RushStart && OpenProgress < RushEnd;

        public event Action Changed;

        readonly Dictionary<ProductData, int> backroom = new Dictionary<ProductData, int>();
        readonly List<Customer> customers = new List<Customer>();
        TrendSystem trends;
        readonly RegularManager regulars = new RegularManager();
        float openTimer;
        float spawnAcc;
        bool rushAnnounced;
        bool initialized;
        readonly DayEvents events = new DayEvents();
        readonly CreditLedger ledger = new CreditLedger();
        readonly List<string> creditNews = new List<string>();
        readonly HashSet<string> seenEvents = new HashSet<string>();
        int friendsPending;
        float friendsTimer;

        public void Init(ProductCatalog catalog, Material baseMaterial)
        {
            Visual.ExpansionVisuals.ComicSpines = new List<Color>(); // static: 이전 슬롯 책등이 남지 않게
            Visual.ProductVisuals.ClearCache(); Visual.ProductIcons.Clear(); // 언어에 따라 상품 모양이 달라서 (대만판)
            Catalog = Instantiate(catalog); // 복제본에 특제 상품(레시피)을 더한다 (에셋은 그대로)
            AddSpecialProducts();
            trends = new TrendSystem(trendConfig, catalog);
            Materials = new MaterialCache(baseMaterial);
            Visual.ShopDecor.ApplyPalette(ExpansionTable.Districts[0].palette); // static 색이 이전 실행 값으로 남지 않게
            Layout = ShopBuilder.Build(transform, Materials);
            // 상품 아이콘: 조명이 생긴 뒤, HUD가 쓰기 전에 한 번에 찍어 둔다
            Visual.ProductIcons.Init(Materials.VertexColor);
            Visual.ProductIcons.Prewarm(catalog.products);
            Hud = HUD.Build(this);
            Dialogue = DialoguePanel.Build(transform);
            gameObject.AddComponent<TapInput>().Init(this);
            HUD.CollectSettlementLines -= AddSettlementLines; // static 이벤트: 중복 구독 방지
            HUD.CollectSettlementLines += AddSettlementLines;
            LoadOrNew();
            ApplyDistrictShape();
            RebuildNav();
            ApplyComicEffects();
            RebuildStaffAgents();
            RebuildDecor();
            Speed = Mathf.Clamp(PlayerPrefs.GetInt(SpeedPref, 1), 1, MaxSpeed);
            initialized = true;
            StartMorning();
            StoryOnStart(); // 새 판이면 프롤로그 (docs/11)
        }

        void OnDestroy()
        {
            HUD.CollectSettlementLines -= AddSettlementLines;
            Time.timeScale = 1f;
        }

        /// <summary>정산 화면에 단골·유행 줄 추가.</summary>
        void AddSettlementLines(GameManager gm, List<SettlementLine> lines)
        {
            if (gm != this) return;
            AddTownSettlementLines(lines);
            if (Stats.regularsVisited > 0) lines.Add(SettlementLine.Count("단골 방문", Stats.regularsVisited));
            if (Stats.tips > 0) lines.Add(SettlementLine.Money("그중 단골 팁", Stats.tips));
            if (Stats.caught > 0) lines.Add(SettlementLine.Count("몰래 나가던 아이 잡음", Stats.caught));
            if (Stats.stolen > 0) lines.Add(SettlementLine.Money($"슬쩍 당함 ({Stats.stolen}개)", Stats.stolenValue, true));
            if (Stats.credits > 0) lines.Add(SettlementLine.Money($"외상 해 줌 ({Stats.credits}건, 내일 받을 돈)", Stats.creditValue));
            int counsel = Stats.counselPerfect + Stats.counselOk + Stats.counselWrong + Stats.counselNone;
            if (counsel > 0)
            {
                lines.Add(SettlementLine.TextLine($"상담 {counsel}명: 딱 맞음 {Stats.counselPerfect} / 괜찮음 {Stats.counselOk} / 아님 {Stats.counselWrong + Stats.counselNone}"));
                string d = Stats.reputationDelta > 0 ? $"+{Stats.reputationDelta}" : Stats.reputationDelta.ToString();
                lines.Add(SettlementLine.TextLine($"동네 평판 {Reputation} ({d})", Stats.reputationDelta >= 0 ? UIKit.Green : UIKit.Red));
            }
            if (Stats.comforted > 0) lines.Add(SettlementLine.Count("달래 준 아이", Stats.comforted));
            if (Stats.arcade > 0) lines.Add(SettlementLine.Money("그중 오락기 수입", Stats.arcade));
            if (Stats.invested > 0) lines.Add(SettlementLine.Money("오늘 가게에 투자", Stats.invested, true));
            if (Stats.salary > 0) lines.Add(SettlementLine.Money($"직원 월급 ({Staff.members.Count}명)", Stats.salary, true));
            if (Stats.upkeep > 0) lines.Add(SettlementLine.Money($"월세·관리비 ({ExpansionTable.District(District).name})", Stats.upkeep, true));
            if (Stats.comic > 0) lines.Add(SettlementLine.Money($"그중 {upstairsLabel ?? "2층 수입"}", Stats.comic));
            if (Stats.pulls > 0) lines.Add(SettlementLine.Count("번호 뽑기", Stats.pulls, "판"));
            if (Stats.jackpots > 0) lines.Add(SettlementLine.Count("1등 당첨!", Stats.jackpots, "번"));
            var board = Layout.Board;
            if (board != null && IsUnlocked(Catalog.Find(BoardProductId)))
            {
                if (board.Count == 0)
                    lines.Add(SettlementLine.Tip("뽑기판이 비었어요. 도매상에서 번호 뽑기를 사서 새 판을 걸어요!"));
                else if (!board.GrandPrizeLeft)
                    lines.Add(SettlementLine.Tip("뽑기판 1등이 이미 나가서 아이들이 시들해요. 남은 걸 다 뽑히면 새 판을!"));
            }
            if (Trend != null)
                lines.Add(SettlementLine.Count($"붐 상품({Trend.displayName}) 판매", Stats.trendSold, "개"));
            if (TrendStage == TrendStage.Falling)
                lines.Add(SettlementLine.Tip($"{Trend.displayName} 붐이 식고 있어요. 남은 재고를 먼저 팔고 사재기는 조심!"));
            else if (TrendRumor != null)
                lines.Add(SettlementLine.Tip($"소문의 {TrendRumor.displayName}, 미리 조금 사 둘까요?"));
        }

        // ───────────── 해금 ─────────────

        /// <summary>도매상에서 살 수 있는 상품인지 (unlockDay가 오늘 이하).</summary>
        public bool IsUnlocked(ProductData p) => p != null && (p.special ? SpecialUnlocked(p) : p.unlockDay <= Day || EarlyUnlocked(p));

        /// <summary>오늘 새로 들어온 상품인지 ("NEW" 표시용).</summary>
        public bool IsNew(ProductData p) => p != null && Day > 1 && p.unlockDay == Day;

        /// <summary>다음에 해금되는 날 (모두 해금됐으면 0).</summary>
        public int NextUnlockDay()
        {
            int next = 0;
            foreach (var p in Catalog.products)
                if (p != null && p.unlockDay > Day && (next == 0 || p.unlockDay < next)) next = p.unlockDay;
            return next;
        }

        // ───────────── 재고 ─────────────

        public int Stock(ProductData p) => p != null && backroom.TryGetValue(p, out var n) ? n : 0;

        void AddStock(ProductData p, int n)
        {
            if (p == null || n <= 0) return;
            backroom[p] = Stock(p) + n;
        }

        void RemoveStock(ProductData p, int n)
        {
            if (p == null || n <= 0) return;
            backroom[p] = Mathf.Max(0, Stock(p) - n);
        }

        public int TotalShelfStock()
        {
            int total = 0;
            foreach (var s in Layout.Slots) total += s.Count;
            return total;
        }

        // ───────────── 플레이어 행동 ─────────────

        public bool BuyBox(ProductData p)
        {
            if (p == null) return false;
            if (Phase != DayPhase.Morning) { Fail("도매상은 아침에만 열어요"); return false; }
            if (!IsUnlocked(p)) { Fail($"{Josa.Eun(p.displayName)} 아직 도매상에 안 들어왔어요 ({p.unlockDay}일차부터)"); return false; }
            int cost = BoxCost(p);
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.cost += cost;
            AddStock(p, p.boxSize);
            if (Selected == null) Selected = p;
            GameEvents.RaiseBoxBought(p);
            Notify();
            return true;
        }

        public void Select(ProductData p)
        {
            Selected = Selected == p ? null : p;
            Notify();
        }

        public void OnSlotTapped(ShelfSlot slot)
        {
            if (Phase == DayPhase.Settlement) return;
            if (Selected == null)
            {
                Fail("아래 창고에서 상품을 먼저 고르세요");
                return;
            }
            if (Stock(Selected) <= 0)
            {
                Fail($"창고에 {Selected.displayName} 재고가 없어요");
                return;
            }
            // 번호 뽑기는 가게 앞 뽑기판에만, 뽑기판엔 번호 뽑기만
            if (slot.IsBoard && !IsBoardProduct(Selected))
            {
                Fail("가게 앞 뽑기판엔 번호 뽑기만 걸 수 있어요");
                return;
            }
            if (!slot.IsBoard && IsBoardProduct(Selected))
            {
                Fail("번호 뽑기는 가게 앞 뽑기판에 걸어요");
                return;
            }
            if (slot.Product != null && slot.Product != Selected)
            {
                // 다른 상품이 있던 매대 → 남은 물건은 창고로 돌려보내고 교체
                var (old, n) = slot.Clear();
                AddStock(old, n);
            }
            if (slot.Count >= slot.Capacity)
            {
                Fail("매대가 꽉 찼어요");
                return;
            }
            int added = slot.Add(Selected, Stock(Selected));
            RemoveStock(Selected, added);
            if (added > 0) GameEvents.RaiseItemsPlaced(slot, Selected, added);
            Notify();
        }

        public void OpenShop()
        {
            if (Phase != DayPhase.Morning) return;
            if (TotalShelfStock() == 0) { Fail("매대에 물건을 먼저 진열하세요"); return; }
            Phase = DayPhase.Open;
            openTimer = 0f;
            spawnAcc = 0.8f; // 문 열자마자 첫 손님
            rushAnnounced = false;
            events.Plan(Day);
            friendsPending = 0;
            TownOpen();
            Hud.CloseModals();
            Save();
            GameEvents.RaisePhaseChanged(Phase);
            Notify();
        }

        public void NextDay()
        {
            if (Phase != DayPhase.Settlement) return;
            Day++;
            StartMorning();
        }

        // ───────────── 하루 흐름 ─────────────

        void StartMorning()
        {
            Phase = DayPhase.Morning;
            // 직원은 쉬는 자리로 (어제 계산대·매대 앞에 있던 채로 남지 않게)
            registerStaff = null;
            restockTargets.Clear();
            foreach (var ag in staffAgents.Values) if (ag != null) ag.GoHome();
            Stats = new DayStats();
            TownMorning();
            trends.Advance(Day, (p, d) => p.unlockDay <= d);
            regulars.PlanDay(Day, UnlockedFavorite);
            // 외상 정리는 저장 전에 (다시 켜도 두 번 받지 않게)
            creditNews.Clear();
            int repaid = ledger.Settle(Day, creditNews, UpgradeTable.CreditRepayChance(Upgrades.Level(UpgradeTable.Ledger)));
            if (repaid > 0) Money += repaid;
            Save();
            GameEvents.RaisePhaseChanged(Phase);
            Notify();
            Hud.ShowMorning();
            ShowMorningNews();
        }

        void ShowMorningNews()
        {
            Dialogue.Clear();
            TownMorningNews();
            if (StaffRules.IsMerchantDay(Day))
            {
                var offer = MerchantOfferToday();
                Dialogue.Show("보따리 상인", "직원 물건 팔러 왔어요~ 오늘은 딱 세 가지! " + string.Join(", ", offer.ConvertAll(i => i.name + "(" + i.RarityName + ")")) + "\n(직원 창에서 사요. " + StaffRules.MerchantEvery + "일마다 와요)", new Color(0.7f, 0.5f, 0.3f), "상");
            }
            foreach (var line in creditNews) Dialogue.Show("외상 장부", line, UIKit.Accent, "외");
            if (IsNew(Catalog.Find(BoardProductId)))
                Dialogue.Show("도매상", "번호 뽑기가 들어왔어요! 사서 가게 앞 뽑기판에 걸어 두면 아이들이 와서 한 판씩 뽑아요. 1등이 나오면 동네에 소문이 나요!", UIKit.Blue);
            if (Day > 1)
            {
                var fresh = new List<string>();
                foreach (var p in Catalog.products)
                    if (IsNew(p)) fresh.Add(p.displayName);
                if (fresh.Count > 0)
                    Dialogue.Show("도매상", $"새 물건이 들어왔어요: {string.Join(", ", fresh)}", UIKit.Blue);
            }
            var lines = trends.MorningLines();
            if (lines.Count > 0)
                Dialogue.Show("동네 소문", string.Join("\n", lines), UIKit.Accent, "소");
        }

        ProductData UnlockedFavorite(RegularProfile r)
        {
            var p = Catalog.Find(r.favoriteId);
            return IsUnlocked(p) ? p : null;
        }

        void EnterSettlement()
        {
            Phase = DayPhase.Settlement;
            // 직원 월급
            int salary = DailySalaryTotal();
            if (salary > 0) { Money -= salary; Stats.salary = salary; }
            // 월세·관리비 (동네·증축할수록) · 2층 만화방/오락실 수입
            int upkeep = ExpansionTable.Upkeep(District, Expansion);
            if (upkeep > 0) { Money -= upkeep; Stats.upkeep = upkeep; }
            UnlockAlbum("first_day");
            int townIncome = TownSettle();
            int comic = HasComicRoom ? townIncome : ExpansionTable.UpstairsIncome(District, Expansion, Stats.visitors, out upstairsLabel);
            if (HasComicRoom) upstairsLabel = "2층 만화방 수입";
            if (comic > 0) { Money += comic; Stats.revenue += comic; Stats.comic = comic; }
            Save();
            GameEvents.RaisePhaseChanged(Phase);
            Notify();
            Hud.ShowSettlement();
        }

        void Update()
        {
            if (!initialized) return;
            ApplySpeed();
            if (Phase == DayPhase.Open || Phase == DayPhase.Closing) TickStaff();
            if (Phase == DayPhase.Open)
            {
                openTimer += Time.deltaTime;
                TickArcade();
                TickTown();
                if (IsRush && !rushAnnounced)
                {
                    rushAnnounced = true;
                    Hud.Toast("하교 시간! 손님이 몰려와요");
                    GameEvents.RaiseRushStarted();
                }
                SpawnTick();
                if (openTimer >= OpenDuration)
                {
                    Phase = DayPhase.Closing;
                    Hud.Toast("문 닫을 시간! 남은 손님만 계산해요");
                    GameEvents.RaisePhaseChanged(Phase);
                    Notify();
                }
            }
            else if (Phase == DayPhase.Closing && customers.Count == 0)
            {
                EnterSettlement();
            }
        }

        void SpawnTick()
        {
            if (customers.Count >= maxCustomersInShop + 2 * ExpansionTable.Bays(District, Expansion)) return; // 넓은 가게엔 더 많이

            // 단골은 정해진 시각에 (가게가 꽉 차 있으면 자리가 날 때) 들어온다
            var regular = regulars.TakeDue(OpenProgress);
            if (regular != null) { SpawnCustomer(regular); return; }

            // 돌발 사건 손님 (몰래 들고 가는 아이, 외상, 우는 아이)
            if (Phase == DayPhase.Open)
            {
                var kind = events.TakeDue(OpenProgress);
                if (kind != CustomerKind.Normal) { SpawnCustomer(null, kind); return; }
            }

            // 달래 준 아이가 데려온 친구들
            if (friendsPending > 0)
            {
                friendsTimer -= Time.deltaTime;
                if (friendsTimer <= 0f)
                {
                    friendsPending--;
                    friendsTimer = 0.9f;
                    SpawnCustomer(null);
                    return;
                }
            }

            float rate = IsRush ? rushSpawnPerSec : baseSpawnPerSec;
            rate *= Mathf.Min(maxSpawnGrowth, 1f + (Day - 1) * dailySpawnGrowth) * UpgradeTable.SpawnMultiplier(Upgrades.Level(UpgradeTable.Sign)) * ReputationSpawnMultiplier * (1f + DecorAppeal) * ExpansionTable.SpawnMultiplier(District, Expansion) * TownSpawnMultiplier;
            spawnAcc += rate * Time.deltaTime;
            if (spawnAcc < 1f) return;
            spawnAcc -= 1f;
            SpawnCustomer(null);
        }

        Customer SpawnCustomer(RegularProfile regular, CustomerKind kind = CustomerKind.Normal, ProductData forced = null, string askOverride = null, string dexKey = null)
        {
            ProductData wanted = null;
            if (regular != null && (regulars.IsAdultVisit(regular) || UnityEngine.Random.value < regular.favoriteChance))
                wanted = UnlockedFavorite(regular);
            if (forced != null) wanted = forced;
            if (wanted == null) wanted = RollWanted();

            // 상담 손님: 매대로 가지 않고 계산대로 와서 고민을 말한다 (docs/05)
            CounselRequest request = null;
            if (regular == null && forced == null && kind == CustomerKind.Normal && RollCounsel())
            {
                request = CounselRequests.Roll(UnlockedCounselable());
                if (request != null) kind = CustomerKind.Counsel;
            }

            var go = new GameObject(regular != null ? $"Regular {regular.id}" : "Customer");
            go.transform.SetParent(transform, false);
            go.transform.position = Layout.EntrancePoint;
            var c = go.AddComponent<Customer>();
            customers.Add(c);
            Stats.visitors++;
            if (regular != null)
            {
                Stats.regularsVisited++;
                bool adult = regulars.IsAdultVisit(regular);
                string intro = regulars.OnEnter(regular, Day);
                if (adult) go.transform.localScale = Vector3.one * 1.3f; // 어른이 되어 찾아옴
                if (intro != null) Dialogue.Show(RegularTitle(regular), intro, regular.color);
                RecordCustomer((adult ? "adult_" : "regular_") + regular.id);
                UnlockAlbum("first_regular");
            }
            if (regular == null) RecordCustomer(dexKey ?? (kind == CustomerKind.Counsel ? "counsel" : kind == CustomerKind.Sneaky ? "sneaky" : kind == CustomerKind.Credit ? "credit" : kind == CustomerKind.Crying ? "crying" : "normal"));
            c.AskOverride = askOverride;
            c.Init(this, wanted, Hud.CreateBubble(go.transform), regular, kind, request);
            return c;
        }

        // ───────────── 상담 손님 (아이 마음 읽기) ─────────────

        /// <summary>동네 평판 0~100 (시작 50). 상담 결과로 오르내리고 손님 수에 영향.</summary>
        public int Reputation { get; private set; } = 50;
        readonly HashSet<string> knownTraits = new HashSet<string>(); // 특징을 알게 된 상품 id

        /// <summary>하루 고민(상담) 손님 수 상한.</summary>
        public const int CounselPerDay = 5;

        /// <summary>
        /// 고민 손님은 하루 CounselPerDay명까지, 영업 시간 동안 고르게 (진행도 몫보다 앞서 나오지 않게).
        /// 1일차는 튜토리얼이라 2명까지.
        /// </summary>
        bool RollCounsel()
        {
            int quota = Day <= 1 ? 2 : CounselPerDay;
            if (Stats.counselSpawned >= quota) return false;
            if (Stats.counselSpawned >= 1 + quota * OpenProgress) return false;
            if (UnityEngine.Random.value >= 0.3f) return false;
            Stats.counselSpawned++;
            return true;
        }

        /// <summary>평판에 따른 손님 수 배율 (0 → ×0.85, 100 → ×1.15).</summary>
        float ReputationSpawnMultiplier => 0.85f + 0.3f * Reputation / 100f;

        public IEnumerable<ProductData> UnlockedCounselable()
        {
            foreach (var p in Catalog.products)
                if (IsUnlocked(p) && ProductTraits.Counselable(p) && !IsBoardProduct(p)) yield return p;
        }

        /// <summary>지금 매대에 진열돼 있는 상담 대상 상품 (종류별 한 번씩, 도매상 순서).</summary>
        public List<ProductData> ShelvedCounselProducts()
        {
            var list = new List<ProductData>();
            foreach (var p in Catalog.products)
                if (ProductTraits.Counselable(p) && ShelfCount(p) > 0) list.Add(p);
            return list;
        }

        /// <summary>매대(뽑기판 제외)에 올라 있는 개수 합.</summary>
        public int ShelfCount(ProductData p)
        {
            int n = 0;
            foreach (var s in Layout.Slots)
                if (!s.IsBoard && s.Product == p) n += s.Count;
            return n;
        }

        public bool IsTraitKnown(ProductData p) => p != null && knownTraits.Contains(p.id);

        /// <summary>매대에서 그 상품을 하나 꺼낸다 (제일 많은 칸에서). 성공하면 true.</summary>
        public bool TakeFromShelf(ProductData p)
        {
            ShelfSlot best = null;
            foreach (var s in Layout.Slots)
                if (!s.IsBoard && s.Product == p && s.Count > 0 && (best == null || s.Count > best.Count)) best = s;
            if (best == null || !best.TakeOne()) return false;
            Notify();
            return true;
        }

        /// <summary>
        /// 상담 결과 반영: 통계·평판·특징 발견·팁. 팁(원)을 돌려준다 (딱 맞을 때만).
        /// given = 실제로 건넨 상품(못 골랐으면 null).
        /// </summary>
        public int ResolveCounsel(CounselResult result, ProductData given, bool bought)
        {
            int tip = 0, rep = 0;
            switch (result)
            {
                case CounselResult.Perfect:
                    Stats.counselPerfect++;
                    rep = 2 + GrandmaCounselRepBonus;
                    UnlockAlbum("first_perfect");
                    if (given != null)
                        tip = Mathf.Max(10, Mathf.RoundToInt(given.retailPrice * UnityEngine.Random.Range(0.3f, 0.5f) / 10f) * 10);
                    break;
                case CounselResult.Ok:
                    Stats.counselOk++;
                    break;
                case CounselResult.Wrong:
                    Stats.counselWrong++;
                    rep = bought ? -1 : -2;
                    break;
                default:
                    Stats.counselNone++;
                    rep = -1;
                    break;
            }
            if (tip > 0)
            {
                Money += tip;
                Stats.revenue += tip;
                Stats.tips += tip;
            }
            Reputation = Mathf.Clamp(Reputation + rep, 0, 100);
            Stats.reputationDelta += rep;
            // 상담으로 건네 본 상품은 특징을 알게 된다 (다음부터 고르기 창에 표시)
            if (given != null && knownTraits.Add(given.id))
                Hud.Toast($"{Josa.Eul(given.displayName)} 건네 보니 특징을 알았어요: #{string.Join(" #", ProductTraits.Of(given))}", 2.6f);
            Notify();
            return tip;
        }

        // ───────────── 가게 업그레이드 ─────────────

        public Upgrades Upgrades { get; } = new Upgrades();
        GameObject upgradeDecor;
        float arcadeTimer;

        /// <summary>상자 값 (도매상 단골 할인 반영, 10원 단위).</summary>
        public int BoxCost(ProductData p) =>
            p == null ? 0 : Mathf.RoundToInt(p.BoxCost * UpgradeTable.WholesaleMultiplier(Upgrades.Level(UpgradeTable.Wholesale)) * GrandmaBoxMultiplier / 10f) * 10;

        /// <summary>영업 시간 (영업시간 연장 반영).</summary>
        public float OpenDuration => openDuration + UpgradeTable.ExtraOpenSeconds(Upgrades.Level(UpgradeTable.Hours));

        /// <summary>계산 줄 인내심 (선풍기·의자 반영).</summary>
        public float QueuePatience => queuePatience * UpgradeTable.PatienceMultiplier(Upgrades.Level(UpgradeTable.Fan)) * ExpansionTable.PatienceMultiplier(District, Expansion) * GrandmaPatienceMultiplier;

        /// <summary>몰래 나가는 아이 속도 배율 (방범 거울 반영).</summary>
        public float SneakSpeed => UpgradeTable.SneakSpeedMultiplier(Upgrades.Level(UpgradeTable.Mirror)) * GrandmaSneakMultiplier * ComicSneakMultiplier;

        /// <summary>업그레이드 사기 (아침에만). 성공하면 바로 가게에 반영.</summary>
        public bool BuyUpgrade(string id)
        {
            var def = UpgradeTable.Find(id);
            if (def == null) return false;
            if (Phase != DayPhase.Morning) { Fail("가게 꾸미기는 아침에만 할 수 있어요"); return false; }
            if (Upgrades.IsMaxed(def)) { Fail("이미 최고 단계예요"); return false; }
            int cost = Upgrades.NextCost(def);
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.invested += cost;
            Upgrades.Raise(id);
            ApplyUpgrades();
            Hud.Toast($"{def.name} {Upgrades.Level(id)}단계 완료!");
            Save();
            Notify();
            return true;
        }

#if UNITY_EDITOR
        /// <summary>스크린샷 도구용: 모든 업그레이드 최고 단계로.</summary>
        public void DebugMaxUpgrades()
        {
            foreach (var d in UpgradeTable.All)
                while (!Upgrades.IsMaxed(d)) Upgrades.Raise(d.id);
            ApplyUpgrades();
            foreach (var s in Layout.Slots)
                if (!s.IsBoard && s.Product == null && Catalog.products.Count > 0)
                {
                    var p = Catalog.products[(s.Index * 5) % Catalog.products.Count];
                    if (!IsBoardProduct(p)) s.Load(p, s.Capacity);
                }
            Notify();
        }

        /// <summary>스크린샷 도구용: 0·1번 진열대에 세트 짝(쫀드기+아폴로)을 놓고 장식 몇 개 배치.</summary>
        public void DebugDecorForCapture()
        {
            Money = Mathf.Max(Money, 30000);
            var a = Catalog.products.Find(p => p.id == "jjondeugi");
            var b = Catalog.products.Find(p => p.id == "apollo");
            if (a != null) Layout.Slots[0].Load(a, Layout.Slots[0].Capacity);
            if (b != null) Layout.Slots[1].Load(b, Layout.Slots[1].Capacity);
            PlaceDecor(0, "candyjar");
            LevelUpDecor(0);
            LevelUpDecor(0);
            PlaceDecor(2, "plant");
            foreach (var s in Layout.Slots)
            {
                if (s.IsBoard || s.Product == null || s.Index < 3) continue;
                var match = Array.Find(FacilityTable.Decors, d => d.affinity == s.Product.category);
                if (match != null && Facilities.At(s.Index) == null) { PlaceDecor(s.Index, match.id); break; }
            }
            Notify();
        }

        /// <summary>스크린샷 도구용: 동네·증축 단계를 바로 바꾸고 새 진열대를 채운다.</summary>
        public void DebugDistrict(int district, int level)
        {
            if (district != District) { ShopBuilder.ClearGroups(Layout); placements.Clear(); ShopBuilder.ResetSlotTransforms(Layout); Facilities.decor.RemoveAll(d => d.slot > Layout.Board.Index); District = Mathf.Clamp(district, 0, ExpansionTable.Districts.Length - 1); ApplyDistrictLook(); }
            Expansion = Mathf.Clamp(level, 0, ExpansionTable.Max);
            if (Hud != null) Hud.RebuildSlotLabels();
            ApplyUpgrades();
            foreach (var s in Layout.Slots)
                if (!s.IsBoard && s.Product == null && Catalog.products.Count > 0)
                {
                    var p = Catalog.products[(s.Index * 7 + 3) % Catalog.products.Count];
                    if (!IsBoardProduct(p)) s.Load(p, s.Capacity);
                }
            RebuildDecor();
            Notify();
        }

        /// <summary>스크린샷 도구용: 지금 배치에서 길이 다 이어지는지, 문 → 각 진열대 길 경유점 수.</summary>
        public string DebugCheckNav()
        {
            RebuildNav();
            bool ok = AllReachable(out string why);
            var sb = new System.Text.StringBuilder($"동네 {District} 증축 {Expansion}: 길 {(ok ? "OK" : "막힘 - " + why)}");
            var path = new List<Vector3>();
            var door = new Vector3(ShopLayout.DoorX, 0f, ShopLayout.DoorInsideZ);
            foreach (var s in Layout.Slots)
            {
                if (s.IsBoard) continue;
                nav.FindPath(door, s.StandPoint, path);
                sb.Append($" | #{s.Index}:{path.Count}");
            }
            return sb.ToString();
        }

        /// <summary>스크린샷 도구용: 다음 날 아침으로 (돈 넉넉히).</summary>
        public void DebugMorningForCapture()
        {
            Money = Mathf.Max(Money, 30000);
            Day++;
            StartMorning();
        }
#endif

        /// <summary>업그레이드·동네·증축 단계를 가게에 반영 (매대 칸 수, 추가 진열대, 장식, 옆칸). 여러 번 불러도 됨.</summary>
        void ApplyUpgrades()
        {
            ApplyDistrictShape();
            int cap = UpgradeTable.ShelfCapacity(Upgrades.Level(UpgradeTable.ShelfSize));
            int before = Layout.Slots.Count;
            bool added = false;
            if (Upgrades.Level(UpgradeTable.Island) > 0) added |= ShopBuilder.AddShelfGroup(Layout, Materials, cap, ShopBuilder.GroupExtra);
            int bays = ExpansionTable.Bays(District, Expansion);
            for (int k = 1; k <= bays; k++)
            {
                added |= ShopBuilder.AddShelfGroup(Layout, Materials, cap, ShopBuilder.BayGroup(k));
                if (ExpansionTable.HasSideShelf(District, Expansion, k))
                    added |= ShopBuilder.AddShelfGroup(Layout, Materials, cap, ShopBuilder.SideGroup(k));
            }
            ApplyPlacements();
            if (added && Hud != null) Hud.RebuildSlotLabels();
            foreach (var s in Layout.Slots)
                if (!s.IsBoard) s.SetCapacity(cap);
            upgradeDecor = Visual.UpgradeVisuals.Rebuild(Layout.Root, Materials, Upgrades, upgradeDecor);
            expansionDecor = Visual.ExpansionVisuals.Rebuild(Layout.Root, Materials, District, Expansion, expansionDecor);
            Staff.maxStaff = ExpansionTable.MaxStaff(District, Expansion);
            RebuildNav();
            // 새로 생긴 진열대가 옮겨 둔 진열대와 겹치면 가까운 빈자리로
            for (int i = before; i < Layout.Slots.Count; i++) RelocateIfBlocked(Layout.Slots[i]);
            UpdateCameraFocus();
        }

        /// <summary>동네 모양(옆칸 깊이)을 레이아웃에 알려 준다.</summary>
        void ApplyDistrictShape()
        {
            for (int k = 1; k <= ShopLayout.BayBacks.Length; k++)
                ShopLayout.BayBacks[k - 1] = ExpansionTable.BayBack(District, k);
        }

        // ───────────── 진열대 옮기기 · 길찾기 ─────────────

        readonly ShopNav nav = new ShopNav();
        /// <summary>플레이어가 옮긴 진열대 자리 (Slots 인덱스 → 자리·방향).</summary>
        readonly Dictionary<int, SlotPlacement> placements = new Dictionary<int, SlotPlacement>();

        public static readonly Vector2 ShelfSize = new Vector2(1.16f, 0.76f);

        static ShopNav.Box ShelfBox(Vector3 pos, float yaw) => new ShopNav.Box(new Vector2(pos.x, pos.z), ShelfSize, yaw);

        /// <summary>바닥·장애물·진열대로 길찾기 격자를 다시 만든다. exclude는 빼고 (옮기기 확인용).</summary>
        void RebuildNav(ShelfSlot exclude = null)
        {
            nav.Begin();
            nav.AddInside(Rect.MinMaxRect(-4f, -4f, 4f, 4f));
            int bays = ExpansionTable.Bays(District, Expansion);
            for (int k = 1; k <= bays; k++)
                nav.AddInside(Rect.MinMaxRect(ShopLayout.BayX0(k), -4f, ShopLayout.BayX1(k), ShopLayout.BayBack(k)));
            float left = bays > 0 ? ShopLayout.BayX0(bays) - 0.2f : -4.2f;
            nav.AddOutside(Rect.MinMaxRect(left, -6f, 4.2f, -4f));
            foreach (var b in Layout.Statics) nav.AddStatic(b);
            foreach (var r in Layout.Reserved) nav.AddReserved(r);
            // 가게 앞 평상·냉동고, 오락기
            if (ExpansionTable.Has(District, Expansion, ExpansionFeature.Pyeongsang) || ExpansionTable.Has(District, Expansion, ExpansionFeature.Freezer))
                nav.AddStatic(new ShopNav.Box(new Vector2(-5.1f, -4.9f), new Vector2(2.0f, 1.0f)));
            int arcades = UpgradeTable.ArcadeMachines(Upgrades.Level(UpgradeTable.Arcade));
            for (int i = 0; i < arcades; i++)
            {
                var p = Visual.UpgradeVisuals.ArcadePosition(i);
                nav.AddStatic(new ShopNav.Box(new Vector2(p.x, p.z), new Vector2(0.55f, 0.55f)));
            }
            var boxes = new List<ShopNav.Box>();
            foreach (var s in Layout.Slots)
                if (s != null && !s.IsBoard && s != exclude)
                    boxes.Add(ShelfBox(s.transform.localPosition, s.transform.localEulerAngles.y));
            nav.Rebuild(boxes);
            ShopNav.Current = nav;
        }

        /// <summary>저장된(옮긴) 진열대 자리를 적용.</summary>
        void ApplyPlacements()
        {
            foreach (var kv in placements)
            {
                if (kv.Key < 0 || kv.Key >= Layout.Slots.Count) continue;
                var s = Layout.Slots[kv.Key];
                if (s.IsBoard) continue;
                s.transform.localPosition = new Vector3(kv.Value.x, 0f, kv.Value.z);
                s.transform.localRotation = Quaternion.Euler(0f, kv.Value.yaw, 0f);
            }
        }

        /// <summary>진열대가 다른 것과 겹치면 가장 가까운 놓을 수 있는 자리로 옮긴다 (증축으로 생긴 진열대용).</summary>
        void RelocateIfBlocked(ShelfSlot s)
        {
            if (s == null || s.IsBoard) return;
            Vector3 p = s.transform.localPosition;
            float yaw = s.transform.localEulerAngles.y;
            RebuildNav(s);
            if (nav.CanPlace(ShelfBox(p, yaw))) { RebuildNav(); return; }
            for (int r = 1; r <= 24; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        var q = p + new Vector3(dx * 0.25f, 0f, dz * 0.25f);
                        if (!nav.CanPlace(ShelfBox(q, yaw))) continue;
                        s.transform.localPosition = q;
                        placements[s.Index] = new SlotPlacement { index = s.Index, x = q.x, z = q.z, yaw = yaw };
                        RebuildNav();
                        return;
                    }
            RebuildNav();
        }

        /// <summary>
        /// 진열대를 옮긴다 (아침에만). pos는 0.25m 격자, yaw는 90° 단위로 맞춘다.
        /// 벽·다른 진열대·계산 줄과 겹치거나, 놓으면 손님이 못 가는 진열대·계산 줄이 생기면 안 됨.
        /// </summary>
        public bool TryMoveShelf(ShelfSlot slot, Vector3 pos, float yaw, out string reason)
        {
            reason = null;
            if (slot == null || slot.IsBoard) { reason = "이건 옮길 수 없어요"; return false; }
            if (Phase != DayPhase.Morning) { reason = "진열대는 아침에만 옮길 수 있어요"; return false; }
            pos = Layout.Root.InverseTransformPoint(pos);
            pos = new Vector3(Mathf.Round(pos.x * 4f) / 4f, 0f, Mathf.Round(pos.z * 4f) / 4f);
            yaw = Mathf.Repeat(Mathf.Round(yaw / 90f) * 90f, 360f);

            RebuildNav(slot);
            if (!nav.CanPlace(ShelfBox(pos, yaw)))
            {
                RebuildNav();
                reason = "여기엔 못 놓아요 (벽·다른 진열대·계산 줄·문 앞과 겹쳐요)";
                return false;
            }
            var oldPos = slot.transform.localPosition;
            var oldRot = slot.transform.localRotation;
            slot.transform.localPosition = pos;
            slot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            RebuildNav();
            if (!AllReachable(out string blocked))
            {
                slot.transform.localPosition = oldPos;
                slot.transform.localRotation = oldRot;
                RebuildNav();
                reason = blocked;
                return false;
            }
            placements[slot.Index] = new SlotPlacement { index = slot.Index, x = pos.x, z = pos.z, yaw = yaw };
            RebuildDecor();
            Notify();
            Save();
            return true;
        }

        /// <summary>문 안쪽에서 모든 진열대 앞·계산 줄·직원 자리·바깥 길까지 갈 수 있는지.</summary>
        bool AllReachable(out string reason)
        {
            reason = null;
            var seen = nav.ReachableFrom(new Vector3(ShopLayout.DoorX, 0f, ShopLayout.DoorInsideZ));
            foreach (var s in Layout.Slots)
            {
                if (s == null || s.IsBoard) continue;
                if (!nav.IsReachable(seen, s.StandPoint))
                {
                    reason = s.Product != null ? $"{s.Product.displayName} 진열대 앞이 막혀요 (앞쪽을 비워 주세요)" : "진열대 앞이 막혀요 (앞쪽을 비워 주세요)";
                    return false;
                }
            }
            foreach (var q in ShopBuilder.QueueSpots)
                if (!nav.IsReachable(seen, q)) { reason = "계산 줄로 가는 길이 막혀요"; return false; }
            if (!nav.IsReachable(seen, RegisterPoint) || !nav.IsReachable(seen, BackroomPoint))
            { reason = "직원이 다닐 길(계산대 뒤·창고 앞)이 막혀요"; return false; }
            if (!nav.IsReachable(seen, Layout.ExitPoint)) { reason = "문으로 나가는 길이 막혀요"; return false; }
            return true;
        }

        /// <summary>진열대 자리를 처음대로 (옮긴 것 전부 취소). 아침에만.</summary>
        public void ResetShelfPlacements()
        {
            if (!MorningOnly("진열대 되돌리기")) return;
            placements.Clear();
            ShopBuilder.ResetSlotTransforms(Layout);
            RebuildNav();
            RebuildDecor();
            Notify();
        }

        // ───────────── 가게 증축 (docs/06 3단계) · 상권 이동 (docs/07) ─────────────

        /// <summary>지금 동네에서 지은 증축 단계 (0 = 이사 온 그대로, 최대 ExpansionTable.Max).</summary>
        public int Expansion { get; private set; }
        GameObject expansionDecor;
        string upstairsLabel;

        /// <summary>지금 동네(상권) 번호 (0 = 학교 앞 골목). docs/07</summary>
        public int District { get; private set; }

        public DistrictDef CurrentDistrict => ExpansionTable.District(District);

        /// <summary>동네 성향·냉동고에 따른 종류별 수요 배율 (손님이 찾는 물건 가중치에 곱함).</summary>
        float DistrictDemand(ProductData p) => p == null ? 1f : ExpansionTable.Demand(District, Expansion, p.category);

        /// <summary>지금 다음 단계를 지을 수 없는 이유 (지을 수 있으면 null). 돈은 따로 본다.</summary>
        public string ExpansionBlocker()
        {
            var next = ExpansionTable.Next(District, Expansion);
            if (next == null) return "다 지었어요";
            if (Day < next.minDay) return $"{next.minDay}일차부터 지을 수 있어요";
            if (Reputation < next.minReputation) return $"동네 평판 {next.minReputation} 이상 필요 (지금 {Reputation})";
            return null;
        }

        public bool BuyExpansion()
        {
            var next = ExpansionTable.Next(District, Expansion);
            if (next == null || !MorningOnly("가게 증축")) return false;
            string blocker = ExpansionBlocker();
            if (blocker != null) { Fail(blocker); return false; }
            if (Money < next.cost) { Fail("돈이 부족해요"); return false; }
            Money -= next.cost;
            Stats.invested += next.cost;
            Expansion++;
            ApplyUpgrades();
            RebuildDecor();
            Hud.Toast($"가게 증축: {next.name} 완료!");
            UnlockAlbum("first_expand");
            GameEvents.RaiseRushStarted();
            Dialogue.Show("할머니", next.celebrate, UIKit.Accent);
            CheckStoryMilestones();
            Save();
            Notify();
            return true;
        }

        /// <summary>다음 동네로 이사할 수 없는 이유 (가능하면 null). 돈은 따로 본다.</summary>
        public string MoveBlocker()
        {
            var next = ExpansionTable.NextDistrict(District);
            if (next == null) return "가장 큰 동네예요";
            if (Expansion < ExpansionTable.Max) return $"이 동네 증축을 다 지어야 이사할 수 있어요 ({Expansion}/{ExpansionTable.Max})";
            if (Reputation < next.moveMinReputation) return $"동네 평판 {next.moveMinReputation} 이상 필요 (지금 {Reputation})";
            return null;
        }

        /// <summary>
        /// 다음 동네로 이사 (아침에만). 더 큰 건물로 옮기고 증축은 0단계부터.
        /// 기본 진열대 8칸·뽑기판의 진열과 장식은 그대로 가져가고, 옆칸 진열대 물건은 창고로, 그 장식은 값 그대로 돌려받는다.
        /// 직원·업그레이드(간판·선풍기 등)는 그대로, 평판은 반만 이어진다 (새 동네라).
        /// </summary>
        public bool MoveDistrict()
        {
            var next = ExpansionTable.NextDistrict(District);
            if (next == null || !MorningOnly("이사")) return false;
            string blocker = MoveBlocker();
            if (blocker != null) { Fail(blocker); return false; }
            if (Money < next.moveCost) { Fail("돈이 부족해요"); return false; }
            Money -= next.moveCost;
            Stats.invested += next.moveCost;

            int keep = Layout.Board != null ? Layout.Board.Index + 1 : Layout.Slots.Count;
            for (int i = keep; i < Layout.Slots.Count; i++)
            {
                var s = Layout.Slots[i];
                if (s.Product != null && s.Count > 0) AddStock(s.Product, s.Count);
            }
            int refund = 0;
            Facilities.decor.RemoveAll(d =>
            {
                if (d.slot < keep) return false;
                var def = FacilityTable.FindDecor(d.id);
                if (def != null)
                    for (int l = 0; l < d.level && l < def.costs.Length; l++) refund += def.costs[l];
                return true;
            });
            Money += refund;
            ShopBuilder.ClearGroups(Layout);
            { var drop = new List<int>(); foreach (var k in placements.Keys) if (k > Layout.Board.Index) drop.Add(k); foreach (var k in drop) placements.Remove(k); }

            District++;
            Town.districtSinceDay = Day;
            UnlockAlbum("first_move");
            Expansion = 0;
            Reputation = Mathf.Clamp(50 + (Reputation - 50) / 2, 40, 100);
            ApplyDistrictLook();
            ApplyUpgrades();
            if (Hud != null) Hud.RebuildSlotLabels();
            RebuildDecor();
            RebuildStaffAgents();

            Hud.Toast($"{next.name}(으)로 이사 완료!");
            GameEvents.RaiseRushStarted();
            Dialogue.Show("할머니", $"{next.name}(으)로 이사 왔어! {next.blurb}\n옆칸 진열대 물건은 창고에 넣어 뒀어. 새 진열대부터 채워 볼까?", UIKit.Accent);
            if (refund > 0) Dialogue.Show("이삿짐", $"옆칸에 있던 장식은 팔아서 {UIKit.Won(refund)} 돌려받았어요.", UIKit.Blue);
            CheckStoryMilestones();
            Save();
            Notify();
            return true;
        }

        /// <summary>동네 색으로 바닥·벽·차양을 다시 그린다.</summary>
        void ApplyDistrictLook()
        {
            ApplyDistrictShape();
            Visual.ShopDecor.ApplyPalette(CurrentDistrict.palette);
            ShopBuilder.RebuildStatic(Layout, Materials);
        }

#if UNITY_EDITOR || MUNBANGGU_CHEATS
        /// <summary>폰 테스트 빌드 전용 (설정 창 "테스트용" 버튼): 0 돈 +3만, 1 평판 +10, 2 날짜 +3일.</summary>
        public void Cheat(int kind)
        {
            switch (kind)
            {
                case 0: Money += 30000; Hud.Toast("테스트: 돈 +30,000"); break;
                case 1: Reputation = Mathf.Min(100, Reputation + 10); Hud.Toast($"테스트: 평판 {Reputation}"); break;
                default: Day += 3; Hud.Toast($"테스트: {Day}일차로"); break;
            }
            Save();
            Notify();
        }
#endif

        /// <summary>동네·증축에 맞춰 카메라 기본 범위를 넓힌다 (옆칸·2층이 화면에 들어오게). 넓어진 만큼 더 확대할 수 있게.</summary>
        void UpdateCameraFocus()
        {
            if (Layout.Camera == null) return;
            var sc = Layout.Camera.GetComponent<ShopCamera>();
            if (sc == null) return;
            var b = new Bounds(new Vector3(-0.3f, 1.5f, -0.3f), new Vector3(8.2f, 3.8f, 8.6f));
            float baseWidth = b.size.x;
            int bays = ExpansionTable.Bays(District, Expansion);
            if (bays > 0)
            {
                float left = ShopLayout.BayX0(bays) - 0.6f;
                float maxBack = 4f;
                for (int k = 1; k <= bays; k++) maxBack = Mathf.Max(maxBack, ShopLayout.BayBack(k));
                b.Encapsulate(Layout.Root.TransformPoint(new Vector3(left, -0.4f, maxBack - 0.2f)));
                b.Encapsulate(Layout.Root.TransformPoint(new Vector3(left, -0.4f, -4.6f)));
            }
            if (ExpansionTable.Has(District, Expansion, ExpansionFeature.ComicRoom) || ExpansionTable.Has(District, Expansion, ExpansionFeature.Arcade2F))
                b.Encapsulate(Layout.Root.TransformPoint(new Vector3(-5.3f, 5.2f, 3.3f)));
            sc.focus = b;
            sc.maxZoom = ShopCamera.MaxZoom * Mathf.Max(1f, b.size.x / baseWidth);
            sc.Fit();
        }


        /// <summary>오락기: 영업 중 대수만큼 일정 간격으로 동전 수입.</summary>
        void TickArcade()
        {
            int machines = UpgradeTable.ArcadeMachines(Upgrades.Level(UpgradeTable.Arcade));
            if (machines <= 0) return;
            arcadeTimer += Time.deltaTime * machines;
            if (arcadeTimer < UpgradeTable.ArcadePlayInterval) return;
            arcadeTimer -= UpgradeTable.ArcadePlayInterval;
            int coin = UpgradeTable.ArcadeCoin;
            Money += coin;
            Stats.revenue += coin;
            Stats.arcade += coin;
            int which = UnityEngine.Random.Range(0, machines);
            Hud.Fx.CoinBurst(Layout.Root.TransformPoint(Visual.UpgradeVisuals.ArcadePosition(which)) + Vector3.up * 1.4f, coin);
            Notify();
        }

        // ───────────── 시설 배치·콤보 (docs/06 2장) ─────────────

        public FacilityState Facilities { get; private set; } = new FacilityState();
        GameObject decorRoot;
        readonly Dictionary<ProductData, float> comboBoost = new Dictionary<ProductData, float>();
        readonly Dictionary<int, string> slotCombo = new Dictionary<int, string>();
        float decorAppeal;

        /// <summary>장식이 끌어오는 손님 보너스 합 (0.06 = +6%).</summary>
        public float DecorAppeal => decorAppeal;

        /// <summary>그 상품이 콤보로 받는 수요 배율 (없으면 1).</summary>
        float ComboDemand(ProductData p) => p != null && comboBoost.TryGetValue(p, out var v) ? v : 1f;

        /// <summary>그 진열대가 속한 콤보 이름 (없으면 null). 진열대 이름표 표시용.</summary>
        public string SlotCombo(int slotIndex) => slotCombo.TryGetValue(slotIndex, out var n) ? n : null;

        public int DiscoveredComboCount => Facilities.discovered.Count;
        public static int TotalComboCount => FacilityTable.Sets.Length + 4; // 세트 + 코너 4종

        /// <summary>발견한 콤보 이름 목록 (꾸미기 창 도감용).</summary>
        public List<string> DiscoveredComboNames()
        {
            var list = new List<string>();
            foreach (var id in Facilities.discovered)
            {
                var set = System.Array.Find(FacilityTable.Sets, s => s.id == id);
                if (set != null) { list.Add(set.name); continue; }
                foreach (ProductCategory c in System.Enum.GetValues(typeof(ProductCategory)))
                    if (FacilityTable.CornerId(c) == id) list.Add(FacilityTable.CornerName(c));
            }
            return list;
        }

        /// <summary>장식·진열 상태로 콤보를 다시 계산. 처음 이루어진 콤보는 알림.</summary>
        void RecomputeCombos()
        {
            if (Layout == null) return;
            comboBoost.Clear();
            slotCombo.Clear();
            decorAppeal = 0f;
            var slots = Layout.Slots;

            foreach (var d in Facilities.decor)
            {
                var def = FacilityTable.FindDecor(d.id);
                if (def == null || d.slot < 0 || d.slot >= slots.Count) continue;
                decorAppeal += FacilityTable.AppealPerLevel(def) * d.level;
                var p = slots[d.slot].Product;
                if (p == null || def.affinity == null || p.category != def.affinity.Value) continue;
                Boost(p, FacilityTable.CornerBoost(d.level));
                slotCombo[d.slot] = FacilityTable.CornerName(p.category);
                Discover(FacilityTable.CornerId(p.category), FacilityTable.CornerName(p.category),
                    $"{def.name} 덕분에 {Josa.I(p.displayName)} 더 잘 팔려요!");
            }

            foreach (var (a, b) in NeighborPairs())
            {
                if (a >= slots.Count || b >= slots.Count) continue;
                var pa = slots[a].Product;
                var pb = slots[b].Product;
                if (pa == null || pb == null || pa == pb) continue;
                foreach (var set in FacilityTable.Sets)
                {
                    if (!set.Matches(pa.id, pb.id)) continue;
                    Boost(pa, FacilityTable.SetBoost);
                    Boost(pb, FacilityTable.SetBoost);
                    slotCombo[a] = set.name;
                    slotCombo[b] = set.name;
                    Discover(set.id, set.name, set.flavor);
                }
            }
        }

        /// <summary>
        /// 이웃한 진열대 쌍: 뽑기판을 뺀 진열대끼리 FacilityTable.NeighborDistance 안에 있으면 이웃.
        /// (뒷벽 줄·모서리·창가·왼쪽 가장자리·옆칸까지 자리만 보고 정하므로 진열대가 늘어도 그대로 맞음)
        /// </summary>
        public IEnumerable<(int a, int b)> NeighborPairs()
        {
            var slots = Layout.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsBoard) continue;
                for (int j = i + 1; j < slots.Count; j++)
                {
                    if (slots[j].IsBoard) continue;
                    Vector3 d = slots[i].transform.localPosition - slots[j].transform.localPosition;
                    d.y = 0f;
                    if (d.magnitude <= FacilityTable.NeighborDistance) yield return (i, j);
                }
            }
        }

        void Boost(ProductData p, float mul) => comboBoost[p] = ComboDemand(p) * mul;

        void Discover(string id, string name, string flavor)
        {
            if (Facilities.discovered.Contains(id) || !initialized) return;
            Facilities.discovered.Add(id);
            UnlockAlbum("first_combo");
            Hud.Toast($"콤보 발견! {name}", 2.4f);
            Dialogue.Show("콤보 발견!", $"{name} — {flavor}", UIKit.Yellow, "★");
            GameEvents.RaiseRushStarted(); // 경쾌한 효과음 재사용
        }

        /// <summary>진열대에 장식 놓기 (이미 있으면 바꾸기, 이전 것은 절반 환불).</summary>
        public bool PlaceDecor(int slot, string id)
        {
            var def = FacilityTable.FindDecor(id);
            if (def == null || !MorningOnly("장식")) return false;
            int cost = def.costs[0];
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            RemoveDecorInternal(slot, refund: true);
            Money -= cost;
            Stats.invested += cost;
            Facilities.decor.Add(new DecorPlacement { slot = slot, id = id, level = 1 });
            AfterDecorChange($"{Josa.Eul(def.name)} 놓았어요!");
            return true;
        }

        public bool LevelUpDecor(int slot)
        {
            var d = Facilities.At(slot);
            var def = d != null ? FacilityTable.FindDecor(d.id) : null;
            if (def == null || !MorningOnly("장식 레벨업")) return false;
            if (d.level >= def.MaxLevel) { Fail("이미 최고 레벨이에요"); return false; }
            int cost = def.costs[d.level];
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.invested += cost;
            d.level++;
            AfterDecorChange($"{def.name} Lv{d.level}!");
            return true;
        }

        public void RemoveDecor(int slot)
        {
            if (!MorningOnly("장식 치우기")) return;
            if (RemoveDecorInternal(slot, refund: true)) AfterDecorChange("장식을 치웠어요 (절반 돌려받음)");
        }

        bool RemoveDecorInternal(int slot, bool refund)
        {
            var d = Facilities.At(slot);
            if (d == null) return false;
            var def = FacilityTable.FindDecor(d.id);
            if (refund && def != null)
            {
                int spent = 0;
                for (int i = 0; i < d.level && i < def.costs.Length; i++) spent += def.costs[i];
                Money += Mathf.RoundToInt(spent * FacilityTable.RefundRate / 10f) * 10;
            }
            Facilities.decor.Remove(d);
            return true;
        }

        void AfterDecorChange(string toast)
        {
            RebuildDecor();
            Hud.Toast(toast);
            Save();
            Notify();
        }

        void RebuildDecor() => decorRoot = Visual.DecorVisuals.Rebuild(Layout, Materials, Facilities, decorRoot);

        // ───────────── 직원 (docs/06) ─────────────

        public StaffState Staff { get; private set; } = new StaffState();
        readonly Dictionary<StaffMember, float> staffTimers = new Dictionary<StaffMember, float>();
        readonly Dictionary<StaffMember, StaffAgent> staffAgents = new Dictionary<StaffMember, StaffAgent>();
        readonly System.Random staffRng = new System.Random();

        /// <summary>오늘의 지원자 (이미 고용한 사람 제외).</summary>
        public List<StaffMember> StaffCandidates()
        {
            if (Staff.hiredDay != Day) { Staff.hiredDay = Day; Staff.hiredToday.Clear(); }
            var taken = new HashSet<string>(Staff.hiredToday);
            foreach (var m in Staff.members) taken.Add(m.name);
            return StaffRules.Candidates(Day, 3, taken);
        }

        public int DailySalaryTotal()
        {
            int sum = 0;
            foreach (var m in Staff.members) sum += StaffRules.DailySalary(m);
            return sum;
        }

        bool MorningOnly(string what)
        {
            if (Phase == DayPhase.Morning) return true;
            Fail($"{Josa.Eun(what)} 아침에만 할 수 있어요");
            return false;
        }

        public bool HireStaff(StaffMember candidate)
        {
            if (candidate == null || !MorningOnly("직원 고용")) return false;
            if (Staff.members.Count >= Staff.maxStaff) { Fail($"직원은 {Staff.maxStaff}명까지예요 (가게를 넓히면 늘어나요)"); return false; }
            Staff.members.Add(candidate);
            Staff.hiredToday.Add(candidate.name);
            Hud.Toast($"{Josa.I(candidate.name)} 오늘부터 일해요! ({StaffRules.JobName(candidate.job)})");
            RebuildStaffAgents();
            Save();
            Notify();
            return true;
        }

        public void FireStaff(StaffMember m)
        {
            if (m == null || !MorningOnly("직원 내보내기")) return;
            Staff.members.Remove(m);
            Hud.Toast($"{Josa.Wa(m.name)} 작별했어요. 그동안 고마웠어!");
            RebuildStaffAgents();
            Save();
            Notify();
        }

        /// <summary>맡은 일을 다음 일로 바꾼다 (계산 → 진열 → 경비 → 상담 보조).</summary>
        public void CycleStaffJob(StaffMember m)
        {
            if (m == null || !MorningOnly("일 바꾸기")) return;
            m.job = (StaffJob)(((int)m.job + 1) % 4);
            RebuildStaffAgents();
            Save();
            Notify();
        }

        /// <summary>교육: 돈을 내고 바로 레벨업.</summary>
        public bool TrainStaff(StaffMember m)
        {
            if (m == null || !MorningOnly("교육")) return false;
            if (m.level >= StaffRules.MaxLevel) { Fail("이미 최고 레벨이에요"); return false; }
            int cost = StaffRules.TrainCost(m);
            if (Money < cost) { Fail("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.invested += cost;
            m.exp = 0;
            StaffRules.LevelUp(m, staffRng);
            Hud.Toast($"{m.name} 교육 완료! Lv{m.level}");
            Save();
            Notify();
            return true;
        }

        // ── 보따리 상인 (3일마다, 3가지만) ──

        /// <summary>오늘 상인이 왔으면 파는 물건 (안 왔으면 빈 목록).</summary>
        public List<StaffItemDef> MerchantOfferToday()
        {
            if (!StaffRules.IsMerchantDay(Day)) return new List<StaffItemDef>();
            if (Staff.merchantDay != Day) { Staff.merchantDay = Day; Staff.merchantBought.Clear(); }
            return StaffRules.MerchantOffer(Day);
        }

        public bool MerchantSold(string id) => Staff.merchantDay == Day && Staff.merchantBought.Contains(id);

        public bool BuyMerchantItem(string id)
        {
            var def = StaffRules.FindItem(id);
            if (def == null || !MorningOnly("상인에게 사기")) return false;
            var offer = MerchantOfferToday();
            if (!offer.Contains(def)) { Fail("오늘 상인은 그 물건이 없어요"); return false; }
            if (MerchantSold(id)) { Fail("그건 벌써 샀어요 (하나씩만 팔아요)"); return false; }
            if (Money < def.price) { Fail("돈이 부족해요"); return false; }
            Money -= def.price;
            Stats.invested += def.price;
            Staff.ownedItems.Add(id);
            Staff.merchantBought.Add(id);
            Hud.Toast($"{def.name} 샀어요! 직원에게 달아 주세요");
            Save();
            Notify();
            return true;
        }

        /// <summary>예전 방식(바로 사기) — 스크린샷 도구용으로만 남김.</summary>
        public bool BuyStaffItem(string id)
        {
            var def = StaffRules.FindItem(id);
            if (def == null) return false;
            Staff.ownedItems.Add(id);
            Notify();
            return true;
        }

        /// <summary>장착할 아이템을 다음 것으로 (없음 → 가진 아이템 중 남는 것 …). 같은 아이템을 여러 개 가질 수 있음.</summary>
        public void CycleStaffItem(StaffMember m)
        {
            if (m == null || !MorningOnly("아이템 달기")) return;
            var free = new List<string> { "" };
            foreach (var id in Staff.ownedItems)
            {
                if (free.Contains(id)) continue;
                int used = Staff.members.FindAll(o => o != m && o.item == id).Count;
                if (Staff.OwnedCount(id) > used) free.Add(id);
            }
            int idx = free.IndexOf(m.item ?? "");
            m.item = free[(idx + 1) % free.Count];
            RebuildStaffAgents();
            Save();
            Notify();
        }

        /// <summary>능력 포인트 하나를 나눠 준다 (아침).</summary>
        public void AllocateStaffStat(StaffMember m, StaffStat s)
        {
            if (m == null || !MorningOnly("능력 올리기")) return;
            if (!StaffRules.Allocate(m, s)) { Fail(m.points <= 0 ? "포인트가 없어요" : "그 능력은 최고예요"); return; }
            if (s == StaffStat.Walk) RebuildStaffAgents();
            Save();
            Notify();
        }

        /// <summary>특기 하나를 고른다 (Lv3·6·9).</summary>
        public void ChooseStaffPerk(StaffMember m, string perkId)
        {
            if (m == null || m.perkChoices <= 0 || !MorningOnly("특기 고르기")) return;
            var offer = StaffRules.PerkOffer(m);
            if (!offer.Exists(p => p.id == perkId)) return;
            m.perks ??= new List<string>();
            m.perks.Add(perkId);
            m.perkChoices--;
            Hud.Toast($"{Josa.I(m.name)} 「{StaffRules.FindPerk(perkId).name}」 특기를 익혔어요!");
            RebuildStaffAgents();
            Save();
            Notify();
        }

        /// <summary>경험치 → 레벨업 (포인트·특기).</summary>
        void GiveStaffExp(StaffMember m, int amount)
        {
            if (m.level >= StaffRules.MaxLevel) return;
            m.exp += amount;
            if (m.exp < StaffRules.ExpToNext(m.level)) return;
            m.exp = 0;
            StaffRules.LevelUp(m, staffRng);
            Hud.Toast(m.perkChoices > 0 ? $"{m.name} Lv{m.level}! 특기를 고를 수 있어요 (아침 직원 창)" : $"{m.name} 레벨업! Lv{m.level} · 능력 포인트 +{m.points}");
            if (staffAgents.TryGetValue(m, out var a)) a.Say($"레벨업! Lv{m.level}", UIKit.BubbleMoney, 1.6f);
        }

        /// <summary>직원들을 가게에 (다시) 세운다. 맡은 일별 순번으로 자리 결정.</summary>
        void RebuildStaffAgents()
        {
            foreach (var a in staffAgents.Values) if (a != null) Destroy(a.gameObject);
            registerStaff = null;
            restockTargets.Clear();
            staffAgents.Clear();
            var order = new Dictionary<StaffJob, int>();
            foreach (var m in Staff.members)
            {
                order.TryGetValue(m.job, out int n);
                order[m.job] = n + 1;
                staffAgents[m] = StaffAgent.Create(Layout.Root, Materials, Hud, m, n);
            }
        }

        StaffMember FirstOnJob(StaffJob job)
        {
            foreach (var m in Staff.members) if (m.job == job) return m;
            return null;
        }

        // ── 걸어 다니는 직원 (계산·진열 담당은 둘 다 함. 맡은 일이 먼저) ──
        StaffMember registerStaff;   // 지금 계산대에 서 있거나 가는 직원
        float registerIdle;          // 계산대에서 줄이 빈 채로 기다린 시간
        readonly HashSet<ShelfSlot> restockTargets = new HashSet<ShelfSlot>(); // 누가 채우러 가는 중인 매대

        /// <summary>직원이 계산하는 자리 (할머니 옆, 계산대 뒤).</summary>
        public static readonly Vector3 RegisterPoint = new Vector3(-3.6f, 0f, -0.45f);
        /// <summary>물건 가지러 가는 창고 상자 앞.</summary>
        public static readonly Vector3 BackroomPoint = new Vector3(-3.3f, 0f, 2.45f);

        /// <summary>
        /// 영업 중 직원 일. 계산·진열 담당은 가게를 돌아다니며
        /// · 줄이 있고 계산대가 비었으면 → 계산대로 걸어가서 계산 (줄이 비면 잠시 뒤 다른 일)
        /// · 반 넘게 빈 매대가 있으면 → 창고에 가서 물건을 들고 → 매대로 가서 채움
        /// 계산 담당은 계산이 먼저, 진열 담당은 진열이 먼저. 경비·상담 보조는 손님 쪽에서 물어봄.
        /// </summary>
        void TickStaff()
        {
            float dt = Time.deltaTime;
            if (registerStaff != null && !Staff.members.Contains(registerStaff)) registerStaff = null;
            foreach (var m in Staff.members)
            {
                if (m.job != StaffJob.Cashier && m.job != StaffJob.Stocker) continue;
                if (!staffAgents.TryGetValue(m, out var a) || a == null) continue;
                staffTimers.TryGetValue(m, out float t);
                t += dt;

                if (registerStaff == m)
                {
                    if (!a.Busy) // 계산대에 서 있음
                    {
                        var front = Layout.Queue.Front;
                        if (front != null)
                        {
                            registerIdle = 0f;
                            if (t >= StaffRules.CashierInterval(m) && front.TryAutoCheckout($"{Josa.I(m.name)} 계산해 줬어요"))
                            {
                                t = 0f;
                                GiveStaffExp(m, 1);
                                if (UnityEngine.Random.value < StaffRules.TipChance(m))
                                {
                                    int tip = 50;
                                    Money += tip; Stats.revenue += tip; Stats.tips += tip;
                                    a.Say($"친절하다고 팁 +{UIKit.Won(tip)}", UIKit.BubbleMoney);
                                }
                            }
                        }
                        else
                        {
                            registerIdle += dt;
                            // 줄이 비면 잠시 기다렸다가 다른 일 (진열 담당은 빨리 떠남)
                            bool restockNeeded = SlotToRestock() != null;
                            float wait = m.job == StaffJob.Cashier ? (restockNeeded ? 3f : 8f) : 1.5f;
                            if (registerIdle > wait) { registerStaff = null; t = 0f; }
                        }
                    }
                    staffTimers[m] = t;
                    continue;
                }

                if (a.Busy) { staffTimers[m] = t; continue; } // 걷는 중 (진열 심부름 등)

                bool canRegister = Layout.Queue.Front != null && registerStaff == null;
                var slot = t >= StaffRules.StockerInterval(m) * 0.4f ? SlotToRestock() : null;
                bool cashierFirst = m.job == StaffJob.Cashier;

                if (canRegister && (cashierFirst || slot == null))
                    GoToRegister(m, a, ref t);
                else if (slot != null)
                    GoRestock(m, a, slot, ref t);
                else if (canRegister)
                    GoToRegister(m, a, ref t);
                else if (t > 2.5f && !a.AtHome)
                    a.GoHome();
                staffTimers[m] = t;
            }
        }

        void GoToRegister(StaffMember m, StaffAgent a, ref float t)
        {
            registerStaff = m;
            registerIdle = 0f;
            t = 0f;
            a.Say("계산할게요!", UIKit.BubbleNormal, 0.8f);
            a.GoTo(RegisterPoint, null, ShopBuilder.QueueSpots[0]);
        }

        void GoRestock(StaffMember m, StaffAgent a, ShelfSlot slot, ref float t)
        {
            restockTargets.Add(slot);
            t = 0f;
            a.Say($"{slot.Product.displayName} 가지러!", UIKit.BubbleNormal, 0.8f);
            var member = m;
            // 창고 상자 앞 → 매대 앞 → 채움 → 그 자리에서 다음 일 (일이 없으면 잠시 뒤 쉬는 자리로)
            a.GoTo(BackroomPoint, () =>
            {
                if (slot == null) { restockTargets.Remove(slot); return; }
                a.GoTo(slot.StandPoint, () =>
                {
                    // 매대 앞에서 손 빠르기만큼 시간을 들여 채운다
                    a.Work(StaffRules.RestockTime(member), () =>
                    {
                        restockTargets.Remove(slot);
                        RestockSlot(member, slot);
                    });
                }, slot != null ? slot.transform.position : (Vector3?)null);
            });
        }

        /// <summary>창고에 같은 상품이 있고 반 넘게 빈 매대 (제일 빈 곳, 이미 누가 가는 곳 빼고).</summary>
        ShelfSlot SlotToRestock()
        {
            ShelfSlot best = null;
            foreach (var s in Layout.Slots)
            {
                if (s == null || s.IsBoard || s.Product == null || s.Count * 2 > s.Capacity || Stock(s.Product) <= 0) continue;
                if (restockTargets.Contains(s)) continue;
                if (best == null || s.Count < best.Count) best = s;
            }
            return best;
        }

        void RestockSlot(StaffMember m, ShelfSlot slot)
        {
            if (slot == null || slot.Product == null) return;
            var p = slot.Product;
            int added = slot.Add(p, Mathf.Min(StaffRules.StockerBatch(m), Stock(p)));
            if (added <= 0) return;
            RemoveStock(p, added);
            GameEvents.RaiseItemsPlaced(slot, p, added);
            GiveStaffExp(m, 1);
            if (staffAgents.TryGetValue(m, out var a)) a.Say($"{p.displayName} +{added}", UIKit.BubbleNormal, 0.9f);
            Notify();
        }

        /// <summary>몰래 나가는 아이가 문에 닿았을 때 경비가 잡는지. 잡았으면 그 직원.</summary>
        public StaffMember TryGuardCatch()
        {
            foreach (var m in Staff.members)
            {
                if (m.job != StaffJob.Guard) continue;
                if (UnityEngine.Random.value < StaffRules.CatchChance(m))
                {
                    GiveStaffExp(m, 2);
                    if (staffAgents.TryGetValue(m, out var a)) a.Say("거기 서!", UIKit.BubbleUrgent, 1.2f);
                    return m;
                }
            }
            return null;
        }

        /// <summary>상담 손님 인내심 배율 (상담 보조 친절).</summary>
        public float CounselPatienceBonus
        {
            get
            {
                var m = FirstOnJob(StaffJob.Counselor);
                return (m != null ? StaffRules.CounselPatience(m) : 1f) * ComicCounselPatience;
            }
        }

        /// <summary>상담 보조의 ★추천 (없으면 null). 정확도만큼 딱 맞는 걸, 아니면 아무거나 추천.</summary>
        public ProductData CounselRecommendation(CounselRequest r, out string who)
        {
            who = null;
            var m = FirstOnJob(StaffJob.Counselor);
            if (m == null || r == null) return null;
            who = m.name;
            var shelved = ShelvedCounselProducts();
            shelved.RemoveAll(p => p.retailPrice > r.budget);
            if (shelved.Count == 0) return null;
            if (UnityEngine.Random.value < StaffRules.RecommendAccuracy(m))
            {
                foreach (var p in shelved)
                    if (CounselRequests.Judge(r, p) == CounselResult.Perfect) { GiveStaffExp(m, 1); return p; }
                foreach (var p in shelved)
                    if (CounselRequests.Judge(r, p) == CounselResult.Ok) { GiveStaffExp(m, 1); return p; }
            }
            return shelved[UnityEngine.Random.Range(0, shelved.Count)];
        }

        // ───────────── 돌발 사건 ─────────────

        /// <summary>처음 보는 사건이면 한 번만 설명을 띄운다 (세이브에 기록).</summary>
        public void ShowEventTip(CustomerKind kind)
        {
            string key = kind.ToString();
            if (seenEvents.Contains(key)) return;
            seenEvents.Add(key);
            string text = kind switch
            {
                CustomerKind.Sneaky => "앗, 저 아이가 계산 안 하고 나가려 해요! 문을 나가기 전에 보라색 말풍선을 탭해서 잡아요.",
                CustomerKind.Credit => "외상을 해 달래요. 해 주면 내일 갚을 수도(덤까지!), 떼먹을 수도 있어요.",
                CustomerKind.Counsel => "아이가 고민을 들고 계산대로 왔어요! 말풍선을 탭하면 무슨 고민인지 듣고, 매대에 있는 것 중에서 골라 줄 수 있어요. 딱 맞게 골라 주면 팁도 받고 동네 평판이 올라요.",
                CustomerKind.Crying => "돈을 잃어버린 아이가 울고 있어요. 말풍선을 탭하면 제일 싼 물건 하나를 주고 달래요. 고마워서 친구들을 데려올지도?",
                _ => null
            };
            if (text != null) Dialogue.Show("가게 일지", text, UIKit.Accent, "!");
        }

        /// <summary>몰래 들고 나가 버렸다.</summary>
        public void OnStolen(ProductData p)
        {
            if (p == null) return;
            Stats.stolen++;
            Stats.stolenValue += p.retailPrice;
            Hud.Toast($"{p.displayName} 하나를 슬쩍 당했어요!");
            GameEvents.RaiseInvalidAction();
            Notify();
        }

        /// <summary>몰래 나가던 아이를 잡음 → 제값 받기.</summary>
        public int CatchThief(Customer c)
        {
            Stats.caught++;
            return Checkout(c);
        }

        /// <summary>외상 해 주기: 돈은 내일 이후에.</summary>
        public void GiveCredit(Customer c)
        {
            if (c.Carried == null) return;
            int price = c.Carried.retailPrice;
            ledger.Add(price, Day + 1);
            Stats.credits++;
            Stats.creditValue += price;
            Stats.served++;
            RecordSold(c.Carried);
            Notify();
        }

        /// <summary>안 사고 간 물건을 창고로 (인내심 손님 통계 없이).</summary>
        public void ReturnItem(ProductData p)
        {
            AddStock(p, 1);
            Notify();
        }

        /// <summary>우는 아이에게 줄 제일 싼 물건을 매대에서 하나 꺼낸다 (없으면 null).</summary>
        public ProductData GiveFreebie()
        {
            ShelfSlot cheapest = null;
            foreach (var s in Layout.Slots)
                if (s.Product != null && s.Count > 0 && (cheapest == null || s.Product.retailPrice < cheapest.Product.retailPrice))
                    cheapest = s;
            if (cheapest == null) return null;
            var p = cheapest.Product;
            cheapest.TakeOne();
            Stats.comforted++;
            Notify();
            return p;
        }

        /// <summary>달래 준 아이가 친구들을 데려온다 (잠시 뒤 차례로 입장).</summary>
        public void ScheduleFriends(int count)
        {
            friendsPending += count;
            friendsTimer = 2.5f;
        }

        // ───────────── 손님이 부르는 함수 ─────────────

        public ShelfSlot FindSlotWith(ProductData p)
        {
            ShelfSlot best = null;
            foreach (var s in Layout.Slots)
                if (s.Product == p && s.Count > 0 && (best == null || s.Count > best.Count))
                    best = s;
            return best;
        }

        public ShelfSlot RandomStockedSlot()
        {
            var candidates = Layout.Slots.FindAll(s => s.Product != null && s.Count > 0 && !s.IsBoard);
            return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>계산. 단골이 얹어 준 돈(팁)을 돌려준다 (일반 손님은 0). 팁도 매출에 포함.</summary>
        public int Checkout(Customer c)
        {
            if (c.Carried == null) return 0;
            int price = c.Carried.retailPrice;
            int tip = 0;
            if (c.Carried == Trend) Stats.trendSold++;

            var r = c.Regular;
            if (r != null)
            {
                tip = r.tip * GrandmaRegularTipMultiplier;
                string title = RegularTitle(r);
                var (line, completed) = regulars.OnServed(r, c.Carried, Day, out int moment);
                if (moment == 2)
                {
                    // 어른이 되어 찾아옴: 반 친구들 몫까지 한가득 + 넉넉한 마음
                    tip += AdultGift;
                    Dialogue.Show($"{r.name} · {r.adultTitle}", line, r.color);
                    Dialogue.Show("추억", $"{Josa.I(r.name)} 반 아이들 몫이라며 {UIKit.Won(AdultGift)}을 더 내고 갔어요. 할머니 가게가 누군가의 추억이 됐어요.", UIKit.Accent, "♥");
                    AddReputation(5);
                    UnlockAlbum("adult");
                }
                else if (moment == 1)
                {
                    Dialogue.Show(title, line, r.color);
                    UnlockAlbum("graduation");
                    Dialogue.Show("졸업", $"{Josa.I(r.name)} 이제 안 와요… 언젠가 어른이 되어 다시 찾아올지도 몰라요.", UIKit.Muted, "졸");
                }
                else
                {
                    if (line != null) Dialogue.Show(title, line, r.color);
                    if (completed && !string.IsNullOrEmpty(r.completeToast))
                        Dialogue.Show("단골 이야기 완성!", r.completeToast, UIKit.Green, "★");
                }
            }

            int bonus = GrandmaBonusSale(c.Carried); // 할머니 "덤 하나 더"
            price += bonus;
            Money += price + tip;
            Stats.revenue += price + tip;
            Stats.tips += tip;
            Stats.served++;
            RecordSold(c.Carried);
            if (c.SpecialId != null) OnSpecialServed(c, c.Carried);
            Notify();
            return tip;
        }

        /// <summary>단골이 물건이 없거나 오래 기다려서 그냥 감 → 친밀도 감소.</summary>
        public void OnRegularUnhappy(Customer c)
        {
            if (c != null && c.Regular != null) regulars.OnUnhappy(c.Regular);
        }

        public void ReturnUnpaid(ProductData p)
        {
            AddStock(p, 1);
            Stats.lostImpatient++;
            Notify();
        }

        public void RecordLostNoStock() => Stats.lostNoStock++;

        public void OnCustomerGone(Customer c) => customers.Remove(c);

        // ───────────── 랜덤 ─────────────

        /// <summary>
        /// 손님이 찾는 물건. 해금된 상품 중 인기 × (붐 종료 폭락) × (단골 완성 보너스) 가중치.
        /// 붐 상품은 전체 손님 중 trends.Share 비율이 찾도록 가중치를 맞춘다.
        /// </summary>
        ProductData RollWanted()
        {
            var trend = ActiveTrend();
            float trendWeight = TrendWeight(trend, out float others);
            float r = UnityEngine.Random.value * (others + trendWeight);
            ProductData last = null;
            foreach (var p in Catalog.products)
            {
                if (!IsUnlocked(p)) continue;
                last = p;
                r -= p == trend ? trendWeight : BaseWeight(p);
                if (r <= 0f) return p;
            }
            return last ?? Catalog.products[0];
        }

        /// <summary>그 상품을 찾는 손님 비율 추정 (0~1, 해금 안 됐으면 0). UI 힌트·디버그용.</summary>
        public float DemandShare(ProductData p)
        {
            if (!IsUnlocked(p)) return 0f;
            var trend = ActiveTrend();
            float trendWeight = TrendWeight(trend, out float others);
            float total = others + trendWeight;
            return total <= 0f ? 0f : (p == trend ? trendWeight : BaseWeight(p)) / total;
        }

        ProductData ActiveTrend() => IsUnlocked(Trend) ? Trend : null;

        float BaseWeight(ProductData p) => p.popularity * trends.PopularityMultiplier(p) * regulars.FavoriteBoost(p) * BoardAppeal(p) * ComboDemand(p) * DistrictDemand(p) * TownDemand(p);

        // ───────────── 번호 뽑기판 ─────────────

        public const string BoardProductId = "ppopgi";
        public static bool IsBoardProduct(ProductData p) => p != null && p.id == BoardProductId;

        /// <summary>1등이 걸린 판은 인기, 1등이 빠진 판은 시들 (새 판을 들일 타이밍 고민거리).</summary>
        float BoardAppeal(ProductData p)
        {
            if (!IsBoardProduct(p) || Layout?.Board == null || Layout.Board.Count == 0) return 1f;
            return Layout.Board.GrandPrizeLeft ? 1.25f : 0.35f;
        }

        /// <summary>뽑기 한 판: 100원 받고 결과 등수(0 꽝, 1~4등). 1등이면 소문나서 아이들이 몰려온다.</summary>
        public int PullTicket(Customer c, ShelfSlot board)
        {
            int price = c.Carried != null ? c.Carried.retailPrice : 0;
            Money += price;
            Stats.revenue += price;
            Stats.served++;
            RecordSold(c.Carried);
            Stats.pulls++;
            int tier = board.RollPrize();
            if (tier == 1)
            {
                Stats.jackpots++;
                Hud.Toast("1등 당첨!! 소문 듣고 아이들이 몰려와요");
                Hud.Fx.CoinBurst(board.transform.position + Vector3.up * 1.6f, 1000);
                ScheduleFriends(3);
            }
            Notify();
            return tier;
        }

        /// <summary>붐 상품 가중치. others = 붐 상품을 뺀 해금 상품 가중치 합.</summary>
        float TrendWeight(ProductData trend, out float others)
        {
            others = 0f;
            foreach (var p in Catalog.products)
                if (IsUnlocked(p) && p != trend) others += BaseWeight(p);
            if (trend == null) return 0f;
            float share = Mathf.Clamp(trends.Share, 0f, 0.9f);
            return Mathf.Max(BaseWeight(trend), others * share / (1f - share));
        }

        // ───────────── 저장 ─────────────

        void LoadOrNew()
        {
            var data = SaveSystem.Load();
            if (data == null)
            {
                freshGame = true;
                Day = 1;
                Money = startMoney;
                return;
            }
            Day = Mathf.Max(1, data.day);
            Money = data.money;
            // 업그레이드 먼저 (매대 칸 수·추가 진열대가 있어야 저장된 진열을 그대로 올릴 수 있음)
            // 예전 "알바생" 업그레이드를 산 적 있으면 무료 직원으로 바꿔 준다 (직원 시스템으로 대체)
            Staff = data.staff ?? new StaffState();
            // 예전 세이브: 걸음 능력이 없던 직원은 레벨에 맞춰 채움, 특기 목록 준비
            foreach (var sm in Staff.members) { if (sm.walk <= 0) sm.walk = Mathf.Min(StaffRules.StatCap, 2 + sm.level / 2); sm.perks ??= new List<string>(); }
            Staff.merchantBought ??= new List<string>();
            if (Staff.members.Count == 0 && data.upgrades != null)
                foreach (var u in data.upgrades)
                    if (u.id == UpgradeTable.Helper && u.level > 0)
                        Staff.members.Add(new StaffMember { name = "알바생", seed = 424242, level = u.level * 2, speed = 3 + u.level * 2, kindness = 3, sharp = 2, job = StaffJob.Cashier });
            Upgrades.Load(data.upgrades);
            Facilities = data.facilities ?? new FacilityState();
            District = Mathf.Clamp(data.district, 0, ExpansionTable.Districts.Length - 1);
            ApplyDistrictShape();
            if (District > 0) ApplyDistrictLook();
            placements.Clear();
            if (data.placements != null) foreach (var p in data.placements) placements[p.index] = p;
            Expansion = Mathf.Clamp(data.expansion, 0, ExpansionTable.Max);
            // 나중에 붙은 진열대는 저장 때 붙어 있던 순서 그대로 (진열 인덱스가 어긋나지 않게)
            if (data.slotGroups != null)
                foreach (var g in data.slotGroups)
                    ShopBuilder.AddShelfGroup(Layout, Materials, UpgradeTable.ShelfCapacity(Upgrades.Level(UpgradeTable.ShelfSize)), g);
            ApplyUpgrades();
            foreach (var e in data.backroom) AddStock(Catalog.Find(e.productId), e.count);
            for (int i = 0; i < Layout.Slots.Count && i < data.shelves.Count; i++)
                Layout.Slots[i].Load(Catalog.Find(data.shelves[i].productId), data.shelves[i].count);
            trends.Load(data.trend);
            regulars.Load(data.regulars);
            ledger.Load(data.credits);
            Reputation = data.reputation > 0 ? Mathf.Clamp(data.reputation, 0, 100) : 50;
            knownTraits.Clear();
            if (data.knownTraits != null) foreach (var t in data.knownTraits) knownTraits.Add(t);
            seenEvents.Clear();
            LoadTown(data.town);
            if (data.seenEvents != null) foreach (var s in data.seenEvents) seenEvents.Add(s);
        }

        public void Save()
        {
            if (!initialized) return;
            var stock = new Dictionary<ProductData, int>(backroom);
            // 손님이 들고 있던 물건은 창고로 되돌린 상태로 저장
            foreach (var c in customers)
                if (c != null && c.Carried != null)
                    stock[c.Carried] = (stock.TryGetValue(c.Carried, out var n) ? n : 0) + 1;

            var data = new SaveData
            {
                day = Day, money = Money, trend = trends.State, regulars = regulars.Save(),
                credits = ledger.Save(), seenEvents = new List<string>(seenEvents),
                upgrades = Upgrades.Save(), staff = Staff, facilities = Facilities,
                reputation = Mathf.Max(1, Reputation), knownTraits = new List<string>(knownTraits),
                district = District, expansion = Expansion, slotGroups = new List<string>(Layout.Groups),
                placements = new List<SlotPlacement>(placements.Values),
                town = Town
            };
            foreach (var kv in stock)
                if (kv.Value > 0) data.backroom.Add(new StockEntry { productId = kv.Key.id, count = kv.Value });
            foreach (var s in Layout.Slots)
                data.shelves.Add(new StockEntry { productId = s.Product != null ? s.Product.id : "", count = s.Count });
            SaveSystem.Save(data);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        void Fail(string message)
        {
            Hud.Toast(message);
            GameEvents.RaiseInvalidAction();
        }

        void Notify()
        {
            RecomputeCombos();
            Changed?.Invoke();
        }
    }
}
