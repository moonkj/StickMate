using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

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

        // 윈도우 지원 라운드(2026-08-30). macOS 경로 상수와 나란히 두되 값은 완전히 분리한다 —
        // 한쪽 빌드가 다른 쪽 산출물을 덮어쓰는 사고를 구조적으로 없앤다.
        private const string WindowsBuildSubFolder = "Builds/Windows";
        private const string WindowsExeFileName = "StickMate.exe";

        [MenuItem("StickMate/Build Standalone macOS Player")]
        public static void PerformBuild()
        {
            ConfigureRunInBackground();
            ConfigureAntiAliasing();
            ConfigureResidencyFootprint();
            ConfigureRenderDiagnostics();

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
        /// ============================================================================
        /// ★ 2026-08-29 실측 확정 — 이 값을 8로 올리지 마라(Apple Silicon에서 무의미하다)
        /// ============================================================================
        /// "캐릭터 선이 저해상도로 보임" 조사 라운드에서 8x를 실제로 빌드해 실행하고 **합성된 화면
        /// 픽셀을 직접 계측**했다. 결과:
        ///
        ///   요청 2x -> 가장자리 커버리지 단계 실측 2개 {0, 0.50}          (283 샘플)
        ///   요청 4x -> 가장자리 커버리지 단계 실측 4개 {0, .25, .50, .75} (279 샘플)
        ///   요청 8x -> 가장자리 커버리지 단계 실측 **4개**(4x와 동일)      (301 샘플)
        ///
        /// 즉 2x/4x는 요청한 만큼 정확히 반영되는데(계측 방법이 샘플 수를 제대로 추적한다는 대조군),
        /// 8x만 실제 결과가 4x와 완전히 동일하다 — Apple GPU의 Metal이 지원하지 않는 샘플 수를 조용히
        /// 4로 낮추기 때문이다. **그런데 `Screen.msaaSamples`는 그때도 8을 그대로 보고한다**(실측
        /// 로그: "MSAA 요청=8x, 실측 Screen.msaaSamples=8x"). 엔진 API만 믿으면 "8x가 걸렸는데 왜
        /// 안 좋아지지?"에 갇히므로, 이 프로젝트에서 MSAA를 다시 의심하게 되면 API가 아니라
        /// **화면 픽셀의 가장자리 커버리지 단계 수**를 세라(Platform/MacOS/MacOverlayStateEnforcer.cs의
        /// [렌더품질] 로그가 요청값/실측값을 나란히 남긴다).
        ///
        /// 게다가 8x는 8x를 **실제로 지원하는** 하드웨어(Intel/AMD Mac, Windows GPU — 이 프로젝트의
        /// 다음 타깃)에서는 전체화면 멀티샘플 버퍼를 두 배로 잡아 상주 앱의 메모리만 늘린다. 즉 8x는
        /// "이 기기에서는 효과 0, 다른 기기에서는 비용만 발생"이라 어느 쪽으로도 이득이 없다.
        ///
        /// 결론: 이 하드웨어의 안티에일리어싱 상한은 4x다. 캐릭터 윤곽의 계단이 더 줄기를 원한다면
        /// MSAA가 아니라 **획 두께 / 캐릭터 크기**(StickConfig.characterScale)를 건드려야 한다 —
        /// 근거는 아래 "실측으로 닫힌 가설" 메모.
        ///
        /// 주의(리더 명시): MSAA는 투명 창 합성에 영향을 줄 수 있으므로 켠 뒤 반드시 투명이 여전히
        /// 정상 동작하는지 실측 재검증할 것 — 투명이 우선순위다.
        ///
        /// <para><b>★ 2026-08-31 성능 라운드 결론 — "부하를 줄이려고" 이 값을 내리지 마라
        /// (docs/ARCHITECTURE.md 6-16절)</b>. present를 60fps에 고정한 채 4x vs 0x를 콜드 스타트
        /// 페어드로 6쌍 측정한 결과:
        /// <list type="bullet">
        /// <item><b>CPU/GPU 절감은 검출되지 않았다</b>(WindowServer 2/6, 앱 CPU 3/6, GPU 3/6 —
        ///   부호조차 일정하지 않음). Apple GPU는 TBDR이라 MSAA resolve가 타일 메모리 안에서 끝나
        ///   대역폭이 4배가 되지 않는다.</item>
        /// <item><b>대신 메모리는 정확히 93MB 차이가 난다</b>(6/6, σ=0.43MB). 즉 이 값은
        ///   <b>부하 손잡이가 아니라 메모리 손잡이</b>다.</item>
        /// <item>화질: 0x는 명백한 회귀(평균 Δ휘도 30.9/255), <b>2x는 4x와 거의 구분 불가</b>
        ///   (평균 Δ 4.65/255 = 1.8%). 메모리 46MB가 급할 때만 2x를 검토하고, 그때도
        ///   6-16절 (5)의 화질 표를 먼저 읽을 것.</item>
        /// </list>
        /// 또한 <b>런타임에 QualitySettings.antiAliasing을 바꿔도 백버퍼에 반영되지 않는다</b>
        /// (Screen.msaaSamples는 그 사실을 감춘다). 그래서 이 값을 바꾸는 유효한 지점은 여전히
        /// 여기(빌드 시점) 또는 프로세스 시작 전(Platform/RenderQualityTuner.cs)뿐이다.</para>
        ///
        /// <para>============================================================================<br/>
        /// <b>★ 2026-09-01 — 이 4x는 이제 "macOS 기준값"이다. Windows는 런타임에 덮어쓴다.</b><br/>
        /// ============================================================================<br/>
        /// 사용자 실기 A/B(콜드 스타트)에서 <b>Windows만</b> MSAA 배수에 따라 체감 렉이 갈렸다
        /// (4x 렉 심함 / 2x 봐줄만함 / 0x 렉 줄지만 화질 "지저분함"). macOS는 위 6쌍 실측대로
        /// 절감이 검출되지 않으므로 <b>4x가 순이득</b>이다. 같은 값이 두 GPU 구조에서 다른 물건이라는
        /// 뜻이라, 한쪽에 맞춰 다른 쪽을 깎지 않는다.</para>
        ///
        /// <para><b>그래서 플랫폼 분기를 이 함수(빌드 시점)가 아니라
        /// <c>Platform/RenderQualityTuner.WindowsDefaultSamples</c>(프로세스 시작 시점)에 두었다.</b>
        /// 여기서 나누지 않은 이유:
        /// <list type="number">
        ///   <item><c>QualitySettings.asset</c>은 <b>두 플랫폼이 공유하는 단일 에셋</b>이다. 빌드 경로마다
        ///     다른 값을 쓰면 macOS 빌드와 Windows 빌드를 번갈아 할 때마다 그 에셋이 4 &lt;-&gt; 2로
        ///     뒤집히며 <b>git 디프가 매번 더러워지고</b>, "마지막에 빌드한 쪽이 이긴다"는 상태가 된다.</item>
        ///   <item>런타임 분기는 <b>사용자가 실기에서 이미 검증한 바로 그 경로</b>다 —
        ///     <c>STICKMATE_FORCE_MSAA</c>가 효과를 낸 곳이 <c>BeforeSceneLoad</c>이고, 기본값도
        ///     같은 지점에서 적용된다. 즉 출시 구성이 검증된 구성과 동일하다.</item>
        ///   <item>누군가 Quality Settings UI를 만져도 Windows 실행 시 <b>자동으로 교정</b>된다
        ///     (이 함수의 멱등 복구는 빌드할 때만 도는 반면, 런타임 교정은 매 실행 도는다).</item>
        /// </list>
        /// 이 함수는 계속 4x를 박는다 — 그게 macOS 값이자 에디터 작업 기준값이기 때문이다.</para>
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

        /// <summary>
        /// <b>GPU 프레임 시간 계측을 켠다</b>(<c>PlayerSettings.enableFrameTimingStats</c>).
        /// 2026-09-01 "윈도우만 렉" 라운드에서 추가.
        ///
        /// <para><b>왜 필요한가 — 이게 없으면 Windows 렌더 비용을 측정할 방법이 아예 없다.</b>
        /// 이 앱은 60fps 상한이 걸려 있어서, GPU가 한 프레임에 9ms를 쓰든 3ms를 쓰든
        /// <b>CPU 프레임 시간은 똑같이 16.7ms로 보인다</b>. 즉 프레임 시간만 보면 MSAA를 4x에서 0x로
        /// 내려도 "차이 없음"이라는 잘못된 결론이 나온다. 그런데 그 6ms는 사라진 게 아니라
        /// <b>사용자의 다른 앱이 쓸 수 있었던 GPU 시간</b>이고, 그것이 정확히 신고 "앱 수치는 낮은데
        /// 시스템이 느려짐"의 정체다. 이 설정을 켜야 <c>FrameTimingManager</c>가 GPU 프레임 시간을
        /// 돌려주고, <c>Platform/FramePacing.cs</c>의 <c>RenderDiagnostics</c>가 그 값을 로그에 남긴다.</para>
        ///
        /// <para><b>개발 머신이 macOS라 Windows 실기 프로파일러를 붙일 수 없다</b>는 이 프로젝트의
        /// 구조적 제약 때문에, 앱이 스스로 찍는 이 숫자가 사실상 유일한 원격 계측 수단이다.</para>
        ///
        /// <para><b>대가(정직하게)</b>: 프레임마다 GPU 타이머 질의가 붙는다. 오버헤드는 작다고
        /// 알려져 있지만 0은 아니다. 24시간 상주 앱이므로, 훗날 이 항목이 실측에서 유의미한 비용으로
        /// 잡히면 이 한 줄을 false로 돌리면 된다(그 순간 진단 로그는 "측정 불가"라고 스스로 밝힌다 —
        /// 0을 진짜 값인 척 찍지 않는다). 수집 자체도 <c>RenderDiagnostics</c>가 시작 후 60초만 하고
        /// 멈춘다.</para>
        ///
        /// <para>두 빌드 경로가 모두 부른다 — 플랫폼 간 진단 능력이 갈리면 "한쪽에서만 재현되는 문제"를
        /// 비교할 수 없게 되기 때문이다(이번 라운드가 정확히 그 상황이었다).</para>
        /// </summary>
        public static void ConfigureRenderDiagnostics()
        {
            if (!PlayerSettings.enableFrameTimingStats)
            {
                PlayerSettings.enableFrameTimingStats = true;
            }

            Debug.Log("[BuildStandalone] enableFrameTimingStats=true 적용 완료 — " +
                "GPU 프레임 시간 계측 활성화. 60fps 상한에 가려 보이지 않는 렌더 비용을 " +
                "[렌더진단] 로그로 드러내기 위한 설정.");
        }

        /// <summary>
        /// 24시간 상주 앱의 **메모리/CPU 상시 점유**를 줄이는 프로젝트 설정 2종을 강제한다
        /// (2026-08-31 성능 라운드, 사용자 신고 "메모리 185MB / CPU 1.5%" 대응).
        /// ConfigureRunInBackground/ConfigureAntiAliasing과 같은 멱등 패턴이다.
        ///
        /// ============================================================================
        /// 왜 이 두 개인가 — 추측이 아니라 실행 중인 .app을 계측해서 골랐다
        /// ============================================================================
        /// 실행 중인 빌드(17분 경과, 유휴 = 캐릭터가 걷기만 하는 상태)를 `vmmap`으로 뜯어보니
        /// 물리 풋프린트 543MB 중 **압도적 다수가 GPU 프레임버퍼**였다. 텍스처/폰트/메시/코드가
        /// 아니다(이 라운드 이전의 통념을 실측이 뒤집었다):
        ///
        ///   owned unmapped (graphics)  222.3MB  <- 이 중 121.0MB + 96.0MB 두 덩어리가 전부다
        ///   IOSurface                   71.1MB  <- 3024x2020 BGRA 'CAMetalLayer Display Drawable' x3
        ///   MALLOC(관리 힙 전체)         49.2MB
        ///   __FONT_DATA                  2352B  <- 폰트는 사실상 0이다. 한글 폰트 용량 가설은 기각.
        ///
        /// 화면은 3024x2020 = 6,108,480픽셀이다. 여기서 두 덩어리의 정체가 산수로 확정된다:
        ///   · 96.0MB  = 6,108,480 x 4바이트(BGRA) x **4샘플** = 97.7MB -> MSAA 4x **컬러** 버퍼
        ///   · 121.0MB = 6,108,480 x 5바이트(depth32f+stencil8) x **4샘플** = 116.5MB(+타일 정렬)
        ///               -> MSAA 4x **깊이+스텐실** 버퍼
        ///   · 71.1MB  = 23.7MB x 3 -> CAMetalLayer 트리플 버퍼(WindowServer와 공유, 투명 합성용)
        ///
        /// 즉 **앱 메모리의 40%가 MSAA 4x 전체화면 프레임버퍼**이고, 그 중 121MB는 이 2D 앱이
        /// 한 번도 쓰지 않는 깊이/스텐실이다.
        ///
        /// ----------------------------------------------------------------------------
        /// (1) disableDepthAndStencilBuffers = true — 위 121MB를 겨냥한다
        /// ----------------------------------------------------------------------------
        /// 이 앱이 깊이/스텐실을 쓰지 않는다는 근거(전수 확인):
        ///   · 렌더링이 전부 2D 투명 큐다(LineRenderer / Sprite / uGUI). 정렬은 깊이 테스트가 아니라
        ///     화가 알고리즘(렌더 큐 + sortingOrder)으로 이뤄진다.
        ///   · uGUI 마스킹은 <c>RectMask2D</c>만 쓴다(전수 검색 확인). RectMask2D는 스텐실이 아니라
        ///     셰이더 사각형 클리핑이다. 스텐실을 쓰는 <c>Mask</c> 컴포넌트는 **0건**이다.
        ///
        /// ★ 주의: 이 설정은 Unity 인스펙터에서 모바일 타깃에만 노출된다. macOS Standalone/Metal에서
        ///   실제로 먹는지는 **문서로 보장되지 않는다**. 그래서 이 라운드는 켠 뒤 빌드해서
        ///   `vmmap`으로 121MB 영역이 사라졌는지 직접 확인했다 — 결과는 Tasklist.md에 기록.
        ///   먹지 않는다면 이 줄은 무해한 no-op이다(2D 앱이라 어차피 깊이를 안 쓴다).
        ///
        /// ----------------------------------------------------------------------------
        /// (2) m_DisableAudio = true — 24시간 돌아가던 오디오 장치를 끈다
        /// ----------------------------------------------------------------------------
        /// 이 프로젝트에는 **오디오 자산이 하나도 없다**(AudioSource/AudioClip/PlayOneShot 전수 검색
        /// 0건 — Core/ItemCatalog.cs 주석도 같은 사실을 적어두고 있다). 그런데 `sample`로 실행 중인
        /// 프로세스의 스레드를 뜯어보니 오디오가 **실제로 돌고 있었다**:
        ///
        ///   Thread: com.apple.audio.IOThread.client
        ///     -> HALC_ProxyIOContext::IOWorkLoop()
        ///        -> FMOD::OutputCoreAudio::renderProc()
        ///           -> FMOD::Output::mix()  ...  FMOD::DSPFilter::read() (무음을 계속 믹싱 중)
        ///
        /// Unity는 씬에 소리가 없어도 FMOD를 초기화하고 CoreAudio 출력 장치를 연다. 그 결과
        /// (a) 오디오 IO 스레드가 버퍼 주기마다(512샘플) 24시간 깨어나고, (b) caulk 메신저 스레드 3개가
        /// 함께 붙고, (c) **오디오 하드웨어 전력 도메인이 계속 살아 있다**. 상주 앱에서 이건 순수 낭비다.
        /// CPU 지분 자체는 작지만(측정 0.2%), 배터리에서 오디오 장치를 붙잡고 있는 비용은 CPU%로
        /// 드러나지 않는 종류의 비용이다.
        ///
        /// UX 영향 0: 재생할 소리가 애초에 없다. 나중에 효과음을 넣게 되면 이 줄을 되돌려야 한다
        /// (그때는 이 주석이 그 사실을 알려줄 것이다).
        ///
        /// <para>구현 메모: 둘 다 <c>PlayerSettings</c>/<c>AudioSettings</c>에 안정적인 공개 세터가
        /// 없어서 <see cref="SerializedObject"/>로 직접 쓴다. 이 프로젝트가 이미 쓰는 "추측하지 말고
        /// 있는 API만 쓴다" 원칙에 맞추기 위해, 프로퍼티를 못 찾으면 조용히 실패하는 대신
        /// <b>경고를 남기고</b> 빌드는 계속한다(설정 하나 때문에 빌드가 깨지면 안 된다).</para>
        /// </summary>
        public static void ConfigureResidencyFootprint()
        {
            bool depthOk = TrySetProjectSettingsBool(
                "ProjectSettings/ProjectSettings.asset", "disableDepthAndStencilBuffers", true);
            bool audioOk = TrySetProjectSettingsBool(
                "ProjectSettings/AudioManager.asset", "m_DisableAudio", true);

            Debug.Log($"[BuildStandalone] 상주 앱 풋프린트 설정 적용 — " +
                $"disableDepthAndStencilBuffers=true({(depthOk ? "성공" : "실패")}), " +
                $"m_DisableAudio=true({(audioOk ? "성공" : "실패")}). " +
                "전자는 실측 121MB(MSAA 4x 깊이+스텐실, 이 2D 앱은 미사용)를, 후자는 24시간 도는 " +
                "FMOD/CoreAudio 출력 스레드(오디오 자산 0건)를 겨냥한다.");
        }

        /// <summary>
        /// ProjectSettings 폴더의 설정 에셋 하나에서 bool 프로퍼티를 멱등적으로 쓴다.
        /// 값이 이미 원하는 값이면 아무것도 하지 않는다(불필요한 파일 dirty 방지).
        /// </summary>
        private static bool TrySetProjectSettingsBool(string assetPath, string propertyPath, bool value)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogWarning($"[BuildStandalone] {assetPath}를 열지 못했습니다 — " +
                    $"{propertyPath} 설정을 건너뜁니다(빌드는 계속합니다).");
                return false;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty(propertyPath);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildStandalone] {assetPath}에 '{propertyPath}' 프로퍼티가 없습니다 " +
                    "(Unity 버전에 따라 이름이 다를 수 있음) — 설정을 건너뜁니다(빌드는 계속합니다).");
                return false;
            }

            if (prop.boolValue != value)
            {
                prop.boolValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            return true;
        }

        // ============================================================================
        // Windows Standalone 빌드 (2026-08-30 윈도우 지원 라운드)
        // ============================================================================

        /// <summary>
        /// Windows(x64) Standalone Player를 <c>Builds/Windows/StickMate.exe</c>에 굽는다.
        ///
        /// macOS용 <see cref="PerformBuild"/>를 플랫폼 인자로 일반화하지 않고 **별도 메서드**로 둔 이유:
        /// 두 플랫폼이 필요로 하는 Player Settings 사전 조정이 서로 다르다(아래
        /// <see cref="ConfigureWindowsTransparencySettings"/>는 Windows 전용 D3D 설정을 건드린다).
        /// 공통 메서드에 플래그를 넘기는 형태로 만들면 그 분기가 macOS 경로 안으로 들어오게 되는데,
        /// macOS 빌드는 이미 실동작 검증이 끝난 경로라 한 줄도 건드리지 않는 편이 안전하다.
        /// 실제로 공유해야 할 부분(씬 목록/runInBackground/MSAA/결과 로깅)은 이미 별도 메서드로
        /// 뽑혀 있어 두 경로가 그대로 재사용한다.
        ///
        /// 사용법:
        /// - 에디터: 메뉴 StickMate/Build Standalone Windows Player.
        /// - 배치 모드: Unity -batchmode -nographics -projectPath &lt;repo&gt; -buildTarget Win64
        ///   -executeMethod StickMate.EditorTools.BuildStandalone.PerformBuildWindows -quit -logFile &lt;path&gt;
        ///
        /// **이 개발 환경(macOS)의 한계 — 반드시 기억할 것**: Unity에 Windows Standalone 모듈이 설치돼
        /// 있으면 여기서 .exe를 크로스 컴파일까지는 할 수 있지만, 그 .exe를 실행해 투명/항상위/
        /// 클릭관통이 실제로 동작하는지는 **이 환경에서 검증할 수 없다**. 최종 실동작 확인은 실제
        /// Windows 머신에서 사용자가 수행해야 한다(Tasklist.md에 동일 내용 기록).
        /// </summary>
        [MenuItem("StickMate/Build Standalone Windows Player")]
        public static void PerformBuildWindows()
        {
            ConfigureRunInBackground();
            ConfigureAntiAliasing();
            ConfigureResidencyFootprint();
            ConfigureRenderDiagnostics();
            ConfigureWindowsTransparencySettings();

            string[] scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildStandalone] EditorBuildSettings에 활성화된 씬이 없습니다 — " +
                    "StickMate.EditorTools.SceneBootstrapper.BuildAll을 먼저 실행하세요.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildDir = Path.Combine(projectRoot, WindowsBuildSubFolder);
            Directory.CreateDirectory(buildDir);
            string locationPath = Path.Combine(buildDir, WindowsExeFileName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            Debug.Log("[BuildStandalone] Windows 빌드 시작 -> " + locationPath +
                " (scenes: " + string.Join(", ", scenes) + ")");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildStandalone] Windows 빌드 결과: {summary.result}, 총 에러 {summary.totalErrors}건, " +
                $"총 경고 {summary.totalWarnings}건, 소요 {summary.totalTime}, 크기 {summary.totalSize} bytes, " +
                $"산출물: {summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[BuildStandalone] Windows 빌드 실패(result=" + summary.result + ") — 위 로그의 " +
                    "에러 메시지를 확인하세요. 이 머신에 Windows Standalone 모듈이 설치돼 있지 않으면 " +
                    "여기서 실패합니다(Unity Hub > Add modules > Windows Build Support (IL2CPP/Mono)).");
            }
        }

        /// <summary>
        /// Windows에서 **투명 창이 실제로 합성되기 위한** Player Settings를 강제한다.
        /// ConfigureRunInBackground/ConfigureAntiAliasing과 같은 멱등 패턴이며, 호출 지점도
        /// <see cref="PerformBuildWindows"/> 하나뿐이다 — macOS 빌드 경로는 이 함수를 부르지 않으므로
        /// 지금 잘 동작하는 macOS 설정에 어떤 영향도 주지 않는다.
        ///
        /// 두 항목 모두 UniWindowController 패키지의 에디터 검증(UniWindowControllerEditor.cs의
        /// ShowPlayerSettingsValidation)이 "고치라"고 경고하는 항목을 코드로 옮긴 것이다:
        ///
        ///   (1) useFlipModelSwapchain = false
        ///       Flip Model 스왑체인은 DWM이 창을 합성하는 경로가 달라져 레이어드 창의 픽셀별 알파가
        ///       먹지 않는다(= 투명 실패). 이 값은 Windows(D3D) 전용 설정이라 macOS(Metal) 빌드에는
        ///       아무 의미가 없다 — 그래서 프로젝트 전역 설정이어도 macOS 회귀 위험이 없다.
        ///
        ///   (2) Graphics APIs for Windows = Direct3D11 고정(Auto 해제)
        ///       Direct3D12는 투명 창을 지원하지 않는다(패키지 경고문 원문). Auto로 두면 Unity가
        ///       환경에 따라 D3D12를 고를 수 있으므로, 추측에 맡기지 않고 D3D11로 못 박는다.
        ///       StandaloneWindows(32)와 StandaloneWindows64 양쪽에 거는 이유는 패키지의 검증 코드가
        ///       32비트 타깃 키로 조회하기 때문이다(실제 빌드는 64비트만 한다).
        ///
        /// 여기서 손대지 않는 것(의도적): resizableWindow / fullScreenMode / allowFullscreenSwitch.
        /// 패키지는 이 셋도 권장하지만 **전부 macOS와 공유되는 전역 설정**이라, 지금 정상 동작 중인
        /// macOS 빌드에 회귀를 줄 수 있다. 그리고 이 프로젝트는 씬의 UniWindowController에
        /// forceWindowed=true가 이미 켜져 있어(SceneBootstrapper.ConfigureUniWindowController) 시작 시
        /// 전체화면이 자동 해제되므로 실질적으로 같은 결과를 얻는다. 실제 Windows 머신에서 창이
        /// 전체화면으로 뜨는 문제가 관측되면 그때 이 셋을 함께 조정할 것(추측으로 미리 바꾸지 않는다).
        /// </summary>
        public static void ConfigureWindowsTransparencySettings()
        {
            PlayerSettings.useFlipModelSwapchain = false;

            var d3d11Only = new[] { GraphicsDeviceType.Direct3D11 };
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows, d3d11Only);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, d3d11Only);

            Debug.Log("[BuildStandalone] Windows 투명 창 전제 조건 적용 완료 — " +
                "useFlipModelSwapchain=false, Graphics APIs(Windows/Windows64)=Direct3D11 고정. " +
                "(둘 다 D3D 전용 설정이라 macOS 빌드에는 영향 없음.)");
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
        // ★ 위 실험 기록의 정정 + 후속 처리 (2026-08-29 Retina 대응 라운드)
        // ----------------------------------------------------------------------------
        // 위 문단이 "폐기했다(코드는 되돌림)"고 적어 둔 것과 달리, **ProjectSettings의
        // `macRetinaSupport` 값만 0인 채로 남아 있었다**(코드는 되돌렸지만 설정은 아니었다). 그 잔재가
        // 사용자 신고 "전체적으로 해상도가 너무 안좋음"의 직접 원인이었다 — 실측: 실행 로그
        // `Screen=(1512x982)` vs `system_profiler`의 `3024x1964 Retina`, 그리고 빌드 Info.plist에
        // `NSHighResolutionCapable` 키 없음. 앱만 1x로 그려지고 OS가 2배로 확대하고 있었다.
        //
        // 위 문단의 "이 설정은 Screen.width/height에 전혀 영향을 주지 않았다"는 관찰은 **방향이 반대로
        // 기록된 것**이다. 이번 라운드에 0 -> 1로 되돌리고 실측한 결과 Screen이 실제로 3024x1964로
        // 바뀌었다(즉 이 설정은 Metal 렌더러에서도 정상 동작한다).
        //
        // 함께 처리한 것(그때 "다음 라운드 과제로 이월"이라고 적어 둔 DPI 보정이 이번 라운드다):
        //   · `StickConfig.desktopDpiScale`의 의미를 "수동 오버라이드(0 이하 = 자동)"로 바꾸고, 실제
        //     배율은 `Platform/ScreenCoordinateConverter`가 창 폭(OS 포인트)/Screen.width(Unity 픽셀)로
        //     **매 발판 폴링마다 실측**한다(하드코딩 0.5 금지 — 외장 모니터/비Retina 환경 대응).
        //   · 좌표 소비자 전원(발판/Dock/안전망/화면 클램프/커서/클릭/드래그)이 그 단일 소스를 거치게 정리.
        //   · ScreenSpaceOverlay 캔버스 3종에 `CanvasScaler.scaleFactor = 1/dpi` — UI의 물리적 크기는
        //     그대로 두고 해상도만 2배로 올린다.
        //   · 잠금 테스트: Tests/PlayMode/RetinaDpiCoordinateTests.cs(배율 1/2 양쪽 왕복 항등).

        // ============================================================================
        // ★ "캐릭터 선이 저해상도로 보임" 라운드 — 실측으로 닫힌 가설 (2026-08-29)
        // ============================================================================
        // 사용자 신고 "캐릭터 해상도가 좀 낮아 보임"에 대해 네 가지 가설을 세우고, **추측으로 값을
        // 바꾸기 전에** 실행 중인 .app의 합성된 화면 픽셀을 직접 계측해서 하나씩 닫았다. 앞으로 같은
        // 신고가 다시 오면 아래를 재실험하지 말고 결론부터 읽어라.
        //
        // [닫힘] 가설 A — "렌더 해상도가 낮고 OS가 확대한다(Retina 회귀)"
        //   반증: 화면 캡처에서 캐릭터 가장자리에 **1픽셀짜리 고립된 중간값**이 존재한다(예: 몸통
        //   가로 단면 `28,28,28,0,0,0,0,7,28`의 그 `7`). 2배 확대된 이미지라면 모든 값이 최소 2x2
        //   블록으로 나타나야 하므로 불가능하다. 카메라 픽셀도 (3024x1964) = 패널 네이티브와 동일하고,
        //   `system_profiler`도 "3024 x 1964 Retina"(스케일드 모드 아님 = 리샘플 없음)로 확인된다.
        //   -> 캐릭터는 이미 물리 픽셀 1:1로 그려지고 있다. 여기서 더 얻을 해상도가 없다.
        //
        // [닫힘] 가설 B — "투명 창에서 MSAA 알파가 프리멀티플라이드로 합성되지 않아 계단이 살아난다"
        //   반증 1(이론): 캐릭터 선 색은 순수 검정 (0,0,0)이다. 프리멀티플라이드 알파와 스트레이트
        //     알파는 RGB가 0일 때 **수학적으로 동일**하다(0 x a = 0). 즉 검은 졸라맨에서는 이 가설이
        //     애초에 성립할 수 없다.
        //   반증 2(실측): 가장자리 픽셀 279개를 재 보니 전부 `배경 x (1 - 커버리지)`를 정확히 만족했다
        //     (배경 28 -> 커버리지 0.25/0.50/0.75에서 각각 21/14/7). 합성 산술에 손실이 전혀 없다.
        //   (참고: 이 가설이 낳는 진짜 아티팩트였던 "밝은 회색 프린지"는 이미 이전 라운드에
        //    MacOverlayStateEnforcer.ApplyTransparentSafeCameraBackground()가 카메라 배경 RGB를
        //    검정으로 낮춰 해결해 두었다. 그 방어책을 되돌리지 마라.)
        //
        // [닫힘] 가설 C — "MSAA가 설정만 있고 실제로는 안 걸린다"
        //   반증: 걸려 있다. 위 ConfigureAntiAliasing() 문서의 2x/4x/8x 대조 실험 참고.
        //   다만 **상한이 4x**라는 새 사실을 얻었다(8x는 Metal이 조용히 4로 낮춘다).
        //
        // [원인 확정 + 개선 여지 없음] 가설 D — "MSAA 가장자리 단계가 4개뿐이고 획이 얇다"
        //   이것이 남은 유일한 원인이다. 실측 획 두께는 2.65~5.16 물리픽셀
        //   (눈동자 점 2.65 / 머리 링 3.87 / 팔 4.30 / 몸통 4.73 / 다리 5.16, characterScale=0.75).
        //   그런데 **여기서 더 개선할 수 있는 폭이 사실상 없다**:
        //     · 4단계 양자화가 만드는 오차는 가장자리 픽셀 1개당 최대 12.5%, 평균 4.2%(수치 시뮬레이션).
        //     · 그래서 셰이더로 거리 기반 알파 페더(= 256단계 연속 AA)를 넣어 이론적 최적을 만들어 봐도
        //       실제 획 두께에서 렌더 결과가 육안으로 구분되지 않는다(5배 확대 나란히 비교로 확인).
        //   -> LineRenderer용 커스텀 AA 셰이더는 **채택하지 않았다**. 얻는 것이 12.5%짜리 한 픽셀인데,
        //      대가로 (a) 페더 폭만큼 지오메트리를 넓혀야 하고, (b) 이 머티리얼을 캐릭터 LineRenderer에서
        //      빌려 쓰는 시각 레이어 10종(StressGauge/Graffiti/WindowTheft/... )이 전부 그만큼 얇아진다.
        //
        // 결론(정직한 한계): 렌더 파이프라인은 이 하드웨어의 물리적 상한에 이미 도달해 있다. 캐릭터가
        // 화면에서 차지하는 픽셀 자체가 141px 높이 / 2.65~5.16px 획이라 "더 선명하게"는 만들 수 없고,
        // 체감을 바꾸려면 **캐릭터를 키우거나(StickConfig.characterScale) 획을 굵히는** 수밖에 없다 —
        // 둘 다 사용자가 직접 "너무 크다/너무 굵다"고 되돌린 적이 있는 값이므로 임의로 바꾸지 마라.
    }
}
