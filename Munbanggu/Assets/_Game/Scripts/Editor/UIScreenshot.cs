using System.IO;
using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// UI 검증용 스크린샷 (에디트 모드, 1080x1920).
    /// 배치: bash tools/unity-compile.sh ui Munbanggu.EditorTools.UIScreenshot.Capture
    /// 결과: Munbanggu/Logs/ui-shots/*.png. 세이브 파일은 찍기 전 상태로 되돌린다.
    /// </summary>
    public static class UIScreenshot
    {
        const string CatalogPath = "Assets/_Game/Data/ProductCatalog.asset";
        const string MaterialPath = "Assets/_Game/Materials/Graybox.mat";
        const int Width = 1080, Height = 1920;

        [MenuItem("문방구/UI 스크린샷 찍기")]
        public static void Capture()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ui-shots"));
            Directory.CreateDirectory(outDir);
            string savePath = Path.Combine(Application.persistentDataPath, "save.json");
            byte[] backup = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            RenderTexture rt = null;
            GameObject root = null;
            try
            {
                if (backup != null) File.Delete(savePath); // 항상 새 게임(1일차) 상태로 찍기
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.58f, 0.54f);

                var catalog = AssetDatabase.LoadAssetAtPath<ProductCatalog>(CatalogPath);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (catalog == null) { Debug.LogError("[UIScreenshot] 상품 목록이 없습니다"); return; }

                root = new GameObject("Game");
                var gm = root.AddComponent<GameManager>();                gm.Init(catalog, mat);
                var hud = gm.Hud;
                if (gm.Dialogue != null) gm.Dialogue.Clear();

                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                var cam = gm.Layout.Camera;
                cam.targetTexture = rt;
                var shopCam = cam.GetComponent<ShopCamera>();
                if (shopCam != null) shopCam.Fit();
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!c.isRootCanvas) continue;
                    var scaler = c.GetComponent<CanvasScaler>();
                    if (scaler != null) scaler.enabled = false;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f + c.sortingOrder * 0.01f;
                    c.scaleFactor = 1f;
                }
                var tutorial = hud.ForceTutorial();

                // 1) 아침: 도매상 창
                Shot(hud, cam, rt, outDir, "1_morning_wholesale");

                // 1b) 아침 도매상 위에 설정 창 (톱니는 모달 위에 보임)
                hud.OpenSettings();
                Shot(hud, cam, rt, outDir, "1b_settings_over_wholesale");
                hud.CloseSettings();

                // 2) 도매상 닫고 창고 (상품 2개 구매)
                int bought = 0;
                foreach (var p in catalog.products)
                    if (p != null && gm.IsUnlocked(p) && bought < 3 && gm.Money >= gm.BoxCost(p) && gm.BuyBox(p)) bought++;
                Shot(hud, cam, rt, outDir, "2_wholesale_bought");
                hud.CloseModals();
                if (tutorial != null) for (int i = 0; i < 160; i++) tutorial.Tick(0.016f); // 2단계(고른 상품 강조) 지나가기
                Shot(hud, cam, rt, outDir, "3_backroom_select");

                // 3) 매대에 진열 + "+N" 팝업
                if (gm.Layout.Slots.Count > 0) gm.OnSlotTapped(gm.Layout.Slots[0]);
                hud.Fx.Tick(0.25f);
                Shot(hud, cam, rt, outDir, "4_placed_popup");

                // 4) 영업 중: 말풍선·동전·배너·토스트
                gm.OpenShop();
                hud.Fx.FreezeBannerForCapture();
                var slots = gm.Layout.Slots;
                if (slots.Count > 2)
                {
                    var a = hud.CreateBubble(slots[1].transform);
                    a.SetIcon(Munbanggu.Visual.ProductIcons.Get(catalog.Find("ddakji")));
                    a.Show("딱지 200원", UIKit.BubbleReady, true, 0.65f);
                    var b = hud.CreateBubble(slots[2].transform);
                    b.SetIcon(Munbanggu.Visual.ProductIcons.Get(catalog.Find("apollo")));
                    b.Show("빨리요~", UIKit.BubbleUrgent, false, 0.2f);
                    var c = hud.CreateBubble(slots[0].transform);
                    c.SetIcon(Munbanggu.Visual.ProductIcons.Get(catalog.Find("jjondeugi")));
                    c.Flash("쫀드기 있어요?", UIKit.BubbleNormal, 1.4f);
                    hud.Fx.CoinBurst(slots[1].transform.position + Vector3.up * 1.5f, 300);
                    hud.Fx.Tick(0.3f);
                }
                hud.Toast("매대가 꽉 찼어요");
                Shot(hud, cam, rt, outDir, "5_open_bubbles");

                // 4b) 영업 중 설정 창 + 소리 끈 상태
                hud.OpenSettings();
                Shot(hud, cam, rt, outDir, "5b_settings_open_shop");
                var audio = Munbanggu.Audio.AudioManager.Instance;
                bool wasMuted = audio != null && audio.IsMuted; // PlayerPrefs에 쓰므로 끝나면 원래 값으로
                if (audio != null) // 배치 모드엔 AudioManager가 없을 수 있음
                {
                    audio.SetMuted(true); hud.CloseSettings(); hud.OpenSettings();
                    Shot(hud, cam, rt, outDir, "5c_settings_muted");
                    audio.SetMuted(wasMuted);
                }
                hud.CloseSettings();

                // 4c) 돌발 사건: 외상 선택 창
                hud.ShowChoice("외상 장부",
                    "꼬마 손님이 미니카를 외상으로 달래요. (1,000원)\n\n해 주면 내일 갚으러 올지도 몰라요. (고맙다고 덤까지?)\n안 해 주면 그냥 살 수도, 삐져서 갈 수도 있어요.",
                    "외상 해 줄게", "안 돼", _ => { });
                Shot(hud, cam, rt, outDir, "5d_credit_choice");
                hud.CloseChoice();

                // 4d) 상담 손님 고르기 창
                var req = Munbanggu.Customers.CounselRequests.Roll(gm.UnlockedCounselable());
                if (req != null)
                {
                    hud.ShowPicker(req, _ => { });
                    Shot(hud, cam, rt, outDir, "5e_counsel_picker");
                    hud.CancelPicker();
                }

                // 5) 정산 (장부)
                hud.ShowSettlement();
                Shot(hud, cam, rt, outDir, "6_settlement");

                // 6) 업그레이드: 상점 화면 + 전부 산 가게 모습
                gm.DebugMorningForCapture();
                hud.OpenUpgrades();
                Shot(hud, cam, rt, outDir, "7_upgrades");
                gm.DebugMaxUpgrades();
                hud.CloseModals();
                Shot(hud, cam, rt, outDir, "8_upgraded_shop");

                // 7) 직원: 지원자 고용 + 아이템 → 직원 창, 직원이 선 가게
                foreach (var cand in gm.StaffCandidates()) gm.HireStaff(cand);
                gm.BuyStaffItem("calculator");
                if (gm.Staff.members.Count > 0) gm.CycleStaffItem(gm.Staff.members[0]);
                hud.OpenStaff();
                Shot(hud, cam, rt, outDir, "9_staff");
                hud.CloseModals();
                Shot(hud, cam, rt, outDir, "10_staff_shop");

                // 8) 장식·콤보: 배치 모드 표시, 진열대 장식 창
                gm.DebugMorningForCapture();
                gm.DebugDecorForCapture();
                hud.CloseModals();
                Shot(hud, cam, rt, outDir, "11_decor_shop");
                hud.EnterDecorMode();
                Shot(hud, cam, rt, outDir, "12_decor_mode");
                hud.OpenDecor(0);
                Shot(hud, cam, rt, outDir, "13_decor_panel");
                hud.CloseModals();
                hud.OpenUpgrades();
                Shot(hud, cam, rt, outDir, "14_upgrades_combos");

                Debug.Log($"[UIScreenshot] 저장 완료: {outDir}");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (rt != null) { RenderTexture.active = null; Object.DestroyImmediate(rt); }
                if (backup != null) File.WriteAllBytes(savePath, backup);
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
        }

        static void Shot(HUD hud, Camera cam, RenderTexture rt, string dir, string name)
        {
            for (int i = 0; i < 2; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
                hud.SnapForCapture();
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new UnityEngine.Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
