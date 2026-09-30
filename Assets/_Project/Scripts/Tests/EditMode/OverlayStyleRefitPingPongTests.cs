using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 사용자 신고 <b>「8초정도 흰화면깜박이 한 5번정도함」</b>(2026-09-30, Windows)의 확정된 기구를
    /// 잠그는 테스트.
    ///
    /// ============================================================================
    /// 이 라운드가 «추측이 아닌» 이유 — 사용자가 보낸 실제 Windows Player.log
    /// ============================================================================
    /// 1차 조사는 재적용 루프가 5회 돌고 회당 비용이 ~1.6초라는 것까지만 확정하고, <b>왜 대부분이
    /// 비싼 경로로 떨어지는가</b>는 후보 3개로 남겼다(커밋 <c>dd72044</c>, 순수 진단 필드만 추가).
    /// 그 뒤 실기 로그가 후보 중 하나를 <b>직접 확정</b>했다:
    /// <list type="number">
    ///   <item><c>전체화면 확장 시도 N/6</c>이 <c>Screen.SetResolution</c>으로 창을 모니터 전체
    ///         (3840x2160) · 원점 (0,0)에 맞춘다.</item>
    ///   <item>그 직후 <c>재적용 N/5</c>가 읽은 <c>GWL_STYLE</c>이
    ///         <b><c>0x94000000</c>(보더리스=True) → <c>0x14CA0000</c>(보더리스=False)</b>로 되돌아가 있다.</item>
    ///   <item>재적용이 <c>ReassignStyleMismatch</c>로 <b>비싼 경로</b>(<c>SetBorderless</c> →
    ///         <c>SetWindowPos</c> 4회)를 탄다 — 이것이 흰 화면 깜박임의 실체다.</item>
    ///   <item>그 <c>SetBorderless</c>가 창을 옛 클라이언트 원점으로 <b>옮기고</b>(실기 150%에서 +(11,45)),
    ///         되돌릴 주체가 필요하므로 재적합이 <b>다시 무장</b>된다.</item>
    ///   <item>재무장된 재적합이 <c>Screen.SetResolution</c>을 또 부른다 → <b>1번으로 되돌아간다.</b></item>
    /// </list>
    /// 로그 실측: 이 고리가 <b>재적용 5회 중 3회</b>(2·4·5번째)를 비싼 경로로 떨어뜨렸고,
    /// <c>SetClickThrough(True)</c> 시점에 같은 형태가 한 번 더 났다.
    ///
    /// ============================================================================
    /// 이 파일이 «순수 규칙 실행»으로 검증하는 이유
    /// ============================================================================
    /// 개발 머신은 macOS이고 Windows 실기 실행이 불가능하다. 그래서 고리의 <b>모든 판정 지점</b>을
    /// 프로덕션 순수 함수(<see cref="OverlayBoundsFitPolicy"/> · <see cref="OverlayStateReapplyPolicy"/>)로
    /// 두고, 이 테스트가 그 함수들을 <b>실제로 실행해</b> 고리가 닫히는지/끊기는지를 센다.
    /// 배선(Enforcer가 그 함수를 정말 부르는가)은 소스 스캔으로 잠근다 — 이 저장소의 확립된 관례다.
    ///
    /// <para><b>거짓 통과 방지</b>: 아래 <see cref="핑퐁_모형은_수정_전_규칙에서_실제로_반복된다"/>가
    /// <b>수정 전 규칙</b>(<see cref="OverlayBoundsFitPolicy.ShouldSetResolution"/> + 무조건 재무장)으로
    /// 같은 모형을 돌려 고리가 <b>닫히는 것</b>을 먼저 보인다. 그 양성 대조가 깨지면 모형이 죽은 것이고,
    /// 그 뒤의 「끊겼다」는 숫자는 전부 무효다.</para>
    /// </summary>
    public class OverlayStyleRefitPingPongTests
    {
        // ────────────────────────────────────────────────────────────────────────
        // 실기 관측값 (프로덕션 상수가 아니다 — 로그/네이티브 원문에서 온 «측정값»이다)
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>실기 로그의 보더리스 스타일 값(<c>WS_VISIBLE | WS_POPUP | WS_CLIPSIBLINGS</c>).</summary>
        private const long ObservedBorderlessStyle = 0x94000000L;

        /// <summary><c>Screen.SetResolution</c> 뒤 되살아난 스타일 값(실기 로그 원문).
        /// <c>WS_VISIBLE | WS_CLIPSIBLINGS | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX</c>.</summary>
        private const long ObservedFramedStyle = 0x14CA0000L;

        /// <summary>프레임→보더리스 전환이 창을 옮기는 양(실기 150% 배율 실측, Enforcer 주석의 +(11,45)).</summary>
        private static readonly Vector2 ObservedBorderlessShift = new Vector2(11f, 45f);

        /// <summary>실기 대상 모니터(로그의 3840x2160 · 원점 (0,0)).</summary>
        private static readonly Rect Monitor = new Rect(0f, 0f, 3840f, 2160f);

        private static string WinEnforcerPath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "Windows",
            "WindowsOverlayStateEnforcer.cs");

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다 — 테스트가 낡았다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>줄 주석을 걷어낸다. <b>"이 코드가 지금 무엇을 하는가"</b>를 물을 때만 쓴다 —
        /// 주석이 그 사실을 말하는 줄은 계수 대상이 아니다(TEAM.md 묶음⑪ 규칙 16).</summary>
        private static string StripLineComments(string src)
            => Regex.Replace(src, @"^[ \t]*//.*$", "", RegexOptions.Multiline);

        /// <summary>에피소드 하드 상한은 Windows 파일의 <c>private const</c>라 테스트가 참조할 수 없다.
        /// 숫자를 베끼지 않고 <b>소스의 리터럴을 실제로 읽는다</b>(<c>OverlayResizeRatchetTests</c>의
        /// <c>MaxSetResolutionCalls</c> 대조와 같은 방식).</summary>
        private static int ReadMaxFullScreenApplyAttempts()
        {
            Match m = Regex.Match(ReadSource(WinEnforcerPath),
                @"MaxFullScreenApplyAttempts\s*=\s*(\d+)");
            Assert.IsTrue(m.Success,
                "Windows판의 MaxFullScreenApplyAttempts 선언을 찾지 못했다 — 이름이 바뀌었다면 " +
                "이 테스트를 함께 갱신해야 한다(숫자를 여기 베끼지 말 것).");
            return int.Parse(m.Groups[1].Value);
        }

        // ════════════════════════════════════════════════════════════════════════
        // 0. 실기 로그의 두 스타일 값이 우리 판정에서 실제로 갈리는가 (계기 교정)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 모형의 전제를 <b>알려진 값으로 먼저 교정</b>한다(TEAM.md 「계산기·검사기는 알려진 값으로
        /// 먼저 교정한다」). 실기 로그의 두 값이 우리 <c>IsBorderless</c>에서 갈리지 않으면,
        /// 아래 모형의 모든 전이가 무의미하다.
        /// </summary>
        [Test]
        public void 실기_로그의_두_스타일_값이_보더리스_판정에서_갈린다()
        {
            Assert.IsTrue(OverlayStateReapplyPolicy.IsBorderless(ObservedBorderlessStyle),
                $"실기 로그가 「보더리스=True」로 찍은 0x{ObservedBorderlessStyle:X}를 우리 판정이 " +
                "보더리스로 보지 않는다 — 모형의 출발점이 틀렸다.");
            Assert.IsFalse(OverlayStateReapplyPolicy.IsBorderless(ObservedFramedStyle),
                $"실기 로그가 「보더리스=False」로 찍은 0x{ObservedFramedStyle:X}를 우리 판정이 " +
                "보더리스로 본다 — 그러면 비싼 경로가 왜 돌았는지 설명할 수 없다.");

            // 이 두 단언이 「항상 참/항상 거짓」인 고장이 아님을 같은 테스트에서 못박는다.
            Assert.AreNotEqual(
                OverlayStateReapplyPolicy.IsBorderless(ObservedBorderlessStyle),
                OverlayStateReapplyPolicy.IsBorderless(ObservedFramedStyle),
                "네거티브 컨트롤: 두 값이 같은 답을 내면 판정이 죽은 것이다.");

            // 그리고 그 스타일 차이가 «비싼 경로»로 이어지는 것까지 실행으로 확인한다.
            Assert.IsTrue(OverlayStateReapplyPolicy.CausesWindowResize(
                    OverlayStateReapplyPolicy.DecideTransparencyReapply(
                        desiredTransparent: true, osStyleReadOk: true,
                        osBorderless: OverlayStateReapplyPolicy.IsBorderless(ObservedFramedStyle),
                        glassOnlyPathAvailable: true)),
                "스타일이 되살아났는데 비싼 경로로 판정되지 않는다 — 실기가 본 깜박임이 설명되지 않는다.");
            Assert.IsFalse(OverlayStateReapplyPolicy.CausesWindowResize(
                    OverlayStateReapplyPolicy.DecideTransparencyReapply(
                        desiredTransparent: true, osStyleReadOk: true,
                        osBorderless: OverlayStateReapplyPolicy.IsBorderless(ObservedBorderlessStyle),
                        glassOnlyPathAvailable: true)),
                "양성 대조: 이미 보더리스면 유리만 다시 걸어야 한다(리사이즈 0회).");
        }

        // ════════════════════════════════════════════════════════════════════════
        // 1. 핑퐁 모형 — 확정된 5개 화살표를 프로덕션 순수 함수로 이어 돌린다
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 모형의 결과. <see cref="ExpensiveEpisodes"/>가 곧 <b>흰 화면 깜박임 횟수</b>다
        /// (재적용 1회 = <c>SetBorderless</c> 1회 = <c>SetWindowPos</c> 4회 = 표면 재생성 4회).
        /// </summary>
        private struct PingPongResult
        {
            public int ExpensiveEpisodes;
            public int SetResolutionCalls;
            public int FitEpisodesArmed;
            public bool StyleBorderlessAtEnd;
        }

        /// <summary>
        /// 확정된 고리를 그대로 돌린다. <b>모든 판정은 프로덕션 순수 함수가 한다</b> — 이 함수가
        /// 직접 판단하는 것은 하나도 없고, 아래 세 「부작용」만 실기 근거로 모형화한다:
        /// <list type="number">
        ///   <item><c>Screen.SetResolution</c> ⇒ 창 스타일이 되살아난다(실기 로그 0x94000000 → 0x14CA0000).</item>
        ///   <item>프레임→보더리스 전환 ⇒ 창이 +(11,45) 이동한다(Enforcer 주석 · 네이티브 원문).</item>
        ///   <item>스타일이 뒤집히면 <b>클라이언트 영역</b>이 프레임 두께만큼 달라지므로
        ///         <c>Screen.width/height</c>가 모니터와 어긋난다(= 해상도 불일치가 다시 선다).
        ///         ★ 이것만은 <b>실기 로그로 개별 확정되지 않은 모형 가정</b>이다. 그래서 아래
        ///         <see cref="핑퐁_차단은_해상도_불일치_가정이_없어도_더_나빠지지_않는다"/>가 이 가정을
        ///         끈 채로도 결론이 뒤집히지 않음을 따로 확인한다.</item>
        /// </list>
        /// </summary>
        /// <param name="pingPongGuardsEnabled">
        /// 참이면 이번 라운드의 두 차단(자기유발 회차의 <c>SetResolution</c> 억제 · 이미 목표 안이면
        /// 재무장 생략)을 쓴다. 거짓이면 <b>수정 전 규칙</b>이다(양성 대조).
        /// </param>
        /// <param name="styleFlipBreaksScreenMatch">위 3번 가정을 켤 것인가.</param>
        /// <param name="startAlreadyBorderless">창 스타일이 <b>이미 보더리스</b>인 상태에서 출발할 것인가.</param>
        /// <param name="glassOnlyPathAvailable">
        /// 유리 전용 경로를 쓸 수 있는가. 거짓이면 <b>이미 보더리스여도</b> 재적용이 라이브러리 전체 경로
        /// (<c>ReassignGlassPathUnavailable</c>)로 가서 <c>SetBorderless</c>가 다시 불린다 — 그 회차가
        /// 차단 ②의 대상이다(스타일 실측 실패도 같은 형태다).
        /// </param>
        /// <param name="ticks">
        /// 돌릴 틱 수. 기본은 재적용 루프의 수명 예산(<see cref="OverlayStateReapplyPolicy.ReapplyAttempts"/>)
        /// 그대로다. 한 화살표만 떼어 볼 때는 1을 준다 — 여러 틱을 돌리면 다른 기구(1px 래칫 누적 등)가
        /// 섞여 «무엇을 잰 값인지»가 흐려진다.
        /// </param>
        private static PingPongResult RunPingPong(bool pingPongGuardsEnabled,
            bool styleFlipBreaksScreenMatch = true, bool startAlreadyBorderless = false,
            bool glassOnlyPathAvailable = true, int ticks = -1)
        {
            if (ticks < 0) ticks = OverlayStateReapplyPolicy.ReapplyAttempts;
            int maxAttempts = ReadMaxFullScreenApplyAttempts();
            float eps = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

            // 기동 구간이 끝난 «정착 직후» 상태에서 출발한다. 실기 로그가 보여 준 그 자리다:
            // 첫 적합이 확정됐고(래치), 그 적합의 SetResolution이 스타일을 이미 되살려 놓았다.
            bool styleBorderless = startAlreadyBorderless;
            // 가정 3을 켠 세계에서는 기동 SetResolution이 이미 스타일을 뒤집어 놓았으므로
            // 클라이언트 영역도 이미 어긋나 있다(= 해상도 불일치가 서 있다). 가정을 끈 세계에서는
            // Screen.width/height가 모니터와 계속 같다.
            bool screenMatchesMonitor = !styleFlipBreaksScreenMatch;
            bool windowedMode = true;                    // 첫 SetResolution이 Windowed로 내려놓았다.
            int setResolutionCalls = 1;                  // 그 첫 호출 1회를 이미 썼다.
            Vector2 windowPos = Monitor.position;
            Vector2 windowSize = Monitor.size;

            bool fitLatched = true;
            int fitAttempts = 0;
            bool selfInducedRefit = false;
            Rect latchedTarget = Monitor;
            bool hasLatchedTarget = true;

            var result = new PingPongResult();

            for (int tick = 0; tick < ticks; tick++)
            {
                // ── (1) 적합 틱 — Update()의 호출 순서와 같다(TickFullScreenBounds가 재적용보다 앞).
                if (!fitLatched && fitAttempts < maxAttempts)
                {
                    fitAttempts++;

                    bool called = pingPongGuardsEnabled
                        ? OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                            screenMatchesMonitor ? (int)Monitor.width : (int)Monitor.width - 22,
                            screenMatchesMonitor ? (int)Monitor.height : (int)Monitor.height - 56,
                            (int)Monitor.width, (int)Monitor.height,
                            windowedMode, eps, setResolutionCalls,
                            OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls, selfInducedRefit)
                        : OverlayBoundsFitPolicy.ShouldSetResolution(
                            screenMatchesMonitor ? (int)Monitor.width : (int)Monitor.width - 22,
                            screenMatchesMonitor ? (int)Monitor.height : (int)Monitor.height - 56,
                            (int)Monitor.width, (int)Monitor.height,
                            windowedMode, eps, setResolutionCalls,
                            OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls);

                    if (called)
                    {
                        setResolutionCalls++;
                        result.SetResolutionCalls++;
                        windowedMode = true;
                        screenMatchesMonitor = true;
                        styleBorderless = false;          // ★ 부작용 1 (실기 로그로 확정)
                    }

                    bool needsMove = OverlayBoundsFitPolicy.ShouldMove(windowPos.x, windowPos.y,
                        Monitor.x, Monitor.y, eps);
                    if (needsMove) windowPos = Monitor.position;

                    bool within = OverlayBoundsFitPolicy.Within(windowPos.x, windowPos.y,
                            Monitor.x, Monitor.y, eps)
                        && OverlayBoundsFitPolicy.Within(windowSize.x, windowSize.y,
                            Monitor.width, Monitor.height, eps);
                    bool wroteThisTick = called || needsMove;

                    if (OverlayBoundsFitPolicy.ShouldLatchFitApplied(within, wroteThisTick))
                    {
                        fitLatched = true;
                        latchedTarget = Monitor;
                        hasLatchedTarget = true;
                        selfInducedRefit = false;
                    }
                }

                // ── (2) 재적용 틱
                TransparencyReapply decision = OverlayStateReapplyPolicy.DecideTransparencyReapply(
                    desiredTransparent: true, osStyleReadOk: true, osBorderless: styleBorderless,
                    glassOnlyPathAvailable: glassOnlyPathAvailable);
                if (!OverlayStateReapplyPolicy.CausesWindowResize(decision)) continue;

                result.ExpensiveEpisodes++;
                if (!styleBorderless)
                {
                    windowPos += ObservedBorderlessShift;   // ★ 부작용 2 (네이티브 원문 · 실기 +(11,45))
                    styleBorderless = true;
                    if (styleFlipBreaksScreenMatch) screenMatchesMonitor = false;   // ★ 모형 가정 3
                }
                else
                {
                    // 이미 보더리스인 창에 다시 불린 회차 — 폭 ±1 흔들기만 남는다.
                    windowSize.x -= OverlayStateReapplyPolicy.BorderlessJiggleWidthLossPixels;
                }

                // ── (3) 재무장 — ReArmFullScreenFitAfterNativeWindowMove()와 같은 순서·같은 가드.
                if (!fitLatched && fitAttempts < maxAttempts) continue;   // 적합 진행 중이면 무장하지 않는다.

                bool withinLatched = hasLatchedTarget
                    && OverlayBoundsFitPolicy.Within(windowPos.x, windowPos.y,
                        latchedTarget.x, latchedTarget.y, eps)
                    && OverlayBoundsFitPolicy.Within(windowSize.x, windowSize.y,
                        latchedTarget.width, latchedTarget.height, eps);
                bool reArm = !pingPongGuardsEnabled
                    || OverlayBoundsFitPolicy.ShouldReArmFitAfterSelfInducedStyleWrite(
                        hasLatchedTarget, withinLatched);
                if (!reArm) continue;

                fitLatched = false;
                fitAttempts = 0;
                selfInducedRefit = true;
                result.FitEpisodesArmed++;
            }

            result.StyleBorderlessAtEnd = styleBorderless;
            return result;
        }

        /// <summary>
        /// ★★★ <b>양성 대조가 먼저다.</b> 수정 전 규칙으로 같은 모형을 돌리면 고리가 <b>닫혀야</b> 한다 —
        /// 닫히지 않으면 모형이 죽은 것이고 아래 「끊겼다」는 숫자를 믿을 수 없다.
        /// </summary>
        [Test]
        public void 핑퐁_모형은_수정_전_규칙에서_실제로_반복된다()
        {
            PingPongResult before = RunPingPong(pingPongGuardsEnabled: false);

            Assert.GreaterOrEqual(before.ExpensiveEpisodes, 3,
                "수정 전 규칙에서 비싼 경로가 3회 미만이면 이 모형은 실기 로그를 재현하지 못한다 " +
                $"(실기 실측: 재적용 5회 중 3회. 모형: {before.ExpensiveEpisodes}회). " +
                "모형이 죽었으므로 다른 테스트의 숫자도 전부 무효다.");
            Assert.GreaterOrEqual(before.FitEpisodesArmed, 1,
                "재무장이 한 번도 안 일어나면 확정된 4번 화살표가 모형에 없는 것이다.");
            Assert.GreaterOrEqual(before.SetResolutionCalls, 1,
                "재무장된 적합이 Screen.SetResolution을 다시 부르지 않으면 5번 화살표가 없는 것이다 — " +
                "그것이 스타일을 되살려 고리를 닫는 유일한 경로다.");
        }

        /// <summary>
        /// ★★★ <b>이 라운드의 본론.</b> 같은 모형·같은 초기 상태에서 두 차단을 켜면 비싼 경로가
        /// <b>정확히 1회</b>(정말 스타일이 어긋나 있던 최초 1회)로 끝나야 한다.
        /// </summary>
        [Test]
        public void 핑퐁_차단을_켜면_비싼_경로가_최초_1회로_끝난다()
        {
            PingPongResult before = RunPingPong(pingPongGuardsEnabled: false);
            PingPongResult after = RunPingPong(pingPongGuardsEnabled: true);

            Assert.AreEqual(1, after.ExpensiveEpisodes,
                "차단을 켰는데 비싼 경로가 1회가 아니다. 1회는 «정말 창 스타일이 어긋나 있던 최초 " +
                $"1회»이고 그것은 막아서는 안 된다(막으면 창틀이 화면에 남는다). 실측: {after.ExpensiveEpisodes}회.");
            Assert.Less(after.ExpensiveEpisodes, before.ExpensiveEpisodes,
                $"차단 전({before.ExpensiveEpisodes})과 후({after.ExpensiveEpisodes})가 같다 — " +
                "차단이 아무것도 안 하고 있다.");
            Assert.AreEqual(0, after.SetResolutionCalls,
                "자기유발 재적합이 Screen.SetResolution을 다시 불렀다 — 그 한 줄이 창 스타일을 " +
                "되살려 고리를 다시 닫는다(확정된 2번 화살표).");
            Assert.IsTrue(after.StyleBorderlessAtEnd,
                "끝났을 때 창이 보더리스가 아니면 사용자 화면에 창틀이 남는다 — 깜박임을 없애려고 " +
                "테두리를 출하하는 거래는 성립하지 않는다.");
        }

        /// <summary>
        /// 위 모형의 유일한 「미확정 가정」(스타일이 뒤집히면 클라이언트 영역이 달라져 해상도 불일치가
        /// 다시 선다)을 <b>끈 채로도</b> 결론이 뒤집히지 않는지 본다. 가정이 틀렸다면 차단이
        /// <b>아무 일도 하지 않아야</b> 하고, 절대로 <b>더 나빠져서는 안 된다</b>.
        /// </summary>
        [Test]
        public void 핑퐁_차단은_해상도_불일치_가정이_없어도_더_나빠지지_않는다()
        {
            PingPongResult before = RunPingPong(pingPongGuardsEnabled: false,
                styleFlipBreaksScreenMatch: false);
            PingPongResult after = RunPingPong(pingPongGuardsEnabled: true,
                styleFlipBreaksScreenMatch: false);

            Assert.LessOrEqual(after.ExpensiveEpisodes, before.ExpensiveEpisodes,
                "모형 가정을 껐을 때 차단이 오히려 깜박임을 늘렸다 — 그러면 이 변경은 다른 환경에서 " +
                "회귀다.");
            Assert.LessOrEqual(after.SetResolutionCalls, before.SetResolutionCalls,
                "차단이 Screen.SetResolution 호출을 늘렸다 — 그것은 표면 재생성을 늘리는 것이다.");
        }

        /// <summary>
        /// 차단 ②만의 몫 — <c>SetBorderless</c>가 <b>이미 보더리스인 창</b>에 다시 불린 회차
        /// (스타일 실측 실패 · 유리 전용 경로 불가)에는 되돌릴 이동이 없으므로 재무장 자체가 없어야 한다.
        /// </summary>
        [Test]
        public void 이미_보더리스인_창에_다시_불린_회차는_재무장하지_않는다()
        {
            // ★ 틱 1개만 돈다 — 여러 틱을 돌리면 별건인 «폭 1px 래칫 누적»이 섞여 3틱째에는
            //   드리프트가 불감대를 넘어 재무장이 «정당하게» 걸린다. 여기서 재는 것은 그 회차가
            //   아니라 「이동이 없는 첫 회차」 하나다.
            PingPongResult before = RunPingPong(pingPongGuardsEnabled: false,
                startAlreadyBorderless: true, glassOnlyPathAvailable: false, ticks: 1);
            PingPongResult after = RunPingPong(pingPongGuardsEnabled: true,
                startAlreadyBorderless: true, glassOnlyPathAvailable: false, ticks: 1);

            // 양성 대조 ①: 이 설정에서 재적용이 정말 «비싼 경로»를 탔는가(안 탔으면 모형이 죽었다).
            Assert.GreaterOrEqual(before.ExpensiveEpisodes, 1,
                "유리 전용 경로 불가 회차가 비싼 경로로 가지 않았다 — 모형이 그 갈래를 못 밟았다.");

            // 양성 대조: 그 상태에서 수정 전 규칙은 «이동이 없는데도» 재무장했다.
            Assert.GreaterOrEqual(before.FitEpisodesArmed, 1,
                "수정 전 규칙이 이 회차에 재무장하지 않았다면 차단 ②가 겨눌 대상이 없는 것이고, " +
                "이 테스트는 죽은 프로브다.");
            Assert.AreEqual(0, after.FitEpisodesArmed,
                "창이 이미 목표 안(폭 ±1은 불감대 안)인데 재무장했다 — 그 에피소드가 " +
                "Screen.SetResolution으로 스타일을 되살려 새 핑퐁을 만든다.");
        }

        // ════════════════════════════════════════════════════════════════════════
        // 2. 규칙 단위 — 막는 것과 막지 않는 것을 각각 못박는다
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void 자기유발_재적합은_해상도_불일치로는_SetResolution을_부르지_않는다()
        {
            Assert.IsFalse(
                OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                    1920, 1080, 3840, 2160,
                    fullScreenModeIsWindowed: true,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                    selfInducedRefit: true),
                "우리 자신의 SetBorderless가 옮긴 창을 되돌리는 회차인데 Screen.SetResolution을 " +
                "부른다 — 그 호출이 창 스타일을 되살려(실기 로그 0x94000000 -> 0x14CA0000) " +
                "다음 재적용을 다시 비싼 경로로 떨어뜨린다.");
        }

        /// <summary>
        /// ★ <b>같은 입력에서 자기유발만 껐을 때 답이 뒤집히는가.</b> 뒤집히지 않으면 위 단언은
        /// 「어차피 false였던 것」을 재확인한 죽은 프로브다.
        /// </summary>
        [Test]
        public void 외부_사건_재적합은_같은_입력에서_여전히_해상도를_고친다()
        {
            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                    1920, 1080, 3840, 2160,
                    fullScreenModeIsWindowed: true,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                    selfInducedRefit: false),
                "모니터를 바꿨거나 해상도가 진짜로 바뀐 경우까지 막으면, 창이 화면에 맞지 않는 채로 " +
                "남는다. 억제는 «우리 자신의 부작용»에만 걸려야 한다.");
        }

        [Test]
        public void 자기유발이어도_창모드_강등은_반드시_복구한다()
        {
            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                    3840, 2160, 3840, 2160,
                    fullScreenModeIsWindowed: false,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                    selfInducedRefit: true),
                "전체화면 계열 모드로 남으면 Unity가 포커스를 잃을 때 창을 z-order 뒤로 보낸다 — " +
                "2026-09-01 신고(엑셀 클릭 시 캐릭터가 창 뒤로 넘어감)가 그대로 재발한다. " +
                "핑퐁 차단이 억제하는 사유는 «해상도 불일치» 하나뿐이다.");
        }

        [Test]
        public void 핑퐁_차단은_수명_상한을_넘겨_호출을_되살리지_않는다()
        {
            foreach (bool selfInduced in new[] { true, false })
            {
                Assert.IsFalse(
                    OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                        1920, 1080, 3840, 2160,
                        fullScreenModeIsWindowed: false,
                        epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                        callsSoFar: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                        maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                        selfInducedRefit: selfInduced),
                    $"수명 상한에 닿았는데 호출을 허용했다(자기유발={selfInduced}) — 상한은 이 라운드에서 " +
                    "올리지도 없애지도 않는다. 판정의 첫 관문은 여전히 ShouldSetResolution이어야 한다.");
            }
        }

        /// <summary>
        /// 자기유발 표시가 <b>불감대 안</b>에서는 아무것도 바꾸지 않아야 한다 — 그 구간에서 두 규칙이
        /// 갈리면 억제가 「해상도 불일치」 밖으로 새어 나간 것이다.
        /// </summary>
        [Test]
        public void 자기유발_표시는_정상_경로의_답을_바꾸지_않는다()
        {
            foreach (bool windowed in new[] { true, false })
            foreach (int screenW in new[] { 3840, 3839, 1920 })
            foreach (int calls in new[] { 0, OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls })
            {
                bool baseline = OverlayBoundsFitPolicy.ShouldSetResolution(
                    screenW, 2160, 3840, 2160, windowed,
                    OverlayBoundsFitPolicy.DefaultEpsilonPixels, calls,
                    OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls);
                bool notSelfInduced = OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(
                    screenW, 2160, 3840, 2160, windowed,
                    OverlayBoundsFitPolicy.DefaultEpsilonPixels, calls,
                    OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls, false);

                Assert.AreEqual(baseline, notSelfInduced,
                    $"자기유발이 아닌데 답이 달라졌다(windowed={windowed}, screenW={screenW}, calls={calls}) — " +
                    "새 규칙은 자기유발 회차 밖에서는 기존 규칙과 글자 그대로 같아야 한다.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // 3. 재무장 규칙 — 진짜 이동은 여전히 무장한다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 진짜_이동이면_보더리스_재적용_뒤에도_재무장한다()
        {
            // 실기 +(11,45)는 불감대 2px를 한참 넘는다 — 그 사실부터 실행으로 확인한다.
            Assert.IsFalse(
                OverlayBoundsFitPolicy.Within(
                    Monitor.x + ObservedBorderlessShift.x, Monitor.y + ObservedBorderlessShift.y,
                    Monitor.x, Monitor.y, OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                "실기에서 관측된 +(11,45) 이동이 불감대 안으로 읽힌다 — 그러면 재무장 조건 자체가 " +
                "성립하지 않아 창이 모니터 원점 +(11,45)에 눌러앉는다(2026-09-02 그 버그).");

            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldReArmFitAfterSelfInducedStyleWrite(
                    latchedTargetKnown: true, geometryWithinLatchedTarget: false),
                "창이 목표 밖으로 옮겨졌는데 재무장하지 않는다 — 되돌릴 주체가 사라진다.");
        }

        [Test]
        public void 이미_목표_안이면_재무장하지_않는다()
        {
            Assert.IsFalse(
                OverlayBoundsFitPolicy.ShouldReArmFitAfterSelfInducedStyleWrite(
                    latchedTargetKnown: true, geometryWithinLatchedTarget: true),
                "되돌릴 것이 없는데 재무장하면 재적합 에피소드가 한 번 더 돌고, 그 에피소드가 " +
                "창 스타일을 되살려 다음 재적용을 비싼 경로로 떨어뜨린다.");

            // 폭 ±1 흔들기는 불감대 안이라 «목표 안»으로 읽혀야 한다(그 회차가 차단 ②의 대상이다).
            Assert.IsTrue(
                OverlayBoundsFitPolicy.Within(
                    Monitor.width - OverlayStateReapplyPolicy.BorderlessJiggleWidthLossPixels,
                    Monitor.height, Monitor.width, Monitor.height,
                    OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                "SetBorderless의 폭 ±1 흔들기가 불감대 밖으로 읽힌다 — 그러면 매 회차 재무장이 되살아난다.");
        }

        [Test]
        public void 목표를_모르면_예전처럼_무장한다()
        {
            foreach (bool within in new[] { true, false })
            {
                Assert.IsTrue(
                    OverlayBoundsFitPolicy.ShouldReArmFitAfterSelfInducedStyleWrite(
                        latchedTargetKnown: false, geometryWithinLatchedTarget: within),
                    "확정 목표를 모르는 상태에서 무장을 생략하면, 네이티브가 옮긴 창을 되돌릴 주체가 " +
                    "없어진다. 모를 때는 고치는 쪽이 이 저장소의 일관된 태도이고, 그 경우 동작은 " +
                    "이 라운드 전과 같아야 한다.");
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // 4. 배선 — 규칙이 있어도 Enforcer가 안 부르면 아무 의미가 없다
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void Windows_Enforcer가_핑퐁_차단_규칙을_실제로_부른다()
        {
            string src = StripLineComments(ReadSource(WinEnforcerPath));

            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldSetResolutionForFitAttempt(", src,
                "적합 틱이 옛 규칙을 그대로 부르고 있다 — 자기유발 회차 억제가 배선되지 않았다.");
            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldReArmFitAfterSelfInducedStyleWrite(", src,
                "재무장이 조건 없이 돌아갔다 — 이미 목표 안인 회차까지 재적합 에피소드를 만든다.");
            StringAssert.Contains("_selfInducedRefit", src,
                "자기유발 표시가 없으면 두 차단이 구별할 근거가 없다.");
        }

        /// <summary>
        /// <b>세우는 곳은 하나, 내리는 곳은 외부 사건 전부 + 확정.</b> 하나라도 빠지면
        /// 「모니터를 바꿨는데 해상도를 안 고친다」는 새 버그가 된다.
        /// </summary>
        [Test]
        public void 자기유발_표시는_외부_사건에서_반드시_내려간다()
        {
            string src = StripLineComments(ReadSource(WinEnforcerPath));

            Assert.AreEqual(1, Regex.Matches(src, @"_selfInducedRefit\s*=\s*true\s*;").Count,
                "자기유발 표시를 세우는 곳은 ReArmFullScreenFitAfterNativeWindowMove() 한 곳이어야 한다 — " +
                "다른 곳에서 세우면 외부 사건까지 억제된다.");

            int clears = Regex.Matches(src, @"_selfInducedRefit\s*=\s*false\s*;").Count;
            Assert.GreaterOrEqual(clears, 3,
                $"자기유발 표시를 내리는 곳이 {clears}곳뿐이다. 최소 셋이어야 한다: " +
                "디스플레이 구성 변경 재무장 · ReArmFullScreenFitForNewTarget · 적합 확정. " +
                "빠진 경로는 「외부 사건인데 Screen.SetResolution을 참는」 새 버그가 된다.");

            // 세우는 곳이 정말 그 메서드 안인가 — 이름 근접이 아니라 «메서드 본문»으로 확인한다.
            int reArmAt = src.IndexOf("private void ReArmFullScreenFitAfterNativeWindowMove()",
                StringComparison.Ordinal);
            Assert.Greater(reArmAt, 0, "재무장 메서드 본문을 찾지 못했다 — 테스트가 낡았다.");
            int bodyEnd = src.IndexOf("\n        }", reArmAt, StringComparison.Ordinal);
            Assert.Greater(bodyEnd, reArmAt, "재무장 메서드 본문의 끝을 찾지 못했다.");
            string body = src.Substring(reArmAt, bodyEnd - reArmAt);
            StringAssert.Contains("_selfInducedRefit = true;", body,
                "자기유발 표시가 재무장 메서드 밖에서 세워지고 있다.");

            // 이 라운드가 수명 상한을 건드리지 않았음을 같은 자리에서 다시 못박는다
            // (PlatformParityAuditTests가 같은 불변식을 갖고 있고, 여기서도 독립으로 잰다).
            StringAssert.DoesNotContain("_setResolutionCalls", body,
                "재무장이 SetResolution 수명 상한을 되돌린다 — 그러면 상한이 사실상 사라진다.");
            StringAssert.DoesNotContain("_windowResizeCalls", body,
                "재무장이 창 리사이즈 수명 상한을 되돌린다 — 1px 래칫이 되살아날 문이다.");
        }

        // ★ 상한·불감대 값 자체를 여기서 다시 잠그지 않는다 — 이미 등재돼 있다
        //   (OverlayResizeRatchetTests의 「불감대_기본값은_2px에서_움직이지_않는다」·
        //    「SetResolution_상한은_두_플랫폼에서_같은_값이다」, FullScreenAttachTimerPreloadTests의
        //    ReapplyIntervalSeconds 잠금). 두 벌로 만들면 갈린다(TEAM.md 묶음⑫ 규칙 53).
        //   이 파일이 상한에 관해 잠그는 것은 「핑퐁 차단이 상한을 넘겨 호출을 되살리지 않는가」 하나다.

        // ★ macOS 비대칭(「이 차단은 Windows에만 있어야 한다」)은 여기서 다시 잠그지 않는다.
        //   정본은 PlatformParityAuditTests의 「해당없음_핑퐁_차단은_Windows에만_필요하다」다 —
        //   CLAUDE.md가 «새 플랫폼 분기는 그 감사에 항목을 추가한다»고 정하고 있고, 두 벌로 만들면
        //   갈린다(docs/TEAM.md 묶음⑫ 규칙 53). 그쪽 항목이 Windows 측 존재·macOS 측 부재·
        //   양성 대조 셋을 함께 센다.
    }
}
