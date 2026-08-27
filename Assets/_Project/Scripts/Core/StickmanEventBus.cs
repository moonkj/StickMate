using System;
using StickMate.Dialogue;

namespace StickMate.Core
{
    /// <summary>스틱맨의 상태 식별자. Core에 두는 이유: States/Dialogue/Plugins 등 상위 레이어가
    /// 모두 이 값을 공유해야 하는데, Core는 프로젝트의 최하위 레이어라 순환 참조가 생기지 않는다.</summary>
    public enum StickmanStateId
    {
        Idle,
        Walk,
        Jump,
        Fall,
        ParkourClimb,
        Attack,
        Ragdoll,
        Getup,
    }

    /// <summary>
    /// docs/UX_FLOW.md 26-3절 "살아있는 느낌" 디테일 — AutoWanderController가 타이밍/확률 조건만 판정해
    /// 발행하는 유휴 연출 신호. 실제 애니메이션 재생은 Phase 2+ 렌더링 레이어가 이 이벤트를 구독해 담당한다
    /// (지금은 아무도 구독하지 않아도 무해 — 트리거 조건 계산 자체는 지금 확정해두는 것이 목적).
    /// </summary>
    public enum WanderAmbientMotion
    {
        /// <summary>Idle 진입 후 1.0~2.5초 랜덤 지연 뒤 발동, 0.6~1.0초 지속(26-3).</summary>
        LookAround,

        /// <summary>"Idle 연장"이 연속 3회 이상 선택된 경우에만 15% 확률로 발동, 1.5~2.5초 지속(26-3).</summary>
        SitAndYawn,
    }

    /// <summary>상태 전이 1건을 나타내는 불변 이벤트 페이로드 (From -> To).</summary>
    public readonly struct StateTransitionEvent
    {
        public readonly StickmanStateId From;
        public readonly StickmanStateId To;

        /// <summary>
        /// 이 전이가 "일반 전이"(자연스러운 완료/입력에 의한 전이)가 아니라 진행 중이던 상태를
        /// 중도에 인터럽트한 "강제 전이"(예: RAGDOLL 피격)인지 여부.
        /// UX_FLOW.md 5절/9절-2 요구사항: 말풍선 UI가 "정상 종료 시 최소 노출시간 보장 후 페이드아웃"과
        /// "강제 취소 시 최소 노출시간 무시하고 즉시 제거"를 구분할 수 있어야 하므로, 그 판단 근거를
        /// DialogueIntent의 IsValid(같은 프레임 만료) 메커니즘과는 별개로 이벤트에 함께 실어 보낸다.
        /// </summary>
        public readonly bool IsForcedInterrupt;

        public StateTransitionEvent(StickmanStateId from, StickmanStateId to, bool isForcedInterrupt)
        {
            From = from;
            To = to;
            IsForcedInterrupt = isForcedInterrupt;
        }
    }

    /// <summary>
    /// 상태 전이 / 발판 변경 / 대사 요청을 알리는 경량 이벤트 버스.
    ///
    /// 왜 C# event 기반 "정적" 클래스인가:
    /// 1) 레이어 간 결합도 최소화 — 입력/렌더/네이티브(P/Invoke)/AI/UI 레이어가 서로의 구체 타입을
    ///    몰라도 통신할 수 있다. 상태머신은 "누가 듣는지" 모른 채 RaiseStateTransitioned만 호출하면 된다.
    /// 2) MonoBehaviour 싱글톤 대신 순수 C# 정적 클래스를 쓰는 이유 — 이 앱은 24시간 상주하는
    ///    백그라운드형 앱이라 씬 전환/오브젝트 파괴 생명주기와 이벤트 구독을 분리해두는 편이
    ///    Destroy 타이밍에 따른 구독 유실 문제를 피하기 쉽다.
    /// 3) 정적 event 자체는 GC 압박이 없다 — 구독/해제(+=/-=)만 규율 있게 지키면 된다.
    ///    특히 MonoBehaviour 구독자는 반드시 OnDisable/OnDestroy에서 -= 로 해제해야 메모리 누수가 없다
    ///    (정적 이벤트가 파괴된 인스턴스를 계속 붙들고 있는 전형적인 Unity 버그 패턴 방지).
    /// 4) "매 프레임 폴링" 대신 "확정된 사건"만 통지 → 구독자들이 Update()에서 매 프레임 상태 비교를
    ///    반복할 필요가 없어져 할당/CPU를 아낄 수 있다 (24시간 상주 앱 성능 컨벤션과 직결).
    /// </summary>
    public static class StickmanEventBus
    {
        /// <summary>상태 전이가 "확정"된 프레임에 발생. DialogueIntent의 자동 만료 감지도 이 이벤트를 구독한다.</summary>
        public static event Action<StateTransitionEvent> StateTransitioned;

