using Munbanggu.Core;
using Munbanggu.Shop;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>손님 외형 지정. 콘텐츠 쪽(단골 등)이 채우고, 비주얼 쪽이 해석한다.</summary>
    public struct CustomerLook
    {
        public int seed;          // 같은 seed = 같은 외형
        public bool isRegular;    // 단골이면 눈에 띄게
        public string regularId;  // 단골 고유 ID (없으면 null)
    }

    /// <summary>손님 3D 외형 생성. 반환한 Transform은 Customer가 걷기 흔들림에 쓴다.</summary>
    public static class CustomerVisual
    {
        public static Transform Build(Transform parent, MaterialCache mats, CustomerLook look)
        {
            var rng = new System.Random(look.seed);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);
            Color shirt = Color.HSVToRGB((float)rng.NextDouble(), 0.45f, 0.95f);
            Color bag = Color.HSVToRGB((float)rng.NextDouble(), 0.6f, 0.75f);

            ShopBuilder.Part(PrimitiveType.Capsule, visual, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.42f, shirt, mats);
            ShopBuilder.Part(PrimitiveType.Sphere, visual, new Vector3(0f, 1.0f, 0f), Vector3.one * 0.36f, new Color(1f, 0.85f, 0.7f), mats);
            ShopBuilder.Part(PrimitiveType.Cube, visual, new Vector3(0f, 0.5f, -0.2f), new Vector3(0.3f, 0.35f, 0.14f), bag, mats);
            if (look.isRegular || rng.NextDouble() < 0.5) // 모자 (단골은 항상)
                ShopBuilder.Part(PrimitiveType.Cube, visual, new Vector3(0f, 1.17f, 0.03f), new Vector3(0.34f, 0.08f, 0.4f),
                    look.isRegular ? new Color(0.9f, 0.2f, 0.2f) : bag, mats);
            return visual;
        }
    }
}
