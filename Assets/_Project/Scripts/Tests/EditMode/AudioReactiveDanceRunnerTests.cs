using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <see cref="AudioReactiveDanceRunner"/> — <b>«조회 실패»를 «무음»으로 접지 않는가</b>.
    /// 2026-09-06 신설 (dev-platform).
    ///
    /// ============================================================================
    /// 여기서 잡으려는 것은 <b>조용한 초록</b> 하나다
    /// ============================================================================
    /// <see cref="AudioReactiveDancePolicy"/> 3절은 <i>"조회에 실패한 틱에는 정책을 부르지 마라"</i>고
    /// 못박는다. 가장 자연스러운 구현(<c>probe.TryRead(out bool p); Evaluate(state, p, …)</c>)은
    /// 그 규칙을 <b>정확히 반대로</b> 어긴다 — 실패했을 때 <c>p</c>는 <c>false</c>이므로:
    /// <code>
    ///   조회 실패가 이어짐 → offRun 이 **가짜로** 증가 → T₂ 충족 → "음악이 끝났다"고 Stop
    ///   화면에 보이는 것: 아무 일도 안 일어남. 로그도 «정상 종료».
    ///   ⇒ «프로브가 고장났다»가 «음악이 안 나온다»와 **똑같이 생긴다**.
    /// </code>
    ///
    /// <para>★ 이 파일은 <b>가짜 프로브</b>로 돌린다 — 진짜 CoreAudio/WASAPI를 부르면
    /// «그때 개발자가 음악을 틀어 놨는가»가 결과를 바꾼다. 그리고 시간은 <b>벽시계 델타를 인자로
    /// 직접 넣으므로</b> 프레임 수에 의존하지 않는다(CLAUDE.md의 시간 기반 검증 규칙).</para>
    ///
    /// <para>★ 숫자를 하나도 적지 않는다 — 전부 <see cref="AudioReactiveDancePolicy"/>의 상수를
    /// 참조한다. 임계값이 바뀌면 이 테스트도 함께 따라가야 하고, 베껴 두면 그날 갈라진다.</para>
    /// </summary>
    public sealed class AudioReactiveDanceRunnerTests
    {
        /// <summary>계약대로만 답하는 가짜 프로브. <b>레벨도 임계값도 없다</b> — 이 계약은 불리언이다.</summary>
        private sealed class FakeProbe : ISystemAudioActivityProbe
        {
            public bool ReadSucceeds = true;
            public bool Playing;
            public int ReadCount;

            public string PlatformTag => "테스트/가짜프로브";
            public bool SupportsPushNotification => false;

            public bool TryReadIsAudioPlaying(out bool isPlaying)
            {
                ReadCount++;
                isPlaying = Playing;
                return ReadSucceeds;
            }
        }

        private static float Poll => AudioReactiveDanceRunner.PollIntervalSeconds;

        /// <summary>주어진 시간만큼 폴링 주기로 틱을 돌린다. 반환은 마지막 결론.</summary>
        private static AudioReactiveDanceAction Run(
            AudioReactiveDanceRunner runner, float seconds, bool suppressed = false)
        {
            AudioReactiveDanceAction last = AudioReactiveDanceAction.None;
            for (float t = 0f; t < seconds; t += Poll) last = runner.Tick(Poll, suppressed);
            return last;
        }

        // ====================================================================
        // ① 배선이 없을 때 — 「모르면 안 한다」
        // ====================================================================

        [Test]
        public void 프로브가_없으면_게이트는_영원히_닫힌_채다()
        {
            var runner = new AudioReactiveDanceRunner(null);

            Run(runner, AudioReactiveDancePolicy.StuckSignalCeilingSeconds / 10f);

            Assert.IsFalse(runner.HasProbe, "양성 대조 — null을 넣었으므로 배선이 없다고 보고해야 합니다.");
            Assert.IsFalse(runner.IsGateOpen,
                "★ 배선이 없는데 춤 게이트가 열렸습니다 — 자동 발동 경로에는 보여줄 사유도, " +
                "그것을 읽을 사람도 없습니다. 아무도 모르는 사이에 춤이 발동합니다.");
            Assert.AreEqual(AudioReactiveDancePhase.Armed, runner.Phase,
                "정책을 부르지 않았어야 하므로 위상이 처음 그대로여야 합니다.");
        }

        // ====================================================================
        // ② 정상 경로 — 이게 통과해야 아래 '실패' 검사들이 의미를 갖는다
        // ====================================================================

        [Test]
        public void 연속_재생이_T1을_넘으면_게이트가_열린다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);

            Run(runner, AudioReactiveDancePolicy.StartDelaySeconds + Poll * 2f);

            Assert.Greater(probe.ReadCount, 0,
                "양성 대조 실패 — 프로브를 한 번도 부르지 않았습니다. 아래 판정은 전부 무의미합니다.");
            Assert.IsTrue(runner.IsGateOpen,
                "T₁만큼 연속 재생했는데 게이트가 안 열립니다: " + runner.LastReason);
            Assert.AreEqual(AudioReactiveDancePhase.Dancing, runner.Phase);
        }

        /// <summary>
        /// ★ T₁이 막기로 한 것 — <b>알림음 1회</b>. 여기서는 계약이 정의한 <b>원(raw) 신호</b>를 넣는다.
        /// <para><b>이 통과를 «Windows도 안전하다»로 읽지 마라.</b> Windows 구현체는 자기
        /// <c>PeakHoldSeconds</c>만큼 ON을 <b>연장 보고</b>하므로 원 신호가 더 길어진다 —
        /// 그 산술적 위험은 <c>PlatformParityAuditTests</c>의 U-7에 적혀 있고 실기 미확인이다.</para>
        /// </summary>
        [Test]
        public void 알림음_길이의_재생은_게이트를_열지_못한다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);

            Run(runner, AudioReactiveDancePolicy.MeasuredNotificationOnSeconds);
            probe.Playing = false;
            Run(runner, Poll * 2f);

            Assert.IsFalse(runner.IsGateOpen,
                "★ 알림음 1회 길이의 소리로 춤이 시작됐습니다 — T₁의 존재 이유가 정확히 이것입니다. " +
                "사용자에게는 «메시지 올 때마다 캐릭터가 씰룩거린다»로 보입니다.");
        }

        // ====================================================================
        // ③ ★ 핵심 — 조회 실패를 무음으로 접지 않는가
        // ====================================================================

        /// <summary>
        /// ★★ <b>실패를 «무음»으로 접었다면 여기서 걸린다.</b> 접었다면 <c>offRun</c>이 가짜로 쌓여
        /// T₂를 채우고 «음악이 끝났다»는 <b>정상 종료</b>로 보고했을 것이다.
        /// </summary>
        [Test]
        public void 짧은_조회_실패는_직전_판정을_유지하고_정책을_부르지_않는다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);
            Run(runner, AudioReactiveDancePolicy.StartDelaySeconds + Poll * 2f);
            Assert.IsTrue(runner.IsGateOpen, "전제 실패 — 먼저 춤이 시작돼 있어야 합니다.");

            // T₂보다 짧게 실패시킨다.
            probe.ReadSucceeds = false;
            AudioReactiveDanceAction action = Run(runner, AudioReactiveDanceRunner.BlindResetSeconds - Poll * 2f);

            Assert.IsFalse(runner.LastReadOk,
                "양성 대조 — 조회가 실패하도록 만들었으므로 실패로 보고해야 합니다. " +
                "이 값이 true면 실패를 주입하지 못한 것이고 아래 판정은 무효입니다.");
            Assert.IsTrue(runner.IsGateOpen,
                "★ 짧은 조회 실패로 춤이 끊겼습니다. «모른다»는 «음악이 끝났다»가 아닙니다: " + runner.LastReason);
            Assert.AreEqual(AudioReactiveDanceAction.Continue, action);
            Assert.AreEqual(AudioReactiveDanceRunner.ReasonBlindHold, runner.LastReason,
                "실패 틱인데 정책이 낸 사유가 붙었습니다 — 정책을 불렀다는 뜻입니다(3절 위반).");
        }

        [Test]
        public void 긴_조회_실패는_누산을_버리고_게이트를_닫는다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);
            Run(runner, AudioReactiveDancePolicy.StartDelaySeconds + Poll * 2f);
            Assert.IsTrue(runner.IsGateOpen, "전제 실패 — 먼저 춤이 시작돼 있어야 합니다.");

            // ★ «마지막 틱의 결론»을 보면 안 된다 — Stop은 임계를 넘는 **그 틱에 한 번** 나오고,
            //   그 뒤로는 이미 닫혀 있으므로 None이 이어진다. 마지막 값만 재면 이 검사는
            //   «Stop이 영영 안 나왔다»와 «Stop이 나왔다»를 구분하지 못한다.
            probe.ReadSucceeds = false;
            bool sawStop = false;
            for (float t = 0f; t < AudioReactiveDanceRunner.BlindResetSeconds + Poll * 2f; t += Poll)
            {
                if (runner.Tick(Poll, false) == AudioReactiveDanceAction.Stop) sawStop = true;
            }

            Assert.IsTrue(sawStop,
                "★ 조회가 T₂ 넘게 실패했는데 «멈춰라»가 한 번도 나오지 않았습니다 — 위쪽이 춤을 끝낼 " +
                "계기를 못 받고, 캐릭터는 신호가 끊긴 채로 계속 춥니다.");
            Assert.IsFalse(runner.IsGateOpen);
            Assert.AreEqual(AudioReactiveDancePhase.Armed, runner.Phase,
                "누산을 버렸어야 합니다 — 낡은 누산 위에 나중의 큰 델타가 얹히면 T₁/T₄가 한 틱에 넘어갑니다.");
        }

        [Test]
        public void 조회를_못_해도_억제는_이긴다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);
            Run(runner, AudioReactiveDancePolicy.StartDelaySeconds + Poll * 2f);
            Assert.IsTrue(runner.IsGateOpen, "전제 실패 — 먼저 춤이 시작돼 있어야 합니다.");

            probe.ReadSucceeds = false;
            AudioReactiveDanceAction action = runner.Tick(Poll, suppressed: true);

            Assert.AreEqual(AudioReactiveDanceAction.Stop, action,
                "★ 신호를 모르는 상태에서 억제가 참인데 춤이 안 멈췄습니다 — 집중 모드를 켰는데 " +
                "«마침 프로브가 실패 중»이라는 이유로 계속 춤추게 됩니다.");
            Assert.IsFalse(runner.IsGateOpen);
            Assert.AreEqual(AudioReactiveDanceRunner.ReasonBlindSuppressed, runner.LastReason);
        }

        // ====================================================================
        // ④ 틱 자체가 오래 끊긴 경우 — OS 절전 복귀 / 긴 프레임 정지
        // ====================================================================

        /// <summary>
        /// ★ 큰 델타 하나로 T₁을 <b>한 틱에</b> 넘지 않는가. 넘으면 노트북 뚜껑을 열자마자
        /// (그 사이 아무 소리도 안 났는데) 캐릭터가 춤추기 시작한다.
        /// </summary>
        [Test]
        public void 오래_끊겼다_돌아온_틱은_처음부터_다시_센다()
        {
            var probe = new FakeProbe { Playing = true };
            var runner = new AudioReactiveDanceRunner(probe);

            // 한 시간 자고 일어났는데 마침 음악이 나오고 있다.
            runner.Tick(3600f, suppressed: false);

            Assert.IsFalse(runner.IsGateOpen,
                "★ 큰 델타 하나로 게이트가 열렸습니다 — 절전에서 복귀한 순간 곧바로 춤춥니다. " +
                "그 델타는 «연속 재생을 관측한 시간»이 아니라 «아무것도 안 본 시간»입니다: " + runner.LastReason);
            Assert.AreEqual(AudioReactiveDanceRunner.ReasonStaleDelta, runner.LastReason);

            // 그 뒤로는 정상적으로 T₁을 채워 열려야 한다(닫아 두기만 하면 그것도 결함이다).
            Run(runner, AudioReactiveDancePolicy.StartDelaySeconds + Poll * 2f);
            Assert.IsTrue(runner.IsGateOpen,
                "복귀 후에도 영영 안 열립니다 — 리셋이 «영구 잠금»이 돼 버렸습니다: " + runner.LastReason);
        }
    }
}
