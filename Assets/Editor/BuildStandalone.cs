using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// "바로 바탕화면에서 구동" 라운드(사용자 명시 요청, 2026-08-28) 대응 — 이 프로젝트가 지금까지 씬/
    /// 프리팹만 만들었을 뿐 한 번도 실제 Standalone 빌드(.app)를 만든 적이 없었다는 것이 macOS 진짜
    /// 오버레이(클릭관통/항상위/투명)를 구현하지 못했던 근본 이유였다(Unity 에디터 Play 모드의 게임뷰는
    /// 에디터 UI 안의 패널일 뿐 실제 OS 창이 아니라서, Platform/MacOS/StickMateOverlayPlugin.m이 조작할
    /// 진짜 NSWindow 자체가 없었음). 이 클래스는 그 실제 빌드를 만드는 최소 배치 스크립트다.
    ///
    /// 사용법:
    /// - 에디터: 메뉴 StickMate/Build Standalone macOS Player.
    /// - 배치 모드: Unity -batchmode -nographics -projectPath <repo>
    ///   -executeMethod StickMate.EditorTools.BuildStandalone.PerformBuild -quit -logFile <path>
    ///   (주의: 실제 빌드는 -quit과 함께 써도 안전하다 — 컴파일/실행 검증 목적의 PlayMode 테스트만
    ///   "-quit 금지" 컨벤션 대상이다. BuildPipeline.BuildPlayer는 그 자체로 완결된 배치 작업이다.)
    ///
    /// 산출물은 Builds/macOS/StickMate.app(신규 폴더, .gitignore의 기존 `[Bb]uilds/` 패턴에 이미
    /// 포함되어 커밋 대상이 아님)에 생성된다.
    /// </summary>
    public static class BuildStandalone
    {
        private const string PluginAssetPath = "Assets/Plugins/macOS/StickMateOverlayPlugin.bundle";
        private const string BuildSubFolder = "Builds/macOS";
        private const string AppFileName = "StickMate.app";

        [MenuItem("StickMate/Build Standalone macOS Player")]
        public static void PerformBuild()
        {
            ConfigureNativePluginImporter();

            string[] scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildStandalone] EditorBuildSettings에 활성화된 씬이 없습니다 — " +
                    "StickMate.EditorTools.SceneBootstrapper.BuildAll을 먼저 실행하세요.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildDir = Path.Combine(projectRoot, BuildSubFolder);
            Directory.CreateDirectory(buildDir);
            string locationPath = Path.Combine(buildDir, AppFileName);

            // 아키텍처는 Player Settings에 이미 설정된 프로젝트 기본값을 그대로 따른다(Unity 6에서
            // EditorUserBuildSettings.macOSXArchitecture 필드가 제거되어 이 스크립트에서 강제로 바꿀
            // 안정적인 공개 API를 확인하지 못했다 — PlayerSettings.SetArchitecture(NamedBuildTarget,int)가
            // 있지만 int 아키텍처 코드가 문서화되어 있지 않아 추측으로 잘못된 값을 설정하는 위험을
            // 피했다). StickMateOverlayPlugin.bundle 자체는 arm64+x86_64 유니버설로 빌드해뒀으므로
            // (Assets/Plugins/macOS/build.sh), 메인 앱 바이너리가 arm64 전용이든 유니버설이든 플러그인
            // 로딩에는 문제가 없다. 이 검증은 arm64 Apple Silicon 개발 머신에서 실행하므로 최소한 이
            // 머신에서는 정상 동작한다 — Intel Mac 배포용 유니버설 강제는 다음 라운드에서 Xcode
            // Build Settings > Architecture UI로 직접 확인 후 처리할 것.
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            Debug.Log("[BuildStandalone] 빌드 시작 -> " + locationPath + " (scenes: " + string.Join(", ", scenes) + ")");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildStandalone] 빌드 결과: {summary.result}, 총 에러 {summary.totalErrors}건, " +
                $"총 경고 {summary.totalWarnings}건, 소요 {summary.totalTime}, 크기 {summary.totalSize} bytes, " +
                $"산출물: {summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[BuildStandalone] 빌드 실패(result=" + summary.result + ") — 위 로그의 에러 메시지를 확인하세요.");
            }
        }

        private static string[] GetEnabledScenePaths()
        {
            var list = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled) list.Add(s.path);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Assets/Plugins/macOS/StickMateOverlayPlugin.bundle을 macOS Standalone 전용(다른 모든
        /// 플랫폼/에디터 비활성, CPU=AnyCPU — 유니버설 바이너리라 아키텍처를 특정하지 않아도 됨)으로
        /// 명시 설정한다.
        ///
        /// 왜 필요한가: Unity의 "Plugins/&lt;플랫폼명&gt; 폴더에 두면 자동으로 그 플랫폼 전용이 된다"는
        /// 매직 동작은 Android/iOS/WebGL 등 모바일/웹 플랫폼 한정이고, 데스크톱 네이티브 플러그인은
        /// 새로 임포트되면 기본적으로 "Any Platform"(모든 플랫폼 + 에디터 포함)으로 활성화된다. 이
        /// 프로젝트는 지금은 macOS만 다루지만 Win32WindowService.cs가 이미 존재하듯 향후 Windows
        /// 빌드도 만들 수 있으므로, 이 macOS 전용 네이티브 코드가 실수로 다른 플랫폼 빌드에 끼어들지
        /// 않도록 명시적으로 잠가야 한다. 에디터 비활성화는 별도로 중요한 안전 장치이기도 하다 — Unity
        /// 에디터 자신의 메인 창을 클릭관통/항상위로 바꿔버리는 사고를 원천 차단한다(MacWindowService.cs
        /// 클래스 문서 참고, 에디터에서는 애초에 StickmanAgent가 이 서비스를 인스턴스화하지도 않지만
        /// PluginImporter 레벨에서도 이중으로 막아둔다).
        /// </summary>
        public static void ConfigureNativePluginImporter()
        {
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(PluginAssetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(PluginAssetPath) as PluginImporter;
            if (importer == null)
            {
                Debug.LogError("[BuildStandalone] " + PluginAssetPath + "의 PluginImporter를 찾지 못했습니다 — " +
                    "번들이 실제로 그 경로에 존재하는지, .bundle 디렉터리 구조가 올바른지 확인하세요.");
                return;
            }

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
            importer.SaveAndReimport();

            Debug.Log("[BuildStandalone] " + PluginAssetPath + " PluginImporter 설정 완료 " +
                "(StandaloneOSX 전용, 에디터 비활성, CPU=AnyCPU).");
        }
    }
}
