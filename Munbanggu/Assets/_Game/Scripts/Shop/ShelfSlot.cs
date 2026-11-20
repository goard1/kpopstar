using Munbanggu.Core;
using Munbanggu.Data;
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

        GameObject[] items;
        MaterialCache mats;

        public void Init(int index, GameObject[] itemVisuals, MaterialCache materials)
        {
            Index = index;
            items = itemVisuals;
            mats = materials;
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
            for (int i = 0; i < items.Length; i++)
            {
                bool on = Product != null && i < Count;
                items[i].SetActive(on);
                if (on) mats.Apply(items[i], Product.color);
            }
        }
    }
}
