using System;
using StickMate.Core;
using StickMate.Dialogue;

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
    /// (DragThrowController의 ReleaseOwnedLocks() 관행과 동일).
    ///
    /// [Phase 5 일반화, docs/UX_FLOW.md 17/18/19절] 선택적 4번째 생성자 인자(dialogueTextSelector)를
    /// 추가했다 — 투두 리마인더("확정된 할일 텍스트")/포모도로 시작-종료-넛지("좋아, 감시 시작"/
    /// "수고했어!"/"그래 쉬자"/"어? 딴 데 보고 있네?")/SULKY("아 몰라...")는 전부 이 클래스와 동일한
    /// "순수 타이머" 형태이면서 원칙 1(대사는 확정된 Enter()에서만 파생)을 지키는 고정 대사 1회만
    /// 추가로 필요했다. null(기본값)이면 기존 4개 등록(그라피티/청소부/블랙홀/크래시)처럼 대사를 전혀
    /// 만들지 않아 하위 호환된다 — 기존 호출부는 무수정.
    /// </summary>
    public sealed class TimedSpectacleState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;
        private readonly StickmanStateId _stateId;
        private readonly Func<StickConfig, float> _durationSecondsSelector;
        private readonly Func<StickConfig, string> _dialogueTextSelector;

        private float _timer;

        public TimedSpectacleState(StickmanBlackboard blackboard, StickmanStateId stateId,
            Func<StickConfig, float> durationSecondsSelector, Func<StickConfig, string> dialogueTextSelector = null)
        {
            _blackboard = blackboard;
            _stateId = stateId;
            _durationSecondsSelector = durationSecondsSelector;
            _dialogueTextSelector = dialogueTextSelector;
        }

        public StickmanStateId StateId => _stateId;

        public void Enter(StateTransitionContext context)
        {
            _timer = 0f;

            if (_dialogueTextSelector == null) return;
            string text = _dialogueTextSelector(_blackboard.Config);
            // 비어있으면 대사를 만들지 않는다(예: 투두 리마인더가 소비할 대기 중인 텍스트가 없는
            // 방어적 엣지 케이스 — 트리거 측이 미리 개수를 확인하므로 정상 경로에서는 발생하지 않는다).
            if (!string.IsNullOrEmpty(text))
            {
                // 종류=Reaction: 알림성 대사(투두/포모도로 등)라 상태가 끝나도 사실이 유지된다.
                _ = new DialogueIntent(context, id => DialogueLine.React(text));
            }
        }

        public void Tick(float deltaTime)
        {
            // ★ 2026-08-29 — "물리/입력 변경 없음"이라는 클래스 설계와는 별개로, 접지 유지만은
            // Idle/Walk와 동일하게 챙겨야 한다. 이 상태들이 서는 발판(Dock/타 앱 창 상단)은 논리
            // 발판이라 물리 콜라이더가 없다 — GroundedTick()을 안 부르면 이 상태에 머무는 동안
            // 중력만 계속 누적돼, 실제로는 화면 최하단 물리 바닥까지 자유낙하한다. 창 도둑을
            // Dock/창 위에서 발동하면 0.5초 만에 랙돌로 강제 취소되는 회귀로 실측 확인됐다
            // (착지 충격량이 랙돌 임계를 넘음). 이 클래스를 공유하는 그라피티/청소부/블랙홀/크래시/
            // 투두 리마인더/포모도로/SULKY 전부 같은 결함을 안고 있었다.
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

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
