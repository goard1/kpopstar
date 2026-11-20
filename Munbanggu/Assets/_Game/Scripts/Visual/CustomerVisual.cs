using Munbanggu.Core;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>손님 외형 지정. 콘텐츠 쪽(단골 등)이 채우고, 비주얼 쪽이 해석한다.</summary>
    public struct CustomerLook
    {
        public int seed;          // 같은 seed = 같은 외형
        public bool isRegular;    // 단골이면 눈에 띄게
        public string regularId;  // 단골 고유 ID (없으면 null)
    }

    /// <summary>
    /// 손님(초등학생) 3D 외형 생성. 부품 전부를 메시 하나로 합친다 (손님당 드로우콜 1 + 그림자 1).
    /// 반환한 Transform은 Customer가 걷기 흔들림에 쓴다. 앞 = +Z.
    /// 단골: regularId로 외형 고정 + 노란 학교 모자 + 주황 외곽선.
    /// </summary>
    public static class CustomerVisual
    {
        static readonly MeshKit kit = new MeshKit();

        static readonly Color Ink = new Color(0.18f, 0.13f, 0.13f);
        static readonly Color White = new Color(0.98f, 0.97f, 0.94f);
        static readonly Color Cheek = new Color(1.0f, 0.62f, 0.62f);
        static readonly Color Mouth = new Color(0.75f, 0.30f, 0.30f);
        static readonly Color SafetyYellow = new Color(1.0f, 0.85f, 0.15f);

        static readonly Color[] Skins =
        {
            new Color(1.00f, 0.86f, 0.72f), new Color(0.98f, 0.80f, 0.64f), new Color(0.92f, 0.73f, 0.56f),
        };
        static readonly Color[] Hairs =
        {
            new Color(0.15f, 0.11f, 0.10f), new Color(0.22f, 0.15f, 0.12f), new Color(0.32f, 0.22f, 0.16f),
        };
        static readonly Color[] Shirts =
        {
            new Color(0.95f, 0.40f, 0.40f), new Color(0.35f, 0.60f, 0.92f), new Color(0.99f, 0.82f, 0.30f),
            new Color(0.50f, 0.80f, 0.50f), new Color(0.98f, 0.62f, 0.75f), new Color(0.98f, 0.97f, 0.92f),
            new Color(0.62f, 0.50f, 0.88f), new Color(0.98f, 0.60f, 0.30f), new Color(0.45f, 0.82f, 0.85f),
        };
        static readonly Color[] Bottoms =
        {
            new Color(0.20f, 0.26f, 0.45f), new Color(0.35f, 0.48f, 0.70f), new Color(0.45f, 0.35f, 0.28f),
            new Color(0.70f, 0.62f, 0.45f), new Color(0.25f, 0.25f, 0.28f),
        };
        static readonly Color[] Skirts =
        {
            new Color(0.85f, 0.25f, 0.30f), new Color(0.22f, 0.28f, 0.50f), new Color(0.95f, 0.55f, 0.70f), new Color(0.55f, 0.40f, 0.75f),
        };
        static readonly Color[] Bags =
        {
            new Color(0.88f, 0.22f, 0.22f), new Color(0.22f, 0.42f, 0.85f), new Color(0.98f, 0.75f, 0.15f),
            new Color(0.95f, 0.50f, 0.65f), new Color(0.30f, 0.65f, 0.40f), new Color(0.25f, 0.25f, 0.40f),
        };
        static readonly Color[] Shoes =
        {
            new Color(0.97f, 0.97f, 0.97f), new Color(0.85f, 0.25f, 0.25f), new Color(0.25f, 0.40f, 0.80f), new Color(0.3f, 0.3f, 0.32f),
        };

        public static Transform Build(Transform parent, MaterialCache mats, CustomerLook look)
        {
            int seed = look.seed;
            if (look.isRegular && !string.IsNullOrEmpty(look.regularId)) seed = StableHash(look.regularId);
            var rng = new System.Random(seed);

            kit.Clear();
            BuildKid(kit, rng, look.isRegular);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);
            var mesh = kit.ToMesh("Customer");
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.gameObject.AddComponent<OwnedMesh>().mesh = mesh;
            var r = visual.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = look.isRegular ? mats.RegularVertexColor : mats.VertexColor;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // 가짜 그림자는 흔들리지 않게 부모에 붙임
            VisualAssets.Blob(parent, new Vector3(0f, 0.006f, 0f), new Vector2(0.62f, 0.62f), mats.Blob);
            return visual;
        }

        /// <summary>문자열 → 실행마다 같은 정수 (string.GetHashCode는 플랫폼마다 다를 수 있음).</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }

        static T Pick<T>(System.Random rng, T[] arr) => arr[rng.Next(arr.Length)];

        // ───────────── 어린이 ─────────────

        static void BuildKid(MeshKit k, System.Random rng, bool regular)
        {
            float height = 0.95f + (float)rng.NextDouble() * 0.18f;  // 키
            float width = 0.95f + (float)rng.NextDouble() * 0.12f;   // 체격
            Color skin = Pick(rng, Skins);
            Color hair = Pick(rng, Hairs);
            Color shirt = Pick(rng, Shirts);
            Color bag = Pick(rng, Bags);
            Color shoe = Pick(rng, Shoes);
            int bottomType = rng.Next(3);   // 0 긴바지, 1 반바지, 2 치마
            Color bottom = bottomType == 2 ? Pick(rng, Skirts) : Pick(rng, Bottoms);
            int hairStyle = rng.Next(6);
            int bagType = rng.Next(10);     // 0~6 책가방, 7~8 크로스백, 9 없음
            bool nameTag = rng.Next(3) == 0;
            bool stripe = rng.Next(3) == 0;
            Color stripeColor = Pick(rng, Shirts);

            k.Push(Vector3.zero, Quaternion.identity, new Vector3(width, height, width));

            // 신발·다리
            foreach (float sx in new[] { -0.075f, 0.075f })
            {
                k.Box(new Vector3(sx, 0.03f, 0.02f), new Vector3(0.09f, 0.06f, 0.15f), shoe, shoe);
                Color leg = bottomType == 0 ? bottom : skin;
                k.Cylinder(new Vector3(sx, 0.16f, 0f), 0.045f, 0.2f, 6, leg);
                if (bottomType != 0) k.Cylinder(new Vector3(sx, 0.08f, 0f), 0.048f, 0.05f, 6, White); // 양말
            }
            if (bottomType == 1) k.Frustum(new Vector3(0f, 0.28f, 0f), 0.16f, 0.15f, 0.1f, 8, bottom, bottom);
            if (bottomType == 2) k.Frustum(new Vector3(0f, 0.29f, 0f), 0.21f, 0.15f, 0.14f, 8, bottom, bottom);

            // 몸통·팔
            k.Frustum(new Vector3(0f, 0.41f, 0f), 0.15f, 0.12f, 0.27f, 8, shirt, shirt);
            if (stripe) k.Frustum(new Vector3(0f, 0.43f, 0f), 0.143f, 0.135f, 0.05f, 8, stripeColor, stripeColor);
            k.Cylinder(new Vector3(0f, 0.555f, 0f), 0.075f, 0.03f, 8, shirt == White ? Pick(rng, Bags) : White); // 깃
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.15f, 0.5f, 0f), Quaternion.Euler(0f, 0f, side * 14f));
                k.Cylinder(new Vector3(0f, -0.09f, 0f), 0.04f, 0.18f, 6, shirt);
                k.Sphere(new Vector3(0f, -0.2f, 0f), Vector3.one * 0.045f, 6, 4, skin);
                k.Pop();
            }
            if (nameTag) k.FlatBox(new Vector3(0.06f, 0.46f, 0.128f), new Vector3(0.065f, 0.03f, 0.012f), White);

            // 머리·얼굴
            Head(k, skin, new Vector3(0f, 0.77f, 0f));
            Hair(k, hairStyle, hair, rng, regular);

            // 가방
            if (bagType <= 6)
            {
                k.Box(new Vector3(0f, 0.42f, -0.2f), new Vector3(0.3f, 0.3f, 0.13f), bag, bag);
                k.Box(new Vector3(0f, 0.52f, -0.205f), new Vector3(0.315f, 0.12f, 0.145f), MeshKit.Shade(bag, 0.82f), MeshKit.Shade(bag, 0.82f));
                k.Box(new Vector3(0f, 0.47f, -0.278f), new Vector3(0.06f, 0.04f, 0.02f), SafetyYellow, SafetyYellow); // 잠금쇠
                foreach (float sx in new[] { -0.08f, 0.08f })
                    k.Box(new Vector3(sx, 0.545f, -0.04f), new Vector3(0.04f, 0.025f, 0.28f), MeshKit.Shade(bag, 0.7f), MeshKit.Shade(bag, 0.7f));
            }
            else if (bagType <= 8)
            {
                k.Push(new Vector3(0f, 0.42f, 0f), Quaternion.Euler(0f, 0f, 38f));
                k.Box(new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.42f, 0.29f), MeshKit.Shade(bag, 0.7f), MeshKit.Shade(bag, 0.7f));
                k.Pop();
                k.Box(new Vector3(0.17f, 0.3f, 0.03f), new Vector3(0.07f, 0.16f, 0.18f), bag, bag);
            }

            k.Pop();
        }

        static void Head(MeshKit k, Color skin, Vector3 c)
        {
            k.Sphere(c, new Vector3(0.2f, 0.19f, 0.19f), 10, 7, skin);
            // 눈·볼·입 (외곽선 없음)
            k.FlatBox(c + new Vector3(-0.07f, -0.025f, 0.172f), new Vector3(0.035f, 0.055f, 0.03f), Ink);
            k.FlatBox(c + new Vector3(0.07f, -0.025f, 0.172f), new Vector3(0.035f, 0.055f, 0.03f), Ink);
            k.FlatBox(c + new Vector3(-0.115f, -0.075f, 0.145f), new Vector3(0.05f, 0.028f, 0.03f), Cheek);
            k.FlatBox(c + new Vector3(0.115f, -0.075f, 0.145f), new Vector3(0.05f, 0.028f, 0.03f), Cheek);
            k.FlatBox(c + new Vector3(0f, -0.09f, 0.168f), new Vector3(0.045f, 0.014f, 0.025f), Mouth);
        }

        static void Hair(MeshKit k, int style, Color hair, System.Random rng, bool regular)
        {
            Vector3 head = new Vector3(0f, 0.77f, 0f);
            Color accent = Pick(rng, Bags);
            switch (style)
            {
                case 0: // 바가지 머리
                    k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.212f, 0.19f, 0.2f), 10, 6, hair);
                    k.Box(head + new Vector3(0f, 0.12f, 0.13f), new Vector3(0.3f, 0.06f, 0.06f), hair, hair);
                    break;
                case 1: // 짧은 머리
                    k.Sphere(head + new Vector3(0f, 0.06f, -0.035f), new Vector3(0.205f, 0.17f, 0.195f), 10, 6, hair);
                    break;
                case 2: // 양갈래
                    k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.21f, 0.19f, 0.2f), 10, 6, hair);
                    foreach (float sx in new[] { -0.21f, 0.21f })
                    {
                        k.Sphere(head + new Vector3(sx, -0.06f, -0.05f), new Vector3(0.075f, 0.1f, 0.075f), 7, 5, hair);
                        k.Cylinder(head + new Vector3(sx * 0.93f, 0.02f, -0.05f), 0.04f, 0.04f, 6, accent);
                    }
                    break;
                case 3: // 묶은 머리
                    k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.21f, 0.19f, 0.2f), 10, 6, hair);
                    k.Sphere(head + new Vector3(0f, 0.03f, -0.23f), new Vector3(0.08f, 0.11f, 0.08f), 7, 5, hair);
                    k.Cylinder(head + new Vector3(0f, 0.1f, -0.2f), 0.045f, 0.04f, 6, accent);
                    break;
                case 4: // 단발 + 머리띠
                    k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.215f, 0.19f, 0.2f), 10, 6, hair);
                    foreach (float sx in new[] { -0.17f, 0.17f })
                        k.Box(head + new Vector3(sx, -0.07f, -0.03f), new Vector3(0.08f, 0.17f, 0.26f), hair, hair);
                    k.Push(head + new Vector3(0f, 0.08f, 0.02f), Quaternion.Euler(-20f, 0f, 0f));
                    k.Frustum(Vector3.zero, 0.205f, 0.2f, 0.035f, 10, accent, accent, false, false);
                    k.Pop();
                    break;
                default: // 야구 모자
                    k.Sphere(head + new Vector3(0f, 0.04f, -0.035f), new Vector3(0.205f, 0.17f, 0.195f), 10, 6, hair);
                    if (!regular)
                    {
                        // 거꾸로 쓴 야구 모자: 챙이 뒤로 가서 이마·눈이 보인다
                        k.Sphere(head + new Vector3(0f, 0.125f, -0.045f), new Vector3(0.21f, 0.125f, 0.21f), 10, 4, accent);
                        k.Box(head + new Vector3(0f, 0.12f, -0.2f), new Vector3(0.22f, 0.025f, 0.13f), accent, accent);
                    }
                    break;
            }

            if (regular) // 단골: 노란 학교 모자 + 빨간 별
            {
                // 모자는 머리 위·뒤로 올리고 챙을 작게 앞쪽만 살짝 들어 올려 얼굴이 보이게 한다
                k.Sphere(head + new Vector3(0f, 0.15f, -0.05f), new Vector3(0.215f, 0.12f, 0.215f), 10, 4, SafetyYellow);
                k.Push(head + new Vector3(0f, 0.125f, -0.04f), Quaternion.Euler(-14f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.235f, 0.022f, 12, SafetyYellow, SafetyYellow);
                k.Pop();
                k.Push(head + new Vector3(0f, 0.245f, 0.09f), Quaternion.Euler(45f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.05f, 0.02f, 5, new Color(0.9f, 0.2f, 0.2f), new Color(0.9f, 0.2f, 0.2f));
                k.Pop();
            }
        }

        // ───────────── 주인 할머니 (가게 장식) ─────────────

        /// <summary>의자에 앉은 문방구 할머니. 원점 = 바닥, 앞 = +Z. 신문을 보는 중.</summary>
        public static void BuildOwner(MeshKit k)
        {
            Color skin = new Color(0.98f, 0.82f, 0.68f);
            Color hair = new Color(0.80f, 0.79f, 0.80f);
            Color cardigan = new Color(0.55f, 0.68f, 0.48f);
            Color pants = new Color(0.58f, 0.42f, 0.62f);
            Color apron = new Color(0.96f, 0.82f, 0.62f);
            Color shoe = new Color(0.35f, 0.30f, 0.30f);

            // 다리 (앉은 자세)
            foreach (float sx in new[] { -0.08f, 0.08f })
            {
                k.Push(new Vector3(sx, 0.3f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.06f, 0.22f, 6, pants);
                k.Pop();
                k.Cylinder(new Vector3(sx, 0.16f, 0.22f), 0.055f, 0.28f, 6, pants);
                k.Box(new Vector3(sx, 0.03f, 0.26f), new Vector3(0.1f, 0.06f, 0.16f), shoe, shoe);
            }
            // 몸통 + 앞치마
            k.Frustum(new Vector3(0f, 0.5f, 0f), 0.19f, 0.15f, 0.38f, 8, cardigan, cardigan);
            k.FlatBox(new Vector3(0f, 0.43f, 0.17f), new Vector3(0.24f, 0.22f, 0.03f), apron);
            k.FlatBox(new Vector3(0f, 0.43f, 0.188f), new Vector3(0.1f, 0.06f, 0.004f), MeshKit.Shade(apron, 0.85f));
            // 팔 + 신문
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.17f, 0.62f, 0.02f), Quaternion.Euler(-65f, 0f, side * 8f));
                k.Cylinder(new Vector3(0f, -0.1f, 0f), 0.045f, 0.2f, 6, cardigan);
                k.Sphere(new Vector3(0f, -0.21f, 0f), Vector3.one * 0.045f, 6, 4, skin);
                k.Pop();
            }
            k.Push(new Vector3(0f, 0.6f, 0.27f), Quaternion.Euler(-25f, 0f, 0f));
            k.Box(Vector3.zero, new Vector3(0.38f, 0.26f, 0.015f), new Color(0.96f, 0.95f, 0.90f), new Color(0.9f, 0.88f, 0.82f));
            for (int i = 0; i < 4; i++)
                k.FlatBox(new Vector3(-0.08f, 0.08f - i * 0.05f, -0.01f), new Vector3(0.16f, 0.018f, 0.004f), new Color(0.55f, 0.55f, 0.55f));
            k.FlatBox(new Vector3(0.1f, 0.02f, -0.01f), new Vector3(0.12f, 0.16f, 0.004f), new Color(0.6f, 0.6f, 0.65f));
            k.Pop();

            // 머리
            var head = new Vector3(0f, 0.9f, 0f);
            Head(k, skin, head);
            k.Sphere(head + new Vector3(0f, 0.06f, -0.035f), new Vector3(0.21f, 0.17f, 0.2f), 10, 6, hair);
            k.Sphere(head + new Vector3(0f, 0.14f, -0.14f), new Vector3(0.09f, 0.08f, 0.09f), 8, 5, hair); // 쪽진 머리
            // 안경
            foreach (float sx in new[] { -0.07f, 0.07f })
            {
                k.Push(head + new Vector3(sx, -0.005f, 0.19f), Quaternion.Euler(-90f, 0f, 0f));
                k.Frustum(Vector3.zero, 0.05f, 0.05f, 0.012f, 8, Ink, new Color(0.85f, 0.93f, 0.96f));
                k.Pop();
            }
            k.FlatBox(head + new Vector3(0f, 0f, 0.195f), new Vector3(0.05f, 0.012f, 0.01f), Ink);
        }
    }
}
