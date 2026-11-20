using UnityEngine;

namespace Munbanggu.UI
{
    /// <summary>오버레이 캔버스의 UI를 3D 오브젝트 위에 붙여 따라가게 함.</summary>
    public class WorldFollower : MonoBehaviour
    {
        public Transform target;
        public Vector3 worldOffset;
        public Camera cam;

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            Vector3 p = cam.WorldToScreenPoint(target.position + worldOffset);
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }
}
