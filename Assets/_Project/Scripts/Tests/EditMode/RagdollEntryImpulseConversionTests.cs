using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ P9-b 회귀 잠금 ①(결정론 파트) — <b>"판정용 충격량을 그대로 넘기지 않는다."</b>
    ///
    /// ============================================================================
    /// 무엇을 막는 테스트인가
    /// ============================================================================
    /// P9-a가 <c>RagdollRig.EnterRagdoll(방향, 충격량)</c>을 만들었고 P9-b가 거기에 생산자를 배선했다.
    /// 그 배선에서 <b>단 하나의 진짜 위험</b>은 <c>StickmanBlackboard.LastImpactMagnitude</c>를 변환 없이
    /// 넘기는 것이다. 실측 감도가 <b>1N·s당 42.8도/초</b>이므로:
    /// <code>
    ///   임계값 5배(40N·s) 그대로  ->  40 x 42.8 = 1712도/초 = 초당 4.8바퀴  (팽이)
    ///   환산 + 상한 클램프 후     ->            400도/초 = 초당 1.1바퀴  (넘어짐)
    /// </code>
    /// 이 파일은 <see cref="RagdollImpactResolver.ResolveEntryImpulse"/>가 순수 함수라는 점을 이용해
    /// <b>물리 시뮬레이션 없이</b> 그 계약을 잠근다(PlayMode는 느리고, 여기서 잡을 수 있는 회귀를
    /// PlayMode에 미루면 실패 원인이 물리 노이즈와 뒤섞인다). 실제 각속도가 정말로 이 예측과 맞는지는
    /// Tests/PlayMode/RagdollEntryImpulseWiringTests가 씬에서 잰다 — 역할 분담이다:
    /// <list type="bullet">
    /// <item>여기(EditMode): <b>설계한 대로 계산되는가</b> — 범위/단조성/상한/OFF 스위치/방어.</item>
    /// <item>PlayMode: <b>계산이 현실과 맞는가</b> — 감도 상수가 여전히 유효하고 부호가 옳은가.</item>
    /// </list>
    /// </summary>
    public sealed class RagdollEntryImpulseConversionTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>설계 목표 구간(도/초) — 리더 지시. 임계값 바로 위는 은은하게, 최대라도 이 위는 안 된다.</summary>
        private const float DesignMinDegreesPerSecond = 90f;
        private const float DesignMaxDegreesPerSecond = 400f;

        /// <summary>"절대 나오면 안 되는" 값 — 임계값 5배를 그대로 넘겼을 때의 실측 예측치(초당 약 5바퀴).</summary>
        private const float RunawayDegreesPerSecond = 1700f;

        private static StickConfig CreateCodeDefaultConfig() => ScriptableObject.CreateInstance<StickConfig>();

        /// <summary>진입 충격량(N·s)을 실측 감도로 예상 진입 각속도(도/초)로 환산한다.</summary>
        private static float PredictDegreesPerSecond(StickConfig config, float rawImpactMagnitude)
            => RagdollImpactResolver.ResolveEntryImpulse(config, rawImpactMagnitude)
               * config.ragdollEntryAngularSensitivityPerImpulse;

        // ========================================================================
        // (1) 설계 범위 — 임계값 바로 위는 은은하게, 강한 충격은 크지만 만화적이지 않게
        // ========================================================================

        [Test]
        public void 임계값_1배부터_5배까지_진입각속도가_설계범위_90에서_400도per초_안에_있어야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                float threshold = config.ragdollForceThreshold;

                // 실제로 존재하는 배율들: 1.02(긴 망토), 1.25(로데오 흔들기), 2.0/4.0(대사 구간 경계),
                // 5.0(회귀 테스트들이 쓰는 "확실히 넘기는" 값).
                float[] ratios = { 1.00f, 1.02f, 1.25f, 1.50f, 2.00f, 3.00f, 4.00f, 5.00f };
                foreach (float ratio in ratios)
                {
                    float raw = threshold * ratio;
                    float degrees = PredictDegreesPerSecond(config, raw);
                    Debug.Log($"[P9B-CONV] 임계값 {ratio:F2}배(원본 {raw:F1}N·s) -> 진입 충격량 " +
                        $"{RagdollImpactResolver.ResolveEntryImpulse(config, raw):F3}N·s -> 예상 {degrees:F1}도/초 " +
                        $"({degrees / 360f:F2}회전/초)");

                    Assert.GreaterOrEqual(degrees, DesignMinDegreesPerSecond,
                        $"임계값 {ratio:F2}배 충격의 진입 각속도가 {degrees:F1}도/초로 설계 하한 " +
                        $"{DesignMinDegreesPerSecond}도/초보다 약합니다 — 얻어맞아도 팔다리가 안 휘둘립니다(P9-a 이전으로 회귀).");
                    Assert.LessOrEqual(degrees, DesignMaxDegreesPerSecond + 0.5f,
                        $"임계값 {ratio:F2}배 충격의 진입 각속도가 {degrees:F1}도/초로 설계 상한 " +
                        $"{DesignMaxDegreesPerSecond}도/초를 넘었습니다 — 상한 클램프가 동작하지 않습니다.");
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void 임계값_바로_위는_상한의_절반_아래로_은은해야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                // 지금 존재하는 가장 약한 랙돌 = 긴 망토(임계값 1.02배). "아프지 않게 보이도록"이
                // 33절의 의도이므로, 최대치와 구분이 안 되면 강약 표현 자체가 없는 것과 같다.
                float weak = PredictDegreesPerSecond(config, config.ragdollForceThreshold * 1.02f);
                float strong = PredictDegreesPerSecond(config, config.ragdollForceThreshold * 5f);
                Debug.Log($"[P9B-CONV] 최약(1.02배) {weak:F1}도/초 vs 최강(포화) {strong:F1}도/초 — 비 {strong / weak:F2}배");

                Assert.Less(weak, strong * 0.5f,
                    $"최약 충격({weak:F1}도/초)이 최강({strong:F1}도/초)의 절반 이상입니다 — 강약 구분이 사라졌습니다.");
                Assert.GreaterOrEqual(weak, DesignMinDegreesPerSecond,
                    $"최약 충격이 {weak:F1}도/초로 설계 하한 미만입니다 — 넘어지는 게 눈에 안 보입니다.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ========================================================================
        // (2) 네거티브 컨트롤 — 상한 클램프가 없으면 어떤 값이 나오는가
        // ========================================================================

        [Test]
        public void 상한클램프가_초당_5바퀴를_실제로_막아야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                float threshold = config.ragdollForceThreshold;
                float sensitivity = config.ragdollEntryAngularSensitivityPerImpulse;

                // 변환 없이 그대로 넘겼을 때(= 이 테스트가 막으려는 그 버그)의 실측 예측치.
                float naive = threshold * 5f * sensitivity;
                float actual = PredictDegreesPerSecond(config, threshold * 5f);
                Debug.Log($"[P9B-CONV] 네거티브 컨트롤 — 원본 그대로 넘기면 {naive:F0}도/초" +
                    $"({naive / 360f:F1}회전/초), 환산 후 {actual:F1}도/초({actual / 360f:F2}회전/초). " +
                    $"삭감비 {actual / naive:F3}");

                Assert.Greater(naive, RunawayDegreesPerSecond,
                    $"전제가 무너졌습니다 — 원본을 그대로 넘겨도 {naive:F0}도/초뿐이라면 이 테스트의 " +
                    "네거티브 컨트롤이 더 이상 폭주를 대표하지 않습니다(감도/임계값이 크게 바뀐 것).");
                Assert.Less(actual, 360f * 1.5f,
                    $"환산 후에도 {actual:F0}도/초(초당 {actual / 360f:F1}바퀴)입니다 — 얻어맞은 게 아니라 팽이입니다.");

                // 임계값의 100배가 들어와도(있을 수 없는 값이지만 방어) 상한을 넘지 않는다.
                float absurd = PredictDegreesPerSecond(config, threshold * 100f);
                Assert.LessOrEqual(absurd, DesignMaxDegreesPerSecond + 0.5f,
                    $"임계값 100배 입력에서 {absurd:F1}도/초가 나왔습니다 — 클램프가 상한이 아니라 " +
                    "비례 축소로 구현돼 있을 수 있습니다.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ========================================================================
        // (3) 단조성 — 세게 맞을수록 약해지는 구간이 없어야 한다
        // ========================================================================

        [Test]
        public void 충격이_커질수록_진입충격량이_줄어드는_구간이_없어야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                float previous = -1f;
                for (float ratio = 0.1f; ratio <= 12f; ratio += 0.1f)
                {
                    float current = RagdollImpactResolver.ResolveEntryImpulse(config, config.ragdollForceThreshold * ratio);
                    Assert.GreaterOrEqual(current, previous - 1e-5f,
                        $"임계값 {ratio:F1}배에서 진입 충격량이 직전보다 줄었습니다({previous:F4} -> {current:F4}) — " +
                        "세게 맞을수록 덜 날아가는 구간이 생겼습니다(상한을 Min이 아니라 나눗셈으로 구현하면 이렇게 됩니다).");
                    previous = current;
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ========================================================================
        // (4) OFF 스위치와 방어적 입력
        // ========================================================================

        [Test]
        public void 목표각속도를_0으로_두면_진입충격량이_완전히_사라져야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                config.ragdollEntryAngularVelocityAtThreshold = 0f;
                Assert.AreEqual(0f, RagdollImpactResolver.ResolveEntryImpulse(config, config.ragdollForceThreshold * 5f), 1e-6f,
                    "ragdollEntryAngularVelocityAtThreshold = 0이 되돌리기 스위치여야 합니다(P9-a 이전 거동).");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void 유효하지_않은_입력에는_힘을_가하지_않아야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                Assert.AreEqual(0f, RagdollImpactResolver.ResolveEntryImpulse(config, 0f), 1e-6f, "충격량 0");
                Assert.AreEqual(0f, RagdollImpactResolver.ResolveEntryImpulse(config, -10f), 1e-6f, "충격량 음수");
                Assert.AreEqual(0f, RagdollImpactResolver.ResolveEntryImpulse(config, float.NaN), 1e-6f, "충격량 NaN");

                // config가 null이어도(손조립 리그/에디터) 폴백 상수로 정상 계산되어야 한다.
                float fallback = RagdollImpactResolver.ResolveEntryImpulse(null, 8f * 5f);
                Assert.Greater(fallback, 0f, "config가 null이면 폴백 상수로 계산해야 합니다.");
                Assert.LessOrEqual(fallback * 42.8f, DesignMaxDegreesPerSecond + 1f,
                    "폴백 경로에도 상한 클램프가 적용돼야 합니다.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void 상한이_목표보다_낮게_오설정돼도_역전구간이_생기지_않아야_한다()
        {
            StickConfig config = CreateCodeDefaultConfig();
            try
            {
                config.ragdollEntryAngularVelocityCap = 10f;   // 목표(100)보다 낮은 오설정.
                float atThreshold = RagdollImpactResolver.ResolveEntryImpulse(config, config.ragdollForceThreshold);
                float atFiveTimes = RagdollImpactResolver.ResolveEntryImpulse(config, config.ragdollForceThreshold * 5f);
                Assert.AreEqual(atThreshold, atFiveTimes, 1e-5f,
                    "상한이 목표보다 낮으면 목표까지 끌어올려 포화시켜야 합니다(그렇지 않으면 임계값 근처에서 " +
                    "이미 상한에 걸려 '세게 맞을수록 약해지는' 구간이 생깁니다).");
                Assert.Greater(atThreshold, 0f, "오설정이라도 힘 자체가 사라지면 안 됩니다.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        // ========================================================================
        // (5) 코드 기본값 ↔ 배포 에셋 표류 방지 (DockGeometryInvariantTests와 같은 관례)
        // ========================================================================

        [Test]
        public void 진입충격량_설정_3종의_코드기본값이_배포에셋과_같아야_한다()
        {
            var deployed = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(deployed, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");

            StickConfig codeDefault = CreateCodeDefaultConfig();
            try
            {
                Assert.AreEqual(deployed.ragdollEntryAngularVelocityAtThreshold,
                    codeDefault.ragdollEntryAngularVelocityAtThreshold, 0.001f,
                    "ragdollEntryAngularVelocityAtThreshold가 코드 기본값과 배포 에셋에서 다릅니다 — " +
                    "CreateInstance<StickConfig>()를 쓰는 테스트가 배포판과 다른 세기로 돌게 됩니다.");
                Assert.AreEqual(deployed.ragdollEntryAngularVelocityCap,
                    codeDefault.ragdollEntryAngularVelocityCap, 0.001f,
                    "ragdollEntryAngularVelocityCap이 코드 기본값과 배포 에셋에서 다릅니다.");
                Assert.AreEqual(deployed.ragdollEntryAngularSensitivityPerImpulse,
                    codeDefault.ragdollEntryAngularSensitivityPerImpulse, 0.001f,
                    "ragdollEntryAngularSensitivityPerImpulse가 코드 기본값과 배포 에셋에서 다릅니다.");
            }
            finally { Object.DestroyImmediate(codeDefault); }
        }
    }
}
