using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-02 — 작업표시줄/Dock <b>자동 숨김 강제 해제</b>(원칙 3의 승인된 예외)의 검증.
    ///
    /// ============================================================================
    /// 이 파일이 진짜로 지키려는 것은 <b>크래시 복구</b> 하나다
    /// ============================================================================
    /// 나머지는 전부 부수적이다. <c>Application.quitting</c>은 SIGTERM/강제 종료/크래시에서 돌지
    /// 않으므로(이 저장소가 <c>driver.sh stop</c>에서 실제로 겪었다), 원복 장치가 <b>디스크</b>에
    /// 있지 않으면 사용자의 작업표시줄이 영구히 바뀐 채 남는다. 그 장치는 성질상 실기에서 재현하기가
    /// 가장 어렵다 — 일부러 앱을 죽여야 하고, 죽인 뒤에 사람이 눈으로 확인해야 한다.
    ///
    /// <para>그래서 <b>네거티브 컨트롤</b>을 박아 둔다: "원복 못 하고 죽은 흔적"을 손으로 만들어
    /// 디스크에 놓고, 다음 실행이 <b>실제로</b> 되돌리는지 확인한다. 그리고 그 검사가 공허하지
    /// 않다는 것까지 확인한다(흔적이 없으면 복구가 <b>일어나지 않아야</b> 한다).</para>
    ///
    /// <para><b>이 개발 머신에는 Windows가 없다.</b> 여기서 검증되는 것은 <b>규칙과 순서</b>이고,
    /// <c>SHAppBarMessage</c>가 실제 Windows 셸에서 무엇을 하는지는 검증되지 않는다. 그 구분을
    /// 흐리지 않기 위해 이 파일은 가짜 제어기만 쓴다(실기 확인 목록은 docs/TASKBAR_REVEAL.md 6절).</para>
    /// </summary>
    public class ReservedBarRevealPolicyTests
    {
        // ====================================================================
        // 가짜 제어기 — OS 대신 메모리 비트 하나를 들고 있고, **호출 순서를 기록한다**
        // ====================================================================

        private sealed class FakeControl : IReservedBarAutoHideControl
        {
            public bool AutoHide;
            public bool ReadFails;
            public bool WriteSilentlyFails;      // 셸이 요청을 무시하는 상황(정책/그룹 정책 등)
            public int ReadCount;
            public int WriteCount;

            /// <summary>쓰기가 일어난 순간의 디스크 흔적 상태. write-ahead 순서 검증의 증거다.</summary>
            public readonly List<ReservedBarLedgerState> LedgerAtWriteTime = new List<ReservedBarLedgerState>();

            public string PlatformTag => "TestOS";

            public bool TryReadAutoHide(out bool autoHideEnabled)
            {
                ReadCount++;
                autoHideEnabled = AutoHide;
                return !ReadFails;
            }

            public bool TrySetAutoHide(bool autoHideEnabled)
            {
                WriteCount++;
                LedgerAtWriteTime.Add(ReservedBarRestoreLedger.Read(PlatformTag, out _));
                if (WriteSilentlyFails) return false;
                AutoHide = autoHideEnabled;
                return true;
            }
        }

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            // 테스트마다 **새 폴더**를 쓴다 — 앞 테스트의 흔적이 남으면 복구 검사가 우연히 통과한다.
            _dir = ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting(
                "policy-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            ReservedBarRevealDirector.ResetForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            ReservedBarRevealDirector.ResetForTesting();
            ReservedBarRestoreLedger.ResetForTesting();
        }

        // ====================================================================
        // 1. 순수 규칙 — 진리표
        // ====================================================================

        [Test]
        public void 자동숨김이_꺼져_있으면_시스템도_디스크도_건드리지_않는다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveStartup(
                controlAvailable: true, observedAutoHide: false, observationSucceeded: true);

            Assert.IsFalse(plan.WriteSystem, "원래 자동 숨김을 안 쓰는 사용자의 시스템 설정을 건드렸습니다.");
            Assert.IsFalse(plan.WriteTrace,
                "바꾸지도 않았는데 디스크에 흔적을 남깁니다 — 사용자 요구 4('우리가 안 바꿨으면 " +
                "안 건드린다')의 정면 위반이고, 그 흔적은 다음 실행이 갚으려 드는 가짜 빚이 됩니다.");
            Assert.AreEqual(ReservedBarReason.AlreadyVisible, plan.Reason);
        }

        [Test]
        public void 자동숨김이_켜져_있으면_흔적을_남기고_해제한다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveStartup(
                controlAvailable: true, observedAutoHide: true, observationSucceeded: true);

            Assert.IsTrue(plan.WriteTrace, "원복 흔적 없이 시스템을 바꾸려 합니다(크래시 시 영구 변경).");
            Assert.IsTrue(plan.WriteSystem);
            Assert.IsFalse(plan.SystemAutoHideValue, "해제 방향이 아닙니다.");
            Assert.AreEqual(ReservedBarReason.RevealForSession, plan.Reason);
        }

        [Test]
        public void 제어_능력이_없거나_조회에_실패하면_아무것도_하지_않는다()
        {
            foreach ((bool available, bool ok) in new[] { (false, true), (true, false), (false, false) })
            {
                ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveStartup(available, true, ok);
                Assert.IsFalse(plan.WriteSystem,
                    $"available={available} observationSucceeded={ok}인데 시스템에 씁니다 — " +
                    "모르는 상태에서 남의 설정을 추측으로 바꾸고 있습니다.");
                Assert.IsFalse(plan.WriteTrace);
                Assert.AreEqual(ReservedBarReason.Unavailable, plan.Reason);
            }
        }

        [Test]
        public void 종료시_우리가_안_바꿨으면_되돌릴_것이_없다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveQuit(
                weChangedIt: false, originalAutoHide: true, observedAutoHide: false, observationSucceeded: true);

            Assert.IsFalse(plan.WriteSystem);
            Assert.IsFalse(plan.CloseTrace);
            Assert.AreEqual(ReservedBarReason.NothingToRestore, plan.Reason);
        }

        [Test]
        public void 종료시_우리가_바꿨으면_원래값으로_되돌린다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveQuit(
                weChangedIt: true, originalAutoHide: true, observedAutoHide: false, observationSucceeded: true);

            Assert.IsTrue(plan.WriteSystem);
            Assert.IsTrue(plan.SystemAutoHideValue, "사용자의 원래 값(자동 숨김 켜짐)으로 안 돌아갑니다.");
            Assert.IsTrue(plan.CloseTrace, "되돌렸는데 흔적을 닫지 않으면 다음 실행이 같은 빚을 또 갚습니다.");
        }

        [Test]
        public void 종료시_이미_원래값이면_시스템에_쓰지_않고_흔적만_닫는다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveQuit(
                weChangedIt: true, originalAutoHide: true, observedAutoHide: true, observationSucceeded: true);

            Assert.IsFalse(plan.WriteSystem,
                "이미 원래 값인데 또 씁니다 — 필요 없는 전역 쓰기는 0회여야 합니다(예외의 범위를 " +
                "스스로 좁히는 것이 이 기능이 승인받은 조건입니다).");
            Assert.IsTrue(plan.CloseTrace);
            Assert.AreEqual(ReservedBarReason.QuitAlreadyMatched, plan.Reason);
        }

        [Test]
        public void 종료시_조회에_실패해도_원복은_시도한다()
        {
            ReservedBarPlan plan = ReservedBarRevealPolicy.ResolveQuit(
                weChangedIt: true, originalAutoHide: true, observedAutoHide: false, observationSucceeded: false);

            Assert.IsTrue(plan.WriteSystem,
                "종료는 다시 오지 않는 기회입니다 — 상태를 못 읽었다고 원복을 포기하면 " +
                "사용자 설정이 바뀐 채로 남습니다. 같은 값을 한 번 더 쓰는 비용은 0입니다.");
        }

        // ====================================================================
        // 2. ★ 네거티브 컨트롤 — "원복 못 한 흔적"을 일부러 만들고 복구를 확인한다
        // ====================================================================

        /// <summary>
        /// 시나리오: 사용자는 <b>자동 숨김 ON</b>. 지난 실행이 해제하고 흔적을 남긴 뒤 <b>크래시</b>했다
        /// (= 종료 훅이 돌지 않았다). 지금 디스크에는 열린 흔적이, OS에는 해제된 상태가 남아 있다.
        /// </summary>
        [Test]
        public void 네거티브컨트롤_원복못한_흔적이_있으면_다음_실행이_먼저_복구한다()
        {
            // --- 크래시 잔해를 손으로 만든다 ---
            Assert.IsTrue(ReservedBarRestoreLedger.Open(originalAutoHide: true, platformTag: "TestOS"),
                "흔적 파일을 만들지 못했습니다 — 이 테스트의 전제가 성립하지 않습니다.");
            var control = new FakeControl { AutoHide = false };   // 우리가 해제해 둔 채 죽었다

            Assert.AreEqual(ReservedBarLedgerState.Open,
                ReservedBarRestoreLedger.Read("TestOS", out ReservedBarRestoreTrace seeded),
                "만들어 둔 흔적이 '열림'으로 읽히지 않습니다.");
            Assert.IsTrue(seeded.originalAutoHide, "원래 값이 흔적에 보존되지 않았습니다.");

            // --- 다음 실행 ---
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(ReservedBarLedgerState.Open, ReservedBarRevealDirector.LastLedgerState,
                "기동이 열린 흔적을 발견하지 못했습니다 — 복구 장치가 통째로 죽어 있습니다.");
            Assert.GreaterOrEqual(control.WriteCount, 1,
                "흔적이 있는데 시스템에 한 번도 쓰지 않았습니다 = 복구가 일어나지 않았습니다. " +
                "이 상태로 출시하면 크래시한 사용자의 작업표시줄이 영구히 바뀐 채 남습니다.");

            // 복구 뒤 이번 실행이 다시 해제했으므로 최종 상태는 '해제'다.
            Assert.IsFalse(control.AutoHide,
                "복구 뒤 이번 실행의 해제가 적용되지 않았습니다.");
            Assert.AreEqual(ReservedBarReason.RevealForSession, ReservedBarRevealDirector.LastStartupReason);
            Assert.IsTrue(ReservedBarRevealDirector.ChangedThisSession);
            Assert.IsTrue(ReservedBarRevealDirector.OriginalAutoHide,
                "복구로 되돌린 **진짜 원래 값**이 아니라 복구 전의 값을 원본으로 기억했습니다 — " +
                "그 상태로 종료하면 사용자 설정이 반대로 굳습니다.");

            // 첫 쓰기(=복구)는 자동 숨김을 다시 켜는 방향이어야 한다.
            Assert.AreEqual(2, control.WriteCount,
                "쓰기 횟수가 2(복구 1 + 이번 실행 해제 1)가 아닙니다 — 실제 순서를 확인하세요.");
        }

        /// <summary>
        /// ★ 위 검사가 <b>공허하지 않은지</b> 확인하는 짝. 흔적이 없으면 복구는 <b>일어나면 안 된다</b>.
        /// (같은 밤 이 저장소에서 "실패한 측정과 성공한 측정이 똑같이 생긴" 거짓 통과가 9건 나왔다.)
        /// </summary>
        [Test]
        public void 네거티브컨트롤_흔적이_없으면_복구는_일어나지_않는다()
        {
            var control = new FakeControl { AutoHide = false };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(ReservedBarLedgerState.None, ReservedBarRevealDirector.LastLedgerState);
            Assert.AreEqual(0, control.WriteCount,
                "흔적이 없는데 시스템에 썼습니다 — 위 복구 검사는 '무조건 쓴다'를 통과시키고 " +
                "있었을 뿐입니다(거짓 통과).");
            Assert.AreEqual(ReservedBarReason.AlreadyVisible, ReservedBarRevealDirector.LastStartupReason);
            Assert.IsFalse(File.Exists(ReservedBarRestoreLedger.FilePath),
                "아무 것도 바꾸지 않았는데 흔적 파일을 만들었습니다(사용자 요구 4 위반).");
        }

        /// <summary>흔적이 있지만 OS가 이미 원래 값이면 — 사용자가 직접 되돌렸거나 셸이 재시작된 경우 —
        /// <b>시스템에 쓰지 않고</b> 흔적만 닫아야 한다.</summary>
        [Test]
        public void 흔적이_있어도_OS가_이미_원래값이면_시스템에_쓰지_않는다()
        {
            ReservedBarRestoreLedger.Open(originalAutoHide: true, platformTag: "TestOS");
            var control = new FakeControl { AutoHide = true };   // 사용자가 이미 되돌려 놓았다

            ReservedBarPlan recovery = ReservedBarRevealPolicy.ResolveRecovery(
                hasLeftover: true, leftoverOriginalAutoHide: true,
                controlAvailable: true, observedAutoHide: true, observationSucceeded: true);

            Assert.IsFalse(recovery.WriteSystem, "쓸 필요가 없는데 전역 설정에 씁니다.");
            Assert.IsTrue(recovery.CloseTrace);
            Assert.AreEqual(ReservedBarReason.LeftoverAlreadyMatched, recovery.Reason);

            ReservedBarRevealDirector.RunStartup(control);
            // 이번 실행의 해제 1회만 있어야 한다(복구 쓰기는 0회).
            Assert.AreEqual(1, control.WriteCount,
                "복구가 불필요한 쓰기를 한 번 더 했습니다.");
        }

        // ====================================================================
        // 3. write-ahead 순서 — 흔적이 시스템보다 **먼저**여야 한다
        // ====================================================================

        [Test]
        public void 흔적은_시스템_변경보다_먼저_디스크에_쓰인다()
        {
            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(1, control.LedgerAtWriteTime.Count,
                "시스템 쓰기가 정확히 1회가 아닙니다 — 아래 순서 단언의 전제가 깨집니다.");
            Assert.AreEqual(ReservedBarLedgerState.Open, control.LedgerAtWriteTime[0],
                "시스템을 바꾸는 순간 디스크에 열린 흔적이 **없었습니다**. 그 사이에 크래시하면 " +
                "사용자의 작업표시줄이 영구히 바뀐 채 남습니다 — 이 순서가 이 기능의 안전장치 전부입니다.");
        }

        [Test]
        public void 흔적을_못_쓰면_시스템을_바꾸지_않는다()
        {
            // 흔적 경로를 **파일**로 막아 디렉터리 생성/쓰기를 실패시킨다(권한 실패와 같은 결과).
            string blocked = Path.Combine(_dir, "blocked-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            File.WriteAllText(blocked, "not a directory");
            ReservedBarRestoreLedger.RedirectToPathForTesting(blocked);

            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(0, control.WriteCount,
                "원복 흔적을 남기지 못했는데도 시스템을 바꿨습니다 — 되돌릴 수 없는 변경입니다. " +
                "'실행 중에만'이라는 사용자 승인 조건을 지킬 수 없게 됩니다.");
            Assert.IsFalse(ReservedBarRevealDirector.ChangedThisSession);
        }

        // ====================================================================
        // 4. 시스템 쓰기가 조용히 실패하는 경우 — 흔적이 거짓말을 하면 안 된다
        // ====================================================================

        [Test]
        public void 시스템_변경이_실패하면_남긴_흔적을_즉시_닫는다()
        {
            var control = new FakeControl { AutoHide = true, WriteSilentlyFails = true };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.IsFalse(ReservedBarRevealDirector.ChangedThisSession);
            Assert.AreEqual(ReservedBarLedgerState.Closed,
                ReservedBarRestoreLedger.Read("TestOS", out _),
                "바뀌지도 않은 변경의 흔적이 열린 채 남았습니다 — 다음 실행이 사용자의 설정을 " +
                "**있지도 않았던 변경**의 이름으로 뒤집습니다.");
        }

        // ====================================================================
        // 5. 종료 경로 통합
        // ====================================================================

        [Test]
        public void 기동해제_후_정상종료하면_사용자_설정이_그대로_돌아온다()
        {
            var control = new FakeControl { AutoHide = true };

            ReservedBarRevealDirector.RunStartup(control);
            Assert.IsFalse(control.AutoHide, "기동 시 해제되지 않았습니다.");

            ReservedBarRevealDirector.RunShutdown();

            Assert.IsTrue(control.AutoHide,
                "종료했는데 사용자의 자동 숨김이 돌아오지 않았습니다 — '실행 중에만'이 아니라 " +
                "'영구 변경'이 됩니다.");
            Assert.AreEqual(ReservedBarReason.RestoreOnQuit, ReservedBarRevealDirector.LastShutdownReason);
            Assert.AreEqual(ReservedBarLedgerState.Closed,
                ReservedBarRestoreLedger.Read("TestOS", out _),
                "원복했는데 흔적이 열린 채입니다 — 다음 실행이 같은 빚을 또 갚으려 듭니다.");
        }

        [Test]
        public void 자동숨김을_안_쓰는_사용자는_종료해도_아무_일이_없다()
        {
            var control = new FakeControl { AutoHide = false };

            ReservedBarRevealDirector.RunStartup(control);
            ReservedBarRevealDirector.RunShutdown();

            Assert.AreEqual(0, control.WriteCount, "시스템 설정을 건드렸습니다.");
            Assert.IsFalse(File.Exists(ReservedBarRestoreLedger.FilePath), "디스크에 흔적을 남겼습니다.");
            Assert.AreEqual(ReservedBarReason.NothingToRestore, ReservedBarRevealDirector.LastShutdownReason);
        }

        // ====================================================================
        // 6. 흔적 파일 스키마
        // ====================================================================

        [Test]
        public void 흔적_스키마는_현재_버전으로_기록되고_그대로_읽힌다()
        {
            Assert.IsTrue(ReservedBarRestoreLedger.Open(originalAutoHide: true, platformTag: "TestOS"));

            Assert.AreEqual(ReservedBarLedgerState.Open,
                ReservedBarRestoreLedger.Read("TestOS", out ReservedBarRestoreTrace trace));
            // 상수를 숫자로 베끼지 않는다(CLAUDE.md 협업 프로토콜).
            Assert.AreEqual(ReservedBarRestoreLedger.CurrentVersion, trace.version);
            Assert.IsTrue(trace.active);
            Assert.IsTrue(trace.originalAutoHide);
            Assert.AreEqual("TestOS", trace.platform);
            Assert.IsNotEmpty(trace.writtenAtUtc, "언제 바꿨는지가 없으면 사용자가 로그와 대조할 수 없습니다.");
        }

        [Test]
        public void 다른_OS가_남긴_흔적은_갚지_않는다()
        {
            ReservedBarRestoreLedger.Open(originalAutoHide: true, platformTag: "OtherOS");

            Assert.AreEqual(ReservedBarLedgerState.ForeignPlatform,
                ReservedBarRestoreLedger.Read("TestOS", out _),
                "다른 OS의 흔적을 우리 것으로 착각합니다 — 폴더 동기화 환경에서 남의 설정을 " +
                "엉뚱한 값으로 덮어씁니다.");

            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(0, control.WriteCount, "남의 흔적을 보고 시스템을 바꿨습니다.");
        }

        [Test]
        public void 이_빌드보다_새로운_스키마는_해석하지_않고_보존한다()
        {
            string json = JsonUtility.ToJson(new ReservedBarRestoreTrace
            {
                version = ReservedBarRestoreLedger.CurrentVersion + 1,
                active = true,
                originalAutoHide = true,
                platform = "TestOS",
                writtenAtUtc = "2026-09-02T00:00:00.0000000Z",
            });
            File.WriteAllText(ReservedBarRestoreLedger.FilePath, json);

            Assert.AreEqual(ReservedBarLedgerState.NewerSchema,
                ReservedBarRestoreLedger.Read("TestOS", out _));

            var control = new FakeControl { AutoHide = true };
            ReservedBarRevealDirector.RunStartup(control);

            Assert.AreEqual(0, control.WriteCount,
                "해석할 수 없는 흔적을 보고 시스템을 바꿨습니다.");
            Assert.AreEqual(ReservedBarLedgerState.NewerSchema,
                ReservedBarRestoreLedger.Read("TestOS", out _),
                "신버전이 갚아야 할 흔적을 우리가 덮어썼습니다 — 그 사용자는 영영 복구되지 않습니다.");
        }

        // ====================================================================
        // 7. 구조 — 정책이 중립 위치에 있고 플랫폼 분기가 없는가
        // ====================================================================

        [Test]
        public void 판정_규칙에는_플랫폼_분기도_OS호출도_없다()
        {
            string policy = Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Platform", "ReservedBarRevealPolicy.cs");
            Assert.IsTrue(File.Exists(policy),
                "판정 규칙이 Platform/ 중립 위치에 없습니다 — 플랫폼 폴더 안으로 들어가면 " +
                "반대편 플랫폼이 물리적으로 호출할 수 없습니다(FullscreenSuspendPolicy 사고).");

            // ★ 주석 줄은 걷어내고 본다. 이 규칙이 없으면 **정직하게 적을수록 감사가 빨개진다** —
            //   이 파일의 클래스 문서는 "UNITY_STANDALONE_* 분기가 한 줄도 없다"고 그 사실을
            //   명시하고 있고, 원문 스캔은 그 문장 자체를 위반으로 잡는다(실제로 잡았다).
            //   그러면 다음 사람은 검사를 통과시키려고 사실을 지운다 — 감사가 지키려던 것의 정반대다.
            //   같은 판단이 이미 저장소 표준이다(PlatformParityAuditTests.StripLineComments 문서).
            string src = StripLineComments(File.ReadAllText(policy));
            StringAssert.DoesNotContain("UNITY_STANDALONE_", src,
                "판정 규칙에 플랫폼 분기가 들어왔습니다.");
            StringAssert.DoesNotContain("DllImport", src, "판정 규칙이 OS를 직접 부릅니다.");
            StringAssert.DoesNotContain("System.IO", src,
                "판정 규칙이 파일을 직접 만집니다 — 순수 함수가 아니면 크래시 시나리오를 " +
                "EditMode에서 재현할 수 없습니다.");

            // 네거티브 컨트롤 — 걷어내기가 **실행 코드까지** 지워 버리면 위 단언이 공허해진다.
            Assert.IsTrue(StripLineComments("// UNITY_STANDALONE_WIN\nint x = 1;\n").Contains("int x = 1;"),
                "주석 제거기가 실행 코드까지 지웁니다 — 이 검사는 아무것도 안 보게 됩니다.");
            Assert.IsFalse(StripLineComments("        /// UNITY_STANDALONE_WIN\n").Contains("UNITY_STANDALONE_"),
                "주석 제거기가 주석을 못 지웁니다 — 문서 문장이 위반으로 잡힙니다(오탐).");
        }

        /// <summary>줄 전체가 주석인 줄만 걷어낸다. 실제 <c>#if</c>는 그런 줄에 있을 수 없다.</summary>
        private static string StripLineComments(string source)
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
    }
}
