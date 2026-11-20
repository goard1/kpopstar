using Munbanggu.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Munbanggu.Core
{
    /// <summary>Main 씬의 유일한 오브젝트. 가게·UI·게임 로직을 전부 코드로 만든다.</summary>
    public class Bootstrap : MonoBehaviour
    {
        public ProductCatalog catalog;
        public Material baseMaterial;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.58f, 0.54f);

            if (catalog == null || catalog.products.Count == 0)
            {
                Debug.LogError("[문방구] 상품 목록이 비어 있습니다. 메뉴 '문방구 > 프로젝트 초기 설정'을 실행하세요.");
                return;
            }
            gameObject.AddComponent<GameManager>().Init(catalog, baseMaterial);
        }
    }
}
