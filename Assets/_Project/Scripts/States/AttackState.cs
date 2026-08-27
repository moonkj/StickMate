using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 공격 모션 재생(전투 로직과 연동, Phase 3).
    /// 진입: Idle/Walk 중 공격 입력(Phase 3에서는 Interaction/RivalStickmanAgent.cs의 근접 판정이
    /// 유일한 실사용처 — 라이벌 스틱맨 대결에서 두 캐릭터가 번갈아 이 상태로 진입해 타격 연출을 보인다).
    /// 전이: 모션 정상 종료(StickConfig.attackDuration 경과) -> 진입 직전 능동 상태로 복귀 /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트, 예: 공격 모션 재생 도중 선타를 맞음 — 이 경우
    ///       Attack이 만든 DialogueIntent는 TransitionGeneration 불일치로 같은 프레임에 자동 만료된다).
    ///
    /// BUG-M7 파라미터 파이프라인 시연(docs/BUG_REPORT_PHASE0.md) + Phase 3 실전 연결: "상태가 자신의
    /// 파라미터를 구조적으로 노출 -> DialogueIntent가 그 파라미터로부터 텍스트를 파생"하는 파이프라인을
    /// IHasDialogueParams로 구현한다. 텍스트 리터럴은 UX_FLOW.md 31-2 표 #1과 정확히 일치시켰다
    /// (Debugger docs/BUG_REPORT_PHASE2.md Minor 1 권고 반영 — 예전 데모 텍스트 "{N}발 더!"/"타앗!"에서 교체).
    /// </summary>
    public sealed class AttackState : IStickmanState, IHasDialogueParams
    {
        public StickmanStateId StateId => StickmanStateId.Attack;

        /// <summary>
        /// 이 상태가 대사 매핑 함수에 구조적으로 노출하는 파라미터(BUG-M7). Phase 3 현재 유일한 사용처
        /// (라이벌 대결)는 매번 이 상태를 1회성 단발 타격으로만 쓰므로 항상 0(=마지막 타격)으로 채운다 —
        /// 콤보/탄약을 추적하는 실제 전투 큐가 생기면 그 값을 그대로 이 필드에 흘려보내면 되고, 파이프라인
        /// 자체는 이미 완성되어 있어 추가 배선이 필요 없다.
        /// </summary>
        public sealed class AttackDialogueParams
        {
            public int ShotsRemaining;
        }

        private readonly StickmanBlackboard _blackboard;
        private readonly AttackDialogueParams _dialogueParams = new AttackDialogueParams();

        private StickmanStateId _returnState;
        private float _elapsed;

        public object DialogueParams => _dialogueParams;

        public AttackState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            _elapsed = 0f;
            _returnState = ResolveReturnState(context.From);

            // TODO(Phase 3+ 전투 로직 확장): 실제 콤보/탄약 카운트가 생기면 여기서 채운다. 지금은
            // Interaction/RivalStickmanAgent.cs가 매번 단발 타격으로만 이 상태를 쓰므로 항상 0.
            _dialogueParams.ShotsRemaining = 0;

            // BUG-M7 대응: 텍스트가 "한 발 더!/오늘은 여기까지"로 파생되되, 그 근거(ShotsRemaining)가
            // 이 상태 인스턴스에서 구조적으로 노출된 값이라는 점이 핵심(UX_FLOW.md 31-2 표 #1 리터럴 그대로).
            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as AttackDialogueParams;
                int remaining = p != null ? p.ShotsRemaining : 0;
                return remaining >= 1 ? "한 발 더!" : "오늘은 여기까지";
            });
        }

        /// <summary>진입 직전 능동 상태로 복귀(문서화된 전이 규칙: Idle/Walk에서만 진입). 그 외 값이
        /// 들어오면(방어적) 항상 더 안전한 Idle로 복귀시킨다(UX_FLOW.md 4절 "애매하면 안전한 쪽" 원칙).</summary>
        private static StickmanStateId ResolveReturnState(StickmanStateId from)
        {
            return from == StickmanStateId.Idle || from == StickmanStateId.Walk ? from : StickmanStateId.Idle;
        }

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float duration = _blackboard.Config != null ? _blackboard.Config.attackDuration : 0.4f;
            if (_elapsed >= duration)
            {
                _blackboard.Machine.ChangeState(_returnState);
            }
        }

        public void Exit() { }
    }
}
