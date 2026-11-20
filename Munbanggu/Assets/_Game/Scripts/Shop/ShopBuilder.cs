using System.Collections.Generic;
using Munbanggu.Core;
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
    /// 그레이박스 가게를 기본 도형으로 만든다. (나중에 로우폴리 모델로 교체)
    /// 바닥 8x8, 뒷벽은 +Z / +X 쪽, 앞쪽(-Z)이 입구, 왼쪽 앞이 계산대.
    /// </summary>
    public static class ShopBuilder
    {
        static readonly Color Floor = new Color(0.80f, 0.66f, 0.48f);
        static readonly Color Wall = new Color(0.96f, 0.91f, 0.80f);
        static readonly Color Table = new Color(0.62f, 0.45f, 0.30f);
        static readonly Color TableTop = new Color(0.93f, 0.87f, 0.74f);
        static readonly Color Counter = new Color(0.50f, 0.34f, 0.22f);

        public static ShopLayout Build(Transform root, MaterialCache mats)
        {
            var layout = new ShopLayout();
            var shop = new GameObject("Shop").transform;
            shop.SetParent(root, false);

            // 바닥·벽
            Part(PrimitiveType.Cube, shop, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f), Floor, mats);
            Part(PrimitiveType.Cube, shop, new Vector3(0f, 1.25f, 4.1f), new Vector3(8.4f, 2.5f, 0.2f), Wall, mats);
            Part(PrimitiveType.Cube, shop, new Vector3(4.1f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 8.4f), Wall, mats);
            // 벽 아래 띠 (깊이감)
            Part(PrimitiveType.Cube, shop, new Vector3(0f, 0.2f, 3.98f), new Vector3(8.2f, 0.4f, 0.05f), Table, mats);
            Part(PrimitiveType.Cube, shop, new Vector3(3.98f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 8.2f), Table, mats);
            // 입구 매트
            Part(PrimitiveType.Cube, shop, new Vector3(1.6f, 0.005f, -3.55f), new Vector3(1.4f, 0.02f, 0.8f), new Color(0.55f, 0.25f, 0.2f), mats);

            // 계산대 + 금전등록기 + 주인
            Part(PrimitiveType.Cube, shop, new Vector3(-2.8f, 0.45f, -1.2f), new Vector3(1.0f, 0.9f, 2.2f), Counter, mats);
            Part(PrimitiveType.Cube, shop, new Vector3(-2.8f, 1.03f, -0.6f), new Vector3(0.45f, 0.26f, 0.35f), new Color(0.35f, 0.35f, 0.38f), mats);
            Part(PrimitiveType.Capsule, shop, new Vector3(-3.6f, 0.55f, -1.2f), new Vector3(0.5f, 0.55f, 0.5f), new Color(0.35f, 0.45f, 0.70f), mats);
            Part(PrimitiveType.Sphere, shop, new Vector3(-3.6f, 1.25f, -1.2f), Vector3.one * 0.42f, new Color(1f, 0.86f, 0.72f), mats);

            // 매대: 뒷벽 5칸(손님은 -Z를 보고 섬), 오른벽 3칸(-X)
            int index = 0;
            foreach (float x in new[] { -2.9f, -1.6f, -0.3f, 1.0f, 2.3f })
                layout.Slots.Add(CreateSlot(shop, index++, new Vector3(x, 0f, 3.35f), 180f, mats));
            foreach (float z in new[] { 1.9f, 0.6f, -0.7f })
                layout.Slots.Add(CreateSlot(shop, index++, new Vector3(3.35f, 0f, z), -90f, mats));

            // 계산 줄: 계산대 앞에서 입구 쪽(-Z)으로
            var spots = new List<Vector3>();
            for (int i = 0; i < 6; i++) spots.Add(new Vector3(-1.9f, 0f, -1.2f - i * 0.75f));
            layout.Queue = new CounterQueue(spots, Vector3.left);

            layout.EntrancePoint = new Vector3(1.6f, 0f, -5.0f);
            layout.ExitPoint = new Vector3(1.6f, 0f, -5.5f);

            // 조명
            var light = new GameObject("Sun").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None; // 저사양 대응
            light.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

            // 카메라 (고정 쿼터뷰, 직교)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.98f, 0.92f, 0.80f);
            camGo.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            var shopCam = camGo.AddComponent<ShopCamera>();
            shopCam.focus = new Bounds(new Vector3(0f, 1f, -0.6f), new Vector3(8.4f, 2.5f, 9.6f));
            shopCam.Fit();
            layout.Camera = cam;

            return layout;
        }

        static ShelfSlot CreateSlot(Transform parent, int index, Vector3 pos, float yaw, MaterialCache mats)
        {
            var go = new GameObject($"Slot {index}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.38f, 0f), new Vector3(1.1f, 0.76f, 0.7f), Table, mats);
            Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.775f, 0f), new Vector3(1.14f, 0.03f, 0.74f), TableTop, mats);

            // 상품 12개 (4 x 3), 위로 쌓인 느낌
            var items = new GameObject[12];
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var item = Part(PrimitiveType.Cube, go.transform,
                    new Vector3(-0.39f + col * 0.26f, 0.88f, 0.2f - row * 0.2f),
                    new Vector3(0.2f, 0.18f, 0.16f), Color.white, mats);
                item.name = $"Item {i}";
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

        /// <summary>기본 도형 생성 (콜라이더 제거).</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, MaterialCache mats)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            mats.Apply(go, color);
            return go;
        }
    }
}
