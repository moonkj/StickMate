#if UNITY_STANDALONE_OSX
using UnityEngine;
using Kirurobo;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// UniWindowController의 "창 부착(Attach) 타이밍" 문제를 해결하는 런타임 전용 보조 컴포넌트
    /// (UniWindowController 도입 라운드, 2026-08-28 — 실측으로 발견한 사고 대응).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 실측으로 확인한 순서 문제
    /// ============================================================================
    /// UniWindowController는 자기 자신의 NSWindow를 Awake()가 아니라 첫 Update()에서 붙잡는다
    /// (UpdateTargetWindow() -> UniWinCore.AttachMyWindow()). 그런데 우리 배선 지점인
    /// StickmanAgent.Start()는 그보다 먼저 실행되므로, 그 시점에 건 설정 중 일부가 조용히 사라진다:
    ///   - SetTopmost(true)는 `_isTopmost = _uniWinCore.IsTopmost`로 되읽는데, IsTopmost는
    ///     `IsActive && _isTopmost`라서 아직 부착 전이면 **무조건 false**로 되돌아간다. 실측 로그:
    ///     "[MacWindowService] SetAlwaysOnTop(True) 적용 완료 — isTopmost=False" + 외부
    ///     CGWindowListCopyWindowInfo 조회에서 kCGWindowLayer=0(= 일반 레이어, 항상위 아님).
    ///   - DetectDesktopDpiScale()도 같은 이유로 clientSize=(0,0)을 읽어 배율 보정을 못 했다
    ///     (이쪽은 MacWindowService에서 CoreGraphics 디스플레이 모드 조회로 따로 해결했다).
    /// 투명(isTransparent)만은 예외적으로 살아남는다 — UpdateTargetWindow()가 부착 성공 직후
    /// `SetTransparent(_isTransparent)`로 직렬화된 값을 다시 적용해주기 때문이다.
    ///
    /// 그래서 이 컴포넌트는 "우리가 의도한 목표 상태"를 들고 있다가 창이 실제로 부착된 것을 확인한 뒤
    /// 한 번 더 적용하고, 그 결과를 되읽어 Player.log에 검증 로그로 남긴다. 부착 판정은
    /// `windowSize`(부착 전에는 (0,0))로 한다.
    ///
    /// 생성 주체는 MacWindowService.CreateOverlayWindow()이며, 그 서비스 자체가 실제 Standalone macOS
    /// Player에서만 인스턴스화되므로(StickmanAgent.CreatePlatformService()의
    /// `UNITY_STANDALONE_OSX && !UNITY_EDITOR` 분기) 에디터/헤드리스에는 애초에 존재하지 않는다.
    /// 씬 에셋에도 저장되지 않는다(런타임 new GameObject).
    /// </summary>
    internal sealed class MacOverlayStateEnforcer : MonoBehaviour
    {
        private const string HostObjectName = "StickMate_MacOverlayStateEnforcer";

        /// <summary>부착 확인 후 목표 상태를 재적용할 최대 횟수. 창 스타일이 부착 직후 한두 프레임에
        /// 걸쳐 확정되는 경우가 있어 한 번만 적용하고 끝내지 않는다. 무한 반복은 하지 않는다 —
        /// 사용자가 창을 직접 조작했을 때 우리가 그것을 계속 되돌려버리는 것이 더 나쁘기 때문이다.</summary>
        private const int ReapplyAttempts = 5;

        /// <summary>재적용 간격(초).</summary>
        private const float ReapplyIntervalSeconds = 0.5f;

        private UniWindowController _controller;
        private int _appliedCount;
        private float _timer;
        private bool _attachDetected;
        private bool _gaveUpLogged;
        private bool _cameraBackgroundPremultiplyFixed;

        /// <summary>렌더 품질 실측 진단(<see cref="LogRenderQualityDiagnostics"/>)을 한 번만 찍기 위한 래치.</summary>
        private bool _renderQualityDiagnosticsLogged;

        /// <summary>부착 대기 제한 시간(초). 이 안에 창을 못 붙잡으면 정직하게 실패 로그를 남긴다.</summary>
        private const float AttachTimeoutSeconds = 15f;
        private float _elapsed;

        // ============================================================================
        // 히트테스트 실측 감시(드래그&던지기 실배선 라운드, 2026-08-28)
        // ============================================================================
        // hitTestType을 Opacity -> Raycast로 바꿨으므로, 그 판정이 실제로 동작하는지를 **로그로**
        // 확인할 수 있어야 한다. 실제 마우스 조작은 자동 검증이 불가능하지만, 라이브러리가 쓰는 것과
        // 정확히 같은 질의(Physics2D.GetRayIntersection + 카메라 ScreenPointToRay)를 우리가 두 지점에
        // 직접 쏴 보면 "판정 인프라가 준비됐는가"까지는 결정적으로 확인된다:
        //   (A) 캐릭터의 현재 화면 좌표 -> 반드시 캐릭터 콜라이더가 잡혀야 한다(클릭 가능).
        //   (B) 캐릭터에서 멀리 떨어진 빈 지점 -> 아무것도 잡히지 않아야 한다(클릭 관통 유지,
        //       비침해 원칙 2). 여기서 무언가 잡히면 그 영역의 클릭을 우리가 훔치고 있다는 뜻이다.
        // 동시에 클릭관통 안전장치(시작 5초 지연, Escape 긴급 해제)가 라이브러리 자동 제어에 덮이지
        // 않는지도 같은 줄에서 되읽어 남긴다.
        private const float ProbeIntervalSeconds = 1f;
        private const float ProbeDurationSeconds = 25f;
        private float _probeTimer;
        private Core.StickmanAgent _agent;

        // ============================================================================
        // 헤드라인 기능("윈도우 창 = 지형") 상시 진단 리포트 — 2026-08-28
        // ============================================================================
        // 리더/사용자가 화면을 볼 수 없는 환경(Screen Recording 권한 없음)에서 "지금 캐릭터가 진짜
        // Finder 창 위에 서 있는가"를 판별할 수 있는 유일한 수단이 로그다. 그래서 히트테스트 감시(25초
        // 한정)와 달리 이 리포트는 상시로 돌되 주기를 넉넉히 잡아 로그량을 통제한다.
        //
        // ★ 2026-08-28 정리(기능 안정화 후): 2.5초 주기는 실측 결과 Player.log의 84%(443줄 중 372줄)를
        // [발판리포트]/[창진단] 두 줄이 차지하게 만들어, 정작 중요한 경고/예외가 묻혔다. 24시간 상주
        // 앱에서는 그 자체로 결함이다. 그렇다고 지우면 다음 회귀 때 다시 눈이 먼 채로 조사해야 하므로
        // 삭제하지 않고 StickConfig.verboseDiagnosticsLogging 스위치로 옮긴다:
        //   - 기본(false): 60초 심장박동. "지금 무엇을 딛고 있는가"는 재빌드 없이 언제든 확인 가능하되
        //     하루 1440줄 수준으로 로그량이 24배 줄어든다.
        //   - 켜면(true):  예전과 동일한 2.5초/7.5초 촘촘한 리포트 + 히트테스트 프로브까지 전부 복귀.
        // 이상 신호([화면클램프]/[캐릭터구조]/[발판변경])는 이 스위치와 무관하게 항상 남는다 —
        // 그것들은 "정상 상태 보고"가 아니라 "무언가 잘못됐다"는 신호라 조용해질 이유가 없다.
        private const float FootholdReportIntervalSecondsVerbose = 2.5f;
        private const float FootholdReportIntervalSecondsQuiet = 60f;

        // 한 줄에 나열할 실제 창의 최대 개수(그 이상은 "...외 N개"로 접는다 — 창을 20개씩 띄운
        // 환경에서 로그 한 줄이 수천 자가 되는 것을 막는다).
        private const int MaxFootholdsPerReport = 8;
        private float _footholdReportTimer;
        private readonly System.Text.StringBuilder _reportBuilder = new System.Text.StringBuilder(512);
        /// <summary>합성 발판(Dock/안전망 두 조각)의 X 구간을 모으는 재사용 버퍼 — 위 겹침 확인용(2026-08-29).</summary>
        private readonly System.Text.StringBuilder _syntheticBuilder = new System.Text.StringBuilder(512);

        /// <summary>창 전체 덤프([창진단]) 주기(초) — 발판 리포트보다 길어서 따로 둔다. 이 덤프는 한 줄이
        /// 수백~수천 자라 특히 무거우므로, 위 정리 이후 verboseDiagnosticsLogging이 켜졌을 때만 남긴다.</summary>
        private const float WindowDumpIntervalSeconds = 7.5f;
        private float _windowDumpTimer;

        /// <summary>
        /// 상세 진단 로그 스위치(StickConfig.verboseDiagnosticsLogging)의 현재 값. 에이전트/블랙보드가
        /// 아직 없으면(초기 몇 프레임) 조용한 기본값(false)으로 취급한다 — 진단 로그 때문에 시작 경로에
        /// 예외를 만들지 않는다는 기존 태도와 같다.
        /// </summary>
        private bool VerboseDiagnostics
        {
            get
            {
                var config = ResolveConfig();
                return config != null && config.verboseDiagnosticsLogging;
            }
        }

        /// <summary>
        /// 씬에 배선된 StickConfig(없으면 null). ScreenCoordinateConverter의 DPI 배율 해석에 넘길
        /// "수동 오버라이드" 출처이며, null이어도 컨버터가 자동 배율로 폴백하므로 안전하다
        /// (VerboseDiagnostics와 같은 조회를 공유한다 — 에이전트가 아직 없는 초기 몇 프레임에는 null).
        /// </summary>
        private Core.StickConfig ResolveConfig()
        {
            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
            var blackboard = _agent != null ? _agent.Blackboard : null;
            return blackboard != null ? blackboard.Config : null;
        }

        // ============================================================================
        // 오버레이 창 전체화면 확장 — 헤드라인 기능의 선행 조건
        // ============================================================================
        // 실측(직전 라운드 Player.log): windowSize=(1512,846), Quartz 원점=(0,61) — 즉 창이 화면
        // (1512x982pt)의 세로 가운데 846pt만 덮고 위 61pt(메뉴바)와 아래 75pt(Dock)가 비어 있었다.
        // ScreenCoordinateConverter는 "OS 좌표 -> 창 클라이언트 좌표"를 OverlayOriginOsScreen으로
        // 보정하므로 좌표 자체는 맞지만, **창 밖 영역에 있는 타 앱 창 위로는 캐릭터를 그릴 수가 없어**
        // 그 영역의 창은 발판으로 쓸 수 없다. 그래서 창을 모니터 전체로 넓힌다.
        //
        // 좌표 규약(실측으로 확정): UniWindowController.windowPosition은 Quartz(좌상단 원점)가 아니라
        // **Cocoa 규약(좌하단 원점, 창의 좌하단 모서리)** 이다. 직전 로그가 그 증거다 —
        // Quartz 원점 y=61 + 창높이 846 = 907이고 화면 높이 982에서 빼면 75, 그리고 라이브러리가
        // 보고한 windowPosition은 정확히 (0, 75)였다. 그래서 이 코드는 규약을 추측하지 않고
        // **같은 라이브러리의 GetMonitorRect()가 돌려주는 모니터 사각형을 그대로** 창 위치/크기로
        // 대입한다(두 값이 같은 좌표계이므로 규약과 무관하게 정확히 겹친다).
        private bool _fullScreenBoundsApplied;
        private int _fullScreenApplyAttempts;
        private const int MaxFullScreenApplyAttempts = 6;

        /// <summary>
        /// 창 <b>기하</b>(크기/위치) 판정 불감대. 단위는 <b>OS 포인트</b> — 이 파일의 monitor/windowSize/
        /// windowPosition이 전부 포인트이기 때문이다. 해상도 판정은 단위가 달라서 같은 값을 쓰지 않는다
        /// (<see cref="OverlayBoundsFitPolicy.ResolutionEpsilonPixels"/>가 배율로 유도한다).
        ///
        /// <para>값과 근거는 플랫폼 중립 순수 규칙
        /// <see cref="OverlayBoundsFitPolicy.DefaultEpsilonPixels"/> 한 곳에 있다 — Windows판 Enforcer가
        /// 같은 상수를 쓰므로 값이 두 벌로 갈라지면 한쪽만 고쳐지는 이 저장소의 단골 실패가 된다.</para>
        /// </summary>
        private const float BoundsEpsilonPoints = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

        /// <summary>
        /// 이 프로세스에서 <c>Screen.SetResolution</c>을 부른 누적 횟수.
        /// <b><see cref="TickDisplayTopology"/>의 재무장에서 절대 0으로 되돌리지 않는다</b> —
        /// 그것이 "프로세스 수명 상한"이라는 말의 전부다. 되돌리면 디스플레이 통지가 진동할 때
        /// 상한이 사실상 사라진다.
        /// </summary>
        private int _setResolutionCalls;

        /// <summary>창 크기를 실제로 재대입한 누적 횟수(로그 전용). 백버퍼 재할당 횟수와 1:1이다.</summary>
        private int _windowResizeCalls;

        // 실행 중 디스플레이 구성 변경 추적(2026-08-31). 판단 로직은 플랫폼 공용
        // Platform/DisplayTopologyWatcher.cs 한 곳에 있고 여기서는 관측만 한다 — Windows판도 같은 클래스를
        // 같은 방식으로 쓴다(오늘 VisibleTopEdgeSolver에서 한쪽만 고쳐 재발한 사례의 재발 방지).
        private readonly DisplayTopologyWatcher _topologyWatcher = new DisplayTopologyWatcher();
        /// <summary>전체화면 적합 에피소드가 끝난 뒤 기준값을 다시 잡았는가. 재무장할 때 false로 돌린다.</summary>
        private bool _topologyBaselineSynced;
        /// <summary>토폴로지 관측 주기(초). 디바운스 창(0.75초)보다 충분히 짧아 판정 해상도는 잃지 않으면서
        /// 디스플레이 열거/CGDisplayBounds 호출을 초당 60회에서 10회로 줄인다.</summary>
        private const float TopologySampleIntervalSeconds = 0.1f;
        private float _topologySampleTimer;

        /// <summary>플랫폼 계층이 배선하는 "지금 즉시 오버레이 창 OS 사각형을 보고하라" 훅
        /// (MacWindowService.ReportOverlayRectNow). 재적합 직후 같은 프레임에 좌표계를 갱신해
        /// 발판 폴링을 기다리는 동안 캐릭터가 옛 좌표계로 튀는 구간을 없앤다.</summary>
        internal System.Action OverlayRectReporter;

        // 목표 상태 — MacWindowService가 자기 API 호출 때마다 갱신한다.
        internal bool DesiredTransparent = true;
        internal bool DesiredTopmost;
        internal bool DesiredClickThrough;
        internal bool DesiredHitTest;

        internal static MacOverlayStateEnforcer EnsureExists(UniWindowController controller)
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<MacOverlayStateEnforcer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing._controller = controller;
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var enforcer = go.AddComponent<MacOverlayStateEnforcer>();
            enforcer._controller = controller;
            return enforcer;
        }

        /// <summary>MacWindowService가 목표 상태를 바꿀 때마다 호출 — 재적용 카운터를 리셋해 새 목표가
        /// 확실히 반영되게 한다.</summary>
        internal void MarkDirty()
        {
            _appliedCount = 0;
            _timer = ReapplyIntervalSeconds; // 다음 Update에서 곧바로 한 번 적용.
        }

        private void Update()
        {
            // ★ 창 부착 여부와 무관하게 가장 먼저 건다(2026-08-31 성능 라운드). 시작 직후 몇 초는
            //   UniWindowController가 NSWindow를 붙잡기를 기다리는 구간이라 오히려 프레임을 가장
            //   헛되이 태우는 구간이다. 설정이 아직 없으면 내부적으로 다음 프레임에 다시 시도한다.
            //   (Platform/FramePacing.cs가 플랫폼 공통 진입점이다 — Windows 쪽 Enforcer도 같은 자리에서 같은 함수를 부른다.)
            if (!FramePacing.IsApplied) FramePacing.ApplyOnce(ResolveConfig());
            // 캐릭터가 제자리에 서 있는지를 넘긴다 — 적응형 프레임 등급의 입력이다(판정 자체는
            // 양 플랫폼 공용 FramePacing.ResolveCharacterIdle 한 곳에만 있다).
            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
            FramePacing.Tick(FramePacing.ResolveCharacterIdle(_agent));

            if (_controller == null)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            // 부착 판정: 부착 전에는 네이티브가 크기를 (0,0)으로 보고한다.
            Vector2 windowSize = _controller.windowSize;
            bool attached = windowSize.x > 0f && windowSize.y > 0f;

            if (!attached)
            {
                if (!_gaveUpLogged && _elapsed > AttachTimeoutSeconds)
                {
                    _gaveUpLogged = true;
                    Debug.LogWarning($"[MacOverlayStateEnforcer] {AttachTimeoutSeconds}초가 지나도 " +
                        "UniWindowController가 자기 NSWindow를 붙잡지 못했습니다(windowSize=(0,0)). " +
                        "투명/항상위/클릭관통이 전부 적용되지 않은 상태입니다 — 정직한 실패 보고용 로그.");
                }
                return;
            }

            if (!_attachDetected)
            {
                _attachDetected = true;
                ApplyTransparentSafeCameraBackground();
                // 창이 실제로 존재하는 이 시점에 앱 등급을 accessory로 내린다(원인 A, R1의 (1)단계).
                // 근거/트레이드오프는 MacSpaceBehaviorNative의 클래스 문서 참고.
                MacSpaceBehaviorNative.ApplyAccessoryActivationPolicyOnce();
                Debug.Log($"[MacOverlayStateEnforcer] 창 부착 감지 — windowSize={windowSize}, " +
                    $"clientSize={_controller.clientSize}, windowPosition={_controller.windowPosition}, " +
                    $"경과 {_elapsed:F2}초. 이제 목표 상태를 재적용합니다. " +
                    $"진단로그={(VerboseDiagnostics ? $"상세(발판리포트 {FootholdReportIntervalSecondsVerbose}초 + 창진단 {WindowDumpIntervalSeconds}초 + 히트테스트 프로브)" : $"조용함(발판리포트 {FootholdReportIntervalSecondsQuiet}초 심장박동만) — 더 보려면 DefaultStickConfig.asset의 verboseDiagnosticsLogging 체크")}.");
                _timer = ReapplyIntervalSeconds;
            }

            TickHitTestProbe();
            // 순서 중요: 재무장을 먼저 판정해야 같은 프레임의 TickFullScreenBounds()가 곧바로 다시 돈다.
            TickDisplayTopology();
            TickFullScreenBounds();
            TickFootholdReport();
            TickAllSpacesBehavior();

            if (_appliedCount >= ReapplyAttempts)
            {
                return;
            }

            _timer += Time.unscaledDeltaTime;
            if (_timer < ReapplyIntervalSeconds)
            {
                return;
            }
            _timer = 0f;
            _appliedCount++;

            // 순서 주의: 히트테스트 자동 제어를 먼저 목표값으로 맞춘 뒤 나머지를 적용한다.
            _controller.isHitTestEnabled = DesiredHitTest;
            _controller.isTransparent = DesiredTransparent;
            _controller.isTopmost = DesiredTopmost;
            _controller.isClickThrough = DesiredClickThrough;

            // ★ 순서 의존: LibUniWinC의 setTopmost()가 collectionBehavior를 통째로 덮어쓰므로
            //   (.fullScreenAuxiliary만 남고 .canJoinAllSpaces가 날아간다) isTopmost 대입 **직후**에
            //   반드시 다시 걸어야 한다. 아래 TickAllSpacesBehavior()의 감시만으로도 결국 복구되지만,
            //   그 사이 최대 2초 동안 타 앱 전체화면에서 캐릭터가 사라지는 창이 생긴다.
            MacSpaceBehaviorNative.EnsureAllSpacesBehavior(out _);

            Debug.Log($"[MacOverlayStateEnforcer] 재적용 {_appliedCount}/{ReapplyAttempts} — " +
                $"목표(transparent={DesiredTransparent}, topmost={DesiredTopmost}, " +
                $"clickThrough={DesiredClickThrough}, hitTest={DesiredHitTest}) / " +
                $"되읽음(isTransparent={_controller.isTransparent}, isTopmost={_controller.isTopmost}, " +
                $"isClickThrough={_controller.isClickThrough}, isHitTestEnabled={_controller.isHitTestEnabled}) / " +
                $"windowSize={_controller.windowSize}, clientSize={_controller.clientSize}, " +
                $"windowPosition={_controller.windowPosition}, cameraBg={CameraBackgroundDescription()}.");
        }

        /// <summary>
        /// 오버레이 창을 현재 모니터 전체(메뉴바/Dock 영역 포함)로 확장한다 — 위 "오버레이 창 전체화면
        /// 확장" 주석의 근거 참고. 창이 부착된 뒤에만 의미가 있으므로 Update()의 attached 분기에서만
        /// 호출된다.
        ///
        /// 세 단계를 모두 밟는 이유(하나라도 빠지면 실측에서 실패한다):
        ///   (a) Screen.SetResolution — Unity 자신의 백버퍼/보고 해상도. 이걸 안 바꾸면 NSWindow만 커지고
        ///       Screen.width/height는 옛 값 그대로라 ScreenCoordinateConverter의 y 반전이 통째로 틀어진다.
        ///   (b) isFreePositioningEnabled=true — macOS는 기본적으로 창을 "보이는 영역(visibleFrame,
        ///       메뉴바/Dock 제외)" 안으로 밀어 넣는다. 이 플래그가 그 제약을 푼다(라이브러리의
        ///       EnableFreePositioning).
        ///   (c) windowSize -> windowPosition 순서로 대입. 크기를 먼저 정해야 위치 대입이 최종 좌표가 된다.
        ///
        /// 재시도하는 이유: (a)의 해상도 변경은 프레임 끝에 반영되고, 창 스타일 확정도 한두 프레임
        /// 걸린다. 최대 MaxFullScreenApplyAttempts번만 시도하고 성공(오차 1pt 이내)하면 즉시 멈춘다 —
        /// 사용자가 창을 직접 만졌을 때 영원히 되돌리지 않기 위한 기존 컨벤션(ReapplyAttempts)과 같은 태도다.
        /// </summary>
        /// <summary>
        /// collectionBehavior(.canJoinAllSpaces | .fullScreenAuxiliary) 유지 감시 — 원인 A의 마지막 안전망.
        ///
        /// 재적용 루프(ReapplyAttempts회)는 몇 초 뒤 끝나지만, UniWindowController는 그 뒤에도 자체 사정으로
        /// SetTopmost를 다시 호출할 수 있다(예: 창 재부착, isTopmost 프로퍼티 재대입). 그때마다 우리 플래그가
        /// 날아가면 사용자는 "가끔 전체화면에서 캐릭터가 사라진다"는 재현 어려운 버그를 겪는다. 그래서 낮은
        /// 빈도로 계속 확인하되, <b>플래그가 이미 맞으면 쓰기도 로그도 하지 않는다</b>(24시간 상주 앱).
        /// </summary>
        private void TickAllSpacesBehavior()
        {
            _timerAllSpaces += Time.unscaledDeltaTime;
            if (_timerAllSpaces < AllSpacesWatchIntervalSeconds) return;
            _timerAllSpaces = 0f;
            MacSpaceBehaviorNative.EnsureAllSpacesBehavior(out _);
        }

        /// <summary>위 감시 주기(초). 사람이 Space를 전환하는 속도보다 충분히 짧고, ObjC 호출 4~5회짜리
        /// 비용이라 상주 부담이 사실상 없다.</summary>
        private const float AllSpacesWatchIntervalSeconds = 2f;
        private float _timerAllSpaces;

        private void TickFullScreenBounds()
        {
            if (_fullScreenBoundsApplied || _fullScreenApplyAttempts >= MaxFullScreenApplyAttempts) return;

            _timerFullScreen += Time.unscaledDeltaTime;
            if (_timerFullScreen < ReapplyIntervalSeconds) return;
            _timerFullScreen = 0f;
            _fullScreenApplyAttempts++;

            if (!TryGetTargetMonitorRect(out Rect monitor, out bool isPrimaryMonitor))
            {
                Debug.LogWarning("[MacOverlayStateEnforcer] 전체화면 확장 실패 — 모니터 사각형을 조회하지 " +
                    $"못했습니다(GetMonitorCount={UniWindowController.GetMonitorCount()}). 창 크기를 그대로 둡니다.");
                _fullScreenApplyAttempts = MaxFullScreenApplyAttempts;
                return;
            }

            Vector2 sizeBefore = _controller.windowSize;
            Vector2 posBefore = _controller.windowPosition;

            // 라이브러리의 GetMonitorRect()는 실측 결과 **visibleFrame**(메뉴바 33pt + Dock 75pt를 뺀
            // 작업영역)을 돌려준다: (0,75,1512,874). 화면 진짜 전체(1512x982)를 덮으려면 그 두 띠까지
            // 포함해야 하고, 그러려면 화면 전체 높이를 알아야 하는데 라이브러리는 그 값을 노출하지 않는다.
            // 그래서 이미 갖고 있는 두 좌표계의 관계식으로 **유도**한다(추측이 아니라 항등식이다):
            //     라이브러리 pos.y(창 좌하단, Cocoa 좌하단 원점) + Quartz origin.y(창 좌상단, 좌상단 원점)
            //     + 창 높이  ==  화면 전체 높이
            // 실측 대입: 75 + 33 + 874 = 982 (= 1512x982 화면). Quartz origin은 MacWindowService가
            // 발판 폴링(0.5초)마다 갱신하므로 이 시점에 항상 현재 창 기준으로 최신이다.
            // 유도값이 상식 범위를 벗어나면(창보다 작거나 300pt 넘게 크면) 조용히 포기하고 작업영역만
            // 덮는다 — 잘못된 값으로 창을 화면 밖에 던지는 것보다 낫다.
            Rect targetRect = monitor;
            string coverageMode = "작업영역(visibleFrame)";
            var describer = ResolveDescriber();
            if (isPrimaryMonitor && describer != null && describer.TryGetMainDisplayBounds(out Rect display))
            {
                // 주 모니터에서는 Cocoa 좌표 y=0이 곧 화면 맨 아래이므로, 창의 좌하단을 (0,0)에 두고
                // 높이를 CGDisplayBounds가 알려준 화면 전체 높이로 늘리면 메뉴바 띠와 Dock 띠까지 전부
                // 덮인다. 보조 모니터는 Cocoa 원점이 주 모니터 기준이라 같은 식이 성립하지 않으므로
                // 작업영역(visibleFrame) 그대로 둔다(정직한 한계 — 멀티모니터 정교화는 별도 과제).
                targetRect = new Rect(0f, 0f, display.width, display.height);
                coverageMode = "화면 전체(메뉴바/Dock 띠 포함)";
            }
            monitor = targetRect;

            // (a) Unity 자신의 해상도.
            //
            // ★★ 단위 주의 (2026-08-29 Retina 대응 라운드 — 여기가 이번 작업에서 가장 위험한 한 줄이었다)
            // `monitor`는 UniWindowController/CGDisplayBounds가 준 **OS 포인트**(1512x982)이고,
            // `Screen.SetResolution`/`Screen.width`는 **Unity 픽셀**이다. `macRetinaSupport`가 꺼져 있던
            // 동안에는 두 단위가 우연히 같아서(1x) 아무 문제가 없었지만, 켠 뒤에도 포인트를 그대로
            // 넘기면 Unity가 백버퍼를 1512x982 **픽셀**로 잡아 창이 756x491 포인트 = 화면의 **1/4**로
            // 쪼그라든다. 게다가 바로 다음 줄의 windowSize 대입(포인트)과 서로 다른 크기를 요구하게 되어
            // 두 값이 매 재시도마다 싸운다.
            // 그래서 포인트 -> Unity 픽셀 변환을 반드시 거친다: 픽셀 = 포인트 / dpiScale (Retina면 x2).
            // 배율의 단일 소스는 ScreenCoordinateConverter다(MacWindowService가 발판 폴링마다 실측 보고).
            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(ResolveConfig()));
            int targetPixelW = Mathf.RoundToInt(monitor.width / dpi);
            int targetPixelH = Mathf.RoundToInt(monitor.height / dpi);

            // ★★★ 2026-09-01 — Windows에서 잡은 래칫과 **완전히 같은 결함**이 여기에도 있었다.
            //
            // 직전까지 이 판정은 `Screen.width != targetPixelW`, 즉 **완전일치**였고, 아래 windowSize/
            // windowPosition은 **무조건 대입**이었다. Windows판이 그 모양 그대로 세션이 갈수록 나빠지는
            // 407ms 멈춤을 만들었다(되읽기 1px 상수 오차 -> 영원히 "불일치" -> 매 에피소드
            // Screen.SetResolution + 창 리사이즈 -> 스왑체인/백버퍼 재생성).
            //
            // ---- macOS는 "지금" 발현 중인가: 아니다 (실측, 추측 아님) ------------------------------
            // 사용자 실기 로그(~/Library/Logs/DefaultCompany/StickMate/Player.log, 09-01 07:23)에
            // 이 함수의 로그는 **딱 한 줄**이었고 되읽기가 대입값과 정확히 같았다:
            //   `이전(size=(1512,982)) -> 이후(size=(1512,982))`, `Screen=(3024x1964) [목표 3024x1964]`,
            //   `결과=성공` -> _fullScreenBoundsApplied=true로 루프 종료.
            // Cocoa의 setFrame/frame은 같은 사각형을 돌려주므로 Windows의 GetWindowRect vs SetWindowPos
            // 불일치(레이어드+DWM 확장 프레임)에 해당하는 것이 없다. **즉 1px 래칫은 macOS 미발현이다.**
            //
            // ---- 그래도 가드를 넣는 이유: 구조적 위험은 발현 여부와 무관하다 ------------------------
            //   (1) 상한이 아예 없었다. TickDisplayTopology가 재무장할 때 _fullScreenApplyAttempts를 0으로
            //       되돌리므로, 디스플레이 통지가 진동하면 SetResolution 호출은 **무제한**이었다.
            //   (2) `ok`가 어떤 이유로든 한 번 실패하면 6회 재시도가 전부 실행되고, 그 6회가 **매번**
            //       SetResolution + 창 크기/위치 재대입이었다 = 백버퍼 재할당 6회.
            //   (3) dpi 배율은 상수가 아니라 **실측값**이다(AutoDpiScale = 창 폭 / Screen.width). 그 값이
            //       0.5에서 아주 조금만 흔들려도 RoundToInt 결과가 1픽셀 어긋나고, 완전일치 판정에서는
            //       그 1픽셀이 곧 영구 불일치다 — Windows에서 실제로 일어난 일이 그것이다.
            //
            // ---- 단위 함정: 여기서 2px을 그대로 쓰면 안 된다 ---------------------------------------
            // 이 함수의 두 판정은 좌표계가 다르다. 창 기하는 **OS 포인트**(1512x982), 해상도는
            // **Unity 픽셀**(3024x1964)이다. Retina에서 포인트 1 = 픽셀 2이므로 2px 상수를 해상도
            // 판정에 쓰면 실효 불감대가 1포인트로 반토막 난다. 그래서 불감대는 포인트로 정의하고
            // 픽셀 단위는 규칙이 배율로 **유도**한다(OverlayBoundsFitPolicy.ResolutionEpsilonPixels).
            // 숫자를 플랫폼마다 흩뿌리지 않는다.
            float resolutionEpsilon = OverlayBoundsFitPolicy.ResolutionEpsilonPixels(dpi);
            bool resolutionMismatch = !OverlayBoundsFitPolicy.Within(
                Screen.width, Screen.height, targetPixelW, targetPixelH, resolutionEpsilon);
            // 해상도가 맞아도 전체화면 계열 모드로 남아 있으면 반드시 한 번 내려야 한다 — Unity는 그
            // 모드에서 포커스를 잃으면 창을 z-order 뒤로 보낸다(Windows판이 실기로 확인한 경로).
            bool modeMismatch = Screen.fullScreenMode != FullScreenMode.Windowed;
            bool resolutionCapped = _setResolutionCalls >= OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls;
            if (OverlayBoundsFitPolicy.ShouldSetResolution(Screen.width, Screen.height,
                    targetPixelW, targetPixelH, !modeMismatch, resolutionEpsilon,
                    _setResolutionCalls, OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls))
            {
                _setResolutionCalls++;
                Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed);
            }

            // (b) 메뉴바/Dock 영역 위로도 창을 놓을 수 있게 한다.
            if (!_controller.isFreePositioningEnabled) _controller.isFreePositioningEnabled = true;

            // (c) 크기 -> 위치. **이미 불감대 안이면 대입 자체를 하지 않는다** — 대입 한 번이 곧 OS
            // 창 리사이즈 한 번이고, 그것이 백버퍼 재할당 한 번이다.
            bool needsResize = OverlayBoundsFitPolicy.ShouldResize(sizeBefore.x, sizeBefore.y,
                monitor.width, monitor.height, BoundsEpsilonPoints);
            bool needsMove = OverlayBoundsFitPolicy.ShouldMove(posBefore.x, posBefore.y,
                monitor.x, monitor.y, BoundsEpsilonPoints);
            if (needsResize)
            {
                _windowResizeCalls++;
                _controller.windowSize = monitor.size;
            }
            if (needsMove) _controller.windowPosition = monitor.position;

            Vector2 sizeAfter = _controller.windowSize;
            Vector2 posAfter = _controller.windowPosition;
            bool ok = OverlayBoundsFitPolicy.Within(sizeAfter.x, sizeAfter.y,
                    monitor.width, monitor.height, BoundsEpsilonPoints)
                && OverlayBoundsFitPolicy.Within(posAfter.x, posAfter.y,
                    monitor.x, monitor.y, BoundsEpsilonPoints);
            if (ok)
            {
                _fullScreenBoundsApplied = true;

                // 같은 프레임에 좌표계를 갱신한다(폴링 대기 없음). 창이 방금 다른 크기/원점이 됐는데
                // ScreenCoordinateConverter가 최대 한 폴링 주기 동안 옛 원점/배율을 들고 있으면, 그 사이의
                // 커서<->월드 변환과 발판 판정이 통째로 어긋나 캐릭터가 화면 밖으로 튄다.
                OverlayRectReporter?.Invoke();
            }

            // ★ 재생성 누적/이번 틱 실행 여부를 Windows판과 **같은 모양**으로 남긴다. 불감대가 진짜
            //   어긋남을 덮고 있지 않은지 사람이 로그만 보고 판정할 수 있어야 하고, 두 플랫폼 로그를
            //   나란히 놓고 비교할 수 있어야 한다(오늘만 세 번 반복된 "한쪽만 고침"의 재발 방지).
            Debug.Log($"[MacOverlayStateEnforcer] 전체화면 확장 시도 {_fullScreenApplyAttempts}/{MaxFullScreenApplyAttempts} — " +
                $"모니터={monitor}, 이전(size={sizeBefore}, pos={posBefore}) -> 이후(size={sizeAfter}, pos={posAfter}), " +
                $"재생성 누적(SetResolution {_setResolutionCalls}/{OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls}회" +
                $"{(resolutionCapped ? " ★상한 도달 — 더는 부르지 않는다" : "")}, 창리사이즈 {_windowResizeCalls}회), " +
                $"이번 틱 실행(SetResolution={(resolutionMismatch || modeMismatch) && !resolutionCapped}, " +
                $"리사이즈={needsResize}, 이동={needsMove}, 불감대={BoundsEpsilonPoints:F0}pt/{resolutionEpsilon:F1}px), " +
                $"Screen=({Screen.width}x{Screen.height}) [목표 {targetPixelW}x{targetPixelH} 픽셀, dpi배율={dpi:F3}], " +
                $"fullScreenMode={Screen.fullScreenMode}(직전 불일치: 해상도={resolutionMismatch}, 모드={modeMismatch}), " +
                $"isFreePositioningEnabled={_controller.isFreePositioningEnabled}, " +
                $"덮는범위={coverageMode}, 결과={(ok ? $"성공(오차 {BoundsEpsilonPoints:F0}pt 이내)" : "미달 — 다음 시도에서 재적용")}.");
        }

        private float _timerFullScreen;

        /// <summary>
        /// 실행 중 디스플레이 구성 변경 감시 — <see cref="_fullScreenBoundsApplied"/> 재무장 지점
        /// (2026-08-31 perf-doc 지적: 그 플래그를 false로 되돌리는 경로가 아예 없어서 오버레이 창이
        /// 최초 기동 해상도에 영원히 박제됐다). Windows판(WindowsOverlayStateEnforcer.TickDisplayTopology)과
        /// <b>같은 원칙·같은 공용 클래스</b>를 쓴다 — 한쪽만 고치면 다른 쪽에서 그대로 재발한다.
        ///
        /// 두 가지 안전장치가 있고 둘 다 없으면 안 된다:
        ///   (1) <b>적합 진행 중에는 관측하지 않는다.</b> 재적합은 Screen.SetResolution/창 크기를 우리가
        ///       직접 바꾸는 일이라, 그 와중의 관측은 "우리가 만든 변화"를 새 사건으로 오인한다.
        ///   (2) <b>에피소드가 끝나면 기준값을 다시 잡는다</b>(관측 대신 ResetBaseline 1회).
        ///       (1)과 합쳐 "재적합 -> 시그니처 변화 -> 재적합"의 무한 루프를 원천 차단한다.
        ///
        /// 디바운스(마지막 변화 후 0.75초 안정)는 전부 DisplayTopologyWatcher 안에 있다. macOS에서
        /// 해상도 모드 전환은 특히 중간 상태를 여러 번 노출하며(디스플레이 재구성 통지가 연속으로 온다),
        /// 그때마다 Screen.SetResolution을 부르면 지금보다 훨씬 큰 히치를 우리가 직접 만든다.
        /// </summary>
        private void TickDisplayTopology()
        {
            bool fitInProgress = !_fullScreenBoundsApplied && _fullScreenApplyAttempts < MaxFullScreenApplyAttempts;
            if (fitInProgress) return;

            // 매 프레임 디스플레이를 열거하는 것은 24시간 상주 앱에서 순수 낭비다. 누적 시간을 그대로
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
            _timerFullScreen = ReapplyIntervalSeconds; // 다음 TickFullScreenBounds에서 곧바로 1회.
            _topologyBaselineSynced = false;
            // ★ _setResolutionCalls는 **일부러 되돌리지 않는다**. 그것이 "프로세스 수명 상한"이라는
            //   말의 전부다 — 여기서 0으로 돌리면 디스플레이 통지가 진동할 때 상한이 사실상 사라지고,
            //   래칫을 막으려고 넣은 가드가 무력해진다.

            Debug.Log("[MacOverlayStateEnforcer] 디스플레이 구성 변경이 안정됐습니다 — " +
                $"{_topologyWatcher.Baseline}. 전체화면 재적합 루프를 다시 무장합니다" +
                $"({ReapplyIntervalSeconds}초 x {MaxFullScreenApplyAttempts}회 분산).");
        }

        /// <summary>
        /// 이번 틱의 화면 구성 지문. <b>OS가 주는 값만</b> 넣는다 — 우리 창 크기/위치에서 유도되는 값
        /// (ScreenCoordinateConverter.AutoDpiScale = 창 폭 / Screen.width)을 넣으면 재적합이 자기 자신을
        /// 다시 트리거한다(DisplayTopologyWatcher 문서).
        ///
        /// macOS에서 화면 전체 크기는 CGDisplayBounds(TryGetMainDisplayBounds)로 읽는다. 이 값은 순수
        /// 조회이고 창과 무관하며, 사용자가 "더 넓게/더 크게" 배율 해상도를 바꾸면 <b>포인트 크기 자체가</b>
        /// 바뀌므로 해상도 변경과 배율 변경을 한 신호로 함께 잡는다(그래서 UI 밀도 항은 쓰지 않는다 —
        /// macOS에서는 아무도 보고하지 않아 항상 0이다).
        /// </summary>
        private DisplayTopologySignature SampleTopology()
        {
            int count = UniWindowController.GetMonitorCount();
            if (count <= 0) return DisplayTopologySignature.Invalid;
            if (!TryGetTargetMonitorRect(out Rect monitor, out _)) return DisplayTopologySignature.Invalid;

            Vector2 desktopSize = Vector2.zero;
            var describer = ResolveDescriber();
            if (describer != null && describer.TryGetMainDisplayBounds(out Rect display))
            {
                desktopSize = display.size;
            }

            return DisplayTopologySignature.Create(count, monitor, desktopSize,
                ScreenCoordinateConverter.AutoUiDensityScale);
        }

        /// <summary>
        /// 창 중심이 속한 모니터의 사각형을 라이브러리 좌표계 그대로 돌려준다. 멀티 모니터에서 어느
        /// 화면을 덮을지 결정하는 유일한 기준이며, 실패 시 0번(주 모니터)로 폴백한다.
        /// </summary>
        private bool TryGetTargetMonitorRect(out Rect monitor, out bool isPrimary)
        {
            monitor = default;
            isPrimary = false;
            int count = UniWindowController.GetMonitorCount();
            if (count <= 0) return false;

            Vector2 pos = _controller.windowPosition;
            Vector2 size = _controller.windowSize;
            Vector2 center = pos + size * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Rect r = UniWindowController.GetMonitorRect(i);
                if (r.width <= 0f || r.height <= 0f) continue;
                if (r.Contains(center))
                {
                    monitor = r;
                    isPrimary = i == 0;
                    return true;
                }
            }

            Rect primary = UniWindowController.GetMonitorRect(0);
            if (primary.width <= 0f || primary.height <= 0f) return false;
            monitor = primary;
            isPrimary = true;
            return true;
        }

        /// <summary>
        /// ★ 헤드라인 기능 검증 리포트 — "지금 감지된 실제 창 목록"과 "캐릭터가 지금 무엇을 딛고 있는지"를
        /// 한 줄로 남긴다. 리더가 화면을 볼 수 없으므로 이 로그가 유일한 판별 수단이다(작업 지시 4항).
        ///
        /// 읽는 법:
        ///   발판N개=[Finder@(x,y,w,h) ...]  — MacWindowService가 이번 폴링에서 실제로 채택한 타 앱 창들.
        ///                                     비어 있으면 "실제 창 0개"(= 안전망만 있는 상태)다.
        ///   딛고있음=Finder / 합성안전망 / (공중) — GroundSensor가 이번 프레임에 채택한 그 발판.
        ///   캐릭터OS=(x,y)                  — 캐릭터 발 위치를 창 원점 보정까지 거쳐 OS 좌표로 되돌린 값.
        ///                                     "딛고있음"이 실제 창이면 그 창의 상단 y와 거의 같아야 한다.
        /// </summary>
        private void TickFootholdReport()
        {
            bool verbose = VerboseDiagnostics; // 프로퍼티가 에이전트 탐색까지 겸한다.
            float reportInterval = verbose ? FootholdReportIntervalSecondsVerbose : FootholdReportIntervalSecondsQuiet;

            _footholdReportTimer += Time.unscaledDeltaTime;
            if (_footholdReportTimer < reportInterval) return;
            _footholdReportTimer = 0f;

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.FootholdPoller == null) return;

            var footholds = blackboard.FootholdPoller.CachedFootholds;
            var describer = ResolveDescriber();

            _reportBuilder.Clear();
            // ★ 2026-08-29 추가 — 합성 발판(Dock + 안전망 두 조각)의 사각형을 매 리포트에 함께 남긴다.
            // 이번 라운드의 버그("독과 겹쳐서 걸음")는 정확히 "Dock 발판의 X 구간"과 "바닥 안전망의 X
            // 구간"이 겹쳐 있던 것이 원인인데, 지금까지 리포트가 안전망을 아예 표기하지 않아 로그만
            // 봐서는 그 겹침을 볼 수 없었다. 이제 한 줄에서 "Dock 구간 / 안전망 왼쪽 / 안전망 오른쪽"이
            // 서로 정확히 맞물려 있는지(틈도 겹침도 없는지)를 사람이 바로 확인할 수 있다.
            _syntheticBuilder.Clear();
            int realCount = 0;
            int listed = 0;
            for (int i = 0; i < footholds.Count; i++)
            {
                var fh = footholds[i];
                // 합성 발판(안전망 두 조각 / Dock)은 실제 창과 섞이지 않게 별도 문자열로 모은다.
                if (fh.Handle == FallbackPlatformWindowService.SyntheticFootholdHandle
                    || fh.Handle == FallbackPlatformWindowService.SyntheticFootholdHandleRight
                    || fh.Handle == FallbackPlatformWindowService.DockFootholdHandle)
                {
                    if (_syntheticBuilder.Length > 0) _syntheticBuilder.Append(", ");
                    Rect sr = fh.ScreenRect;
                    _syntheticBuilder
                        .Append(fh.Handle == FallbackPlatformWindowService.DockFootholdHandle ? "Dock"
                            : fh.Handle == FallbackPlatformWindowService.SyntheticFootholdHandle ? "안전망왼쪽" : "안전망오른쪽")
                        .Append(" x").Append(sr.x.ToString("F0")).Append('~').Append(sr.xMax.ToString("F0"))
                        .Append(" 상단y").Append(sr.y.ToString("F0"));
                    continue;
                }
                realCount++;
                if (listed >= MaxFootholdsPerReport) continue;
                if (listed > 0) _reportBuilder.Append(", ");
                listed++;
                Rect r = fh.ScreenRect;
                _reportBuilder.Append(DescribeName(describer, fh.Handle))
                    .Append('@').Append('(').Append(r.x.ToString("F0")).Append(',').Append(r.y.ToString("F0"))
                    .Append(' ').Append(r.width.ToString("F0")).Append('x').Append(r.height.ToString("F0")).Append(')');
            }
            if (realCount > listed) _reportBuilder.Append(" 외 ").Append(realCount - listed).Append("개");

            var body = blackboard.Body;
            Vector2 charOs = Vector2.zero;
            string standing = "(몸 없음)";
            string groundTop = "-";
            if (body != null && blackboard.MainCamera != null)
            {
                charOs = ScreenCoordinateConverter.WorldToOsScreen(blackboard.MainCamera, body.position, blackboard.Config, out _);
                var info = blackboard.SenseGround();
                if (!info.Grounded)
                {
                    standing = "(공중/낙하)";
                }
                else if (info.GroundedFootholdHandle == FallbackPlatformWindowService.SyntheticFootholdHandle)
                {
                    standing = "화면 최하단 안전망(Dock 왼쪽 바깥)";
                }
                else if (info.GroundedFootholdHandle == FallbackPlatformWindowService.SyntheticFootholdHandleRight)
                {
                    standing = "화면 최하단 안전망(Dock 오른쪽 바깥)";
                }
                else if (info.GroundedFootholdHandle == FallbackPlatformWindowService.DockFootholdHandle)
                {
                    standing = "Dock";
                }
                else
                {
                    standing = "실제 창: " + DescribeName(describer, info.GroundedFootholdHandle);
                }
                if (info.Grounded)
                {
                    Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(blackboard.MainCamera,
                        new Vector2(body.position.x, info.GroundWorldY), blackboard.Config, out _);
                    groundTop = groundOs.y.ToString("F1");
                }
            }

            string occlusionNote = describer != null
                ? $" (원본창 {describer.LastRawWindowCount}개 중 완전히 가려져 제외 {describer.LastFullyOccludedWindowCount}개)"
                : string.Empty;
            // 리더 지시: "화면 경계 내 여부"를 매 리포트에 명시한다(화면 밖 소실 추적).
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            Vector2 winSize = _controller.windowSize;
            bool insideScreen = charOs.x >= origin.x && charOs.x <= origin.x + winSize.x
                && charOs.y >= origin.y && charOs.y <= origin.y + winSize.y;

            Debug.Log($"[발판리포트] 보이는 상단테두리 {realCount}개{occlusionNote}=[{_reportBuilder}] | 합성=[{_syntheticBuilder}] | 딛고있음={standing} | " +
                $"고착핸들={blackboard.CurrentFootholdHandle} | 발판상단OS y={groundTop} | " +
                $"캐릭터OS=({charOs.x:F1},{charOs.y:F1}) 화면안={(insideScreen ? "예" : "아니오(문제!)")} | " +
                $"상태={(blackboard.Machine != null ? blackboard.Machine.CurrentStateId.ToString() : "?")} | " +
                $"오버레이원점={origin}, 창={winSize}, Screen=({Screen.width}x{Screen.height})");

            // 리더 지시 1항 — 창 전체 덤프(앱 이름 + PID + 사각형 + 알파 + onscreen + z-order + 탈락 사유).
            // 발판 리포트보다 훨씬 길어서 주기를 별도로 둔다.
            _windowDumpTimer += reportInterval;
            if (verbose && describer != null && _windowDumpTimer >= WindowDumpIntervalSeconds)
            {
                _windowDumpTimer = 0f;
                _reportBuilder.Clear();
                describer.AppendWindowDiagnostics(_reportBuilder);
                Debug.Log("[창진단] " + _reportBuilder);
            }
        }

        /// <summary>
        /// 발판 핸들 -> 소유 앱 이름 조회기(MacWindowService). StickmanAgent는 항상
        /// FallbackPlatformWindowService 데코레이터를 노출하므로 한 겹 벗겨서 찾는다.
        /// </summary>
        private MacWindowService ResolveDescriber()
        {
            var service = _agent != null ? _agent.PlatformService : null;
            if (service is FallbackPlatformWindowService decorator) service = decorator.Inner;
            return service as MacWindowService;
        }

        private static string DescribeName(MacWindowService describer, long handle)
        {
            string name = describer != null ? describer.TryDescribeFoothold(handle) : null;
            return string.IsNullOrEmpty(name) ? "창#" + handle : name;
        }

        /// <summary>
        /// 위 "히트테스트 실측 감시" 주석 참고 — 라이브러리와 같은 질의를 두 지점에 직접 쏴 보고,
        /// 클릭관통/히트테스트 플래그를 함께 되읽어 Player.log에 남긴다. 창 부착 후 ProbeDurationSeconds
        /// 동안만 1초 간격으로 돌고 스스로 멈춘다(24시간 상주 앱의 로그를 영원히 더럽히지 않는다).
        /// </summary>
        private void TickHitTestProbe()
        {
            if (_elapsed > ProbeDurationSeconds) return;
            // 시작 25초로 이미 스스로 멈추는 유한 프로브지만, 평상시에는 이것도 남기지 않는다
            // (위 FootholdReportIntervalSecondsQuiet 문단의 로그량 정리와 같은 취지 — 삭제가 아니라
            // StickConfig.verboseDiagnosticsLogging 스위치로 이동).
            if (!VerboseDiagnostics) return;

            _probeTimer += Time.unscaledDeltaTime;
            if (_probeTimer < ProbeIntervalSeconds) return;
            _probeTimer = 0f;

            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null) return;

            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
            var body = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Body : null;
            if (body == null) return;

            // (A) 캐릭터 몸통 중앙(발끝 원점 + 대략 상반신 높이)의 화면 좌표.
            Vector3 charWorld = body.position + new Vector2(0f, 1.1f);
            Vector3 charScreen = cam.WorldToScreenPoint(charWorld);
            RaycastHit2D onChar = Physics2D.GetRayIntersection(cam.ScreenPointToRay(charScreen));

            // (B) 캐릭터에서 화면 가로로 멀리 떨어진 빈 지점(같은 높이) — 반드시 아무것도 없어야 한다.
            var emptyScreen = new Vector3(
                Mathf.Repeat(charScreen.x + Screen.width * 0.4f, Screen.width),
                charScreen.y, charScreen.z);
            RaycastHit2D onEmpty = Physics2D.GetRayIntersection(cam.ScreenPointToRay(emptyScreen));

            Debug.Log("[MacOverlayStateEnforcer] 히트테스트 감시 — " +
                $"모드={_controller.hitTestType}, isHitTestEnabled={_controller.isHitTestEnabled}, " +
                $"isClickThrough={_controller.isClickThrough}, isTransparent={_controller.isTransparent}, " +
                $"isTopmost={_controller.isTopmost} / " +
                $"캐릭터지점(screen {charScreen.x:F0},{charScreen.y:F0})={(onChar.collider != null ? onChar.collider.name + "/" + onChar.collider.GetType().Name : "미검출(문제!)")}, " +
                $"빈지점(screen {emptyScreen.x:F0},{emptyScreen.y:F0})={(onEmpty.collider != null ? onEmpty.collider.name + " (관통 깨짐!)" : "없음(정상 관통)")}, " +
                $"상태={( _agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null ? _agent.Blackboard.Machine.CurrentStateId.ToString() : "?")}, " +
                $"오버레이원점={StickMate.Platform.ScreenCoordinateConverter.OverlayOriginOsScreen}.");
        }

        /// <summary>
        /// 투명이 실제로 켜진 것이 확인된 뒤에만, 카메라 배경 RGB를 검정으로 낮춘다(알파는 계속 0).
        ///
        /// ============================================================================
        /// 왜 필요한가 — "캐릭터 주변이 반짝거림"의 진짜 원인(2026-08-28 사용자 지적)
        /// ============================================================================
        /// 씬에는 카메라 배경이 (0.94, 0.94, 0.94, 0) = "밝은 회색 + 알파 0"으로 저장돼 있다. RGB를 밝은
        /// 회색으로 둔 것은 "투명화가 실패해도 검정-on-검정이 되지 않게" 하려는 이전 라운드의 방어책이다
        /// (SceneBootstrapper.BuildMainScene 주석 참고). 알파가 0이라 투명이 성공하면 이 RGB는 눈에
        /// 보이지 않는다 — **MSAA를 켜기 전까지는**.
        ///
        /// MSAA는 한 픽셀 안의 여러 서브샘플을 평균해서 최종 색을 만든다. 캐릭터 윤곽선 픽셀은 일부
        /// 서브샘플만 검은 선에 덮이므로, 예를 들어 50% 덮인 픽셀은
        ///     rgb = (검정 0.0 x 0.5) + (배경 0.94 x 0.5) = 0.47,  alpha = (1 x 0.5) + (0 x 0.5) = 0.5
        /// 가 된다. 즉 **알파 0인 배경의 밝은 RGB가 가장자리 픽셀로 새어 들어온다.** 그 결과 검은 캐릭터
        /// 둘레에 밝은 회색 테두리(프린지)가 생기고, 캐릭터가 서브픽셀 단위로 움직일 때마다 그 테두리
        /// 밝기가 프레임마다 변해 "반짝거리는" 것처럼 보인다.
        ///
        /// 배경 RGB를 검정으로 낮추면 같은 픽셀이 rgb = 0, alpha = 0.5가 되어 프린지 없이 정확히
        /// "50% 농도의 검은 선"으로 합성된다 — 계단 현상 제거(MSAA)와 반짝임 제거를 동시에 얻는다.
        /// 실제로 UniWindowController 자신도 autoSwitchCameraBackground가 켜져 있으면 투명화 시점에
        /// 배경을 Color.clear(= 0,0,0,0)로 바꾼다(SetCameraBackground()) — 우리가 그 자동 전환을 끄고
        /// 밝은 회색을 유지한 것이 바로 이 아티팩트의 원인이었다. 즉 이 메서드는 라이브러리가 원래 하던
        /// 일을 "투명이 실제로 확인된 뒤에만" 하도록 조건부로 되살리는 것이다.
        ///
        /// 방어책은 그대로 유지된다: 이 교정은 창이 실제로 부착되고 isTransparent가 true로 되읽힌
        /// 경우에만 수행한다. 투명화가 실패한 상황에서는 배경이 밝은 회색으로 남아, 예전처럼
        /// "밝은 회색 창 안의 검정 캐릭터"(최소한 보이는 상태)가 된다.
        /// </summary>
        private void ApplyTransparentSafeCameraBackground()
        {
            if (_cameraBackgroundPremultiplyFixed) return;
            if (!_controller.isTransparent) return;

            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null) return;

            Color before = cam.backgroundColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, before.a);
            _cameraBackgroundPremultiplyFixed = true;

            Debug.Log($"[MacOverlayStateEnforcer] 투명 확인됨 — 카메라 배경 RGB를 검정으로 교정했습니다 " +
                $"(MSAA 가장자리 프린지/반짝임 제거): ({before.r:F2},{before.g:F2},{before.b:F2},{before.a:F2}) " +
                $"-> (0.00,0.00,0.00,{before.a:F2}). 알파는 그대로 유지.");

            LogRenderQualityDiagnostics(cam);
        }

        /// <summary>
        /// 렌더 품질 **실측** 진단 — 딱 한 번(투명 확인 직후) 찍는다.
        ///
        /// ============================================================================
        /// 왜 이 로그가 필요한가 (2026-08-29 "캐릭터 선이 저해상도로 보임" 조사 라운드)
        /// ============================================================================
        /// 이 프로젝트는 그동안 안티에일리어싱 문제를 "QualitySettings.antiAliasing을 4로 설정했다"는
        /// **설정값**만 보고 판단해 왔다. 그런데 설정값과 실제로 GPU가 쓰는 샘플 수는 다를 수 있다:
        ///   · Camera.allowMSAA가 꺼져 있으면 무시된다.
        ///   · 카메라가 RenderTexture로 우회 렌더되면(targetTexture != null 또는 이미지 이펙트)
        ///     백버퍼 MSAA 경로 자체를 타지 않는다.
        ///   · HDR 버퍼로 강제되면 포맷에 따라 샘플 수가 내려간다.
        ///   · **하드웨어가 요청한 샘플 수를 지원하지 않으면 드라이버가 조용히 낮춘다**
        ///     (8x를 요청해도 4x로 떨어질 수 있다 — 이 값이 이 라운드의 핵심 관측 대상이다).
        /// `Screen.msaaSamples`가 그 최종 실측치이므로, 요청값(QualitySettings.antiAliasing)과
        /// 나란히 남겨 둘이 어긋나는 순간 로그만 보고 바로 알 수 있게 한다.
        ///
        /// 함께 남기는 "물리픽셀/월드유닛"과 획 두께 실측은 "선이 얇아서 계단이 보이는 것"과
        /// "렌더 해상도가 낮아서 계단이 보이는 것"을 구분하는 근거다 — 전자면 픽셀 수는 정상인데
        /// 획이 얇은 것이고, 후자면 화면 전체가 저해상도다(Retina 회귀 재발 감시도 겸한다).
        /// </summary>
        private void LogRenderQualityDiagnostics(Camera cam)
        {
            if (_renderQualityDiagnosticsLogged) return;
            _renderQualityDiagnosticsLogged = true;

            // 세로 물리픽셀 / 세로 월드유닛(= orthographicSize * 2). 캐릭터가 화면에서 실제로 몇
            // 픽셀을 차지하는지 계산하는 유일한 환산 계수다.
            float pixelsPerUnit = cam.orthographic && cam.orthographicSize > 0f
                ? cam.pixelHeight / (cam.orthographicSize * 2f)
                : 0f;

            // 캐릭터 선 두께 실측 — 씬의 모든 LineRenderer를 한 번만 훑는다(상주 앱이라 매 프레임
            // 하지 않는다). 월드 스케일이 곱해진 실제 두께를 봐야 하므로 lossyScale.x를 함께 쓴다.
            float minWidthPx = float.MaxValue, maxWidthPx = 0f;
            int lineCount = 0;
            var lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (LineRenderer lr in lines)
            {
                if (lr == null) continue;
                float widthPx = lr.startWidth * Mathf.Abs(lr.transform.lossyScale.x) * pixelsPerUnit;
                if (widthPx <= 0f) continue;
                lineCount++;
                if (widthPx < minWidthPx) minWidthPx = widthPx;
                if (widthPx > maxWidthPx) maxWidthPx = widthPx;
            }
            if (lineCount == 0) minWidthPx = 0f;

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
                $" | orthographicSize={cam.orthographicSize:F2} -> {pixelsPerUnit:F1} 물리픽셀/유닛" +
                $" | LineRenderer {lineCount}개 획 두께 실측 {minWidthPx:F2}~{maxWidthPx:F2} 물리픽셀" +
                $" | GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
        }

        private string CameraBackgroundDescription()
        {
            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null)
            {
                return "(카메라 없음)";
            }
            Color c = cam.backgroundColor;
            return $"clearFlags={cam.clearFlags}, rgba=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
        }
    }
}
#endif
