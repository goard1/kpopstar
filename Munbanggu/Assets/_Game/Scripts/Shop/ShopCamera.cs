using UnityEngine;

namespace Munbanggu.Shop
{
    /// <summary>
    /// 쿼터뷰 카메라. 기본은 가게가 가로로 꽉 차는 크기(Fit),
    /// 거기서 손가락 두 개로 확대(최대 MaxZoom배)하고 한 손가락으로 끌어 옮길 수 있다.
    /// 확대한 화면은 기본 화면 바깥으로 나가지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ShopCamera : MonoBehaviour
    {
        public Bounds focus;
        public float margin = 1.06f;
        [Tooltip("아래쪽 UI가 더 두꺼워서 가게를 화면 위쪽으로 살짝 올림 (화면 높이 비율)")]
        public float lift = 0.06f;

        public const float MaxZoom = 2.6f;

        Camera cam;
        int lastW, lastH;
        float baseSize;       // 확대 1배일 때 orthographicSize
        Vector3 basePos;      // 확대 1배일 때 카메라 위치
        float zoom = 1f;
        Vector2 pan;          // 카메라 평면(오른쪽, 위쪽) 기준 이동량 (월드 단위)

        public float Zoom => zoom;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (Screen.width != lastW || Screen.height != lastH) Fit();
        }

        public void Fit()
        {
            if (cam == null) cam = GetComponent<Camera>();
            lastW = Screen.width;
            lastH = Screen.height;

            Quaternion inv = Quaternion.Inverse(transform.rotation);
            Vector3 c = focus.center, e = focus.extents;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var p = inv * (corner - c);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            float aspect = Mathf.Max(0.01f, cam.aspect);
            baseSize = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect) * margin;

            Vector3 centerOffset = transform.rotation * new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            basePos = c + centerOffset - transform.forward * 30f - transform.up * (baseSize * 2f * lift);
            Apply();
        }

        /// <summary>화면 좌표 screenPos 아래의 지점을 고정한 채로 factor배 확대/축소.</summary>
        public void ZoomAt(Vector2 screenPos, float factor)
        {
            if (baseSize <= 0f) return;
            Vector2 before = ScreenToPlane(screenPos);
            zoom = Mathf.Clamp(zoom * factor, 1f, MaxZoom);
            Apply();
            Vector2 after = ScreenToPlane(screenPos);
            pan += before - after;
            Apply();
        }

        /// <summary>손가락이 screenDelta(px)만큼 움직였을 때 화면을 따라 옮긴다.</summary>
        public void PanBy(Vector2 screenDelta)
        {
            if (zoom <= 1.001f || Screen.height <= 0) return;
            float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
            pan -= screenDelta * unitsPerPixel;
            Apply();
        }

        public void ResetView()
        {
            zoom = 1f;
            pan = Vector2.zero;
            Apply();
        }

        /// <summary>화면 좌표 → 카메라 평면 좌표 (카메라 중심 기준, 월드 단위).</summary>
        Vector2 ScreenToPlane(Vector2 screenPos)
        {
            float unitsPerPixel = cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);
            Vector2 fromCenter = screenPos - new Vector2(Screen.width, Screen.height) * 0.5f;
            return pan + fromCenter * unitsPerPixel;
        }

        void Apply()
        {
            cam.orthographicSize = baseSize / zoom;
            // 확대된 화면이 기본 화면 안에 머물도록 이동 범위 제한
            float aspect = Mathf.Max(0.01f, cam.aspect);
            float limitY = baseSize - cam.orthographicSize;
            float limitX = limitY * aspect;
            pan.x = Mathf.Clamp(pan.x, -limitX, limitX);
            pan.y = Mathf.Clamp(pan.y, -limitY, limitY);
            transform.position = basePos + transform.right * pan.x + transform.up * pan.y;
        }
    }
}
