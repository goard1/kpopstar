using Munbanggu.Core;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 업그레이드가 가게 모습에 나타나는 부분 (간판 전구, 선풍기, 알바생, 방범 거울, 오락기).
    /// 업그레이드를 살 때마다 통째로 다시 만든다 (몇 개 안 되는 메시라 가볍다).
    /// 진열대 추가는 ShopBuilder.AddExtraShelves가 따로 만든다.
    /// </summary>
    public static class UpgradeVisuals
    {
        static readonly Color Bulb = new Color(1f, 0.93f, 0.55f);
        static readonly Color BulbWarm = new Color(1f, 0.72f, 0.35f);
        static readonly Color Metal = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color MetalDark = new Color(0.45f, 0.47f, 0.52f);
        static readonly Color FanBlue = new Color(0.55f, 0.78f, 0.92f);
        static readonly Color Ink = new Color(0.2f, 0.18f, 0.22f);
        static readonly Color Mirror = new Color(0.84f, 0.9f, 0.95f);
        static readonly Color Orange = new Color(0.98f, 0.6f, 0.25f);

        /// <summary>오락기 자리 (가게 앞 문 오른쪽, 길 쪽을 봄). 동전 연출 위치로도 씀.</summary>
        public static Vector3 ArcadePosition(int i) => new Vector3(2.12f + i * 0.6f, 0f, -4.62f);

        static readonly MeshKit kit = new MeshKit();

        /// <summary>현재 단계대로 다시 만든다. 이전 것은 지운다.</summary>
        public static GameObject Rebuild(Transform parent, MaterialCache mats, Upgrades up, GameObject previous)
        {
            if (previous != null)
            {
                if (Application.isPlaying) Object.Destroy(previous);
                else Object.DestroyImmediate(previous);
            }
            var root = new GameObject("Upgrade Decor");
            root.transform.SetParent(parent, false);

            kit.Clear();
            SignBulbs(kit, up.Level(UpgradeTable.Sign));
            Fans(kit, up.Level(UpgradeTable.Fan));
            Mirrors(kit, up.Level(UpgradeTable.Mirror));
            for (int i = 0; i < UpgradeTable.ArcadeMachines(up.Level(UpgradeTable.Arcade)); i++)
                ArcadeCabinet(kit, ArcadePosition(i), i);
            kit.Build("Upgrade Mesh", root.transform, mats.VertexColor);

            // 알바생: 계산대 뒤 (할머니 옆에 서서 손님 줄을 봄)
            if (up.Level(UpgradeTable.Helper) > 0)
            {
                var helper = new GameObject("Helper");
                helper.transform.SetParent(root.transform, false);
                helper.transform.localPosition = new Vector3(-3.62f, 0f, -0.45f);
                helper.transform.localRotation = Quaternion.Euler(0f, 110f, 0f);
                CustomerVisual.Build(helper.transform, mats, new CustomerLook { seed = 424242 });
            }
            return root;
        }

        // 간판 둘레 전구: 1단계 아래 줄, 2단계 + 위 줄, 3단계 + 양옆 + 별 장식
        static void SignBulbs(MeshKit k, int lv)
        {
            if (lv <= 0) return;
            const float cx = -0.4f, y0 = 3.02f, h = 0.92f, w = 5.6f, z = 3.98f;
            int n = 18;
            for (int i = 0; i < n; i++)
            {
                float x = cx - w * 0.5f + (i + 0.5f) * w / n;
                var c = i % 2 == 0 ? Bulb : BulbWarm;
                k.Sphere(new Vector3(x, y0 - 0.06f, z), Vector3.one * 0.055f, 6, 3, c);
                if (lv >= 2) k.Sphere(new Vector3(x, y0 + h + 0.06f, z), Vector3.one * 0.055f, 6, 3, c);
            }
            if (lv >= 3)
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = y0 + 0.1f + i * (h - 0.2f) / 3f;
                    k.Sphere(new Vector3(cx - w * 0.5f - 0.12f, y, z), Vector3.one * 0.055f, 6, 3, Bulb);
                    k.Sphere(new Vector3(cx + w * 0.5f + 0.12f, y, z), Vector3.one * 0.055f, 6, 3, Bulb);
                }
                // 오른쪽 위 "NEW!" 별 딱지
                k.Push(new Vector3(cx + w * 0.5f - 0.2f, y0 + h + 0.1f, z - 0.05f), Quaternion.Euler(0f, 0f, 15f));
                k.Box(Vector3.zero, new Vector3(0.5f, 0.3f, 0.04f), new Color(0.95f, 0.25f, 0.25f), new Color(0.8f, 0.15f, 0.15f));
                k.FlatBox(new Vector3(0f, 0f, -0.022f), new Vector3(0.36f, 0.08f, 0.004f), Bulb);
                k.Pop();
            }
        }

        // 1단계: 계산대 위 탁상 선풍기 / 2단계: 줄 서는 곳 옆 스탠드 선풍기 + 긴 의자
        static void Fans(MeshKit k, int lv)
        {
            if (lv >= 1) Fan(k, new Vector3(-2.75f, 0.72f, -0.55f), 200f, 0.42f);
            if (lv >= 2)
            {
                Fan(k, new Vector3(-1.15f, 0f, -0.75f), 225f, 1.25f);
                // 줄 옆 긴 의자
                var b = new Vector3(-2.45f, 0f, -2.85f);
                k.Box(b + new Vector3(0f, 0.36f, 0f), new Vector3(0.36f, 0.06f, 1.0f), new Color(0.62f, 0.42f, 0.26f), new Color(0.5f, 0.33f, 0.2f));
                foreach (float dz in new[] { -0.42f, 0.42f })
                    k.Box(b + new Vector3(0f, 0.17f, dz), new Vector3(0.3f, 0.34f, 0.05f), new Color(0.5f, 0.33f, 0.2f));
            }
        }

        static void Fan(MeshKit k, Vector3 pos, float yaw, float height)
        {
            k.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            k.Cylinder(new Vector3(0f, 0.025f, 0f), 0.13f, 0.05f, 10, MetalDark);
            k.Cylinder(new Vector3(0f, height * 0.5f, 0f), 0.022f, height, 6, Metal);
            var head = new Vector3(0f, height + 0.06f, 0f);
            k.Push(head, Quaternion.Euler(80f, 0f, 0f));
            k.Cylinder(Vector3.zero, 0.2f, 0.06f, 12, FanBlue, new Color(0.85f, 0.93f, 0.98f));
            k.Cylinder(new Vector3(0f, -0.05f, 0f), 0.06f, 0.06f, 8, MetalDark);
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3(0f, 0.035f, 0f), Quaternion.Euler(0f, i * 120f, 0f));
                k.FlatBox(new Vector3(0.09f, 0f, 0f), new Vector3(0.15f, 0.006f, 0.07f), Color.white);
                k.Pop();
            }
            k.Pop();
            k.Pop();
        }

        // 볼록 거울: 1단계 왼쪽 뒤 구석 높이, 2단계 + 오른쪽 앞(문 쪽)
        static void Mirrors(MeshKit k, int lv)
        {
            if (lv >= 1) ConvexMirror(k, new Vector3(-3.55f, 2.55f, 3.75f), 135f);
            if (lv >= 2) ConvexMirror(k, new Vector3(3.8f, 2.45f, -3.55f), -45f);
        }

        static void ConvexMirror(MeshKit k, Vector3 pos, float yaw)
        {
            k.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            k.Box(new Vector3(0f, 0.12f, -0.12f), new Vector3(0.04f, 0.04f, 0.26f), MetalDark); // 받침대
            k.Push(Vector3.zero, Quaternion.Euler(70f, 0f, 0f));
            k.Cylinder(Vector3.zero, 0.24f, 0.04f, 14, Orange);
            k.Sphere(new Vector3(0f, 0.03f, 0f), new Vector3(0.21f, 0.07f, 0.21f), 12, 4, Mirror);
            k.Pop();
            k.Pop();
        }

        // 레트로 오락기: 몸통 + 화면 + 조이스틱
        static void ArcadeCabinet(MeshKit k, Vector3 pos, int i)
        {
            var body = i == 0 ? new Color(0.25f, 0.35f, 0.75f) : new Color(0.8f, 0.25f, 0.45f);
            var bodyDark = body * 0.75f;
            bodyDark.a = 1f;
            k.Push(pos, Quaternion.Euler(0f, 180f, 0f)); // 앞(+Z)이 길 쪽(-Z)을 보게
            k.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 0.5f), body, bodyDark);
            k.Box(new Vector3(0f, 1.32f, 0.02f), new Vector3(0.52f, 0.24f, 0.46f), Ink, bodyDark); // 윗간판
            k.FlatBox(new Vector3(0f, 1.32f, 0.255f), new Vector3(0.42f, 0.12f, 0.004f), i == 0 ? Bulb : new Color(0.6f, 0.95f, 0.85f));
            // 화면 (어두운 남색 + 점 몇 개)
            k.FlatBox(new Vector3(0f, 0.98f, 0.252f), new Vector3(0.38f, 0.3f, 0.004f), new Color(0.1f, 0.12f, 0.3f));
            k.FlatBox(new Vector3(-0.08f, 0.95f, 0.256f), new Vector3(0.05f, 0.05f, 0.004f), Bulb);
            k.FlatBox(new Vector3(0.1f, 1.03f, 0.256f), new Vector3(0.04f, 0.04f, 0.004f), new Color(1f, 0.4f, 0.4f));
            k.FlatBox(new Vector3(0.02f, 0.88f, 0.256f), new Vector3(0.18f, 0.02f, 0.004f), new Color(0.5f, 0.9f, 0.6f));
            // 조작판 + 조이스틱 + 버튼
            k.Box(new Vector3(0f, 0.74f, 0.3f), new Vector3(0.5f, 0.06f, 0.16f), bodyDark);
            k.Cylinder(new Vector3(-0.1f, 0.82f, 0.3f), 0.015f, 0.1f, 5, Ink);
            k.Sphere(new Vector3(-0.1f, 0.88f, 0.3f), Vector3.one * 0.035f, 6, 3, new Color(0.95f, 0.25f, 0.25f));
            k.Cylinder(new Vector3(0.07f, 0.78f, 0.3f), 0.025f, 0.02f, 6, Bulb);
            k.Cylinder(new Vector3(0.15f, 0.78f, 0.3f), 0.025f, 0.02f, 6, new Color(0.5f, 0.9f, 0.6f));
            // 동전 투입구
            k.FlatBox(new Vector3(0f, 0.45f, 0.252f), new Vector3(0.08f, 0.12f, 0.004f), MetalDark);
            k.Pop();
        }
    }
}
