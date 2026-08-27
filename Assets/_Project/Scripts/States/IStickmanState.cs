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
    /// 이력(BUG-M1, docs/BUG_REPORT_PHASE0.md): 원래는 readonly struct였다. 구조체는 항상
    /// default(StateTransitionContext)/new StateTransitionContext()로 "OriginMachine == null"인
    /// 가짜 인스턴스를 만들 수 있었고, 설령 생성자/필드를 internal로 좁혀도(1차 대응) "머신 참조 하나"만
    /// 있으면 TransitionGeneration이 현재 세대와 정확히 일치하는 "진짜처럼 통과하는" 컨텍스트를
    /// 위조해 같은 컨텍스트로 DialogueIntent를 여러 번(또는 Enter() 밖에서) 만들 수 있었다.
    /// 2026-08-27 Coder 대응(BUG-M1 Phase 2 완결, Debugger/Architect 합의사항 — "발급 1회용 토큰을 가진
    /// sealed 클래스"로 전환):
    ///   1) struct -> sealed class. 클래스는 명시적으로 선언한 생성자만 존재하므로(암묵적 매개변수 없는
    ///      public 생성자가 생기지 않음) default(StateTransitionContext)/new StateTransitionContext()류의
    ///      "공짜 위조"는 애초에 컴파일되지 않는다. 유일한 생성자는 여전히 internal이라 접근 범위도 좁다.
    ///      (기존 IStickmanState.Enter(StateTransitionContext context) 시그니처는 타입 이름이 그대로라
    ///      변경 없음 — 호출부/구현부 어디도 손댈 필요가 없다.)
    ///   2) 1회용 발급 토큰(<see cref="TryConsumeToken"/>) 추가. DialogueIntent 생성자가 텍스트를
    ///      만들기 직전에 이 토큰을 소비하며, 같은 컨텍스트로 두 번째 DialogueIntent를 만들려는 시도는
    ///      항상 실패한다 — "같은 전이 확정 시점을 재사용해 대사를 여러 번 위조"하는 경로를 원천 차단.
    /// 남은 한계(정직하게 문서화): 이 프로젝트에 asmdef 분리가 없어 States/Dialogue가 전부 같은
    /// 어셈블리(Assembly-CSharp)로 컴파일되므로, 같은 어셈블리 내부 코드가 internal 생성자를 직접
    /// 호출해 임의의 From/To/OriginMachine으로 컨텍스트를 새로 찍어내는 것 자체는 여전히 가능하다
    /// (컴파일러 수준에서 "발급자는 오직 ChangeState뿐"임을 강제하려면 asmdef 분리가 필요). 다만 이제는
    /// 그런 시도가 "같은 컨텍스트 재사용"이 아니라 "완전히 새로운 컨텍스트를 처음부터 조작"하는 훨씬
    /// 노골적인 코드가 되므로, 코드 리뷰로 걸러내기 쉬워졌다는 점에서 방어선이 실질적으로 강화되었다.
    /// </summary>
    public sealed class StateTransitionContext
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

        /// <summary>BUG-M1 Phase 2 완결 — 1회용 발급 토큰. TryConsumeToken()이 이미 한 번 성공적으로
        /// 소비했는지를 추적한다.</summary>
        private bool _tokenConsumed;

        internal StateTransitionContext(StickmanStateId from, StickmanStateId to, int confirmedFrame, int transitionGeneration, StickmanStateMachine originMachine)
        {
            From = from;
            To = to;
            ConfirmedFrame = confirmedFrame;
            TransitionGeneration = transitionGeneration;
            OriginMachine = originMachine;
        }

        /// <summary>
        /// 이 컨텍스트의 1회용 발급 토큰을 소비한다. 최초 호출만 true를 반환하고, 그 이후로는 항상
        /// false를 반환한다 — DialogueIntent 생성자가 이 메서드를 통해 "같은 전이 확정 시점(컨텍스트)으로
        /// DialogueIntent가 이미 만들어진 적이 있는지"를 확인해, 같은 컨텍스트로 대사를 두 번 만드는
        /// 경로를 구조적으로 막는다(BUG-M1 Phase 2 완결).
        /// </summary>
        internal bool TryConsumeToken()
        {
            if (_tokenConsumed) return false;
            _tokenConsumed = true;
            return true;
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
