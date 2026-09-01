#if UNITY_STANDALONE_WIN
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

        /// <summary>부착 확인 후 목표 상태를 재적용할 최대 횟수(macOS와 동일한 값/이유). 무한 반복은
        /// 하지 않는다 — 사용자가 창을 직접 조작했을 때 우리가 계속 되돌리는 것이 더 나쁘다.</summary>
        private const int ReapplyAttempts = 5;
        private const float ReapplyIntervalSeconds = 0.5f;
        private const float AttachTimeoutSeconds = 15f;

        /// <summary>전체화면 확장 재시도 상한 — 해상도 변경이 프레임 끝에 반영되고 창 스타일 확정에도
        /// 한두 프레임 걸려서 한 번에 성공하지 않을 수 있다.</summary>
        private const int MaxFullScreenApplyAttempts = 6;

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

        // 실행 중 디스플레이 구성 변경 추적(2026-08-31). 판단 로직은 플랫폼 공용
        // Platform/DisplayTopologyWatcher.cs 한 곳에 있고 여기서는 관측만 한다 — macOS판도 같은 클래스를
        // 같은 방식으로 쓴다(오늘 VisibleTopEdgeSolver에서 한쪽만 고쳐 재발한 사례의 재발 방지).
        private readonly DisplayTopologyWatcher _topologyWatcher = new DisplayTopologyWatcher();
        /// <summary>전체화면 적합 에피소드가 끝난 뒤 기준값을 다시 잡았는가. 재무장할 때 false로 돌린다.</summary>
        private bool _topologyBaselineSynced;
        /// <summary>토폴로지 관측 주기(초). 디바운스 창(0.75초)보다 충분히 짧아 판정 해상도는 잃지 않으면서
        /// OS 디스플레이 열거 호출을 초당 60회에서 10회로 줄인다.</summary>
        private const float TopologySampleIntervalSeconds = 0.1f;
        private float _topologySampleTimer;

        /// <summary>플랫폼 계층이 배선하는 "지금 즉시 오버레이 창 OS 사각형을 보고하라" 훅
        /// (Win32WindowService.CaptureOverlayOrigin). 재적합 직후 같은 프레임에 좌표계를 갱신해
        /// 0.5초 폴링을 기다리는 동안 캐릭터가 옛 좌표계로 튀는 구간을 없앤다.</summary>
        internal System.Action OverlayRectReporter;

        // 목표 상태 — Win32WindowService가 자기 API 호출 때마다 갱신한다.
        internal bool DesiredTransparent = true;
        internal bool DesiredTopmost;
        internal bool DesiredClickThrough;
        internal bool DesiredHitTest;

        internal static WindowsOverlayStateEnforcer EnsureExists(UniWindowController controller)
        {
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
            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
            var blackboard = _agent != null ? _agent.Blackboard : null;
            return blackboard != null ? blackboard.Config : null;
        }

        private void Update()
        {
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
            if (_agent == null) _agent = UnityEngine.Object.FindAnyObjectByType<Core.StickmanAgent>();
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
            // ★ 왜 isTopmost <b>만</b> 가드하는가 (나머지는 일부러 무조건 대입한다)
            //   · isTopmost 게터는 `_isTopmost = _uniWinCore.IsTopmost`로 <b>네이티브 진실을 되읽는다</b>.
            //     따라서 "이미 맞다"는 판정이 실제 창 상태에 근거한다 — 버려진 값은 여전히 복구된다.
            //   · isTransparent / isClickThrough 게터는 <b>캐시된 C# 필드</b>를 그대로 돌려준다
            //     (UniWindowController.cs:136-141, :126-131). 네이티브가 값을 조용히 버려도 캐시는
            //     목표값 그대로다. 여기에 같은 가드를 걸면 "투명이 실제로는 안 걸렸는데 걸린 줄 알고
            //     재적용을 건너뛰는" 최악의 경우가 생긴다 — 회색 불투명 전체화면 창이다.
            //     이 enforcer가 존재하는 이유 자체가 그 사고를 막는 것이므로 절대 가드하지 않는다.
            //   · isHitTestEnabled는 네이티브 부작용이 없는 평범한 public 필드라 대입 비용이 0이다.
            _controller.isHitTestEnabled = DesiredHitTest;
            _controller.isTransparent = DesiredTransparent;
            bool topmostSkipped = _controller.isTopmost == DesiredTopmost;
            if (!topmostSkipped) _controller.isTopmost = DesiredTopmost;
            _controller.isClickThrough = DesiredClickThrough;

            Debug.Log($"[WindowsOverlayStateEnforcer] 재적용 {_appliedCount}/{ReapplyAttempts} " +
                $"(isTopmost 재적용={(topmostSkipped ? "생략(이미 목표값)" : "실행")}) — " +
                $"목표(transparent={DesiredTransparent}, topmost={DesiredTopmost}, " +
                $"clickThrough={DesiredClickThrough}, hitTest={DesiredHitTest}) / " +
                $"되읽음(isTransparent={_controller.isTransparent}, isTopmost={_controller.isTopmost}, " +
                $"isClickThrough={_controller.isClickThrough}, isHitTestEnabled={_controller.isHitTestEnabled}) / " +
                $"windowSize={_controller.windowSize}, windowPosition={_controller.windowPosition}, " +
                $"transparentType={_controller.transparentType}.");
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

            // 단위: Windows에서는 Unity Player가 per-monitor DPI aware라 Screen.width(Unity 픽셀)와
            // Win32/라이브러리의 좌표(물리 픽셀)가 같은 단위이므로 배율이 1.0으로 실측된다. 그래도
            // 값을 하드코딩하지 않고 macOS와 같은 단일 소스(ScreenCoordinateConverter)를 거친다 —
            // 배율이 1이 아닌 환경이 나오면 그쪽 한 곳만 고치면 되게 하기 위함이다.
            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(ResolveConfig()));
            int targetPixelW = Mathf.RoundToInt(monitor.width / dpi);
            int targetPixelH = Mathf.RoundToInt(monitor.height / dpi);
            if (Screen.width != targetPixelW || Screen.height != targetPixelH)
            {
                Screen.SetResolution(targetPixelW, targetPixelH, FullScreenMode.Windowed);
            }

            // 크기 -> 위치 순서(크기를 먼저 정해야 위치 대입이 최종 좌표가 된다).
            _controller.windowSize = monitor.size;
            _controller.windowPosition = monitor.position;

            Vector2 sizeAfter = _controller.windowSize;
            Vector2 posAfter = _controller.windowPosition;
            bool ok = Mathf.Abs(sizeAfter.x - monitor.width) <= 1f && Mathf.Abs(sizeAfter.y - monitor.height) <= 1f
                && Mathf.Abs(posAfter.x - monitor.x) <= 1f && Mathf.Abs(posAfter.y - monitor.y) <= 1f;
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
                $"clientSize={_controller.clientSize}, " +
                $"Screen=({Screen.width}x{Screen.height}) [목표 {targetPixelW}x{targetPixelH} 픽셀, dpi배율={dpi:F3}], " +
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

        /// <summary>창 중심이 속한 모니터의 사각형(실패 시 0번 = 주 모니터로 폴백).</summary>
        private bool TryGetTargetMonitorRect(out Rect monitor)
        {
            monitor = default;
            int count = UniWindowController.GetMonitorCount();
            if (count <= 0) return false;

            Vector2 center = _controller.windowPosition + _controller.windowSize * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Rect r = UniWindowController.GetMonitorRect(i);
                if (r.width <= 0f || r.height <= 0f) continue;
                if (r.Contains(center))
                {
                    monitor = r;
                    return true;
                }
            }

            Rect primary = UniWindowController.GetMonitorRect(0);
            if (primary.width <= 0f || primary.height <= 0f) return false;
            monitor = primary;
            return true;
        }

        /// <summary>
        /// 투명이 실제로 확인된 뒤에만 카메라 배경 RGB를 검정으로 낮춘다(알파는 보존).
        /// macOS판과 같은 이유이며 Windows에서 오히려 더 중요하다: 투명 창 합성이 알파 채널을
        /// 프리멀티플라이드로 다루므로 배경 RGB가 밝으면 캐릭터 가장자리에 밝은 프린지가 남는다.
        /// 투명화가 실패한 상황에서는 손대지 않아 "밝은 배경 안의 캐릭터"(최소한 보이는 상태)가 된다.
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

            Debug.Log($"[WindowsOverlayStateEnforcer] 투명 확인됨 — 카메라 배경 RGB를 검정으로 교정 " +
                $"({before.r:F2},{before.g:F2},{before.b:F2},{before.a:F2}) -> (0.00,0.00,0.00,{before.a:F2}). " +
                $"MSAA 요청={QualitySettings.antiAliasing}x, 실측 Screen.msaaSamples={Screen.msaaSamples}x, " +
                $"allowMSAA={cam.allowMSAA}, GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}).");
        }
    }
}
#endif
