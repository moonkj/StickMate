using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 (3차 D, 4차, 5차) — 정상 종료 표지와 비정상 종료 다음 실행의 <c>Player-prev.log</c> 복사.
    /// 파일 조작은 전부 임시 폴더에서 <b>실행</b>한다. 두 종료 입구가 표지를 남기는지(R-6)·원복 도중 끊김(R5)·재실행 가드(R7)는 <c>SessionEndShutdownTests</c>가 본다.
    ///
    /// <para><b>★ 이 러너(Unity Mono, macOS 에디터)가 원칙 3 축 중 실행으로 재는 것과 못 재는 것(5차 정정, verify-change 4차 하니스 실측).</b>
    /// 잰다: 내용·크기·수정 시각, 속성 중 <b>ReadOnly·Hidden</b>, 남의 핸들 <b>열기(읽기·쓰기)</b>를 막는가.
    /// 못 잰다: 그 밖의 속성(System·Archive·Temporary·NotContentIndexed — 설정해도 <c>GetAttributes</c>에 안 보인다), 남의 <b>이름 바꾸기·삭제</b>를 막는가
    /// (Mono는 <c>FileShare.None</c> 핸들을 쥔 채로도 <c>File.Move</c>를 허용한다). 못 재는 축은 소스 감사가 <b>형태로</b>만 막고, 실제 동작은 Windows 실기 항목이다.</para>
    /// </summary>
    public sealed class SessionExitMarkerTests
    {
        private string _root;
        private string _ours;
        private string _logs;
        private string _prev;
        private static readonly Func<int, bool> NobodyAlive = _ => false;

        [SetUp]
        public void SetUp()
        {
            SessionExitMarker.ResetForTesting();
            _root = Path.Combine(Path.GetTempPath(), "stickmate-exitmarker-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _ours = Path.Combine(_root, "persistent", "FreezeForensics");
            _logs = Path.Combine(_root, "Logs");
            Directory.CreateDirectory(_logs);
            _prev = Path.Combine(_logs, SessionExitMarkerPolicy.PreviousPlayerLogFileName);
        }

        [TearDown]
        public void TearDown()
        {
            SessionExitMarker.ResetForTesting();
            try
            {
                if (!Directory.Exists(_root)) return;
                foreach (string f in Directory.GetFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception) { /* 임시 폴더 */ }
        }

        private static string MarkerPath(string dir) => Path.Combine(dir, SessionExitMarkerPolicy.MarkerFileName);

        // ------------------------------------------------------------------ 판정

        [Test]
        public void 판정표_표지_없음_정상_비정상_동시실행_해석불가_종료시작()
        {
            DateTime t = new DateTime(2026, 9, 14, 1, 2, 3, DateTimeKind.Utc);
            string running = SessionExitMarkerPolicy.FormatRunning(500, t);
            string started = SessionExitMarkerPolicy.FormatExitStarted(500, AppShutdownTrigger.SessionEnding, t);
            string clean = SessionExitMarkerPolicy.FormatCleanExit(500, AppShutdownTrigger.SessionEnding, t);

            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(running, out string s1, out int p1));
            Assert.AreEqual(SessionExitMarkerPolicy.RunningState, s1);
            Assert.AreEqual(500, p1);
            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(clean, out string s2, out _));
            Assert.AreEqual(SessionExitMarkerPolicy.CleanExitState, s2);
            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(started, out string s3, out _));
            Assert.AreEqual(SessionExitMarkerPolicy.ExitStartedState, s3);
            StringAssert.Contains(nameof(AppShutdownTrigger.SessionEnding), clean, "어느 입구로 끝났는지 표지에 남아야 한다.");

            Assert.AreEqual(PreviousSessionVerdict.NoMarker, SessionExitMarkerPolicy.Classify(false, null, 1, NobodyAlive));
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, SessionExitMarkerPolicy.Classify(true, clean, 1, NobodyAlive));
            Assert.AreEqual(PreviousSessionVerdict.AbnormalExit, SessionExitMarkerPolicy.Classify(true, running, 1, NobodyAlive));
            Assert.AreEqual(PreviousSessionVerdict.ExitStartedNotFinished, SessionExitMarkerPolicy.Classify(true, started, 1, NobodyAlive),
                "종료를 시작했다가 끊긴 실행은 크래시와 구별해야 한다(4차).");
            Assert.AreEqual(PreviousSessionVerdict.OtherInstanceAlive, SessionExitMarkerPolicy.Classify(true, running, 1, pid => pid == 500),
                "표지의 pid가 살아 있는 같은 앱이면 동시 실행이다 — 비정상 종료로 보면 안 된다.");
            Assert.AreEqual(PreviousSessionVerdict.AbnormalExit, SessionExitMarkerPolicy.Classify(true, running, 500, pid => pid == 500),
                "표지의 pid가 우리 자신이면(pid 재사용) 다른 인스턴스가 아니다.");
            Assert.AreEqual(PreviousSessionVerdict.Unreadable, SessionExitMarkerPolicy.Classify(true, "쓰레기", 1, NobodyAlive));
            Assert.AreEqual(PreviousSessionVerdict.Unreadable, SessionExitMarkerPolicy.Classify(true, "state=weird pid=3", 1, NobodyAlive));

            foreach (PreviousSessionVerdict v in Enum.GetValues(typeof(PreviousSessionVerdict)))
            {
                Assert.AreEqual(v == PreviousSessionVerdict.AbnormalExit, SessionExitMarkerPolicy.ShouldCopyPreviousPlayerLog(v),
                    $"복사는 비정상 종료 다음에만 — {v}");
            }
        }

        [Test]
        public void 상한을_넘으면_끝에서_상한만큼만_복사_위치를_잡는다()
        {
            Assert.AreEqual(0, SessionExitMarkerPolicy.CopyStartOffset(100, 1000));
            Assert.AreEqual(0, SessionExitMarkerPolicy.CopyStartOffset(1000, 1000));
            Assert.AreEqual(1, SessionExitMarkerPolicy.CopyStartOffset(1001, 1000));
            Assert.Greater(SessionExitMarkerPolicy.MaxCopyBytes, 0);
        }

        // ------------------------------------------------------------------ 실행

        [Test]
        public void 첫_실행은_복사하지_않고_비정상_종료_다음_실행만_직전_로그를_복사한다()
        {
            byte[] content = Encoding.UTF8.GetBytes("멈춘 실행의 마지막 줄들\n[동결기록] ...\n");
            File.WriteAllBytes(_prev, content);

            SessionExitMarker.StartupResult first = SessionExitMarker.RunStartup(_ours, 10, _prev, NobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.NoMarker, first.Verdict);
            Assert.IsNull(first.CopiedFileName, "근거 없이 복사하지 않는다.");
            Assert.IsTrue(SessionExitMarker.IsStarted);
            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(File.ReadAllText(MarkerPath(_ours)), out string state, out int pid));
            Assert.AreEqual(SessionExitMarkerPolicy.RunningState, state);
            Assert.AreEqual(10, pid);

            // 실행 10이 종료 순서 없이 끝났다 → 실행 11이 복사한다.
            SessionExitMarker.StartupResult second = SessionExitMarker.RunStartup(_ours, 11, _prev, NobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.AbnormalExit, second.Verdict);
            Assert.AreEqual(SessionExitMarkerPolicy.CopySlotFileName(0), second.CopiedFileName);
            Assert.AreEqual(content.Length, second.SourceBytes);
            Assert.AreEqual(content.Length, second.CopiedBytes);
            Assert.IsFalse(second.Truncated);

            byte[] copy = File.ReadAllBytes(Path.Combine(_ours, second.CopiedFileName));
            CollectionAssert.AreEqual(content, Tail(copy, content.Length), "복사본 본문이 원본과 같아야 한다.");
            string header = Encoding.UTF8.GetString(copy, 0, copy.Length - content.Length);
            StringAssert.Contains(nameof(PreviousSessionVerdict.AbnormalExit), header);
            StringAssert.DoesNotContain(_logs, header, "복사본 머리줄에 사용자 경로를 남기지 않는다(파일 이름만).");

            // 정상 종료를 남기면 다음 실행은 복사하지 않는다.
            Assert.IsTrue(SessionExitMarker.WriteCleanExit(AppShutdownTrigger.ApplicationQuitting));
            SessionExitMarker.StartupResult third = SessionExitMarker.RunStartup(_ours, 12, _prev, NobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.CleanExit, third.Verdict);
            Assert.IsNull(third.CopiedFileName);
        }

        [Test]
        public void 종료_시작_표지에서_끊긴_실행은_비정상이_아니고_복사하지_않는다()
        {
            File.WriteAllText(_prev, "로그\n");
            SessionExitMarker.RunStartup(_ours, 60, _prev, NobodyAlive);
            Assert.IsTrue(SessionExitMarker.WriteExitStarted(AppShutdownTrigger.SessionEnding));
            StringAssert.StartsWith("state=" + SessionExitMarkerPolicy.ExitStartedState, File.ReadAllText(MarkerPath(_ours)));

            SessionExitMarker.StartupResult r = SessionExitMarker.RunStartup(_ours, 61, _prev, NobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.ExitStartedNotFinished, r.Verdict);
            Assert.IsNull(r.CopiedFileName, "종료 중 끊김은 세션 중 멈춤이 아니다 — 매일 밤 반복돼도 매일 아침 복사가 나면 안 된다.");
            Assert.IsNotNull(r.Note, "왜 복사하지 않았는지 로그 한 줄로 남긴다.");
            Assert.AreEqual(0, Directory.GetFiles(_ours, SessionExitMarkerPolicy.CopyFilePrefix + "*").Length);
        }

        /// <summary>
        /// ★ 원칙 3 — 우리 폴더 <b>밖</b> 파일(원본 <c>Player-prev.log</c> 포함)의 <b>내용(해시)·크기·수정 시각·이 러너가 반영하는 속성(읽기 전용·숨김)</b>이
        /// 그대로이고, 지워지거나 새로 생긴 파일이 없다.
        /// <para>4차(verify-change 3차 V4a): 3차 스냅샷은 내용·크기·수정 시각만 봐서 복사 뒤 원본에 읽기 전용 속성을 거는 변이가 초록이었다.</para>
        /// <para><b>★ 5차 정정 — 속성 축의 실제 범위.</b> 4차 이름("…속성이 그대로")은 속성 전체를 잠그는 것처럼 읽혔는데, 이 러너(Unity Mono, macOS)의
        /// <c>File.GetAttributes</c>는 <b>ReadOnly·Hidden만</b> 반영한다(verify-change 4차 하니스 실측 — System·Archive·Temporary·NotContentIndexed는 설정해도
        /// 읽히지 않는다). 그래서 원본에 <c>NotContentIndexed</c>를 거는 N3 변이가 이 테스트를 초록으로 통과했다. 그 밖의 속성 변경은 이 실행 테스트가
        /// 못 보고, 소스 감사(속성·시각 <b>세터</b>와 <c>SetAttributes</c> 금지 — <see cref="원칙3_소스_감사_표지_코드는_삭제_이동_속성변경_독점열기_없이_원본을_읽기_전용으로만_연다"/>)가
        /// <b>형태로</b> 막는다. Windows 파일 시스템에서의 속성 반영은 실기 항목이다. 속성 칸이 이 러너에서 살아 있는지는 테스트 첫머리의 교정(숨김 한 비트)이 매번 보인다.</para>
        /// <para><b>잠그지 않는 것(정직하게).</b> 접근 시각(읽으면 OS가 바꾼다 — 읽기 자체의 부수효과라 원칙 3 위반이 아니다), Unix의 inode 변경 시각,
        /// ACL·확장 속성. 원본을 연 동안 남의 핸들을 막는가(공유 모드)는 아래 별도 실행 테스트가 본다(열기 축만).</para>
        /// </summary>
        [Test]
        public void 원칙3_우리_폴더_밖_파일의_내용_크기_수정시각_읽기전용_숨김_속성이_그대로이고_지워지거나_생기지_않는다()
        {
            // 교정 — 이 러너에서 스냅샷의 속성 칸이 살아 있는가(숨김 한 비트를 켜서 칸이 달라지는지 본다). 깨지면 아래 속성 비교는 공허하다.
            string calibration = Path.Combine(_root, "attribute-calibration.txt");
            File.WriteAllText(calibration, "교정");
            string plainRow = SnapshotOutside(_ours)[Path.GetFullPath(calibration)];
            File.SetAttributes(calibration, File.GetAttributes(calibration) | FileAttributes.Hidden);
            string hiddenRow = SnapshotOutside(_ours)[Path.GetFullPath(calibration)];
            Assert.AreNotEqual(plainRow, hiddenRow, "교정 실패 — 이 러너의 스냅샷은 숨김 속성 변경도 보지 못한다. 속성 축은 공허하다(한계로 보고할 것).");
            File.SetAttributes(calibration, FileAttributes.Normal);
            File.Delete(calibration);

            File.WriteAllText(_prev, "원본 로그\n");
            string neighbor = Path.Combine(_logs, "Player.log");
            File.WriteAllText(neighbor, "지금 실행 로그\n");
            Directory.CreateDirectory(Path.Combine(_root, "persistent"));
            string sibling = Path.Combine(_root, "persistent", "stickmate_save.json");
            File.WriteAllText(sibling, "{\"save\":1}");
            var old = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (string p in new[] { _prev, neighbor, sibling }) File.SetLastWriteTimeUtc(p, old);

            Dictionary<string, string> before = SnapshotOutside(_ours);
            Assert.AreEqual(3, before.Count, "전제: 우리 폴더 밖 파일 3개를 지켜본다(교정 파일은 지웠다).");
            StringAssert.Contains("ro=False", before[Path.GetFullPath(_prev)], "전제: 원본은 읽기 전용이 아니다(아래 속성 비교가 공허하지 않게).");

            SessionExitMarker.RunStartup(_ours, 20, _prev, NobodyAlive);
            SessionExitMarker.StartupResult copied = SessionExitMarker.RunStartup(_ours, 21, _prev, NobodyAlive);
            Assert.IsNotNull(copied.CopiedFileName, "전제: 이 실행은 실제로 복사했어야 한다(아래 불변 단언이 공허하지 않게).");
            SessionExitMarker.WriteExitStarted(AppShutdownTrigger.SessionEnding);
            SessionExitMarker.WriteCleanExit(AppShutdownTrigger.SessionEnding);

            CollectionAssert.AreEquivalent(before, SnapshotOutside(_ours),
                "우리 폴더 밖의 파일(원본 Player-prev.log 포함)의 내용·크기·수정 시각·읽기 전용/숨김 속성이 바뀌었거나 사라졌거나 새로 생겼다 — 원칙 3 위반.");
            foreach (string written in Directory.GetFiles(_ours))
            {
                string name = Path.GetFileName(written);
                Assert.IsTrue(name == SessionExitMarkerPolicy.MarkerFileName || name.StartsWith(SessionExitMarkerPolicy.CopyFilePrefix, StringComparison.Ordinal),
                    $"우리 폴더에 예상 밖 파일이 생겼다: {name}");
            }
        }

        /// <summary>
        /// ★ 원칙 3 — 원본을 <b>읽는 동안</b>에도 다른 핸들(Unity·사용자·다른 프로그램)의 <b>읽기·쓰기 열기</b>를 막지 않는다.
        /// 원본이 열린 순간 프로덕션이 부르는 탐침(<c>SourceOpenedForTesting</c>)에서 실제로 열어 본다.
        /// <para>4차(verify-change 3차 V4b): 원본을 <c>FileShare.None</c>으로 여는 변이가 초록이었다.</para>
        /// <para><b>★ 5차 정정 — 이 러너가 재는 축은 열기(읽기·쓰기)뿐이다.</b> 4차 이름은 "…읽기_쓰기_이름바꾸기를_막지_않는다"였고 주석은 "Mono는 같은 프로세스
        /// 안의 공유 모드를 흉내 낸다"였다. verify-change 4차 하니스 실측으로 둘 다 틀렸다: Unity Mono(macOS)는 공유 모드를 <b>열기</b>에서만 흉내 내고,
        /// <c>FileShare.None</c> 핸들을 쥔 채로도 <c>File.Move</c>(이름 바꾸기)를 허용한다. 옛 이름 바꾸기 탐침은 어떤 공유 모드에서도 "ok"라 아무것도 재지
        /// 않았고, 삭제 공유를 뺀 두 번째 핸들을 쥐는 N4 변이가 초록이었다. 이름 바꾸기·삭제(<c>FileShare.Delete</c>) 축은 <b>Windows 실기 항목</b>이다
        /// (앱이 비정상 종료 다음 실행에서 <c>Player-prev.log</c>를 복사하는 동안 탐색기에서 그 파일 이름 바꾸기). 이 머신에서 그 축에 할 수 있는 것은
        /// 형태 감사뿐이다(원본 핸들의 공유 모드 형태 + 파일 핸들 수 — 소스 감사 테스트).</para>
        /// <para>음성 대조는 축마다 따로 둔다 — 독점 핸들을 쥔 채 같은 탐침이 막혀야 그 축의 "ok"가 공허하지 않다.</para>
        /// </summary>
        [Test]
        public void 원칙3_원본을_읽는_동안_다른_핸들의_읽기_쓰기_열기를_막지_않는다()
        {
            File.WriteAllText(_prev, "원본 로그\n");
            SessionExitMarker.RunStartup(_ours, 50, _prev, NobodyAlive);

            var outcomes = new List<string>();
            int calls = 0;
            SessionExitMarker.SourceOpenedForTesting = path =>
            {
                calls++;
                outcomes.Add(Probe("읽기", () => { using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { } }));
                outcomes.Add(Probe("쓰기", () => { using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { } }));
            };
            SessionExitMarker.StartupResult r = SessionExitMarker.RunStartup(_ours, 51, _prev, NobodyAlive);

            Assert.AreEqual(1, calls, "전제: 원본을 연 동안 탐침이 실제로 돌았어야 한다(아래 단언이 공허하지 않게).");
            Assert.IsNotNull(r.CopiedFileName, "전제: 이 실행은 실제로 복사했어야 한다.");
            CollectionAssert.AreEqual(new[] { "읽기:ok", "쓰기:ok" }, outcomes,
                "원본을 읽는 동안 남의 읽기·쓰기 열기를 막았다 — 사용자가 로그를 열어 보거나 Unity가 쓰는 것을 우리가 방해한다(원칙 3).");

            // 음성 대조(축마다) — 탐침 자체가 막힘을 알아보는가: 독점 핸들을 쥔 채 같은 탐침을 돌리면 막혀야 한다.
            using (new FileStream(_prev, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.AreNotEqual("읽기:ok", Probe("읽기", () => { using (new FileStream(_prev, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { } }),
                    "읽기 축 대조 실패 — 이 런타임은 열기 공유 모드를 흉내 내지 않는다. 위 읽기 단언은 공허하다(한계로 보고할 것).");
                Assert.AreNotEqual("쓰기:ok", Probe("쓰기", () => { using (new FileStream(_prev, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { } }),
                    "쓰기 축 대조 실패 — 이 런타임은 쓰기 열기 공유 모드를 흉내 내지 않는다. 위 쓰기 단언은 공허하다(한계로 보고할 것).");
            }
        }

        [Test]
        public void 상한을_넘는_원본은_끝부분만_복사한다()
        {
            var content = new byte[1000];
            for (int i = 0; i < content.Length; i++) content[i] = (byte)('a' + i % 26);
            File.WriteAllBytes(_prev, content);

            SessionExitMarker.RunStartup(_ours, 30, _prev, NobodyAlive, 100, DateTime.UtcNow);
            SessionExitMarker.StartupResult r = SessionExitMarker.RunStartup(_ours, 31, _prev, NobodyAlive, 100, DateTime.UtcNow);
            Assert.IsTrue(r.Truncated);
            Assert.AreEqual(100, r.CopiedBytes);
            byte[] copy = File.ReadAllBytes(Path.Combine(_ours, r.CopiedFileName));
            CollectionAssert.AreEqual(Tail(content, 100), Tail(copy, 100), "멈춘 순간은 로그의 끝에 있다 — 끝부분이어야 한다.");
            StringAssert.Contains("900", Encoding.UTF8.GetString(copy, 0, copy.Length - 100), "생략한 바이트 수를 머리줄에 남긴다.");
        }

        [Test]
        public void 복사본은_슬롯_링을_덮어써_파일_수가_늘지_않는다()
        {
            File.WriteAllText(_prev, "로그\n");
            int runs = SessionExitMarkerPolicy.CopySlotCount + 3;
            SessionExitMarker.RunStartup(_ours, 100, _prev, NobodyAlive);
            var seen = new HashSet<string>();
            for (int k = 1; k <= runs; k++)
            {
                SessionExitMarker.StartupResult r = SessionExitMarker.RunStartup(_ours, 100 + k, _prev, NobodyAlive);
                Assert.IsNotNull(r.CopiedFileName);
                seen.Add(r.CopiedFileName);
                // 파일 시각 해상도에 기대지 않는다 — 방금 쓴 슬롯을 가장 새것으로 못박는다.
                File.SetLastWriteTimeUtc(Path.Combine(_ours, r.CopiedFileName), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(k));
            }
            int copies = Directory.GetFiles(_ours, SessionExitMarkerPolicy.CopyFilePrefix + "*").Length;
            Assert.AreEqual(SessionExitMarkerPolicy.CopySlotCount, copies, "복사본 수가 슬롯 수를 넘었다.");
            Assert.AreEqual(SessionExitMarkerPolicy.CopySlotCount, seen.Count, "링이 모든 슬롯을 돌지 않았다.");
        }

        [Test]
        public void 동시_실행이면_복사하지_않고_원본이_없으면_표지만_쓴다()
        {
            File.WriteAllText(_prev, "로그\n");
            SessionExitMarker.RunStartup(_ours, 40, _prev, NobodyAlive);
            SessionExitMarker.StartupResult other = SessionExitMarker.RunStartup(_ours, 41, _prev, pid => pid == 40);
            Assert.AreEqual(PreviousSessionVerdict.OtherInstanceAlive, other.Verdict);
            Assert.IsNull(other.CopiedFileName);

            SessionExitMarker.StartupResult missing = SessionExitMarker.RunStartup(_ours, 42, Path.Combine(_logs, "없는-파일.log"), NobodyAlive);
            Assert.AreEqual(PreviousSessionVerdict.AbnormalExit, missing.Verdict);
            Assert.IsNull(missing.CopiedFileName);
            Assert.IsNotNull(missing.Note);
            Assert.IsTrue(SessionExitMarkerPolicy.TryParse(File.ReadAllText(MarkerPath(_ours)), out _, out int pid));
            Assert.AreEqual(42, pid, "복사하지 못해도 이번 실행의 표지는 써야 한다.");
        }

        [Test]
        public void 기동이_표지를_켜지_않았으면_종료_표지도_쓰지_않는다()
        {
            Assert.IsFalse(SessionExitMarker.IsStarted);
            Assert.IsFalse(SessionExitMarker.WriteExitStarted(AppShutdownTrigger.SessionEnding),
                "에디터·모바일·기동 실패에서는 아무 파일도 만들지 않는다.");
            Assert.IsFalse(SessionExitMarker.WriteCleanExit(AppShutdownTrigger.ApplicationQuitting),
                "에디터·모바일·기동 실패에서는 아무 파일도 만들지 않는다.");
            Assert.IsFalse(Directory.Exists(_ours));
        }

        // ------------------------------------------------------------------ 소스 감사 (원칙 3 · 배선 · 원복 앞 대기 없음)

        private static string MarkerSource() =>
            File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "SessionExitMarker.cs")).Replace("\r\n", "\n");

        /// <summary>
        /// 파일 메타데이터 <b>세터</b>(속성·읽기 전용·세 시각). ★ 5차(verify-change 4차 N3): 4차 금지 목록은 <c>"SetAttributes"</c> 글자만 봐서
        /// <c>new FileInfo(p).Attributes |= FileAttributes.NotContentIndexed;</c>가 비껴갔다. 비교(<c>==</c>)·람다(<c>=&gt;</c>)·읽기는 세터가 아니다.
        /// </summary>
        private static readonly Regex FileMetadataSetter = new Regex(
            @"\.\s*@?(?:Attributes|IsReadOnly|LastWriteTime(?:Utc)?|CreationTime(?:Utc)?|LastAccessTime(?:Utc)?)\s*(?:=(?![=>])|\|=|&=|\^=)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// 파일 핸들을 여는 <c>FileStream</c> 생성. ★ 5차(verify-change 4차 N4): <c>(FileAccess)1</c> 캐스트로 <c>"FileAccess.Read,"</c> 계수를 비껴간 두 번째 핸들을 개수로 막는다.
        /// ★ 5-b(verify-change 5차 S4): 한정 이름(<c>new System.IO.FileStream(</c>·<c>new global::System.IO.FileStream(</c>·<c>new IO.FileStream(</c>)도 센다 — 5차 식은 <c>new</c> 바로 뒤 <c>FileStream</c>만 봤다.
        /// </summary>
        private static readonly Regex FileStreamConstruction = new Regex(@"(?<![\w@])new\s+(?:@?\w+\s*(?:\.|::)\s*)*@?FileStream\s*\(", RegexOptions.CultureInvariant);

        /// <summary>
        /// ★ 5-b(verify-change 5차 S4) — 타깃 형식 <c>new(</c>. 5차에 <c>FileStream extra = new(…)</c>로 다섯 번째 핸들이 계수를 비껴갔다. 만들어지는 형식이 앞서 선언한 변수·인자·반환
        /// 형식에서 오면 글자로 FileStream 문맥인지 가를 수 없어 <b>문맥과 무관하게</b> 표지 코드에서 0건을 요구한다(대가는 감사 문서).
        /// </summary>
        private static readonly Regex TargetTypedNew = new Regex(@"(?<![\w@])new\s*\(", RegexOptions.CultureInvariant);

        /// <summary>
        /// 원칙 3 금지 형태(글자). ★ 5-b(verify-change 5차 S4): 비교는 <see cref="SpacedForm"/>로 토막 사이 공백·줄바꿈을 허용한다 — 5차 글자 비교는 <c>File . Copy(</c>를 못 봤다.
        /// </summary>
        private static readonly string[] ForbiddenForms =
        {
            "File.Delete(", "File.Move(", "File.Replace(", "File.Copy(", "Directory.Delete(", "Directory.Move(", ".MoveTo(", ".Delete(",
            "FileMode.Truncate", "FileMode.Append", "FileMode.OpenOrCreate",
            "SetAttributes", "SetLastWriteTime", "SetCreationTime", "SetLastAccessTime", "SetAccessControl", "FileShare.None",
        };

        /// <summary>
        /// ★ 5-b — 글자 형태를 "토막 사이 공백 허용" 식으로 바꾼다. 식별자 토막(<c>[A-Za-z0-9_]+</c>)과 기호 한 글자씩을 <c>\s*</c>로 잇는다.
        /// 앞뒤 경계는 두지 않는다 — 5차 글자 비교(부분 일치)와 같은 범위에서 공백만 넓힌다.
        /// </summary>
        private static Regex SpacedForm(string literal)
        {
            var parts = new List<string>();
            foreach (Match token in Regex.Matches(literal, @"[A-Za-z0-9_]+|[^A-Za-z0-9_\s]")) parts.Add(Regex.Escape(token.Value));
            return new Regex(string.Join(@"\s*", parts), RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// <c>FileStream</c>이 아닌 열기·통째 읽기/쓰기·암호화 형태 — 공유 모드를 코드가 정하지 못하거나(<c>File.ReadAllText</c>는 <c>FileShare.Read</c>로 열어
        /// 남의 쓰기를 막는다) 핸들 수 계수를 비껴간다.
        /// </summary>
        private static readonly Regex ExtraOpenForms = new Regex(
            @"(?<![\w@])File\s*\.\s*(?:Open|OpenRead|OpenWrite|OpenText|ReadAll\w*|ReadLines|WriteAll\w*|AppendAll\w*|AppendText|Create|CreateText|Encrypt|Decrypt)\s*\(" +
            @"|\.\s*(?:Open|OpenRead|OpenWrite|OpenText|Encrypt|Decrypt)\s*\(" +
            @"|(?<![\w@])new\s+Stream(?:Reader|Writer)\s*\(",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// ★ 원칙 3 소스 감사 — 표지 코드에 삭제·이동·속성/시각 변경·독점 열기·추가 핸들이 없고, 원본은 읽기 전용 공유 모드로만 연다.
        /// <para><b>★ 이 감사가 형태로만 막는 축(5차).</b> 이 러너가 실행으로 못 재는 두 축 — ReadOnly·Hidden 밖의 속성(N3), 남의 이름 바꾸기·삭제를 막는
        /// 공유 모드(N4) — 은 여기서 <b>형태</b>로만 막는다. 실제 동작(Windows 파일 시스템·커널 공유 검사)은 Windows 실기 항목이다.</para>
        /// <para><b>★ 텍스트 감사의 원리적 한계(리더 판정 2026-09-14: 쫓지 않는다 — C# 파서를 들여오지 않는다).</b> 유니코드 이스케이프 식별자
        /// (<c>File.SetAttributes(…)</c> — 컴파일러에게는 같은 이름, 글자 비교에게는 다른 글자), 계산된 이름의 리플렉션
        /// (<c>typeof(File).GetMethod("Set" + "Attributes")</c>), <c>dynamic</c>·표현식 트리. 일부러 꼬아야만 생기는 형태다.</para>
        /// <para><b>★ 5-b — S4 뒤에도 남는 형태(글자로 원리상 못 잡거나 일부러 넣지 않았다).</b> 5-b는 타깃 형식 <c>new(</c>·한정 이름 <c>FileStream</c> 생성·금지 형태의 토막 사이 공백을
        /// 더했다(교정: <see cref="원칙3_소스_감사_탐지식은_타깃_형식_new와_한정_이름과_공백_낀_금지_형태를_잡고_비슷한_무해_입력은_잡지_않는다"/>). 여전히 못 보는 것:
        /// 형식 별칭(<c>using FS = System.IO.FileStream;</c> 뒤 <c>new FS(…)</c>, <c>using F = System.IO.File;</c> 뒤 <c>F.Copy(…)</c>) ·
        /// <c>using static System.IO.File;</c> 뒤의 맨 <c>Copy(…)</c>·<c>Delete(…)</c> · 리플렉션 생성(<c>Activator.CreateInstance(typeof(FileStream), …)</c>) ·
        /// 다른 API가 여는 핸들(<c>FileInfo.Create()</c>·<c>FileInfo.CopyTo(…)</c>·<c>FileInfo.Replace(…)</c>, P/Invoke <c>CreateFile</c>, <c>SafeFileHandle</c>) ·
        /// 열거형 캐스트(<c>(FileShare)0</c>·<c>(FileMode)6</c> — N4의 <c>(FileAccess)1</c>과 같은 부류, 여는 곳이 <c>FileStream</c>이면 생성 수 계수가 대신 막는다) ·
        /// ★ 5-d(verify-change 2단계 추가) 셸 명령 실행(<c>Process.Start("cmd", "/c del …")</c>·<c>Process.Start("mv", …)</c> — 지우거나 옮기는 일이 다른 프로세스에서 일어나
        /// 이 파일의 글자로는 보이지 않는다) · <c>Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(…)</c>·<c>MoveFile(…)</c>(금지 목록의 글자는 <c>File.Delete(</c>·<c>File.Move(</c>라
        /// <c>FileSystem.DeleteFile(</c>을 잡지 않는다).
        /// <c>.CopyTo(</c>·<c>.Replace(</c>·<c>.Create(</c>는 <c>Stream.CopyTo</c>(새 핸들을 열지 않는다)·<c>string.Replace</c> 등과 글자로 구별되지 않아 넣지 않았다.
        /// <b>타깃 형식 <c>new(</c>는 문맥과 무관하게 0건을 요구한다</b> — 만들어지는 형식이 앞서 선언한 변수·인자·반환 형식에서 오면 FileStream 문맥인지 글자로 가를 수 없다.
        /// 대가: FileStream이 아닌 타깃 형식 생성이나 제네릭 제약 <c>where T : new()</c>도 빨개진다(조용한 초록보다 시끄러운 빨강 쪽 — 명시 형식으로 고쳐 쓰면 된다).</para>
        /// </summary>
        [Test]
        public void 원칙3_소스_감사_표지_코드는_삭제_이동_속성변경_독점열기_없이_원본을_읽기_전용으로만_연다()
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(MarkerSource(), null, blankInterpolationHoles: true);

            foreach (string forbidden in ForbiddenForms)
            {
                Assert.AreEqual(0, SpacedForm(forbidden).Matches(code).Count,
                    $"표지 코드에 '{forbidden}'(토막 사이 공백 무관)가 있다 — 원칙 3(유저 자산 불변) 감사 대상.");
            }
            Assert.AreEqual(0, TargetTypedNew.Matches(code).Count,
                "표지 코드에 타깃 형식 new(가 있다 — 만들어지는 형식을 글자로 알 수 없어 아래 FileStream 생성 수 계수를 비껴간다(S4). " +
                "명시 형식(new FileStream(…))으로 써라.");
            Assert.AreEqual(0, FileMetadataSetter.Matches(code).Count,
                "표지 코드에 파일 속성·시각 세터가 있다(예: FileInfo.Attributes |= …) — 원본 속성을 바꾸면 원칙 3 위반이고, ReadOnly·Hidden 밖의 속성은 이 러너의 실행 테스트가 못 본다(N3).");
            Assert.AreEqual(0, ExtraOpenForms.Matches(code).Count,
                "표지 코드에 FileStream이 아닌 열기·통째 읽기/쓰기 형태가 있다 — 공유 모드를 코드가 정하지 못하고 핸들 수 계수를 비껴간다.");
            Assert.AreEqual(4, FileStreamConstruction.Matches(code).Count,
                "파일 핸들은 표지 읽기·표지 쓰기·원본 읽기·복사본 쓰기 넷뿐이어야 한다 — 다섯 번째 핸들이 원본을 삭제 공유 없이 열면 Windows에서 사용자의 " +
                "이름 바꾸기·삭제를 막는데 이 러너(Mono)는 그 축을 재지 못한다(N4).");
            // 대조 — 스캐너가 코드를 지우지 않았고, 쓰기는 우리 파일 두 곳(표지·복사본)뿐이다.
            Assert.AreEqual(2, Regex.Matches(code, @"FileAccess\s*\.\s*Write\b").Count, "쓰기로 여는 곳은 표지와 복사본 두 곳뿐이어야 한다.");
            Assert.AreEqual(2, Regex.Matches(code, @"FileMode\s*\.\s*Create\s*,").Count, "우리 파일은 통째로 다시 쓴다(덮어쓰기).");
            Assert.AreEqual(2, Regex.Matches(code, @"FileAccess\s*\.\s*Read\s*,").Count, "원본(Player-prev.log)과 표지는 읽기 전용으로 연다.");
            Assert.IsTrue(Regex.IsMatch(code, @"FileAccess\s*\.\s*Read\s*,\s*FileShare\s*\.\s*ReadWrite\s*\|\s*FileShare\s*\.\s*Delete\s*\)"),
                "원본은 남의 읽기·쓰기·이름 바꾸기를 막지 않는 공유 모드로 연다(이름 바꾸기 축의 실제 동작은 Windows 실기 항목).");
        }

        /// <summary>교정 — 위 감사의 탐지식이 변이 원형(N3·N4)과 그 이웃 표기를 실제로 잡고, 읽기·비교·주석·문자열은 잡지 않는다.</summary>
        [Test]
        public void 원칙3_소스_감사_탐지식은_속성_세터와_추가_핸들을_잡고_읽기와_비교와_주석은_잡지_않는다()
        {
            string snippet =
                "new FileInfo(p).Attributes |= FileAttributes.NotContentIndexed;\n" +                                   // N3 원형
                "info.IsReadOnly = true;\n" +
                "fi . LastWriteTimeUtc\n    = t;\n" +
                "x.CreationTime= t;\n" +
                "var a = info.Attributes; if (info.Attributes == a) { } Func<FileInfo, FileAttributes> g = f => f.Attributes;\n" +
                "bool ro = fi.IsReadOnly; var w = fi.LastWriteTime;\n" +
                "// f.Attributes = x;\n" +
                "Log(\"f.IsReadOnly = true\"); Log($\"{fi.LastAccessTime}\");\n" +
                "using (new FileStream(fullSource, FileMode.Open, (FileAccess)1, FileShare.ReadWrite)) { }\n" +          // N4 원형
                "var s = File.OpenRead(p); var r = new StreamReader(p); var t2 = File . ReadAllText(p);\n" +
                "var m = FileMode.Open; bool e = File.Exists(p);\n";
            string code = SourceTextScanner.BlankCommentsAndStrings(snippet, null, blankInterpolationHoles: true);

            Assert.AreEqual(4, FileMetadataSetter.Matches(code).Count, "세터 넷(N3 원형 |= · IsReadOnly = · 줄바꿈 낀 LastWriteTimeUtc = · 붙여 쓴 CreationTime=)만 잡아야 한다.");
            Assert.AreEqual(1, FileStreamConstruction.Matches(code).Count, "N4 원형의 FileStream 생성 하나를 잡아야 한다.");
            Assert.AreEqual(3, ExtraOpenForms.Matches(code).Count, "File.OpenRead · new StreamReader · 공백 낀 File.ReadAllText 셋만 잡아야 한다(FileMode.Open·File.Exists는 아니다).");
            Assert.AreEqual(0, TargetTypedNew.Matches(code).Count, "위 합성 입력에는 타깃 형식 new(가 없다(음성 대조).");
        }

        /// <summary>
        /// ★ 5-b 교정(verify-change 5차 S4) — 5차 탐지식을 비껴간 세 형태를 합성 입력으로 잡고(빨강 쪽), 이름이 비슷한 무해 입력은 잡지 않는다(초록 쪽).
        /// (1) 타깃 형식 <c>new(</c> — S4 원형(<c>FileStream extra = new(…)</c>)·using 선언·앞서 선언한 변수에 대입·반환. (2) 한정 이름 <c>FileStream</c> 생성.
        /// (3) 금지 형태의 토막 사이 공백·줄바꿈(<c>File . Copy(</c>) — 목록의 <b>모든</b> 형태를 같은 규칙으로 벌려 본다.
        /// </summary>
        [Test]
        public void 원칙3_소스_감사_탐지식은_타깃_형식_new와_한정_이름과_공백_낀_금지_형태를_잡고_비슷한_무해_입력은_잡지_않는다()
        {
            string snippet =
                "using (FileStream extra = new(fullSource, FileMode.Open, (FileAccess)1, FileShare.ReadWrite)) { }\n" +   // S4 원형
                "using FileStream b = new (p, FileMode.Open);\n" +
                "c = new(p, FileMode.Open);\n" +
                "return new(p, FileMode.Open);\n" +
                "var d = new System.IO.FileStream(p, FileMode.Open);\n" +
                "var e = new global::System.IO.FileStream(p, FileMode.Open);\n" +
                "var f = new IO . FileStream (p, FileMode.Open);\n" +
                // 무해 — 이름이 비슷하거나 new 뒤에 다른 것이 온다.
                "var g = new FileStreamOptions(); var h = new MyFileStream(p); var j = new byte[4]; var k = new[] { 1 }; renew(p); var m = anew (p);\n" +
                "// FileStream q = new(p);\n" +
                "Log(\"new System.IO.FileStream(p)\");\n";
            string code = SourceTextScanner.BlankCommentsAndStrings(snippet, null, blankInterpolationHoles: true);

            Assert.AreEqual(4, TargetTypedNew.Matches(code).Count, "타깃 형식 new( 넷(S4 원형·using 선언·대입·반환)만 잡아야 한다 — 주석 속 것과 renew(·anew (는 아니다.");
            Assert.AreEqual(3, FileStreamConstruction.Matches(code).Count,
                "한정 이름 FileStream 생성 셋(System.IO · global:: · 공백 낀 IO .)만 잡아야 한다 — FileStreamOptions·MyFileStream·문자열 속 것은 아니다.");

            Assert.AreEqual(1, SpacedForm("File.Copy(").Matches(SourceTextScanner.BlankCommentsAndStrings("File . Copy (a, b);\n", null, blankInterpolationHoles: true)).Count,
                "S4 원형 — 점 주변 공백 낀 File . Copy(를 잡아야 한다.");
            foreach (string forbidden in ForbiddenForms)
            {
                var tokens = new List<string>();
                foreach (Match token in Regex.Matches(forbidden, @"[A-Za-z0-9_]+|[^A-Za-z0-9_\s]")) tokens.Add(token.Value);
                string spaced = string.Join(" \n\t ", tokens);
                Regex form = SpacedForm(forbidden);
                Assert.IsTrue(form.IsMatch(SourceTextScanner.BlankCommentsAndStrings(forbidden + "\n", null, blankInterpolationHoles: true)), $"붙여 쓴 '{forbidden}'를 못 잡는다.");
                Assert.IsTrue(form.IsMatch(SourceTextScanner.BlankCommentsAndStrings(spaced + "\n", null, blankInterpolationHoles: true)), $"토막 사이 공백·줄바꿈을 낀 '{forbidden}'를 못 잡는다.");
                Assert.IsFalse(form.IsMatch(SourceTextScanner.BlankCommentsAndStrings("// " + spaced.Replace("\n", " ") + "\nLog(\"" + forbidden + "\");\n", null, blankInterpolationHoles: true)),
                    $"주석·문자열 속 '{forbidden}'를 잡았다(음성 대조).");
            }
            Assert.IsFalse(SpacedForm("File.Copy(").IsMatch("FileCopy(a, b); File.CopyTo(a); File.Exists(a);"), "이름이 비슷한 무해 입력(FileCopy( · File.CopyTo( · File.Exists()을 잡았다.");
        }

        [Test]
        public void 종료_시작_표지는_디스크_동기화를_하지_않아_원복_앞에_기다리는_단계가_없다()
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(MarkerSource(), null, blankInterpolationHoles: true);
            string exitStarted = SourceTextScanner.BlockMemberBody(code, "public static bool WriteExitStarted(");
            string cleanExit = SourceTextScanner.BlockMemberBody(code, "public static bool WriteCleanExit(");
            string writeMarker = SourceTextScanner.BlockMemberBody(code, "private static void WriteMarker(");
            Assert.IsNotNull(exitStarted, "종료 시작 표지 본문을 찾지 못했다.");
            Assert.IsNotNull(cleanExit, "정상 종료 표지 본문을 찾지 못했다.");
            Assert.IsNotNull(writeMarker, "표지 쓰기 본문을 찾지 못했다.");

            Assert.IsTrue(Regex.IsMatch(exitStarted, @"flushToDisk\s*:\s*false"),
                "종료 시작 표지가 디스크 동기화를 한다 — 작업표시줄 원복 앞에 기다리는 단계가 생긴다(R-1과 충돌).");
            Assert.IsFalse(Regex.IsMatch(exitStarted, @"Flush\s*\(\s*true\s*\)"), "종료 시작 표지 본문에 직접 동기화가 있다.");
            Assert.IsTrue(Regex.IsMatch(cleanExit, @"flushToDisk\s*:\s*true"), "정상 종료 표지는 전원 차단 대비로 동기화해야 한다(대조).");
            Assert.IsTrue(Regex.IsMatch(writeMarker, @"if\s*\(\s*flushToDisk\s*\)\s*stream\s*\.\s*Flush\s*\(\s*true\s*\)"),
                "동기화가 flushToDisk에 걸려 있지 않다 — 위 두 단언이 공허해진다.");
        }

        [Test]
        public void 기동과_종료_순서가_표지에_배선돼_있다()
        {
            string boot = SourceTextScanner.BlankCommentsAndStrings(
                File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "FreezeWatchdog.cs")), null, blankInterpolationHoles: true);
            Assert.IsTrue(Regex.IsMatch(boot, nameof(SessionExitMarker) + @"\s*\.\s*" + nameof(SessionExitMarker.RunStartup) + @"\s*\("));
            Assert.IsTrue(Regex.IsMatch(boot, nameof(Application) + @"\s*\.\s*" + nameof(Application.consoleLogPath)),
                "직전 로그 위치는 Unity가 알려 준 로그 폴더에서 찾는다(플랫폼마다 폴더가 다르다).");
            Assert.IsTrue(Regex.IsMatch(boot, nameof(SessionExitMarkerPolicy) + @"\s*\.\s*" + nameof(SessionExitMarkerPolicy.PreviousPlayerLogFileName)));
            Match activate = Regex.Match(boot, nameof(FreezeForensicsLog) + @"\s*\.\s*" + nameof(FreezeForensicsLog.Activate) + @"\s*\(");
            Match record = Regex.Match(boot, @"RecordPreviousSessionVerdict\s*\(\s*directory\s*,\s*pid\s*\)");
            Assert.IsTrue(activate.Success);
            Assert.IsTrue(record.Success);
            Assert.Greater(record.Index, activate.Index, "표지는 동결 원장과 같은 폴더(우리 폴더)에서, 원장 활성 뒤에 돈다.");

            var order = new List<AppShutdownStep>(AppShutdownSequence.Order);
            CollectionAssert.Contains(order, AppShutdownStep.MarkExitStarted);
            CollectionAssert.Contains(order, AppShutdownStep.MarkCleanExit);
        }

        // ------------------------------------------------------------------ 도우미

        private static string Probe(string label, Action action)
        {
            try
            {
                action();
                return label + ":ok";
            }
            catch (Exception e)
            {
                return label + ":" + e.GetType().Name;
            }
        }

        private static byte[] Tail(byte[] bytes, int length)
        {
            var tail = new byte[length];
            Array.Copy(bytes, bytes.Length - length, tail, 0, length);
            return tail;
        }

        private Dictionary<string, string> SnapshotOutside(string excludedDirectory)
        {
            string excluded = Path.GetFullPath(excludedDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            using (SHA256 sha = SHA256.Create())
            {
                foreach (string file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(file);
                    if (full.StartsWith(excluded, StringComparison.Ordinal)) continue;
                    var info = new FileInfo(full);
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(full)));
                    snapshot[full] = hash + "|" + info.Length + "|" + File.GetLastWriteTimeUtc(full).Ticks +
                                     "|" + File.GetAttributes(full) + "|ro=" + info.IsReadOnly;
                }
            }
            return snapshot;
        }
    }
}
