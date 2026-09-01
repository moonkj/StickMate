using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 격파 미니게임 <b>재도전</b>의 행동-텍스트 싱크(절대 불변 원칙 1) 회귀 테스트 —
    /// 2026-09-01 실측 위반 2건.
    ///
    /// ============================================================================
    /// 무엇이 잡혔나 (페르소나 실측 로그)
    /// ============================================================================
    /// <code>
    /// frame=10908 [말풍선] 표시 (BattleMinigame) "어... 힘이 다 샜다" — 가독예산 1.18초
    /// frame=11110 [말풍선] 교체 — 이전 "어... 힘이 다 샜다" 노출 3.38초, 새 "어... 힘이 다 샜다"
    /// </code>
    /// 3.38초 = <c>battleFailRetryDelaySeconds</c>(1.5) + <b>다음 게이지 충전 1.88초</b>. 즉 뒤의
    /// 1.88초 동안 캐릭터는 기를 <b>다시 모으고 있는데</b> 화면에는 기가 <b>빠졌다</b>는 문장이
    /// 떠 있었다. 게다가 새 대사가 직전과 <b>같은 문자열</b>이라, 보는 사람 눈에는 아무 이유 없이
    /// 같은 글자가 한 번 더 팝인하는 렌더 글리치로 읽혔다.
    ///
    /// ============================================================================
    /// 원인은 튜닝이 아니라 구조였다
    /// ============================================================================
    /// <c>TickResolving()</c>이 재도전에서 <c>ChangeState</c>가 아니라 <c>BeginCharge()</c>를
    /// <b>직접</b> 불렀다. 전이가 아니므로 <c>TransitionGeneration</c>이 오르지 않고, 그래서
    /// (a) 직전 판정 대사가 만료되지 않으며 (b) 개시 대사는 <c>Enter()</c> 안에서만 만들어지므로
    /// 재도전에서 <b>구조적으로 도달 불가</b>였다(실측 3사이클 동안 개시 대사 1회).
    /// "판정 순간"을 self-transition으로 만든 것과 같은 이유로 재충전도 self-transition이어야 하는데,
    /// 그 대칭이 반쪽만 있었다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 파일이 "항상 참인 단언"이 아님을 증명)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><see cref="네거티브_판정_대사는_재충전_전까지는_유효하다"/> — 만료 단언이 "무엇이든
    ///     항상 만료된다"로 통과하는 것이 아님을 보인다. 판정 직후~재충전 직전 구간에서는
    ///     <b>반드시 유효</b>해야 한다.</item>
    ///   <item><see cref="네거티브_타임아웃이_넉넉하면_재충전_개시_대사가_나온다"/> — 무입력 타임아웃
    ///     상한 검사가 "재충전에서는 늘 침묵"으로 통과하지 않음을 보인다.</item>
    /// </list>
    /// 수정 전 코드에 그대로 걸면 <see cref="재충전은_직전_판정_대사를_만료시킨다"/>와
    /// <see cref="재충전마다_개시_대사가_다시_나온다"/>가 빨간불이다(전자는 IsValid가 계속 true,
    /// 후자는 개시 대사가 1회뿐).
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립이다. 상태/대사 파이프라인에는 플랫폼 분기가 없다.</para>
    /// </summary>
    public sealed class BattleRetryDialogueSyncTests
    {
        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StickmanStateMachine _machine;
        private BattleMinigameState _battle;
        private readonly List<DialogueIntent> _requested = new List<DialogueIntent>();

        /// <summary>대사를 만들지 않는 종착 상태(격파가 Idle로 빠질 때 필요).</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting(); // 사용자 설정이 확률/노출에 끼어들지 않게.

            _config = ScriptableObject.CreateInstance<StickConfig>();
            // 게이지 길이를 한 점으로 고정해 난수를 제거한다(BeginCharge의 Random.Range는
            // max <= min이면 min을 그대로 쓴다). 대사 분기도 이 값 하나에서 결정론적으로 나온다.
            _config.battleChargeDurationMin = ChargeSeconds;
            _config.battleChargeDurationMax = ChargeSeconds;
            _config.battleFailRetryDelaySeconds = RetryDelaySeconds;
            _config.battleSuccessResolveDelaySeconds = RetryDelaySeconds;
            _config.battleMaxRetries = 3;
            _config.battleInputTimeoutSeconds = GenerousTimeoutSeconds;

            _blackboard = new StickmanBlackboard { Config = _config };
            _battle = new BattleMinigameState(_blackboard);
            _machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new SilentState(StickmanStateId.Idle) },
                { StickmanStateId.BattleMinigame, _battle },
            });
            _blackboard.Machine = _machine;

            _requested.Clear();
            StickmanEventBus.DialogueRequested += OnRequested;
        }

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.DialogueRequested -= OnRequested;
            // 살아 있는 DialogueIntent가 정적 이벤트 구독을 물고 다음 테스트로 넘어가지 않게 만료시킨다.
            if (_machine != null && _machine.CurrentStateId != StickmanStateId.Idle)
            {
                _machine.ChangeState(StickmanStateId.Idle);
            }
            _machine = null;
            _battle = null;
            _blackboard = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        private void OnRequested(DialogueIntent intent) => _requested.Add(intent);

        // 무클릭 자동 릴리즈(r = 1.0) 경로만 쓴다 — 실측 로그가 정확히 그 무인 경로다.
        private const float ChargeSeconds = 1.6f;
        private const float RetryDelaySeconds = 1.5f;
        private const float GenerousTimeoutSeconds = 600f; // 타임아웃이 끼어들지 않는 넉넉한 값.

        /// <summary>게이지가 만충될 때까지 틱을 돌린다(자동 릴리즈 → 판정 self-transition).</summary>
        private void TickThroughCharge() => _machine.Tick(ChargeSeconds);

        /// <summary>판정 후 대기(재도전 간격)를 소진시킨다 → 재충전 self-transition.</summary>
        private void TickThroughResolveDelay() => _machine.Tick(RetryDelaySeconds);

        private static bool IsOpeningLine(string text)
            => text == "천천히... 모은다" || text == "빠르다, 집중";

        // ==================== 위반 (a): 직전 판정 대사가 만료되지 않았다 ====================

        [Test]
        public void 재충전은_직전_판정_대사를_만료시킨다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            TickThroughCharge(); // 1회차 판정(무클릭 → NoInput 실패, 재도전 남음).

            DialogueIntent verdict = _requested[_requested.Count - 1];
            Assert.AreEqual(StickmanStateId.BattleMinigame, verdict.StateId);
            Assert.AreEqual(DialogueKind.Reaction, verdict.Kind, "판정 대사는 점 사건 서술(Reaction)이다.");
            Assert.IsTrue(verdict.IsValid, "사전 조건 — 판정 직후에는 유효해야 한다.");

            TickThroughResolveDelay(); // 재충전.

            Assert.IsFalse(verdict.IsValid,
                "재도전 게이지가 다시 차기 시작했는데 직전 판정 대사가 아직 유효하다 — " +
                "기를 다시 모으는 그림 위에 기가 빠졌다는 문장이 남는다(절대 불변 원칙 1 위반). " +
                "재충전이 ChangeState가 아니라 BeginCharge() 직접 호출이면 정확히 이렇게 된다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 위 단언이 "무엇이든 항상 만료된다"로 통과하는 것이 아님을 보인다.
        /// 판정 직후부터 재충전 직전까지는 <b>반드시 유효</b>해야 한다(그래야 판정 대사가 화면에서
        /// 제 몫의 가독예산을 받는다).
        /// </summary>
        [Test]
        public void 네거티브_판정_대사는_재충전_전까지는_유효하다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            TickThroughCharge();

            DialogueIntent verdict = _requested[_requested.Count - 1];

            // 재도전 간격의 절반만 흘려보낸다 — 아직 재충전 전이는 일어나지 않았다.
            _machine.Tick(RetryDelaySeconds * 0.5f);

            Assert.IsTrue(verdict.IsValid,
                "판정 대기 구간인데 판정 대사가 벌써 만료됐다 — 그러면 위 만료 단언은 " +
                "'항상 참'이 되어 아무것도 검사하지 않는다.");
        }

        // ==================== 위반 (b): 재도전에 개시 대사가 없었다 ====================

        [Test]
        public void 재충전마다_개시_대사가_다시_나온다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);

            int openingsAfterFirstEnter = CountOpenings();
            Assert.AreEqual(1, openingsAfterFirstEnter, "최초 진입에서 개시 대사가 정확히 1회 나와야 한다.");

            TickThroughCharge();        // 1회차 판정
            TickThroughResolveDelay();  // 재충전 → 개시 대사 2회차
            Assert.AreEqual(2, CountOpenings(),
                "재도전 게이지가 다시 차는데 개시 대사가 나오지 않았다 — 재충전이 Enter()를 거치지 " +
                "않으면 개시 분기가 구조적으로 도달 불가가 된다(실측: 3사이클 동안 개시 대사 1회).");

            TickThroughCharge();        // 2회차 판정
            TickThroughResolveDelay();  // 재충전 → 개시 대사 3회차
            Assert.AreEqual(3, CountOpenings(), "재충전마다 개시 대사가 나와야 한다(사이클 3회차).");
        }

        [Test]
        public void 재충전_사이클에서_같은_문장이_자기_자신을_교체하지_않는다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            TickThroughCharge();
            TickThroughResolveDelay();
            TickThroughCharge();

            // 실측의 글리치는 "판정 → (아무 것도 없이) 다음 판정"이라 같은 문자열이 연속으로 요청된
            // 것이었다. 재충전이 전이가 되면 그 사이에 반드시 개시 대사가 한 줄 들어간다.
            for (int i = 1; i < _requested.Count; i++)
            {
                Assert.AreNotEqual(_requested[i - 1].Text, _requested[i].Text,
                    $"연속된 두 대사가 같은 문자열이다(#{i - 1}, #{i}: \"{_requested[i].Text}\") — " +
                    "화면에서는 아무 이유 없이 같은 글자가 한 번 더 팝인하는 렌더 글리치로 보인다.");
            }
        }

        // ==================== 개시 대사의 계획 체류는 타임아웃 잔여로도 상한된다 ====================

        [Test]
        public void 무입력_타임아웃_잔여가_짧으면_재충전_개시_대사를_보류한다()
        {
            // 1회차 충전(1.6) + 재도전 대기(1.5) = 3.1초를 쓴 시점에 재충전이 일어나도록 잡고,
            // 타임아웃을 3.6초로 두면 재충전 시점의 잔여는 0.5초다.
            // 필요체류("천천히... 모은다" 10자) = 0.06 + 1.03 = 1.09초 > 0.5초 → 보류돼야 한다.
            _config.battleInputTimeoutSeconds = ChargeSeconds + RetryDelaySeconds + 0.5f;

            _machine.Start(StickmanStateId.BattleMinigame);
            int openingsBefore = CountOpenings();

            TickThroughCharge();
            TickThroughResolveDelay(); // 재충전 — 그러나 남은 시간이 게이지보다 훨씬 짧다.

            Assert.AreEqual(StickmanStateId.BattleMinigame, _machine.CurrentStateId,
                "사전 조건 — 아직 타임아웃 전이라 격파 상태여야 한다.");
            Assert.AreEqual(openingsBefore, CountOpenings(),
                "게이지를 다 채우기 전에 무입력 타임아웃이 끊을 것이 확정인데 개시 대사를 냈다 — " +
                "규칙 8이 없애려던 '번쩍이고 사라지는 글자'가 정확히 이 자리에서 생긴다. " +
                "상태가 이미 아는 종료 사유를 게이트에 넘기지 않으면 게이트는 알 방법이 없다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 위 검사가 "재충전에서는 늘 침묵"으로 통과하는 것이 아님을 보인다.</summary>
        [Test]
        public void 네거티브_타임아웃이_넉넉하면_재충전_개시_대사가_나온다()
        {
            _config.battleInputTimeoutSeconds = GenerousTimeoutSeconds;

            _machine.Start(StickmanStateId.BattleMinigame);
            int openingsBefore = CountOpenings();

            TickThroughCharge();
            TickThroughResolveDelay();

            Assert.AreEqual(openingsBefore + 1, CountOpenings(),
                "타임아웃 여유가 충분한데도 재충전 개시 대사가 침묵했다 — 그러면 위 보류 검사는 " +
                "'항상 참'이 되어 아무것도 검사하지 않는다.");
        }

        // ==================== 재충전이 대결을 끝내지 않는다(회귀 방어) ====================

        [Test]
        public void 재충전_자기전이는_상태를_벗어나지_않고_재도전_횟수를_그대로_잇는다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);

            // maxRetries = 3 → 총 시도 4회. 4회를 모두 실패해야 소진(Idle 복귀)이다.
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                Assert.AreEqual(StickmanStateId.BattleMinigame, _machine.CurrentStateId,
                    $"{attempt}번째 시도가 시작되기 전에 이미 격파 상태를 벗어났다.");
                TickThroughCharge();
                TickThroughResolveDelay();
            }

            Assert.AreEqual(StickmanStateId.Idle, _machine.CurrentStateId,
                "재도전 4회를 모두 실패했는데 격파가 끝나지 않았다 — 재충전 자기-전이가 " +
                "_retryCount를 리셋해 버리면(FreshStart로 오인하면) 대결이 영원히 끝나지 않는다.");
        }

        private int CountOpenings()
        {
            int n = 0;
            foreach (DialogueIntent intent in _requested)
            {
                if (IsOpeningLine(intent.Text)) n++;
            }
            return n;
        }
    }
}
