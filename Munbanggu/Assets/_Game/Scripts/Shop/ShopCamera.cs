using UnityEngine;

namespace Munbanggu.Shop
{
    /// <summary>고정 쿼터뷰. 화면 비율이 바뀌면 가게가 가로로 꽉 차게 다시 맞춘다.</summary>
    [RequireComponent(typeof(Camera))]
    public class ShopCamera : MonoBehaviour
    {
        public Bounds focus;
        public float margin = 1.06f;
        [Tooltip("아래쪽 UI가 더 두꺼워서 가게를 화면 위쪽으로 살짝 올림 (화면 높이 비율)")]
        public float lift = 0.06f;

        Camera cam;
        int lastW, lastH;

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
            cam.orthographicSize = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect) * margin;

            Vector3 centerOffset = transform.rotation * new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            transform.position = c + centerOffset - transform.forward * 30f
                                 - transform.up * (cam.orthographicSize * 2f * lift);
        }
    }
}
