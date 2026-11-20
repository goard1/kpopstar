using System.Collections.Generic;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>
    /// 색상별 머티리얼을 한 번만 만들어 재사용 (SRP Batcher 유지).
    /// 기본은 툰 셰이더(Resources/Visual/Toon). 버텍스 컬러 메시용 공유 머티리얼도 여기서 준다.
    /// </summary>
    public class MaterialCache
    {
        readonly Material baseMaterial;
        readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        /// <summary>MeshKit으로 만든 버텍스 컬러 메시용 (외곽선 포함). 가게·상품·손님이 모두 공유.</summary>
        public Material VertexColor { get; }
        /// <summary>단골 손님용 (주황 외곽선).</summary>
        public Material RegularVertexColor { get; }
        /// <summary>바닥 가짜 그림자 (곱하기, 원형).</summary>
        public Material Blob { get; }
        /// <summary>유리 (곱하기, 면 전체).</summary>
        public Material Glass { get; }

        public MaterialCache(Material baseMaterial)
        {
            var toon = VisualAssets.LoadToon(VisualAssets.ToonPath, false, 0f, VisualAssets.OutlineColor);
            if (toon != null && toon.shader != null && toon.shader.name == "Munbanggu/Toon")
                this.baseMaterial = toon;
            else
                this.baseMaterial = baseMaterial != null
                    ? baseMaterial
                    : new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));

            VertexColor = VisualAssets.LoadToon(VisualAssets.ToonVCPath, true, VisualAssets.OutlineWidth, VisualAssets.OutlineColor);
            RegularVertexColor = VisualAssets.LoadToon(VisualAssets.ToonVCRegularPath, true, VisualAssets.RegularOutlineWidth, VisualAssets.RegularOutline);
            Blob = VisualAssets.LoadMultiply(VisualAssets.BlobPath, VisualAssets.BlobColor, true);
            Glass = VisualAssets.LoadMultiply(VisualAssets.GlassPath, VisualAssets.GlassColor, false);
        }

        public Material Get(Color color)
        {
            if (!cache.TryGetValue(color, out var m))
            {
                m = new Material(baseMaterial) { color = color };
                cache[color] = m;
            }
            return m;
        }

        public void Apply(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Get(color);
        }
    }
}
