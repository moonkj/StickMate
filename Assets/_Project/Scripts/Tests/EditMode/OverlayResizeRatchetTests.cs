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
    }
}
