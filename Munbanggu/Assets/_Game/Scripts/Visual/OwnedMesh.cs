using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>오브젝트가 사라질 때 직접 만든 메시도 같이 정리 (손님·매대 상품처럼 코드로 만든 메시용).</summary>
    public class OwnedMesh : MonoBehaviour
    {
        public Mesh mesh;

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
