using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 (debugger D1) — "실제 시간 N초마다 한 번"이 <b>호출 빈도와 무관하게</b> 지켜지는가,
    /// 그리고 <c>[표시모니터]</c> 줄이 근거·사유가 바뀔 때만 찍히는가.
    /// </summary>
    public sealed class WallClockIntervalGateTests
    {
        // 이 값들은 프로덕션 상수가 아니라 이 테스트가 고른 시나리오다(Enforcer의 게이트 주기 0.25/0.5초와 같은 형태).
        private const double Interval = 1.0;

        [Test]
        public void 첫_호출은_즉시_열린다()
        {
            var g = new WallClockIntervalGate();
            Assert.IsFalse(g.IsArmed);
            Assert.IsTrue(g.TryConsume(123.0, Interval));
            Assert.IsTrue(g.IsArmed);
        }

        [TestCase(0.25)]
        [TestCase(0.5)]
        public void 드물게_불려도_실제_시간_간격대로_열린다(double callEvery)
        {
            var g = new WallClockIntervalGate();
            int opened = 0;
            const double duration = 10.0;
            for (double now = 0.0; now < duration; now += callEvery)
            {
                if (g.TryConsume(now, Interval)) opened++;
            }
            // 0,1,2,…,9초에 열린다 = 10회. 옛 누적식(호출당 한 프레임 dt만 더함)이면 10초에 한 번 남짓이다.
            Assert.That(opened, Is.InRange((int)(duration / Interval), (int)(duration / Interval) + 1),
                $"{callEvery}초마다 불렀는데 10초 동안 {opened}회 열렸다.");
        }

        [Test]
        public void 늦게_불리면_한_번만_열리고_다음_기준은_그_시각부터다()
        {
            var g = new WallClockIntervalGate();
            Assert.IsTrue(g.TryConsume(0.0, Interval));
            Assert.IsTrue(g.TryConsume(5.3, Interval), "간격을 한참 넘겨 불리면 곧바로 열린다.");
            Assert.IsFalse(g.TryConsume(5.9, Interval), "밀린 횟수만큼 연달아 열리지 않는다.");
            Assert.IsTrue(g.TryConsume(6.3, Interval));
        }

        [Test]
        public void 간격이_0_이하면_매번_열린다()
        {
            var g = new WallClockIntervalGate();
            Assert.IsTrue(g.TryConsume(1.0, 0.0));
            Assert.IsTrue(g.TryConsume(1.0, 0.0));
            Assert.IsTrue(g.TryConsume(1.0, -5.0));
        }

        // ------------------------------------------------------------------ [표시모니터] 중복 억제

        [Test]
        public void 같은_근거와_같은_사유는_다시_찍지_않는다()
        {
            const string reason = "폴백 사유";
            Assert.IsFalse(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.StartSlotDefault, null, OverlayMonitorChoiceSource.StartSlotDefault, null));
            Assert.IsFalse(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.UserPreferredMissing, reason, OverlayMonitorChoiceSource.UserPreferredMissing, reason),
                "사유가 붙은 폴백이 0.25초마다 같은 줄을 다시 찍던 결함(D1 후속).");
        }

        [Test]
        public void 근거나_사유가_바뀌면_찍는다()
        {
            Assert.IsTrue(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.StartSlotDefault, null, OverlayMonitorChoiceSource.UserPreferred, null));
            Assert.IsTrue(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.StartSlotDefault, "a", OverlayMonitorChoiceSource.StartSlotDefault, "b"));
            Assert.IsTrue(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.StartSlotDefault, "a", OverlayMonitorChoiceSource.StartSlotDefault, null),
                "사유가 사라지는 순간(복구)도 한 번 찍혀야 한다.");
            Assert.IsTrue(OverlayMonitorChoicePolicy.ShouldLogChoiceChange(
                OverlayMonitorChoiceSource.StartSlotDefault, null, OverlayMonitorChoiceSource.StartSlotDefault, "a"));
        }
    }
}
