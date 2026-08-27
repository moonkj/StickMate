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
    /// 2) 대사 문자열 자체도 "상태 확정 이후" 시점에 그 상태(context.To)로부터만 파생되도록 강제하기
    ///    위해, 생성자가 완성된 문자열을 직접 받지 않고 Func&lt;StickmanStateId, string&gt;
    ///    (상태 -> 텍스트 매핑 함수)를 받아 그 자리에서 context.To를 넣어 텍스트를 만들어낸다.
    ///    호출자가 상태와 무관한 임의 문자열을 미리 준비해 끼워 넣는 경로 자체를 없앤다.
    /// 3) "상태가 중도 취소되면 같은 프레임에 자동 만료": StickmanStateMachine은 ChangeState가
    ///    호출될 때마다 TransitionGeneration을 증가시킨다. DialogueIntent는 생성 시점의 세대를
    ///    스냅샷으로 보관하고, StickmanEventBus.StateTransitioned를 구독해 이후 발생하는 모든 전이
    ///    이벤트에서 "내 세대가 아직 최신인지"를 확인한다. 세대가 바뀌었다면(즉 자신을 만든 전이가
    ///    다른 전이로 추월/취소됨) 같은 프레임 안에서 즉시 만료 처리(IsValid = false)하고
    ///    DialogueExpired 이벤트를 발생시켜 UI 레이어가 말풍선을 즉시 숨기게 한다.
    /// 4) 외부(UI 등)는 StickmanEventBus.DialogueRequested/DialogueExpired 이벤트만 구독한다 —
    ///    상태머신이나 개별 상태 클래스를 직접 참조하지 않는다 (레이어 분리).
    ///
    /// 알려진 한계 (Debugger 리뷰 필요): C# 구조체 특성상 default(StateTransitionContext) 또는
    /// new StateTransitionContext()로 OriginMachine == null인 "가짜" 컨텍스트가 만들어질 수 있다.
    /// 이 경우 아래 생성자가 즉시 ArgumentException을 던지므로 크래시 대신 명확한 실패로 막지만,
    /// 컴파일 타임에 원천 차단하는 수준은 아니다. 더 강한 보증이 필요하면 Phase 2에서
    /// StateTransitionContext를 발급 1회용 토큰을 가진 클래스로 바꾸는 방안을 검토한다.
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
        /// context.To(확정된 상태)만 입력받는 텍스트 파생 함수. 상태와 무관한 자유 문자열 전달을
        /// 막기 위해 시그니처를 의도적으로 Func&lt;StickmanStateId, string&gt;로 제한한다.
        /// </param>
        public DialogueIntent(StateTransitionContext context, Func<StickmanStateId, string> textFromState)
        {
            if (context.OriginMachine == null)
            {
                throw new ArgumentException(
                    "StateTransitionContext는 StickmanStateMachine.ChangeState()가 발급한 것이어야 합니다. " +
                    "default(StateTransitionContext) 등으로 임의 생성된 컨텍스트로는 DialogueIntent를 만들 수 없습니다.",
                    nameof(context));
            }
            if (textFromState == null) throw new ArgumentNullException(nameof(textFromState));

            StateId = context.To;
            CreatedFrame = context.ConfirmedFrame;
            _transitionGeneration = context.TransitionGeneration;
            _originMachine = context.OriginMachine;
            Text = textFromState(context.To);

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
