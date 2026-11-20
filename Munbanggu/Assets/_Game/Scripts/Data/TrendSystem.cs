using System;
using System.Collections.Generic;

namespace Munbanggu.Data
{
    public enum TrendStage
    {
        None,     // 지금 붐 없음
        Rising,   // 막 뜨는 중
        Peak,     // 절정
        Falling   // 식는 중 (곧 끝남 → 재고 조심)
    }

    /// <summary>유행 진행 상태 (세이브됨).</summary>
    [Serializable]
    public class TrendState
    {
        public int processedDay;          // 마지막으로 진행시킨 날 (같은 날 다시 불러와도 두 번 진행하지 않게)

        public string boomId = "";        // 지금 붐 상품 (없으면 "")
        public int boomStart;
        public int boomLength;

        public string nextId = "";        // 다음 붐으로 소문난 상품
        public int nextStart;
        public int nextLength;
        public bool nextFake;             // 헛소문이면 시작일에 다른 상품이 붐

        public string crashId = "";       // 붐이 끝나 수요가 떨어진 상품
        public int crashUntil;            // 이 날 전까지 폭락

        public List<string> history = new List<string>(); // 지난 붐 (최근이 뒤)
    }

    /// <summary>
    /// 주간 붐 일정: 소문(예고) → 시작 → 정점 → 하락 → 종료 후 수요 급감.
    /// 아침마다 Advance(day)를 한 번 부른다. 수치는 TrendConfig.
    /// </summary>
    public class TrendSystem
    {
        readonly TrendConfig cfg;
        readonly ProductCatalog catalog;
        readonly Random rng = new Random();
        TrendState s = new TrendState();
        readonly List<string> news = new List<string>();

        public TrendSystem(TrendConfig config, ProductCatalog productCatalog)
        {
            cfg = config;
            catalog = productCatalog;
        }

        public TrendState State => s;

        /// <summary>지금 붐인 상품 (없으면 null).</summary>
        public ProductData Current { get; private set; }
        public TrendStage Stage { get; private set; }
        /// <summary>손님 중 붐 상품을 찾는 비율 (0~1).</summary>
        public float Share { get; private set; }
        /// <summary>아침에 소문이 도는 다음 붐 상품 (아직 소문이 안 났으면 null). 헛소문일 수 있음.</summary>
        public ProductData Rumor { get; private set; }
        /// <summary>다음 붐까지 남은 날 (소문이 있을 때만 의미).</summary>
        public int RumorDaysLeft { get; private set; }
        /// <summary>붐이 끝나 안 팔리는 상품 (없으면 null).</summary>
        public ProductData Crashed { get; private set; }

        /// <summary>오늘 아침 새로 생긴 일 (붐 시작·종료·헛소문). 같은 날 다시 불러오면 비어 있음.</summary>
        public IReadOnlyList<string> News => news;

        public void Load(TrendState state)
        {
            s = state ?? new TrendState();
            s.history ??= new List<string>();
            s.boomId ??= ""; s.nextId ??= ""; s.crashId ??= "";
        }

        /// <summary>그 날 아침 처리. isUnlockedOn(상품, 날짜) = 그 날짜에 도매상에 있는지.</summary>
        public void Advance(int day, Func<ProductData, int, bool> isUnlockedOn)
        {
            news.Clear();
            if (s.processedDay >= day) { Refresh(day); return; }

            if (s.processedDay == 0)
            {
                // 새 게임(또는 v1 세이브): 오늘 바로 붐 하나로 시작 → 1일차부터 유행 개념을 보여줌
                var first = PickProduct(day, isUnlockedOn, null);
                if (first != null) StartBoom(first.id, day, RollLength());
            }
            else
            {
                // 1) 붐 종료
                if (BoomActive && day >= s.boomStart + s.boomLength)
                {
                    var ended = catalog.Find(s.boomId);
                    s.crashId = s.boomId;
                    s.crashUntil = day + cfg.crashDays;
                    s.boomId = "";
                    if (ended != null) news.Add($"{ended.displayName} 유행이 끝났어요… 이제 애들이 잘 안 찾아요");
                }
                // 2) 다음 붐 시작
                if (!BoomActive && !string.IsNullOrEmpty(s.nextId) && day >= s.nextStart)
                {
                    string id = s.nextId;
                    if (s.nextFake)
                    {
                        var rumored = catalog.Find(s.nextId);
                        var actual = PickProduct(day, isUnlockedOn, s.nextId);
                        if (actual != null)
                        {
                            id = actual.id;
                            if (rumored != null) news.Add($"{rumored.displayName} 소문은 헛소문이었나 봐요!");
                        }
                    }
                    StartBoom(id, day, s.nextLength);
                    s.nextId = "";
                    var started = catalog.Find(id);
                    if (started != null) news.Add($"{started.displayName} 붐 시작! 애들이 너도나도 찾아요");
                }
            }

            // 3) 다음 붐 예약 (소문은 시작 rumorDays일 전부터 보임)
            if (string.IsNullOrEmpty(s.nextId))
            {
                int after = BoomActive ? s.boomStart + s.boomLength : day;
                int start = Math.Max(day + 1, after + rng.Next(cfg.minGapDays, Math.Max(cfg.minGapDays, cfg.maxGapDays) + 1));
                var next = PickProduct(start, isUnlockedOn, null);
                if (next != null)
                {
                    s.nextId = next.id;
                    s.nextStart = start;
                    s.nextLength = RollLength();
                    s.nextFake = rng.NextDouble() > cfg.rumorReliability;
                }
            }

            s.processedDay = day;
            Refresh(day);
        }

