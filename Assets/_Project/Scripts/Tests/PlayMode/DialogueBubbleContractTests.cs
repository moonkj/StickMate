using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ docs/UX_FLOW.md 5절 `DialogueIntent` UX 계약을 **화면 표시 레이어**에서 고정하는 회귀 테스트.
    ///
    /// 지금까지 EditMode의 DialogueTextActionSyncTests가 잠가둔 것은 "대사가 언제 만들어지고 언제
    /// 무효화되는가"(파이프라인 계약)였다. 이 파일은 그 다음 칸 — **"무효화된 대사가 화면에서 언제
    /// 사라지는가"**(표시 계약)를 잠근다. 5절이 "이 경로가 '한 발 더라고 말만 하고 안 쏨' 버그를 막는
    /// 핵심 방어선"이라고 지목한 바로 그 지점이다.
    ///
    /// 검증하는 4가지(각각 5절의 규칙 번호와 1:1 대응):
    ///   ① 규칙 3(b)/4 — 강제 인터럽트 시 말풍선이 **같은 프레임에** 사라진다. 프레임을 하나도 넘기지
    ///      않고(yield 없이) ChangeState 직후 곧바로 확인하므로, "사라지는 데 1프레임이라도 걸리면"
    ///      즉시 실패한다.
    ///   ② 규칙 3(a)/4 — 반대로 **정상 종료**에서는 최소 노출 시간이 보장된다(같은 프레임에 사라지면
    ///      안 된다). ①과 ②가 쌍으로 있어야 "무조건 즉시 지우기"라는 손쉬운 오답이 통과하지 못한다.
    ///   ③ 규칙 5 — 새 대사는 이전 말풍선을 **즉시 교체**한다(큐잉 없음).
    ///   ④ 규칙 7 — 다른 캐릭터(다른 상태머신)의 대사는 이 렌더러가 그리지 않는다.
    ///
    /// 검증 방식: 실제 씬/캐릭터에 의존하지 않는다. 상태머신과 렌더러만 코드로 만들고, 테스트용
    /// 상태 2종(말하는 상태 / 침묵 상태)을 등록해 전이를 직접 유도한다 — "강제 인터럽트"라는 조건은
    /// 물리 충돌 없이 <c>ChangeState(next, isForcedInterrupt: true)</c> 한 줄로 정확히 재현되고,
    /// 그것이 실제 프로덕션 경로(States/RagdollImpactResolver.cs가 호출하는 바로 그 시그니처)와 같다.
    /// </summary>
    public sealed class DialogueBubbleContractTests
    {
        private const string TalkText = "한 발 더!";
        private const string SecondTalkText = "오늘은 여기까지";

        /// <summary>진입할 때마다 지정된 텍스트로 DialogueIntent를 하나 만드는 테스트 상태.</summary>
        private sealed class TalkingState : IStickmanState
        {
            private readonly string _text;
            public TalkingState(StickmanStateId id, string text) { StateId = id; _text = text; }
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) => _ = new DialogueIntent(context, id => _text);
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        /// <summary>대사를 전혀 만들지 않는 테스트 상태(강제 인터럽트의 목적지 역할).</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) { StateId = id; }
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private GameObject _rendererGo;
        private DialogueBubbleRenderer _renderer;
        private StickmanStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            // 렌더러를 **먼저** 만들어 이벤트를 구독시킨 뒤에야 상태머신을 시작한다 — 실제 씬에서도
            // 렌더러의 OnEnable이 어떤 상태 전이보다 앞선다(Dialogue/DialogueBubbleRenderer.cs 문서의
            // "이벤트 순서에 대한 근거").
            _rendererGo = new GameObject("TestDialogueBubbleRenderer");
            _renderer = _rendererGo.AddComponent<DialogueBubbleRenderer>();

            _machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Attack, new TalkingState(StickmanStateId.Attack, TalkText) },
                { StickmanStateId.Getup, new TalkingState(StickmanStateId.Getup, SecondTalkText) },
                { StickmanStateId.Ragdoll, new SilentState(StickmanStateId.Ragdoll) },
            });
            _renderer.Bind(_machine, _rendererGo.transform);
        }

        [TearDown]
        public void TearDown()
        {
            // 살아 있는 DialogueIntent가 정적 이벤트 구독을 물고 다음 테스트로 넘어가지 않도록,
            // 침묵 상태로 한 번 더 전이시켜 전부 만료시킨다(세대 증가 = 일괄 만료).
            if (_machine != null && _machine.CurrentStateId != StickmanStateId.Ragdoll)
            {
                _machine.ChangeState(StickmanStateId.Ragdoll);
            }
            _machine = null;
            if (_rendererGo != null) Object.DestroyImmediate(_rendererGo);
            _rendererGo = null;
            _renderer = null;
        }

        /// <summary>① 규칙 3(b)/4 — 강제 인터럽트: 같은 프레임에 즉시 제거(페이드아웃 없음).</summary>
        [UnityTest]
        public IEnumerator ForcedInterrupt_RemovesBubbleInSameFrame()
        {
            yield return null; // 렌더러의 Start()까지 한 번 돌려 초기화를 끝낸다.

            _machine.Start(StickmanStateId.Attack);
            Assert.IsTrue(_renderer.IsBubbleVisible, "말하는 상태로 전이했는데 말풍선이 나타나지 않았다.");
            Assert.AreEqual(TalkText, _renderer.VisibleText);

            int frameAtInterrupt = Time.frameCount;
            int removalsBefore = _renderer.ImmediateRemovalCount;

            // ★ 핵심: 프레임을 넘기지 않고(yield 없이) 곧바로 확인한다.
            _machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);

            Assert.IsFalse(_renderer.IsBubbleVisible,
                "강제 인터럽트(RAGDOLL)로 취소된 상태의 말풍선이 같은 프레임에 사라지지 않았다 — " +
                "UX_FLOW.md 5절 규칙 3(b) 위반('한 발 더'라고 말만 하고 널브러지는 그 버그).");
            Assert.AreEqual(removalsBefore + 1, _renderer.ImmediateRemovalCount, "즉시 제거 카운터가 증가하지 않았다.");
            Assert.AreEqual(frameAtInterrupt, _renderer.LastImmediateRemovalFrame,
                "즉시 제거가 인터럽트와 같은 프레임에 일어나지 않았다.");
        }

        /// <summary>
        /// ② 규칙 3(a)/4 — 정상 종료에서는 최소 노출 시간이 보장된다(같은 프레임에 사라지면 안 된다).
        /// ①과 쌍을 이뤄 "무조건 즉시 지우기"라는 오답을 걸러낸다.
        /// </summary>
        [UnityTest]
        public IEnumerator NormalTransition_KeepsBubbleForMinimumExposureThenFadesOut()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            Assert.IsTrue(_renderer.IsBubbleVisible);

            int removalsBefore = _renderer.ImmediateRemovalCount;
            _machine.ChangeState(StickmanStateId.Ragdoll); // isForcedInterrupt: false (정상 종료)

            Assert.IsTrue(_renderer.IsBubbleVisible,
                "정상 종료인데 말풍선이 같은 프레임에 사라졌다 — 최소 노출 시간(규칙 4)이 지켜지지 않았다.");
            Assert.AreEqual(removalsBefore, _renderer.ImmediateRemovalCount,
                "정상 종료를 강제 인터럽트로 오인해 즉시 제거했다.");

            // 최소 노출 시간(기본 0.7초) + 페이드아웃(0.12초) + 여유.
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(_renderer.IsBubbleVisible, "정상 종료 후 페이드아웃이 끝났는데도 말풍선이 남아 있다.");
        }

        /// <summary>③ 규칙 5 — 새 대사는 이전 말풍선을 즉시 교체한다(큐잉 없음).</summary>
        [UnityTest]
        public IEnumerator NewDialogue_ReplacesPreviousBubbleImmediately()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            Assert.AreEqual(TalkText, _renderer.VisibleText);

            _machine.ChangeState(StickmanStateId.Getup);

            Assert.IsTrue(_renderer.IsBubbleVisible);
            Assert.AreEqual(SecondTalkText, _renderer.VisibleText,
                "새 상태의 대사가 즉시 교체되지 않았다 — 이전 대사가 남아 있으면 텍스트가 행동보다 " +
                "뒤처진다(UX_FLOW.md 5절 규칙 5).");
        }

        /// <summary>④ 규칙 7 — 다른 캐릭터(라이벌)의 대사는 이 렌더러가 그리지 않는다.</summary>
        [UnityTest]
        public IEnumerator OtherSpeakerDialogue_IsNotRendered()
        {
            yield return null;

            var otherMachine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Attack, new TalkingState(StickmanStateId.Attack, "라이벌의 대사") },
                { StickmanStateId.Ragdoll, new SilentState(StickmanStateId.Ragdoll) },
            });

            otherMachine.Start(StickmanStateId.Attack);

            Assert.IsFalse(_renderer.IsBubbleVisible,
                "다른 상태머신(라이벌)의 대사를 이 렌더러가 그렸다 — 두 캐릭터의 말풍선이 서로 " +
                "섞이면 안 된다(UX_FLOW.md 5절 규칙 7).");

            otherMachine.ChangeState(StickmanStateId.Ragdoll); // 만료시켜 구독 정리.
        }
    }
}
