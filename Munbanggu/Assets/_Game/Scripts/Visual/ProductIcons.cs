using System.Collections.Generic;
using Munbanggu.Data;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 상품 UI 아이콘. 매대에 놓이는 3D 모양(ProductVisuals)을 숨은 카메라로 한 번 찍어서 스프라이트로 만든다.
    /// 흰 테두리를 둘러 레트로 스티커처럼 보이게 한다. 상품별 캐시.
    /// 씬 조명(Sun)이 만들어진 뒤 Init → Prewarm 순서로 부른다 (GameManager.Init).
    /// </summary>
    public static class ProductIcons
    {
        const int Size = 160;          // 아이콘 해상도 (px)
        const int Border = 6;          // 흰 스티커 테두리 두께 (px)
        const int Layer = 31;          // 아이콘 촬영 전용 레이어 (씬의 다른 것과 안 섞이게)
        const float Fill = 0.80f;      // 테두리 안쪽에서 상품이 차지하는 비율
        static readonly Vector3 StagePos = new Vector3(0f, -200f, 0f);
        static readonly Color Key = new Color(1f, 0f, 1f, 0f); // 배경 (알파가 안 남는 환경이면 이 색을 빼냄)

        static readonly Dictionary<ProductData, Sprite> cache = new Dictionary<ProductData, Sprite>();
        static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            material = null;
        }

        public static void Init(Material vertexColorMaterial) => material = vertexColorMaterial;

        /// <summary>상품 아이콘 (없으면 그 자리에서 찍음). 상품이 null이면 null.</summary>
        public static Sprite Get(ProductData p)
        {
            if (p == null) return null;
            if (cache.TryGetValue(p, out var s) && s != null) return s;
            Prewarm(new[] { p });
            cache.TryGetValue(p, out s);
            return s;
        }

        /// <summary>여러 상품을 한 번에 찍어 둔다 (카메라·렌더 텍스처를 한 번만 만든다).</summary>
        public static void Prewarm(IEnumerable<ProductData> products)
        {
            if (material == null) return;
            var todo = new List<ProductData>();
            foreach (var p in products)
                if (p != null && (!cache.TryGetValue(p, out var s) || s == null) && !todo.Contains(p)) todo.Add(p);
            if (todo.Count == 0) return;

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("Icon Camera") { hideFlags = HideFlags.HideAndDontSave };
            var stage = new GameObject("Icon Stage") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            var prevActive = RenderTexture.active;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Key;
                cam.cullingMask = 1 << Layer;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                cam.targetTexture = rt;
                // 살짝 위에서 내려다보는 3/4 각도. 씬 조명이 입구(-Z) 쪽에서 오므로 상품 앞면이 -Z를 보게 놓는다.
                cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

                stage.transform.position = StagePos;
                stage.transform.rotation = Quaternion.Euler(0f, 158f, 0f);
                var mf = stage.AddComponent<MeshFilter>();
                var mr = stage.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                foreach (var p in todo)
                {
                    var mesh = ProductVisuals.MeshFor(p);
                    if (mesh == null) continue;
                    mf.sharedMesh = mesh;
                    Frame(cam, stage.transform, mesh.bounds);
                    cam.Render();
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                    read.Apply(false);
                    cache[p] = MakeSticker(read.GetPixels32(), p.id);
                }
                DestroySafe(read);
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                DestroySafe(camGo);
                DestroySafe(stage);
            }
        }

        /// <summary>상품 바운드가 테두리 안쪽에 꽉 차게 카메라 크기·위치를 맞춘다.</summary>
        static void Frame(Camera cam, Transform stage, Bounds local)
        {
            Vector3 center = stage.TransformPoint(local.center);
            Quaternion inv = Quaternion.Inverse(cam.transform.rotation);
            float half = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 v = inv * (stage.TransformPoint(corner) - center);
                half = Mathf.Max(half, Mathf.Abs(v.x), Mathf.Abs(v.y));
            }
            float inner = (Size - Border * 2f - 4f) / Size; // 테두리 자리 남기기
            cam.orthographicSize = Mathf.Max(0.02f, half / (Fill * inner));
            cam.transform.position = center - cam.transform.forward * 10f;
        }

        /// <summary>배경을 투명하게 하고 흰 테두리를 둘러 스프라이트로 만든다.</summary>
        static Sprite MakeSticker(Color32[] px, string id)
        {
            int n = Size * Size;
            var alpha = new byte[n];

            // URP 설정에 따라 알파가 안 남을 수 있음 → 그럴 땐 배경색(마젠타)으로 판별
            bool alphaWorks = false;
            for (int i = 0; i < n; i++) if (px[i].a < 128) { alphaWorks = true; break; }
            for (int i = 0; i < n; i++)
            {
                var c = px[i];
                bool bg = alphaWorks ? c.a < 128 : (c.r > 235 && c.g < 30 && c.b > 235);
                alpha[i] = bg ? (byte)0 : (byte)255;
            }

            // 테두리: 상품 모양을 Border px 만큼 원형으로 부풀린 자리를 흰색으로
            var offsets = new List<Vector2Int>();
            for (int dy = -Border; dy <= Border; dy++)
                for (int dx = -Border; dx <= Border; dx++)
                    if (dx * dx + dy * dy <= Border * Border && (dx != 0 || dy != 0)) offsets.Add(new Vector2Int(dx, dy));

            var outPx = new Color32[n];
            var white = new Color32(255, 253, 246, 255);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (alpha[i] != 0)
                    {
                        var c = px[i];
                        c.a = 255;
                        outPx[i] = c;
                        continue;
                    }
                    bool near = false;
                    foreach (var o in offsets)
                    {
                        int sx = x + o.x, sy = y + o.y;
                        if (sx < 0 || sy < 0 || sx >= Size || sy >= Size) continue;
                        if (alpha[sy * Size + sx] != 0) { near = true; break; }
                    }
                    outPx[i] = near ? white : new Color32(0, 0, 0, 0);
                }
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Icon " + id,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(outPx);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
