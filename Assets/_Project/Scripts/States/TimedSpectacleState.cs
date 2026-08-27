using System;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// docs/UX_FLOW.md 27절(그라피티/청소부/블랙홀/크래시 스윙) 공용 "순수 연출, 물리/입력 변경 없음"
    /// 상태 — 정해진 시간(초) 동안 머물다가 정상 종료로 Idle 복귀하는 것 외에는 아무것도 하지 않는다.
    /// 실제 모션/파티클/오버레이 스프라이트는 전부 Phase2+ 렌더링 레이어가 Interaction/*Director가 발행하는
    /// 전용 이벤트(WindowTheftOverlayChanged 등)를 구독해 담당한다 — 이 상태 자신은 "지금 이 스펙터클이
    /// 확정되어 진행 중"이라는 사실과 그 지속 시간만 표현한다.
    ///
    /// 왜 여러 기능이 이 하나의 클래스를 공유하는가: DragThrowState(Kinematic 전환)/RagdollState(전신
    /// 물리 위임)처럼 상태별로 실제 물리/입력 로직이 다른 경우와 달리, 그라피티/청소부/블랙홀/크래시
    /// 스윙은 전부 "캐릭터 쪽 부수 효과가 전혀 없는 순수 타이머"라는 동일한 형태다. 4개(사실상 5개 —
    /// StickmanStateId별 인스턴스는 별도) 상태 클래스를 거의 동일한 코드로 중복 작성하지 않기 위해
    /// 지속 시간(초)을 생성자 인자로 받는 하나의 재사용 가능한 구현으로 통합한다.
    ///
    /// 취소(전체화면 감지/유저 실제 조작 감지 등)는 이 클래스의 책임이 아니다 — 각 Director가
    /// StickmanStateMachine.ChangeState(Idle, isForcedInterrupt: true)를 직접 호출해 강제 종료시킨다
    /// (BattleMinigameDirector/DragThrowController의 ReleaseOwnedLocks() 관행과 동일).
    /// </summary>
    public sealed class TimedSpectacleState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;
        private readonly StickmanStateId _stateId;
        private readonly Func<StickConfig, float> _durationSecondsSelector;

        private float _timer;

        public TimedSpectacleState(StickmanBlackboard blackboard, StickmanStateId stateId, Func<StickConfig, float> durationSecondsSelector)
        {
            _blackboard = blackboard;
            _stateId = stateId;
            _durationSecondsSelector = durationSecondsSelector;
        }

        public StickmanStateId StateId => _stateId;

        public void Enter(StateTransitionContext context)
        {
            _timer = 0f;
        }

        public void Tick(float deltaTime)
        {
            _timer += deltaTime;
            float duration = _durationSecondsSelector != null && _blackboard.Config != null
                ? _durationSecondsSelector(_blackboard.Config)
                : 1f;

            if (_timer >= duration)
            {
                // 정상 완료 — 강제 인터럽트가 아니므로 isForcedInterrupt 기본값(false) 그대로.
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
            }
        }

        public void Exit() { }
    }
}
