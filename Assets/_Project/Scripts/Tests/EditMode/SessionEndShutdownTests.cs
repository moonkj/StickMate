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
    /// 종료 순서가 "종료 시작 표지 → 원복 → 워치독 정지 → 진행 저장 → 정상 종료 표지"인가(persona-stress R-1, R-6, 4차, 5차). 원칙 3 예외의 조건
    /// ("실행 중에만 + 종료 시 원복")을 평범한 Windows 종료 경로에서도 지키는 장치다 — 경위는 <c>docs/TASKBAR_REVEAL.md</c> §2-2.
    ///
    /// <para>Windows 창 프로시저 자체는 이 머신에서 실행되지 않는다(<c>#if UNITY_STANDALONE_WIN</c>). 그래서 판정·순서·
    /// 재실행 동작은 중립 코드를 <b>실행해</b> 잠그고, 프로시저 배선은 소스로 본다 — ★ 4차(verify-change 3차 V5): 소스는 주석·문자열·보간 구멍을
    /// 전부 지운 코드(<see cref="SourceTextScanner"/>)로 보고, 니들은 공백을 정규화한 정규식이다. 옛 판은 <c>//</c>만 지워, 같은 파일 로그
    /// <c>$"…{SessionExitMarker.IsStarted}…"</c>가 조건 니들을 채웠다.</para>
    ///
    /// <para>★ 5차(verify-change 4차 V5g·X1g): 4차 배선 감사는 "판정 줄이 있다"만 봐서, 그 줄 <b>앞에</b> 조건 한 줄을 넣거나(V5g) 호출에
    /// lParam 조건을 붙여도(X1g) 전량 초록이었다. 결정 경로 전체를 중립 함수(<see cref="SessionEndReceiverGate"/>,
    /// <see cref="AppShutdownSequence.TryHandleSessionEndMessage"/>)로 옮겨 <b>실행으로</b> 잠그고, 트레이에 남은 몇 줄은 <b>모양 전체</b>(공백 무관)를
    /// 허용 형태와 비교한다 — "그 줄이 있다"가 아니라 "그 줄 말고 아무것도 없다"를 본다.</para>
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
            AppShutdownSequence.ResetSaveHandlerForTesting();
            SessionEndReceiverGate.ResetForTesting();
            SessionExitMarker.ResetForTesting();
            _markerRoot = null;
        }

        [TearDown]
        public void TearDown()
        {
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            AppShutdownSequence.QuitRequesterOverrideForTesting = null;
            AppShutdownSequence.ResetSaveHandlerForTesting();
            SessionEndReceiverGate.ResetForTesting();
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

        private static string MarkerText(string ours) => File.ReadAllText(Path.Combine(ours, SessionExitMarkerPolicy.MarkerFileName));

        private static string MarkerStateOrNull(string ours)
        {
            string path = Path.Combine(ours, SessionExitMarkerPolicy.MarkerFileName);
            if (!File.Exists(path)) return null;
            return SessionExitMarkerPolicy.TryParse(File.ReadAllText(path), out string state, out _) ? state : null;
        }

        /// <summary>
        /// 표지 한 줄의 <c>trigger=</c> 칸 값. 키 이름은 디스크 형식이고 Windows 체크표 §R이 <b>그 글자로</b> 판독한다 — 그래서 여기서도 글자로
        /// 잠근다(형식이 바뀌면 체크표도 함께 바뀌어야 한다). 칸이 없으면 null(아래 단언이 "칸이 없다"로 시끄럽게 빨개진다).
        /// </summary>
        private static string TriggerOf(string markerText)
        {
            foreach (string token in markerText.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("trigger=", StringComparison.Ordinal)) return token.Substring("trigger=".Length);
            }
            return null;
        }

        private static void AssertMarker(string text, string expectedState, int expectedPid, AppShutdownTrigger expectedTrigger, string because)
        {
            Assert.IsNotNull(text, "표지를 읽지 못했다 — " + because);
            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(text, out string state, out int pid), $"표지를 해석하지 못했다({text.Trim()}) — {because}");
            Assert.AreEqual(expectedState, state, $"표지 상태({text.Trim()}) — {because}");
            Assert.AreEqual(expectedPid, pid, $"표지 pid({text.Trim()}) — {because}");
            string trigger = TriggerOf(text);
            Assert.IsNotNull(trigger, $"표지에 trigger= 칸이 없다({text.Trim()}) — 형식이 바뀌었으면 체크표 §R 판독도 함께 바뀌어야 한다.");
            Assert.AreEqual(expectedTrigger.ToString(), trigger, $"표지 trigger({text.Trim()}) — {because}");
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

        /// <summary>
        /// ★ 5차(verify-change 4차 X1g) — 창 프로시저의 세션 종료 분기. <c>WM_ENDSESSION</c> + wParam 참이면 <b>lParam이 무엇이든</b>
        /// (시스템 종료·재시작 0 / Restart Manager / 강제 종료 / 로그오프 / 겹친 비트 / 64비트 부호 확장) 세션 종료 처리가 <b>정확히 한 번</b>
        /// 돈다 — 순서 다섯 단계가 한 번씩, 앱 종료 요청이 한 번. 4차에는 트레이 줄에 <c>if (lParam.ToInt64() == 0)</c>를 붙여 로그오프·
        /// Restart Manager 원복이 빠져도 초록이었다.
        /// </summary>
        [TestCase(0L)]
        [TestCase(SessionEndPolicy.EndSessionCloseApp)]
        [TestCase(SessionEndPolicy.EndSessionCritical)]
        [TestCase(SessionEndPolicy.EndSessionLogoff)]
        [TestCase(SessionEndPolicy.EndSessionCloseApp | SessionEndPolicy.EndSessionLogoff)]
        [TestCase(SessionEndPolicy.EndSessionCritical | SessionEndPolicy.EndSessionLogoff)]
        [TestCase(unchecked((long)0xFFFFFFFF80000000UL))]
        public void X1g_WM_ENDSESSION_wParam이_참이면_lParam과_무관하게_세션_종료_처리를_정확히_한_번_부른다(long lParam)
        {
            var events = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) => events.Add(step + "/" + t);
            AppShutdownSequence.QuitRequesterOverrideForTesting = () => events.Add("quit");

            Assert.IsTrue(AppShutdownSequence.TryHandleSessionEndMessage(SessionEndPolicy.WmEndSession, 1, lParam),
                "세션 종료를 처리했으면 참을 돌려줘야 프로시저가 0을 돌려준다.");

            CollectionAssert.AreEqual(new[]
            {
                AppShutdownStep.MarkExitStarted + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.RestoreReservedBar + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.StopFreezeWatchdog + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.FlushProgressSave + "/" + AppShutdownTrigger.SessionEnding,
                AppShutdownStep.MarkCleanExit + "/" + AppShutdownTrigger.SessionEnding,
                "quit",
            }, events, $"lParam=0x{lParam:X}에서 세션 종료 처리가 정확히 한 번 돌지 않았다 — 로그오프·Restart Manager·강제 종료 중 하나에서 원복·표지가 빠진다(X1g).");
        }

        [Test]
        public void X1g_세션_종료가_아닌_메시지는_아무것도_부르지_않고_거짓을_돌려준다()
        {
            var events = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) => events.Add(step + "/" + t);
            AppShutdownSequence.QuitRequesterOverrideForTesting = () => events.Add("quit");

            var cases = new (uint Message, long WParam, long LParam)[]
            {
                (SessionEndPolicy.WmEndSession, 0, 0),
                (SessionEndPolicy.WmEndSession, 0, SessionEndPolicy.EndSessionLogoff),
                (SessionEndPolicy.WmEndSession, 0, SessionEndPolicy.EndSessionCloseApp),
                (SessionEndPolicy.WmQueryEndSession, 1, 0),
                (SessionEndPolicy.WmQueryEndSession, 1, SessionEndPolicy.EndSessionLogoff),
                (0u, 1, 0),
            };
            foreach ((uint message, long wParam, long lParam) in cases)
            {
                Assert.IsFalse(AppShutdownSequence.TryHandleSessionEndMessage(message, wParam, lParam),
                    $"메시지 0x{message:X4} wParam={wParam} lParam=0x{lParam:X}를 세션 종료로 처리했다 — 종료 취소·묻기에서 원복하면 실행 중인데 자동 숨김이 돌아간다.");
            }
            CollectionAssert.IsEmpty(events, "세션 종료가 아닌 메시지에서 순서나 종료 요청이 돌았다.");
        }

        // ------------------------------------------------------------------ 트레이를 끈 사용자의 수신 창 (V5 · V5g)

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
        /// ★ 4차(verify-change 3차 V5) — 판정이 <b>실행 중 사실 두 가지를 실제로 읽는가</b>. 3차에는 트레이 파일이
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

        /// <summary>
        /// ★ 5차(verify-change 4차 V5g) — 수신 창을 <b>세울지 결정하는 경로 전체</b>를 실행한다. 표지만 켜진 옵트아웃 사용자(작업표시줄 자동 숨김을
        /// 원래 안 쓰는 사람 — 가장 흔한 경우)에게 창이 서야 한다. 4차에는 트레이 파일의 판정 줄 앞에
        /// <c>if (!ReservedBarRevealDirector.ChangedThisSession) return;</c>를 넣으면 바로 이 사용자가 수신 창을 잃는데 초록이었다.
        /// </summary>
        [Test]
        public void V5g_표지만_켜진_옵트아웃_사용자에게도_수신_창을_세우고_판정은_한_번뿐이다()
        {
            int creates = 0;
            Func<bool> create = () => { creates++; return true; };

            string ours = PrepareMarkerFolders(out string previous);
            SessionExitMarker.RunStartup(ours, 7101, previous, _ => false);
            Assert.IsFalse(ReservedBarRevealDirector.ChangedThisSession, "전제: 작업표시줄은 바꾸지 않았다(자동 숨김을 원래 안 쓰는 사용자).");

            Assert.AreEqual(SessionEndReceiverOutcome.Created, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, create),
                "표지만 켜진 옵트아웃 사용자에게 수신 창을 세우지 않는다 — 그 사용자의 밤 종료가 매일 비정상으로 오판되고 매일 아침 로그 복사가 난다(V5g).");
            Assert.AreEqual(1, creates, "창을 만드는 손을 정확히 한 번 불러야 한다.");

            Assert.AreEqual(SessionEndReceiverOutcome.AlreadyEvaluated, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, create),
                "매 Tick 다시 판정한다 — 판정은 프로세스당 한 번이다.");
            Assert.AreEqual(1, creates, "두 번째 Tick이 창을 또 만들려 했다.");
        }

        [Test]
        public void V5g_작업표시줄만_바꾼_옵트아웃_사용자에게도_수신_창을_세운다()
        {
            int creates = 0;
            ReservedBarRevealDirector.RunStartup(new FakeControl { AutoHide = true });
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession, "전제: 기동이 자동 숨김을 해제했다.");
            Assert.IsFalse(SessionExitMarker.IsStarted, "전제: 표지는 꺼져 있다.");

            Assert.AreEqual(SessionEndReceiverOutcome.Created,
                SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, () => { creates++; return true; }),
                "작업표시줄을 바꾼 옵트아웃 사용자에게 수신 창을 세우지 않는다 — 로그오프·종료 때 원복이 누락된다.");
            Assert.AreEqual(1, creates);
        }

        [Test]
        public void V5g_할_일이_없거나_트레이가_켜져_있으면_창을_만들지_않고_판정은_기동_사실로_한_번이다()
        {
            int creates = 0;
            Func<bool> create = () => { creates++; return true; };

            Assert.AreEqual(SessionEndReceiverOutcome.NotNeeded, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, create),
                "세션 종료에서 할 일이 없는데 숨은 창을 만든다(음성 대조).");
            Assert.AreEqual(0, creates);

            // 기동 사실은 첫 Tick보다 먼저 정해진다 — 그 뒤 사실이 바뀌어도 다시 판정하지 않는다(설계, 클래스 문서).
            string ours = PrepareMarkerFolders(out string previous);
            SessionExitMarker.RunStartup(ours, 7201, previous, _ => false);
            Assert.AreEqual(SessionEndReceiverOutcome.AlreadyEvaluated, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, create));
            Assert.AreEqual(0, creates);

            SessionEndReceiverGate.ResetForTesting();
            Assert.AreEqual(SessionEndReceiverOutcome.NotNeeded, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: false, create),
                "트레이가 켜져 있으면 트레이 호스트 창이 이미 수신자다 — 두 번째 창을 만들면 안 된다.");
            Assert.AreEqual(0, creates);
        }

        [Test]
        public void V5g_창_생성이_실패하거나_던지면_실패로_돌려주고_Tick을_깨지_않는다()
        {
            string ours = PrepareMarkerFolders(out string previous);
            SessionExitMarker.RunStartup(ours, 7301, previous, _ => false);

            Assert.AreEqual(SessionEndReceiverOutcome.CreateFailed, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, () => false));

            SessionEndReceiverGate.ResetForTesting();
            SessionEndReceiverOutcome thrown = SessionEndReceiverOutcome.AlreadyEvaluated;
            Assert.DoesNotThrow(() => thrown = SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, () => throw new InvalidOperationException("CreateWindowEx 실패")));
            Assert.AreEqual(SessionEndReceiverOutcome.CreateFailed, thrown);

            SessionEndReceiverGate.ResetForTesting();
            Assert.AreEqual(SessionEndReceiverOutcome.CreateFailed, SessionEndReceiverGate.EnsureReceiverWithoutTray(trayOptedOut: true, null));
        }

        // ------------------------------------------------------------------ 순서 (R-1, R-6, 4차, 5차)

        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void 종료_순서는_작업표시줄_원복이_워치독_정지보다_먼저다(AppShutdownTrigger trigger)
        {
            Assert.AreEqual(AppShutdownStep.MarkExitStarted, AppShutdownSequence.Order[0],
                "종료 시작 표지가 맨 앞이어야 순서 도중 끊겨도 '비정상'으로 남지 않는다(4차).");
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
                AppShutdownStep.FlushProgressSave + "/" + trigger,
                AppShutdownStep.MarkCleanExit + "/" + trigger,
            }, ran, "세션 종료는 처리 직후 프로세스가 끊긴다 — 워치독 합류 대기가 원복보다 먼저 오면 원복이 누락된다. " +
                    "진행 저장은 워치독 정지 뒤·정상 종료 표지 앞이다(5차, coder 설계 채택안).");
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
                AppShutdownStep.MarkExitStarted, AppShutdownStep.RestoreReservedBar, AppShutdownStep.StopFreezeWatchdog,
                AppShutdownStep.FlushProgressSave, AppShutdownStep.MarkCleanExit,
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

        // ------------------------------------------------------------------ G2 · R7 — 세션 종료가 남기는 최종 표지와 재실행 가드 (5차)

        /// <summary>
        /// ★ 5차(verify-change 4차 G2) — 세션 종료 처리(<see cref="AppShutdownSequence.HandleSessionEnding"/>, 실제 실행기)가 끝난 뒤 디스크의 표지는
        /// <c>clean-exit</c> · 이번 pid · <c>trigger=SessionEnding</c>이다. 4차에는 처리 뒤 "종료 시작"을 한 번 더 써도 초록이었다 — 그러면 매일 밤
        /// 정상적으로 끝난 세션 종료가 체크표 §R에서 "원복 도중 끊김"으로 읽힌다.
        /// </summary>
        [TestCase(0L)]
        [TestCase(SessionEndPolicy.EndSessionCloseApp)]
        [TestCase(SessionEndPolicy.EndSessionCritical)]
        [TestCase(SessionEndPolicy.EndSessionLogoff)]
        public void G2_세션_종료_처리가_남기는_최종_표지는_SessionEnding_정상_종료다(long lParam)
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 3001, prev, _ => false);
            int quits = 0;
            AppShutdownSequence.QuitRequesterOverrideForTesting = () => quits++;

            AppShutdownSequence.HandleSessionEnding(lParam);

            Assert.AreEqual(1, quits, "전제: 세션 종료 처리가 앱 종료를 한 번 요청했어야 한다.");
            AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, 3001, AppShutdownTrigger.SessionEnding,
                "세션 종료 처리가 끝났는데 최종 표지가 SessionEnding 정상 종료가 아니다(G2).");
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, SessionExitMarker.RunStartup(ours, 3002, prev, _ => false).Verdict);
        }

        /// <summary>
        /// ★ 5차(리더 질의, verify-change 4차 하니스 D) — 세션 종료 처리 → 앱 종료 요청 → Unity quitting이 같은 순서를 <b>다시</b> 돈다(여기서는
        /// 종료 요청 가로채기가 그 재실행을 흉내 낸다). 두 번째 순서는 표지 두 단계를 건너뛰어 첫 순서가 남긴 표지가 <b>바이트 그대로</b> 남는다.
        /// 4차에는 두 번째 순서가 "종료 시작"으로 덮어써, 그 틈에 끊기면 판정이 "원복 도중 끊김"과 구별되지 않았다.
        /// </summary>
        [TestCase(0L)]
        [TestCase(SessionEndPolicy.EndSessionLogoff)]
        public void R7_세션_종료_뒤_quitting_순서가_다시_돌아도_정상_종료_표지를_덮어쓰지_않는다(long lParam)
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 5001, prev, _ => false);
            string afterSessionEnd = null;
            int reRuns = 0;
            AppShutdownSequence.QuitRequesterOverrideForTesting = () =>
            {
                afterSessionEnd = MarkerText(ours);
                reRuns++;
                AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);   // Unity가 종료 요청 뒤 quitting에서 부르는 것과 같은 호출.
            };

            AppShutdownSequence.HandleSessionEnding(lParam);

            Assert.AreEqual(1, reRuns, "전제: 종료 요청(→ quitting 재실행)이 한 번 있었어야 한다.");
            AssertMarker(afterSessionEnd, SessionExitMarkerPolicy.CleanExitState, 5001, AppShutdownTrigger.SessionEnding, "전제: 첫 순서가 끝난 표지.");
            Assert.AreEqual(afterSessionEnd, MarkerText(ours),
                "두 번째(quitting) 순서가 표지를 다시 썼다 — 그 사이 끊기면 정상적으로 원복을 마친 세션 종료가 ExitStartedNotFinished로 읽힌다(재실행 가드 R7).");
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, SessionExitMarker.RunStartup(ours, 5002, prev, _ => false).Verdict);
        }

        /// <summary>
        /// ★ 5차 — 두 번째 순서가 <b>원복 단계에서 끊겨도</b> 다음 실행은 정상 종료로 본다. 가드는 가로채기 <b>앞에서</b> 걸리므로 흉내 낸 실행기가
        /// 표지를 쓸 기회 자체가 없어야 하고, 원복·워치독 정지·진행 저장은 두 번째 순서에서도 불려야 한다(첫 원복이 실패했으면 재시도 — 기존 설계).
        /// </summary>
        [Test]
        public void R7_두_번째_순서가_원복_단계에서_끊겨도_다음_실행은_정상_종료로_본다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 5101, prev, _ => false);
            AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding);   // 실제 실행기 — 끝까지.
            Assert.IsTrue(SessionExitMarker.CleanExitWrittenThisRun, "전제: 첫 순서가 정상 종료 표지를 썼다.");

            var ran = new List<AppShutdownStep>();
            bool killed = false;
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) =>
            {
                ran.Add(step);
                if (killed) return;
                switch (step)
                {
                    case AppShutdownStep.MarkExitStarted: SessionExitMarker.WriteExitStarted(t); return;
                    case AppShutdownStep.RestoreReservedBar: killed = true; return;   // 두 번째 순서가 원복에서 끊겼다.
                    case AppShutdownStep.MarkCleanExit: SessionExitMarker.WriteCleanExit(t); return;
                }
            };
            AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);

            Assert.IsTrue(killed, "전제: 두 번째 순서의 원복 단계가 불렸다.");
            CollectionAssert.AreEqual(new[] { AppShutdownStep.RestoreReservedBar, AppShutdownStep.StopFreezeWatchdog, AppShutdownStep.FlushProgressSave }, ran,
                "두 번째 순서는 표지 두 단계만 건너뛰어야 한다 — 표지 단계가 불렸거나(가드 없음), 원복·워치독·진행 저장까지 건너뛰었다(원복 재시도 설계 파괴).");
            SessionExitMarker.StartupResult next = SessionExitMarker.RunStartup(ours, 5102, prev, _ => false);
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, next.Verdict,
                "두 번째 순서가 끊긴 실행이 정상 종료로 읽히지 않는다 — 원복을 마친 세션 종료가 '원복 도중 끊김'과 구별되지 않는다.");
        }

        /// <summary>대조 — 가드는 "두 번째 순서"가 아니라 "정상 종료 표지를 이미 썼는가"에 걸린다. 첫 순서가 표지에 닿지 못했으면 두 번째 순서가 다시 쓴다.</summary>
        [Test]
        public void R7_대조_정상_종료_표지를_못_남긴_순서_뒤에는_두_번째_순서가_표지를_다시_쓴다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 5201, prev, _ => false);

            bool killed = false;
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) =>
            {
                if (killed) return;
                switch (step)
                {
                    case AppShutdownStep.MarkExitStarted: SessionExitMarker.WriteExitStarted(t); return;
                    case AppShutdownStep.RestoreReservedBar: killed = true; return;
                    case AppShutdownStep.MarkCleanExit: SessionExitMarker.WriteCleanExit(t); return;
                }
            };
            AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding);
            Assert.IsFalse(SessionExitMarker.CleanExitWrittenThisRun, "전제: 첫 순서는 정상 종료 표지에 닿지 못했다.");
            Assert.AreEqual(SessionExitMarkerPolicy.ExitStartedState, MarkerStateOrNull(ours), "전제: 첫 순서는 종료 시작 표지만 남겼다.");

            AppShutdownSequence.ExecutorOverrideForTesting = null;
            AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);   // 실제 실행기.

            AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, 5201, AppShutdownTrigger.ApplicationQuitting,
                "첫 순서가 정상 종료 표지를 못 남겼는데 두 번째 순서가 표지를 쓰지 않았다 — 가드가 너무 넓다.");
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, SessionExitMarker.RunStartup(ours, 5202, prev, _ => false).Verdict);
        }

        [Test]
        public void R7_재실행_가드는_표지_두_단계에만_걸린다()
        {
            var markerSteps = new HashSet<AppShutdownStep> { AppShutdownStep.MarkExitStarted, AppShutdownStep.MarkCleanExit };
            foreach (AppShutdownStep step in (AppShutdownStep[])Enum.GetValues(typeof(AppShutdownStep)))
            {
                Assert.IsTrue(AppShutdownSequence.ShouldRunStep(step, cleanExitAlreadyMarkedThisRun: false),
                    $"{step}: 정상 종료 표지를 아직 안 썼으면 모든 단계가 돌아야 한다.");
                Assert.AreEqual(!markerSteps.Contains(step), AppShutdownSequence.ShouldRunStep(step, cleanExitAlreadyMarkedThisRun: true),
                    $"{step}: 정상 종료 표지를 이미 썼으면 표지 두 단계만 건너뛴다 — 원복은 재시도, 진행 저장은 처리기 책임.");
            }
        }

        [Test]
        public void R7_정상_종료_기록은_실제로_쓴_뒤에만_서고_기동이_되돌린다()
        {
            Assert.IsFalse(SessionExitMarker.WriteCleanExit(AppShutdownTrigger.ApplicationQuitting), "전제: 기동 전에는 쓰지 않는다.");
            Assert.IsFalse(SessionExitMarker.CleanExitWrittenThisRun, "쓰지 않았는데 '썼다'로 선다 — 두 번째 순서가 표지를 영영 못 쓴다.");

            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 5301, prev, _ => false);
            Assert.IsFalse(SessionExitMarker.CleanExitWrittenThisRun);
            Assert.IsTrue(SessionExitMarker.WriteExitStarted(AppShutdownTrigger.SessionEnding));
            Assert.IsFalse(SessionExitMarker.CleanExitWrittenThisRun, "종료 시작 표지는 정상 종료 기록을 세우지 않는다.");
            Assert.IsTrue(SessionExitMarker.WriteCleanExit(AppShutdownTrigger.SessionEnding));
            Assert.IsTrue(SessionExitMarker.CleanExitWrittenThisRun);

            SessionExitMarker.RunStartup(ours, 5302, prev, _ => false);
            Assert.IsFalse(SessionExitMarker.CleanExitWrittenThisRun, "새 기동이 지난 실행의 정상 종료 기록을 이어받았다 — 새 실행의 표지 단계가 통째로 빠진다.");
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
                AppShutdownStep.FlushProgressSave + "/" + AppShutdownTrigger.SessionEnding,
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

        /// <summary>
        /// 원복 재시도 설계 — 셸이 먼저 끝나 첫 원복이 반영되지 않으면 흔적을 열어 두고, 뒤따르는 quitting 순서가 한 번 더 시도한다.
        /// ★ 5차: 표지를 켜서 <b>재실행 가드가 실제로 걸린 상태</b>(첫 순서가 정상 종료 표지를 씀)에서 재시도를 본다 — 가드가 표지 밖 단계까지
        /// 넓어지면 여기서 빨개진다. 표지는 다시 쓰지 않는다.
        /// </summary>
        [Test]
        public void 셸이_먼저_끝나_원복이_반영되지_않으면_흔적을_열어_둔다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 6001, prev, _ => false);
            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);
            control.WriteSilentlyFails = true;   // 탐색기가 먼저 종료돼 요청이 반영되지 않는다

            AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding);
            Assert.AreEqual(ReservedBarLedgerState.Open, ReservedBarRestoreLedger.Read(control.PlatformTag, out _),
                "원복을 확인하지 못했으면 흔적을 닫으면 안 된다 — 다음 실행이 먼저 갚는다(기존 설계).");
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession, "실패했으면 뒤따르는 경로가 다시 시도할 수 있어야 한다.");
            Assert.IsTrue(SessionExitMarker.CleanExitWrittenThisRun, "전제: 첫 순서가 정상 종료 표지를 써서 재실행 가드가 걸린 상태다.");
            string markerAfterFirst = MarkerText(ours);

            int writesBefore = control.WriteCount;
            AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);
            Assert.Greater(control.WriteCount, writesBefore, "뒤따르는 quitting 경로가 한 번 더 시도해야 한다 — 표지 재실행 가드가 원복까지 막았다.");
            Assert.AreEqual(markerAfterFirst, MarkerText(ours), "재시도하는 두 번째 순서가 표지를 다시 썼다(R7).");
        }

        // ------------------------------------------------------------------ C — 진행 저장 합류 계약 (5차, coder 설계 채택안)

        private static bool SaveSucceeds(AppShutdownTrigger trigger) => true;

        [Test]
        public void C_진행_저장_처리기는_비었을_때만_등록되고_등록한_참조로만_해제된다()
        {
            var first = new AppShutdownSaveHandler(SaveSucceeds);
            var sameMethodOtherInstance = new AppShutdownSaveHandler(SaveSucceeds);
            Assert.IsTrue(first.Equals(sameMethodOtherInstance) && !ReferenceEquals(first, sameMethodOtherInstance),
                "전제: 같은 메서드를 가리키는 서로 다른 대리자 인스턴스다.");

            Assert.IsFalse(AppShutdownSequence.RegisterSaveHandler(null), "null은 등록하지 않는다.");
            Assert.IsTrue(AppShutdownSequence.RegisterSaveHandler(first), "빈 슬롯에는 등록된다.");
            Assert.IsFalse(AppShutdownSequence.RegisterSaveHandler(first), "같은 처리기라도 슬롯이 차 있으면 거절한다(단일 슬롯).");
            Assert.IsFalse(AppShutdownSequence.RegisterSaveHandler(new AppShutdownSaveHandler(t => false)), "다른 처리기가 남의 슬롯을 덮지 않는다.");

            Assert.IsFalse(AppShutdownSequence.UnregisterSaveHandler(null));
            Assert.IsFalse(AppShutdownSequence.UnregisterSaveHandler(sameMethodOtherInstance),
                "같은 메서드라도 등록한 인스턴스가 아니면 해제하지 않는다 — 등록한 대리자를 필드에 보관해 그것으로 해제하라(메서드 그룹을 다시 쓰면 새 인스턴스다).");
            Assert.IsTrue(AppShutdownSequence.UnregisterSaveHandler(first), "등록한 참조로는 해제된다.");
            Assert.IsFalse(AppShutdownSequence.UnregisterSaveHandler(first), "이미 비었다.");
            Assert.IsTrue(AppShutdownSequence.RegisterSaveHandler(sameMethodOtherInstance), "비면 다시 등록된다.");
        }

        [Test]
        public void C_처리기가_없으면_진행_저장_단계는_아무것도_하지_않고_정상_종료_표지까지_간다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 8001, prev, _ => false);
            Assert.DoesNotThrow(() => AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting));
            AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, 8001, AppShutdownTrigger.ApplicationQuitting,
                "처리기 미등록(coder 등록 전 상태)에서 순서가 정상 종료 표지에 닿지 못했다.");
        }

        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void C_진행_저장은_종료_시작_표지_뒤_정상_종료_표지_앞에서_경로와_함께_한_번_불린다(AppShutdownTrigger trigger)
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 8101, prev, _ => false);
            var seen = new List<string>();
            var handler = new AppShutdownSaveHandler(t => { seen.Add(t + "|" + MarkerStateOrNull(ours)); return true; });
            Assert.IsTrue(AppShutdownSequence.RegisterSaveHandler(handler));

            AppShutdownSequence.Run(trigger);   // 실제 실행기.

            CollectionAssert.AreEqual(new[] { trigger + "|" + SessionExitMarkerPolicy.ExitStartedState }, seen,
                "진행 저장이 한 번, 경로와 함께, '종료 시작' 표지가 선 뒤 '정상 종료' 표지 전에 불려야 한다 — 저장 중 끊기면 표지가 '종료 시작'으로 남는다.");
            AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, 8101, trigger, "진행 저장 뒤 정상 종료 표지.");
        }

        [Test]
        public void C_진행_저장이_실패를_알리거나_던져도_정상_종료_표지는_남는다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            foreach (bool throws in new[] { false, true })
            {
                AppShutdownSequence.ResetSaveHandlerForTesting();
                int pid = throws ? 8202 : 8201;
                SessionExitMarker.RunStartup(ours, pid, prev, _ => false);
                int calls = 0;
                Assert.IsTrue(AppShutdownSequence.RegisterSaveHandler(new AppShutdownSaveHandler(t =>
                {
                    calls++;
                    if (throws) throw new IOException("디스크 가득");
                    return false;
                })));

                Assert.DoesNotThrow(() => AppShutdownSequence.Run(AppShutdownTrigger.SessionEnding));

                Assert.AreEqual(1, calls, $"처리기가 한 번 불려야 한다(던짐={throws}).");
                AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, pid, AppShutdownTrigger.SessionEnding,
                    $"진행 저장 실패(던짐={throws})로 정상 종료 표지가 빠졌다 — 표지는 '순서를 끝까지 돌았다'만 말한다(계약 문서).");
            }
        }

        [Test]
        public void C_세션_종료_뒤_quitting이_다시_돌면_진행_저장_처리기는_두_번_불린다_멱등은_처리기_책임이다()
        {
            string ours = PrepareMarkerFolders(out string prev);
            SessionExitMarker.RunStartup(ours, 8301, prev, _ => false);
            var seen = new List<AppShutdownTrigger>();
            Assert.IsTrue(AppShutdownSequence.RegisterSaveHandler(new AppShutdownSaveHandler(t => { seen.Add(t); return true; })));
            AppShutdownSequence.QuitRequesterOverrideForTesting = () => AppShutdownSequence.Run(AppShutdownTrigger.ApplicationQuitting);

            AppShutdownSequence.HandleSessionEnding(SessionEndPolicy.EndSessionLogoff);

            CollectionAssert.AreEqual(new[] { AppShutdownTrigger.SessionEnding, AppShutdownTrigger.ApplicationQuitting }, seen,
                "처리기는 세션 종료와 뒤따르는 quitting에서 각각 불린다 — 두 번째 쓰기를 막는 것은 처리기 쪽 가드다(coder 설계).");
            AssertMarker(MarkerText(ours), SessionExitMarkerPolicy.CleanExitState, 8301, AppShutdownTrigger.SessionEnding, "표지는 세션 종료가 남긴 그대로.");
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

        /// <summary>공백을 전부 지운다 — 줄바꿈·들여쓰기가 달라도 같은 모양이면 같게 비교한다(허용 형태 비교용).</summary>
        private static string Compact(string code) => Regex.Replace(code, @"\s+", string.Empty);

        /// <summary>
        /// 트레이 파일 배선. ★ 5차: 결정은 중립 함수가 실행으로 잠그고, 여기서는 트레이에 남은 줄의 <b>모양 전체</b>를 허용 형태와 비교한다
        /// (앞에 조건을 끼우거나 호출에 조건을 붙이면 모양이 달라진다). 허용 형태는 트레이의 private 식별자라 <c>nameof</c>로 가리킬 수 없어 글자로
        /// 적는다 — 이름이 바뀌면 비교가 시끄럽게 빨개진다(부재 단언이 아니라 일치 단언이다).
        /// </summary>
        [Test]
        public void 트레이_프로시저가_세션_종료를_동기로_처리하고_옵트아웃에도_수신자가_있다()
        {
            string trayPath = SourcePath("Platform", "Windows", "WindowsSystemTrayIcon.cs");
            string tray = ReadCode("Platform", "Windows", "WindowsSystemTrayIcon.cs");

            // (1) ★ X1g — 창 프로시저의 첫 문장은 중립 분기 한 줄이고, 조건 없이 wParam·lParam을 그대로 넘기며, 참이면 곧바로 0을 돌려준다.
            string proc = SourceTextScanner.BlockMemberBody(tray, "private static IntPtr WindowProcedure(");
            Assert.IsNotNull(proc, "창 프로시저를 찾지 못했습니다(니들이 썩었다).");
            string expectedProcHead = Compact(
                "private static IntPtr WindowProcedure(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam) { try { " +
                "if (" + nameof(AppShutdownSequence) + "." + nameof(AppShutdownSequence.TryHandleSessionEndMessage) +
                "(message, wParam.ToInt64(), lParam.ToInt64())) { return IntPtr.Zero; }");
            StringAssert.StartsWith(expectedProcHead, Compact(proc),
                "창 프로시저 맨 앞이 '중립 분기 한 줄 → 0 반환'이 아니다 — 앞에 다른 분기가 생겼거나 호출에 조건이 붙었다(lParam 조건이면 로그오프·Restart Manager 원복이 빠진다, X1g).");
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(tray, nameof(AppShutdownSequence.TryHandleSessionEndMessage)),
                "세션 종료 분기 호출이 한 번이 아니다.");
            Assert.AreEqual(0, SourceTextScanner.CountIdentifier(tray, nameof(AppShutdownSequence.HandleSessionEnding)),
                "트레이가 세션 종료 처리를 중립 분기 없이 직접 부른다 — 분기 규칙이 다시 트레이로 들어왔다(X1g).");
            Assert.AreEqual(0, SourceTextScanner.CountIdentifier(tray, nameof(SessionEndPolicy.IsSessionEndingNow)),
                "트레이가 메시지 판정을 직접 한다 — 분기 규칙이 다시 트레이로 들어왔다(X1g).");
            Assert.AreEqual(0, Regex.Matches(tray, Member(nameof(AppShutdownSequence), nameof(AppShutdownSequence.Run)) + @"\s*\(").Count,
                "트레이가 순서만 돌고 앱 종료 요청을 건너뛴다 — 살아남으면 원복된 채 도는 실행이 남는다(X1).");
            StringAssert.Contains("DefWindowProc(hWnd, message, wParam, lParam)", Compact(proc).Replace(",", ", "),
                "대조: 처리하지 않은 메시지는 DefWindowProc로 간다(WM_QUERYENDSESSION = 종료 허용).");

            // (2) 옵트아웃 조기 반환 블록은 "수신 창 확보 → 반환" 그 모양 그대로다(앞에 조건을 끼울 자리가 없다).
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
            Assert.AreEqual(Compact("if (_optOut) { #if !UNITY_EDITOR EnsureSessionEndReceiverWithoutTray(); #endif return; }"), Compact(earlyReturnBlocks[0]),
                "옵트아웃 조기 반환 블록이 '수신 창 확보 → 반환' 모양이 아니다 — 확보에 조건이 붙었거나 반환이 앞섰다.");

            // (3) ★ V5g — 수신 창 확보 메서드의 첫 문장은 중립 결정 경로 호출이고(옵트아웃 여부 + 창을 만드는 손), 그 뒤는 로그뿐이다.
            string receiver = SourceTextScanner.BlockMemberBody(tray, "private static void EnsureSessionEndReceiverWithoutTray()");
            Assert.IsNotNull(receiver, "수신 창 확보 본문을 찾지 못했습니다.");
            string expectedReceiverHead = Compact(
                "private static void EnsureSessionEndReceiverWithoutTray() { " + nameof(SessionEndReceiverOutcome) + " outcome = " +
                nameof(SessionEndReceiverGate) + "." + nameof(SessionEndReceiverGate.EnsureReceiverWithoutTray) + "(_optOut, CreateSessionEndReceiverWindow);");
            StringAssert.StartsWith(expectedReceiverHead, Compact(receiver),
                "수신 창 확보의 첫 문장이 중립 결정 경로 호출이 아니다 — 그 앞에 조건이 끼면 표지만 켜진 옵트아웃 사용자가 수신 창을 잃는다(V5g).");
            Assert.AreEqual(0, SourceTextScanner.CountIdentifier(receiver, "return"), "수신 창 확보 본문에 반환이 생겼다 — 로그 외의 흐름을 두지 않는다.");
            Assert.AreEqual(0, SourceTextScanner.CountIdentifier(receiver, "EnsureHostWindow"), "창을 중립 결정 없이 직접 만든다.");

            // (4) 창을 만드는 손은 조건 없는 그 모양 그대로다.
            string hand = SourceTextScanner.BlockMemberBody(tray, "private static bool CreateSessionEndReceiverWindow()");
            Assert.IsNotNull(hand, "창을 만드는 손 본문을 찾지 못했습니다.");
            Assert.AreEqual(Compact("private static bool CreateSessionEndReceiverWindow() { if (!EnsureHostWindow()) return false; InstallQuitHook(); return true; }"),
                Compact(hand), "창을 만드는 손에 조건이 붙었다 — 결정은 중립 쪽에서만 한다(V5g).");

            // (5) 경로가 하나뿐이다 — 손은 결정 경로에만 넘어가고, 결정 경로는 Tick에서만 불리며, 사실·옛 판정은 트레이 코드에 없다.
            Assert.AreEqual(2, SourceTextScanner.CountIdentifier(tray, "CreateSessionEndReceiverWindow"), "창을 만드는 손이 선언·인자 외에 또 쓰인다.");
            Assert.AreEqual(2, SourceTextScanner.CountIdentifier(tray, "EnsureSessionEndReceiverWithoutTray"), "수신 창 확보가 선언·Tick 호출 외에 또 쓰인다.");
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(tray, nameof(SessionEndReceiverGate)), "중립 결정 경로 호출이 한 번이 아니다.");
            foreach (string gone in new[] { nameof(SessionEndPolicy.ShouldCreateReceiverWithoutTrayNow), nameof(SessionEndPolicy.NeedsReceiverWithoutTray) })
            {
                Assert.AreEqual(0, SourceTextScanner.CountIdentifier(tray, gone), $"트레이가 판정({gone})을 직접 부른다 — 결정 경로가 다시 둘로 갈라졌다.");
            }
            foreach (string fact in new[] { nameof(ReservedBarRevealDirector.ChangedThisSession), nameof(SessionExitMarker.IsStarted) })
            {
                Assert.AreEqual(0, SourceTextScanner.CountMemberAccess(tray, fact),
                    $"지운 코드에 사실({fact})이 남았다 — 스캐너가 보간 구멍을 못 지웠거나, 트레이가 사실을 직접 읽어 조건을 만든다(V5g).");
            }

            // (6) 대조 — 같은 파일의 로그 보간에는 두 사실이 실재하지만(원문), 지운 코드에는 남지 않는다(로그가 니들을 못 채운다).
            string raw = File.ReadAllText(trayPath);
            StringAssert.Contains(nameof(SessionExitMarker) + "." + nameof(SessionExitMarker.IsStarted), raw,
                "대조 전제: 로그 보간에 표지 사실이 있어야 위 부재 단언이 공허하지 않다.");
            StringAssert.Contains(nameof(ReservedBarRevealDirector) + "." + nameof(ReservedBarRevealDirector.ChangedThisSession), raw,
                "대조 전제: 로그 보간에 원복 사실이 있어야 위 부재 단언이 공허하지 않다.");
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
                    "종료 시작 표지 → 작업표시줄 원복 → 워치독 정지 → 진행 저장(처리기 등록 시) → 정상 종료 표지. " +
                    "WM_ENDSESSION도 같은 Run을 부른다(TryHandleSessionEndMessage → HandleSessionEnding)."),
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
        /// (4) 표현식 트리·<c>dynamic</c>으로 조립한 구독.
        /// (5) ★ 5차 명시 — 유니코드 이스케이프 식별자(<c>quitting += …</c>): 컴파일러에게는 같은 이름이지만 이 정규식에게는 다른 글자다.
        /// 리더 판정(2026-09-14, verify-change 4차 N1·N2 분류): 일부러 꼬아야만 생기는 형태라 쫓지 않는다 — C# 파서를 들여오지 않는다.</para>
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
