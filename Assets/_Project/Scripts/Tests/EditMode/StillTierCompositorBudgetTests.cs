using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"사용자가 업무 중이어도, 캐릭터가 서 있으면 제출을 줄인다."</b>
    ///
    /// ============================================================================
    /// 무엇을 고쳤나 (2026-09-01 컴포지터 라운드)
    /// ============================================================================
    /// 사용자 Windows 실기 측정: <c>StickMate 30% + dwm 30~40%</c> (GPU). 원인이 <b>두 개</b>이고
    /// 그중 dwm 쪽이 이 라운드의 대상이다. 이미 확정된 사실은 다시 다투지 않는다:
    /// <list type="bullet">
    /// <item>투명 always-on-top 오버레이는 레거시 BitBlt 스왑체인이 <b>구조적으로 강제</b>된다.</item>
    /// <item>DWM은 투명 창이 하나라도 있으면 바탕화면 <b>전체</b>를 다시 합성한다 — 창을 작게 만드는
    ///       것은 이미 기각됐다(면적 무관, macOS 실측).</item>
    /// <item>컴포지터 비용은 <b>제출 횟수에 비례</b>한다(macOS 실측: ACTIVE-OFF=+12.09%p,
    ///       AWAY-OFF=+3.06%p, 비율 0.25 = 코드상 제출비와 일치, 고정항 ~0).</item>
    /// </list>
    /// 즉 남은 레버는 <b>제출 횟수</b> 하나다. 그런데 사용자 지시는 "기본 60fps 유지"다.
    ///
    /// <para><b>구멍은 등급 판정 조건에 있었다.</b> <see cref="FramePacingTier.Calm"/>은
    /// <c>characterIdle AND 최근 2초 무입력</c>을 요구했다. 사용자는 <b>업무 중</b>이라 입력이
    /// 끊이지 않았고, 그래서 두 번째 조건이 하루 종일 한 번도 성립하지 않았다 — 캐릭터가 가만히
    /// 서 있는 시간에도 초당 60장을 제출했다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 지키는 5개의 불변식
    /// ============================================================================
    /// <list type="number">
    /// <item><b>업무 중(무입력 0초)이어도 캐릭터가 오래 서 있으면 Still이다.</b> 이 한 줄이
    ///   신고 상황의 재현이자 수정 확인이다. 네거티브 컨트롤: 걷는 중이면 같은 관측에서 Active.</item>
    /// <item><b>Still은 더 확실한 관측을 이기지 못한다.</b> DisplayOff / Suspended / Away / UI홀드
    ///   전부 Still보다 우선한다(우선순위가 뒤집히면 각각 비침해 위반·절감 무력화가 된다).</item>
    /// <item><b>Windows에서 게임 루프를 늦추지 않는다.</b> 보는 사람이 있는 등급은 이제 양 플랫폼
    ///   모두 <c>renderFrameInterval</c>만 나눈다. <c>targetFrameRate</c>를 나누면 입력/커서 폴링
    ///   주기까지 반이 되어 신고 "기어 설정창조차 클릭하면 렉"이 되살아난다.</item>
    /// <item><b>보는 사람이 없는 등급의 숫자는 한 글자도 바뀌지 않았다.</b> Away/Suspended/DisplayOff는
    ///   예전대로 루프까지 늦춘다(회귀 잠금).</item>
    /// <item><b>배선이 실제로 존재한다.</b> 정책만 고치고 호출부가 없으면 전부 초록불인 채 버그가 산다
    ///   — 이 저장소가 이미 겪은 실패 유형이라 정적 스캔으로 못박는다.</item>
    /// </list>
    ///
    /// <para><b>왜 "제출을 줄여도 안 보인다"고 말할 수 있나(수치 근거)</b>: 1080p에서 45픽셀/월드유닛,
    /// 캐릭터 배율 0.75 기준으로 Idle 구간에 남는 움직임은 호흡 진폭 0.012유닛 = <b>0.4픽셀</b>
    /// (0.8Hz), 호흡 팔각도 ±1.5도 = 손끝 <b>0.3픽셀</b>, 눈동자 커서 추적 최대 오프셋 0.09유닛 =
    /// <b>편도 3픽셀</b>이다. 총량이 수 픽셀이라 15fps에서도 프레임당 변화가 1픽셀 남짓이다.
    /// 이 숫자들은 아래 <see cref="정지구간의_움직임은_전부_수픽셀_규모다"/>가 소스에서 직접 읽어
    /// 검증한다 — 주석이 낡으면 테스트가 깨진다.</para>
    /// </summary>
    public sealed class StillTierCompositorBudgetTests
    {
        // 두 플랫폼 기준값(AdaptiveFramePacingPolicyTests와 같은 상수).
        private const int MacBaseVSync = 2;
        private const int MacBaseTarget = -1;
        private const int WinBaseVSync = 0;
        private const int WinBaseTarget = 60;

        private static ViewerPresenceSnapshot Presence(bool asleep = false, float idleSeconds = 0f,
            bool lowPower = false, bool onBattery = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPower, onBattery);

        /// <summary>"사용자가 업무 중이다" — 방금 키를 눌렀거나 마우스를 움직였다.
        /// 이 관측이 신고 상황의 전부다(그래서 Calm의 전제가 성립하지 않았다).</summary>
        private static ViewerPresenceSnapshot WorkingRightNow() => Presence(idleSeconds: 0f);

        private static FramePacingTier Decide(in ViewerPresenceSnapshot p, bool idle, bool still,
            bool suspended = false, bool held = false)
            => FramePacingPolicy.DecideTier(p, suspended, idle, held, still);

        // ========================================================================
        // 불변식 1 — 업무 중이어도 캐릭터가 오래 서 있으면 제출을 줄인다 (이번 수정의 핵심)
        // ========================================================================

        [Test]
        public void 업무중_무입력0초여도_캐릭터가_오래_서있으면_정지등급이다()
        {
            Assert.AreEqual(FramePacingTier.Still,
                Decide(WorkingRightNow(), idle: true, still: true),
                "★ 신고 상황 그대로다 — 사용자가 키보드를 두드리는 중이고 캐릭터는 서 있다. " +
                "여기서 Active가 나오면 dwm이 초당 60번 바탕화면 전체를 다시 합성한다.");
        }

        [Test]
        public void 네거티브컨트롤_같은_관측에서_걷는중이면_활성이다()
        {
            // 위 테스트가 "항상 참인 단언"이 아님을 보인다. 판정을 가르는 것은 오직 캐릭터 상태다.
            Assert.AreEqual(FramePacingTier.Active,
                Decide(WorkingRightNow(), idle: false, still: false),
                "걷는 중에는 60fps여야 한다(사용자 확정: 움직일 때는 60fps).");
        }

        [Test]
        public void 정지등급은_관측이_실패해도_성립한다()
        {
            // Still은 OS 관측을 필요로 하지 않는 유일한 절감 등급이다 — 캐릭터 상태는 추정이 아니라
            // 우리가 직접 아는 사실이기 때문이다. 관측 실패 시 "모르면 안 내려간다" 규칙의 예외이며,
            // 그 예외가 안전한 이유는 이 등급이 화면을 얼리지 않기 때문이다(최악 15fps).
            Assert.AreEqual(FramePacingTier.Still, Decide(default, idle: true, still: true));
            Assert.AreEqual(FramePacingTier.Active, Decide(default, idle: false, still: false),
                "네거티브 컨트롤 — 걷는 중이면 관측 실패에서도 Active 그대로다.");
        }

        [Test]
        public void 짧게_서있으면_아직_정지등급이_아니다()
        {
            // still=false(문턱 미달)면 Still로 가지 않는다. 무입력이 짧으니 Calm도 아니다 -> Active.
            Assert.AreEqual(FramePacingTier.Active,
                Decide(WorkingRightNow(), idle: true, still: false),
                "히스테리시스(StillDwellSeconds)를 무시하고 곧바로 내려가면 걷기 사이의 순간 정지마다 " +
                "제출률이 튄다.");
        }

        // ========================================================================
        // 불변식 2 — Still은 더 확실한 관측을 이기지 못한다
        // ========================================================================

        [Test]
        public void 정지등급은_화면꺼짐을_이기지_못한다()
        {
            Assert.AreEqual(FramePacingTier.DisplayOff,
                Decide(Presence(asleep: true), idle: true, still: true),
                "화면이 꺼진 것은 관측된 사실이다 — 4fps가 맞다.");
        }

        [Test]
        public void 정지등급은_전체화면_숨김을_이기지_못한다()
        {
            Assert.AreEqual(FramePacingTier.Suspended,
                Decide(WorkingRightNow(), idle: true, still: true, suspended: true),
                "CLAUDE.md 원칙 2(비침해).");
        }

        [Test]
        public void 정지등급은_자리비움을_이기지_못한다()
        {
            Assert.AreEqual(FramePacingTier.Away,
                Decide(Presence(idleSeconds: FramePacingPolicy.AwaySeconds + 1f), idle: true, still: true),
                "자리비움은 게임 루프까지 늦추는 더 깊은 절감이다 — Still이 이기면 밤새 손해다.");
        }

        [Test]
        public void 정지등급은_UI홀드에게_진다()
        {
            Assert.AreEqual(FramePacingTier.Active,
                Decide(WorkingRightNow(), idle: true, still: true, held: true),
                "정보창/부채꼴메뉴를 만지는 중에는 캐릭터가 얼마나 오래 서 있었든 60fps다.");
        }

        [Test]
        public void 등급_숫자는_깊이_순서와_일치한다()
        {
            // enum 값으로 체류 시간 배열을 인덱싱하므로(FramePacing.TierSeconds) 순서가 곧 계약이다.
            Assert.Less((int)FramePacingTier.Active, (int)FramePacingTier.Calm);
            Assert.Less((int)FramePacingTier.Calm, (int)FramePacingTier.Still);
            Assert.Less((int)FramePacingTier.Still, (int)FramePacingTier.Away);
        }

        // ========================================================================
        // 불변식 3 — Windows에서 게임 루프를 늦추지 않는다 (이번 라운드의 두 번째 수정)
        // ========================================================================

        [Test]
        public void 보는사람이_있는_등급은_양_플랫폼_모두_게임루프를_건드리지_않는다()
        {
            foreach (FramePacingTier tier in new[]
                     { FramePacingTier.Active, FramePacingTier.Calm, FramePacingTier.Still })
            {
                FramePacingPlan win = FramePacingPolicy.BuildPlan(tier, WinBaseVSync, WinBaseTarget, false);
                Assert.AreEqual(WinBaseTarget, win.TargetFrameRate,
                    $"{tier}: Windows targetFrameRate가 내려갔다 — 입력/커서 폴링 주기까지 같이 나뉜다.");
                Assert.AreEqual(0, win.VSyncCount, $"{tier}: Windows는 vsync를 끈 채로 유지한다.");

                FramePacingPlan mac = FramePacingPolicy.BuildPlan(tier, MacBaseVSync, MacBaseTarget, false);
                Assert.AreEqual(MacBaseVSync, mac.VSyncCount,
                    $"{tier}: macOS vSyncCount를 바꾸면 전환 순간 위상이 다시 잡혀 튄다.");
                Assert.AreEqual(MacBaseTarget, mac.TargetFrameRate, $"{tier}: macOS targetFrameRate");
            }
        }

        [Test]
        public void 정지등급의_절감은_렌더간격으로만_표현된다()
        {
            FramePacingPlan win = FramePacingPolicy.BuildPlan(
                FramePacingTier.Still, WinBaseVSync, WinBaseTarget, false);
            Assert.AreEqual(FramePacingPolicy.DefaultStillDivisor, win.RenderFrameInterval);
            Assert.AreEqual(WinBaseTarget / FramePacingPolicy.DefaultStillDivisor, win.EffectiveTargetFps,
                "기본 분주 4 -> 60fps 루프 위의 15장/초 제출.");

            FramePacingPlan mac = FramePacingPolicy.BuildPlan(
                FramePacingTier.Still, MacBaseVSync, MacBaseTarget, false);
            Assert.AreEqual(FramePacingPolicy.DefaultStillDivisor, mac.RenderFrameInterval);
        }

        [Test]
        public void 정지등급_분주는_지정할_수_있고_범위를_벗어나면_잘린다()
        {
            // 실기 A/B(4 vs 8)용 손잡이. 범위를 벗어난 값이 들어와도 화면이 얼지 않아야 한다.
            Assert.AreEqual(8, FramePacingPolicy.BuildPlan(
                FramePacingTier.Still, WinBaseVSync, WinBaseTarget, false, 8).RenderFrameInterval);

            Assert.AreEqual(FramePacingPolicy.MaxStillDivisor, FramePacingPolicy.BuildPlan(
                FramePacingTier.Still, WinBaseVSync, WinBaseTarget, false, 999).RenderFrameInterval,
                "상한을 넘기면 호흡 주기(0.8Hz)의 표본이 한 주기당 8장 아래로 떨어진다.");

            Assert.AreEqual(1, FramePacingPolicy.BuildPlan(
                FramePacingTier.Still, WinBaseVSync, WinBaseTarget, false, -5).RenderFrameInterval,
                "음수/0이 그대로 들어가면 렌더가 영영 안 돈다.");
        }

        [Test]
        public void 정지등급은_다른_등급의_분주에_영향을_주지_않는다()
        {
            // 분주 인자를 잘못 배선하면 Calm/Away까지 같이 바뀐다.
            foreach (int divisor in new[] { 1, 2, 4, 8 })
            {
                Assert.AreEqual(2, FramePacingPolicy.BuildPlan(
                    FramePacingTier.Calm, WinBaseVSync, WinBaseTarget, false, divisor).RenderFrameInterval,
                    $"분주 {divisor}에서 Calm이 흔들렸다.");
                Assert.AreEqual(15, FramePacingPolicy.BuildPlan(
                    FramePacingTier.Away, WinBaseVSync, WinBaseTarget, false, divisor).TargetFrameRate,
                    $"분주 {divisor}에서 Away가 흔들렸다.");
            }
        }

        // ========================================================================
        // 불변식 4 — 보는 사람이 없는 등급의 숫자는 한 글자도 바뀌지 않았다
        // ========================================================================

        [Test]
        public void 자리비움_전체화면숨김_화면꺼짐의_손잡이는_예전_그대로다()
        {
            // Windows
            FramePacingPlan away = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, WinBaseVSync, WinBaseTarget, false);
            Assert.AreEqual(15, away.TargetFrameRate);
            Assert.AreEqual(1, away.RenderFrameInterval, "루프를 늦추는 등급은 렌더 간격을 쓰지 않는다.");

            FramePacingPlan susp = FramePacingPolicy.BuildPlan(
                FramePacingTier.Suspended, WinBaseVSync, WinBaseTarget, false);
            Assert.AreEqual(30, susp.TargetFrameRate);
            Assert.AreEqual(1, susp.RenderFrameInterval);

            // macOS
            FramePacingPlan macAway = FramePacingPolicy.BuildPlan(
                FramePacingTier.Away, MacBaseVSync, MacBaseTarget, false);
            Assert.AreEqual(4, macAway.VSyncCount);
            Assert.AreEqual(2, macAway.RenderFrameInterval);

            FramePacingPlan macSusp = FramePacingPolicy.BuildPlan(
                FramePacingTier.Suspended, MacBaseVSync, MacBaseTarget, false);
            Assert.AreEqual(4, macSusp.VSyncCount);
            Assert.AreEqual(1, macSusp.RenderFrameInterval);

            foreach ((int v, int t) in new[] { (MacBaseVSync, MacBaseTarget), (WinBaseVSync, WinBaseTarget) })
            {
                FramePacingPlan off = FramePacingPolicy.BuildPlan(FramePacingTier.DisplayOff, v, t, false);
                Assert.AreEqual(FramePacingPolicy.DisplayOffTargetFps, off.TargetFrameRate);
                Assert.AreEqual(0, off.VSyncCount);
            }
        }

        [Test]
        public void 어떤_등급_어떤_분주에서도_화면이_얼지_않는다()
        {
            // 이 프로젝트가 render-on-demand(변화 없으면 아예 안 그림)를 채택하지 않은 이유이기도 하다:
            // 어떤 신호를 놓쳐도 최악이 "느리게 그려진다"여야 하고 "멈춘다"이면 안 된다.
            foreach (FramePacingTier tier in Enum.GetValues(typeof(FramePacingTier)))
            {
                foreach (int divisor in new[] { -1, 0, 1, 4, 8, 100 })
                {
                    foreach (bool lowPower in new[] { false, true })
                    {
                        FramePacingPlan mac = FramePacingPolicy.BuildPlan(
                            tier, MacBaseVSync, MacBaseTarget, lowPower, divisor);
                        Assert.GreaterOrEqual(mac.RenderFrameInterval, 1, $"{tier}/{divisor}/mac");
                        Assert.LessOrEqual(mac.VSyncCount, 4, $"{tier}/{divisor}/mac — Unity 유효 범위 0..4");

                        FramePacingPlan win = FramePacingPolicy.BuildPlan(
                            tier, WinBaseVSync, WinBaseTarget, lowPower, divisor);
                        Assert.GreaterOrEqual(win.RenderFrameInterval, 1, $"{tier}/{divisor}/win");
                        Assert.GreaterOrEqual(win.EffectiveTargetFps, 4,
                            $"{tier}/{divisor}/win — 4fps 아래로 내려가면 깨어남 폴링이 늦어진다.");
                    }
                }
            }
        }

        // ========================================================================
        // 불변식 5 — 배선(정적 스캔). 정책만 고치고 호출부가 없으면 전부 초록불인 채 버그가 산다
        // ========================================================================

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 거버너가_정지문턱을_실제로_계산해_판정에_넘긴다()
        {
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("_idleDwellSeconds >= StillDwellSeconds"),
                "정지 지속 시간을 StillDwellSeconds와 비교하는 곳이 사라졌다 — Still 등급이 영영 " +
                "성립하지 않아 이 라운드의 절감이 통째로 죽는다(테스트는 전부 초록불인 채로).");

            int decide = source.IndexOf("FramePacingPolicy.DecideTier(", StringComparison.Ordinal);
            Assert.Greater(decide, 0, "DecideTier 호출이 사라졌다.");
            Assert.IsTrue(source.IndexOf("characterStill", decide, StringComparison.Ordinal) > 0,
                "DecideTier에 characterStill을 넘기지 않는다 — 5인자 오버로드를 쓰지 않으면 " +
                "Still이 절대 나오지 않는다.");

            Assert.IsTrue(source.Contains("_stillDivisor"),
                "BuildPlan에 분주를 넘기는 배선이 사라졌다 — 환경변수 A/B가 아무 효과도 내지 않는다.");
        }

        [Test]
        public void 정지에서_이동으로_바뀌는_순간_폴링을_기다리지_않는다()
        {
            // 복귀가 다음 폴링(최대 0.2초)까지 밀리면 걷기 시작의 첫 3프레임이 15fps로 그려진다.
            // 그것이 정확히 이 프로젝트가 이미 한 번 신고받은 증상("부드럽지 않다")이다.
            string source = ReadScript("Platform", "FramePacing.cs");

            int governor = source.IndexOf("private static void TickAdaptiveGovernor", StringComparison.Ordinal);
            Assert.Greater(governor, 0, "TickAdaptiveGovernor가 사라졌다 — 이 테스트를 갱신하라.");

            int edge = source.IndexOf("wasIdle && !characterIdle", governor, StringComparison.Ordinal);
            Assert.Greater(edge, governor,
                "정지 -> 이동 엣지에서 즉시 재평가하는 분기가 사라졌다.");

            int poll = source.IndexOf("_presencePollTimer += dt;", governor, StringComparison.Ordinal);
            Assert.Greater(poll, edge,
                "엣지 처리가 폴링 타이머 뒤에 있다 — 그러면 즉시가 아니라 최대 0.2초 뒤에 복귀한다.");
        }

        [Test]
        public void 실효_제출이_로그에_찍힌다()
        {
            // 사용자가 실기에서 효과를 확인할 유일한 수단이다. 설정값이 아니라 **결과**를 세야 한다
            // (이 프로젝트는 Screen.msaaSamples가 거짓말하는 것을 이미 두 번 겪었다).
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("Time.renderedFrameCount"),
                "실제로 그려/제출한 장수를 세지 않는다 — renderFrameInterval이 조용히 무시돼도 " +
                "사용자는 '고쳤는데 그대로'만 보게 된다.");
            Assert.IsTrue(source.Contains("실효 제출"),
                "5분 요약/A-B 요약에서 실효 제출 표기가 사라졌다.");
        }

        [Test]
        public void 실제_렌더_콜백이라는_독립_계기가_함께_찍힌다()
        {
            // ★ 2026-09-01 계기 정직성 라운드. willCurrentFrameRender는 네이티브 바인딩이 없는
            //   **순수 산술**(Time.frameCount % renderFrameInterval == 0)이라 "실제로 그렸는가"의
            //   증거가 될 수 없다 — 그것으로 거른 표본 수가 interval에 비례하는 것은 동어반복이다.
            //   그래서 계획이 아니라 사건을 세는 세 번째 계기가 반드시 함께 있어야 한다.
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("Camera.onPostRender"),
                "실제 렌더 콜백 계기가 사라졌다 — 그러면 renderedFrameCount가 거짓말할 때 " +
                "그것을 반증할 수단이 이 앱에 하나도 남지 않는다.");
            Assert.IsTrue(source.Contains("실측 렌더 콜백"),
                "요약 로그에서 실측 렌더 콜백 표기가 사라졌다 — 세 계기를 한 줄에 나란히 찍지 않으면 " +
                "사람이 불일치를 놓친다(이 저장소가 실제로 여러 라운드를 날린 방식이다).");
            Assert.IsTrue(source.Contains("cam.targetTexture != null"),
                "오프스크린 카메라를 거르는 가드가 사라졌다 — 초상화 스테이지가 렌더텍스처에 그리면 " +
                "화면에 제출되지 않은 프레임이 '렌더됨'으로 잘못 잡힌다.");
        }

        [Test]
        public void GPU_점유는_msx제출의_곱으로_찍힌다()
        {
            // ★ 이 라운드가 줄이는 것은 ms/프레임이 **아니다** — 한 장을 그리는 비용은 그대로이고
            //   줄어드는 것은 장수다. 그래서 ms만 보면 "안 줄었다"는 잘못된 결론이 나온다.
            //   실기 확인이 그 함정에 빠지지 않도록 로그가 두 값과 그 곱을 한 줄에 같이 찍는다.
            //   (사용자 실측에서 MSAA 4x/2x/0x가 전부 GPU 30%로 같았다 = 비용이 "무엇을 그리는가"가
            //    아니라 "몇 번 제출하는가"에 달려 있다는 반증 불가능한 증거였다.)
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("GPU 점유 추정"),
                "GPU 점유 추정(= ms/프레임 x 제출/초 / 10) 표기가 사라졌다 — 작업 관리자 GPU %와 " +
                "대응하는 유일한 숫자다.");
            Assert.IsTrue(source.Contains("OngoingGpuSampleStride"),
                "상시 GPU 표본 수집이 사라졌다 — A/B 요약은 시작 60초에 한 번 찍고 끝나므로 " +
                "그것만으로는 절감 구간이 얼마나 포함됐는지 알 수 없다.");
            Assert.IsTrue(source.Contains("OnDemandRendering.willCurrentFrameRender"),
                "건너뛴 프레임에서 같은 GPU 표본을 다시 세지 않는 가드가 사라졌다.");
        }

        [Test]
        public void 첫_요약은_사용자가_기다리지_않도록_일찍_나온다()
        {
            // 리더 요청: "사용자가 몇 분 안에 검증할 수 있는 절차". 첫 확인에 5분을 기다리게 하면
            // 실기 검증 루프가 무너진다.
            string source = ReadScript("Platform", "FramePacing.cs");
            Assert.IsTrue(source.Contains("FirstTierSummarySeconds"),
                "첫 요약을 앞당기는 상수가 사라졌다.");
            Assert.IsTrue(source.Contains("_firstSummaryDone"),
                "첫 요약 이후 원래 주기로 돌아가는 배선이 사라졌다 — 24시간 상주 앱에서 주기 로그를 " +
                "늘리면 그 자체가 비용이다.");
        }

        [Test]
        public void 커서추종펫은_커서가_움직이는_동안_홀드를_갱신한다()
        {
            // characterIdle은 "캐릭터 상태가 Idle"일 뿐 "화면이 안 움직인다"가 아니다. 커서 친구 펫은
            // 캐릭터가 서 있어도 커서를 따라 화면을 가로지르므로 스스로 신고해야 한다.
            string source = ReadScript("Interaction", "CharacterPetRenderer.cs");

            int tick = source.IndexOf("private void TickCursorFriend", StringComparison.Ordinal);
            Assert.Greater(tick, 0, "TickCursorFriend가 사라졌다 — 이 테스트를 갱신하라.");

            int hold = source.IndexOf("FramePacing.HoldActiveForInteraction(", tick, StringComparison.Ordinal);
            Assert.Greater(hold, tick,
                "커서 친구가 프레임 페이싱 홀드를 갱신하지 않는다 — Still(15fps)에서 커서를 따라가는 " +
                "펫이 뚝뚝 끊긴다.");

            // 무조건 홀드면 이 펫을 낀 사용자는 절감이 통째로 사라진다 — 속도 조건이 있어야 한다.
            int speedGuard = source.IndexOf("speed > worldPerPoint * CursorHoldSpeedPoints", tick,
                StringComparison.Ordinal);
            Assert.Greater(speedGuard, tick, "속도 문턱 가드가 사라졌다.");
            Assert.Less(speedGuard, hold,
                "홀드가 속도 가드보다 앞이다 — 커서가 멈춰 있어도 하루 종일 60fps를 붙잡는다.");
        }

        [Test]
        public void 깊어지는_전이에만_제동이_걸리고_얕아지는_전이는_즉시다()
        {
            // 사용자 실기 로그에서 Active<->Calm이 4~7초마다 왕복했다. 왕복 자체는 자율 배회의
            // 정상 동작이라 없앨 수 없고, 없애려 하면 캐릭터 상태를 무시하게 되어 절감이 죽는다.
            // 그래서 (A) 왕복을 싸게 만들고(루프 페이스 불변 — 위 불변식 3), (B) 병적인 고속 왕복만
            // 최소 제동으로 흡수한다. 제동이 **얕아지는 방향**에 걸리면 그게 곧 "걷기 시작이 끊긴다"다.
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("TierDescendCooldownSeconds"), "제동 상수가 사라졌다.");

            int guard = source.IndexOf("bool deeper = (int)plan.Tier > (int)_currentTier;",
                StringComparison.Ordinal);
            Assert.Greater(guard, 0,
                "제동이 '더 깊어질 때만'이라는 조건이 사라졌다 — 얕아지는 전이까지 늦추면 " +
                "걷기 시작의 첫 프레임들이 절감 등급으로 그려진다.");

            int exempt = source.IndexOf("FramePacingTier.Suspended", guard, StringComparison.Ordinal);
            Assert.Greater(exempt, guard,
                "전체화면 숨김/화면꺼짐 면제가 사라졌다 — 관측된 사실이자 비침해 원칙(2)이라 " +
                "제동 대상이 아니다.");
        }

        [Test]
        public void 스파이크가_나면_그_순간의_정황이_로그로_남는다()
        {
            // 사용자 실기: p99 150ms / 최대 407ms. 분위수는 "튄다"만 말하고 **왜**는 말하지 않는다.
            // 407ms는 분포가 아니라 단일 사건이므로 그 순간에 정황을 찍지 않으면 영영 못 잡는다.
            string source = ReadScript("Platform", "FramePacing.cs");

            Assert.IsTrue(source.Contains("[프레임스파이크]"), "스파이크 로그가 사라졌다.");

            // 최우선 용의자: 백버퍼 재생성(같은 로그에서 창 폭이 재적용마다 1px씩 줄고 있었다).
            Assert.IsTrue(source.Contains("스왑체인 재생성 유력"),
                "스파이크 순간의 백버퍼 크기 변화 비교가 사라졌다 — 이게 1순위 용의자다.");
            Assert.IsTrue(source.Contains("System.GC.CollectionCount(0)"),
                "GC 증가분 계측이 사라졌다(2순위 용의자).");
            Assert.IsTrue(source.Contains("SpikeRelativeFactor"),
                "기대 프레임 시간 대비 배수 조건이 사라졌다 — 절감 등급(Away 15fps/DisplayOff 4fps)의 " +
                "긴 프레임은 정상이므로 절대값만 보면 밤새 거짓 경보가 쌓인다.");
            Assert.IsTrue(source.Contains("SpikeLogCooldownSeconds"),
                "스파이크 로그 쿨다운이 사라졌다 — 재생성 루프가 돌면 초당 여러 번 찍힌다.");
        }

        // ========================================================================
        // 근거 잠금 — "제출을 줄여도 안 보인다"의 수치가 소스와 어긋나면 깨진다
        // ========================================================================

        [Test]
        public void 정지구간의_움직임은_전부_수픽셀_규모다()
        {
            // 이 라운드의 판단 근거 자체를 소스에서 다시 읽어 확인한다. 누가 호흡 진폭을 10배로
            // 키우면(= 서 있는 캐릭터가 눈에 띄게 움직이면) Still의 전제가 깨지므로 여기서 걸린다.
            string config = ReadScript("Core", "StickConfig.cs");

            float breathAmplitude = ReadFloatField(config, "idleBreathAmplitude");
            float breathHz = ReadFloatField(config, "idleBreathFrequencyHz");

            // 1080p 세로 / (orthographicSize 12 x 2) = 45픽셀/월드유닛 (DockGeometry 문서의 환산과 동일한 형태).
            const float PixelsPerWorldUnit = 1080f / 24f;
            float breathPixels = breathAmplitude * PixelsPerWorldUnit;

            Assert.Less(breathPixels, 1f,
                $"호흡 진폭이 {breathPixels:F2}픽셀이다 — 1픽셀을 넘으면 '서브픽셀이라 안 보인다'는 " +
                "Still 등급의 근거가 무너진다. 진폭을 되돌리거나 StillDwellSeconds/분주를 재검토하라.");
            Assert.Less(breathHz * FramePacingPolicy.MaxStillDivisor, 60f / 4f,
                "호흡 주기당 표본이 너무 적어지는 조합이다(주파수를 올렸다면 분주 상한을 낮춰라).");

            float eyeOffset = StickMate.States.EyeController.MaxSafePupilOffset;
            Assert.Less(eyeOffset * PixelsPerWorldUnit, 6f,
                "눈동자 이동 폭이 6픽셀을 넘으면 커서 추적이 15fps에서 계단으로 읽힐 수 있다.");
        }

        /// <summary>소스에서 <c>public float 이름 = 값;</c> 형태의 기본값을 읽는다(에셋이 아니라
        /// 코드의 기본값을 본다 — 테스트가 씬/에셋 로딩에 의존하지 않게).</summary>
        private static float ReadFloatField(string source, string fieldName)
        {
            string needle = "public float " + fieldName + " = ";
            int i = source.IndexOf(needle, StringComparison.Ordinal);
            Assert.Greater(i, 0, $"{fieldName} 필드를 찾지 못했다 — 이름이 바뀌었으면 이 테스트를 갱신하라.");
            int start = i + needle.Length;
            int end = source.IndexOfAny(new[] { 'f', ';' }, start);
            Assert.Greater(end, start, $"{fieldName} 값을 읽지 못했다.");
            return float.Parse(source.Substring(start, end - start),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
