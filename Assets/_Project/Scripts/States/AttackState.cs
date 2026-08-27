using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 공격 모션 재생(격파 미니게임/전투 로직과 연동, Phase 3).
    /// 진입: Idle/Walk 중 공격 입력.
    /// 전이: 모션 정상 종료 -> 진입 직전 능동 상태로 복귀 / 외력 임계값 초과 -> Ragdoll(강제 인터럽트,
    ///       예: 공격 모션 재생 도중 라이벌에게 선타를 맞음 — 이 경우 Attack이 만든 DialogueIntent는
    ///       TransitionGeneration 불일치로 같은 프레임에 자동 만료된다).
    ///
    /// BUG-M7 파라미터 파이프라인 시연(docs/BUG_REPORT_PHASE0.md, Phase 2 착수 시점 필수 재설계 항목):
    /// 실제 전투 로직(공격 큐/콤보 판정, 히트박스 활성화 등)은 여전히 Phase 3 범위이지만, "상태가 자신의
    /// 파라미터를 구조적으로 노출 -> DialogueIntent가 그 파라미터로부터 텍스트를 파생"하는 파이프라인
    /// 자체는 지금 검증해야 한다는 요구사항에 따라 IHasDialogueParams를 구현해 "남은 타격 횟수"를
    /// 시연용으로 연결한다.
    /// </summary>
    public sealed class AttackState : IStickmanState, IHasDialogueParams
    {
        public StickmanStateId StateId => StickmanStateId.Attack;

        /// <summary>
        /// 이 상태가 대사 매핑 함수에 구조적으로 노출하는 파라미터(BUG-M7). 실제 전투 시스템이 아직
        /// 없으므로 지금은 데모용 고정값(DemoShotsRemaining)으로 채우지만, DialogueIntent가 이 값을
        /// "호출자가 자유롭게 지어낸 문자열"이 아니라 상태 인스턴스에서 직접 읽어간다는 파이프라인
        /// 자체는 실제 전투 로직으로 교체되어도 그대로 유지된다.
        /// </summary>
        public sealed class AttackDialogueParams
        {
            public int ShotsRemaining;
        }

        // TODO(Phase 3): 실제 공격 큐/콤보 카운트로 대체. 지금은 파라미터 파이프라인 시연용 고정값.
        private const int DemoShotsRemaining = 1;

        private readonly AttackDialogueParams _dialogueParams = new AttackDialogueParams();

        public object DialogueParams => _dialogueParams;

        public void Enter(StateTransitionContext context)
        {
            _dialogueParams.ShotsRemaining = DemoShotsRemaining;

            // BUG-M7 대응 시연: 텍스트가 "한 발 더!/타앗!"으로 파생되되, 그 근거(ShotsRemaining)가 이
            // 상태 인스턴스(this)에서 구조적으로 노출된 값이라는 점이 핵심 — 하드코딩된 문자열이 상태
            // 파라미터와 따로 놀 수 없다. 실제 히트박스 활성화/공격 큐 소비는 Phase 3에서 채워진다.
            _ = new DialogueIntent(context, (id, dialogueParams) =>
            {
                var p = dialogueParams as AttackDialogueParams;
                int remaining = p != null ? p.ShotsRemaining : 0;
                return remaining > 0 ? $"{remaining}발 더!" : "타앗!";
            });

            // TODO(Phase 3): 공격 모션 재생 시작, 히트박스 활성화 타이밍 예약.
        }

        public void Tick(float deltaTime)
        {
            // TODO(Phase 3): 모션 진행도 추적, 완료 시 StateMachine.ChangeState(진입 직전 능동 상태)로 복귀.
        }

        public void Exit() { }
    }
}
