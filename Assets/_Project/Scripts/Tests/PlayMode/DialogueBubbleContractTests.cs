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
    ///   ⑤ 규칙 4-b(2026-09-01 개정) — **노출 상한도 글자수의 함수다.** 짧은 대사가 긴 대사보다
    ///      먼저 사라진다(역전 금지). 벽시계(초) 기준으로 검증한다 — 이 저장소의 배치모드 PlayMode는
    ///      2,000fps 이상으로 돌아 프레임 수 기반 예산은 실제 시간과 무관해진다(CLAUDE.md).
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

        // ⑤용 표본 — 실측 표의 양 끝(4자 vs 9자)과 같은 길이 대비다. 상태가 끝나지 않으므로
        // 사라지는 유일한 이유가 **노출 상한**이고, 그래서 상한만 순수하게 관측된다.
        private const string ShortText = "심심하다";
        private const string LongText = "창 위는 미끄러워";

        /// <summary>진입할 때마다 지정된 텍스트로 DialogueIntent를 하나 만드는 테스트 상태.</summary>
        private sealed class TalkingState : IStickmanState
        {
            private readonly string _text;
            public TalkingState(StickmanStateId id, string text) { StateId = id; _text = text; }
            public StickmanStateId StateId { get; }
            // 종류는 매핑 함수가 텍스트와 함께 돌려준다(UX_FLOW.md 5절 규칙 4-a). 이 스텁은 최소 노출
            // 계약(가독예산)을 검증하는 리그라 Reaction을 쓴다 — Narrative는 상태 종료 시 즉시 컷되므로
            // 최소 노출 자체가 적용되지 않는다.
            public void Enter(StateTransitionContext context)
                => _ = new DialogueIntent(context, id => DialogueLine.React(_text));
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
            // ⑤(노출 상한)는 사용자 설정 상한과 예산 상한 중 짧은 쪽을 쓴다. 앞선 테스트가 남긴
            // 사용자 설정이 살아 있으면 두 표본이 같은 값에서 잘려 역전 검증 자체가 무의미해진다.
            AppSettingsModel.ResetForTesting();

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
                { StickmanStateId.Idle, new TalkingState(StickmanStateId.Idle, ShortText) },
                { StickmanStateId.Walk, new TalkingState(StickmanStateId.Walk, LongText) },
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
            AppSettingsModel.ResetForTesting();
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

        /// <summary>
        /// ③ 규칙 5 — 새 대사는 이전 말풍선을 <b>즉시 교체</b>한다(큐잉 없음).
        ///
        /// <para>★ 2026-09-02 — 교체 전에 <b>팝인만큼 벽시계로 기다린다</b>. 팝인 중의 교체는
        /// 2026-09-02에 발화 자격이 부정됐기 때문이다(아래 <c>ReplacementWithinPopIn_*</c>).
        /// 그래도 이 테스트가 지키는 것은 <b>그대로</b>다: 자격이 있는 교체는 <b>같은 프레임에</b>
        /// 일어나야 하고 큐에 쌓이면 안 된다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NewDialogue_ReplacesPreviousBubbleImmediately()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            Assert.AreEqual(TalkText, _renderer.VisibleText);

            // 팝인이 끝나 교체 자격이 생길 때까지. 프레임 수가 아니라 벽시계다(CLAUDE.md).
            yield return new WaitForSecondsRealtime(DialogueTiming.PopInSeconds + 0.05f);
            Assert.GreaterOrEqual(_renderer.VisibleSeconds, DialogueTiming.PopInSeconds,
                "사전 조건 — 교체 시점에 이전 대사가 팝인을 이미 끝냈어야 한다.");

            _machine.ChangeState(StickmanStateId.Getup);

            Assert.IsTrue(_renderer.IsBubbleVisible);
            Assert.AreEqual(SecondTalkText, _renderer.VisibleText,
                "새 상태의 대사가 즉시 교체되지 않았다 — 이전 대사가 남아 있으면 텍스트가 행동보다 " +
                "뒤처진다(UX_FLOW.md 5절 규칙 5).");
        }

        // ============================================================================
        // ⑥ 교체 경로의 발화 자격 (2026-09-02) — ★ 이 저장소가 실제로 겪은 결함
        // ============================================================================

        /// <summary>
        /// ★★ 실기 로그에서 잡힌 <b>0.02초 번쩍임 2연속</b>:
        /// <code>
        ///   frame=11110 교체 — 이전 "어... 힘이 다 샜다"(반응) 노출 3.38초 → 새 "어... 힘이 다 샜다"(반응)
        ///   frame=11111 교체 — 이전 "어... 힘이 다 샜다"(반응) 노출 0.02초 → 새 "여기 좋네"(Idle, 서술)
        ///   frame=11112 즉시 컷 (Idle) "여기 좋네" — 노출 0.02초
        /// </code>
        /// 그 빌드는 <b>규칙 8 게이트를 이미 갖고 있었다</b>(같은 로그에 `발화 보류` 31건). 그런데도
        /// 통과했다 — 게이트는 "상태의 계획 잔여"만 보고 <b>지금 화면에 무엇이 떠 있는지</b>는 한 번도
        /// 보지 않았기 때문이다.
        ///
        /// <para><b>왜 이 결함이 살아남았나</b>: 최소 노출 보호가 <i>만료</i> 경로에만 있었고
        /// <i>교체</i> 경로에는 한 줄도 없었는데, <c>DialogueExposureBudgetTests</c> 12건이 전부
        /// <b>상한과 게이트만</b> 봤다. 교체 경로의 노출 하한을 보는 테스트가 <b>0건</b>이었다.
        /// 이 테스트가 그 공백이다.</para>
        ///
        /// <para><b>큐잉이 아니다</b>(규칙 5 유지): 막힌 대사는 <b>버려진다</b>. 나중에 뒤늦게
        /// 튀어나오지 않는다는 것까지 여기서 확인한다.</para>
        ///
        /// <para><b>벽시계 기준</b>: 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아 프레임 수
        /// 예산은 실제 시간과 무관해진다(CLAUDE.md).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ReplacementWithinPopIn_IsDeniedAndNotQueued()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            Assert.AreEqual(TalkText, _renderer.VisibleText);
            Assert.Less(_renderer.VisibleSeconds, DialogueTiming.PopInSeconds,
                "사전 조건 — 같은 프레임이므로 이전 대사는 아직 팝인 중이어야 한다.");

            // 팝인이 끝나기 전의 교체 = 사용자가 본 것은 문장이 아니라 깜빡임이다.
            _machine.ChangeState(StickmanStateId.Idle);

            Assert.AreEqual(TalkText, _renderer.VisibleText,
                $"팝인({DialogueTiming.PopInSeconds:F2}초)도 못 끝낸 대사가 교체됐다 — 실측 0.02초 " +
                "번쩍임이 그대로 재현된 것이다(UX_FLOW.md 5절 규칙 8의 교체 경로 확장).");

            // ★ 큐잉이 아니다 — 막힌 대사는 나중에도 나오지 않는다.
            yield return new WaitForSecondsRealtime(DialogueTiming.PopInSeconds + 0.05f);
            Assert.AreNotEqual(ShortText, _renderer.VisibleText,
                "막혔던 대사가 뒤늦게 화면에 나왔다 — 발화 자격을 부정한 것이 아니라 큐에 쌓은 것이다" +
                "(규칙 5 큐잉 금지 위반).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 위 보호가 "무조건 교체 금지"라는 오답이 아님을 보인다. <b>같은 전이</b>가
        /// 팝인 뒤에는 그대로 통과한다. 이 짝이 없으면 "교체를 아예 없애 버리기"도 초록이 된다.
        /// </summary>
        [UnityTest]
        public IEnumerator NegativeControl_ReplacementAfterPopIn_IsAllowed()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            yield return new WaitForSecondsRealtime(DialogueTiming.PopInSeconds + 0.05f);

            _machine.ChangeState(StickmanStateId.Idle);
            Assert.AreEqual(ShortText, _renderer.VisibleText,
                "팝인이 끝난 뒤인데도 교체가 막혔다 — 그러면 위 보호는 '교체를 없앤 것'이고 " +
                "규칙 5(즉시 교체)가 죽는다.");
        }

        /// <summary>
        /// ★ 같은 글자가 자기 자신을 교체하는 것도 막는다(실기 로그 frame=11110). 지금까지는
        /// <b>노출 시계와 팝인이 리셋</b>돼 화면상 같은 글자가 다시 튀어올랐다 — 사용자에게는
        /// 렌더 글리치로 읽힌다.
        ///
        /// <para>텍스트만 보면 "같은 글자로 교체됨"과 "교체되지 않음"이 구분되지 않는다. 그래서
        /// <see cref="DialogueBubbleRenderer.VisibleSeconds"/>(노출 시계)로 잰다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SameTextReplacement_DoesNotRestartTheExposureClock()
        {
            yield return null;

            _machine.Start(StickmanStateId.Attack);
            float waited = DialogueTiming.PopInSeconds + 0.05f;
            yield return new WaitForSecondsRealtime(waited);

            _machine.ChangeState(StickmanStateId.Attack);   // 같은 상태 · 같은 글자 · 같은 종류.

            Assert.AreEqual(TalkText, _renderer.VisibleText);
            Assert.GreaterOrEqual(_renderer.VisibleSeconds, waited,
                $"같은 글자가 자기 자신을 교체해 노출 시계가 리셋됐다(지금 {_renderer.VisibleSeconds:F2}초 " +
                $"< 기다린 {waited:F2}초) — 화면에서는 같은 글자가 다시 튀어오른다.");
        }

        /// <summary>④ 규칙 7 — 다른 상태머신(= 다른 화자)의 대사는 이 렌더러가 그리지 않는다.</summary>
        [UnityTest]
        public IEnumerator OtherSpeakerDialogue_IsNotRendered()
        {
            yield return null;

            var otherMachine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Attack, new TalkingState(StickmanStateId.Attack, "다른 화자의 대사") },
                { StickmanStateId.Ragdoll, new SilentState(StickmanStateId.Ragdoll) },
            });

            otherMachine.Start(StickmanStateId.Attack);

            Assert.IsFalse(_renderer.IsBubbleVisible,
                "다른 상태머신의 대사를 이 렌더러가 그렸다 — 두 화자의 말풍선이 서로 " +
                "섞이면 안 된다(UX_FLOW.md 5절 규칙 7).");

            otherMachine.ChangeState(StickmanStateId.Ragdoll); // 만료시켜 구독 정리.
        }

        /// <summary>
        /// ⑤ 규칙 4-b 개정 — <b>짧은 대사가 긴 대사보다 먼저 사라진다</b>(노출 시간 역전 금지).
        ///
        /// 실측 위반: 하암...(5자)이 4.14초, 창 위는 미끄러워(9자)가 1.45초 떠 있었다. 하한만
        /// 글자수 비례로 바꾸고 상한을 고정 4초로 남겨 둔 결과가 개편 취지의 정반대였다.
        ///
        /// <para><b>왜 벽시계인가</b>: 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아
        /// 프레임 수 기반 대기는 실제로 수십 ms밖에 안 되는 경우가 있다(CLAUDE.md 확정 사항).
        /// 그래서 대기 예산을 <see cref="WaitForSecondsRealtime"/>로만 잡는다.</para>
        ///
        /// <para><b>왜 상태를 끝내지 않는가</b>: 상태가 살아 있으면 만료 경로가 열리지 않아
        /// 말풍선이 사라지는 이유가 <b>노출 상한 하나</b>뿐이다 — 관측하려는 것을 그것만 남긴다.</para>
        ///
        /// <para><b>네거티브 컨트롤</b>: 같은 대기 시점에 <b>긴 대사는 아직 살아 있어야 한다.</b>
        /// 그 짝이 없으면 "무조건 일찍 지우기"라는 오답이 통과하고, 구판(둘 다 4초 고정)에서는
        /// 짧은 대사 단언 쪽이 그대로 빨간불이 된다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ExposureCap_ShortLineDisappearsBeforeLongLine()
        {
            yield return null;

            float shortCap = DialogueBudget.MaxVisibleSecondsFor(ShortText,
                DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);
            float longCap = DialogueBudget.MaxVisibleSecondsFor(LongText,
                DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);
            Assert.Less(shortCap, longCap,
                "사전 조건 — 짧은 대사의 상한이 긴 대사보다 짧아야 한다(예산 함수 자체의 성질).");

            // 두 상한 사이의 한 점. 여기서 짧은 쪽은 이미 사라졌고 긴 쪽은 아직 살아 있어야 한다.
            // 페이드아웃(FadeOutSeconds)까지 끝날 시간을 짧은 쪽에 더해 관측점을 잡는다.
            float observeAt = shortCap + DialogueTiming.FadeOutSeconds + 0.15f;
            Assert.Less(observeAt, longCap,
                $"관측점 {observeAt:F2}초가 긴 대사 상한 {longCap:F2}초를 넘어 두 사건을 구분할 수 없다 — " +
                "예산 상수가 바뀌었다면 이 테스트의 표본 길이를 다시 잡아야 한다.");

            // ── 짧은 대사: 상한 + 페이드아웃이 지나면 사라져 있어야 한다.
            _machine.Start(StickmanStateId.Idle);
            Assert.AreEqual(ShortText, _renderer.VisibleText);
            yield return new WaitForSecondsRealtime(observeAt);
            Assert.IsFalse(_renderer.IsBubbleVisible,
                $"4자 대사가 {observeAt:F2}초 뒤에도 떠 있다 — 상한이 글자수를 안 보고 있다. " +
                "실측에서 \"심심하다\"(4자)가 4.10초, \"창 위는 미끄러워\"(9자)가 1.45초 떠 있던 " +
                "그 역전이다(가장 짧은 대사가 가장 오래).");

            // ── 긴 대사: 같은 시점에는 아직 살아 있어야 한다(네거티브 컨트롤).
            _machine.ChangeState(StickmanStateId.Walk);
            Assert.AreEqual(LongText, _renderer.VisibleText);
            yield return new WaitForSecondsRealtime(observeAt);
            Assert.IsTrue(_renderer.IsBubbleVisible,
                $"9자 대사가 {observeAt:F2}초 만에 사라졌다 — 같은 시점에 짧은 대사만 사라져야 " +
                "역전 해소를 검증한 것이 된다. 둘 다 사라지면 '무조건 일찍 지우기'라는 오답도 통과한다.");
        }
    }
}
