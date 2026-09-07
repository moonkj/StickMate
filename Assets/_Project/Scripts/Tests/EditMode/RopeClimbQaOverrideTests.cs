using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ QA 전용 밧줄등반 오버라이드(<see cref="RopeClimbQaOverride"/>) 회귀 테스트 —
    /// docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §9-4-C의 구현 검증.
    ///
    /// 무엇을 잡으려는가:
    ///  (1) 확률 오버라이드 파싱 — 유효한 0..1 값만 통과하고, 범위 밖/파싱 불가/미설정은 전부
    ///      "오버라이드 없음"(null)으로 떨어진다(=StickConfig.ropeClimbChance가 그대로 쓰인다).
    ///  (2) 합성 시험벽 스위치 — StickMateDevTools와 같은 "1/true/on/yes" 관례.
    ///  (3) ★ 이 스위치들은 EquipmentDebugUnlock과 달리 "에디터/개발빌드는 무조건 열림" 가지가
    ///      <b>없다</b>(RopeClimbQaOverride.cs 클래스 문서의 구조적 이유) — 환경변수가 실제로 설정된
    ///      경우에만 켜진다는 것을 developmentConfiguration 인자 없이 직접 확인한다.
    ///  (4) 환경변수 이름이 STICKMATE_UNLOCK_ALL/STICKMATE_DEVTOOLS와 겹치지 않는다(겹치면 서로 다른
    ///      QA 목적이 한 스위치에 묶여 버린다).
    ///  (5) 프로퍼티(ChanceOverride/TestWallEnabled)와 SourceLabel/SetTestOverride 왕복 — 게이트뿐
    ///      아니라 실제 소비 경로가 읽는 값도 맞는지.
    /// </summary>
    public sealed class RopeClimbQaOverrideTests
    {
        [TearDown]
        public void RestoreSuiteDefault()
        {
            // 스위트 규약으로 되돌린다 — 여기서 새면 뒤 테스트가 조용히 물러진다(EquipmentDebugUnlockReleaseGateTests와 동일 관례).
            RopeClimbQaOverride.SetChanceTestOverride(null);
            RopeClimbQaOverride.SetWallTestOverride(null);
        }

        // ============================================================================
        // (1) 확률 오버라이드 — 순수 함수
        // ============================================================================

        [TestCase("0", 0f)]
        [TestCase("1", 1f)]
        [TestCase("0.2", 0.2f)]
        [TestCase(" 0.85 ", 0.85f)]
        public void 확률_오버라이드_유효한_값은_그대로_파싱된다(string raw, float expected)
        {
            float? resolved = RopeClimbQaOverride.ResolveChanceOverride(raw);
            Assert.IsTrue(resolved.HasValue, $"\"{raw}\"가 유효한 확률로 파싱되지 않았습니다.");
            Assert.AreEqual(expected, resolved.Value, 0.0001f);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("-0.1")]
        [TestCase("1.1")]
        [TestCase("아무거나")]
        [TestCase("1,0")] // 로케일에 따라 소수점이 쉼표인 문자열 — InvariantCulture로 실패해야 한다.
        public void 확률_오버라이드_무효한_값은_오버라이드없음으로_떨어진다(string raw)
        {
            Assert.IsNull(RopeClimbQaOverride.ResolveChanceOverride(raw),
                $"\"{raw ?? "null"}\"이 유효한 확률로 잘못 파싱됐습니다 — StickConfig 원래 값을 조용히 뭉갤 수 있습니다.");
        }

        // ============================================================================
        // (2) 합성 시험벽 스위치 — StickMateDevTools와 같은 관례
        // ============================================================================

        [TestCase("1", true)]
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("on", true)]
        [TestCase("yes", true)]
        [TestCase(" 1 ", true)]
        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("0", false)]
        [TestCase("false", false)]
        [TestCase("off", false)]
        [TestCase("2", false)]
        [TestCase("아무거나", false)]
        public void 시험벽_스위치는_StickMateDevTools와_같은_문자열_관례를_따른다(string raw, bool expected)
        {
            Assert.AreEqual(expected, RopeClimbQaOverride.ResolveTestWallEnabled(raw), $"\"{raw ?? "null"}\" 판정 불일치.");
            // 대조 — 실제로 같은 함수를 재사용하고 있는지(형태만 흉내 낸 별도 구현이 아닌지) 값으로 못박는다.
            Assert.AreEqual(StickMateDevTools.ResolveFromEnvironmentValue(raw), RopeClimbQaOverride.ResolveTestWallEnabled(raw),
                "StickMateDevTools.ResolveFromEnvironmentValue와 판정이 갈립니다 — 문자열 해석 관례가 스위치마다 갈리면 안 됩니다.");
        }

        // ============================================================================
        // (3) "에디터는 무조건 열림" 가지가 없다 — developmentConfiguration 없이도 환경변수만 본다
        // ============================================================================

        [Test]
        public void 환경변수가_없으면_에디터에서도_확률오버라이드는_닫혀있다()
        {
            // 이 프로세스는 지금 에디터(UNITY_EDITOR)로 돌고 있지만, 그 사실이 확률 오버라이드에
            // 아무 영향을 주지 않아야 한다 — EquipmentDebugUnlock과 달리 "무조건 열림" 가지가 없다.
            Assert.IsNull(RopeClimbQaOverride.ResolveChanceOverride(null),
                "에디터에서 환경변수 없이도 확률 오버라이드가 열려 있습니다 — 모든 빌드 구성에서 " +
                "환경변수 미설정 시 StickConfig 원래 값이 그대로 쓰여야 합니다.");
        }

        [Test]
        public void 환경변수가_없으면_에디터에서도_시험벽은_닫혀있다()
        {
            Assert.IsFalse(RopeClimbQaOverride.ResolveTestWallEnabled(null),
                "에디터에서 환경변수 없이도 시험벽이 열려 있습니다.");
        }

        // ============================================================================
        // (4) 환경변수 이름 — 기존 QA 스위치와 겹치지 않는다
        // ============================================================================

        [Test]
        public void 환경변수_이름은_기존_QA_스위치와_겹치지_않는다()
        {
            Assert.AreEqual("STICKMATE_QA_ROPE_CLIMB_CHANCE", RopeClimbQaOverride.ChanceEnvironmentVariableName);
            Assert.AreEqual("STICKMATE_QA_ROPE_CLIMB_TEST_WALL", RopeClimbQaOverride.TestWallEnvironmentVariableName);

            Assert.AreNotEqual(RopeClimbQaOverride.ChanceEnvironmentVariableName, RopeClimbQaOverride.TestWallEnvironmentVariableName,
                "확률 오버라이드와 시험벽 스위치가 같은 환경변수를 씁니다 — 하나만 켜고 싶어도 둘 다 켜집니다.");
            Assert.AreNotEqual(EquipmentDebugUnlock.EnvironmentVariableName, RopeClimbQaOverride.ChanceEnvironmentVariableName,
                "확률 오버라이드가 STICKMATE_UNLOCK_ALL과 같은 환경변수를 씁니다 — 장비 QA와 배회 AI 확률 QA가 뒤섞입니다.");
            Assert.AreNotEqual(EquipmentDebugUnlock.EnvironmentVariableName, RopeClimbQaOverride.TestWallEnvironmentVariableName,
                "시험벽 스위치가 STICKMATE_UNLOCK_ALL과 같은 환경변수를 씁니다.");
            Assert.AreNotEqual(StickMateDevTools.EnvironmentVariableName, RopeClimbQaOverride.ChanceEnvironmentVariableName,
                "확률 오버라이드가 STICKMATE_DEVTOOLS와 같은 환경변수를 씁니다.");
            Assert.AreNotEqual(StickMateDevTools.EnvironmentVariableName, RopeClimbQaOverride.TestWallEnvironmentVariableName,
                "시험벽 스위치가 STICKMATE_DEVTOOLS와 같은 환경변수를 씁니다.");
        }

        // ============================================================================
        // (5) 프로퍼티/SourceLabel/SetTestOverride 왕복 — 실제 소비 경로가 읽는 값
        // ============================================================================

        [Test]
        public void 테스트_강제값을_주면_ChanceOverride가_그대로_반영된다()
        {
            RopeClimbQaOverride.SetChanceTestOverride(0.42f);
            Assert.AreEqual(0.42f, RopeClimbQaOverride.ChanceOverride, 0.0001f);
            StringAssert.Contains("테스트 강제", RopeClimbQaOverride.ChanceSourceLabel);

            RopeClimbQaOverride.SetChanceTestOverride(null);
            // 강제를 풀면 실제 판정 경로로 돌아간다 — 이 프로세스엔 환경변수가 없으므로 null이어야 한다.
            Assert.IsNull(RopeClimbQaOverride.ChanceOverride,
                "테스트 강제를 풀었는데도 확률 오버라이드가 남아 있습니다 — 실제 환경변수 판정 경로로 되돌아가지 않았습니다.");
        }

        [Test]
        public void 테스트_강제값을_주면_TestWallEnabled가_그대로_반영된다()
        {
            RopeClimbQaOverride.SetWallTestOverride(true);
            Assert.IsTrue(RopeClimbQaOverride.TestWallEnabled);
            Assert.AreEqual("테스트 강제 ON", RopeClimbQaOverride.WallSourceLabel);

            RopeClimbQaOverride.SetWallTestOverride(false);
            Assert.IsFalse(RopeClimbQaOverride.TestWallEnabled);
            Assert.AreEqual("테스트 강제 OFF", RopeClimbQaOverride.WallSourceLabel);

            RopeClimbQaOverride.SetWallTestOverride(null);
            Assert.IsFalse(RopeClimbQaOverride.TestWallEnabled,
                "테스트 강제를 풀었는데도 시험벽이 켜져 있습니다 — 실제 환경변수 판정 경로로 되돌아가지 않았습니다.");
        }
    }
}
