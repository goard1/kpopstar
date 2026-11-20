using System;
using System.Collections.Generic;
using Munbanggu.Customers;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>
    /// 돌발 사건 예약. 영업 시작 때 오늘 일어날 사건(특별한 손님)과 시각(영업 진행 0~1)을 정한다.
    /// 1일차는 없음 (튜토리얼), 2일차 1건, 3일차부터 우는 아이 추가·2건, 5일차부터 3건.
    /// </summary>
    public class DayEvents
    {
        const float Earliest = 0.12f, Latest = 0.85f;

        readonly List<(float at, CustomerKind kind)> planned = new List<(float, CustomerKind)>();

        public void Plan(int day)
        {
            planned.Clear();
            if (day < 2) return;
            int count = day < 3 ? 1 : day < 5 ? 2 : 3;
            var pool = new List<CustomerKind> { CustomerKind.Sneaky, CustomerKind.Credit };
            if (day >= 3) pool.Add(CustomerKind.Crying);

            CustomerKind last = CustomerKind.Normal;
            for (int i = 0; i < count; i++)
            {
                // 같은 사건이 연달아 나오지 않게
                CustomerKind kind;
                int guard = 0;
                do kind = pool[UnityEngine.Random.Range(0, pool.Count)];
                while (kind == last && pool.Count > 1 && ++guard < 8);
                last = kind;
                // 하루를 count칸으로 나눠 칸마다 하나씩 (겹치지 않게)
                float at = Mathf.Lerp(Earliest, Latest, (i + UnityEngine.Random.Range(0.15f, 0.85f)) / count);
                planned.Add((at, kind));
            }
        }

        /// <summary>시각이 된 사건을 하나 꺼낸다 (없으면 Normal).</summary>
        public CustomerKind TakeDue(float progress)
        {
            if (planned.Count == 0 || progress < planned[0].at) return CustomerKind.Normal;
            var kind = planned[0].kind;
            planned.RemoveAt(0);
            return kind;
        }

        public void Clear() => planned.Clear();
    }

    [Serializable]
    public class CreditEntry
    {
        public int amount;   // 외상 금액
        public int dueDay;   // 갚으러 오는 날
    }

    /// <summary>외상 장부. 해준 외상은 다음 날 아침에 갚거나(이자 덤) 떼먹는다.</summary>
    public class CreditLedger
    {
        public const float RepayBonus = 0.3f; // "엄마가 고맙다고 더 주래요"

        readonly List<CreditEntry> entries = new List<CreditEntry>();

        public int Count => entries.Count;

        public void Add(int amount, int dueDay) => entries.Add(new CreditEntry { amount = amount, dueDay = dueDay });

        /// <summary>오늘까지 갚을 외상을 정리. 돌려받은 돈 합계와 아침 소식 문장들.</summary>
        public int Settle(int day, List<string> news, float repayChance)
        {
            int received = 0, repaid = 0, defaulted = 0, lost = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.dueDay > day) continue;
                entries.RemoveAt(i);
                if (UnityEngine.Random.value < repayChance)
                {
                    int pay = Mathf.RoundToInt(e.amount * (1f + RepayBonus) / 10f) * 10;
                    received += pay;
                    repaid++;
                }
                else
                {
                    defaulted++;
                    lost += e.amount;
                }
            }
            if (repaid > 0) news.Add($"외상 {repaid}건을 갚으러 왔어요! 고맙다고 덤까지 +{received:N0}원");
            if (defaulted > 0) news.Add($"외상 {defaulted}건({lost:N0}원)은... 안 갚았네요. 장부에서 지울 수밖에요.");
            return received;
        }

        public List<CreditEntry> Save() => new List<CreditEntry>(entries);

        public void Load(List<CreditEntry> saved)
        {
            entries.Clear();
            if (saved != null) entries.AddRange(saved);
        }
    }
}
