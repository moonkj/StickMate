using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 음악 감지 → 춤 히스테리시스 게이트(<see cref="AudioReactiveDancePolicy"/>) 검증.
    ///
    /// <para>★ <b>숫자를 한 개도 베끼지 않는다</b>(CLAUDE.md). T₁~T₄도, 실측 앵커(곡 사이 공백
    /// 1.09초 · 알림음 ON 2.38초)도 전부 <b>프로덕션 상수를 참조</b>한다. design-systems가 값을
    /// 바꾸는 날 이 테스트는 <b>따라 움직이고</b>, 관계가 깨질 때만 빨개진다.</para>
    ///
    /// <para>★ 이 파일이 재는 두 불변식은 <c>design-systems</c>가 §19-5에서 명시적으로 요구한 것이다:
    /// ① <b>알림 2회로 춤이 발동하지 않는다</b>(양성 대조 필수) ②
    /// <b>공백 없는 ON이 T₄에 닿으면 멈춘다</b>.</para>
    ///
    /// <para><b>왜 EditMode인가</b>: 이 정책은 Unity API를 한 줄도 안 쓰는 순수 함수라
    /// <b>20분(T₄) 시나리오를 밀리초 안에 접어서</b> 돌릴 수 있다. 벽시계 예산이 필요한
    /// PlayMode 규칙(CLAUDE.md)은 여기 해당하지 않는다 — 시간이 <b>인자</b>이지 흐르는 것이 아니다.</para>
    /// </summary>
    public sealed class AudioReactiveDancePolicyTests
    {
        /// <summary>정책을 시계열로 돌리는 최소 시뮬레이터. 상태를 밖에서 들고 다니는 것이
        /// 이 정책의 계약이므로(정적 가변 상태 0개) 테스트도 같은 모양으로 쓴다.</summary>
        private sealed class DanceSim
        {
            public AudioReactiveDanceState State;
            public readonly List<AudioReactiveDanceAction> Seen = new List<AudioReactiveDanceAction>();
            public string LastReason = string.Empty;

            public void Tick(bool playing, float dt, bool suppressed = false)
            {
                AudioReactiveDanceVerdict v =
                    AudioReactiveDancePolicy.Evaluate(State, playing, dt, suppressed);
                State = v.Next;
                LastReason = v.Reason;
                Seen.Add(v.Action);
            }

            /// <summary>같은 신호를 <paramref name="seconds"/>만큼 <paramref name="dt"/> 간격으로 흘린다.</summary>
            public void Run(bool playing, float seconds, float dt, bool suppressed = false)
            {
                int steps = (int)Math.Round(seconds / dt);
                for (int i = 0; i < steps; i++) Tick(playing, dt, suppressed);
            }

            public bool Saw(AudioReactiveDanceAction action) => Seen.Contains(action);
            public bool IsDancing => State.Phase == AudioReactiveDancePhase.Dancing;
            public void Forget() => Seen.Clear();
        }

        /// <summary>폴링 실측 주기(2Hz)보다 촘촘한 틱. 게이트가 «폴링 해상도 때문에» 통과/실패하는
        /// 것이 아니라 <b>규칙 때문에</b> 그러는지 보려고 일부러 잘게 썬다.</summary>
        private const float FineTick = 0.05f;

        // ====================================================================
        // ① design-systems 불변식 1 — 알림 2회로는 발동하지 않는다 (+ 양성 대조)
        // ====================================================================

        /// <summary>
        /// ★★ <b>이 저장소의 함정 그 자체.</b> T₂(해제 지연)를 개시 판정에도 쓰면 1.09초 공백이
        /// 병합되어 알림 2회가 5.86초 ON으로 보이고, 그 5.86초가 T₁(3.0초)을 <b>뚫는다</b>.
        /// 그러면 알림음 두 번에 캐릭터가 춤춘다.
        ///
        /// <para><b>양성 대조가 같은 테스트 안에 있다</b> — 아래 절반은 «연속 ON 3.0초면 반드시
        /// 발동한다»를 확인한다. 이게 없으면 "아무것도 발동 안 하게 만들어 두고 초록"이 성립한다.</para>
        /// </summary>
        [Test]
        public void 알림음_2회는_춤을_발동시키지_않는다_그리고_연속_ON은_발동시킨다()
        {
            // ---- 음성: 실측 그림 그대로(ON 2.38 → OFF 1.09 → ON 2.38) ----
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.MeasuredNotificationOnSeconds, FineTick);
            sim.Run(false, AudioReactiveDancePolicy.MeasuredTrackGapSeconds, FineTick);
            sim.Run(true, AudioReactiveDancePolicy.MeasuredNotificationOnSeconds, FineTick);

            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Start),
                "★ 알림음 2회(각 " + AudioReactiveDancePolicy.MeasuredNotificationOnSeconds +
                "초, 간격 " + AudioReactiveDancePolicy.MeasuredTrackGapSeconds + "초)에 춤이 발동했습니다. " +
                "T₂(해제 지연)를 개시 판정에 섞으면 두 알림이 병합되어 T₁을 뚫습니다 — " +
                "OnRunSeconds는 반드시 **병합 없는 원 신호**여야 합니다(§19-1). 마지막 사유: " + sim.LastReason);
            Assert.IsFalse(sim.IsDancing, "알림 2회 뒤 위상이 Dancing입니다.");

            // ---- 양성 대조: 연속 ON이 T₁을 넘으면 반드시 발동한다 ----
            var control = new DanceSim();
            control.Run(true, AudioReactiveDancePolicy.StartDelaySeconds + FineTick, FineTick);

            Assert.IsTrue(control.Saw(AudioReactiveDanceAction.Start),
                "★ 양성 대조 실패 — 연속 ON " + AudioReactiveDancePolicy.StartDelaySeconds +
                "초에도 춤이 발동하지 않습니다. 그렇다면 위의 '발동 안 함'은 아무것도 증명하지 않습니다. " +
                "마지막 사유: " + control.LastReason);
            Assert.IsTrue(control.IsDancing, "발동했는데 위상이 Dancing이 아닙니다.");
        }

        /// <summary>알림 하나가 <b>혼자서도</b> 못 뚫는지. 위 테스트의 음성 절반이 «두 개여서 실패»가
        /// 아니라 «하나도 못 뚫는다»에서 시작한다는 것을 못박는다.</summary>
        [Test]
        public void 알림음_1회는_T1에_못_미친다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.MeasuredNotificationOnSeconds, FineTick);

            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Start),
                "알림음 1회의 실측 ON 지속이 T₁을 넘었습니다 — 두 상수의 관계가 깨졌습니다.");
        }

        // ====================================================================
        // ② design-systems 불변식 2 — 공백 없는 ON은 T₄에서 멈춘다
        // ====================================================================

        /// <summary>
        /// ★★ <b>macOS M-B(무음 스트림 거짓 양성)의 유일한 탈출구.</b> macOS 신호는 "소리"가 아니라
        /// "스트림 개방"이라, 무음 스트림을 여는 앱 하나만 상주해도 신호가 <b>고착</b>된다.
        /// T₄가 없으면 그건 버그가 아니라 <b>설계된 무한 춤</b>이다.
        /// </summary>
        [Test]
        public void 공백_없는_ON이_상한에_닿으면_춤이_멈춘다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StuckSignalCeilingSeconds + 1f,
                AudioReactiveDancePolicy.ReferencePollIntervalSeconds);

            Assert.IsTrue(sim.Saw(AudioReactiveDanceAction.Start), "양성 대조 실패 — 애초에 시작하지 않았습니다.");
            Assert.IsTrue(sim.Saw(AudioReactiveDanceAction.Stop),
                "★ 공백 없는 ON " + AudioReactiveDancePolicy.StuckSignalCeilingSeconds +
                "초에도 춤이 멈추지 않았습니다 — macOS에서 영원히 춤춥니다. 마지막 사유: " + sim.LastReason);
            Assert.AreEqual(AudioReactiveDancePhase.Lockout, sim.State.Phase,
                "T₄에 걸렸는데 위상이 Lockout이 아닙니다 — 다음 틱에 곧바로 다시 시작합니다.");
        }

        /// <summary>★ <b>진짜 음악은 T₄에 안 걸린다</b>. 트랙 경계의 OFF가 <c>onRun</c>을 리셋하기
        /// 때문이다 — 앨범·플레이리스트는 몇 시간이든 계속 춤춘다(§19-2). 이 대조가 없으면
        /// 위 T₄ 테스트는 "그냥 오래 틀면 멈춘다"로도 통과한다.</summary>
        [Test]
        public void 트랙_경계가_있는_긴_재생은_상한에_걸리지_않는다()
        {
            var sim = new DanceSim();
            float dt = AudioReactiveDancePolicy.ReferencePollIntervalSeconds;

            // 5분짜리 트랙 여러 개를 T₄를 훌쩍 넘게 이어 붙인다. 사이마다 실측 공백이 들어간다.
            float track = 300f;
            int tracks = (int)(AudioReactiveDancePolicy.StuckSignalCeilingSeconds / track) + 3;
            for (int i = 0; i < tracks; i++)
            {
                sim.Run(true, track, dt);
                sim.Run(false, AudioReactiveDancePolicy.MeasuredTrackGapSeconds, dt);
            }

            Assert.AreNotEqual(AudioReactiveDancePhase.Lockout, sim.State.Phase,
                "★ 트랙 경계가 있는 " + (tracks * track) + "초 재생이 T₄ 고착으로 잠겼습니다 — " +
                "T₄의 축은 «세션 길이»가 아니라 «한 번도 끊기지 않은 구간»이어야 합니다(§19-2). " +
                "OnRunSeconds가 OFF에서 0으로 리셋되는지 확인하십시오.");
            Assert.IsTrue(sim.IsDancing, "긴 재생 끝에 춤이 꺼져 있습니다. 마지막 사유: " + sim.LastReason);
        }

        /// <summary>곡 사이 실측 공백(1.09초)은 T₂(3.0초)에 못 미치므로 춤을 <b>끊지 않는다</b>.</summary>
        [Test]
        public void 곡_사이_공백은_춤을_끊지_않는다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.MinimumHoldSeconds + AudioReactiveDancePolicy.StartDelaySeconds, FineTick);
            Assert.IsTrue(sim.IsDancing, "선행 조건 실패 — 춤이 시작되지 않았습니다.");

            sim.Forget();
            sim.Run(false, AudioReactiveDancePolicy.MeasuredTrackGapSeconds, FineTick);

            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Stop),
                "실측 곡 사이 공백에 춤이 끊겼습니다 — T₂가 그 공백보다 커야 합니다. 사유: " + sim.LastReason);
            Assert.IsTrue(sim.IsDancing, "공백 뒤 위상이 Dancing이 아닙니다.");
        }

        // ====================================================================
        // ③ T₃ 최소 유지
        // ====================================================================

        /// <summary>음악이 춤 시작 직후 멈춰도 <b>최소 T₃</b>는 유지한다 — 그러지 않으면 모션 전이가
        /// 잘린다(§19-4가 그 대가를 «최대 8.0초 정적 속에서 춤»으로 이미 값 매겼다).</summary>
        [Test]
        public void 시작_직후_음악이_끊겨도_최소유지_전에는_안_멈춘다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StartDelaySeconds + FineTick, FineTick);
            Assert.IsTrue(sim.IsDancing, "선행 조건 실패 — 춤이 시작되지 않았습니다.");

            sim.Forget();
            // T₂는 이미 넘겼지만 T₃는 아직인 지점까지만 흘린다.
            // ★ 1.0초의 여유를 남긴다 — 경계를 딱 맞추면 float 누산 오차가 이 테스트의 판정을
            //   가르게 되고, 그러면 이 테스트가 재는 것이 규칙이 아니라 산술 오차가 된다.
            float beforeHold = AudioReactiveDancePolicy.MinimumHoldSeconds
                               - AudioReactiveDancePolicy.ReleaseDelaySeconds - 1.0f;
            Assert.Greater(beforeHold, AudioReactiveDancePolicy.ReleaseDelaySeconds,
                "이 테스트는 T₃ > 2·T₂를 전제로 짜였습니다 — 값이 바뀌었다면 시나리오를 다시 잡으십시오.");
            sim.Run(false, AudioReactiveDancePolicy.ReleaseDelaySeconds + beforeHold, FineTick);

            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Stop),
                "T₂는 찼지만 T₃(최소 유지)가 아직인데 멈췄습니다 — 에피소드가 중간에 잘립니다. 사유: " + sim.LastReason);

            sim.Forget();
            sim.Run(false, AudioReactiveDancePolicy.MinimumHoldSeconds, FineTick);
            Assert.IsTrue(sim.Saw(AudioReactiveDanceAction.Stop),
                "T₂·T₃가 모두 찼는데도 안 멈췄습니다 — 위 '안 멈춘다'가 '영원히 안 멈춘다'로도 통과하게 됩니다.");
        }

        // ====================================================================
        // ④ 억제 — 집중 모드가 춤보다 우선
        // ====================================================================

        /// <summary>★ <b>억제는 T₃(최소 유지)보다 이긴다.</b> 사용자 확정
        /// <i>"집중모드일때는 춤모션이 아닌 집중모드 모션이 우선"</i>.</summary>
        [Test]
        public void 억제는_최소유지보다_이겨서_즉시_멈춘다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StartDelaySeconds + FineTick, FineTick);
            Assert.IsTrue(sim.IsDancing, "선행 조건 실패 — 춤이 시작되지 않았습니다.");

            sim.Forget();
            sim.Tick(true, FineTick, suppressed: true);

            Assert.AreEqual(AudioReactiveDanceAction.Stop, sim.Seen[0],
                "★ 억제 입력이 참이 된 틱에 멈추지 않았습니다 — 집중 모드가 춤에 졌습니다. 사유: " + sim.LastReason);
            Assert.IsFalse(sim.IsDancing, "억제 뒤에도 위상이 Dancing입니다.");
        }

        /// <summary>★ 억제 중에는 <b>발동하지 않는다</b>. 위 테스트만으로는 «한 번 멈추고 다음 틱에
        /// 다시 시작»이 통과한다 — 그게 정확히 <i>"집중 모드 켰는데 계속 춤춘다"</i>의 모양이다.</summary>
        [Test]
        public void 억제가_계속되는_동안에는_다시_발동하지_않는다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StartDelaySeconds * 10f, FineTick, suppressed: true);

            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Start),
                "★ 억제가 참인 내내 음악이 나왔는데 춤이 발동했습니다. 사유: " + sim.LastReason);
            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Continue), "억제 중에 Continue가 나왔습니다.");

            // 양성 대조 — 억제가 풀리면 (여전히 음악이 나오므로) 발동해야 한다.
            sim.Forget();
            sim.Tick(true, FineTick);
            Assert.AreEqual(AudioReactiveDanceAction.Start, sim.Seen[0],
                "억제가 풀렸는데 발동하지 않습니다 — 위 '발동 안 함'이 '영원히 발동 안 함'으로도 통과합니다. " +
                "사유: " + sim.LastReason);
        }

        // ====================================================================
        // ⑤ Lockout 해제 · 계약 성질
        // ====================================================================

        [Test]
        public void 고착_잠금은_연속_OFF_최소유지_시간으로_풀린다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StuckSignalCeilingSeconds + 1f,
                AudioReactiveDancePolicy.ReferencePollIntervalSeconds);
            Assert.AreEqual(AudioReactiveDancePhase.Lockout, sim.State.Phase, "선행 조건 실패 — 잠기지 않았습니다.");

            // 아직 부족한 OFF — 풀리면 안 된다.
            sim.Run(false, AudioReactiveDancePolicy.LockoutReleaseSeconds - FineTick, FineTick);
            Assert.AreEqual(AudioReactiveDancePhase.Lockout, sim.State.Phase,
                "연속 OFF가 " + AudioReactiveDancePolicy.LockoutReleaseSeconds + "초에 못 미치는데 풀렸습니다.");

            // 채우면 풀린다.
            sim.Run(false, FineTick * 2f, FineTick);
            Assert.AreEqual(AudioReactiveDancePhase.Armed, sim.State.Phase,
                "연속 OFF를 채웠는데도 잠금이 안 풀립니다 — 고착 뒤 영원히 안 춤춥니다. 사유: " + sim.LastReason);
        }

        /// <summary><c>default</c>가 올바른 시작 상태인가. 별도 초기화 함수를 두지 않은 근거다.</summary>
        [Test]
        public void 기본값_상태는_대기_위상이고_아무것도_하지_않는다()
        {
            var fresh = default(AudioReactiveDanceState);
            Assert.AreEqual(AudioReactiveDancePhase.Armed, fresh.Phase);

            AudioReactiveDanceVerdict v = AudioReactiveDancePolicy.Evaluate(fresh, false, FineTick, false);
            Assert.AreEqual(AudioReactiveDanceAction.None, v.Action, v.Reason);
        }

        /// <summary>순수 함수인가 — 같은 입력에 같은 출력이고, 호출이 정적 상태를 남기지 않는다.</summary>
        [Test]
        public void 같은_입력은_언제나_같은_결과를_낸다()
        {
            var state = new AudioReactiveDanceState(AudioReactiveDancePhase.Dancing,
                AudioReactiveDancePolicy.StartDelaySeconds, 0f, AudioReactiveDancePolicy.MinimumHoldSeconds);

            AudioReactiveDanceVerdict a = AudioReactiveDancePolicy.Evaluate(state, true, FineTick, false);
            AudioReactiveDanceVerdict b = AudioReactiveDancePolicy.Evaluate(state, true, FineTick, false);

            Assert.AreEqual(a.Action, b.Action);
            Assert.AreEqual(a.Reason, b.Reason);
            Assert.AreEqual(a.Next.Phase, b.Next.Phase);
            Assert.AreEqual(a.Next.OnRunSeconds, b.Next.OnRunSeconds, 1e-6f);
        }

        /// <summary>음수·NaN 델타가 시계열을 오염시키지 않는가(도메인 리로드·프레임 히치 뒤에 온다).</summary>
        [Test]
        public void 비정상_델타는_누산을_망가뜨리지_않는다()
        {
            var sim = new DanceSim();
            sim.Tick(true, float.NaN);
            sim.Tick(true, -5f);

            Assert.AreEqual(0f, sim.State.OnRunSeconds, 1e-6f,
                "NaN/음수 델타가 누산에 섞였습니다 — T₁·T₄가 무엇을 셌는지 말할 수 없게 됩니다.");
            Assert.IsFalse(sim.Saw(AudioReactiveDanceAction.Start));
        }

        /// <summary>★ 상태기계가 «원 신호»를 정말 원본으로 들고 있는가 — OFF 한 틱에
        /// <c>OnRunSeconds</c>가 0으로 떨어지는지. 이 성질 하나가 §19-1 전체를 떠받친다.</summary>
        [Test]
        public void OFF_한_틱이_ON_연속시간을_0으로_리셋한다()
        {
            var sim = new DanceSim();
            sim.Run(true, AudioReactiveDancePolicy.StartDelaySeconds * 0.9f, FineTick);
            Assert.Greater(sim.State.OnRunSeconds, 0f, "선행 조건 실패 — 누산이 안 됐습니다.");

            sim.Tick(false, FineTick);
            Assert.AreEqual(0f, sim.State.OnRunSeconds, 1e-6f,
                "★ OFF 틱에 OnRunSeconds가 리셋되지 않았습니다. 이게 «병합»이고, 그러면 알림 2회가 " +
                "T₁을 뚫습니다(§19-1).");
        }
    }
}
