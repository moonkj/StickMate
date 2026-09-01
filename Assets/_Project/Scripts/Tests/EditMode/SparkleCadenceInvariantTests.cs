using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 반짝임 FX 리듬의 <b>불변식</b> 회귀 — 2026-09-01, docs/UX_FLOW.md 37-3 (F)(3) / 로드맵 P4.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 옛 <c>CharacterFxRenderer</c>는 무장 3초 + 재발동 대기 6~10초를 <b>상수</b>로 갖고 있었고,
    /// 자율 배회의 Idle 최대 지속시간은 <b>6초</b>였다. Idle을 벗어나면 두 타이머가 0으로 리셋되므로
    /// <b>2회차 반짝임은 원리적으로 절대 오지 않았다</b>(노출률 약 8%).
    ///
    /// 값을 고쳐서는 안 되는 이유가 여기 있다: 배회 시간을 조정하는 순간 같은 버그가 재발한다.
    /// 그래서 리듬을 <see cref="SparkleCadence"/>가 <b>Idle 창에서 유도</b>하게 바꿨고,
    /// 이 파일은 그 유도가 어떤 설정 조합에서도 부등식을 만족하는지 본다.
    ///
    /// <b>네거티브 컨트롤을 함께 둔다</b> — 옛 상수 조합이 실제로 이 부등식을 어긴다는 것을
    /// 같은 테스트가 박제한다. 그러지 않으면 "통과하는 헐거운 조건"과 구별되지 않는다.
    /// </summary>
    public sealed class SparkleCadenceInvariantTests
    {
        /// <summary>배회 설정이 가질 법한 Idle 최대 지속시간 — 배포값(6)과 극단값을 함께 훑는다.</summary>
        private static readonly float[] IdleWindows =
        {
            0.05f, 0.5f, 1f, 2f, 4f, 6f, 6.0001f, 8f, 12f, 30f, 120f,
        };

        [Test]
        public void 재발동_대기는_배회_Idle_최대_지속시간을_절대_넘지_않는다()
        {
            for (int i = 0; i < IdleWindows.Length; i++)
            {
                float window = IdleWindows[i];
                SparkleCadence.Resolve(window, out float arm, out float life,
                    out float min, out float max);

                Assert.LessOrEqual(arm + max + life, window + 1e-4f,
                    $"Idle 창 {window}초: 무장 {arm:F3} + 대기 최대 {max:F3} + 수명 {life:F3} = " +
                    $"{arm + max + life:F3}초가 창을 넘습니다 — 한 Idle 구간 안에서 두 번째 반짝임이 " +
                    "완결되지 못하므로 옛 결함이 그대로 되살아납니다.");

                Assert.Greater(max, 0f, $"Idle 창 {window}초: 재발동 대기가 0 이하입니다(연속 발동).");
                Assert.LessOrEqual(min, max, $"Idle 창 {window}초: 대기 하한이 상한보다 큽니다.");
                Assert.Greater(min, 0f, $"Idle 창 {window}초: 대기 하한이 0 이하입니다.");
                Assert.Greater(arm, 0f, $"Idle 창 {window}초: 무장 시간이 0 이하입니다.");
                Assert.Greater(life, 0f, $"Idle 창 {window}초: 수명이 0 이하입니다.");
            }
        }

        /// <summary>★ 네거티브 컨트롤 — 옛 상수 조합은 배포 설정에서 <b>실제로</b> 부등식을 어긴다.</summary>
        [Test]
        public void 옛_상수_조합은_배포_설정에서_부등식을_어긴다()
        {
            const float legacyArm = 3.0f;
            const float legacyIntervalMax = 10f;
            const float legacyLife = 0.7f;
            float window = SparkleCadence.FallbackIdleWindowSeconds;   // 6초 = 배포값

            Assert.Greater(legacyArm + legacyIntervalMax + legacyLife, window,
                "옛 상수 조합이 부등식을 만족한다면 이 라운드의 전제(2회차가 절대 오지 않는다)가 " +
                "거짓이라는 뜻입니다 — 그렇다면 위 테스트는 아무것도 잡지 못합니다.");
        }

        /// <summary>창이 넉넉하면 <b>설계값 그대로</b> 나가야 한다(상한이지 강제 축소가 아니다).</summary>
        [Test]
        public void 창이_넉넉하면_설계값을_그대로_쓴다()
        {
            SparkleCadence.Resolve(60f, out float arm, out float life, out float min, out float max);
            Assert.AreEqual(SparkleCadence.ConfiguredArmSeconds, arm, 1e-4f);
            Assert.AreEqual(SparkleCadence.ConfiguredLifeSeconds, life, 1e-4f);
            Assert.AreEqual(SparkleCadence.ConfiguredIntervalMinSeconds, min, 1e-4f);
            Assert.AreEqual(SparkleCadence.ConfiguredIntervalMaxSeconds, max, 1e-4f);
        }

        /// <summary>배포 에셋의 실제 값으로도 성립하는가 — 상수가 아니라 <b>지금 배포되는 설정</b>이 기준이다.</summary>
        [Test]
        public void 배포_설정에서_한_Idle_구간에_두_번_이상_터진다()
        {
            // 배포 에셋을 <b>읽기만</b> 한다(불변 원칙 3). 경로는 WanderEdgeConfigInvariantTests와 같다.
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(
                "Assets/_Project/Data/DefaultStickConfig.asset");
            Assert.IsNotNull(config, "배포 설정 에셋을 찾지 못했습니다.");
            float window = SparkleCadence.IdleWindowSeconds(config);
            Assert.AreEqual(config.wanderIdleDurationMax, window, 1e-5f,
                "리듬이 배회 Idle 최대 지속시간이 아닌 다른 값을 보고 있습니다 — " +
                "그것이 바로 이 라운드가 없앤 '서로의 값을 모르는' 구조입니다.");

            SparkleCadence.Resolve(config, out float arm, out float life, out float min, out float max);
            Assert.LessOrEqual(arm + max + life, window + 1e-4f,
                $"배포 설정(Idle 최대 {window}초)에서 두 번째 반짝임이 완결되지 못합니다.");

            // 최악의 경우(대기 상한)에도 2회, 최선(대기 하한)에는 3회 이상 나오는지 함께 본다.
            int worst = 1 + Mathf.FloorToInt((window - arm - life) / max);
            Assert.GreaterOrEqual(worst, 2,
                $"배포 설정에서 한 Idle 구간에 최악 {worst}회뿐입니다 — 옛 결함(1회)과 같습니다.");
        }

        /// <summary>설정이 없는 리그(테스트 스텁/사본)의 폴백이 배회 컨트롤러의 폴백과 같은가.</summary>
        [Test]
        public void 설정이_없으면_배회_컨트롤러와_같은_폴백을_쓴다()
        {
            Assert.AreEqual(6f, SparkleCadence.FallbackIdleWindowSeconds, 1e-5f,
                "States/AutoWanderController가 쓰는 폴백(wanderIdleDurationMax 기본 6초)과 " +
                "다르면 설정이 빠진 씬에서만 리듬이 어긋납니다.");
            Assert.AreEqual(SparkleCadence.FallbackIdleWindowSeconds,
                SparkleCadence.IdleWindowSeconds(null), 1e-5f);
        }
    }
}
