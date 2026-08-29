using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 스틱맨 상태 전이를 관장하는 상태머신 골격.
    ///
    /// 전이 규칙 (주석 — Phase 1/2에서 각 상태 Tick()에 실제로 구현됨):
    /// - Idle  <-> Walk           : 이동 입력 유무.
    /// - Idle/Walk -> Jump        : 점프 입력 + (접지 또는 코요테 타임 이내)일 때.
    ///       코요테 타임(StickConfig.coyoteTimeDuration)은 발판을 막 벗어난 직후 짧은 유예 구간에서도
    ///       점프를 허용하는 의도된 사양이다(Architect 결정, 2026-08-27, docs/BUG_REPORT_PHASE1.md
    ///       BUG-P1-M5 — 캐주얼 데스크톱 토이라 관대한 조작감이 낫다는 판단). 발판 이탈→Fall 강제 전이
    ///       유예에 쓰이는 StickConfig.fallGraceDuration과 값은 같아도 되지만 개념적으로 분리된 필드다.
    /// - Jump -> Fall             : 상승 속도가 0 이하로 바뀌는 시점(정점 통과).
    /// - Fall -> LandingCrouch    : 발판 착지 감지 + 낙하 높이 >= StickConfig.rollLandingHeightThreshold.
    ///       "무릎앉아 착지"(사용자 명시 요청 2026-08-29) 연출 상태. StickConfig.landingCrouchEnabled를
    ///       끄면 이 갈래가 사라지고 아래 Fall -> Idle/Walk만 남는다(예전 거동과 100% 동일).
    /// - Fall -> Idle/Walk        : 발판 착지 감지 (StickConfig.fallGraceDuration 유예 적용).
    ///       낙하 높이가 위 임계값 미만일 때의 갈래다(예: macOS Dock 단차 0.855유닛).
    /// - LandingCrouch -> Idle/Walk : 무릎앉아 진행 곡선 완료 시(이동 의도 유무로 분기).
    ///       도중 접지를 잃으면(창이 닫히거나 움직임) Fall로, 외력 임계값 초과 시 Ragdoll로 빠진다.
    /// - Idle/Walk/Jump/Fall/ParkourClimb/Attack -> Ragdoll :
    ///       외력(피격/투척/충격량 크기)이 StickConfig.ragdollForceThreshold 이상이면
    ///       "능동 상태가 무엇이든" 즉시 강제 인터럽트 전이. (진행 중이던 Attack/ParkourClimb도 취소됨)
    /// - Ragdoll -> Getup         : 전신 Rigidbody2D의 속도 크기가 StickConfig.ragdollSettleSpeedThreshold
    ///       이하로 StickConfig.ragdollSettleHoldDuration초 이상 지속될 때 자동 전이.
    ///       (아직 감속 중이거나 순간적으로만 느려진 경우는 Ragdoll 유지 — 오탐 방지)
    /// - Getup -> Idle            : 기상 모션(널브러진 포즈 -> 직립 포즈 IK 보간) 완료 시.
    ///       단, Getup 도중에도 새 외력이 임계값을 넘으면 즉시 Ragdoll로 재인터럽트될 수 있음.
    /// - Idle/Walk -> ParkourClimb: 벽/모서리 발판 근접(StickConfig.parkourDetectionRadius 이내) + 상승 입력.
    ///       정상 종료 시 Idle/Walk로 복귀, 도중 외력 임계값 초과 시 Ragdoll로 강제 인터럽트 가능.
    /// - Idle/Walk -> Attack      : 공격 입력. 모션 종료 시 직전 능동 상태로 복귀, 도중 Ragdoll 인터럽트 가능.
    ///
    /// 전이 취소와 DialogueIntent: ChangeState가 호출될 때마다 TransitionGeneration을 증가시킨다.
    /// 그 결과 직전 컨텍스트로 만들어진 DialogueIntent는 (StickmanEventBus.StateTransitioned를
    /// 구독하고 있다가) 자신의 세대가 더 이상 최신이 아님을 즉시 감지해 같은 프레임에 자동 만료된다.
    /// 예: Attack 시작 직후 같은 프레임에 강한 피격이 들어와 Ragdoll로 인터럽트되면, Attack이 만든
    /// "타아앗!" 대사는 화면에 그려지기도 전에 만료 처리된다.
    /// </summary>
    public sealed class StickmanStateMachine
    {
        private readonly Dictionary<StickmanStateId, IStickmanState> _states;
        private IStickmanState _current;
        private int _transitionGeneration;

        public StickmanStateId CurrentStateId => _current != null ? _current.StateId : default;

        /// <summary>
        /// DialogueIntent.IsValid가 참조하는 "현재 유효한 전이 세대" 값.
        /// BUG-M1 대응(2026-08-27, Debugger/Architect 권고): public이면 Enter() 밖에서도 이 값을 읽어
        /// "현재 세대와 정확히 일치하는" 위조 컨텍스트를 만들 수 있었다. internal로 좁혀 DialogueIntent
        /// (같은 어셈블리)만 읽을 수 있게 한다 — IStickmanState.cs의 StateTransitionContext internal화와
        /// 짝을 이루는 조치. 완전한 방어는 아님(같은 어셈블리 내부는 여전히 접근 가능) — 위 로그 참고.
        /// </summary>
        internal int CurrentTransitionGeneration => _transitionGeneration;

        /// <summary>
        /// 지금 활성화된 상태 인스턴스 그 자체(BUG-M7 대응, docs/BUG_REPORT_PHASE0.md). DialogueIntent가
        /// 파라미터 기반 텍스트를 파생시킬 때, 호출자가 임의로 넘긴 객체가 아니라 "실제로 지금 Enter()가
        /// 실행 중인 그 상태 인스턴스"에서 직접 IHasDialogueParams.DialogueParams를 읽어오기 위해
        /// 노출한다 — ChangeState()가 Enter() 호출 전에 이미 _current를 새 상태로 교체해두므로, Enter()
        /// 구현부 안에서 이 프로퍼티를 읽으면 항상 "지금 확정된 바로 그 상태"를 가리킨다. internal로 좁혀
        /// 같은 어셈블리(DialogueIntent)만 접근 가능하게 한다 — CurrentTransitionGeneration과 동일한
        /// 캡슐화 수준.
        /// </summary>
        internal IStickmanState CurrentState => _current;

        /// <summary>
        /// 생성만 하고 아직 어떤 상태도 활성화하지 않는다(Enter()가 호출되지 않음).
        /// BUG-P1-M2 대응(Major, docs/BUG_REPORT_PHASE1.md, Architect 권고): 이전에는 생성자가 즉시
        /// ChangeState(initialState)를 호출해, 호출자가 StickmanBlackboard.Machine을 아직 배선하기
        /// 전(Awake() 중간)에 초기 상태의 Enter()가 실행되는 타이밍 문제가 있었다(우연히 Idle.Enter()가
        /// Machine을 안 써서 지금까지는 안전했을 뿐). 생성과 "최초 상태 활성화"를 분리해, 호출자가 모든
        /// 의존성(특히 blackboard.Machine)을 완전히 배선한 뒤 <see cref="Start"/>를 호출하게 강제함으로써
        /// "초기 상태의 Enter()가 무엇을 참조하든 Machine이 null일 수 있는" 경우의 수 자체를 구조적으로
        /// 없앤다.
        /// </summary>
        public StickmanStateMachine(Dictionary<StickmanStateId, IStickmanState> states)
        {
            _states = states;
            _current = null; // 아직 어떤 상태도 활성화되지 않음 — Start() 호출 전까지 Tick()도 no-op.
        }

        /// <summary>
        /// 블랙보드 등 모든 의존성이 완전히 배선된 뒤 1회 호출한다. 이 호출 시점부터 초기 상태의
        /// Enter()가 실행된다(BUG-P1-M2). 두 번째 호출은 상태머신을 재초기화하지 않고 에러 로그만 남긴다
        /// (일반 상태 전이는 ChangeState()를 사용할 것 — Start()는 최초 1회 전용).
        /// </summary>
        public void Start(StickmanStateId initialState)
        {
            if (_current != null)
            {
                Debug.LogError("[StickmanStateMachine] Start()가 이미 호출된 상태머신에 다시 호출되었습니다. 무시합니다.");
                return;
            }
            ChangeState(initialState);
        }

        /// <summary>
        /// 상태 전이를 "확정"한다. 이 메서드가 반환되는 시점에는 이미 새 상태의 Enter()가 호출된 뒤이며,
        /// StickmanEventBus.StateTransitioned도 발생한 뒤이다 — 즉 이 메서드 호출 = 전이 확정.
        /// </summary>
        /// <param name="isForcedInterrupt">
        /// 진행 중이던 상태가 완료되기 전에 상위 우선순위 상태(대표적으로 Ragdoll)가 끼어드는
        /// "강제 인터럽트" 전이라면 true. 예: ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true).
        /// 일반적인 자연 완료 전이(예: Attack 애니메이션 종료 -> Idle)는 기본값 false를 사용한다.
        /// DialogueIntent의 즉시 만료 자체는 이 값과 무관하게 항상 발생하지만(TransitionGeneration
        /// 불일치로 판정), UI 레이어가 "정상 종료 페이드아웃 vs 강제 취소 즉시 제거"(UX_FLOW.md 5절)를
        /// 구분해 연출하려면 이 플래그가 필요하다.
        /// </param>
        public void ChangeState(StickmanStateId next, bool isForcedInterrupt = false)
        {
            // BUG-M2 대응(2026-08-27, Debugger/Architect 권고): 원래 코드는 _states[next] 조회를
            // Exit()/세대 증가 "이후"에 수행해, next가 미등록 키면 이미 Exit()된 옛 상태를 _current가
            // 계속 가리키는 "좀비" 상태로 고착되고 복구 경로가 없었다(KeyNotFoundException 발생 시점에
            // 이미 뮤테이션이 절반 진행된 상태). 뮤테이션(Exit 호출/세대 증가) 이전에 next의 존재를
            // 먼저 검증해, 실패 시 현재 상태를 그대로 유지한 채 안전하게 반환한다.
            if (!_states.TryGetValue(next, out IStickmanState nextState))
            {
                Debug.LogError($"[StickmanStateMachine] ChangeState({next}) 실패 — 등록되지 않은 상태 ID. " +
                                $"현재 상태({CurrentStateId})를 그대로 유지합니다.");
                return;
            }

            StickmanStateId from = CurrentStateId;

            _current?.Exit();
            _transitionGeneration++; // 직전 전이로 만들어진 모든 DialogueIntent를 이 시점부로 구세대 취급

            var context = new StateTransitionContext(from, next, Time.frameCount, _transitionGeneration, this);
            _current = nextState;
            _current.Enter(context);

            StickmanEventBus.RaiseStateTransitioned(from, next, isForcedInterrupt);
        }

        public void Tick(float deltaTime) => _current?.Tick(deltaTime);
    }
}
