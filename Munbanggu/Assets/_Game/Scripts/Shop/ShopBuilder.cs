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
            bool a = InAnnex(from), b = InAnnex(to);
            if (a && !b) { path.Add(HubAnnex); path.Add(HubMain); }
            else if (!a && b) { path.Add(HubMain); path.Add(HubAnnex); }
            path.Add(to);
        }
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

            // 나무 매대는 장식 메시에 합침
            ShopDecor.ShelfCabinet(kit, blob, pos, yaw, index);

            // 상품 12개 자리 (4 x 3, 앞줄이 낮은 계단식). 실제 모양은 ShelfSlot이 메시 하나로 그림.
            var items = ShelfSlot.CreateAnchors(go.transform, 12, index);

            // 탭 판정용 넉넉한 콜라이더
            var col3d = go.AddComponent<BoxCollider>();
            col3d.center = new Vector3(0f, 0.6f, 0.1f);
            col3d.size = new Vector3(1.25f, 1.2f, 1.0f);

            var slot = go.AddComponent<ShelfSlot>();
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

        /// <summary>증축 1단계(옆칸): 옆칸 뒷벽 2칸. 2단계: 옆칸 왼쪽 가장자리 1칸.</summary>
        static (Vector3 pos, float yaw)[] GroupSpots(string group) => group switch
        {
            GroupExtra => ExtraShelfSpots,
            GroupAnnex => new[] { (new Vector3(-4.65f, 0f, 3.35f), 180f), (new Vector3(-5.9f, 0f, 3.35f), 180f) },
            GroupAnnexSide => new[] { (new Vector3(-6.05f, 0f, 1.2f), 90f) },
            _ => System.Array.Empty<(Vector3, float)>(),
        };

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
            kit.Build($"Shelves {group}", layout.Root, mats.VertexColor);
            if (mats.Blob != null) blob.Build($"Shadows {group}", layout.Root, mats.Blob);
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
