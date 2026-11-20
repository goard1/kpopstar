using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Data
{
    [CreateAssetMenu(menuName = "문방구/상품 목록", fileName = "ProductCatalog")]
    public class ProductCatalog : ScriptableObject
    {
        public List<ProductData> products = new List<ProductData>();

        public ProductData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return products.Find(p => p != null && p.id == id);
        }
    }
}
