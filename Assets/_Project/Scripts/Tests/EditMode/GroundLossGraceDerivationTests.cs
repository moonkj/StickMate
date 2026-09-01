using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 사용자 신고 "캐릭터가 창에서 가끔 갑자기 떨어짐"의 <b>근본 원인 2</b> 회귀 잠금
    /// (디버거 가설 H5 — 성립).
    ///
    /// <para>무엇이 문제였나: 발판 상실 유예의 목적은 "창 열거가 한 번 튀는 것"을 흡수하는 것이다.
    /// 그런데 발판 목록은 <c>footholdPollInterval</c>(0.3초) 동안 캐시로 고정돼 있어서, 열거가 한 번만
    /// 튀면 그 나쁜 목록이 <b>0.3초 내내</b> 유지된다. 예전 유예는 <c>fallGraceDuration</c>(0.1초)를
    /// 그대로 썼으므로 캐시 수명의 1/3이었고, 설계 목적을 <b>원리적으로</b> 수행할 수 없었다.</para>
    ///
    /// <para>이 파일은 "숫자를 베끼지 않는다"까지 함께 잠근다 — 모든 단언이 <c>StickConfig</c>의 필드에서
    /// 유도되므로, 폴링 주기를 바꾸면 기대값도 함께 움직인다(0.45라는 숫자가 이 파일에 없다).</para>
    /// </summary>
    public sealed class GroundLossGraceDerivationTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        private static StickConfig NewCodeDefault() => ScriptableObject.CreateInstance<StickConfig>();

        // ====================================================================
        // (1) 핵심 계약 — 유예는 폴링 간격 이상이다
        // ====================================================================

        [Test]
        public void 발판상실_유예는_창열거_폴링간격_이상이다()
        {
            StickConfig c = NewCodeDefault();
            try
            {
                float grace = c.ResolveGroundLossGraceDuration();
                Assert.GreaterOrEqual(grace, c.footholdPollInterval,
                    $"발판 상실 유예({grace:F3}초)가 창 열거 폴링 간격({c.footholdPollInterval:F3}초)보다 짧습니다 — " +
                    "열거가 한 번만 튀어도 그 나쁜 목록이 폴링 주기 내내 유지되므로 유예가 흡수할 수 " +
                    "없습니다(디버거 가설 H5). 이것이 신고 '창에서 가끔 갑자기 떨어짐'의 근본 원인 2입니다.");
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void 발판상실_유예는_폴링간격x배수로_유도된다()
        {
            StickConfig c = NewCodeDefault();
            try
            {
                float expected = c.footholdPollInterval * c.groundLossGracePollIntervalMultiplier;
                Assert.AreEqual(expected, c.ResolveGroundLossGraceDuration(), 1e-5f,
                    "유예가 폴링 간격 x 배수에서 유도되지 않았습니다 — 숫자를 따로 적어 두면 " +
                    "폴링 주기를 바꿨을 때 조용히 어긋납니다.");
            }
            finally { Object.DestroyImmediate(c); }
        }

        [TestCase(0.1f)]
        [TestCase(0.3f)]
        [TestCase(0.75f)]
        [TestCase(2.0f)]
        public void 폴링주기를_바꾸면_유예가_따라간다(float pollInterval)
        {
            StickConfig c = NewCodeDefault();
            try
            {
                c.footholdPollInterval = pollInterval;
                float grace = c.ResolveGroundLossGraceDuration();
                Assert.GreaterOrEqual(grace, pollInterval,
                    $"폴링 주기 {pollInterval:F2}초에서 유예({grace:F3}초)가 그보다 짧습니다 — " +
                    "유도식이 끊어졌습니다.");
            }
            finally { Object.DestroyImmediate(c); }
        }

        // ====================================================================
        // (2) 하한 — 착지 확정용 fallGraceDuration을 절대 아래로 깎지 않는다
        // ====================================================================

        [Test]
        public void fallGraceDuration이_더_길면_그쪽을_쓴다()
        {
            StickConfig c = NewCodeDefault();
            try
            {
                c.footholdPollInterval = 0.05f;
                c.fallGraceDuration = 1.25f;
                Assert.AreEqual(c.fallGraceDuration, c.ResolveGroundLossGraceDuration(), 1e-5f,
                    "유도값이 fallGraceDuration보다 작을 때 그쪽으로 내려가면, 이 값을 길게 잡아 둔 " +
                    "설정이 조용히 무시됩니다(max여야 합니다).");
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void 배수를_0으로_두면_예전거동이_된다()
        {
            // 네거티브 컨트롤 — 이 스위치가 실제로 유도를 껐다 켰다 하는지 확인한다.
            StickConfig c = NewCodeDefault();
            try
            {
                c.groundLossGracePollIntervalMultiplier = 0f;
                Assert.AreEqual(c.fallGraceDuration, c.ResolveGroundLossGraceDuration(), 1e-5f,
                    "배수를 0으로 두었는데도 유예가 fallGraceDuration으로 돌아가지 않았습니다 — " +
                    "탈출구가 동작하지 않으면 회귀 시 되돌릴 방법이 없습니다.");
            }
            finally { Object.DestroyImmediate(c); }
        }

        // ====================================================================
        // (3) 코드 기본값 ↔ 배포 에셋 (DockGeometryInvariantTests와 같은 계열의 지뢰 제거)
        // ====================================================================

        [Test]
        public void 신규_3필드가_코드기본값과_배포에셋에서_같아야_한다()
        {
            StickConfig deployed = LoadDeployedConfig();
            StickConfig codeDefault = NewCodeDefault();
            try
            {
                Assert.AreEqual(codeDefault.groundLossGracePollIntervalMultiplier,
                    deployed.groundLossGracePollIntervalMultiplier, 1e-5f,
                    "groundLossGracePollIntervalMultiplier의 코드 기본값과 배포 에셋값이 다릅니다 — " +
                    "CreateInstance<StickConfig>()로 도는 테스트가 배포판과 다른 유예에서 돕니다(m3와 같은 지뢰).");
                Assert.AreEqual(codeDefault.groundedGravitySuppressionEnabled,
                    deployed.groundedGravitySuppressionEnabled,
                    "groundedGravitySuppressionEnabled의 코드 기본값과 배포 에셋값이 다릅니다.");
                Assert.AreEqual(codeDefault.overlayOriginSanityCheckEnabled,
                    deployed.overlayOriginSanityCheckEnabled,
                    "overlayOriginSanityCheckEnabled의 코드 기본값과 배포 에셋값이 다릅니다.");
            }
            finally { Object.DestroyImmediate(codeDefault); }
        }

        [Test]
        public void 배포에셋에서도_유예가_폴링간격_이상이다()
        {
            StickConfig deployed = LoadDeployedConfig();
            float grace = deployed.ResolveGroundLossGraceDuration();
            Debug.Log($"[유예유도] 배포 에셋 — 폴링 {deployed.footholdPollInterval:F2}초 x " +
                $"배수 {deployed.groundLossGracePollIntervalMultiplier:F2} = {grace:F3}초 " +
                $"(fallGraceDuration {deployed.fallGraceDuration:F2}초).");
            Assert.GreaterOrEqual(grace, deployed.footholdPollInterval,
                "실제 배포 설정에서 유예가 폴링 간격보다 짧습니다 — 코드가 아니라 에셋이 회귀했습니다.");
        }
    }
}
