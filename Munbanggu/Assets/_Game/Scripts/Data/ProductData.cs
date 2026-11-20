using UnityEngine;

namespace Munbanggu.Data
{
    /// <summary>상품 하나의 정의. 값은 에디터에서 바로 조정 가능.</summary>
    [CreateAssetMenu(menuName = "문방구/상품", fileName = "Product")]
    public class ProductData : ScriptableObject
    {
        [Tooltip("세이브에 쓰이는 고유 ID. 출시 후엔 바꾸지 말 것")]
        public string id;
        public string displayName;

        [Header("가격")]
        [Min(0)] public int wholesalePrice = 50;   // 개당 도매가
        [Min(0)] public int retailPrice = 100;     // 개당 판매가
        [Min(1)] public int boxSize = 10;          // 한 번에 사는 수량

        [Header("수요")]
        [Range(0.1f, 5f)] public float popularity = 1f;

        [Header("그레이박스 표시")]
        public Color color = Color.white;

        public int BoxCost => wholesalePrice * boxSize;
    }
}
