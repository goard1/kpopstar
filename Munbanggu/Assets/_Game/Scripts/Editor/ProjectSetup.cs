using System.Collections.Generic;
using System.IO;
using Munbanggu.Core;
using Munbanggu.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// 프로젝트 초기 설정: Android 플레이어 설정, 상품 데이터, Main 씬 생성.
    /// 여러 번 실행해도 안전 (이미 있는 상품·씬은 덮어쓰지 않음).
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Game";
        const string ProductsDir = Root + "/Data/Products";
        const string CatalogPath = Root + "/Data/ProductCatalog.asset";
        const string MaterialPath = Root + "/Materials/Graybox.mat";
        const string ScenePath = Root + "/Scenes/Main.unity";

        // id, 이름, 도매가, 판매가, 상자 수량, 인기, 색
        static readonly (string id, string name, int buy, int sell, int box, float pop, Color color)[] Products =
        {
            ("jjondeugi", "쫀드기", 60, 100, 20, 1.4f, new Color(0.85f, 0.55f, 0.30f)),
            ("apollo", "아폴로", 40, 100, 20, 1.2f, new Color(0.95f, 0.85f, 0.30f)),
            ("ddakji", "딱지", 120, 200, 10, 1.0f, new Color(0.30f, 0.50f, 0.85f)),
            ("marble", "구슬", 150, 300, 10, 0.8f, new Color(0.35f, 0.80f, 0.85f)),
            ("eraser", "지우개", 80, 150, 20, 1.0f, new Color(0.95f, 0.95f, 0.92f)),
            ("sticker", "스티커", 100, 200, 10, 1.1f, new Color(0.65f, 0.45f, 0.85f)),
            ("paperdoll", "종이인형", 200, 400, 10, 0.7f, new Color(0.95f, 0.60f, 0.75f)),
            ("minicar", "미니카", 600, 1000, 5, 0.5f, new Color(0.85f, 0.25f, 0.25f)),
        };

        [MenuItem("문방구/프로젝트 초기 설정")]
        public static void Run()
        {
            ConfigurePlayer();
            var material = CreateMaterial();
            var catalog = CreateCatalog();
            CreateScene(catalog, material);
            CleanupTemplate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[문방구] 초기 설정 완료. Assets/_Game/Scenes/Main 씬에서 Play를 누르세요.");
        }

        /// <summary>배치 모드용: Unity.exe -batchmode -executeMethod Munbanggu.EditorTools.ProjectSetup.RunBatch</summary>
        public static void RunBatch() => Run();

        [MenuItem("문방구/세이브 삭제")]
        public static void DeleteSave()
        {
            SaveSystem.Delete();
            Debug.Log("[문방구] 세이브를 삭제했습니다. 다음 Play부터 1일차로 시작합니다.");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Munbanggu";
            PlayerSettings.productName = "추억의 문방구";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            var android = NamedBuildTarget.Android;
            // ⚠️ 패키지명은 Play 스토어에 한 번 올리면 못 바꿈. 출시 전에 확정할 것.
            PlayerSettings.SetApplicationIdentifier(android, "com.munbanggu.retroshop");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            EditorUserBuildSettings.buildAppBundle = true;
        }

        static Material CreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;
            EnsureFolder(Path.GetDirectoryName(MaterialPath));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        static ProductCatalog CreateCatalog()
        {
            EnsureFolder(ProductsDir);
            var list = new List<ProductData>();
            foreach (var d in Products)
            {
                string path = $"{ProductsDir}/{d.id}.asset";
                var p = AssetDatabase.LoadAssetAtPath<ProductData>(path);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<ProductData>();
                    p.id = d.id;
                    p.displayName = d.name;
                    p.wholesalePrice = d.buy;
                    p.retailPrice = d.sell;
                    p.boxSize = d.box;
                    p.popularity = d.pop;
                    p.color = d.color;
                    AssetDatabase.CreateAsset(p, path);
                }
                list.Add(p);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProductCatalog>();
                catalog.products = list;
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                foreach (var p in list)
                    if (!catalog.products.Contains(p)) catalog.products.Add(p);
                EditorUtility.SetDirty(catalog);
            }
            return catalog;
        }

        static void CreateScene(ProductCatalog catalog, Material material)
        {
            if (!File.Exists(ScenePath))
            {
                EnsureFolder(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Game");
                var boot = go.AddComponent<Bootstrap>();
                boot.catalog = catalog;
                boot.baseMaterial = material;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(ScenePath);
        }

        static void CleanupTemplate()
        {
            foreach (var path in new[] { "Assets/TutorialInfo", "Assets/Readme.asset", "Assets/Scenes" })
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
