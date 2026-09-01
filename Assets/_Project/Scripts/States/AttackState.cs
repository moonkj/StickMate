using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 공격 모션 재생(전투 로직과 연동, Phase 3).
    /// 진입: Idle/Walk 중 공격 입력.
    /// ★ 2026-08-30 — 유일한 호출자였던 컴포넌트가 함께 삭제되어 <b>런타임 생산자가 0개</b>가 됐다.
    ///   상태 자체는 CLAUDE.md가 명시한 능동 상태 5종의 하나라
    ///   상태머신에 계속 등록해 두지만, 다시 누군가 ChangeState(Attack)를 부르기 전까지는 재생되지
    ///   않는다 — 신규 기능이 이 상태를 쓰려면 AttackShotsRemaining을 채우고 호출하면 된다.
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
        /// 이 상태가 대사 매핑 함수에 구조적으로 노출하는 파라미터(BUG-M7). 값의 출처는
        /// StickmanBlackboard.AttackShotsRemaining(docs/BUG_REPORT_PHASE3.md Minor 1 대응) — 호출자가
        /// ChangeState(Attack) 직전에 채워두는 스냅샷 입력이다. Phase 5 이전엔 콤보/탄약을 추적하는 진짜
        /// 전투 큐가 없으므로 "이번 타격이 결정타인지" 정도의 신호로만 쓴다 — 지금은 채우는 호출자가
        /// 없어 항상 기본값 0("오늘은 여기까지")이다.
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

            // BUG-P3-M1(Minor 1) 대응: 호출자가 ChangeState(Attack) 직전에 채워둔 스냅샷을 그대로
            // 읽는다(StickmanBlackboard.AttackShotsRemaining 문서 참고) — 값을 채우지 않은 호출부는
            // 기본값 0("오늘은 여기까지")을 그대로 받는다.
            _dialogueParams.ShotsRemaining = _blackboard.AttackShotsRemaining;

            // BUG-M7 대응: 텍스트가 "한 발 더!/오늘은 여기까지"로 파생되되, 그 근거(ShotsRemaining)가
            // 이 상태 인스턴스에서 구조적으로 노출된 값이라는 점이 핵심(UX_FLOW.md 31-2 표 #1 리터럴 그대로).
            // 종류=Reaction: 확정된 점 사건("이번 타격이 어떤 타격이었나")의 서술이라 상태 종료 후에도 참.
            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as AttackDialogueParams;
                int remaining = p != null ? p.ShotsRemaining : 0;
                return remaining >= 1 ? DialogueLine.React("한 발 더!") : DialogueLine.React("오늘은 여기까지");
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
