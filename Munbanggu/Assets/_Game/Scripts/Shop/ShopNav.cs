using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Shop
{
    /// <summary>
    /// 가게 바닥 격자 길찾기 (0.25m 칸, 8방향 A*). 손님·직원이 진열대를 피해 다닌다.
    /// 바닥(가게 안 + 옆칸 + 앞 보도)만 걸을 수 있고, 가게 앞 선(z = -4)은 문 틈으로만 지난다.
    /// 진열대·계산대 같은 장애물은 몸 반지름만큼 넓혀서 막는다.
    /// 진열대를 옮기거나 가게가 바뀌면 GameManager가 Rebuild를 다시 부른다 (Version이 오르면 걷던 길도 다시 찾음).
    /// </summary>
    public sealed class ShopNav
    {
        public static ShopNav Current;

        public const float Cell = 0.25f;
        public const float AgentRadius = 0.18f;
        public const float FrontZ = -4f;
        public const float DoorX0 = 0.7f, DoorX1 = 1.75f;

        /// <summary>장애물 한 개 (가운데, 크기, 회전).</summary>
        public struct Box
        {
            public Vector2 center, size; // x, z
            public float yaw;
            public Box(Vector2 c, Vector2 s, float yaw = 0f) { center = c; size = s; this.yaw = yaw; }
        }

        public int Version { get; private set; }

        float minX, minZ;
        int w, h;
        bool[] floor;     // 바닥 (걸을 수 있는 자리, 장애물 전)
        bool[] inside;    // 가게 안 바닥 (진열대를 놓을 수 있는 곳)
        bool[] walk;      // 지금 걸을 수 있는 칸
        bool[] solid;     // 장애물 몸통 (넓히지 않은 것) — 진열대 놓기 겹침 확인용
        bool[] reserved;  // 진열대를 놓으면 안 되는 자리 (계산 줄, 문 앞)

        readonly List<Rect> floors = new List<Rect>();
        readonly List<Rect> insides = new List<Rect>();
        readonly List<Box> statics = new List<Box>();
        readonly List<Rect> reserves = new List<Rect>();

        // A* 작업 배열 (세대 번호로 재사용)
        float[] g;
        int[] parent, stamp, closedStamp;
        int gen;

        // ───────────── 만들기 ─────────────

        public void Begin()
        {
            floors.Clear(); insides.Clear(); statics.Clear(); reserves.Clear();
        }

        /// <summary>가게 안 바닥 (진열대를 놓을 수 있는 곳).</summary>
        public void AddInside(Rect r) { insides.Add(r); floors.Add(r); }
        /// <summary>걸을 수만 있는 바닥 (가게 앞 보도).</summary>
        public void AddOutside(Rect r) => floors.Add(r);
        public void AddStatic(Box b) => statics.Add(b);
        public void AddReserved(Rect r) => reserves.Add(r);

        /// <summary>바닥·고정 장애물·움직이는 장애물(진열대)로 격자를 만든다.</summary>
        public void Rebuild(IEnumerable<Box> movables)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var r in floors) { x0 = Mathf.Min(x0, r.xMin); z0 = Mathf.Min(z0, r.yMin); x1 = Mathf.Max(x1, r.xMax); z1 = Mathf.Max(z1, r.yMax); }
            if (floors.Count == 0) { x0 = z0 = -1; x1 = z1 = 1; }
            minX = Mathf.Floor(x0 / Cell) * Cell;
            minZ = Mathf.Floor(z0 / Cell) * Cell;
            w = Mathf.CeilToInt((x1 - minX) / Cell);
            h = Mathf.CeilToInt((z1 - minZ) / Cell);
            int n = w * h;
            if (floor == null || floor.Length != n)
            {
                floor = new bool[n]; inside = new bool[n]; walk = new bool[n]; solid = new bool[n]; reserved = new bool[n];
                g = new float[n]; parent = new int[n]; stamp = new int[n]; closedStamp = new int[n];
                gen = 0;
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 c = CellCenter(i);
                floor[i] = Contains(floors, c);
                inside[i] = Contains(insides, c);
                // 가게 앞 선: 문 틈 말고는 못 지나감
                if (floor[i] && Mathf.Abs(c.y - FrontZ) < 0.2f && (c.x < DoorX0 || c.x > DoorX1)) floor[i] = false;
                walk[i] = floor[i];
                solid[i] = false;
                reserved[i] = Contains(reserves, c);
            }
            foreach (var b in statics) Stamp(b);
            if (movables != null) foreach (var b in movables) Stamp(b);
            Version++;
        }

        static bool Contains(List<Rect> rs, Vector2 p)
        {
            foreach (var r in rs) if (r.Contains(p)) return true;
            return false;
        }

        void Stamp(Box b)
        {
            ForCells(b, AgentRadius, i => walk[i] = false);
            ForCells(b, 0f, i => solid[i] = true);
        }

        /// <summary>회전된 상자(+여유)에 가운데가 들어가는 칸마다 f.</summary>
        void ForCells(Box b, float pad, System.Action<int> f)
        {
            float rad = b.yaw * Mathf.Deg2Rad; // 월드→로컬: lx = x·cos − z·sin, lz = x·sin + z·cos (Unity yaw 기준)
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float hx = b.size.x * 0.5f + pad, hz = b.size.y * 0.5f + pad;
            float reach = Mathf.Sqrt(hx * hx + hz * hz);
            int cx0 = Mathf.Max(0, Mathf.FloorToInt((b.center.x - reach - minX) / Cell));
            int cx1 = Mathf.Min(w - 1, Mathf.CeilToInt((b.center.x + reach - minX) / Cell));
            int cz0 = Mathf.Max(0, Mathf.FloorToInt((b.center.y - reach - minZ) / Cell));
            int cz1 = Mathf.Min(h - 1, Mathf.CeilToInt((b.center.y + reach - minZ) / Cell));
            for (int z = cz0; z <= cz1; z++)
                for (int x = cx0; x <= cx1; x++)
                {
                    Vector2 c = new Vector2(minX + (x + 0.5f) * Cell, minZ + (z + 0.5f) * Cell) - b.center;
                    // 상자 로컬로 돌림
                    float lx = c.x * cos - c.y * sin, lz = c.x * sin + c.y * cos;
                    if (Mathf.Abs(lx) <= hx && Mathf.Abs(lz) <= hz) f(z * w + x);
                }
        }

        // ───────────── 칸 계산 ─────────────

        Vector2 CellCenter(int i) => new Vector2(minX + (i % w + 0.5f) * Cell, minZ + (i / w + 0.5f) * Cell);
        Vector3 CellWorld(int i) { var c = CellCenter(i); return new Vector3(c.x, 0f, c.y); }

        int IndexOf(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - minX) / Cell), z = Mathf.FloorToInt((p.z - minZ) / Cell);
            if (x < 0 || z < 0 || x >= w || z >= h) return -1;
            return z * w + x;
        }

        public bool Walkable(Vector3 p) { int i = IndexOf(p); return i >= 0 && walk[i]; }

        /// <summary>p에서 가장 가까운 걸을 수 있는 칸 (없으면 -1).</summary>
        int Nearest(Vector3 p, int maxRing = 16)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((p.x - minX) / Cell), 0, w - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((p.z - minZ) / Cell), 0, h - 1);
            if (walk[z0 * w + x0]) return z0 * w + x0;
            int best = -1; float bestD = float.MaxValue;
            for (int r = 1; r <= maxRing; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        int x = x0 + dx, z = z0 + dz;
                        if (x < 0 || z < 0 || x >= w || z >= h) continue;
                        int i = z * w + x;
                        if (!walk[i]) continue;
                        float d = (CellWorld(i) - new Vector3(p.x, 0f, p.z)).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                if (best >= 0) return best;
            }
            return -1;
        }

        // ───────────── 길찾기 ─────────────

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>
        /// from → to 길 (월드 좌표 경유점들, 마지막은 to 또는 to에서 가장 가까운 걸을 수 있는 곳).
        /// 길이 없으면 to 하나만 (직선으로 감).
        /// </summary>
        public void FindPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            result.Clear();
            int s = Nearest(from), t = Nearest(to);
            if (s < 0 || t < 0) { result.Add(to); return; }
            bool toWalkable = Walkable(to);
            if (s == t) { result.Add(toWalkable ? to : CellWorld(t)); return; }

            gen++;
            var open = heap;
            open.Clear();
            g[s] = 0f; parent[s] = -1; stamp[s] = gen;
            open.Push(s, H(s, t));
            bool found = false;
            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (closedStamp[cur] == gen) continue;
                closedStamp[cur] = gen;
                if (cur == t) { found = true; break; }
                int cx = cur % w, cz = cur / w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], nz = cz + DZ[d];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int ni = nz * w + nx;
                    if (!walk[ni] || closedStamp[ni] == gen) continue;
                    if (d >= 4 && (!walk[cz * w + nx] || !walk[nz * w + cx])) continue; // 모서리 자르기 금지
                    float ng = g[cur] + (d >= 4 ? 1.4142f : 1f);
                    if (stamp[ni] == gen && ng >= g[ni]) continue;
                    stamp[ni] = gen; g[ni] = ng; parent[ni] = cur;
                    open.Push(ni, ng + H(ni, t));
                }
            }
            if (!found) { result.Add(to); return; }

            // 칸 목록 → 직선으로 볼 수 있는 곳까지 건너뛰며 줄이기
            var cells = cellBuf;
            cells.Clear();
            for (int c = t; c >= 0; c = parent[c]) cells.Add(c);
            cells.Reverse();
            Vector3 cursor = from;
            int k = 0;
            while (k < cells.Count - 1)
            {
                int far = k + 1;
                for (int j = cells.Count - 1; j > k + 1; j--)
                    if (LineWalkable(cursor, CellWorld(cells[j]))) { far = j; break; }
                cursor = CellWorld(cells[far]);
                if (far < cells.Count - 1) result.Add(cursor);
                k = far;
            }
            result.Add(toWalkable ? to : CellWorld(t));
        }

        float H(int a, int b)
        {
            int dx = Mathf.Abs(a % w - b % w), dz = Mathf.Abs(a / w - b / w);
            return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz);
        }

        bool LineWalkable(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            int steps = Mathf.CeilToInt(len / (Cell * 0.5f));
            for (int i = 1; i < steps; i++)
            {
                int c = IndexOf(a + d * (i / (float)steps));
                if (c < 0 || !walk[c]) return false;
            }
            return true;
        }

        readonly MinHeap heap = new MinHeap();
        readonly List<int> cellBuf = new List<int>();

        sealed class MinHeap
        {
            readonly List<(float f, int i)> items = new List<(float, int)>();
            public int Count => items.Count;
            public void Clear() => items.Clear();
            public void Push(int i, float f)
            {
                items.Add((f, i));
                int c = items.Count - 1;
                while (c > 0)
                {
                    int p = (c - 1) / 2;
                    if (items[p].f <= items[c].f) break;
                    (items[p], items[c]) = (items[c], items[p]);
                    c = p;
                }
            }
            public int Pop()
            {
                int top = items[0].i;
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, m = c;
                    if (l < items.Count && items[l].f < items[m].f) m = l;
                    if (r < items.Count && items[r].f < items[m].f) m = r;
                    if (m == c) break;
                    (items[m], items[c]) = (items[c], items[m]);
                    c = m;
                }
                return top;
            }
        }

        // ───────────── 진열대 놓기 확인 ─────────────

        /// <summary>
        /// 상자를 여기 놓을 수 있나: 몸통 칸이 전부 가게 안 바닥이고, 다른 장애물·예약 자리와 안 겹침.
        /// (지금 격자에 그 진열대 자신이 들어 있으면 먼저 빼고 Rebuild한 격자로 확인할 것)
        /// </summary>
        public bool CanPlace(Box b)
        {
            bool ok = true;
            ForCells(b, 0.02f, i => { if (!inside[i] || solid[i] || reserved[i]) ok = false; });
            // 가장자리가 격자 밖으로 나가면 ForCells가 못 셈 → 네 모서리도 확인
            float rad = b.yaw * Mathf.Deg2Rad;
            Vector2 ax = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * (b.size.x * 0.5f);
            Vector2 az = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (b.size.y * 0.5f);
            foreach (var corner in new[] { b.center + ax + az, b.center + ax - az, b.center - ax + az, b.center - ax - az })
            {
                int i = IndexOf(new Vector3(corner.x, 0f, corner.y));
                if (i < 0 || !inside[i]) ok = false;
            }
            return ok;
        }

        /// <summary>from에서 갈 수 있는 칸인지 (홍수 채우기).</summary>
        public bool[] ReachableFrom(Vector3 from)
        {
            var seen = new bool[w * h];
            int s = Nearest(from);
            if (s < 0) return seen;
            var q = new Queue<int>();
            q.Enqueue(s); seen[s] = true;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % w, cz = cur / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d], nz = cz + DZ[d];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int ni = nz * w + nx;
                    if (seen[ni] || !walk[ni]) continue;
                    seen[ni] = true;
                    q.Enqueue(ni);
                }
            }
            return seen;
        }

        /// <summary>ReachableFrom 결과에서 p 자리(그 칸이 걸을 수 있어야 함)에 갈 수 있는지.</summary>
        public bool IsReachable(bool[] seen, Vector3 p)
        {
            int i = IndexOf(p);
            return i >= 0 && walk[i] && seen[i];
        }
    }

    /// <summary>
    /// 한 사람이 쓰는 길 따라가기. MoveTo가 매 프레임 목적지를 주면 길을 (바뀔 때만) 찾고 다음 경유점을 돌려준다.
    /// </summary>
    public sealed class NavWalker
    {
        readonly List<Vector3> path = new List<Vector3>();
        int idx, version = -1;
        Vector3 goal;
        bool has;

        public bool OnLast => !has || idx >= path.Count - 1;

        /// <summary>pos에서 dest로 가는 다음 경유점.</summary>
        public Vector3 Next(Vector3 pos, Vector3 dest)
        {
            var nav = ShopNav.Current;
            if (nav == null) { has = false; return dest; }
            dest.y = 0f;
            if (!has || version != nav.Version || (dest - goal).sqrMagnitude > 0.0025f)
            {
                nav.FindPath(pos, dest, path);
                idx = 0; goal = dest; version = nav.Version; has = path.Count > 0;
                if (!has) return dest;
            }
            // 경유점에 닿으면 다음으로
            while (idx < path.Count - 1)
            {
                Vector3 d = path[idx] - pos; d.y = 0f;
                if (d.sqrMagnitude > 0.06f * 0.06f) break;
                idx++;
            }
            return path[idx];
        }

        public void Reset() => has = false;
    }
}
