using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — OS 세션 종료(로그오프·시스템 종료)에서 작업표시줄 자동 숨김 원복이 누락되지 않는가, 그리고
    /// 종료 순서가 "원복 먼저, 워치독 정지 나중, 정상 종료 표지 마지막"인가(persona-stress R-1, R-6). 원칙 3 예외의 조건
    /// ("실행 중에만 + 종료 시 원복")을 평범한 Windows 종료 경로에서도 지키는 장치다 — 경위는 <c>docs/TASKBAR_REVEAL.md</c> §2-2.
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

        private string _markerRoot;

        [SetUp]
        public void SetUp()
        {
            ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting("session-end-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            ReservedBarRevealDirector.ResetForTesting();
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            SessionExitMarker.ResetForTesting();
            _markerRoot = null;
        }

        [TearDown]
        public void TearDown()
        {
            AppShutdownSequence.ExecutorOverrideForTesting = null;
            ReservedBarRevealDirector.ResetForTesting();
            ReservedBarRestoreLedger.ResetForTesting();
            SessionExitMarker.ResetForTesting();
            if (_markerRoot != null && Directory.Exists(_markerRoot))
            {
                try { Directory.Delete(_markerRoot, recursive: true); } catch (Exception) { /* 임시 폴더 — 남아도 무해 */ }
            }
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

        // ------------------------------------------------------------------ 순서 (R-1, R-6)

        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void 종료_순서는_작업표시줄_원복이_워치독_정지보다_먼저다(AppShutdownTrigger trigger)
        {
            Assert.AreEqual(AppShutdownStep.RestoreReservedBar, AppShutdownSequence.Order[0]);
            Assert.AreEqual(AppShutdownStep.MarkCleanExit, AppShutdownSequence.Order[AppShutdownSequence.Order.Count - 1],
                "정상 종료 표지는 맨 끝이어야 한다 — 앞 단계가 전부 돈 뒤에야 '정상 종료'다.");
            var ran = new List<string>();
            AppShutdownSequence.ExecutorOverrideForTesting = (step, t) => ran.Add(step + "/" + t);
            AppShutdownSequence.Run(trigger);
            CollectionAssert.AreEqual(new[]
            {
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
            CollectionAssert.AreEqual(new[] { AppShutdownStep.RestoreReservedBar, AppShutdownStep.StopFreezeWatchdog, AppShutdownStep.MarkCleanExit }, ran);
        }

        /// <summary>
        /// ★ 3차 D (persona-stress R-6) — 두 종료 입구 <b>모두</b> 정상 종료 표지를 남기고, 그래서 다음 실행이 직전 로그를
        /// 복사하지 않는다. 양성 대조로 "종료 순서를 안 돈 실행"은 비정상으로 판정되어 복사가 일어남을 같은 배치에서 보인다.
        /// </summary>
        [TestCase(AppShutdownTrigger.ApplicationQuitting)]
        [TestCase(AppShutdownTrigger.SessionEnding)]
        public void R6_두_종료_입구_모두_정상_종료_표지를_남겨_다음_실행이_비정상으로_오판하지_않는다(AppShutdownTrigger trigger)
        {
            _markerRoot = Path.Combine(Path.GetTempPath(), "stickmate-r6-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string ours = Path.Combine(_markerRoot, "FreezeForensics");
            string logs = Path.Combine(_markerRoot, "Logs");
            Directory.CreateDirectory(logs);
            string prev = Path.Combine(logs, SessionExitMarkerPolicy.PreviousPlayerLogFileName);
            File.WriteAllText(prev, "직전 실행 로그\n");
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
            StringAssert.Contains(nameof(SessionExitMarker) + "." + nameof(SessionExitMarker.IsStarted), tray,
                "옵트아웃 수신 창 판정에 정상 종료 표지가 들어가지 않는다 — 트레이를 끈 사용자의 밤 종료가 비정상으로 오판된다(R-6).");
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

        // ------------------------------------------------------------------ 종료 구독 대장 (3차 — "quitting 구독 1곳" 정정)

        private enum SessionEndDecision
        {
            /// <summary>종료 순서 자체 — 세션 종료에서도 돈다.</summary>
            SequenceItself,
            /// <summary>정상 종료에서만 돈다(세션 종료 경로에 넣지 않기로 결정 — 사유 필수).</summary>
            QuitOnly,
            /// <summary>정상 종료에서만 도는데 세션 종료에서도 돌아야 할 수 있다 — 결정 미정(러너에 건너뜀으로 띄운다).</summary>
            OpenGap,
        }

        private const string QuittingKind = "Application.quitting +=";
        private const string OnApplicationQuitKind = "void OnApplicationQuit()";

        /// <summary>
        /// ★ 종료 구독 대장. 키 = "파일 이름|종류". <b>프로덕션 전체(Assets, 테스트 제외)에서 실제로 찾은 목록과 양방향으로
        /// 같아야 한다</b> — 새 구독이 생기면 결정 없이 넘어갈 수 없고, 사라진 구독은 대장에서 지워야 한다.
        /// </summary>
        private static readonly Dictionary<string, (SessionEndDecision Decision, string Why)> QuitHookLedger =
            new Dictionary<string, (SessionEndDecision, string)>(StringComparer.Ordinal)
            {
                ["AppShutdownSequence.cs|" + QuittingKind] = (SessionEndDecision.SequenceItself,
                    "작업표시줄 원복 → 워치독 정지 → 정상 종료 표지. WM_ENDSESSION도 같은 Run을 부른다."),
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

        private static readonly Regex QuittingSubscription = new Regex(@"\bApplication\s*\.\s*quitting\s*\+=", RegexOptions.CultureInvariant);
        private static readonly Regex OnApplicationQuitDeclaration = new Regex(@"\bvoid\s+OnApplicationQuit\s*\(\s*\)", RegexOptions.CultureInvariant);

        private static List<string> FindQuitHooks()
        {
            var found = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), null);
                string name = Path.GetFileName(file);
                for (int k = QuittingSubscription.Matches(code).Count; k > 0; k--) found.Add(name + "|" + QuittingKind);
                for (int k = OnApplicationQuitDeclaration.Matches(code).Count; k > 0; k--) found.Add(name + "|" + OnApplicationQuitKind);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        [Test]
        public void 종료_구독_대장은_프로덕션의_모든_종료_훅과_일치하고_결정마다_사유가_있다()
        {
            List<string> found = FindQuitHooks();
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
        public void 스캐너는_주석과_문자열_속_종료_구독을_세지_않는다()
        {
            string snippet = "// Application.quitting += A;\nvar s = \"Application.quitting += B\";\n" +
                             "UnityEngine.Application . quitting\n   += C;\nvoid OnApplicationQuit() { }\n";
            string code = SourceTextScanner.BlankCommentsAndStrings(snippet, null);
            Assert.AreEqual(1, QuittingSubscription.Matches(code).Count);
            Assert.AreEqual(1, OnApplicationQuitDeclaration.Matches(code).Count);
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
