using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// "엑셀 클릭하면 캐릭터가 창 뒤로 넘어간다" 회귀 방지 (2026-09-01, 같은 버그 3번째 신고)
    /// ============================================================================
    /// 사용자 신고 원문(2026-08-31): "엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가
    /// 없어져버림 <b>화면 뒤로 넘어 가는 거 같음</b>" / 확인 사살(2026-09-01):
    /// "자동숨김이 아니라 창뒤로 넘어가는거야".
    ///
    /// 앞선 수정 2회는 전부 <b>자동 숨김 판정</b>을 고쳤고 증상은 그대로였다. 진짜 원인은 z-order였다.
    /// 이 테스트는 그 진짜 원인 3가지가 다시 들어오지 못하게 잠근다:
    ///   (A) 항상위 재적용이 기동 직후 몇 초로 끝나고 그 뒤엔 아무도 되돌리지 않음
    ///   (B) "이미 topmost다" 판정을 라이브러리 <b>캐시</b>로 해서 재적용을 영원히 건너뜀
    ///   (C) 플레이어가 Windows에서만 FullScreenWindow 모드로 남아 Unity가 포커스 상실 시 창을 뒤로 보냄
    ///
    /// <para>(A)(C)는 <c>#if UNITY_STANDALONE_WIN</c> 안이라 이 macOS EditMode에서는 타입이 아예
    /// 존재하지 않는다. 그래서 소스 텍스트 정적 스캔으로 검사한다 —
    /// <c>WindowsFullscreenGamePolicyTests</c>가 원칙 3을 잠그는 방식과 같다.</para>
    /// </summary>
    public class TopmostZOrderWatchdogTests
    {
        // ====================================================================
        // 1. 재적용 규칙 (순수 함수) — 언제 topmost를 다시 거는가
        // ====================================================================

        [Test]
        public void 항상위가_풀렸으면_재적용한다()
        {
            Assert.IsTrue(TopmostRestorePolicy.ShouldReassert(
                desiredTopmost: true, osTopmostAlive: false, suspended: false),
                "OS 실측상 WS_EX_TOPMOST가 없는데 재적용하지 않으면 캐릭터가 영원히 창 뒤에 남는다.");
        }

        [Test]
        public void 항상위가_살아있으면_재적용하지_않는다()
        {
            Assert.IsFalse(TopmostRestorePolicy.ShouldReassert(
                desiredTopmost: true, osTopmostAlive: true, suspended: false),
                "멀쩡한데 0.1초마다 SetWindowPos를 다시 걸면 레이어드 창 합성이 계속 무효화된다(깜박임).");
        }

        [Test]
        public void 항상위를_원하지_않으면_감시하지_않는다()
        {
            Assert.IsFalse(TopmostRestorePolicy.ShouldReassert(
                desiredTopmost: false, osTopmostAlive: false, suspended: false),
                "기동 직후 등 목표가 false인 구간에서 멋대로 topmost를 걸면 안 된다.");
        }

        [Test]
        public void 전체화면_게임_숨김중에는_재적용을_보류한다()
        {
            Assert.IsFalse(TopmostRestorePolicy.ShouldReassert(
                desiredTopmost: true, osTopmostAlive: false, suspended: true),
                "절대 불변 원칙 2(비침해) — 전체화면 게임 위로 기어 올라가지 않는다.");
        }

        // ====================================================================
        // 2. 전이 추적 — 24시간 상주 앱이므로 "변화가 있을 때만" 로그해야 한다
        // ====================================================================

        [Test]
        public void 첫_관측은_기준선만_잡고_로그하지_않는다()
        {
            var tracker = new TopmostWatchdogTracker();
            var evt = tracker.Observe(true, true, true, 0x100, 1.0, out _);
            Assert.AreEqual(TopmostWatchEvent.None, evt);
        }

        [Test]
        public void 변화가_없으면_계속_None이다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(TopmostWatchEvent.None,
                    tracker.Observe(true, true, true, 0x100, 1.1 + i * 0.1, out _),
                    "매 폴링마다 로그를 남기면 Player.log가 쓸모없어진다.");
            }
        }

        [Test]
        public void 밀렸다가_같은틱에_되돌리면_DemotedAndRestored다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);

            // 엑셀 클릭 -> WS_EX_TOPMOST가 사라진 것을 관측(aliveBefore=false),
            // 같은 틱에 재적용해서 살아남(aliveAfter=true).
            var evt = tracker.Observe(true, false, true, 0x200, 1.1, out _);
            Assert.AreEqual(TopmostWatchEvent.DemotedAndRestored, evt);
            Assert.AreEqual(1, tracker.DemotionCount);
        }

        [Test]
        public void 되돌리기가_실패하면_Demoted로_구분된다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);

            var evt = tracker.Observe(true, false, false, 0x200, 1.1, out _);
            Assert.AreEqual(TopmostWatchEvent.Demoted, evt,
                "'밀렸다'와 '밀린 걸 되돌렸다'가 같은 줄로 찍히면 다음 신고에서 또 원인을 못 가른다.");
        }

        [Test]
        public void 되돌리기_실패가_지속되면_반복_로그하지_않는다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);
            tracker.Observe(true, false, false, 0x200, 1.1, out _);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(TopmostWatchEvent.None,
                    tracker.Observe(true, false, false, 0x200, 1.2 + i * 0.1, out _),
                    "복구 실패 상태가 이어지는 동안 초당 10줄을 찍으면 안 된다(상주 앱).");
            }
        }

        [Test]
        public void 늦게_복구되면_밀려있던_시간을_보고한다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);
            tracker.Observe(true, false, false, 0x200, 2.0, out _);   // 2.0초에 밀림 발견
            for (int i = 0; i < 5; i++) tracker.Observe(true, false, false, 0x200, 2.1 + i * 0.1, out _);

            var evt = tracker.Observe(true, true, true, 0x200, 3.25, out double demotedFor);
            Assert.AreEqual(TopmostWatchEvent.Restored, evt);
            Assert.AreEqual(1.25, demotedFor, 1e-6,
                "'되돌아오긴 하는데 눈에 보일 만큼 느리다'를 가르려면 지속 시간이 로그에 있어야 한다.");
        }

        [Test]
        public void 밀림_없이_전경창만_바뀌면_ForegroundChanged다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);

            var evt = tracker.Observe(true, true, true, 0x200, 1.1, out _);
            Assert.AreEqual(TopmostWatchEvent.ForegroundChanged, evt,
                "이 줄만 있고 Demoted 계열이 없는데도 캐릭터가 가려진다면 원인은 z-order가 아니다 — " +
                "그 판별이 이 사건의 존재 이유다.");
        }

        [Test]
        public void 밀림_횟수는_누적된다()
        {
            var tracker = new TopmostWatchdogTracker();
            tracker.Observe(true, true, true, 0x100, 1.0, out _);
            for (int i = 0; i < 3; i++)
            {
                tracker.Observe(true, false, true, 0x200, 2.0 + i, out _);   // 밀림 -> 즉시 복구
                tracker.Observe(true, true, true, 0x200, 2.5 + i, out _);    // 안정
            }
            Assert.AreEqual(3, tracker.DemotionCount);
        }

        // ====================================================================
        // 3. Windows 구현 구조 잠금 (소스 정적 스캔)
        //    — 이 세 가지가 실제 원인이었으므로 텍스트로 못 박는다
        // ====================================================================

        private static string WindowsPlatformDir => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "Windows");

        private static string ReadEnforcer() =>
            File.ReadAllText(Path.Combine(WindowsPlatformDir, "WindowsOverlayStateEnforcer.cs"));

        [Test]
        public void 원인A_항상위_감시는_재적용_상한보다_먼저_호출된다()
        {
            string src = ReadEnforcer();
            int watchdog = src.IndexOf("TickTopmostWatchdog();", System.StringComparison.Ordinal);
            int cap = src.IndexOf("if (_appliedCount >= ReapplyAttempts) return;", System.StringComparison.Ordinal);

            Assert.Greater(watchdog, 0, "TickTopmostWatchdog() 호출이 사라졌다 — 항상위를 되돌릴 주체가 없어진다.");
            Assert.Greater(cap, 0, "재적용 상한 early return 문구가 바뀌었다면 이 테스트도 함께 갱신해야 한다.");
            Assert.Less(watchdog, cap,
                "감시 호출이 `_appliedCount >= ReapplyAttempts` early return **아래**로 내려가면, " +
                "기동 2.5초 뒤부터 감시가 통째로 죽어 원래 버그가 그대로 재발한다.");
        }

        [Test]
        public void 원인B_재적용_생략_판정에_라이브러리_캐시를_쓰지_않는다()
        {
            string src = ReadEnforcer();

            StringAssert.DoesNotContain("_controller.isTopmost == DesiredTopmost", src,
                "UniWinCore.IsTopmost는 `IsActive && _isTopmost`인 순수 C# 캐시다(네이티브 되읽기 extern은 " +
                "선언만 있고 호출되지 않는다). 이 캐시로 '이미 목표값'을 판정하면 OS가 창을 강등시켜도 " +
                "영원히 재적용을 건너뛴다 — 신고된 버그의 직접 원인이다.");

            StringAssert.Contains("TryReadOsTopmost", src,
                "생략 판정은 반드시 OS 실측(GetWindowLong(GWL_EXSTYLE) & WS_EX_TOPMOST)에 근거해야 한다.");
        }

        [Test]
        public void 원인C_창모드_강제는_해상도가_같아도_실행된다()
        {
            string src = ReadEnforcer();

            StringAssert.Contains("Screen.fullScreenMode != FullScreenMode.Windowed", src,
                "조건이 해상도 비교뿐이면, 모니터 네이티브 해상도(dpi 1.0)인 Windows에서 " +
                "SetResolution이 한 번도 불리지 않아 플레이어가 FullScreenWindow 모드로 남는다. " +
                "그 모드에서 Unity는 포커스를 잃을 때 창을 뒤로 보낸다 — 사용자가 본 증상 그대로다.");

            StringAssert.Contains("fullScreenMode={Screen.fullScreenMode}", src,
                "실기 확인 수단 — 이 값이 로그에 없으면 다음 신고에서도 원인을 가를 수 없다.");
        }

        [Test]
        public void 감시자는_쓰기계열_Win32를_선언조차_하지_않는다()
        {
            string src = File.ReadAllText(Path.Combine(WindowsPlatformDir, "WindowsTopmostWatchdog.cs"));

            foreach (string forbidden in new[] { "SetWindowPos", "SetWindowLong", "SetForegroundWindow",
                                                 "ShowWindow", "BringWindowToTop", "MoveWindow", "SetParent" })
            {
                StringAssert.DoesNotContain($"extern {forbidden}", src,
                    $"절대 불변 원칙 3 — 남의 창을 바꿀 수 있는 {forbidden}은 선언조차 하지 않는다. " +
                    "topmost 재적용은 우리 창에만 작용하는 UniWindowController.isTopmost 대입으로만 한다.");
                StringAssert.DoesNotContain($"bool {forbidden}(", src, $"{forbidden} 선언 금지.");
            }
        }

        [Test]
        public void 감시자는_전이때만_로그한다()
        {
            string src = File.ReadAllText(Path.Combine(WindowsPlatformDir, "WindowsTopmostWatchdog.cs"));
            StringAssert.Contains("if (evt == TopmostWatchEvent.None) return;", src,
                "24시간 상주 앱 — 변화가 없는 폴링은 한 줄도 남기지 않아야 한다.");
        }
    }
}
