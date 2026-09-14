using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — OS 세션 종료(로그오프·시스템 종료)에서 작업표시줄 자동 숨김 원복이 누락되지 않는가, 그리고
    /// 종료 순서가 "종료 시작 표지 → 원복 → 워치독 정지 → 정상 종료 표지"인가(persona-stress R-1, R-6, 4차). 원칙 3 예외의 조건
    /// ("실행 중에만 + 종료 시 원복")을 평범한 Windows 종료 경로에서도 지키는 장치다 — 경위는 <c>docs/TASKBAR_REVEAL.md</c> §2-2.
    ///
    /// <para>Windows 창 프로시저 자체는 이 머신에서 실행되지 않는다(<c>#if UNITY_STANDALONE_WIN</c>). 그래서 판정·순서·
    /// 멱등은 중립 코드를 <b>실행해</b> 잠그고, 프로시저 배선은 소스로 본다 — ★ 4차(verify-change 3차 V5): 소스는 주석·문자열·보간 구멍을
    /// 전부 지운 코드(<see cref="SourceTextScanner"/>)로 보고, 니들은 공백을 정규화한 정규식이다. 옛 판은 <c>//</c>만 지워, 같은 파일 로그
    /// <c>$"…{SessionExitMarker.IsStarted}…"</c>가 조건 니들을 채웠다.</para>
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

        private string _markerRoot;

        [SetUp]
        public void SetUp()
        {
            ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting("session-end-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            ReservedBarRevealDirector.ResetForTesting();
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            AppShutdownSequence.QuitRequesterOverrideForTesting = null;
            SessionExitMarker.ResetForTesting();
            _markerRoot = null;
        }

        [TearDown]
        public void TearDown()
        {
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            AppShutdownSequence.QuitRequesterOverrideForTesting = null;
            ReservedBarRevealDirector.ResetForTesting();
            ReservedBarRestoreLedger.ResetForTesting();
            SessionExitMarker.ResetForTesting();
            if (_markerRoot != null && Directory.Exists(_markerRoot))
            {
                try { Directory.Delete(_markerRoot, recursive: true); } catch (Exception) { /* 임시 폴더 — 남아도 무해 */ }
            }
        }

        /// <summary>임시 폴더에 우리 폴더와 직전 로그를 만든다.</summary>
        private string PrepareMarkerFolders(out string previousLog)
        {
            _markerRoot = Path.Combine(Path.GetTempPath(), "stickmate-shutdown-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string logs = Path.Combine(_markerRoot, "Logs");
            Directory.CreateDirectory(logs);
            previousLog = Path.Combine(logs, SessionExitMarkerPolicy.PreviousPlayerLogFileName);
            File.WriteAllText(previousLog, "직전 실행 로그\n");
            return Path.Combine(_markerRoot, "FreezeForensics");
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
        public void 트레이를_끈_사용자에게는_세션_종료에서_할_일이_있을_때만_수신_창을_세운다()
        {
            Assert.IsTrue(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: true, reservedBarChangedThisSession: true, sessionExitMarkerStarted: false));
            Assert.IsTrue(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: true, reservedBarChangedThisSession: false, sessionExitMarkerStarted: true),
                "정상 종료 표지가 켜져 있으면 수신자가 필요하다 — 없으면 트레이를 끈 사용자의 매일 밤 종료가 비정상으로 오판된다(R-6).");
            Assert.IsFalse(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: true, reservedBarChangedThisSession: false, sessionExitMarkerStarted: false),
                "세션 종료에서 할 일이 없으면 숨은 창을 만들 이유가 없다.");
            Assert.IsFalse(SessionEndPolicy.NeedsReceiverWithoutTray(trayOptedOut: false, reservedBarChangedThisSession: true, sessionExitMarkerStarted: true),
                "트레이가 켜져 있으면 트레이 호스트 창이 이미 수신자다.");
        }

        /// <summary>
        /// ★ 4차(verify-change 3차 V5) — 트레이 파일이 부르는 판정이 <b>실행 중 사실 두 가지를 실제로 읽는가</b>. 3차에는 트레이 파일이
        /// 사실을 인자로 모았고 표지 인자를 <c>false</c>로 바꿔도 초록이었다. 이제 사실 수집이 중립 함수 안에 있어 실행으로 잰다.
        /// </summary>
        [Test]
        public void V5_트레이를_끈_사용자의_수신_창_판정은_원복_사실과_표지_사실을_실제로_읽는다()
        {
            Assert.IsFalse(ReservedBarRevealDirector.ChangedThisSession, "전제: 이번 실행이 작업표시줄을 바꾸지 않았다.");
            Assert.IsFalse(SessionExitMarker.IsStarted, "전제: 표지가 꺼져 있다.");
            Assert.IsFalse(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow(trayOptedOut: true),
                "할 일이 없는데 수신 창을 세운다(음성 대조).");

            string ours = PrepareMarkerFolders(out string previous);
            SessionExitMarker.RunStartup(ours, 7001, previous, _ => false);
            Assert.IsTrue(SessionExitMarker.IsStarted);
            Assert.IsTrue(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow(trayOptedOut: true),
                "표지가 켜졌는데 수신 창을 세우지 않는다 — 트레이를 끈 사용자의 밤 종료가 매일 비정상으로 오판되고 매일 아침 로그 복사가 난다(V5).");
            Assert.IsFalse(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow(trayOptedOut: false), "트레이가 켜져 있으면 트레이 창이 이미 수신자다.");

            SessionExitMarker.ResetForTesting();
            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession, "전제: 기동이 자동 숨김을 해제했다.");
            Assert.IsTrue(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow(trayOptedOut: true),
                "작업표시줄을 바꿨는데 수신 창을 세우지 않는다 — 로그오프·종료 때 원복이 누락된다.");
        }

        // ------------------------------------------------------------------ 순서 (R-1, R-6, 4차)

        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void 종료_순서는_작업표시줄_원복이_워치독_정지보다_먼저다(AppShutdownTrigger trigger)
        {
            Assert.AreEqual(AppShutdownStep.MarkExitStarted, AppShutdownSequence.Order[0],
                "종료 시작 표지가 맨 앞이어야 원복 도중 끊겨도 '비정상'으로 남지 않는다(4차).");
            Assert.AreEqual(AppShutdownStep.RestoreReservedBar, AppShutdownSequence.Order[1],
                "시스템을 바꾸는 단계 중 원복이 맨 앞이다 — 그 앞에는 동기화 없는 표지 한 줄만 허용된다(R-1).");
            Assert.AreEqual(AppShutdownStep.MarkCleanExit, AppShutdownSequence.Order[AppShutdownSequence.Order.Count - 1],
                "정상 종료 표지는 맨 끝이어야 한다 — 앞 단계가 전부 돈 뒤에야 '정상 종료'다.");
            var ran = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) => ran.Add(step + "/" + t);
            AppShutdownSequence.Run(trigger);
            CollectionAssert.AreEqual(new[]
            {
                AppShutdownStep.MarkExitStarted + "/" + trigger,
                AppShutdownStep.RestoreReservedBar + "/" + trigger,
                AppShutdownStep.StopFreezeWatchdog + "/" + trigger,
                AppShutdownStep.MarkCleanExit + "/" + trigger,
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
            CollectionAssert.AreEqual(new[]
            {
                AppShutdownStep.MarkExitStarted, AppShutdownStep.RestoreReservedBar, AppShutdownStep.StopFreezeWatchdog, AppShutdownStep.MarkCleanExit,
            }, ran);
        }

        /// <summary>
        /// ★ 3차 D (persona-stress R-6) — 두 종료 입구 <b>모두</b> 정상 종료 표지를 남기고, 그래서 다음 실행이 직전 로그를
        /// 복사하지 않는다. 양성 대조로 "종료 순서를 안 돈 실행"은 비정상으로 판정되어 복사가 일어남을 같은 배치에서 보인다.
        /// </summary>
        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void R6_두_종료_입구_모두_정상_종료_표지를_남겨_다음_실행이_비정상으로_오판하지_않는다(AppShutdownTrigger trigger)
        {
            string ours = PrepareMarkerFolders(out string prev);
            Func<int, bool> nobodyAlive = _ => false;

            // 실행 1 — 기동 후 종료 순서를 돈다(실제 실행기: 원복 흔적은 SetUp이 임시 폴더로 돌렸다).
            SessionExitMarker.RunStartup(ours, 1001, prev, nobodyAlive);
            Assert.IsTrue(SessionExitMarker.IsStarted);
            AppShutdownSequence.Run(trigger);

            // 실행 2 — 정상 종료로 판정되어 복사가 없다.
            SessionExitMarker.StartupResult second = SessionExitMarker.RunStartup(ours, 1002, prev, nobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, second.Verdict,
                $"{trigger} 경로가 정상 종료 표지를 남기지 않았다 — 매일 밤 Windows 종료가 비정상으로 오판된다(R-6).");
            Assert.IsNull(second.CopiedFileName);
            Assert.IsFalse(File.Exists(Path.Combine(ours, SessionExitMarkerPolicy.CopySlotFileName(0))));

            // 양성 대조 — 실행 2가 종료 순서를 돌지 않고 끝났다면(크래시) 실행 3은 비정상으로 보고 복사한다.
            SessionExitMarker.StartupResult third = SessionExitMarker.RunStartup(ours, 1003, prev, nobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.AbnormalExit, third.Verdict, "대조 실패 — 판정기가 모든 실행을 정상으로 본다.");
            Assert.AreEqual(SessionExitMarkerPolicy.CopySlotFileName(0), third.CopiedFileName);
        }

        /// <summary>
        /// ★ 4차 — 원복 단계에서 멈춘 채 끊긴 실행(추정 경로: 셸이 먼저 내려가 동기 셸 호출이 돌아오지 않은 채 세션 종료로 강제 종료)은
        /// "종료 시작"을 남기고, 다음 실행은 그것을 <b>비정상 종료로 보지 않으며 복사하지 않는다</b>. 단계 실행기를 가로채 "원복에서
        /// 프로세스가 끊겼다"를 흉내 낸다 — 그 뒤 단계는 하나도 돌지 않는다. 종료 시작 표지가 원복 <b>뒤</b>로 옮겨지면 이 테스트가 빨개진다.
        /// </summary>
        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void R5_원복_단계에서_끊긴_실행은_종료_시작으로_남아_다음_실행이_비정상으로_보지_않는다(AppShutdownTrigger trigger)
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 2001, prev, _ => false);

            bool killed = false;
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) =>
            {
                if (killed) return;   // 프로세스가 끊긴 뒤에는 어떤 단계도 돌지 않는다.
                switch (step)
                {
                    case AppShutdownStep.RestoreReservedBar: killed = true; return;   // 셸 호출이 돌아오지 않은 채 강제 종료.
                    case AppShutdownStep.MarkExitStarted: SessionExitMarker.WriteExitStarted(t); return;
                    case AppShutdownStep.MarkCleanExit: SessionExitMarker.WriteCleanExit(t); return;
                }
            };
            AppShutdownSequence.Run(trigger);
            Assert.IsTrue(killed, "전제: 흉내 낸 강제 종료가 실제로 원복 단계에서 일어났어야 한다.");

            SessionExitMarker.StartupResult next = SessionExitMarker.RunStartup(ours, 2002, prev, _ => false);
            Assert.AreEqual(PreviousSessionVerdict.ExitStartedNotFinished, next.Verdict,
                "원복 도중 끊긴 실행이 '실행 중'으로 남았다 — 종료 시작 표지가 원복보다 뒤에 있거나 빠졌다. 셸이 먼저 내려가는 사용자의 밤 종료가 비정상으로 오판된다.");
            Assert.IsNull(next.CopiedFileName, "종료 중 끊김은 세션 중 멈춤이 아니다 — 복사하지 않는다.");
        }

        // ------------------------------------------------------------------ X1 — WM_ENDSESSION lParam · 살아남는 경로 (4차)

        [Test]
        public void X1_세션_종료_플래그_값은_MS_문서_값이고_설명은_비트마스크로_읽는다()
        {
            // 외부 ABI 상수(WM_ENDSESSION 문서의 lParam 표) — 튜닝 값이 아니다.
            Assert.AreEqual(0x1L, SessionEndPolicy.EndSessionCloseApp);
            Assert.AreEqual(0x40000000L, SessionEndPolicy.EndSessionCritical);
            Assert.AreEqual(0x80000000L, SessionEndPolicy.EndSessionLogoff);

            StringAssert.Contains("알 수 없음", SessionEndPolicy.DescribeEndSessionFlags(0), "0은 시스템 종료·재시작(구별 불가).");
            StringAssert.Contains("ENDSESSION_LOGOFF", SessionEndPolicy.DescribeEndSessionFlags(SessionEndPolicy.EndSessionLogoff));
            string both = SessionEndPolicy.DescribeEndSessionFlags(SessionEndPolicy.EndSessionCloseApp | SessionEndPolicy.EndSessionLogoff);
            StringAssert.Contains("ENDSESSION_CLOSEAPP", both, "비트마스크다 — 같음 비교로 읽으면 겹친 플래그를 놓친다.");
            StringAssert.Contains("ENDSESSION_LOGOFF", both);
            StringAssert.Contains("ENDSESSION_LOGOFF", SessionEndPolicy.DescribeEndSessionFlags(unchecked((long)0xFFFFFFFF80000000UL)),
                "64비트 LPARAM 부호 확장이 와도 같은 플래그로 읽어야 한다.");
            StringAssert.Contains("알 수 없는 비트", SessionEndPolicy.DescribeEndSessionFlags(0x00000100L));
        }

        /// <summary>
        /// ★ 4차 X1 — 세션 종료를 처리하면 lParam 플래그와 무관하게 종료 순서를 <b>끝까지</b> 돈 뒤 앱 종료를 <b>한 번</b> 요청한다.
        /// Restart Manager(<c>ENDSESSION_CLOSEAPP</c>)나 종료 취소로 프로세스가 살아남으면, 원복된 작업표시줄·"정상 종료" 표지를 든 채
        /// 계속 도는 실행이 남는다 — 그 상태를 없애는 장치다. 한 단계가 던져도 종료 요청은 온다.
        /// </summary>
        [TestCase(0L)]
        [TestCase(SessionEndPolicy.EndSessionCloseApp)]
        [TestCase(SessionEndPolicy.EndSessionCritical)]
        [TestCase(SessionEndPolicy.EndSessionLogoff)]
        public void X1_세션_종료를_처리하면_어떤_플래그든_종료_순서를_끝까지_돈_뒤_앱_종료를_한_번_요청한다(long flags)
        {
            var events = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) =>
            {
                events.Add(step + "/" + t);
                if (step == AppShutdownStep.RestoreReservedBar) throw new InvalidOperationException("셸이 이미 없다");
            };
            AppShutdownSequence.QuitRequesterOverrideForTesting = () => events.Add("quit");

            AppShutdownSequence.HandleSessionEnding(flags);

            CollectionAssert.AreEqual(new[]
            {
                AppShutdownStep.MarkExitStarted + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.RestoreReservedBar + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.StopFreezeWatchdog + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.MarkCleanExit + "/" + AppShutdownTrigger.SessionEnding,
                "quit",
            }, events, "종료 요청이 빠졌거나 순서보다 앞섰다 — 살아남은 실행이 원복된 작업표시줄·정상 종료 표지를 든 채 계속 돈다(X1).");
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

        // ------------------------------------------------------------------ 배선 (소스 — 주석·문자열·보간을 지운 코드)

        private static string SourcePath(params string[] parts) => Path.Combine(ScriptsRoot, Path.Combine(parts));

        private static string ReadCode(params string[] parts)
        {
            string path = SourcePath(parts);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다({path}).");
            return SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(path).Replace("\r\n", "\n"), null, blankInterpolationHoles: true);
        }

        private static string Member(string owner, string member) => owner + @"\s*\.\s*" + member;

        [Test]
        public void 트레이_프로시저가_세션_종료를_동기로_처리하고_옵트아웃에도_수신자가_있다()
        {
            string trayPath = SourcePath("Platform", "Windows", "WindowsSystemTrayIcon.cs");
            string tray = ReadCode("Platform", "Windows", "WindowsSystemTrayIcon.cs");

            // (1) 창 프로시저 맨 앞에서 세션 종료를 동기로 처리한다.
            string proc = SourceTextScanner.BlockMemberBody(tray, "private static IntPtr WindowProcedure(");
            Assert.IsNotNull(proc, "창 프로시저를 찾지 못했습니다(니들이 썩었다).");
            Match branch = Regex.Match(proc, Member(nameof(SessionEndPolicy), nameof(SessionEndPolicy.IsSessionEndingNow)) + @"\s*\(");
            int shell = proc.IndexOf("_shellRestartMessage", StringComparison.Ordinal);
            int def = proc.IndexOf("DefWindowProc", StringComparison.Ordinal);
            Assert.IsTrue(branch.Success, "프로시저에 세션 종료 분기가 없다.");
            Assert.GreaterOrEqual(shell, 0, "셸 재시작 분기 니들이 썩었다.");
            Assert.GreaterOrEqual(def, 0, "DefWindowProc 니들이 썩었다.");
            Assert.Less(branch.Index, shell, "세션 종료 분기는 프로시저 맨 앞이어야 한다.");
            Assert.Less(branch.Index, def, "세션 종료 분기가 DefWindowProc 뒤에 있다.");
            Assert.AreEqual(1, Regex.Matches(proc, Member(nameof(AppShutdownSequence), nameof(AppShutdownSequence.HandleSessionEnding)) +
                    @"\s*\(\s*lParam\s*\.\s*ToInt64\s*\(\s*\)\s*\)").Count,
                "세션 종료에서 처리 진입점(종료 순서 + 앱 종료 요청)을 lParam과 함께 정확히 한 번 부른다(4차 X1).");
            Assert.AreEqual(0, Regex.Matches(tray, Member(nameof(AppShutdownSequence), nameof(AppShutdownSequence.Run)) + @"\s*\(").Count,
                "트레이가 순서만 돌고 앱 종료 요청을 건너뛴다 — 살아남으면 원복된 채 도는 실행이 남는다(X1).");

            // (2) 옵트아웃 조기 반환보다 앞에서 수신 창을 확보한다.
            string tick = SourceTextScanner.BlockMemberBody(tray, "internal static void Tick(");
            Assert.IsNotNull(tick, "Tick 본문을 찾지 못했습니다.");
            //   `if (_optOut) {`는 Tick 안에 둘이다(옵트아웃 안내 로그 / 조기 반환 분기) — 조기 반환이 있는 블록을 골라 그 안을 본다.
            var earlyReturnBlocks = new List<string>();
            foreach (Match optOut in Regex.Matches(tick, @"if\s*\(\s*_optOut\s*\)\s*\{"))
            {
                string block = SourceTextScanner.BlockMemberBody(tick.Substring(optOut.Index), "if");
                Assert.IsNotNull(block, "옵트아웃 분기 블록의 끝을 찾지 못했다.");
                if (Regex.IsMatch(block, @"return\s*;")) earlyReturnBlocks.Add(block);
            }
            Assert.AreEqual(1, earlyReturnBlocks.Count, "옵트아웃 조기 반환 분기가 정확히 하나가 아니다(니들이 썩었거나 분기가 늘었다).");
            Match receiverCall = Regex.Match(earlyReturnBlocks[0], @"EnsureSessionEndReceiverWithoutTray\s*\(\s*\)\s*;");
            Match early = Regex.Match(earlyReturnBlocks[0], @"return\s*;");
            Assert.IsTrue(receiverCall.Success, "트레이 옵트아웃 경로에 세션 종료 수신 창이 없다.");
            Assert.Less(receiverCall.Index, early.Index, "옵트아웃 조기 반환이 수신 창 확보보다 앞에 있다.");

            // (3) ★ V5 — 수신 창 판정은 중립 함수 하나에 옵트아웃 여부만 넘긴다(사실 수집은 중립 함수가 한다 — 실행 테스트가 잠근다).
            string receiver = SourceTextScanner.BlockMemberBody(tray, "private static void EnsureSessionEndReceiverWithoutTray()");
            Assert.IsNotNull(receiver, "수신 창 확보 본문을 찾지 못했습니다.");
            Match decision = Regex.Match(receiver, @"if\s*\(\s*!\s*" +
                Member(nameof(SessionEndPolicy), nameof(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow)) + @"\s*\(\s*_optOut\s*\)\s*\)\s*return\s*;");
            Assert.IsTrue(decision.Success,
                "수신 창 판정이 중립 함수에 옵트아웃 여부만 넘기는 형태가 아니다 — 트레이가 사실을 직접 모으면 하나를 빠뜨려도 초록이던 구멍(V5)이 다시 열린다.");
            int host = receiver.IndexOf("EnsureHostWindow", StringComparison.Ordinal);
            Assert.Greater(host, decision.Index, "판정보다 먼저 숨은 창을 만든다.");
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(tray, nameof(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow)),
                "중립 판정이 한 번이 아니다.");
            Assert.AreEqual(0, SourceTextScanner.CountIdentifier(tray, nameof(SessionEndPolicy.NeedsReceiverWithoutTray)),
                "트레이가 규칙을 직접 부르며 사실을 인자로 모은다 — V5 구멍이 다시 열린다.");

            // (4) 대조 — 같은 파일의 로그 보간에는 표지 사실이 실재하지만(원문), 지운 코드에는 남지 않는다(로그가 니들을 못 채운다).
            string raw = File.ReadAllText(trayPath);
            StringAssert.Contains(nameof(SessionExitMarker) + "." + nameof(SessionExitMarker.IsStarted), raw,
                "대조 전제: 로그 보간에 표지 사실이 있어야 이 대조가 공허하지 않다.");
            Assert.AreEqual(0, SourceTextScanner.CountMemberAccess(tray, nameof(SessionExitMarker.IsStarted)),
                "지운 코드에 표지 사실이 남았다 — 스캐너가 보간 구멍을 못 지웠거나, 트레이가 사실을 직접 읽는다.");
        }

        [Test]
        public void 종료_훅은_종료_순서_한_곳에만_붙는다()
        {
            string sequence = ReadCode("Platform", "AppShutdownSequence.cs");
            Assert.AreEqual(1, QuittingSubscription.Matches(sequence).Count, "종료 순서가 quitting에 정확히 한 번 붙어야 한다.");

            var ensure = new Regex(Member(nameof(AppShutdownSequence), nameof(AppShutdownSequence.EnsureQuitHookInstalled)) + @"\s*\(\s*\)");
            foreach (string[] file in new[]
                     {
                         new[] { "Platform", "ReservedBarRevealDirector.cs" },
                         new[] { "Platform", "FreezeWatchdog.cs" },
                     })
            {
                string code = ReadCode(file);
                Assert.IsTrue(ensure.IsMatch(code), $"{file[1]}: 종료 순서에 합류하지 않는다(양성 대조).");
                Assert.AreEqual(0, QuittingSubscription.Matches(code).Count,
                    $"{file[1]}: quitting에 따로 붙었다 — 구독 순서라는 우연에 종료 순서가 걸린다(R-1).");
            }
        }

        // ------------------------------------------------------------------ 종료 구독 대장 (3차 — "quitting 구독 1곳" 정정, 4차 — 표기 변형)

        private enum SessionEndDecision
        {
            /// <summary>종료 순서 자체 — 세션 종료에서도 돈다.</summary>
            SequenceItself,
            /// <summary>정상 종료에서만 돈다(세션 종료 경로에 넣지 않기로 결정 — 사유 필수).</summary>
            QuitOnly,
            /// <summary>정상 종료에서만 도는데 세션 종료에서도 돌아야 할 수 있다 — 결정 미정(러너에 건너뜀으로 띄운다).</summary>
            OpenGap,
        }

        private const string QuittingKind = "quitting +=";
        private const string WantsToQuitKind = "wantsToQuit +=";
        private const string ProcessExitKind = "ProcessExit +=";
        private const string OnApplicationQuitKind = "void OnApplicationQuit()";
        private const string EventNameReferenceKind = "종료 이벤트 이름(문자열·nameof — 리플렉션 구독 우회)";

        /// <summary>
        /// ★ 종료 구독 대장. 키 = "파일 이름|종류". <b>프로덕션 전체(Assets, 테스트 제외)에서 실제로 찾은 목록과 양방향으로
        /// 같아야 한다</b> — 새 구독이 생기면 결정 없이 넘어갈 수 없고, 사라진 구독은 대장에서 지워야 한다.
        /// </summary>
        private static readonly Dictionary<string, (SessionEndDecision Decision, string Why)> QuitHookLedger =
            new Dictionary<string, (SessionEndDecision, string)>(StringComparer.Ordinal)
            {
                ["AppShutdownSequence.cs|" + QuittingKind] = (SessionEndDecision.SequenceItself,
                    "종료 시작 표지 → 작업표시줄 원복 → 워치독 정지 → 정상 종료 표지. WM_ENDSESSION도 같은 Run을 부른다."),
                ["WindowsSystemTrayIcon.cs|" + QuittingKind] = (SessionEndDecision.QuitOnly,
                    "알림 아이콘 제거 + 숨은 호스트 창 파괴. 세션 종료면 탐색기(알림 영역)도 함께 끝나 유령 아이콘이 남을 곳이 없다. " +
                    "세션 종료 통보를 받는 창이 바로 이 창이라 자기 프로시저 안에서 DestroyWindow를 부르게 되고, " +
                    "승인 조건 5는 DestroyWindow를 quitting 경로로만 한정한다."),
                ["WindowsTaskbarButtonRemover.cs|" + QuittingKind] = (SessionEndDecision.QuitOnly,
                    "ITaskbarList COM 참조 해제뿐(창 상태 무변경). 세션 종료면 작업표시줄 자체가 끝나고 프로세스 종료가 참조를 거둔다. " +
                    "세션 종료의 동기 구간에 COM 호출을 넣지 않는다(보낸 메시지 처리 중 외부 COM 호출 거부 가능성 — 미확인)."),
                ["WindowsSystemAudioActivityProbe.cs|" + QuittingKind] = (SessionEndDecision.QuitOnly,
                    "IMMDeviceEnumerator COM 참조 해제뿐. 남기는 시스템 상태가 없다 — 위와 같은 이유로 세션 종료 구간에 넣지 않는다."),
                ["WindowsVirtualDesktopProbe.cs|" + QuittingKind] = (SessionEndDecision.QuitOnly,
                    "IVirtualDesktopManager COM 참조 해제뿐. 남기는 시스템 상태가 없다 — 위와 같은 이유로 세션 종료 구간에 넣지 않는다."),
                ["SteamPackEntitlementSource.cs|" + QuittingKind] = (SessionEndDecision.QuitOnly,
                    "SteamAPI.Shutdown(서드파티 네이티브). 세션 종료면 스팀 클라이언트도 함께 끝난다. 짧은 동기 구간에 서드파티 호출을 넣지 않는다. " +
                    "현재 STICKMATE_STEAMWORKS_INSTALLED 미정의라 컴파일 대상도 아니다."),
                ["CharacterProgressionDirector.cs|" + OnApplicationQuitKind] = (SessionEndDecision.OpenGap,
                    "종료 직전 마지막 저장(IsAnythingDirty면 CharacterSaveStore.Save). Unity가 로그오프·시스템 종료에서 OnApplicationQuit을 " +
                    "부르는지 실기 미확인이고, 안 부르면 주기 저장 사이의 진행(주석상 최대 1분)이 날아간다. Interaction/은 coder 소유라 " +
                    "이번 라운드(dev-platform)에서 옮기지 않았다 — 리더 배정 대기(3차 커밋 뒤 coder 배정 예정)."),
            };

        /// <summary>
        /// 이벤트 구독. 한정자 무관(<c>Application.quitting</c> · <c>using</c> 별칭 <c>App.quitting</c> · <c>using static UnityEngine.Application;</c> 뒤의
        /// 맨 <c>quitting</c>) — 끝은 이벤트 이름이다(4차, verify-change 3차 V6). 앞뒤가 식별자 문자면 다른 이름이다(<c>_quitting</c>, <c>quittingCount</c>).
        /// </summary>
        private static readonly Regex QuittingSubscription = new Regex(@"(?<![\w@])@?quitting\s*\+=", RegexOptions.CultureInvariant);
        private static readonly Regex WantsToQuitSubscription = new Regex(@"(?<![\w@])@?wantsToQuit\s*\+=", RegexOptions.CultureInvariant);
        private static readonly Regex ProcessExitSubscription = new Regex(@"(?<![\w@])@?ProcessExit\s*\+=", RegexOptions.CultureInvariant);
        private static readonly Regex OnApplicationQuitDeclaration = new Regex(@"(?<![\w@])void\s+OnApplicationQuit\s*\(\s*\)", RegexOptions.CultureInvariant);
        private static readonly Regex QuitEventNameOf = new Regex(@"nameof\s*\(\s*(?:@?\w+\s*\.\s*)*@?(?:quitting|wantsToQuit|ProcessExit)\s*\)", RegexOptions.CultureInvariant);
        private static readonly string[] QuitEventNames = { "quitting", "wantsToQuit", "ProcessExit" };

        /// <summary>
        /// 소스 한 파일의 종료 훅 목록(키 "파일|종류", 중복 허용). 주석·문자열·보간 구멍을 지운 코드에서 센다.
        ///
        /// <para><b>★ 텍스트 감사가 원리상 못 보는 형태(조용히 두지 않고 적는다).</b>
        /// (1) 계산된 이름의 리플렉션 구독(<c>GetEvent("quit" + "ting")</c>, 이벤트 목록 순회) — 평문 이름·<c>nameof</c>만 잡는다.
        /// (2) <c>OnDisable</c>/<c>OnDestroy</c> 등 종료 때도 불리는 일반 수명 콜백 — 이름으로는 "종료 의도"를 가를 수 없다.
        /// (3) 네이티브 플러그인·Unity 내부의 종료 처리, <c>Packages/</c> 등 Assets 밖 코드 — 스캔 범위 밖.
        /// (4) 표현식 트리·<c>dynamic</c>으로 조립한 구독.</para>
        /// </summary>
        private static IEnumerable<string> FindQuitHooks(string fileName, string source)
        {
            var literals = new List<string>();
            string code = SourceTextScanner.BlankCommentsAndStrings(source, literals, blankInterpolationHoles: true);
            for (int k = QuittingSubscription.Matches(code).Count; k > 0; k--) yield return fileName + "|" + QuittingKind;
            for (int k = WantsToQuitSubscription.Matches(code).Count; k > 0; k--) yield return fileName + "|" + WantsToQuitKind;
            for (int k = ProcessExitSubscription.Matches(code).Count; k > 0; k--) yield return fileName + "|" + ProcessExitKind;
            for (int k = OnApplicationQuitDeclaration.Matches(code).Count; k > 0; k--) yield return fileName + "|" + OnApplicationQuitKind;
            int names = QuitEventNameOf.Matches(code).Count;
            foreach (string literal in literals)
            {
                if (Array.IndexOf(QuitEventNames, literal) >= 0) names++;
            }
            for (int k = names; k > 0; k--) yield return fileName + "|" + EventNameReferenceKind;
        }

        private static List<string> FindQuitHooksInProduction()
        {
            var found = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                found.AddRange(FindQuitHooks(Path.GetFileName(file), File.ReadAllText(file)));
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        [Test]
        public void 종료_구독_대장은_프로덕션의_모든_종료_훅과_일치하고_결정마다_사유가_있다()
        {
            List<string> found = FindQuitHooksInProduction();
            Assert.Contains("AppShutdownSequence.cs|" + QuittingKind, found, "양성 대조 실패 — 스캐너가 종료 순서 자신의 구독을 못 본다.");

            var expected = new List<string>(QuitHookLedger.Keys);
            expected.Sort(StringComparer.Ordinal);
            CollectionAssert.AreEqual(expected, found,
                "종료 훅 목록이 대장과 다릅니다. 새 훅이면 대장에 결정(세션 종료 경로에 넣는가)과 사유를 적으세요 — " +
                "Windows 로그오프·시스템 종료에서는 quitting이 돈다는 보장이 없습니다(docs/TASKBAR_REVEAL.md §2-2).\n실제: " +
                string.Join(", ", found));

            foreach (KeyValuePair<string, (SessionEndDecision Decision, string Why)> entry in QuitHookLedger)
            {
                Assert.GreaterOrEqual(entry.Value.Why?.Length ?? 0, 20, $"{entry.Key}: 결정 사유가 비었거나 너무 짧습니다.");
            }
        }

        [Test]
        public void 스캐너는_표기_변형의_종료_구독을_세고_주석과_문자열_속은_세지_않는다()
        {
            string snippet =
                "// Application.quitting += A;\n" +
                "var s = \"Application.quitting += B\";\n" +
                "Debug.Log($\"{(flag ? \"quitting += X\" : \"y\")}\");\n" +
                "UnityEngine.Application . quitting\n   += C;\n" +
                "using static UnityEngine.Application;\nquitting += D;\n" +
                "App.quitting += E;\n" +
                "int quittingCount = 0; quittingCount += 1; _quitting += 1;\n" +
                "Application.quitting -= J;\n" +
                "Application.wantsToQuit += F;\n" +
                "AppDomain.CurrentDomain.ProcessExit += G;\n" +
                "typeof(Application).GetEvent(\"quitting\").AddEventHandler(null, H);\n" +
                "typeof(Application).GetEvent(nameof(Application.quitting)).AddEventHandler(null, I);\n" +
                "void OnApplicationQuit() { }\n";
            var hooks = new List<string>(FindQuitHooks("S.cs", snippet));
            Assert.AreEqual(3, hooks.FindAll(h => h == "S.cs|" + QuittingKind).Count, "C(공백·줄바꿈)·D(using static)·E(별칭) 셋이어야 한다: " + string.Join(", ", hooks));
            Assert.AreEqual(1, hooks.FindAll(h => h == "S.cs|" + WantsToQuitKind).Count);
            Assert.AreEqual(1, hooks.FindAll(h => h == "S.cs|" + ProcessExitKind).Count);
            Assert.AreEqual(1, hooks.FindAll(h => h == "S.cs|" + OnApplicationQuitKind).Count);
            Assert.AreEqual(2, hooks.FindAll(h => h == "S.cs|" + EventNameReferenceKind).Count, "평문 이름 H·nameof I");
            Assert.AreEqual(8, hooks.Count, "주석(A)·문자열(B)·보간 속 문자열(X)·다른 이름·구독 해제(J)는 세지 않는다: " + string.Join(", ", hooks));
        }

        /// <summary>
        /// ★ 4차 — 본문 추출(<see cref="SourceTextScanner.BlockMemberBody"/>)이 보간 문자열의 중괄호에 흔들리지 않는다. 첫 판은 구멍을 여는 '{'를
        /// 코드에 남겨 보간 문자열 하나마다 짝 없는 중괄호가 생겼고, 로그가 많은 적합 틱·트레이 Tick의 끝을 찾지 못했다(두 감사가 빨개져 발견).
        /// </summary>
        [Test]
        public void 스캐너_본문_추출은_보간_문자열과_구멍_속_람다의_중괄호에_흔들리지_않는다()
        {
            string source =
                "class C\n{\n" +
                "    void F()\n    {\n" +
                "        Debug.Log($\"a {x} b {(y ? \"}\" : \"{\")} c {{literal}} d {items.Select(i => { return i; }).Count()}\");\n" +
                "        var v = $@\"{p}\"\"{q}\";\n" +
                "        if (z) { Call('{'); }\n" +
                "    }\n" +
                "    void G() { }\n}\n";
            foreach (bool blankHoles in new[] { false, true })
            {
                string code = SourceTextScanner.BlankCommentsAndStrings(source, null, blankHoles);
                Assert.AreEqual(source.Length, code.Length, "지운 코드의 길이(위치)가 원문과 같아야 한다.");
                string body = SourceTextScanner.BlockMemberBody(code, "void F()");
                Assert.IsNotNull(body, $"본문 끝을 찾지 못했다(구멍 지움={blankHoles}).");
                StringAssert.DoesNotContain("void G()", body, $"본문이 다음 멤버까지 넘쳤다(구멍 지움={blankHoles}).");
                StringAssert.Contains("Call(", body, "본문이 너무 일찍 끝났다.");
                Assert.AreEqual(SourceTextScanner.CountIdentifier(body, "Select"), blankHoles ? 0 : 1,
                    "구멍 속 코드는 기본 모드에서 남고, 배선 감사 모드에서 지워져야 한다.");
            }
        }

        [Test]
        public void 갭추적_세션_종료에서_진행_저장이_도는지_결정되지_않았다()
        {
            var open = new List<string>();
            foreach (KeyValuePair<string, (SessionEndDecision Decision, string Why)> entry in QuitHookLedger)
            {
                if (entry.Value.Decision == SessionEndDecision.OpenGap) open.Add(entry.Key + " — " + entry.Value.Why);
            }
            if (open.Count == 0) Assert.Pass("세션 종료 결정이 열린 종료 훅이 없다.");
            Assert.Ignore("세션 종료 경로 미결정 " + open.Count + "건: " + string.Join(" | ", open));
        }
    }
}
