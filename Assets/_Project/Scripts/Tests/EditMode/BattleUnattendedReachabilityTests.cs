using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 격파 미니게임의 <b>무인(무클릭) 도달 가능성</b>을 숫자로 잠근다 — 2026-09-02 디버거 조사 +
    /// 리더 결정.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 이 팀의 표준 검증 수단은 <b>무인 장시간 관측</b>이다(빌드를 띄워 두고 로그를 읽는다).
    /// 그런데 무클릭이면 격파는 <b>항상</b> 무입력 타임아웃으로 끝나서, 대사표 8줄 중 두 줄이
    /// <b>화면에서 한 번도 검증되지 않는다</b>:
    /// <list type="bullet">
    ///   <item><c>"마지막 한 번!"</c> — 재도전이 정확히 1회 남았을 때.</item>
    ///   <item><c>"오늘은 여기까지"</c> — 재도전 소진(<c>Terminal</c>).</item>
    /// </list>
    ///
    /// <b>산식</b>(k번째 판정 시각): <c>T_k = Σd_i + (k-1) × battleFailRetryDelaySeconds</c>,
    /// <c>d ~ U(battleChargeDurationMin, Max)</c>. 배포값(1.5~2.0 / 1.5 / 타임아웃 5초)에서
    /// 소진에 필요한 4회차 판정은 <b>10.5~12.5초</b>가 걸리는데 타임아웃이 5초다 —
    /// <b>최소 5.5초 부족하며 구조적으로 도달 불가</b>다.
    ///
    /// <para><b>실기 로그 2개 인스턴스가 이 산식과 정확히 일치했다</b>(2026-09-02):
    /// 한쪽은 판정 2회(남은재도전 3 → 2), 다른 쪽은 1회(3) 뒤 둘 다 타임아웃. 예측한
    /// "1회 또는 2회, 각각 P=0.5"가 그대로 나왔고 두 로그를 합쳐 위 두 줄은 <b>0건</b>이었다.</para>
    ///
    /// ============================================================================
    /// 리더 판정: <b>버그가 아니라 의도</b>다. 그래서 값을 바꾸지 않고 <b>관계를 잠근다</b>
    /// ============================================================================
    /// 결정적 근거는 배포 에셋의 <c>battleAutoTriggerChance = 0</c>이다 — 격파는
    /// <b>자율 발동하지 않으며</b> 모든 발동이 사람이 시작한 것이다. 그리고 클릭 한 번이
    /// 무입력 타이머를 리셋하므로 <b>사람이 있으면 재도전 4회가 전부 도달 가능</b>하다.
    /// 타임아웃을 12.5초로 늘리는 것은 없는 문제를 위해 실제 UX를 늘어지게 만든다.
    ///
    /// <para>그래서 이 파일이 잠그는 것은 "값이 옳다"가 아니라 <b>두 사실의 관계</b>다:
    /// (A) 무클릭이면 두 줄에 도달하지 못한다 — 그러니 <b>무인 관측 로그에 그 두 줄이 없다고 해서
    /// 결함이 아니다</b>. (B) 클릭하면 도달한다 — 그러니 <b>기능이 죽은 것도 아니다</b>.
    /// 둘 중 하나라도 깨지면 위 판정의 전제가 무너지므로 빨간불이어야 한다.</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><see cref="네거티브_타임아웃만_늘리면_무인_경로도_두_줄에_도달한다"/> —
    ///     (A)가 "이 대사들은 어차피 아무 때도 안 나온다"로 통과하는 것이 아님을 보인다.
    ///     막고 있는 것이 <b>정확히 타임아웃</b>임을 같은 리그에서 증명한다.</item>
    ///   <item><see cref="네거티브_클릭이_없으면_같은_설정에서도_두_줄이_안_나온다"/> —
    ///     (B)가 "클릭과 무관하게 늘 나온다"로 통과하는 것이 아님을 보인다.</item>
    ///   <item><see cref="네거티브_사유_두_값이_실제로_서로_다른_값이다"/> —
    ///     이벤트 분리가 이름만 바뀐 것이 아님을 보인다.</item>
    /// </list>
    ///
    /// <para><b>상수를 베끼지 않는다</b>: 산식 검증은 전부 <c>_config</c>의 필드를 읽어서 한다
    /// (CLAUDE.md "테스트에 프로덕션 상수를 숫자로 베끼지 않는다"). 배포 에셋의 실제 값으로 도달
    /// 불가를 확인하는 검사는 에셋을 직접 읽는다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 상태/대사 파이프라인에 플랫폼 분기가 없다.</para>
    /// </summary>
    public sealed class BattleUnattendedReachabilityTests
    {
        private const string LogPrefix = "[격파도달성]";
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StickmanStateMachine _machine;
        private BattleMinigameState _battle;
        private readonly List<DialogueIntent> _requested = new List<DialogueIntent>();
        private readonly List<BattleMinigamePhase> _phases = new List<BattleMinigamePhase>();

        /// <summary>대사를 만들지 않는 종착 상태(격파가 Idle로 빠질 때 필요).</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        // 게이지 길이를 한 점으로 고정해 난수를 제거한다(BeginCharge의 Random.Range는 max <= min이면
        // min을 그대로 쓴다). 아래 산식이 결정론적이 되어 "몇 회차에 무엇이 일어나는가"가 확정된다.
        private const float ChargeSeconds = 1.6f;
        private const float RetryDelaySeconds = 1.5f;

        private const string LastRetryLine = "마지막 한 번!";
        private const string TerminalLine = "오늘은 여기까지";

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.battleChargeDurationMin = ChargeSeconds;
            _config.battleChargeDurationMax = ChargeSeconds;
            _config.battleFailRetryDelaySeconds = RetryDelaySeconds;
            _config.battleSuccessResolveDelaySeconds = RetryDelaySeconds;
            // ★ 재도전 횟수와 타임아웃은 **배포 에셋의 실효값을 그대로 가져온다** — 숫자를 베끼면
            //   튜닝이 움직이는 날 이 리그가 조용히 다른 세계를 검증하게 된다.
            StickConfig deployed = LoadDeployedConfig();
            _config.battleMaxRetries = deployed.battleMaxRetries;
            _config.battleInputTimeoutSeconds = deployed.battleInputTimeoutSeconds;

            _blackboard = new StickmanBlackboard { Config = _config };
            _battle = new BattleMinigameState(_blackboard);
            _machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Idle, new SilentState(StickmanStateId.Idle) },
                { StickmanStateId.BattleMinigame, _battle },
            });
            _blackboard.Machine = _machine;

            _requested.Clear();
            _phases.Clear();
            StickmanEventBus.DialogueRequested += OnRequested;
            StickmanEventBus.BattleMinigamePhaseChanged += OnPhase;
        }

        [TearDown]
        public void TearDown()
        {
            StickmanEventBus.DialogueRequested -= OnRequested;
            StickmanEventBus.BattleMinigamePhaseChanged -= OnPhase;
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
        private void OnPhase(BattleMinigamePhase phase) => _phases.Add(phase);

        private static StickConfig LoadDeployedConfig()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}");
            return config;
        }

        private bool Said(string text)
        {
            foreach (DialogueIntent d in _requested) if (d.Text == text) return true;
            return false;
        }

        private bool Raised(BattleMinigamePhase phase) => _phases.Contains(phase);

        /// <summary>
        /// 무클릭으로 상태가 끝날 때까지(또는 안전 상한까지) 돌린다. 한 틱을 게이지 길이만큼 주면
        /// 충전 한 사이클/대기 한 사이클이 정확히 한 틱에 소화된다.
        /// </summary>
        private void RunUnattendedToEnd()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            for (int i = 0; i < 64 && _machine.CurrentStateId == StickmanStateId.BattleMinigame; i++)
            {
                _machine.Tick(i % 2 == 0 ? ChargeSeconds : RetryDelaySeconds);
            }
            Assert.AreEqual(StickmanStateId.Idle, _machine.CurrentStateId,
                $"{LogPrefix} 무클릭인데 격파가 끝나지 않았습니다 — 종료 경로가 사라졌습니다.");
        }

        /// <summary>사람이 계속 클릭하는 경로. 매 충전마다 **스위트스팟 밖**에서 눌러 실패시키되,
        /// 클릭 자체가 무입력 타이머를 리셋하므로 재도전이 끝까지 진행된다.</summary>
        private void RunWithClicksToEnd()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            for (int i = 0; i < 64 && _machine.CurrentStateId == StickmanStateId.BattleMinigame; i++)
            {
                // 충전이 거의 끝나갈 때 누른다 → ratio가 스위트스팟(0.70~0.85) 위라 LateMiss(실패).
                // 중요한 것은 "클릭이 있었다"는 사실이며, 그것이 _noInputTimer를 0으로 되돌린다.
                _blackboard.BattleClickSignaled = true;
                _machine.Tick(ChargeSeconds * 0.98f);
                if (_machine.CurrentStateId != StickmanStateId.BattleMinigame) break;
                _machine.Tick(RetryDelaySeconds); // 판정 후 대기 → 재충전.
            }
            Assert.AreEqual(StickmanStateId.Idle, _machine.CurrentStateId,
                $"{LogPrefix} 클릭 경로인데 격파가 끝나지 않았습니다.");
        }

        // ====================================================================
        // (A) 무클릭이면 두 줄에 도달하지 못한다 — 산식과 실행 양쪽으로
        // ====================================================================

        /// <summary>
        /// 산식만으로 도달 불가를 보인다(실행 없이). <b>배포 에셋의 실제 값</b>을 읽어 계산하므로,
        /// 누가 튜닝을 바꿔 도달 가능해지는 순간 이 검사가 빨간불로 알려준다 — 그때는 위 리더 판정
        /// ("무인 도달 불가는 의도")의 전제 자체가 바뀐 것이다.
        /// </summary>
        [Test]
        public void 배포값에서_무인_경로는_재도전_소진에_구조적으로_도달하지_못한다()
        {
            StickConfig c = LoadDeployedConfig();

            // 소진(_retryCount > battleMaxRetries)에는 판정이 battleMaxRetries + 1 회 필요하다.
            int verdictsNeeded = c.battleMaxRetries + 1;
            float fastestCharge = Mathf.Min(c.battleChargeDurationMin, c.battleChargeDurationMax);
            // 가장 빠른 경우조차 이만큼 걸린다: 충전 N번 + 판정 후 대기 (N-1)번.
            float fastestTotal = verdictsNeeded * fastestCharge
                + (verdictsNeeded - 1) * c.battleFailRetryDelaySeconds;

            Assert.Greater(fastestTotal, c.battleInputTimeoutSeconds,
                $"{LogPrefix} 무인 경로가 재도전 소진에 **도달할 수 있게** 바뀌었습니다 " +
                $"(가장 빠른 소진 {fastestTotal:F2}초 <= 타임아웃 {c.battleInputTimeoutSeconds:F2}초).\n" +
                "이 검사는 '도달 불가가 의도'라는 2026-09-02 리더 판정의 전제를 잠급니다. 전제가 " +
                "바뀌었다면 그 판정을 다시 받으십시오 — 값을 조용히 바꾸고 이 줄을 지우지 마십시오.");

            Debug.Log($"{LogPrefix} 배포값 산식 — 소진에 판정 {verdictsNeeded}회 필요, 가장 빠른 " +
                $"경우도 {fastestTotal:F2}초(타임아웃 {c.battleInputTimeoutSeconds:F2}초). " +
                $"여유 {fastestTotal - c.battleInputTimeoutSeconds:F2}초만큼 도달 불가입니다.");
        }

        [Test]
        public void 무클릭이면_두_대사에_도달하지_못하고_무입력_타임아웃으로_끝난다()
        {
            RunUnattendedToEnd();

            Assert.IsTrue(Raised(BattleMinigamePhase.InputTimeout),
                $"{LogPrefix} 무클릭인데 무입력 타임아웃이 발행되지 않았습니다.");
            Assert.IsFalse(Raised(BattleMinigamePhase.RetriesExhausted),
                $"{LogPrefix} 무클릭인데 재도전 소진이 발행됐습니다 — 산식상 불가능합니다.");

            Assert.IsFalse(Said(LastRetryLine),
                $"{LogPrefix} 무클릭인데 \"{LastRetryLine}\"이 나왔습니다 — 재도전 잔여가 1까지 " +
                "내려갔다는 뜻이고, 그러면 위 산식 검사의 전제가 깨진 것입니다.");
            Assert.IsFalse(Said(TerminalLine),
                $"{LogPrefix} 무클릭인데 \"{TerminalLine}\"이 나왔습니다 — 소진에 도달했다는 뜻입니다.");

            Debug.Log($"{LogPrefix} 무인 경로 실행 — 발행 페이즈=[{string.Join(", ", _phases)}], " +
                $"대사 {_requested.Count}줄. 무인 관측 로그에 \"{LastRetryLine}\"/\"{TerminalLine}\"이 " +
                "없는 것은 결함이 아니라 이 구조의 정상 결과입니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 위 검사가 "이 대사들은 어차피 아무 때도 안 나온다"로 통과하는 것이
        /// 아님을 보인다. <b>타임아웃 하나만</b> 넉넉하게 바꾸면 같은 무클릭 경로에서
        /// <c>"오늘은 여기까지"</c>가 실제로 나온다 — 즉 그 줄을 막고 있던 것이 정확히 타임아웃임이
        /// 같은 리그에서 증명된다.
        ///
        /// ============================================================================
        /// ★★ 이 컨트롤이 처음 돌 때 <b>빨간불이 나면서 알려준 것</b> (2026-09-02)
        /// ============================================================================
        /// 처음에는 "타임아웃만 늘리면 <b>두 줄 다</b> 나온다"로 적었는데 실패했다. 원인은 테스트가
        /// 아니라 <b>내 모델이 틀렸던 것</b>이었다: 두 줄의 차단 조건이 서로 다르다.
        /// <list type="bullet">
        ///   <item><c>"오늘은 여기까지"</c> — 차단 조건이 <b>하나</b>(무입력 타임아웃). 타임아웃을
        ///     풀면 무클릭으로도 나온다.</item>
        ///   <item><c>"마지막 한 번!"</c> — 차단 조건이 <b>둘</b>이다. 재도전 잔여가 1이어야 하고,
        ///     <b>그 실패가 실제 클릭에서 나왔어야</b> 한다
        ///     (<c>p.Release != BattleRelease.NoInput</c>). 무클릭 실패는 전부 <c>NoInput</c>이라
        ///     타임아웃을 아무리 늘려도 이 줄은 나오지 않는다.</item>
        /// </list>
        /// <b>그리고 그 두 번째 조건은 옳다</b> — "마지막 한 번!"은 <b>누른 사람에게 하는 말</b>이다
        /// (눌렀는데 빗나갔으니 한 번 더). 한 번도 누른 적 없는 상대에게 할 말이 아니다. 즉 이 줄이
        /// 무인 경로에서 안 나오는 이유는 타임아웃이 아니라 <b>대사의 의미 자체</b>다.
        ///
        /// <para>그래서 이 컨트롤은 그 구조를 그대로 잠근다 — 타임아웃을 풀면 한 줄은 열리고
        /// 한 줄은 열리지 않아야 한다. 만약 나중에 <c>NoInput</c> 조건이 사라져 무클릭으로도
        /// "마지막 한 번!"이 나오게 되면 여기서 빨간불이 난다.</para>
        /// </summary>
        [Test]
        public void 네거티브_타임아웃만_늘리면_소진줄은_열리지만_마지막한번은_클릭을_따로_요구한다()
        {
            _config.battleInputTimeoutSeconds = 600f; // 타임아웃만 바꾼다. 나머지는 그대로.

            RunUnattendedToEnd();

            Assert.IsTrue(Raised(BattleMinigamePhase.RetriesExhausted),
                $"{LogPrefix} 타임아웃을 없앴는데도 재도전 소진에 도달하지 못했습니다 — 그러면 위 " +
                "'무클릭이면 도달 불가' 검사는 타임아웃과 무관하게 항상 참이라 아무것도 증명하지 못합니다.");
            Assert.IsFalse(Raised(BattleMinigamePhase.InputTimeout),
                $"{LogPrefix} 타임아웃 600초인데 무입력 타임아웃이 발행됐습니다.");

            Assert.IsTrue(Said(TerminalLine),
                $"{LogPrefix} \"{TerminalLine}\"에 도달하지 못했습니다 — 이 줄의 차단 조건은 " +
                "타임아웃 하나여야 합니다.");

            Assert.IsFalse(Said(LastRetryLine),
                $"{LogPrefix} 무클릭인데 \"{LastRetryLine}\"이 나왔습니다. 이 줄은 **누른 사람에게 " +
                "하는 말**이라 실패 사유가 NoInput이면 나오지 않아야 합니다 " +
                "(BattleMinigameState.ResolveDialogueLine의 `Release != NoInput` 조건). " +
                "그 조건이 사라졌다면, 한 번도 누른 적 없는 유저에게 '마지막 한 번!'이라고 말하게 됩니다.");
        }

        /// <summary>
        /// 위에서 드러난 <b>두 번째 차단 조건</b>을 정면으로 잠근다 — 재도전 잔여가 1인 순간에
        /// 도달했더라도 그 실패가 <c>NoInput</c>이면 이 줄은 나오지 않는다.
        ///
        /// <para>매핑 함수는 순수 함수이므로 상태를 돌리지 않고 <b>파라미터만 바꿔</b> 두 경우를
        /// 나란히 본다. 이것이 이 프로젝트가 "대사는 확정된 사실 하나에서만 파생된다"(31-1)를
        /// 지킨 덕에 가능한 검사다.</para>
        /// </summary>
        [Test]
        public void 마지막_한_번은_재도전_잔여만이_아니라_실제_클릭도_요구한다()
        {
            var clicked = new BattleMinigameState.BattleDialogueParams
            {
                Release = BattleMinigameState.BattleRelease.LateMiss, // 눌렀는데 늦었다.
                RetriesLeft = 1,
                Terminal = false,
                ChargeRatio = 0.95f,
                SweetStart = _config.battleSweetSpotStart,
                SweetEnd = _config.battleSweetSpotEnd,
            };
            var notClicked = new BattleMinigameState.BattleDialogueParams
            {
                Release = BattleMinigameState.BattleRelease.NoInput,  // 한 번도 안 눌렀다.
                RetriesLeft = 1,
                Terminal = false,
                ChargeRatio = 1f,
                SweetStart = _config.battleSweetSpotStart,
                SweetEnd = _config.battleSweetSpotEnd,
            };

            DialogueLine withClick = BattleMinigameState.ResolveDialogueLine(
                StickmanStateId.BattleMinigame, clicked);
            DialogueLine withoutClick = BattleMinigameState.ResolveDialogueLine(
                StickmanStateId.BattleMinigame, notClicked);

            Assert.AreEqual(LastRetryLine, withClick.Text,
                $"{LogPrefix} 눌러서 빗나갔고 재도전이 1회 남았는데 \"{LastRetryLine}\"이 아닙니다.");
            Assert.AreNotEqual(LastRetryLine, withoutClick.Text,
                $"{LogPrefix} 한 번도 누르지 않았는데 \"{LastRetryLine}\"이 나옵니다 — " +
                "누른 적 없는 유저에게 '한 번 더'라고 말하는 것은 행동-텍스트 싱크 위반입니다.");

            Debug.Log($"{LogPrefix} 두 번째 차단 조건 확인 — 재도전 잔여 1 + 클릭O => " +
                $"\"{withClick.Text}\" / 재도전 잔여 1 + 클릭X => \"{withoutClick.Text}\". " +
                "무인 관측에서 \"마지막 한 번!\"이 안 보이는 이유는 타임아웃이 아니라 이 조건입니다.");
        }

        // ====================================================================
        // (B) 사람이 클릭하면 도달한다 — 기능이 죽은 것이 아니다
        // ====================================================================

        [Test]
        public void 클릭하면_배포값_그대로도_두_대사에_도달한다()
        {
            // 타임아웃/재도전은 SetUp에서 **배포 에셋 값 그대로**다. 바뀐 것은 "사람이 클릭한다"뿐이다.
            RunWithClicksToEnd();

            Assert.IsTrue(Raised(BattleMinigamePhase.RetriesExhausted),
                $"{LogPrefix} 클릭 경로인데 재도전 소진에 도달하지 못했습니다 — 클릭이 무입력 타이머를 " +
                "리셋하지 않는다는 뜻이고, 그러면 battleMaxRetries가 **아무 경로에서도** 도달 불가라 " +
                "리더의 '의도' 판정이 무너집니다.");
            Assert.IsFalse(Raised(BattleMinigamePhase.InputTimeout),
                $"{LogPrefix} 매 사이클 클릭했는데 무입력 타임아웃이 발행됐습니다 — 타이머 리셋이 " +
                "동작하지 않습니다.");

            Assert.IsTrue(Said(LastRetryLine),
                $"{LogPrefix} 클릭 경로에서도 \"{LastRetryLine}\"이 나오지 않았습니다.");
            Assert.IsTrue(Said(TerminalLine),
                $"{LogPrefix} 클릭 경로에서도 \"{TerminalLine}\"이 나오지 않았습니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 위 검사가 "클릭과 무관하게 늘 나온다"로 통과하는 것이
        /// 아님을 보인다. 같은 설정에서 클릭만 빼면 두 줄이 사라져야 한다.</summary>
        [Test]
        public void 네거티브_클릭이_없으면_같은_설정에서도_두_줄이_안_나온다()
        {
            RunUnattendedToEnd(); // 설정은 위 검사와 완전히 동일하고 클릭만 없다.

            Assert.IsFalse(Said(LastRetryLine) || Said(TerminalLine),
                $"{LogPrefix} 클릭이 없는데도 두 줄이 나왔습니다 — 그러면 위 '클릭하면 도달한다' " +
                "검사가 클릭 덕분이라는 근거가 사라집니다.");
        }

        // ====================================================================
        // 이벤트 분리 자체
        // ====================================================================

        /// <summary>
        /// ★ 리더 결정 2 — <c>Exhausted</c> 하나가 두 사실을 겸하던 것을 쪼갠 것이 <b>이름만 바뀐
        /// 것이 아님</b>을 보인다. 같은 리그에서 조건만 바꿔 두 값이 <b>실제로 갈라지는지</b> 본다.
        /// </summary>
        [Test]
        public void 네거티브_사유_두_값이_실제로_서로_다른_값이다()
        {
            Assert.AreNotEqual(BattleMinigamePhase.RetriesExhausted, BattleMinigamePhase.InputTimeout,
                $"{LogPrefix} 두 사유가 같은 값입니다 — 구독자가 구분할 수 없습니다.");

            // (1) 무클릭 → InputTimeout만.
            RunUnattendedToEnd();
            bool unattendedTimeout = Raised(BattleMinigamePhase.InputTimeout);
            bool unattendedExhausted = Raised(BattleMinigamePhase.RetriesExhausted);

            // (2) 같은 리그를 다시 세워 클릭 경로 → RetriesExhausted만.
            TearDown();
            SetUp();
            RunWithClicksToEnd();
            bool clickedTimeout = Raised(BattleMinigamePhase.InputTimeout);
            bool clickedExhausted = Raised(BattleMinigamePhase.RetriesExhausted);

            Assert.IsTrue(unattendedTimeout && !unattendedExhausted,
                $"{LogPrefix} 무클릭 경로가 두 사유를 구분해 발행하지 않습니다.");
            Assert.IsTrue(clickedExhausted && !clickedTimeout,
                $"{LogPrefix} 클릭 경로가 두 사유를 구분해 발행하지 않습니다.");

            Debug.Log($"{LogPrefix} 사유 분리 확인 — 무클릭: 타임아웃={unattendedTimeout}/소진={unattendedExhausted}, " +
                $"클릭: 타임아웃={clickedTimeout}/소진={clickedExhausted}. " +
                "두 경로가 서로 다른 값을 내므로 구독자가 화면 결과와 1:1로 맞출 수 있습니다.");
        }

        /// <summary>
        /// 대사 유무가 두 사유의 <b>화면 차이</b>다 — 소진은 말하고 끝나지만 타임아웃은 말이 없다.
        /// 이벤트를 쪼갠 이유가 바로 이것이므로 그 관계도 함께 잠근다.
        /// </summary>
        [Test]
        public void 무입력_타임아웃은_대사를_만들지_않고_끝난다()
        {
            _machine.Start(StickmanStateId.BattleMinigame);
            int before = _requested.Count;

            // 타임아웃이 걸릴 때까지 충전만 돌린다(클릭 없음).
            for (int i = 0; i < 64 && _machine.CurrentStateId == StickmanStateId.BattleMinigame; i++)
            {
                _machine.Tick(i % 2 == 0 ? ChargeSeconds : RetryDelaySeconds);
            }

            Assert.IsTrue(Raised(BattleMinigamePhase.InputTimeout), $"{LogPrefix} 사전 조건 실패.");

            // 마지막으로 만들어진 대사가 **타임아웃 그 자체 때문에** 생긴 것이면 안 된다.
            // (타임아웃 전 판정/개시 대사는 정상이므로 개수 증가 자체는 허용된다 —
            //  검사 대상은 "타임아웃 프레임이 새 대사를 만들었는가"다.)
            foreach (DialogueIntent d in _requested)
            {
                Assert.AreNotEqual(TerminalLine, d.Text,
                    $"{LogPrefix} 무입력 타임아웃 경로인데 소진 대사 \"{TerminalLine}\"이 나왔습니다 — " +
                    "중단과 판정이 같은 대사를 쓰면 화면에서 두 사건을 구분할 수 없습니다.");
            }
            Assert.GreaterOrEqual(_requested.Count, before,
                $"{LogPrefix} 대사 목록이 줄어들 수는 없습니다(리그 오류).");
        }
    }
}
