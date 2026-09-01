using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 사용자 신고 <b>"계속 실행해 놓을수록 렉이 심해지는거 같음"</b>(2026-09-01, Windows 릴리즈
    /// 20260901b)의 근본 원인을 잠그는 테스트.
    ///
    /// ============================================================================
    /// 신고와 증거
    /// ============================================================================
    /// 같은 실행에서 프레임 시간 꼬리가 시간순으로 악화됐다:
    /// <code>
    /// 표본 512개 — 평균 24.60ms  최대  33.48ms
    ///   …
    /// 표본 512개 — 평균 25.31ms  p95 58.27ms / p99 150.16ms / 최대 407.49ms
    /// </code>
    /// <b>GPU는 무관하다</b>(GPU 프레임시간 평균 0.01ms). 순수 CPU다.
    ///
    /// 같은 로그에 창 크기의 단조 감소가 있었다:
    /// <c>windowSize=(3840) -> (3839) -> (3838) -> ... -> (3831)</c>.
    ///
    /// ============================================================================
    /// 확정된 인과 (추측 아님 — 아래 테스트가 수정 전 규칙으로 재현한다)
    /// ============================================================================
    /// <c>WindowsOverlayStateEnforcer.TickFullScreenBounds()</c>의 창 기하 판정이
    /// <b>"목표와 정확히 같은가"</b>였다. 그래서 되읽기가 대입값보다 1px 작게 돌아오는
    /// <b>상수 오차</b>가 영원히 "불일치"로 읽혔고, 매 에피소드마다
    ///   · <c>Screen.SetResolution</c> 재호출
    ///   · 창 크기 재대입
    /// 이 실행됐다. <b>둘 다 클라이언트 영역을 바꾸므로 D3D 스왑체인 + DWM 리디렉션 표면이
    /// 재생성된다</b> — 수백 ms 정지의 정체다.
    ///
    /// 이 저장소는 이 인과를 이미 문서로 갖고 있었다(<see cref="DisplayTopologyWatcher"/> 클래스 문서:
    /// "중간 상태마다 Screen.SetResolution을 부르면 백버퍼 재할당이 연달아 일어나 사용자가 체감하는
    /// 멈춤이 오히려 길어진다"). 판정 조건만 그 경고를 위반하고 있었다.
    ///
    /// ============================================================================
    /// 이 파일이 순수 규칙을 검증하는 이유
    /// ============================================================================
    /// 개발 머신은 macOS이고 Windows 실기 실행이 불가능하다. 그래서 판정을 P/Invoke가 없는
    /// <see cref="OverlayBoundsFitPolicy"/>로 뽑아냈고, 여기서 <b>실행으로</b> 검증한다.
    /// 나머지(호출 배선)는 소스 스캔으로 잠근다 — 이 저장소의 확립된 관례다
    /// (<c>TopmostZOrderWatchdogTests</c>와 같은 방식).
    /// </summary>
    public class OverlayResizeRatchetTests
    {
        /// <summary>실기 관측: 대입값보다 되읽기가 딱 1px 작다.</summary>
        private const float NativeUndershootPx = 1f;

        private static string WindowsPlatformDir => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "Windows");

        private static string ReadEnforcer()
            => File.ReadAllText(Path.Combine(WindowsPlatformDir, "WindowsOverlayStateEnforcer.cs"));

        // ────────────────────────────────────────────────────────────────────────
        // macOS 쪽 (2026-09-01 확장) — 같은 래칫이 그대로 있었다
        // ────────────────────────────────────────────────────────────────────────

        private static string ReadMacEnforcer()
            => File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Platform", "MacOS", "MacOverlayStateEnforcer.cs"));

        /// <summary>
        /// 주석을 걷어낸 소스. <b>"이 코드가 지금 무엇을 하는가"를 물을 때만</b> 쓴다.
        ///
        /// <para>이 함수가 필요한 이유는 이 테스트를 쓰면서 <b>실제로 밟았기</b> 때문이다:
        /// MacOverlayStateEnforcer에 "직전까지 이 판정은 <c>Screen.width != targetPixelW</c>였다"고
        /// 결함의 역사를 정직하게 적어 두었더니, 그 주석이 <b>구현으로 오인</b>되어
        /// "완전일치 판정이 돌아왔다"는 거짓 실패가 났다.
        /// <c>PlatformParityAuditTests.StripLineComments</c>가 같은 함정을 반대 방향(거짓 통과)으로
        /// 겪고 남긴 규칙과 동일하다 — 결함을 정직하게 적을수록 감사가 눈머는 것을 막는다.</para>
        /// </summary>
        private static string StripComments(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>실측 Retina 배율(OS 포인트 / Unity 픽셀). 사용자 실기 로그의 `dpi배율=0.500`.</summary>
        private const float RetinaPointsPerPixel = 0.5f;

        /// <summary>실측 화면: 1512x982 포인트 = 3024x1964 Unity 픽셀.</summary>
        private const float MacMonitorWidthPoints = 1512f;
        private const int MacScreenWidthPixels = 3024;

        // ────────────────────────────────────────────────────────────────────────
        // 1. 래칫 재현/차단 (순수 규칙 실행)
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 한 "에피소드"(TickFullScreenBounds 1회)를 반복 시뮬레이션한다.
        /// <paramref name="useDeadBand"/>가 false면 <b>수정 전 규칙</b>(정확히 같은가)이다.
        /// </summary>
        private static (int resizes, int setResolutions, float finalWidth) Simulate(
            bool useDeadBand, int episodes)
        {
            const float target = 3840f;
            float actual = 1920f;          // 기동 시 창 크기
            int screenW = 1920;
            bool windowed = false;         // ProjectSettings 기본값은 FullScreenWindow(=1)
            int resizes = 0, setResolutions = 0;

            for (int e = 0; e < episodes; e++)
            {
                bool needResize = useDeadBand
                    ? OverlayBoundsFitPolicy.ShouldResize(actual, 2160f, target, 2160f,
                        OverlayBoundsFitPolicy.DefaultEpsilonPixels)
                    : actual != target;

                bool needSetResolution = useDeadBand
                    ? OverlayBoundsFitPolicy.ShouldSetResolution(screenW, 2160, (int)target, 2160,
                        windowed, OverlayBoundsFitPolicy.DefaultEpsilonPixels, setResolutions, 4)
                    : (screenW != (int)target || !windowed);

                if (needSetResolution)
                {
                    setResolutions++;
                    windowed = true;
                    screenW = (int)target;
                }
                if (needResize)
                {
                    resizes++;
                    actual = target - NativeUndershootPx;
                    screenW = (int)actual;   // 클라이언트가 바뀌면 Screen.width도 따라간다
                }
            }
            return (resizes, setResolutions, actual);
        }

        [Test]
        public void 수정_전_규칙은_1px_오차만으로_스왑체인_재생성을_무한_반복한다()
        {
            var before = Simulate(useDeadBand: false, episodes: 200);

            Assert.GreaterOrEqual(before.resizes, 200,
                "이 테스트가 재현에 실패하면 이 파일의 전제(되읽기 1px 부족)가 바뀐 것이다. " +
                "수정 전 규칙에서는 에피소드마다 창 리사이즈가 일어나야 한다.");
            Assert.GreaterOrEqual(before.setResolutions, 100,
                "Screen.SetResolution도 함께 무한 반복된다 — 이것이 407ms 스파이크의 직접 원인이다.");
        }

        [Test]
        public void 불감대는_같은_상황에서_재생성을_1회로_수렴시킨다()
        {
            var after = Simulate(useDeadBand: true, episodes: 200);

            Assert.AreEqual(1, after.resizes,
                "창 리사이즈는 최초 적합 1회로 끝나야 한다. 2회 이상이면 래칫이 남아 있다.");
            Assert.LessOrEqual(after.setResolutions, 4,
                "Screen.SetResolution은 프로세스 수명 상한 안에 있어야 한다.");
            Assert.LessOrEqual(Math.Abs(after.finalWidth - 3840f),
                OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                "수렴한 창 크기는 목표의 불감대 안이어야 한다 — 증상을 덮은 것이 아니라 " +
                "실제로 맞은 것임을 확인한다.");
        }

        [Test]
        public void 불감대는_사람이_인지할_수_있는_어긋남은_그대로_잡는다()
        {
            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldResize(3837f, 2160f, 3840f, 2160f,
                    OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                "3px 어긋남까지 덮으면 불감대가 진짜 결함을 숨기는 장치가 된다. " +
                "불감대는 관측된 상수 오차(1px)만 흡수해야 한다.");

            Assert.IsFalse(
                OverlayBoundsFitPolicy.ShouldResize(3839f, 2160f, 3840f, 2160f,
                    OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                "관측된 1px 오차는 재적용을 유발해서는 안 된다.");
        }

        [Test]
        public void 불감대_기본값은_2px에서_움직이지_않는다()
        {
            Assert.AreEqual(2f, OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                "이 값을 키우면 진짜 어긋남까지 덮이고, 0으로 되돌리면 신고된 래칫이 그대로 재발한다. " +
                "바꾸려면 실기 로그를 근거로 OverlayBoundsFitPolicy 문서에 함께 남길 것.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 2. 창모드 강제는 살아 있어야 한다 (별개 신고 "창 뒤로 넘어감"의 원인)
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 창모드_강제는_해상도가_이미_같아도_실행된다()
        {
            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldSetResolution(3840, 2160, 3840, 2160,
                    fullScreenModeIsWindowed: false,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 0, maxCalls: 4),
                "불감대를 넣으면서 이 경로까지 막으면 2026-09-01의 다른 신고(엑셀 클릭 시 캐릭터가 " +
                "창 뒤로 넘어감)가 그대로 재발한다. 전체화면 계열 모드로 남으면 Unity가 포커스를 " +
                "잃을 때 창을 z-order 뒤로 보낸다.");
        }

        [Test]
        public void 창모드이고_불감대_안이면_SetResolution을_부르지_않는다()
        {
            Assert.IsFalse(
                OverlayBoundsFitPolicy.ShouldSetResolution(3839, 2160, 3840, 2160,
                    fullScreenModeIsWindowed: true,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 0, maxCalls: 4),
                "1px 차이로 백버퍼를 재할당하면 안 된다.");
        }

        [Test]
        public void SetResolution에는_프로세스_수명_상한이_있다()
        {
            Assert.IsFalse(
                OverlayBoundsFitPolicy.ShouldSetResolution(1920, 1080, 3840, 2160,
                    fullScreenModeIsWindowed: false,
                    epsilonPixels: OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    callsSoFar: 4, maxCalls: 4),
                "24시간 상주 앱에서 이 호출이 무제한이면, 판정이 어떤 이유로든 진동할 때 " +
                "사용자는 몇 초마다 수백 ms씩 얼어붙는 앱을 보게 된다. 상한은 반드시 있어야 한다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 3. 배선 잠금 (소스 스캔)
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void Enforcer는_판정을_순수_규칙에_위임한다()
        {
            string src = ReadEnforcer();

            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldSetResolution", src,
                "판정이 파일 안으로 되돌아오면 Windows 실기가 없는 이 저장소에서 다시 검증 불가능해진다.");
            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldResize", src);
            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldMove", src);
        }

        [Test]
        public void Enforcer는_이미_맞은_창을_다시_대입하지_않는다()
        {
            string src = ReadEnforcer();

            int resizeGuard = src.IndexOf("if (needsResize)", StringComparison.Ordinal);
            int assign = src.IndexOf("_controller.windowSize = monitor.size;", StringComparison.Ordinal);

            Assert.Greater(resizeGuard, 0,
                "창 크기 대입에 가드가 사라졌다 — 대입 한 번이 곧 백버퍼 재할당 한 번이다.");
            Assert.Greater(assign, resizeGuard,
                "windowSize 대입은 반드시 needsResize 가드 **안**에 있어야 한다.");

            StringAssert.Contains("if (needsMove) _controller.windowPosition = monitor.position;", src,
                "창 이동도 같은 이유로 가드해야 한다.");
        }

        [Test]
        public void Enforcer_로그는_재생성_누적_횟수를_남긴다()
        {
            string src = ReadEnforcer();

            StringAssert.Contains("_setResolutionCalls", src,
                "실기에서 '이 스파이크의 범인이 우리인가'를 가르려면 재생성 누적 횟수가 로그에 있어야 한다 — " +
                "FramePacing의 [프레임스파이크] '백버퍼가 바뀌었다' 줄과 시각 대조가 이 값으로 이뤄진다.");
            StringAssert.Contains("재생성 누적", src);
        }

        // ────────────────────────────────────────────────────────────────────────
        // 4. 되먹임 차단 — 토폴로지 시그니처가 우리 창 기하에 반응하면 안 된다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 모니터_선택은_히스테리시스를_가진다()
        {
            string src = ReadEnforcer();

            StringAssert.Contains("_lastMonitorIndex", src,
                "DisplayTopologyWatcher 문서는 '우리 창의 크기/위치에서 유도되는 값'을 시그니처에 넣지 " +
                "말라고 못박고 있다. TryGetTargetMonitorRect는 우리 창 중심으로 모니터를 고르므로 " +
                "그 선택이 흔들리면 '재적합 -> 시그니처 변화 -> 재적합' 되먹임이 된다. " +
                "직전 선택을 기억하는 히스테리시스가 그 경로를 끊는다.");

            int fallback = src.IndexOf("직전 선택을 유지한다", StringComparison.Ordinal);
            Assert.Greater(fallback, 0,
                "창 중심이 어느 모니터에도 안 속할 때 0번으로 튀면, '잠깐 좌표를 못 읽었다'를 " +
                "'사용자가 창을 다른 모니터로 옮겼다'로 오인해 재적합을 부른다.");
        }

        // ════════════════════════════════════════════════════════════════════════
        // 4. macOS — Windows를 고쳤으니 여기도 고친다 (CLAUDE.md 협업 프로토콜)
        //
        //    ★ 정직한 구분: macOS에서 1px 래칫은 **지금 발현하고 있지 않다**.
        //      실기 로그(Player.log, 09-01 07:23)에 이 함수의 로그는 딱 한 줄이었고 되읽기가
        //      대입값과 정확히 같았다(size 1512 -> 1512, Screen 3024 == 목표 3024, 결과=성공).
        //      아래 테스트들이 잠그는 것은 "지금 아픈 곳"이 아니라 **구조적 위험**이다:
        //      상한 없는 SetResolution, 무조건 대입, 그리고 Retina 단위 함정.
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ★ 이 라운드의 핵심 발견 — <b>2px 상수를 macOS에 그대로 쓰면 안 된다</b>.
        ///
        /// 이 규칙의 두 판정은 좌표계가 다르다. 창 기하는 <b>OS 포인트</b>, 해상도는
        /// <b>Unity 픽셀</b>이고 Retina에서는 포인트 1 = 픽셀 2다. 그래서 픽셀 판정에 2를 그대로
        /// 쓰면 실효 불감대가 1포인트로 반토막 나고, <b>기하 판정은 "맞았다"는데 해상도 판정만 홀로
        /// "틀렸다"</b>고 해서 <c>Screen.SetResolution</c>이 다시 불린다 — 정확히 래칫의 모양이다.
        /// </summary>
        [Test]
        public void Retina에서_기하_불감대_안이면_해상도_판정도_조용해야_한다()
        {
            float epsilon = OverlayBoundsFitPolicy.ResolutionEpsilonPixels(RetinaPointsPerPixel);

            // 창 폭이 기하 불감대(2pt) 안에서 흔들리는 모든 경우.
            for (int errPoints = -2; errPoints <= 2; errPoints++)
            {
                float actualWidthPoints = MacMonitorWidthPoints + errPoints;

                // 기하 판정은 이 오차를 "맞았다"고 본다 — 그것이 불감대의 정의다.
                Assert.IsFalse(
                    OverlayBoundsFitPolicy.ShouldResize(actualWidthPoints, 982f,
                        MacMonitorWidthPoints, 982f, OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                    $"전제가 깨졌다: {errPoints:+#;-#;0}pt는 기하 불감대 안이어야 한다.");

                // 배율은 상수가 아니라 실측값이다(AutoDpiScale = 창 폭 / Screen.width).
                float measuredDpi = actualWidthPoints / MacScreenWidthPixels;
                int targetPixelW = Mathf.RoundToInt(MacMonitorWidthPoints / measuredDpi);

                Assert.IsFalse(
                    OverlayBoundsFitPolicy.ShouldSetResolution(
                        MacScreenWidthPixels, 1964, targetPixelW, 1964,
                        fullScreenModeIsWindowed: true,
                        epsilonPixels: OverlayBoundsFitPolicy.ResolutionEpsilonPixels(measuredDpi),
                        callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls),
                    $"창 폭 {actualWidthPoints}pt(오차 {errPoints:+#;-#;0}pt)에서 해상도 판정만 홀로 " +
                    $"재적용을 걸었다. 기하 판정은 같은 오차를 통과시키므로, 이 상태는 " +
                    $"'영원히 한쪽이 다른 쪽을 되살리는' 래칫이다. " +
                    $"(목표={targetPixelW}px, 현재={MacScreenWidthPixels}px, 불감대={epsilon:F3}px)");
            }
        }

        /// <summary>
        /// 불감대가 <b>진짜 어긋남까지 덮으면</b> 처방이 아니라 은폐다. 기하 불감대의 두 배(4pt)로
        /// 어긋나면 해상도 판정은 반드시 다시 걸려야 한다.
        /// </summary>
        [Test]
        public void Retina_불감대는_진짜_어긋남까지_덮지는_않는다()
        {
            foreach (int errPoints in new[] { -4, 4 })
            {
                float actualWidthPoints = MacMonitorWidthPoints + errPoints;
                float measuredDpi = actualWidthPoints / MacScreenWidthPixels;
                int targetPixelW = Mathf.RoundToInt(MacMonitorWidthPoints / measuredDpi);

                Assert.IsTrue(
                    OverlayBoundsFitPolicy.ShouldSetResolution(
                        MacScreenWidthPixels, 1964, targetPixelW, 1964,
                        fullScreenModeIsWindowed: true,
                        epsilonPixels: OverlayBoundsFitPolicy.ResolutionEpsilonPixels(measuredDpi),
                        callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls),
                    $"{errPoints:+#;-#;0}pt 어긋남을 불감대가 삼켰다. 그러면 이 규칙은 결함을 고치는 " +
                    "것이 아니라 덮는 것이 된다.");
            }
        }

        [Test]
        public void 불감대는_배율에서_유도되고_이상한_배율에는_넓어지지_않는다()
        {
            Assert.AreEqual(2f + OverlayBoundsFitPolicy.TargetRoundingSlackPixels,
                OverlayBoundsFitPolicy.ResolutionEpsilonPixels(1f), 0.0001f,
                "배율 1(Windows)에서는 포인트와 픽셀이 같으므로 기본값 그대로여야 한다.");

            Assert.AreEqual(4f + OverlayBoundsFitPolicy.TargetRoundingSlackPixels,
                OverlayBoundsFitPolicy.ResolutionEpsilonPixels(0.5f), 0.0001f,
                "Retina(배율 2)에서는 포인트 1 = 픽셀 2이므로 픽셀 불감대도 2배여야 한다.");

            // 깨진 측정값으로 불감대를 무한히 키우면 진짜 어긋남까지 덮는다.
            Assert.AreEqual(
                OverlayBoundsFitPolicy.DefaultEpsilonPixels * OverlayBoundsFitPolicy.MaxDeviceScale
                    + OverlayBoundsFitPolicy.TargetRoundingSlackPixels,
                OverlayBoundsFitPolicy.ResolutionEpsilonPixels(0.0001f), 0.0001f,
                "존재하지 않는 배율(1만 배)에서 불감대가 그대로 커지면 은폐가 된다.");

            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.AreEqual(OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                    OverlayBoundsFitPolicy.ResolutionEpsilonPixels(bad), 0.0001f,
                    $"배율을 모를 때({bad})는 넓히지 않고 기본값으로 떨어져야 한다 — " +
                    "모르는 상태에서 관대해지는 것이 가장 나쁘다.");
            }
        }

        /// <summary>
        /// macOS에는 <c>SetResolution</c> 수명 상한이 <b>아예 없었다</b>. 발현 여부와 무관하게
        /// 그 자체가 위험이다 — <c>TickDisplayTopology</c>가 재무장할 때 시도 횟수를 0으로 되돌리므로
        /// 디스플레이 통지가 진동하면 호출이 무제한이 된다.
        /// </summary>
        [Test]
        public void macOS도_SetResolution_수명_상한을_가진다()
        {
            string src = StripComments(ReadMacEnforcer());

            StringAssert.Contains("OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls", src,
                "24시간 상주 앱에서 백버퍼 재할당은 절대 무제한이면 안 된다.");
            StringAssert.Contains("_setResolutionCalls++", src,
                "상한을 세려면 실제로 세어야 한다.");

            Assert.IsFalse(src.Contains("_setResolutionCalls = 0"),
                "누적 횟수를 어디선가 0으로 되돌리면 '프로세스 수명 상한'이 아니게 된다. " +
                "특히 TickDisplayTopology의 재무장 블록에서 되돌리면 상한이 사실상 사라진다.");

            int reArm = src.IndexOf("_fullScreenApplyAttempts = 0;", StringComparison.Ordinal);
            Assert.Greater(reArm, 0, "재무장 블록을 찾지 못했다 — 테스트가 낡았다.");
        }

        [Test]
        public void macOS_Enforcer도_판정을_같은_순수_규칙에_위임한다()
        {
            string src = StripComments(ReadMacEnforcer());

            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldSetResolution", src,
                "한쪽 플랫폼만 규칙을 지나면, 고친 쪽이 아닌 쪽에서 같은 결함이 그대로 재발한다 — " +
                "이 저장소에서 오늘만 세 번 반복된 패턴이다.");
            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldResize", src);
            StringAssert.Contains("OverlayBoundsFitPolicy.ShouldMove", src);
            StringAssert.Contains("OverlayBoundsFitPolicy.ResolutionEpsilonPixels", src,
                "Retina에서는 해상도 판정 단위가 창 기하와 다르다 — 상수 2px을 그대로 쓰면 안 된다.");

            Assert.IsFalse(src.Contains("Screen.width != targetPixelW"),
                "완전일치 판정이 돌아왔다. 그것이 Windows에서 407ms 멈춤을 만든 바로 그 조건이다.");
        }

        [Test]
        public void macOS_Enforcer도_이미_맞은_창을_다시_대입하지_않는다()
        {
            string src = StripComments(ReadMacEnforcer());

            int resizeGuard = src.IndexOf("if (needsResize)", StringComparison.Ordinal);
            int assign = src.IndexOf("_controller.windowSize = monitor.size;", StringComparison.Ordinal);

            Assert.Greater(resizeGuard, 0,
                "창 크기 대입이 무조건 실행으로 돌아갔다 — 대입 한 번이 곧 백버퍼 재할당 한 번이다.");
            Assert.Greater(assign, resizeGuard,
                "windowSize 대입은 반드시 needsResize 가드 **안**에 있어야 한다.");
            StringAssert.Contains("if (needsMove) _controller.windowPosition = monitor.position;", src);
        }

        /// <summary>
        /// 상한 값이 두 플랫폼에서 갈라지지 않게 잠근다. Windows판은 아직 자기 파일에 사본
        /// (<c>private const int MaxSetResolutionCalls = 4</c>)을 들고 있으므로, 그 리터럴을
        /// <b>실제로 읽어</b> 규칙의 상수와 대조한다 — 한쪽만 바꾸면 여기서 깨진다.
        /// </summary>
        [Test]
        public void SetResolution_상한은_두_플랫폼에서_같은_값이다()
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                ReadEnforcer(), @"MaxSetResolutionCalls\s*=\s*(\d+)");

            Assert.IsTrue(m.Success,
                "Windows판의 상한 선언을 찾지 못했다. 상한이 사라졌다면 그쪽이 결함이고, " +
                "이름만 바뀌었다면 이 테스트를 함께 갱신해야 한다.");
            Assert.AreEqual(OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                int.Parse(m.Groups[1].Value),
                "Windows와 macOS가 서로 다른 상한을 쓰면, 한쪽에서 검증한 결론이 다른 쪽에서 " +
                "성립하지 않는다. 값은 OverlayBoundsFitPolicy 한 곳에서만 정한다.");
        }
    }
}
