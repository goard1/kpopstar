using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// 폰 테스트용 APK 빌드 (디버그 키로 서명, 스토어 업로드용 아님).
    /// 배치: bash tools/unity-compile.sh apk Munbanggu.EditorTools.BuildAndroid.Apk
    /// 결과: Munbanggu/Builds/munbanggu-test.apk
    /// 참고: ARM64 전용. Unity 6.3은 안드로이드 x86_64 빌드를 막아 두었고,
    ///       LDPlayer 14(ARM 번역)에서는 엔진 초기화 단계에서 멈춘다 → 실제 폰으로 테스트할 것.
    /// </summary>
    public static class BuildAndroid
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("문방구/테스트 APK 빌드")]
        public static void Apk()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "munbanggu-test.apk");

            bool prevBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false; // 폰에 바로 깔 수 있는 APK
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                if (s.result == BuildResult.Succeeded)
                    Debug.Log($"[BuildAndroid] 성공: {outPath} ({s.totalTime.TotalMinutes:F1}분)");
                else
                {
                    Debug.LogError($"[BuildAndroid] 실패: {s.result}, 오류 {s.totalErrors}개");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = prevBundle;
            }
        }
    }
}
