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
        public float margin = 1.02f;

        // HUD가 가리는 비율 (1920 높이 기준 상단 ~200px, 하단 창고 바+버튼 ~430px). 화면 비율이 달라도 비율로 계산.
        const float TopUiFrac = 0.105f;
        const float BottomUiFrac = 0.225f;

        public const float MaxZoom = 2.2f;

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

            // 상단 바/하단 UI를 뺀 "보이는 영역"의 높이 비율 (화면 높이 기준). 핵심 영역(focus)이 여기에 다 들어오도록 맞춘다.
            float visible = Mathf.Clamp(1f - TopUiFrac - BottomUiFrac, 0.3f, 1f);
            float aspect = Mathf.Max(0.01f, cam.aspect);
            // 세로 전체 크기(2*size)의 visible 비율이 focus 높이, 가로는 화면 폭 전체
            float sizeByH = (maxY - minY) * 0.5f / visible;
            float sizeByW = (maxX - minX) * 0.5f / aspect;
            baseSize = Mathf.Max(sizeByH, sizeByW) * margin;

            // 보이는 영역의 중심은 화면 중심보다 (BottomUiFrac - TopUiFrac)/2 만큼 위. 가게 중심을 거기에 놓는다.
            float shift = (BottomUiFrac - TopUiFrac) * 0.5f;
            Vector3 centerOffset = transform.rotation * new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            basePos = c + centerOffset - transform.forward * 30f - transform.up * (baseSize * 2f * shift);
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
