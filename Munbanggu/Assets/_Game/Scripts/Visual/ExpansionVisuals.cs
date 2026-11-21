using Munbanggu.Core;
using Munbanggu.Shop;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 가게 증축·동네 건물 모습. 단계가 바뀔 때마다 통째로 다시 만든다.
    /// 옆칸 k: 왼쪽(-X)으로 바닥·뒷벽·차양·보도·받침을 2.5m씩 이어 붙임 (ShopLayout.BayX0/X1)
    /// 평상(옆칸1 앞 보도), 냉동고(옆칸1 앞 보도), 2층 만화방/오락실(옆칸1 뒷벽 위), 네온 간판(본 간판 둘레)
    /// 옆칸 진열대는 ShopBuilder.AddShelfGroup이 따로 만든다.
    /// </summary>
    public static class ExpansionVisuals
    {
        static readonly MeshKit kit = new MeshKit();
        static readonly MeshKit glass = new MeshKit();
        static readonly MeshKit blob = new MeshKit();

        public const float AnnexX0 = -6.5f, AnnexX1 = -4.0f;

        public static GameObject Rebuild(Transform parent, MaterialCache mats, int district, int level, GameObject previous)
        {
            if (previous != null)
            {
                if (Application.isPlaying) Object.Destroy(previous);
                else Object.DestroyImmediate(previous);
            }
            var root = new GameObject("Expansion");
            root.transform.SetParent(parent, false);

            kit.Clear(); glass.Clear(); blob.Clear();
            int bays = ExpansionTable.Bays(district, level);
            for (int i = 1; i <= bays; i++) Bay(kit, blob, i, i == bays);
            bool Has(ExpansionFeature f) => ExpansionTable.Has(district, level, f);
            if (Has(ExpansionFeature.Pyeongsang)) Pyeongsang(kit, blob, new Vector3(-5.25f, 0f, -4.95f));
            if (Has(ExpansionFeature.Freezer)) Freezer(kit, glass, blob, new Vector3(-5.25f, 0f, -4.85f));
            if (Has(ExpansionFeature.ComicRoom)) Upstairs(kit, glass, arcade: false);
            if (Has(ExpansionFeature.Arcade2F)) Upstairs(kit, glass, arcade: true);
            if (Has(ExpansionFeature.Neon)) Neon(kit);
            if (kit.VertexCount == 0) return root;
            kit.Build("Expansion Mesh", root.transform, mats.VertexColor);
            if (mats.Glass != null) glass.Build("Expansion Glass", root.transform, mats.Glass);
            if (mats.Blob != null) blob.Build("Expansion Shadows", root.transform, mats.Blob);
            return root;
        }

        // ───────────── 옆칸 ─────────────

        /// <summary>옆칸 k (1부터). leftmost면 왼쪽 끝 기둥·화분까지.</summary>
        static void Bay(MeshKit k, MeshKit b, int bay, bool leftmost)
        {
            float x0 = ShopLayout.BayX0(bay), x1 = ShopLayout.BayX1(bay);
            // 받침·벽·보도는 이 칸 오른쪽 끝(본 가게면 -4.2)에서 왼쪽 끝(맨 왼쪽 칸이면 -0.2 더)까지
            float px1 = bay == 1 ? -4.2f : x1, px0 = leftmost ? x0 - 0.2f : x0;
            b.FloorQuad(new Vector3((px0 + px1) * 0.5f - 0.3f, -0.45f, -1.3f), new Vector2(px1 - px0 + 1.2f, 16f), Color.white);

            // 받침
            k.Box(new Vector3((px0 + px1) * 0.5f, -0.2f, -0.9f), new Vector3(px1 - px0, 0.4f, 10.2f), ShopDecor.PlinthLip, ShopDecor.PlinthSide);
            k.FlatBox(new Vector3((px0 + px1) * 0.5f, -0.03f, -0.9f), new Vector3(px1 - px0 + 0.02f, 0.06f, 10.22f), ShopDecor.PlinthLip);

            // 바닥 (본 가게 체크무늬 이어서: 본 가게 x 칸 번호를 음수로 이어 감)
            const float t = 0.5f;
            int cols = Mathf.RoundToInt((x1 - x0) / t);
            int firstCol = Mathf.RoundToInt((-4f - x1) / t); // 이 칸 오른쪽 끝이 본 가게 왼쪽 끝에서 몇 칸 떨어졌나
            for (int i = 1; i <= cols; i++)
                for (int z = 0; z < 16; z++)
                {
                    int gx = -(firstCol + i);
                    var c = ((gx + z) % 2 + 2) % 2 == 0 ? ShopDecor.FloorA : ShopDecor.FloorB;
                    k.FloorQuad(new Vector3(x1 - t * 0.5f - (i - 1) * t, 0.001f, -4f + t * 0.5f + z * t), new Vector2(t, t), c);
                }
            // 옛 벽 자리 표시: 바닥에 얇은 나무 문턱 (옆칸을 텄다는 느낌)
            k.FlatBox(new Vector3(x1, 0.004f, 0f), new Vector3(0.1f, 0.008f, 8f), ShopDecor.WoodLight);

            // 뒷벽 (+Z) 이어 붙이기 + 몰딩·징두리
            float wh = ShopDecor.WallH;
            k.Box(new Vector3((px0 + px1) * 0.5f, wh * 0.5f, 4.1f), new Vector3(px1 - px0, wh, 0.2f), ShopDecor.WallTop, ShopDecor.WallUpper);
            k.Push(new Vector3(0f, 0f, 4.0f), Quaternion.identity);
            ShopDecor.WallTrim(k, x0, x1);
            // 차양: 오른쪽 칸과 줄무늬가 이어지게 (2.5m = 0.32m × 8줄 → 칸마다 위상 같음)
            ShopDecor.Awning(k, x1 - 8 * 0.32f + 0.001f, x1, 0);
            // 벽 포스터 두 장 (진열대 위)
            Poster(k, new Vector3(x1 - 0.65f, 1.95f, -0.02f), bay % 2 == 1 ? ShopDecor.Blue : ShopDecor.Green, ShopDecor.Yellow);
            Poster(k, new Vector3(x1 - 1.9f, 1.95f, -0.02f), ShopDecor.Pink, bay % 2 == 1 ? ShopDecor.Green : ShopDecor.Blue);
            k.Pop();

            // 보도 (z -6 ~ -4) 이어 붙이기
            int pcols = Mathf.CeilToInt((px1 - px0) / t - 0.01f);
            for (int x = 0; x < pcols; x++)
                for (int z = 0; z < 4; z++)
                {
                    Color c = (x + bay + z * 2) % 3 == 1 ? ShopDecor.PaveB : ShopDecor.Pave;
                    k.FloorQuad(new Vector3(px1 - t * 0.5f - x * t, 0.006f, -6f + t * 0.5f + z * t), new Vector2(t * 0.96f, t * 0.96f), c);
                }
            k.FlatBox(new Vector3((px0 + px1) * 0.5f, 0.0005f, -5f), new Vector3(px1 - px0, 0.001f, 2f), new Color(0.62f, 0.58f, 0.54f));
            k.Box(new Vector3((px0 + px1) * 0.5f, 0.04f, -5.95f), new Vector3(px1 - px0, 0.08f, 0.12f), new Color(0.85f, 0.83f, 0.80f));
            k.Box(new Vector3((px0 + px1) * 0.5f, 0.02f, -4.0f), new Vector3(px1 - px0, 0.04f, 0.08f), new Color(0.6f, 0.55f, 0.52f));

            if (!leftmost) return;
            // 왼쪽 끝 기둥 (디오라마 단면 쪽 마감) + 구석 화분
            k.Box(new Vector3(x0 - 0.1f, wh * 0.5f, 4.1f), new Vector3(0.22f, wh, 0.24f), ShopDecor.WoodDark, ShopDecor.Wood);
            var pot = new Vector3(x0 + 0.25f, 0f, -3.6f);
            k.Frustum(pot + new Vector3(0f, 0.18f, 0f), 0.16f, 0.2f, 0.36f, 8, new Color(0.8f, 0.45f, 0.3f), new Color(0.45f, 0.3f, 0.22f));
            k.Sphere(pot + new Vector3(0f, 0.55f, 0f), new Vector3(0.26f, 0.3f, 0.26f), 8, 4, new Color(0.36f, 0.66f, 0.4f));
            ShopDecor.Shadow(b, pot, new Vector2(0.6f, 0.6f), 0f);
        }

        /// <summary>벽 좌표계(원점=벽 안쪽 면, -Z=방 쪽)에 붙이는 포스터.</summary>
        static void Poster(MeshKit k, Vector3 c, Color main, Color accent)
        {
            k.FlatBox(c, new Vector3(0.62f, 0.8f, 0.01f), ShopDecor.Paper);
            k.FlatBox(c + new Vector3(0f, 0.12f, -0.007f), new Vector3(0.5f, 0.44f, 0.004f), main);
            k.FlatBox(c + new Vector3(0f, 0.16f, -0.01f), new Vector3(0.22f, 0.22f, 0.004f), accent);
            k.FlatBox(c + new Vector3(0f, -0.25f, -0.007f), new Vector3(0.44f, 0.06f, 0.004f), ShopDecor.Ink);
        }

        // ───────────── 2단계: 평상 ─────────────

        static void Pyeongsang(MeshKit k, MeshKit b, Vector3 p)
        {
            k.Push(p, Quaternion.identity);
            const float w = 1.7f, d = 0.95f, top = 0.42f;
            // 다리 4개
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    k.Box(new Vector3(sx * (w * 0.5f - 0.08f), top * 0.5f, sz * (d * 0.5f - 0.08f)), new Vector3(0.09f, top, 0.09f), ShopDecor.WoodDark);
            // 상판 (널판 줄)
            k.Box(new Vector3(0f, top, 0f), new Vector3(w, 0.07f, d), ShopDecor.WoodLight, ShopDecor.Wood);
            for (int i = 1; i < 6; i++)
                k.FlatBox(new Vector3(-w * 0.5f + i * w / 6f, top + 0.036f, 0f), new Vector3(0.012f, 0.004f, d - 0.04f), ShopDecor.Wood);
            // 방석 두 개
            k.Box(new Vector3(-0.5f, top + 0.06f, 0.1f), new Vector3(0.42f, 0.06f, 0.42f), ShopDecor.Red, MeshKit.Shade(ShopDecor.Red, 0.8f));
            k.Box(new Vector3(0.5f, top + 0.06f, 0.1f), new Vector3(0.42f, 0.06f, 0.42f), ShopDecor.Blue, MeshKit.Shade(ShopDecor.Blue, 0.8f));
            // 가운데 바둑판 + 돌
            k.Box(new Vector3(0f, top + 0.07f, -0.05f), new Vector3(0.36f, 0.08f, 0.36f), new Color(0.92f, 0.76f, 0.48f), ShopDecor.Wood);
            for (int i = 0; i < 6; i++)
                k.Cylinder(new Vector3(-0.12f + (i % 3) * 0.12f, top + 0.118f, -0.12f + (i / 3) * 0.14f), 0.025f, 0.012f, 6, i % 2 == 0 ? ShopDecor.Ink : ShopDecor.Paper);
            // 부채
            k.Push(new Vector3(0.62f, top + 0.045f, -0.3f), Quaternion.Euler(0f, 30f, 0f));
            k.FlatBox(Vector3.zero, new Vector3(0.26f, 0.01f, 0.2f), ShopDecor.Yellow);
            k.FlatBox(new Vector3(0f, 0f, -0.15f), new Vector3(0.03f, 0.01f, 0.12f), ShopDecor.WoodDark);
            k.Pop();
            k.Pop();
            ShopDecor.Shadow(b, p, new Vector2(w + 0.4f, d + 0.35f), 0f);
        }

        // ───────────── 2층 만화방 / 오락실 (옆칸1 뒷벽 위) ─────────────

        static void Upstairs(MeshKit k, MeshKit g, bool arcade)
        {
            const float x0 = AnnexX0 - 0.15f, x1 = -4.05f;
            const float z0 = 3.2f, z1 = 4.25f;
            float y0 = ShopDecor.WallH;
            const float h = 1.35f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
            Color body = arcade ? new Color(0.30f, 0.30f, 0.46f) : new Color(0.62f, 0.80f, 0.86f);
            Color roof = arcade ? new Color(0.20f, 0.22f, 0.32f) : ShopDecor.Red;

            // 바닥판 + 몸체
            k.Box(new Vector3(cx, y0 + 0.06f, cz), new Vector3(w + 0.1f, 0.12f, d + 0.1f), ShopDecor.WoodDark);
            k.Box(new Vector3(cx, y0 + 0.12f + h * 0.5f, cz), new Vector3(w, h, d), ShopDecor.WallTop, body);
            // 지붕 (앞으로 살짝 기울게)
            k.Push(new Vector3(cx, y0 + 0.12f + h + 0.12f, cz - 0.05f), Quaternion.Euler(-12f, 0f, 0f));
            k.Box(Vector3.zero, new Vector3(w + 0.3f, 0.1f, d + 0.45f), roof, MeshKit.Shade(roof, 0.75f));
            for (int i = 0; i < 4; i++)
                k.FlatBox(new Vector3(0f, 0.052f, -d * 0.5f + 0.08f + i * (d + 0.2f) / 4f), new Vector3(w + 0.28f, 0.004f, 0.03f), MeshKit.Shade(roof, 0.85f));
            k.Pop();

            // 앞면(-Z) 창문 2개: 만화방은 책장, 오락실은 오락기 화면들
            float wy = y0 + 0.12f + h * 0.52f;
            foreach (float wx in new[] { x0 + w * 0.28f, x0 + w * 0.72f })
            {
                k.FlatBox(new Vector3(wx, wy, z0 - 0.005f), new Vector3(0.78f, 0.62f, 0.01f), arcade ? new Color(0.12f, 0.12f, 0.2f) : ShopDecor.Paper);
                if (arcade)
                {
                    Color[] screens = { new Color(0.4f, 0.95f, 0.9f), new Color(1f, 0.45f, 0.75f), new Color(1f, 0.9f, 0.3f) };
                    for (int i = 0; i < 3; i++)
                    {
                        float sx = wx - 0.24f + i * 0.24f;
                        k.FlatBox(new Vector3(sx, wy + 0.06f, z0 - 0.012f), new Vector3(0.18f, 0.16f, 0.004f), screens[i]);
                        k.FlatBox(new Vector3(sx, wy - 0.16f, z0 - 0.012f), new Vector3(0.2f, 0.2f, 0.004f), ShopDecor.Red);
                    }
                }
                else
                {
                    Color[] spines = { ShopDecor.Red, ShopDecor.Yellow, ShopDecor.Blue, ShopDecor.Green, ShopDecor.Pink, ShopDecor.Ink };
                    for (int r = 0; r < 2; r++)
                        for (int i = 0; i < 9; i++)
                            k.FlatBox(new Vector3(wx - 0.3f + i * 0.075f, wy - 0.12f + r * 0.25f, z0 - 0.012f), new Vector3(0.06f, 0.2f, 0.004f), spines[(i + r * 2) % spines.Length]);
                }
                g.FlatBox(new Vector3(wx, wy, z0 - 0.02f), new Vector3(0.72f, 0.56f, 0.004f), new Color(0.8f, 0.92f, 1f));
                k.Box(new Vector3(wx, wy - 0.33f, z0 - 0.04f), new Vector3(0.84f, 0.05f, 0.08f), ShopDecor.WoodLight, ShopDecor.Wood);
            }

            // 간판: 만화방 = 노란 테 빨간 판 + 펼친 책, 오락실 = 분홍 테 남색 판 + 조이스틱
            float sy = y0 + 0.12f + h + 0.5f;
            Color frame = arcade ? new Color(1f, 0.45f, 0.75f) : ShopDecor.Yellow;
            Color board = arcade ? new Color(0.16f, 0.18f, 0.36f) : ShopDecor.Red;
            k.Box(new Vector3(cx, sy, z0 + 0.05f), new Vector3(1.5f, 0.5f, 0.08f), frame, board);
            k.FlatBox(new Vector3(cx, sy, z0 + 0.005f), new Vector3(1.38f, 0.38f, 0.004f), board);
            k.Push(new Vector3(cx, sy, z0), Quaternion.identity);
            if (arcade)
            {
                k.FlatBox(new Vector3(0f, -0.08f, -0.012f), new Vector3(0.5f, 0.12f, 0.004f), new Color(0.4f, 0.95f, 0.9f));
                k.FlatBox(new Vector3(-0.1f, 0.04f, -0.014f), new Vector3(0.03f, 0.16f, 0.004f), ShopDecor.Paper);
                k.FlatBox(new Vector3(-0.1f, 0.12f, -0.016f), new Vector3(0.09f, 0.09f, 0.004f), ShopDecor.Red);
                k.FlatBox(new Vector3(0.12f, -0.02f, -0.016f), new Vector3(0.07f, 0.07f, 0.004f), ShopDecor.Yellow);
                k.FlatBox(new Vector3(0.22f, -0.02f, -0.016f), new Vector3(0.07f, 0.07f, 0.004f), new Color(1f, 0.45f, 0.75f));
            }
            else
            {
                k.Push(new Vector3(-0.17f, 0f, -0.01f), Quaternion.Euler(0f, 0f, 8f));
                k.FlatBox(Vector3.zero, new Vector3(0.32f, 0.24f, 0.006f), ShopDecor.Paper);
                k.Pop();
                k.Push(new Vector3(0.17f, 0f, -0.01f), Quaternion.Euler(0f, 0f, -8f));
                k.FlatBox(Vector3.zero, new Vector3(0.32f, 0.24f, 0.006f), ShopDecor.Paper);
                k.Pop();
                for (int i = 0; i < 3; i++)
                {
                    k.FlatBox(new Vector3(-0.17f, 0.06f - i * 0.06f, -0.016f), new Vector3(0.2f, 0.02f, 0.004f), ShopDecor.Ink);
                    k.FlatBox(new Vector3(0.17f, 0.06f - i * 0.06f, -0.016f), new Vector3(0.2f, 0.02f, 0.004f), ShopDecor.Ink);
                }
            }
            k.FlatBox(new Vector3(-0.55f, 0f, -0.012f), new Vector3(0.14f, 0.14f, 0.004f), frame);
            k.FlatBox(new Vector3(0.55f, 0f, -0.012f), new Vector3(0.14f, 0.14f, 0.004f), frame);
            k.Pop();
            // 간판 받침 기둥
            k.Box(new Vector3(cx - 0.55f, sy - 0.4f, z0 + 0.12f), new Vector3(0.05f, 0.4f, 0.05f), ShopDecor.WoodDark);
            k.Box(new Vector3(cx + 0.55f, sy - 0.4f, z0 + 0.12f), new Vector3(0.05f, 0.4f, 0.05f), ShopDecor.WoodDark);
        }

        // ───────────── 아이스크림 냉동고 (옆칸1 앞 보도) ─────────────

        static void Freezer(MeshKit k, MeshKit g, MeshKit b, Vector3 p)
        {
            k.Push(p, Quaternion.identity);
            const float w = 1.4f, d = 0.7f, hgt = 0.82f;
            var white = new Color(0.97f, 0.97f, 0.98f);
            var blue = new Color(0.25f, 0.55f, 0.9f);
            k.Box(new Vector3(0f, hgt * 0.5f, 0f), new Vector3(w, hgt, d), white, MeshKit.Shade(white, 0.85f));
            k.FlatBox(new Vector3(0f, hgt * 0.45f, -d * 0.5f - 0.003f), new Vector3(w - 0.1f, 0.2f, 0.004f), blue); // 앞 띠
            k.FlatBox(new Vector3(0f, hgt * 0.45f, -d * 0.5f - 0.006f), new Vector3(0.5f, 0.08f, 0.004f), ShopDecor.Yellow);
            // 유리 뚜껑 + 안의 아이스크림
            Color[] ice = { ShopDecor.Pink, ShopDecor.Yellow, new Color(0.55f, 0.35f, 0.25f), new Color(0.6f, 0.85f, 0.95f), ShopDecor.Green };
            for (int i = 0; i < 10; i++)
                k.Box(new Vector3(-0.55f + (i % 5) * 0.27f, hgt - 0.05f, -0.15f + (i / 5) * 0.3f), new Vector3(0.2f, 0.06f, 0.22f), ice[(i * 3) % ice.Length]);
            g.FlatBox(p + new Vector3(0f, hgt + 0.005f, 0f), new Vector3(w - 0.08f, 0.004f, d - 0.08f), new Color(0.82f, 0.93f, 1f)); // g엔 Push가 없으니 p를 더함
            k.Box(new Vector3(0f, hgt + 0.01f, d * 0.5f - 0.03f), new Vector3(w, 0.03f, 0.05f), white);
            // 파라솔 기둥 + 우산
            k.Cylinder(new Vector3(w * 0.5f + 0.15f, 0.8f, 0.1f), 0.025f, 1.6f, 6, ShopDecor.Metal);
            k.Frustum(new Vector3(w * 0.5f + 0.15f, 1.62f, 0.1f), 0.75f, 0.04f, 0.3f, 10, ShopDecor.Red, ShopDecor.Paper);
            k.Pop();
            ShopDecor.Shadow(b, p, new Vector2(w + 0.6f, d + 0.4f), 0f);
        }

        // ───────────── 네온 간판 (본 간판 둘레) ─────────────

        static void Neon(MeshKit k)
        {
            // 본 간판: 가운데 x -0.4, 폭 5.6(+테 0.16), y 3.02~3.94, 앞면 z ≈ 4.0
            const float cx = -0.4f, w = 5.96f, y0 = 2.9f, y1 = 4.06f, z = 3.93f, th = 0.05f;
            var pink = new Color(1f, 0.38f, 0.72f);
            var cyan = new Color(0.35f, 0.95f, 0.95f);
            k.Box(new Vector3(cx, y1, z), new Vector3(w, th, th), pink);
            k.Box(new Vector3(cx, y0, z), new Vector3(w, th, th), pink);
            k.Box(new Vector3(cx - w * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(th, y1 - y0, th), pink);
            k.Box(new Vector3(cx + w * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(th, y1 - y0, th), pink);
            // 안쪽 하늘색 줄 + 위쪽 반짝 별
            k.Box(new Vector3(cx, y1 - 0.08f, z - 0.01f), new Vector3(w - 0.2f, 0.03f, 0.03f), cyan);
            k.Box(new Vector3(cx, y0 + 0.08f, z - 0.01f), new Vector3(w - 0.2f, 0.03f, 0.03f), cyan);
            for (int i = 0; i < 5; i++)
            {
                float sx = cx - 2.4f + i * 1.2f;
                k.Push(new Vector3(sx, y1 + 0.22f, z), Quaternion.Euler(0f, 0f, 45f));
                k.Box(Vector3.zero, new Vector3(0.14f, 0.14f, 0.03f), i % 2 == 0 ? ShopDecor.Yellow : cyan);
                k.Pop();
            }
        }
    }
}
