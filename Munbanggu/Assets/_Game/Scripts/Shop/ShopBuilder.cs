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
        public Camera Camera;
    }

    /// <summary>
    /// 가게를 코드로 만든다 (로우폴리 디오라마, 외부 모델 없음).
    /// 바닥 8x8, 뒷벽은 +Z / +X 쪽, 앞쪽(-Z)이 입구, 왼쪽 앞이 계산대. 앞·왼쪽 벽은 없는 디오라마 구조.
    /// 움직이지 않는 장식은 Visual/ShopDecor가 메시 하나로 합쳐 그린다.
    /// </summary>
    public static class ShopBuilder
    {
        // 매대 상품 윗면 높이 (앞줄 → 뒷줄, ShopDecor.ShelfCabinet 계단과 맞춤)
        static readonly float[] TierTop = { 0.68f, 0.80f, 0.92f };

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

            kit.Build("Decor", shop, mats.VertexColor);
            if (mats.Glass != null) glass.Build("Glass", shop, mats.Glass);
            if (mats.Blob != null) blob.Build("Shadows", shop, mats.Blob);

            layout.Queue = new CounterQueue(new List<Vector3>(QueueSpots), Vector3.left);

            layout.EntrancePoint = new Vector3(1.6f, 0f, -5.0f);
            layout.ExitPoint = new Vector3(1.6f, 0f, -5.5f);

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
            // 바닥(-0.4) ~ 간판 꼭대기(3.9). 가로로 꽉 차게 맞춰지고, 세로는 가운데 정렬 기준.
            shopCam.focus = new Bounds(new Vector3(0f, 1.5f, -0.6f), new Vector3(8.4f, 3.8f, 9.6f));
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
            var items = new GameObject[12];
            var rng = new System.Random(index * 97 + 13);
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var item = new GameObject($"Item {i}");
                item.transform.SetParent(go.transform, false);
                item.transform.localPosition = new Vector3(-0.39f + col * 0.26f + ((float)rng.NextDouble() - 0.5f) * 0.02f,
                                                           TierTop[row], 0.2f - row * 0.2f);
                item.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 18f, 0f);
                items[i] = item;
            }

            // 탭 판정용 넉넉한 콜라이더
            var col3d = go.AddComponent<BoxCollider>();
            col3d.center = new Vector3(0f, 0.6f, 0.1f);
            col3d.size = new Vector3(1.25f, 1.2f, 1.0f);

            var slot = go.AddComponent<ShelfSlot>();
            slot.Init(index, items, mats);
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
