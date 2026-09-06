using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 벽타기 진입 대사의 <b>자율 예산</b>과 <b>변주 풀</b> 회귀 —
    /// design-narrative 2026-09-06 R9(`design/narrative/2026-09-06_파쿠르대사_편중해소.md`) 구현분.
    ///
    /// ============================================================================
    /// 무엇이 신고됐나
    /// ============================================================================
    /// <c>persona-immersion</c> 3시간 실기: <b>「가뿐하네」가 전체 발화의 25.8%</b>, 등반 141회 중
    /// 137회(97.2%)가 같은 한 줄. 원인 중 이 코드의 몫은 하나다 — <c>ParkourClimbState.Enter</c>는
    /// 확률도 쿨다운도 없이 <b>무조건</b> 말하고 있었다(Idle/Walk 앰비언트에는 둘 다 있다).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 성질 (전부 양성/음성 대조를 짝으로 둔다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>공유 쿨다운에 「편입」됐는가</b> — 별도 타이머를 새로 만들면 두 타이머의 최소값이
    ///     다시 0이 되어 "앰비언트 직후 0.7초에 등반 대사"가 그대로 재현된다. 그래서 이 파일은
    ///     <b>앰비언트와 등반이 서로를 막는지</b>를 양방향으로 확인한다 — 같은 타이머를 쓰지 않으면
    ///     그 두 검사는 통과할 수 없다.</item>
    ///   <item><b>확률이 실제로 걸리는가</b> — 0이면 침묵, 1이면 발화, 그리고 배포 확률에서
    ///     실효 비율이 실제로 그 값 근처인가(추첨만 4,000회).</item>
    ///   <item><b>막힌 발화가 쿨다운을 태우지 않는가</b> — 확률에 막힌 경우와 규칙 8에 막힌 경우
    ///     <b>둘 다</b>. 먼저 태우면 "말할 시간이 없어서 침묵한" 대가로 다음 발화까지 벙어리가 된다.</item>
    ///   <item><b>티어별 변주가 균등한가</b> — 임계 상수를 <b>베끼지 않고</b> 높이를 훑어 경계를
    ///     스스로 찾아낸다. design-motion이 <c>MOTION_SPEC §21-3</c>으로 임계를 옮겨도 이 검사는 산다.</item>
    ///   <item>★ <b>T1(최저 티어)은 여전히 한 줄인가</b> — 확장이 <b>의도적으로 보류</b>된 자리다
    ///     (§4-5). 배포 임계에서 등반의 97.2%가 T1이고, 자기 키의 94.5%를 4박자로 기어오르며
    ///     "가볍다"고 말하는 것은 이미 모션과 어긋나 있다 — 여기에 문안을 더 넣으면 어긋난 말이
    ///     1종에서 4종으로 늘 뿐이다. 그래서 이 파일은 <b>T1이 늘어나면 빨개진다</b>. 임계 재조정
    ///     (<c>MOTION_SPEC §21-3</c>)이 착지하는 라운드가 이 단언을 <b>의식적으로</b> 풀도록
    ///     강제하는 것이 목적이다(조용히 흘러가면 그 라운드가 §4-5를 다시 어긴다).</item>
    ///   <item><b>새 문안이 회귀 검사에 닿는가</b> — 말뭉치/골든 <b>존재 단언</b>. 문안을
    ///     <c>string[]</c>로 빼는 순간 여기서 빨개진다(부재 단언이 아니라 존재 단언이라 시끄럽다).</item>
    ///   <item><b>에셋에 키가 실제로 있는가</b> — 코드 기본값만 고치고 에셋을 빠뜨리면 배포본에서
    ///     스위치가 꺼진 채 나간다(이 저장소의 거짓 통과 형태 #9). <c>AssetDatabase</c> 로드만으로는
    ///     그 둘을 <b>구분할 수 없다</b>(키가 없으면 코드 기본값이 그대로 보인다) — 그래서 YAML 원문도 본다.</item>
    /// </list>
    ///
    /// <para><b>숫자를 베끼지 않는다</b>: 확률·쿨다운·등반 길이는 전부 <see cref="StickConfig"/>에서
    /// 읽는다. 티어 임계는 <b>측정</b>한다(private const라 읽을 수도 없고, 읽으면 곧 베끼는 것이다).</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 등반 대사 경로에는 플랫폼 분기가 없다 — 이 상태는 발판의
    /// 정체를 묻지 않고 <b>높이 ÷ 신장</b>만 본다(그래서 macOS Dock이든 Windows 작업표시줄이든
    /// 같은 코드가 돈다).</para>
    /// </summary>
    public sealed class ParkourClimbChatterBudgetTests
    {
        private const string LogPrefix = "[등반대사예산-TEST]";
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>대사를 만들지 않는 출발 상태(PlannedDwellStateScopeTests와 같은 관례).</summary>
        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) => StateId = id;
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private sealed class StubIntent : IMovementIntentSource, IPlannedDwellSource
        {
            public float MoveInputX { get; set; }
            public float PlannedDwellRemainingSeconds { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickConfig _config;
        private StickmanBlackboard _blackboard;
        private StubIntent _intent;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();

            _randomState = Random.state;
            Random.InitState(20260906);

            _config = ScriptableObject.CreateInstance<StickConfig>();
            _config.dialogueBubbleEnabled = true;
            // 자극값이지 기대값이 아니다 — 대사가 길이 때문에 규칙 8에 걸리지 않을 만큼만 넉넉히 준다.
            _config.parkourClimbDuration = 3f;
            _config.ambientChatterCooldownSeconds = 17f;   // 배포값과 일부러 다르게 둔다(필드를 읽는지 보려고)
            _config.idleChatterChance = 1f;
            _config.walkChatterChance = 1f;

            _intent = new StubIntent { MoveInputX = 0f, PlannedDwellRemainingSeconds = 30f };
            _blackboard = new StickmanBlackboard { Config = _config, IntentSource = _intent };
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = _randomState;
            _blackboard = null;
            _intent = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
            AppSettingsModel.ResetForTesting();
        }

        // ================================================================================
        // 리그 — 실제 Enter() 경로로 진입시키고 발화 수를 센다
        // ================================================================================

        private int EnterClimbAndCountDialogue()
        {
            var climb = new ParkourClimbState(_blackboard);
            var machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Walk, new SilentState(StickmanStateId.Walk) },
                { StickmanStateId.ParkourClimb, climb },
            });
            _blackboard.Machine = machine;

            int count = 0;
            void OnRequested(DialogueIntent _) => count++;
            StickmanEventBus.DialogueRequested += OnRequested;
            try
            {
                machine.Start(StickmanStateId.Walk);
                machine.ChangeState(StickmanStateId.ParkourClimb);
            }
            finally
            {
                StickmanEventBus.DialogueRequested -= OnRequested;
                machine.ChangeState(StickmanStateId.Walk); // 살아 있는 DialogueIntent의 정적 구독을 정리한다.
            }
            return count;
        }

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}");
            return config;
        }

        // ================================================================================
        // 1. 확률 게이트
        // ================================================================================

        [Test]
        public void 확률이_0이면_등반_진입이_침묵하고_1이면_말한다()
        {
            // ★ 음성 — 확률 0.
            _config.parkourClimbChatterChance = 0f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(0, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 확률 0인데 등반 진입이 말했습니다 — 확률 게이트가 진입 경로에 배선되지 " +
                "않았다는 뜻입니다(추첨 함수만 있고 아무도 부르지 않는 형태).");

            // ★ 양성 — 같은 조건에서 확률만 1로. 이게 없으면 위 단언은 "언제나 침묵"과 구별되지 않는다.
            _config.parkourClimbChatterChance = 1f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(1, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 확률 1인데 침묵했습니다 — 게이트가 과잉 차단하고 있습니다(등반이 " +
                "통째로 벙어리가 되면 그건 해결이 아니라 증상을 반대쪽으로 옮긴 것입니다).");
        }

        /// <summary>
        /// ★ 배포 확률이 실제로 그 값으로 작동하는가 — <b>추첨만</b> 4,000회 돌려 실효 비율을 잰다.
        /// <para>기대값은 <see cref="StickConfig"/>에서 읽는다(0.35를 여기 적지 않는다). 씨앗이
        /// 고정돼 있으므로 결정론이지만, 허용 오차는 4σ보다 넉넉히 잡아 씨앗이 바뀌어도 안 흔들리게 한다.</para>
        /// </summary>
        [Test]
        public void 배포_확률이_실효_추첨_비율과_같다()
        {
            StickConfig deployed = LoadDeployedConfig();
            _config.parkourClimbChatterChance = deployed.parkourClimbChatterChance;

            float expected = AppSettingsModel.ResolveParkourClimbChatterChance(_config);
            Assert.Greater(expected, 0f, $"{LogPrefix} 배포 확률이 0입니다 — 등반 대사가 통째로 꺼져 있습니다.");
            Assert.Less(expected, 1f, $"{LogPrefix} 배포 확률이 1입니다 — 예산이 사실상 없는 것과 같습니다.");

            var climb = new ParkourClimbState(_blackboard);
            const int trials = 4000;
            int hits = 0;
            for (int i = 0; i < trials; i++)
            {
                _blackboard.NextChatterAllowedUnscaledTime = 0f; // 쿨다운 축은 여기서 재지 않는다.
                if (climb.TryRollClimbChatter()) hits++;
            }

            float actual = hits / (float)trials;
            Assert.AreEqual(expected, actual, 0.05f,
                $"{LogPrefix} 실효 추첨 비율 {actual:F3} 이 설정 확률 {expected:F3} 과 어긋납니다 " +
                $"({hits}/{trials}).");
        }

        /// <summary>
        /// ★★ 설정창 「잡담 빈도」 슬라이더가 <b>등반 대사에도</b> 걸린다.
        /// <para>이 배선(<c>ScaleChance</c> 통과)이 없으면 사용자가 잡담을 0%로 내려도 등반만 계속
        /// 말한다 — 쿨다운이 없던 시절과 <b>정확히 같은 병의 재발</b>이다.</para>
        /// </summary>
        [Test]
        public void 잡담빈도_슬라이더가_등반_확률에도_곱해진다()
        {
            // 배율의 절반이 상한(1.0)에 잘리지 않도록 중간값을 자극으로 쓴다(기대값이 아니다).
            _config.parkourClimbChatterChance = 0.4f;

            Assert.Greater(AppSettingsModel.ResolveParkourClimbChatterChance(_config), 0f,
                $"{LogPrefix} 기준 확률이 0이면 아래 배율 검사가 공허합니다.");

            AppSettingsModel.SetChatterPercent(100);
            float full = AppSettingsModel.ResolveParkourClimbChatterChance(_config);
            AppSettingsModel.SetChatterPercent(50);
            float half = AppSettingsModel.ResolveParkourClimbChatterChance(_config);
            Assert.AreEqual(full * 0.5f, half, 1e-5f,
                $"{LogPrefix} 50%가 100%의 절반이 아닙니다 — 슬라이더가 배율이 아닌 다른 방식으로 " +
                "걸려 있거나, 아예 안 걸립니다.");

            AppSettingsModel.SetChatterPercent(0);
            Assert.AreEqual(0f, AppSettingsModel.ResolveParkourClimbChatterChance(_config), 1e-6f,
                $"{LogPrefix} 잡담 빈도 0%인데 등반 확률이 남아 있습니다 — 사용자가 잡담을 껐는데 " +
                "등반만 계속 말합니다(쿨다운이 없던 시절과 정확히 같은 병입니다).");

            // ★ 해석이 아니라 거동으로 — 확률 필드가 1이어도 슬라이더가 0%면 진입이 침묵해야 한다.
            _config.parkourClimbChatterChance = 1f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(0, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 확률 필드가 1이면 슬라이더 0%를 무시하고 말했습니다.");

            // ★ 양성 대조 — 슬라이더를 되돌리면 같은 조건에서 말한다.
            AppSettingsModel.SetChatterPercent(100);
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(1, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 슬라이더를 100%로 되돌려도 침묵합니다 — 위 단언이 '언제나 침묵'과 " +
                "구별되지 않습니다.");
        }

        // ================================================================================
        // 2. 공유 쿨다운 — 「편입」이지 「신설」이 아니다
        // ================================================================================

        [Test]
        public void 쿨다운이_살아_있으면_침묵하고_만료되면_말한다()
        {
            _config.parkourClimbChatterChance = 1f;

            _blackboard.NextChatterAllowedUnscaledTime = Time.unscaledTime + 999f;
            Assert.AreEqual(0, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 공유 쿨다운이 살아 있는데 등반 진입이 말했습니다.");

            // ★ 양성 대조 — 같은 조건에서 쿨다운만 만료시키면 반드시 말해야 한다.
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(1, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 쿨다운이 만료됐는데도 침묵했습니다 — 위 단언이 '언제나 침묵'이라는 " +
                "오답과 구별되지 않습니다.");
        }

        /// <summary>
        /// ★★ <b>같은 타이머인가</b> — 앰비언트 잡담이 등반을 막고, 등반이 앰비언트를 막는다.
        /// <para>별도 타이머를 새로 만들었다면 이 두 단언은 <b>통과할 수 없다</b>. 그리고 그 분리가
        /// 정확히 원래 증상(앰비언트 직후 0.7초에 등반 대사)의 기제다.</para>
        /// </summary>
        [Test]
        public void 앰비언트와_등반이_하나의_타이머를_공유한다()
        {
            _config.parkourClimbChatterChance = 1f;

            // (가) 앰비언트가 먼저 말하면 등반이 막힌다.
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.IsTrue(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle,
                    new AmbientChatter.ChatterParams()),
                $"{LogPrefix} 앰비언트가 말하지 못했습니다 — 이 검사의 자극이 성립하지 않습니다.");
            Assert.AreEqual(0, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 앰비언트가 방금 말했는데 등반이 곧바로 또 말했습니다 — 타이머가 " +
                "쪼개져 있다는 뜻이고, 그게 신고된 '수다스럽다'의 실체입니다.");

            // (나) 반대 방향 — 등반이 먼저 말하면 앰비언트가 막힌다.
            _blackboard.NextChatterAllowedUnscaledTime = 0f;
            Assert.AreEqual(1, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 등반이 말하지 못했습니다 — (나)의 자극이 성립하지 않습니다.");
            Assert.IsFalse(AmbientChatter.TryRollChatter(_blackboard, StickmanStateId.Idle,
                    new AmbientChatter.ChatterParams()),
                $"{LogPrefix} 등반이 방금 말했는데 앰비언트가 곧바로 또 말했습니다 — 등반이 공유 " +
                "타이머를 재장전하지 않았다는 뜻입니다(읽기만 하고 쓰지 않는 반쪽 편입).");
        }

        [Test]
        public void 성공한_발화는_설정된_쿨다운만큼_재장전한다()
        {
            _config.parkourClimbChatterChance = 1f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;

            float before = Time.unscaledTime;
            Assert.AreEqual(1, EnterClimbAndCountDialogue());
            float after = Time.unscaledTime;

            float cooldown = _config.ambientChatterCooldownSeconds;   // 숫자를 베끼지 않는다
            float reloaded = _blackboard.NextChatterAllowedUnscaledTime;

            Assert.GreaterOrEqual(reloaded, before + cooldown - 1e-3f,
                $"{LogPrefix} 재장전된 쿨다운 만료 시각이 너무 이릅니다 — 설정값이 아니라 다른 값을 " +
                "쓰고 있을 수 있습니다.");
            Assert.LessOrEqual(reloaded, after + cooldown + 1e-3f,
                $"{LogPrefix} 재장전된 쿨다운 만료 시각이 너무 늦습니다.");
        }

        // ================================================================================
        // 3. 막힌 발화는 쿨다운을 태우지 않는다 (두 갈래 전부)
        // ================================================================================

        [Test]
        public void 확률에_막힌_발화는_쿨다운을_소비하지_않는다()
        {
            _config.parkourClimbChatterChance = 0f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;

            Assert.AreEqual(0, EnterClimbAndCountDialogue());
            Assert.AreEqual(0f, _blackboard.NextChatterAllowedUnscaledTime,
                $"{LogPrefix} 확률에 막힌(= 추첨이 없었던) 진입이 쿨다운을 태웠습니다 — 등반이 잦은 " +
                "구간에서 캐릭터가 말할 수 있는 순간을 통째로 잃습니다.");
        }

        /// <summary>
        /// ★ 규칙 8(발화 자격 게이트)에 막힌 경우 — <b>텍스트까지 만들었는데</b> 예산에 걸린 갈래다.
        /// <para>순서가 계약이다: <c>TryCreate</c>가 <c>null</c>을 돌려주면 쿨다운을 태우지 않는다.
        /// 이 검사가 없으면 "성공 여부와 무관하게 미리 태우는" 구현이 그대로 통과한다.</para>
        /// </summary>
        [Test]
        public void 규칙8에_막힌_발화도_쿨다운을_소비하지_않는다()
        {
            _config.parkourClimbChatterChance = 1f;

            // 계획 잔여 체류를 어떤 대사의 필요체류보다도 짧게 만든다(자극이지 기대값이 아니다).
            _config.parkourClimbDuration = 0.01f;
            _blackboard.NextChatterAllowedUnscaledTime = 0f;

            Assert.AreEqual(0, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 계획 잔여 체류 0.01초인데 대사가 나왔습니다 — 규칙 8이 이 경로에서 " +
                "동작하지 않습니다.");
            Assert.AreEqual(0f, _blackboard.NextChatterAllowedUnscaledTime,
                $"{LogPrefix} 규칙 8에 막힌 발화가 쿨다운을 태웠습니다 — '말할 시간이 없어서 침묵한' " +
                "대가로 다음 발화까지 벙어리가 됩니다(AmbientChatter가 같은 순서를 쓰는 이유).");

            // ★ 양성 대조 — 체류만 늘리면 같은 조건에서 말하고, 그때는 태운다.
            _config.parkourClimbDuration = 3f;
            Assert.AreEqual(1, EnterClimbAndCountDialogue(),
                $"{LogPrefix} 체류를 늘려도 침묵합니다 — 위 단언이 '언제나 침묵'과 구별되지 않습니다.");
            Assert.Greater(_blackboard.NextChatterAllowedUnscaledTime, 0f,
                $"{LogPrefix} 성공한 발화가 쿨다운을 태우지 않았습니다.");
        }

        // ================================================================================
        // 4. 티어 × 변주 — 임계를 베끼지 않고 측정한다
        // ================================================================================

        private static DialogueLine Line(float heightRatio, float variant01)
        {
            float h = StickConfig.BaselineCharacterTotalHeight;
            return ParkourClimbState.ResolveClimbLine(new ParkourClimbState.ParkourClimbDialogueParams
            {
                ClimbHeightUnits = heightRatio * h,
                CharacterHeightWorld = h,
                LineVariant01 = variant01,
            });
        }

        /// <summary>
        /// 높이 비율(H 배수)을 훑어 <b>티어 경계를 스스로 찾아낸다</b>. 임계 상수를 테스트에 적지
        /// 않으므로 design-motion이 <c>MOTION_SPEC §21-3</c>으로 임계를 옮겨도 이 검사는 그대로 산다.
        /// </summary>
        private static List<float> FindTierRepresentativeRatios()
        {
            const float max = 5f;
            const int steps = 2000;
            var segments = new List<(float lo, float hi)>();
            string previous = null;
            float segmentStart = 0f;

            for (int i = 0; i <= steps; i++)
            {
                float ratio = max * i / steps;
                string text = Line(ratio, 0f).Text;
                if (previous == null)
                {
                    previous = text;
                    segmentStart = ratio;
                    continue;
                }
                if (text == previous) continue;
                segments.Add((segmentStart, ratio));
                previous = text;
                segmentStart = ratio;
            }
            segments.Add((segmentStart, max));

            var representatives = new List<float>();
            foreach ((float lo, float hi) in segments) representatives.Add((lo + hi) * 0.5f);
            return representatives;
        }

        [Test]
        public void 등반_대사는_세_티어로_갈린다()
        {
            List<float> tiers = FindTierRepresentativeRatios();
            Assert.AreEqual(3, tiers.Count,
                $"{LogPrefix} 높이를 0~5H로 훑었더니 티어가 {tiers.Count}종입니다 — 3종(가벼움/중간/높음)이어야 " +
                "합니다. 티어가 줄었다면 임계가 범위 밖으로 나갔거나 분기가 사라진 것입니다.");
        }

        /// <summary>
        /// 한 티어의 변주 풀을 <b>실측</b>한다 — 문안 -> 뽑힌 횟수.
        /// <para>표본을 균등 격자로 훑으므로 <c>(int)(v × 줄수)</c>가 균등이면 몫도 균등해야 한다.</para>
        /// </summary>
        private static Dictionary<string, int> SampleTierPool(float ratio, int samples)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < samples; i++)
            {
                string text = Line(ratio, (i + 0.5f) / samples).Text;
                counts.TryGetValue(text, out int n);
                counts[text] = n + 1;
            }
            return counts;
        }

        /// <summary>
        /// ★ 각 티어 안에서 <c>LineVariant01</c>이 <b>균등하게</b> 갈리는가.
        /// <para>문안을 테스트에 베끼지 않는다 — <b>서로 다름</b>과 <b>추첨 몫이 같음</b>만 잰다.
        /// 균등 추첨의 표본을 4,000개로 잡으면 <c>(int)(v × 줄수)</c>는 정확히 같은 수로 갈려야 한다.</para>
        ///
        /// <para>★★ <b>최저 티어(T1)는 이 검사의 대상이 아니다</b> — 확장이 §4-5로 <b>보류</b>돼
        /// 한 줄뿐이고, 한 줄짜리 풀은 정의상 항상 균등하다. T1이 한 줄이라는 사실 자체는 아래
        /// 전용 검사가 따로 못박는다(여기서 "1종은 통과"로 흘려보내면 <b>T1이 나중에 조용히
        /// 늘어나도 아무도 모른다</b> — 이 파일이 막으려는 것이 정확히 그것이다).</para>
        /// </summary>
        [Test]
        public void T1_위_티어들_안에서_변주가_균등하게_갈린다()
        {
            const int samples = 4000;
            List<float> tiers = FindTierRepresentativeRatios();

            // FindTierRepresentativeRatios는 높이를 오름차순으로 훑으므로 [0]이 최저 티어(T1)다.
            // 그 전제가 깨지면 아래 Skip(1)이 엉뚱한 티어를 건너뛴다 -> 먼저 못박는다.
            for (int i = 1; i < tiers.Count; i++)
            {
                Assert.Greater(tiers[i], tiers[i - 1],
                    $"{LogPrefix} 티어 대표 높이가 오름차순이 아닙니다 — 최저 티어가 [0]이라는 전제가 " +
                    "깨졌습니다.");
            }

            var poolSizes = new List<int>();
            foreach (float ratio in tiers.Skip(1))
            {
                Dictionary<string, int> counts = SampleTierPool(ratio, samples);

                Assert.Greater(counts.Count, 1,
                    $"{LogPrefix} 높이 {ratio:F2}H 티어의 대사가 {counts.Count}종뿐입니다 — 변주가 " +
                    "이 티어에 배선되지 않았습니다(신고된 '97.2%가 한 줄'이 그 상태입니다).");

                int expectedPerLine = samples / counts.Count;
                foreach (KeyValuePair<string, int> kv in counts)
                {
                    Assert.AreEqual((double)expectedPerLine, kv.Value, 2d,
                        $"{LogPrefix} 높이 {ratio:F2}H 티어에서 \"{kv.Key}\" 가 {kv.Value}/{samples}회 " +
                        $"뽑혔습니다(균등이면 {expectedPerLine}회). 인덱스 매핑이 비균등입니다 — " +
                        "정수 인덱스에 나머지 연산을 쓰면 이렇게 됩니다.");
                }
                poolSizes.Add(counts.Count);
            }

            Assert.AreEqual(tiers.Count - 1, poolSizes.Count);
            Assert.Greater(poolSizes.Sum(), poolSizes.Count,
                $"{LogPrefix} T1 위 티어가 전부 1줄씩이면 이 라운드가 한 일이 없습니다.");
        }

        /// <summary>
        /// ★★ <b>T1(최저 티어)은 변주가 없다 — 언제나 같은 한 줄이다.</b> 이건 미구현이 아니라
        /// <b>확정된 보류</b>다(design-narrative §4-5).
        ///
        /// <para><b>왜 보류인가</b>: 배포 임계에서 등반의 97.2%가 T1로 떨어지는데(소은 실측 137/141),
        /// 자기 키의 94.5%를 양손 4박자로 기어오르며 "가볍다"고 말하는 것은 <b>이미 모션과 어긋나</b>
        /// 있다. 여기에 문안을 3줄 더 넣으면 어긋난 말이 1종에서 4종으로 늘 뿐이고, 개별 발화
        /// 빈도는 공유 쿨다운 + 확률이 이미 낮춰 놓았으므로 종수를 늘리는 실익이 없다.</para>
        ///
        /// <para><b>이 검사가 빨개지는 날</b>: design-motion의 임계 재조정
        /// (<c>MOTION_SPEC §21-3</c>, <c>0.95 → 0.4109 H</c>)이 착지해 배포 기본 등반이 T2로 옮겨 간
        /// 뒤라면, T1 확장 3줄(<c>이쯤이야</c>/<c>여긴 낮지</c>/<c>쉽다 쉬워</c>)을 넣고 이 검사를
        /// <b>의식적으로</b> 풀면 된다. 그 전에 빨개졌다면 §4-5를 다시 어긴 것이다.</para>
        ///
        /// <para>★ 이건 <b>부재 단언이 아니다</b>. "T1 문안 3종이 소스에 없다"로 짰다면 나중에 그
        /// 문안이 되살아나도 니들이 썩어 조용히 초록이 될 수 있다. 대신 <b>풀 크기를 실측</b>해
        /// 늘어나는 순간 시끄럽게 빨개지게 한다.</para>
        /// </summary>
        [Test]
        public void T1은_변주없이_언제나_같은_문안을_낸다()
        {
            const int samples = 4000;
            List<float> tiers = FindTierRepresentativeRatios();
            Assert.IsNotEmpty(tiers, $"{LogPrefix} 티어를 하나도 못 찾았습니다.");

            float t1 = tiers[0];
            Dictionary<string, int> counts = SampleTierPool(t1, samples);

            Assert.AreEqual(1, counts.Count,
                $"{LogPrefix} 최저 티어(높이 {t1:F2}H)의 대사가 {counts.Count}종입니다 — T1 확장은 " +
                "design-narrative 2026-09-06 §4-5로 **보류**된 상태입니다. 배포 임계에서 등반의 " +
                "97.2%가 이 티어이고, 자기 키의 94.5%를 4박자로 기어오르며 '가볍다'고 말하는 것은 " +
                "이미 모션과 어긋나 있습니다 — 문안을 늘리면 어긋난 말이 1종에서 4종으로 늘 뿐입니다. " +
                "MOTION_SPEC §21-3(0.95 → 0.4109 H)이 먼저 착지해 배포 기본 등반이 T2로 옮겨 간 " +
                "뒤에 이 단언과 함께 푸세요.");

            // ★ 양성 대조 — "언제나 같은 문안"이 '측정이 안 됐다'와 구별되게 표본 수를 확인한다.
            Assert.AreEqual(samples, counts.Values.Sum(),
                $"{LogPrefix} 표본이 {counts.Values.Sum()}개뿐입니다 — 위 단언이 아무것도 재고 " +
                "있지 않습니다.");

            // ★ 그리고 그 '한 줄'이 T1 밖에서는 나오지 않는다(티어 분기 자체가 살아 있는가).
            string t1Line = counts.Keys.First();
            foreach (float ratio in tiers.Skip(1))
            {
                CollectionAssert.DoesNotContain(SampleTierPool(ratio, 256).Keys, t1Line,
                    $"{LogPrefix} 높이 {ratio:F2}H 티어에서도 T1 문안(\"{t1Line}\")이 나왔습니다 — " +
                    "티어 분기가 무너졌습니다.");
            }
        }

        /// <summary>
        /// ★ <c>Random.value</c>는 <b>1.0을 포함</b>한다. 경계 표본이 인덱스 밖으로 넘쳐 기존 문안에
        /// 조용히 얹히면 균등이 살짝 깨진다 — 그 한 표본을 직접 확인한다.
        /// </summary>
        [Test]
        public void 변주_경계값이_인덱스_밖으로_넘치지_않는다()
        {
            foreach (float ratio in FindTierRepresentativeRatios())
            {
                string last = Line(ratio, 0.9999999f).Text;
                Assert.AreEqual(last, Line(ratio, 1f).Text,
                    $"{LogPrefix} 높이 {ratio:F2}H 티어에서 변주 1.0이 마지막 줄로 수렴하지 않습니다 " +
                    "— Random.value의 상한 표본이 다른 줄로 넘치고 있습니다.");
                Assert.AreEqual(Line(ratio, 0f).Text, Line(ratio, -0.5f).Text,
                    $"{LogPrefix} 높이 {ratio:F2}H 티어에서 음수 변주가 클램프되지 않습니다.");
            }
        }

        // ================================================================================
        // 5. 새 문안이 회귀 검사에 실제로 닿는가 (존재 단언)
        // ================================================================================

        private static List<string> AllClimbLines()
        {
            var texts = new List<string>();
            foreach (float ratio in FindTierRepresentativeRatios())
            {
                for (int i = 0; i < 64; i++)
                {
                    string t = Line(ratio, (i + 0.5f) / 64f).Text;
                    if (!texts.Contains(t)) texts.Add(t);
                }
            }
            return texts;
        }

        /// <summary>
        /// ★★ 등반 문안 전부가 말뭉치 수집기와 골든에 잡힌다.
        /// <para>문안을 <c>const</c>/<c>string[]</c>로 빼는 순간 <c>DialogueCorpus.ExtractSayReact</c>와
        /// <c>golden_gen.py</c>의 정규식이 <b>구조적으로 못 보고</b>, 골든을 다시 굽는 날 그 줄들이
        /// 조용히 빠져 "화면에는 뜨는데 어떤 회귀 검사에도 닿지 않는 대사"가 된다. 이건 <b>존재
        /// 단언</b>이라 썩으면 시끄럽게 빨개진다(부재 단언으로 바꾸지 마라 — 조용히 초록이 된다).</para>
        /// </summary>
        [Test]
        public void 등반_문안_전부가_말뭉치와_골든에_들어와_있다()
        {
            List<string> lines = AllClimbLines();
            Assert.Greater(lines.Count, 3,
                $"{LogPrefix} 등반 문안을 {lines.Count}종밖에 못 모았습니다 — 이 검사가 아무것도 " +
                "재고 있지 않습니다.");

            List<string> corpus = DialogueCorpus.ScanDistinct();
            var goldenTexts = new HashSet<string>(DialogueCorpus.ReadGolden().Select(r => r.Text));

            foreach (string text in lines)
            {
                CollectionAssert.Contains(corpus, text,
                    $"{LogPrefix} \"{text}\" 를 말뭉치 수집기가 못 봤습니다 — 문안이 인라인 리터럴이 " +
                    "아니라 배열/상수로 빠졌을 가능성이 큽니다.");
                Assert.IsTrue(goldenTexts.Contains(text),
                    $"{LogPrefix} \"{text}\" 가 골든에 없습니다 — docs/localization/verify/golden_gen.py 로 " +
                    "골든을 다시 구우세요(손으로 고치지 마세요).");
            }
        }

        /// <summary>
        /// ★ 모든 등반 문안이 <b>배포 등반 길이</b> 안에서 실제로 발화 가능한가(규칙 8).
        /// <para>design-narrative의 "전 문안 예산 통과" 주장을 <b>다른 자</b>로 다시 잰다 —
        /// 문서의 계산이 아니라 프로덕션 예산 함수로.</para>
        /// </summary>
        [Test]
        public void 모든_등반_문안이_배포_등반길이_안에서_발화_가능하다()
        {
            float plannedDwell = LoadDeployedConfig().parkourClimbDuration;   // 숫자를 베끼지 않는다
            Assert.Greater(plannedDwell, 0f, $"{LogPrefix} 배포 등반 길이가 0입니다.");

            foreach (float ratio in FindTierRepresentativeRatios())
            {
                for (int i = 0; i < 64; i++)
                {
                    DialogueLine line = Line(ratio, (i + 0.5f) / 64f);
                    Assert.IsTrue(DialogueBudget.IsEligible(line, plannedDwell, DialogueTiming.FadeInSeconds),
                        $"{LogPrefix} \"{line.Text}\" 는 등반 길이 {plannedDwell:F2}초 안에서 읽을 수 없어 " +
                        $"영영 침묵합니다(필요체류 " +
                        $"{DialogueBudget.RequiredDwellSeconds(line.Text, DialogueTiming.FadeInSeconds):F3}초). " +
                        "화면에 절대 안 뜨는 문안을 대사표에 남기지 마세요.");
                }
            }
        }

        // ================================================================================
        // 6. 에셋 누락 — 코드만 고치고 배포 에셋을 빠뜨리는 형태
        // ================================================================================

        /// <summary>
        /// ★★ 배포 에셋에 <b>키가 실제로 있는가</b>.
        /// <para><c>AssetDatabase</c>로 읽은 값만 보면 "에셋에 키가 있고 값이 0.35"와 "에셋에 키가
        /// 없어서 코드 기본값 0.35가 보이는 것"이 <b>똑같이 생겼다</b> — 후자는 나중에 코드 기본값을
        /// 바꾸는 순간 배포본이 조용히 따라 움직인다. 그래서 YAML 원문도 함께 본다.</para>
        /// <para>니들은 <c>nameof</c>로 만든다(문자열을 베끼지 않는다). 음성 대조로 "아무거나 참"이
        /// 아님을 보인다.</para>
        /// </summary>
        [Test]
        public void 배포_에셋에_등반_확률_키가_실제로_있다()
        {
            string key = nameof(StickConfig.parkourClimbChatterChance);
            string yaml = File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty, DeployedConfigPath));

            Assert.IsTrue(yaml.Contains(key + ":"),
                $"{LogPrefix} 배포 에셋에 {key} 키가 없습니다 — 코드 기본값만 고치고 에셋을 빠뜨린 " +
                "형태입니다(이 저장소의 거짓 통과 #9). 지금은 값이 같아 보이지만, 코드 기본값이 " +
                "움직이는 날 배포본이 조용히 따라갑니다.");

            // ★ 음성 대조 — Contains가 무엇이든 참이 아님을 보인다.
            Assert.IsFalse(yaml.Contains(key + "Nonexistent:"),
                $"{LogPrefix} 존재하지 않는 키까지 찾았습니다 — 위 단언이 아무것도 재지 않습니다.");

            // 값 자체도 코드 기본값과 같은지 확인한다(둘 중 하나만 고친 형태를 잡는다).
            var codeDefault = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.AreEqual(codeDefault.parkourClimbChatterChance,
                    LoadDeployedConfig().parkourClimbChatterChance, 1e-6f,
                    $"{LogPrefix} 코드 기본값과 배포 에셋의 {key} 가 다릅니다 — 의도한 차이라면 " +
                    "ConfigAssetDriftLedgerTests 대장에 등재하세요.");
            }
            finally
            {
                Object.DestroyImmediate(codeDefault);
            }
        }
    }
}
