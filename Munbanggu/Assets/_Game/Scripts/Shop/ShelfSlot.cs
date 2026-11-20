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

        /// <summary>손님이 서서 고르는 위치 (매대 앞).</summary>
        public Vector3 StandPoint => transform.TransformPoint(new Vector3(0f, 0f, 0.85f));

        // 가격표 위치 (매대 앞 레일, 로컬)
        static readonly Vector3 TagPos = new Vector3(0f, 0.56f, 0.39f);
        static readonly Color TagPaper = new Color(0.99f, 0.97f, 0.92f);
        static readonly Color TagEmpty = new Color(0.78f, 0.74f, 0.70f);
        static readonly Color TagSoldOut = new Color(0.88f, 0.22f, 0.20f);
        static readonly Color Ink = new Color(0.25f, 0.2f, 0.2f);
        static readonly MeshKit kit = new MeshKit();
        static readonly Vector3 ItemScale = Vector3.one * 1.15f; // 숏폼 화면에서 잘 보이게 살짝 크게

        GameObject[] items;
        MaterialCache mats;
        Mesh goodsMesh;

        public void Init(int index, GameObject[] itemVisuals, MaterialCache materials)
        {
            Index = index;
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
            Refresh();
        }

        /// <summary>같은 상품만 추가. 실제로 올린 개수를 돌려줌.</summary>
        public int Add(ProductData p, int amount)
        {
            if (p == null || (Product != null && Product != p)) return 0;
            Product = p;
            int n = Mathf.Clamp(amount, 0, Capacity - Count);
            Count += n;
            Refresh();
            return n;
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
            var data = ProductVisuals.DataFor(Product);
            kit.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                bool on = Product != null && i < Count;
                if (items[i] == null) continue;
                items[i].SetActive(on);
                if (!on || data == null) continue;
                var t = items[i].transform;
                kit.Push(t.localPosition, t.localRotation, ItemScale);
                kit.Append(data);
                kit.Pop();
            }
            BuildTag();
            if (goodsMesh != null) kit.Fill(goodsMesh);
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