        /// <summary>인기 배수 (붐 종료 후 폭락만. 붐 상품 자체는 Share로 따로 계산).</summary>
        public float PopularityMultiplier(ProductData p) => p != null && p == Crashed ? cfg.crashMultiplier : 1f;

        /// <summary>아침에 보여줄 유행 소식 (새 소식 + 현재 상황 + 소문).</summary>
        public List<string> MorningLines()
        {
            var lines = new List<string>(news);
            // 오늘 막 시작/종료된 상품은 새 소식에 이미 나왔으니 상황 줄은 생략
            if (Current != null && !news.Exists(l => l.StartsWith(Current.displayName + " 붐 시작")))
                lines.Add(StatusLine());
            if (Crashed != null && !news.Exists(l => l.StartsWith(Crashed.displayName + " 유행이 끝")))
                lines.Add($"{Josa.Eun(Crashed.displayName)} 이제 한물갔대요. 재고를 줄이세요");
            string rumor = RumorLine();
            if (rumor != null) lines.Add(rumor);
            return lines;
        }

        /// <summary>지금 붐 상황 한 줄 (없으면 null). UI 상시 표시용.</summary>
        public string StatusLine()
        {
            if (Current == null) return null;
            string n = Current.displayName;
            return Stage switch
            {
                TrendStage.Rising => $"요즘 애들이 {Josa.Eul(n)} 찾기 시작했어요!",
                TrendStage.Peak => $"{n} 붐이 절정이에요! 넉넉히 진열하세요",
                TrendStage.Falling => $"{n} 인기가 한풀 꺾이고 있어요… 사재기는 조심!",
                _ => null
            };
        }

        /// <summary>다음 붐 소문 한 줄 (없으면 null).</summary>
        public string RumorLine()
        {
            if (Rumor == null) return null;
            return RumorDaysLeft <= 1
                ? $"곧 {Rumor.displayName} 붐이 올 거라는 소문이에요!"
                : $"요즘 애들 사이에 {Rumor.displayName} 소문이…";
        }

        public string StageLabel => Stage switch
        {
            TrendStage.Rising => "상승",
            TrendStage.Peak => "절정",
            TrendStage.Falling => "하락",
            _ => ""
        };

        // ───────────── 내부 ─────────────

        bool BoomActive => !string.IsNullOrEmpty(s.boomId);

        void StartBoom(string id, int day, int length)
        {
            s.boomId = id;
            s.boomStart = day;
            s.boomLength = Math.Max(1, length);
            if (s.crashId == id) s.crashId = ""; // 다시 붐이면 폭락 해제
            s.history.Add(id);
            while (s.history.Count > 8) s.history.RemoveAt(0);
        }

        int RollLength() => rng.Next(cfg.minBoomDays, Math.Max(cfg.minBoomDays, cfg.maxBoomDays) + 1);

        ProductData PickProduct(int onDay, Func<ProductData, int, bool> isUnlockedOn, string exclude)
        {
            var pool = new List<ProductData>();
            for (int pass = 0; pass < 2 && pool.Count == 0; pass++)
            {
                foreach (var p in catalog.products)
                {
                    if (p == null || p.id == exclude || !isUnlockedOn(p, onDay)) continue;
                    if (p.id == s.boomId || p.id == s.crashId) continue;
                    if (pass == 0 && RecentlyBoomed(p.id)) continue; // 두 번째 시도에선 최근 붐도 허용
                    pool.Add(p);
                }
            }
            return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
        }

        bool RecentlyBoomed(string id)
        {
            for (int i = s.history.Count - 1, n = 0; i >= 0 && n < cfg.noRepeatCount; i--, n++)
                if (s.history[i] == id) return true;
            return false;
        }

        void Refresh(int day)
        {
            Current = BoomActive && day < s.boomStart + s.boomLength ? catalog.Find(s.boomId) : null;
            if (Current != null)
            {
                int i = day - s.boomStart;
                float curve = cfg.Curve(i, s.boomLength);
                Share = cfg.ShareFor(i, s.boomLength);
                bool beforePeak = (i + 0.5f) / s.boomLength < cfg.peakAt;
                Stage = curve >= cfg.peakStageThreshold ? TrendStage.Peak
                    : beforePeak ? TrendStage.Rising : TrendStage.Falling;
            }
            else
            {
                Share = 0f;
                Stage = TrendStage.None;
            }

            Crashed = !string.IsNullOrEmpty(s.crashId) && day < s.crashUntil ? catalog.Find(s.crashId) : null;

            Rumor = null;
            RumorDaysLeft = 0;
            if (!string.IsNullOrEmpty(s.nextId) && day >= s.nextStart - cfg.rumorDays && day < s.nextStart)
            {
                Rumor = catalog.Find(s.nextId);
                RumorDaysLeft = s.nextStart - day;
            }
        }
    }
}
