#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
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
        /// <summary>부착 제한 시간(초). ★ 2026-09-28 — 값이 <b>플랫폼 중립 정본</b>으로 옮겨졌다.
        /// 그전까지 양 Enforcer가 각자 <c>15f</c>를 들고 있었고, 둘 다 <c>#if UNITY_STANDALONE_*</c> 안이라
        /// 테스트가 참조할 수 없어 어긋나도 아무도 몰랐다(<see cref="ReapplyAttempts"/>와 같은 이유).
        /// 기동 표시 보류가 <b>같은 예산</b>을 쓴다 — 새 상수를 만들지 않는다.</summary>
        private const float AttachTimeoutSeconds = OverlayStateReapplyPolicy.AttachTimeoutSeconds;

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

        /// <summary>앱 전환 표면(작업표시줄 버튼 · Alt+Tab) 제외(2026-09-03). macOS가 시작 시
        /// <c>NSApplicationActivationPolicyAccessory</c>로 Dock/⌘Tab에서 빠지는 것과 <b>같은 목적</b>이며,
        /// 이 라운드에 두 플랫폼이 처음으로 대칭이 됐다. 규칙은 플랫폼 중립
        /// <see cref="StickMate.Platform.AppSwitcherPresencePolicy"/>에 있고 여기는 실행만 한다.
        /// 위 해소기와 마찬가지로 자기 HWND를 스스로 해석하므로 <see cref="OverlayHandle"/>에 의존하지 않는다.</summary>
        private readonly WindowsToolWindowStyleControl _toolWindowStyle = new WindowsToolWindowStyleControl();

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
            // ★ 2026-09-14 화면 변경 유예 — 부착·컨트롤러 조기 반환보다 <b>앞</b>이다. 모니터가 빠지는 순간 창
            //   크기가 (0,0)으로 읽히거나 컨트롤러가 사라져도 벽시계 상한 해제(R-2a)는 반드시 돈다.
            TickDisplayChangeHold();

            // ★ 2026-09-03 (사용자 확정 "실행시 시스템 트레이에 표시되어야함") — 트레이 아이콘.
            //   <b>이 자리가 중요하다.</b> 아래 `if (_controller == null) return;`와 부착 판정
            //   (`if (!attached) return;`)보다 **위**에 있어야 한다: 트레이는 우리 전용 호스트 창
            //   위에서 도는 물건이라 오버레이 창의 부착과 아무 상관이 없고, 오히려 **부착이 영영
            //   실패한 환경에서야말로 유일한 탈출구**가 된다(그 환경에서는 캐릭터도 톱니도 화면에
            //   없다). 부착 뒤로 내리면 "앱은 떠 있는데 끌 방법이 없는" 상태를 그대로 방치하게 된다.
            //   상한/옵트아웃 판정은 플랫폼 중립 SystemTrayPresencePolicy가 내린다.
            WindowsSystemTrayIcon.Tick(Time.unscaledDeltaTime);

            if (_controller == null) return;

            _elapsed += Time.unscaledDeltaTime;

            // ★ 2026-09-28 기동 표시 보류 — 부착 전 근백색(0.94) 노출을 스플래시 배경과 같은 어두운 값으로
            //   덮고, 상한(부착 제한 시간)에 닿으면 반드시 근백색으로 되돌린다.
            //   이 자리가 부착 판정보다 앞인 것이 핵심이다: 보류가 필요한 구간이 정확히 "부착 전"이고,
            //   첫 프레임부터 걸려야 첫 present를 덮는다(Enforcer는 StickmanAgent.Start에서 만들어지므로
            //   이 Update는 첫 렌더보다 앞선다). 판정·전이·상한은 플랫폼 중립 StartupPresentationHold 한 곳.
            BeginStartupPresentationHoldIfNeeded();
            TickStartupPresentationHold();

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
                    // ★ 2026-09-28 — 여기가 「탈출구」다(아래 메서드 문서). 경고만 남기고 끝내면
                    //   조기 해제를 뺀 이 플랫폼에서는 전체화면 불투명 창이 영원히 남는다.
                    ReleaseFullscreenAfterAttachFailureIfPolicyRequires();
                    // ★ 2026-09-28 — 제목표시줄 복귀와 배경 복귀는 <b>한 사건</b>이다
                    //   (둘 다 "투명을 포기했다"는 같은 사실의 결과다). 멱등이라 상한 복원이 이미
                    //   일어났으면 아무 일도 하지 않는다.
                    RestoreStartupPresentationHoldOnAttachFailure();
                }
                return;
            }

            if (!_attachDetected)
            {
                _attachDetected = true;
                ApplyTransparentSafeCameraBackground();
                // ★ 2026-09-28 — 위 교정이 <b>실제로 걸렸을 때만</b> 기동 표시 보류를 넘긴다.
                //   걸리지 않았으면(투명 실패/카메라 없음) 보류를 유지해 상한에서 근백색으로 되돌린다 —
                //   검정-on-검정(잉크색이 검정인 사용자에게 아무것도 안 보임)을 막는 유일한 경로다.
                NoteStartupPresentationHandoverIfCorrected();
                // 창이 실제로 존재하는 이 시점에 앱 전환 표면에서 뺀다.
                // macOS판(MacOverlayStateEnforcer)이 <b>같은 자리</b>에서
                // MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce()를 부른다 —
                // 그 대칭 자체를 PlatformParityAuditTests가 잠근다. 근거/한계는
                // Platform/AppSwitcherPresencePolicy.cs 클래스 문서 참고.
                _toolWindowStyle.ApplyOnce();
                Debug.Log($"[WindowsOverlayStateEnforcer] 창 부착 감지 — windowSize={windowSize}, " +
                    $"clientSize={_controller.clientSize}, windowPosition={_controller.windowPosition}, " +
                    $"경과 {_elapsed:F2}초. 이제 목표 상태를 재적용합니다.");
                // Phase 0 계측 — 여기가 유일한 호출 지점이다. 창이 방금 붙었고 우리가 아직 아무것도
                // 옮기지 않은 시점이라 "Unity가 놓아 준 자리"가 그대로 찍힌다(그 자리가 이번 조사의 대상이다).
                EmitMonitorTopologyOnce();
                _timer = ReapplyIntervalSeconds;
                // ★ 2026-09-07 — 이 재무장 관용구(다른 세 자리 — TickDisplayTopology 재무장,
                //   ReArmFullScreenFitForNewTarget, ReArmFullScreenFitAfterNativeWindowMove —
                //   는 이미 "_fullScreenTimer = ReapplyIntervalSeconds"로 곧바로 1회를 보장하고
                //   있었다)가 정작 최초 부착 시점에는 빠져 있었다. 그 결과 "부착감지 -> _fullScreenTimer가
                //   0부터 새로 0.5초 채움 ->
                //   그제서야 TickFullScreenBounds() 첫 시도"라는 신규 대기가 매 세션 기동마다
                //   껴 있었고, 그 0.5초 동안은 전체화면->창모드 전환(투명 네이티브 처리의 전제조건)이
                //   아예 시도조차 되지 않아 씬 카메라 배경색(근백색 0.94)이 알파 무시된 채 불투명하게
                //   노출된다(Windows 실기 확인 버그 — 기동 초반 ~2초 흰 배경). 여기서
                //   ReapplyIntervalSeconds를 미리 채워 두면 바로 아래 TickFullScreenBounds()가
                //   <b>같은 프레임</b>에서 곧바로 첫 시도를 한다 — 그 외 재시도 주기/상한/이후 로직은
                //   전혀 건드리지 않는다.
                _fullScreenTimer = ReapplyIntervalSeconds; // 다음 TickFullScreenBounds에서 곧바로 1회.
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
            // ★ 2026-09-05 진단 — isClickThrough 캐시(순수 C# 필드)를 함께 넘긴다. 해소기가 그 엣지로
            //   "LAYERED가 붙은 프레임"을 타임라인에 남긴다(WindowsLayeredHybridResolver.Tick 문서). 비용은 필드 읽기 1회.
            _layeredHybridResolver.Tick(Time.unscaledDeltaTime, (int)_controller.transparentType, _controller.isClickThrough);
            // ★ 2026-09-03 — 앱 전환 표면 제외 비트가 <아직 서 있는지> 2초마다 되묻는다.
            //   라이브러리의 SetClickThrough는 같은 GWL_EXSTYLE을 읽고-고쳐-쓰기 하므로 우리 비트를
            //   보존할 것으로 판단하지만, 이 개발 머신에 Windows가 없어 실행으로 확인할 수 없다.
            //   그래서 추측을 코드에 박는 대신 <감시>를 둔다 — 사라지면 다시 켜고 한 번 경고한다.
            _toolWindowStyle.Tick(Time.unscaledDeltaTime);
            // ★ 2026-09-03 (리더 판정 (b)) — 스타일 비트는 Alt+Tab만 닫는다. 이미 만들어진
            //   <작업표시줄 버튼>은 셸에게 직접 지우게 한다(ITaskbarList::DeleteTab).
            //   ShowWindow 왕복은 기각됐다 — 우리 창의 표시 상태를 바꾸면 바로 이 루프와
            //   TickTopmostWatchdog이 재적용하려 다툰다(같은 종류의 충돌로 영구 비활성된 해소기가
            //   이미 있다). DeleteTab은 창 상태를 한 비트도 건드리지 않는다.
            //   상한(3회)에 도달하면 내부에서 즉시 반환하므로 상주 비용은 0으로 수렴한다.
            WindowsTaskbarButtonRemover.Tick(Time.unscaledDeltaTime);

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

            // ★ 2026-09-14 동결 계측 — 네이티브 SetBorderless(SetWindowPos 4회 = 표면 재생성)가 걸리는 대입 직전.
            //   창 사각형을 바꾸는 경로만 적는다(FreezeForensicsPolicy.ShouldRecordTransparencyReassign).
            //   ★ 가드 블록 <b>밖</b>에 같은 조건으로 둔다 — OverlayTransparencyReapplyTests가 "재대입 바로 위 줄이
            //   정책 가드여야 한다"를 줄 단위로 잠그고 있고, 그 불변식(가드 밖 재대입 금지)을 흐리지 않기 위해서다.
            if (OverlayStateReapplyPolicy.CausesWindowResize(transparency) && FreezeForensics.IsActive)
            {
                FreezeForensics.RecordTransparencyReassign(causesWindowResize: true,
                    UniWindowController.GetMonitorCount(),
                    $"Windows isTransparent 재대입 — {OverlayStateReapplyPolicy.Describe(transparency)}, " +
                    $"재적용 {_appliedCount}/{ReapplyAttempts}, SetBorderless 누적 {_borderlessResizeEpisodes + 1}회, " +
                    $"창 {_controller.windowPosition} {_controller.windowSize}");
            }

            if (OverlayStateReapplyPolicy.CausesWindowResize(transparency))
            {
                _controller.isTransparent = DesiredTransparent;
                _borderlessResizeEpisodes++;
                // ★★ 2026-09-02 — 방금 부른 네이티브 SetBorderless는 <b>창을 옮긴다</b>.
                //   libuniwinc.cpp:736~ 이 프레임→보더리스 전환에서
                //       newX = rcWin.left + bw;  newY = rcWin.top + (dy - bh);
                //   로 창을 옛 클라이언트 원점(= 테두리 두께 + 캡션 높이만큼 안쪽)으로 옮긴다.
                //   그러므로 이 호출 직후의 창 기하는 <b>우리가 확정해 둔 값이 아니다</b>.
                //   확정을 그대로 두면 되돌릴 주체가 없다 — 실기에서 모니터 원점 +(11,45)에
                //   눌러앉은 상태가 정확히 그것이다.
                //   ★ 이것이 1px 래칫을 되살리지 않는 이유: 재무장은 _fullScreenApplyAttempts만
                //     0으로 되돌리고, 실제 쓰기를 막는 <b>수명 상한</b>
                //     (_setResolutionCalls / _windowResizeCalls, 각각 4)은 <b>절대 되돌리지 않는다</b>.
                //     즉 프로세스 전체에서 창 리사이즈는 여전히 최대 4회다. 위치 이동(SetWindowPos +
                //     SWP_NOSIZE)은 클라이언트 영역을 바꾸지 않으므로 스왑체인 재생성이 아니다.
                //   macOS에는 이 재무장이 없다(있으면 안 된다) — Swift의 _setWindowBorderless는
                //   styleMask 한 줄이라 frame을 건드리지 않는다(위 재적용 블록 주석의 원문 인용 참고).
                ReArmFullScreenFitAfterNativeWindowMove();
            }

            bool topmostSkipped = _topmostWatchdog.TryReadOsTopmost(OverlayHandle, out bool osTopmost)
                && osTopmost == DesiredTopmost;
            if (!topmostSkipped) _controller.isTopmost = DesiredTopmost;
            _controller.isClickThrough = DesiredClickThrough;

            // ★ 2026-09-30 — 이 줄에 <b>벽시계</b>를 싣는다(진단 전용, 동작 변경 0줄).
            //
            //   Player.log에는 줄마다 시각이 없다. 그래서 "재적용 5회 = 0.5초 x 5 = <b>2.5초 구간</b>"이
            //   <b>상수에서 유도된 값</b>인데도 <b>측정값처럼</b> 인용되는 사고가 났다. 사용자 실측은
            //   「8초정도 흰화면깜박이 한 5번정도함」(2026-09-30 Windows 신고)으로 <b>3배 이상</b> 길다.
            //
            //   두 값이 갈리는 이유는 결함이 아니라 이 루프의 형태다: <see cref="ReapplyIntervalSeconds"/>는
            //   <b>주기가 아니라 하한</b>이다(바로 위 `_timer += dt; if (_timer < ReapplyIntervalSeconds) return;`).
            //   이 틱이 SetBorderless를 부르면 그 한 번이 SetWindowPos 4회 = 클라이언트 영역 변경 4회 =
            //   스왑체인/DWM 리디렉션 표면 재생성 4회이고(등식 자체를 OverlayTransparencyReapplyTests가
            //   "재적용 1회 = SetBorderless 1회 = SetWindowPos 4회"로 잠근다), 실기 실측이 재생성 1회당
            //   268ms(OverlayStateReapplyPolicy 클래스 문서의 [프레임스파이크] 인용) ~ 최대 프레임 407ms다.
            //   즉 한 회차가 1초를 넘겨 <b>0.5초 하한을 삼킨다</b> — 그래서 실제 간격은 상한이 아니라
            //   이 틱이 한 일의 정지 시간이 지배한다.
            //
            //   ⇒ 앞으로 간격을 <b>상수에서 유도하지 말고</b> 이 줄의 「경과」 차이로 <b>잰다</b>.
            //   (읽는 법 설명은 로그 줄에 싣지 않는다 — StallAttribution 클래스 문서의 2026-09-02 판정과 같은 규칙.)
            Debug.Log($"[WindowsOverlayStateEnforcer] 재적용 {_appliedCount}/{ReapplyAttempts} " +
                $"(경과 {_elapsed:F2}초) " +
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

            // ★★★ 2026-09-03 — <c>IsSuspended</c> → <c>HidesScreenSurfaces</c>. 이 워치독이 보류하는
            //   이유는 <i>"게임 위로 기어 올라가지 않는다"</i>(원칙 2)인데, 사용자 명시 숨김 단독에서는
            //   전체화면 게임이 없고 <b>톱니와 열린 창이 그대로 떠 있다</b> — 그 표면들이 항상위를
            //   잃으면 사용자가 [보이기]를 누를 창이 다른 창 밑으로 가라앉는다(탈출구 손실).
            //   축 1(전체화면 게임 감지)에서는 이 값이 참이라 보류 동작이 예전 그대로다.
            bool suspended = _agent != null && _agent.HidesScreenSurfaces;
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
            // ★ 2026-09-14 화면 변경 유예 — 조용한 구간에는 SetResolution·리사이즈·이동을 보류한다. 타이머와 시도 횟수를
            //   소모하지 않고 돌아가므로, 유예가 끝나면 재적합은 예전과 같은 순서·같은 상한으로 돈다.
            if (DisplayChangeHold.ShouldDeferFit)
            {
                DisplayChangeHold.NotifyFitDeferred(Time.frameCount);
                return;
            }

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
            bool calledSetResolution = OverlayBoundsFitPolicy.ShouldSetResolution(
                Screen.width, Screen.height, targetPixelW, targetPixelH, !modeMismatch,
                BoundsEpsilonPixels, _setResolutionCalls, MaxSetResolutionCalls);
            if (calledSetResolution)
            {
                _setResolutionCalls++;
                // ★ 2026-09-14 동결 계측 — 스왑체인을 다시 만드는 호출 <b>직전</b>에 디스크까지 적는다(동작 변경 0).
                if (FreezeForensics.IsActive)
                {
                    FreezeForensics.Record(FreezeForensicsEvent.SetResolution, 0f, 0f, targetPixelW, targetPixelH,
                        UniWindowController.GetMonitorCount(),
                        $"Windows Screen {Screen.width}x{Screen.height} {Screen.fullScreenMode} -> {targetPixelW}x{targetPixelH} Windowed, " +
                        $"대상 모니터={monitor}, 누적 {_setResolutionCalls}/{MaxSetResolutionCalls}, 시도 {_fullScreenApplyAttempts}/{MaxFullScreenApplyAttempts}");
                }
                Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed);
                // ★ 2026-09-30 — 방금 쓴 것이 <b>마지막 한 장</b>이었으면 사용자에게 한 번 알린다.
                //   창 상태를 되돌리는 시도가 아니다(아래 메서드 문서) — 상한 자체는 그대로 둔다.
                NoticeSetResolutionCapIfReached();
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
                // ★ 2026-09-14 동결 계측 — 창 크기 대입(= SetWindowPos → 스왑체인 재생성) 직전.
                if (FreezeForensics.IsActive)
                {
                    FreezeForensics.Record(FreezeForensicsEvent.WindowResize, posBefore.x, posBefore.y,
                        monitor.width, monitor.height, UniWindowController.GetMonitorCount(),
                        $"Windows 창 크기 {sizeBefore.x}x{sizeBefore.y} -> {monitor.width}x{monitor.height}, " +
                        $"누적 {_windowResizeCalls}/{OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls}");
                }
                _controller.windowSize = monitor.size;
            }
            // ★ 2026-09-14 동결 계측 — 창 위치 대입 직전(크기는 그대로 = 스왑체인 재생성 아님). 대입 줄 자체는
            //   기존 한 줄 형태를 그대로 둔다(OverlayResizeRatchetTests가 그 가드 형태를 문자 그대로 잠근다).
            if (needsMove && FreezeForensics.IsActive)
            {
                FreezeForensics.Record(FreezeForensicsEvent.WindowMove, monitor.x, monitor.y,
                    needsResize ? monitor.width : sizeBefore.x, needsResize ? monitor.height : sizeBefore.y,
                    UniWindowController.GetMonitorCount(),
                    $"Windows 창 위치 ({posBefore.x},{posBefore.y}) -> ({monitor.x},{monitor.y})");
            }
            if (needsMove) _controller.windowPosition = monitor.position;

            Vector2 sizeAfter = _controller.windowSize;
            Vector2 posAfter = _controller.windowPosition;
            bool within = OverlayBoundsFitPolicy.Within(sizeAfter.x, sizeAfter.y,
                    monitor.width, monitor.height, BoundsEpsilonPixels)
                && OverlayBoundsFitPolicy.Within(posAfter.x, posAfter.y,
                    monitor.x, monitor.y, BoundsEpsilonPixels);

            // ★★★ 2026-09-02 — "완료" 확정을 <b>쓰기가 0인 틱</b>으로 미룬다.
            //
            // 직전까지 이 자리는 `if (within) _fullScreenBoundsApplied = true;`였고, 그 within은
            // **같은 틱에 우리가 방금 요청한 변화가 아직 반영되지 않은** 상태에서 잰 값이었다:
            //   · Screen.SetResolution은 프레임 끝에 적용된다(Unity 계약).
            //   · 그 전환(FullScreenWindow -> Windowed)은 창 스타일을 되살리고,
            //     그러면 다음 재적용 틱의 OS 실측이 "보더리스 아님"이 되어 네이티브 SetBorderless가
            //     다시 실행된다. 그 함수는 libuniwinc.cpp:736~ 에서
            //         newX = rcWin.left + bw;  newY = rcWin.top + (dy - bh);
            //     로 **창을 옛 클라이언트 원점으로 옮긴다**(프레임 두께만큼 오른쪽/아래로).
            //     150% 배율 실기에서 그 값이 정확히 (+11,+45)였다.
            // 우리가 그 전에 완료를 확정해 버리면 이 이동을 되돌릴 주체가 **아무도 없다**
            // (_fullScreenBoundsApplied를 내리는 유일한 경로가 디스플레이 구성 변경이다).
            //
            // 판정은 플랫폼 중립 한 곳(OverlayBoundsFitPolicy.ShouldLatchFitApplied)에 있고
            // macOS판 Enforcer도 같은 함수를 같은 자리에서 쓴다 — 그쪽 SetResolution도 똑같이 지연 적용이다.
            // ★ 비용이 늘지 않는 이유: 확정을 미루면 다음 틱이 한 번 더 도는데, 그 틱은 이미 불감대
            //   안이므로 `needsResize`/`needsMove`가 둘 다 false여서 **대입을 한 줄도 하지 않는다**.
            //   즉 OS 표면 재생성 횟수는 그대로다(늘어나는 것은 되읽기 두 번뿐).
            bool wroteThisTick = calledSetResolution || needsResize || needsMove;
            // ★ 2026-09-14 (verify-change 2차 X2c) — 확정 판정은 신호 객체가 한다(규칙은 ShouldLatchFitApplied 그대로).
            //   참이면 그 객체가 화면 변경 유예를 무장한다 — 무장 메서드는 따로 없다. 옛 `DisplayChangeHold.Arm();` 한 줄은
            //   매 프레임 틱으로 옮겨도 전량 초록이었다. 이제 무장을 옮기려면 이 판정(within·wroteThisTick)을 옮겨야 한다.
            bool ok = _fullScreenFitLatch.Evaluate(within, wroteThisTick);
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
                // ★ SetResolution 항목은 재-유도가 아니라 <b>실제로 부른 그 조건</b>을 찍는다.
                //   전에는 로그가 `(resolutionMismatch || modeMismatch) && !resolutionCapped`로 다시
                //   계산했는데, 그것은 규칙(ShouldSetResolution)과 다른 식이라 언젠가 갈라진다 —
                //   갈라지면 로그가 거짓말을 하고, 이 저장소는 그 사고를 이미 여러 번 겪었다.
                $"이번 틱 실행(SetResolution={calledSetResolution}, " +
                $"리사이즈={needsResize}, 이동={needsMove}, 불감대={BoundsEpsilonPixels:F0}px), " +
                $"기하 일치={within}(쓰기 있던 틱={wroteThisTick} -> 확정={ok}), " +
                $"clientSize={_controller.clientSize}, " +
                $"Screen=({Screen.width}x{Screen.height}) [목표 {targetPixelW}x{targetPixelH} 픽셀, dpi배율={dpi:F3}], " +
                // ★ fullScreenMode를 반드시 남긴다(2026-09-01): 이 값이 Windowed가 아니면 Unity가
                //   포커스를 잃을 때 창을 뒤로 보내므로, "캐릭터가 창 뒤로 넘어간다" 신고에서 이 한 줄이
                //   원인을 가른다. 이전 로그에는 이 값이 없어서 실기 확인이 불가능했다.
                $"fullScreenMode={Screen.fullScreenMode}(직전 불일치: 해상도={resolutionMismatch}, 모드={modeMismatch}), " +
                $"결과={(ok ? "성공(오차 1px 이내)" : "미달 — 다음 시도에서 재적용")}.");
        }

        /// <summary>이 프로세스에서 상한 도달 알림을 이미 냈는가(프로세스당 1회 — 아래 문서).</summary>
        private bool _setResolutionCapNoticed;

        /// <summary>
        /// ★ 2026-09-30 — <b><c>Screen.SetResolution</c> 수명 상한에 닿았음을 사용자에게 한 번 알린다.</b>
        ///
        /// ============================================================================
        /// 왜 필요한가 — 상한은 정당한데 «닿은 뒤»에 사용자가 알 방법이 없었다
        /// ============================================================================
        /// <see cref="MaxSetResolutionCalls"/>는 진동 루프(= 몇 초마다 수백 ms 정지)를 막는 안전장치이고
        /// <b>여기서 올리지도 없애지도 않는다</b>. 그런데 이 카운터는 <b>프로세스 수명 동안 한 번도
        /// 초기화되지 않는다</b> — 재무장 메서드 셋이 모두 「절대 되돌리지 않는다」고 못박고 있다
        /// (<see cref="ReArmFullScreenFitForNewTarget"/> · <see cref="ReArmFullScreenFitAfterNativeWindowMove"/> ·
        /// <see cref="TickDisplayTopology"/>). 그래서 외장 모니터를 몇 번 뺐다 꽂으면 넷을 다 쓰고,
        /// 그 뒤로는 <b>그 세션 안에서 창을 다시 맞출 수 없으며 재시작 말고 복구 경로가 없다.</b>
        ///
        /// <para>그때까지 남는 것은 아래 전체화면 확장 로그의 「★상한 도달」한 줄뿐이었다. 사용자는
        /// <c>Player.log</c>를 읽지 않으므로 실제로는 <b>「창이 화면에 안 맞는데 이유를 모르는 상태」</b>가 된다.</para>
        ///
        /// ============================================================================
        /// ★ 이것은 <b>복구 시도가 아니다</b>
        /// ============================================================================
        /// 창 위치·크기·스타일·스왑체인을 <b>한 비트도</b> 건드리지 않는다. 직전 조사가 재적용 루프 부활을
        /// 명시적으로 기각했다 — 드라이버가 회복 중인 순간에 <c>SetBorderless</c> → <c>SetWindowPos</c> 4회 →
        /// 스왑체인 재생성을 또 하면 새 P0를 만든다. 여기서 하는 일은 <b>상황과 해법(재시작)을 알리는 것</b> 하나다.
        ///
        /// <para><b>판정은 플랫폼 중립</b>
        /// (<see cref="OverlayBoundsFitPolicy.ShouldNoticeSetResolutionCapReached"/>)이고 <b>문안도 중립</b>
        /// (<see cref="SystemTrayPresencePolicy.RefitCapNoticeBody"/>)이다 — macOS Enforcer도 같은 상한을
        /// 같은 방식으로 쓰므로, 그쪽에 알림 창구가 생기면 이 두 개를 그대로 부른다
        /// (<c>FullscreenSuspendPolicy</c> 사고 재발 방지).</para>
        ///
        /// <para><b>트레이가 없으면</b>(옵트아웃 · 설치 실패 · 셸 트레이 부재) 알림은 못 뜨지만 <b>경고 로그는
        /// 반드시 남는다</b> — 못 띄운 것이 사건이 없었다는 뜻이 아니다. macOS에는 트레이 대응물 자체가 없다
        /// (<see cref="SystemTrayPresencePolicy.MacOsGapReason"/>).</para>
        ///
        /// <para><b>실기 미확인</b>: 이 머신에 Windows가 없어 풍선이 실제로 뜨는지 확인하지 못했다.</para>
        /// </summary>
        private void NoticeSetResolutionCapIfReached()
        {
            if (!OverlayBoundsFitPolicy.ShouldNoticeSetResolutionCapReached(
                    _setResolutionCalls, MaxSetResolutionCalls, _setResolutionCapNoticed))
            {
                return;
            }
            _setResolutionCapNoticed = true;

            bool shown = WindowsSystemTrayIcon.TryShowNotice(
                SystemTrayPresencePolicy.RefitCapNoticeTitle,
                SystemTrayPresencePolicy.RefitCapNoticeBody);

            Debug.LogWarning("[WindowsOverlayStateEnforcer] ★ Screen.SetResolution 수명 상한 " +
                $"{_setResolutionCalls}/{MaxSetResolutionCalls} 도달 — 이 세션에서는 창 해상도를 다시 맞추지 " +
                "않습니다(진동 루프 방지 장치이며 여기서 풀지 않습니다). 사용자 알림: " +
                $"\"{SystemTrayPresencePolicy.RefitCapNoticeBody}\" / 트레이 풍선=" +
                (shown ? "띄웠습니다" : "띄우지 못했습니다(트레이 아이콘 없음 — 옵트아웃/설치 실패/셸 트레이 부재)") +
                ". 창 위치·크기·스타일·스왑체인은 한 비트도 건드리지 않았습니다.");
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

            // ★ 2026-09-14 동결 계측 — 관측만 한다. 감시기 입력·판정·재무장은 한 줄도 바뀌지 않았다
            //   (예전 한 줄 `if (!Observe(SampleTopology(), dt)) return;`을 변수로 풀었을 뿐이다).
            //   변화 감지(t0) · 안정 · 원복을 벽시계와 함께 디스크에 즉시 적는다(FreezeForensicsPolicy 문서).
            bool wasSettling = _topologyWatcher.IsSettling;
            DisplayTopologySignature topologySample = SampleTopology();
            bool topologySettled = _topologyWatcher.Observe(topologySample, sampleDelta);
            FreezeForensics.ObserveTopologyTransition(wasSettling, _topologyWatcher.IsSettling, topologySettled,
                topologySample, "Windows");
            // ★ 2026-09-14 화면 변경 유예 — 같은 분류를 유예 상태기계에 넘긴다(감시기 판정은 무변경).
            DisplayChangeHold.OnTopologyTransition(
                FreezeForensicsPolicy.ClassifyTopologyTransition(wasSettling, _topologyWatcher.IsSettling, topologySettled),
                Time.unscaledTimeAsDouble, Time.frameCount);
            if (!topologySettled) return;

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

            // ★★ 2026-09-02 사용자 확정 규칙이 먼저다 — "그럼 왼쪽 오른쪽 선택할수 있게 기본은 왼쪽"
            //   (기본 = 가장 왼쪽 모니터, 사용자가 고르면 그 화면). 판정은 플랫폼 중립
            //   OverlayMonitorChoicePolicy 한 곳에 있고 여기서는 사실 조회와 창 이동만 한다.
            if (TryResolveChosenMonitorRect(count, out monitor)) return true;

            // 아래는 <b>폴백 전용</b>이다 — 모니터 목록을 아예 얻지 못했을 때만 온다.
            // 그 경우 0번으로 위장하지 않고 <b>예전 동작(창 중심이 속한 모니터 + 히스테리시스)</b>을
            // 그대로 유지한다(리더 지시 3항).
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

        /// <summary>직전에 고른 모니터 인덱스(-1 = 아직 없음). 위 히스테리시스의 상태.
        ///
        /// <para>★ <b>2026-09-02 — 이 히스테리시스는 이제 폴백 경로에서만 산다.</b> 리더 지시 2항에
        /// 대한 판단: <b>없애지 않고, 범위를 좁힌다</b>.</para>
        /// <list type="bullet">
        ///   <item><b>정상 경로에서는 필요 없어졌다.</b> 히스테리시스의 목적은 "창이 1px 줄어 중심이
        ///     움직이면 폴백이 0번으로 튀고 → 시그니처가 바뀌고 → 재적합이 자기 자신을 다시 부른다"는
        ///     <b>되먹임</b>을 막는 것이었다. 목표가 <b>주 모니터(고정)</b>가 되면 그 답은
        ///     <b>우리 창 기하와 완전히 무관</b>해진다 — 되먹임을 <b>감쇠</b>하는 대신 <b>원천에서 제거</b>한다.
        ///     <see cref="DisplayTopologyWatcher"/>가 자기 문서에서 요구하는 "우리 창에서 유도된 값을
        ///     시그니처에 넣지 말 것"도 이제 구조적으로 만족된다.</item>
        ///   <item><b>폴백 경로에서는 여전히 필요하다.</b> 그쪽은 아직 창 중심에서 유도하므로
        ///     되먹임 위험이 그대로다. 그래서 삭제하지 않는다.</item>
        /// </list></summary>
        private int _lastMonitorIndex = -1;

        // ============================================================================
        // 표시 모니터 선택 — 사실 조회(모니터 목록) + 좌표계 변환. 판정은 전부 중립 정책이 한다.
        // ============================================================================

        /// <summary>OS 모니터 목록 캐시. 재열거 주기를 두는 이유: 이 함수는 토폴로지 관측(0.25초)에서도
        /// 불리므로, 매번 열거하면 24시간 상주 앱에서 순수 낭비다.</summary>
        private readonly List<OsMonitorFact> _osMonitors = new List<OsMonitorFact>(8);
        // ★ 2026-09-14 (debugger D1) — 누적 타이머(`+= Time.unscaledDeltaTime`)를 벽시계 문으로 바꿨다.
        //   이 함수는 토폴로지 표본(0.25초)·재적합(0.5초) 게이트 뒤에서만 불려 한 번에 한 프레임 dt만 쌓였고,
        //   "1초마다"가 실제로는 평상시 약 15초·재적합 중 약 30초마다였다(루프 60Hz 기준, WallClockIntervalGate 문서).
        //   첫 호출은 여전히 즉시 1회다. 참조 형식이라 readonly여도 복사 함정이 없다.
        private readonly WallClockIntervalGate _osMonitorRefreshGate = new WallClockIntervalGate();
        private const float OsMonitorRefreshIntervalSeconds = 1f;

        // ============================================================================
        // ★ 2026-09-14 화면 변경 유예(완화 1안) 배선 — 판정·순서는 플랫폼 중립 DisplayChangeHoldDriver.
        //   여기서는 신호를 넘기고 사실 조회 훅을 주입할 뿐이다. 클릭 관통·투명·항상위는 건드리지 않는다(원칙 2).
        // ============================================================================
        // ★ 2026-09-14 (verify-change 2차 X2c) — 적합 확정 판정 객체. 유예 구동기의 무장 원천과 <b>같은 인스턴스</b>여야 한다
        //   (PlatformParityAuditTests가 인스턴스 1개·판정 호출 1곳을 잠근다).
        private readonly FullScreenFitLatchSignal _fullScreenFitLatch = new FullScreenFitLatchSignal();
        private DisplayChangeHoldDriver _displayChangeHold;
        private UniWindowController _holdSubscribedController;
        private UniWindowController.OnMonitorChangedDelegate _onLibraryMonitorChanged;

        private DisplayChangeHoldDriver DisplayChangeHold => _displayChangeHold ??= new DisplayChangeHoldDriver(
            new DisplayChangeHoldDriver.Hooks
            {
                PlatformTag = "Windows",
                IsFitPending = IsFullScreenFitPending,
                ForceRefreshOsMonitors = RefreshOsMonitorList,
                SetRenderHold = FramePacing.SetDisplayChangeHold,
                ActualRenderedFrames = () => RenderDiagnostics.ActualRenderedFrameCount,
                RenderedFrameCount = () => Time.renderedFrameCount,
                EffectiveRenderFrameInterval = () => FramePacing.EffectiveRenderFrameInterval,
                MonitorCount = UniWindowController.GetMonitorCount,
            },
            _fullScreenFitLatch,
            DisplayChangeHoldPolicy.ReadDisabledFromEnvironment());

        private void TickDisplayChangeHold()
        {
            DisplayChangeHoldDriver hold = DisplayChangeHold;
            if (hold.IsDisabled) return;   // 끄기 스위치 — 구독·렌더 입력·재적합 보류·원장 줄 전부 없음(이전 동작).
            if (_controller != null && !ReferenceEquals(_holdSubscribedController, _controller))
            {
                _onLibraryMonitorChanged ??= OnLibraryMonitorChanged;
                if (!ReferenceEquals(_holdSubscribedController, null))
                    _holdSubscribedController.OnMonitorChanged -= _onLibraryMonitorChanged;
                _controller.OnMonitorChanged += _onLibraryMonitorChanged;
                _holdSubscribedController = _controller;
            }
            hold.Tick(Time.unscaledTimeAsDouble, Time.frameCount);
            // ★ 2026-09-30 — 유예가 <b>방금 해제된</b> 프레임을 잡는다. 해제는 위 Tick 안에서만 일어나므로
            //   (상태기계의 Release 호출자가 Tick 하나다) 이 자리가 해제 직후의 유일하고 확실한 지점이다.
            //   여기서 하는 일은 «읽기 전용 사실 한 줄»뿐이다 — 판정도, 재적용도, 쓰기도 없다.
            bool holdingNow = hold.IsHolding;
            if (_displayChangeHoldWasHolding && !holdingNow) LogPostHoldFactsOnce(hold);
            _displayChangeHoldWasHolding = holdingNow;
        }

        /// <summary>직전 프레임에 유예가 걸려 있었는가(해제 가장자리 검출용 — 상태를 바꾸지 않는다).</summary>
        private bool _displayChangeHoldWasHolding;

        /// <summary>유예 해제 직후 사실 줄을 몇 번 남겼는가. 아래 상한까지만 남긴다.</summary>
        private int _postHoldFactLines;

        /// <summary>
        /// 유예 해제 직후 사실 줄의 <b>프로세스 수명 상한</b>. 24시간 상주 앱이라 진단이 무제한으로
        /// 디스크·로그를 먹는 경로를 원천 차단한다(<c>WindowsTopmostWatchdog.MaxDetailLogs</c>와 같은 관례).
        /// 모니터 착탈은 세션당 몇 번이라 8이면 신고 재현 구간을 충분히 덮는다.
        /// </summary>
        private const int MaxPostHoldFactLines = 8;

        /// <summary>
        /// ★ 2026-09-30 — <b>화면 변경 유예 해제 직후의 읽기 전용 기하·합성 사실 한 줄.</b>
        ///
        /// ============================================================================
        /// 왜 필요한가 — 확정이 서면 기하 확인이 <b>영구히</b> 멈춘다
        /// ============================================================================
        /// <see cref="TickFullScreenBounds"/>는 첫 줄에서 <c>_fullScreenBoundsApplied</c>면 즉시 반환한다.
        /// 즉 확정 이후에는 창 기하를 <b>한 번도 다시 읽지 않는다</b>(그것이 래칫을 막는 옳은 설계다).
        /// 대가는 진단 쪽이다: 유예가 끝난 뒤 기하가 틀어져 있어도 <b>기록이 남지 않는다.</b>
        /// 외장 모니터 분리 뒤 흰 화면 신고에서 정확히 그 구간이 비어 있었다.
        ///
        /// ============================================================================
        /// ★ 이 메서드가 <b>하지 않는 것</b> (직전 조사의 명시적 기각 사항)
        /// ============================================================================
        /// <list type="bullet">
        ///   <item><b>재적용을 하지 않는다.</b> <c>MarkDirty()</c>·<c>ReArm*</c>·<c>Screen.SetResolution</c>·
        ///     창 크기/위치 대입이 한 줄도 없다. 드라이버가 회복 중인 순간에 <c>SetBorderless</c> →
        ///     <c>SetWindowPos</c> 4회 → 스왑체인 재생성을 또 하는 것이 새 P0를 만드는 경로다.</item>
        ///   <item><b>판정을 하지 않는다.</b> 「기하가 틀렸다/맞았다」를 쓰지 않는다 — 가설(프리멀티플라이드 워시)이
        ///     아직 실기 로그로 확정되지 않았고, 확정 전에 판정을 코드에 박으면 반대 방향 사고를 만든다.</item>
        ///   <item><b>목표 모니터를 다시 고르지 않는다.</b> <see cref="TryGetTargetMonitorRect"/>는 선택이 바뀌면
        ///     <see cref="ReArmFullScreenFitForNewTarget"/>를 부르는 <b>쓰기 경로</b>다. 여기서는 직전에 고른
        ///     인덱스의 사각형만 그대로 읽고, 그 사실을 줄에 적는다(재판정 없음).</item>
        /// </list>
        ///
        /// <para><b>무엇을 읽는가</b>: 우리 창의 OS 실측 사각형(<c>GetWindowRect</c>) · 확장 스타일
        /// (<c>GWL_EXSTYLE</c>) · 창 스타일(<c>GWL_STYLE</c>) · <b>DWM 클로킹</b>(<c>DWMWA_CLOAKED</c>) ·
        /// 카메라 배경 RGBA · <c>Screen.fullScreenMode</c>와 해상도 · 적합 상한 소모 상태 · 라이브러리가
        /// 주장하는 창 기하. 전부 조회이며, 실패는 「모름」으로 적는다(0으로 접지 않는다).</para>
        ///
        /// <para>★ <b>DWM 클로킹이 이 줄의 핵심</b>이다. 클로킹된 창은 <c>IsWindowVisible</c>이 true인데도
        /// 화면에 그려지지 않는다 — 워치독 하트비트가 「렌더는 나갔다」를 말하는데 화면이 비어 있는 경우의
        /// 1순위 후보이면서, 지금까지 <b>남의 창 판정에만</b> 쓰이고 우리 창에는 한 번도 물어본 적이 없다.</para>
        ///
        /// <para><b>실기 미확인</b>: 이 머신에 Windows가 없어 실행으로 확인하지 못했다. 세 조회 모두
        /// <c>WindowsTopmostWatchdog</c>의 기존 조회 전용 P/Invoke를 그대로 쓴다(새 extern 0개).</para>
        /// </summary>
        private void LogPostHoldFactsOnce(DisplayChangeHoldDriver hold)
        {
            if (_postHoldFactLines >= MaxPostHoldFactLines) return;
            _postHoldFactLines++;
            bool last = _postHoldFactLines >= MaxPostHoldFactLines;

            IntPtr probeHandle = UniWinCNativeHandle.TryGetNative();
            bool handleIsNative = probeHandle != IntPtr.Zero;
            if (!handleIsNative) probeHandle = OverlayHandle;

            string osRect = WindowsTopmostWatchdog.TryReadWindowRectangle(probeHandle,
                    out int l, out int t, out int r, out int b)
                ? $"({l},{t})-({r},{b}) = {r - l}x{b - t}"
                : "모름(조회 실패/핸들 없음)";
            string exStyle = WindowsTopmostWatchdog.TryReadExStyle(probeHandle, out long ex)
                ? $"0x{ex:X}"
                : "모름";
            string style = WindowsWindowStyleProbe.TryReadStyle(probeHandle, out long st)
                ? $"0x{st:X}"
                : "모름";
            string cloaked = WindowsTopmostWatchdog.TryReadCloakedAttribute(probeHandle, out int cloakBits)
                ? (cloakBits == 0 ? "아님(0)" : $"★클로킹됨(0x{cloakBits:X}) — IsWindowVisible이 true여도 화면에 안 그려진다")
                : "모름(DwmGetWindowAttribute 실패)";

            Camera cam = ResolveHoldCamera();
            string background = cam != null
                ? $"({cam.backgroundColor.r:F3},{cam.backgroundColor.g:F3},{cam.backgroundColor.b:F3},{cam.backgroundColor.a:F3})"
                : "카메라 없음";

            // ★ 직전에 고른 모니터의 사각형만 그대로 읽는다 — 목표를 다시 고르지 않는다(위 문서 3항).
            int monitorCount = UniWindowController.GetMonitorCount();
            string targetRect = _lastMonitorIndex >= 0 && _lastMonitorIndex < monitorCount
                ? UniWindowController.GetMonitorRect(_lastMonitorIndex).ToString()
                : "모름(직전 선택 없음/범위 밖)";

            string libraryRect = _controller != null
                ? $"{_controller.windowPosition} {_controller.windowSize} client={_controller.clientSize}"
                : "컨트롤러 없음";

            // ★ 항목 순서가 계약이다 — 원장 줄은 FreezeForensicsPolicy.MaxDetailChars(600자)에서 <b>뒤가 잘린다</b>
            //   (Player.log 쪽은 잘리지 않는다). 이 줄은 해상도·모니터 사각형이 들어가 길어질 수 있으므로
            //   <b>값이 큰 항목을 앞에</b> 둔다: DWM 클로킹 → 카메라 배경 → 화면 모드 → 적합 상한 → 창 기하 →
            //   스타일 → 모니터. 뒤에서 잘려도 신고를 가르는 세 값(클로킹·배경·상한)은 남는다.
            string detail =
                $"Windows 유예 #{hold.State.EpisodeNumber} 해제 직후 읽기 전용 재확인(쓰기 0·판정 0) " +
                $"[줄 {_postHoldFactLines}/{MaxPostHoldFactLines}{(last ? " ★상한 — 이후 이 줄은 남기지 않는다" : "")}] " +
                $"사유={hold.State.LastReleaseReason}" +
                $" | DWM클로킹={cloaked}" +
                $" | 카메라배경 RGBA={background}(검정교정={_cameraBackgroundPremultiplyFixed})" +
                $" | Screen={Screen.width}x{Screen.height} {Screen.fullScreenMode}" +
                $" | SetResolution {_setResolutionCalls}/{MaxSetResolutionCalls}" +
                $"{(_setResolutionCalls >= MaxSetResolutionCalls ? "★상한 도달(이 세션에서는 창을 다시 맞출 수 없다 — 재시작 필요)" : "")}" +
                $", 창리사이즈 {_windowResizeCalls}/{OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls}" +
                $", 적합확정={_fullScreenBoundsApplied}(시도 {_fullScreenApplyAttempts}/{MaxFullScreenApplyAttempts})" +
                $", 진동래치={_boundsOscillation.IsOscillating}" +
                $" | 창 OS실측 {osRect}" +
                $" | 라이브러리 {libraryRect}" +
                $" | GWL_EXSTYLE={exStyle}, GWL_STYLE={style}, 핸들={(handleIsNative ? "네이티브" : ".NET폴백")}" +
                $" | 직전 선택 모니터[{_lastMonitorIndex}/{monitorCount}] {targetRect}";

            if (FreezeForensics.IsActive)
            {
                FreezeForensics.Record(FreezeForensicsEvent.RenderHoldReleaseProbe, monitorCount, detail);
            }
            Debug.Log("[화면변경유예/사후확인] " + detail + ".");
        }

        private void OnLibraryMonitorChanged()
            => DisplayChangeHold.OnLibraryMonitorChanged(Time.unscaledTimeAsDouble, Time.frameCount);

        /// <summary>전체화면 재적합이 아직 끝나지 않았는가(유예의 재적합 단계가 끝나는 조건).</summary>
        private bool IsFullScreenFitPending()
            => !_fullScreenBoundsApplied && _fullScreenApplyAttempts < MaxFullScreenApplyAttempts
               && !_boundsOscillation.IsOscillating;

        /// <summary>
        /// OS 모니터 목록을 열거해 <c>OverlayMonitorDirectory</c>에 게시한다. 평소에는 벽시계 문(1초) 뒤에서 불리고,
        /// 화면 변경 유예 해제 <b>직전</b>에는 문을 우회해 한 번 불린다(재개 순서 계약 — DisplayChangeHoldStatus 문서).
        /// </summary>
        private void RefreshOsMonitorList()
        {
            if (OsMonitorEnumerator == null) return;
            try
            {
                if (OsMonitorEnumerator(_osMonitors))
                {
                    // 설정 UI가 보는 목록과 <b>같은 관측</b>을 쓴다. 여기서 갈라지면
                    // "설정에는 2번인데 창은 1번에 뜬다"가 된다.
                    OverlayMonitorDirectory.Publish(_osMonitors);
                }
                else
                {
                    _osMonitors.Clear();
                }
            }
            catch (System.Exception e)
            {
                _osMonitors.Clear();
                Debug.LogWarning($"[표시모니터] OS 모니터 열거 실패 — {e.GetType().Name}: {e.Message}. " +
                    "이번 판정은 폴백(창이 놓인 자리)으로 갑니다.");
            }
        }
        private OverlayMonitorChoiceSource _lastChoiceSource = (OverlayMonitorChoiceSource)(-1);
        /// <summary>직전에 찍은 폴백 사유(null = 사유 없음). <see cref="LogChoiceOnce"/>의 중복 억제 입력.</summary>
        private string _lastChoiceExtra;

        /// <summary>
        /// 사용자 확정 규칙(기본 주 모니터 / 사용자가 고르면 그 화면)으로 목표 사각형을 정한다.
        /// 정하지 못하면 false — 그때만 호출자가 예전 동작으로 폴백한다.
        /// </summary>
        private bool TryResolveChosenMonitorRect(int libraryCount, out Rect monitor)
        {
            monitor = default;

            if (_osMonitorRefreshGate.TryConsume(Time.unscaledTime, OsMonitorRefreshIntervalSeconds))
            {
                RefreshOsMonitorList();
            }

            _libraryRects.Clear();
            for (int i = 0; i < libraryCount; i++) _libraryRects.Add(UniWindowController.GetMonitorRect(i));

            OverlayMonitorChoice choice = OverlayMonitorDirectory.Resolve();
            int libIndex = choice.HasIndex
                ? OverlayMonitorChoicePolicy.LibraryIndexForOsMonitor(_libraryRects, _osMonitors, choice.Index)
                : -1;

            if (libIndex < 0)
            {
                // ★ OS 목록을 못 얻었거나 좌표 매칭에 실패했다. 그래도 <b>기본값은 지킬 수 있다</b>:
                //   "가장 왼쪽"은 두 네이티브 라이브러리의 <b>정렬 규칙 그 자체</b>라
                //   (Phase 0에서 원문으로 확정: Windows는 left 오름차순, macOS는 minX 오름차순)
                //   <b>라이브러리 0번이 곧 가장 왼쪽</b>이다. 창 중심 폴백보다 이쪽이 사용자 확정
                //   규칙에 훨씬 가깝다. 사용자가 고른 화면은 여기서 지킬 수 없으므로 그 사실을 남긴다.
                if (_libraryRects.Count > 0 && _libraryRects[0].width > 0f && _libraryRects[0].height > 0f)
                {
                    monitor = _libraryRects[0];
                    _lastMonitorIndex = 0;
                    LogChoiceOnce(choice,
                        "★ OS 모니터 목록을 쓰지 못해 <b>라이브러리 정렬(가장 왼쪽 = 0번)</b>로 기본값을 지킵니다 — " +
                        "사용자 선택은 이번 판정에 반영되지 않았습니다.");
                    return true;
                }
                LogChoiceOnce(choice, "★ 라이브러리 모니터 목록도 비어 있어 폴백합니다.");
                return false;
            }

            monitor = _libraryRects[libIndex];
            // ★ 2026-09-14 (debugger D2) — 크기 0 검사를 재무장 <b>앞</b>으로 옮겼다(macOS판은 원래 이 순서였다).
            //   예전에는 재무장이 먼저라, 사라지는 중인 모니터가 크기 0으로 조회되는 순간에도
            //   `_lastAppliedTargetIndex`가 그 인덱스로 바뀌고 진동 이력이 버려진 뒤 false로 폴백했다 —
            //   유효하지 않은 목표로 재적합 루프를 무장시키는 순서였다.
            if (monitor.width <= 0f || monitor.height <= 0f) return false;

            // ★★ 2026-09-02 — <b>선택이 바뀌면 그 자리에서 창이 따라가야 한다</b>(ux-designer §49의
            //   성립 조건). 그냥 두면 창을 옮기는 유일한 경로(TickFullScreenBounds)가
            //   `_fullScreenBoundsApplied` 래치에 막혀 <b>다음 재시작에만</b> 반영된다.
            //   토폴로지 감시가 목표 사각형 변화를 결국 잡아 주긴 하지만 디바운스까지 ~1초가 걸리고,
            //   무엇보다 <b>진동 가드가 사용자의 왕복을 A↔B 진동으로 오인</b>해 4회 만에 영구 래치된다.
            //   그래서 (1) 목표가 바뀐 순간 관측 이력을 버리고(래치는 건드리지 않는다)
            //         (2) 재적합 루프를 즉시 재무장한다.
            if (_lastAppliedTargetIndex != libIndex)
            {
                _lastAppliedTargetIndex = libIndex;
                _boundsOscillation.ForgetSamplesForNewTarget();
                ReArmFullScreenFitForNewTarget();
            }

            _lastMonitorIndex = libIndex;
            LogChoiceOnce(choice);
            return true;
        }

        private readonly List<Rect> _libraryRects = new List<Rect>(8);

        /// <summary>직전에 <b>목표로 삼은</b> 라이브러리 모니터 인덱스(-1 = 아직 없음).
        /// 사용자가 표시 모니터를 바꾼 순간을 잡는 유일한 신호다.</summary>
        private int _lastAppliedTargetIndex = -1;

        /// <summary>
        /// 표시 모니터 선택이 바뀌어 재적합 루프를 다시 무장한다.
        ///
        /// <para><b>되돌리는 것</b>: <c>_fullScreenBoundsApplied</c> / <c>_fullScreenApplyAttempts</c> / 타이머.
        /// <b>절대 되돌리지 않는 것</b>: <c>_setResolutionCalls</c> · <c>_windowResizeCalls</c>
        /// (프로세스 수명 상한) · 진동 래치. 그 둘을 풀면 상한이 사실상 사라진다.</para>
        ///
        /// <para>★ <b>정직한 한계</b>: 그 수명 상한(각 4회) 때문에, 두 모니터의 <b>크기가 다르면</b>
        /// 한 세션에서 화면 전환이 <b>4회까지만</b> 온전히 반영된다(그 뒤로는 위치만 따라가고
        /// 크기/해상도는 남는다). 크기가 같으면 위치 대입만 일어나므로 <b>횟수 제한이 없다</b>.
        /// 이 상한은 스왑체인 재생성(수백 ms 정지)을 막는 장치라 여기서 풀지 않는다.</para>
        /// </summary>
        private void ReArmFullScreenFitForNewTarget()
        {
            if (_boundsOscillation.IsOscillating) return;
            _fullScreenBoundsApplied = false;
            _fullScreenApplyAttempts = 0;
            _fullScreenTimer = ReapplyIntervalSeconds;   // 다음 틱에서 곧바로 1회.
            _topologyBaselineSynced = false;
        }


        /// <summary>선택 근거가 <b>바뀔 때만</b> 남긴다(24시간 상주 앱 — 매 폴링 로그 금지).
        /// 실패 근거(주 모니터 판정 실패 / 사용자 선택 미발견)는 반드시 여기 찍힌다 —
        /// 조용히 폴백하면 사용자가 "설정이 안 먹는다"고 신고한다(리더 지시 3항).</summary>
        private void LogChoiceOnce(OverlayMonitorChoice choice, string extra = null)
        {
            // ★ 2026-09-14 (debugger D1 후속) — 사유(extra)가 붙으면 호출마다(0.25초) 다시 찍던 우회를 닫았다.
            //   판정은 플랫폼 중립 OverlayMonitorChoicePolicy.ShouldLogChoiceChange 한 곳(macOS판과 같은 함수).
            if (!OverlayMonitorChoicePolicy.ShouldLogChoiceChange(_lastChoiceSource, _lastChoiceExtra, choice.Source, extra)) return;
            _lastChoiceSource = choice.Source;
            _lastChoiceExtra = extra;
            Debug.Log("[표시모니터] " +
                OverlayMonitorChoicePolicy.Describe(choice, Core.AppSettingsModel.PreferredOverlayMonitorKey) +
                (extra != null ? " / " + extra : "") +
                $" (인식 {OverlayMonitorDirectory.MonitorCount}대, 멀티={OverlayMonitorDirectory.IsMultiMonitor})");
        }

        /// <summary>
        /// 네이티브가 <b>우리 몰래 창을 옮긴 뒤</b> 전체화면 적합 루프를 다시 무장한다
        /// (지금 유일한 호출자는 위 재적용 루프의 <c>SetBorderless</c> 실행 지점).
        ///
        /// <para><b>되돌리는 것과 되돌리지 않는 것을 분명히 한다</b> — 여기서 상한까지 풀면
        /// <c>_setResolutionCalls</c> 상한이 사실상 사라진다는 것이 <see cref="TickDisplayTopology"/>가
        /// 이미 적어 둔 경고다. 그래서:</para>
        /// <list type="bullet">
        ///   <item>되돌린다: <c>_fullScreenBoundsApplied</c>, <c>_fullScreenApplyAttempts</c>, 타이머.</item>
        ///   <item><b>절대 되돌리지 않는다</b>: <c>_setResolutionCalls</c>, <c>_windowResizeCalls</c>
        ///         (프로세스 수명 상한), 진동 래치.</item>
        /// </list>
        /// <para>진동으로 이미 확정된 뒤에는 무장 자체를 하지 않는다(같은 이유).</para>
        /// </summary>
        private void ReArmFullScreenFitAfterNativeWindowMove()
        {
            if (_boundsOscillation.IsOscillating) return;
            if (!_fullScreenBoundsApplied && _fullScreenApplyAttempts < MaxFullScreenApplyAttempts) return;

            _fullScreenBoundsApplied = false;
            _fullScreenApplyAttempts = 0;
            _fullScreenTimer = ReapplyIntervalSeconds;   // 다음 TickFullScreenBounds에서 곧바로 1회.
            _topologyBaselineSynced = false;             // 우리가 만든 변화를 새 사건으로 오인하지 않도록.
        }

        /// <summary>
        /// OS 모니터 전수 열거(조회 전용). <see cref="Win32WindowService.CreateOverlayWindow"/>가
        /// 주입한다 — <see cref="OverlayRectReporter"/>와 같은 배선 형태이며, 이 클래스는 Win32를
        /// 직접 부르지 않는다(P/Invoke는 <c>Win32WindowService</c>의 리전 밖으로 나가지 않는다).
        /// </summary>
        internal OsMonitorEnumerator OsMonitorEnumerator;

        /// <summary>
        /// Phase 0 계측 — 기동 시 <b>한 번만</b> 모니터 지형 한 줄을 낸다(동작 변경 0).
        /// 무엇을 확정하려는 줄인지는 <see cref="MonitorTopologyReport"/> 클래스 문서에 있다.
        /// macOS판 Enforcer도 같은 자리에서 같은 함수를 부른다(규약 인자만 다르다).
        /// </summary>
        private void EmitMonitorTopologyOnce()
        {
            Rect overlayLibraryRect = new Rect(_controller.windowPosition, _controller.windowSize);
            // ★ "아직 보고 전"과 "정말 (0,0)"을 구분할 방법이 없어서 추정하지 않는다.
            //   ScreenCoordinateConverter에는 '한 번이라도 보고됐는가' 플래그가 없고, 이 라운드에
            //   공유 클래스에 상태를 새로 넣지 않는다(다른 라운드가 같은 파일을 만지고 있다).
            //   그래서 값을 그대로 찍고 <b>OS 실측이라고 부르지 않는다</b>(계측 줄의 라벨 참고).
            Rect overlayOsRect = new Rect(ScreenCoordinateConverter.OverlayOriginOsScreen,
                new Vector2(Screen.width, Screen.height));

            MonitorTopologyReport.EmitOnce(
                "Windows",
                LibraryMonitorYConvention.FlippedFromPrimaryBottom,
                UniWindowController.GetMonitorCount(),
                UniWindowController.GetMonitorRect,
                overlayLibraryRect,
                OsMonitorEnumerator,
                overlayOsRect,
                true);
        }

        /// <summary>
        /// 투명이 실제로 확인된 뒤에만 카메라 배경 RGB를 검정으로 낮춘다(알파는 보존).
        /// macOS판과 같은 이유이며 Windows에서 오히려 더 중요하다: 투명 창 합성이 알파 채널을
        /// 프리멀티플라이드로 다루므로 배경 RGB가 밝으면 캐릭터 가장자리에 밝은 프린지가 남는다.
        /// 투명화가 실패한 상황에서는 손대지 않아 "밝은 배경 안의 캐릭터"(최소한 보이는 상태)가 된다.
        /// </summary>
        /// <summary>
        /// ★ 2026-09-28 — <b>부착이 끝내 실패했을 때의 탈출구</b>. 판정은 플랫폼 중립
        /// <see cref="StartupWindowModePolicy.ShouldReleaseFullscreenAfterAttachFailure"/> 한 곳에 있고,
        /// 이 메서드는 <b>사실 조회와 적용</b>만 한다(CLAUDE.md "정책은 중립 위치, 플랫폼은 사실 조회").
        ///
        /// <para><b>왜 필요한가</b>: 이 플랫폼은 기동 시 전체화면 <b>조기</b> 해제를 끈다(제목표시줄 노출
        /// 구간을 없애기 위해 — <see cref="StartupWindowModePolicy"/> 문서). 정상 경로에서는 부착 프레임의
        /// <c>TickFullScreenBounds()</c>가 창모드로 내리므로 아무 손실이 없다. 그런데 <b>부착이 영영
        /// 실패하면</b> 그 전환도 영영 오지 않아, 사용자에게는 <b>테두리 없는 전체화면 불투명 창</b>이 남는다
        /// (제목표시줄도 닫기 버튼도 없다). 트레이 아이콘은 이 상황에서도 살아 있지만
        /// <c>SystemTrayPresencePolicy.OptOutEnvironmentVariable</c>로 <b>끌 수 있으므로</b> 유일한 탈출구로
        /// 삼지 않는다. 그래서 제한 시간이 지나면 <b>그때</b> 창모드로 내려 제목표시줄을 되돌린다 —
        /// 조기 해제를 없앤 것이 아니라 <b>필요한 순간으로 미룬 것</b>이다.</para>
        ///
        /// <para><c>Screen.fullScreen = false</c>를 쓰는 이유: 패키지의 <c>forceWindowed</c>가 걸던
        /// <b>그 한 줄</b>과 같아 새 동작을 발명하지 않고, <c>Screen.SetResolution</c>의 <b>수명 상한</b>
        /// (<see cref="MaxSetResolutionCalls"/>)을 소모하지 않는다. 호출은 <c>_gaveUpLogged</c> 가드 안이라
        /// <b>프로세스당 1회</b>다(재적용 루프가 되지 않는다).</para>
        ///
        /// <para><b>정직한 한계</b>: 이 머신에 Windows 실기가 없어 <b>실행으로 확인하지 못했다</b>.
        /// 부착 실패 자체가 드문 경로라 실기에서도 자연 발생을 기다릴 수 없다 — 그래서 판정은 EditMode가
        /// 순수 함수로, 배선은 소스 감사가 잠근다.</para>
        /// </summary>
        private void ReleaseFullscreenAfterAttachFailureIfPolicyRequires()
        {
            if (!StartupWindowModePolicy.ShouldReleaseFullscreenAfterAttachFailure(OverlayHostPlatform.Windows)) return;

            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Debug.LogWarning("[WindowsOverlayStateEnforcer] 부착 실패 탈출구 — 이미 창모드(Windowed)라 " +
                    "창 모드를 건드리지 않았습니다(제목표시줄은 이미 있습니다).");
                return;
            }

            FullScreenMode before = Screen.fullScreenMode;
            Screen.fullScreen = false;
            Debug.LogWarning($"[WindowsOverlayStateEnforcer] 부착 실패 탈출구 — 창 모드를 {before} -> Windowed로 " +
                "요청했습니다(프레임 끝에 적용). 제목표시줄이 돌아오므로 창을 옮기고 닫을 수 있습니다. " +
                "이 호출은 프로세스당 1회이고 Screen.SetResolution 수명 상한을 소모하지 않습니다.");
        }

        // ============================================================================
        // ★ 2026-09-28 기동 표시 보류 — 「시작할때 전체 흰화면이 계속 켜져있다가 꺼짐」
        // ============================================================================
        // 판정·전이·상한은 전부 플랫폼 중립 StartupPresentationHold에 있고 이 파일은 <b>사실 조회와
        // 적용</b>만 한다(CLAUDE.md). macOS판(MacOverlayStateEnforcer)이 같은 자리에 같은 넷을 갖는다 —
        // 한쪽만 고치면 그 플랫폼만 흰 화면이 남는다.
        private StartupPresentationHold _startupHold;

        private StartupPresentationHold StartupHold => _startupHold ??= new StartupPresentationHold(
            StartupPresentationHoldPolicy.ReadDisabledFromEnvironment(),
            AttachTimeoutSeconds,
            ApplyStartupHoldBackgroundRgb);

        /// <summary>보류가 색을 쓸 카메라. 투명 교정(<see cref="ApplyTransparentSafeCameraBackground"/>)과
        /// <b>같은 규칙</b>으로 고른다 — 두 경로가 다른 카메라를 잡으면 한쪽이 다른 쪽을 덮는다.</summary>
        private Camera ResolveHoldCamera()
            => _controller != null && _controller.currentCamera != null ? _controller.currentCamera : Camera.main;

        /// <summary>보류의 <b>유일한 쓰기 지점</b>. 알파는 절대 건드리지 않는다 —
        /// 그 알파가 곧 창 투명도의 입력이고, 여기서 손대면 투명 합성 자체가 바뀐다.</summary>
        private void ApplyStartupHoldBackgroundRgb(float r, float g, float b)
        {
            Camera cam = ResolveHoldCamera();
            if (cam == null) return;
            Color before = cam.backgroundColor;
            cam.backgroundColor = new Color(r, g, b, before.a);
        }

        /// <summary>부착 전 첫 기회에 보류를 시작한다(한 번만 먹는다).</summary>
        private void BeginStartupPresentationHoldIfNeeded()
        {
            StartupPresentationHold hold = StartupHold;
            if (hold.IsDisabled || hold.Phase != StartupPresentationHoldPhase.Inactive) return;

            Camera cam = ResolveHoldCamera();
            if (cam == null) return;   // 카메라를 아직 못 찾았다 — 다음 프레임에 다시 시도한다.

            float keptAlpha = cam.backgroundColor.a;
            Color fallback = ResolveStartupFallbackBackground(cam);
            if (!hold.Begin(Time.unscaledTimeAsDouble, fallback.r, fallback.g, fallback.b)) return;

            Debug.Log("[WindowsOverlayStateEnforcer] 기동 표시 보류 시작 — 부착 전 카메라 배경 RGB를 " +
                $"스플래시 배경과 같은 어두운 값({StartupPresentationHoldPolicy.HoldRed:F3}," +
                $"{StartupPresentationHoldPolicy.HoldGreen:F3},{StartupPresentationHoldPolicy.HoldBlue:F3})으로 " +
                $"덮었습니다(알파 {keptAlpha:F2} 보존). 상한 {hold.BudgetSeconds:F0}초에 닿으면 " +
                $"근백색({hold.RestoreRed:F2},{hold.RestoreGreen:F2},{hold.RestoreBlue:F2})으로 반드시 " +
                $"되돌립니다. 끄려면 {StartupPresentationHoldPolicy.DisableEnvironmentVariable}=1.");
        }

        /// <summary>상한에서 되돌릴 색의 출처. <b>RGB만</b> 쓴다(알파는 카메라의 현재 값을 보존한다).</summary>
        private Color ResolveStartupFallbackBackground(Camera cam)
        {
            var config = ResolveConfig();
            // 이 값은 캐릭터 튜닝이 아니라 <b>오버레이 배경 폴백</b>이다 — 투명이 실패했을 때
            // "밝은 배경 안의 검정 캐릭터"(최소한 보이는 상태)를 만드는 색이고, 그것이 이 보류가
            // 상한에서 반드시 되돌려야 하는 이유다.
            if (config != null) return config.backgroundFallbackColor;
            return cam.backgroundColor;   // 설정을 아직 못 찾았다 — 씬이 구운 같은 값이 이미 여기 있다.
        }

        /// <summary>매 프레임. 상한에 닿으면 보류가 근백색을 되돌리고, 그때 한 번 경고를 남긴다.</summary>
        private void TickStartupPresentationHold()
        {
            StartupPresentationHold hold = StartupHold;
            if (!hold.Tick(Time.unscaledTimeAsDouble)) return;

            Debug.LogWarning($"[WindowsOverlayStateEnforcer] 기동 표시 보류 상한 도달({hold.BudgetSeconds:F0}초) — " +
                $"카메라 배경 RGB를 근백색({hold.RestoreRed:F2},{hold.RestoreGreen:F2},{hold.RestoreBlue:F2})으로 " +
                "되돌렸습니다(알파 보존). 이 시간 안에 부착/투명이 성립하지 않았다는 뜻이며, 어두운 배경을 " +
                "그대로 두면 잉크색이 검정인 사용자에게 아무것도 보이지 않습니다(검정-on-검정).");
        }

        /// <summary>투명 교정이 <b>실제로 걸린 뒤</b>에만 보류를 넘긴다(조기 반환 경로에서는 유지한다).</summary>
        private void NoteStartupPresentationHandoverIfCorrected()
        {
            if (!_cameraBackgroundPremultiplyFixed) return;
            if (!StartupHold.NoteTransparentCorrectionApplied()) return;

            Debug.Log("[WindowsOverlayStateEnforcer] 기동 표시 보류 종료 — 투명 교정이 걸려 카메라 배경이 " +
                "검정(알파 보존)으로 넘어갔습니다. 이후 이 보류는 색을 다시 쓰지 않습니다.");
        }

        /// <summary>부착 실패 보고와 같은 순간의 강제 복원(멱등).</summary>
        private void RestoreStartupPresentationHoldOnAttachFailure()
        {
            StartupPresentationHold hold = StartupHold;
            if (!hold.RestoreNow(StartupPresentationHoldRelease.AttachFailureRestored)) return;

            Debug.LogWarning("[WindowsOverlayStateEnforcer] 부착 실패 — 기동 표시 보류를 걷고 카메라 배경 RGB를 " +
                $"근백색({hold.RestoreRed:F2},{hold.RestoreGreen:F2},{hold.RestoreBlue:F2})으로 되돌렸습니다. " +
                "제목표시줄 복귀와 한 사건입니다(둘 다 '투명을 포기했다'는 같은 사실의 결과).");
        }

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
