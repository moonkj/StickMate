using System.Collections.Generic;
using UnityEngine;
using StickMate.Platform;
using StickMate.States;
#if UNITY_STANDALONE_WIN
using StickMate.Platform.Windows;
#endif
#if UNITY_IOS || UNITY_ANDROID
using StickMate.Platform.Mobile;
#endif

namespace StickMate.Core
{
    /// <summary>
    /// Phase 1 코어 루프의 실제 진입점. 플랫폼 서비스 선택, 발판 폴러/상태머신 생성, 매 프레임 입력
    /// 스냅샷, 클릭 관통 기본 ON 배선, 전체화면 감지 → Suspended 처리를 모두 이 MonoBehaviour가
    /// 조율한다. Rigidbody2D가 붙은 캐릭터 루트 오브젝트에 부착한다(씬/프리팹 배선은 Phase 2+에서 진행).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class StickmanAgent : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private Rigidbody2D _body;
        private Camera _mainCamera;
        private IPlatformWindowService _platformService;
        private ICursorPositionService _cursorService; // 지원하는 구현체에서만 non-null (분리된 경로, ICursorPositionService.cs 참고)
        private FootholdPoller _footholdPoller;
        private StickmanStateMachine _machine;
        private StickmanBlackboard _blackboard;
        private Renderer[] _renderers;

        private float _fullscreenPollTimer;
        private bool _isSuspended;

