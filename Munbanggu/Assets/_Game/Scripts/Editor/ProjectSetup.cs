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

        const ProductCategory Snack = ProductCategory.Snack, Stat = ProductCategory.Stationery,
            Toy = ProductCategory.Toy, Play = ProductCategory.Play;

        /// <summary>
        /// 상품 표 (이 표가 원본. RunBatch 때 기존 에셋 값도 이 표로 갱신됨 — id는 세이브 키라 절대 바꾸지 말 것).
        /// 공통 추억템 14종(70%) + 한국 시그니처 6종(30%). 처음 8종은 1일차부터, 나머지는 2~7일차에 2종씩 해금.
        /// 밸런스 기준: 손님 1명이 원할 때의 기대 마진 = (판매가-도매가) × 인기 ≈ 50~180원.
        /// 싼 물건은 잘 팔리고 회전이 빠름, 비싼 물건은 마진이 크지만 느리게 팔려 돈이 묶임.
        /// </summary>
        static readonly (string id, string name, int buy, int sell, int box, float pop, ProductCategory cat, bool kr, int unlock, Color color, string blurb)[] Products =
        {
            // ── 1일차 (기존 8종, id 유지) ──
            ("jjondeugi", "쫀드기", 50, 100, 20, 1.6f, Snack, true, 1, new Color(0.85f, 0.55f, 0.30f), "연탄불에 살짝 구워 쭉쭉 찢어 먹던 쫀득한 불량식품."),
            ("apollo", "아폴로", 50, 100, 20, 1.4f, Snack, true, 1, new Color(0.95f, 0.85f, 0.30f), "빨대 모양 비닐에 든 새콤달콤 가루 과자. 이로 쭉 짜 먹는다."),
            ("ddakji", "딱지", 100, 200, 10, 1.0f, Play, true, 1, new Color(0.30f, 0.50f, 0.85f), "내려쳐서 상대 딱지를 뒤집으면 내 것! 동네 대회의 주인공."),
            ("marble", "구슬", 100, 200, 10, 0.8f, Play, false, 1, new Color(0.35f, 0.80f, 0.85f), "유리구슬 한 봉지. 땅에 구멍 파고 튕겨 넣는 구슬치기."),
            ("eraser", "지우개", 50, 100, 20, 1.2f, Stat, false, 1, new Color(0.95f, 0.95f, 0.92f), "향기 나는 지우개. 쓰기보다 모으고 자랑하는 게 더 중요했다."),
            ("sticker", "스티커", 100, 200, 10, 1.0f, Play, false, 1, new Color(0.65f, 0.45f, 0.85f), "만화 캐릭터 스티커. 필통과 책상이 전부 스티커 천지."),
            ("paperdoll", "종이인형", 150, 300, 10, 0.7f, Toy, false, 1, new Color(0.95f, 0.60f, 0.75f), "가위로 오려 옷을 갈아입히는 종이 인형 세트."),
            ("minicar", "미니카", 600, 1000, 5, 0.45f, Toy, false, 1, new Color(0.85f, 0.25f, 0.25f), "모터 달린 경주용 미니카. 직접 조립하고 트랙에서 겨룬다."),
            // ── 2일차 ──
            ("beercandy", "맥주사탕", 20, 50, 30, 1.5f, Snack, true, 2, new Color(0.95f, 0.70f, 0.15f), "맥주 거품 맛(?)이 나는 동그란 사탕. 50원의 행복."),
            ("pencil", "연필", 50, 100, 20, 1.0f, Stat, false, 2, new Color(0.40f, 0.65f, 0.30f), "캐릭터가 그려진 연필. 새 학기엔 한 다스씩."),
            // ── 3일차 ──
            ("bouncyball", "탱탱볼", 100, 200, 10, 0.9f, Toy, false, 3, new Color(0.95f, 0.35f, 0.55f), "바닥에 던지면 천장까지 튀는 반짝이 고무공."),
            ("yoyo", "요요", 300, 500, 10, 0.6f, Toy, false, 3, new Color(0.20f, 0.30f, 0.65f), "줄을 감았다 풀며 기술을 겨루는 요요. 붐이 오면 품귀!"),
            // ── 4일차 ──
            ("ssaldaerong", "쌀대롱", 40, 100, 20, 1.2f, Snack, true, 4, new Color(0.95f, 0.90f, 0.75f), "쌀로 만든 길쭉한 대롱 과자. 바삭하고 고소하다."),
            ("bubble", "비눗방울", 150, 300, 10, 0.7f, Toy, false, 4, new Color(0.60f, 0.85f, 0.95f), "후 불면 무지개빛 방울이 둥실. 운동장 필수템."),
            // ── 5일차 ──
            ("card", "캐릭터 카드", 100, 200, 20, 1.0f, Play, false, 5, new Color(0.90f, 0.45f, 0.20f), "한 봉지에 몇 장씩 든 수집 카드. 반짝이 카드가 나오면 대박."),
            ("ppopgi", "번호 뽑기", 50, 100, 30, 1.3f, Play, true, 5, new Color(0.95f, 0.30f, 0.30f), "판에서 종이를 뜯어 번호를 확인! 1등은 커다란 선물."),
            // ── 6일차 ──
            ("rubberband", "고무줄", 100, 200, 10, 0.6f, Play, false, 6, new Color(0.85f, 0.75f, 0.55f), "고무줄놀이용 긴 고무줄. 노래 부르며 폴짝폴짝."),
            ("watergun", "물총", 300, 500, 10, 0.5f, Toy, false, 6, new Color(0.25f, 0.70f, 0.45f), "여름 골목 물싸움의 무기. 펌프식은 사거리가 다르다."),
            // ── 7일차 ──
            ("virtualpet", "전자 펫", 600, 1000, 5, 0.4f, Toy, false, 7, new Color(0.75f, 0.85f, 0.35f), "손바닥 속 전자 애완동물. 밥 주고 똥 치우느라 수업도 뒷전."),
            ("slime", "액체괴물", 150, 300, 10, 0.8f, Toy, false, 7, new Color(0.45f, 0.90f, 0.55f), "말랑말랑 끈적끈적. 통에 담긴 형광색 액체괴물."),
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
                bool created = p == null;
                if (created) p = ScriptableObject.CreateInstance<ProductData>();

                // 표 값으로 생성 또는 갱신 (기존 에셋도 덮어씀)
                p.id = d.id;
                p.displayName = d.name;
                p.wholesalePrice = d.buy;
                p.retailPrice = d.sell;
                p.boxSize = d.box;
                p.popularity = d.pop;
                p.category = d.cat;
                p.korean = d.kr;
                p.unlockDay = d.unlock;
                p.blurb = d.blurb;
                p.color = d.color;

                if (created) AssetDatabase.CreateAsset(p, path);
                else EditorUtility.SetDirty(p);
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
                // 표 순서대로 정렬 (해금 순서 = 도매상 표시 순서). 표에 없는 수동 추가 상품은 뒤에 유지.
                var extra = catalog.products.FindAll(p => p != null && !list.Contains(p));
                catalog.products = new List<ProductData>(list);
                catalog.products.AddRange(extra);
                EditorUtility.SetDirty(catalog);
            }
            Debug.Log($"[문방구] 상품 {list.Count}종 생성/갱신 완료 (목록 {catalog.products.Count}종)");
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
