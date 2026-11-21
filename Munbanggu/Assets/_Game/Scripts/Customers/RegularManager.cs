using System;
using System.Collections.Generic;
using Munbanggu.Data;

namespace Munbanggu.Customers
{
    /// <summary>단골 한 명의 진행도 (세이브됨).</summary>
    [Serializable]
    public class RegularState
    {
        public string id;
        public int affection;     // 친밀도
        public int seen;          // 지금까지 본 이야기 단계 수
        public int visits;        // 방문 횟수
        public int served;        // 계산까지 한 횟수
        public int lastVisitDay;
        // ── 성장 (docs/08) ──
        public int metDay;        // 처음 만난 날 (학년 계산)
        public int completedDay;  // 이야기를 다 본 날
        public bool graduated;    // 졸업(이사)해서 더는 안 옴
        public int graduatedDay;
        public bool adultVisited; // 어른이 되어 한 번 찾아옴
    }

    /// <summary>
    /// 단골 방문 계획·친밀도·이야기 진행.
    /// 친밀도: 좋아하는 상품을 사 가면 +2, 다른 걸 사 가면 +1, 그냥 가면 -1.
    /// 이야기를 다 보면 그 단골이 좋아하는 상품 수요가 늘고 더 자주 온다.
    /// </summary>
    public class RegularManager
    {
        public const int AffectionFavorite = 2;
        public const int AffectionOther = 1;
        public const int AffectionUnhappy = -1;
        public const float CompletedVisitBonus = 0.15f;
        public const float CompletedFavoriteBoost = 1.25f;

        readonly Dictionary<string, RegularState> states = new Dictionary<string, RegularState>();
        readonly List<(RegularProfile profile, float at)> planned = new List<(RegularProfile, float)>();
        readonly HashSet<string> adultToday = new HashSet<string>();

        /// <summary>졸업 인사까지 이야기 완성 뒤 며칠, 어른이 되어 오기까지 졸업 뒤 며칠.</summary>
        public const int FarewellAfterDays = 8, AdultAfterDays = 14;

        /// <summary>학년 (처음 만날 때 3학년, 10일마다 한 학년, 최대 6학년).</summary>
        public int Grade(RegularProfile r, int day)
        {
            var s = StateOf(r);
            if (s.metDay <= 0) return 3;
            return Math.Min(6, 3 + (day - s.metDay) / 10);
        }

        /// <summary>오늘 오는 이 단골이 어른이 되어 오는 것인지.</summary>
        public bool IsAdultVisit(RegularProfile r) => r != null && adultToday.Contains(r.id);
        readonly Random rng = new Random();

        public void Load(List<RegularState> saved)
        {
            states.Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (s != null && !string.IsNullOrEmpty(s.id) && RegularRoster.Find(s.id) != null)
                    states[s.id] = s;
        }

        public List<RegularState> Save() => new List<RegularState>(states.Values);

        public RegularState StateOf(RegularProfile r)
        {
            if (!states.TryGetValue(r.id, out var s)) states[r.id] = s = new RegularState { id = r.id };
            return s;
        }

        /// <summary>이야기를 끝까지 봤는지.</summary>
        public bool IsComplete(RegularProfile r) => r.story != null && StateOf(r).seen >= r.story.Length;

        /// <summary>현재 이야기 단계 (0 = 아직 안 만남). UI 표시용.</summary>
        public int Stage(RegularProfile r) => StateOf(r).seen;

