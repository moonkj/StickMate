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
    /// 알려진 한계(디버거 리뷰 포인트): C# 구조체는 항상 암묵적 매개변수 없는 생성자를 가지므로
    /// default(StateTransitionContext)로 OriginMachine == null 인 "가짜" 컨텍스트를 만들 수는 있다.
    /// 이 가짜 컨텍스트로는 DialogueIntent 생성자가 즉시 예외를 던지도록 방어했지만(컴파일 타임이 아닌
    /// 런타임 가드), 완전한 캡슐화는 아니다. 더 강한 보증이 필요해지면 Phase 2에서 클래스 + 내부 전용
    /// 발급 토큰 방식으로 강화하는 것을 고려한다.
    /// </summary>
    public readonly struct StateTransitionContext
    {
        public readonly StickmanStateId From;
        public readonly StickmanStateId To;

        /// <summary>전이가 확정된 프레임(Time.frameCount). 같은 프레임 내 취소 여부 디버깅에 사용.</summary>
        public readonly int ConfirmedFrame;

        /// <summary>
        /// 이 전이가 속한 "세대" 번호. StickmanStateMachine은 ChangeState가 호출될 때마다 이 값을
        /// 증가시킨다. DialogueIntent는 생성 시점의 이 값을 스냅샷으로 들고 있다가, 머신의 현재
        /// 세대와 달라지는 순간(=다른 전이가 일어나 이 전이가 취소/추월됨) 자동으로 만료된다.
        /// </summary>
        public readonly int TransitionGeneration;

        /// <summary>
        /// 이 컨텍스트를 발급한 상태머신. DialogueIntent의 IsValid 판정 대상이자, "이 컨텍스트가
        /// 실제로 ChangeState()를 통해 발급되었는지"를 구분하는 최소한의 안전장치(null이면 가짜 컨텍스트).
        /// </summary>
        public readonly StickmanStateMachine OriginMachine;

        public StateTransitionContext(StickmanStateId from, StickmanStateId to, int confirmedFrame, int transitionGeneration, StickmanStateMachine originMachine)
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
