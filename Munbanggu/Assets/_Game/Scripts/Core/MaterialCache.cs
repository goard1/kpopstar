using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>색상별 머티리얼을 한 번만 만들어 재사용 (SRP Batcher 유지).</summary>
    public class MaterialCache
    {
        readonly Material baseMaterial;
        readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        public MaterialCache(Material baseMaterial)
        {
            this.baseMaterial = baseMaterial != null
                ? baseMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
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
