using Munbanggu.Core;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 가게 고양이 「치즈」: 2등신 치즈냥 (노랑 줄무늬, 흰 주둥이·배, 큰 눈, 분홍 코·볼, 말린 꼬리).
    /// 원점 = 앉은 자리 바닥, 앞 = +Z. 숨 쉬듯 살짝 커졌다 작아진다.
    /// </summary>
    public static class CatVisual
    {
        static readonly MeshKit kit = new MeshKit();

        public static GameObject Build(Transform parent, MaterialCache mats, Vector3 localPos, float yaw)
        {
            var root = new GameObject("Cat Cheese");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Color fur = new Color(1.0f, 0.74f, 0.36f), stripe = new Color(0.92f, 0.55f, 0.22f);
            Color white = new Color(1f, 0.98f, 0.95f), ink = new Color(0.25f, 0.16f, 0.14f), pink = new Color(1f, 0.6f, 0.68f);

            kit.Clear();
            kit.SmoothSpheres = true;
            kit.SmoothUpBias = 0.5f;
            // 몸 (앉은 자세) + 흰 배
            kit.Sphere(new Vector3(0f, 0.11f, 0f), new Vector3(0.13f, 0.11f, 0.12f), 12, 6, fur);
            kit.Sphere(new Vector3(0f, 0.1f, 0.07f), new Vector3(0.08f, 0.08f, 0.06f), 10, 5, white);
            // 앞발
            foreach (float sx in new[] { -0.05f, 0.05f })
                kit.Sphere(new Vector3(sx, 0.025f, 0.09f), new Vector3(0.035f, 0.025f, 0.04f), 8, 4, white);
            // 꼬리 (옆으로 말림)
            for (int i = 0; i < 6; i++)
            {
                float a = i * 0.45f;
                kit.Sphere(new Vector3(0.13f + Mathf.Sin(a) * 0.06f, 0.03f + i * 0.012f, -0.04f - Mathf.Cos(a) * 0.05f), Vector3.one * 0.028f, 6, 4, i % 2 == 0 ? fur : stripe);
            }
            // 머리 (크게)
            var h = new Vector3(0f, 0.3f, 0.02f);
            var hr = new Vector3(0.14f, 0.12f, 0.12f);
            kit.Sphere(h, hr, 14, 7, fur);
            // 이마 줄무늬 세 줄
            for (int i = -1; i <= 1; i++)
                kit.DiscFacing(h + new Vector3(i * 0.035f, 0.08f, 0.09f), new Vector2(0.012f, 0.03f), new Vector3(0f, 0.6f, 0.8f), 8, stripe);
            // 귀
            foreach (float sx in new[] { -1f, 1f })
            {
                kit.Push(h + new Vector3(sx * 0.085f, 0.1f, 0f), Quaternion.Euler(0f, 0f, -sx * 20f));
                kit.Frustum(Vector3.zero, 0.045f, 0.004f, 0.08f, 6, fur, fur);
                kit.Pop();
                kit.Push(h + new Vector3(sx * 0.083f, 0.1f, 0.012f), Quaternion.Euler(0f, 0f, -sx * 20f));
                kit.Frustum(Vector3.zero, 0.025f, 0.003f, 0.05f, 6, pink, pink);
                kit.Pop();
            }
            // 주둥이 + 코 + 눈 + 볼 + 수염
            kit.Sphere(h + new Vector3(0f, -0.04f, 0.09f), new Vector3(0.06f, 0.04f, 0.035f), 10, 4, white);
            Vector3 fwd = Vector3.forward;
            kit.DiscFacing(h + new Vector3(0f, -0.02f, 0.127f), new Vector2(0.012f, 0.009f), fwd, 8, pink);
            foreach (float sx in new[] { -1f, 1f })
            {
                var d = new Vector3(sx * 0.42f, 0.05f, 0.9f).normalized;
                var p = h + Vector3.Scale(d, hr) + d * 0.004f;
                kit.DiscFacing(p, new Vector2(0.022f, 0.03f), d, 12, ink);
                Vector3 right = Vector3.Cross(Vector3.up, d).normalized;
                kit.DiscFacing(p + d * 0.002f + right * 0.008f + Vector3.up * 0.011f, new Vector2(0.008f, 0.008f), d, 8, white);
                var bd = new Vector3(sx * 0.65f, -0.25f, 0.7f).normalized;
                kit.DiscFacing(h + Vector3.Scale(bd, hr) + bd * 0.004f, new Vector2(0.025f, 0.014f), bd, 10, pink);
                for (int w = 0; w < 2; w++)
                {
                    kit.Push(h + new Vector3(sx * 0.07f, -0.035f - w * 0.012f, 0.1f), Quaternion.Euler(0f, 0f, 90f + sx * (8f - w * 10f)));
                    kit.Cylinder(new Vector3(0f, sx * 0.04f, 0f), 0.002f, 0.08f, 3, ink);
                    kit.Pop();
                }
            }
            kit.Build("Cat", root.transform, mats.VertexColor);
            kit.SmoothSpheres = false;
            kit.SmoothUpBias = 0f;
            VisualAssets.Blob(root.transform, new Vector3(0f, 0.004f, 0f), new Vector2(0.36f, 0.3f), mats.Blob);
            root.AddComponent<CatIdle>();
            return root;
        }
    }

    /// <summary>숨 쉬듯 몸이 살짝 부풀었다 줄어들고, 가끔 고개를 갸웃.</summary>
    public class CatIdle : MonoBehaviour
    {
        Transform body;
        float seed;

        void Start()
        {
            body = transform.Find("Cat");
            seed = Random.value * 10f;
        }

        void Update()
        {
            if (body == null) return;
            float t = Time.time + seed;
            float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.025f;
            body.localScale = new Vector3(1f, breathe, 1f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f) > 0.85f ? 8f : 0f);
        }
    }
}
