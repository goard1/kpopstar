using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Shop
{
    /// <summary>매대 한 칸. 한 종류의 상품만 올라간다.</summary>
    public class ShelfSlot : MonoBehaviour
    {
        public int Index { get; private set; }
        public ProductData Product { get; private set; }
        public int Count { get; private set; }
        public int Capacity => items.Length;

        /// <summary>가게 앞 번호 뽑기판이면 true (번호 뽑기만 걸 수 있고, 손님이 그 자리에서 뽑는다).</summary>
        public bool IsBoard { get; private set; }
        /// <summary>뽑기판에 1등이 아직 남아 있는지 (새 판을 걸면 다시 생김).</summary>
        public bool GrandPrizeLeft { get; private set; }

        /// <summary>손님이 서서 고르는 위치 (매대 앞).</summary>
        /// <summary>처음 놓인 자리 (진열대 옮기기 "처음대로"용).</summary>
        public Vector3 DefaultPos;
        public float DefaultYaw;

        public Vector3 StandPoint => transform.TransformPoint(new Vector3(0f, 0f, IsBoard ? 0.7f : 0.85f));

        // 가격표 위치 (매대 앞 레일, 로컬)
        static readonly Vector3 TagPos = new Vector3(0f, 0.56f, 0.39f);
        static readonly Color TagPaper = new Color(0.99f, 0.97f, 0.92f);
        static readonly Color TagEmpty = new Color(0.78f, 0.74f, 0.70f);
        static readonly Color TagSoldOut = new Color(0.88f, 0.22f, 0.20f);
        static readonly Color Ink = new Color(0.25f, 0.2f, 0.2f);
        static readonly MeshKit kit = new MeshKit();
        static readonly Vector3 ItemScale = Vector3.one * 1.15f; // 숏폼 화면에서 잘 보이게 살짝 크게
        // 매대 상품 윗면 높이 (앞줄 → 뒷줄, ShopDecor.ShelfCabinet 계단과 맞춤)
        static readonly float[] TierTop = { 0.68f, 0.80f, 0.92f };

        /// <summary>
        /// 상품 자리(앵커)를 capacity개로 다시 깐다: 3줄 계단 × (capacity/3)칸. 칸이 많을수록 촘촘·작게.
        /// 이미 올려 둔 개수는 그대로 (줄어들면 잘림). 매대 넓히기 업그레이드용.
        /// </summary>
        public void SetCapacity(int capacity)
        {
            if (IsBoard || items == null || capacity == items.Length) return;
            foreach (var it in items) if (it != null) DestroySafe(it);
            items = CreateAnchors(transform, capacity, Index);
            Count = Mathf.Min(Count, Capacity);
            Refresh();
        }

        /// <summary>매대 상품 자리 만들기 (ShopBuilder와 SetCapacity가 같이 씀).</summary>
        public static GameObject[] CreateAnchors(Transform parent, int capacity, int seed)
        {
            int cols = Mathf.Max(1, capacity / 3);
            float span = 0.78f;
            float step = cols > 1 ? span / (cols - 1) : 0f;
            var items = new GameObject[cols * 3];
            var rng = new System.Random(seed * 97 + 13);
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % cols, row = i / cols;
                var item = new GameObject($"Item {i}");
                item.transform.SetParent(parent, false);
                item.transform.localPosition = new Vector3(-span * 0.5f + col * step + ((float)rng.NextDouble() - 0.5f) * 0.02f,
                                                           TierTop[row], 0.2f - row * 0.2f);
                item.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 18f, 0f);
                items[i] = item;
            }
            return items;
        }

        /// <summary>칸이 많아지면 상품을 조금 작게 (4칸 1.15 → 6칸 0.9).</summary>
        Vector3 CurrentItemScale => items == null || items.Length <= 12 ? ItemScale : Vector3.one * (items.Length <= 15 ? 1.0f : 0.9f);

        static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        GameObject[] items;
        MaterialCache mats;
        Mesh goodsMesh;

        public void Init(int index, GameObject[] itemVisuals, MaterialCache materials, bool isBoard = false)
        {
            Index = index;
            IsBoard = isBoard;
            items = itemVisuals;
            mats = materials;

            // 상품 12개는 메시 하나로 합쳐 그린다 (드로우콜 절약). items는 놓일 자리(앵커)로 사용.
            foreach (var it in items)
            {
                var r = it != null ? it.GetComponent<Renderer>() : null;
                if (r != null) r.enabled = false;
            }
            var goods = new GameObject("Goods");
            goods.transform.SetParent(transform, false);
            goodsMesh = new Mesh { name = $"Goods {index}" };
            goodsMesh.MarkDynamic();
            goods.AddComponent<MeshFilter>().sharedMesh = goodsMesh;
            goods.AddComponent<OwnedMesh>().mesh = goodsMesh;
            var mr = goods.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats.VertexColor;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            Refresh();
        }

        public void Load(ProductData p, int count)
        {
            Product = p;
            Count = p == null ? 0 : Mathf.Clamp(count, 0, Capacity);
            GrandPrizeLeft = IsBoard && Count > 0;
            Refresh();
        }

        /// <summary>같은 상품만 추가. 실제로 올린 개수를 돌려줌.</summary>
        public int Add(ProductData p, int amount)
        {
            if (p == null || (Product != null && Product != p)) return 0;
            Product = p;
            int n = Mathf.Clamp(amount, 0, Capacity - Count);
            if (IsBoard && Count == 0 && n > 0) GrandPrizeLeft = true; // 빈 판에 새로 걸면 새 판 (1등 다시 있음)
            Count += n;
            Refresh();
            return n;
        }

        /// <summary>
        /// 뽑기판에서 방금 한 장 뜯었을 때 결과 등수 (0 = 꽝, 1 = 1등 … 4 = 4등).
        /// 1등은 판마다 한 장뿐 → 남은 장 수 중 1장 확률. 나머지는 고정 확률.
        /// </summary>
        public int RollPrize()
        {
            int before = Count + 1; // TakeOne 전 장 수
            if (GrandPrizeLeft && Random.value < 1f / Mathf.Max(1, before))
            {
                GrandPrizeLeft = false;
                Refresh();
                return 1;
            }
            float r = Random.value;
            return r < 0.07f ? 2 : r < 0.2f ? 3 : r < 0.45f ? 4 : 0;
        }

        /// <summary>손님이 하나 집어감. 상품 종류는 기억(품절 표시용).</summary>
        public bool TakeOne()
        {
            if (Count <= 0) return false;
            Count--;
            Refresh();
            return true;
        }

        public (ProductData product, int count) Clear()
        {
            var result = (Product, Count);
            Product = null;
            Count = 0;
            Refresh();
            return result;
        }

        void Refresh()
        {
            if (items == null) return;
            kit.Clear();
            if (IsBoard)
            {
                BuildBoard();
                if (goodsMesh != null) kit.Fill(goodsMesh);
                return;
            }
            var data = ProductVisuals.DataFor(Product);
            for (int i = 0; i < items.Length; i++)
            {
                bool on = Product != null && i < Count;
                if (items[i] == null) continue;
                items[i].SetActive(on);
                if (!on || data == null) continue;
                var t = items[i].transform;
                kit.Push(t.localPosition, t.localRotation, CurrentItemScale);
                kit.Append(data);
                kit.Pop();
            }
            BuildTag();
            if (goodsMesh != null) kit.Fill(goodsMesh);
        }

        // ───────────── 번호 뽑기판 ─────────────

        public const int BoardCols = 6, BoardRows = 5; // 30장
        static readonly Color BoardWood = new Color(0.55f, 0.36f, 0.22f);
        static readonly Color BoardRed = new Color(0.86f, 0.24f, 0.22f);
        static readonly Color BoardYellow = new Color(1f, 0.82f, 0.22f);
        static readonly Color BoardPaper = new Color(0.98f, 0.95f, 0.86f);
        static readonly Color[] TabColors =
        {
            new Color(1f, 0.72f, 0.72f), new Color(0.72f, 0.86f, 1f), new Color(1f, 0.92f, 0.6f),
            new Color(0.74f, 0.94f, 0.76f), new Color(0.88f, 0.78f, 1f),
        };

        /// <summary>
        /// 나무 다리 두 개 + 빨간 테두리 판 + 위쪽 상품 진열(1등 대왕 로봇·2~4등 선물) + 종이 뽑기 30장.
        /// 남은 장 수만큼 종이가 붙어 있다. 로컬 앞=+Z (손님·카메라 쪽).
        /// </summary>
        void BuildBoard()
        {
            // 다리
            kit.Box(new Vector3(-0.52f, 0.8f, -0.06f), new Vector3(0.07f, 1.6f, 0.07f), BoardWood, BoardWood);
            kit.Box(new Vector3(0.52f, 0.8f, -0.06f), new Vector3(0.07f, 1.6f, 0.07f), BoardWood, BoardWood);
            // 판 (빨간 테두리 + 종이 바탕)
            kit.Box(new Vector3(0f, 0.98f, 0f), new Vector3(1.06f, 0.66f, 0.05f), BoardRed, BoardRed);
            kit.FlatBox(new Vector3(0f, 0.95f, 0.027f), new Vector3(0.94f, 0.5f, 0.004f), BoardPaper);
            // 위쪽 선물 칸 (노란 띠)
            kit.Box(new Vector3(0f, 1.42f, 0f), new Vector3(1.06f, 0.3f, 0.05f), BoardYellow, BoardYellow);
            // 1등 대왕 로봇 (뽑혀 가면 빈자리)
            if (GrandPrizeLeft)
            {
                var c = new Vector3(0f, 1.42f, 0.06f);
                kit.Box(c + new Vector3(0f, -0.02f, 0f), new Vector3(0.16f, 0.16f, 0.08f), new Color(0.35f, 0.55f, 0.95f), new Color(0.25f, 0.4f, 0.75f));
                kit.Box(c + new Vector3(0f, 0.11f, 0f), new Vector3(0.11f, 0.09f, 0.08f), new Color(0.75f, 0.78f, 0.85f), new Color(0.55f, 0.58f, 0.65f));
                kit.FlatBox(c + new Vector3(-0.025f, 0.115f, 0.042f), new Vector3(0.02f, 0.02f, 0.004f), BoardYellow);
                kit.FlatBox(c + new Vector3(0.025f, 0.115f, 0.042f), new Vector3(0.02f, 0.02f, 0.004f), BoardYellow);
                kit.Box(c + new Vector3(-0.11f, -0.02f, 0f), new Vector3(0.05f, 0.12f, 0.05f), BoardRed, BoardRed);
                kit.Box(c + new Vector3(0.11f, -0.02f, 0f), new Vector3(0.05f, 0.12f, 0.05f), BoardRed, BoardRed);
            }
            else
            {
                kit.FlatBox(new Vector3(0f, 1.42f, 0.027f), new Vector3(0.2f, 0.2f, 0.004f), new Color(0.8f, 0.72f, 0.5f));
            }
            // 2~4등 작은 선물 상자들
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? -0.38f : 0.26f) + (i % 2) * 0.12f;
                kit.Box(new Vector3(x, 1.38f, 0.05f), new Vector3(0.08f, 0.08f, 0.06f), TabColors[i], TabColors[i]);
            }
            // 종이 뽑기 (남은 장 수만큼, 위 줄부터 채움)
            const float w = 0.13f, h = 0.08f, gapX = 0.15f, gapY = 0.095f;
            for (int i = 0; i < Capacity; i++)
            {
                if (Product == null || i >= Count) continue;
                int col = i % BoardCols, row = i / BoardCols;
                var pos = new Vector3((col - (BoardCols - 1) * 0.5f) * gapX, 1.14f - row * gapY, 0.034f);
                kit.Box(pos, new Vector3(w, h, 0.008f), TabColors[(col + row * 2) % TabColors.Length], TabColors[(col + row * 2) % TabColors.Length]);
            }
        }

        /// <summary>가격표: 흰 카드 + 상품색 띠 (비었으면 회색, 다 팔렸으면 빨간 '품절' 띠).</summary>
        void BuildTag()
        {
            Color band = Product == null ? TagEmpty : Count == 0 ? TagSoldOut : Product.color;
            kit.Push(TagPos, Quaternion.Euler(-8f, 0f, 0f));
            kit.Box(Vector3.zero, new Vector3(0.30f, 0.12f, 0.012f), TagPaper);
            kit.FlatBox(new Vector3(0f, 0.032f, 0.007f), new Vector3(0.30f, 0.04f, 0.004f), band);
            if (Product != null)
            {
                kit.FlatBox(new Vector3(-0.05f, -0.02f, 0.007f), new Vector3(0.14f, 0.02f, 0.004f), Ink);
                kit.FlatBox(new Vector3(0.09f, -0.02f, 0.007f), new Vector3(0.05f, 0.02f, 0.004f), Ink);
            }
            if (Product != null && Count == 0) // 품절: 대각선 띠
            {
                kit.Push(new Vector3(0f, 0f, 0.012f), Quaternion.Euler(0f, 0f, 18f));
                kit.FlatBox(Vector3.zero, new Vector3(0.34f, 0.035f, 0.004f), TagSoldOut);
                kit.Pop();
            }
            kit.Pop();
        }
    }
}
