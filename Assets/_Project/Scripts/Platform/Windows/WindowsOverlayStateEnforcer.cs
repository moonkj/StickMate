#if UNITY_STANDALONE_WIN
using System;
using UnityEngine;
using Kirurobo;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// UniWindowController의 "창 부착(Attach) 타이밍" 문제를 Windows에서 해결하는 런타임 전용 보조
    /// 컴포넌트(윈도우 지원 라운드, 2026-08-30). macOS의 Platform/MacOS/MacOverlayStateEnforcer.cs와
    /// 같은 역할을 하는 형제 파일이다.
    ///
    /// ============================================================================
    /// 왜 필요한가 — 플랫폼과 무관한 라이브러리 자체의 순서 문제
    /// ============================================================================
    /// UniWindowController는 자기 창을 Awake()가 아니라 첫 Update()에서 붙잡는다
    /// (UpdateTargetWindow() -> UniWinCore.AttachMyWindow()). 그런데 우리 배선 지점인
    /// StickmanAgent.Start()는 그보다 먼저 실행되므로, 그 시점의 설정 중 항상위/클릭관통은
    /// `IsTopmost => IsActive &amp;&amp; _isTopmost` 같은 되읽기 규칙 때문에 조용히 false로 돌아간다.
    /// 이 되읽기 규칙은 UniWinCore.cs의 플랫폼 공통 코드라 Windows에서도 정확히 동일하다 —
    /// macOS에서 실측으로 확인된 사고가 Windows에서만 안 일어날 이유가 없다.
    ///
    /// ============================================================================
    /// 왜 MacOverlayStateEnforcer를 그대로 쓰지 않고 형제 파일을 두는가 (중복이 아니다)
    /// ============================================================================
    /// 그 클래스의 700줄 중 대부분은 **macOS 창 기하 고유의 보정**이다:
    ///   · GetMonitorRect()가 macOS에서만 visibleFrame(메뉴바 33pt + Dock 75pt를 뺀 작업영역)을
    ///     돌려주기 때문에 화면 전체 높이를 Cocoa/Quartz 두 좌표계의 항등식으로 역산하는 로직
    ///   · isFreePositioningEnabled(= macOS가 창을 visibleFrame 안으로 밀어 넣는 제약을 푸는 플래그)
    ///   · Retina 포인트/픽셀 배율 보정, Dock 발판 리포트, 히트테스트 프로브(macOS 실측 진단 도구)
    /// Windows에는 메뉴바도 Dock도 없고 GetMonitorRectangle이 처음부터 모니터 전체 사각형을 준다.
    /// 그 700줄을 공용화하려면 macOS 전용 보정을 전부 조건 분기로 갈라야 하는데, 그것은 **이미 실측으로
    /// 튜닝이 끝난 macOS 경로에 회귀 위험을 주입하는 대가로** Windows에서 절반이 죽은 코드를 얻는
    /// 거래다. 진짜 공유 대상(= 오버레이 솔루션 자체)은 UniWindowController 패키지이고, 이 파일은 그
    /// 패키지를 부르는 20줄짜리 얇은 껍데기다 — 중복 구현은 여기서 발생하지 않는다.
    ///
    /// 생성 주체는 Win32WindowService.CreateOverlayWindow()이며, 그 서비스 자체가 실제 Standalone
    /// Windows Player에서만 인스턴스화되므로(StickmanAgent.CreatePlatformService()의
    /// `UNITY_STANDALONE_WIN &amp;&amp; !UNITY_EDITOR` 분기) 에디터/헤드리스에는 존재하지 않는다.
    /// 씬 에셋에도 저장되지 않는다(런타임 new GameObject).
    /// </summary>
    internal sealed class WindowsOverlayStateEnforcer : MonoBehaviour
    {
        private const string HostObjectName = "StickMate_WindowsOverlayStateEnforcer";

        /// <summary>부착 확인 후 목표 상태를 재적용할 최대 횟수. 무한 반복은 하지 않는다 —
        /// 사용자가 창을 직접 조작했을 때 우리가 계속 되돌리는 것이 더 나쁘다.
        ///
        /// <para>★ 2026-09-02 — 값이 <b>플랫폼 중립</b> <see cref="OverlayStateReapplyPolicy"/>로
        /// 옮겨졌다. 이 파일은 <c>#if UNITY_STANDALONE_WIN</c> 안이라 이 머신의 EditMode 테스트가
        /// 상수를 참조할 방법이 없었고, 그래서 회귀 테스트가 숫자 5를 베낄 수밖에 없었다
        /// (CLAUDE.md 금지 사항). macOS판 Enforcer도 같은 상수를 참조한다.</para></summary>
        private const int ReapplyAttempts = OverlayStateReapplyPolicy.ReapplyAttempts;
        private const float ReapplyIntervalSeconds = OverlayStateReapplyPolicy.ReapplyIntervalSeconds;
        private const float AttachTimeoutSeconds = 15f;

        /// <summary>전체화면 확장 재시도 상한 — 해상도 변경이 프레임 끝에 반영되고 창 스타일 확정에도
        /// 한두 프레임 걸려서 한 번에 성공하지 않을 수 있다.</summary>
        private const int MaxFullScreenApplyAttempts = 6;

        /// <summary>
        /// 창 기하 판정 불감대(픽셀). <b>"목표와 정확히 같은가"를 묻지 않는다.</b>
        ///
        /// <para>근거(2026-09-01 실기): 대입값 3840에 대해 되읽기가 3839로 돌아온다. 그 1px을 불일치로
        /// 보면 <c>Screen.SetResolution</c>과 창 리사이즈가 에피소드마다 다시 실행되고, 둘 다
        /// <b>클라이언트 영역 변경 = 스왑체인/리디렉션 표면 재생성</b>이라 수백 ms 정지를 만든다
        /// (실기 최대 프레임 407ms). 재적용이 반복될수록 창이 1px씩 더 줄어드는 래칫까지 겹쳤다.</para>
        ///
        /// <para>★ <b>2026-09-02 정정</b> — 위 문단이 지목한 "1px 래칫"의 원인은 <b>이 불감대가 막는
        /// 경로가 아니었다</b>. 2차 신고 실기 로그에서 <c>ApplyFullScreenBounds</c>는 세션당 <b>한 번만</b>
        /// 실행됐고 그때조차 크기를 건드리지 않았는데(<c>리사이즈=False</c>, 결과 2560 유지) 창은 계속
        /// 줄었다. 진짜 래칫은 <b>재적용 루프의 <c>isTransparent</c> 대입</b>이 부르는 네이티브
        /// <c>SetBorderless</c>의 폭 흔들기였다(<see cref="Update"/> 안의 해당 블록 주석과
        /// <see cref="OverlayStateReapplyPolicy"/> 참고). 불감대는 그대로 유효하지만 — 이쪽 경로에는
        /// 원래 자기 몫의 방어가 필요했다 — <b>이 신고의 원인은 아니었다</b>.</para>
        ///
        /// <para>값과 근거는 플랫폼 중립 순수 규칙
        /// <see cref="StickMate.Platform.OverlayBoundsFitPolicy.DefaultEpsilonPixels"/> 한 곳에 있다 —
        /// macOS판 Enforcer가 같은 결함을 갖고 있으므로 값이 두 벌로 갈라지면 안 된다.</para>
        /// </summary>
        private const float BoundsEpsilonPixels = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

        /// <summary>
        /// 프로세스 수명 전체에서 <c>Screen.SetResolution</c>을 부를 수 있는 최대 횟수.
        ///
        /// <para>24시간 상주 앱에서 이 호출은 <b>절대 무제한이면 안 된다</b>. 한 번이 곧 백버퍼 재할당
        /// 한 번이고, 어떤 이유로든 판정이 진동하면 사용자는 몇 초마다 수백 ms씩 얼어붙는 앱을 보게
        /// 된다 — 그것이 이번 신고("계속 실행해 놓을수록 렉이 심해지는거 같음")의 모양 그대로다.
        /// 상한에 닿으면 조용히 죽지 않고 <b>로그에 상한 도달을 명시</b>한다(정직한 실패 보고).</para>
        ///
        /// <para>4인 이유: 정상 경로는 기동 시 1회다. 디스플레이 구성 변경(모니터 착탈/해상도 변경)이
        /// 세션당 몇 번 일어나도 감당하면서, 진동 루프는 즉시 멈춘다.</para>
        /// </summary>
        private const int MaxSetResolutionCalls = 4;

        private UniWindowController _controller;
        private Core.StickmanAgent _agent;

        private int _appliedCount;
        private float _timer;
        private float _elapsed;
        private bool _attachDetected;
        private bool _gaveUpLogged;
        private bool _cameraBackgroundPremultiplyFixed;

        private bool _fullScreenBoundsApplied;
        private int _fullScreenApplyAttempts;
        private float _fullScreenTimer;

        /// <summary>스왑체인 재생성을 유발하는 두 호출의 <b>프로세스 누적</b> 횟수. 로그에 항상 함께
        /// 찍어 [프레임스파이크]의 "백버퍼가 바뀌었다" 줄과 시각 대조가 가능하게 한다.</summary>
        private int _setResolutionCalls;
        private int _windowResizeCalls;

        /// <summary>라이브러리 <c>isTransparent</c> 대입(= 네이티브 <c>SetBorderless</c>)이 실제로
        /// 실행된 <b>프로세스 누적</b> 횟수. 1회당 <c>SetWindowPos</c> 4회(클라이언트 영역 변경 4회)다.
        /// 정상 동작이면 이 값은 <b>0에서 멈춰 있어야 한다</b> — 세션 내내 늘고 있으면 OS 실측이
        /// "보더리스 아님"을 계속 돌려주고 있다는 뜻이고, 다음 라운드가 볼 곳은 그 스타일 값이다.</summary>
        private int _borderlessResizeEpisodes;

        /// <summary>
        /// 창 기하 A↔B 진동 가드. 판정은 플랫폼 중립 한 곳
        /// (<see cref="StickMate.Platform.OverlayGeometryOscillationGuard"/>)에 있고 여기서는 관측만 한다 —
        /// macOS판 Enforcer도 같은 클래스를 같은 방식으로 쓴다(2026-09-01 맥 실기에서 오버레이 창
        /// 사각형이 두 값 사이를 교대하는 것이 관측됐고, 불감대는 그 부류를 원리적으로 막지 못한다).
        /// </summary>
        private readonly OverlayGeometryOscillationGuard _boundsOscillation =
            new OverlayGeometryOscillationGuard();

        // 실행 중 디스플레이 구성 변경 추적(2026-08-31). 판단 로직은 플랫폼 공용
        // Platform/DisplayTopologyWatcher.cs 한 곳에 있고 여기서는 관측만 한다 — macOS판도 같은 클래스를
        // 같은 방식으로 쓴다(오늘 VisibleTopEdgeSolver에서 한쪽만 고쳐 재발한 사례의 재발 방지).
        private readonly DisplayTopologyWatcher _topologyWatcher = new DisplayTopologyWatcher();
        /// <summary>전체화면 적합 에피소드가 끝난 뒤 기준값을 다시 잡았는가. 재무장할 때 false로 돌린다.</summary>
        private bool _topologyBaselineSynced;
        /// <summary>토폴로지 관측 주기(초). 디바운스 창(0.75초)보다 충분히 짧아 판정 해상도는 잃지 않으면서
        /// OS 디스플레이 열거 호출을 줄인다.
        ///
        /// <para>★ 2026-09-01 — 0.1초에서 0.25초로 늘렸다. 한 번의 관측은
        /// <c>GetMonitorCount()</c> + 모니터 수만큼의 <c>GetMonitorRect()</c> P/Invoke이고,
        /// 이 앱은 24시간 상주다. 디바운스가 0.75초이므로 0.25초면 안정 판정에 여전히 3표본이 들어가
        /// <b>판정 품질은 그대로</b>이면서 상시 네이티브 호출이 60%↓ 한다. 이보다 늘리면 표본이
        /// 2개 이하로 떨어져 디바운스가 사실상 무력해지므로 늘리지 말 것.</para></summary>
        private const float TopologySampleIntervalSeconds = 0.25f;
        private float _topologySampleTimer;

        /// <summary>플랫폼 계층이 배선하는 "지금 즉시 오버레이 창 OS 사각형을 보고하라" 훅
        /// (Win32WindowService.CaptureOverlayOrigin). 재적합 직후 같은 프레임에 좌표계를 갱신해
        /// 0.5초 폴링을 기다리는 동안 캐릭터가 옛 좌표계로 튀는 구간을 없앤다.</summary>
        internal System.Action OverlayRectReporter;

        /// <summary>우리 오버레이 창의 HWND(Win32WindowService.CreateOverlayWindow가 확보해 넣어준다).
        /// 항상위 감시가 <b>OS 실측</b>을 하려면 반드시 필요하다 — 라이브러리는 자기 캐시만 돌려준다.</summary>
        internal System.IntPtr OverlayHandle;

        /// <summary>항상위 강등 감시 + 진단 로그. 아래 <see cref="TickTopmostWatchdog"/> 참고.</summary>
        private readonly WindowsTopmostWatchdog _topmostWatchdog = new WindowsTopmostWatchdog();

        /// <summary>레이어드/DWM 하이브리드 해소기(2026-09-01). 자기 HWND를 스스로 해석하므로
        /// <see cref="OverlayHandle"/>에 의존하지 않는다 — 그 핸들이 <b>라이브러리가 붙잡은 창과
        /// 같다는 보장이 없다</b>는 것이 이 라운드의 발견 중 하나다(UniWinCNativeHandle 문서 참고).</summary>
        private readonly WindowsLayeredHybridResolver _layeredHybridResolver = new WindowsLayeredHybridResolver();

        // 목표 상태 — Win32WindowService가 자기 API 호출 때마다 갱신한다.
        internal bool DesiredTransparent = true;
        internal bool DesiredTopmost;
        internal bool DesiredClickThrough;
        internal bool DesiredHitTest;

        internal static WindowsOverlayStateEnforcer EnsureExists(UniWindowController controller)
        {
            // ★ 2026-09-01 — 알파/합성 진단 프로브를 <b>여기서</b> 세운다(부착 성공을 기다리지 않는다).
            //   부착이 끝내 실패하는 경우가 이 진단이 가장 필요한 순간인데, 부착 이후 경로에만 걸어 두면
            //   정확히 그때 아무 관측도 남지 않는다. 프로브는 멱등이고 2초에 한 번, 지문이 바뀔 때만
            //   찍는다(WindowsCompositionProbe 문서 참고).
            WindowsCompositionProbe.EnsureExists(controller, null);

            var existing = UnityEngine.Object.FindAnyObjectByType<WindowsOverlayStateEnforcer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing._controller = controller;
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var enforcer = go.AddComponent<WindowsOverlayStateEnforcer>();
            enforcer._controller = controller;
            return enforcer;
        }

        /// <summary>목표 상태가 바뀔 때마다 호출 — 재적용 카운터를 리셋해 새 목표가 확실히 반영되게 한다.</summary>
        internal void MarkDirty()
        {
            _appliedCount = 0;
            _timer = ReapplyIntervalSeconds; // 다음 Update에서 곧바로 한 번 적용.
        }

        private Core.StickConfig ResolveConfig()
        {
            EnsureAgentResolved();
            var blackboard = _agent != null ? _agent.Blackboard : null;
            return blackboard != null ? blackboard.Config : null;
        }

        /// <summary>
        /// 에이전트 참조 확보 — <b>실패해도 매 프레임 다시 찾지 않는다</b>(2026-09-01).
        ///
        /// <para><c>FindAnyObjectByType&lt;T&gt;()</c>는 씬 전체를 훑는 호출이다. 예전에는 이 줄이
        /// <c>Update()</c>에서 조건 없이 돌았고, 에이전트가 아직/영영 없는 상황(기동 구간, 씬 전환 중,
        /// 에이전트가 파괴된 뒤)에서는 <b>60fps × 24시간 = 500만 회</b>의 씬 스캔이 된다.
        /// 찾으면 캐시되므로 정상 경로의 비용은 그대로 0이고, 못 찾는 경로만 초당 1회로 눌린다.</para>
        /// </summary>
        private void EnsureAgentResolved()
        {
            if (_agent != null) return;
            if (Time.unscaledTime < _nextAgentLookupTime) return;
            _nextAgentLookupTime = Time.unscaledTime + AgentLookupRetrySeconds;
            _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
        }

        private const float AgentLookupRetrySeconds = 1f;
        private float _nextAgentLookupTime;

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.PlatformEnforcer);   // [스톨구간] 계측
            // ★ 2026-09-01 패리티 감사 — 순서를 macOS판(MacOverlayStateEnforcer.Update)과 맞췄다.
            //   전에는 `if (_controller == null) return;`이 이 블록보다 **위에** 있었다. 그러면
            //   컨트롤러를 아직/더는 잡지 못한 프레임에서 FramePacing이 통째로 멈춘다 —
            //   적응형 등급이 마지막 값에 얼어붙어 24시간 상주 절감이 그 플랫폼에서만 꺼진다.
            //   창 부착 여부와 무관하게 가장 먼저 거는 것이 원래 의도다(시작 구간의 부착 대기 몇 초가
            //   오히려 프레임을 가장 헛되이 태우는 구간이다).
            //   설정이 아직 없으면 내부적으로 다음 프레임에 다시 시도한다.
            //   IsApplied를 먼저 본다 — 적용이 끝난 뒤에는 ResolveConfig() 호출조차 하지 않는다.
            if (!FramePacing.IsApplied) FramePacing.ApplyOnce(ResolveConfig());
            // 캐릭터가 제자리에 서 있는지를 넘긴다 — 적응형 프레임 등급의 입력이다(판정 자체는
            // 양 플랫폼 공용 FramePacing.ResolveCharacterIdle 한 곳에만 있다).
            EnsureAgentResolved();
            FramePacing.Tick(FramePacing.ResolveCharacterIdle(_agent));

            if (_controller == null) return;

            _elapsed += Time.unscaledDeltaTime;

            // 부착 판정: 부착 전에는 네이티브가 크기를 (0,0)으로 보고한다(macOS와 동일한 계약).
            Vector2 windowSize = _controller.windowSize;
            bool attached = windowSize.x > 0f && windowSize.y > 0f;

            if (!attached)
            {
                if (!_gaveUpLogged && _elapsed > AttachTimeoutSeconds)
                {
                    _gaveUpLogged = true;
                    Debug.LogWarning($"[WindowsOverlayStateEnforcer] {AttachTimeoutSeconds}초가 지나도 " +
                        "UniWindowController가 자기 HWND를 붙잡지 못했습니다(windowSize=(0,0)). " +
                        "투명/항상위/클릭관통이 전부 적용되지 않은 상태입니다 — 정직한 실패 보고용 로그.");
                }
                return;
            }

            if (!_attachDetected)
            {
                _attachDetected = true;
                ApplyTransparentSafeCameraBackground();
                Debug.Log($"[WindowsOverlayStateEnforcer] 창 부착 감지 — windowSize={windowSize}, " +
                    $"clientSize={_controller.clientSize}, windowPosition={_controller.windowPosition}, " +
                    $"경과 {_elapsed:F2}초. 이제 목표 상태를 재적용합니다.");
                _timer = ReapplyIntervalSeconds;
            }

            // 순서 중요: 재무장을 먼저 판정해야 같은 프레임의 TickFullScreenBounds()가 곧바로 다시 돈다.
            TickDisplayTopology();
            TickFullScreenBounds();
            TickTopmostWatchdog();
            // ★ 2026-09-01 (debugger) — "레이어드 + DWM 확장 프레임" 하이브리드 해소.
            //   네이티브 SetClickThrough(TRUE)가 WS_EX_TRANSPARENT와 함께 켜고 <다시는 끄지 않는>
            //   WS_EX_LAYERED를, 클릭 관통이 유지되는지 OS에게 직접 확인한 뒤에만 떼어낸다.
            //   판정 규칙/근거 전문은 Platform/LayeredHybridPolicy.cs, 실행은 WindowsLayeredHybridResolver.
            //   TickTopmostWatchdog와 마찬가지로 재적용 상한과 무관하게 앱 수명 내내 돈다 —
            //   라이브러리가 커서 이동마다 레이어드를 다시 켜기 때문이다.
            _layeredHybridResolver.Tick(Time.unscaledDeltaTime, (int)_controller.transparentType);

            // ★ 위 TickTopmostWatchdog()이 이 return **위에** 있는 것이 핵심이다(2026-09-01).
            //   아래 재적용 루프는 ReapplyAttempts(5) x 0.5초 = 2.5초로 상한이 걸려 있어, 기동 몇 초 뒤엔
            //   영원히 돌지 않는다. 그래서 그 뒤에 OS가 우리 창을 z-order에서 강등시키면 되돌릴 주체가
            //   아무도 없었다 — 사용자가 3번 신고한 "엑셀 클릭하면 캐릭터가 창 뒤로 넘어감"의 직접 원인.
            //   macOS판은 같은 자리에 TickAllSpacesBehavior()라는 상시 감시가 이미 있었고
            //   (MacOverlayStateEnforcer), Windows에만 대응물이 없었다.
            if (_appliedCount >= ReapplyAttempts) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < ReapplyIntervalSeconds) return;
            _timer = 0f;
            _appliedCount++;

            // 순서 주의(macOS와 동일): 히트테스트 자동 제어를 먼저 목표값으로 맞춘 뒤 나머지를 적용한다.
            // 반대로 하면 라이브러리의 매 프레임 자동 제어(UpdateClickThrough)가 우리 값을 덮어쓴다.
            //
            // ★ 2026-08-31 — isTopmost만 "이미 목표값이면 대입하지 않는다"(시작 시 깜박임 대응).
            //
            // UniWindowController의 세터에는 동등성 가드가 없다. isTopmost 대입 한 번마다 네이티브가
            // 자기 창의 Z-order를 HWND_TOPMOST로 다시 지정하고, 레이어드 창에서는 그때마다 DWM 합성이 한 번
            // 무효화되어 화면이 순간 비칠 수 있다. 지금까지 이 루프는 값이 이미 맞아도 0.5초 간격으로
            // 5번을 무조건 다시 걸었고, 그것이 사용자가 신고한 "처음 실행시 캐릭터와 나사 버튼이
            // 깜박깜박"의 후보 중 하나다(확정된 원인 아님 — Tasklist 참고).
            //
            // ★★ 2026-09-01 반증 — 위 가드의 근거가 **사실이 아니었다**(같은 버그 3번째 신고에서 발각).
            //
            // 원래 여기에는 이렇게 적혀 있었다: "isTopmost 게터는 `_isTopmost = _uniWinCore.IsTopmost`로
            // 네이티브 진실을 되읽는다". 패키지 소스를 실제로 열어 보니 그 끝은 네이티브가 아니다:
            //     UniWinCore.cs:256  public bool IsTopmost { get { return (IsActive && _isTopmost); } }
            // 즉 <b>순수 C# 캐시 필드</b>이고, 네이티브 되읽기용 extern
            //     UniWinCore.cs:78   public static extern bool IsTopmost();
            // 은 <b>선언만 되어 있고 패키지 전체에서 한 번도 호출되지 않는다</b>(전수 검색으로 확인).
            // 그래서 OS가 우리 창의 WS_EX_TOPMOST를 떼어내도 이 게터는 계속 true를 돌려주고,
            // 가드는 "이미 목표값이니 생략"을 영원히 반복한다 — 바로 아래 문단이 isTransparent에 대해
            // 경고하는 "캐시 때문에 재적용을 건너뛰는 최악의 경우"가 isTopmost에서 실제로 일어났다.
            //
            // 그래서 판정 근거를 라이브러리 캐시에서 <b>OS 실측</b>(GetWindowLong(GWL_EXSTYLE) &
            // WS_EX_TOPMOST)으로 바꾼다. 실측을 못 읽는 상황(핸들 미확보 등)에서는 가드를 걸지 않고
            // 무조건 재적용한다 — 모를 때는 거는 쪽이 안전하다.
            //
            //   · isClickThrough 게터는 <b>캐시된 C# 필드</b>를 그대로 돌려준다
            //     (UniWindowController.cs:126-131). 네이티브가 값을 조용히 버려도 캐시는 목표값
            //     그대로이므로 여기에 캐시 기반 가드를 걸면 안 된다. 그리고 그 세터의 네이티브 끝은
            //     SetWindowLong(GWL_EXSTYLE) 뿐이라(libuniwinc.cpp:954) <b>창 사각형을 건드리지
            //     않는다</b> — 무조건 재대입해도 스왑체인 재생성이 없다. 원칙 2(클릭 관통)를 지키기
            //     위해 앞으로도 가드하지 않는다.
            //   · isHitTestEnabled는 네이티브 부작용이 없는 평범한 public 필드라 대입 비용이 0이다.
            _controller.isHitTestEnabled = DesiredHitTest;

            // ★★★ 2026-09-02 — 여기가 2차 신고("윈도우 버전인데 여전히 사용할수록 렉생김")의 진원지다.
            //
            // 이 루프는 크기를 한 줄도 대입하지 않는데도 실기에서 창 폭이 재적용 1회당 정확히 1px씩
            // 줄었다(높이 1600은 불변). 범인은 바로 아래 한 줄이었다:
            //
            //     _controller.isTransparent = DesiredTransparent;
            //       -> UniWinCore.EnableTransparent(true)              (UniWinCore.cs:535)
            //          -> LibUniWinC.SetTransparent(true)   ... 유리(DWM). 창 사각형 안 건드림
            //          -> LibUniWinC.SetBorderless(true)    ... ★ SetWindowPos 4회, 폭을 ±1 흔든다
            //
            // SetBorderless에는 동등성 가드가 없어서, 이미 보더리스여도 매번 폭 흔들기를 다시 한다.
            // 흔들기가 폭에만 걸리고(newH 고정) 보더리스일 때 offset이 -1이라 중간 상태가 항상 더
            // 좁은 쪽인 것까지 실기 로그와 정확히 일치한다. 그리고 다음 호출의 기준값을
            // GetWindowRect/GetClientRect로 다시 읽으므로 한 번 잃은 1px이 새 기준이 된다(래칫).
            // 더 큰 피해는 폭 1px이 아니라 <b>클라이언트 영역 변경 4회 = 스왑체인 재생성 4회</b>이며,
            // MarkDirty()로 라운드가 재무장될 때마다(UI 표면 개폐 1회당) 최대 20회가 된다.
            // 근거 전문(패키지 C++ 원문 인용 포함)은 Platform/OverlayStateReapplyPolicy.cs.
            //
            // 처방: <b>무조건 재적용을 없애지 않는다. 반으로 쪼갠다.</b>
            //   · 유리 = 되읽을 API가 없고 비용도 없다        -> 매 회차 <b>무조건</b> 다시 건다.
            //   · 보더리스 = OS 실측 가능, 대신 비용이 크다   -> 실측이 이미 목표면 부르지 않는다.
            // 즉 캐시를 믿고 생략하는 것이 아니라 <b>OS에게 물어보고</b> 생략한다 — isTopmost에서
            // 이미 한 번 데였던 그 함정(캐시 게터를 진실로 착각)을 반복하지 않는 유일한 방법이다.
            // ★ 핸들 주의 — 이 실측만은 <b>OverlayHandle이 아니라 네이티브 핸들</b>을 먼저 쓴다.
            //   판정 대상은 "LibUniWinC가 실제로 SetBorderless를 건 창"이어야 하는데,
            //   OverlayHandle은 Win32WindowService가 .NET Process.MainWindowHandle로 잡은 값이고
            //   두 규칙이 같은 창을 고른다는 보장이 없다(UniWinCNativeHandle 클래스 문서).
            //   여기서 엉뚱한 창을 재면 "보더리스 아님"이 매 회차 참이 되어 <b>고치려는 래칫이 그대로
            //   되살아난다</b>. 네이티브를 못 얻을 때만 기존 핸들로 물러난다.
            IntPtr styleProbeHandle = UniWinCNativeHandle.TryGetNative();
            bool styleHandleIsNative = styleProbeHandle != IntPtr.Zero;
            if (!styleHandleIsNative) styleProbeHandle = OverlayHandle;
            bool styleReadOk = WindowsWindowStyleProbe.TryReadStyle(styleProbeHandle, out long osStyle);
            bool osBorderless = styleReadOk && OverlayStateReapplyPolicy.IsBorderless(osStyle);
            TransparencyReapply transparency = OverlayStateReapplyPolicy.DecideTransparencyReapply(
                DesiredTransparent, styleReadOk, osBorderless,
                !UniWinCNativeHandle.GlassOnlyPathKnownUnavailable);

            // 유리 전용 경로가 이번 호출에서 실패하면 <b>같은 틱에</b> 전체 경로로 물러난다 —
            // 투명화를 못 거는 것(회색 불투명 전체화면 창)이 1px 래칫보다 훨씬 나쁘다.
            if (transparency == TransparencyReapply.GlassOnly
                && !UniWinCNativeHandle.TrySetTransparent(DesiredTransparent))
            {
                transparency = TransparencyReapply.ReassignGlassPathUnavailable;
            }

            if (OverlayStateReapplyPolicy.CausesWindowResize(transparency))
            {
                _controller.isTransparent = DesiredTransparent;
                _borderlessResizeEpisodes++;
            }

            bool topmostSkipped = _topmostWatchdog.TryReadOsTopmost(OverlayHandle, out bool osTopmost)
                && osTopmost == DesiredTopmost;
            if (!topmostSkipped) _controller.isTopmost = DesiredTopmost;
            _controller.isClickThrough = DesiredClickThrough;

            Debug.Log($"[WindowsOverlayStateEnforcer] 재적용 {_appliedCount}/{ReapplyAttempts} " +
                $"(isTopmost 재적용={(topmostSkipped ? "생략(이미 목표값)" : "실행")}) — " +
                $"투명 재적용: {OverlayStateReapplyPolicy.Describe(transparency)} " +
                $"[OS 실측 GWL_STYLE=0x{osStyle:X}(읽기={(styleReadOk ? "성공" : "실패")}, " +
                $"보더리스={osBorderless}, 핸들={(styleHandleIsNative ? "네이티브" : ".NET폴백")}), " +
                $"SetBorderless 실행 누적 {_borderlessResizeEpisodes}회] / " +
                $"목표(transparent={DesiredTransparent}, topmost={DesiredTopmost}, " +
                $"clickThrough={DesiredClickThrough}, hitTest={DesiredHitTest}) / " +
                $"되읽음(isTransparent={_controller.isTransparent}, isTopmost={_controller.isTopmost}, " +
                $"isClickThrough={_controller.isClickThrough}, isHitTestEnabled={_controller.isHitTestEnabled}) / " +
                $"windowSize={_controller.windowSize}, windowPosition={_controller.windowPosition}, " +
                $"transparentType={_controller.transparentType}.");
        }

        /// <summary>
        /// 항상위(topmost) 상시 감시 — 2026-09-01 신설. <b>재적용 루프 상한과 무관하게 앱이 살아 있는
        /// 내내 돈다</b>(macOS의 TickAllSpacesBehavior와 같은 계약).
        ///
        /// 하는 일은 세 가지뿐이고 전부 <c>WindowsTopmostWatchdog</c> 안에 있다:
        ///   (1) <c>GetWindowLong(GWL_EXSTYLE) &amp; WS_EX_TOPMOST</c>로 <b>OS의 진실</b>을 읽는다.
        ///   (2) 풀렸으면 <c>isTopmost</c> 대입으로 다시 건다(우리 창에만 작용 — 원칙 3 준수).
        ///   (3) <b>전이 순간에만</b> [Z-ORDER] 한 줄을 남긴다.
        ///
        /// <para>숨김 중(전체화면 게임 감지)에는 재적용을 보류한다 — 게임 위로 기어 올라가는 것은
        /// 원칙 2 위반이고, 독점 전체화면 앱과 z-order를 다투면 그쪽만 깜빡인다. 다만 <b>로그는 남긴다</b>:
        /// "숨김 때문인가 z-order 때문인가"를 다음 신고에서 가르는 것이 이 라운드의 목적이다.</para>
        /// </summary>
        private void TickTopmostWatchdog()
        {
            // ★ 델리게이트를 필드에 캐시해 넘긴다(인라인 람다 금지). `this`를 캡처하는 람다는 Roslyn이
            //   캐시하지 않으므로, 호출부에 그냥 쓰면 **매 프레임 델리게이트 2개**가 새로 할당된다.
            //   Tick()은 내부 주기 가드보다 앞에서 인자를 평가하므로 조기 반환으로도 못 피한다.
            //   24시간 상주 앱에서 초당 120개의 쓰레기는 그냥 결함이다.
            _reassertTopmost ??= ReassertTopmost;
            _describeOverlay ??= DescribeOverlay;

            bool suspended = _agent != null && _agent.IsSuspended;
            _topmostWatchdog.Tick(
                Time.unscaledDeltaTime, OverlayHandle, DesiredTopmost, suspended,
                _reassertTopmost, _describeOverlay);
        }

        private System.Action _reassertTopmost;
        private System.Func<string> _describeOverlay;

        /// <summary>topmost 재적용. 라이브러리 세터에는 동등성 가드가 없으므로
        /// (UniWindowController.SetTopmost의 `//if (_isTopmost == topmost) return;`가 주석 처리되어 있다)
        /// 캐시값이 목표와 같아도 네이티브 SetWindowPos까지 확실히 내려간다.</summary>
        private void ReassertTopmost()
        {
            if (_controller != null) _controller.isTopmost = DesiredTopmost;
        }

        /// <summary>진단 로그에 붙일 "라이브러리가 주장하는 상태". OS 실측값과 <b>나란히</b> 찍히므로
        /// 둘이 어긋나는 순간(= 캐시가 거짓말하는 순간)이 로그에 그대로 드러난다.</summary>
        private string DescribeOverlay()
        {
            if (_controller == null) return "컨트롤러 없음";
            return $"isTopmost(캐시)={_controller.isTopmost}, " +
                $"windowPosition={_controller.windowPosition}, windowSize={_controller.windowSize}";
        }

        /// <summary>
        /// 오버레이 창을 현재 모니터 전체로 확장한다. macOS판과 달리 메뉴바/Dock 역산이 없다 —
        /// Windows의 GetMonitorRectangle은 처음부터 작업영역이 아니라 **모니터 전체 사각형**을 준다
        /// (작업표시줄 띠까지 포함). 그래서 라이브러리가 준 값을 그대로 목표로 삼는다.
        ///
        /// Screen.SetResolution을 함께 호출하는 이유는 macOS와 완전히 동일하다: 이걸 빼면 OS 창만
        /// 커지고 Screen.width/height는 옛 값이라 ScreenCoordinateConverter의 y 반전이 통째로 틀어진다.
        ///
        /// 성공하면 <see cref="_fullScreenBoundsApplied"/>가 서고 루프가 멈춘다. 그 플래그를 다시
        /// 내리는 <b>유일한</b> 경로가 <see cref="TickDisplayTopology"/>다(실행 중 해상도/모니터 변경).
        /// </summary>
        private void TickFullScreenBounds()
        {
            if (_fullScreenBoundsApplied || _fullScreenApplyAttempts >= MaxFullScreenApplyAttempts) return;
            if (_boundsOscillation.IsOscillating) return;   // 아래 진동 가드가 이미 멈춘 상태.

            _fullScreenTimer += Time.unscaledDeltaTime;
            if (_fullScreenTimer < ReapplyIntervalSeconds) return;
            _fullScreenTimer = 0f;
            _fullScreenApplyAttempts++;

            if (!TryGetTargetMonitorRect(out Rect monitor))
            {
                Debug.LogWarning("[WindowsOverlayStateEnforcer] 전체화면 확장 실패 — 모니터 사각형을 " +
                    $"조회하지 못했습니다(GetMonitorCount={UniWindowController.GetMonitorCount()}). 창 크기를 그대로 둡니다.");
                _fullScreenApplyAttempts = MaxFullScreenApplyAttempts;
                return;
            }

            Vector2 sizeBefore = _controller.windowSize;
            Vector2 posBefore = _controller.windowPosition;

            // ★★ 2026-09-01 — A↔B 진동 가드(플랫폼 중립 OverlayGeometryOscillationGuard).
            //
            // 위 불감대는 **1px 래칫**만 막는다. 창 기하가 두 값 사이를 오가면 두 값 모두 불감대 밖이라
            // "불일치" 판정이 매번 참이고, 재적용이 원리적으로 수렴하지 않는다. 재적용 한 번 =
            // 스왑체인/리디렉션 표면 재생성 한 번 = 수백 ms 정지이므로, **수렴 불가라는 사실 자체**를
            // 감지해 멈춘다. 정상 세션에서는 값이 정착하므로 이 가드는 아무 일도 하지 않는다
            // (= Windows 기존 동작 무변경). macOS판 Enforcer도 같은 클래스를 같은 자리에서 쓴다.
            if (_boundsOscillation.Observe(new Rect(posBefore, sizeBefore), BoundsEpsilonPixels))
            {
                _fullScreenApplyAttempts = MaxFullScreenApplyAttempts;   // 이번 에피소드 즉시 종료.
                Debug.LogWarning("[WindowsOverlayStateEnforcer] ★전체화면 재적합을 중단합니다 — " +
                    _boundsOscillation.Diagnosis +
                    " 이후 디스플레이 구성이 바뀌어도 이 프로세스에서는 재무장하지 않습니다" +
                    "(_setResolutionCalls 상한과 같은 이유 — 여기서 풀면 상한이 사실상 사라집니다).");
                return;
            }

            // 단위: Windows에서는 Unity Player가 per-monitor DPI aware라 Screen.width(Unity 픽셀)와
            // Win32/라이브러리의 좌표(물리 픽셀)가 같은 단위이므로 배율이 1.0으로 실측된다. 그래도
            // 값을 하드코딩하지 않고 macOS와 같은 단일 소스(ScreenCoordinateConverter)를 거친다 —
            // 배율이 1이 아닌 환경이 나오면 그쪽 한 곳만 고치면 되게 하기 위함이다.
            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(ResolveConfig()));
            int targetPixelW = Mathf.RoundToInt(monitor.width / dpi);
            int targetPixelH = Mathf.RoundToInt(monitor.height / dpi);

            // ★★ 2026-09-01 — 여기가 "엑셀 클릭하면 캐릭터가 창 뒤로 넘어간다"의 Windows 전용 원인이다.
            //
            // 이 호출의 세 번째 인자 FullScreenMode.Windowed가 말하듯, 오버레이는 **반드시 창 모드**여야
            // 한다(테두리는 라이브러리의 SetBorderless가 없앤다). 그런데 조건이 "해상도가 다를 때"뿐이라
            // 다음 두 사실이 겹치면 이 줄이 **한 번도 실행되지 않는다**:
            //   (1) ProjectSettings의 fullscreenMode가 1(FullScreenWindow)이다 — Unity 신규 프로젝트 기본값.
            //   (2) Windows에서는 dpi 배율이 1.0이라 targetPixel* == 모니터 해상도이고, 플레이어는
            //       이미 네이티브 해상도로 떠 있다. 즉 Screen.width/height가 목표와 **이미 같다**.
            // 결과: 플레이어가 FullScreenWindow 모드로 남고, Unity는 전체화면 계열 모드에서 포커스를
            // 잃으면 창을 뒤로 보낸다(다른 앱을 쓸 수 있게 하는 의도된 동작). 사용자가 본 "화면 뒤로
            // 넘어감"이 정확히 이것이다.
            //
            // 같은 코드가 macOS에서 멀쩡했던 이유도 여기서 갈린다: Retina 배율(dpi=2) 때문에
            // targetPixel*가 항상 Screen.width/height와 달라 조건이 늘 참이었고, 그래서 macOS는
            // 매번 Windowed로 내려갔다. **한쪽에서만 우연히 성립하던 전제**였던 셈이라, 조건에
            // fullScreenMode 자체를 명시적으로 넣어 우연에 기대지 않게 한다.
            // ★★★ 2026-09-01 2차 — 여기가 "407ms 멈춤 / 켜둘수록 렉이 심해짐"의 진원지다(래칫).
            //
            // 실기 로그 실측: `windowSize=(3840) -> (3839) -> (3838) -> ... -> (3831)`.
            // 되읽기가 대입값보다 1px 작게 돌아오는 것 자체는 **증상이 아니라 상수**다(원인은 아래
            // "1px의 정체" 참고). 진짜 결함은 그 1px이 **다음 판정의 입력이 되어** 아래 두 줄을 계속
            // 다시 실행시킨 것이다:
            //   · `Screen.width(3839) != targetPixelW(3840)` -> 매 에피소드 Screen.SetResolution 재호출
            //   · 창 크기 재대입 -> OS 창 리사이즈
            // 둘 다 **클라이언트 영역 변경 = D3D 스왑체인 + DWM 리디렉션 표면 재생성**이며, 수백 ms짜리
            // 정지다. `Platform/DisplayTopologyWatcher.cs` 클래스 문서가 바로 이 인과("중간 상태마다
            // SetResolution을 부르면 백버퍼 재할당이 연달아 일어나 멈춤이 오히려 길어진다")를 이미
            // 적어 두었는데, 여기 조건이 `!=` 완전일치라 그 경고를 우리 스스로 위반하고 있었다.
            //
            // ---- 1px의 정체(가설 2건, 실기 확인 항목) --------------------------------------------
            // (a) `Screen.SetResolution`은 **프레임 끝에 지연 적용**된다. 그래서 같은 틱에서 우리가
            //     `windowSize`로 세운 값을 프레임 끝의 Unity 리사이즈가 다시 덮어쓰고, 그쪽은 클라이언트
            //     사각형 기준이라 테두리/DWM 확장 프레임 계산에서 1px이 남을 수 있다.
            // (b) 라이브러리의 `SetSize`(SetWindowPos)와 `GetSize`(GetWindowRect)가 서로 다른 사각형을
            //     보는 경우(레이어드+DWM 확장 프레임).
            // 어느 쪽이든 **우리가 없앨 수 없는 상수 오차**다. 그러므로 옳은 처방은 "1px을 없애기"가
            // 아니라 **1px이 재적용을 유발하지 못하게 막는 것**이다 — 아래 불감대.
            //
            // ---- 왜 불감대가 증상을 덮는 것이 아닌가 ----------------------------------------------
            // 오버레이가 모니터보다 1px 좁아도 기능적 손실이 없다: 좌표 변환기는 "창 폭 == 모니터 폭"을
            // 가정하지 않고 **실측 창 사각형**에서 배율/원점을 유도한다(ScreenCoordinateConverter.
            // AutoDpiScale = 창 폭 / Screen.width). 반대로 스왑체인 재생성은 수백 ms 정지라 손실이
            // 압도적으로 크다. 그리고 불감대가 진짜 어긋남을 숨기지 않도록 (1) 불감대를 2px로 좁게 잡고
            // (2) 아래 로그가 실측 오차와 재생성 누적 횟수를 항상 함께 남긴다.
            // 판정 자체는 플랫폼 중립 순수 규칙 한 곳(OverlayBoundsFitPolicy)에 있다 — 그래야 Windows
            // 실기가 없는 이 개발 머신의 EditMode가 "래칫이 다시 생기지 않는다"를 실행으로 검증한다.
            bool resolutionMismatch = !OverlayBoundsFitPolicy.Within(
                Screen.width, Screen.height, targetPixelW, targetPixelH, BoundsEpsilonPixels);
            bool modeMismatch = Screen.fullScreenMode != FullScreenMode.Windowed;
            bool resolutionCapped = _setResolutionCalls >= MaxSetResolutionCalls;
            if (OverlayBoundsFitPolicy.ShouldSetResolution(Screen.width, Screen.height,
                    targetPixelW, targetPixelH, !modeMismatch, BoundsEpsilonPixels,
                    _setResolutionCalls, MaxSetResolutionCalls))
            {
                _setResolutionCalls++;
                Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed);
            }

            // 크기/위치도 같은 불감대를 쓴다. **이미 목표 안에 들어와 있으면 대입 자체를 하지 않는다** —
            // 대입 한 번이 곧 OS 리사이즈 한 번이고, 그것이 백버퍼 재할당 한 번이다.
            // 크기 -> 위치 순서(크기를 먼저 정해야 위치 대입이 최종 좌표가 된다).
            // ★ 2026-09-01 — 창 크기 재대입에도 **수명 상한**을 건다. Screen.SetResolution만 상한이
            //   있고 이쪽은 무제한이던 비대칭을 없앤다(둘 다 OS 표면 재생성 = 수백 ms 정지).
            //   지금 터지는 버그가 아니라, 불감대를 넘는 오차를 가진 환경에서 다시 열릴 문을 닫는
            //   하드닝이다 — 근거는 OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls 문서.
            bool resizeCapped = _windowResizeCalls >= OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls;
            bool needsResize = OverlayBoundsFitPolicy.ShouldResizeWithinBudget(sizeBefore.x, sizeBefore.y,
                monitor.width, monitor.height, BoundsEpsilonPixels,
                _windowResizeCalls, OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls);
            bool needsMove = OverlayBoundsFitPolicy.ShouldMove(posBefore.x, posBefore.y,
                monitor.x, monitor.y, BoundsEpsilonPixels);
            if (needsResize)
            {
                _windowResizeCalls++;
                _controller.windowSize = monitor.size;
            }
            if (needsMove) _controller.windowPosition = monitor.position;

            Vector2 sizeAfter = _controller.windowSize;
            Vector2 posAfter = _controller.windowPosition;
            bool ok = OverlayBoundsFitPolicy.Within(sizeAfter.x, sizeAfter.y,
                    monitor.width, monitor.height, BoundsEpsilonPixels)
                && OverlayBoundsFitPolicy.Within(posAfter.x, posAfter.y,
                    monitor.x, monitor.y, BoundsEpsilonPixels);
            if (ok)
            {
                _fullScreenBoundsApplied = true;

                // 같은 프레임에 좌표계를 갱신한다(폴링 대기 없음). 창이 방금 다른 크기/원점이 됐는데
                // ScreenCoordinateConverter가 최대 0.5초 동안 옛 원점/배율을 들고 있으면, 그 사이의
                // 커서<->월드 변환과 발판 판정이 통째로 어긋나 캐릭터가 화면 밖으로 튄다.
                OverlayRectReporter?.Invoke();
            }

            // clientSize를 함께 남긴다: Unity가 실제로 그리는 백버퍼 크기(= clientSize)와 Screen.width/height가
            // 어긋나면 표시 단계에서 전체 화면이 한 번 리샘플링되고, 그러면 <b>모든 표면</b>의 획이
            // 두 겹으로 번져 보인다(2026-08-31 신고와 같은 모양). 실기 로그 한 줄로 그 가설이 갈린다.
            Debug.Log($"[WindowsOverlayStateEnforcer] 전체화면 확장 시도 {_fullScreenApplyAttempts}/{MaxFullScreenApplyAttempts} — " +
                $"모니터={monitor}, 이전(size={sizeBefore}, pos={posBefore}) -> 이후(size={sizeAfter}, pos={posAfter}), " +
                // ★ 스왑체인 재생성 누적 — [프레임스파이크]의 "백버퍼가 바뀌었다" 줄과 짝을 이룬다.
                //   두 줄의 시각이 겹치면 그 스파이크의 범인이 이 파일임이 확정된다.
                $"재생성 누적(SetResolution {_setResolutionCalls}/{MaxSetResolutionCalls}회" +
                $"{(resolutionCapped ? " ★상한 도달 — 더는 부르지 않는다" : "")}, 창리사이즈 {_windowResizeCalls}/{OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls}회{(resizeCapped ? " ★상한 도달" : "")}), " +
                $"이번 틱 실행(SetResolution={(resolutionMismatch || modeMismatch) && !resolutionCapped}, " +
                $"리사이즈={needsResize}, 이동={needsMove}, 불감대={BoundsEpsilonPixels:F0}px), " +
                $"clientSize={_controller.clientSize}, " +
                $"Screen=({Screen.width}x{Screen.height}) [목표 {targetPixelW}x{targetPixelH} 픽셀, dpi배율={dpi:F3}], " +
                // ★ fullScreenMode를 반드시 남긴다(2026-09-01): 이 값이 Windowed가 아니면 Unity가
                //   포커스를 잃을 때 창을 뒤로 보내므로, "캐릭터가 창 뒤로 넘어간다" 신고에서 이 한 줄이
                //   원인을 가른다. 이전 로그에는 이 값이 없어서 실기 확인이 불가능했다.
                $"fullScreenMode={Screen.fullScreenMode}(직전 불일치: 해상도={resolutionMismatch}, 모드={modeMismatch}), " +
                $"결과={(ok ? "성공(오차 1px 이내)" : "미달 — 다음 시도에서 재적용")}.");
        }

        /// <summary>
        /// 실행 중 디스플레이 구성 변경 감시 — <see cref="_fullScreenBoundsApplied"/> 재무장 지점
        /// (2026-08-31 perf-doc 지적: 그 플래그를 false로 되돌리는 경로가 아예 없어서 오버레이 창이
        /// 최초 기동 해상도에 영원히 박제됐다).
        ///
        /// 두 가지 안전장치가 있고 둘 다 없으면 안 된다:
        ///   (1) <b>적합 진행 중에는 관측하지 않는다.</b> 재적합은 Screen.SetResolution/창 크기를 우리가
        ///       직접 바꾸는 일이라, 그 와중의 관측은 "우리가 만든 변화"를 새 사건으로 오인한다.
        ///   (2) <b>에피소드가 끝나면 기준값을 다시 잡는다</b>(관측 대신 ResetBaseline 1회).
        ///       (1)과 합쳐 "재적합 -> 시그니처 변화 -> 재적합"의 무한 루프를 원천 차단한다.
        ///
        /// 디바운스(마지막 변화 후 0.75초 안정)는 전부 DisplayTopologyWatcher 안에 있다. 여기서 즉시
        /// 재적합을 걸면 해상도 전환 중간 상태마다 SetResolution이 불려 지금보다 큰 히치를 만든다.
        /// </summary>
        private void TickDisplayTopology()
        {
            // 진동으로 확정된 뒤에는 재무장 자체를 하지 않는다 — 재무장은 상한을 되돌리는 유일한 경로라,
            // 여기를 막지 않으면 위에서 멈춘 것이 다음 통지에 그대로 되살아난다(macOS판과 동일).
            if (_boundsOscillation.IsOscillating) return;

            bool fitInProgress = !_fullScreenBoundsApplied && _fullScreenApplyAttempts < MaxFullScreenApplyAttempts;
            if (fitInProgress) return;

            // 매 프레임 OS 디스플레이를 열거하는 것은 24시간 상주 앱에서 순수 낭비다. 누적 시간을 그대로
            // 감시기에 넘기므로 디바운스는 여전히 벽시계 기준으로 정확하다.
            _topologySampleTimer += Time.unscaledDeltaTime;
            if (_topologySampleTimer < TopologySampleIntervalSeconds) return;
            float sampleDelta = _topologySampleTimer;
            _topologySampleTimer = 0f;

            if (!_topologyBaselineSynced)
            {
                _topologyBaselineSynced = true;
                _topologyWatcher.ResetBaseline(SampleTopology());
                return;
            }

            if (!_topologyWatcher.Observe(SampleTopology(), sampleDelta)) return;

            _fullScreenBoundsApplied = false;
            _fullScreenApplyAttempts = 0;
            _fullScreenTimer = ReapplyIntervalSeconds; // 다음 TickFullScreenBounds에서 곧바로 1회.
            _topologyBaselineSynced = false;

            Debug.Log("[WindowsOverlayStateEnforcer] 디스플레이 구성 변경이 안정됐습니다 — " +
                $"{_topologyWatcher.Baseline}. 전체화면 재적합 루프를 다시 무장합니다" +
                $"({ReapplyIntervalSeconds}초 x {MaxFullScreenApplyAttempts}회 분산).");
        }

        /// <summary>
        /// 이번 틱의 화면 구성 지문. <b>OS가 주는 값만</b> 넣는다 — 우리 창 크기/위치에서 유도되는 값
        /// (AutoDpiScale 등)을 넣으면 재적합이 자기 자신을 다시 트리거한다(DisplayTopologyWatcher 문서).
        /// UI 밀도는 Win32WindowService가 GetDpiForWindow로 읽어 보고한 OS 값이라 안전하며, 해상도가
        /// 그대로인 배율 전용 변경(100% -> 150%)을 잡는 유일한 신호다.
        /// </summary>
        private DisplayTopologySignature SampleTopology()
        {
            int count = UniWindowController.GetMonitorCount();
            if (count <= 0) return DisplayTopologySignature.Invalid;
            if (!TryGetTargetMonitorRect(out Rect monitor)) return DisplayTopologySignature.Invalid;

            Resolution desktop = Screen.currentResolution;
            return DisplayTopologySignature.Create(count, monitor,
                new Vector2(desktop.width, desktop.height),
                ScreenCoordinateConverter.AutoUiDensityScale);
        }

        /// <summary>
        /// 창 중심이 속한 모니터의 사각형.
        ///
        /// ============================================================================
        /// ★ 2026-09-01 — 되먹임 차단(히스테리시스). 이 함수는 <see cref="SampleTopology"/>의 입력이다
        /// ============================================================================
        /// <see cref="StickMate.Platform.DisplayTopologyWatcher"/> 클래스 문서는 시그니처에
        /// <b>"우리 창의 크기/위치, 그리고 그로부터 유도되는 값"을 절대 넣지 말라</b>고 못박고 있다 —
        /// 넣으면 "재적합 -> 시그니처 변화 -> 재적합"의 자기 되먹임 루프가 되고, 이 앱에서 재적합 한
        /// 번은 <b>스왑체인 재생성 = 수백 ms 정지</b>다.
        ///
        /// 그런데 이 함수가 고르는 모니터는 <b>우리 창 중심</b>으로 결정되므로, 그 값이 시그니처로
        /// 들어가는 순간 위 금지를 우리 스스로 어기고 있었다. 실제 경로:
        ///   창이 1px 줄어 중심이 0.5px 이동 -> (창이 모니터 경계에 걸쳐 있거나 모든 모니터 밖으로
        ///   벗어나면) 폴백이 <b>0번 모니터</b>로 튄다 -> 시그니처 변화 -> 재적합 -> 창 기하 변화 -> …
        ///
        /// 그래서 두 곳을 고정한다:
        ///   (1) <b>직전에 고른 모니터를 먼저 검사</b>한다 — 중심이 여전히 그 안이면 목록 순서와
        ///       무관하게 같은 답을 준다(모니터가 겹쳐 배치된 구성에서도 답이 흔들리지 않는다).
        ///   (2) 어느 모니터에도 속하지 않으면 <b>0번으로 튀지 않고 직전 선택을 유지</b>한다.
        ///       "잠깐 좌표를 못 읽었다"와 "사용자가 창을 다른 모니터로 옮겼다"는 완전히 다른 사건인데,
        ///       0번 폴백은 전자를 후자로 오인해 재적합을 부른다.
        /// 진짜 모니터 이동(중심이 다른 모니터 <b>안</b>으로 들어감)은 (1)의 검사가 그대로 잡는다.
        /// </summary>
        private bool TryGetTargetMonitorRect(out Rect monitor)
        {
            monitor = default;
            int count = UniWindowController.GetMonitorCount();
            if (count <= 0) return false;

            Vector2 center = _controller.windowPosition + _controller.windowSize * 0.5f;

            // (1) 직전 선택 우선.
            if (_lastMonitorIndex >= 0 && _lastMonitorIndex < count)
            {
                Rect last = UniWindowController.GetMonitorRect(_lastMonitorIndex);
                if (last.width > 0f && last.height > 0f && last.Contains(center))
                {
                    monitor = last;
                    return true;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Rect r = UniWindowController.GetMonitorRect(i);
                if (r.width <= 0f || r.height <= 0f) continue;
                if (r.Contains(center))
                {
                    _lastMonitorIndex = i;
                    monitor = r;
                    return true;
                }
            }

            // (2) 어디에도 속하지 않음 — 직전 선택을 유지한다(없으면 그때만 0번).
            int fallback = _lastMonitorIndex >= 0 && _lastMonitorIndex < count ? _lastMonitorIndex : 0;
            Rect fallbackRect = UniWindowController.GetMonitorRect(fallback);
            if (fallbackRect.width <= 0f || fallbackRect.height <= 0f) return false;
            _lastMonitorIndex = fallback;
            monitor = fallbackRect;
            return true;
        }

        /// <summary>직전에 고른 모니터 인덱스(-1 = 아직 없음). 위 히스테리시스의 상태.</summary>
        private int _lastMonitorIndex = -1;

        /// <summary>
        /// 투명이 실제로 확인된 뒤에만 카메라 배경 RGB를 검정으로 낮춘다(알파는 보존).
        /// macOS판과 같은 이유이며 Windows에서 오히려 더 중요하다: 투명 창 합성이 알파 채널을
        /// 프리멀티플라이드로 다루므로 배경 RGB가 밝으면 캐릭터 가장자리에 밝은 프린지가 남는다.
        /// 투명화가 실패한 상황에서는 손대지 않아 "밝은 배경 안의 캐릭터"(최소한 보이는 상태)가 된다.
        /// </summary>
        private void ApplyTransparentSafeCameraBackground()
        {
            // ★ 2026-09-01 — 진단 프로브를 <b>아래 조기 반환들보다 먼저</b> 세운다.
            //   이 진단이 가장 필요한 순간이 바로 "아래 교정이 실패해 배경이 0.94 회색으로 남는" 경우다.
            //   교정 성공 여부와 무관하게 관측이 돌아야 그 실패를 실기 로그에서 볼 수 있다.
            //   비용: 2초에 한 번, 지문이 바뀔 때만 한 줄(WindowsCompositionProbe 문서 참고).
            WindowsCompositionProbe.EnsureExists(_controller, ResolveConfig());

            if (_cameraBackgroundPremultiplyFixed) return;

            // ★ 2026-09-01 주의(반증 기록) — 이 가드는 <b>네이티브 진실이 아니다</b>.
            //   UniWindowController.isTransparent 게터는 캐시된 C# 필드(_isTransparent)를 그대로
            //   돌려주고, 그 값은 씬 에셋에서 이미 true로 직렬화돼 있다(Main.unity의
            //   `_isTransparent: 1`). 즉 이 줄은 사실상 항상 통과하며, 문서가 주장하던
            //   "투명화가 실패하면 밝은 회색을 유지한다"는 방어는 <b>성립하지 않는다</b>.
            //   같은 착각이 오늘 isTopmost에서 실제 버그로 드러났다(Tasklist 과학적 토론 로그).
            //   실측 대체 수단이 없어 지금은 그대로 두되, 위 프로브가 실기에서 이 상황을 잡아 준다.
            if (!_controller.isTransparent) return;

            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null) return;

            Color before = cam.backgroundColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, before.a);
            _cameraBackgroundPremultiplyFixed = true;

            Debug.Log($"[WindowsOverlayStateEnforcer] 투명 확인됨 — 카메라 배경 RGB를 검정으로 교정 " +
                $"({before.r:F2},{before.g:F2},{before.b:F2},{before.a:F2}) -> (0.00,0.00,0.00,{before.a:F2}). " +
                "알파는 그대로 유지. 렌더 품질 실측은 바로 아래 [렌더품질] 줄에 있습니다.");

            LogRenderQualityDiagnostics(cam);
        }

        /// <summary>[렌더품질] 줄은 프로세스당 한 번이면 충분하다 — 24시간 상주 앱이라 반복 금지.</summary>
        private bool _renderQualityDiagnosticsLogged;

        /// <summary>
        /// ★ 2026-09-02 — 렌더 품질 <b>실측</b> 진단. macOS(<c>MacOverlayStateEnforcer</c>)에만 있던 것을
        /// Windows에도 붙였다. <b>태그·필드 순서·문구를 macOS와 같게</b> 맞춘 것이 핵심이다 —
        /// 사용자에게 "Player.log에서 <c>[렌더품질]</c> 줄을 찾아 보내 주세요"라고 말할 때 그 지시가
        /// 두 플랫폼에서 <b>같은 문장</b>이어야 하고, 돌아온 두 줄을 나란히 놓고 비교할 수 있어야 한다.
        /// (Windows 실기 측정이 이 개발 머신에서 불가능하다는 제약이 바로 이 줄의 존재 이유다.)
        ///
        /// <para>직전까지 Windows는 위 배경 교정 로그에 MSAA 요청/실측만 끼워 넣고 있었고
        /// <b>획 두께는 한 번도 재지 않았다</b>. 그래서 "선이 얇아서 계단이 보이는가(획 하한 미달),
        /// 아니면 렌더 해상도가 낮은가(MSAA/DPI)"를 Windows에서는 구분할 수 없었다.</para>
        ///
        /// <para><b>계산은 여기 없다.</b> 월드→물리픽셀→OS 포인트 환산과 하한
        /// (<c>StickConfig.MinStrokeScreenPoints</c>) 대비 판정은 전부 플랫폼 중립
        /// <see cref="StrokeWidthDiagnostics"/>가 한다. 이 메서드가 하는 일은 사실 조회와 출력뿐이다
        /// (CLAUDE.md: "정책 판정 로직은 플랫폼 중립 위치에, 플랫폼 전용 코드는 사실 조회만").
        /// 여기에 환산을 인라인하면 그 순간 두 플랫폼이 다른 숫자를 내기 시작한다 —
        /// <c>FullscreenSuspendPolicy</c> 사고와 같은 형태다.</para>
        /// </summary>
        private void LogRenderQualityDiagnostics(Camera cam)
        {
            if (_renderQualityDiagnosticsLogged) return;
            _renderQualityDiagnosticsLogged = true;

            StrokeWidthDiagnostics.Report strokes = StrokeWidthDiagnostics.Measure(cam, ResolveConfig());

            int requested = QualitySettings.antiAliasing;
            int actual = Screen.msaaSamples;
            string verdict = actual <= 1
                ? "MSAA 꺼짐(계단 현상 그대로 노출)"
                : (actual == requested
                    ? $"요청대로 적용됨 — 가장자리 알파 단계 {actual + 1}개(0 포함)"
                    : $"★ 요청({requested})과 실측({actual})이 다름 — 하드웨어/렌더경로가 낮춘 것");

            Debug.Log("[렌더품질] MSAA 요청=" + requested + "x, **실측 Screen.msaaSamples=" + actual + "x** -> " + verdict +
                $" | 품질레벨={QualitySettings.names[QualitySettings.GetQualityLevel()]}" +
                $" | allowMSAA={cam.allowMSAA}, allowHDR={cam.allowHDR}, targetTexture={(cam.targetTexture == null ? "없음(백버퍼 직접)" : "있음(RT 우회 — MSAA 경로 이탈 의심)")}" +
                $" | 카메라픽셀=({cam.pixelWidth}x{cam.pixelHeight}), Screen=({Screen.width}x{Screen.height}), dpi={Screen.dpi:F0}" +
                $" | orthographicSize={cam.orthographicSize:F2} -> {strokes.PixelsPerWorldUnit:F1} 물리픽셀/유닛" +
                $" | {StrokeWidthDiagnostics.Describe(strokes)}" +
                // ★ Windows에만 있는 항목: 표시 배율(GetDpiForWindow/96). macOS는 이 값이 창 폭 비에
                //   실려 오지만 Windows는 별도 조회라, 획 두께 pt 환산의 근거로 함께 남긴다.
                $" | UI 밀도(표시 배율)={ScreenCoordinateConverter.AutoUiDensityScale:F3}" +
                $" | GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
        }
    }
}
#endif
