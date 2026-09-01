using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// **스파이크 계기에 문맥을 붙인다**(2026-09-02 라운드) — 잠금.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// <c>[프레임스파이크]</c> 줄은 <b>"누적 4,222회"만</b> 말했다. 경과 시간도, 분당 발생률도,
    /// 어느 등급에서 쌓였는지도 말하지 않는다. <b>누적 단조증가 숫자는 24시간 상주 앱에서 항상
    /// "망가졌다"로 읽힌다</b> — 그게 3분에 쌓였는지 20시간에 쌓였는지 구분할 수 없기 때문이다.
    ///
    /// ============================================================================
    /// ★ 같은 라운드에서 **반증된** 처방 3건 (여기서 되살아나지 않게 잠근다)
    /// ============================================================================
    /// <list type="number">
    /// <item><b><c>× renderFrameInterval</c></b> — <c>dtMs</c>는 <c>Time.unscaledDeltaTime</c> =
    ///   <b>게임 루프 주기</b>인데 <c>renderFrameInterval</c>은 프레젠트만 건너뛰고 루프는 늦추지
    ///   않는다. 곱하면 임계가 167ms로 올라가 실제 관측된 100~216ms 스톨이 통째로 사라진다 —
    ///   계기를 고치려다 <b>눈을 감는</b> 변경이다.</item>
    /// <item><b>기대치 보정만으로 충분</b> — <see cref="StallAttribution.SpikeAbsoluteMs"/> 100ms
    ///   절대 하한이 2.5배 기대치를 전부 삼켜서 DisplayOff 외 전 등급의 실효 임계는 그대로다.</item>
    /// <item><b>오클루전 억제로 해결</b> — 등급별 귀속 결과 Suspended는 4~14%뿐이라 노이즈의
    ///   86~96%가 남는다.</item>
    /// </list>
    /// 그래서 이 라운드가 실제로 고친 것은 <b>로그의 읽을 수 있음</b>이다.
    /// </summary>
    public class SpikeInstrumentContextTests
    {
        private static string ReadScript(params string[] parts)
            => File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts",
                Path.Combine(parts)));

        // ====================================================================================
        // A. 기대 프레임 시간 — 실제 기구에서 나온다
        // ====================================================================================

        [Test]
        public void vsync가_켜져있으면_주사율로_기대치를_낸다()
        {
            // ★ 이것이 예전 계산의 거짓말이었다: vsync가 켜져 있으면 targetFrameRate는 무시되는데
            //   그 값으로 "기대 프레임 시간"을 말했다.
            Assert.AreEqual(1000f / 120f, StallAttribution.ExpectedFrameMs(1, 120.0, 60), 0.01f);
            Assert.AreEqual(2 * (1000f / 120f), StallAttribution.ExpectedFrameMs(2, 120.0, 60), 0.01f);
            Assert.AreEqual(1000f / 60f, StallAttribution.ExpectedFrameMs(1, 60.0, 240), 0.01f);
        }

        [Test]
        public void vsync가_꺼져있으면_targetFrameRate로_낸다()
        {
            Assert.AreEqual(250f, StallAttribution.ExpectedFrameMs(0, 120.0, 4), 0.01f);   // DisplayOff 등급
            Assert.AreEqual(1000f / 15f, StallAttribution.ExpectedFrameMs(0, 120.0, 15), 0.01f); // Away 등급
            Assert.AreEqual(1000f / 60f, StallAttribution.ExpectedFrameMs(0, 120.0, -1), 0.01f); // 제한 없음
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 계기가 <b>눈을 감는</b> 경우를 막는다. 주사율 조회가 0을 돌려줄 때
        /// 1000/0 = Infinity가 되면 <c>dtMs &lt; Infinity * 2.5</c>가 항상 참이라 스파이크를
        /// <b>한 건도 세지 않게</b> 된다. 그 상태의 로그는 "0회"라 완벽해 보인다 — 최악의 실패다.
        /// </summary>
        [Test]
        public void 주사율_조회가_실패해도_Infinity를_만들지_않는다()
        {
            foreach (double badHz in new[] { 0.0, -1.0, 0.5, double.NaN })
            {
                float ms = StallAttribution.ExpectedFrameMs(1, badHz, -1);
                Assert.IsFalse(float.IsNaN(ms) || float.IsInfinity(ms), $"hz={badHz}에서 {ms}가 나왔다.");
                Assert.AreEqual(1000f / 60f, ms, 0.01f, $"hz={badHz}는 60Hz로 떨어져야 한다.");
            }
        }

        [Test]
        public void 기대치는_어떤_입력에서도_양수다()
        {
            int[] vsyncs = { 0, 1, 2, 4 };
            double[] hzs = { 0.0, 30.0, 60.0, 120.0, 240.0 };
            int[] caps = { -1, 0, 4, 15, 60, 120 };
            foreach (int v in vsyncs)
            foreach (double hz in hzs)
            foreach (int cap in caps)
            {
                float ms = StallAttribution.ExpectedFrameMs(v, hz, cap);
                Assert.Greater(ms, 0f, $"vsync={v} hz={hz} cap={cap}");
                Assert.IsFalse(float.IsInfinity(ms));
            }
        }

        /// <summary>
        /// ★★ 반증 1 잠금. <c>renderFrameInterval</c>을 곱하면 DisplayOff(4fps=250ms)에서
        /// 임계가 <c>250 × 2.5 × interval</c>로 올라가 실측 스톨이 통째로 사라진다.
        /// 계산 함수는 그 값을 <b>쳐다보지도 않아야</b> 한다.
        /// </summary>
        [Test]
        public void 기대치_계산은_renderFrameInterval을_곱하지_않는다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            int fn = src.IndexOf("public static float ExpectedFrameMs(int vSyncCount",
                System.StringComparison.Ordinal);
            Assert.Greater(fn, 0, "순수 산술 오버로드가 사라졌다.");

            int end = src.IndexOf("\n        }", fn, System.StringComparison.Ordinal);
            string body = src.Substring(fn, end - fn);
            StringAssert.DoesNotContain("renderFrameInterval", body,
                "renderFrameInterval을 곱하면 임계가 167ms로 올라가 관측된 100~216ms 스톨이 사라진다.");
        }

        // ====================================================================================
        // B. 로그가 등급 축으로 쪼개져 있고 발생률을 함께 말한다
        // ====================================================================================

        [Test]
        public void 스파이크_로그가_등급축과_발생률을_함께_찍는다()
        {
            string src = ReadScript("Platform", "FramePacing.cs");

            StringAssert.Contains("_spikeCountActionable", src,
                "등급 축 분해가 없으면 '조인 구간의 정상적인 긴 프레임'이 '진짜 히치'를 덮는다.");
            StringAssert.Contains("_spikeCountThrottled", src);

            int log = src.IndexOf("[프레임스파이크]", System.StringComparison.Ordinal);
            Assert.Greater(log, 0, "스파이크 로그가 사라졌다.");
            int end = src.IndexOf("\");", log, System.StringComparison.Ordinal);
            string line = src.Substring(log, end - log);

            StringAssert.Contains("_spikeCountActionable", line, "로그가 두 숫자를 함께 찍어야 한다.");
            StringAssert.Contains("_spikeCountThrottled", line);
            StringAssert.Contains("분당", line, "누적만 있고 발생률이 없으면 '망가졌다'로만 읽힌다.");
        }

        /// <summary>
        /// ★ 반증 잠금 — <c>_spikeCount++</c>는 쿨다운 <b>앞</b>에 있어야 한다. 뒤로 옮기면 5초
        /// 쿨다운 때문에 누적값이 실제 발생의 1/20만 세게 된다(전부 세고 일부만 찍는 것이 의도다).
        /// </summary>
        [Test]
        public void 스파이크_카운트는_쿨다운_앞에서_센다()
        {
            string src = ReadScript("Platform", "FramePacing.cs");
            int count = src.IndexOf("_spikeCount++;", System.StringComparison.Ordinal);
            int cooldown = src.IndexOf("if (_spikeCooldownLeft > 0f) return;", System.StringComparison.Ordinal);
            Assert.Greater(count, 0);
            Assert.Greater(cooldown, count,
                "카운트가 쿨다운 뒤로 가면 누적값이 실제 발생의 1/20이 된다.");
        }

        /// <summary>
        /// 두 로그(<c>[프레임스파이크]</c> / <c>[스톨귀인]</c>)는 같은 프레임#으로 1:1 짝을 이뤄야
        /// 사용자가 눈으로 대조할 수 있다. 그러려면 상대 조건의 <b>분모가 같아야</b> 하는데, 예전에는
        /// 같은 식을 양쪽에 복사해 뒀었다. 한 함수를 함께 부르는 형태로 합쳤음을 잠근다.
        /// </summary>
        [Test]
        public void 두_스파이크_로그가_같은_기대치_함수를_쓴다()
        {
            string pacing = ReadScript("Platform", "FramePacing.cs");
            StringAssert.Contains("StallAttribution.ExpectedFrameMs()", pacing,
                "기대치 계산이 다시 복제되면 두 로그의 프레임# 짝이 조용히 어긋난다.");
        }
    }
}
