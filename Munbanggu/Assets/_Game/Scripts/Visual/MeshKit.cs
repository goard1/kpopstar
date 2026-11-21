using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Visual
{
    /// <summary>
    /// 버텍스 컬러 로우폴리 메시를 코드로 쌓는 도구.
    /// 면 노멀(플랫 셰이딩) + UV2에 외곽선용 부드러운 노멀을 넣는다.
    /// 여러 부품을 한 메시로 합쳐 드로우콜을 줄이는 용도.
    /// </summary>
    public class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>(1024);
        readonly List<Vector3> normals = new List<Vector3>(1024);
        readonly List<Vector3> smooth = new List<Vector3>(1024);
        readonly List<Color> colors = new List<Color>(1024);
        readonly List<Vector2> uvs = new List<Vector2>(1024);
        readonly List<int> tris = new List<int>(2048);

        /// <summary>true면 Sphere를 꼭짓점 노멀로 매끈하게 (캐릭터처럼 동글한 것). 기본은 각진 로우폴리.</summary>
        public bool SmoothSpheres;
        /// <summary>매끈한 구의 노멀을 위로 기울이는 정도 (0~1). 얼굴에 그늘 선이 덜 지게 (캐릭터용).</summary>
        public float SmoothUpBias;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        Matrix4x4 m = Matrix4x4.identity;

        public int VertexCount => verts.Count;

        // ───────────── 변환 스택 ─────────────

        public void Push(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            stack.Push(m);
            m = m * Matrix4x4.TRS(pos, rot, scale);
        }

        public void Push(Vector3 pos, Quaternion rot) => Push(pos, rot, Vector3.one);
        public void Push(Vector3 pos) => Push(pos, Quaternion.identity, Vector3.one);
        public void Pop() => m = stack.Count > 0 ? stack.Pop() : Matrix4x4.identity;

        public void Clear()
        {
            verts.Clear(); normals.Clear(); smooth.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
            stack.Clear();
            m = Matrix4x4.identity;
        }

        // ───────────── 기본 ─────────────

        int V(Vector3 p, Vector3 n, Vector3 s, Color c, Vector2 uv = default)
        {
            verts.Add(m.MultiplyPoint3x4(p));
            normals.Add(m.MultiplyVector(n).normalized);
            var sw = m.MultiplyVector(s);
            smooth.Add(sw.sqrMagnitude > 1e-8f ? sw.normalized : Vector3.zero);
            colors.Add(c);
            uvs.Add(uv);
            return verts.Count - 1;
        }

        /// <summary>사각형 면 (반시계 a-b-c-d 가 바깥에서 보이는 방향). 외곽선 노멀은 각 꼭짓점의 sN.</summary>
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col,
                  Vector3 sa, Vector3 sb, Vector3 sc, Vector3 sd)
        {
            int i0 = V(a, n, sa, col, new Vector2(0, 0));
            int i1 = V(b, n, sb, col, new Vector2(0, 1));
            int i2 = V(c, n, sc, col, new Vector2(1, 1));
            int i3 = V(d, n, sd, col, new Vector2(1, 0));
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i0); tris.Add(i2); tris.Add(i3);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n, Color col, Vector3 sa, Vector3 sb, Vector3 sc)
        {
            int i0 = V(a, n, sa, col);
            int i1 = V(b, n, sb, col);
            int i2 = V(c, n, sc, col);
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
        }

        // ───────────── 도형 ─────────────

        /// <summary>상자. center/size는 현재 변환 기준. top/side 색을 따로 줄 수 있음.</summary>
        public void Box(Vector3 center, Vector3 size, Color color) => Box(center, size, color, color);

        public void Box(Vector3 center, Vector3 size, Color top, Color side, bool outline = true)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 S(float x, float y, float z) => outline ? new Vector3(x, y, z).normalized : Vector3.zero;

            // +Y
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up, top,
                S(-1, 1, -1), S(-1, 1, 1), S(1, 1, 1), S(1, 1, -1));
            // -Y
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), Vector3.down, side,
                S(-1, -1, 1), S(-1, -1, -1), S(1, -1, -1), S(1, -1, 1));
            // -Z
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Vector3.back, side,
                S(-1, -1, -1), S(-1, 1, -1), S(1, 1, -1), S(1, -1, -1));
            // +Z
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), Vector3.forward, side,
                S(1, -1, 1), S(1, 1, 1), S(-1, 1, 1), S(-1, -1, 1));
            // -X
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), Vector3.left, side,
                S(-1, -1, 1), S(-1, 1, 1), S(-1, 1, -1), S(-1, -1, -1));
            // +X
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Vector3.right, side,
                S(1, -1, -1), S(1, 1, -1), S(1, 1, 1), S(1, -1, 1));
        }

        /// <summary>외곽선 없는 상자 (바닥 타일·얇은 장식용).</summary>
        public void FlatBox(Vector3 center, Vector3 size, Color color) => Box(center, size, color, color, false);

        /// <summary>위를 보는 사각 판 (외곽선 없음).</summary>
        public void FloorQuad(Vector3 center, Vector2 size, Color color)
        {
            Vector3 h = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
            Quad(center + new Vector3(-h.x, 0, -h.z), center + new Vector3(-h.x, 0, h.z),
                 center + new Vector3(h.x, 0, h.z), center + new Vector3(h.x, 0, -h.z),
                 Vector3.up, color, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
        }

        /// <summary>앞(-Z 쪽)을 보는 세로 판 (포스터·스티커용, 외곽선 없음).</summary>
        public void WallQuad(Vector3 center, Vector2 size, Color color)
        {
            Vector3 h = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            Quad(center + new Vector3(-h.x, -h.y, 0), center + new Vector3(-h.x, h.y, 0),
                 center + new Vector3(h.x, h.y, 0), center + new Vector3(h.x, -h.y, 0),
                 Vector3.back, color, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
        }

        /// <summary>Y축 원기둥 (위·아래 뚜껑). segments가 적을수록 로우폴리.</summary>
        public void Cylinder(Vector3 center, float radius, float height, int segments, Color color, Color? topColor = null)
            => Frustum(center, radius, radius, height, segments, color, topColor ?? color);

        /// <summary>Y축 원뿔대 (아래 반지름 r0, 위 반지름 r1).</summary>
        public void Frustum(Vector3 center, float r0, float r1, float height, int segments, Color side, Color top,
                            bool capBottom = true, bool capTop = true)
        {
            float y0 = center.y - height * 0.5f, y1 = center.y + height * 0.5f;
            float slope = (r0 - r1) / Mathf.Max(0.0001f, height);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 dm = (d0 + d1).normalized;
                Vector3 n = (dm + Vector3.up * slope).normalized;
                Vector3 b0 = center + d0 * r0; b0.y = y0;
                Vector3 b1 = center + d1 * r0; b1.y = y0;
                Vector3 t0 = center + d0 * r1; t0.y = y1;
                Vector3 t1 = center + d1 * r1; t1.y = y1;
                Vector3 sb0 = (d0 - Vector3.up * 0.7f).normalized, sb1 = (d1 - Vector3.up * 0.7f).normalized;
                Vector3 st0 = (d0 + Vector3.up * 0.7f).normalized, st1 = (d1 + Vector3.up * 0.7f).normalized;
                // 바깥에서 볼 때 시계방향이 되도록 (Unity 왼손 좌표계: 시계 = 앞면)
                Quad(b0, t0, t1, b1, n, side, sb0, st0, st1, sb1);
                if (capTop && r1 > 0.0001f)
                {
                    Vector3 c = center; c.y = y1;
                    Tri(c, t1, t0, Vector3.up, top, Vector3.up, st1, st0);
                }
                if (capBottom && r0 > 0.0001f)
                {
                    Vector3 c = center; c.y = y0;
                    Tri(c, b0, b1, Vector3.down, side, Vector3.down, sb0, sb1);
                }
            }
        }

        /// <summary>로우폴리 구 (위도 rings, 경도 segments). 납작하게 하려면 scale 사용.</summary>
        public void Sphere(Vector3 center, Vector3 radius, int segments, int rings, Color color)
        {
            for (int r = 0; r < rings; r++)
            {
                float p0 = Mathf.PI * r / rings - Mathf.PI * 0.5f;
                float p1 = Mathf.PI * (r + 1) / rings - Mathf.PI * 0.5f;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = Mathf.PI * 2f * s / segments;
                    float a1 = Mathf.PI * 2f * (s + 1) / segments;
                    Vector3 u00 = Dir(p0, a0), u01 = Dir(p0, a1), u10 = Dir(p1, a0), u11 = Dir(p1, a1);
                    Vector3 v00 = center + Vector3.Scale(u00, radius);
                    Vector3 v01 = center + Vector3.Scale(u01, radius);
                    Vector3 v10 = center + Vector3.Scale(u10, radius);
                    Vector3 v11 = center + Vector3.Scale(u11, radius);
                    if (SmoothSpheres)
                    {
                        // 꼭짓점마다 타원면 노멀 → 동글동글 매끈한 음영 (캐릭터용)
                        Vector3 N(Vector3 u) => (new Vector3(u.x / Mathf.Max(1e-4f, radius.x), u.y / Mathf.Max(1e-4f, radius.y), u.z / Mathf.Max(1e-4f, radius.z)).normalized + Vector3.up * SmoothUpBias).normalized;
                        int a = V(v00, N(u00), u00, color), b = V(v10, N(u10), u10, color);
                        int c = V(v11, N(u11), u11, color), d = V(v01, N(u01), u01, color);
                        if (r == 0) { tris.Add(a); tris.Add(b); tris.Add(c); }
                        else if (r == rings - 1) { tris.Add(a); tris.Add(b); tris.Add(d); }
                        else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
                        continue;
                    }
                    Vector3 n = Vector3.Cross(v10 - v00, v01 - v00);
                    if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(v11 - v10, v01 - v10);
                    n = n.normalized;
                    if (Vector3.Dot(n, (v00 + v11) * 0.5f - center) < 0f) n = -n;
                    if (r == 0) Tri(v00, v10, v11, n, color, u00, u10, u11);
                    else if (r == rings - 1) Tri(v00, v10, v01, n, color, u00, u10, u01);
                    else Quad(v00, v10, v11, v01, n, color, u00, u10, u11, u01);
                }
            }
        }

        static Vector3 Dir(float pitch, float yaw)
            => new Vector3(Mathf.Cos(pitch) * Mathf.Cos(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Sin(yaw));

        /// <summary>
        /// normal 쪽을 보는 타원 판 (외곽선 없음). 얼굴의 눈·반짝이·볼터치·입처럼 둥근 면 위에 붙이는 용도.
        /// radius.x = 가로(normal과 수직인 수평 방향), radius.y = 세로.
        /// </summary>
        public void DiscFacing(Vector3 center, Vector2 radius, Vector3 normal, int segments, Color color)
        {
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, normal);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(normal, right).normalized;
            int c = V(center, normal, Vector3.zero, color, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments, a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector3 p0 = center + right * (Mathf.Cos(a0) * radius.x) + up * (Mathf.Sin(a0) * radius.y);
                Vector3 p1 = center + right * (Mathf.Cos(a1) * radius.x) + up * (Mathf.Sin(a1) * radius.y);
                int i0 = V(p0, normal, Vector3.zero, color, new Vector2(0.5f, 0.5f));
                int i1 = V(p1, normal, Vector3.zero, color, new Vector2(0.5f, 0.5f));
                // normal 쪽에서 보면 right가 왼쪽에 보이므로 p0→p1은 시계 방향 (Unity: 시계 = 앞면)
                tris.Add(c); tris.Add(i0); tris.Add(i1);
            }
        }

        /// <summary>바닥에 붙는 원판 (가짜 그림자·매트용). UV는 0~1 정사각.</summary>
        public void Disc(Vector3 center, float radius, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments, a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                int c = V(center, Vector3.up, Vector3.zero, color, new Vector2(0.5f, 0.5f));
                int i0 = V(p1, Vector3.up, Vector3.zero, color, new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f));
                int i1 = V(p0, Vector3.up, Vector3.zero, color, new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f));
                tris.Add(c); tris.Add(i0); tris.Add(i1);
            }
        }

        // ───────────── 결과 ─────────────

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            Fill(mesh);
            return mesh;
        }

        /// <summary>기존 메시에 덮어쓰기 (매대 상품처럼 자주 다시 만들 때 할당 줄이기).</summary>
        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, smooth);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>메시 데이터 사본 (여러 번 복제해 붙일 모양용, 할당 없이 Append).</summary>
        public sealed class Data
        {
            public Vector3[] v, n, s;
            public Color[] c;
            public Vector2[] uv;
            public int[] t;
        }

        public Data ToData() => new Data
        {
            v = verts.ToArray(), n = normals.ToArray(), s = smooth.ToArray(),
            c = colors.ToArray(), uv = uvs.ToArray(), t = tris.ToArray(),
        };

        /// <summary>데이터 하나를 현재 변환으로 이어 붙임 (상품 모양 복제용).</summary>
        public void Append(Data d)
        {
            int baseIndex = verts.Count;
            for (int i = 0; i < d.v.Length; i++)
            {
                verts.Add(m.MultiplyPoint3x4(d.v[i]));
                normals.Add(m.MultiplyVector(d.n[i]).normalized);
                var sw = m.MultiplyVector(d.s[i]);
                smooth.Add(sw.sqrMagnitude > 1e-8f ? sw.normalized : Vector3.zero);
                colors.Add(d.c[i]);
                uvs.Add(d.uv[i]);
            }
            for (int i = 0; i < d.t.Length; i++) tris.Add(baseIndex + d.t[i]);
        }

        /// <summary>삼각 깃발 등 얇은 삼각형 (양면, 외곽선 없음).</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Tri(a, b, c, n, color, Vector3.zero, Vector3.zero, Vector3.zero);
            Tri(a, c, b, -n, color, Vector3.zero, Vector3.zero, Vector3.zero);
        }

        /// <summary>MeshFilter+MeshRenderer 달린 오브젝트를 만든다 (콜라이더 없음).</summary>
        public GameObject Build(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        // ───────────── 색 도우미 ─────────────

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
