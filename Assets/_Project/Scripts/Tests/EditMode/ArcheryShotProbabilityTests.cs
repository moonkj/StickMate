using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 활쏘기 명중/빗나감 <b>확률 모델</b> 회귀 테스트 (사용자 신고 2026-09-03 대응).
    ///
    /// <para>신고 원문: <i>"활쏘기는 무조건 66.6%아니야? 계속 시도해봐도 무조건 2대만 과녁에 명중하고
    /// 1대는 무조건 실패하는데"</i>. <b>버그가 아니라 의도된 결정론이었다</b> — 결과가
    /// "빗나감 1 + 외곽 1 + 정중앙 1"로 고정된 시나리오였고, 순서만 섞였다. 그래서 몇 번을 봐도
    /// 명중 수는 예외 없이 2였다.</para>
    ///
    /// <para>사용자 지시로 뒤집혔다: <i>"확률제로 바꿔야지.. 어쩔땐 다맞고 어쩔땐 하나도 안맞고
    /// 어쩔땐 1대 어쩔땐 2대 이런식으로"</i>. 이제 <c>StickConfig.archeryHitChance</c>로
    /// <b>발마다 독립 추첨</b>하고, <b>명중한 발에만</b> <c>StickConfig.archeryBullseyeChance</c>로
    /// 한 번 더 추첨해 정중앙/외곽을 가른다(리더 판정 ②안, 2026-09-03).</para>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>극값의 결정론</b> — 0.0이면 전부 Miss, 1.0이면 전부 Hit. 확률은 "대충 맞는다"로
    ///     검사할 수 없지만 <b>두 경계는 확정</b>이라 값 하나로 잠글 수 있다.
    ///     ★ 1.0 경계는 특히 중요하다: <c>UnityEngine.Random.value</c>가 <b>1.0을 포함</b>하는
    ///     구간이라 <c>value &lt; 1f</c>만 썼다면 "확률 1인데 빗나갔다"가 드물게 튀어나온다.</item>
    ///   <item><b>개수의 분포</b> — 0발/1발/2발/3발 명중이 <b>전부</b> 나온다. 특히
    ///     "3발 다 명중"과 "3발 다 빗나감"이 각각 최소 1회.</item>
    ///   <item><b>순서의 독립성</b> — 같은 명중 수(2/3)라도 배열이 여러 형태로 나온다
    ///     (<c>[Hit,Miss,Hit]</c>와 <c>[Miss,Hit,Hit]</c>가 둘 다). 그리고 인덱스별 명중률이
    ///     서로 같다 — <b>특정 자리에 특정 결과가 묶여 있지 않다</b>는 뜻이다.</item>
    ///   <item><b>정중앙 서브추첨</b> — 극값(0/1)의 결정론, 정중앙이 <b>모든 자리</b>에서 나오고
    ///     한 사이클에 0~3회 나온다는 것, 그리고 ★ <b>빗나간 발이 정중앙으로 새지 않는다</b>는 것
    ///     (정중앙확률을 1로 켠 채 확인하는 누수 음성 대조).</item>
    ///   <item><b>기본값 유도의 검산</b> — 두 확률의 곱이 옛 고정 시나리오의 기대값(세션당 정중앙
    ///     1.000회 · 명중률 1/3)을 보존한다. 기대값은 <b>설정 필드에서 독립 계산</b>하고 옛 값은
    ///     복원본을 <b>실제로 돌려</b> 얻는다 — 기대값을 프로덕션 함수로 만들지 않는다.</item>
    ///   <item><b>출하 설정이 실제로 확률적인가</b> — 두 기본값 어느 쪽이든 0이나 1로 굳어 있으면
    ///     사용자가 신고한 "무조건 …" 체감이 형태만 바꿔 되돌아온다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 음성 대조 — 옛 결정론 시나리오를 <b>같은 파일 안에서 다시 돌린다</b>
    /// ============================================================================
    /// <see cref="LegacyDeterministicScenario"/>가 폐지된 옛 알고리즘의 복원본이다. 위 ②③이
    /// "새 모델이라서 통과한 것"임을 증명하려면, <b>옛 모델에서는 같은 검사가 실패한다</b>는 것을
    /// 보여야 한다 — 그러지 않으면 이 단언들이 무엇을 걸러내는지 알 수 없다(이 저장소가 아홉 번
    /// 당한 "실패한 측정과 성공한 측정이 똑같이 생겼다"의 예방책이다).
    /// 옛 모델은 몇 만 번을 돌려도 <b>명중 수가 영원히 2</b>이고 마지막 자리는 <b>영원히 Bullseye</b>다.
    ///
    /// <para><b>난수는 주입</b>한다(시드 고정 <c>System.Random</c>). 프로덕션은
    /// <c>ArcheryState.ResolveShotResult(hitChance, roll01)</c>라는 순수 함수를 노출하므로 씬/카메라/
    /// MonoBehaviour 없이 EditMode에서 그대로 돌아간다 — <c>ArcheryDirector.ResolvePlacement</c>가
    /// 같은 이유로 취한 구조다. 플레이키가 아니다: 같은 시드는 같은 표본을 준다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 활쏘기 경로에는 <c>#if UNITY_STANDALONE_*</c> 분기가 한 곳도
    /// 없다(실측). 소스 감사도 파일을 <b>읽기만</b> 한다.</para>
    /// </summary>
    public sealed class ArcheryShotProbabilityTests
    {
        /// <summary>한 조합을 세기 위한 표본 사이클 수. 3발 독립이므로 p=0.667에서
        /// "3발 다 빗나감"은 약 3.7%(=0.333³), 2000 사이클이면 기대 74회다 — 0회일 확률은
        /// 사실상 0이라 시드 의존 플레이키가 생기지 않는다.</summary>
        private const int Cycles = 2000;

        /// <summary>한 사이클(3발)을 프로덕션과 <b>같은 순서로</b> 뽑는다 — 발마다 난수 두 개
        /// (명중 판정 / 정중앙 판정)를 따로 소비하는 것까지 같다.</summary>
        private static ArcheryShotResult[] RollCycle(float hitChance, float bullseyeChance, System.Random rng)
        {
            var results = new ArcheryShotResult[ArcheryState.ShotCount];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = ArcheryState.ResolveShotResult(hitChance, bullseyeChance,
                    (float)rng.NextDouble(), (float)rng.NextDouble());
            }
            return results;
        }

        private static int CountBullseyes(ArcheryShotResult[] results)
        {
            int n = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] == ArcheryShotResult.Bullseye) n++;
            }
            return n;
        }

        private static int CountHits(ArcheryShotResult[] results)
        {
            int hits = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] != ArcheryShotResult.Miss) hits++;
            }
            return hits;
        }

        /// <summary>결과 배열을 "HMH" 같은 한 낱말로 — 순서까지 포함한 조합을 세기 위한 키.</summary>
        private static string Shape(ArcheryShotResult[] results)
        {
            var chars = new char[results.Length];
            for (int i = 0; i < results.Length; i++)
            {
                chars[i] = results[i] == ArcheryShotResult.Miss ? 'M'
                    : results[i] == ArcheryShotResult.Hit ? 'H' : 'B';
            }
            return new string(chars);
        }

        /// <summary>
        /// ★ 음성 대조용 — <b>폐지된</b> 옛 결정론 시나리오의 복원본(빗나감 1 + 외곽 1 + 정중앙 1,
        /// 순서만 섞임). 프로덕션에는 더 이상 없다. 여기 남겨 두는 이유는 아래 테스트들이
        /// "무엇을 걸러내는가"를 실제로 실행해 보이기 위해서다.
        /// </summary>
        private static ArcheryShotResult[] LegacyDeterministicScenario(System.Random rng)
        {
            var results = new ArcheryShotResult[ArcheryState.ShotCount];
            int missIndex = rng.Next(0, ArcheryState.ShotCount - 1);
            for (int i = 0; i < ArcheryState.ShotCount - 1; i++)
            {
                results[i] = i == missIndex ? ArcheryShotResult.Miss : ArcheryShotResult.Hit;
            }
            results[ArcheryState.ShotCount - 1] = ArcheryShotResult.Bullseye;
            return results;
        }

        // ============================================================================
        // ① 극값 — 확률 0/1은 결정론이다
        // ============================================================================

        [Test]
        public void 확률이_0이면_어떤_난수에서도_세_발_전부_빗나간다()
        {
            // 경계 포함: 0.0과 1.0을 표본에 직접 넣는다.
            float[] rolls = { 0f, 1e-7f, 0.25f, 0.5f, 0.999f, 1f };
            for (int i = 0; i < rolls.Length; i++)
            {
                Assert.AreEqual(ArcheryShotResult.Miss, ArcheryState.ResolveShotResult(0f, rolls[i]),
                    $"명중확률 0인데 난수 {rolls[i]}에서 명중이 나왔습니다 — '전부 빗나감'을 " +
                    "확정으로 잠글 수 없으면 이 필드는 킬 스위치가 아닙니다.");
            }

            // ★ 정중앙 확률을 1로 **켠 채** 돌린다 — 빗나감이 두 번째 추첨으로 새지 않는지까지 본다.
            var rng = new System.Random(1);
            for (int c = 0; c < Cycles; c++)
            {
                ArcheryShotResult[] results = RollCycle(0f, 1f, rng);
                Assert.AreEqual(0, CountHits(results),
                    "명중확률 0인 사이클에서 명중한 발이 나왔습니다.");
                Assert.AreEqual(0, CountBullseyes(results),
                    "명중확률 0인데 정중앙이 나왔습니다 — 빗나간 발이 정중앙 추첨으로 새고 있습니다.");
            }
        }

        [Test]
        public void 확률이_1이면_난수가_정확히_1이어도_세_발_전부_명중한다()
        {
            // ★ 이 1f가 이 테스트의 본체다 — UnityEngine.Random.value는 1.0을 포함하므로
            //   'roll < chance' 하나로만 짜면 여기서 Miss가 나온다(재현 불가능한 형태로 새는 버그).
            float[] rolls = { 0f, 0.5f, 0.9999999f, 1f };
            for (int i = 0; i < rolls.Length; i++)
            {
                Assert.AreEqual(ArcheryShotResult.Hit, ArcheryState.ResolveShotResult(1f, rolls[i]),
                    $"명중확률 1인데 난수 {rolls[i]}에서 빗나감이 나왔습니다 — 난수 구간이 " +
                    "1.0을 포함한다는 사실을 부등호가 흘리고 있습니다.");
            }

            var rng = new System.Random(2);
            for (int c = 0; c < Cycles; c++)
            {
                Assert.AreEqual(ArcheryState.ShotCount, CountHits(RollCycle(1f, 0f, rng)),
                    "명중확률 1인 사이클에서 빗나간 발이 나왔습니다.");
            }
        }

        [Test]
        public void 중간_확률에서_난수와_결과의_대응이_경계까지_정확하다()
        {
            const float p = 0.5f;
            Assert.AreEqual(ArcheryShotResult.Hit, ArcheryState.ResolveShotResult(p, p - 1e-4f),
                "확률 바로 아래 난수가 명중이 아닙니다.");
            Assert.AreEqual(ArcheryShotResult.Miss, ArcheryState.ResolveShotResult(p, p),
                "난수가 확률과 같으면 빗나감이어야 합니다(반개구간 [0,p) 계약).");
            Assert.AreEqual(ArcheryShotResult.Miss, ArcheryState.ResolveShotResult(p, p + 1e-4f),
                "확률 바로 위 난수가 빗나감이 아닙니다.");
        }

        // ============================================================================
        // ② 개수 — 0/1/2/3발이 전부 나온다 (신고의 본체)
        // ============================================================================

        [Test]
        public void 명중_개수가_0에서_3까지_전부_나온다([Values(1, 7, 12345)] int seed)
        {
            var rng = new System.Random(seed);
            var counts = new int[ArcheryState.ShotCount + 1];
            for (int c = 0; c < Cycles; c++) counts[CountHits(RollCycle(0.667f, 0.5f, rng))]++;

            for (int hits = 0; hits <= ArcheryState.ShotCount; hits++)
            {
                Assert.Greater(counts[hits], 0,
                    $"{Cycles}사이클을 돌렸는데 '{hits}발 명중'이 한 번도 안 나왔습니다 — " +
                    "사용자 요구는 '어쩔땐 다맞고 어쩔땐 하나도 안맞고 어쩔땐 1대 어쩔땐 2대'입니다.");
            }

            // ★ 음성 대조 — 옛 결정론 시나리오는 같은 표본 수에서 2발 말고는 절대 안 나온다.
            var legacyRng = new System.Random(seed);
            var legacyCounts = new int[ArcheryState.ShotCount + 1];
            for (int c = 0; c < Cycles; c++) legacyCounts[CountHits(LegacyDeterministicScenario(legacyRng))]++;

            Assert.AreEqual(Cycles, legacyCounts[ArcheryState.ShotCount - 1],
                "음성 대조가 깨졌습니다 — 옛 시나리오 복원본이 2발 명중을 고정으로 내놓지 않습니다. " +
                "이 대조가 죽으면 위 단언이 무엇을 걸러내는지 증명할 수 없습니다.");
            Assert.AreEqual(0, legacyCounts[0] + legacyCounts[ArcheryState.ShotCount],
                "음성 대조가 깨졌습니다 — 옛 시나리오에서 '전부 명중'/'전부 빗나감'이 나왔습니다. " +
                "그 두 조합이 영원히 나오지 않는 것이 바로 사용자가 신고한 증상입니다.");

            Debug.Log($"[활쏘기확률] 시드 {seed} / {Cycles}사이클 — 새 모델 명중수 분포 " +
                $"0발 {counts[0]} · 1발 {counts[1]} · 2발 {counts[2]} · 3발 {counts[3]} " +
                $"(옛 결정론 모델은 2발 {legacyCounts[2]}회로 전부 몰림 = 신고된 증상).");
        }

        // ============================================================================
        // ③ 순서 — 같은 개수라도 배열이 다르고, 인덱스에 결과가 묶여 있지 않다
        // ============================================================================

        [Test]
        public void 같은_명중_개수라도_맞는_순서가_여러_형태로_나온다()
        {
            var rng = new System.Random(4242);
            var shapes = new System.Collections.Generic.Dictionary<string, int>();
            for (int c = 0; c < Cycles; c++)
            {
                // ★ 정중앙 추첨을 꺼서(0f) 결과를 H/M 두 값으로 고정한다 — 이 테스트가 재는 것은
                //   "순서가 독립인가"이고, 세 번째 값이 섞이면 2^ShotCount 단언이 무의미해진다.
                //   정중앙 서브추첨은 아래 전용 테스트들이 따로 잠근다.
                string shape = Shape(RollCycle(0.5f, 0f, rng));
                shapes.TryGetValue(shape, out int n);
                shapes[shape] = n + 1;
            }

            // 2발 명중의 세 가지 순서가 전부 나와야 한다 — 하나라도 0이면 어느 자리가 묶여 있다는 뜻.
            string[] twoHitShapes = { "HHM", "HMH", "MHH" };
            for (int i = 0; i < twoHitShapes.Length; i++)
            {
                Assert.IsTrue(shapes.ContainsKey(twoHitShapes[i]) && shapes[twoHitShapes[i]] > 0,
                    $"2발 명중인데 배열 {twoHitShapes[i]}가 {Cycles}사이클 동안 한 번도 안 나왔습니다 — " +
                    "명중 수를 먼저 뽑고 앞쪽에 몰아넣는 식이면 이 셋 중 하나만 나옵니다.");
            }
            // 1발 명중도 마찬가지로 세 자리 전부에서 나와야 한다.
            string[] oneHitShapes = { "HMM", "MHM", "MMH" };
            for (int i = 0; i < oneHitShapes.Length; i++)
            {
                Assert.IsTrue(shapes.ContainsKey(oneHitShapes[i]) && shapes[oneHitShapes[i]] > 0,
                    $"1발 명중인데 배열 {oneHitShapes[i]}가 한 번도 안 나왔습니다 — 명중이 특정 자리에 " +
                    "묶여 있습니다.");
            }
            Assert.AreEqual(1 << ArcheryState.ShotCount, shapes.Count,
                $"관측된 배열이 {shapes.Count}종입니다 — 3발 독립이면 2³ = 8종이 전부 나와야 합니다.");
        }

        [Test]
        public void 인덱스별_명중률이_서로_같다_특정_자리에_결과가_묶여_있지_않다()
        {
            const float p = 0.667f;
            const int Samples = 20000;
            var rng = new System.Random(90210);
            var hitsAt = new int[ArcheryState.ShotCount];
            for (int c = 0; c < Samples; c++)
            {
                ArcheryShotResult[] results = RollCycle(p, 0.5f, rng);
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] != ArcheryShotResult.Miss) hitsAt[i]++;
                }
            }

            // 표준편차 = sqrt(p(1-p)/N) ≈ 0.0033. 0.02는 6σ라 시드에 흔들리지 않는다.
            for (int i = 0; i < hitsAt.Length; i++)
            {
                float rate = (float)hitsAt[i] / Samples;
                Assert.AreEqual(p, rate, 0.02f,
                    $"{i + 1}번째 발의 명중률이 {rate:P1}입니다 — 설정 확률 {p:P1}에서 크게 벗어났습니다. " +
                    "특정 자리에 결과가 묶여 있다는 신호입니다(옛 모델은 마지막 자리가 100%였습니다).");
            }

            // ★ 음성 대조 — 옛 모델의 마지막 자리는 100%, 첫 자리는 50%였다.
            var legacyRng = new System.Random(90210);
            var legacyHitsAt = new int[ArcheryState.ShotCount];
            for (int c = 0; c < Samples; c++)
            {
                ArcheryShotResult[] results = LegacyDeterministicScenario(legacyRng);
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] != ArcheryShotResult.Miss) legacyHitsAt[i]++;
                }
            }
            Assert.AreEqual(Samples, legacyHitsAt[ArcheryState.ShotCount - 1],
                "음성 대조가 깨졌습니다 — 옛 모델의 마지막 발은 100% 정중앙이어야 합니다.");
            Assert.AreNotEqual(Samples, hitsAt[ArcheryState.ShotCount - 1],
                "새 모델의 마지막 발도 100% 명중입니다 — '마지막은 항상 정중앙' 잔재가 남아 있습니다.");

            Debug.Log($"[활쏘기확률] 인덱스별 명중률 = {(float)hitsAt[0] / Samples:P1} / " +
                $"{(float)hitsAt[1] / Samples:P1} / {(float)hitsAt[2] / Samples:P1} " +
                $"(옛 모델: {(float)legacyHitsAt[0] / Samples:P1} / {(float)legacyHitsAt[1] / Samples:P1} / " +
                $"{(float)legacyHitsAt[2] / Samples:P1} — 마지막이 100%로 고정).");
        }

        // ============================================================================
        // ③-b 정중앙 서브추첨 (리더 판정 ②안, 2026-09-03)
        // ============================================================================

        [Test]
        public void 정중앙_확률의_극값이_결정론이다()
        {
            float[] rolls = { 0f, 1e-7f, 0.5f, 0.9999999f, 1f };
            for (int i = 0; i < rolls.Length; i++)
            {
                // 명중은 확정(hitChance=1)으로 두고 두 번째 추첨만 본다.
                Assert.AreEqual(ArcheryShotResult.Hit,
                    ArcheryState.ResolveShotResult(1f, 0f, rolls[i], rolls[i]),
                    $"정중앙확률 0인데 난수 {rolls[i]}에서 정중앙이 나왔습니다.");
                Assert.AreEqual(ArcheryShotResult.Bullseye,
                    ArcheryState.ResolveShotResult(1f, 1f, rolls[i], rolls[i]),
                    $"정중앙확률 1인데 난수 {rolls[i]}에서 외곽에 그쳤습니다 — 난수 구간이 1.0을 " +
                    "포함한다는 사실을 부등호가 흘리고 있습니다(명중 판정과 같은 함정).");
            }

            // 반개구간 계약 — 난수가 확률과 같으면 정중앙이 아니다(명중 판정과 어법이 같아야 한다).
            const float b = 0.5f;
            Assert.AreEqual(ArcheryShotResult.Bullseye, ArcheryState.ResolveShotResult(1f, b, 0f, b - 1e-4f),
                "정중앙확률 바로 아래 난수가 정중앙이 아닙니다.");
            Assert.AreEqual(ArcheryShotResult.Hit, ArcheryState.ResolveShotResult(1f, b, 0f, b),
                "난수가 정중앙확률과 같으면 외곽이어야 합니다(반개구간 [0,b) 계약).");
        }

        /// <summary>
        /// ★ 누수 음성 대조 — <b>빗나간 발은 어떤 난수에서도 정중앙이 되지 않는다.</b>
        /// 정중앙확률을 1로 최대한 켠 상태에서 본다: 승격 경로가 있다면 여기서 반드시 샌다.
        /// 이 계약이 깨지면 도달점은 과녁 <b>앞 땅</b>인데 결과만 정중앙이 되어, 화면에는 땅에 꽂힌
        /// 화살이 보이는데 명중률과 보너스 XP는 올라간다(그림과 판정의 정면 충돌).
        /// </summary>
        [Test]
        public void 빗나간_발은_정중앙확률이_1이어도_정중앙이_되지_않는다()
        {
            float[] rolls = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            for (int i = 0; i < rolls.Length; i++)
            {
                for (int j = 0; j < rolls.Length; j++)
                {
                    // hitChance = 0 -> 반드시 Miss. 두 번째 난수를 어떻게 줘도 Miss여야 한다.
                    Assert.AreEqual(ArcheryShotResult.Miss,
                        ArcheryState.ResolveShotResult(0f, 1f, rolls[i], rolls[j]),
                        $"빗나간 발(난수 {rolls[i]})이 정중앙 추첨(난수 {rolls[j]})으로 승격됐습니다.");
                }
            }

            // 중간 확률에서도 — 표본 전체에서 'Miss인 발이 Bullseye로 기록된' 사이클이 0이어야 한다.
            var rng = new System.Random(777);
            int missCount = 0;
            for (int c = 0; c < Cycles; c++)
            {
                ArcheryShotResult[] results = RollCycle(0.3f, 1f, rng);
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] == ArcheryShotResult.Miss) missCount++;
                    // 정중앙확률 1이므로 명중한 발은 전부 정중앙이어야 하고, 외곽(Hit)은 0건이어야 한다.
                    Assert.AreNotEqual(ArcheryShotResult.Hit, results[i],
                        "정중앙확률 1인데 외곽 명중이 나왔습니다 — 두 번째 추첨이 무시되고 있습니다.");
                }
            }
            Assert.Greater(missCount, 0,
                "표본에 빗나간 발이 한 발도 없습니다 — 이 누수 검사가 아무것도 보지 못했다는 뜻입니다.");
        }

        [Test]
        public void 정중앙이_모든_자리에서_나오고_한_사이클에_0회부터_3회까지_나온다()
        {
            var rng = new System.Random(20260903);
            var bullseyeAt = new int[ArcheryState.ShotCount];
            var perCycle = new int[ArcheryState.ShotCount + 1];
            for (int c = 0; c < Cycles; c++)
            {
                ArcheryShotResult[] results = RollCycle(0.667f, 0.5f, rng);
                perCycle[CountBullseyes(results)]++;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] == ArcheryShotResult.Bullseye) bullseyeAt[i]++;
                }
            }

            for (int i = 0; i < bullseyeAt.Length; i++)
            {
                Assert.Greater(bullseyeAt[i], 0,
                    $"{i + 1}번째 발에서 정중앙이 한 번도 안 나왔습니다 — 옛 모델처럼 특정 자리에 " +
                    "묶여 있다는 신호입니다.");
            }
            for (int n = 0; n <= ArcheryState.ShotCount; n++)
            {
                Assert.Greater(perCycle[n], 0,
                    $"{Cycles}사이클 동안 '정중앙 {n}회'인 사이클이 한 번도 안 나왔습니다 — " +
                    "옛 모델은 영원히 1회뿐이었고, 그것이 폐지 대상이었습니다.");
            }

            // ★ 음성 대조 — 옛 모델은 정중앙이 항상 1회, 항상 마지막 자리였다.
            var legacyRng = new System.Random(20260903);
            var legacyAt = new int[ArcheryState.ShotCount];
            var legacyPerCycle = new int[ArcheryState.ShotCount + 1];
            for (int c = 0; c < Cycles; c++)
            {
                ArcheryShotResult[] results = LegacyDeterministicScenario(legacyRng);
                legacyPerCycle[CountBullseyes(results)]++;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] == ArcheryShotResult.Bullseye) legacyAt[i]++;
                }
            }
            Assert.AreEqual(Cycles, legacyPerCycle[1],
                "음성 대조가 깨졌습니다 — 옛 모델은 사이클마다 정중앙이 정확히 1회여야 합니다.");
            Assert.AreEqual(Cycles, legacyAt[ArcheryState.ShotCount - 1],
                "음성 대조가 깨졌습니다 — 옛 모델의 정중앙은 항상 마지막 자리여야 합니다.");
            Assert.AreEqual(0, legacyAt[0],
                "음성 대조가 깨졌습니다 — 옛 모델에서 첫 발이 정중앙이 될 수는 없었습니다.");

            Debug.Log($"[활쏘기확률] 정중앙 자리별 = {bullseyeAt[0]} / {bullseyeAt[1]} / {bullseyeAt[2]}, " +
                $"사이클당 정중앙 수 0회 {perCycle[0]} · 1회 {perCycle[1]} · 2회 {perCycle[2]} · 3회 {perCycle[3]} " +
                $"(옛 모델: 1회 {legacyPerCycle[1]}회로 고정, 자리는 마지막 {legacyAt[2]}회로 고정).");
        }

        /// <summary>
        /// ★ 기본값 유도의 <b>검산</b> — 두 기본 확률은 "옛 고정 시나리오의 기대값 보존"으로 유도했다:
        /// <c>ShotCount × archeryHitChance × archeryBullseyeChance ≈ 1.000회/세션</c>(옛 모델은 정확히 1회 고정),
        /// 그리고 정보창 명중률 <c>archeryHitChance × archeryBullseyeChance ≈ 1/3</c>(옛 고정 33.3%).
        ///
        /// <para>기대값은 <b>프로덕션 함수가 아니라 설정 필드에서 독립적으로 계산</b>하고, 관측값만
        /// 프로덕션 추첨에서 얻는다 — 둘을 같은 코드로 만들면 함께 틀려도 초록이 된다(TEAM.md).
        /// 옛 모델의 값(1회 / 1분의 3)은 <see cref="LegacyDeterministicScenario"/>를 실제로 돌려 얻는다.</para>
        /// </summary>
        [Test]
        public void 기본값의_정중앙_기대값이_옛_고정_시나리오와_같다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                const int Samples = 20000;
                float expectedPerCycle = ArcheryState.ShotCount * cfg.archeryHitChance * cfg.archeryBullseyeChance;
                float expectedAccuracy = cfg.archeryHitChance * cfg.archeryBullseyeChance;

                var rng = new System.Random(5150);
                int bullseyes = 0, shots = 0;
                for (int c = 0; c < Samples; c++)
                {
                    ArcheryShotResult[] results = RollCycle(cfg.archeryHitChance, cfg.archeryBullseyeChance, rng);
                    bullseyes += CountBullseyes(results);
                    shots += results.Length;
                }
                float observedPerCycle = (float)bullseyes / Samples;
                float observedAccuracy = (float)bullseyes / shots;

                // 옛 모델의 실측값 — 손으로 적은 상수가 아니라 복원본을 돌려서 얻는다.
                var legacyRng = new System.Random(5150);
                int legacyBullseyes = 0, legacyShots = 0;
                for (int c = 0; c < Samples; c++)
                {
                    ArcheryShotResult[] results = LegacyDeterministicScenario(legacyRng);
                    legacyBullseyes += CountBullseyes(results);
                    legacyShots += results.Length;
                }
                float legacyPerCycle = (float)legacyBullseyes / Samples;
                float legacyAccuracy = (float)legacyBullseyes / legacyShots;

                Assert.AreEqual(expectedPerCycle, observedPerCycle, 0.03f,
                    $"사이클당 정중앙이 관측 {observedPerCycle:F3}회로 이론값 {expectedPerCycle:F3}회와 " +
                    "다릅니다 — 두 추첨이 독립이 아니거나 확률이 다른 곳에서 덮이고 있습니다.");
                Assert.AreEqual(legacyPerCycle, observedPerCycle, 0.05f,
                    $"사이클당 정중앙이 옛 모델 {legacyPerCycle:F3}회 대비 {observedPerCycle:F3}회입니다 — " +
                    "기본값 유도의 전제(기대값 보존)가 깨졌습니다. 보너스 XP 획득량이 함께 흔들립니다.");
                Assert.AreEqual(legacyAccuracy, observedAccuracy, 0.02f,
                    $"정보창 명중률(정중앙/발) 기대가 옛 {legacyAccuracy:P1} 대비 {observedAccuracy:P1}입니다.");
                Assert.AreEqual(expectedAccuracy, observedAccuracy, 0.01f,
                    "명중률 관측이 설정 필드에서 계산한 이론값과 다릅니다.");

                Debug.Log($"[활쏘기확률] 기본값 검산 — 사이클당 정중앙 이론 {expectedPerCycle:F4} / " +
                    $"관측 {observedPerCycle:F4} / 옛 모델 {legacyPerCycle:F4}, " +
                    $"명중률 관측 {observedAccuracy:P2} / 옛 모델 {legacyAccuracy:P2}.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(cfg);
            }
        }

        // ============================================================================
        // ④ 출하 설정이 실제로 확률적인가 + 프로덕션 배선 감사
        // ============================================================================

        [Test]
        public void 출하_기본값이_퇴화하지_않았다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.Greater(cfg.archeryHitChance, 0f,
                    "출하 기본 명중확률이 0입니다 — 사용자에게는 '무조건 다 빗나감'으로 보입니다.");
                Assert.Less(cfg.archeryHitChance, 1f,
                    "출하 기본 명중확률이 1입니다 — 사용자에게는 '무조건 다 명중'으로 보입니다. " +
                    "신고된 병(무조건 같은 결과)이 형태만 바꿔 되돌아온 상태입니다.");
                Assert.Greater(cfg.archeryBullseyeChance, 0f,
                    "출하 기본 정중앙확률이 0입니다 — 정보창 명중률이 영원히 0%가 되고 " +
                    "보너스 XP(progressionBullseyeXp) 획득 경로가 사망합니다.");
                Assert.Less(cfg.archeryBullseyeChance, 1f,
                    "출하 기본 정중앙확률이 1입니다 — 명중이 전부 정중앙이라 외곽 링 착탄이 사라집니다.");

                // 기본값이 확률적이라면 세 발이 서로 다른 결과가 되는 사이클이 실제로 나와야 한다.
                var rng = new System.Random(31337);
                bool mixed = false;
                for (int c = 0; c < Cycles && !mixed; c++)
                {
                    int hits = CountHits(RollCycle(cfg.archeryHitChance, cfg.archeryBullseyeChance, rng));
                    mixed = hits > 0 && hits < ArcheryState.ShotCount;
                }
                Assert.IsTrue(mixed,
                    "출하 기본값에서 '일부만 명중'인 사이클이 한 번도 안 나왔습니다.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// ★ 프로덕션 배선 감사 — 순수 함수만 검사하면 <b>ArcheryState가 그 함수를 실제로 쓰는지</b>는
        /// 아무것도 증명되지 않는다(옛 결정론 코드가 그대로 남아 있어도 위 테스트는 전부 초록이다).
        ///
        /// <para>니들은 <b>전부 <c>nameof</c>로 조립</b>한다 — 식별자가 개명되면 컴파일이 깨져서
        /// 니들이 조용히 썩지 않는다(이 저장소의 "부재 단언은 썩으면 조용히 초록이 된다" 대책).
        /// 그리고 <b>존재 단언과 부재 단언을 같은 테스트에서 대조</b>한다: 같은 읽기 경로로
        /// 하나는 반드시 찾고 하나는 반드시 못 찾아야 한다. 둘 다 못 찾으면 그건 파일을 못 읽은 것이다.</para>
        /// </summary>
        [Test]
        public void 상태_코드가_자리별_독립추첨을_실제로_호출한다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "States",
                nameof(ArcheryState) + ".cs");
            Assert.IsTrue(File.Exists(path), $"프로덕션 소스를 찾지 못했습니다: {path}");
            string source = File.ReadAllText(path);
            Assert.Greater(source.Length, 0, "프로덕션 소스가 비어 있습니다 — 이 감사가 아무것도 못 봅니다.");

            // (가) 존재 단언 = 이 검사가 실제로 소스를 읽고 있다는 양성 대조이기도 하다.
            string call = nameof(ArcheryState.ResolveShotResult) + "(hitChance, ";
            StringAssert.Contains(call, source,
                $"BuildScenario가 {nameof(ArcheryState.ResolveShotResult)}를 자리마다 호출하지 않습니다 — " +
                "확률 모델이 프로덕션에 배선되지 않았습니다.");

            // (가-2) 정중앙 서브추첨이 실제로 배선됐는가 — 설정 필드를 읽고 4인자 오버로드로 넘기는가.
            StringAssert.Contains(nameof(StickConfig.archeryBullseyeChance), source,
                $"BuildScenario가 {nameof(StickConfig.archeryBullseyeChance)}를 읽지 않습니다 — " +
                "정중앙 서브추첨이 프로덕션에 배선되지 않았습니다(리더 판정 ②안).");
            StringAssert.Contains(nameof(ArcheryState.ResolveShotResult) + "(hitChance, bullseyeChance, ", source,
                "명중 확률과 정중앙 확률을 함께 넘기는 호출을 찾지 못했습니다 — 한쪽만 배선된 상태일 수 있습니다.");

            // (나) 부재 단언 = 폐지된 "마지막 발 고정 대입"이 실재했다가 사라진 것.
            string legacyPin = "_results[" + nameof(ArcheryState.ShotCount) + " - 1] = ";
            StringAssert.DoesNotContain(legacyPin, source,
                "폐지된 '마지막 발을 고정 결과로 대입하는' 코드가 아직 남아 있습니다 — " +
                "사용자가 신고한 결정론이 그대로입니다.");

            // (다) 대조 — (나)의 검사 기법이 살아 있는가. 같은 방식으로 지금 실재하는 문자열을 찾는다.
            StringAssert.Contains("_results[i] = ", source,
                "자리별 대입(_results[i] = …)을 찾지 못했습니다 — 부재 단언 (나)의 초록이 " +
                "'없어서'인지 '못 읽어서'인지 구분할 수 없는 상태입니다.");
        }
    }
}
