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

        // BUG-P1-R5-B3 조사 기록(Architect 실측 진단 대응, 2026-08-28) — Architect가 "실제 Retina 화면
        // (실측 1512x949 포인트 vs 3024x1898 백킹 픽셀)에서 낙하 고착/랙돌 폭주가 재발한다"고 지적하며
        // `PlayerSettings.macRetinaSupport`(Unity가 Screen.width/height를 백킹 픽셀로 보고하게 하는
        // 설정)를 의심할 만한 근거로 들었다. 먼저 `PlayerSettings.macRetinaSupport = false`로 꺼서
        // Unity가 `Screen.width`/`height`를 AppKit과 같은 "포인트" 단위로 보고하게 만드는 방법을
        // 시도했으나, **실측으로 확인한 결과 이 프로젝트의 Unity 6 Metal 렌더러에서는 이 설정이
        // `Screen.width`/`height`에 전혀 영향을 주지 않았다**(빌드된 `Info.plist`에 `NSHighResolutionCapable`
        // 키가 사라진 것은 확인했지만, 실행 중인 `.app`의 진단 로그는 이 값을 끈 뒤에도 여전히
        // `screenWH=(3024x1898)`을 보고함 — `NSHighResolutionCapable`은 구형 OpenGL/Quartz 백킹스토어
        // 협상용 힌트라서 Metal 기반 렌더러는 이를 무시하는 것으로 보인다). 그래서 이 접근은 폐기했다
        // (해당 코드는 되돌림).
        //
        // 대신 실측(60초+ 실제 .app 실행, Player.log 임시 디버그)으로 확인한 진짜 원인은 픽셀/포인트
        // 단위 불일치가 **아니라** `Platform/FallbackPlatformWindowService.cs`의 안전망 발판이 (1) 화면
        // 하단 고정 40px에 있어 씬이 가정하는 지면 Y(화면 하단에서 위로 20%)와 어긋났고(BUG-P1-R5-B2로
        // 수정), (2) 폭도 뷰포트 폭 그대로라 `NullPlatformWindowService`의 4배 넓힌 더미 발판과 달리
        // `AutoWanderController`의 최대 Walk 이동거리보다 좁아 실제 배포 환경에서만 가장자리 이탈이
        // 자주 발생했던 것이었다(BUG-P1-R5-B3, 아래 `FallbackPlatformWindowService.cs` 참고) — 둘 다
        // `FallbackPlatformWindowService.cs`/`NullPlatformWindowService.cs` 안에서 완결되며,
        // `Screen.width`/`height`가 물리 픽셀이든 포인트든 그 안전망 자신의 계산과 캐릭터 좌표 변환이
        // "같은 Unity Screen.height/width 값"을 일관되게 재사용하기 때문에 자체적으로 상쇄되어 무관하다
        // (`Platform/ScreenCoordinateConverter.cs`도 동일 값을 왕복 변환에 쓴다). 실측: 두 수정을 모두
        // 적용한 뒤 macRetinaSupport는 원래대로(`true`, Retina 렌더링 유지) 둔 채로 138초+ 연속 실행 —
        // `grounded=False`(낙하) 이벤트 0건.
        //
        // 남은 진짜 한계(정직하게 기록, 다음 라운드 참고): `StickConfig.desktopDpiScale`(기본값 1)은
        // 실제 데스크톱의 다른 진짜 창(`CGWindowListCopyWindowInfo`가 보고하는, AppKit 포인트 단위)을
        // 발판으로 인식하는 경로에는 여전히 보정되지 않은 채 남아 있다 — Retina Mac에서 Unity의
        // 물리픽셀 기준 캐릭터 좌표와 실제 창의 포인트 기준 좌표가 어긋나, "캐릭터가 실제 다른 창 위에
        // 정확히 올라서는" 핵심 기능 자체는 이 라운드에서 실측 검증되지 못했다(이 실행 환경에는 발밑에
        // 밟을 다른 실제 창이 하나도 없어 안전망(위 수정)만 계속 쓰였음 — footholds=1 고정). 이번
        // 라운드는 시간 관계상 "실제 창이 전혀 없어도 절대 낙하 고착되지 않는다"까지만 확정하고, 실제
        // 창 위 정밀 착지의 DPI 보정(예: 네이티브 `NSWindow.backingScaleFactor` 조회로
        // `desktopDpiScale` 자동 설정)은 다음 라운드 과제로 이월한다.
    }
}
