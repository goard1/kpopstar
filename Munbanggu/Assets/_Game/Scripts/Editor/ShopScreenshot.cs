using System.IO;
using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// 비주얼 검증용: 에디트 모드에서 가게를 임시로 만들고 1080x1920으로 찍어 PNG로 저장.
    /// 배치: bash tools/unity-compile.sh visual Munbanggu.EditorTools.ShopScreenshot.Capture
    /// 결과: Munbanggu/Logs/shot-shop.png (원본), shot-shop-ui.png (UI 가림 영역 표시), shot-shop-zoom.png (매대 확대)
    /// 씬은 저장하지 않고, 만든 오브젝트는 끝나면 지운다.
    /// </summary>
    public static class ShopScreenshot
    {
        const string ResDir = "Assets/_Game/Resources/Visual";
        const string ToonShaderPath = "Assets/_Game/Shaders/Toon.shader";
        const string BlobShaderPath = "Assets/_Game/Shaders/BlobShadow.shader";
        const string CatalogPath = "Assets/_Game/Data/ProductCatalog.asset";
        const string MaterialPath = "Assets/_Game/Materials/Graybox.mat";

        const int Width = 1080, Height = 1920;
        const int TopUi = 200, BottomUi = 430;

        [MenuItem("문방구/비주얼 에셋 갱신")]
        public static void EnsureAssets()
        {
            EnsureFolder(ResDir);
            var toon = AssetDatabase.LoadAssetAtPath<Shader>(ToonShaderPath);
            var blob = AssetDatabase.LoadAssetAtPath<Shader>(BlobShaderPath);
            if (toon == null || blob == null)
            {
                Debug.LogError("[문방구] 비주얼 셰이더를 찾을 수 없습니다: " + ToonShaderPath);
                return;
            }
            Toon("Toon", toon, false, 0f, VisualAssets.OutlineColor);
            Toon("ToonVC", toon, true, VisualAssets.OutlineWidth, VisualAssets.OutlineColor);
            Toon("ToonVCRegular", toon, true, VisualAssets.RegularOutlineWidth, VisualAssets.RegularOutline);
            Multiply("BlobShadow", blob, VisualAssets.BlobColor, true);
            Multiply("Glass", blob, VisualAssets.GlassColor, false);
            AssetDatabase.SaveAssets();
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            string path = $"{ResDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        static void Toon(string name, Shader shader, bool vc, float width, Color outline)
        {
            var mat = GetOrCreate(name, shader);
            VisualAssets.ConfigureToon(mat, vc, width, outline);
            EditorUtility.SetDirty(mat);
        }

        static void Multiply(string name, Shader shader, Color color, bool radial)
        {
            var mat = GetOrCreate(name, shader);
            VisualAssets.ConfigureMultiply(mat, color, radial);
            EditorUtility.SetDirty(mat);
        }

        [MenuItem("문방구/가게 스크린샷")]
        public static void Capture()
        {
            EnsureAssets();
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            ProductVisuals.ClearCache();

            var root = new GameObject("ShopScreenshot (임시)");
            RenderTexture rt = null;
            try
            {
                var mats = new MaterialCache(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
                var layout = ShopBuilder.Build(root.transform, mats);
                FillShelves(layout);
                PlaceCustomers(root.transform, layout, mats);

                var cam = layout.Camera;
                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.GetComponent<ShopCamera>().Fit();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);

                var shot = RenderTo(cam, rt);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop.png"), shot.EncodeToPNG());
                ShadeUi(shot);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop-ui.png"), shot.EncodeToPNG());
                Object.DestroyImmediate(shot);

                // 확대 샷: 오른쪽 뒤 매대 + 손님
                float size = cam.orthographicSize;
                cam.orthographicSize = size * 0.42f;
                cam.transform.position = new Vector3(1.4f, 0.8f, 1.6f) - cam.transform.forward * 30f;
                var zoom = RenderTo(cam, rt);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop-zoom.png"), zoom.EncodeToPNG());
                Object.DestroyImmediate(zoom);

                // 확대 샷 2: 계산대 + 줄
                cam.transform.position = new Vector3(-2.4f, 0.8f, -1.8f) - cam.transform.forward * 30f;
                var zoom2 = RenderTo(cam, rt);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop-zoom2.png"), zoom2.EncodeToPNG());
                Object.DestroyImmediate(zoom2);

                // 확대 샷 4: 가게 앞 번호 뽑기판
                cam.orthographicSize = size * 0.32f;
                cam.transform.position = new Vector3(-1.95f, 0.9f, -4.7f) - cam.transform.forward * 30f;
                var zoom4 = RenderTo(cam, rt);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop-board.png"), zoom4.EncodeToPNG());
                Object.DestroyImmediate(zoom4);

                // 확대 샷 3: 문 앞 보도 (보도블록·줄눈 깜빡임/깨짐 확인용)
                cam.orthographicSize = size * 0.3f;
                cam.transform.position = new Vector3(0.6f, 0.1f, -5.0f) - cam.transform.forward * 30f;
                var zoom3 = RenderTo(cam, rt);
                File.WriteAllBytes(Path.Combine(dir, "shot-shop-zoom3.png"), zoom3.EncodeToPNG());
                Object.DestroyImmediate(zoom3);

                cam.targetTexture = null;
                Debug.Log("[문방구] 스크린샷 저장: " + dir);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                ShaderUtil.allowAsyncCompilation = prevAsync;
            }
        }

        /// <summary>손님 얼굴 확인용 클로즈업: 일반 9명 + 단골 3명을 게임 카메라 각도로 확대해 shot-customers*.png 저장.</summary>
        [MenuItem("문방구/손님 클로즈업")]
        public static void CaptureCustomers()
        {
            EnsureAssets();
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var root = new GameObject("CustomerShot (임시)");
            RenderTexture rt = null;
            try
            {
                var mats = new MaterialCache(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
                var layout = ShopBuilder.Build(root.transform, mats);
                var cam = layout.Camera;
                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.GetComponent<ShopCamera>().Fit();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.82f, 0.9f, 0.95f);

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                string[] regs = { "cheolsu", "younghee", "minsu" };
                Vector3 camRight = cam.transform.right;
                Vector3 camFwd = cam.transform.forward;
                Vector3 camDir = -new Vector3(camFwd.x, 0f, camFwd.z).normalized; // 카메라 쪽
                // 두 장: 줄 방향(왼쪽)을 보는 모습 / 카메라를 정면으로 보는 모습
                for (int pass = 0; pass < 2; pass++)
                {
                    var holder = new GameObject("Row").transform;
                    holder.SetParent(root.transform, false);
                    Vector3 face = pass == 0 ? Vector3.left : camDir;
                    Vector3 origin = new Vector3(0f, 40f, 0f);
                    for (int i = 0; i < 12; i++)
                    {
                        int col = i % 6, row = i / 6;
                        Vector3 pos = origin + camRight * ((col - 2.5f) * 0.75f) + Vector3.up * (-row * 1.3f);
                        var look = i < 9 ? new CustomerLook { seed = (i + 1) * 7919 }
                                         : new CustomerLook { seed = 0, isRegular = true, regularId = regs[i - 9] };
                        Spawn(holder, mats, pos, pos + face, look);
                    }
                    cam.orthographicSize = 1.6f;
                    cam.transform.position = origin + new Vector3(0f, -0.75f, 0f) - camFwd * 30f;
                    var tex = RenderTo(cam, rt);
                    File.WriteAllBytes(Path.Combine(dir, pass == 0 ? "shot-customers.png" : "shot-customers-front.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(holder.gameObject);
                }
                cam.targetTexture = null;
                Debug.Log("[문방구] 손님 클로즈업 저장: " + dir);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                ShaderUtil.allowAsyncCompilation = prevAsync;
            }
        }

        static Texture2D RenderTo(Camera cam, RenderTexture rt)
        {
            var request = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                request.destination = rt;
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>위·아래 UI가 덮을 영역을 어둡게 표시.</summary>
        static void ShadeUi(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int top = Mathf.RoundToInt(TopUi * h / 1920f), bottom = Mathf.RoundToInt(BottomUi * h / 1920f);
            for (int y = 0; y < h; y++)
            {
                bool covered = y < bottom || y >= h - top; // 텍스처는 아래가 y=0
                if (!covered) continue;
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    px[y * w + x] = new Color32((byte)(c.r / 3 + 40), (byte)(c.g / 3 + 40), (byte)(c.b / 3 + 60), 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        static void FillShelves(ShopLayout layout)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(CatalogPath);
            if (catalog == null || catalog.products.Count == 0) return;
            int[] counts = { 12, 9, 12, 7, 12, 11, 0, 12 };
            int n = catalog.products.Count;
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                var p = catalog.products[(i * 3 + i / 3) % n]; // 여러 종류가 섞여 보이게
                if (layout.Slots[i].IsBoard) { layout.Slots[i].Load(catalog.Find("ppopgi"), 22); continue; }
                var slot = layout.Slots[i];
                if (counts[i % counts.Length] == 0)
                {
                    slot.Load(p, 1);
                    slot.TakeOne(); // 품절 표시
                }
                else slot.Load(p, counts[i % counts.Length]);
            }
        }

        static void PlaceCustomers(Transform root, ShopLayout layout, MaterialCache mats)
        {
            int seed = 1;
            // 매대 앞에서 고르는 손님
            foreach (int s in new[] { 1, 3, 6 })
            {
                var slot = layout.Slots[s];
                Spawn(root, mats, slot.StandPoint, slot.transform.position, new CustomerLook { seed = seed++ * 7919 });
            }
            // 계산 줄 (맨 앞은 단골)
            for (int i = 0; i < 4; i++)
            {
                var spot = ShopBuilder.QueueSpots[i];
                var look = i == 0
                    ? new CustomerLook { seed = 0, isRegular = true, regularId = "minsu" }
                    : new CustomerLook { seed = seed++ * 7919 };
                Spawn(root, mats, spot, spot + layout.Queue.Facing, look);
            }
            // 들어오는 손님
            Spawn(root, mats, new Vector3(1.5f, 0f, -3.0f), new Vector3(0.5f, 0f, 0f), new CustomerLook { seed = seed++ * 7919 });
            Spawn(root, mats, new Vector3(0.6f, 0f, 0.4f), new Vector3(-1.6f, 0f, 2.5f), new CustomerLook { seed = seed++ * 7919 });
        }

        static void Spawn(Transform root, MaterialCache mats, Vector3 pos, Vector3 lookAt, CustomerLook look)
        {
            var go = new GameObject("Customer (임시)");
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var dir = lookAt - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(dir);
            CustomerVisual.Build(go.transform, mats, look);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
