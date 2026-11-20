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
        public int Profit => revenue - cost;
    }

    /// <summary>하루 루프, 돈, 창고 재고, 손님 생성을 관리.</summary>
    public class GameManager : MonoBehaviour
    {
        [Header("밸런스 (나중에 Remote Config로 옮길 값)")]
        public int startMoney = 5000;
        public float openDuration = 150f;
        public float queuePatience = 16f;
        public int maxCustomersInShop = 8;
        public float baseSpawnPerSec = 0.14f;
        public float rushSpawnPerSec = 0.5f;
        public float trendMultiplier = 3f;
        [Range(0f, 1f)] public float alternativeChance = 0.5f;

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
        public ProductData Trend { get; private set; }
        public ProductData Selected { get; private set; }

        public float OpenProgress => Phase == DayPhase.Morning ? 0f
            : openDuration <= 0f ? 1f : Mathf.Clamp01(openTimer / openDuration);
        public bool IsRush => Phase == DayPhase.Open && OpenProgress >= RushStart && OpenProgress < RushEnd;

        public event Action Changed;

        readonly Dictionary<ProductData, int> backroom = new Dictionary<ProductData, int>();
        readonly List<Customer> customers = new List<Customer>();
        float openTimer;
        float spawnAcc;
        bool rushAnnounced;
        bool initialized;

        public void Init(ProductCatalog catalog, Material baseMaterial)
        {
            Catalog = catalog;
            Materials = new MaterialCache(baseMaterial);
            Layout = ShopBuilder.Build(transform, Materials);
            Hud = HUD.Build(this);
            gameObject.AddComponent<TapInput>().Init(this);
            LoadOrNew();
            initialized = true;
            StartMorning();
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
            if (Phase != DayPhase.Morning) { Hud.Toast("도매상은 아침에만 열어요"); return false; }
            int cost = p.BoxCost;
            if (Money < cost) { Hud.Toast("돈이 부족해요"); return false; }
            Money -= cost;
            Stats.cost += cost;
            AddStock(p, p.boxSize);
            if (Selected == null) Selected = p;
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
                Hud.Toast("아래 창고에서 상품을 먼저 고르세요");
                return;
            }
            if (Stock(Selected) <= 0)
            {
                Hud.Toast($"창고에 {Selected.displayName} 재고가 없어요");
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
                Hud.Toast("매대가 꽉 찼어요");
                return;
            }
            int added = slot.Add(Selected, Stock(Selected));
            RemoveStock(Selected, added);
            Notify();
        }

        public void OpenShop()
        {
            if (Phase != DayPhase.Morning) return;
            if (TotalShelfStock() == 0) { Hud.Toast("매대에 물건을 먼저 진열하세요"); return; }
            Phase = DayPhase.Open;
            openTimer = 0f;
            spawnAcc = 0.8f; // 문 열자마자 첫 손님
            rushAnnounced = false;
            Hud.CloseModals();
            Save();
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
            Trend = RollTrend();
            Save();
            Notify();
            Hud.ShowMorning();
        }

        void EnterSettlement()
        {
            Phase = DayPhase.Settlement;
            Save();
            Notify();
            Hud.ShowSettlement();
        }

        void Update()
        {
            if (!initialized) return;
            if (Phase == DayPhase.Open)
            {
                openTimer += Time.deltaTime;
                if (IsRush && !rushAnnounced)
                {
                    rushAnnounced = true;
                    Hud.Toast("하교 시간! 손님이 몰려와요");
                }
                SpawnTick();
                if (openTimer >= openDuration)
                {
                    Phase = DayPhase.Closing;
                    Hud.Toast("문 닫을 시간! 남은 손님만 계산해요");
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
            float rate = IsRush ? rushSpawnPerSec : baseSpawnPerSec;
            rate *= Mathf.Min(2f, 1f + (Day - 1) * 0.05f);
            spawnAcc += rate * Time.deltaTime;
            if (spawnAcc < 1f) return;
            spawnAcc -= 1f;

            var go = new GameObject("Customer");
            go.transform.SetParent(transform, false);
            go.transform.position = Layout.EntrancePoint;
            var c = go.AddComponent<Customer>();
            customers.Add(c);
            Stats.visitors++;
            c.Init(this, RollWanted(), Hud.CreateBubble(go.transform));
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
            var candidates = Layout.Slots.FindAll(s => s.Product != null && s.Count > 0);
            return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        public void Checkout(Customer c)
        {
            if (c.Carried == null) return;
            Money += c.Carried.retailPrice;
            Stats.revenue += c.Carried.retailPrice;
            Stats.served++;
            Notify();
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

        ProductData RollWanted()
        {
            float total = 0f;
            foreach (var p in Catalog.products) total += Weight(p);
            float r = UnityEngine.Random.value * total;
            foreach (var p in Catalog.products)
            {
                r -= Weight(p);
                if (r <= 0f) return p;
            }
            return Catalog.products[Catalog.products.Count - 1];
        }

        float Weight(ProductData p) => p.popularity * (p == Trend ? trendMultiplier : 1f);

        ProductData RollTrend() => Catalog.products[UnityEngine.Random.Range(0, Catalog.products.Count)];

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
        }

        public void Save()
        {
            if (!initialized) return;
            var stock = new Dictionary<ProductData, int>(backroom);
            // 손님이 들고 있던 물건은 창고로 되돌린 상태로 저장
            foreach (var c in customers)
                if (c != null && c.Carried != null)
                    stock[c.Carried] = (stock.TryGetValue(c.Carried, out var n) ? n : 0) + 1;

            var data = new SaveData { day = Day, money = Money };
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

        void Notify() => Changed?.Invoke();
    }
}
