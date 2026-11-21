using System.Collections.Generic;
using Munbanggu.Data;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 상품 id → 매대 위 작은 상품 모양 (버텍스 컬러 메시, 상품별 1개를 캐시해 공유).
    /// 기준: 바닥 중앙이 원점, 앞면은 +Z, 크기는 대략 폭 0.22 × 깊이 0.16 × 높이 0.12~0.24.
    /// 모르는 id는 id 해시로 기본 모양(상자/봉지/통/카드) 중 하나 + product.color.
    /// </summary>
    public static class ProductVisuals
    {
        static readonly Dictionary<ProductData, MeshKit.Data> cache = new Dictionary<ProductData, MeshKit.Data>();
        static readonly Dictionary<ProductData, Mesh> meshCache = new Dictionary<ProductData, Mesh>();
        static readonly MeshKit kit = new MeshKit();

        static readonly Color Paper = new Color(0.98f, 0.96f, 0.90f);
        static readonly Color Ink = new Color(0.20f, 0.16f, 0.16f);
        static readonly Color Red = new Color(0.90f, 0.25f, 0.22f);
        static readonly Color Clear = new Color(0.85f, 0.95f, 0.97f);

        /// <summary>매대에 여러 개 복제해 붙일 모양 데이터 (상품별 캐시).</summary>
        public static MeshKit.Data DataFor(ProductData p)
        {
            if (p == null) return null;
            if (cache.TryGetValue(p, out var data)) return data;
            string id = !string.IsNullOrEmpty(p.visualOf) ? p.visualOf : string.IsNullOrEmpty(p.id) ? p.name : p.id;
            kit.Clear();
            BuildShape(kit, id, p.color);
            data = kit.ToData();
            cache[p] = data;
            return data;
        }

        /// <summary>상품 하나짜리 메시 (손님 손에 든 물건·연출용, 상품별 캐시). 머티리얼은 MaterialCache.VertexColor.</summary>
        public static Mesh MeshFor(ProductData p)
        {
            if (p == null) return null;
            if (meshCache.TryGetValue(p, out var mesh) && mesh != null) return mesh;
            kit.Clear();
            kit.Append(DataFor(p));
            mesh = kit.ToMesh("Product " + p.id);
            meshCache[p] = mesh;
            return mesh;
        }

        /// <summary>상품 모양 캐시 비우기 (에디터에서 색을 바꿨을 때).</summary>
        public static void ClearCache()
        {
            cache.Clear();
            meshCache.Clear();
        }

        static void BuildShape(MeshKit k, string id, Color c)
        {
            if (Munbanggu.Core.L.Zh && TaiwanShape(k, id)) return; // 대만판: 王子麵·牛奶糖·乖乖·尪仔標
            if (Munbanggu.Core.L.En && UsShape(k, id)) return;      // 미국판: 태피·루트비어 배럴·치즈퍼프·밀크캡
            switch (id)
            {
                case "jjondeugi": Jjondeugi(k, c); return;
                case "apollo": Apollo(k, c); return;
                case "ddakji": Ddakji(k, c); return;
                case "marble": Marble(k, c); return;
                case "eraser": Eraser(k, c); return;
                case "sticker": Sticker(k, c); return;
                case "paperdoll": PaperDoll(k, c); return;
                case "minicar": MiniCar(k, c); return;
                case "beercandy": Candy(k, c); return;
                case "pencil": Pencil(k, c); return;
                case "bouncyball": BouncyBall(k, c); return;
                case "yoyo": Yoyo(k, c); return;
                case "ssaldaerong": Ssaldaerong(k, c); return;
                case "bubble": Bubble(k, c); return;
                case "card": CardPack(k, c); return;
                case "ppopgi": Ppopgi(k, c); return;
                case "rubberband": RubberBand(k, c); return;
                case "watergun": WaterGun(k, c); return;
                case "virtualpet": VirtualPet(k, c); return;
                case "slime": Slime(k, c); return;
            }

            // 이름에 힌트가 있으면 비슷한 모양으로
            string s = id.ToLowerInvariant();
            if (s.Contains("candy") || s.Contains("gum") || s.Contains("jelly") || s.Contains("choco")) { Candy(k, c); return; }
            if (s.Contains("pencil") || s.Contains("pen") || s.Contains("crayon")) { Pencil(k, c); return; }
            if (s.Contains("ball")) { BouncyBall(k, c); return; }
            if (s.Contains("card") || s.Contains("sticker")) { CardPack(k, c); return; }
            if (s.Contains("car") || s.Contains("robot")) { MiniCar(k, c); return; }
            if (s.Contains("note") || s.Contains("book")) { Notebook(k, c); return; }

            switch (Hash(id) % 4)
            {
                case 0: GenericBox(k, c); break;
                case 1: GenericBag(k, c); break;
                case 2: GenericTub(k, c); break;
                default: GenericCard(k, c); break;
            }
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        static Color L(Color c, float t) => Color.Lerp(c, Color.white, t);
        static Color D(Color c, float k) => MeshKit.Shade(c, k);

        // ───────────── 대만판 모양 (docs/10) ─────────────

        /// <summary>대만판에서 이름만이 아니라 모양까지 바뀌는 상품. 아니면 false.</summary>
        static bool TaiwanShape(MeshKit k, string id)
        {
            switch (id)
            {
                case "jjondeugi": PrinceNoodle(k); return true;
                case "beercandy": MilkCandyBox(k); return true;
                case "ssaldaerong": KuaiKuai(k); return true;
                case "ddakji": AngAPiau(k); return true;
            }
            return false;
        }

        /// <summary>베개 모양 과자 봉지 (위아래 톱니 이음새).</summary>
        static void PillowBag(MeshKit k, Color body, Color seam, float w, float h)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.Sphere(new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.5f, h * 0.46f, 0.045f), 10, 6, body);
            k.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.92f, h * 0.78f, 0.05f), body);
            k.Box(new Vector3(0f, h - 0.012f, 0f), new Vector3(w, 0.024f, 0.012f), seam);
            k.Box(new Vector3(0f, 0.012f, 0f), new Vector3(w, 0.024f, 0.012f), seam);
            for (int i = 0; i < 6; i++) // 톱니
            {
                float x = -w * 0.5f + w * (i + 0.5f) / 6f;
                k.FlatBox(new Vector3(x, h + 0.002f, 0f), new Vector3(w / 12f, 0.008f, 0.012f), seam);
            }
            k.Pop();
        }

        // 王子麵: 흰 바탕 봉지 + 빨간 띠 + 노란 동그라미(왕자 얼굴) + 면 그림
        static void PrinceNoodle(MeshKit k)
        {
            Color white = new Color(0.98f, 0.96f, 0.92f), red = new Color(0.88f, 0.18f, 0.16f);
            PillowBag(k, white, red, 0.19f, 0.21f);
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.FlatBox(new Vector3(0f, 0.16f, 0.05f), new Vector3(0.17f, 0.045f, 0.004f), red);
            k.FlatBox(new Vector3(0f, 0.16f, 0.053f), new Vector3(0.1f, 0.016f, 0.004f), new Color(1f, 0.85f, 0.25f));
            k.DiscFacing(new Vector3(-0.045f, 0.085f, 0.053f), new Vector2(0.03f, 0.03f), Vector3.forward, 10, new Color(1f, 0.82f, 0.62f)); // 왕자 얼굴
            k.FlatBox(new Vector3(-0.045f, 0.118f, 0.054f), new Vector3(0.04f, 0.012f, 0.004f), new Color(1f, 0.8f, 0.2f));     // 왕관
            for (int i = 0; i < 3; i++) // 꼬불 면
                k.FlatBox(new Vector3(0.04f, 0.065f + i * 0.022f, 0.053f), new Vector3(0.06f, 0.01f, 0.004f), new Color(0.95f, 0.72f, 0.35f));
            k.Pop();
        }

        // 牛奶糖: 노란 작은 상자 + 빨간 띠, 옆에 종이로 싼 네모 사탕 두 개
        static void MilkCandyBox(MeshKit k)
        {
            Color yellow = new Color(1f, 0.84f, 0.3f), red = new Color(0.86f, 0.2f, 0.18f), cream = new Color(0.98f, 0.94f, 0.82f);
            k.Push(new Vector3(-0.025f, 0f, 0f), Quaternion.Euler(0f, -10f, 0f));
            k.Box(new Vector3(0f, 0.085f, 0f), new Vector3(0.11f, 0.17f, 0.05f), yellow, yellow);
            k.FlatBox(new Vector3(0f, 0.12f, 0.026f), new Vector3(0.112f, 0.035f, 0.004f), red);
            k.FlatBox(new Vector3(0f, 0.06f, 0.026f), new Vector3(0.06f, 0.05f, 0.004f), cream);
            k.DiscFacing(new Vector3(0f, 0.06f, 0.029f), new Vector2(0.016f, 0.016f), Vector3.forward, 8, new Color(0.55f, 0.35f, 0.2f));
            k.Pop();
            for (int i = 0; i < 2; i++)
            {
                k.Push(new Vector3(0.075f, 0.016f + i * 0.03f, 0.02f - i * 0.01f), Quaternion.Euler(0f, 20f + i * 35f, 0f));
                k.Box(Vector3.zero, new Vector3(0.04f, 0.028f, 0.028f), cream, cream);
                k.FlatBox(new Vector3(0f, 0f, 0.0145f), new Vector3(0.04f, 0.01f, 0.002f), red);
                k.Pop();
            }
        }

        // 乖乖: 초록 봉지 + 노란 동그라미 로고 + 빨간 글씨 띠
        static void KuaiKuai(MeshKit k)
        {
            Color green = new Color(0.22f, 0.66f, 0.30f), yellow = new Color(1f, 0.86f, 0.22f);
            PillowBag(k, green, new Color(0.15f, 0.5f, 0.22f), 0.18f, 0.22f);
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.DiscFacing(new Vector3(0f, 0.13f, 0.052f), new Vector2(0.05f, 0.05f), Vector3.forward, 12, yellow);
            k.FlatBox(new Vector3(0f, 0.13f, 0.055f), new Vector3(0.07f, 0.02f, 0.004f), new Color(0.9f, 0.2f, 0.18f));
            for (int i = 0; i < 3; i++) // 옥수수 과자 그림
                k.DiscFacing(new Vector3(-0.04f + i * 0.04f, 0.055f, 0.052f), new Vector2(0.014f, 0.01f), Vector3.forward, 6, new Color(0.98f, 0.78f, 0.35f));
            k.Pop();
        }

        // 尪仔標: 그림이 찍힌 동그란 두꺼운 종이 딱지 여러 장
        static void AngAPiau(MeshKit k)
        {
            Color[] rims = { new Color(0.9f, 0.25f, 0.22f), new Color(0.25f, 0.5f, 0.9f), new Color(0.98f, 0.8f, 0.2f), new Color(0.3f, 0.7f, 0.4f) };
            Color face = new Color(0.98f, 0.95f, 0.88f);
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3((i % 2) * 0.02f - 0.01f, 0.008f + i * 0.016f, (i % 3) * 0.012f - 0.012f);
                Color rim = rims[i % rims.Length];
                k.Cylinder(p, 0.07f, 0.014f, 14, rim, face);
                // 그림: 가운데 동그란 얼굴 + 테두리 점
                k.Cylinder(p + new Vector3(0f, 0.0075f, 0f), 0.034f, 0.002f, 10, Color.Lerp(rim, Color.white, 0.35f));
                k.Cylinder(p + new Vector3(0f, 0.009f, 0f), 0.016f, 0.002f, 8, rim);
            }
            // 맨 위 한 장은 비스듬히 세워 그림이 보이게
            k.Push(new Vector3(0f, 0.12f, -0.02f), Quaternion.Euler(-65f, 0f, 0f));
            k.Cylinder(Vector3.zero, 0.07f, 0.014f, 14, rims[0], face);
            k.Cylinder(new Vector3(0f, 0.0075f, 0f), 0.04f, 0.002f, 10, new Color(1f, 0.82f, 0.62f));
            k.Cylinder(new Vector3(-0.014f, 0.009f, 0.008f), 0.006f, 0.002f, 6, new Color(0.2f, 0.16f, 0.16f));
            k.Cylinder(new Vector3(0.014f, 0.009f, 0.008f), 0.006f, 0.002f, 6, new Color(0.2f, 0.16f, 0.16f));
            k.Cylinder(new Vector3(0f, 0.009f, -0.014f), 0.012f, 0.002f, 6, rims[0]);
            k.Pop();
        }

        // ───────────── 미국판 모양 ─────────────

        /// <summary>미국판에서 모양까지 바뀌는 상품 (태피·루트비어 배럴·치즈퍼프·밀크캡). 아니면 false.</summary>
        static bool UsShape(MeshKit k, string id)
        {
            switch (id)
            {
                case "jjondeugi": Taffy(k); return true;
                case "beercandy": RootBeerBarrels(k); return true;
                case "ssaldaerong": CheesePuffs(k); return true;
                case "ddakji": MilkCaps(k); return true;
            }
            return false;
        }

        // 태피: 파스텔 종이에 양끝을 꼰 말랑 사탕 무더기
        static void Taffy(MeshKit k)
        {
            Color[] cols = { new Color(1f, 0.7f, 0.78f), new Color(0.7f, 0.88f, 1f), new Color(1f, 0.93f, 0.6f), new Color(0.72f, 0.95f, 0.78f) };
            Vector3[] pos = { new Vector3(-0.05f, 0.03f, 0.03f), new Vector3(0.05f, 0.03f, -0.02f), new Vector3(0f, 0.03f, -0.06f), new Vector3(0f, 0.075f, 0.01f), new Vector3(-0.04f, 0.075f, -0.04f) };
            for (int i = 0; i < pos.Length; i++)
            {
                k.Push(pos[i], Quaternion.Euler(0f, i * 47f, 0f));
                k.Box(Vector3.zero, new Vector3(0.07f, 0.035f, 0.04f), cols[i % cols.Length], cols[i % cols.Length]);
                k.Push(new Vector3(0.05f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
                k.Frustum(Vector3.zero, 0.022f, 0.006f, 0.025f, 5, Paper, Paper);
                k.Pop();
                k.Push(new Vector3(-0.05f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
                k.Frustum(Vector3.zero, 0.022f, 0.006f, 0.025f, 5, Paper, Paper);
                k.Pop();
                k.Pop();
            }
        }

        // 루트비어 배럴: 갈색 통 모양 사탕 (흰 띠) 여러 개
        static void RootBeerBarrels(MeshKit k)
        {
            Color brown = new Color(0.55f, 0.30f, 0.16f), band = new Color(0.98f, 0.9f, 0.75f);
            Vector3[] pos = { new Vector3(-0.045f, 0.03f, 0.03f), new Vector3(0.045f, 0.03f, 0.01f), new Vector3(0f, 0.03f, -0.05f), new Vector3(0f, 0.085f, 0f) };
            for (int i = 0; i < pos.Length; i++)
            {
                k.Push(pos[i], Quaternion.Euler(0f, i * 40f, 90f));
                k.Sphere(Vector3.zero, new Vector3(0.032f, 0.045f, 0.032f), 8, 5, brown);
                k.Cylinder(new Vector3(0f, 0.022f, 0f), 0.031f, 0.006f, 8, band);
                k.Cylinder(new Vector3(0f, -0.022f, 0f), 0.031f, 0.006f, 8, band);
                k.Pop();
            }
        }

        // 치즈퍼프: 주황 봉지 + 노란 퍼프 그림
        static void CheesePuffs(MeshKit k)
        {
            Color orange = new Color(0.98f, 0.55f, 0.15f), yellow = new Color(1f, 0.86f, 0.3f);
            PillowBag(k, orange, new Color(0.85f, 0.35f, 0.1f), 0.18f, 0.22f);
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.DiscFacing(new Vector3(0f, 0.14f, 0.052f), new Vector2(0.06f, 0.035f), Vector3.forward, 12, yellow);
            k.FlatBox(new Vector3(0f, 0.14f, 0.055f), new Vector3(0.08f, 0.018f, 0.004f), new Color(0.85f, 0.2f, 0.15f));
            for (int i = 0; i < 3; i++)
                k.DiscFacing(new Vector3(-0.045f + i * 0.045f, 0.06f, 0.052f), new Vector2(0.018f, 0.012f), Vector3.forward, 7, new Color(1f, 0.65f, 0.2f));
            k.Pop();
        }

        // 밀크캡: 그림 찍힌 동그란 종이 뚜껑 묶음 (尪仔標와 같은 모양, 색만 미국풍)
        static void MilkCaps(MeshKit k) => AngAPiau(k);

        // ───────────── 알려진 상품 ─────────────

        // 쫀드기: 비닐 포장된 납작한 판, 뒤로 기대어 서 있음
        static void Jjondeugi(MeshKit k, Color c)
        {
            k.Push(new Vector3(0f, 0f, 0f), Quaternion.Euler(-14f, 0f, 0f));
            k.Box(new Vector3(0f, 0.10f, 0f), new Vector3(0.20f, 0.20f, 0.035f), c);
            k.Box(new Vector3(0f, 0.185f, 0.006f), new Vector3(0.205f, 0.045f, 0.03f), Paper);
            k.FlatBox(new Vector3(0f, 0.185f, 0.022f), new Vector3(0.12f, 0.018f, 0.004f), Red);
            for (int i = 0; i < 3; i++) // 쫀드기 결
                k.FlatBox(new Vector3(-0.06f + i * 0.06f, 0.08f, 0.019f), new Vector3(0.035f, 0.11f, 0.004f), D(c, 0.8f));
            k.Pop();
        }

        // 아폴로: 노란 봉지에 빨대 과자 여러 개
        static void Apollo(MeshKit k, Color c)
        {
            k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.18f, 0.12f, 0.12f), c);
            k.FlatBox(new Vector3(0f, 0.07f, 0.062f), new Vector3(0.14f, 0.04f, 0.004f), Red);
            Color[] straws = { Red, new Color(0.3f, 0.7f, 0.35f), new Color(0.3f, 0.5f, 0.9f), new Color(0.95f, 0.55f, 0.75f), Paper };
            for (int i = 0; i < 5; i++)
                k.Cylinder(new Vector3(-0.065f + i * 0.033f, 0.15f + (i % 2) * 0.02f, (i % 2) * 0.02f - 0.01f), 0.012f, 0.12f, 6, straws[i]);
        }

        // 딱지: 접은 정사각 딱지 여러 장
        static void Ddakji(MeshKit k, Color c)
        {
            Color b = Red;
            for (int i = 0; i < 4; i++)
            {
                k.Push(new Vector3(0f, 0.015f + i * 0.026f, 0f), Quaternion.Euler(0f, i * 13f - 15f, 0f));
                Color col = i % 2 == 0 ? c : b;
                k.Box(new Vector3(0f, 0f, 0f), new Vector3(0.15f, 0.024f, 0.15f), col);
                // 접은 자국 X
                k.Push(new Vector3(0f, 0.0125f, 0f), Quaternion.Euler(0f, 45f, 0f));
                k.FlatBox(Vector3.zero, new Vector3(0.19f, 0.003f, 0.012f), L(col, 0.45f));
                k.FlatBox(Vector3.zero, new Vector3(0.012f, 0.003f, 0.19f), L(col, 0.45f));
                k.Pop();
                k.Pop();
            }
        }

        // 구슬: 망사 봉지 대신 작은 쟁반에 유리구슬 무더기
        static void Marble(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.012f, 0f), 0.095f, 0.024f, 8, new Color(0.95f, 0.85f, 0.55f));
            Color[] cols = { c, new Color(0.9f, 0.35f, 0.4f), new Color(0.98f, 0.85f, 0.3f), new Color(0.4f, 0.55f, 0.95f), L(c, 0.4f) };
            Vector3[] pos =
            {
                new Vector3(-0.04f, 0.05f, 0.03f), new Vector3(0.04f, 0.05f, 0.03f), new Vector3(0f, 0.05f, -0.04f),
                new Vector3(-0.045f, 0.05f, -0.03f), new Vector3(0.045f, 0.05f, -0.035f), new Vector3(0f, 0.09f, 0f),
            };
            for (int i = 0; i < pos.Length; i++)
                k.Sphere(pos[i], Vector3.one * 0.036f, 6, 4, cols[i % cols.Length]);
        }

        // 지우개: 하얀 블록 + 종이 띠
        static void Eraser(MeshKit k, Color c)
        {
            Color band = new Color(0.30f, 0.55f, 0.90f);
            k.Push(Vector3.zero, Quaternion.Euler(0f, -8f, 0f));
            k.Box(new Vector3(0f, 0.045f, 0f), new Vector3(0.19f, 0.09f, 0.11f), c, D(c, 0.95f));
            k.Box(new Vector3(0.015f, 0.045f, 0f), new Vector3(0.09f, 0.095f, 0.115f), band);
            k.FlatBox(new Vector3(0.015f, 0.05f, 0.059f), new Vector3(0.05f, 0.02f, 0.003f), Paper);
            k.Pop();
        }

        // 스티커: 세워진 시트 + 동그란 스티커들
        static void Sticker(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.22f, 0.012f), c);
            Color[] dots = { new Color(0.98f, 0.85f, 0.3f), new Color(0.95f, 0.45f, 0.55f), new Color(0.4f, 0.8f, 0.5f), Paper };
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    k.FlatBox(new Vector3(-0.055f + x * 0.055f, 0.05f + y * 0.06f, 0.008f), new Vector3(0.04f, 0.04f, 0.004f), dots[(x + y) % dots.Length]);
            k.Pop();
        }

        // 종이인형: 키 큰 카드 + 인형 실루엣
        static void PaperDoll(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-10f, 0f, 0f));
            k.Box(new Vector3(0f, 0.125f, 0f), new Vector3(0.17f, 0.25f, 0.012f), c);
            k.FlatBox(new Vector3(0f, 0.235f, 0.007f), new Vector3(0.17f, 0.03f, 0.004f), Paper);
            k.FlatBox(new Vector3(0f, 0.175f, 0.008f), new Vector3(0.045f, 0.045f, 0.004f), new Color(1f, 0.86f, 0.74f)); // 얼굴
            k.FlatBox(new Vector3(0f, 0.195f, 0.010f), new Vector3(0.055f, 0.02f, 0.004f), Ink);                       // 머리
            k.FlatBox(new Vector3(0f, 0.10f, 0.008f), new Vector3(0.08f, 0.10f, 0.004f), new Color(0.98f, 0.98f, 1f)); // 드레스
            k.FlatBox(new Vector3(0f, 0.10f, 0.011f), new Vector3(0.05f, 0.015f, 0.004f), D(c, 0.75f));
            k.Pop();
        }

        // 미니카: 차체 + 지붕 + 바퀴 4개
        static void MiniCar(MeshKit k, Color c)
        {
            Color tire = new Color(0.15f, 0.14f, 0.15f);
            k.Push(Vector3.zero, Quaternion.Euler(0f, -25f, 0f));
            k.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.20f, 0.05f, 0.11f), c);
            k.Box(new Vector3(-0.02f, 0.09f, 0f), new Vector3(0.09f, 0.04f, 0.09f), D(c, 0.8f));
            k.FlatBox(new Vector3(-0.02f, 0.09f, 0.046f), new Vector3(0.07f, 0.025f, 0.003f), new Color(0.6f, 0.85f, 0.95f));
            k.FlatBox(new Vector3(0.10f, 0.055f, 0f), new Vector3(0.003f, 0.02f, 0.08f), new Color(1f, 0.95f, 0.6f));
            foreach (var w in new[] { new Vector3(0.06f, 0.025f, 0.058f), new Vector3(-0.06f, 0.025f, 0.058f), new Vector3(0.06f, 0.025f, -0.058f), new Vector3(-0.06f, 0.025f, -0.058f) })
            {
                k.Push(w, Quaternion.Euler(90f, 0f, 0f));
                k.Cylinder(Vector3.zero, 0.025f, 0.02f, 8, tire, new Color(0.8f, 0.8f, 0.8f));
                k.Pop();
            }
            k.Pop();
        }

        // 사탕: 비닐에 싼 동그란 사탕 두 알
        static void Candy(MeshKit k, Color c)
        {
            WrappedCandy(k, new Vector3(-0.035f, 0.035f, 0.02f), c, 20f);
            WrappedCandy(k, new Vector3(0.045f, 0.035f, -0.015f), L(c, 0.25f), -30f);
            WrappedCandy(k, new Vector3(0.005f, 0.085f, 0f), c, 70f);
        }

        static void WrappedCandy(MeshKit k, Vector3 pos, Color c, float yaw)
        {
            k.Push(pos, Quaternion.Euler(0f, yaw, 0f));
            k.Sphere(Vector3.zero, new Vector3(0.042f, 0.036f, 0.036f), 7, 4, c);
            k.Push(new Vector3(0.05f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
            k.Frustum(Vector3.zero, 0.03f, 0.008f, 0.03f, 5, Clear, Clear);
            k.Pop();
            k.Push(new Vector3(-0.05f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
            k.Frustum(Vector3.zero, 0.03f, 0.008f, 0.03f, 5, Clear, Clear);
            k.Pop();
            k.Pop();
        }

        // 연필: 육각 연필 세 자루가 세워진 묶음
        static void Pencil(MeshKit k, Color c)
        {
            Color wood = new Color(0.95f, 0.80f, 0.55f), pink = new Color(0.95f, 0.6f, 0.65f);
            Color[] cols = { c, new Color(0.95f, 0.75f, 0.2f), new Color(0.3f, 0.5f, 0.9f) };
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3(-0.05f + i * 0.05f, 0f, (i % 2) * 0.02f), Quaternion.Euler(0f, 0f, (i - 1) * -6f));
                k.Cylinder(new Vector3(0f, 0.09f, 0f), 0.018f, 0.18f, 6, cols[i], pink);
                k.Frustum(new Vector3(0f, 0.2f, 0f), 0.018f, 0.004f, 0.04f, 6, wood, Ink);
                k.Pop();
            }
            k.Box(new Vector3(0f, 0.05f, 0.01f), new Vector3(0.16f, 0.03f, 0.06f), Paper);
        }

        // 탱탱볼: 반짝이 공 + 띠
        static void BouncyBall(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.008f, 0f), 0.05f, 0.016f, 8, Paper);
            k.Sphere(new Vector3(0f, 0.075f, 0f), Vector3.one * 0.068f, 8, 6, c);
            k.Cylinder(new Vector3(0f, 0.075f, 0f), 0.07f, 0.018f, 8, new Color(0.98f, 0.9f, 0.35f));
        }

        // 요요: 세워진 원판 두 개
        static void Yoyo(MeshKit k, Color c)
        {
            k.Push(new Vector3(0f, 0.07f, 0f), Quaternion.Euler(90f, 0f, 0f));
            k.Cylinder(new Vector3(0f, 0.03f, 0f), 0.07f, 0.035f, 10, c, L(c, 0.35f));
            k.Cylinder(new Vector3(0f, -0.03f, 0f), 0.07f, 0.035f, 10, c, L(c, 0.35f));
            k.Cylinder(Vector3.zero, 0.025f, 0.03f, 6, Paper);
            k.Pop();
            k.FlatBox(new Vector3(0f, 0.07f, 0.054f), new Vector3(0.05f, 0.05f, 0.004f), new Color(0.98f, 0.85f, 0.3f));
        }

        // 쌀대롱: 봉지 + 위로 삐죽 나온 대롱 과자
        static void Ssaldaerong(MeshKit k, Color c)
        {
            Color bag = new Color(0.95f, 0.45f, 0.3f);
            for (int i = 0; i < 4; i++)
                k.Cylinder(new Vector3(-0.05f + i * 0.033f, 0.16f + (i % 2) * 0.02f, (i % 2) * 0.015f), 0.017f, 0.12f, 6, c);
            k.Box(new Vector3(0f, 0.065f, 0f), new Vector3(0.18f, 0.13f, 0.10f), bag, D(bag, 0.92f));
            k.FlatBox(new Vector3(0f, 0.07f, 0.052f), new Vector3(0.11f, 0.05f, 0.004f), Paper);
        }

        // 비눗방울: 통 + 뚜껑 + 불대
        static void Bubble(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.07f, 0f), 0.04f, 0.14f, 8, c);
            k.Cylinder(new Vector3(0f, 0.155f, 0f), 0.042f, 0.03f, 8, new Color(0.95f, 0.45f, 0.6f));
            k.Cylinder(new Vector3(0f, 0.19f, 0f), 0.006f, 0.05f, 4, new Color(0.95f, 0.45f, 0.6f));
            k.Push(new Vector3(0f, 0.225f, 0f), Quaternion.Euler(90f, 0f, 0f));
            k.Cylinder(Vector3.zero, 0.024f, 0.008f, 8, new Color(0.95f, 0.45f, 0.6f));
            k.Pop();
            k.FlatBox(new Vector3(0f, 0.07f, 0.041f), new Vector3(0.05f, 0.05f, 0.004f), Paper);
            k.Sphere(new Vector3(0.06f, 0.2f, 0.02f), Vector3.one * 0.018f, 6, 4, L(c, 0.6f));
        }

        // 캐릭터 카드: 반짝이 봉지 팩
        static void CardPack(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.Box(new Vector3(0f, 0.105f, 0f), new Vector3(0.15f, 0.21f, 0.025f), c);
            k.FlatBox(new Vector3(0f, 0.13f, 0.014f), new Vector3(0.09f, 0.09f, 0.004f), new Color(0.98f, 0.88f, 0.35f));
            k.FlatBox(new Vector3(0f, 0.13f, 0.017f), new Vector3(0.05f, 0.05f, 0.004f), Red);
            k.FlatBox(new Vector3(0f, 0.04f, 0.014f), new Vector3(0.12f, 0.02f, 0.004f), Paper);
            k.Pop();
        }

        // 번호 뽑기: 접은 종이가 붙은 작은 판
        static void Ppopgi(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-14f, 0f, 0f));
            k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.2f, 0.22f, 0.015f), c);
            k.FlatBox(new Vector3(0f, 0.195f, 0.009f), new Vector3(0.18f, 0.035f, 0.004f), new Color(0.98f, 0.85f, 0.3f));
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 5; x++)
                    if ((x + y * 5) % 7 != 3) // 뜯긴 칸 몇 개
                        k.FlatBox(new Vector3(-0.072f + x * 0.036f, 0.035f + y * 0.035f, 0.009f), new Vector3(0.028f, 0.026f, 0.004f), Paper);
            k.Pop();
        }

        // 고무줄: 둘둘 감긴 고무줄 다발
        static void RubberBand(MeshKit k, Color c)
        {
            Color[] cols = { c, new Color(0.95f, 0.5f, 0.6f), new Color(0.98f, 0.85f, 0.35f) };
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3((i - 1) * 0.012f, 0.02f + i * 0.022f, 0f), Quaternion.Euler(0f, i * 25f, 0f));
                k.Cylinder(Vector3.zero, 0.075f - i * 0.008f, 0.02f, 10, cols[i], L(cols[i], 0.3f));
                k.Pop();
            }
            k.Cylinder(new Vector3(0f, 0.08f, 0f), 0.03f, 0.008f, 8, D(c, 0.6f));
        }

        // 물총: 몸통 + 총열 + 손잡이 + 물탱크
        static void WaterGun(MeshKit k, Color c)
        {
            Color tank = new Color(0.98f, 0.8f, 0.25f);
            k.Push(new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, -20f, 0f));
            k.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.15f, 0.05f, 0.05f), c);
            k.Push(new Vector3(0.1f, 0.095f, 0f), Quaternion.Euler(0f, 0f, 90f));
            k.Cylinder(Vector3.zero, 0.012f, 0.06f, 6, D(c, 0.8f));
            k.Pop();
            k.Push(new Vector3(-0.045f, 0.04f, 0f), Quaternion.Euler(0f, 0f, -12f));
            k.Box(Vector3.zero, new Vector3(0.04f, 0.08f, 0.045f), D(c, 0.85f));
            k.Pop();
            k.Cylinder(new Vector3(0.01f, 0.145f, 0f), 0.03f, 0.06f, 8, tank);
            k.Pop();
        }

        // 전자 펫: 달걀 모양 + 화면 + 버튼
        static void VirtualPet(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.008f, 0f), 0.05f, 0.016f, 8, Paper);
            k.Sphere(new Vector3(0f, 0.085f, 0f), new Vector3(0.065f, 0.075f, 0.035f), 8, 6, c);
            k.FlatBox(new Vector3(0f, 0.095f, 0.033f), new Vector3(0.05f, 0.04f, 0.006f), new Color(0.75f, 0.82f, 0.68f));
            k.FlatBox(new Vector3(0f, 0.098f, 0.037f), new Vector3(0.018f, 0.016f, 0.003f), Ink);
            for (int i = 0; i < 3; i++)
                k.Cylinder(new Vector3(-0.02f + i * 0.02f, 0.05f, 0.03f), 0.007f, 0.012f, 5, Red);
            k.Cylinder(new Vector3(0f, 0.17f, 0f), 0.012f, 0.012f, 6, new Color(0.8f, 0.8f, 0.82f));
        }

        // 액체괴물: 통 + 형광 괴물이 볼록
        static void Slime(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.045f, 0f), 0.065f, 0.09f, 8, Clear, c);
            k.Sphere(new Vector3(0f, 0.09f, 0f), new Vector3(0.06f, 0.035f, 0.06f), 8, 4, c);
            k.Cylinder(new Vector3(0f, 0.04f, 0f), 0.067f, 0.035f, 8, c);
            k.Push(new Vector3(0.07f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 70f));
            k.Cylinder(Vector3.zero, 0.04f, 0.012f, 8, new Color(0.95f, 0.45f, 0.6f)); // 뚜껑(옆에 기대 둠)
            k.Pop();
        }

        // 공책: 세워진 노트
        static void Notebook(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.17f, 0.22f, 0.02f), c);
            k.FlatBox(new Vector3(0f, 0.14f, 0.012f), new Vector3(0.11f, 0.05f, 0.004f), Paper);
            k.FlatBox(new Vector3(-0.08f, 0.11f, 0.012f), new Vector3(0.012f, 0.22f, 0.004f), D(c, 0.6f));
            k.Pop();
        }

        // ───────────── 기본 모양 ─────────────

        static void GenericBox(MeshKit k, Color c)
        {
            k.Box(new Vector3(0f, 0.075f, 0f), new Vector3(0.18f, 0.15f, 0.13f), c, D(c, 0.95f));
            k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.185f, 0.035f, 0.135f), Paper);
            k.FlatBox(new Vector3(0f, 0.055f, 0.066f), new Vector3(0.07f, 0.05f, 0.004f), L(c, 0.5f));
        }

        static void GenericBag(MeshKit k, Color c)
        {
            k.Sphere(new Vector3(0f, 0.08f, 0f), new Vector3(0.095f, 0.08f, 0.06f), 8, 5, c);
            k.Box(new Vector3(0f, 0.165f, 0f), new Vector3(0.15f, 0.025f, 0.03f), D(c, 0.8f));
            k.FlatBox(new Vector3(0f, 0.085f, 0.058f), new Vector3(0.07f, 0.045f, 0.004f), Paper);
        }

        static void GenericTub(MeshKit k, Color c)
        {
            k.Cylinder(new Vector3(0f, 0.065f, 0f), 0.06f, 0.13f, 8, c, L(c, 0.3f));
            k.Cylinder(new Vector3(0f, 0.14f, 0f), 0.064f, 0.025f, 8, Paper);
            k.FlatBox(new Vector3(0f, 0.065f, 0.058f), new Vector3(0.06f, 0.05f, 0.004f), Paper);
        }

        static void GenericCard(MeshKit k, Color c)
        {
            k.Push(Vector3.zero, Quaternion.Euler(-12f, 0f, 0f));
            k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.17f, 0.22f, 0.015f), c);
            k.FlatBox(new Vector3(0f, 0.2f, 0.009f), new Vector3(0.17f, 0.03f, 0.004f), Paper);
            k.FlatBox(new Vector3(0f, 0.1f, 0.009f), new Vector3(0.1f, 0.1f, 0.004f), L(c, 0.5f));
            k.Pop();
        }
    }
}