        /// <summary>발판 목록이 바뀌었을 때(창 열림/닫힘, 모바일 유저 지정 추가/삭제 등) 발생.</summary>
        public static event Action FootholdsChanged;

        /// <summary>새 DialogueIntent가 생성되었을 때 발생 — UI 레이어는 이 이벤트만 구독해 말풍선을 띄운다.</summary>
        public static event Action<DialogueIntent> DialogueRequested;

        /// <summary>DialogueIntent가 만료(상태 재전이로 무효화)되었을 때 발생 — UI 레이어는 말풍선을 즉시 숨긴다.</summary>
        public static event Action<DialogueIntent> DialogueExpired;

        /// <summary>
        /// 인질극/로데오 커서 등 방해성 이벤트를 상태머신의 일반 전이 규칙과 완전히 무관하게 즉시
        /// 중단시키기 위한 전역 긴급 정지 채널 (UX_FLOW.md 6-5절/9절-6). 발행자는 Phase 3/4의
        /// 트레이/메뉴바 상시 노출 버튼이며, 구독자는 방해성 이벤트를 구현하는 상태들이다.
        /// Phase 0에서는 아직 아무도 발행/구독하지 않지만, "지금 인터페이스에 자리를 예약해둘 것"이라는
        /// 교차 레이어 요구사항에 따라 이벤트 슬롯만 미리 확보해둔다.
        /// </summary>
        public static event Action GlobalEmergencyStopRequested;

        /// <summary>AutoWanderController가 두리번거리기/앉기·하품 트리거 조건을 판정했을 때 발생(UX_FLOW.md 26-3절).</summary>
        public static event Action<WanderAmbientMotion> WanderAmbientMotionRequested;

        /// <summary>
        /// FallState가 착지를 확정한 순간, 낙하 높이가 StickConfig.rollLandingHeightThreshold 이상이었을
        /// 때 발생(UX_FLOW.md 4절 "구르기(ROLL)"). 페이로드는 실제 낙하 높이(월드 유닛). 실제 구르기
        /// 파티클/애니메이션 재생은 Phase 2+ 렌더링 레이어가 이 이벤트를 구독해 담당한다 — 지금은 아무도
        /// 구독하지 않아도 무해(트리거 조건 계산 자체가 지금 확정해두는 목적).
        /// </summary>
        public static event Action<float> LandingRollRequested;

        public static void RaiseStateTransitioned(StickmanStateId from, StickmanStateId to, bool isForcedInterrupt = false)
            => StateTransitioned?.Invoke(new StateTransitionEvent(from, to, isForcedInterrupt));

        public static void RaiseFootholdsChanged()
            => FootholdsChanged?.Invoke();

        public static void RaiseDialogueRequested(DialogueIntent intent)
            => DialogueRequested?.Invoke(intent);

        public static void RaiseDialogueExpired(DialogueIntent intent)
            => DialogueExpired?.Invoke(intent);

        public static void RaiseGlobalEmergencyStop()
            => GlobalEmergencyStopRequested?.Invoke();

        public static void RaiseWanderAmbientMotionRequested(WanderAmbientMotion motion)
            => WanderAmbientMotionRequested?.Invoke(motion);

        public static void RaiseLandingRollRequested(float fallHeight)
            => LandingRollRequested?.Invoke(fallHeight);
    }
}
