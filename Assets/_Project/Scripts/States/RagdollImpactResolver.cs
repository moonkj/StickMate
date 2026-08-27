using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// RAGDOLL 강제 전이 판정("충격량 &gt;= ragdollForceThreshold면 Ragdoll로, 아니면 그대로")을 한
    /// 곳에 모은 순수 함수 헬퍼(부작용은 blackboard.Machine.ChangeState 호출 하나뿐).
    ///
    /// 왜 필요한가(Phase 3): Core/StickmanAgent.ReportExternalImpact()가 원래 이 판정의 유일한
    /// 진입점이었지만(Phase 2, "단일 진입점" 설계), 그 메서드는 MonoBehaviour 인스턴스 메서드라 블랙보드만
    /// 가진 순수 상태/컨트롤러 클래스(States/DragThrowState.cs — 던진 속도로부터 계산한 충격량,
    /// States/RodeoCursorState.cs — 거친 흔들기로 튕겨 떨어질 때, Interaction/RivalStickmanAgent.cs —
    /// 라이벌 자신이 맞았을 때)에서 직접 호출할 수 없다(참조 대상이 다름). 이 정적 유틸로 로직을 분리해
    /// 세 곳 이상에서 같은 판정식이 어긋나지 않게 한다 — StickmanAgent.ReportExternalImpact()도 내부적으로
    /// 이 메서드를 호출하도록 리팩터했다(공개 시그니처는 그대로, 내부 구현만 위임).
    /// </summary>
    public static class RagdollImpactResolver
    {
        /// <returns>임계값 이상이라 Ragdoll로 전이시켰으면 true, 미만이라 아무 것도 하지 않았으면 false.</returns>
        public static bool TryApplyImpact(StickmanBlackboard blackboard, float impulseMagnitude)
        {
            if (blackboard == null || blackboard.Machine == null || blackboard.Config == null) return false;
            if (impulseMagnitude < blackboard.Config.ragdollForceThreshold) return false;

            // UX_FLOW.md 31-2 #2 대비 스냅샷 — RagdollState.Enter()가 이 값을 IHasDialogueParams로
            // 노출해 "윽.../으악!/으아아아악?!" 같은 충격 강도별 대사를 파생시킨다(31-1 원칙).
            blackboard.LastImpactMagnitude = impulseMagnitude;
            blackboard.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            return true;
        }
    }
}
