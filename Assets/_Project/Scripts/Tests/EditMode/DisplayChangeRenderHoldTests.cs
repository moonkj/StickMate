using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;
using UnityEngine.Rendering;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — 화면 변경 유예(완화 1안) 잠금. 시간은 손으로 흘린다(상태기계는 시계를 인자로 받는다).
    /// 숫자는 전부 <see cref="DisplayChangeHoldPolicy"/> 상수에서 유도한다.
    /// </summary>
    public sealed class DisplayChangeRenderHoldTests
    {
        private const double Quiet = DisplayChangeHoldPolicy.QuietAfterSettleSeconds;
        private const double Early = DisplayChangeHoldPolicy.EarlySignalHoldSeconds;
        private const double Max = DisplayChangeHoldPolicy.MaxHoldSeconds;
        private const double Step = 0.05;

        [SetUp]
        public void SetUp() => DisplayChangeHoldStatus.ResetForTesting();

        [TearDown]
        public void TearDown() => DisplayChangeHoldStatus.ResetForTesting();

        private static DisplayChangeRenderHold Armed()
        {
            var latch = new FullScreenFitLatchSignal();
            var h = new DisplayChangeRenderHold(disabled: false, armingLatch: latch);
            Assert.IsTrue(latch.Evaluate(withinTolerance: true, wroteThisTick: false), "전제: 쓰기 없는 틱의 기하 일치는 확정이어야 한다.");
            Assert.IsTrue(h.IsArmed, "전제: 확정 통지가 상태기계를 무장해야 한다(양성 대조).");
            return h;
        }

        // ------------------------------------------------------------------ 상태기계

        [Test]
        public void 무장_전에는_어떤_신호도_무시한다()
        {
            var h = new DisplayChangeRenderHold(disabled: false, armingLatch: new FullScreenFitLatchSignal());
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.OnLibraryMonitorChanged(0.0),
                "라이브러리는 창을 붙잡는 순간에도 모니터 변경을 통지한다 — 기동 적합을 늦추면 흰 배경 구간이 길어진다.");
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0));
            Assert.IsFalse(h.IsHolding);
        }

        [Test]
        public void 꺼져_있으면_무장해도_시작하지_않는다()
        {
            var latch = new FullScreenFitLatchSignal();
            var h = new DisplayChangeRenderHold(disabled: true, armingLatch: latch);
            Assert.IsTrue(latch.Evaluate(withinTolerance: true, wroteThisTick: false), "전제: 확정 자체는 선다.");
            Assert.IsFalse(h.IsArmed);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.OnLibraryMonitorChanged(0.0));
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0));
            Assert.IsFalse(h.IsHolding);
            Assert.IsFalse(h.ShouldDeferFit);
        }

        [Test]
        public void 정상_경로는_안정_뒤_조용한_구간을_지나_재적합을_끝내고_해제된다()
        {
            var h = Armed();
            Assert.AreEqual(DisplayChangeHoldEvent.Started, h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0));
            Assert.AreEqual(DisplayChangeHoldStartReason.TopologyChangeDetected, h.StartReason);
            Assert.IsTrue(h.ShouldDeferFit);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(0.5, fitPending: false), "안정 신호 전에는 끝나지 않는다.");

            const double settledAt = 1.0;
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.OnTopologyTransition(TopologyForensicsTransition.Settled, settledAt));
            for (double now = settledAt; now < settledAt + Quiet - Step / 2; now += Step)
            {
                Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(now, fitPending: true));
                Assert.IsTrue(h.ShouldDeferFit, "조용한 구간에는 재적합을 보류해야 한다.");
            }

            Assert.AreEqual(DisplayChangeHoldEvent.RefitAllowed, h.Tick(settledAt + Quiet, fitPending: true));
            Assert.IsFalse(h.ShouldDeferFit, "재적합 단계에서는 SetResolution·리사이즈를 허용한다.");
            Assert.IsTrue(h.IsHolding, "재적합 중에도 렌더 억제는 유지한다(소은 #3: 튐을 한 번으로 합친다).");
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(settledAt + Quiet + 0.5, fitPending: true));
            Assert.AreEqual(DisplayChangeHoldEvent.Released, h.Tick(settledAt + Quiet + 1.0, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldReleaseReason.Settled, h.LastReleaseReason);
            Assert.IsFalse(h.IsHolding);
        }

        [Test]
        public void 원래_구성으로_돌아오면_재적합_없이_조용한_구간_끝에_해제된다()
        {
            var h = Armed();
            h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0);
            h.OnTopologyTransition(TopologyForensicsTransition.Reverted, 0.8);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(0.8 + Quiet - Step, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldEvent.Released, h.Tick(0.8 + Quiet, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldReleaseReason.Reverted, h.LastReleaseReason);
        }

        [Test]
        public void 라이브러리_신호만_오면_짧은_유예_뒤_해제된다()
        {
            var h = Armed();
            Assert.AreEqual(DisplayChangeHoldEvent.Started, h.OnLibraryMonitorChanged(0.0));
            Assert.AreEqual(DisplayChangeHoldStartReason.LibraryMonitorChanged, h.StartReason);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(Early - Step, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldEvent.Released, h.Tick(Early, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldReleaseReason.EarlySignalExpired, h.LastReleaseReason);
        }

        [Test]
        public void 라이브러리_신호_뒤_변화가_감지되면_안정을_기다린다()
        {
            var h = Armed();
            h.OnLibraryMonitorChanged(0.0);
            h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.25);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(Early + 1.0, fitPending: false),
                "진짜 변화가 감지됐으면 짧은 유예가 아니라 안정 신호를 기다려야 한다.");
            h.OnTopologyTransition(TopologyForensicsTransition.Settled, Early + 1.0);
            Assert.AreEqual(DisplayChangeHoldEvent.Released, h.Tick(Early + 1.0 + Quiet, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldReleaseReason.Settled, h.LastReleaseReason);
        }

        [Test]
        public void R2c_흔들림이_계속되면_감시기와_무관하게_상한_시각에_해제된다()
        {
            var h = Armed();
            var watcher = new DisplayTopologyWatcher();
            var a = DisplayTopologySignature.Create(2, new Rect(0, 0, 2560, 1600), new Vector2(4480, 1600), 1f);
            var b = DisplayTopologySignature.Create(1, new Rect(0, 0, 1920, 1080), new Vector2(1920, 1080), 1f);
            watcher.ResetBaseline(a);

            double releasedAt = RunUntilReleased(h, (now, i) =>
            {
                bool was = watcher.IsSettling;
                bool fired = watcher.Observe(i % 2 == 0 ? b : a, (float)Step);   // 헐거운 케이블 — 매 표본 흔들린다
                return FreezeForensicsPolicy.ClassifyTopologyTransition(was, watcher.IsSettling, fired);
            }, fitPending: false);

            Assert.AreEqual(DisplayChangeHoldReleaseReason.SafetyCap, h.LastReleaseReason);
            Assert.That(releasedAt, Is.InRange(Max, Max + Step * 1.5));
        }

        [Test]
        public void R2c_관측_실패가_계속되면_상한_시각에_해제된다()
        {
            var h = Armed();
            var watcher = new DisplayTopologyWatcher();
            var a = DisplayTopologySignature.Create(2, new Rect(0, 0, 2560, 1600), new Vector2(4480, 1600), 1f);
            var b = DisplayTopologySignature.Create(1, new Rect(0, 0, 1920, 1080), new Vector2(1920, 1080), 1f);
            watcher.ResetBaseline(a);

            double releasedAt = RunUntilReleased(h, (now, i) =>
            {
                bool was = watcher.IsSettling;
                // 첫 표본만 변화를 보고 그 뒤로는 계속 조회 실패(덮개 닫힘·RDP) — 감시기는 실패를 무시한다.
                bool fired = watcher.Observe(i == 0 ? b : DisplayTopologySignature.Invalid, (float)Step);
                return FreezeForensicsPolicy.ClassifyTopologyTransition(was, watcher.IsSettling, fired);
            }, fitPending: false);

            Assert.AreEqual(DisplayChangeHoldReleaseReason.SafetyCap, h.LastReleaseReason);
            Assert.That(releasedAt, Is.InRange(Max, Max + Step * 1.5));
        }

        [Test]
        public void R2c_재적합_중_사건이면_관측이_멈추고_적합이_안_끝나도_상한_시각에_해제된다()
        {
            var h = Armed();
            Assert.AreEqual(DisplayChangeHoldEvent.Started, h.OnLibraryMonitorChanged(0.0));
            bool sawRefit = false;
            double releasedAt = RunUntilReleased(h, (now, i) => TopologyForensicsTransition.None,   // 재적합 중 관측 스킵
                fitPending: true, onEvent: e => sawRefit |= e == DisplayChangeHoldEvent.RefitAllowed, startedExternally: true);

            Assert.IsTrue(sawRefit, "짧은 유예가 끝나면 재적합 단계로 넘어가야 한다.");
            Assert.AreEqual(DisplayChangeHoldReleaseReason.SafetyCap, h.LastReleaseReason,
                "적합이 끝내 수렴하지 않아도(억제 중 SetResolution이 반영되지 않는 경우 등) 상한이 이긴다.");
            Assert.That(releasedAt, Is.InRange(Max, Max + Step * 1.5));
        }

        [Test]
        public void 새_변화가_와도_상한은_최초_시작_시각_기준이다()
        {
            var h = Armed();
            h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0);
            h.OnTopologyTransition(TopologyForensicsTransition.Settled, 1.0);
            h.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 3.0);
            h.OnTopologyTransition(TopologyForensicsTransition.Settled, Max - 1.0);
            Assert.AreEqual(DisplayChangeHoldEvent.None, h.Tick(Max - Step, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldEvent.Released, h.Tick(Max, fitPending: false));
            Assert.AreEqual(DisplayChangeHoldReleaseReason.SafetyCap, h.LastReleaseReason);
        }

        [Test]
        public void 에피소드_번호는_시작마다_늘어난다()
        {
            var h = Armed();
            h.OnLibraryMonitorChanged(0.0);
            h.Tick(Early, fitPending: false);
            h.OnLibraryMonitorChanged(10.0);
            Assert.AreEqual(2, h.EpisodeNumber);
        }

        private static double RunUntilReleased(DisplayChangeRenderHold h,
            System.Func<double, int, TopologyForensicsTransition> sample, bool fitPending,
            System.Action<DisplayChangeHoldEvent> onEvent = null, bool startedExternally = false)
        {
            double now = 0.0;
            for (int i = 0; now <= Max * 3; i++, now += Step)
            {
                TopologyForensicsTransition t = sample(now, i);
                DisplayChangeHoldEvent e1 = h.OnTopologyTransition(t, now);
                if (i == 0 && !startedExternally)
                    Assert.AreEqual(DisplayChangeHoldEvent.Started, e1, "첫 표본이 유예를 시작해야 한다.");
                onEvent?.Invoke(e1);
                DisplayChangeHoldEvent e2 = h.Tick(now, fitPending);
                onEvent?.Invoke(e2);
                if (e2 == DisplayChangeHoldEvent.Released) return now;
            }
            Assert.Fail("상한의 세 배가 지나도 해제되지 않았다 — 유예가 영원히 걸릴 수 있다(R-2a 위반).");
            return double.NaN;
        }

        // ------------------------------------------------------------------ 규칙

        [Test]
        public void 렌더_간격은_유예_중_큰_쪽이고_끝나면_등급_값_그대로다()
        {
            for (int tier = 1; tier <= DisplayChangeHoldPolicy.SuppressedRenderFrameInterval + 10; tier++)
            {
                Assert.AreEqual(Mathf.Max(tier, DisplayChangeHoldPolicy.SuppressedRenderFrameInterval),
                    DisplayChangeHoldPolicy.ResolveRenderFrameInterval(tier, holding: true), $"tier {tier}");
                Assert.AreEqual(tier, DisplayChangeHoldPolicy.ResolveRenderFrameInterval(tier, holding: false), $"tier {tier}");
            }
        }

        [Test]
        public void 끄기_스위치는_비었거나_0이면_켜짐이다()
        {
            StringAssert.StartsWith("STICKMATE_", DisplayChangeHoldPolicy.DisableEnvironmentVariable);
            Assert.IsFalse(DisplayChangeHoldPolicy.IsDisabledByEnvironmentValue(null));
            Assert.IsFalse(DisplayChangeHoldPolicy.IsDisabledByEnvironmentValue(""));
            Assert.IsFalse(DisplayChangeHoldPolicy.IsDisabledByEnvironmentValue("0"));
            Assert.IsTrue(DisplayChangeHoldPolicy.IsDisabledByEnvironmentValue("1"));
            Assert.IsTrue(DisplayChangeHoldPolicy.IsDisabledByEnvironmentValue("true"));
        }

        // ------------------------------------------------------------------ 구동기 (재개 순서 계약)

        private sealed class FakeHooks
        {
            public readonly List<string> Calls = new List<string>();
            public bool FitPending;
            public int Interval = 1;

            public DisplayChangeHoldDriver.Hooks Build() => new DisplayChangeHoldDriver.Hooks
            {
                PlatformTag = "Test",
                IsFitPending = () => FitPending,
                ForceRefreshOsMonitors = () => Calls.Add("refresh(active=" + DisplayChangeHoldStatus.IsActive + ")"),
                SetRenderHold = on =>
                {
                    Calls.Add("render(" + on + ",active=" + DisplayChangeHoldStatus.IsActive + ")");
                    Interval = on ? DisplayChangeHoldPolicy.SuppressedRenderFrameInterval : 1;
                },
                ActualRenderedFrames = () => 0,
                RenderedFrameCount = () => 0,
                EffectiveRenderFrameInterval = () => Interval,
                MonitorCount = () => 1,
            };
        }

        [Test]
        public void 해제는_OS_모니터_목록_강제_갱신이_해제_게시보다_먼저다()
        {
            var fake = new FakeHooks();
            var latch = new FullScreenFitLatchSignal();
            var driver = new DisplayChangeHoldDriver(fake.Build(), latch, disabled: false);
            Assert.IsTrue(latch.Evaluate(withinTolerance: true, wroteThisTick: false), "전제: 첫 적합 확정.");

            driver.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.0, 100);
            Assert.IsTrue(DisplayChangeHoldStatus.IsActive);
            Assert.AreEqual(1, DisplayChangeHoldStatus.EpisodeNumber);
            Assert.AreEqual(100, DisplayChangeHoldStatus.StartedAtFrame);
            CollectionAssert.AreEqual(new[] { "render(True,active=False)" }, fake.Calls);

            driver.OnTopologyTransition(TopologyForensicsTransition.Settled, 1.0, 160);
            fake.Calls.Clear();
            driver.Tick(1.0 + Quiet, 400);

            CollectionAssert.AreEqual(new[] { "refresh(active=True)", "render(False,active=True)" }, fake.Calls,
                "해제 신호(IsActive=false)를 받은 쪽이 발판을 다시 읽을 때 목록은 이미 최신이어야 한다 — 갱신이 먼저다.");
            Assert.IsFalse(DisplayChangeHoldStatus.IsActive);
            Assert.AreEqual(400, DisplayChangeHoldStatus.ReleasedAtFrame);
            Assert.AreEqual(DisplayChangeHoldReleaseReason.Settled, DisplayChangeHoldStatus.LastReleaseReason);
        }

        [Test]
        public void 끄기_스위치가_켜지면_구동기는_어떤_훅도_부르지_않는다()
        {
            var fake = new FakeHooks { FitPending = true };
            var latch = new FullScreenFitLatchSignal();
            var driver = new DisplayChangeHoldDriver(fake.Build(), latch, disabled: true);
            latch.Evaluate(withinTolerance: true, wroteThisTick: false);
            driver.OnLibraryMonitorChanged(0.0, 1);
            driver.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, 0.1, 2);
            for (double now = 0.0; now < Max * 2; now += 0.5) driver.Tick(now, 3);
            driver.NotifyFitDeferred(4);
            CollectionAssert.IsEmpty(fake.Calls, "끄면 이전 동작과 한 글자도 다르지 않아야 한다 — 훅 호출 0.");
            Assert.IsFalse(driver.ShouldDeferFit);
            Assert.IsFalse(DisplayChangeHoldStatus.IsActive);
            Assert.AreEqual(0, DisplayChangeHoldStatus.EpisodeNumber);
        }

        // ------------------------------------------------------------------ 무장 자리 잠금 (verify-change 2차 X2c)

        [Test]
        public void X2c_적합_확정_통지_없이는_아무리_많은_틱과_신호에도_무장되지_않는다()
        {
            var fake = new FakeHooks();
            var latch = new FullScreenFitLatchSignal();
            var driver = new DisplayChangeHoldDriver(fake.Build(), latch, disabled: false);

            // 기동 구간 흉내: 매 프레임 유예 틱 + 라이브러리의 부착 시점 모니터 통지 + 확정되지 못한 판정(쓰기 있던 틱 / 기하 불일치).
            const int bootFrames = 600;
            for (int frame = 1; frame <= bootFrames; frame++)
            {
                double now = frame * Step;
                driver.Tick(now, frame);
                Assert.IsFalse(latch.Evaluate(withinTolerance: true, wroteThisTick: true), "쓰기가 있던 틱은 확정이 아니다.");
                Assert.IsFalse(latch.Evaluate(withinTolerance: false, wroteThisTick: false), "기하 불일치는 확정이 아니다.");
                driver.OnLibraryMonitorChanged(now, frame);
                driver.OnTopologyTransition(TopologyForensicsTransition.ChangeDetected, now, frame);
            }
            Assert.IsFalse(driver.State.IsArmed,
                "적합 확정 전에 무장됐다 — 기동 중 라이브러리 통지로 유예가 걸려 기동 흰 배경 구간이 길어진다(X2c).");
            Assert.IsFalse(driver.IsHolding);
            CollectionAssert.IsEmpty(fake.Calls, "무장 전 신호가 렌더 입력·목록 갱신 훅을 불렀다.");
            Assert.AreEqual(0, DisplayChangeHoldStatus.EpisodeNumber);
            Assert.IsFalse(latch.HasLatchedOnce);

            // 양성 대조 — 같은 구동기가 확정 한 번 뒤에는 같은 신호로 시작한다(위 부재 단언이 공허하지 않다).
            Assert.IsTrue(latch.Evaluate(withinTolerance: true, wroteThisTick: false));
            Assert.IsTrue(driver.State.IsArmed, "확정 통지가 무장하지 못했다.");
            driver.OnLibraryMonitorChanged((bootFrames + 1) * Step, bootFrames + 1);
            Assert.IsTrue(driver.IsHolding);
            Assert.AreEqual(DisplayChangeHoldStartReason.LibraryMonitorChanged, DisplayChangeHoldStatus.StartReason);
        }

        [Test]
        public void X2c_구동기가_확정보다_늦게_만들어져도_이미_선_확정으로_무장되고_확정이_없으면_무장되지_않는다()
        {
            var latched = new FullScreenFitLatchSignal();
            Assert.IsTrue(latched.Evaluate(withinTolerance: true, wroteThisTick: false));
            var late = new DisplayChangeHoldDriver(new FakeHooks().Build(), latched, disabled: false);
            Assert.IsTrue(late.State.IsArmed, "Enforcer는 구동기를 지연 생성한다 — 이미 선 확정을 놓치면 영원히 무장되지 않는다.");

            var none = new DisplayChangeHoldDriver(new FakeHooks().Build(), armingLatch: null, disabled: false);
            none.OnLibraryMonitorChanged(0.0, 1);
            Assert.IsFalse(none.State.IsArmed, "확정 원천이 없으면 무장할 길이 없어야 한다.");
            Assert.IsFalse(none.IsHolding);
        }

        [Test]
        public void X2c_확정_신호는_ShouldLatchFitApplied_규칙_그대로이고_설_때마다_통지한다()
        {
            var latch = new FullScreenFitLatchSignal();
            int notified = 0;
            latch.Latched += () => notified++;
            foreach (bool within in new[] { false, true })
            foreach (bool wrote in new[] { false, true })
            {
                int before = notified;
                bool expected = OverlayBoundsFitPolicy.ShouldLatchFitApplied(within, wrote);
                Assert.AreEqual(expected, latch.Evaluate(within, wrote), $"within={within}, wrote={wrote}");
                Assert.AreEqual(expected ? before + 1 : before, notified, "통지는 확정이 설 때만 올라야 한다.");
            }
            Assert.AreEqual(1, latch.LatchCount);

            latch.Latched += () => throw new System.InvalidOperationException("구독자 실패");
            Assert.IsTrue(latch.Evaluate(withinTolerance: true, wroteThisTick: false), "구독자가 던져도 확정 판정은 그대로여야 한다.");
        }

        [Test]
        public void X2c_구동기와_상태기계에는_무장_메서드가_없다()
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (System.Type type in new[] { typeof(DisplayChangeHoldDriver), typeof(DisplayChangeRenderHold) })
            {
                var nonPrivateArmers = new List<string>();
                int armedProperty = 0;
                foreach (System.Reflection.MethodInfo m in type.GetMethods(all))
                {
                    if (m.Name == "get_" + nameof(DisplayChangeRenderHold.IsArmed)) { armedProperty++; continue; }
                    if (m.IsPrivate) continue;
                    if (m.Name.IndexOf("Arm", System.StringComparison.Ordinal) >= 0) nonPrivateArmers.Add(m.Name);
                }
                if (type == typeof(DisplayChangeRenderHold))
                    Assert.AreEqual(1, armedProperty, "양성 대조: 리플렉션이 이 타입의 멤버를 실제로 보고 있어야 한다.");
                CollectionAssert.IsEmpty(nonPrivateArmers,
                    $"{type.Name}에 외부에서 부를 수 있는 무장 메서드가 생겼다 — 그 호출은 어디로든 옮길 수 있고, 옮겨도 초록이던 구멍(X2c)이 다시 열린다. " +
                    $"무장은 {nameof(FullScreenFitLatchSignal)} 통지로만.");
            }
        }

        // ------------------------------------------------------------------ FramePacing 우선순위 (R-2b)

        [Test]
        public void 유예_중_등급이_바뀌어도_해제하면_새_등급의_간격으로_정확히_돌아간다()
        {
            int saved = OnDemandRendering.renderFrameInterval;
            try
            {
                FramePacing.ResetForTests();
                int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
                FramePacing.ApplyPlanForTesting(new FramePacingPlan(FramePacingTier.Calm, vsync, target, 2));
                Assert.AreEqual(2, OnDemandRendering.renderFrameInterval, "등급 계획이 간격을 쓰지 않았다(전제 실패).");

                FramePacing.SetDisplayChangeHold(true);
                Assert.AreEqual(DisplayChangeHoldPolicy.SuppressedRenderFrameInterval, OnDemandRendering.renderFrameInterval);

                FramePacing.ApplyPlanForTesting(new FramePacingPlan(FramePacingTier.Active, vsync, target, 1));
                Assert.AreEqual(DisplayChangeHoldPolicy.SuppressedRenderFrameInterval, OnDemandRendering.renderFrameInterval,
                    "유예 중 등급 전환이 억제를 풀어 버렸다 — 우선순위가 코드에 없다.");

                FramePacing.SetDisplayChangeHold(false);
                Assert.AreEqual(1, OnDemandRendering.renderFrameInterval,
                    "해제 뒤 유예 중에 바뀐 새 등급 값(1)으로 돌아가야 한다 — 억제값이 다음 등급 전환까지 남으면 안 된다.");
            }
            finally
            {
                FramePacing.ResetForTests();
                OnDemandRendering.renderFrameInterval = saved;
            }
        }

        [Test]
        public void 등급_계획이_없을_때는_유예_전_간격으로_돌아간다()
        {
            int saved = OnDemandRendering.renderFrameInterval;
            try
            {
                FramePacing.ResetForTests();
                OnDemandRendering.renderFrameInterval = 3;
                Assert.AreEqual(3, OnDemandRendering.renderFrameInterval, "EditMode에서 렌더 간격을 쓸 수 없다(전제 실패).");
                FramePacing.SetDisplayChangeHold(true);
                Assert.AreEqual(DisplayChangeHoldPolicy.SuppressedRenderFrameInterval, OnDemandRendering.renderFrameInterval);
                FramePacing.SetDisplayChangeHold(false);
                Assert.AreEqual(3, OnDemandRendering.renderFrameInterval);
            }
            finally
            {
                FramePacing.ResetForTests();
                OnDemandRendering.renderFrameInterval = saved;
            }
        }
    }
}
