using Munbanggu.Core;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 가게 증축 모습. 단계가 바뀔 때마다 통째로 다시 만든다.
    /// 1 옆칸: 왼쪽(-X)으로 바닥·뒷벽·차양·보도·받침을 2.5m 이어 붙임 (x -6.5..-4)
    /// 2 평상: 옆칸 앞 보도에 나무 평상 (바둑판·부채·방석)
    /// 3 2층 만화방: 옆칸 뒷벽 위에 올린 작은 2층 (빨간 지붕·책장 창문·간판)
    /// 옆칸 진열대는 ShopBuilder.AddShelfGroup이 따로 만든다.
    /// </summary>
    public static class ExpansionVisuals
    {
        static readonly MeshKit kit = new MeshKit();
        static readonly MeshKit glass = new MeshKit();
        static readonly MeshKit blob = new MeshKit();

        public const float AnnexX0 = -6.5f, AnnexX1 = -4.0f;

        public static GameObject Rebuild(Transform parent, MaterialCache mats, int level, GameObject previous)
        {
            if (previous != null)
            {
                if (Application.isPlaying) Object.Destroy(previous);
                else Object.DestroyImmediate(previous);
            }
            var root = new GameObject("Expansion");
            root.transform.SetParent(parent, false);
            if (level <= 0) return root;

            kit.Clear(); glass.Clear(); blob.Clear();
            Annex(kit, blob);
            if (level >= 2) Pyeongsang(kit, blob, new Vector3(-5.25f, 0f, -4.95f));
            if (level >= 3) ComicRoom(kit, glass);
            kit.Build("Expansion Mesh", root.transform, mats.VertexColor);
            if (mats.Glass != null) glass.Build("Expansion Glass", root.transform, mats.Glass);
            if (mats.Blob != null) blob.Build("Expansion Shadows", root.transform, mats.Blob);
            return root;
        }

        // ───────────── 1단계: 옆칸 ─────────────

        static void Annex(MeshKit k, MeshKit b)
        {
            const float x0 = AnnexX0, x1 = AnnexX1;
            const float px0 = x0 - 0.2f, px1 = -4.2f; // 받침은 본 가게 받침(x -4.2)에 맞붙임
            b.FloorQuad(new Vector3((px0 + px1) * 0.5f - 0.3f, -0.45f, -1.3f), new Vector2(px1 - px0 + 1.2f, 16f), Color.white);

            // 받침
            k.Box(new Vector3((px0 + px1) * 0.5f, -0.2f, -0.9f), new Vector3(px1 - px0, 0.4f, 10.2f), ShopDecor.PlinthLip, ShopDecor.PlinthSide);
            k.FlatBox(new Vector3((px0 + px1) * 0.5f, -0.03f, -0.9f), new Vector3(px1 - px0 + 0.02f, 0.06f, 10.22f), ShopDecor.PlinthLip);

            // 바닥 (본 가게 체크무늬 이어서: 본 가게 칸 x=0이 FloorA(z=0일 때))
            const float t = 0.5f;
            int cols = Mathf.RoundToInt((x1 - x0) / t);
            for (int i = 1; i <= cols; i++)
                for (int z = 0; z < 16; z++)
                {
                    var c = ((-i + z) % 2 + 2) % 2 == 0 ? ShopDecor.FloorA : ShopDecor.FloorB;
                    k.FloorQuad(new Vector3(x1 - t * 0.5f - (i - 1) * t, 0.001f, -4f + t * 0.5f + z * t), new Vector2(t, t), c);
                }
            // 옛 벽 자리 표시: 바닥에 얇은 나무 문턱 (옆칸을 텄다는 느낌)
            k.FlatBox(new Vector3(x1, 0.004f, 0f), new Vector3(0.1f, 0.008f, 8f), ShopDecor.WoodLight);

            // 뒷벽 (+Z) 이어 붙이기 + 몰딩·징두리
            float wh = ShopDecor.WallH;
            k.Box(new Vector3((x0 - 0.2f + -4.2f) * 0.5f, wh * 0.5f, 4.1f), new Vector3(-4.2f - (x0 - 0.2f), wh, 0.2f), ShopDecor.WallTop, ShopDecor.WallUpper);
            k.Push(new Vector3(0f, 0f, 4.0f), Quaternion.identity);
            ShopDecor.WallTrim(k, x0, x1);
            // 차양: 본 가게 차양(x -4부터 빨강)과 줄무늬가 이어지게 8줄
            ShopDecor.Awning(k, x1 - 8 * 0.32f + 0.001f, x1, 0);
            // 벽 포스터 두 장 (옆칸 진열대 위)
            Poster(k, new Vector3(-4.65f, 1.95f, -0.02f), ShopDecor.Blue, ShopDecor.Yellow);
            Poster(k, new Vector3(-5.9f, 1.95f, -0.02f), ShopDecor.Pink, ShopDecor.Green);
            k.Pop();

            // 왼쪽 끝 기둥 (디오라마 단면 쪽 마감)
            k.Box(new Vector3(x0 - 0.1f, wh * 0.5f, 4.1f), new Vector3(0.22f, wh, 0.24f), ShopDecor.WoodDark, ShopDecor.Wood);

            // 보도 (z -6 ~ -4) 이어 붙이기
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 4; z++)
                {
                    Color c = (x + z * 2) % 3 == 1 ? ShopDecor.PaveB : ShopDecor.Pave;
                    k.FloorQuad(new Vector3(-4.2f - t * 0.5f - x * t, 0.006f, -6f + t * 0.5f + z * t), new Vector2(t * 0.96f, t * 0.96f), c);
                }
            k.FlatBox(new Vector3((px0 + px1) * 0.5f, 0.0005f, -5f), new Vector3(px1 - px0, 0.001f, 2f), new Color(0.62f, 0.58f, 0.54f));
            k.Box(new Vector3((px0 + px1) * 0.5f, 0.04f, -5.95f), new Vector3(px1 - px0, 0.08f, 0.12f), new Color(0.85f, 0.83f, 0.80f));
            k.Box(new Vector3((x0 + -4.2f) * 0.5f, 0.02f, -4.0f), new Vector3(-4.2f - x0, 0.04f, 0.08f), new Color(0.6f, 0.55f, 0.52f));

            // 옆칸 구석 소품: 화분 하나
            k.Frustum(new Vector3(-6.25f, 0.18f, -3.6f), 0.16f, 0.2f, 0.36f, 8, new Color(0.8f, 0.45f, 0.3f), new Color(0.45f, 0.3f, 0.22f));
            k.Sphere(new Vector3(-6.25f, 0.55f, -3.6f), new Vector3(0.26f, 0.3f, 0.26f), 8, 4, new Color(0.36f, 0.66f, 0.4f));
            ShopDecor.Shadow(b, new Vector3(-6.25f, 0f, -3.6f), new Vector2(0.6f, 0.6f), 0f);
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

        // ───────────── 3단계: 2층 만화방 ─────────────

        static void ComicRoom(MeshKit k, MeshKit g)
        {
            const float x0 = AnnexX0 - 0.15f, x1 = -4.05f;
            const float z0 = 3.2f, z1 = 4.25f;
            float y0 = ShopDecor.WallH;
            const float h = 1.35f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;

            // 바닥판 + 몸체
            k.Box(new Vector3(cx, y0 + 0.06f, cz), new Vector3(w + 0.1f, 0.12f, d + 0.1f), ShopDecor.WoodDark);
            k.Box(new Vector3(cx, y0 + 0.12f + h * 0.5f, cz), new Vector3(w, h, d), ShopDecor.WallTop, new Color(0.62f, 0.80f, 0.86f));
            // 지붕 (빨간 기와, 앞으로 살짝 기울게)
            k.Push(new Vector3(cx, y0 + 0.12f + h + 0.12f, cz - 0.05f), Quaternion.Euler(-12f, 0f, 0f));
            k.Box(Vector3.zero, new Vector3(w + 0.3f, 0.1f, d + 0.45f), ShopDecor.Red, MeshKit.Shade(ShopDecor.Red, 0.75f));
            for (int i = 0; i < 4; i++)
                k.FlatBox(new Vector3(0f, 0.052f, -d * 0.5f + 0.08f + i * (d + 0.2f) / 4f), new Vector3(w + 0.28f, 0.004f, 0.03f), MeshKit.Shade(ShopDecor.Red, 0.85f));
            k.Pop();

            // 앞면(-Z) 창문 2개: 안에 만화책 꽂힌 책장이 보임
            float wy = y0 + 0.12f + h * 0.52f;
            foreach (float wx in new[] { x0 + w * 0.28f, x0 + w * 0.72f })
            {
                k.FlatBox(new Vector3(wx, wy, z0 - 0.005f), new Vector3(0.78f, 0.62f, 0.01f), ShopDecor.Paper);
                // 책등 줄
                Color[] spines = { ShopDecor.Red, ShopDecor.Yellow, ShopDecor.Blue, ShopDecor.Green, ShopDecor.Pink, ShopDecor.Ink };
                for (int r = 0; r < 2; r++)
                    for (int i = 0; i < 9; i++)
                        k.FlatBox(new Vector3(wx - 0.3f + i * 0.075f, wy - 0.12f + r * 0.25f, z0 - 0.012f), new Vector3(0.06f, 0.2f, 0.004f), spines[(i + r * 2) % spines.Length]);
                g.FlatBox(new Vector3(wx, wy, z0 - 0.02f), new Vector3(0.72f, 0.56f, 0.004f), new Color(0.8f, 0.92f, 1f));
                k.Box(new Vector3(wx, wy - 0.33f, z0 - 0.04f), new Vector3(0.84f, 0.05f, 0.08f), ShopDecor.WoodLight, ShopDecor.Wood);
            }

            // 간판: 빨간 판 + 노란 펼친 책 모양
            float sy = y0 + 0.12f + h + 0.5f;
            k.Box(new Vector3(cx, sy, z0 + 0.05f), new Vector3(1.5f, 0.5f, 0.08f), ShopDecor.Yellow, ShopDecor.Red);
            k.FlatBox(new Vector3(cx, sy, z0 + 0.005f), new Vector3(1.38f, 0.38f, 0.004f), ShopDecor.Red);
            k.Push(new Vector3(cx, sy, z0), Quaternion.identity);
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
            k.FlatBox(new Vector3(-0.55f, 0f, -0.012f), new Vector3(0.14f, 0.14f, 0.004f), ShopDecor.Yellow);
            k.FlatBox(new Vector3(0.55f, 0f, -0.012f), new Vector3(0.14f, 0.14f, 0.004f), ShopDecor.Yellow);
            k.Pop();
            // 간판 받침 기둥
            k.Box(new Vector3(cx - 0.55f, sy - 0.4f, z0 + 0.12f), new Vector3(0.05f, 0.4f, 0.05f), ShopDecor.WoodDark);
            k.Box(new Vector3(cx + 0.55f, sy - 0.4f, z0 + 0.12f), new Vector3(0.05f, 0.4f, 0.05f), ShopDecor.WoodDark);

        }
    }
}
