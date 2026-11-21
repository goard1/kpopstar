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
    /// 손님(초등학생) 3D 외형 — 아주 귀여운 2등신 꼬마.
    /// 큰 머리(키의 절반), 반짝이는 큰 눈 + 볼터치, 동글동글 몸·손·발, 파스텔 옷.
    /// 부품 전부를 메시 하나로 합친다 (손님당 드로우콜 1 + 그림자 1). 앞 = +Z, 키 ≈ 1.0.
    /// 단골: regularId로 외형 고정 + 노란 학교 모자(빨간 별).
    /// </summary>
    public static class CustomerVisual
    {
        static readonly MeshKit kit = new MeshKit();

        static readonly Color Ink = new Color(0.20f, 0.13f, 0.14f);
        static readonly Color EyeBrown = new Color(0.26f, 0.16f, 0.14f);
        static readonly Color White = new Color(1.00f, 0.99f, 0.97f);
        static readonly Color Blush = new Color(1.00f, 0.60f, 0.64f);
        static readonly Color Mouth = new Color(0.78f, 0.28f, 0.34f);
        static readonly Color Tongue = new Color(1.00f, 0.55f, 0.58f);
        static readonly Color SafetyYellow = new Color(1.00f, 0.86f, 0.25f);

        static readonly Color[] Skins =
        {
            new Color(1.00f, 0.88f, 0.78f), new Color(1.00f, 0.84f, 0.72f), new Color(0.96f, 0.78f, 0.64f),
        };
        static readonly Color[] Hairs =
        {
            new Color(0.22f, 0.15f, 0.13f), new Color(0.34f, 0.22f, 0.16f), new Color(0.48f, 0.32f, 0.22f), new Color(0.16f, 0.13f, 0.16f),
        };
        // 파스텔 옷
        static readonly Color[] Shirts =
        {
            new Color(1.00f, 0.62f, 0.66f), new Color(0.58f, 0.76f, 1.00f), new Color(1.00f, 0.88f, 0.48f),
            new Color(0.62f, 0.88f, 0.66f), new Color(1.00f, 0.74f, 0.84f), new Color(1.00f, 0.98f, 0.92f),
            new Color(0.78f, 0.68f, 0.98f), new Color(1.00f, 0.74f, 0.52f), new Color(0.58f, 0.90f, 0.90f),
        };
        static readonly Color[] Bottoms =
        {
            new Color(0.38f, 0.48f, 0.78f), new Color(0.50f, 0.64f, 0.88f), new Color(0.62f, 0.48f, 0.40f), new Color(0.48f, 0.46f, 0.56f),
        };
        static readonly Color[] Skirts =
        {
            new Color(1.00f, 0.50f, 0.58f), new Color(0.48f, 0.56f, 0.90f), new Color(1.00f, 0.70f, 0.82f), new Color(0.72f, 0.58f, 0.92f),
        };
        static readonly Color[] Accents =
        {
            new Color(1.00f, 0.38f, 0.48f), new Color(0.40f, 0.62f, 1.00f), new Color(1.00f, 0.80f, 0.25f),
            new Color(1.00f, 0.56f, 0.76f), new Color(0.42f, 0.82f, 0.56f), new Color(0.70f, 0.52f, 0.95f),
        };
        static readonly Color[] Shoes =
        {
            new Color(1.00f, 0.98f, 0.96f), new Color(1.00f, 0.45f, 0.50f), new Color(0.45f, 0.62f, 1.00f), new Color(1.00f, 0.80f, 0.35f),
        };

        public static Transform Build(Transform parent, MaterialCache mats, CustomerLook look)
        {
            int seed = look.seed;
            if (look.isRegular && !string.IsNullOrEmpty(look.regularId)) seed = StableHash(look.regularId);
            var rng = new System.Random(seed);

            kit.Clear();
            kit.SmoothSpheres = true;
            kit.SmoothUpBias = 0.55f;
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
            VisualAssets.Blob(parent, new Vector3(0f, 0.006f, 0f), new Vector2(0.6f, 0.6f), mats.Blob);
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

        // ───────────── 비율 (직원 앞치마 등이 같이 씀) ─────────────

        /// <summary>몸통 가운데·반지름 (2등신: 몸은 작고 동글).</summary>
        public static readonly Vector3 BodyCenter = new Vector3(0f, 0.27f, 0f);
        public static readonly Vector3 BodyRadius = new Vector3(0.15f, 0.14f, 0.13f);
        /// <summary>머리 가운데·반지름 (키의 절반 가까이).</summary>
        public static readonly Vector3 HeadCenter = new Vector3(0f, 0.6f, 0f);
        public static readonly Vector3 HeadRadius = new Vector3(0.26f, 0.24f, 0.24f);

        // ───────────── 어린이 ─────────────

        static void BuildKid(MeshKit k, System.Random rng, bool regular)
        {
            float height = 0.96f + (float)rng.NextDouble() * 0.1f;   // 키 (조금만 차이)
            float width = 0.97f + (float)rng.NextDouble() * 0.08f;   // 체격
            Color skin = Pick(rng, Skins);
            Color hair = Pick(rng, Hairs);
            Color shirt = Pick(rng, Shirts);
            Color accent = Pick(rng, Accents);
            Color shoe = Pick(rng, Shoes);
            int bottomType = rng.Next(3);   // 0 긴바지, 1 반바지, 2 치마
            Color bottom = bottomType == 2 ? Pick(rng, Skirts) : Pick(rng, Bottoms);
            int hairStyle = rng.Next(7);
            int bagType = rng.Next(10);     // 0~6 책가방, 7~8 크로스백, 9 없음
            int mouthType = rng.Next(3);
            bool collar = rng.Next(2) == 0;
            bool pocketHeart = rng.Next(3) == 0;

            k.Push(Vector3.zero, Quaternion.identity, new Vector3(width, height, width));

            // 발 (동글 신발) + 짧은 다리
            foreach (float sx in new[] { -0.07f, 0.07f })
            {
                k.Sphere(new Vector3(sx, 0.045f, 0.025f), new Vector3(0.068f, 0.048f, 0.09f), 8, 4, shoe);
                Color leg = bottomType == 0 ? bottom : skin;
                k.Cylinder(new Vector3(sx, 0.12f, 0f), 0.042f, 0.1f, 8, leg);
            }

            // 바지·치마 (몸통 아래)
            if (bottomType == 2)
                k.Frustum(new Vector3(0f, 0.18f, 0f), 0.19f, 0.13f, 0.11f, 10, bottom, bottom);
            else
                k.Sphere(new Vector3(0f, 0.19f, 0f), new Vector3(0.145f, 0.085f, 0.125f), 10, 4, bottom);

            // 몸통 (달걀형)
            k.Sphere(BodyCenter, BodyRadius, 12, 6, shirt);
            if (collar) // 둥근 깃
            {
                k.Sphere(new Vector3(-0.05f, 0.37f, 0.08f), new Vector3(0.06f, 0.025f, 0.04f), 8, 3, White);
                k.Sphere(new Vector3(0.05f, 0.37f, 0.08f), new Vector3(0.06f, 0.025f, 0.04f), 8, 3, White);
            }
            if (pocketHeart) Heart(k, new Vector3(0.055f, 0.27f, 0.128f), 0.03f, accent);

            // 팔 (짧고 통통) + 동그란 손
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.13f, 0.33f, 0f), Quaternion.Euler(0f, 0f, side * 28f));
                k.Sphere(new Vector3(0f, -0.06f, 0f), new Vector3(0.045f, 0.08f, 0.045f), 8, 4, shirt);
                k.Sphere(new Vector3(0f, -0.14f, 0.01f), Vector3.one * 0.045f, 8, 4, skin);
                k.Pop();
            }

            // 머리·얼굴·머리카락
            Head(k, skin, HeadCenter, HeadRadius, mouthType);
            Hair(k, hairStyle, hair, accent, regular);

            // 가방 (동글한 책가방 + 열쇠고리)
            if (bagType <= 6)
            {
                k.Sphere(new Vector3(0f, 0.3f, -0.17f), new Vector3(0.15f, 0.15f, 0.075f), 10, 5, accent);
                k.Sphere(new Vector3(0f, 0.36f, -0.2f), new Vector3(0.155f, 0.08f, 0.06f), 10, 4, MeshKit.Shade(accent, 0.85f));
                k.Sphere(new Vector3(0.09f, 0.24f, -0.24f), Vector3.one * 0.03f, 6, 4, SafetyYellow); // 열쇠고리 방울
                foreach (float sx in new[] { -0.08f, 0.08f })
                    k.Box(new Vector3(sx, 0.38f, -0.06f), new Vector3(0.035f, 0.022f, 0.2f), MeshKit.Shade(accent, 0.75f), MeshKit.Shade(accent, 0.75f));
            }
            else if (bagType <= 8)
            {
                k.Push(new Vector3(0f, 0.3f, 0f), Quaternion.Euler(0f, 0f, 40f));
                k.Box(Vector3.zero, new Vector3(0.025f, 0.34f, 0.27f), MeshKit.Shade(accent, 0.7f), MeshKit.Shade(accent, 0.7f));
                k.Pop();
                k.Sphere(new Vector3(0.16f, 0.2f, 0.04f), new Vector3(0.055f, 0.06f, 0.075f), 8, 4, accent);
            }

            k.Pop();
        }

        /// <summary>얼굴 표면의 점 (머리 가운데에서 dir 방향, 살짝 띄움).</summary>
        static Vector3 OnHead(Vector3 c, Vector3 r, Vector3 dir, float lift = 0.006f)
        {
            dir.Normalize();
            return c + Vector3.Scale(dir, r) + dir * lift;
        }

        static void Head(MeshKit k, Color skin, Vector3 c, Vector3 r, int mouthType)
        {
            k.Sphere(c, r, 14, 8, skin);
            // 볼에 살짝 귀
            foreach (float sx in new[] { -1f, 1f })
                k.Sphere(c + new Vector3(sx * r.x * 0.97f, -0.02f, 0f), new Vector3(0.035f, 0.05f, 0.04f), 6, 4, skin);

            // 큰 눈 (세로 타원) + 반짝이 두 개 (반짝이는 눈 판 위에서 판 기준으로 옮김)
            foreach (float sx in new[] { -1f, 1f })
            {
                var d = new Vector3(sx * 0.36f, -0.1f, 0.93f);
                var p = OnHead(c, r, d);
                var n = d.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, n).normalized; // 얼굴에서 보면 왼쪽
                Vector3 up = Vector3.Cross(n, right).normalized;
                k.DiscFacing(p, new Vector2(0.046f, 0.062f), n, 16, EyeBrown);
                k.DiscFacing(p + n * 0.003f + right * 0.016f + up * 0.022f, new Vector2(0.019f, 0.019f), n, 12, White);
                k.DiscFacing(p + n * 0.003f - right * 0.014f - up * 0.022f, new Vector2(0.009f, 0.009f), n, 8, White);
                // 볼터치
                var bd = new Vector3(sx * 0.6f, -0.33f, 0.73f);
                k.DiscFacing(OnHead(c, r, bd), new Vector2(0.05f, 0.03f), bd.normalized, 12, Blush);
            }

            // 입
            var md = new Vector3(0f, -0.36f, 0.93f);
            var mp = OnHead(c, r, md);
            var mn = md.normalized;
            switch (mouthType)
            {
                case 0: // 활짝 웃는 입 (반달 + 혀)
                    k.DiscFacing(mp, new Vector2(0.03f, 0.022f), mn, 12, Mouth);
                    k.DiscFacing(mp + mn * 0.002f + new Vector3(0f, -0.008f, 0f), new Vector2(0.018f, 0.01f), mn, 10, Tongue);
                    break;
                case 1: // 고양이 입 (ω)
                    k.DiscFacing(mp + new Vector3(-0.012f, 0f, 0f), new Vector2(0.014f, 0.008f), mn, 8, Ink);
                    k.DiscFacing(mp + new Vector3(0.012f, 0f, 0f), new Vector2(0.014f, 0.008f), mn, 8, Ink);
                    break;
                default: // 동그랗게 "오"
                    k.DiscFacing(mp, new Vector2(0.014f, 0.016f), mn, 10, Mouth);
                    break;
            }
        }

        static void Heart(MeshKit k, Vector3 p, float s, Color color)
        {
            k.Sphere(p + new Vector3(-s * 0.45f, s * 0.25f, 0f), new Vector3(s * 0.55f, s * 0.55f, s * 0.3f), 6, 4, color);
            k.Sphere(p + new Vector3(s * 0.45f, s * 0.25f, 0f), new Vector3(s * 0.55f, s * 0.55f, s * 0.3f), 6, 4, color);
            k.Push(p + new Vector3(0f, -s * 0.2f, 0f), Quaternion.Euler(0f, 0f, 45f));
            k.Box(Vector3.zero, new Vector3(s * 0.85f, s * 0.85f, s * 0.5f), color, color, false);
            k.Pop();
        }

        static void Bow(MeshKit k, Vector3 p, float s, Color color, float yaw = 0f)
        {
            k.Push(p, Quaternion.Euler(0f, yaw, 0f));
            k.Sphere(new Vector3(-s, 0f, 0f), new Vector3(s * 0.9f, s * 0.65f, s * 0.35f), 8, 4, color);
            k.Sphere(new Vector3(s, 0f, 0f), new Vector3(s * 0.9f, s * 0.65f, s * 0.35f), 8, 4, color);
            k.Sphere(Vector3.zero, Vector3.one * s * 0.4f, 6, 4, MeshKit.Shade(color, 0.85f));
            k.Pop();
        }

        static void Hair(MeshKit k, int style, Color hair, Color accent, bool regular)
        {
            Vector3 h = HeadCenter;
            Vector3 cap = new Vector3(0.272f, 0.245f, 0.258f);
            // 기본 머리 (뒤·위를 덮는 반구) + 앞머리
            k.Sphere(h + new Vector3(0f, 0.035f, -0.025f), cap, 14, 7, hair);
            k.Sphere(h + new Vector3(0f, 0.15f, 0.12f), new Vector3(0.24f, 0.085f, 0.13f), 12, 4, hair);

            switch (style)
            {
                case 0: // 동글 바가지 + 볼까지 오는 옆머리
                    foreach (float sx in new[] { -1f, 1f })
                        k.Sphere(h + new Vector3(sx * 0.215f, -0.06f, 0.02f), new Vector3(0.07f, 0.13f, 0.11f), 8, 4, hair);
                    break;
                case 1: // 짧은 머리 + 새싹 머리카락
                    k.Push(h + new Vector3(0.02f, 0.27f, 0.02f), Quaternion.Euler(0f, 0f, -25f));
                    k.Sphere(Vector3.zero, new Vector3(0.025f, 0.07f, 0.025f), 6, 4, hair);
                    k.Pop();
                    k.Sphere(h + new Vector3(0.055f, 0.33f, 0.02f), new Vector3(0.035f, 0.025f, 0.025f), 6, 3, hair);
                    break;
                case 2: // 양쪽 똥머리 + 리본
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        k.Sphere(h + new Vector3(sx * 0.19f, 0.2f, -0.04f), Vector3.one * 0.09f, 10, 5, hair);
                        Bow(k, h + new Vector3(sx * 0.15f, 0.13f, 0.02f), 0.035f, accent, sx * 25f);
                    }
                    break;
                case 3: // 양갈래 + 방울
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        k.Sphere(h + new Vector3(sx * 0.27f, -0.07f, -0.06f), new Vector3(0.07f, 0.13f, 0.07f), 8, 5, hair);
                        k.Sphere(h + new Vector3(sx * 0.26f, 0.04f, -0.06f), Vector3.one * 0.035f, 6, 4, accent);
                    }
                    break;
                case 4: // 단발 + 큰 리본 머리띠
                    foreach (float sx in new[] { -1f, 1f })
                        k.Sphere(h + new Vector3(sx * 0.2f, -0.08f, -0.02f), new Vector3(0.09f, 0.15f, 0.17f), 8, 5, hair);
                    k.Push(h + new Vector3(0f, 0.12f, 0.03f), Quaternion.Euler(-20f, 0f, 0f));
                    k.Frustum(Vector3.zero, 0.262f, 0.258f, 0.03f, 14, accent, accent, false, false);
                    k.Pop();
                    Bow(k, h + new Vector3(0.14f, 0.24f, 0.04f), 0.05f, accent, 20f);
                    break;
                case 5: // 고양이 귀 비니
                    k.Sphere(h + new Vector3(0f, 0.09f, -0.02f), new Vector3(0.28f, 0.2f, 0.27f), 14, 5, accent);
                    k.Cylinder(h + new Vector3(0f, 0.07f, 0f), 0.276f, 0.05f, 14, MeshKit.Shade(accent, 0.9f));
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        k.Push(h + new Vector3(sx * 0.15f, 0.27f, 0f), Quaternion.Euler(0f, 0f, -sx * 18f));
                        k.Frustum(Vector3.zero, 0.07f, 0.005f, 0.12f, 6, accent, accent);
                        k.Pop();
                    }
                    break;
                default: // 포니테일 + 큰 리본
                    k.Sphere(h + new Vector3(0f, 0.04f, -0.29f), new Vector3(0.075f, 0.14f, 0.08f), 8, 5, hair);
                    Bow(k, h + new Vector3(0f, 0.13f, -0.25f), 0.055f, accent);
                    break;
            }

            if (regular) // 단골: 노란 학교 모자 + 빨간 별
            {
                k.Sphere(h + new Vector3(0f, 0.12f, -0.03f), new Vector3(0.285f, 0.18f, 0.285f), 14, 5, SafetyYellow);
                k.Push(h + new Vector3(0f, 0.13f, 0f), Quaternion.Euler(-16f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.31f, 0.025f, 16, SafetyYellow, SafetyYellow);
                k.Pop();
                var sp = h + new Vector3(0f, 0.25f, 0.18f);
                k.DiscFacing(sp, new Vector2(0.045f, 0.045f), new Vector3(0f, 0.6f, 1f), 5, new Color(0.95f, 0.25f, 0.28f));
            }
        }

        // ───────────── 주인공 손주 (가게 주인, docs/11) ─────────────

        /// <summary>의자에 앉아 만화책을 읽는 2등신 손주 (손녀: 묶은 머리 + 리본, 손자: 짧은 머리). 노란 가게 앞치마.</summary>
        public static void BuildHeroOwner(MeshKit k, bool girl)
        {
            Color skin = new Color(1.0f, 0.86f, 0.76f);
            Color hair = new Color(0.36f, 0.24f, 0.18f);
            Color shirt = girl ? new Color(0.98f, 0.64f, 0.70f) : new Color(0.48f, 0.68f, 0.95f);
            Color apron = new Color(1.0f, 0.78f, 0.30f);
            Color pants = new Color(0.36f, 0.42f, 0.62f);
            Color shoe = new Color(0.96f, 0.96f, 0.96f);
            Color ribbon = new Color(0.95f, 0.42f, 0.50f);
            bool prevSmooth = k.SmoothSpheres;
            float prevBias = k.SmoothUpBias;
            k.SmoothUpBias = 0.55f;
            k.SmoothSpheres = true;

            // 다리 (앉은 자세)
            foreach (float sx in new[] { -0.075f, 0.075f })
            {
                k.Push(new Vector3(sx, 0.31f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.055f, 0.16f, 8, pants);
                k.Pop();
                k.Cylinder(new Vector3(sx, 0.18f, 0.18f), 0.045f, 0.22f, 8, pants);
                k.Sphere(new Vector3(sx, 0.05f, 0.21f), new Vector3(0.065f, 0.045f, 0.09f), 8, 4, shoe);
            }
            // 몸통 + 앞치마 + 주머니
            k.Sphere(new Vector3(0f, 0.45f, 0f), new Vector3(0.165f, 0.17f, 0.15f), 12, 6, shirt);
            k.Sphere(new Vector3(0f, 0.43f, 0.095f), new Vector3(0.13f, 0.13f, 0.07f), 10, 4, apron);
            k.Box(new Vector3(0f, 0.38f, 0.165f), new Vector3(0.08f, 0.04f, 0.01f), Color.white, Color.white);
            // 팔 + 펼친 만화책
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.14f, 0.5f, 0.05f), Quaternion.Euler(-65f, 0f, side * 15f));
                k.Sphere(new Vector3(0f, -0.06f, 0f), new Vector3(0.048f, 0.09f, 0.048f), 8, 4, shirt);
                k.Sphere(new Vector3(0f, -0.15f, 0f), Vector3.one * 0.042f, 8, 4, skin);
                k.Pop();
            }
            k.Push(new Vector3(0f, 0.5f, 0.26f), Quaternion.Euler(-35f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.06f, 0f, 0f), Quaternion.Euler(0f, side * -15f, 0f));
                k.Box(Vector3.zero, new Vector3(0.12f, 0.15f, 0.012f), new Color(0.98f, 0.96f, 0.9f), new Color(0.9f, 0.3f, 0.3f));
                k.Pop();
            }
            k.Pop();

            // 머리 + 머리카락
            var head = new Vector3(0f, 0.78f, 0f);
            var hr = new Vector3(0.24f, 0.22f, 0.22f);
            Head(k, skin, head, hr, 1);
            k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.252f, 0.22f, 0.24f), 14, 7, hair);
            k.Sphere(head + new Vector3(0f, 0.15f, 0.1f), new Vector3(0.22f, 0.075f, 0.12f), 12, 4, hair); // 앞머리
            if (girl)
            {
                // 옆머리 + 뒤로 묶은 머리 + 리본
                foreach (float sx in new[] { -1f, 1f })
                    k.Sphere(head + new Vector3(sx * 0.2f, -0.12f, -0.02f), new Vector3(0.07f, 0.14f, 0.08f), 8, 5, hair);
                k.Sphere(head + new Vector3(0f, 0.02f, -0.27f), new Vector3(0.08f, 0.13f, 0.08f), 8, 5, hair);
                k.Sphere(head + new Vector3(0f, 0.13f, -0.24f), new Vector3(0.09f, 0.05f, 0.05f), 8, 4, ribbon);
            }
            else
            {
                // 삐죽 솟은 머리카락
                for (int i = 0; i < 3; i++)
                    k.Sphere(head + new Vector3(-0.06f + i * 0.06f, 0.22f, 0.02f), new Vector3(0.04f, 0.06f, 0.04f), 6, 4, hair);
            }
            k.SmoothSpheres = prevSmooth;
            k.SmoothUpBias = prevBias;
        }

        // ───────────── 주인 할머니 (가게 장식) ─────────────

        /// <summary>의자에 앉은 2등신 문방구 할머니. 원점 = 바닥, 앞 = +Z. 뜨개질 중.</summary>
        public static void BuildOwner(MeshKit k)
        {
            Color skin = new Color(1.0f, 0.86f, 0.76f);
            Color hair = new Color(0.88f, 0.87f, 0.90f);
            Color cardigan = new Color(0.62f, 0.82f, 0.66f);
            Color skirt = new Color(0.72f, 0.56f, 0.82f);
            Color apron = new Color(1.0f, 0.88f, 0.72f);
            Color shoe = new Color(0.55f, 0.42f, 0.40f);
            Color yarn = new Color(1.0f, 0.55f, 0.62f);
            bool prevSmooth = k.SmoothSpheres;
            float prevBias = k.SmoothUpBias;
            k.SmoothUpBias = 0.55f;
            k.SmoothSpheres = true;

            // 다리 (앉은 자세, 짧게)
            foreach (float sx in new[] { -0.075f, 0.075f })
            {
                k.Push(new Vector3(sx, 0.31f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.06f, 0.16f, 8, skirt);
                k.Pop();
                k.Cylinder(new Vector3(sx, 0.18f, 0.18f), 0.045f, 0.22f, 8, skin);
                k.Sphere(new Vector3(sx, 0.05f, 0.21f), new Vector3(0.065f, 0.045f, 0.09f), 8, 4, shoe);
            }
            // 몸통 (동글) + 앞치마
            k.Sphere(new Vector3(0f, 0.45f, 0f), new Vector3(0.18f, 0.17f, 0.16f), 12, 6, cardigan);
            k.Sphere(new Vector3(0f, 0.42f, 0.11f), new Vector3(0.12f, 0.11f, 0.06f), 10, 4, apron);
            Heart(k, new Vector3(0f, 0.42f, 0.168f), 0.03f, yarn);
            // 팔 + 뜨개질 (실뭉치는 무릎 위)
            foreach (float side in new[] { -1f, 1f })
            {
                k.Push(new Vector3(side * 0.15f, 0.5f, 0.05f), Quaternion.Euler(-60f, 0f, side * 18f));
                k.Sphere(new Vector3(0f, -0.06f, 0f), new Vector3(0.05f, 0.09f, 0.05f), 8, 4, cardigan);
                k.Sphere(new Vector3(0f, -0.15f, 0f), Vector3.one * 0.045f, 8, 4, skin);
                k.Pop();
            }
            k.Sphere(new Vector3(0f, 0.36f, 0.24f), Vector3.one * 0.07f, 10, 5, yarn);
            foreach (float sx in new[] { -0.05f, 0.05f })
            {
                k.Push(new Vector3(sx, 0.47f, 0.24f), Quaternion.Euler(0f, 0f, sx > 0 ? -35f : 35f));
                k.Cylinder(Vector3.zero, 0.008f, 0.2f, 4, new Color(0.85f, 0.7f, 0.45f));
                k.Pop();
            }
            k.Box(new Vector3(0f, 0.43f, 0.25f), new Vector3(0.1f, 0.06f, 0.02f), yarn, yarn); // 뜨던 목도리

            // 머리 (크게) + 쪽진 은발 + 동그란 안경
            var head = new Vector3(0f, 0.78f, 0f);
            var hr = new Vector3(0.24f, 0.22f, 0.22f);
            Head(k, skin, head, hr, 1);
            k.Sphere(head + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.252f, 0.22f, 0.24f), 14, 7, hair);
            k.Sphere(head + new Vector3(0f, 0.15f, 0.1f), new Vector3(0.22f, 0.07f, 0.12f), 12, 4, hair);
            k.Sphere(head + new Vector3(0f, 0.2f, -0.15f), Vector3.one * 0.095f, 10, 5, hair); // 쪽
            k.Cylinder(head + new Vector3(0.05f, 0.26f, -0.15f), 0.008f, 0.18f, 4, new Color(0.9f, 0.3f, 0.35f)); // 비녀
            foreach (float sx in new[] { -1f, 1f })
            {
                var d = new Vector3(sx * 0.36f, -0.1f, 0.93f).normalized;
                var p = head + Vector3.Scale(d, hr) + d * 0.02f;
                k.Push(p, Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f));
                k.Frustum(Vector3.zero, 0.062f, 0.062f, 0.012f, 12, new Color(0.75f, 0.55f, 0.35f), new Color(0.9f, 0.95f, 1f), false, false);
                k.Pop();
            }
            k.SmoothSpheres = prevSmooth;
            k.SmoothUpBias = prevBias;
        }
    }
}
