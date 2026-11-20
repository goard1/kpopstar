using UnityEngine;

namespace Munbanggu.UI
{
    /// <summary>캔버스의 UI를 3D 오브젝트 위에 붙여 따라가게 함 (오버레이·카메라 캔버스 모두 지원).</summary>
    public class WorldFollower : MonoBehaviour
    {
        public Transform target;
        public Vector3 worldOffset;
        public Camera cam;
        /// <summary>화면 기준 추가 오프셋 (캔버스 단위 아님, 픽셀).</summary>
        public Vector2 screenOffset;

        Canvas canvas;

        void LateUpdate() => Snap();

        /// <summary>지금 위치로 즉시 맞춤.</summary>
        public void Snap()
        {
            if (target == null || cam == null) return;
            Vector3 p = cam.WorldToScreenPoint(target.position + worldOffset);
            p.x += screenOffset.x;
            p.y += screenOffset.y;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                transform.position = new Vector3(p.x, p.y, 0f);
                return;
            }
            var parent = transform.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, p, canvas.worldCamera, out var world))
                transform.position = world;
        }
    }
}
