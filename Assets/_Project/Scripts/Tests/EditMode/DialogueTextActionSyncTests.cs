using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 텍스트-액션 싱크 계약(CLAUDE.md 절대 불변 원칙 1 / docs/ARCHITECTURE.md 3절) 회귀 테스트.
    ///
    /// 기획서 0번 항목의 실패 사례("한 발 더"라고 말만 하고 안 쏨, "오늘은 여기까지"라고 해놓고
    /// 다시 쏨)를 구조적으로 재현 불가능하게 만드는 DialogueIntent/StateTransitionContext/
    /// TransitionGeneration 메커니즘을 고정한다. 관련 문서: docs/UX_FLOW.md 5절(생성/소멸 타이밍
    /// 계약)·31절(파라미터 스냅샷 분기 원칙), docs/BUG_REPORT_PHASE0.md BUG-M1/BUG-M7,
    /// docs/BUG_REPORT_PHASE2.md, Tasklist.md Phase 2 "텍스트-액션 싱크 회귀 테스트" 행.
    ///
    /// 실제 프로덕션 상태(AttackState/RagdollState 등)는 StickmanBlackboard/Rigidbody2D 등 씬
    /// 의존성이 있어 순수 EditMode 단위 테스트로 인스턴스화하기 무겁다. 이 테스트는 그 대신
    /// IStickmanState/IHasDialogueParams를 직접 구현하는 최소 가짜(fake) 상태로 StickmanStateMachine
    /// ↔ DialogueIntent ↔ StickmanEventBus의 계약 자체만 순수하게 검증한다 — 전투/파쿠르 등 상태별
    /// 실제 게임플레이 로직의 정확성은 이 테스트의 범위가 아니다.
    /// </summary>
    public class DialogueTextActionSyncTests
    {
        // ================= 테스트 전용 가짜(fake) 상태 =================

        /// <summary>대사를 만들지 않는 최소 상태. Enter()에서 받은 context만 관찰용으로 저장한다.</summary>
        private sealed class SimpleState : IStickmanState
        {
            public StickmanStateId StateId { get; }
            public StateTransitionContext LastContext { get; private set; }

            public SimpleState(StickmanStateId id) => StateId = id;

            public void Enter(StateTransitionContext context) => LastContext = context;
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        /// <summary>Enter() 확정 즉시 파라미터 없는 DialogueIntent를 만드는 상태(정상/취소 케이스용).</summary>
        private sealed class DialogueEmittingState : IStickmanState
        {
            public StickmanStateId StateId { get; }
            public DialogueIntent LastIntent { get; private set; }
            private readonly Func<StickmanStateId, string> _textFn;

            public DialogueEmittingState(StickmanStateId id, Func<StickmanStateId, string> textFn)
            {
                StateId = id;
                _textFn = textFn;
            }

            public void Enter(StateTransitionContext context) => LastIntent = new DialogueIntent(context, _textFn);
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        /// <summary>
        /// UX_FLOW.md 31-2 표의 Attack(shotsRemaining) 예시를 그대로 재현하는 IHasDialogueParams 가짜 상태.
        /// LiveParams는 테스트 코드가 Enter() 전/후로 자유롭게 조작할 수 있는 참조 타입 필드다.
        /// </summary>
        private sealed class ParamDialogueState : IStickmanState, IHasDialogueParams
        {
            public sealed class Params
            {
                public int ShotsRemaining;
            }

            public StickmanStateId StateId { get; }
            public Params LiveParams { get; } = new Params();
            public DialogueIntent LastIntent { get; private set; }

            public ParamDialogueState(StickmanStateId id) => StateId = id;

            public object DialogueParams => LiveParams;

            public void Enter(StateTransitionContext context)
            {
                // UX 31-1 원칙 그대로: 같은 매핑 함수, 같은 파라미터 스냅샷 안의 if/else 분기만 사용.
                LastIntent = new DialogueIntent(context, (id, p) =>
                {
                    var snapshot = p as Params;
                    int shots = snapshot?.ShotsRemaining ?? 0;
                    return shots >= 1 ? "한 발 더!" : "오늘은 여기까지";
                });
            }

            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private static StickmanStateMachine BuildMachine(params IStickmanState[] states)
        {
            var dict = new Dictionary<StickmanStateId, IStickmanState>();
            foreach (var s in states) dict[s.StateId] = s;
            return new StickmanStateMachine(dict);
        }

        // ================= 1. 정상 케이스 =================

        [Test]
        public void ChangeState가_확정되면_같은_전이에서_생성된_DialogueIntent는_Valid다()
        {
            var idle = new SimpleState(StickmanStateId.Idle);
            var attack = new DialogueEmittingState(StickmanStateId.Attack, id => "타앗!");
            var machine = BuildMachine(idle, attack);

            machine.Start(StickmanStateId.Idle);
            machine.ChangeState(StickmanStateId.Attack);

            Assert.IsNotNull(attack.LastIntent, "상태 전이가 확정되는 Enter() 안에서 DialogueIntent가 생성되어야 한다.");
            Assert.IsTrue(attack.LastIntent.IsValid, "정상 전이 직후 DialogueIntent는 유효(IsValid==true)해야 한다.");
            Assert.AreEqual(StickmanStateId.Attack, attack.LastIntent.StateId);
            Assert.AreEqual("타앗!", attack.LastIntent.Text);
        }

        // ================= 2. 강제 취소 케이스(핵심) =================

        [Test]
        public void 강제인터럽트_ChangeState는_직전_DialogueIntent를_즉시_만료시키고_DialogueExpired를_발행한다()
        {
            var idle = new SimpleState(StickmanStateId.Idle);
            var attack = new DialogueEmittingState(StickmanStateId.Attack, id => "한 발 더!");
            var ragdoll = new SimpleState(StickmanStateId.Ragdoll);
            var machine = BuildMachine(idle, attack, ragdoll);

            machine.Start(StickmanStateId.Idle);
            machine.ChangeState(StickmanStateId.Attack);
            var intent = attack.LastIntent;
            Assert.IsTrue(intent.IsValid, "인터럽트 발생 전에는 유효해야 한다(사전 조건 확인).");

            var expiredEvents = new List<DialogueIntent>();
            void OnExpired(DialogueIntent e) => expiredEvents.Add(e);
            StickmanEventBus.DialogueExpired += OnExpired;
            try
            {
                // "한 발 더!"라고 말한 직후 피격 -> Ragdoll로 강제 인터럽트(Attack 미완료).
                machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);

                Assert.IsFalse(intent.IsValid,
                    "강제 인터럽트가 일어나면 그 즉시 IsValid가 false여야 한다 — " +
                    "'한 발 더'라고 말만 하고 안 쏘는 버그를 구조적으로 막는 핵심 계약.");
                Assert.AreEqual(1, expiredEvents.Count, "DialogueExpired 이벤트가 정확히 1회 발행되어야 한다.");
                Assert.AreSame(intent, expiredEvents[0], "만료 이벤트 페이로드는 원래의 DialogueIntent 인스턴스와 동일해야 한다.");
            }
            finally
            {
                StickmanEventBus.DialogueExpired -= OnExpired;
            }
        }

        [Test]
        public void 정상적인_후속_전이도_이전_세대_DialogueIntent를_즉시_만료시킨다()
        {
            // isForcedInterrupt 플래그와 무관하게(=자연 완료 전이에서도) 세대(TransitionGeneration)가
            // 바뀌는 순간 이전 DialogueIntent는 자동 만료된다 — "다음 프레임이든" 요구사항을
            // 프레임 시점과 무관한 세대 비교로 만족시킨다는 것을 확인한다.
            var idle = new SimpleState(StickmanStateId.Idle);
            var attack = new DialogueEmittingState(StickmanStateId.Attack, id => "한 발 더!");
            var walk = new SimpleState(StickmanStateId.Walk);
            var machine = BuildMachine(idle, attack, walk);

            machine.Start(StickmanStateId.Idle);
            machine.ChangeState(StickmanStateId.Attack);
            var intent = attack.LastIntent;

            machine.ChangeState(StickmanStateId.Walk);

            Assert.IsFalse(intent.IsValid, "다음 전이가 발생하면 이전 DialogueIntent는 강제 인터럽트 여부와 무관하게 만료되어야 한다.");
        }

        // ================= 3. 위조 방지 =================

        [Test]
        public void StateTransitionContext는_public_생성자가_없는_sealed_class다()
        {
            // 이력(BUG-M1): 원래 readonly struct였을 때는 default(StateTransitionContext)/
            // new StateTransitionContext()로 OriginMachine==null인 가짜 컨텍스트를 공짜로 만들 수 있었다.
            // 지금은 sealed class + 생성자 전부 internal이라 그런 코드는 "이 테스트 파일에 그대로
            // 옮겨 적어도 컴파일 자체가 되지 않는다" — 아래 리플렉션 검증이 그 사실을 자동화한다.
            var type = typeof(StateTransitionContext);

            Assert.IsTrue(type.IsClass, "구조체(값 복사로 default(...) 위조가 가능)가 아니라 클래스여야 한다.");
            Assert.IsTrue(type.IsSealed, "상속을 통한 우회 생성을 막기 위해 sealed여야 한다.");

            var publicCtors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(0, publicCtors.Length,
                "public 생성자가 하나도 없어야 한다 — 외부 코드가 new StateTransitionContext(...)로 " +
                "임의의 컨텍스트를 위조할 수 없어야 한다.");
        }

        [Test]
        public void TryConsumeToken은_최초_1회만_true를_반환한다()
        {
            var machine = BuildMachine(new SimpleState(StickmanStateId.Idle));
            // internal 생성자 — InternalsVisibleTo(StickMate.Tests.EditMode)로 이 테스트 어셈블리에만
            // 허용된 접근이다. 일반 외부 코드는 이 생성자를 호출할 수 없다.
            var ctx = new StateTransitionContext(StickmanStateId.Idle, StickmanStateId.Attack, 0, 1, machine);

            Assert.IsTrue(ctx.TryConsumeToken(), "최초 소비 시도는 성공(true)해야 한다.");
            Assert.IsFalse(ctx.TryConsumeToken(), "두 번째 소비 시도부터는 항상 실패(false)해야 한다.");
            Assert.IsFalse(ctx.TryConsumeToken(), "세 번째 이후로도 계속 실패해야 한다.");
        }

        // ================= 4. 파라미터 스냅샷 무결성 (UX 31-1) =================

        [Test]
        public void DialogueIntent_텍스트는_Enter시점_파라미터_스냅샷을_반영하고_이후_변경은_반영하지않는다()
        {
            var idle = new SimpleState(StickmanStateId.Idle);
            var attack = new ParamDialogueState(StickmanStateId.Attack);
            var machine = BuildMachine(idle, attack);

            machine.Start(StickmanStateId.Idle);
            attack.LiveParams.ShotsRemaining = 1; // Enter() 시점에 이 값이 스냅샷된다.
            machine.ChangeState(StickmanStateId.Attack);

            Assert.AreEqual("한 발 더!", attack.LastIntent.Text);

            // Enter() 이후 파라미터 원본 객체 값이 바뀌어도(예: 다음 틱에 값이 갱신되는 상황을 흉내)
            // 이미 만들어진 DialogueIntent.Text는 그대로여야 한다 — 나중 값을 반영하면 31-1 위반.
            attack.LiveParams.ShotsRemaining = 0;

            Assert.AreEqual("한 발 더!", attack.LastIntent.Text,
                "DialogueIntent.Text는 Enter() 시점 파라미터 스냅샷을 반영해야 하며, 그 이후의 값 변경에 영향받으면 안 된다.");
        }

        [Test]
        public void 파라미터를_경계값_양쪽으로_바꿔_Enter하면_대응하는_텍스트_한종류만_나온다()
        {
            // UX_FLOW.md 31-3 회귀 기준선: 파라미터를 경계값 양쪽으로 바꿔가며 Enter()를 호출했을 때
            // 정확히 대응하는 텍스트 한 종류만 나오고, 두 텍스트가 뒤섞이지 않아야 한다.
            var idle = new SimpleState(StickmanStateId.Idle);
            var walk = new SimpleState(StickmanStateId.Walk);
            var attack = new ParamDialogueState(StickmanStateId.Attack);
            var machine = BuildMachine(idle, walk, attack);

            machine.Start(StickmanStateId.Idle);

            attack.LiveParams.ShotsRemaining = 1;
            machine.ChangeState(StickmanStateId.Attack);
            Assert.AreEqual("한 발 더!", attack.LastIntent.Text);

            machine.ChangeState(StickmanStateId.Walk); // Attack에서 빠져나와 재진입 가능하게 함.
            attack.LiveParams.ShotsRemaining = 0;
            machine.ChangeState(StickmanStateId.Attack);
            Assert.AreEqual("오늘은 여기까지", attack.LastIntent.Text);
        }

        // ================= 5. 동일 컨텍스트 재사용 차단 =================

        [Test]
        public void 같은_StateTransitionContext로_DialogueIntent를_두번_만들면_두번째는_실패한다()
        {
            var idle = new SimpleState(StickmanStateId.Idle);
            var capture = new SimpleState(StickmanStateId.Attack); // 대사를 만들지 않고 context만 저장.
            var machine = BuildMachine(idle, capture);

            machine.Start(StickmanStateId.Idle);
            machine.ChangeState(StickmanStateId.Attack);
            var ctx = capture.LastContext;
            Assert.IsNotNull(ctx, "Enter()가 context를 받아야 한다(사전 조건 확인).");

            var first = new DialogueIntent(ctx, id => "첫 번째 대사");
            Assert.IsNotNull(first);
            Assert.IsTrue(first.IsValid);

            Assert.Throws<InvalidOperationException>(
                () => new DialogueIntent(ctx, id => "같은 컨텍스트 재사용 시도"),
                "같은 StateTransitionContext로 두 번째 DialogueIntent를 생성하려는 시도는 " +
                "InvalidOperationException으로 실패해야 한다(BUG-M1 Phase 2 완결 계약).");
        }
    }
}
