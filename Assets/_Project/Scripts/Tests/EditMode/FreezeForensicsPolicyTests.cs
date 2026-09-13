using System;
using System.Globalization;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — 동결 계측의 <b>판정 규칙</b>(순수 함수) 잠금. 숫자는 전부
    /// <see cref="FreezeForensicsPolicy"/>의 상수에서 유도한다(베껴 쓰지 않는다).
    /// </summary>
    public sealed class FreezeForensicsPolicyTests
    {
        private const double Poll = FreezeForensicsPolicy.WatchdogPollSeconds;
        private const double Stall = FreezeForensicsPolicy.StallThresholdSeconds;
        private const double Jump = FreezeForensicsPolicy.WatchdogClockJumpSeconds;

        // ------------------------------------------------------------------ 정지 판정

        [Test]
        public void 정지는_문턱에_닿는_폴링에서_정확히_한번만_보고된다()
        {
            var t = new FreezeWatchdogTracker();
            int started = 0;
            double firstStartAt = double.NaN;
            for (double now = 0.0; now <= Stall * 3; now += Poll)
            {
                FreezeWatchdogVerdict v = t.Observe(7, now, Stall, Jump);
                if (v == FreezeWatchdogVerdict.StallStarted)
                {
                    started++;
                    if (double.IsNaN(firstStartAt)) firstStartAt = now;
                }
            }
            Assert.AreEqual(1, started, "한 정지에 정지 줄은 한 번만 — 폴링마다 쓰면 디스크를 긁는다.");
            Assert.GreaterOrEqual(firstStartAt, Stall, "문턱 전에 보고했다 = 거짓 정지.");
            Assert.Less(firstStartAt, Stall + Poll + 1e-9, "문턱을 한 폴링 넘게 지나서야 보고했다.");
        }

        [Test]
        public void 프레임이_다시_진행하면_재개가_보고되고_지속시간이_실린다()
        {
            var t = new FreezeWatchdogTracker();
            double now = 0.0;
            t.Observe(3, now, Stall, Jump);
            FreezeWatchdogVerdict v = FreezeWatchdogVerdict.None;
            while (v != FreezeWatchdogVerdict.StallStarted) { now += Poll; v = t.Observe(3, now, Stall, Jump); }
            now += Poll * 2;
            Assert.AreEqual(FreezeWatchdogVerdict.None, t.Observe(3, now, Stall, Jump));
            now += Poll;
            Assert.AreEqual(FreezeWatchdogVerdict.StallEnded, t.Observe(4, now, Stall, Jump));
            Assert.AreEqual(now, t.StalledSeconds, 1e-9, "재개 줄에는 마지막 진행 이후 전체 정지 시간이 실려야 한다.");
            Assert.IsFalse(t.StallReported);
        }

        [Test]
        public void 프레임이_꾸준히_진행하면_아무것도_보고하지_않는다()
        {
            var t = new FreezeWatchdogTracker();
            long frame = 1;
            for (double now = 0.0; now < Stall * 10; now += Poll)
            {
                Assert.AreEqual(FreezeWatchdogVerdict.None, t.Observe(frame++, now, Stall, Jump));
            }
        }

        [Test]
        public void 탐침이_프레임을_발행하기_전에는_무장하지_않는다()
        {
            var t = new FreezeWatchdogTracker();
            for (double now = 0.0; now < Stall * 10; now += Poll)
            {
                Assert.AreEqual(FreezeWatchdogVerdict.None, t.Observe(0, now, Stall, Jump),
                    "프레임 0(탐침 없음/기동 중)을 정지로 오인하면 모든 기동이 정지 줄을 남긴다.");
            }
        }

        [Test]
        public void 워치독_자신의_시계가_튀면_거짓_정지를_내지_않는다()
        {
            var t = new FreezeWatchdogTracker();
            t.Observe(9, 0.0, Stall, Jump);
            double resumeAt = Jump + Stall + 1.0;   // 절전으로 워치독 자신도 멈춰 있었다.
            Assert.AreEqual(FreezeWatchdogVerdict.ClockJump, t.Observe(9, resumeAt, Stall, Jump));
            for (double now = resumeAt + Poll; now < resumeAt + Stall - Poll; now += Poll)
            {
                Assert.AreEqual(FreezeWatchdogVerdict.None, t.Observe(9, now, Stall, Jump),
                    "시계가 튄 뒤에는 기준을 새로 잡아야 한다 — 절전 복귀마다 정지 줄이 찍히면 증거가 오염된다.");
            }
        }

        // ------------------------------------------------------------------ 하트비트

        [Test]
        public void 토폴로지_사건이_없으면_하트비트를_절대_쓰지_않는다()
        {
            double last = double.NegativeInfinity;
            for (double now = 0.0; now < FreezeForensicsPolicy.HeartbeatWindowSeconds * 3; now += Poll)
            {
                Assert.IsFalse(FreezeForensicsPolicy.ShouldWriteHeartbeat(now, -1.0, last,
                    FreezeForensicsPolicy.HeartbeatWindowSeconds, FreezeForensicsPolicy.HeartbeatIntervalSeconds),
                    "정상 상주 중 디스크 쓰기 0이 이 장치의 전제다.");
            }
        }

        [Test]
        public void 하트비트는_t0부터_창_안에서만_간격대로_쓴다()
        {
            double window = FreezeForensicsPolicy.HeartbeatWindowSeconds;
            double interval = FreezeForensicsPolicy.HeartbeatIntervalSeconds;
            double t0 = 100.0;
            double last = double.NegativeInfinity;
            int inside = 0, after = 0;
            for (double now = t0 - 5.0; now < t0 + window * 2; now += Poll)
            {
                if (!FreezeForensicsPolicy.ShouldWriteHeartbeat(now, t0, last, window, interval)) continue;
                last = now;
                if (now <= t0 + window) inside++; else after++;
            }
            int expectedMin = (int)Math.Floor(window / Math.Max(interval, Poll));
            int expectedMax = (int)Math.Ceiling(window / interval) + 1;
            Assert.That(inside, Is.InRange(expectedMin, expectedMax), $"창 안 하트비트 {inside}개.");
            Assert.AreEqual(0, after, "창이 끝난 뒤에도 쓰면 24시간 상주 중 디스크를 계속 긁는다.");
        }

        [Test]
        public void 새_사건이_오면_하트비트_창이_다시_열린다()
        {
            double window = FreezeForensicsPolicy.HeartbeatWindowSeconds;
            double interval = FreezeForensicsPolicy.HeartbeatIntervalSeconds;
            double lastOld = 10.0 + window;
            double t0New = 10.0 + window * 3;
            Assert.IsTrue(FreezeForensicsPolicy.ShouldWriteHeartbeat(t0New, t0New, lastOld, window, interval));
            Assert.IsFalse(FreezeForensicsPolicy.ShouldWriteHeartbeat(t0New + interval * 0.5, t0New, t0New, window, interval));
        }

        // ------------------------------------------------------------------ 분류·형식·슬롯

        [Test]
        public void 토폴로지_전이_분류()
        {
            Assert.AreEqual(TopologyForensicsTransition.None, FreezeForensicsPolicy.ClassifyTopologyTransition(false, false, false));
            Assert.AreEqual(TopologyForensicsTransition.ChangeDetected, FreezeForensicsPolicy.ClassifyTopologyTransition(false, true, false));
            Assert.AreEqual(TopologyForensicsTransition.None, FreezeForensicsPolicy.ClassifyTopologyTransition(true, true, false));
            Assert.AreEqual(TopologyForensicsTransition.Settled, FreezeForensicsPolicy.ClassifyTopologyTransition(true, false, true));
            Assert.AreEqual(TopologyForensicsTransition.Reverted, FreezeForensicsPolicy.ClassifyTopologyTransition(true, false, false));
        }

        [Test]
        public void 토폴로지_분류는_실제_감시기의_상태_전이와_맞물린다()
        {
            // 규칙이 감시기와 따로 놀면 원장이 "변화 감지"를 한 번도 못 찍는다 — 진짜 감시기로 확인한다.
            var watcher = new DisplayTopologyWatcher();
            var a = DisplayTopologySignature.Create(2, new UnityEngine.Rect(0, 0, 2560, 1600), new UnityEngine.Vector2(4480, 1600), 1f);
            var b = DisplayTopologySignature.Create(1, new UnityEngine.Rect(0, 0, 1920, 1080), new UnityEngine.Vector2(1920, 1080), 1f);
            watcher.ResetBaseline(a);

            var seen = new System.Collections.Generic.List<TopologyForensicsTransition>();
            float dt = 0.25f;
            for (int i = 0; i < 12; i++)
            {
                bool was = watcher.IsSettling;
                bool fired = watcher.Observe(b, dt);
                TopologyForensicsTransition tr = FreezeForensicsPolicy.ClassifyTopologyTransition(was, watcher.IsSettling, fired);
                if (tr != TopologyForensicsTransition.None) seen.Add(tr);
            }
            CollectionAssert.AreEqual(new[] { TopologyForensicsTransition.ChangeDetected, TopologyForensicsTransition.Settled }, seen);
        }

        [Test]
        public void 투명_재적용은_창_사각형을_바꿀_때만_적는다()
        {
            Assert.IsTrue(FreezeForensicsPolicy.ShouldRecordTransparencyReassign(true));
            Assert.IsFalse(FreezeForensicsPolicy.ShouldRecordTransparencyReassign(false));
        }

        [Test]
        public void 한_줄_형식은_줄바꿈이_없고_시각은_UTC이며_모르는_값은_물음표다()
        {
            var when = new DateTime(2026, 9, 14, 10, 15, 3, 250, DateTimeKind.Utc);
            var r = new FreezeForensicsRecord(FreezeForensicsEvent.WindowResize, when, 12.5, 700, 2,
                1.5f, -3f, 2560f, 1600f, "첫줄\r\n둘째\t셋째");
            string line = FreezeForensicsPolicy.FormatLine(r);
            StringAssert.DoesNotContain("\n", line);
            StringAssert.DoesNotContain("\r", line);
            StringAssert.StartsWith("2026-09-14T10:15:03.250Z | " + nameof(FreezeForensicsEvent.WindowResize), line);
            StringAssert.Contains("rt=12.500", line);
            StringAssert.Contains("frame=700", line);
            StringAssert.Contains("monitors=2", line);
            StringAssert.Contains("rect=(1.5,-3 2560x1600)", line);
            DateTime parsed = DateTime.Parse(line.Substring(0, line.IndexOf(' ')), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            Assert.AreEqual(when, parsed);

            var unknown = new FreezeForensicsRecord(FreezeForensicsEvent.Heartbeat, when, -1.0, -1, -1, null);
            string u = FreezeForensicsPolicy.FormatLine(unknown);
            StringAssert.Contains("rt=?", u);
            StringAssert.Contains("frame=?", u);
            StringAssert.Contains("monitors=?", u);
            StringAssert.DoesNotContain("rect=", u);
        }

        [Test]
        public void 상세는_상한에서_잘린다()
        {
            string longDetail = new string('가', FreezeForensicsPolicy.MaxDetailChars * 3);
            Assert.AreEqual(FreezeForensicsPolicy.MaxDetailChars, FreezeForensicsPolicy.SanitizeDetail(longDetail).Length);
        }

        [Test]
        public void 형식은_현재_문화권의_소수점에_흔들리지_않는다()
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var r = new FreezeForensicsRecord(FreezeForensicsEvent.SetResolution, DateTime.UtcNow, 1.25, 1, 1, 0.5f, 0f, 1f, 1f, "");
                string line = FreezeForensicsPolicy.FormatLine(r);
                StringAssert.Contains("rt=1.250", line);
                StringAssert.Contains("rect=(0.5,0 1x1)", line);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Test]
        public void 슬롯은_없는_것부터_그다음은_가장_오래된_것을_고르고_제외는_건너뛴다()
        {
            var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(1, FreezeForensicsPolicy.ChooseSlot(new[] { true, false, false }, new[] { t, default, default }, null));
            Assert.AreEqual(2, FreezeForensicsPolicy.ChooseSlot(new[] { true, true, true },
                new[] { t.AddHours(2), t.AddHours(3), t.AddHours(1) }, null));
            Assert.AreEqual(0, FreezeForensicsPolicy.ChooseSlot(new[] { true, true }, new[] { t, t }, null), "동률이면 번호가 작은 것.");
            Assert.AreEqual(0, FreezeForensicsPolicy.ChooseSlot(new[] { true, true, true },
                new[] { t.AddHours(2), t.AddHours(3), t.AddHours(1) }, new[] { false, false, true }),
                "제외한 슬롯(2)을 건너뛰면 다음으로 오래된 0번이 골라져야 한다.");
            Assert.AreEqual(-1, FreezeForensicsPolicy.ChooseSlot(new[] { true }, new[] { t }, new[] { true }));
        }

        [Test]
        public void 예산_경계()
        {
            Assert.IsTrue(FreezeForensicsPolicy.FitsBudget(1, 10, 10, 2, 20));
            Assert.IsFalse(FreezeForensicsPolicy.FitsBudget(2, 10, 10, 2, 20), "줄 상한 초과.");
            Assert.IsFalse(FreezeForensicsPolicy.FitsBudget(1, 11, 10, 2, 20), "바이트 상한 초과.");
        }

        [Test]
        public void 에디터에서는_켜지_않고_데스크톱_플레이어에서만_켠다()
        {
            Assert.IsFalse(FreezeForensicsPolicy.ShouldActivate(isEditor: true, isDesktopPlayer: true));
            Assert.IsFalse(FreezeForensicsPolicy.ShouldActivate(isEditor: false, isDesktopPlayer: false));
            Assert.IsTrue(FreezeForensicsPolicy.ShouldActivate(isEditor: false, isDesktopPlayer: true));
        }

        [Test]
        public void 열린_구간_이름은_열거와_특수값을_구분한다()
        {
            Assert.AreEqual("없음", FreezeWatchdog.DescribeOpenSection(-1));
            Assert.AreEqual("깊이초과", FreezeWatchdog.DescribeOpenSection(-2));
            Assert.AreEqual(nameof(StallSection.PlatformEnforcer), FreezeWatchdog.DescribeOpenSection((int)StallSection.PlatformEnforcer));
            StringAssert.StartsWith("알수없음", FreezeWatchdog.DescribeOpenSection((int)StallSection.Count));
        }
    }
}
