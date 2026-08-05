using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TeachAndFight.Integration.EditorTools
{
    // #20 완료기준: 데모 빌드가 에러 없이 실행되는지 확인용 개발 빌드.
    // Build Settings에 enabled로 체크된 씬만 그대로 사용(프리뷰 씬 제외 여부는 Build Settings에서 관리).
    public static class DemoBuildMenu
    {
        [MenuItem("TeachAndFight/Build/데모 개발 빌드 (Windows)")]
        public static void BuildDemo()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            string outputDir = Path.Combine(Application.dataPath, "..", "DemoBuild");
            outputDir = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "TeachAndFight.exe");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            Debug.Log($"[데모 빌드] 씬 {scenes.Length}개 포함: {string.Join(", ", scenes)}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[데모 빌드] 결과={summary.result} 에러={summary.totalErrors} 경고={summary.totalWarnings} 크기={summary.totalSize}bytes 소요={summary.totalTime}");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.LogError("[데모 빌드] 실패 - 위 에러 확인 필요");
        }
    }
}
