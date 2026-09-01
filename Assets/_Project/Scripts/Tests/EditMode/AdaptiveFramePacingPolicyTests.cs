using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 적응형 프레임 등급(2026-08-31 2차 성능 라운드)의 <b>판단 규칙</b> 회귀 잠금.
    ///
    /// ============================================================================
    /// 이 테스트가 지키려는 것 — 숫자가 아니라 <b>두 개의 안전 불변식</b>
    /// ============================================================================
    /// 1. <b>"보는 사람이 있으면 표시 동기화 기구를 바꾸지 않는다"</b>
    ///    Active/Calm에서는 <c>vSyncCount</c>가 기준값 그대로여야 하고, 절감은
    ///    <c>renderFrameInterval</c>로만 한다. vSyncCount를 바꾸는 것은 디스플레이 위상 고정 자체를
    ///    다시 잡는 일이라 전환 순간이 튈 수 있는데, 이번 사용자 신고가 하필 "부드럽지 않다"였다.
    ///    <b>이 불변식이 깨지면 그 신고가 그대로 재발한다.</b>
    /// 2. <b>"모르면 절대 내려가지 않는다"</b>
    ///    OS 관측이 실패했거나(Valid=false) 방금 사용자가 입력했으면 무조건 Active다. 절감은 언제나
    ///    "확실히 아무도 안 본다"가 증명됐을 때만 한다.
    ///
    /// 두 불변식 모두 <b>네거티브 컨트롤</b>을 함께 둔다(이 프로젝트 표준) — 규칙을 되돌리면 실제로
    /// 실패하는지가 확인돼야 테스트가 "항상 참인 단언"이 아니다.
    ///
    /// <para>순수 함수만 검사한다. OS 조회(<c>CGDisplayIsAsleep</c> 등)는 플랫폼 구현체에 있고
    /// 배치 모드 EditMode에서는 실행되지 않는다 — 여기서는 관측값을 손으로 만들어 넣는다.</para>
    /// </summary>
    public sealed class AdaptiveFramePacingPolicyTests
    {
        // macOS 기준값(현재 제품 설정): vSyncCount=2(120Hz -> 60fps), targetFrameRate 미사용.
        private const int MacBaseVSync = 2;
        private const int MacBaseTarget = -1;

        // Windows 기준값: vsync를 끄고 targetFrameRate로 제어한다(FramePacing 클래스 문서의 비대칭 표).
        private const int WinBaseVSync = 0;
        private const int WinBaseTarget = 60;

        private static ViewerPresenceSnapshot Presence(bool asleep = false, float idleSeconds = 0f,
            bool lowPower = false, bool onBattery = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPower, onBattery);

        // ========================================================================
        // 등급 판정
        // ========================================================================

        [Test]
        public void 관측이_없으면_항상_활성등급이다()
        {
            // default = Valid:false. 조회 실패/미지원 플랫폼의 폴백 경로.
            FramePacingTier tier = FramePacingPolicy.DecideTier(default, false, characterIdle: true);
            Assert.AreEqual(FramePacingTier.Active, tier,
                "관측이 없으면 캐릭터가 서 있어도 절감하지 않아야 한다(모르면 내려가지 않는다).");
        }

        [Test]
        public void 캐릭터가_서있고_최근입력이_없으면_정적등급이다()
        {
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: 5f), false, characterIdle: true);
            Assert.AreEqual(FramePacingTier.Calm, tier);
        }

        [Test]
        public void 캐릭터가_서있어도_방금_입력이_있었으면_활성등급이다()
        {
            // ★ UI(정보창/부채꼴메뉴/포스트잇)를 만지는 중에 프레임이 떨어지는 것을 막는 장치.
            //   UI 코드와 전혀 결합하지 않고 "최근 입력" 하나로 막는다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: 0.3f), false, characterIdle: true);
            Assert.AreEqual(FramePacingTier.Active, tier);
        }

        [Test]
        public void 캐릭터가_걷는중이면_아무리_오래_무입력이어도_활성등급이다()
        {
            // 사용자 확정 사항: "움직일 때는 60fps". 2026-09-01부터 이 제목은 <b>문자 그대로</b>
            // 참이다 — Away조차 characterIdle을 요구하므로 걷는 동안에는 어떤 무입력 시간에도
            // 내려가지 않는다. 경계 넘어간 쪽의 상세 검증은 AwayTierMotionGuardTests에 있다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: FramePacingPolicy.AwaySeconds - 1f), false, characterIdle: false);
            Assert.AreEqual(FramePacingTier.Active, tier);
        }

        [Test]
        public void 오래_무입력이고_캐릭터도_서있으면_자리비움등급이다()
        {
            // ★ characterIdle: false -> true로 바뀐 것은 2026-09-01 수정 때문이다(테스트 의도는 그대로).
            //   Away는 이제 "무입력 AND 캐릭터 정지"다. 걷는 중 무입력 케이스는 위 테스트와
            //   AwayTierMotionGuardTests가 맡는다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: FramePacingPolicy.AwaySeconds + 1f), false, characterIdle: true);
            Assert.AreEqual(FramePacingTier.Away, tier);
        }

        [Test]
        public void 디스플레이가_꺼져있으면_다른_모든_조건을_이긴다()
        {
            // 우선순위는 "절감이 큰 순서"가 아니라 "확실한 순서"다 — 화면 꺼짐은 관측된 사실이다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(asleep: true, idleSeconds: 0f), suspendedForFullscreen: true, characterIdle: false);
            Assert.AreEqual(FramePacingTier.DisplayOff, tier);
        }

        [Test]
        public void 전체화면_숨김은_자리비움보다_우선한다()
        {
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: 9999f), suspendedForFullscreen: true, characterIdle: true);
            Assert.AreEqual(FramePacingTier.Suspended, tier);
        }

        // ========================================================================
        // 불변식 1 — 보는 사람이 있으면 vSyncCount를 건드리지 않는다
        // ========================================================================

        [Test]
        public void 정적등급은_vSyncCount를_바꾸지_않고_렌더간격만_늘린다()
        {
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                FramePacingTier.Calm, MacBaseVSync, MacBaseTarget, lowPowerMode: false);

            Assert.AreEqual(MacBaseVSync, plan.VSyncCount,
                "★ 사람이 보고 있는 등급에서 vSyncCount를 바꾸면 전환 순간 위상이 다시 잡혀 튈 수 있다.");
            Assert.AreEqual(2, plan.RenderFrameInterval, "60fps 위상 위의 30fps여야 한다.");
        }

        [Test]
        public void 활성등급은_기준값을_한_글자도_바꾸지_않는다()
        {
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, MacBaseVSync, MacBaseTarget, lowPowerMode: false);
            Assert.AreEqual(MacBaseVSync, plan.VSyncCount);
            Assert.AreEqual(MacBaseTarget, plan.TargetFrameRate);
            Assert.AreEqual(1, plan.RenderFrameInterval);
        }

        [Test]
        public void 네거티브컨트롤_보는사람이_없는_등급에서는_vSyncCount를_실제로_바꾼다()
        {
            // 위 두 테스트가 "항상 참인 단언"이 아님을 보이는 대조군: 같은 함수가 등급만 바뀌면
            // vSyncCount를 실제로 올린다. 즉 불변식 1은 "등급에 따라 갈리는 진짜 규칙"이다.
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, MacBaseVSync, MacBaseTarget, lowPowerMode: false);
            Assert.AreNotEqual(MacBaseVSync, away.VSyncCount);
            Assert.AreEqual(4, away.VSyncCount, "vSyncCount 상한은 4다.");
            Assert.AreEqual(2, away.RenderFrameInterval, "상한을 넘는 몫은 렌더 간격으로 넘긴다(2x4 = 15fps).");
        }

        // ========================================================================
        // 손잡이 값 — 기존 동작 보존과 플랫폼 대칭
        // ========================================================================

        [Test]
        public void 전체화면_숨김_등급은_기존_동작과_숫자가_동일하다()
        {
            // 이전 구현은 macOS에서 vSyncCount를 상수 4로, Windows에서 targetFrameRate를 절반으로
            // 내렸다. 리팩터링으로 그 숫자가 바뀌지 않았음을 못박는다.
            FramePacingPlan mac = FramePacingPolicy.BuildPlan(
                FramePacingTier.Suspended, MacBaseVSync, MacBaseTarget, lowPowerMode: false);
            Assert.AreEqual(4, mac.VSyncCount);
            Assert.AreEqual(1, mac.RenderFrameInterval);

            FramePacingPlan win = FramePacingPolicy.BuildPlan(
                FramePacingTier.Suspended, WinBaseVSync, WinBaseTarget, lowPowerMode: false);
            Assert.AreEqual(0, win.VSyncCount, "Windows는 vsync를 끈 채로 유지해야 한다(잔상 라운드 결론).");
            Assert.AreEqual(30, win.TargetFrameRate);
        }

        [Test]
        public void 윈도우_기구에서도_같은_등급이_같은_비율로_내려간다()
        {
            // ★ 2026-09-01 — "비율"은 그대로지만 **어느 손잡이로 표현되는가**가 바뀌었다.
            //   보는 사람이 있는 등급(Calm)은 이제 Windows에서도 게임 루프를 늦추지 않고
            //   renderFrameInterval만 나눈다. 그래서 TargetFrameRate가 아니라 실효 제출을 본다.
            FramePacingPlan calm = FramePacingPolicy.BuildPlan(
                FramePacingTier.Calm, WinBaseVSync, WinBaseTarget, lowPowerMode: false);
            Assert.AreEqual(30, calm.EffectiveTargetFps, "제출은 절반이어야 한다.");
            Assert.AreEqual(WinBaseTarget, calm.TargetFrameRate,
                "★ 게임 루프는 60Hz 그대로여야 한다 — 이걸 나누면 입력/커서 폴링 주기까지 반이 되어 " +
                "신고 '기어 설정창조차 클릭하면 약간 렉걸린듯이 움직임'이 되살아난다.");

            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, WinBaseVSync, WinBaseTarget, lowPowerMode: false);
            Assert.AreEqual(15, away.TargetFrameRate,
                "보는 사람이 없는 등급은 예전대로 루프까지 늦춘다(거기서는 반응성보다 절감이 우선).");
            Assert.AreEqual(1, away.RenderFrameInterval);
        }

        [Test]
        public void 화면꺼짐_등급은_주사율과_무관한_절대값을_쓴다()
        {
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                FramePacingTier.DisplayOff, MacBaseVSync, MacBaseTarget, lowPowerMode: false);
            Assert.AreEqual(0, plan.VSyncCount, "화면이 꺼져 있으면 디스플레이 위상 자체가 의미 없다.");
            Assert.AreEqual(FramePacingPolicy.DisplayOffTargetFps, plan.TargetFrameRate);
            Assert.Greater(plan.TargetFrameRate, 0,
                "0fps로 완전히 멈추면 깨어남 감지 폴링이 Update()에서 돌지 못해 영영 복귀하지 못한다.");
        }

        [Test]
        public void 저전력모드는_활성등급만_한_칸_낮춘다()
        {
            FramePacingPlan active = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, MacBaseVSync, MacBaseTarget, lowPowerMode: true);
            Assert.AreEqual(MacBaseVSync, active.VSyncCount, "저전력이어도 보는 사람이 있으면 기구는 그대로다.");
            Assert.AreEqual(2, active.RenderFrameInterval);

            // 이미 내려간 등급을 또 곱하지는 않는다(곱하면 자리비움이 7fps가 되어 과하다).
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, MacBaseVSync, MacBaseTarget, lowPowerMode: true);
            FramePacingPlan awayNormal = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, MacBaseVSync, MacBaseTarget, lowPowerMode: false);
            Assert.IsTrue(away.SameAs(awayNormal));
        }

        [Test]
        public void 어떤_등급에서도_렌더간격은_1보다_작아지지_않는다()
        {
            foreach (FramePacingTier tier in System.Enum.GetValues(typeof(FramePacingTier)))
            {
                FramePacingPlan plan = FramePacingPolicy.BuildPlan(tier, MacBaseVSync, MacBaseTarget, false);
                Assert.GreaterOrEqual(plan.RenderFrameInterval, 1, $"{tier} 등급");
                Assert.LessOrEqual(plan.VSyncCount, 4, $"{tier} 등급 — Unity vSyncCount 유효 범위는 0..4다.");
                Assert.GreaterOrEqual(plan.VSyncCount, 0, $"{tier} 등급");
            }
        }
    }
}
