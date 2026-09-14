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
    /// ★ 2026-09-14 (3차 D, 4차) — 정상 종료 표지와 비정상 종료 다음 실행의 <c>Player-prev.log</c> 복사.
    /// 파일 조작은 전부 임시 폴더에서 <b>실행</b>한다. 두 종료 입구가 표지를 남기는지(R-6)와 원복 도중 끊김(R5)은 <c>SessionEndShutdownTests</c>가 본다.
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
        /// ★ 원칙 3 — 우리 폴더 <b>밖</b> 파일(원본 <c>Player-prev.log</c> 포함)의 <b>내용(해시)·크기·수정 시각·파일 속성(읽기 전용 포함)</b>이 그대로이고,
        /// 지워지거나 새로 생긴 파일이 없다.
        /// <para><b>잠그지 않는 것(정직하게).</b> 접근 시각(읽으면 OS가 바꾼다 — 읽기 자체의 부수효과라 원칙 3 위반이 아니다), Unix의 inode 변경 시각,
        /// ACL·확장 속성. 원본을 연 동안 남의 핸들을 막는가(공유 모드)는 아래 별도 실행 테스트가 본다.</para>
        /// <para>4차(verify-change 3차 V4a): 3차 스냅샷은 내용·크기·수정 시각만 봐서 복사 뒤 원본에 읽기 전용 속성을 거는 변이가 초록이었다.</para>
        /// </summary>
        [Test]
        public void 원칙3_우리_폴더_밖_파일의_내용_크기_수정시각_속성이_그대로이고_지워지거나_생기지_않는다()
        {
            File.WriteAllText(_prev, "원본 로그\n");
            string neighbor = Path.Combine(_logs, "Player.log");
            File.WriteAllText(neighbor, "지금 실행 로그\n");
            Directory.CreateDirectory(Path.Combine(_root, "persistent"));
            string sibling = Path.Combine(_root, "persistent", "stickmate_save.json");
            File.WriteAllText(sibling, "{\"save\":1}");
            var old = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (string p in new[] { _prev, neighbor, sibling }) File.SetLastWriteTimeUtc(p, old);

            Dictionary<string, string> before = SnapshotOutside(_ours);
            Assert.AreEqual(3, before.Count, "전제: 우리 폴더 밖 파일 3개를 지켜본다.");
            StringAssert.Contains("ro=False", before[Path.GetFullPath(_prev)], "전제: 원본은 읽기 전용이 아니다(아래 속성 비교가 공허하지 않게).");

            SessionExitMarker.RunStartup(_ours, 20, _prev, NobodyAlive);
            SessionExitMarker.StartupResult copied = SessionExitMarker.RunStartup(_ours, 21, _prev, NobodyAlive);
            Assert.IsNotNull(copied.CopiedFileName, "전제: 이 실행은 실제로 복사했어야 한다(아래 불변 단언이 공허하지 않게).");
            SessionExitMarker.WriteExitStarted(AppShutdownTrigger.SessionEnding);
            SessionExitMarker.WriteCleanExit(AppShutdownTrigger.SessionEnding);

            CollectionAssert.AreEquivalent(before, SnapshotOutside(_ours),
                "우리 폴더 밖의 파일(원본 Player-prev.log 포함)의 내용·크기·수정 시각·속성이 바뀌었거나 사라졌거나 새로 생겼다 — 원칙 3 위반.");
            foreach (string written in Directory.GetFiles(_ours))
            {
                string name = Path.GetFileName(written);
                Assert.IsTrue(name == SessionExitMarkerPolicy.MarkerFileName || name.StartsWith(SessionExitMarkerPolicy.CopyFilePrefix, StringComparison.Ordinal),
                    $"우리 폴더에 예상 밖 파일이 생겼다: {name}");
            }
        }

        /// <summary>
        /// ★ 원칙 3 — 원본을 <b>읽는 동안</b>에도 다른 핸들(Unity·사용자·다른 프로그램)의 읽기·쓰기 열기와 이름 바꾸기(삭제 공유)를 막지 않는다.
        /// 원본이 열린 순간 프로덕션이 부르는 탐침(<c>SourceOpenedForTesting</c>)에서 실제로 열어 본다.
        /// <para>4차(verify-change 3차 V4b): 원본을 <c>FileShare.None</c>으로 여는 변이가 초록이었다. ★ 한계: 이 실행은 에디터의 Mono에서
        /// 돈다(Mono는 같은 프로세스 안의 공유 모드를 흉내 낸다). Windows 커널의 공유 검사는 실기에서만 관측된다.</para>
        /// </summary>
        [Test]
        public void 원칙3_원본을_읽는_동안_다른_핸들의_읽기_쓰기_이름바꾸기를_막지_않는다()
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
                outcomes.Add(Probe("이름바꾸기", () =>
                {
                    string moved = path + ".probe";
                    File.Move(path, moved);
                    File.Move(moved, path);
                }));
            };
            SessionExitMarker.StartupResult r = SessionExitMarker.RunStartup(_ours, 51, _prev, NobodyAlive);

            Assert.AreEqual(1, calls, "전제: 원본을 연 동안 탐침이 실제로 돌았어야 한다(아래 단언이 공허하지 않게).");
            Assert.IsNotNull(r.CopiedFileName, "전제: 이 실행은 실제로 복사했어야 한다.");
            CollectionAssert.AreEqual(new[] { "읽기:ok", "쓰기:ok", "이름바꾸기:ok" }, outcomes,
                "원본을 읽는 동안 남의 읽기·쓰기·이름 바꾸기를 막았다 — 사용자가 로그를 열거나 옮기는 것을 우리가 방해한다(원칙 3).");

            // 음성 대조 — 탐침 자체가 막힘을 알아보는가: 독점 핸들을 쥔 채 같은 탐침을 돌리면 막혀야 한다.
            using (new FileStream(_prev, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.AreNotEqual("읽기:ok", Probe("읽기", () => { using (new FileStream(_prev, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { } }),
                    "대조 실패 — 이 런타임은 같은 프로세스 안 공유 모드를 흉내 내지 않는다. 위 단언은 공허하다(한계로 보고할 것).");
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

        [Test]
        public void 원칙3_소스_감사_표지_코드는_삭제_이동_속성변경_독점열기_없이_원본을_읽기_전용으로만_연다()
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(MarkerSource(), null, blankInterpolationHoles: true);

            foreach (string forbidden in new[] { "File.Delete(", "File.Move(", "File.Replace(", "File.Copy(", "Directory.Delete(",
                         "Directory.Move(", ".MoveTo(", ".Delete(", "FileMode.Truncate", "FileMode.Append", "FileMode.OpenOrCreate",
                         "SetAttributes", "SetLastWriteTime", "SetCreationTime", "SetLastAccessTime", "SetAccessControl", "FileShare.None" })
            {
                StringAssert.DoesNotContain(forbidden, code, $"표지 코드에 '{forbidden}'가 있다 — 원칙 3(유저 자산 불변) 감사 대상.");
            }
            // 대조 — 스캐너가 코드를 지우지 않았고, 쓰기는 우리 파일 두 곳(표지·복사본)뿐이다.
            Assert.AreEqual(2, Regex.Matches(code, @"FileAccess\s*\.\s*Write\b").Count, "쓰기로 여는 곳은 표지와 복사본 두 곳뿐이어야 한다.");
            Assert.AreEqual(2, Regex.Matches(code, @"FileMode\s*\.\s*Create\s*,").Count, "우리 파일은 통째로 다시 쓴다(덮어쓰기).");
            Assert.AreEqual(2, Regex.Matches(code, @"FileAccess\s*\.\s*Read\s*,").Count, "원본(Player-prev.log)과 표지는 읽기 전용으로 연다.");
            Assert.IsTrue(Regex.IsMatch(code, @"FileAccess\s*\.\s*Read\s*,\s*FileShare\s*\.\s*ReadWrite\s*\|\s*FileShare\s*\.\s*Delete\s*\)"),
                "원본은 남의 읽기·쓰기·이름 바꾸기를 막지 않는 공유 모드로 연다.");
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
