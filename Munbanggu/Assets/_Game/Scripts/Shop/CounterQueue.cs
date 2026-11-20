using System.Collections.Generic;
using Munbanggu.Customers;
using UnityEngine;

namespace Munbanggu.Shop
{
    /// <summary>계산대 줄. 맨 앞 손님만 계산 가능.</summary>
    public class CounterQueue
    {
        readonly List<Vector3> spots;
        readonly List<Customer> line = new List<Customer>();

        /// <summary>줄 선 손님이 바라보는 방향 (계산대 쪽).</summary>
        public Vector3 Facing { get; }

        public CounterQueue(List<Vector3> spots, Vector3 facing)
        {
            this.spots = spots;
            Facing = facing;
        }

        public Customer Front => line.Count > 0 ? line[0] : null;

        public void Join(Customer c)
        {
            if (!line.Contains(c)) line.Add(c);
        }

        public void Leave(Customer c) => line.Remove(c);

        public Vector3 SpotFor(Customer c)
        {
            int i = line.IndexOf(c);
            if (i < 0) i = line.Count;
            if (i < spots.Count) return spots[i];
            Vector3 last = spots[spots.Count - 1];
            Vector3 dir = (last - spots[spots.Count - 2]).normalized;
            return last + dir * 0.75f * (i - spots.Count + 1);
        }
    }
}
