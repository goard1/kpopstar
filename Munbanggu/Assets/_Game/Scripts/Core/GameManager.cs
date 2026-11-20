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
        public int Profit => revenue - cost;
    }

    /// <summary>하루 루프, 돈, 창고 재고, 손님 생성을 관리.</summary>
    public class GameManager : MonoBehaviour
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
            bool paused = Hud != null && (Hud.IsSettingsOpen || Hud.IsChoiceOpen);
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
            : openDuration <= 0f ? 1f : Mathf.Clamp01(openTimer / openDuration);
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
            Catalog = catalog;
            trends = new TrendSystem(trendConfig, catalog);
            Materials = new MaterialCache(baseMaterial);
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
            Speed = Mathf.Clamp(PlayerPrefs.GetInt(SpeedPref, 1), 1, MaxSpeed);
            initialized = true;
            StartMorning();
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
            if (Stats.regularsVisited > 0) lines.Add(SettlementLine.Count("단골 방문", Stats.regularsVisited));
            if (Stats.tips > 0) lines.Add(SettlementLine.Money("그중 단골 팁", Stats.tips));
            if (Stats.caught > 0) lines.Add(SettlementLine.Count("몰래 나가던 아이 잡음", Stats.caught));
            if (Stats.stolen > 0) lines.Add(SettlementLine.Money($"슬쩍 당함 ({Stats.stolen}개)", Stats.stolenValue, true));
            if (Stats.credits > 0) lines.Add(SettlementLine.Money($"외상 해 줌 ({Stats.credits}건, 내일 받을 돈)", Stats.creditValue));
            if (Stats.comforted > 0) lines.Add(SettlementLine.Count("달래 준 아이", Stats.comforted));
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
        public bool IsUnlocked(ProductData p) => p != null && p.unlockDay <= Day;

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
            int cost = p.BoxCost;
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
            Stats = new DayStats();
            trends.Advance(Day, (p, d) => p.unlockDay <= d);
            regulars.PlanDay(Day, UnlockedFavorite);
            // 외상 정리는 저장 전에 (다시 켜도 두 번 받지 않게)
            creditNews.Clear();
            int repaid = ledger.Settle(Day, creditNews);
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
            Save();
            GameEvents.RaisePhaseChanged(Phase);
            Notify();
            Hud.ShowSettlement();
        }

        void Update()
        {
            if (!initialized) return;
            ApplySpeed();
            if (Phase == DayPhase.Open)
            {
                openTimer += Time.deltaTime;
                if (IsRush && !rushAnnounced)
                {
                    rushAnnounced = true;
                    Hud.Toast("하교 시간! 손님이 몰려와요");
                    GameEvents.RaiseRushStarted();
                }
                SpawnTick();
                if (openTimer >= openDuration)
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
            if (customers.Count >= maxCustomersInShop) return;

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
            rate *= Mathf.Min(maxSpawnGrowth, 1f + (Day - 1) * dailySpawnGrowth);
            spawnAcc += rate * Time.deltaTime;
            if (spawnAcc < 1f) return;
            spawnAcc -= 1f;
            SpawnCustomer(null);
        }

        void SpawnCustomer(RegularProfile regular, CustomerKind kind = CustomerKind.Normal)
        {
            ProductData wanted = null;
            if (regular != null && UnityEngine.Random.value < regular.favoriteChance)
                wanted = UnlockedFavorite(regular);
            if (wanted == null) wanted = RollWanted();

            var go = new GameObject(regular != null ? $"Regular {regular.id}" : "Customer");
            go.transform.SetParent(transform, false);
            go.transform.position = Layout.EntrancePoint;
            var c = go.AddComponent<Customer>();
            customers.Add(c);
            Stats.visitors++;
            if (regular != null)
            {
                Stats.regularsVisited++;
                string intro = regulars.OnEnter(regular, Day);
                if (intro != null) Dialogue.Show($"{regular.name} · {regular.title}", intro, regular.color);
            }
            c.Init(this, wanted, Hud.CreateBubble(go.transform), regular, kind);
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
                tip = r.tip;
                var (line, completed) = regulars.OnServed(r, c.Carried);
                if (line != null) Dialogue.Show($"{r.name} · {r.title}", line, r.color);
                if (completed && !string.IsNullOrEmpty(r.completeToast))
                    Dialogue.Show("단골 이야기 완성!", r.completeToast, UIKit.Green, "★");
            }

            Money += price + tip;
            Stats.revenue += price + tip;
            Stats.tips += tip;
            Stats.served++;
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

        float BaseWeight(ProductData p) => p.popularity * trends.PopularityMultiplier(p) * regulars.FavoriteBoost(p) * BoardAppeal(p);

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
                Day = 1;
                Money = startMoney;
                return;
            }
            Day = Mathf.Max(1, data.day);
            Money = data.money;
            foreach (var e in data.backroom) AddStock(Catalog.Find(e.productId), e.count);
            for (int i = 0; i < Layout.Slots.Count && i < data.shelves.Count; i++)
                Layout.Slots[i].Load(Catalog.Find(data.shelves[i].productId), data.shelves[i].count);
            trends.Load(data.trend);
            regulars.Load(data.regulars);
            ledger.Load(data.credits);
            seenEvents.Clear();
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
                credits = ledger.Save(), seenEvents = new List<string>(seenEvents)
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

        void Notify() => Changed?.Invoke();
    }
}
