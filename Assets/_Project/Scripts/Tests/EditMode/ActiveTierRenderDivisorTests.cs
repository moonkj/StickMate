using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <b>활성 등급 렌더 분주</b>(<see cref="FramePacingPolicy.DefaultActiveDivisor"/>)의 계약.
    ///
    /// ============================================================================
    /// 이 파일이 지키던 것(2026-09-07 초입) — <b>"손잡이는 있고, 기본값은 안 켜져 있다"</b>
    /// ★★ 그 결정은 같은 날 안에 사용자 본인이 대체했다 — 아래 "판정 뒤집힘" 절.
    /// ============================================================================
    /// 2026-09-07 GPU 라운드에서 "Active 등급도 30fps로 상한을 걸자"는 제안이 왔다.
    /// <b>절감은 실측으로 참이다</b>(아래 실측). 처음에는 <b>켜는 것은 코더가 할 결정이 아니다</b>로
    /// 판단해 손잡이만 만들고 기본값은 그대로 뒀다:
    ///
    /// <list type="number">
    /// <item><see cref="FramePacingTier.Active"/> 문서에 <b>"여기는 절대 건드리지 않는다
    ///   (2026-08-31 사용자 확정: 움직일 때는 60fps)"</b>가 적혀 있었다. 사용자가 닫은 문이었다.</item>
    /// <item><b>Active는 "우리 창을 만지는 중"이 아니다.</b> <see cref="FramePacingPolicy.DecideTier"/>의
    ///   기본 반환값이라 <b>캐릭터가 걷는 모든 시간</b>이 여기 들어간다(자율 배회 실측:
    ///   Active 3.15초 중 걷기 2.75초 = 87%). 즉 이 분주는 UI가 아니라 <b>걷기</b>에 걸린다.</item>
    /// <item><c>AwayTierMotionGuardTests</c>가 <b>보행 한 주기 24프레임</b>을 하한으로 잠가 뒀고,
    ///   분주 2는 22.2프레임이라 그 아래였다. 그 하한은 사용자 요청
    ///   *"캐릭터 움직임도 좀더 부드럽게 변경해야함"*에 대응해 세운 것이었다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★ 판정 뒤집힘(같은 날, 2026-09-07) — 기본값이 실제로 2로 바뀌었다
    /// ============================================================================
    /// 사용자가 Windows 실기(Intel Iris Xe 내장GPU)에서 GPU 사용률 문제(30~90%대)를 직접 겪다가
    /// <c>STICKMATE_ACTIVE_DIVISOR=2</c> 환경변수로 이 손잡이를 스스로 켜서 시험했고, GPU
    /// 사용률이 40%대 위주로 개선되는 것을 실측 확인한 뒤 <b>"움직임이 좀 덜부드럽지만 그냥
    /// 이정도로 만족할께" + "자동적용으로"</b>라고 명시적으로 승인했다 — 2026-08-31 결정을
    /// 대체하는, 같은 사용자의 새 정보에 입각한 결정이다. design-motion의 별도 눈판정은 없었지만,
    /// 실제 판정 주체(사용자 본인)가 실기로 이미 확인했으므로 그것으로 대신한다(리더 판단).
    ///
    /// 그래서 이 라운드의 최종 산출물은 "판정하지 않고 손잡이만"이 아니라 <b>기본값을 2로 올리고,
    /// 되돌리는 쪽에 손잡이(<c>STICKMATE_ACTIVE_DIVISOR=1</c>)를 남긴 것</b>이다 — 방향이
    /// 반대로 바뀌었을 뿐 "재빌드 없이 눈으로 대조할 수 있는 손잡이"라는 관례
    /// (<c>STICKMATE_VSYNC</c>가 세운 것과 같은 것)는 그대로다. 아래의 "기본값 1"을 전제로 한
    /// 테스트들은 전부 "기본값 2"로 재작성됐다 — 각 테스트의 주석에 근거가 있다.
    ///
    /// ============================================================================
    /// 실측 (2026-09-07, ioreg AGXAccelerator "Device Utilization %", 페어드 교차 2회차 × 70초)
    /// ============================================================================
    /// <code>
    ///   기저(앱 없음)                    GPU 0.1% / 3.8%
    ///   제출 59.2장/초 (분주 1)  25.4%     제출 29.8장/초 (분주 2)  14.5%    -> -10.9%p
    ///   제출 59.9장/초 (분주 1)  29.4%     제출 29.8장/초 (분주 2)  16.4%    -> -13.0%p
    /// </code>
    /// ※ <b>코드 변경 없이 쟀다</b> — <c>STICKMATE_FORCE_TIER=Active</c> vs <c>=Calm</c>.
    ///   두 등급의 계획은 <c>renderFrameInterval</c> 하나만 다르므로(아래
    ///   <see cref="분주2의_계획은_정적등급의_계획과_완전히_같다"/>가 이것을 못 박는다),
    ///   <b>Calm이 곧 "분주 2를 건 Active"</b>다. 이 등가성이 깨지면 위 실측이 무효가 되므로
    ///   테스트로 잠근다.
    /// </summary>
    public sealed class ActiveTierRenderDivisorTests
    {
        // 두 플랫폼 기준값(AdaptiveFramePacingPolicyTests / AwayTierMotionGuardTests와 같은 상수).
        private const int MacBaseVSync = 2;
        private const int MacBaseTarget = -1;
        private const int WinBaseVSync = 0;
        private const int WinBaseTarget = 60;

        /// <summary>보행 사이클 주파수(Hz). <c>StickmanPoseAnimator.TickWalkPose</c> 실측 주석에서 온 값이고
        /// <c>AwayTierMotionGuardTests</c>가 쓰는 값과 같다. 아래 <see cref="자리비움_예산이_문서의_약_11프레임과_맞는지_먼저_교정한다"/>가
        /// 이 상수 자체를 알려진 값으로 교정한다(계산기를 만들면 먼저 교정한다 — 저장소 공통 처방).</summary>
        private const float GaitCycleHz = 1.35f;

        // ========================================================================
        // 교정 — 계산기를 알려진 값으로 먼저 맞춘다. 여기가 깨지면 아래 숫자는 전부 폐기다.
        // ========================================================================

        [Test]
        public void 자리비움_예산이_문서의_약_11프레임과_맞는지_먼저_교정한다()
        {
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, WinBaseVSync, WinBaseTarget, lowPowerMode: false);

            Assert.AreEqual(11f, away.EffectiveTargetFps / GaitCycleHz, 1f,
                "보행 예산 계산기가 문서의 '약 11프레임'(Away)과 어긋났다 — 이 파일의 나머지 숫자를 " +
                "믿을 수 없다. GaitCycleHz 또는 Away 손잡이 중 하나가 움직인 것이다.");
        }

        // ========================================================================
        // 불변식 1 — 기본값은 현행 동작을 한 글자도 바꾸지 않는다
        // ========================================================================

        [Test]
        public void 기본값은_2다_2026_09_07_사용자가_실기로_확인하고_직접_승인했다()
        {
            // ★★ 이 테스트는 이전 버전("기본값은 1이다 — 사용자가 닫은 문을 다시 여는 일이다")을
            //   대체한다. 그 문은 코더가 연 것이 아니라 **같은 사용자가 같은 날** 실기로 열었다 —
            //   Windows(Intel Iris Xe)에서 STICKMATE_ACTIVE_DIVISOR=2를 직접 켜 GPU 사용률 개선
            //   (30~90%대 -> 40%대 위주)을 확인한 뒤 "움직임이 좀 덜부드럽지만 그냥 이정도로
            //   만족할께" + "자동적용으로"라고 명시 승인했다. 2026-08-31 결정을 대체하는 새 결정이다.
            //
            // ★ 리터럴 2는 의도적이다(저장소 규칙: 기대값을 프로덕션 상수 자기 자신으로 만들지
            //   마라 — 그러면 상수가 조용히 움직여도 이 테스트가 못 잡는다).
            Assert.AreEqual(2, FramePacingPolicy.DefaultActiveDivisor,
                "활성 등급 기본 분주가 2가 아니다. 2026-09-07 사용자 승인(GPU 실기 확인, " +
                "'자동적용으로')을 되돌리는 변경이라면 그 근거부터 확인하라 — 이 값을 1로 내리면 " +
                "실측된 GPU 절감(약 -43%, 25.4%->14.5%)이 사라진다. 반대로 올리려면 " +
                "MaxActiveDivisor(2) 자체를 먼저 검토해야 한다(그 위는 Away와 예산이 겹친다).");

            Assert.AreEqual(FramePacingPolicy.MaxActiveDivisor, FramePacingPolicy.DefaultActiveDivisor,
                "기본값은 이제 '허용 상한'과 같아야 한다 — 2026-09-07부터 Active 등급은 상한까지 " +
                "절감한다(그 위는 Away 예산과 겹쳐 금지된다).");
        }

        [Test]
        public void 인자를_넘기지_않은_호출과_기본값을_넘긴_호출이_모든_등급에서_같다()
        {
            foreach (FramePacingTier tier in Enum.GetValues(typeof(FramePacingTier)))
            {
                foreach ((int vsync, int target) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
                {
                    foreach (bool lowPower in new[] { false, true })
                    {
                        FramePacingPlan legacy = FramePacingPolicy.BuildPlan(tier, vsync, target, lowPower);
                        FramePacingPlan explicitDefault = FramePacingPolicy.BuildPlan(
                            tier, vsync, target, lowPower,
                            FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.DefaultActiveDivisor);

                        Assert.IsTrue(legacy.SameAs(explicitDefault),
                            $"{tier}/vsync={vsync}/target={target}/저전력={lowPower}: 새 매개변수를 " +
                            "넘기지 않은 기존 호출부의 결과가 바뀌었다 — 이 라운드는 기본 동작을 " +
                            "바꾸지 않기로 한 라운드다.");
                    }
                }
            }
        }

        [Test]
        public void 기본값에서_활성등급은_렌더분주만큼만_줄고_표시기구와_게임루프는_그대로다()
        {
            // ★ 2026-09-07 이전 제목은 "여전히 매 프레임 제출한다"였고 RenderFrameInterval도
            //   리터럴 1로 단언했다 — 기본값이 1이던 시절의 사실이었다. 지금은 기본값이 2라서
            //   RenderFrameInterval도 2가 정상이다. 이 테스트가 실제로 지키는 불변식은 처음부터
            //   그게 아니라 <b>표시 기구(vSyncCount)와 게임 루프(targetFrameRate)는 Active에서
            //   절대 안 바뀐다</b>였다(FramePacingPolicy.BuildPlan 클래스 문서 "설계 원칙" 1번).
            //   그 불변식만 남기고, 렌더 간격은 정책 기본값을 참조한다(리터럴로 베끼면 기본값이
            //   또 바뀔 때 이 테스트만 조용히 낡는다 — CLAUDE.md 규칙).
            foreach ((int vsync, int target) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
            {
                FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                    FramePacingTier.Active, vsync, target, lowPowerMode: false);

                Assert.AreEqual(FramePacingPolicy.DefaultActiveDivisor, plan.RenderFrameInterval,
                    $"vsync={vsync}/target={target}");
                Assert.AreEqual(vsync, plan.VSyncCount, "표시 기구는 그대로다.");
                Assert.AreEqual(target, plan.TargetFrameRate, "게임 루프는 그대로다.");
            }
        }

        // ========================================================================
        // 불변식 2 — 켰을 때 무슨 일이 일어나는가(절감과 대가를 둘 다 숫자로 남긴다)
        // ========================================================================

        [Test]
        public void 분주2의_계획은_정적등급의_계획과_완전히_같다()
        {
            // ★ 이 등가성이 이 라운드 실측의 근거다. 깨지면 "STICKMATE_FORCE_TIER=Calm으로 쟀다"는
            //   측정이 통째로 무효가 된다(다른 것을 잰 것이 되므로).
            foreach ((int vsync, int target) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
            {
                FramePacingPlan cappedActive = FramePacingPolicy.BuildPlan(
                    FramePacingTier.Active, vsync, target, lowPowerMode: false,
                    FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.MaxActiveDivisor);
                FramePacingPlan calm = FramePacingPolicy.BuildPlan(
                    FramePacingTier.Calm, vsync, target, lowPowerMode: false);

                Assert.IsTrue(cappedActive.SameAs(calm),
                    $"vsync={vsync}/target={target}: 분주 2를 건 Active가 Calm과 다른 손잡이를 낸다 — " +
                    "'Calm 등급으로 대신 측정했다'는 이 라운드의 실측 절차가 무효가 된다.");
                Assert.AreEqual(FramePacingTier.Active, cappedActive.Tier, "등급 이름까지 바뀌면 안 된다.");
            }
        }

        [Test]
        public void 분주2는_보행_한주기_프레임을_정확히_절반으로_줄인다()
        {
            // 대가를 숫자로 남긴다 — 나중에 누가 기본값을 올릴 때 이 숫자를 보고 결정하도록.
            FramePacingPlan full = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, WinBaseVSync, WinBaseTarget, false,
                FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.MinActiveDivisor);
            FramePacingPlan capped = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, WinBaseVSync, WinBaseTarget, false,
                FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.MaxActiveDivisor);

            float budgetFull = full.EffectiveTargetFps / GaitCycleHz;
            float budgetCapped = capped.EffectiveTargetFps / GaitCycleHz;

            Assert.AreEqual(44.4f, budgetFull, 0.5f, "현행 보행 예산이 문서의 44.4프레임과 어긋났다.");
            Assert.AreEqual(22.2f, budgetCapped, 0.5f, "분주 2 보행 예산이 문서의 22.2프레임과 어긋났다.");
            Assert.AreEqual(budgetFull / 2f, budgetCapped, 0.01f);

            // ★ 그리고 그 값은 Away(신고되어 고쳐진 값)와 현행의 정확히 중간이다 — "절반쯤 되돌리는
            //   것"이라는 뜻이고, 그래서 이것이 눈에 보이는지 아닌지는 사람이 판정해야 한다.
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, WinBaseVSync, WinBaseTarget, false);
            Assert.AreEqual(away.EffectiveTargetFps * 2f, capped.EffectiveTargetFps, 0.01f,
                "분주 2의 제출이 Away의 2배가 아니다 — 위 대조 서술이 낡았다.");
        }

        // ========================================================================
        // 불변식 3 — 범위 밖 값이 화면을 얼리거나 신고된 구간으로 되돌아가지 않는다
        // ========================================================================

        [Test]
        public void 상한은_2이고_그_위는_잘린다()
        {
            // 3 이상이면 보행 한 주기가 14.8프레임 이하 = Away(11.1)와 같은 구간이다.
            // 그 구간은 이미 "무릎이 눈에 보이게 튄다"로 신고되어 고쳐진 값이라, 계측 변수로도
            // 되살아나지 못하게 막는다.
            Assert.AreEqual(2, FramePacingPolicy.MaxActiveDivisor,
                "활성 분주 상한이 2가 아니다 — 그 위는 신고되어 고쳐진 Away 구간과 겹친다.");

            foreach (int requested in new[] { 3, 4, 8, 999 })
            {
                Assert.AreEqual(FramePacingPolicy.MaxActiveDivisor, FramePacingPolicy.BuildPlan(
                        FramePacingTier.Active, WinBaseVSync, WinBaseTarget, false,
                        FramePacingPolicy.DefaultStillDivisor, requested).RenderFrameInterval,
                    $"요청 {requested}가 상한으로 잘리지 않았다.");
            }
        }

        [Test]
        public void 음수나_0은_현행_동작으로_떨어진다()
        {
            foreach (int requested in new[] { 0, -1, -999, int.MinValue })
            {
                Assert.AreEqual(FramePacingPolicy.MinActiveDivisor, FramePacingPolicy.BuildPlan(
                        FramePacingTier.Active, MacBaseVSync, MacBaseTarget, false,
                        FramePacingPolicy.DefaultStillDivisor, requested).RenderFrameInterval,
                    $"요청 {requested}에서 렌더 간격이 1 아래로 갔다 — 그러면 화면이 영영 안 그려진다.");
            }
        }

        [Test]
        public void 저전력_감쇄와_곱해지지_않는다()
        {
            // BuildPlan은 `lowPowerMode && divisor == 1`일 때만 한 칸 낮춘다. 활성 분주가 이미 2면
            // 그 가지에 들어가지 않아야 한다 — 곱하면 걷는 중에 15fps가 되어 Away와 같아진다.
            FramePacingPlan plan = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, WinBaseVSync, WinBaseTarget, lowPowerMode: true,
                FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.MaxActiveDivisor);

            Assert.AreEqual(FramePacingPolicy.MaxActiveDivisor, plan.RenderFrameInterval,
                "저전력 감쇄가 활성 분주와 곱해졌다 — 배터리 노트북에서 걷기가 15fps가 된다.");

            // 네거티브 컨트롤 — activeDivisor를 명시적으로 MinActiveDivisor(1)로 고정해 저전력
            // 감쇄가 **실제로** 걸리는지 격리해서 잰다.
            // ★ 2026-09-07: 여기서 인자를 생략하면 DefaultActiveDivisor가 이제 2라서, 저전력 감쇄가
            //   실제로 걸렸는지와 무관하게 항상 2가 나와 이 대조군이 조용히 무의미해진다 — 그
            //   함정을 피하려고 이 인자를 명시한다.
            FramePacingPlan defaultPlan = FramePacingPolicy.BuildPlan(
                FramePacingTier.Active, WinBaseVSync, WinBaseTarget, lowPowerMode: true,
                FramePacingPolicy.DefaultStillDivisor, FramePacingPolicy.MinActiveDivisor);
            Assert.AreEqual(2, defaultPlan.RenderFrameInterval,
                "대조군 전제 실패 — 저전력 감쇄 자체가 사라졌다면 위 테스트가 아무것도 재지 않는다.");
        }

        [Test]
        public void 활성_분주는_다른_등급을_흔들지_않는다()
        {
            foreach (int activeDivisor in new[] { 1, 2, 3, 0, -5 })
            {
                foreach (FramePacingTier tier in new[]
                         {
                             FramePacingTier.Calm, FramePacingTier.Still, FramePacingTier.Away,
                             FramePacingTier.Suspended, FramePacingTier.DisplayOff,
                         })
                {
                    foreach ((int vsync, int target) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
                    {
                        FramePacingPlan baseline = FramePacingPolicy.BuildPlan(tier, vsync, target, false);
                        FramePacingPlan withKnob = FramePacingPolicy.BuildPlan(
                            tier, vsync, target, false, FramePacingPolicy.DefaultStillDivisor, activeDivisor);

                        Assert.IsTrue(baseline.SameAs(withKnob),
                            $"활성 분주 {activeDivisor}가 {tier}(vsync={vsync})를 흔들었다 — " +
                            "분주 인자를 잘못 배선하면 다른 등급까지 같이 바뀐다.");
                    }
                }
            }
        }

        [Test]
        public void 어떤_활성_분주에서도_렌더간격은_1과_2_사이다()
        {
            foreach (int requested in new[] { int.MinValue, -3, 0, 1, 2, 3, 60, int.MaxValue })
            {
                int interval = FramePacingPolicy.BuildPlan(
                    FramePacingTier.Active, MacBaseVSync, MacBaseTarget, false,
                    FramePacingPolicy.DefaultStillDivisor, requested).RenderFrameInterval;

                Assert.GreaterOrEqual(interval, FramePacingPolicy.MinActiveDivisor, $"요청 {requested}");
                Assert.LessOrEqual(interval, FramePacingPolicy.MaxActiveDivisor, $"요청 {requested}");
            }
        }

        // ========================================================================
        // 불변식 4 — 배선(정적 스캔). 정책만 고치고 거버너가 안 넘기면 손잡이가 죽은 채로 초록불이다
        // ========================================================================

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 거버너가_활성_분주를_읽어_BuildPlan에_넘긴다()
        {
            string source = ReadScript("Platform", "FramePacing.cs");

            // ★ 양성 대조 먼저 — 이 프로브가 실제로 문자열을 찾아낼 수 있는지 보인다.
            //   (없는 문자열을 0건으로 읽는 것과 프로브가 죽은 것은 출력이 똑같이 생긴다.)
            Assert.IsTrue(source.Contains("_stillDivisor"),
                "대조군 전제 실패 — 이미 있는 형제 손잡이조차 못 찾는다면 이 프로브가 죽은 것이다.");
            Assert.IsFalse(source.Contains("_thisIdentifierMustNotExist_"),
                "음성 대조 실패 — 없는 문자열이 발견됐다. 이 파일의 정적 스캔 결과 전부 무효다.");

            // ★ 상수의 **값**이 아니라 **식별자**를 찾는다. 값으로 찾으면 XML 문서 주석과 로그 문장에
            //   같은 문자열이 있어서 배선이 없어도 통과한다(실제로 이 파일이 그렇게 한 번 속았다).
            StringAssert.Contains(nameof(FramePacing.ActiveDivisorEnvironmentVariableName), source,
                "계측 환경변수를 읽는 배선이 없다 — 재빌드 없는 A/B가 아무 효과도 내지 않는다.");

            int build = source.IndexOf("FramePacingPolicy.BuildPlan(tier,", StringComparison.Ordinal);
            Assert.Greater(build, 0, "등급별 BuildPlan 호출이 사라졌다 — 이 테스트를 갱신하라.");

            int semicolon = source.IndexOf(';', build);
            Assert.Greater(semicolon, build, "BuildPlan 호출 구문을 파싱하지 못했다.");
            string call = source.Substring(build, semicolon - build);
            StringAssert.Contains("_activeDivisor", call,
                "BuildPlan 호출에 _activeDivisor를 넘기지 않는다 — 필드만 있고 배선이 없으면 " +
                "환경변수를 줘도 아무 일도 일어나지 않고, 테스트는 전부 초록불이다.");
        }

        [Test]
        public void 기본값이_아닌_회차는_로그가_스스로_그렇다고_말한다()
        {
            // 이 저장소 서명 사고("실패한 측정과 성공한 측정이 똑같이 생겼다")를 막는 장치다.
            // 계측 회차의 로그가 제품 기본값 회차의 로그와 구분되지 않으면, 나중에 그 로그로 낸
            // 판정을 되살릴 수 없다.
            string source = ReadScript("Platform", "FramePacing.cs");

            int guard = source.IndexOf("_activeDivisor != FramePacingPolicy.DefaultActiveDivisor",
                StringComparison.Ordinal);
            Assert.Greater(guard, 0,
                "기본값과 다를 때만 찍는 가지가 없다 — 기본값 회차의 로그에 없던 문장이 생기거나, " +
                "계측 회차가 제품 회차와 똑같이 생기거나 둘 중 하나다.");

            StringAssert.Contains("제품 기본값이 아닙니다", source,
                "계측 회차임을 로그가 명시하지 않는다.");
        }

        // ========================================================================
        // 불변식 5 — ★ 출하 스위치. 이 한 줄이 «사용자가 무엇을 받는가»를 정한다
        // ========================================================================

        [Test]
        public void 출하_애셋의_활성_분주가_2다_2026_09_07_사용자_승인()
        {
            // ★★ 2026-09-07 이전에는 이 테스트가 "출하 애셋이 1이다"였고, 빨갛게 만드는 유일한
            //    방법이 2로 올리는 것이었다 — **의도적 마찰**이었다. 그 마찰은 같은 날 사용자
            //    본인이 실기로 건넜다: Windows(Intel Iris Xe)에서 STICKMATE_ACTIVE_DIVISOR=2를
            //    직접 켜 GPU 사용률 개선(30~90%대 -> 40%대 위주)을 확인한 뒤 "움직임이 좀
            //    덜부드럽지만 그냥 이정도로 만족할께" + "자동적용으로"라고 명시 승인했다 —
            //    2026-08-31 결정을 대체하는, 같은 사용자의 새 정보에 입각한 결정이다.
            //
            //    이제 이 테스트는 **반대 방향**의 의도적 마찰이다 — 이 값을 1로 되돌리는 것
            //    (사용자 승인을 되돌리는 행위)에 저항을 건다. 되돌리기 전에 확인해야 하는 것:
            //      · GPU  : 위 실기 개선(약 -43%, 실측 2회차 25.4%->14.5%)이 사라진다.
            //               ※ 실제 운용에서 활성 등급 체류는 100%가 아니라 약 44%다(2026-09-07
            //                 실측: 활성 44% / 정적 8% / 정지 48%). 강제 등급 측정값을 운용
            //                 절감으로 그대로 옮겨 적지 마라.
            //      · 대가 : 보행 한 주기가 22.2 -> 44.4프레임으로 돌아간다(부드러움은 좋아진다).
            //      · 근거 : 되돌리려면 사용자에게 위 GPU 개선을 포기하는지 먼저 다시 확인하라.
            string path = Path.Combine(Application.dataPath, "_Project", "Data", "DefaultStickConfig.asset");
            Assert.IsTrue(File.Exists(path), $"애셋을 찾지 못했다: {path}");
            string asset = File.ReadAllText(path);

            // 양성 대조 — 파서가 살아 있는지 형제 키로 먼저 보인다.
            Assert.IsTrue(asset.Contains("windowsTargetFrameRate:"),
                "대조군 전제 실패 — 형제 키조차 못 찾는다면 이 파일 파싱이 죽은 것이다.");

            string key = nameof(Core.StickConfig.activeTierRenderDivisor);
            Assert.IsTrue(asset.Contains($"{key}:"),
                $"출하 애셋에 {key}가 없다 — 애셋이 코드 기본값을 덮으므로 이 손잡이가 " +
                "사용자 기기에서 0으로 직렬화되어 clamp에 기대게 된다(거짓 통과 #9와 같은 형태).");

            int i = asset.IndexOf($"\n  {key}:", StringComparison.Ordinal);
            int start = asset.IndexOf(':', i) + 1;
            int end = asset.IndexOf('\n', start);
            string raw = asset.Substring(start, end - start).Trim();

            Assert.AreEqual(FramePacingPolicy.DefaultActiveDivisor.ToString(), raw,
                "출하 애셋의 활성 등급 렌더 분주가 정책 기본값(2026-09-07부터 2)과 갈라졌다. " +
                "이 값이 1이면 **걷는 동안에도 매 프레임(60fps)으로 그린다** — 2026-09-07 사용자 " +
                "승인(Windows 실기 GPU 확인, '자동적용으로')이 무효화된 채 출하된다는 뜻이다. " +
                "의도한 되돌리기라면 이 단언과 AwayTierMotionGuardTests의 보행 하한을 같은 " +
                "커밋에서 함께 갱신하고, 근거를 커밋 메시지에 남겨라.");
        }

        [Test]
        public void 코드_기본값과_출하_애셋이_같다()
        {
            var config = ScriptableObject.CreateInstance<Core.StickConfig>();
            try
            {
                Assert.AreEqual(FramePacingPolicy.DefaultActiveDivisor, config.activeTierRenderDivisor,
                    "StickConfig의 코드 기본값이 정책 기본값과 갈라졌다 — 애셋 없이 뜨는 경로" +
                    "(테스트·초기 부팅)와 출하 경로가 서로 다른 fps로 돈다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void 환경변수_이름은_문서와_운영_스크립트가_의존하는_공개_계약이다()
        {
            // ★ 리터럴이 의도적이다. 이 이름은 코드 바깥(실기 A/B 절차·클래스 문서·운영 메모)이
            //   그대로 타이핑하는 값이라, 바뀌면 그쪽이 조용히 무효가 된다.
            Assert.AreEqual("STICKMATE_ACTIVE_DIVISOR", FramePacing.ActiveDivisorEnvironmentVariableName,
                "계측 환경변수 이름이 바뀌었다 — 이 이름을 적어 둔 문서와 절차를 함께 고쳐라.");
        }

        [Test]
        public void 거버너는_애셋값을_읽고_환경변수가_그_위를_덮는다()
        {
            string source = ReadScript("Platform", "FramePacing.cs");

            int clamp = source.IndexOf("_activeDivisor = Mathf.Clamp(", StringComparison.Ordinal);
            Assert.Greater(clamp, 0, "활성 분주를 읽는 곳이 사라졌다.");
            int semi = source.IndexOf(';', clamp);
            Assert.Greater(semi, clamp, "대입 구문을 파싱하지 못했다.");
            string expr = source.Substring(clamp, semi - clamp);

            // ★ 니들을 문자열 리터럴이 아니라 **심볼 이름**에서 만든다. 앞선 실패가 정확히 그 함정이었다 —
            //   프로덕션은 상수 식별자를 쓰는데 테스트가 상수의 «값»을 찾고 있었다(CLAUDE.md:
            //   테스트에 프로덕션 식별자를 문자열로 베끼지 않는다).
            string envSymbol = nameof(FramePacing.ActiveDivisorEnvironmentVariableName);
            string cfgSymbol = nameof(Core.StickConfig.activeTierRenderDivisor);

            StringAssert.Contains(cfgSymbol, expr,
                "거버너가 애셋 값을 읽지 않는다 — 그러면 애셋을 고쳐도 출하 거동이 안 바뀌고, " +
                "위 «출하 스위치» 테스트가 아무것도 지키지 못한다.");
            StringAssert.Contains(envSymbol, expr,
                "환경변수 덮어쓰기가 사라졌다 — 재빌드 없는 실기 A/B가 막힌다.");

            // 순서 계약: 환경변수가 **기본값 자리**에 애셋을 넣는 형태여야 애셋이 A/B를 막지 않는다.
            Assert.Less(expr.IndexOf(envSymbol, StringComparison.Ordinal),
                expr.IndexOf(cfgSymbol, StringComparison.Ordinal),
                "애셋 값이 환경변수보다 먼저 읽힌다 — 그러면 애셋이 계측 덮어쓰기를 이긴다.");
        }
    }
}
