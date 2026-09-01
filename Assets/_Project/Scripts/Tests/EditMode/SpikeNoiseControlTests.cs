using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// **숨어 있는 동안 로그가 오히려 늘던 문제**(2026-09-02 검증 R2-1/R2-2/R2-3/R2-5)의 회귀 잠금.
    ///
    /// ============================================================================
    /// 실측이 말한 것 — 목표의 정반대였다
    /// ============================================================================
    /// <code>
    ///   조용한 기준선          93.2 B/s   1.0x
    ///   순수 Suspended 85초   207.1 B/s   2.2x
    ///   6회 급속 왕복 76초    465.7 B/s   5.0x
    /// </code>
    /// 그리고 452초 가려짐 구간에서 <c>[스톨귀인]</c> 137.5 B/s + <c>[프레임스파이크]</c> 100.1 B/s
    /// = <b>전체 바이트의 70.7%</b>. 게임을 몇 시간 켜 두는 시간이 곧 가장 조용해야 할 시간이므로
    /// 절대 불변 원칙 2("비침해")와 정면으로 충돌한다.
    ///
    /// ============================================================================
    /// ★ R2-6 — 원인은 vSync 계획이 아니라 <b>OS의 가려진 앱 조이기</b>였다(452초 실측)
    /// ============================================================================
    /// <code>
    ///   계획: vSyncCount=2 @120Hz -> 기대 16.7ms
    ///   실측: p50 105ms / p95 197ms / p99 215ms  (13개 창, 등급은 내내 Active/Calm/Still)
    /// </code>
    /// <b>Suspended가 아니었는데도</b> 9.5fps로 떨어졌다 = 가설 A(OS가 계획보다 세게 조인다) 확정.
    /// 분포가 p50≈평균으로 촘촘한 <b>규칙적 박자</b>라 가설 B("불규칙 스톨만 남는다")는 반증됐다.
    /// 그래서 문턱은 계획값이 아니라 <b>관측 중앙값</b>까지 봐야 한다 — 105ms를 예측할 수 있는 값은
    /// 계획 어디에도 없다.
    /// </summary>
    public class SpikeNoiseControlTests
    {
        // ====================================================================================
        // A. 관측 기반 문턱 (R2-1 / R2-6)
        // ====================================================================================

        [Test]
        public void 관측_중앙값이_계획보다_크면_문턱이_따라_올라간다()
        {
            // 실측 그대로: 계획 16.7ms인데 루프가 105ms로 돌던 구간.
            float t = StallAttribution.SpikeThresholdMs(16.67f, 105f);
            Assert.AreEqual(105f * StallAttribution.SpikeRelativeFactor, t, 0.01f);

            Assert.Less(111f, t, "그 구간의 111ms 프레임은 히치가 아니라 그 구간의 **평상 박자**다.");
            Assert.Less(215f, t, "p99(215ms)조차 문턱 아래여야 로그가 조용해진다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 평상시에는 문턱이 하나도 안 움직여야 한다(눈을 감지 않는다).</summary>
        [Test]
        public void 평상시에는_문턱이_그대로_100ms다()
        {
            float t = StallAttribution.SpikeThresholdMs(16.67f, 16.67f);
            Assert.AreEqual(StallAttribution.SpikeAbsoluteMs, t, 0.01f,
                "평상시 문턱이 바뀌면 이 라운드는 '계기를 고친' 것이 아니라 '눈을 감은' 것이다.");

            Assert.Greater(400f, t, "400ms짜리 진짜 히치는 변함없이 잡혀야 한다.");
            Assert.Greater(150f, t, "실기 p99였던 150ms도 계속 잡혀야 한다.");
        }

        [Test]
        public void 관측값이_없거나_작으면_계획값을_쓴다()
        {
            Assert.AreEqual(StallAttribution.SpikeAbsoluteMs,
                StallAttribution.SpikeThresholdMs(16.67f, 0f), 0.01f);
            Assert.AreEqual(StallAttribution.SpikeAbsoluteMs,
                StallAttribution.SpikeThresholdMs(16.67f, -5f), 0.01f);

            // 계획이 큰 쪽(DisplayOff 4fps)에서는 계획이 이긴다.
            Assert.AreEqual(250f * StallAttribution.SpikeRelativeFactor,
                StallAttribution.SpikeThresholdMs(250f, 10f), 0.01f);
        }

        [Test]
        public void 문턱은_절대_100ms_아래로_내려가지_않는다()
        {
            for (float median = 0f; median < 60f; median += 3f)
            {
                Assert.GreaterOrEqual(StallAttribution.SpikeThresholdMs(8f, median),
                    StallAttribution.SpikeAbsoluteMs);
            }
        }

        // ====================================================================================
        // B. 전환 오염 소급 재분류 (R2-2)
        // ====================================================================================

        [Test]
        public void 정상_구간의_등급_분리는_그대로_동작한다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);                       // 전환 유예를 벗어난다

            for (int i = 0; i < 5; i++) { ledger.Count(FramePacingTier.Active); Advance(ref ledger, 1.5f); }
            for (int i = 0; i < 7; i++) { ledger.Count(FramePacingTier.Suspended); Advance(ref ledger, 1.5f); }

            Assert.AreEqual(5, ledger.Actionable);
            Assert.AreEqual(7, ledger.Throttled);
            Assert.AreEqual(0, ledger.Transitional);
            Assert.AreEqual(12, ledger.Total);
        }

        /// <summary>
        /// ★ 이 라운드의 핵심. Space 슬라이드 스톨은 <b>등급이 바뀌기 전에</b> 일어난다
        /// (전체화면 폴링 1.5초 + 판정 디바운스 1.0초 뒤에야 등급이 따라온다).
        /// 앞만 보는 유예로는 절반도 못 잡으므로 <b>소급</b>이 필요하다.
        /// </summary>
        [Test]
        public void 전환_직전의_스톨이_소급으로_전환칸에_들어간다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);

            // Space 슬라이드 구간 — 등급은 아직 Active다.
            for (int i = 0; i < 13; i++) { ledger.Count(FramePacingTier.Active); Advance(ref ledger, 0.15f); }
            Assert.AreEqual(13, ledger.Actionable, "아직은 실사용으로 잡혀 있는 것이 맞다.");

            // 그리고 나서 등급 전환이 도착한다.
            ledger.Tick(0.016f, tierTransitioned: true);

            Assert.AreEqual(0, ledger.Actionable,
                "왕복 1회당 실사용이 +13.5씩 오염되던 것이 이 소급으로 사라져야 한다.");
            Assert.AreEqual(13, ledger.Transitional);
            Assert.AreEqual(13, ledger.Total, "총합은 보존된다 — 재분류지 삭제가 아니다.");
        }

        [Test]
        public void 전환_직후의_스톨도_전환칸이다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);
            ledger.Tick(0.016f, tierTransitioned: true);

            ledger.Count(FramePacingTier.Active);
            Advance(ref ledger, 1f);
            ledger.Count(FramePacingTier.Active);

            Assert.AreEqual(2, ledger.Transitional);
            Assert.AreEqual(0, ledger.Actionable);
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 소급이 <b>과거를 통째로 지우지</b> 않는다. 전환과 무관하게 한참 전에
        /// 난 실사용 스파이크까지 전환칸으로 옮기면, 진짜 히치가 OS 애니메이션 뒤에 숨는다.
        /// </summary>
        [Test]
        public void 오래된_실사용_스파이크는_소급되지_않는다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);

            ledger.Count(FramePacingTier.Active);          // 아주 오래된 진짜 히치
            Advance(ref ledger, 30f);                      // 유예/소급 슬롯을 한참 넘긴다
            ledger.Count(FramePacingTier.Active);          // 전환 직전의 스톨
            ledger.Tick(0.016f, tierTransitioned: true);

            Assert.AreEqual(1, ledger.Actionable, "30초 전의 히치까지 지워지면 진짜 문제가 숨는다.");
            Assert.AreEqual(1, ledger.Transitional);
        }

        /// <summary>
        /// ★★ 2026-09-02 신빌드 실측 회귀 잠금 — <b>유예가 진짜 히치를 삼키면 안 된다.</b>
        ///
        /// <para>처음 구현은 <b>모든</b> 등급 전환에 3초 유예를 걸었다. 그런데 Active↔Calm↔Still
        /// 미세 전환은 캐릭터가 서고 걷기만 해도 수 초마다 일어나서, 유예가 타임라인 대부분을 덮었다.
        /// 실측: <c>SIGSTOP</c>으로 유도한 <b>192ms(Active) / 434ms(Calm) 히치가 둘 다 전환 칸</b>으로
        /// 갔고, 6회 왕복 188초 구간의 <c>실사용 +0</c>은 성공이 아니라 <b>실명</b>이었다.</para>
        /// </summary>
        [Test]
        public void 미세_등급전환은_유예_사유가_아니다()
        {
            Assert.IsFalse(SpikeTierLedger.CrossesThrottleBoundary(FramePacingTier.Active, FramePacingTier.Calm));
            Assert.IsFalse(SpikeTierLedger.CrossesThrottleBoundary(FramePacingTier.Calm, FramePacingTier.Still));
            Assert.IsFalse(SpikeTierLedger.CrossesThrottleBoundary(FramePacingTier.Away, FramePacingTier.Suspended));

            // Space 슬라이드처럼 실제로 100~300ms를 먹는 전환만 경계를 넘는다.
            Assert.IsTrue(SpikeTierLedger.CrossesThrottleBoundary(FramePacingTier.Still, FramePacingTier.Suspended));
            Assert.IsTrue(SpikeTierLedger.CrossesThrottleBoundary(FramePacingTier.Suspended, FramePacingTier.Active));
        }

        /// <summary>실기 재현: 미세 전환이 계속 일어나는 와중의 진짜 히치는 <b>실사용</b>에 남아야 한다.</summary>
        [Test]
        public void 미세전환_와중의_진짜_히치는_실사용에_남는다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);

            // Active <-> Calm <-> Still 이 2초마다 오가는, 캐릭터가 서고 걷는 평범한 구간.
            var churn = new[] { FramePacingTier.Active, FramePacingTier.Calm, FramePacingTier.Still };
            var prev = FramePacingTier.Active;
            for (int i = 0; i < 12; i++)
            {
                var next = churn[i % churn.Length];
                ledger.Tick(0.016f, SpikeTierLedger.CrossesThrottleBoundary(prev, next));
                prev = next;
                Advance(ref ledger, 2f);
                if (i == 6) ledger.Count(next);          // 그 한복판에서 난 진짜 히치
            }

            Assert.AreEqual(1, ledger.Actionable,
                "미세 전환 유예가 진짜 히치를 전환 칸으로 삼켰다 — '실사용 0'은 성공이 아니라 실명이다.");
            Assert.AreEqual(0, ledger.Transitional);
        }

        [Test]
        public void 절감등급_스파이크는_소급_대상이_아니다()
        {
            var ledger = new SpikeTierLedger();
            Advance(ref ledger, 10f);
            for (int i = 0; i < 9; i++) { ledger.Count(FramePacingTier.Suspended); Advance(ref ledger, 0.1f); }
            ledger.Tick(0.016f, tierTransitioned: true);

            Assert.AreEqual(9, ledger.Throttled);
            Assert.AreEqual(0, ledger.Transitional);
        }

        [Test]
        public void 어떤_순서로도_세_칸의_합이_총합이다()
        {
            var ledger = new SpikeTierLedger();
            var rng = new System.Random(20260902);
            var tiers = (FramePacingTier[])System.Enum.GetValues(typeof(FramePacingTier));
            int counted = 0;

            for (int i = 0; i < 4000; i++)
            {
                ledger.Tick((float)rng.NextDouble() * 0.4f, rng.Next(50) == 0);
                if (rng.Next(3) != 0) continue;
                ledger.Count(tiers[rng.Next(tiers.Length)]);
                counted++;
            }

            Assert.AreEqual(counted, ledger.Total, "재분류 과정에서 건수가 새거나 늘었다.");
            Assert.GreaterOrEqual(ledger.Actionable, 0, "소급이 실사용 칸을 음수로 만들었다.");
        }

        // ====================================================================================
        // C. 적응형 백오프 (R2-1)
        // ====================================================================================

        private const int Throttled = (int)SpikeTierLedger.SpikeClass.Throttled;
        private const int Transitional = (int)SpikeTierLedger.SpikeClass.Transitional;
        private const int Actionable = (int)SpikeTierLedger.SpikeClass.Actionable;

        [Test]
        public void 같은_분류가_이어지면_간격이_두배씩_벌어진다()
        {
            var b = new SpikeLogBackoff();

            Assert.IsTrue(b.ShouldLog(Throttled, urgent: false), "첫 줄은 즉시 나가야 한다.");
            Assert.AreEqual(SpikeLogBackoff.MinSeconds, b.CurrentIntervalSeconds, 0.01f);

            float expected = SpikeLogBackoff.MinSeconds;
            for (int i = 0; i < 6; i++)
            {
                b.Tick(expected * 0.5f);
                Assert.IsFalse(b.ShouldLog(Throttled, false));
                b.Tick(expected);
                Assert.IsTrue(b.ShouldLog(Throttled, false));

                expected = Mathf.Min(SpikeLogBackoff.MaxSeconds, expected * 2f);
                Assert.AreEqual(expected, b.CurrentIntervalSeconds, 0.01f);
            }
            Assert.AreEqual(SpikeLogBackoff.MaxSeconds, b.CurrentIntervalSeconds, 0.01f,
                "상한이 없으면 24시간 상주 앱이 '아직 살아 있다'는 신호까지 잃는다.");
        }

        /// <summary>★ 사용자가 느끼는 히치는 절대 늦게 알려지지 않는다 — 이게 없으면 백오프는 눈가리개다.</summary>
        [Test]
        public void 실사용_히치는_억제_중에도_즉시_찍는다()
        {
            var b = new SpikeLogBackoff();
            b.ShouldLog(Throttled, false);
            b.Tick(1f);
            Assert.IsFalse(b.ShouldLog(Throttled, false), "같은 분류는 억제된다.");

            Assert.IsTrue(b.ShouldLog(Actionable, urgent: true),
                "실사용 히치가 억제되면 사용자가 느끼는 렉이 최대 60초 늦게 보고된다.");
            Assert.AreEqual(SpikeLogBackoff.MinSeconds, b.CurrentIntervalSeconds, 0.01f);
        }

        /// <summary>
        /// ★★ 2026-09-02 신빌드 실측 회귀 잠금. "분류가 달라지기만 하면 뚫는다"로 두면
        /// 전체화면을 빠르게 들락거릴 때 전환↔절감이 번갈아 들어와 <b>억제가 통째로 무력화</b>된다
        /// (실측: 6회 왕복 188초에서 신빌드 26줄 vs 구빌드 18줄 — 목표의 정반대였다).
        /// </summary>
        [Test]
        public void 전환과_절감이_번갈아_와도_억제가_풀리지_않는다()
        {
            var b = new SpikeLogBackoff();
            b.ShouldLog(Transitional, false);
            b.Tick(1f);

            for (int i = 0; i < 20; i++)
            {
                Assert.IsFalse(b.ShouldLog(i % 2 == 0 ? Throttled : Transitional, urgent: false),
                    "전환/절감이 번갈아 왔다고 억제가 풀리면 왕복 구간에서 로그가 오히려 늘어난다.");
                b.Tick(0.05f);
            }
        }

        [Test]
        public void 조용해지면_간격이_최소로_돌아온다()
        {
            var b = new SpikeLogBackoff();
            b.ShouldLog(Throttled, false);
            b.Tick(SpikeLogBackoff.MinSeconds);
            b.ShouldLog(Throttled, false);           // 간격 10초로 확대
            Assert.AreEqual(10f, b.CurrentIntervalSeconds, 0.01f);

            b.Tick(10f);                             // 그 10초 동안 아무 후보도 없었다
            Assert.AreEqual(SpikeLogBackoff.MinSeconds, b.CurrentIntervalSeconds, 0.01f);
        }

        /// <summary>실측 452초 구간에 규칙을 그대로 적용해 <b>줄 수가 실제로 줄어드는지</b> 확인한다.</summary>
        [Test]
        public void 실측_452초_구간에서_줄_수가_유의미하게_줄어든다()
        {
            var b = new SpikeLogBackoff();
            const float dt = 0.105f;                 // 실측 p50 105ms
            int lines = 0;

            for (float t = 0f; t < 452f; t += dt)
            {
                b.Tick(dt);
                if (b.ShouldLog(Throttled, false)) lines++;   // 매 프레임 후보라는 최악 가정
            }

            Assert.Less(lines, 20, $"백오프 후에도 {lines}줄이면 감량이 아니다(실측 전 88줄).");
            Assert.Greater(lines, 5, "너무 조용하면 상주 신호를 잃는다.");
        }

        // ====================================================================================
        // C-2. 발생률 창이 사건을 따라간다 (R2-5)
        // ====================================================================================

        /// <summary>
        /// ★ 텀블링 창의 두 병증을 동시에 잡는다:
        /// (1) 값이 <b>단조 상승만</b> 한다, (2) 창이 굴리는 순간 <b>2.4배 점프</b>한다.
        /// 실제 발생률이 일정한데 계기가 그렇게 움직이면 사람은 "갑자기 나빠졌다"로 읽는다.
        /// </summary>
        [Test]
        public void 일정한_발생률에서_값이_점프하지_않고_수렴한다()
        {
            var w = new SpikeRateWindow();
            const float dt = 0.1f;
            const int perTick = 1;                       // 0.1초마다 1회 = 600회/분

            float prev = -1f, maxJumpRatio = 1f;
            for (float t = 0f; t < 900f; t += dt)        // 창 길이(300초)의 3배를 돌린다
            {
                w.Tick(dt);
                for (int i = 0; i < perTick; i++) w.Count1();

                if (t > SpikeRateWindow.MinSpanSeconds && prev > 0f)
                {
                    float ratio = Mathf.Max(w.PerMinute / prev, prev / w.PerMinute);
                    if (ratio > maxJumpRatio) maxJumpRatio = ratio;
                }
                prev = w.PerMinute;
            }

            Assert.AreEqual(600f, w.PerMinute, 30f, "정상 상태에서 실제 발생률로 수렴해야 한다.");
            Assert.Less(maxJumpRatio, 1.2f,
                $"한 줄 만에 {maxJumpRatio:F2}배 튀었다 — 실측에서 2.4배 점프가 오독을 만들었다.");
        }

        /// <summary>★ 텀블링과 결정적으로 다른 점 — 사건이 멎으면 값이 <b>내려간다</b>.</summary>
        [Test]
        public void 사건이_멎으면_발생률이_내려간다()
        {
            var w = new SpikeRateWindow();
            for (float t = 0f; t < 300f; t += 0.1f) { w.Tick(0.1f); w.Count1(); }
            float busy = w.PerMinute;
            Assert.Greater(busy, 100f);

            for (float t = 0f; t < 150f; t += 0.1f) w.Tick(0.1f);   // 절반 창 동안 조용
            Assert.Less(w.PerMinute, busy * 0.6f,
                "조용해졌는데 값이 안 내려가면 '아직도 나쁘다'로 오독된다(텀블링의 병증).");
        }

        [Test]
        public void 관측이_짧으면_짧다고_말한다()
        {
            var w = new SpikeRateWindow();
            w.Tick(1f); w.Count1();
            Assert.IsTrue(w.SpanTooShort, "1초 관측으로 분당 60회를 단언하면 그게 허수다.");

            for (float t = 0f; t < SpikeRateWindow.MinSpanSeconds + 5f; t += 0.5f) w.Tick(0.5f);
            Assert.IsFalse(w.SpanTooShort);
        }

        /// <summary>아주 긴 공백(디스플레이 절전 복귀 등)에서 값이 폭주하지 않는다.</summary>
        [Test]
        public void 긴_공백_뒤에는_창을_비운다()
        {
            var w = new SpikeRateWindow();
            for (int i = 0; i < 50; i++) { w.Tick(0.1f); w.Count1(); }
            w.Tick(SpikeRateWindow.WindowSeconds + 10f);

            Assert.AreEqual(0, w.Count);
            Assert.AreEqual(0f, w.PerMinute, 0.001f);
        }

        // ====================================================================================
        // D. 배선 잠금
        // ====================================================================================

        private static string ReadScript(params string[] parts)
            => File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts",
                Path.Combine(parts)));

        /// <summary>
        /// ★ R2-3 — 예전에는 <c>StallAttribution.cs</c>에 <c>FramePacingTier</c> 참조가 <b>0건</b>이었다.
        /// 두 줄을 같은 프레임#으로 짝지어 놓고 한쪽만 등급을 모르면 축이 중간에 끊긴다.
        /// </summary>
        [Test]
        public void 두_스파이크_로그가_모두_등급_축을_안다()
        {
            foreach (string file in new[] { "FramePacing.cs", "StallAttribution.cs" })
            {
                string src = ReadScript("Platform", file);
                StringAssert.Contains("SpikeTierLedger", src, $"{file}에 등급 장부가 없다.");
                StringAssert.Contains("FramePacing.CurrentTier", src, $"{file}이 등급을 읽지 않는다.");
                StringAssert.Contains("SpikeThresholdMs(", src, $"{file}이 관측 기반 문턱을 쓰지 않는다.");
                StringAssert.Contains("SpikeLogBackoff", src, $"{file}이 적응형 백오프를 쓰지 않는다.");
                StringAssert.Contains("CrossesThrottleBoundary(", src,
                    $"{file}이 모든 등급 전환을 유예 사유로 쓰면 진짜 히치가 전환 칸으로 삼켜진다.");
                StringAssert.Contains("SpikeClass.Actionable", src,
                    $"{file}의 억제 돌파가 '실사용'으로 좁혀져 있지 않다 — 왕복 구간에서 억제가 무력화된다.");
            }

            StringAssert.Contains("SpikeRateWindow", ReadScript("Platform", "FramePacing.cs"),
                "발생률 창이 텀블링으로 되돌아가면 값이 다시 단조 상승 + 점프한다(R2-5).");
        }

        /// <summary>고정 5초 쿨다운이 되살아나면 R2-1이 그대로 재발한다.</summary>
        [Test]
        public void 고정_5초_쿨다운으로_돌아가지_않았다()
        {
            StringAssert.DoesNotContain("_spikeCooldownLeft = SpikeLogCooldownSeconds;",
                ReadScript("Platform", "StallAttribution.cs"));
            StringAssert.DoesNotContain("_spikeCooldownLeft = SpikeLogCooldownSeconds;",
                ReadScript("Platform", "FramePacing.cs"));
        }

        private static void Advance(ref SpikeTierLedger ledger, float seconds)
        {
            // 0.1초 단위로 굴린다 — 소급 슬롯(1초)이 제대로 은퇴하는지까지 재현하려면 필요하다.
            for (float t = 0f; t < seconds; t += 0.1f) ledger.Tick(0.1f, false);
        }
    }
}
