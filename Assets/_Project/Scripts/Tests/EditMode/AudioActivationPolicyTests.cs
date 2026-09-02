using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 온디맨드 오디오 개폐 판정(<see cref="AudioActivationPolicy"/>) 검증.
    ///
    /// <para><b>왜 이 테스트가 실기 없이도 값을 하는가</b>: 이 판정은 순수 함수라 24시간짜리
    /// 시나리오를 초 단위로 접어 넣을 수 있다. 반대로 이것이 <c>Platform/MacOS/</c> 안에 있었다면
    /// Windows 규칙은 이 개발 머신에서 <b>한 줄도 실행되지 않았을 것</b>이다.</para>
    ///
    /// <para><b>기대값의 출처</b>: 임계 관련 기대값은 <b>프로덕션 상수를 참조</b>해서 만든다.
    /// 숫자를 베끼면 상수가 움직인 날 기준과 대상이 함께 움직여 아무것도 못 잰다(CLAUDE.md).
    /// <b>단 하나 예외가 비용 함수다</b> — 계산기를 검증하려면 계산기 밖의 값이 필요하다.
    /// 그 값은 내가 만든 것이 아니라 <c>design-sound</c>가 독립적으로 계산해 보낸 것이며
    /// (4.100초 / 0.0132% / 1:7,579 / 3.4%), 그래서 <b>생성기와 검사기가 같이 틀릴 수 없다</b>.</para>
    /// </summary>
    public class AudioActivationPolicyTests
    {
        private static AudioActivationInputs Inputs(
            bool playing = false,
            float sinceEnded = 0f,
            bool shellForbids = false,
            bool suppressed = false,
            bool deviceOpen = false,
            int failures = 0,
            float sinceLastAttempt = 9999f,
            float linger = AudioActivationPolicy.DefaultLingerSeconds)
            => new AudioActivationInputs(playing, sinceEnded, shellForbids, suppressed,
                deviceOpen, failures, sinceLastAttempt, linger);

        // ==========================================================================================
        // (1) 비침해가 가장 세다 — design-sound가 어떤 값을 골라도 뒤집을 수 없다
        // ==========================================================================================

        [Test]
        public void 셸이_소리를_금지하면_열려_있어도_닫는다()
        {
            var v = AudioActivationPolicy.Evaluate(
                Inputs(shellForbids: true, deviceOpen: true, playing: true,
                       linger: AudioActivationPolicy.MaxLingerSeconds));

            Assert.AreEqual(AudioActivationAction.Close, v.Action,
                "세션 잠금/남의 전체화면 구간인데 장치를 열어 둡니다 — 이 칸이 절전 어서션을 " +
                $"실제로 풀어 주는 장치입니다(원칙 2). 판정 사유: {v.Reason}");
        }

        [Test]
        public void 셸이_소리를_금지하면_재생이_있어도_열지_않는다()
        {
            var v = AudioActivationPolicy.Evaluate(Inputs(shellForbids: true, playing: true));
            Assert.AreEqual(AudioActivationAction.None, v.Action, v.Reason);
        }

        [Test]
        public void 묵음_정책이_막으면_열려_있던_장치를_닫는다()
        {
            var v = AudioActivationPolicy.Evaluate(Inputs(suppressed: true, deviceOpen: true, playing: true));
            Assert.AreEqual(AudioActivationAction.Close, v.Action,
                "기본 OFF와 상주 비용 0이 맞물리는 지점입니다 — 여기가 새면 m_DisableAudio=false와 " +
                $"같은 상태가 됩니다. 판정 사유: {v.Reason}");
        }

        // ==========================================================================================
        // (2) ★ design-sound Q1 — 자리 비움을 개방 게이트에 넣으면 focus.complete가 100% 죽는다
        // ==========================================================================================

        [Test]
        public void 입력이_25분간_없어도_재생_요청만으로_열린다()
        {
            // focus.complete 시나리오: 마지막 사용자 입력이 25분 전, 화면도 안 보고 있다.
            // 이 정책의 입력에는 "마지막 입력으로부터 지난 시간"이 **아예 없다** — 그것이 설계다.
            // 여기서 열리지 않으면 이 기능이 존재하는 이유인 그 소리가 사라진다.
            var v = AudioActivationPolicy.Evaluate(Inputs(playing: true));

            Assert.AreEqual(AudioActivationAction.Open, v.Action,
                "타이머만으로 열려야 합니다. 입력 게이트를 붙이면 focus.complete(마지막 입력 25분 전)가 " +
                $"100% 죽습니다 — SILENCE_POLICY §3-8 M3가 focus.* 3키를 면제하는 이유입니다. 사유: {v.Reason}");
        }

        [Test]
        public void 셸_금지_입력에_자리비움_축이_섞여_있지_않다()
        {
            // ★ 구조적 잠금 — 필드가 늘어나 "자리 비움"이 개방 게이트로 슬며시 들어오는 것을 막는다.
            //   AudioActivationInputs의 생성자 인자 수가 바뀌면 여기서 컴파일이 깨지고, 그때
            //   "무엇을 추가했는지"를 이 주석 앞에서 다시 판단하게 된다.
            var ctor = typeof(AudioActivationInputs).GetConstructors();
            Assert.AreEqual(1, ctor.Length, "입력 구조체의 생성자가 하나가 아닙니다.");
            Assert.AreEqual(8, ctor[0].GetParameters().Length,
                "입력 항목이 늘거나 줄었습니다. 늘렸다면 그것이 '자리 비움/디스플레이 슬립/마지막 입력' " +
                "계열인지 먼저 확인하세요 — 그 축을 개방 게이트에 넣으면 focus.complete가 구조적으로 " +
                "사라지고, 사라진 것은 테스트로도 안 보입니다(소리는 원래 안 나는 것처럼 보인다).");
        }

        // ==========================================================================================
        // (3) 잔류 — ★ 기준시각은 재생 「완료」다(Q2)
        // ==========================================================================================

        [Test]
        public void 재생_중에는_잔류와_무관하게_닫지_않는다()
        {
            // 기준시각을 시작으로 잡으면 성취음 800ms의 꼬리가 잘린다.
            var v = AudioActivationPolicy.Evaluate(
                Inputs(deviceOpen: true, playing: true, sinceEnded: 9999f));

            Assert.AreEqual(AudioActivationAction.None, v.Action,
                $"재생 중에 닫으면 소리 꼬리가 잘립니다. 판정 사유: {v.Reason}");
        }

        [Test]
        public void 잔류_시간_안에는_조용해도_열어_둔다()
        {
            float linger = AudioActivationPolicy.DefaultLingerSeconds;
            var v = AudioActivationPolicy.Evaluate(
                Inputs(deviceOpen: true, sinceEnded: linger * 0.5f, linger: linger));

            Assert.AreEqual(AudioActivationAction.None, v.Action, v.Reason);
        }

        [Test]
        public void 잔류_시간에_정확히_도달하면_닫는다()
        {
            float linger = AudioActivationPolicy.DefaultLingerSeconds;
            var v = AudioActivationPolicy.Evaluate(
                Inputs(deviceOpen: true, sinceEnded: linger, linger: linger));

            Assert.AreEqual(AudioActivationAction.Close, v.Action,
                $"경계값(>=)에서 닫히지 않습니다. 판정 사유: {v.Reason}");
        }

        [Test]
        public void 잔류_요청은_플랫폼_상한을_넘을_수_없다()
        {
            float absurd = AudioActivationPolicy.MaxLingerSeconds * 100f;

            Assert.AreEqual(AudioActivationPolicy.MaxLingerSeconds,
                AudioActivationPolicy.ClampLinger(absurd),
                "상한이 무시되면 design-sound가 사실상 '항상 열기'를 설정할 수 있고, 그것은 " +
                "m_DisableAudio=false와 같은 상태입니다.");

            var v = AudioActivationPolicy.Evaluate(
                Inputs(deviceOpen: true,
                       sinceEnded: AudioActivationPolicy.MaxLingerSeconds + 0.1f,
                       linger: absurd));
            Assert.AreEqual(AudioActivationAction.Close, v.Action, v.Reason);
        }

        // ==========================================================================================
        // (4) 개방 — ★ 미리 열지 않는다(Q3). 미리 여는 것이 오히려 나쁘다
        // ==========================================================================================

        [Test]
        public void 울릴_소리가_없으면_닫힌_채로_둔다()
        {
            // ★ 음성 대조 — 위 '열린다' 테스트들이 '항상 Open'을 내는 고장난 정책에서도
            //   통과하지 않게 한다.
            var v = AudioActivationPolicy.Evaluate(Inputs());
            Assert.AreEqual(AudioActivationAction.None, v.Action,
                $"요청이 없는데 엽니다 — 상주 비용 0이라는 전제가 무너집니다. 판정 사유: {v.Reason}");
        }

        [Test]
        public void 개방_지연이_융합창_안쪽인지_판정할_수_있다()
        {
            // macOS 실측: 콜드 48ms, 이후 약 38ms. 둘 다 융합창(50ms) 안쪽이라 개방 클릭이
            // 우리 소리의 어택에 흡수된다 — 이것이 'Q3 허용'의 유일한 전제다.
            Assert.IsTrue(AudioActivationPolicy.IsOpenLatencyWithinFusionWindow(0.048f),
                "macOS 콜드 실측 48ms가 융합창 밖으로 판정됩니다 — 판정기가 틀렸거나 창이 좁아졌습니다.");
            Assert.IsTrue(AudioActivationPolicy.IsOpenLatencyWithinFusionWindow(0.038f));

            // ★ 음성 대조 — 창 밖은 반드시 false여야 한다. 이것이 항상 true면 Windows 실측이
            //   아무리 나빠도 아무도 못 알아챈다.
            Assert.IsFalse(AudioActivationPolicy.IsOpenLatencyWithinFusionWindow(0.120f),
                "융합창 밖 지연을 안쪽으로 판정합니다 — Windows/블루투스 실측이 나빠도 " +
                "이 판정기가 조용히 통과시키게 됩니다(그러면 Q3 판정을 다시 열 계기가 사라집니다).");
            Assert.IsFalse(AudioActivationPolicy.IsOpenLatencyWithinFusionWindow(-1f),
                "아직 한 번도 열지 않은 상태(음수)를 '안쪽'으로 판정합니다.");
        }

        // ==========================================================================================
        // (5) 백오프 — ★ 영구 비활성이 아니다
        // ==========================================================================================

        [Test]
        public void 열기_실패_직후에는_백오프를_지킨다()
        {
            float wait = AudioActivationPolicy.BackoffSecondsFor(1);
            Assert.Greater(wait, 0f, "첫 실패 뒤 대기가 0이면 실패 루프가 매 프레임 OS를 두드립니다.");

            var v = AudioActivationPolicy.Evaluate(
                Inputs(playing: true, failures: 1, sinceLastAttempt: wait * 0.5f));
            Assert.AreEqual(AudioActivationAction.None, v.Action, v.Reason);
        }

        [Test]
        public void 백오프가_지나면_다시_시도한다()
        {
            float wait = AudioActivationPolicy.BackoffSecondsFor(1);
            var v = AudioActivationPolicy.Evaluate(
                Inputs(playing: true, failures: 1, sinceLastAttempt: wait));
            Assert.AreEqual(AudioActivationAction.Open, v.Action, v.Reason);
        }

        [Test]
        public void 백오프는_영구_비활성이_아니다()
        {
            // ★ 이 라운드가 고치려는 버그를 스스로 만드는 것을 막는 검사다.
            //   블루투스 헤드폰이 아직 안 붙은 것 같은 **일시적** 실패에서 영구 비활성을 걸면
            //   남은 하루 종일 소리가 안 나고 사용자는 원인을 알 방법이 없다.
            float huge = AudioActivationPolicy.BackoffSecondsFor(100000);
            Assert.IsFalse(float.IsInfinity(huge), "무한 대기 = 사실상 영구 비활성입니다.");
            Assert.AreEqual(
                AudioActivationPolicy.OpenRetryBackoffSeconds[AudioActivationPolicy.OpenRetryBackoffSeconds.Length - 1],
                huge,
                "백오프가 사다리 끝에서 멈추지 않습니다 — 끝없이 늘어나면 영구 비활성과 구분되지 않습니다.");

            var v = AudioActivationPolicy.Evaluate(
                Inputs(playing: true, failures: 100000, sinceLastAttempt: huge + 1f));
            Assert.AreEqual(AudioActivationAction.Open, v.Action,
                $"실패가 아무리 쌓여도 결국 다시 시도해야 합니다. 판정 사유: {v.Reason}");
        }

        [Test]
        public void 백오프_중이어도_닫기는_막지_않는다()
        {
            var v = AudioActivationPolicy.Evaluate(
                Inputs(deviceOpen: true, shellForbids: true, failures: 100000, sinceLastAttempt: 0f));
            Assert.AreEqual(AudioActivationAction.Close, v.Action,
                $"백오프가 비침해 닫기를 가로막으면 어서션이 계속 잡힙니다. 판정 사유: {v.Reason}");
        }

        // ==========================================================================================
        // (6) ★ 비용 함수 — design-sound가 독립적으로 계산한 값으로 교정한다
        // ==========================================================================================

        [Test]
        public void 어서션_보유_예산이_design_sound의_계산과_일치한다()
        {
            // design-sound Q2가 보낸 값: 0.800(클립) + 3.0(잔류) + 0.3(해제 지연) = 4.100초
            float hold = AudioActivationPolicy.EstimateMaxAssertionHoldSeconds(
                0.800f, AudioActivationPolicy.DefaultLingerSeconds);

            Assert.AreEqual(4.100f, hold, 0.001f,
                "어서션 보유 예산이 design-sound의 독립 계산(4.100초)과 갈립니다 — 두 계산 중 " +
                "하나가 틀렸고, 그것이 이 기능의 절전 근거 전체입니다.");

            // 그리고 그 값은 최단 디스플레이 슬립 120초의 3.4%여야 한다.
            Assert.AreEqual(0.0342f, hold / 120f, 0.0005f,
                "슬립 대비 비율이 design-sound의 3.4%와 갈립니다.");
        }

        [Test]
        public void 일간_듀티가_design_sound의_계산과_일치한다()
        {
            // design-sound Q2: 하루 3건 × (0.8 + 3.0) / 86400 = 0.0132% → 상시 개방 대비 1/7,579
            float duty = AudioActivationPolicy.EstimateDailyOpenDutyCycle(
                3, 0.800f, AudioActivationPolicy.DefaultLingerSeconds);

            Assert.AreEqual(0.000132f, duty, 0.0000005f,
                "일간 듀티가 design-sound의 독립 계산(0.0132%)과 갈립니다.");
            Assert.AreEqual(7579f, 1f / duty, 2f,
                "상시 개방 대비 비율이 design-sound의 1:7,579와 갈립니다.");
        }

        [Test]
        public void 듀티_계산도_상한을_존중한다()
        {
            // 상한을 넘겨 요청해도 상한 기준으로 계산돼야 한다 — 그러지 않으면 design-sound가
            // 표에서 본 숫자와 실제 동작이 갈린다.
            float capped = AudioActivationPolicy.EstimateDailyOpenDutyCycle(
                1, 0f, AudioActivationPolicy.MaxLingerSeconds * 100f);
            Assert.AreEqual(AudioActivationPolicy.MaxLingerSeconds / 86400f, capped, 0.0000001f);

            // 이벤트가 없으면 0. (양성 대조: 위에서 0이 아닌 값이 나왔으므로 이 0은 공허하지 않다)
            Assert.AreEqual(0f, AudioActivationPolicy.EstimateDailyOpenDutyCycle(0, 1f, 3f), 0.0000001f);
        }

        // ==========================================================================================
        // (7) 순수성
        // ==========================================================================================

        [Test]
        public void 같은_입력에는_같은_판정이_나온다()
        {
            var input = Inputs(playing: true, failures: 2, sinceLastAttempt: 0f);
            var a = AudioActivationPolicy.Evaluate(input);
            var b = AudioActivationPolicy.Evaluate(input);

            Assert.AreEqual(a.Action, b.Action,
                "판정이 호출 사이에 상태를 들고 있습니다 — 그러면 EditMode가 시나리오를 " +
                "재현할 수 없고, 실기 로그의 사유도 신뢰할 수 없게 됩니다.");
            Assert.AreEqual(a.Reason, b.Reason);
        }

        [Test]
        public void 모든_판정에는_사람이_읽는_사유가_붙는다()
        {
            var cases = new[]
            {
                Inputs(),
                Inputs(playing: true),
                Inputs(shellForbids: true, deviceOpen: true),
                Inputs(suppressed: true, deviceOpen: true),
                Inputs(deviceOpen: true, sinceEnded: 999f),
                Inputs(deviceOpen: true, playing: true),
                Inputs(playing: true, failures: 3, sinceLastAttempt: 0f),
            };

            // ★ 빈 목록 잠금 — 기대 개수를 명시한다(면제 목록이 비어 조용히 초록이 된 사고 회피).
            Assert.AreEqual(7, cases.Length);

            foreach (var c in cases)
            {
                var v = AudioActivationPolicy.Evaluate(c);
                Assert.IsNotEmpty(v.Reason,
                    "사유 없는 판정이 있습니다 — 실기 로그에서 '왜 지금 열렸나'를 되짚을 수 없으면 " +
                    "이 정책은 반증 불가능해집니다.");
            }
        }
    }
}
