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

        /// <summary>
        /// 이 대사의 종류(docs/UX_FLOW.md 5절 규칙 4-a). 텍스트와 **같은 매핑 함수 호출 한 번**에서
        /// 함께 나온 값이며, 문자열에서 역추론된 것이 아니다 — 그래야 같은 상태가 상황에 따라
        /// 서술/반응을 갈라 쓸 수 있다(<see cref="DialogueKind"/> 문서의 2026-09-02 메모 참고 —
        /// 지금 실제로 그렇게 쓰는 상태는 없다).
        /// </summary>
        public DialogueKind Kind { get; }

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
        /// <param name="lineFromState">
        /// context.To(확정된 상태)만 입력받는 대사 파생 함수. 반환형이 string이 아니라
        /// <see cref="DialogueLine"/>(텍스트 + 종류)인 이유는 UX_FLOW.md 5절 규칙 4-a 참고 —
        /// 종류를 문자열에서 역추론하지 않기 위해서다. 파라미터가 필요 없는 단순 상태를 위한 편의
        /// 오버로드이며 내부적으로 아래 파라미터 포함 생성자에 위임한다.
        /// </param>
        public DialogueIntent(StateTransitionContext context, Func<StickmanStateId, DialogueLine> lineFromState)
            : this(context, WrapSimpleLineFunc(lineFromState))
        {
        }

        // Func<StickmanStateId,DialogueLine> -> Func<StickmanStateId,object,DialogueLine> 어댑터.
        // lineFromState가 null이면 그대로 null을 전달해(파라미터 포함 생성자가) 동일한
        // ArgumentNullException을 던지게 한다.
        private static Func<StickmanStateId, object, DialogueLine> WrapSimpleLineFunc(Func<StickmanStateId, DialogueLine> lineFromState)
        {
            return lineFromState == null ? (Func<StickmanStateId, object, DialogueLine>)null : (id, _) => lineFromState(id);
        }

        /// <param name="context">
        /// 상태 전이가 확정된 그 순간의 컨텍스트. StickmanStateMachine.ChangeState() 내부에서만
        /// 생성되어 IStickmanState.Enter(context)로 전달되므로, 정상적인 경로에서는 상태 구현체의
        /// Enter() 안에서만 이 값을 가질 수 있다.
        /// </param>
        /// <param name="lineFromState">
        /// context.To(확정된 상태)와 그 상태의 파라미터(IHasDialogueParams.DialogueParams, 구현하지
        /// 않은 상태라면 null)를 입력받는 대사 파생 함수(BUG-M7 대응). 상태와 무관한 자유 문자열/
        /// 파라미터 전달을 막기 위해 파라미터는 항상 context.OriginMachine.CurrentState에서 직접 읽는다.
        ///
        /// ★ 이 생성자는 <b>게이트를 거치지 않는다</b> — <see cref="DialogueKind.Reaction"/>(점 사건
        /// 서술)처럼 계획 체류 시간과 무관한 대사 전용이다. <see cref="DialogueKind.Narrative"/>는
        /// 반드시 <see cref="TryCreate"/>로 만든다(UX_FLOW.md 5절 규칙 8).
        /// </param>
        public DialogueIntent(StateTransitionContext context, Func<StickmanStateId, object, DialogueLine> lineFromState)
            : this(context, ConsumeAndResolve(context, lineFromState))
        {
        }

        /// <summary>
        /// ★ 발화 자격 게이트 경로(docs/UX_FLOW.md 5절 규칙 8, 2026-09-01 신설).
        ///
        /// <see cref="DialogueKind.Narrative"/>(진행 서술)는 상태가 끝나는 순간 문장이 거짓이 되므로
        /// 규칙 4-c ③에 따라 **즉시 컷**된다. 그것만 넣으면 "0.08초 번쩍이고 사라지는 글자"라는 새
        /// 노이즈가 생기므로, 제거 시점이 아니라 **발화 시점에** 막는다 — 지금 확정된 상태의 계획
        /// 잔여 체류 시간이 "페이드인 + 가독예산"에 못 미치면 <b>대사를 아예 만들지 않고 null을
        /// 돌려준다</b>. 침묵은 거짓말이 아니다.
        ///
        /// 반환값이 null이어도 <b>토큰은 소비된다</b>. "말할지 말지"를 같은 전이에서 두 번 묻는 경로를
        /// 남기면, 첫 판정을 나중에 번복하는 구조가 되어 31-1(하나의 Enter, 하나의 스냅샷)이 깨진다.
        /// </summary>
        /// <param name="plannedDwellSeconds">
        /// 지금 확정된 상태의 **계획 잔여 체류 시간**(초). 지어내는 값이 아니라 각 상태가 이미
        /// <c>Enter()</c>에서 확정해 둔 값이다(규칙 8 표). 알 수 없으면 <see cref="float.NaN"/>을
        /// 넘긴다 — 그때는 막지 않는다(침묵보다 안전한 쪽).
        /// </param>
        public static DialogueIntent TryCreate(StateTransitionContext context,
            Func<StickmanStateId, object, DialogueLine> lineFromState, float plannedDwellSeconds)
        {
            DialogueLine line = ConsumeAndResolve(context, lineFromState);
            if (!DialogueBudget.IsEligible(line, plannedDwellSeconds, DialogueTiming.FadeInSeconds))
            {
                // 화면을 볼 수 없는 검증 환경에서 "왜 이 상태에서 대사가 안 나왔는가"를 로그만으로
                // 재구성할 수 있어야 한다 — 침묵이 계약의 결과인지 버그인지 구분되지 않으면 규칙 8은
                // 검증 불가능한 규칙이 된다.
                UnityEngine.Debug.Log($"[말풍선] 발화 보류 ({context.To}) \"{line.Text}\" — 서술 대사인데 " +
                    $"계획 잔여 체류 {plannedDwellSeconds:F2}초 < 필요체류 " +
                    $"{DialogueBudget.RequiredDwellSeconds(line.Text, DialogueTiming.FadeInSeconds):F2}초" +
                    $"(페이드인 {DialogueTiming.FadeInSeconds:F2} + 가독예산 {DialogueBudget.ReadingSeconds(line.Text):F2}). " +
                    $"규칙 8 — 말할 시간이 없으면 말하지 않는다. frame={UnityEngine.Time.frameCount}");
                return null;
            }
            return new DialogueIntent(context, line);
        }

        /// <summary>
        /// 컨텍스트 검증 -> 토큰 1회 소비 -> 파라미터 조회 -> 매핑 함수 1회 호출. 공개 생성자와
        /// <see cref="TryCreate"/>가 **정확히 같은 순서**를 밟도록 한 곳에 모아 둔다.
        /// </summary>
        private static DialogueLine ConsumeAndResolve(StateTransitionContext context,
            Func<StickmanStateId, object, DialogueLine> lineFromState)
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
            if (lineFromState == null) throw new ArgumentNullException(nameof(lineFromState));

            // BUG-M1 Phase 2 완결: 같은 컨텍스트로 DialogueIntent를 두 번 만드는 시도를 여기서 차단한다.
            // 검증(null 체크)이 모두 끝난 뒤에만 토큰을 소비해, 무관한 사유로 예외가 나는 경우까지
            // 토큰을 낭비하지 않는다.
            if (!context.TryConsumeToken())
            {
                throw new InvalidOperationException(
                    "이 StateTransitionContext는 이미 다른 DialogueIntent를 생성하는 데 소비되었습니다. " +
                    "같은 상태 전이 확정 시점으로 DialogueIntent를 두 번 만들 수 없습니다(BUG-M1 Phase 2 대응).");
            }

            // BUG-M7 대응: 파라미터는 호출자가 넘기는 게 아니라 "지금 실제로 Enter() 중인 상태 인스턴스"
            // 에서 직접 읽는다(StickmanStateMachine.CurrentState 참고) — 상태와 무관한 파라미터 위조 불가.
            object dialogueParams = (context.OriginMachine.CurrentState as IHasDialogueParams)?.DialogueParams;
            return lineFromState(context.To, dialogueParams);
        }

        /// <summary>
        /// 실제 발급부. 위 <see cref="ConsumeAndResolve"/>가 이미 검증/토큰소비/매핑을 끝냈다는 전제
        /// 하에만 호출된다(공개 생성자의 <c>: this(...)</c> 체인과 <see cref="TryCreate"/> 두 곳뿐).
        /// </summary>
        private DialogueIntent(StateTransitionContext context, in DialogueLine line)
        {
            StateId = context.To;
            CreatedFrame = context.ConfirmedFrame;
            _transitionGeneration = context.TransitionGeneration;
            _originMachine = context.OriginMachine;
            Text = line.Text;
            Kind = line.Kind;

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
