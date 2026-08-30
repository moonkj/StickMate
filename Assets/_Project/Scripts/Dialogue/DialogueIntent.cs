using System;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Dialogue
{
    /// <summary>
    /// 텍스트-액션 싱크 계약(절대 불변 원칙 1)의 핵심 타입.
    ///
    /// 설계 의도:
    /// 1) "말풍선을 먼저 정하고 행동을 끼워 맞추는" 순서 오염을 원천 차단하기 위해, 이 클래스는
    ///    매개변수 없는 생성자를 두지 않는다. 유일한 공개 생성자는 StateTransitionContext를 요구하며,
    ///    이 컨텍스트는 StickmanStateMachine.ChangeState()가 전이를 "확정"하는 순간에만 발급되어
    ///    해당 상태의 IStickmanState.Enter(context)로 전달된다 (States/IStickmanState.cs 참고).
    ///    즉 DialogueIntent는 사실상 어떤 상태의 Enter() 구현부 안에서만 만들어질 수 있다.
    /// 2) 대사 문자열 자체도 "상태 확정 이후" 시점에 그 상태(context.To)와 그 상태가 노출하는 파라미터
    ///    (IHasDialogueParams.DialogueParams, BUG-M7 대응)로부터만 파생되도록 강제하기 위해, 생성자가
    ///    완성된 문자열을 직접 받지 않고 Func&lt;StickmanStateId, object, string&gt;(상태+파라미터 ->
    ///    텍스트 매핑 함수)를 받아 그 자리에서 만들어낸다. 호출자가 상태와 무관한 임의 문자열/파라미터를
    ///    미리 준비해 끼워 넣는 경로 자체를 없앤다 — 파라미터는 항상 StickmanStateMachine.CurrentState
    ///    (=지금 Enter() 중인 그 상태 인스턴스)에서 직접 읽는다(아래 3.5 참고). 파라미터가 필요 없는
    ///    단순 상태(Idle 유휴 잡담 등)를 위해 Func&lt;StickmanStateId, string&gt;를 받는 편의 생성자도
    ///    유지한다.
    /// 3) "상태가 중도 취소되면 같은 프레임에 자동 만료": StickmanStateMachine은 ChangeState가
    ///    호출될 때마다 TransitionGeneration을 증가시킨다. DialogueIntent는 생성 시점의 세대를
    ///    스냅샷으로 보관하고, StickmanEventBus.StateTransitioned를 구독해 이후 발생하는 모든 전이
    ///    이벤트에서 "내 세대가 아직 최신인지"를 확인한다. 세대가 바뀌었다면(즉 자신을 만든 전이가
    ///    다른 전이로 추월/취소됨) 같은 프레임 안에서 즉시 만료 처리(IsValid = false)하고
    ///    DialogueExpired 이벤트를 발생시켜 UI 레이어가 말풍선을 즉시 숨기게 한다.
    /// 3.5) 파라미터 조회(BUG-M7): context.OriginMachine.CurrentState를 IHasDialogueParams로 캐스팅해
    ///    파라미터 객체를 얻는다. ChangeState()가 Enter() 호출 "전"에 이미 _current를 새 상태로
    ///    교체해두므로, 이 생성자가 실행되는 시점(=Enter() 구현부 안)에는 CurrentState가 항상 지금
    ///    확정된 바로 그 상태 인스턴스를 가리킨다 — 즉 호출자가 파라미터를 자유롭게 지어낼 수 없고,
    ///    실제 상태 객체가 구조적으로 노출한 값만 텍스트에 반영된다.
    /// 4) 외부(UI 등)는 StickmanEventBus.DialogueRequested/DialogueExpired 이벤트만 구독한다 —
    ///    상태머신이나 개별 상태 클래스를 직접 참조하지 않는다 (레이어 분리).
    /// 5) 1회용 발급 토큰(BUG-M1 Phase 2 완결, docs/BUG_REPORT_PHASE0.md): 생성자가
    ///    context.TryConsumeToken()을 호출해, 같은 StateTransitionContext로 DialogueIntent를 두 번
    ///    만드는 시도를 InvalidOperationException으로 막는다. StateTransitionContext 자체도
    ///    readonly struct에서 sealed class로 전환되어(States/IStickmanState.cs 참고)
    ///    default(...)/new StateTransitionContext() 같은 "공짜 위조"가 컴파일 단계에서부터 불가능하다.
    /// </summary>
    public sealed class DialogueIntent
    {
        /// <summary>이 전이가 확정된 상태로부터 파생된 대사 텍스트.</summary>
        public string Text { get; }

        /// <summary>이 대사가 파생된 상태.</summary>
        public StickmanStateId StateId { get; }

        /// <summary>생성(=전이 확정) 프레임. Time.frameCount 스냅샷.</summary>
        public int CreatedFrame { get; }

        private readonly int _transitionGeneration;
        private readonly StickmanStateMachine _originMachine;
        private bool _expired;

        /// <summary>
        /// 이 대사를 발급한 상태머신(=화자). UX_FLOW.md 5절 규칙 7("다중 캐릭터 동시 발화 — 서로 다른
        /// 캐릭터의 말풍선은 독립적으로 동일 계약을 따른다")을 UI 레이어가 지키려면, 전역 이벤트
        /// (StickmanEventBus.DialogueRequested)로 날아온 대사가 "누구의 대사인지"를 구분할 수 있어야
        /// 한다 — 두 캐릭터가 있다면 각자 별도의 StickmanStateMachine 인스턴스를 갖기 때문이다.
        /// 기존 private 필드를 읽기 전용으로 노출할 뿐 새 로직은
        /// 없고, internal이라 같은 어셈블리(Dialogue/DialogueBubbleRenderer.cs) 밖으로는 새지 않는다 —
        /// 대사 **생성** 경로의 방어선(생성자가 컨텍스트를 요구하고 토큰을 소비하는 구조)에는 아무런
        /// 영향이 없다.
        /// </summary>
        internal StickmanStateMachine OriginMachine => _originMachine;

        /// <summary>
        /// 이 대사가 아직 유효한지. 자신을 만든 전이가 더 이상 머신의 "현재 세대"가 아니게 되면
        /// (즉 다른 상태로 재전이/인터럽트되면) false가 된다. UI 레이어는 DialogueExpired 이벤트로
        /// 통지받으므로 보통 이 값을 직접 폴링할 필요는 없지만, 방어적 조회용으로 공개한다.
        /// </summary>
        public bool IsValid => !_expired && _originMachine.CurrentTransitionGeneration == _transitionGeneration;

        /// <param name="context">
        /// 상태 전이가 확정된 그 순간의 컨텍스트. StickmanStateMachine.ChangeState() 내부에서만
        /// 생성되어 IStickmanState.Enter(context)로 전달되므로, 정상적인 경로에서는 상태 구현체의
        /// Enter() 안에서만 이 값을 가질 수 있다.
        /// </param>
        /// <param name="textFromState">
        /// context.To(확정된 상태)만 입력받는 텍스트 파생 함수. 파라미터가 필요 없는 단순 상태(예: Idle
        /// 유휴 잡담)를 위한 편의 오버로드 — 내부적으로 아래 파라미터 포함 생성자에 위임한다.
        /// </param>
        public DialogueIntent(StateTransitionContext context, Func<StickmanStateId, string> textFromState)
            : this(context, WrapSimpleTextFunc(textFromState))
        {
        }

        // Func<StickmanStateId,string> -> Func<StickmanStateId,object,string> 어댑터. textFromState가
        // null이면 그대로 null을 전달해(파라미터 포함 생성자가) 동일한 ArgumentNullException을 던지게 한다.
        private static Func<StickmanStateId, object, string> WrapSimpleTextFunc(Func<StickmanStateId, string> textFromState)
        {
            return textFromState == null ? (Func<StickmanStateId, object, string>)null : (id, _) => textFromState(id);
        }

        /// <param name="context">
        /// 상태 전이가 확정된 그 순간의 컨텍스트. StickmanStateMachine.ChangeState() 내부에서만
        /// 생성되어 IStickmanState.Enter(context)로 전달되므로, 정상적인 경로에서는 상태 구현체의
        /// Enter() 안에서만 이 값을 가질 수 있다.
        /// </param>
        /// <param name="textFromState">
        /// context.To(확정된 상태)와 그 상태의 파라미터(IHasDialogueParams.DialogueParams, 구현하지
        /// 않은 상태라면 null)를 입력받는 텍스트 파생 함수(BUG-M7 대응). 상태와 무관한 자유 문자열/
        /// 파라미터 전달을 막기 위해 파라미터는 항상 context.OriginMachine.CurrentState에서 직접 읽는다.
        /// </param>
        public DialogueIntent(StateTransitionContext context, Func<StickmanStateId, object, string> textFromState)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context),
                    "StateTransitionContext는 null일 수 없습니다 — StickmanStateMachine.ChangeState()가 발급한 " +
                    "컨텍스트만 사용하세요.");
            }
            if (context.OriginMachine == null)
            {
                throw new ArgumentException(
                    "StateTransitionContext는 StickmanStateMachine.ChangeState()가 발급한 것이어야 합니다. " +
                    "OriginMachine이 없는 컨텍스트로는 DialogueIntent를 만들 수 없습니다.",
                    nameof(context));
            }
            if (textFromState == null) throw new ArgumentNullException(nameof(textFromState));

            // BUG-M1 Phase 2 완결: 같은 컨텍스트로 DialogueIntent를 두 번 만드는 시도를 여기서 차단한다.
            // 검증(null 체크)이 모두 끝난 뒤에만 토큰을 소비해, 무관한 사유로 예외가 나는 경우까지
            // 토큰을 낭비하지 않는다.
            if (!context.TryConsumeToken())
            {
                throw new InvalidOperationException(
                    "이 StateTransitionContext는 이미 다른 DialogueIntent를 생성하는 데 소비되었습니다. " +
                    "같은 상태 전이 확정 시점으로 DialogueIntent를 두 번 만들 수 없습니다(BUG-M1 Phase 2 대응).");
            }

            StateId = context.To;
            CreatedFrame = context.ConfirmedFrame;
            _transitionGeneration = context.TransitionGeneration;
            _originMachine = context.OriginMachine;

            // BUG-M7 대응: 파라미터는 호출자가 넘기는 게 아니라 "지금 실제로 Enter() 중인 상태 인스턴스"
            // 에서 직접 읽는다(StickmanStateMachine.CurrentState 참고) — 상태와 무관한 파라미터 위조 불가.
            object dialogueParams = (_originMachine.CurrentState as IHasDialogueParams)?.DialogueParams;
            Text = textFromState(context.To, dialogueParams);

            // 이후 이 전이가 추월/취소되는 첫 순간을 감지하기 위해 구독한다. Expire()에서 반드시 해제한다.
            StickmanEventBus.StateTransitioned += OnAnyStateTransitioned;
            StickmanEventBus.RaiseDialogueRequested(this);
        }

        private void OnAnyStateTransitioned(StateTransitionEvent evt)
        {
            if (_expired) return;
            // 아직 내 세대가 머신의 현재 세대와 같다면(=이 이벤트가 바로 나를 만든 그 전이라면) 취소가 아니다.
            if (_originMachine.CurrentTransitionGeneration == _transitionGeneration) return;
            Expire();
        }

        private void Expire()
        {
            _expired = true;
            StickmanEventBus.StateTransitioned -= OnAnyStateTransitioned; // 구독 해제 — 메모리 누수 방지
            StickmanEventBus.RaiseDialogueExpired(this);
        }
    }
}