        /// <summary>아침에 오늘 올 단골과 시각을 정한다. favorite = 좋아하는 상품 (해금 안 됐으면 null).</summary>
        public void PlanDay(int day, Func<RegularProfile, ProductData> unlockedFavorite)
        {
            planned.Clear();
            adultToday.Clear();
            foreach (var r in RegularRoster.All)
            {
                if (day < r.minDay || unlockedFavorite(r) == null) continue;
                var st = StateOf(r);
                if (st.graduated)
                {
                    // 졸업한 아이는 안 오다가, 한참 뒤 어른이 되어 딱 한 번 찾아온다
                    if (!st.adultVisited && day >= st.graduatedDay + AdultAfterDays && !string.IsNullOrEmpty(r.adultLine))
                    {
                        adultToday.Add(r.id);
                        planned.Add((r, 0.3f + (float)rng.NextDouble() * 0.3f));
                    }
                    continue;
                }
                float chance = r.visitChance + (IsComplete(r) ? CompletedVisitBonus : 0f);
                // 아직 안 만난 단골은 조건이 되는 첫날 반드시 온다 (첫 만남을 놓치지 않게)
                if (StateOf(r).visits == 0) chance = 1f;
                if (rng.NextDouble() >= chance) continue;
                float at = r.windowStart + (float)rng.NextDouble() * Math.Max(0f, r.windowEnd - r.windowStart);
                planned.Add((r, at));
            }
        }

        /// <summary>영업 진행도가 방문 시각을 지난 단골을 하나 꺼낸다 (없으면 null).</summary>
        public RegularProfile TakeDue(float progress)
        {
            for (int i = 0; i < planned.Count; i++)
            {
                if (progress < planned[i].at) continue;
                var r = planned[i].profile;
                planned.RemoveAt(i);
                return r;
            }
            return null;
        }

        /// <summary>입장. 첫 만남이면 소개 대사를 돌려준다 (아니면 null).</summary>
        public string OnEnter(RegularProfile r, int day)
        {
            var s = StateOf(r);
            s.visits++;
            s.lastVisitDay = day;
            if (s.metDay <= 0) s.metDay = day;
            if (adultToday.Contains(r.id)) return null; // 어른 방문 대사는 계산 때
            if (s.seen == 0 && r.story != null && r.story.Length > 0)
            {
                s.seen = 1;
                return r.story[0].line;
            }
            return null;
        }

        /// <summary>계산 완료. 새 이야기 단계가 열리면 그 대사를, 방금 이야기를 다 봤으면 completed=true.</summary>
        public (string line, bool completed) OnServed(RegularProfile r, ProductData bought) => OnServed(r, bought, 0, out _);

        /// <summary>
        /// 계산 완료 (성장 포함). moment: 0 보통, 1 졸업 인사(이번이 마지막 방문), 2 어른이 되어 찾아옴.
        /// </summary>
        public (string line, bool completed) OnServed(RegularProfile r, ProductData bought, int day, out int moment)
        {
            moment = 0;
            var s = StateOf(r);
            if (adultToday.Contains(r.id))
            {
                adultToday.Remove(r.id);
                s.adultVisited = true;
                moment = 2;
                return (r.adultLine, false);
            }
            if (IsComplete(r) && s.completedDay <= 0 && day > 0) s.completedDay = day; // 예전 세이브: 오늘부터 셈
            if (IsComplete(r) && !s.graduated && day > 0 && s.completedDay > 0 && day >= s.completedDay + FarewellAfterDays
                && !string.IsNullOrEmpty(r.farewellLine))
            {
                s.graduated = true;
                s.graduatedDay = day;
                moment = 1;
                return (r.farewellLine, false);
            }
            s.served++;
            s.affection += bought != null && bought.id == r.favoriteId ? AffectionFavorite : AffectionOther;
            if (r.story == null || s.seen >= r.story.Length) return (null, false);
            var beat = r.story[s.seen];
            if (s.affection < beat.affection) return (null, false);
            s.seen++;
            bool done = s.seen >= r.story.Length;
            if (done && day > 0) s.completedDay = day;
            return (beat.line, done);
        }

        public void OnUnhappy(RegularProfile r)
        {
            var s = StateOf(r);
            s.affection = Math.Max(0, s.affection + AffectionUnhappy);
        }

        /// <summary>이야기를 끝낸 단골이 좋아하는 상품의 수요 배수.</summary>
        public float FavoriteBoost(ProductData p)
        {
            if (p == null) return 1f;
            float m = 1f;
            foreach (var r in RegularRoster.All)
                if (r.favoriteId == p.id && IsComplete(r)) m *= CompletedFavoriteBoost;
            return m;
        }
    }
}
