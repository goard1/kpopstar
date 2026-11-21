using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 가게의 움직이지 않는 장식 전부 (바닥·벽·간판·차양·벽 상품·창문·계산대·주인·문·바깥 보도).
    /// 전부 MeshKit 하나(버텍스 컬러)에 쌓아서 드로우콜 1~2개로 그린다.
    /// 좌표: 바닥 8x8 (x,z ∈ -4..4), 뒷벽 +Z / +X, 앞쪽(-Z)이 입구, 왼쪽 앞이 계산대.
    /// </summary>
    public static class ShopDecor
    {
        // ───────────── 팔레트 ─────────────
        public static Color FloorA = new Color(0.96f, 0.90f, 0.77f);
        public static Color FloorB = new Color(0.80f, 0.50f, 0.42f);
        public static Color WallUpper = new Color(0.98f, 0.93f, 0.81f);
        public static readonly Color WallTop = new Color(0.50f, 0.34f, 0.27f);
        public static Color Wainscot = new Color(0.56f, 0.78f, 0.70f);
        public static Color WainscotLine = new Color(0.46f, 0.67f, 0.60f);
        public static readonly Color WoodDark = new Color(0.47f, 0.30f, 0.21f);
        public static readonly Color Wood = new Color(0.70f, 0.48f, 0.31f);
        public static readonly Color WoodLight = new Color(0.86f, 0.67f, 0.45f);
        public static readonly Color Paper = new Color(0.99f, 0.97f, 0.91f);
        public static readonly Color Red = new Color(0.89f, 0.27f, 0.24f);
        public static readonly Color Blue = new Color(0.20f, 0.40f, 0.76f);
        public static readonly Color Yellow = new Color(0.99f, 0.82f, 0.28f);
        public static readonly Color Green = new Color(0.36f, 0.68f, 0.40f);
        public static readonly Color Pink = new Color(0.97f, 0.58f, 0.66f);
        public static readonly Color Sky = new Color(0.62f, 0.84f, 0.95f);
        public static readonly Color Metal = new Color(0.78f, 0.80f, 0.83f);
        public static readonly Color Ink = new Color(0.22f, 0.17f, 0.16f);
        public static readonly Color PlinthSide = new Color(0.52f, 0.38f, 0.30f);
        public static readonly Color PlinthLip = new Color(0.66f, 0.50f, 0.38f);
        public static readonly Color Pave = new Color(0.80f, 0.76f, 0.70f);
        public static readonly Color PaveB = new Color(0.72f, 0.67f, 0.62f);

        static readonly Color[] Candy =
        {
            new Color(0.95f, 0.40f, 0.45f), new Color(0.99f, 0.80f, 0.30f), new Color(0.45f, 0.75f, 0.95f),
            new Color(0.55f, 0.85f, 0.50f), new Color(0.80f, 0.55f, 0.90f), new Color(0.98f, 0.60f, 0.30f),
        };

        /// <summary>차양 줄무늬 색 (동네마다 다름).</summary>
        public static Color AwningColor = new Color(0.89f, 0.27f, 0.24f);

        /// <summary>동네(상권) 색으로 바꾼다. 바꾼 뒤 ShopBuilder.RebuildStatic으로 다시 그려야 보인다.</summary>
        public static void ApplyPalette(Munbanggu.Core.DistrictPalette p)
        {
            if (p == null) return;
            FloorA = p.floorA; FloorB = p.floorB; WallUpper = p.wallUpper;
            Wainscot = p.wainscot; WainscotLine = p.wainscotLine; AwningColor = p.awning;
        }

        public const float WallH = 3.0f;

        /// <summary>장식 전부 쌓기. kit = 불투명 버텍스 컬러, glass = 유리(곱하기), blob = 가짜 그림자.</summary>
        public static void Build(MeshKit kit, MeshKit glass, MeshKit blob)
        {
            DropShadow(blob);
            Plinth(kit);
            Floor(kit);
            Walls(kit);
            Sign(kit);
            Awnings(kit);
            BackWallGoods(kit);
            SideWallGoods(kit, glass);
            CornerProps(kit, blob);
            Counter(kit, glass, blob);
            Owner(kit, blob);
            Door(kit, glass);
            Outside(kit, blob);
        }

        // ───────────── 바닥·받침 ─────────────

        /// <summary>디오라마 전체 아래 부드러운 그림자 (배경 위에 떠 있는 느낌).</summary>
        static void DropShadow(MeshKit blob)
        {
            blob.FloorQuad(new Vector3(0.4f, -0.45f, -1.3f), new Vector2(14f, 16f), Color.white);
        }

        static void Plinth(MeshKit k)
        {
            // 디오라마 받침 (앞·왼쪽 단면이 보임)
            k.Box(new Vector3(0f, -0.2f, -0.9f), new Vector3(8.4f, 0.4f, 10.2f), PlinthLip, PlinthSide);
            k.FlatBox(new Vector3(0f, -0.03f, -0.9f), new Vector3(8.42f, 0.06f, 10.22f), PlinthLip);
        }

        static void Floor(MeshKit k)
        {
            const float t = 0.5f;
            for (int x = 0; x < 16; x++)
                for (int z = 0; z < 16; z++)
                {
                    var c = (x + z) % 2 == 0 ? FloorA : FloorB;
                    k.FloorQuad(new Vector3(-4f + t * 0.5f + x * t, 0.001f, -4f + t * 0.5f + z * t), new Vector2(t, t), c);
                }
            // 문 앞 매트
            k.FlatBox(new Vector3(1.4f, 0.006f, -3.55f), new Vector3(1.3f, 0.012f, 0.75f), new Color(0.55f, 0.30f, 0.24f));
            for (int i = 0; i < 4; i++)
                k.FlatBox(new Vector3(1.4f, 0.013f, -3.82f + i * 0.18f), new Vector3(1.2f, 0.004f, 0.05f), new Color(0.68f, 0.40f, 0.30f));
        }

        // ───────────── 벽 ─────────────

        /// <summary>벽 좌표계: 원점=벽 안쪽 면, 로컬 x=방 안에서 볼 때 오른쪽, 로컬 -Z=방 쪽.</summary>
        static void PushWallZ(MeshKit k) => k.Push(new Vector3(0f, 0f, 4.0f), Quaternion.identity);
        static void PushWallX(MeshKit k) => k.Push(new Vector3(4.0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

        static void Walls(MeshKit k)
        {
            // +Z 벽 (x -4.2..4.2), +X 벽 (z -4..4.2)
            k.Box(new Vector3(0f, WallH * 0.5f, 4.1f), new Vector3(8.4f, WallH, 0.2f), WallTop, WallUpper);
            k.Box(new Vector3(4.1f, WallH * 0.5f, 0.1f), new Vector3(0.2f, WallH, 8.2f), WallTop, WallUpper);

            PushWallZ(k); WallTrim(k, -4f, 4f); k.Pop();
            PushWallX(k); WallTrim(k, -4f, 4f); k.Pop();
        }

        public static void WallTrim(MeshKit k, float x0, float x1)
        {
            float w = x1 - x0, cx = (x0 + x1) * 0.5f;
            k.Box(new Vector3(cx, 0.5f, -0.015f), new Vector3(w, 1.0f, 0.03f), Wainscot);
            for (float x = x0 + 0.25f; x < x1; x += 0.25f)
                k.FlatBox(new Vector3(x, 0.5f, -0.032f), new Vector3(0.025f, 0.8f, 0.006f), WainscotLine);
            k.Box(new Vector3(cx, 1.02f, -0.035f), new Vector3(w, 0.05f, 0.07f), Wood, Wood);       // 몰딩
            k.Box(new Vector3(cx, 0.07f, -0.04f), new Vector3(w, 0.14f, 0.08f), WoodDark, WoodDark); // 걸레받이
            k.Box(new Vector3(cx, WallH - 0.04f, -0.03f), new Vector3(w, 0.08f, 0.06f), Wood, Wood); // 천장 몰딩
        }

        // ───────────── 간판 (+Z 벽 위) ─────────────

        static void Sign(MeshKit k)
        {
            const float cx = -0.4f, y0 = 3.02f, h = 0.92f, w = 5.6f;
            float cy = y0 + h * 0.5f;
            // 판 + 테두리
            k.Box(new Vector3(cx, cy, 4.08f), new Vector3(w + 0.16f, h + 0.16f, 0.12f), Red, Red);
            k.Box(new Vector3(cx, cy, 4.03f), new Vector3(w, h, 0.06f), Blue, Blue);
            // 위아래 노란 띠
            k.FlatBox(new Vector3(cx, y0 + h - 0.07f, 3.995f), new Vector3(w, 0.05f, 0.012f), Yellow);
            k.FlatBox(new Vector3(cx, y0 + 0.07f, 3.995f), new Vector3(w, 0.05f, 0.012f), Yellow);

            // 문 방 구 (블록 글자)
            const float g = 0.62f, gap = 0.78f;
            float gx = cx - gap - g * 0.5f, gy = cy - g * 0.5f;
            Glyph(k, Mun, gx, gy, g);
            Glyph(k, Bang, gx + gap, gy, g);
            Ring(k, gx + gap + 0.5f * g, gy + 0.17f * g, 0.17f * g, 0.085f * g);
            Glyph(k, Gu, gx + gap * 2f, gy, g);

            // 양옆 장식: 별 동그라미
            foreach (float sx in new[] { cx - 2.25f, cx + 2.25f })
            {
                k.Push(new Vector3(sx, cy, 3.98f), Quaternion.Euler(-90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.27f, 0.04f, 12, Yellow, Yellow);
                k.Cylinder(new Vector3(0f, 0.02f, 0f), 0.2f, 0.04f, 12, Red, Red);
                k.Pop();
                Star(k, new Vector3(sx, cy, 3.93f), 0.15f, Paper);
            }
            // 받침 기둥
            k.Box(new Vector3(cx - 2.3f, 3.01f, 4.1f), new Vector3(0.1f, 0.06f, 0.14f), WoodDark);
            k.Box(new Vector3(cx + 2.3f, 3.01f, 4.1f), new Vector3(0.1f, 0.06f, 0.14f), WoodDark);
        }

        // 글자 획: (x0, y0, x1, y1) 0~1
        static readonly Vector4[] Mun =
        {
            new Vector4(0.18f, 0.62f, 0.82f, 0.71f), new Vector4(0.18f, 0.87f, 0.82f, 0.96f),
            new Vector4(0.18f, 0.62f, 0.29f, 0.96f), new Vector4(0.71f, 0.62f, 0.82f, 0.96f),
            new Vector4(0.02f, 0.45f, 0.98f, 0.54f), new Vector4(0.45f, 0.30f, 0.55f, 0.45f),
            new Vector4(0.18f, 0.00f, 0.29f, 0.24f), new Vector4(0.18f, 0.00f, 0.86f, 0.09f),
        };
        static readonly Vector4[] Bang =
        {
            new Vector4(0.04f, 0.42f, 0.15f, 0.98f), new Vector4(0.45f, 0.42f, 0.56f, 0.98f),
            new Vector4(0.04f, 0.65f, 0.56f, 0.73f), new Vector4(0.04f, 0.42f, 0.56f, 0.51f),
            new Vector4(0.68f, 0.38f, 0.79f, 1.00f), new Vector4(0.79f, 0.65f, 0.95f, 0.74f),
        };
        static readonly Vector4[] Gu =
        {
            new Vector4(0.12f, 0.87f, 0.86f, 0.96f), new Vector4(0.75f, 0.58f, 0.86f, 0.96f),
            new Vector4(0.02f, 0.40f, 0.98f, 0.49f), new Vector4(0.45f, 0.02f, 0.55f, 0.40f),
        };

        static void Glyph(MeshKit k, Vector4[] strokes, float x, float y, float size)
        {
            foreach (var s in strokes)
            {
                var c = new Vector3(x + (s.x + s.z) * 0.5f * size, y + (s.y + s.w) * 0.5f * size, 3.975f);
                k.Box(c, new Vector3((s.z - s.x) * size, (s.w - s.y) * size, 0.05f), Paper, Paper);
            }
        }

        static void Ring(MeshKit k, float x, float y, float r, float inner)
        {
            k.Push(new Vector3(x, y, 3.975f), Quaternion.Euler(-90f, 0f, 0f));
            k.Cylinder(Vector3.zero, r, 0.05f, 14, Paper, Paper);
            k.Pop();
            k.Push(new Vector3(x, y, 3.948f), Quaternion.Euler(-90f, 0f, 0f));
            k.Frustum(Vector3.zero, inner, inner, 0.004f, 14, Blue, Blue, false, true);
            k.Pop();
        }

        /// <summary>앞(-Z)을 보는 납작한 별.</summary>
        static void Star(MeshKit k, Vector3 c, float r, Color col)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 5f;
                float a0 = a - Mathf.PI / 5f, a1 = a + Mathf.PI / 5f;
                Vector3 tip = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                Vector3 l = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * r * 0.42f;
                Vector3 rr = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * r * 0.42f;
                k.Triangle(c, l, tip, col);
                k.Triangle(c, tip, rr, col);
            }
        }

        // ───────────── 줄무늬 차양 (두 벽 위쪽, 안쪽으로) ─────────────

        static void Awnings(MeshKit k)
        {
            PushWallZ(k); Awning(k, -4.0f, 4.0f, 0); k.Pop();
            PushWallX(k); Awning(k, -3.45f, 4.0f, 1); k.Pop();
        }

        public static void Awning(MeshKit k, float x0, float x1, int phase)
        {
            const float stripe = 0.32f, depth = 0.62f, slope = 30f;
            const float topY = 2.92f;
            float rad = slope * Mathf.Deg2Rad;
            float frontZ = -Mathf.Cos(rad) * depth, frontY = topY - Mathf.Sin(rad) * depth;
            int n = Mathf.CeilToInt((x1 - x0) / stripe);
            for (int i = 0; i < n; i++)
            {
                float a = x0 + i * stripe, b = Mathf.Min(x1, a + stripe);
                float cx = (a + b) * 0.5f, w = b - a;
                Color c = (i + phase) % 2 == 0 ? AwningColor : Paper;
                k.Push(new Vector3(cx, (topY + frontY) * 0.5f, frontZ * 0.5f), Quaternion.Euler(-slope, 0f, 0f));
                k.Box(Vector3.zero, new Vector3(w, 0.025f, depth), c, c);
                k.Pop();
                // 앞 드림 + 물결
                k.Box(new Vector3(cx, frontY - 0.06f, frontZ), new Vector3(w, 0.12f, 0.02f), c, c);
                k.Push(new Vector3(cx, frontY - 0.12f, frontZ), Quaternion.Euler(90f, 0f, 0f));
                k.Frustum(Vector3.zero, w * 0.5f, w * 0.5f, 0.02f, 10, c, c);
                k.Pop();
            }
            // 차양 받침 쇠막대
            k.Push(new Vector3((x0 + x1) * 0.5f, frontY + 0.01f, frontZ + 0.01f), Quaternion.Euler(0f, 0f, 90f));
            k.Cylinder(Vector3.zero, 0.02f, x1 - x0, 6, Metal);
            k.Pop();
        }

        // ───────────── +Z 벽: 뽑기판, 딱지 걸이, 선반 ─────────────

        static void BackWallGoods(MeshKit k)
        {
            PushWallZ(k);
            k.Push(new Vector3(0f, 0.2f, 0f)); // 벽이 높아진 만큼 벽 상품도 위로

            // 뽑기판 (슬롯 0~1 위)
            {
                const float cx = -2.25f, cy = 1.52f, w = 1.15f, h = 0.66f;
                k.Box(new Vector3(cx, cy, -0.03f), new Vector3(w + 0.08f, h + 0.08f, 0.04f), Red, Red);
                k.Box(new Vector3(cx, cy, -0.05f), new Vector3(w, h, 0.02f), Paper, Paper);
                k.FlatBox(new Vector3(cx, cy + h * 0.5f - 0.08f, -0.062f), new Vector3(w, 0.1f, 0.006f), Yellow);
                for (int x = 0; x < 10; x++)
                    for (int y = 0; y < 3; y++)
                    {
                        if ((x * 7 + y * 3) % 9 == 0) continue; // 뜯긴 칸
                        Color c = Candy[(x + y * 2) % Candy.Length];
                        k.FlatBox(new Vector3(cx - w * 0.5f + 0.08f + x * 0.11f, cy - 0.22f + y * 0.11f, -0.063f),
                                  new Vector3(0.08f, 0.08f, 0.006f), c);
                    }
                // 1등 상품 상자들
                for (int i = 0; i < 4; i++)
                {
                    Color c = Candy[(i * 2 + 1) % Candy.Length];
                    k.Box(new Vector3(cx - 0.42f + i * 0.28f, cy + 0.13f, -0.1f), new Vector3(0.2f, 0.13f, 0.08f), MeshKit.Shade(c, 1.05f), c);
                }
            }

            // 딱지 걸이 (슬롯 2 위): 막대에 줄줄이
            {
                const float cx = -0.3f;
                k.Push(new Vector3(cx, 1.94f, -0.08f), Quaternion.Euler(0f, 0f, 90f));
                k.Cylinder(Vector3.zero, 0.02f, 1.1f, 6, Metal);
                k.Pop();
                for (int s = 0; s < 4; s++)
                {
                    float sx = cx - 0.42f + s * 0.28f;
                    k.FlatBox(new Vector3(sx, 1.6f, -0.075f), new Vector3(0.02f, 0.66f, 0.008f), new Color(0.85f, 0.85f, 0.88f));
                    for (int i = 0; i < 4; i++)
                    {
                        Color c = Candy[(s * 3 + i) % Candy.Length];
                        k.Push(new Vector3(sx, 1.82f - i * 0.16f, -0.09f), Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 4f : -4f)));
                        k.Box(Vector3.zero, new Vector3(0.2f, 0.14f, 0.015f), c, c);
                        k.FlatBox(new Vector3(0f, 0.02f, -0.009f), new Vector3(0.11f, 0.06f, 0.004f), Paper);
                        k.Pop();
                    }
                }
            }

            // 벽 선반 (슬롯 3~4 위): 사탕 병, 장난감 상자
            {
                const float x0 = 0.5f, x1 = 2.85f, y = 1.32f;
                k.Box(new Vector3((x0 + x1) * 0.5f, y, -0.13f), new Vector3(x1 - x0, 0.05f, 0.26f), WoodLight, Wood);
                foreach (float bx in new[] { x0 + 0.2f, x1 - 0.2f })
                    k.Box(new Vector3(bx, y - 0.1f, -0.06f), new Vector3(0.04f, 0.16f, 0.12f), WoodDark);
                // 사탕 병 3개
                for (int i = 0; i < 3; i++)
                {
                    float jx = x0 + 0.25f + i * 0.3f;
                    Color c = Candy[i * 2 % Candy.Length];
                    k.Cylinder(new Vector3(jx, y + 0.13f, -0.13f), 0.1f, 0.2f, 8, new Color(0.88f, 0.96f, 0.98f));
                    k.Cylinder(new Vector3(jx, y + 0.1f, -0.13f), 0.095f, 0.13f, 8, c);
                    k.Cylinder(new Vector3(jx, y + 0.25f, -0.13f), 0.07f, 0.04f, 8, Red, Red);
                }
                // 장난감 상자
                k.Box(new Vector3(x0 + 1.25f, y + 0.16f, -0.13f), new Vector3(0.32f, 0.27f, 0.18f), Blue, Blue);
                k.FlatBox(new Vector3(x0 + 1.25f, y + 0.16f, -0.222f), new Vector3(0.2f, 0.15f, 0.004f), Yellow);
                k.Box(new Vector3(x0 + 1.62f, y + 0.12f, -0.13f), new Vector3(0.26f, 0.19f, 0.18f), Pink, Pink);
                k.FlatBox(new Vector3(x0 + 1.62f, y + 0.12f, -0.222f), new Vector3(0.14f, 0.1f, 0.004f), Paper);
                k.Box(new Vector3(x0 + 1.98f, y + 0.2f, -0.12f), new Vector3(0.24f, 0.35f, 0.16f), Green, Green);
                k.FlatBox(new Vector3(x0 + 1.98f, y + 0.24f, -0.202f), new Vector3(0.14f, 0.14f, 0.004f), Paper);
                // 위쪽 줄줄이 과자
                SnackStrings(k, x0 + 0.1f, x1 - 0.1f, 1.95f, 5);
            }

            k.Pop(); // 높이 올림
            k.Pop(); // 벽 좌표계
        }

        /// <summary>줄줄이 걸린 과자 봉지 (벽 좌표계).</summary>
        static void SnackStrings(MeshKit k, float x0, float x1, float topY, int strings)
        {
            for (int s = 0; s < strings; s++)
            {
                float sx = Mathf.Lerp(x0, x1, strings == 1 ? 0.5f : s / (float)(strings - 1));
                k.FlatBox(new Vector3(sx, topY - 0.15f, -0.05f), new Vector3(0.015f, 0.3f, 0.006f), Red);
                for (int i = 0; i < 2; i++)
                {
                    Color c = Candy[(s + i * 3) % Candy.Length];
                    k.Box(new Vector3(sx, topY - 0.08f - i * 0.15f, -0.07f), new Vector3(0.14f, 0.12f, 0.025f), c, c);
                }
            }
        }

        // ───────────── +X 벽: 창문, 시계, 달력, 줄줄이 ─────────────

        static void SideWallGoods(MeshKit k, MeshKit glass)
        {
            PushWallX(k);
            k.Push(new Vector3(0f, 0.15f, 0f));
            glass.Push(new Vector3(4.0f, 0.15f, 0f), Quaternion.Euler(0f, 90f, 0f));

            // 창문 (로컬 x 1.75..3.35 = 월드 z -1.75..-3.35)
            {
                const float x0 = 1.75f, x1 = 3.35f, y0 = 1.0f, y1 = 2.05f;
                float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, w = x1 - x0, h = y1 - y0;
                // 바깥 풍경 (하늘, 먼 건물, 나무)
                k.FlatBox(new Vector3(cx, cy, -0.004f), new Vector3(w, h, 0.004f), Sky);
                k.FlatBox(new Vector3(cx, y1 - 0.15f, -0.007f), new Vector3(w, 0.25f, 0.004f), new Color(0.74f, 0.90f, 0.98f));
                k.FlatBox(new Vector3(x0 + 0.35f, y0 + 0.25f, -0.008f), new Vector3(0.5f, 0.5f, 0.004f), new Color(0.93f, 0.80f, 0.66f));
                k.FlatBox(new Vector3(x0 + 0.35f, y0 + 0.36f, -0.010f), new Vector3(0.08f, 0.1f, 0.004f), new Color(0.6f, 0.75f, 0.85f));
                k.FlatBox(new Vector3(x0 + 0.95f, y0 + 0.17f, -0.008f), new Vector3(0.6f, 0.34f, 0.004f), new Color(0.85f, 0.68f, 0.60f));
                k.Sphere(new Vector3(x1 - 0.3f, y0 + 0.32f, -0.004f), new Vector3(0.28f, 0.26f, 0.004f), 8, 4, Green);
                k.FlatBox(new Vector3(x1 - 0.3f, y0 + 0.08f, -0.01f), new Vector3(0.06f, 0.18f, 0.004f), WoodDark);
                // 틀
                Color frame = new Color(0.95f, 0.95f, 0.93f);
                k.Box(new Vector3(cx, y0 - 0.03f, -0.07f), new Vector3(w + 0.16f, 0.06f, 0.14f), WoodLight, Wood); // 창턱
                k.Box(new Vector3(cx, y1 + 0.03f, -0.03f), new Vector3(w + 0.1f, 0.06f, 0.06f), frame);
                k.Box(new Vector3(x0 - 0.02f, cy, -0.03f), new Vector3(0.06f, h + 0.1f, 0.06f), frame);
                k.Box(new Vector3(x1 + 0.02f, cy, -0.03f), new Vector3(0.06f, h + 0.1f, 0.06f), frame);
                k.Box(new Vector3(cx, cy, -0.03f), new Vector3(0.04f, h, 0.05f), frame);
                k.Box(new Vector3(cx, cy + 0.12f, -0.03f), new Vector3(w, 0.03f, 0.04f), frame);
                // 유리 + 반사 줄
                glass.WallQuad(new Vector3(cx, cy, -0.02f), new Vector2(w, h), Color.white);
                k.Push(new Vector3(x0 + 0.3f, cy + 0.1f, -0.022f), Quaternion.Euler(0f, 0f, -40f));
                k.FlatBox(Vector3.zero, new Vector3(0.05f, 0.45f, 0.003f), new Color(0.92f, 0.98f, 1f));
                k.Pop();
                k.Push(new Vector3(x0 + 1.1f, cy + 0.05f, -0.022f), Quaternion.Euler(0f, 0f, -40f));
                k.FlatBox(Vector3.zero, new Vector3(0.03f, 0.35f, 0.003f), new Color(0.92f, 0.98f, 1f));
                k.Pop();
                // 창턱 화분
                k.Frustum(new Vector3(x0 + 0.25f, y0 + 0.08f, -0.08f), 0.06f, 0.08f, 0.12f, 8, new Color(0.80f, 0.45f, 0.30f), WoodDark);
                k.Sphere(new Vector3(x0 + 0.25f, y0 + 0.2f, -0.08f), new Vector3(0.1f, 0.09f, 0.1f), 7, 4, Green);
            }

            // 시계 (슬롯 7 위쪽)
            {
                var c = new Vector3(0.7f, 1.78f, -0.03f);
                k.Push(c, Quaternion.Euler(-90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.17f, 0.05f, 14, Red, Paper);
                k.Pop();
                k.Push(c + new Vector3(0f, 0f, -0.03f), Quaternion.Euler(0f, 0f, 0f));
                k.FlatBox(new Vector3(0f, 0.05f, 0f), new Vector3(0.022f, 0.1f, 0.005f), Ink);
                k.Pop();
                k.Push(c + new Vector3(0f, 0f, -0.032f), Quaternion.Euler(0f, 0f, -60f));
                k.FlatBox(new Vector3(0f, 0.035f, 0f), new Vector3(0.018f, 0.07f, 0.005f), Ink);
                k.Pop();
            }

            // 달력 (구석 쪽)
            {
                var c = new Vector3(-3.3f, 1.72f, -0.01f);
                k.Box(c, new Vector3(0.42f, 0.55f, 0.012f), Paper, Paper);
                k.FlatBox(c + new Vector3(0f, 0.17f, -0.008f), new Vector3(0.42f, 0.16f, 0.004f), Pink);
                for (int r = 0; r < 4; r++)
                    for (int q = 0; q < 5; q++)
                        k.FlatBox(c + new Vector3(-0.15f + q * 0.075f, 0.03f - r * 0.07f, -0.008f), new Vector3(0.04f, 0.035f, 0.004f),
                                  (r == 2 && q == 3) ? Red : new Color(0.55f, 0.5f, 0.5f));
            }

            // 슬롯 5~6 위 줄줄이 사탕
            SnackStrings(k, -2.3f, -0.3f, 1.95f, 6);

            glass.Pop();
            k.Pop(); // 높이 올림
            k.Pop(); // 벽 좌표계
        }

        // ───────────── 구석 소품 ─────────────

        static void CornerProps(MeshKit k, MeshKit blob)
        {
            // 오른쪽 뒤 구석: 캡슐 뽑기 기계
            GachaMachine(k, new Vector3(3.45f, 0f, 3.35f), 225f, Red, Yellow);
            Shadow(blob, new Vector3(3.45f, 0f, 3.35f), new Vector2(0.9f, 0.9f), 0f);

            // 왼쪽 뒤 구석: 재고 상자 더미
            Crate(k, new Vector3(-3.7f, 0f, 3.5f), new Vector3(0.5f, 0.4f, 0.5f), 8f);
            Crate(k, new Vector3(-3.72f, 0.4f, 3.52f), new Vector3(0.44f, 0.34f, 0.44f), -6f);
            Crate(k, new Vector3(-3.7f, 0f, 2.95f), new Vector3(0.45f, 0.32f, 0.45f), -15f);
            Shadow(blob, new Vector3(-3.7f, 0f, 3.25f), new Vector2(0.9f, 1.2f), 0f);
        }

        public static void Crate(MeshKit k, Vector3 basePos, Vector3 size, float yaw)
        {
            Color card = new Color(0.82f, 0.64f, 0.44f);
            k.Push(basePos, Quaternion.Euler(0f, yaw, 0f));
            k.Box(new Vector3(0f, size.y * 0.5f, 0f), size, MeshKit.Shade(card, 1.06f), card);
            k.FlatBox(new Vector3(0f, size.y + 0.001f, 0f), new Vector3(size.x + 0.002f, 0.004f, 0.07f), new Color(0.72f, 0.60f, 0.45f));
            k.FlatBox(new Vector3(0f, size.y * 0.55f, -size.z * 0.5f - 0.002f), new Vector3(size.x * 0.5f, size.y * 0.3f, 0.004f), Paper);
            k.Pop();
        }

        /// <summary>캡슐 뽑기 기계. 앞면이 +Z (yaw로 돌림).</summary>
        public static void GachaMachine(MeshKit k, Vector3 basePos, float yaw, Color body, Color accent)
        {
            k.Push(basePos, Quaternion.Euler(0f, yaw, 0f));
            k.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.45f), body, body);
            k.Box(new Vector3(0f, 0.62f, 0f), new Vector3(0.54f, 0.04f, 0.49f), accent, accent);
            // 투명 돔 (캡슐 보이게)
            k.Sphere(new Vector3(0f, 0.86f, 0f), new Vector3(0.24f, 0.24f, 0.22f), 10, 6, new Color(0.86f, 0.95f, 0.98f));
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f;
                var p = new Vector3(Mathf.Cos(a) * 0.17f, 0.78f + (i % 3) * 0.06f, Mathf.Sin(a) * 0.15f + 0.06f);
                k.Sphere(p, Vector3.one * 0.065f, 6, 4, Candy[i % Candy.Length]);
            }
            k.Cylinder(new Vector3(0f, 1.11f, 0f), 0.08f, 0.04f, 8, body, body);
            // 손잡이·동전 투입구·배출구
            k.Push(new Vector3(0f, 0.42f, 0.24f), Quaternion.Euler(90f, 0f, 0f));
            k.Cylinder(Vector3.zero, 0.07f, 0.04f, 10, Metal, Metal);
            k.Pop();
            k.Box(new Vector3(0f, 0.42f, 0.27f), new Vector3(0.12f, 0.025f, 0.025f), Paper);
            k.Box(new Vector3(0f, 0.15f, 0.22f), new Vector3(0.18f, 0.12f, 0.04f), Ink, Ink);
            k.FlatBox(new Vector3(0.15f, 0.52f, 0.227f), new Vector3(0.1f, 0.06f, 0.004f), accent);
            k.Pop();
        }

        // ───────────── 계산대 + 주인 ─────────────

        static void Counter(MeshKit k, MeshKit glass, MeshKit blob)
        {
            const float x0 = -3.3f, x1 = -2.3f, z0 = -2.1f, z1 = -0.3f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
            Shadow(blob, new Vector3(cx, 0f, cz), new Vector2(1.5f, 2.4f), 0f);
            // 높이는 낮게(0.72): 카메라가 -X 쪽에서 보므로 계산대 너머(+X) 줄 선 아이가 가려지지 않게.
            // 줄 맨 앞(-1.9, -1.2)의 시선은 계산대의 z ≤ -1.6 구간을 지나므로 높은 물건은 z > -1.45 에만 둔다.
            const float top = 0.72f, glassY0 = 0.36f, glassY1 = 0.66f;
            float gH = glassY1 - glassY0, gC = (glassY0 + glassY1) * 0.5f;

            // 아래 나무장
            k.Box(new Vector3(cx, glassY0 * 0.5f, cz), new Vector3(w, glassY0, d), WoodLight, Wood);
            k.Box(new Vector3(cx, 0.06f, cz), new Vector3(w + 0.02f, 0.12f, d + 0.02f), WoodDark, WoodDark);
            k.FlatBox(new Vector3(x1 + 0.006f, 0.24f, cz), new Vector3(0.004f, 0.14f, d - 0.25f), WoodLight); // 앞판 무늬
            // 진열장 바닥 + 안의 과자
            k.FlatBox(new Vector3(cx, glassY0 + 0.005f, cz), new Vector3(w - 0.04f, 0.01f, d - 0.04f), Paper);
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 2; j++)
                {
                    Color c = Candy[(i + j * 3) % Candy.Length];
                    k.Box(new Vector3(cx - 0.22f + j * 0.42f, glassY0 + 0.06f, z0 + 0.22f + i * 0.34f), new Vector3(0.3f, 0.1f, 0.24f), MeshKit.Shade(c, 1.05f), c);
                }
            // 기둥 + 상판
            foreach (var p in new[] { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x0, z1), new Vector2(x1, z1) })
                k.Box(new Vector3(p.x + (p.x < cx ? 0.03f : -0.03f), gC, p.y + (p.y < cz ? 0.03f : -0.03f)), new Vector3(0.06f, gH, 0.06f), Wood);
            k.Box(new Vector3(cx, top - 0.03f, cz), new Vector3(w + 0.08f, 0.06f, d + 0.08f), WoodLight, WoodDark);
            // 유리 (앞·옆)
            glass.Push(new Vector3(x1 - 0.005f, gC, cz), Quaternion.Euler(0f, -90f, 0f));
            glass.WallQuad(Vector3.zero, new Vector2(d, gH), Color.white);
            glass.Pop();
            glass.WallQuad(new Vector3(cx, gC, z0 + 0.005f), new Vector2(w, gH), Color.white);

            // 금전등록기 (줄 맨 앞 손님 바로 앞)
            {
                var b = new Vector3(cx + 0.05f, top, -1.1f);
                Color beige = new Color(0.90f, 0.86f, 0.76f);
                k.Box(b + new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.16f, 0.4f), beige, MeshKit.Shade(beige, 0.95f));
                k.Push(b + new Vector3(0.08f, 0.17f, 0f), Quaternion.Euler(0f, 0f, 20f));
                k.Box(Vector3.zero, new Vector3(0.22f, 0.06f, 0.36f), new Color(0.35f, 0.33f, 0.34f), beige);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 4; j++)
                        k.Box(new Vector3(-0.06f + i * 0.06f, 0.035f, -0.12f + j * 0.08f), new Vector3(0.04f, 0.02f, 0.05f),
                              j == 3 ? Red : Paper, Paper);
                k.Pop();
                k.Box(b + new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.1f, 0.12f, 0.24f), beige, beige); // 표시창
                k.FlatBox(b + new Vector3(-0.065f, 0.25f, 0f), new Vector3(0.004f, 0.06f, 0.18f), new Color(0.4f, 0.75f, 0.5f));
            }
            // 사탕 병 2개 (안쪽 끝)
            for (int i = 0; i < 2; i++)
            {
                var b = new Vector3(cx - 0.2f + i * 0.3f, top, -0.5f - i * 0.08f);
                k.Cylinder(b + new Vector3(0f, 0.12f, 0f), 0.1f, 0.24f, 8, new Color(0.88f, 0.96f, 0.98f));
                k.Cylinder(b + new Vector3(0f, 0.09f, 0f), 0.095f, 0.16f, 8, Candy[i * 3 % Candy.Length]);
                k.Cylinder(b + new Vector3(0f, 0.26f, 0f), 0.08f, 0.04f, 8, i == 0 ? Blue : Red);
            }
            // 동전 접시·주판 (낮은 물건은 앞쪽에)
            k.Cylinder(new Vector3(cx + 0.15f, top + 0.01f, -1.65f), 0.11f, 0.02f, 10, new Color(0.40f, 0.55f, 0.85f));
            k.Cylinder(new Vector3(cx + 0.15f, top + 0.025f, -1.65f), 0.03f, 0.012f, 6, Yellow);
            k.Push(new Vector3(cx - 0.15f, top + 0.02f, -1.75f), Quaternion.Euler(0f, 20f, 0f));
            k.Box(Vector3.zero, new Vector3(0.18f, 0.03f, 0.36f), WoodDark, WoodDark);
            for (int i = 0; i < 4; i++)
                k.Box(new Vector3(0f, 0.025f, -0.12f + i * 0.08f), new Vector3(0.14f, 0.02f, 0.025f), Red, Red);
            k.Pop();
            // 작은 TV (할머니용, 안쪽)
            {
                var b = new Vector3(cx - 0.22f, top, -0.85f);
                k.Push(b, Quaternion.Euler(0f, 215f, 0f));
                k.Box(new Vector3(0f, 0.14f, 0f), new Vector3(0.3f, 0.26f, 0.26f), new Color(0.55f, 0.42f, 0.32f), new Color(0.45f, 0.33f, 0.25f));
                k.FlatBox(new Vector3(-0.03f, 0.15f, 0.132f), new Vector3(0.2f, 0.17f, 0.004f), new Color(0.55f, 0.75f, 0.85f));
                k.FlatBox(new Vector3(0.11f, 0.12f, 0.132f), new Vector3(0.04f, 0.04f, 0.004f), Ink);
                k.Push(new Vector3(0.05f, 0.27f, 0f), Quaternion.Euler(0f, 0f, -30f));
                k.Cylinder(new Vector3(0f, 0.08f, 0f), 0.006f, 0.16f, 4, Metal);
                k.Pop();
                k.Push(new Vector3(-0.05f, 0.27f, 0f), Quaternion.Euler(0f, 0f, 30f));
                k.Cylinder(new Vector3(0f, 0.08f, 0f), 0.006f, 0.16f, 4, Metal);
                k.Pop();
                k.Pop();
            }
        }

        static void Owner(MeshKit k, MeshKit blob)
        {
            // 계산대 옆 의자에 앉은 문방구 할머니 (손님 줄 쪽을 반쯤 보며)
            var pos = new Vector3(-3.72f, 0f, -1.25f);
            Shadow(blob, pos, new Vector2(0.75f, 0.75f), 0f);
            k.Push(pos, Quaternion.Euler(0f, 160f, 0f));
            // 의자
            k.Cylinder(new Vector3(0f, 0.27f, 0f), 0.2f, 0.05f, 8, Red, Red);
            foreach (var p in new[] { new Vector3(0.12f, 0.125f, 0.12f), new Vector3(-0.12f, 0.125f, 0.12f), new Vector3(0.12f, 0.125f, -0.12f), new Vector3(-0.12f, 0.125f, -0.12f) })
                k.Cylinder(p, 0.02f, 0.25f, 5, Metal);
            CustomerVisual.BuildOwner(k);
            k.Pop();
        }

        // ───────────── 입구 (미닫이 유리문) ─────────────

        static void Door(MeshKit k, MeshKit glass)
        {
            const float x0 = 0.6f, x1 = 2.6f, z = -4.0f, top = 2.3f;
            Color alu = Metal;
            k.Box(new Vector3((x0 + x1) * 0.5f, 0.012f, z), new Vector3(x1 - x0 + 0.1f, 0.024f, 0.14f), new Color(0.6f, 0.6f, 0.62f)); // 레일
            k.Box(new Vector3(x0, top * 0.5f, z), new Vector3(0.08f, top, 0.12f), alu);
            k.Box(new Vector3(x1, top * 0.5f, z), new Vector3(0.08f, top, 0.12f), alu);
            k.Box(new Vector3((x0 + x1) * 0.5f, top + 0.04f, z), new Vector3(x1 - x0 + 0.08f, 0.08f, 0.12f), alu);
            // 열린 미닫이 두 짝 (오른쪽에 겹쳐 있음)
            for (int i = 0; i < 2; i++)
            {
                float px0 = 1.95f - i * 0.12f, px1 = x1 - 0.04f;
                float pz = z + (i == 0 ? -0.035f : 0.035f);
                float pcx = (px0 + px1) * 0.5f, pw = px1 - px0;
                k.Box(new Vector3(px0, top * 0.5f, pz), new Vector3(0.05f, top - 0.04f, 0.04f), alu);
                k.Box(new Vector3(px1, top * 0.5f, pz), new Vector3(0.05f, top - 0.04f, 0.04f), alu);
                k.Box(new Vector3(pcx, 0.06f, pz), new Vector3(pw, 0.1f, 0.04f), alu);
                k.Box(new Vector3(pcx, top - 0.04f, pz), new Vector3(pw, 0.06f, 0.04f), alu);
                k.Box(new Vector3(px0 + 0.06f, 1.0f, pz - 0.03f), new Vector3(0.03f, 0.25f, 0.03f), new Color(0.45f, 0.45f, 0.48f)); // 손잡이
                glass.WallQuad(new Vector3(pcx, top * 0.5f, pz), new Vector2(pw, top - 0.1f), Color.white);
            }
            // 유리문 스티커 ("미세요" 대신 색 띠)
            k.FlatBox(new Vector3(2.28f, 1.35f, z - 0.058f), new Vector3(0.36f, 0.12f, 0.004f), Red);
            k.FlatBox(new Vector3(2.28f, 1.35f, z - 0.061f), new Vector3(0.24f, 0.04f, 0.004f), Paper);
            // 문 위 작은 간판
            k.Box(new Vector3((x0 + x1) * 0.5f, top + 0.24f, z), new Vector3(1.4f, 0.32f, 0.06f), Yellow, Yellow);
            k.FlatBox(new Vector3((x0 + x1) * 0.5f, top + 0.24f, z - 0.033f), new Vector3(1.25f, 0.2f, 0.004f), Red);
            for (int i = 0; i < 5; i++)
                k.FlatBox(new Vector3(1.1f + i * 0.25f, top + 0.24f, z - 0.036f), new Vector3(0.12f, 0.12f, 0.004f), Paper);
        }

        // ───────────── 바깥 (보도) ─────────────

        static void Outside(MeshKit k, MeshKit blob)
        {
            // 보도블록 (z -6 ~ -4)
            const float t = 0.5f;
            for (int x = 0; x < 17; x++)
                for (int z = 0; z < 4; z++)
                {
                    Color c = (x + z * 2) % 3 == 0 ? PaveB : Pave;
                    // 블록은 줄눈판(윗면 y 0.001)보다 확실히 위에 둔다. 같은 높이면 확대했을 때 줄무늬로 깨진다(z-fighting).
                    k.FloorQuad(new Vector3(-4.2f + t * 0.5f + x * t * (8.4f / 8.5f), 0.006f, -6f + t * 0.5f + z * t), new Vector2(t * 0.96f, t * 0.96f), c);
                }
            k.FlatBox(new Vector3(0f, 0.0005f, -5f), new Vector3(8.4f, 0.001f, 2f), new Color(0.62f, 0.58f, 0.54f)); // 줄눈
            k.Box(new Vector3(0f, 0.04f, -5.95f), new Vector3(8.4f, 0.08f, 0.12f), new Color(0.85f, 0.83f, 0.80f)); // 연석
            // 문지방
            k.Box(new Vector3(0f, 0.02f, -4.0f), new Vector3(8.4f, 0.04f, 0.08f), new Color(0.6f, 0.55f, 0.52f));

            // 가게 앞 뽑기 기계 두 대 (왼쪽)
            GachaMachine(k, new Vector3(-3.55f, 0f, -4.55f), 180f, Blue, Yellow);
            GachaMachine(k, new Vector3(-2.95f, 0f, -4.55f), 180f, Pink, Paper);
            Shadow(blob, new Vector3(-3.25f, 0f, -4.55f), new Vector2(1.5f, 0.8f), 0f);

            // 입간판 (오른쪽)
            {
                var b = new Vector3(3.3f, 0f, -4.7f);
                k.Push(b, Quaternion.Euler(0f, 200f, 0f));
                k.Push(new Vector3(0f, 0.45f, 0.12f), Quaternion.Euler(-12f, 0f, 0f));
                k.Box(Vector3.zero, new Vector3(0.6f, 0.9f, 0.04f), Paper, WoodDark);
                k.FlatBox(new Vector3(0f, 0.32f, 0.022f), new Vector3(0.6f, 0.2f, 0.004f), Red);
                for (int i = 0; i < 3; i++)
                    k.FlatBox(new Vector3(0f, 0.08f - i * 0.14f, 0.022f), new Vector3(0.42f, 0.05f, 0.004f), i == 1 ? Blue : Ink);
                k.Pop();
                k.Push(new Vector3(0f, 0.45f, -0.12f), Quaternion.Euler(12f, 0f, 0f));
                k.Box(Vector3.zero, new Vector3(0.6f, 0.9f, 0.04f), WoodDark, WoodDark);
                k.Pop();
                k.Pop();
                Shadow(blob, b, new Vector2(0.8f, 0.5f), 0f);
            }
        }

        // ───────────── 매대 (ShelfSlot 자리) ─────────────

        /// <summary>나무 계단식 매대. 슬롯 로컬 좌표(앞=+Z)로 쌓는다. 상품 윗면 높이: 앞 0.68, 가운데 0.80, 뒤 0.92.</summary>
        public static void ShelfCabinet(MeshKit k, MeshKit blob, Vector3 pos, float yaw, int index)
        {
            k.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            // 몸통 (앞쪽은 재고 상자가 보이는 열린 칸)
            k.Box(new Vector3(0f, 0.36f, -0.075f), new Vector3(1.1f, 0.52f, 0.55f), WoodLight, Wood);
            k.Box(new Vector3(0f, 0.05f, 0.0f), new Vector3(1.12f, 0.1f, 0.72f), WoodDark, WoodDark);
            k.Box(new Vector3(-0.53f, 0.36f, 0.275f), new Vector3(0.04f, 0.52f, 0.15f), Wood, Wood);
            k.Box(new Vector3(0.53f, 0.36f, 0.275f), new Vector3(0.04f, 0.52f, 0.15f), Wood, Wood);
            k.FlatBox(new Vector3(0f, 0.36f, 0.203f), new Vector3(1.02f, 0.5f, 0.004f), MeshKit.Shade(Wood, 0.62f));
            Color box = new Color(0.82f, 0.64f, 0.44f);
            k.Box(new Vector3(-0.24f, 0.21f, 0.27f), new Vector3(0.42f, 0.22f, 0.12f), MeshKit.Shade(box, 1.05f), box);
            k.Box(new Vector3(0.24f, 0.19f, 0.27f), new Vector3(0.4f, 0.18f, 0.12f), MeshKit.Shade(box, 1.05f), box);
            k.FlatBox(new Vector3(-0.24f, 0.22f, 0.332f), new Vector3(0.16f, 0.08f, 0.004f), Paper);
            k.Box(new Vector3(0f, 0.59f, 0.33f), new Vector3(1.1f, 0.06f, 0.04f), Wood, Wood);
            // 계단 3단
            k.Box(new Vector3(0f, 0.65f, 0f), new Vector3(1.14f, 0.06f, 0.74f), WoodLight, Wood);
            k.Box(new Vector3(0f, 0.74f, -0.125f), new Vector3(1.1f, 0.12f, 0.45f), WoodLight, Wood);
            k.Box(new Vector3(0f, 0.86f, -0.225f), new Vector3(1.1f, 0.12f, 0.25f), WoodLight, Wood);
            // 단마다 앞 가격 레일 (크림색)
            k.FlatBox(new Vector3(0f, 0.68f, 0.371f), new Vector3(1.14f, 0.04f, 0.006f), Paper);
            k.FlatBox(new Vector3(0f, 0.785f, 0.101f), new Vector3(1.1f, 0.03f, 0.006f), Paper);
            k.FlatBox(new Vector3(0f, 0.905f, -0.099f), new Vector3(1.1f, 0.03f, 0.006f), Paper);
            // 칸막이 (열 사이)
            for (int i = 1; i < 4; i++)
            {
                float x = -0.52f + i * 0.26f;
                k.Box(new Vector3(x, 0.71f, 0.22f), new Vector3(0.012f, 0.06f, 0.24f), Paper, Paper);
            }
            // 뒤 머리판 (색 띠)
            Color band = Candy[index % Candy.Length];
            k.Box(new Vector3(0f, 1.06f, -0.33f), new Vector3(1.12f, 0.28f, 0.04f), WoodLight, Wood);
            k.FlatBox(new Vector3(0f, 1.07f, -0.308f), new Vector3(1.0f, 0.16f, 0.004f), band);
            k.FlatBox(new Vector3(0f, 1.07f, -0.305f), new Vector3(0.5f, 0.06f, 0.004f), Paper);
            k.Pop();

            // 그림자
            blob.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            blob.FloorQuad(new Vector3(0f, 0.004f, 0.08f), new Vector2(1.6f, 1.15f), Color.white);
            blob.Pop();
        }

        public static void Shadow(MeshKit blob, Vector3 pos, Vector2 size, float yaw)
        {
            blob.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            blob.FloorQuad(new Vector3(0f, 0.004f, 0f), size, Color.white);
            blob.Pop();
        }
    }
}
