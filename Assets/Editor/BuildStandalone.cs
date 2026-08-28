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
    /// 에디터 UI 안의 패널일 뿐 실제 OS 창이 아니라서, 조작할 진짜 NSWindow 자체가 없었음). 이 클래스는
    /// 그 실제 빌드를 만드는 최소 배치 스크립트다.
    ///
    /// UniWindowController 도입 라운드(2026-08-28) 갱신: 자체 제작 Objective-C 플러그인
    /// (StickMateOverlayPlugin.bundle)을 전부 제거하고 검증된 오픈소스 UniWindowController
    /// (com.kirurobo.uniwinc, UPM git 의존성)로 교체했으므로, 이 스크립트가 그 번들의 PluginImporter를
    /// 손으로 설정하던 ConfigureNativePluginImporter()도 함께 삭제했다 — 패키지에 동봉된
    /// Runtime/Plugins/MacOS/LibUniWinC.bundle은 패키지 자신의 .meta가 이미 플랫폼별 임포트 설정을
    /// 들고 있어(그리고 x86_64+arm64 유니버설로 배포되어) 추가 설정이 필요 없다.
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
        private const string BuildSubFolder = "Builds/macOS";
        private const string AppFileName = "StickMate.app";

        [MenuItem("StickMate/Build Standalone macOS Player")]
        public static void PerformBuild()
        {
            ConfigureRunInBackground();
            ConfigureAntiAliasing();

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
            // 피했다). UniWindowController가 동봉한 LibUniWinC.bundle 자체가 arm64+x86_64 유니버설이므로
            // (lipo -info로 실측 확인), 메인 앱 바이너리가 arm64 전용이든 유니버설이든 플러그인
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

        /// <summary>
        /// PlayerSettings.runInBackground을 true로 강제한다(2026-08-28, 걷기 애니메이션 검증 라운드 —
        /// 실측으로 발견한 사고 대응). 왜 필요한가: 이 프로젝트 기본값(Unity 신규 프로젝트 템플릿의
        /// 기본값)은 false였는데, 이 앱은 클릭관통+항상위 오버레이로 "사용자가 다른 창에서 작업하는
        /// 동안 배경에서 계속 돌아다니는" 것 자체가 핵심 컨셉이다(CLAUDE.md "비침해" 원칙, 클릭관통
        /// 기본 ON). runInBackground=false인 채로는 Unity가 자기 자신이 OS 포그라운드(활성) 앱이 아닌
        /// 순간 게임 루프를 사실상 멈춰버려(Update()가 거의 호출되지 않음), 클릭관통 오버레이의 존재
        /// 이유와 정면으로 모순된다 — 실측으로 확인: 이 값이 false인 빌드를 실행한 뒤 다른 앱(에디터)에
        /// 포커스가 가 있는 상태로 100초+ 관찰하니 Walk 상태 진입 후 겨우 ~3초치 걷기 애니메이션 로그만
        /// 남기고 완전히 멈췄다(Tasklist.md 참고). true로 바꾼 뒤 동일 시나리오 재실측에서 정상적으로
        /// 계속 갱신됨을 확인했다. 매 빌드마다 멱등적으로 강제 적용해 향후 다른 사람이 Player Settings
        /// UI에서 실수로 꺼도 다음 빌드에서 자동으로 복구되게 한다.
        /// </summary>
        public static void ConfigureRunInBackground()
        {
            PlayerSettings.runInBackground = true;
            Debug.Log("[BuildStandalone] PlayerSettings.runInBackground=true 적용 완료 — 오버레이 앱이 " +
                "OS 포그라운드가 아니어도(다른 창 사용 중에도) 계속 시뮬레이션되도록 강제.");
        }

        /// <summary>
        /// 모든 QualitySettings 레벨의 안티에일리어싱(MSAA)을 4x로 강제한다(2026-08-28, 사용자 실측
        /// 지적 "캐릭터 주변으로 픽셀이 깨져보이는데" 대응). ConfigureRunInBackground()와 동일한 멱등
        /// 패턴 — 누군가 Quality Settings UI에서 실수로 꺼도 다음 빌드에서 자동 복구된다.
        ///
        /// 왜 투명 창에서 특히 중요한가: 투명 오버레이에서는 프레임버퍼의 알파 채널 값이 그대로 창의
        /// 픽셀 투명도가 된다. MSAA가 꺼져 있으면 캐릭터 선의 가장자리 알파가 0 아니면 1로만 나와
        /// 윤곽선이 들쭉날쭉한 계단 모양으로 보인다. MSAA를 켜면 가장자리 픽셀이 부분 커버리지에 따라
        /// 중간 알파값을 갖게 되어 부드러운 경계가 만들어진다.
        ///
        /// 4x를 고른 이유: 8x는 이 정도 단순한 2D 라인 렌더링에서 육안 차이가 거의 없으면서 24시간
        /// 상주 앱의 GPU 부담만 늘린다(이 프로젝트의 상주 앱 성격상 4x가 적정선).
        ///
        /// 주의(리더 명시): MSAA는 투명 창 합성에 영향을 줄 수 있으므로 켠 뒤 반드시 투명이 여전히
        /// 정상 동작하는지 실측 재검증할 것 — 투명이 우선순위다.
        /// </summary>
        public static void ConfigureAntiAliasing()
        {
            const int TargetAntiAliasing = 4;

            int originalLevel = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                // 두 번째 인자 false = 레벨 전환 시 무거운 리소스 재적용(ApplyExpensiveChanges)을 건너뛴다.
                // 배치 빌드에서 굳이 각 레벨의 셰이더/텍스처를 다시 로드할 이유가 없다.
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = TargetAntiAliasing;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            Debug.Log($"[BuildStandalone] QualitySettings.antiAliasing={TargetAntiAliasing} 적용 완료 " +
                $"(전체 {names.Length}개 품질 레벨) — 투명 창에서 캐릭터 윤곽선 계단 현상 제거용.");
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
