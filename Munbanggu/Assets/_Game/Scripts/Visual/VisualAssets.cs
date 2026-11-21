using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 비주얼 머티리얼 로드. 머티리얼 에셋은 Assets/_Game/Resources/Visual/ 에 있어서
    /// 빌드에 셰이더가 같이 들어간다 (Shader.Find만 쓰면 빌드에서 빠짐).
    /// 에셋이 없으면(에디터에서 아직 안 만들었을 때) 셰이더를 찾아 임시로 만든다.
    /// 에셋 생성·갱신: Munbanggu.EditorTools.ShopScreenshot.EnsureAssets
    /// </summary>
    public static class VisualAssets
    {
        public const string ToonPath = "Visual/Toon";                   // 단색 (MaterialCache 기본, 외곽선 없음)
        public const string ToonVCPath = "Visual/ToonVC";               // 버텍스 컬러 + 외곽선
        public const string ToonVCRegularPath = "Visual/ToonVCRegular"; // 단골 손님 (주황 굵은 외곽선)
        public const string BlobPath = "Visual/BlobShadow";             // 가짜 그림자
        public const string GlassPath = "Visual/Glass";                 // 유리 (곱하기)

        public const float OutlineWidth = 0.016f;
        public const float RegularOutlineWidth = 0.034f;

        // 팔레트 — 따뜻한 형광등 아래 문방구
        public static readonly Color OutlineColor = new Color(0.27f, 0.17f, 0.14f);
        public static readonly Color RegularOutline = new Color(1.0f, 0.58f, 0.12f);
        public static readonly Color Background = new Color(1.00f, 0.88f, 0.86f); // 연분홍
        public static readonly Color LightColor = new Color(1.0f, 0.96f, 0.88f);
        public const float LightIntensity = 0.78f;

        public static Material LoadToon(string path, bool vertexColor, float outlineWidth, Color outline)
        {
            var mat = Resources.Load<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Munbanggu/Toon");
            if (shader == null)
                return new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = path };
            mat = new Material(shader) { name = System.IO.Path.GetFileName(path) };
            ConfigureToon(mat, vertexColor, outlineWidth, outline);
            return mat;
        }

        /// <summary>툰 머티리얼 공통 값 (에디터 에셋 생성에도 사용).</summary>
        public static void ConfigureToon(Material mat, bool vertexColor, float outlineWidth, Color outline)
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            mat.SetColor("_ShadeColor", new Color(0.70f, 0.62f, 0.70f));
            mat.SetColor("_SkyColor", new Color(0.25f, 0.24f, 0.23f));
            mat.SetColor("_GroundColor", new Color(0.17f, 0.15f, 0.16f));
            mat.SetFloat("_RampCenter", 0.50f);
            mat.SetFloat("_RampSoftness", 0.04f);
            mat.SetColor("_OutlineColor", outline);
            mat.SetFloat("_OutlineWidth", outlineWidth);
            mat.SetShaderPassEnabled("SRPDefaultUnlit", outlineWidth > 0f);
        }

        public static Material LoadMultiply(string path, Color color, bool radial)
        {
            var mat = Resources.Load<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Munbanggu/BlobShadow");
            if (shader == null) return null;
            mat = new Material(shader) { name = System.IO.Path.GetFileName(path) };
            ConfigureMultiply(mat, color, radial);
            return mat;
        }

        public static void ConfigureMultiply(Material mat, Color color, bool radial)
        {
            mat.SetColor("_ShadowColor", color);
            mat.SetFloat("_Radial", radial ? 1f : 0f);
        }

        public static readonly Color BlobColor = new Color(0.62f, 0.50f, 0.48f);
        public static readonly Color GlassColor = new Color(0.80f, 0.92f, 0.95f);

        static Mesh blobMesh;

        /// <summary>바닥 원형 그림자 오브젝트 (부모 기준 바닥 위). 메시는 전부 공유.</summary>
        public static GameObject Blob(Transform parent, Vector3 localPos, Vector2 size, Material blob)
        {
            if (blob == null) return null;
            if (blobMesh == null)
            {
                var kit = new MeshKit();
                kit.FloorQuad(Vector3.zero, Vector2.one, Color.white);
                blobMesh = kit.ToMesh("Blob");
            }
            var go = new GameObject("Blob");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = blobMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = blob;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            return go;
        }
    }
}
