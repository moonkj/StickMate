using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — OS 세션 종료(로그오프·시스템 종료)에서 작업표시줄 자동 숨김 원복이 누락되지 않는가, 그리고
    /// 종료 순서가 "원복 먼저, 워치독 정지 나중"인가(persona-stress R-1). 원칙 3 예외의 조건("실행 중에만 + 종료 시
    /// 원복")을 평범한 Windows 종료 경로에서도 지키는 장치다 — 경위는 <c>docs/TASKBAR_REVEAL.md</c>.
    ///
    /// <para>Windows 창 프로시저 자체는 이 머신에서 실행되지 않는다(<c>#if UNITY_STANDALONE_WIN</c>). 그래서 판정·순서·
    /// 멱등은 중립 코드를 <b>실행해</b> 잠그고, 프로시저 배선은 소스로 본다.</para>
    /// </summary>
    public sealed class SessionEndShutdownTests
    {
        private sealed class FakeControl : IReservedBarAutoHideControl
        {
            public bool AutoHide;
            public bool WriteSilentlyFails;
            public int WriteCount;
            public string PlatformTag => "TestOS";

            public bool TryReadAutoHide(out bool autoHideEnabled)
            {
                autoHideEnabled = AutoHide;
                return true;
            }

            public bool TrySetAutoHide(bool autoHideEnabled)
            {
                WriteCount++;
                if (WriteSilentlyFails) return false;
                AutoHide = autoHideEnabled;
                return true;
            }
        }

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        [SetUp]
        public void SetUp()
        {
            ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting("session-end-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            ReservedBarRevealDirector.ResetForTesting();
            AppShutdownSequence.ExecutorOverrideForTesting = null;
        }

        [TearDown]
        public void TearDown()
        {
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            ReservedBarRevealDirector.ResetForTesting();
            ReservedBarRestoreLedger.ResetForTesting();
        }

        // ------------------------------------------------------------------ 메시지 판정

        [Test]
        public void 세션_종료는_WM_ENDSESSION이고_wParam이_참일_때만이다()
        {
            Assert.IsTrue(SessionEndPolicy.IsSessionEndingNow(SessionEndPolicy.WmEndSession, 1));
            Assert.IsFalse(SessionEndPolicy.IsSessionEndingNow(SessionEndPolicy.WmEndSession, 0),
                "wParam=FALSE는 '종료가 취소됐다'는 통지다 — 여기서 원복하면 실행 중인데 자동 숨김이 돌아간다.");
            Assert.IsFalse(SessionEndPolicy.IsSessionEndingNow(SessionEndPolicy.WmQueryEndSession, 1),
                "WM_QUERYENDSESSION은 묻기일 뿐이다 — 다른 앱이 종료를 거부하면 세션은 계속된다.");
        }

        [Test]
        public void 메시지_번호는_winuser_h_값이다()
        {
            // 외부 ABI 상수다(프로덕션 튜닝 값이 아니다). 틀리면 이 머신에서는 실행으로 알 방법이 없다.
            Assert.AreEqual(0x0011u, SessionEndPolicy.WmQueryEndSession);
            Assert.AreEqual(0x0016u, SessionEndPolicy.WmEndSession);
        }

        [Test]
        public void 트레이를_끈_사용자에게는_이번_실행이_작업표시줄을_바꿨을_때만_수신_창을_세운다()
        {
            Assert.IsTrue(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: true, reservedBarChangedThisSession: true));
            Assert.IsFalse(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: true, reservedBarChangedThisSession: false),
                "바꾼 것이 없으면 되돌릴 것도 없다 — 숨은 창을 만들 이유가 없다.");
            Assert.IsFalse(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: false, reservedBarChangedThisSession: true),
                "트레이가 켜져 있으면 트레이 호스트 창이 이미 수신자다.");
        }

        // ------------------------------------------------------------------ 순서 (R-1)

        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void 종료_순서는_작업표시줄_원복이_워치독_정지보다_먼저다(AppShutdownTrigger trigger)
        {
            Assert.AreEqual(AppShutdownStep.RestoreReservedBar, AppShutdownSequence.Order[0]);
            var ran = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) => ran.Add(step + "/" + t);
            AppShutdownSequence.Run(trigger);
            CollectionAssert.AreEqual(new[]
            {
                AppShutdownStep.RestoreReservedBar + "/" + trigger,
                AppShutdownStep.StopFreezeWatchdog + "/" + trigger,
            }, ran, "세션 종료는 처리 직후 프로세스가 끊긴다 — 워치독 합류 대기가 원복보다 먼저 오면 원복이 누락된다.");
        }

        [Test]
        public void 세션_종료에서는_워치독_합류를_기다리지_않는다()
        {
            Assert.AreEqual(0, AppShutdownSequence.WatchdogJoinMilliseconds(AppShutdownTrigger.SessionEnding));
            Assert.Greater(AppShutdownSequence.WatchdogJoinMilliseconds(AppShutdownTrigger.ApplicationQuitting), 0);
        }

        [Test]
        public void 한_단계가_던져도_다음_단계는_돈다()
        {
            var ran = new List<AppShutdownStep>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) =>
            {
                ran.Add(step);
                if (step == AppShutdownStep.RestoreReservedBar) throw new InvalidOperationException("셸이 이미 없다");
            };
            Assert.DoesNotThrow(() => AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding));
            CollectionAssert.AreEqual(new[] { AppShutdownStep.RestoreReservedBar, AppShutdownStep.StopFreezeWatchdog }, ran);
        }

        // ------------------------------------------------------------------ 멱등 · 셸 선종료 (c)(d)

        [Test]
        public void 세션_종료_원복_뒤_quitting이_또_와도_시스템에_두_번_쓰지_않는다()
        {
            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession, "전제: 기동이 자동 숨김을 해제했어야 한다.");
            Assert.AreEqual(1, control.WriteCount);

            AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding);
            Assert.AreEqual(2, control.WriteCount, "세션 종료에서 원복이 한 번 일어나야 한다.");
            Assert.IsTrue(control.AutoHide, "사용자의 원래 설정(자동 숨김 켜짐)으로 돌아와야 한다.");
            Assert.AreEqual(ReservedBarLedgerState.Closed, ReservedBarRestoreLedger.Read(control.PlatformTag, out _));

            AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);
            Assert.AreEqual(2, control.WriteCount, "이미 원복했으면 두 번째 경로는 시스템에 쓰지 않는다.");
            Assert.AreEqual(ReservedBarLedgerState.Closed, ReservedBarRestoreLedger.Read(control.PlatformTag, out _));
        }

        [Test]
        public void 셸이_먼저_끝나_원복이_반영되지_않으면_흔적을_열어_둔다()
        {
            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);
            control.WriteSilentlyFails = true;   // 탐색기가 먼저 종료돼 요청이 반영되지 않는다

            AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding);
            Assert.AreEqual(ReservedBarLedgerState.Open, ReservedBarRestoreLedger.Read(control.PlatformTag, out _),
                "원복을 확인하지 못했으면 흔적을 닫으면 안 된다 — 다음 실행이 먼저 갚는다(기존 설계).");
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession, "실패했으면 뒤따르는 경로가 다시 시도할 수 있어야 한다.");

            int writesBefore = control.WriteCount;
            AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);
            Assert.Greater(control.WriteCount, writesBefore, "뒤따르는 quitting 경로가 한 번 더 시도해야 한다.");
        }

        // ------------------------------------------------------------------ 배선 (소스)

        private static string ReadCode(params string[] parts)
        {
            string path = Path.Combine(ScriptsRoot, Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다({path}).");
            var sb = new StringBuilder();
            foreach (string raw in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw;
                int quotes = 0, cut = -1;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) quotes++;
                    if (line[i] == '/' && line[i + 1] == '/' && quotes % 2 == 0) { cut = i; break; }
                }
                sb.Append(cut >= 0 ? line.Substring(0, cut) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static int Count(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        [Test]
        public void 트레이_프로시저가_세션_종료를_동기로_처리하고_옵트아웃에도_수신자가_있다()
        {
            string tray = ReadCode("Platform", "Windows", "WindowsSystemTrayIcon.cs");
            string branch = nameof(SessionEndPolicy) + "." + nameof(SessionEndPolicy.IsSessionEndingNow) + "(";
            string run = nameof(AppShutdownSequence) + "." + nameof(AppShutdownSequence.Run) + "(" +
                         nameof(AppShutdownTrigger) + "." + nameof(AppShutdownTrigger.SessionEnding) + ")";
            int proc = tray.IndexOf("private static IntPtr WindowProcedure(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(proc, 0, "창 프로시저를 찾지 못했습니다(니들이 썩었다).");
            int b = tray.IndexOf(branch, proc, StringComparison.Ordinal);
            int shell = tray.IndexOf("_shellRestartMessage != 0", proc, StringComparison.Ordinal);
            int def = tray.IndexOf("return DefWindowProc(", proc, StringComparison.Ordinal);
            Assert.GreaterOrEqual(b, 0, "프로시저에 세션 종료 분기가 없다.");
            Assert.GreaterOrEqual(shell, 0, "셸 재시작 분기 니들이 썩었다.");
            Assert.Less(b, shell, "세션 종료 분기는 프로시저 맨 앞이어야 한다.");
            Assert.Less(b, def, "세션 종료 분기가 DefWindowProc 뒤에 있다.");
            Assert.AreEqual(1, Count(tray, run), "세션 종료에서 종료 순서를 정확히 한 번 부른다.");

            int optOut = tray.IndexOf("if (_optOut)\n", StringComparison.Ordinal);
            Assert.GreaterOrEqual(optOut, 0);
            int receiver = tray.IndexOf("EnsureSessionEndReceiverWithoutTray();", optOut, StringComparison.Ordinal);
            Assert.GreaterOrEqual(receiver, 0, "트레이 옵트아웃 경로에 세션 종료 수신 창이 없다.");
            Assert.Less(receiver, tray.IndexOf("return;", optOut, StringComparison.Ordinal),
                "옵트아웃 조기 반환이 수신 창 확보보다 앞에 있다.");
        }

        [Test]
        public void 종료_훅은_종료_순서_한_곳에만_붙는다()
        {
            string sequence = ReadCode("Platform", "AppShutdownSequence.cs");
            Assert.AreEqual(1, Count(sequence, "Application.quitting +="), "종료 순서가 quitting에 정확히 한 번 붙어야 한다.");

            string ensure = nameof(AppShutdownSequence) + "." + nameof(AppShutdownSequence.EnsureQuitHookInstalled) + "()";
            foreach (string[] file in new[]
                     {
                         new[] { "Platform", "ReservedBarRevealDirector.cs" },
                         new[] { "Platform", "FreezeWatchdog.cs" },
                     })
            {
                string code = ReadCode(file);
                StringAssert.Contains(ensure, code, $"{file[1]}: 종료 순서에 합류하지 않는다(양성 대조).");
                StringAssert.DoesNotContain("Application.quitting +=", code,
                    $"{file[1]}: quitting에 따로 붙었다 — 구독 순서라는 우연에 종료 순서가 걸린다(R-1).");
            }
        }
    }
}
