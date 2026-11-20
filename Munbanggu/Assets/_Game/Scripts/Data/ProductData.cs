using UnityEngine;

namespace Munbanggu.Data
{
    /// <summary>상품 분류 (도감·유행 연출용).</summary>
    public enum ProductCategory
    {
        Snack,      // 불량식품·과자
        Stationery, // 문구
        Toy,        // 장난감
        Play        // 놀이·뽑기·수집
    }

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

        [Header("분류·해금")]
        public ProductCategory category = ProductCategory.Toy;
        [Tooltip("한국 시그니처 상품 (해외 유저용 도감 설명 대상)")]
        public bool korean;
        [Tooltip("이 날짜(일차)부터 도매상에 나옴. 1 = 처음부터")]
        [Min(1)] public int unlockDay = 1;
        [Tooltip("도감용 한 줄 설명")]
        [TextArea] public string blurb;

        [Header("그레이박스 표시")]
        public Color color = Color.white;

        public int BoxCost => wholesalePrice * boxSize;
        public int Margin => retailPrice - wholesalePrice;
    }
}
