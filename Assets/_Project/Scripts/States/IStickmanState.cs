using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 상태 전이가 "확정된 그 프레임"에만 존재 의미를 갖는 불변 컨텍스트.
    ///
    /// DialogueIntent 생성자가 이 타입(정확히는 이 안의 OriginMachine)을 요구함으로써, 말풍선이
    /// 상태보다 먼저 결정되는 순서 오염을 구조적으로 막는다 (절대 불변 원칙 1: 행동-텍스트 싱크).
    /// 오직 StickmanStateMachine.ChangeState()가 전이를 확정하는 순간에만 새로 발급되고,
    /// 발급 즉시 해당 상태의 Enter(context)로 전달된다 — 즉 DialogueIntent는 사실상
    /// IStickmanState.Enter() 구현부 안에서만 만들어질 수 있다.
    ///
    /// 알려진 한계(Debugger 리뷰, docs/BUG_REPORT_PHASE0.md BUG-M1): 애초 우려했던 default(StateTransitionContext)
    /// 우회보다 실제 위험 범위가 더 넓었다 — 생성자와 필드가 모두 public이면 Enter() 밖의 어떤 코드든
    /// "머신 참조 하나"만 있으면 TransitionGeneration이 현재 세대와 정확히 일치하는 "진짜처럼 통과하는"
    /// 컨텍스트를 위조할 수 있어, ArgumentException 가드(OriginMachine == null 검사)를 그대로 우회한다.
    /// 이는 원칙 1(행동-텍스트 싱크) 방어선을 실질적으로 무력화할 수 있는 경로였다.
    /// 2026-08-27 Coder 대응(BUG-M1 최소 비용안, Debugger/Architect 권고 반영): 생성자와 모든 필드를
    /// public에서 internal로 좁혔다 — 프로젝트에 별도 asmdef가 없어 States/Dialogue 네임스페이스가
    /// 전부 같은 기본 어셈블리(Assembly-CSharp)에서 컴파일되므로 기존 호출부(DialogueIntent 등)는
    /// 그대로 동작한다. 단, 이 조치는 "다른 어셈블리에서의 위조"만 막을 뿐 같은 어셈블리 내 임의 코드가
    /// internal 생성자를 호출하는 것까지는 막지 못하는 절반의 방어라는 한계를 Debugger가 이미 명시했다
    /// (Tasklist.md 교차 레이어 로그 BUG-M1 참고). 완전한 보증은 여전히 Phase 2의 "발급 1회용 토큰을
    /// 가진 sealed 클래스" 전환으로 미룬다.
    /// </summary>
    public readonly struct StateTransitionContext
    {
        internal readonly StickmanStateId From;
        internal readonly StickmanStateId To;

        /// <summary>전이가 확정된 프레임(Time.frameCount). 같은 프레임 내 취소 여부 디버깅에 사용.</summary>
        internal readonly int ConfirmedFrame;

        /// <summary>
        /// 이 전이가 속한 "세대" 번호. StickmanStateMachine은 ChangeState가 호출될 때마다 이 값을
        /// 증가시킨다. DialogueIntent는 생성 시점의 이 값을 스냅샷으로 들고 있다가, 머신의 현재
        /// 세대와 달라지는 순간(=다른 전이가 일어나 이 전이가 취소/추월됨) 자동으로 만료된다.
        /// </summary>
        internal readonly int TransitionGeneration;

        /// <summary>
        /// 이 컨텍스트를 발급한 상태머신. DialogueIntent의 IsValid 판정 대상이자, "이 컨텍스트가
        /// 실제로 ChangeState()를 통해 발급되었는지"를 구분하는 최소한의 안전장치(null이면 가짜 컨텍스트).
        /// </summary>
        internal readonly StickmanStateMachine OriginMachine;

        internal StateTransitionContext(StickmanStateId from, StickmanStateId to, int confirmedFrame, int transitionGeneration, StickmanStateMachine originMachine)
        {
            From = from;
            To = to;
            ConfirmedFrame = confirmedFrame;
            TransitionGeneration = transitionGeneration;
            OriginMachine = originMachine;
        }
    }

    /// <summary>
    /// 스틱맨의 능동/피동 상태 하나를 표현하는 인터페이스.
    /// Enter에서 받는 StateTransitionContext가 곧 "이 전이가 확정되었다"는 증거이며,
    /// 상태 구현체는 이 컨텍스트를 그대로 DialogueIntent 생성자에 넘겨 그 상태로부터 파생된
    /// 대사만 만들 수 있다.
    /// </summary>
    public interface IStickmanState
    {
        /// <summary>이 상태의 식별자 (StickmanEventBus 이벤트 페이로드 등에 사용).</summary>
        StickmanStateId StateId { get; }

        /// <summary>상태머신이 이 상태로의 전이를 확정한 직후 1회 호출.</summary>
        void Enter(StateTransitionContext context);

        /// <summary>이 상태가 현재 상태인 동안 매 프레임(또는 매 물리 스텝) 호출.</summary>
        void Tick(float deltaTime);

        /// <summary>다른 상태로 전이되기 직전 1회 호출.</summary>
        void Exit();
    }
}
