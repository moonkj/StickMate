using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 유휴 "주위 살피기"의 두 신고를 잠근다 (2026-08-31 사용자 신고 원문:
    /// "자꾸 머리를 움직이는데 목에서 벗어나서 이상함 ... 그리고 너무 자주함").
    ///
    /// ============================================================================
    /// (A) 머리가 목에서 벗어나는 문제 — 원인은 확률이 아니라 <b>리그의 구조</b>였다
    /// ============================================================================
    /// 이 캐릭터에는 <b>목 관절이 없다</b>. 목은 별도 오브젝트가 아니라 Torso LineRenderer의 윗부분이고
    /// (Editor/SceneBootstrapper.cs가 x=0에 곧게 굽는다), 머리는 그것과 <b>형제</b>인 "Head" 앵커다.
    /// States/StickmanPoseAnimator.SetBodyOffset의 headOffsetX는 그 머리 앵커만 좌우로 민다.
    /// 즉 이 값이 0이 아니면 <b>정의상</b> 머리가 목에서 미끄러진다 — 조건부 버그가 아니다.
    ///
    /// 안전 상한의 유도(이 파일이 단언하는 숫자):
    ///   목선이 여전히 "머리 중심을 가리키는 선"으로 읽히려면, 머리 중심이 목 획의 폭 밖으로
    ///   나가면 안 된다. 목 획 두께는 0.11 x 0.7 = 0.077(SceneBootstrapper.BaselineLineWidth x
    ///   LineWidthScale), 반폭 0.0385, 배율 1.0 신장 2.2746944 이므로
    ///       상한 = 0.0385 / 2.2746944 = 0.01693 (신장 배수)
    ///   이 비율은 배율에 무관하다(획 두께와 신장이 같은 배율로 함께 커진다).
    ///   예전 기본값 0.035는 그 <b>2.07배</b>였고, 머리 반경(신장의 0.0967배)의 36%나 밀었다.
    ///
    /// ============================================================================
    /// (B) "너무 자주함" — 실측
    /// ============================================================================
    /// States/AutoWanderController.cs의 확률/지속시간을 그대로 1시간 시뮬레이션한 결과:
    ///   유예 없음(예전): 분당 9.7회 / 중앙값 간격 6.3초 / 최소 간격 1.4초
    ///   유예 30초:       분당 1.8회 / 중앙값 간격 32.9초
    /// 트리거 조건(26-3) 자체는 멀쩡했고, 진짜 원인은 26-1의 "Idle 연장"(25%)이 EnterResting을 다시
    /// 불러 <b>새 Idle 구간 = 새 추첨권</b>을 만든다는 데 있었다. 그래서 조건식이 아니라 최소 간격을
    /// 얹었다(StickConfig.wanderLookAroundCooldownSeconds).
    /// 아래 <see cref="주위_살피기_유예가_있으면_빈도가_실제로_떨어진다"/>가 그 컨트롤러의 갈래를
    /// 그대로 재현해 <b>수치로</b> 단언한다(네거티브 컨트롤: 유예 0이면 같은 검사가 실패한다).
    ///
    /// 이 테스트는 배포 자산을 <b>읽기만</b> 한다(CLAUDE.md 절대 불변 원칙 3).
    /// </summary>
    public class IdleAmbientLookAroundInvariantTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        // SceneBootstrapper의 지오메트리 상수(그 파일은 Editor 어셈블리 private이라 값을 옮겨 적는다 —
        // 옮겨 적은 값이 낡으면 아래 목_획_유도값_검증 테스트가 잡는다).
        private const float LineWidthScale = 0.7f;
        private const float BaselineLineWidth = 0.11f * LineWidthScale;   // 몸통(=목) 획 두께
        private const float BaselineHeadVisualRadius = 0.22f;             // 머리 링 반경

        /// <summary>머리 중심이 목 획 밖으로 나가지 않는 최대 좌우 이동 — <b>신장 배수</b>.
        /// 제품 코드의 상수를 그대로 쓴다(테스트가 자기만의 사본을 들면 그것이 낡는다). 아래
        /// <see cref="상한_유도에_쓰인_지오메트리_상수가_실제_프리팹_치수와_같다"/>가 그 상수가
        /// SceneBootstrapper 지오메트리에서 실제로 유도되는지까지 확인한다.</summary>
        private static float MaxHeadShiftRatio => StickConfig.MaxSafeHeadShiftRatio;

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        // ============================================================================
        // (A) 머리-목 어긋남
        // ============================================================================

        [Test]
        public void 배포_설정의_머리_좌우_이동은_목_획_반폭을_넘지_않는다()
        {
            StickConfig c = LoadDefaultConfig();
            float limit = MaxHeadShiftRatio;

            Assert.LessOrEqual(c.idleAmbientLookHeadShiftRatio, limit + 1e-6f,
                $"[주위살피기] idleAmbientLookHeadShiftRatio={c.idleAmbientLookHeadShiftRatio:F5}가 " +
                $"안전 상한 {limit:F5}(신장 배수)를 넘었습니다. 이 리그에는 목 관절이 없어서" +
                "(목 = Torso 선의 윗부분, 루트 로컬 x=0 고정) 머리 앵커만 미는 방식은 " +
                "머리를 목에서 떼어놓습니다 — 2026-08-31 사용자 신고 그대로입니다. " +
                "값을 되살리려면 목을 함께 기울이는 배관부터 만드십시오.");
        }

        /// <summary>네거티브 컨트롤 — 예전 기본값(0.035)을 같은 검사에 넣으면 <b>실제로 걸린다</b>.
        /// 이것이 없으면 위 단언은 "지금 0이라서 그냥 통과하는" 검사와 구분되지 않는다.</summary>
        [Test]
        public void 네거티브_컨트롤_예전_기본값_0_035는_같은_검사에_걸린다()
        {
            const float legacy = 0.035f;
            float limit = MaxHeadShiftRatio;
            Assert.Greater(legacy, limit,
                "예전 기본값이 상한 이하로 계산됐습니다 — 상한 유도가 잘못됐거나 지오메트리 상수가 " +
                "바뀌었습니다(이 검사는 아무것도 잡아내지 못하는 상태입니다).");
            Debug.Log($"[주위살피기] 네거티브 컨트롤 통과 — 예전 0.035 / 상한 {limit:F5} " +
                $"(= {legacy / limit:F2}배 초과), 머리 반경 대비 " +
                $"{legacy * StickConfig.BaselineCharacterTotalHeight / BaselineHeadVisualRadius * 100f:F0}%.");
        }

        [Test]
        public void 상한_유도에_쓰인_지오메트리_상수가_실제_프리팹_치수와_같다()
        {
            // 신장 = 몸통 위 끝 + 머리 지름. SceneBootstrapper의 SpecTorsoTopY(1.35) + footLift(0.4846944)
            // + 2 x 0.22 = 2.2746944. 상수가 한쪽만 바뀌면 위 상한이 조용히 틀려지므로 여기서 묶는다.
            const float specTorsoTopY = 1.35f, footLift = 0.4846944f;
            float derivedHeight = specTorsoTopY + footLift + BaselineHeadVisualRadius * 2f;
            Assert.AreEqual(StickConfig.BaselineCharacterTotalHeight, derivedHeight, 1e-4f,
                "SceneBootstrapper 지오메트리에서 유도한 신장과 StickConfig.BaselineCharacterTotalHeight가 " +
                "어긋납니다 — 위 안전 상한 계산이 낡았습니다.");

            // 상한 자체도 지오메트리에서 다시 유도해 제품 상수와 맞춰본다.
            float derivedLimit = (BaselineLineWidth * 0.5f) / derivedHeight;
            Assert.AreEqual(derivedLimit, StickConfig.MaxSafeHeadShiftRatio, 1e-6f,
                "StickConfig.MaxSafeHeadShiftRatio가 SceneBootstrapper의 획 두께/신장에서 유도되지 " +
                "않습니다 — 한쪽 상수만 바뀌었습니다.");
        }

        // ============================================================================
        // (B) 빈도
        // ============================================================================

        [Test]
        public void 배포_설정에_주위_살피기_유예가_걸려_있다()
        {
            StickConfig c = LoadDefaultConfig();
            Assert.Greater(c.wanderLookAroundCooldownSeconds, 0f,
                "[주위살피기] wanderLookAroundCooldownSeconds가 0입니다 — 유예 없이는 " +
                "'Idle 연장'이 뽑힐 때마다 새 추첨권이 생겨 분당 9.7회까지 나옵니다(2026-08-31 실측).");
        }

        [Test]
        public void 주위_살피기_유예가_있으면_빈도가_실제로_떨어진다()
        {
            StickConfig c = LoadDefaultConfig();

            Frequency withCooldown = Simulate(c, c.wanderLookAroundCooldownSeconds);
            Frequency legacy = Simulate(c, 0f);

            Debug.Log($"[주위살피기] 1시간 시뮬레이션 — 예전(유예 0): 분당 {legacy.PerMinute:F1}회 / " +
                $"중앙값 간격 {legacy.MedianGapSeconds:F1}초 / 최소 {legacy.MinGapSeconds:F1}초. " +
                $"지금(유예 {c.wanderLookAroundCooldownSeconds:F0}초): 분당 {withCooldown.PerMinute:F1}회 / " +
                $"중앙값 간격 {withCooldown.MedianGapSeconds:F1}초 / 최소 {withCooldown.MinGapSeconds:F1}초.");

            // (1) 유예가 실제로 지켜진다 — 어떤 두 발동도 유예보다 가깝지 않다.
            Assert.GreaterOrEqual(withCooldown.MinGapSeconds, c.wanderLookAroundCooldownSeconds - 0.001f,
                "유예보다 짧은 간격으로 발동했습니다 — 게이트가 새고 있습니다.");

            // (2) 네거티브 컨트롤 — 같은 시뮬레이션에서 예전 설정은 사용자가 신고한 그 빈도가 나온다.
            Assert.Greater(legacy.PerMinute, 5f,
                "유예 0인데도 빈도가 낮게 나왔습니다 — 시뮬레이션이 실제 갈래를 재현하지 못하고 있어 " +
                "(1)의 통과가 아무것도 증명하지 못합니다.");

            // (3) 체감 가능한 감소 — 최소 3배.
            Assert.Less(withCooldown.PerMinute * 3f, legacy.PerMinute,
                $"빈도가 충분히 줄지 않았습니다({legacy.PerMinute:F1} -> {withCooldown.PerMinute:F1}회/분).");
        }

        // ------------------------------------------------------------------------
        // AutoWanderController의 Resting/Moving 갈래를 그대로 재현한 시뮬레이션.
        // 제품 코드를 부르지 않는 이유: 그쪽은 StickmanBlackboard/씬이 필요한 MonoBehaviour 배선
        // 경로다. 대신 재현이 맞는지는 위 (2) 네거티브 컨트롤이 지킨다 — 예전 설정에서 사용자가
        // 신고한 빈도가 나오지 않으면 이 시뮬레이션 자체가 실패로 처리된다.
        // ------------------------------------------------------------------------
        private struct Frequency
        {
            public float PerMinute;
            public float MedianGapSeconds;
            public float MinGapSeconds;
        }

        private static Frequency Simulate(StickConfig c, float cooldownSeconds)
        {
            const float totalSeconds = 3600f;
            var rng = new System.Random(20260831); // 고정 시드 — 테스트가 흔들리지 않게.
            var fireTimes = new System.Collections.Generic.List<float>(1024);

            float t = 0f;
            // 유예는 "마지막 발동으로부터 경과 시간"으로 본다 — 매 프레임 감소시키는 타이머와 수학적으로
            // 같고(단조 시간), 부호를 헷갈릴 여지가 없다.
            float lastFireTime = float.NegativeInfinity;
            while (t < totalSeconds)
            {
                // EnterResting()
                float restDuration = Jitter(rng, c, Range(rng, c.wanderIdleDurationMin, c.wanderIdleDurationMax));
                float lookDelay = Range(rng, c.wanderLookAroundDelayMin, c.wanderLookAroundDelayMax);

                // TickResting(): 지연시간이 이 Idle 구간 안에 들어올 때만 추첨권을 쓴다.
                if (lookDelay < restDuration)
                {
                    float fireAt = t + lookDelay;
                    if (fireAt - lastFireTime >= cooldownSeconds)
                    {
                        fireTimes.Add(fireAt);
                        lastFireTime = fireAt;
                    }
                    // 유예에 막혔더라도 추첨권(_lookAroundFiredThisRest)은 소모된다 — 컨트롤러와 같은 규칙.
                }
                t += restDuration;

                // ResolvePostIdleBranch(): Walk / (제자리 점프) / Idle 연장
                double roll = rng.NextDouble();
                if (roll < c.wanderPostIdleWalkChance)
                {
                    t += Jitter(rng, c, Range(rng, c.wanderWalkDurationMin, c.wanderWalkDurationMax));
                }
                // 나머지 갈래(점프/연장)는 곧바로 새 Resting — 이것이 빈도 폭주의 원인이다.
            }

            var gaps = new System.Collections.Generic.List<float>(fireTimes.Count);
            for (int i = 1; i < fireTimes.Count; i++) gaps.Add(fireTimes[i] - fireTimes[i - 1]);
            gaps.Sort();

            return new Frequency
            {
                PerMinute = fireTimes.Count / (totalSeconds / 60f),
                MedianGapSeconds = gaps.Count > 0 ? gaps[gaps.Count / 2] : float.PositiveInfinity,
                MinGapSeconds = gaps.Count > 0 ? gaps[0] : float.PositiveInfinity,
            };
        }

        private static float Range(System.Random rng, float min, float max)
            => max <= min ? min : min + (float)rng.NextDouble() * (max - min);

        private static float Jitter(System.Random rng, StickConfig c, float baseValue)
        {
            float ratio = c.wanderDurationJitterRatio;
            float factor = 1f + (float)((rng.NextDouble() * 2.0 - 1.0) * ratio);
            return Mathf.Max(0.01f, baseValue * factor);
        }
    }
}
