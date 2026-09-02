using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"움직이고 있는 캐릭터를 자리 비움으로 재우지 않는다."</b>
    ///
    /// ============================================================================
    /// 무엇이 터졌었나 (2026-09-01)
    /// ============================================================================
    /// 사용자 요청 원문: "캐릭터 움직임도 좀더 부드럽게 변경해야함".
    ///
    /// <see cref="FramePacingPolicy.DecideTier"/>의 Away 판정이 <b>무입력 180초 하나만</b> 보고
    /// 캐릭터가 무엇을 하는지는 보지 않았다. 그래서 다음이 성립했다:
    /// <list type="number">
    /// <item>사용자가 마우스에서 손을 떼고 캐릭터를 <b>구경한다</b>. 이 앱에서 지켜보기는 이탈이
    ///       아니라 <b>기본 액션</b>이다(docs/UX_FLOW.md 2절).</item>
    /// <item>정확히 180초 뒤 Away 진입 -> 프레임 1/4 (60 -> 15fps).</item>
    /// <item>캐릭터는 여전히 걷는 중이다. 보행 사이클 1.35Hz
    ///       (<c>StickmanPoseAnimator.TickWalkPose</c> 실측)면 한 주기가 <b>약 11프레임</b>으로만
    ///       그려진다 -> 무릎 관절이 프레임당 최대 9도씩 튄다.</item>
    /// <item>마우스를 조금만 움직이면 즉시 사라진다 -> 재현 조건이 "3분간 아무것도 하지 않기"라
    ///       아무도 눈치채지 못한 채 오래 살았다.</item>
    /// </list>
    /// 즉 <b>보고 있는 사람 앞에서 프레임을 4분의 1로 깎고 있었다</b> — 적응형 등급의 대전제
    /// ("아무도 보고 있지 않은 시간에만 깎는다")를 어긴 유일한 경로였다.
    ///
    /// ============================================================================
    /// 처방과 그 경계 — 이 파일이 지키는 4개의 불변식
    /// ============================================================================
    /// <list type="number">
    /// <item><b>걷는 중에는 무입력이 아무리 길어도 Away가 아니다.</b> 3분/5분/10분 전부.</item>
    /// <item><b>서 있으면 Away는 그대로 살아 있다.</b> 24시간 상주 앱의 절감 안전장치를 지운 것이
    ///       아니라 조건을 하나 더한 것이다(네거티브 컨트롤이 이걸 증명한다).</item>
    /// <item><b>DisplayOff / Suspended는 이 정정의 대상이 아니다.</b> 화면이 물리적으로 꺼졌거나
    ///       전체화면 게임이 감지된 상태에서는 걷고 있어도 그대로 내려가야 한다 — 전자는 볼 사람이
    ///       없다는 <b>관측된 사실</b>이고, 후자는 CLAUDE.md 원칙 2(비침해)다.</item>
    /// <item><b>멈추면 즉시 재개된다.</b> 걷다가 Idle로 바뀐 뒤에는 같은 무입력 시간에서 다시
    ///       Away가 나와야 한다(등급이 "한번 막히면 영영 안 내려가는" 상태로 굳으면 밤새 60fps다).</item>
    /// </list>
    ///
    /// <para><b>왜 임계값(180초)을 당기는 방향이 아닌가</b>: 같은 날 정반대 제안(무입력 30~60초로
    /// 앞당겨 절감을 키우자)이 있었고 기각됐다. 이 앱에서 무입력은 이탈 신호가 아니라 <b>몰입
    /// 신호일 수 있어서</b>, 임계값을 당기면 위 증상이 <b>더 자주</b> 난다. 고쳐야 할 것은 시간이
    /// 아니라 판정에 빠져 있던 사실("지금 움직이는 중인가")이었다.</para>
    ///
    /// <para>순수 함수만 검사한다 — OS 조회는 플랫폼 구현체에 있고 배치 EditMode에서는 돌지 않는다.
    /// 각 불변식에 <b>네거티브 컨트롤</b>을 붙이는 것이 이 프로젝트 표준이다.</para>
    /// </summary>
    public sealed class AwayTierMotionGuardTests
    {
        // 두 플랫폼 기준값(AdaptiveFramePacingPolicyTests와 같은 상수).
        private const int MacBaseVSync = 2;
        private const int MacBaseTarget = -1;
        private const int WinBaseVSync = 0;
        private const int WinBaseTarget = 60;

        /// <summary>보행 사이클 주파수(Hz). <c>StickmanPoseAnimator.TickWalkPose</c>의 실측 주석에서
        /// 온 값이다("사이클이 1.35Hz여야 할 구간에서 0.94Hz로 돌아 디딤발이 미끄러졌다").</summary>
        private const float GaitCycleHz = 1.35f;

        /// <summary>한 보행 주기를 <b>사람이 연속 동작으로 보려면</b> 최소한 이만큼의 프레임이 필요하다고
        /// 두는 하한. 정확한 지각 임계를 주장하는 값이 아니라, "Away(15fps)의 약 11프레임은 명백히
        /// 모자라고 Active(60fps)의 약 44프레임은 충분하다"는 두 값 사이를 가르는 선이다.</summary>
        private const float MinFramesPerGaitCycle = 24f;

        private static ViewerPresenceSnapshot Presence(bool asleep = false, float idleSeconds = 0f,
            bool lowPower = false, bool onBattery = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPower, onBattery,
                // 이 픽스처들은 세션 잠금 축을 재지 않는다(프레임 페이싱 등급 전용).
                // 잠금 축은 SessionVisibilityPolicyTests가 따로 겨눈다.
                sessionLocked: false);

        /// <summary>"사용자가 손을 떼고 구경만 하고 있다" — 이 앱의 기본 액션.</summary>
        private static ViewerPresenceSnapshot WatchingHandsOff(float seconds) => Presence(idleSeconds: seconds);

        /// <summary>무입력이 Away 문턱을 한참 넘긴 대표 시간들(3분 직후 / 5분 / 10분 / 하룻밤).</summary>
        private static readonly float[] LongIdleSeconds =
        {
            FramePacingPolicy.AwaySeconds + 0.01f,
            FramePacingPolicy.AwaySeconds + 1f,
            300f,    // 5분
            600f,    // 10분
            28800f,  // 8시간 — "잊고 켜 둔 밤"
        };

        // ========================================================================
        // 불변식 1 — 걷는 중에는 무입력이 아무리 길어도 Away가 아니다 (이번 수정의 핵심)
        // ========================================================================

        [Test]
        public void 걷는중이면_무입력_3분_5분_10분_어디서도_자리비움으로_내려가지_않는다()
        {
            foreach (float sec in LongIdleSeconds)
            {
                FramePacingTier tier = FramePacingPolicy.DecideTier(
                    WatchingHandsOff(sec), suspendedForFullscreen: false, characterIdle: false);

                Assert.AreEqual(FramePacingTier.Active, tier,
                    $"무입력 {sec}초 — 캐릭터가 움직이는 중인데 Away로 내려갔다. 사용자가 손을 떼고 " +
                    "구경만 하는 시간은 이 앱에서 이탈이 아니라 몰입이다(UX_FLOW 2절). 15fps에서 " +
                    "걷기 한 주기는 약 11프레임뿐이라 무릎이 눈에 보이게 튄다.");
            }
        }

        [Test]
        public void 자리비움_문턱_직전과_직후_어디서도_걷는중이면_등급이_바뀌지_않는다()
        {
            // 경계에서 값이 갈리면 "3분 지나면 갑자기 뚝 끊긴다"는 신고가 그대로 돌아온다.
            // 문턱을 사이에 두고 등급이 **연속**임을 못박는다.
            float t = FramePacingPolicy.AwaySeconds;
            foreach (float sec in new[] { t - 1f, t - 0.01f, t, t + 0.01f, t + 1f })
            {
                Assert.AreEqual(FramePacingTier.Active,
                    FramePacingPolicy.DecideTier(WatchingHandsOff(sec), false, characterIdle: false),
                    $"무입력 {sec}초 — 문턱 근처에서 등급이 갈렸다.");
            }
        }

        [Test]
        public void 움직이는_동안에는_한_보행주기의_프레임_예산이_하한_아래로_내려가지_않는다()
        {
            // 이번 수정의 **근거 자체**를 숫자로 잠근다. 등급 이름이 아니라 "실제로 몇 장이 그려지는가"로
            // 확인하므로, 나중에 누가 등급 체계를 갈아엎어도 이 성질이 유지되는지가 검사된다.
            foreach (float sec in LongIdleSeconds)
            {
                FramePacingTier tier = FramePacingPolicy.DecideTier(
                    WatchingHandsOff(sec), false, characterIdle: false);
                FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                    tier, WinBaseVSync, WinBaseTarget, lowPowerMode: false);

                // 실효 제출(= targetFrameRate / renderFrameInterval)로 센다. 2026-09-01부터 절감이
                // 두 손잡이 중 어느 쪽으로도 표현될 수 있어 TargetFrameRate만 보면 과대평가된다.
                float framesPerCycle = plan.EffectiveTargetFps / GaitCycleHz;
                Assert.GreaterOrEqual(framesPerCycle, MinFramesPerGaitCycle,
                    $"무입력 {sec}초, 등급 {tier}: 보행 한 주기가 {framesPerCycle:F1}프레임으로만 그려진다.");
            }
        }

        [Test]
        public void 네거티브컨트롤_같은_계산이_자리비움등급에서는_실제로_하한을_깬다()
        {
            // 위 테스트가 "항상 참인 단언"이 아님을 보이는 대조군 = 신고된 증상의 재현이다.
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, WinBaseVSync, WinBaseTarget, lowPowerMode: false);
            float framesPerCycle = away.EffectiveTargetFps / GaitCycleHz;

            Assert.Less(framesPerCycle, MinFramesPerGaitCycle,
                "대조군 전제 실패 — Away 등급이 원래 보행에 모자라야 위 테스트가 의미를 가진다.");
            Assert.AreEqual(11f, framesPerCycle, 1f,
                "문서에 적은 '약 11프레임'과 실제 계산이 어긋났다 — 둘 중 하나를 고쳐라.");
        }

        // ========================================================================
        // 불변식 2 — 서 있으면 Away는 그대로 살아 있다 (절감을 지운 것이 아니다)
        // ========================================================================

        [Test]
        public void 네거티브컨트롤_캐릭터가_서있으면_같은_무입력에서_실제로_자리비움이다()
        {
            foreach (float sec in LongIdleSeconds)
            {
                Assert.AreEqual(FramePacingTier.Away,
                    FramePacingPolicy.DecideTier(WatchingHandsOff(sec), false, characterIdle: true),
                    $"무입력 {sec}초 — 캐릭터가 서 있는데도 Away가 안 나온다. 이번 수정은 조건을 " +
                    "하나 더한 것이지 24시간 상주 절감을 지운 것이 아니다.");
            }
        }

        [Test]
        public void 걷다가_멈추면_같은_무입력_시간에서_자리비움_판정이_재개된다()
        {
            // "한 번 막히면 영영 안 내려간다"가 되면 밤새 60fps다. 판정은 상태를 갖지 않는 순수
            // 함수이므로, 같은 관측에서 characterIdle만 뒤집으면 즉시 갈려야 한다.
            ViewerPresenceSnapshot p = WatchingHandsOff(600f);

            Assert.AreEqual(FramePacingTier.Active,
                FramePacingPolicy.DecideTier(p, false, characterIdle: false), "걷는 중");
            Assert.AreEqual(FramePacingTier.Away,
                FramePacingPolicy.DecideTier(p, false, characterIdle: true),
                "멈춘 뒤에도 Active로 남으면 '걷다가 한 번 막히면 영영 안 내려간다'는 뜻이다.");
        }

        [Test]
        public void 정적등급_경로는_이번_수정에_영향을_받지_않는다()
        {
            // Calm은 원래부터 characterIdle을 요구했다. 회귀 확인.
            Assert.AreEqual(FramePacingTier.Calm,
                FramePacingPolicy.DecideTier(Presence(idleSeconds: 5f), false, characterIdle: true));
            Assert.AreEqual(FramePacingTier.Active,
                FramePacingPolicy.DecideTier(Presence(idleSeconds: 5f), false, characterIdle: false));
        }

        // ========================================================================
        // 불변식 3 — DisplayOff / Suspended는 이 정정의 대상이 아니다
        // ========================================================================

        [Test]
        public void 걷는중이어도_화면이_꺼지면_여전히_화면꺼짐등급이다()
        {
            // ★ 리더 지시 명시 사항: DisplayOff는 그대로 둔다. 화면이 꺼진 것은 **관측된 사실**이라
            //   캐릭터가 걷든 말든 볼 수 있는 사람이 물리적으로 없다 — 거기서 부드러움은 의미가 없다.
            foreach (float sec in new[] { 0f, 5f, FramePacingPolicy.AwaySeconds + 1f })
            {
                Assert.AreEqual(FramePacingTier.DisplayOff,
                    FramePacingPolicy.DecideTier(Presence(asleep: true, idleSeconds: sec),
                        suspendedForFullscreen: false, characterIdle: false),
                    $"무입력 {sec}초 — 화면이 꺼졌는데도 절감하지 않는다면 이번 수정이 범위를 넘었다.");
            }
        }

        [Test]
        public void 걷는중_화면꺼짐의_손잡이값은_4fps_그대로다()
        {
            // 등급 이름만 맞고 숫자가 달라지는 회귀를 막는다(양 플랫폼 모두).
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(asleep: true, idleSeconds: FramePacingPolicy.AwaySeconds + 1f),
                false, characterIdle: false);

            foreach ((int vsync, int target) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
            {
                FramePacingPlan plan = FramePacingPolicy.BuildPlan(tier, vsync, target, lowPowerMode: false);
                Assert.AreEqual(FramePacingPolicy.DisplayOffTargetFps, plan.TargetFrameRate,
                    $"기준값 vSync={vsync}/target={target}");
                Assert.AreEqual(0, plan.VSyncCount, "화면이 꺼져 있으면 디스플레이 위상 자체가 의미 없다.");
            }
        }

        [Test]
        public void 걷는중이어도_전체화면_게임이_감지되면_여전히_숨김등급이다()
        {
            // CLAUDE.md 원칙 2(비침해) — 숨겨져 있는 캐릭터의 부드러움을 위해 남의 게임 프레임을
            // 갉아먹지 않는다.
            Assert.AreEqual(FramePacingTier.Suspended,
                FramePacingPolicy.DecideTier(WatchingHandsOff(FramePacingPolicy.AwaySeconds + 1f),
                    suspendedForFullscreen: true, characterIdle: false));

            Assert.AreEqual(FramePacingTier.Suspended,
                FramePacingPolicy.DecideTier(Presence(idleSeconds: 0f),
                    suspendedForFullscreen: true, characterIdle: false),
                "무입력이 짧아도 숨김은 유지돼야 한다.");
        }

        // ========================================================================
        // 불변식 4 — 다른 입력 축(UI 홀드 / 관측 실패 / 저전력)과 섞이지 않는다
        // ========================================================================

        [Test]
        public void 걷는중_자리비움_금지는_UI홀드_유무와_무관하다()
        {
            // 홀드는 Calm만 이기는 장치다. 이번 조건이 홀드에 얹혀 있으면 창을 닫는 순간 증상이
            // 되살아난다 — 두 축이 독립임을 못박는다.
            foreach (bool held in new[] { true, false })
            {
                Assert.AreEqual(FramePacingTier.Active,
                    FramePacingPolicy.DecideTier(WatchingHandsOff(600f), false,
                        characterIdle: false, uiInteractionActive: held),
                    $"UI홀드={held}");
            }
        }

        [Test]
        public void UI홀드는_여전히_자리비움을_이기지_못한다()
        {
            // 기존 불변식 회귀 확인: 창을 열어 둔 채 자리를 비우고 캐릭터도 서 있으면 Away다.
            Assert.AreEqual(FramePacingTier.Away,
                FramePacingPolicy.DecideTier(WatchingHandsOff(FramePacingPolicy.AwaySeconds + 1f),
                    false, characterIdle: true, uiInteractionActive: true),
                "'잊고 열어 둔 창' 하나가 밤새 절감을 무력화하면 안 된다.");
        }

        [Test]
        public void 관측이_실패하면_걷든_서있든_활성이다()
        {
            // "모르면 내려가지 않는다" — 이번 수정이 이 규칙을 흐리지 않았음을 확인.
            Assert.AreEqual(FramePacingTier.Active,
                FramePacingPolicy.DecideTier(default, false, characterIdle: false));
            Assert.AreEqual(FramePacingTier.Active,
                FramePacingPolicy.DecideTier(default, false, characterIdle: true));
        }

        [Test]
        public void 저전력_감쇄는_등급과_별개라_이번_수정에_영향받지_않는다()
        {
            // 저전력 경로는 DecideTier가 아니라 ShouldApplyLowPowerDownshift다. 걷는 중이어도
            // OS 저전력이 켜져 있으면 Active가 한 칸 낮아지는 기존 동작은 그대로여야 한다
            // (사용자가 OS에서 명시적으로 켠 의사표시라 존중한다).
            ViewerPresenceSnapshot p = Presence(idleSeconds: 600f, lowPower: true, onBattery: true);
            Assert.IsTrue(FramePacingPolicy.ShouldApplyLowPowerDownshift(p, uiInteractionActive: false));

            FramePacingTier tier = FramePacingPolicy.DecideTier(p, false, characterIdle: false);
            Assert.AreEqual(FramePacingTier.Active, tier);
            Assert.AreEqual(30,
                FramePacingPolicy.BuildPlan(tier, WinBaseVSync, WinBaseTarget, lowPowerMode: true)
                    .EffectiveTargetFps,
                "저전력 감쇄까지 사라졌다면 이번 수정이 범위를 넘었다.");
        }

        [Test]
        public void 기존_3인자_판정과_4인자_판정은_홀드없음에서_여전히_같다()
        {
            // 시그니처가 둘이라 한쪽만 고치는 사고가 이 파일의 수정에서 가장 흔하다.
            foreach (bool idle in new[] { true, false })
            {
                foreach (float sec in new[] { 0f, 1f, 5f, FramePacingPolicy.AwaySeconds - 1f,
                    FramePacingPolicy.AwaySeconds + 1f, 600f })
                {
                    ViewerPresenceSnapshot p = Presence(idleSeconds: sec);
                    Assert.AreEqual(
                        FramePacingPolicy.DecideTier(p, false, idle),
                        FramePacingPolicy.DecideTier(p, false, idle, uiInteractionActive: false),
                        $"idle={idle}, 무입력={sec}초");
                }
            }
        }

        // ========================================================================
        // 배선(정적 스캔) — 정책 문장이 실제로 그렇게 쓰여 있는가
        //
        // 위 단언들은 전부 순수 함수의 반환값만 본다. 그것만으로도 규칙은 잠기지만, "왜 이 조건이
        // 여기 있는지"를 모르는 다음 사람이 Away 줄에서 characterIdle을 지우면 위 테스트가
        // 실패하면서도 **어디를 되돌려야 하는지**는 알려주지 못한다. 아래 두 개가 그 화살표다.
        // ========================================================================

        private static string ReadPolicySource()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "ViewerPresence.cs");
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 자리비움_판정줄이_캐릭터정지를_함께_읽는다()
        {
            string source = ReadPolicySource();
            // 4인자 오버로드의 **본문**에서만 찾는다(같은 파일 위쪽 문서 문단에 걸리지 않게).
            int decide = source.IndexOf(
                "bool suspendedForFullscreen, bool characterIdle, bool uiInteractionActive)",
                StringComparison.Ordinal);
            Assert.Greater(decide, 0, "DecideTier 4인자 오버로드가 사라졌거나 시그니처가 바뀌었다 — 이 테스트를 갱신하라.");

            int away = source.IndexOf("AwaySeconds && characterIdle", decide, StringComparison.Ordinal);
            Assert.Greater(away, decide,
                "Away 판정이 더 이상 characterIdle을 AND로 읽지 않는다 — 2026-09-01 '구경 중 15fps' " +
                "버그가 그대로 되살아난다. 근거는 FramePacingPolicy.AwaySeconds 문서에 있다.");
        }

        [Test]
        public void 화면꺼짐_판정은_캐릭터정지를_읽지_않는다()
        {
            // 이번 수정이 DisplayOff까지 번지면 화면이 꺼진 밤 내내 캐릭터가 걷는 동안 60fps가 된다
            // (24시간 상주 앱에서 가장 비싼 종류의 회귀다).
            string source = ReadPolicySource();
            int line = source.IndexOf("if (presence.Valid && presence.DisplayAsleep)", StringComparison.Ordinal);
            Assert.Greater(line, 0, "DisplayOff 판정줄이 사라졌다 — 이 테스트를 갱신하라.");

            int eol = source.IndexOf('\n', line);
            string statement = source.Substring(line, eol - line);
            Assert.IsFalse(statement.Contains("characterIdle"),
                "DisplayOff 판정에 characterIdle이 섞여 들어왔다 — 화면이 꺼진 것은 관측된 사실이라 " +
                "캐릭터가 무엇을 하든 볼 사람이 물리적으로 없다.");
        }
    }
}
