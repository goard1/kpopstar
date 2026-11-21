using System.Collections.Generic;
using Munbanggu.Core;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Shop
{
    public class ShopLayout
    {
        public readonly List<ShelfSlot> Slots = new List<ShelfSlot>();
        public CounterQueue Queue;
        public Vector3 EntrancePoint;
        public Vector3 ExitPoint;
        // 문 경유점. ShopDecor.Door(): 문틀 x 0.6~2.6, z -4.0, 미닫이 두 짝이 x≈1.83~2.56에 겹쳐 열림
        // → 실제로 뚫린 곳은 x≈0.65~1.8 (가운데 1.22). 손님은 가운데 ± DoorSpread 안에서 흩어져 지난다.
        public const float DoorX = 1.22f;       // 열린 폭 가운데
        public const float DoorSpread = 0.2f;   // 손님마다 ±이만큼 (0.65~1.8 안에 충분히 들어감)
        public const float DoorZ = -4.0f;       // 문 선
        public const float DoorOutsideZ = -5.0f; // 문 앞 바깥 (EntrancePoint와 같은 z)
        public const float DoorInsideZ = -3.3f;  // 문 안쪽 한 걸음 (계산 줄 z=-3.45 바로 안쪽, 문지방 0.7 안)
        public Camera Camera;
        /// <summary>가게 앞 번호 뽑기판 (Slots에도 들어 있음).</summary>
        public ShelfSlot Board;
        /// <summary>가게 오브젝트들의 부모 (업그레이드로 나중에 붙는 것들도 여기에).</summary>
        public Transform Root;
        /// <summary>나중에 붙은 진열대 묶음 (붙은 순서 = Slots 인덱스 순서). 저장해 두고 불러올 때 같은 순서로 다시 붙인다.</summary>
        public readonly List<string> Groups = new List<string>();
        public bool HasGroup(string id) => Groups.Contains(id);

        // ── 옆칸(증축) 동선 ──
        /// <summary>이보다 왼쪽(-X)이면 옆칸. 옆칸 ↔ 본 가게 사이는 경유점 두 개를 지나 다닌다 (왼쪽 진열대·재고 상자를 피해서).</summary>
        public const float AnnexEdgeX = -3.95f;
        public static readonly Vector3 HubMain = new Vector3(-1.0f, 0f, 2.2f);
        public static readonly Vector3 HubAnnex = new Vector3(-4.05f, 0f, 2.25f);
        public static bool InAnnex(Vector3 p) => p.x < AnnexEdgeX && p.z > -4.0f;

        /// <summary>from → to로 갈 때 필요한 경유점(옆칸 ↔ 본 가게)과 to를 path 뒤에 붙인다.</summary>
        public static void AppendPath(List<Vector3> path, Vector3 from, Vector3 to)
        {
            // 길찾기(ShopNav)가 진열대를 돌아가므로 경유점은 따로 넣지 않는다
            path.Add(to);
        }

        /// <summary>옆칸 k(1부터)가 차지하는 x 범위: 본 가게 왼쪽 끝(-4)에서 2.5m씩.</summary>
        public const float BayWidth = 2.5f;
        public static float BayX0(int k) => -4f - BayWidth * k;      // 왼쪽 끝
        public static float BayX1(int k) => -4f - BayWidth * (k - 1); // 오른쪽 끝

        /// <summary>옆칸 k(1부터)의 뒷벽 안쪽 z. 동네마다 가게 모양이 달라서 깊은 칸(ㄱ자·계단 모양)이 있다. GameManager가 동네에 맞춰 채움.</summary>
        public static float[] BayBacks = { 4f, 4f, 4f, 4f };
        public static float BayBack(int k) => k >= 1 && k <= BayBacks.Length ? BayBacks[k - 1] : 4f;

        /// <summary>움직이지 않는 장애물 (계산대·상자·뽑기 기계 등). 길찾기가 피해 간다.</summary>
        public readonly List<ShopNav.Box> Statics = new List<ShopNav.Box>();
        /// <summary>진열대를 놓으면 안 되는 자리 (계산 줄·문 앞·계산대 뒤·창고 앞).</summary>
        public readonly List<Rect> Reserved = new List<Rect>();
    }

    /// <summary>
    /// 가게를 코드로 만든다 (로우폴리 디오라마, 외부 모델 없음).
    /// 바닥 8x8, 뒷벽은 +Z / +X 쪽, 앞쪽(-Z)이 입구, 왼쪽 앞이 계산대. 앞·왼쪽 벽은 없는 디오라마 구조.
    /// 움직이지 않는 장식은 Visual/ShopDecor가 메시 하나로 합쳐 그린다.
    /// </summary>
    public static class ShopBuilder
    {

        /// <summary>계산 줄 자리: 계산대 앞에서 입구 쪽(-Z)으로 가다가, 가게 앞쪽 안에서 오른쪽(+X)으로 꺾임.</summary>
        public static readonly Vector3[] QueueSpots =
        {
            new Vector3(-1.9f, 0f, -1.2f), new Vector3(-1.9f, 0f, -1.95f), new Vector3(-1.9f, 0f, -2.7f),
            new Vector3(-1.9f, 0f, -3.45f), new Vector3(-1.15f, 0f, -3.45f), new Vector3(-0.4f, 0f, -3.45f),
        };

        public static ShopLayout Build(Transform root, MaterialCache mats)
        {
            var layout = new ShopLayout();
            var shop = new GameObject("Shop").transform;
            layout.Root = shop;
            shop.SetParent(root, false);

            var kit = new MeshKit();
            var glass = new MeshKit();
            var blob = new MeshKit();
            ShopDecor.Build(kit, glass, blob);

            // 매대: 뒷벽 5칸(손님은 -Z를 보고 섬), 오른벽 3칸(-X)
            int index = 0;
            foreach (float x in new[] { -2.9f, -1.6f, -0.3f, 1.0f, 2.3f })
                layout.Slots.Add(CreateSlot(shop, index++, new Vector3(x, 0f, 3.35f), 180f, mats, kit, blob));
            foreach (float z in new[] { 1.9f, 0.6f, -0.7f })
                layout.Slots.Add(CreateSlot(shop, index++, new Vector3(3.35f, 0f, z), -90f, mats, kit, blob));

            // 가게 앞 번호 뽑기판 (뽑기 기계 오른쪽, 길 쪽을 봄). 번호 뽑기만 걸 수 있고 아이들이 거기서 뽑는다.
            layout.Board = CreateBoard(shop, index++, new Vector3(-1.95f, 0f, -4.55f), 180f, mats);
            layout.Slots.Add(layout.Board);

            kit.Build("Decor", shop, mats.VertexColor);
            if (mats.Glass != null) glass.Build("Glass", shop, mats.Glass);
            if (mats.Blob != null) blob.Build("Shadows", shop, mats.Blob);

            layout.Queue = new CounterQueue(new List<Vector3>(QueueSpots), Vector3.left);

            // 길찾기 장애물 (ShopDecor 위치와 맞춤) · 진열대 못 놓는 자리
            var st = layout.Statics;
            st.Add(new ShopNav.Box(new Vector2(-2.8f, -1.2f), new Vector2(1.0f, 1.8f)));     // 계산대
            st.Add(new ShopNav.Box(new Vector2(-3.72f, -1.25f), new Vector2(0.55f, 0.55f))); // 할머니 의자
            st.Add(new ShopNav.Box(new Vector2(-3.72f, 3.25f), new Vector2(0.5f, 0.95f)));   // 재고 상자 더미
            st.Add(new ShopNav.Box(new Vector2(3.45f, 3.35f), new Vector2(0.8f, 0.8f)));     // 안쪽 구석 캡슐 뽑기
            st.Add(new ShopNav.Box(new Vector2(-3.25f, -4.55f), new Vector2(1.3f, 0.6f)));   // 가게 앞 뽑기 기계 두 대
            st.Add(new ShopNav.Box(new Vector2(-1.95f, -4.55f), new Vector2(1.3f, 0.5f)));   // 번호 뽑기판
            st.Add(new ShopNav.Box(new Vector2(3.3f, -4.7f), new Vector2(0.7f, 0.5f)));      // 입간판
            var rs = layout.Reserved;
            rs.Add(Rect.MinMaxRect(-2.35f, -3.85f, -0.05f, -3.05f));  // 계산 줄 (앞쪽 가로)
            rs.Add(Rect.MinMaxRect(-2.35f, -3.85f, -1.45f, -0.75f));  // 계산 줄 (계산대 앞 세로)
            rs.Add(Rect.MinMaxRect(0.3f, -4.0f, 2.2f, -2.7f));        // 문 안쪽
            rs.Add(Rect.MinMaxRect(-4.0f, -2.4f, -2.15f, 0.15f));     // 계산대·뒤 (직원 계산 자리)
            rs.Add(Rect.MinMaxRect(-3.7f, 2.0f, -2.9f, 2.8f));        // 창고 상자 앞 (직원이 물건 가지러 서는 곳)

            layout.EntrancePoint = new Vector3(ShopLayout.DoorX, 0f, ShopLayout.DoorOutsideZ);
            layout.ExitPoint = new Vector3(ShopLayout.DoorX, 0f, -5.6f);

            // 조명: 따뜻한 형광등 느낌. 왼벽(+Z)은 밝게, 오른벽(+X)은 그늘 톤 (툰 2단)
            var light = new GameObject("Sun").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Directional;
            light.intensity = VisualAssets.LightIntensity;
            light.color = VisualAssets.LightColor;
            light.shadows = LightShadows.None; // 저사양 대응 (바닥 원형 가짜 그림자 사용)
            light.transform.rotation = Quaternion.Euler(53f, -35f, 0f);

            // 카메라 (고정 쿼터뷰, 직교)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = VisualAssets.Background;
            camGo.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            var shopCam = camGo.AddComponent<ShopCamera>();
            // 핵심 영역(매대·계산대·문)만 감싼 범위. 바닥(-0.4) ~ 간판 꼭대기(3.9). 문 밖 보도 끝은 잘려도 된다.
            // 보이는 영역(상·하단 UI 제외)에 이 범위가 다 들어오도록 ShopCamera가 크기/위치를 정한다.
            shopCam.focus = new Bounds(new Vector3(-0.3f, 1.5f, -0.3f), new Vector3(8.2f, 3.8f, 8.6f));
            shopCam.Fit();
            layout.Camera = cam;

            return layout;
        }

        static ShelfSlot CreateSlot(Transform parent, int index, Vector3 pos, float yaw, MaterialCache mats, MeshKit kit, MeshKit blob)
        {
            var go = new GameObject($"Slot {index}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 나무 매대는 진열대마다 따로 (진열대를 옮기면 같이 움직이게). kit·blob 인자는 예전 호환용으로 안 씀.
            var ck = new MeshKit();
            var cb = new MeshKit();
            ShopDecor.ShelfCabinet(ck, cb, Vector3.zero, 0f, index);
            ck.Build("Cabinet", go.transform, mats.VertexColor);
            if (mats.Blob != null) cb.Build("Cabinet Shadow", go.transform, mats.Blob);

            // 상품 12개 자리 (4 x 3, 앞줄이 낮은 계단식). 실제 모양은 ShelfSlot이 메시 하나로 그림.
            var items = ShelfSlot.CreateAnchors(go.transform, 12, index);

            // 탭 판정용 넉넉한 콜라이더
            var col3d = go.AddComponent<BoxCollider>();
            col3d.center = new Vector3(0f, 0.6f, 0.1f);
            col3d.size = new Vector3(1.25f, 1.2f, 1.0f);

            var slot = go.AddComponent<ShelfSlot>();
            slot.DefaultPos = pos;
            slot.DefaultYaw = yaw;
            slot.Init(index, items, mats);
            return slot;
        }

        /// <summary>진열대 추가 업그레이드: 창가(오른벽 창문 아래) 1칸 + 왼쪽 가장자리 1칸. 손님 동선을 막지 않는 자리.</summary>
        public static readonly (Vector3 pos, float yaw)[] ExtraShelfSpots =
        {
            (new Vector3(3.35f, 0f, -2.55f), -90f), // 창가, 가게 안(-X)을 봄
            (new Vector3(-3.35f, 0f, 1.3f), 90f),   // 왼쪽 가장자리, 가게 안(+X)을 봄
        };

        public const string GroupExtra = "extra", GroupAnnex = "annex", GroupAnnexSide = "annexSide";

        /// <summary>옆칸 k의 뒷벽 진열대 묶음 이름 (1번은 예전 세이브와 같은 "annex").</summary>
        public static string BayGroup(int k) => k == 1 ? GroupAnnex : $"bay{k}";
        /// <summary>옆칸 k의 옆(가운데) 진열대 묶음 이름 (1번은 "annexSide").</summary>
        public static string SideGroup(int k) => k == 1 ? GroupAnnexSide : $"side{k}";

        /// <summary>
        /// 묶음별 진열대 자리. 옆칸 k: 뒷벽 2칸(x = 옆칸 오른쪽 끝 -0.65, -1.9), 옆 진열대 1칸(옆칸 왼쪽 끝 +0.45, 가게 안을 봄).
        /// 옆칸이 더 붙으면 앞 칸의 옆 진열대는 가운데 섬 진열대가 된다 (통로 z 2.25는 비어 있음).
        /// </summary>
        static (Vector3 pos, float yaw)[] GroupSpots(string group)
        {
            if (group == GroupExtra) return ExtraShelfSpots;
            int k = group == GroupAnnex ? 1 : group == GroupAnnexSide ? 1
                : group.StartsWith("bay") || group.StartsWith("side") ? ParseIndex(group) : 0;
            if (k <= 0) return System.Array.Empty<(Vector3, float)>();
            float x1 = ShopLayout.BayX1(k), x0 = ShopLayout.BayX0(k);
            if (group == GroupAnnex || group.StartsWith("bay"))
                return new[] { (new Vector3(x1 - 0.65f, 0f, ShopLayout.BayBack(k) - 0.65f), 180f), (new Vector3(x1 - 1.9f, 0f, ShopLayout.BayBack(k) - 0.65f), 180f) };
            return new[] { (new Vector3(x0 + 0.45f, 0f, 1.2f), 90f) };
        }

        static int ParseIndex(string group)
        {
            int i = 0;
            while (i < group.Length && !char.IsDigit(group[i])) i++;
            return int.TryParse(group.Substring(i), out int n) ? n : 0;
        }

        /// <summary>
        /// 나중에 붙은 진열대 묶음을 전부 치운다 (이사할 때). 기본 진열대 8칸 + 뽑기판은 남긴다.
        /// 치운 진열대의 물건은 부르는 쪽이 먼저 창고로 옮겨야 한다.
        /// </summary>
        public static void ClearGroups(ShopLayout layout)
        {
            int keep = layout.Board != null ? layout.Board.Index + 1 : layout.Slots.Count;
            for (int i = layout.Slots.Count - 1; i >= keep; i--)
            {
                Kill(layout.Slots[i].gameObject);
                layout.Slots.RemoveAt(i);
            }
            foreach (var g in layout.Groups)
                foreach (var name in new[] { $"Shelves {g}", $"Shadows {g}" })
                {
                    var t = layout.Root.Find(name);
                    if (t != null) Kill(t.gameObject);
                }
            layout.Groups.Clear();
        }

        /// <summary>움직이지 않는 장식(바닥·벽·간판·기본 진열대 몸통)을 지금 동네 색으로 다시 만든다.</summary>
        public static void RebuildStatic(ShopLayout layout, MaterialCache mats)
        {
            foreach (var name in new[] { "Decor", "Glass", "Shadows" })
            {
                var t = layout.Root.Find(name);
                if (t != null) Kill(t.gameObject);
            }
            var kit = new MeshKit();
            var glass = new MeshKit();
            var blob = new MeshKit();
            ShopDecor.Build(kit, glass, blob);
            kit.Build("Decor", layout.Root, mats.VertexColor);
            if (mats.Glass != null) glass.Build("Glass", layout.Root, mats.Glass);
            if (mats.Blob != null) blob.Build("Shadows", layout.Root, mats.Blob);
        }

        /// <summary>모든 진열대를 처음 자리·방향으로.</summary>
        public static void ResetSlotTransforms(ShopLayout layout)
        {
            foreach (var s in layout.Slots)
            {
                if (s == null || s.IsBoard) continue;
                s.transform.localPosition = s.DefaultPos;
                s.transform.localRotation = Quaternion.Euler(0f, s.DefaultYaw, 0f);
            }
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>진열대 묶음을 만들어 Slots 뒤에 붙인다 (이미 있으면 건너뜀). 매대 칸 수도 맞춘다.</summary>
        public static bool AddShelfGroup(ShopLayout layout, MaterialCache mats, int capacity, string group)
        {
            if (layout.HasGroup(group)) return false;
            layout.Groups.Add(group);
            var kit = new MeshKit();
            var blob = new MeshKit();
            int index = layout.Slots.Count;
            foreach (var (pos, yaw) in GroupSpots(group))
            {
                var slot = CreateSlot(layout.Root, index++, pos, yaw, mats, kit, blob);
                slot.SetCapacity(capacity);
                layout.Slots.Add(slot);
            }


            return true;
        }

        static ShelfSlot CreateBoard(Transform parent, int index, Vector3 pos, float yaw, MaterialCache mats)
        {
            var go = new GameObject($"Ppopgi Board {index}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 종이 30장 자리 (모양은 ShelfSlot.BuildBoard가 그림, 여기선 개수만)
            var tabs = new GameObject[ShelfSlot.BoardCols * ShelfSlot.BoardRows];
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = new GameObject($"Tab {i}");
                tabs[i].transform.SetParent(go.transform, false);
            }

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0.1f);
            col.size = new Vector3(1.3f, 1.8f, 0.8f);

            var slot = go.AddComponent<ShelfSlot>();
            slot.DefaultPos = pos;
            slot.DefaultYaw = yaw;
            slot.Init(index, tabs, mats, isBoard: true);
            return slot;
        }

        /// <summary>기본 도형 생성 (콜라이더 제거). 에디트 모드(스크린샷 도구)에서도 동작.</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, MaterialCache mats)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            mats.Apply(go, color);
            return go;
        }
    }
}