        /// <summary>
        /// 클릭 관통(SetClickThrough)과 완전히 독립된 커서 좌표 조회 경로(UX_FLOW.md 9절-3).
        /// 지원하지 않는 플랫폼/구현체(모바일 등)에서는 항상 false.
        /// </summary>
        public bool TryGetCursorPosition(out Vector2 osScreenPosition)
        {
            if (_cursorService != null) return _cursorService.TryGetGlobalCursorPosition(out osScreenPosition);
            osScreenPosition = default;
            return false;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _mainCamera = Camera.main;
            _renderers = GetComponentsInChildren<Renderer>(true);

            _platformService = CreatePlatformService();
            _cursorService = _platformService as ICursorPositionService;
            _footholdPoller = new FootholdPoller(_platformService, _config);

            _blackboard = new StickmanBlackboard
            {
                Body = _body,
                MainCamera = _mainCamera,
                Config = _config,
                FootholdPoller = _footholdPoller,
            };

            var states = new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new IdleState(_blackboard) },
                { StickmanStateId.Walk, new WalkState(_blackboard) },
                { StickmanStateId.Jump, new JumpState(_blackboard) },
                { StickmanStateId.Fall, new FallState(_blackboard) },
                // Phase 1 범위 밖 — Phase 0 스텁을 그대로 등록(파라미터 없는 생성자 유지 중).
                // 전부 등록해두는 이유: StickmanStateMachine.ChangeState()가 미등록 키를 안전하게
                // 거부하도록 방금 고쳤지만(BUG-M2), 애초에 8종을 다 등록해두면 그 방어 코드를 밟을
                // 일 자체가 없다.
                { StickmanStateId.ParkourClimb, new ParkourClimbState() },
                { StickmanStateId.Attack, new AttackState() },
                { StickmanStateId.Ragdoll, new RagdollState() },
                { StickmanStateId.Getup, new GetupState() },
            };

            // 주의(Debugger 검토 요청 — Tasklist.md 교차 레이어 로그 참고): StickmanStateMachine 생성자는
            // 즉시 ChangeState(initialState)를 호출해 초기 상태의 Enter()를 실행한다. 그 시점에는 아직
            // 아래 줄(_blackboard.Machine = _machine)이 실행되기 전이라 blackboard.Machine이 null이다.
            // 현재 IdleState.Enter()는 Machine을 참조하지 않으므로 Phase 1에서는 문제가 없지만,
            // Phase 2 이후 어떤 상태의 Enter()가 Machine을 참조하게 되면 NullReferenceException이 난다.
            // StickmanStateMachine의 생성자 타이밍을 바꾸는 건 구조 변경이라 여기서 임의로 고치지 않았다.
            _machine = new StickmanStateMachine(states, StickmanStateId.Idle);
            _blackboard.Machine = _machine;
        }

        private void Start()
        {
            _platformService.CreateOverlayWindow();

            bool clickThroughDefault = _config != null ? _config.clickThroughDefaultEnabled : true;
            try
            {
                // 비침해 원칙 2: 클릭 관통 기본 ON — "앱 시작 시 SetClickThrough 호출 지점"은 여기로 고정한다.
                // 주의(BUG-B1, docs/BUG_REPORT_PHASE0.md Blocker): Win32WindowService는 아직 진짜
                // 분리된 오버레이 창이 없어(게임 자신의 창을 재사용하는 스텁), 안전 가드가
                // NotSupportedException을 던지도록 막아뒀다. 진짜 오버레이 HWND 구현 전까지 Windows
                // 에서는 아래 두 호출이 의도적으로 실패한다 — 버그가 아니라 "게임 창 자체가
                // 클릭관통/최상단 고정되는" 훨씬 나쁜 결과를 막기 위한 임시 안전장치다.
                _platformService.SetClickThrough(clickThroughDefault);
                _platformService.SetAlwaysOnTop(true);
            }
            catch (System.NotSupportedException ex)
            {
                Debug.LogWarning("[StickmanAgent] 클릭 관통/항상위 배선을 건너뜀 — 진짜 오버레이 창 구현 전까지 " +
                                  "안전 가드가 활성화되어 있습니다(BUG-B1 참고): " + ex.Message);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            TickFullscreenSuspend(dt);
            if (_isSuspended) return; // Suspended 동안 Tick 자체를 건너뛰어 상태/파라미터/물리를 그대로 보존.

            _footholdPoller.Tick(dt);

            // 입력은 여기서 프레임당 1회만 읽어 블랙보드에 스냅샷 — 각 상태가 개별적으로 Input을 폴링하지 않게 함.
            _blackboard.MoveInputX = Input.GetAxisRaw("Horizontal");
            _blackboard.JumpPressed = Input.GetButtonDown("Jump");

            _machine.Tick(dt);
        }

        private void TickFullscreenSuspend(float deltaTime)
        {
            _fullscreenPollTimer += deltaTime;
            float interval = _config != null ? Mathf.Max(0.1f, _config.fullscreenPollInterval) : 1f;
            if (_fullscreenPollTimer < interval) return;
            _fullscreenPollTimer = 0f;

            bool fullscreenActive = _platformService.IsFullscreenAppActive();
            if (fullscreenActive && !_isSuspended) Suspend();
            else if (!fullscreenActive && _isSuspended) Resume();
        }

        private void Suspend()
        {
            _isSuspended = true;
            // 상태/파라미터 보존(UX_FLOW.md 6-4절/9절-4, "IDLE 리셋 금지"): 상태 인스턴스를 파괴하거나
            // Idle로 되돌리지 않고 단순히 Tick 호출 자체를 건너뛴다 — 진행 중이던 상태의 내부 타이머
            // (예: FallState._landingConfirmTimer)가 그대로 멈춰 있다가 Resume() 이후 이어서 진행된다.
            if (_body != null) _body.simulated = false; // 물리 시뮬레이션도 함께 멈춰 숨겨진 동안 위치가 흐트러지지 않게 함.
            SetRenderersEnabled(false);
            // TODO(Phase 2 렌더링 레이어): 즉시 on/off 대신 ≤200ms 페이드 아웃/인 연출 추가.
        }

        private void Resume()
        {
            _isSuspended = false;
            if (_body != null) _body.simulated = true;
            SetRenderersEnabled(true);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = enabled;
            }
        }

        private IPlatformWindowService CreatePlatformService()
        {
#if UNITY_STANDALONE_WIN
            return new Win32WindowService();
#elif UNITY_IOS || UNITY_ANDROID
            // 모바일 발판/배경 설정 자체(SetBackdropScreenshot/AddUserDefinedFoothold)는 UX 온보딩
            // 흐름이 별도로 호출한다(docs/UX_FLOW.md 1-B/3절) — 여기서는 서비스 인스턴스만 만들어 배선한다.
            return new ScreenshotBackdropPlatformService();
#else
            // 에디터 및 macOS(네이티브 플러그인 미구현, Platform/MacOS/.gitkeep만 존재) 폴백.
            // macOS 실구현은 Phase 0 버그 리포트(BUG_REPORT_PHASE0.md m8)에 커버리지 공백으로 이미
            // 기록된 대로 별도 Objective-C++ 플러그인 작업이 필요하며 Phase 1 범위 밖이다.
            return new NullPlatformWindowService();
#endif
        }
    }
}
