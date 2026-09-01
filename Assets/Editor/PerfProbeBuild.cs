using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// 성능 계측 전용 macOS 빌드 — 산출물을 <c>Builds/PerfProbe/StickMate.app</c>에 따로 굽는다.
    ///
    /// <para>왜 <see cref="BuildStandalone.PerformBuild"/>를 그대로 쓰지 않는가: 그쪽은
    /// <c>Builds/macOS/StickMate.app</c>를 덮어쓴다. 그 경로는 다른 팀원이 같은 시각에 실동작
    /// 검증에 쓰고 있을 수 있어(병렬 라운드), 계측 목적의 빌드가 남의 검증 대상을 갈아치우면 안 된다.
    /// 설정 강제(runInBackground / MSAA / 상주 풋프린트)와 씬 목록은 그대로 재사용하므로
    /// <b>제품 빌드와 동일한 바이너리 구성</b>이다.</para>
    ///
    /// <para>배치 실행:
    /// <c>Unity -batchmode -nographics -projectPath &lt;repo&gt;
    /// -executeMethod StickMate.EditorTools.PerfProbeBuild.PerformBuild -quit -logFile &lt;path&gt;</c></para>
    /// </summary>
    public static class PerfProbeBuild
    {
        private const string BuildSubFolder = "Builds/PerfProbe";
        private const string AppFileName = "StickMate.app";

        [MenuItem("StickMate/Build Perf Probe macOS Player")]
        public static void PerformBuild()
        {
            BuildStandalone.ConfigureRunInBackground();
            BuildStandalone.ConfigureAntiAliasing();
            BuildStandalone.ConfigureResidencyFootprint();

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);
            }
            if (scenes.Count == 0)
            {
                Debug.LogError("[PerfProbeBuild] 활성화된 씬이 없습니다 — SceneBootstrapper.BuildAll을 먼저 실행하세요.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildDir = Path.Combine(projectRoot, BuildSubFolder);
            Directory.CreateDirectory(buildDir);
            string locationPath = Path.Combine(buildDir, AppFileName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = locationPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            Debug.Log("[PerfProbeBuild] 빌드 시작 -> " + locationPath);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[PerfProbeBuild] 빌드 결과: {summary.result}, 에러 {summary.totalErrors}건, " +
                $"경고 {summary.totalWarnings}건, 소요 {summary.totalTime}, 산출물: {summary.outputPath}");
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[PerfProbeBuild] 빌드 실패(result=" + summary.result + ")");
            }
        }
    }
}
