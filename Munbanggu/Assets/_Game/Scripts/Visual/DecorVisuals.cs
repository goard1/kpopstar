using Munbanggu.Core;
using Munbanggu.Shop;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 진열대 위 장식 모양 (진열대 뒤 머리판 위에 올림). 배치가 바뀔 때마다 통째로 다시 만든다.
    /// 레벨이 오를수록 살짝 커지고, 2레벨부터 금색 받침이 생긴다.
    /// </summary>
    public static class DecorVisuals
    {
        static readonly MeshKit kit = new MeshKit();
        static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
        static readonly Color GoldDark = new Color(0.8f, 0.58f, 0.15f);
        static readonly Color Ink = new Color(0.2f, 0.18f, 0.22f);

        /// <summary>장식이 놓이는 자리 (진열대 로컬: 뒤 머리판 위).</summary>
        public static readonly Vector3 LocalSpot = new Vector3(0f, 1.2f, -0.31f);

        public static GameObject Rebuild(ShopLayout layout, MaterialCache mats, FacilityState state, GameObject previous)
        {
            if (previous != null)
            {
                if (Application.isPlaying) Object.Destroy(previous);
                else Object.DestroyImmediate(previous);
            }
            var root = new GameObject("Decor Items");
            root.transform.SetParent(layout.Root, false);
            kit.Clear();
            foreach (var d in state.decor)
            {
                if (d.slot < 0 || d.slot >= layout.Slots.Count) continue;
                var slot = layout.Slots[d.slot];
                kit.Push(slot.transform.localPosition, slot.transform.localRotation);
                kit.Push(LocalSpot, Quaternion.identity, Vector3.one * (1f + 0.12f * (d.level - 1)));
                if (d.level >= 2) // 금색 받침
                    kit.Cylinder(new Vector3(0f, 0.012f, 0f), 0.13f, 0.024f, 10, GoldDark, Gold);
                Build(kit, d.id);
                kit.Pop();
                kit.Pop();
            }
            kit.Build("Decor Mesh", root.transform, mats.VertexColor);
            return root;
        }

        static void Build(MeshKit k, string id)
        {
            switch (id)
            {
                case "candyjar":
                    k.Cylinder(new Vector3(0f, 0.12f, 0f), 0.09f, 0.22f, 10, new Color(0.8f, 0.92f, 0.95f), new Color(0.9f, 0.97f, 1f));
                    foreach (var (p, c) in new[]
                    {
                        (new Vector3(-0.03f, 0.07f, 0.02f), new Color(0.95f, 0.35f, 0.4f)),
                        (new Vector3(0.03f, 0.08f, -0.02f), new Color(0.98f, 0.8f, 0.25f)),
                        (new Vector3(0f, 0.13f, 0.03f), new Color(0.45f, 0.8f, 0.5f)),
                        (new Vector3(0.02f, 0.16f, -0.01f), new Color(0.55f, 0.6f, 0.95f)),
                    })
                        k.Sphere(p, Vector3.one * 0.035f, 6, 3, c);
                    k.Cylinder(new Vector3(0f, 0.245f, 0f), 0.095f, 0.04f, 10, new Color(0.88f, 0.25f, 0.25f));
                    break;

                case "robot":
                {
                    var body = new Color(0.35f, 0.55f, 0.92f);
                    k.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.14f, 0.09f), body, body * 0.8f);
                    k.Box(new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.09f, 0.09f), new Color(0.78f, 0.8f, 0.86f), new Color(0.6f, 0.62f, 0.68f));
                    k.FlatBox(new Vector3(-0.022f, 0.225f, 0.047f), new Vector3(0.02f, 0.02f, 0.004f), Gold);
                    k.FlatBox(new Vector3(0.022f, 0.225f, 0.047f), new Vector3(0.02f, 0.02f, 0.004f), Gold);
                    k.Cylinder(new Vector3(0f, 0.29f, 0f), 0.006f, 0.05f, 4, Ink);
                    k.Sphere(new Vector3(0f, 0.32f, 0f), Vector3.one * 0.016f, 5, 3, new Color(0.95f, 0.3f, 0.3f));
                    k.Box(new Vector3(-0.095f, 0.1f, 0f), new Vector3(0.04f, 0.11f, 0.04f), new Color(0.88f, 0.3f, 0.3f));
                    k.Box(new Vector3(0.095f, 0.1f, 0f), new Vector3(0.04f, 0.11f, 0.04f), new Color(0.88f, 0.3f, 0.3f));
                    break;
                }

                case "trophy":
                    k.Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.12f, 0.05f, 0.1f), new Color(0.45f, 0.3f, 0.2f));
                    k.Cylinder(new Vector3(0f, 0.08f, 0f), 0.018f, 0.07f, 6, GoldDark);
                    k.Frustum(new Vector3(0f, 0.16f, 0f), 0.04f, 0.085f, 0.1f, 10, Gold, Gold);
                    foreach (float s in new[] { -1f, 1f })
                        k.Box(new Vector3(s * 0.095f, 0.17f, 0f), new Vector3(0.025f, 0.06f, 0.015f), GoldDark);
                    k.FlatBox(new Vector3(0f, 0.025f, 0.051f), new Vector3(0.07f, 0.025f, 0.004f), Gold);
                    break;

                case "sharpener":
                {
                    var red = new Color(0.9f, 0.28f, 0.25f);
                    k.Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.16f, 0.12f), red, red * 0.8f);
                    k.FlatBox(new Vector3(0f, 0.11f, 0.061f), new Vector3(0.05f, 0.05f, 0.004f), Ink); // 연필 구멍
                    k.Box(new Vector3(0.08f, 0.13f, 0f), new Vector3(0.02f, 0.02f, 0.02f), new Color(0.7f, 0.7f, 0.74f));
                    k.Box(new Vector3(0.1f, 0.1f, 0f), new Vector3(0.015f, 0.08f, 0.015f), new Color(0.7f, 0.7f, 0.74f)); // 손잡이
                    k.Cylinder(new Vector3(0.1f, 0.06f, 0f), 0.018f, 0.03f, 6, Ink);
                    break;
                }

                case "plant":
                    k.Frustum(new Vector3(0f, 0.06f, 0f), 0.06f, 0.08f, 0.12f, 8, new Color(0.8f, 0.45f, 0.3f), new Color(0.6f, 0.35f, 0.25f));
                    k.Sphere(new Vector3(0f, 0.17f, 0f), new Vector3(0.09f, 0.08f, 0.09f), 8, 4, new Color(0.35f, 0.7f, 0.4f));
                    k.Sphere(new Vector3(-0.05f, 0.22f, 0.02f), Vector3.one * 0.055f, 6, 3, new Color(0.45f, 0.8f, 0.45f));
                    k.Sphere(new Vector3(0.05f, 0.21f, -0.02f), Vector3.one * 0.05f, 6, 3, new Color(0.4f, 0.75f, 0.42f));
                    k.Sphere(new Vector3(0.02f, 0.27f, 0f), Vector3.one * 0.03f, 6, 3, new Color(0.98f, 0.6f, 0.7f)); // 꽃
                    break;

                default: // radio
                {
                    var wood = new Color(0.55f, 0.35f, 0.22f);
                    k.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.16f, 0.1f), wood, wood * 0.8f);
                    k.Cylinder(new Vector3(-0.055f, 0.09f, 0.051f), 0.045f, 0.004f, 10, new Color(0.9f, 0.82f, 0.62f)); // 스피커
                    k.FlatBox(new Vector3(0.06f, 0.12f, 0.052f), new Vector3(0.08f, 0.03f, 0.004f), Gold);   // 다이얼 창
                    k.Cylinder(new Vector3(0.04f, 0.06f, 0.055f), 0.014f, 0.01f, 6, Ink);
                    k.Cylinder(new Vector3(0.08f, 0.06f, 0.055f), 0.014f, 0.01f, 6, Ink);
                    k.Push(new Vector3(0.09f, 0.17f, -0.02f), Quaternion.Euler(0f, 0f, -25f));
                    k.Cylinder(new Vector3(0f, 0.08f, 0f), 0.005f, 0.16f, 4, new Color(0.75f, 0.75f, 0.78f)); // 안테나
                    k.Pop();
                    break;
                }
            }
        }
    }
}
